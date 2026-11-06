using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    /// <summary>Frame-timed artist sprites driven by actual movement and lantern events; no root motion.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationCatalog catalog;
        [SerializeField] private SpriteRenderer target;
        [Tooltip("Visual-only grace for momentary ground-contact loss at seams; never grants physical support.")]
        [SerializeField, Range(0f, .2f)] private float groundAnimationGrace = .12f;
        [Tooltip("Minimum real drop from the last supported position before a walk-off shows an air pose.")]
        [SerializeField, Range(.02f, .3f)] private float fallPoseDrop = .16f;
        private PlayerMotor2D motor;
        private PlayerRespawnController recovery;
        private LanternController lantern;
        private LanternFuel fuel;
        private PlayerSpriteSequence sequence;
        private float elapsed;
        private bool oneShot, explicitContext, holdContext;
        private bool originalSquash, originalTilt;
        private Sprite originalSprite;
        private Vector3 originalScale, originalLightPosition;
        private Quaternion originalRotation;
        private int transferFrame = -1;
        private float unsupportedTime;
        private float supportedHeight;
        private bool hadSupport, jumpLaunched, airPoseShown;
        private Object pushOwner;
        private PlayerActionWalkLayer actionWalkLayer;
        private float walkElapsed;
        private bool walkingAction;
        private bool hasLightAnchor;
        private int lightFacing;
        private LightAnimation lightAnimation;
        public bool HasWalkingActionLayer => walkingAction && actionWalkLayer != null && actionWalkLayer.Legs.gameObject.activeSelf;
        public SpriteRenderer WalkingLegsRenderer => actionWalkLayer != null ? actionWalkLayer.Legs : null;
        public int WalkingLegFrame { get; private set; } = -1;
        public bool AutomaticTicks { get; set; } = true;
        public PlayerAnimationCatalog Catalog => catalog;
        public LightAnimation CurrentAnimation => sequence != null ? sequence.Kind : LightAnimation.Idle;
        public int CurrentFrame { get; private set; }
        public SpriteRenderer Renderer => target;
        /// <summary>Contact owners report effort; they retain responsibility for collision and movement.</summary>
        public void SetPushing(Object owner, bool pushing)
        {
            if (owner == null) return;
            if (pushing) pushOwner = owner;
            else if (pushOwner == owner) pushOwner = null;
        }

        private void Awake()
        {
            motor = GetComponent<PlayerMotor2D>();
            recovery = GetComponent<PlayerRespawnController>();
            lantern = GetComponent<LanternController>();
            fuel = GetComponent<LanternFuel>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (catalog == null) catalog = Resources.Load<PlayerAnimationCatalog>(PlayerAnimationCatalog.ResourcePath);
        }
        private void OnEnable()
        {
            if (catalog == null || target == null) return;
            originalSprite = target.sprite;
            originalScale = target.transform.localScale;
            originalRotation = target.transform.localRotation;
            originalSquash = motor.EnableSquashAndStretch;
            originalTilt = motor.EnableKineticTilt;
            motor.EnableSquashAndStretch = motor.EnableKineticTilt = false;
            if (motor.LanternTransform != null) originalLightPosition = motor.LanternTransform.localPosition;
            motor.OnJumped += Jumped;
            motor.OnLanded += Landed;
            if (lantern != null) lantern.FlareChanged += FlareChanged;
            if (fuel != null) fuel.FuelChanged += FuelChanged;
            if (recovery != null) recovery.OnPlayerRespawned += Respawned;
            walkElapsed = 0;
            hasLightAnchor = false;
            ResetAirState();
            Select(LightAnimation.Idle, false, true);
            RenderFrame();
        }
        private void OnDisable()
        {
            pushOwner = null;
            ClearWalkingAction();
            if (motor == null || catalog == null || target == null) return;
            motor.OnJumped -= Jumped;
            motor.OnLanded -= Landed;
            if (lantern != null) lantern.FlareChanged -= FlareChanged;
            if (fuel != null) fuel.FuelChanged -= FuelChanged;
            if (recovery != null) recovery.OnPlayerRespawned -= Respawned;
            motor.EnableSquashAndStretch = originalSquash;
            motor.EnableKineticTilt = originalTilt;
            target.sprite = originalSprite;
            target.transform.localScale = originalScale;
            target.transform.localRotation = originalRotation;
            if (motor.LanternTransform != null) motor.LanternTransform.localPosition = originalLightPosition;
        }
        private void Jumped()
        {
            pushOwner = null;
            explicitContext = holdContext = false;
            jumpLaunched = airPoseShown = true;
            Select(LightAnimation.Jump, true, true);
        }
        private void Landed(float impact)
        {
            // A small seam can produce a real contact event without ever showing an air pose.
            if (!explicitContext && airPoseShown) Select(LightAnimation.Landing, true, true);
            ResetAirState();
        }
        private void FlareChanged(bool active)
        {
            // Airborne legs retain the jump/fall pose; the light/fuel event still happens normally.
            if (active && IsAlive && !explicitContext && motor.IsGrounded) Select(LightAnimation.Flare, true, true);
        }
        private bool IsAlive => recovery == null || recovery.State == RespawnState.Alive;
        private void FuelChanged(float current, float capacity)
        {
            if (!IsAlive || !GameControls.GameplayAllowed || transferFrame == Time.frameCount) return;
            var keyboard = Keyboard.current; var pad = GameControls.ActivePad;
            bool interact = (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                (pad != null && (pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame));
            if (interact) PlayEmberTransfer();
        }
        public void PlayEmberTransfer()
        {
            if (!IsAlive || explicitContext || CurrentAnimation == LightAnimation.Flare && oneShot) return;
            transferFrame = Time.frameCount;
            Select(LightAnimation.EmberSwap, true, true);
        }
        private void Respawned()
        {
            pushOwner = null;
            explicitContext = holdContext = false; transferFrame = -1;
            walkElapsed = 0;
            hasLightAnchor = false;
            ResetAirState();
            Select(LightAnimation.Idle, false, true); RenderFrame();
        }
        /// <summary>Visual hook for future climbing/stowing owners. It does not move or lock the player.</summary>
        public bool PlayContextAnimation(LightAnimation kind, bool holdLastFrame = false)
        {
            if (!IsAlive || catalog == null || catalog.Find(kind) == null) return false;
            explicitContext = true; holdContext = holdLastFrame;
            ResetAirState();
            Select(kind, true, true); return true;
        }
        public void ClearContextAnimation()
        { explicitContext = holdContext = false; oneShot = false; ResetAirState(); Select(LightAnimation.Idle, false, true); RenderFrame(); }

        /// <summary>Sample an owned context at an exact source time without advancing a second clock.</summary>
        public bool SampleContextPose(float sourceTime)
        {
            if (!explicitContext || sequence == null || float.IsNaN(sourceTime) || float.IsInfinity(sourceTime)) return false;
            elapsed = Mathf.Clamp(sourceTime, 0, sequence.Duration);
            RenderFrame();
            return true;
        }

        private void ResetAirState()
        {
            unsupportedTime = 0; jumpLaunched = airPoseShown = false;
            hadSupport = motor != null && motor.IsGrounded;
            supportedHeight = motor != null && motor.Body != null ? motor.Body.position.y : transform.position.y;
        }
        private void StartFallPose()
        {
            Select(LightAnimation.Jump, true, true);
            // A walk-off has no take-off anticipation. Hold the delivered airborne pose directly.
            elapsed = sequence != null ? sequence.Duration : 0;
        }

        private void LateUpdate() { if (AutomaticTicks && Time.timeScale > 0f) Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            if (sequence == null || target == null || !IsAlive || deltaTime <= 0) return;
            walkingAction = false;
            if (explicitContext)
            {
                elapsed += deltaTime;
                if (elapsed >= sequence.Duration && !holdContext)
                { explicitContext = false; oneShot = false; }
            }
            else
            {
                bool unsupported = motor.enabled && !motor.IsGrounded;
                unsupportedTime = unsupported ? unsupportedTime + deltaTime : 0;
                bool dropped = !hadSupport || (motor.Body.linearVelocity.y < -.5f && motor.Body.position.y < supportedHeight - fallPoseDrop);
                bool airborne = unsupported && (jumpLaunched || airPoseShown || (unsupportedTime >= groundAnimationGrace && dropped));
                bool visuallySupported = motor.IsGrounded || (unsupported && !airborne && hadSupport);
                bool moving = visuallySupported && Mathf.Abs(motor.HorizontalInput) > .05f && Mathf.Abs(motor.HorizontalSpeed) > .08f;
                bool pushing = pushOwner != null && visuallySupported && Mathf.Abs(motor.HorizontalInput) > .05f;
                if (airborne) { airPoseShown = true; hadSupport = false; }
                else if (motor.IsGrounded) { hadSupport = true; supportedHeight = motor.Body.position.y; jumpLaunched = airPoseShown = false; }
                if (airborne && CurrentAnimation != LightAnimation.Jump)
                    StartFallPose();
                if (moving && oneShot && (CurrentAnimation == LightAnimation.Landing || CurrentAnimation == LightAnimation.EmberSwap)) oneShot = false;
                if (!oneShot)
                {
                    if (airborne) StartFallPose();
                    else Select(pushing ? LightAnimation.Push : moving ? LightAnimation.Walk : LightAnimation.Idle, false, false);
                }
                // At normal walk speed, retain the artist's 100ms frames. Slow
                // analog movement scales proportionally without a minimum cadence.
                float gaitRate = Mathf.Abs(motor.HorizontalSpeed) / Mathf.Max(.1f, motor.WalkSpeed);
                float speed = CurrentAnimation == LightAnimation.Push ? Mathf.Clamp(Mathf.Abs(motor.HorizontalSpeed) / 1.2f, .65f, 1.1f)
                    : CurrentAnimation == LightAnimation.Walk ? gaitRate : 1;
                elapsed += deltaTime * speed;
                if (moving)
                {
                    if (CurrentAnimation == LightAnimation.Walk) walkElapsed = elapsed;
                    else walkElapsed += deltaTime * gaitRate;
                }
                else if (CurrentAnimation == LightAnimation.Idle) walkElapsed = 0;
                walkingAction = moving && oneShot && CurrentAnimation == LightAnimation.Flare;
                if (oneShot && elapsed >= sequence.Duration && (!airborne || CurrentAnimation != LightAnimation.Jump)) oneShot = false;
            }
            RenderFrame(deltaTime);
        }
        private void Select(LightAnimation kind, bool once, bool restart)
        {
            if (catalog == null || (!restart && sequence != null && sequence.Kind == kind)) return;
            var next = catalog.Find(kind);
            if (next == null || next.Frames == null || next.Frames.Length == 0) return;
            ClearWalkingAction();
            sequence = next; elapsed = !restart && kind == LightAnimation.Walk ? walkElapsed : 0;
            oneShot = once; CurrentFrame = 0;
        }
        private void ClearWalkingAction()
        {
            walkingAction = false; WalkingLegFrame = -1;
            if (actionWalkLayer != null) actionWalkLayer.Clear();
        }
        private void OnDestroy() { if (actionWalkLayer != null) actionWalkLayer.Dispose(); }
        private void RenderFrame(float deltaTime = 0f)
        {
            if (sequence == null || target == null) return;
            CurrentFrame = sequence.FrameAt(elapsed);
            if (CurrentFrame < 0) return;
            target.sprite = sequence.Frames[CurrentFrame];
            target.flipX = motor.FacingDirection < 0;
            if (walkingAction && !explicitContext && CurrentAnimation == LightAnimation.Flare)
            {
                var walk = catalog.Find(LightAnimation.Walk);
                if (walk != null && walk.Frames != null && walk.Frames.Length > 0)
                {
                    if (actionWalkLayer == null) actionWalkLayer = new PlayerActionWalkLayer(target);
                    WalkingLegFrame = walk.FrameAt(walkElapsed);
                    float upperWaist = sequence.WaistOffsetsX != null && CurrentFrame < sequence.WaistOffsetsX.Length ? sequence.WaistOffsetsX[CurrentFrame] : 0;
                    float lowerWaist = walk.WaistOffsetsX != null && WalkingLegFrame < walk.WaistOffsetsX.Length ? walk.WaistOffsetsX[WalkingLegFrame] : 0;
                    actionWalkLayer.Show(sequence.Frames[CurrentFrame], walk.Frames[WalkingLegFrame], upperWaist - lowerWaist);
                }
            }
            else ClearWalkingAction();
            if (motor.LanternTransform != null && sequence.LanternOffsets != null && CurrentFrame < sequence.LanternOffsets.Length)
            {
                var offset = sequence.LanternOffsets[CurrentFrame];
                // Convert through the sprite's transform so prefab visual offsets/scales remain valid.
                var local = new Vector3(offset.x * (target.flipX ? -1 : 1), offset.y, 0);
                var position = transform.InverseTransformPoint(target.transform.TransformPoint(local));
                position.z = originalLightPosition.z;
                var previous = motor.LanternTransform.localPosition;
                // The artwork keeps its authored cadence. Only the light between its
                // hand markers settles, avoiding room-wide lighting steps at every frame.
                bool snap = !hasLightAnchor || deltaTime <= 0f || explicitContext ||
                    lightFacing != motor.FacingDirection || lightAnimation != CurrentAnimation ||
                    (position - previous).sqrMagnitude > 1f;
                motor.LanternTransform.localPosition = snap ? position
                    : Vector3.Lerp(previous, position, 1f - Mathf.Exp(-deltaTime / .035f));
                hasLightAnchor = true;
                lightFacing = motor.FacingDirection;
                lightAnimation = CurrentAnimation;
            }
        }
    }
}
