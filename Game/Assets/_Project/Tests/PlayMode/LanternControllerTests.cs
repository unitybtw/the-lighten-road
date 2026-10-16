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
        fuel.Initialize(6f, 10f); // 3 Embers (6 charges)
        yield return null;
    }

    [Test]
    public void RadiiMatchFurkanSpecifications()
    {
        // 50% of old 5.5 = 2.75, 200% of old 5.5 = 11.0
        Assert.That(controller.DimRadius, Is.EqualTo(2.75f).Within(0.01f));
        Assert.That(controller.FlareRadius, Is.EqualTo(11.0f).Within(0.01f));
    }

    [Test]
    public void ToggleFlareModeConsumesOneChargeAndSetsScoutTimer()
    {
        fuel.Initialize(6f, 10f);

        Assert.That(controller.IsFlared, Is.False);
        bool flared = controller.SetFlared(true);
        Assert.That(flared, Is.True);
        Assert.That(controller.IsFlared, Is.True);
        Assert.That(fuel.TotalCharges, Is.EqualTo(5), "Activating Flare scout should consume 1 charge.");
        Assert.That(controller.FlareTimer, Is.EqualTo(controller.FlareScoutDuration).Within(0.1f));

        bool dimmed = controller.SetFlared(false);
        Assert.That(dimmed, Is.True);
        Assert.That(controller.IsFlared, Is.False);
        Assert.That(controller.FlareTimer, Is.EqualTo(0f));

        // When empty, flare must be rejected
        fuel.Initialize(0f, 10f);
        bool flareRejected = controller.SetFlared(true);
        Assert.That(flareRejected, Is.False);
        Assert.That(controller.IsFlared, Is.False);
    }

    [Test]
    public void IdentifiesLowFuelThreshold()
    {
        fuel.Initialize(6f, 10f);
        Assert.That(controller.IsLowFuel, Is.False);

        fuel.Initialize(2f, 10f);
        Assert.That(controller.IsLowFuel, Is.True);

        fuel.Initialize(1f, 10f);
        Assert.That(controller.IsLowFuel, Is.True);
    }

    [UnityTest]
    public IEnumerator SimulateKeyToggleSwitchesMode()
    {
        fuel.Initialize(6f, 10f);
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

    [Test]
    public void ShadowAndFlickerSettingsConfigurable()
    {
        controller.EnableShadows = true;
        controller.ShadowIntensity = 0.9f;
        controller.ShadowSoftness = 0.4f;

        Assert.That(controller.EnableShadows, Is.True);
        Assert.That(controller.ShadowIntensity, Is.EqualTo(0.9f).Within(0.01f));
        Assert.That(controller.ShadowSoftness, Is.EqualTo(0.4f).Within(0.01f));

        Assert.That(controller.EnableFlicker, Is.True);
        Assert.That(controller.FlickerIntensity, Is.GreaterThan(0f));
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
