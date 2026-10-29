using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerLedgeClimbTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private GameObject player, ledge;
    private PlayerLedgeClimb climb;
    private PlayerMotor2D motor;
    private PlayerSpriteAnimator poses;
    private Keyboard keyboard, previousKeyboard;
    private Gamepad pad;
    private InputSettings.BackgroundBehavior background;
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;

    private GameObject Box(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name); objects.Add(go);
        go.transform.position = position; go.AddComponent<BoxCollider2D>().size = size;
        return go;
    }
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        background = InputSystem.settings.backgroundBehavior;
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        Box("Climb test floor", new Vector2(0, -.5f), new Vector2(20, 1));
        ledge = Box("Solid archive ledge", new Vector2(1.65f, .75f), new Vector2(2, 1.5f));
#if UNITY_EDITOR
        player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab"), new Vector3(0, .85f), Quaternion.identity);
#else
        Assert.Ignore("Actual shared prefab is validated in Editor Play Mode."); yield break;
#endif
        objects.Add(player);
        climb = player.GetComponent<PlayerLedgeClimb>(); motor = player.GetComponent<PlayerMotor2D>(); poses = player.GetComponent<PlayerSpriteAnimator>();
        Assert.That(climb, Is.Not.Null);
        player.GetComponent<PlayerRespawnController>().SpawnPosition = new Vector3(0, .85f);
        yield return new WaitForSeconds(.2f);
        Assert.That(motor.IsGrounded, Is.True);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        foreach (var go in objects)
        {
            if (go == null) continue;
            LegacyInteractionPollingGate.Release(go); GameControls.BlockGameplay(go, false);
            Object.DestroyImmediate(go);
        }
        objects.Clear();
        InputSystem.RemoveDevice(keyboard);
        if (pad != null) InputSystem.RemoveDevice(pad);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        yield return null; yield return null; yield return null;
    }
    private IEnumerator Finish()
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (climb.IsClimbing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(climb.IsClimbing, Is.False, $"Progress {climb.Progress}; pose {poses.CurrentAnimation}/{poses.CurrentFrame}; controls {GameControls.GameplayAllowedExcept(climb)}; position {motor.Body.position}");
        yield return new WaitForSeconds(.15f);
    }

    [UnityTest] public IEnumerator UpInputStowsLanternThenClimbsOntoSolidLedgeWithoutSpendingFuel()
    {
        int charges = player.GetComponent<LanternFuel>().TotalCharges;
        var size = player.GetComponent<CapsuleCollider2D>().size;
        var visualPosition = poses.Renderer.transform.localPosition;
        var start = motor.Body.position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return new WaitForSeconds(.2f);
        Assert.That(climb.IsClimbing, Is.True);
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Sheathing));
        Assert.That(Vector2.Distance(motor.Body.position, start), Is.LessThan(.03f));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.7f);
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Climb));
        var destination = climb.LandingPosition;
        yield return Finish();
        Assert.That(motor.Body.position.x, Is.EqualTo(destination.x).Within(.06f));
        Assert.That(motor.IsGrounded, Is.True);
        Assert.That(motor.Body.position.y, Is.GreaterThan(2.25f));
        Assert.That(motor.TraversalLocked, Is.False);
        Assert.That(player.GetComponent<LanternFuel>().TotalCharges, Is.EqualTo(charges));
        Assert.That(player.GetComponent<CapsuleCollider2D>().size, Is.EqualTo(size));
        Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(poses.Renderer.transform.localPosition, Is.EqualTo(visualPosition));
        Assert.That(GameControls.GameplayAllowed, Is.True);
    }
    [UnityTest] public IEnumerator HorizontalMovementDoesNotStartAnUnrequestedClimb()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.4f);
        Assert.That(climb.IsClimbing, Is.False);
        Assert.That(motor.Body.position.y, Is.LessThan(1));
    }
    [UnityTest] public IEnumerator GamepadDpadStartsClimbAndJumpCancelsRestoringGravity()
    {
        pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
        var state = new GamepadState().WithButton(GamepadButton.DpadUp);
        InputSystem.QueueStateEvent(pad, state); yield return new WaitForSeconds(.12f);
        Assert.That(climb.IsClimbing, Is.True);
        InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return new WaitForSeconds(.1f);
        Assert.That(climb.IsClimbing, Is.False);
        Assert.That(motor.TraversalLocked, Is.False);
        Assert.That(motor.Body.gravityScale, Is.GreaterThan(0));
        Assert.That(poses.AutomaticTicks, Is.True);
    }
    [UnityTest] public IEnumerator TallNarrowMovingAndObstructedLedgesAreRejected()
    {
        var collider = ledge.GetComponent<BoxCollider2D>();
        ledge.transform.position = new Vector2(1.65f, 2); collider.size = new Vector2(2, 4);
        Assert.That(climb.TryBeginClimb(), Is.False, "Tall wall");
        ledge.transform.position = new Vector2(.8f, .75f); collider.size = new Vector2(.3f, 1.5f);
        Assert.That(climb.TryBeginClimb(), Is.False, "No full landing footprint");
        ledge.transform.position = new Vector2(1.65f, .75f); collider.size = new Vector2(2, 1.5f);
        var moving = ledge.AddComponent<Rigidbody2D>(); moving.bodyType = RigidbodyType2D.Kinematic;
        Assert.That(climb.TryBeginClimb(), Is.False, "Player must not take over moving machinery");
        Object.DestroyImmediate(moving);
        Box("Low ceiling", new Vector2(0, 2.0f), new Vector2(1, .25f));
        Assert.That(climb.TryBeginClimb(), Is.False, "Lift path hits ceiling");
        Assert.That(motor.TraversalLocked, Is.False);
        Assert.That(poses.AutomaticTicks, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator PauseAndOtherInputOwnersFreezeBothMotionAndPose()
    {
        // Exercise a physics relocation before the interpolated Transform catches up.
        ledge.transform.position += Vector3.right * 20;
        motor.Body.position += Vector2.right * 20;
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(1.4f);
        Assert.That(poses.Renderer.transform.localPosition.x, Is.InRange(.2f, 1.25f));
        Assert.That(Mathf.Abs(poses.Renderer.transform.position.x - motor.Body.position.x), Is.LessThan(1.3f));
        var owner = Box("Modal owner", new Vector2(-10, -10), Vector2.one);
        GameControls.BlockGameplay(owner, true);
        var position = motor.Body.position; int frame = poses.CurrentFrame;
        yield return new WaitForSeconds(.25f);
        Assert.That(motor.Body.position, Is.EqualTo(position)); Assert.That(poses.CurrentFrame, Is.EqualTo(frame));
        GameControls.BlockGameplay(owner, false); yield return null; yield return null; yield return null;
        Time.timeScale = 0; position = motor.Body.position; frame = poses.CurrentFrame;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(motor.Body.position, Is.EqualTo(position)); Assert.That(poses.CurrentFrame, Is.EqualTo(frame));
        Time.timeScale = 1; yield return Finish();
        Assert.That(motor.IsGrounded, Is.True);
    }
    [UnityTest] public IEnumerator DeathAndRespawnDoNotLeaveTraversalOrInputLocked()
    {
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(1.1f);
        var recovery = player.GetComponent<PlayerRespawnController>(); recovery.AutoRespawnEnabled = false;
        recovery.Kill(DeathReason.Hazard);
        Assert.That(climb.IsClimbing, Is.False); Assert.That(motor.TraversalLocked, Is.False);
        Assert.That(motor.Body.simulated, Is.False);
        recovery.Respawn(); yield return new WaitForSeconds(.2f);
        Assert.That(motor.enabled, Is.True); Assert.That(poses.AutomaticTicks, Is.True);
        Assert.That(motor.IsGrounded, Is.True); Assert.That(GameControls.GameplayAllowed, Is.True);
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
        Assert.That(climb.TryBeginClimb(), Is.True);
    }
    [UnityTest] public IEnumerator RemovedPlatformAndNewObstacleAbortSafely()
    {
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(.8f);
        ledge.SetActive(false); yield return new WaitForFixedUpdate(); yield return null;
        Assert.That(climb.IsClimbing, Is.False); Assert.That(motor.Body.gravityScale, Is.GreaterThan(0));
        ledge.SetActive(true); yield return new WaitForSeconds(.15f);
        Assert.That(climb.TryBeginClimb(), Is.True);
        Box("Fallen obstruction", new Vector2(0, 2.1f), new Vector2(.9f, .2f));
        yield return new WaitForSeconds(2);
        Assert.That(climb.IsClimbing, Is.False);
        Assert.That(motor.Body.position.y, Is.LessThan(1.5f), "Must not teleport through the new obstruction");
    }
    [UnityTest] public IEnumerator FlareRemainsAnExplicitSpendAndPreventsStowingAnActiveScout()
    {
        var lantern = player.GetComponent<LanternController>();
        Assert.That(lantern.SetFlared(true), Is.True);
        int fuel = lantern.Fuel.TotalCharges;
        Assert.That(climb.TryBeginClimb(), Is.False);
        Assert.That(lantern.Fuel.TotalCharges, Is.EqualTo(fuel));
        Assert.That(lantern.HasActiveScout, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator TraversalAndModalLeasesRestoreOnlyAfterBothOwnersRelease()
    {
        var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TheLightenRoad.Interaction.EmberVein")).First(t => t != null);
        var go = Box("Real legacy circuit", new Vector2(-10, -10), Vector2.one);
        var vein = (Behaviour)go.AddComponent(type);
        var owner = Box("Recovery input owner", new Vector2(-12, -10), Vector2.one);
        Assert.That(climb.TryBeginClimb(), Is.True); Assert.That(vein.enabled, Is.False);
        LegacyInteractionPollingGate.Acquire(owner);
        climb.CancelClimb(); Assert.That(vein.enabled, Is.False, "Modal still owns the polling lock");
        LegacyInteractionPollingGate.Release(owner); Assert.That(vein.enabled, Is.True);
        vein.enabled = false;
        LegacyInteractionPollingGate.Acquire(owner); LegacyInteractionPollingGate.Release(owner);
        Assert.That(vein.enabled, Is.False, "Originally disabled circuits must stay disabled");
        yield return null;
    }
    [UnityTest] public IEnumerator DisablingTraversalRestoresPlayerAndAllowsAnotherAttempt()
    {
        Assert.That(climb.TryBeginClimb(), Is.True);
        climb.enabled = false;
        Assert.That(motor.TraversalLocked, Is.False); Assert.That(motor.Body.gravityScale, Is.GreaterThan(0));
        Assert.That(poses.AutomaticTicks, Is.True);
        yield return null; yield return null; yield return null;
        climb.enabled = true;
        Assert.That(climb.TryBeginClimb(), Is.True);
    }
    [UnityTest] public IEnumerator LeftFacingAirborneGrabUsesTheSameSafeTraversal()
    {
        ledge.transform.position = new Vector2(-1.65f, .75f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); yield return new WaitForSeconds(.06f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        motor.Body.position = new Vector2(0, 1.1f); motor.Body.linearVelocity = Vector2.zero;
        Assert.That(motor.FacingDirection, Is.EqualTo(-1));
        Assert.That(climb.TryBeginClimb(), Is.True);
        yield return new WaitForSeconds(.8f);
        Assert.That(poses.Renderer.flipX, Is.True);
        yield return Finish();
        Assert.That(motor.Body.position.x, Is.LessThan(-1)); Assert.That(motor.IsGrounded, Is.True);
    }
    [UnityTest] public IEnumerator HotplugWithHeldStickRequiresReleaseBeforeClimbing()
    {
        pad = InputSystem.AddDevice<Gamepad>(); pad.MakeCurrent();
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
        yield return new WaitForSeconds(.1f);
        Assert.That(climb.IsClimbing, Is.False);
        InputSystem.QueueStateEvent(pad, new GamepadState()); yield return new WaitForSeconds(.05f);
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up }); yield return new WaitForSeconds(.1f);
        Assert.That(climb.IsClimbing, Is.True);
    }
    [UnityTest] public IEnumerator StartingInsideGeometryIsRejectedBeforeOwningControls()
    {
        motor.Body.position = new Vector2(0, .25f); motor.Body.linearVelocity = Vector2.zero;
        Assert.That(climb.TryBeginClimb(), Is.False);
        Assert.That(motor.TraversalLocked, Is.False); Assert.That(poses.AutomaticTicks, Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator HandStaysOnTheActualLipAndStandingFeetDoNotFloat()
    {
        var probe = player.AddComponent<ClimbPoseProbe>();
        probe.Climb = climb; probe.Poses = poses; probe.Top = ledge.GetComponent<BoxCollider2D>().bounds.max.y;
        Assert.That(climb.TryBeginClimb(), Is.True);
        Assert.That(climb.EntryFrame, Is.GreaterThan(0), "Low ledges must skip the overhead hanging pose.");
        yield return Finish();
        Assert.That(probe.GripFrames.Count, Is.GreaterThanOrEqualTo(7));
        Assert.That(probe.MaxGripError, Is.LessThan(.045f), "Hand must remain on the corner in both axes.");
        Assert.That(probe.StandingSamples, Is.GreaterThan(5));
        Assert.That(probe.MaxSoleGap, Is.LessThan(.065f), "Standing frames must stay on the top surface.");
        Assert.That(probe.TrailingFootPenetration, Is.LessThan(.015f), "The hanging foot must stay outside the wall before it clears the lip.");
        Assert.That(probe.CompletionSoleJump, Is.LessThan(.06f), "Returning to Idle must not pop the feet.");
    }
    [UnityTest] public IEnumerator AirborneGrabStartsWithClimbInsteadOfHangingThroughTheStowClip()
    {
        motor.Body.position = new Vector2(0, 1.12f); motor.Body.linearVelocity = Vector2.down;
        yield return new WaitForFixedUpdate(); yield return null;
        Assert.That(motor.IsGrounded, Is.False);
        Assert.That(climb.TryBeginClimb(), Is.True);
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Climb));
        Assert.That(climb.EntryFrame, Is.GreaterThan(0));
        yield return Finish();
        Assert.That(motor.IsGrounded, Is.True);
    }
    [UnityTest] public IEnumerator TallLedgeKeepsTheFullHangingStartAndPinsBothAxes()
    {
        ledge.transform.position = new Vector2(1.65f, 1.05f);
        ledge.GetComponent<BoxCollider2D>().size = new Vector2(2, 2.1f);
        var probe = player.AddComponent<ClimbPoseProbe>();
        probe.Climb = climb; probe.Poses = poses; probe.Top = 2.1f;
        Assert.That(climb.TryBeginClimb(), Is.True);
        Assert.That(climb.EntryFrame, Is.Zero);
        yield return Finish();
        Assert.That(probe.GripFrames.Contains(0), Is.True);
        Assert.That(probe.GripFrames.Count, Is.GreaterThanOrEqualTo(12));
        Assert.That(probe.MaxGripError, Is.LessThan(.045f));
        Assert.That(probe.MaxSoleGap, Is.LessThan(.065f));
        Assert.That(probe.CompletionSoleJump, Is.LessThan(.06f));
    }
    [UnityTest] public IEnumerator CancellationPreservesTheVisibleSoleAndRestoresOffsets()
    {
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(1.35f);
        var before = ClimbPoseProbe.Sole(poses);
        climb.CancelClimb();
        var after = ClimbPoseProbe.Sole(poses);
        Assert.That(Vector3.Distance(before, after), Is.LessThan(.045f));
        Assert.That(motor.TraversalLocked, Is.False);
        yield return new WaitForSeconds(.35f);
        Assert.That(poses.Renderer.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(GameControls.GameplayAllowed, Is.True);
        Assert.That(motor.Body.gravityScale, Is.GreaterThan(0));
    }
    [UnityTest] public IEnumerator DeathFreezesTheActualClimbPoseUntilRespawn()
    {
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(1.3f);
        var position = poses.Renderer.transform.position; var sprite = poses.Renderer.sprite;
        var recovery = player.GetComponent<PlayerRespawnController>(); recovery.AutoRespawnEnabled = false;
        recovery.Kill(DeathReason.Hazard);
        Assert.That(Vector3.Distance(poses.Renderer.transform.position, position), Is.LessThan(.12f));
        Assert.That(poses.Renderer.sprite, Is.EqualTo(sprite));
        yield return new WaitForSeconds(.2f);
        Assert.That(poses.Renderer.sprite, Is.EqualTo(sprite));
        recovery.Respawn(); yield return new WaitForSeconds(.2f);
        Assert.That(poses.Renderer.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
    }
    [UnityTest] public IEnumerator UpPressIsBufferedBrieflyBeforeTheLedgeBecomesReachable()
    {
        motor.Body.position = new Vector2(-.4f, .81f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
        yield return new WaitForSeconds(.16f);
        Assert.That(climb.IsClimbing, Is.True);
    }
    [UnityTest] public IEnumerator RotatingSupportAbortsAndReleasesTheOwnedInputLock()
    {
        Assert.That(climb.TryBeginClimb(), Is.True); yield return new WaitForSeconds(1);
        ledge.transform.rotation = Quaternion.Euler(0, 0, 90);
        yield return new WaitForSeconds(.2f);
        Assert.That(climb.IsClimbing, Is.False);
        Assert.That(motor.TraversalLocked, Is.False);
        yield return new WaitForSeconds(.2f);
        Assert.That(GameControls.GameplayAllowed, Is.True);
    }
    [UnityTest] public IEnumerator SimultaneousLeftAndUpDoNotGrabTheLedgeBehindThePlayer()
    {
        Box("Left archive ledge", new Vector2(-1.65f, .75f), new Vector2(2, 1.5f));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.W));
        yield return new WaitForSeconds(.18f);
        Assert.That(climb.IsClimbing, Is.True);
        Assert.That(climb.LandingPosition.x, Is.LessThan(0));
        Assert.That(motor.FacingDirection, Is.EqualTo(-1));
    }
    [UnityTest] public IEnumerator JumpAndUpLaunchBeforeTheAirborneGrabInsteadOfStowingOnTheFloor()
    {
        int jumps = 0; motor.OnJumped += () => jumps++;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.W));
        yield return new WaitForSeconds(.14f);
        Assert.That(jumps, Is.EqualTo(1));
        Assert.That(climb.IsClimbing, Is.True);
        Assert.That(poses.CurrentAnimation, Is.EqualTo(LightAnimation.Climb));
    }
}

// Observe after traversal's LateUpdate and the sprite animator, as the rendered frame sees it.
[DefaultExecutionOrder(200)]
public sealed class ClimbPoseProbe : MonoBehaviour
{
    public PlayerLedgeClimb Climb;
    public PlayerSpriteAnimator Poses;
    public float Top, MaxGripError, MaxSoleGap, CompletionSoleJump, TrailingFootPenetration;
    public int StandingSamples;
    public readonly HashSet<int> GripFrames = new HashSet<int>();
    private bool previousClimb;
    private int previousFrame;
    private Vector3 previousSole;
    public static Vector3 Sole(PlayerSpriteAnimator poses)
    {
        var data = poses.Catalog.Find(poses.CurrentAnimation);
        var p = data.SoleOffsets[poses.CurrentFrame];
        if (poses.Renderer.flipX) p.x = -p.x;
        return poses.Renderer.transform.TransformPoint(p);
    }
    private void LateUpdate()
    {
        if (Climb == null || Poses == null) return;
        bool active = Climb.IsClimbing && Poses.CurrentAnimation == LightAnimation.Climb;
        int frame = Poses.CurrentFrame;
        var sole = Sole(Poses);
        if (active)
        {
            var data = Poses.Catalog.Find(LightAnimation.Climb);
            if (!Climb.IsReaching && frame < 15)
            {
                var p = data.GripOffsets[frame]; if (Poses.Renderer.flipX) p.x = -p.x;
                MaxGripError = Mathf.Max(MaxGripError, Vector2.Distance(Poses.Renderer.transform.TransformPoint(p), Climb.GripPoint));
                GripFrames.Add(frame);
            }
            if (frame >= 20) { StandingSamples++; MaxSoleGap = Mathf.Max(MaxSoleGap, Mathf.Abs(sole.y - Top)); }
            else if (frame >= 15 && sole.y < Top - .015f)
            {
                int direction = Poses.Renderer.flipX ? -1 : 1;
                float edge = Climb.GripPoint.x - direction * .02f;
                TrailingFootPenetration = Mathf.Max(TrailingFootPenetration, (sole.x - edge) * direction);
            }
        }
        else if (previousClimb && previousFrame >= 25 && Poses.CurrentAnimation == LightAnimation.Idle)
            CompletionSoleJump = Mathf.Max(CompletionSoleJump, Vector3.Distance(previousSole, sole));
        previousClimb = active; previousFrame = frame; previousSole = sole;
    }
}
