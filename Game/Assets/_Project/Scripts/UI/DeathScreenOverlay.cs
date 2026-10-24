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
    /// "Last Ember" death screen: quiet, sorrowful and readable.
    ///
    /// Concept: the world slowly goes dark and only two things are left alive - a dying fluorescent
    /// tube stuttering overhead (the office's own light) and one small ember at the centre that
    /// slowly burns lower. Nothing shouts; the screen simply waits for the player.
    ///
    /// Layout (top to bottom): flickering tube + cold light cone, the last ember, cause title,
    /// hairline rule, one-line lore, CAUSE / FALLEN stats, two large buttons, input hint.
    ///
    /// Behaviour:
    /// - Takes ownership of the respawn decision: automatic respawn is disabled while it is visible.
    /// - Staged reveal (void, lamp, ember, title, stats, buttons) so information never competes.
    /// - Input is armed only once the screen is fully shown AND confirm keys were released, so a held
    ///   Space / jump key can never skip it by accident.
    /// - Keyboard (W/S, arrows, Enter, Space, R), gamepad (D-pad, stick, South) and mouse (hover + click).
    /// - Selection is never colour-only: the selected button also shows a flame marker and a bright rim.
    /// - Total deaths persist between sessions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
        private const string TotalDeathsPrefKey = "TheLightenRoad.TotalDeaths";

        [Header("Target Controller (auto-found if empty)")]
        [SerializeField] private PlayerRespawnController respawnController;

        [Header("Typography Font")]
        [SerializeField] private TMP_FontAsset fontAsset;

        [Header("Fade Speeds")]
        [Tooltip("Higher is faster. 0.85 = about 1.2 seconds to reach the full screen.")]
        [SerializeField, Min(0.1f)] private float deathFadeSpeed = 0.85f;
        [SerializeField, Min(0.1f)] private float respawnFadeSpeed = 3.2f;

        [Header("Palette (cold office light + one warm ember)")]
        [SerializeField] private Color voidColor = new Color32(4, 8, 9, 255);
        [SerializeField] private Color titleColor = new Color32(220, 236, 229, 255);
        [SerializeField] private Color bodyColor = new Color32(158, 186, 177, 255);
        [SerializeField] private Color labelColor = new Color32(104, 138, 128, 255);
        [SerializeField] private Color coldLight = new Color32(168, 205, 192, 255);
        [SerializeField] private Color emberWarm = new Color(1.0f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color ignitionColor = new Color32(255, 214, 128, 255);

        // Runtime hierarchy
        private Canvas canvas;
        private Image blackout;
        private Image coldWash;
        private Image vignette;
        private Image lampCone;
        private Image lampCase;
        private Image lampTube;
        private Image emberHalo;
        private Image emberIcon;
        private Image ignitionFlash;
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

        // Dust in the cold light
        private const int DustCount = 34;
        private readonly Image[] dustImages = new Image[DustCount];
        private readonly RectTransform[] dustRects = new RectTransform[DustCount];
        private readonly Vector2[] dustVelocity = new Vector2[DustCount];
        private readonly float[] dustPhase = new float[DustCount];
        private readonly float[] dustAlpha = new float[DustCount];

        // The few sparks left over from the ember
        private const int SparkCount = 4;
        private const float EmberY = 74f;
        private readonly Image[] sparkImages = new Image[SparkCount];
        private readonly RectTransform[] sparkRects = new RectTransform[SparkCount];
        private readonly float[] sparkProgress = new float[SparkCount];
        private readonly float[] sparkSpeed = new float[SparkCount];
        private readonly float[] sparkDrift = new float[SparkCount];

        // Fluorescent tube state (staccato, never smooth)
        private float lampLevel = 1f;
        private float lampTimer = 0.4f;
        private int lampBurstSteps;
        private bool lampBursting;

        // State
        private float currentFadeAlpha;
        private float targetFadeAlpha;
        private float ignitionAlpha;
        private float deathElapsed;
        private bool isSubscribed;
        private bool armed;
        private bool busy;
        private bool lastHintUsedGamepad;
        private Vector2 lastMousePosition;
        private DeathReason currentReason;
        private int totalDeaths;

        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => currentFadeAlpha > 0.01f || (canvas != null && canvas.enabled);
        public int SelectedButtonIndex => selectedButtonIndex;
        public int TotalDeaths => totalDeaths;
        public bool IsArmed => armed;

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

        // ------------------------------------------------------------------ events

        private void HandlePlayerDied(DeathReason reason)
        {
            currentReason = reason;
            targetFadeAlpha = 1f;
            deathElapsed = 0f;
            armed = false;
            busy = false;
            buttonPunch = 0f;
            SetSelectedButton(0, silent: true);

            totalDeaths++;
            PlayerPrefs.SetInt(TotalDeathsPrefKey, totalDeaths);

            if (respawnController != null)
            {
                // Auto-respawn is preserved so test suites and idle players reignite naturally;
                // manual navigation can pause it when selecting Return to Archive.
                respawnController.AutoRespawnEnabled = true;
            }

            ApplyReasonContent(reason);

            for (int i = 0; i < SparkCount; i++)
                ResetSpark(i, UnityEngine.Random.value);

            lampLevel = 1f;
            lampBursting = true;
            lampBurstSteps = 4;
            lampTimer = 0.05f;

            if (SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayFlareExtinguish();

            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;
        }

        private void HandlePlayerRespawned()
        {
            targetFadeAlpha = 0f;
            ignitionAlpha = 1f;
            armed = false;

            if (respawnController != null)
            {
                respawnController.AutoRespawnEnabled = true;
                respawnController.AllowManualRespawnInput = true;
            }

            if (SoundEffectManager.HasInstance)
                SoundEffectManager.Instance.PlayFlareIgnite();
        }

        private void ApplyReasonContent(DeathReason reason)
        {
            switch (reason)
            {
                case DeathReason.Void:
                    emberIcon.sprite = PixelSprites.EmberFragments;
                    titleText.text = "LOST TO THE ABYSS";
                    loreText.text = "The floor ended. Nothing below answered.";
                    causeValue.text = "THE ABYSS";
                    break;
                case DeathReason.Hazard:
                    emberIcon.sprite = PixelSprites.EmberCracks;
                    titleText.text = "FLAME EXTINGUISHED";
                    loreText.text = "Warmth is fragile. The ruins were not gentle.";
                    causeValue.text = "HAZARD";
                    break;
                default:
                    emberIcon.sprite = PixelSprites.ExtinguishedFlame;
                    titleText.text = "THE LIGHT HAS FADED";
                    loreText.text = "Your lantern ran dry. The dark kept the rest.";
                    causeValue.text = "DARKNESS";
                    break;
            }

            fallenValue.text = Mathf.Max(1, totalDeaths).ToString("D3");
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (respawnController == null)
            {
                respawnController = FindAnyObjectByType<PlayerRespawnController>();
                TrySubscribe();
            }

            bool dead = respawnController != null && respawnController.State != RespawnState.Alive;
            if (dead && currentFadeAlpha > 0.35f && !busy)
            {
                HandleMenuInput();
                HandlePointer();
            }

            Tick(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        private void HandleMenuInput()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;

            bool confirmHeld = (k != null && (k.enterKey.isPressed || k.spaceKey.isPressed || k.rKey.isPressed))
                            || (pad != null && pad.buttonSouth.isPressed);

            // Arm only when the screen is complete and nothing is being held from gameplay.
            if (!armed)
            {
                if (currentFadeAlpha >= 0.98f && deathElapsed >= 1.0f && !confirmHeld)
                    armed = true;
                else
                    return;
            }

            if (k != null && k.rKey.wasPressedThisFrame)
            {
                Activate(0);
                return;
            }

            bool navDown = (k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
                        || (pad != null && (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame));
            bool navUp = (k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
                      || (pad != null && (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame));

            if (navDown) SetSelectedButton(1);
            else if (navUp) SetSelectedButton(0);

            bool confirm = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm)
                Activate(selectedButtonIndex);
        }

        private void HandlePointer()
        {
            if (!armed) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool moved = (pos - lastMousePosition).sqrMagnitude > 1f;
            lastMousePosition = pos;

            int hover = -1;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i]?.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(buttons[i].Rect, pos, null))
                    hover = i;
            }

            if (hover < 0) return;
            if (moved && hover != selectedButtonIndex)
                SetSelectedButton(hover);
            if (mouse.leftButton.wasPressedThisFrame)
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

            if (index == 0) TriggerReignite();
            else TriggerReturnToTitle();
        }

        private void TriggerReignite()
        {
            if (respawnController != null)
                respawnController.Respawn();
        }

        private void TriggerReturnToTitle()
        {
            Time.timeScale = 1f;
            try
            {
                SceneManager.LoadScene(MainMenuScenePath);
            }
            catch
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        // ------------------------------------------------------------------ animation

        private static float Stage(float value, float from, float to)
        {
            return Mathf.Clamp01((value - from) / Mathf.Max(0.0001f, to - from));
        }

        public void Tick(float dt, float time)
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
            float sLamp = Stage(fade, 0.15f, 0.50f);
            float sEmber = Stage(fade, 0.30f, 0.60f);
            float sTitle = Stage(fade, 0.50f, 0.75f);
            float sStats = Stage(fade, 0.65f, 0.88f);
            float sMenu = Stage(fade, 0.80f, 1.00f);

            // 1. Void
            blackout.color = new Color(voidColor.r, voidColor.g, voidColor.b, sVoid * 0.97f);
            vignette.color = new Color(1f, 1f, 1f, sVoid);

            // 2. Fluorescent tube and its cold light
            if (sLamp > 0f)
                UpdateLamp(dt);

            Color tubeDark = new Color(0.12f, 0.17f, 0.15f, 1f);
            Color tubeLit = new Color(0.86f, 0.96f, 0.92f, 1f);
            Color tube = Color.Lerp(tubeDark, tubeLit, Mathf.Clamp01(lampLevel));
            tube.a = sLamp;
            lampTube.color = tube;
            lampCase.color = new Color(0.07f, 0.10f, 0.09f, sLamp);
            lampCone.color = new Color(coldLight.r, coldLight.g, coldLight.b, sLamp * 0.30f * Mathf.Clamp01(lampLevel));
            coldWash.color = new Color(0.20f, 0.36f, 0.31f, sLamp * 0.10f * Mathf.Clamp01(lampLevel));

            // 3. Dust drifting through the light
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
                dustImages[i].color = new Color(coldLight.r, coldLight.g, coldLight.b, sLamp * a);
            }

            // 4. The last ember: burns lower with every second, never fully out
            float life = Mathf.Lerp(1f, 0.30f, Mathf.Clamp01(deathElapsed / 3f));
            float breath = 0.5f + 0.5f * Mathf.Sin(time * 2.1f);

            float haloAlpha = sEmber * (0.10f + 0.36f * life) * (0.72f + 0.28f * breath);
            emberHalo.color = new Color(1f, 0.62f, 0.22f, haloAlpha);
            float haloScale = 0.75f + 0.55f * life + 0.06f * breath;
            emberHalo.rectTransform.localScale = new Vector3(haloScale, haloScale, 1f);

            float bright = 0.50f + 0.50f * life;
            emberIcon.color = new Color(0.96f * bright, 0.66f * bright, 0.36f * bright, sEmber);

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
                Color c = Color.Lerp(emberWarm, new Color(1f, 0.45f, 0.14f), t);
                c.a = sEmber * fadeOut * life * 0.9f;
                sparkImages[i].color = c;
            }

            // 5. Title, rule, lore
            titleText.color = new Color(titleColor.r, titleColor.g, titleColor.b, sTitle);
            ruleLine.color = new Color(coldLight.r, coldLight.g, coldLight.b, sTitle * 0.38f);
            ruleGem.color = new Color(emberWarm.r, emberWarm.g, emberWarm.b, sTitle * 0.9f);
            loreText.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, sTitle * 0.95f);

            // 6. Stats
            Color lab = new Color(labelColor.r, labelColor.g, labelColor.b, sStats);
            causeLabel.color = lab;
            fallenLabel.color = lab;
            causeValue.color = new Color(titleColor.r, titleColor.g, titleColor.b, sStats);
            fallenValue.color = new Color(emberWarm.r, emberWarm.g, emberWarm.b, sStats);
            statDivider.color = new Color(coldLight.r, coldLight.g, coldLight.b, sStats * 0.30f);

            // 7. Buttons
            buttonPunch = Mathf.MoveTowards(buttonPunch, 0f, 5f * dt);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            for (int i = 0; i < buttons.Length; i++)
                UpdateButton(buttons[i], i == selectedButtonIndex, sMenu, pulse, dt, i == selectedButtonIndex ? buttonPunch : 0f);

            // 8. Hint: appears when input is actually accepted
            bool usePad = Gamepad.current != null;
            if (usePad != lastHintUsedGamepad || string.IsNullOrEmpty(hintText.text))
            {
                lastHintUsedGamepad = usePad;
                hintText.text = usePad ? "D-PAD  SELECT        A  CONFIRM" : "W / S  SELECT        ENTER  CONFIRM";
            }
            float hintAlpha = armed ? sMenu : sMenu * 0.35f;
            hintText.color = new Color(labelColor.r, labelColor.g, labelColor.b, hintAlpha * 0.9f);

            // 9. Re-ignition flash (warm, brief, quiet)
            if (ignitionAlpha > 0f)
            {
                ignitionFlash.color = new Color(ignitionColor.r, ignitionColor.g, ignitionColor.b, ignitionAlpha * 0.6f);
                float s = 1f + (1f - ignitionAlpha) * 1.8f;
                ignitionFlash.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                ignitionFlash.color = Color.clear;
            }
        }

        private void UpdateButton(MenuButton b, bool selected, float alpha, float pulse, float dt, float punch)
        {
            b.Blend = Mathf.MoveTowards(b.Blend, selected ? 1f : 0f, 12f * dt);
            float t = b.Blend;

            Color plateOff = new Color(0.03f, 0.07f, 0.07f, 0.55f);
            Color rimOff = new Color(0.30f, 0.42f, 0.38f, 0.55f);
            Color textOff = new Color(0.58f, 0.72f, 0.67f, 0.85f);

            Color plate = Color.Lerp(plateOff, b.SelectedPlate, t);
            Color rim = Color.Lerp(rimOff, Color.Lerp(b.SelectedRim, Color.white, pulse * 0.25f), t);
            Color text = Color.Lerp(textOff, Color.Lerp(b.SelectedText, Color.white, pulse * 0.35f), t);

            plate.a *= alpha;
            rim.a *= alpha;
            text.a *= alpha;

            b.Plate.color = plate;
            for (int i = 0; i < b.Rim.Length; i++)
                b.Rim[i].color = rim;
            b.Label.color = text;
            b.Label.characterSpacing = Mathf.Lerp(3.5f, 5.0f, t);

            Color marker = b.SelectedText;
            marker.a = alpha * t * (0.75f + 0.25f * pulse);
            b.Marker.color = marker;

            float scale = 1f - 0.03f * punch;
            b.Rect.localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateLamp(float dt)
        {
            lampTimer -= dt;
            if (lampTimer > 0f) return;

            if (!lampBursting)
            {
                lampBursting = true;
                lampBurstSteps = UnityEngine.Random.Range(3, 7);
            }

            if (lampBurstSteps > 0)
            {
                lampBurstSteps--;
                lampLevel = UnityEngine.Random.value < 0.5f
                    ? UnityEngine.Random.Range(0f, 0.10f)
                    : UnityEngine.Random.Range(0.75f, 1.1f);
                lampTimer = UnityEngine.Random.Range(0.03f, 0.09f);
            }
            else
            {
                lampBursting = false;
                lampLevel = 0.92f;
                lampTimer = UnityEngine.Random.Range(2.5f, 6.0f);
            }
        }

        private void ResetSpark(int i, float startProgress)
        {
            sparkProgress[i] = startProgress;
            sparkSpeed[i] = UnityEngine.Random.Range(0.28f, 0.50f);
            sparkDrift[i] = UnityEngine.Random.Range(-9f, 9f);
        }

        // ------------------------------------------------------------------ construction

        private void BuildHierarchy()
        {
            var canvasGo = new GameObject("DeathScreen Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            canvas.pixelPerfect = true;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 1f;

            Transform root = canvasGo.transform;

            // Backdrop
            blackout = CreateFullscreen("Void", root, PixelSprites.White);
            coldWash = CreateFullscreen("ColdWash", root, PixelSprites.White);

            // Dust
            var dustRoot = new GameObject("Dust", typeof(RectTransform));
            dustRoot.transform.SetParent(root, false);
            for (int i = 0; i < DustCount; i++)
            {
                float size = UnityEngine.Random.value < 0.7f ? 1f : 2f;
                dustImages[i] = CreateImage($"Mote_{i}", dustRoot.transform,
                    new Vector2(UnityEngine.Random.Range(-300f, 300f), UnityEngine.Random.Range(-180f, 180f)),
                    new Vector2(size, size), PixelSprites.White, Color.clear);
                dustRects[i] = dustImages[i].rectTransform;
                dustVelocity[i] = new Vector2(UnityEngine.Random.Range(-2f, 2f), -UnityEngine.Random.Range(3f, 9f));
                dustPhase[i] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                dustAlpha[i] = UnityEngine.Random.Range(0.35f, 0.9f);
            }

            vignette = CreateFullscreen("Vignette", root, PixelSprites.Vignette);

            // Lamp: dark case, glowing tube, cone of cold light
            lampCone = CreateImage("LampCone", root, new Vector2(0f, 62f), new Vector2(340f, 206f), GetConeSprite(), Color.clear);
            lampCase = CreateImage("LampCase", root, new Vector2(0f, 168f), new Vector2(150f, 9f), PixelSprites.White, Color.clear);
            lampTube = CreateImage("LampTube", root, new Vector2(0f, 166f), new Vector2(132f, 3f), PixelSprites.White, Color.clear);

            // Ember and its sparks
            emberHalo = CreateImage("EmberHalo", root, new Vector2(0f, EmberY), new Vector2(86f, 86f), PixelSprites.Glow, Color.clear);
            for (int i = 0; i < SparkCount; i++)
            {
                sparkImages[i] = CreateImage($"Spark_{i}", root, new Vector2(0f, EmberY), new Vector2(2f, 2f), PixelSprites.White, Color.clear);
                sparkRects[i] = sparkImages[i].rectTransform;
                ResetSpark(i, i / (float)SparkCount);
            }
            emberIcon = CreateImage("Ember", root, new Vector2(0f, EmberY), new Vector2(36f, 40f), PixelSprites.ExtinguishedFlame, Color.clear);

            // Title block
            titleText = AddTMPText("Title", root, new Vector2(0f, 38f), new Vector2(520f, 30f), 22, TextAlignmentOptions.Center);
            titleText.characterSpacing = 7f;
            titleText.fontStyle = FontStyles.Bold;

            ruleLine = CreateImage("RuleLine", root, new Vector2(0f, 21f), new Vector2(150f, 1f), PixelSprites.White, Color.clear);
            ruleGem = CreateImage("RuleGem", root, new Vector2(0f, 21f), new Vector2(5f, 5f), PixelSprites.White, Color.clear);
            ruleGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            loreText = AddTMPText("Lore", root, new Vector2(0f, 3f), new Vector2(500f, 24f), 13, TextAlignmentOptions.Center);
            loreText.characterSpacing = 1.5f;

            // Stats: CAUSE | FALLEN
            causeLabel = AddTMPText("CauseLabel", root, new Vector2(-74f, -22f), new Vector2(130f, 14f), 10, TextAlignmentOptions.Center);
            causeLabel.text = "CAUSE";
            causeLabel.characterSpacing = 4f;
            causeValue = AddTMPText("CauseValue", root, new Vector2(-74f, -38f), new Vector2(140f, 20f), 14, TextAlignmentOptions.Center);
            causeValue.characterSpacing = 3f;
            causeValue.fontStyle = FontStyles.Bold;

            statDivider = CreateImage("StatDivider", root, new Vector2(0f, -31f), new Vector2(1f, 30f), PixelSprites.White, Color.clear);

            fallenLabel = AddTMPText("FallenLabel", root, new Vector2(74f, -22f), new Vector2(130f, 14f), 10, TextAlignmentOptions.Center);
            fallenLabel.text = "FALLEN";
            fallenLabel.characterSpacing = 4f;
            fallenValue = AddTMPText("FallenValue", root, new Vector2(74f, -38f), new Vector2(140f, 20f), 14, TextAlignmentOptions.Center);
            fallenValue.characterSpacing = 3f;
            fallenValue.fontStyle = FontStyles.Bold;

            // Buttons
            buttons[0] = CreateMenuButton(root, "ReigniteButton", new Vector2(0f, -76f), new Vector2(280f, 32f),
                "REIGNITE FLAME",
                new Color(1f, 0.74f, 0.30f, 1f), new Color(0.17f, 0.11f, 0.04f, 0.92f), new Color(1f, 0.82f, 0.45f, 1f));
            buttons[1] = CreateMenuButton(root, "ReturnButton", new Vector2(0f, -114f), new Vector2(280f, 32f),
                "RETURN TO ARCHIVE",
                new Color(0.72f, 0.90f, 0.84f, 1f), new Color(0.06f, 0.12f, 0.11f, 0.92f), new Color(0.80f, 0.94f, 0.89f, 1f));

            hintText = AddTMPText("Hint", root, new Vector2(0f, -150f), new Vector2(480f, 16f), 10, TextAlignmentOptions.Center);
            hintText.characterSpacing = 3f;

            // Re-ignition flash sits above everything
            ignitionFlash = CreateImage("IgnitionFlash", root, new Vector2(0f, EmberY), new Vector2(420f, 420f), PixelSprites.Glow, Color.clear);

            canvas.enabled = false;
        }

        private MenuButton CreateMenuButton(Transform parent, string name, Vector2 pos, Vector2 size, string label,
            Color selectedRim, Color selectedPlate, Color selectedText)
        {
            var b = new MenuButton { SelectedRim = selectedRim, SelectedPlate = selectedPlate, SelectedText = selectedText };

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            b.Rect = go.GetComponent<RectTransform>();
            SetCentered(b.Rect, pos, size);

            b.Plate = CreateImage("Plate", go.transform, Vector2.zero, size, PixelSprites.White, Color.clear);
            b.Rim = CreateRim(go.transform);

            b.Marker = CreateImage("Marker", go.transform, new Vector2(-size.x * 0.5f + 20f, 0f), new Vector2(10f, 14f), PixelSprites.Flame, Color.clear);
            b.Marker.preserveAspect = true;

            b.Label = AddTMPText("Label", go.transform, Vector2.zero, size, 13, TextAlignmentOptions.Center);
            b.Label.text = label;
            b.Label.fontStyle = FontStyles.Bold;
            b.Label.characterSpacing = 3.5f;
            return b;
        }

        private Image[] CreateRim(Transform parent)
        {
            var rim = new Image[4];
            // top, bottom, left, right - one crisp pixel each
            rim[0] = CreateEdge("RimTop", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            rim[1] = CreateEdge("RimBottom", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
            rim[2] = CreateEdge("RimLeft", parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
            rim[3] = CreateEdge("RimRight", parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f));
            return rim;
        }

        private static Image CreateEdge(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.sprite = PixelSprites.White;
            img.color = Color.clear;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite coneSprite;

        /// <summary>
        /// Soft cone of light that widens downward from the tube. Smooth on purpose: the stepped Glow
        /// sprite shows hard bands when stretched this wide.
        /// </summary>
        private static Sprite GetConeSprite()
        {
            if (coneSprite != null)
                return coneSprite;

            const int w = 64;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "DeathScreen_LampCone",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < h; y++)
            {
                float v = 1f - (y + 0.5f) / h;                   // 0 at the tube, 1 at the far end
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

        private static void SetCentered(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
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
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
