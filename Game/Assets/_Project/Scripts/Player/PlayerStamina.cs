using System;
using UnityEngine;

namespace TheLightenRoad.Player
{
    /// <summary>
    /// Manages player endurance for sprinting and athletic jumps.
    /// Walking is free and atmospheric; sprinting expends stamina.
    /// When depleted, player is exhausted and must catch their breath before sprinting again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [Header("Stamina Tuning")]
        [SerializeField, Min(10f)] private float maxStamina = 100f;
        [Tooltip("Stamina drained per second while sprinting on the ground.")]
        [SerializeField, Min(1f)] private float sprintDrainPerSecond = 20f;
        [Tooltip("Stamina recovered per second when not sprinting.")]
        [SerializeField, Min(1f)] private float staminaRegenPerSecond = 25f;
        [Tooltip("Delay in seconds after sprinting before stamina starts regenerating.")]
        [SerializeField, Min(0f)] private float regenDelayAfterSprint = 0.5f;
        [Tooltip("Stamina cost deducted when launching a sprint-jump.")]
        [SerializeField, Min(0f)] private float jumpStaminaCost = 10f;
        [Tooltip("Normalized fraction (e.g. 0.25 = 25%) required to recover from exhaustion.")]
        [SerializeField, Range(0.05f, 0.5f)] private float exhaustionRecoveryThreshold = 0.25f;

        private float currentStamina;
        private float regenDelayTimer;
        private bool isExhausted;

        /// <summary>Invoked whenever current stamina or max stamina changes: (current, max).</summary>
        public event Action<float, float> StaminaChanged;

        /// <summary>Invoked when entering (true) or exiting (false) the exhausted state.</summary>
        public event Action<bool> ExhaustionChanged;

        public float MaxStamina => maxStamina;
        public float CurrentStamina => currentStamina;
        public float NormalizedStamina => maxStamina > 0.001f ? currentStamina / maxStamina : 0f;
        public bool IsExhausted => isExhausted;
        public bool CanSprint => !isExhausted && currentStamina > 0.01f;
        public float SprintDrainRate => sprintDrainPerSecond;
        public float RegenRate => staminaRegenPerSecond;
        public float JumpCost => jumpStaminaCost;
        public float ExhaustionThreshold => exhaustionRecoveryThreshold;

        private void Awake()
        {
            currentStamina = maxStamina;
        }

        private void Start()
        {
            StaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        /// <summary>
        /// Drains stamina while sprinting. Call from FixedUpdate with deltaTime.
        /// </summary>
        public void ConsumeSprint(float dt)
        {
            if (dt <= 0f) return;

            float previous = currentStamina;
            currentStamina = Mathf.Max(0f, currentStamina - sprintDrainPerSecond * dt);
            regenDelayTimer = regenDelayAfterSprint;

            if (currentStamina <= 0.01f && !isExhausted)
            {
                isExhausted = true;
                ExhaustionChanged?.Invoke(true);
            }

            if (!Mathf.Approximately(previous, currentStamina))
            {
                StaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        /// <summary>
        /// Consumes stamina for a sprint jump if available.
        /// </summary>
        public bool TryConsumeJump()
        {
            if (jumpStaminaCost <= 0f) return true;

            float previous = currentStamina;
            currentStamina = Mathf.Max(0f, currentStamina - jumpStaminaCost);
            regenDelayTimer = regenDelayAfterSprint;

            if (currentStamina <= 0.01f && !isExhausted)
            {
                isExhausted = true;
                ExhaustionChanged?.Invoke(true);
            }

            if (!Mathf.Approximately(previous, currentStamina))
            {
                StaminaChanged?.Invoke(currentStamina, maxStamina);
            }

            return true;
        }

        /// <summary>
        /// Recharges stamina when not sprinting. Call from FixedUpdate with deltaTime.
        /// </summary>
        public void Regenerate(float dt)
        {
            if (dt <= 0f) return;

            if (regenDelayTimer > 0f)
            {
                float delayTime = Mathf.Min(regenDelayTimer, dt);
                regenDelayTimer -= delayTime;
                dt -= delayTime;
                if (dt <= 0.0001f)
                    return;
            }

            if (currentStamina >= maxStamina)
                return;

            float previous = currentStamina;
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * dt);

            if (isExhausted && currentStamina >= maxStamina * exhaustionRecoveryThreshold)
            {
                isExhausted = false;
                ExhaustionChanged?.Invoke(false);
            }

            if (!Mathf.Approximately(previous, currentStamina))
            {
                StaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        /// <summary>
        /// Restores stamina to full and clears exhaustion (used on respawn / level reset).
        /// </summary>
        public void ResetStamina()
        {
            currentStamina = maxStamina;
            regenDelayTimer = 0f;
            bool wasExhausted = isExhausted;
            isExhausted = false;

            if (wasExhausted)
            {
                ExhaustionChanged?.Invoke(false);
            }

            StaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        /// <summary>
        /// Explicit initialization for tests.
        /// </summary>
        public void Initialize(float current, float max)
        {
            maxStamina = Mathf.Max(1f, max);
            currentStamina = Mathf.Clamp(current, 0f, maxStamina);
            isExhausted = currentStamina <= 0.01f;
            regenDelayTimer = 0f;
            StaminaChanged?.Invoke(currentStamina, maxStamina);
            ExhaustionChanged?.Invoke(isExhausted);
        }
    }
}
