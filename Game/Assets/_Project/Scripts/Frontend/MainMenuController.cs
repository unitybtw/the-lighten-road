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
    /// Ultra-minimal, richly animated Main Menu for The Lighten Road.
    /// Pairs the pure-code 2.5D animated archive stage with a clean single-line
    /// Hollow-Knight / Ori inspired home navigation, a 2-column professional Settings UX
    /// with animated sliding switches, and a pixel-aligned 2x3 Controls & Ability grid.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string DefaultGameplayScene = "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity";

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
                Title = "THE RUINED ARCHIVE",
                Tag = "SIRAC SANDBOX  •  CORE EXPERIENCE",
                Description = "Traverse the dark library ruins, manage stamina across wide chasms, and ignite your lantern flare to awaken dormant embers.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity"
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
        private readonly Color radiantIvory = new Color32(248, 243, 232, 255);
        private readonly Color moonlightSilver = new Color32(174, 188, 212, 230);
        private readonly Color mutedIndigo = new Color32(114, 128, 156, 195);
        private readonly Color amberGold = new Color32(255, 186, 74, 255);
        private readonly Color glassSurface = new Color(0.032f, 0.048f, 0.092f, 0.68f);
        private readonly Color glassCard = new Color(0.045f, 0.068f, 0.128f, 0.72f);
        private readonly Color subtleBorder = new Color(0.58f, 0.72f, 0.96f, 0.16f);

        private Canvas canvas;
        private ProceduralArchiveStage stage;
        private Sprite filigreeSprite;
        private Sprite softRadialSprite;

        private RectTransform safeFrame;
        private RectTransform titleFloatGroup;
        private Image titleAuraImage;
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
        private TMP_Text contextWhisperText;
        private TMP_Text hintText;
        private TMP_Text loadingText;
        private Image loadingBarFill;
        private GameObject loadingContainer;

        private Button playButton;
        private Button realmsFirst;
        private Button controlsFirst;
        private Button creditsFirst;
        private Button quitFirst;

        private readonly List<Image> realmCardBgs = new List<Image>();
        private readonly List<Image> realmCardBorders = new List<Image>();
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

            if (!introIgnitePlayed && age >= 0.18f)
            {
                introIgnitePlayed = true;
                if (Application.isPlaying && !MenuPreferences.ReducedMotion && SoundEffectManager.HasInstance)
                {
                    SoundEffectManager.Instance.PlayFlareIgnite();
                }
                stage?.TriggerInteractionSurge(new Vector2(0f, 36f), Vector2.zero, 1.3f);
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

            if (!introDone && age >= 2.75f)
            {
                SkipIntro();
            }

            float introIgnition = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.InverseLerp(0.10f, 1.65f, age));
            float uiReveal = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.InverseLerp(0.55f, 2.20f, age));

            if (!loading && !quitting && curtain != null)
            {
                float curtainAlpha = 1f - SmoothStep(Mathf.InverseLerp(0.08f, 1.75f, age));
                curtain.color = new Color(0.010f, 0.016f, 0.036f, curtainAlpha);
            }

            // Continuous breathing levitation on the Home Title Crest
            if (titleFloatGroup != null)
            {
                float floatY = MenuPreferences.ReducedMotion ? 0f : Mathf.Sin(age * 1.45f) * 5.5f;
                titleFloatGroup.anchoredPosition = new Vector2(0f, floatY);
            }
            if (titleAuraImage != null)
            {
                float auraPulse = MenuPreferences.ReducedMotion ? 0.16f : (0.15f + 0.05f * Mathf.Sin(age * 2.4f));
                titleAuraImage.color = new Color(1.0f, 0.66f, 0.20f, auraPulse * uiReveal);
            }

            // Mouse tracking for 2.5D stage parallax & spotlight
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

            bool isSubView = view != "home";
            if (subpanelVeil != null)
            {
                float targetVeil = view == "quit" ? 0.86f : (isSubView ? 0.80f : 0f);
                subpanelVeil.color = new Color(0.012f, 0.020f, 0.046f, Mathf.MoveTowards(subpanelVeil.color.a, targetVeil, dt * 4.5f));
            }

            float transitionProgress = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.Clamp01(viewAge / 0.24f));
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
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 7f);
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
                float yOffset = -16f * (1f - masterReveal);
                rect.anchoredPosition = new Vector2(0f, yOffset);
            }
            else
            {
                float yRise = -20f * (1f - transition);
                float scale = Mathf.Lerp(0.965f, 1f, transition);
                rect.anchoredPosition = new Vector2(0f, yRise);
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        public void SkipIntro()
        {
            age = Mathf.Max(age, 2.8f);
            introDone = true;
            introIgnitePlayed = true;
            if (curtain != null && !loading && !quitting)
            {
                curtain.color = new Color(0.010f, 0.016f, 0.036f, 0f);
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
            stage?.TriggerInteractionSurge(new Vector2(0f, 30f), stageCameraFocus, 0.95f);

            if (EventSystem.current != null && firstSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
            }
        }

        public void ShowHome() => SetView("home", playButton, Vector2.zero);
        public void ShowRealms() { RefreshRealmSelectionUI(); SetView("realms", realmsFirst, new Vector2(-28f, -14f)); }
        public void ShowSettings() { SyncSettingsUI(); SetView("settings", masterSlider, new Vector2(26f, -16f)); }
        public void ShowControls() => SetView("controls", controlsFirst, new Vector2(-20f, 18f));
        public void ShowCredits() => SetView("credits", creditsFirst, new Vector2(20f, 18f));
        public void ShowQuit() => SetView("quit", quitFirst, new Vector2(0f, -26f));

        public void SelectRealm(int index)
        {
            if (index < 0 || index >= AvailableRealms.Length) return;
            selectedRealmIndex = index;
            gameplayScene = AvailableRealms[selectedRealmIndex].ScenePath;
            MenuPreferences.SetSelectedRealm(selectedRealmIndex);
            RefreshRealmSelectionUI();

            stage?.TriggerInteractionSurge(new Vector2(0f, 110f - index * 130f), new Vector2(-20f, 12f - index * 10f), 1.15f);

            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayEmberPickup();
            }
        }

        private void RefreshRealmSelectionUI()
        {
            if (activeRealmBadgeText != null && selectedRealmIndex >= 0 && selectedRealmIndex < AvailableRealms.Length)
            {
                var r = AvailableRealms[selectedRealmIndex];
                activeRealmBadgeText.text = $"{r.Number}   •   {r.Title}";
            }

            for (int i = 0; i < realmCardBgs.Count; i++)
            {
                bool isSelected = (i == selectedRealmIndex);
                if (realmCardBgs[i] != null)
                {
                    realmCardBgs[i].color = isSelected
                        ? new Color(0.14f, 0.10f, 0.05f, 0.86f)
                        : glassCard;
                }
                if (realmCardBorders[i] != null)
                {
                    realmCardBorders[i].color = isSelected
                        ? new Color(1.0f, 0.74f, 0.28f, 0.90f)
                        : subtleBorder;
                }
                if (realmStatusLabels[i] != null)
                {
                    realmStatusLabels[i].text = isSelected ? "ACTIVE REALM" : "SELECT";
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

            stage?.TriggerInteractionSurge(Vector2.zero, new Vector2(0f, 36f), 1.65f);
            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayFlareIgnite();
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
                    curtain.color = new Color(0.010f, 0.016f, 0.036f, SmoothStep(progress));
                }
                yield return null;
            }

            if (curtain != null)
            {
                curtain.color = new Color(0.010f, 0.016f, 0.036f, 1f);
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
                    curtain.color = new Color(0.008f, 0.012f, 0.028f, SmoothStep(t / 0.35f));
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
        // SETTINGS CALLBACKS & SWITCH ANIMATION
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
            stage?.TriggerInteractionSurge(new Vector2(275f, 110f), new Vector2(22f, 8f), 0.7f);
            if (Application.isPlaying && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayEmberPickup();
        }

        private void OnVSyncToggled(bool value)
        {
            MenuPreferences.SetVSync(value);
            UpdateSwitchVisual(vsyncToggle);
            stage?.TriggerInteractionSurge(new Vector2(275f, 20f), new Vector2(22f, 0f), 0.7f);
            if (Application.isPlaying && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayEmberPickup();
        }

        private void OnReducedMotionToggled(bool value)
        {
            MenuPreferences.SetReducedMotion(value);
            UpdateSwitchVisual(motionToggle);
            if (Application.isPlaying && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayEmberPickup();
        }

        private void CycleTargetFPS()
        {
            int current = MenuPreferences.TargetFPS;
            int next = current == 60 ? 120 : (current == 120 ? -1 : 60);
            MenuPreferences.SetTargetFPS(next);
            UpdateFpsLabel();
            stage?.TriggerInteractionSurge(new Vector2(275f, -160f), new Vector2(22f, -12f), 0.7f);
            if (Application.isPlaying && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayEmberPickup();
        }

        private void TestLanternAudioSample()
        {
            stage?.TriggerInteractionSurge(new Vector2(-275f, -160f), new Vector2(-24f, 12f), 1.35f);
            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayFlareIgnite();
            }
        }

        private void UpdateFpsLabel()
        {
            if (fpsButtonValueText == null) return;
            int fps = MenuPreferences.TargetFPS;
            fpsButtonValueText.text = fps <= 0 ? "<  UNLIMITED  >" : $"<  {fps} FPS  >";
        }

        public void ResetDefaults()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsUI();
            stage?.TriggerInteractionSurge(Vector2.zero, Vector2.zero, 1.1f);
            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayEmberPickup();
            }
        }

        private static void UpdateSliderPercentLabel(Slider slider)
        {
            var card = slider.transform.parent;
            if (card == null) return;
            var valNode = card.Find("PercentValue");
            if (valNode != null && valNode.TryGetComponent<TMP_Text>(out var txt))
            {
                txt.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            }
        }

        private void UpdateSwitchVisual(Toggle toggle)
        {
            if (toggle == null) return;
            var pillNode = toggle.transform.Find("SwitchTrack");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = toggle.isOn
                    ? new Color(0.96f, 0.64f, 0.18f, 0.34f)
                    : new Color(0.08f, 0.12f, 0.22f, 0.65f);
            }

            var knobNode = toggle.transform.Find("SwitchTrack/Knob");
            if (knobNode is RectTransform knobRect)
            {
                knobRect.anchoredPosition = new Vector2(toggle.isOn ? 24f : -24f, 0f);
                if (knobNode.TryGetComponent<Image>(out var knobImg))
                {
                    knobImg.color = toggle.isOn ? amberGold : mutedIndigo;
                }
            }

            var valNode = toggle.transform.Find("SwitchTrack/StateText");
            if (valNode != null && valNode.TryGetComponent<TMP_Text>(out var txt))
            {
                txt.text = toggle.isOn ? "ON" : "OFF";
                txt.color = toggle.isOn ? radiantIvory : mutedIndigo;
                ((RectTransform)valNode).anchoredPosition = new Vector2(toggle.isOn ? -14f : 14f, 0f);
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

            // 1. Pure-Code Procedural 2.5D Animated Stage
            stage = gameObject.AddComponent<ProceduralArchiveStage>();
            stage.BuildStage(rootRect);
            filigreeSprite = stage.CreateOrnateFiligreeSprite(512, 64);
            softRadialSprite = CreateSoftRadialSprite(128);

            // 2. Sub-Panel Midnight Veil
            subpanelVeil = CreateFullStretchImage("Ethereal Subpanel Veil", rootRect, Color.clear);

            // 3. Safe Area Container
            safeFrame = CreateStretchRect("Safe area", rootRect);

            // 4. Views
            home = CreateCenterStagePanel("Home", safeFrame, out homeGroup);
            realms = CreateCenterStagePanel("Realms", safeFrame, out realmsGroup);
            settings = CreateCenterStagePanel("Settings", safeFrame, out settingsGroup);
            controls = CreateCenterStagePanel("Controls", safeFrame, out controlsGroup);
            credits = CreateCenterStagePanel("Credits", safeFrame, out creditsGroup);
            quit = CreateCenterStagePanel("Quit confirmation", safeFrame, out quitGroup);

            BuildMinimalHomeView();
            BuildRealmsView();
            BuildProfessionalSettingsView();
            BuildProfessionalControlsView();
            BuildCreditsView();
            BuildQuitView();

            homeGroup.alpha = 0f;
            realmsGroup.alpha = 0f;
            settingsGroup.alpha = 0f;
            controlsGroup.alpha = 0f;
            creditsGroup.alpha = 0f;
            quitGroup.alpha = 0f;

            // 5. Whispered Footer & Intro Skip Prompt
            var footer = CreateCenterLabel("Footer Brand", safeFrame, "THE LIGHTEN ROAD   •   ATMOSPHERIC LANTERN PLATFORMER", 10.5f, mutedIndigo, new Vector2(820f, 24f), new Vector2(0f, -508f));
            footer.characterSpacing = 5.5f;

            hintText = CreateCenterLabel("Skip Hint", safeFrame, "PRESS [ENTER], [SPACE] OR CLICK TO AWAKEN THE LANTERN", 11f, amberGold, new Vector2(680f, 24f), new Vector2(0f, -476f));
            hintText.characterSpacing = 3.5f;

            // 6. Cinematic Curtain & Loading Indicator
            curtain = CreateFullStretchImage("Cinematic Curtain", rootRect, new Color(0.010f, 0.016f, 0.036f, 1f));

            var loadRect = CreateAnchoredRect("Loading Container", rootRect, new Vector2(680f, 84f), new Vector2(0.5f, 0.14f), Vector2.zero);
            loadingContainer = loadRect.gameObject;

            loadingText = CreateCenterLabel("Loading Text", loadRect, "ENTERING THE ARCHIVE...", 14.5f, radiantIvory, new Vector2(680f, 34f), new Vector2(0f, 18f));
            loadingText.characterSpacing = 6f;

            var barBg = CreateAnchoredRect("Loading Bar BG", loadRect, new Vector2(420f, 3f), new Vector2(0.5f, 0.5f), new Vector2(0f, -14f));
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
        // HOME VIEW: ULTRA-MINIMAL SINGLE-LINE NAVIGATION & FLOATING CREST
        // =====================================================================

        private void BuildMinimalHomeView()
        {
            // Floating Title Container (gently levitates in Update)
            titleFloatGroup = CreateAnchoredRect("Title Float Group", home, new Vector2(1200f, 240f), new Vector2(0.5f, 0.5f), new Vector2(0f, 265f));

            var titleAura = CreateAnchoredRect("Title Soft Lantern Glow", titleFloatGroup, new Vector2(920f, 260f), new Vector2(0.5f, 0.5f), Vector2.zero);
            titleAuraImage = titleAura.gameObject.AddComponent<Image>();
            titleAuraImage.sprite = softRadialSprite;
            titleAuraImage.color = new Color(1.0f, 0.66f, 0.20f, 0.16f);
            titleAuraImage.raycastTarget = false;

            var topCrest = CreateAnchoredRect("Top Ornate Crest", titleFloatGroup, new Vector2(440f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f));
            var topCrestImg = topCrest.gameObject.AddComponent<Image>();
            topCrestImg.sprite = filigreeSprite;
            topCrestImg.color = new Color(1.0f, 0.78f, 0.36f, 0.72f);
            topCrestImg.raycastTarget = false;

            var eyebrow = CreateCenterLabel("Eyebrow", titleFloatGroup, "SOME LIGHTS REFUSE TO DIE", 11.5f, moonlightSilver, new Vector2(680f, 24f), new Vector2(0f, 62f));
            eyebrow.characterSpacing = 11f;

            var mainTitle = CreateCenterLabel("Title Lighten Road", titleFloatGroup, "THE LIGHTEN ROAD", 68f, radiantIvory, new Vector2(1280f, 88f), new Vector2(0f, 2f));
            mainTitle.fontStyle = FontStyles.Bold;
            mainTitle.characterSpacing = 13f;

            var midCrest = CreateAnchoredRect("Title Bottom Filigree", titleFloatGroup, new Vector2(580f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, -58f));
            var midCrestImg = midCrest.gameObject.AddComponent<Image>();
            midCrestImg.sprite = filigreeSprite;
            midCrestImg.color = amberGold;
            midCrestImg.raycastTarget = false;

            activeRealmBadgeText = CreateCenterLabel("Active Realm Text", titleFloatGroup, "REALM I   •   THE RUINED ARCHIVE", 11.5f, amberGold, new Vector2(680f, 24f), new Vector2(0f, -92f));
            activeRealmBadgeText.characterSpacing = 5f;

            // Ultra-Minimal Single-Line Menu Stack (Zero cluttered sub-labels under buttons!)
            playButton = CreateMinimalMenuButton(
                home,
                objectName: "Enter the Archive",
                displayLabel: "ENTER THE ARCHIVE",
                contextHint: "Awaken the lantern and step into the selected archive realm",
                yPos: 58f,
                cameraPan: new Vector2(0f, 22f),
                onClick: Play,
                isPrimary: true);

            CreateMinimalMenuButton(
                home,
                objectName: "Realms & Chapters",
                displayLabel: "REALMS",
                contextHint: "Select between The Ruined Archive, Flooded Mechanisms & Test Chamber",
                yPos: 2f,
                cameraPan: new Vector2(-28f, 10f),
                onClick: ShowRealms);

            CreateMinimalMenuButton(
                home,
                objectName: "Settings",
                displayLabel: "SETTINGS",
                contextHint: "Configure audio mixer levels, display mode, V-Sync & motion",
                yPos: -50f,
                cameraPan: new Vector2(28f, 4f),
                onClick: ShowSettings);

            CreateMinimalMenuButton(
                home,
                objectName: "Controls & Guide",
                displayLabel: "CONTROLS",
                contextHint: "View movement bindings, coyote jump, lantern flare & ember conduits",
                yPos: -102f,
                cameraPan: new Vector2(-20f, -12f),
                onClick: ShowControls);

            CreateMinimalMenuButton(
                home,
                objectName: "Credits",
                displayLabel: "CREDITS",
                contextHint: "Meet the creators and soundscape artists behind The Lighten Road",
                yPos: -154f,
                cameraPan: new Vector2(20f, -16f),
                onClick: ShowCredits);

            CreateMinimalMenuButton(
                home,
                objectName: "Leave the Light",
                displayLabel: "EXIT",
                contextHint: "Extinguish the lantern and return to desktop",
                yPos: -206f,
                cameraPan: new Vector2(0f, -28f),
                onClick: ShowQuit);

            // Single Whispered Context Hint at the bottom of the stack
            contextWhisperText = CreateCenterLabel(
                "Context Whisper",
                home,
                "Awaken the lantern and step into the selected archive realm",
                12f,
                moonlightSilver,
                new Vector2(760f, 26f),
                new Vector2(0f, -282f));
            contextWhisperText.characterSpacing = 2f;
        }

        private Button CreateMinimalMenuButton(
            RectTransform parent,
            string objectName,
            string displayLabel,
            string contextHint,
            float yPos,
            Vector2 cameraPan,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary = false)
        {
            var itemRect = CreateAnchoredRect(objectName, parent, new Vector2(480f, isPrimary ? 48f : 42f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var hitGraphic = itemRect.gameObject.AddComponent<Image>();
            hitGraphic.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(420f, 64f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            var wingContainer = CreateStretchRect("Ornate Wings", itemRect);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = isPrimary ? 0.35f : 0f;
            wingGroup.blocksRaycasts = false;

            float wingOffset = Mathf.Max(115f, displayLabel.Length * 8.5f + 48f);

            var leftWing = CreateOrnateWingGraphic("Left Wing", wingContainer, new Vector2(-wingOffset, 0f), isLeft: true, out RectTransform leftDiamond);
            var rightWing = CreateOrnateWingGraphic("Right Wing", wingContainer, new Vector2(wingOffset, 0f), isLeft: false, out RectTransform rightDiamond);

            var titleTxt = CreateCenterLabel("Title", itemRect, displayLabel, isPrimary ? 23f : 18.5f, isPrimary ? amberGold : radiantIvory, new Vector2(440f, 34f), Vector2.zero);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 8f : 6.5f;

            var lineRect = CreateAnchoredRect("Underline Filigree", itemRect, new Vector2(0f, 10f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f));
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
            motion.SoftGlow = glowImg;
            motion.LeftWing = leftWing;
            motion.RightWing = rightWing;
            motion.LeftDiamond = leftDiamond;
            motion.RightDiamond = rightDiamond;
            motion.WingGroup = wingGroup;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;
            motion.BaseWingOffset = wingOffset;
            motion.FocusedUnderlineWidth = 220f;
            motion.ContextHint = contextHint;
            motion.StageFocusCanvasPosition = new Vector2(0f, yPos);
            motion.StageCameraPanOffset = cameraPan;
            motion.OnFocusedCallback = OnMenuButtonFocused;

            return btn;
        }

        private RectTransform CreateOrnateWingGraphic(string name, RectTransform parent, Vector2 pos, bool isLeft, out RectTransform diamondRect)
        {
            var wing = CreateAnchoredRect(name, parent, new Vector2(84f, 18f), new Vector2(0.5f, 0.5f), pos);

            var stem = CreateAnchoredRect("Filigree Stem", wing, new Vector2(64f, 10f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? -8f : 8f, 0f));
            var stemImg = stem.gameObject.AddComponent<Image>();
            stemImg.sprite = filigreeSprite;
            stemImg.color = amberGold;
            stemImg.raycastTarget = false;

            diamondRect = CreateAnchoredRect("Diamond Tip", wing, new Vector2(7.5f, 7.5f), new Vector2(0.5f, 0.5f), new Vector2(isLeft ? 28f : -28f, 0f));
            diamondRect.localEulerAngles = new Vector3(0f, 0f, 45f);
            var diamondImg = diamondRect.gameObject.AddComponent<Image>();
            diamondImg.color = radiantIvory;
            diamondImg.raycastTarget = false;

            return wing;
        }

        private void OnMenuButtonFocused(MenuButtonMotion motion)
        {
            if (motion == null) return;
            if (!string.IsNullOrEmpty(motion.ContextHint) && contextWhisperText != null)
            {
                contextWhisperText.text = motion.ContextHint;
            }
            stage?.TriggerInteractionSurge(motion.StageFocusCanvasPosition, motion.StageCameraPanOffset, 1.0f);
        }

        // =====================================================================
        // SUB-VIEW 1: REALMS & CHAPTERS (CLEAN 3-CARD SELECTOR)
        // =====================================================================

        private void BuildRealmsView()
        {
            var surface = CreateSubViewStage(realms, "REALMS & CHAPTERS", "SELECT DESTINATION REALM");

            realmCardBgs.Clear();
            realmCardBorders.Clear();
            realmStatusLabels.Clear();

            for (int i = 0; i < AvailableRealms.Length; i++)
            {
                int capturedIndex = i;
                var r = AvailableRealms[i];
                float yPos = 118f - i * 126f;

                var card = CreateAnchoredRect($"Realm Option {i}", surface, new Vector2(880f, 108f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
                var bg = card.gameObject.AddComponent<Image>();
                bg.color = glassCard;
                realmCardBgs.Add(bg);

                var leftAccent = CreateAnchoredRect("Left Accent", card, new Vector2(3.5f, 108f), new Vector2(0f, 0.5f), new Vector2(1.75f, 0f));
                var borderImg = leftAccent.gameObject.AddComponent<Image>();
                borderImg.color = subtleBorder;
                borderImg.raycastTarget = false;
                realmCardBorders.Add(borderImg);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectRealm(capturedIndex));
                if (i == 0) realmsFirst = btn;

                var tagTxt = CreateLeftLabel("Realm Number", card, $"{r.Number}   •   {r.Tag}", 11f, amberGold, 580f, 20f, 26f, 30f);
                tagTxt.characterSpacing = 3f;

                var statusTxt = CreateRightLabel("Status", card, "SELECT", 11f, amberGold, 180f, 20f, -26f, 30f);
                statusTxt.fontStyle = FontStyles.Bold;
                statusTxt.characterSpacing = 3f;
                realmStatusLabels.Add(statusTxt);

                var titleTxt = CreateLeftLabel("Realm Title", card, r.Title, 22f, radiantIvory, 640f, 28f, 26f, 3f);
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.characterSpacing = 3f;

                var descTxt = CreateLeftLabel("Realm Desc", card, r.Description, 12.5f, moonlightSilver, 820f, 22f, 26f, -25f);
            }

            CreateActionPillButton(surface, "<  RETURN", new Vector2(-170f, -278f), ShowHome, isPrimary: false);
            CreateActionPillButton(surface, "ENTER REALM  >", new Vector2(170f, -278f), Play, isPrimary: true);
        }

        // =====================================================================
        // SUB-VIEW 2: PROFESSIONAL 2-COLUMN SETTINGS UX
        // =====================================================================

        private void BuildProfessionalSettingsView()
        {
            var surface = CreateSubViewStage(settings, "SETTINGS", "AUDIO MIXER, DISPLAY & ACCESSIBILITY");

            // Left Column Header: AUDIO MIXER (x = -275, width = 510)
            var audioColHeader = CreateAnchoredRect("Audio Column Header", surface, new Vector2(510f, 28f), new Vector2(0.5f, 0.5f), new Vector2(-275f, 184f));
            var audioHdrTxt = CreateLeftLabel("HeaderText", audioColHeader, "AUDIO MIXER", 12f, amberGold, 300f, 24f, 4f, 2f);
            audioHdrTxt.fontStyle = FontStyles.Bold;
            audioHdrTxt.characterSpacing = 4f;
            var audioRule = CreateAnchoredRect("Rule", audioColHeader, new Vector2(510f, 1f), new Vector2(0.5f, 0f), Vector2.zero);
            audioRule.gameObject.AddComponent<Image>().color = new Color(1f, 0.74f, 0.30f, 0.28f);

            // Left Column Cards: 3 Audio Sliders + Live SFX Preview Button
            masterSlider = CreateAudioMixerCard(surface, "Master Volume", "Global output level for all archive audio", new Vector2(-275f, 114f));
            musicSlider = CreateAudioMixerCard(surface, "Ambient Music", "Atmospheric background score (In-game key: M)", new Vector2(-275f, 12f));
            effectsSlider = CreateAudioMixerCard(surface, "Sound Effects (SFX)", "Footsteps, lantern flare & embers (In-game key: N)", new Vector2(-275f, -90f));

            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);

            // Live Audio Preview Card
            var testAudioCard = CreateAnchoredRect("Test Audio Button", surface, new Vector2(510f, 52f), new Vector2(0.5f, 0.5f), new Vector2(-275f, -178f));
            var testBg = testAudioCard.gameObject.AddComponent<Image>();
            testBg.color = new Color(0.14f, 0.10f, 0.05f, 0.72f);
            var testBorder = CreateAnchoredRect("Left Accent", testAudioCard, new Vector2(3f, 52f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
            testBorder.gameObject.AddComponent<Image>().color = amberGold;

            var testLbl = CreateLeftLabel("Label", testAudioCard, "TEST LANTERN FLARE SOUND", 13f, amberGold, 320f, 24f, 20f, 0f);
            testLbl.fontStyle = FontStyles.Bold;
            testLbl.characterSpacing = 2.5f;
            var testSub = CreateRightLabel("Hint", testAudioCard, "PREVIEW SFX  >", 11f, radiantIvory, 140f, 24f, -20f, 0f);
            testSub.characterSpacing = 2f;

            var testBtn = testAudioCard.gameObject.AddComponent<Button>();
            testBtn.targetGraphic = testBg;
            testBtn.onClick.AddListener(TestLanternAudioSample);

            // Right Column Header: DISPLAY & ACCESSIBILITY (x = +275, width = 510)
            var dispColHeader = CreateAnchoredRect("Display Column Header", surface, new Vector2(510f, 28f), new Vector2(0.5f, 0.5f), new Vector2(275f, 184f));
            var dispHdrTxt = CreateLeftLabel("HeaderText", dispColHeader, "DISPLAY & MOTION", 12f, amberGold, 340f, 24f, 4f, 2f);
            dispHdrTxt.fontStyle = FontStyles.Bold;
            dispHdrTxt.characterSpacing = 4f;
            var dispRule = CreateAnchoredRect("Rule", dispColHeader, new Vector2(510f, 1f), new Vector2(0.5f, 0f), Vector2.zero);
            dispRule.gameObject.AddComponent<Image>().color = new Color(1f, 0.74f, 0.30f, 0.28f);

            // Right Column Cards: 3 Sliding Toggle Switches + Target FPS Cycler
            fullscreenToggle = CreateSlidingSwitchCard(surface, "Fullscreen Mode", "Expand across the entire screen", new Vector2(275f, 122f), OnFullscreenToggled);
            vsyncToggle = CreateSlidingSwitchCard(surface, "Vertical Sync (V-Sync)", "Synchronize frame pacing with display", new Vector2(275f, 36f), OnVSyncToggled);
            motionToggle = CreateSlidingSwitchCard(surface, "Reduced Motion", "Calm camera parallax & lantern sway", new Vector2(275f, -50f), OnReducedMotionToggled);
            CreateFpsSelectorCard(surface, "Target Frame Rate", "Frame rate ceiling (60 / 120 / Unlimited)", new Vector2(275f, -136f));

            // Bottom Action Bar
            CreateActionPillButton(surface, "RESET DEFAULTS", new Vector2(-170f, -278f), ResetDefaults, isPrimary: false);
            CreateActionPillButton(surface, "<  SAVE & RETURN", new Vector2(170f, -278f), ShowHome, isPrimary: true);

            SyncSettingsUI();
        }

        private Slider CreateAudioMixerCard(RectTransform parent, string title, string subtitle, Vector2 centerPos)
        {
            var card = CreateAnchoredRect(title, parent, new Vector2(510f, 90f), new Vector2(0.5f, 0.5f), centerPos);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = glassCard;
            bg.raycastTarget = false;

            var leftAccent = CreateAnchoredRect("Left Accent", card, new Vector2(3f, 90f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
            leftAccent.gameObject.AddComponent<Image>().color = subtleBorder;

            // Top Row: Title on Left (x = 20), Live Percentage on Right (x = -20)
            var titleTxt = CreateLeftLabel("Title", card, title.ToUpperInvariant(), 14f, radiantIvory, 340f, 22f, 20f, 24f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var valTxt = CreateRightLabel("PercentValue", card, "85%", 14f, amberGold, 90f, 22f, -20f, 24f);
            valTxt.fontStyle = FontStyles.Bold;

            // Middle Row: Custom Glowing Slider Track (width = 470, centered at y = -2)
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(card, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(0.5f, 0.5f);
            sRect.anchorMax = new Vector2(0.5f, 0.5f);
            sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.sizeDelta = new Vector2(470f, 22f);
            sRect.anchoredPosition = new Vector2(0f, -2f);

            var track = CreateAnchoredRect("Track", sRect, new Vector2(470f, 5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            track.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.85f);

            // Subtle 25%, 50%, 75% tick marks on the track
            for (int t = 1; t <= 3; t++)
            {
                var tick = CreateAnchoredRect($"Tick_{t * 25}", track, new Vector2(1.5f, 9f), new Vector2(t * 0.25f, 0.5f), Vector2.zero);
                tick.gameObject.AddComponent<Image>().color = new Color(0.65f, 0.78f, 0.96f, 0.22f);
            }

            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = amberGold;

            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(14f, 14f), new Vector2(0f, 0.5f), Vector2.zero);
            handle.localEulerAngles = new Vector3(0f, 0f, 45f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = radiantIvory;

            // Bottom Row: Subtitle on Left (x = 20, y = -26)
            CreateLeftLabel("Subtitle", card, subtitle, 11f, moonlightSilver, 460f, 18f, 20f, -26f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            return slider;
        }

        private Toggle CreateSlidingSwitchCard(RectTransform parent, string title, string subtitle, Vector2 centerPos, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var card = CreateAnchoredRect(title, parent, new Vector2(510f, 74f), new Vector2(0.5f, 0.5f), centerPos);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = glassCard;

            var leftAccent = CreateAnchoredRect("Left Accent", card, new Vector2(3f, 74f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
            leftAccent.gameObject.AddComponent<Image>().color = subtleBorder;

            var titleTxt = CreateLeftLabel("Title", card, title.ToUpperInvariant(), 14f, radiantIvory, 350f, 22f, 20f, 11f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            CreateLeftLabel("Subtitle", card, subtitle, 11.5f, moonlightSilver, 350f, 18f, 20f, -12f);

            // Animated Sliding Switch Track on Right (x = -20, width = 84, height = 30)
            var switchTrack = CreateAnchoredRect("SwitchTrack", card, new Vector2(84f, 30f), new Vector2(1f, 0.5f), new Vector2(-62f, 0f));
            var trackImg = switchTrack.gameObject.AddComponent<Image>();
            trackImg.color = new Color(0.08f, 0.12f, 0.22f, 0.65f);
            trackImg.raycastTarget = false;

            var stateTxt = CreateCenterLabel("StateText", switchTrack, "OFF", 10.5f, mutedIndigo, new Vector2(44f, 24f), new Vector2(14f, 0f));
            stateTxt.fontStyle = FontStyles.Bold;
            stateTxt.characterSpacing = 1.5f;

            var knob = CreateAnchoredRect("Knob", switchTrack, new Vector2(20f, 20f), new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f));
            knob.localEulerAngles = new Vector3(0f, 0f, 45f);
            var knobImg = knob.gameObject.AddComponent<Image>();
            knobImg.color = mutedIndigo;
            knobImg.raycastTarget = false;

            var toggle = card.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = null;
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private void CreateFpsSelectorCard(RectTransform parent, string title, string subtitle, Vector2 centerPos)
        {
            var card = CreateAnchoredRect(title, parent, new Vector2(510f, 74f), new Vector2(0.5f, 0.5f), centerPos);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = glassCard;

            var leftAccent = CreateAnchoredRect("Left Accent", card, new Vector2(3f, 74f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
            leftAccent.gameObject.AddComponent<Image>().color = subtleBorder;

            var titleTxt = CreateLeftLabel("Title", card, title.ToUpperInvariant(), 14f, radiantIvory, 320f, 22f, 20f, 11f);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            CreateLeftLabel("Subtitle", card, subtitle, 11.5f, moonlightSilver, 320f, 18f, 20f, -12f);

            var pill = CreateAnchoredRect("FPS Pill", card, new Vector2(144f, 32f), new Vector2(1f, 0.5f), new Vector2(-92f, 0f));
            pill.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.62f, 0.18f, 0.24f);

            fpsButtonValueText = CreateCenterLabel("FPS Value", pill, "<  60 FPS  >", 12f, amberGold, new Vector2(140f, 30f), Vector2.zero);
            fpsButtonValueText.fontStyle = FontStyles.Bold;
            fpsButtonValueText.characterSpacing = 2f;

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(CycleTargetFPS);
        }

        // =====================================================================
        // SUB-VIEW 3: PROFESSIONAL 2x3 CONTROLS & ABILITY GRID (ZERO OVERLAP!)
        // =====================================================================

        private void BuildProfessionalControlsView()
        {
            var surface = CreateSubViewStage(controls, "CONTROLS & ABILITY GUIDE", "MOVEMENT, LANTERN MECHANISMS & SHORTCUTS");

            // Left Column (x = -275): Movement & Stamina
            CreateControlCard(
                surface,
                keys: "A / D",
                subKey: "or  ARROWS",
                category: "LOCOMOTION",
                actionTitle: "TRAVERSE THE ARCHIVE",
                desc: "Smooth horizontal run with air momentum control.",
                centerPos: new Vector2(-275f, 118f));

            CreateControlCard(
                surface,
                keys: "SPACE",
                subKey: "or  W",
                category: "ACROBATICS",
                actionTitle: "LEAP & COYOTE JUMP",
                desc: "Hold for higher arc; forgiving ledge grace window.",
                centerPos: new Vector2(-275f, 0f));

            CreateControlCard(
                surface,
                keys: "SHIFT",
                subKey: "HOLD",
                category: "STAMINA",
                actionTitle: "ARCHIVE SPRINT",
                desc: "Burst speed for wide chasms; drains stamina bar.",
                centerPos: new Vector2(-275f, -118f));

            // Right Column (x = +275): Lantern, Conduits & Audio
            CreateControlCard(
                surface,
                keys: "F",
                subKey: "or  L-CLICK",
                category: "LANTERN ABILITY",
                actionTitle: "LANTERN FLARE SURGE",
                desc: "Ignite a radiant light burst to reveal dark platforms.",
                centerPos: new Vector2(275f, 118f));

            CreateControlCard(
                surface,
                keys: "HOLD E",
                subKey: "INTERACT",
                category: "ANCIENT MECHANISM",
                actionTitle: "CHANNEL EMBER VEIN",
                desc: "Transfer lantern light into conduits, doors & lifts.",
                centerPos: new Vector2(275f, 0f));

            CreateControlCard(
                surface,
                keys: "M   /   N",
                subKey: "HOTKEYS",
                category: "LIVE MIXER",
                actionTitle: "AUDIO QUICK TOGGLES",
                desc: "Mute music (M) or sound effects (N) anytime in-game.",
                centerPos: new Vector2(275f, -118f));

            controlsFirst = CreateActionPillButton(surface, "<  RETURN TO TITLE", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateControlCard(
            RectTransform parent,
            string keys,
            string subKey,
            string category,
            string actionTitle,
            string desc,
            Vector2 centerPos)
        {
            var card = CreateAnchoredRect(actionTitle, parent, new Vector2(510f, 102f), new Vector2(0.5f, 0.5f), centerPos);
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = glassCard;
            bg.raycastTarget = false;

            var leftAccent = CreateAnchoredRect("Left Accent", card, new Vector2(3f, 102f), new Vector2(0f, 0.5f), new Vector2(1.5f, 0f));
            leftAccent.gameObject.AddComponent<Image>().color = amberGold;

            // 1. Hardware Keycap Box on the Left (Strictly x = 18..154, width = 136)
            var keycapShadow = CreateAnchoredRect("Keycap Bevel", card, new Vector2(136f, 68f), new Vector2(0f, 0.5f), new Vector2(86f, -3f));
            keycapShadow.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.015f, 0.03f, 0.95f);

            var keycapFace = CreateAnchoredRect("Keycap Badge", card, new Vector2(136f, 64f), new Vector2(0f, 0.5f), new Vector2(86f, 1f));
            keycapFace.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.22f, 0.95f);

            var keycapTopRim = CreateAnchoredRect("Keycap Rim", keycapFace, new Vector2(136f, 2f), new Vector2(0.5f, 1f), new Vector2(0f, -1f));
            keycapTopRim.gameObject.AddComponent<Image>().color = new Color(1.0f, 0.74f, 0.28f, 0.65f);

            var keyTxt = CreateCenterLabel("Key Text", keycapFace, keys, 15.5f, amberGold, new Vector2(126f, 28f), new Vector2(0f, 8f));
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 2f;

            var subKeyTxt = CreateCenterLabel("SubKey Text", keycapFace, subKey, 9.5f, moonlightSilver, new Vector2(126f, 18f), new Vector2(0f, -14f));
            subKeyTxt.characterSpacing = 2f;

            // 2. Vertical Divider at x = 172
            var divider = CreateAnchoredRect("Vertical Rule", card, new Vector2(1f, 66f), new Vector2(0f, 0.5f), new Vector2(172f, 0f));
            divider.gameObject.AddComponent<Image>().color = new Color(0.58f, 0.72f, 0.96f, 0.18f);

            // 3. Right Text Zone (Strictly left-aligned starting at x = 190, width = 304 -> zero overlap!)
            var catTxt = CreateLeftLabel("Category", card, category, 10f, amberGold, 304f, 18f, 190f, 26f);
            catTxt.fontStyle = FontStyles.Bold;
            catTxt.characterSpacing = 3.5f;

            var actTxt = CreateLeftLabel("Action", card, actionTitle, 15f, radiantIvory, 304f, 24f, 190f, 4f);
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 2f;

            CreateLeftLabel("Description", card, desc, 11.5f, moonlightSilver, 304f, 20f, 190f, -20f);
        }

        // =====================================================================
        // SUB-VIEW 4: CREDITS
        // =====================================================================

        private void BuildCreditsView()
        {
            var surface = CreateSubViewStage(credits, "CREDITS", "THE TEAM BEHIND THE LIGHTEN ROAD");

            CreateCreditCard(surface, "SIRAÇ ŞİMŞEK", "MOVEMENT, LANTERN SYSTEMS, AUDIO & PROCEDURAL FRONTEND", "Player physics, stamina & lantern HUD, dynamic lighting, foley & 2.5D animated archive stage", 112f);
            CreateCreditCard(surface, "KEMAL", "PUZZLE MECHANISMS & EMBER VEIN ARCHITECTURE", "Ember Vein conduits, Flooded Mechanisms sandbox, interactive elevators & hazards", -6f);
            CreateCreditCard(surface, "ARCHIVE SOUNDSCAPE & ART", "ATMOSPHERIC AUDIO & PROCEDURAL SILHOUETTES", "Custom multi-layered SDF gothic colonnades, swinging lantern light rays & CC0 foley", -124f);

            creditsFirst = CreateActionPillButton(surface, "<  RETURN TO TITLE", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateCreditCard(RectTransform parent, string name, string role, string details, float yPos)
        {
            var card = CreateAnchoredRect(name, parent, new Vector2(820f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
            var bg = card.gameObject.AddComponent<Image>();
            bg.color = glassCard;
            bg.raycastTarget = false;

            var topBorder = CreateAnchoredRect("Top Filigree", card, new Vector2(420f, 14f), new Vector2(0.5f, 1f), new Vector2(0f, -7f));
            var topImg = topBorder.gameObject.AddComponent<Image>();
            topImg.sprite = filigreeSprite;
            topImg.color = new Color(1.0f, 0.74f, 0.30f, 0.45f);

            var nameTxt = CreateCenterLabel("Name", card, name, 22f, radiantIvory, new Vector2(760f, 28f), new Vector2(0f, 22f));
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.characterSpacing = 4.5f;

            var roleTxt = CreateCenterLabel("Role", card, role, 11f, amberGold, new Vector2(760f, 18f), new Vector2(0f, -2f));
            roleTxt.characterSpacing = 3.5f;

            CreateCenterLabel("Details", card, details, 12f, moonlightSilver, new Vector2(760f, 20f), new Vector2(0f, -24f));
        }

        // =====================================================================
        // SUB-VIEW 5: QUIT CONFIRMATION
        // =====================================================================

        private void BuildQuitView()
        {
            var surface = CreateAnchoredRect("Quit Ethereal Frame", quit, new Vector2(760f, 360f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var topFiligree = CreateAnchoredRect("Top Filigree", surface, new Vector2(540f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, 138f));
            var topImg = topFiligree.gameObject.AddComponent<Image>();
            topImg.sprite = filigreeSprite;
            topImg.color = amberGold;
            topImg.raycastTarget = false;

            var tag = CreateCenterLabel("Quit Tag", surface, "UNTIL YOUR NEXT RETURN", 11.5f, amberGold, new Vector2(620f, 24f), new Vector2(0f, 94f));
            tag.characterSpacing = 6f;

            var title = CreateCenterLabel("Quit Title", surface, "LEAVE THE LIGHT BEHIND?", 36f, radiantIvory, new Vector2(720f, 48f), new Vector2(0f, 42f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 5f;

            CreateCenterLabel("Quit Note", surface, "The ruined archive will wait in the midnight dark.\nAll audio and realm preferences have been saved.", 14f, moonlightSilver, new Vector2(620f, 52f), new Vector2(0f, -20f));

            var botFiligree = CreateAnchoredRect("Bottom Filigree", surface, new Vector2(540f, 38f), new Vector2(0.5f, 0.5f), new Vector2(0f, -74f));
            var botImg = botFiligree.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.65f);
            botImg.raycastTarget = false;

            quitFirst = CreateActionPillButton(surface, "STAY IN THE LIGHT", new Vector2(-160f, -138f), ShowHome, isPrimary: true, width: 270f);
            CreateActionPillButton(surface, "EXTINGUISH & EXIT", new Vector2(160f, -138f), ConfirmQuit, isPrimary: false, width: 270f);
        }

        // =====================================================================
        // SHARED STAGE & PIXEL-ALIGNED LABEL HELPERS
        // =====================================================================

        private RectTransform CreateSubViewStage(RectTransform viewRoot, string heading, string subheading)
        {
            var container = CreateAnchoredRect("Subview Stage", viewRoot, new Vector2(1140f, 720f), new Vector2(0.5f, 0.5f), Vector2.zero);

            var topCrest = CreateAnchoredRect("Header Top Filigree", container, new Vector2(560f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0f, 326f));
            var topImg = topCrest.gameObject.AddComponent<Image>();
            topImg.sprite = filigreeSprite;
            topImg.color = amberGold;
            topImg.raycastTarget = false;

            var sub = CreateCenterLabel("Subheading", container, subheading, 11f, amberGold, new Vector2(820f, 22f), new Vector2(0f, 292f));
            sub.characterSpacing = 5.5f;

            var head = CreateCenterLabel("Heading", container, heading, 34f, radiantIvory, new Vector2(860f, 44f), new Vector2(0f, 252f));
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 6f;

            var botCrest = CreateAnchoredRect("Header Bottom Filigree", container, new Vector2(720f, 32f), new Vector2(0.5f, 0.5f), new Vector2(0f, 212f));
            var botImg = botCrest.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.55f);
            botImg.raycastTarget = false;

            return container;
        }

        private Button CreateActionPillButton(
            RectTransform parent,
            string label,
            Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary,
            float width = 290f)
        {
            var rect = CreateAnchoredRect(label, parent, new Vector2(width, 48f), new Vector2(0.5f, 0.5f), anchoredPos);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = isPrimary ? new Color(0.16f, 0.11f, 0.05f, 0.78f) : new Color(0.05f, 0.08f, 0.15f, 0.68f);

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width + 40f, 62f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = isPrimary ? new Color(1.0f, 0.66f, 0.20f, 0.18f) : Color.clear;
            glowImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(width * 0.8f, 12f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = isPrimary ? amberGold : subtleBorder;
            lineImg.raycastTarget = false;

            var txt = CreateCenterLabel("Caption", rect, label, 14f, isPrimary ? amberGold : radiantIvory, new Vector2(width - 16f, 40f), Vector2.zero);
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3.5f;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (Application.isPlaying && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayEmberPickup();
                onClick();
            });

            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = txt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.IsPrimaryAction = isPrimary;
            motion.BaseUnderlineWidth = width * 0.65f;
            motion.FocusedUnderlineWidth = width * 0.92f;
            motion.StageFocusCanvasPosition = anchoredPos;
            motion.StageCameraPanOffset = Vector2.zero;
            motion.OnFocusedCallback = OnMenuButtonFocused;

            return btn;
        }

        private TMP_Text CreateLeftLabel(string name, RectTransform parent, string content, float fontSize, Color color, float width, float height, float leftX, float offsetY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(leftX, offsetY);

            var text = go.AddComponent<TextMeshProUGUI>();
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

        private TMP_Text CreateRightLabel(string name, RectTransform parent, string content, float fontSize, Color color, float width, float height, float rightX, float offsetY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(rightX, offsetY);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private TMP_Text CreateCenterLabel(string name, RectTransform parent, string content, float fontSize, Color color, Vector2 size, Vector2 centerOffset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = centerOffset;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
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
