using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.TestChamber
{
    /// <summary>Local test-route conveniences; has no dependencies on Kemal's prototypes.</summary>
    public sealed class ChamberDirector : MonoBehaviour
    {
        [SerializeField] private PlayerRespawnController player;
        [SerializeField] private Transform pickupRoot;
        [SerializeField] private GameObject pickupPrefab;
        [SerializeField] private Vector3 startPosition;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private bool triggerCompletionSequence = true;
        [SerializeField] private ChamberCompletionSequence completionSequence;

        private Vector3[] pickupPositions;
        private ChamberMarker[] markers;
        private CanvasGroup note;
        private TextMeshProUGUI title;
        private TextMeshProUGUI hint;
        private float noteTimer;
        private int checkpointIndex;

        public PlayerRespawnController Player => player;
        public int CheckpointIndex => checkpointIndex;
        public bool IsComplete { get; private set; }
        public ChamberCompletionSequence CompletionSequence => completionSequence;
        public bool TriggerCompletionSequenceEnabled
        {
            get => triggerCompletionSequence;
            set => triggerCompletionSequence = value;
        }

        public void Configure(PlayerRespawnController target, Transform pickups, GameObject template, Vector3 start, TMP_FontAsset typography)
        {
            player = target;
            pickupRoot = pickups;
            pickupPrefab = template;
            startPosition = start;
            fontAsset = typography;
        }

        private void Start()
        {
            markers = GetComponentsInChildren<ChamberMarker>();
            pickupPositions = new Vector3[pickupRoot.childCount];
            for (int i = 0; i < pickupPositions.Length; i++)
                pickupPositions[i] = pickupRoot.GetChild(i).position;
            BuildNote();
            SpawnChamberWhispers();
            ShowNote("ARCHIVE / TEST CHAMBER", "A/D Move   SPACE Jump   F Flare   BACKSPACE Restart", 6f);
        }

        private void SpawnChamberWhispers()
        {
            DiegeticWhisper.Spawn(new Vector3(2.5f, 1.2f, 0f), "A / D", "TRAVERSE THE ARCHIVE", WhisperAction.Move, "tc_move", transform);
            DiegeticWhisper.Spawn(new Vector3(21.5f, 1.8f, 0f), "F", "HARNESS FLARE ACROSS THE VOID", WhisperAction.Flare, "tc_flare", transform);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
                RestartRoute();
            noteTimer = Mathf.Max(0f, noteTimer - Time.unscaledDeltaTime);
            if (note != null)
                note.alpha = Mathf.MoveTowards(note.alpha, noteTimer > 0f ? Mathf.Min(1f, noteTimer) : 0f,
                    Time.unscaledDeltaTime * 3f);
        }

        public void EnterMarker(ChamberMarker marker)
        {
            if (marker.Kind == ChamberMarker.MarkerKind.Checkpoint)
            {
                if (marker.CheckpointIndex <= checkpointIndex) return;
                checkpointIndex = marker.CheckpointIndex;
                player.SetCheckpoint(marker.SpawnPosition);
                marker.SetActivated(true);
                ShowNote("CHECKPOINT / " + marker.CheckpointIndex.ToString("00"), "Safe retry point recorded. Embers are restored when you respawn.");
            }
            else if (marker.Kind == ChamberMarker.MarkerKind.Exit)
            {
                IsComplete = true;
                marker.SetActivated(true);
                ShowNote("TEST ROUTE COMPLETE", "BACKSPACE to restart the route or return to the Grand Archive.", 8f);

                if (triggerCompletionSequence && player != null)
                {
                    TriggerCompletion();
                }
            }
            else
            {
                marker.SetActivated(true);
                ShowNote(marker.Title, marker.Hint);
            }
        }

        public void TriggerCompletion()
        {
            if (completionSequence == null)
            {
                completionSequence = GetComponent<ChamberCompletionSequence>();
                if (completionSequence == null)
                {
                    completionSequence = gameObject.AddComponent<ChamberCompletionSequence>();
                    var profile = ScriptableObject.CreateInstance<ChamberCompletionProfile>();
                    profile.Eyebrow = "CHAMBER CONQUERED";
                    profile.Title = "ARCHIVE TEST CHAMBER";
                    profile.Subtitle = "ALL PROTOCOL TESTS EXECUTED AND MASTERED";
                    profile.Font = fontAsset;
                    profile.LightColor = new Color(1f, 0.74f, 0.35f);
                    profile.LightRadius = 12f;
                    profile.LightIntensity = 1.4f;
                    profile.RevealSeconds = 1.1f;
                    profile.HoldSeconds = 1.3f;
                    profile.FadeOutSeconds = 0.8f;
                    profile.FadeInSeconds = 0.8f;
                    completionSequence.Profile = profile;
                    completionSequence.DestinationScene = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
                }
            }

            if (!completionSequence.IsRunning && player != null)
            {
                completionSequence.TryBegin(player);
            }
        }

        public void RestartRoute()
        {
            if (player == null || pickupPositions == null) return;
            checkpointIndex = 0;
            IsComplete = false;
            foreach (ChamberMarker marker in markers) marker.SetActivated(false);
            foreach (Transform child in pickupRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            foreach (Vector3 position in pickupPositions)
                Instantiate(pickupPrefab, position, Quaternion.identity, pickupRoot);
            player.SetCheckpoint(startPosition);
            if (player.State == RespawnState.Alive) player.Kill(DeathReason.Hazard);
            player.Respawn();
            ShowNote("TEST ROUTE RESET", "All checkpoints and pickups reset. A/D Move   SPACE Jump   F Flare");
        }

        private void ShowNote(string heading, string detail, float seconds = 4f)
        {
            if (title == null) return;
            title.text = heading;
            hint.text = detail;
            noteTimer = seconds;
        }

        private void BuildNote()
        {
            var go = new GameObject("Test route prompts", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scale = go.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(960f, 540f);
            scale.matchWidthOrHeight = 1f;
            var card = new GameObject("Route note", typeof(RectTransform), typeof(CanvasGroup));
            card.transform.SetParent(go.transform, false);
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 22f);
            rect.sizeDelta = new Vector2(580f, 58f);
            note = card.GetComponent<CanvasGroup>();
            note.blocksRaycasts = false;
            note.interactable = false;
            note.alpha = 0f;
            var scrim = new GameObject("Contrast", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(card.transform, false);
            var scrimRect = (RectTransform)scrim.transform;
            scrimRect.anchorMin = Vector2.zero;
            scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = scrimRect.offsetMax = Vector2.zero;
            var image = scrim.GetComponent<Image>();
            image.sprite = PixelSprites.Glow;
            image.color = new Color(0f, 0f, 0f, 0.8f);
            image.raycastTarget = false;
            title = Label("Heading", card.transform, new Vector2(0f, 27f), 12f, new Color32(235, 197, 137, 255));
            title.characterSpacing = 2f;
            hint = Label("Hint", card.transform, new Vector2(0f, 6f), 9f, new Color32(193, 195, 188, 255));
        }

        private TextMeshProUGUI Label(string name, Transform parent, Vector2 position, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(580f, 20f);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
