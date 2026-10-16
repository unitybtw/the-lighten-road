using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Controls lantern illumination, Dim/Flare scout states, 2D dynamic shadows, and flame flicker.
    /// In accordance with Game Designer Furkan's feedback:
    /// - Dim radius is 2.75 units (50% of old flare) with infinite light.
    /// - Flare radius is 11.0 units (200% of old flare) acting as a powerful scout mode.
    /// - Each scout use expends 1 charge from the player's ember inventory.
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
        [Tooltip("50% of old Flare radius (2.75 units).")]
        [SerializeField, Min(0.01f)] private float dimRadius = 2.75f;
        [Tooltip("200% of old Flare radius (11.0 units).")]
        [SerializeField, Min(0.01f)] private float flareRadius = 11.0f;
        [SerializeField] private Color lanternColor = new Color(1f, 0.58f, 0.22f);
        [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float lowFuelMinimumLight = 0.5f;
        [SerializeField, Min(0.1f)] private float lowFuelPulseRate = 5f;

        [Header("Flare Scout Timing")]
        [Tooltip("Duration in seconds that 1 ember charge keeps Flare scout light active.")]
        [SerializeField, Min(1f)] private float flareScoutDuration = 6.0f;
        [SerializeField] private bool useTimedScoutMode = true;

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

        /// <summary>Raised when the lantern switches between Dim (false) and Flare (true).</summary>
        public event System.Action<bool> FlareChanged;

        public bool IsFlared { get; private set; }
        public bool ShowDebugOverlay { get => showDebugOverlay; set => showDebugOverlay = value; }
        public LanternFuel Fuel => fuel;
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
        public bool EnableShadows { get => enableShadows; set { enableShadows = value; ApplyLight(); } }
        public float ShadowIntensity { get => shadowIntensity; set { shadowIntensity = value; ApplyLight(); } }
        public float ShadowSoftness { get => shadowSoftness; set { shadowSoftness = value; ApplyLight(); } }
        public bool EnableFlicker { get => enableFlicker; set => enableFlicker = value; }
        public float FlickerIntensity => flickerIntensity;
        public float FlickerSpeed => flickerSpeed;

        private void Awake()
        {
            fuel = GetComponent<LanternFuel>();
            if (lanternLight == null)
                lanternLight = GetComponentInChildren<Light2D>();
            keyboard = Keyboard.current;
            fuel.FuelDepleted += HandleFuelDepleted;
            ApplyLight();
        }

        private void Update()
        {
            keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleFlareKey].wasPressedThisFrame)
                ToggleFlare();

            if (IsFlared)
            {
                if (useTimedScoutMode)
                {
                    flareTimer -= Time.deltaTime;
                    if (flareTimer <= 0f)
                    {
                        SetFlared(false);
                    }
                }
                else
                {
                    fuel.TickLanternDrain(true, Time.deltaTime);
                }
            }

            ApplyLight();
        }

        public void ToggleFlare()
        {
            SetFlared(!IsFlared);
        }

        public bool SetFlared(bool flared)
        {
            if (flared)
            {
                if (fuel.CurrentFuel <= 0f)
                    return false;

                if (!IsFlared)
                {
                    // Deduct 1 charge when activating Flare scout mode
                    if (useTimedScoutMode)
                    {
                        if (!fuel.TrySpendFuel(1f))
                            return false;
                        flareTimer = flareScoutDuration;
                    }
                }
            }
            else
            {
                flareTimer = 0f;
            }

            if (IsFlared == flared)
                return true;

            IsFlared = flared;
            ApplyLight();
            FlareChanged?.Invoke(IsFlared);
            return true;
        }

        private void HandleFuelDepleted()
        {
            SetFlared(false);
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

            lanternLight.intensity = (IsFlared ? flareIntensity : dimIntensity) * pulse * flicker;
            lanternLight.pointLightOuterRadius = (IsFlared ? flareRadius : dimRadius) * pulse * (1f + (flicker - 1f) * (flickerRadius / Mathf.Max(0.001f, flickerIntensity)));
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
                $"[F] {(IsFlared ? "FLARE (SCOUT)" : "DIM (INFINITE)")} — Embers: {fuel.TotalEmbers} ({fuel.TotalCharges} charges)");
        }
    }
}
