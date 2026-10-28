using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Frontend;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TheLightenRoad.FirstLight.Tests
{
    public sealed class FirstLightTutorialTests
    {
        private FirstLightTutorial tutorial;
        private Scene scene,previous;
        private Keyboard keyboard,original;
        private bool reduced;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        [UnitySetUp] public IEnumerator Setup()
        {
            reduced=MenuPreferences.ReducedMotion;MenuPreferences.SetReducedMotion(false);
            oldBackground=InputSystem.settings.backgroundBehavior;oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("First light regression keyboard");
            previous=SceneManager.GetActiveScene();yield return SceneManager.LoadSceneAsync(FirstLightTutorial.ScenePath,LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByPath(FirstLightTutorial.ScenePath);SceneManager.SetActiveScene(scene);
            tutorial=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FirstLightTutorial>()).Single();
            tutorial.AutomaticTicks=false;tutorial.LoadSceneOnExit=false;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var departure in Object.FindObjectsByType<FirstLightDeparture>(FindObjectsSortMode.None))Object.Destroy(departure.gameObject);
            if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.RemoveDevice(keyboard);if(original!=null && original.added)original.MakeCurrent();
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
            MenuPreferences.SetReducedMotion(reduced);Time.timeScale=1;yield return null;
        }
        private void Ignite()
        {
            tutorial.Tick(3);Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.FindEmber));
            Assert.That(tutorial.FirstEmber.CollectImmediate(tutorial.Lantern.Fuel),Is.True);tutorial.Tick(.01f);
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Ignition));tutorial.Tick(1.9f);
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Move));
        }
        private void Place(float x,float y=1.2f)
        {tutorial.Player.transform.position=new Vector3(x,y);var body=tutorial.Player.GetComponent<PlayerMotor2D>().Body;body.position=new Vector2(x,y);body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
        private IEnumerator Scout()
        {
            Ignite();Place(4.9f,.95f);tutorial.Tick(.01f);
            for(int i=0;i<6;i++)yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;yield return new WaitForFixedUpdate();
            Assert.That(tutorial.HasJumped,Is.True,"The real motor must publish its jump from the actual Input System binding.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Place(12);tutorial.Tick(.01f);Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Gather));
            if(tutorial.ReserveA!=null && !tutorial.ReserveA.IsCollected)tutorial.ReserveA.CollectImmediate(tutorial.Lantern.Fuel);
            if(tutorial.ReserveB!=null && !tutorial.ReserveB.IsCollected)tutorial.ReserveB.CollectImmediate(tutorial.Lantern.Fuel);
            tutorial.Tick(.01f);Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Scout));
        }
        private void WakeMarks()
        {Place(21);Assert.That(tutorial.Lantern.SetFlared(true),Is.True);tutorial.Lantern.TickScout(.8f);tutorial.Tick(1.3f);}
        [UnityTest] public IEnumerator OpeningIsActuallyBlackAndDoesNotKillAPlayerWaitingForTheFirstEmber()
        {
            Assert.That(tutorial.Presentation.BlackVeil.color.a,Is.EqualTo(1));Assert.That(tutorial.Lantern.Fuel.IsEmpty,Is.True);
            Assert.That(tutorial.Player.KillOnFuelDepleted,Is.False);tutorial.Player.TickSurvival(20);
            Assert.That(tutorial.Player.State,Is.EqualTo(RespawnState.Alive));tutorial.Tick(3);
            yield return null;yield return null;
            Assert.That(GameControls.GameplayAllowed,Is.True);Assert.That(tutorial.FirstEmber.gameObject.activeSelf,Is.True);
        }
        [UnityTest] public IEnumerator RealPickupIgnitesOneEmberAndReleasesThePlayerOnlyAfterTheReveal()
        {
            tutorial.Tick(3);tutorial.FirstEmber.CollectImmediate(tutorial.Lantern.Fuel);tutorial.Tick(.01f);
            Assert.That(GameControls.GameplayAllowed,Is.False);Assert.That(tutorial.Lantern.Fuel.TotalEmbers,Is.EqualTo(1));
            tutorial.Tick(.9f);Assert.That(tutorial.Lantern.LanternLight.intensity,Is.GreaterThan(0));
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Ignition));tutorial.Tick(1);
            Assert.That(tutorial.Player.KillOnFuelDepleted,Is.True);Assert.That(tutorial.Lantern.enabled,Is.True);yield return null;yield return null;Assert.That(GameControls.GameplayAllowed,Is.True);
        }
        [UnityTest] public IEnumerator RealJumpAndTwoPickupsUnlockTheScoutLesson()
        {yield return Scout();Assert.That(tutorial.CollectedReserves,Is.EqualTo(2));Assert.That(tutorial.Lantern.Fuel.TotalEmbers,Is.EqualTo(3));}
        [UnityTest] public IEnumerator DimAndRemoteFlaresCannotOpenTheWhiteDoor()
        {
            yield return Scout();Place(21);tutorial.Tick(2);Assert.That(tutorial.Marks.All(m=>m==0),Is.True);
            Place(16.4f);tutorial.Lantern.SetFlared(true);tutorial.Lantern.TickScout(.8f);tutorial.Tick(2);
            Assert.That(tutorial.Marks.All(m=>m==0),Is.True);Assert.That(tutorial.CanEnterDoor,Is.False);
            Assert.That(tutorial.TryEnterDoor(tutorial.Player),Is.False);
        }
        [UnityTest] public IEnumerator OneRealFlareChargesThreeMarksAndDrawsAnOrderedDoor()
        {
            yield return Scout();WakeMarks();
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.DrawingDoor));Assert.That(tutorial.Lantern.Fuel.TotalEmbers,Is.EqualTo(2));
            Assert.That(GameControls.GameplayAllowed,Is.False);tutorial.Tick(1);
            Assert.That(tutorial.Presentation.DoorOutline.enabled,Is.True);Assert.That(tutorial.CanEnterDoor,Is.False);
            tutorial.Tick(1.9f);Assert.That(tutorial.CanEnterDoor,Is.True);Assert.That(tutorial.Presentation.DoorCore.color.a,Is.GreaterThan(.9f));
            foreach(var line in tutorial.Presentation.Conduits)Assert.That(line.GetPosition(line.positionCount-1).x,Is.EqualTo(30.2f).Within(.01f),"Completed traces must not restart when the door becomes ready.");
            yield return null;yield return null;Assert.That(GameControls.GameplayAllowed,Is.True);Assert.That(tutorial.DestinationScene,Is.EqualTo(FirstLightTutorial.FirstLevel));
            Assert.That(Application.CanStreamedLevelBeLoaded(tutorial.DestinationScene),Is.True);
        }
        [UnityTest] public IEnumerator DeathAtTheSealKeepsProgressAndRestoresEnoughFuelToRetry()
        {
            yield return Scout();Place(21);tutorial.Lantern.Fuel.TrySpendFuel(6);
            tutorial.Player.Kill(DeathReason.Darkness);tutorial.Player.Respawn();
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Scout));Assert.That(tutorial.Lantern.Fuel.TotalEmbers,Is.EqualTo(3));
            Assert.That(tutorial.Player.transform.position.x,Is.EqualTo(16.4f).Within(.05f));yield return null;yield return null;Assert.That(GameControls.GameplayAllowed,Is.True);
        }
        [UnityTest] public IEnumerator WhitePassageFinishesAndReleasesInputWithoutLeavingAPersistentCurtain()
        {
            yield return Scout();WakeMarks();tutorial.Tick(3);
            Assert.That(tutorial.TryEnterDoor(null),Is.False);Assert.That(tutorial.TryEnterDoor(tutorial.Player),Is.True);
            Assert.That(tutorial.TryEnterDoor(tutorial.Player),Is.False);
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(tutorial.Stage,Is.EqualTo(FirstLightStage.Complete));Assert.That(GameControls.GameplayAllowed,Is.True);
            Assert.That(Object.FindAnyObjectByType<FirstLightDeparture>(),Is.Null);
        }
        [Test] public void ReducedMotionKeepsTheTutorialReadableWithoutOrbitOrZoom()
        {
            MenuPreferences.SetReducedMotion(true);tutorial.Tick(3);
            Assert.That(tutorial.Presentation.SeedRing.enabled,Is.False);Assert.That(tutorial.Presentation.SceneCamera.orthographicSize,Is.EqualTo(4.6f));
            Assert.That(tutorial.Presentation.Hint.text,Does.Contain("Approach"));
        }
        [UnityTest] public IEnumerator DisablingTheDirectorDuringTheIntroNeverLeavesGlobalInputLocked()
        {Assert.That(GameControls.GameplayAllowed,Is.False);tutorial.enabled=false;yield return null;yield return null;Assert.That(GameControls.GameplayAllowed,Is.True);}
    }
}
