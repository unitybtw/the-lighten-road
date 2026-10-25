using System;
using TheLightenRoad.Audio;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Three distinct cinematic, animated death screens responding contextually to the cause of death:
    ///
    /// 1. DARKNESS ("Consumed by the Dark"):
    ///    The lantern runs dry. A cold overhead fluorescent tube sputters and buzzes; 34 drifting
    ///    dust motes sway within the cold light wash; 8 creeping shadow tendrils swirl inward
    ///    from the corners; and the central ember takes its final, slow dying breaths.
    ///
    /// 2. VOID / ABYSS ("Lost to the Abyss"):
    ///    Falling into the bottomless pit. Pitch-black sky with 28 terminal velocity light streaks
    ///    rushing downward at high speed; 6 falling sparks trailing into the depths; rolling abyss
    ///    vapor undulating from below; and a deep midnight-purple tunnel vignette.
    ///
    /// 3. HAZARD ("Shattered Impact"):
    ///    Spikes, electrified water, or sudden trauma. Initial high-energy screen shake (perlin trauma
    ///    decay), bright warning flash, 14 sharp glass shards exploding and drifting in zero-g,
    ///    6 crackling electric arc spark lines zapping erratically around the fractured core,
    ///    and an emergency hazard pulse vignette.
    ///
    /// All three screens share:
    /// - Two large, accessible controller-first buttons: [ REIGNITE FLAME ] and [ RETURN TO ARCHIVE ]
    /// - Persistent Total Fallen counter stored in PlayerPrefs
    /// - Keyboard (W/S, Arrows, Enter, Space, R), Gamepad (D-pad, Left Stick, South) and Mouse support
    /// - Safe auto-respawn compatibility with headless automated test suites
    /// - In-editor / development hotkeys (1, 2, 3) and public PreviewDeath() for instant live preview
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
        private const string TotalDeathsPrefKey = "TheLightenRoad.TotalDeaths";
        private const float EmberY = 74f;

        [Header("Target Controller (auto-found if empty)")]
        [SerializeField] private PlayerRespawnController respawnController;

        [Header("Typography Font")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Fade Speeds")]
        [Tooltip("Higher is faster. 0.85 = about 1.2 seconds to reach the full screen.")]
        [SerializeField, Min(0.1f)] private float deathFadeSpeed = 0.85f;
        [SerializeField, Min(0.1f)] private float respawnFadeSpeed = 3.2f;

        // Visual Hierarchies & Elements
        private Canvas canvas;
        private RectTransform contentRoot;
        private Image blackout;
        private Image vignette;
        private Image ignitionFlash;

        // --- 1. Darkness Elements ---
        private GameObject darknessGroup;
        private Image lampCase;
        private Image lampTube;
        private Image lampCone;
        private Image coldWash;
        private const int DustCount = 34;
        private readonly Image[] dustImages = new Image[DustCount];
        private readonly RectTransform[] dustRects = new RectTransform[DustCount];
        private readonly Vector2[] dustVelocity = new Vector2[DustCount];
        private readonly float[] dustPhase = new float[DustCount];
        private readonly float[] dustAlpha = new float[DustCount];

        private const int TendrilCount = 8;
        private readonly Image[] tendrilImages = new Image[TendrilCount];
        private readonly RectTransform[] tendrilRects = new RectTransform[TendrilCount];
        private readonly Vector2[] tendrilBasePos = new Vector2[TendrilCount];
        private readonly float[] tendrilPhase = new float[TendrilCount];

        // --- 2. Void Elements ---
        private GameObject voidGroup;
        private const int StreakCount = 28;
        private readonly Image[] streakImages = new Image[StreakCount];
        private readonly RectTransform[] streakRects = new RectTransform[StreakCount];
        private readonly float[] streakSpeed = new float[StreakCount];
        private readonly float[] streakLength = new float[StreakCount];
        private Image abyssVapor;

        private const int FallingSparkCount = 6;
        private readonly Image[] fallingSparkImages = new Image[FallingSparkCount];
        private readonly RectTransform[] fallingSparkRects = new RectTransform[FallingSparkCount];
        private readonly float[] fallingSparkProgress = new float[FallingSparkCount];
        private readonly float[] fallingSparkSpeed = new float[FallingSparkCount];

        // --- 3. Hazard Elements ---
        private GameObject hazardGroup;
        private Image hazardFlash;
        private const int ShardCount = 14;
        private readonly Image[] shardImages = new Image[ShardCount];
        private readonly RectTransform[] shardRects = new RectTransform[ShardCount];
        private readonly Vector2[] shardVelocity = new Vector2[ShardCount];
        private readonly float[] shardRotSpeed = new float[ShardCount];
        private readonly float[] shardCurrentRot = new float[ShardCount];

        private const int ArcCount = 6;
        private readonly Image[] arcImages = new Image[ArcCount];
        private readonly RectTransform[] arcRects = new RectTransform[ArcCount];
        private readonly float[] arcTimer = new float[ArcCount];
        private readonly Vector2[] arcBasePos = new Vector2[ArcCount];

        // --- Shared Central Ember Elements ---
        private const int SparkCount = 4;
        private Image emberHalo;
        private Image emberIcon;
        private readonly Image[] sparkImages = new Image[SparkCount];
        private readonly RectTransform[] sparkRects = new RectTransform[SparkCount];
        private readonly float[] sparkProgress = new float[SparkCount];
        private readonly float[] sparkSpeed = new float[SparkCount];
        private readonly float[] sparkDrift = new float[SparkCount];

        // --- Shared Typography & UI Elements ---
        private TextMeshProUGUI titleText;
        private Image ruleLine;
        private Image ruleGem;
        private TextMeshProUGUI loreText;
        private TextMeshProUGUI causeLabel;
        private TextMeshProUGUI causeValue;
        private TextMeshProUGUI fallenLabel;
        private TextMeshProUGUI fallenValue;
        private Image statDivider;
        private TextMeshProUGUI hintText;
        private GameObject hintGlyphsRoot;
        private Image hintSelectIcon;
        private TextMeshProUGUI hintSelectText;
        private Image hintConfirmIcon;
        private TextMeshProUGUI hintConfirmText;
        private Image hintReturnIcon;
        private TextMeshProUGUI hintReturnText;

        private sealed class MenuButton
        {
            public RectTransform Rect;
            public Image Plate;
            public Image[] Rim;
            public Image Marker;
            public TextMeshProUGUI Label;
            public Color SelectedRim;
            public Color SelectedPlate;
            public Color SelectedText;
            public float Blend;
        }

        private readonly MenuButton[] buttons = new MenuButton[2];
        private int selectedButtonIndex;
        private float buttonPunch;

        // Lamp flicker state
        private float lampLevel = 1f;
        private float lampTimer = 0.4f;
        private int lampBurstSteps;
        private bool lampBursting;

        // Trauma / Screen shake state (for Hazard)
        private float screenTrauma;

        // Animation & Navigation State
        private float currentFadeAlpha;
        private float targetFadeAlpha;
        private float ignitionAlpha;
        private float deathElapsed;
        private bool isSubscribed;
        private bool armed;
        private bool busy;
        private bool lastHintUsedGamepad;
        private Vector2 lastMousePosition;
        private DeathReason currentReason = DeathReason.Darkness;
        private int totalDeaths;

        // Procedural Sprite Caches
        private static Sprite coneSprite;
        private static Sprite streakSprite;
        private static Sprite vaporSprite;
        private static Sprite shardSprite;
        private static Sprite arcSprite;
        private static Sprite tendrilSprite;

        // Public Properties for Inspection and Testing
        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => currentFadeAlpha > 0.01f || (canvas != null && canvas.enabled);
        public int SelectedButtonIndex => selectedButtonIndex;
        public int TotalDeaths => totalDeaths;
        public bool IsArmed => armed;
        public DeathReason CurrentDeathReason => currentReason;

        private void Awake()
        {
            totalDeaths = PlayerPrefs.GetInt(TotalDeathsPrefKey, 0);
            EnsureFontLoaded();
            BuildHierarchy();
        }

        private void Start()
        {
            if (respawnController == null)
                respawnController = FindAnyObjectByType<PlayerRespawnController>();

            TrySubscribe();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(PlayerRespawnController controller)
        {
            Unsubscribe();
            respawnController = controller;
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (respawnController == null || isSubscribed)
                return;

            respawnController.OnPlayerDied += HandlePlayerDied;
            respawnController.OnPlayerRespawned += HandlePlayerRespawned;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || respawnController == null)
            {
                isSubscribed = false;
                return;
            }

            respawnController.OnPlayerDied -= HandlePlayerDied;
            respawnController.OnPlayerRespawned -= HandlePlayerRespawned;
            // Never leave the controller stuck waiting for a screen that no longer exists.
            respawnController.AutoRespawnEnabled = true;
            respawnController.AllowManualRespawnInput = true;
            isSubscribed = false;
        }

        private void EnsureFontLoaded()
        {
            if (fontAsset != null) return;

#if UNITY_EDITOR
            fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Scenes/Sandbox/Sirac/TestChamber/ArchiveFont.asset");
#endif
            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("UI/MenuType");
            if (fontAsset == null)
                fontAsset = Resources.Load<TMP_FontAsset>("UI/EmberHUD");
            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;
        }

        // ------------------------------------------------------------------ Events

        public void HandlePlayerDied(DeathReason reason)
        {
            currentReason = reason;
            targetFadeAlpha = 1f;
            deathElapsed = 0f;
            armed = false;
            busy = false;
            buttonPunch = 0f;
            if (Mouse.current != null)
                lastMousePosition = Mouse.current.position.ReadValue();
            SetSelectedButton(0, silent: true);

            totalDeaths++;
            PlayerPrefs.SetInt(TotalDeathsPrefKey, totalDeaths);

            if (respawnController != null)
            {
                // Auto-respawn is preserved so test suites and idle players reignite naturally;
                // manual navigation down pauses it when selecting Return to Archive.
                respawnController.AutoRespawnEnabled = true;
            }

            // Configure archetype-specific visual groups and reset particles
            ConfigureArchetype(reason);

            ControllerGlyphService.Rumble(0.65f, 0.85f, 0.40f);

            if (SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayFlareExtinguish();

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;
        }

        public void HandlePlayerRespawned()
        {
            targetFadeAlpha = 0f;
            ignitionAlpha = 1f;
            armed = false;
            screenTrauma = 0f;

            if (respawnController != null)
            {
                respawnController.AutoRespawnEnabled = true;
                respawnController.AllowManualRespawnInput = true;
            }

            if (SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayFlareIgnite();
        }

        /// <summary>
        /// Public testing hook: Trigger any death screen on demand with one call.
        /// </summary>
        public void PreviewDeath(DeathReason reason)
        {
            HandlePlayerDied(reason);
        }

        // ------------------------------------------------------------------ Hierarchy

        private void BuildHierarchy()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Fullscreen backdrop
            blackout = CreateFullscreen("Blackout", transform, PixelSprites.White);
            vignette = CreateFullscreen("Vignette", transform, PixelSprites.Vignette);

            // Content root (shakes during Hazard trauma)
            var contentRootGo = new GameObject("ContentRoot", typeof(RectTransform));
            contentRootGo.transform.SetParent(transform, false);
            contentRoot = contentRootGo.GetComponent<RectTransform>();
            SetFullscreen(contentRoot);

            // --- 1. Darkness Group ---
            darknessGroup = new GameObject("DarknessGroup", typeof(RectTransform));
            darknessGroup.transform.SetParent(contentRoot, false);
            SetFullscreen(darknessGroup.GetComponent<RectTransform>());

            coldWash = CreateFullscreen("ColdWash", darknessGroup.transform, PixelSprites.White);
            lampCone = CreateImage("LampCone", darknessGroup.transform, new Vector2(0f, 134f), new Vector2(340f, 272f), GetConeSprite(), Color.clear);
            lampCase = CreateImage("LampCase", darknessGroup.transform, new Vector2(0f, 252f), new Vector2(204f, 16f), PixelSprites.White, Color.clear);
            lampTube = CreateImage("LampTube", darknessGroup.transform, new Vector2(0f, 248f), new Vector2(190f, 6f), PixelSprites.White, Color.clear);

            for (int i = 0; i < DustCount; i++)
            {
                dustImages[i] = CreateImage($"Dust_{i}", darknessGroup.transform, Vector2.zero, new Vector2(3f, 3f), PixelSprites.White, Color.clear);
                dustRects[i] = dustImages[i].rectTransform;
                ResetDust(i, UnityEngine.Random.value);
            }

            for (int i = 0; i < TendrilCount; i++)
            {
                tendrilImages[i] = CreateImage($"Tendril_{i}", darknessGroup.transform, Vector2.zero, new Vector2(160f, 160f), GetTendrilSprite(), Color.clear);
                tendrilRects[i] = tendrilImages[i].rectTransform;
                float angle = i * (Mathf.PI * 2f / TendrilCount);
                tendrilBasePos[i] = new Vector2(Mathf.Cos(angle) * 440f, Mathf.Sin(angle) * 240f);
                tendrilPhase[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            }

            // --- 2. Void Group ---
            voidGroup = new GameObject("VoidGroup", typeof(RectTransform));
            voidGroup.transform.SetParent(contentRoot, false);
            SetFullscreen(voidGroup.GetComponent<RectTransform>());

            for (int i = 0; i < StreakCount; i++)
            {
                streakImages[i] = CreateImage($"Streak_{i}", voidGroup.transform, Vector2.zero, new Vector2(2f, 50f), GetStreakSprite(), Color.clear);
                streakRects[i] = streakImages[i].rectTransform;
                ResetStreak(i, UnityEngine.Random.value);
            }

            abyssVapor = CreateImage("AbyssVapor", voidGroup.transform, new Vector2(0f, -220f), new Vector2(960f, 180f), GetVaporSprite(), Color.clear);

            for (int i = 0; i < FallingSparkCount; i++)
            {
                fallingSparkImages[i] = CreateImage($"FallingSpark_{i}", voidGroup.transform, Vector2.zero, new Vector2(3f, 7f), PixelSprites.Spark, Color.clear);
                fallingSparkRects[i] = fallingSparkImages[i].rectTransform;
                ResetFallingSpark(i, UnityEngine.Random.value);
            }

            // --- 3. Hazard Group ---
            hazardGroup = new GameObject("HazardGroup", typeof(RectTransform));
            hazardGroup.transform.SetParent(contentRoot, false);
            SetFullscreen(hazardGroup.GetComponent<RectTransform>());

            hazardFlash = CreateFullscreen("HazardFlash", hazardGroup.transform, PixelSprites.White);

            for (int i = 0; i < ShardCount; i++)
            {
                shardImages[i] = CreateImage($"Shard_{i}", hazardGroup.transform, Vector2.zero, new Vector2(20f, 20f), GetShardSprite(), Color.clear);
                shardRects[i] = shardImages[i].rectTransform;
                ResetShard(i);
            }

            for (int i = 0; i < ArcCount; i++)
            {
                arcImages[i] = CreateImage($"Arc_{i}", hazardGroup.transform, Vector2.zero, new Vector2(32f, 32f), GetArcSprite(), Color.clear);
                arcRects[i] = arcImages[i].rectTransform;
                float angle = i * (Mathf.PI * 2f / ArcCount);
                arcBasePos[i] = new Vector2(Mathf.Cos(angle) * 38f, Mathf.Sin(angle) * 38f + EmberY);
                arcTimer[i] = UnityEngine.Random.Range(0.02f, 0.12f);
            }

            // --- Central Ember ---
            emberHalo = CreateImage("EmberHalo", contentRoot, new Vector2(0f, EmberY), new Vector2(68f, 68f), PixelSprites.Glow, Color.clear);
            emberIcon = CreateImage("EmberIcon", contentRoot, new Vector2(0f, EmberY), new Vector2(24f, 28f), PixelSprites.Flame, Color.clear);

            for (int i = 0; i < SparkCount; i++)
            {
                sparkImages[i] = CreateImage($"Spark_{i}", contentRoot, Vector2.zero, new Vector2(3f, 3f), PixelSprites.Spark, Color.clear);
                sparkRects[i] = sparkImages[i].rectTransform;
                ResetSpark(i, UnityEngine.Random.value);
            }

            // --- Typography ---
            titleText = AddTMPText("Title", contentRoot, new Vector2(0f, 24f), new Vector2(800f, 40f), 28f, TextAlignmentOptions.Center);
            titleText.characterSpacing = 8f;
            titleText.fontStyle = FontStyles.Bold;

            ruleLine = CreateImage("RuleLine", contentRoot, new Vector2(0f, 2f), new Vector2(260f, 1f), PixelSprites.White, Color.clear);
            ruleGem = CreateImage("RuleGem", contentRoot, new Vector2(0f, 2f), new Vector2(7f, 7f), PixelSprites.White, Color.clear);
            ruleGem.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            loreText = AddTMPText("Lore", contentRoot, new Vector2(0f, -22f), new Vector2(700f, 26f), 15f, TextAlignmentOptions.Center);

            // --- Stats ---
            causeLabel = AddTMPText("CauseLabel", contentRoot, new Vector2(-60f, -54f), new Vector2(180f, 16f), 11f, TextAlignmentOptions.Center);
            causeLabel.text = "CAUSE";
            causeLabel.characterSpacing = 6f;

            causeValue = AddTMPText("CauseValue", contentRoot, new Vector2(-60f, -74f), new Vector2(200f, 24f), 17f, TextAlignmentOptions.Center);
            causeValue.fontStyle = FontStyles.Bold;

            fallenLabel = AddTMPText("FallenLabel", contentRoot, new Vector2(60f, -54f), new Vector2(180f, 16f), 11f, TextAlignmentOptions.Center);
            fallenLabel.text = "FALLEN";
            fallenLabel.characterSpacing = 6f;

            fallenValue = AddTMPText("FallenValue", contentRoot, new Vector2(60f, -74f), new Vector2(120f, 24f), 17f, TextAlignmentOptions.Center);
            fallenValue.fontStyle = FontStyles.Bold;

            statDivider = CreateImage("StatDivider", contentRoot, new Vector2(0f, -64f), new Vector2(1f, 32f), PixelSprites.White, Color.clear);

            // --- Buttons ---
            buttons[0] = BuildButton(contentRoot, 0, new Vector2(0f, -116f), new Vector2(380f, 50f), "REIGNITE FLAME");
            buttons[1] = BuildButton(contentRoot, 1, new Vector2(0f, -172f), new Vector2(380f, 46f), "RETURN TO ARCHIVE");

            // --- Hint & Ignition Flash ---
            hintText = AddTMPText("Hint", contentRoot, new Vector2(0f, -225f), new Vector2(500f, 20f), 12.5f, TextAlignmentOptions.Center);
            hintText.characterSpacing = 4f;

            // Authentic Controller Glyphs Footer Bar
            hintGlyphsRoot = new GameObject("HintGlyphsBar", typeof(RectTransform));
            hintGlyphsRoot.transform.SetParent(contentRoot, false);
            var hgRect = hintGlyphsRoot.GetComponent<RectTransform>();
            SetCentered(hgRect, new Vector2(0f, -225f), new Vector2(620f, 38f));

            // Item 1: D-Pad Select
            hintSelectIcon = CreateImage("SelectIcon", hintGlyphsRoot.transform, new Vector2(-235f, 0f), new Vector2(34f, 34f), null, Color.white);
            hintSelectText = AddTMPText("SelectText", hintGlyphsRoot.transform, new Vector2(-155f, 0f), new Vector2(100f, 24f), 13.5f, TextAlignmentOptions.Left);
            hintSelectText.text = "SELECT";
            hintSelectText.fontStyle = FontStyles.Bold;
            hintSelectText.characterSpacing = 2.5f;

            // Item 2: Confirm Button
            hintConfirmIcon = CreateImage("ConfirmIcon", hintGlyphsRoot.transform, new Vector2(-40f, 0f), new Vector2(34f, 34f), null, Color.white);
            hintConfirmText = AddTMPText("ConfirmText", hintGlyphsRoot.transform, new Vector2(40f, 0f), new Vector2(100f, 24f), 13.5f, TextAlignmentOptions.Left);
            hintConfirmText.text = "CONFIRM";
            hintConfirmText.fontStyle = FontStyles.Bold;
            hintConfirmText.characterSpacing = 2.5f;

            // Item 3: Return Button
            hintReturnIcon = CreateImage("ReturnIcon", hintGlyphsRoot.transform, new Vector2(155f, 0f), new Vector2(34f, 34f), null, Color.white);
            hintReturnText = AddTMPText("ReturnText", hintGlyphsRoot.transform, new Vector2(235f, 0f), new Vector2(90f, 24f), 13.5f, TextAlignmentOptions.Left);
            hintReturnText.text = "RETURN";
            hintReturnText.fontStyle = FontStyles.Bold;
            hintReturnText.characterSpacing = 2.5f;

            hintGlyphsRoot.SetActive(false);

            ignitionFlash = CreateFullscreen("IgnitionFlash", transform, PixelSprites.White);

            canvas.enabled = false;
        }

        private MenuButton BuildButton(Transform parent, int index, Vector2 pos, Vector2 size, string label)
        {
            var btnGo = new GameObject($"Button_{index}", typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.GetComponent<RectTransform>();
            SetCentered(rect, pos, size);

            var plate = CreateImage("Plate", btnGo.transform, Vector2.zero, size, PixelSprites.Pill, Color.clear);
            plate.type = Image.Type.Sliced;

            var rim = new Image[4];
            const float t = 1f;
            rim[0] = CreateImage("RimT", btnGo.transform, new Vector2(0f, size.y * 0.5f - t * 0.5f), new Vector2(size.x, t), PixelSprites.White, Color.clear);
            rim[1] = CreateImage("RimB", btnGo.transform, new Vector2(0f, -size.y * 0.5f + t * 0.5f), new Vector2(size.x, t), PixelSprites.White, Color.clear);
            rim[2] = CreateImage("RimL", btnGo.transform, new Vector2(-size.x * 0.5f + t * 0.5f, 0f), new Vector2(t, size.y), PixelSprites.White, Color.clear);
            rim[3] = CreateImage("RimR", btnGo.transform, new Vector2(size.x * 0.5f - t * 0.5f, 0f), new Vector2(t, size.y), PixelSprites.White, Color.clear);

            var marker = CreateImage("Marker", btnGo.transform, new Vector2(-size.x * 0.5f + 20f, 0f), new Vector2(10f, 12f), PixelSprites.Flame, Color.clear);

            var txt = AddTMPText("Label", btnGo.transform, Vector2.zero, size, 14f, TextAlignmentOptions.Center);
            txt.text = label;
            txt.characterSpacing = 5f;
            txt.fontStyle = FontStyles.Bold;

            return new MenuButton
            {
                Rect = rect,
                Plate = plate,
                Rim = rim,
                Marker = marker,
                Label = txt,
                SelectedRim = new Color(1f, 0.72f, 0.32f, 0.95f),
                SelectedPlate = new Color(0.24f, 0.16f, 0.08f, 0.65f),
                SelectedText = new Color(1f, 0.94f, 0.82f, 1f),
                Blend = 0f
            };
        }

        // ------------------------------------------------------------------ Configuration

        private void ConfigureArchetype(DeathReason reason)
        {
            darknessGroup.SetActive(reason == DeathReason.Darkness);
            voidGroup.SetActive(reason == DeathReason.Void);
            hazardGroup.SetActive(reason == DeathReason.Hazard);

            fallenValue.text = totalDeaths.ToString("D3");

            switch (reason)
            {
                case DeathReason.Darkness:
                    titleText.text = "THE LIGHT HAS FADED";
                    loreText.text = "Your lantern ran dry. The dark kept the rest.";
                    causeValue.text = "DARKNESS";
                    emberIcon.sprite = PixelSprites.Flame;

                    lampLevel = 1f;
                    lampBursting = true;
                    lampBurstSteps = 4;
                    lampTimer = 0.05f;

                    for (int i = 0; i < DustCount; i++)
                        ResetDust(i, UnityEngine.Random.value);
                    for (int i = 0; i < SparkCount; i++)
                        ResetSpark(i, UnityEngine.Random.value);

                    buttons[0].SelectedRim = new Color(1.0f, 0.72f, 0.32f, 0.95f);
                    buttons[0].SelectedPlate = new Color(0.24f, 0.16f, 0.08f, 0.65f);
                    break;

                case DeathReason.Void:
                    titleText.text = "LOST TO THE ABYSS";
                    loreText.text = "The ground gave way. Some depths have no bottom.";
                    causeValue.text = "THE VOID";
                    emberIcon.sprite = PixelSprites.Flame;

                    for (int i = 0; i < StreakCount; i++)
                        ResetStreak(i, UnityEngine.Random.value);
                    for (int i = 0; i < FallingSparkCount; i++)
                        ResetFallingSpark(i, UnityEngine.Random.value);

                    buttons[0].SelectedRim = new Color(0.55f, 0.72f, 1.0f, 0.95f);
                    buttons[0].SelectedPlate = new Color(0.08f, 0.14f, 0.28f, 0.65f);
                    break;

                case DeathReason.Hazard:
                    titleText.text = "SHATTERED BY HAZARD";
                    loreText.text = "A sudden rupture. Glass and bone broke together.";
                    causeValue.text = "LETHAL HAZARD";
                    emberIcon.sprite = PixelSprites.EmberCracks;

                    screenTrauma = 1.0f;
                    hazardFlash.color = new Color(1f, 0.20f, 0.25f, 0.85f);

                    for (int i = 0; i < ShardCount; i++)
                        ResetShard(i);
                    for (int i = 0; i < ArcCount; i++)
                    {
                        arcTimer[i] = UnityEngine.Random.Range(0.02f, 0.10f);
                        UpdateArcPosition(i);
                    }

                    buttons[0].SelectedRim = new Color(1.0f, 0.40f, 0.45f, 0.95f);
                    buttons[0].SelectedPlate = new Color(0.28f, 0.08f, 0.10f, 0.65f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Per-frame Update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;

            UpdateVisuals(dt, time);
            HandleDebugKeys();

            if (currentFadeAlpha < 0.98f)
                return;

            if (!armed)
            {
                // Delay 1 second and verify confirm keys released before accepting input
                if (deathElapsed >= 1.0f && !IsConfirmHeld())
                    armed = true;
                return;
            }

            if (busy) return;

            HandleNavigation();
            HandlePointer();
        }

        private void HandleDebugKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                PreviewDeath(DeathReason.Darkness);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                PreviewDeath(DeathReason.Void);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                PreviewDeath(DeathReason.Hazard);
#endif
        }

        private void UpdateVisuals(float dt, float time)
        {
            float speed = targetFadeAlpha > currentFadeAlpha ? deathFadeSpeed : respawnFadeSpeed;
            currentFadeAlpha = Mathf.MoveTowards(currentFadeAlpha, targetFadeAlpha, speed * dt);

            if (targetFadeAlpha > 0f)
                deathElapsed += dt;

            if (ignitionAlpha > 0f)
                ignitionAlpha = Mathf.MoveTowards(ignitionAlpha, 0f, 2.2f * dt);

            if (currentFadeAlpha <= 0.001f && ignitionAlpha <= 0.001f)
            {
                if (canvas != null && canvas.enabled)
                    canvas.enabled = false;
                return;
            }

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;

            float fade = currentFadeAlpha;
            float sVoid = Stage(fade, 0f, 0.55f);
            float sTop = Stage(fade, 0.15f, 0.50f);
            float sEmber = Stage(fade, 0.30f, 0.60f);
            float sTitle = Stage(fade, 0.50f, 0.75f);
            float sStats = Stage(fade, 0.65f, 0.88f);
            float sMenu = Stage(fade, 0.80f, 1.00f);

            // 1. Fullscreen Backdrop & Vignette
            Color bgCol = currentReason == DeathReason.Void ? new Color(0.03f, 0.04f, 0.09f, 0.98f) :
                          currentReason == DeathReason.Hazard ? new Color(0.08f, 0.03f, 0.04f, 0.98f) :
                          new Color(0.02f, 0.04f, 0.04f, 0.98f);
            blackout.color = new Color(bgCol.r, bgCol.g, bgCol.b, sVoid * bgCol.a);

            Color vigTint = currentReason == DeathReason.Void ? new Color(0.40f, 0.45f, 0.70f, sVoid * 0.85f) :
                            currentReason == DeathReason.Hazard ? new Color(0.90f, 0.30f, 0.35f, sVoid * (0.6f + 0.25f * Mathf.Sin(time * 5f))) :
                            new Color(0.55f, 0.70f, 0.65f, sVoid * 0.75f);
            vignette.color = vigTint;

            // 2. Archetype Animations
            switch (currentReason)
            {
                case DeathReason.Darkness:
                    AnimateDarkness(sTop, sEmber, dt, time);
                    break;
                case DeathReason.Void:
                    AnimateVoid(sTop, sEmber, dt, time);
                    break;
                case DeathReason.Hazard:
                    AnimateHazard(sTop, sEmber, dt, time);
                    break;
            }

            // 3. Central Ember
            float life = Mathf.Lerp(1f, 0.28f, Mathf.Clamp01(deathElapsed / 3.2f));
            float breath = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);

            Color haloColor = currentReason == DeathReason.Void ? new Color(0.55f, 0.72f, 1.0f) :
                              currentReason == DeathReason.Hazard ? new Color(1.0f, 0.35f, 0.35f) :
                              new Color(1.0f, 0.62f, 0.22f);

            float haloAlpha = sEmber * (0.12f + 0.36f * life) * (0.75f + 0.25f * breath);
            emberHalo.color = new Color(haloColor.r, haloColor.g, haloColor.b, haloAlpha);
            float haloScale = 0.75f + 0.55f * life + 0.06f * breath;
            emberHalo.rectTransform.localScale = new Vector3(haloScale, haloScale, 1f);

            float bright = 0.50f + 0.50f * life;
            Color iconColor = currentReason == DeathReason.Void ? new Color(0.70f * bright, 0.85f * bright, 1.0f * bright, sEmber) :
                              currentReason == DeathReason.Hazard ? new Color(1.0f * bright, 0.45f * bright, 0.45f * bright, sEmber) :
                              new Color(0.96f * bright, 0.66f * bright, 0.36f * bright, sEmber);
            emberIcon.color = iconColor;

            // Sparks rising from the ember
            for (int i = 0; i < SparkCount; i++)
            {
                sparkProgress[i] += dt * sparkSpeed[i] * (0.35f + 0.65f * life);
                if (sparkProgress[i] >= 1f)
                    ResetSpark(i, 0f);

                float t = sparkProgress[i];
                sparkRects[i].anchoredPosition = new Vector2(
                    Mathf.Round(sparkDrift[i] + Mathf.Sin(time * 2.4f + i * 1.7f) * 3f),
                    Mathf.Round(EmberY + 8f + t * 46f));
                float fadeOut = (1f - t) * Mathf.Clamp01(t * 6f);
                Color c = Color.Lerp(haloColor, Color.white, t * 0.4f);
                c.a = sEmber * fadeOut * life * 0.9f;
                sparkImages[i].color = c;
            }

            // 4. Typography
            Color titleCol = currentReason == DeathReason.Void ? new Color(0.85f, 0.92f, 1.0f) :
                             currentReason == DeathReason.Hazard ? new Color(1.0f, 0.88f, 0.88f) :
                             new Color(0.86f, 0.94f, 0.90f);
            titleText.color = new Color(titleCol.r, titleCol.g, titleCol.b, sTitle);

            ruleLine.color = new Color(titleCol.r, titleCol.g, titleCol.b, sTitle * 0.38f);
            ruleGem.color = new Color(haloColor.r, haloColor.g, haloColor.b, sTitle * 0.9f);
            loreText.color = new Color(0.65f, 0.75f, 0.72f, sTitle * 0.95f);

            // 5. Stats
            Color lab = new Color(0.45f, 0.58f, 0.54f, sStats);
            causeLabel.color = lab;
            fallenLabel.color = lab;
            causeValue.color = new Color(titleCol.r, titleCol.g, titleCol.b, sStats);
            fallenValue.color = new Color(haloColor.r, haloColor.g, haloColor.b, sStats);
            statDivider.color = new Color(titleCol.r, titleCol.g, titleCol.b, sStats * 0.30f);

            // 6. Buttons
            buttonPunch = Mathf.MoveTowards(buttonPunch, 0f, 5f * dt);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            for (int i = 0; i < buttons.Length; i++)
                UpdateButton(buttons[i], i == selectedButtonIndex, sMenu, pulse, dt, i == selectedButtonIndex ? buttonPunch : 0f);

            // 7. Hint & Controller Glyphs
            var style = ControllerGlyphService.ActiveStyle;
            bool isGamepad = (style == ControllerPromptStyle.PlayStation || style == ControllerPromptStyle.Xbox);

            if (hintGlyphsRoot != null)
                hintGlyphsRoot.SetActive(isGamepad);
            if (hintText != null)
                hintText.gameObject.SetActive(!isGamepad);

            float hintAlpha = armed ? sMenu : sMenu * 0.35f;

            if (isGamepad)
            {
                var selectSprite = ControllerGlyphService.GetGlyphSprite("DPad", style);
                var confirmSprite = ControllerGlyphService.GetActionSprite(GlyphAction.Confirm, style);
                var returnSprite = ControllerGlyphService.GetActionSprite(GlyphAction.Cancel, style);

                Color iconCol = new Color(1f, 1f, 1f, hintAlpha * 0.95f);
                Color textCol = new Color(0.78f, 0.86f, 0.92f, hintAlpha * 0.9f);

                if (hintSelectIcon != null) { hintSelectIcon.sprite = selectSprite; hintSelectIcon.color = iconCol; }
                if (hintConfirmIcon != null) { hintConfirmIcon.sprite = confirmSprite; hintConfirmIcon.color = iconCol; }
                if (hintReturnIcon != null) { hintReturnIcon.sprite = returnSprite; hintReturnIcon.color = iconCol; }
                if (hintSelectText != null) hintSelectText.color = textCol;
                if (hintConfirmText != null) hintConfirmText.color = textCol;
                if (hintReturnText != null) hintReturnText.color = textCol;
            }
            else
            {
                hintText.text = "W / S / ARROWS  SELECT        ENTER  CONFIRM        ESC  RETURN";
                hintText.color = new Color(0.45f, 0.58f, 0.54f, hintAlpha * 0.9f);
            }

            // 8. Re-ignition Flash
            if (ignitionAlpha > 0f)
            {
                ignitionFlash.color = new Color(1f, 0.84f, 0.50f, ignitionAlpha * 0.65f);
                float s = 1f + (1f - ignitionAlpha) * 1.8f;
                ignitionFlash.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                ignitionFlash.color = Color.clear;
            }
        }

        // ------------------------------------------------------------------ Archetype Animations

        private void AnimateDarkness(float sTop, float sEmber, float dt, float time)
        {
            if (sTop > 0f)
                UpdateLamp(dt);

            Color tubeDark = new Color(0.12f, 0.17f, 0.15f, 1f);
            Color tubeLit = new Color(0.86f, 0.96f, 0.92f, 1f);
            Color tube = Color.Lerp(tubeDark, tubeLit, Mathf.Clamp01(lampLevel));
            tube.a = sTop;
            lampTube.color = tube;
            lampCase.color = new Color(0.07f, 0.10f, 0.09f, sTop);
            lampCone.color = new Color(0.66f, 0.80f, 0.75f, sTop * 0.32f * Mathf.Clamp01(lampLevel));
            coldWash.color = new Color(0.20f, 0.36f, 0.31f, sTop * 0.10f * Mathf.Clamp01(lampLevel));

            // Floating dust motes inside the cone
            for (int i = 0; i < DustCount; i++)
            {
                Vector2 p = dustRects[i].anchoredPosition;
                p.x += Mathf.Sin(time * 0.7f + dustPhase[i]) * 3f * dt + dustVelocity[i].x * dt;
                p.y += dustVelocity[i].y * dt;
                if (p.y < -184f)
                {
                    p.y = 184f;
                    p.x = UnityEngine.Random.Range(-300f, 300f);
                }
                dustRects[i].anchoredPosition = p;

                float cone = Mathf.Clamp01(1f - Mathf.Abs(p.x) / 230f) * Mathf.Clamp01((p.y + 150f) / 280f);
                float a = dustAlpha[i] * (0.18f + 0.82f * cone) * Mathf.Clamp01(0.35f + lampLevel * 0.65f);
                dustImages[i].color = new Color(0.66f, 0.80f, 0.75f, sTop * a);
            }

            // Swirling shadow tendrils at screen corners
            for (int i = 0; i < TendrilCount; i++)
            {
                float breathe = Mathf.Sin(time * 1.4f + tendrilPhase[i]);
                Vector2 p = tendrilBasePos[i] + new Vector2(Mathf.Sin(time * 0.9f + tendrilPhase[i]) * 24f, Mathf.Cos(time * 0.8f + tendrilPhase[i]) * 18f);
                tendrilRects[i].anchoredPosition = p;
                float scale = 1.0f + 0.25f * breathe;
                tendrilRects[i].localScale = new Vector3(scale, scale, 1f);
                float a = sTop * (0.22f + 0.18f * breathe);
                tendrilImages[i].color = new Color(0.01f, 0.02f, 0.03f, a);
            }
        }

        private void AnimateVoid(float sTop, float sEmber, float dt, float time)
        {
            // Vertical falling speed streaks (terminal velocity)
            for (int i = 0; i < StreakCount; i++)
            {
                Vector2 p = streakRects[i].anchoredPosition;
                p.y -= dt * streakSpeed[i];
                if (p.y < -300f)
                {
                    p.y = 300f;
                    p.x = UnityEngine.Random.Range(-440f, 440f);
                    streakSpeed[i] = UnityEngine.Random.Range(380f, 780f);
                }
                streakRects[i].anchoredPosition = p;

                float a = sTop * UnityEngine.Random.Range(0.25f, 0.55f);
                streakImages[i].color = new Color(0.55f, 0.72f, 1.0f, a);
            }

            // Abyss rolling vapor at the bottom
            float vaporWave = Mathf.Sin(time * 1.2f) * 10f;
            abyssVapor.rectTransform.anchoredPosition = new Vector2(0f, -220f + vaporWave);
            float vaporAlpha = sTop * (0.45f + 0.15f * Mathf.Sin(time * 1.6f));
            abyssVapor.color = new Color(0.20f, 0.15f, 0.40f, vaporAlpha);

            // Falling sparks rushing into the depth
            for (int i = 0; i < FallingSparkCount; i++)
            {
                fallingSparkProgress[i] += dt * fallingSparkSpeed[i];
                if (fallingSparkProgress[i] >= 1f)
                    ResetFallingSpark(i, 0f);

                float t = fallingSparkProgress[i];
                float y = Mathf.Lerp(EmberY, -260f, t * t); // Accelerate downwards with gravity
                float x = Mathf.Sin(time * 3f + i * 2f) * (14f + t * 30f);
                fallingSparkRects[i].anchoredPosition = new Vector2(x, y);

                float fade = (1f - t) * Mathf.Clamp01(t * 8f);
                Color sc = Color.Lerp(new Color(1f, 0.65f, 0.25f), new Color(0.40f, 0.60f, 1.0f), t);
                sc.a = sEmber * fade * 0.85f;
                fallingSparkImages[i].color = sc;
            }
        }

        private void AnimateHazard(float sTop, float sEmber, float dt, float time)
        {
            // Trauma screen shake on ContentRoot
            if (screenTrauma > 0.001f)
            {
                float shakeX = Mathf.Sin(time * 52f) * 16f * screenTrauma;
                float shakeY = Mathf.Cos(time * 44f) * 12f * screenTrauma;
                contentRoot.anchoredPosition = new Vector2(shakeX, shakeY);
                screenTrauma = Mathf.MoveTowards(screenTrauma, 0f, 2.8f * dt);
            }
            else
            {
                contentRoot.anchoredPosition = Vector2.zero;
            }

            // Initial impact hazard flash decay
            if (hazardFlash != null)
            {
                float hfa = hazardFlash.color.a;
                hfa = Mathf.MoveTowards(hfa, 0f, 3.5f * dt);
                hazardFlash.color = new Color(1f, 0.15f, 0.20f, hfa);
            }

            // Drifting, rotating glass shards
            for (int i = 0; i < ShardCount; i++)
            {
                Vector2 p = shardRects[i].anchoredPosition;
                p += shardVelocity[i] * dt * (0.35f + 0.65f * Mathf.Exp(-deathElapsed * 1.5f));
                shardRects[i].anchoredPosition = p;

                shardCurrentRot[i] += shardRotSpeed[i] * dt;
                shardRects[i].localEulerAngles = new Vector3(0f, 0f, shardCurrentRot[i]);

                float dist = p.magnitude;
                float a = sTop * Mathf.Clamp01(1f - dist / 380f) * 0.85f;
                Color shardCol = (i % 2 == 0) ? new Color(0.9f, 0.95f, 1.0f, a) : new Color(1.0f, 0.5f, 0.5f, a);
                shardImages[i].color = shardCol;
            }

            // Jagged electric arcs crackling around the fractured core
            for (int i = 0; i < ArcCount; i++)
            {
                arcTimer[i] -= dt;
                if (arcTimer[i] <= 0f)
                {
                    arcTimer[i] = UnityEngine.Random.Range(0.04f, 0.12f);
                    UpdateArcPosition(i);
                }

                float a = sTop * UnityEngine.Random.Range(0.4f, 1.0f);
                Color arcCol = (i % 2 == 0) ? new Color(1.0f, 0.92f, 0.45f, a) : new Color(1.0f, 0.35f, 0.35f, a);
                arcImages[i].color = arcCol;
            }
        }

        private void UpdateArcPosition(int index)
        {
            float angle = index * (Mathf.PI * 2f / ArcCount) + UnityEngine.Random.Range(-0.4f, 0.4f);
            float dist = UnityEngine.Random.Range(18f, 52f);
            arcRects[index].anchoredPosition = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist + EmberY);
            arcRects[index].localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            float scale = UnityEngine.Random.Range(0.7f, 1.3f);
            arcRects[index].localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateLamp(float dt)
        {
            lampTimer -= dt;
            if (lampTimer <= 0f)
            {
                if (lampBursting)
                {
                    lampBurstSteps--;
                    lampLevel = lampBurstSteps % 2 == 0 ? UnityEngine.Random.Range(0.55f, 0.95f) : UnityEngine.Random.Range(0.05f, 0.25f);
                    lampTimer = UnityEngine.Random.Range(0.03f, 0.08f);
                    if (lampBurstSteps <= 0)
                    {
                        lampBursting = false;
                        lampLevel = UnityEngine.Random.Range(0.75f, 1f);
                        lampTimer = UnityEngine.Random.Range(0.35f, 1.10f);
                    }
                }
                else
                {
                    lampBursting = true;
                    lampBurstSteps = UnityEngine.Random.Range(2, 5);
                    lampTimer = UnityEngine.Random.Range(0.03f, 0.07f);
                }
            }
        }

        // ------------------------------------------------------------------ Resets & Helpers

        private void ResetDust(int i, float phase)
        {
            dustRects[i].anchoredPosition = new Vector2(UnityEngine.Random.Range(-240f, 240f), Mathf.Lerp(184f, -184f, phase));
            dustVelocity[i] = new Vector2(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(-26f, -12f));
            dustPhase[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            dustAlpha[i] = UnityEngine.Random.Range(0.35f, 0.90f);
        }

        private void ResetStreak(int i, float phase)
        {
            streakRects[i].anchoredPosition = new Vector2(UnityEngine.Random.Range(-440f, 440f), Mathf.Lerp(300f, -300f, phase));
            streakSpeed[i] = UnityEngine.Random.Range(420f, 800f);
            streakLength[i] = UnityEngine.Random.Range(35f, 90f);
            streakRects[i].sizeDelta = new Vector2(2f, streakLength[i]);
        }

        private void ResetFallingSpark(int i, float phase)
        {
            fallingSparkProgress[i] = phase;
            fallingSparkSpeed[i] = UnityEngine.Random.Range(0.4f, 0.8f);
        }

        private void ResetShard(int i)
        {
            shardRects[i].anchoredPosition = new Vector2(0f, EmberY);
            float angle = i * (Mathf.PI * 2f / ShardCount) + UnityEngine.Random.Range(-0.25f, 0.25f);
            float spd = UnityEngine.Random.Range(90f, 240f);
            shardVelocity[i] = new Vector2(Mathf.Cos(angle) * spd, Mathf.Sin(angle) * spd);
            shardRotSpeed[i] = UnityEngine.Random.Range(-280f, 280f);
            shardCurrentRot[i] = UnityEngine.Random.Range(0f, 360f);
            float scale = UnityEngine.Random.Range(0.7f, 1.4f);
            shardRects[i].localScale = new Vector3(scale, scale, 1f);
        }

        private void ResetSpark(int i, float phase)
        {
            sparkProgress[i] = phase;
            sparkSpeed[i] = UnityEngine.Random.Range(0.40f, 0.75f);
            sparkDrift[i] = UnityEngine.Random.Range(-12f, 12f);
        }

        // ------------------------------------------------------------------ Input & Navigation

        private void HandleNavigation()
        {
            var k = Keyboard.current;
            var pad = ControllerGlyphService.GetActiveGamepad();

            if (k != null && k.rKey.wasPressedThisFrame)
            {
                Activate(0);
                return;
            }

            bool navDown = (k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
                        || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame || (pad.leftStick.ReadValue().y < -0.5f && Mathf.Abs(pad.leftStick.ReadValue().x) < 0.5f)));
            bool navUp = (k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
                      || (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame || (pad.leftStick.ReadValue().y > 0.5f && Mathf.Abs(pad.leftStick.ReadValue().x) < 0.5f)));

            if (navDown) SetSelectedButton(1);
            else if (navUp) SetSelectedButton(0);

            bool confirm = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm)
            {
                Activate(selectedButtonIndex);
                return;
            }

            bool cancelOrReturn = (k != null && k.escapeKey.wasPressedThisFrame)
                               || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (cancelOrReturn)
            {
                SetSelectedButton(1);
                Activate(1);
                return;
            }
        }

        private void HandlePointer()
        {
            if (!armed) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool moved = (pos - lastMousePosition).sqrMagnitude > 16f;
            if (!moved) return;
            lastMousePosition = pos;

            int hover = -1;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i]?.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(buttons[i].Rect, pos, null))
                    hover = i;
            }

            if (hover >= 0 && hover != selectedButtonIndex)
                SetSelectedButton(hover);
            if (hover >= 0 && mouse.leftButton.wasPressedThisFrame)
            {
                SetSelectedButton(hover);
                Activate(hover);
            }
        }

        public void SetSelectedButton(int index)
        {
            SetSelectedButton(index, silent: false);
        }

        private void SetSelectedButton(int index, bool silent)
        {
            index = Mathf.Clamp(index, 0, buttons.Length - 1);
            if (selectedButtonIndex == index) return;

            selectedButtonIndex = index;
            if (respawnController != null)
            {
                // Pause auto-respawn if the player navigated down to Return to Archive
                respawnController.AutoRespawnEnabled = (selectedButtonIndex == 0);
            }
            if (!silent && SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayUIHover(0.75f);
        }

        private void Activate(int index)
        {
            if (busy) return;
            busy = true;
            buttonPunch = 1f;

            if (SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayUIClick();

            if (index == 0)
            {
                if (respawnController != null)
                    respawnController.Respawn();
                else
                    HandlePlayerRespawned();
            }
            else
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(MainMenuScenePath);
            }
        }

        private void UpdateButton(MenuButton b, bool selected, float stage, float pulse, float dt, float punch)
        {
            if (b?.Rect == null) return;

            b.Blend = Mathf.MoveTowards(b.Blend, selected ? 1f : 0f, 9f * dt);
            float scale = (1f + b.Blend * 0.045f) * (1f - punch * 0.06f);
            b.Rect.localScale = new Vector3(scale, scale, 1f);

            Color restPlate = new Color(0.012f, 0.020f, 0.038f, 0.70f);
            Color restRim = new Color(0.45f, 0.58f, 0.78f, 0.28f);
            Color restText = new Color(0.85f, 0.90f, 0.98f, 0.88f);

            Color pCol = Color.Lerp(restPlate, b.SelectedPlate, b.Blend);
            pCol.a *= stage;
            b.Plate.color = pCol;

            Color rCol = Color.Lerp(restRim, b.SelectedRim, b.Blend);
            if (selected)
                rCol = Color.Lerp(rCol, Color.white, pulse * 0.25f);
            rCol.a *= stage;
            for (int r = 0; r < b.Rim.Length; r++)
                b.Rim[r].color = rCol;

            Color tCol = Color.Lerp(restText, b.SelectedText, b.Blend);
            tCol.a *= stage;
            b.Label.color = tCol;
            b.Label.characterSpacing = Mathf.Lerp(4.5f, 6.5f, b.Blend);

            if (b.Marker != null)
            {
                Color mCol = b.SelectedRim;
                mCol.a = stage * b.Blend * (0.75f + 0.25f * pulse);
                b.Marker.color = mCol;
            }
        }

        private static bool IsConfirmHeld()
        {
            var k = Keyboard.current;
            if (k != null && (k.enterKey.isPressed || k.spaceKey.isPressed || k.rKey.isPressed))
                return true;
            var pad = ControllerGlyphService.GetActiveGamepad();
            if (pad != null && pad.buttonSouth.isPressed)
                return true;
            return false;
        }

        private static float Stage(float fade, float start, float end)
        {
            if (fade <= start) return 0f;
            if (fade >= end) return 1f;
            return Mathf.SmoothStep(0f, 1f, (fade - start) / (end - start));
        }

        // ------------------------------------------------------------------ Procedural Sprites

        private static Sprite GetConeSprite()
        {
            if (coneSprite != null) return coneSprite;
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_LampCone",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < h; y++)
            {
                float v = 1f - (y + 0.5f) / h;
                float halfWidth = 0.20f + 0.80f * v;
                float vertical = Mathf.Pow(1f - v, 1.35f);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;
                    float side = Mathf.Clamp01(1f - Mathf.Abs(u) / halfWidth);
                    float a = Mathf.SmoothStep(0f, 1f, side) * vertical;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            coneSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            coneSprite.hideFlags = HideFlags.HideAndDontSave;
            return coneSprite;
        }

        private static Sprite GetStreakSprite()
        {
            if (streakSprite != null) return streakSprite;
            const int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_Streak",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                float a = Mathf.Sin(v * Mathf.PI);
                a = a * a;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float side = Mathf.Sin(u * Mathf.PI);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * side));
                }
            }
            tex.Apply();
            streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            streakSprite.hideFlags = HideFlags.HideAndDontSave;
            return streakSprite;
        }

        private static Sprite GetVaporSprite()
        {
            if (vaporSprite != null) return vaporSprite;
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_Vapor",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < h; y++)
            {
                float v = 1f - (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float wave = Mathf.Sin((float)x / (w - 1) * Mathf.PI * 4f) * 0.12f;
                    float a = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(v + wave), 1.6f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            vaporSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            vaporSprite.hideFlags = HideFlags.HideAndDontSave;
            return vaporSprite;
        }

        private static Sprite GetShardSprite()
        {
            if (shardSprite != null) return shardSprite;
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_Shard",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            // Sharp asymmetrical polygonal shard
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool inside = (x >= 2 && x <= 14 && y >= 2 && y <= 14) &&
                                  (y <= x + 4) && (y >= x - 6) && (x + y <= 22);
                    bool edge = inside && (y == x + 4 || y == x - 6 || x + y == 22 || x == 2 || y == 2);
                    Color c = inside ? (edge ? Color.white : new Color(0.85f, 0.95f, 1.0f, 0.75f)) : Color.clear;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            shardSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            shardSprite.hideFlags = HideFlags.HideAndDontSave;
            return shardSprite;
        }

        private static Sprite GetArcSprite()
        {
            if (arcSprite != null) return arcSprite;
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_Arc",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, Color.clear);

            // Jagged spark line
            int[] py = { 2, 4, 7, 6, 9, 12, 11, 14 };
            int[] px = { 2, 4, 5, 8, 9, 11, 13, 14 };
            for (int i = 0; i < px.Length; i++)
            {
                tex.SetPixel(px[i], py[i], Color.white);
                if (px[i] + 1 < s) tex.SetPixel(px[i] + 1, py[i], new Color(1f, 1f, 1f, 0.6f));
            }

            tex.Apply();
            arcSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            arcSprite.hideFlags = HideFlags.HideAndDontSave;
            return arcSprite;
        }

        private static Sprite GetTendrilSprite()
        {
            if (tendrilSprite != null) return tendrilSprite;
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_Tendril",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float r = s / 2f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d * d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            tendrilSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            tendrilSprite.hideFlags = HideFlags.HideAndDontSave;
            return tendrilSprite;
        }

        private static void SetCentered(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void SetFullscreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetCentered(go.GetComponent<RectTransform>(), pos, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image CreateFullscreen(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            SetFullscreen(rect);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.clear;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI AddTMPText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetCentered(go.GetComponent<RectTransform>(), position, size);

            var txt = go.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
                txt.font = fontAsset;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.clear;
            txt.raycastTarget = false;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            return txt;
        }
    }
}
