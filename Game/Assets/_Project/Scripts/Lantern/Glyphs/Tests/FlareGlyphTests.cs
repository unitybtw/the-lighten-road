using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Glyphs;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Lantern.Glyphs.Tests
{
    public sealed class FlareGlyphTests
    {
        private GameObject playerObject;
        private LanternController lantern;
        private LanternFuel fuel;
        private GameObject glyphObject;
        private FlareGlyph glyph;

        [SetUp]
        public void Setup()
        {
            Time.timeScale = 1f;
            playerObject = new GameObject("TestPlayer");
            fuel = playerObject.AddComponent<LanternFuel>();
            fuel.ResetForChamber(); // Gives 3 embers (6 charges)
            lantern = playerObject.AddComponent<LanternController>();

            glyphObject = new GameObject("TestGlyph");
            glyphObject.transform.position = new Vector3(3f, 0f, 0f); // Within 9.5m activation range
            glyph = glyphObject.AddComponent<FlareGlyph>();
            glyph.BindLantern(lantern);
        }

        [TearDown]
        public void Teardown()
        {
            Time.timeScale = 1f;
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (glyphObject != null) Object.DestroyImmediate(glyphObject);
        }

        [Test]
        public void Glyph_InitializesWithDormantVisuals()
        {
            Assert.AreEqual(0f, glyph.ChargeLevel, "Charge level should initially be 0.");
            Assert.IsFalse(glyph.IsFullyCharged, "Glyph should not be fully charged at start.");
            Assert.IsFalse(glyph.IsReceivingFlareLight, "Glyph should not receive Flare light initially.");
        }

        [UnityTest]
        public IEnumerator Glyph_DoesNotChargeInDimMode()
        {
            // Player is close, but lantern is in default Dim mode (not flared)
            yield return null;
            yield return null;

            Assert.IsFalse(lantern.IsFlared, "Lantern should start in Dim mode.");
            Assert.IsFalse(glyph.IsReceivingFlareLight, "Glyph should not receive Flare light while lantern is Dim.");
            Assert.AreEqual(0f, glyph.ChargeLevel, "ChargeLevel should remain 0 in Dim mode.");
        }

        [UnityTest]
        public IEnumerator Glyph_ChargesWhenIlluminatedByFlare()
        {
            // Activate Flare on lantern
            lantern.ToggleFlare();
            yield return null;

            Assert.IsTrue(lantern.IsFlared, "Lantern should be in Flare mode.");
            Assert.IsTrue(glyph.IsReceivingFlareLight, "Glyph should now detect incoming Flare light.");

            // Wait a few frames for charging
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(glyph.ChargeLevel, 0f, "ChargeLevel should increase while illuminated by Flare.");
        }

        [UnityTest]
        public IEnumerator Glyph_ReachesFullCharge_AndInvokesEvent()
        {
            bool fullyChargedFired = false;
            glyph.OnFullyCharged.AddListener(() => fullyChargedFired = true);

            glyph.ForceCharge(1f);
            yield return null;

            Assert.IsTrue(glyph.IsFullyCharged, "Glyph should be marked fully charged.");
            Assert.IsTrue(fullyChargedFired, "OnFullyCharged event should have been invoked.");
            Assert.AreEqual(1f, glyph.ChargeLevel, "Charge level should be 1.0.");
        }

        [UnityTest]
        public IEnumerator Glyph_LingersAfterFlareEnds()
        {
            glyph.ForceCharge(1f);
            yield return null;

            // Turn off Flare and simulate 0.5s (within 3.5s lingerDuration)
            glyph.BindLantern(null); // No incoming light
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(glyph.ChargeLevel, 0.8f, "Glyph should remain lit during linger duration.");
            Assert.IsTrue(glyph.IsFullyCharged, "Glyph should remain marked fully charged during linger.");
        }

        [UnityTest]
        public IEnumerator Glyph_ExtinguishesAfterDischarge()
        {
            bool extinguishedFired = false;
            glyph.OnExtinguished.AddListener(() => extinguishedFired = true);

            glyph.ForceCharge(1f);
            yield return null;

            glyph.ForceDischarge();
            yield return null;

            Assert.AreEqual(0f, glyph.ChargeLevel, "Charge level should return to 0.");
            Assert.IsFalse(glyph.IsFullyCharged, "Glyph should no longer be fully charged.");
            Assert.IsTrue(extinguishedFired, "OnExtinguished event should have been invoked.");
        }

        [UnityTest]
        public IEnumerator SecretPassage_OpensWhenGlyphCharged()
        {
            var passageObj = new GameObject("TestPassage");
            var col = passageObj.AddComponent<BoxCollider2D>();
            var ren = passageObj.AddComponent<SpriteRenderer>();
            ren.color = Color.white;
            var passage = passageObj.AddComponent<SecretPassage>();

            glyph.OnFullyCharged.AddListener(passage.Open);

            Assert.IsTrue(col.enabled, "Passage collider should initially be enabled.");
            Assert.IsFalse(passage.IsOpen, "Passage should initially be closed.");

            // Fully charge glyph
            glyph.ForceCharge(1f);
            yield return null;
            yield return new WaitForSeconds(0.1f);

            Assert.IsTrue(passage.IsOpen, "Passage should be open after glyph charge.");
            Assert.IsFalse(col.enabled, "Passage collider should be disabled to allow traversal.");

            Object.DestroyImmediate(passageObj);
        }
    }
}
