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
        private bool completed0,completed2;

        [SetUp]
        public void SetUp()
        {
            completed0=MenuPreferences.IsRealmCompleted(0);completed2=MenuPreferences.IsRealmCompleted(2);
            MenuPreferences.ResetToDefaults();
            MenuPreferences.SetSelectedRealm(0);
            _host = new GameObject("MainMenuTestHost");
            _host.AddComponent<AudioListener>();
            _menu = _host.AddComponent<MainMenuController>();
        }

        [TearDown]
        public void TearDown()
        {
            MenuPreferences.SetRealmCompleted(0,completed0);MenuPreferences.SetRealmCompleted(2,completed2);
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
            Assert.That(_menu.GameplayScene, Is.EqualTo(MainMenuController.DefaultGameplayScene));
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

        [UnityTest]
        public IEnumerator ConqueredRealms_DisplayBadgesAndRetainStatus()
        {
            yield return null;
            _menu.SkipIntro();

            MenuPreferences.SetRealmCompleted(0, true);
            MenuPreferences.SetRealmCompleted(2, true);

            _menu.ShowRealms();
            _menu.SelectRealm(0);

            var activeBadge = _menu.ActiveRealmBadgeText;
            Assert.That(activeBadge, Is.Not.Null);
            Assert.That(activeBadge.text, Does.Contain("[* CONQUERED]"));

            var statusLabels = _menu.GetComponentsInChildren<TMPro.TMP_Text>();
            bool foundConqueredActive = false;
            bool foundConqueredInactive = false;
            foreach (var label in statusLabels)
            {
                if (label.text.Contains("[ ACTIVE REALM - CONQUERED * ]")) foundConqueredActive = true;
                if (label.text.Contains("[ CONQUERED * ]")) foundConqueredInactive = true;
            }
            Assert.That(foundConqueredActive, Is.True, "Active conquered realm must have active conquered label.");
            Assert.That(foundConqueredInactive, Is.True, "Other conquered realm must show conquered label.");

            MenuPreferences.ResetToDefaults();
            Assert.That(MenuPreferences.IsRealmCompleted(0), Is.True,"Resetting settings must preserve progress.");
            Assert.That(MenuPreferences.IsRealmCompleted(2), Is.True);
        }

        [UnityTest]
        public IEnumerator StartGame_SuppressesInactiveSubViews_DuringDarknessTransition()
        {
            yield return null;
            _menu.SkipIntro();

            Assert.That(_menu.CurrentView, Is.EqualTo("home"));
            Assert.That(_menu.SettingsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.RealmsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.ControlsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.CreditsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.QuitGroup.alpha, Is.EqualTo(0f));

            _menu.LoadSceneOnPlay = false;
            _menu.Play();
            yield return null;

            // During transition, inactive subviews must NEVER become visible
            Assert.That(_menu.SettingsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.RealmsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.ControlsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.CreditsGroup.alpha, Is.EqualTo(0f));
            Assert.That(_menu.QuitGroup.alpha, Is.EqualTo(0f));
        }
        [UnityTest]
        public IEnumerator SettingsCategoriesAndResetConfirmationPreserveChangesUntilConfirmed()
        {
            yield return null;_menu.SkipIntro();_menu.ShowSettings();
            Assert.That(_menu.CurrentSettingsSection,Is.EqualTo(0));
            _menu.SelectSettingsSection(1);
            Assert.That(_menu.CurrentSettingsSection,Is.EqualTo(1));
            var panels=_menu.GetComponentsInChildren<RectTransform>(true);
            var audio=System.Array.Find(panels,p=>p.name=="Audio settings");
            var display=System.Array.Find(panels,p=>p.name=="Display settings");
            Assert.That(audio.gameObject.activeSelf,Is.False);Assert.That(display.gameObject.activeSelf,Is.True);
            MenuPreferences.SetAudio(.3f,.4f,.5f);
            _menu.RequestSettingsReset();Assert.That(_menu.IsSettingsResetOpen,Is.True);
            _menu.CancelSettingsReset();Assert.That(_menu.IsSettingsResetOpen,Is.False);
            Assert.That(MenuPreferences.Master,Is.EqualTo(.3f).Within(.01f));
            _menu.RequestSettingsReset();_menu.ResetDefaults();
            Assert.That(_menu.IsSettingsResetOpen,Is.False);
            Assert.That(MenuPreferences.Master,Is.EqualTo(.85f).Within(.01f));
        }

        [UnityTest]
        public IEnumerator VSyncControlsFrameLimitAndAudioChangesReceiveSaveFeedback()
        {
            yield return null;_menu.SkipIntro();_menu.ShowSettings();
            MenuPreferences.SetVSync(true);int before=MenuPreferences.TargetFPS;
            _menu.SetSettingsFrameRate(120);Assert.That(MenuPreferences.TargetFPS,Is.EqualTo(before));
            MenuPreferences.SetVSync(false);_menu.SetSettingsFrameRate(120);
            Assert.That(MenuPreferences.TargetFPS,Is.EqualTo(120));
            var slider=GameObject.Find("Master Volume").GetComponentInChildren<Slider>();
            slider.value=.42f;yield return new WaitForSecondsRealtime(.65f);
            var labels=_menu.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.That(System.Array.Exists(labels,t=>t.text=="All changes saved"),Is.True);
            _menu.ShowHome();Assert.That(_menu.CurrentView,Is.EqualTo("home"));
        }

    }
}
