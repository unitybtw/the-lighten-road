using System;
using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Shared fuel and ember charge state.
    /// Each ember has 2 charges (Full = 2, Half = 1, Expended = 0).
    /// Dim light is an infinite baseline resource; embers provide charges for Flare scouting
    /// and machinery interaction.
    /// </summary>
    public sealed class LanternFuel : MonoBehaviour
    {
        [Header("Ember & Fuel Tuning")]
        [SerializeField, Min(0.01f)] private float capacity = 10f; // 5 Embers = 10 charges max standard HUD
        [SerializeField, Min(0f)] private float startingFuel = 6f; // 3 Embers = 6 charges by default
        [SerializeField, Min(0f)] private float dimDrainPerSecond = 0f; // Infinite in Dim mode
        [SerializeField, Min(0f)] private float flareDrainPerSecond = 0f;
        [SerializeField] private bool allowOverflowCapacity = true;

        public float Capacity => capacity;
        public float CurrentFuel { get; private set; }
        public float NormalizedFuel => capacity > 0f ? CurrentFuel / capacity : 0f;
        public bool IsFull => CurrentFuel >= capacity;
        public bool IsEmpty => CurrentFuel <= 0f;
        public bool AllowOverflowCapacity { get => allowOverflowCapacity; set => allowOverflowCapacity = value; }

        public float StartingFuel => startingFuel;
        public float DimDrainPerSecond => dimDrainPerSecond;
        public float FlareDrainPerSecond => flareDrainPerSecond;

        // Discrete Ember Inventory Properties (Game Designer Furkan's Feedback)
        public int TotalCharges => Mathf.RoundToInt(CurrentFuel);
        public int TotalEmbers => Mathf.CeilToInt(CurrentFuel / 2f);
        public int FullEmbers => Mathf.FloorToInt(CurrentFuel / 2f);
        public bool HasHalfEmber => (Mathf.RoundToInt(CurrentFuel) % 2) == 1;
        public int OverflowEmbers => Mathf.Max(0, TotalEmbers - 5);

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

        public bool CanSpendFuel(float amount)
        {
            return amount >= 0f && CurrentFuel >= amount;
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

        public bool TrySpendCharge()
        {
            return TrySpendFuel(1f);
        }

        public float AddFuel(float amount)
        {
            if (amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), "Fuel recovery cannot be negative.");

            // Never reject picking up embers: automatically expand capacity to hold inventory
            if (allowOverflowCapacity && CurrentFuel + amount > capacity)
                capacity = CurrentFuel + amount;

            float before = CurrentFuel;
            SetFuel(CurrentFuel + amount);
            return CurrentFuel - before;
        }

        public void AddEmbers(int count)
        {
            AddFuel(count * 2f);
        }

        public void Initialize(float current, float maxCapacity)
        {
            if (maxCapacity <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be positive.");
            capacity = maxCapacity;
            SetFuel(current);
        }

        public void SetDrainRates(float dimDrain, float flareDrain)
        {
            dimDrainPerSecond = Mathf.Max(0f, dimDrain);
            flareDrainPerSecond = Mathf.Max(0f, flareDrain);
        }

        public void TickLanternDrain(bool flared, float deltaTime)
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
