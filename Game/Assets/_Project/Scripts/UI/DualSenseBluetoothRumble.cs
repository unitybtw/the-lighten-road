using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.HID;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace TheLightenRoad.UI
{
    /// <summary>DualSense Bluetooth output for the Input System 1.17 USB-only haptics path.</summary>
    public static class DualSenseBluetoothRumble
    {
        private static readonly Dictionary<int, double> NextMotorWrite = new Dictionary<int, double>();
        private static readonly Dictionary<int, byte> Sequences = new Dictionary<int, byte>();
        public static long LastCommandResult { get; private set; }

        public static bool IsBluetooth(Gamepad pad)
        {
            if (!(pad is DualSenseGamepadHID) || pad.description.interfaceName != "HID") return false;
            return IsBluetoothDescriptor(pad.description.capabilities);
        }

        public static bool IsBluetoothDescriptor(string capabilities)
        {
            if (string.IsNullOrEmpty(capabilities)) return false;
            try
            {
                var descriptor = HID.HIDDeviceDescriptor.FromJson(capabilities);
                return descriptor.vendorId == 0x054c && (descriptor.productId == 0x0ce6 || descriptor.productId == 0x0df2)
                    && (descriptor.outputReportSize == 78 ||
                        (descriptor.inputReportSize == 78 && descriptor.outputReportSize >= 78));
            }
            catch (Exception) { return false; }
        }

        public static bool Send(Gamepad pad, float low, float high, float rightTrigger = 0f)
        {
            if (pad == null || !pad.added) return false;
            // The Editor can render hundreds of frames per second. Limit motor traffic so it
            // cannot crowd out the 47 Hz speaker stream. Zero output always releases immediately.
            bool stopping = low <= 0f && high <= 0f && rightTrigger <= 0f;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!stopping && NextMotorWrite.TryGetValue(pad.deviceId, out double next) && now < next) return true;
            NextMotorWrite[pad.deviceId] = stopping ? 0 : now + (DualSenseSpeaker.IsPlaying ? 1.0 / 30 : 1.0 / 60);
            byte sequence=NextSequence(pad.deviceId);
            var command = BluetoothOutputCommand.Create(low, high, sequence,rightTrigger,true);
            LastCommandResult = pad.ExecuteCommand(ref command);
            return LastCommandResult >= 0;
        }

        public static byte NextSequence(int deviceId)
        {
            lock (Sequences)
            {
                Sequences.TryGetValue(deviceId,out byte sequence);
                Sequences[deviceId]=(byte)((sequence+1)&15);return sequence;
            }
        }

        // Optional light R2 resistance shares the motor packet. L2, LEDs and audio levels are never configured.
        public static byte[] BuildReport(float low, float high, byte sequence, float rightTrigger = 0f, bool updateTrigger = false)
        {
            var report = new byte[78];
            report[0] = 0x31;
            report[1] = (byte)((sequence & 15) << 4);
            report[2] = 0x10;
            report[3] = 0x03; // Rumble emulation and selection of rumble over audio haptics.
            report[5] = EncodeMotor(high);
            report[6] = EncodeMotor(low);
            if(updateTrigger)
            {
                report[3]|=0x04; // Update right adaptive trigger only.
                bool active=!float.IsNaN(rightTrigger) && rightTrigger>0f;
                report[13]=active?(byte)0x01:(byte)0x05; // Constant resistance / explicit release.
                report[14]=active?(byte)80:(byte)0; // Start after the first third of R2 travel.
                report[15]=active?EncodeMotor(Mathf.Min(rightTrigger,.22f)):(byte)0;
            }
            uint crc = 0xffffffff;
            crc = CrcByte(crc, 0xa2); // Bluetooth HID output transaction prefix.
            for (int i = 0; i < 74; i++) crc = CrcByte(crc, report[i]);
            crc = ~crc;
            for (int i = 0; i < 4; i++) report[74+i] = (byte)(crc >> (i*8));
            return report;
        }
        private static byte EncodeMotor(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Clamp01(value)*255f);
        private static uint CrcByte(uint crc, byte value)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            return crc;
        }

        [StructLayout(LayoutKind.Explicit, Size = InputDeviceCommand.BaseCommandSize + 78)]
        public struct BluetoothOutputCommand : IInputDeviceCommandInfo
        {
            public FourCC typeStatic => new FourCC('H','I','D','O');
            [FieldOffset(0)] public InputDeviceCommand Header;
            [FieldOffset(InputDeviceCommand.BaseCommandSize)] public byte ReportId;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+1)] public byte Sequence;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+2)] public byte Tag;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+3)] public byte Flags;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+5)] public byte HighMotor;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+6)] public byte LowMotor;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+74)] public uint Crc;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+13)] public byte TriggerMode;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+14)] public byte TriggerStart;
            [FieldOffset(InputDeviceCommand.BaseCommandSize+15)] public byte TriggerForce;
            public static BluetoothOutputCommand Create(float low, float high, byte sequence, float rightTrigger = 0f, bool updateTrigger = false)
            {
                var report = BuildReport(low, high, sequence,rightTrigger,updateTrigger);
                return new BluetoothOutputCommand
                {
                    Header = new InputDeviceCommand(new FourCC('H','I','D','O'), InputDeviceCommand.BaseCommandSize+78),
                    ReportId=report[0], Sequence=report[1], Tag=report[2], Flags=report[3],
                    HighMotor=report[5], LowMotor=report[6],
                    TriggerMode=report[13],TriggerStart=report[14],TriggerForce=report[15],
                    Crc=(uint)report[74] | (uint)report[75]<<8 | (uint)report[76]<<16 | (uint)report[77]<<24
                };
            }
        }
    }
}
