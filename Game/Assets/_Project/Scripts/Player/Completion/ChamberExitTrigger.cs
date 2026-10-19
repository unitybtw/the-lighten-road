using UnityEngine;

namespace TheLightenRoad.Player.Completion
{
    [RequireComponent(typeof(BoxCollider2D), typeof(ChamberCompletionSequence))]
    public sealed class ChamberExitTrigger : MonoBehaviour
    {
        [Tooltip("Optional: restrict this exit to one player. Otherwise accepts the entering player.")]
        public PlayerRespawnController Player;
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerRespawnController>();
            if (player != null && (Player == null || Player == player))
                GetComponent<ChamberCompletionSequence>().TryBegin(player);
        }
    }
}
