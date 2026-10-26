using System;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace TheLightenRoad.UI
{
    /// <summary>Speaker-only Bluetooth Opus framing. Protocol references and MIT notices are in the accompanying documentation.</summary>
    public static class DualSenseSpeakerProtocol
    {
        public const int OpusBytes=200;
        public const double ReportSeconds=1024.0/48000.0;
        public static byte[] Setup(byte sequence,byte volume)
        {
            var report=new byte[142];report[0]=0x32;report[1]=(byte)((sequence&15)<<4);report[2]=0x90;report[3]=0x3f;
            report[4]=0xa0; // Speaker level and audio route only; microphone and headset-volume flags are clear.
            report[5]=0x82; // Audio preamp and mute-control validity; no LED or trigger update.
            report[9]=(byte)Math.Min(64,(int)volume);
            report[11]=0; // Controller speaker output path.
            report[13]=0; // Speaker not muted.
            report[41]=3; // Modest speaker preamp.
            FinishCrc(report);return report;
        }
        public static byte[] Audio(byte sequence,byte counter,byte[] frames,int offset)
        {
            if(frames==null || offset<0 || offset>frames.Length-400)throw new ArgumentException("Two complete 200-byte Opus frames are required.");
            var report=new byte[547];report[0]=0x39;report[1]=(byte)((sequence&15)<<4);
            report[2]=0x91;report[3]=6;report[4]=0x7e; // Microphone/duplex capture bit is clear.
            for(int i=5;i<=8;i++)report[i]=64;
            report[9]=counter;report[10]=0xd2;report[11]=64; // Two silent haptic-audio blocks; emulated rumble is independent.
            report[140]=0xd3;report[141]=200;
            Buffer.BlockCopy(frames,offset,report,142,400);
            FinishCrc(report);return report;
        }
        public static void FinishCrc(byte[] report)
        {
            uint crc=0xffffffff;
            crc=Update(crc,0xa2);
            for(int i=0;i<report.Length-4;i++)crc=Update(crc,report[i]);
            crc=~crc;for(int i=0;i<4;i++)report[report.Length-4+i]=(byte)(crc>>(8*i));
        }
        private static uint Update(uint crc,byte value)
        {
            crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);return crc;
        }
        public static long Send(Gamepad pad,byte[] report)
        {
            if(pad==null || !pad.added)return -1;
            var command=Command.Create(report);return pad.ExecuteCommand(ref command);
        }
        [StructLayout(LayoutKind.Sequential,Pack=1)]
        public struct Block64 {public ulong A,B,C,D,E,F,G,H;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]
        public struct Block32 {public ulong A,B,C,D;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]
        public struct Payload547 {public Block64 A,B,C,D,E,F,G,H;public Block32 I;public byte J,K,L;}
        [StructLayout(LayoutKind.Sequential,Pack=1)]
        public struct Command:IInputDeviceCommandInfo
        {
            public InputDeviceCommand Header;
            public Payload547 Payload;
            public FourCC typeStatic=>new FourCC('H','I','D','O');
            public static Command Create(byte[] report)
            {
                if(report==null || report.Length<4 || report.Length>547)throw new ArgumentException("Invalid speaker HID report length.");
                var raw=new byte[555];
                Buffer.BlockCopy(report,0,raw,8,report.Length);
                var handle=GCHandle.Alloc(raw,GCHandleType.Pinned);
                try{var command=Marshal.PtrToStructure<Command>(handle.AddrOfPinnedObject());command.Header=new InputDeviceCommand(new FourCC('H','I','D','O'),8+report.Length);return command;}
                finally{handle.Free();}
            }
        }
    }
}
