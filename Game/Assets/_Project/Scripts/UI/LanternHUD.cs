using TheLightenRoad.Lantern;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Quiet stone reserve with two-stage wear: fade to half, then fracture and disappear.
    /// Shares the controller's gameplay timeline; presentation never mutates fuel.
    /// Art and crack audio can be replaced by the artists' final assets.
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

        [Header("Ember art and sound — placeholders until artist delivery")]
        [SerializeField] private Sprite fullEmberArt;
        [SerializeField] private Sprite halfEmberArt;
        [SerializeField] private Sprite emberCrackArt;
        [SerializeField] private AudioClip emberBreakClip;
        [SerializeField, Range(0f, 1f)] private float emberBreakVolume = 0.25f;

        [Header("Motion")]
        [Tooltip("Keep status changes immediate and suppress decorative movement / pulsing.")]
        [SerializeField] private bool reduceMotion;

        private const float Width = 174f;
        private const float Height = 50f;
        private const float FeedbackDuration = 1.2f;
        private static readonly Color Gold = new Color32(190, 139, 78, 255);
        private static readonly Color Ivory = new Color32(222, 204, 169, 255);
        private static readonly Color Muted = new Color32(133, 120, 101, 255);

        private sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Glow;
            public Image Fill;
            public Image Ghost;
            public Image Cracks;
            public int Charges = -1;
            public float Reaction;
            public bool Gained;
            public bool Broken;
        }

        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup panelGroup;
        private RectTransform panel;
        private Slot[] slots;
        private TextMeshProUGUI overflowBadge;
        private TextMeshProUGUI modeLabel;
        private TextMeshProUGUI detailLabel;
        private TextMeshProUGUI keyLabel;
        private TextMeshProUGUI popupLabel;
        private int lastCharges = -1;
        private int lastOverflow = -1;
        private float entrance;
        private float popupTimer;
        private float countReaction;
        private bool subscribed;
        private AudioSource breakAudio;
        private AudioClip placeholderBreakClip;
        private int lastBurningSlot = -1;
        public int BreakFeedbackCount { get; private set; }
        public float ActiveEmberLevel => target != null && target.HasActiveScout ? target.ScoutEmberLevel : 0f;
        public float ActiveCrackAmount => target != null && target.HasActiveScout && target.ScoutIsSecondUse ? 1f - target.ScoutProgress : 0f;
        public float GetSlotCrackAlpha(int index) => ValidSlot(index) ? slots[index].Cracks.color.a : 0f;
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
        public float ScoutProgress => target != null ? target.ScoutProgress : 0f;
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
            target.EmberBroken += HandleEmberBroken;
            subscribed = true;
        }

        private void Unbind()
        {
            if (subscribed && target != null) target.EmberBroken -= HandleEmberBroken;
            subscribed = false;
        }

        private void HandleEmberBroken()
        {
            BreakFeedbackCount++;
            int index = Mathf.Min(slots.Length - 1, lastBurningSlot);
            if (index >= 0)
            {
                slots[index].Reaction = 0.55f;
                slots[index].Gained = false;
                slots[index].Broken = true;
                slots[index].Ghost.sprite = PixelSprites.EmberFragments;
            }
            if (breakAudio == null)
            {
                breakAudio = gameObject.AddComponent<AudioSource>();
                breakAudio.playOnAwake = false;
                breakAudio.spatialBlend = 0f;
            }
            if (emberBreakClip == null && placeholderBreakClip == null)
            {
                const int samples = 4410;
                float[] data = new float[samples];
                var noise = new System.Random(173);
                for (int i = 0; i < samples; i++)
                {
                    float t = i / 22050f;
                    float click = Mathf.Exp(-t * 85f) + 0.35f * Mathf.Exp(-Mathf.Abs(t - 0.035f) * 180f);
                    data[i] = ((float)noise.NextDouble() * 2f - 1f) * click * 0.45f;
                }
                placeholderBreakClip = AudioClip.Create("Ember crack / prototype", samples, 1, 22050, false);
                placeholderBreakClip.SetData(data, 0);
            }
            breakAudio.PlayOneShot(emberBreakClip != null ? emberBreakClip : placeholderBreakClip, emberBreakVolume);
        }

        private void OnDestroy()
        {
            Unbind();
            if (placeholderBreakClip != null) Destroy(placeholderBreakClip);
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

            UpdateBurningEmber();
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
                slot.Broken = false;
                slot.Charges = next;
                slot.Fill.sprite = next == 1 ? (halfEmberArt != null ? halfEmberArt : PixelSprites.EmberHalf) : (fullEmberArt != null ? fullEmberArt : PixelSprites.EmberFull);
                slot.Fill.color = next > 0 ? Color.white : Color.clear;
            }

            int overflow = Mathf.Max(0, Mathf.CeilToInt(charges / 2f) - maxDisplaySlots);
            if (lastOverflow != overflow)
            {
                overflowBadge.text = overflow > 0 ? "+" + overflow : string.Empty;
                lastOverflow = overflow;
            }
            lastCharges = charges;
        }

        private void UpdateBurningEmber()
        {
            // Inventory reserves a charge immediately; its stone remains visible while it burns.
            // Restore every other slot first so pickup gains cannot leave two burning stones.
            for (int i = 0; i < slots.Length; i++)
            {
                int next = Mathf.Clamp(target.Fuel.TotalCharges - i * 2, 0, 2);
                slots[i].Fill.sprite = next == 1 ? (halfEmberArt != null ? halfEmberArt : PixelSprites.EmberHalf) : (fullEmberArt != null ? fullEmberArt : PixelSprites.EmberFull);
                slots[i].Fill.color = next > 0 ? Color.white : Color.clear;
                slots[i].Cracks.color = Color.clear;
            }
            if (!target.HasActiveScout)
            {
                lastBurningSlot = -1;
                return;
            }
            int index = Mathf.Min(slots.Length - 1, target.ScoutEmberIndex);
            lastBurningSlot = index;
            Slot active = slots[index];
            active.Reaction = 0f;
            float progress = 1f - target.ScoutProgress;
            active.Fill.sprite = target.ScoutIsSecondUse
                ? (halfEmberArt != null ? halfEmberArt : PixelSprites.EmberHalf)
                : (fullEmberArt != null ? fullEmberArt : PixelSprites.EmberFull);
            Color soot = target.ScoutIsSecondUse ? new Color(0.15f, 0.15f, 0.15f, 0f) : new Color(0.62f, 0.48f, 0.33f, 1f);
            active.Fill.color = Color.Lerp(Color.white, soot, progress);
            active.Cracks.fillAmount = progress;
            active.Cracks.color = new Color(0.09f, 0.07f, 0.05f, target.ScoutIsSecondUse ? progress : 0f);
        }

        private void UpdateStatus(int charges)
        {
            bool active = target.HasActiveScout;
            keyLabel.text = target.ToggleFlareKey.ToString().ToUpperInvariant();
            modeLabel.text = active ? (target.IsDimming ? "DIMMING" : "FLARE ACTIVE") : charges <= 0 ? "NO EMBERS" : "FLARE";
            modeLabel.color = active ? Gold : Muted;
            detailLabel.text = active ? (target.ScoutIsSecondUse ? "CRACKING" : "FADING") : "";
            detailLabel.color = Muted;
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
                slot.Group.alpha = (slot.Charges > 0 || i == lastBurningSlot || slot.Reaction > 0f) ? EaseOut(reveal) : 0f;
                slot.Root.anchoredPosition = new Vector2(i * 28f, -(1f - EaseOut(reveal)) * 2f);

                float breath = reduceMotion ? 0f : Mathf.Sin(time * 1.6f + i * 0.85f) * 0.015f;
                float alpha = slot.Charges > 0 || i == lastBurningSlot ? (0.07f + breath) * (i == lastBurningSlot ? target.ScoutEmberLevel : 1f) : 0f;
                slot.Glow.color = WithAlpha(Gold, alpha + reaction * (slot.Gained ? 0.25f : 0.1f));
                slot.Ghost.color = WithAlpha(Ivory, !slot.Gained ? reaction * 0.35f : 0f);
                slot.Ghost.rectTransform.anchoredPosition = new Vector2(1f,
                    -1f + (1f - reaction) * (slot.Broken ? -6f : 4f));
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
                slot.Root = Rect($"Ember {i + 1}", panel, new Vector2(i * 28f, 0f), new Vector2(22f, 30f));
                slot.Group = slot.Root.gameObject.AddComponent<CanvasGroup>();
                slot.Glow = Picture("Ember aura", slot.Root, new Vector2(-6f, 5f), new Vector2(34f, 34f), PixelSprites.Glow, Color.clear);

                slot.Fill = Picture("Ember", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), PixelSprites.EmberFull, Color.clear);
                slot.Cracks = Picture("Growing fractures", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), emberCrackArt != null ? emberCrackArt : PixelSprites.EmberCracks, Color.clear);
                slot.Cracks.type = Image.Type.Filled;
                slot.Cracks.fillMethod = Image.FillMethod.Vertical;
                slot.Cracks.fillOrigin = 1;
                CenterPivot(slot.Fill.rectTransform);
                slot.Ghost = Picture("Spent afterglow", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), PixelSprites.EmberFull, Color.clear);

            }

            overflowBadge = Text("Overflow", panel, new Vector2(maxDisplaySlots * 28f, -10f), new Vector2(30f, 16f), 10f, "", Gold);
            keyLabel = Text("Bound key", panel, new Vector2(0f, -38f), new Vector2(10f, 12f), 7f, "F", Muted);
            modeLabel = Text("Action", panel, new Vector2(16f, -38f), new Vector2(85f, 12f), 7f, "FLARE", Muted);
            detailLabel = Text("Ember wear", panel, new Vector2(100f, -38f), new Vector2(70f, 12f), 7f, "", Muted);
            detailLabel.alignment = TextAlignmentOptions.MidlineRight;

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
