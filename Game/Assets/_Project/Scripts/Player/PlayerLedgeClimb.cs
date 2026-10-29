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
        [SerializeField, Range(.1f, .7f)] private float stowDuration = .7f;
        private const float Skin = .035f;
        private const float LandingSkin = .012f;
        private const float ReachDuration = .12f;
        private const float RecoveryDuration = .16f;
        private const int ReleaseFrame = 15, StandingFrame = 20;

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
        private Quaternion supportRotation;
        private Vector2 start, landing, centreOffset, shape;
        private Vector2 lip;
        private Vector3 originalVisualPosition;
        private PlayerSpriteSequence climbSequence, idleSequence;
        private float originalGravity, elapsed, climbDuration, totalDuration, activeStow, entryTime, requestTime;
        private float recoveryTime;
        private Vector3 recoveryOffset;
        private bool originalTicks, climbingPose, ownsTraversal, retainedVisual, recoveringVisual;
        private bool stickUpHeld;
        private int padId = -1;

        public bool IsClimbing => ownsTraversal;
        public Vector2 LandingPosition => landing;
        public Vector2 GripPoint => lip + new Vector2(motor.FacingDirection * .02f, Skin);
        public float Progress => ownsTraversal ? Mathf.Clamp01(elapsed / totalDuration) : 0;
        public int EntryFrame { get; private set; }
        public bool IsReaching => ownsTraversal && elapsed >= activeStow && elapsed < activeStow + ReachDuration;
        private float SourceTime => Mathf.Min(climbSequence.Duration, entryTime + Mathf.Max(0, elapsed - activeStow - ReachDuration));

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
            RestoreVisual(); requestTime = 0;
            if (recovery == null) return;
            recovery.OnPlayerDied -= Died;
            recovery.OnPlayerRespawned -= Respawned;
        }
        private void Died(DeathReason reason) => EndTraversal(false);
        private void Respawned() { EndTraversal(true); RestoreVisual(); }

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
            if (!GameControls.GameplayAllowed) { requestTime = 0; return; }
            if (pressed) requestTime = .2f;
            if (requestTime > 0)
            {
                requestTime -= Time.deltaTime;
                bool jumpPressed = (key != null && key.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                if (jumpPressed || motor.HasBufferedJump) return;
                float direction = GameControls.MoveAxis(pad);
                if (key != null)
                {
                    float keyboardDirection = (key.dKey.isPressed || key.rightArrowKey.isPressed ? 1 : 0) -
                        (key.aKey.isPressed || key.leftArrowKey.isPressed ? 1 : 0);
                    if (keyboardDirection != 0) direction = keyboardDirection;
                }
                if (Mathf.Abs(direction) > .05f && Mathf.Sign(direction) != motor.FacingDirection) return;
                if (TryBeginClimb()) requestTime = 0;
            }
        }

        public bool TryBeginClimb()
        {
            if (!isActiveAndEnabled || ownsTraversal || recoveringVisual || retainedVisual || !GameControls.GameplayAllowed || !motor.enabled ||
                !body.simulated || body.bodyType != RigidbodyType2D.Dynamic || !poses.enabled || poses.Renderer == null ||
                (recovery != null && recovery.State != RespawnState.Alive) ||
                (lantern != null && lantern.HasActiveScout)) return false;
            var climb = poses.Catalog != null ? poses.Catalog.Find(LightAnimation.Climb) : null;
            var stow = poses.Catalog != null ? poses.Catalog.Find(LightAnimation.Sheathing) : null;
            var idle = poses.Catalog != null ? poses.Catalog.Find(LightAnimation.Idle) : null;
            if (!HasMarkers(climb) || idle == null || idle.SoleOffsets == null || idle.SoleOffsets.Length == 0 ||
                stow == null || climb.Duration <= 0 || stowDuration > stow.Duration || poses.Renderer.transform == transform) return false;
            if (!FindLedge(out support, out landing)) return false;
            start = body.position;
            var raised = new Vector2(start.x, landing.y);
            if (!ClearAt(start) || !ClearAt(landing) || !ClearPath(start, raised) || !ClearPath(raised, landing)) return false;

            supportBounds = support.bounds;
            supportRotation = support.transform.rotation;
            originalGravity = body.gravityScale;
            originalTicks = poses.AutomaticTicks;
            var visual = poses.Renderer.transform;
            originalVisualPosition = visual.localPosition;
            climbSequence = climb; idleSequence = idle;
            // Begin with a pose that can reach this lip without burying the feet in the floor.
            EntryFrame = 0;
            for (; EntryFrame < 10; EntryFrame++)
            {
                float span = visual.TransformVector(new Vector3(0, climb.GripOffsets[EntryFrame].y - climb.SoleOffsets[EntryFrame].y, 0)).y;
                if (span <= lip.y - capsule.bounds.min.y + Skin) break;
            }
            entryTime = FrameTime(EntryFrame);
            activeStow = SupportedAtStart() ? Mathf.Min(stowDuration, .7f) : 0;
            elapsed = 0; climbDuration = climb.Duration - entryTime; climbingPose = false;
            totalDuration = activeStow + ReachDuration + climbDuration;
            requestTime = 0; retainedVisual = true;
            ownsTraversal = true;
            motor.SetTraversalLocked(true);
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0;
            poses.AutomaticTicks = false;
            poses.PlayContextAnimation(activeStow > 0 ? LightAnimation.Sheathing : LightAnimation.Climb, true);
            climbingPose = activeStow <= 0;
            poses.SampleContextPose(activeStow > 0 ? 0 : entryTime);
            GameControls.BlockGameplay(this, true);
            LegacyInteractionPollingGate.Acquire(this);
            return true;
        }

        private static bool HasMarkers(PlayerSpriteSequence sequence) => sequence != null && sequence.Frames != null && sequence.Frames.Length > StandingFrame &&
            sequence.GripOffsets != null && sequence.GripOffsets.Length == sequence.Frames.Length &&
            sequence.SoleOffsets != null && sequence.SoleOffsets.Length == sequence.Frames.Length &&
            sequence.PlantOffsets != null && sequence.PlantOffsets.Length == sequence.Frames.Length;

        private bool SupportedAtStart()
        {
            if (body.linearVelocity.y > .05f) return false;
            int count = body.Cast(Vector2.down, filter, hits, .065f);
            for (int i = 0; i < count; i++) if (Solid(hits[i].collider) && hits[i].normal.y >= .65f) return true;
            return false;
        }

        private float FrameTime(int frame)
        {
            float result = 0;
            for (int i = 0; i < frame && i < climbSequence.Durations.Length; i++) result += climbSequence.Durations[i];
            return result;
        }

        private bool FindLedge(out Collider2D ledge, out Vector2 destination)
        {
            ledge = null; destination = default;
            Physics2D.SyncTransforms();
            var bounds = capsule.bounds;
            centreOffset = (Vector2)bounds.center - body.position;
            shape = bounds.size;
            if (Mathf.Abs(Mathf.DeltaAngle(0, transform.eulerAngles.z)) > .01f || capsule.direction != CapsuleDirection2D.Vertical) return false;
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
            destination = new Vector2(x, top.point.y + bounds.extents.y + LandingSkin) - centreOffset;
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
            lip = new Vector2(face.point.x, top.point.y);
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

        private bool SupportIsValid() => support != null && support.enabled && support.gameObject.activeInHierarchy && Stationary(support) &&
            (support.bounds.center - supportBounds.center).sqrMagnitude <= .0001f &&
            (support.bounds.size - supportBounds.size).sqrMagnitude <= .0001f && Quaternion.Angle(support.transform.rotation, supportRotation) < .1f;

        private void FixedUpdate()
        {
            if (!ownsTraversal) return;
            if (!motor.enabled || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic || !capsule.enabled ||
                !poses.enabled || poses.Renderer == null || !SupportIsValid()) { CancelClimb(); return; }
            if (!GameControls.GameplayAllowedExcept(this)) return;
            elapsed = Mathf.Min(totalDuration, elapsed + Time.fixedDeltaTime);
            if (elapsed < activeStow + ReachDuration)
            {
                if (!ClearAt(body.position) || (body.position - start).sqrMagnitude > .01f) CancelClimb();
                return;
            }
            float source = SourceTime;
            float lift = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(entryTime, FrameTime(ReleaseFrame), source));
            float crossing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FrameTime(ReleaseFrame), FrameTime(StandingFrame), source));
            var next = new Vector2(Mathf.Lerp(start.x, landing.x, crossing), Mathf.Lerp(start.y, landing.y, lift));
            if (!ClearAt(next) || !ClearPath(body.position, next)) { CancelClimb(); return; }
            body.MovePosition(next);
        }

        private void LateUpdate()
        {
            if (!ownsTraversal)
            {
                if (recoveringVisual && GameControls.GameplayAllowed && (recovery == null || recovery.State == RespawnState.Alive))
                {
                    recoveryTime += Time.deltaTime;
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(recoveryTime / RecoveryDuration));
                    if (poses != null && poses.Renderer != null) poses.Renderer.transform.localPosition = originalVisualPosition + recoveryOffset * (1 - t);
                    if (t >= 1) RestoreVisual();
                }
                return;
            }
            if (!SupportIsValid()) { CancelClimb(); return; }
            if (!GameControls.GameplayAllowedExcept(this)) return;
            if (elapsed < activeStow)
            {
                poses.SampleContextPose(elapsed);
                return;
            }
            if (!climbingPose)
            {
                climbingPose = true;
                poses.PlayContextAnimation(LightAnimation.Climb, true);
            }
            float source = SourceTime;
            int frame = climbSequence.FrameAt(source);
            var visual = poses.Renderer.transform;
            Vector3 origin;
            if (frame < ReleaseFrame)
            {
                origin = OriginFor(climbSequence.GripOffsets[frame], GripPoint);
                float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - activeStow) / ReachDuration));
                origin = Vector3.Lerp(BaseOrigin(), origin, reach);
            }
            else if (frame < StandingFrame)
            {
                float step = Mathf.InverseLerp(FrameTime(ReleaseFrame), FrameTime(StandingFrame - 1), source);
                var standingFoot = StandingFoot();
                var toeOffset = MarkerVector(climbSequence.PlantOffsets[StandingFrame] - climbSequence.SoleOffsets[StandingFrame]);
                var footTarget = new Vector2(Mathf.Lerp(lip.x + motor.FacingDirection * .08f, standingFoot.x + toeOffset.x, Mathf.SmoothStep(0, 1, step)),
                    standingFoot.y + .20f * (1 - Mathf.SmoothStep(0, 1, step)));
                var planted = OriginFor(climbSequence.PlantOffsets[frame], footTarget);
                float release = Mathf.SmoothStep(0, 1, Mathf.Clamp01((source - FrameTime(ReleaseFrame)) / .16f));
                origin = Vector3.Lerp(OriginFor(climbSequence.GripOffsets[ReleaseFrame - 1], GripPoint), planted, release);
                // A lifted foot stays outside the face until it has cleared the top surface.
                var toe = origin + MarkerVector(climbSequence.PlantOffsets[frame]);
                if (toe.y < lip.y + LandingSkin && (toe.x - lip.x) * motor.FacingDirection > -.02f)
                    origin.x += lip.x - motor.FacingDirection * .02f - toe.x;
                var trailingSole = origin + MarkerVector(climbSequence.SoleOffsets[frame]);
                if (trailingSole.y < lip.y + LandingSkin && (trailingSole.x - lip.x) * motor.FacingDirection > -.02f)
                    origin.x += lip.x - motor.FacingDirection * .02f - trailingSole.x;
            }
            else origin = OriginFor(climbSequence.SoleOffsets[frame], StandingFoot());
            visual.localPosition = visual.parent.InverseTransformPoint(origin);
            poses.SampleContextPose(source);
            if (elapsed >= totalDuration - .0001f) EndTraversal(true, true);
        }

        private Vector3 BaseOrigin() => poses.Renderer.transform.parent.TransformPoint(originalVisualPosition);
        private Vector3 MarkerVector(Vector2 marker) => poses.Renderer.transform.TransformVector(new Vector3(marker.x * motor.FacingDirection, marker.y, 0));
        private Vector3 OriginFor(Vector2 marker, Vector2 target)
        {
            var origin = (Vector3)target - MarkerVector(marker);
            origin.z = BaseOrigin().z;
            return origin;
        }
        private Vector2 IdleFootFromRoot()
        {
            var baseOffset = transform.TransformVector(transform.InverseTransformPoint(BaseOrigin()));
            return (Vector2)(baseOffset + MarkerVector(idleSequence.SoleOffsets[0]));
        }
        private Vector2 StandingFoot() => landing + IdleFootFromRoot();
        private Vector3 VisibleSole()
        {
            var sequence = poses.Catalog.Find(poses.CurrentAnimation);
            if (sequence != null && sequence.SoleOffsets != null && poses.CurrentFrame < sequence.SoleOffsets.Length)
                return poses.Renderer.transform.position + MarkerVector(sequence.SoleOffsets[poses.CurrentFrame]);
            return poses.Renderer.transform.position;
        }
        private void ReconcileCancelledBody(Vector3 visibleSole)
        {
            if (!body.simulated || body.bodyType != RigidbodyType2D.Dynamic) return;
            var candidate = (Vector2)visibleSole - IdleFootFromRoot();
            if (!ClearAt(candidate))
            {
                candidate.x = lip.x - motor.FacingDirection * (shape.x * .5f + Skin) - centreOffset.x;
                if (!ClearAt(candidate)) return;
            }
            var outer = new Vector2(candidate.x, Mathf.Max(candidate.y, body.position.y));
            if (!ClearPath(body.position, candidate) && !(ClearPath(body.position, outer) && ClearPath(outer, candidate))) return;
            // The visible sole already occupies this location. Reconcile only along a checked clear path.
            body.position = candidate;
            transform.position = new Vector3(candidate.x, candidate.y, transform.position.z);
            Physics2D.SyncTransforms();
        }

        public void CancelClimb() => EndTraversal(true);
        private void EndTraversal(bool clearPose, bool completed = false)
        {
            if (!ownsTraversal) return;
            bool alive = recovery == null || recovery.State == RespawnState.Alive;
            bool canRecover = clearPose && alive && poses != null && poses.Renderer != null;
            Vector3 visibleSole = canRecover ? VisibleSole() : Vector3.zero;
            if (canRecover && !completed) ReconcileCancelledBody(visibleSole);
            ownsTraversal = false;
            motor.SetTraversalLocked(false);
            if (Mathf.Approximately(body.gravityScale, 0)) body.gravityScale = originalGravity;
            if (body.bodyType == RigidbodyType2D.Dynamic) body.linearVelocity = Vector2.zero;
            poses.AutomaticTicks = originalTicks;
            GameControls.BlockGameplay(this, false);
            LegacyInteractionPollingGate.Release(this);
            support = null;
            if (!clearPose) return; // Keep the actual death pose until respawn, without snapping to the capsule.
            RestoreVisual();
            if (poses.isActiveAndEnabled) poses.ClearContextAnimation();
            if (canRecover && !completed && isActiveAndEnabled && poses.isActiveAndEnabled)
            {
                var delta = visibleSole - VisibleSole();
                recoveryOffset = poses.Renderer.transform.parent.InverseTransformVector(delta);
                recoveryOffset.z = 0;
                if (recoveryOffset.sqrMagnitude > .000001f)
                {
                    retainedVisual = recoveringVisual = true; recoveryTime = 0;
                    poses.Renderer.transform.localPosition = originalVisualPosition + recoveryOffset;
                }
            }
        }
        private void RestoreVisual()
        {
            if (!retainedVisual && !recoveringVisual) return;
            if (poses != null && poses.Renderer != null) poses.Renderer.transform.localPosition = originalVisualPosition;
            retainedVisual = recoveringVisual = false; recoveryOffset = Vector3.zero;
        }
    }
}
