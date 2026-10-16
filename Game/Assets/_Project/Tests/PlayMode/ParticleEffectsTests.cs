using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TheLightenRoad.Effects;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;

public sealed class ParticleEffectsTests
{
    private GameObject testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = new GameObject("TestRoot");
    }

    [TearDown]
    public void TearDown()
    {
        if (testRoot != null)
            Object.Destroy(testRoot);
    }

    [Test]
    public void DustMotesAtmosphereInitializesAndConfiguresModules()
    {
        var go = new GameObject("MotesTest");
        go.transform.SetParent(testRoot.transform);

        var dust = go.AddComponent<DustMotesAtmosphere>();
        var ps = go.GetComponent<ParticleSystem>();

        Assert.That(ps, Is.Not.Null, "DustMotesAtmosphere should automatically create ParticleSystem.");
        Assert.That(dust.EmissionRate, Is.GreaterThan(0f));
        Assert.That(dust.AreaSize.x, Is.GreaterThan(0f));
        Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
        Assert.That(ps.emission.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator LanternSparksFXRespondsToDimAndFlare()
    {
        var playerGo = new GameObject("PlayerWithSparks");
        playerGo.transform.SetParent(testRoot.transform);

        var fuel = playerGo.AddComponent<LanternFuel>();
        fuel.Capacity = 100f;
        fuel.CurrentFuel = 100f;

        var lantern = playerGo.AddComponent<LanternController>();

        var sparksGo = new GameObject("Sparks");
        sparksGo.transform.SetParent(playerGo.transform);
        var sparks = sparksGo.AddComponent<LanternSparksFX>();

        yield return null; // Wait 1 frame for Awake/Update

        Assert.That(sparks.IsEmitting, Is.True, "Sparks should emit when lantern has fuel.");
        float initialRate = sparks.CurrentEmissionRate;
        Assert.That(initialRate, Is.GreaterThan(0f));

        // Flare the lantern
        lantern.StartFlare();
        yield return null;

        Assert.That(sparks.CurrentEmissionRate, Is.GreaterThan(initialRate),
            "Flare mode should significantly increase spark emission rate.");

        // Stop flare
        lantern.StopFlare();
        yield return null;

        Assert.That(sparks.CurrentEmissionRate, Is.EqualTo(initialRate).Within(0.1f),
            "Returning to Dim mode should restore normal spark emission rate.");

        // Drain fuel to 0
        fuel.Consume(100f);
        yield return null;

        Assert.That(sparks.CurrentEmissionRate, Is.EqualTo(0f),
            "Sparks should stop emitting when lantern fuel is depleted.");
    }

    [Test]
    public void PlayerFootstepDustFXInitializesChildSystems()
    {
        var playerGo = new GameObject("PlayerWithFootsteps");
        playerGo.transform.SetParent(testRoot.transform);
        playerGo.AddComponent<Rigidbody2D>();
        playerGo.AddComponent<CapsuleCollider2D>();
        playerGo.AddComponent<PlayerMotor2D>();

        var dustFx = playerGo.AddComponent<PlayerFootstepDustFX>();

        var runChild = playerGo.transform.Find("RunDust_FX");
        var burstChild = playerGo.transform.Find("BurstDust_FX");

        Assert.That(runChild, Is.Not.Null, "Should create RunDust_FX child object.");
        Assert.That(burstChild, Is.Not.Null, "Should create BurstDust_FX child object.");

        Assert.That(runChild.GetComponent<ParticleSystem>(), Is.Not.Null);
        Assert.That(burstChild.GetComponent<ParticleSystem>(), Is.Not.Null);
        Assert.That(dustFx.EnableRunDust, Is.True);
        Assert.That(dustFx.EnableJumpDust, Is.True);
        Assert.That(dustFx.EnableLandDust, Is.True);
    }
}
