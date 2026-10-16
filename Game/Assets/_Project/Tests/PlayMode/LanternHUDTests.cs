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
        fuel.Initialize(6f, 10f); // 3 Embers (6 charges)

        hudObject = new GameObject("Test HUD");
        hud = hudObject.AddComponent<LanternHUD>();
        hud.SetTarget(controller);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (hudObject != null)
            Object.Destroy(hudObject);
        if (playerObject != null)
            Object.Destroy(playerObject);
        yield return null;
    }

    private void Step(float seconds, float dt = 1f / 60f)
    {
        for (float t = 0f; t < seconds; t += dt)
            hud.Tick(dt, Time.unscaledTime + t);
    }

    [Test]
    public void BuildsOverlayCanvasAndInitializes5EmberSlots()
    {
        Step(0.1f);
        Assert.That(hud.Canvas, Is.Not.Null);
        Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.Target, Is.SameAs(controller));
        Assert.That(hud.MaxDisplaySlots, Is.EqualTo(5));
    }

    [Test]
    public void DisplaysFullHalfAndEmptyEmberSpritesCorrectly()
    {
        // 5 charges = 2 Full embers (slots 0, 1), 1 Half ember (slot 2), 2 Empty sockets (slots 3, 4)
        fuel.Initialize(5f, 10f);
        Step(0.05f);

        Assert.That(hud.GetSlotSprite(0), Is.EqualTo(PixelSprites.EmberFull));
        Assert.That(hud.GetSlotColor(0), Is.EqualTo(Color.white));

        Assert.That(hud.GetSlotSprite(1), Is.EqualTo(PixelSprites.EmberFull));
        Assert.That(hud.GetSlotColor(1), Is.EqualTo(Color.white));

        Assert.That(hud.GetSlotSprite(2), Is.EqualTo(PixelSprites.EmberHalf));
        Assert.That(hud.GetSlotColor(2), Is.EqualTo(Color.white));

        Assert.That(hud.GetSlotColor(3), Is.EqualTo(Color.clear));
        Assert.That(hud.GetSlotColor(4), Is.EqualTo(Color.clear));
    }

    [Test]
    public void DisplaysOverflowBadgeWhenEmbersExceed5()
    {
        // 14 charges = 7 embers -> 5 displayed slots, +2 overflow badge
        fuel.Initialize(14f, 14f);
        Step(0.05f);
        Assert.That(hud.OverflowText, Is.EqualTo("+2"));

        // 18 charges = 9 embers -> 5 displayed slots, +4 overflow badge (designer Furkan's exact example)
        fuel.Initialize(18f, 18f);
        Step(0.05f);
        Assert.That(hud.OverflowText, Is.EqualTo("+4"));

        // 10 charges = 5 embers -> exactly 5 displayed slots, no overflow badge
        fuel.Initialize(10f, 10f);
        Step(0.05f);
        Assert.That(hud.OverflowText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void FlareScoutModeUpdatesModeLabel()
    {
        fuel.Initialize(6f, 10f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE"));

        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE ACTIVE"));

        controller.SetFlared(false);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("DIMMING"));
        controller.TickScout(controller.TransitionDuration);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE"));
    }

    [Test]
    public void EmptyEmberStateDisplaysEnglishWarning()
    {
        fuel.Initialize(0f, 10f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("NO EMBERS"));
        for (int i = 0; i < 5; i++)
        {
            Assert.That(hud.GetSlotColor(i), Is.EqualTo(Color.clear));
        }
    }

    [Test]
    public void EmberGainShowsPickupFeedback()
    {
        fuel.Initialize(4f, 10f);
        Step(0.1f);
        Assert.That(hud.IsShowingPickup, Is.False);

        // Add 1 ember (2 charges)
        fuel.AddFuel(2f);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.True);

        Step(1.5f);
        Assert.That(hud.IsShowingPickup, Is.False);
    }

    [Test]
    public void SingleChargeGainIsNotMisreportedAsAFullEmber()
    {
        Step(0.1f);
        fuel.AddFuel(1f);
        Step(0.05f);
        Assert.That(hud.PickupText, Is.EqualTo("+1 CHARGE"));
        Assert.That(hud.DisplayedCharges, Is.EqualTo(7));
        Assert.That(hud.GetSlotSprite(3), Is.EqualTo(PixelSprites.EmberHalf));
    }

    [Test]
    public void ScoutMeterReflectsControllerWithoutSpendingAdditionalCharges()
    {
        Step(0.1f);
        controller.SetFlared(true);
        float afterActivation = fuel.CurrentFuel;
        Step(0.1f);
        Assert.That(hud.ScoutProgress, Is.EqualTo(controller.FlareTimer / controller.FlareScoutDuration).Within(0.001f));
        Assert.That(hud.ScoutProgress, Is.GreaterThan(0f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(afterActivation));
        Assert.That(hud.IsShowingSpend, Is.True);

        controller.SetFlared(false);
        controller.TickScout(controller.TransitionDuration);
        Step(0.05f);
        Assert.That(hud.ScoutProgress, Is.Zero);
    }

    [Test]
    public void EmberWearTracksTwoUsesAndBreaksOnceAtZero()
    {
        fuel.Initialize(2f, 10f);
        Step(0.05f);
        controller.SetFlared(true);
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Step(0.05f);
        Assert.That(hud.ActiveEmberLevel, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(hud.GetSlotSprite(0), Is.SameAs(PixelSprites.EmberFull));
        Assert.That(hud.GetSlotColor(0), Is.Not.EqualTo(Color.white));
        Assert.That(hud.ActiveCrackAmount, Is.Zero);
        controller.TickScout(controller.FlareScoutDuration);
        Step(0.05f);
        Assert.That(hud.GetSlotSprite(0), Is.SameAs(PixelSprites.EmberHalf));
        Assert.That(hud.BreakFeedbackCount, Is.Zero);
        controller.SetFlared(true);
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE ACTIVE"));
        Assert.That(hud.GetSlotCrackAlpha(0), Is.GreaterThan(0f));
        Assert.That(hud.ActiveEmberLevel, Is.EqualTo(0.25f).Within(0.001f));
        controller.TickScout(controller.FlareScoutDuration);
        Step(0.7f);
        Assert.That(hud.GetSlotColor(0), Is.EqualTo(Color.clear));
        Assert.That(hud.BreakFeedbackCount, Is.EqualTo(1));
        Assert.That(hud.ModeText, Is.EqualTo("NO EMBERS"));
    }

    [Test]
    public void PickupDuringBurnKeepsOneCorrectlyFadingEmber()
    {
        fuel.Initialize(2f, 10f);
        Step(0.05f);
        controller.SetFlared(true);
        controller.TickScout(controller.FlareScoutDuration * 0.5f);
        Step(0.05f);
        fuel.AddEmbers(1);
        Step(0.05f);
        Assert.That(controller.ScoutEmberIndex, Is.EqualTo(1));
        Assert.That(hud.GetSlotColor(0), Is.EqualTo(Color.white));
        Assert.That(hud.GetSlotColor(1), Is.Not.EqualTo(Color.white));
        Assert.That(hud.ActiveEmberLevel, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(hud.PickupText, Is.EqualTo("+1 EMBER"));
    }

    [Test]
    public void DisablingHudHidesCanvasAndReenableDoesNotFakePickup()
    {
        Step(0.1f);
        hud.enabled = false;
        Assert.That(hud.Canvas.enabled, Is.False);
        fuel.AddFuel(2f);
        hud.enabled = true;
        Step(0.1f);
        Assert.That(hud.Canvas.enabled, Is.True);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(8));
        Assert.That(hud.IsShowingPickup, Is.False);
    }

    [Test]
    public void ReducedMotionKeepsInventoryAndPickupFeedbackReadable()
    {
        hud.ReduceMotion = true;
        Step(0.1f);
        fuel.AddFuel(2f);
        Step(0.1f);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(8));
        Assert.That(hud.IsShowingPickup, Is.True);
        Assert.That(hud.PickupText, Is.EqualTo("+1 EMBER"));
        Assert.That(hud.Canvas.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1f));
    }

    [Test]
    public void RetargetingClearsPreviousInventoryFeedback()
    {
        Step(0.1f);
        fuel.AddFuel(2f);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.True);
        hud.SetTarget(controller);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.False);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(8));
    }

    [Test]
    public void BurningEmberEmitsSparksAndHeatAuraWhileFlareIsActive()
    {
        fuel.Initialize(6f, 10f); // 3 embers
        Step(0.1f);
        Assert.That(hud.IsEmberActivelyBurning, Is.False);
        Assert.That(hud.ActiveSparkCount, Is.Zero);
        Assert.That(hud.GetSlotBurnAuraAlpha(2), Is.Zero);

        controller.SetFlared(true);
        Step(0.2f);

        Assert.That(hud.IsEmberActivelyBurning, Is.True);
        Assert.That(hud.GetSlotBurnAuraAlpha(2), Is.GreaterThan(0f));
        Assert.That(hud.ActiveSparkCount, Is.GreaterThan(0));

        controller.SetFlared(false);
        controller.TickScout(controller.TransitionDuration);
        Step(0.8f);

        Assert.That(hud.IsEmberActivelyBurning, Is.False);
        Assert.That(hud.GetSlotBurnAuraAlpha(2), Is.Zero);
        Assert.That(hud.ActiveSparkCount, Is.Zero);
    }

    [Test]
    public void MinimalModeTogglesPromptVisibilityWhilePreservingModeText()
    {
        hud.MinimalMode = true;
        fuel.Initialize(4f, 10f);
        Step(0.1f);
        Assert.That(hud.MinimalMode, Is.True);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE"));

        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE ACTIVE"));

        hud.MinimalMode = false;
        Assert.That(hud.MinimalMode, Is.False);
        Assert.That(hud.ModeText, Is.EqualTo("FLARE ACTIVE"));
    }
}
