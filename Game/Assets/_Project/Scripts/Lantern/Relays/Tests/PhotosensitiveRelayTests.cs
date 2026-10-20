using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Relays;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Lantern.Relays.Tests
{
    public sealed class PhotosensitiveRelayTests
    {
        private GameObject relayGo;
        private PhotosensitiveRelay2D relay;
        private GameObject lanternGo;
        private LanternController lantern;
        private LanternFuel fuel;

        [SetUp]
        public void SetUp()
        {
            relayGo = new GameObject("Test_PhotosensitiveRelay");
            relayGo.transform.position = Vector3.zero;
            relay = relayGo.AddComponent<PhotosensitiveRelay2D>();

            lanternGo = new GameObject("Test_Lantern");
            lanternGo.transform.position = new Vector3(2f, 0f, 0f);
            fuel = lanternGo.AddComponent<LanternFuel>();
            fuel.AddEmbers(3);
            lantern = lanternGo.AddComponent<LanternController>();

            relay.TargetLantern = lantern;
        }

        [TearDown]
        public void TearDown()
        {
            if (relayGo != null) Object.DestroyImmediate(relayGo);
            if (lanternGo != null) Object.DestroyImmediate(lanternGo);
        }

        [Test]
        public void InitialStateIsDormantAndUncharged()
        {
            Assert.That(relay.ChargeLevel, Is.EqualTo(0f));
            Assert.That(relay.IsActivated, Is.False);
            Assert.That(relay.IsLit, Is.False);
        }

        [Test]
        public void HierarchyAndVisualComponentsAreProperlyConstructed()
        {
            relay.EnsureHierarchy();
            Assert.That(relay.CrystalTransform, Is.Not.Null);
            Assert.That(relay.CrystalRenderer, Is.Not.Null);
            Assert.That(relay.CrystalLight, Is.Not.Null);
        }

        [Test]
        public void DimLightChargesRelayWithinDistance()
        {
            // Position lantern within dim range (1.2 < 1.925 dim radius)
            lanternGo.transform.position = new Vector3(1.2f, 0f, 0f);
            relay.ChargeTime = 1.0f;

            relay.Tick(0.5f);

            Assert.That(relay.IsLit, Is.True);
            Assert.That(relay.ChargeLevel, Is.GreaterThan(0.2f));
            Assert.That(relay.IsActivated, Is.False);
        }

        [Test]
        public void FlareLightChargesSignificantlyFasterAndFromFarther()
        {
            // Position lantern outside dim range (6.0 > 3.2) but inside flare range (6.0 < 9.5)
            lanternGo.transform.position = new Vector3(6.0f, 0f, 0f);
            relay.ChargeTime = 1.0f;
            relay.FlareMultiplier = 3.0f;

            // When dim, outside reach
            relay.Tick(0.2f);
            Assert.That(relay.IsLit, Is.False);
            Assert.That(relay.ChargeLevel, Is.EqualTo(0f));

            // Activate Flare on lantern
            lantern.SetFlared(true);
            relay.Tick(0.3f);

            Assert.That(relay.IsLit, Is.True);
            Assert.That(relay.ChargeLevel, Is.GreaterThan(0.4f), "Flare should charge rapidly across large distance");
        }

        [Test]
        public void FullChargeTriggersActivationEventAndTurnsActive()
        {
            bool eventFired = false;
            relay.OnActivated.AddListener(() => eventFired = true);

            lanternGo.transform.position = new Vector3(1.0f, 0f, 0f);
            relay.ChargeTime = 0.5f;

            // Tick enough to reach full charge
            relay.Tick(0.6f);

            Assert.That(relay.ChargeLevel, Is.EqualTo(1f).Within(0.01f));
            Assert.That(relay.IsActivated, Is.True);
            Assert.That(eventFired, Is.True);
        }

        [Test]
        public void TimedModeLingersThenDischargesToDeactivated()
        {
            relay.Mode = RelayMode.Timed;
            relay.LingerDuration = 0.5f;
            relay.DischargeSpeed = 2.0f;

            // Fully charge
            relay.ForceSetCharge(1.0f);
            Assert.That(relay.IsActivated, Is.True);

            // Move lantern far away
            lanternGo.transform.position = new Vector3(50f, 0f, 0f);

            // Tick during linger duration (0.3s < 0.5s)
            relay.Tick(0.3f);
            Assert.That(relay.IsActivated, Is.True, "Should remain active during linger period");
            Assert.That(relay.ChargeLevel, Is.EqualTo(1f));

            // Tick past linger duration and through discharge
            relay.Tick(0.3f); // expires linger, starts discharge
            Assert.That(relay.ChargeLevel, Is.LessThan(1.0f));

            relay.Tick(0.5f); // finishes discharge
            Assert.That(relay.ChargeLevel, Is.EqualTo(0f));
            Assert.That(relay.IsActivated, Is.False, "Should deactivate once completely discharged");
        }

        [Test]
        public void LatchedModeRemainsActivePermanentlyOnceCharged()
        {
            relay.Mode = RelayMode.Latched;
            relay.ForceSetCharge(1.0f);
            Assert.That(relay.IsActivated, Is.True);

            // Move lantern far away
            lanternGo.transform.position = new Vector3(100f, 0f, 0f);

            // Tick for several seconds in darkness
            relay.Tick(5.0f);

            Assert.That(relay.IsActivated, Is.True, "Latched relay must never discharge");
            Assert.That(relay.ChargeLevel, Is.EqualTo(1.0f));
        }

        [Test]
        public void ArchiveGateOpensWhenRelayActivatesAndClosesWhenDeactivated()
        {
            var gateGo = new GameObject("Test_ArchiveGate");
            gateGo.transform.position = new Vector3(10f, 0f, 0f);
            var gate = gateGo.AddComponent<ArchiveGate2D>();
            gate.Relay = relay;
            gate.TravelDuration = 0.5f;

            // Manually register listener as would happen in OnEnable
            relay.OnStateChanged.AddListener(active => gate.SetGateOpen(active));

            Assert.That(gate.IsOpen, Is.False);
            Assert.That(gate.MotionProgress, Is.EqualTo(0f));

            // Activate relay
            relay.SetActivated(true);
            Assert.That(gate.IsOpen, Is.True);

            // Tick gate motion
            gate.Tick(0.25f);
            Assert.That(gate.IsMoving, Is.True);
            Assert.That(gate.MotionProgress, Is.GreaterThan(0.4f));

            gate.Tick(0.3f);
            Assert.That(gate.IsMoving, Is.False);
            Assert.That(gate.MotionProgress, Is.EqualTo(1.0f));

            // Deactivate relay
            relay.SetActivated(false);
            Assert.That(gate.IsOpen, Is.False);

            gate.Tick(0.6f);
            Assert.That(gate.MotionProgress, Is.EqualTo(0f));
            Assert.That(gate.IsOpen, Is.False);

            Object.DestroyImmediate(gateGo);
        }
    }
}
