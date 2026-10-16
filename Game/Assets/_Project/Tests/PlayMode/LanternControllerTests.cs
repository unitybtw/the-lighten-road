using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

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
        var lightObject = new GameObject("Test Lantern Light");
        lightObject.transform.SetParent(testObject.transform);
        lightObject.AddComponent<Light2D>();
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
        Assert.That(controller.HasActiveScout, Is.True, "The reserved ember finishes during the dimming transition.");
        controller.TickScout(controller.TransitionDuration);
        Assert.That(controller.FlareTimer, Is.Zero);

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

    [Test]
    public void LightFadesBothWaysWithinTheScoutTimer()
    {
        controller.EnableFlicker = false;
        controller.SetFlared(true);
        Assert.That(controller.LanternLight.pointLightOuterRadius, Is.EqualTo(controller.DimRadius).Within(0.01f));
        controller.TickScout(controller.TransitionDuration * 0.5f);
        Assert.That(controller.FlareBlend, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(controller.LanternLight.pointLightOuterRadius, Is.GreaterThan(controller.DimRadius).And.LessThan(controller.FlareRadius));
        controller.TickScout(controller.TransitionDuration * 0.5f);
        Assert.That(controller.FlareBlend, Is.EqualTo(1f).Within(0.001f));
        controller.TickScout(controller.FlareScoutDuration - 1.5f * controller.TransitionDuration);
        Assert.That(controller.FlareBlend, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(controller.FlareTimer, Is.EqualTo(controller.TransitionDuration * 0.5f).Within(0.001f));
        controller.TickScout(controller.TransitionDuration);
        Assert.That(controller.FlareBlend, Is.Zero);
        Assert.That(controller.IsFlared, Is.False);
        Assert.That(fuel.TotalCharges, Is.EqualTo(5));
    }

    [Test]
    public void FirstUseFadesToHalfAndLastChargeBreaksOnlyAfterSecondUseCompletes()
    {
        fuel.Initialize(2f, 10f);
        int breaks = 0;
        controller.EmberBroken += () => breaks++;
        controller.SetFlared(true);
        Assert.That(controller.ScoutEmberLevel, Is.EqualTo(1f));
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Assert.That(controller.ScoutEmberLevel, Is.EqualTo(0.75f).Within(0.001f));
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Assert.That(fuel.TotalCharges, Is.EqualTo(1));
        Assert.That(breaks, Is.Zero);
        Assert.That(controller.SetFlared(true), Is.True);
        Assert.That(fuel.IsEmpty, Is.True);
        Assert.That(controller.IsFlared, Is.True, "The final reserved charge must still grant its full scout.");
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Assert.That(controller.ScoutEmberLevel, Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(breaks, Is.Zero);
        controller.TickScout(controller.FlareScoutDuration);
        controller.TickScout(controller.FlareScoutDuration);
        Assert.That(breaks, Is.EqualTo(1));
        Assert.That(controller.HasActiveScout, Is.False);
    }

    [Test]
    public void EarlyReleaseFadesWithoutReservingAnotherCharge()
    {
        controller.SetFlared(true);
        controller.TickScout(controller.TransitionDuration);
        controller.SetFlared(false);
        Assert.That(controller.IsDimming, Is.True);
        Assert.That(controller.SetFlared(true), Is.False);
        controller.TickScout(controller.TransitionDuration * 0.5f);
        Assert.That(controller.FlareBlend, Is.GreaterThan(0f).And.LessThan(1f));
        controller.TickScout(controller.TransitionDuration);
        Assert.That(controller.HasActiveScout, Is.False);
        Assert.That(fuel.TotalCharges, Is.EqualTo(5));
        Assert.That(controller.SetFlared(true), Is.True);
        Assert.That(fuel.TotalCharges, Is.EqualTo(4));
    }

    [Test]
    public void ResetCancelsBurnWithoutPlayingABreak()
    {
        fuel.Initialize(1f, 10f);
        int breaks = 0;
        controller.EmberBroken += () => breaks++;
        controller.SetFlared(true);
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        controller.ResetToDim();
        controller.TickScout(controller.FlareScoutDuration);
        Assert.That(breaks, Is.Zero);
        Assert.That(controller.FlareBlend, Is.Zero);
        Assert.That(controller.HasActiveScout, Is.False);
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
