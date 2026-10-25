using System.Collections;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.UI
{
    public enum HapticCue { Jump, Landing, Flare, EmberPickup, EmberBreak, Death, ChamberComplete, Preview }

    public sealed partial class ControllerGlyphService
    {
        private Gamepad rumblePad;
        private int rumblePriority;
        private bool hapticsFocusLost, hapticsAppPaused;
        private bool HapticsSuspended => hapticsFocusLost || hapticsAppPaused;
        private LanternController boundLantern;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapHaptics() => EnsureExists();

        public static void PlayHaptic(HapticCue cue, float strength = 1f)
        {
            float low, high, duration;
            int priority;
            switch (cue)
            {
                case HapticCue.Jump: low=.10f; high=.16f; duration=.065f; priority=0; break;
                case HapticCue.Landing: low=.40f; high=.18f; duration=.16f; priority=1; break;
                case HapticCue.Flare: low=.32f; high=.42f; duration=.28f; priority=2; break;
                case HapticCue.EmberPickup: low=.10f; high=.24f; duration=.13f; priority=0; break;
                case HapticCue.EmberBreak: low=.18f; high=.40f; duration=.15f; priority=2; break;
                case HapticCue.Death: low=.52f; high=.66f; duration=.38f; priority=4; break;
                case HapticCue.ChamberComplete: low=.28f; high=.36f; duration=.48f; priority=3; break;
                default: low=.18f; high=.28f; duration=.16f; priority=1; break;
            }
            if (!IsFinite(strength) || strength <= 0f || !VibrationEnabled) return;
            EnsureExists();
            instance.TriggerRumble(low*Mathf.Clamp01(strength), high*Mathf.Clamp01(strength), duration,
                priority, cue == HapticCue.Preview, cue == HapticCue.Flare ? .045f : .008f);
        }

        // Compatibility entry point for external callers; new game feedback should use named cues.
        public static void Rumble(float lowFrequency, float highFrequency, float duration = .15f)
        {
            if (!VibrationEnabled || !IsFinite(lowFrequency) || !IsFinite(highFrequency) || !IsFinite(duration)) return;
            EnsureExists();
            instance.TriggerRumble(Mathf.Clamp01(lowFrequency), Mathf.Clamp01(highFrequency), duration, 1, false, .008f);
        }

        public static void StopRumble() { if (instance != null) instance.HaltRumble(); }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void TriggerRumble(float low, float high, float duration, int priority, bool allowPaused, float attack)
        {
            var pad = GetActiveGamepad();
            if (pad == null || !pad.added || !isActiveAndEnabled || HapticsSuspended || duration <= 0f ||
                (Time.timeScale <= 0f && !allowPaused)) return;
            if (activeRumbleRoutine != null && pad == rumblePad && priority < rumblePriority) return;
            HaltRumble();
            rumblePad = pad;
            rumblePriority = priority;
            activeRumbleRoutine = StartCoroutine(RumbleRoutine(pad, low, high, Mathf.Min(duration, 2f), allowPaused, attack));
        }

        private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float duration, bool allowPaused, float attack)
        {
            float elapsed = 0f;
            while (elapsed < duration && VibrationEnabled && pad.added && GetActiveGamepad() == pad &&
                (!HapticsSuspended) && (allowPaused || Time.timeScale > 0f))
            {
                float envelope = Mathf.Clamp01((elapsed + .004f) / attack) * Mathf.Pow(1f - elapsed / duration, 1.4f);
                SetMotors(pad, low*envelope, high*envelope);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetMotors(pad, 0f, 0f);
            rumblePad = null;
            activeRumbleRoutine = null;
        }

        private static void SetMotors(Gamepad pad, float low, float high)
        {
            if (pad == null) return;
            try
            {
                if (DualSenseBluetoothRumble.IsBluetooth(pad))
                {
                    DualSenseBluetoothRumble.Send(pad, low, high);
                    return;
                }
                pad.SetMotorSpeeds(low, high);
            }
            catch (System.Exception) { /* Unsupported/disconnected drivers must not interrupt gameplay. */ }
        }

        private void HaltRumble()
        {
            if (activeRumbleRoutine != null) StopCoroutine(activeRumbleRoutine);
            activeRumbleRoutine = null;
            SetMotors(rumblePad, 0f, 0f);
            rumblePad = null;
            rumblePriority = 0;
        }

        private void CheckLanternBinding()
        {
            var lantern = boundPlayer != null ? boundPlayer.GetComponentInChildren<LanternController>() : null;
            if (lantern == boundLantern) return;
            if (boundLantern != null) boundLantern.EmberBroken -= HandleEmberBroken;
            boundLantern = lantern;
            if (boundLantern != null) boundLantern.EmberBroken += HandleEmberBroken;
        }
        private void HandleEmberCollected() => PlayHaptic(HapticCue.EmberPickup);
        private void HandleEmberBroken() => PlayHaptic(HapticCue.EmberBreak);
        private void HandleHapticSceneChange(Scene previous, Scene next) => HaltRumble();
        private void HandleHapticDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == rumblePad && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Disabled || change == InputDeviceChange.SoftReset || change == InputDeviceChange.HardReset)) HaltRumble();
        }
        private void OnApplicationFocus(bool focused) { hapticsFocusLost = !focused; if (!focused) HaltRumble(); }
        private void OnApplicationPause(bool paused) { hapticsAppPaused = paused; if (paused) HaltRumble(); }
        private void OnApplicationQuit() => HaltRumble();
    }
}
