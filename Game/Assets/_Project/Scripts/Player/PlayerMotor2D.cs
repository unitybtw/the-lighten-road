using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        [Header("Jumping & Responsiveness")]
        [SerializeField, Min(0f)] private float jumpSpeed = 9f;
        [Tooltip("Grace period in seconds allowing a jump shortly after walking off a ledge.")]
        [SerializeField, Range(0.01f, 0.3f)] private float coyoteTime = 0.15f;
        [Tooltip("Window in seconds to remember a jump press before touching the ground.")]
        [SerializeField, Range(0.01f, 0.3f)] private float jumpBufferTime = 0.12f;
        [Tooltip("Releasing jump early cuts upward velocity for short-hop control.")]
        [SerializeField] private bool variableJumpHeight = true;
        [SerializeField, Range(0.1f, 1f)] private float jumpCutMultiplier = 0.5f;

        [Header("Visual Juice & Polish")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform visualTransform;
        [SerializeField] private bool enableSquashAndStretch = true;
        [SerializeField] private Vector2 jumpStretch = new Vector2(0.85f, 1.25f);
        [SerializeField] private Vector2 landSquash = new Vector2(1.25f, 0.75f);
        [SerializeField, Min(1f)] private float squashRecoverySpeed = 9f;

        [Header("Ground Detection")]
        [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.06f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private Rigidbody2D body;
        private InputAction move;
        private InputAction jump;
        private float horizontalInput;

        private float coyoteTimer;
        private float jumpBufferTimer;
        private bool jumpReleased;
        private int facingDirection = 1;
        private Vector3 baseVisualScale = Vector3.one;
        private Vector2 squashScale = Vector2.one;
        private float previousVerticalVelocity;

        public event System.Action OnJumped;
        public event System.Action<float> OnLanded;

        public bool IsGrounded { get; private set; }
        public int FacingDirection => facingDirection;
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        public float JumpSpeed { get => jumpSpeed; set => jumpSpeed = value; }
        public float CoyoteTime { get => coyoteTime; set => coyoteTime = value; }
        public float JumpBufferTime { get => jumpBufferTime; set => jumpBufferTime = value; }
        public bool VariableJumpHeight { get => variableJumpHeight; set => variableJumpHeight = value; }
        public bool IsInCoyoteTime => coyoteTimer > 0f && !IsGrounded;
        public bool HasBufferedJump => jumpBufferTimer > 0f;
        public Vector3 CurrentVisualScale => visualTransform != null ? visualTransform.localScale : Vector3.one;
        public float HorizontalInput => horizontalInput;
        public Rigidbody2D Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (visualTransform == null)
            {
                var visualChild = transform.Find("Placeholder Visual");
                visualTransform = visualChild != null ? visualChild : (spriteRenderer != null ? spriteRenderer.transform : transform);
            }

            if (visualTransform != null)
                baseVisualScale = visualTransform.localScale;

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
        }

        private void OnEnable()
        {
            move.Enable();
            jump.Enable();
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
        }

        private void FixedUpdate()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = CheckGrounded();

            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;

                // Trigger landing squash when touching ground from air
                if (!wasGrounded && body.linearVelocity.y <= 0.1f)
                {
                    float impactSpeed = Mathf.Abs(previousVerticalVelocity);
                    ApplySquash(landSquash);
                    OnLanded?.Invoke(impactSpeed);
                }
            }
            else
            {
                coyoteTimer -= Time.fixedDeltaTime;
            }

            if (jumpBufferTimer > 0f)
            {
                jumpBufferTimer -= Time.fixedDeltaTime;
            }

            var velocity = body.linearVelocity;
            velocity.x = horizontalInput * moveSpeed;

            // Execute jump if buffer is active AND player is either grounded or within coyote time
            if (jumpBufferTimer > 0f && (IsGrounded || coyoteTimer > 0f))
            {
                velocity.y = jumpSpeed;
                IsGrounded = false;
                coyoteTimer = 0f;
                jumpBufferTimer = 0f;
                jumpReleased = false;
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
                return false;

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(groundLayers);
            int count = body.Cast(Vector2.down, filter, groundHits, groundCheckDistance);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].normal.y >= 0.65f)
                    return true;
            }
            return false;
        }

        private void OnDisable()
        {
            move.Disable();
            jump.Disable();
            horizontalInput = 0f;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpReleased = false;
            IsGrounded = false;
            if (body != null)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        private void OnDestroy()
        {
            move?.Dispose();
            jump?.Dispose();
        }
    }
}
