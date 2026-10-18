using System;
using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Shared fuel and ember charge state.
    /// Each ember has 2 charges (Full = 2, Half = 1, Expended = 0).
    /// The inventory holds at most three embers. Legacy charge APIs remain available
    /// for mechanism compatibility: two charges always represent one whole ember.
    /// </summary>
    public sealed class LanternFuel : MonoBehaviour
    {
        [Header("Ember & Fuel Tuning")]
        [SerializeField, Min(0.01f)] private float capacity = 6f; // Three embers, two legacy charges each
        [SerializeField, Min(0f)] private float startingFuel = 6f; // 3 Embers = 6 charges by default
        [SerializeField, Min(0f)] private float dimDrainPerSecond = 0f; // Infinite in Dim mode
        [SerializeField, Min(0f)] private float flareDrainPerSecond = 0f;
        [SerializeField] private bool allowOverflowCapacity = false;

        public const int MaxEmbers = 3;
        public const int ChargesPerEmber = 2;
        public const float ChamberFuel = MaxEmbers * ChargesPerEmber;
        public float Capacity => ChamberFuel;
        public float CurrentFuel { get; private set; }
        public float NormalizedFuel => capacity > 0f ? CurrentFuel / capacity : 0f;
        public bool IsFull => CurrentFuel >= ChamberFuel;
        public bool IsEmpty => CurrentFuel <= 0f;
        // Kept for older callers; overflow is forbidden by the current chamber rules.
        public bool AllowOverflowCapacity { get => false; set => allowOverflowCapacity = false; }

        public float StartingFuel => startingFuel;
        public float DimDrainPerSecond => dimDrainPerSecond;
        public float FlareDrainPerSecond => flareDrainPerSecond;

        // Discrete Ember Inventory Properties (Game Designer Furkan's Feedback)
        public int TotalCharges => Mathf.RoundToInt(CurrentFuel);
        public int TotalEmbers => Mathf.CeilToInt(CurrentFuel / 2f);
        public int FullEmbers => Mathf.FloorToInt(CurrentFuel / 2f);
        public bool HasHalfEmber => (Mathf.RoundToInt(CurrentFuel) % 2) == 1;
        public int OverflowEmbers => 0;

        /// <summary>Raised when fuel changes. Arguments are current fuel and capacity.</summary>
        public event Action<float, float> FuelChanged;
        /// <summary>Raised once when fuel reaches zero. A stage failure flow can subscribe later.</summary>
        public event Action FuelDepleted;

        private void Awake()
        {
            capacity = startingFuel = ChamberFuel;
            allowOverflowCapacity = false;
            CurrentFuel = ChamberFuel;
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

        public bool TrySpendEmber() => TrySpendFuel(ChargesPerEmber);

        public void ResetForChamber() => Initialize(ChamberFuel, ChamberFuel);

        public float AddFuel(float amount)
        {
            if (amount < 0f)
                throw new ArgumentOutOfRangeException(nameof(amount), "Fuel recovery cannot be negative.");


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
            // Ignore old serialized/prototype capacities above the new hard limit.
            capacity = ChamberFuel;
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
            CurrentFuel = Mathf.Clamp(value, 0f, ChamberFuel);
            if (Mathf.Approximately(before, CurrentFuel))
                return;

            FuelChanged?.Invoke(CurrentFuel, capacity);
            if (before > 0f && CurrentFuel <= 0f)
                FuelDepleted?.Invoke();
        }
    }
}
