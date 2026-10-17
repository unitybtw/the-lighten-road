using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend.Tests
{
    public class MainMenuTests
    {
        private GameObject _host;
        private MainMenuController _menu;

        [SetUp]
        public void SetUp()
        {
            MenuPreferences.ResetToDefaults();
            MenuPreferences.SetSelectedRealm(0);
            _host = new GameObject("MainMenuTestHost");
            _host.AddComponent<AudioListener>();
            _menu = _host.AddComponent<MainMenuController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        [UnityTest]
        public IEnumerator AwakensInHomeView_AndBuildsProceduralArchiveStageAndFramelessUI()
        {
            yield return null;

            Assert.That(_menu.CurrentView, Is.EqualTo("home"));
            Assert.That(GameObject.Find("Enter the Archive"), Is.Not.Null);
            Assert.That(GameObject.Find("Realms & Chapters"), Is.Not.Null);
            Assert.That(GameObject.Find("Settings"), Is.Not.Null);
            Assert.That(GameObject.Find("Controls & Guide"), Is.Not.Null);
            Assert.That(GameObject.Find("Credits"), Is.Not.Null);
            Assert.That(GameObject.Find("Leave the Light"), Is.Not.Null);

            // Verify procedural 2.5D animated archive stage elements
            Assert.That(GameObject.Find("Procedural Archive Stage"), Is.Not.Null);
            Assert.That(GameObject.Find("Great Archive Lantern"), Is.Not.Null);
            Assert.That(GameObject.Find("Cloaked Wanderer Silhouette"), Is.Not.Null);
            Assert.That(GameObject.Find("Carved Ember Veins Glow"), Is.Not.Null);
            Assert.That(GameObject.Find("Ember Vein Energy Tether"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SubViews_SwitchCleanlyBetweenRealmsSettingsControlsAndCredits()
        {
            yield return null;
            _menu.SkipIntro();

            _menu.ShowRealms();
            Assert.That(_menu.CurrentView, Is.EqualTo("realms"));

            _menu.ShowSettings();
            Assert.That(_menu.CurrentView, Is.EqualTo("settings"));

            _menu.ShowControls();
            Assert.That(_menu.CurrentView, Is.EqualTo("controls"));

            _menu.ShowCredits();
            Assert.That(_menu.CurrentView, Is.EqualTo("credits"));

            _menu.ShowHome();
            Assert.That(_menu.CurrentView, Is.EqualTo("home"));
        }

        [UnityTest]
        public IEnumerator QuitView_OpensAndCancelsBackToHome()
        {
            yield return null;
            _menu.SkipIntro();

            _menu.ShowQuit();
            Assert.That(_menu.CurrentView, Is.EqualTo("quit"));

            _menu.ShowHome();
            Assert.That(_menu.CurrentView, Is.EqualTo("home"));
        }

        [UnityTest]
        public IEnumerator SelectingRealm_UpdatesTargetGameplaySceneAndPersistsChoice()
        {
            yield return null;
            _menu.SkipIntro();
            _menu.ShowRealms();

            _menu.SelectRealm(1);
            Assert.That(_menu.SelectedRealmIndex, Is.EqualTo(1));
            Assert.That(_menu.GameplayScene, Is.EqualTo("Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity"));
            Assert.That(MenuPreferences.SelectedRealmIndex, Is.EqualTo(1));

            _menu.SelectRealm(2);
            Assert.That(_menu.SelectedRealmIndex, Is.EqualTo(2));
            Assert.That(_menu.GameplayScene, Is.EqualTo("Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity"));

            _menu.SelectRealm(0);
            Assert.That(_menu.SelectedRealmIndex, Is.EqualTo(0));
            Assert.That(_menu.GameplayScene, Is.EqualTo("Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity"));
        }

        [UnityTest]
        public IEnumerator SettingsSliders_PersistMasterMusicAndEffectsLevels()
        {
            yield return null;
            _menu.SkipIntro();
            _menu.ShowSettings();

            var masterSlider = GameObject.Find("Master Volume")?.GetComponentInChildren<Slider>();
            var musicSlider = GameObject.Find("Ambient Music")?.GetComponentInChildren<Slider>();
            var sfxSlider = GameObject.Find("Sound Effects (SFX)")?.GetComponentInChildren<Slider>();

            Assert.That(masterSlider, Is.Not.Null);
            Assert.That(musicSlider, Is.Not.Null);
            Assert.That(sfxSlider, Is.Not.Null);

            masterSlider.value = 0.62f;
            musicSlider.value = 0.45f;
            sfxSlider.value = 0.80f;

            Assert.That(MenuPreferences.Master, Is.EqualTo(0.62f).Within(0.01f));
            Assert.That(MenuPreferences.Music, Is.EqualTo(0.45f).Within(0.01f));
            Assert.That(MenuPreferences.Effects, Is.EqualTo(0.80f).Within(0.01f));
            Assert.That(AudioListener.volume, Is.EqualTo(0.62f).Within(0.01f));
        }
    }
}
