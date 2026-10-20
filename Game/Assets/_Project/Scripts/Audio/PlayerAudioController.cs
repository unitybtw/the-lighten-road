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
        private float skidCooldown;
        private float airDraftCooldown;

        public float WalkInterval { get => walkInterval; set => walkInterval = value; }
        public float SprintInterval { get => sprintInterval; set => sprintInterval = value; }

        public SurfaceType GetCurrentSurface()
        {
            if (motor == null) return SurfaceType.Stone;
            var col = motor.GroundCollider;
            if (col == null) return SurfaceType.Stone;

            var provider = col.GetComponent<SurfaceAudioTypeProvider>() 
                           ?? col.GetComponentInParent<SurfaceAudioTypeProvider>()
                           ?? col.GetComponentInChildren<SurfaceAudioTypeProvider>();
            if (provider != null) return provider.SurfaceType;

            string colName = col.gameObject.name.ToLowerInvariant();
            string parentName = col.transform.parent != null ? col.transform.parent.gameObject.name.ToLowerInvariant() : "";

            if (colName.Contains("ethereal") || parentName.Contains("ethereal") ||
                colName.Contains("crystal") || parentName.Contains("crystal") ||
                colName.Contains("glyph") || parentName.Contains("glyph"))
            {
                return SurfaceType.Ethereal;
            }

            if (colName.Contains("shelf") || parentName.Contains("shelf") ||
                colName.Contains("wood") || parentName.Contains("wood") ||
                colName.Contains("crate") || parentName.Contains("crate") ||
                colName.Contains("timber") || parentName.Contains("timber"))
            {
                return SurfaceType.Wood;
            }

            if (colName.Contains("metal") || parentName.Contains("metal") ||
                colName.Contains("iron") || parentName.Contains("iron") ||
                colName.Contains("grid") || parentName.Contains("grid") ||
                colName.Contains("circuit") || parentName.Contains("circuit") ||
                colName.Contains("gate") || parentName.Contains("gate"))
            {
                return SurfaceType.Metal;
            }

            if (colName.Contains("water") || parentName.Contains("water") ||
                colName.Contains("flood") || parentName.Contains("puddle"))
            {
                return SurfaceType.Water;
            }

            return SurfaceType.Stone;
        }

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
            TickFoley(Time.deltaTime);
        }

        public void TickFoley(float dt)
        {
            if (motor == null) return;

            // Skid scuff on rapid directional turn
            if (motor.IsGrounded && Mathf.Abs(motor.HorizontalSpeed) > 1.8f)
            {
                if (Mathf.Abs(motor.HorizontalInput) > 0.15f && Mathf.Sign(motor.HorizontalInput) != Mathf.Sign(motor.HorizontalSpeed))
                {
                    if (skidCooldown <= 0f)
                    {
                        skidCooldown = 0.35f;
                        SoundEffectManager.Instance?.PlaySkidScuff(GetCurrentSurface());
                    }
                }
            }
            if (skidCooldown > 0f) skidCooldown -= dt;

            // Air draft on high-speed fall
            if (!motor.IsGrounded && motor.Body != null && motor.Body.linearVelocity.y < -7.0f)
            {
                if (airDraftCooldown <= 0f)
                {
                    airDraftCooldown = 0.9f;
                    SoundEffectManager.Instance?.PlayAirDraft();
                }
            }
            if (airDraftCooldown > 0f) airDraftCooldown -= dt;
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

                    var sfx = SoundEffectManager.Instance;
                    if (sfx != null)
                    {
                        var surface = GetCurrentSurface();
                        if (isSprinting)
                        {
                            sfx.PlaySprintFootstep(surface);
                        }
                        else
                        {
                            sfx.PlayWalkFootstep(surface);
                        }
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
            var surface = GetCurrentSurface();
            SoundEffectManager.Instance?.PlayJump(surface);
        }

        private void HandleLanded(float impactSpeed)
        {
            var surface = GetCurrentSurface();
            SoundEffectManager.Instance?.PlayLanding(impactSpeed, surface);
        }

        private void HandleExhaustionChanged(bool isExhausted)
        {
            SoundEffectManager.Instance?.SetExhaustionActive(isExhausted);
        }

        private void HandleFlareChanged(bool isFlared)
        {
            if (isFlared)
            {
                SoundEffectManager.Instance?.PlayFlareIgnite();
            }
            else
            {
                SoundEffectManager.Instance?.PlayFlareExtinguish();
            }
        }

        private void HandleEmberBroken()
        {
            SoundEffectManager.Instance?.PlayEmberBreak();
        }
    }
}
