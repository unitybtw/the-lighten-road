using TheLightenRoad.Input;
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
    /// In-game pause menu with a clear action list, current chamber and lantern status,
    /// readable settings and controls, and confirmation before restarting or leaving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PauseMenuController : MonoBehaviour
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
        private readonly Color radiantIvory = new Color32(235, 228, 212, 255);
        private readonly Color moonlightSilver = new Color32(179, 174, 163, 255);
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
        private TMP_Text currentFuelLabel, pauseActionHint;
        private Image pauseLanternFlame;

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle fullscreenToggle;
        private Toggle vsyncToggle;
        private Toggle reducedMotionToggle;

        private Button resumeButton;
        private Button settingsBackButton;
        private Button controlsBackButton;

        // Hollow Knight Controller Footer & Bumper Badges
        private RectTransform pauseFooterBar;
        private Image pauseFooterSelectIcon, pauseFooterBackIcon, pauseFooterResetIcon, pauseFooterTabIcon;
        private TMP_Text pauseFooterSelectText, pauseFooterBackText, pauseFooterResetText, pauseFooterTabText;
        private Image pauseLeftBumperImg, pauseRightBumperImg;
        private TMP_Text pauseLeftBumperText, pauseRightBumperText;

        private RectTransform modalRect;
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
        private Sprite roundTrackSprite;
        private Sprite roundFillSprite;
        private Sprite thinBorderSprite;

        private readonly List<Sprite> proceduralSprites = new List<Sprite>();


        private string currentView = "none";
        private float previousTimeScale = 1f;
        private Coroutine restoreInteractions;
        private bool destroying;

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
            roundTrackSprite = CreateRoundedCapsuleSprite(24, 8, new Color(0.55f, 0.68f, 0.86f, 0.35f), 4);
            roundFillSprite = CreateRoundedCapsuleSprite(24, 8, amberGold, 4);
            thinBorderSprite = CreateThinBorderBoxSprite(36, 36, 4, 2f);

            EnsureEventSystem();
            BuildUIHierarchy();
            ControllerGlyphService.OnPromptStyleChanged += RefreshPauseFooterPrompts;
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            FlushPauseSettingsSave();
            if (IsPaused) Resume();
            if (restoreInteractions != null) StopCoroutine(restoreInteractions);
            restoreInteractions = null;
            GameControls.BlockGameplay(this, false);
            RestoreInteractions();
        }

        private void OnDestroy()
        {
            destroying = true;
            FlushPauseSettingsSave();
            ControllerGlyphService.OnPromptStyleChanged -= RefreshPauseFooterPrompts;
            bool ownsPause = Instance == this;
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
            if (ownsPause)
            {
                if (IsPaused) Resume();
                IsPaused = false;
                Instance = null;
            }
            GameControls.BlockGameplay(this, false);
            RestoreInteractions();

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

            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            TickPauseSettingsSave();
            if (returningToTitle || DeathScreenOverlay.IsAnyVisible) return;
            if(ArchiveCodexMenu.IsOpen || (!IsPaused && ArchiveCodexMenu.ClosedThisFrame))return;
            var k = Keyboard.current;
            var g = ControllerGlyphService.GetActiveGamepad();

            bool escapePressed = k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
            bool startPressed = g != null && g.startButton.wasPressedThisFrame;
            bool eastPressed = g != null && g.buttonEast.wasPressedThisFrame;

            if (IsPaused && IsConfirmationOpen)
            {
                if (escapePressed || startPressed || eastPressed) CancelConfirmation();
                else RestoreLostPauseSelection(k, g);
                return;
            }

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
                    if (!startPressed && (currentView == "settings" || currentView == "controls"))
                    {
                        ShowMainView();
                    }
                    else
                    {
                        Resume();
                    }
                }
            }

            if (IsPaused)
            {
                RestoreLostPauseSelection(k, g);
                if (currentView == "settings")
                {
                    if ((k != null && k.qKey.wasPressedThisFrame) || (g != null && g.leftShoulder.wasPressedThisFrame)) SelectSettingsSection((pauseSettingsSection + 3) % 4);
                    else if ((k != null && k.eKey.wasPressedThisFrame) || (g != null && g.rightShoulder.wasPressedThisFrame)) SelectSettingsSection((pauseSettingsSection + 1) % 4);
                }
                if (k != null)
                {
                    if (currentView == "controls")
                    {
                        if (k.qKey.wasPressedThisFrame)
                        {
                            SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 2) % 3);
                            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                        }
                        else if (k.eKey.wasPressedThisFrame)
                        {
                            SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 1) % 3);
                            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                        }
                    }

                    if (k.rKey.wasPressedThisFrame)
                    {
                        if (currentView == "settings") RequestResetDefaults();
                    }
                }

                if (g != null)
                {
                    if (g.buttonNorth.wasPressedThisFrame)
                    {
                        if (currentView == "settings") RequestResetDefaults();
                    }

                    if (currentView == "controls")
                    {
                        if (g.leftShoulder.wasPressedThisFrame)
                        {
                            SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 2) % 3);
                            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                        }
                        else if (g.rightShoulder.wasPressedThisFrame)
                        {
                            SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 1) % 3);
                            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
                        }
                    }
                }
            }
        }

        private void RestorePauseMenuSelection()
        {
            if (EventSystem.current == null) return;
            GameObject target = null;
            if (IsConfirmationOpen && confirmationCancelButton != null) target = confirmationCancelButton.gameObject;
            else if (currentView == "main" && resumeButton != null) target = resumeButton.gameObject;
            else if (currentView == "controls" && controlsBackButton != null) target = controlsBackButton.gameObject;
            else if (currentView == "settings" && FirstPauseSetting != null) target = FirstPauseSetting.gameObject;
            if (target != null) EventSystem.current.SetSelectedGameObject(target);
        }

        public void Pause()
        {
            if (IsPaused || DeathScreenOverlay.IsAnyVisible) return;
            IsPaused = true;

            previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;

            SuspendPlayerInput(true);
            lastHomeSelection = null;
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

            PlaySound(pauseOpenSound, isOpen: true);
            ControllerGlyphService.StopRumble();
            DualSenseSpeaker.Stop();
            TheLightenRoad.UI.ControllerLightbar.SetAmbientColor(TheLightenRoad.UI.ControllerLightbar.ArchiveBlue);
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
            FlushPauseSettingsSave();
            HideConfirmation();
            IsPaused = false;

            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            SuspendPlayerInput(false);

            currentView = "none";
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            PlaySound(pauseCloseSound, isOpen: false);
            TheLightenRoad.UI.ControllerLightbar.SetAmbientColor(TheLightenRoad.UI.ControllerLightbar.AmberLantern);
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
            HideConfirmation();
            FlushPauseSettingsSave();
            currentView = "main";
            if (mainHeader != null) mainHeader.SetActive(true);
            if (mainPanel != null) mainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            UpdateChamberInfo();
            AnimateSubView(mainPanelGroup);
            RefreshPauseFooterPrompts();

            if (EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(lastHomeSelection != null && lastHomeSelection.activeInHierarchy ? lastHomeSelection : resumeButton.gameObject);
        }

        public void ShowSettingsView()
        {
            RememberHomeSelection();
            HideConfirmation();
            FlushPauseSettingsSave();
            currentView = "settings";
            if (mainHeader != null) mainHeader.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            SyncSettingsValues();
            AnimateSubView(settingsPanelGroup);
            RefreshPauseFooterPrompts();

            SelectSettingsSection(pauseSettingsSection);
        }

        public void ShowControlsView()
        {
            RememberHomeSelection();
            HideConfirmation();
            FlushPauseSettingsSave();
            currentView = "controls";
            if (mainHeader != null) mainHeader.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(true);
            AnimateSubView(controlsPanelGroup);
            RefreshPauseFooterPrompts();

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
            if (returningToTitle) return;
            if (!IsPaused) Pause();
            returningToTitle = true;
            if (transitionRoutine != null) { StopCoroutine(transitionRoutine); transitionRoutine = null; }
            FlushPauseSettingsSave();
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            // Keep the scene frozen and gameplay blocked until the destination takes over.
            if (canvasGroup != null) canvasGroup.interactable = false;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            float elapsed = 0;
            float duration = MenuPreferences.ReducedMotion ? 0 : .22f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, elapsed / duration);
                yield return null;
            }
            yield return null;
            if (Application.CanStreamedLevelBeLoaded(MainMenuScenePath))
            {
                SceneManager.LoadSceneAsync(MainMenuScenePath);
            }
            else
            {
                SceneManager.LoadSceneAsync("MainMenu");
            }
        }

        private void SuspendPlayerInput(bool suspend)
        {
            GameControls.BlockGameplay(this, suspend);
            if (restoreInteractions != null)
            {
                StopCoroutine(restoreInteractions);
                restoreInteractions = null;
            }
            if (suspend)
            {
                LegacyInteractionPollingGate.Acquire(this);
            }
            else if (destroying || !isActiveAndEnabled) RestoreInteractions();
            else restoreInteractions = StartCoroutine(RestoreInteractionsAfterMenuInput());
        }

        private IEnumerator RestoreInteractionsAfterMenuInput()
        {
            // Wait past both the menu submit frame and the gameplay input suppression frame.
            yield return null;
            yield return null;
            RestoreInteractions();
            restoreInteractions = null;
        }

        private void RestoreInteractions()
        {
            LegacyInteractionPollingGate.Release(this);
        }

        private void UpdateChamberInfo()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string chamberName = "Ruined Archive";

            if (sceneName == "FirstLightTutorial")
            {
                chamberName = "First Light";
            }
            else if (sceneName.Contains("Furkan"))
            {
                chamberName = "The First Light";
            }
            else if (sceneName.Contains("Kemal"))
            {
                chamberName = "The Flooded Mechanisms";
            }
            else if (sceneName.Contains("ArchiveTest"))
            {
                chamberName = "Archive Test Chamber";
            }

            if (chamberTitleLabel != null)
            {
                chamberTitleLabel.text = chamberName;
            }

            if (currentFuelLabel != null)
            {
                var fuel = LanternController.ActiveInstance != null ? LanternController.ActiveInstance.Fuel : null;
                currentFuelLabel.gameObject.SetActive(fuel != null);
                if (fuel != null) currentFuelLabel.text = $"{fuel.TotalEmbers} / {Mathf.FloorToInt(fuel.Capacity / 2f)} embers";
                if (pauseLanternFlame != null) pauseLanternFlame.enabled = fuel != null && !fuel.IsEmpty;
            }

            if (mementoBadgeLabel != null)
            {
                bool hasMemento = false;
                bool tracksMemento = false;
                var directors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var d in directors)
                {
                    if (d.GetType().Name == "FurkanLevelDirector")
                    {
                        var field = d.GetType().GetField("HasMemento");
                        if (field != null) { hasMemento = (bool)field.GetValue(d); tracksMemento = true; }
                    }
                }

                mementoBadgeLabel.gameObject.SetActive(tracksMemento);
                if (!tracksMemento) return;
                if (hasMemento)
                {
                    mementoBadgeLabel.text = "Archive memento\nRecovered";
                    mementoBadgeLabel.color = amberGold;
                }
                else
                {
                    mementoBadgeLabel.text = "Archive memento\nNot recovered";
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
            SyncPauseControllerSettings();
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
                txt.text = toggle.isOn ? "<  ON  >" : "<  OFF  >";
                txt.color = toggle.isOn ? amberGold : moonlightSilver;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = Color.clear;
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
            bImg.color = new Color(0.025f, 0.028f, 0.027f, 0.94f);
            bImg.raycastTarget = true;

            // Central Frameless Modal Container
            var modal = new GameObject("ModalContainer", typeof(RectTransform));
            var readableLayer = CreateStretchRect("Readable menu safe canvas", (RectTransform)canvasGo.transform);
            readableLayer.gameObject.AddComponent<MenuReadability>();
            modal.transform.SetParent(readableLayer, false);
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

            BuildPauseHeader(mainHeader.transform);

            // Sub-panels (Frameless Center Stage)
            BuildMainPanel(modal.transform);
            BuildSettingsPanel(modal.transform);
            BuildControlsPanel(modal.transform);
            BuildPauseFooter(modal.transform);
            BuildPauseConfirmation(modal.transform);
            ApplyPauseReadability();

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
            r.anchoredPosition = new Vector2(0f, 0f);

            r.sizeDelta = new Vector2(540f, 420f);
            r.anchoredPosition = new Vector2(-280f, -10f);

            resumeButton = CreatePauseAction("ResumeButton", r, "Continue", "Return to your current attempt.", 140f, Resume);
            var settingsBtn = CreatePauseAction("SettingsButton", r, "Settings", "Audio, display, controller and accessibility.", 62f, ShowSettingsView);
            var controlsBtn = CreatePauseAction("ControlsButton", r, "Button guide", "See keyboard, Xbox and PlayStation bindings.", -16f, ShowControlsView);
            var restartBtn = CreatePauseAction("RestartButton", r, "Restart chamber", "Start again from the last checkpoint.", -114f, RequestRestartChamber);
            var quitBtn = CreatePauseAction("QuitButton", r, "Return to title", "Leave this attempt and return to the title screen.", -192f, RequestQuitToMainMenu);

            var rule = CreateAnchoredRect("Recovery actions separator", r, new Vector2(480, 1), Vector2.one * .5f, new Vector2(0, -64)).gameObject.AddComponent<Image>();
            rule.color = new Color(0.7f, 0.62f, 0.44f, .22f);
            rule.raycastTarget = false;
            var navButtons = new[] { resumeButton, settingsBtn, controlsBtn, restartBtn, quitBtn };
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
            r.anchoredPosition = new Vector2(0f, 0f);

            if (topFiligreeSprite != null)
            {
                var topFiligree = CreateAnchoredRect("Settings Top Filigree", settingsPanel.transform, new Vector2(400f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0f, 195f));
                var tImg = topFiligree.gameObject.AddComponent<Image>();
                tImg.sprite = topFiligreeSprite;
                tImg.color = new Color(0.86f, 0.92f, 1.0f, 0.85f);
                tImg.raycastTarget = false;
            }

            var titleLabel = CreateLabel("SettingsTitle", settingsPanel.transform, new Vector2(0f, 155f), new Vector2(600f, 32f),
                "SETTINGS", 22f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            float y = 100f;
            masterSlider = CreateSlider("MasterSlider", r, new Vector2(0f, y), "Master Volume", "Overall audio level", v =>
            {
                MenuPreferences.SetAudio(v, MenuPreferences.Music, MenuPreferences.Effects);
                MarkPauseSettingsChanged();
                UpdateSliderPercentLabel(masterSlider);
            });

            y -= 48f;
            musicSlider = CreateSlider("MusicSlider", r, new Vector2(0f, y), "Ambient Music", "Music and atmospheric score", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, v, MenuPreferences.Effects);
                MarkPauseSettingsChanged();
                UpdateSliderPercentLabel(musicSlider);
            });

            y -= 48f;
            effectsSlider = CreateSlider("EffectsSlider", r, new Vector2(0f, y), "Sound Effects (SFX)", "Footsteps, lantern and interactions", v =>
            {
                MenuPreferences.SetAudio(MenuPreferences.Master, MenuPreferences.Music, v);
                MarkPauseSettingsChanged();
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

            if (bottomCrestSprite != null)
            {
                var botCrest = CreateAnchoredRect("Settings Bottom Filigree", r, new Vector2(360f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f));
                var bImg = botCrest.gameObject.AddComponent<Image>();
                bImg.sprite = bottomCrestSprite;
                bImg.color = new Color(0.86f, 0.92f, 1.0f, 0.65f);
                bImg.raycastTarget = false;
            }

            var resetBtn = CreateEtherealActionButton("ResetDefaultsButton", r, "Reset Defaults", new Vector2(-160f, -205f), RequestResetDefaults, isPrimary: false, width: 230f);
            settingsBackButton = CreateEtherealActionButton("SettingsBackButton", r, "<  Return to Pause", new Vector2(160f, -205f), ShowMainView, isPrimary: true, width: 260f);

            BuildPauseSettingsWorkspace();
        }

        private readonly TMP_Text[] pauseControlsRowKeyLabels = new TMP_Text[5];
        private readonly Image[] pauseControlsRowIcons = new Image[5];
        private readonly Button[] pauseControlsDeviceTabs = new Button[3];
        private readonly TMP_Text[] pauseControlsDeviceTabLabels = new TMP_Text[3];
        private int selectedPauseControlsDeviceTab = 0;
        private Button controlsSettingsButton;

        private void BuildControlsPanel(Transform parent)
        {
            controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform), typeof(CanvasGroup));
            controlsPanel.transform.SetParent(parent, false);
            controlsPanelGroup = controlsPanel.GetComponent<CanvasGroup>();
            var r = controlsPanel.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(780f, 530f);
            r.anchoredPosition = new Vector2(0f, 0f);

            if (topFiligreeSprite != null)
            {
                var topFiligree = CreateAnchoredRect("Controls Top Filigree", controlsPanel.transform, new Vector2(400f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0f, 245f));
                var tImg = topFiligree.gameObject.AddComponent<Image>();
                tImg.sprite = topFiligreeSprite;
                tImg.color = new Color(0.86f, 0.92f, 1.0f, 0.85f);
                tImg.raycastTarget = false;
            }

            var titleLabel = CreateLabel("ControlsTitle", controlsPanel.transform, new Vector2(0f, 205f), new Vector2(600f, 30f),
                "BUTTON GUIDE", 21f, TextAlignmentOptions.Center, radiantIvory);
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.characterSpacing = 5f;

            // 3-way Device Switcher at top
            var tabRow = CreateAnchoredRect("Pause Controls Tabs", controlsPanel.transform, new Vector2(740f, 36f), Vector2.one * 0.5f, new Vector2(0f, 172f));

            // Left Bumper Badge [LB / Q]
            var lBumperRect = CreateAnchoredRect("Left Bumper Badge", tabRow, new Vector2(36f, 26f), Vector2.one * 0.5f, new Vector2(-330f, 0f));
            pauseLeftBumperImg = lBumperRect.gameObject.AddComponent<Image>();
            pauseLeftBumperImg.preserveAspect = true;
            pauseLeftBumperImg.raycastTarget = false;
            pauseLeftBumperText = CreateLabel("Text", lBumperRect, Vector2.zero, new Vector2(36f, 24f), "[ Q ]", 11f, TextAlignmentOptions.Center, moonlightSilver);
            var lBumperBtn = lBumperRect.gameObject.AddComponent<Button>();
            lBumperBtn.onClick.AddListener(() =>
            {
                SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 2) % 3);
                if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
            });

            // Right Bumper Badge [RB / E]
            var rBumperRect = CreateAnchoredRect("Right Bumper Badge", tabRow, new Vector2(36f, 26f), Vector2.one * 0.5f, new Vector2(330f, 0f));
            pauseRightBumperImg = rBumperRect.gameObject.AddComponent<Image>();
            pauseRightBumperImg.preserveAspect = true;
            pauseRightBumperImg.raycastTarget = false;
            pauseRightBumperText = CreateLabel("Text", rBumperRect, Vector2.zero, new Vector2(36f, 24f), "[ E ]", 11f, TextAlignmentOptions.Center, moonlightSilver);
            var rBumperBtn = rBumperRect.gameObject.AddComponent<Button>();
            rBumperBtn.onClick.AddListener(() =>
            {
                SetPauseControlsDeviceTab((selectedPauseControlsDeviceTab + 1) % 3);
                if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(0.7f);
            });

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
            pauseControlsRowKeyLabels[0] = CreateControlRow(r, "[ A / D  or  ARROWS ]", "MOVE / CLIMB", "Walk left / right; W or Up at a ledge climbs", y, 0);
            pauseControlsRowKeyLabels[1] = CreateControlRow(r, "[ SPACE ]", "JUMP", "Jump and variable jump height", y - step, 1);
            pauseControlsRowKeyLabels[2] = CreateControlRow(r, "[ F ]", "LANTERN FLARE", "Spend 1 ember to briefly reveal the surroundings", y - step * 2, 2);
            pauseControlsRowKeyLabels[3] = CreateControlRow(r, "[ E ]", "INTERACT", "Transfer an ember into a vein or take it back", y - step * 3, 3);
            pauseControlsRowKeyLabels[4] = CreateControlRow(r, "[ ESC / P ]", "PAUSE MENU", "Open pause menu and controls guide", y - step * 4, 4);

            controlsSettingsButton = CreateEtherealActionButton("ControllerSettingsButton", r, "Controller settings", new Vector2(0, -232), ShowControllerSettings, false, 400);
            controlsBackButton = CreateEtherealActionButton("ControlsBackButton", r, "Return to pause", new Vector2(0, -300), ShowMainView, true, 360);
            for (int i = 0; i < 3; i++)
                pauseControlsDeviceTabs[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = pauseControlsDeviceTabs[(i + 2) % 3], selectOnRight = pauseControlsDeviceTabs[(i + 1) % 3],
                    selectOnDown = controlsSettingsButton, selectOnUp = controlsBackButton };
            controlsSettingsButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = pauseControlsDeviceTabs[1], selectOnDown = controlsBackButton };
            controlsBackButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = controlsSettingsButton, selectOnDown = pauseControlsDeviceTabs[1] };

            var initialStyle = ControllerGlyphService.ActiveStyle;
            int defaultTab = initialStyle == ControllerPromptStyle.PlayStation ? 2 : (initialStyle == ControllerPromptStyle.Xbox ? 1 : 0);
            SetPauseControlsDeviceTab(defaultTab);
        }

        public void SetPauseControlsDeviceTab(int tab)
        {
            selectedPauseControlsDeviceTab = Mathf.Clamp(tab, 0, 2);
            string[] rawNames = { "KEYBOARD", "XBOX CONTROLLER", "PLAYSTATION" };
            for (int i = 0; i < 3; i++)
            {
                bool active = (i == selectedPauseControlsDeviceTab);
                var marker = pauseControlsDeviceTabs[i].transform.Find("Selection rule");
                if (marker != null) marker.gameObject.SetActive(active);
                if (pauseControlsDeviceTabLabels[i] != null)
                {
                    pauseControlsDeviceTabLabels[i].text = rawNames[i];
                    pauseControlsDeviceTabLabels[i].color = active ? amberGold : radiantIvory;
                    pauseControlsDeviceTabLabels[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }

            if (pauseControlsRowKeyLabels[0] == null) return;
            var movementDescription = pauseControlsRowKeyLabels[0].transform.parent.parent.Find("Description")?.GetComponent<TMP_Text>();
            if (movementDescription != null) movementDescription.text = selectedPauseControlsDeviceTab == 0
                ? "Walk left / right; W or Up at a ledge climbs"
                : "Move left / right; press up at a ledge to climb.";

            if (selectedPauseControlsDeviceTab == 0) // Keyboard
            {
                for (int i = 0; i < pauseControlsRowIcons.Length; i++)
                    if (pauseControlsRowIcons[i] != null) pauseControlsRowIcons[i].color = Color.clear;

                pauseControlsRowKeyLabels[0].text = "[ A / D  or  ARROWS ]";
                pauseControlsRowKeyLabels[1].text = "[ SPACE ]";
                pauseControlsRowKeyLabels[2].text = "[ F ]";
                pauseControlsRowKeyLabels[3].text = "[ E ]";
                pauseControlsRowKeyLabels[4].text = "[ ESC / P ]";
            }
            else if (selectedPauseControlsDeviceTab == 1) // Xbox
            {
                string[] xbIcons = { "LeftStick", "A", "RT", "Y", "Menu" };
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
                pauseControlsRowKeyLabels[2].text = "[ RT ]";
                pauseControlsRowKeyLabels[3].text = "[ Y ]";
                pauseControlsRowKeyLabels[4].text = "[ MENU ]";
            }
            else // PlayStation
            {
                string[] psIcons = { "LeftStick", "Cross", "R2", "Triangle", "Options" };
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
                pauseControlsRowKeyLabels[2].text = "[ R2 ]";
                pauseControlsRowKeyLabels[3].text = "[ TRIANGLE ]";
                pauseControlsRowKeyLabels[4].text = "[ OPTIONS ]";
            }
        }

        private void BuildPauseFooter(Transform parent)
        {
            pauseFooterBar = CreateAnchoredRect("Pause HollowKnight Footer", parent, new Vector2(860f, 36f), Vector2.one * 0.5f, new Vector2(0f, -365f));
            var fBg = pauseFooterBar.gameObject.AddComponent<Image>();
            fBg.sprite = softRadialSprite;
            fBg.color = new Color(0.008f, 0.014f, 0.028f, 0.70f);
            fBg.raycastTarget = false;

            float[] xPos = { -300f, -100f, 100f, 300f };

            // Slot 0: SELECT
            var s0 = CreateAnchoredRect("Slot_Select", pauseFooterBar, new Vector2(170f, 32f), Vector2.one * 0.5f, new Vector2(xPos[0], 0f));
            var icon0 = CreateAnchoredRect("Icon", s0, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            pauseFooterSelectIcon = icon0.gameObject.AddComponent<Image>();
            pauseFooterSelectIcon.preserveAspect = true;
            pauseFooterSelectIcon.raycastTarget = false;
            pauseFooterSelectText = CreateLabel("Label", s0, new Vector2(24f, 0f), new Vector2(130f, 24f), "SELECT", 12f, TextAlignmentOptions.Left, radiantIvory);
            pauseFooterSelectText.fontStyle = FontStyles.Bold;
            pauseFooterSelectText.characterSpacing = 1.2f;

            // Slot 1: RESUME / BACK
            var s1 = CreateAnchoredRect("Slot_Back", pauseFooterBar, new Vector2(170f, 32f), Vector2.one * 0.5f, new Vector2(xPos[1], 0f));
            var icon1 = CreateAnchoredRect("Icon", s1, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            pauseFooterBackIcon = icon1.gameObject.AddComponent<Image>();
            pauseFooterBackIcon.preserveAspect = true;
            pauseFooterBackIcon.raycastTarget = false;
            pauseFooterBackText = CreateLabel("Label", s1, new Vector2(24f, 0f), new Vector2(130f, 24f), "RESUME", 12f, TextAlignmentOptions.Left, radiantIvory);
            pauseFooterBackText.fontStyle = FontStyles.Bold;
            pauseFooterBackText.characterSpacing = 1.2f;
            var bBtn = s1.gameObject.AddComponent<Button>();
            bBtn.onClick.AddListener(() =>
            {
                if (IsConfirmationOpen) CancelConfirmation();
                else if (currentView == "settings" || currentView == "controls") ShowMainView();
                else Resume();
            });

            // Slot 2: RESTART LEVEL / RESTORE DEFAULTS
            var s2 = CreateAnchoredRect("Slot_Reset", pauseFooterBar, new Vector2(200f, 32f), Vector2.one * 0.5f, new Vector2(xPos[2], 0f));
            var icon2 = CreateAnchoredRect("Icon", s2, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            pauseFooterResetIcon = icon2.gameObject.AddComponent<Image>();
            pauseFooterResetIcon.preserveAspect = true;
            pauseFooterResetIcon.raycastTarget = false;
            pauseFooterResetText = CreateLabel("Label", s2, new Vector2(24f, 0f), new Vector2(160f, 24f), "RESTART LEVEL", 12f, TextAlignmentOptions.Left, radiantIvory);
            pauseFooterResetText.fontStyle = FontStyles.Bold;
            pauseFooterResetText.characterSpacing = 1.2f;
            var rBtn = s2.gameObject.AddComponent<Button>();
            rBtn.onClick.AddListener(() =>
            {
                if (currentView == "settings") RequestResetDefaults();
                else if (currentView == "main") RequestRestartChamber();

            });

            // Slot 3: SWITCH TAB
            var s3 = CreateAnchoredRect("Slot_Tab", pauseFooterBar, new Vector2(180f, 32f), Vector2.one * 0.5f, new Vector2(xPos[3], 0f));
            var icon3 = CreateAnchoredRect("Icon", s3, new Vector2(24f, 24f), new Vector2(0f, 0.5f), new Vector2(16f, 0f));
            pauseFooterTabIcon = icon3.gameObject.AddComponent<Image>();
            pauseFooterTabIcon.preserveAspect = true;
            pauseFooterTabIcon.raycastTarget = false;
            pauseFooterTabText = CreateLabel("Label", s3, new Vector2(24f, 0f), new Vector2(140f, 24f), "SWITCH TAB", 12f, TextAlignmentOptions.Left, radiantIvory);
            pauseFooterTabText.fontStyle = FontStyles.Bold;
            pauseFooterTabText.characterSpacing = 1.2f;

            RefreshPauseFooterPrompts();
        }

        public void RefreshPauseFooterPrompts()
        {
            var style = ControllerGlyphService.ActiveStyle;
            bool isPad = ControllerGlyphService.IsGamepadActive;

            // Bumper badges in Controls Tab
            if (pauseLeftBumperImg != null)
            {
                if (isPad)
                {
                    pauseLeftBumperImg.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.PrevTab, style);
                    pauseLeftBumperImg.color = Color.white;
                    if (pauseLeftBumperText != null) pauseLeftBumperText.text = "";
                }
                else
                {
                    pauseLeftBumperImg.sprite = null;
                    pauseLeftBumperImg.color = Color.clear;
                    if (pauseLeftBumperText != null) pauseLeftBumperText.text = "[ Q ]";
                }
            }

            if (pauseRightBumperImg != null)
            {
                if (isPad)
                {
                    pauseRightBumperImg.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.NextTab, style);
                    pauseRightBumperImg.color = Color.white;
                    if (pauseRightBumperText != null) pauseRightBumperText.text = "";
                }
                else
                {
                    pauseRightBumperImg.sprite = null;
                    pauseRightBumperImg.color = Color.clear;
                    if (pauseRightBumperText != null) pauseRightBumperText.text = "[ E ]";
                }
            }

            // Footer bar slots
            if (pauseFooterBar == null) return;
            bool compactFooter = currentView != "controls" && currentView != "settings";
            var selectSlot = (RectTransform)pauseFooterSelectIcon.transform.parent;
            var backSlot = (RectTransform)pauseFooterBackIcon.transform.parent;
            selectSlot.anchoredPosition = new Vector2(compactFooter ? -180 : (currentView == "controls" ? -360 : -450), 0);
            backSlot.anchoredPosition = new Vector2(compactFooter ? 180 : (currentView == "controls" ? 0 : -150), 0);

            // Slot 0: Select
            if (pauseFooterSelectIcon != null)
            {
                if (isPad)
                {
                    pauseFooterSelectIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Confirm, style);
                    pauseFooterSelectIcon.color = Color.white;
                    if (pauseFooterSelectText != null) pauseFooterSelectText.text = "SELECT";
                }
                else
                {
                    pauseFooterSelectIcon.sprite = null;
                    pauseFooterSelectIcon.color = Color.clear;
                    if (pauseFooterSelectText != null) pauseFooterSelectText.text = "[ ENTER ] SELECT";
                }
            }

            // Slot 1: Back / Resume
            string backLabel = currentView == "main" ? "CONTINUE" : "BACK";
            if (pauseFooterBackIcon != null)
            {
                if (isPad)
                {
                    pauseFooterBackIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Cancel, style);
                    pauseFooterBackIcon.color = Color.white;
                    if (pauseFooterBackText != null) pauseFooterBackText.text = backLabel;
                }
                else
                {
                    pauseFooterBackIcon.sprite = null;
                    pauseFooterBackIcon.color = Color.clear;
                    if (pauseFooterBackText != null) pauseFooterBackText.text = $"[ ESC ] {backLabel}";
                }
            }

            // Restoring preferences always opens the confirmation before changing anything.
            if (pauseFooterResetIcon != null) pauseFooterResetIcon.transform.parent.gameObject.SetActive(currentView == "settings");
            // Slot 2: Rumble preference shortcut
            string resetLabel = "RESTORE DEFAULTS";
            if (pauseFooterResetIcon != null)
            {
                if (isPad)
                {
                    pauseFooterResetIcon.sprite = ControllerGlyphService.GetGlyphSprite(style == ControllerPromptStyle.PlayStation ? "Triangle" : "Y", style);
                    pauseFooterResetIcon.color = Color.white;
                    if (pauseFooterResetText != null) pauseFooterResetText.text = resetLabel;
                }
                else
                {
                    pauseFooterResetIcon.sprite = null;
                    pauseFooterResetIcon.color = Color.clear;
                    if (pauseFooterResetText != null) pauseFooterResetText.text = $"[ R ] {resetLabel}";
                }
            }

            // Slot 3: Switch Tab
            if (pauseFooterTabIcon != null)
            {
                bool showTabSlot = currentView == "controls" || currentView == "settings";
                ((RectTransform)pauseFooterTabIcon.transform.parent).anchoredPosition = new Vector2(currentView == "controls" ? 360 : 450, 0);
                pauseFooterTabIcon.transform.parent.gameObject.SetActive(showTabSlot);
                if (showTabSlot)
                {
                    if (isPad)
                    {
                        pauseFooterTabIcon.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.NextTab, style);
                        pauseFooterTabIcon.color = Color.white;
                        if (pauseFooterTabText != null) pauseFooterTabText.text = "SWITCH TAB";
                    }
                    else
                    {
                        pauseFooterTabIcon.sprite = null;
                        pauseFooterTabIcon.color = Color.clear;
                        if (pauseFooterTabText != null) pauseFooterTabText.text = "[ Q / E ] SWITCH TAB";
                    }
                }
            }
        }

        private void BuildPauseHeader(Transform parent)
        {
            var caption = CreateLabel("Eyebrow", parent, new Vector2(-530, 300), new Vector2(500, 28),
                "THE LIGHTEN ROAD", 18, TextAlignmentOptions.Left, moonlightSilver);
            caption.rectTransform.pivot = new Vector2(0, .5f);
            var title = CreateLabel("Title", parent, new Vector2(-530, 244), new Vector2(500, 68),
                "Paused", 54, TextAlignmentOptions.Left, radiantIvory);
            title.rectTransform.pivot = new Vector2(0, .5f);
            title.characterSpacing = 1;

            var divider = CreateAnchoredRect("Pause column divider", parent, new Vector2(1, 428), Vector2.one * .5f, new Vector2(55, 15)).gameObject.AddComponent<Image>();
            divider.color = new Color(.7f, .62f, .44f, .22f);
            divider.raycastTarget = false;
            var location = CreateLabel("Location caption", parent, new Vector2(140, 184), new Vector2(420, 30),
                "CURRENT CHAMBER", 18, TextAlignmentOptions.Left, moonlightSilver);
            location.rectTransform.pivot = new Vector2(0, .5f);
            chamberTitleLabel = CreateLabel("ChamberTitle", parent, new Vector2(140, 118), new Vector2(420, 82),
                "Ruined Archive", 30, TextAlignmentOptions.TopLeft, radiantIvory);
            chamberTitleLabel.rectTransform.pivot = new Vector2(0, .5f);
            chamberTitleLabel.textWrappingMode = TextWrappingModes.Normal;

            var lantern = CreateAnchoredRect("Pause lantern", parent, new Vector2(54, 76), Vector2.one * .5f, new Vector2(168, -12));
            var frame = lantern.gameObject.AddComponent<Image>();
            frame.sprite = PixelSprites.LanternFrame;
            frame.color = new Color(0.78f, .68f, .46f);
            frame.preserveAspect = true;
            frame.raycastTarget = false;
            pauseLanternFlame = CreateAnchoredRect("Pause lantern flame", lantern, new Vector2(16, 24), Vector2.one * .5f, new Vector2(0, -3)).gameObject.AddComponent<Image>();
            pauseLanternFlame.sprite = PixelSprites.Flame;
            pauseLanternFlame.color = new Color(1f, .72f, .32f);
            pauseLanternFlame.preserveAspect = true;
            pauseLanternFlame.raycastTarget = false;
            currentFuelLabel = CreateLabel("Current embers", parent, new Vector2(224, -12), new Vector2(340, 50),
                "", 26, TextAlignmentOptions.Left, radiantIvory);
            currentFuelLabel.rectTransform.pivot = new Vector2(0, .5f);
            mementoBadgeLabel = CreateLabel("MementoBadge", parent, new Vector2(140, -106), new Vector2(420, 70),
                "", 22, TextAlignmentOptions.TopLeft, moonlightSilver);
            mementoBadgeLabel.rectTransform.pivot = new Vector2(0, .5f);
            mementoBadgeLabel.textWrappingMode = TextWrappingModes.Normal;
            pauseActionHint = CreateLabel("Pause action hint", parent, new Vector2(-530, -294), new Vector2(520, 62),
                "Return to your current attempt.", 20, TextAlignmentOptions.TopLeft, moonlightSilver);
            pauseActionHint.rectTransform.pivot = new Vector2(0, .5f);
            pauseActionHint.textWrappingMode = TextWrappingModes.Normal;
            foreach (var text in new[] { caption, location, currentFuelLabel, mementoBadgeLabel, pauseActionHint })
                UsePauseBodyFont(text);
        }

        private static void UsePauseBodyFont(TMP_Text text)
        {
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.characterSpacing = 0;
        }

        private Button CreatePauseAction(string name, RectTransform parent, string title, string hint, float y, UnityAction onClick)
        {
            var rect = CreateAnchoredRect(name, parent, new Vector2(540, 68), Vector2.one * .5f, new Vector2(0, y));
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            var label = CreateLabel("Title", rect, new Vector2(-250, 0), new Vector2(480, 48), title, 30, TextAlignmentOptions.Left, radiantIvory);
            label.rectTransform.pivot = new Vector2(0, .5f);
            var marker = CreateAnchoredRect("Selection marker", rect, new Vector2(3, 26), Vector2.one * .5f, new Vector2(-270, 0));
            var markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = amberGold;
            markerImage.raycastTarget = false;
            var group = marker.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = label;
            motion.WingGroup = group;
            motion.LeftAlignedItem = true;
            motion.InitializeBasePositions(label.rectTransform.anchoredPosition, Vector2.zero, Vector2.zero);
            motion.OnFocusedCallback = _ => { if (pauseActionHint != null) pauseActionHint.text = hint; };
            return button;
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
            plateImg.color = Color.clear;
            plateImg.raycastTarget = true;

            var borderRect = CreateStretchRect("Card Border", rect);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.clear;
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

            float wingOffset = width * 0.5f + 25f;
            var wingContainer = CreateAnchoredRect("Wings Container", rect, new Vector2(width + 60f, 28f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = isPrimary ? 0.25f : 0f;

            RectTransform leftWing = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(34f, 16f), new Vector2(0.5f, 0.5f), new Vector2(-wingOffset, 0f));
            RectTransform rightWing = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(34f, 16f), new Vector2(0.5f, 0.5f), new Vector2(wingOffset, 0f));
            rightWing.localScale = new Vector3(-1f, 1f, 1f);

            if (buttonPointerSprite != null)
            {
                var lImg = leftWing.gameObject.AddComponent<Image>();
                lImg.sprite = buttonPointerSprite;
                lImg.color = radiantIvory;
                lImg.raycastTarget = false;

                var rImg = rightWing.gameObject.AddComponent<Image>();
                rImg.sprite = buttonPointerSprite;
                rImg.color = radiantIvory;
                rImg.raycastTarget = false;
            }

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
            motion.LeftWing = leftWing;
            motion.RightWing = rightWing;
            motion.WingGroup = wingGroup;
            motion.BaseWingOffset = wingOffset;
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
            plateImg.color = Color.clear;
            plateImg.raycastTarget = false;

            var borderRect = CreateStretchRect("Row Border", row);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.clear;
            borderImg.raycastTarget = false;

            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(740f, 1.5f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.12f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, new Vector2(16f, 8f), new Vector2(350f, 20f),
                title.ToUpperInvariant(), 13.5f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Normal;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(16f, -9f), new Vector2(350f, 16f),
                subtitle, 10.5f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(MenuSlider));
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(350f, 28f);
            sRect.anchoredPosition = new Vector2(-16f, 0f);

            // Thin Hollow Knight Luminous Track Line (2.5px tall)
            var track = CreateAnchoredRect("Track", sRect, new Vector2(270f, 2.5f), new Vector2(0f, 0.5f), Vector2.zero);
            track.pivot = new Vector2(0f, 0.5f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = roundTrackSprite;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(0.75f, 0.85f, 0.98f, 0.40f);

            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = roundFillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(1.0f, 0.80f, 0.35f, 0.85f);

            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(44f, 44f), new Vector2(0f, 0.5f), Vector2.zero);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.clear;
            handleImg.raycastTarget = true;

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
            LanternSliderHandle.Attach(slider,handle);

            var focus = row.gameObject.AddComponent<SettingsLightFocus>();
            focus.Label = titleTxt;
            focus.CardPlate = plateImg;
            focus.BorderFrame = borderImg;

            if (buttonPointerSprite != null)
            {
                float halfWidth = 370f;
                float offset = halfWidth + 24f;
                var leftPointer = CreateAnchoredRect("Left Pointer", row, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-offset, 0f));
                var lImg = leftPointer.gameObject.AddComponent<Image>();
                lImg.sprite = buttonPointerSprite;
                lImg.color = Color.clear;
                lImg.raycastTarget = false;

                var rightPointer = CreateAnchoredRect("Right Pointer", row, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(offset, 0f));
                rightPointer.localScale = new Vector3(-1f, 1f, 1f);
                var rImg = rightPointer.gameObject.AddComponent<Image>();
                rImg.sprite = buttonPointerSprite;
                rImg.color = Color.clear;
                rImg.raycastTarget = false;

                focus.LeftPointer = leftPointer;
                focus.RightPointer = rightPointer;
                focus.PointerOffset = offset;
            }

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
            plateImg.color = Color.clear;
            plateImg.raycastTarget = true;

            var borderRect = CreateStretchRect("Row Border", row);
            var borderImg = borderRect.gameObject.AddComponent<Image>();
            borderImg.sprite = thinBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.clear;
            borderImg.raycastTarget = false;

            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(740f, 1.5f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.12f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, new Vector2(16f, 8f), new Vector2(450f, 20f),
                title.ToUpperInvariant(), 13.5f, TextAlignmentOptions.Left, radiantIvory);
            titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleTxt.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleTxt.fontStyle = FontStyles.Normal;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, new Vector2(16f, -9f), new Vector2(450f, 16f),
                subtitle, 10.5f, TextAlignmentOptions.Left, moonlightSilver);
            subTxt.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            subTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            subTxt.rectTransform.pivot = new Vector2(0f, 0.5f);

            var pill = CreateAnchoredRect("Pill", row, new Vector2(130f, 32f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = softRadialSprite;
            pillImg.color = Color.clear;

            var valTxt = CreateLabel("Value", pill, Vector2.zero, new Vector2(130f, 32f),
                "<  OFF  >", 13.5f, TextAlignmentOptions.Center, moonlightSilver);
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.characterSpacing = 2.5f;

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

            if (buttonPointerSprite != null)
            {
                float halfWidth = 370f;
                float offset = halfWidth + 24f;
                var leftPointer = CreateAnchoredRect("Left Pointer", row, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-offset, 0f));
                var lImg = leftPointer.gameObject.AddComponent<Image>();
                lImg.sprite = buttonPointerSprite;
                lImg.color = Color.clear;
                lImg.raycastTarget = false;

                var rightPointer = CreateAnchoredRect("Right Pointer", row, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(offset, 0f));
                rightPointer.localScale = new Vector3(-1f, 1f, 1f);
                var rImg = rightPointer.gameObject.AddComponent<Image>();
                rImg.sprite = buttonPointerSprite;
                rImg.color = Color.clear;
                rImg.raycastTarget = false;

                focus.LeftPointer = leftPointer;
                focus.RightPointer = rightPointer;
                focus.PointerOffset = offset;
            }

            return toggle;
        }

        private TMP_Text CreateControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos, int rowIndex = -1)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(740f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var divRect = CreateAnchoredRect("Row Rule", row, new Vector2(740f, 1f), new Vector2(0.5f, 0f), Vector2.zero);
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.color = new Color(0.40f, 0.52f, 0.72f, 0.12f);
            divImg.raycastTarget = false;

            var badge = CreateAnchoredRect("Keycap Badge", row, new Vector2(240f, 36f), new Vector2(0f, 0.5f), new Vector2(120f, 0f));
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = softRadialSprite;
            badgeImg.color = new Color(.18f, .16f, .12f, .25f);

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
            ArchiveMenuStyle.Apply(tmp,name,fontSize);
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

                    Color center = new Color(0.015f, 0.024f, 0.045f, 0.94f);
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

                    float lineThickness = Mathf.Lerp(0.55f, 0.25f, ax);
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

        private static RectTransform CreateAnchoredRect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 pos, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? (Vector2.one * 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private void PlaySound(AudioClip clip, bool isOpen)
        {
            var sound = clip != null ? clip : Resources.Load<AudioClip>(isOpen ? "Audio/SFX/UI/pause_open" : "Audio/SFX/UI/pause_close");
            if (sound != null && SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayOneShot(sound, soundVolume, bypassGrace: true);
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

        }

        private void SetClosedVisualState()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (modalRect != null)
            {
                modalRect.localScale = Vector3.one;
                modalRect.anchoredPosition = Vector2.zero;
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
