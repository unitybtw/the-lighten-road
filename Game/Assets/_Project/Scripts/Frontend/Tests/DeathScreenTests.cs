using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class DeathScreenTests
    {
        private GameObject player, host, pauseHost;
        private PlayerRespawnController recovery;
        private DeathScreenOverlay screen;
        private Keyboard keyboard, previousKeyboard;
        private Mouse mouse, previousMouse;
        private Gamepad pad, previousPad;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private bool reduced, hadDeathCount;
        private int deathCount;
        private ControllerPromptStyle style;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            reduced = MenuPreferences.ReducedMotion;
            MenuPreferences.SetReducedMotion(true);
            style = ControllerGlyphService.ConfiguredStyle;
            ControllerGlyphService.ConfiguredStyle = ControllerPromptStyle.Keyboard;
            hadDeathCount = PlayerPrefs.HasKey("TheLightenRoad.TotalDeaths");
            deathCount = PlayerPrefs.GetInt("TheLightenRoad.TotalDeaths");
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current; previousPad = Gamepad.current;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
            player = new GameObject("Recovery actor", typeof(PlayerRespawnController));
            recovery = player.GetComponent<PlayerRespawnController>();
            recovery.VoidKillY = -10000; recovery.KillOnFuelDepleted = false;
            recovery.AutoRespawnDelay = .2f;
            host = new GameObject("Archive death screen", typeof(DeathScreenOverlay));
            screen = host.GetComponent<DeathScreenOverlay>(); screen.SetTarget(recovery);
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (player != null) Object.DestroyImmediate(player);
            if (pauseHost != null) Object.DestroyImmediate(pauseHost);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(pad);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            ControllerGlyphService.ConfiguredStyle = style;
            MenuPreferences.SetReducedMotion(reduced);
            if (hadDeathCount) PlayerPrefs.SetInt("TheLightenRoad.TotalDeaths", deathCount); else PlayerPrefs.DeleteKey("TheLightenRoad.TotalDeaths");
            PlayerPrefs.Save(); Time.timeScale = 1;
            yield return null; yield return null;
        }
        private IEnumerator Ready()
        { float deadline = Time.realtimeSinceStartup + 3; while (!screen.IsArmed && Time.realtimeSinceStartup < deadline) yield return null; Assert.That(screen.IsArmed, Is.True); }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }

        [UnityTest] public IEnumerator RecoveryWaitsForThePlayerAndPreviewDoesNotCountAsDeath()
        {
            int count = screen.TotalDeaths;
            screen.PreviewDeath(DeathReason.Void);
            Assert.That(screen.TotalDeaths, Is.EqualTo(count));
            screen.HandlePlayerRespawned();
            yield return new WaitForSecondsRealtime(.15f);
            recovery.Kill(DeathReason.Hazard);
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(recovery.State, Is.EqualTo(RespawnState.Dead));
            Assert.That(recovery.AutoRespawnEnabled, Is.False);
            Assert.That(recovery.AllowManualRespawnInput, Is.False);
            Assert.That(screen.TotalDeaths, Is.EqualTo(count + 1));
            Assert.That(GameControls.GameplayAllowed, Is.False);
            yield return Ready(); yield return Press(Key.Enter);
            Assert.That(recovery.State, Is.EqualTo(RespawnState.Alive));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.IsVisible, Is.False);
            Assert.That(GameControls.GameplayAllowed, Is.True);
        }
        [UnityTest] public IEnumerator HeldSubmitCannotSkipTheRevealOrChooseRetry()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null; yield return null;
            recovery.Kill(DeathReason.Darkness);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(screen.IsArmed, Is.False);
            Assert.That(recovery.State, Is.EqualTo(RespawnState.Dead));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Ready();
            yield return Press(Key.Enter);
            Assert.That(recovery.State, Is.EqualTo(RespawnState.Alive));
        }
        [UnityTest] public IEnumerator RecoveryInTheDeathEventFrameNeverLeavesAnInvisibleInputBlocker()
        {
            recovery.Kill(DeathReason.Hazard);
            recovery.Respawn();
            Assert.That(screen.IsVisible, Is.False);
            Assert.That(screen.Canvas.enabled, Is.False);
            Assert.That(recovery.AutoRespawnEnabled, Is.True);
            Assert.That(recovery.AllowManualRespawnInput, Is.True);
            yield return null; yield return null;
            Assert.That(GameControls.GameplayAllowed, Is.True);
        }
        [UnityTest] public IEnumerator AStationaryPointerCanClickRetry()
        {
            screen.PreviewDeath(DeathReason.Void); yield return Ready();
            var rect = host.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Button_0");
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(screen.IsVisible, Is.False, "Clicking must work without moving the mouse on the click frame.");
        }
        [UnityTest] public IEnumerator ControllerBackSelectsTitleWithoutLeavingOrOpeningPause()
        {
            pauseHost = new GameObject("Recovery pause test", typeof(PauseMenuController));
            recovery.Kill(DeathReason.Hazard); yield return Ready();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            yield return null; yield return null;
            Assert.That(screen.SelectedButtonIndex, Is.EqualTo(1));
            Assert.That(PauseMenuController.IsPaused, Is.False);
            Assert.That(screen.IsVisible, Is.True);
            Assert.That(recovery.State, Is.Not.EqualTo(RespawnState.Alive));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null; yield return null;
            Assert.That(recovery.State, Is.EqualTo(RespawnState.Alive));
        }
        [UnityTest] public IEnumerator DisablingTheScreenRestoresTheControllersOriginalRecoveryPolicy()
        {
            recovery.AutoRespawnEnabled = false; recovery.AllowManualRespawnInput = true;
            recovery.Kill(DeathReason.Void);
            Assert.That(recovery.AllowManualRespawnInput, Is.False);
            screen.enabled = false;
            Assert.That(recovery.AutoRespawnEnabled, Is.False);
            Assert.That(recovery.AllowManualRespawnInput, Is.True);
            Assert.That(DeathScreenOverlay.IsAnyVisible, Is.False);
            recovery.Respawn(); yield return null; yield return null;
            Assert.That(GameControls.GameplayAllowed, Is.True);
        }
        [UnityTest] public IEnumerator MenuInputSuspendsOnlyEnabledLegacyPollersAndRestoresThemAfterTheFade()
        {
            var type = System.Type.GetType("TheLightenRoad.Interaction.EmberVein, TheLightenRoad.Interaction");
            Assert.That(type, Is.Not.Null);
            var enabledHost = new GameObject("Enabled legacy poller");
            var disabledHost = new GameObject("Already disabled legacy poller");
            try
            {
                var enabledPoller = (MonoBehaviour)enabledHost.AddComponent(type);
                var disabledPoller = (MonoBehaviour)disabledHost.AddComponent(type);
                disabledPoller.enabled = false;
                screen.PreviewDeath(DeathReason.Hazard);
                Assert.That(enabledPoller.enabled, Is.False);
                Assert.That(disabledPoller.enabled, Is.False);
                yield return Ready();
                yield return Press(Key.Enter);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(enabledPoller.enabled, Is.True);
                Assert.That(disabledPoller.enabled, Is.False, "The overlay must preserve another owner's disabled state.");
            }
            finally { Object.DestroyImmediate(enabledHost); Object.DestroyImmediate(disabledHost); }
        }
        [UnityTest] public IEnumerator DeathArchetypesShareTheMenuTypefaceAndFitSupportedCanvasShapes()
        {
            foreach (var reason in new[] { DeathReason.Darkness, DeathReason.Void, DeathReason.Hazard })
            {
                screen.PreviewDeath(reason); yield return Ready();
                var title = host.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Title");
                Assert.That(title.font, Is.SameAs(ArchiveMenuStyle.Serif));
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                {
                    MenuReadabilityTests.Capture(screen.Canvas, size.x, size.y, "death-" + reason + "-" + size.x + "x" + size.y);
                    host.SendMessage("LateUpdate"); Canvas.ForceUpdateCanvases();
                    var bounds = ((RectTransform)screen.Canvas.transform).rect;
                    foreach (var label in host.GetComponentsInChildren<TMP_Text>().Where(t => t.gameObject.activeInHierarchy))
                    {
                        var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                        var low = screen.Canvas.transform.InverseTransformPoint(corners[0]);
                        var high = screen.Canvas.transform.InverseTransformPoint(corners[2]);
                        Assert.That(low.x, Is.GreaterThanOrEqualTo(bounds.xMin), label.name);
                        Assert.That(high.x, Is.LessThanOrEqualTo(bounds.xMax), label.name);
                        Assert.That(low.y, Is.GreaterThanOrEqualTo(bounds.yMin), label.name);
                        Assert.That(high.y, Is.LessThanOrEqualTo(bounds.yMax), label.name);
                        Assert.That(label.isTextOverflowing, Is.False, label.name);
                    }
                }
            }
        }
    }
}
