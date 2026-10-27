using System;
using TheLightenRoad.Lantern;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Player
{
    public enum DeathReason
    {
        Darkness,
        Void,
        Hazard
    }

    public enum RespawnState
    {
        Alive,
        Dying,
        Dead,
        Respawning
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor2D))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private LanternController lantern;
        [SerializeField] private LanternFuel fuel;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private FollowCamera2D followCamera;

        [Header("Spawn & Checkpoint")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(-5f, 1f, 0f);
        [SerializeField, Min(0f)] private float respawnFuelAmount = 6f;

        [Header("Void & Hazard")]
        [SerializeField] private bool enableVoidKill = true;
        [SerializeField] private float voidKillY = -5.5f;

        [Header("Darkness survival — three seconds without an ember")]
        [SerializeField] private bool killOnFuelDepleted = true;
        public const float DarknessGraceSeconds = 3f;
        private float darknessTimer;

        [Header("Timings & Input")]
        [Tooltip("Seconds the death screen remains visible before auto-respawning.")]
        [SerializeField, Min(0.2f)] private float autoRespawnDelay = 2.4f;
        [Tooltip("Allow pressing Space, Enter, or R to respawn immediately.")]
        [SerializeField] private bool allowManualRespawnInput = true;

        private bool autoRespawnEnabled = true;
        private RespawnState state = RespawnState.Alive;
        private DeathReason lastDeathReason;
        private float stateTimer;

        public event Action<DeathReason> OnPlayerDied;
        public event Action OnPlayerRespawned;

        public RespawnState State => state;
        public DeathReason LastDeathReason => lastDeathReason;
        public Vector3 SpawnPosition { get => spawnPosition; set => spawnPosition = value; }
        public float RespawnFuelAmount { get => respawnFuelAmount; set => respawnFuelAmount = value; }
        public bool KillOnFuelDepleted { get => killOnFuelDepleted; set => killOnFuelDepleted = value; }
        public float VoidKillY { get => voidKillY; set => voidKillY = value; }
        public float AutoRespawnDelay { get => autoRespawnDelay; set => autoRespawnDelay = value; }
        public bool AllowManualRespawnInput { get => allowManualRespawnInput; set => allowManualRespawnInput = value; }
        /// <summary>
        /// When false the controller never respawns on its own; a death screen owns the decision.
        /// Defaults to true so scenes without a death screen keep their automatic respawn.
        /// </summary>
        public bool AutoRespawnEnabled { get => autoRespawnEnabled; set => autoRespawnEnabled = value; }
        public float StateTimer => stateTimer;
        public float DarknessTimer => darknessTimer;
        public float NormalizedDeathProgress => Mathf.Clamp01(stateTimer / Mathf.Max(0.001f, autoRespawnDelay));

        private void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor2D>();
            if (lantern == null)
                lantern = GetComponentInChildren<LanternController>();
            if (fuel == null)
                fuel = GetComponentInChildren<LanternFuel>();
            if (stamina == null)
                stamina = GetComponent<PlayerStamina>();
            if (followCamera == null)
                followCamera = FindAnyObjectByType<FollowCamera2D>();

            // These chamber rules override legacy serialized defaults.
            killOnFuelDepleted = true;
            respawnFuelAmount = LanternFuel.ChamberFuel;

            // Default spawn point to the starting position if not overridden
            if (spawnPosition == Vector3.zero && transform.position != Vector3.zero)
                spawnPosition = transform.position;
        }

        private void OnEnable()
        {
            GameControls.BlockGameplay(this, state != RespawnState.Alive);
            if (fuel != null)
            {
                fuel.FuelDepleted += HandleFuelDepleted;
                fuel.FuelChanged += HandleFuelChanged;
            }
        }

        private void OnDisable()
        {
            GameControls.BlockGameplay(this, false);
            if (fuel != null)
            {
                fuel.FuelDepleted -= HandleFuelDepleted;
                fuel.FuelChanged -= HandleFuelChanged;
            }
        }

        private void Update()
        {
            if (state == RespawnState.Alive)
            {
                if (enableVoidKill && transform.position.y < voidKillY)
                {
                    Kill(DeathReason.Void);
                }
                TickSurvival(Time.deltaTime);
                return;
            }

            if (state == RespawnState.Dying)
            {
                // Brief transition into fully dead
                stateTimer += Time.deltaTime;
                if (stateTimer >= 0.4f)
                {
                    state = RespawnState.Dead;
                    stateTimer = 0f;
                }
                return;
            }

            if (state == RespawnState.Dead)
            {
                stateTimer += Time.deltaTime;

                // Manual skip input
                if (allowManualRespawnInput)
                {
                    bool skip = false;
                    var kb = Keyboard.current;
                    if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame))
                        skip = true;

                    var gp = Gamepad.current;
                    if (gp != null && gp.buttonSouth.wasPressedThisFrame)
                        skip = true;

                    if (skip)
                    {
                        Respawn();
                        return;
                    }
                }

                // Automatic countdown respawn
                if (autoRespawnEnabled && stateTimer >= autoRespawnDelay)
                {
                    Respawn();
                }
            }
        }

        public void Kill(DeathReason reason)
        {
            if (state != RespawnState.Alive)
                return;

            state = RespawnState.Dying;
            GameControls.BlockGameplay(this, true);
            lastDeathReason = reason;
            stateTimer = 0f;

            // Freeze player physics and input
            if (motor != null)
            {
                motor.enabled = false;
                if (motor.Body != null)
                {
                    motor.Body.linearVelocity = Vector2.zero;
                    motor.Body.simulated = false;
                }
            }

            // Death cancels scouting; a spent ember is never refunded.
            if (lantern != null) lantern.ResetToDim();

            // Camera feedback
            if (followCamera != null)
                followCamera.Shake(0.12f, 0.4f);

            OnPlayerDied?.Invoke(reason);
        }

        public void Respawn()
        {
            if (state == RespawnState.Alive || state == RespawnState.Respawning)
                return;

            state = RespawnState.Respawning;
            stateTimer = 0f;

            // Reposition player
            transform.position = spawnPosition;

            // Restore body simulation
            if (motor != null && motor.Body != null)
            {
                motor.Body.simulated = true;
                motor.Body.linearVelocity = Vector2.zero;
            }

            // Restore lantern fuel
            if (fuel != null)
                fuel.ResetForChamber();
            darknessTimer = 0f;

            // Restore stamina
            if (stamina != null)
                stamina.ResetStamina();

            // Turn lantern back to Dim mode if needed
            if (lantern != null)
                lantern.ResetToDim();

            // Re-center camera cleanly
            if (followCamera != null)
                followCamera.SetTarget(transform, snap: true);

            OnPlayerRespawned?.Invoke();

            // Re-enable player movement
            if (motor != null)
                motor.enabled = true;

            state = RespawnState.Alive;
            GameControls.BlockGameplay(this, false);
        }

        public void SetCheckpoint(Vector3 newSpawn)
        {
            BeginChamber(newSpawn);
        }

        /// <summary>Existing prototype checkpoints mark the start of a new chamber segment.</summary>
        public void BeginChamber(Vector3 start)
        {
            spawnPosition = start;
            darknessTimer = 0f;
            if (lantern != null) lantern.ResetToDim();
            if (fuel != null) fuel.ResetForChamber();
            if (stamina != null) stamina.ResetStamina();
        }

        public void TickSurvival(float deltaTime)
        {
            if (state != RespawnState.Alive) return;
            if (fuel == null || !fuel.IsEmpty || !killOnFuelDepleted)
            {
                darknessTimer = 0f;
                return;
            }
            darknessTimer += Mathf.Max(0f, deltaTime);
            if (darknessTimer > DarknessGraceSeconds) Kill(DeathReason.Darkness);
        }

        private void HandleFuelChanged(float current, float capacity)
        {
            if (current > 0f) darknessTimer = 0f;
        }

        private void HandleFuelDepleted()
        {
            // Start timing in Update rather than killing on the depletion event.
            darknessTimer = 0f;
        }
    }
}
