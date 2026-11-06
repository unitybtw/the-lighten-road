using TheLightenRoad.Input;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TMPro;

namespace TheLightenRoad.Frontend.Tests
{
    public class PauseMenuTests
    {
        private GameObject host;
        private PauseMenuController pauseMenu;

        [SetUp]
        public void SetUp()
        {
            PauseMenuController.ResetStaticStateForTesting();
            Time.timeScale = 1f;
            MenuPreferences.ResetToDefaults();
            host = new GameObject("PauseMenuTestHost");
            pauseMenu = host.AddComponent<PauseMenuController>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (pauseMenu != null && PauseMenuController.IsPaused)
            {
                pauseMenu.Resume();
            }
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
            PauseMenuController.ResetStaticStateForTesting();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator PauseMenu_AwakensAndBuildsFullUIHierarchy()
        {
            yield return null;

            Assert.That(pauseMenu, Is.Not.Null);
            Assert.That(PauseMenuController.Instance, Is.SameAs(pauseMenu));
            Assert.That(PauseMenuController.IsPaused, Is.False);

            Assert.That(GameObject.Find("PauseCanvas"), Is.Not.Null);
            Assert.That(GameObject.Find("ModalContainer"), Is.Not.Null);
            Assert.That(GameObject.Find("MainPanel"), Is.Not.Null);
            Assert.That(GameObject.Find("ResumeButton"), Is.Not.Null);
            Assert.That(GameObject.Find("ControlsButton"), Is.Not.Null);
            Assert.That(GameObject.Find("SettingsButton"), Is.Not.Null);
            Assert.That(GameObject.Find("RestartButton"), Is.Not.Null);
            Assert.That(GameObject.Find("QuitButton"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PauseHomeKeepsActionsReadableAndSeparateAcrossCanvasSizes()
        {
            var fuelHost = new GameObject("Pause preview lantern");
            fuelHost.AddComponent<TheLightenRoad.Lantern.LanternFuel>();
            fuelHost.AddComponent<TheLightenRoad.Lantern.LanternController>();
            try
            {
                pauseMenu.Pause();
                yield return new WaitForSecondsRealtime(.3f);
                var flame = pauseMenu.GetComponentsInChildren<Image>().Single(i => i.name == "Pause lantern flame");
                Assert.That(flame.enabled, Is.True);
                fuelHost.GetComponent<TheLightenRoad.Lantern.LanternFuel>().Initialize(0, 8);
                pauseMenu.ShowMainView();
                Assert.That(flame.enabled, Is.False, "An empty lantern must not display a lit flame.");
                fuelHost.GetComponent<TheLightenRoad.Lantern.LanternFuel>().Initialize(6, 8);
                pauseMenu.ShowMainView();
                var buttons = pauseMenu.MainPanel.GetComponentsInChildren<Button>();
                foreach (var button in buttons)
                    Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(64));
                var settings = buttons.Single(b => b.name == "SettingsButton");
                settings.GetComponent<MenuButtonMotion>().OnPointerEnter(null);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(settings.gameObject));
                EventSystem.current.SetSelectedGameObject(buttons.Single(b => b.name == "ResumeButton").gameObject);
                yield return new WaitForSecondsRealtime(.2f);
                var canvas = pauseMenu.GetComponentInChildren<Canvas>();
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768) })
                {
                    MenuReadabilityTests.Capture(canvas, size.x, size.y, "pause-home-" + size.x);
                    var rects = buttons.Select(b => RelativeRect((RectTransform)b.transform, canvas.transform)).ToArray();
                    for (int i = 0; i < rects.Length; i++)
                    {
                        var safe = ((RectTransform)canvas.transform).rect;
                        Assert.That(safe.Contains(rects[i].min) && safe.Contains(rects[i].max), Is.True, buttons[i].name);
                        for (int j = i + 1; j < rects.Length; j++) Assert.That(rects[i].Overlaps(rects[j]), Is.False);
                    }
                }
                pauseMenu.RequestRestartChamber();
                yield return new WaitForSecondsRealtime(.3f);
                MenuReadabilityTests.Capture(canvas, 1280, 720, "pause-restart-confirmation");
            }
            finally { Object.DestroyImmediate(fuelHost); }
        }

        private static Rect RelativeRect(RectTransform rect, Transform canvas)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var low = canvas.InverseTransformPoint(corners[0]); var high = canvas.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        [UnityTest]
        public IEnumerator ReversingPauseMotionContinuesFromTheVisibleFrame()
        {
            yield return null;
            var modal = pauseMenu.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "ModalContainer");
            var group = pauseMenu.GetComponentsInChildren<CanvasGroup>(true).Single(g => g.name == "PauseCanvas");
            pauseMenu.Pause();
            yield return new WaitForSecondsRealtime(.06f);
            Vector3 openingScale = modal.localScale;
            Vector2 openingPosition = modal.anchoredPosition;
            pauseMenu.Resume();
            Assert.That(Vector3.Distance(modal.localScale, openingScale), Is.LessThan(.005f));
            Assert.That(Vector2.Distance(modal.anchoredPosition, openingPosition), Is.LessThan(1f));
            yield return new WaitForSecondsRealtime(.04f);
            float closingAlpha = group.alpha;
            Vector3 closingScale = modal.localScale;
            Vector2 closingPosition = modal.anchoredPosition;
            pauseMenu.Pause();
            Assert.That(group.alpha, Is.GreaterThanOrEqualTo(closingAlpha - .02f));
            Assert.That(Vector3.Distance(modal.localScale, closingScale), Is.LessThan(.005f));
            Assert.That(Vector2.Distance(modal.anchoredPosition, closingPosition), Is.LessThan(1f));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(modal.localScale, Is.EqualTo(Vector3.one));
            Assert.That(modal.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator ReducedMotionUsesFadesWithoutZoomOrTranslation()
        {
            MenuPreferences.SetReducedMotion(true);
            var modal = pauseMenu.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "ModalContainer");
            pauseMenu.Pause();
            Assert.That(modal.localScale, Is.EqualTo(Vector3.one));
            Assert.That(modal.anchoredPosition, Is.EqualTo(Vector2.zero));
            yield return new WaitForSecondsRealtime(.08f);
            pauseMenu.Resume();
            Assert.That(modal.localScale, Is.EqualTo(Vector3.one));
            Assert.That(modal.anchoredPosition, Is.EqualTo(Vector2.zero));
            yield return new WaitForSecondsRealtime(.08f);
        }

        [UnityTest]
        public IEnumerator PauseMenu_PausesAndResumesTimeScaleCorrectly()
        {
            yield return null;

            Assert.That(Time.timeScale, Is.EqualTo(1f));

            pauseMenu.Pause();
            Assert.That(PauseMenuController.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("main"));

            pauseMenu.Resume();
            Assert.That(PauseMenuController.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("none"));
        }

        [UnityTest]
        public IEnumerator PauseMenu_SubViewsSwitchCleanly()
        {
            yield return null;

            pauseMenu.Pause();
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("main"));

            pauseMenu.ShowSettingsView();
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("settings"));
            Assert.That(pauseMenu.SettingsPanel.activeSelf, Is.True);
            Assert.That(pauseMenu.MainPanel.activeSelf, Is.False);

            pauseMenu.ShowControlsView();
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("controls"));
            Assert.That(pauseMenu.ControlsPanel.activeSelf, Is.True);
            Assert.That(pauseMenu.SettingsPanel.activeSelf, Is.False);

            pauseMenu.ShowMainView();
            Assert.That(pauseMenu.CurrentView, Is.EqualTo("main"));
            Assert.That(pauseMenu.MainPanel.activeSelf, Is.True);

            pauseMenu.Resume();
        }

        [UnityTest]
        public IEnumerator PauseMenu_SuspendsAndRestoresPlayerMotor()
        {
            var playerGo = new GameObject("TestPlayer", typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerMotor2D));
            var motor = playerGo.GetComponent<PlayerMotor2D>();
            motor.enabled = true;

            yield return null;

            pauseMenu.Pause();
            Assert.That(motor.enabled, Is.True, "Pause should preserve component lifecycle and movement state.");
            Assert.That(GameControls.GameplayAllowed, Is.False);

            pauseMenu.Resume();
            Assert.That(motor.enabled, Is.True);
            Assert.That(GameControls.GameplayAllowed, Is.False, "Resume must consume the menu input frame.");
            yield return null;
            yield return null;
            Assert.That(GameControls.GameplayAllowed, Is.True);

            Object.DestroyImmediate(playerGo);
        }

        [UnityTest]
        public IEnumerator PauseMenu_SettingsSyncsWithMenuPreferences()
        {
            yield return null;

            MenuPreferences.SetAudio(0.5f, 0.4f, 0.9f);
            MenuPreferences.SetReducedMotion(true);

            pauseMenu.Pause();
            pauseMenu.ShowSettingsView();

            Assert.That(MenuPreferences.Master, Is.EqualTo(0.5f));
            Assert.That(MenuPreferences.Music, Is.EqualTo(0.4f));
            Assert.That(MenuPreferences.Effects, Is.EqualTo(0.9f));
            Assert.That(MenuPreferences.ReducedMotion, Is.True);

            pauseMenu.Resume();
        }

        [UnityTest]
        public IEnumerator RestartRequiresConfirmationAndCannotRunTwice()
        {
            yield return null;
            int restarts = 0;
            var previous = PauseMenuController.OnRestartLevelRequested;
            PauseMenuController.OnRestartLevelRequested = () => restarts++;
            try
            {
                Time.timeScale = .5f;
                pauseMenu.Pause();
                var restart = pauseMenu.MainPanel.transform.Find("RestartButton").GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(restart.gameObject);
                restart.onClick.Invoke();
                Assert.That(restarts, Is.Zero);
                Assert.That(pauseMenu.IsConfirmationOpen, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Cancel decision"));
                pauseMenu.CancelConfirmation();
                Assert.That(restarts, Is.Zero);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(restart.gameObject));
                restart.onClick.Invoke();
                pauseMenu.ConfirmPendingAction();
                pauseMenu.ConfirmPendingAction();
                Assert.That(restarts, Is.EqualTo(1));
                Assert.That(Time.timeScale, Is.EqualTo(.5f));
                Assert.That(PauseMenuController.IsPaused, Is.False);
            }
            finally { PauseMenuController.OnRestartLevelRequested = previous; }
        }

        [UnityTest]
        public IEnumerator ReturningToTitleCanBeCancelledWithoutUnpausingOrLeavingTheScene()
        {
            yield return null;
            var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            pauseMenu.Pause();
            var quit = pauseMenu.MainPanel.transform.Find("QuitButton").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            quit.onClick.Invoke();
            Assert.That(pauseMenu.MainPanel.activeSelf, Is.False);
            var cancel = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
            Assert.That(cancel.navigation.selectOnRight.name, Is.EqualTo("Confirm decision"));
            Assert.That(cancel.navigation.selectOnRight.navigation.selectOnLeft, Is.SameAs(cancel));
            cancel.onClick.Invoke();
            yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(originalScene));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GameControls.GameplayAllowed, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(quit.gameObject));
        }

        [UnityTest]
        public IEnumerator RestoreDefaultsCanBeCancelledAndPreservesChapterProgressWhenConfirmed()
        {
            const string progressKey = "TLR.Menu.RealmCompleted.999";
            bool hadProgress = PlayerPrefs.HasKey(progressKey);
            int progress = PlayerPrefs.GetInt(progressKey);
            try
            {
                MenuPreferences.SetRealmCompleted(999);
                MenuPreferences.SetAudio(.23f, .34f, .45f);
                pauseMenu.Pause();
                pauseMenu.ShowSettingsView();
                var reset = pauseMenu.SettingsPanel.transform.Find("ResetDefaultsButton").GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(reset.gameObject);
                reset.onClick.Invoke();
                pauseMenu.CancelConfirmation();
                Assert.That(MenuPreferences.Master, Is.EqualTo(.23f).Within(.001f));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(reset.gameObject));
                reset.onClick.Invoke();
                pauseMenu.ConfirmPendingAction();
                Assert.That(MenuPreferences.Master, Is.EqualTo(.85f).Within(.001f));
                Assert.That(MenuPreferences.IsRealmCompleted(999), Is.True);
                Assert.That(pauseMenu.CurrentView, Is.EqualTo("settings"));
                Assert.That(Time.timeScale, Is.Zero);
                yield return null;
            }
            finally
            {
                if (hadProgress) PlayerPrefs.SetInt(progressKey, progress); else PlayerPrefs.DeleteKey(progressKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator EscapeCancelsTheDecisionBeforeResumingGameplay()
        {
            var previousKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var oldUpdateMode = InputSystem.settings.updateMode;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                keyboard.MakeCurrent();
                // Input System 1.17 lazily enables press tracking on the first property read.
                // Prime it before pumping the event so a newly added device is deterministic.
                Assert.That(keyboard.escapeKey.wasPressedThisFrame, Is.False);
                InputSystem.Update();
                InputState.Change(keyboard, new KeyboardState());
                pauseMenu.Pause();
                pauseMenu.RequestRestartChamber();
                // Test UI routing against real device state. The native Editor can consume queued
                // synthetic Escape events before they reach a paused Game view; this supported
                // state API bypasses that transport without bypassing the menu's input handler.
                InputState.Change(keyboard, new KeyboardState(Key.Escape));
                Assert.That(keyboard.escapeKey.wasPressedThisFrame, Is.True);
                pauseMenu.SendMessage("Update");
                InputState.Change(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(pauseMenu.IsConfirmationOpen, Is.False);
                Assert.That(pauseMenu.CurrentView, Is.EqualTo("main"));
                Assert.That(Time.timeScale, Is.Zero);
                yield return null;
                InputSystem.Update();
                InputState.Change(keyboard, new KeyboardState(Key.Escape));
                pauseMenu.SendMessage("Update");
                InputState.Change(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(PauseMenuController.IsPaused, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                InputSystem.settings.updateMode = oldUpdateMode;
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            }
        }

        [UnityTest]
        public IEnumerator PendingVolumeSaveFinishesWhilePausedAndFlushesWhenDisabled()
        {
            pauseMenu.Pause();
            pauseMenu.ShowSettingsView();
            var slider = pauseMenu.SettingsPanel.transform.Find("Audio settings/MasterSlider/Slider").GetComponent<Slider>();
            var status = pauseMenu.SettingsPanel.transform.Find("Pause settings save status").GetComponent<TMP_Text>();
            slider.value = .31f;
            Assert.That(MenuPreferences.Master, Is.EqualTo(.31f).Within(.001f), "Audio changes immediately.");
            Assert.That(status.text, Is.EqualTo("Saving preferences..."));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(status.text, Is.EqualTo("Saved automatically on this device"));
            slider.value = .42f;
            pauseMenu.enabled = false;
            Assert.That(status.text, Is.EqualTo("Saved automatically on this device"));
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SettingsExposeControllerPreferencesAndNavigationStaysOnTheVisiblePage()
        {
            Assert.That(MenuPreferences.StickDeadzone,Is.EqualTo(.15f).Within(.001f),"Constructing the menu must not rewrite saved controller preferences.");
            pauseMenu.Pause();pauseMenu.ShowSettingsView();pauseMenu.SelectSettingsSection(2);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(pauseMenu.CurrentSettingsSection,Is.EqualTo(2));
            var root=pauseMenu.SettingsPanel.transform;
            Assert.That(root.Find("Controller settings").gameObject.activeInHierarchy,Is.True);
            Assert.That(root.Find("Audio settings").gameObject.activeInHierarchy,Is.False);
            Assert.That(root.Find("Display settings").gameObject.activeInHierarchy,Is.False);
            var rows=root.Find("Controller settings").GetComponentsInChildren<Selectable>();
            Assert.That(rows.Length,Is.EqualTo(6),"Controller preferences must be reachable while paused.");
            // Walk the page's explicit navigation graph, independently of the hardware mouse
            // hovering a footer or a tab after another resolution test.
            var selected=root.Find("Controller settings/Controller prompt style/ControllerStyleButton").GetComponent<Selectable>();
            for(int i=0;i<6;i++)
            {
                Assert.That(rows.Contains(selected),Is.True,"Do not navigate into a hidden settings section.");
                selected=selected.navigation.selectOnDown;
            }
            Assert.That(selected.name,Is.EqualTo("ResetDefaultsButton"));
            var tabs=root.Find("Pause Settings Tabs").GetComponentsInChildren<Button>();
            Assert.That(tabs.Select(t=>t.GetComponentInChildren<TMP_Text>().text),Is.EquivalentTo(new[]{"AUDIO","DISPLAY","CONTROLLER","ACCESSIBILITY"}));
        }

        [UnityTest] public IEnumerator ControllerPreferencesApplyAndTheButtonGuideDoesNotChangeThem()
        {
            pauseMenu.Pause();pauseMenu.ShowControllerSettings();
            var root=pauseMenu.SettingsPanel.transform.Find("Controller settings");
            root.Find("ControllerVibration").GetComponent<Toggle>().isOn=false;
            root.Find("ControllerAdaptiveTriggers").GetComponent<Toggle>().isOn=false;
            root.Find("ControllerLightbar").GetComponent<Toggle>().isOn=false;
            root.Find("ControllerSpeaker").GetComponent<Toggle>().isOn=false;
            root.Find("ControllerDeadzone/Slider").GetComponent<Slider>().value=.23f;
            Assert.That(MenuPreferences.Vibration,Is.False);
            Assert.That(MenuPreferences.AdaptiveTriggers,Is.False);
            Assert.That(MenuPreferences.ControllerLightbar,Is.False);
            Assert.That(MenuPreferences.ControllerSpeaker,Is.False);
            Assert.That(MenuPreferences.StickDeadzone,Is.EqualTo(.23f).Within(.001f));
            pauseMenu.ShowControlsView();int style=MenuPreferences.ControllerStyle;
            pauseMenu.SetPauseControlsDeviceTab(1);pauseMenu.SetPauseControlsDeviceTab(2);
            var movement=pauseMenu.ControlsPanel.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Action" && t.text=="MOVE / CLIMB").transform.parent.Find("Description").GetComponent<TMP_Text>();
            Assert.That(movement.text,Does.Not.Contain("W or Up"),"The gamepad guide must describe its own directional input.");
            Assert.That(MenuPreferences.ControllerStyle,Is.EqualTo(style),"Browsing a device guide must not force its button symbols.");
            pauseMenu.ControlsPanel.transform.Find("ControllerSettingsButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(pauseMenu.CurrentView,Is.EqualTo("settings"));
            Assert.That(pauseMenu.CurrentSettingsSection,Is.EqualTo(2));
            yield return null;
            Assert.That(root.Find("ControllerSpeaker").GetComponent<Toggle>().isOn,Is.False);
        }

        [UnityTest] public IEnumerator ReturningToPauseRestoresTheHomeActionThatOpenedTheSubview()
        {
            pauseMenu.Pause();
            var settings=pauseMenu.MainPanel.transform.Find("SettingsButton").gameObject;
            EventSystem.current.SetSelectedGameObject(settings);pauseMenu.ShowSettingsView();pauseMenu.ShowMainView();
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(settings));
            var guide=pauseMenu.MainPanel.transform.Find("ControlsButton").gameObject;
            EventSystem.current.SetSelectedGameObject(guide);pauseMenu.ShowControlsView();pauseMenu.ShowMainView();
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(guide));
            pauseMenu.Resume();pauseMenu.Pause();
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("ResumeButton"),"A new pause still starts with Continue.");
            yield return null;
        }

        [UnityTest] public IEnumerator MouseAndKeyboardShareOneSettingsFocus()
        {
            pauseMenu.Pause();pauseMenu.ShowSettingsView();
            var root=pauseMenu.SettingsPanel.transform.Find("Audio settings");
            var first=root.Find("MasterSlider").GetComponent<SettingsLightFocus>();
            var second=root.Find("MusicSlider").GetComponent<SettingsLightFocus>();
            first.OnPointerEnter(new PointerEventData(EventSystem.current));
            second.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(root.Find("MusicSlider/Slider").gameObject));
            // Keyboard navigation replaces the hover selection without requiring the mouse to move.
            EventSystem.current.SetSelectedGameObject(root.Find("MasterSlider/Slider").gameObject);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(second.LeftPointer.GetComponent<Image>().color.a,Is.LessThan(.01f));
            Assert.That(first.LeftPointer.GetComponent<Image>().color.a,Is.GreaterThan(.9f));
        }
    }
}
