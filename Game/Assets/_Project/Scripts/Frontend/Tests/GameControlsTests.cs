using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Input;
using TheLightenRoad.Player;
using TheLightenRoad.Lantern;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public class GameControlsTests
    {
        private Gamepad pad;
        private GameObject actor;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [SetUp] public void Setup()
        {
            Time.timeScale=1;
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            pad=InputSystem.AddDevice<Gamepad>();
        }
        [TearDown] public void Cleanup()
        {
            if(actor!=null)Object.DestroyImmediate(actor);
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Time.timeScale=1;
        }
        [TestCase(0.1f,0f)] [TestCase(0.15f,0f)] [TestCase(1f,1f)] [TestCase(-1f,-1f)]
        public void DeadzonePreservesFullRange(float value,float expected)
        { Assert.That(GameControls.ApplyDeadzone(value,.15f),Is.EqualTo(expected).Within(.0001f)); }
        [Test] public void TriggerRequiresFullPressAndRelease()
        {
            var gate=new TriggerPressGate();
            Assert.That(gate.Read(.6f),Is.False);
            Assert.That(gate.Read(.7f),Is.True);
            Assert.That(gate.Read(.55f),Is.False);
            Assert.That(gate.Read(.9f),Is.False);
            Assert.That(gate.Read(.3f),Is.False);
            Assert.That(gate.Read(.8f),Is.True);
            gate.Seed(1);
            Assert.That(gate.Read(1),Is.False,"Hotplug with a held trigger must not spend an ember.");
        }
        [Test] public void SliderUsesDeliberateControllerSteps()
        {
            actor=new GameObject("Slider",typeof(RectTransform),typeof(MenuSlider));
            var slider=actor.GetComponent<MenuSlider>(); slider.value=.5f;
            var data=new AxisEventData(null){moveDir=MoveDirection.Right}; slider.OnMove(data);
            Assert.That(slider.value,Is.EqualTo(.55f).Within(.001f));
            Assert.That(data.used,Is.True);
            slider.NavigationStep=.01f;slider.OnMove(new AxisEventData(null){moveDir=MoveDirection.Left});
            Assert.That(slider.value,Is.EqualTo(.54f).Within(.001f));
        }
        [UnityTest] public IEnumerator DpadRemainsDigitalAndDriftDoesNotMovePlayer()
        {
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.04f,0)});yield return null;
            Assert.That(GameControls.MoveAxis(pad),Is.Zero);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;
            Assert.That(GameControls.MoveAxis(pad),Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FlareOnlyUsesRightTriggerAndDoesNotRepeatWhileHeld()
        {
            actor=new GameObject("Lantern",typeof(LanternController));
            var lantern=actor.GetComponent<LanternController>();
            yield return null;yield return null;
            int count=lantern.Fuel.TotalCharges;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return null;
            Assert.That(lantern.Fuel.TotalCharges,Is.EqualTo(count));
            InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=1});yield return null;
            Assert.That(lantern.Fuel.TotalCharges,Is.EqualTo(count-2));
            yield return null;yield return null;
            Assert.That(lantern.Fuel.TotalCharges,Is.EqualTo(count-2));
        }
        [UnityTest] public IEnumerator SprintIsIndependentFromFlareAndHasAToggle()
        {
            var floor=new GameObject("Control test floor",typeof(BoxCollider2D));
            floor.transform.position=new Vector3(0,-.5f);floor.GetComponent<BoxCollider2D>().size=new Vector2(100,1);
            actor=new GameObject("Motor",typeof(Rigidbody2D),typeof(CapsuleCollider2D),typeof(PlayerMotor2D));
            actor.transform.position=new Vector3(0,2);
            var motor=actor.GetComponent<PlayerMotor2D>();
            try
            {
                yield return new WaitForSeconds(.7f);
                Assert.That(motor.IsGrounded,Is.True);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right,rightTrigger=1});
                yield return new WaitForSeconds(.08f);
                Assert.That(motor.IsRunning,Is.False,"R2 must never double as sprint.");
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.LeftShoulder));
                yield return new WaitForSeconds(.08f);
                Assert.That(motor.IsRunning,Is.True);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.LeftStick));
                yield return new WaitForSeconds(.08f);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});
                yield return new WaitForSeconds(.08f);
                Assert.That(motor.IsRunning,Is.True,"L3 is a toggle, not a button to hold while steering.");
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return new WaitForSeconds(.08f);
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.right});yield return new WaitForSeconds(.08f);
                Assert.That(motor.IsRunning,Is.False,"Stopping movement releases toggle sprint.");
            }
            finally{Object.DestroyImmediate(floor);}
        }
        [UnityTest] public IEnumerator PausePreservesMomentumAndDoesNotEnableDisabledActors()
        {
            actor=new GameObject("PauseHost",typeof(PauseMenuController));
            var pause=actor.GetComponent<PauseMenuController>();
            var player=new GameObject("Disabled player",typeof(Rigidbody2D),typeof(CapsuleCollider2D),typeof(PlayerMotor2D));
            try
            {
                var motor=player.GetComponent<PlayerMotor2D>();motor.enabled=false;
                var body=player.GetComponent<Rigidbody2D>();body.linearVelocity=new Vector2(3,2);
                pause.Pause();
                Assert.That(body.linearVelocity,Is.EqualTo(new Vector2(3,2)));
                pause.Resume();yield return null;yield return null;
                Assert.That(motor.enabled,Is.False);
            }
            finally{Object.DestroyImmediate(player);}
        }
        [UnityTest] public IEnumerator PauseSuspendsLegacyVeinPollingWithoutChangingCircuitState()
        {
            actor=new GameObject("PauseHost",typeof(PauseMenuController));
            var pause=actor.GetComponent<PauseMenuController>();
            var veinType=System.Type.GetType("TheLightenRoad.Interaction.EmberVein, TheLightenRoad.Interaction");
            Assert.That(veinType,Is.Not.Null);
            var node=new GameObject("Existing vein");
            var disabledNode=new GameObject("Already disabled vein");
            try
            {
                var vein=(MonoBehaviour)node.AddComponent(veinType);
                var disabled=(MonoBehaviour)disabledNode.AddComponent(veinType);disabled.enabled=false;
                var state=veinType.GetField("currentState");
                state.SetValue(vein,System.Enum.ToObject(state.FieldType,1));
                pause.Pause();
                Assert.That(vein.enabled,Is.False);
                Assert.That(state.GetValue(vein).ToString(),Is.EqualTo("Powered"));
                pause.Resume();
                Assert.That(vein.enabled,Is.False,"Do not replay menu input into the legacy interaction poll.");
                yield return null;yield return null;yield return null;
                Assert.That(vein.enabled,Is.True);
                Assert.That(disabled.enabled,Is.False);
                Assert.That(state.GetValue(vein).ToString(),Is.EqualTo("Powered"));
            }
            finally{Object.DestroyImmediate(node);Object.DestroyImmediate(disabledNode);}
        }
        [UnityTest] public IEnumerator KeyboardPromptsWinOverConnectedIdleController()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            actor=new GameObject("Glyphs",typeof(ControllerGlyphService));
            ControllerGlyphService.ConfiguredStyle=ControllerPromptStyle.Auto;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;
            Assert.That(ControllerGlyphService.ActiveStyle,Is.Not.EqualTo(ControllerPromptStyle.Keyboard));
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.05f,0)});
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return null;
            Assert.That(ControllerGlyphService.ActiveStyle,Is.EqualTo(ControllerPromptStyle.Keyboard));
            InputSystem.RemoveDevice(keyboard);
        }
    }
}
