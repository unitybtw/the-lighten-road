using UnityEngine;
using TheLightenRoad.Lantern;

namespace TheLightenRoad.Effects
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class LanternSparksFX : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LanternController lantern;
        [SerializeField] private LanternFuel fuel;

        [Header("Dim Mode Settings")]
        [SerializeField, Min(0f)] private float dimEmissionRate = 4f;
        [SerializeField] private Vector2 dimSpeedRange = new Vector2(0.6f, 1.2f);
        [SerializeField] private Vector2 dimLifetimeRange = new Vector2(0.6f, 1.0f);

        [Header("Flare Mode Settings")]
        [SerializeField, Min(0f)] private float flareEmissionRate = 22f;
        [SerializeField] private Vector2 flareSpeedRange = new Vector2(1.2f, 2.4f);
        [SerializeField] private Vector2 flareLifetimeRange = new Vector2(0.8f, 1.4f);

        [Header("Appearance")]
        [SerializeField] private Vector2 sparkSize = new Vector2(0.04f, 0.09f);
        [SerializeField] private Color sparkStartColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 8;

        private ParticleSystem ps;
        private ParticleSystemRenderer psRenderer;

        public bool IsEmitting => ps != null && ps.emission.rateOverTime.constant > 0f;
        public float CurrentEmissionRate => ps != null ? ps.emission.rateOverTime.constant : 0f;
        public LanternController Lantern => lantern;

        private void Awake()
        {
            if (lantern == null)
                lantern = GetComponentInParent<LanternController>();
            if (fuel == null)
                fuel = GetComponentInParent<LanternFuel>();

            SetupParticleSystem();
        }

        private void SetupParticleSystem()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null)
                ps = gameObject.AddComponent<ParticleSystem>();

            psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer == null)
                psRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            if (psRenderer != null && (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.name.Contains("Default-Particle")))
            {
                psRenderer.material = ParticleTextures.CreateMaterial(false, ParticleTextures.Spark);
                psRenderer.sortingLayerName = sortingLayerName;
                psRenderer.sortingOrder = orderInLayer;
            }

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(sparkSize.x, sparkSize.y);
            main.startColor = sparkStartColor;
            main.loop = true;
            main.playOnAwake = true;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.15f, 0.05f), 0.8f),
                    new GradientColorKey(new Color(0.2f, 0.05f, 0.05f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0.1f);
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            if (!ps.isPlaying && Application.isPlaying)
                ps.Play();
        }

        private void Update()
        {
            if (ps == null)
                return;

            bool hasFuel = fuel == null || fuel.CurrentFuel > 0f;
            bool isFlared = lantern != null && lantern.IsFlared;

            var emission = ps.emission;
            var main = ps.main;

            if (!hasFuel)
            {
                emission.rateOverTime = 0f;
                return;
            }

            if (isFlared)
            {
                emission.rateOverTime = flareEmissionRate;
                main.startSpeed = new ParticleSystem.MinMaxCurve(flareSpeedRange.x, flareSpeedRange.y);
                main.startLifetime = new ParticleSystem.MinMaxCurve(flareLifetimeRange.x, flareLifetimeRange.y);
            }
            else
            {
                emission.rateOverTime = dimEmissionRate;
                main.startSpeed = new ParticleSystem.MinMaxCurve(dimSpeedRange.x, dimSpeedRange.y);
                main.startLifetime = new ParticleSystem.MinMaxCurve(dimLifetimeRange.x, dimLifetimeRange.y);
            }
        }
    }
}
