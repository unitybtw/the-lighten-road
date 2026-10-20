using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;

namespace TheLightenRoad.Effects
{
    /// <summary>
    /// Simulates a hanging chain, cable, or industrial cage lamp with damped harmonic oscillator pendulum physics.
    /// Responds with realistic angular swings when brushed by the player or buffeted by a lantern Flare shockwave,
    /// triggering authentic chain clink audio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwayingProp2D : MonoBehaviour
    {
        [Header("Pendulum Physics")]
        [Tooltip("Natural oscillation frequency in radians per second.")]
        [SerializeField, Range(0.5f, 10f)] private float naturalFrequency = 3.6f;

        [Tooltip("Damping coefficient (higher values cause oscillations to die down faster).")]
        [SerializeField, Range(0.1f, 5f)] private float dampingCoefficient = 1.2f;

        [Tooltip("Maximum allowed swing angle in degrees from vertical.")]
        [SerializeField, Range(10f, 85f)] private float maxAngle = 55f;

        [Header("Player Interaction")]
        [Tooltip("Multiplier for angular impulse imparted by player horizontal speed.")]
        [SerializeField, Range(1f, 30f)] private float playerPushMultiplier = 12.0f;

        [Tooltip("Minimum horizontal speed required to nudge the prop.")]
        [SerializeField] private float minPushSpeed = 0.4f;

        [Tooltip("Cooldown in seconds between successive player brush impulses.")]
        [SerializeField] private float playerPushCooldown = 0.22f;

        [Header("Lantern Flare Shockwave")]
        [Tooltip("Angular impulse imparted when a lantern flares nearby.")]
        [SerializeField, Range(5f, 80f)] private float flareShockwaveStrength = 42.0f;

        [Tooltip("Maximum distance from flare source to receive shockwave impulse.")]
        [SerializeField] private float flareShockwaveRadius = 11.0f;

        [Header("Audio")]
        [Tooltip("Minimum angular velocity required to trigger a chain clink sound.")]
        [SerializeField] private float soundMinVelocity = 14.0f;

        [Tooltip("Cooldown in seconds between chain clink sounds.")]
        [SerializeField] private float soundCooldown = 0.3f;

        [Header("Visual Construction")]
        [SerializeField] private bool autoBuildVisuals = true;
        [SerializeField] private Transform pivotTransform;
        [SerializeField] private int chainLinkCount = 4;
        [SerializeField] private float linkSpacing = 0.22f;
        [SerializeField] private Sprite chainLinkSprite;
        [SerializeField] private Sprite lampSprite;
        [SerializeField] private bool addLampLight = true;
        [SerializeField] private Color lampLightColor = new Color(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private float lampLightRadius = 3.8f;
        [SerializeField] private float lampLightIntensity = 0.8f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 3;

        private float currentAngle;
        private float angularVelocity;
        private float nextPushTime;
        private float nextSoundTime;
        private Light2D lampLight;
        private Collider2D triggerCollider;

        private static Sprite fallbackChainSprite;
        private static Sprite fallbackLampSprite;

        public float CurrentAngle => currentAngle;
        public float AngularVelocity => angularVelocity;
        public float NaturalFrequency { get => naturalFrequency; set => naturalFrequency = value; }
        public float DampingCoefficient { get => dampingCoefficient; set => dampingCoefficient = value; }
        public float MaxAngle { get => maxAngle; set => maxAngle = value; }
        public Transform PivotTransform => pivotTransform;
        public Light2D LampLight => lampLight;
        public Collider2D TriggerCollider => triggerCollider;

        private void Awake()
        {
            SetupPivot();
            if (autoBuildVisuals)
            {
                BuildVisualHierarchy();
            }
            EnsureTriggerCollider();
        }

        private void OnEnable()
        {
            LanternController.AnyFlareChanged += HandleAnyFlareChanged;
        }

        private void OnDisable()
        {
            LanternController.AnyFlareChanged -= HandleAnyFlareChanged;
        }

        private void SetupPivot()
        {
            if (pivotTransform != null) return;

            var existingPivot = transform.Find("Pivot");
            if (existingPivot != null)
            {
                pivotTransform = existingPivot;
            }
            else
            {
                var pivotGo = new GameObject("Pivot");
                pivotGo.transform.SetParent(transform, false);
                pivotTransform = pivotGo.transform;
            }
        }

        private void BuildVisualHierarchy()
        {
            if (pivotTransform == null || pivotTransform.childCount > 0) return;

            EnsureSpritesLoaded();

            // Construct chain links
            for (int i = 0; i < chainLinkCount; i++)
            {
                var linkGo = new GameObject($"Chain_Link_{i}");
                linkGo.transform.SetParent(pivotTransform, false);
                linkGo.transform.localPosition = new Vector3(0f, -i * linkSpacing, 0f);

                var sr = linkGo.AddComponent<SpriteRenderer>();
                sr.sprite = chainLinkSprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = orderInLayer;
            }

            // Construct bottom lamp prop
            float lampY = -(chainLinkCount * linkSpacing + 0.16f);
            var lampGo = new GameObject("HangingCageLamp");
            lampGo.transform.SetParent(pivotTransform, false);
            lampGo.transform.localPosition = new Vector3(0f, lampY, 0f);

            var lampSr = lampGo.AddComponent<SpriteRenderer>();
            lampSr.sprite = lampSprite;
            lampSr.sortingLayerName = sortingLayerName;
            lampSr.sortingOrder = orderInLayer + 1;

            if (addLampLight)
            {
                var lightGo = new GameObject("CageLamp_Light");
                lightGo.transform.SetParent(lampGo.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);

                lampLight = lightGo.AddComponent<Light2D>();
                lampLight.lightType = Light2D.LightType.Point;
                lampLight.pointLightInnerRadius = 0.2f;
                lampLight.pointLightOuterRadius = lampLightRadius;
                lampLight.color = lampLightColor;
                lampLight.intensity = lampLightIntensity;
                lampLight.shadowsEnabled = false;
            }
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (chainLinkSprite == null)
            {
                chainLinkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/ChainLink.png");
            }
            if (lampSprite == null)
            {
                lampSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/HangingCageLamp.png");
            }
#endif
            if (chainLinkSprite == null)
            {
                if (fallbackChainSprite == null) fallbackChainSprite = CreateFallbackChainSprite();
                chainLinkSprite = fallbackChainSprite;
            }

            if (lampSprite == null)
            {
                if (fallbackLampSprite == null) fallbackLampSprite = CreateFallbackLampSprite();
                lampSprite = fallbackLampSprite;
            }
        }

        private void EnsureTriggerCollider()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                float totalLength = (chainLinkCount * linkSpacing) + 0.5f;
                box.offset = new Vector2(0f, -totalLength * 0.5f);
                box.size = new Vector2(0.55f, totalLength);
                triggerCollider = box;
            }
        }

        private void Update()
        {
            TickPhysics(Time.deltaTime);
        }

        /// <summary>
        /// Steps the damped harmonic oscillator pendulum physics by dt seconds.
        /// Public for deterministic unit and PlayMode test simulation.
        /// </summary>
        public void TickPhysics(float dt)
        {
            if (dt <= 0f) return;

            // theta'' + 2*zeta*omega*theta' + omega^2*sin(theta) = 0
            float thetaRad = currentAngle * Mathf.Deg2Rad;
            float restoringTorque = -(naturalFrequency * naturalFrequency) * Mathf.Sin(thetaRad) * Mathf.Rad2Deg;
            float dampingTorque = -(2f * dampingCoefficient * naturalFrequency) * angularVelocity;

            float angularAccel = restoringTorque + dampingTorque;
            angularVelocity += angularAccel * dt;
            currentAngle += angularVelocity * dt;

            // Angle clamp with restitution bounce
            if (currentAngle > maxAngle)
            {
                currentAngle = maxAngle;
                angularVelocity = -angularVelocity * 0.25f;
            }
            else if (currentAngle < -maxAngle)
            {
                currentAngle = -maxAngle;
                angularVelocity = -angularVelocity * 0.25f;
            }

            // Apply rotation to pivot
            if (pivotTransform != null)
            {
                pivotTransform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }
        }

        /// <summary>
        /// Imparts an immediate angular impulse (in deg/s) to the pendulum.
        /// </summary>
        public void ApplyAngularImpulse(float impulse)
        {
            angularVelocity += impulse;

            if (Mathf.Abs(angularVelocity) >= soundMinVelocity && Time.time >= nextSoundTime)
            {
                nextSoundTime = Time.time + soundCooldown;
                float volumeScale = Mathf.Clamp01(Mathf.Abs(angularVelocity) / 50f);
                if (SoundEffectManager.HasInstance)
                {
                    SoundEffectManager.Instance.PlayChainClink(volumeScale);
                }
            }
        }

        /// <summary>
        /// Imparts a directional angular impulse caused by a radial shockwave (e.g. Flare expansion or explosion).
        /// </summary>
        public void ApplyRadialShockwave(Vector2 origin, float strength, float maxRadius = 11.0f)
        {
            Vector2 propCenter = transform.position;
            float dist = Vector2.Distance(origin, propCenter);
            if (dist > maxRadius) return;

            float diffX = propCenter.x - origin.x;
            float direction = Mathf.Abs(diffX) > 0.05f ? Mathf.Sign(diffX) : 1f;
            float falloff = 1f - Mathf.Clamp01(dist / maxRadius);

            float impulse = direction * strength * falloff;
            ApplyAngularImpulse(impulse);
        }

        private void HandleAnyFlareChanged(LanternController lantern, bool isFlared)
        {
            if (!isFlared || lantern == null) return;
            ApplyRadialShockwave(lantern.transform.position, flareShockwaveStrength, Mathf.Max(flareShockwaveRadius, lantern.FlareRadius));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandlePlayerBrush(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHandlePlayerBrush(other);
        }

        private void TryHandlePlayerBrush(Collider2D other)
        {
            if (Time.time < nextPushTime) return;

            var motor = other.GetComponentInParent<PlayerMotor2D>();
            var rb = other.attachedRigidbody;
            if (motor == null && rb == null) return;

            float speedX = motor != null ? motor.HorizontalSpeed : (rb != null ? rb.linearVelocity.x : 0f);
            if (Mathf.Abs(speedX) < minPushSpeed) return;

            nextPushTime = Time.time + playerPushCooldown;
            float impulse = speedX * playerPushMultiplier;
            ApplyAngularImpulse(impulse);
        }

        private static Sprite CreateFallbackChainSprite()
        {
            int w = 8, h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 ironDark = new Color32(45, 50, 58, 255);
            Color32 ironMid = new Color32(85, 95, 110, 255);
            Color32 ironLight = new Color32(140, 155, 175, 255);

            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            for (int y = 0; y < h; y++)
            {
                for (int x = 1; x < 7; x++)
                {
                    bool isBorder = (x == 1 || x == 6 || y == 0 || y == 1 || y == h - 2 || y == h - 1);
                    bool isHole = (x >= 3 && x <= 4 && y >= 3 && y <= h - 4);

                    if (isHole) continue;
                    if (isBorder) pixels[y * w + x] = ironDark;
                    else if (x == 2) pixels[y * w + x] = ironLight;
                    else pixels[y * w + x] = ironMid;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        private static Sprite CreateFallbackLampSprite()
        {
            int w = 16, h = 24;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 metal = new Color32(60, 55, 50, 255);
            Color32 metalRim = new Color32(90, 85, 75, 255);
            Color32 glassGlow = new Color32(255, 195, 80, 220);

            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            // Top cap
            for (int y = 20; y < 24; y++)
            {
                for (int x = 4; x < 12; x++)
                    pixels[y * w + x] = metal;
            }

            // Cage & Glass
            for (int y = 4; y < 20; y++)
            {
                for (int x = 2; x < 14; x++)
                {
                    if (x == 2 || x == 13 || x == 5 || x == 10 || y == 4 || y == 19)
                        pixels[y * w + x] = metalRim;
                    else
                        pixels[y * w + x] = glassGlow;
                }
            }

            // Bottom base
            for (int y = 0; y < 4; y++)
            {
                for (int x = 3; x < 13; x++)
                    pixels[y * w + x] = metal;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
