using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class LanternControllerTests
{
    private GameObject testObject;
    private LanternFuel fuel;
    private LanternController controller;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior originalBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();

        testObject = new GameObject("Test Lantern Controller");
        fuel = testObject.AddComponent<LanternFuel>();
        controller = testObject.AddComponent<LanternController>();
        yield return null;
    }

    [Test]
    public void ToggleFlareModeUpdatesStateAndRejectsWhenEmpty()
    {
        fuel.Initialize(50f, 100f);

        Assert.That(controller.IsFlared, Is.False);
        bool flared = controller.SetFlared(true);
        Assert.That(flared, Is.True);
        Assert.That(controller.IsFlared, Is.True);

        bool dimmed = controller.SetFlared(false);
        Assert.That(dimmed, Is.True);
        Assert.That(controller.IsFlared, Is.False);

        fuel.Initialize(0f, 100f);
        bool flareRejected = controller.SetFlared(true);
        Assert.That(flareRejected, Is.False);
        Assert.That(controller.IsFlared, Is.False);
    }

    [Test]
    public void RevertsFlareToDimWhenFuelDepleted()
    {
        fuel.Initialize(20f, 100f);
        controller.SetFlared(true);
        Assert.That(controller.IsFlared, Is.True);

        fuel.TrySpendFuel(20f);
        Assert.That(fuel.IsEmpty, Is.True);
        Assert.That(controller.IsFlared, Is.False);
    }

    [Test]
    public void IdentifiesLowFuelThreshold()
    {
        fuel.Initialize(80f, 100f);
        Assert.That(controller.IsLowFuel, Is.False);

        fuel.Initialize(25f, 100f);
        Assert.That(controller.IsLowFuel, Is.True);

        fuel.Initialize(10f, 100f);
        Assert.That(controller.IsLowFuel, Is.True);
    }

    [UnityTest]
    public IEnumerator SimulateKeyToggleSwitchesMode()
    {
        fuel.Initialize(80f, 100f);
        Assert.That(controller.IsFlared, Is.False);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
        yield return null;
        yield return null;
        Assert.That(controller.IsFlared, Is.True);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
        yield return null;
        yield return null;
        Assert.That(controller.IsFlared, Is.False);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (testObject != null)
            Object.Destroy(testObject);
        if (keyboard != null && keyboard.added)
            InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
        yield return null;
    }
}
