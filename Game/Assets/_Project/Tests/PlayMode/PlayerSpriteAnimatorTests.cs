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
        Assert.That(data.Sequences.Length, Is.EqualTo(8));
        Assert.That(data.Sequences.Sum(s => s.Frames.Length), Is.EqualTo(121));
        foreach (var s in data.Sequences)
        {
            Assert.That(s.Durations.All(d => Mathf.Abs(d - .1f) < .0001f), Is.True);
            Assert.That(s.Frames.All(f => f != null && Mathf.Abs(f.pixelsPerUnit - 36) < .001f), Is.True);
            Assert.That(s.PreviewClip.length, Is.EqualTo(s.Duration).Within(.001f));
            Assert.That(s.LanternOffsets.Length, Is.EqualTo(s.Frames.Length));
            Assert.That(s.Frames.All(f => Mathf.Approximately(f.rect.width, 70)), Is.True);
            Assert.That(s.Frames.All(f => Mathf.Approximately(f.rect.height, s.Kind == LightAnimation.Climb ? 80 : 70)), Is.True);
        }
        Assert.That(data.Find(LightAnimation.Climb).Frames[0].pivot.y, Is.EqualTo(30));
        Assert.That(data.Find(LightAnimation.Climb).ReachOffsetsX.Length, Is.EqualTo(27));
        Assert.That(data.Find(LightAnimation.Climb).ReachOffsetsX.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), Is.True);
        Assert.That(data.Find(LightAnimation.Idle).Frames[0].pivot.y, Is.EqualTo(35));
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
}
