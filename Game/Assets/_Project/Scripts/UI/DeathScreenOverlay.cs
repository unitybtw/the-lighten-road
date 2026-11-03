using System;
using TheLightenRoad.Audio;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLightenRoad.UI
{
    /// <summary>Lantern-lit recovery screen. Owns the recovery decision until the player chooses.</summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenOverlay : MonoBehaviour
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/Frontend/MainMenu.unity";
        private const string TotalDeathsPrefKey = "TheLightenRoad.TotalDeaths";
        private static DeathScreenOverlay activeScreen;
        public static bool IsAnyVisible => activeScreen != null && activeScreen.IsVisible;

        [SerializeField] private PlayerRespawnController respawnController;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField, Min(.1f)] private float deathFadeSpeed = .85f;
        [SerializeField, Min(.1f)] private float respawnFadeSpeed = 3.2f;

        private Canvas canvas;
        private CanvasGroup contentGroup;
        private RectTransform contentRoot, safeArea, lantern, flame;
        private Image blackout, halo, lanternFrame, flameImage;
        private TMP_Text title, checkpoint, footer;
        private RectTransform glyphFooter;
        private CanvasGroup glyphGroup;
        private Image navigationGlyph, confirmGlyph, backGlyph;
        private readonly ActionItem[] buttons = new ActionItem[2];
        private sealed class ActionItem
        {
            public RectTransform Rect;
            public Image Underline;
            public TMP_Text Label;
            public float Focus;
        }

        private float currentFadeAlpha, targetFadeAlpha, deathElapsed, navigationTimer;
        private int selectedButtonIndex, totalDeaths;
        private DeathReason currentReason;
        private bool isSubscribed, ownsController, originalAuto, originalManual;
        private bool armed, busy, preview, returningToTitle;
        private bool pointerSeeded;
        private Vector2 lastMousePosition;
        private ControllerPromptStyle lastStyle = (ControllerPromptStyle)(-1);
        private Rect lastSafeArea;
        private Vector2 lastCanvasSize;

        public PlayerRespawnController TargetController => respawnController;
        public Canvas Canvas => canvas;
        public float CurrentFadeAlpha => currentFadeAlpha;
        public bool IsVisible => targetFadeAlpha > 0 || currentFadeAlpha > .001f;
        public int SelectedButtonIndex => selectedButtonIndex;
        public int TotalDeaths => totalDeaths;
        public bool IsArmed => armed;
        public DeathReason CurrentDeathReason => currentReason;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeScreen = null;

        private void Awake()
        {
            totalDeaths = PlayerPrefs.GetInt(TotalDeathsPrefKey, 0);
            BuildHierarchy();
        }
        private void Start()
        {
            if (respawnController == null) respawnController = FindAnyObjectByType<PlayerRespawnController>();
            TrySubscribe();
        }
        private void OnEnable() => TrySubscribe();
        private void OnDisable()
        {
            Unsubscribe();
            StopAllCoroutines();
            busy = returningToTitle = false;
            currentFadeAlpha = targetFadeAlpha = 0;
            armed = false;
            if (canvas != null) canvas.enabled = false;
            RestoreLegacyPolling();
            GameControls.BlockGameplay(this, false);
            if (activeScreen == this) activeScreen = null;
        }
        public void SetTarget(PlayerRespawnController controller)
        {
            Unsubscribe();
            currentFadeAlpha = targetFadeAlpha = 0;
            if (canvas != null) canvas.enabled = false;
            RestoreLegacyPolling();
            GameControls.BlockGameplay(this, false);
            if (activeScreen == this) activeScreen = null;
            respawnController = controller;
            TrySubscribe();
        }
        private void TrySubscribe()
        {
            if (respawnController == null || isSubscribed) return;
            respawnController.OnPlayerDied += HandlePlayerDied;
            respawnController.OnPlayerRespawned += HandlePlayerRespawned;
            isSubscribed = true;
        }
        private void Unsubscribe()
        {
            ReleaseController();
            if (respawnController != null && isSubscribed)
            {
                respawnController.OnPlayerDied -= HandlePlayerDied;
                respawnController.OnPlayerRespawned -= HandlePlayerRespawned;
            }
            isSubscribed = false;
        }
        private void SuspendLegacyPolling()
        {
            LegacyInteractionPollingGate.Acquire(this);
        }
        private void RestoreLegacyPolling()
        {
            LegacyInteractionPollingGate.Release(this);
        }
        private void OwnRecoveryDecision()
        {
            if (respawnController == null || preview) return;
            if (!ownsController)
            {
                originalAuto = respawnController.AutoRespawnEnabled;
                originalManual = respawnController.AllowManualRespawnInput;
                ownsController = true;
            }
            respawnController.AutoRespawnEnabled = false;
            respawnController.AllowManualRespawnInput = false;
        }
        private void ReleaseController()
        {
            if (ownsController && respawnController != null)
            {
                respawnController.AutoRespawnEnabled = originalAuto;
                respawnController.AllowManualRespawnInput = originalManual;
            }
            ownsController = false;
        }

        public void HandlePlayerDied(DeathReason reason) => BeginDeath(reason, false);
        public void PreviewDeath(DeathReason reason) => BeginDeath(reason, true);
        private void BeginDeath(DeathReason reason, bool isPreview)
        {
            if ((isPreview && ownsController) || returningToTitle || (!isPreview && !preview && targetFadeAlpha > 0)) return;
            preview = isPreview;
            currentReason = reason;
            targetFadeAlpha = 1;
            deathElapsed = navigationTimer = 0;
            selectedButtonIndex = 0;
            armed = busy = false;
            pointerSeeded = Mouse.current != null;
            if (pointerSeeded) lastMousePosition = Mouse.current.position.ReadValue();
            activeScreen = this;
            canvas.enabled = true;
            GameControls.BlockGameplay(this, true);
            SuspendLegacyPolling();
            if (!isPreview)
            {
                totalDeaths++;
                PlayerPrefs.SetInt(TotalDeathsPrefKey, totalDeaths);
                OwnRecoveryDecision();
                ControllerGlyphService.PlayHaptic(HapticCue.Death);
                if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayFlareExtinguish();
            }
            switch (reason)
            {
                case DeathReason.Darkness:
                    title.text = "The light went out";
                    break;
                case DeathReason.Void:
                    title.text = "You fell";
                    break;
                default:
                    title.text = "Caught in a hazard";
                    break;
            }
            Paint(0);
            RefreshPrompts();
        }
        public void HandlePlayerRespawned()
        {
            ReleaseController();
            targetFadeAlpha = 0;
            armed = false;
            busy = true;
            if (!preview && SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayFlareIgnite();
            // Checkpoints can kill and recover synchronously, before the first visual frame.
            // There is no closing animation to run in that case, but input still needs releasing.
            if (currentFadeAlpha <= .001f) FinishClose();
        }

        private void FinishClose()
        {
            currentFadeAlpha = targetFadeAlpha = 0;
            canvas.enabled = false;
            RestoreLegacyPolling();
            GameControls.BlockGameplay(this, false);
            if (activeScreen == this) activeScreen = null;
        }

        private void Update()
        {
            if (!IsVisible) return;
            float dt = Time.unscaledDeltaTime;
            deathElapsed += dt;
            float speed = targetFadeAlpha > currentFadeAlpha ? deathFadeSpeed : respawnFadeSpeed;
            if (ArchiveInterfaceStyle.ReducedMotion) speed = Mathf.Max(12, speed);
            currentFadeAlpha = Mathf.MoveTowards(currentFadeAlpha, targetFadeAlpha, speed * dt);
            Paint(dt);
            if (targetFadeAlpha <= 0 && currentFadeAlpha <= .001f)
            {
                FinishClose();
                return;
            }
            if (returningToTitle || targetFadeAlpha <= 0 || busy) return;
            if (!armed)
            {
                // A jump or held submit that caused the fall must never choose a recovery action.
                armed = currentFadeAlpha >= .98f && deathElapsed >= .45f && !IsConfirmHeld();
                return;
            }
            HandleNavigation(dt);
            if (!busy) HandlePointer();
        }
        private void LateUpdate()
        {
            if (IsVisible) FitContent();
        }
        private void HandleNavigation(float dt)
        {
            var k = Keyboard.current;
            var pad = ControllerGlyphService.GetActiveGamepad();
            navigationTimer = Mathf.Max(0, navigationTimer - dt);
            bool down = k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame);
            bool up = k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame);
            if (pad != null)
            {
                down |= pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
                up |= pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame;
                float axis = pad.leftStick.ReadValue().y;
                if (navigationTimer <= 0 && Mathf.Abs(axis) > .55f) { down |= axis < 0; up |= axis > 0; navigationTimer = .23f; }
            }
            if (down) SetSelectedButton(1);
            else if (up) SetSelectedButton(0);
            // Back selects the title action; leaving still requires an explicit confirmation.
            if ((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            { SetSelectedButton(1); return; }
            if (k != null && k.rKey.wasPressedThisFrame) { Activate(0); return; }
            if ((k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame))
                Activate(selectedButtonIndex);
        }
        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            bool moved = !pointerSeeded || (pos - lastMousePosition).sqrMagnitude > 4;
            bool clicked = mouse.leftButton.wasPressedThisFrame;
            if (!moved && !clicked) return;
            lastMousePosition = pos;
            pointerSeeded = true;
            for (int i = 0; i < buttons.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(buttons[i].Rect, pos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera))
                {
                    SetSelectedButton(i);
                    if (clicked) Activate(i);
                    return;
                }
        }
        public void SetSelectedButton(int index)
        {
            index = Mathf.Clamp(index, 0, 1);
            if (selectedButtonIndex == index) return;
            selectedButtonIndex = index;
            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIHover(.65f);
        }
        private void Activate(int index)
        {
            if (busy || !armed || targetFadeAlpha <= 0) return;
            busy = true;
            if (SoundEffectManager.HasInstance) SoundEffectManager.Instance.PlayUIClick();
            if (index == 0)
            {
                if (!preview && respawnController != null) respawnController.Respawn();
                else HandlePlayerRespawned();
            }
            else StartCoroutine(ReturnToTitle());
        }
        private System.Collections.IEnumerator ReturnToTitle()
        {
            returningToTitle = true;
            armed = false;
            PlayerPrefs.Save();
            // The opaque backdrop remains while the menu content fades and the destination loads.
            float elapsed = 0, start = contentGroup.alpha;
            float duration = ArchiveInterfaceStyle.ReducedMotion ? 0 : .22f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                contentGroup.alpha = Mathf.Lerp(start, 0, elapsed / duration);
                yield return null;
            }
            contentGroup.alpha = 0;
            var operation = SceneManager.LoadSceneAsync(MainMenuScenePath);
            if (operation != null) yield return operation;
        }
        private static bool IsConfirmHeld()
        {
            var k = Keyboard.current;
            var pad = ControllerGlyphService.GetActiveGamepad();
            return (k != null && (k.enterKey.isPressed || k.spaceKey.isPressed || k.rKey.isPressed)) ||
                (pad != null && pad.buttonSouth.isPressed) || (Mouse.current != null && Mouse.current.leftButton.isPressed);
        }

        private void BuildHierarchy()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            blackout = Picture("Archive blackout", transform, Vector2.zero, Vector2.zero, PixelSprites.White);
            Stretch(blackout.rectTransform);
            blackout.raycastTarget = true;
            safeArea = Rect("Death safe area", transform, Vector2.zero, Vector2.zero);
            Stretch(safeArea);
            contentRoot = Rect("ContentRoot", safeArea, Vector2.zero, new Vector2(1120, 660));
            contentGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            halo = Picture("Lantern halo", contentRoot, new Vector2(0, 204), new Vector2(180, 150), ArchiveInterfaceStyle.SoftGlow);
            lantern = Rect("Fading lantern", contentRoot, new Vector2(0, 208), new Vector2(48, 66));
            flameImage = Picture("Last flame", lantern, new Vector2(0, -3), new Vector2(17, 24), PixelSprites.Flame);
            flame = flameImage.rectTransform;
            lanternFrame = Picture("Lantern frame", lantern, Vector2.zero, new Vector2(48, 66), PixelSprites.LanternFrame);
            title = Label("Title", new Vector2(0, 108), new Vector2(1040, 76), "", 48, true);
            title.characterSpacing = 1.4f;
            checkpoint = Label("Checkpoint", new Vector2(0, 38), new Vector2(1000, 40),
                $"Retry from the last checkpoint with {LanternFuel.StartingEmbers} embers.", 22, false);
            buttons[0] = BuildButton(0, -64, "Try again");
            buttons[1] = BuildButton(1, -142, "Return to title");
            footer = Label("Hint", new Vector2(0, -276), new Vector2(1040, 68), "", 20, false);
            glyphFooter = Rect("HintGlyphsBar", contentRoot, new Vector2(0, -276), new Vector2(1000, 44));
            glyphGroup = glyphFooter.gameObject.AddComponent<CanvasGroup>();
            navigationGlyph = GlyphSlot("Select", -330, "CHOOSE");
            confirmGlyph = GlyphSlot("Confirm", -30, "CONFIRM");
            backGlyph = GlyphSlot("Return", 290, "SELECT TITLE");
            canvas.enabled = false;
        }
        private ActionItem BuildButton(int index, float y, string text)
        {
            var rect = Rect("Button_" + index, contentRoot, new Vector2(0, y), new Vector2(660, 66));
            var item = new ActionItem { Rect = rect };
            item.Label = Text("Label", rect, Vector2.zero, new Vector2(600, 54), text, 30, true);
            item.Underline = Picture("Focus underline", rect, new Vector2(0, -27), new Vector2(64, 2), PixelSprites.White);
            return item;
        }
        private Image GlyphSlot(string name, float x, string text)
        {
            var slot = Rect(name, glyphFooter, new Vector2(x, 0), new Vector2(250, 44));
            var icon = Picture(name + "Icon", slot, new Vector2(-95, 0), new Vector2(32, 32), null);
            var label = Text(name + "Text", slot, new Vector2(22, 0), new Vector2(180, 38), text, 20, false);
            label.color = ArchiveInterfaceStyle.Silver;
            return icon;
        }
        private void FitContent()
        {
            Vector2 size = ((RectTransform)canvas.transform).rect.size;
            Rect safe = Screen.safeArea;
            if (size == lastCanvasSize && safe == lastSafeArea) return;
            lastCanvasSize = size; lastSafeArea = safe;
            if (Screen.width > 0 && Screen.height > 0)
            {
                safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            }
            contentRoot.localScale = Vector3.one * ArchiveInterfaceStyle.FitScale(safeArea.rect.size, new Vector2(1120, 660));
        }
        private void Paint(float dt)
        {
            bool still = ArchiveInterfaceStyle.ReducedMotion;
            float a = Mathf.SmoothStep(0, 1, currentFadeAlpha);
            blackout.color = new Color(.008f, .009f, .010f, a);
            if (!returningToTitle) contentGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .86f, currentFadeAlpha));
            // One extinguish and one recovery. A dead lantern does not keep glowing or breathing.
            float remaining = 1 - Mathf.SmoothStep(0, 1, deathElapsed / .85f);
            if (targetFadeAlpha == 0) remaining = 1 - currentFadeAlpha;
            halo.color = new Color(1, .65f, .2f, .12f * remaining);
            lanternFrame.color = Color.Lerp(new Color(.53f, .53f, .52f), new Color(.88f, .79f, .60f), remaining);
            flameImage.color = new Color(1, .85f, .58f, remaining);
            flame.localScale = new Vector3(1, still ? 1 : Mathf.Lerp(.4f, 1, remaining), 1);
            title.color = ArchiveInterfaceStyle.Ivory;
            checkpoint.color = ArchiveInterfaceStyle.Silver;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                b.Focus = Mathf.MoveTowards(b.Focus, selectedButtonIndex == i ? 1 : 0, still ? 1 : dt * 9);
                b.Label.color = Color.Lerp(ArchiveInterfaceStyle.Ivory, ArchiveInterfaceStyle.Amber, b.Focus);
                b.Underline.color = new Color(1, .87f, .62f, b.Focus * (armed ? 1 : .45f));
            }
            RefreshPrompts();
            footer.color = new Color(.78f, .83f, .89f, armed ? .85f : .38f);
            glyphGroup.alpha = armed ? .85f : .38f;
        }
        private void RefreshPrompts()
        {
            var style = ControllerGlyphService.ActiveStyle;
            if (lastStyle == style) return;
            lastStyle = style;
            bool pad = style == ControllerPromptStyle.PlayStation || style == ControllerPromptStyle.Xbox;
            footer.gameObject.SetActive(!pad);
            glyphFooter.gameObject.SetActive(pad);
            if (pad)
            {
                navigationGlyph.sprite = ControllerGlyphService.GetGlyphSprite("DPad", style);
                confirmGlyph.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Confirm, style);
                backGlyph.sprite = ControllerGlyphService.GetActionSprite(GlyphAction.Cancel, style);
            }
            else footer.text = "↑ / ↓   CHOOSE     ENTER   CONFIRM     ESC   SELECT TITLE";
        }
        private TMP_Text Label(string name, Vector2 pos, Vector2 size, string text, float fontSize, bool display) => Text(name, contentRoot, pos, size, text, fontSize, display);
        private TMP_Text Text(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize, bool display)
        {
            var label = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = display ? ArchiveInterfaceStyle.DisplayFont : ArchiveInterfaceStyle.BodyFont;
            if (label.font == null) label.font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f;
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }
        private static Image Picture(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }
        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
