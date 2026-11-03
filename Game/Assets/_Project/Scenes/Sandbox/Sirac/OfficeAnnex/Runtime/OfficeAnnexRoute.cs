using TheLightenRoad.Player;
using UnityEngine;

namespace TheLightenRoad.Sirac.OfficeAnnex
{
    public sealed class OfficeAnnexRoute : MonoBehaviour
    {
        public PlayerRespawnController Player;
        public AnnexPushBlock Crate;
        private void OnEnable() { if (Player != null) Player.OnPlayerRespawned += Retry; }
        private void OnDisable() { if (Player != null) Player.OnPlayerRespawned -= Retry; }
        private void Retry() { if (Crate != null) Crate.ResetForRetry(); }
    }
}
