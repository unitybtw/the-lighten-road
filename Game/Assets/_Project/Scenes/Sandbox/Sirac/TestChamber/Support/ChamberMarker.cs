using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;

namespace TheLightenRoad.Sirac.TestChamber
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ChamberMarker : MonoBehaviour
    {
        public enum MarkerKind { Zone, Checkpoint, Exit }
        [SerializeField] private ChamberDirector director;
        [SerializeField] private MarkerKind kind;
        [SerializeField] private string title;
        [SerializeField] private string hint;
        [SerializeField] private int checkpointIndex;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private TextMeshPro sign;
        [SerializeField] private SpriteRenderer indicator;
        private bool activated;
        private LanternController lantern;
        public MarkerKind Kind => kind;
        public int CheckpointIndex => checkpointIndex;
        public Vector3 SpawnPosition => spawnPosition;
        public string Title => title;
        public string Hint => hint;

        public void Configure(ChamberDirector owner, MarkerKind type, string heading, string description,
            int index, Vector3 spawn, TextMeshPro label, SpriteRenderer light)
        {
            director = owner;
            kind = type;
            title = heading;
            hint = description;
            checkpointIndex = index;
            spawnPosition = spawn;
            sign = label;
            indicator = light;
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void Update()
        {
            if (sign == null || director == null || director.Player == null) return;
            if (lantern == null) lantern = director.Player.GetComponent<LanternController>();
            float radius = lantern != null && lantern.IsFlared ? lantern.FlareRadius : 3.2f;
            sign.enabled = Vector2.Distance(director.Player.transform.position, sign.transform.position) <= radius;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activated || director == null) return;
            var player = other.GetComponentInParent<PlayerRespawnController>();
            if (player != director.Player || player == null || player.State != RespawnState.Alive) return;
            director.EnterMarker(this);
        }

        public void SetActivated(bool value)
        {
            activated = value;
            if (indicator != null)
                indicator.color = value ? new Color32(116, 204, 183, 255) : new Color32(194, 155, 95, 255);
        }
    }
}
