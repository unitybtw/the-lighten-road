using UnityEngine;
using TheLightenRoad.Player;

namespace TheLightenRoad.Effects
{
    public sealed class PlayerFootstepDustFX : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor2D motor;

        [Header("Foot Offset")]
        [SerializeField] private Vector2 feetOffset = new Vector2(0f, -0.45f);

        [Header("Run Dust Trail")]
        [SerializeField] private bool enableRunDust = true;
        [SerializeField, Min(0.01f)] private float runDustInterval = 0.14f;
        [SerializeField, Range(1, 5)] private int runDustCount = 1;

        [Header("Jump & Land Bursts")]
        [SerializeField] private bool enableJumpDust = true;
        [SerializeField, Range(2, 10)] private int jumpDustCount = 4;
        [SerializeField] private bool enableLandDust = true;
        [SerializeField, Range(3, 16)] private int baseLandDustCount = 6;

        [Header("Appearance")]
        [SerializeField] private Color dustColor = new Color(0.85f, 0.82f, 0.78f, 0.5f);
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int orderInLayer = 2;

        private ParticleSystem runPs;
        private ParticleSystem burstPs;
        private float runTimer;

        public bool EnableRunDust { get => enableRunDust; set => enableRunDust = value; }
        public bool EnableJumpDust { get => enableJumpDust; set => enableJumpDust = value; }
        public bool EnableLandDust { get => enableLandDust; set => enableLandDust = value; }

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor2D>();

            SetupSystems();
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.OnJumped += HandleJumped;
                motor.OnLanded += HandleLanded;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.OnJumped -= HandleJumped;
                motor.OnLanded -= HandleLanded;
            }
        }

        private void SetupSystems()
        {
            var runGo = new GameObject("RunDust_FX");
            runGo.transform.SetParent(transform);
            runGo.transform.localPosition = feetOffset;

            runPs = CreateParticleSystem(runGo, false);

            var burstGo = new GameObject("BurstDust_FX");
            burstGo.transform.SetParent(transform);
            burstGo.transform.localPosition = feetOffset;

            burstPs = CreateParticleSystem(burstGo, true);
        }

        private ParticleSystem CreateParticleSystem(GameObject go, bool isBurst)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = ParticleTextures.CreateMaterial(true, ParticleTextures.DustPuff);
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = orderInLayer;

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = isBurst ? 0.45f : 0.35f;
            main.startSpeed = isBurst ? 1.2f : 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = dustColor;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0.7f);
            curve.AddKey(1f, 1.4f);
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            return ps;
        }

        private void Update()
        {
            if (!enableRunDust || motor == null || runPs == null)
                return;

            bool isMovingOnGround = motor.IsGrounded && Mathf.Abs(motor.HorizontalInput) > 0.1f;
            if (isMovingOnGround)
            {
                runTimer -= Time.deltaTime;
                if (runTimer <= 0f)
                {
                    runTimer = runDustInterval;
                    EmitRunDust();
                }
            }
            else
            {
                runTimer = 0.04f;
            }
        }

        private void EmitRunDust()
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = (Vector2)transform.position + feetOffset,
                velocity = new Vector2(-motor.FacingDirection * 0.4f, Random.Range(0.1f, 0.35f)),
                startColor = dustColor,
                startSize = Random.Range(0.07f, 0.13f),
                startLifetime = 0.32f
            };
            runPs.Emit(emitParams, runDustCount);
        }

        private void HandleJumped()
        {
            if (!enableJumpDust || burstPs == null)
                return;

            Vector2 pos = (Vector2)transform.position + feetOffset;
            for (int i = 0; i < jumpDustCount; i++)
            {
                float dir = (i % 2 == 0) ? 1f : -1f;
                float speed = Random.Range(0.6f, 1.3f) * dir;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = pos + new Vector2(Random.Range(-0.05f, 0.05f), 0f),
                    velocity = new Vector2(speed, Random.Range(0.1f, 0.4f)),
                    startColor = dustColor,
                    startSize = Random.Range(0.09f, 0.16f),
                    startLifetime = Random.Range(0.35f, 0.5f)
                };
                burstPs.Emit(emitParams, 1);
            }
        }

        private void HandleLanded(float impactSpeed)
        {
            if (!enableLandDust || burstPs == null)
                return;

            int count = Mathf.Clamp(Mathf.RoundToInt(baseLandDustCount * (1f + impactSpeed * 0.1f)), 4, 14);
            Vector2 pos = (Vector2)transform.position + feetOffset;

            for (int i = 0; i < count; i++)
            {
                float dir = (i % 2 == 0) ? 1f : -1f;
                float speed = Random.Range(0.8f, 2.0f) * dir * Mathf.Clamp(1f + impactSpeed * 0.12f, 1f, 2.5f);
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = pos + new Vector2(Random.Range(-0.08f, 0.08f), 0f),
                    velocity = new Vector2(speed, Random.Range(0.15f, 0.55f)),
                    startColor = dustColor,
                    startSize = Random.Range(0.1f, 0.2f),
                    startLifetime = Random.Range(0.4f, 0.65f)
                };
                burstPs.Emit(emitParams, 1);
            }
        }
    }
}
