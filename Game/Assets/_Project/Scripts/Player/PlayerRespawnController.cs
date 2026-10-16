using System;
using TheLightenRoad.Lantern;
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

        [Header("Darkness Death (Disabled by default: lantern baseline light is infinite)")]
        [SerializeField] private bool killOnFuelDepleted;

        [Header("Timings & Input")]
        [Tooltip("Seconds the death screen remains visible before auto-respawning.")]
        [SerializeField, Min(0.2f)] private float autoRespawnDelay = 2.4f;
        [Tooltip("Allow pressing Space, Enter, or R to respawn immediately.")]
        [SerializeField] private bool allowManualRespawnInput = true;

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
        public float StateTimer => stateTimer;
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

            // Default spawn point to the starting position if not overridden
            if (spawnPosition == Vector3.zero && transform.position != Vector3.zero)
                spawnPosition = transform.position;
        }

        private void OnEnable()
        {
            if (fuel != null)
                fuel.FuelDepleted += HandleFuelDepleted;
        }

        private void OnDisable()
        {
            if (fuel != null)
                fuel.FuelDepleted -= HandleFuelDepleted;
        }

        private void Update()
        {
            if (state == RespawnState.Alive)
            {
                if (enableVoidKill && transform.position.y < voidKillY)
                {
                    Kill(DeathReason.Void);
                }
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
                if (stateTimer >= autoRespawnDelay)
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
                fuel.Initialize(respawnFuelAmount, fuel.Capacity);

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
        }

        public void SetCheckpoint(Vector3 newSpawn)
        {
            spawnPosition = newSpawn;
        }

        private void HandleFuelDepleted()
        {
            if (killOnFuelDepleted)
                Kill(DeathReason.Darkness);
        }
    }
}
