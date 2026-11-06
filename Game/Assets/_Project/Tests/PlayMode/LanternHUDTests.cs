using System.Collections;
using System.Linq;
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
    public void CachedPromptStillRespondsToBindingAndControllerStyleChanges()
    {
        var original=ControllerGlyphService.ConfiguredStyle;
        var originalKey=controller.ToggleFlareKey;
        try
        {
            ControllerGlyphService.ConfiguredStyle=ControllerPromptStyle.Keyboard;
            hud.Tick(5f, 5f);
            var prompts = hud.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Prompt Labels");
            Assert.That(prompts.alpha, Is.Zero);
            controller.ToggleFlareKey=UnityEngine.InputSystem.Key.G;hud.Tick(0,0);
            Assert.That(prompts.alpha, Is.EqualTo(1f), "A changed binding must bring the hint back.");
            var labels=hud.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.That(System.Array.Exists(labels,label=>label.text=="G"),Is.True);
            controller.ToggleFlareKey=UnityEngine.InputSystem.Key.F;hud.Tick(0,0);
            Assert.That(System.Array.Exists(labels,label=>label.text=="F"),Is.True);
            ControllerGlyphService.ConfiguredStyle=ControllerPromptStyle.PlayStation;hud.Tick(0,0);
            var expected=ControllerGlyphService.GetActionSprite(GlyphAction.Flare,ControllerPromptStyle.PlayStation);
            Assert.That(expected,Is.Not.Null);
            Assert.That(System.Array.Exists(hud.GetComponentsInChildren<UnityEngine.UI.Image>(true),image=>image.sprite==expected),Is.True);
        }
        finally{controller.ToggleFlareKey=originalKey;ControllerGlyphService.ConfiguredStyle=original;}
    }

    [Test]
    public void BuildsOverlayCanvasAndInitializesFourEmberSlots()
    {
        Step(0.1f);
        Assert.That(hud.Canvas, Is.Not.Null);
        Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(hud.Target, Is.SameAs(controller));
        Assert.That(hud.MaxDisplaySlots, Is.EqualTo(4));
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
    public void InventoryCannotExposeMoreThanFourIconsOrAnOverflowBadge()
    {
        fuel.Initialize(18f, 18f);
        Step(.05f);
        Assert.That(hud.MaxDisplaySlots, Is.EqualTo(4));
        Assert.That(hud.DisplayedCharges, Is.EqualTo(8));
        Assert.That(hud.OverflowText, Is.Empty);
        Assert.That(hud.GetSlotSprite(4), Is.Null);
    }

    [Test]
    public void FlareScoutModeUpdatesModeLabel()
    {
        fuel.Initialize(6f, 10f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("Flare"));

        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("Dim"));

        controller.SetFlared(false);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("Dimming"));
        Assert.That(hud.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Prompt Labels").alpha, Is.Zero,
            "The stone already shows the transition; no extra status line is needed.");
        controller.TickScout(controller.TransitionDuration);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("Flare"));
    }

    [Test]
    public void EmptyEmberStateDisplaysEnglishWarning()
    {
        fuel.Initialize(0f, 10f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("No embers"));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(hud.GetSlotColor(i), Is.EqualTo(Color.clear));
        }
    }

    [Test]
    public void MinimalModeKeepsTheEmptyWarningVisibleDuringTheLastFlare()
    {
        hud.MinimalMode = true;
        fuel.Initialize(2f, 8f);
        hud.SetTarget(controller);
        Step(.1f);
        var prompts = hud.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Prompt Labels");
        Assert.That(prompts.alpha, Is.Zero);
        Assert.That(controller.SetFlared(true), Is.True);
        Step(.1f);
        Assert.That(controller.HasActiveScout, Is.True);
        Assert.That(fuel.IsEmpty, Is.True);
        Assert.That(hud.ModeText, Is.EqualTo("No embers"));
        Assert.That(hud.IsEmptyWarningVisible, Is.True);
        Assert.That(prompts.alpha, Is.EqualTo(1f));
        var key = hud.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Bound key icon");
        Assert.That(key.gameObject.activeSelf, Is.False, "An empty lantern must not advertise another Flare.");

        fuel.AddEmbers(1);
        Step(.1f);
        Assert.That(hud.IsEmptyWarningVisible, Is.False);
        Assert.That(prompts.alpha, Is.Zero, "Recovering fuel restores the chosen minimal layout.");
    }

    [Test]
    public void ContextualHintOmitsTheCostAndDoesNotAdvertiseAnUnavailableAction()
    {
        Step(.1f);
        Assert.That(hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.name == "Flare cost"), Is.False);
        fuel.Initialize(1f, 8f);
        Step(.1f);
        Assert.That(controller.SetFlared(true), Is.False);
        Assert.That(hud.ModeText, Is.EqualTo("Need 1 ember"));
        var key = hud.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Bound key");
        Assert.That(key.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void IdleReservesStayStillAndBurnAuraTracksTheSpentEmbersRemainingLife()
    {
        hud.Tick(1f, 1f);
        var aura = hud.GetComponentsInChildren<UnityEngine.UI.Image>().First(i => i.name == "Ember aura");
        Color idle = aura.color;
        hud.Tick(0f, 100f);
        Assert.That(aura.color, Is.EqualTo(idle), "A held reserve must not pulse just because time passes.");
        controller.SetFlared(true);
        controller.TickScout(controller.FlareScoutDuration * .2f);
        hud.Tick(0f, 100f);
        float early = hud.GetSlotBurnAuraAlpha(2);
        float afterSpend = fuel.CurrentFuel;
        controller.TickScout(controller.FlareScoutDuration * .5f);
        hud.Tick(0f, 1f);
        Assert.That(hud.GetSlotBurnAuraAlpha(2), Is.InRange(.001f, early * .5f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(afterSpend), "Presentation must never spend additional fuel.");
    }

    [Test]
    public void ActionHintLeavesSpaceAroundTheReserveAndFadesUntilNeeded()
    {
        Step(.6f);
        var labels = hud.GetComponentsInChildren<TMPro.TMP_Text>(true);
        var action = labels.Single(t => t.name == "Action");
        var key = labels.Single(t => t.name == "Bound key");
        var prompts = hud.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Prompt Labels");
        float gap = action.rectTransform.anchoredPosition.x - key.rectTransform.anchoredPosition.x - key.rectTransform.sizeDelta.x;
        Assert.That(gap, Is.GreaterThanOrEqualTo(12f));
        Assert.That(action.fontSize, Is.GreaterThanOrEqualTo(12f));
        Assert.That(-prompts.GetComponent<RectTransform>().anchoredPosition.y - 30f, Is.GreaterThanOrEqualTo(12f));
        foreach (var aura in hud.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i => i.name == "Ember aura"))
            Assert.That(aura.color.a, Is.Zero, "An unused reserve needs no persistent halo.");
        Assert.That(hud.ActiveSparkCount, Is.Zero);

        hud.Tick(5f, 5f);
        Assert.That(prompts.alpha, Is.Zero, "Exploration should settle to just the reserve icons.");
        Assert.That(hud.GetComponentsInChildren<CanvasGroup>().Single(g => g.name == "Ember 1").alpha, Is.EqualTo(1f));
        controller.SetFlared(true);
        hud.Tick(.1f, 5.1f);
        Assert.That(hud.ModeText, Is.EqualTo("Dim"));
        Assert.That(prompts.alpha, Is.EqualTo(1f), "The new available action should briefly explain itself.");
        hud.Tick(5f, 10.1f);
        Assert.That(prompts.alpha, Is.Zero);
        fuel.Initialize(0f, 8f);
        hud.Tick(5f, 15.1f);
        Assert.That(hud.IsEmptyWarningVisible, Is.True, "The survival warning must never time out with optional hints.");
    }

    [UnityTest]
    public IEnumerator LosingTheTargetHidesStaleInventoryAndReplacementDoesNotInventAPickup()
    {
        Step(.1f);
        Object.DestroyImmediate(playerObject);
        hud.Tick(.1f, 0f);
        Assert.That(hud.Canvas.enabled, Is.False);

        playerObject = new GameObject("Replacement HUD Lantern");
        fuel = playerObject.AddComponent<LanternFuel>();
        controller = playerObject.AddComponent<LanternController>();
        fuel.Initialize(4f, 8f);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(hud.Canvas.enabled, Is.True);
        Assert.That(hud.Target, Is.SameAs(controller));
        Assert.That(hud.DisplayedCharges, Is.EqualTo(4));
        Assert.That(hud.IsShowingPickup, Is.False);
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
        fuel.Initialize(8f,8f);
        hud.SetTarget(controller); // Reset the setup inventory feedback before testing a rejected pickup.
        Step(.1f);
        fuel.AddEmbers(1);
        Step(.05f);
        Assert.That(hud.IsShowingPickup, Is.False);
        Assert.That(hud.DisplayedCharges, Is.EqualTo(8));
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
        Assert.That(hud.ModeText, Is.EqualTo("No embers"));
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
        Assert.That(hud.PickupText, Is.EqualTo("+1"));
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
        Assert.That(hud.PickupText, Is.EqualTo("+1"));
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
    public void BurningEmberKeepsItsWearFeedbackWithoutEmittingHudSparks()
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
        Assert.That(hud.ActiveSparkCount, Is.Zero);
        Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.Image>(true).Any(i=>i.name.StartsWith("Spark ")), Is.False);

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
        Assert.That(hud.ModeText, Is.EqualTo("Flare"));

        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("Dim"));

        hud.MinimalMode = false;
        Assert.That(hud.MinimalMode, Is.False);
        Assert.That(hud.ModeText, Is.EqualTo("Dim"));
    }
}
