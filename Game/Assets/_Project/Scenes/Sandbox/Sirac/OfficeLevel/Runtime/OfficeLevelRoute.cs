using TheLightenRoad.Player;
using TheLightenRoad.Sirac.OfficeAnnex;
using UnityEngine;

namespace TheLightenRoad.Sirac.OfficeLevel
{
    public sealed class OfficeLevelRoute : MonoBehaviour
    {
        public PlayerRespawnController Player;
        public AnnexPushBlock Crate;
        private void OnEnable() { if (Player != null) Player.OnPlayerRespawned += ResetRoute; }
        private void OnDisable() { if (Player != null) Player.OnPlayerRespawned -= ResetRoute; }
        private void ResetRoute() { if (Crate != null) Crate.ResetForRetry(); }
    }
}
