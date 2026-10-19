using System;
using System.Collections;
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
    /// Manages game freezing (Time.timeScale = 0), player motor/lantern input suspension,
    /// in-game audio/accessibility settings, controls reference, chamber restart,
    /// archive memento status, and smooth transition back to MainMenu.
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

        private string currentView = "none";
        private float previousTimeScale = 1f;
        private readonly System.Collections.Generic.Dictionary<PlayerMotor2D, bool> suspendedMotors = new();
        private readonly System.Collections.Generic.Dictionary<LanternController, bool> suspendedLanterns = new();
        private static AudioClip proceduralPauseClip;

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
        }

        public void ShowSettingsView()
        {
            currentView = "settings";
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            SyncSettingsValues();
        }

        public void ShowControlsView()
        {
            currentView = "controls";
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(true);
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
                chamberName = "CHAMBER 01-02 / FIRST LIGHT";
            }
            else if (sceneName.Contains("ArchiveTest"))
            {
                chamberName = "CHAMBER 03 / DEEP ARCHIVE";
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
                    mementoBadgeLabel.text = "✦ ARCHIVE MEMENTO RECOVERED\n<size=14><color=#b5c4a5>\"The deep stone whispers of paths that open only in high flame.\"</color></size>";
                    mementoBadgeLabel.color = new Color(1f, 0.85f, 0.45f, 1f);
                }
                else
                {
                    mementoBadgeLabel.text = "◇ ARCHIVE MEMENTO: UNFOUND\n<size=14><color=#758280>Look for ancient seal runes in high, forgotten ledges.</color></size>";
                    mementoBadgeLabel.color = new Color(0.6f, 0.65f, 0.64f, 0.75f);
                }
            }
        }

        private void SyncSettingsValues()
        {
            if (masterSlider != null) masterSlider.value = MenuPreferences.Master;
            if (musicSlider != null) musicSlider.value = MenuPreferences.Music;
            if (effectsSlider != null) effectsSlider.value = MenuPreferences.Effects;
            if (reducedMotionToggle != null) reducedMotionToggle.isOn = MenuPreferences.ReducedMotion;
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
            pauseCanvas.sortingOrder = 95;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // Fullscreen Dark Backdrop
            var backdrop = new GameObject("Backdrop", typeof(Image));
            backdrop.transform.SetParent(canvasGo.transform, false);
            var bRect = backdrop.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.sizeDelta = Vector2.zero;
            var bImg = backdrop.GetComponent<Image>();
            bImg.color = new Color(0.04f, 0.06f, 0.07f, 0.90f);

            // Container Modal
            var modal = new GameObject("ModalContainer", typeof(RectTransform));
            modal.transform.SetParent(canvasGo.transform, false);
            var mRect = modal.GetComponent<RectTransform>();
            mRect.anchorMin = mRect.anchorMax = new Vector2(0.5f, 0.5f);
            mRect.sizeDelta = new Vector2(760, 680);

            // Header: PAUSED
            var title = CreateLabel("Title", modal.transform, new Vector2(0, 260), new Vector2(600, 60), "PAUSED", 44, TextAlignmentOptions.Center, new Color(0.96f, 0.92f, 0.82f));
            title.characterSpacing = 8f;

            chamberTitleLabel = CreateLabel("ChamberTitle", modal.transform, new Vector2(0, 215), new Vector2(600, 36), "CHAMBER / RUNES", 18, TextAlignmentOptions.Center, new Color(0.48f, 0.82f, 0.74f));
            chamberTitleLabel.characterSpacing = 5f;

            // Memento Badge
            mementoBadgeLabel = CreateLabel("MementoBadge", modal.transform, new Vector2(0, 155), new Vector2(660, 60), "", 16, TextAlignmentOptions.Center, Color.white);

            // Sub-panels
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
            r.sizeDelta = new Vector2(600, 380);
            r.anchoredPosition = new Vector2(0, -60);

            float startY = 80f;
            float stepY = 56f;

            CreateMenuButton("ResumeButton", mainPanel.transform, new Vector2(0, startY), "RESUME", Resume);
            CreateMenuButton("ControlsButton", mainPanel.transform, new Vector2(0, startY - stepY), "CONTROLS & INPUT", ShowControlsView);
            CreateMenuButton("SettingsButton", mainPanel.transform, new Vector2(0, startY - stepY * 2), "SETTINGS & AUDIO", ShowSettingsView);
            CreateMenuButton("RestartButton", mainPanel.transform, new Vector2(0, startY - stepY * 3), "RESTART CHAMBER", RestartChamber);
            CreateMenuButton("QuitButton", mainPanel.transform, new Vector2(0, startY - stepY * 4), "QUIT TO MAIN MENU", QuitToMainMenu);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(parent, false);
            var r = settingsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(640, 380);
            r.anchoredPosition = new Vector2(0, -60);

            CreateLabel("SettingsTitle", settingsPanel.transform, new Vector2(0, 110), new Vector2(500, 36), "AUDIO & COMFORT", 22, TextAlignmentOptions.Center, new Color(0.96f, 0.88f, 0.65f));

            float y = 50f;
            masterSlider = CreateSlider("MasterSlider", settingsPanel.transform, new Vector2(0, y), "Master Volume", v => {
                MenuPreferences.SetAudio(v, MenuPreferences.Music, MenuPreferences.Effects);
                MenuPreferences.Save();
            });

            y -= 54f;
            musicSlider = CreateSlider("MusicSlider", settingsPanel.transform, new Vector2(0, y), "Music Volume", v => {
                MenuPreferences.SetAudio(MenuPreferences.Master, v, MenuPreferences.Effects);
                MenuPreferences.Save();
            });

            y -= 54f;
            effectsSlider = CreateSlider("EffectsSlider", settingsPanel.transform, new Vector2(0, y), "SFX Volume", v => {
                MenuPreferences.SetAudio(MenuPreferences.Master, MenuPreferences.Music, v);
                MenuPreferences.Save();
            });

            y -= 54f;
            reducedMotionToggle = CreateToggle("ReducedMotionToggle", settingsPanel.transform, new Vector2(0, y), "Reduced Motion Mode", b => {
                MenuPreferences.SetReducedMotion(b);
                MenuPreferences.Save();
            });

            CreateMenuButton("SettingsBackButton", settingsPanel.transform, new Vector2(0, -140), "◄ BACK TO MENU", ShowMainView);
        }

        private void BuildControlsPanel(Transform parent)
        {
            controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
            controlsPanel.transform.SetParent(parent, false);
            var r = controlsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(640, 380);
            r.anchoredPosition = new Vector2(0, -60);

            CreateLabel("ControlsTitle", controlsPanel.transform, new Vector2(0, 115), new Vector2(500, 36), "CONTROLS & ACTIONS", 22, TextAlignmentOptions.Center, new Color(0.96f, 0.88f, 0.65f));

            string guide =
                "<color=#e2dcce><b>A / D</b> or <b>← / →</b></color>   —   Walk / Move\n" +
                "<color=#e2dcce><b>SPACE</b></color>   —   Jump\n" +
                "<color=#e2dcce><b>SHIFT</b></color>   —   Sprint (consumes stamina)\n" +
                "<color=#ffd166><b>F</b></color>   —   <b>FLARE</b> Scout (charges ancient glyphs, spends 1 ember)\n" +
                "<color=#4ef2d2><b>E</b></color>   —   Ember Circuit Deposit / Recovery\n" +
                "<color=#e2dcce><b>ESC / P</b></color>   —   Pause Menu / Resume\n" +
                "<color=#e2dcce><b>BACKSPACE</b></color>   —   Quick Chamber Reset";

            var guideLabel = CreateLabel("ControlsBody", controlsPanel.transform, new Vector2(0, -10), new Vector2(620, 210), guide, 17, TextAlignmentOptions.Center, new Color(0.85f, 0.88f, 0.87f));
            guideLabel.lineSpacing = 16f;

            CreateMenuButton("ControlsBackButton", controlsPanel.transform, new Vector2(0, -140), "◄ BACK TO MENU", ShowMainView);
        }

        private Button CreateMenuButton(string name, Transform parent, Vector2 pos, string text, UnityAction action)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
            btnGo.transform.SetParent(parent, false);
            var r = btnGo.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(380, 46);

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.18f, 0.65f);

            var label = CreateLabel("Label", btnGo.transform, Vector2.zero, r.sizeDelta, text, 19, TextAlignmentOptions.Center, new Color(0.92f, 0.90f, 0.84f));
            label.characterSpacing = 3.5f;

            var btn = btnGo.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.15f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            btn.colors = colors;

            if (action != null) btn.onClick.AddListener(action);

            return btn;
        }

        private Slider CreateSlider(string name, Transform parent, Vector2 pos, string labelText, UnityAction<float> onValueChanged)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var r = root.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(500, 36);

            CreateLabel("Name", root.transform, new Vector2(-150, 0), new Vector2(180, 30), labelText, 16, TextAlignmentOptions.Left, new Color(0.85f, 0.88f, 0.86f));

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(root.transform, false);
            var sRect = sliderGo.GetComponent<RectTransform>();
            sRect.anchoredPosition = new Vector2(80, 0);
            sRect.sizeDelta = new Vector2(260, 20);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.24f, 0.8f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.color = new Color(0.38f, 0.88f, 0.76f, 0.95f);
            slider.fillRect = fillRect;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14, 24);
            var handleImg = handle.GetComponent<Image>();
            handleImg.color = new Color(0.96f, 0.94f, 0.86f, 1f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            if (onValueChanged != null) slider.onValueChanged.AddListener(onValueChanged);

            return slider;
        }

        private Toggle CreateToggle(string name, Transform parent, Vector2 pos, string labelText, UnityAction<bool> onValueChanged)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            var r = root.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(400, 36);

            var toggle = root.GetComponent<Toggle>();

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            var bRect = box.GetComponent<RectTransform>();
            bRect.anchoredPosition = new Vector2(-150, 0);
            bRect.sizeDelta = new Vector2(24, 24);
            box.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.28f, 0.9f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(box.transform, false);
            var cRect = check.GetComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(14, 14);
            var cImg = check.GetComponent<Image>();
            cImg.color = new Color(0.42f, 0.95f, 0.82f, 1f);

            toggle.graphic = cImg;
            toggle.targetGraphic = box.GetComponent<Image>();

            CreateLabel("Label", root.transform, new Vector2(10, 0), new Vector2(280, 30), labelText, 16, TextAlignmentOptions.Left, new Color(0.85f, 0.88f, 0.86f));

            if (onValueChanged != null) toggle.onValueChanged.AddListener(onValueChanged);

            return toggle;
        }

        private TMP_Text CreateLabel(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            return tmp;
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
