using System;
using System.Collections;
using System.Collections.Generic;
using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// In-game pause menu and loop controller.
    /// Redesigned to match the Main Menu's frameless, ornate Hollow-Knight / Ori ethereal
    /// midnight-archive aesthetic: custom typography, procedural filigree crests,
    /// soft radial lantern glow auras, frameless action buttons with MenuButtonMotion wings,
    /// dynamic chamber title and memento recovery badges, and audio/accessibility settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";

        public static PauseMenuController Instance { get; private set; }
        public static bool IsPaused { get; private set; }

        public UnityEvent<bool> OnPauseToggled = new UnityEvent<bool>();

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip pauseOpenSound;
        [SerializeField] private AudioClip pauseCloseSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.7f;

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset font;

        // Midnight Indigo & Warm Lantern Amber Palette matching MainMenuController
        private readonly Color radiantIvory = new Color32(248, 242, 228, 255);
        private readonly Color moonlightSilver = new Color32(176, 190, 214, 235);
        private readonly Color mutedIndigo = new Color32(118, 132, 158, 200);
        private readonly Color amberGold = new Color32(255, 184, 72, 255);
        private readonly Color deepAmber = new Color32(214, 128, 36, 255);

        private Canvas pauseCanvas;
        private CanvasGroup canvasGroup;
        private GameObject mainPanel;
        private GameObject settingsPanel;
        private GameObject controlsPanel;
        private TMP_Text chamberTitleLabel;
        private TMP_Text mementoBadgeLabel;

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle reducedMotionToggle;

        private Button resumeButton;
        private Button settingsBackButton;
        private Button controlsBackButton;

        private Sprite filigreeSprite;
        private Sprite softRadialSprite;

        private string currentView = "none";
        private float previousTimeScale = 1f;
        private readonly Dictionary<PlayerMotor2D, bool> suspendedMotors = new Dictionary<PlayerMotor2D, bool>();
        private readonly Dictionary<LanternController, bool> suspendedLanterns = new Dictionary<LanternController, bool>();

        public string CurrentView => currentView;
        public GameObject MainPanel => mainPanel;
        public GameObject SettingsPanel => settingsPanel;
        public GameObject ControlsPanel => controlsPanel;

        public static void ResetStaticStateForTesting()
        {
            Time.timeScale = 1f;
            if (Instance != null && Instance.gameObject != null)
            {
                DestroyImmediate(Instance.gameObject);
            }
            Instance = null;
            IsPaused = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetEngineStateOnPlay()
        {
            Time.timeScale = 1f;
            IsPaused = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorPlayModeWatchdog()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                    state == UnityEditor.PlayModeStateChange.EnteredPlayMode)
                {
                    Time.timeScale = 1f;
                    IsPaused = false;
                }
            };
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject != null && Instance.gameObject != gameObject)
                {
                    DestroyImmediate(gameObject);
                    return;
                }
            }
            Instance = this;

            Time.timeScale = 1f;
            IsPaused = false;

            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("UI/MenuType");
            }

            filigreeSprite = CreateOrnateFiligreeSprite(512, 64);
            softRadialSprite = CreateSoftRadialSprite(128);

            EnsureEventSystem();
            BuildUIHierarchy();
        }

        private void OnEnable()
        {
            if (!IsPaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (IsPaused)
            {
                Resume();
            }
            IsPaused = false;

            if (softRadialSprite != null)
            {
                if (softRadialSprite.texture != null) Destroy(softRadialSprite.texture);
                Destroy(softRadialSprite);
            }
            if (filigreeSprite != null)
            {
                if (filigreeSprite.texture != null) Destroy(filigreeSprite.texture);
                Destroy(filigreeSprite);
            }

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;

            bool escapePressed = k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
            bool startPressed = g != null && g.startButton.wasPressedThisFrame;

            if (escapePressed || startPressed)
            {
                if (!IsPaused)
                {
                    var completions = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                    foreach (var comp in completions)
                    {
                        if (comp.GetType().Name == "ChamberCompletionSequence")
                        {
                            var prop = comp.GetType().GetProperty("IsRunning");
                            if (prop != null && (bool)prop.GetValue(comp)) return;
                        }
                    }

                    Pause();
                }
                else
                {
                    if (currentView == "settings" || currentView == "controls")
                    {
                        ShowMainView();
                    }
                    else
                    {
                        Resume();
                    }
                }
            }
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;

            SuspendPlayerInput(true);
            UpdateChamberInfo();
            ShowMainView();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            PlaySound(pauseOpenSound, 520f, 392f);
            OnPauseToggled.Invoke(true);

            if (EventSystem.current != null && resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            SuspendPlayerInput(false);

            currentView = "none";
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            PlaySound(pauseCloseSound, 392f, 520f);
            OnPauseToggled.Invoke(false);
        }

        public void ShowMainView()
        {
            currentView = "main";
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            UpdateChamberInfo();

            if (EventSystem.current != null && resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        public void ShowSettingsView()
        {
            currentView = "settings";
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            SyncSettingsValues();

            if (EventSystem.current != null && masterSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
            }
        }

        public void ShowControlsView()
        {
            currentView = "controls";
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(true);

            if (EventSystem.current != null && controlsBackButton != null)
            {
                EventSystem.current.SetSelectedGameObject(controlsBackButton.gameObject);
            }
        }

        public void RestartChamber()
        {
            Resume();

            var directors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var d in directors)
            {
                if (d.GetType().Name == "FurkanLevelDirector")
                {
                    var restartMethod = d.GetType().GetMethod("Restart");
                    if (restartMethod != null)
                    {
                        restartMethod.Invoke(d, null);
                        return;
                    }
                    var resetMethod = d.GetType().GetMethod("ResetAttempt");
                    if (resetMethod != null)
                    {
                        resetMethod.Invoke(d, null);
                    }
                }
            }

            var player = FindAnyObjectByType<PlayerRespawnController>();
            if (player != null)
            {
                player.Respawn();
            }
        }

        public void QuitToMainMenu()
        {
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            yield return null;
            if (Application.CanStreamedLevelBeLoaded(MainMenuScenePath))
            {
                SceneManager.LoadScene(MainMenuScenePath);
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private void SuspendPlayerInput(bool suspend)
        {
            var motors = FindObjectsByType<PlayerMotor2D>(FindObjectsSortMode.None);
            foreach (var motor in motors)
            {
                if (motor == null) continue;
                if (suspend)
                {
                    suspendedMotors[motor] = motor.enabled;
                    motor.enabled = false;
                    var rb = motor.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    if (suspendedMotors.TryGetValue(motor, out bool wasEnabled))
                    {
                        motor.enabled = wasEnabled;
                    }
                    else
                    {
                        motor.enabled = true;
                    }
                }
            }
            if (!suspend) suspendedMotors.Clear();

            var lanterns = FindObjectsByType<LanternController>(FindObjectsSortMode.None);
            foreach (var lantern in lanterns)
            {
                if (lantern == null) continue;
                if (suspend)
                {
                    suspendedLanterns[lantern] = lantern.enabled;
                    lantern.enabled = false;
                }
                else
                {
                    if (suspendedLanterns.TryGetValue(lantern, out bool wasEnabled))
                    {
                        lantern.enabled = wasEnabled;
                    }
                    else
                    {
                        lantern.enabled = true;
                    }
                }
            }
            if (!suspend) suspendedLanterns.Clear();
        }

        private void UpdateChamberInfo()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string chamberName = "RUINED ARCHIVE";

            if (sceneName.Contains("Furkan"))
            {
                chamberName = "CHAMBER 01-02   •   THE FIRST LIGHT";
            }
            else if (sceneName.Contains("Kemal"))
            {
                chamberName = "CHAMBER 02   •   THE FLOODED MECHANISMS";
            }
            else if (sceneName.Contains("ArchiveTest"))
            {
                chamberName = "CHAMBER 03   •   CALIBRATION SANCTUARY";
            }

            if (chamberTitleLabel != null)
            {
                chamberTitleLabel.text = chamberName;
            }

            if (mementoBadgeLabel != null)
            {
                bool hasMemento = false;
                var directors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var d in directors)
                {
                    if (d.GetType().Name == "FurkanLevelDirector")
                    {
                        var field = d.GetType().GetField("HasMemento");
                        if (field != null) hasMemento = (bool)field.GetValue(d);
                    }
                }

                if (hasMemento)
                {
                    mementoBadgeLabel.text = "✦   ARCHIVE MEMENTO RECOVERED   ✦\n<size=13><color=#B0BED6>\"The deep stone whispers of paths that open only in high flame.\"</color></size>";
                    mementoBadgeLabel.color = amberGold;
                }
                else
                {
                    mementoBadgeLabel.text = "◇   ARCHIVE MEMENTO : UNFOUND   ◇\n<size=13><color=#76849E>Scout forgotten ledges with radiant flare to reveal the ancient seal.</color></size>";
                    mementoBadgeLabel.color = moonlightSilver;
                }
            }
        }

        private void SyncSettingsValues()
        {
            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(MenuPreferences.Master);
                UpdateSliderPercentLabel(masterSlider);
            }
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(MenuPreferences.Music);
                UpdateSliderPercentLabel(musicSlider);
            }
            if (effectsSlider != null)
            {
                effectsSlider.SetValueWithoutNotify(MenuPreferences.Effects);
                UpdateSliderPercentLabel(effectsSlider);
            }
            if (reducedMotionToggle != null)
            {
                reducedMotionToggle.SetIsOnWithoutNotify(MenuPreferences.ReducedMotion);
                UpdateSwitchVisual(reducedMotionToggle);
            }
        }

        private static void UpdateSliderPercentLabel(Slider slider)
        {
            if (slider == null) return;
            var valNode = slider.transform.Find("Value");
            if (valNode != null && valNode.TryGetComponent<TMP_Text>(out var txt))
            {
                txt.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            }
        }

        private void UpdateSwitchVisual(Toggle toggle)
        {
            if (toggle == null) return;
            var valNode = toggle.transform.Find("Pill/Value");
            if (valNode != null && valNode.TryGetComponent<TMP_Text>(out var txt))
            {
                txt.text = toggle.isOn ? "ON" : "OFF";
                txt.color = toggle.isOn ? amberGold : mutedIndigo;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = toggle.isOn
                    ? new Color(0.95f, 0.62f, 0.18f, 0.28f)
                    : new Color(0.10f, 0.14f, 0.24f, 0.35f);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                if (Application.isPlaying) DontDestroyOnLoad(es);
            }
        }

        private void BuildUIHierarchy()
        {
            if (pauseCanvas != null) return;

            var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            pauseCanvas = canvasGo.GetComponent<Canvas>();
            pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            pauseCanvas.sortingOrder = 220;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Fullscreen Midnight Ethereal Veil
            var backdrop = new GameObject("Backdrop", typeof(Image));
            backdrop.transform.SetParent(canvasGo.transform, false);
            var bRect = backdrop.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.sizeDelta = Vector2.zero;
            var bImg = backdrop.GetComponent<Image>();
            bImg.color = new Color(0.012f, 0.018f, 0.038f, 0.90f);
            bImg.raycastTarget = true;

            // Central Frameless Modal Container
            var modal = new GameObject("ModalContainer", typeof(RectTransform));
            modal.transform.SetParent(canvasGo.transform, false);
            var mRect = modal.GetComponent<RectTransform>();
            mRect.anchorMin = mRect.anchorMax = new Vector2(0.5f, 0.5f);
            mRect.pivot = new Vector2(0.5f, 0.5f);
            mRect.sizeDelta = new Vector2(880f, 820f);
            mRect.anchoredPosition = Vector2.zero;

            // Soft Radial Lantern Glow behind Title
            var titleGlow = CreateAnchoredRect("Title Soft Lantern Glow", modal.transform, new Vector2(740f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0f, 305f));
            var glowImg = titleGlow.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = new Color(1.0f, 0.64f, 0.18f, 0.16f);
            glowImg.raycastTarget = false;

            // Top Ornate Filigree Crest
            var topCrest = CreateAnchoredRect("Top Ornate Crest", modal.transform, new Vector2(460f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0f, 375f));
            var topCrestImg = topCrest.gameObject.AddComponent<Image>();
            topCrestImg.sprite = filigreeSprite;
            topCrestImg.color = new Color(1.0f, 0.76f, 0.34f, 0.80f);
            topCrestImg.raycastTarget = false;

            // Whispered Eyebrow
            var eyebrow = CreateLabel("Eyebrow", modal.transform, new Vector2(0f, 345f), new Vector2(600f, 24f),
                "—   ARCHIVE TRAVERSAL SUSPENDED   —", 11.5f, TextAlignmentOptions.Center, moonlightSilver);
            eyebrow.characterSpacing = 6.5f;

            // Monumental Center Title: PAUSED
            var title = CreateLabel("Title", modal.transform, new Vector2(0f, 300f), new Vector2(640f, 60f),
                "PAUSED", 52f, TextAlignmentOptions.Center, radiantIvory);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 10f;

            // Lower Ornate Filigree Divider under Title
            var midCrest = CreateAnchoredRect("Title Bottom Filigree", modal.transform, new Vector2(520f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0f, 255f));
            var midCrestImg = midCrest.gameObject.AddComponent<Image>();
            midCrestImg.sprite = filigreeSprite;
            midCrestImg.color = amberGold;
            midCrestImg.raycastTarget = false;

            // Chamber Title Whisper
            chamberTitleLabel = CreateLabel("ChamberTitle", modal.transform, new Vector2(0f, 225f), new Vector2(640f, 26f),
                "CHAMBER 01-02   •   THE FIRST LIGHT", 13.5f, TextAlignmentOptions.Center, amberGold);
            chamberTitleLabel.characterSpacing = 4f;

            // Memento Recovery Badge
            mementoBadgeLabel = CreateLabel("MementoBadge", modal.transform, new Vector2(0f, 175f), new Vector2(680f, 50f),
                "", 14f, TextAlignmentOptions.Center, radiantIvory);

            // Sub-panels (Frameless Center Stage)
            BuildMainPanel(modal.transform);
            BuildSettingsPanel(modal.transform);
            BuildControlsPanel(modal.transform);

            ShowMainView();
            currentView = "none";
        }

        private void BuildMainPanel(Transform parent)
        {
            mainPanel = new GameObject("MainPanel", typeof(RectTransform));
            mainPanel.transform.SetParent(parent, false);
            var r = mainPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(640f, 420f);
            r.anchoredPosition = new Vector2(0f, -80f);

            float startY = 95f;
            float stepY = 66f;

            resumeButton = CreateFramelessMenuButton("ResumeButton", r, "Resume Traversal", "Awaken the lantern and resume your journey", new Vector2(0f, startY), Resume, isPrimary: true);
            CreateFramelessMenuButton("ControlsButton", r, "Controls & Guide", "Keybindings, coyote leap & ember mechanics", new Vector2(0f, startY - stepY), ShowControlsView);
            CreateFramelessMenuButton("SettingsButton", r, "Sanctuary Settings", "Harmonize audio levels & motion preferences", new Vector2(0f, startY - stepY * 2), ShowSettingsView);
            CreateFramelessMenuButton("RestartButton", r, "Restart Chamber", "Restore chamber to last checkpoint", new Vector2(0f, startY - stepY * 3), RestartChamber);
            CreateFramelessMenuButton("QuitButton", r, "Leave to Title", "Save preferences and return to title screen", new Vector2(0f, startY - stepY * 4), QuitToMainMenu);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(parent, false);
            var r = settingsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(760f, 440f);
            r.anchoredPosition = new Vector2(0f, -80f);

            var titleLabel = CreateLabel("SettingsTitle", settingsPanel.transform, new Vector2(0f, 135f), new Vector2(600f, 32f),
                "SANCTUARY SETTINGS", 22f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            var crest = CreateAnchoredRect("Settings Filigree", settingsPanel.transform, new Vector2(480f, 26f), new Vector2(0.5f, 0.5f), new Vector2(0f, 108f));
            var crestImg = crest.gameObject.AddComponent<Image>();
            crestImg.sprite = filigreeSprite;
            crestImg.color = amberGold;
            crestImg.raycastTarget = false;

            float y = 62f;
            masterSlider = CreateSlider("MasterSlider", r, new Vector2(0f, y), "Master Volume", "Global audio output level", v =>
            {
                MenuPreferences.SetAudio(v, MenuPreferences.Music, MenuPreferences.Effects);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(masterSlider);
            });

            y -= 58f;
            musicSlider = CreateSlider("MusicSlider", r, new Vector2(0f, y), "Ambient Music", "Background archive score (In-game key: M)", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, v, MenuPreferences.Effects);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(musicSlider);
            });

            y -= 58f;
            effectsSlider = CreateSlider("EffectsSlider", r, new Vector2(0f, y), "Sound Effects (SFX)", "Footsteps, lantern flare & embers (In-game key: N)", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, MenuPreferences.Music, v);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(effectsSlider);
            });

            y -= 58f;
            reducedMotionToggle = CreateToggle("ReducedMotionToggle", r, new Vector2(0f, y), "Reduced Motion Mode", "Calm camera shake and UI oscillations", b =>
            {
                MenuPreferences.SetReducedMotion(b);
                MenuPreferences.Save();
                UpdateSwitchVisual(reducedMotionToggle);
            });

            settingsBackButton = CreateEtherealActionButton("SettingsBackButton", r, "<  Return to Pause", new Vector2(0f, -165f), ShowMainView, isPrimary: true);
        }

        private void BuildControlsPanel(Transform parent)
        {
            controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
            controlsPanel.transform.SetParent(parent, false);
            var r = controlsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(760f, 440f);
            r.anchoredPosition = new Vector2(0f, -80f);

            var titleLabel = CreateLabel("ControlsTitle", controlsPanel.transform, new Vector2(0f, 135f), new Vector2(600f, 32f),
                "CONTROLS & LANTERN GUIDE", 22f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            var crest = CreateAnchoredRect("Controls Filigree", controlsPanel.transform, new Vector2(480f, 26f), new Vector2(0.5f, 0.5f), new Vector2(0f, 108f));
            var crestImg = crest.gameObject.AddComponent<Image>();
            crestImg.sprite = filigreeSprite;
            crestImg.color = amberGold;
            crestImg.raycastTarget = false;

            CreateControlRow(r, "A / D  or  ARROWS", "TRAVERSE ARCHIVE", "Smooth horizontal locomotion with momentum & air control", 66f);
            CreateControlRow(r, "SPACE  /  W", "LEAP & COYOTE JUMP", "Hold for maximum arc; forgiving ledge grace window", 16f);
            CreateControlRow(r, "LEFT SHIFT", "ARCHIVE SPRINT", "Burst speed across wide chasms (consumes stamina)", -34f);
            CreateControlRow(r, "F  /  L-CLICK", "LANTERN FLARE SURGE", "Ignite radiant light burst to charge ancient glyphs", -84f);
            CreateControlRow(r, "E  (HOLD)", "EMBER CIRCUIT DRAIN", "Transfer / recover embers to power ancient platforms", -134f);

            controlsBackButton = CreateEtherealActionButton("ControlsBackButton", r, "<  Return to Pause", new Vector2(0f, -188f), ShowMainView, isPrimary: true);
        }

        private Button CreateFramelessMenuButton(
            string name,
            RectTransform parent,
            string title,
            string subtitle,
            Vector2 pos,
            UnityAction onClick,
            bool isPrimary = false)
        {
            var itemRect = CreateAnchoredRect(name, parent, new Vector2(580f, isPrimary ? 62f : 52f), new Vector2(0.5f, 0.5f), pos);

            var hitGraphic = itemRect.gameObject.AddComponent<Image>();
            hitGraphic.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(460f, 76f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            var wingContainer = CreateStretchRect("Ornate Wings", itemRect);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = isPrimary ? 0.35f : 0f;
            wingGroup.blocksRaycasts = false;

            float wingOffset = Mathf.Max(140f, title.Length * 9.5f + 48f);
            var leftWing = CreateOrnateWingGraphic("Left Wing", wingContainer, new Vector2(-wingOffset, 0f), isLeft: true);
            var rightWing = CreateOrnateWingGraphic("Right Wing", wingContainer, new Vector2(wingOffset, 0f), isLeft: false);

            var titleTxt = CreateLabel("Title", itemRect, new Vector2(0f, 6f), new Vector2(520f, 28f),
                title.ToUpperInvariant(), isPrimary ? 23f : 19f, TextAlignmentOptions.Center, isPrimary ? amberGold : radiantIvory);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 6f : 4.5f;

            var subTxt = CreateLabel("Subtitle", itemRect, new Vector2(0f, -14f), new Vector2(520f, 18f),
                subtitle, 11f, TextAlignmentOptions.Center, moonlightSilver);
            subTxt.characterSpacing = 1.5f;

            var lineRect = CreateAnchoredRect("Underline Filigree", itemRect, new Vector2(0f, 2f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            var btn = itemRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hitGraphic;
            btn.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
                    onClick.Invoke();
                });
            }

            var motion = itemRect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = titleTxt;
            motion.SubtitleText = subTxt;
            motion.SoftGlow = glowImg;
            motion.LeftWing = leftWing;
            motion.RightWing = rightWing;
            motion.WingGroup = wingGroup;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;
            motion.BaseWingOffset = wingOffset;

            return btn;
        }

        private RectTransform CreateOrnateWingGraphic(string name, RectTransform parent, Vector2 pos, bool isLeft)
        {
            var wing = CreateAnchoredRect(name, parent, new Vector2(88f, 20f), new Vector2(0.5f, 0.5f), pos);

            var stem = CreateAnchoredRect("Filigree Stem", wing, new Vector2(68f, 10f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? -6f : 6f, 0f));
            var stemImg = stem.gameObject.AddComponent<Image>();
            stemImg.sprite = filigreeSprite;
            stemImg.color = amberGold;
            stemImg.raycastTarget = false;

            var diamond = CreateAnchoredRect("Diamond Tip", wing, new Vector2(7f, 7f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? 30f : -30f, 0f));
            diamond.localEulerAngles = new Vector3(0f, 0f, 45f);
            var diamondImg = diamond.gameObject.AddComponent<Image>();
            diamondImg.color = radiantIvory;
            diamondImg.raycastTarget = false;

            return wing;
        }

        private Button CreateEtherealActionButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 anchoredPos,
            UnityAction onClick,
            bool isPrimary = false,
            float width = 300f)
        {
            var rect = CreateAnchoredRect(name, parent, new Vector2(width, 48f), new Vector2(0.5f, 0.5f), anchoredPos);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width + 40f, 60f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = isPrimary ? new Color(1.0f, 0.66f, 0.20f, 0.22f) : Color.clear;
            glowImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(width * 0.70f, 14f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = isPrimary ? amberGold : new Color(0.65f, 0.75f, 0.92f, 0.35f);
            lineImg.raycastTarget = false;

            var txt = CreateLabel("Caption", rect, Vector2.zero, new Vector2(width, 36f),
                label.ToUpperInvariant(), 14.5f, TextAlignmentOptions.Center, isPrimary ? amberGold : radiantIvory);
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3f;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
                    onClick.Invoke();
                });
            }

            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = txt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;

            return btn;
        }

        private Slider CreateSlider(string name, RectTransform parent, Vector2 pos, string title, string subtitle, UnityAction<float> onValueChanged)
        {
            var row = CreateAnchoredRect(name, parent, new Vector2(700f, 54f), new Vector2(0.5f, 0.5f), pos);

            var titleTxt = CreateLabel("Title", row, new Vector2(0f, 10f), new Vector2(320f, 22f),
                title.ToUpperInvariant(), 14f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(0f, -10f), new Vector2(320f, 18f),
                subtitle, 11f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(340f, 26f);
            sRect.anchoredPosition = new Vector2(0f, 0f);

            var track = CreateAnchoredRect("Track", sRect, new Vector2(260f, 4f), new Vector2(0f, 0.5f), Vector2.zero);
            track.pivot = new Vector2(0f, 0.5f);
            track.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.68f, 0.92f, 0.22f);

            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = amberGold;

            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(13f, 13f), new Vector2(0f, 0.5f), Vector2.zero);
            handle.localEulerAngles = new Vector3(0f, 0f, 45f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = radiantIvory;

            var valTxt = CreateLabel("Value", sRect, Vector2.zero, new Vector2(60f, 24f),
                "100%", 13f, TextAlignmentOptions.Right, amberGold);
            valTxt.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            valTxt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            valTxt.rectTransform.pivot = new Vector2(1f, 0.5f);
            valTxt.fontStyle = FontStyles.Bold;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;

            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(onValueChanged);
            }

            return slider;
        }

        private Toggle CreateToggle(string name, RectTransform parent, Vector2 pos, string title, string subtitle, UnityAction<bool> onValueChanged)
        {
            var row = CreateAnchoredRect(name, parent, new Vector2(700f, 50f), new Vector2(0.5f, 0.5f), pos);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, new Vector2(0f, 9f), new Vector2(440f, 20f),
                title.ToUpperInvariant(), 13.5f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(0f, -9f), new Vector2(440f, 18f),
                subtitle, 11f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var pill = CreateAnchoredRect("Pill", row, new Vector2(90f, 28f), new Vector2(1f, 0.5f), new Vector2(-45f, 0f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.color = new Color(0.10f, 0.14f, 0.24f, 0.35f);

            var valTxt = CreateLabel("Value", pill, Vector2.zero, new Vector2(90f, 28f),
                "OFF", 12f, TextAlignmentOptions.Center, amberGold);
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.characterSpacing = 2f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            toggle.graphic = null;
            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(onValueChanged);
            }
            return toggle;
        }

        private void CreateControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(720f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var badge = CreateAnchoredRect("Keycap Badge", row, new Vector2(190f, 30f), new Vector2(0f, 0.5f), new Vector2(95f, 0f));
            badge.gameObject.AddComponent<Image>().color = new Color(0.98f, 0.68f, 0.24f, 0.16f);

            var keyTxt = CreateLabel("Key Text", badge, Vector2.zero, new Vector2(180f, 30f),
                keys, 12f, TextAlignmentOptions.Center, amberGold);
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 1.5f;

            var actTxt = CreateLabel("Action", row, new Vector2(210f, 7f), new Vector2(490f, 20f),
                actionTitle, 13.5f, TextAlignmentOptions.Left, radiantIvory);
            actTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            actTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            actTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 2f;

            var descTxt = CreateLabel("Description", row, new Vector2(210f, -9f), new Vector2(490f, 18f),
                desc, 11f, TextAlignmentOptions.Left, moonlightSilver);
            descTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            descTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            descTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        private TMP_Text CreateLabel(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Sprite CreateOrnateFiligreeSprite(int w = 512, int h = 64)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float ax = Mathf.Abs(nx);
                    float ay = Mathf.Abs(ny);

                    float lineThickness = Mathf.Lerp(0.08f, 0.01f, ax);
                    float mainLine = Mathf.Clamp01(1f - ay / lineThickness) * Mathf.Clamp01(1f - ax);
                    float centerDiamond = Mathf.Clamp01(1f - (ax * 14f + ay * 1.8f));

                    float wingCurveY = Mathf.Sin((ax - 0.06f) * 18f) * 0.42f * Mathf.Clamp01(1f - Mathf.Abs(ax - 0.16f) * 7f);
                    float wing = (ax > 0.05f && ax < 0.28f)
                        ? Mathf.Clamp01(1f - Mathf.Abs(ay - Mathf.Abs(wingCurveY)) / 0.07f) * Mathf.Clamp01(1f - (ax - 0.05f) * 4f)
                        : 0f;

                    float alpha = Mathf.Clamp01(Mathf.Max(mainLine, Mathf.Max(centerDiamond, wing)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 100f);
        }

        private static Sprite CreateSoftRadialSprite(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        private static RectTransform CreateStretchRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateAnchoredRect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private void PlaySound(AudioClip clip, float freq1, float freq2)
        {
            AudioClip sound = clip != null ? clip : GetProceduralPauseClip(freq1, freq2);
            if (sound != null && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayOneShot(sound, soundVolume * SoundEffectManager.Instance.MasterSFXVolume);
            }
        }

        private static AudioClip GetProceduralPauseClip(float freq1, float freq2)
        {
            const int sampleRate = 44100;
            const float duration = 0.45f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 8.5f);
                float f = Mathf.Lerp(freq1, freq2, t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
            }

            var clip = AudioClip.Create("PauseChime", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
