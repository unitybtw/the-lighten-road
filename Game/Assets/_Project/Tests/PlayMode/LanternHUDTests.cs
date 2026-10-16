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
        Assert.That(hud.ModeText, Is.EqualTo("[F] İZCİ FIRLAT"));

        controller.SetFlared(true);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("ALEV İZCİSİ"));

        controller.SetFlared(false);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("[F] İZCİ FIRLAT"));
    }

    [Test]
    public void EmptyEmberStateDisplaysKorTukendi()
    {
        fuel.Initialize(0f, 10f);
        Step(0.05f);
        Assert.That(hud.ModeText, Is.EqualTo("[F] KOR TÜKENDİ"));
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
}
