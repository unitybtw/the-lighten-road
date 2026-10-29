using TheLightenRoad.Input;
using System.Collections;
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
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                pauseMenu.Pause();
                pauseMenu.RequestRestartChamber();
                // Pump one complete input frame. An Editor update can otherwise consume/reset a
                // queued synthetic Escape before the paused runtime's Update gets its turn.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                Assert.That(keyboard.escapeKey.wasPressedThisFrame, Is.True);
                pauseMenu.SendMessage("Update");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.That(pauseMenu.IsConfirmationOpen, Is.False);
                Assert.That(pauseMenu.CurrentView, Is.EqualTo("main"));
                Assert.That(Time.timeScale, Is.Zero);
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                pauseMenu.SendMessage("Update");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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
            var slider = pauseMenu.SettingsPanel.transform.Find("MasterSlider/Slider").GetComponent<Slider>();
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
    }
}
