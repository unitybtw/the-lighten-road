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
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TheLightenRoad.Sirac.OfficeAnnex.Tests
{
    public sealed class OfficeAnnexTests
    {
        private Scene scene, previousScene;
        private OfficeAnnexRoute route;
        private PlayerMotor2D motor;
        private PlayerSpriteAnimator pose;
        private Rigidbody2D crate;
        private Keyboard keyboard, previousKeyboard;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            previousScene = SceneManager.GetActiveScene();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
#if UNITY_EDITOR
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/_Project/Scenes/Sandbox/Sirac/OfficeAnnex.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!scene.isLoaded) yield return null;
#else
            yield return SceneManager.LoadSceneAsync("OfficeAnnex", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("OfficeAnnex");
#endif
            SceneManager.SetActiveScene(scene);
            route = Find<OfficeAnnexRoute>();
            motor = route.Player.GetComponent<PlayerMotor2D>(); pose = motor.GetComponent<PlayerSpriteAnimator>();
            crate = route.Crate.GetComponent<Rigidbody2D>();
            yield return new WaitForSeconds(.3f);
            Assert.That(motor.IsGrounded, Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var reader = Object.FindAnyObjectByType<ArchiveCodexMenu>(); if (reader != null) reader.Close();
            Time.timeScale = 1;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>()).Single();
        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, message + " Player=" + motor.Body.position + " Crate=" + crate.position + " Pose=" + pose.CurrentAnimation);
        }
        private IEnumerator Warp(Vector2 position)
        {
            motor.Body.position = position; motor.transform.position = position; motor.Body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        }
        private IEnumerator StartPush()
        {
            yield return Warp(new Vector2(8.3f, .8f)); Keys(Key.D);
            yield return Until(() => route.Crate.DistanceMoved > .15f && pose.CurrentAnimation == LightAnimation.Push, 3, "Grounded contact should push and animate.");
        }

        [Test] public void SuppliedTilesFormCollidableTerrainAndDeskKeepsThePlayerInFront()
        {
            var maps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>()).ToArray();
            var textures = maps.SelectMany(m => m.GetTilesBlock(m.cellBounds)).OfType<Tile>().Select(t => t.sprite.texture.name).Distinct().ToArray();
            foreach (string name in new[] { "TileMapOffice_Gray_1", "TileMapOffice_Gray_2", "TileMapOffice_Blue_1", "TileMapOffice_Blue_2" })
                Assert.That(textures, Does.Contain(name));
            Assert.That(Physics2D.Raycast(new Vector2(3, .4f), Vector2.down, .8f).collider, Is.Not.Null);
            Assert.That(Physics2D.Raycast(new Vector2(18, 4.4f), Vector2.down, .8f).collider, Is.Not.Null);
            Assert.That(Physics2D.Raycast(new Vector2(27.5f, 2), Vector2.down, 4).collider, Is.Null, "The shaft must be open.");
            Assert.That(Find<DiegeticWhisper>().GetComponent<SpriteRenderer>().sortingOrder, Is.LessThan(pose.Renderer.sortingOrder));
            var push = pose.Catalog.Find(LightAnimation.Push);
            Assert.That(push.Frames.Length, Is.EqualTo(10)); Assert.That(push.Duration, Is.EqualTo(1).Within(.001f));
            Assert.That(push.Loop, Is.True); Assert.That(push.Frames[0].pivot.x, Is.EqualTo(42).Within(.01f));
        }
        [UnityTest] public IEnumerator GroundedContactLoopsPushAndReleasingInputReturnsToIdle()
        {
            yield return StartPush();
            int first = pose.CurrentFrame;
            yield return new WaitForSeconds(.24f);
            Assert.That(pose.CurrentFrame, Is.Not.EqualTo(first));
            Assert.That(crate.linearVelocity.x, Is.InRange(0, 1.6f));
            Keys(); yield return new WaitForSeconds(.25f);
            Assert.That(route.Crate.IsBeingPushed, Is.False);
            Assert.That(pose.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
            float reverseStart = crate.position.x;
            yield return Warp(new Vector2(reverseStart + 1.3f, .8f)); Keys(Key.A);
            yield return Until(() => crate.position.x < reverseStart - .15f && pose.CurrentAnimation == LightAnimation.Push,
                3, "The same crate must also push left.");
            Assert.That(pose.Renderer.flipX, Is.True);
        }
        [UnityTest] public IEnumerator PauseFreezesContactAndJumpReleasesPushPose()
        {
            yield return StartPush();
            var pause = Find<PauseMenuController>(); pause.Pause();
            Vector2 location = crate.position; int frame = pose.CurrentFrame;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(crate.position, Is.EqualTo(location)); Assert.That(pose.CurrentFrame, Is.EqualTo(frame));
            Keys(); pause.Resume(); yield return null; yield return null; yield return null;
            Keys(Key.Space, Key.D); yield return new WaitForSeconds(.12f);
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(route.Crate.IsBeingPushed, Is.False);
            Assert.That(pose.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        }
        [UnityTest] public IEnumerator RetryRestoresTheCrateAndUpperCheckpoint()
        {
            Vector2 start = crate.position;
            yield return StartPush(); Keys();
            route.Player.Kill(DeathReason.Hazard); route.Player.Respawn(); yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(crate.position, start), Is.LessThan(.04f));
            yield return Warp(new Vector2(18, 4.85f));
            yield return new WaitForSeconds(.2f);
            Assert.That(route.Player.SpawnPosition.x, Is.EqualTo(18.8f).Within(.01f));
            route.Player.Kill(DeathReason.Void); route.Player.Respawn(); yield return null;
            Assert.That(motor.Body.position.x, Is.EqualTo(18.8f).Within(.05f));
        }
        [UnityTest] public IEnumerator TheRouteCanBeTraversedAndItsLedgerOpens()
        {
            yield return StartPush();
            yield return Until(() => crate.position.x > 13.32f, 5, "The crate should reach the landing wall.");
            Keys(); yield return new WaitForSeconds(.12f);
            Keys(Key.Space, Key.D); yield return new WaitForSeconds(.6f);
            Keys(); yield return new WaitForSeconds(.2f);
            Assert.That(motor.Body.position.y, Is.GreaterThan(1.9f), "Land on the crate before the high climb.");
            Keys(Key.Space, Key.D); yield return new WaitForSeconds(.24f);
            Keys(Key.Space, Key.D, Key.W); yield return new WaitForSeconds(.1f); Keys();
            yield return Until(() => !motor.GetComponent<PlayerLedgeClimb>().IsClimbing && motor.IsGrounded && motor.Body.position.x > 14,
                4, "Jump and climb from the crate onto the records landing.");
            Keys(Key.D); yield return Until(() => motor.Body.position.x >= 25.45f, 4, "Reach the shaft approach.");
            Keys(); yield return null;
            Keys(Key.Space, Key.D); yield return new WaitForSeconds(.95f); Keys();
            yield return Until(() => motor.IsGrounded && motor.Body.position.x > 28, 2, "Jump the service shaft.");
            Keys(Key.D); yield return Until(() => motor.Body.position.x >= 33.3f, 2, "Reach the ledger desk.");
            Keys(); yield return new WaitForSeconds(.1f);
            Keys(Key.E); yield return null; yield return null; Keys(); yield return null;
            Assert.That(ArchiveCodexMenu.IsOpen, Is.True, "The desk must open the existing book reader.");
            Keys(Key.Escape); yield return null; yield return null; Keys(); yield return null; yield return null;
            Assert.That(ArchiveCodexMenu.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator ExitUsesTheExistingLanternTransitionAndReleasesInput()
        {
            yield return Warp(new Vector2(37.5f, 3.85f));
            var exit = Find<ChamberCompletionSequence>();
            Assert.That(exit.TryBegin(route.Player), Is.True);
            yield return Until(() => !exit.IsRunning, 6, "Completion should finish.");
            Assert.That(exit.Phase, Is.EqualTo("Complete"));
            yield return null; yield return null; yield return null;
            Assert.That(TheLightenRoad.Input.GameControls.GameplayAllowed, Is.True);
        }
    }
}
