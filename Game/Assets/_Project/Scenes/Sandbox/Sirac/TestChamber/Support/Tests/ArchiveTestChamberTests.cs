using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Sirac.TestChamber;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ArchiveTestChamberTests
{
    private Scene scene;
    private ChamberDirector director;
    private PlayerMotor2D motor;
    private PlayerRespawnController player;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior background;
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
    private const string Path = "Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity";

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1f;
        background = InputSystem.settings.backgroundBehavior;
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
#if UNITY_EDITOR
        scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Additive));
#else
        Assert.Ignore("This suite loads the authored sandbox scene in the Editor.");
        yield break;
#endif
        yield return null;
        director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChamberDirector>()).Single();
        player = director.Player;
        motor = player.GetComponent<PlayerMotor2D>();
        yield return new WaitForSeconds(0.4f);
        Assert.That(motor.IsGrounded, Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator FullRouteCanBeCompletedUsingMovementAndSingleJumps()
    {
        float elapsed = 0f;
        float jumpHold = 0f;
        int deaths = 0;
        bool dying = false;
        bool spaceHeld = false;
        while (elapsed < 35f && !director.IsComplete)
        {
            if (player.State != RespawnState.Alive)
            {
                if (!dying) deaths++;
                dying = true;
                jumpHold = 0f;
                spaceHeld = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }
            else
            {
                dying = false;
                Vector2 position = motor.Body.position;
                float foot = position.y - 0.8f;
                bool obstacle = Physics2D.RaycastAll(new Vector2(position.x + 0.45f, foot + 0.2f), Vector2.right, 0.65f)
                    .Any(h => !h.collider.isTrigger && h.collider.GetComponentInParent<PlayerMotor2D>() == null);
                bool gapUpcoming = !Physics2D.RaycastAll(new Vector2(position.x + 2.0f, foot + 0.35f), Vector2.down, 0.75f)
                    .Any(h => !h.collider.isTrigger && h.collider.GetComponentInParent<PlayerMotor2D>() == null);
                bool gapEdge = !Physics2D.RaycastAll(new Vector2(position.x + 0.6f, foot + 0.35f), Vector2.down, 0.75f)
                    .Any(h => !h.collider.isTrigger && h.collider.GetComponentInParent<PlayerMotor2D>() == null);

                // Release Space between presses even if a short overhead collision ends a jump early.
                bool isBlackoutCrate = position.x > 35.0f && position.x < 37.5f;
                if (motor.IsGrounded && jumpHold <= 0f && !spaceHeld && (obstacle || (gapEdge && !isBlackoutCrate)))
                {
                    jumpHold = gapEdge ? 0.55f : 0.35f;
                }
                spaceHeld = jumpHold > 0f;
                bool sprint = spaceHeld || gapUpcoming;
                InputSystem.QueueStateEvent(keyboard, spaceHeld
                    ? (sprint ? new KeyboardState(Key.D, Key.Space, Key.LeftShift) : new KeyboardState(Key.D, Key.Space))
                    : (sprint ? new KeyboardState(Key.D, Key.LeftShift) : new KeyboardState(Key.D)));
            }
            yield return null;
            jumpHold -= Time.deltaTime;
            elapsed += Time.deltaTime;
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Assert.That(director.IsComplete, Is.True, $"Route stopped at {motor.Body.position} after {elapsed:0.0}s; deaths={deaths}.");
        Assert.That(director.CheckpointIndex, Is.EqualTo(2));
        Assert.That(deaths, Is.Zero, "The authored main route must be traversable without forced death.");
    }

    [UnityTest]
    public IEnumerator TrenchDeathReturnsToActivatedCheckpoint()
    {
        motor.Body.position = new Vector2(14f, 0.85f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(director.CheckpointIndex, Is.EqualTo(1));
        motor.Body.position = new Vector2(18f, -0.3f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(player.State, Is.Not.EqualTo(RespawnState.Alive));
        yield return new WaitForSeconds(3.1f);
        Assert.That(player.State, Is.EqualTo(RespawnState.Alive));
        Assert.That(motor.Body.position.x, Is.EqualTo(14f).Within(0.2f));
        Assert.That(player.GetComponent<LanternFuel>().TotalCharges, Is.EqualTo(6));
    }

    [UnityTest]
    public IEnumerator BackspaceRestoresPickupsStartAndCheckpointState()
    {
        motor.Body.position = new Vector2(32.5f, 0.85f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(director.CheckpointIndex, Is.EqualTo(2));
        foreach (var pickup in director.GetComponentsInChildren<EmberPickup>()) Object.Destroy(pickup.gameObject);
        yield return null;
        Assert.That(director.GetComponentsInChildren<EmberPickup>().Length, Is.Zero);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Assert.That(director.CheckpointIndex, Is.Zero);
        Assert.That(director.IsComplete, Is.False);
        Assert.That(motor.Body.position.x, Is.EqualTo(-5f).Within(0.2f));
        Assert.That(director.GetComponentsInChildren<EmberPickup>().Length, Is.EqualTo(3));
    }
}
