using UnityEngine;

namespace TheLightenRoad.Player
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class HazardTrigger2D : MonoBehaviour
    {
        [SerializeField] private DeathReason deathReason = DeathReason.Hazard;

        public DeathReason Reason => deathReason;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var respawn = other.GetComponentInParent<PlayerRespawnController>();
            if (respawn != null)
            {
                respawn.Kill(deathReason);
            }
        }
    }
}
