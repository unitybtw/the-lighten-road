using System.Collections;
using NUnit.Framework;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    [InputControlLayout(stateType = typeof(GamepadState))]
    public class RecordingRumbleGamepad : Gamepad
    {
        public float Low, High;
        public int Writes;
        public override void SetMotorSpeeds(float lowFrequency, float highFrequency)
        { Low=lowFrequency; High=highFrequency; Writes++; }
    }

    public sealed class ControllerHapticsTests
    {
        private RecordingRumbleGamepad pad, other;
        private Gamepad previous;
        private bool previousVibration;
        private float previousScale;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            previous=Gamepad.current;previousScale=Time.timeScale;
            previousVibration=ControllerGlyphService.VibrationEnabled;
            Time.timeScale=1f;
            InputSystem.RegisterLayout<RecordingRumbleGamepad>();
            pad=InputSystem.AddDevice<RecordingRumbleGamepad>();pad.MakeCurrent();
            ControllerGlyphService.EnsureExists();
            ControllerGlyphService.Instance.SendMessage("OnApplicationFocus",true);
            ControllerGlyphService.Instance.SendMessage("OnApplicationPause",false);
            ControllerGlyphService.VibrationEnabled=true;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ControllerGlyphService.StopRumble();
            if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
            if(other!=null && other.added)InputSystem.RemoveDevice(other);
            if(previous!=null && previous.added)previous.MakeCurrent();
            ControllerGlyphService.VibrationEnabled=previousVibration;Time.timeScale=previousScale;
            ControllerGlyphService.Instance.SendMessage("OnApplicationFocus",true);
            yield return null;
        }
        [UnityTest]
        public IEnumerator CueSendsFadingBoundedMotorOutputAndEndsAtZero()
        {
            ControllerGlyphService.PlayHaptic(HapticCue.Flare);
            yield return new WaitForSecondsRealtime(.08f);
            float peak=pad.Low+pad.High;
            Assert.That(peak,Is.GreaterThan(0f));
            Assert.That(pad.Low,Is.InRange(0f,.32f));Assert.That(pad.High,Is.InRange(0f,.42f));
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(pad.Low+pad.High,Is.LessThan(peak));
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(pad.Low+pad.High,Is.Zero);
        }
        [UnityTest]
        public IEnumerator DisablingVibrationStopsImmediatelyAndRejectsFurtherCues()
        {
            ControllerGlyphService.PlayHaptic(HapticCue.Death);
            yield return null;Assert.That(pad.Low+pad.High,Is.GreaterThan(0));
            ControllerGlyphService.VibrationEnabled=false;
            Assert.That(pad.Low+pad.High,Is.Zero);
            int writes=pad.Writes;ControllerGlyphService.PlayHaptic(HapticCue.Flare);
            yield return null;Assert.That(pad.Writes,Is.EqualTo(writes));
        }
        [UnityTest]
        public IEnumerator ImportantFeedbackSurvivesJumpAndDeviceSwitchStopsOriginalPad()
        {
            ControllerGlyphService.PlayHaptic(HapticCue.Death);
            yield return new WaitForSecondsRealtime(.06f);
            int writes=pad.Writes;ControllerGlyphService.PlayHaptic(HapticCue.Jump);
            Assert.That(pad.Writes,Is.EqualTo(writes),"Jump must not replace an active death cue.");
            other=InputSystem.AddDevice<RecordingRumbleGamepad>();other.MakeCurrent();
            yield return null;yield return null;
            Assert.That(pad.Low+pad.High,Is.Zero);Assert.That(other.Low+other.High,Is.Zero);
        }
        [UnityTest]
        public IEnumerator PauseStopsGameplayButAllowsShortSettingsPreview()
        {
            ControllerGlyphService.PlayHaptic(HapticCue.Flare);yield return null;
            Time.timeScale=0f;yield return null;yield return null;
            Assert.That(pad.Low+pad.High,Is.Zero);
            ControllerGlyphService.PlayHaptic(HapticCue.Jump);Assert.That(pad.Low+pad.High,Is.Zero);
            ControllerGlyphService.PlayHaptic(HapticCue.Preview);yield return null;
            Assert.That(pad.Low+pad.High,Is.GreaterThan(0));
            yield return new WaitForSecondsRealtime(.25f);Assert.That(pad.Low+pad.High,Is.Zero);
        }
        [UnityTest]
        public IEnumerator FocusLossAndDisconnectNeverLeaveMotorsRunning()
        {
            ControllerGlyphService.PlayHaptic(HapticCue.Flare);yield return null;
            ControllerGlyphService.Instance.SendMessage("OnApplicationFocus",false);
            Assert.That(pad.Low+pad.High,Is.Zero);
            ControllerGlyphService.Instance.SendMessage("OnApplicationPause",false);
            ControllerGlyphService.PlayHaptic(HapticCue.Preview);Assert.That(pad.Low+pad.High,Is.Zero);
            ControllerGlyphService.Instance.SendMessage("OnApplicationFocus",true);
            ControllerGlyphService.PlayHaptic(HapticCue.Death);yield return null;
            InputSystem.RemoveDevice(pad);Assert.That(pad.Low+pad.High,Is.Zero);
        }
    }
}
