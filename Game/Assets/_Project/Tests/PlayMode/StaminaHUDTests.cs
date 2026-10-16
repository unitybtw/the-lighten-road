using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StaminaHUDTests
{
    private GameObject playerObject;
    private GameObject hudObject;
    private PlayerStamina stamina;
    private StaminaHUD hud;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        playerObject = new GameObject("Test Player");
        stamina = playerObject.AddComponent<PlayerStamina>();
        stamina.Initialize(100f, 100f);

        hudObject = new GameObject("Test Stamina HUD");
        hud = hudObject.AddComponent<StaminaHUD>();
        hud.SetTarget(stamina);
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
            hud.Tick(dt);
    }

    [Test]
    public void BuildsGaugeAndBindsToPlayerStamina()
    {
        Step(0.1f);
        Assert.That(hud.Target, Is.EqualTo(stamina));
        Assert.That(hud.DisplayedFill, Is.EqualTo(1f).Within(0.01f));
        Assert.That(hud.DisplayedGhostFill, Is.EqualTo(1f).Within(0.01f));
        Assert.That(hud.IsExhausted, Is.False);
        Assert.That(hud.AnchoredPosition.y, Is.EqualTo(hud.BottomOffset).Within(0.1f), "Gauge should be positioned at the bottom-center.");
        Assert.That(hud.BarSize.x, Is.GreaterThan(100f), "Pill gauge width should be slender and proportional.");
    }

    [Test]
    public void UpdatesFillAndTrailsGhostBarOnSprint()
    {
        Step(0.1f);

        // Consume 40 stamina
        stamina.ConsumeSprint(2.0f); // 20/sec * 2 = 40 consumed -> 60 remaining (0.6 fill)
        Assert.That(hud.DisplayedFill, Is.EqualTo(0.6f).Within(0.02f));

        // Right after drain, ghost bar holds higher
        Step(0.05f);
        Assert.That(hud.DisplayedGhostFill, Is.GreaterThan(hud.DisplayedFill),
            "Ghost bar must linger above current fill for physical feedback.");

        // After catch-up time, ghost bar smoothly meets fill
        Step(0.6f);
        Assert.That(hud.DisplayedGhostFill, Is.EqualTo(hud.DisplayedFill).Within(0.05f),
            "Ghost bar must catch down to current fill.");
    }

    [Test]
    public void EntersExhaustionWarningModeWhenDepleted()
    {
        Step(0.1f);

        // Drain stamina completely
        stamina.ConsumeSprint(6.0f); // 20/s * 6 = 120 > 100
        Step(0.1f);

        Assert.That(stamina.IsExhausted, Is.True);
        Assert.That(hud.IsExhausted, Is.True, "HUD must reflect exhaustion mode.");
        Assert.That(hud.DisplayedFill, Is.EqualTo(0f).Within(0.01f));

        // Regenerate past recovery threshold (25%)
        stamina.Regenerate(2.0f); // 25/s * 2 = 50% > 25%
        Step(0.1f);

        Assert.That(stamina.IsExhausted, Is.False);
        Assert.That(hud.IsExhausted, Is.False, "HUD must clear exhaustion once stamina recovers.");
    }

    [Test]
    public void FadesToIdleAlphaWhenFullAndRested()
    {
        // Immediately at start, opacity is 1.0
        hud.Tick(0.1f);
        Assert.That(hud.PanelAlpha, Is.GreaterThan(0.9f));

        // After resting idle at 100% for 3 seconds, panel completely fades out (contextual HUD)
        for (int i = 0; i < 180; i++)
        {
            hud.Tick(1f / 60f);
        }

        Assert.That(hud.PanelAlpha, Is.EqualTo(0f).Within(0.05f),
            "Panel alpha should fade out completely when resting at full stamina to maintain clean screen.");

        // When stamina is consumed, panel immediately wakes up to full visibility
        stamina.ConsumeSprint(1.0f);
        Assert.That(hud.PanelAlpha, Is.EqualTo(1f).Within(0.01f),
            "Panel alpha must immediately snap to 1.0 on stamina consumption.");
    }

    [Test]
    public void RestoresFullStaminaOnReset()
    {
        Step(0.1f);
        stamina.ConsumeSprint(3.0f);
        Step(0.1f);
        Assert.That(hud.DisplayedFill, Is.LessThan(1f));

        stamina.ResetStamina();
        Step(0.1f);

        Assert.That(hud.DisplayedFill, Is.EqualTo(1f).Within(0.01f));
        Assert.That(hud.IsExhausted, Is.False);
    }
}
