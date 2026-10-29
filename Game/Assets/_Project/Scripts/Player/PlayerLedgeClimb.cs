using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    /// <summary>Deliberate, collision-checked ledge traversal using Maksim's stow and climb poses.</summary>
    [DefaultExecutionOrder(-50), DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor2D), typeof(PlayerSpriteAnimator))]
    public sealed class PlayerLedgeClimb : MonoBehaviour
    {
        [Header("Reach from the player's centre")]
        [SerializeField, Min(.05f)] private float forwardReach = .32f;
        [SerializeField, Min(.1f)] private float minimumLipHeight = .25f;
        [SerializeField, Min(.1f)] private float maximumLipHeight = 1.35f;
        [SerializeField, Min(.02f)] private float landingInset = .12f;
        [Header("Authored timing")]
        [Tooltip("The first seven Sheathing frames put the lantern away. Climb already draws it again at its end.")]
        [SerializeField, Range(.1f, 1.2f)] private float stowDuration = .7f;
        private const float Skin = .035f;

        private PlayerMotor2D motor;
        private PlayerSpriteAnimator poses;
        private PlayerRespawnController recovery;
        private LanternController lantern;
        private CapsuleCollider2D capsule;
        private Rigidbody2D body;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[24];
        private readonly Collider2D[] overlaps = new Collider2D[24];
        private ContactFilter2D filter;
        private Collider2D support;
        private Bounds supportBounds;
        private Vector2 start, landing, centreOffset, shape;
        private float lipX;
        private Vector3 originalVisualPosition;
        private float baseVisualOffsetX;
        private PlayerSpriteSequence climbSequence;
        private float originalGravity, elapsed, climbDuration;
        private bool originalTicks, climbingPose, ownsTraversal;
        private bool stickUpHeld;
        private int padId = -1;

        public bool IsClimbing => ownsTraversal;
        public Vector2 LandingPosition => landing;
        public float Progress => ownsTraversal ? Mathf.Clamp01(elapsed / (stowDuration + climbDuration)) : 0;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor2D>();
            poses = GetComponent<PlayerSpriteAnimator>();
            recovery = GetComponent<PlayerRespawnController>();
            lantern = GetComponent<LanternController>();
            capsule = GetComponent<CapsuleCollider2D>();
            body = GetComponent<Rigidbody2D>();
            filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(motor.GroundLayers);
        }
        private void OnEnable()
        {
            if (recovery == null) return;
            recovery.OnPlayerDied += Died;
            recovery.OnPlayerRespawned += Respawned;
        }
        private void OnDisable()
        {
            CancelClimb();
            if (recovery == null) return;
            recovery.OnPlayerDied -= Died;
            recovery.OnPlayerRespawned -= Respawned;
        }
        private void Died(DeathReason reason) => EndTraversal(false);
        private void Respawned() => EndTraversal(true);

        private void Update()
        {
            var key = Keyboard.current;
            var pad = GameControls.ActivePad;
            int currentPad = pad != null ? pad.deviceId : -1;
            float up = pad != null ? pad.leftStick.ReadUnprocessedValue().y : 0;
            if (currentPad != padId) { stickUpHeld = up > .35f; padId = currentPad; }
            bool stickPressed = !stickUpHeld && up > .65f;
            if (up < .35f) stickUpHeld = false;
            else if (up > .65f) stickUpHeld = true;
            if (ownsTraversal)
            {
                if (!GameControls.GameplayAllowedExcept(this)) return;
                bool cancel = (key != null && (key.sKey.wasPressedThisFrame || key.downArrowKey.wasPressedThisFrame || key.spaceKey.wasPressedThisFrame)) ||
                    (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame));
                if (cancel) CancelClimb();
                return;
            }
            bool pressed = (key != null && (key.wKey.wasPressedThisFrame || key.upArrowKey.wasPressedThisFrame)) ||
                (pad != null && pad.dpad.up.wasPressedThisFrame) || stickPressed;
            if (pressed) TryBeginClimb();
        }

        public bool TryBeginClimb()
        {
            if (!isActiveAndEnabled || ownsTraversal || !GameControls.GameplayAllowed || !motor.enabled ||
                !body.simulated || body.bodyType != RigidbodyType2D.Dynamic || !poses.enabled || poses.Renderer == null ||
                (recovery != null && recovery.State != RespawnState.Alive) ||
                (lantern != null && lantern.HasActiveScout)) return false;
            var climb = poses.Catalog != null ? poses.Catalog.Find(LightAnimation.Climb) : null;
            var stow = poses.Catalog != null ? poses.Catalog.Find(LightAnimation.Sheathing) : null;
            if (climb == null || stow == null || climb.Duration <= 0 || stowDuration > stow.Duration) return false;
            if (!FindLedge(out support, out landing)) return false;
            start = body.position;
            var raised = new Vector2(start.x, landing.y);
            if (!ClearAt(start) || !ClearAt(landing) || !ClearPath(start, raised) || !ClearPath(raised, landing)) return false;

            supportBounds = support.bounds;
            originalGravity = body.gravityScale;
            originalTicks = poses.AutomaticTicks;
            var visual = poses.Renderer.transform;
            originalVisualPosition = visual.localPosition;
            // Measure relative to the root; interpolation may leave the Transform behind a teleport.
            var relativeVisual = transform.InverseTransformPoint(visual.position);
            baseVisualOffsetX = transform.TransformVector(relativeVisual).x;
            climbSequence = climb;
            elapsed = 0; climbDuration = climb.Duration; climbingPose = false;
            ownsTraversal = true;
            motor.SetTraversalLocked(true);
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0;
            poses.AutomaticTicks = false;
            poses.PlayContextAnimation(LightAnimation.Sheathing, true);
            poses.Tick(.00001f);
            GameControls.BlockGameplay(this, true);
            LegacyInteractionPollingGate.Acquire(this);
            return true;
        }

        private bool FindLedge(out Collider2D ledge, out Vector2 destination)
        {
            ledge = null; destination = default;
            Physics2D.SyncTransforms();
            var bounds = capsule.bounds;
            centreOffset = (Vector2)bounds.center - body.position;
            shape = bounds.size;
            if (Mathf.Abs(transform.eulerAngles.z) > .01f || capsule.direction != CapsuleDirection2D.Vertical) return false;
            var direction = Vector2.right * motor.FacingDirection;
            int count = Physics2D.Raycast((Vector2)bounds.center + Vector2.up * .18f, direction, filter, hits, bounds.extents.x + forwardReach);
            if (count == hits.Length) return false;
            RaycastHit2D face = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!Solid(hit.collider) || Vector2.Dot(hit.normal, direction) > -.85f) continue;
                if (face.collider == null || hit.distance < face.distance) face = hit;
            }
            if (face.collider == null || !Stationary(face.collider) || face.collider.GetComponent<PlatformEffector2D>() != null) return false;
            float x = face.point.x + motor.FacingDirection * (bounds.extents.x + landingInset);
            var topOrigin = new Vector2(x, bounds.center.y + maximumLipHeight + Skin);
            count = Physics2D.Raycast(topOrigin, Vector2.down, filter, hits, maximumLipHeight - minimumLipHeight + Skin);
            if (count == hits.Length) return false;
            RaycastHit2D top = default;
            for (int i = 0; i < count; i++)
            {
                if (!Solid(hits[i].collider)) continue;
                if (top.collider == null || hits[i].distance < top.distance) top = hits[i];
            }
            if (top.collider != face.collider || top.normal.y < .95f) return false;
            destination = new Vector2(x, top.point.y + bounds.extents.y + Skin) - centreOffset;
            // Both sides of the footprint need a real platform, rather than a thin pillar.
            for (int side = -1; side <= 1; side += 2)
            {
                var sample = new Vector2(x + side * bounds.extents.x * .85f, top.point.y + Skin);
                count = Physics2D.Raycast(sample, Vector2.down, filter, hits, Skin * 3);
                if (count == hits.Length) return false;
                bool supported = false;
                for (int i = 0; i < count; i++)
                    if (hits[i].collider == top.collider && hits[i].normal.y >= .95f) supported = true;
                if (!supported) return false;
            }
            ledge = top.collider;
            lipX = face.point.x;
            return true;
        }

        private bool Solid(Collider2D other) => other != null && !other.isTrigger && other.attachedRigidbody != body &&
            !other.transform.IsChildOf(transform) && !Physics2D.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer) &&
            !Physics2D.GetIgnoreCollision(capsule, other);
        private static bool Stationary(Collider2D other) => other.attachedRigidbody == null || other.attachedRigidbody.bodyType == RigidbodyType2D.Static;

        private bool ClearAt(Vector2 position)
        {
            int count = Physics2D.OverlapCapsule(position + centreOffset, shape - Vector2.one * .012f,
                capsule.direction, 0, filter, overlaps);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (Solid(overlaps[i])) return false;
            return true;
        }
        private bool ClearPath(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            int count = Physics2D.CapsuleCast(from + centreOffset, shape - Vector2.one * .012f,
                capsule.direction, 0, delta.normalized, filter, hits, delta.magnitude);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (Solid(hits[i].collider) && Vector2.Dot(hits[i].normal, delta.normalized) < -.05f) return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (!ownsTraversal) return;
            if (!motor.enabled || !body.simulated || !poses.enabled || support == null || !support.enabled ||
                !support.gameObject.activeInHierarchy || !Stationary(support) ||
                (support.bounds.center - supportBounds.center).sqrMagnitude > .0001f ||
                (support.bounds.size - supportBounds.size).sqrMagnitude > .0001f)
            { CancelClimb(); return; }
            if (!GameControls.GameplayAllowedExcept(this)) return;
            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            if (elapsed < stowDuration)
            {
                if (!ClearAt(body.position) || (body.position - start).sqrMagnitude > .01f)
                { CancelClimb(); return; }
                poses.Tick(dt); return;
            }
            if (!climbingPose)
            {
                climbingPose = true;
                poses.PlayContextAnimation(LightAnimation.Climb, true);
            }
            float t = Mathf.Clamp01((elapsed - stowDuration) / climbDuration);
            // Let the hand reach the lip while the physical capsule stays safely beside the wall.
            // Return to the normal registration as the player crosses onto the top.
            float reach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .1f, t)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .84f, t)));
            if (poses.Renderer.transform != transform)
            {
                var visual = poses.Renderer.transform;
                int frame = climbSequence.FrameAt(Mathf.Max(0, elapsed - stowDuration));
                float gripX = climbSequence.ReachOffsetsX != null && frame < climbSequence.ReachOffsetsX.Length ? climbSequence.ReachOffsetsX[frame] : -10f / 36f;
                float handX = body.position.x + baseVisualOffsetX + visual.TransformVector(Vector3.right * (gripX * motor.FacingDirection)).x;
                var offset = visual.parent.InverseTransformVector(Vector3.right * (lipX - handX));
                visual.localPosition = originalVisualPosition + offset * reach;
            }
            poses.Tick(dt);
            var next = new Vector2(Mathf.Lerp(start.x, landing.x, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.58f, .84f, t))),
                Mathf.Lerp(start.y, landing.y, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .58f, t))));
            if (!ClearAt(next) || !ClearPath(body.position, next)) { CancelClimb(); return; }
            body.MovePosition(next);
            if (t >= 1) EndTraversal(true);
        }

        public void CancelClimb() => EndTraversal(true);
        private void EndTraversal(bool clearPose)
        {
            if (!ownsTraversal) return;
            ownsTraversal = false;
            motor.SetTraversalLocked(false);
            body.gravityScale = originalGravity;
            body.linearVelocity = Vector2.zero;
            poses.AutomaticTicks = originalTicks;
            if (poses.Renderer.transform != transform) poses.Renderer.transform.localPosition = originalVisualPosition;
            if (clearPose && poses != null) poses.ClearContextAnimation();
            GameControls.BlockGameplay(this, false);
            LegacyInteractionPollingGate.Release(this);
            support = null;
        }
    }
}
