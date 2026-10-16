using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Atmospheric cinematic death and respawn overlay.
    /// Provides creeping darkness vignette, blackout curtain, contextual death messages,
    /// animated prompt, auto-respawn countdown progress, and warm lantern re-ignition flash.
    /// Builds its own Screen Space Overlay Canvas at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        [Header("Target Controller (auto-found if empty)")]
        [SerializeField] private PlayerRespawnController respawnController;

        [Header("Fade Speeds")]
        [SerializeField, Min(0.1f)] private float deathFadeSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float respawnFadeSpeed = 3.0f;

        [Header("Palette")]
        [SerializeField] private Color blackoutColor = new Color32(6, 7, 10, 255);
        [SerializeField] private Color headerColor = new Color32(245, 230, 215, 255);
        [SerializeField] private Color subtitleColor = new Color32(165, 150, 135, 255);
        [SerializeField] private Color promptColor = new Color32(235, 185, 95, 255);
        [SerializeField] private Color progressFillColor = new Color32(220, 150, 50, 255);
        [SerializeField] private Color ignitionColor = new Color32(255, 210, 110, 255);

        // Runtime hierarchy
        private Canvas canvas;
        private Image blackout;
        private Image vignette;
        private Image icon;
        private Image ignitionFlash;
        private Text headerText;
        private Text subtitleText;
        private Text promptText;
        private Image progressBarBorder;
        private Image progressBarFill;
        private RectTransform centerGroup;

        // Animation state
        private float currentFadeAlpha;
        private float targetFadeAlpha;
        private float ignitionAlpha;
        private bool isSubscribed;

        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => currentFadeAlpha > 0.01f || canvas.enabled;

        private void Awake()
        {
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

        private void HandlePlayerDied(DeathReason reason)
        {
            targetFadeAlpha = 1f;

            switch (reason)
            {
                case DeathReason.Darkness:
                    icon.sprite = PixelSprites.ExtinguishedFlame;
                    headerText.text = "KARANLIKTA KAYBOLDUN";
                    subtitleText.text = "Fenerin alevi söndü... Karanlık seni yuttu.";
                    break;
                case DeathReason.Void:
                    icon.sprite = PixelSprites.SkullIcon;
                    headerText.text = "DİPSİZ KUYUYA DÜŞTÜN";
                    subtitleText.text = "Eski dünyanın derinliklerinde kayboldun.";
                    break;
                case DeathReason.Hazard:
                    icon.sprite = PixelSprites.SkullIcon;
                    headerText.text = "YOK OLDUN";
                    subtitleText.text = "Harabelerin ölümcül tehlikesi seni yakaladı.";
                    break;
            }

            if (!canvas.enabled)
                canvas.enabled = true;
        }

        private void HandlePlayerRespawned()
        {
            targetFadeAlpha = 0f;
            ignitionAlpha = 1f;
        }

        private void Update()
        {
            if (respawnController == null)
            {
                respawnController = FindAnyObjectByType<PlayerRespawnController>();
                TrySubscribe();
            }

            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        public void Tick(float dt, float time)
        {
            float speed = targetFadeAlpha > currentFadeAlpha ? deathFadeSpeed : respawnFadeSpeed;
            currentFadeAlpha = Mathf.MoveTowards(currentFadeAlpha, targetFadeAlpha, speed * dt);

            if (ignitionAlpha > 0f)
                ignitionAlpha = Mathf.MoveTowards(ignitionAlpha, 0f, 1.8f * dt);

            if (currentFadeAlpha <= 0.001f && ignitionAlpha <= 0.001f)
            {
                if (canvas.enabled)
                    canvas.enabled = false;
                return;
            }

            if (!canvas.enabled)
                canvas.enabled = true;

            // Apply fading
            Color bCol = blackoutColor;
            bCol.a = currentFadeAlpha;
            blackout.color = bCol;

            Color vCol = Color.black;
            vCol.a = Mathf.Clamp01(currentFadeAlpha * 1.25f);
            vignette.color = vCol;

            float contentAlpha = Mathf.Clamp01((currentFadeAlpha - 0.25f) / 0.75f);

            Color hCol = headerColor;
            hCol.a = contentAlpha;
            headerText.color = hCol;

            Color sCol = subtitleColor;
            sCol.a = contentAlpha;
            subtitleText.color = sCol;

            Color iCol = icon.sprite == PixelSprites.SkullIcon ? Color.white : new Color(0.85f, 0.75f, 0.75f, 1f);
            iCol.a = contentAlpha;
            icon.color = iCol;

            // Breathing prompt pulse
            float pulse = (Mathf.Sin(time * 4f) + 1f) * 0.5f;
            Color pCol = Color.Lerp(promptColor, Color.white, pulse * 0.45f);
            pCol.a = contentAlpha * Mathf.Lerp(0.6f, 1f, pulse);
            promptText.color = pCol;

            // Progress bar
            Color borderCol = new Color(0.35f, 0.28f, 0.22f, contentAlpha * 0.8f);
            progressBarBorder.color = borderCol;

            float progress = respawnController != null ? respawnController.NormalizedDeathProgress : 0f;
            progressBarFill.fillAmount = Mathf.Clamp01(progress);
            Color fillCol = progressFillColor;
            fillCol.a = contentAlpha * 0.9f;
            progressBarFill.color = fillCol;

            // Lantern re-ignition flash
            if (ignitionAlpha > 0f)
            {
                Color ignCol = ignitionColor;
                ignCol.a = ignitionAlpha * 0.85f;
                ignitionFlash.color = ignCol;
                float s = 1f + (1f - ignitionAlpha) * 1.5f;
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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

            // 3. Re-ignition Flash
            var ignGo = new GameObject("IgnitionFlash", typeof(RectTransform));
            ignGo.transform.SetParent(canvasGo.transform, false);
            var ignRect = ignGo.GetComponent<RectTransform>();
            ignRect.anchorMin = ignRect.anchorMax = ignRect.pivot = new Vector2(0.5f, 0.5f);
            ignRect.sizeDelta = new Vector2(300f, 300f);
            ignitionFlash = ignGo.AddComponent<Image>();
            ignitionFlash.sprite = PixelSprites.Glow;
            ignitionFlash.color = Color.clear;

            // 4. Center Group Container
            var centerGo = new GameObject("CenterGroup", typeof(RectTransform));
            centerGo.transform.SetParent(canvasGo.transform, false);
            centerGroup = centerGo.GetComponent<RectTransform>();
            centerGroup.anchorMin = centerGroup.anchorMax = centerGroup.pivot = new Vector2(0.5f, 0.5f);
            centerGroup.sizeDelta = new Vector2(400f, 200f);

            // 4a. Icon (extinguished flame or skull, 24x24 reference pixels)
            var iconGo = new GameObject("DeathIcon", typeof(RectTransform));
            iconGo.transform.SetParent(centerGroup, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 50f);
            iconRect.sizeDelta = new Vector2(28f, 32f);
            icon = iconGo.AddComponent<Image>();
            icon.sprite = PixelSprites.ExtinguishedFlame;
            icon.color = Color.clear;

            // 4b. Header Title
            headerText = AddText("Header", centerGroup, font, new Vector2(0f, 15f), new Vector2(380f, 30f), 18,
                TextAnchor.MiddleCenter, Color.clear);
            headerText.fontStyle = FontStyle.Bold;

            // 4c. Subtitle Lore Flavor
            subtitleText = AddText("Subtitle", centerGroup, font, new Vector2(0f, -12f), new Vector2(380f, 22f), 11,
                TextAnchor.MiddleCenter, Color.clear);

            // 4d. Progress Bar (120x4px)
            var barBorderGo = new GameObject("ProgressBarBorder", typeof(RectTransform));
            barBorderGo.transform.SetParent(centerGroup, false);
            var barBorderRect = barBorderGo.GetComponent<RectTransform>();
            barBorderRect.anchorMin = barBorderRect.anchorMax = barBorderRect.pivot = new Vector2(0.5f, 0.5f);
            barBorderRect.anchoredPosition = new Vector2(0f, -42f);
            barBorderRect.sizeDelta = new Vector2(124f, 6f);
            progressBarBorder = barBorderGo.AddComponent<Image>();
            progressBarBorder.sprite = PixelSprites.White;
            progressBarBorder.color = Color.clear;

            var barFillGo = new GameObject("ProgressBarFill", typeof(RectTransform));
            barFillGo.transform.SetParent(barBorderGo.transform, false);
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            barFillRect.anchorMin = barFillRect.anchorMax = barFillRect.pivot = new Vector2(0.5f, 0.5f);
            barFillRect.sizeDelta = new Vector2(120f, 4f);
            progressBarFill = barFillGo.AddComponent<Image>();
            progressBarFill.sprite = PixelSprites.White;
            progressBarFill.type = Image.Type.Filled;
            progressBarFill.fillMethod = Image.FillMethod.Horizontal;
            progressBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressBarFill.fillAmount = 0f;
            progressBarFill.color = Color.clear;

            // 4e. Prompt
            promptText = AddText("Prompt", centerGroup, font, new Vector2(0f, -65f), new Vector2(380f, 20f), 10,
                TextAnchor.MiddleCenter, Color.clear);
            promptText.text = "[SPACE] / [R] Yeniden Doğ";

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

        private static Text AddText(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize,
            TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = color;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
