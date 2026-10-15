using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerMotor2DTests
{
    private GameObject player;
    private GameObject floor;
    private GameObject cameraObject;
    private Rigidbody2D body;
    private PlayerMotor2D motor;
    private Keyboard keyboard;
    private InputSettings.BackgroundBehavior originalBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // Synthetic keyboard events must not depend on which Editor window is focused.
        originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
        floor = new GameObject("Test Floor");
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(30f, 1f);

#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/PlayerPrototype.prefab");
        Assert.That(prefab, Is.Not.Null, "The tests must exercise the actual player prefab.");
        player = Object.Instantiate(prefab, new Vector3(0f, 1.5f, 0f), Quaternion.identity);
        body = player.GetComponent<Rigidbody2D>();
        motor = player.GetComponent<PlayerMotor2D>();
#else
        Assert.Ignore("This prefab integration suite runs in Editor Play Mode.");
        yield break;
#endif
        yield return new WaitForSeconds(0.6f);
        Assert.That(motor.IsGrounded, Is.True, "Player must settle on the floor before input tests.");
    }

    [UnityTest]
    public IEnumerator MovesBothDirectionsAndStopsOnRelease()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.25f);
        float rightX = body.position.x;
        Assert.That(rightX, Is.GreaterThan(0.7f),
            $"D pressed: {keyboard.dKey.isPressed}; device enabled: {keyboard.enabled}; velocity: {body.linearVelocity}");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.08f);
        Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(0.05f));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(0.25f);
        Assert.That(body.position.x, Is.LessThan(rightX - 0.7f));
    }

    [UnityTest]
    public IEnumerator JumpsOnceRejectsAirbornePressAndLands()
    {
        float startY = body.position.y;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.1f);
        Assert.That(body.position.y, Is.GreaterThan(startY + 0.3f));
        Assert.That(motor.IsGrounded, Is.False);
        float risingVelocity = body.linearVelocity.y;
        Assert.That(risingVelocity, Is.GreaterThan(0f));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.04f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.06f);
        Assert.That(body.linearVelocity.y, Is.LessThan(risingVelocity),
            "A second airborne press must not reset upward speed.");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.9f);
        Assert.That(motor.IsGrounded, Is.True);
        Assert.That(body.position.y, Is.EqualTo(startY).Within(0.06f));
    }

    [UnityTest]
    public IEnumerator FallsAlongWallWhileHoldingTowardsIt()
    {
        var wall = new GameObject("Test Wall");
        wall.transform.SetParent(floor.transform);
        wall.transform.position = new Vector3(1.5f, 4f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
        body.position = new Vector2(0f, 3.8f);
        body.linearVelocity = Vector2.zero;

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.7f);
        Assert.That(body.position.y, Is.LessThan(1.2f),
            "Pressing into a side wall must not keep the player suspended.");
        Assert.That(motor.IsGrounded, Is.True);
    }

    [UnityTest]
    public IEnumerator CameraFollowsTheMovingPlayer()
    {
        cameraObject = new GameObject("Test Camera");
        cameraObject.AddComponent<Camera>().enabled = false;
        var follow = cameraObject.AddComponent<FollowCamera2D>();
        follow.SetTarget(player.transform, true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.4f);
        Assert.That(cameraObject.transform.position.x, Is.GreaterThan(0.5f));
        Assert.That(Mathf.Abs(cameraObject.transform.position.x - player.transform.position.x),
            Is.LessThan(1.1f));
        Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.01f));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.Destroy(player);
        Object.Destroy(floor);
        if (cameraObject != null) Object.Destroy(cameraObject);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
        yield return null;
    }
}
