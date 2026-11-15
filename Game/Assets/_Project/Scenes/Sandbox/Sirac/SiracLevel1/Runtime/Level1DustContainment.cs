using UnityEngine;
using TheLightenRoad.Lantern;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Keeps camera-following dust motes confined to SiracLevel1's carved air chambers and lit near light sources.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class Level1DustContainment : MonoBehaviour
    {
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Rect[] carvedAirRects = new Rect[0];
        [SerializeField] private float baseAlpha = 0.52f;
        [SerializeField] private float minAmbientAlphaFactor = 0.14f;

        private ParticleSystem particles;
        private ParticleSystem.Particle[] buffer;
        private Rigidbody2D playerBody;
        private LanternController lantern;
        private float nextPlayerResolveTime;

        private static readonly Vector2[] Directions =
        {
            Vector2.left,
            Vector2.right,
            Vector2.up,
            Vector2.down
        };

        public void Configure(Collider2D playerCol, Rect[] airRects)
        {
            playerCollider = playerCol;
            carvedAirRects = airRects ?? new Rect[0];
        }

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            ResolvePlayer();
        }

        private void Start()
        {
            if (particles == null)
                particles = GetComponent<ParticleSystem>();

            var psRenderer = GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.name.Contains("Sprite-Lit")))
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null)
                {
                    var mat = new Material(shader) { mainTexture = CreateSoftCircleTexture(32) };
                    mat.SetFloat("_Surface", 1f);
                    psRenderer.sharedMaterial = mat;
                }
            }

            if (particles != null && Application.isPlaying)
            {
                particles.Simulate(8f, true, true);
                particles.Play();
                UpdateParticles(0f);
            }
        }

        private static Texture2D CreateSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    byte a = (byte)(d < 1f ? Mathf.RoundToInt(255f * (1f - d) * (1f - d)) : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private void ResolvePlayer()
        {
            if (playerCollider == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerCollider = player.GetComponent<Collider2D>();
            }

            if (playerCollider != null)
            {
                playerBody = playerCollider.attachedRigidbody;
                lantern = playerCollider.GetComponentInParent<LanternController>();
            }
        }

        private bool TryGetAirMargin(Vector2 p, out float margin)
        {
            if (carvedAirRects == null || carvedAirRects.Length == 0)
            {
                margin = 10f;
                return true;
            }

            bool inside = false;
            float bestMargin = 0f;
            for (int i = 0; i < carvedAirRects.Length; i++)
            {
                var r = carvedAirRects[i];
                if (p.x > r.xMin + 0.08f && p.x < r.xMax - 0.08f &&
                    p.y > r.yMin + 0.08f && p.y < r.yMax - 0.08f)
                {
                    inside = true;
                    float m = Mathf.Min(Mathf.Min(p.x - r.xMin, r.xMax - p.x), Mathf.Min(p.y - r.yMin, r.yMax - p.y));
                    if (m > bestMargin) bestMargin = m;
                }
            }
            margin = bestMargin;
            return inside;
        }

        private bool InAir(Vector2 p) => TryGetAirMargin(p, out _);

        private void LateUpdate()
        {
            UpdateParticles(Time.deltaTime);
        }

        private void UpdateParticles(float dt)
        {
            if (particles == null || !particles.isPlaying)
                return;

            if ((playerCollider == null || lantern == null) && (!Application.isPlaying || Time.unscaledTime >= nextPlayerResolveTime))
            {
                ResolvePlayer();
                nextPlayerResolveTime = Time.unscaledTime + 0.5f;
            }

            int capacity = particles.main.maxParticles;
            if (buffer == null || buffer.Length < capacity)
                buffer = new ParticleSystem.Particle[capacity];

            int count = particles.GetParticles(buffer);
            bool playerActive = playerCollider != null && playerCollider.enabled &&
                playerCollider.gameObject.activeInHierarchy && playerBody != null && playerBody.simulated;
            Vector2 playerVelocity = playerActive ? playerBody.linearVelocity : Vector2.zero;
            Vector2 playerBoundsCenter = playerActive ? (Vector2)playerCollider.bounds.center : Vector2.zero;
            float movement = Mathf.Clamp01(playerVelocity.magnitude / 3.2f);

            Vector2 cameraCenter = transform.position;
            var light = lantern != null ? lantern.LanternLight : null;
            bool lightActive = light != null && light.isActiveAndEnabled && light.intensity > 0f;
            Vector2 lightPosition = lightActive ? (Vector2)light.transform.position : Vector2.zero;
            float lightRadius = lightActive ? light.pointLightOuterRadius : 0f;
            float lightRadiusSq = lightRadius * lightRadius;
            float lightStrength = lightActive ? Mathf.Clamp01(light.intensity / 0.55f) : 0f;

            for (int i = 0; i < count; i++)
            {
                var p = buffer[i];
                Vector2 position = p.position;

                if (!TryGetAirMargin(position, out float airMargin) || Mathf.Abs(position.x - cameraCenter.x) > 8.5f ||
                    Mathf.Abs(position.y - cameraCenter.y) > 5.0f)
                {
                    p.remainingLifetime = 0f;
                    buffer[i] = p;
                    continue;
                }

                if (playerActive && movement > 0.03f && dt > 0f && (position - playerBoundsCenter).sqrMagnitude < 2.5f)
                {
                    Vector2 nearest = playerCollider.ClosestPoint(position);
                    Vector2 away = position - nearest;
                    float distance = away.magnitude;
                    if (distance < 0.60f)
                    {
                        if (distance < 0.001f) away = position - playerBoundsCenter;
                        if (away.sqrMagnitude < 0.000001f) away = Vector2.up;
                        float influence = 1f - distance / 0.60f;
                        Vector2 current = p.velocity;
                        Vector2 target = Vector2.ClampMagnitude(
                            current + away.normalized * (0.45f * influence * movement) + playerVelocity * (0.08f * influence),
                            0.45f);
                        p.velocity = Vector2.Lerp(current, target, 1f - Mathf.Exp(-4f * dt));
                    }
                }

                float wallFade = 1f;
                if (airMargin <= 0.38f)
                {
                    for (int d = 0; d < Directions.Length; d++)
                    {
                        if (!InAir(position + Directions[d] * 0.14f)) wallFade = Mathf.Min(wallFade, 0.25f);
                        else if (!InAir(position + Directions[d] * 0.30f)) wallFade = Mathf.Min(wallFade, 0.65f);
                    }
                }

                float lightFactor = minAmbientAlphaFactor;
                if (lightRadius > 0.001f)
                {
                    float lightDistSq = (position - lightPosition).sqrMagnitude;
                    if (lightDistSq < lightRadiusSq)
                    {
                        float radial = Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Sqrt(lightDistSq) / lightRadius));
                        lightFactor = Mathf.Max(minAmbientAlphaFactor, radial * lightStrength);
                    }
                }

                var color = p.startColor;
                color.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(baseAlpha * wallFade * lightFactor));
                p.startColor = color;

                var point = p.position;
                point.z = 0f;
                p.position = point;
                buffer[i] = p;
            }

            particles.SetParticles(buffer, count);
        }
    }
}
