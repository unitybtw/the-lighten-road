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
        public bool AutomaticTicks { get; set; } = true;
        public PlayerAnimationCatalog Catalog => catalog;
        public LightAnimation CurrentAnimation => sequence != null ? sequence.Kind : LightAnimation.Idle;
        public int CurrentFrame { get; private set; }
        public SpriteRenderer Renderer => target;

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
            Select(LightAnimation.Idle, false, true);
            RenderFrame();
        }
        private void OnDisable()
        {
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
        { explicitContext = holdContext = false; Select(LightAnimation.Jump, true, true); }
        private void Landed(float impact)
        { if (!explicitContext) Select(LightAnimation.Landing, true, true); }
        private void FlareChanged(bool active)
        {
            if (active && IsAlive && !explicitContext) Select(LightAnimation.Flare, true, true);
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
            explicitContext = holdContext = false; transferFrame = -1;
            Select(LightAnimation.Idle, false, true); RenderFrame();
        }
        /// <summary>Visual hook for future climbing/stowing owners. It does not move or lock the player.</summary>
        public bool PlayContextAnimation(LightAnimation kind, bool holdLastFrame = false)
        {
            if (!IsAlive || catalog == null || catalog.Find(kind) == null) return false;
            explicitContext = true; holdContext = holdLastFrame;
            Select(kind, true, true); return true;
        }
        public void ClearContextAnimation()
        { explicitContext = holdContext = false; oneShot = false; Select(LightAnimation.Idle, false, true); }

        private void LateUpdate() { if (AutomaticTicks) Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            if (sequence == null || target == null || !IsAlive || deltaTime <= 0) return;
            bool moving = motor.IsGrounded && Mathf.Abs(motor.HorizontalInput) > .05f && Mathf.Abs(motor.HorizontalSpeed) > .08f;
            bool airborne = motor.enabled && !motor.IsGrounded;
            if (explicitContext)
            {
                elapsed += deltaTime;
                if (elapsed >= sequence.Duration && !holdContext)
                { explicitContext = false; oneShot = false; }
            }
            else
            {
                if (airborne && CurrentAnimation != LightAnimation.Jump && CurrentAnimation != LightAnimation.Flare)
                    Select(LightAnimation.Jump, true, true);
                if (moving && oneShot && (CurrentAnimation == LightAnimation.Landing || CurrentAnimation == LightAnimation.EmberSwap)) oneShot = false;
                if (!oneShot) Select(airborne ? LightAnimation.Jump : moving ? LightAnimation.Walk : LightAnimation.Idle, airborne, false);
                float speed = CurrentAnimation == LightAnimation.Walk ? Mathf.Clamp(Mathf.Abs(motor.HorizontalSpeed) / Mathf.Max(.1f, motor.WalkSpeed), .55f, 1.8f) : 1;
                elapsed += deltaTime * speed;
                if (oneShot && elapsed >= sequence.Duration && (!airborne || CurrentAnimation != LightAnimation.Jump)) oneShot = false;
            }
            RenderFrame();
        }
        private void Select(LightAnimation kind, bool once, bool restart)
        {
            if (catalog == null || (!restart && sequence != null && sequence.Kind == kind)) return;
            var next = catalog.Find(kind);
            if (next == null || next.Frames == null || next.Frames.Length == 0) return;
            sequence = next; elapsed = 0; oneShot = once; CurrentFrame = 0;
        }
        private void RenderFrame()
        {
            if (sequence == null || target == null) return;
            CurrentFrame = sequence.FrameAt(elapsed);
            if (CurrentFrame < 0) return;
            target.sprite = sequence.Frames[CurrentFrame];
            target.flipX = motor.FacingDirection < 0;
            if (motor.LanternTransform != null && sequence.LanternOffsets != null && CurrentFrame < sequence.LanternOffsets.Length)
            {
                var offset = sequence.LanternOffsets[CurrentFrame];
                // Convert through the sprite's transform so prefab visual offsets/scales remain valid.
                var local = new Vector3(offset.x * (target.flipX ? -1 : 1), offset.y, 0);
                var position = transform.InverseTransformPoint(target.transform.TransformPoint(local));
                position.z = originalLightPosition.z;
                motor.LanternTransform.localPosition = position;
            }
        }
    }
}
