using TheLightenRoad.Input;
using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
