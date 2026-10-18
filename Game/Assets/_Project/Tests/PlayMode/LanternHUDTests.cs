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
    public void BuildsOverlayCanvasAndInitializesThreeEmberSlots()
    {
        Step(0.1f);
        Assert.That(hud.Canvas, Is.Not.Null);
        Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.Target, Is.SameAs(controller));
        Assert.That(hud.MaxDisplaySlots, Is.EqualTo(3));
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

        Assert.That(hud.GetSlotColor(3), Is.EqualTo(Color.clear)); // There is no fourth slot.
    }

    [Test]
    public void InventoryCannotExposeMoreThanThreeIconsOrAnOverflowBadge()
    {
        fuel.Initialize(18f, 18f);
        Step(.05f);
        Assert.That(hud.MaxDisplaySlots, Is.EqualTo(3));
        Assert.That(hud.DisplayedCharges, Is.EqualTo(6));
        Assert.That(hud.OverflowText, Is.Empty);
        Assert.That(hud.GetSlotSprite(3), Is.Null);
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
        for (int i = 0; i < 3; i++)
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
    public void CollectingAtCapacityDoesNotInventPickupFeedback()
    {
        Step(.1f);
        fuel.AddEmbers(1);
        Step(.05f);
        Assert.That(hud.IsShowingPickup, Is.False);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(6));
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
    public void OneUseFadesCracksAndRemovesAWholeEmber()
    {
        fuel.Initialize(2f, 6f);
        Step(.05f);
        controller.SetFlared(true);
        Assert.That(fuel.TotalEmbers, Is.Zero);
        controller.TickScout(controller.FlareScoutDuration * .5f);
        Step(.05f);
        Assert.That(hud.ActiveEmberLevel, Is.EqualTo(.5f).Within(.001f));
        Assert.That(hud.GetSlotSprite(0), Is.SameAs(PixelSprites.EmberFull));
        Assert.That(hud.GetSlotCrackAlpha(0), Is.GreaterThan(0));
        controller.TickScout(controller.FlareScoutDuration);
        Step(.7f);
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
        Assert.That(hud.ActiveEmberLevel, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(hud.PickupText, Is.EqualTo("+1 EMBER"));
    }

    [Test]
    public void DisablingHudHidesCanvasAndReenableDoesNotFakePickup()
    {
        fuel.Initialize(4f, 6f);
        Step(0.1f);
        hud.enabled = false;
        Assert.That(hud.Canvas.enabled, Is.False);
        fuel.AddFuel(2f);
        hud.enabled = true;
        Step(0.1f);
        Assert.That(hud.Canvas.enabled, Is.True);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(6));
        Assert.That(hud.IsShowingPickup, Is.False);
    }

    [Test]
    public void ReducedMotionKeepsInventoryAndPickupFeedbackReadable()
    {
        fuel.Initialize(4f, 6f);
        hud.ReduceMotion = true;
        Step(0.1f);
        fuel.AddFuel(2f);
        Step(0.1f);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(6));
        Assert.That(hud.IsShowingPickup, Is.True);
        Assert.That(hud.PickupText, Is.EqualTo("+1 EMBER"));
        Assert.That(hud.Canvas.GetComponentInChildren<CanvasGroup>().alpha, Is.EqualTo(1f));
    }

    [Test]
    public void RetargetingClearsPreviousInventoryFeedback()
    {
        fuel.Initialize(4f, 6f);
        Step(0.1f);
        fuel.AddFuel(2f);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.True);
        hud.SetTarget(controller);
        Step(0.05f);
        Assert.That(hud.IsShowingPickup, Is.False);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(6));
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
