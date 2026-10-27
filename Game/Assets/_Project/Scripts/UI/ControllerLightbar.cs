using System.Collections;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.SceneManagement;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Controls the dynamic RGB lightbar on DualSense and DualShock controllers.
    /// Reflects lantern flame, flare states, crystal pickups, damage, low fuel, and level completion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerLightbar : MonoBehaviour
    {
        private static ControllerLightbar instance;
        public static bool HasInstance => instance != null;
        public static ControllerLightbar Instance => instance;

        public static readonly Color AmberLantern = new Color(0.95f, 0.58f, 0.14f);
        public static readonly Color FlareRadiant = new Color(0.35f, 0.90f, 1.00f);
        public static readonly Color EmberMint = new Color(0.15f, 1.00f, 0.65f);
        public static readonly Color EmberCrackle = new Color(1.00f, 0.40f, 0.05f);
        public static readonly Color DangerCrimson = new Color(0.95f, 0.08f, 0.05f);
        public static readonly Color ExtinguishedBlack = new Color(0.00f, 0.00f, 0.00f);
        public static readonly Color ArchiveBlue = new Color(0.20f, 0.35f, 0.65f);
        public static readonly Color ChamberAura = new Color(0.35f, 0.95f, 0.80f);

        private Color currentDisplayedColor = AmberLantern;
        private Color baseAmbientColor = AmberLantern;
        private Color pulseColor = AmberLantern;
        private float pulseRemainingTimer;
        private float pulseTotalDuration;
        private float pulsePriority;
        private Coroutine activeSpecialSequence;
        private double nextBluetoothUpdate;
        private Color lastSentColor = Color.black;
        private int lastSentPadId = -1;
        private bool focusLost, applicationPaused;
        private bool Suspended => focusLost || applicationPaused;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("TLR.Menu.ControllerLightbar", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("TLR.Menu.ControllerLightbar", value ? 1 : 0);
                PlayerPrefs.Save();
                DualSenseBluetoothRumble.LightbarEnabled = value;
                if (!value && instance != null)
                {
                    instance.ApplyColorImmediate(ExtinguishedBlack);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureExists();

        public static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("Controller Lightbar Service");
            instance = go.AddComponent<ControllerLightbar>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            baseAmbientColor = AmberLantern;
            currentDisplayedColor = AmberLantern;
            DualSenseBluetoothRumble.LightbarEnabled = Enabled;

            LanternController.AnyFlareChanged += HandleAnyFlareChanged;
            EmberPickup.EmberCollected += HandleEmberCollected;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            LanternController.AnyFlareChanged -= HandleAnyFlareChanged;
            EmberPickup.EmberCollected -= HandleEmberCollected;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
            {
                ApplyColorImmediate(ExtinguishedBlack);
                instance = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            ApplyColorImmediate(currentDisplayedColor);
        }

        private void OnApplicationFocus(bool focused)
        {
            focusLost = !focused;
            ApplyColorImmediate(currentDisplayedColor);
        }

        private void OnDisable()
        {
            if (instance != this) return;
            if (activeSpecialSequence != null) StopCoroutine(activeSpecialSequence);
            activeSpecialSequence = null;
            ApplyColorImmediate(ExtinguishedBlack);
        }

        private void OnApplicationQuit()
        {
            ApplyColorImmediate(ExtinguishedBlack);
        }

        public static void Pulse(Color color, float duration = 0.5f, float priority = 1f)
        {
            EnsureExists();
            if (instance != null) instance.TriggerPulse(color, duration, priority);
        }

        public static void SetAmbientColor(Color color)
        {
            EnsureExists();
            if (instance != null) instance.baseAmbientColor = color;
        }

        public static void HandleHapticCue(HapticCue cue, float strength = 1f)
        {
            EnsureExists();
            if (instance == null || !Enabled) return;

            switch (cue)
            {
                case HapticCue.Flare:
                    Pulse(FlareRadiant, 1.1f, 3f);
                    break;
                case HapticCue.EmberPickup:
                    Pulse(EmberMint, 0.45f, 2f);
                    break;
                case HapticCue.EmberBreak:
                    Pulse(EmberCrackle, 0.40f, 2f);
                    break;
                case HapticCue.Death:
                    instance.TriggerDeathSequence();
                    break;
                case HapticCue.ChamberComplete:
                    instance.TriggerChamberCompleteSequence();
                    break;
                case HapticCue.Preview:
                    Pulse(FlareRadiant, 0.85f, 1f);
                    break;
            }
        }

        private void TriggerPulse(Color color, float duration, float priority)
        {
            if (priority < pulsePriority && pulseRemainingTimer > 0f) return;
            pulseColor = color;
            pulseTotalDuration = Mathf.Max(0.05f, duration);
            pulseRemainingTimer = pulseTotalDuration;
            pulsePriority = priority;
        }

        public void TriggerDeathSequence()
        {
            if (activeSpecialSequence != null) StopCoroutine(activeSpecialSequence);
            activeSpecialSequence = StartCoroutine(DeathSequenceRoutine());
        }

        public void TriggerChamberCompleteSequence()
        {
            if (activeSpecialSequence != null) StopCoroutine(activeSpecialSequence);
            activeSpecialSequence = StartCoroutine(ChamberCompleteSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            pulsePriority = 10f;
            // Stark red strike
            ApplyColorImmediate(new Color(1f, 0.05f, 0.05f));
            yield return new WaitForSecondsRealtime(0.18f);

            // Smooth fading out into cold dark abyss
            float elapsed = 0f;
            float fadeTime = 0.9f;
            Color start = new Color(0.6f, 0.05f, 0.1f);
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                Color c = Color.Lerp(start, ExtinguishedBlack, elapsed / fadeTime);
                ApplyColorImmediate(c);
                yield return null;
            }
            ApplyColorImmediate(ExtinguishedBlack);
            pulsePriority = 0f;
            activeSpecialSequence = null;
        }

        private IEnumerator ChamberCompleteSequenceRoutine()
        {
            pulsePriority = 10f;
            float elapsed = 0f;
            float duration = 2.4f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                // Auroral wave: Radiant Cyan -> Lavender Violet -> Ethereal Amber Gold
                Color c;
                if (t < 0.45f)
                {
                    c = Color.Lerp(FlareRadiant, new Color(0.70f, 0.40f, 0.95f), t / 0.45f);
                }
                else if (t < 0.80f)
                {
                    c = Color.Lerp(new Color(0.70f, 0.40f, 0.95f), AmberLantern, (t - 0.45f) / 0.35f);
                }
                else
                {
                    c = Color.Lerp(AmberLantern, baseAmbientColor, (t - 0.80f) / 0.20f);
                }

                ApplyColorImmediate(c);
                yield return null;
            }

            pulsePriority = 0f;
            activeSpecialSequence = null;
        }

        private void HandleAnyFlareChanged(LanternController lantern, bool flared)
        {
            if (flared)
            {
                TriggerPulse(FlareRadiant, 1.2f, 3f);
            }
        }

        private void HandleEmberCollected()
        {
            TriggerPulse(EmberMint, 0.5f, 2f);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (activeSpecialSequence != null)
            {
                StopCoroutine(activeSpecialSequence);
                activeSpecialSequence = null;
            }
            pulseRemainingTimer = 0f;
            pulsePriority = 0f;
            baseAmbientColor = scene.name.Contains("Menu") ? ArchiveBlue : AmberLantern;
            ApplyColorImmediate(baseAmbientColor);
        }

        private void Update()
        {
            if (!Enabled || Suspended) return;
            if (activeSpecialSequence != null) return;

            Color targetColor = baseAmbientColor;

            // Check if paused
            if (Time.timeScale <= 0f)
            {
                targetColor = ArchiveBlue;
            }
            else
            {
                // Organic breathing living lantern flicker in ambient
                float flicker = 0.90f + 0.10f * Mathf.Sin(Time.time * 2.4f) * Mathf.Cos(Time.time * 0.7f);
                targetColor = baseAmbientColor * flicker;

                // Check for low lantern fuel
                var fuel = FindAnyObjectByType<LanternFuel>();
                if (fuel != null && fuel.TotalEmbers <= 1)
                {
                    float heartbeat = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.8f);
                    targetColor = Color.Lerp(AmberLantern * 0.4f, DangerCrimson, heartbeat);
                }
            }

            // Blend pulse override
            if (pulseRemainingTimer > 0f)
            {
                pulseRemainingTimer -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(pulseRemainingTimer / pulseTotalDuration);
                targetColor = Color.Lerp(targetColor, pulseColor, Mathf.SmoothStep(0f, 1f, t));
                if (pulseRemainingTimer <= 0f) pulsePriority = 0f;
            }

            // Smooth transition
            currentDisplayedColor = Color.Lerp(currentDisplayedColor, targetColor, Time.unscaledDeltaTime * 7f);

            // Transmit at controlled interval (25 Hz max)
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextBluetoothUpdate)
            {
                if ((ControllerGlyphService.GetActiveGamepad()?.deviceId ?? -1) != lastSentPadId || ColorDistance(currentDisplayedColor, lastSentColor) > 0.015f)
                {
                    ApplyColorImmediate(currentDisplayedColor);
                    nextBluetoothUpdate = now + 0.040; // 25 Hz
                }
            }
        }

        public void ApplyColorImmediate(Color color)
        {
            currentDisplayedColor = color;
            DualSenseBluetoothRumble.LightbarEnabled = Enabled;

            if (!Enabled || Suspended || !isActiveAndEnabled)
            {
                color = ExtinguishedBlack;
            }

            Color32 c32 = (Color32)color;
            c32.a = 255;

            var pad = ControllerGlyphService.GetActiveGamepad();
            if (pad == null || !pad.added) return;

            try
            {
                if (DualSenseBluetoothRumble.IsBluetooth(pad))
                {
                    if (!DualSenseBluetoothRumble.SetLightbarColor(pad, c32)) return;
                }
                else if (pad is DualShockGamepad ds)
                {
                    ds.SetLightBarColor(color);
                }
                else return;
                lastSentColor = color;
                lastSentPadId = pad.deviceId;
            }
            catch (System.Exception)
            {
                // Driver failures or controller unplug must never throw into game loop.
            }
        }

        private static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }
    }
}
