using UnityEngine;
using TheLightenRoad.Input;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Controls lantern illumination, Dim/Flare scout states, 2D dynamic shadows, and flame flicker.
    /// In accordance with Game Designer Furkan's feedback:
    /// - Dim radius is 2.75 units (50% of old flare) while an ember is held.
    /// - Flare radius is 11.0 units (200% of old flare) acting as a powerful scout mode.
    /// - Each scout use permanently expends 1 full ember from the player's ember inventory.
    /// </summary>
    [RequireComponent(typeof(LanternFuel))]
    public sealed class LanternController : MonoBehaviour
    {
        [Header("Prototype input — confirm with the team")]
        [SerializeField] private Key toggleFlareKey = Key.F;

        [Header("Prototype lighting — tune during playtesting")]
        [SerializeField] private Light2D lanternLight;
        [SerializeField, Min(0f)] private float dimIntensity = 0.55f;
        [SerializeField, Min(0f)] private float flareIntensity = 1.25f;
        [Tooltip("Reduced by 30% from 2.75 to 1.925 units per Furkan's balancing feedback to limit dimmed visibility.")]
        [SerializeField, Min(0.01f)] private float dimRadius = 1.925f;
        [Tooltip("200% of old Flare radius (11.0 units).")]
        [SerializeField, Min(0.01f)] private float flareRadius = 11.0f;
        [SerializeField] private Color lanternColor = new Color(1f, 0.58f, 0.22f);
        [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float lowFuelMinimumLight = 0.5f;
        [SerializeField, Min(0.1f)] private float lowFuelPulseRate = 5f;

        [Header("Flare Scout Timing")]
        [Tooltip("Duration in seconds that 1 full ember keeps Flare scout light active.")]
        [SerializeField, Min(1f)] private float flareScoutDuration = 6.0f;
        [SerializeField] private bool useTimedScoutMode = true;

        [Header("Light transition")]
        [Tooltip("Fade time is included in the scout duration, not added after it.")]
        [SerializeField, Min(0.05f)] private float transitionDuration = 0.65f;

        [Header("2D Dynamic Shadows")]
        [SerializeField] private bool enableShadows = true;
        [SerializeField, Range(0f, 1f)] private float shadowIntensity = 0.85f;
        [SerializeField, Range(0f, 1f)] private float shadowSoftness = 0.25f;

        [Header("Organic Flame Flicker")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField, Range(0f, 0.2f)] private float flickerIntensity = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float flickerRadius = 0.05f;
        [SerializeField, Min(0.1f)] private float flickerSpeed = 8f;

        [Header("Debug")]
        [Tooltip("Legacy IMGUI meter. LanternHUD replaces it; enable only for debugging.")]
        [SerializeField] private bool showDebugOverlay;

        private LanternFuel fuel;
        private Keyboard keyboard;
        private float flareTimer;
        private float flareBlend;
        private float scoutElapsed;
        private float burnProgress;
        private float releaseElapsed;
        private float releaseDuration;
        private float releaseStartBlend;
        private float releaseStartProgress;
        private int scoutChargesBefore;
        private bool activeScout;
        private bool releasing;
        private bool reservingCharge;

        /// <summary>Global active instance reference for fast zero-allocation access.</summary>
        public static LanternController ActiveInstance { get; private set; }

        /// <summary>Raised when the lantern switches between Dim (false) and Flare (true).</summary>
        public event System.Action<bool> FlareChanged;
        /// <summary>Raised globally when any lantern switches between Dim and Flare.</summary>
        public static event System.Action<LanternController, bool> AnyFlareChanged;
        /// <summary>Raised once when a spent ember finishes its burn animation.</summary>
        public event System.Action EmberBroken;

        public bool IsFlared { get; private set; }
        public bool ShowDebugOverlay { get => showDebugOverlay; set => showDebugOverlay = value; }
        public LanternFuel Fuel => fuel != null ? fuel : (fuel = GetComponent<LanternFuel>());
        public bool IsLowFuel => fuel != null && fuel.NormalizedFuel <= lowFuelThreshold;
        public float LowFuelThreshold => lowFuelThreshold;
        public Key ToggleFlareKey { get => toggleFlareKey; set => toggleFlareKey = value; }
        public Light2D LanternLight => lanternLight;
        public float DimIntensity => dimIntensity;
        public float FlareIntensity => flareIntensity;
        public float DimRadius => dimRadius;
        public float FlareRadius => flareRadius;
        public float FlareTimer => flareTimer;
        public float FlareScoutDuration => flareScoutDuration;
        public float TransitionDuration => transitionDuration;
        public float FlareBlend => flareBlend;
        public bool HasActiveScout => activeScout;
        public bool IsDimming => releasing;
        public float ScoutProgress => activeScout ? 1f - burnProgress : 0f;
        public int ScoutEmberIndex => fuel != null ? fuel.TotalCharges / 2 : 0;
        // A single scout now burns a whole stone, including its cracking phase.
        public bool ScoutIsSecondUse => true;
        public float ScoutEmberLevel => activeScout ? 1f - burnProgress : 0f;
        public bool EnableShadows { get => enableShadows; set { enableShadows = value; ApplyLight(); } }
        public float ShadowIntensity { get => shadowIntensity; set { shadowIntensity = value; ApplyLight(); } }
        public float ShadowSoftness { get => shadowSoftness; set { shadowSoftness = value; ApplyLight(); } }
        public bool EnableFlicker { get => enableFlicker; set => enableFlicker = value; }
        public float FlickerIntensity => flickerIntensity;
        public float FlickerSpeed => flickerSpeed;

        private void Awake()
        {
            ActiveInstance = this;
            fuel = GetComponent<LanternFuel>();
            useTimedScoutMode = true; // Migrate legacy scenes to the whole-ember scout contract.
            if (lanternLight == null)
                lanternLight = GetComponentInChildren<Light2D>();
            keyboard = Keyboard.current;
            fuel.FuelDepleted += HandleFuelDepleted;
            ApplyLight();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
        }

        private TriggerPressGate flareTrigger;
        private int flarePadId=-1;

        private void OnDisable()
        {
            if (ActiveInstance == this) ActiveInstance = null;
        }

        private void Update()
        {
            keyboard = Keyboard.current;
            var pad = GameControls.ActivePad;
            int padId=pad!=null?pad.deviceId:-1;
            float trigger=pad!=null?pad.rightTrigger.ReadValue():0;
            if(padId!=flarePadId){flareTrigger.Seed(trigger);flarePadId=padId;}
            bool gamepadFlare=flareTrigger.Read(trigger);
            if (GameControls.GameplayAllowed && ((keyboard != null && keyboard[toggleFlareKey].wasPressedThisFrame) || gamepadFlare))
                ToggleFlare();

            TickScout(Time.deltaTime);
        }

        /// <summary>Advance the shared light / ember timeline using scaled gameplay time.</summary>
        public void TickScout(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            if (activeScout)
            {
                if (releasing)
                {
                    releaseElapsed += dt;
                    float t = Mathf.Clamp01(releaseElapsed / Mathf.Max(0.001f, releaseDuration));
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    flareBlend = Mathf.Lerp(releaseStartBlend, 0f, eased);
                    burnProgress = Mathf.Lerp(releaseStartProgress, 1f, eased);
                    flareTimer = Mathf.Max(0f, releaseDuration - releaseElapsed);
                    if (t >= 1f) FinishScout();
                }
                else
                {
                    scoutElapsed = Mathf.Min(flareScoutDuration, scoutElapsed + dt);
                    flareTimer = Mathf.Max(0f, flareScoutDuration - scoutElapsed);
                    burnProgress = Mathf.Clamp01(scoutElapsed / Mathf.Max(0.001f, flareScoutDuration));
                    float fade = Mathf.Min(transitionDuration, flareScoutDuration * 0.5f);
                    flareBlend = Mathf.Min(Mathf.SmoothStep(0f, 1f, scoutElapsed / fade),
                        Mathf.SmoothStep(0f, 1f, flareTimer / fade));
                    if (flareTimer <= 0f) FinishScout();
                }
            }
            else
            {
                if (IsFlared && !useTimedScoutMode)
                    fuel.TickLanternDrain(true, dt);
                flareBlend = Mathf.MoveTowards(flareBlend, IsFlared ? 1f : 0f,
                    dt / Mathf.Max(0.05f, transitionDuration));
            }
            ApplyLight();
        }

        public void ToggleFlare() => SetFlared(!IsFlared);

        public bool SetFlared(bool flared)
        {
            if (IsFlared == flared) return true;
            if (flared)
            {
                // An early release must finish before another charge can be reserved.
                if (activeScout || fuel.CurrentFuel < LanternFuel.ChargesPerEmber) return false;
                if (useTimedScoutMode)
                {
                    scoutChargesBefore = fuel.TotalCharges;
                    reservingCharge = true;
                    bool spent;
                    try { spent = fuel.TrySpendEmber(); }
                    finally { reservingCharge = false; }
                    if (!spent) return false;
                    scoutElapsed = burnProgress = releaseElapsed = 0f;
                    releasing = false;
                    activeScout = true;
                    flareTimer = flareScoutDuration;
                }
            }
            else if (activeScout)
            {
                releasing = true;
                releaseElapsed = 0f;
                releaseDuration = Mathf.Min(transitionDuration, flareTimer);
                releaseStartBlend = flareBlend;
                releaseStartProgress = burnProgress;
                flareTimer = releaseDuration;
            }
            IsFlared = flared;
            FlareChanged?.Invoke(IsFlared);
            AnyFlareChanged?.Invoke(this, IsFlared);
            ApplyLight();
            return true;
        }

        private void FinishScout()
        {
            if (!activeScout) return;
            bool broken = ScoutIsSecondUse;
            activeScout = releasing = false;
            flareTimer = flareBlend = 0f;
            burnProgress = 1f;
            bool changed = IsFlared;
            IsFlared = false;
            if (changed)
            {
                FlareChanged?.Invoke(false);
                AnyFlareChanged?.Invoke(this, false);
            }
            if (broken) EmberBroken?.Invoke();
        }

        /// <summary>Reset/retry cancels presentation without playing an ember break.</summary>
        public void ResetToDim()
        {
            bool changed = IsFlared;
            IsFlared = activeScout = releasing = false;
            flareTimer = flareBlend = burnProgress = 0f;
            if (changed)
            {
                FlareChanged?.Invoke(false);
                AnyFlareChanged?.Invoke(this, false);
            }
            ApplyLight();
        }

        private void HandleFuelDepleted()
        {
            // Spending the last reserved charge still grants its complete scout duration.
            if (!reservingCharge) SetFlared(false);
        }

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            if (fuel != null) fuel.FuelDepleted -= HandleFuelDepleted;
        }

        private void ApplyLight()
        {
            if (lanternLight == null)
                return;

            lanternLight.color = lanternColor;
            lanternLight.shadowsEnabled = enableShadows;
            lanternLight.shadowIntensity = shadowIntensity;
            lanternLight.shadowSoftness = shadowSoftness;

            float pulse = 1f;
            if (fuel != null && fuel.NormalizedFuel <= lowFuelThreshold)
            {
                float phase = (Mathf.Sin(Time.time * lowFuelPulseRate * Mathf.PI * 2f) + 1f) * 0.5f;
                pulse = Mathf.Lerp(lowFuelMinimumLight, 1f, phase);
            }

            float flicker = 1f;
            if (enableFlicker)
            {
                float noise = (Mathf.PerlinNoise(Time.time * flickerSpeed, 0.31f) * 2f - 1f);
                flicker = 1f + noise * flickerIntensity;
            }

            float passiveIntensity = fuel != null && !fuel.IsEmpty ? dimIntensity : 0f;
            float passiveRadius = passiveIntensity > 0f ? dimRadius : 0.001f;
            lanternLight.intensity = Mathf.Lerp(passiveIntensity, flareIntensity, flareBlend) * pulse * flicker;
            lanternLight.pointLightOuterRadius = Mathf.Lerp(passiveRadius, flareRadius, flareBlend) * pulse * (1f + (flicker - 1f) * (flickerRadius / Mathf.Max(0.001f, flickerIntensity)));
        }

        private void OnGUI()
        {
            if (!showDebugOverlay || fuel == null)
                return;

            const float width = 260f;
            const float height = 52f;
            var panel = new Rect(16f, 16f, width, height);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(28f, 20f, width - 20f, 20f),
                $"[F] {(IsFlared ? "FLARE (SCOUT)" : "DIM")} - Embers: {fuel.TotalEmbers} ({fuel.TotalCharges} charges)");
        }
    }
}
