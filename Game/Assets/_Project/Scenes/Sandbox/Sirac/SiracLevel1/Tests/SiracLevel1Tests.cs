using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Frontend;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TheLightenRoad.Sirac.Level1.Tests
{
    public sealed class SiracLevel1Tests
    {
        private Scene scene, previousScene;
        private SiracLevel1Route route;
        private PlayerMotor2D motor;
        private Keyboard keyboard, previousKeyboard;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();

#if UNITY_EDITOR
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/_Project/Scenes/Sandbox/Sirac/SiracLevel1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
#else
            yield return SceneManager.LoadSceneAsync("SiracLevel1", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("SiracLevel1");
#endif
            SceneManager.SetActiveScene(scene);
            route = Find<SiracLevel1Route>();
            motor = route.Player.GetComponent<PlayerMotor2D>();

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(motor.IsGrounded, Is.True, "Player must start grounded on the Reception Lounge floor.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var reader = Object.FindAnyObjectByType<ArchiveCodexMenu>();
            if (reader != null) reader.Close();
            Time.timeScale = 1f;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        private T Find<T>() where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        [UnityTest]
        public IEnumerator MaksimPropsAndCoreSystems_AreProperlyLoaded()
        {
            yield return null;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Crate, Is.Not.Null);
            Assert.That(route.BridgeRelay, Is.Not.Null);
            Assert.That(route.BridgePlatform, Is.Not.Null);
            Assert.That(route.WaterVein, Is.Not.Null);
            Assert.That(route.WaterHazard, Is.Not.Null);
            Assert.That(route.ExitRelay, Is.Not.Null);
            Assert.That(route.ExitGate, Is.Not.Null);

            var propsGroup = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Furniture & Props");
            Assert.That(propsGroup, Is.Not.Null);
            var renderers = propsGroup.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(25));
            Assert.That(renderers.All(r => r.sprite != null), Is.True, "Every prop sprite must resolve cleanly.");
            Assert.That(route.BridgeRelay.CrystalRenderer.sprite, Is.Not.Null, "Bridge relay crystal sprite must resolve.");
            Assert.That(route.ExitRelay.CrystalRenderer.sprite, Is.Not.Null, "Exit relay crystal sprite must resolve.");
            Assert.That(route.ExitGate.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Executive security gate sprite must resolve.");
            Assert.That(route.WaterVein.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Ember vein pedestal sprite must resolve.");
        }

        [UnityTest]
        public IEnumerator EmberVeinPurifiesWaterAndRelayOpensExitGate()
        {
            yield return null;
            Assert.That(route.WaterHazard.hazardZone.activeSelf, Is.True, "Water must start electrified.");

            route.WaterVein.Interact(route.Player.gameObject);
            yield return null;
            Assert.That(route.WaterHazard.hazardZone.activeSelf, Is.False, "Powering the vein must purify the water.");

            route.WaterVein.Interact(route.Player.gameObject);
            yield return null;
            Assert.That(route.WaterHazard.hazardZone.activeSelf, Is.True, "Draining the vein must re-electrify the water.");

            Assert.That(route.ExitGate.IsOpen, Is.False);
            route.ExitRelay.ForceSetCharge(1f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!route.ExitGate.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(route.ExitGate.IsOpen, Is.True, "Executive security gate must open when its relay latches.");
        }

        [UnityTest]
        public IEnumerator FlareGlyphOpensSecretPassageAndExitCompletesLevel()
        {
            yield return null;
            Assert.That(route.SecretCache.IsOpen, Is.False);
            route.SecretGlyph.ForceCharge();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!route.SecretCache.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(route.SecretCache.IsOpen, Is.True, "FlareGlyph must open SecretPassage.");

            var exit = Find<ChamberCompletionSequence>();
            Assert.That(exit, Is.Not.Null);
            bool started = exit.TryBegin(route.Player);
            Assert.That(started, Is.True, "Level 1 exit sequence must begin cleanly.");
            yield return null;
            exit.Cancel();
        }

        [UnityTest]
        public IEnumerator MovementLadderAndFlareCameraPan_WorkAsSpecified()
        {
            yield return null;
            var route = Find<SiracLevel1Route>();
            Assert.That(route, Is.Not.Null);
            Assert.That(route.TraversalRig, Is.Not.Null);
            Assert.That(route.MezzanineLadder, Is.Not.Null);
            Assert.That(route.TraversalRig.CalibratedJumpSpeed, Is.GreaterThan(0f));
            Assert.That(route.TraversalRig.CalibratedLedgeMaxHeight, Is.GreaterThanOrEqualTo(2f));

            Vector3 originalPos = route.Player.transform.position;
            route.TraversalRig.StartClimbing(route.MezzanineLadder);
            Assert.That(route.TraversalRig.IsClimbingLadder, Is.True, "Player must enter ladder climbing state.");
            float yBefore = route.Player.transform.position.y;
            route.TraversalRig.StepClimb(1f, 0.35f);
            Assert.That(route.Player.transform.position.y, Is.GreaterThan(yBefore), "Climbing up must raise player Y.");
            route.TraversalRig.StopClimbing();
            Assert.That(route.TraversalRig.IsClimbingLadder, Is.False);
            route.Player.transform.position = originalPos;

            route.TraversalRig.SetFlareScouting(true);
            Assert.That(route.TraversalRig.IsCharacterFrozenByFlare, Is.True, "Flare must freeze character movement.");
            route.TraversalRig.PanCamera(1f, 0.25f);
            Assert.That(route.TraversalRig.IsFlarePanning, Is.True);
            Assert.That(route.TraversalRig.CurrentPanOffsetX, Is.GreaterThan(0f), "A/D must pan camera horizontally during flare.");
            route.TraversalRig.SetFlareScouting(false);
            Assert.That(route.TraversalRig.IsCharacterFrozenByFlare, Is.False);
        }

        [UnityTest]
        public IEnumerator LanternLensesAndFourStateVeinMechanisms_WorkAsSpecified()
        {
            yield return null;
            var route = Find<SiracLevel1Route>();
            Assert.That(route, Is.Not.Null);
            Assert.That(route.LensController, Is.Not.Null);
            Assert.That(route.EmberInventory, Is.Not.Null);
            Assert.That(route.MultiStateVein, Is.Not.Null);

            route.LensController.SetLens(LanternLensType.Normal);
            Assert.That(route.LensController.ActiveLens, Is.EqualTo(LanternLensType.Normal));
            route.LensController.CycleLens(1);
            Assert.That(route.LensController.ActiveLens, Is.EqualTo(LanternLensType.Ethereal));
            route.LensController.CycleLens(1);
            Assert.That(route.LensController.ActiveLens, Is.EqualTo(LanternLensType.Invert));
            route.LensController.CycleLens(-1);
            Assert.That(route.LensController.ActiveLens, Is.EqualTo(LanternLensType.Ethereal));

            Assert.That(route.GhostDoorMech, Is.Not.Null);
            Assert.That(route.GhostCrateMech, Is.Not.Null);
            Assert.That(route.GhostElevatorMech, Is.Not.Null);
            route.GhostDoorMech.StepMechanism(0.05f);
            route.GhostCrateMech.StepMechanism(0.05f);
            Assert.That(route.GhostDoorMech.IsPhasedOut, Is.True, "Ethereal lens must phase out ghost door collision.");
            Assert.That(route.GhostCrateMech.IsPhasedOut, Is.True, "Ethereal lens must phase out ghost crate collision.");

            route.LensController.SetLens(LanternLensType.Normal);
            route.GhostDoorMech.StepMechanism(0.05f);
            route.GhostCrateMech.StepMechanism(0.05f);
            Assert.That(route.GhostCrateMech.IsPhasedOut, Is.False);

            route.EmberInventory.AddEmber(EmberColor.Orange);
            route.EmberInventory.AddEmber(EmberColor.Cyan);
            route.EmberInventory.AddEmber(EmberColor.Crimson);
            Assert.That(route.EmberInventory.HasEmber(EmberColor.Orange), Is.True);
            Assert.That(route.EmberInventory.HasEmber(EmberColor.Cyan), Is.True);
            Assert.That(route.EmberInventory.HasEmber(EmberColor.Crimson), Is.True);

            route.MultiStateVein.SetSlottedEmberDirect(EmberColor.Orange);
            Assert.That(route.MultiStateVein.VeinStateValue, Is.EqualTo(1));
            Assert.That(route.EmberInventory.HasEmber(EmberColor.Orange), Is.True, "Embers must never be consumed.");

            route.MultiStateVein.SetSlottedEmberDirect(EmberColor.Cyan);
            Assert.That(route.MultiStateVein.VeinStateValue, Is.EqualTo(2));

            route.MultiStateVein.SetSlottedEmberDirect(EmberColor.Crimson);
            Assert.That(route.MultiStateVein.VeinStateValue, Is.EqualTo(-1));

            route.EmberInventory.SelectColor(EmberColor.Crimson);
            route.MultiStateVein.TryInteract(route.EmberInventory);
            Assert.That(route.MultiStateVein.VeinStateValue, Is.EqualTo(0));

            Assert.That(route.BlastDoorMech, Is.Not.Null);
            Assert.That(route.PistonMech, Is.Not.Null);
            Assert.That(route.ElevatorMech, Is.Not.Null);
            Assert.That(route.MovingPlatformMech, Is.Not.Null);
            Assert.That(route.MovingBlockMech, Is.Not.Null);
            Assert.That(route.PusherMech, Is.Not.Null);
            Assert.That(route.ElevatorMech.ElevatorDwellTime, Is.EqualTo(3f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ConduitAndLightMirrorSystems_RoutePowerMeltDarknessAndCleanseVein()
        {
            yield return null;
            var route = Find<SiracLevel1Route>();
            Assert.That(route, Is.Not.Null);
            Assert.That(route.ConduitPuzzle, Is.Not.Null);
            Assert.That(route.ConduitPuzzle.Nodes.Count, Is.GreaterThanOrEqualTo(3));

            route.ConduitPuzzle.RandomizeLayout(42);
            route.ConduitPuzzle.EvaluateNetwork();
            route.ConduitPuzzle.SolveAllNodes();
            route.ConduitPuzzle.EvaluateNetwork();
            Assert.That(route.ConduitPuzzle.IsOutputPowered, Is.True, "Solved conduit network must route power to output.");

            float deadline = Time.realtimeSinceStartup + 2f;
            while (route.ConduitPuzzle.TargetSecretPanel != null && !route.ConduitPuzzle.TargetSecretPanel.IsOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (route.ConduitPuzzle.TargetSecretPanel != null)
                Assert.That(route.ConduitPuzzle.TargetSecretPanel.IsOpen, Is.True, "Powered conduit must slide open secret panel.");

            Assert.That(route.MainProjector, Is.Not.Null);
            Assert.That(route.RotatableMirror, Is.Not.Null);
            Assert.That(route.CorruptedMirror, Is.Not.Null);
            Assert.That(route.CorruptedMirror.IsCorrupted, Is.True);
            Assert.That(route.MirrorRail, Is.Not.Null);
            Assert.That(route.DarknessBarrier, Is.Not.Null);
            Assert.That(route.CorruptedVein, Is.Not.Null);

            route.RotatableMirror.RotateMirror(15f);
            route.MirrorRail.StepRail(0.4f);
            route.MainProjector.EvaluateBeam(0.1f);
            Assert.That(route.MainProjector.LastBeamPoints, Is.Not.Null);

            route.DarknessBarrier.ApplyBeamHit(2.5f);
            Assert.That(route.DarknessBarrier.IsMelted, Is.True, "Reflected projector beam must melt darkness barrier.");

            Assert.That(route.CorruptedVein.IsCorrupted, Is.True);
            route.CorruptedVein.CleanseCorruption();
            Assert.That(route.CorruptedVein.IsCorrupted, Is.False, "Cleansing corrupted vein must restore it.");
        }

        [UnityTest]
        public IEnumerator ShadowRealmLevelFlowMementosAndFourBeamFinale_CompleteTheGame()
        {
            yield return null;
            var route = Find<SiracLevel1Route>();
            Assert.That(route, Is.Not.Null);
            Assert.That(route.ShadowRealm, Is.Not.Null);
            Assert.That(route.HudOverlay, Is.Not.Null);
            Assert.That(route.ScriptedFall, Is.Not.Null);
            Assert.That(route.CentralCoreFinale, Is.Not.Null);

            route.ShadowRealm.SetPlayerInside(true);
            yield return null;
            Assert.That(route.ShadowRealm.IsPlayerInside, Is.True);
            Assert.That(route.LensController.IsSuppressedByShadowRealm, Is.True, "Shadow realm must extinguish lantern/lens.");
            Assert.That(route.HudOverlay.IsMonochromeRedActive, Is.True, "Shadow realm must activate red monochrome HUD filter.");

            route.ShadowRealm.SetPlayerInside(false);
            yield return null;
            Assert.That(route.ShadowRealm.IsPlayerInside, Is.False);
            Assert.That(route.LensController.IsSuppressedByShadowRealm, Is.False);
            Assert.That(route.HudOverlay.IsMonochromeRedActive, Is.False);

            route.ScriptedFall.TriggerFall();
            Assert.That(route.ScriptedFall.HasTriggeredFall, Is.True, "Scripted breakaway floor fall must trigger.");

            Assert.That(route.Mementos, Is.Not.Null);
            Assert.That(route.Mementos.Length, Is.GreaterThanOrEqualTo(2));
            bool foundSecret = false;
            foreach (var memento in route.Mementos)
            {
                if (memento.IsSecretMemento) foundSecret = true;
                memento.Collect();
                Assert.That(memento.IsCollected, Is.True);
            }
            Assert.That(foundSecret, Is.True, "Level must include at least one hidden memento.");

            Assert.That(route.CentralCoreFinale.RequiredBeams, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
                route.CentralCoreFinale.RegisterDirectBeam();
            yield return null;
            Assert.That(route.CentralCoreFinale.IsGameEndingTriggered, Is.True, "4 beams reaching central core must trigger game ending.");
        }
    }
}
