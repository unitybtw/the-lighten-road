using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Held ember reserve and the fading, fracturing stone spent by a Flare.
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
        [SerializeField, Range(1, 4)] private int maxDisplaySlots = 4;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField, HideInInspector] private Material textContrastMaterial;

        [Header("Ember art and sound — placeholders until artist delivery")]
        [SerializeField] private Sprite fullEmberArt;
        [SerializeField] private Sprite halfEmberArt;
        [SerializeField] private Sprite emberCrackArt;
        [SerializeField] private AudioClip emberBreakClip;
        [SerializeField, Range(0f, 1f)] private float emberBreakVolume = 0.25f;

        [Header("Minimalism & Burning Ember Feedback")]
        [Tooltip("Hide optional action text. An empty-lantern warning always remains visible.")]
        [SerializeField] private bool minimalMode = false;

        [Header("Motion")]
        [Tooltip("Keep status changes immediate and suppress decorative movement / pulsing.")]
        [SerializeField] private bool reduceMotion;

        private const float Width = 174f;
        private const float Height = 64f;
        private const float SlotPitch = 32f;
        private const float FeedbackDuration = 1.2f;
        private const float HintDuration = 4f;
        private const float HintFadeDuration = 0.4f;
        private static readonly Color Gold = new Color32(190, 139, 78, 255);
        private static readonly Color Ivory = new Color32(222, 204, 169, 255);
        private static readonly Color Muted = new Color32(133, 120, 101, 255);

        private sealed class Slot
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Socket;
            public Image Glow;
            public Image BurnAura;
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
        private RectTransform promptContainer;
        private CanvasGroup promptGroup;
        private TextMeshProUGUI modeLabel;
        private TextMeshProUGUI keyLabel;
        private Image keyIcon;
        private TextMeshProUGUI popupLabel;
        private int lastCharges = -1;
        private int lastOverflow = -1;
        private float entrance;
        private float popupTimer;
        private float countReaction;
        private float hintTimer;
        private bool subscribed;
        private LanternController boundTarget;
        private float bindRetryTimer;
        private AudioSource breakAudio;
        private AudioClip placeholderBreakClip;
        private int lastBurningSlot = -1;
        public int BreakFeedbackCount { get; private set; }
        public float ActiveEmberLevel => target != null && target.HasActiveScout ? target.ScoutEmberLevel : 0f;
        public float ActiveCrackAmount => target != null && target.HasActiveScout && target.ScoutIsSecondUse ? 1f - target.ScoutProgress : 0f;
        public float GetSlotCrackAlpha(int index) => ValidSlot(index) ? slots[index].Cracks.color.a : 0f;
        public float GetSlotBurnAuraAlpha(int index) => ValidSlot(index) && slots[index].BurnAura != null ? slots[index].BurnAura.color.a : 0f;
        public int ActiveSparkCount => 0; // Burning wear stays on the stone; the HUD emits no sparks.
        public bool IsEmptyWarningVisible => target != null && target.Fuel != null && target.Fuel.IsEmpty &&
            canvas != null && canvas.enabled && panelGroup != null && panelGroup.alpha > 0f &&
            promptGroup != null && promptGroup.alpha > 0f;
        public bool IsEmberActivelyBurning => target != null && target.HasActiveScout;
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
        public bool MinimalMode
        {
            get => minimalMode;
            set
            {
                if (minimalMode == value) return;
                minimalMode = value;
                if (!value) hintTimer = HintDuration;
                ApplyMinimalMode();
            }
        }
        public Sprite GetSlotSprite(int index) => ValidSlot(index) ? slots[index].Fill.sprite : null;
        public Color GetSlotColor(int index) => ValidSlot(index) ? slots[index].Fill.color : Color.clear;
        private bool lastChangeWasGain;
        private string cachedModeString = string.Empty;

        private bool ValidSlot(int index) => slots != null && index >= 0 && index < slots.Length;

        private void Awake()
        {
            maxDisplaySlots = LanternFuel.MaxEmbers;
            BuildHierarchy();
            ApplyMinimalMode();
        }

        private void OnValidate()
        {
            ApplyMinimalMode();
        }

        private void OnEnable()
        {
            // Script recompilation during Play Mode loses the nonserialized slot references.
            // Replace the generated canvas rather than ticking a stale hierarchy.
            if (slots == null)
            {
                var oldCanvas = transform.Find("LanternHUD Canvas");
                if (oldCanvas != null)
                {
                    oldCanvas.gameObject.SetActive(false);
                    Destroy(oldCanvas.gameObject);
                }
                maxDisplaySlots = LanternFuel.MaxEmbers;
                BuildHierarchy();
            }
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
            if (canvas != null)
                canvas.enabled = false;
            ClearBurnFeedback();
        }

        public void SetTarget(LanternController controller)
        {
            Unbind();
            target = controller;
            TryBind();
        }

        private void ApplyMinimalMode()
        {
            if (promptGroup != null)
            {
                bool empty = target != null && target.Fuel != null && target.Fuel.IsEmpty;
                bool hideHint = minimalMode || (target != null && target.IsDimming);
                float hintAlpha = reduceMotion ? (hintTimer > 0f ? 1f : 0f)
                    : Mathf.Clamp01(hintTimer / HintFadeDuration);
                promptGroup.alpha = empty ? 1f : hideHint ? 0f : hintAlpha;
                promptGroup.blocksRaycasts = false;
                promptGroup.interactable = false;
            }
        }

        private void ClearBurnFeedback()
        {
            if (slots == null) return;
            foreach (var slot in slots)
                if (slot.BurnAura != null) slot.BurnAura.color = Color.clear;
        }

        private void TryBind()
        {
            if (target == null)
                target = LanternController.ActiveInstance != null
                    ? LanternController.ActiveInstance : FindAnyObjectByType<LanternController>();
            if (target != null && target == boundTarget && subscribed && target.Fuel != null) return;
            Unbind();
            ResetPresentation();
            if (target == null || target.Fuel == null)
            {
                if (canvas != null) canvas.enabled = false;
                return;
            }
            boundTarget = target;
            boundTarget.EmberBroken += HandleEmberBroken;
            subscribed = true;
        }

        private void ResetPresentation()
        {
            lastCharges = lastBurningSlot = -1;
            entrance = popupTimer = countReaction = 0f;
            hintTimer = HintDuration;
            cachedModeString = string.Empty;
            ClearBurnFeedback();
            if (slots == null) return;
            foreach (var slot in slots)
            {
                slot.Charges = -1;
                slot.Reaction = 0f;
                slot.Gained = slot.Broken = false;
            }
        }

        private void Unbind()
        {
            if (subscribed && boundTarget != null) boundTarget.EmberBroken -= HandleEmberBroken;
            subscribed = false;
            boundTarget = null;
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

            // Centralized ember break SFX is handled by PlayerAudioController via SoundEffectManager.
            // Only play local fallback audio if SoundEffectManager is not active.
            if (!SoundEffectManager.HasInstance && Time.timeSinceLevelLoad > 0.65f)
            {
                if (breakAudio == null)
                {
                    breakAudio = gameObject.AddComponent<AudioSource>();
                    breakAudio.playOnAwake = false;
                    breakAudio.spatialBlend = 0f;
                }
                if (emberBreakClip == null && placeholderBreakClip == null)
                    placeholderBreakClip = Resources.Load<AudioClip>("Audio/SFX/Lantern/ember_break");
                breakAudio.PlayOneShot(emberBreakClip != null ? emberBreakClip : placeholderBreakClip, emberBreakVolume);
            }
        }

        private void OnDestroy()
        {
            Unbind();
            if (textContrastMaterial != null)
                Destroy(textContrastMaterial);
        }

        private void Update()
        {
            if (target != boundTarget || boundTarget == null || !subscribed)
            {
                bindRetryTimer -= Time.unscaledDeltaTime;
                if (target != boundTarget || (target != null && !subscribed) || bindRetryTimer <= 0f)
                {
                    bindRetryTimer = .35f;
                    TryBind();
                }
            }
            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        public void Tick(float deltaTime, float time)
        {
            bool available = isActiveAndEnabled && target != null && target.Fuel != null;
            if (canvas != null) canvas.enabled = available;
            if (!available) return;

            float dt = Mathf.Max(0f, deltaTime);
            LanternFuel fuel = target.Fuel;
            int charges = fuel.TotalCharges;
            if (charges != lastCharges)
                UpdateInventory(charges);

            UpdateBurningEmber();
            UpdateStatus(charges);
            hintTimer = Mathf.Max(0f, hintTimer - dt);
            ApplyMinimalMode();
            AnimateSlots(dt);
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
                    popupLabel.text = "+" + (difference / (float)LanternFuel.ChargesPerEmber)
                        .ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
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
            if (!target.HasActiveScout || target.Fuel.IsFull)
            {
                lastBurningSlot = -1;
                return;
            }
            int index = Mathf.Min(slots.Length - 1, target.ScoutEmberIndex);
            lastBurningSlot = index;
            Slot active = slots[index];
            active.Reaction = 0f;
            float progress = 1f - target.ScoutProgress;
            active.Fill.sprite = fullEmberArt != null ? fullEmberArt : PixelSprites.EmberFull;
            Color soot = new Color(0.15f, 0.15f, 0.15f, 0f);
            active.Fill.color = Color.Lerp(Color.white, soot, progress);
            active.Cracks.fillAmount = progress;
            active.Cracks.color = new Color(0.09f, 0.07f, 0.05f, Mathf.InverseLerp(0.4f, 1f, progress));
        }

        private ControllerPromptStyle cachedPromptStyle=(ControllerPromptStyle)(-1);
        private Key cachedFlareKey=(Key)(-1);
        private Sprite cachedFlareSprite;
        private string cachedFlareText;
        private void UpdateStatus(int charges)
        {
            bool active = target.HasActiveScout;
            bool empty = charges <= 0;
            bool hasWholeEmber = charges >= LanternFuel.ChargesPerEmber;
            bool canUseKey = !empty && !target.IsDimming && (active || hasWholeEmber);
            var style = ControllerGlyphService.ActiveStyle;
            if(cachedPromptStyle!=style || cachedFlareKey!=target.ToggleFlareKey)
            {
                hintTimer = HintDuration;
                cachedPromptStyle=style;cachedFlareKey=target.ToggleFlareKey;
                cachedFlareSprite=ControllerGlyphService.GetActionSprite(GlyphAction.Flare,style);
                cachedFlareText=style==ControllerPromptStyle.PlayStation?"R2":style==ControllerPromptStyle.Xbox?"RT":cachedFlareKey.ToString().ToUpperInvariant();
            }
            var flareSprite = cachedFlareSprite;
            keyIcon.gameObject.SetActive(canUseKey && flareSprite != null);
            keyLabel.gameObject.SetActive(canUseKey && flareSprite == null);

            if (keyIcon != null)
            {
                if (flareSprite != null)
                {
                    keyIcon.sprite = flareSprite;
                    keyIcon.color = Ivory;
                    if (keyLabel != null && keyLabel.text != "") keyLabel.text = "";
                }
                else
                {
                    keyIcon.color = Color.clear;
                    string keyText = cachedFlareText;
                    if (keyLabel != null && keyLabel.text != keyText)
                    {
                        keyLabel.text = keyText;
                    }
                }
            }
            else if (keyLabel != null)
            {
                string keyText = cachedFlareText;
                if (keyLabel.text != keyText) keyLabel.text = keyText;
            }

            // The fuel warning takes priority even while the last spent stone is still burning.
            string mode = empty ? "No embers" : active ? (target.IsDimming ? "Dimming" : "Dim")
                : hasWholeEmber ? "Flare" : "Need 1 ember";
            if (mode != cachedModeString)
            {
                hintTimer = HintDuration;
                cachedModeString = mode;
                modeLabel.text = mode;
                modeLabel.rectTransform.sizeDelta = new Vector2(canUseKey ? Width - 32f : Width, 20f);
            }
            modeLabel.color = Ivory;
            keyLabel.color = Ivory;
            modeLabel.rectTransform.anchoredPosition = new Vector2(canUseKey ? 32f : 0f, 0f);
        }

        private void AnimateSlots(float dt)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.Reaction = Mathf.Max(0f, slot.Reaction - dt);
                float reaction = reduceMotion ? 0f : slot.Reaction / 0.55f;
                float settle = 1f - reaction;
                float bump = Mathf.Sin(settle * Mathf.PI) * reaction;
                slot.Fill.rectTransform.localScale = Vector3.one * (1f + bump * (slot.Gained ? 0.06f : 0f));
                float reveal = reduceMotion ? 1f : Mathf.Clamp01((entrance - i * 0.025f) / 0.25f);
                slot.Group.alpha = EaseOut(reveal);
                slot.Root.anchoredPosition = new Vector2(i * SlotPitch, -(1f - EaseOut(reveal)) * 2f);

                slot.Glow.color = WithAlpha(Gold, reaction * (slot.Gained ? .1f : 0f));
                slot.Ghost.color = WithAlpha(Ivory, !slot.Gained ? reaction * 0.35f : 0f);
                slot.Ghost.rectTransform.anchoredPosition = new Vector2(1f, -1f);
                slot.Ghost.rectTransform.localScale = Vector3.one;

                // Keep the remaining burn legible without another particle system or pulse.
                bool burning = target.HasActiveScout && i == lastBurningSlot;
                slot.BurnAura.color = burning ? WithAlpha(Gold, .06f * target.ScoutEmberLevel) : Color.clear;
                slot.BurnAura.rectTransform.localScale = Vector3.one;
            }
        }

        private void AnimateFeedback(float dt)
        {
            popupTimer = Mathf.Max(0f, popupTimer - dt);
            float pickupT = 1f - popupTimer / FeedbackDuration;
            float popupAlpha = popupTimer <= 0f ? 0f : Mathf.Min(pickupT * 10f, 1f) * Mathf.Clamp01((1f - pickupT) * 4f);
            popupLabel.color = WithAlpha(Ivory, popupAlpha);

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

            // Keep contrast on the glyphs, without darkening the room behind the HUD.
            // Share one owned material; never alter the font asset used by other screens.
            if (textContrastMaterial == null && fontAsset != null && fontAsset.material != null)
            {
                textContrastMaterial = new Material(fontAsset.material)
                {
                    name = "Ember HUD text contrast",
                    hideFlags = HideFlags.HideAndDontSave
                };
                textContrastMaterial.EnableKeyword("OUTLINE_ON");
                textContrastMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.14f);
                textContrastMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.06f, 0.045f, 0.03f, 0.95f));
            }

            panel = Rect("Ember Reserve", canvasRect, screenMargin, new Vector2(Width, Height));
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            panelGroup.alpha = 0f;

            slots = new Slot[maxDisplaySlots];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new Slot();
                slots[i] = slot;
                slot.Root = Rect($"Ember {i + 1}", panel, new Vector2(i * SlotPitch, 0f), new Vector2(22f, 30f));
                slot.Group = slot.Root.gameObject.AddComponent<CanvasGroup>();
                slot.Socket = Picture("Ember socket", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), PixelSprites.EmberEmpty, new Color(0.42f, 0.38f, 0.32f, 0.55f));
                CenterPivot(slot.Socket.rectTransform);
                slot.Glow = Picture("Ember aura", slot.Root, new Vector2(-6f, 5f), new Vector2(34f, 34f), PixelSprites.Glow, Color.clear);
                slot.BurnAura = Picture("Ember burn aura", slot.Root, new Vector2(-7f, 6f), new Vector2(36f, 36f), PixelSprites.Glow, Color.clear);
                CenterPivot(slot.BurnAura.rectTransform);

                slot.Fill = Picture("Ember", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), PixelSprites.EmberFull, Color.clear);
                slot.Cracks = Picture("Growing fractures", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), emberCrackArt != null ? emberCrackArt : PixelSprites.EmberCracks, Color.clear);
                slot.Cracks.type = Image.Type.Filled;
                slot.Cracks.fillMethod = Image.FillMethod.Vertical;
                slot.Cracks.fillOrigin = 1;
                CenterPivot(slot.Fill.rectTransform);
                slot.Ghost = Picture("Spent afterglow", slot.Root, new Vector2(1f, -1f), new Vector2(20f, 27f), PixelSprites.EmberFull, Color.clear);


            }

            overflowBadge = Text("Overflow", panel, new Vector2(maxDisplaySlots * SlotPitch, -10f), new Vector2(30f, 16f), 10f, "", Gold);

            promptContainer = Rect("Prompt Labels", panel, new Vector2(0f, -44f), new Vector2(Width, 20f));
            promptGroup = promptContainer.gameObject.AddComponent<CanvasGroup>();
            keyLabel = Text("Bound key", promptContainer, new Vector2(0f, 0f), new Vector2(20f, 20f), 11f, "F", Muted);
            keyIcon = Picture("Bound key icon", promptContainer, new Vector2(0f, 0f), new Vector2(20f, 20f), null, Color.clear);
            modeLabel = Text("Action", promptContainer, new Vector2(32f, 0f), new Vector2(Width - 32f, 20f), 12f, "Flare", Ivory, 2f);

            popupLabel = Text("Pickup receipt", panel, new Vector2(maxDisplaySlots * SlotPitch, -6f), new Vector2(46f, 18f), 11f, "", Color.clear);

            ApplyMinimalMode();
            canvas.enabled = true;
        }

        private TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size,
            float fontSize, string content, Color color, float spacing = 0f)
        {
            RectTransform rect = Rect(name, parent, position, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            if (textContrastMaterial != null)
                label.fontSharedMaterial = textContrastMaterial;
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
