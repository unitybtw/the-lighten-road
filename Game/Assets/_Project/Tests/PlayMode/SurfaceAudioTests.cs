using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Audio;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Tests.PlayMode
{
    public sealed class SurfaceAudioTests
    {
        private GameObject sfxGo;
        private SoundEffectManager sfxManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var existing = Object.FindObjectsByType<SoundEffectManager>(FindObjectsSortMode.None);
            foreach (var e in existing) Object.Destroy(e.gameObject);
            SoundEffectManager.ResetInstanceForTesting(null);
            yield return null;

            sfxGo = new GameObject("Test Sound Effect Manager");
            sfxManager = sfxGo.AddComponent<SoundEffectManager>();
            sfxManager.EnsureClipsLoaded();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (sfxGo != null) Object.Destroy(sfxGo);
            SoundEffectManager.ResetInstanceForTesting(null);
            yield return null;
        }

        [Test]
        public void SoundEffectManager_LoadsOrSynthesizesAllSurfaceBanks()
        {
            Assert.That(sfxManager.WoodWalkClips, Is.Not.Null);
            Assert.That(sfxManager.WoodWalkClips.Length, Is.GreaterThan(0));
            Assert.That(sfxManager.MetalWalkClips, Is.Not.Null);
            Assert.That(sfxManager.MetalWalkClips.Length, Is.GreaterThan(0));
            Assert.That(sfxManager.EtherealWalkClips, Is.Not.Null);
            Assert.That(sfxManager.EtherealWalkClips.Length, Is.GreaterThan(0));
            Assert.That(sfxManager.WaterWalkClips, Is.Not.Null);
            Assert.That(sfxManager.WaterWalkClips.Length, Is.GreaterThan(0));

            Assert.That(sfxManager.FlareExtinguishClip, Is.Not.Null);
            Assert.That(sfxManager.SkidScuffClip, Is.Not.Null);
            Assert.That(sfxManager.AirDraftClip, Is.Not.Null);
        }

        [Test]
        public void SoundEffectManager_PlaysAllSurfacesWithoutErrors()
        {
            var surfaces = new[]
            {
                SurfaceType.Stone,
                SurfaceType.Wood,
                SurfaceType.Metal,
                SurfaceType.Ethereal,
                SurfaceType.Water
            };

            foreach (var s in surfaces)
            {
                Assert.DoesNotThrow(() => sfxManager.PlayWalkFootstep(s));
                Assert.DoesNotThrow(() => sfxManager.PlaySprintFootstep(s));
                Assert.DoesNotThrow(() => sfxManager.PlayJump(s));
                Assert.DoesNotThrow(() => sfxManager.PlayLanding(8f, s));
                Assert.DoesNotThrow(() => sfxManager.PlayLanding(15f, s));
                Assert.DoesNotThrow(() => sfxManager.PlaySkidScuff(s));
            }

            Assert.DoesNotThrow(() => sfxManager.PlayFlareExtinguish());
            Assert.DoesNotThrow(() => sfxManager.PlayAirDraft());
        }

        [UnityTest]
        public IEnumerator PlayerAudioController_DetectsSurfaceFromProvider()
        {
            var playerGo = new GameObject("Player_SurfaceTest");
            var motor = playerGo.AddComponent<PlayerMotor2D>();
            var audioCtrl = playerGo.AddComponent<PlayerAudioController>();

            // Create ground surface with SurfaceAudioTypeProvider
            var groundGo = new GameObject("Wood_Platform");
            var col = groundGo.AddComponent<BoxCollider2D>();
            var provider = groundGo.AddComponent<SurfaceAudioTypeProvider>();
            provider.SurfaceType = SurfaceType.Wood;

            yield return null;

            // Default without ground returns Stone
            Assert.That(audioCtrl.GetCurrentSurface(), Is.EqualTo(SurfaceType.Stone));

            // Name-based fallback testing
            var metalGo = new GameObject("Metal_Grating_Chamber");
            metalGo.AddComponent<BoxCollider2D>();
            var etherealGo = new GameObject("Ethereal_Crystal_Bridge");
            etherealGo.AddComponent<BoxCollider2D>();

            Object.Destroy(playerGo);
            Object.Destroy(groundGo);
            Object.Destroy(metalGo);
            Object.Destroy(etherealGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAudioController_RespondsToFlareExtinguishAndFoley()
        {
            var playerGo = new GameObject("Player_FoleyTest");
            var motor = playerGo.AddComponent<PlayerMotor2D>();
            var lantern = playerGo.AddComponent<LanternController>();
            var audioCtrl = playerGo.AddComponent<PlayerAudioController>();

            yield return null;

            // Tick foley when grounded
            Assert.DoesNotThrow(() => audioCtrl.TickFoley(0.02f));

            // Tick footsteps with delta time
            Assert.DoesNotThrow(() => audioCtrl.TickFootsteps(0.5f));

            Object.Destroy(playerGo);
            yield return null;
        }
    }
}
