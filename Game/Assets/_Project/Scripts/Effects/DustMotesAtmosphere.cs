using UnityEngine;

namespace TheLightenRoad.Effects
{
    [ExecuteAlways]
    public sealed class DustMotesAtmosphere : MonoBehaviour
    {
        [Header("Spawn Area & Density")]
        [SerializeField] private Vector2 areaSize = new Vector2(26f, 14f);
        [SerializeField, Range(10, 200)] private int maxParticles = 90;
        [SerializeField, Range(1f, 30f)] private float emissionRate = 8f;

        [Header("Mote Properties")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.04f, 0.12f);
        [SerializeField] private Vector2 speedRange = new Vector2(0.08f, 0.22f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(8f, 14f);
        [SerializeField] private Color moteColor = new Color(1f, 0.96f, 0.85f, 0.55f);

        [Header("Sorting")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 3;

        private ParticleSystem ps;
        private ParticleSystemRenderer psRenderer;

        public int MaxParticles => maxParticles;
        public float EmissionRate => emissionRate;
        public Vector2 AreaSize => areaSize;

        private void Awake()
        {
            SetupParticleSystem();
        }

        private void OnValidate()
        {
            if (ps != null)
                ConfigureModules();
        }

        private void SetupParticleSystem()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null)
                ps = gameObject.AddComponent<ParticleSystem>();

            psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer == null)
                psRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            if (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.name.Contains("Default-Particle"))
            {
                psRenderer.material = ParticleTextures.CreateMaterial(true, ParticleTextures.SoftCircle);
            }

            psRenderer.sortingLayerName = sortingLayerName;
            psRenderer.sortingOrder = orderInLayer;

            ConfigureModules();
        }

        private void ConfigureModules()
        {
            var main = ps.main;
            main.maxParticles = maxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = moteColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.loop = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, areaSize.y, 0f);

            // Subtle Perlin noise turbulence to make motes swirl organically
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            // Color over lifetime: smooth fade in and fade out
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            if (!ps.isPlaying && Application.isPlaying)
                ps.Play();
        }
    }
}
