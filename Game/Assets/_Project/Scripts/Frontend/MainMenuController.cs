using System.Collections;
using System.Collections.Generic;
using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Redesigned Main Menu for The Lighten Road.
    /// Pairs a pure-code 2.5D animated midnight-archive stage (swinging pendulum lantern,
    /// volumetric god-rays, carved pillar ember veins, cloaked wanderer silhouette, and
    /// interactive Bezier energy stream) with a frameless, ornate Hollow-Knight / Ori
    /// inspired minimalist typography layout.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity";

        [System.Serializable]
        public struct RealmOption
        {
            public string Number;
            public string Title;
            public string Tag;
            public string Description;
            public string ScenePath;
        }

        public static readonly RealmOption[] AvailableRealms = new[]
        {
            new RealmOption
            {
                Number = "REALM I",
                Title = "THE FIRST LIGHT",
                Tag = "FURKAN / TUTORIAL & BEGINNINGS",
                Description = "Learn movement through the archive, scout the compact crossing, and power a shared circuit to reach an optional memory.",
                ScenePath = DefaultGameplayScene
            },
            new RealmOption
            {
                Number = "REALM II",
                Title = "THE FLOODED MECHANISMS",
                Tag = "KEMAL SANDBOX  •  PUZZLES & VEINS",
                Description = "Channel lantern energy into ancient Ember Veins, push rolling archive shelves, and navigate past electrified waters and lifts.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity"
            },
            new RealmOption
            {
                Number = "REALM III",
                Title = "ARCHIVE TEST CHAMBER",
                Tag = "PROTOTYPE  •  CALIBRATION ROOM",
                Description = "Focused testing ground for lantern light radii, movement physics, coyote time, and environmental hazards.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity"
            }
        };

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;

        // Midnight Indigo & Warm Lantern Amber Palette
        private readonly Color radiantIvory = new Color32(248, 242, 228, 255);
        private readonly Color moonlightSilver = new Color32(176, 190, 214, 235);
        private readonly Color mutedIndigo = new Color32(118, 132, 158, 200);
        private readonly Color amberGold = new Color32(255, 184, 72, 255);
        private readonly Color deepAmber = new Color32(214, 128, 36, 255);

        private Canvas canvas;
        private ProceduralArchiveStage stage;
        private Sprite filigreeSprite;
        private Sprite softRadialSprite;

        private RectTransform safeFrame;
        private Image subpanelVeil;
        private Image curtain;

        // Views & CanvasGroups
        private RectTransform home;
        private RectTransform realms;
        private RectTransform settings;
        private RectTransform controls;
        private RectTransform credits;
        private RectTransform quit;

        private CanvasGroup homeGroup;
        private CanvasGroup realmsGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup controlsGroup;
        private CanvasGroup creditsGroup;
        private CanvasGroup quitGroup;

        // Interactive Elements
        private TMP_Text activeRealmBadgeText;
        private TMP_Text hintText;
        private TMP_Text loadingText;
        private Image loadingBarFill;
        private GameObject loadingContainer;

        private Button playButton;
        private Button realmsFirst;
        private Button controlsFirst;
        private Button creditsFirst;
        private Button quitFirst;

        private readonly List<Image> realmRowAuras = new List<Image>();
        private readonly List<Image> realmRowUnderlines = new List<Image>();
        private readonly List<TMP_Text> realmStatusLabels = new List<TMP_Text>();

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle fullscreenToggle;
        private Toggle vsyncToggle;
        private Toggle motionToggle;
        private TMP_Text fpsButtonValueText;

        // Runtime State
        private float age;
        private float viewAge;
        private string view = "home";
        private bool introDone;
        private bool introIgnitePlayed;
        private bool loading;
        private bool quitting;
        private int selectedRealmIndex;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;

        public Canvas Canvas => canvas;
        public string CurrentView => view;
        public bool IntroComplete => introDone;
        public bool IsLoading => loading;
        public string GameplayScene => gameplayScene;
        public int SelectedRealmIndex => selectedRealmIndex;

        private void Awake()
        {
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("UI/MenuType");
            }

            selectedRealmIndex = Mathf.Clamp(MenuPreferences.SelectedRealmIndex, 0, AvailableRealms.Length - 1);
            gameplayScene = AvailableRealms[selectedRealmIndex].ScenePath;

            BuildUI();
            RefreshRealmSelectionUI();
        }

        private void Start()
        {
            MenuPreferences.EnsureActive();
            MenuPreferences.ApplySavedDisplay();
            MenuPreferences.Apply();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;

            var bgm = BackgroundMusicManager.Instance;
            if (bgm != null && !bgm.IsPlaying)
            {
                bgm.Play();
            }

            if (MenuPreferences.ReducedMotion)
            {
                SkipIntro();
            }
        }

        private void OnDestroy()
        {
            MenuPreferences.Save();
            if (softRadialSprite != null)
            {
                if (softRadialSprite.texture != null) Destroy(softRadialSprite.texture);
                Destroy(softRadialSprite);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            viewAge += dt;

            if (!introIgnitePlayed && age >= 0.20f)
            {
                introIgnitePlayed = true;
                if (Application.isPlaying && !MenuPreferences.ReducedMotion)
                {
                    SoundEffectManager.Instance?.PlayFlareIgnite();
                }
                stage?.TriggerInteractionSurge(new Vector2(0f, -65f), Vector2.zero, 1.25f);
            }

            bool backPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            bool confirmPressed = (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                               || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                               || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (!introDone && (confirmPressed || backPressed))
            {
                SkipIntro();
            }
            else if (backPressed && !loading && !quitting)
            {
                if (view == "home") ShowQuit();
                else ShowHome();
            }

            if (!introDone && age >= 2.9f)
            {
                SkipIntro();
            }

            float introIgnition = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.InverseLerp(0.12f, 1.75f, age));
            float uiReveal = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.InverseLerp(0.65f, 2.35f, age));

            if (!loading && !quitting && curtain != null)
            {
                float curtainAlpha = 1f - SmoothStep(Mathf.InverseLerp(0.10f, 1.85f, age));
                curtain.color = new Color(0.012f, 0.018f, 0.038f, curtainAlpha);
            }

            // Mouse tracking for stage parallax & lantern spotlight
            Vector2 normMouse = Vector2.zero;
            Vector2 mouseCanvasPos = Vector2.zero;
            if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 raw = Mouse.current.position.ReadValue();
                normMouse = Vector2.ClampMagnitude(new Vector2(raw.x / Screen.width - 0.5f, raw.y / Screen.height - 0.5f), 0.75f);
                if (canvas != null)
                {
                    var cRect = (RectTransform)canvas.transform;
                    mouseCanvasPos = new Vector2((raw.x / Screen.width - 0.5f) * cRect.rect.width, (raw.y / Screen.height - 0.5f) * cRect.rect.height);
                }
            }

            stage?.TickStage(dt, introIgnition, normMouse, mouseCanvasPos, MenuPreferences.ReducedMotion);

            // Ethereal Midnight Veil when a sub-view is open
            bool isSubView = view != "home";
            if (subpanelVeil != null)
            {
                float targetVeil = view == "quit" ? 0.84f : (isSubView ? 0.76f : 0f);
                subpanelVeil.color = new Color(0.015f, 0.024f, 0.052f, Mathf.MoveTowards(subpanelVeil.color.a, targetVeil, dt * 4f));
            }

            float transitionProgress = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.Clamp01(viewAge / 0.25f));
            UpdateEtherealView(homeGroup, view == "home", uiReveal, transitionProgress, isHome: true);
            UpdateEtherealView(realmsGroup, view == "realms", 1f, transitionProgress, isHome: false);
            UpdateEtherealView(settingsGroup, view == "settings", 1f, transitionProgress, isHome: false);
            UpdateEtherealView(controlsGroup, view == "controls", 1f, transitionProgress, isHome: false);
            UpdateEtherealView(creditsGroup, view == "credits", 1f, transitionProgress, isHome: false);
            UpdateEtherealView(quitGroup, view == "quit", 1f, transitionProgress, isHome: false);

            if (hintText != null)
            {
                hintText.gameObject.SetActive(!introDone);
            }

            UpdateSafeFrame();
        }

        private void UpdateEtherealView(CanvasGroup group, bool active, float masterReveal, float transition, bool isHome)
        {
            if (group == null) return;
            float targetAlpha = active ? masterReveal : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 6.5f);
            group.interactable = active && introDone && !loading && !quitting;
            group.blocksRaycasts = active && !loading && !quitting;

            var rect = (RectTransform)group.transform;
            if (MenuPreferences.ReducedMotion)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            else if (isHome)
            {
                float yOffset = -18f * (1f - masterReveal);
                rect.anchoredPosition = new Vector2(0f, yOffset);
            }
            else
            {
                float yRise = -22f * (1f - transition);
                float scale = Mathf.Lerp(0.96f, 1f, transition);
                rect.anchoredPosition = new Vector2(0f, yRise);
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void SkipIntro()
        {
            age = Mathf.Max(age, 3.0f);
            introDone = true;
            introIgnitePlayed = true;
            if (curtain != null && !loading && !quitting)
            {
                curtain.color = new Color(0.012f, 0.018f, 0.038f, 0f);
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && playButton != null)
            {
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
            }
        }

        private void SetView(string nextView, Selectable firstSelectable, Vector2 stageCameraFocus)
        {
            if (loading || quitting) return;
            MenuPreferences.Save();
            view = nextView;
            viewAge = 0f;
            stage?.TriggerInteractionSurge(new Vector2(0f, 40f), stageCameraFocus, 0.9f);

            if (EventSystem.current != null && firstSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
            }
        }

        public void ShowHome() => SetView("home", playButton, Vector2.zero);
        public void ShowRealms() { RefreshRealmSelectionUI(); SetView("realms", realmsFirst, new Vector2(-28f, -14f)); }
        public void ShowSettings() { SyncSettingsUI(); SetView("settings", masterSlider, new Vector2(24f, -18f)); }
        public void ShowControls() => SetView("controls", controlsFirst, new Vector2(-18f, 16f));
        public void ShowCredits() => SetView("credits", creditsFirst, new Vector2(18f, 16f));
        public void ShowQuit() => SetView("quit", quitFirst, new Vector2(0f, -26f));

        public void SelectRealm(int index)
        {
            if (index < 0 || index >= AvailableRealms.Length) return;
            selectedRealmIndex = index;
            gameplayScene = AvailableRealms[selectedRealmIndex].ScenePath;
            MenuPreferences.SetSelectedRealm(selectedRealmIndex);
            RefreshRealmSelectionUI();

            stage?.TriggerInteractionSurge(new Vector2(0f, 120f - index * 140f), new Vector2(-20f, 10f - index * 10f), 1.1f);

            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayEmberPickup();
            }
        }

        private void RefreshRealmSelectionUI()
        {
            if (activeRealmBadgeText != null && selectedRealmIndex >= 0 && selectedRealmIndex < AvailableRealms.Length)
            {
                var r = AvailableRealms[selectedRealmIndex];
                activeRealmBadgeText.text = $"—   {r.Number}  :  {r.Title}   —";
            }

            for (int i = 0; i < realmRowAuras.Count; i++)
            {
                bool isSelected = (i == selectedRealmIndex);
                if (realmRowAuras[i] != null)
                {
                    realmRowAuras[i].color = isSelected
                        ? new Color(1.0f, 0.68f, 0.22f, 0.26f)
                        : new Color(0.55f, 0.68f, 0.90f, 0.04f);
                }
                if (realmRowUnderlines[i] != null)
                {
                    realmRowUnderlines[i].color = isSelected
                        ? new Color(1.0f, 0.74f, 0.28f, 0.85f)
                        : new Color(0.55f, 0.68f, 0.90f, 0.16f);
                }
                if (realmStatusLabels[i] != null)
                {
                    realmStatusLabels[i].text = isSelected ? "[ ACTIVE REALM ]" : "SELECT REALM";
                    realmStatusLabels[i].color = isSelected ? amberGold : mutedIndigo;
                }
            }
        }

        public void Play()
        {
            if (loading || quitting) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                if (loadingContainer != null) loadingContainer.SetActive(true);
                if (loadingText != null)
                {
                    loadingText.text = $"SCENE NOT IN BUILD SETTINGS: {gameplayScene}";
                }
                return;
            }
            StartCoroutine(EnterGameRoutine());
        }

        private IEnumerator EnterGameRoutine()
        {
            loading = true;
            MenuPreferences.Save();

            stage?.TriggerInteractionSurge(Vector2.zero, new Vector2(0f, 32f), 1.6f);
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayFlareIgnite();
            }

            if (loadingContainer != null) loadingContainer.SetActive(true);
            if (loadingText != null)
            {
                var r = AvailableRealms[Mathf.Clamp(selectedRealmIndex, 0, AvailableRealms.Length - 1)];
                loadingText.text = $"ENTERING {r.Title}...";
            }

            var operation = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
            if (operation == null)
            {
                loading = false;
                if (loadingText != null) loadingText.text = "UNABLE TO OPEN REALM.";
                yield break;
            }

            operation.allowSceneActivation = false;
            float elapsed = 0f;
            while (elapsed < 0.75f || operation.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(Mathf.Min(elapsed / 0.75f, operation.progress / 0.9f));
                if (loadingBarFill != null)
                {
                    loadingBarFill.rectTransform.anchorMax = new Vector2(progress, 1f);
                }
                if (curtain != null)
                {
                    curtain.color = new Color(0.012f, 0.018f, 0.038f, SmoothStep(progress));
                }
                yield return null;
            }

            if (curtain != null)
            {
                curtain.color = new Color(0.012f, 0.018f, 0.038f, 1f);
            }
            operation.allowSceneActivation = true;
        }

        public void ConfirmQuit()
        {
            if (loading || quitting) return;
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator QuitRoutine()
        {
            quitting = true;
            MenuPreferences.Save();

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                if (curtain != null)
                {
                    curtain.color = new Color(0.01f, 0.015f, 0.03f, SmoothStep(t / 0.35f));
                }
                yield return null;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================
        // SETTINGS CALLBACKS
        // =====================================================================

        private void SyncSettingsUI()
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(MenuPreferences.Master);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(MenuPreferences.Music);
            if (effectsSlider != null) effectsSlider.SetValueWithoutNotify(MenuPreferences.Effects);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(MenuPreferences.Fullscreen);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(MenuPreferences.VSync);
            if (motionToggle != null) motionToggle.SetIsOnWithoutNotify(MenuPreferences.ReducedMotion);

            if (masterSlider != null) UpdateSliderPercentLabel(masterSlider);
            if (musicSlider != null) UpdateSliderPercentLabel(musicSlider);
            if (effectsSlider != null) UpdateSliderPercentLabel(effectsSlider);
            UpdateSwitchVisual(fullscreenToggle);
            UpdateSwitchVisual(vsyncToggle);
            UpdateSwitchVisual(motionToggle);
            UpdateFpsLabel();
        }

        private void OnAudioSliderChanged(float _)
        {
            if (masterSlider == null || musicSlider == null || effectsSlider == null) return;
            UpdateSliderPercentLabel(masterSlider);
            UpdateSliderPercentLabel(musicSlider);
            UpdateSliderPercentLabel(effectsSlider);
            MenuPreferences.SetAudio(masterSlider.value, musicSlider.value, effectsSlider.value);
        }

        private void OnFullscreenToggled(bool value)
        {
            MenuPreferences.SetFullscreen(value);
            UpdateSwitchVisual(fullscreenToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
        }

        private void OnVSyncToggled(bool value)
        {
            MenuPreferences.SetVSync(value);
            UpdateSwitchVisual(vsyncToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
        }

        private void OnReducedMotionToggled(bool value)
        {
            MenuPreferences.SetReducedMotion(value);
            UpdateSwitchVisual(motionToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
        }

        private void CycleTargetFPS()
        {
            int current = MenuPreferences.TargetFPS;
            int next = current == 60 ? 120 : (current == 120 ? -1 : 60);
            MenuPreferences.SetTargetFPS(next);
            UpdateFpsLabel();
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
        }

        private void UpdateFpsLabel()
        {
            if (fpsButtonValueText == null) return;
            int fps = MenuPreferences.TargetFPS;
            fpsButtonValueText.text = fps <= 0 ? "UNLIMITED" : $"{fps} FPS";
        }

        public void ResetDefaults()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsUI();
            stage?.TriggerInteractionSurge(Vector2.zero, Vector2.zero, 1f);
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayEmberPickup();
            }
        }

        private static void UpdateSliderPercentLabel(Slider slider)
        {
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

        private void UpdateSafeFrame()
        {
            if (lastWidth == Screen.width && lastHeight == Screen.height && lastSafeArea == Screen.safeArea) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0 || safeFrame == null) return;

            safeFrame.anchorMin = new Vector2(lastSafeArea.xMin / lastWidth, lastSafeArea.yMin / lastHeight);
            safeFrame.anchorMax = new Vector2(lastSafeArea.xMax / lastWidth, lastSafeArea.yMax / lastHeight);
        }

        // =====================================================================
        // UI & PROCEDURAL STAGE CONSTRUCTION
        // =====================================================================

        private void BuildUI()
        {
            var rootGo = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootGo.transform.SetParent(transform, false);
            canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rootRect = (RectTransform)rootGo.transform;

            // 1. Pure-Code Procedural 2.5D Animated Stage (No static photos!)
            stage = gameObject.AddComponent<ProceduralArchiveStage>();
            stage.BuildStage(rootRect);
            filigreeSprite = stage.CreateOrnateFiligreeSprite(512, 64);
            softRadialSprite = CreateSoftRadialSprite(128);

            // 2. Ethereal Sub-Panel Midnight Veil
            subpanelVeil = CreateFullStretchImage("Ethereal Subpanel Veil", rootRect, Color.clear);

            // 3. Safe Area Container
            safeFrame = CreateStretchRect("Safe area", rootRect);

            // 4. Views (Frameless Center Stage Architecture)
            home = CreateCenterStagePanel("Home", safeFrame, out homeGroup);
            realms = CreateCenterStagePanel("Realms", safeFrame, out realmsGroup);
            settings = CreateCenterStagePanel("Settings", safeFrame, out settingsGroup);
            controls = CreateCenterStagePanel("Controls", safeFrame, out controlsGroup);
            credits = CreateCenterStagePanel("Credits", safeFrame, out creditsGroup);
            quit = CreateCenterStagePanel("Quit confirmation", safeFrame, out quitGroup);

            BuildFramelessHomeView();
            BuildFramelessRealmsView();
            BuildFramelessSettingsView();
            BuildFramelessControlsView();
            BuildFramelessCreditsView();
            BuildFramelessQuitView();

            homeGroup.alpha = 0f;
            realmsGroup.alpha = 0f;
            settingsGroup.alpha = 0f;
            controlsGroup.alpha = 0f;
            creditsGroup.alpha = 0f;
            quitGroup.alpha = 0f;

            // 5. Delicate Whispered Footer & Intro Skip Prompt
            var footer = CreateLabel("Footer Brand", safeFrame, "THE LIGHTEN ROAD   •   AN ATMOSPHERIC LANTERN JOURNEY", 11f, mutedIndigo, new Vector2(820f, 26f), new Vector2(0.5f, 0f), new Vector2(0f, 22f));
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.alignment = TextAlignmentOptions.Center;
            footer.characterSpacing = 5f;

            hintText = CreateLabel("Skip Hint", safeFrame, "PRESS [ENTER], [SPACE] OR CLICK TO AWAKEN THE LANTERN", 11f, amberGold, new Vector2(680f, 26f), new Vector2(0.5f, 0f), new Vector2(0f, 52f));
            hintText.rectTransform.pivot = new Vector2(0.5f, 0f);
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.characterSpacing = 3.5f;

            // 6. Cinematic Curtain & Loading Indicator
            curtain = CreateFullStretchImage("Cinematic Curtain", rootRect, new Color(0.012f, 0.018f, 0.038f, 1f));

            var loadRect = CreateAnchoredRect("Loading Container", rootRect, new Vector2(680f, 84f), new Vector2(0.5f, 0.14f), Vector2.zero);
            loadingContainer = loadRect.gameObject;

            loadingText = CreateLabel("Loading Text", loadRect, "ENTERING THE ARCHIVE...", 14.5f, radiantIvory, new Vector2(680f, 34f), new Vector2(0.5f, 1f), new Vector2(0f, -8f));
            loadingText.rectTransform.pivot = new Vector2(0.5f, 1f);
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.characterSpacing = 6f;

            var barBg = CreateAnchoredRect("Loading Bar BG", loadRect, new Vector2(420f, 3f), new Vector2(0.5f, 0f), new Vector2(0f, 18f));
            barBg.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);

            var barFillRect = CreateStretchRect("Loading Bar Fill", barBg);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            loadingBarFill = barFillRect.gameObject.AddComponent<Image>();
            loadingBarFill.color = amberGold;

            loadingContainer.SetActive(false);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                esGo.transform.SetParent(transform, false);
                esGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        // =====================================================================
        // HOME VIEW: FRAMELESS HOLLOW-KNIGHT / ORI CENTERED LAYOUT
        // =====================================================================

        private void BuildFramelessHomeView()
        {
            // Soft Golden Aura behind the Monumental Title
            var titleAura = CreateAnchoredRect("Title Soft Lantern Glow", home, new Vector2(980f, 320f), new Vector2(0.5f, 0.5f), new Vector2(0f, 275f));
            var auraImg = titleAura.gameObject.AddComponent<Image>();
            auraImg.sprite = softRadialSprite;
            auraImg.color = new Color(1.0f, 0.64f, 0.18f, 0.16f);
            auraImg.raycastTarget = false;

            // Top Ornate Filigree Crest
            var topCrest = CreateAnchoredRect("Top Ornate Crest", home, new Vector2(540f, 44f), new Vector2(0.5f, 0.5f), new Vector2(0f, 388f));
            var topCrestImg = topCrest.gameObject.AddComponent<Image>();
            topCrestImg.sprite = filigreeSprite;
            topCrestImg.color = new Color(1.0f, 0.76f, 0.34f, 0.78f);
            topCrestImg.raycastTarget = false;

            // Whispered Eyebrow
            var eyebrow = CreateLabel("Eyebrow", home, "SOME LIGHTS REFUSE TO DIE", 12.5f, moonlightSilver, new Vector2(720f, 28f), new Vector2(0.5f, 0.5f), new Vector2(0f, 348f));
            eyebrow.rectTransform.pivot = Vector2.one * 0.5f;
            eyebrow.alignment = TextAlignmentOptions.Center;
            eyebrow.characterSpacing = 9f;

            // Monumental Center Title: THE LIGHTEN ROAD
            var mainTitle = CreateLabel("Title Lighten Road", home, "THE LIGHTEN ROAD", 72f, radiantIvory, new Vector2(1320f, 96f), new Vector2(0.5f, 0.5f), new Vector2(0f, 272f));
            mainTitle.rectTransform.pivot = Vector2.one * 0.5f;
            mainTitle.alignment = TextAlignmentOptions.Center;
            mainTitle.fontStyle = FontStyles.Bold;
            mainTitle.characterSpacing = 11f;

            // Lower Ornate Filigree Divider under Title
            var midCrest = CreateAnchoredRect("Title Bottom Filigree", home, new Vector2(680f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, 202f));
            var midCrestImg = midCrest.gameObject.AddComponent<Image>();
            midCrestImg.sprite = filigreeSprite;
            midCrestImg.color = amberGold;
            midCrestImg.raycastTarget = false;

            // Frameless Active Realm Whisper
            activeRealmBadgeText = CreateLabel("Active Realm Text", home, "—   REALM I  :  THE RUINED ARCHIVE   —", 13f, amberGold, new Vector2(820f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f));
            activeRealmBadgeText.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmBadgeText.alignment = TextAlignmentOptions.Center;
            activeRealmBadgeText.characterSpacing = 4.5f;

            // Frameless Floating Menu Options (Centered over the animated midnight archive)
            playButton = CreateFramelessMenuButton(
                home,
                "Enter the Archive",
                "Awaken the lantern and step into the realm",
                yPos: 54f,
                cameraPan: new Vector2(0f, 22f),
                onClick: Play,
                isPrimary: true);

            CreateFramelessMenuButton(
                home,
                "Realms & Chapters",
                "Choose between Sirac Sandbox, Kemal Sandbox & Test Chamber",
                yPos: -26f,
                cameraPan: new Vector2(-28f, 10f),
                onClick: ShowRealms);

            CreateFramelessMenuButton(
                home,
                "Settings",
                "Audio mixer, display & motion preferences",
                yPos: -98f,
                cameraPan: new Vector2(28f, 4f),
                onClick: ShowSettings);

            CreateFramelessMenuButton(
                home,
                "Controls & Guide",
                "Movement, coyote jump, lantern flare & ember veins",
                yPos: -170f,
                cameraPan: new Vector2(-20f, -12f),
                onClick: ShowControls);

            CreateFramelessMenuButton(
                home,
                "Credits",
                "The creators & soundscape of the archive",
                yPos: -242f,
                cameraPan: new Vector2(20f, -16f),
                onClick: ShowCredits);

            CreateFramelessMenuButton(
                home,
                "Leave the Light",
                "Extinguish the lantern and exit to desktop",
                yPos: -314f,
                cameraPan: new Vector2(0f, -28f),
                onClick: ShowQuit);
        }

        private Button CreateFramelessMenuButton(
            RectTransform parent,
            string title,
            string subtitle,
            float yPos,
            Vector2 cameraPan,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary = false)
        {
            var itemRect = CreateAnchoredRect(title, parent, new Vector2(640f, isPrimary ? 68f : 58f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            // Invisible raycast hit graphic (no ugly box!)
            var hitGraphic = itemRect.gameObject.AddComponent<Image>();
            hitGraphic.color = Color.clear;

            // Soft radial lantern glow behind the text when focused
            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(520f, 92f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            // Symmetrical Procedural Graphic Wings (Filigree + Rotated Golden Diamond)
            var wingContainer = CreateStretchRect("Ornate Wings", itemRect);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = isPrimary ? 0.35f : 0f;
            wingGroup.blocksRaycasts = false;

            float wingOffset = Mathf.Max(155f, title.Length * 9.5f + 56f);

            var leftWing = CreateOrnateWingGraphic("Left Wing", wingContainer, new Vector2(-wingOffset, 6f), isLeft: true);
            var rightWing = CreateOrnateWingGraphic("Right Wing", wingContainer, new Vector2(wingOffset, 6f), isLeft: false);

            // Center Title Typography
            var titleTxt = CreateLabel("Title", itemRect, title.ToUpperInvariant(), isPrimary ? 26f : 21f, isPrimary ? amberGold : radiantIvory, new Vector2(580f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f));
            titleTxt.rectTransform.pivot = Vector2.one * 0.5f;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 6.5f : 5f;

            // Whispered Subtitle underneath
            var subTxt = CreateLabel("Subtitle", itemRect, subtitle, 11.5f, moonlightSilver, new Vector2(580f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f));
            subTxt.rectTransform.pivot = Vector2.one * 0.5f;
            subTxt.alignment = TextAlignmentOptions.Center;
            subTxt.characterSpacing = 1.5f;

            // Center-out glowing filigree underline
            var lineRect = CreateAnchoredRect("Underline Filigree", itemRect, new Vector2(0f, 2f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            var btn = itemRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hitGraphic;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);

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
            motion.StageFocusCanvasPosition = new Vector2(0f, yPos);
            motion.StageCameraPanOffset = cameraPan;
            motion.OnFocusedCallback = OnMenuButtonFocused;

            return btn;
        }

        private RectTransform CreateOrnateWingGraphic(string name, RectTransform parent, Vector2 pos, bool isLeft)
        {
            var wing = CreateAnchoredRect(name, parent, new Vector2(96f, 22f), new Vector2(0.5f, 0.5f), pos);

            var stem = CreateAnchoredRect("Filigree Stem", wing, new Vector2(76f, 12f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? -8f : 8f, 0f));
            var stemImg = stem.gameObject.AddComponent<Image>();
            stemImg.sprite = filigreeSprite;
            stemImg.color = amberGold;
            stemImg.raycastTarget = false;

            var diamond = CreateAnchoredRect("Diamond Tip", wing, new Vector2(8f, 8f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? 34f : -34f, 0f));
            diamond.localEulerAngles = new Vector3(0f, 0f, 45f);
            var diamondImg = diamond.gameObject.AddComponent<Image>();
            diamondImg.color = radiantIvory;
            diamondImg.raycastTarget = false;

            return wing;
        }

        private void OnMenuButtonFocused(MenuButtonMotion motion)
        {
            if (stage != null && motion != null)
            {
                stage.TriggerInteractionSurge(motion.StageFocusCanvasPosition, motion.StageCameraPanOffset, 1.0f);
            }
        }

        // =====================================================================
        // SUB-VIEW 1: REALMS & CHAPTERS (FRAMELESS ORNATE SELECTOR)
        // =====================================================================

        private void BuildFramelessRealmsView()
        {
            var surface = CreateEtherealSubViewFrame(realms, "REALMS & CHAPTERS", "CHOOSE WHICH ARCHIVE REALM TO ILLUMINATE");

            realmRowAuras.Clear();
            realmRowUnderlines.Clear();
            realmStatusLabels.Clear();

            for (int i = 0; i < AvailableRealms.Length; i++)
            {
                int capturedIndex = i;
                var r = AvailableRealms[i];
                float yPos = 125f - i * 142f;

                var row = CreateAnchoredRect($"Realm Option {i}", surface, new Vector2(860f, 122f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
                var hitImg = row.gameObject.AddComponent<Image>();
                hitImg.color = Color.clear;

                var auraRect = CreateAnchoredRect("Row Aura", row, new Vector2(860f, 130f), new Vector2(0.5f, 0.5f), Vector2.zero);
                var auraImg = auraRect.gameObject.AddComponent<Image>();
                auraImg.sprite = softRadialSprite;
                auraImg.raycastTarget = false;
                realmRowAuras.Add(auraImg);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hitImg;
                btn.onClick.AddListener(() => SelectRealm(capturedIndex));
                if (i == 0) realmsFirst = btn;

                var tagTxt = CreateLabel("Realm Number", row, $"{r.Number}   •   {r.Tag}", 11.5f, amberGold, new Vector2(780f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f));
                tagTxt.rectTransform.pivot = Vector2.one * 0.5f;
                tagTxt.alignment = TextAlignmentOptions.Center;
                tagTxt.characterSpacing = 3.5f;

                var titleTxt = CreateLabel("Realm Title", row, r.Title, 26f, radiantIvory, new Vector2(780f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
                titleTxt.rectTransform.pivot = Vector2.one * 0.5f;
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.characterSpacing = 4f;

                var descTxt = CreateLabel("Realm Desc", row, r.Description, 13f, moonlightSilver, new Vector2(760f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f));
                descTxt.rectTransform.pivot = Vector2.one * 0.5f;
                descTxt.alignment = TextAlignmentOptions.Center;

                var statusTxt = CreateLabel("Status", row, "SELECT REALM", 11f, amberGold, new Vector2(260f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -44f));
                statusTxt.rectTransform.pivot = Vector2.one * 0.5f;
                statusTxt.alignment = TextAlignmentOptions.Center;
                statusTxt.characterSpacing = 3f;
                realmStatusLabels.Add(statusTxt);

                var lineRect = CreateAnchoredRect("Filigree Underline", row, new Vector2(560f, 14f), new Vector2(0.5f, 0.5f), new Vector2(0f, -56f));
                var lineImg = lineRect.gameObject.AddComponent<Image>();
                lineImg.sprite = filigreeSprite;
                lineImg.raycastTarget = false;
                realmRowUnderlines.Add(lineImg);
            }

            CreateEtherealActionButton(surface, "Enter Selected Realm  >", new Vector2(175f, -278f), Play, isPrimary: true);
            CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(-175f, -278f), ShowHome, isPrimary: false);
        }

        // =====================================================================
        // SUB-VIEW 2: SETTINGS (AUDIO MIXER, DISPLAY & ACCESSIBILITY)
        // =====================================================================

        private void BuildFramelessSettingsView()
        {
            var surface = CreateEtherealSubViewFrame(settings, "SANCTUARY SETTINGS", "HARMONIZE AUDIO, DISPLAY & MOTION");

            masterSlider = CreateEtherealSlider(surface, "Master Volume", "Global output level for all archive audio", 168f);
            musicSlider = CreateEtherealSlider(surface, "Ambient Music", "Background score level (In-game key: M)", 94f);
            effectsSlider = CreateEtherealSlider(surface, "Sound Effects (SFX)", "Footsteps, lantern flare & embers (In-game key: N)", 20f);

            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);

            fullscreenToggle = CreateEtherealToggle(surface, "Fullscreen Mode", "Expand the archive across the entire display", -62f, OnFullscreenToggled);
            vsyncToggle = CreateEtherealToggle(surface, "Vertical Sync (V-Sync)", "Synchronize frame pacing with display refresh", -118f, OnVSyncToggled);
            motionToggle = CreateEtherealToggle(surface, "Reduced Motion", "Calm lantern sway, parallax and HUD tremors", -174f, OnReducedMotionToggled);

            CreateEtherealFpsSelector(surface, "Target Frame Rate", "Frame rate ceiling (60 / 120 / Unlimited)", -230f);

            CreateEtherealActionButton(surface, "Reset Defaults", new Vector2(-175f, -296f), ResetDefaults, isPrimary: false);
            CreateEtherealActionButton(surface, "<  Save & Return", new Vector2(175f, -296f), ShowHome, isPrimary: true);

            SyncSettingsUI();
        }

        private Slider CreateEtherealSlider(RectTransform parent, string title, string subtitle, float yPos)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 64f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 15f, radiantIvory, new Vector2(380f, 24f), new Vector2(0f, 0.5f), new Vector2(0f, 12f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11.5f, moonlightSilver, new Vector2(380f, 20f), new Vector2(0f, 0.5f), new Vector2(0f, -10f));

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(360f, 28f);
            sRect.anchoredPosition = new Vector2(0f, 0f);

            var track = CreateAnchoredRect("Track", sRect, new Vector2(285f, 4f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
            track.pivot = new Vector2(0f, 0.5f);
            track.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.68f, 0.92f, 0.22f);

            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = amberGold;

            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(14f, 14f), new Vector2(0f, 0.5f), Vector2.zero);
            handle.localEulerAngles = new Vector3(0f, 0f, 45f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = radiantIvory;

            var valTxt = CreateLabel("Value", sRect, "85%", 13.5f, amberGold, new Vector2(64f, 26f), new Vector2(1f, 0.5f), Vector2.zero);
            valTxt.rectTransform.pivot = new Vector2(1f, 0.5f);
            valTxt.alignment = TextAlignmentOptions.Right;
            valTxt.fontStyle = FontStyles.Bold;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            return slider;
        }

        private Toggle CreateEtherealToggle(RectTransform parent, string title, string subtitle, float yPos, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 14.5f, radiantIvory, new Vector2(480f, 22f), new Vector2(0f, 0.5f), new Vector2(0f, 8f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11f, moonlightSilver, new Vector2(520f, 18f), new Vector2(0f, 0.5f), new Vector2(0f, -11f));

            var pill = CreateAnchoredRect("Pill", row, new Vector2(96f, 30f), new Vector2(1f, 0.5f), new Vector2(-48f, 0f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.color = new Color(0.10f, 0.14f, 0.24f, 0.35f);

            var valTxt = CreateLabel("Value", pill, "OFF", 12.5f, amberGold, new Vector2(96f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero);
            valTxt.rectTransform.pivot = Vector2.one * 0.5f;
            valTxt.alignment = TextAlignmentOptions.Center;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.characterSpacing = 2.5f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            toggle.graphic = null;
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private void CreateEtherealFpsSelector(RectTransform parent, string title, string subtitle, float yPos)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 14.5f, radiantIvory, new Vector2(480f, 22f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 8f));
            titleTxt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11f, moonlightSilver, new Vector2(480f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -11f));
            subTxt.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var pill = CreateAnchoredRect("FPS Pill", row, new Vector2(136f, 30f), new Vector2(1f, 0.5f), new Vector2(-68f, 0f));
            pill.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.62f, 0.18f, 0.24f);

            fpsButtonValueText = CreateLabel("FPS Value", pill, "60 FPS", 12.5f, amberGold, new Vector2(136f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero);
            fpsButtonValueText.rectTransform.pivot = Vector2.one * 0.5f;
            fpsButtonValueText.alignment = TextAlignmentOptions.Center;
            fpsButtonValueText.fontStyle = FontStyles.Bold;
            fpsButtonValueText.characterSpacing = 2f;

            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.onClick.AddListener(CycleTargetFPS);
        }

        // =====================================================================
        // SUB-VIEW 3: CONTROLS & GUIDE
        // =====================================================================

        private void BuildFramelessControlsView()
        {
            var surface = CreateEtherealSubViewFrame(controls, "CONTROLS & LANTERN GUIDE", "MASTER THE MECHANISMS OF THE ARCHIVE");

            CreateControlRow(surface, "A / D  or  ARROWS", "TRAVERSE THE ARCHIVE", "Smooth horizontal movement with momentum & air control", 156f);
            CreateControlRow(surface, "SPACE  /  W", "LEAP & COYOTE JUMP", "Hold for higher arc; forgiving ledge grace window", 88f);
            CreateControlRow(surface, "LEFT SHIFT", "ARCHIVE SPRINT", "Burst speed across wide chasms (consumes Stamina)", 20f);
            CreateControlRow(surface, "F  /  L-CLICK", "LANTERN FLARE SURGE", "Ignite a radiant light burst to reveal platforms", -48f);
            CreateControlRow(surface, "E  (HOLD)", "CHANNEL EMBER VEIN", "Pour lantern light into ancient conduits & bridges", -116f);
            CreateControlRow(surface, "M  /  N", "LIVE AUDIO MIXING", "Mute/adjust music (M) or sound effects (N) in-game", -184f);

            controlsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(820f, 56f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var badge = CreateAnchoredRect("Keycap Badge", row, new Vector2(210f, 38f), new Vector2(0f, 0.5f), new Vector2(105f, 0f));
            badge.gameObject.AddComponent<Image>().color = new Color(0.98f, 0.68f, 0.24f, 0.16f);

            var keyTxt = CreateLabel("Key Text", badge, keys, 13f, amberGold, new Vector2(196f, 38f), new Vector2(0.5f, 0.5f), Vector2.zero);
            keyTxt.rectTransform.pivot = Vector2.one * 0.5f;
            keyTxt.alignment = TextAlignmentOptions.Center;
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 2f;

            var actTxt = CreateLabel("Action", row, actionTitle, 15.5f, radiantIvory, new Vector2(560f, 24f), new Vector2(0f, 0.5f), new Vector2(236f, 9f));
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 2.5f;

            var descTxt = CreateLabel("Description", row, desc, 12f, moonlightSilver, new Vector2(560f, 20f), new Vector2(0f, 0.5f), new Vector2(236f, -11f));
        }

        // =====================================================================
        // SUB-VIEW 4: CREDITS
        // =====================================================================

        private void BuildFramelessCreditsView()
        {
            var surface = CreateEtherealSubViewFrame(credits, "KEEPERS OF THE FLAME", "CRAFTED FOR THE 2026 GAME DEVELOPMENT PROJECT");

            CreateCreditBlock(surface, "SIRAÇ ŞİMŞEK", "MOVEMENT, LANTERN SYSTEMS, AUDIO & PROCEDURAL FRONTEND", "Player physics, stamina & lantern HUD, dynamic lighting, foley & animated archive stage", 125f);
            CreateCreditBlock(surface, "KEMAL", "PUZZLE MECHANISMS & EMBER VEIN ARCHITECTURE", "Ember Vein conduits, Flooded Mechanisms sandbox, interactive elevators & hazards", 0f);
            CreateCreditBlock(surface, "ARCHIVE SOUNDSCAPE & ART", "ATMOSPHERIC AUDIO & PROCEDURAL SILHOUETTES", "Custom multi-layered SDF gothic colonnades, swinging lantern light rays & CC0 foley", -125f);

            creditsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateCreditBlock(RectTransform parent, string name, string role, string details, float yPos)
        {
            var block = CreateAnchoredRect(name, parent, new Vector2(820f, 98f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var nameTxt = CreateLabel("Name", block, name, 24f, radiantIvory, new Vector2(780f, 32f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f));
            nameTxt.rectTransform.pivot = Vector2.one * 0.5f;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.characterSpacing = 4f;

            var roleTxt = CreateLabel("Role", block, role, 11.5f, amberGold, new Vector2(780f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f));
            roleTxt.rectTransform.pivot = Vector2.one * 0.5f;
            roleTxt.alignment = TextAlignmentOptions.Center;
            roleTxt.characterSpacing = 3.5f;

            var detTxt = CreateLabel("Details", block, details, 12.5f, moonlightSilver, new Vector2(780f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f));
            detTxt.rectTransform.pivot = Vector2.one * 0.5f;
            detTxt.alignment = TextAlignmentOptions.Center;
        }

        // =====================================================================
        // SUB-VIEW 5: QUIT CONFIRMATION
        // =====================================================================

        private void BuildFramelessQuitView()
        {
            var surface = CreateAnchoredRect("Quit Ethereal Frame", quit, new Vector2(780f, 380f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var topFiligree = CreateAnchoredRect("Top Filigree", surface, new Vector2(580f, 42f), new Vector2(0.5f, 0.5f), new Vector2(0f, 145f));
            var topImg = topFiligree.gameObject.AddComponent<Image>();
            topImg.sprite = filigreeSprite;
            topImg.color = amberGold;
            topImg.raycastTarget = false;

            var tag = CreateLabel("Quit Tag", surface, "UNTIL YOUR NEXT RETURN", 12f, amberGold, new Vector2(620f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, 98f));
            tag.rectTransform.pivot = Vector2.one * 0.5f;
            tag.alignment = TextAlignmentOptions.Center;
            tag.characterSpacing = 6f;

            var title = CreateLabel("Quit Title", surface, "EXTINGUISH THE LANTERN?", 38f, radiantIvory, new Vector2(720f, 52f), new Vector2(0.5f, 0.5f), new Vector2(0f, 44f));
            title.rectTransform.pivot = Vector2.one * 0.5f;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 5f;

            var note = CreateLabel("Quit Note", surface, "The ruined archive will wait in the midnight dark.\nAll audio and realm preferences have been preserved.", 14.5f, moonlightSilver, new Vector2(620f, 56f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f));
            note.rectTransform.pivot = Vector2.one * 0.5f;
            note.alignment = TextAlignmentOptions.Center;

            var botFiligree = CreateAnchoredRect("Bottom Filigree", surface, new Vector2(580f, 42f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f));
            var botImg = botFiligree.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.65f);
            botImg.raycastTarget = false;

            quitFirst = CreateEtherealActionButton(surface, "Stay in the Light", new Vector2(-165f, -142f), ShowHome, isPrimary: true, width: 280f);
            CreateEtherealActionButton(surface, "Extinguish & Exit", new Vector2(165f, -142f), ConfirmQuit, isPrimary: false, width: 280f);
        }

        // =====================================================================
        // SHARED FRAMELESS ETHEREAL HELPERS
        // =====================================================================

        private RectTransform CreateEtherealSubViewFrame(RectTransform viewRoot, string heading, string subheading)
        {
            var container = CreateAnchoredRect("Ethereal Subview Container", viewRoot, new Vector2(960f, 760f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var topCrest = CreateAnchoredRect("Header Top Filigree", container, new Vector2(640f, 44f), new Vector2(0.5f, 0.5f), new Vector2(0f, 342f));
            var topImg = topCrest.gameObject.AddComponent<Image>();
            topImg.sprite = filigreeSprite;
            topImg.color = amberGold;
            topImg.raycastTarget = false;

            var sub = CreateLabel("Subheading", container, subheading, 11.5f, amberGold, new Vector2(820f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, 304f));
            sub.rectTransform.pivot = Vector2.one * 0.5f;
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 5.5f;

            var head = CreateLabel("Heading", container, heading, 38f, radiantIvory, new Vector2(860f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f));
            head.rectTransform.pivot = Vector2.one * 0.5f;
            head.alignment = TextAlignmentOptions.Center;
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 6f;

            var botCrest = CreateAnchoredRect("Header Bottom Filigree", container, new Vector2(760f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0f, 216f));
            var botImg = botCrest.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.55f);
            botImg.raycastTarget = false;

            return container;
        }

        private Button CreateEtherealActionButton(
            RectTransform parent,
            string label,
            Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary,
            float width = 310f)
        {
            var rect = CreateAnchoredRect(label, parent, new Vector2(width, 52f), new Vector2(0.5f, 0.5f), anchoredPos);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width + 60f, 68f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = isPrimary ? new Color(1.0f, 0.66f, 0.20f, 0.22f) : Color.clear;
            glowImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(width * 0.75f, 16f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = isPrimary ? amberGold : new Color(0.65f, 0.75f, 0.92f, 0.35f);
            lineImg.raycastTarget = false;

            var txt = CreateLabel("Caption", rect, label.ToUpperInvariant(), 15f, isPrimary ? amberGold : radiantIvory, new Vector2(width, 40f), new Vector2(0.5f, 0.5f), Vector2.zero);
            txt.rectTransform.pivot = Vector2.one * 0.5f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3.5f;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
                onClick();
            });

            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = txt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;
            motion.StageFocusCanvasPosition = anchoredPos;
            motion.StageCameraPanOffset = Vector2.zero;
            motion.OnFocusedCallback = OnMenuButtonFocused;

            return btn;
        }

        private static RectTransform CreateCenterStagePanel(string panelName, RectTransform parent, out CanvasGroup group)
        {
            var rect = CreateStretchRect(panelName, parent);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
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

        private static RectTransform CreateAnchoredRect(string name, RectTransform parent, Vector2 size, Vector2 anchor, Vector2 pos)
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

        private static Image CreateFullStretchImage(string name, RectTransform parent, Color color)
        {
            var rect = CreateStretchRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text CreateLabel(string name, RectTransform parent, string content, float fontSize, Color color, Vector2 size, Vector2 anchor, Vector2 pos)
        {
            var rect = CreateAnchoredRect(name, parent, size, anchor, pos);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite CreateSoftRadialSprite(int size)
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

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
