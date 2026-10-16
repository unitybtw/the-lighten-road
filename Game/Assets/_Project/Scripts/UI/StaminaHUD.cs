using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Slender, responsive stamina gauge anchored beneath the Ember Reserve.
    /// Features:
    /// - Immediate fill drop with smooth trailing ghost bar for satisfying physical weight.
    /// - Vitality amber fill transitioning to warning pulse when exhausted.
    /// - Soft resting fade when full and idle, snapping to full opacity when running.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaminaHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private PlayerStamina target;

        [Header("Layout — Bottom Center")]
        [Tooltip("Vertical offset in pixels from the bottom edge of the screen.")]
        [SerializeField] private float bottomOffset = 24f;
        [Tooltip("Dimensions of the slender stamina pill (width x height).")]
        [SerializeField] private Vector2 barSize = new Vector2(150f, 5f);

        [Header("Colors & Styling")]
        [SerializeField] private Color staminaColor = new Color(0.94f, 0.68f, 0.24f, 1f);
        [SerializeField] private Color exhaustedColor = new Color(0.90f, 0.22f, 0.14f, 1f);
        [SerializeField] private Color ghostColor = new Color(1f, 0.90f, 0.65f, 0.70f);
        [SerializeField] private Color trackColor = new Color(0.04f, 0.06f, 0.08f, 0.88f);
        [SerializeField] private Color borderColor = new Color(0.18f, 0.22f, 0.26f, 0.95f);

        [Header("Behavior")]
        [Tooltip("Fade alpha when resting at full stamina (0 = fully contextual fade-out).")]
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0f;
        [Tooltip("Seconds at full stamina before fading out.")]
        [SerializeField, Min(0.2f)] private float idleFadeDelay = 1.2f;
        [Tooltip("Ghost bar catch-up delay in seconds.")]
        [SerializeField, Min(0.05f)] private float ghostDelay = 0.22f;
        [Tooltip("Ghost bar drain speed.")]
        [SerializeField, Min(0.1f)] private float ghostCatchupSpeed = 2.2f;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform panel;
        private CanvasGroup panelGroup;
        private Image trackImage;
        private Image borderImage;
        private Image ghostImage;
        private Image fillImage;
        private Image exhaustedGlow;

        private float currentFill = 1f;
        private float ghostFill = 1f;
        private float ghostTimer;
        private float fullIdleTimer;
        private bool isExhausted;
        private bool isBuilt;

        public PlayerStamina Target => target;
        public float DisplayedFill => currentFill;
        public float DisplayedGhostFill => ghostFill;
        public bool IsExhausted => isExhausted;
        public float PanelAlpha => panelGroup != null ? panelGroup.alpha : 0f;
        public float BottomOffset => bottomOffset;
        public Vector2 BarSize => barSize;
        public Vector2 AnchoredPosition => panel != null ? panel.anchoredPosition : Vector2.zero;

        private void Awake()
        {
            BuildHierarchy();
        }

        private void OnEnable()
        {
            fullIdleTimer = 0f;
            currentFill = ghostFill = 1f;
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (target == null)
            {
                TryBind();
            }

            Tick(Time.unscaledDeltaTime);
        }

        public void SetTarget(PlayerStamina newTarget)
        {
            Unbind();
            target = newTarget;
            TryBind();
        }

        private void TryBind()
        {
            if (target == null)
            {
                target = FindAnyObjectByType<PlayerStamina>();
            }

            if (target != null)
            {
                target.StaminaChanged += HandleStaminaChanged;
                target.ExhaustionChanged += HandleExhaustionChanged;
                isExhausted = target.IsExhausted;
                currentFill = ghostFill = target.NormalizedStamina;
            }
        }

        private void Unbind()
        {
            if (target != null)
            {
                target.StaminaChanged -= HandleStaminaChanged;
                target.ExhaustionChanged -= HandleExhaustionChanged;
            }
        }

        private void HandleStaminaChanged(float current, float max)
        {
            float targetNorm = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;

            if (targetNorm < currentFill)
            {
                ghostTimer = ghostDelay;
                fullIdleTimer = 0f;
                if (panelGroup != null)
                {
                    panelGroup.alpha = 1f;
                }
            }

            currentFill = targetNorm;
        }

        private void HandleExhaustionChanged(bool exhausted)
        {
            isExhausted = exhausted;
            if (exhausted && panelGroup != null)
            {
                panelGroup.alpha = 1f;
            }
        }

        public void Tick(float dt)
        {
            if (!isBuilt) return;

            // Ghost bar trailing animation
            if (ghostTimer > 0f)
            {
                ghostTimer -= dt;
            }
            else if (ghostFill > currentFill)
            {
                ghostFill = Mathf.MoveTowards(ghostFill, currentFill, ghostCatchupSpeed * dt);
            }
            else
            {
                ghostFill = currentFill;
            }

            // Apply fills to UI images
            if (fillImage != null)
            {
                fillImage.fillAmount = currentFill;
                fillImage.color = isExhausted
                    ? Color.Lerp(exhaustedColor, staminaColor, Mathf.PingPong(Time.unscaledTime * 5f, 1f) * 0.3f)
                    : staminaColor;
            }

            if (ghostImage != null)
            {
                ghostImage.fillAmount = ghostFill;
            }

            // Exhaustion glow pulsing
            if (exhaustedGlow != null)
            {
                if (isExhausted)
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
                    exhaustedGlow.color = new Color(exhaustedColor.r, exhaustedColor.g, exhaustedColor.b, 0.4f * pulse);
                }
                else
                {
                    exhaustedGlow.color = Color.clear;
                }
            }

            // Contextual fade: fully visible when consuming or recovering; fades out when rested at full
            if (currentFill >= 0.999f && !isExhausted)
            {
                fullIdleTimer += dt;
            }
            else
            {
                fullIdleTimer = 0f;
            }

            float targetAlpha = fullIdleTimer >= idleFadeDelay ? idleAlpha : 1f;
            if (panelGroup != null)
            {
                float fadeSpeed = targetAlpha > panelGroup.alpha ? 8f : 2.0f;
                panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetAlpha, fadeSpeed * dt);
            }
        }

        private void BuildHierarchy()
        {
            if (isBuilt) return;

            // Look for existing HUD Canvas (e.g. from LanternHUD) or create one
            var existingCanvas = FindAnyObjectByType<LanternHUD>()?.Canvas;
            if (existingCanvas != null)
            {
                canvas = existingCanvas;
            }
            else
            {
                var canvasGo = new GameObject("StaminaHUD Canvas");
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 99;
                scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(960f, 540f);
                scaler.matchWidthOrHeight = 1f;
            }

            // Outer panel anchored Bottom-Center
            var panelGo = new GameObject("Stamina Gauge");
            panel = panelGo.AddComponent<RectTransform>();
            panel.SetParent(canvas.transform, false);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, bottomOffset);
            panel.sizeDelta = barSize + new Vector2(6f, 6f);

            panelGroup = panelGo.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;

            // Exhaustion warning glow behind the bar (Pill glow aura)
            var glowGo = new GameObject("Exhaustion Glow");
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.SetParent(panel, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-12f, -10f);
            glowRect.offsetMax = new Vector2(12f, 10f);
            exhaustedGlow = glowGo.AddComponent<Image>();
            exhaustedGlow.sprite = PixelSprites.PillGlow;
            exhaustedGlow.type = Image.Type.Sliced;
            exhaustedGlow.color = Color.clear;

            // Outer dark frame / border (Pill shape)
            var borderGo = new GameObject("Border");
            var borderRect = borderGo.AddComponent<RectTransform>();
            borderRect.SetParent(panel, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderImage = borderGo.AddComponent<Image>();
            borderImage.sprite = PixelSprites.PillBorder;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = borderColor;

            // Inner track background (Pill shape with Mask to clip fills into pill curve)
            var trackGo = new GameObject("Track");
            var trackRect = trackGo.AddComponent<RectTransform>();
            trackRect.SetParent(borderRect, false);
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(1f, 1f);
            trackRect.offsetMax = new Vector2(-1f, -1f);
            trackImage = trackGo.AddComponent<Image>();
            trackImage.sprite = PixelSprites.Pill;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = trackColor;

            var mask = trackGo.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Trailing ghost bar (clipped inside pill track)
            var ghostGo = new GameObject("Ghost Fill");
            var ghostRect = ghostGo.AddComponent<RectTransform>();
            ghostRect.SetParent(trackRect, false);
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.one;
            ghostRect.sizeDelta = Vector2.zero;
            ghostImage = ghostGo.AddComponent<Image>();
            ghostImage.sprite = PixelSprites.White;
            ghostImage.type = Image.Type.Filled;
            ghostImage.fillMethod = Image.FillMethod.Horizontal;
            ghostImage.fillOrigin = 0;
            ghostImage.fillAmount = 1f;
            ghostImage.color = ghostColor;

            // Main stamina fill bar (clipped inside pill track)
            var fillGo = new GameObject("Stamina Fill");
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.SetParent(trackRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillImage = fillGo.AddComponent<Image>();
            fillImage.sprite = PixelSprites.White;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.color = staminaColor;

            isBuilt = true;
        }
    }
}
