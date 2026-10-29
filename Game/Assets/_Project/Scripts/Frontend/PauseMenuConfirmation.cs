using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    public sealed partial class PauseMenuController
    {
        private GameObject confirmationPanel;
        private CanvasGroup confirmationGroup;
        private RectTransform confirmationContent;
        private TMP_Text confirmationTitle, confirmationBody, pauseSaveStatus;
        private Button confirmationCancelButton, confirmationAcceptButton;
        private GameObject confirmationOrigin;
        private string confirmationReturnView;
        private System.Action confirmationAction;
        private float confirmationAge, pauseSaveDelay;
        private bool pauseSettingsDirty, returningToTitle;

        public bool IsConfirmationOpen => confirmationPanel != null && confirmationPanel.activeSelf;

        private void BuildPauseConfirmation(Transform parent)
        {
            var root = CreateAnchoredRect("Pause confirmation", parent, new Vector2(1040, 700), Vector2.one * .5f, Vector2.zero);
            confirmationPanel = root.gameObject;
            confirmationGroup = root.gameObject.AddComponent<CanvasGroup>();
            confirmationContent = CreateStretchRect("Lantern decision", root);

            var glow = CreateAnchoredRect("Warm lantern halo", confirmationContent, new Vector2(500, 330), Vector2.one * .5f, new Vector2(0, 100)).gameObject.AddComponent<Image>();
            glow.sprite = softRadialSprite;
            glow.color = new Color(1f, .49f, .12f, .16f);
            glow.raycastTarget = false;

            var frame = CreateAnchoredRect("Decision lantern", confirmationContent, new Vector2(44, 60), Vector2.one * .5f, new Vector2(0, 194));
            var flame = CreateAnchoredRect("Decision flame", frame, new Vector2(16, 22), Vector2.one * .5f, new Vector2(0, -2)).gameObject.AddComponent<Image>();
            flame.sprite = PixelSprites.Flame;
            flame.preserveAspect = true;
            flame.raycastTarget = false;
            var lantern = frame.gameObject.AddComponent<Image>();
            lantern.sprite = PixelSprites.LanternFrame;
            lantern.preserveAspect = true;
            lantern.color = new Color(1f, .85f, .59f);
            lantern.raycastTarget = false;

            var eyebrow = CreateLabel("Decision caption", confirmationContent, new Vector2(0, 122), new Vector2(800, 32), "A MOMENT BEFORE YOU GO", 20, TextAlignmentOptions.Center, amberGold);
            eyebrow.characterSpacing = 3;
            confirmationTitle = CreateLabel("Decision title", confirmationContent, new Vector2(0, 64), new Vector2(900, 60), "", 38, TextAlignmentOptions.Center, radiantIvory);
            confirmationBody = CreateLabel("Decision explanation", confirmationContent, new Vector2(0, -22), new Vector2(800, 100), "", 24, TextAlignmentOptions.Center, moonlightSilver);
            confirmationBody.characterSpacing = 0;

            confirmationCancelButton = CreateEtherealActionButton("Cancel decision", confirmationContent, "Keep exploring", new Vector2(-210, -142), CancelConfirmation, true, 360);
            confirmationAcceptButton = CreateEtherealActionButton("Confirm decision", confirmationContent, "Confirm", new Vector2(210, -142), ConfirmPendingAction, false, 360);
            foreach (var button in new[] { confirmationCancelButton, confirmationAcceptButton })
            {
                ((RectTransform)button.transform).sizeDelta = new Vector2(360, 60);
                MenuReadability.HideFocusWings(button);
                button.GetComponentInChildren<TMP_Text>().fontSize = 26;
            }
            confirmationCancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = confirmationAcceptButton, selectOnDown = confirmationAcceptButton };
            confirmationAcceptButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = confirmationCancelButton, selectOnUp = confirmationCancelButton };

            pauseSaveStatus = CreateLabel("Pause settings save status", settingsPanel.transform, new Vector2(0, -236), new Vector2(800, 32), "Saved automatically on this device", 20, TextAlignmentOptions.Center, moonlightSilver);
            confirmationPanel.SetActive(false);
        }

        public void RequestRestartChamber() => RequestConfirmation("Restart this chamber?",
            "Return to the last checkpoint.\nYour current attempt will be reset.", "Restart chamber", RestartChamber);

        public void RequestQuitToMainMenu() => RequestConfirmation("Return to the title?",
            "Your settings and completed chapters are saved.\nProgress in this attempt will be lost.", "Return to title", QuitToMainMenu);

        public void RequestResetDefaults() => RequestConfirmation("Restore default settings?",
            "Audio, display and controller preferences will reset.\nYour discoveries and completed chapters are kept.", "Restore defaults", () => { ResetDefaults(); ShowSettingsView(); });

        private void RequestConfirmation(string title, string body, string accept, System.Action action)
        {
            if (!IsPaused || IsConfirmationOpen || returningToTitle) return;
            FlushPauseSettingsSave();
            confirmationOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            confirmationReturnView = currentView;
            confirmationAction = action;
            currentView = "confirmation";
            if (subViewRoutine != null) { StopCoroutine(subViewRoutine); subViewRoutine = null; }
            mainHeader.SetActive(false);
            mainPanel.SetActive(false);
            settingsPanel.SetActive(false);
            controlsPanel.SetActive(false);
            confirmationTitle.text = title;
            confirmationBody.text = body;
            confirmationAcceptButton.GetComponentInChildren<TMP_Text>().text = accept;
            confirmationCancelButton.GetComponentInChildren<TMP_Text>().text = confirmationReturnView == "settings" ? "Keep my settings" : "Keep exploring";
            confirmationAge = 0;
            confirmationPanel.SetActive(true);
            AnimateConfirmation(0);
            RefreshPauseFooterPrompts();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmationCancelButton.gameObject);
        }

        public void CancelConfirmation()
        {
            if (!IsConfirmationOpen) return;
            var origin = confirmationOrigin;
            bool settings = confirmationReturnView == "settings";
            if (settings) ShowSettingsView(); else ShowMainView();
            if (origin != null && origin.activeInHierarchy && origin.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(origin);
        }

        public void ConfirmPendingAction()
        {
            if (!IsConfirmationOpen || returningToTitle) return;
            var action = confirmationAction;
            HideConfirmation();
            action?.Invoke();
        }

        private void HideConfirmation()
        {
            confirmationAction = null;
            if (confirmationPanel != null) confirmationPanel.SetActive(false);
        }

        private void RestoreLostPauseSelection(Keyboard keyboard, Gamepad pad)
        {
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy && selected.TryGetComponent<Selectable>(out var usable) && usable.IsInteractable()) return;
            bool keyboardIntent = keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame);
            bool padIntent = pad != null && (pad.dpad.ReadValue().sqrMagnitude > .5f || pad.leftStick.ReadValue().sqrMagnitude > .15f || pad.buttonSouth.wasPressedThisFrame);
            if (keyboardIntent || padIntent) RestorePauseMenuSelection();
        }

        private void AnimateConfirmation(float dt)
        {
            confirmationAge += dt;
            float reveal = MenuPreferences.ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(confirmationAge / .22f));
            confirmationGroup.alpha = reveal;
            confirmationContent.anchoredPosition = new Vector2(0, Mathf.Lerp(-12, 0, reveal));
        }

        private void MarkPauseSettingsChanged()
        {
            pauseSettingsDirty = true;
            pauseSaveDelay = .35f;
            if (pauseSaveStatus != null) pauseSaveStatus.text = "Saving preferences...";
        }

        private void TickPauseSettingsSave()
        {
            if (IsConfirmationOpen) AnimateConfirmation(Time.unscaledDeltaTime);
            if (!pauseSettingsDirty) return;
            pauseSaveDelay -= Time.unscaledDeltaTime;
            if (pauseSaveDelay <= 0) FlushPauseSettingsSave();
        }

        private void FlushPauseSettingsSave()
        {
            if (!pauseSettingsDirty) return;
            MenuPreferences.Save();
            pauseSettingsDirty = false;
            if (pauseSaveStatus != null) pauseSaveStatus.text = "Saved automatically on this device";
        }

        private void OnApplicationFocus(bool focus) { if (!focus) FlushPauseSettingsSave(); }
        private void OnApplicationPause(bool paused) { if (paused) FlushPauseSettingsSave(); }
    }
}
