using System.Collections.Generic;
using TheLightenRoad.Frontend;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Frameless top-right mechanics instrument cluster and Shadow Realm atmosphere overlay.
    /// Matches LanternHUD, StaminaHUD, and ArchiveInterfaceStyle typography, pixel craft,
    /// and spring/breath animations.
    /// </summary>
    public sealed class MechanicsHUD : MonoBehaviour
    {
        public LanternLensController LensController;
        public EmberInventoryController EmberInventory;
        public ShadowRealmZone2D ShadowRealm;
        public CentralCoreFinale2D CentralCore;

        private const int MaxEmberSlots = 3;
        private const int CoreBeamSlots = 4;
        private const float HintShowDuration = 4f;
        private const float HintFadeDuration = 1.2f;
        private const float EntranceDuration = 0.45f;

        private static readonly Color Gold = new Color32(190, 139, 78, 255);
        private static readonly Color Ivory = new Color32(222, 204, 169, 255);
        private static readonly Color Muted = new Color32(133, 120, 101, 255);
        private static readonly Color OutlineInk = new Color(0.06f, 0.045f, 0.03f, 0.95f);

        private static readonly Color NormalAmber = new Color(1.0f, 0.72f, 0.30f, 1f);
        private static readonly Color EtherealCyan = new Color(0.28f, 0.92f, 1.0f, 1f);
        private static readonly Color InvertCrimson = new Color(1.0f, 0.28f, 0.36f, 1f);
        private static readonly Color SuppressedSlate = new Color(0.46f, 0.24f, 0.26f, 0.78f);

        private Canvas canvas;
        private RectTransform safeAreaRoot;
        private RectTransform clusterAnchor;
        private CanvasGroup clusterGroup;

        private Image shadowOverlay;
        private Image shadowVignette;

        private CanvasGroup bannerGroup;
        private RectTransform bannerRect;
        private TextMeshProUGUI statusBanner;
        private Image bannerFiligree;

        private RectTransform lensDialRoot;
        private Image lensHalo;
        private Image lensFrame;
        private Image lensCore;
        private TextMeshProUGUI lensLabel;
        private TextMeshProUGUI lensHintLabel;

        private CanvasGroup emberRowGroup;
        private readonly Image[] emberIcons = new Image[MaxEmberSlots];
        private readonly Image[] emberHalos = new Image[MaxEmberSlots];
        private readonly float[] emberPops = new float[MaxEmberSlots];
        private Image emberSelector;
        private RectTransform emberSelectorRect;
        private TextMeshProUGUI emberLabel;
        private TextMeshProUGUI emberHintLabel;

        private RectTransform progressRowRoot;
        private readonly Image[] corePrisms = new Image[CoreBeamSlots];
        private readonly float[] corePops = new float[CoreBeamSlots];
        private readonly List<Image> mementoSeals = new List<Image>();
        private readonly List<float> mementoPops = new List<float>();
        private TextMeshProUGUI coreCountLabel;
        private TextMeshProUGUI mementoCountLabel;

        private TMP_FontAsset hudFont;
        private Material hudTextMaterial;
        private Material displayTextMaterial;

        private Rect lastSafeArea = new Rect(-1f, -1f, 0f, 0f);
        private Vector2Int lastScreenSize = Vector2Int.zero;

        private LanternLensType trackedLens = LanternLensType.Normal;
        private bool trackedSuppressed;
        private EmberColor trackedSelectedEmber = EmberColor.None;
        private int trackedEmberCount = -1;
        private int trackedBeamCount = -1;
        private int trackedMementoCount = -1;
        private bool trackedFinale;

        private float entranceElapsed;
        private float lensPop;
        private float lensTargetAngle;
        private float lensCurrentAngle;
        private Color lensCurrentColor = NormalAmber;
        private float selectorTargetX;
        private float selectorCurrentX;
        private float shadowWeight;
        private float bannerWeight;
        private float hintTimer = HintShowDuration;
        private string activeBannerKey = string.Empty;

        private MementoCollectible2D[] cachedMementos;
        private int lastMementoSlotsBuilt = -1;
        private float nextTargetResolveTime;
        private string lastLensLabelText;
        private string lastEmberLabelText;
        private int lastDisplayedBeams = -1;
        private int lastDisplayedMementos = -1;
        private int lastDisplayedTotalMementos = -1;

        public bool IsMonochromeRedActive => ShadowRealm != null
            ? ShadowRealm.IsPlayerInside
            : (shadowOverlay != null && shadowOverlay.color.a > 0.05f);

        public string LensDisplayText => lensLabel != null ? lensLabel.text : string.Empty;
        public string EmberDisplayText => emberLabel != null ? emberLabel.text : string.Empty;
        public string FinaleStatusText => statusBanner != null ? statusBanner.text : string.Empty;

        private void Awake()
        {
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (hudTextMaterial != null) Destroy(hudTextMaterial);
            if (displayTextMaterial != null) Destroy(displayTextMaterial);
        }

        private void Start()
        {
            ResolveTargets();
            SyncStateFromTargets(immediate: true);
        }

        private void Update()
        {
            if (canvas != null)
            {
                canvas.enabled = !ArchiveCodexMenu.IsOpen;
            }
            if (ArchiveCodexMenu.IsOpen)
            {
                return;
            }

            UpdateSafeArea();
            if ((LensController == null || EmberInventory == null || ShadowRealm == null ||
                 CentralCore == null || cachedMementos == null || cachedMementos.Length == 0) &&
                Time.unscaledTime >= nextTargetResolveTime)
            {
                ResolveTargets();
                nextTargetResolveTime = Time.unscaledTime + 0.5f;
            }
            SyncStateFromTargets(immediate: false);
            AnimateVisuals(Time.unscaledDeltaTime);
        }

        public void RefreshDisplay()
        {
            if (canvas == null) BuildCanvas();
            ResolveTargets();
            SyncStateFromTargets(immediate: true);
            AnimateVisuals(0f);
        }

        private void BuildCanvas()
        {
            if (canvas != null) return;

            hudFont = Resources.Load<TMP_FontAsset>("UI/EmberHUD");
            if (hudFont == null) hudFont = ArchiveInterfaceStyle.BodyFont;
            if (hudFont == null) hudFont = TMP_Settings.defaultFontAsset;

            if (hudFont != null && hudFont.material != null)
            {
                hudTextMaterial = new Material(hudFont.material) { hideFlags = HideFlags.HideAndDontSave };
                hudTextMaterial.EnableKeyword("OUTLINE_ON");
                hudTextMaterial.SetColor(ShaderUtilities.ID_OutlineColor, OutlineInk);
                hudTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.14f);
            }

            var displayFont = ArchiveInterfaceStyle.DisplayFont;
            if (displayFont == null) displayFont = hudFont;
            if (displayFont != null && displayFont.material != null)
            {
                displayTextMaterial = new Material(displayFont.material) { hideFlags = HideFlags.HideAndDontSave };
                displayTextMaterial.EnableKeyword("OUTLINE_ON");
                displayTextMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.03f, 0.025f, 0.96f));
                displayTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            }

            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 98;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 1. Atmospheric Shadow Realm layers (behind HUD glyphs)
            shadowOverlay = CreateFullScreenImage("ShadowWash", PixelSprites.White, new Color(0.55f, 0.04f, 0.07f, 0f));
            shadowVignette = CreateFullScreenImage("ShadowVignette", PixelSprites.Vignette, new Color(0.28f, 0.02f, 0.05f, 0f));

            // 2. Safe area container
            var safeGo = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safeAreaRoot = safeGo.GetComponent<RectTransform>();
            safeAreaRoot.SetParent(transform, false);
            safeAreaRoot.anchorMin = Vector2.zero;
            safeAreaRoot.anchorMax = Vector2.one;
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            UpdateSafeArea();

            // 3. Top-right frameless instrument cluster
            var anchorGo = new GameObject("InstrumentCluster", typeof(RectTransform), typeof(CanvasGroup));
            clusterAnchor = anchorGo.GetComponent<RectTransform>();
            clusterAnchor.SetParent(safeAreaRoot, false);
            clusterAnchor.anchorMin = new Vector2(1f, 1f);
            clusterAnchor.anchorMax = new Vector2(1f, 1f);
            clusterAnchor.pivot = new Vector2(1f, 1f);
            clusterAnchor.anchoredPosition = new Vector2(-24f, -24f);
            clusterAnchor.sizeDelta = new Vector2(220f, 92f);

            clusterGroup = anchorGo.GetComponent<CanvasGroup>();
            clusterGroup.alpha = 0f;

            BuildLensRow();
            BuildEmberRow();
            BuildProgressRow();
            BuildStatusWhisper(displayFont);
        }

        private Image CreateFullScreenImage(string objName, Sprite sprite, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void BuildLensRow()
        {
            var dialGo = new GameObject("LensDial", typeof(RectTransform));
            lensDialRoot = dialGo.GetComponent<RectTransform>();
            lensDialRoot.SetParent(clusterAnchor, false);
            lensDialRoot.anchorMin = new Vector2(1f, 1f);
            lensDialRoot.anchorMax = new Vector2(1f, 1f);
            lensDialRoot.pivot = new Vector2(0.5f, 0.5f);
            lensDialRoot.anchoredPosition = new Vector2(-11f, -11f);
            lensDialRoot.sizeDelta = new Vector2(22f, 22f);

            lensHalo = CreateIcon("LensHalo", lensDialRoot, PixelSprites.Glow, new Vector2(52f, 52f), NormalAmber);
            lensCore = CreateIcon("LensCore", lensDialRoot, Level1PixelArt.LensCore, new Vector2(22f, 22f), NormalAmber);
            lensFrame = CreateIcon("LensFrame", lensDialRoot, Level1PixelArt.LensFrame, new Vector2(22f, 22f), Ivory);

            lensLabel = CreateHudText("LensLabel", clusterAnchor, new Vector2(-30f, -9f), 13f, TextAlignmentOptions.MidlineRight, Ivory);
            lensHintLabel = CreateHudText("LensHint", clusterAnchor, new Vector2(-30f, -21f), 9f, TextAlignmentOptions.MidlineRight, Muted);
            lensHintLabel.text = "Q / E";
        }

        private void BuildEmberRow()
        {
            const float rowY = -39f;
            const float stepX = 17f;

            var rowGo = new GameObject("EmberRow", typeof(RectTransform), typeof(CanvasGroup));
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.SetParent(clusterAnchor, false);
            rowRt.anchorMin = Vector2.zero;
            rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = Vector2.zero;
            rowRt.offsetMax = Vector2.zero;
            emberRowGroup = rowGo.GetComponent<CanvasGroup>();
            emberRowGroup.alpha = 0f;

            for (int i = 0; i < MaxEmberSlots; i++)
            {
                float x = -11f - i * stepX;

                var halo = CreateIcon($"EmberHalo_{i}", rowRt, PixelSprites.Glow, new Vector2(30f, 30f), Color.clear, topRight: true);
                halo.rectTransform.anchoredPosition = new Vector2(x, rowY);
                emberHalos[i] = halo;

                var icon = CreateIcon($"EmberSlot_{i}", rowRt, PixelSprites.EmberEmpty, new Vector2(12f, 16f), Muted, topRight: true);
                icon.rectTransform.anchoredPosition = new Vector2(x, rowY);
                emberIcons[i] = icon;
            }

            emberSelector = CreateIcon("EmberSelector", rowRt, Level1PixelArt.SelectorDiamond, new Vector2(7f, 7f), Ivory, topRight: true);
            emberSelectorRect = emberSelector.rectTransform;
            emberSelectorRect.anchoredPosition = new Vector2(-11f, rowY - 12f);
            emberSelector.enabled = false;

            float textX = -11f - 3 * stepX - 6f;
            emberLabel = CreateHudText("EmberLabel", rowRt, new Vector2(textX, rowY + 2f), 11f, TextAlignmentOptions.MidlineRight, Ivory);
            emberHintLabel = CreateHudText("EmberHint", rowRt, new Vector2(textX, rowY - 9f), 9f, TextAlignmentOptions.MidlineRight, Muted);
            emberHintLabel.text = "C";
        }

        private void BuildProgressRow()
        {
            const float rowY = -68f;
            const float prismStep = 12f;

            var progressGo = new GameObject("ProgressRow", typeof(RectTransform));
            progressRowRoot = progressGo.GetComponent<RectTransform>();
            progressRowRoot.SetParent(clusterAnchor, false);
            progressRowRoot.anchorMin = Vector2.zero;
            progressRowRoot.anchorMax = Vector2.one;
            progressRowRoot.offsetMin = Vector2.zero;
            progressRowRoot.offsetMax = Vector2.zero;

            // Core Crystals (right side, 4 prisms)
            for (int i = 0; i < CoreBeamSlots; i++)
            {
                // Left-to-right reading order ending at x = -8
                int visualIndex = CoreBeamSlots - 1 - i;
                float x = -9f - visualIndex * prismStep;
                var prism = CreateIcon($"CorePrism_{i}", progressRowRoot, Level1PixelArt.CorePrismEmpty, new Vector2(9f, 13f), Ivory, topRight: true);
                prism.rectTransform.anchoredPosition = new Vector2(x, rowY);
                corePrisms[i] = prism;
            }

            float coreCountX = -9f - (CoreBeamSlots - 1) * prismStep - 10f;
            coreCountLabel = CreateHudText("CoreCount", progressRowRoot, new Vector2(coreCountX, rowY), 9f, TextAlignmentOptions.MidlineRight, Muted);

            // Subtle vertical brass tick between Core and Mementos
            var divider = CreateIcon("ProgressTick", progressRowRoot, PixelSprites.White, new Vector2(1f, 9f), new Color(Gold.r, Gold.g, Gold.b, 0.45f), topRight: true);
            divider.rectTransform.anchoredPosition = new Vector2(coreCountX - 24f, rowY);

            mementoCountLabel = CreateHudText("MementoCount", progressRowRoot, new Vector2(coreCountX - 30f, rowY), 9f, TextAlignmentOptions.MidlineRight, Muted);
            EnsureMementoSeals(2);
        }

        private void EnsureMementoSeals(int count)
        {
            int clamped = Mathf.Clamp(count, 1, 6);
            if (clamped == lastMementoSlotsBuilt && mementoSeals.Count == clamped) return;

            for (int i = 0; i < mementoSeals.Count; i++)
            {
                if (mementoSeals[i] != null) Destroy(mementoSeals[i].gameObject);
            }
            mementoSeals.Clear();
            mementoPops.Clear();

            const float rowY = -68f;
            float baseX = -90f;
            Transform parentRoot = progressRowRoot != null ? progressRowRoot : clusterAnchor;
            for (int i = 0; i < clamped; i++)
            {
                int visualIdx = clamped - 1 - i;
                float x = baseX - visualIdx * 13f;
                var seal = CreateIcon($"MementoSeal_{i}", parentRoot, Level1PixelArt.MementoSealEmpty, new Vector2(11f, 11f), Ivory, topRight: true);
                seal.rectTransform.anchoredPosition = new Vector2(x, rowY);
                mementoSeals.Add(seal);
                mementoPops.Add(0f);
            }

            if (mementoCountLabel != null)
            {
                mementoCountLabel.rectTransform.anchoredPosition = new Vector2(baseX - (clamped - 1) * 13f - 10f, rowY);
            }

            lastMementoSlotsBuilt = clamped;
        }

        private void BuildStatusWhisper(TMP_FontAsset displayFont)
        {
            var bannerGo = new GameObject("StatusWhisper", typeof(RectTransform), typeof(CanvasGroup));
            bannerRect = bannerGo.GetComponent<RectTransform>();
            bannerRect.SetParent(safeAreaRoot, false);
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -26f);
            bannerRect.sizeDelta = new Vector2(340f, 38f);

            bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;

            var textGo = new GameObject("WhisperText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(bannerRect, false);
            textRt.anchorMin = new Vector2(0.5f, 1f);
            textRt.anchorMax = new Vector2(0.5f, 1f);
            textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(320f, 22f);

            statusBanner = textGo.GetComponent<TextMeshProUGUI>();
            if (displayFont != null) statusBanner.font = displayFont;
            if (displayTextMaterial != null) statusBanner.fontSharedMaterial = displayTextMaterial;
            statusBanner.fontSize = 14f;
            statusBanner.fontStyle = FontStyles.SmallCaps;
            statusBanner.characterSpacing = 4f;
            statusBanner.alignment = TextAlignmentOptions.Center;
            statusBanner.color = Ivory;
            statusBanner.raycastTarget = false;
            statusBanner.text = string.Empty;

            var filigreeSprite = Resources.Load<Sprite>("UI/HollowKnight_TopFiligree");
            if (filigreeSprite == null) filigreeSprite = Level1PixelArt.FiligreeRule;
            var filGo = new GameObject("WhisperFiligree", typeof(RectTransform), typeof(Image));
            var filRt = filGo.GetComponent<RectTransform>();
            filRt.SetParent(bannerRect, false);
            filRt.anchorMin = new Vector2(0.5f, 1f);
            filRt.anchorMax = new Vector2(0.5f, 1f);
            filRt.pivot = new Vector2(0.5f, 1f);
            filRt.anchoredPosition = new Vector2(0f, -22f);
            filRt.sizeDelta = new Vector2(184f, 23f);

            bannerFiligree = filGo.GetComponent<Image>();
            bannerFiligree.sprite = filigreeSprite;
            bannerFiligree.color = new Color(Gold.r, Gold.g, Gold.b, 0.78f);
            bannerFiligree.preserveAspect = true;
            bannerFiligree.raycastTarget = false;
        }

        private Image CreateIcon(string objName, Transform parent, Sprite sprite, Vector2 size, Color color, bool topRight = false)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (topRight)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI CreateHudText(string objName, Transform parent, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(130f, 18f);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (hudFont != null) tmp.font = hudFont;
            if (hudTextMaterial != null) tmp.fontSharedMaterial = hudTextMaterial;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;
            return tmp;
        }

        private void UpdateSafeArea()
        {
            if (safeAreaRoot == null) return;
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == lastSafeArea && screen == lastScreenSize) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            lastSafeArea = safe;
            lastScreenSize = screen;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= screen.x;
            min.y /= screen.y;
            max.x /= screen.x;
            max.y /= screen.y;

            safeAreaRoot.anchorMin = min;
            safeAreaRoot.anchorMax = max;
        }

        private void ResolveTargets()
        {
            if (LensController == null) LensController = FindFirstObjectByType<LanternLensController>();
            if (EmberInventory == null) EmberInventory = FindFirstObjectByType<EmberInventoryController>();
            if (ShadowRealm == null) ShadowRealm = FindFirstObjectByType<ShadowRealmZone2D>();
            if (CentralCore == null) CentralCore = FindFirstObjectByType<CentralCoreFinale2D>();
            if (cachedMementos == null || cachedMementos.Length == 0)
            {
                cachedMementos = FindObjectsByType<MementoCollectible2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (cachedMementos != null && cachedMementos.Length > 1)
                {
                    System.Array.Sort(cachedMementos, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                }
            }
        }

        private void SyncStateFromTargets(bool immediate)
        {
            LanternLensType lens = LensController != null ? LensController.ActiveLens : LanternLensType.Normal;
            bool inShadow = ShadowRealm != null && ShadowRealm.IsPlayerInside;

            if (lens != trackedLens || inShadow != trackedSuppressed)
            {
                trackedLens = lens;
                trackedSuppressed = inShadow;
                if (!immediate)
                {
                    lensPop = 1f;
                    hintTimer = Mathf.Max(hintTimer, 2.6f);
                }
                lensTargetAngle = -(int)lens * 90f;
            }

            if (lensLabel != null)
            {
                string nextLensText = inShadow ? "Extinguished" : FormatLensName(lens);
                if (lastLensLabelText != nextLensText)
                {
                    lastLensLabelText = nextLensText;
                    lensLabel.text = nextLensText;
                }
            }

            // Ember inventory strip
            int carriedCount = EmberInventory != null ? Mathf.Min(EmberInventory.CarriedEmbers.Count, MaxEmberSlots) : 0;
            EmberColor selected = EmberInventory != null ? EmberInventory.SelectedColor : EmberColor.None;
            bool usageBlocked = EmberInventory != null && EmberInventory.IsUsageBlocked;

            if (carriedCount != trackedEmberCount || selected != trackedSelectedEmber)
            {
                if (!immediate && carriedCount > trackedEmberCount && carriedCount > 0)
                {
                    emberPops[carriedCount - 1] = 1f;
                    hintTimer = Mathf.Max(hintTimer, 3.0f);
                }
                trackedEmberCount = carriedCount;
                trackedSelectedEmber = selected;
            }

            const float emberRowY = -39f;
            const float emberStepX = 18f;
            int selectedIndex = -1;

            for (int i = 0; i < MaxEmberSlots; i++)
            {
                var icon = emberIcons[i];
                var halo = emberHalos[i];
                if (icon == null) continue;

                bool showSlot = i < carriedCount && EmberInventory != null;
                icon.enabled = showSlot;

                if (showSlot)
                {
                    // Pack carried embers left-to-right flush against the right margin (-11f)
                    float slotX = -11f - (carriedCount - 1 - i) * emberStepX;
                    icon.rectTransform.anchoredPosition = new Vector2(slotX, emberRowY);

                    EmberColor col = EmberInventory.CarriedEmbers[i];
                    icon.sprite = Level1PixelArt.VeinEmberCrystal;
                    Color tint = LanternLensController.GetEmberColorTint(col);
                    icon.color = usageBlocked ? Color.Lerp(tint, SuppressedSlate, 0.7f) : tint;

                    bool isSel = col == selected && selectedIndex < 0;
                    if (isSel)
                    {
                        selectedIndex = i;
                        selectorTargetX = slotX;
                    }

                    if (halo != null)
                    {
                        halo.rectTransform.anchoredPosition = new Vector2(slotX, emberRowY);
                        halo.enabled = !usageBlocked && isSel;
                        halo.color = new Color(tint.r, tint.g, tint.b, isSel ? 0.28f : 0f);
                    }
                }
                else
                {
                    if (halo != null) halo.enabled = false;
                }
            }

            if (emberSelector != null)
            {
                emberSelector.enabled = selectedIndex >= 0 && carriedCount > 0;
                if (selectedIndex >= 0)
                {
                    selectorTargetX = -11f - (carriedCount - 1 - selectedIndex) * emberStepX;
                    Color selTint = LanternLensController.GetEmberColorTint(selected);
                    emberSelector.color = usageBlocked ? Muted : Color.Lerp(selTint, Ivory, 0.45f);
                    if (immediate) selectorCurrentX = selectorTargetX;
                }
            }

            if (emberLabel != null)
            {
                string nextEmberText = carriedCount == 0
                    ? string.Empty
                    : (usageBlocked ? "Embers Sealed" : FormatEmberName(selected));
                if (lastEmberLabelText != nextEmberText)
                {
                    lastEmberLabelText = nextEmberText;
                    emberLabel.text = nextEmberText;
                }
                float textShift = -11f - Mathf.Max(0, carriedCount - 1) * emberStepX - 14f;
                emberLabel.rectTransform.anchoredPosition = new Vector2(textShift, -37f);
                if (emberHintLabel != null)
                {
                    emberHintLabel.rectTransform.anchoredPosition = new Vector2(textShift, -48f);
                }
            }

            // Central Core & Mementos
            int beams = CentralCore != null ? Mathf.Clamp(CentralCore.ActiveBeamCount, 0, CoreBeamSlots) : 0;
            bool finale = CentralCore != null && CentralCore.IsGameEndingTriggered;
            if (beams != trackedBeamCount)
            {
                if (!immediate && beams > trackedBeamCount)
                {
                    for (int i = Mathf.Max(0, trackedBeamCount); i < beams && i < CoreBeamSlots; i++)
                        corePops[i] = 1f;
                }
                trackedBeamCount = beams;
            }
            trackedFinale = finale;

            for (int i = 0; i < CoreBeamSlots; i++)
            {
                bool lit = i < beams;
                corePrisms[i].sprite = lit ? Level1PixelArt.CorePrismLit : Level1PixelArt.CorePrismEmpty;
                corePrisms[i].color = lit ? NormalAmber : new Color(Ivory.r, Ivory.g, Ivory.b, 0.72f);
            }
            if (coreCountLabel != null && beams != lastDisplayedBeams)
            {
                lastDisplayedBeams = beams;
                coreCountLabel.text = $"{beams}/{CoreBeamSlots}";
                coreCountLabel.color = beams >= CoreBeamSlots ? NormalAmber : Muted;
            }

            int totalMementos = cachedMementos != null && cachedMementos.Length > 0 ? cachedMementos.Length : 2;
            EnsureMementoSeals(totalMementos);
            int collectedMementos = 0;
            if (cachedMementos != null)
            {
                for (int i = 0; i < cachedMementos.Length && i < mementoSeals.Count; i++)
                {
                    var m = cachedMementos[i];
                    bool got = m != null && m.IsCollected;
                    if (got) collectedMementos++;
                    mementoSeals[i].sprite = got ? Level1PixelArt.MementoSealLit : Level1PixelArt.MementoSealEmpty;
                    Color relicColor = m != null && m.IsSecretMemento
                        ? new Color(0.84f, 0.64f, 1.0f, 1f)
                        : Ivory;
                    mementoSeals[i].color = got ? relicColor : new Color(Ivory.r, Ivory.g, Ivory.b, 0.72f);
                }
            }
            if (collectedMementos != trackedMementoCount)
            {
                if (!immediate && collectedMementos > trackedMementoCount)
                {
                    for (int i = 0; i < mementoPops.Count; i++)
                        mementoPops[i] = 0.8f;
                }
                trackedMementoCount = collectedMementos;
            }
            if (mementoCountLabel != null &&
                (collectedMementos != lastDisplayedMementos || totalMementos != lastDisplayedTotalMementos))
            {
                lastDisplayedMementos = collectedMementos;
                lastDisplayedTotalMementos = totalMementos;
                mementoCountLabel.text = $"{collectedMementos}/{totalMementos}";
                mementoCountLabel.color = collectedMementos >= totalMementos ? Ivory : Muted;
            }

            // Atmospheric banner text (no AI slop status dumps)
            string nextBanner = string.Empty;
            if (inShadow) nextBanner = "THE SHADOW REALM";
            else if (finale) nextBanner = "THE CORE AWAKENS";

            if (statusBanner != null && nextBanner != activeBannerKey)
            {
                activeBannerKey = nextBanner;
                if (!string.IsNullOrEmpty(nextBanner))
                {
                    statusBanner.text = nextBanner;
                    statusBanner.color = inShadow ? new Color(0.95f, 0.56f, 0.58f, 1f) : Ivory;
                    if (bannerFiligree != null)
                    {
                        bannerFiligree.color = inShadow
                            ? new Color(0.78f, 0.24f, 0.28f, 0.75f)
                            : new Color(Gold.r, Gold.g, Gold.b, 0.78f);
                    }
                }
                else if (immediate)
                {
                    statusBanner.text = string.Empty;
                }
            }
        }

        private void AnimateVisuals(float dt)
        {
            bool reduced = ArchiveInterfaceStyle.ReducedMotion;
            float t = Time.unscaledTime;

            // Cluster fade & subtle vertical settle on entry
            if (clusterGroup != null && clusterAnchor != null)
            {
                if (reduced)
                {
                    clusterGroup.alpha = 1f;
                    clusterAnchor.anchoredPosition = new Vector2(-24f, -24f);
                }
                else
                {
                    entranceElapsed = Mathf.Min(EntranceDuration, entranceElapsed + dt);
                    float u = 1f - Mathf.Pow(1f - Mathf.Clamp01(entranceElapsed / EntranceDuration), 3f);
                    clusterGroup.alpha = u;
                    clusterAnchor.anchoredPosition = new Vector2(-24f, -16f - u * 8f);
                }
            }

            // Lens dial rotation, color blend & halo breath
            Color targetLensColor = trackedSuppressed ? SuppressedSlate : GetLensColor(trackedLens);
            if (reduced || dt <= 0f)
            {
                lensCurrentAngle = lensTargetAngle;
                lensCurrentColor = targetLensColor;
                lensPop = 0f;
            }
            else
            {
                lensCurrentAngle = Mathf.LerpAngle(lensCurrentAngle, lensTargetAngle, 1f - Mathf.Exp(-14f * dt));
                lensCurrentColor = Color.Lerp(lensCurrentColor, targetLensColor, 1f - Mathf.Exp(-12f * dt));
                lensPop = Mathf.MoveTowards(lensPop, 0f, dt / 0.28f);
            }

            float popWave = reduced ? 0f : Mathf.Sin(lensPop * Mathf.PI);
            if (lensDialRoot != null)
            {
                float s = 1f + popWave * 0.26f;
                lensDialRoot.localScale = new Vector3(s, s, 1f);
            }
            if (lensFrame != null)
            {
                lensFrame.rectTransform.localRotation = Quaternion.Euler(0f, 0f, lensCurrentAngle);
            }
            if (lensCore != null)
            {
                lensCore.color = lensCurrentColor;
            }
            if (lensHalo != null)
            {
                float breath = reduced ? 0f : Mathf.Sin(t * 2.2f) * 0.06f;
                float haloAlpha = (trackedSuppressed ? 0.14f : 0.34f) + popWave * 0.35f + breath;
                lensHalo.color = new Color(lensCurrentColor.r, lensCurrentColor.g, lensCurrentColor.b, Mathf.Clamp01(haloAlpha));
            }
            if (lensLabel != null)
            {
                lensLabel.color = Color.Lerp(Ivory, targetLensColor, trackedSuppressed ? 0.55f : 0.22f);
            }

            // Ember selector glide + per-slot spring pop
            if (emberSelectorRect != null)
            {
                selectorCurrentX = (reduced || dt <= 0f)
                    ? selectorTargetX
                    : Mathf.Lerp(selectorCurrentX, selectorTargetX, 1f - Mathf.Exp(-18f * dt));
                float bob = reduced ? 0f : Mathf.Sin(t * 3.4f) * 1.1f;
                emberSelectorRect.anchoredPosition = new Vector2(selectorCurrentX, -51f + bob);
            }

            for (int i = 0; i < MaxEmberSlots; i++)
            {
                emberPops[i] = (reduced || dt <= 0f) ? 0f : Mathf.MoveTowards(emberPops[i], 0f, dt / 0.26f);
                if (emberIcons[i] != null)
                {
                    float s = 1f + (reduced ? 0f : Mathf.Sin(emberPops[i] * Mathf.PI) * 0.3f);
                    emberIcons[i].rectTransform.localScale = new Vector3(s, s, 1f);
                }
            }

            // Core prism & Memento pops
            for (int i = 0; i < CoreBeamSlots; i++)
            {
                corePops[i] = (reduced || dt <= 0f) ? 0f : Mathf.MoveTowards(corePops[i], 0f, dt / 0.32f);
                if (corePrisms[i] != null)
                {
                    float s = 1f + (reduced ? 0f : Mathf.Sin(corePops[i] * Mathf.PI) * 0.32f);
                    corePrisms[i].rectTransform.localScale = new Vector3(s, s, 1f);
                }
            }
            for (int i = 0; i < mementoPops.Count && i < mementoSeals.Count; i++)
            {
                mementoPops[i] = (reduced || dt <= 0f) ? 0f : Mathf.MoveTowards(mementoPops[i], 0f, dt / 0.30f);
                if (mementoSeals[i] != null)
                {
                    float s = 1f + (reduced ? 0f : Mathf.Sin(mementoPops[i] * Mathf.PI) * 0.28f);
                    mementoSeals[i].rectTransform.localScale = new Vector3(s, s, 1f);
                }
            }

            // Quiet auto-fading key hints matching LanternHUD
            if (dt > 0f && hintTimer > 0f)
            {
                hintTimer = Mathf.Max(0f, hintTimer - dt);
            }
            float hintAlpha = hintTimer > HintFadeDuration
                ? 0.72f
                : Mathf.Clamp01(hintTimer / HintFadeDuration) * 0.72f;
            if (lensHintLabel != null) lensHintLabel.alpha = hintAlpha;
            if (emberHintLabel != null) emberHintLabel.alpha = trackedEmberCount > 1 ? hintAlpha : 0f;
            if (emberRowGroup != null)
            {
                float targetEmberAlpha = trackedEmberCount > 0 ? 1f : 0f;
                emberRowGroup.alpha = (reduced || dt <= 0f)
                    ? targetEmberAlpha
                    : Mathf.MoveTowards(emberRowGroup.alpha, targetEmberAlpha, dt * 6f);
            }
            if (progressRowRoot != null)
            {
                float targetY = trackedEmberCount > 0 ? 0f : 26f;
                float currentY = progressRowRoot.anchoredPosition.y;
                float nextY = (reduced || dt <= 0f)
                    ? targetY
                    : Mathf.Lerp(currentY, targetY, 1f - Mathf.Exp(-14f * dt));
                progressRowRoot.anchoredPosition = new Vector2(0f, nextY);
            }

            // Smooth Shadow Realm crimson monochrome wash + vignette
            float targetShadow = (ShadowRealm != null && ShadowRealm.IsPlayerInside) ? 1f : 0f;
            shadowWeight = (reduced || dt <= 0f)
                ? targetShadow
                : Mathf.MoveTowards(shadowWeight, targetShadow, dt * 3.2f);

            float dreadPulse = reduced ? 0f : Mathf.Sin(t * 2.1f) * 0.03f;
            if (shadowOverlay != null)
            {
                shadowOverlay.color = new Color(0.55f, 0.04f, 0.07f, Mathf.Clamp01(shadowWeight * (0.34f + dreadPulse)));
            }
            if (shadowVignette != null)
            {
                shadowVignette.color = new Color(0.28f, 0.02f, 0.05f, Mathf.Clamp01(shadowWeight * (0.82f + dreadPulse * 1.5f)));
            }

            // Filigree whisper banner fade & float
            float targetBanner = string.IsNullOrEmpty(activeBannerKey) ? 0f : 1f;
            bannerWeight = (reduced || dt <= 0f)
                ? targetBanner
                : Mathf.MoveTowards(bannerWeight, targetBanner, dt * 3.0f);
            if (bannerGroup != null)
            {
                bannerGroup.alpha = bannerWeight;
                if (bannerWeight <= 0.001f && statusBanner != null && string.IsNullOrEmpty(activeBannerKey))
                {
                    statusBanner.text = string.Empty;
                }
            }
            if (bannerRect != null)
            {
                float drift = reduced ? 0f : (1f - bannerWeight) * 6f;
                bannerRect.anchoredPosition = new Vector2(0f, -26f - drift);
            }
        }

        private static Color GetLensColor(LanternLensType lens)
        {
            switch (lens)
            {
                case LanternLensType.Ethereal: return EtherealCyan;
                case LanternLensType.Invert: return InvertCrimson;
                default: return NormalAmber;
            }
        }

        private static string FormatLensName(LanternLensType lens)
        {
            switch (lens)
            {
                case LanternLensType.Ethereal: return "Ethereal Lens";
                case LanternLensType.Invert: return "Invert Lens";
                default: return "Amber Lens";
            }
        }

        private static string FormatEmberName(EmberColor color)
        {
            switch (color)
            {
                case EmberColor.Orange: return "Orange Ember";
                case EmberColor.Cyan: return "Cyan Ember";
                case EmberColor.Crimson: return "Crimson Ember";
                default: return string.Empty;
            }
        }
    }
}
