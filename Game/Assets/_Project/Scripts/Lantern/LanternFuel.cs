using System;
using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Shared fuel and ember charge state.
    /// Each ember has 2 charges (Full = 2, Half = 1, Expended = 0).
    /// The inventory holds at most four embers. Legacy charge APIs remain available
    /// for mechanism compatibility: two charges always represent one whole ember.
    /// </summary>
    public sealed class LanternFuel : MonoBehaviour, ISocialListener
    {
        [Header("Ember & Fuel Tuning")]
        [SerializeField, Min(0.01f)] private float capacity = 8f; // Four embers, two legacy charges each
        [SerializeField, Min(0f)] private float startingFuel = 6f; // 3 Embers = 6 charges by default
        [SerializeField, Min(0f)] private float dimDrainPerSecond = 0f; // Infinite in Dim mode
        [SerializeField, Min(0f)] private float flareDrainPerSecond = 0f;
        [SerializeField] private bool allowOverflowCapacity = false;

        public const int MaxEmbers = 4;
        public const int StartingEmbers = 3;
        public const int ChargesPerEmber = 2;
        public const float ChamberFuel = StartingEmbers * ChargesPerEmber;
        public const float MaximumFuel = MaxEmbers * ChargesPerEmber;
        public float Capacity => MaximumFuel;
        public float CurrentFuel { get; private set; }
        public float NormalizedFuel => capacity > 0f ? CurrentFuel / capacity : 0f;
        public bool IsFull => CurrentFuel >= MaximumFuel;
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
            capacity = MaximumFuel;
            startingFuel = ChamberFuel;
            allowOverflowCapacity = false;
            CurrentFuel = ChamberFuel;
            FuelChanged?.Invoke(CurrentFuel, capacity);
            if (CurrentFuel <= 0f)
                FuelDepleted?.Invoke();
        }

        private SocialChannel loreChannel;
        private readonly System.Collections.Generic.HashSet<string> rewardedLogs=new System.Collections.Generic.HashSet<string>();
        private void OnEnable()
        {
            TheLightenRoad.Codex.CodexSession.Ensure();
            loreChannel=TheLightenRoad.Codex.CodexSession.Channel;
            if(loreChannel!=null)loreChannel.Subscribe(this);
        }
        private void OnDisable(){if(loreChannel!=null)loreChannel.Unsubscribe(this);loreChannel=null;}
        public void OnSocialEvent(SocialEvent e)
        {
            if(e.isReplay || e.kind!=SocialEventKind.LogFound || string.IsNullOrEmpty(e.text))return;
            if(TheLightenRoad.Codex.CodexSession.Catalog?.Find(e.text)==null || !rewardedLogs.Add(e.text))return;
            AddEmbers(1);
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
            capacity = MaximumFuel;
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
            CurrentFuel = Mathf.Clamp(value, 0f, MaximumFuel);
            if (Mathf.Approximately(before, CurrentFuel))
                return;

            FuelChanged?.Invoke(CurrentFuel, capacity);
            if (before > 0f && CurrentFuel <= 0f)
                FuelDepleted?.Invoke();
        }
    }
}
