using System.Collections;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
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

        public static bool AdaptiveTriggersEnabled
        {
            get => PlayerPrefs.GetInt("TLR.Menu.AdaptiveTriggersEnabled", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("TLR.Menu.AdaptiveTriggersEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) StopRumble();
                UpdateAdaptiveTriggers(true);
            }
        }

        private static DualSenseTriggerEffect activeLanternTrigger = DualSenseTriggerEffect.Off;
        public static DualSenseTriggerEffect CurrentRightTriggerEffect => activeLanternTrigger;

        public static void UpdateAdaptiveTriggers(bool force = false)
        {
            if (instance != null) instance.EvaluateAdaptiveTriggers(force);
        }

        public void EvaluateAdaptiveTriggers(bool force = false)
        {
            var pad = GetActiveGamepad();
            if (pad == null || !pad.added || !DualSenseBluetoothRumble.IsBluetooth(pad))
            {
                activeLanternTrigger = DualSenseTriggerEffect.Off;
                return;
            }

            DualSenseTriggerEffect target = DualSenseTriggerEffect.Off;
            if (AdaptiveTriggersEnabled && !HapticsSuspended && Time.timeScale > 0f && boundLantern != null)
            {
                // When lantern is unlit: Two-Stage Mechanical Flint Lock (Section mode: 0.35 to 0.65 threshold)
                // When lantern is flared (burning): Trigger is released and completely free (Off)
                if (!boundLantern.IsFlared)
                {
                    target = DualSenseHapticPatterns.GetFlintLockTriggerEffect();
                }
                else
                {
                    target = DualSenseTriggerEffect.Off;
                }
            }

            if (force || !activeLanternTrigger.Equals(target))
            {
                activeLanternTrigger = target;
                if (activeRumbleRoutine == null)
                {
                    DualSenseBluetoothRumble.Send(pad, 0f, 0f, DualSenseTriggerEffect.Off, activeLanternTrigger);
                }
            }
        }

        public static void PlayHaptic(HapticCue cue, float strength = 1f)
        {
            float low, high, duration;
            int priority;
            switch (cue)
            {
                case HapticCue.Jump: low = .10f; high = .16f; duration = .065f; priority = 0; break;
                case HapticCue.Landing: low = .40f; high = .18f; duration = .16f; priority = 1; break;
                case HapticCue.Flare: low = .32f; high = .42f; duration = .28f; priority = 2; break;
                case HapticCue.EmberPickup: low = .10f; high = .24f; duration = .13f; priority = 0; break;
                case HapticCue.EmberBreak: low = .18f; high = .40f; duration = .15f; priority = 2; break;
                case HapticCue.Death: low = .52f; high = .66f; duration = .38f; priority = 4; break;
                case HapticCue.ChamberComplete: low = .28f; high = .36f; duration = .48f; priority = 3; break;
                default: low = .18f; high = .28f; duration = .16f; priority = 1; break;
            }
            ControllerLightbar.HandleHapticCue(cue, strength);
            if (!IsFinite(strength) || strength <= 0f || !VibrationEnabled) return;
            EnsureExists();
            instance.TriggerRumble(low * Mathf.Clamp01(strength), high * Mathf.Clamp01(strength), duration,
                priority, cue == HapticCue.Preview, cue == HapticCue.Flare ? .045f : .008f, cue, Mathf.Clamp01(strength));
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

        private void TriggerRumble(float low, float high, float duration, int priority, bool allowPaused, float attack, HapticCue? cue = null, float strength = 1f)
        {
            var pad = GetActiveGamepad();
            if (pad == null || !pad.added || !isActiveAndEnabled || HapticsSuspended || duration <= 0f ||
                (Time.timeScale <= 0f && !allowPaused)) return;
            if (activeRumbleRoutine != null && pad == rumblePad && priority < rumblePriority) return;
            HaltRumble();
            rumblePad = pad;
            rumblePriority = priority;
            bool dualSense = pad is DualSenseGamepadHID && cue.HasValue;
            if (dualSense) duration = DualSenseHapticPatterns.Duration(cue.Value);
            activeRumbleRoutine = StartCoroutine(RumbleRoutine(pad, low, high, Mathf.Min(duration, 2f), allowPaused, attack, dualSense ? cue : null, strength));
        }

        private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float duration, bool allowPaused, float attack, HapticCue? cue, float strength)
        {
            float elapsed = 0f;
            while (elapsed < duration && VibrationEnabled && pad.added && GetActiveGamepad() == pad &&
                (!HapticsSuspended) && (allowPaused || Time.timeScale > 0f))
            {
                float envelope = Mathf.Clamp01((elapsed + .004f) / attack) * Mathf.Pow(1f - elapsed / duration, 1.4f);
                Vector2 texture = cue.HasValue ? DualSenseHapticPatterns.Sample(cue.Value, elapsed / duration) : Vector2.one * envelope;
                
                DualSenseTriggerEffect leftTrigger = DualSenseTriggerEffect.Off;
                DualSenseTriggerEffect rightTrigger = activeLanternTrigger;

                if (cue.HasValue && AdaptiveTriggersEnabled)
                {
                    float normTime = elapsed / duration;
                    leftTrigger = DualSenseHapticPatterns.GetLeftTriggerEffect(cue.Value, normTime, strength);
                    var cueTrigger = DualSenseHapticPatterns.GetRightTriggerEffect(cue.Value, normTime, strength);
                    if (cue.Value == HapticCue.Preview || cueTrigger.IsActive)
                    {
                        rightTrigger = cueTrigger;
                    }
                }

                SetMotors(pad, low * texture.x, high * texture.y, leftTrigger, rightTrigger);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetMotors(pad, 0f, 0f, DualSenseTriggerEffect.Off, activeLanternTrigger);
            rumblePad = null;
            activeRumbleRoutine = null;
        }

        private static void SetMotors(Gamepad pad, float low, float high, DualSenseTriggerEffect leftTrigger, DualSenseTriggerEffect rightTrigger)
        {
            if (pad == null) return;
            try
            {
                if (DualSenseBluetoothRumble.IsBluetooth(pad))
                {
                    DualSenseBluetoothRumble.Send(pad, low, high, leftTrigger, rightTrigger);
                    return;
                }
                pad.SetMotorSpeeds(low, high);
            }
            catch (System.Exception) { /* Unsupported/disconnected drivers must not interrupt gameplay. */ }
        }

        private static void SetMotors(Gamepad pad, float low, float high, float rightTrigger = 0f)
        {
            var rt = rightTrigger > 0f ? DualSenseTriggerEffect.Continuous(80f / 255f, Mathf.Min(rightTrigger, .22f)) : DualSenseTriggerEffect.Off;
            SetMotors(pad, low, high, DualSenseTriggerEffect.Off, rt);
        }

        private void HaltRumble()
        {
            if (activeRumbleRoutine != null) StopCoroutine(activeRumbleRoutine);
            activeRumbleRoutine = null;
            SetMotors(rumblePad, 0f, 0f, DualSenseTriggerEffect.Off, activeLanternTrigger);
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
            EvaluateAdaptiveTriggers(true);
        }
        private void HandleEmberCollected() { PlayHaptic(HapticCue.EmberPickup); DualSenseSpeaker.Play(SpeakerCue.Pickup); }
        private void HandleEmberBroken() { PlayHaptic(HapticCue.EmberBreak); DualSenseSpeaker.Play(SpeakerCue.Break); }
        private void HandleHapticSceneChange(Scene previous, Scene next) { HaltRumble(); EvaluateAdaptiveTriggers(true); }
        private void HandleHapticDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == rumblePad && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected ||
                change == InputDeviceChange.Disabled || change == InputDeviceChange.SoftReset || change == InputDeviceChange.HardReset)) HaltRumble();
            EvaluateAdaptiveTriggers(true);
        }
        private void OnApplicationFocus(bool focused) { hapticsFocusLost = !focused; if (!focused) HaltRumble(); EvaluateAdaptiveTriggers(true); }
        private void OnApplicationPause(bool paused) { hapticsAppPaused = paused; if (paused) HaltRumble(); EvaluateAdaptiveTriggers(true); }
        private void OnApplicationQuit() => HaltRumble();
    }
}
