using System;
using TheLightenRoad.Audio;
using TheLightenRoad.Input;
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
        private Image blackout, halo, lanternFrame, flameImage, crest;
        private TMP_Text title, story, checkpoint, eyebrow, footer;
        private RectTransform glyphFooter;
        private CanvasGroup glyphGroup;
        private Image navigationGlyph, confirmGlyph, backGlyph;
        private readonly System.Collections.Generic.List<MonoBehaviour> suspendedInteractions = new System.Collections.Generic.List<MonoBehaviour>();
        private readonly Image[] ashes = new Image[14];
        private readonly ActionItem[] buttons = new ActionItem[2];
        private sealed class ActionItem
        {
            public RectTransform Rect;
            public Image Glow, Left, Right;
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
            // Same UI boundary as pause: suspend raw input polling, preserving circuit/device state.
            foreach (var actor in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (actor.enabled && actor.GetType().FullName == "TheLightenRoad.Interaction.EmberVein")
                { suspendedInteractions.Add(actor); actor.enabled = false; }
        }
        private void RestoreLegacyPolling()
        {
            foreach (var actor in suspendedInteractions) if (actor != null) actor.enabled = true;
            suspendedInteractions.Clear();
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
                    title.text = "The light has faded";
                    story.text = "Your last ember went dark.\nA little light is enough to begin again.";
                    break;
                case DeathReason.Void:
                    title.text = "Lost to the depths";
                    story.text = "The path vanished beneath your feet.\nThere is still a way back.";
                    break;
                default:
                    title.text = "A light interrupted";
                    story.text = "The ruins claimed another step.\nYour journey does not end here.";
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
            contentRoot = Rect("ContentRoot", safeArea, Vector2.zero, new Vector2(1120, 820));
            contentGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            halo = Picture("Lantern halo", contentRoot, new Vector2(0, 190), new Vector2(520, 380), ArchiveInterfaceStyle.SoftGlow);
            for (int i = 0; i < ashes.Length; i++) ashes[i] = Picture("Quiet ash " + i, contentRoot, Vector2.zero, new Vector2(i % 3 == 0 ? 3 : 2, 3), PixelSprites.Spark);
            eyebrow = Label("Archive caption", new Vector2(0, 326), new Vector2(900, 34), "THE LIGHTEN ROAD", 20, true);
            eyebrow.characterSpacing = 3;
            lantern = Rect("Fading lantern", contentRoot, new Vector2(0, 220), new Vector2(48, 66));
            flameImage = Picture("Last flame", lantern, new Vector2(0, -3), new Vector2(17, 24), PixelSprites.Flame);
            flame = flameImage.rectTransform;
            lanternFrame = Picture("Lantern frame", lantern, Vector2.zero, new Vector2(48, 66), PixelSprites.LanternFrame);
            title = Label("Title", new Vector2(0, 112), new Vector2(1040, 76), "", 48, true);
            title.characterSpacing = 1.4f;
            crest = Picture("Archive filigree", contentRoot, new Vector2(0, 55), new Vector2(320, 26), Resources.Load<Sprite>("UI/HollowKnight_TopFiligree"));
            if (crest.sprite == null) { crest.sprite = PixelSprites.White; crest.rectTransform.sizeDelta = new Vector2(180, 1); }
            story = Label("Story", new Vector2(0, -8), new Vector2(940, 84), "", 24, false);
            checkpoint = Label("Checkpoint", new Vector2(0, -83), new Vector2(900, 36), "Return to your last checkpoint.", 20, false);
            buttons[0] = BuildButton(0, -160, "Rekindle the lantern");
            buttons[1] = BuildButton(1, -238, "Return to title");
            footer = Label("Hint", new Vector2(0, -354), new Vector2(1040, 40), "", 20, false);
            glyphFooter = Rect("HintGlyphsBar", contentRoot, new Vector2(0, -354), new Vector2(1000, 44));
            glyphGroup = glyphFooter.gameObject.AddComponent<CanvasGroup>();
            navigationGlyph = GlyphSlot("Select", -330, "CHOOSE");
            confirmGlyph = GlyphSlot("Confirm", -30, "CONFIRM");
            backGlyph = GlyphSlot("Return", 290, "TITLE");
            canvas.enabled = false;
        }
        private ActionItem BuildButton(int index, float y, string text)
        {
            var rect = Rect("Button_" + index, contentRoot, new Vector2(0, y), new Vector2(660, 66));
            var item = new ActionItem { Rect = rect };
            item.Glow = Picture("Focus light", rect, Vector2.zero, new Vector2(640, 110), ArchiveInterfaceStyle.SoftGlow);
            item.Label = Text("Label", rect, Vector2.zero, new Vector2(600, 54), text, 30, true);
            var pointer = Resources.Load<Sprite>("UI/HollowKnight_ButtonPointer");
            item.Left = Picture("Left pointer", rect, new Vector2(-285, 0), new Vector2(38, 20), pointer);
            item.Right = Picture("Right pointer", rect, new Vector2(285, 0), new Vector2(38, 20), pointer);
            item.Right.rectTransform.localScale = new Vector3(-1, 1, 1);
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
            contentRoot.localScale = Vector3.one * ArchiveInterfaceStyle.FitScale(safeArea.rect.size, new Vector2(1120, 820));
        }
        private void Paint(float dt)
        {
            bool still = ArchiveInterfaceStyle.ReducedMotion;
            float a = Mathf.SmoothStep(0, 1, currentFadeAlpha);
            blackout.color = new Color(.008f, .012f, .020f, a);
            if (!returningToTitle) contentGroup.alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.18f, .86f, currentFadeAlpha));
            contentRoot.anchoredPosition = new Vector2(0, still ? 0 : Mathf.Lerp(-14, 0, a));
            Color accent = currentReason == DeathReason.Void ? new Color(.58f, .68f, .82f) : ArchiveInterfaceStyle.Amber;
            float breath = still ? 1 : 1 + .035f * Mathf.Sin(Time.unscaledTime * 1.9f);
            float remaining = Mathf.Lerp(1, .13f, Mathf.Clamp01(deathElapsed / 2.2f));
            if (targetFadeAlpha == 0) remaining = Mathf.Lerp(.13f, 1, 1 - currentFadeAlpha);
            halo.color = new Color(accent.r, accent.g * .78f, accent.b * .5f, .11f * breath);
            halo.rectTransform.localScale = Vector3.one * breath;
            lanternFrame.color = Color.Lerp(new Color(.53f, .53f, .52f), new Color(.88f, .79f, .60f), remaining);
            flameImage.color = new Color(1, .85f, .58f, remaining);
            flame.localScale = new Vector3(1, still ? 1 : breath, 1);
            title.color = ArchiveInterfaceStyle.Ivory;
            eyebrow.color = new Color(accent.r, accent.g, accent.b, .85f);
            crest.color = new Color(.81f, .78f, .67f, .58f);
            story.color = ArchiveInterfaceStyle.Silver;
            checkpoint.color = new Color(.66f, .70f, .76f, .8f);
            for (int i = 0; i < ashes.Length; i++)
            {
                float phase = (i * .173f + (still ? 0 : deathElapsed * .025f)) % 1;
                float x = ((i * 197 % 101) / 100f - .5f) * 1050;
                float y = Mathf.Lerp(-315, 305, phase);
                // Keep the centre column clear for the message and actions.
                if (Mathf.Abs(x) < 310) x += Mathf.Sign(x == 0 ? 1 : x) * 330;
                ashes[i].rectTransform.anchoredPosition = new Vector2(x, y);
                ashes[i].color = new Color(.85f, .67f, .43f, .12f * Mathf.Sin(phase * Mathf.PI));
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                b.Focus = Mathf.MoveTowards(b.Focus, selectedButtonIndex == i ? 1 : 0, still ? 1 : dt * 9);
                b.Glow.color = new Color(1, .65f, .2f, b.Focus * .16f);
                b.Label.color = Color.Lerp(ArchiveInterfaceStyle.Ivory, ArchiveInterfaceStyle.Amber, b.Focus);
                float wings = b.Focus * (armed ? 1 : .45f);
                b.Left.color = b.Right.color = new Color(1, .87f, .62f, wings);
                float offset = Mathf.Min(290, b.Label.preferredWidth * .5f + 36);
                b.Left.rectTransform.anchoredPosition = new Vector2(-offset, 0);
                b.Right.rectTransform.anchoredPosition = new Vector2(offset, 0);
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
            else footer.text = "W / S  or  ↑ / ↓   CHOOSE     ENTER   CONFIRM     ESC   TITLE";
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
