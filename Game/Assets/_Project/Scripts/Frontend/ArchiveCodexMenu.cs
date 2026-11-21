using System.Collections.Generic;
using System.Linq;
using TheLightenRoad.Audio;
using TheLightenRoad.Codex;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// 2D Pixel-Art Field Archive Codex reader.
    /// Pauses gameplay over a darkened world vignette, animates a 6-frame 2D pixel book
    /// opening & page turn, and provides right-edge archival bookmark tabs for direct entry switching.
    /// </summary>
    public sealed class ArchiveCodexMenu : MonoBehaviour, ISocialListener
    {
        public static bool IsOpen { get; private set; }
        private static int closedFrame = -10;
        public static bool ClosedThisFrame => Time.frameCount <= closedFrame + 1;

        private readonly List<string> entries = new List<string>();
        private readonly List<Button> bookmarkButtons = new List<Button>();
        private readonly List<Image> bookmarkImages = new List<Image>();
        private readonly List<TMP_Text> bookmarkLabels = new List<TMP_Text>();
        private readonly Dictionary<string, List<string>> paginatedText = new Dictionary<string, List<string>>();

        private SocialChannel channel;
        private GameObject panel, footerPanel, recoveryFeedCanvas;
        private TMP_Text progress, title, body, clue, pages, location, shortcuts;
        private CanvasGroup readingGroup;
        private CanvasGroup copyGroup;
        private RectTransform decryptionFill;
        private float revealTime;
        private ArchiveBookPresentation presentation;
        private TMP_FontAsset handwriting;
        private bool opening, ownsPause;

        public bool IsAnimating => opening || (presentation != null && presentation.Turn != null && presentation.Turn.IsBusy);
        public const float OpeningDuration = 0.85f;
        private const int ReaderLayoutVersion = 3;
        [SerializeField, HideInInspector] private int builtLayoutVersion;

        private Button previous, next, close, voiceToggle, voiceReplay;
        private TMP_Text voiceStatus, voiceToggleCaption, voiceTime;
        private RectTransform voiceFill;
        private Button[] readingButtons;
        private int navigationMask = -1;

        private bool narrationPaused;
        public bool IsNarrationPaused => narrationPaused;
        private AudioSource audioSource;
        private bool narrationShouldResume;
        private float narrationLastTime;
        private ArchiveNarration narration;
        private ArchiveLog narrationLog;
        private bool narrationRequested;
        public bool IsNarrationPlaying => audioSource != null && audioSource.isPlaying;

        private int selected, page;
        private float previousTime;
        private GameObject previousSelection;
        public int EntryCount => entries.Count;
        public int SelectedEntryIndex => selected;
        public int BookmarkTabCount => bookmarkButtons.Count;
        [SerializeField, HideInInspector] private bool deferPresentation;
        public bool PresentationCreated => panel != null && presentation != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            closedFrame = -10;
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
        }

        private static void Install(Scene scene, LoadSceneMode mode)
        {
            bool hasPlayer = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<ArchiveCodexMenu>(true) != null) return;
                if (root.GetComponentInChildren<PlayerMotor2D>() != null) hasPlayer = true;
            }
            if (hasPlayer) CreateStandby(scene);
        }

        public static ArchiveCodexMenu CreateStandby() => CreateStandby(SceneManager.GetActiveScene());

        private static ArchiveCodexMenu CreateStandby(Scene scene)
        {
            var root = new GameObject("Archive Codex");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var reader = root.AddComponent<ArchiveCodexMenu>();
            reader.deferPresentation = true;
            root.SetActive(true);
            return reader;
        }

        private void Awake()
        {
            if (!deferPresentation) Build();
        }

        private void Start()
        {
            if (!PresentationCreated) return;
            var catalog = CodexSession.Catalog;
            if (catalog != null && catalog == Resources.Load<ArchiveCatalog>("Codex/ArchiveCatalog"))
            {
                foreach (var log in catalog.Logs) narration.Prepare(log);
            }
        }

        private void OnEnable()
        {
            if (!deferPresentation || PresentationCreated) EnsurePresentation();
            CodexSession.Ensure();
            channel = CodexSession.Channel;
            entries.Clear();
            EnsureRecoveryFeed();
            if (channel != null) channel.Subscribe(this);
            CodexSession.PageRequested += Open;
            ControllerGlyphService.OnPromptStyleChanged += RefreshShortcuts;
            AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
            AudioOutputRecovery.OnAudioOutputReset += HandleAudioOutputReset;
            Redraw();
            RefreshShortcuts();
        }

        private void EnsurePresentation()
        {
            if (panel == null && presentation == null && builtLayoutVersion == 0) Build();
            else if (builtLayoutVersion != ReaderLayoutVersion || !PresentationCreated || !presentation.IsInitialized) RebuildAfterReload();
            presentation.Rebind(CanTurn, ApplyTurn);
        }

        private void RebuildAfterReload()
        {
            Close();
            foreach (var child in transform.Cast<Transform>().ToArray()) DestroyImmediate(child.gameObject);
            foreach (var component in GetComponents<MonoBehaviour>().Where(c => c != this).ToArray()) DestroyImmediate(component);
            foreach (var source in GetComponents<AudioSource>()) DestroyImmediate(source);
            bookmarkButtons.Clear();
            bookmarkImages.Clear();
            bookmarkLabels.Clear();
            paginatedText.Clear();
            navigationMask = -1;
            Build();
        }

        private void OnDisable()
        {
            Close();
            if (channel != null) channel.Unsubscribe(this);
            CodexSession.PageRequested -= Open;
            ControllerGlyphService.OnPromptStyleChanged -= RefreshShortcuts;
            AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
            AudioOutputRecovery.OnAudioOutputReset -= HandleAudioOutputReset;
        }

        private void HandleAudioOutputReset() => HandleAudioConfigurationChanged(true);

        private void HandleAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!IsOpen || narrationPaused || narrationRequested || !narrationShouldResume || audioSource == null || audioSource.clip == null)
            {
                return;
            }

            if (!audioSource.isPlaying)
            {
                audioSource.volume = MenuPreferences.Effects;
                float resumeTime = (audioSource.clip.length > 0.1f && narrationLastTime > 0f && narrationLastTime < audioSource.clip.length - 0.05f)
                    ? narrationLastTime
                    : 0f;
                audioSource.time = resumeTime;
                audioSource.Play();
            }
        }

        public void OnSocialEvent(SocialEvent e)
        {
            if (e.kind != SocialEventKind.LogFound || CodexSession.Catalog?.Find(e.text) == null) return;
            if (!entries.Contains(e.text)) entries.Add(e.text);
            Redraw();
        }

        public void Open(string id)
        {
            int index = entries.IndexOf(id);
            if (index < 0 || (IsOpen && !ownsPause)) return;
            if (!ownsPause && GameControls.HasGameplayBlockers) return;

            EnsurePresentation();
            bool wasOpen = IsOpen;
            selected = index;
            page = 0;

            if (!IsOpen)
            {
                previousTime = Time.timeScale;
                previousSelection = EventSystem.current?.currentSelectedGameObject;
                Time.timeScale = 0f;
                GameControls.BlockGameplay(this, true);
                LegacyInteractionPollingGate.Acquire(this);
                IsOpen = true;
                ownsPause = true;
            }

            presentation.Turn.Cancel();
            panel.SetActive(true);
            footerPanel.SetActive(true);
            presentation.Show(true);
            revealTime = 0f;
            opening = !wasOpen && !MenuPreferences.ReducedMotion;
            Redraw();
            RefreshShortcuts();
            AnimateBook(0f);
            QueueNarration(CodexSession.Catalog.Find(id));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        public void Close()
        {
            if (!PresentationCreated && !ownsPause) return;
            if (panel != null) panel.SetActive(false);
            if (footerPanel != null) footerPanel.SetActive(false);
            if (presentation != null) presentation.Show(false);

            opening = false;
            narrationRequested = false;
            narrationPaused = false;
            narrationShouldResume = false;
            narrationLastTime = 0f;
            narrationLog = null;
            if (!ownsPause) return;

            if (audioSource != null) audioSource.Stop();
            ownsPause = false;
            Time.timeScale = previousTime;
            GameControls.BlockGameplay(this, false);
            LegacyInteractionPollingGate.Release(this);
            IsOpen = false;
            closedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        public void SelectEntry(int targetIndex)
        {
            if (!IsOpen || opening || presentation == null || presentation.Turn.IsBusy) return;
            if (targetIndex < 0 || targetIndex >= entries.Count || targetIndex == selected) return;

            int dir = targetIndex > selected ? 1 : -1;
            audioSource.Stop();
            narrationRequested = false;
            narrationPaused = false;

            presentation.Turn.AutomaticTo(dir, () =>
            {
                if (targetIndex < 0 || targetIndex >= entries.Count) return;
                selected = targetIndex;
                page = 0;
                var current = CodexSession.Catalog.Find(entries[selected]);
                QueueNarration(current);
                Redraw();
            });
        }

        private void Update()
        {
            if (!PresentationCreated)
            {
                var keyboard = Keyboard.current;
                var gamepad = GameControls.ActivePad;
                if (GameControls.GameplayAllowed && entries.Count > 0 &&
                    ((keyboard != null && keyboard.tabKey.wasPressedThisFrame) || (gamepad != null && gamepad.selectButton.wasPressedThisFrame)))
                {
                    Open(entries[Mathf.Clamp(selected, 0, entries.Count - 1)]);
                }
                return;
            }

            if (IsOpen && !ownsPause) return;

            if (IsOpen)
            {
                AnimateBook(Time.unscaledDeltaTime);
                audioSource.volume = MenuPreferences.Effects;
                if (narrationRequested)
                {
                    var clip = audioSource.clip != null ? audioSource.clip : narration.GetReadyClip(narrationLog);
                    if (clip != null)
                    {
                        audioSource.clip = clip;
                        if (!narrationPaused && !IsAnimating)
                        {
                            audioSource.Play();
                            narrationRequested = false;
                            narrationShouldResume = true;
                            narrationLastTime = 0f;
                        }
                    }
                    else if (!narration.IsPending(narrationLog))
                    {
                        narrationRequested = false;
                    }
                }
                if (audioSource.isPlaying)
                {
                    narrationShouldResume = true;
                    narrationLastTime = audioSource.time;
                }
                else if (!narrationRequested && !narrationPaused)
                {
                    // If Unity's audio driver silently stopped playback mid-clip (e.g., AudioSettings.Reset
                    // without firing OnAudioConfigurationChanged), audioSource.time resets to 0 while
                    // narrationLastTime is still well before the clip end. Resume instead of marking complete.
                    if (narrationShouldResume && audioSource.clip != null && audioSource.clip.length > 0.5f &&
                        audioSource.time < 0.01f && narrationLastTime > 0.05f && narrationLastTime < audioSource.clip.length - 0.25f)
                    {
                        HandleAudioConfigurationChanged(false);
                    }
                    else
                    {
                        narrationShouldResume = false;
                    }
                }
                RefreshNarrationControls();
            }

            var kb = Keyboard.current;
            var pad = GameControls.ActivePad;
            if (IsOpen)
            {
                if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) ||
                    (pad != null && pad.buttonEast.wasPressedThisFrame))
                {
                    Close();
                    return;
                }

                if (opening && ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) ||
                                (pad != null && pad.buttonSouth.wasPressedThisFrame)))
                {
                    revealTime = OpeningDuration;
                    AnimateBook(0f);
                    return;
                }

                if (!IsAnimating)
                {
                    if ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) ToggleNarration();
                    if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) ReplayNarration();
                }

                if ((kb != null && kb.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) Turn(-1);
                if ((kb != null && kb.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) Turn(1);
            }
            else if (GameControls.GameplayAllowed && entries.Count > 0 &&
                     ((kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)))
            {
                Open(entries[selected]);
            }
        }

        private void AnimateBook(float dt)
        {
            revealTime += dt;
            if (MenuPreferences.ReducedMotion) opening = false;

            float unfold = opening ? Mathf.Clamp01(revealTime / OpeningDuration) : 1f;
            presentation.SetOpening(unfold);
            if (revealTime >= OpeningDuration) opening = false;

            readingGroup.alpha = 1f;
            bool interactive = !IsAnimating;
            readingGroup.interactable = interactive;
            readingGroup.blocksRaycasts = interactive;
        }

        private void QueueNarration(ArchiveLog log)
        {
            audioSource.Stop();
            audioSource.clip = null;
            narrationPaused = false;
            narrationShouldResume = false;
            narrationLastTime = 0f;
            narrationLog = log;
            narrationRequested = log != null;
            narration.Prepare(log);
            RefreshNarrationControls();
        }

        public void ToggleNarration()
        {
            if (!IsOpen || narrationLog == null) return;
            if (narrationPaused)
            {
                narrationPaused = false;
                if (!narrationRequested)
                {
                    audioSource.UnPause();
                    narrationShouldResume = audioSource.isPlaying;
                }
            }
            else if (narrationRequested || audioSource.isPlaying)
            {
                narrationPaused = true;
                narrationShouldResume = false;
                audioSource.Pause();
            }
            else
            {
                ReplayNarration();
            }
            RefreshNarrationControls();
        }

        public void ReplayNarration()
        {
            if (!IsOpen || narrationLog == null) return;
            QueueNarration(narrationLog);
        }

        private int displayedVoiceSecond = -1, displayedVoiceLength = -1;
        private AudioClip displayedVoiceClip;

        private void RefreshNarrationControls()
        {
            if (voiceStatus == null) return;
            bool available = narrationLog != null && (audioSource.clip != null || narrationRequested);
            voiceToggle.interactable = voiceReplay.interactable = available;
            ConfigureReadingNavigation();

            voiceStatus.text = !available
                ? "Voice unavailable"
                : narrationPaused
                    ? "Reading paused"
                    : narrationRequested
                        ? "Preparing the archivist…"
                        : audioSource.isPlaying
                            ? "The archivist is reading"
                            : "Reading complete";

            var clip = audioSource.clip;
            float elapsed = clip == null ? 0f : audioSource.time;
            if (clip != null && !narrationRequested && !narrationPaused && !audioSource.isPlaying) elapsed = clip.length;
            voiceFill.sizeDelta = new Vector2(412f * (clip == null ? 0f : Mathf.Clamp01(elapsed / Mathf.Max(0.01f, clip.length))), 8f);

            int second = clip == null ? -1 : Mathf.FloorToInt(elapsed);
            int length = clip == null ? -1 : Mathf.FloorToInt(clip.length);
            if (displayedVoiceClip != clip || displayedVoiceSecond != second || displayedVoiceLength != length)
            {
                displayedVoiceClip = clip;
                displayedVoiceSecond = second;
                displayedVoiceLength = length;
                voiceTime.text = clip == null ? "ARCHIVIST VOICE" : "ARCHIVIST VOICE   ·   " + VoiceClock(elapsed) + " / " + VoiceClock(clip.length);
            }

            voiceToggleCaption.text = narrationPaused
                ? "Resume voice"
                : (narrationRequested || audioSource.isPlaying) ? "Pause voice" : "Listen again";
        }

        private void ConfigureReadingNavigation()
        {
            int mask = 0;
            for (int i = 0; i < readingButtons.Length; i++)
            {
                if (readingButtons[i].interactable) mask |= 1 << i;
            }
            if (mask == navigationMask) return;
            navigationMask = mask;

            var enabled = readingButtons.Where(b => b.interactable).ToArray();
            for (int i = 0; i < enabled.Length; i++)
            {
                enabled[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = enabled[(i + enabled.Length - 1) % enabled.Length],
                    selectOnRight = enabled[(i + 1) % enabled.Length],
                    selectOnDown = close
                };
            }
            var fallbackTab = bookmarkButtons.FirstOrDefault(b => b != null && b.gameObject.activeSelf && b.interactable);
            close.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = enabled.LastOrDefault() ?? fallbackTab
            };

            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            if (selectedObject != null && readingButtons.Any(b => b.gameObject == selectedObject && !b.interactable))
            {
                EventSystem.current.SetSelectedGameObject(close.gameObject);
            }
        }

        private static string VoiceClock(float value) => $"{Mathf.FloorToInt(value) / 60:00}:{Mathf.FloorToInt(value) % 60:00}";

        private List<string> ReadingPages(string text)
        {
            text = text ?? "";
            if (paginatedText.TryGetValue(text, out var cached)) return cached;

            var result = new List<string>();
            int offset = 0;
            while (offset < text.Length)
            {
                int low = 1, high = text.Length - offset, fit = 1;
                while (low <= high)
                {
                    int length = (low + high) / 2;
                    float height = body.GetPreferredValues(text.Substring(offset, length), body.rectTransform.rect.width, 0f).y;
                    if (height <= body.rectTransform.rect.height)
                    {
                        fit = length;
                        low = length + 1;
                    }
                    else
                    {
                        high = length - 1;
                    }
                }
                if (offset + fit < text.Length)
                {
                    int boundary = fit - 1;
                    while (boundary > 0 && !char.IsWhiteSpace(text[offset + boundary])) boundary--;
                    if (boundary > 0) fit = boundary + 1;
                    else if (fit > 1 && char.IsHighSurrogate(text[offset + fit - 1])) fit--;
                }
                result.Add(text.Substring(offset, fit));
                offset += fit;
            }
            if (result.Count == 0) result.Add("");
            paginatedText[text] = result;
            return result;
        }

        private int PageCount(string text) => ReadingPages(text).Count;

        private bool CanTurn() =>
            IsOpen && !opening && entries.Count > 0 &&
            (entries.Count > 1 || PageCount(CodexSession.Catalog.Find(entries[selected]).Text) > 1);

        private void Turn(int direction)
        {
            if (!CanTurn() || presentation.Turn.IsBusy) return;
            var log = CodexSession.Catalog.Find(entries[selected]);
            if (page + direction < 0 || page + direction >= PageCount(log.Text))
            {
                audioSource.Stop();
                narrationRequested = false;
                narrationPaused = false;
            }
            presentation.Turn.Automatic(direction);
        }

        private void MovePage(int direction)
        {
            var log = CodexSession.Catalog.Find(entries[selected]);
            page += direction;
            if (page < 0)
            {
                selected = (selected - 1 + entries.Count) % entries.Count;
                page = PageCount(CodexSession.Catalog.Find(entries[selected]).Text) - 1;
            }
            else if (page >= PageCount(log.Text))
            {
                selected = (selected + 1) % entries.Count;
                page = 0;
            }
        }

        private void ApplyTurn(int direction)
        {
            if (entries.Count == 0) return;
            var log = CodexSession.Catalog.Find(entries[selected]);
            MovePage(direction);
            var current = CodexSession.Catalog.Find(entries[selected]);
            if (current != log) QueueNarration(current);
            Redraw();
        }

        private void Redraw()
        {
            if (progress == null) return;
            progress.text = $"{CodexSession.FoundCount:00} / {CodexSession.Catalog?.Total ?? 0:00} RECOVERED   ·   {CodexSession.Decryption:0}%";
            decryptionFill.sizeDelta = new Vector2(412f * CodexSession.Decryption / 100f, 8f);

            if (entries.Count == 0)
            {
                title.text = "The archive is silent";
                body.text = "Recover logs from Archive Terminals to reveal the Old World.";
                clue.text = "";
                pages.text = "";
                SyncBookmarkTabs();
                return;
            }

            selected = Mathf.Clamp(selected, 0, entries.Count - 1);
            var log = CodexSession.Catalog.Find(entries[selected]);
            location.text = (string.IsNullOrWhiteSpace(log.Location) ? "THE ARCHIVE" : log.Location).ToUpperInvariant();
            previous.interactable = next.interactable = entries.Count > 1 || PageCount(log.Text) > 1;
            title.text = log.Title;
            var text = log.Text ?? "";
            page = Mathf.Clamp(page, 0, PageCount(text) - 1);
            body.text = ReadingPages(text)[page];
            clue.text = string.IsNullOrWhiteSpace(log.RoomClue) ? "No field observations recorded." : log.RoomClue;
            pages.text = $"RECORD {selected + 1:00} / {entries.Count:00}   ·   PAGE {page + 1} / {PageCount(text)}";

            SyncBookmarkTabs();
        }

        private void SyncBookmarkTabs()
        {
            if (presentation == null || presentation.BookmarkRail == null) return;

            int visibleCount = Mathf.Min(entries.Count, 8);
            while (bookmarkButtons.Count < visibleCount)
            {
                int idx = bookmarkButtons.Count;
                var tabRect = Rect("Bookmark Tab " + idx, presentation.BookmarkRail, new Vector2(112f, 56f), Vector2.zero);
                var tabImg = tabRect.gameObject.AddComponent<Image>();
                tabImg.sprite = PixelArchiveBook.BookmarkTab;
                tabImg.type = Image.Type.Simple;
                tabImg.preserveAspect = false;
                tabImg.raycastTarget = true;

                var tabBtn = tabRect.gameObject.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                tabBtn.transition = Selectable.Transition.None;
                int capturedIndex = idx;
                tabBtn.onClick.AddListener(() => SelectEntry(capturedIndex));

                var numLabel = Label("Tab Index " + idx, tabRect, new Vector2(72f, 34f), new Vector2(-4f, 0f), 20f, new Color(0.98f, 0.93f, 0.80f));
                numLabel.alignment = TextAlignmentOptions.Center;
                ArchiveMenuStyle.Apply(numLabel, "Caption", 20f);

                var focus = tabRect.gameObject.AddComponent<SettingsLightFocus>();
                focus.Label = numLabel;
                focus.UseInkPalette = false;

                bookmarkButtons.Add(tabBtn);
                bookmarkImages.Add(tabImg);
                bookmarkLabels.Add(numLabel);
            }

            float topY = 236f;
            float stepY = 66f;
            for (int i = 0; i < bookmarkButtons.Count; i++)
            {
                bool active = i < visibleCount;
                bookmarkButtons[i].gameObject.SetActive(active);
                if (!active) continue;

                bool isSelected = i == selected;
                var rect = (RectTransform)bookmarkButtons[i].transform;
                rect.anchoredPosition = new Vector2(isSelected ? 16f : 0f, topY - i * stepY);

                Color baseTint = PixelArchiveBook.GetBookmarkColor(i);
                Color tabColor = isSelected ? baseTint : Color.Lerp(baseTint, new Color(0.24f, 0.20f, 0.18f, 1f), 0.35f);
                bookmarkImages[i].color = tabColor;
                bookmarkLabels[i].text = $"{i + 1:00}";
                float luminance = tabColor.r * 0.299f + tabColor.g * 0.587f + tabColor.b * 0.114f;
                bool lightTab = luminance > 0.52f;
                bookmarkLabels[i].color = lightTab ? new Color(0.14f, 0.09f, 0.06f, 1f) : new Color(0.98f, 0.94f, 0.84f, 1f);
                bookmarkButtons[i].interactable = true;

                var focus = bookmarkButtons[i].GetComponent<SettingsLightFocus>();
                if (focus != null)
                {
                    focus.IsCurrent = isSelected;
                    focus.UseInkPalette = lightTab;
                }
            }
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one * 0.5f;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        private TMP_Text Label(string name, Transform parent, Vector2 size, Vector2 pos, float font, Color color)
        {
            var t = Rect(name, parent, size, pos).gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = font;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = false;
            return t;
        }

        private void RefreshShortcuts()
        {
            if (shortcuts == null) return;
            var style = ControllerGlyphService.ActiveStyle;
            shortcuts.text = style == ControllerPromptStyle.PlayStation
                ? "L1 / R1  Turn Page    ·    Circle  Close    ·    Create  Reopen\nTriangle  Pause / Resume Voice    ·    Square  Replay"
                : style == ControllerPromptStyle.Xbox
                    ? "LB / RB  Turn Page    ·    B  Close    ·    View  Reopen\nY  Pause / Resume Voice    ·    X  Replay"
                    : "Left / Right  Turn Page    ·    Esc  Close    ·    Tab  Reopen\nV  Pause / Resume Voice    ·    R  Replay";
        }

        private Image PixelRule(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
            image.sprite = PixelArchiveBook.PixelRule;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Image SolidRect(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
        {
            var image = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void Hand(TMP_Text label, float size)
        {
            if (handwriting != null) label.font = handwriting;
            label.fontSize = size;
            label.characterSpacing = 0f;
            label.fontStyle = FontStyles.Normal;
        }

        private Button Action(
            Transform parent,
            string text,
            float x,
            UnityEngine.Events.UnityAction callback,
            float y = -264f,
            float width = 200f,
            float height = 48f,
            bool onParchment = true)
        {
            var r = Rect(text, parent, new Vector2(width, height), new Vector2(x, y));
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = PixelArchiveBook.PixelButton;
            bg.type = Image.Type.Sliced;
            bg.color = onParchment ? new Color(1f, 0.96f, 0.88f, 0.95f) : new Color(0.28f, 0.21f, 0.19f, 0.96f);
            bg.raycastTarget = true;

            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(callback);

            Color labelColor = onParchment ? new Color(0.27f, 0.16f, 0.08f) : new Color(0.98f, 0.84f, 0.52f);
            var t = Label(text + " caption", r, new Vector2(width - 28f, height - 4f), Vector2.zero, 20f, labelColor);
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            ArchiveMenuStyle.Apply(t, "Caption", 20f);

            var focus = r.gameObject.AddComponent<SettingsLightFocus>();
            focus.Label = t;
            focus.UseInkPalette = onParchment;
            focus.PointerOffset = width * 0.5f - 10f;
            var diamond = SolidRect(r, "Focus diamond", new Vector2(6f, 6f), new Vector2(-focus.PointerOffset, 0f), Color.clear);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            focus.LeftPointer = diamond.rectTransform;
            return b;
        }

        private void EnsureRecoveryFeed()
        {
            if (recoveryFeedCanvas != null) return;
            CodexSession.Ensure();
            recoveryFeedCanvas = new GameObject("Archive feed canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            recoveryFeedCanvas.transform.SetParent(transform, false);
            var canvas = recoveryFeedCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 119;
            var scaler = recoveryFeedCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rect = Rect("Archive recovery feed", recoveryFeedCanvas.transform, new Vector2(500f, 130f), new Vector2(0f, 120f));
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = new Color(0.92f, 0.80f, 0.56f);
            text.raycastTarget = false;
            var feed = rect.gameObject.AddComponent<PartyFeedUI>();
            feed.enabled = false;
            feed.channel = CodexSession.Channel;
            feed.text = text;
            feed.maxLines = 2;
            feed.enabled = true;
            rect.gameObject.AddComponent<ArchiveFeedVisibility>();
        }

        private void Build()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("Codex EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            handwriting = Resources.Load<TMP_FontAsset>("UI/ArchiveHand");
            narration = gameObject.AddComponent<ArchiveNarration>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.ignoreListenerPause = true;
            audioSource.playOnAwake = false;

            var canvasGo = new GameObject("Codex Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var overlay = Rect("Codex overlay", canvasGo.transform, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.sizeDelta = Vector2.zero;
            panel = overlay.gameObject;

            var readingRect = Rect("Codex reading surface", overlay, ArchiveBookPresentation.StageReferenceSize, Vector2.zero);
            readingGroup = readingRect.gameObject.AddComponent<CanvasGroup>();
            var r = Rect("Book ink and controls", readingRect, ArchiveBookPresentation.StageReferenceSize, Vector2.zero);
            copyGroup = r.gameObject.AddComponent<CanvasGroup>();

            var sepiaGold = new Color(0.36f, 0.22f, 0.10f);
            var darkInk = new Color(0.14f, 0.10f, 0.08f);
            var emberFill = new Color(0.76f, 0.45f, 0.14f);

            // ---- LEFT PAGE (x center = -264, width = 420) ----
            var emblemRect = Rect("Lantern Emblem", r, new Vector2(28f, 36f), new Vector2(-456f, 286f));
            var emblemImg = emblemRect.gameObject.AddComponent<Image>();
            emblemImg.sprite = PixelArchiveBook.LanternEmblem;
            emblemImg.preserveAspect = true;
            emblemImg.raycastTarget = false;

            var stamp = Label("Archive stamp", r, new Vector2(376f, 28f), new Vector2(-246f, 286f), 19f, sepiaGold);
            stamp.text = "THE FIELD ARCHIVE";
            ArchiveMenuStyle.Apply(stamp, "Caption", 19f);
            stamp.characterSpacing = 3f;

            PixelRule(r, "Left heading rule", new Vector2(420f, 10f), new Vector2(-264f, 258f), new Color(0.42f, 0.27f, 0.13f, 0.72f));

            var heading = Label("Archive heading", r, new Vector2(420f, 96f), new Vector2(-264f, 206f), 44f, darkInk);
            heading.text = "Recovered\nmemories";
            ArchiveMenuStyle.Apply(heading, "Heading", 44f);
            heading.characterSpacing = 0f;
            heading.lineSpacing = -12f;

            progress = Label("Decryption", r, new Vector2(420f, 28f), new Vector2(-264f, 124f), 18f, sepiaGold);
            ArchiveMenuStyle.Apply(progress, "Caption", 18f);

            var trackRect = Rect("Decryption track", r, new Vector2(420f, 14f), new Vector2(-264f, 102f));
            var trackImg = trackRect.gameObject.AddComponent<Image>();
            trackImg.sprite = PixelArchiveBook.PixelBarFrame;
            trackImg.type = Image.Type.Sliced;
            trackImg.raycastTarget = false;
            var fillImg = SolidRect(trackRect, "Decryption fill", Vector2.zero, new Vector2(4f, 0f), emberFill);
            decryptionFill = fillImg.rectTransform;
            decryptionFill.anchorMin = decryptionFill.anchorMax = decryptionFill.pivot = new Vector2(0f, 0.5f);

            var note = Label("Archive note", r, new Vector2(420f, 48f), new Vector2(-264f, 56f), 24f, new Color(0.32f, 0.23f, 0.15f));
            note.text = "A record of what the world left behind.";
            note.fontStyle = FontStyles.Italic;

            SolidRect(r, "Clue margin", new Vector2(4f, 114f), new Vector2(-470f, -48f), new Color(0.62f, 0.38f, 0.14f, 0.75f));
            var clueHeading = Label("Clue heading", r, new Vector2(396f, 26f), new Vector2(-258f, -4f), 17f, sepiaGold);
            clueHeading.text = "FIELD OBSERVATION";
            ArchiveMenuStyle.Apply(clueHeading, "Caption", 17f);
            clueHeading.characterSpacing = 2.5f;

            clue = Label("Room clue", r, new Vector2(396f, 92f), new Vector2(-258f, -64f), 25f, darkInk);
            clue.lineSpacing = 4f;

            PixelRule(r, "Narration separator", new Vector2(420f, 10f), new Vector2(-264f, -134f), new Color(0.42f, 0.27f, 0.13f, 0.65f));

            voiceStatus = Label("Voice status", r, new Vector2(420f, 28f), new Vector2(-264f, -162f), 20f, darkInk);
            voiceStatus.alignment = TextAlignmentOptions.Center;

            var voiceTrackRect = Rect("Voice track", r, new Vector2(420f, 14f), new Vector2(-264f, -190f));
            var voiceTrackImg = voiceTrackRect.gameObject.AddComponent<Image>();
            voiceTrackImg.sprite = PixelArchiveBook.PixelBarFrame;
            voiceTrackImg.type = Image.Type.Sliced;
            voiceTrackImg.raycastTarget = false;
            voiceFill = SolidRect(voiceTrackRect, "Voice progress", Vector2.zero, new Vector2(4f, 0f), emberFill).rectTransform;
            voiceFill.anchorMin = voiceFill.anchorMax = voiceFill.pivot = new Vector2(0f, 0.5f);

            voiceTime = Label("Voice time", r, new Vector2(420f, 24f), new Vector2(-264f, -214f), 16f, sepiaGold);
            voiceTime.alignment = TextAlignmentOptions.Center;
            ArchiveMenuStyle.Apply(voiceTime, "Caption", 16f);

            voiceToggle = Action(r, "Pause voice", -372f, ToggleNarration, -264f, 200f, 48f, onParchment: true);
            voiceReplay = Action(r, "Replay voice", -156f, ReplayNarration, -264f, 200f, 48f, onParchment: true);
            voiceToggleCaption = voiceToggle.GetComponentInChildren<TMP_Text>();

            // ---- RIGHT PAGE (x center = +264, width = 420) ----
            location = Label("Record location", r, new Vector2(420f, 26f), new Vector2(264f, 286f), 17f, sepiaGold);
            ArchiveMenuStyle.Apply(location, "Caption", 17f);
            location.characterSpacing = 2.5f;

            title = Label("Log title", r, new Vector2(420f, 96f), new Vector2(264f, 224f), 36f, darkInk);
            ArchiveMenuStyle.Apply(title, "Heading", 36f);
            title.characterSpacing = 0f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 28f;
            title.fontSizeMax = 40f;

            PixelRule(r, "Right heading rule", new Vector2(420f, 10f), new Vector2(264f, 166f), new Color(0.42f, 0.27f, 0.13f, 0.72f));

            body = Label("Log text", r, new Vector2(420f, 352f), new Vector2(264f, -22f), 24f, darkInk);
            body.characterSpacing = 0f;
            body.lineSpacing = 6f;
            body.paragraphSpacing = 8f;
            body.alignment = TextAlignmentOptions.TopLeft;

            PixelRule(r, "Reading footer rule", new Vector2(420f, 10f), new Vector2(264f, -206f), new Color(0.42f, 0.27f, 0.13f, 0.65f));

            pages = Label("Page counter", r, new Vector2(420f, 26f), new Vector2(264f, -228f), 17f, sepiaGold);
            pages.alignment = TextAlignmentOptions.Center;
            ArchiveMenuStyle.Apply(pages, "Caption", 17f);

            previous = Action(r, "Previous", 158f, () => Turn(-1), -264f, 196f, 48f, onParchment: true);
            next = Action(r, "Next", 370f, () => Turn(1), -264f, 196f, 48f, onParchment: true);

            readingButtons = new[] { voiceToggle, voiceReplay, previous, next };

            Hand(heading, 48f);
            Hand(title, 40f);
            title.fontSizeMin = 30f;
            title.fontSizeMax = 40f;
            Hand(body, 34f);
            body.fontStyle = FontStyles.Bold;
            body.lineSpacing = 3f;
            body.paragraphSpacing = 7f;
            Hand(clue, 26f);
            clue.fontStyle = FontStyles.Bold;
            clue.enableAutoSizing = true;
            clue.fontSizeMin = 22f;
            clue.fontSizeMax = 26f;
            clue.lineSpacing = 3f;
            Hand(note, 26f);

            // ---- FOOTER CANVAS (Close button + dynamic shortcuts) ----
            var footer = new GameObject("Archive reading footer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            footer.transform.SetParent(transform, false);
            footerPanel = footer;
            var footerCanvas = footer.GetComponent<Canvas>();
            footerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            footerCanvas.sortingOrder = 121;
            var footerScaler = footer.GetComponent<CanvasScaler>();
            footerScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            footerScaler.referenceResolution = new Vector2(1920f, 1080f);
            footerScaler.matchWidthOrHeight = 0.5f;

            close = Action(footer.transform, "Close", 0f, Close, 92f, 190f, 48f, onParchment: false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 92f);

            shortcuts = Label("Codex shortcuts", footer.transform, new Vector2(1180f, 44f), new Vector2(0f, 38f), 17f, new Color(0.84f, 0.78f, 0.64f));
            shortcuts.alignment = TextAlignmentOptions.Center;
            shortcuts.rectTransform.anchorMin = shortcuts.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            shortcuts.rectTransform.anchoredPosition = new Vector2(0f, 38f);

            EnsureRecoveryFeed();

            // ---- 2D PIXEL-ART BOOK PRESENTATION ----
            presentation = gameObject.AddComponent<ArchiveBookPresentation>();
            presentation.Initialize(canvas, footerCanvas, overlay, readingRect, copyGroup, CanTurn, ApplyTurn);

            panel.SetActive(false);
            footerPanel.SetActive(false);
            builtLayoutVersion = ReaderLayoutVersion;
        }
    }
}
