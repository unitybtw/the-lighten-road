using System;
using System.Collections;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.UI
{
    public enum ControllerPromptStyle
    {
        Auto = 0,
        Xbox = 1,
        PlayStation = 2,
        Keyboard = 3
    }

    public enum GlyphAction
    {
        Confirm,
        Cancel,
        Move,
        Jump,
        Sprint,
        Flare,
        Interact,
        Pause,
        NextTab,
        PrevTab
    }

    /// <summary>
    /// Centralized controller glyph & prompt service.
    /// Provides dynamic, high-readability button prompts and symbols for Xbox, PlayStation (DualShock / DualSense),
    /// and PC Keyboard & Mouse. Also manages haptic gamepad rumble feedback and stick deadzone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerGlyphService : MonoBehaviour
    {
        public const string StylePrefKey = "TLR.Menu.ControllerStyle";
        public const string VibrationPrefKey = "TLR.Menu.VibrationEnabled";
        public const string DeadzonePrefKey = "TLR.Menu.StickDeadzone";

        private static ControllerGlyphService instance;
        public static ControllerGlyphService Instance
        {
            get
            {
                if (instance == null) EnsureExists();
                return instance;
            }
        }

        public static event Action OnPromptStyleChanged;

        private static ControllerPromptStyle configuredStyle = ControllerPromptStyle.Auto;
        private static bool isInitialized;
        private static bool lastUsedGamepad;
        private static ControllerPromptStyle lastDetectedPad = ControllerPromptStyle.Xbox;

        private Coroutine activeRumbleRoutine;

        public static ControllerPromptStyle ConfiguredStyle
        {
            get
            {
                EnsureInitialized();
                return configuredStyle;
            }
            set
            {
                EnsureInitialized();
                if (configuredStyle != value)
                {
                    configuredStyle = value;
                    PlayerPrefs.SetInt(StylePrefKey, (int)value);
                    PlayerPrefs.Save();
                    NotifyStyleChanged();
                }
            }
        }

        public static ControllerPromptStyle ActiveStyle
        {
            get
            {
                EnsureInitialized();
                if (configuredStyle != ControllerPromptStyle.Auto)
                {
                    return configuredStyle;
                }

                if (lastUsedGamepad || (Gamepad.current != null && (Keyboard.current == null || !Keyboard.current.anyKey.isPressed)))
                {
                    return DetectGamepadHardware();
                }

                return ControllerPromptStyle.Keyboard;
            }
        }

        public static bool IsGamepadActive
        {
            get
            {
                var style = ActiveStyle;
                return style == ControllerPromptStyle.Xbox || style == ControllerPromptStyle.PlayStation;
            }
        }

        public static bool VibrationEnabled
        {
            get
            {
                EnsureInitialized();
                return PlayerPrefs.GetInt(VibrationPrefKey, 1) == 1;
            }
            set
            {
                EnsureInitialized();
                PlayerPrefs.SetInt(VibrationPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static float StickDeadzone
        {
            get
            {
                EnsureInitialized();
                return PlayerPrefs.GetFloat(DeadzonePrefKey, 0.15f);
            }
            set
            {
                EnsureInitialized();
                PlayerPrefs.SetFloat(DeadzonePrefKey, Mathf.Clamp(value, 0.05f, 0.40f));
                PlayerPrefs.Save();
            }
        }

        public static void EnsureExists()
        {
            if (instance != null) return;
            var existing = FindAnyObjectByType<ControllerGlyphService>();
            if (existing != null)
            {
                instance = existing;
                return;
            }

            var go = new GameObject("[ControllerGlyphService]");
            instance = go.AddComponent<ControllerGlyphService>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
        }

        private static void EnsureInitialized()
        {
            if (isInitialized) return;
            configuredStyle = (ControllerPromptStyle)PlayerPrefs.GetInt(StylePrefKey, 0);
            isInitialized = true;
        }

        public static void NotifyStyleChanged()
        {
            OnPromptStyleChanged?.Invoke();
        }

        private PlayerMotor2D boundPlayer;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            EnsureInitialized();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            LanternController.AnyFlareChanged -= HandleAnyFlareChanged;
            LanternController.AnyFlareChanged += HandleAnyFlareChanged;
        }

        private void OnDisable()
        {
            LanternController.AnyFlareChanged -= HandleAnyFlareChanged;
            UnbindPlayer();
            HaltRumble();
        }

        private void Update()
        {
            CheckActiveInputDevice();
            CheckPlayerBinding();
        }

        private void CheckPlayerBinding()
        {
            var currentMotor = PlayerMotor2D.ActiveInstance;
            if (currentMotor != boundPlayer)
            {
                UnbindPlayer();
                if (currentMotor != null)
                {
                    boundPlayer = currentMotor;
                    boundPlayer.OnJumped += HandlePlayerJumped;
                    boundPlayer.OnLanded += HandlePlayerLanded;
                }
            }
        }

        private void UnbindPlayer()
        {
            if (boundPlayer != null)
            {
                boundPlayer.OnJumped -= HandlePlayerJumped;
                boundPlayer.OnLanded -= HandlePlayerLanded;
                boundPlayer = null;
            }
        }

        private void HandlePlayerJumped()
        {
            Rumble(0.20f, 0.35f, 0.08f);
        }

        private void HandlePlayerLanded(float velocity)
        {
            if (Mathf.Abs(velocity) > 8f)
            {
                Rumble(0.40f, 0.20f, 0.12f);
            }
        }

        private void HandleAnyFlareChanged(LanternController lantern, bool isFlared)
        {
            if (isFlared)
            {
                Rumble(0.35f, 0.55f, 0.16f);
            }
        }

        private void CheckActiveInputDevice()
        {
            var gp = Gamepad.current;
            if (gp != null)
            {
                bool padActivity = gp.buttonSouth.wasPressedThisFrame ||
                                   gp.buttonEast.wasPressedThisFrame ||
                                   gp.buttonWest.wasPressedThisFrame ||
                                   gp.buttonNorth.wasPressedThisFrame ||
                                   gp.startButton.wasPressedThisFrame ||
                                   gp.selectButton.wasPressedThisFrame ||
                                   gp.leftShoulder.wasPressedThisFrame ||
                                   gp.rightShoulder.wasPressedThisFrame ||
                                   gp.leftTrigger.wasPressedThisFrame ||
                                   gp.rightTrigger.wasPressedThisFrame ||
                                   gp.dpad.left.wasPressedThisFrame ||
                                   gp.dpad.right.wasPressedThisFrame ||
                                   gp.dpad.up.wasPressedThisFrame ||
                                   gp.dpad.down.wasPressedThisFrame ||
                                   gp.leftStick.ReadValue().sqrMagnitude > 0.08f;

                if (padActivity)
                {
                    var detected = DetectGamepadHardware();
                    if (!lastUsedGamepad || detected != lastDetectedPad)
                    {
                        lastUsedGamepad = true;
                        lastDetectedPad = detected;
                        if (configuredStyle == ControllerPromptStyle.Auto)
                        {
                            NotifyStyleChanged();
                        }
                    }
                    return;
                }
            }

            var kb = Keyboard.current;
            var ms = Mouse.current;
            bool pcActivity = (kb != null && kb.anyKey.wasPressedThisFrame) ||
                              (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame));

            if (pcActivity)
            {
                if (lastUsedGamepad)
                {
                    lastUsedGamepad = false;
                    if (configuredStyle == ControllerPromptStyle.Auto)
                    {
                        NotifyStyleChanged();
                    }
                }
            }
        }

        public static ControllerPromptStyle DetectGamepadHardware()
        {
            var pad = Gamepad.current;
            if (pad == null) return ControllerPromptStyle.Xbox;

            string info = (pad.description.manufacturer + " " + pad.description.product + " " + pad.name + " " + pad.displayName).ToLowerInvariant();
            if (info.Contains("sony") || info.Contains("dualshock") || info.Contains("dualsense") ||
                info.Contains("playstation") || info.Contains("ps4") || info.Contains("ps5") ||
                info.Contains("wireless controller"))
            {
                return ControllerPromptStyle.PlayStation;
            }

            return ControllerPromptStyle.Xbox;
        }

        public static string GetActionPrompt(GlyphAction action, ControllerPromptStyle? overrideStyle = null)
        {
            var style = overrideStyle ?? ActiveStyle;
            switch (style)
            {
                case ControllerPromptStyle.PlayStation:
                    switch (action)
                    {
                        case GlyphAction.Confirm: return "CROSS  CONFIRM";
                        case GlyphAction.Cancel: return "CIRCLE  BACK";
                        case GlyphAction.Move: return "LEFT STICK / D-PAD";
                        case GlyphAction.Jump: return "CROSS";
                        case GlyphAction.Sprint: return "R2 / L3";
                        case GlyphAction.Flare: return "R2 / SQUARE";
                        case GlyphAction.Interact: return "TRIANGLE";
                        case GlyphAction.Pause: return "OPTIONS";
                        case GlyphAction.NextTab: return "R1";
                        case GlyphAction.PrevTab: return "L1";
                        default: return "";
                    }

                case ControllerPromptStyle.Xbox:
                    switch (action)
                    {
                        case GlyphAction.Confirm: return "A  CONFIRM";
                        case GlyphAction.Cancel: return "B  BACK";
                        case GlyphAction.Move: return "LEFT STICK / D-PAD";
                        case GlyphAction.Jump: return "A";
                        case GlyphAction.Sprint: return "RT / LS";
                        case GlyphAction.Flare: return "RT / X";
                        case GlyphAction.Interact: return "Y / B";
                        case GlyphAction.Pause: return "MENU";
                        case GlyphAction.NextTab: return "RB";
                        case GlyphAction.PrevTab: return "LB";
                        default: return "";
                    }

                case ControllerPromptStyle.Keyboard:
                default:
                    switch (action)
                    {
                        case GlyphAction.Confirm: return "ENTER  CONFIRM";
                        case GlyphAction.Cancel: return "ESC  BACK";
                        case GlyphAction.Move: return "A / D  or  ARROWS";
                        case GlyphAction.Jump: return "SPACE";
                        case GlyphAction.Sprint: return "L-SHIFT";
                        case GlyphAction.Flare: return "F / L-CLICK";
                        case GlyphAction.Interact: return "E (HOLD)";
                        case GlyphAction.Pause: return "ESC";
                        case GlyphAction.NextTab: return "E";
                        case GlyphAction.PrevTab: return "Q";
                        default: return "";
                    }
            }
        }

        public static string GetConfirmPrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Confirm, overrideStyle);
        public static string GetCancelPrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Cancel, overrideStyle);
        public static string GetJumpPrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Jump, overrideStyle);
        public static string GetSprintPrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Sprint, overrideStyle);
        public static string GetFlarePrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Flare, overrideStyle);
        public static string GetInteractPrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Interact, overrideStyle);
        public static string GetMovePrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Move, overrideStyle);
        public static string GetPausePrompt(ControllerPromptStyle? overrideStyle = null) => GetActionPrompt(GlyphAction.Pause, overrideStyle);
        public static string GetTabCyclePrompt(ControllerPromptStyle? overrideStyle = null)
        {
            var style = overrideStyle ?? ActiveStyle;
            if (style == ControllerPromptStyle.PlayStation) return "L1 / R1";
            if (style == ControllerPromptStyle.Xbox) return "LB / RB";
            return "Q / E";
        }

        public static string GetControllerName(ControllerPromptStyle? overrideStyle = null)
        {
            var style = overrideStyle ?? ActiveStyle;
            switch (style)
            {
                case ControllerPromptStyle.PlayStation: return "PlayStation (DualSense / DualShock)";
                case ControllerPromptStyle.Xbox: return "Xbox Wireless Controller";
                case ControllerPromptStyle.Keyboard: return "Keyboard & Mouse";
                default: return "Auto-Detect";
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> spriteCache = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Retrieves the authentic high-resolution button glyph sprite (Zacksly pack) for the active or requested controller style.
        /// </summary>
        public static Sprite GetGlyphSprite(string glyphName, ControllerPromptStyle? overrideStyle = null)
        {
            var style = overrideStyle ?? ActiveStyle;
            if (style == ControllerPromptStyle.Keyboard) return null;

            string folder = style == ControllerPromptStyle.PlayStation ? "PlayStation" : "Xbox";
            string path = $"UI/ControllerGlyphs/{folder}/{glyphName}";

            if (spriteCache.TryGetValue(path, out var cached))
                return cached;

            var loaded = Resources.Load<Sprite>(path);
            spriteCache[path] = loaded;
            return loaded;
        }

        /// <summary>
        /// Retrieves the primary action icon sprite for the current controller device.
        /// </summary>
        public static Sprite GetActionSprite(GlyphAction action, ControllerPromptStyle? overrideStyle = null)
        {
            var style = overrideStyle ?? ActiveStyle;
            if (style == ControllerPromptStyle.Keyboard) return null;

            if (style == ControllerPromptStyle.PlayStation)
            {
                switch (action)
                {
                    case GlyphAction.Confirm:
                    case GlyphAction.Jump: return GetGlyphSprite("Cross", style);
                    case GlyphAction.Cancel: return GetGlyphSprite("Circle", style);
                    case GlyphAction.Move: return GetGlyphSprite("LeftStick", style);
                    case GlyphAction.Sprint: return GetGlyphSprite("R2", style);
                    case GlyphAction.Flare: return GetGlyphSprite("Square", style);
                    case GlyphAction.Interact: return GetGlyphSprite("Triangle", style);
                    case GlyphAction.Pause: return GetGlyphSprite("Options", style);
                    case GlyphAction.NextTab: return GetGlyphSprite("R1", style);
                    case GlyphAction.PrevTab: return GetGlyphSprite("L1", style);
                    default: return null;
                }
            }
            else
            {
                switch (action)
                {
                    case GlyphAction.Confirm:
                    case GlyphAction.Jump: return GetGlyphSprite("A", style);
                    case GlyphAction.Cancel: return GetGlyphSprite("B", style);
                    case GlyphAction.Move: return GetGlyphSprite("LeftStick", style);
                    case GlyphAction.Sprint: return GetGlyphSprite("RT", style);
                    case GlyphAction.Flare: return GetGlyphSprite("X", style);
                    case GlyphAction.Interact: return GetGlyphSprite("Y", style);
                    case GlyphAction.Pause: return GetGlyphSprite("Menu", style);
                    case GlyphAction.NextTab: return GetGlyphSprite("RB", style);
                    case GlyphAction.PrevTab: return GetGlyphSprite("LB", style);
                    default: return null;
                }
            }
        }

        public static void Rumble(float lowFrequency, float highFrequency, float duration = 0.15f)
        {
            if (!VibrationEnabled) return;
            EnsureExists();
            if (instance != null)
            {
                instance.TriggerRumble(lowFrequency, highFrequency, duration);
            }
        }

        public static void StopRumble()
        {
            if (instance != null)
            {
                instance.HaltRumble();
            }
        }

        private void TriggerRumble(float lowFreq, float highFreq, float duration)
        {
            if (Gamepad.current == null) return;
            if (activeRumbleRoutine != null)
            {
                StopCoroutine(activeRumbleRoutine);
                activeRumbleRoutine = null;
            }
            if (isActiveAndEnabled)
            {
                activeRumbleRoutine = StartCoroutine(RumbleRoutine(lowFreq, highFreq, duration));
            }
        }

        private void HaltRumble()
        {
            if (activeRumbleRoutine != null)
            {
                StopCoroutine(activeRumbleRoutine);
                activeRumbleRoutine = null;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                try
                {
                    pad.SetMotorSpeeds(0f, 0f);
                }
                catch { }
            }
        }

        private IEnumerator RumbleRoutine(float lowFreq, float highFreq, float duration)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                try
                {
                    pad.SetMotorSpeeds(Mathf.Clamp01(lowFreq), Mathf.Clamp01(highFreq));
                }
                catch { }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (pad != null)
            {
                try
                {
                    pad.SetMotorSpeeds(0f, 0f);
                }
                catch { }
            }
            activeRumbleRoutine = null;
        }

        private void OnDestroy()
        {
            HaltRumble();
            if (instance == this) instance = null;
        }
    }
}
