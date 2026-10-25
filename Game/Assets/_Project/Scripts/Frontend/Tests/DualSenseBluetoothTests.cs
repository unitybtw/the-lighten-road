using System.Runtime.InteropServices;
using NUnit.Framework;
using TheLightenRoad.UI;
using UnityEngine.InputSystem.LowLevel;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class DualSenseBluetoothTests
    {
        [Test]
        public void BluetoothReportHasTransportHeaderAndIndependentCrcFixture()
        {
            var report=DualSenseBluetoothRumble.BuildReport(.5f,.25f,3);
            Assert.That(report.Length,Is.EqualTo(78));
            Assert.That(report[0],Is.EqualTo(0x31));Assert.That(report[1],Is.EqualTo(0x30));
            Assert.That(report[2],Is.EqualTo(0x10));Assert.That(report[3],Is.EqualTo(3));
            Assert.That(report[5],Is.EqualTo(64));Assert.That(report[6],Is.EqualTo(128));
            // Independent Python zlib.crc32(0xA2 + report[0:74]) = 0xA03C6E33.
            Assert.That(new[]{report[74],report[75],report[76],report[77]},Is.EqualTo(new byte[]{0x33,0x6e,0x3c,0xa0}));
            for(int i=7;i<74;i++)Assert.That(report[i],Is.Zero,"No LED/audio/trigger setting should be enabled.");
        }
        [Test]
        public void StopReportHasValidCrcAndZeroMotors()
        {
            var report=DualSenseBluetoothRumble.BuildReport(0,0,0);
            Assert.That(report[5]+report[6],Is.Zero);
            Assert.That(new[]{report[74],report[75],report[76],report[77]},Is.EqualTo(new byte[]{0xcc,0x64,0x2e,0x96}));
        }
        [Test]
        public void NativeCommandContainsExactSeventyEightByteReport()
        {
            var command=DualSenseBluetoothRumble.BluetoothOutputCommand.Create(.5f,.25f,3);
            Assert.That(Marshal.SizeOf<DualSenseBluetoothRumble.BluetoothOutputCommand>(),Is.EqualTo(InputDeviceCommand.BaseCommandSize+78));
            Assert.That(command.Header.sizeInBytes,Is.EqualTo(InputDeviceCommand.BaseCommandSize+78));
            Assert.That(command.Header.type.ToString(),Is.EqualTo("HIDO"));
            Assert.That(command.Crc,Is.EqualTo(0xa03c6e33u));
            Assert.That(command.LowMotor,Is.EqualTo(128));Assert.That(command.HighMotor,Is.EqualTo(64));
            var memory=Marshal.AllocHGlobal(Marshal.SizeOf<DualSenseBluetoothRumble.BluetoothOutputCommand>());
            try
            {
                Marshal.StructureToPtr(command,memory,false);
                var actual=new byte[78];
                Marshal.Copy(System.IntPtr.Add(memory,InputDeviceCommand.BaseCommandSize),actual,0,actual.Length);
                Assert.That(actual,Is.EqualTo(DualSenseBluetoothRumble.BuildReport(.5f,.25f,3)));
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        [Test]
        public void RoutingAcceptsSonyBluetoothAndLeavesUsbAndOtherDevicesAlone()
        {
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor("{\"vendorId\":1356,\"productId\":3302,\"outputReportSize\":78}"),Is.True);
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor("{\"vendorId\":1356,\"productId\":3570,\"outputReportSize\":78}"),Is.True);
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor("{\"vendorId\":1356,\"productId\":3302,\"outputReportSize\":48}"),Is.False);
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor("{\"vendorId\":1118,\"productId\":3302,\"outputReportSize\":78}"),Is.False);
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor("{\"vendorId\":1356,\"productId\":3302,\"inputReportSize\":78,\"outputReportSize\":547}"),Is.True);
            Assert.That(DualSenseBluetoothRumble.IsBluetoothDescriptor(null),Is.False);
        }
    }
}
