using UnityEngine;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera2D : MonoBehaviour
    {
        [Header("Target & Base Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        [Header("Horizontal Look-Ahead")]
        [SerializeField] private bool enableLookAhead = true;
        [Tooltip("Extra horizontal distance the camera leads ahead while moving.")]
        [SerializeField, Min(0f)] private float lookAheadDistance = 0.85f;
        [Tooltip("Horizontal distance the camera maintains ahead in the facing direction while idle.")]
        [SerializeField, Min(0f)] private float idleLookAheadDistance = 0.35f;
        [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.25f;

        [Header("Vertical Fall Anticipation")]
        [SerializeField] private bool enableFallAnticipation = true;
        [Tooltip("Downward vertical velocity threshold to trigger fall anticipation (negative value).")]
        [SerializeField] private float fallVelocityThreshold = -3.5f;
        [Tooltip("Maximum downward offset when falling rapidly.")]
        [SerializeField, Min(0f)] private float fallLeadDistance = 1.2f;
        [SerializeField, Min(0.01f)] private float fallSmoothTime = 0.35f;

        [Header("Screen Shake & Juice")]
        [SerializeField] private bool enableLandingShake = true;
        [Tooltip("Minimum downward impact velocity required to trigger a landing shake.")]
        [SerializeField, Min(1f)] private float minShakeImpactSpeed = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float landingShakeIntensity = 0.08f;
        [SerializeField, Range(0.05f, 0.5f)] private float landingShakeDuration = 0.12f;

        [Header("Bounds (Optional)")]
        [SerializeField] private bool enableBounds = false;
        [SerializeField] private Vector2 boundsMin = new Vector2(-50f, -10f);
        [SerializeField] private Vector2 boundsMax = new Vector2(50f, 20f);

        private Vector3 followVelocity;
        private Vector3 currentFollowPosition;
        private float currentLookAheadX;
        private float lookAheadVelocity;
        private float currentFallLeadY;
        private float fallLeadVelocity;

        private float shakeIntensity;
        private float shakeDuration;
        private float shakeTimer;

        private PlayerMotor2D targetMotor;
        private Rigidbody2D targetBody;
        private Vector3 previousTargetPosition;

        public static FollowCamera2D ActiveInstance { get; private set; }

        public Transform Target => target;
        public Vector3 Offset { get => offset; set => offset = value; }
        public float SmoothTime { get => smoothTime; set => smoothTime = value; }
        public bool EnableLookAhead { get => enableLookAhead; set => enableLookAhead = value; }
        public float LookAheadDistance { get => lookAheadDistance; set => lookAheadDistance = value; }
        public float IdleLookAheadDistance { get => idleLookAheadDistance; set => idleLookAheadDistance = value; }
        public bool EnableFallAnticipation { get => enableFallAnticipation; set => enableFallAnticipation = value; }
        public float FallLeadDistance { get => fallLeadDistance; set => fallLeadDistance = value; }
        public float FallVelocityThreshold { get => fallVelocityThreshold; set => fallVelocityThreshold = value; }
        public bool EnableLandingShake { get => enableLandingShake; set => enableLandingShake = value; }
        public float CurrentLookAheadX => currentLookAheadX;
        public float CurrentFallLeadY => currentFallLeadY;
        public bool IsShaking => shakeTimer > 0f;
        public Vector3 CurrentFollowPosition => currentFollowPosition;

        private void OnDestroy()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            if (targetMotor != null)
            {
                targetMotor.OnLanded -= HandleTargetLanded;
            }
        }

        private void Awake()
        {
            if (ActiveInstance == null) ActiveInstance = this;
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            currentFollowPosition = transform.position;
            if (target == null)
            {
                var player = PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<PlayerMotor2D>();
                if (player != null)
                {
                    target = player.transform;
                }
            }
            if (target != null)
                BindTarget(target);
        }

        private void Start()
        {
            if (target == null)
            {
                var player = PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<PlayerMotor2D>();
                if (player != null)
                {
                    SetTarget(player.transform, snap: true);
                }
            }
            else
            {
                currentFollowPosition = target.position + offset;
                transform.position = currentFollowPosition;
            }
        }

        private void OnEnable()
        {
            if (ActiveInstance == null) ActiveInstance = this;
            if (target != null && targetMotor == null)
                BindTarget(target);
        }

        private void OnDisable()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            UnbindTarget();
        }

        public void SetTarget(Transform newTarget, bool snap = false)
        {
            UnbindTarget();
            target = newTarget;
            followVelocity = Vector3.zero;
            currentLookAheadX = 0f;
            lookAheadVelocity = 0f;
            currentFallLeadY = 0f;
            fallLeadVelocity = 0f;

            if (target != null)
            {
                BindTarget(target);
                previousTargetPosition = target.position;
                if (snap)
                {
                    currentFollowPosition = target.position + offset;
                    transform.position = currentFollowPosition;
                }
            }
        }

        public void Shake(float intensity, float duration)
        {
            if (duration <= 0f || intensity <= 0f)
                return;

            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = duration;
            shakeTimer = duration;
        }

        private void BindTarget(Transform t)
        {
            targetMotor = t.GetComponent<PlayerMotor2D>();
            targetBody = t.GetComponent<Rigidbody2D>();
            if (targetMotor != null)
                targetMotor.OnLanded += HandleTargetLanded;
            previousTargetPosition = t.position;
        }

        private void UnbindTarget()
        {
            if (targetMotor != null)
            {
                targetMotor.OnLanded -= HandleTargetLanded;
                targetMotor = null;
            }
            targetBody = null;
        }

        private void HandleTargetLanded(float impactSpeed)
        {
            if (!enableLandingShake)
                return;

            if (impactSpeed >= minShakeImpactSpeed)
            {
                float t = Mathf.Clamp01((impactSpeed - minShakeImpactSpeed) / 8f);
                float intensity = Mathf.Lerp(landingShakeIntensity, landingShakeIntensity * 1.8f, t);
                Shake(intensity, landingShakeDuration);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                var player = PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<PlayerMotor2D>();
                if (player != null)
                {
                    SetTarget(player.transform, snap: true);
                }
                else
                {
                    return;
                }
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // 1. Calculate target velocity
            Vector2 velocity;
            if (targetBody != null)
            {
                velocity = targetBody.linearVelocity;
            }
            else
            {
                velocity = (target.position - previousTargetPosition) / dt;
            }
            previousTargetPosition = target.position;

            // 2. Horizontal Look-Ahead
            float targetLookAheadX = 0f;
            if (enableLookAhead)
            {
                if (Mathf.Abs(velocity.x) > 0.25f)
                {
                    targetLookAheadX = Mathf.Sign(velocity.x) * lookAheadDistance;
                }
                else if (targetMotor != null)
                {
                    targetLookAheadX = targetMotor.FacingDirection * idleLookAheadDistance;
                }
                currentLookAheadX = Mathf.SmoothDamp(
                    currentLookAheadX, targetLookAheadX, ref lookAheadVelocity, lookAheadSmoothTime);
            }
            else
            {
                currentLookAheadX = 0f;
            }

            // 3. Vertical Fall Anticipation
            float targetFallLeadY = 0f;
            if (enableFallAnticipation)
            {
                bool isFalling = velocity.y < fallVelocityThreshold;
                if (targetMotor != null && targetMotor.IsGrounded)
                    isFalling = false;

                if (isFalling)
                {
                    float excessDropSpeed = Mathf.Abs(velocity.y) - Mathf.Abs(fallVelocityThreshold);
                    float factor = Mathf.Clamp01(excessDropSpeed / 6f);
                    targetFallLeadY = -factor * fallLeadDistance;
                }
                currentFallLeadY = Mathf.SmoothDamp(
                    currentFallLeadY, targetFallLeadY, ref fallLeadVelocity, fallSmoothTime);
            }
            else
            {
                currentFallLeadY = 0f;
            }

            // 4. Base smoothed follow
            Vector3 desiredPosition = target.position + offset + new Vector3(currentLookAheadX, currentFallLeadY, 0f);

            // Bounds clamping if enabled
            if (enableBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, boundsMin.x, boundsMax.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, boundsMin.y, boundsMax.y);
            }

            currentFollowPosition = Vector3.SmoothDamp(
                currentFollowPosition, desiredPosition, ref followVelocity, smoothTime);

            // 5. Screen Shake Trauma
            Vector3 shakeOffset = Vector3.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                float progress = Mathf.Clamp01(shakeTimer / Mathf.Max(0.0001f, shakeDuration));
                float currentShake = shakeIntensity * progress;
                float noiseX = (Mathf.PerlinNoise(Time.time * 40f, 0f) * 2f - 1f) * currentShake;
                float noiseY = (Mathf.PerlinNoise(0f, Time.time * 40f) * 2f - 1f) * currentShake;
                shakeOffset = new Vector3(noiseX, noiseY, 0f);

                if (shakeTimer <= 0f)
                    shakeIntensity = 0f;
            }

            transform.position = currentFollowPosition + shakeOffset;
        }
    }
}
