using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Frontend;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TheLightenRoad.Sirac.OfficeLevel.Tests
{
    public sealed class OfficeLevelTests
    {
        private Scene scene, previousScene;
        private OfficeLevelRoute route;
        private PlayerMotor2D motor;
        private Rigidbody2D crate;
        private Keyboard keyboard, previousKeyboard;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private Camera camera;
        private FollowCamera2D follow;
        private ChamberCompletionSequence exit;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;previousScene=SceneManager.GetActiveScene();
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
#if UNITY_EDITOR
            scene=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Sandbox/Sirac/Office.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            while(!scene.isLoaded)yield return null;
#else
            Assert.Ignore("Authored scene integration is checked in Editor Play Mode.");yield break;
#endif
            SceneManager.SetActiveScene(scene);
            route=Find<OfficeLevelRoute>();motor=route.Player.GetComponent<PlayerMotor2D>();crate=route.Crate.GetComponent<Rigidbody2D>();
            camera=All<Camera>().Single(c=>c.CompareTag("MainCamera"));follow=camera.GetComponent<FollowCamera2D>();exit=Find<ChamberCompletionSequence>();
            yield return new WaitForSeconds(.3f);Assert.That(motor.IsGrounded,Is.True);AssertView();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(exit!=null && exit.IsRunning)exit.Cancel();
            Time.timeScale=1;
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(previousScene.IsValid() && previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
            if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
        private T[] All<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private T Find<T>() where T:Component => All<T>().Single();
        private void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        private void AssertView()
        {
            Assert.That(follow.HasWorldBounds,Is.True);
            Rect area=follow.WorldBounds;
            foreach(var uv in new[]{Vector2.zero,Vector2.one,Vector2.right,Vector2.up})
            {
                var p=camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,10));
                Assert.That(p.x,Is.InRange(area.xMin-.002f,area.xMax+.002f));
                Assert.That(p.y,Is.InRange(area.yMin-.002f,area.yMax+.002f));
            }
        }
        private IEnumerator Until(Func<bool> condition,float seconds,string message)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<end){AssertView();yield return null;}
            Assert.That(condition(),Is.True,message+" actor="+motor.Body.position+" crate="+crate.position+" pose="+motor.GetComponent<PlayerSpriteAnimator>().CurrentAnimation);
        }
        private IEnumerator Warp(Vector2 p)
        { motor.Body.position=p;motor.transform.position=p;motor.Body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate(); }
        private IEnumerator Push()
        {
            yield return Warp(new Vector2(4.6f,-2.15f));Keys(Key.D);
            yield return Until(()=>route.Crate.DistanceMoved>.15f,3,"Contact should physically move the crate.");
            Assert.That(motor.GetComponent<PlayerSpriteAnimator>().CurrentAnimation,Is.EqualTo(LightAnimation.Push));
        }
        [Test] public void BothBooksAndAllTerrainStagesArePresentWithAnOpenServiceShaft()
        {
            var books=All<DiegeticWhisper>().Where(w=>w.ArchiveLog!=null).ToArray();Assert.That(books.Length,Is.EqualTo(2));
            Assert.That(books.Select(b=>b.ArchiveLog.Id),Is.EquivalentTo(new[]{"office-ledger","office-margin"}));
            foreach(var book in books)
            {
                var desk=book.transform.Find("Filing desk and ledger").GetComponent<SpriteRenderer>();
                float floor=book.ArchiveLog.Id=="office-ledger"?-3:-1;
                Assert.That(desk.bounds.min.y,Is.EqualTo(floor).Within(.03f));
                Assert.That(desk.sortingOrder,Is.LessThan(motor.GetComponent<PlayerSpriteAnimator>().Renderer.sortingOrder));
            }
            foreach(var sample in new[]{new Vector2(0,-3),new Vector2(15,0),new Vector2(26,-1),new Vector2(31,1)})
            {
                var hit=Physics2D.Raycast(sample+Vector2.up*.3f,Vector2.down,.6f);
                Assert.That(hit.collider,Is.Not.Null,"Missing floor at "+sample);
            }
            Assert.That(Physics2D.Raycast(new Vector2(21,-1),Vector2.down,2).collider,Is.Null);
        }
        [UnityTest] public IEnumerator CratePushesAndReleasesInBothDirections()
        {
            yield return Push();float x=crate.position.x;Keys();yield return new WaitForSeconds(.15f);
            Assert.That(route.Crate.IsBeingPushed,Is.False);
            yield return Warp(new Vector2(x+1.3f,-2.15f));Keys(Key.A);
            yield return Until(()=>crate.position.x<x-.1f,3,"The crate should also move left.");
            Assert.That(motor.GetComponent<PlayerSpriteAnimator>().Renderer.flipX,Is.True);
        }
        [UnityTest] public IEnumerator RetryRestoresTheCrateAndKeepsTheRecordsCheckpoint()
        {
            Vector2 original=crate.position;yield return Push();Keys();route.Player.Kill(DeathReason.Hazard);route.Player.Respawn();yield return new WaitForFixedUpdate();
            Assert.That(crate.position.x,Is.EqualTo(original.x).Within(.03f));
            yield return Warp(new Vector2(14.4f,.85f));yield return new WaitForSeconds(.15f);
            Assert.That(route.Player.SpawnPosition.x,Is.EqualTo(14.8f).Within(.01f));
            route.Player.Kill(DeathReason.Hazard);route.Player.Respawn();yield return null;
            Assert.That(motor.Body.position.x,Is.EqualTo(14.8f).Within(.03f));AssertView();
        }
        [UnityTest] public IEnumerator CameraStaysInsideTheOfficeAtBothEndsAcrossAspectRatios()
        {
            foreach(float aspect in new[]{4f/3,16f/9,21f/9,32f/9})
            {
                camera.aspect=aspect;
                foreach(var p in new[]{new Vector2(-8,-2.15f),new Vector2(32,1.85f)})
                {
                    yield return Warp(p);follow.SetTarget(motor.transform,true);AssertView();
                    follow.Shake(.5f,.2f);yield return new WaitForSeconds(.08f);AssertView();
                }
            }
        }
        [UnityTest] public IEnumerator TheWholeOfficeCanBeCompletedWithWalkingPushingAndTwoClimbs()
        {
            yield return Push();
            yield return Until(()=>crate.position.x>10.32f,5,"Push the crate against the records landing.");
            Keys();yield return new WaitForSeconds(.1f);
            Keys(Key.D,Key.Space);yield return new WaitForSeconds(.6f);Keys();yield return new WaitForSeconds(.15f);
            Assert.That(motor.Body.position.y,Is.GreaterThan(-1.2f),"Land on the crate.");
            Keys(Key.D,Key.W);yield return new WaitForSeconds(.12f);Keys();
            yield return Until(()=>motor.IsGrounded && motor.Body.position.x>11,4,"Climb the records landing from the crate.");
            Keys(Key.D);yield return Until(()=>motor.Body.position.x>19.5f,4,"Cross the records gallery.");
            Keys();yield return null;Keys(Key.D,Key.Space);yield return new WaitForSeconds(.95f);Keys();
            yield return Until(()=>motor.IsGrounded && motor.Body.position.x>22,2,"Jump the service shaft at walking speed.");
            Keys(Key.D);yield return Until(()=>motor.Body.position.x>28.5f,3,"Reach the upper files riser.");
            Keys();yield return null;Keys(Key.D,Key.W);yield return new WaitForSeconds(.12f);Keys();
            yield return Until(()=>motor.IsGrounded && motor.Body.position.x>29,4,"Climb to the exit landing.");
            Keys(Key.D);yield return Until(()=>exit.IsRunning,3,"The exit should begin the lantern-led completion.");Keys();
            yield return Until(()=>exit.HasCompleted && !exit.IsRunning,8,"Completion must finish and release the player.");
            Assert.That(motor.enabled && motor.Body.simulated,Is.True);AssertView();
        }
    }
}
