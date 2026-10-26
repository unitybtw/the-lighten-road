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
    /// <summary>DualSense adaptive trigger feedback modes (Sony HID protocol specification).</summary>
    public enum DualSenseTriggerMode : byte
    {
        Off = 0x05,                 // Explicit release / actuator resting
        Continuous = 0x01,          // Constant resistance (startPosition, force)
        Section = 0x02,             // Section resistance / two-stage trigger snap (startPosition, endPosition, force)
        Vibration = 0x06            // Frequency vibration / pulse (startPosition, amplitude, frequency)
    }

    /// <summary>Represents an 11-byte DualSense adaptive trigger effect payload.</summary>
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DualSenseTriggerEffect : System.IEquatable<DualSenseTriggerEffect>
    {
        public DualSenseTriggerMode Mode;
        public byte Param1; // startPosition
        public byte Param2; // force (Continuous) / endPosition (Section) / amplitude (Vibration)
        public byte Param3; // force (Section) / frequency (Vibration)
        public byte Param4;
        public byte Param5;
        public byte Param6;
        public byte Param7;
        public byte Param8;
        public byte Param9;
        public byte Param10;

        public bool IsActive => Mode != DualSenseTriggerMode.Off;

        public static DualSenseTriggerEffect Off => new DualSenseTriggerEffect { Mode = DualSenseTriggerMode.Off };

        /// <summary>Applies continuous resistance across trigger travel.</summary>
        public static DualSenseTriggerEffect Continuous(float start, float force)
        {
            if (force <= 0f) return Off;
            return new DualSenseTriggerEffect
            {
                Mode = DualSenseTriggerMode.Continuous,
                Param1 = (byte)Mathf.Clamp(Mathf.RoundToInt(start * 255f), 0, 255),
                Param2 = (byte)Mathf.Clamp(Mathf.RoundToInt(force * 255f), 0, 255)
            };
        }

        /// <summary>Applies bounded section resistance with a tactile break-point snap (two-stage trigger).</summary>
        public static DualSenseTriggerEffect Section(float start, float end, float force)
        {
            if (force <= 0f) return Off;
            return new DualSenseTriggerEffect
            {
                Mode = DualSenseTriggerMode.Section,
                Param1 = (byte)Mathf.Clamp(Mathf.RoundToInt(start * 255f), 0, 255),
                Param2 = (byte)Mathf.Clamp(Mathf.RoundToInt(end * 255f), 0, 255),
                Param3 = (byte)Mathf.Clamp(Mathf.RoundToInt(force * 255f), 0, 255)
            };
        }

        /// <summary>Applies frequency oscillation / trigger vibration under the player's finger.</summary>
        public static DualSenseTriggerEffect Vibration(float start, float amplitude, float frequency)
        {
            if (amplitude <= 0f) return Off;
            return new DualSenseTriggerEffect
            {
                Mode = DualSenseTriggerMode.Vibration,
                Param1 = (byte)Mathf.Clamp(Mathf.RoundToInt(start * 255f), 0, 255),
                Param2 = (byte)Mathf.Clamp(Mathf.RoundToInt(amplitude * 255f), 0, 255),
                Param3 = (byte)Mathf.Clamp(Mathf.RoundToInt(frequency * 255f), 0, 255)
            };
        }

        public void WriteTo(byte[] destination, int offset)
        {
            if (destination == null || offset + 11 > destination.Length) return;
            destination[offset] = (byte)Mode;
            destination[offset + 1] = Param1;
            destination[offset + 2] = Param2;
            destination[offset + 3] = Param3;
            destination[offset + 4] = Param4;
            destination[offset + 5] = Param5;
            destination[offset + 6] = Param6;
            destination[offset + 7] = Param7;
            destination[offset + 8] = Param8;
            destination[offset + 9] = Param9;
            destination[offset + 10] = Param10;
        }

        public bool Equals(DualSenseTriggerEffect other) =>
            Mode == other.Mode && Param1 == other.Param1 && Param2 == other.Param2 && Param3 == other.Param3;

        public override bool Equals(object obj) => obj is DualSenseTriggerEffect other && Equals(other);
        public override int GetHashCode() => ((int)Mode * 397) ^ (Param1 << 16) ^ (Param2 << 8) ^ Param3;
        public static bool operator ==(DualSenseTriggerEffect a, DualSenseTriggerEffect b) => a.Equals(b);
        public static bool operator !=(DualSenseTriggerEffect a, DualSenseTriggerEffect b) => !a.Equals(b);
    }

    /// <summary>DualSense Bluetooth output for the Input System 1.17 USB-only haptics path.</summary>
    public static class DualSenseBluetoothRumble
    {
        private static readonly Dictionary<int, double> NextMotorWrite = new Dictionary<int, double>();
        private static readonly Dictionary<int, byte> Sequences = new Dictionary<int, byte>();
        private static readonly Dictionary<int, DualSenseTriggerEffect> LastSentLeftTrigger = new Dictionary<int, DualSenseTriggerEffect>();
        private static readonly Dictionary<int, DualSenseTriggerEffect> LastSentRightTrigger = new Dictionary<int, DualSenseTriggerEffect>();
        public static long LastCommandResult { get; private set; }

        public static Color32 CurrentLightbarColor { get; set; } = new Color32(235, 140, 25, 255); // Warm Amber Gold
        public static bool LightbarEnabled { get; set; } = true;

        public static void ResetState()
        {
            NextMotorWrite.Clear();
            Sequences.Clear();
            LastSentLeftTrigger.Clear();
            LastSentRightTrigger.Clear();
        }

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

        public static bool Send(Gamepad pad, float low, float high, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger, Color32? lightbar = null)
        {
            if (pad == null || !pad.added) return false;
            Color32 activeColor = lightbar ?? (LightbarEnabled ? CurrentLightbarColor : new Color32(0, 0, 0, 255));
            bool stopping = low <= 0f && high <= 0f && !leftTrigger.IsActive && !rightTrigger.IsActive;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!stopping && NextMotorWrite.TryGetValue(pad.deviceId, out double next) && now < next) return true;
            NextMotorWrite[pad.deviceId] = stopping ? 0 : now + (DualSenseSpeaker.IsPlaying ? 1.0 / 30 : 1.0 / 60);
            byte sequence = NextSequence(pad.deviceId);

            bool needUpdateRight = !LastSentRightTrigger.TryGetValue(pad.deviceId, out var prevR) || !prevR.Equals(rightTrigger);
            bool needUpdateLeft = !LastSentLeftTrigger.TryGetValue(pad.deviceId, out var prevL) || !prevL.Equals(leftTrigger);

            LastSentRightTrigger[pad.deviceId] = rightTrigger;
            LastSentLeftTrigger[pad.deviceId] = leftTrigger;

            var command = BluetoothOutputCommand.Create(low, high, sequence, leftTrigger, rightTrigger, needUpdateLeft, needUpdateRight, activeColor);
            LastCommandResult = pad.ExecuteCommand(ref command);
            return LastCommandResult >= 0;
        }

        public static bool SetLightbarColor(Gamepad pad, Color32 color)
        {
            CurrentLightbarColor = color;
            if (pad == null || !pad.added) return false;
            if (IsBluetooth(pad))
            {
                return Send(pad, 0f, 0f, DualSenseTriggerEffect.Off, DualSenseTriggerEffect.Off, color);
            }
            if (pad is DualShockGamepad ds)
            {
                ds.SetLightBarColor(color);
                return true;
            }
            return false;
        }

        public static bool Send(Gamepad pad, float low, float high, float rightTrigger = 0f)
        {
            if (pad == null || !pad.added) return false;
            bool stopping = low <= 0f && high <= 0f && rightTrigger <= 0f;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!stopping && NextMotorWrite.TryGetValue(pad.deviceId, out double next) && now < next) return true;
            NextMotorWrite[pad.deviceId] = stopping ? 0 : now + (DualSenseSpeaker.IsPlaying ? 1.0 / 30 : 1.0 / 60);
            byte sequence = NextSequence(pad.deviceId);
            var command = BluetoothOutputCommand.Create(low, high, sequence, rightTrigger, true);
            LastCommandResult = pad.ExecuteCommand(ref command);
            return LastCommandResult >= 0;
        }

        public static byte NextSequence(int deviceId)
        {
            lock (Sequences)
            {
                Sequences.TryGetValue(deviceId, out byte sequence);
                Sequences[deviceId] = (byte)((sequence + 1) & 15);
                return sequence;
            }
        }

        /// <summary>Constructs a 78-byte DualSense Bluetooth output report with independent left and right adaptive trigger configurations and RGB lightbar support.</summary>
        public static byte[] BuildReport(float low, float high, byte sequence, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger, bool updateLeft, bool updateRight, Color32? lightbarColor = null)
        {
            var report = new byte[78];
            report[0] = 0x31;
            report[1] = (byte)((sequence & 15) << 4);
            report[2] = 0x10;
            report[3] = 0x03; // Rumble emulation flag

            report[5] = EncodeMotor(high);
            report[6] = EncodeMotor(low);

            if (updateRight)
            {
                report[3] |= 0x04; // Bit 2 = right trigger
                rightTrigger.WriteTo(report, 13);
            }

            if (updateLeft)
            {
                report[3] |= 0x08; // Bit 3 = left trigger
                leftTrigger.WriteTo(report, 24);
            }

            if (lightbarColor.HasValue)
            {
                report[4] |= 0x04; // Enable lightbar control (DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE)
                report[41] |= 0x02; // Enable lightbar setup (DS_OUTPUT_VALID_FLAG2_LIGHTBAR_SETUP_CONTROL_ENABLE)
                report[44] = 0x02;  // Custom light out mode (DS_OUTPUT_LIGHTBAR_SETUP_LIGHT_OUT)
                report[45] = 0x00;  // Full brightness
                report[47] = lightbarColor.Value.r;
                report[48] = lightbarColor.Value.g;
                report[49] = lightbarColor.Value.b;
            }

            uint crc = 0xffffffff;
            crc = CrcByte(crc, 0xa2);
            for (int i = 0; i < 74; i++) crc = CrcByte(crc, report[i]);
            crc = ~crc;
            for (int i = 0; i < 4; i++) report[74 + i] = (byte)(crc >> (i * 8));
            return report;
        }

        public static byte[] BuildReport(float low, float high, byte sequence, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger, bool updateTriggers = true)
        {
            return BuildReport(low, high, sequence, leftTrigger, rightTrigger, updateTriggers, updateTriggers, null);
        }

        /// <summary>Compatibility overload for single R2 scalar callers.</summary>
        public static byte[] BuildReport(float low, float high, byte sequence, float rightTrigger = 0f, bool updateTrigger = false)
        {
            var report = new byte[78];
            report[0] = 0x31;
            report[1] = (byte)((sequence & 15) << 4);
            report[2] = 0x10;
            report[3] = 0x03; // Rumble emulation and selection of rumble over audio haptics.
            report[5] = EncodeMotor(high);
            report[6] = EncodeMotor(low);
            if (updateTrigger)
            {
                report[3] |= 0x04; // Update right adaptive trigger only.
                bool active = !float.IsNaN(rightTrigger) && rightTrigger > 0f;
                report[13] = active ? (byte)0x01 : (byte)0x05; // Constant resistance / explicit release.
                report[14] = active ? (byte)80 : (byte)0; // Start after the first third of R2 travel.
                report[15] = active ? EncodeMotor(Mathf.Min(rightTrigger, .22f)) : (byte)0;
            }
            uint crc = 0xffffffff;
            crc = CrcByte(crc, 0xa2); // Bluetooth HID output transaction prefix.
            for (int i = 0; i < 74; i++) crc = CrcByte(crc, report[i]);
            crc = ~crc;
            for (int i = 0; i < 4; i++) report[74 + i] = (byte)(crc >> (i * 8));
            return report;
        }

        public static byte EncodeMotor(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

        private static uint CrcByte(uint crc, byte value)
        {
            crc ^= value;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            return crc;
        }

        [StructLayout(LayoutKind.Explicit, Size = InputDeviceCommand.BaseCommandSize + 78)]
        public struct BluetoothOutputCommand : IInputDeviceCommandInfo
        {
            public FourCC typeStatic => new FourCC('H', 'I', 'D', 'O');
            [FieldOffset(0)] public InputDeviceCommand Header;
            [FieldOffset(InputDeviceCommand.BaseCommandSize)] public byte ReportId;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 1)] public byte Sequence;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 2)] public byte Tag;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 3)] public byte Flags;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 4)] public byte ValidFlag1;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 5)] public byte HighMotor;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 6)] public byte LowMotor;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 74)] public uint Crc;

            // Right trigger (offset 13..23)
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 13)] public byte TriggerMode;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 14)] public byte TriggerStart;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 15)] public byte TriggerForce;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 16)] public byte RightParam3;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 17)] public byte RightParam4;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 18)] public byte RightParam5;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 19)] public byte RightParam6;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 20)] public byte RightParam7;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 21)] public byte RightParam8;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 22)] public byte RightParam9;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 23)] public byte RightParam10;

            // Left trigger (offset 24..34)
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 24)] public byte LeftTriggerMode;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 25)] public byte LeftTriggerParam1;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 26)] public byte LeftTriggerParam2;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 27)] public byte LeftTriggerParam3;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 28)] public byte LeftTriggerParam4;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 29)] public byte LeftTriggerParam5;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 30)] public byte LeftTriggerParam6;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 31)] public byte LeftTriggerParam7;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 32)] public byte LeftTriggerParam8;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 33)] public byte LeftTriggerParam9;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 34)] public byte LeftTriggerParam10;

            // Lightbar & LEDs (offset 41..49)
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 41)] public byte ValidFlag2;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 44)] public byte LightbarSetup;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 45)] public byte LedBrightness;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 46)] public byte PlayerLeds;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 47)] public byte LightbarRed;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 48)] public byte LightbarGreen;
            [FieldOffset(InputDeviceCommand.BaseCommandSize + 49)] public byte LightbarBlue;

            public static BluetoothOutputCommand Create(float low, float high, byte sequence, float rightTrigger = 0f, bool updateTrigger = false)
            {
                var report = BuildReport(low, high, sequence, rightTrigger, updateTrigger);
                return FromReport(report);
            }

            public static BluetoothOutputCommand Create(float low, float high, byte sequence, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger, bool updateLeft, bool updateRight, Color32? lightbarColor = null)
            {
                var report = BuildReport(low, high, sequence, leftTrigger, rightTrigger, updateLeft, updateRight, lightbarColor);
                return FromReport(report);
            }

            public static BluetoothOutputCommand Create(float low, float high, byte sequence, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger, bool updateTriggers = true)
            {
                var report = BuildReport(low, high, sequence, leftTrigger, rightTrigger, updateTriggers);
                return FromReport(report);
            }

            private static BluetoothOutputCommand FromReport(byte[] report)
            {
                return new BluetoothOutputCommand
                {
                    Header = new InputDeviceCommand(new FourCC('H', 'I', 'D', 'O'), InputDeviceCommand.BaseCommandSize + 78),
                    ReportId = report[0],
                    Sequence = report[1],
                    Tag = report[2],
                    Flags = report[3],
                    ValidFlag1 = report[4],
                    HighMotor = report[5],
                    LowMotor = report[6],
                    TriggerMode = report[13],
                    TriggerStart = report[14],
                    TriggerForce = report[15],
                    RightParam3 = report[16],
                    RightParam4 = report[17],
                    RightParam5 = report[18],
                    RightParam6 = report[19],
                    RightParam7 = report[20],
                    RightParam8 = report[21],
                    RightParam9 = report[22],
                    RightParam10 = report[23],
                    LeftTriggerMode = report[24],
                    LeftTriggerParam1 = report[25],
                    LeftTriggerParam2 = report[26],
                    LeftTriggerParam3 = report[27],
                    LeftTriggerParam4 = report[28],
                    LeftTriggerParam5 = report[29],
                    LeftTriggerParam6 = report[30],
                    LeftTriggerParam7 = report[31],
                    LeftTriggerParam8 = report[32],
                    LeftTriggerParam9 = report[33],
                    LeftTriggerParam10 = report[34],
                    ValidFlag2 = report[41],
                    LightbarSetup = report[44],
                    LedBrightness = report[45],
                    PlayerLeds = report[46],
                    LightbarRed = report[47],
                    LightbarGreen = report[48],
                    LightbarBlue = report[49],
                    Crc = (uint)report[74] | ((uint)report[75] << 8) | ((uint)report[76] << 16) | ((uint)report[77] << 24)
                };
            }
        }
    }
}
