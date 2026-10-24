using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>A quiet, weathered endurance instrument. Presentation never spends stamina.</summary>
    [DisallowMultipleComponent]
    public sealed class StaminaHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private PlayerStamina target;
        [SerializeField] private TMP_FontAsset fontAsset;
        [Header("Layout — bottom center, reference resolution 960x540")]
        [SerializeField] private float bottomOffset = 28f;
        [SerializeField] private float hiddenOffset = -34f;
        [SerializeField] private Vector2 barSize = new Vector2(154f, 3f);
        [Header("Weathered ivory, iron and fired clay")]
        [SerializeField] private Color staminaColor = new Color32(194, 178, 139, 255);
        [SerializeField] private Color exhaustedColor = new Color32(170, 100, 75, 255);
        [SerializeField] private Color ghostColor = new Color32(107, 97, 76, 255);
        [SerializeField] private Color trackColor = new Color32(25, 26, 23, 255);
        [SerializeField] private Color borderColor = new Color32(76, 72, 60, 255);
        [Header("Motion")]
        [SerializeField] private bool reduceMotion;
        [SerializeField, Min(0.2f)] private float idleFadeDelay = 1.1f;
        [SerializeField, Min(1f)] private float popUpSpeed = 6f;
        [SerializeField, Min(1f)] private float retreatSpeed = 3f;
        [SerializeField, Min(0.05f)] private float ghostDelay = 0.14f;
        [SerializeField, Min(0.1f)] private float ghostCatchupSpeed = 2.4f;

        private Canvas canvas;
        private RectTransform panel, tip, sheen;
        private CanvasGroup panelGroup;
        private Image frame, fillImage, ghostImage, breath, sheenImage;
        private TextMeshProUGUI label, status;
        private PlayerStamina boundTarget;
        private PlayerMotor2D motor;
        private float currentFill = 1f, ghostFill = 1f, ghostTimer, fullIdleTimer;
        private float currentVisibility = 1f, recoveryTimer, recovering;
        private float phase;
        private float safeBottom;
        private float bindRetryTimer;
        private Rect lastSafeArea;
        private int lastHeight;

        public PlayerStamina Target => target;
        public Canvas Canvas => canvas;
        public float DisplayedFill => currentFill;
        public float DisplayedGhostFill => ghostFill;
        public bool IsExhausted { get; private set; }
        public float PanelAlpha => panelGroup != null ? panelGroup.alpha : 0f;
        public float BottomOffset => bottomOffset;
        public float HiddenOffset => hiddenOffset;
        public float Visibility => currentVisibility;
        public Vector2 BarSize => barSize;
        public Vector2 AnchoredPosition => panel != null ? panel.anchoredPosition : Vector2.zero;
        public string StatusText => status != null ? status.text : string.Empty;
        public bool ReduceMotion { get => reduceMotion; set => reduceMotion = value; }

        private void Awake() => BuildHierarchy();

        private void OnEnable()
        {
            fullIdleTimer = ghostTimer = recoveryTimer = recovering = phase = 0f;
            currentVisibility = 1f;
            TryBind();
            Tick(0f);
        }

        private void OnDisable()
        {
            Unbind();
            if (canvas != null) canvas.enabled = false;
        }

        private void Update()
        {
            if (boundTarget == null)
            {
                bindRetryTimer -= Time.unscaledDeltaTime;
                if (bindRetryTimer <= 0f)
                {
                    bindRetryTimer = 0.35f;
                    TryBind();
                }
            }
            else if (boundTarget != target)
            {
                TryBind();
            }

            Tick(Time.unscaledDeltaTime);
        }

        public void SetTarget(PlayerStamina newTarget)
        {
            Unbind();
            target = newTarget;
            fullIdleTimer = ghostTimer = recoveryTimer = recovering = 0f;
            currentVisibility = 1f;
            TryBind();
            Tick(0f);
        }

        private void TryBind()
        {
            if (target == null)
            {
                var activeMotor = PlayerMotor2D.ActiveInstance;
                target = activeMotor != null ? activeMotor.Stamina : FindAnyObjectByType<PlayerStamina>();
            }
            if (boundTarget == target && boundTarget != null) return;
            Unbind();
            boundTarget = target;
            if (boundTarget == null)
            {
                if (canvas != null) canvas.enabled = false;
                return;
            }
            motor = boundTarget.GetComponent<PlayerMotor2D>();
            boundTarget.StaminaChanged += HandleStaminaChanged;
            boundTarget.ExhaustionChanged += HandleExhaustionChanged;
            currentFill = ghostFill = Mathf.Clamp01(boundTarget.NormalizedStamina);
            IsExhausted = boundTarget.IsExhausted;
            if (canvas != null) canvas.enabled = true;
        }

        private void Unbind()
        {
            if (boundTarget != null)
            {
                boundTarget.StaminaChanged -= HandleStaminaChanged;
                boundTarget.ExhaustionChanged -= HandleExhaustionChanged;
            }
            boundTarget = null;
            motor = null;
        }

        private void HandleStaminaChanged(float current, float max)
        {
            float value = max > 0.001f ? Mathf.Clamp01(current / max) : 0f;
            if (value < currentFill)
            {
                ghostTimer = ghostDelay;
                recovering = 0f;
                fullIdleTimer = 0f;
            }
            else if (value > currentFill)
            {
                recovering = 0.35f;
                if (value >= 0.999f && currentFill < 0.999f) recoveryTimer = 0.55f;
            }
            currentFill = value;
        }

        private void HandleExhaustionChanged(bool exhausted)
        {
            IsExhausted = exhausted;
            fullIdleTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (panel == null || boundTarget == null) return;
            float dt = Mathf.Max(0f, deltaTime);
            phase += dt;
            recoveryTimer = Mathf.Max(0f, recoveryTimer - dt);
            recovering = Mathf.Max(0f, recovering - dt);
            if (ghostTimer > 0f) ghostTimer = Mathf.Max(0f, ghostTimer - dt);
            else ghostFill = Mathf.MoveTowards(ghostFill, currentFill, ghostCatchupSpeed * dt);
            if (ghostFill < currentFill) ghostFill = currentFill;

            bool running = motor != null && motor.IsSprinting;
            bool low = currentFill <= 0.25f;
            string state = IsExhausted ? "EXHAUSTED" : low ? "LOW" : running ? "SPRINTING"
                : recovering > 0f ? "RECOVERING" : currentFill >= 0.999f ? "READY" : "REST";
            if (status.text != state) status.text = state;
            Color ink = IsExhausted || low ? exhaustedColor : staminaColor;
            status.color = IsExhausted || low ? exhaustedColor : new Color32(137, 128, 106, 255);
            fillImage.color = Color.Lerp(ink, new Color32(219, 206, 170, 255), recoveryTimer * 0.2f);
            fillImage.fillAmount = currentFill;
            ghostImage.fillAmount = ghostFill;
            frame.color = Color.Lerp(borderColor, exhaustedColor, IsExhausted ? 0.45f : 0f);
            float breathAlpha = reduceMotion || !IsExhausted ? 0.8f : 0.65f + 0.25f * (0.5f + 0.5f * Mathf.Sin(phase * 5f));
            breath.color = new Color(ink.r, ink.g, ink.b, breathAlpha);
            tip.gameObject.SetActive(currentFill > 0.01f && currentFill < 0.999f);
            tip.anchoredPosition = new Vector2(-barSize.x / 2f + currentFill * barSize.x, 6f);
            sheen.gameObject.SetActive(!reduceMotion && recoveryTimer > 0f);
            sheen.anchoredPosition = new Vector2(Mathf.Lerp(-barSize.x * 0.15f, barSize.x * 1.15f, 1f - recoveryTimer / 0.55f), 0f);
            sheenImage.color = new Color(0.85f, 0.79f, 0.64f, 0.22f * recoveryTimer / 0.55f);

            bool active = currentFill < 0.999f || IsExhausted || running || recoveryTimer > 0f;
            fullIdleTimer = active ? 0f : fullIdleTimer + dt;
            float wanted = fullIdleTimer >= idleFadeDelay ? 0f : 1f;
            currentVisibility = reduceMotion ? wanted : Mathf.MoveTowards(currentVisibility, wanted,
                (wanted > currentVisibility ? popUpSpeed : retreatSpeed) * dt);
            float eased = Mathf.SmoothStep(0f, 1f, currentVisibility);
            UpdateSafeArea();
            panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(hiddenOffset, bottomOffset + safeBottom, eased));
            panelGroup.alpha = eased;
        }

        private void UpdateSafeArea()
        {
            if (Screen.safeArea == lastSafeArea && Screen.height == lastHeight) return;
            lastSafeArea = Screen.safeArea;
            lastHeight = Screen.height;
            safeBottom = lastHeight > 0 ? lastSafeArea.yMin * 540f / lastHeight : 0f;
        }

        private void BuildHierarchy()
        {
            var root = new GameObject("Stamina HUD / etched reserve", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 1f;
            panel = Rect("Endurance instrument", root.transform, new Vector2(0f, bottomOffset), new Vector2(barSize.x + 32f, 28f));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.interactable = panelGroup.blocksRaycasts = false;
            if (fontAsset == null) fontAsset = Resources.Load<TMP_FontAsset>("UI/EmberHUD");
            var shadow = Graphic("Recess shadow", panel, new Vector2(0f, 5f), new Vector2(barSize.x + 11f, 11f), PixelSprites.StaminaPlate, new Color(0f, 0f, 0f, 0.35f));
            shadow.raycastTarget = false;
            frame = Graphic("Chipped iron edge", panel, new Vector2(0f, 6f), new Vector2(barSize.x + 8f, 7f), PixelSprites.StaminaPlate, borderColor);
            var track = Rect("Engraved channel", panel, new Vector2(0f, 6f), barSize);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.gameObject.AddComponent<RectMask2D>();
            StretchGraphic("Soot track", track, trackColor);
            ghostImage = StretchGraphic("Spent endurance trail", track, ghostColor, true);
            fillImage = StretchGraphic("Endurance", track, staminaColor, true);
            for (int i = 1; i < 4; i++) Graphic("Etch " + i, panel,
                new Vector2(-barSize.x / 2f + barSize.x * i / 4f, 6f), new Vector2(0.7f, barSize.y), PixelSprites.White, new Color(0.08f, 0.09f, 0.075f, 0.8f));
            tip = Graphic("Remaining edge", panel, new Vector2(barSize.x / 2f, 6f), new Vector2(0.7f, barSize.y + 1f), PixelSprites.White,
                new Color32(219, 201, 156, 210)).rectTransform;
            breath = Graphic("Breath engraving", panel, new Vector2(-barSize.x / 2f - 13f, 11f), new Vector2(11f, 13f), PixelSprites.BreathMark, staminaColor);
            label = Text("Stamina", panel, "STAMINA", TextAlignmentOptions.Left);
            status = Text("State", panel, "READY", TextAlignmentOptions.Right);
            sheen = Rect("Recovery glint", track, Vector2.zero, new Vector2(barSize.x * 0.12f, barSize.y));
            sheen.anchorMin = sheen.anchorMax = new Vector2(0f, 0.5f);
            sheen.pivot = new Vector2(0.5f, 0.5f);
            sheenImage = StretchGraphic("Glint", sheen, Color.clear);
        }

        private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Image Graphic(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            var rect = Rect(name, parent, position, size);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Image StretchGraphic(string name, RectTransform parent, Color color, bool filled = false)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PixelSprites.White;
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
            }
            return image;
        }

        private TextMeshProUGUI Text(string name, Transform parent, string value, TextAlignmentOptions alignment)
        {
            var rect = Rect(name, parent, new Vector2(0f, 15f), new Vector2(barSize.x, 12f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = 6.5f;
            text.characterSpacing = 2f;
            text.text = value;
            text.color = new Color32(137, 128, 106, 255);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
