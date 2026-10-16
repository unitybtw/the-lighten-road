using UnityEngine;
using UnityEngine.Events;
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
        public int chargesRequired = 2; // Örn: 1 tam kor harcar

        [Header("Mechanism Events")]
        public UnityEvent OnPowered;
        public UnityEvent OnDrained;

        // "E" tuþu basýldýðýnda Input veya Interaction Controller'ý bu fonksiyonu tetikleyecek.
        public void Interact(GameObject interactor)
        {
            var lantern = interactor.GetComponentInChildren<LanternFuel>();
            if (lantern == null) return;

            if (currentState == VeinState.Drained)
            {
                // CanSpendFuel metodunu kullanarak yeterli þarj var mý kontrol et.
                // 1 Charge = 1.0f fuel deðerine denk geldiði için float çevrimi yapýyoruz.
                if (lantern.CanSpendFuel(chargesRequired))
                {
                    // Gereken þarj miktarý kadar TrySpendCharge döngüsü çalýþtýr
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
                // Kor damarýndan þarjlarý geri al (Drain)
                // scriptinde AddFuel kullanýlýyor. 1 charge = 1.0f fuel.
                lantern.AddFuel(chargesRequired);

                currentState = VeinState.Drained;
                OnDrained?.Invoke();
                Debug.Log($"Ember Vein Drained! Recovered {chargesRequired} charges.");
            }
        }
    }
}