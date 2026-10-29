using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using TheLightenRoad.Sirac.LevelMap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RuinedArchiveRouteTests
{
    private Scene scene;
    private RuinedArchiveRoute route;
    private PlayerRespawnController player;
    private PlayerMotor2D motor;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior oldBackground;
    private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;
        oldBackground = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
#if UNITY_EDITOR
        scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        while (!scene.isLoaded) yield return null;
        SceneManager.SetActiveScene(scene);
#else
        Assert.Ignore("Authored scene integration runs in the Editor.");
        yield break;
#endif
        route = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RuinedArchiveRoute>()).Single();
        player = route.Player;
        motor = player.GetComponent<PlayerMotor2D>();
        yield return new WaitForSeconds(0.4f);
        Assert.That(motor.IsGrounded, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = oldBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        if (scene.IsValid() && scene.isLoaded)
        {
            if (SceneManager.GetActiveScene() == scene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s != scene && s.isLoaded)
                    {
                        SceneManager.SetActiveScene(s);
                        break;
                    }
                }
            }
            var op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
        }
        LogAssert.ignoreFailingMessages = false;
    }

    private IEnumerator WalkTo(float x)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        float timeout = Time.time + 15f;
        while (motor.Body.position.x < x && Time.time < timeout)
        {
            // Keep the simulated key held across Editor focus/device changes.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            Assert.That(player.State, Is.EqualTo(RespawnState.Alive), "Unexpected death at " + motor.Body.position);
            yield return new WaitForFixedUpdate();
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.06f);
        Assert.That(motor.Body.position.x, Is.GreaterThanOrEqualTo(x - 0.1f), "Blocked walking to " + x + " at " + motor.Body.position + " velocity=" + motor.Body.linearVelocity);
    }

    private IEnumerator SprintJumpTo(float x, float minFloor)
    {
        Assert.That(motor.IsGrounded, Is.True, "Jump needs a safe launch pad at " + motor.Body.position);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift, Key.Space));
        yield return new WaitForSeconds(0.08f);
        float timeout = Time.time + 1.5f;
        while (motor.Body.position.x < x && Time.time < timeout)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift, Key.Space));
            Assert.That(player.State, Is.EqualTo(RespawnState.Alive), "Jump hit a hazard at " + motor.Body.position);
            yield return new WaitForFixedUpdate();
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.6f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.05f);
        Assert.That(player.State, Is.EqualTo(RespawnState.Alive));
        Assert.That(motor.Body.position.x, Is.GreaterThan(x - 0.2f));
        Assert.That(motor.IsGrounded, Is.True, "No safe landing at " + motor.Body.position);
        Assert.That(motor.Body.position.y - 0.8f, Is.GreaterThan(minFloor - 0.1f));
    }

    [UnityTest]
    public IEnumerator MainRouteReachesExitWithThreeEmberChamberBudget()
    {
        player.GetComponent<LanternFuel>().ResetForChamber();
        int deaths = 0;
        player.OnPlayerDied += reason => deaths++;
        yield return WalkTo(1.3f);
        yield return SprintJumpTo(3.1f, 0.9f);
        yield return WalkTo(11.4f);
        yield return SprintJumpTo(14.5f, 0f);
        yield return WalkTo(26.2f);
        yield return SprintJumpTo(29.4f, 0f);
        yield return WalkTo(31.4f);
        yield return SprintJumpTo(34.8f, 0f);
        yield return WalkTo(40f);
        yield return SprintJumpTo(43.2f, 0f);
        yield return WalkTo(45.4f);
        yield return SprintJumpTo(48.8f, 0f);
        yield return WalkTo(58f);
        Assert.That(route.IsComplete, Is.True);
        Assert.That(route.CheckpointIndex, Is.EqualTo(2));
        Assert.That(deaths, Is.Zero);
    }

    [UnityTest]
    public IEnumerator OptionalUpperOfficeIsReachableWithCurrentJumpAndRewardsAnEmber()
    {
        // Leave room for the route and office rewards under the three-ember cap.
        player.GetComponent<LanternFuel>().Initialize(2f, 6f);
        yield return WalkTo(1.3f);
        yield return SprintJumpTo(3.1f, 0.9f);
        yield return SprintJumpTo(5.7f, 1.8f);
        yield return SprintJumpTo(8.3f, 2.7f);
        yield return SprintJumpTo(10.9f, 3.5f);
        yield return WalkTo(14f);
        yield return SprintJumpTo(15.6f, 4.1f);
        Assert.That(route.GetComponentsInChildren<EmberPickup>().Length, Is.EqualTo(1), "Main-route and office embers must both be collectible.");
    }

    [UnityTest]
    public IEnumerator SpikeContactRespawnsAtCheckpointAndRestartRestoresAllReserves()
    {
        motor.Body.position = new Vector2(22.5f, 0.85f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(route.CheckpointIndex, Is.EqualTo(1));
        motor.Body.position = new Vector2(27.85f, 0.85f);
        yield return new WaitForSeconds(0.1f);
        Assert.That(player.State, Is.Not.EqualTo(RespawnState.Alive), "Visible spikes must be lethal on contact.");
        var deathScreen=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DeathScreenOverlay>()).Single();
        float revealDeadline=Time.realtimeSinceStartup+3;
        while(!deathScreen.IsArmed && Time.realtimeSinceStartup<revealDeadline)yield return null;
        Assert.That(deathScreen.IsArmed,Is.True);
        Assert.That(player.State,Is.Not.EqualTo(RespawnState.Alive),"The recovery decision must wait for the player.");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
        yield return null;yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        yield return null;yield return null;
        Assert.That(player.State, Is.EqualTo(RespawnState.Alive));
        Assert.That(motor.Body.position.x, Is.EqualTo(22.5f).Within(0.2f));
        foreach (var ember in route.GetComponentsInChildren<EmberPickup>()) Object.Destroy(ember.gameObject);
        yield return null;
        route.RestartRoute();
        yield return null;
        Assert.That(route.CheckpointIndex, Is.Zero);
        Assert.That(route.IsComplete, Is.False);
        Assert.That(route.GetComponentsInChildren<EmberPickup>().Length, Is.EqualTo(3));
        Assert.That(motor.Body.position.x, Is.EqualTo(-7.5f).Within(0.2f));
    }
}
