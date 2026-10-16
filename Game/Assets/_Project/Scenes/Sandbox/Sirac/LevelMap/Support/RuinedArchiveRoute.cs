using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.LevelMap
{
    /// <summary>Scene-local route state. Shared player systems retain their own tuning.</summary>
    public sealed class RuinedArchiveRoute : MonoBehaviour
    {
        [SerializeField] private PlayerRespawnController player;
        [SerializeField] private Transform pickups;
        [SerializeField] private GameObject pickupPrefab;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector3 start = new Vector3(-7.5f, 0.85f);
        private Vector3[] emberPositions;
        private ArchiveRouteVolume[] volumes;
        private CanvasGroup note;
        private TextMeshProUGUI heading;
        private TextMeshProUGUI detail;
        private float noteTime;
        public PlayerRespawnController Player => player;
        public int CheckpointIndex { get; private set; }
        public bool IsComplete { get; private set; }

        public void Configure(PlayerRespawnController target, Transform emberRoot, GameObject template, TMP_FontAsset typography)
        {
            player = target;
            pickups = emberRoot;
            pickupPrefab = template;
            font = typography;
        }

        private void Start()
        {
            volumes = GetComponentsInChildren<ArchiveRouteVolume>();
            emberPositions = new Vector3[pickups.childCount];
            for (int i = 0; i < emberPositions.Length; i++) emberPositions[i] = pickups.GetChild(i).position;
            BuildNote();
            Show("THE RUINED ARCHIVE", "A/D Walk   SHIFT Sprint   SPACE Jump   F Scout   BACKSPACE Restart", 6f);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame) RestartRoute();
            noteTime = Mathf.Max(0f, noteTime - Time.unscaledDeltaTime);
            if (note != null) note.alpha = Mathf.MoveTowards(note.alpha, Mathf.Min(1f, noteTime), Time.unscaledDeltaTime * 3f);
        }

        public void Enter(ArchiveRouteVolume volume)
        {
            if (volume.Kind == ArchiveRouteVolume.VolumeKind.Checkpoint)
            {
                if (volume.CheckpointIndex <= CheckpointIndex) return;
                CheckpointIndex = volume.CheckpointIndex;
                player.SetCheckpoint(volume.Spawn);
            }
            if (volume.Kind == ArchiveRouteVolume.VolumeKind.Exit) IsComplete = true;
            volume.SetActivated(true);
            Show(volume.Heading, volume.Hint, IsComplete ? 8f : 4f);
        }

        public void RestartRoute()
        {
            if (player == null || emberPositions == null) return;
            IsComplete = false;
            CheckpointIndex = 0;
            foreach (var volume in volumes) volume.SetActivated(false);
            foreach (Transform child in pickups)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            foreach (var position in emberPositions) Instantiate(pickupPrefab, position, Quaternion.identity, pickups);
            player.SetCheckpoint(start);
            if (player.State == RespawnState.Alive) player.Kill(DeathReason.Hazard);
            player.Respawn();
            Show("ROUTE RESET", "Three embers restored. Take the upper office or the lower passage.", 4f);
        }

        private void Show(string title, string hint, float duration)
        {
            if (heading == null) return;
            heading.text = title;
            detail.text = hint;
            noteTime = duration;
        }

        private void BuildNote()
        {
            var root = new GameObject("Route prompts", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 1f;
            note = root.GetComponent<CanvasGroup>();
            note.alpha = 0f;
            note.interactable = note.blocksRaycasts = false;
            heading = Label(root.transform, "Heading", 12f, -26f, new Color32(208, 183, 138, 255));
            detail = Label(root.transform, "Hint", 9f, -43f, new Color32(159, 155, 142, 255));
        }

        private TextMeshProUGUI Label(Transform parent, string labelName, float size, float y, Color color)
        {
            var go = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(540f, 20f);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
