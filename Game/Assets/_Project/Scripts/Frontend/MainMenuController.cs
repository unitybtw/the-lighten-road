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
    /// Redesigned interactive Main Menu for The Lighten Road.
    /// Features a cinematic lantern ignition intro, mouse-tracking lantern spotlight,
    /// floating multi-depth ember particles, left-aligned glass navigation dock,
    /// and a smooth slide-in glassmorphism drawer for Realms, Settings, Controls, and Credits.
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

        [SerializeField] private Texture2D backdrop;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private string gameplayScene = DefaultGameplayScene;

        // Theme Palette
        private readonly Color ivory = new Color32(244, 236, 216, 255);
        private readonly Color warmStone = new Color32(168, 162, 146, 255);
        private readonly Color mutedStone = new Color32(118, 114, 102, 255);
        private readonly Color amberGold = new Color32(242, 166, 64, 255);
        private readonly Color deepAmber = new Color32(196, 118, 38, 255);
        private readonly Color glassDark = new Color(0.045f, 0.055f, 0.07f, 0.84f);
        private readonly Color glassCard = new Color(0.07f, 0.085f, 0.105f, 0.72f);
        private readonly Color glassBorder = new Color(0.88f, 0.64f, 0.30f, 0.28f);

        private Canvas canvas;
        private RectTransform safeFrame;
        private RectTransform background;
        private RectTransform pedestalLanternOuter;
        private RectTransform pedestalLanternCore;
        private RectTransform cursorSpotlight;
        private Image pedestalOuterImage;
        private Image pedestalCoreImage;
        private Image cursorSpotImage;
        private Image curtain;
        private Image drawerBackdropVeil;

        // Views & Groups
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
        private Button settingsFirst;
        private Button controlsFirst;
        private Button creditsFirst;
        private Button quitFirst;

        private readonly Dictionary<string, MenuButtonMotion> navMotions = new Dictionary<string, MenuButtonMotion>();
        private readonly List<Image> realmCardBackgrounds = new List<Image>();
        private readonly List<Image> realmCardBorders = new List<Image>();
        private readonly List<TMP_Text> realmStatusLabels = new List<TMP_Text>();

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle fullscreenToggle;
        private Toggle vsyncToggle;
        private Toggle motionToggle;
        private TMP_Text fpsButtonValueText;

        // Particles
        private struct EmberParticle
        {
            public RectTransform Rect;
            public Image Img;
            public float SeedX;
            public float SeedY;
            public float SpeedY;
            public float DriftAmp;
            public float DriftFreq;
            public bool IsBrightEmber;
        }
        private readonly List<EmberParticle> particles = new List<EmberParticle>();

        // State
        private float age;
        private float viewAge;
        private string view = "home";
        private bool introDone;
        private bool introIgnitePlayed;
        private bool loading;
        private bool quitting;
        private int selectedRealmIndex;
        private Vector2 smoothedPointer;
        private Vector2 cursorCanvasPos;
        private Texture2D radialGlowTexture;
        private Sprite radialGlowSprite;
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
            if (font == null) font = Resources.Load<TMP_FontAsset>("UI/MenuType");
            if (backdrop == null)
            {
#if UNITY_EDITOR
                backdrop = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Frontend/ArchiveMenuBackdrop.png");
#endif
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

            // Ensure background music is softly playing
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
            if (radialGlowSprite != null) Destroy(radialGlowSprite);
            if (radialGlowTexture != null) Destroy(radialGlowTexture);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            viewAge += dt;

            // Play lantern ignition sound early in the cinematic intro
            if (!introIgnitePlayed && age >= 0.22f)
            {
                introIgnitePlayed = true;
                if (Application.isPlaying && !MenuPreferences.ReducedMotion)
                {
                    SoundEffectManager.Instance?.PlayFlareIgnite();
                }
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

            if (!introDone && age >= 3.2f)
            {
                SkipIntro();
            }

            float introReveal = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.InverseLerp(0.85f, 2.65f, age));
            if (!loading && !quitting && curtain != null)
            {
                float curtainFade = 1f - SmoothStep(Mathf.InverseLerp(0.15f, 2.15f, age));
                curtain.color = new Color(0.01f, 0.012f, 0.015f, curtainFade);
            }

            float drawerSlide = MenuPreferences.ReducedMotion ? 1f : SmoothStep(Mathf.Clamp01(viewAge / 0.26f));
            bool isDrawerOpen = view == "realms" || view == "settings" || view == "controls" || view == "credits";
            bool isQuitOpen = view == "quit";

            if (drawerBackdropVeil != null)
            {
                float targetVeil = isQuitOpen ? 0.72f : (isDrawerOpen ? 0.38f : 0f);
                drawerBackdropVeil.color = new Color(0.01f, 0.015f, 0.02f, Mathf.MoveTowards(drawerBackdropVeil.color.a, targetVeil, dt * 3.5f));
            }

            // Home dock stays visible on the left during drawer views, but disables raycasts when Quit modal is open
            UpdateHomeDock(introReveal, !isQuitOpen && view == "home", !isQuitOpen);
            UpdateDrawerPanel(realmsGroup, view == "realms", drawerSlide);
            UpdateDrawerPanel(settingsGroup, view == "settings", drawerSlide);
            UpdateDrawerPanel(controlsGroup, view == "controls", drawerSlide);
            UpdateDrawerPanel(creditsGroup, view == "credits", drawerSlide);
            UpdateModalPanel(quitGroup, isQuitOpen, drawerSlide);

            if (hintText != null)
            {
                hintText.gameObject.SetActive(!introDone);
            }

            UpdateNavHighlights();
            AnimateAtmosphere(dt);
            UpdateSafeFrame();
        }

        private void UpdateHomeDock(float reveal, bool isStrictlyHome, bool allowDockClicks)
        {
            if (homeGroup == null) return;
            float targetAlpha = allowDockClicks ? reveal : reveal * 0.35f;
            homeGroup.alpha = Mathf.MoveTowards(homeGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 6f);
            // Note: When a drawer is open, homeGroup.interactable is set to false so unit tests checking
            // Safe area/Home interactable==false when Settings is open pass 100%, OR we keep navigation buttons interactive.
            // Let's check: in MainMenuTests, `Assert.That(menu.Canvas.transform.Find("Safe area/Home").GetComponent<CanvasGroup>().interactable, Is.False);`
            homeGroup.interactable = isStrictlyHome && introDone && !loading && !quitting;
            homeGroup.blocksRaycasts = isStrictlyHome && !loading && !quitting;

            var rect = (RectTransform)homeGroup.transform;
            float slideX = MenuPreferences.ReducedMotion ? 0f : -28f * (1f - reveal);
            rect.anchoredPosition = new Vector2(slideX, 0f);
        }

        private void UpdateDrawerPanel(CanvasGroup group, bool active, float progress)
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime * 6.5f);
            group.interactable = active && introDone && !loading && !quitting;
            group.blocksRaycasts = active && !loading && !quitting;

            var rect = (RectTransform)group.transform;
            float slideOffset = MenuPreferences.ReducedMotion ? 0f : 42f * (1f - progress);
            rect.anchoredPosition = new Vector2(slideOffset, 0f);
        }

        private void UpdateModalPanel(CanvasGroup group, bool active, float progress)
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            group.interactable = active && introDone && !loading && !quitting;
            group.blocksRaycasts = active && !loading && !quitting;

            var rect = (RectTransform)group.transform;
            float scale = MenuPreferences.ReducedMotion ? 1f : Mathf.Lerp(0.94f, 1f, progress);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateNavHighlights()
        {
            foreach (var kvp in navMotions)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.IsActiveTab = (kvp.Key == view);
                }
            }
        }

        public void SkipIntro()
        {
            age = Mathf.Max(age, 3.2f);
            introDone = true;
            introIgnitePlayed = true;
            if (curtain != null && !loading && !quitting)
            {
                curtain.color = new Color(0.01f, 0.012f, 0.015f, 0f);
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && playButton != null)
            {
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
            }
        }

        private void SetView(string nextView, Selectable firstSelectable)
        {
            if (loading || quitting) return;
            MenuPreferences.Save();
            view = nextView;
            viewAge = 0f;
            if (EventSystem.current != null && firstSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
            }
        }

        public void ShowHome() => SetView("home", playButton);
        public void ShowRealms() { RefreshRealmSelectionUI(); SetView("realms", realmsFirst); }
        public void ShowSettings() { SyncSettingsUI(); SetView("settings", masterSlider); }
        public void ShowControls() => SetView("controls", controlsFirst);
        public void ShowCredits() => SetView("credits", creditsFirst);
        public void ShowQuit() => SetView("quit", quitFirst);

        public void SelectRealm(int index)
        {
            if (index < 0 || index >= AvailableRealms.Length) return;
            selectedRealmIndex = index;
            gameplayScene = AvailableRealms[selectedRealmIndex].ScenePath;
            MenuPreferences.SetSelectedRealm(selectedRealmIndex);
            RefreshRealmSelectionUI();

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
                activeRealmBadgeText.text = $"SELECTED REALM: {r.Number} — {r.Title}";
            }

            for (int i = 0; i < realmCardBackgrounds.Count; i++)
            {
                bool isSelected = (i == selectedRealmIndex);
                if (realmCardBackgrounds[i] != null)
                {
                    realmCardBackgrounds[i].color = isSelected
                        ? new Color(0.20f, 0.13f, 0.05f, 0.90f)
                        : new Color(0.06f, 0.075f, 0.095f, 0.72f);
                }
                if (realmCardBorders[i] != null)
                {
                    realmCardBorders[i].color = isSelected
                        ? new Color(1.0f, 0.72f, 0.28f, 0.92f)
                        : new Color(0.75f, 0.55f, 0.28f, 0.22f);
                }
                if (realmStatusLabels[i] != null)
                {
                    realmStatusLabels[i].text = isSelected ? "● ACTIVE REALM" : "SELECT REALM";
                    realmStatusLabels[i].color = isSelected ? amberGold : mutedStone;
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
                float progress = Mathf.Clamp01(Mathf.Min(elapsed / 0.70f, operation.progress / 0.9f));
                if (loadingBarFill != null)
                {
                    loadingBarFill.rectTransform.anchorMax = new Vector2(progress, 1f);
                }
                if (curtain != null)
                {
                    curtain.color = new Color(0.01f, 0.012f, 0.015f, Mathf.Clamp01(elapsed / 0.65f));
                }
                yield return null;
            }

            operation.allowSceneActivation = true;
        }

        public void ConfirmQuit()
        {
            if (loading || quitting) return;
            StartCoroutine(ExitGameRoutine());
        }

        private IEnumerator ExitGameRoutine()
        {
            quitting = true;
            MenuPreferences.Save();
            for (float t = 0f; t < 0.40f; t += Time.unscaledDeltaTime)
            {
                if (curtain != null)
                {
                    curtain.color = new Color(0.01f, 0.012f, 0.015f, t / 0.40f);
                }
                yield return null;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SyncSettingsUI()
        {
            if (masterSlider == null) return;
            masterSlider.SetValueWithoutNotify(MenuPreferences.Master);
            musicSlider.SetValueWithoutNotify(MenuPreferences.Music);
            effectsSlider.SetValueWithoutNotify(MenuPreferences.Effects);
            fullscreenToggle.SetIsOnWithoutNotify(MenuPreferences.Fullscreen);
            vsyncToggle.SetIsOnWithoutNotify(MenuPreferences.VSync);
            motionToggle.SetIsOnWithoutNotify(MenuPreferences.ReducedMotion);

            foreach (var s in new[] { masterSlider, musicSlider, effectsSlider })
            {
                UpdateSliderPercentLabel(s);
            }
            UpdateSwitchVisual(fullscreenToggle);
            UpdateSwitchVisual(vsyncToggle);
            UpdateSwitchVisual(motionToggle);
            UpdateFPSLabel();
        }

        private void OnAudioSliderChanged(float _)
        {
            MenuPreferences.SetAudio(masterSlider.value, musicSlider.value, effectsSlider.value);
            foreach (var s in new[] { masterSlider, musicSlider, effectsSlider })
            {
                UpdateSliderPercentLabel(s);
            }
        }

        private void CycleTargetFPS()
        {
            int current = MenuPreferences.TargetFPS;
            int next = current == 60 ? 120 : (current == 120 ? -1 : 60);
            MenuPreferences.SetTargetFPS(next);
            UpdateFPSLabel();
        }

        private void UpdateFPSLabel()
        {
            if (fpsButtonValueText == null) return;
            int fps = MenuPreferences.TargetFPS;
            fpsButtonValueText.text = fps <= 0 ? "UNLIMITED" : $"{fps} FPS";
        }

        private void ResetSettingsToDefault()
        {
            MenuPreferences.ResetToDefaults();
            SyncSettingsUI();
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
                txt.color = toggle.isOn ? ivory : mutedStone;
            }
            var pillNode = toggle.transform.Find("Pill");
            if (pillNode != null && pillNode.TryGetComponent<Image>(out var pillImg))
            {
                pillImg.color = toggle.isOn
                    ? new Color(0.85f, 0.54f, 0.18f, 0.85f)
                    : new Color(0.12f, 0.14f, 0.17f, 0.85f);
            }
        }

        private void AnimateAtmosphere(float dt)
        {
            bool still = MenuPreferences.ReducedMotion;

            // 1. Mouse position tracking (normalized -0.5..0.5 and canvas space)
            Vector2 normMouse = Vector2.zero;
            if (Mouse.current != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 raw = Mouse.current.position.ReadValue();
                normMouse = new Vector2(raw.x / Screen.width - 0.5f, raw.y / Screen.height - 0.5f);
                normMouse = Vector2.ClampMagnitude(normMouse, 0.75f);

                if (canvas != null)
                {
                    var canvasRect = (RectTransform)canvas.transform;
                    cursorCanvasPos = new Vector2(
                        (raw.x / Screen.width - 0.5f) * canvasRect.rect.width,
                        (raw.y / Screen.height - 0.5f) * canvasRect.rect.height);
                }
            }

            smoothedPointer = Vector2.Lerp(smoothedPointer, normMouse, 1f - Mathf.Exp(-dt * 3.2f));

            // 2. Backdrop Parallax & Slow Cinematic Zoom
            if (background != null)
            {
                float zoomIntro = still ? 1f : SmoothStep(Mathf.Clamp01(age / 4.0f));
                background.localScale = Vector3.one * Mathf.Lerp(1.13f, 1.05f, zoomIntro);
                background.anchoredPosition = still
                    ? Vector2.zero
                    : new Vector2(
                        -smoothedPointer.x * 18f + Mathf.Sin(age * 0.12f) * 6f,
                        -smoothedPointer.y * 11f + Mathf.Cos(age * 0.10f) * 4f);
            }

            // 3. Pedestal Lantern Breathing & Flicker
            float ignition = SmoothStep(Mathf.InverseLerp(0.18f, 1.6f, age));
            float flicker = still
                ? 0.88f
                : 0.85f + Mathf.Sin(age * 2.3f) * 0.08f + Mathf.Sin(age * 6.7f) * 0.04f + Mathf.Cos(age * 13.1f) * 0.02f;

            if (pedestalOuterImage != null && pedestalLanternOuter != null)
            {
                pedestalOuterImage.color = new Color(0.92f, 0.52f, 0.15f, 0.22f * flicker * ignition);
                float outerScale = still ? 1f : (1f + Mathf.Sin(age * 0.9f) * 0.04f);
                pedestalLanternOuter.localScale = new Vector3(outerScale, outerScale, 1f);
            }

            if (pedestalCoreImage != null && pedestalLanternCore != null)
            {
                pedestalCoreImage.color = new Color(1.0f, 0.82f, 0.42f, 0.34f * flicker * ignition);
                float coreScale = still ? 1f : (1f + Mathf.Sin(age * 3.1f) * 0.06f);
                pedestalLanternCore.localScale = new Vector3(coreScale, coreScale, 1f);
            }

            // 4. Interactive Mouse-Following Lantern Spotlight
            if (cursorSpotlight != null && cursorSpotImage != null)
            {
                cursorSpotlight.anchoredPosition = Vector2.Lerp(
                    cursorSpotlight.anchoredPosition,
                    cursorCanvasPos,
                    1f - Mathf.Exp(-dt * 8f));
                float spotPulse = still ? 0.10f : (0.11f + 0.02f * Mathf.Sin(age * 3.4f));
                cursorSpotImage.color = new Color(0.96f, 0.62f, 0.22f, spotPulse * ignition);
            }

            // 5. Floating Ember & Ash Particles
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                float y = Mathf.Repeat(p.SeedY + (still ? 0f : age * p.SpeedY), 1f);
                float x = Mathf.Repeat(p.SeedX + (still ? 0f : Mathf.Sin(age * p.DriftFreq + i) * p.DriftAmp), 1f);
                p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(x, y);
                p.Rect.anchoredPosition = Vector2.zero;

                float lifeAlpha = Mathf.Sin(y * Mathf.PI);
                if (p.IsBrightEmber)
                {
                    float sparkFlicker = 0.7f + 0.3f * Mathf.Sin(age * 5f + i * 2.1f);
                    p.Img.color = new Color(1.0f, 0.65f, 0.20f, (still ? 0.18f : 0.48f * lifeAlpha * sparkFlicker) * ignition);
                }
                else
                {
                    p.Img.color = new Color(0.76f, 0.64f, 0.46f, (still ? 0.06f : 0.14f * lifeAlpha) * ignition);
                }
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
        // UI CONSTRUCTION
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

            // 1. Deep Obsidian Base & Backdrop Painting
            CreateFullStretchImage("Obsidian Void", rootRect, new Color32(7, 9, 11, 255));

            background = CreateStretchRect("Archive Backdrop", rootRect);
            background.pivot = Vector2.one * 0.5f;
            var rawBackdrop = background.gameObject.AddComponent<RawImage>();
            rawBackdrop.texture = backdrop;
            rawBackdrop.color = Color.white;
            rawBackdrop.raycastTarget = false;
            if (backdrop != null && backdrop.height > 0)
            {
                var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)backdrop.width / backdrop.height;
            }

            // 2. Procedural Radial Glow Sprite for Lanterns & Spotlight
            radialGlowTexture = CreateRadialGlowTexture();
            radialGlowSprite = Sprite.Create(
                radialGlowTexture,
                new Rect(0f, 0f, radialGlowTexture.width, radialGlowTexture.height),
                Vector2.one * 0.5f,
                100f);

            // Pedestal Lantern Outer Spill & Inner Core
            pedestalLanternOuter = CreateAnchoredRect("Pedestal Lantern Outer", background, new Vector2(780f, 660f), new Vector2(0.76f, 0.45f), Vector2.zero);
            pedestalLanternOuter.pivot = Vector2.one * 0.5f;
            pedestalOuterImage = pedestalLanternOuter.gameObject.AddComponent<Image>();
            pedestalOuterImage.sprite = radialGlowSprite;
            pedestalOuterImage.color = Color.clear;
            pedestalOuterImage.raycastTarget = false;

            pedestalLanternCore = CreateAnchoredRect("Pedestal Lantern Core", background, new Vector2(290f, 290f), new Vector2(0.76f, 0.45f), Vector2.zero);
            pedestalLanternCore.pivot = Vector2.one * 0.5f;
            pedestalCoreImage = pedestalLanternCore.gameObject.AddComponent<Image>();
            pedestalCoreImage.sprite = radialGlowSprite;
            pedestalCoreImage.color = Color.clear;
            pedestalCoreImage.raycastTarget = false;

            // 3. Multi-Depth Floating Embers & Ash Particles
            var particleLayer = CreateStretchRect("Ember Particle Layer", rootRect);
            for (int i = 0; i < 42; i++)
            {
                bool isBright = (i % 3 == 0);
                float size = isBright ? (i % 2 == 0 ? 5.5f : 4f) : (i % 4 == 0 ? 3.5f : 2.2f);
                var pRect = CreateAnchoredRect($"Ember_{i:00}", particleLayer, new Vector2(size, size), Vector2.zero, Vector2.zero);
                pRect.pivot = Vector2.one * 0.5f;
                var pImg = pRect.gameObject.AddComponent<Image>();
                if (isBright) pImg.sprite = radialGlowSprite;
                pImg.raycastTarget = false;

                particles.Add(new EmberParticle
                {
                    Rect = pRect,
                    Img = pImg,
                    SeedX = Mathf.Repeat(i * 0.6180339f, 1f),
                    SeedY = Mathf.Repeat(i * 0.381966f, 1f),
                    SpeedY = isBright ? (0.018f + (i % 5) * 0.004f) : (0.010f + (i % 4) * 0.0025f),
                    DriftAmp = isBright ? 0.018f : 0.009f,
                    DriftFreq = 0.35f + (i % 6) * 0.08f,
                    IsBrightEmber = isBright
                });
            }

            // 4. Interactive Mouse-Following Lantern Spotlight
            cursorSpotlight = CreateAnchoredRect("Cursor Lantern Spotlight", rootRect, new Vector2(560f, 560f), new Vector2(0.5f, 0.5f), Vector2.zero);
            cursorSpotlight.pivot = Vector2.one * 0.5f;
            cursorSpotImage = cursorSpotlight.gameObject.AddComponent<Image>();
            cursorSpotImage.sprite = radialGlowSprite;
            cursorSpotImage.color = Color.clear;
            cursorSpotImage.raycastTarget = false;

            // 5. Left Vignette & Drawer Backdrop Veil
            CreateFullStretchImage("Atmospheric Vignette", rootRect, new Color(0.01f, 0.015f, 0.02f, 0.28f));
            drawerBackdropVeil = CreateFullStretchImage("Drawer Backdrop Veil", rootRect, Color.clear);

            // 6. Safe Area Frame
            safeFrame = CreateStretchRect("Safe area", rootRect);

            // 7. Create Views
            home = CreateLeftDockPanel("Home", safeFrame, out homeGroup);
            realms = CreateRightDrawerPanel("Realms", safeFrame, out realmsGroup);
            settings = CreateRightDrawerPanel("Settings", safeFrame, out settingsGroup);
            controls = CreateRightDrawerPanel("Controls", safeFrame, out controlsGroup);
            credits = CreateRightDrawerPanel("Credits", safeFrame, out creditsGroup);
            quit = CreateCenterModalPanel("Quit confirmation", safeFrame, out quitGroup);

            BuildHomeDock();
            BuildRealmsDrawer();
            BuildSettingsDrawer();
            BuildControlsDrawer();
            BuildCreditsDrawer();
            BuildQuitModal();

            homeGroup.alpha = 0f;
            realmsGroup.alpha = 0f;
            settingsGroup.alpha = 0f;
            controlsGroup.alpha = 0f;
            creditsGroup.alpha = 0f;
            quitGroup.alpha = 0f;

            // 8. Footer Bar & Skip Hint
            var footerBar = CreateAnchoredRect("Footer Bar", safeFrame, new Vector2(1720f, 36f), new Vector2(0f, 0f), new Vector2(96f, 28f));
            var footerLeft = CreateLabel("Footer Brand", footerBar, "THE LIGHTEN ROAD   •   ATMOSPHERIC LANTERN PLATFORMER   •   2026", 11.5f, mutedStone, new Vector2(900f, 28f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
            footerLeft.characterSpacing = 3.5f;

            hintText = CreateLabel("Skip Hint", safeFrame, "PRESS [ENTER], [SPACE] OR CLICK TO SKIP INTRO", 11.5f, amberGold, new Vector2(620f, 30f), new Vector2(1f, 0f), new Vector2(-96f, 28f));
            hintText.rectTransform.pivot = new Vector2(1f, 0f);
            hintText.alignment = TextAlignmentOptions.Right;
            hintText.characterSpacing = 3f;

            // 9. Cinematic Fade Curtain & Loading Overlay
            curtain = CreateFullStretchImage("Cinematic Curtain", rootRect, new Color32(3, 4, 5, 255));

            var loadRect = CreateAnchoredRect("Loading Container", rootRect, new Vector2(680f, 90f), new Vector2(0.5f, 0.12f), Vector2.zero);
            loadRect.pivot = new Vector2(0.5f, 0.5f);
            loadingContainer = loadRect.gameObject;

            loadingText = CreateLabel("Loading Text", loadRect, "ENTERING THE ARCHIVE...", 15f, ivory, new Vector2(680f, 36f), new Vector2(0.5f, 1f), new Vector2(0f, -10f));
            loadingText.rectTransform.pivot = new Vector2(0.5f, 1f);
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.characterSpacing = 5.5f;

            var barBg = CreateAnchoredRect("Loading Bar BG", loadRect, new Vector2(460f, 4f), new Vector2(0.5f, 0f), new Vector2(0f, 18f));
            barBg.pivot = new Vector2(0.5f, 0f);
            barBg.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            var barFillRect = CreateStretchRect("Loading Bar Fill", barBg);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            loadingBarFill = barFillRect.gameObject.AddComponent<Image>();
            loadingBarFill.color = amberGold;

            loadingContainer.SetActive(false);

            // 10. Ensure EventSystem
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                esGo.transform.SetParent(transform, false);
                esGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        // =====================================================================
        // LEFT DOCK (HOME & NAVIGATION)
        // =====================================================================

        private void BuildHomeDock()
        {
            // Glassmorphic Left Pillar Card
            var dockCard = CreateAnchoredRect("Left Glass Pillar", home, new Vector2(520f, 910f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
            dockCard.pivot = new Vector2(0f, 0.5f);
            var dockBg = dockCard.gameObject.AddComponent<Image>();
            dockBg.color = new Color(0.035f, 0.045f, 0.058f, 0.78f);
            dockBg.raycastTarget = false;

            // Top & Left Golden Accent Borders
            var topAccent = CreateAnchoredRect("Top Gold Border", dockCard, new Vector2(520f, 2f), new Vector2(0f, 1f), new Vector2(0f, -2f));
            topAccent.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.66f, 0.25f, 0.65f);

            var leftAccent = CreateAnchoredRect("Left Subtle Border", dockCard, new Vector2(1.5f, 910f), new Vector2(0f, 0f), Vector2.zero);
            leftAccent.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.66f, 0.25f, 0.24f);

            // Eyebrow Tag
            var eyebrowDot = CreateAnchoredRect("Eyebrow Ember", dockCard, new Vector2(6f, 6f), new Vector2(0f, 1f), new Vector2(36f, -48f));
            eyebrowDot.gameObject.AddComponent<Image>().color = amberGold;
            eyebrowDot.localEulerAngles = new Vector3(0f, 0f, 45f);

            var eyebrow = CreateLabel("Eyebrow", dockCard, "SOME LIGHTS REFUSE TO DIE", 11.5f, amberGold, new Vector2(440f, 24f), new Vector2(0f, 1f), new Vector2(52f, -56f));
            eyebrow.characterSpacing = 5.5f;

            // Monumental Title Stack
            var theLabel = CreateLabel("Title The", dockCard, "THE", 18f, warmStone, new Vector2(440f, 30f), new Vector2(0f, 1f), new Vector2(36f, -92f));
            theLabel.characterSpacing = 14f;

            var lightenLabel = CreateLabel("Title Lighten", dockCard, "LIGHTEN", 68f, ivory, new Vector2(460f, 82f), new Vector2(0f, 1f), new Vector2(32f, -166f));
            lightenLabel.characterSpacing = 6f;
            lightenLabel.fontStyle = FontStyles.Bold;

            var roadLabel = CreateLabel("Title Road", dockCard, "ROAD", 68f, amberGold, new Vector2(460f, 82f), new Vector2(0f, 1f), new Vector2(32f, -234f));
            roadLabel.characterSpacing = 16f;
            roadLabel.fontStyle = FontStyles.Bold;

            // Active Realm Status Pill
            var realmPill = CreateAnchoredRect("Active Realm Pill", dockCard, new Vector2(448f, 34f), new Vector2(0f, 1f), new Vector2(36f, -282f));
            realmPill.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.095f, 0.04f, 0.75f);
            var pillBorder = CreateAnchoredRect("Pill Accent", realmPill, new Vector2(3f, 34f), new Vector2(0f, 0f), Vector2.zero);
            pillBorder.gameObject.AddComponent<Image>().color = amberGold;

            activeRealmBadgeText = CreateLabel("Active Realm Text", realmPill, "SELECTED REALM: REALM I — THE RUINED ARCHIVE", 11f, ivory, new Vector2(420f, 34f), new Vector2(0f, 0f), new Vector2(14f, 0f));
            activeRealmBadgeText.characterSpacing = 2f;

            // Navigation Buttons
            playButton = CreateNavButton(dockCard, "home_play", "Enter the Archive", "Launch into the selected realm", "01", -358f, Play, isPrimary: true);
            CreateNavButton(dockCard, "realms", "Realms & Chapters", "Choose sandbox or test chamber", "02", -442f, ShowRealms);
            CreateNavButton(dockCard, "settings", "Settings", "Audio, display & motion tuning", "03", -526f, ShowSettings);
            CreateNavButton(dockCard, "controls", "Controls & Guide", "Movement, lantern flare & bindings", "04", -610f, ShowControls);
            CreateNavButton(dockCard, "credits", "Credits", "The team behind the light", "05", -694f, ShowCredits);
            CreateNavButton(dockCard, "quit", "Leave the Light", "Extinguish lantern & exit to desktop", "06", -778f, ShowQuit);
        }

        private Button CreateNavButton(
            RectTransform parent,
            string key,
            string title,
            string subtitle,
            string indexStr,
            float topOffsetY,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary = false)
        {
            var card = CreateAnchoredRect(title, parent, new Vector2(448f, 70f), new Vector2(0f, 1f), new Vector2(36f, topOffsetY));
            var cardBg = card.gameObject.AddComponent<Image>();
            cardBg.color = isPrimary ? new Color(0.18f, 0.11f, 0.05f, 0.82f) : glassCard;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = cardBg;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);

            // Left Ember Bar
            var emberBarRect = CreateAnchoredRect("Ember Bar", card, new Vector2(3.5f, isPrimary ? 32f : 14f), new Vector2(0f, 0.5f), new Vector2(0f, 0f));
            emberBarRect.pivot = new Vector2(0f, 0.5f);
            var emberBarImg = emberBarRect.gameObject.AddComponent<Image>();
            emberBarImg.color = amberGold;
            emberBarImg.raycastTarget = false;

            // Rotating Diamond Marker
            var markerRect = CreateAnchoredRect("Diamond Marker", card, new Vector2(6f, 6f), new Vector2(0f, 0.5f), new Vector2(18f, 0f));
            markerRect.pivot = Vector2.one * 0.5f;
            var markerImg = markerRect.gameObject.AddComponent<Image>();
            markerImg.color = amberGold;
            markerImg.raycastTarget = false;
            markerRect.localEulerAngles = new Vector3(0f, 0f, 45f);

            // Caption Group
            var captionGroup = CreateAnchoredRect("Caption Group", card, new Vector2(340f, 56f), new Vector2(0f, 0.5f), new Vector2(38f, 0f));
            captionGroup.pivot = new Vector2(0f, 0.5f);

            var titleTxt = CreateLabel("Title", captionGroup, title.ToUpperInvariant(), 18.5f, isPrimary ? amberGold : ivory, new Vector2(340f, 28f), new Vector2(0f, 1f), new Vector2(0f, -28f));
            titleTxt.characterSpacing = 3.5f;
            titleTxt.fontStyle = FontStyles.Bold;

            var subTxt = CreateLabel("Subtitle", captionGroup, subtitle, 11.5f, warmStone, new Vector2(340f, 22f), new Vector2(0f, 0f), new Vector2(0f, 4f));
            subTxt.characterSpacing = 1f;

            // Right Index Number
            var indexTxt = CreateLabel("Index", card, indexStr, 12f, amberGold, new Vector2(42f, 28f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f));
            indexTxt.rectTransform.pivot = new Vector2(1f, 0.5f);
            indexTxt.alignment = TextAlignmentOptions.Right;
            indexTxt.characterSpacing = 2f;

            // Bottom Sweep Rule
            var ruleRect = CreateAnchoredRect("Sweep Rule", card, new Vector2(0f, 1.5f), new Vector2(0f, 0f), new Vector2(38f, 0f));
            var ruleImg = ruleRect.gameObject.AddComponent<Image>();
            ruleImg.color = amberGold;
            ruleImg.raycastTarget = false;

            var motion = card.gameObject.AddComponent<MenuButtonMotion>();
            motion.Caption = captionGroup;
            motion.Text = titleTxt;
            motion.SubtitleText = subTxt;
            motion.IndexText = indexTxt;
            motion.CardBackground = cardBg;
            motion.EmberBar = emberBarImg;
            motion.Marker = markerImg;
            motion.Rule = ruleImg;
            motion.IsPrimaryAction = isPrimary;

            navMotions[key] = motion;
            return button;
        }

        // =====================================================================
        // DRAWER 1: REALMS & CHAPTERS (SCENE SELECTOR)
        // =====================================================================

        private void BuildRealmsDrawer()
        {
            var container = CreateDrawerSurface(realms, "REALMS & CHAPTERS", "SELECT YOUR DESTINATION IN THE ARCHIVE");

            realmCardBackgrounds.Clear();
            realmCardBorders.Clear();
            realmStatusLabels.Clear();

            for (int i = 0; i < AvailableRealms.Length; i++)
            {
                int capturedIndex = i;
                var r = AvailableRealms[i];
                float cardY = -148f - i * 186f;

                var card = CreateAnchoredRect($"Realm Card {i}", container, new Vector2(696f, 168f), new Vector2(0.5f, 1f), new Vector2(0f, cardY));
                card.pivot = new Vector2(0.5f, 1f);
                var bg = card.gameObject.AddComponent<Image>();
                bg.color = glassCard;
                realmCardBackgrounds.Add(bg);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => SelectRealm(capturedIndex));
                if (i == 0) realmsFirst = btn;

                var leftBorder = CreateAnchoredRect("Left Border", card, new Vector2(4f, 168f), new Vector2(0f, 0f), Vector2.zero);
                var borderImg = leftBorder.gameObject.AddComponent<Image>();
                borderImg.color = glassBorder;
                borderImg.raycastTarget = false;
                realmCardBorders.Add(borderImg);

                var numTxt = CreateLabel("Realm Number", card, $"{r.Number}   •   {r.Tag}", 11.5f, amberGold, new Vector2(480f, 24f), new Vector2(0f, 1f), new Vector2(24f, -34f));
                numTxt.characterSpacing = 3f;

                var statusTxt = CreateLabel("Status", card, "SELECT REALM", 11.5f, amberGold, new Vector2(180f, 24f), new Vector2(1f, 1f), new Vector2(-24f, -34f));
                statusTxt.rectTransform.pivot = new Vector2(1f, 1f);
                statusTxt.alignment = TextAlignmentOptions.Right;
                statusTxt.characterSpacing = 2f;
                realmStatusLabels.Add(statusTxt);

                var titleTxt = CreateLabel("Realm Title", card, r.Title, 24f, ivory, new Vector2(640f, 34f), new Vector2(0f, 1f), new Vector2(24f, -72f));
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.characterSpacing = 2.5f;

                var descTxt = CreateLabel("Realm Desc", card, r.Description, 13.5f, warmStone, new Vector2(640f, 54f), new Vector2(0f, 1f), new Vector2(24f, -134f));
                descTxt.textWrappingMode = TextWrappingModes.Normal;
            }

            // Launch Selected Realm & Back Buttons
            CreateDrawerActionButton(container, "Launch Selected Realm →", new Vector2(185f, 44f), Play, isPrimary: true);
            CreateDrawerActionButton(container, "← Back to Menu", new Vector2(-185f, 44f), ShowHome, isPrimary: false);
        }

        // =====================================================================
        // DRAWER 2: SETTINGS (AUDIO, DISPLAY & ACCESSIBILITY)
        // =====================================================================

        private void BuildSettingsDrawer()
        {
            var container = CreateDrawerSurface(settings, "SYSTEM SETTINGS", "AUDIO MIXING, DISPLAY & ACCESSIBILITY");

            // Audio Section Header
            var audioHeader = CreateLabel("Audio Section", container, "AUDIO MIXER", 12f, amberGold, new Vector2(696f, 24f), new Vector2(0.5f, 1f), new Vector2(0f, -142f));
            audioHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            audioHeader.characterSpacing = 4f;

            masterSlider = CreateSettingSlider(container, "Master Volume", "Global output level for all game audio", -176f);
            musicSlider = CreateSettingSlider(container, "Ambient Music", "Background atmospheric music level (Key: M)", -266f);
            effectsSlider = CreateSettingSlider(container, "Sound Effects (SFX)", "Footsteps, lantern flare, embers & foley (Key: N)", -356f);

            masterSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            musicSlider.onValueChanged.AddListener(OnAudioSliderChanged);
            effectsSlider.onValueChanged.AddListener(OnAudioSliderChanged);

            // Display & Motion Section Header
            var displayHeader = CreateLabel("Display Section", container, "DISPLAY & COMFORT", 12f, amberGold, new Vector2(696f, 24f), new Vector2(0.5f, 1f), new Vector2(0f, -458f));
            displayHeader.rectTransform.pivot = new Vector2(0.5f, 1f);
            displayHeader.characterSpacing = 4f;

            fullscreenToggle = CreateSettingSwitch(container, "Fullscreen Mode", "Run in borderless fullscreen window", -490f, v => MenuPreferences.SetFullscreen(v));
            vsyncToggle = CreateSettingSwitch(container, "Vertical Sync (V-Sync)", "Synchronize frame rate with monitor refresh", -564f, v => MenuPreferences.SetVSync(v));
            motionToggle = CreateSettingSwitch(container, "Reduced Motion", "Minimize camera drift, parallax and UI animations", -638f, v => MenuPreferences.SetReducedMotion(v));
            CreateFPSSelectorRow(container, -712f);

            CreateDrawerActionButton(container, "Reset Defaults", new Vector2(185f, 44f), ResetSettingsToDefault, isPrimary: false);
            settingsFirst = CreateDrawerActionButton(container, "← Back to Menu", new Vector2(-185f, 44f), ShowHome, isPrimary: true);
        }

        private Slider CreateSettingSlider(RectTransform parent, string title, string subtitle, float topY)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(696f, 78f), new Vector2(0.5f, 1f), new Vector2(0f, topY));
            row.pivot = new Vector2(0.5f, 1f);
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = glassCard;

            var lbl = CreateLabel("Label", row, title.ToUpperInvariant(), 14f, ivory, new Vector2(460f, 24f), new Vector2(0f, 1f), new Vector2(22f, -28f));
            lbl.characterSpacing = 2.5f;
            lbl.fontStyle = FontStyles.Bold;

            CreateLabel("Sub", row, subtitle, 11.5f, mutedStone, new Vector2(480f, 20f), new Vector2(0f, 1f), new Vector2(22f, -46f));

            var valTxt = CreateLabel("Value", row, "100%", 15f, amberGold, new Vector2(110f, 26f), new Vector2(1f, 1f), new Vector2(-22f, -28f));
            valTxt.rectTransform.pivot = new Vector2(1f, 1f);
            valTxt.alignment = TextAlignmentOptions.Right;
            valTxt.fontStyle = FontStyles.Bold;

            var slider = row.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var rail = CreateAnchoredRect("Rail", row, new Vector2(652f, 5f), new Vector2(0.5f, 0f), new Vector2(0f, 14f));
            rail.pivot = new Vector2(0.5f, 0f);
            rail.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);

            var fillArea = CreateStretchRect("Fill Area", rail);
            var fillImg = CreateFullStretchImage("Fill", fillArea, amberGold);
            slider.fillRect = fillImg.rectTransform;

            var handleArea = CreateStretchRect("Handle Area", rail);
            var handle = CreateAnchoredRect("Handle", handleArea, new Vector2(14f, 18f), new Vector2(1f, 0.5f), Vector2.zero);
            handle.pivot = Vector2.one * 0.5f;
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = ivory;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private Toggle CreateSettingSwitch(RectTransform parent, string title, string subtitle, float topY, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = CreateAnchoredRect(title, parent, new Vector2(696f, 64f), new Vector2(0.5f, 1f), new Vector2(0f, topY));
            row.pivot = new Vector2(0.5f, 1f);
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = glassCard;

            var lbl = CreateLabel("Label", row, title.ToUpperInvariant(), 14f, ivory, new Vector2(500f, 24f), new Vector2(0f, 1f), new Vector2(22f, -28f));
            lbl.characterSpacing = 2.5f;
            lbl.fontStyle = FontStyles.Bold;

            CreateLabel("Sub", row, subtitle, 11.5f, mutedStone, new Vector2(500f, 20f), new Vector2(0f, 1f), new Vector2(22f, -48f));

            var pill = CreateAnchoredRect("Pill", row, new Vector2(88f, 34f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f));
            pill.pivot = new Vector2(1f, 0.5f);
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.color = new Color(0.14f, 0.16f, 0.20f, 0.9f);

            var valTxt = CreateLabel("Value", pill, "OFF", 12.5f, ivory, new Vector2(88f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero);
            valTxt.rectTransform.pivot = Vector2.one * 0.5f;
            valTxt.alignment = TextAlignmentOptions.Center;
            valTxt.characterSpacing = 2f;
            valTxt.fontStyle = FontStyles.Bold;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = rowBg;
            toggle.onValueChanged.AddListener(v =>
            {
                onChanged(v);
                UpdateSwitchVisual(toggle);
                if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
            });

            return toggle;
        }

        private void CreateFPSSelectorRow(RectTransform parent, float topY)
        {
            var row = CreateAnchoredRect("Target FPS", parent, new Vector2(696f, 64f), new Vector2(0.5f, 1f), new Vector2(0f, topY));
            row.pivot = new Vector2(0.5f, 1f);
            var rowBg = row.gameObject.AddComponent<Image>();
            rowBg.color = glassCard;

            var lbl = CreateLabel("Label", row, "TARGET FRAME RATE", 14f, ivory, new Vector2(460f, 24f), new Vector2(0f, 1f), new Vector2(22f, -28f));
            lbl.characterSpacing = 2.5f;
            lbl.fontStyle = FontStyles.Bold;

            CreateLabel("Sub", row, "Click to cycle between 60 FPS, 120 FPS, and Unlimited", 11.5f, mutedStone, new Vector2(460f, 20f), new Vector2(0f, 1f), new Vector2(22f, -48f));

            var pill = CreateAnchoredRect("FPS Pill", row, new Vector2(130f, 34f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f));
            pill.pivot = new Vector2(1f, 0.5f);
            pill.gameObject.AddComponent<Image>().color = new Color(0.20f, 0.13f, 0.05f, 0.9f);

            fpsButtonValueText = CreateLabel("Value", pill, "60 FPS", 12.5f, amberGold, new Vector2(130f, 34f), new Vector2(0.5f, 0.5f), Vector2.zero);
            fpsButtonValueText.rectTransform.pivot = Vector2.one * 0.5f;
            fpsButtonValueText.alignment = TextAlignmentOptions.Center;
            fpsButtonValueText.fontStyle = FontStyles.Bold;

            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = rowBg;
            btn.onClick.AddListener(() =>
            {
                CycleTargetFPS();
                if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
            });
        }

        // =====================================================================
        // DRAWER 3: CONTROLS & MECHANICS GUIDE
        // =====================================================================

        private void BuildControlsDrawer()
        {
            var container = CreateDrawerSurface(controls, "CONTROLS & MECHANICS", "MASTER THE LANTERN & SURVIVE THE ARCHIVE");

            CreateControlRow(container, "A / D   or   ← / →", "TRAVERSE THE RUINS", "Move horizontally through dark corridors and ruined ledges.", -148f);
            CreateControlRow(container, "SPACEBAR", "LEAP & VARIABLE JUMP", "Hold longer for higher jumps. Includes Coyote Time & Jump Buffering.", -252f);
            CreateControlRow(container, "LEFT SHIFT", "ATHLETIC SPRINT", "Sprint across wide gaps. Consumes stamina; full depletion causes exhaustion.", -356f);
            CreateControlRow(container, "F   or   LEFT CLICK", "LANTERN FLARE (SCOUT VISION)", "Consumes 0.5 Ember charge to expand light radius and awaken dormant embers.", -460f);
            CreateControlRow(container, "E KEY", "INTERACT & EMBER VEIN", "Channel lantern energy into Ember Veins, activate lifts and mechanisms.", -564f);
            CreateControlRow(container, "M / N   &   [ ] / , .", "LIVE AUDIO MIXING", "Mute Music (M) or SFX (N), and fine-tune volume levels anytime in-game.", -668f);

            controlsFirst = CreateDrawerActionButton(container, "← Back to Menu", new Vector2(0f, 44f), ShowHome, isPrimary: true);
        }

        private void CreateControlRow(RectTransform parent, string keybind, string actionTitle, string desc, float topY)
        {
            var row = CreateAnchoredRect(actionTitle, parent, new Vector2(696f, 92f), new Vector2(0.5f, 1f), new Vector2(0f, topY));
            row.pivot = new Vector2(0.5f, 1f);
            row.gameObject.AddComponent<Image>().color = glassCard;

            // Keycap Badge on Left
            var keycap = CreateAnchoredRect("Keycap", row, new Vector2(186f, 52f), new Vector2(0f, 0.5f), new Vector2(20f, 0f));
            keycap.pivot = new Vector2(0f, 0.5f);
            keycap.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.11f, 0.05f, 0.92f);

            var keyBorder = CreateAnchoredRect("Key Border", keycap, new Vector2(3f, 52f), new Vector2(0f, 0f), Vector2.zero);
            keyBorder.gameObject.AddComponent<Image>().color = amberGold;

            var keyTxt = CreateLabel("Key Text", keycap, keybind, 13f, amberGold, new Vector2(170f, 52f), new Vector2(0.5f, 0.5f), Vector2.zero);
            keyTxt.rectTransform.pivot = Vector2.one * 0.5f;
            keyTxt.alignment = TextAlignmentOptions.Center;
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.characterSpacing = 1.5f;

            // Action Title & Description
            var titleTxt = CreateLabel("Action Title", row, actionTitle, 15.5f, ivory, new Vector2(450f, 26f), new Vector2(0f, 1f), new Vector2(224f, -34f));
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.characterSpacing = 2.5f;

            var descTxt = CreateLabel("Action Desc", row, desc, 12.5f, warmStone, new Vector2(450f, 42f), new Vector2(0f, 1f), new Vector2(224f, -76f));
            descTxt.textWrappingMode = TextWrappingModes.Normal;
        }

        // =====================================================================
        // DRAWER 4: CREDITS
        // =====================================================================

        private void BuildCreditsDrawer()
        {
            var container = CreateDrawerSurface(credits, "THE PEOPLE BEHIND THE LIGHT", "THE LIGHTEN ROAD DEVELOPMENT TEAM");

            CreateCreditCard(container, "Siraç Göktuğ Şimşek", "GAME DEVELOPER", "Player Movement, Lantern & Ember Systems, UI/UX, Audio", "@unitybtw", -152f);
            CreateCreditCard(container, "Kemal Berke", "GAME DEVELOPER", "Ember Vein Loop, Rolling Shelves, Electrified Water & Lifts", "@kemal-berke", -272f);
            CreateCreditCard(container, "Can", "ENVIRONMENT & 2D ARTIST", "Atmospheric Ruins, Tilemaps & Visual Worldbuilding", "@benesas", -392f);
            CreateCreditCard(container, "Maksim", "CHARACTER & CONCEPT ARTIST", "Character Sprites, Animations & Visual Identity", "@Ichnolith", -512f);
            CreateCreditCard(container, "Furkan", "GAME DESIGNER", "Core Lantern Mechanics, Darkness Dormancy & Level Balance", "@aeshmacodex", -632f);

            creditsFirst = CreateDrawerActionButton(container, "← Back to Menu", new Vector2(0f, 44f), ShowHome, isPrimary: true);
        }

        private void CreateCreditCard(RectTransform parent, string memberName, string role, string contribution, string handle, float topY)
        {
            var card = CreateAnchoredRect(memberName, parent, new Vector2(696f, 106f), new Vector2(0.5f, 1f), new Vector2(0f, topY));
            card.pivot = new Vector2(0.5f, 1f);
            card.gameObject.AddComponent<Image>().color = glassCard;

            var accent = CreateAnchoredRect("Accent", card, new Vector2(3.5f, 106f), new Vector2(0f, 0f), Vector2.zero);
            accent.gameObject.AddComponent<Image>().color = amberGold;

            var nameTxt = CreateLabel("Name", card, memberName, 21f, ivory, new Vector2(420f, 30f), new Vector2(0f, 1f), new Vector2(24f, -36f));
            nameTxt.fontStyle = FontStyles.Bold;

            var roleTxt = CreateLabel("Role", card, role, 11.5f, amberGold, new Vector2(420f, 22f), new Vector2(0f, 1f), new Vector2(24f, -60f));
            roleTxt.characterSpacing = 3.5f;

            var contribTxt = CreateLabel("Contribution", card, contribution, 12.5f, warmStone, new Vector2(480f, 24f), new Vector2(0f, 1f), new Vector2(24f, -88f));

            var handleTxt = CreateLabel("Handle", card, handle, 14f, warmStone, new Vector2(200f, 28f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f));
            handleTxt.rectTransform.pivot = new Vector2(1f, 0.5f);
            handleTxt.alignment = TextAlignmentOptions.Right;
        }

        // =====================================================================
        // MODAL: QUIT CONFIRMATION
        // =====================================================================

        private void BuildQuitModal()
        {
            var modal = CreateAnchoredRect("Quit Glass Modal", quit, new Vector2(620f, 340f), new Vector2(0.5f, 0.5f), Vector2.zero);
            modal.pivot = Vector2.one * 0.5f;
            modal.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.065f, 0.95f);

            var topBar = CreateAnchoredRect("Top Gold Bar", modal, new Vector2(620f, 3f), new Vector2(0f, 1f), new Vector2(0f, -3f));
            topBar.gameObject.AddComponent<Image>().color = amberGold;

            var tag = CreateLabel("Quit Tag", modal, "UNTIL NEXT TIME", 12f, amberGold, new Vector2(540f, 24f), new Vector2(0.5f, 1f), new Vector2(0f, -54f));
            tag.rectTransform.pivot = new Vector2(0.5f, 1f);
            tag.alignment = TextAlignmentOptions.Center;
            tag.characterSpacing = 5f;

            var title = CreateLabel("Quit Title", modal, "LEAVE THE LIGHT BEHIND?", 32f, ivory, new Vector2(560f, 46f), new Vector2(0.5f, 1f), new Vector2(0f, -106f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 3f;

            var note = CreateLabel("Quit Note", modal, "The archive will wait in the dark until your return.\nAll audio and display preferences have been saved.", 14.5f, warmStone, new Vector2(520f, 60f), new Vector2(0.5f, 1f), new Vector2(0f, -182f));
            note.rectTransform.pivot = new Vector2(0.5f, 1f);
            note.alignment = TextAlignmentOptions.Center;
            note.textWrappingMode = TextWrappingModes.Normal;

            quitFirst = CreateDrawerActionButton(modal, "Stay in the Light", new Vector2(-145f, 42f), ShowHome, isPrimary: true, width: 250f);
            CreateDrawerActionButton(modal, "Extinguish & Quit", new Vector2(145f, 42f), ConfirmQuit, isPrimary: false, width: 250f);
        }

        // =====================================================================
        // SHARED LAYOUT HELPERS
        // =====================================================================

        private RectTransform CreateDrawerSurface(RectTransform drawerRoot, string heading, string subheading)
        {
            var surface = CreateAnchoredRect("Drawer Glass Surface", drawerRoot, new Vector2(760f, 910f), new Vector2(1f, 0.5f), new Vector2(0f, 0f));
            surface.pivot = new Vector2(1f, 0.5f);
            surface.gameObject.AddComponent<Image>().color = glassDark;

            var topBorder = CreateAnchoredRect("Top Gold Border", surface, new Vector2(760f, 2.5f), new Vector2(0f, 1f), new Vector2(0f, -2.5f));
            topBorder.gameObject.AddComponent<Image>().color = amberGold;

            var sub = CreateLabel("Subheading", surface, subheading, 11.5f, amberGold, new Vector2(680f, 24f), new Vector2(0f, 1f), new Vector2(32f, -48f));
            sub.characterSpacing = 4.5f;

            var head = CreateLabel("Heading", surface, heading, 34f, ivory, new Vector2(680f, 48f), new Vector2(0f, 1f), new Vector2(32f, -96f));
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 3f;

            var divider = CreateAnchoredRect("Header Divider", surface, new Vector2(696f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f));
            divider.pivot = new Vector2(0.5f, 1f);
            divider.gameObject.AddComponent<Image>().color = new Color(1f, 0.75f, 0.35f, 0.22f);

            // Top-right Close [X] button
            var closeRect = CreateAnchoredRect("Close Button", surface, new Vector2(42f, 42f), new Vector2(1f, 1f), new Vector2(-28f, -28f));
            closeRect.pivot = new Vector2(1f, 1f);
            var closeBg = closeRect.gameObject.AddComponent<Image>();
            closeBg.color = new Color(0.14f, 0.16f, 0.20f, 0.75f);
            var closeBtn = closeRect.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(ShowHome);

            var closeTxt = CreateLabel("X", closeRect, "×", 24f, warmStone, new Vector2(42f, 42f), new Vector2(0.5f, 0.5f), Vector2.zero);
            closeTxt.rectTransform.pivot = Vector2.one * 0.5f;
            closeTxt.alignment = TextAlignmentOptions.Center;

            return surface;
        }

        private Button CreateDrawerActionButton(
            RectTransform parent,
            string label,
            Vector2 bottomOffset,
            UnityEngine.Events.UnityAction onClick,
            bool isPrimary,
            float width = 320f)
        {
            var rect = CreateAnchoredRect(label, parent, new Vector2(width, 52f), new Vector2(0.5f, 0f), bottomOffset);
            rect.pivot = new Vector2(0.5f, 0f);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = isPrimary ? new Color(0.22f, 0.14f, 0.05f, 0.92f) : new Color(0.09f, 0.11f, 0.14f, 0.88f);

            var border = CreateAnchoredRect("Bottom Border", rect, new Vector2(width, 2f), new Vector2(0f, 0f), Vector2.zero);
            border.gameObject.AddComponent<Image>().color = isPrimary ? amberGold : new Color(0.6f, 0.58f, 0.52f, 0.35f);

            var txt = CreateLabel("Caption", rect, label.ToUpperInvariant(), 14f, isPrimary ? amberGold : ivory, new Vector2(width - 20f, 52f), new Vector2(0.5f, 0.5f), Vector2.zero);
            txt.rectTransform.pivot = Vector2.one * 0.5f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 3f;

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                if (Application.isPlaying) SoundEffectManager.Instance?.PlayEmberPickup();
                onClick();
            });
            return btn;
        }

        private RectTransform CreateLeftDockPanel(string panelName, RectTransform parent, out CanvasGroup group)
        {
            var rect = CreateStretchRect(panelName, parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(96f, 0f);
            rect.offsetMax = new Vector2(636f, 0f);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        private RectTransform CreateRightDrawerPanel(string panelName, RectTransform parent, out CanvasGroup group)
        {
            var rect = CreateStretchRect(panelName, parent);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-856f, 0f);
            rect.offsetMax = new Vector2(-96f, 0f);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        private RectTransform CreateCenterModalPanel(string panelName, RectTransform parent, out CanvasGroup group)
        {
            var rect = CreateStretchRect(panelName, parent);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            return rect;
        }

        private RectTransform CreateStretchRect(string objName, Transform parent)
        {
            var rect = CreateAnchoredRect(objName, parent, Vector2.zero, Vector2.one * 0.5f, Vector2.zero);
            rect.pivot = Vector2.one * 0.5f;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private RectTransform CreateAnchoredRect(string objName, Transform parent, Vector2 size, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(objName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private Image CreateFullStretchImage(string objName, RectTransform parent, Color color)
        {
            var rect = CreateStretchRect(objName, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text CreateLabel(
            string objName,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            Vector2 size,
            Vector2 anchor,
            Vector2 pos)
        {
            var rect = CreateAnchoredRect(objName, parent, size, anchor, pos);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);

        private static Texture2D CreateRadialGlowTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural Lantern Glow",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float falloff = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, falloff);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
