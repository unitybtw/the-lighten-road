using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LanternHUDTests
{
    private GameObject playerObject;
    private GameObject hudObject;
    private LanternFuel fuel;
    private LanternController controller;
    private LanternHUD hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        playerObject = new GameObject("Test HUD Lantern");
        fuel = playerObject.AddComponent<LanternFuel>();
        controller = playerObject.AddComponent<LanternController>();
        fuel.Initialize(80f, 100f);

        hudObject = new GameObject("Test HUD");
        hud = hudObject.AddComponent<LanternHUD>();
        hud.SetTarget(controller);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.Destroy(hudObject);
        Object.Destroy(playerObject);
        yield return null;
    }

    private void Step(float seconds, float dt = 1f / 60f)
    {
        for (float t = 0f; t < seconds; t += dt)
            hud.Tick(dt, Time.unscaledTime + t);
    }

    [Test]
    public void BuildsOverlayCanvasAndShowsCurrentFuel()
    {
        Step(0.1f);
        Assert.That(hud.Canvas, Is.Not.Null);
        Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.Target, Is.SameAs(controller));
        Assert.That(hud.PercentText, Is.EqualTo("80%"));
        Assert.That(hud.ModeText, Is.EqualTo("DIM"));
        Assert.That(hud.DisplayedFill, Is.EqualTo(0.8f).Within(0.01f));
    }

    [Test]
    public void DisplayedFillEasesDownWhileTrailLingers()
    {
        Step(0.1f);
        fuel.TrySpendFuel(40f);
        Step(0.2f);

        Assert.That(hud.TrailFill, Is.GreaterThan(hud.DisplayedFill + 0.05f),
            "Trail should linger above the main fill right after a drain.");

        Step(3f);
        Assert.That(hud.DisplayedFill, Is.EqualTo(0.4f).Within(0.01f));
        Assert.That(hud.TrailFill, Is.EqualTo(hud.DisplayedFill).Within(0.01f));
    }

    [Test]
    public void FlareModeUpdatesLabel()
    {
        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE"));

        controller.SetFlared(false);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("DIM"));
    }

    [Test]
    public void LowAndEmptyFuelAreFlagged()
    {
        fuel.Initialize(15f, 100f);
        Step(0.05f);
        Assert.That(hud.IsShowingLowFuel, Is.True);
        StringAssert.Contains("LOW", hud.ModeText);

        fuel.Initialize(0f, 100f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("EMPTY"));
        Assert.That(hud.PercentText, Is.EqualTo("0%"));
    }

    [Test]
    public void FuelGainShowsPickupFeedback()
    {
        Step(0.1f);
        Assert.That(hud.IsShowingPickup, Is.False);

        fuel.AddFuel(20f);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.True);

        Step(1.5f);
        Assert.That(hud.IsShowingPickup, Is.False);
        Assert.That(hud.DisplayedFill, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void LegacyDebugOverlayIsOffByDefault()
    {
        Assert.That(controller.ShowDebugOverlay, Is.False);
    }
}
