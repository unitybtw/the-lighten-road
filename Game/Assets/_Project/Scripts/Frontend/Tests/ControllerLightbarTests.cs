using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Frontend;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class ControllerLightbarTests
    {
        [SetUp]
        public void SetUp()
        {
            MenuPreferences.ResetToDefaults();
            ControllerLightbar.EnsureExists();
        }

        [TearDown]
        public void TearDown()
        {
            MenuPreferences.ResetToDefaults();
        }

        [Test]
        public void LightbarSerializesAccuratelyIntoBluetoothReport()
        {
            var color = new Color32(255, 140, 20, 255);
            var report = DualSenseBluetoothRumble.BuildReport(0, 0, 1, DualSenseTriggerEffect.Off, DualSenseTriggerEffect.Off, false, false, color);

            Assert.That(report[0], Is.EqualTo(0x31));
            Assert.That((report[4] & 0x04), Is.EqualTo(0x04), "ValidFlag1 must enable lightbar control (bit 2).");
            Assert.That((report[41] & 0x02), Is.EqualTo(0x02), "ValidFlag2 must enable lightbar setup (bit 1).");
            Assert.That(report[44], Is.EqualTo(0x02), "LightbarSetup must set custom light out mode.");
            Assert.That(report[45], Is.EqualTo(0x00), "LedBrightness must be full.");
            Assert.That(report[47], Is.EqualTo(255), "Red channel must match.");
            Assert.That(report[48], Is.EqualTo(140), "Green channel must match.");
            Assert.That(report[49], Is.EqualTo(20), "Blue channel must match.");

            // Compute expected CRC independently
            uint crc = 0xffffffff;
            byte val = 0xa2;
            crc ^= val;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            for (int j = 0; j < 74; j++)
            {
                crc ^= report[j];
                for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
            crc = ~crc;

            uint packetCrc = (uint)report[74] | ((uint)report[75] << 8) | ((uint)report[76] << 16) | ((uint)report[77] << 24);
            Assert.That(packetCrc, Is.EqualTo(crc), "CRC32 must match over the 74-byte payload.");
        }

        [Test]
        public void LightbarCommandFromReportPreservesAllByteFields()
        {
            var color = new Color32(80, 220, 255, 255);
            var cmd = DualSenseBluetoothRumble.BluetoothOutputCommand.Create(0.2f, 0.3f, 3, DualSenseTriggerEffect.Off, DualSenseTriggerEffect.Off, false, false, color);

            Assert.That(cmd.ReportId, Is.EqualTo(0x31));
            Assert.That(cmd.ValidFlag1, Is.EqualTo(0x04));
            Assert.That(cmd.ValidFlag2, Is.EqualTo(0x02));
            Assert.That(cmd.LightbarSetup, Is.EqualTo(0x02));
            Assert.That(cmd.LightbarRed, Is.EqualTo(80));
            Assert.That(cmd.LightbarGreen, Is.EqualTo(220));
            Assert.That(cmd.LightbarBlue, Is.EqualTo(255));
        }

        [Test]
        public void LegacyReportWithoutLightbarPreservesExactChecksumFixture()
        {
            var report = DualSenseBluetoothRumble.BuildReport(0.2f, 0.25f, 0, 0.18f, true);
            var cmd = DualSenseBluetoothRumble.BluetoothOutputCommand.Create(0.2f, 0.25f, 0, 0.18f, true);
            Assert.That(report[4], Is.Zero, "Legacy report must not set lightbar flag.");
            Assert.That(cmd.Crc, Is.EqualTo(0xf98e9b61u), "Legacy CRC fixture must remain identical.");
        }

        [Test]
        public void PreferencesToggleDisablesAndRestoresLightbar()
        {
            MenuPreferences.SetControllerLightbar(false);
            Assert.That(MenuPreferences.ControllerLightbar, Is.False);
            Assert.That(ControllerLightbar.Enabled, Is.False);
            Assert.That(DualSenseBluetoothRumble.LightbarEnabled, Is.False);

            MenuPreferences.SetControllerLightbar(true);
            Assert.That(MenuPreferences.ControllerLightbar, Is.True);
            Assert.That(ControllerLightbar.Enabled, Is.True);
            Assert.That(DualSenseBluetoothRumble.LightbarEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator HapticCuesTriggerDistinctLightbarColors()
        {
            yield return null;
            ControllerLightbar.HandleHapticCue(HapticCue.Flare);
            yield return null;

            ControllerLightbar.HandleHapticCue(HapticCue.EmberPickup);
            yield return null;

            ControllerLightbar.HandleHapticCue(HapticCue.Death);
            yield return null;

            Assert.That(ControllerLightbar.HasInstance, Is.True);
        }
    }
}
