using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Frontend;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class ControllerSupportTests
    {
        private GameObject menuGo;
        private MainMenuController menu;
        private GameObject pauseGo;
        private PauseMenuController pauseMenu;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            menuGo = new GameObject("Test Main Menu");
            menu = menuGo.AddComponent<MainMenuController>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (menuGo != null) Object.Destroy(menuGo);
            if (pauseGo != null) Object.Destroy(pauseGo);
            MenuPreferences.ResetToDefaults();
            yield return null;
        }

        [Test]
        public void ControllerGlyphService_GeneratesAccuratePlayStationGlyphs()
        {
            ControllerGlyphService.ConfiguredStyle = ControllerPromptStyle.PlayStation;
            Assert.That(ControllerGlyphService.ActiveStyle, Is.EqualTo(ControllerPromptStyle.PlayStation));

            Assert.That(ControllerGlyphService.GetConfirmPrompt(), Does.Contain("CROSS"));
            Assert.That(ControllerGlyphService.GetCancelPrompt(), Does.Contain("CIRCLE"));
            Assert.That(ControllerGlyphService.GetJumpPrompt(), Is.EqualTo("CROSS"));
            Assert.That(ControllerGlyphService.GetFlarePrompt(), Does.Contain("SQUARE"));
            Assert.That(ControllerGlyphService.GetTabCyclePrompt(), Is.EqualTo("L1 / R1"));
            Assert.That(ControllerGlyphService.GetControllerName(), Does.Contain("PlayStation"));
        }

        [Test]
        public void ControllerGlyphService_GeneratesAccurateXboxGlyphs()
        {
            ControllerGlyphService.ConfiguredStyle = ControllerPromptStyle.Xbox;
            Assert.That(ControllerGlyphService.ActiveStyle, Is.EqualTo(ControllerPromptStyle.Xbox));

            Assert.That(ControllerGlyphService.GetConfirmPrompt(), Does.Contain("A"));
            Assert.That(ControllerGlyphService.GetCancelPrompt(), Does.Contain("B"));
            Assert.That(ControllerGlyphService.GetJumpPrompt(), Is.EqualTo("A"));
            Assert.That(ControllerGlyphService.GetFlarePrompt(), Does.Contain("X"));
            Assert.That(ControllerGlyphService.GetTabCyclePrompt(), Is.EqualTo("LB / RB"));
            Assert.That(ControllerGlyphService.GetControllerName(), Does.Contain("Xbox"));
        }

        [Test]
        public void ControllerGlyphService_GeneratesAccurateKeyboardGlyphs()
        {
            ControllerGlyphService.ConfiguredStyle = ControllerPromptStyle.Keyboard;
            Assert.That(ControllerGlyphService.ActiveStyle, Is.EqualTo(ControllerPromptStyle.Keyboard));

            Assert.That(ControllerGlyphService.GetConfirmPrompt(), Does.Contain("ENTER"));
            Assert.That(ControllerGlyphService.GetCancelPrompt(), Does.Contain("ESC"));
            Assert.That(ControllerGlyphService.GetJumpPrompt(), Is.EqualTo("SPACE"));
            Assert.That(ControllerGlyphService.GetFlarePrompt(), Does.Contain("F"));
            Assert.That(ControllerGlyphService.GetTabCyclePrompt(), Is.EqualTo("Q / E"));
        }

        [Test]
        public void ControllerGlyphService_LoadsAuthenticZackslySprites()
        {
            var psCross = ControllerGlyphService.GetGlyphSprite("Cross", ControllerPromptStyle.PlayStation);
            Assert.That(psCross, Is.Not.Null, "PlayStation Cross sprite should load from Resources");

            var psCircle = ControllerGlyphService.GetGlyphSprite("Circle", ControllerPromptStyle.PlayStation);
            Assert.That(psCircle, Is.Not.Null, "PlayStation Circle sprite should load from Resources");

            var xboxA = ControllerGlyphService.GetGlyphSprite("A", ControllerPromptStyle.Xbox);
            Assert.That(xboxA, Is.Not.Null, "Xbox A sprite should load from Resources");

            var xboxX = ControllerGlyphService.GetGlyphSprite("X", ControllerPromptStyle.Xbox);
            Assert.That(xboxX, Is.Not.Null, "Xbox X sprite should load from Resources");

            var psJump = ControllerGlyphService.GetActionSprite(GlyphAction.Jump, ControllerPromptStyle.PlayStation);
            Assert.That(psJump, Is.EqualTo(psCross), "PlayStation Jump should resolve to Cross sprite");

            var xboxJump = ControllerGlyphService.GetActionSprite(GlyphAction.Jump, ControllerPromptStyle.Xbox);
            Assert.That(xboxJump, Is.EqualTo(xboxA), "Xbox Jump should resolve to A sprite");
        }

        [Test]
        public void MenuPreferences_PersistsControllerSettingsAndDefaults()
        {
            MenuPreferences.SetControllerStyle(2); // PlayStation
            Assert.That(MenuPreferences.ControllerStyle, Is.EqualTo(2));
            Assert.That(ControllerGlyphService.ConfiguredStyle, Is.EqualTo(ControllerPromptStyle.PlayStation));

            MenuPreferences.SetVibration(false);
            Assert.That(MenuPreferences.Vibration, Is.False);
            Assert.That(ControllerGlyphService.VibrationEnabled, Is.False);

            MenuPreferences.SetStickDeadzone(0.22f);
            Assert.That(MenuPreferences.StickDeadzone, Is.EqualTo(0.22f).Within(0.01f));

            MenuPreferences.ResetToDefaults();
            Assert.That(MenuPreferences.ControllerStyle, Is.EqualTo(0)); // Auto
            Assert.That(MenuPreferences.Vibration, Is.True);
            Assert.That(MenuPreferences.StickDeadzone, Is.EqualTo(0.15f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator MainMenuSettingsView_ControlsSectionCanBeSelectedAndToggled()
        {
            menu.SkipIntro();
            menu.ShowSettings();
            yield return null;

            menu.SelectSettingsSection(2); // Controls section
            Assert.That(menu.CurrentSettingsSection, Is.EqualTo(2));

            var panels = menu.GetComponentsInChildren<RectTransform>(true);
            var controlsPanel = System.Array.Find(panels, p => p.name == "Controls settings");
            var audioPanel = System.Array.Find(panels, p => p.name == "Audio settings");

            Assert.That(controlsPanel, Is.Not.Null);
            Assert.That(controlsPanel.gameObject.activeSelf, Is.True);
            Assert.That(audioPanel.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator MainMenuController_ControlsSubViewSwitchesDeviceTabs()
        {
            menu.SkipIntro();
            menu.ShowControls();
            yield return null;

            Assert.That(menu.CurrentView, Is.EqualTo("controls"));

            // Switch to Xbox tab
            menu.SetControlsDeviceTab(1);
            var labels = menu.GetComponentsInChildren<TMPro.TMP_Text>(true);
            bool hasXboxJump = System.Array.Exists(labels, t => t.text.Contains("[ A ]"));
            Assert.That(hasXboxJump, Is.True, "Controls view should show Xbox [ A ] jump button");

            // Switch to PlayStation tab
            menu.SetControlsDeviceTab(2);
            labels = menu.GetComponentsInChildren<TMPro.TMP_Text>(true);
            bool hasPSJump = System.Array.Exists(labels, t => t.text.Contains("[ CROSS ]"));
            Assert.That(hasPSJump, Is.True, "Controls view should show PlayStation [ CROSS ] jump button");
        }

        [UnityTest]
        public IEnumerator PauseMenuController_ControlsPanelSwitchesTabsAndPrompts()
        {
            pauseGo = new GameObject("Test Pause Menu");
            pauseMenu = pauseGo.AddComponent<PauseMenuController>();
            yield return null;

            pauseMenu.Pause();
            pauseMenu.ShowControlsView();
            yield return null;

            Assert.That(pauseMenu.ControlsPanel.activeSelf, Is.True);

            // Switch to PlayStation tab
            pauseMenu.SetPauseControlsDeviceTab(2);
            var labels = pauseGo.GetComponentsInChildren<TMPro.TMP_Text>(true);
            bool hasPSCross = System.Array.Exists(labels, t => t.text.Contains("CROSS"));
            Assert.That(hasPSCross, Is.True, "Pause controls panel should display PlayStation CROSS glyph");

            // Switch to Xbox tab
            pauseMenu.SetPauseControlsDeviceTab(1);
            labels = pauseGo.GetComponentsInChildren<TMPro.TMP_Text>(true);
            bool hasXboxA = System.Array.Exists(labels, t => t.text.Contains("[ A ]"));
            Assert.That(hasXboxA, Is.True, "Pause controls panel should display Xbox [ A ] glyph");
        }

        [UnityTest]
        public IEnumerator DeathScreenOverlay_CancelInputSelectsReturnToArchive()
        {
            var deathGo = new GameObject("TestDeathOverlay");
            var death = deathGo.AddComponent<DeathScreenOverlay>();
            yield return null;

            death.PreviewDeath(DeathReason.Hazard);
            yield return null;

            Assert.That(death.SelectedButtonIndex, Is.EqualTo(0));
            death.SetSelectedButton(1);
            Assert.That(death.SelectedButtonIndex, Is.EqualTo(1));

            Object.Destroy(deathGo);
        }

        [UnityTest]
        public IEnumerator ExportControllerScreenshots()
        {
            const string outDir = "/Users/siracsimsek/.gemini/antigravity/brain/8f3d549c-7f35-4ab1-8e4f-82e26f75a440/";
            System.IO.Directory.CreateDirectory(outDir);

            var camGo = new GameObject("TestCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.008f, 0.012f, 0.024f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            var canvas = menu.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
            }

            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;

            menu.SkipIntro();
            yield return null;

            // 1. Settings -> Controls Tab
            menu.ShowSettings();
            menu.SelectSettingsSection(2);
            for (int f = 0; f < 10; f++) yield return null;
            SaveCamShot(cam, rt, System.IO.Path.Combine(outDir, "controller_settings_controls_tab.png"));

            // 2. Controls Sub-View -> Xbox
            menu.ShowControls();
            menu.SetControlsDeviceTab(1);
            for (int f = 0; f < 10; f++) yield return null;
            SaveCamShot(cam, rt, System.IO.Path.Combine(outDir, "controller_guide_xbox.png"));

            // 3. Controls Sub-View -> PlayStation
            menu.SetControlsDeviceTab(2);
            for (int f = 0; f < 10; f++) yield return null;
            SaveCamShot(cam, rt, System.IO.Path.Combine(outDir, "controller_guide_playstation.png"));

            // 4. Pause Menu -> Controls & Gamepad (Isolate from Main Menu)
            if (canvas != null) canvas.gameObject.SetActive(false);

            var pauseObj = new GameObject("PauseMenuTest");
            var pause = pauseObj.AddComponent<PauseMenuController>();
            yield return null;

            var pCanvas = pauseObj.GetComponentInChildren<Canvas>();
            if (pCanvas != null)
            {
                pCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                pCanvas.worldCamera = cam;
                pCanvas.planeDistance = 10f;
            }

            pause.Pause();
            pause.ShowControlsView();
            pause.SetPauseControlsDeviceTab(2);
            for (int f = 0; f < 10; f++) yield return null;
            SaveCamShot(cam, rt, System.IO.Path.Combine(outDir, "pause_menu_controls_gamepad.png"));

            pause.Resume();
            Object.Destroy(pauseObj);
            yield return null;

            // 5. Death Screen with PlayStation Prompt (Isolate from Pause Menu)
            ControllerGlyphService.ConfiguredStyle = ControllerPromptStyle.PlayStation;
            var deathGo = new GameObject("DeathOverlayTest");
            var death = deathGo.AddComponent<DeathScreenOverlay>();
            yield return null;
            var dCanvas = deathGo.GetComponentInChildren<Canvas>();
            if (dCanvas != null)
            {
                dCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                dCanvas.worldCamera = cam;
                dCanvas.planeDistance = 10f;
            }
            death.PreviewDeath(DeathReason.Darkness);
            for (int f = 0; f < 90; f++) yield return null;
            SaveCamShot(cam, rt, System.IO.Path.Combine(outDir, "death_screen_playstation_prompt.png"));
            Object.Destroy(deathGo);

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(camGo);
        }

        private static void SaveCamShot(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
