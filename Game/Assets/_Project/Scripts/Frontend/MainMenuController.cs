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
                Tag = "KEMAL SANDBOX  -  PUZZLES & VEINS",
                Description = "Channel lantern energy into ancient Ember Veins, push rolling archive shelves, and navigate past electrified waters and lifts.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity"
            },
            new RealmOption
            {
                Number = "REALM III",
                Title = "ARCHIVE TEST CHAMBER",
                Tag = "PROTOTYPE  -  CALIBRATION ROOM",
                Description = "Focused testing ground for lantern light radii, movement physics, coyote time, and environmental hazards.",
                ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity"
            }
        };

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private Sprite hangingLampSprite;
        [SerializeField] private Sprite chainLinkSprite;

        // Dark-Fantasy Obsidian & Lantern Ember Palette
        private readonly Color radiantIvory = new Color32(246, 248, 252, 255);
        private readonly Color moonlightSilver = new Color32(158, 168, 184, 215);
        private readonly Color mutedIndigo = new Color32(104, 116, 136, 170);
        private readonly Color amberGold = new Color32(255, 175, 64, 255);
        private readonly Color deepAmber = new Color32(212, 118, 32, 255);
        private static readonly Color FocusedAmberGold = new Color32(255, 204, 96, 255);

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
        public TMP_Text ActiveRealmBadgeText => activeRealmBadgeText;

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
                        b.Group.interactable = !loading && !quitting;
                        b.Group.blocksRaycasts = !loading && !quitting;
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
                        bool canInteract = btnE >= 0.98f && !loading && !quitting;
                        b.Group.interactable = canInteract;
                        b.Group.blocksRaycasts = canInteract;
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
                    realmRowAuras[i].color = isSelected ? new Color(1.0f, 0.72f, 0.28f, 0.22f) : Color.clear;
                }
                if (i < realmRowUnderlines.Count && realmRowUnderlines[i] != null)
                {
                    realmRowUnderlines[i].color = isSelected ? new Color(1.0f, 0.78f, 0.35f, 0.75f) : Color.clear;
                }
                if (i < realmStatusLabels.Count && realmStatusLabels[i] != null)
                {
                    realmStatusLabels[i].text = isSelected
                        ? (isCompleted ? "[ ACTIVE REALM - CONQUERED * ]" : "[ ACTIVE REALM ]")
                        : (isCompleted ? "[ CONQUERED * ]" : "SELECT REALM");
                    realmStatusLabels[i].color = isSelected ? amberGold : (isCompleted ? radiantIvory : mutedIndigo);
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

            // Extinguish lantern audio (flame hiss & cold air draft)
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayFlareExtinguish();
                SoundEffectManager.Instance?.PlayAirDraft();
            }

            // Immediately disable interaction on menus so player feels the darkness setting in
            if (homeGroup != null)
            {
                homeGroup.interactable = false;
                homeGroup.blocksRaycasts = false;
            }
            if (realmsGroup != null)
            {
                realmsGroup.interactable = false;
                realmsGroup.blocksRaycasts = false;
            }

            if (loadingContainer != null) loadingContainer.SetActive(true);
            if (loadingText != null)
            {
                var r = AvailableRealms[Mathf.Clamp(selectedRealmIndex, 0, AvailableRealms.Length - 1)];
                loadingText.text = $"DESCENDING INTO {r.Title}...";
                loadingText.color = new Color(radiantIvory.r, radiantIvory.g, radiantIvory.b, 0f);
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
            const float transitionDuration = 1.30f;
            while (elapsed < transitionDuration || operation.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(Mathf.Min(elapsed / transitionDuration, operation.progress / 0.9f));

                // Fade UI away into the deep dark
                float uiAlpha = Mathf.Clamp01(1f - progress * 2.2f);
                if (homeGroup != null) homeGroup.alpha = uiAlpha;
                if (realmsGroup != null) realmsGroup.alpha = uiAlpha;
                if (homeHeaderGroup != null) homeHeaderGroup.alpha = uiAlpha;

                // Animate lantern flame extinction & stage darkness
                stage?.SetPlayZoom(progress);

                // Deep obsidian darkness collapses across the entire screen
                if (curtain != null)
                {
                    float darknessAlpha = SmoothStep(Mathf.Clamp01(progress * 1.15f));
                    curtain.color = new Color(0.005f, 0.008f, 0.016f, darknessAlpha);
                }

                if (loadingBarFill != null)
                {
                    loadingBarFill.rectTransform.anchorMax = new Vector2(progress, 1f);
                    loadingBarFill.color = new Color(amberGold.r, amberGold.g, amberGold.b, Mathf.Clamp01(progress * 1.5f));
                }
                if (loadingText != null)
                {
                    float textAlpha = SmoothStep(Mathf.Clamp01(Mathf.InverseLerp(0.30f, 0.85f, progress)));
                    loadingText.color = new Color(radiantIvory.r, radiantIvory.g, radiantIvory.b, textAlpha);
                }

                yield return null;
            }

            stage?.SetPlayZoom(1f);
            if (curtain != null)
            {
                curtain.color = new Color(0.005f, 0.008f, 0.016f, 1f);
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
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void OnVSyncToggled(bool value)
        {
            MenuPreferences.SetVSync(value);
            UpdateSwitchVisual(vsyncToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void OnReducedMotionToggled(bool value)
        {
            MenuPreferences.SetReducedMotion(value);
            UpdateSwitchVisual(motionToggle);
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void CycleTargetFPS()
        {
            int current = MenuPreferences.TargetFPS;
            int next = current == 60 ? 120 : (current == 120 ? -1 : 60);
            MenuPreferences.SetTargetFPS(next);
            UpdateFpsLabel();
            if (Application.isPlaying) SoundEffectManager.Instance?.PlayUIClick();
        }

        private void UpdateFpsLabel()
        {
            if (fpsButtonValueText == null) return;
            int fps = MenuPreferences.TargetFPS;
            fpsButtonValueText.text = fps <= 0 ? "[ UNLIMITED ]" : $"[ {fps} FPS ]";
        }

        public void ResetDefaults()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsUI();
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
                txt.text = toggle.isOn ? "[ ON ]" : "[ OFF ]";
                txt.color = toggle.isOn ? amberGold : mutedIndigo;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = Color.clear;
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

            // Procedural Full-Screen Depth Blur Vignette & Rounded Capsule Slider Sprites
            vignetteBlurSprite = CreateVignetteBlurSprite(256);
            roundTrackSprite = CreateRoundedCapsuleSprite(24, 8, new Color(0.40f, 0.50f, 0.65f, 0.35f), 4);
            roundFillSprite = CreateRoundedCapsuleSprite(24, 8, amberGold, 4);
            roundHandleSprite = CreateCircularHandleSprite(32);

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
        // HOME VIEW: CENTERED HOLLOW KNIGHT / LIGHTEN ROAD ETHEREAL SANCTUM
        // =====================================================================

        private void BuildFramelessHomeView()
        {
            homeButtons.Clear();

            // Centered Main Menu Container
            var centerColumn = CreateAnchoredRect("Center Menu Column", home, new Vector2(960f, 960f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));

            // Soft atmospheric darkness behind menu to enhance contrast against background pillars
            var centerVignette = CreateAnchoredRect("Center Stage Shadow Vignette", centerColumn, new Vector2(1100f, 1000f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var vigImg = centerVignette.gameObject.AddComponent<Image>();
            vigImg.sprite = softRadialSprite;
            vigImg.color = new Color(0.008f, 0.012f, 0.022f, 0.86f);
            vigImg.raycastTarget = false;

            // 1. Monumental Title Header (Crown Arch, Cinzel Serif Title, Sacred Lantern Crest, Active Realm)
            homeHeaderRect = CreateAnchoredRect("Home Header Container", centerColumn, new Vector2(960f, 380f), new Vector2(0.5f, 0.5f), new Vector2(0f, 215f), pivot: new Vector2(0.5f, 0.5f));
            homeHeaderBasePos = homeHeaderRect.anchoredPosition;
            homeHeaderGroup = homeHeaderRect.gameObject.AddComponent<CanvasGroup>();

            // Soft Golden Radial Bloom behind Title
            var titleAura = CreateAnchoredRect("Title Soft Lantern Glow", homeHeaderRect, new Vector2(720f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), pivot: new Vector2(0.5f, 0.5f));
            var auraImg = titleAura.gameObject.AddComponent<Image>();
            auraImg.sprite = softRadialSprite;
            auraImg.color = new Color(1.0f, 0.80f, 0.45f, 0.07f);
            auraImg.raycastTarget = false;

            // Top Filigree Crown Arch
            if (topFiligreeSprite != null)
            {
                var topArchRect = CreateAnchoredRect("Top Filigree Crown", homeHeaderRect, new Vector2(760f, 85f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), pivot: new Vector2(0.5f, 0.5f));
                var topArchImg = topArchRect.gameObject.AddComponent<Image>();
                topArchImg.sprite = topFiligreeSprite;
                topArchImg.color = Color.white;
                topArchImg.raycastTarget = false;
            }

            // Monumental Serif Title: THE LIGHTEN ROAD
            var mainTitle = CreateLabel("Title Lighten Road", homeHeaderRect, "THE LIGHTEN ROAD", 56f, radiantIvory, new Vector2(940f, 70f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), pivot: new Vector2(0.5f, 0.5f));
            mainTitle.alignment = TextAlignmentOptions.Center;
            mainTitle.fontStyle = FontStyles.Bold;
            mainTitle.characterSpacing = 9.5f;

            // Bottom Sacred Lantern Relic & Ember Vein Wings Crest
            if (bottomCrestSprite != null)
            {
                var botCrestRect = CreateAnchoredRect("Bottom Lantern Crest", homeHeaderRect, new Vector2(920f, 130f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), pivot: new Vector2(0.5f, 0.5f));
                var botCrestImg = botCrestRect.gameObject.AddComponent<Image>();
                botCrestImg.sprite = bottomCrestSprite;
                botCrestImg.color = Color.white;
                botCrestImg.raycastTarget = false;
            }

            // Whispered Active Realm Badge
            activeRealmBadgeText = CreateLabel("Active Realm Text", homeHeaderRect, "--   REALM I  :  THE FIRST LIGHT   --", 11.5f, amberGold, new Vector2(700f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), pivot: new Vector2(0.5f, 0.5f));
            activeRealmBadgeText.alignment = TextAlignmentOptions.Center;
            activeRealmBadgeText.characterSpacing = 4.5f;

            // 2. Centered Vertical Menu Stack with Symmetrical Ember Wing Pointers
            playButton = CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Enter the Archive",
                labelText: "START GAME",
                yPos: -45f,
                cameraPan: new Vector2(0f, 10f),
                onClick: Play,
                isPrimary: true,
                customWingOffset: 135f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Settings",
                labelText: "OPTIONS",
                yPos: -100f,
                cameraPan: new Vector2(14f, 4f),
                onClick: ShowSettings,
                isPrimary: false,
                customWingOffset: 118f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Realms & Chapters",
                labelText: "REALMS & CHAPTERS",
                yPos: -155f,
                cameraPan: new Vector2(-16f, 6f),
                onClick: ShowRealms,
                isPrimary: false,
                customWingOffset: 175f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Controls & Guide",
                labelText: "CONTROLS",
                yPos: -210f,
                cameraPan: new Vector2(-12f, -6f),
                onClick: ShowControls,
                isPrimary: false,
                customWingOffset: 122f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Credits",
                labelText: "CREDITS",
                yPos: -265f,
                cameraPan: new Vector2(12f, -8f),
                onClick: ShowCredits,
                isPrimary: false,
                customWingOffset: 118f);

            CreateFramelessMenuButton(
                centerColumn,
                gameObjectName: "Leave the Light",
                labelText: "QUIT GAME",
                yPos: -320f,
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
            float customWingOffset = 140f)
        {
            var itemRect = CreateAnchoredRect(gameObjectName, parent, new Vector2(480f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));

            var btnGroup = itemRect.gameObject.AddComponent<CanvasGroup>();
            btnGroup.alpha = 0f;
            btnGroup.interactable = false;
            btnGroup.blocksRaycasts = false;

            // Transparent hit graphic
            var hitGraphic = itemRect.gameObject.AddComponent<Image>();
            hitGraphic.color = Color.clear;

            // Soft radial lantern glow behind text on hover
            var glowRect = CreateAnchoredRect("Soft Lantern Aura", itemRect, new Vector2(240f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = Color.clear;
            glowImg.raycastTarget = false;

            // Centered Title Typography
            var titleTxt = CreateLabel("Title", itemRect, labelText.ToUpperInvariant(), isPrimary ? 22f : 19f, radiantIvory, new Vector2(440f, 32f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = isPrimary ? 6.5f : 5.2f;

            // Symmetrical Ember Wing Pointers (Hollow Knight / The Lighten Road style)
            var wingContainer = CreateAnchoredRect("Wings Container", itemRect, new Vector2(480f, 32f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
            wingGroup.alpha = 0f;

            RectTransform leftWingRect = null;
            RectTransform rightWingRect = null;
            if (buttonPointerSprite != null)
            {
                leftWingRect = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(48f, 24f), new Vector2(0.5f, 0.5f), new Vector2(-customWingOffset, 0f), pivot: new Vector2(0.5f, 0.5f));
                var leftImg = leftWingRect.gameObject.AddComponent<Image>();
                leftImg.sprite = buttonPointerSprite;
                leftImg.color = Color.white;
                leftImg.raycastTarget = false;

                rightWingRect = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(48f, 24f), new Vector2(0.5f, 0.5f), new Vector2(customWingOffset, 0f), pivot: new Vector2(0.5f, 0.5f));
                rightWingRect.localScale = new Vector3(-1f, 1f, 1f); // Mirrored horizontally
                var rightImg = rightWingRect.gameObject.AddComponent<Image>();
                rightImg.sprite = buttonPointerSprite;
                rightImg.color = Color.white;
                rightImg.raycastTarget = false;
            }

            var btn = itemRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = hitGraphic;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(onClick);

            var motion = itemRect.gameObject.AddComponent<MenuButtonMotion>();
            motion.Text = titleTxt;
            motion.SoftGlow = glowImg;
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
        // SUB-VIEW 1: REALMS & CHAPTERS (FRAMELESS ORNATE SELECTOR)
        // =====================================================================

        private void BuildFramelessRealmsView()
        {
            var surface = CreateEtherealSubViewFrame(realms, "REALMS & CHAPTERS", "CHOOSE WHICH ARCHIVE REALM TO ILLUMINATE");

            realmButtons.Clear();
            realmTitles.Clear();
            realmWingGroups.Clear();
            realmRowAuras.Clear();
            realmRowUnderlines.Clear();
            realmStatusLabels.Clear();

            // Top: Realm Options (Floating Frameless Typography with Symmetrical Wings)
            for (int i = 0; i < AvailableRealms.Length; i++)
            {
                int capturedIndex = i;
                var r = AvailableRealms[i];
                float yPos = 170f - i * 68f;

                var row = CreateAnchoredRect($"Realm Option {i}", surface, new Vector2(740f, 54f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));
                var hit = row.gameObject.AddComponent<Image>();
                hit.color = Color.clear;

                // Soft radial aura behind row
                var auraRect = CreateAnchoredRect("Row Aura", row, new Vector2(580f, 48f), new Vector2(0.5f, 0.5f), Vector2.zero);
                var auraImg = auraRect.gameObject.AddComponent<Image>();
                auraImg.sprite = softRadialSprite;
                auraImg.color = (i == selectedRealmIndex) ? new Color(1.0f, 0.72f, 0.28f, 0.22f) : Color.clear;
                auraImg.raycastTarget = false;
                realmRowAuras.Add(auraImg);

                // Symmetrical Wing Pointers for Realm Option
                var wingContainer = CreateAnchoredRect("Wings Container", row, new Vector2(600f, 32f), new Vector2(0.5f, 0.5f), Vector2.zero);
                var wingGroup = wingContainer.gameObject.AddComponent<CanvasGroup>();
                wingGroup.alpha = (i == selectedRealmIndex) ? 1f : 0f;
                realmWingGroups.Add(wingGroup);

                if (buttonPointerSprite != null)
                {
                    var leftWing = CreateAnchoredRect("Left Pointer", wingContainer, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(-260f, 0f));
                    var leftImg = leftWing.gameObject.AddComponent<Image>();
                    leftImg.sprite = buttonPointerSprite;
                    leftImg.color = Color.white;
                    leftImg.raycastTarget = false;

                    var rightWing = CreateAnchoredRect("Right Pointer", wingContainer, new Vector2(36f, 18f), new Vector2(0.5f, 0.5f), new Vector2(260f, 0f));
                    rightWing.localScale = new Vector3(-1f, 1f, 1f);
                    var rightImg = rightWing.gameObject.AddComponent<Image>();
                    rightImg.sprite = buttonPointerSprite;
                    rightImg.color = Color.white;
                    rightImg.raycastTarget = false;
                }

                // Title: REALM I : THE FIRST LIGHT
                var titleTxt = CreateLabel("Realm Title", row, $"{r.Number}  :  {r.Title}", 20f, (i == selectedRealmIndex) ? FocusedAmberGold : radiantIvory, new Vector2(500f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));
                titleTxt.rectTransform.pivot = Vector2.one * 0.5f;
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.characterSpacing = 4f;
                realmTitles.Add(titleTxt);

                // Status text (Used by tests & player feedback)
                var statusTxt = CreateLabel("Status", row, (i == selectedRealmIndex) ? "[ ACTIVE REALM ]" : "SELECT REALM", 10.5f, amberGold, new Vector2(360f, 18f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f));
                statusTxt.rectTransform.pivot = Vector2.one * 0.5f;
                statusTxt.alignment = TextAlignmentOptions.Center;
                statusTxt.characterSpacing = 2.5f;
                realmStatusLabels.Add(statusTxt);

                // Underline filigree
                var lineRect = CreateAnchoredRect("Filigree Underline", row, new Vector2(440f, 4f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f));
                var lineImg = lineRect.gameObject.AddComponent<Image>();
                lineImg.sprite = filigreeSprite;
                lineImg.color = (i == selectedRealmIndex) ? new Color(1.0f, 0.78f, 0.35f, 0.75f) : Color.clear;
                lineImg.raycastTarget = false;
                realmRowUnderlines.Add(lineImg);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() => SelectRealm(capturedIndex));
                realmButtons.Add(btn);
                if (i == 0) realmsFirst = btn;
            }

            // Bottom Showcase Vitrini (Cinematic Realm Lore & Atmosphere)
            var showcaseBox = CreateAnchoredRect("Showcase Container", surface, new Vector2(780f, 160f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f));
            var showcaseAura = CreateAnchoredRect("Showcase Aura", showcaseBox, new Vector2(620f, 140f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var scAuraImg = showcaseAura.gameObject.AddComponent<Image>();
            scAuraImg.sprite = softRadialSprite;
            scAuraImg.color = new Color(0.008f, 0.015f, 0.030f, 0.65f);
            scAuraImg.raycastTarget = false;

            activeRealmShowcaseTag = CreateLabel("Showcase Tag", showcaseBox, "- CHAPTER I : THE ABYSSAL ARCHIVE -", 11.5f, amberGold, new Vector2(740f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 48f));
            activeRealmShowcaseTag.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseTag.alignment = TextAlignmentOptions.Center;
            activeRealmShowcaseTag.characterSpacing = 5f;

            activeRealmShowcaseDesc = CreateLabel("Showcase Desc", showcaseBox, "\"Ancient stone halls carved deep within the bedrock. Master lantern jumps and ember veins.\"", 13f, moonlightSilver, new Vector2(720f, 44f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f));
            activeRealmShowcaseDesc.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseDesc.alignment = TextAlignmentOptions.Center;

            activeRealmShowcaseStatus = CreateLabel("Showcase Status", showcaseBox, "[ ACTIVE REALM ]", 12f, amberGold, new Vector2(500f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f));
            activeRealmShowcaseStatus.rectTransform.pivot = Vector2.one * 0.5f;
            activeRealmShowcaseStatus.alignment = TextAlignmentOptions.Center;
            activeRealmShowcaseStatus.characterSpacing = 3.5f;

            var scDivider = CreateAnchoredRect("Showcase Divider", showcaseBox, new Vector2(480f, 4f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f));
            var scDivImg = scDivider.gameObject.AddComponent<Image>();
            scDivImg.sprite = filigreeSprite;
            scDivImg.color = new Color(1.0f, 0.74f, 0.30f, 0.40f);
            scDivImg.raycastTarget = false;

            // Frameless action buttons
            CreateEtherealActionButton(surface, "Enter Selected Realm  >", new Vector2(175f, -278f), Play, isPrimary: true);
            CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(-175f, -278f), ShowHome, isPrimary: false);
        }

        // =====================================================================
        // SUB-VIEW 2: SETTINGS (FRAMELESS AUDIO MIXER, DISPLAY & MOTION)
        // =====================================================================

        private void BuildFramelessSettingsView()
        {
            var surface = CreateEtherealSubViewFrame(settings, "SANCTUARY SETTINGS", "HARMONIZE AUDIO, DISPLAY & MOTION");

            masterSlider = CreateEtherealSlider(surface, "Master Volume", "Global output level for all archive audio", 175f, out masterValueText);
            musicSlider = CreateEtherealSlider(surface, "Ambient Music", "Background score level (In-game key: M)", 115f, out musicValueText);
            effectsSlider = CreateEtherealSlider(surface, "Sound Effects (SFX)", "Footsteps, lantern flare & embers (In-game key: N)", 55f, out effectsValueText);

            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);

            fullscreenToggle = CreateEtherealToggle(surface, "Fullscreen Mode", "Expand the archive across the entire display", -15f, out fullscreenToggleValueText, OnFullscreenToggled);
            vsyncToggle = CreateEtherealToggle(surface, "Vertical Sync (V-Sync)", "Synchronize frame pacing with display refresh", -75f, out vsyncToggleValueText, OnVSyncToggled);
            motionToggle = CreateEtherealToggle(surface, "Reduced Motion", "Calm lantern sway, parallax and HUD tremors", -135f, out motionToggleValueText, OnReducedMotionToggled);

            CreateEtherealFpsSelector(surface, "Target Frame Rate", "Frame rate ceiling (60 / 120 / Unlimited)", -195f);

            CreateEtherealActionButton(surface, "Reset Defaults", new Vector2(-175f, -290f), ResetDefaults, isPrimary: false);
            CreateEtherealActionButton(surface, "<  Save & Return", new Vector2(175f, -290f), ShowHome, isPrimary: true);

            SyncSettingsUI();
        }

        private Slider CreateEtherealSlider(RectTransform parent, string title, string subtitle, float yPos, out TMP_Text valueText)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 52f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));
            var rowHit = row.gameObject.AddComponent<Image>();
            rowHit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 14f, radiantIvory, new Vector2(360f, 22f), new Vector2(0f, 0.5f), new Vector2(0f, 10f), pivot: new Vector2(0f, 0.5f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11f, moonlightSilver, new Vector2(360f, 18f), new Vector2(0f, 0.5f), new Vector2(0f, -10f), pivot: new Vector2(0f, 0.5f));

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row, false);
            var sRect = (RectTransform)sliderGo.transform;
            sRect.anchorMin = new Vector2(1f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(1f, 0.5f);
            sRect.sizeDelta = new Vector2(360f, 28f);
            sRect.anchoredPosition = new Vector2(0f, 0f);

            var track = CreateAnchoredRect("Track", sRect, new Vector2(280f, 4f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), pivot: new Vector2(0f, 0.5f));
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
            var handle = CreateAnchoredRect("Handle", handleSlide, new Vector2(16f, 16f), new Vector2(0f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            handle.localEulerAngles = Vector3.zero;

            // Soft radial bloom aura behind circular handle
            var handleGlow = CreateAnchoredRect("Handle Glow", handle, new Vector2(28f, 28f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            var hGlowImg = handleGlow.gameObject.AddComponent<Image>();
            hGlowImg.sprite = softRadialSprite;
            hGlowImg.color = new Color(1.0f, 0.75f, 0.30f, 0.35f);
            hGlowImg.raycastTarget = false;

            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = roundHandleSprite;
            handleImg.color = Color.white;

            valueText = CreateLabel("Value", sRect, "85%", 13f, amberGold, new Vector2(64f, 24f), new Vector2(1f, 0.5f), Vector2.zero, pivot: new Vector2(1f, 0.5f));
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.fontStyle = FontStyles.Bold;

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            return slider;
        }

        private Toggle CreateEtherealToggle(RectTransform parent, string title, string subtitle, float yPos, out TMP_Text valText, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 14f, radiantIvory, new Vector2(480f, 22f), new Vector2(0f, 0.5f), new Vector2(0f, 8f), pivot: new Vector2(0f, 0.5f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11f, moonlightSilver, new Vector2(480f, 18f), new Vector2(0f, 0.5f), new Vector2(0f, -10f), pivot: new Vector2(0f, 0.5f));

            var pill = CreateAnchoredRect("Pill", row, new Vector2(120f, 30f), new Vector2(1f, 0.5f), Vector2.zero, pivot: new Vector2(1f, 0.5f));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.color = Color.clear;

            valText = CreateLabel("Value", pill, "[ OFF ]", 13f, mutedIndigo, new Vector2(120f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            valText.alignment = TextAlignmentOptions.Right;
            valText.fontStyle = FontStyles.Bold;
            valText.characterSpacing = 2.5f;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = hit;
            toggle.graphic = null;
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        private void CreateEtherealFpsSelector(RectTransform parent, string title, string subtitle, float yPos)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(780f, 48f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var titleTxt = CreateLabel("Title", row, title.ToUpperInvariant(), 14f, radiantIvory, new Vector2(480f, 22f), new Vector2(0f, 0.5f), new Vector2(0f, 8f), pivot: new Vector2(0f, 0.5f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var subTxt = CreateLabel("Subtitle", row, subtitle, 11f, moonlightSilver, new Vector2(480f, 18f), new Vector2(0f, 0.5f), new Vector2(0f, -10f), pivot: new Vector2(0f, 0.5f));

            var pill = CreateAnchoredRect("FPS Pill", row, new Vector2(160f, 30f), new Vector2(1f, 0.5f), Vector2.zero, pivot: new Vector2(1f, 0.5f));
            var fpsVal = CreateLabel("FPS Value", pill, "[ 60 FPS ]", 13f, amberGold, new Vector2(160f, 30f), new Vector2(0.5f, 0.5f), Vector2.zero, pivot: new Vector2(0.5f, 0.5f));
            fpsVal.alignment = TextAlignmentOptions.Right;
            fpsVal.fontStyle = FontStyles.Bold;
            fpsVal.characterSpacing = 2f;
            fpsButtonValueText = fpsVal;

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

            CreateControlRow(surface, "[ A / D  or  ARROWS ]", "TRAVERSE THE ARCHIVE", "Smooth horizontal movement with momentum & air control", 156f);
            CreateControlRow(surface, "[ SPACE  /  W ]", "LEAP & COYOTE JUMP", "Hold for higher arc; forgiving ledge grace window", 94f);
            CreateControlRow(surface, "[ LEFT SHIFT ]", "ARCHIVE SPRINT", "Burst speed across wide chasms (consumes Stamina)", 32f);
            CreateControlRow(surface, "[ F  /  L-CLICK ]", "LANTERN FLARE SURGE", "Ignite a radiant light burst to reveal platforms", -30f);
            CreateControlRow(surface, "[ E  (HOLD) ]", "CHANNEL EMBER VEIN", "Pour lantern light into ancient conduits & bridges", -92f);
            CreateControlRow(surface, "[ M  /  N ]", "LIVE AUDIO MIXING", "Mute/adjust music (M) or sound effects (N) in-game", -154f);

            controlsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateControlRow(RectTransform parent, string keys, string actionTitle, string desc, float yPos)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(780f, 50f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos), pivot: new Vector2(0.5f, 0.5f));

            var keyTxt = CreateLabel("Key Text", row, keys, 13f, amberGold, new Vector2(230f, 30f), new Vector2(0f, 0.5f), Vector2.zero, pivot: new Vector2(0f, 0.5f));
            keyTxt.alignment = TextAlignmentOptions.Left;
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 2.5f;

            var actTxt = CreateLabel("Action", row, actionTitle, 14f, radiantIvory, new Vector2(530f, 22f), new Vector2(0f, 0.5f), new Vector2(245f, 8f), pivot: new Vector2(0f, 0.5f));
            actTxt.fontStyle = FontStyles.Bold;
            actTxt.characterSpacing = 2.5f;

            var descTxt = CreateLabel("Description", row, desc, 11.5f, moonlightSilver, new Vector2(530f, 18f), new Vector2(0f, 0.5f), new Vector2(245f, -10f), pivot: new Vector2(0f, 0.5f));

            var div = CreateAnchoredRect("Divider", row, new Vector2(780f, 1f), new Vector2(0.5f, 0f), Vector2.zero, pivot: new Vector2(0.5f, 0f));
            var divImg = div.gameObject.AddComponent<Image>();
            divImg.color = new Color(0.35f, 0.45f, 0.60f, 0.15f);
        }

        // =====================================================================
        // SUB-VIEW 4: CREDITS
        // =====================================================================

        private void BuildFramelessCreditsView()
        {
            var surface = CreateEtherealSubViewFrame(credits, "KEEPERS OF THE FLAME", "CRAFTED FOR THE 2026 GAME DEVELOPMENT PROJECT");

            CreateCreditBlock(surface, "SIRAÇ ŞİMŞEK", "MOVEMENT, LANTERN SYSTEMS, AUDIO & PROCEDURAL FRONTEND", "Player physics, stamina & lantern HUD, dynamic lighting, foley & animated archive stage", 130f);
            CreateCreditBlock(surface, "KEMAL", "PUZZLE MECHANISMS & EMBER VEIN ARCHITECTURE", "Ember Vein conduits, Flooded Mechanisms sandbox, interactive elevators & hazards", 10f);
            CreateCreditBlock(surface, "ARCHIVE SOUNDSCAPE & ART", "ATMOSPHERIC AUDIO & PROCEDURAL SILHOUETTES", "Custom multi-layered SDF gothic colonnades, swinging lantern light rays & CC0 foley", -110f);

            creditsFirst = CreateEtherealActionButton(surface, "<  Return to Title", new Vector2(0f, -278f), ShowHome, isPrimary: true);
        }

        private void CreateCreditBlock(RectTransform parent, string name, string role, string details, float yPos)
        {
            var block = CreateAnchoredRect(name, parent, new Vector2(800f, 96f), new Vector2(0.5f, 0.5f), new Vector2(0f, yPos));

            var nameTxt = CreateLabel("Name", block, name, 22f, radiantIvory, new Vector2(780f, 30f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f));
            nameTxt.rectTransform.pivot = Vector2.one * 0.5f;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.characterSpacing = 4.5f;

            var roleTxt = CreateLabel("Role", block, role, 11.5f, amberGold, new Vector2(780f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f));
            roleTxt.rectTransform.pivot = Vector2.one * 0.5f;
            roleTxt.alignment = TextAlignmentOptions.Center;
            roleTxt.characterSpacing = 3.5f;

            var detTxt = CreateLabel("Details", block, details, 11.5f, moonlightSilver, new Vector2(780f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f));
            detTxt.rectTransform.pivot = Vector2.one * 0.5f;
            detTxt.alignment = TextAlignmentOptions.Center;

            var div = CreateAnchoredRect("Divider", block, new Vector2(400f, 2f), new Vector2(0.5f, 0f), new Vector2(0f, -6f));
            var divImg = div.gameObject.AddComponent<Image>();
            divImg.sprite = filigreeSprite;
            divImg.color = new Color(1.0f, 0.74f, 0.30f, 0.35f);
        }

        // =====================================================================
        // SUB-VIEW 5: QUIT CONFIRMATION
        // =====================================================================

        private void BuildFramelessQuitView()
        {
            var surface = CreateAnchoredRect("Quit Ethereal Frame", quit, new Vector2(760f, 360f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var surfHit = surface.gameObject.AddComponent<Image>();
            surfHit.color = Color.clear;

            var tag = CreateLabel("Quit Tag", surface, "- UNTIL YOUR NEXT RETURN -", 11.5f, amberGold, new Vector2(620f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f));
            tag.rectTransform.pivot = Vector2.one * 0.5f;
            tag.alignment = TextAlignmentOptions.Center;
            tag.characterSpacing = 6f;

            var title = CreateLabel("Quit Title", surface, "EXTINGUISH THE LANTERN?", 34f, radiantIvory, new Vector2(720f, 46f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f));
            title.rectTransform.pivot = Vector2.one * 0.5f;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;

            var botFiligree = CreateAnchoredRect("Bottom Filigree", surface, new Vector2(460f, 6f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f));
            var botImg = botFiligree.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.75f);
            botImg.raycastTarget = false;

            var note = CreateLabel("Quit Note", surface, "The ruined archive will wait in the midnight dark.\nAll audio and realm preferences have been preserved.", 13.5f, moonlightSilver, new Vector2(620f, 54f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f));
            note.rectTransform.pivot = Vector2.one * 0.5f;
            note.alignment = TextAlignmentOptions.Center;

            quitFirst = CreateEtherealActionButton(surface, "Stay in the Light", new Vector2(-160f, -125f), ShowHome, isPrimary: true, width: 260f);
            CreateEtherealActionButton(surface, "Extinguish & Exit", new Vector2(160f, -125f), ConfirmQuit, isPrimary: false, width: 260f);
        }

        // =====================================================================
        // SHARED FRAMELESS ETHEREAL HELPERS
        // =====================================================================

        private RectTransform CreateEtherealSubViewFrame(RectTransform viewRoot, string heading, string subheading)
        {
            var container = CreateAnchoredRect("Ethereal Subview Container", viewRoot, new Vector2(1000f, 800f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var containerHit = container.gameObject.AddComponent<Image>();
            containerHit.color = Color.clear;

            var sub = CreateLabel("Subheading", container, $"-   {subheading}   -", 11f, amberGold, new Vector2(860f, 22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 320f));
            sub.rectTransform.pivot = Vector2.one * 0.5f;
            sub.alignment = TextAlignmentOptions.Center;
            sub.characterSpacing = 5.5f;

            var head = CreateLabel("Heading", container, heading, 34f, radiantIvory, new Vector2(900f, 44f), new Vector2(0.5f, 0.5f), new Vector2(0f, 280f));
            head.rectTransform.pivot = Vector2.one * 0.5f;
            head.alignment = TextAlignmentOptions.Center;
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 6.5f;

            var botCrest = CreateAnchoredRect("Header Bottom Filigree", container, new Vector2(580f, 6f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f));
            var botImg = botCrest.gameObject.AddComponent<Image>();
            botImg.sprite = filigreeSprite;
            botImg.color = new Color(1.0f, 0.74f, 0.30f, 0.75f);
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
            var rect = CreateAnchoredRect(label, parent, new Vector2(width, 48f), new Vector2(0.5f, 0.5f), anchoredPos);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var glowRect = CreateAnchoredRect("Soft Glow", rect, new Vector2(width + 24f, 44f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var glowImg = glowRect.gameObject.AddComponent<Image>();
            glowImg.sprite = softRadialSprite;
            glowImg.color = isPrimary ? new Color(1.0f, 0.74f, 0.28f, 0.12f) : Color.clear;
            glowImg.raycastTarget = false;

            var lineRect = CreateAnchoredRect("Filigree Underline", rect, new Vector2(width * 0.72f, 4f), new Vector2(0.5f, 0.5f), new Vector2(0f, -17f));
            var lineImg = lineRect.gameObject.AddComponent<Image>();
            lineImg.sprite = filigreeSprite;
            lineImg.color = isPrimary ? amberGold : new Color(0.60f, 0.70f, 0.85f, 0.30f);
            lineImg.raycastTarget = false;

            var txt = CreateLabel("Caption", rect, label.ToUpperInvariant(), 14f, isPrimary ? amberGold : radiantIvory, new Vector2(width, 36f), new Vector2(0.5f, 0.5f), Vector2.zero);
            txt.rectTransform.pivot = Vector2.one * 0.5f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3.5f;

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

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
