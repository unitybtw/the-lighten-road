using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>
    /// Reusable ember pickup that adds an ember crystal to the player's inventory on contact.
    /// In accordance with Game Designer Furkan's feedback, embers are always collected
    /// and added into inventory (stacked into the top-left HUD).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class EmberPickup : MonoBehaviour
    {
        [Tooltip("Amount of fuel/charges restored (default 2 = 1 full ember).")]
        [SerializeField, Min(0f)] private float fuelAmount = 2f;

        private bool collected;

        public float FuelAmount => fuelAmount;
        public bool IsCollected => collected;

        public void SetFuelAmount(float amount)
        {
            fuelAmount = Mathf.Max(0f, amount);
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected)
                return;

            var fuel = other.GetComponentInParent<LanternFuel>();
            if (fuel == null)
                return;

            // Always collect ember and add to inventory
            fuel.AddFuel(fuelAmount);

            collected = true;
            Destroy(gameObject);
        }
    }
}
