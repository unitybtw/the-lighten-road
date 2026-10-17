using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Collectible ember crystal that rests dormant and completely invisible in pure darkness.
    /// In accordance with Game Designer Furkan's feedback:
    /// - In pure darkness, embers are dormant fuel and completely invisible.
    /// - When illuminated by the player's lantern (whether in Dim or Flare Scout vision),
    ///   an unnatural chemical reaction causes the crystal to flare up, emit warm light,
    ///   and become clearly visible to guide the player.
    /// - When the player leaves or Flare dims out, it cools back down into pure darkness.
    /// - Embers are always collected on contact and added to player inventory.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class EmberPickup : MonoBehaviour
    {
        [Header("Fuel Rewards")]
        [Tooltip("Amount of fuel/charges restored (default 2 = 1 full ember).")]
        [SerializeField, Min(0f)] private float fuelAmount = 2f;

        [Header("Darkness & Chemical Flare Reaction")]
        [Tooltip("When enabled, the ember remains completely invisible in pure darkness until illuminated.")]
        [SerializeField] private bool enableDarknessDormancy = true;
        [Tooltip("Time in seconds to flare up upon entering the player's lantern light.")]
        [SerializeField, Min(0.01f)] private float flareUpDuration = 0.22f;
        [Tooltip("Time in seconds to cool down and return to pure darkness upon leaving lantern light.")]
        [SerializeField, Min(0.01f)] private float coolDownDuration = 0.40f;

        [Header("Reactive Illumination")]
        [SerializeField, Range(0.1f, 2f)] private float reactiveLightIntensity = 0.75f;
        [SerializeField, Range(0.5f, 3f)] private float reactiveLightRadius = 1.35f;
        [SerializeField] private Color reactiveLightColor = new Color(1f, 0.62f, 0.22f);

        [Header("Juice & Animation")]
        [SerializeField] private bool enableFloatingBob = true;
        [SerializeField, Range(0.01f, 0.2f)] private float bobAmplitude = 0.05f;
        [SerializeField, Range(0.5f, 5f)] private float bobFrequency = 2.4f;

        private bool collected;
        private SpriteRenderer visualRenderer;
        private Light2D reactiveLight;
        private ParticleSystem reactiveParticles;
        private Transform visualTransform;
        private Vector3 initialVisualLocalPosition;
        private Color baseVisualColor = new Color(1f, 0.48f, 0.12f, 1f);
        private float reactionProgress;
        private bool isIlluminated;
        private float animationSeed;
        private LanternController cachedLantern;

        public float FuelAmount => fuelAmount;
        public bool IsCollected => collected;
        public bool EnableDarknessDormancy { get => enableDarknessDormancy; set => enableDarknessDormancy = value; }
        public float ReactionProgress => reactionProgress;
        public bool IsIlluminated => isIlluminated;
        public bool IsFlaredUp => reactionProgress > 0.05f;
        public Light2D ReactiveLight => reactiveLight;
        public SpriteRenderer VisualRenderer => visualRenderer;
        public ParticleSystem ReactiveParticles => reactiveParticles;

        public void SetFuelAmount(float amount)
        {
            fuelAmount = Mathf.Max(0f, amount);
        }

        public void SetTargetLantern(LanternController lantern)
        {
            cachedLantern = lantern;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            animationSeed = Random.value * 100f;
            SetupVisuals();
            SetupReactiveLight();
            SetupReactiveParticles();
            ApplyReaction(0f);
        }

        private void SetupVisuals()
        {
            visualRenderer = GetComponentInChildren<SpriteRenderer>();
            if (visualRenderer != null)
            {
                visualTransform = visualRenderer.transform;
                initialVisualLocalPosition = visualTransform.localPosition;
                baseVisualColor = visualRenderer.color;
                if (baseVisualColor.a <= 0.01f)
                {
                    baseVisualColor = new Color(baseVisualColor.r, baseVisualColor.g, baseVisualColor.b, 1f);
                }

                // Ensure sprite uses lit material so it obeys 2D lighting
                if (visualRenderer.sharedMaterial == null || visualRenderer.sharedMaterial.name.Contains("Unlit"))
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                    if (shader != null)
                    {
                        visualRenderer.material = new Material(shader);
                    }
                }
            }
        }

        private void SetupReactiveLight()
        {
            reactiveLight = GetComponentInChildren<Light2D>();
            if (reactiveLight == null)
            {
                var lightGo = new GameObject("Reactive Light");
                lightGo.transform.SetParent(transform, false);
                reactiveLight = lightGo.AddComponent<Light2D>();
            }

            reactiveLight.lightType = Light2D.LightType.Point;
            reactiveLight.pointLightInnerRadius = 0.15f;
            reactiveLight.pointLightOuterRadius = reactiveLightRadius;
            reactiveLight.color = reactiveLightColor;
            reactiveLight.intensity = 0f;
            reactiveLight.shadowsEnabled = false;
        }

        private void SetupReactiveParticles()
        {
            reactiveParticles = GetComponentInChildren<ParticleSystem>();
            if (reactiveParticles == null)
            {
                var psGo = new GameObject("Reaction Sparks");
                psGo.transform.SetParent(transform, false);
                reactiveParticles = psGo.AddComponent<ParticleSystem>();
                var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
                if (psRenderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        psRenderer.material = mat;
                    }
                    psRenderer.sortingOrder = 3;
                }

                var main = reactiveParticles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startColor = new Color(1f, 0.75f, 0.25f, 0.9f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = reactiveParticles.emission;
                emission.rateOverTime = 6f;

                var shape = reactiveParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.15f;

                var velocityOverLifetime = reactiveParticles.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
                velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
                velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (collected) return;

            if (enableDarknessDormancy)
            {
                CheckLightStatus();
            }
            else
            {
                isIlluminated = true;
            }

            float targetReaction = isIlluminated ? 1f : 0f;
            float speed = isIlluminated ? (1f / Mathf.Max(0.01f, flareUpDuration)) : (1f / Mathf.Max(0.01f, coolDownDuration));
            reactionProgress = Mathf.MoveTowards(reactionProgress, targetReaction, speed * dt);

            ApplyReaction(reactionProgress);
        }

        private void CheckLightStatus()
        {
            if (cachedLantern == null || !cachedLantern.gameObject.activeInHierarchy)
            {
                cachedLantern = FindAnyObjectByType<LanternController>();
            }

            if (cachedLantern == null || cachedLantern.LanternLight == null)
            {
                isIlluminated = false;
                return;
            }

            Vector3 lightPos = cachedLantern.LanternLight.transform.position;
            float lightRadius = cachedLantern.LanternLight.pointLightOuterRadius;
            float distance = Vector2.Distance(transform.position, lightPos);

            isIlluminated = distance <= lightRadius;
        }

        private void ApplyReaction(float t)
        {
            if (visualRenderer != null)
            {
                // In pure darkness (t=0): completely invisible
                // When flaring up: alpha smoothly increases to 1.0, and glows
                float alpha = Mathf.Clamp01(t);
                visualRenderer.color = new Color(baseVisualColor.r, baseVisualColor.g, baseVisualColor.b, alpha);

                if (visualTransform != null)
                {
                    float ignitionScale = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                    visualTransform.localScale = new Vector3(0.36f * ignitionScale, 0.36f * ignitionScale, 1f);

                    if (enableFloatingBob && t > 0.05f)
                    {
                        float bob = Mathf.Sin(Time.time * bobFrequency + animationSeed) * bobAmplitude * t;
                        visualTransform.localPosition = initialVisualLocalPosition + new Vector3(0f, bob, 0f);
                    }
                    else
                    {
                        visualTransform.localPosition = initialVisualLocalPosition;
                    }
                }
            }

            if (reactiveLight != null)
            {
                if (t <= 0.001f)
                {
                    reactiveLight.intensity = 0f;
                    reactiveLight.enabled = false;
                }
                else
                {
                    reactiveLight.enabled = true;
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 5f + animationSeed);
                    reactiveLight.intensity = reactiveLightIntensity * t * pulse;
                }
            }

            if (reactiveParticles != null)
            {
                if (t > 0.25f && !reactiveParticles.isPlaying && Application.isPlaying)
                {
                    reactiveParticles.Play();
                }
                else if (t <= 0.05f && reactiveParticles.isPlaying)
                {
                    reactiveParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public static event System.Action EmberCollected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected)
                return;

            var fuel = other.GetComponentInParent<LanternFuel>();
            if (fuel == null)
                return;

            fuel.AddFuel(fuelAmount);

            collected = true;
            EmberCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
