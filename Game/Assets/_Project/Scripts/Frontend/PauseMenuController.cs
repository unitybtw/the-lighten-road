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
    /// midnight-archive aesthetic: custom typography, authentic Hollow-Knight crests,
    /// soft radial lantern glow auras, frameless action buttons with MenuButtonMotion wings,
    /// rounded capsule sliders with circular anti-aliased glowing handles,
    /// dynamic chamber title and memento recovery badges, and audio/display/motion settings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";

        public static PauseMenuController Instance { get; private set; }
        public static System.Func<bool> IsCompletionSequenceRunning;
        public static System.Action OnRestartLevelRequested;
        public static System.Action OnReturnToMenuRequested;
        public static bool IsPaused { get; private set; }

        public UnityEvent<bool> OnPauseToggled = new UnityEvent<bool>();

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip pauseOpenSound;
        [SerializeField] private AudioClip pauseCloseSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.7f;

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset font;

        // Moonlight & Silver Mist + Warm Ember Palette matching MainMenuController
        private readonly Color radiantIvory = new Color32(236, 242, 252, 255);
        private readonly Color moonlightSilver = new Color32(180, 198, 224, 240);
        private readonly Color mutedIndigo = new Color32(118, 136, 166, 210);
        private readonly Color silverBorder = new Color32(150, 174, 210, 115);
        private readonly Color amberGold = new Color32(255, 174, 66, 255);
        private readonly Color deepAmber = new Color32(214, 122, 34, 255);

        private Canvas pauseCanvas;
        private CanvasGroup canvasGroup;
        private GameObject mainHeader;
        private GameObject mainPanel;
        private GameObject settingsPanel;
        private GameObject controlsPanel;
        private TMP_Text chamberTitleLabel;
        private TMP_Text mementoBadgeLabel;

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle fullscreenToggle;
        private Toggle vsyncToggle;
        private Toggle reducedMotionToggle;

        private Button resumeButton;
        private Button settingsBackButton;
        private Button controlsBackButton;

        private RectTransform modalRect;
        private RectTransform titleGlowRect;
        private CanvasGroup mainPanelGroup;
        private CanvasGroup settingsPanelGroup;
        private CanvasGroup controlsPanelGroup;
        private Coroutine transitionRoutine;
        private Coroutine subViewRoutine;

        private Sprite filigreeSprite;
        private Sprite softRadialSprite;
        private Sprite topFiligreeSprite;
        private Sprite bottomCrestSprite;
        private Sprite buttonPointerSprite;
        private Sprite vignetteBlurSprite;
        private Sprite roundTrackSprite;
        private Sprite roundFillSprite;
        private Sprite roundHandleSprite;
        private Sprite thinBorderSprite;

        private readonly List<Sprite> proceduralSprites = new List<Sprite>();

        private AudioClip cachedOpenClip;
        private AudioClip cachedCloseClip;

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

            topFiligreeSprite = Resources.Load<Sprite>("UI/HollowKnight_TopFiligree");
            bottomCrestSprite = Resources.Load<Sprite>("UI/HollowKnight_BottomCrest");
            buttonPointerSprite = Resources.Load<Sprite>("UI/HollowKnight_ButtonPointer");

#if UNITY_EDITOR
            if (topFiligreeSprite == null)
                topFiligreeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/HollowKnight_TopFiligree.png");
            if (bottomCrestSprite == null)
                bottomCrestSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/HollowKnight_BottomCrest.png");
            if (buttonPointerSprite == null)
                buttonPointerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/HollowKnight_ButtonPointer.png");
#endif

            filigreeSprite = CreateOrnateFiligreeSprite(512, 64);
            softRadialSprite = CreateSoftRadialSprite(128);
            vignetteBlurSprite = CreateVignetteBlurSprite(256);
            roundTrackSprite = CreateRoundedCapsuleSprite(24, 8, new Color(0.55f, 0.68f, 0.86f, 0.35f), 4);
            roundFillSprite = CreateRoundedCapsuleSprite(24, 8, amberGold, 4);
            roundHandleSprite = CreateCircularHandleSprite(32);
            thinBorderSprite = CreateThinBorderBoxSprite(36, 36, 4, 2f);

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
            if (Instance == this)
            {
                Instance = null;
            }
            Time.timeScale = 1f;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
            if (subViewRoutine != null)
            {
                StopCoroutine(subViewRoutine);
                subViewRoutine = null;
            }
            if (IsPaused)
            {
                Resume();
            }
            IsPaused = false;

            for (int i = 0; i < proceduralSprites.Count; i++)
            {
                var s = proceduralSprites[i];
                if (s != null)
                {
                    if (s.texture != null) Destroy(s.texture);
                    Destroy(s);
                }
            }
            proceduralSprites.Clear();

            if (cachedOpenClip != null)
            {
                Destroy(cachedOpenClip);
                cachedOpenClip = null;
            }
            if (cachedCloseClip != null)
            {
                Destroy(cachedCloseClip);
                cachedCloseClip = null;
            }

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var k = Keyboard.current;
            var g = ControllerGlyphService.GetActiveGamepad();

            bool escapePressed = k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
            bool startPressed = g != null && g.startButton.wasPressedThisFrame;
            bool eastPressed = g != null && g.buttonEast.wasPressedThisFrame;

            if (escapePressed || startPressed || (IsPaused && eastPressed))
            {
                if (!IsPaused)
                {
                    if (IsCompletionSequenceRunning != null && IsCompletionSequenceRunning())
                        return;

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

            if (IsPaused && g != null)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                {
                    bool padNavIntent = g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame ||
                                        g.dpad.left.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame ||
                                        g.buttonSouth.wasPressedThisFrame ||
                                        (g.leftStick.ReadValue().sqrMagnitude > 0.15f);
                    if (padNavIntent)
                    {
                        RestorePauseMenuSelection();
                    }
                }

                if (currentView == "controls")
                {
                    if (g.leftShoulder.wasPressedThisFrame || g.dpad.left.wasPressedThisFrame)
                    {
                        SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 2) % 3);
                        if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                    }
                    else if (g.rightShoulder.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame)
                    {
                        SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 1) % 3);
                        if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                    }
                }
            }
        }

        private void RestorePauseMenuSelection()
        {
            if (EventSystem.current == null) return;
            GameObject target = null;
            if (currentView == "main" && resumeButton != null) target = resumeButton.gameObject;
            else if (currentView == "controls" && controlsBackButton != null) target = controlsBackButton.gameObject;
            else if (currentView == "settings" && masterSlider != null) target = masterSlider.gameObject;
            if (target != null) EventSystem.current.SetSelectedGameObject(target);
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

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            PlaySound(pauseOpenSound, 520f, 392f, isOpen: true);
            OnPauseToggled.Invoke(true);

            if (isActiveAndEnabled)
            {
                transitionRoutine = StartCoroutine(AnimateOpenRoutine());
            }
            else
            {
                SetOpenVisualState(1f);
            }

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
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            PlaySound(pauseCloseSound, 392f, 520f, isOpen: false);
            OnPauseToggled.Invoke(false);

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (isActiveAndEnabled)
            {
                transitionRoutine = StartCoroutine(AnimateCloseRoutine());
            }
            else
            {
                SetClosedVisualState();
            }
        }

        public void ShowMainView()
        {
            currentView = "main";
            if (mainHeader != null) mainHeader.SetActive(true);
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            UpdateChamberInfo();
            AnimateSubView(mainPanelGroup);

            if (EventSystem.current != null && resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        public void ShowSettingsView()
        {
            currentView = "settings";
            if (mainHeader != null) mainHeader.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            SyncSettingsValues();
            AnimateSubView(settingsPanelGroup);

            if (EventSystem.current != null && masterSlider != null)
            {
                EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
            }
        }

        public void ShowControlsView()
        {
            currentView = "controls";
            if (mainHeader != null) mainHeader.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(true);
            AnimateSubView(controlsPanelGroup);

            if (EventSystem.current != null && controlsBackButton != null)
            {
                EventSystem.current.SetSelectedGameObject(controlsBackButton.gameObject);
            }
        }

        public void RestartChamber()
        {
            Resume();

            if (OnRestartLevelRequested != null)
            {
                OnRestartLevelRequested.Invoke();
                return;
            }

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
                        return;
                    }
                }
            }

            var player = FindAnyObjectByType<PlayerRespawnController>();
            if (player != null)
            {
                player.Respawn();
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
                chamberName = "CHAMBER 01-02   -   THE FIRST LIGHT";
            }
            else if (sceneName.Contains("Kemal"))
            {
                chamberName = "CHAMBER 02   -   THE FLOODED MECHANISMS";
            }
            else if (sceneName.Contains("ArchiveTest"))
            {
                chamberName = "CHAMBER 03   -   CALIBRATION SANCTUARY";
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
                    mementoBadgeLabel.text = "*   ARCHIVE MEMENTO RECOVERED   *\n<size=13><color=#B0BED6>\"The deep stone whispers of paths that open only in high flame.\"</color></size>";
                    mementoBadgeLabel.color = amberGold;
                }
                else
                {
                    mementoBadgeLabel.text = "-   ARCHIVE MEMENTO : UNFOUND   -\n<size=13><color=#76849E>Scout forgotten ledges with radiant flare to reveal the ancient seal.</color></size>";
                    mementoBadgeLabel.color = moonlightSilver;
                }
            }
        }

        public void ResetDefaults()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsValues();
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
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
            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(MenuPreferences.Fullscreen);
                UpdateSwitchVisual(fullscreenToggle);
            }
            if (vsyncToggle != null)
            {
                vsyncToggle.SetIsOnWithoutNotify(MenuPreferences.VSync);
                UpdateSwitchVisual(vsyncToggle);
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
                txt.text = toggle.isOn ? "[ ON ]" : "[ OFF ]";
                txt.color = toggle.isOn ? amberGold : mutedIndigo;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = toggle.isOn
                    ? new Color(0.95f, 0.62f, 0.18f, 0.25f)
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

            // Fullscreen Midnight Ethereal Depth Vignette
            var backdrop = new GameObject("Backdrop", typeof(Image));
            backdrop.transform.SetParent(canvasGo.transform, false);
            var bRect = backdrop.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.sizeDelta = Vector2.zero;
            var bImg = backdrop.GetComponent<Image>();
            bImg.sprite = vignetteBlurSprite;
            bImg.color = Color.white;
            bImg.raycastTarget = true;

            // Central Frameless Modal Container
            var modal = new GameObject("ModalContainer", typeof(RectTransform));
            modal.transform.SetParent(canvasGo.transform, false);
            var mRect = modal.GetComponent<RectTransform>();
            mRect.anchorMin = mRect.anchorMax = new Vector2(0.5f, 0.5f);
            mRect.pivot = new Vector2(0.5f, 0.5f);
            mRect.sizeDelta = new Vector2(920f, 860f);
            mRect.anchoredPosition = Vector2.zero;
            modalRect = mRect;

            mainHeader = new GameObject("MainHeader", typeof(RectTransform));
            mainHeader.transform.SetParent(modal.transform, false);
            var headerRect = mainHeader.GetComponent<RectTransform>();
            headerRect.anchorMin = headerRect.anchorMax = new Vector2(0.5f, 0.5f);
            headerRect.sizeDelta = new Vector2(920f, 400f);
            headerRect.anchoredPosition = Vector2.zero;

            // Soft Radial Moonlight Glow behind Title
            var titleGlow = CreateAnchoredRect("Title Soft Lantern Glow", mainHeader.transform, new Vector2(760f, 240f), new Vector2(0.5f, 0.5f), new Vector2(0f, 310f));
            var glowImg = titleGlow.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = new Color(0.65f, 0.78f, 0.96f, 0.16f);
            glowImg.raycastTarget = false;
            titleGlowRect = titleGlow;

            // Top Ornate Filigree Crest
            var topCrest = CreateAnchoredRect("Top Ornate Crest", mainHeader.transform, new Vector2(460f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0f, 385f));
            var topCrestImg = topCrest.gameObject.AddComponent<Image>();
            topCrestImg.sprite = topFiligreeSprite != null ? topFiligreeSprite : filigreeSprite;
            topCrestImg.color = moonlightSilver;
            topCrestImg.raycastTarget = false;

            // Whispered Eyebrow
            var eyebrow = CreateLabel("Eyebrow", mainHeader.transform, new Vector2(0f, 345f), new Vector2(620f, 24f),
                "--   GAME PAUSED   --", 11.5f, TextAlignmentOptions.Center, moonlightSilver);
            eyebrow.characterSpacing = 6.5f;

            // Monumental Center Title: PAUSED
            var title = CreateLabel("Title", mainHeader.transform, new Vector2(0f, 300f), new Vector2(640f, 60f),
                "PAUSED", 52f, TextAlignmentOptions.Center, radiantIvory);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 10f;

            // Lower Ornate Filigree Divider under Title
            var midCrest = CreateAnchoredRect("Title Bottom Filigree", mainHeader.transform, new Vector2(520f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 252f));
            var midCrestImg = midCrest.gameObject.AddComponent<Image>();
            midCrestImg.sprite = bottomCrestSprite != null ? bottomCrestSprite : filigreeSprite;
            midCrestImg.color = moonlightSilver;
            midCrestImg.raycastTarget = false;

            // Chamber Title Whisper
            chamberTitleLabel = CreateLabel("ChamberTitle", mainHeader.transform, new Vector2(0f, 222f), new Vector2(640f, 26f),
                "CHAMBER 01-02   -   THE FIRST LIGHT", 13.5f, TextAlignmentOptions.Center, amberGold);
            chamberTitleLabel.characterSpacing = 4f;

            // Memento Recovery Badge
            mementoBadgeLabel = CreateLabel("MementoBadge", mainHeader.transform, new Vector2(0f, 172f), new Vector2(680f, 50f),
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
            mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(CanvasGroup));
            mainPanel.transform.SetParent(parent, false);
            mainPanelGroup = mainPanel.GetComponent<CanvasGroup>();
            var r = mainPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(680f, 440f);
            r.anchoredPosition = new Vector2(0f, -80f);

            float startY = 100f;
            float stepY = 66f;

            resumeButton = CreateFramelessMenuButton("ResumeButton", r, "Resume", "Return to game", new Vector2(0f, startY), Resume, isPrimary: true);
            var controlsBtn = CreateFramelessMenuButton("ControlsButton", r, "Controls", "Keybindings & movement mechanics", new Vector2(0f, startY - stepY), ShowControlsView);
            var settingsBtn = CreateFramelessMenuButton("SettingsButton", r, "Settings", "Audio, display & motion preferences", new Vector2(0f, startY - stepY * 2), ShowSettingsView);
            var restartBtn = CreateFramelessMenuButton("RestartButton", r, "Restart Level", "Restart from the last checkpoint", new Vector2(0f, startY - stepY * 3), RestartChamber);
            var quitBtn = CreateFramelessMenuButton("QuitButton", r, "Quit to Title", "Save preferences and return to title screen", new Vector2(0f, startY - stepY * 4), QuitToMainMenu);

            var navButtons = new[] { resumeButton, controlsBtn, settingsBtn, restartBtn, quitBtn };
            for (int i = 0; i < navButtons.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = navButtons[(i - 1 + navButtons.Length) % navButtons.Length],
                    selectOnDown = navButtons[(i + 1) % navButtons.Length]
                };
                navButtons[i].navigation = nav;
            }
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasGroup));
            settingsPanel.transform.SetParent(parent, false);
            settingsPanelGroup = settingsPanel.GetComponent<CanvasGroup>();
            var r = settingsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(780f, 500f);
            r.anchoredPosition = new Vector2(0f, -80f);

            var titleLabel = CreateLabel("SettingsTitle", settingsPanel.transform, new Vector2(0f, 175f), new Vector2(600f, 32f),
                "SETTINGS", 22f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            var crest = CreateAnchoredRect("Settings Filigree", settingsPanel.transform, new Vector2(480f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, 145f));
            var crestImg = crest.gameObject.AddComponent<Image>();
            crestImg.sprite = bottomCrestSprite != null ? bottomCrestSprite : filigreeSprite;
            crestImg.color = amberGold;
            crestImg.raycastTarget = false;

            float y = 105f;
            masterSlider = CreateSlider("MasterSlider", r, new Vector2(0f, y), "Master Volume", "Overall audio level", v =>
            {
                MenuPreferences.SetAudio(v, MenuPreferences.Music, MenuPreferences.Effects);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(masterSlider);
            });

            y -= 48f;
            musicSlider = CreateSlider("MusicSlider", r, new Vector2(0f, y), "Ambient Music", "Music and atmospheric score", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, v, MenuPreferences.Effects);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(musicSlider);
            });

            y -= 48f;
            effectsSlider = CreateSlider("EffectsSlider", r, new Vector2(0f, y), "Sound Effects (SFX)", "Footsteps, lantern and interactions", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, MenuPreferences.Music, v);
                MenuPreferences.Save();
                UpdateSliderPercentLabel(effectsSlider);
            });

            y -= 48f;
            fullscreenToggle = CreateToggle("FullscreenToggle", r, new Vector2(0f, y), "Fullscreen Mode", "Display in fullscreen", b =>
            {
                MenuPreferences.SetFullscreen(b);
                MenuPreferences.Save();
                UpdateSwitchVisual(fullscreenToggle);
            });

            y -= 48f;
            vsyncToggle = CreateToggle("VSyncToggle", r, new Vector2(0f, y), "Vertical Sync (V-Sync)", "Match display refresh rate", b =>
            {
                MenuPreferences.SetVSync(b);
                MenuPreferences.Save();
                UpdateSwitchVisual(vsyncToggle);
            });

            y -= 48f;
            reducedMotionToggle = CreateToggle("ReducedMotionToggle", r, new Vector2(0f, y), "Reduced Motion Mode", "Reduce camera shake and UI motion", b =>
            {
                MenuPreferences.SetReducedMotion(b);
                MenuPreferences.Save();
                UpdateSwitchVisual(reducedMotionToggle);
            });

            CreateEtherealActionButton("ResetDefaultsButton", r, "Reset Defaults", new Vector2(-160f, -205f), ResetDefaults, isPrimary: false, width: 230f);
            settingsBackButton = CreateEtherealActionButton("SettingsBackButton", r, "<  Return to Pause", new Vector2(160f, -205f), ShowMainView, isPrimary: true, width: 260f);
        }

        private readonly TMP_Text[] pauseControlsRowKeyLabels = new TMP_Text[6];
        private readonly Image[] pauseControlsRowIcons = new Image[6];
        private readonly Button[] pauseControlsDeviceTabs = new Button[3];
        private readonly TMP_Text[] pauseControlsDeviceTabLabels = new TMP_Text[3];
        private int selectedPauseControlsDeviceTab = 0;
        private Button pausePromptStyleButton;
        private TMP_Text pausePromptStyleText;
        private Button pauseVibrationButton;
        private TMP_Text pauseVibrationText;

        private void BuildControlsPanel(Transform parent)
        {
            controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform), typeof(CanvasGroup));
            controlsPanel.transform.SetParent(parent, false);
            controlsPanelGroup = controlsPanel.GetComponent<CanvasGroup>();
            var r = controlsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(780f, 530f);
            r.anchoredPosition = new Vector2(0f, -70f);

            var titleLabel = CreateLabel("ControlsTitle", controlsPanel.transform, new Vector2(0f, 210f), new Vector2(600f, 30f),
                "CONTROLS & GAMEPAD", 21f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            // 3-way Device Switcher at top
            var tabRow = CreateAnchoredRect("Pause Controls Tabs", controlsPanel.transform, new Vector2(680f, 36f), Vector2.one * 0.5f, new Vector2(0f, 172f));
            string[] deviceTabNames = { "KEYBOARD", "XBOX CONTROLLER", "PLAYSTATION" };
            float[] tabWidths = { 180f, 190f, 180f };
            float[] tabX = { -200f, 0f, 200f };

            for (int i = 0; i < 3; i++)
            {
                int tabIdx = i;
                pauseControlsDeviceTabs[i] = CreateEtherealActionButton("Tab_" + i, tabRow, deviceTabNames[i], new Vector2(tabX[i], 0f), () => SetPauseControlsDeviceTab(tabIdx), isPrimary: false, width: tabWidths[i]);
                pauseControlsDeviceTabLabels[i] = pauseControlsDeviceTabs[i].GetComponentInChildren<TMP_Text>();
                if (pauseControlsDeviceTabLabels[i] != null) pauseControlsDeviceTabLabels[i].fontSize = 12f;
            }

            float y = 120f;
            float step = 40f;
            pauseControlsRowKeyLabels[0] = CreateControlRow(r, "[ A / D  or  ARROWS ]", "MOVE", "Walk left and right", y, 0);
            pauseControlsRowKeyLabels[1] = CreateControlRow(r, "[ SPACE  /  W ]", "JUMP", "Jump and variable jump height", y - step, 1);
            pauseControlsRowKeyLabels[2] = CreateControlRow(r, "[ LEFT SHIFT ]", "SPRINT", "Run faster (consumes stamina)", y - step * 2, 2);
            pauseControlsRowKeyLabels[3] = CreateControlRow(r, "[ F  /  L-CLICK ]", "LANTERN FLARE", "Light pulse to reveal hidden surroundings", y - step * 3, 3);
            pauseControlsRowKeyLabels[4] = CreateControlRow(r, "[ E  (HOLD) ]", "INTERACT", "Activate ember veins and mechanisms", y - step * 4, 4);
            pauseControlsRowKeyLabels[5] = CreateControlRow(r, "[ ESC / P ]", "PAUSE MENU", "Open pause menu and controls guide", y - step * 5, 5);

            // Quick options bar: Prompt Style and Vibration
            pausePromptStyleButton = CreateEtherealActionButton("PausePromptStyleBtn", r, "PROMPT: < AUTO >", new Vector2(-155f, -150f), CyclePausePromptStyle, isPrimary: false, width: 280f);
            pausePromptStyleText = pausePromptStyleButton.GetComponentInChildren<TMP_Text>();
            if (pausePromptStyleText != null) pausePromptStyleText.fontSize = 12f;

            pauseVibrationButton = CreateEtherealActionButton("PauseVibrationBtn", r, "RUMBLE: < ENABLED >", new Vector2(155f, -150f), TogglePauseVibration, isPrimary: false, width: 280f);
            pauseVibrationText = pauseVibrationButton.GetComponentInChildren<TMP_Text>();
            if (pauseVibrationText != null) pauseVibrationText.fontSize = 12f;

            controlsBackButton = CreateEtherealActionButton("ControlsBackButton", r, "<  Return to Pause", new Vector2(0f, -205f), ShowMainView, isPrimary: true, width: 260f);

            var initialStyle = ControllerGlyphService.ActiveStyle;
            int defaultTab = initialStyle == ControllerPromptStyle.PlayStation ? 2 : (initialStyle == ControllerPromptStyle.Xbox ? 1 : 0);
            SetPauseControlsDeviceTab(defaultTab);
            RefreshPauseControlsQuickOptions();
        }

        private void CyclePausePromptStyle()
        {
            int current = MenuPreferences.ControllerStyle;
            int next = (current + 1) % 4;
            MenuPreferences.SetControllerStyle(next);
            RefreshPauseControlsQuickOptions();
            var active = ControllerGlyphService.ActiveStyle;
            int targetTab = active == ControllerPromptStyle.PlayStation ? 2 : (active == ControllerPromptStyle.Xbox ? 1 : 0);
            SetPauseControlsDeviceTab(targetTab);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void TogglePauseVibration()
        {
            bool current = MenuPreferences.Vibration;
            MenuPreferences.SetVibration(!current);
            RefreshPauseControlsQuickOptions();
            if (!current && Application.isPlaying)
            {
                ControllerGlyphService.Rumble(0.4f, 0.6f, 0.15f);
                SoundEffectManager.Instance?.PlayUIClick();
            }
        }

        private void RefreshPauseControlsQuickOptions()
        {
            if (pausePromptStyleText != null)
            {
                int style = MenuPreferences.ControllerStyle;
                string label = "PROMPT: < AUTO >";
                if (style == 1) label = "PROMPT: < XBOX >";
                else if (style == 2) label = "PROMPT: < PLAYSTATION >";
                else if (style == 3) label = "PROMPT: < KEYBOARD >";
                pausePromptStyleText.text = label;
            }
            if (pauseVibrationText != null)
            {
                pauseVibrationText.text = MenuPreferences.Vibration ? "RUMBLE: < ON >" : "RUMBLE: < OFF >";
            }
        }

        public void SetPauseControlsDeviceTab(int tab)
        {
            selectedPauseControlsDeviceTab = Mathf.Clamp(tab, 0, 2);
            for (int i = 0; i < 3; i++)
            {
                bool active = (i == selectedPauseControlsDeviceTab);
                if (pauseControlsDeviceTabLabels[i] != null) pauseControlsDeviceTabLabels[i].color = active ? amberGold : radiantIvory;
            }

            if (pauseControlsRowKeyLabels[0] == null) return;

            if (selectedPauseControlsDeviceTab == 0) // Keyboard
            {
                for (int i = 0; i < pauseControlsRowIcons.Length; i++)
                    if (pauseControlsRowIcons[i] != null) pauseControlsRowIcons[i].color = Color.clear;

                pauseControlsRowKeyLabels[0].text = "[ A / D  or  ARROWS ]";
                pauseControlsRowKeyLabels[1].text = "[ SPACE  /  W ]";
                pauseControlsRowKeyLabels[2].text = "[ LEFT SHIFT ]";
                pauseControlsRowKeyLabels[3].text = "[ F  /  L-CLICK ]";
                pauseControlsRowKeyLabels[4].text = "[ E  (HOLD) ]";
                pauseControlsRowKeyLabels[5].text = "[ ESC / P ]";
            }
            else if (selectedPauseControlsDeviceTab == 1) // Xbox
            {
                string[] xbIcons = { "LeftStick", "A", "RT", "X", "Y", "Menu" };
                for (int i = 0; i < pauseControlsRowIcons.Length; i++)
                {
                    if (pauseControlsRowIcons[i] != null)
                    {
                        pauseControlsRowIcons[i].sprite = ControllerGlyphService.GetGlyphSprite(xbIcons[i], ControllerPromptStyle.Xbox);
                        pauseControlsRowIcons[i].color = Color.white;
                    }
                }

                pauseControlsRowKeyLabels[0].text = "[ L-STICK / D-PAD ]";
                pauseControlsRowKeyLabels[1].text = "[ A ]  (SOUTH)";
                pauseControlsRowKeyLabels[2].text = "[ RT / LS ]";
                pauseControlsRowKeyLabels[3].text = "[ RT / X ]";
                pauseControlsRowKeyLabels[4].text = "[ Y / B ]";
                pauseControlsRowKeyLabels[5].text = "[ MENU ]";
            }
            else // PlayStation
            {
                string[] psIcons = { "LeftStick", "Cross", "R2", "Square", "Triangle", "Options" };
                for (int i = 0; i < pauseControlsRowIcons.Length; i++)
                {
                    if (pauseControlsRowIcons[i] != null)
                    {
                        pauseControlsRowIcons[i].sprite = ControllerGlyphService.GetGlyphSprite(psIcons[i], ControllerPromptStyle.PlayStation);
                        pauseControlsRowIcons[i].color = Color.white;
                    }
                }

                pauseControlsRowKeyLabels[0].text = "[ L-STICK / D-PAD ]";
                pauseControlsRowKeyLabels[1].text = "[ CROSS ]";
                pauseControlsRowKeyLabels[2].text = "[ R2 / L3 ]";
                pauseControlsRowKeyLabels[3].text = "[ R2 / SQUARE ]";
                pauseControlsRowKeyLabels[4].text = "[ TRIANGLE / CIRCLE ]";
                pauseControlsRowKeyLabels[5].text = "[ OPTIONS ]";
            }
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
            var cardSize = new Vector2(isPrimary ? 540f : 500f, isPrimary ? 62f : 54f);
            var itemRect = CreateAnchoredRect(name, parent, cardSize, new Vector2(0.5f, 0.5f), pos);

            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(cardSize.x + 80f, cardSize.y + 36f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            var plateRect = CreateStretchRect("Card Plate", itemRect);
            var plateImg = plateRect.gameObject.AddComponent<Image>();
            plateImg.sprite = thinBorderSprite;
            plateImg.type = Image.Type.Sliced;
            plateImg.color = isPrimary ? new Color(0.025f, 0.040f, 0.065f, 0.78f) : new Color(0.012f, 0.020f, 0.038f, 0.68f);
            plateImg.raycastTarget = true;

            var borderRect = CreateStretchRect("Card Border", itemRect);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = isPrimary ? new Color(0.92f, 0.68f, 0.28f, 0.45f) : new Color(0.48f, 0.62f, 0.82f, 0.25f);
            borderImg.raycastTarget = false;

            var wingContainer = CreateStretchRect("Ornate Wings", itemRect);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = isPrimary ? 0.25f : 0f;
            wingGroup.blocksRaycasts = false;

            float wingOffset = isPrimary ? 295f : 275f;
            RectTransform leftWing = null;
            RectTransform rightWing = null;

            if (buttonPointerSprite != null)
            {
                leftWing = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(44f, 22f), new Vector2(0.5f, 0.5f), new Vector2(-wingOffset, 0f));
                var leftImg = leftWing.gameObject.AddComponent<Image>();
                leftImg.sprite = buttonPointerSprite;
                leftImg.color = radiantIvory;
                leftImg.raycastTarget = false;

                rightWing = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(44f, 22f), new Vector2(0.5f, 0.5f), new Vector2(wingOffset, 0f));
                rightWing.localScale = new Vector3(-1f, 1f, 1f);
                var rightImg = rightWing.gameObject.AddComponent<Image>();
                rightImg.sprite = buttonPointerSprite;
                rightImg.color = radiantIvory;
                rightImg.raycastTarget = false;

                var lSpark = CreateAnchoredRect("Left Ember Spark", leftWing, new Vector2(6f, 6f), new Vector2(0.5f, 0.5f), new Vector2(21f, 0f));
                lSpark.localEulerAngles = new Vector3(0f, 0f, 45f);
                var lSparkImg = lSpark.gameObject.AddComponent<Image>();
                lSparkImg.color = amberGold;
                lSparkImg.raycastTarget = false;

                var rSpark = CreateAnchoredRect("Right Ember Spark", rightWing, new Vector2(6f, 6f), new Vector2(0.5f, 0.5f), new Vector2(21f, 0f));
                rSpark.localEulerAngles = new Vector3(0f, 0f, 45f);
                var rSparkImg = rSpark.gameObject.AddComponent<Image>();
                rSparkImg.color = amberGold;
                rSparkImg.raycastTarget = false;
            }
            else
            {
                leftWing = CreateOrnateWingGraphic("Left Wing", wingContainer, new Vector2(-wingOffset, 0f), isLeft: true);
                rightWing = CreateOrnateWingGraphic("Right Wing", wingContainer, new Vector2(wingOffset, 0f), isLeft: false);
            }

            var titleTxt = CreateLabel("Title", itemRect, new Vector2(0f, 8f), new Vector2(cardSize.x - 40f, 28f),
                title.ToUpperInvariant(), isPrimary ? 20.5f : 17.5f, TextAlignmentOptions.Center, isPrimary ? amberGold : radiantIvory);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 6f : 4.5f;

            var subTxt = CreateLabel("Subtitle", itemRect, new Vector2(0f, -12f), new Vector2(cardSize.x - 40f, 18f),
                subtitle, 11f, TextAlignmentOptions.Center, moonlightSilver);
            subTxt.characterSpacing = 1.5f;

            var lineRect = CreateAnchoredRect("Underline Filigree", itemRect, new Vector2(0f, 2.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, isPrimary ? -23f : -20f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            var btn = itemRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = plateImg;
            btn.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
                    onClick.Invoke();
                });
            }

            var motion = itemRect.gameObject.AddComponent<MenuButtonMotion>();
            motion.CardPlate = plateImg;
            motion.BorderFrame = borderImg;
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

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width + 60f, 64f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = isPrimary ? new Color(1.0f, 0.66f, 0.20f, 0.22f) : Color.clear;
            glowImg.raycastTarget = false;

            var plateRect = CreateStretchRect("Card Plate", rect);
            var plateImg = plateRect.gameObject.AddComponent<Image>();
            plateImg.sprite = thinBorderSprite;
            plateImg.type = Image.Type.Sliced;
            plateImg.color = isPrimary ? new Color(0.025f, 0.040f, 0.065f, 0.78f) : new Color(0.012f, 0.020f, 0.038f, 0.68f);
            plateImg.raycastTarget = true;

            var borderRect = CreateStretchRect("Card Border", rect);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = isPrimary ? new Color(0.92f, 0.68f, 0.28f, 0.45f) : new Color(0.48f, 0.62f, 0.82f, 0.25f);
            borderImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(0f, 2.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -17f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            var txt = CreateLabel("Caption", rect, Vector2.zero, new Vector2(width - 20f, 36f),
                label.ToUpperInvariant(), 14f, TextAlignmentOptions.Center, isPrimary ? amberGold : radiantIvory);
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3.5f;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = plateImg;
            btn.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
                    onClick.Invoke();
                });
            }

            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.CardPlate = plateImg;
            motion.BorderFrame = borderImg;
            motion.Text = txt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;

            return btn;
        }

        private Slider CreateSlider(string name, RectTransform parent, Vector2 pos, string title, string subtitle, UnityAction<float> onValueChanged)
        {
            var row = CreateAnchoredRect(name, parent, new Vector2(740f, 48f), new Vector2(0.5f, 0.5f), pos);

            var plateRect = CreateStretchRect("Row Plate", row);
            var plateImg = plateRect.gameObject.AddComponent<Image>();
            plateImg.sprite = thinBorderSprite;
            plateImg.type = Image.Type.Sliced;
            plateImg.color = new Color(0.010f, 0.016f, 0.030f, 0.60f);
            plateImg.raycastTarget = false;

            var borderRect = CreateStretchRect("Row Border", row);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.44f, 0.58f, 0.78f, 0.18f);
            borderImg.raycastTarget = false;

            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(740f, 2f), new Vector2(0.5f, 0f), Vector2.zero);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.20f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, new Vector2(16f, 8f), new Vector2(350f, 20f),
                title.ToUpperInvariant(), 13.5f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(16f, -9f), new Vector2(350f, 16f),
                subtitle, 10.5f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(350f, 28f);
            sRect.anchoredPosition = new Vector2(-16f, 0f);

            // Rounded capsule track
            var track = CreateAnchoredRect("Track", sRect, new Vector2(270f, 6f), new Vector2(0f, 0.5f), Vector2.zero);
            track.pivot = new Vector2(0f, 0.5f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = roundTrackSprite;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = Color.white;

            // Rounded capsule fill
            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = roundFillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Color.white;

            // Circular anti-aliased handle with radial bloom
            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(16f, 16f), new Vector2(0f, 0.5f), Vector2.zero);
            handle.localEulerAngles = Vector3.zero;

            var handleGlow = CreateAnchoredRect("Handle Glow", handle, new Vector2(28f, 28f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var hGlowImg = handleGlow.gameObject.AddComponent<Image>();
            hGlowImg.sprite = softRadialSprite;
            hGlowImg.color = new Color(1.0f, 0.75f, 0.30f, 0.35f);
            hGlowImg.raycastTarget = false;

            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = roundHandleSprite;
            handleImg.color = Color.white;

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

            var focus = row.gameObject.AddComponent<SettingsLightFocus>();
            focus.Label = titleTxt;
            focus.CardPlate = plateImg;
            focus.BorderFrame = borderImg;

            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(onValueChanged);
            }

            return slider;
        }

        private Toggle CreateToggle(string name, RectTransform parent, Vector2 pos, string title, string subtitle, UnityAction<bool> onValueChanged)
        {
            var row = CreateAnchoredRect(name, parent, new Vector2(740f, 48f), new Vector2(0.5f, 0.5f), pos);

            var plateRect = CreateStretchRect("Row Plate", row);
            var plateImg = plateRect.gameObject.AddComponent<Image>();
            plateImg.sprite = thinBorderSprite;
            plateImg.type = Image.Type.Sliced;
            plateImg.color = new Color(0.010f, 0.016f, 0.030f, 0.60f);
            plateImg.raycastTarget = true;

            var borderRect = CreateStretchRect("Row Border", row);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(0.44f, 0.58f, 0.78f, 0.18f);
            borderImg.raycastTarget = false;

            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(740f, 2f), new Vector2(0.5f, 0f), Vector2.zero);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.20f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, new Vector2(16f, 8f), new Vector2(450f, 20f),
                title.ToUpperInvariant(), 13.5f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(16f, -9f), new Vector2(450f, 16f),
                subtitle, 10.5f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var pill = CreateAnchoredRect("Pill", row, new Vector2(110f, 28f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = softRadialSprite;
            pillImg.color = Color.clear;

            var valTxt = CreateLabel("Value", pill, Vector2.zero, new Vector2(110f, 28f),
                "[ OFF ]", 12.5f, TextAlignmentOptions.Center, mutedIndigo);
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.characterSpacing = 2f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = plateImg;
            toggle.graphic = null;
            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(onValueChanged);
            }

            var focus = row.gameObject.AddComponent<SettingsLightFocus>();
            focus.Label = titleTxt;
            focus.CardPlate = plateImg;
            focus.BorderFrame = borderImg;

            return toggle;
        }

        private TMP_Text CreateControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos, int rowIndex = -1)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(740f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(740f, 2f), new Vector2(0.5f, 0f), Vector2.zero);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.22f);
            divImg.raycastTarget = false;

            var badge = CreateAnchoredRect("Keycap Badge", row, new Vector2(240f, 36f), new Vector2(0f, 0.5f), new Vector2(120f, 0f));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = softRadialSprite;
            badgeImg.color = new Color(1.0f, 0.72f, 0.28f, 0.16f);

            var iconRect = CreateAnchoredRect("Glyph Icon", badge, new Vector2(34f, 34f), new Vector2(0f, 0.5f), new Vector2(24f, 0f));
            var iconImg = iconRect.gameObject.AddComponent<Image>();
            iconImg.color = Color.clear;
            iconImg.raycastTarget = false;
            if (rowIndex >= 0 && rowIndex < pauseControlsRowIcons.Length)
                pauseControlsRowIcons[rowIndex] = iconImg;

            var keyTxt = CreateLabel("Key Text", badge, new Vector2(15f, 0f), new Vector2(180f, 32f),
                keys, 12.5f, TextAlignmentOptions.Center, amberGold);
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 1.5f;

            var actTxt = CreateLabel("Action", row, new Vector2(255f, 9f), new Vector2(460f, 20f),
                actionTitle, 13.5f, TextAlignmentOptions.Left, radiantIvory);
            actTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            actTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            actTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 2f;

            var descTxt = CreateLabel("Description", row, new Vector2(255f, -9f), new Vector2(460f, 18f),
                desc, 11.5f, TextAlignmentOptions.Left, moonlightSilver);
            descTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            descTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            descTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            return keyTxt;
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

        private Sprite CreateVignetteBlurSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = (size - 1f) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float t = Mathf.Clamp01(dist / 1.35f);
                    float smooth = t * t * (3f - 2f * t);

                    Color center = new Color(0.015f, 0.024f, 0.045f, 0.72f);
                    Color edge = new Color(0.006f, 0.009f, 0.018f, 0.98f);
                    Color c = Color.Lerp(center, edge, smooth);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateRoundedCapsuleSprite(int width, int height, Color col, int sliceMargin)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float halfY = (height - 1f) * 0.5f;
            float radius = halfY;
            float leftCenter = radius;
            float rightCenter = (width - 1f) - radius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist;
                    if (x < leftCenter)
                    {
                        float dx = x - leftCenter;
                        float dy = y - halfY;
                        dist = Mathf.Sqrt(dx * dx + dy * dy);
                    }
                    else if (x > rightCenter)
                    {
                        float dx = x - rightCenter;
                        float dy = y - halfY;
                        dist = Mathf.Sqrt(dx * dx + dy * dy);
                    }
                    else
                    {
                        dist = Mathf.Abs(y - halfY);
                    }

                    float cov = Mathf.Clamp01(radius + 0.5f - dist);
                    Color c = new Color(col.r, col.g, col.b, col.a * cov);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            Vector4 border = new Vector4(sliceMargin, 0, sliceMargin, 0);
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, border);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateThinBorderBoxSprite(int width, int height, int cornerRadius, float borderWidth)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = Mathf.Max(1f, cornerRadius);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = 0f;
                    if (x < r) dx = r - x;
                    else if (x > (width - 1f) - r) dx = x - ((width - 1f) - r);

                    float dy = 0f;
                    if (y < r) dy = r - y;
                    else if (y > (height - 1f) - r) dy = y - ((height - 1f) - r);

                    float distToOuter;
                    if (dx > 0f && dy > 0f)
                    {
                        float dCorner = Mathf.Sqrt(dx * dx + dy * dy);
                        distToOuter = r - dCorner;
                    }
                    else
                    {
                        float dEdgeX = Mathf.Min(x, (width - 1f) - x);
                        float dEdgeY = Mathf.Min(y, (height - 1f) - y);
                        distToOuter = Mathf.Min(dEdgeX, dEdgeY);
                    }

                    float outerCov = Mathf.Clamp01(distToOuter + 0.5f);
                    if (outerCov <= 0.001f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float borderAlpha = Mathf.Clamp01((borderWidth + 0.5f) - distToOuter);
                    float fillAlpha = 0.22f;
                    float a = Mathf.Lerp(fillAlpha, 1.0f, borderAlpha) * outerCov;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            int slice = Mathf.Max(cornerRadius + 2, 6);
            Vector4 border = new Vector4(slice, slice, slice, slice);
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, border);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateCircularHandleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = (size - 1f) * 0.5f;
            float radius = half - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(radius + 0.5f - dist);
                    if (cov <= 0.001f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float t = dist / radius;
                    Color core = new Color(0.98f, 0.98f, 1.0f, 1f);
                    Color rim = amberGold;
                    Color c = Color.Lerp(core, rim, t * 0.75f);
                    c.a *= cov;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateOrnateFiligreeSprite(int w = 512, int h = 64)
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
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 100f);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private Sprite CreateSoftRadialSprite(int size = 128)
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
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            proceduralSprites.Add(sprite);
            return sprite;
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

        private void PlaySound(AudioClip clip, float freq1, float freq2, bool isOpen)
        {
            AudioClip sound = clip;
            if (sound == null)
            {
                if (isOpen)
                {
                    if (cachedOpenClip == null) cachedOpenClip = GetProceduralPauseClip(freq1, freq2);
                    sound = cachedOpenClip;
                }
                else
                {
                    if (cachedCloseClip == null) cachedCloseClip = GetProceduralPauseClip(freq1, freq2);
                    sound = cachedCloseClip;
                }
            }

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

        private void AnimateSubView(CanvasGroup targetGroup)
        {
            if (targetGroup == null) return;
            if (subViewRoutine != null)
            {
                StopCoroutine(subViewRoutine);
                subViewRoutine = null;
            }
            if (isActiveAndEnabled)
            {
                subViewRoutine = StartCoroutine(SubViewFadeRoutine(targetGroup));
            }
            else
            {
                targetGroup.alpha = 1f;
            }
        }

        private IEnumerator SubViewFadeRoutine(CanvasGroup group)
        {
            float duration = MenuPreferences.ReducedMotion ? 0.04f : 0.14f;
            float elapsed = 0f;
            group.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = EaseOutCubic(t);
                yield return null;
            }

            group.alpha = 1f;
            subViewRoutine = null;
        }

        private IEnumerator AnimateOpenRoutine()
        {
            float duration = MenuPreferences.ReducedMotion ? 0.05f : 0.22f;
            float elapsed = 0f;

            SetOpenVisualState(0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetOpenVisualState(t);
                yield return null;
            }

            SetOpenVisualState(1f);
            transitionRoutine = null;
        }

        private IEnumerator AnimateCloseRoutine()
        {
            float duration = MenuPreferences.ReducedMotion ? 0.04f : 0.14f;
            float elapsed = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = EaseInCubic(t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, smooth);
                }
                if (modalRect != null)
                {
                    float scale = Mathf.Lerp(1.0f, 0.95f, smooth);
                    modalRect.localScale = new Vector3(scale, scale, 1f);
                    modalRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -14f, smooth));
                }
                yield return null;
            }

            SetClosedVisualState();
            transitionRoutine = null;
        }

        private void SetOpenVisualState(float t)
        {
            float smooth = EaseOutCubic(t);
            if (canvasGroup != null) canvasGroup.alpha = smooth;
            if (modalRect != null)
            {
                float scale = Mathf.Lerp(0.92f, 1.0f, smooth);
                modalRect.localScale = new Vector3(scale, scale, 1f);
                modalRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-24f, 0f, smooth));
            }
            if (titleGlowRect != null)
            {
                float glowScale = Mathf.Lerp(0.70f, 1.0f, smooth);
                titleGlowRect.localScale = new Vector3(glowScale, glowScale, 1f);
            }
        }

        private void SetClosedVisualState()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (modalRect != null)
            {
                modalRect.localScale = Vector3.one;
                modalRect.anchoredPosition = Vector2.zero;
            }
            if (titleGlowRect != null)
            {
                titleGlowRect.localScale = Vector3.one;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }
    }
}
