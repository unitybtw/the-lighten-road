using System;
using TheLightenRoad.Audio;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Atmospheric cinematic death and respawn overlay.
    /// Features:
    /// - Deep creeping void vignette and cinematic blackout curtain
    /// - Procedurally drifting glowing ember / cinder sparks drifting upward (Dark Souls / Hollow Knight aesthetic)
    /// - Dignified TextMeshPro typography using the Archive font with soft golden/ash radiance
    /// - Ornate divider line and contextual philosophical lore text
    /// - Sleek ember-pill progress countdown bar (2-3s auto-respawn)
    /// - Instant [SPACE] / [ENTER] / [R] skip to reignite immediately
    /// - Audio cues: flame extinguish on death, burst of flame kindle on respawn
    /// - Radiant warm lantern re-ignition flash upon reviving
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        [Header("Target Controller (auto-found if empty)")]
        [SerializeField] private PlayerRespawnController respawnController;

        [Header("Typography Font")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Fade Speeds")]
        [SerializeField, Min(0.1f)] private float deathFadeSpeed = 1.9f;
        [SerializeField, Min(0.1f)] private float respawnFadeSpeed = 3.2f;

        [Header("Palette")]
        [SerializeField] private Color blackoutColor = new Color32(6, 8, 12, 255);
        [SerializeField] private Color headerColor = new Color32(248, 232, 212, 255);
        [SerializeField] private Color subtitleColor = new Color32(175, 165, 155, 255);
        [SerializeField] private Color promptColor = new Color32(245, 195, 105, 255);
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
        private TextMeshProUGUI promptText;
        private Image progressBarBorder;
        private Image progressBarFill;
        private Image progressBarGlow;
        private RectTransform centerGroup;

        // Floating Cinder Sparks
        private const int CinderCount = 16;
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

        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => currentFadeAlpha > 0.01f || (canvas != null && canvas.enabled);

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

            switch (reason)
            {
                case DeathReason.Darkness:
                    icon.sprite = PixelSprites.ExtinguishedFlame;
                    headerText.text = "THE LIGHT HAS FADED";
                    subtitleText.text = "Your lantern went out. The darkness claimed another seeker.";
                    break;
                case DeathReason.Void:
                    icon.sprite = PixelSprites.SkullIcon;
                    headerText.text = "LOST TO THE ABYSS";
                    subtitleText.text = "You vanished into the silent depths of the old world.";
                    break;
                case DeathReason.Hazard:
                    icon.sprite = PixelSprites.EmberCracks;
                    headerText.text = "FLAME EXTINGUISHED";
                    subtitleText.text = "The ancient ruins claimed your fragile warmth.";
                    break;
            }

            // Scatter cinder particles across screen bottom
            for (int i = 0; i < CinderCount; i++)
            {
                float x = UnityEngine.Random.Range(-300f, 300f);
                float y = UnityEngine.Random.Range(-180f, -60f);
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

            // Handle manual reignite input (Space / Enter / R)
            if (currentFadeAlpha > 0.3f && respawnController != null && respawnController.State != RespawnState.Alive)
            {
                var k = Keyboard.current;
                if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.rKey.wasPressedThisFrame))
                {
                    respawnController.Respawn();
                }
            }

            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
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
            bCol.a = currentFadeAlpha;
            blackout.color = bCol;

            Color vCol = Color.black;
            vCol.a = Mathf.Clamp01(currentFadeAlpha * 1.35f);
            vignette.color = vCol;

            float contentAlpha = Mathf.Clamp01((currentFadeAlpha - 0.2f) / 0.8f);

            // 2. Center Content Fading
            Color hCol = headerColor;
            hCol.a = contentAlpha;
            headerText.color = hCol;

            Color dCol = new Color(0.85f, 0.72f, 0.45f, contentAlpha * 0.75f);
            dividerText.color = dCol;

            Color sCol = subtitleColor;
            sCol.a = contentAlpha * 0.9f;
            subtitleText.color = sCol;

            Color iCol = icon.sprite == PixelSprites.SkullIcon ? Color.white : new Color(0.95f, 0.78f, 0.55f, 1f);
            iCol.a = contentAlpha;
            icon.color = iCol;

            Color haloCol = new Color(0.95f, 0.55f, 0.18f, contentAlpha * 0.35f * (0.8f + Mathf.Sin(time * 3f) * 0.2f));
            iconHalo.color = haloCol;

            // 3. Floating Cinder Sparks Animation
            for (int i = 0; i < CinderCount; i++)
            {
                if (cinderRects[i] == null) continue;

                Vector2 pos = cinderRects[i].anchoredPosition;
                pos.x += Mathf.Sin(time * 2f + cinderPhase[i]) * 18f * dt;
                pos.y += cinderVelocities[i].y * dt;

                // Loop back to bottom when drifted past top
                if (pos.y > 200f)
                {
                    pos.y = -190f;
                    pos.x = UnityEngine.Random.Range(-300f, 300f);
                }
                cinderRects[i].anchoredPosition = pos;

                float cinderFlicker = Mathf.Clamp01(Mathf.Sin(time * 5f + cinderPhase[i]) * 0.3f + 0.7f);
                Color cCol = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.45f), cinderFlicker);
                cCol.a = contentAlpha * cinderBaseAlpha[i] * cinderFlicker;
                cinderImages[i].color = cCol;
            }

            // 4. Breathing Prompt
            float pulse = (Mathf.Sin(time * 3.5f) + 1f) * 0.5f;
            Color pCol = Color.Lerp(promptColor, Color.white, pulse * 0.35f);
            pCol.a = contentAlpha * Mathf.Lerp(0.55f, 1f, pulse);
            promptText.color = pCol;

            // 5. Progress Bar (Ember Capsule)
            Color borderCol = new Color(0.45f, 0.38f, 0.28f, contentAlpha * 0.85f);
            progressBarBorder.color = borderCol;

            float progress = respawnController != null ? respawnController.NormalizedDeathProgress : 0f;
            progressBarFill.fillAmount = Mathf.Clamp01(progress);
            Color fillCol = progressFillColor;
            fillCol.a = contentAlpha * 0.95f;
            progressBarFill.color = fillCol;

            Color glowCol = new Color(1f, 0.65f, 0.2f, contentAlpha * 0.4f * (0.8f + pulse * 0.2f));
            progressBarGlow.color = glowCol;

            // 6. Lantern Re-ignition Flash
            if (ignitionAlpha > 0f)
            {
                Color ignCol = ignitionColor;
                ignCol.a = ignitionAlpha * 0.9f;
                ignitionFlash.color = ignCol;
                float s = 1f + (1f - ignitionAlpha) * 2.0f;
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
            canvas.sortingOrder = 200;
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
                float size = UnityEngine.Random.Range(3f, 6f);
                r.sizeDelta = new Vector2(size, size);
                cinderRects[i] = r;

                var img = cinderGo.AddComponent<Image>();
                img.sprite = PixelSprites.Spark;
                img.color = Color.clear;
                cinderImages[i] = img;

                cinderVelocities[i] = new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(35f, 75f));
                cinderPhase[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                cinderBaseAlpha[i] = UnityEngine.Random.Range(0.6f, 1f);
            }

            // 4. Re-ignition Flash
            var ignGo = new GameObject("IgnitionFlash", typeof(RectTransform));
            ignGo.transform.SetParent(canvasGo.transform, false);
            var ignRect = ignGo.GetComponent<RectTransform>();
            ignRect.anchorMin = ignRect.anchorMax = ignRect.pivot = new Vector2(0.5f, 0.5f);
            ignRect.sizeDelta = new Vector2(400f, 400f);
            ignitionFlash = ignGo.AddComponent<Image>();
            ignitionFlash.sprite = PixelSprites.Glow;
            ignitionFlash.color = Color.clear;

            // 5. Center Group Container
            var centerGo = new GameObject("CenterGroup", typeof(RectTransform));
            centerGo.transform.SetParent(canvasGo.transform, false);
            centerGroup = centerGo.GetComponent<RectTransform>();
            centerGroup.anchorMin = centerGroup.anchorMax = centerGroup.pivot = new Vector2(0.5f, 0.5f);
            centerGroup.sizeDelta = new Vector2(500f, 240f);

            // 5a. Icon Halo & Death Icon
            var haloGo = new GameObject("IconHalo", typeof(RectTransform));
            haloGo.transform.SetParent(centerGroup, false);
            var haloRect = haloGo.GetComponent<RectTransform>();
            haloRect.anchorMin = haloRect.anchorMax = haloRect.pivot = new Vector2(0.5f, 0.5f);
            haloRect.anchoredPosition = new Vector2(0f, 62f);
            haloRect.sizeDelta = new Vector2(80f, 80f);
            iconHalo = haloGo.AddComponent<Image>();
            iconHalo.sprite = PixelSprites.Glow;
            iconHalo.color = Color.clear;

            var iconGo = new GameObject("DeathIcon", typeof(RectTransform));
            iconGo.transform.SetParent(centerGroup, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 62f);
            iconRect.sizeDelta = new Vector2(32f, 36f);
            icon = iconGo.AddComponent<Image>();
            icon.sprite = PixelSprites.ExtinguishedFlame;
            icon.color = Color.clear;

            // 5b. Header Title (TextMeshPro with letter spacing)
            headerText = AddTMPText("Header", centerGroup, new Vector2(0f, 22f), new Vector2(480f, 34f), 22,
                TextAlignmentOptions.Center, Color.clear);
            headerText.characterSpacing = 8f;
            headerText.fontStyle = FontStyles.Bold;

            // 5c. Ornate Divider Line
            dividerText = AddTMPText("Divider", centerGroup, new Vector2(0f, 2f), new Vector2(300f, 16f), 12,
                TextAlignmentOptions.Center, Color.clear);
            dividerText.text = "- - -";
            dividerText.characterSpacing = 4f;

            // 5d. Subtitle Lore Flavor
            subtitleText = AddTMPText("Subtitle", centerGroup, new Vector2(0f, -18f), new Vector2(460f, 24f), 12,
                TextAlignmentOptions.Center, Color.clear);

            // 5e. Ember Progress Bar Container (140x8px pill)
            var barBorderGo = new GameObject("ProgressBarBorder", typeof(RectTransform));
            barBorderGo.transform.SetParent(centerGroup, false);
            var barBorderRect = barBorderGo.GetComponent<RectTransform>();
            barBorderRect.anchorMin = barBorderRect.anchorMax = barBorderRect.pivot = new Vector2(0.5f, 0.5f);
            barBorderRect.anchoredPosition = new Vector2(0f, -50f);
            barBorderRect.sizeDelta = new Vector2(144f, 10f);
            progressBarBorder = barBorderGo.AddComponent<Image>();
            progressBarBorder.sprite = PixelSprites.PillBorder;
            progressBarBorder.type = Image.Type.Sliced;
            progressBarBorder.color = Color.clear;

            var barGlowGo = new GameObject("ProgressBarGlow", typeof(RectTransform));
            barGlowGo.transform.SetParent(barBorderGo.transform, false);
            var barGlowRect = barGlowGo.GetComponent<RectTransform>();
            barGlowRect.anchorMin = barGlowRect.anchorMax = barGlowRect.pivot = new Vector2(0.5f, 0.5f);
            barGlowRect.sizeDelta = new Vector2(160f, 22f);
            progressBarGlow = barGlowGo.AddComponent<Image>();
            progressBarGlow.sprite = PixelSprites.PillGlow;
            progressBarGlow.color = Color.clear;

            var barFillGo = new GameObject("ProgressBarFill", typeof(RectTransform));
            barFillGo.transform.SetParent(barBorderGo.transform, false);
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            barFillRect.anchorMin = barFillRect.anchorMax = barFillRect.pivot = new Vector2(0.5f, 0.5f);
            barFillRect.sizeDelta = new Vector2(138f, 6f);
            progressBarFill = barFillGo.AddComponent<Image>();
            progressBarFill.sprite = PixelSprites.Pill;
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillAmount = 0f;
            progressBarFill.color = Color.clear;

            // 5f. Prompt Text
            promptText = AddTMPText("Prompt", centerGroup, new Vector2(0f, -76f), new Vector2(380f, 22f), 11,
                TextAlignmentOptions.Center, Color.clear);
            promptText.text = "[ SPACE ] REIGNITE FLAME";
            promptText.characterSpacing = 6f;

            canvas.enabled = false;
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
            txt.enableWordWrapping = false;
            return txt;
        }
    }
}
