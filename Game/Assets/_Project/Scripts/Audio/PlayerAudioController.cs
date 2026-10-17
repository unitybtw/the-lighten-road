using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;

namespace TheLightenRoad.Audio
{
    /// <summary>
    /// Connects PlayerMotor2D, PlayerStamina, and LanternController events
    /// to SoundEffectManager for responsive, tactile audio feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudioController : MonoBehaviour
    {
        [Header("References (auto-found when empty)")]
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private LanternController lantern;

        [Header("Footstep Intervals")]
        [Tooltip("Seconds between footstep sounds while walking.")]
        [SerializeField, Min(0.1f)] private float walkInterval = 0.44f;
        [Tooltip("Seconds between footstep sounds while sprinting.")]
        [SerializeField, Min(0.1f)] private float sprintInterval = 0.28f;

        private float footstepTimer;

        public float WalkInterval { get => walkInterval; set => walkInterval = value; }
        public float SprintInterval { get => sprintInterval; set => sprintInterval = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToActivePlayers()
        {
            AttachToAllMotors();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            AttachToAllMotors();
        }

        public static void AttachToAllMotors()
        {
            var motors = Object.FindObjectsByType<PlayerMotor2D>(FindObjectsSortMode.None);
            foreach (var m in motors)
            {
                if (m != null && m.GetComponent<PlayerAudioController>() == null)
                {
                    m.gameObject.AddComponent<PlayerAudioController>();
                }
            }
        }

        public void EnsureReferences()
        {
            if (motor == null) motor = GetComponent<PlayerMotor2D>() ?? GetComponentInParent<PlayerMotor2D>() ?? GetComponentInChildren<PlayerMotor2D>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>() ?? GetComponentInParent<PlayerStamina>() ?? GetComponentInChildren<PlayerStamina>();
            if (lantern == null) lantern = GetComponent<LanternController>() ?? GetComponentInParent<LanternController>() ?? GetComponentInChildren<LanternController>();
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();

            if (motor != null)
            {
                motor.OnJumped -= HandleJumped;
                motor.OnJumped += HandleJumped;
                motor.OnLanded -= HandleLanded;
                motor.OnLanded += HandleLanded;
            }

            if (stamina != null)
            {
                stamina.ExhaustionChanged -= HandleExhaustionChanged;
                stamina.ExhaustionChanged += HandleExhaustionChanged;
            }

            if (lantern != null)
            {
                lantern.FlareChanged -= HandleFlareChanged;
                lantern.FlareChanged += HandleFlareChanged;
                lantern.EmberBroken -= HandleEmberBroken;
                lantern.EmberBroken += HandleEmberBroken;
            }

            footstepTimer = 0.05f;
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.OnJumped -= HandleJumped;
                motor.OnLanded -= HandleLanded;
            }

            if (stamina != null)
            {
                stamina.ExhaustionChanged -= HandleExhaustionChanged;
            }

            if (lantern != null)
            {
                lantern.FlareChanged -= HandleFlareChanged;
                lantern.EmberBroken -= HandleEmberBroken;
            }

            if (SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance?.SetExhaustionActive(false);
            }
        }

        private void Update()
        {
            TickFootsteps(Time.deltaTime);
        }

        public void TickFootsteps(float dt)
        {
            if (motor == null)
            {
                EnsureReferences();
                if (motor == null) return;
            }

            bool isMovingOnGround = motor.IsGrounded && (Mathf.Abs(motor.HorizontalSpeed) > 0.15f || Mathf.Abs(motor.HorizontalInput) > 0.1f);

            if (isMovingOnGround)
            {
                footstepTimer -= dt;
                if (footstepTimer <= 0f)
                {
                    bool isSprinting = motor.IsSprinting;
                    footstepTimer = isSprinting ? sprintInterval : walkInterval;

                    if (isSprinting)
                    {
                        SoundEffectManager.Instance.PlaySprintFootstep();
                    }
                    else
                    {
                        SoundEffectManager.Instance.PlayWalkFootstep();
                    }
                }
            }
            else
            {
                footstepTimer = 0.05f;
            }
        }

        private void HandleJumped()
        {
            SoundEffectManager.Instance.PlayJump();
        }

        private void HandleLanded(float impactSpeed)
        {
            SoundEffectManager.Instance.PlayLanding(impactSpeed);
        }

        private void HandleExhaustionChanged(bool isExhausted)
        {
            SoundEffectManager.Instance.SetExhaustionActive(isExhausted);
        }

        private void HandleFlareChanged(bool isFlared)
        {
            if (isFlared)
            {
                SoundEffectManager.Instance.PlayFlareIgnite();
            }
        }

        private void HandleEmberBroken()
        {
            SoundEffectManager.Instance.PlayEmberBreak();
        }
    }
}
