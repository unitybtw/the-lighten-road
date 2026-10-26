using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField, Range(1, 4)] private int maxDisplaySlots = 4;
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Ember art and sound — placeholders until artist delivery")]
        [SerializeField] private Sprite fullEmberArt;
        [SerializeField] private Sprite halfEmberArt;
        [SerializeField] private Sprite emberCrackArt;
        [SerializeField] private AudioClip emberBreakClip;
        [SerializeField, Range(0f, 1f)] private float emberBreakVolume = 0.25f;

        [Header("Minimalism & Burning Ember Feedback")]
        [Tooltip("When enabled, hides the text prompts (key bind, mode label) and relies on the burning ember particles.")]
        [SerializeField] private bool minimalMode = false;
        [Tooltip("Enable rising spark particles from the actively burning ember during Flare.")]
        [SerializeField] private bool enableBurnParticles = true;
        [SerializeField, Range(4, 16)] private int sparksPerSlot = 8;

        [Header("Motion")]
        [Tooltip("Keep status changes immediate and suppress decorative movement / pulsing.")]
        [SerializeField] private bool reduceMotion;

        private const float Width = 174f;
        private const float Height = 50f;
        private const float FeedbackDuration = 1.2f;
        private static readonly Color Gold = new Color32(190, 139, 78, 255);
        private static readonly Color Ivory = new Color32(222, 204, 169, 255);
        private static readonly Color Muted = new Color32(133, 120, 101, 255);

        private struct UiSpark
        {
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float BaseSize;
            public bool Active;
        }

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
            public RectTransform SparkRoot;
            public UiSpark[] Sparks;
            public int Charges = -1;
            public float Reaction;
            public bool Gained;
            public bool Broken;
            public float SparkSpawnTimer;
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
        private TextMeshProUGUI detailLabel;
        private TextMeshProUGUI keyLabel;
        private Image keyIcon;
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
        public float GetSlotBurnAuraAlpha(int index) => ValidSlot(index) && slots[index].BurnAura != null ? slots[index].BurnAura.color.a : 0f;
        public int ActiveSparkCount => GetActiveSparkCount();
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
                minimalMode = value;
                ApplyMinimalMode();
            }
        }
        public bool EnableBurnParticles
        {
            get => enableBurnParticles;
            set => enableBurnParticles = value;
        }
        public Sprite GetSlotSprite(int index) => ValidSlot(index) ? slots[index].Fill.sprite : null;
        public Color GetSlotColor(int index) => ValidSlot(index) ? slots[index].Fill.color : Color.clear;
        private bool lastChangeWasGain;
        private Key lastToggleKey = (Key)(-1);
        private string cachedKeyString = string.Empty;
        private string cachedModeString = string.Empty;
        private string cachedDetailString = string.Empty;

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
            ClearAllSparks();
        }

        public void SetTarget(LanternController controller)
        {
            Unbind();
            target = controller;
            lastCharges = -1;
            popupTimer = 0f;
            countReaction = 0f;
            ClearAllSparks();
            if (slots != null)
            {
                foreach (Slot slot in slots)
                {
                    slot.Charges = -1;
                    slot.Reaction = 0f;
                    slot.SparkSpawnTimer = 0f;
                }
            }
            TryBind();
        }

        private void ApplyMinimalMode()
        {
            if (promptGroup != null)
            {
                promptGroup.alpha = minimalMode ? 0f : 1f;
                promptGroup.blocksRaycasts = false;
                promptGroup.interactable = false;
            }
        }

        private void ClearAllSparks()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]?.BurnAura != null) slots[i].BurnAura.color = Color.clear;
                if (slots[i]?.Sparks == null) continue;
                for (int s = 0; s < slots[i].Sparks.Length; s++)
                {
                    slots[i].Sparks[s].Active = false;
                    if (slots[i].Sparks[s].Image != null)
                        slots[i].Sparks[s].Image.color = Color.clear;
                }
            }
        }

        private int GetActiveSparkCount()
        {
            if (slots == null) return 0;
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]?.Sparks == null) continue;
                for (int s = 0; s < slots[i].Sparks.Length; s++)
                {
                    if (slots[i].Sparks[s].Active) count++;
                }
            }
            return count;
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
            bool available = isActiveAndEnabled;
            if (canvas != null) canvas.enabled = available;
            if (!available || target == null || target.Fuel == null) return;

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

        private void UpdateStatus(int charges)
        {
            bool active = target.HasActiveScout;
            var style = ControllerGlyphService.ActiveStyle;
            var flareSprite = ControllerGlyphService.GetActionSprite(GlyphAction.Flare, style);

            if (keyIcon != null)
            {
                if (flareSprite != null)
                {
                    keyIcon.sprite = flareSprite;
                    keyIcon.color = active ? Gold : Ivory;
                    if (keyLabel != null && keyLabel.text != "") keyLabel.text = "";
                }
                else
                {
                    keyIcon.color = Color.clear;
                    string keyText = target.ToggleFlareKey.ToString().ToUpperInvariant();
                    if (keyLabel != null && keyLabel.text != keyText)
                    {
                        keyLabel.text = keyText;
                    }
                }
            }
            else if (keyLabel != null)
            {
                string keyText = style == ControllerPromptStyle.PlayStation ? "R2" : (style == ControllerPromptStyle.Xbox ? "RT" : target.ToggleFlareKey.ToString().ToUpperInvariant());
                if (keyLabel.text != keyText) keyLabel.text = keyText;
            }

            string mode = active ? (target.IsDimming ? "DIMMING" : "FLARE ACTIVE") : charges <= 0 ? "NO EMBERS" : "FLARE";
            if (mode != cachedModeString)
            {
                cachedModeString = mode;
                modeLabel.text = mode;
            }
            modeLabel.color = active ? Gold : Muted;

            string detail = active ? (target.ScoutProgress < .6f ? "CRACKING" : "FADING") : "";
            if (detail != cachedDetailString)
            {
                cachedDetailString = detail;
                detailLabel.text = detail;
            }
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
                slot.Group.alpha = EaseOut(reveal);
                slot.Root.anchoredPosition = new Vector2(i * 28f, -(1f - EaseOut(reveal)) * 2f);

                float breath = reduceMotion ? 0f : Mathf.Sin(time * 1.6f + i * 0.85f) * 0.015f;
                float alpha = slot.Charges > 0 || i == lastBurningSlot ? (0.07f + breath) * (i == lastBurningSlot ? target.ScoutEmberLevel : 1f) : 0f;
                slot.Glow.color = WithAlpha(Gold, alpha + reaction * (slot.Gained ? 0.25f : 0.1f));
                slot.Ghost.color = WithAlpha(Ivory, !slot.Gained ? reaction * 0.35f : 0f);
                slot.Ghost.rectTransform.anchoredPosition = new Vector2(1f,
                    -1f + (1f - reaction) * (slot.Broken ? -6f : 4f));
                slot.Ghost.rectTransform.localScale = Vector3.one;

                // Burning ember heat aura and fiery sparks
                bool isBurningThisSlot = (target != null && target.HasActiveScout && i == lastBurningSlot);
                if (isBurningThisSlot && slot.BurnAura != null)
                {
                    float heatFlicker = Mathf.PerlinNoise(time * 16f, i * 11.3f);
                    float auraAlpha = reduceMotion ? 0.55f : (0.5f + 0.35f * heatFlicker);
                    Color baseFire = target.ScoutIsSecondUse
                        ? new Color(1f, 0.32f, 0.1f, auraAlpha)
                        : new Color(1f, 0.62f, 0.18f, auraAlpha);
                    slot.BurnAura.color = baseFire;
                    if (!reduceMotion)
                    {
                        float scale = 1f + 0.14f * (heatFlicker - 0.5f);
                        slot.BurnAura.rectTransform.localScale = new Vector3(scale, scale, 1f);
                    }

                    if (enableBurnParticles && !reduceMotion)
                    {
                        slot.SparkSpawnTimer -= dt;
                        if (slot.SparkSpawnTimer <= 0f)
                        {
                            slot.SparkSpawnTimer = 0.06f;
                            if (slot.Sparks != null)
                            {
                                for (int s = 0; s < slot.Sparks.Length; s++)
                                {
                                    if (!slot.Sparks[s].Active)
                                    {
                                        float maxLife = Random.Range(0.35f, 0.65f);
                                        float size = Random.Range(3f, 5f);
                                        slot.Sparks[s].Active = true;
                                        slot.Sparks[s].Life = maxLife;
                                        slot.Sparks[s].MaxLife = maxLife;
                                        slot.Sparks[s].BaseSize = size;
                                        slot.Sparks[s].Seed = Random.value * 100f;
                                        slot.Sparks[s].Position = new Vector2(Random.Range(-5f, 5f), Random.Range(-4f, 6f));
                                        slot.Sparks[s].Velocity = new Vector2(Random.Range(-10f, 10f), Random.Range(30f, 58f));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (slot.BurnAura != null)
                {
                    slot.BurnAura.color = Color.clear;
                }

                if (slot.Sparks != null)
                {
                    for (int s = 0; s < slot.Sparks.Length; s++)
                    {
                        if (!slot.Sparks[s].Active)
                        {
                            slot.Sparks[s].Image.color = Color.clear;
                            continue;
                        }

                        slot.Sparks[s].Life -= dt;
                        if (slot.Sparks[s].Life <= 0f || reduceMotion)
                        {
                            slot.Sparks[s].Active = false;
                            slot.Sparks[s].Image.color = Color.clear;
                            continue;
                        }

                        float t = 1f - Mathf.Clamp01(slot.Sparks[s].Life / slot.Sparks[s].MaxLife);
                        slot.Sparks[s].Position += slot.Sparks[s].Velocity * dt;
                        float drift = Mathf.Sin(time * 14f + slot.Sparks[s].Seed) * 10f * dt;
                        slot.Sparks[s].Position.x += drift;

                        Color sparkColor;
                        if (t < 0.35f)
                        {
                            sparkColor = Color.Lerp(
                                new Color(1f, 0.98f, 0.85f, 1f),
                                new Color(1f, 0.72f, 0.22f, 0.95f),
                                t / 0.35f
                            );
                        }
                        else
                        {
                            sparkColor = Color.Lerp(
                                new Color(1f, 0.72f, 0.22f, 0.95f),
                                new Color(0.85f, 0.25f, 0.05f, 0f),
                                (t - 0.35f) / 0.65f
                            );
                        }

                        float currentSize = slot.Sparks[s].BaseSize * (1f - t * 0.4f);
                        slot.Sparks[s].Image.rectTransform.anchoredPosition = slot.Sparks[s].Position;
                        slot.Sparks[s].Image.rectTransform.sizeDelta = new Vector2(currentSize, currentSize);
                        slot.Sparks[s].Image.color = sparkColor;
                    }
                }
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

                slot.SparkRoot = Rect("Sparks", slot.Root, new Vector2(11f, -15f), Vector2.zero);
                slot.SparkRoot.pivot = new Vector2(0.5f, 0.5f);
                int sparkCount = Mathf.Clamp(sparksPerSlot, 4, 16);
                slot.Sparks = new UiSpark[sparkCount];
                for (int s = 0; s < sparkCount; s++)
                {
                    var sparkImg = Picture($"Spark {s + 1}", slot.SparkRoot, Vector2.zero, new Vector2(3f, 3f), PixelSprites.Spark, Color.clear);
                    CenterPivot(sparkImg.rectTransform);
                    slot.Sparks[s] = new UiSpark
                    {
                        Image = sparkImg,
                        Active = false,
                        Life = 0f
                    };
                }
            }

            overflowBadge = Text("Overflow", panel, new Vector2(maxDisplaySlots * 28f, -10f), new Vector2(30f, 16f), 10f, "", Gold);

            promptContainer = Rect("Prompt Labels", panel, new Vector2(0f, -40f), new Vector2(Width, 22f));
            promptGroup = promptContainer.gameObject.AddComponent<CanvasGroup>();
            keyLabel = Text("Bound key", promptContainer, new Vector2(0f, 0f), new Vector2(16f, 16f), 8.5f, "F", Muted);
            keyIcon = Picture("Bound key icon", promptContainer, new Vector2(0f, 0f), new Vector2(22f, 22f), null, Color.clear);
            modeLabel = Text("Action", promptContainer, new Vector2(26f, 0f), new Vector2(85f, 16f), 8.5f, "FLARE", Muted);
            detailLabel = Text("Ember wear", promptContainer, new Vector2(110f, 0f), new Vector2(70f, 16f), 8.5f, "", Muted);
            detailLabel.alignment = TextAlignmentOptions.MidlineRight;

            popupLabel = Text("Pickup receipt", panel, new Vector2(0f, -Height - 5f), new Vector2(Width, 16f), 8f, "", Color.clear, 0.4f);

            ApplyMinimalMode();
            canvas.enabled = true;
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
