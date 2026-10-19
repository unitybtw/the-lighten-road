using UnityEngine;

namespace TheLightenRoad.Sirac.FurkanPrototype
{
    public sealed class ArchiveCircuitNode : MonoBehaviour
    {
        public ArchiveCircuit Circuit;
        public SpriteRenderer Core;
        private void Update()
        {
            if (Core != null) Core.color = Circuit != null && Circuit.Powered
                ? new Color(.95f,.65f,.24f) : new Color(.35f,.59f,.64f);
        }
    }
}
