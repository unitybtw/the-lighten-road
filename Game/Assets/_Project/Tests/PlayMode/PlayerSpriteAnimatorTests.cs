using System.Collections;
using System.Linq;
using NUnit.Framework;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PlayerSpriteAnimatorTests
{
    private GameObject player, floor;
    private PlayerMotor2D motor;
    private PlayerSpriteAnimator animation;
    private Keyboard keyboard, previousKeyboard;
    private InputSettings.BackgroundBehavior background;
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;

    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        background = InputSystem.settings.backgroundBehavior;
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        floor = new GameObject("Artist animation floor"); floor.transform.position = new Vector3(0, -.5f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(40, 1);
#if UNITY_EDITOR
        player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/PlayerPrototype.prefab"), new Vector3(0, .85f), Quaternion.identity);
#else
        Assert.Ignore("Prefab integration is tested in Editor Play Mode."); yield break;
#endif
        motor = player.GetComponent<PlayerMotor2D>(); animation = player.GetComponent<PlayerSpriteAnimator>();
        player.GetComponent<PlayerRespawnController>().SpawnPosition = new Vector3(0, .85f);
        Assert.That(animation, Is.Not.Null);
        yield return new WaitForSeconds(.15f);
        Assert.That(motor.IsGrounded, Is.True);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (player != null) Object.DestroyImmediate(player);
        if (floor != null) Object.DestroyImmediate(floor);
        InputSystem.RemoveDevice(keyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        InputSystem.settings.backgroundBehavior = background;
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        yield return null; yield return null;
    }
    [Test] public void CatalogContainsAllDeliveredFramesAndClimbHasItsOwnRegistration()
    {
        var data = animation.Catalog;
        Assert.That(data.Sequences.Length, Is.EqualTo(9));
        Assert.That(data.Sequences.Sum(s => s.Frames.Length), Is.EqualTo(131));
        foreach (var s in data.Sequences)
        {
            Assert.That(s.Durations.All(d => Mathf.Abs(d - .1f) < .0001f), Is.True);
            Assert.That(s.Frames.All(f => f != null && Mathf.Abs(f.pixelsPerUnit - 36) < .001f), Is.True);
            Assert.That(s.PreviewClip.length, Is.EqualTo(s.Duration).Within(.001f));
            Assert.That(s.LanternOffsets.Length, Is.EqualTo(s.Frames.Length));
            Assert.That(s.SoleOffsets.Length, Is.EqualTo(s.Frames.Length));
            Assert.That(s.Frames.All(f => Mathf.Approximately(f.rect.width, 70)), Is.True);
            Assert.That(s.Frames.All(f => Mathf.Approximately(f.rect.height, s.Kind == LightAnimation.Climb ? 80 : 70)), Is.True);
        }
        Assert.That(data.Find(LightAnimation.Climb).Frames[0].pivot.y, Is.EqualTo(30));
        Assert.That(data.Find(LightAnimation.Climb).ReachOffsetsX.Length, Is.EqualTo(27));
        Assert.That(data.Find(LightAnimation.Climb).GripOffsets.Length, Is.EqualTo(27));
        Assert.That(data.Find(LightAnimation.Climb).PlantOffsets.Length, Is.EqualTo(27));
        Assert.That(data.Find(LightAnimation.Climb).ReachOffsetsX.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True);
        Assert.That(data.Find(LightAnimation.Idle).Frames[0].pivot.y, Is.EqualTo(35));
        Assert.That(data.Find(LightAnimation.Walk).WaistOffsetsX.Length, Is.EqualTo(10));
        Assert.That(data.Find(LightAnimation.Flare).WaistOffsetsX.Length, Is.EqualTo(12));
    }
    [UnityTest] public IEnumerator WalkingAndFacingUseTheDeliveredFramesWithoutChangingPhysics()
    {
        var collider = player.GetComponent<CapsuleCollider2D>(); var size = collider.size;
        Assert.That(motor.EnableSquashAndStretch, Is.False); Assert.That(motor.EnableKineticTilt, Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.25f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        Assert.That(animation.Renderer.flipX, Is.False);
        Assert.That(motor.LanternTransform.localPosition.x, Is.GreaterThan(0));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); yield return new WaitForSeconds(.3f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        Assert.That(animation.Renderer.flipX, Is.True);
        Assert.That(motor.LanternTransform.localPosition.x, Is.LessThan(0));
        Assert.That(collider.size, Is.EqualTo(size));
        Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
    }
    [UnityTest] public IEnumerator WalkCadenceSlowsWithActualMotionWithoutAMinimumSteppingRate()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(.2f);
        animation.AutomaticTicks = false;
        animation.enabled = false; animation.enabled = true;
        motor.Body.linearVelocity = new Vector2(motor.WalkSpeed, 0);
        animation.Tick(.2f);
        int fullSpeedFrame = animation.CurrentFrame;
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        Assert.That(fullSpeedFrame, Is.EqualTo(2), "Normal walking must retain the delivered 100ms frames, not accelerate the art's one-second cycle.");
        animation.enabled = false; animation.enabled = true;
        motor.Body.linearVelocity = new Vector2(motor.WalkSpeed * .1f, 0);
        animation.Tick(.2f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        Assert.That(animation.CurrentFrame, Is.Zero, "Slow analog movement must not be forced to play at half the full-speed cadence.");
        animation.AutomaticTicks = true;
    }
    [UnityTest] public IEnumerator WalkingAfterAStopStartsAtTheFirstStepInsteadOfAStaleRaisedFoot()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(.3f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new WaitForSeconds(.15f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
        animation.AutomaticTicks = false;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(.08f);
        animation.Tick(.001f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        Assert.That(animation.CurrentFrame, Is.Zero);
        animation.AutomaticTicks = true;
    }
    [UnityTest] public IEnumerator WalkingFlareKeepsTheArmsAnimatingAndTheLegsSteppingInBothDirections()
    {
        var shape = player.GetComponent<CapsuleCollider2D>().size;
        var lantern = player.GetComponent<LanternController>();
        var fuel = player.GetComponent<LanternFuel>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(.2f);
        float initialFuel = fuel.CurrentFuel;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.F));
        yield return new WaitForSeconds(.1f);
        Assert.That(lantern.IsFlared, Is.True);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Flare));
        Assert.That(animation.HasWalkingActionLayer, Is.True);
        Assert.That(animation.Renderer.sprite.texture, Is.SameAs(animation.Catalog.Find(LightAnimation.Flare).Frames[0].texture));
        Assert.That(animation.WalkingLegsRenderer.sprite.texture, Is.SameAs(animation.Catalog.Find(LightAnimation.Walk).Frames[0].texture));
        Assert.That(animation.WalkingLegsRenderer.sortingOrder, Is.EqualTo(animation.Renderer.sortingOrder));
        int upper = animation.CurrentFrame, lower = animation.WalkingLegFrame;
        var legSprite = animation.WalkingLegsRenderer.sprite;
        yield return new WaitForSeconds(.22f);
        Assert.That(animation.CurrentFrame, Is.GreaterThan(upper));
        Assert.That(animation.WalkingLegFrame, Is.Not.EqualTo(lower));
        Assert.That(animation.WalkingLegsRenderer.sprite, Is.Not.SameAs(legSprite));
        Assert.That(motor.HorizontalSpeed, Is.EqualTo(motor.WalkSpeed).Within(.15f));
        Assert.That(fuel.CurrentFuel, Is.EqualTo(initialFuel - LanternFuel.ChargesPerEmber));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(.15f);
        Assert.That(animation.HasWalkingActionLayer, Is.True);
        Assert.That(animation.Renderer.flipX && animation.WalkingLegsRenderer.flipX, Is.True);
        Assert.That(player.GetComponent<CapsuleCollider2D>().size, Is.EqualTo(shape));
        Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
    }
    [UnityTest] public IEnumerator StoppingDuringFlareRestoresTheOriginalFullPoseAndJumpClearsWalkingLegs()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.2f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.F)); yield return new WaitForSeconds(.1f);
        Assert.That(animation.HasWalkingActionLayer, Is.True);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSeconds(.12f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Flare));
        Assert.That(animation.HasWalkingActionLayer, Is.False);
        Assert.That(animation.Renderer.sprite, Is.SameAs(animation.Catalog.Find(LightAnimation.Flare).Frames[animation.CurrentFrame]));
        Assert.That(animation.WalkingLegsRenderer.gameObject.activeSelf, Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.1f);
        Assert.That(animation.HasWalkingActionLayer, Is.True);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space)); yield return new WaitForSeconds(.06f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        Assert.That(animation.HasWalkingActionLayer, Is.False);
        Assert.That(animation.WalkingLegsRenderer.gameObject.activeSelf, Is.False);
        Assert.That(motor.Body.linearVelocity.y, Is.GreaterThan(1));
    }
    [UnityTest] public IEnumerator PauseFreezesBothFlareAndGaitAndDisableRemovesTheExtraRenderer()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); yield return new WaitForSeconds(.2f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.F)); yield return new WaitForSeconds(.1f);
        Assert.That(animation.HasWalkingActionLayer, Is.True);
        Time.timeScale = 0;
        int upper = animation.CurrentFrame, lower = animation.WalkingLegFrame;
        Sprite sprite = animation.WalkingLegsRenderer.sprite;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(animation.CurrentFrame, Is.EqualTo(upper)); Assert.That(animation.WalkingLegFrame, Is.EqualTo(lower));
        Assert.That(animation.WalkingLegsRenderer.sprite, Is.SameAs(sprite));
        Time.timeScale = 1; animation.enabled = false;
        Assert.That(animation.WalkingLegsRenderer.gameObject.activeSelf, Is.False);
        animation.enabled = true; yield return null;
        Assert.That(animation.HasWalkingActionLayer, Is.False);
    }
    [UnityTest] public IEnumerator IgnitingWhileAirborneDoesNotReplaceTheJumpWithStandingLegs()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return new WaitForSeconds(.1f);
        Assert.That(motor.IsGrounded, Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.F)); yield return new WaitForSeconds(.08f);
        Assert.That(player.GetComponent<LanternController>().IsFlared, Is.True);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        Assert.That(animation.HasWalkingActionLayer, Is.False);
    }
    [UnityTest] public IEnumerator LanternLightSettlesBetweenWalkMarkersButSnapsOnFacingChanges()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return new WaitForSeconds(.2f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
        animation.AutomaticTicks = false;
        bool sawSmoothedFrame = false;
        for (int step = 0; step < 120; step++)
        {
            int frame = animation.CurrentFrame;
            Vector3 before = motor.LanternTransform.localPosition;
            animation.Tick(.01f);
            if (frame == animation.CurrentFrame) continue;
            Vector3 desired = ExpectedLanternAnchor();
            if (Vector3.Distance(before, desired) < .01f) continue;
            Vector3 after = motor.LanternTransform.localPosition;
            Assert.That(Vector3.Distance(after, desired), Is.LessThan(Vector3.Distance(before, desired)));
            Assert.That(Vector3.Distance(after, desired), Is.GreaterThan(.0001f));
            sawSmoothedFrame = true;
            break;
        }
        Assert.That(sawSmoothedFrame, Is.True, "The real walk markers should settle instead of stepping the room light.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
        yield return new WaitForSeconds(.15f);
        animation.Tick(.01f);
        Assert.That(motor.FacingDirection, Is.EqualTo(-1));
        Assert.That(Vector3.Distance(motor.LanternTransform.localPosition, ExpectedLanternAnchor()), Is.LessThan(.0001f),
            "Turning must place the source on the new hand immediately, without sweeping through the body.");
    }

    private Vector3 ExpectedLanternAnchor()
    {
        var sequence = animation.Catalog.Find(animation.CurrentAnimation);
        var point = sequence.LanternOffsets[animation.CurrentFrame];
        Vector3 local = new Vector3(point.x * (animation.Renderer.flipX ? -1 : 1), point.y, 0f);
        var anchor = player.transform.InverseTransformPoint(animation.Renderer.transform.TransformPoint(local));
        anchor.z = motor.LanternTransform.localPosition.z;
        return anchor;
    }
    [UnityTest] public IEnumerator RealJumpAndLandingDriveTheirOwnAnimations()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return new WaitForSeconds(.12f);
        Assert.That(motor.IsGrounded, Is.False);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        float deadline = Time.realtimeSinceStartup + 3;
        while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
        Assert.That(motor.IsGrounded, Is.True);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Landing));
        yield return new WaitForSeconds(.65f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
    }
    [UnityTest] public IEnumerator SuccessfulFlarePlaysOnceAndDeathRespawnClearsThePose()
    {
        var lantern = player.GetComponent<LanternController>();
        Assert.That(lantern.SetFlared(true), Is.True);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Flare));
        yield return new WaitForSeconds(.15f);
        var recovery = player.GetComponent<PlayerRespawnController>(); recovery.AutoRespawnEnabled = false;
        recovery.Kill(DeathReason.Hazard);
        int frame = animation.CurrentFrame;
        yield return new WaitForSeconds(.2f);
        Assert.That(animation.CurrentFrame, Is.EqualTo(frame));
        recovery.Respawn(); yield return null; yield return null;
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
        var fuel = player.GetComponent<LanternFuel>(); fuel.Initialize(0, LanternFuel.MaximumFuel);
        Assert.That(lantern.SetFlared(true), Is.False);
        Assert.That(animation.CurrentAnimation, Is.Not.EqualTo(LightAnimation.Flare));
    }
    [UnityTest] public IEnumerator TransferAnimationRequiresFuelChangeAndInteractIntent()
    {
        var fuel = player.GetComponent<LanternFuel>();
        fuel.Initialize(4, LanternFuel.MaximumFuel);
        Assert.That(animation.CurrentAnimation, Is.Not.EqualTo(LightAnimation.EmberSwap));
        var previousMode = InputSystem.settings.updateMode;
        try
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
            Assert.That(fuel.TrySpendFuel(100), Is.False);
            Assert.That(animation.CurrentAnimation, Is.Not.EqualTo(LightAnimation.EmberSwap));
            Assert.That(fuel.TrySpendEmber(), Is.True);
            Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.EmberSwap));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        }
        finally { InputSystem.settings.updateMode = previousMode; }
        yield return null;
    }
    [UnityTest] public IEnumerator PauseFreezesPlaybackAndContextClipsHaveNoRootMotion()
    {
        Assert.That(animation.PlayContextAnimation(LightAnimation.Sheathing, true), Is.True);
        yield return new WaitForSeconds(.15f);
        Time.timeScale = 0; int frame = animation.CurrentFrame;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(animation.CurrentFrame, Is.EqualTo(frame));
        Time.timeScale = 1;
        animation.AutomaticTicks = false;
        var position = player.transform.position;
        var shape = player.GetComponent<CapsuleCollider2D>().size;
        animation.Tick(2);
        Assert.That(animation.CurrentFrame, Is.EqualTo(16));
        animation.PlayContextAnimation(LightAnimation.Climb); animation.Tick(.5f);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Climb));
        Assert.That(player.transform.position, Is.EqualTo(position));
        Assert.That(player.GetComponent<CapsuleCollider2D>().size, Is.EqualTo(shape));
        animation.ClearContextAnimation();
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Idle));
    }
    [UnityTest] public IEnumerator ShortFloorGapDoesNotShowTakeoffOrLandingWhileCrossing()
    {
        // Two real colliders with a small seam; the motor must still report its raw contacts.
        floor.GetComponent<BoxCollider2D>().size = new Vector2(4, 1);
        floor.transform.position = new Vector2(-1, -.5f); // right edge = 1
        var farFloor = new GameObject("Floor beyond narrow seam");
        farFloor.transform.SetParent(floor.transform, false);
        farFloor.transform.position = new Vector2(5.55f, -.5f);
        farFloor.AddComponent<BoxCollider2D>().size = new Vector2(8, 1); // left edge = 1.55
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        bool rawContactLost = false;
        string unexpectedPose = null;
        float lowestY = motor.Body.position.y;
        float deadline = Time.realtimeSinceStartup + 3;
        while (motor.Body.position.x < 2.5f && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            rawContactLost |= !motor.IsGrounded;
            lowestY = Mathf.Min(lowestY, motor.Body.position.y);
            if (unexpectedPose == null && (animation.CurrentAnimation == LightAnimation.Jump || animation.CurrentAnimation == LightAnimation.Landing))
                unexpectedPose = $"{animation.CurrentAnimation} at {motor.Body.position}, velocity {motor.Body.linearVelocity}, grounded {motor.IsGrounded}";
        }
        Assert.That(motor.Body.position.x, Is.GreaterThan(2.4f));
        Assert.That(rawContactLost, Is.True, "The test must exercise a real contact interruption.");
        Assert.That(lowestY, Is.GreaterThan(.6f));
        Assert.That(unexpectedPose, Is.Null, "No take-off or landing flinch at a short seam: " + unexpectedPose);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Walk));
    }
    [UnityTest] public IEnumerator GenuineWalkOffShowsAirborneHoldWithoutFakeJumpAnticipation()
    {
        int jumped = 0; motor.OnJumped += () => jumped++;
        floor.SetActive(false);
        yield return new WaitForSeconds(.22f);
        Assert.That(motor.IsGrounded, Is.False);
        Assert.That(motor.Body.linearVelocity.y, Is.LessThan(-1));
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        Assert.That(animation.CurrentFrame, Is.EqualTo(animation.Catalog.Find(LightAnimation.Jump).Frames.Length - 1));
        Assert.That(jumped, Is.Zero, "A fall does not synthesize a physical jump.");
    }
    [UnityTest] public IEnumerator DeliberateJumpBypassesVisualGroundGrace()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return new WaitForSeconds(.06f);
        Assert.That(motor.Body.linearVelocity.y, Is.GreaterThan(1));
        Assert.That(animation.CurrentAnimation, Is.EqualTo(LightAnimation.Jump));
        Assert.That(animation.CurrentFrame, Is.LessThan(2));
    }
}
