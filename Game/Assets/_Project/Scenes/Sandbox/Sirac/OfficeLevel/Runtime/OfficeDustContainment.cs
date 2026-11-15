using UnityEngine;

namespace TheLightenRoad.Sirac.OfficeLevel
{
    /// <summary>Keeps camera-following dust in the office's connected air space.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class OfficeDustContainment : MonoBehaviour
    {
        private ParticleSystem particles;
        private ParticleSystem.Particle[] buffer;
        [SerializeField] private Collider2D playerCollider;
        private Rigidbody2D playerBody;
        private TheLightenRoad.Lantern.LanternController lantern;
        private const float BaseAlpha = .32f;
        private static readonly Vector2[] Directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            if (playerCollider != null)
            {
                playerBody = playerCollider.attachedRigidbody;
                lantern = playerCollider.GetComponentInParent<TheLightenRoad.Lantern.LanternController>();
            }
        }

        private static bool InAir(Vector2 p)
        {
            if (p.x < -10f || p.x > 34f || p.y > 4.9f) return false;
            float floor = p.x < 11f ? -3f : p.x < 20f ? 0f : p.x < 22f ? -4f : p.x < 29f ? -1f : 1f;
            return p.y > floor + .08f;
        }

        private void LateUpdate()
        {
            if (particles == null || !particles.isPlaying) return;
            int capacity = particles.main.maxParticles;
            if (buffer == null || buffer.Length < capacity) buffer = new ParticleSystem.Particle[capacity];
            int count = particles.GetParticles(buffer);
            bool playerActive = playerCollider != null && playerCollider.enabled &&
                playerCollider.gameObject.activeInHierarchy && playerBody != null && playerBody.simulated;
            Vector2 playerVelocity = playerActive ? playerBody.linearVelocity : Vector2.zero;
            float movement = Mathf.Clamp01(playerVelocity.magnitude / 3.2f);
            Vector2 cameraCenter = transform.position;
            var light = lantern != null ? lantern.LanternLight : null;
            bool lightActive = light != null && light.isActiveAndEnabled && light.intensity > 0f;
            Vector2 lightPosition = lightActive ? (Vector2)light.transform.position : Vector2.zero;
            float lightRadius = lightActive ? light.pointLightOuterRadius : 0f;
            float lightStrength = lightActive ? Mathf.Clamp01(light.intensity / .55f) : 0f;
            for (int i = 0; i < count; i++)
            {
                var p = buffer[i];
                Vector2 position = p.position;
                // Retire motes left behind by a camera jump, so their particle slots
                // can immediately populate the newly visible room.
                if (!InAir(position) || Mathf.Abs(position.x - cameraCenter.x) > 10f ||
                    Mathf.Abs(position.y - cameraCenter.y) > 5f)
                { p.remainingLifetime = 0f; buffer[i] = p; continue; }
                if (playerActive && movement > .03f)
                {
                    Vector2 nearest = playerCollider.ClosestPoint(position);
                    Vector2 away = position - nearest;
                    float distance = away.magnitude;
                    if (distance < .55f)
                    {
                        if (distance < .001f) away = position - (Vector2)playerCollider.bounds.center;
                        if (away.sqrMagnitude < .000001f) away = Vector2.up;
                        float influence = 1f - distance / .55f;
                        Vector2 current = p.velocity;
                        Vector2 target = Vector2.ClampMagnitude(current + away.normalized * (.45f * influence * movement) +
                            playerVelocity * (.08f * influence), .45f);
                        p.velocity = Vector2.Lerp(current, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
                    }
                }
                // Fade towards solid boundaries instead of drawing bright motes in the floor.
                float fade = 1f;
                foreach (var direction in Directions)
                {
                    if (!InAir(position + direction * .12f)) fade = Mathf.Min(fade, .2f);
                    else if (!InAir(position + direction * .25f)) fade = Mathf.Min(fade, .6f);
                }
                float lightFalloff = lightRadius > .001f
                    ? Mathf.Sqrt(Mathf.Clamp01(1f - Vector2.Distance(position, lightPosition) / lightRadius))
                    : 0f;
                var color = p.startColor;
                color.a = (byte)Mathf.RoundToInt(255f * BaseAlpha * fade * lightFalloff * lightStrength);
                p.startColor = color;
                var point = p.position; point.z = 0f; p.position = point;
                buffer[i] = p;
            }
            particles.SetParticles(buffer, count);
        }
    }
}
