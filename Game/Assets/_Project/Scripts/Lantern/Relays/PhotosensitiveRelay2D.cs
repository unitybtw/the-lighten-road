using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern.Relays
{
    public enum RelayMode
    {
        Timed,
        Latched,
        Continuous
    }

    /// <summary>
    /// Ancient photosensitive solar crystal relay etched onto archive pedestals.
    /// Absorbs photon energy from the player's lantern (Dim or Flare).
    /// When fully charged, powers connected mechanisms, opens massive archive gates,
    /// or extends ethereal bridges. Supports Timed decay, permanent Latch, or Continuous hold.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class PhotosensitiveRelay2D : MonoBehaviour
    {
        [Header("Relay Mode & Activation")]
        [SerializeField] private RelayMode mode = RelayMode.Timed;
        [Tooltip("Seconds of continuous illumination required to charge from 0% to 100%.")]
        [SerializeField, Min(0.1f)] private float chargeTime = 1.25f;
        [Tooltip("Seconds the relay remains fully active after light leaves in Timed mode.")]
        [SerializeField, Min(0.1f)] private float lingerDuration = 5.0f;
        [Tooltip("Rate per second at which charge drops when unlit (1.0 = full charge).")]
        [SerializeField, Min(0.05f)] private float dischargeSpeed = 0.45f;
        [Tooltip("Multiplier applied to charge rate when struck by high-intensity Flare.")]
        [SerializeField, Min(1.0f)] private float flareMultiplier = 3.0f;

        [Header("Illumination Distances")]
        [Tooltip("Maximum distance from player lantern in Dim mode to charge this relay.")]
        [SerializeField, Min(0.5f)] private float dimActivationRadius = 3.2f;
        [Tooltip("Maximum distance from player lantern in Flare mode to charge this relay.")]
        [SerializeField, Min(1.0f)] private float flareActivationRadius = 9.5f;

        [Header("Visual Juice & Luminescence")]
        [SerializeField] private SpriteRenderer pedestalRenderer;
        [SerializeField] private SpriteRenderer crystalRenderer;
        [SerializeField] private Transform crystalTransform;
        [SerializeField] private Light2D crystalLight;
        [SerializeField] private Color dormantColor = new Color(0.28f, 0.32f, 0.48f, 0.45f); // Deep muted amethyst
        [SerializeField] private Color activeColor = new Color(0.35f, 0.95f, 1.0f, 1.0f);     // Radiant celestial cyan
        [SerializeField] private Color latchedColor = new Color(1.0f, 0.82f, 0.35f, 1.0f);   // Locked golden sunstone
        [SerializeField, Range(0.01f, 0.25f)] private float bobAmplitude = 0.08f;
        [SerializeField, Min(0.5f)] private float bobSpeed = 2.5f;

        [Header("Audio Feedback")]
        [SerializeField] private bool enableAudio = true;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.85f;

        [Header("Events")]
        public UnityEvent<float> OnChargeChanged = new UnityEvent<float>();
        public UnityEvent OnActivated = new UnityEvent();
        public UnityEvent OnDeactivated = new UnityEvent();
        public UnityEvent<bool> OnStateChanged = new UnityEvent<bool>();

        private LanternController targetLantern;
        private Vector3 baseCrystalLocalPos = new Vector3(0f, 0.85f, 0f);
        private float chargeLevel;
        private float lingerTimer;
        private bool isActivated;
        private bool isLit;
        private float soundTimer;

        public RelayMode Mode { get => mode; set => mode = value; }
        public float ChargeLevel => chargeLevel;
        public float ChargeTime { get => chargeTime; set => chargeTime = Mathf.Max(0.01f, value); }
        public float LingerDuration { get => lingerDuration; set => lingerDuration = Mathf.Max(0.01f, value); }
        public float DischargeSpeed { get => dischargeSpeed; set => dischargeSpeed = Mathf.Max(0.01f, value); }
        public float FlareMultiplier { get => flareMultiplier; set => flareMultiplier = Mathf.Max(1f, value); }
        public float DimActivationRadius { get => dimActivationRadius; set => dimActivationRadius = Mathf.Max(0.1f, value); }
        public float FlareActivationRadius { get => flareActivationRadius; set => flareActivationRadius = Mathf.Max(0.1f, value); }
        public bool IsActivated => isActivated;
        public bool IsLit => isLit;
        public float LingerTimer => lingerTimer;
        public LanternController TargetLantern { get => targetLantern; set => targetLantern = value; }
        public Transform CrystalTransform => crystalTransform;
        public Light2D CrystalLight => crystalLight;
        public SpriteRenderer CrystalRenderer => crystalRenderer;

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void Start()
        {
            if (targetLantern == null)
            {
                targetLantern = Object.FindAnyObjectByType<LanternController>();
            }
        }

        private void OnEnable()
        {
            EnsureHierarchy();
            UpdateVisuals(0f);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            // 1. Detect illumination from lantern
            bool illuminated = EvaluateIllumination(out bool isFlare, out float distToLantern, out float maxReach);
            isLit = illuminated;

            // 2. Charge or Discharge
            if (illuminated)
            {
                float proximityNorm = Mathf.Clamp01(1f - (distToLantern / Mathf.Max(0.01f, maxReach)));
                float rateMultiplier = isFlare ? flareMultiplier : (0.75f + 0.25f * proximityNorm);
                float chargeDelta = (1f / chargeTime) * rateMultiplier * dt;

                float oldCharge = chargeLevel;
                chargeLevel = Mathf.Clamp01(chargeLevel + chargeDelta);
                lingerTimer = lingerDuration;

                if (oldCharge != chargeLevel)
                {
                    OnChargeChanged?.Invoke(chargeLevel);
                }

                // Audio feedback while charging
                soundTimer -= dt;
                if (soundTimer <= 0f && chargeLevel < 0.99f && enableAudio)
                {
                    soundTimer = 0.45f;
                    float pitch = 0.85f + 0.5f * chargeLevel;
                    SoundEffectManager.Instance?.PlayCrystalCharge(pitch, soundVolume * 0.7f);
                }

                if (!isActivated && chargeLevel >= 0.999f)
                {
                    SetActivated(true);
                }
            }
            else
            {
                if (mode == RelayMode.Latched && isActivated)
                {
                    // Latched relays remain permanently at 100% once activated
                    chargeLevel = 1f;
                }
                else if (mode == RelayMode.Timed && isActivated)
                {
                    if (lingerTimer > 0f)
                    {
                        lingerTimer -= dt;
                        if (lingerTimer < 0f)
                        {
                            float excessDt = -lingerTimer;
                            lingerTimer = 0f;
                            float oldCharge = chargeLevel;
                            chargeLevel = Mathf.Clamp01(chargeLevel - dischargeSpeed * excessDt);
                            if (oldCharge != chargeLevel)
                            {
                                OnChargeChanged?.Invoke(chargeLevel);
                            }

                            if (chargeLevel <= 0.001f)
                            {
                                SetActivated(false);
                            }
                        }
                    }
                    else
                    {
                        float oldCharge = chargeLevel;
                        chargeLevel = Mathf.Clamp01(chargeLevel - dischargeSpeed * dt);
                        if (oldCharge != chargeLevel)
                        {
                            OnChargeChanged?.Invoke(chargeLevel);
                        }

                        if (chargeLevel <= 0.001f)
                        {
                            SetActivated(false);
                        }
                    }
                }
                else
                {
                    // Continuous mode or discharging while partially charged
                    if (chargeLevel > 0f)
                    {
                        float oldCharge = chargeLevel;
                        chargeLevel = Mathf.Clamp01(chargeLevel - dischargeSpeed * dt);
                        if (oldCharge != chargeLevel)
                        {
                            OnChargeChanged?.Invoke(chargeLevel);
                        }

                        if (isActivated && chargeLevel <= 0.001f)
                        {
                            SetActivated(false);
                        }
                    }
                }
            }

            // 3. Visual animation
            UpdateVisuals(dt);
        }

        private bool EvaluateIllumination(out bool isFlare, out float distance, out float maxReach)
        {
            isFlare = false;
            distance = float.MaxValue;
            maxReach = 0f;

            if (targetLantern == null)
            {
                targetLantern = Object.FindAnyObjectByType<LanternController>();
                if (targetLantern == null) return false;
            }

            if (targetLantern.Fuel != null && targetLantern.Fuel.IsEmpty)
            {
                return false;
            }

            distance = Vector2.Distance(transform.position, targetLantern.transform.position);
            isFlare = targetLantern.IsFlared;

            float allowedRelayRadius = isFlare ? flareActivationRadius : dimActivationRadius;
            float lanternPhysicalRadius = isFlare ? targetLantern.FlareRadius : targetLantern.DimRadius;
            maxReach = Mathf.Min(allowedRelayRadius, lanternPhysicalRadius);

            return distance <= maxReach;
        }

        public void SetActivated(bool active)
        {
            if (isActivated == active) return;

            isActivated = active;
            if (isActivated)
            {
                chargeLevel = 1f;
                lingerTimer = lingerDuration;
                OnActivated?.Invoke();
                OnStateChanged?.Invoke(true);
                if (enableAudio)
                {
                    SoundEffectManager.Instance?.PlayCrystalActivate(soundVolume);
                }
            }
            else
            {
                chargeLevel = 0f;
                lingerTimer = 0f;
                OnDeactivated?.Invoke();
                OnStateChanged?.Invoke(false);
            }
        }

        public void ForceSetCharge(float charge)
        {
            chargeLevel = Mathf.Clamp01(charge);
            OnChargeChanged?.Invoke(chargeLevel);
            if (chargeLevel >= 0.999f && !isActivated)
            {
                SetActivated(true);
            }
            else if (chargeLevel <= 0.001f && isActivated)
            {
                SetActivated(false);
            }
            UpdateVisuals(0f);
        }

        private void UpdateVisuals(float dt)
        {
            // Bobbing float animation
            if (crystalTransform != null)
            {
                float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
                crystalTransform.localPosition = baseCrystalLocalPos + new Vector3(0f, bob, 0f);
            }

            // Color & emission pulse
            Color targetGlowColor = (mode == RelayMode.Latched && isActivated) ? latchedColor : activeColor;
            Color curColor = Color.Lerp(dormantColor, targetGlowColor, chargeLevel);

            if (isActivated)
            {
                float pulse = 1.0f + 0.12f * Mathf.Sin(Time.time * 6f);
                curColor.a = Mathf.Clamp01(curColor.a * pulse);
            }

            if (crystalRenderer != null)
            {
                crystalRenderer.color = curColor;
            }

            // Dynamic Point Light2D
            if (crystalLight != null)
            {
                crystalLight.pointLightInnerRadius = 0.2f + 0.5f * chargeLevel;
                crystalLight.pointLightOuterRadius = 0.8f + 3.2f * chargeLevel;
                crystalLight.intensity = 0.1f + 1.15f * chargeLevel;
                crystalLight.color = curColor;
            }
        }

        public void EnsureHierarchy()
        {
            // Pedestal child
            var pedestal = transform.Find("Pedestal");
            if (pedestal == null)
            {
                var pedGo = new GameObject("Pedestal");
                pedGo.transform.SetParent(transform, false);
                pedGo.transform.localPosition = new Vector3(0f, 0f, 0f);
                pedestalRenderer = pedGo.AddComponent<SpriteRenderer>();
                pedestalRenderer.sortingOrder = 5;
                pedestal = pedGo.transform;
            }
            else if (pedestalRenderer == null)
            {
                pedestalRenderer = pedestal.GetComponent<SpriteRenderer>();
            }

            var pedSprite = Resources.Load<Sprite>("Art/Sprites/RelayPedestal");
            if (pedSprite == null) pedSprite = LoadSpriteDirect("Assets/_Project/Art/Sprites/RelayPedestal.png");
            if (pedestalRenderer != null && pedestalRenderer.sprite == null)
            {
                pedestalRenderer.sprite = pedSprite;
            }

            // Crystal child
            var crystal = transform.Find("Crystal");
            if (crystal == null)
            {
                var cryGo = new GameObject("Crystal");
                cryGo.transform.SetParent(transform, false);
                cryGo.transform.localPosition = baseCrystalLocalPos;
                crystalRenderer = cryGo.AddComponent<SpriteRenderer>();
                crystalRenderer.sortingOrder = 6;
                crystalTransform = cryGo.transform;
            }
            else
            {
                crystalTransform = crystal;
                if (crystalRenderer == null)
                {
                    crystalRenderer = crystal.GetComponent<SpriteRenderer>();
                }
            }

            var crySprite = Resources.Load<Sprite>("Art/Sprites/RelayCrystal");
            if (crySprite == null) crySprite = LoadSpriteDirect("Assets/_Project/Art/Sprites/RelayCrystal.png");
            if (crystalRenderer != null && crystalRenderer.sprite == null)
            {
                crystalRenderer.sprite = crySprite;
            }

            // Light2D child
            if (crystalTransform != null)
            {
                var lightTr = crystalTransform.Find("Relay_Light");
                if (lightTr == null)
                {
                    var lightGo = new GameObject("Relay_Light");
                    lightGo.transform.SetParent(crystalTransform, false);
                    lightGo.transform.localPosition = Vector3.zero;
                    crystalLight = lightGo.AddComponent<Light2D>();
                    crystalLight.lightType = Light2D.LightType.Point;
                    crystalLight.pointLightInnerRadius = 0.3f;
                    crystalLight.pointLightOuterRadius = 1.0f;
                    crystalLight.intensity = 0.15f;
                    crystalLight.color = dormantColor;
                }
                else if (crystalLight == null)
                {
                    crystalLight = lightTr.GetComponent<Light2D>();
                }
            }
        }

        private static Sprite LoadSpriteDirect(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }
    }
}
