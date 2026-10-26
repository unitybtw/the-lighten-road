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
            Assert.That(report.Length, Is.EqualTo(142));
            Assert.That(report[9], Is.EqualTo(64), "Speaker output is capped.");
            Assert.That(report[4] & 0x50, Is.Zero, "Microphone volume is never configured.");
            Assert.That(report[4] & 0x0f, Is.Zero, "Motor and trigger fields are not updated.");
            Assert.That(report[5] & 0x14, Is.Zero, "LED state is not updated.");
            Assert.That(Crc(report), Is.EqualTo(0x380f0e06u), "Independent Python zlib fixture.");
            Assert.That(DualSenseSpeakerProtocol.Setup(0, 0)[9], Is.Zero);
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
        public void NativeCommandHasCorrectFourCcHeaderLengthAndEveryPayloadByte()
        {
            foreach(var report in new[]{DualSenseSpeakerProtocol.Setup(0,24), DualSenseSpeakerProtocol.Audio(0,2,new byte[400],0)})
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
                Assert.That(shortResource.bytes.Length, Is.InRange(400,1600), "Mac details must fit at most four reports.");
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
                PlayerPrefs.SetFloat("TLR.Menu.Master",0);
                Assert.That(DualSenseSpeaker.OutputGain, Is.Zero);
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
            }
            finally { DualSenseSpeaker.Enabled=enabled;UnityEngine.Object.Destroy(host); }
            yield return null;
        }
    }
}
