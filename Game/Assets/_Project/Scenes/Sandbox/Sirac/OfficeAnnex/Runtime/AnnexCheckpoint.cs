using TheLightenRoad.Player;
using UnityEngine;

namespace TheLightenRoad.Sirac.OfficeAnnex
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class AnnexCheckpoint : MonoBehaviour
    {
        public Vector3 Spawn;
        private bool visited;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerRespawnController>();
            if (visited || player == null) return;
            visited = true;
            player.BeginChamber(Spawn);
        }
    }
}
