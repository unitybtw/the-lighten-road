using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// RPG-style Ember inventory HUD in accordance with Game Designer Furkan's feedback:
    /// - 5 discrete ember slots side-by-side like an RPG health bar.
    /// - Each ember displays 3 visual states: Full (2 charges), Half (1 charge), Empty (0 charges).
    /// - Stacking badge (+1, +2, +N) positioned next to the 5th slot when total embers exceed 5.
    /// - Timed Flare scout indicator and floating pickup feedback.
    /// Builds its own Screen Space Overlay Canvas at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanternHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private LanternController target;

        [Header("Layout — reference resolution 640x360")]
        [SerializeField] private Vector2 screenMargin = new Vector2(14f, 14f);
        [SerializeField] private int maxDisplaySlots = 5;

        [Header("Colors")]
        [SerializeField] private Color panelColor = new Color32(14, 11, 16, 220);
        [SerializeField] private Color borderColor = new Color32(92, 64, 40, 255);
        [SerializeField] private Color badgeTextColor = new Color32(255, 215, 110, 255);
        [SerializeField] private Color dimTextColor = new Color32(185, 155, 120, 255);
        [SerializeField] private Color flareTextColor = new Color32(255, 185, 60, 255);
        [SerializeField] private Color emptyTextColor = new Color32(140, 110, 100, 255);

        // Runtime hierarchy
        private Canvas canvas;
        private RectTransform panel;
        private Image[] slotSockets;
        private Image[] slotFills;
        private Text overflowBadge;
        private Text modeLabel;
        private Text timerLabel;
        private Text popupLabel;
        private Image flareGlow;

        // State
        private float lastCharges = -1f;
        private float popupTimer;
        private float flashTimer;
        private bool subscribed;

        public LanternController Target => target;
        public Canvas Canvas => canvas;
        public string ModeText => modeLabel != null ? modeLabel.text : string.Empty;
        public string OverflowText => overflowBadge != null ? overflowBadge.text : string.Empty;
        public bool IsShowingPickup => popupTimer > 0f;
        public int MaxDisplaySlots => maxDisplaySlots;
        public Sprite GetSlotSprite(int index) => (slotFills != null && index >= 0 && index < slotFills.Length) ? slotFills[index].sprite : null;
        public Color GetSlotColor(int index) => (slotFills != null && index >= 0 && index < slotFills.Length) ? slotFills[index].color : Color.clear;

        private void Awake()
        {
            BuildHierarchy();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void SetTarget(LanternController controller)
        {
            Unbind();
            target = controller;
            lastCharges = -1f;
            TryBind();
        }

        private void TryBind()
        {
            if (target == null)
                target = FindAnyObjectByType<LanternController>();
            if (target == null || subscribed || target.Fuel == null)
                return;

            target.FlareChanged += HandleFlareChanged;
            subscribed = true;
        }

        private void Unbind()
        {
            if (!subscribed || target == null)
            {
                subscribed = false;
                return;
            }

            target.FlareChanged -= HandleFlareChanged;
            subscribed = false;
        }

        private void HandleFlareChanged(bool flared)
        {
            flashTimer = 0.3f;
        }

        private void Update()
        {
            if (!subscribed)
                TryBind();

            if (target == null || target.Fuel == null)
            {
                if (canvas != null)
                    canvas.enabled = false;
                return;
            }

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;

            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        public void Tick(float deltaTime, float time)
        {
            LanternFuel fuel = target.Fuel;
            int totalCharges = fuel.TotalCharges;
            int totalEmbers = fuel.TotalEmbers;
            int overflow = fuel.OverflowEmbers;

            // Detect pickup additions
            if (lastCharges >= 0 && totalCharges > lastCharges)
            {
                int gainedCharges = totalCharges - Mathf.RoundToInt(lastCharges);
                ShowPickup(Mathf.CeilToInt(gainedCharges / 2f));
            }
            lastCharges = totalCharges;

            // Update the 5 discrete ember slots
            for (int i = 0; i < maxDisplaySlots; i++)
            {
                // Each slot represents 2 charges
                int chargesForThisEmber = Mathf.Clamp(totalCharges - (i * 2), 0, 2);

                if (chargesForThisEmber == 2)
                {
                    slotFills[i].sprite = PixelSprites.EmberFull;
                    slotFills[i].color = Color.white;
                }
                else if (chargesForThisEmber == 1)
                {
                    slotFills[i].sprite = PixelSprites.EmberHalf;
                    slotFills[i].color = Color.white;
                }
                else
                {
                    slotFills[i].color = Color.clear;
                }
            }

            // Overflow badge (e.g. "+1", "+2", "+4")
            if (overflow > 0)
            {
                overflowBadge.text = "+" + overflow;
                overflowBadge.color = badgeTextColor;
            }
            else
            {
                overflowBadge.text = string.Empty;
            }

            // Mode and Scout labels
            if (target.IsFlared)
            {
                float remaining = target.FlareTimer;
                modeLabel.text = "ALEV İZCİSİ";
                modeLabel.color = flareTextColor;
                timerLabel.text = remaining > 0f ? $"{remaining:0.0}s" : string.Empty;
                timerLabel.color = flareTextColor;

                float pulse = (Mathf.Sin(time * 8f) + 1f) * 0.5f;
                flareGlow.color = new Color(1f, 0.7f, 0.2f, 0.25f + pulse * 0.25f);
            }
            else
            {
                flareGlow.color = Color.clear;
                timerLabel.text = string.Empty;

                if (totalCharges <= 0)
                {
                    modeLabel.text = "[F] KOR TÜKENDİ";
                    modeLabel.color = emptyTextColor;
                }
                else
                {
                    modeLabel.text = "[F] İZCİ FIRLAT";
                    modeLabel.color = dimTextColor;
                }
            }

            // Feedback animations
            if (popupTimer > 0f)
            {
                popupTimer -= deltaTime;
                float t = 1f - Mathf.Clamp01(popupTimer / 0.9f);
                popupLabel.rectTransform.anchoredPosition = new Vector2(popupLabel.rectTransform.anchoredPosition.x,
                    -36f + Mathf.Round(t * 8f));
                Color c = flareTextColor;
                c.a = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                popupLabel.color = c;
            }
            else
            {
                popupLabel.color = Color.clear;
            }

            if (flashTimer > 0f)
                flashTimer -= deltaTime;
        }

        private void ShowPickup(int emberCount)
        {
            popupTimer = 0.9f;
            popupLabel.text = "+" + emberCount + " KOR";
            popupLabel.color = flareTextColor;
        }

        private void BuildHierarchy()
        {
            var canvasGo = new GameObject("LanternHUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 1f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Main Panel
            Vector2 basePos = new Vector2(screenMargin.x, -screenMargin.y);
            panel = CreateRect("EmberHUDPanel", canvasGo.transform, basePos, new Vector2(146f, 44f));
            var border = AddImage(panel.gameObject, PixelSprites.White, borderColor);
            var inner = CreateRect("Inner", panel, new Vector2(2f, -2f), new Vector2(142f, 40f));
            AddImage(inner.gameObject, PixelSprites.White, panelColor);

            // Flare active ambient glow backing
            var glowRect = CreateRect("FlareGlow", inner, Vector2.zero, new Vector2(142f, 40f));
            flareGlow = AddImage(glowRect.gameObject, PixelSprites.White, Color.clear);

            // 5 Ember slots
            slotSockets = new Image[maxDisplaySlots];
            slotFills = new Image[maxDisplaySlots];

            float slotStartX = 6f;
            float slotY = -6f;
            float slotSpacing = 20f;

            for (int i = 0; i < maxDisplaySlots; i++)
            {
                Vector2 slotPos = new Vector2(slotStartX + i * slotSpacing, slotY);

                // Socket background
                var socketRect = CreateRect($"Slot_{i}_Socket", inner, slotPos, new Vector2(16f, 16f));
                slotSockets[i] = AddImage(socketRect.gameObject, PixelSprites.EmberEmpty, Color.white);

                // Ember crystal foreground
                var fillRect = CreateRect($"Slot_{i}_Fill", inner, slotPos, new Vector2(16f, 16f));
                slotFills[i] = AddImage(fillRect.gameObject, PixelSprites.EmberFull, Color.white);
            }

            // Overflow badge right next to the 5th slot
            overflowBadge = AddText("OverflowBadge", inner, font,
                new Vector2(slotStartX + maxDisplaySlots * slotSpacing + 2f, slotY + 2f),
                new Vector2(32f, 16f), 10, TextAnchor.MiddleLeft, badgeTextColor);
            overflowBadge.fontStyle = FontStyle.Bold;

            // Labels below slots
            modeLabel = AddText("ModeLabel", inner, font, new Vector2(6f, -24f), new Vector2(90f, 14f), 9,
                TextAnchor.MiddleLeft, dimTextColor);

            timerLabel = AddText("TimerLabel", inner, font, new Vector2(98f, -24f), new Vector2(38f, 14f), 9,
                TextAnchor.MiddleRight, flareTextColor);
            timerLabel.fontStyle = FontStyle.Bold;

            popupLabel = AddText("PopupLabel", inner, font, new Vector2(6f, -38f), new Vector2(120f, 14f), 10,
                TextAnchor.MiddleLeft, Color.clear);
            popupLabel.fontStyle = FontStyle.Bold;

            canvas.enabled = false;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text AddText(string name, Transform parent, Font font, Vector2 pos, Vector2 size, int fontSize,
            TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
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
