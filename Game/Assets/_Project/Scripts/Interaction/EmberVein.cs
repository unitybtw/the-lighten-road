using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; 
using TheLightenRoad.Lantern;

namespace TheLightenRoad.Interaction
{
    public class EmberVein : MonoBehaviour
    {
        public enum VeinState { Drained, Powered }

        [Header("Ember Vein State")]
        public VeinState currentState = VeinState.Drained;

        [Header("Energy Cost (Charges)")]
        [Tooltip("1 Ember = 2 Charges. How many charges needed to power this machine?")]
        public int chargesRequired = 2;

        [Header("Mechanism Events")]
        public UnityEvent OnPowered;
        public UnityEvent OnDrained;

        private void Update()
        {
            // Yeni Input Sistemi ile E tuþunu dinliyoruz
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                var lantern = FindFirstObjectByType<LanternFuel>();

                if (lantern != null)
                {
                    // Mesafeyi de 3f'e çýkardýk ki objenin pivot merkezinden dolayý uzak kalma sorunu yaþamayalým
                    float distance = Vector2.Distance(transform.position, lantern.transform.position);
                    if (distance <= 3f)
                    {
                        Interact(lantern.gameObject);
                    }
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            var lantern = interactor.GetComponentInChildren<LanternFuel>();
            if (lantern == null) return;

            if (currentState == VeinState.Drained)
            {
                if (lantern.CanSpendFuel(chargesRequired))
                {
                    for (int i = 0; i < chargesRequired; i++)
                    {
                        lantern.TrySpendCharge();
                    }

                    currentState = VeinState.Powered;
                    OnPowered?.Invoke();
                    Debug.Log($"Ember Vein Powered! Spent {chargesRequired} charges.");
                }
                else
                {
                    Debug.Log("Not enough Ember charges to power this vein!");
                }
            }
            else if (currentState == VeinState.Powered)
            {
                lantern.AddFuel(chargesRequired);
                currentState = VeinState.Drained;
                OnDrained?.Invoke();
                Debug.Log($"Ember Vein Drained! Recovered {chargesRequired} charges.");
            }
        }
    }
}