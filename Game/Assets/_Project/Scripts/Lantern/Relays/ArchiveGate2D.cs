using TheLightenRoad.Audio;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.Events;

namespace TheLightenRoad.Lantern.Relays
{
    /// <summary>
    /// Heavy ancient archive security gate or portcullis.
    /// Connects to a PhotosensitiveRelay2D (or other triggers).
    /// When activated, grinding stone mechanisms lift the heavy iron gate into the ceiling.
    /// When deactivated, the gate slides down and locks into the foundation with a thud.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ArchiveGate2D : MonoBehaviour
    {
        [Header("Controlling Relay")]
        [SerializeField] private PhotosensitiveRelay2D relay;

        [Header("Movement Mechanics")]
        [Tooltip("Local position offset when the gate is fully open (typically straight up).")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3.2f, 0f);
        [Tooltip("Seconds required to smoothly travel from closed to open.")]
        [SerializeField, Min(0.1f)] private float travelDuration = 1.15f;
        [SerializeField] private bool startOpen = false;

        [Header("Visual & Audio Juice")]
        [SerializeField] private SpriteRenderer gateRenderer;
        [SerializeField] private BoxCollider2D gateCollider;
        [SerializeField] private bool screenShakeOnMovement = true;
        [SerializeField, Range(0.01f, 0.3f)] private float shakeIntensity = 0.08f;
        [SerializeField, Range(0.05f, 0.4f)] private float shakeDuration = 0.16f;
        [SerializeField] private bool enableAudio = true;

        [Header("Events")]
        public UnityEvent OnGateOpening = new UnityEvent();
        public UnityEvent OnGateFullyOpen = new UnityEvent();
        public UnityEvent OnGateClosing = new UnityEvent();
        public UnityEvent OnGateFullyClosed = new UnityEvent();

        private Vector3 closedPosition;
        private Vector3 openPosition;
        private float motionProgress;
        private bool isOpen;
        private bool wasOpen;
        private bool isMoving;

        public PhotosensitiveRelay2D Relay { get => relay; set => relay = value; }
        public bool IsOpen => isOpen;
        public bool IsMoving => isMoving;
        public float MotionProgress => motionProgress;
        public Vector3 OpenOffset { get => openOffset; set => openOffset = value; }
        public float TravelDuration { get => travelDuration; set => travelDuration = Mathf.Max(0.01f, value); }
        public BoxCollider2D GateCollider => gateCollider != null ? gateCollider : (gateCollider = GetComponent<BoxCollider2D>());

        private void Awake()
        {
            closedPosition = transform.position;
            openPosition = closedPosition + openOffset;

            EnsureHierarchy();

            isOpen = startOpen;
            wasOpen = startOpen;
            motionProgress = startOpen ? 1f : 0f;
            transform.position = Vector3.Lerp(closedPosition, openPosition, motionProgress);
        }

        private void OnEnable()
        {
            if (relay != null)
            {
                relay.OnStateChanged.AddListener(HandleRelayStateChanged);
                // Synchronize with initial relay state
                if (relay.IsActivated != isOpen)
                {
                    SetGateOpen(relay.IsActivated, instant: true);
                }
            }
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.OnStateChanged.RemoveListener(HandleRelayStateChanged);
            }
        }

        private void Start()
        {
            if (relay == null)
            {
                relay = GetComponentInParent<PhotosensitiveRelay2D>();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            float targetProgress = isOpen ? 1f : 0f;
            if (Mathf.Abs(motionProgress - targetProgress) > 0.0001f)
            {
                isMoving = true;
                float step = (1f / travelDuration) * dt;
                motionProgress = Mathf.MoveTowards(motionProgress, targetProgress, step);

                // Smooth easing
                float smooth = Mathf.SmoothStep(0f, 1f, motionProgress);
                transform.position = Vector3.Lerp(closedPosition, openPosition, smooth);

                if (Mathf.Abs(motionProgress - targetProgress) <= 0.0001f)
                {
                    motionProgress = targetProgress;
                    isMoving = false;
                    transform.position = isOpen ? openPosition : closedPosition;

                    if (isOpen)
                    {
                        OnGateFullyOpen?.Invoke();
                    }
                    else
                    {
                        OnGateFullyClosed?.Invoke();
                        if (enableAudio)
                        {
                            SoundEffectManager.Instance?.PlayGateClose();
                        }
                        TriggerCameraShake();
                    }
                }
            }
        }

        public void SetGateOpen(bool open, bool instant = false)
        {
            if (isOpen == open && !instant) return;

            isOpen = open;
            if (instant)
            {
                motionProgress = open ? 1f : 0f;
                transform.position = open ? openPosition : closedPosition;
                isMoving = false;
                if (isOpen) OnGateFullyOpen?.Invoke();
                else OnGateFullyClosed?.Invoke();
                return;
            }

            if (isOpen)
            {
                OnGateOpening?.Invoke();
                if (enableAudio)
                {
                    SoundEffectManager.Instance?.PlayGateOpen();
                }
                TriggerCameraShake();
            }
            else
            {
                OnGateClosing?.Invoke();
            }
        }

        public void OpenGate() => SetGateOpen(true);
        public void CloseGate() => SetGateOpen(false);

        private void HandleRelayStateChanged(bool active)
        {
            SetGateOpen(active);
        }

        private void TriggerCameraShake()
        {
            if (!screenShakeOnMovement) return;

            var cam = Object.FindAnyObjectByType<FollowCamera2D>();
            if (cam != null && cam.Target != null)
            {
                float dist = Vector2.Distance(transform.position, cam.Target.position);
                if (dist < 18f)
                {
                    cam.Shake(shakeIntensity, shakeDuration);
                }
            }
        }

        public void EnsureHierarchy()
        {
            if (gateCollider == null)
            {
                gateCollider = GetComponent<BoxCollider2D>();
                if (gateCollider == null)
                {
                    gateCollider = gameObject.AddComponent<BoxCollider2D>();
                }
            }

            gateCollider.size = new Vector2(1.75f, 3.0f);
            gateCollider.offset = new Vector2(0f, 1.5f);

            if (gateRenderer == null)
            {
                gateRenderer = GetComponent<SpriteRenderer>();
                if (gateRenderer == null)
                {
                    gateRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            gateRenderer.sortingOrder = 7;

            var gateSprite = Resources.Load<Sprite>("Art/Sprites/ArchiveGate");
            if (gateSprite == null) gateSprite = LoadSpriteDirect("Assets/_Project/Art/Sprites/ArchiveGate.png");
            if (gateRenderer.sprite == null && gateSprite != null)
            {
                gateRenderer.sprite = gateSprite;
            }
        }

        private static Sprite LoadSpriteDirect(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }
    }
}
