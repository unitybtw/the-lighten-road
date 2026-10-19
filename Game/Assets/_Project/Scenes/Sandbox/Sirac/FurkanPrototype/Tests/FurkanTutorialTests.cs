using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.FurkanPrototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class FurkanTutorialTests
{
    private Scene scene;
    private FurkanLevelDirector level;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior oldBackground;
    private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
    [UnitySetUp] public IEnumerator SetUp()
    {
        oldBackground=InputSystem.settings.backgroundBehavior;oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard=InputSystem.AddDevice<Keyboard>();
#if UNITY_EDITOR
        scene=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Sandbox/Sirac/FurkanTutorial.unity",new LoadSceneParameters(LoadSceneMode.Additive));
#endif
        yield return null;
        level=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FurkanLevelDirector>()).Single();
        yield return new WaitForSeconds(.3f);
    }
    [UnityTearDown] public IEnumerator TearDown()
    {
        if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
        if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
    }
    [UnityTest] public IEnumerator BothNodesShareOneDepositAndFullRecoveryNeverLosesIt()
    {
        var fuel=level.Player.GetComponent<LanternFuel>();
        Assert.That(level.Nodes[0].Circuit,Is.SameAs(level.Nodes[1].Circuit));
        Assert.That(level.Nodes[0].Circuit.TryTransfer(fuel),Is.True);
        Assert.That(fuel.TotalEmbers,Is.EqualTo(2));
        fuel.AddEmbers(2);Assert.That(fuel.TotalEmbers,Is.EqualTo(4));
        Assert.That(level.Nodes[1].Circuit.TryTransfer(fuel),Is.False);
        Assert.That(level.Circuit.Powered,Is.True);
        fuel.TrySpendEmber();Assert.That(level.Nodes[1].Circuit.TryTransfer(fuel),Is.True);
        Assert.That(fuel.TotalEmbers,Is.EqualTo(4));Assert.That(level.Circuit.Powered,Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator PlatformMovesThenDrainingFreezesItsExactPosition()
    {
        var fuel=level.Player.GetComponent<LanternFuel>();var platform=level.Circuit.Platform;
        var body=platform.GetComponent<Rigidbody2D>();Vector2 start=body.position;
        level.Circuit.TryTransfer(fuel);yield return new WaitForSeconds(1.4f);
        Assert.That(Vector2.Distance(body.position,start),Is.GreaterThan(.3f));
        level.Circuit.TryTransfer(fuel);Vector2 stopped=body.position;
        yield return new WaitForSeconds(.3f);
        Assert.That(Vector2.Distance(body.position,stopped),Is.LessThan(.025f));
        Assert.That(fuel.TotalEmbers,Is.EqualTo(3));
    }
    [UnityTest] public IEnumerator ChamberRetryResetsDepositPlatformAndThreeEmberBudget()
    {
        level.EnterSecondChamber(level.SecondSpawn);
        level.Circuit.TryTransfer(level.Player.GetComponent<LanternFuel>());
        yield return new WaitForSeconds(.2f);
        level.Player.Kill(DeathReason.Hazard);level.Player.Respawn();yield return new WaitForFixedUpdate();
        Assert.That(level.Chamber,Is.EqualTo(2));Assert.That(level.Circuit.Powered,Is.False);
        Assert.That(level.Player.GetComponent<LanternFuel>().TotalEmbers,Is.EqualTo(3));
        Assert.That(level.Circuit.Platform.GetComponent<Rigidbody2D>().position,Is.EqualTo(level.Circuit.Platform.Waypoints[0]));
        Assert.That(level.PickupRoot.GetComponentsInChildren<EmberPickup>().Length,Is.EqualTo(2));
    }
    private IEnumerator Move(float x,bool jump=false)
    {
        var motor=level.Player.GetComponent<PlayerMotor2D>();float end=Time.time+7;
        while(motor.Body.position.x<x && Time.time<end)
        {
            InputSystem.QueueStateEvent(keyboard,jump?new KeyboardState(Key.D,Key.Space):new KeyboardState(Key.D));
            Assert.That(level.Player.State,Is.EqualTo(RespawnState.Alive),"Died at "+motor.Body.position);
            yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.5f);
        Assert.That(motor.Body.position.x,Is.GreaterThanOrEqualTo(x-.15f),"Blocked at "+motor.Body.position);
    }
    [UnityTest] public IEnumerator TutorialStepsAndDeskReachSecondChamberThroughRealMovement()
    {
        yield return Move(4.15f);yield return Move(5.7f,true);
        yield return Move(6.25f);yield return Move(8.3f,true);
        yield return Move(9.15f);yield return Move(10.6f,true);
        yield return Move(13.6f);yield return Move(15.1f,true);
        yield return Move(18.0f);
        Assert.That(level.Chamber,Is.EqualTo(2));
        Assert.That(level.ReturnShutter.activeSelf,Is.True);
        Assert.That(level.Player.GetComponent<LanternFuel>().TotalEmbers,Is.EqualTo(3));
    }
    [UnityTest] public IEnumerator CompactCrossingAndMovingShelfLeadToTheExit()
    {
        var motor=level.Player.GetComponent<PlayerMotor2D>();
        level.EnterSecondChamber(level.SecondSpawn);motor.Body.position=level.SecondSpawn;
        yield return new WaitForSeconds(.4f);
        Assert.That(level.Player.GetComponent<LanternController>().SetFlared(true),Is.True);
        yield return Move(18.65f);yield return Move(20.65f,true);
        yield return Move(21.0f);yield return Move(22.9f,true);
        yield return Move(23.15f);yield return Move(25.3f,true);
        yield return Move(28.3f);
        var fuel=level.Player.GetComponent<LanternFuel>();int held=fuel.TotalEmbers;
        // Board the drained shelf first. Node A remains within interaction range.
        yield return Move(29.65f,true);
        Assert.That(motor.IsGrounded,Is.True,"Boarding pad must be stable before activation.");
        Assert.That(Vector2.Distance(motor.Body.position,level.Nodes[0].transform.position),Is.LessThan(2.1f));
        Assert.That(level.Circuit.TryTransfer(fuel),Is.True);
        float timeout=Time.time+6f;
        while(level.Circuit.Platform.Destination!=2 && Time.time<timeout)
        {
            Assert.That(level.Player.State,Is.EqualTo(RespawnState.Alive),"Shelf ride failed at "+motor.Body.position+" platform="+level.Circuit.Platform.transform.position);
            yield return null;
        }
        Assert.That(level.Circuit.Platform.Destination,Is.EqualTo(2),"Shelf must reach the upper stop.");
        yield return new WaitForSeconds(.2f);
        Assert.That(motor.Body.position.y,Is.GreaterThan(5.8f),"Player must travel on the moving shelf.");
        yield return Move(36.2f,true);
        Assert.That(level.Circuit.TryTransfer(fuel),Is.True);
        Assert.That(fuel.TotalEmbers,Is.EqualTo(held));
        yield return Move(40.6f);
        Assert.That(level.IsComplete,Is.True);
    }
    [UnityTest] public IEnumerator UpperPlatformDetourCanReachTheOptionalMemory()
    {
        level.EnterSecondChamber(level.SecondSpawn);
        var platform=level.Circuit.Platform;
        platform.GetComponent<Rigidbody2D>().position=platform.Waypoints[1];
        var motor=level.Player.GetComponent<PlayerMotor2D>();
        motor.Body.position=new Vector2(34f,6.35f);
        yield return new WaitForSeconds(.3f);
        float timeout=Time.time+2f;
        while(!level.HasMemento && Time.time<timeout)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A,Key.Space));
            yield return null;
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        Assert.That(level.HasMemento,Is.True,"Upper ledge must be reachable with the real jump.");
    }
}
