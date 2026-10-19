using TheLightenRoad.Lantern;
using UnityEngine;

namespace TheLightenRoad.Sirac.FurkanPrototype
{
    /// <summary>Scene-local prototype circuit. Both nodes share one deposited ember.</summary>
    public sealed class ArchiveCircuit : MonoBehaviour
    {
        public ArchivePatrolPlatform Platform;
        public bool Powered { get; private set; }
        public bool TryTransfer(LanternFuel fuel)
        {
            if (fuel == null) return false;
            if (!Powered)
            {
                if (!fuel.TrySpendEmber()) return false;
                Powered = true;
            }
            else
            {
                // Refuse the entire transaction while full: never discard the stored ember.
                if (fuel.Capacity - fuel.CurrentFuel < LanternFuel.ChargesPerEmber) return false;
                fuel.AddEmbers(1);
                Powered = false;
            }
            if (Platform != null) Platform.Powered = Powered;
            return true;
        }
        public void ResetCircuit()
        {
            Powered = false;
            if (Platform != null) Platform.ResetPlatform();
        }
    }
}
