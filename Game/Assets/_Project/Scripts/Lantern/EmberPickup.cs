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
    /// - Embers remain in the world when the three-ember inventory is full.
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

        [Header("Magnetism & Spark Trail")]
        [SerializeField] private bool enableMagnetism = true;
        [SerializeField, Range(0.3f, 3f)] private float attractRadius = 0.65f;
        [SerializeField, Range(2f, 18f)] private float maxMagnetSpeed = 9.5f;
        [SerializeField, Range(5f, 35f)] private float magnetAcceleration = 24.0f;
        [SerializeField] private float arrivalDistance = 0.32f;
        [SerializeField] private Color trailStartColor = new Color(1f, 0.75f, 0.25f, 0.9f);
        [SerializeField] private Color trailEndColor = new Color(1f, 0.35f, 0.05f, 0f);

        private bool collected;
        private SpriteRenderer visualRenderer;
        private Light2D reactiveLight;
        private ParticleSystem reactiveParticles;
        private TrailRenderer sparkTrail;
        private Transform visualTransform;
        private Vector3 initialVisualLocalPosition;
        private Color baseVisualColor = new Color(1f, 0.48f, 0.12f, 1f);
        private float reactionProgress;
        private bool isIlluminated;
        private float animationSeed;
        private LanternController cachedLantern;
        private LanternFuel targetFuel;
        private bool isMagnetizing;
        private Vector3 magnetVelocity;

        public float FuelAmount => fuelAmount;
        public bool IsCollected => collected;
        public bool EnableDarknessDormancy { get => enableDarknessDormancy; set => enableDarknessDormancy = value; }
        public float ReactionProgress => reactionProgress;
        public bool IsIlluminated => isIlluminated;
        public bool IsFlaredUp => reactionProgress > 0.05f;
        public Light2D ReactiveLight => reactiveLight;
        public SpriteRenderer VisualRenderer => visualRenderer;
        public ParticleSystem ReactiveParticles => reactiveParticles;
        public TrailRenderer SparkTrail => sparkTrail;
        public bool EnableMagnetism { get => enableMagnetism; set => enableMagnetism = value; }
        public float AttractRadius { get => attractRadius; set => attractRadius = value; }
        public float MagnetRadius { get => attractRadius; set => attractRadius = value; }
        public bool IsMagnetizing => isMagnetizing;

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
            SetupSparkTrail();
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
                reactiveParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        private void SetupSparkTrail()
        {
            sparkTrail = GetComponentInChildren<TrailRenderer>();
            if (sparkTrail == null)
            {
                var trailGo = new GameObject("Spark Trail");
                trailGo.transform.SetParent(transform, false);
                sparkTrail = trailGo.AddComponent<TrailRenderer>();
            }

            sparkTrail.time = 0.28f;
            sparkTrail.startWidth = 0.15f;
            sparkTrail.endWidth = 0.01f;
            sparkTrail.emitting = false;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(trailStartColor, 0f), new GradientColorKey(trailEndColor, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sparkTrail.colorGradient = gradient;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                sparkTrail.material = new Material(shader);
            }
            sparkTrail.sortingOrder = 4;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (collected) return;

            if (isMagnetizing)
            {
                TickMagnetism(dt);
                return;
            }

            if (enableDarknessDormancy)
            {
                CheckLightStatus();
            }
            else
            {
                isIlluminated = true;
            }

            if (enableMagnetism && isIlluminated && cachedLantern != null)
            {
                CheckMagnetismTrigger();
            }

            float targetReaction = isIlluminated ? 1f : 0f;
            float speed = isIlluminated ? (1f / Mathf.Max(0.01f, flareUpDuration)) : (1f / Mathf.Max(0.01f, coolDownDuration));
            reactionProgress = Mathf.MoveTowards(reactionProgress, targetReaction, speed * dt);

            ApplyReaction(reactionProgress);
        }

        private void CheckMagnetismTrigger()
        {
            if (cachedLantern == null || collected || isMagnetizing) return;

            if (targetFuel == null)
            {
                targetFuel = cachedLantern.GetComponentInParent<LanternFuel>();
            }

            if (targetFuel == null || targetFuel.Capacity - targetFuel.CurrentFuel < fuelAmount)
                return;

            float effectiveRadius = cachedLantern.IsFlared ? (attractRadius * 2.2f) : attractRadius;
            Vector3 lanternPos = cachedLantern.LanternLight != null ? cachedLantern.LanternLight.transform.position : cachedLantern.transform.position;
            float dist = Vector2.Distance(transform.position, lanternPos);
            if (dist <= effectiveRadius)
            {
                StartMagnetism(targetFuel);
            }
        }

        public void StartMagnetism(LanternFuel fuel)
        {
            if (isMagnetizing || collected) return;

            targetFuel = fuel;
            isMagnetizing = true;
            isIlluminated = true;
            reactionProgress = 1f;

            if (sparkTrail != null)
            {
                sparkTrail.emitting = true;
            }

            // Initial gentle lift impulse for natural arc curve
            magnetVelocity = new Vector3(0f, 1.2f, 0f);
        }

        private void TickMagnetism(float dt)
        {
            isIlluminated = true;
            reactionProgress = 1f;
            if (cachedLantern == null || targetFuel == null)
            {
                cachedLantern = FindAnyObjectByType<LanternController>();
                if (cachedLantern != null && targetFuel == null)
                {
                    targetFuel = cachedLantern.GetComponentInParent<LanternFuel>();
                }
                if (cachedLantern == null || targetFuel == null) return;
            }

            Vector3 targetPos = cachedLantern.LanternLight != null ? cachedLantern.LanternLight.transform.position : cachedLantern.transform.position;
            Vector3 diff = targetPos - transform.position;
            float dist = diff.magnitude;

            if (dist <= arrivalDistance)
            {
                FinishCollection(targetFuel);
                return;
            }

            Vector3 targetDir = diff / Mathf.Max(0.001f, dist);
            magnetVelocity = Vector3.MoveTowards(magnetVelocity, targetDir * maxMagnetSpeed, magnetAcceleration * dt);
            transform.position += magnetVelocity * dt;

            // Visual spin juice while flying
            if (visualTransform != null)
            {
                visualTransform.Rotate(0f, 0f, 420f * dt);
            }

            ApplyReaction(1f);
        }

        private void CheckLightStatus()
        {
            if (cachedLantern == null || !cachedLantern.gameObject.activeInHierarchy)
            {
                cachedLantern = FindAnyObjectByType<LanternController>();
            }

            if (cachedLantern == null || cachedLantern.LanternLight == null || cachedLantern.LanternLight.intensity <= 0f)
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

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other.GetComponentInParent<LanternFuel>());

        // A full lantern may spend an ember while still overlapping the pickup.
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other.GetComponentInParent<LanternFuel>());

        public bool TryCollect(LanternFuel fuel)
        {
            if (collected || fuel == null || fuelAmount <= 0f || fuel.Capacity - fuel.CurrentFuel < fuelAmount)
                return false;

            return FinishCollection(fuel);
        }

        public bool CollectImmediate(LanternFuel fuel)
        {
            return FinishCollection(fuel);
        }

        private bool FinishCollection(LanternFuel fuel = null)
        {
            if (collected) return false;
            var f = fuel ?? targetFuel;
            if (f == null) return false;

            float accepted = f.AddFuel(fuelAmount);
            if (accepted <= 0f) return false;

            collected = true;
            isMagnetizing = false;
            EmberCollected?.Invoke();

            SpawnAbsorptionBurst(transform.position);

            Destroy(gameObject);
            return true;
        }

        private void SpawnAbsorptionBurst(Vector3 pos)
        {
            var burstGo = new GameObject("Ember_AbsorptionBurst");
            burstGo.transform.position = pos;
            var ps = burstGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var psr = burstGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null) psr.material = new Material(shader);
                psr.sortingOrder = 5;
            }

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new Color(1f, 0.78f, 0.22f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = false;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            if (Application.isPlaying)
            {
                ps.Play();
                Destroy(burstGo, 0.6f);
            }
            else
            {
                DestroyImmediate(burstGo);
            }
        }
    }
}
