using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Premium, slender stamina gauge anchored at the bottom-center of the screen.
    /// Features:
    /// - Contextual motion: Smoothly slides up from below the screen upon sprinting/exertion,
    ///   and gracefully slides back down beneath the screen when rested and full.
    /// - Liquid dual-fill: Instant fill drop with smooth trailing ghost bar for physical weight.
    /// - Multi-layer aura: Ambient resting shadow, soft breathing glow, and energetic pulse on low stamina/exhaustion.
    /// - Clean rounded pill contour with internal masking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StaminaHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private PlayerStamina target;

        [Header("Layout — Bottom Center")]
        [Tooltip("Vertical resting position in pixels above the bottom edge of the screen.")]
        [SerializeField] private float bottomOffset = 26f;
        [Tooltip("Vertical hidden position in pixels below the bottom edge of the screen.")]
        [SerializeField] private float hiddenOffset = -35f;
        [Tooltip("Dimensions of the slender stamina pill (width x height).")]
        [SerializeField] private Vector2 barSize = new Vector2(150f, 5f);

        [Header("Colors & Styling")]
        [SerializeField] private Color staminaColor = new Color(0.96f, 0.72f, 0.24f, 1f);
        [SerializeField] private Color exhaustedColor = new Color(0.92f, 0.24f, 0.16f, 1f);
        [SerializeField] private Color ghostColor = new Color(1f, 0.92f, 0.70f, 0.70f);
        [SerializeField] private Color trackColor = new Color(0.05f, 0.07f, 0.09f, 0.92f);
        [SerializeField] private Color borderColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("Animation & Behavior")]
        [Tooltip("Seconds resting at full stamina before sliding down out of view.")]
        [SerializeField, Min(0.2f)] private float idleFadeDelay = 1.0f;
        [Tooltip("Speed when sliding up into view upon exertion.")]
        [SerializeField, Min(1f)] private float popUpSpeed = 5.0f;
        [Tooltip("Speed when sliding down below the screen upon resting.")]
        [SerializeField, Min(1f)] private float retreatSpeed = 2.5f;
        [Tooltip("Ghost bar catch-up delay in seconds.")]
        [SerializeField, Min(0.05f)] private float ghostDelay = 0.18f;
        [Tooltip("Ghost bar drain speed.")]
        [SerializeField, Min(0.1f)] private float ghostCatchupSpeed = 2.4f;

        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform panel;
        private CanvasGroup panelGroup;
        private Image shadowImage;
        private Image exhaustedGlow;
        private Image borderImage;
        private Image trackImage;
        private Image ghostImage;
        private Image fillImage;

        private float currentFill = 1f;
        private float ghostFill = 1f;
        private float ghostTimer;
        private float fullIdleTimer;
        private float currentVisibility = 1f;
        private float recoveryFlashTimer;
        private bool isExhausted;
        private bool isBuilt;
        private PlayerMotor2D motor;

        public PlayerStamina Target => target;
        public float DisplayedFill => currentFill;
        public float DisplayedGhostFill => ghostFill;
        public bool IsExhausted => isExhausted;
        public float PanelAlpha => panelGroup != null ? panelGroup.alpha : 0f;
        public float BottomOffset => bottomOffset;
        public float HiddenOffset => hiddenOffset;
        public float Visibility => currentVisibility;
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
            currentVisibility = 1f;
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
            motor = null;
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
                motor = target.GetComponent<PlayerMotor2D>();
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
                currentVisibility = 1f;
                if (panelGroup != null)
                {
                    panelGroup.alpha = 1f;
                }
            }
            else if (targetNorm >= 0.999f && currentFill < 0.999f)
            {
                recoveryFlashTimer = 0.35f;
            }

            currentFill = targetNorm;
        }

        private void HandleExhaustionChanged(bool exhausted)
        {
            isExhausted = exhausted;
            if (exhausted)
            {
                fullIdleTimer = 0f;
                currentVisibility = 1f;
                if (panelGroup != null)
                {
                    panelGroup.alpha = 1f;
                }
            }
        }

        public void Tick(float dt)
        {
            if (!isBuilt) return;

            bool isSprinting = motor != null && motor.IsSprinting;

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

            // Recovery gleam timer
            if (recoveryFlashTimer > 0f)
            {
                recoveryFlashTimer = Mathf.Max(0f, recoveryFlashTimer - dt);
            }

            bool isLowStamina = currentFill <= 0.25f || isExhausted;

            // Apply fill and color to stamina fill bar
            if (fillImage != null)
            {
                fillImage.fillAmount = currentFill;

                if (isExhausted)
                {
                    float pulse = Mathf.PingPong(Time.unscaledTime * 6f, 1f);
                    fillImage.color = Color.Lerp(exhaustedColor, staminaColor, pulse * 0.35f);
                }
                else if (isLowStamina)
                {
                    float lowPulse = Mathf.PingPong(Time.unscaledTime * 5f, 1f);
                    Color warnTint = Color.Lerp(staminaColor, exhaustedColor, 0.65f);
                    fillImage.color = Color.Lerp(staminaColor, warnTint, lowPulse);
                }
                else if (recoveryFlashTimer > 0f)
                {
                    float flashNorm = recoveryFlashTimer / 0.35f;
                    fillImage.color = Color.Lerp(staminaColor, Color.white, flashNorm * 0.45f);
                }
                else
                {
                    fillImage.color = staminaColor;
                }
            }

            if (ghostImage != null)
            {
                ghostImage.fillAmount = ghostFill;
            }

            // Dynamic aura glow behind the bar
            if (exhaustedGlow != null)
            {
                if (isExhausted)
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f;
                    exhaustedGlow.color = new Color(exhaustedColor.r, exhaustedColor.g, exhaustedColor.b, 0.45f * pulse);
                }
                else if (isLowStamina)
                {
                    float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                    exhaustedGlow.color = new Color(exhaustedColor.r, exhaustedColor.g, exhaustedColor.b, 0.25f * pulse);
                }
                else if (isSprinting || currentFill < 0.999f)
                {
                    float subtleBreath = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                    exhaustedGlow.color = new Color(staminaColor.r, staminaColor.g, staminaColor.b, 0.18f + 0.12f * subtleBreath);
                }
                else
                {
                    exhaustedGlow.color = Color.clear;
                }
            }

            // Contextual emergence: active when sprinting, depleted, recovering, or exhausted
            bool shouldBeVisible = (currentFill < 0.999f) || isExhausted || isSprinting;
            if (shouldBeVisible)
            {
                fullIdleTimer = 0f;
            }
            else
            {
                fullIdleTimer += dt;
            }

            float targetVisibility = (fullIdleTimer >= idleFadeDelay) ? 0f : 1f;
            float animSpeed = (targetVisibility > currentVisibility) ? popUpSpeed : retreatSpeed;
            currentVisibility = Mathf.MoveTowards(currentVisibility, targetVisibility, animSpeed * dt);

            // Smooth easing curve
            float curvedT = (targetVisibility > 0.5f)
                ? EaseOutCubic(currentVisibility)
                : EaseInCubic(currentVisibility);

            // Vertical position slide: smoothly rises from hiddenOffset (-35px) to bottomOffset (+26px)
            float currentY = Mathf.Lerp(hiddenOffset, bottomOffset, curvedT);
            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0f, currentY);

                // Subtle scale spring when sliding in
                float scale = Mathf.Lerp(0.90f, 1.0f, curvedT);
                panel.localScale = new Vector3(scale, scale, 1f);
            }

            // Alpha fade
            if (panelGroup != null)
            {
                panelGroup.alpha = Mathf.SmoothStep(0f, 1f, currentVisibility);
            }
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - Mathf.Clamp01(t);
            return 1f - inv * inv * inv;
        }

        private static float EaseInCubic(float t)
        {
            float val = Mathf.Clamp01(t);
            return val * val * val;
        }

        private void BuildHierarchy()
        {
            if (isBuilt) return;

            // Dedicated independent Canvas for Stamina HUD
            var canvasGo = new GameObject("StaminaHUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 1f;

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

            // Soft ambient drop shadow behind everything for clean depth
            var shadowGo = new GameObject("Ambient Shadow");
            var shadowRect = shadowGo.AddComponent<RectTransform>();
            shadowRect.SetParent(panel, false);
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-10f, -8f);
            shadowRect.offsetMax = new Vector2(10f, 6f);
            shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = PixelSprites.PillGlow;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = shadowColor;

            // Dynamic aura / exhaustion pulse behind the bar
            var glowGo = new GameObject("Exhaustion Glow");
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.SetParent(panel, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-14f, -12f);
            glowRect.offsetMax = new Vector2(14f, 12f);
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

            // Inner track background (Pill shape with Mask to clip fills into smooth pill curve)
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
            ghostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
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
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.color = staminaColor;

            isBuilt = true;
        }
    }
}
