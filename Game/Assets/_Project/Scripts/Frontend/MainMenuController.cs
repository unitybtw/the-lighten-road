using System.Collections;
using System.Collections.Generic;
using TheLightenRoad.Audio;
using TheLightenRoad.UI;
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
    /// Redesigned Full 2D Orthographic Parallax Main Menu for The Lighten Road.
    /// Pairs a multi-layered 2D pixel-art archive stage featuring Character_Light_Stand.png
    /// holding a glowing lantern on the right with a sleek left-aligned vertical menu list,
    /// animated pixel-art Flame Cursor, and a lantern-zoom radiant white-gold Play transition.
    /// </summary>
    public sealed partial class MainMenuController : MonoBehaviour
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
                Tag = "TUTORIAL & BEGINNINGS",
                Description = "Learn basic movement mechanics, explore the ruined archive, and power the conduit to advance.",
                ScenePath = DefaultGameplayScene
            },
            new RealmOption
            {
                Number = "REALM II",
                Title = "THE FLOODED MECHANISMS",
                Tag = "PUZZLES & MECHANICS",
                Description = "Channel lantern energy into ancient Ember Veins and navigate past electrified waters and lifts.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity"
            },
            new RealmOption
            {
                Number = "REALM III",
                Title = "ARCHIVE TEST CHAMBER",
                Tag = "CALIBRATION ROOM",
                Description = "Testing ground for lantern light radius, movement physics, coyote time, and environmental hazards.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity"
            }
        };

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private Sprite hangingLampSprite;
        [SerializeField] private Sprite chainLinkSprite;

        public bool LoadSceneOnPlay { get; set; } = true;

        // Moonlight & Silver Mist Palette with Warm Ember Highlights (Hollow Knight Inspired)
        private readonly Color radiantIvory = new Color32(236, 242, 252, 255);
        private readonly Color moonlightSilver = new Color32(168, 186, 214, 225);
        private readonly Color mutedIndigo = new Color32(110, 130, 160, 185);
        private readonly Color silverBorder = new Color32(142, 168, 204, 92);
        private readonly Color amberGold = new Color32(255, 178, 68, 255);
        private readonly Color deepAmber = new Color32(214, 118, 32, 255);
        private static readonly Color FocusedAmberGold = new Color32(255, 210, 108, 255);

        private Canvas canvas;
        private ProceduralArchiveStage stage;
        private Sprite filigreeSprite;
        private Sprite softRadialSprite;
        private Sprite topFiligreeSprite;
        private Sprite bottomCrestSprite;
        private Sprite buttonPointerSprite;

        // Sprites & Procedural Assets
        private readonly List<Sprite> proceduralSprites = new List<Sprite>();
        private Sprite vignetteBlurSprite;
        private Sprite roundTrackSprite;
        private Sprite roundFillSprite;
        private Sprite roundHandleSprite;
        private Sprite thinBorderSprite;

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

        private struct HomeButtonItem
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public MenuButtonMotion Motion;
            public Vector2 BasePos;
        }
        private readonly List<HomeButtonItem> homeButtons = new List<HomeButtonItem>();

        private RectTransform homeHeaderRect;
        private CanvasGroup homeHeaderGroup;
        private Vector2 homeHeaderBasePos;

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

        // Realms Selection & Showcase Elements (Frameless Cinematic Vitrini)
        private readonly List<Button> realmButtons = new List<Button>();
        private readonly List<TMP_Text> realmTitles = new List<TMP_Text>();
        private readonly List<CanvasGroup> realmWingGroups = new List<CanvasGroup>();
        private readonly List<Image> realmRowAuras = new List<Image>();
        private readonly List<Image> realmRowUnderlines = new List<Image>();
        private readonly List<TMP_Text> realmStatusLabels = new List<TMP_Text>();
        private TMP_Text activeRealmShowcaseTag;
        private TMP_Text activeRealmShowcaseDesc;
        private TMP_Text activeRealmShowcaseStatus;

        // Settings Elements (Frameless Minimal Rows)
        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private TMP_Text masterValueText;
        private TMP_Text musicValueText;
        private TMP_Text effectsValueText;
        private Toggle fullscreenToggle;
        private Toggle vsyncToggle;
        private Toggle motionToggle;
        private TMP_Text fullscreenToggleValueText;
        private TMP_Text vsyncToggleValueText;
        private TMP_Text motionToggleValueText;

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
        public TMP_Text ActiveRealmBadgeText => activeRealmBadgeText;
        public CanvasGroup HomeGroup => homeGroup;
        public CanvasGroup RealmsGroup => realmsGroup;
        public CanvasGroup SettingsGroup => settingsGroup;
        public CanvasGroup ControlsGroup => controlsGroup;
        public CanvasGroup CreditsGroup => creditsGroup;
        public CanvasGroup QuitGroup => quitGroup;

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
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            TickSettingsSave(dt);
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
                if(view=="settings" && settingsResetPanel!=null && settingsResetPanel.activeSelf)CancelSettingsReset();
                else if (view == "home") ShowQuit();
                else ShowHome();
            }

            if (view == "settings" && Gamepad.current != null)
            {
                if (Gamepad.current.leftShoulder.wasPressedThisFrame)
                {
                    SelectSettingsSection((settingsSection - 1 + 4) % 4);
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
                }
                else if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                {
                    SelectSettingsSection((settingsSection + 1) % 4);
                    if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
                }
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

            // Ethereal Midnight Blur & Vignette when a sub-view is open
            bool isSubView = view != "home";
            stage?.SetSubViewFocus(isSubView);
            if (subpanelVeil != null)
            {
                float targetVeil = view == "quit" ? 0.94f : (isSubView ? 0.88f : 0f);
                float curA = subpanelVeil.color.a;
                float newA = Mathf.MoveTowards(curA, targetVeil, dt * 3.5f);
                subpanelVeil.color = new Color(1f, 1f, 1f, newA);
            }

            float transitionProgress = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.Clamp01(viewAge / 0.25f));
            if (!loading && !quitting)
            {
                UpdateEtherealView(homeGroup, view == "home", 1f, transitionProgress, isHome: true);
                UpdateEtherealView(realmsGroup, view == "realms", 1f, transitionProgress, isHome: false);
                UpdateEtherealView(settingsGroup, view == "settings", 1f, transitionProgress, isHome: false);
                UpdateEtherealView(controlsGroup, view == "controls", 1f, transitionProgress, isHome: false);
                UpdateEtherealView(creditsGroup, view == "credits", 1f, transitionProgress, isHome: false);
                UpdateEtherealView(quitGroup, view == "quit", 1f, transitionProgress, isHome: false);

                // Cascading Staggered Button Entrance Animation on Initial Opening
                if (view == "home")
                {
                    if (MenuPreferences.ReducedMotion || introDone)
                    {
                        if (homeHeaderGroup != null) homeHeaderGroup.alpha = 1f;
                        if (homeHeaderRect != null) homeHeaderRect.anchoredPosition = homeHeaderBasePos;
                        for (int i = 0; i < homeButtons.Count; i++)
                        {
                            var b = homeButtons[i];
                            b.Rect.anchoredPosition = b.BasePos;
                            b.Group.alpha = 1f;
                            b.Group.interactable = true;
                            b.Group.blocksRaycasts = true;
                        }
                    }
                    else
                    {
                        // Header fades down & in (age 0.35s .. 0.90s)
                        float headerT = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 0.90f, age));
                        float headerE = SmoothStep(headerT);
                        if (homeHeaderGroup != null) homeHeaderGroup.alpha = headerE;
                        if (homeHeaderRect != null) homeHeaderRect.anchoredPosition = homeHeaderBasePos + new Vector2(0f, (1f - headerE) * 16f);

                        // Each button cascades in from below (age 0.65s .. 1.80s)
                        for (int i = 0; i < homeButtons.Count; i++)
                        {
                            float btnStart = 0.65f + i * 0.10f;
                            float btnT = Mathf.Clamp01(Mathf.InverseLerp(btnStart, btnStart + 0.38f, age));
                            float btnE = 1f - Mathf.Pow(1f - btnT, 3f); // Smooth ease-out cubic
                            var b = homeButtons[i];
                            b.Rect.anchoredPosition = b.BasePos + new Vector2(0f, (1f - btnE) * -14f);
                            b.Group.alpha = btnE;
                            bool canInteract = btnE >= 0.98f;
                            b.Group.interactable = canInteract;
                            b.Group.blocksRaycasts = canInteract;
                        }
                    }
                }
            }

            if (activeRealmBadgeText != null)
            {
                float badgePulse = MenuPreferences.ReducedMotion ? 1f : (0.80f + 0.20f * Mathf.Sin(age * 2.2f));
                activeRealmBadgeText.color = new Color(amberGold.r, amberGold.g, amberGold.b, badgePulse);
            }

            if (hintText != null)
            {
                hintText.gameObject.SetActive(!introDone && !loading && !quitting);
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
                // Stretched full-screen home canvas stays fixed at zero to prevent canvas drift
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            else
            {
                // Smooth ease-out cubic for ethereal entrance & gentle midnight ambient float
                float ease = 1f - Mathf.Pow(1f - transition, 3f);
                float yRise = -18f * (1f - ease);
                float scale = Mathf.Lerp(0.95f, 1f, ease);
                float ambientFloat = (active && transition >= 0.96f) ? Mathf.Sin(viewAge * 1.6f) * 2.2f : 0f;
                rect.anchoredPosition = new Vector2(0f, yRise + ambientFloat);
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
            if (homeGroup != null) homeGroup.alpha = 1f;
            if (homeHeaderGroup != null) homeHeaderGroup.alpha = 1f;
            if (homeHeaderRect != null) homeHeaderRect.anchoredPosition = homeHeaderBasePos;
            for (int i = 0; i < homeButtons.Count; i++)
            {
                var b = homeButtons[i];
                b.Rect.anchoredPosition = b.BasePos;
                b.Group.alpha = 1f;
                b.Group.interactable = !loading && !quitting;
                b.Group.blocksRaycasts = !loading && !quitting;
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

        public void ShowHome()
        {
            if(view=="settings")FlushSettingsSave();
            SetView("home", playButton, Vector2.zero);
        }
        public void ShowRealms() { RefreshRealmSelectionUI(); SetView("realms", realmsFirst, new Vector2(-28f, -14f)); }
        public void ShowSettings() { SyncSettingsUI(); SelectSettingsSection(0); SetView("settings", masterSlider, new Vector2(24f, -18f)); }
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
                SoundEffectManager.Instance?.PlayUIClick();
            }
        }

        private void RefreshRealmSelectionUI()
        {
            if (activeRealmBadgeText != null && selectedRealmIndex >= 0 && selectedRealmIndex < AvailableRealms.Length)
            {
                var r = AvailableRealms[selectedRealmIndex];
                bool activeDone = MenuPreferences.IsRealmCompleted(selectedRealmIndex);
                activeRealmBadgeText.text = activeDone
                    ? $"--   {r.Number}  :  {r.Title}  [* CONQUERED]   --"
                    : $"--   {r.Number}  :  {r.Title}   --";
            }

            if (selectedRealmIndex >= 0 && selectedRealmIndex < AvailableRealms.Length)
            {
                var cur = AvailableRealms[selectedRealmIndex];
                bool curDone = MenuPreferences.IsRealmCompleted(selectedRealmIndex);

                if (activeRealmShowcaseTag != null) activeRealmShowcaseTag.text = $"-   {cur.Tag}   -";
                if (activeRealmShowcaseDesc != null) activeRealmShowcaseDesc.text = $"\"{cur.Description}\"";
                if (activeRealmShowcaseStatus != null)
                {
                    activeRealmShowcaseStatus.text = curDone ? "[ ACTIVE REALM  -  CONQUERED * ]" : "[ ACTIVE REALM ]";
                    activeRealmShowcaseStatus.color = curDone ? amberGold : radiantIvory;
                }
            }

            for (int i = 0; i < realmTitles.Count; i++)
            {
                bool isSelected = (i == selectedRealmIndex);
                bool isCompleted = MenuPreferences.IsRealmCompleted(i);

                if (realmTitles[i] != null)
                {
                    realmTitles[i].color = isSelected ? FocusedAmberGold : (isCompleted ? radiantIvory : moonlightSilver);
                }
                if (i < realmWingGroups.Count && realmWingGroups[i] != null)
                {
                    realmWingGroups[i].alpha = isSelected ? 1f : 0f;
                }
                if (i < realmRowAuras.Count && realmRowAuras[i] != null)
                {
                    realmRowAuras[i].color = isSelected ? new Color(1.0f, 0.72f, 0.28f, 0.22f) : new Color(0.48f, 0.62f, 0.84f, 0.04f);
                }
                if (i < realmRowUnderlines.Count && realmRowUnderlines[i] != null)
                {
                    realmRowUnderlines[i].color = isSelected ? new Color(1.0f, 0.76f, 0.32f, 0.92f) : new Color(0.56f, 0.70f, 0.88f, 0.38f);
                }
                if (i < realmStatusLabels.Count && realmStatusLabels[i] != null)
                {
                    realmStatusLabels[i].text = isSelected
                        ? (isCompleted ? "[ ACTIVE REALM - CONQUERED * ]" : "[ ACTIVE REALM ]")
                        : (isCompleted ? "[ CONQUERED * ]" : "SELECT REALM");
                    realmStatusLabels[i].color = isSelected ? amberGold : (isCompleted ? radiantIvory : moonlightSilver);
                }
            }
        }

        public void Play()
        {
            if (loading || quitting) return;
            if (LoadSceneOnPlay && !Application.CanStreamedLevelBeLoaded(gameplayScene))
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

            // Clear any active UI selection to prevent re-triggering or ghost highlights
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            // Extinguish lantern audio (flame hiss & cold air draft)
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayFlareExtinguish();
                SoundEffectManager.Instance?.PlayAirDraft();
            }

            // Immediately disable interaction on all menus so player feels the darkness setting in
            if (homeGroup != null) { homeGroup.interactable = false; homeGroup.blocksRaycasts = false; }
            if (realmsGroup != null) { realmsGroup.interactable = false; realmsGroup.blocksRaycasts = false; }
            if (settingsGroup != null) { settingsGroup.interactable = false; settingsGroup.blocksRaycasts = false; }
            if (controlsGroup != null) { controlsGroup.interactable = false; controlsGroup.blocksRaycasts = false; }
            if (creditsGroup != null) { creditsGroup.interactable = false; creditsGroup.blocksRaycasts = false; }
            if (quitGroup != null) { quitGroup.interactable = false; quitGroup.blocksRaycasts = false; }

            // Ensure only the currently active view is visible; immediately zero out all inactive subviews
            if (view != "home" && homeGroup != null) homeGroup.alpha = 0f;
            if (view != "realms" && realmsGroup != null) realmsGroup.alpha = 0f;
            if (view != "settings" && settingsGroup != null) settingsGroup.alpha = 0f;
            if (view != "controls" && controlsGroup != null) controlsGroup.alpha = 0f;
            if (view != "credits" && creditsGroup != null) creditsGroup.alpha = 0f;
            if (view != "quit" && quitGroup != null) quitGroup.alpha = 0f;

            float startHomeAlpha = (view == "home" && homeGroup != null) ? homeGroup.alpha : 0f;
            float startRealmsAlpha = (view == "realms" && realmsGroup != null) ? realmsGroup.alpha : 0f;
            float startSettingsAlpha = (view == "settings" && settingsGroup != null) ? settingsGroup.alpha : 0f;
            float startControlsAlpha = (view == "controls" && controlsGroup != null) ? controlsGroup.alpha : 0f;
            float startCreditsAlpha = (view == "credits" && creditsGroup != null) ? creditsGroup.alpha : 0f;
            float startQuitAlpha = (view == "quit" && quitGroup != null) ? quitGroup.alpha : 0f;
            float startHeaderAlpha = (view == "home" && homeHeaderGroup != null) ? homeHeaderGroup.alpha : 0f;
            float startVeilAlpha = subpanelVeil != null ? subpanelVeil.color.a : 0f;

            var startBtnAlphas = new float[homeButtons.Count];
            for (int i = 0; i < homeButtons.Count; i++)
            {
                startBtnAlphas[i] = (view == "home" && homeButtons[i].Group != null) ? homeButtons[i].Group.alpha : 0f;
            }

            // Ensure no intermediate loading bar or text box appears over the cinematic transition
            if (loadingContainer != null) loadingContainer.SetActive(false);
            if (hintText != null) hintText.gameObject.SetActive(false);

            AsyncOperation operation = null;
            if (LoadSceneOnPlay)
            {
                operation = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
                if (operation == null)
                {
                    loading = false;
                    if (loadingContainer != null) loadingContainer.SetActive(true);
                    if (loadingText != null) loadingText.text = "UNABLE TO OPEN REALM.";
                    yield break;
                }

                operation.allowSceneActivation = false;
            }

            float elapsed = 0f;
            const float transitionDuration = 1.30f;
            while (elapsed < transitionDuration || (operation != null && operation.progress < 0.9f))
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(Mathf.Min(elapsed / transitionDuration, operation != null ? (operation.progress / 0.9f) : 1f));

                // Fade ONLY the currently active view smoothly away into the dark
                float fadeFactor = Mathf.Clamp01(1f - progress * 2.5f);
                if (homeGroup != null) homeGroup.alpha = startHomeAlpha * fadeFactor;
                if (realmsGroup != null) realmsGroup.alpha = startRealmsAlpha * fadeFactor;
                if (settingsGroup != null) settingsGroup.alpha = startSettingsAlpha * fadeFactor;
                if (controlsGroup != null) controlsGroup.alpha = startControlsAlpha * fadeFactor;
                if (creditsGroup != null) creditsGroup.alpha = startCreditsAlpha * fadeFactor;
                if (quitGroup != null) quitGroup.alpha = startQuitAlpha * fadeFactor;
                if (homeHeaderGroup != null) homeHeaderGroup.alpha = startHeaderAlpha * fadeFactor;
                if (subpanelVeil != null) subpanelVeil.color = new Color(1f, 1f, 1f, startVeilAlpha * fadeFactor);
                for (int i = 0; i < homeButtons.Count; i++)
                {
                    if (homeButtons[i].Group != null)
                    {
                        homeButtons[i].Group.alpha = startBtnAlphas[i] * fadeFactor;
                    }
                }

                // Animate lantern flame extinction & stage darkness
                stage?.SetPlayZoom(progress);

                // Deep obsidian darkness collapses across the entire screen
                if (curtain != null)
                {
                    float darknessAlpha = SmoothStep(Mathf.Clamp01(progress * 1.15f));
                    curtain.color = new Color(0.005f, 0.008f, 0.016f, darknessAlpha);
                }

                yield return null;
            }

            stage?.SetPlayZoom(1f);
            if (curtain != null)
            {
                curtain.color = new Color(0.005f, 0.008f, 0.016f, 1f);
            }
            if (homeGroup != null) homeGroup.alpha = 0f;
            if (realmsGroup != null) realmsGroup.alpha = 0f;
            if (settingsGroup != null) settingsGroup.alpha = 0f;
            if (controlsGroup != null) controlsGroup.alpha = 0f;
            if (creditsGroup != null) creditsGroup.alpha = 0f;
            if (quitGroup != null) quitGroup.alpha = 0f;
            if (homeHeaderGroup != null) homeHeaderGroup.alpha = 0f;
            if (subpanelVeil != null) subpanelVeil.color = new Color(1f, 1f, 1f, 0f);
            if (operation != null) operation.allowSceneActivation = true;
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
            if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(MenuPreferences.Vibration);

            if (masterSlider != null) UpdateSliderPercentLabel(masterSlider);
            if (musicSlider != null) UpdateSliderPercentLabel(musicSlider);
            if (effectsSlider != null) UpdateSliderPercentLabel(effectsSlider);
            UpdateSwitchVisual(fullscreenToggle);
            UpdateSwitchVisual(vsyncToggle);
            UpdateSwitchVisual(motionToggle);
            UpdateSwitchVisual(vibrationToggle);
            RefreshSettingsFrameRate();
            RefreshControllerSettingsUI();
        }

        private void OnAudioSliderChanged(float _)
        {
            if (masterSlider == null || musicSlider == null || effectsSlider == null) return;
            UpdateSliderPercentLabel(masterSlider);
            UpdateSliderPercentLabel(musicSlider);
            UpdateSliderPercentLabel(effectsSlider);
            MenuPreferences.SetAudio(masterSlider.value, musicSlider.value, effectsSlider.value);
            MarkSettingsChanged();
        }

        private void OnFullscreenToggled(bool value)
        {
            MenuPreferences.SetFullscreen(value);
            MarkSettingsChanged();
            UpdateSwitchVisual(fullscreenToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void OnVSyncToggled(bool value)
        {
            MenuPreferences.SetVSync(value);
            MarkSettingsChanged();
            RefreshSettingsFrameRate();
            UpdateSwitchVisual(vsyncToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void OnReducedMotionToggled(bool value)
        {
            MenuPreferences.SetReducedMotion(value);
            MarkSettingsChanged();
            UpdateSwitchVisual(motionToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        public void ResetDefaults()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsUI();
            CancelSettingsReset();
            MarkSettingsChanged();
            stage?.TriggerInteractionSurge(Vector2.zero, Vector2.zero, 1f);
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayUIClick();
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
                txt.text = toggle.isOn ? "<  ON  >" : "<  OFF  >";
                txt.color = toggle.isOn ? amberGold : moonlightSilver;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = toggle.isOn
                    ? new Color(1.0f, 0.72f, 0.25f, 0.22f)
                    : new Color(0.55f, 0.68f, 0.88f, 0.0f);
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

            // 1. Full 2D Orthographic Pixel-Art Parallax Stage
            stage = gameObject.AddComponent<ProceduralArchiveStage>();
            stage.SetArtOverrides(characterSprite, hangingLampSprite, chainLinkSprite);
            stage.BuildStage(rootRect);
            filigreeSprite = stage.CreateOrnateFiligreeSprite(512, 64);
            softRadialSprite = CreateSoftRadialSprite(128);

            // Procedural Full-Screen Depth Blur Vignette, Thin Silver Frame & Capsule Slider Sprites
            vignetteBlurSprite = CreateVignetteBlurSprite(256);
            roundTrackSprite = CreateRoundedCapsuleSprite(24, 10, new Color(0.54f, 0.68f, 0.86f, 0.42f), 5);
            roundFillSprite = CreateRoundedCapsuleSprite(24, 10, amberGold, 5);
            roundHandleSprite = CreateCircularHandleSprite(32);
            thinBorderSprite = CreateThinBorderBoxSprite(32, 32, 5, 8);

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

            // 2. Ethereal Sub-Panel Midnight Veil (Full-Screen Depth Vignette & Blur)
            subpanelVeil = CreateFullStretchImage("Ethereal Subpanel Veil", rootRect, Color.clear);
            subpanelVeil.sprite = vignetteBlurSprite;
            subpanelVeil.color = new Color(1f, 1f, 1f, 0f);

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

            // 5. Delicate Whispered Intro Skip Prompt
            hintText = CreateLabel("Skip Hint", safeFrame, "PRESS [ENTER], [SPACE] OR CLICK TO AWAKEN THE LANTERN", 11f, amberGold, new Vector2(680f, 26f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), pivot: new Vector2(0.5f, 0f));
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.characterSpacing = 3.5f;

            // 6. Cinematic Curtain & Loading Indicator
            curtain = CreateFullStretchImage("Cinematic Curtain", rootRect, new Color(0.012f, 0.018f, 0.038f, 1f));

            var loadRect = CreateAnchoredRect("Loading Container", rootRect, new Vector2(680f, 84f), new Vector2(0.5f, 0.14f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            loadingContainer = loadRect.gameObject;

            loadingText = CreateLabel("Loading Text", loadRect, "ENTERING THE ARCHIVE...", 14.5f, radiantIvory, new Vector2(680f, 34f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), pivot: new Vector2(0.5f, 1f));
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.characterSpacing = 6f;

            var barBg = CreateAnchoredRect("Loading Bar BG", loadRect, new Vector2(420f, 3f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), pivot: new Vector2(0.5f, 0f));
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
        // HOME VIEW: CENTERED HOLLOW-KNIGHT & LANTERN SANCTUARY ARCHITECTURE
        // =====================================================================

        private void BuildFramelessHomeView()
        {
            homeButtons.Clear();

            // Dead-Center Main Menu Column (x = 0)
            var centerColumn = CreateAnchoredRect("Center Menu Column", home, new Vector2(820f, 960f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));

            // Deep midnight-indigo radial sanctuary shadow behind centered menu for crisp contrast
            var centerVignette = CreateAnchoredRect("Center Stage Shadow Vignette", centerColumn, new Vector2(960f, 1020f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var vigImg = centerVignette.gameObject.AddComponent<Image>();
            vigImg.sprite = softRadialSprite;
            vigImg.color = new Color(0.008f, 0.013f, 0.024f, 0.86f);
            vigImg.raycastTarget = false;

            // 1. Monumental Centered Title Header (Silver Crown Arch, Pale White Title, Sacred Lantern Emblem)
            homeHeaderRect = CreateAnchoredRect("Home Header Container", centerColumn, new Vector2(820f, 340f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), pivot: new Vector2(0.5f, 0.5f));
            homeHeaderBasePos = homeHeaderRect.anchoredPosition;
            homeHeaderGroup = homeHeaderRect.gameObject.AddComponent<CanvasGroup>();

            // Warm Lantern Ember & Moonlight Radial Bloom behind Title
            var titleAura = CreateAnchoredRect("Title Soft Lantern Glow", homeHeaderRect, new Vector2(720f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), pivot: new Vector2(0.5f, 0.5f));
            var auraImg = titleAura.gameObject.AddComponent<Image>();
            auraImg.sprite = softRadialSprite;
            auraImg.color = new Color(1.0f, 0.72f, 0.28f, 0.12f);
            auraImg.raycastTarget = false;

            // Top Ornate Filigree Crown Arch (Hollow Knight style)
            if (topFiligreeSprite != null)
            {
                var topArchRect = CreateAnchoredRect("Top Filigree Crown", homeHeaderRect, new Vector2(640f, 72f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f), pivot: new Vector2(0.5f, 0.5f));
                var topArchImg = topArchRect.gameObject.AddComponent<Image>();
                topArchImg.sprite = topFiligreeSprite;
                topArchImg.color = new Color(0.86f, 0.92f, 1.0f, 0.92f);
                topArchImg.raycastTarget = false;
            }

            // Whispered Sanctuary Eyebrow
            var eyebrow = CreateLabel("Sanctuary Eyebrow", homeHeaderRect, "-   SANCTUARY OF THE ETERNAL FLAME   -", 10.5f, moonlightSilver, new Vector2(640f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, 54f), pivot: new Vector2(0.5f, 0.5f));
            eyebrow.alignment = TextAlignmentOptions.Center;
            eyebrow.characterSpacing = 5.5f;

            // Monumental Centered Title: THE LIGHTEN ROAD
            var mainTitle = CreateLabel("Title Lighten Road", homeHeaderRect, "THE LIGHTEN ROAD", 52f, radiantIvory, new Vector2(820f, 68f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), pivot: new Vector2(0.5f, 0.5f));
            mainTitle.alignment = TextAlignmentOptions.Center;
            mainTitle.fontStyle = FontStyles.Bold;
            mainTitle.characterSpacing = 9.5f;

            // Bottom Sacred Crest + Signature Lantern Ember Core ("Hollow Knight + Uniquely Ours")
            if (bottomCrestSprite != null)
            {
                var botCrestRect = CreateAnchoredRect("Bottom Lantern Crest", homeHeaderRect, new Vector2(740f, 102f), new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), pivot: new Vector2(0.5f, 0.5f));
                var botCrestImg = botCrestRect.gameObject.AddComponent<Image>();
                botCrestImg.sprite = bottomCrestSprite;
                botCrestImg.color = new Color(0.88f, 0.93f, 1.0f, 0.94f);
                botCrestImg.raycastTarget = false;
            }

            // Signature Glowing Lantern Ember Core at the Heart of the Crest
            var crestGlow = CreateAnchoredRect("Crest Lantern Halo", homeHeaderRect, new Vector2(96f, 44f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), pivot: new Vector2(0.5f, 0.5f));
            var crestGlowImg = crestGlow.gameObject.AddComponent<Image>();
            crestGlowImg.sprite = softRadialSprite;
            crestGlowImg.color = new Color(1.0f, 0.72f, 0.24f, 0.48f);
            crestGlowImg.raycastTarget = false;

            var crestSpark = CreateAnchoredRect("Crest Lantern Spark", homeHeaderRect, new Vector2(14f, 14f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), pivot: new Vector2(0.5f, 0.5f));
            crestSpark.localEulerAngles = new Vector3(0f, 0f, 45f);
            var crestSparkImg = crestSpark.gameObject.AddComponent<Image>();
            crestSparkImg.color = amberGold;
            crestSparkImg.raycastTarget = false;

            // Centered Active Realm Badge
            activeRealmBadgeText = CreateLabel("Active Realm Text", homeHeaderRect, "--   REALM I  :  THE FIRST LIGHT   --", 11.5f, amberGold, new Vector2(680f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, -134f), pivot: new Vector2(0.5f, 0.5f));
            activeRealmBadgeText.alignment = TextAlignmentOptions.Center;
            activeRealmBadgeText.characterSpacing = 4.5f;

            // 2. Centered Frameless Vertical Menu Stack (No Rectangular Boxes — Pure Ornate Pointers & Ember Vein Underlines)
            playButton = CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Enter the Archive",
                labelText: "START GAME",
                yPos: -18f,
                cameraPan: new Vector2(0f, 10f),
                onClick: Play,
                isPrimary: true,
                customWingOffset: 148f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Realms & Chapters",
                labelText: "REALMS & CHAPTERS",
                yPos: -74f,
                cameraPan: new Vector2(-14f, 6f),
                onClick: ShowRealms,
                isPrimary: false,
                customWingOffset: 185f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Settings",
                labelText: "OPTIONS",
                yPos: -130f,
                cameraPan: new Vector2(14f, 4f),
                onClick: ShowSettings,
                isPrimary: false,
                customWingOffset: 118f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Controls & Guide",
                labelText: "CONTROLS",
                yPos: -186f,
                cameraPan: new Vector2(-12f, -6f),
                onClick: ShowControls,
                isPrimary: false,
                customWingOffset: 124f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Credits",
                labelText: "CREDITS",
                yPos: -242f,
                cameraPan: new Vector2(12f, -8f),
                onClick: ShowCredits,
                isPrimary: false,
                customWingOffset: 115f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Leave the Light",
                labelText: "QUIT GAME",
                yPos: -298f,
                cameraPan: new Vector2(0f, -16f),
                onClick: ShowQuit,
                isPrimary: false,
                customWingOffset: 132f);
        }

        private Button CreateFramelessMenuButton(
            RectTransform parent,
            string gameObjectName,
            string labelText,
            float yPos,
            Vector2 cameraPan,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary = false,
            float customWingOffset = 160f)
        {
            var itemRect = CreateAnchoredRect(gameObjectName, parent, new Vector2(520f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));

            var btnGroup = itemRect.gameObject.AddComponent<CanvasGroup>();
            btnGroup.alpha = 0f;
            btnGroup.interactable = false;
            btnGroup.blocksRaycasts = false;

            // Transparent hit graphic (Frameless Hollow-Knight style — NO rectangular box!)
            var hitGraphic = itemRect.gameObject.AddComponent<Image>();
            hitGraphic.color = Color.clear;

            // Soft radial lantern ember aura behind text when focused/hovered
            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(360f, 44f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            // Center-Expanding Ember-Vein Filigree Underline
            var lineRect = CreateAnchoredRect("Filigree Underline", itemRect, new Vector2(0f, 2.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), pivot: new Vector2(0.5f, 0.5f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            // Centered Typography (Pale Moon-Ivory at rest -> Warm Lantern Ember on focus)
            var titleTxt = CreateLabel("Title", itemRect, labelText.ToUpperInvariant(), isPrimary ? 21.5f : 18.5f, radiantIvory, new Vector2(440f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 6.0f : 4.5f;

            // Symmetrical Hollow-Knight Pointers + Signature Lantern Ember Spark Tips
            var wingContainer = CreateAnchoredRect("Wings Container", itemRect, new Vector2(520f, 32f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = 0f;

            RectTransform leftWingRect = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(46f, 22f), new Vector2(0.5f, 0.5f), new Vector2(-customWingOffset, 0f), pivot: new Vector2(0.5f, 0.5f));
            RectTransform rightWingRect = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(46f, 22f), new Vector2(0.5f, 0.5f), new Vector2(customWingOffset, 0f), pivot: new Vector2(0.5f, 0.5f));
            rightWingRect.localScale = new Vector3(-1f, 1f, 1f);

            if (buttonPointerSprite != null)
            {
                var leftImg = leftWingRect.gameObject.AddComponent<Image>();
                leftImg.sprite = buttonPointerSprite;
                leftImg.color = radiantIvory;
                leftImg.raycastTarget = false;

                var rightImg = rightWingRect.gameObject.AddComponent<Image>();
                rightImg.sprite = buttonPointerSprite;
                rightImg.color = radiantIvory;
                rightImg.raycastTarget = false;
            }

            // Signature Lantern Ember Diamond Spark on the inner tip of each pointer
            var leftSpark = CreateAnchoredRect("Left Ember Spark", leftWingRect, new Vector2(6.5f, 6.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), pivot: new Vector2(0.5f, 0.5f));
            leftSpark.localEulerAngles = new Vector3(0f, 0f, 45f);
            var leftSparkImg = leftSpark.gameObject.AddComponent<Image>();
            leftSparkImg.color = amberGold;
            leftSparkImg.raycastTarget = false;

            var rightSpark = CreateAnchoredRect("Right Ember Spark", rightWingRect, new Vector2(6.5f, 6.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), pivot: new Vector2(0.5f, 0.5f));
            rightSpark.localEulerAngles = new Vector3(0f, 0f, 45f);
            var rightSparkImg = rightSpark.gameObject.AddComponent<Image>();
            rightSparkImg.color = amberGold;
            rightSparkImg.raycastTarget = false;

            var btn = itemRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hitGraphic;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);

            var motion = itemRect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = titleTxt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.LeftWing = leftWingRect;
            motion.RightWing = rightWingRect;
            motion.WingGroup = wingGroup;
            motion.BaseWingOffset = customWingOffset;
            motion.IsPrimaryAction = isPrimary;
            motion.LeftAlignedItem = false;
            motion.InitializeBasePositions(Vector2.zero, Vector2.zero, Vector2.zero);
            motion.StageFocusCanvasPosition = new Vector2(0f, yPos);
            motion.StageCameraPanOffset = cameraPan;
            motion.OnFocusedCallback = OnMenuButtonFocused;

            homeButtons.Add(new HomeButtonItem
            {
                Rect = itemRect,
                Group = btnGroup,
                Motion = motion,
                BasePos = new Vector2(0f, yPos)
            });

            return btn;
        }

        private void OnMenuButtonFocused(MenuButtonMotion motion)
        {
            if (stage != null && motion != null)
            {
                stage.TriggerInteractionSurge(motion.StageFocusCanvasPosition, motion.StageCameraPanOffset, 1.0f);
            }
        }

        // =====================================================================
        // SUB-VIEW 1: REALMS & CHAPTERS (CENTERED FRAMELESS SANCTUARY STACK)
        // =====================================================================

        private void BuildFramelessRealmsView()
        {
            var surface = CreateEtherealSubViewFrame(realms, "REALMS & CHAPTERS", "SELECT REALM");

            realmButtons.Clear();
            realmTitles.Clear();
            realmWingGroups.Clear();
            realmRowAuras.Clear();
            realmRowUnderlines.Clear();
            realmStatusLabels.Clear();

            // Centered Vertical Stack of the 3 Realms (x = 0)
            for (int i = 0; i < AvailableRealms.Length; i++)
            {
                int capturedIndex = i;
                var r = AvailableRealms[i];
                float yPos = 132f - i * 94f;

                var row = CreateAnchoredRect($"Realm Option {i}", surface, new Vector2(720f, 82f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = Color.clear;

                // Soft radial lantern ember aura behind selected realm
                var auraRect = CreateAnchoredRect("Row Aura", row, new Vector2(620f, 76f), new Vector2(0.5f, 0.5f), Vector2.zero);
                var auraImg = auraRect.gameObject.AddComponent<Image>();
                auraImg.sprite = softRadialSprite;
                auraImg.color = (i == selectedRealmIndex) ? new Color(1.0f, 0.72f, 0.28f, 0.22f) : Color.clear;
                auraImg.raycastTarget = false;
                realmRowAuras.Add(auraImg);

                // Tapered Bottom Filigree Divider Line
                var lineRect = CreateAnchoredRect("Row Filigree Underline", row, new Vector2(560f, 2.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f));
                var lineImg = lineRect.gameObject.AddComponent<Image>();
                lineImg.sprite = filigreeSprite;
                lineImg.color = (i == selectedRealmIndex) ? new Color(1.0f, 0.76f, 0.32f, 0.92f) : new Color(0.58f, 0.72f, 0.90f, 0.25f);
                lineImg.raycastTarget = false;
                realmRowUnderlines.Add(lineImg);

                // Symmetrical Wing Pointers + Lantern Sparks on Selected Realm
                var wingContainer = CreateAnchoredRect("Wings Container", row, new Vector2(680f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f));
                var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
                wingGroup.alpha = (i == selectedRealmIndex) ? 1f : 0f;
                realmWingGroups.Add(wingGroup);

                if (buttonPointerSprite != null)
                {
                    var leftWing = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(42f, 20f), new Vector2(0.5f, 0.5f), new Vector2(-240f, 0f));
                    var leftImg = leftWing.gameObject.AddComponent<Image>();
                    leftImg.sprite = buttonPointerSprite;
                    leftImg.color = amberGold;
                    leftImg.raycastTarget = false;

                    var rightWing = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(42f, 20f), new Vector2(0.5f, 0.5f), new Vector2(240f, 0f));
                    rightWing.localScale = new Vector3(-1f, 1f, 1f);
                    var rightImg = rightWing.gameObject.AddComponent<Image>();
                    rightImg.sprite = buttonPointerSprite;
                    rightImg.color = amberGold;
                    rightImg.raycastTarget = false;
                }

                // Centered Realm Number & Title
                var titleTxt = CreateLabel("Realm Title", row, $"{r.Number}   —   {r.Title}", 20f, (i == selectedRealmIndex) ? FocusedAmberGold : radiantIvory, new Vector2(640f, 28f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
                titleTxt.rectTransform.pivot = Vector2.one * 0.5f;
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.characterSpacing = 3.5f;
                realmTitles.Add(titleTxt);

                // Centered Status & Tag Whisper (Preserves exact test contracts)
                var statusTxt = CreateLabel("Status", row, (i == selectedRealmIndex) ? "[ ACTIVE REALM ]" : "SELECT REALM", 11.5f, amberGold, new Vector2(520f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f));
                statusTxt.rectTransform.pivot = Vector2.one * 0.5f;
                statusTxt.alignment = TextAlignmentOptions.Center;
                statusTxt.fontStyle = FontStyles.Bold;
                statusTxt.characterSpacing = 2.8f;
                realmStatusLabels.Add(statusTxt);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() => SelectRealm(capturedIndex));
                realmButtons.Add(btn);
                if (i == 0) realmsFirst = btn;
            }

            // Centered Selected Realm Lore Whisper (Frameless between two delicate filigree lines)
            var showcaseBox = CreateAnchoredRect("Showcase Container", surface, new Vector2(780f, 86f), new Vector2(0.5f, 0.5f), new Vector2(0f, -168f));

            activeRealmShowcaseTag = CreateLabel("Showcase Tag", showcaseBox, "- TUTORIAL & BEGINNINGS -", 11f, amberGold, new Vector2(740f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f));
            activeRealmShowcaseTag.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseTag.alignment = TextAlignmentOptions.Center;
            activeRealmShowcaseTag.characterSpacing = 4.5f;

            activeRealmShowcaseDesc = CreateLabel("Showcase Desc", showcaseBox, "\"Learn basic movement mechanics, explore the ruined archive, and power the conduit to advance.\"", 13f, radiantIvory, new Vector2(760f, 26f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));
            activeRealmShowcaseDesc.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseDesc.alignment = TextAlignmentOptions.Center;

            activeRealmShowcaseStatus = CreateLabel("Showcase Status", showcaseBox, "[ ACTIVE REALM ]", 11f, moonlightSilver, new Vector2(500f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f));
            activeRealmShowcaseStatus.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseStatus.alignment = TextAlignmentOptions.Center;
            activeRealmShowcaseStatus.characterSpacing = 3.0f;

            // Centered Frameless Action Buttons
            CreateEtherealActionButton(surface, "Enter Selected Realm  >", new Vector2(180f, -268f), Play, isPrimary: true, width: 310f);
            CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(-180f, -268f), ShowHome, isPrimary: false, width: 310f);
        }

        // =====================================================================
        // SUB-VIEW 2: SETTINGS (CENTERED FRAMELESS HOLLOW-KNIGHT ROWS)
        // =====================================================================

        private void BuildFramelessSettingsView() => BuildSettingsWorkspace();

        private Slider CreateEtherealSlider(RectTransform parent, string title, string subtitle, float yPos, out TMP_Text valueText)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(680f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));
            var rowHit = row.gameObject.AddComponent<Image>();
            rowHit.color = Color.clear;

            // Tapered Bottom Filigree Divider (Frameless Hollow-Knight style)
            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(680f, 2f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.24f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 16.5f, radiantIvory, new Vector2(340f, 24f), new Vector2(0f, 0.5f), new Vector2(8f, 11f), pivot: new Vector2(0f, 0.5f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 12f, moonlightSilver, new Vector2(350f, 22f), new Vector2(0f, 0.5f), new Vector2(8f, -12f), pivot: new Vector2(0f, 0.5f));

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderGo.GetComponent<Image>().color = Color.clear;
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(310f, 40f);
            sRect.anchoredPosition = new Vector2(-8f, 0f);

            var track = CreateAnchoredRect("Track", sRect, new Vector2(225f, 6f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), pivot: new Vector2(0f, 0.5f));
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = roundTrackSprite;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = Color.white;

            var fillArea = CreateStretchRect("Fill Area", track);
            var fill = CreateStretchRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = roundFillSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Color.white;

            var handleSlide = CreateStretchRect("Handle Slide Area", track);
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(18f, 18f), new Vector2(0f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            handle.localEulerAngles = Vector3.zero;

            var handleGlow = CreateAnchoredRect("Handle Glow", handle, new Vector2(30f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var hGlowImg = handleGlow.gameObject.AddComponent<Image>();
            hGlowImg.sprite = softRadialSprite;
            hGlowImg.color = new Color(1.0f, 0.75f, 0.30f, 0.45f);
            hGlowImg.raycastTarget = false;

            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = roundHandleSprite;
            handleImg.color = Color.white;

            valueText = CreateLabel("Value", sRect, "85%", 15f, amberGold, new Vector2(68f, 24f), new Vector2(1f, 0.5f), Vector2.zero, pivot: new Vector2(1f, 0.5f));
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.fontStyle = FontStyles.Bold;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            AddSettingsFocus(row, slider, titleTxt);
            slider.transition = Selectable.Transition.ColorTint;
            var colors = slider.colors;
            colors.normalColor = radiantIvory;
            colors.highlightedColor = amberGold;
            colors.selectedColor = amberGold;
            slider.colors = colors;
            return slider;
        }

        private Toggle CreateEtherealToggle(RectTransform parent, string title, string subtitle, float yPos, out TMP_Text valText, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(680f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            // Tapered Bottom Filigree Divider
            var divRect = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(680f, 2f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var divImg = divRect.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.24f);
            divImg.raycastTarget = false;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 16.5f, radiantIvory, new Vector2(440f, 24f), new Vector2(0f, 0.5f), new Vector2(8f, 11f), pivot: new Vector2(0f, 0.5f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 12f, moonlightSilver, new Vector2(440f, 22f), new Vector2(0f, 0.5f), new Vector2(8f, -12f), pivot: new Vector2(0f, 0.5f));

            var pill = CreateAnchoredRect("Pill", row, new Vector2(130f, 36f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), pivot: new Vector2(1f, 0.5f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.sprite = softRadialSprite;
            pillImg.color = Color.clear;

            valText = CreateLabel("Value", pill, "<  OFF  >", 15f, moonlightSilver, new Vector2(130f, 36f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            valText.alignment = TextAlignmentOptions.Center;
            valText.fontStyle = FontStyles.Bold;
            valText.characterSpacing = 2.5f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            toggle.graphic = null;
            toggle.onValueChanged.AddListener(onChanged);
            AddSettingsFocus(row, toggle, titleTxt);
            return toggle;
        }

        // =====================================================================
        // SUB-VIEW 3: CONTROLS & GUIDE (CENTERED HOLLOW-KNIGHT KEYBINDING ROWS)
        // =====================================================================

        private readonly TMP_Text[] controlsRowKeyLabels = new TMP_Text[6];
        private readonly Image[] controlsRowIcons = new Image[6];
        private readonly Button[] controlsDeviceTabs = new Button[3];
        private readonly TMP_Text[] controlsDeviceTabLabels = new TMP_Text[3];
        private int selectedControlsDeviceTab = 0;

        private void BuildFramelessControlsView()
        {
            var surface = CreateEtherealSubViewFrame(controls, "CONTROLS", "KEYBOARD, XBOX & PLAYSTATION");

            // 3-way Device Switcher at top
            var tabRow = CreateAnchoredRect("Controls Device Tabs", surface, new Vector2(680f, 40f), Vector2.one * 0.5f, new Vector2(0f, 185f));
            string[] deviceTabNames = { "KEYBOARD", "XBOX PAD", "PLAYSTATION" };
            float[] tabWidths = { 190f, 160f, 190f };
            float[] tabX = { -190f, 0f, 190f };

            for (int i = 0; i < 3; i++)
            {
                int tabIdx = i;
                controlsDeviceTabs[i] = SettingsButton(tabRow, deviceTabNames[i], new Vector2(tabX[i], 0f), new Vector2(tabWidths[i], 38f), () => SetControlsDeviceTab(tabIdx));
                controlsDeviceTabLabels[i] = controlsDeviceTabs[i].GetComponentInChildren<TMP_Text>();
                controlsDeviceTabLabels[i].fontSize = 13.5f;
            }

            controlsRowKeyLabels[0] = CreateCenteredControlRow(surface, "[ A / D  or  ARROWS ]", "MOVE", "Walk left and right across the archive", 135f, 0);
            controlsRowKeyLabels[1] = CreateCenteredControlRow(surface, "[ SPACE ]", "JUMP", "Variable height jump and ledge climb", 84f, 1);
            controlsRowKeyLabels[2] = CreateCenteredControlRow(surface, "[ LEFT SHIFT ]", "SPRINT", "Run faster across hazards (uses Stamina)", 33f, 2);
            controlsRowKeyLabels[3] = CreateCenteredControlRow(surface, "[ F / L-CLICK ]", "LANTERN FLARE", "Light pulse to reveal hidden runes", -18f, 3);
            controlsRowKeyLabels[4] = CreateCenteredControlRow(surface, "[ E ]", "INTERACT", "Channel embers into ancient conduits", -69f, 4);
            controlsRowKeyLabels[5] = CreateCenteredControlRow(surface, "[ ESC ]", "PAUSE MENU", "Open tactical pause, options & guide", -120f, 5);

            controlsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -268f), ShowHome, isPrimary: true, width: 310f);

            var initialStyle = ControllerGlyphService.ActiveStyle;
            int defaultTab = initialStyle == ControllerPromptStyle.PlayStation ? 2 : (initialStyle == ControllerPromptStyle.Xbox ? 1 : 0);
            SetControlsDeviceTab(defaultTab);
        }

        public void SetControlsDeviceTab(int tab)
        {
            selectedControlsDeviceTab = Mathf.Clamp(tab, 0, 2);
            for (int i = 0; i < 3; i++)
            {
                bool active = (i == selectedControlsDeviceTab);
                if (controlsDeviceTabLabels[i] != null) controlsDeviceTabLabels[i].color = active ? amberGold : radiantIvory;
                var marker = controlsDeviceTabs[i] != null ? controlsDeviceTabs[i].transform.Find("Selection rule") : null;
                if (marker != null) marker.gameObject.SetActive(active);
            }

            if (controlsRowKeyLabels[0] == null) return;

            if (selectedControlsDeviceTab == 0) // Keyboard
            {
                for (int i = 0; i < controlsRowIcons.Length; i++)
                    if (controlsRowIcons[i] != null) controlsRowIcons[i].color = Color.clear;

                controlsRowKeyLabels[0].text = "[ A / D  or  ARROWS ]";
                controlsRowKeyLabels[1].text = "[ SPACE ]";
                controlsRowKeyLabels[2].text = "[ LEFT SHIFT ]";
                controlsRowKeyLabels[3].text = "[ F / L-CLICK ]";
                controlsRowKeyLabels[4].text = "[ E ]";
                controlsRowKeyLabels[5].text = "[ ESC ]";
            }
            else if (selectedControlsDeviceTab == 1) // Xbox
            {
                string[] xbIcons = { "LeftStick", "A", "RT", "X", "Y", "Menu" };
                for (int i = 0; i < controlsRowIcons.Length; i++)
                {
                    if (controlsRowIcons[i] != null)
                    {
                        controlsRowIcons[i].sprite = ControllerGlyphService.GetGlyphSprite(xbIcons[i], ControllerPromptStyle.Xbox);
                        controlsRowIcons[i].color = Color.white;
                    }
                }

                controlsRowKeyLabels[0].text = "[ L-STICK / D-PAD ]";
                controlsRowKeyLabels[1].text = "[ A ]";
                controlsRowKeyLabels[2].text = "[ RT / LS ]";
                controlsRowKeyLabels[3].text = "[ RT / X ]";
                controlsRowKeyLabels[4].text = "[ Y / B ]";
                controlsRowKeyLabels[5].text = "[ MENU ]";
            }
            else // PlayStation
            {
                string[] psIcons = { "LeftStick", "Cross", "R2", "Square", "Triangle", "Options" };
                for (int i = 0; i < controlsRowIcons.Length; i++)
                {
                    if (controlsRowIcons[i] != null)
                    {
                        controlsRowIcons[i].sprite = ControllerGlyphService.GetGlyphSprite(psIcons[i], ControllerPromptStyle.PlayStation);
                        controlsRowIcons[i].color = Color.white;
                    }
                }

                controlsRowKeyLabels[0].text = "[ L-STICK / D-PAD ]";
                controlsRowKeyLabels[1].text = "[ CROSS ]";
                controlsRowKeyLabels[2].text = "[ R2 / L3 ]";
                controlsRowKeyLabels[3].text = "[ R2 / SQUARE ]";
                controlsRowKeyLabels[4].text = "[ TRIANGLE / CIRCLE ]";
                controlsRowKeyLabels[5].text = "[ OPTIONS ]";
            }
        }

        private TMP_Text CreateCenteredControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos, int rowIndex = -1)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(680f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));

            // Left of center: Action Title & Whispered Mechanic Description
            var actTxt = CreateLabel("Action", row, actionTitle, 15.5f, radiantIvory, new Vector2(380f, 22f), new Vector2(0f, 0.5f), new Vector2(12f, 8f), pivot: new Vector2(0f, 0.5f));
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 3.0f;

            var descTxt = CreateLabel("Description", row, desc, 11.5f, moonlightSilver, new Vector2(420f, 20f), new Vector2(0f, 0.5f), new Vector2(12f, -10f), pivot: new Vector2(0f, 0.5f));

            // Right of center: Ornate Keybinding Emblem with Warm Lantern Glow
            var keyGlow = CreateAnchoredRect("Key Lantern Aura", row, new Vector2(210f, 34f), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), pivot: new Vector2(0.5f, 0.5f));
            var keyGlowImg = keyGlow.gameObject.AddComponent<Image>();
            keyGlowImg.sprite = softRadialSprite;
            keyGlowImg.color = new Color(1.0f, 0.72f, 0.26f, 0.16f);
            keyGlowImg.raycastTarget = false;

            var iconRect = CreateAnchoredRect("Glyph Icon", row, new Vector2(24f, 24f), new Vector2(1f, 0.5f), new Vector2(-195f, 0f), pivot: new Vector2(0.5f, 0.5f));
            var iconImg = iconRect.gameObject.AddComponent<Image>();
            iconImg.color = Color.clear;
            iconImg.raycastTarget = false;
            if (rowIndex >= 0 && rowIndex < controlsRowIcons.Length)
                controlsRowIcons[rowIndex] = iconImg;

            var keyTxt = CreateLabel("Key Text", row, keys, 14f, amberGold, new Vector2(175f, 32f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), pivot: new Vector2(1f, 0.5f));
            keyTxt.alignment = TextAlignmentOptions.Right;
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 2.5f;

            // Tapered Filigree Divider under row
            var div = CreateAnchoredRect("Row Filigree Rule", row, new Vector2(680f, 2f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var divImg = div.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.62f, 0.76f, 0.94f, 0.22f);
            divImg.raycastTarget = false;

            return keyTxt;
        }

        // =====================================================================
        // SUB-VIEW 4: CREDITS (CENTERED MONUMENTAL SANCTUARY ROLL)
        // =====================================================================

        private void BuildFramelessCreditsView()
        {
            var surface = CreateEtherealSubViewFrame(credits, "CREDITS", "THE LIGHTEN ROAD TEAM");

            CreateCenteredCreditEntry(
                surface,
                "SİRAÇ GÖKTUĞ ŞİMŞEK",
                "GAME DESIGN, MOVEMENT, LANTERN & UI SYSTEMS",
                "Player motor, lantern mechanics, audio architecture & user interface",
                148f);

            CreateCenteredCreditEntry(
                surface,
                "KEMAL",
                "LEVEL DESIGN, FLOODED MECHANISMS & PUZZLES",
                "Ember vein conduits, electrified water hazards & puzzle chambers",
                52f);

            CreateCenteredCreditEntry(
                surface,
                "FURKAN",
                "TUTORIAL CHAMBER DESIGN & MECHANICS PROTOTYPE",
                "Opening sanctuary layout, rune crossings & player onboarding flow",
                -44f);

            CreateCenteredCreditEntry(
                surface,
                "AUDIO & ART ASSETS",
                "SOUND DESIGN, MUSIC & SPECIAL THANKS",
                "Atmospheric score, foley sound effects, pixel art & visual resources",
                -140f);

            creditsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -268f), ShowHome, isPrimary: true, width: 310f);
        }

        private void CreateCenteredCreditEntry(RectTransform parent, string name, string role, string details, float yPos)
        {
            var entry = CreateAnchoredRect(name, parent, new Vector2(760f, 84f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var aura = CreateAnchoredRect("Credit Lantern Glow", entry, new Vector2(480f, 64f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var auraImg = aura.gameObject.AddComponent<Image>();
            auraImg.sprite = softRadialSprite;
            auraImg.color = new Color(1.0f, 0.72f, 0.28f, 0.08f);
            auraImg.raycastTarget = false;

            var nameTxt = CreateLabel("Name", entry, name, 20f, radiantIvory, new Vector2(720f, 26f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f));
            nameTxt.rectTransform.pivot = Vector2.one * 0.5f;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.characterSpacing = 4.5f;

            var roleTxt = CreateLabel("Role", entry, role, 11f, amberGold, new Vector2(720f, 18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));
            roleTxt.rectTransform.pivot = Vector2.one * 0.5f;
            roleTxt.alignment = TextAlignmentOptions.Center;
            roleTxt.fontStyle = FontStyles.Bold;
            roleTxt.characterSpacing = 3.0f;

            var detTxt = CreateLabel("Details", entry, details, 12f, moonlightSilver, new Vector2(720f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f));
            detTxt.rectTransform.pivot = Vector2.one * 0.5f;
            detTxt.alignment = TextAlignmentOptions.Center;

            var div = CreateAnchoredRect("Divider", entry, new Vector2(440f, 2f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f));
            var divImg = div.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(0.66f, 0.78f, 0.94f, 0.30f);
            divImg.raycastTarget = false;
        }

        // =====================================================================
        // SUB-VIEW 5: QUIT CONFIRMATION (CENTERED FRAMELESS SANCTUARY PROMPT)
        // =====================================================================

        private void BuildFramelessQuitView()
        {
            var surface = CreateAnchoredRect("Quit Ethereal Frame", quit, new Vector2(760f, 420f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var surfHit = surface.gameObject.AddComponent<Image>();
            surfHit.color = Color.clear;

            if (topFiligreeSprite != null)
            {
                var topCrest = CreateAnchoredRect("Quit Top Filigree", surface, new Vector2(460f, 52f), new Vector2(0.5f, 0.5f), new Vector2(0f, 148f));
                var topImg = topCrest.gameObject.AddComponent<Image>();
                topImg.sprite = topFiligreeSprite;
                topImg.color = new Color(0.86f, 0.92f, 1.0f, 0.90f);
                topImg.raycastTarget = false;
            }

            var tag = CreateLabel("Quit Tag", surface, "-   EXIT GAME   -", 11.5f, moonlightSilver, new Vector2(620f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 102f));
            tag.rectTransform.pivot = Vector2.one * 0.5f;
            tag.alignment = TextAlignmentOptions.Center;
            tag.characterSpacing = 6f;

            var title = CreateLabel("Quit Title", surface, "ARE YOU SURE?", 36f, radiantIvory, new Vector2(680f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0f, 55f));
            title.rectTransform.pivot = Vector2.one * 0.5f;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6.5f;

            var botFiligree = CreateAnchoredRect("Bottom Filigree", surface, new Vector2(480f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f));
            var botImg = botFiligree.gameObject.AddComponent<Image>();
            botImg.sprite = bottomCrestSprite != null ? bottomCrestSprite : filigreeSprite;
            botImg.color = new Color(0.86f, 0.92f, 1.0f, 0.85f);
            botImg.raycastTarget = false;

            var note = CreateLabel("Quit Note", surface, "Any unsaved progress will be lost.\nYour settings will remain saved.", 14f, moonlightSilver, new Vector2(620f, 54f), new Vector2(0.5f, 0.5f), new Vector2(0f, -45f));
            note.rectTransform.pivot = Vector2.one * 0.5f;
            note.alignment = TextAlignmentOptions.Center;

            quitFirst = CreateEtherealActionButton(surface, "Cancel", new Vector2(-165f, -135f), ShowHome, isPrimary: true, width: 260f);
            CreateEtherealActionButton(surface, "Quit", new Vector2(165f, -135f), ConfirmQuit, isPrimary: false, width: 260f);
        }

        // =====================================================================
        // SHARED CENTERED HOLLOW-KNIGHT & LANTERN EMBER HELPERS
        // =====================================================================

        private RectTransform CreateEtherealSubViewFrame(RectTransform viewRoot, string heading, string subheading)
        {
            var container = CreateAnchoredRect("Ethereal Subview Container", viewRoot, new Vector2(1040f, 800f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var containerHit = container.gameObject.AddComponent<Image>();
            containerHit.color = Color.clear;

            // Top Ornate Filigree Crown above Subview Title
            if (topFiligreeSprite != null)
            {
                var topCrest = CreateAnchoredRect("Header Top Filigree", container, new Vector2(480f, 52f), new Vector2(0.5f, 0.5f), new Vector2(0f, 362f));
                var topImg = topCrest.gameObject.AddComponent<Image>();
                topImg.sprite = topFiligreeSprite;
                topImg.color = new Color(0.86f, 0.92f, 1.0f, 0.90f);
                topImg.raycastTarget = false;
            }

            var sub = CreateLabel("Subheading", container, $"-   {subheading}   -", 11f, moonlightSilver, new Vector2(860f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 322f));
            sub.rectTransform.pivot = Vector2.one * 0.5f;
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 5.5f;

            var head = CreateLabel("Heading", container, heading, 36f, radiantIvory, new Vector2(900f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0f, 282f));
            head.rectTransform.pivot = Vector2.one * 0.5f;
            head.alignment = TextAlignmentOptions.Center;
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 7.0f;

            var botCrest = CreateAnchoredRect("Header Bottom Filigree", container, new Vector2(540f, 34f), new Vector2(0.5f, 0.5f), new Vector2(0f, 238f));
            var botImg = botCrest.gameObject.AddComponent<Image>();
            botImg.sprite = bottomCrestSprite != null ? bottomCrestSprite : filigreeSprite;
            botImg.color = new Color(0.86f, 0.92f, 1.0f, 0.88f);
            botImg.raycastTarget = false;

            // Signature Lantern Ember Core at the center of the subview crest
            var spark = CreateAnchoredRect("Header Lantern Spark", container, new Vector2(10f, 10f), new Vector2(0.5f, 0.5f), new Vector2(0f, 244f));
            spark.localEulerAngles = new Vector3(0f, 0f, 45f);
            var sparkImg = spark.gameObject.AddComponent<Image>();
            sparkImg.color = amberGold;
            sparkImg.raycastTarget = false;

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
            var rect = CreateAnchoredRect(label, parent, new Vector2(width, 46f), new Vector2(0.5f, 0.5f), anchoredPos);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width, 42f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(0f, 2.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = Color.clear;
            lineImg.raycastTarget = false;

            var txt = CreateLabel("Caption", rect, label.ToUpperInvariant(), 14.5f, radiantIvory, new Vector2(width, 36f), new Vector2(0.5f, 0.5f), Vector2.zero);
            txt.rectTransform.pivot = Vector2.one * 0.5f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3.8f;

            // Symmetrical Wing Pointers for Action Buttons
            float wingOffset = Mathf.Max(95f, label.Length * 5.2f + 28f);
            var wingContainer = CreateAnchoredRect("Wings Container", rect, new Vector2(width + 60f, 28f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = 0f;

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
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
                onClick();
            });

            var motion = rect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = txt;
            motion.SoftGlow = glowImg;
            motion.UnderlineFiligree = lineImg;
            motion.LeftWing = leftWing;
            motion.RightWing = rightWing;
            motion.WingGroup = wingGroup;
            motion.BaseWingOffset = wingOffset;
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

        private static RectTransform CreateAnchoredRect(
            string name,
            RectTransform parent,
            Vector2 size,
            Vector2 anchor,
            Vector2 pos,
            Vector2? pivot = null)
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

        private static Image CreateFullStretchImage(string name, RectTransform parent, Color color)
        {
            var rect = CreateStretchRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text CreateLabel(
            string name,
            RectTransform parent,
            string content,
            float fontSize,
            Color color,
            Vector2 size,
            Vector2 anchor,
            Vector2 pos,
            Vector2? pivot = null)
        {
            var rect = CreateAnchoredRect(name, parent, size, anchor, pos, pivot);
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

                    Color center = new Color(0.015f, 0.024f, 0.045f, 0.65f);
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

        private Sprite CreateThinBorderBoxSprite(int width, int height, int cornerRadius, int sliceMargin)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = cornerRadius;
            float minX = r;
            float maxX = (width - 1f) - r;
            float minY = r;
            float maxY = (height - 1f) - r;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = Mathf.Clamp(x, minX, maxX);
                    float cy = Mathf.Clamp(y, minY, maxY);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                    {
                        float dEdge = Mathf.Min(Mathf.Min(x, width - 1f - x), Mathf.Min(y, height - 1f - y));
                        float borderAlpha = Mathf.Clamp01(1.8f - dEdge);
                        float fillAlpha = Mathf.Max(0.06f, borderAlpha);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, fillAlpha));
                    }
                    else
                    {
                        float outerCov = Mathf.Clamp01(r + 0.5f - dist);
                        float innerDist = Mathf.Abs((r - 1.2f) - dist);
                        float borderCov = Mathf.Clamp01(1.4f - innerDist);
                        float alpha = outerCov * Mathf.Max(0.06f, borderCov);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
            }

            tex.Apply();
            Vector4 border = new Vector4(sliceMargin, sliceMargin, sliceMargin, sliceMargin);
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, border);
            proceduralSprites.Add(sprite);
            return sprite;
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
