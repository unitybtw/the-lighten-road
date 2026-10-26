using System;
using System.Collections;
using System.Runtime.InteropServices;
using NUnit.Framework;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class DualSenseSpeakerTests
    {
        private static uint Crc(byte[] report) => BitConverter.ToUInt32(report, report.Length - 4);
        [Test]
        public void SetupUsesSpeakerAudioFlagsAndAnIndependentCrcFixture()
        {
            var report = DualSenseSpeakerProtocol.Setup(3, 255);
            Assert.That(report.Length, Is.EqualTo(78));
            Assert.That(report[8], Is.EqualTo(100), "Speaker output is capped.");
            Assert.That(report[10], Is.EqualTo(0x30), "The controller must explicitly route to its speaker.");
            Assert.That(report[3] & 0x50, Is.Zero, "Microphone and headset levels are not configured.");
            Assert.That(report[3] & 0x0f, Is.Zero, "Motor and trigger fields are not updated.");
            Assert.That(report[4] & 0x14, Is.Zero, "LED state is not updated.");
            Assert.That(Crc(report), Is.EqualTo(0x56e02f92u), "Independent Python zlib fixture.");
            Assert.That(DualSenseSpeakerProtocol.Setup(0, 0)[8], Is.Zero);
        }
        [Test]
        public void StreamHasTwoExactOpusPacketsSilentHapticsAndNoDuplexMicrophone()
        {
            var frames = new byte[400];
            for (int i=0;i<400;i++) frames[i]=(byte)i;
            var report = DualSenseSpeakerProtocol.Audio(3, 2, frames, 0);
            Assert.That(report.Length, Is.EqualTo(547));
            Assert.That(report[4] & 1, Is.Zero, "Microphone capture must remain disabled.");
            Assert.That(report[140], Is.EqualTo(0xd3));
            for(int i=12;i<140;i++) Assert.That(report[i], Is.Zero);
            for(int i=0;i<400;i++) Assert.That(report[142+i], Is.EqualTo(frames[i]));
            Assert.That(Crc(report), Is.EqualTo(0xeee7729eu), "Independent Python zlib fixture.");
        }
        [Test]
        public void CompatibilityAudioUsesOneOpusFrameAndNoHapticOrMicrophoneStream()
        {
            var frame=new byte[200];for(int i=0;i<200;i++)frame[i]=(byte)i;
            var report=DualSenseSpeakerProtocol.LegacyAudio(3,7,frame,0);
            Assert.That(report.Length,Is.EqualTo(334));Assert.That(report[0],Is.EqualTo(0x35));
            Assert.That(report[4]&1,Is.Zero);Assert.That(report[11],Is.EqualTo(0x93));
            for(int i=0;i<200;i++)Assert.That(report[13+i],Is.EqualTo(frame[i]));
            Assert.That(Crc(report),Is.EqualTo(0x26399459u));
            Assert.Throws<ArgumentException>(()=>DualSenseSpeakerProtocol.LegacyAudio(0,0,new byte[199],0));
        }
        [Test]
        public void NativeCommandHasCorrectFourCcHeaderLengthAndEveryPayloadByte()
        {
            foreach(var report in new[]{DualSenseSpeakerProtocol.Setup(0,24), DualSenseSpeakerProtocol.Audio(0,2,new byte[400],0),DualSenseSpeakerProtocol.LegacyAudio(0,0,new byte[200],0)})
            {
                var command = DualSenseSpeakerProtocol.Command.Create(report);
                Assert.That(Marshal.SizeOf<DualSenseSpeakerProtocol.Command>(), Is.EqualTo(555));
                Assert.That(command.Header.type.ToString(), Is.EqualTo("HIDO"));
                Assert.That(command.Header.sizeInBytes, Is.EqualTo(8+report.Length));
                IntPtr memory = Marshal.AllocHGlobal(555);
                try
                {
                    Marshal.StructureToPtr(command, memory, false);
                    var actual = new byte[report.Length];
                    Marshal.Copy(IntPtr.Add(memory, 8), actual, 0, actual.Length);
                    Assert.That(actual, Is.EqualTo(report));
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
        }
        [Test]
        public void IncompleteOrOverflowingFrameOffsetsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>DualSenseSpeakerProtocol.Audio(0,0,new byte[399],0));
            Assert.Throws<ArgumentException>(()=>DualSenseSpeakerProtocol.Audio(0,0,new byte[400],int.MaxValue));
            Assert.Throws<ArgumentException>(()=>DualSenseSpeakerProtocol.Audio(0,0,null,0));
        }
        [Test]
        public void AllCuesHaveCompletePreencodedPacketPairs()
        {
            foreach(SpeakerCue cue in Enum.GetValues(typeof(SpeakerCue)))
            {
                var resource = Resources.Load<TextAsset>("Audio/ControllerSpeaker/"+cue);
                Assert.That(resource, Is.Not.Null, cue.ToString());
                Assert.That(resource.bytes.Length, Is.InRange(400, 24000));
                Assert.That(resource.bytes.Length % 400, Is.Zero);
                var shortResource=Resources.Load<TextAsset>("Audio/ControllerSpeaker/Mac/"+cue);
                Assert.That(shortResource, Is.Not.Null);
                Assert.That(shortResource.bytes.Length, Is.InRange(4000,10400), "Mac cues need enough audible content and a bounded playback budget.");
                Assert.That(shortResource.bytes.Length % 400, Is.Zero);
            }
        }
        [Test]
        public void SpeakerVolumeUsesBothMasterAndEffectsAndIsIndependentOfVibration()
        {
            float master=MenuPreferences.Master, effects=MenuPreferences.Effects;
            bool enabled=DualSenseSpeaker.Enabled, vibration=ControllerGlyphService.VibrationEnabled;
            try
            {
                DualSenseSpeaker.Enabled=true;
                ControllerGlyphService.VibrationEnabled=false;
                PlayerPrefs.SetFloat("TLR.Menu.Master",.5f);PlayerPrefs.SetFloat("TLR.Menu.Effects",.4f);
                Assert.That(DualSenseSpeaker.OutputGain, Is.EqualTo(.2f).Within(.001));
                Assert.That(DualSenseSpeaker.Enabled, Is.True);
                PlayerPrefs.SetFloat("TLR.Menu.Master",.69f);PlayerPrefs.SetFloat("TLR.Menu.Effects",.90f);
                Assert.That(DualSenseSpeaker.NativeVolume,Is.InRange(95,97),"Use the calibrated speaker range at ordinary listening settings.");
                PlayerPrefs.SetFloat("TLR.Menu.Master",0);
                Assert.That(DualSenseSpeaker.OutputGain, Is.Zero);Assert.That(DualSenseSpeaker.NativeVolume,Is.Zero);
                PlayerPrefs.SetFloat("TLR.Menu.Master",float.NaN);
                Assert.That(DualSenseSpeaker.OutputGain, Is.Zero);
                DualSenseSpeaker.Enabled=false;
                Assert.That(DualSenseSpeaker.IsPlaying, Is.False);
            }
            finally
            {
                PlayerPrefs.SetFloat("TLR.Menu.Master",master);PlayerPrefs.SetFloat("TLR.Menu.Effects",effects);
                DualSenseSpeaker.Enabled=enabled;ControllerGlyphService.VibrationEnabled=vibration;
            }
        }
        [Test]
        public void AudioAndMotorThreadsShareASequenceWithoutLostIncrements()
        {
            var counts = new int[16];
            System.Threading.Tasks.Parallel.For(0, 1600, _ =>
            {
                byte sequence = DualSenseBluetoothRumble.NextSequence(int.MaxValue);
                System.Threading.Interlocked.Increment(ref counts[sequence]);
            });
            foreach(int count in counts) Assert.That(count, Is.EqualTo(100));
        }
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [UnityTest]
        public IEnumerator MissingPhysicalControllerFinishesNativeWorkWithoutTouchingAnotherPad()
        {
            var previous = Gamepad.current;
            var description = new UnityEngine.InputSystem.Layouts.InputDeviceDescription
            {
                interfaceName="HID", manufacturer="Sony", product="DualSense Wireless Controller",
                serial="Test-controller-that-does-not-exist",
                capabilities="{\"vendorId\":1356,\"productId\":3302,\"inputReportSize\":78,\"outputReportSize\":547}"
            };
            var device = InputSystem.AddDevice(description);
            MacDualSenseSpeakerTransport.Playback playback=null;
            try
            {
                Assert.That(device, Is.InstanceOf<Gamepad>());
                Assert.That(MacDualSenseSpeakerTransport.TryPlay((Gamepad)device, new byte[400], 0, out playback), Is.True);
                double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(!playback.Finished && Time.realtimeSinceStartupAsDouble<deadline) yield return null;
                Assert.That(playback.Finished, Is.True, "Worker must close its handles and finish when the target is absent.");
                Assert.That(playback.Completed, Is.False);
                Assert.That(playback.SuccessfulReports, Is.Zero, "A fake serial must never address a real controller.");
                Assert.That(playback.Result, Is.EqualTo(-1));
            }
            finally
            {
                playback?.Cancel();
                InputSystem.RemoveDevice(device);
                if(previous!=null && previous.added)previous.MakeCurrent();
            }
        }
#endif
        [Test]
        public void SlowNativeWritesDoNotBuildAnImpossibleCatchupSchedule()
        {
            double scheduled=0;
            for(int i=0;i<40;i++)
            {
                double completed=scheduled+.055;
                scheduled=MacDualSenseSpeakerTransport.NextReportTime(scheduled,completed);
                Assert.That(scheduled,Is.GreaterThanOrEqualTo(completed));
            }
            double fast=MacDualSenseSpeakerTransport.NextReportTime(1,1.001);
            Assert.That(fast,Is.EqualTo(1+DualSenseSpeakerProtocol.LegacyReportSeconds).Within(.000001));
        }
        [Test]
        public void DisabledMutedAndPausedRequestsExplainWhyTheyDoNotPlay()
        {
            bool enabled=DualSenseSpeaker.Enabled;
            float master=MenuPreferences.Master,scale=Time.timeScale;
            try
            {
                DualSenseSpeaker.Enabled=false;
                Assert.That(DualSenseSpeaker.TryPlay(SpeakerCue.Preview,true),Is.False);
                Assert.That(DualSenseSpeaker.LastStatus,Does.Contain("Turn on"));
                DualSenseSpeaker.Enabled=true;PlayerPrefs.SetFloat("TLR.Menu.Master",0);
                Assert.That(DualSenseSpeaker.TryPlay(SpeakerCue.Preview,true),Is.False);
                Assert.That(DualSenseSpeaker.LastStatus,Does.Contain("muted"));
                PlayerPrefs.SetFloat("TLR.Menu.Master",1);Time.timeScale=0;
                Assert.That(DualSenseSpeaker.TryPlay(SpeakerCue.Ignite),Is.False);
                Assert.That(DualSenseSpeaker.LastStatus,Does.Contain("paused"));
            }
            finally {PlayerPrefs.SetFloat("TLR.Menu.Master",master);Time.timeScale=scale;DualSenseSpeaker.Enabled=enabled;}
        }
        [UnityTest]
        public IEnumerator RealLanternStateChangesReachTheSpeakerFeedbackBinding()
        {
            bool enabled=DualSenseSpeaker.Enabled,vibration=ControllerGlyphService.VibrationEnabled;
            float scale=Time.timeScale;
            DualSenseSpeaker.Enabled=false;ControllerGlyphService.VibrationEnabled=false;Time.timeScale=1;
            ControllerGlyphService.EnsureExists();
            var host=new GameObject("Lantern speaker binding review");
            var lantern=host.AddComponent<TheLightenRoad.Lantern.LanternController>();
            yield return null;
            try
            {
                int requests=DualSenseSpeaker.RequestCount;
                Assert.That(lantern.SetFlared(true),Is.True);
                Assert.That(DualSenseSpeaker.RequestCount,Is.GreaterThan(requests));
                Assert.That(DualSenseSpeaker.LastStatus,Does.Contain("Turn on"));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                DualSenseSpeaker.Enabled=enabled;ControllerGlyphService.VibrationEnabled=vibration;Time.timeScale=scale;
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator AudioSettingsExposeAndPersistASpeakerSwitchWithGamepadNavigation()
        {
            bool enabled=DualSenseSpeaker.Enabled;
            DualSenseSpeaker.Enabled=true;
            var host=new GameObject("Speaker Menu Test");
            var menu=host.AddComponent<MainMenuController>();
            yield return null;
            try
            {
                menu.SkipIntro();menu.ShowSettings();menu.SelectSettingsSection(0);
                var field=typeof(MainMenuController).GetField("controllerSpeakerToggle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var toggle=(Toggle)field.GetValue(menu);
                Assert.That(toggle, Is.Not.Null);
                toggle.isOn=false;
                Assert.That(MenuPreferences.ControllerSpeaker, Is.False);
                Assert.That(toggle.navigation.selectOnUp, Is.TypeOf<Slider>());
                Assert.That(toggle.navigation.selectOnUp.navigation.selectOnDown, Is.EqualTo(toggle));
                Assert.That(toggle.navigation.selectOnDown, Is.TypeOf<Button>());
                var test=(Button)toggle.navigation.selectOnDown;
                Assert.That(test.gameObject.name,Is.EqualTo("Test DualSense Speaker"));
                Assert.That(test.navigation.selectOnLeft,Is.EqualTo(toggle));
                test.onClick.Invoke();
                Assert.That(DualSenseSpeaker.LastStatus,Does.Contain("Turn on"));
            }
            finally { DualSenseSpeaker.Enabled=enabled;UnityEngine.Object.Destroy(host); }
            yield return null;
        }
    }
}
