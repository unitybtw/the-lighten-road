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
        Assert.That(fuel.Capacity, Is.EqualTo(8f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(6f));
        Assert.That(fuel.TotalCharges, Is.EqualTo(6));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
        Assert.That(fuel.FullEmbers, Is.EqualTo(3));
        Assert.That(fuel.HasHalfEmber, Is.False);
        Assert.That(fuel.OverflowEmbers, Is.EqualTo(0));
    }

    [Test]
    public void InitializeSetsCurrentFuelAndCapacity()
    {
        fuel.Initialize(5f, 10f);
        Assert.That(fuel.Capacity, Is.EqualTo(8f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(5f));
        Assert.That(fuel.TotalCharges, Is.EqualTo(5));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
        Assert.That(fuel.FullEmbers, Is.EqualTo(2));
        Assert.That(fuel.HasHalfEmber, Is.True);

        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.Initialize(10f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.Initialize(10f, -5f));
    }

    [Test]
    public void TrySpendFuelDeductsWhenSufficientAndRejectsWhenInsufficient()
    {
        fuel.Initialize(5f, 10f);

        bool spent = fuel.TrySpendFuel(2f);
        Assert.That(spent, Is.True);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(3f));

        bool overspent = fuel.TrySpendFuel(4f);
        Assert.That(overspent, Is.False);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(3f));
    }

    [Test]
    public void LegacyTrySpendChargeKeepsMechanismCompatibility()
    {
        fuel.Initialize(4f, 10f);

        bool spent = fuel.TrySpendCharge();
        Assert.That(spent, Is.True);
        Assert.That(fuel.TotalCharges, Is.EqualTo(3));
        Assert.That(fuel.HasHalfEmber, Is.True);
    }

    [Test]
    public void AddEmbersCapsAtFourAndNeverExpandsCapacity()
    {
        fuel.Initialize(4f, 10f);
        fuel.AddEmbers(2);
        Assert.That(fuel.TotalCharges, Is.EqualTo(8));
        Assert.That(fuel.TotalEmbers, Is.EqualTo(4));
        Assert.That(fuel.Capacity, Is.EqualTo(8f));
    }

    [Test]
    public void LegacyOverflowFlagCannotBypassFourEmberLimit()
    {
        fuel.AllowOverflowCapacity = true;
        fuel.Initialize(100f, 100f);
        fuel.AddEmbers(2);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(8f));
        Assert.That(fuel.AllowOverflowCapacity, Is.False);
        Assert.That(fuel.OverflowEmbers, Is.Zero);
    }

    [Test]
    public void BoundedCapacityCapsWhenOverflowIsDisabled()
    {
        fuel.Initialize(6f, 10f);
        fuel.AllowOverflowCapacity = false;

        float accepted = fuel.AddFuel(5f);
        Assert.That(accepted, Is.EqualTo(2f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(8f));
        Assert.That(fuel.IsFull, Is.True);
    }

    [Test]
    public void AddFuelRejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => fuel.AddFuel(-5f));
    }

    [Test]
    public void CanSpendFuelChecksAvailabilityWithoutDeducting()
    {
        fuel.Initialize(3f, 10f);

        Assert.That(fuel.CanSpendFuel(3f), Is.True);
        Assert.That(fuel.CanSpendFuel(2f), Is.True);
        Assert.That(fuel.CanSpendFuel(4f), Is.False);
        Assert.That(fuel.CanSpendFuel(-1f), Is.False);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(3f));
    }

    [Test]
    public void RaisesFuelChangedEventOnSpendAndAdd()
    {
        fuel.Initialize(5f, 10f);
        float lastFuel = -1f;
        float lastCap = -1f;
        fuel.FuelChanged += (f, c) =>
        {
            lastFuel = f;
            lastCap = c;
        };

        fuel.TrySpendFuel(2f);
        Assert.That(lastFuel, Is.EqualTo(3f));
        Assert.That(lastCap, Is.EqualTo(8f));

        fuel.AddFuel(3f);
        Assert.That(lastFuel, Is.EqualTo(6f));
        Assert.That(lastCap, Is.EqualTo(8f));
    }

    [Test]
    public void RaisesFuelDepletedEventOnlyWhenFuelReachesZero()
    {
        fuel.Initialize(2f, 10f);
        int depletedCount = 0;
        fuel.FuelDepleted += () => depletedCount++;

        fuel.TrySpendCharge();
        Assert.That(depletedCount, Is.EqualTo(0));

        fuel.TrySpendCharge();
        Assert.That(depletedCount, Is.EqualTo(1));
    }

    [Test]
    public void InfiniteDimDrainPreservesFuelWhileFlareConsumes()
    {
        fuel.Initialize(6f, 10f);
        fuel.SetDrainRates(dimDrain: 0f, flareDrain: 2f);

        fuel.TickLanternDrain(flared: false, deltaTime: 3f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(6f), "Dim mode should never drain fuel.");

        fuel.TickLanternDrain(flared: true, deltaTime: 1f);
        Assert.That(fuel.CurrentFuel, Is.EqualTo(4f));
    }
    [Test]
    public void WholeEmberSpendAndChamberResetUseThreeStoneBudget()
    {
        Assert.That(fuel.TrySpendEmber(), Is.True);
        Assert.That(fuel.TotalEmbers, Is.EqualTo(2));
        Assert.That(fuel.TrySpendEmber(), Is.True);
        Assert.That(fuel.TrySpendEmber(), Is.True);
        Assert.That(fuel.TrySpendEmber(), Is.False);
        fuel.ResetForChamber();
        Assert.That(fuel.TotalEmbers, Is.EqualTo(3));
    }
}
