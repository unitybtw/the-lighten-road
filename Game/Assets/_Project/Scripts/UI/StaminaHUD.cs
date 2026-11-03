using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>Stamina shown as woven bookcloth held between two archive binding clips.</summary>
    [DisallowMultipleComponent]
    public sealed class StaminaHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private PlayerStamina target;
        [SerializeField] private TMP_FontAsset fontAsset;
        [Header("Layout — bottom center, reference resolution 960x540")]
        [SerializeField] private float bottomOffset = 28f;
        [SerializeField] private float hiddenOffset = -34f;
        [SerializeField] private Vector2 barSize = new Vector2(154f, 7f);
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
        private RectTransform panel, tip;
        private CanvasGroup panelGroup;
        private Image fillImage, ghostImage, leftClip, rightClip;
        private TextMeshProUGUI label, status;
        private PlayerStamina boundTarget;
        private PlayerMotor2D motor;
        private float currentFill = 1f, ghostFill = 1f, ghostTimer, fullIdleTimer;
        private float currentVisibility = 1f, recoveryTimer;
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

        private void Awake()
        {
            // Legacy scenes used a three-pixel channel. The woven material needs a seven-pixel face.
            barSize.y = Mathf.Max(7f, barSize.y);
            BuildHierarchy();
        }

        private void OnEnable()
        {
            fullIdleTimer = ghostTimer = recoveryTimer = 0f;
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
            fullIdleTimer = ghostTimer = recoveryTimer = 0f;
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
                fullIdleTimer = 0f;
            }
            else if (value > currentFill)
            {
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
            recoveryTimer = Mathf.Max(0f, recoveryTimer - dt);
            if (ghostTimer > 0f) ghostTimer = Mathf.Max(0f, ghostTimer - dt);
            else ghostFill = Mathf.MoveTowards(ghostFill, currentFill, ghostCatchupSpeed * dt);
            if (ghostFill < currentFill) ghostFill = currentFill;

            bool running = motor != null && motor.IsSprinting;
            bool low = currentFill <= 0.25f;
            string state = IsExhausted ? "EXHAUSTED" : low ? "LOW" : "";
            if (status.text != state) status.text = state;
            Color ink = IsExhausted || low ? exhaustedColor : staminaColor;
            status.color = Color.Lerp(exhaustedColor, staminaColor, .45f);
            fillImage.color = ink;
            fillImage.fillAmount = currentFill;
            ghostImage.fillAmount = ghostFill;
            Color clipColor = Color.Lerp(Color.white, IsExhausted ? exhaustedColor : borderColor, IsExhausted ? .25f : .12f);
            leftClip.color = rightClip.color = Color.Lerp(clipColor, Color.white, recoveryTimer / .55f * .35f);
            tip.gameObject.SetActive(currentFill > 0.01f && currentFill < 0.999f);
            tip.anchoredPosition = new Vector2(-barSize.x / 2f + currentFill * barSize.x, 22f);

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
            var root = new GameObject("Stamina HUD / archive binding", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 1f;
            panel = Rect("Archive binding", root.transform, new Vector2(0f, bottomOffset), new Vector2(barSize.x + 24f, 32f));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.interactable = panelGroup.blocksRaycasts = false;
            if (fontAsset == null) fontAsset = Resources.Load<TMP_FontAsset>("UI/EmberHUD");
            Graphic("Binding shadow", panel, new Vector2(0f, 20f), new Vector2(barSize.x + 12f, barSize.y + 6f), StaminaBindingArt.Cloth, new Color(0, 0, 0, .65f));
            Graphic("Worn bookcloth", panel, new Vector2(0f, 22f), new Vector2(barSize.x, barSize.y + 4f), StaminaBindingArt.Cloth, Color.Lerp(Color.white, trackColor, .1f));
            var track = Rect("Woven reserve", panel, new Vector2(0f, 22f), barSize);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.gameObject.AddComponent<RectMask2D>();
            ghostImage = StretchGraphic("Spent endurance trail", track, ghostColor, true);
            fillImage = StretchGraphic("Endurance", track, staminaColor, true);
            tip = Graphic("Loose binding thread", panel, new Vector2(barSize.x / 2f, 22f), new Vector2(4f, 7f), StaminaBindingArt.Knot, Color.white).rectTransform;
            leftClip = Graphic("Left iron fastener", panel, new Vector2(-barSize.x / 2f - 2f, 22f), new Vector2(7f, 12f), StaminaBindingArt.Clip, Color.white);
            rightClip = Graphic("Right iron fastener", panel, new Vector2(barSize.x / 2f + 2f, 23f), new Vector2(7f, 12f), StaminaBindingArt.Clip, Color.white);
            rightClip.rectTransform.localScale = new Vector3(-1, 1, 1);
            label = Text("Stamina", panel, "STAMINA", TextAlignmentOptions.Left,
                new Vector2(-barSize.x / 2f + 29f, 0f), new Vector2(58f, 16f), 9.5f);
            status = Text("State", panel, "", TextAlignmentOptions.Right,
                new Vector2(barSize.x / 2f - 47f, 0f), new Vector2(94f, 16f), 10.5f);
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
            image.sprite = StaminaBindingArt.Weave;
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

        private TextMeshProUGUI Text(string name, Transform parent, string value, TextAlignmentOptions alignment,
            Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = fontSize;
            text.characterSpacing = .5f;
            text.text = value;
            text.color = new Color32(174, 159, 129, 255);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
