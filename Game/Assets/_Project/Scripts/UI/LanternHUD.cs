using System.Globalization;
using TheLightenRoad.Lantern;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Compact ember reserve: five two-charge stones, exact charge count, overflow,
    /// and a timed scout indicator. Presentation never mutates the lantern inventory.
    /// Uses the project's existing pixel sprites with subtle, unscaled UI animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanternHUD : MonoBehaviour
    {
        [Header("Target (auto-found when empty)")]
        [SerializeField] private LanternController target;

        [Header("Layout — reference resolution 960x540")]
        [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);
        [SerializeField, Range(1, 5)] private int maxDisplaySlots = 5;
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Motion")]
        [Tooltip("Keep status changes immediate and suppress decorative movement / pulsing.")]
        [SerializeField] private bool reduceMotion;

        private const float Width = 210f;
        private const float Height = 54f;
        private const float TrackWidth = Width;
        private const float FeedbackDuration = 1.2f;
        private static readonly Color Gold = new Color32(236, 184, 107, 255);
        private static readonly Color Ivory = new Color32(250, 234, 202, 255);
        private static readonly Color Muted = new Color32(142, 146, 142, 255);
        private static readonly Color Bronze = new Color32(77, 66, 52, 255);
        private static readonly Color Warning = new Color32(237, 130, 91, 255);

        private sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Glow;
            public Image Fill;
            public Image Ghost;
            public Image[] Pips;
            public int Charges = -1;
            public float Reaction;
            public bool Gained;
        }

        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup panelGroup;
        private RectTransform panel;
        private Slot[] slots;
        private TextMeshProUGUI chargeLabel;
        private TextMeshProUGUI overflowBadge;
        private TextMeshProUGUI modeLabel;
        private TextMeshProUGUI detailLabel;
        private TextMeshProUGUI keyLabel;
        private TextMeshProUGUI popupLabel;
        private Image progress;
        private Image progressTip;
        private Image keyRim;
        private int lastCharges = -1;
        private int lastTimerTenths = -1;
        private int lastOverflow = -1;
        private int lastMode = -1;
        private string lastKey;
        private float entrance;
        private float popupTimer;
        private float countReaction;
        private float lastScoutDuration = -1f;
        private bool subscribed;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        public LanternController Target => target;
        public Canvas Canvas => canvas;
        public string ModeText => modeLabel != null ? modeLabel.text : string.Empty;
        public string OverflowText => overflowBadge != null ? overflowBadge.text : string.Empty;
        public string PickupText => popupLabel != null ? popupLabel.text : string.Empty;
        public bool IsShowingPickup => popupTimer > 0f;
        public bool IsShowingSpend => countReaction > 0f && !lastChangeWasGain;
        public int MaxDisplaySlots => maxDisplaySlots;
        public int DisplayedCharges => Mathf.Max(0, lastCharges);
        public float ScoutProgress => progress != null ? progress.fillAmount : 0f;
        public bool ReduceMotion { get => reduceMotion; set => reduceMotion = value; }
        public Sprite GetSlotSprite(int index) => ValidSlot(index) ? slots[index].Fill.sprite : null;
        public Color GetSlotColor(int index) => ValidSlot(index) ? slots[index].Fill.color : Color.clear;
        private bool lastChangeWasGain;

        private bool ValidSlot(int index) => slots != null && index >= 0 && index < slots.Length;

        private void Awake()
        {
            maxDisplaySlots = Mathf.Clamp(maxDisplaySlots, 1, 5);
            BuildHierarchy();
        }

        private void OnEnable()
        {
            entrance = 0f;
            lastCharges = -1;
            popupTimer = countReaction = 0f;
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
            if (canvas != null)
                canvas.enabled = false;
        }

        public void SetTarget(LanternController controller)
        {
            Unbind();
            target = controller;
            lastCharges = -1;
            lastMode = -1;
            popupTimer = 0f;
            countReaction = 0f;
            foreach (Slot slot in slots)
            {
                slot.Charges = -1;
                slot.Reaction = 0f;
            }
            TryBind();
        }

        private void TryBind()
        {
            if (target == null)
                target = FindAnyObjectByType<LanternController>();
            if (target == null || subscribed || target.Fuel == null)
                return;
            subscribed = true;
        }

        private void Unbind()
        {
            subscribed = false;
        }

        private void Update()
        {
            if (!subscribed) TryBind();
            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        public void Tick(float deltaTime, float time)
        {
            bool available = isActiveAndEnabled && target != null && target.Fuel != null;
            canvas.enabled = available;
            if (!available) return;

            float dt = Mathf.Max(0f, deltaTime);
            LanternFuel fuel = target.Fuel;
            int charges = fuel.TotalCharges;
            if (charges != lastCharges)
                UpdateInventory(charges);

            UpdateStatus(charges);
            AnimateSlots(dt, time);
            AnimateFeedback(dt);
            AnimateEntrance(dt);
            UpdateSafeArea();
        }

        private void UpdateInventory(int charges)
        {
            if (lastCharges >= 0)
            {
                int difference = charges - lastCharges;
                lastChangeWasGain = difference > 0;
                countReaction = 0.5f;
                if (difference > 0)
                {
                    popupTimer = FeedbackDuration;
                    if (difference % 2 == 0)
                    {
                        int embers = difference / 2;
                        popupLabel.text = $"+{embers} {(embers == 1 ? "EMBER" : "EMBERS")}";
                    }
                    else
                    {
                        popupLabel.text = $"+{difference} {(difference == 1 ? "CHARGE" : "CHARGES")}";
                    }
                }
            }

            chargeLabel.text = charges.ToString("00");
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                int next = Mathf.Clamp(charges - i * 2, 0, 2);
                if (slot.Charges == next) continue;
                if (slot.Charges >= 0)
                {
                    slot.Ghost.sprite = slot.Fill.sprite;
                    slot.Gained = next > slot.Charges;
                    slot.Reaction = 0.55f;
                }
                slot.Charges = next;
                slot.Fill.sprite = next == 1 ? PixelSprites.EmberHalf : PixelSprites.EmberFull;
                slot.Fill.color = next > 0 ? Color.white : Color.clear;
                for (int p = 0; p < 2; p++)
                    slot.Pips[p].color = p < next ? Gold : new Color32(46, 48, 47, 255);
            }

            int overflow = Mathf.Max(0, Mathf.CeilToInt(charges / 2f) - maxDisplaySlots);
            if (lastOverflow != overflow)
            {
                overflowBadge.text = overflow > 0 ? "+" + overflow : string.Empty;
                lastOverflow = overflow;
            }
            lastCharges = charges;
        }

        private void UpdateStatus(int charges)
        {
            bool flared = target.IsFlared;
            int mode = flared ? 2 : charges <= 0 ? 0 : 1;
            string key = target.ToggleFlareKey.ToString().ToUpperInvariant();
            if (lastKey != key)
            {
                keyLabel.text = key;
                lastKey = key;
            }

            bool modeChanged = lastMode != mode || lastScoutDuration != target.FlareScoutDuration;
            if (modeChanged)
            {
                modeLabel.text = flared ? "FLARE ACTIVE" : charges <= 0 ? "NO EMBERS" : "FLARE";
                modeLabel.color = flared ? Gold : Muted;
                keyRim.color = WithAlpha(Muted, charges <= 0 && !flared ? 0.2f : 0.45f);
                lastMode = mode;
                lastScoutDuration = target.FlareScoutDuration;
                lastTimerTenths = -1;
            }

            if (flared)
            {
                int tenths = Mathf.CeilToInt(Mathf.Max(0f, target.FlareTimer) * 10f);
                if (tenths != lastTimerTenths)
                {
                    detailLabel.text = (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s";
                    lastTimerTenths = tenths;
                }
                progress.fillAmount = Mathf.Clamp01(target.FlareTimer / Mathf.Max(0.01f, target.FlareScoutDuration));
                detailLabel.color = Ivory;
            }
            else
            {
                if (modeChanged)
                    detailLabel.text = charges <= 0 ? "DIM LIGHT" : "1 CHARGE / " + target.FlareScoutDuration.ToString("0", CultureInfo.InvariantCulture) + "s";
                detailLabel.color = Muted;
                progress.fillAmount = 0f;
            }

            progressTip.enabled = flared && progress.fillAmount > 0f;
            progress.enabled = flared;
            progress.rectTransform.sizeDelta = new Vector2(TrackWidth * progress.fillAmount, 1.5f);
            progressTip.rectTransform.anchoredPosition = new Vector2(TrackWidth * progress.fillAmount - 3f, -33f);
        }

        private void AnimateSlots(float dt, float time)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.Reaction = Mathf.Max(0f, slot.Reaction - dt);
                float reaction = reduceMotion ? 0f : slot.Reaction / 0.55f;
                float settle = 1f - reaction;
                float bump = Mathf.Sin(settle * Mathf.PI) * reaction;
                slot.Fill.rectTransform.localScale = Vector3.one * (1f + bump * (slot.Gained ? 0.12f : -0.08f));
                float reveal = reduceMotion ? 1f : Mathf.Clamp01((entrance - i * 0.025f) / 0.25f);
                slot.Group.alpha = EaseOut(reveal);
                slot.Root.anchoredPosition = new Vector2(i * 26f, -(1f - EaseOut(reveal)) * 2f);

                float breath = reduceMotion ? 0f : Mathf.Sin(time * 1.6f + i * 0.85f) * 0.015f;
                float alpha = slot.Charges > 0 ? 0.12f + breath : 0f;
                slot.Glow.color = WithAlpha(Gold, alpha + reaction * (slot.Gained ? 0.25f : 0.1f));
                slot.Ghost.color = WithAlpha(Ivory, !slot.Gained ? reaction * 0.35f : 0f);
                slot.Ghost.rectTransform.anchoredPosition = new Vector2(1f, -1f + (1f - reaction) * 4f);
                slot.Ghost.rectTransform.localScale = Vector3.one;
            }
        }

        private void AnimateFeedback(float dt)
        {
            popupTimer = Mathf.Max(0f, popupTimer - dt);
            float pickupT = 1f - popupTimer / FeedbackDuration;
            float popupAlpha = popupTimer <= 0f ? 0f : Mathf.Min(pickupT * 10f, 1f) * Mathf.Clamp01((1f - pickupT) * 4f);
            popupLabel.color = WithAlpha(Ivory, popupAlpha);
            popupLabel.rectTransform.anchoredPosition = new Vector2(0f, -Height - 5f + (reduceMotion ? 0f : EaseOut(pickupT) * 3f));

            countReaction = Mathf.Max(0f, countReaction - dt);
            float countT = countReaction / 0.5f;
            chargeLabel.color = Color.Lerp(lastCharges <= 2 ? Warning : Ivory, lastChangeWasGain ? Gold : Warning, countT);
            chargeLabel.rectTransform.localScale = Vector3.one * (1f + (reduceMotion ? 0f : Mathf.Sin(countT * Mathf.PI) * 0.03f));
        }

        private void AnimateEntrance(float dt)
        {
            entrance = Mathf.Min(1f, entrance + dt);
            float intro = reduceMotion ? 1f : EaseOut(Mathf.Clamp01(entrance / 0.4f));
            panelGroup.alpha = intro;
            Vector2 home = SafePosition();
            panel.anchoredPosition = home + new Vector2(0f, (1f - intro) * 3f);
        }

        private Vector2 SafePosition()
        {
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            Rect safe = Screen.safeArea;
            float horizontal = Mathf.Max(20f, screenMargin.x) + safe.xMin / scale;
            float vertical = Mathf.Max(20f, screenMargin.y) + (Screen.height - safe.yMax) / scale;
            return new Vector2(horizontal, -vertical);
        }

        private void UpdateSafeArea()
        {
            Rect safe = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (lastSafeArea == safe && lastScreen == size) return;
            lastSafeArea = safe;
            lastScreen = size;
            // Match the limiting dimension so the fixed-width instrument fits portrait and narrow windows.
            scaler.matchWidthOrHeight = Screen.width / Mathf.Max(1f, Screen.height) < 1.2f ? 0f : 1f;
        }

        private void BuildHierarchy()
        {
            RectTransform canvasRect = Rect("LanternHUD Canvas", transform, Vector2.zero, Vector2.zero);
            canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 1f;

            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("UI/EmberHUD") ?? TMP_Settings.defaultFontAsset;

            panel = Rect("Ember Reserve", canvasRect, screenMargin, new Vector2(Width, Height));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            panelGroup.alpha = 0f;

            // A soft local scrim maintains contrast over lit rooms without a visible card.
            Picture("Soft contrast", panel, new Vector2(-18f, 14f), new Vector2(244f, 84f),
                PixelSprites.Glow, new Color(0f, 0f, 0f, 0.7f));

            slots = new Slot[maxDisplaySlots];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new Slot();
                slots[i] = slot;
                slot.Root = Rect($"Ember {i + 1}", panel, new Vector2(i * 26f, 0f), new Vector2(22f, 30f));
                slot.Group = slot.Root.gameObject.AddComponent<CanvasGroup>();
                slot.Glow = Picture("Ember aura", slot.Root, new Vector2(-6f, 5f), new Vector2(34f, 34f), PixelSprites.Glow, Color.clear);
                Picture("Empty socket", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 24f), PixelSprites.EmberEmpty, new Color32(130, 135, 133, 255));
                slot.Fill = Picture("Ember", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 24f), PixelSprites.EmberFull, Color.clear);
                CenterPivot(slot.Fill.rectTransform);
                slot.Ghost = Picture("Spent afterglow", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 24f), PixelSprites.EmberFull, Color.clear);
                slot.Pips = new Image[2];
                for (int p = 0; p < 2; p++)
                    slot.Pips[p] = Box($"Charge {p + 1}", slot.Root, new Vector2(2f + p * 11f, -28f), new Vector2(7f, 1f), Bronze);
            }

            Box("Count separator", panel, new Vector2(134f, -4f), new Vector2(1f, 18f), WithAlpha(Muted, 0.2f));
            chargeLabel = Text("Charge count", panel, new Vector2(145f, -1f), new Vector2(33f, 25f), 18f, "00", Ivory);
            Text("Charge unit", panel, new Vector2(177f, -10f), new Vector2(28f, 12f), 6f, "CHG", Muted, 0.8f);
            overflowBadge = Text("Overflow", panel, new Vector2(145f, -24f), new Vector2(60f, 12f), 8f, "", Gold);
            overflowBadge.alignment = TextAlignmentOptions.MidlineLeft;

            keyRim = Box("Key rim", panel, new Vector2(0f, -42f), new Vector2(13f, 12f), WithAlpha(Muted, 0.45f));
            Box("Key face", panel, new Vector2(1f, -43f), new Vector2(11f, 10f), new Color32(11, 16, 18, 255));
            keyLabel = Text("Bound key", panel, new Vector2(0f, -42f), new Vector2(13f, 12f), 7f, "F", Muted);
            keyLabel.alignment = TextAlignmentOptions.Center;
            modeLabel = Text("Action", panel, new Vector2(20f, -42f), new Vector2(101f, 12f), 7f, "FLARE", Muted, 0.6f);
            detailLabel = Text("Scout detail", panel, new Vector2(115f, -42f), new Vector2(95f, 12f), 7f, "", Muted);
            detailLabel.alignment = TextAlignmentOptions.MidlineRight;

            progress = Box("Scout time", panel, new Vector2(0f, -36f), new Vector2(TrackWidth, 1.5f), WithAlpha(Gold, 0.75f));
            progress.fillAmount = 0f;
            progressTip = Picture("Scout playhead", panel, Vector2.zero, new Vector2(6f, 6f), PixelSprites.Glow, Ivory);
            progressTip.enabled = false;
            popupLabel = Text("Pickup receipt", panel, new Vector2(0f, -Height - 5f), new Vector2(Width, 16f), 8f, "", Color.clear, 0.4f);

            canvas.enabled = false;
        }

        private TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size,
            float fontSize, string content, Color color, float spacing = 0f)
        {
            RectTransform rect = Rect(name, parent, position, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.fontSize = fontSize;
            label.text = content;
            label.color = color;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.raycastTarget = false;
            label.richText = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
            => Picture(name, parent, position, size, PixelSprites.White, color);

        private static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Image picture = rect.gameObject.AddComponent<Image>();
            picture.sprite = sprite;
            picture.color = color;
            picture.raycastTarget = false;
            return picture;
        }

        private static void CenterPivot(RectTransform rect)
        {
            rect.anchoredPosition += new Vector2(rect.sizeDelta.x * 0.5f, -rect.sizeDelta.y * 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
