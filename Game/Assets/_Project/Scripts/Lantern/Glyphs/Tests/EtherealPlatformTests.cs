using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Glyphs;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Lantern.Glyphs.Tests
{
    public sealed class EtherealPlatformTests
    {
        private GameObject playerObject;
        private LanternController lantern;
        private LanternFuel fuel;
        private GameObject platformObject;
        private EtherealPlatform2D platform;

        [SetUp]
        public void Setup()
        {
            Time.timeScale = 1f;
            playerObject = new GameObject("TestPlayer");
            playerObject.transform.position = Vector3.zero;
            fuel = playerObject.AddComponent<LanternFuel>();
            fuel.ResetForChamber();
            lantern = playerObject.AddComponent<LanternController>();

            platformObject = new GameObject("TestEtherealPlatform");
            platformObject.transform.position = new Vector3(20f, 0f, 0f); // Far away initially
            platform = platformObject.AddComponent<EtherealPlatform2D>();
        }

        [TearDown]
        public void Teardown()
        {
            Time.timeScale = 1f;
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (platformObject != null) Object.DestroyImmediate(platformObject);
        }

        [Test]
        public void Platform_InitializesEtherealWithColliderDisabled()
        {
            var col = platform.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(col, "Platform must have a BoxCollider2D.");
            Assert.IsFalse(col.enabled, "Collider must start disabled in ethereal state.");
            Assert.IsFalse(platform.IsSolid, "Platform must start non-solid.");
        }

        [UnityTest]
        public IEnumerator Platform_SolidifiesWhenLanternInDimRange()
        {
            // Move platform into close Dim range (2.0m, default dimActivationDistance is 3.4m)
            platformObject.transform.position = new Vector3(2.0f, 0f, 0f);

            yield return null;
            yield return null;

            Assert.IsTrue(platform.IsIlluminated, "Platform must detect lantern illumination in close range.");
            Assert.IsTrue(platform.IsSolid, "Platform must solidify when illuminated by lantern.");
            var col = platform.GetComponent<BoxCollider2D>();
            Assert.IsTrue(col.enabled, "BoxCollider2D must be enabled when solid.");
        }

        [UnityTest]
        public IEnumerator Platform_SolidifiesAtLongRangeWhenFlared()
        {
            // Place platform at 6.0m (outside Dim range 3.4m, but within Flare range 8.5m)
            platformObject.transform.position = new Vector3(6.0f, 0f, 0f);

            yield return null;
            yield return null;

            Assert.IsFalse(platform.IsIlluminated, "Platform must not be illuminated in Dim mode at 6m.");
            Assert.IsFalse(platform.IsSolid, "Platform must remain ethereal at 6m in Dim mode.");

            // Ignite Flare
            Assert.IsTrue(lantern.SetFlared(true), "Lantern must enter Flare mode.");

            yield return null;
            yield return null;

            Assert.IsTrue(platform.IsIlluminated, "Platform must be illuminated at 6m when Flare is active.");
            Assert.IsTrue(platform.IsSolid, "Platform must solidify at 6m under radiant Flare light.");
            var col = platform.GetComponent<BoxCollider2D>();
            Assert.IsTrue(col.enabled, "Collider must enable when energized by Flare.");
        }

        [UnityTest]
        public IEnumerator Platform_DecaysAndDissolvesWhenLanternDeparts()
        {
            // Place in range and solidify
            platformObject.transform.position = new Vector3(2.0f, 0f, 0f);
            yield return null;
            yield return null;

            Assert.IsTrue(platform.IsSolid);

            // Move player far away
            playerObject.transform.position = new Vector3(50f, 0f, 0f);

            yield return null;
            yield return null;

            Assert.IsFalse(platform.IsIlluminated, "Illumination must be lost when lantern departs.");
            Assert.IsTrue(platform.IsSolid, "Platform must still be solid during grace linger period.");
            Assert.IsTrue(platform.LingerTimer > 0f, "Linger timer must be active.");

            // Force expire linger timer
            platform.DissolveImmediate();

            Assert.IsFalse(platform.IsSolid, "Platform must dissolve back to ethereal.");
            var col = platform.GetComponent<BoxCollider2D>();
            Assert.IsFalse(col.enabled, "Collider must disable upon dissolving.");
        }

        [UnityTest]
        public IEnumerator Platform_ForceSolidifyWorksForScriptedEvents()
        {
            Assert.IsFalse(platform.IsSolid);
            platform.ForceSolidify(5.0f);

            yield return null;

            Assert.IsTrue(platform.IsSolid, "Platform must solidify via ForceSolidify.");
            var col = platform.GetComponent<BoxCollider2D>();
            Assert.IsTrue(col.enabled, "Collider must enable via ForceSolidify.");
        }
    }
}
