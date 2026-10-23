using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Atmospheric walking speed through dark ruins.")]
        [SerializeField, Min(0f)] private float walkSpeed = 3.2f;
        [Tooltip("Athletic sprint speed for crossing wide gaps and escaping.")]
        [SerializeField, Min(0f)] private float runSpeed = 5.5f;

        [Header("Stamina & Endurance")]
        [SerializeField] private PlayerStamina stamina;

        [Header("Jumping & Responsiveness")]
        [SerializeField, Min(0f)] private float jumpSpeed = 9f;
        [Tooltip("Grace period in seconds allowing a jump shortly after walking off a ledge.")]
        [SerializeField, Range(0.01f, 0.3f)] private float coyoteTime = 0.15f;
        [Tooltip("Window in seconds to remember a jump press before touching the ground.")]
        [SerializeField, Range(0.01f, 0.3f)] private float jumpBufferTime = 0.12f;
        [Tooltip("Releasing jump early cuts upward velocity for short-hop control.")]
        [SerializeField] private bool variableJumpHeight = true;
        [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.5f;

        [Header("Kinematics & Responsiveness")]
        [Tooltip("Acceleration rate when moving from rest or increasing speed on the ground.")]
        [SerializeField, Min(10f)] private float groundAcceleration = 120f;
        [Tooltip("Deceleration rate when releasing input on the ground.")]
        [SerializeField, Min(10f)] private float groundDeceleration = 90f;
        [Tooltip("Snappy braking rate when reversing direction (skid turn).")]
        [SerializeField, Min(10f)] private float skidTurnDeceleration = 120f;
        [Tooltip("Airborne horizontal control acceleration.")]
        [SerializeField, Min(10f)] private float airAcceleration = 80f;
        [Tooltip("Airborne horizontal drag when no input is held.")]
        [SerializeField, Min(10f)] private float airDeceleration = 30f;

        [Header("Jump Physics & Apex Feel")]
        [Tooltip("Reduces gravity near jump apex to create weightless hang-time and precise landing control.")]
        [SerializeField] private bool enableApexHangTime = true;
        [SerializeField, Range(0.2f, 1f)] private float apexGravityMultiplier = 0.65f;
        [SerializeField, Range(0.5f, 3f)] private float apexVelocityThreshold = 1.4f;
        [Tooltip("Slightly firmer gravity when descending for snappy platforming.")]
        [SerializeField, Range(1f, 2f)] private float fallGravityMultiplier = 1.0f;
        [Tooltip("Maximum downward fall speed to prevent disorienting plunges.")]
        [SerializeField] private float terminalFallSpeed = -16f;

        [Header("Visual Juice & Polish")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform visualTransform;
        [SerializeField] private Transform lanternTransform;
        [SerializeField] private bool enableSquashAndStretch = true;
        [SerializeField] private Vector2 jumpStretch = new Vector2(0.85f, 1.25f);
        [SerializeField] private Vector2 landSquash = new Vector2(1.25f, 0.75f);
        [SerializeField, Min(1f)] private float squashRecoverySpeed = 9f;

        [Header("Kinetic Visual Lean")]
        [SerializeField] private bool enableKineticTilt = true;
        [SerializeField, Range(0f, 10f)] private float maxRunTiltAngle = 3.2f;
        [SerializeField, Range(0f, 5f)] private float maxWalkTiltAngle = 1.5f;
        [SerializeField, Min(1f)] private float tiltRecoverySpeed = 30f;

        [Header("Ledge Assist & Corner Correction")]
        [SerializeField] private bool enableCornerCorrection = true;
        [SerializeField, Range(0.01f, 0.2f)] private float cornerCorrectionDistance = 0.10f;

        [Header("Ground Detection")]
        [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.06f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private Rigidbody2D body;
        private CapsuleCollider2D capsuleCollider;
        private InputAction move;
        private InputAction jump;
        private InputAction sprint;
        private float horizontalInput;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool jumpReleased;
        private int facingDirection = 1;
        private Vector3 baseVisualScale = Vector3.one;
        private Vector2 squashScale = Vector2.one;
        private float previousVerticalVelocity;
        private float baseGravityScale = 3f;
        private float currentTilt;
        private bool groundStateInitialized;

        public event System.Action OnJumped;
        public event System.Action<float> OnLanded;

        public bool IsGrounded { get; private set; }
        public Collider2D GroundCollider { get; private set; }
        public int FacingDirection => facingDirection;
        public float WalkSpeed { get => walkSpeed; set => walkSpeed = value; }
        public float RunSpeed { get => runSpeed; set => runSpeed = value; }
        public float MoveSpeed { get => CurrentSpeed; set => walkSpeed = value; }
        public float CurrentSpeed => IsRunning ? runSpeed : walkSpeed;
        public bool IsRunning { get; private set; }
        public bool IsSprinting => IsRunning;
        public PlayerStamina Stamina => stamina;
        public float JumpSpeed { get => jumpSpeed; set => jumpSpeed = value; }
        public float CoyoteTime { get => coyoteTime; set => coyoteTime = value; }
        public float JumpBufferTime { get => jumpBufferTime; set => jumpBufferTime = value; }
        public bool VariableJumpHeight { get => variableJumpHeight; set => variableJumpHeight = value; }
        public bool IsInCoyoteTime => coyoteTimer > 0f && !IsGrounded;
        public bool HasBufferedJump => jumpBufferTimer > 0f;
        public Vector3 CurrentVisualScale => visualTransform != null ? visualTransform.localScale : Vector3.one;
        public Transform LanternTransform => lanternTransform;
        public float HorizontalInput => horizontalInput;
        public float HorizontalSpeed => body != null ? body.linearVelocity.x : 0f;
        public Rigidbody2D Body => body;
        public float GroundAcceleration { get => groundAcceleration; set => groundAcceleration = value; }
        public float GroundDeceleration { get => groundDeceleration; set => groundDeceleration = value; }
        public bool EnableApexHangTime { get => enableApexHangTime; set => enableApexHangTime = value; }
        public bool EnableKineticTilt { get => enableKineticTilt; set => enableKineticTilt = value; }
        public bool EnableCornerCorrection { get => enableCornerCorrection; set => enableCornerCorrection = value; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            baseGravityScale = body.gravityScale;
            capsuleCollider = GetComponent<CapsuleCollider2D>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (visualTransform == null)
            {
                var visualChild = transform.Find("Placeholder Visual");
                visualTransform = visualChild != null ? visualChild : (spriteRenderer != null ? spriteRenderer.transform : transform);
            }

            if (visualTransform != null)
                baseVisualScale = visualTransform.localScale;

            if (lanternTransform == null)
            {
                var lanternChild = transform.Find("Lantern Light");
                if (lanternChild != null)
                    lanternTransform = lanternChild;
            }

            if (stamina == null)
                stamina = GetComponent<PlayerStamina>();

            // Local prototype bindings avoid changing the team's shared input asset.
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Axis");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick/x");

            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Keyboard>/rightShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");
            sprint.AddBinding("<Gamepad>/rightTrigger");
            sprint.AddBinding("<Gamepad>/buttonEast");
        }

        private void OnEnable()
        {
            move.Enable();
            jump.Enable();
            sprint.Enable();
            groundStateInitialized = false;
            IsGrounded = CheckGrounded();
        }

        private void Update()
        {
            horizontalInput = move.ReadValue<float>();

            if (jump.WasPressedThisFrame())
            {
                jumpBufferTimer = jumpBufferTime;
            }

            if (variableJumpHeight && jump.WasReleasedThisFrame())
            {
                jumpReleased = true;
            }

            // Smooth squash & stretch recovery back to normal 1x scale
            if (enableSquashAndStretch && visualTransform != null)
            {
                squashScale = Vector2.MoveTowards(squashScale, Vector2.one, squashRecoverySpeed * Time.deltaTime);
                visualTransform.localScale = new Vector3(
                    baseVisualScale.x * squashScale.x,
                    baseVisualScale.y * squashScale.y,
                    baseVisualScale.z);
            }

            // Smooth kinetic tilt in the direction of motion
            if (enableKineticTilt && visualTransform != null)
            {
                float targetTilt = 0f;
                if (IsGrounded && Mathf.Abs(horizontalInput) > 0.05f)
                {
                    targetTilt = -horizontalInput * (IsRunning ? maxRunTiltAngle : maxWalkTiltAngle);
                }
                currentTilt = Mathf.MoveTowards(currentTilt, targetTilt, tiltRecoverySpeed * Time.deltaTime);
                visualTransform.localRotation = Quaternion.Euler(0f, 0f, currentTilt);
            }
        }

        private void FixedUpdate()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = CheckGrounded();

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;

                // Trigger landing squash when touching ground from air (suppressed on initial spawn frame or micro-curbs)
                if (!wasGrounded && body.linearVelocity.y <= 0.1f && groundStateInitialized)
                {
                    float impactSpeed = Mathf.Abs(previousVerticalVelocity);
                    if (impactSpeed > 0.8f)
                    {
                        ApplySquash(landSquash);
                        OnLanded?.Invoke(impactSpeed);
                    }
                }
            }
            else
            {
                coyoteTimer -= Time.fixedDeltaTime;
            }

            groundStateInitialized = true;

            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= Time.fixedDeltaTime;
            }

            bool sprintHeld = (sprint != null && (sprint.IsPressed() || sprint.ReadValue<float>() > 0.5f))
                || (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed));
            bool hasHorizontalInput = Mathf.Abs(horizontalInput) > 0.05f;

            if (IsGrounded)
            {
                bool canSprint = sprintHeld && hasHorizontalInput && (stamina == null || stamina.CanSprint);
                if (canSprint)
                {
                    IsRunning = true;
                    if (stamina != null)
                    {
                        stamina.ConsumeSprint(Time.fixedDeltaTime);
                    }
                }
                else
                {
                    IsRunning = false;
                    if (stamina != null)
                    {
                        stamina.Regenerate(Time.fixedDeltaTime);
                    }
                }
            }
            else
            {
                // In air: retain running momentum if we jumped while running
                if (IsRunning && stamina != null && sprintHeld)
                {
                    stamina.ConsumeSprint(Time.fixedDeltaTime * 0.5f);
                }
            }

            var velocity = body.linearVelocity;
            float currentMoveSpeed = IsRunning ? runSpeed : walkSpeed;
            float targetSpeed = horizontalInput * currentMoveSpeed;

            float accelRate;
            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                if (IsGrounded)
                {
                    bool isReversing = Mathf.Abs(velocity.x) > 0.1f && Mathf.Sign(velocity.x) != Mathf.Sign(targetSpeed);
                    accelRate = isReversing ? skidTurnDeceleration : groundAcceleration;
                }
                else
                {
                    accelRate = airAcceleration;
                }
            }
            else
            {
                accelRate = IsGrounded ? groundDeceleration : airDeceleration;
            }

            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);

            // Execute jump if buffer is active AND player is either grounded or within coyote time
            if (jumpBufferTimer > 0f && (IsGrounded || coyoteTimer > 0f))
            {
                velocity.y = jumpSpeed;
                IsGrounded = false;
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpReleased = false;

                if (IsRunning && stamina != null)
                {
                    stamina.TryConsumeJump();
                }

                ApplySquash(jumpStretch);
                OnJumped?.Invoke();
            }

            // Variable jump: cutting upward velocity when space is released early
            if (variableJumpHeight && jumpReleased)
            {
                if (velocity.y > 0.5f)
                {
                    velocity.y *= jumpCutMultiplier;
                }
                jumpReleased = false;
            }

            // Ledge corner correction when rising
            if (enableCornerCorrection && velocity.y > 0.5f && capsuleCollider != null)
            {
                PerformCornerCorrection();
            }

            // Dynamic apex hang time & fall feel
            if (enableApexHangTime && !IsGrounded)
            {
                if (Mathf.Abs(velocity.y) < apexVelocityThreshold)
                {
                    body.gravityScale = baseGravityScale * apexGravityMultiplier;
                }
                else if (velocity.y < -0.5f)
                {
                    body.gravityScale = baseGravityScale * fallGravityMultiplier;
                }
                else
                {
                    body.gravityScale = baseGravityScale;
                }
            }
            else
            {
                body.gravityScale = baseGravityScale;
            }

            // Terminal fall speed clamp
            if (velocity.y < terminalFallSpeed)
            {
                velocity.y = terminalFallSpeed;
            }

            body.linearVelocity = velocity;
            previousVerticalVelocity = velocity.y;

            // Update facing direction and flip sprite
            if (Mathf.Abs(horizontalInput) > 0.05f)
            {
                facingDirection = horizontalInput > 0f ? 1 : -1;
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = facingDirection < 0;
                }

                if (lanternTransform != null)
                {
                    var lp = lanternTransform.localPosition;
                    lp.x = Mathf.Abs(lp.x) * facingDirection;
                    lanternTransform.localPosition = lp;
                }
            }
        }

        private void ApplySquash(Vector2 factor)
        {
            if (!enableSquashAndStretch || visualTransform == null)
                return;

            squashScale = factor;
            visualTransform.localScale = new Vector3(
                baseVisualScale.x * squashScale.x,
                baseVisualScale.y * squashScale.y,
                baseVisualScale.z);
        }

        private bool CheckGrounded()
        {
            if (body.linearVelocity.y > 0.05f)
            {
                GroundCollider = null;
                return false;
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(groundLayers);
            int count = body.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].normal.y >= 0.65f)
                {
                    GroundCollider = groundHits[i].collider;
                    return true;
                }
            }
            GroundCollider = null;
            return false;
        }

        private void PerformCornerCorrection()
        {
            var bounds = capsuleCollider.bounds;
            float rayLength = cornerCorrectionDistance + 0.05f;
            Vector2 originCenter = new Vector2(bounds.center.x, bounds.max.y);
            Vector2 originLeft = new Vector2(bounds.min.x + 0.04f, bounds.max.y);
            Vector2 originRight = new Vector2(bounds.max.x - 0.04f, bounds.max.y);

            var hitCenter = GetSolidCeilingHit(originCenter, rayLength);
            var hitLeft = GetSolidCeilingHit(originLeft, rayLength);
            var hitRight = GetSolidCeilingHit(originRight, rayLength);

            // If right edge hits solid ceiling but left and center are clear, nudge left around corner
            if (hitRight != null && hitLeft == null && hitCenter == null)
            {
                transform.position += Vector3.left * (cornerCorrectionDistance * 0.5f);
            }
            // If left edge hits solid ceiling but right and center are clear, nudge right around corner
            else if (hitLeft != null && hitRight == null && hitCenter == null)
            {
                transform.position += Vector3.right * (cornerCorrectionDistance * 0.5f);
            }
        }

        private Collider2D GetSolidCeilingHit(Vector2 origin, float length)
        {
            var hits = Physics2D.RaycastAll(origin, Vector2.up, length, groundLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col != null && !col.isTrigger && col.gameObject != gameObject)
                    return col;
            }
            return null;
        }

        private void OnDisable()
        {
            move.Disable();
            jump.Disable();
            sprint.Disable();
            horizontalInput = 0f;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpReleased = false;
            IsGrounded = false;
            GroundCollider = null;
            IsRunning = false;
            currentTilt = 0f;
            if (visualTransform != null) visualTransform.localRotation = Quaternion.identity;
            if (body != null)
            {
                body.gravityScale = baseGravityScale;
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            }
        }

        private void OnDestroy()
        {
            move?.Dispose();
            jump?.Dispose();
            sprint?.Dispose();
        }
    }
}
