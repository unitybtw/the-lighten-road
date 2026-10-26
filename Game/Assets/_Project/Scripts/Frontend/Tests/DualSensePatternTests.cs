using NUnit.Framework;
using TheLightenRoad.UI;
using UnityEngine;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class DualSensePatternTests
    {
        [Test]
        public void EveryTextureIsBoundedFiniteDistinctAndStopsAtItsEnd()
        {
            var signatures = new System.Collections.Generic.HashSet<string>();
            foreach (HapticCue cue in System.Enum.GetValues(typeof(HapticCue)))
            {
                string signature = "";
                Assert.That(DualSenseHapticPatterns.Duration(cue), Is.InRange(.05f, 1f));
                for (int i = 0; i <= 100; i++)
                {
                    var v = DualSenseHapticPatterns.Sample(cue, i / 100f);
                    Assert.That(v.x, Is.InRange(0f, 1f)); Assert.That(v.y, Is.InRange(0f, 1f));
                    signature += Mathf.RoundToInt(v.x * 100) + "," + Mathf.RoundToInt(v.y * 100) + ";";
                }
                Assert.That(signatures.Add(signature), Is.True, "Each cue should have a distinct texture: " + cue);
                Assert.That(DualSenseHapticPatterns.Sample(cue, 1f), Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void PickupHasTwoClicksAndBreakHasThreeSeparatedClicks()
        {
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup, .1f).y, Is.GreaterThan(0));
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup, .4f).y, Is.Zero);
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup, .6f).y, Is.GreaterThan(0));
            foreach (float t in new[] { .1f, .4f, .75f }) Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberBreak, t).y, Is.GreaterThan(0));
            foreach (float t in new[] { .25f, .6f }) Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberBreak, t).y, Is.Zero);
        }

        [Test]
        public void AdaptiveResistanceIsLightAndLimitedToFlareAndPreview()
        {
            foreach (HapticCue cue in System.Enum.GetValues(typeof(HapticCue)))
            {
                float strength = DualSenseHapticPatterns.RightTrigger(cue, .5f);
                Assert.That(strength, Is.InRange(0f, .18f));
                Assert.That(strength > 0, Is.EqualTo(cue == HapticCue.Flare || cue == HapticCue.Preview));
                Assert.That(DualSenseHapticPatterns.RightTrigger(cue, 1f), Is.Zero);
            }
        }

        [Test]
        public void TriggerPacketIncludesLightR2AndExplicitReleaseWithoutTouchingL2()
        {
            var active = DualSenseBluetoothRumble.BuildReport(.2f, .25f, 0, .18f, true);
            Assert.That(active[3] & 4, Is.EqualTo(4)); Assert.That(active[3] & 8, Is.Zero);
            Assert.That(active[13], Is.EqualTo(1)); Assert.That(active[14], Is.EqualTo(80)); Assert.That(active[15], Is.EqualTo(46));
            for (int i = 24; i < 35; i++) Assert.That(active[i], Is.Zero);
            var stop = DualSenseBluetoothRumble.BuildReport(0, 0, 1, 0, true);
            Assert.That(stop[13], Is.EqualTo(5)); Assert.That(stop[14] + stop[15] + stop[5] + stop[6], Is.Zero);
            var command = DualSenseBluetoothRumble.BluetoothOutputCommand.Create(.2f, .25f, 0, .18f, true);
            Assert.That(command.TriggerMode, Is.EqualTo(active[13])); Assert.That(command.TriggerForce, Is.EqualTo(active[15]));
            Assert.That(command.Crc, Is.EqualTo(0xf98e9b61u)); // Independent zlib CRC fixture.
        }

        [Test]
        public void TriggerModesSerializeAccuratelyIntoReportBytes()
        {
            var cont = DualSenseTriggerEffect.Continuous(0.2f, 0.7f);
            Assert.That(cont.Mode, Is.EqualTo(DualSenseTriggerMode.Continuous));
            Assert.That(cont.Param1, Is.EqualTo(Mathf.RoundToInt(0.2f * 255f)));
            Assert.That(cont.Param2, Is.EqualTo(Mathf.RoundToInt(0.7f * 255f)));

            var sec = DualSenseTriggerEffect.Section(0.15f, 0.65f, 0.8f);
            Assert.That(sec.Mode, Is.EqualTo(DualSenseTriggerMode.Section));
            Assert.That(sec.Param1, Is.EqualTo(Mathf.RoundToInt(0.15f * 255f)));
            Assert.That(sec.Param2, Is.EqualTo(Mathf.RoundToInt(0.65f * 255f)));
            Assert.That(sec.Param3, Is.EqualTo(Mathf.RoundToInt(0.8f * 255f)));

            var vib = DualSenseTriggerEffect.Vibration(0.1f, 0.9f, 0.75f);
            Assert.That(vib.Mode, Is.EqualTo(DualSenseTriggerMode.Vibration));
            Assert.That(vib.Param1, Is.EqualTo(Mathf.RoundToInt(0.1f * 255f)));
            Assert.That(vib.Param2, Is.EqualTo(Mathf.RoundToInt(0.9f * 255f)));
            Assert.That(vib.Param3, Is.EqualTo(Mathf.RoundToInt(0.75f * 255f)));

            var off = DualSenseTriggerEffect.Off;
            Assert.That(off.Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(off.IsActive, Is.False);
        }

        [Test]
        public void BothTriggersCanBeUpdatedSimultaneously()
        {
            var left = DualSenseTriggerEffect.Continuous(0.1f, 0.5f);
            var right = DualSenseTriggerEffect.Vibration(0.05f, 0.85f, 0.8f);

            var report = DualSenseBluetoothRumble.BuildReport(0.3f, 0.4f, 2, left, right, true);
            Assert.That(report[0], Is.EqualTo(0x31));
            Assert.That((report[3] & 0x0C), Is.EqualTo(0x0C)); // Both trigger flags enabled

            // Right trigger at offset 13
            Assert.That(report[13], Is.EqualTo((byte)DualSenseTriggerMode.Vibration));
            Assert.That(report[14], Is.EqualTo(right.Param1));
            Assert.That(report[15], Is.EqualTo(right.Param2));
            Assert.That(report[16], Is.EqualTo(right.Param3));

            // Left trigger at offset 24
            Assert.That(report[24], Is.EqualTo((byte)DualSenseTriggerMode.Continuous));
            Assert.That(report[25], Is.EqualTo(left.Param1));
            Assert.That(report[26], Is.EqualTo(left.Param2));

            // Command struct preserves both trigger states
            var cmd = DualSenseBluetoothRumble.BluetoothOutputCommand.Create(0.3f, 0.4f, 2, left, right, true);
            Assert.That(cmd.TriggerMode, Is.EqualTo(report[13]));
            Assert.That(cmd.LeftTriggerMode, Is.EqualTo(report[24]));
        }

        [Test]
        public void ProfessionalTriggerProfilesEngageAcrossGameplayCues()
        {
            // Flare: Smooth continuous resistance on R2, safe capped force, releases cleanly
            var flareMid = DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Flare, 0.4f);
            Assert.That(flareMid.Mode, Is.EqualTo(DualSenseTriggerMode.Continuous));
            Assert.That(flareMid.Param1, Is.EqualTo(Mathf.RoundToInt(0.25f * 255f))); // Deadzone clearance
            Assert.That(flareMid.Param2, Is.InRange(1, Mathf.RoundToInt(0.25f * 255f))); // Capped torque
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Flare, 1.0f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));

            // Preview: Gentle continuous resistance preview
            var prevMid = DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Preview, 0.4f);
            Assert.That(prevMid.Mode, Is.EqualTo(DualSenseTriggerMode.Continuous));
            Assert.That(prevMid.Param2, Is.InRange(1, Mathf.RoundToInt(0.25f * 255f)));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Preview, 1.0f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));

            // Platforming cues (Jump, Landing, EmberPickup, EmberBreak, Death, ChamberComplete):
            // Triggers must remain completely OFF to prevent mechanical clicking, gear chatter, or jarring finger kicks.
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Jump, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Landing, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.EmberPickup, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.EmberBreak, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.Death, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            Assert.That(DualSenseHapticPatterns.GetRightTriggerEffect(HapticCue.ChamberComplete, 0.1f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));

            // Left trigger must remain off across all standard cues
            foreach (HapticCue cue in System.Enum.GetValues(typeof(HapticCue)))
            {
                Assert.That(DualSenseHapticPatterns.GetLeftTriggerEffect(cue, 0.5f).Mode, Is.EqualTo(DualSenseTriggerMode.Off));
            }
        }

        [Test]
        public void FlintLockProvidesSectionResistanceMatchingTriggerPressGateThresholds()
        {
            var flint = DualSenseHapticPatterns.GetFlintLockTriggerEffect();
            Assert.That(flint.Mode, Is.EqualTo(DualSenseTriggerMode.Section));
            Assert.That(flint.Param1, Is.EqualTo(Mathf.RoundToInt(0.35f * 255f)), "Begins at release gate threshold (0.35).");
            Assert.That(flint.Param2, Is.EqualTo(Mathf.RoundToInt(0.65f * 255f)), "Ends at ignition snap threshold (0.65).");
            Assert.That(flint.Param3, Is.EqualTo(Mathf.RoundToInt(0.55f * 255f)), "Tactile flint resistance force.");
            Assert.That(flint.IsActive, Is.True);
        }
    }
}
