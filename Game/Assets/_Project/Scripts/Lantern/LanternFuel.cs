using System;
using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Shared fuel state for lantern modes and environmental mechanisms.
    /// Values are prototype tuning defaults, not final game balance.
    /// </summary>
    public sealed class LanternFuel : MonoBehaviour
    {
        [Header("Prototype tuning — review with the team")]
        [SerializeField, Min(0.01f)] private float capacity = 100f;
        [SerializeField, Min(0f)] private float startingFuel = 100f;
        [SerializeField, Min(0f)] private float dimDrainPerSecond;
        [SerializeField, Min(0f)] private float flareDrainPerSecond = 0.5f;

        public float Capacity => capacity;
        public float CurrentFuel { get; private set; }
        public float NormalizedFuel => capacity > 0f ? CurrentFuel / capacity : 0f;

        /// <summary>Raised when fuel changes. Arguments are current fuel and capacity.</summary>
        public event Action<float, float> FuelChanged;
        /// <summary>Raised once when fuel reaches zero. A stage failure flow can subscribe later.</summary>
        public event Action FuelDepleted;

        private void Awake()
        {
            CurrentFuel = Mathf.Clamp(startingFuel, 0f, capacity);
            FuelChanged?.Invoke(CurrentFuel, capacity);
            if (CurrentFuel <= 0f)
                FuelDepleted?.Invoke();
        }

        public bool TrySpendFuel(float amount)
        {
            if (amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), "Fuel cost cannot be negative.");
            if (CurrentFuel < amount)
                return false;
            SetFuel(CurrentFuel - amount);
            return true;
        }

        public float AddFuel(float amount)
        {
            if (amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), "Fuel recovery cannot be negative.");
            float before = CurrentFuel;
            SetFuel(CurrentFuel + amount);
            return CurrentFuel - before;
        }

        internal void TickLanternDrain(bool flared, float deltaTime)
        {
            float rate = flared ? flareDrainPerSecond : dimDrainPerSecond;
            if (rate > 0f && deltaTime > 0f)
                SetFuel(CurrentFuel - rate * deltaTime);
        }

        private void SetFuel(float value)
        {
            float before = CurrentFuel;
            CurrentFuel = Mathf.Clamp(value, 0f, capacity);
            if (Mathf.Approximately(before, CurrentFuel))
                return;

            FuelChanged?.Invoke(CurrentFuel, capacity);
            if (before > 0f && CurrentFuel <= 0f)
                FuelDepleted?.Invoke();
        }
    }
}
