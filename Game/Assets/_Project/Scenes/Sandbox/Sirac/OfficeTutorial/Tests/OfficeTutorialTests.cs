using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Lantern.Relays;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.FurkanPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace TheLightenRoad.Sirac.OfficeTutorial.Tests
{
    public sealed class OfficeTutorialTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sirac/OfficeTutorial.unity";
        private Scene scene;
        private FurkanLevelDirector level;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();

#if UNITY_EDITOR
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            level = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FurkanLevelDirector>()).Single();
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SceneLoadsWithGridTilemapsAndProps()
        {
            var grid = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Grid>()).FirstOrDefault();
            Assert.That(grid, Is.Not.Null, "OfficeTutorial must have a Grid.");

            var tilemaps = grid.GetComponentsInChildren<Tilemap>();
            Assert.That(tilemaps.Any(t => t.gameObject.name == "Tilemap_Floor"), Is.True, "Tilemap_Floor must exist.");
            Assert.That(tilemaps.Any(t => t.gameObject.name == "Tilemap_Wall"), Is.True, "Tilemap_Wall must exist.");

            var floorTm = tilemaps.First(t => t.gameObject.name == "Tilemap_Floor");
            Assert.That(floorTm.GetTile(new Vector3Int(1, -1, 0)), Is.Not.Null, "Spawn floor tile must be painted.");
            Assert.That(floorTm.GetTile(new Vector3Int(6, 0, 0)), Is.Not.Null, "Jump 1 step tile must be painted.");
            Assert.That(floorTm.GetTile(new Vector3Int(9, 1, 0)), Is.Not.Null, "Jump 2 step tile must be painted.");
            Assert.That(floorTm.GetTile(new Vector3Int(14, 2, 0)), Is.Not.Null, "Reception landing floor tile must be painted.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerSpawnsGroundedWithLanternAndStamina()
        {
            Assert.That(level.Player, Is.Not.Null, "Level must have a player assigned.");
            var motor = level.Player.GetComponent<PlayerMotor2D>();
            var fuel = level.Player.GetComponent<LanternFuel>();
            var stamina = level.Player.GetComponent<PlayerStamina>();

            Assert.That(motor, Is.Not.Null);
            Assert.That(fuel, Is.Not.Null);
            Assert.That(stamina, Is.Not.Null);

            float timeout = Time.time + 1.5f;
            while (!motor.IsGrounded && Time.time < timeout)
                yield return null;

            Assert.That(motor.IsGrounded, Is.True, "Player must spawn stably on the floor tilemap.");
        }

        [UnityTest]
        public IEnumerator TransitionRelayChargesAndOpensArchiveGate()
        {
            var relay = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PhotosensitiveRelay2D>()).FirstOrDefault();
            var gate = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArchiveGate2D>()).FirstOrDefault();

            Assert.That(relay, Is.Not.Null, "Transition relay must exist in scene.");
            Assert.That(gate, Is.Not.Null, "Archive gate must exist in scene.");
            Assert.That(gate.Relay, Is.SameAs(relay), "Gate must be connected to relay.");

            Assert.That(gate.IsOpen, Is.False, "Gate must be closed initially.");
            relay.ForceSetCharge(1.0f);
            relay.SetActivated(true);
            yield return new WaitForSeconds(0.1f);
            Assert.That(relay.IsActivated, Is.True, "Relay must be activated.");
            Assert.That(gate.IsOpen, Is.True, "Gate must open when relay activates.");
        }

        [UnityTest]
        public IEnumerator CircuitControlsPatrolPlatformAndWaypoints()
        {
            var fuel = level.Player.GetComponent<LanternFuel>();
            var platform = level.Circuit.Platform;
            Assert.That(platform, Is.Not.Null, "Patrol platform must be connected to circuit.");

            var body = platform.GetComponent<Rigidbody2D>();
            Vector2 initialPos = body.position;

            // Initially not powered
            Assert.That(level.Circuit.Powered, Is.False);
            Assert.That(platform.Powered, Is.False);

            // Deposit ember to power platform
            Assert.That(level.Circuit.TryTransfer(fuel), Is.True);
            Assert.That(level.Circuit.Powered, Is.True);
            Assert.That(platform.Powered, Is.True);

            yield return new WaitForSeconds(1.2f);
            Assert.That(Vector2.Distance(body.position, initialPos), Is.GreaterThan(0.2f), "Platform must move along waypoints when powered.");

            // Recover ember freezes platform
            Assert.That(level.Circuit.TryTransfer(fuel), Is.True);
            Assert.That(level.Circuit.Powered, Is.False);
            Assert.That(platform.Powered, Is.False);
        }

        [UnityTest]
        public IEnumerator SecretFragmentMementoGlowsAndIsCollectable()
        {
            Assert.That(level.Memento, Is.Not.Null, "Level must have a secret memento.");
            Assert.That(level.Memento.gameObject.activeSelf, Is.True, "Memento must be active.");
            Assert.That(level.HasMemento, Is.False);

            // Simulating collecting the fragment
            level.Memento.GetComponent<Collider2D>().SendMessage("OnTriggerEnter2D", level.Player.GetComponent<Collider2D>());
            Assert.That(level.HasMemento, Is.True, "Collecting memento must register on director.");
            Assert.That(level.Memento.gameObject.activeSelf, Is.False, "Memento must deactivate upon collection.");

            yield return null;
        }
    }
}
