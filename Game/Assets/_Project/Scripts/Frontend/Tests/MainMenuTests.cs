using System.Collections;
using System.Linq;
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MenuPreferences.SetRealmCompleted(0,completed0);MenuPreferences.SetRealmCompleted(2,completed2);
            if (_host != null)
            {
                Object.Destroy(_host);
            }
            // Complete deferred destruction before the next menu checks for an EventSystem.
            yield return null;
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
            Assert.That(GameObject.Find("Wanderer Lantern Core").GetComponent<Image>().sprite, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FocusingAMenuChoiceDoesNotReigniteTheWorld()
        {
            _menu.SkipIntro();
            yield return null;
            var stage = _menu.GetComponent<ProceduralArchiveStage>();
            stage.TickStage(10f, 1f, Vector2.zero, Vector2.zero, false);
            var light = GameObject.Find("Hero Lantern Mid Radiance").GetComponent<Image>();
            var brightness = light.color;
            var button = _menu.GetComponentsInChildren<MenuButtonMotion>().Single(b => b.name == "Settings");
            button.OnPointerEnter(null);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.EqualTo(button.gameObject),
                "Pointer and controller selection must identify the same choice.");
            stage.TickStage(0f, 1f, Vector2.zero, Vector2.zero, false);
            Assert.That(light.color, Is.EqualTo(brightness),
                "Menu focus should be communicated by the selected item, without relighting the archive.");
            yield return new WaitForSecondsRealtime(.2f);
            MenuReadabilityTests.Capture(_menu.Canvas, 1920, 1080, "menu-art-direction-home");
        }

        [UnityTest]
        public IEnumerator ReducedMotionKeepsTheCurrentMenuFocusVisibleWithoutMovingItsText()
        {
            _menu.SkipIntro();MenuPreferences.SetReducedMotion(true);
            yield return null;
            var button=_menu.GetComponentsInChildren<MenuButtonMotion>().Single(b=>b.name=="Settings");
            button.OnDeselect(null);yield return new WaitForSecondsRealtime(.12f);
            var textPosition=button.Text.rectTransform.anchoredPosition;
            var leftPosition=button.LeftWing!=null?button.LeftWing.anchoredPosition:Vector2.zero;
            var rightPosition=button.RightWing!=null?button.RightWing.anchoredPosition:Vector2.zero;
            var idleColor=button.Text.color;
            button.OnSelect(null);button.OnPointerDown(null);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(button.Text.rectTransform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(button.Text.rectTransform.anchoredPosition,Is.EqualTo(textPosition));
            Assert.That(button.transform.localScale,Is.EqualTo(Vector3.one));
            if(button.LeftWing!=null)Assert.That(button.LeftWing.anchoredPosition,Is.EqualTo(leftPosition));
            if(button.RightWing!=null)Assert.That(button.RightWing.anchoredPosition,Is.EqualTo(rightPosition));
            Assert.That(button.WingGroup.alpha,Is.GreaterThan(.99f),"Keep the menu's existing selection ornaments visible.");
            Assert.That(button.Text.color,Is.Not.EqualTo(idleColor));
            button.OnPointerUp(null);button.OnDeselect(null);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(button.Text.rectTransform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(button.Text.rectTransform.anchoredPosition,Is.EqualTo(textPosition));
            Assert.That(button.WingGroup.alpha,Is.LessThan(.01f));
            MenuPreferences.SetReducedMotion(false);button.OnSelect(null);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(button.Text.rectTransform.localScale.x,Is.GreaterThan(1.005f),"Normal motion remains available when comfort mode is off.");
        }

        [UnityTest]
        public IEnumerator ReducedMotionKeepsAnOptionalFlameCursorStationary()
        {
            // Exercise this reusable component's optional cursor without adding one
            // to the authored menu, which currently uses selection ornaments.
            _menu.SkipIntro();MenuPreferences.SetReducedMotion(true);
            var fixture=new GameObject("Optional cursor regression",typeof(RectTransform));
            fixture.transform.SetParent(_host.transform,false);fixture.SetActive(false);
            var label=new GameObject("Fixture label",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
            label.transform.SetParent(fixture.transform,false);
            var cursor=new GameObject("Fixture flame",typeof(RectTransform),typeof(Image));
            cursor.transform.SetParent(fixture.transform,false);
            var button=fixture.AddComponent<MenuButtonMotion>();button.Text=label.GetComponent<TMPro.TMP_Text>();
            button.FlameCursorRect=cursor.GetComponent<RectTransform>();button.FlameCursorImage=cursor.GetComponent<Image>();
            button.InitializeBasePositions(Vector2.zero,Vector2.zero,new Vector2(-40,4));fixture.SetActive(true);
            yield return null;var cursorPosition=button.FlameCursorRect.anchoredPosition;
            button.OnSelect(null);button.OnPointerDown(null);yield return new WaitForSecondsRealtime(.12f);
            Assert.That(button.FlameCursorRect.anchoredPosition,Is.EqualTo(cursorPosition));
            Assert.That(button.FlameCursorRect.localScale,Is.EqualTo(Vector3.one));
            Assert.That(button.FlameCursorImage.color.a,Is.GreaterThan(.99f));
            button.OnPointerUp(null);button.OnDeselect(null);yield return new WaitForSecondsRealtime(.12f);
            Assert.That(button.FlameCursorRect.anchoredPosition,Is.EqualTo(cursorPosition));
            Assert.That(button.FlameCursorRect.localScale,Is.EqualTo(Vector3.one));
            Assert.That(button.FlameCursorImage.color.a,Is.LessThan(.01f));
            MenuPreferences.SetReducedMotion(false);
        }

        [UnityTest]
        public IEnumerator ReducedMotionSkipsTheVisualIntroButRetainsOneIgnitionSound()
        {
            // SetUp's active menu may have already run Start before the iterator
            // begins. Create a fresh disabled host so the preference predates Awake/Start.
            Object.DestroyImmediate(_host);
            MenuPreferences.SetReducedMotion(true);
            var sound=TheLightenRoad.Audio.SoundEffectManager.Instance;
            var field=typeof(TheLightenRoad.Audio.SoundEffectManager).GetField("flareIgniteClip",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var original=field.GetValue(sound);
            var clip=AudioClip.Create("Menu ignition regression",48000,1,24000,false);
            field.SetValue(sound,clip);
            try
            {
                _host=new GameObject("Reduced motion startup regression");_host.SetActive(false);
                _host.AddComponent<AudioListener>();_menu=_host.AddComponent<MainMenuController>();
                _host.SetActive(true);
                float deadline=Time.realtimeSinceStartup+2;
                while(!_menu.IntroComplete && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(_menu.IntroComplete,Is.True);
                Assert.That(sound.GetComponents<AudioSource>().Count(s=>s.clip==clip),Is.EqualTo(1),
                    "Visual comfort must not mute the lantern cue, including during scene-start grace.");
                yield return new WaitForSecondsRealtime(.08f);
                _menu.SkipIntro();yield return null;
                Assert.That(sound.GetComponents<AudioSource>().Count(s=>s.clip==clip),Is.EqualTo(1),"Skipping an already completed intro must not replay its sound.");
            }
            finally
            {
                field.SetValue(sound,original);
                foreach(var source in sound.GetComponents<AudioSource>())if(source.clip==clip){source.Stop();source.clip=null;}
                Object.Destroy(clip);
                MenuPreferences.SetReducedMotion(false);
            }
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
            _menu.ShowSettings();_menu.SelectSettingsSection(0);

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
        public IEnumerator CompletedRealms_DisplayBadgesAndRetainStatus()
        {
            yield return null;
            _menu.SkipIntro();

            MenuPreferences.SetRealmCompleted(0, true);
            MenuPreferences.SetRealmCompleted(2, true);

            _menu.ShowRealms();
            _menu.SelectRealm(0);

            var activeBadge = _menu.ActiveRealmBadgeText;
            Assert.That(activeBadge, Is.Not.Null);
            Assert.That(activeBadge.text, Does.Contain("COMPLETE"));

            var statusLabels = _menu.GetComponentsInChildren<TMPro.TMP_Text>();
            bool foundCompletedActive = false;
            bool foundCompletedInactive = false;
            foreach (var label in statusLabels)
            {
                if (label.text == "SELECTED · COMPLETE") foundCompletedActive = true;
                if (label.text == "COMPLETE") foundCompletedInactive = true;
            }
            Assert.That(foundCompletedActive, Is.True, "Selected completed realm must show both states.");
            Assert.That(foundCompletedInactive, Is.True, "Other completed realm must show its completion state.");

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
            Assert.That(_menu.CurrentSettingsSection,Is.EqualTo(-1));
            Assert.That(_menu.IsOptionsIndex,Is.True);
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
            yield return null;_menu.SkipIntro();_menu.ShowSettings();_menu.SelectSettingsSection(0);
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

        [UnityTest]
        public IEnumerator OptionsIndexBackReturnsThroughHierarchyAndNeverTargetsHiddenControls()
        {
            yield return null;_menu.SkipIntro();_menu.ShowSettings();
            Assert.That(_menu.IsOptionsIndex,Is.True);
            Assert.That(GameObject.Find("Master Volume"),Is.Null);
            _menu.SelectSettingsSection(2);
            Assert.That(_menu.IsOptionsIndex,Is.False);
            var selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.That(selected,Is.Not.Null);Assert.That(selected.activeInHierarchy,Is.True);
            _menu.BackFromSettings();Assert.That(_menu.IsOptionsIndex,Is.True);
            selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.That(selected,Is.Not.Null);Assert.That(selected.activeInHierarchy,Is.True);
            _menu.BackFromSettings();Assert.That(_menu.CurrentView,Is.EqualTo("home"));
        }

        [UnityTest]
        public IEnumerator ControllerGuideLoadsLicensedArtworkAndKeepsKeyboardBindingsAvailable()
        {
            yield return null;_menu.SkipIntro();_menu.ShowControls();_menu.SetControlsDeviceTab(1);
            var diagram=_menu.GetComponentInChildren<ControllerSchematic>();
            Assert.That(diagram,Is.Not.Null);Assert.That(diagram.SymmetricSticks,Is.False);
            Assert.That(diagram.CurrentArtwork,Is.EqualTo(Resources.Load<Sprite>("UI/ControllerArtwork/XboxSeries")));
            Assert.That(diagram.CurrentArtwork,Is.Not.Null);
            Assert.That(diagram.GetComponentInChildren<Image>().raycastTarget,Is.False);
            Assert.That(diagram.GetComponent<CanvasRenderer>(),Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(diagram.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0));
            _menu.SetControlsDeviceTab(2);Assert.That(diagram.SymmetricSticks,Is.True);
            Assert.That(diagram.CurrentArtwork,Is.EqualTo(Resources.Load<Sprite>("UI/ControllerArtwork/PlayStation5")));
            Assert.That(diagram.CurrentArtwork,Is.Not.Null);
            _menu.SetControlsDeviceTab(0);Assert.That(diagram.gameObject.activeInHierarchy,Is.False);
        }

        [UnityTest]
        public IEnumerator DualSenseTriggerOptionPersistsIndependentlyOfVibration()
        {
            yield return null;_menu.SkipIntro();_menu.ShowSettings();_menu.SelectSettingsSection(2);
            var adaptive=GameObject.Find("DualSense R2").GetComponentInChildren<Toggle>();
            var vibration=GameObject.Find("Vibration (Rumble)").GetComponentInChildren<Toggle>();
            Assert.That(adaptive,Is.Not.Null);Assert.That(vibration,Is.Not.Null);
            adaptive.isOn=false;
            Assert.That(MenuPreferences.AdaptiveTriggers,Is.False);
            Assert.That(MenuPreferences.Vibration,Is.True);
            Assert.That(adaptive.navigation.selectOnUp,Is.EqualTo(vibration));
            _menu.ShowHome();_menu.ShowSettings();_menu.SelectSettingsSection(2);
            Assert.That(adaptive.isOn,Is.False);
            adaptive.isOn=true;
        }

    }
}
