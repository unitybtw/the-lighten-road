using System;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using UnityEngine;

public sealed class LanternFuelTests
{
    private GameObject testObject;
    private LanternFuel fuel;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("Test Lantern Fuel");
        fuel = testObject.AddComponent<LanternFuel>();
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
            UnityEngine.Object.Destroy(testObject);
    }

    [Test]
    public void InitializesWithDefaultCapacityAndStartingFuel()
    {
        Assert.That(fuel.Capacity, Is.EqualTo(100f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f));
        Assert.That(fuel.IsFull, Is.True);
        Assert.That(fuel.IsEmpty, Is.False);
        Assert.That(fuel.NormalizedFuel, Is.EqualTo(1f));
    }

    [Test]
    public void InitializeSetsCurrentFuelAndCapacity()
    {
        fuel.Initialize(40f, 80f);
        Assert.That(fuel.Capacity, Is.EqualTo(80f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(40f));
        Assert.That(fuel.NormalizedFuel, Is.EqualTo(0.5f));
        Assert.That(fuel.IsFull, Is.False);
        Assert.That(fuel.IsEmpty, Is.False);

        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.Initialize(10f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.Initialize(10f, -5f));
    }

    [Test]
    public void TrySpendFuelDeductsWhenSufficientAndRejectsWhenInsufficient()
    {
        fuel.Initialize(50f, 100f);

        bool spent = fuel.TrySpendFuel(20f);
        Assert.That(spent, Is.True);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(30f));

        bool overspent = fuel.TrySpendFuel(40f);
        Assert.That(overspent, Is.False);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(30f));
    }

    [Test]
    public void TrySpendFuelRejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.TrySpendFuel(-5f));
    }

    [Test]
    public void CanSpendFuelChecksAvailabilityWithoutDeducting()
    {
        fuel.Initialize(30f, 100f);

        Assert.That(fuel.CanSpendFuel(30f), Is.True);
        Assert.That(fuel.CanSpendFuel(20f), Is.True);
        Assert.That(fuel.CanSpendFuel(35f), Is.False);
        Assert.That(fuel.CanSpendFuel(-1f), Is.False);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(30f));
    }

    [Test]
    public void AddFuelIncreasesUpToCapacityAndReturnsAcceptedAmount()
    {
        fuel.Initialize(70f, 100f);

        float accepted = fuel.AddFuel(20f);
        Assert.That(accepted, Is.EqualTo(20f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(90f));

        float cappedAccepted = fuel.AddFuel(25f);
        Assert.That(cappedAccepted, Is.EqualTo(10f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f));
        Assert.That(fuel.IsFull, Is.True);
    }

    [Test]
    public void AddFuelWhenFullReturnsZero()
    {
        fuel.Initialize(100f, 100f);

        float accepted = fuel.AddFuel(15f);
        Assert.That(accepted, Is.EqualTo(0f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(100f));
    }

    [Test]
    public void AddFuelRejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.AddFuel(-10f));
    }

    [Test]
    public void RaisesFuelChangedEventOnSpendAndAdd()
    {
        fuel.Initialize(50f, 100f);
        float lastFuel = -1f;
        float lastCap = -1f;
        fuel.FuelChanged += (f, c) =>
        {
            lastFuel = f;
            lastCap = c;
        };

        fuel.TrySpendFuel(15f);
        Assert.That(lastFuel, Is.EqualTo(35f));
        Assert.That(lastCap, Is.EqualTo(100f));

        fuel.AddFuel(25f);
        Assert.That(lastFuel, Is.EqualTo(60f));
        Assert.That(lastCap, Is.EqualTo(100f));
    }

    [Test]
    public void RaisesFuelDepletedEventOnlyWhenFuelReachesZero()
    {
        fuel.Initialize(20f, 100f);
        int depletedCount = 0;
        fuel.FuelDepleted += () => depletedCount++;

        fuel.TrySpendFuel(10f);
        Assert.That(depletedCount, Is.EqualTo(0));

        fuel.TrySpendFuel(10f);
        Assert.That(depletedCount, Is.EqualTo(1));

        fuel.TrySpendFuel(5f);
        Assert.That(depletedCount, Is.EqualTo(1));
    }

    [Test]
    public void TickLanternDrainConsumesFuelInFlareAndPreservesInDim()
    {
        fuel.Initialize(50f, 100f);
        fuel.SetDrainRates(dimDrain: 0f, flareDrain: 10f);

        fuel.TickLanternDrain(flared: false, deltaTime: 1f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(50f));

        fuel.TickLanternDrain(flared: true, deltaTime: 2f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(30f));
    }
}
