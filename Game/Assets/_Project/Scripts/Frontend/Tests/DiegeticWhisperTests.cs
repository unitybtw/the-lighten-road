using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Audio;
using TheLightenRoad.Frontend;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class DiegeticWhisperTests
    {
        private GameObject playerGo;
        private PlayerMotor2D playerMotor;
        private LanternController playerLantern;
        private DiegeticWhisper whisper;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;

            // Clean up any existing managers
            var existingWhispers = Object.FindObjectsByType<DiegeticWhisper>(FindObjectsSortMode.None);
            foreach (var w in existingWhispers) Object.Destroy(w.gameObject);

            playerGo = new GameObject("TestPlayer_Whisper");
            playerMotor = playerGo.AddComponent<PlayerMotor2D>();
            playerLantern = playerGo.AddComponent<LanternController>();
            playerGo.transform.position = new Vector3(0f, 0f, 0f);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (playerGo != null) Object.Destroy(playerGo);
            if (whisper != null) Object.Destroy(whisper.gameObject);

            PlayerPrefs.DeleteKey("TLR_Whisper_test_jump_pers");
            PlayerPrefs.DeleteKey("TLR_Whisper_test_flare_pers");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Whisper_InitializesHidden_WithWorldCanvas()
        {
            whisper = DiegeticWhisper.Spawn(
                new Vector3(20f, 0f, 0f),
                "SPACE",
                "LEAP ACROSS THE ABYSS",
                WhisperAction.Jump
            );

            yield return null;

            Assert.That(whisper, Is.Not.Null);
            Assert.That(whisper.KeyHint, Is.EqualTo("SPACE"));
            Assert.That(whisper.ActionPrompt, Is.EqualTo("LEAP ACROSS THE ABYSS"));
            Assert.That(whisper.CurrentAlpha, Is.EqualTo(0f));
            Assert.That(whisper.IsCompleted, Is.False);
            Assert.That(whisper.IsDissolving, Is.False);
        }

        [UnityTest]
        public IEnumerator Whisper_FadesIn_WhenPlayerEntersProximity()
        {
            whisper = DiegeticWhisper.Spawn(
                new Vector3(2f, 0f, 0f),
                "SPACE",
                "LEAP ACROSS THE ABYSS",
                WhisperAction.Jump
            );

            yield return null;

            // Player at (0, 0, 0), whisper at (2, 0, 0) -> distance 2.0 <= triggerRadius (4.2)
            whisper.TickWhisper(0.35f);

            Assert.That(whisper.CurrentAlpha, Is.GreaterThan(0.5f));
            Assert.That(whisper.IsRevealed, Is.True);
        }

        [UnityTest]
        public IEnumerator Whisper_FadesOut_WhenPlayerLeavesProximity()
        {
            whisper = DiegeticWhisper.Spawn(
                new Vector3(2f, 0f, 0f),
                "SPACE",
                "LEAP ACROSS THE ABYSS",
                WhisperAction.Jump
            );

            yield return null;

            // Fade in
            whisper.TickWhisper(0.35f);
            Assert.That(whisper.CurrentAlpha, Is.GreaterThan(0.5f));

            // Move player far away
            playerGo.transform.position = new Vector3(30f, 0f, 0f);

            // Tick fade out
            whisper.TickWhisper(0.35f);
            Assert.That(whisper.CurrentAlpha, Is.LessThan(0.15f));
        }

        [UnityTest]
        public IEnumerator Whisper_DissolvesAndCompletes_OnJumpAction()
        {
            whisper = DiegeticWhisper.Spawn(
                new Vector3(1.5f, 0f, 0f),
                "SPACE",
                "LEAP ACROSS THE ABYSS",
                WhisperAction.Jump,
                "test_jump_pers"
            );

            yield return null;

            whisper.TickWhisper(0.2f);

            // Trigger action fulfillment
            whisper.TriggerActionFulfillment();

            Assert.That(whisper.IsDissolving, Is.True);

            // Advance dissolution past 0.35s
            whisper.TickWhisper(0.40f);

            Assert.That(whisper.IsCompleted, Is.True);
            Assert.That(PlayerPrefs.GetInt("TLR_Whisper_test_jump_pers", 0), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Whisper_Dissolves_OnFlareAction()
        {
            whisper = DiegeticWhisper.Spawn(
                new Vector3(1.5f, 0f, 0f),
                "F",
                "IGNITE FLARE TO PIERCE DARKNESS",
                WhisperAction.Flare,
                "test_flare_pers"
            );

            yield return null;

            whisper.TickWhisper(0.2f);

            whisper.TriggerActionFulfillment();
            Assert.That(whisper.IsDissolving, Is.True);

            // Advance dissolution past 0.35s
            whisper.TickWhisper(0.40f);

            Assert.That(whisper.IsCompleted, Is.True);
            Assert.That(PlayerPrefs.GetInt("TLR_Whisper_test_flare_pers", 0), Is.EqualTo(1));
        }
    }
}
