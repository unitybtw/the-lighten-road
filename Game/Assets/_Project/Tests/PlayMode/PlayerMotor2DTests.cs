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
        yield return new WaitForSeconds(0.3f);
        float rightX = body.position.x;
        Assert.That(rightX, Is.GreaterThan(0.65f),
            $"D pressed: {keyboard.dKey.isPressed}; device enabled: {keyboard.enabled}; velocity: {body.linearVelocity}");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.15f);
        Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(0.15f));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(0.3f);
        Assert.That(body.position.x, Is.LessThan(rightX - 0.55f));
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
        yield return new WaitForSeconds(0.9f);
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

    [UnityTest]
    public IEnumerator MovementInputFlipsSpriteDirection()
    {
        var renderer = player.GetComponentInChildren<SpriteRenderer>();
        Assert.That(renderer, Is.Not.Null);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.15f);
        Assert.That(motor.FacingDirection, Is.EqualTo(1));
        Assert.That(renderer.flipX, Is.False, "Facing right should not flip sprite horizontally.");
        if (motor.LanternTransform != null)
        {
            Assert.That(motor.LanternTransform.localPosition.x, Is.GreaterThan(0f),
                "Facing right should place lantern light on the positive X side.");
        }

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(0.2f);
        Assert.That(motor.FacingDirection, Is.EqualTo(-1));
        Assert.That(renderer.flipX, Is.True, "Facing left must flip sprite horizontally.");
        if (motor.LanternTransform != null)
        {
            Assert.That(motor.LanternTransform.localPosition.x, Is.LessThan(0f),
                "Facing left must flip lantern light position horizontally.");
        }

        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    [UnityTest]
    public IEnumerator CoyoteTimeAllowsJumpShortlyAfterLeavingLedge()
    {
        // Place a small test ledge that ends at X = 2.0
        var ledge = new GameObject("Test Ledge");
        ledge.transform.position = new Vector3(0f, 0f, 0f);
        ledge.AddComponent<BoxCollider2D>().size = new Vector2(4f, 1f); // extends from -2.0 to +2.0

        body.position = new Vector2(1.8f, 1.2f);
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.3f);
        Assert.That(motor.IsGrounded, Is.True, "Player must settle on ledge first.");

        // Walk off the right edge of the ledge (past X = 2.0). Capsule width is 0.75m, so player reaches > 2.4m to clear.
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.24f);
        Assert.That(body.position.x, Is.GreaterThan(2.4f), "Player must be past the ledge edge.");
        Assert.That(motor.IsGrounded, Is.False, "Player is now in the air.");
        Assert.That(motor.IsInCoyoteTime, Is.True, "Player should be within the coyote time window.");

        // Press jump while in the air during coyote time
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.08f);
        Assert.That(body.linearVelocity.y, Is.GreaterThan(2f), "Coyote jump must launch the player upward.");

        Object.Destroy(ledge);
    }

    [UnityTest]
    public IEnumerator JumpBufferExecutesUponLanding()
    {
        // Drop player from height
        body.position = new Vector2(0f, 2.8f);
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.15f);
        Assert.That(motor.IsGrounded, Is.False);

        // Press jump while still ~0.05s above the floor
        yield return new WaitForSeconds(0.15f);
        Assert.That(motor.IsGrounded, Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(0.03f);
        Assert.That(motor.HasBufferedJump, Is.True, "Jump input must be buffered.");

        // Allow player to touch ground; buffered jump should fire immediately
        yield return new WaitForSeconds(0.08f);
        Assert.That(body.linearVelocity.y, Is.GreaterThan(1f), "Buffered jump must propel player upward upon landing.");
    }

    [UnityTest]
    public IEnumerator VisualSquashAndStretchReactsToJump()
    {
        Vector3 initialScale = motor.CurrentVisualScale;

        // Trigger jump
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null;
        yield return new WaitForFixedUpdate();

        // During jump initiation, visual scale should stretch vertically (Y > initial Y)
        Assert.That(motor.CurrentVisualScale.y, Is.GreaterThan(initialScale.y * 1.02f),
            "Visual should stretch vertically upon jump takeoff.");

        // Wait for recovery
        yield return new WaitForSeconds(0.4f);
        Assert.That(motor.CurrentVisualScale.y, Is.EqualTo(initialScale.y).Within(0.08f),
            "Visual scale should smoothly recover toward base scale.");
    }

    [UnityTest]
    public IEnumerator WalkingAndSprintingModesWithShift()
    {
        var stamina = player.GetComponent<PlayerStamina>();
        Assert.That(stamina, Is.Not.Null, "Player must have PlayerStamina component.");

        // 1. Walk test (only Key.D)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.2f);
        Assert.That(motor.IsRunning, Is.False, "Player must walk when Shift is not held.");
        Assert.That(body.linearVelocity.x, Is.EqualTo(motor.WalkSpeed).Within(0.2f));

        // 2. Sprint test (Key.D + Key.LeftShift)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
        yield return new WaitForSeconds(0.2f);
        Assert.That(motor.IsRunning, Is.True, "Player must sprint when Shift is held.");
        Assert.That(body.linearVelocity.x, Is.EqualTo(motor.RunSpeed).Within(0.2f));
        Assert.That(stamina.CurrentStamina, Is.LessThan(stamina.MaxStamina), "Stamina must drain while sprinting.");

        // 3. Stop and verify regen
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        float staminaBeforeRegen = stamina.CurrentStamina;
        yield return new WaitForSeconds(0.8f);
        Assert.That(stamina.CurrentStamina, Is.GreaterThan(staminaBeforeRegen), "Stamina must regenerate when resting.");
    }

    [UnityTest]
    public IEnumerator ExhaustionForcesPlayerToWalkUntilRecovered()
    {
        var stamina = player.GetComponent<PlayerStamina>();
        stamina.Initialize(3f, 100f);

        // Sprint until exhausted
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
        yield return new WaitForSeconds(0.35f);

        Assert.That(stamina.IsExhausted, Is.True,
            $"Stamina: {stamina.CurrentStamina}/{stamina.MaxStamina}, Grounded: {motor.IsGrounded}, Running: {motor.IsRunning}, Vel: {body.linearVelocity}, Pos: {body.position}");
        Assert.That(motor.IsRunning, Is.False, "Exhausted player cannot sprint even if Shift is held.");
        Assert.That(body.linearVelocity.x, Is.EqualTo(motor.WalkSpeed).Within(0.25f));

        // Release keys and wait for recovery threshold (25%)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(1.8f);
        Assert.That(stamina.IsExhausted, Is.False, "Exhaustion must clear once recovered past threshold.");
        Assert.That(stamina.CanSprint, Is.True);
    }

    [UnityTest]
    public IEnumerator SprintJumpCoversSignificantlyMoreHorizontalDistance()
    {
        // Measure walking jump
        body.position = new Vector2(0f, 0.8f);
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(0.1f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.05f);
        float walkStartX = body.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
        yield return new WaitForSeconds(0.65f);
        float walkJumpDistance = body.position.x - walkStartX;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(0.1f);

        // Reset position and stamina
        body.position = new Vector2(0f, 0.8f);
        body.linearVelocity = Vector2.zero;
        var stamina = player.GetComponent<PlayerStamina>();
        if (stamina != null) stamina.ResetStamina();
        yield return new WaitForSeconds(0.1f);

        // Measure sprint jump (running start before takeoff)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
        yield return new WaitForSeconds(0.05f);
        float sprintStartX = body.position.x;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift, Key.Space));
        yield return new WaitForSeconds(0.65f);
        float sprintJumpDistance = body.position.x - sprintStartX;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());

        Assert.That(sprintJumpDistance, Is.GreaterThan(walkJumpDistance + 0.6f),
            $"Sprint jump ({sprintJumpDistance:F2}m) must cover substantially more ground than walk jump ({walkJumpDistance:F2}m).");
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
