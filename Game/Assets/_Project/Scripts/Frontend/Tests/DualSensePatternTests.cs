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
            var signatures=new System.Collections.Generic.HashSet<string>();
            foreach(HapticCue cue in System.Enum.GetValues(typeof(HapticCue)))
            {
                string signature="";
                Assert.That(DualSenseHapticPatterns.Duration(cue),Is.InRange(.05f,1f));
                for(int i=0;i<=100;i++)
                {
                    var v=DualSenseHapticPatterns.Sample(cue,i/100f);
                    Assert.That(v.x,Is.InRange(0f,1f));Assert.That(v.y,Is.InRange(0f,1f));
                    signature+=Mathf.RoundToInt(v.x*100)+","+Mathf.RoundToInt(v.y*100)+";";
                }
                Assert.That(signatures.Add(signature),Is.True,"Each cue should have a distinct texture: "+cue);
                Assert.That(DualSenseHapticPatterns.Sample(cue,1f),Is.EqualTo(Vector2.zero));
            }
        }
        [Test]
        public void PickupHasTwoClicksAndBreakHasThreeSeparatedClicks()
        {
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup,.1f).y,Is.GreaterThan(0));
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup,.4f).y,Is.Zero);
            Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberPickup,.6f).y,Is.GreaterThan(0));
            foreach(float t in new[]{.1f,.4f,.75f})Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberBreak,t).y,Is.GreaterThan(0));
            foreach(float t in new[]{.25f,.6f})Assert.That(DualSenseHapticPatterns.Sample(HapticCue.EmberBreak,t).y,Is.Zero);
        }
        [Test]
        public void AdaptiveResistanceIsLightAndLimitedToFlareAndPreview()
        {
            foreach(HapticCue cue in System.Enum.GetValues(typeof(HapticCue)))
            {
                float strength=DualSenseHapticPatterns.RightTrigger(cue,.5f);
                Assert.That(strength,Is.InRange(0f,.18f));
                Assert.That(strength>0,Is.EqualTo(cue==HapticCue.Flare || cue==HapticCue.Preview));
                Assert.That(DualSenseHapticPatterns.RightTrigger(cue,1f),Is.Zero);
            }
        }
        [Test]
        public void TriggerPacketIncludesLightR2AndExplicitReleaseWithoutTouchingL2()
        {
            var active=DualSenseBluetoothRumble.BuildReport(.2f,.25f,0,.18f,true);
            Assert.That(active[3]&4,Is.EqualTo(4));Assert.That(active[3]&8,Is.Zero);
            Assert.That(active[13],Is.EqualTo(1));Assert.That(active[14],Is.EqualTo(80));Assert.That(active[15],Is.EqualTo(46));
            for(int i=24;i<35;i++)Assert.That(active[i],Is.Zero);
            var stop=DualSenseBluetoothRumble.BuildReport(0,0,1,0,true);
            Assert.That(stop[13],Is.EqualTo(5));Assert.That(stop[14]+stop[15]+stop[5]+stop[6],Is.Zero);
            var command=DualSenseBluetoothRumble.BluetoothOutputCommand.Create(.2f,.25f,0,.18f,true);
            Assert.That(command.TriggerMode,Is.EqualTo(active[13]));Assert.That(command.TriggerForce,Is.EqualTo(active[15]));
            Assert.That(command.Crc,Is.EqualTo(0xf98e9b61u)); // Independent zlib CRC fixture.
        }
    }
}
