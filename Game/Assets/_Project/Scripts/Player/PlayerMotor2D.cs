using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour
    {
        [Header("Prototype tuning — subject to playtesting")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float jumpSpeed = 9f;
        [SerializeField, Min(0.001f)] private float groundCheckDistance = 0.06f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private Rigidbody2D body;
        private InputAction move;
        private InputAction jump;
        private float horizontalInput;
        private bool jumpRequested;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;

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
            jumpRequested |= jump.WasPressedThisFrame();
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            var velocity = body.linearVelocity;
            velocity.x = horizontalInput * moveSpeed;
            if (jumpRequested && IsGrounded)
            {
                velocity.y = jumpSpeed;
                IsGrounded = false;
            }
            // Airborne presses are consumed here, so they cannot queue a second jump.
            jumpRequested = false;
            body.linearVelocity = velocity;
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
            jumpRequested = false;
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
