using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Pixel-art lantern HUD: lantern icon with live flame, segmented fuel bar with a drain trail,
    /// Dim / Flare mode label, low-fuel warning, and pickup feedback.
    /// Builds its own Screen Space Overlay canvas at runtime, so it needs no prefab or art import.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanternHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private LanternController target;

        [Header("Layout — reference resolution 640x360")]
        [SerializeField] private Vector2 screenMargin = new Vector2(10f, 10f);
        [SerializeField, Min(4)] private int segmentCount = 20;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] private float fillFollowSpeed = 6f;
        [SerializeField, Min(0f)] private float trailDelay = 0.45f;
        [SerializeField, Min(0.05f)] private float trailFollowSpeed = 1.6f;
        [SerializeField, Min(0.1f)] private float lowFuelBlinkRate = 2.5f;

        [Header("Palette")]
        [SerializeField] private Color panelColor = new Color32(14, 11, 16, 255);
        [SerializeField] private Color borderColor = new Color32(92, 64, 40, 255);
        [SerializeField] private Color fuelHighColor = new Color32(255, 184, 64, 255);
        [SerializeField] private Color fuelLowColor = new Color32(232, 64, 40, 255);
        [SerializeField] private Color trailColor = new Color32(255, 236, 200, 200);
        [SerializeField] private Color dimTextColor = new Color32(196, 160, 120, 255);
        [SerializeField] private Color flareTextColor = new Color32(255, 214, 110, 255);

        // Runtime-built hierarchy
        private Canvas canvas;
        private RectTransform panel;
        private Image panelBorder;
        private Image glow;
        private Image glass;
        private Image flame;
        private Image fill;
        private Image trail;
        private Image flash;
        private Text modeLabel;
        private Text percentLabel;
        private Text keyHint;
        private Text popupLabel;
        private Vector2 panelBasePosition;

        // State
        private float displayedFill;
        private float trailFill;
        private float trailHoldTimer;
        private float lastFuel = -1f;
        private float flashTimer;
        private float popupTimer;
        private float shakeTimer;
        private float flameFlareBlend;
        private bool subscribed;

        private const float FlashDuration = 0.3f;
        private const float PopupDuration = 0.9f;
        private const float ShakeDuration = 0.45f;

        public LanternController Target => target;
        public Canvas Canvas => canvas;
        public float DisplayedFill => displayedFill;
        public float TrailFill => trailFill;
        public string ModeText => modeLabel != null ? modeLabel.text : string.Empty;
        public string PercentText => percentLabel != null ? percentLabel.text : string.Empty;
        public bool IsShowingLowFuel { get; private set; }
        public bool IsShowingPickup => popupTimer > 0f;

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

        /// <summary>Bind to a specific lantern (useful for tests or multiple players).</summary>
        public void SetTarget(LanternController controller)
        {
            Unbind();
            target = controller;
            lastFuel = -1f;
            TryBind();
        }

        private void TryBind()
        {
            if (target == null)
                target = FindAnyObjectByType<LanternController>();
            if (target == null || subscribed || target.Fuel == null)
                return;

            target.FlareChanged += HandleFlareChanged;
            target.Fuel.FuelDepleted += HandleDepleted;
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
            if (target.Fuel != null)
                target.Fuel.FuelDepleted -= HandleDepleted;
            subscribed = false;
        }

        private void HandleFlareChanged(bool flared)
        {
            flashTimer = flared ? FlashDuration * 0.5f : 0f;
        }

        private void HandleDepleted()
        {
            shakeTimer = ShakeDuration;
        }

        private void Update()
        {
            if (!subscribed)
                TryBind();
            if (target == null || target.Fuel == null)
            {
                canvas.enabled = false;
                return;
            }

            canvas.enabled = true;
            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        /// <summary>Advance HUD animation. Public so Play Mode tests can step it deterministically.</summary>
        public void Tick(float deltaTime, float time)
        {
            LanternFuel fuel = target.Fuel;
            float normalized = Mathf.Clamp01(fuel.NormalizedFuel);
            float current = fuel.CurrentFuel;

            if (lastFuel < 0f)
            {
                // First frame: snap instead of animating from zero.
                displayedFill = trailFill = normalized;
            }
            else if (current > lastFuel + 0.5f)
            {
                ShowPickup(current - lastFuel);
                trailFill = Mathf.Max(trailFill, normalized);
            }
            else if (current < lastFuel - 0.01f)
            {
                trailHoldTimer = trailDelay;
            }
            lastFuel = current;

            // Main fill eases toward the real value; the trail lingers, then catches up.
            displayedFill = Mathf.MoveTowards(displayedFill, normalized,
                Mathf.Max(0.002f, Mathf.Abs(normalized - displayedFill)) * fillFollowSpeed * deltaTime * 4f);
            if (trailHoldTimer > 0f)
                trailHoldTimer -= deltaTime;
            else
                trailFill = Mathf.MoveTowards(trailFill, displayedFill, trailFollowSpeed * deltaTime * 0.5f);
            if (trailFill < displayedFill)
                trailFill = displayedFill;

            fill.fillAmount = SnapToPixels(displayedFill);
            trail.fillAmount = SnapToPixels(trailFill);

            bool flared = target.IsFlared;
            IsShowingLowFuel = target.IsLowFuel;
            float blink = (Mathf.Sin(time * lowFuelBlinkRate * Mathf.PI * 2f) + 1f) * 0.5f;

            // Fuel colour: warm amber that heats toward red as fuel drops; blinks when low.
            float heat = Mathf.InverseLerp(0.6f, 0.1f, normalized);
            Color fuelColor = Color.Lerp(fuelHighColor, fuelLowColor, heat);
            if (IsShowingLowFuel)
                fuelColor = Color.Lerp(fuelColor, Color.white * 0.35f + fuelLowColor * 0.65f, blink * 0.6f);
            fill.color = fuelColor;

            // Labels
            if (fuel.IsEmpty)
            {
                modeLabel.text = "EMPTY";
                modeLabel.color = Color.Lerp(fuelLowColor, dimTextColor, blink);
            }
            else if (IsShowingLowFuel)
            {
                modeLabel.text = flared ? "FLARE  LOW" : "DIM  LOW";
                modeLabel.color = Color.Lerp(fuelLowColor, flared ? flareTextColor : dimTextColor, blink);
            }
            else
            {
                modeLabel.text = flared ? "FLARE" : "DIM";
                modeLabel.color = flared ? flareTextColor : dimTextColor;
            }

            percentLabel.text = Mathf.CeilToInt(normalized * 100f) + "%";
            percentLabel.color = IsShowingLowFuel ? Color.Lerp(fuelLowColor, dimTextColor, blink) : dimTextColor;
            keyHint.text = "[" + target.ToggleFlareKey + "]";

            panelBorder.color = IsShowingLowFuel ? Color.Lerp(borderColor, fuelLowColor, blink) : borderColor;

            AnimateLantern(flared, normalized, deltaTime, time);
            AnimateFeedback(deltaTime);
        }

        private void AnimateLantern(bool flared, float normalized, float deltaTime, float time)
        {
            flameFlareBlend = Mathf.MoveTowards(flameFlareBlend, flared ? 1f : 0f, deltaTime * 6f);
            bool empty = normalized <= 0f;

            // Organic flicker from layered sines.
            float flicker = 0.85f + 0.1f * Mathf.Sin(time * 13.1f) + 0.05f * Mathf.Sin(time * 29.7f + 1.3f);
            if (IsShowingLowFuel)
                flicker *= 0.55f + 0.45f * Mathf.PerlinNoise(time * 9f, 0.37f);

            Color dimFlame = new Color32(255, 128, 48, 255);
            Color flareFlame = new Color32(255, 230, 140, 255);
            Color flameColor = Color.Lerp(dimFlame, flareFlame, flameFlareBlend);
            flameColor.a = empty ? 0f : Mathf.Clamp01(flicker);
            flame.color = flameColor;

            float flameScale = Mathf.Lerp(0.75f, 1.1f, flameFlareBlend) * (0.95f + 0.05f * flicker);
            flame.rectTransform.localScale = new Vector3(1f, empty ? 0f : flameScale, 1f);

            Color glassColor = Color.Lerp(new Color32(120, 60, 24, 255), new Color32(255, 196, 96, 255), flameFlareBlend);
            glassColor.a = empty ? 0.08f : Mathf.Lerp(0.25f, 0.55f, flameFlareBlend) * flicker;
            glass.color = glassColor;

            Color glowColor = Color.Lerp(new Color32(255, 110, 40, 255), new Color32(255, 170, 70, 255), flameFlareBlend);
            glowColor.a = empty ? 0f : Mathf.Lerp(0.14f, 0.4f, flameFlareBlend) * flicker;
            glow.color = glowColor;
            float glowScale = Mathf.Lerp(0.85f, 1.2f, flameFlareBlend) * (0.97f + 0.03f * flicker);
            glow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
        }

        private void AnimateFeedback(float deltaTime)
        {
            // White flash over the bar (pickup / flare ignition)
            if (flashTimer > 0f)
                flashTimer -= deltaTime;
            flash.color = new Color(1f, 0.95f, 0.8f, Mathf.Clamp01(flashTimer / FlashDuration) * 0.85f);

            // Floating "+N" popup
            if (popupTimer > 0f)
            {
                popupTimer -= deltaTime;
                float t = 1f - Mathf.Clamp01(popupTimer / PopupDuration);
                popupLabel.rectTransform.anchoredPosition = new Vector2(popupLabel.rectTransform.anchoredPosition.x,
                    -18f + Mathf.Round(t * 10f));
                Color c = popupLabel.color;
                c.a = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                popupLabel.color = c;
            }
            else
            {
                popupLabel.color = Color.clear;
            }

            // Shake on depletion — whole-pixel offsets keep the pixel look crisp.
            Vector2 offset = Vector2.zero;
            if (shakeTimer > 0f)
            {
                shakeTimer -= deltaTime;
                float strength = Mathf.Ceil(2f * Mathf.Clamp01(shakeTimer / ShakeDuration));
                offset = new Vector2(Mathf.Round(Random.Range(-strength, strength)),
                    Mathf.Round(Random.Range(-strength, strength)));
            }
            panel.anchoredPosition = panelBasePosition + offset;
        }

        private void ShowPickup(float amount)
        {
            flashTimer = FlashDuration;
            popupTimer = PopupDuration;
            popupLabel.text = "+" + Mathf.RoundToInt(amount);
            popupLabel.color = flareTextColor;
        }

        private float SnapToPixels(float value)
        {
            // Bar is 104 reference pixels wide; snap so the edge moves one pixel at a time.
            const float barPixels = 104f;
            return Mathf.Round(value * barPixels) / barPixels;
        }

        // ------------------------------------------------------------------ hierarchy

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
            Sprite white = PixelSprites.White;

            // Panel: outer border + inner fill (2px border, drawn as two layered rects)
            panelBasePosition = new Vector2(screenMargin.x, -screenMargin.y);
            panel = CreateRect("Panel", canvasGo.transform, panelBasePosition, new Vector2(156f, 40f));
            panelBorder = AddImage(panel.gameObject, white, borderColor);
            var inner = CreateRect("Inner", panel, new Vector2(2f, -2f), new Vector2(152f, 36f));
            AddImage(inner.gameObject, white, panelColor);
            // Top highlight line for a bevelled feel
            var bevel = CreateRect("Bevel", inner, Vector2.zero, new Vector2(152f, 1f));
            AddImage(bevel.gameObject, white, new Color(1f, 0.85f, 0.6f, 0.08f));

            // Lantern icon (11x15 pixel art at 2x = 22x30)
            var iconRoot = CreateRect("Lantern", inner, new Vector2(5f, -3f), new Vector2(22f, 30f));
            glow = AddImage(CreateCentered("Glow", iconRoot, new Vector2(0f, -3f), new Vector2(36f, 36f)).gameObject,
                PixelSprites.Glow, Color.clear);
            glass = AddImage(CreateRect("Glass", iconRoot, new Vector2(6f, -10f), new Vector2(10f, 12f)).gameObject,
                white, Color.clear);
            var flameRect = CreateRect("Flame", iconRoot, new Vector2(6f, -10f), new Vector2(10f, 12f));
            flameRect.pivot = new Vector2(0.5f, 0f);
            flameRect.anchoredPosition = new Vector2(11f, -22f);
            flame = AddImage(flameRect.gameObject, PixelSprites.Flame, Color.clear);
            AddImage(CreateRect("Frame", iconRoot, Vector2.zero, new Vector2(22f, 30f)).gameObject,
                PixelSprites.LanternFrame, Color.white);

            // Mode label + percentage + key hint
            modeLabel = AddText("Mode", inner, font, new Vector2(33f, -2f), new Vector2(80f, 14f), 10,
                TextAnchor.MiddleLeft, dimTextColor);
            percentLabel = AddText("Percent", inner, font, new Vector2(98f, -2f), new Vector2(34f, 14f), 9,
                TextAnchor.MiddleRight, dimTextColor);
            keyHint = AddText("Key", inner, font, new Vector2(132f, -2f), new Vector2(18f, 14f), 8,
                TextAnchor.MiddleRight, new Color32(120, 100, 84, 255));

            // Fuel bar: outline, background, trail, fill, flash, segment dividers
            var barOutline = CreateRect("BarOutline", inner, new Vector2(32f, -19f), new Vector2(108f, 12f));
            AddImage(barOutline.gameObject, white, new Color32(6, 4, 8, 255));
            var bar = CreateRect("Bar", barOutline, new Vector2(2f, -2f), new Vector2(104f, 8f));
            AddImage(bar.gameObject, white, new Color32(36, 26, 22, 255));
            trail = AddFilled(CreateRect("Trail", bar, Vector2.zero, new Vector2(104f, 8f)).gameObject, white, trailColor);
            fill = AddFilled(CreateRect("Fill", bar, Vector2.zero, new Vector2(104f, 8f)).gameObject, white, fuelHighColor);
            var shine = CreateRect("Shine", bar, Vector2.zero, new Vector2(104f, 2f));
            AddImage(shine.gameObject, white, new Color(1f, 1f, 1f, 0.12f));
            flash = AddImage(CreateRect("Flash", bar, Vector2.zero, new Vector2(104f, 8f)).gameObject, white, Color.clear);

            float segmentWidth = 104f / segmentCount;
            for (int i = 1; i < segmentCount; i++)
            {
                var divider = CreateRect("Div" + i, bar, new Vector2(Mathf.Round(i * segmentWidth), 0f), new Vector2(1f, 8f));
                AddImage(divider.gameObject, white, new Color32(14, 10, 12, 170));
            }

            popupLabel = AddText("Popup", panel, font, new Vector2(160f, -18f), new Vector2(40f, 14f), 10,
                TextAnchor.MiddleLeft, Color.clear);
            popupLabel.GetComponent<Outline>().enabled = true;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform CreateCentered(string name, Transform parent, Vector2 offset, Vector2 size)
        {
            var rect = CreateRect(name, parent, Vector2.zero, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            return rect;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image AddFilled(GameObject go, Sprite sprite, Color color)
        {
            var image = AddImage(go, sprite, color);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            return image;
        }

        private static Text AddText(string name, Transform parent, Font font, Vector2 topLeft, Vector2 size,
            int fontSize, TextAnchor anchor, Color color)
        {
            var rect = CreateRect(name, parent, topLeft, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
