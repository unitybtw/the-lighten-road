using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern
{
    /// <summary>Prototype Dim / Flare input and URP 2D light presentation.</summary>
    [RequireComponent(typeof(LanternFuel))]
    public sealed class LanternController : MonoBehaviour
    {
        [Header("Prototype input — confirm with the team")]
        [SerializeField] private Key toggleFlareKey = Key.F;

        [Header("Prototype lighting — tune during playtesting")]
        [SerializeField] private Light2D lanternLight;
        [SerializeField, Min(0f)] private float dimIntensity = 0.25f;
        [SerializeField, Min(0f)] private float flareIntensity = 1.1f;
        [SerializeField, Min(0.01f)] private float dimRadius = 2f;
        [SerializeField, Min(0.01f)] private float flareRadius = 5.5f;
        [SerializeField] private Color lanternColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float lowFuelMinimumLight = 0.45f;
        [SerializeField, Min(0.1f)] private float lowFuelPulseRate = 5f;

        [Header("Debug")]
        [Tooltip("Legacy IMGUI meter. LanternHUD replaces it; enable only for debugging.")]
        [SerializeField] private bool showDebugOverlay;

        private LanternFuel fuel;
        private Keyboard keyboard;

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
                SetFlared(!IsFlared);

            fuel.TickLanternDrain(IsFlared, Time.deltaTime);
            ApplyLight();
        }

        public bool SetFlared(bool flared)
        {
            if (flared && fuel.CurrentFuel <= 0f)
                return false;

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
            float pulse = 1f;
            if (fuel.NormalizedFuel <= lowFuelThreshold)
            {
                float phase = (Mathf.Sin(Time.time * lowFuelPulseRate * Mathf.PI * 2f) + 1f) * 0.5f;
                pulse = Mathf.Lerp(lowFuelMinimumLight, 1f, phase);
            }

            lanternLight.intensity = (IsFlared ? flareIntensity : dimIntensity) * pulse;
            lanternLight.pointLightOuterRadius = (IsFlared ? flareRadius : dimRadius) * pulse;
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
                $"LANTERN  {(IsFlared ? "FLARE" : "DIM")}  |  F: toggle");

            var previousColor = GUI.color;
            GUI.color = new Color(0.16f, 0.13f, 0.1f, 1f);
            GUI.DrawTexture(new Rect(28f, 43f, width - 24f, 12f), Texture2D.whiteTexture);
            GUI.color = lanternColor;
            GUI.DrawTexture(new Rect(28f, 43f, (width - 24f) * fuel.NormalizedFuel, 12f), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(new Rect(width + 24f, 39f, 100f, 20f),
                $"{fuel.CurrentFuel:0}/{fuel.Capacity:0}");
        }

        private void OnDestroy()
        {
            if (fuel != null)
                fuel.FuelDepleted -= HandleFuelDepleted;
        }
    }
}
