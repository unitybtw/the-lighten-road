using UnityEngine;

namespace TheLightenRoad.Lantern
{
    /// <summary>A reusable prototype pickup that restores lantern fuel on contact.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class EmberPickup : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fuelAmount = 20f;

        private bool collected;

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

            // Full lanterns leave the pickup available for a later return.
            if (fuel.AddFuel(fuelAmount) <= 0f)
                return;

            collected = true;
            Destroy(gameObject);
        }
    }
}
