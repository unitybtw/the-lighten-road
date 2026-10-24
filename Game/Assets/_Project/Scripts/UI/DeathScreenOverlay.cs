using System;
using TheLightenRoad.Audio;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Atmospheric cinematic death and respawn overlay adhering to game-ui-design & design-ui-ux-game principles.
    /// Features:
    /// - Deep creeping void vignette and midnight blackout curtain
    /// - Procedurally drifting glowing ember / cinder sparks drifting upward (Hollow Knight / Souls aesthetic)
    /// - Dignified TextMeshPro typography using ArchiveFont (minimum 14px legible body text)
    /// - Ornate filigree divider in warm amber gold
    /// - Contextual philosophical lore text based on death reason
    /// - Two-button controller-first action menu: [REIGNITE FLAME] and [RETURN TO ARCHIVE]
    /// - Full D-Pad, Arrow, W/S, Enter, Space, and mouse navigation with audio feedback
    /// - Sleek ember-pill auto-respawn progress countdown bar (pauses when player interacts to respect agency)
    /// - Instant [SPACE] / [R] skip to reignite immediately
    /// - Audio cues: flame extinguish on death, burst of flame kindle on respawn
    /// - Radiant warm lantern re-ignition flash upon reviving
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";

        [Header("Target Controller (auto-found if empty)")]
        [SerializeField] private PlayerRespawnController respawnController;

        [Header("Typography Font")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Fade Speeds")]
        [SerializeField, Min(0.1f)] private float deathFadeSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float respawnFadeSpeed = 3.2f;

        [Header("Palette")]
        [SerializeField] private Color blackoutColor = new Color32(4, 6, 10, 245);
        [SerializeField] private Color headerColor = new Color32(248, 232, 212, 255);
        [SerializeField] private Color subtitleColor = new Color32(195, 184, 172, 255);
        [SerializeField] private Color amberGold = new Color(1.0f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color moonlightSilver = new Color(0.70f, 0.80f, 0.94f, 1f);
        [SerializeField] private Color progressFillColor = new Color32(235, 155, 55, 255);
        [SerializeField] private Color ignitionColor = new Color32(255, 220, 130, 255);

        // Runtime hierarchy
        private Canvas canvas;
        private Image blackout;
        private Image vignette;
        private Image icon;
        private Image iconHalo;
        private Image ignitionFlash;
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI dividerText;
        private TextMeshProUGUI subtitleText;
        private Image progressBarBorder;
        private Image progressBarFill;
        private Image progressBarGlow;
        private TextMeshProUGUI hintText;
        private RectTransform centerGroup;

        // Two-Button Controller Navigation Menu
        private int selectedButtonIndex = 0; // 0 = Reignite, 1 = Return to Title
        private Button reigniteButton;
        private Image reignitePedestal;
        private Image reigniteBorder;
        private Image reigniteAura;
        private TextMeshProUGUI reigniteText;

        private Button returnButton;
        private Image returnPedestal;
        private Image returnBorder;
        private Image returnAura;
        private TextMeshProUGUI returnText;

        // Floating Cinder Sparks
        private const int CinderCount = 20;
        private readonly Image[] cinderImages = new Image[CinderCount];
        private readonly RectTransform[] cinderRects = new RectTransform[CinderCount];
        private readonly Vector2[] cinderVelocities = new Vector2[CinderCount];
        private readonly float[] cinderPhase = new float[CinderCount];
        private readonly float[] cinderBaseAlpha = new float[CinderCount];

        // Animation state
        private float currentFadeAlpha;
        private float targetFadeAlpha;
        private float ignitionAlpha;
        private bool isSubscribed;
        private bool hasPlayedExtinguishSFX;
        private float buttonPulse;
        private bool hasUserInteracted;

        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => currentFadeAlpha > 0.01f || (canvas != null && canvas.enabled);
        public int SelectedButtonIndex => selectedButtonIndex;

        private void Awake()
        {
            EnsureFontLoaded();
            BuildHierarchy();
        }

        private void Start()
        {
            if (respawnController == null)
                respawnController = FindAnyObjectByType<PlayerRespawnController>();

            TrySubscribe();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(PlayerRespawnController controller)
        {
            Unsubscribe();
            respawnController = controller;
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (respawnController == null || isSubscribed)
                return;

            respawnController.OnPlayerDied += HandlePlayerDied;
            respawnController.OnPlayerRespawned += HandlePlayerRespawned;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || respawnController == null)
            {
                isSubscribed = false;
                return;
            }

            respawnController.OnPlayerDied -= HandlePlayerDied;
            respawnController.OnPlayerRespawned -= HandlePlayerRespawned;
            isSubscribed = false;
        }

        private void EnsureFontLoaded()
        {
            if (fontAsset != null) return;

#if UNITY_EDITOR
            fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset");
#endif
            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("UI/MenuType");
            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("UI/EmberHUD");
            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;
        }

        private void HandlePlayerDied(DeathReason reason)
        {
            targetFadeAlpha = 1f;
            hasPlayedExtinguishSFX = false;
            selectedButtonIndex = 0; // Default to Reignite
            hasUserInteracted = false;

            if (respawnController != null)
            {
                respawnController.AllowManualRespawnInput = false;
            }

            switch (reason)
            {
                case DeathReason.Darkness:
                    icon.sprite = PixelSprites.ExtinguishedFlame;
                    headerText.text = "THE LIGHT HAS FADED";
                    subtitleText.text = "Your lantern flickered and died. The silent archive claimed another spark.";
                    break;
                case DeathReason.Void:
                    icon.sprite = PixelSprites.SkullIcon;
                    headerText.text = "LOST TO THE ABYSS";
                    subtitleText.text = "Swallowed by the bottomless chasms of the forgotten ruins.";
                    break;
                case DeathReason.Hazard:
                    icon.sprite = PixelSprites.EmberCracks;
                    headerText.text = "FLAME EXTINGUISHED";
                    subtitleText.text = "The ancient ruins offered no sanctuary to fragile warmth.";
                    break;
            }

            // Scatter cinder particles across screen bottom
            for (int i = 0; i < CinderCount; i++)
            {
                float x = UnityEngine.Random.Range(-310f, 310f);
                float y = UnityEngine.Random.Range(-180f, -50f);
                if (cinderRects[i] != null)
                    cinderRects[i].anchoredPosition = new Vector2(x, y);
            }

            if (SoundEffectManager.HasInstance && !hasPlayedExtinguishSFX)
            {
                SoundEffectManager.Instance.PlayFlareExtinguish();
                hasPlayedExtinguishSFX = true;
            }

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;
        }

        private void HandlePlayerRespawned()
        {
            targetFadeAlpha = 0f;
            ignitionAlpha = 1f;

            if (respawnController != null)
            {
                respawnController.AllowManualRespawnInput = true;
            }

            if (SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayFlareIgnite();
            }
        }

        private void Update()
        {
            if (respawnController == null)
            {
                respawnController = FindAnyObjectByType<PlayerRespawnController>();
                TrySubscribe();
            }

            // Handle Controller / Keyboard Menu Navigation when Dead
            if (currentFadeAlpha > 0.35f && respawnController != null && respawnController.State != RespawnState.Alive)
            {
                HandleMenuInput();
            }

            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        private void HandleMenuInput()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;

            // Direct instant restart shortcut (Space / R)
            if ((k != null && (k.rKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) && selectedButtonIndex == 0)
            {
                TriggerReignite();
                return;
            }

            // Vertical Navigation: W/S, Up/Down, Gamepad D-pad/Left Stick
            bool navDown = (k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
                        || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame));
            bool navUp = (k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
                      || (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame));

            if (navDown && selectedButtonIndex != 1)
            {
                hasUserInteracted = true;
                SetSelectedButton(1);
            }
            else if (navUp && selectedButtonIndex != 0)
            {
                hasUserInteracted = true;
                SetSelectedButton(0);
            }

            // Confirm Selection (Enter / Space / Gamepad South)
            bool confirmPressed = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                               || (pad != null && pad.buttonSouth.wasPressedThisFrame);

            if (confirmPressed)
            {
                if (selectedButtonIndex == 0)
                {
                    TriggerReignite();
                }
                else
                {
                    TriggerReturnToTitle();
                }
            }
        }

        public void SetSelectedButton(int index)
        {
            hasUserInteracted = true;
            if (selectedButtonIndex != index)
            {
                selectedButtonIndex = Mathf.Clamp(index, 0, 1);
                if (SoundEffectManager.HasInstance)
                {
                    SoundEffectManager.Instance.PlayUIHover(0.75f);
                }
            }
        }

        private void TriggerReignite()
        {
            if (SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayUIClick();
            }
            if (respawnController != null)
            {
                respawnController.Respawn();
            }
        }

        private void TriggerReturnToTitle()
        {
            if (SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayUIClick();
            }

            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadScene(MainMenuScenePath);
            }
            catch
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        public void Tick(float dt, float time)
        {
            float speed = targetFadeAlpha > currentFadeAlpha ? deathFadeSpeed : respawnFadeSpeed;
            currentFadeAlpha = Mathf.MoveTowards(currentFadeAlpha, targetFadeAlpha, speed * dt);

            if (ignitionAlpha > 0f)
                ignitionAlpha = Mathf.MoveTowards(ignitionAlpha, 0f, 2.0f * dt);

            if (currentFadeAlpha <= 0.001f && ignitionAlpha <= 0.001f)
            {
                if (canvas != null && canvas.enabled)
                    canvas.enabled = false;
                return;
            }

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;

            // 1. Blackout & Vignette Fading
            Color bCol = blackoutColor;
            bCol.a = currentFadeAlpha * 0.96f;
            blackout.color = bCol;

            Color vCol = Color.black;
            vCol.a = Mathf.Clamp01(currentFadeAlpha * 1.4f);
            vignette.color = vCol;

            float contentAlpha = Mathf.Clamp01((currentFadeAlpha - 0.2f) / 0.8f);

            // 2. Header & Subtitle Typography Fading
            Color hCol = headerColor;
            hCol.a = contentAlpha;
            headerText.color = hCol;

            Color dCol = new Color(amberGold.r, amberGold.g, amberGold.b, contentAlpha * 0.8f);
            dividerText.color = dCol;

            Color sCol = subtitleColor;
            sCol.a = contentAlpha * 0.92f;
            subtitleText.color = sCol;

            Color iCol = icon.sprite == PixelSprites.SkullIcon ? Color.white : new Color(0.96f, 0.80f, 0.58f, 1f);
            iCol.a = contentAlpha;
            icon.color = iCol;

            Color haloCol = new Color(1.0f, 0.65f, 0.22f, contentAlpha * 0.38f * (0.8f + Mathf.Sin(time * 3f) * 0.2f));
            iconHalo.color = haloCol;

            // 3. Floating Cinder Sparks Animation
            for (int i = 0; i < CinderCount; i++)
            {
                if (cinderRects[i] == null) continue;

                Vector2 pos = cinderRects[i].anchoredPosition;
                pos.x += Mathf.Sin(time * 2.2f + cinderPhase[i]) * 16f * dt;
                pos.y += cinderVelocities[i].y * dt;

                // Loop back to bottom when drifted past top
                if (pos.y > 210f)
                {
                    pos.y = -190f;
                    pos.x = UnityEngine.Random.Range(-310f, 310f);
                }
                cinderRects[i].anchoredPosition = pos;

                float cinderFlicker = Mathf.Clamp01(Mathf.Sin(time * 5.5f + cinderPhase[i]) * 0.3f + 0.7f);
                Color cCol = Color.Lerp(new Color(1f, 0.40f, 0.12f), new Color(1f, 0.88f, 0.48f), cinderFlicker);
                cCol.a = contentAlpha * cinderBaseAlpha[i] * cinderFlicker;
                cinderImages[i].color = cCol;
            }

            // 4. Two-Button Menu Visual States
            buttonPulse = (Mathf.Sin(time * 3.2f) + 1f) * 0.5f;

            // Button 0: REIGNITE FLAME
            bool reigniteActive = selectedButtonIndex == 0;
            Color rBg = reigniteActive ? new Color(0.18f, 0.12f, 0.05f, contentAlpha * 0.85f) : new Color(0.04f, 0.06f, 0.10f, contentAlpha * 0.45f);
            reignitePedestal.color = rBg;

            Color rBorder = reigniteActive ? Color.Lerp(new Color(1f, 0.72f, 0.28f, 0.9f), new Color(1f, 0.90f, 0.55f, 1f), buttonPulse * 0.35f) : new Color(0.5f, 0.4f, 0.3f, 0.3f);
            rBorder.a *= contentAlpha;
            reigniteBorder.color = rBorder;

            Color rAura = new Color(1f, 0.65f, 0.2f, contentAlpha * (reigniteActive ? 0.35f * (0.8f + buttonPulse * 0.3f) : 0f));
            reigniteAura.color = rAura;

            Color rText = reigniteActive ? Color.Lerp(amberGold, Color.white, buttonPulse * 0.4f) : new Color(0.85f, 0.80f, 0.75f, 0.7f);
            rText.a *= contentAlpha;
            reigniteText.color = rText;
            reigniteText.characterSpacing = reigniteActive ? 5f : 3.5f;
            reigniteText.text = reigniteActive ? "[ REIGNITE FLAME ]" : "REIGNITE FLAME";

            // Button 1: RETURN TO ARCHIVE
            bool returnActive = selectedButtonIndex == 1;
            Color retBg = returnActive ? new Color(0.08f, 0.12f, 0.18f, contentAlpha * 0.85f) : new Color(0.03f, 0.05f, 0.08f, contentAlpha * 0.45f);
            returnPedestal.color = retBg;

            Color retBorder = returnActive ? Color.Lerp(new Color(0.70f, 0.84f, 1.0f, 0.85f), Color.white, buttonPulse * 0.35f) : new Color(0.35f, 0.45f, 0.55f, 0.25f);
            retBorder.a *= contentAlpha;
            returnBorder.color = retBorder;

            Color retAura = new Color(0.40f, 0.65f, 0.95f, contentAlpha * (returnActive ? 0.25f * (0.8f + buttonPulse * 0.3f) : 0f));
            returnAura.color = retAura;

            Color retText = returnActive ? Color.Lerp(moonlightSilver, Color.white, buttonPulse * 0.4f) : new Color(0.65f, 0.72f, 0.80f, 0.65f);
            retText.a *= contentAlpha;
            returnText.color = retText;
            returnText.characterSpacing = returnActive ? 4.5f : 3f;
            returnText.text = returnActive ? "[ RETURN TO ARCHIVE ]" : "RETURN TO ARCHIVE";

            // 5. Navigation Hint
            if (hintText != null)
            {
                Color hHint = new Color(0.65f, 0.60f, 0.52f, contentAlpha * 0.60f);
                hintText.color = hHint;
            }

            // 6. Progress Bar (Ember Capsule auto-respawn indicator)
            if (hasUserInteracted)
            {
                progressBarBorder.color = Color.clear;
                progressBarFill.color = Color.clear;
                progressBarGlow.color = Color.clear;
            }
            else
            {
                Color borderCol = new Color(0.45f, 0.38f, 0.28f, contentAlpha * 0.75f);
                progressBarBorder.color = borderCol;

                float progress = respawnController != null ? respawnController.NormalizedDeathProgress : 0f;
                progressBarFill.fillAmount = Mathf.Clamp01(progress);
                Color fillCol = progressFillColor;
                fillCol.a = contentAlpha * 0.90f;
                progressBarFill.color = fillCol;

                Color glowCol = new Color(1f, 0.65f, 0.2f, contentAlpha * 0.35f * (0.8f + buttonPulse * 0.2f));
                progressBarGlow.color = glowCol;
            }

            // 7. Lantern Re-ignition Flash
            if (ignitionAlpha > 0f)
            {
                Color ignCol = ignitionColor;
                ignCol.a = ignitionAlpha * 0.92f;
                ignitionFlash.color = ignCol;
                float s = 1f + (1f - ignitionAlpha) * 2.2f;
                ignitionFlash.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                ignitionFlash.color = Color.clear;
            }
        }

        private void BuildHierarchy()
        {
            var canvasGo = new GameObject("DeathScreen Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvas.pixelPerfect = true;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 1f;

            // 1. Blackout Curtain
            var blackGo = CreateFullscreenRect("Blackout", canvasGo.transform);
            blackout = blackGo.AddComponent<Image>();
            blackout.sprite = PixelSprites.White;
            blackout.color = Color.clear;

            // 2. Fullscreen Vignette
            var vigGo = CreateFullscreenRect("Vignette", canvasGo.transform);
            vignette = vigGo.AddComponent<Image>();
            vignette.sprite = PixelSprites.Vignette;
            vignette.color = Color.clear;

            // 3. Floating Cinder Sparks Container
            var cindersRoot = new GameObject("Cinder Sparks", typeof(RectTransform));
            cindersRoot.transform.SetParent(canvasGo.transform, false);
            for (int i = 0; i < CinderCount; i++)
            {
                var cinderGo = new GameObject($"Cinder_{i}", typeof(RectTransform));
                cinderGo.transform.SetParent(cindersRoot.transform, false);
                var r = cinderGo.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                float size = UnityEngine.Random.Range(3f, 7f);
                r.sizeDelta = new Vector2(size, size);
                cinderRects[i] = r;

                var img = cinderGo.AddComponent<Image>();
                img.sprite = PixelSprites.Spark;
                img.color = Color.clear;
                cinderImages[i] = img;

                cinderVelocities[i] = new Vector2(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(32f, 72f));
                cinderPhase[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                cinderBaseAlpha[i] = UnityEngine.Random.Range(0.6f, 1f);
            }

            // 4. Re-ignition Flash
            var ignGo = new GameObject("IgnitionFlash", typeof(RectTransform));
            ignGo.transform.SetParent(canvasGo.transform, false);
            var ignRect = ignGo.GetComponent<RectTransform>();
            ignRect.anchorMin = ignRect.anchorMax = ignRect.pivot = new Vector2(0.5f, 0.5f);
            ignRect.sizeDelta = new Vector2(480f, 480f);
            ignitionFlash = ignGo.AddComponent<Image>();
            ignitionFlash.sprite = PixelSprites.Glow;
            ignitionFlash.color = Color.clear;

            // 5. Center Group Container
            var centerGo = new GameObject("CenterGroup", typeof(RectTransform));
            centerGo.transform.SetParent(canvasGo.transform, false);
            centerGroup = centerGo.GetComponent<RectTransform>();
            centerGroup.anchorMin = centerGroup.anchorMax = centerGroup.pivot = new Vector2(0.5f, 0.5f);
            centerGroup.sizeDelta = new Vector2(520f, 290f);

            // 5a. Icon Halo & Death Icon
            var haloGo = new GameObject("IconHalo", typeof(RectTransform));
            haloGo.transform.SetParent(centerGroup, false);
            var haloRect = haloGo.GetComponent<RectTransform>();
            haloRect.anchorMin = haloRect.anchorMax = haloRect.pivot = new Vector2(0.5f, 0.5f);
            haloRect.anchoredPosition = new Vector2(0f, 96f);
            haloRect.sizeDelta = new Vector2(90f, 90f);
            iconHalo = haloGo.AddComponent<Image>();
            iconHalo.sprite = PixelSprites.Glow;
            iconHalo.color = Color.clear;

            var iconGo = new GameObject("DeathIcon", typeof(RectTransform));
            iconGo.transform.SetParent(centerGroup, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 96f);
            iconRect.sizeDelta = new Vector2(36f, 40f);
            icon = iconGo.AddComponent<Image>();
            icon.sprite = PixelSprites.ExtinguishedFlame;
            icon.color = Color.clear;

            // 5b. Monumental Title (24px, Bold, Character Spacing 8)
            headerText = AddTMPText("Header", centerGroup, new Vector2(0f, 54f), new Vector2(500f, 36f), 24,
                TextAlignmentOptions.Center, Color.clear);
            headerText.characterSpacing = 8f;
            headerText.fontStyle = FontStyles.Bold;

            // 5c. Ornate Divider Line
            dividerText = AddTMPText("Divider", centerGroup, new Vector2(0f, 30f), new Vector2(320f, 18f), 13,
                TextAlignmentOptions.Center, Color.clear);
            dividerText.text = "- - - - -";
            dividerText.characterSpacing = 4f;

            // 5d. Subtitle Lore Flavor (14px legible body text)
            subtitleText = AddTMPText("Subtitle", centerGroup, new Vector2(0f, 8f), new Vector2(480f, 26f), 14,
                TextAlignmentOptions.Center, Color.clear);
            subtitleText.characterSpacing = 2.5f;

            // 5e. Two-Button Menu Section
            var menuContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
            menuContainer.transform.SetParent(centerGroup, false);
            var menuRect = menuContainer.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.anchoredPosition = new Vector2(0f, -48f);
            menuRect.sizeDelta = new Vector2(340f, 90f);

            // Button 0: REIGNITE FLAME
            reigniteButton = CreateMenuButton(menuRect, "ReigniteButton", new Vector2(0f, 18f), new Vector2(300f, 36f),
                "[ REIGNITE FLAME ]", 13f, 0, TriggerReignite,
                out reignitePedestal, out reigniteBorder, out reigniteAura, out reigniteText);

            // Button 1: RETURN TO ARCHIVE
            returnButton = CreateMenuButton(menuRect, "ReturnButton", new Vector2(0f, -24f), new Vector2(300f, 32f),
                "RETURN TO ARCHIVE", 12f, 1, TriggerReturnToTitle,
                out returnPedestal, out returnBorder, out returnAura, out returnText);

            // 5f. Auto-Respawn Progress Bar (Slim Ember Capsule below buttons)
            var barBorderGo = new GameObject("ProgressBarBorder", typeof(RectTransform));
            barBorderGo.transform.SetParent(centerGroup, false);
            var barBorderRect = barBorderGo.GetComponent<RectTransform>();
            barBorderRect.anchorMin = barBorderRect.anchorMax = barBorderRect.pivot = new Vector2(0.5f, 0.5f);
            barBorderRect.anchoredPosition = new Vector2(0f, -106f);
            barBorderRect.sizeDelta = new Vector2(160f, 8f);
            progressBarBorder = barBorderGo.AddComponent<Image>();
            progressBarBorder.sprite = PixelSprites.PillBorder;
            progressBarBorder.type = Image.Type.Sliced;
            progressBarBorder.color = Color.clear;

            var barGlowGo = new GameObject("ProgressBarGlow", typeof(RectTransform));
            barGlowGo.transform.SetParent(barBorderGo.transform, false);
            var barGlowRect = barGlowGo.GetComponent<RectTransform>();
            barGlowRect.anchorMin = barGlowRect.anchorMax = barGlowRect.pivot = new Vector2(0.5f, 0.5f);
            barGlowRect.sizeDelta = new Vector2(176f, 18f);
            progressBarGlow = barGlowGo.AddComponent<Image>();
            progressBarGlow.sprite = PixelSprites.PillGlow;
            progressBarGlow.color = Color.clear;

            var barFillGo = new GameObject("ProgressBarFill", typeof(RectTransform));
            barFillGo.transform.SetParent(barBorderGo.transform, false);
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            barFillRect.anchorMin = barFillRect.anchorMax = barFillRect.pivot = new Vector2(0.5f, 0.5f);
            barFillRect.sizeDelta = new Vector2(154f, 4f);
            progressBarFill = barFillGo.AddComponent<Image>();
            progressBarFill.sprite = PixelSprites.Pill;
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillAmount = 0f;
            progressBarFill.color = Color.clear;

            // 5g. Navigation Hint Text (Title Safe standard)
            hintText = AddTMPText("NavHint", centerGroup, new Vector2(0f, -126f), new Vector2(440f, 20f), 10,
                TextAlignmentOptions.Center, Color.clear);
            hintText.text = "[ W / S ] SELECT       [ SPACE / ENTER ] CONFIRM";
            hintText.characterSpacing = 3.5f;

            canvas.enabled = false;
        }

        private Button CreateMenuButton(RectTransform parent, string name, Vector2 pos, Vector2 size,
            string label, float fontSize, int buttonIndex, Action onClick,
            out Image pedestalImg, out Image borderImg, out Image auraImg, out TextMeshProUGUI textComp)
        {
            var btnGo = new GameObject(name, typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            // Soft Radial Glow Aura
            var auraGo = new GameObject("GlowAura", typeof(RectTransform));
            auraGo.transform.SetParent(btnGo.transform, false);
            var auraRect = auraGo.GetComponent<RectTransform>();
            auraRect.anchorMin = auraRect.anchorMax = auraRect.pivot = new Vector2(0.5f, 0.5f);
            auraRect.sizeDelta = size + new Vector2(24f, 16f);
            auraImg = auraGo.AddComponent<Image>();
            auraImg.sprite = PixelSprites.Glow;
            auraImg.color = Color.clear;
            auraImg.raycastTarget = false;

            // Frosted Pedestal Plate
            var pedGo = new GameObject("Pedestal", typeof(RectTransform));
            pedGo.transform.SetParent(btnGo.transform, false);
            var pedRect = pedGo.GetComponent<RectTransform>();
            pedRect.anchorMin = pedRect.anchorMax = pedRect.pivot = new Vector2(0.5f, 0.5f);
            pedRect.sizeDelta = size;
            pedestalImg = pedGo.AddComponent<Image>();
            pedestalImg.sprite = PixelSprites.Pill;
            pedestalImg.type = Image.Type.Sliced;
            pedestalImg.color = Color.clear;

            // Subtle Border Rim
            var bordGo = new GameObject("Border", typeof(RectTransform));
            bordGo.transform.SetParent(btnGo.transform, false);
            var bordRect = bordGo.GetComponent<RectTransform>();
            bordRect.anchorMin = bordRect.anchorMax = bordRect.pivot = new Vector2(0.5f, 0.5f);
            bordRect.sizeDelta = size;
            borderImg = bordGo.AddComponent<Image>();
            borderImg.sprite = PixelSprites.PillBorder;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = Color.clear;
            borderImg.raycastTarget = false;

            // Label Text
            textComp = AddTMPText("Label", btnGo.transform, Vector2.zero, size, fontSize,
                TextAlignmentOptions.Center, Color.clear);
            textComp.text = label;
            textComp.fontStyle = FontStyles.Bold;

            // Button Component & Event Trigger for Hover
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = pedestalImg;
            var colors = btn.colors;
            colors.fadeDuration = 0.05f;
            colors.colorMultiplier = 1f;
            colors.highlightedColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var trigger = btnGo.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) => SetSelectedButton(buttonIndex));
            trigger.triggers.Add(enterEntry);

            return btn;
        }

        private static GameObject CreateFullscreenRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private TextMeshProUGUI AddTMPText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize,
            TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var txt = go.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
                txt.font = fontAsset;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.raycastTarget = false;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            return txt;
        }
    }
}
