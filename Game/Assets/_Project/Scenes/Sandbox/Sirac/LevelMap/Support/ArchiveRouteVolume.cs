using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;

namespace TheLightenRoad.Sirac.LevelMap
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ArchiveRouteVolume : MonoBehaviour
    {
        public enum VolumeKind { Hint, Checkpoint, Exit }
        [SerializeField] private RuinedArchiveRoute route;
        [SerializeField] private VolumeKind kind;
        [SerializeField] private string heading;
        [SerializeField] private string hint;
        [SerializeField] private int checkpointIndex;
        [SerializeField] private Vector3 spawn;
        [SerializeField] private TextMeshPro sign;
        [SerializeField] private SpriteRenderer indicator;
        private bool activated;
        private LanternController lantern;
        public VolumeKind Kind => kind;
        public string Heading => heading;
        public string Hint => hint;
        public int CheckpointIndex => checkpointIndex;
        public Vector3 Spawn => spawn;

        public void Configure(RuinedArchiveRoute owner, VolumeKind type, string title, string description, int index,
            Vector3 safeSpawn, TextMeshPro label, SpriteRenderer beacon)
        {
            route = owner;
            kind = type;
            heading = title;
            hint = description;
            checkpointIndex = index;
            spawn = safeSpawn;
            sign = label;
            indicator = beacon;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void Update()
        {
            if (sign == null || route == null || route.Player == null) return;
            if (lantern == null) lantern = route.Player.GetComponent<LanternController>();
            float radius = lantern != null ? Mathf.Lerp(lantern.DimRadius, lantern.FlareRadius, lantern.FlareBlend) : 2f;
            sign.enabled = Vector2.Distance(route.Player.transform.position, sign.transform.position) < radius + 0.5f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activated || route == null) return;
            var player = other.GetComponentInParent<PlayerRespawnController>();
            if (player == null || player != route.Player || player.State != RespawnState.Alive) return;
            route.Enter(this);
        }

        public void SetActivated(bool value)
        {
            activated = value;
            if (indicator != null) indicator.color = value ? new Color32(136, 168, 130, 255) : new Color32(176, 139, 86, 255);
        }
    }
}
