using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TheLightenRoad.Frontend.Tests
{
    public sealed class LifecycleRegressionTests
    {
        private Gamepad pad, previous;
        private GameObject actor;
        private readonly List<byte[]> reports = new List<byte[]>();
        private bool failCommand;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp] public void Setup()
        {
            previous=Gamepad.current;
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Time.timeScale=1;
            pad=(Gamepad)InputSystem.AddDevice(new InputDeviceDescription {
                interfaceName="HID",manufacturer="Sony",product="DualSense Wireless Controller",
                serial="Lifecycle-regression-synthetic-controller",
                capabilities="{\"vendorId\":1356,\"productId\":3302,\"inputReportSize\":78,\"outputReportSize\":78}"
            });
            pad.MakeCurrent();Subscribe(true);
            ControllerGlyphService.EnsureExists();
            ControllerGlyphService.Instance.enabled=true;
            ControllerGlyphService.Instance.SendMessage("OnApplicationFocus",true);
            ControllerGlyphService.Instance.SendMessage("OnApplicationPause",false);
            ControllerGlyphService.VibrationEnabled=true;
            ControllerGlyphService.AdaptiveTriggersEnabled=true;
            DualSenseBluetoothRumble.ResetState();
            reports.Clear();failCommand=false;
        }
        [TearDown] public void Cleanup()
        {
            if(actor!=null)Object.DestroyImmediate(actor);
            ControllerGlyphService.StopRumble();
            ControllerGlyphService.Instance.enabled=true;
            ControllerLightbar.Instance?.SendMessage("OnApplicationFocus",true);
            Subscribe(false);
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(previous!=null&&previous.added)previous.MakeCurrent();
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Time.timeScale=1;
        }
        private unsafe void Subscribe(bool add)
        { if(add)InputSystem.onDeviceCommand+=Record;else InputSystem.onDeviceCommand-=Record; }
        private unsafe long? Record(InputDevice device, InputDeviceCommand* command)
        {
            if(device!=pad || command->type!=default(DualSenseBluetoothRumble.BluetoothOutputCommand).typeStatic)return null;
            var bytes=new byte[78];var ptr=(byte*)command+InputDeviceCommand.BaseCommandSize;
            for(int i=0;i<bytes.Length;i++)bytes[i]=ptr[i];
            reports.Add(bytes);return failCommand?-1:1;
        }
        [Test] public void LightbarOutputDoesNotWriteMotorOrTriggerChannels()
        {
            Assert.That(DualSenseBluetoothRumble.IsBluetooth(pad),Is.True);
            var effect=DualSenseTriggerEffect.Continuous(.3f,.2f);
            DualSenseBluetoothRumble.Send(pad,.3f,.4f,DualSenseTriggerEffect.Off,effect);
            Assert.That(DualSenseBluetoothRumble.SetLightbarColor(pad,new Color32(40,80,120,255)),Is.True);
            var led=reports[reports.Count-1];
            Assert.That(led[3],Is.Zero,"An LED update must leave motor/trigger enable bits clear.");
            Assert.That(led[4]&4,Is.EqualTo(4));
            Assert.That(new[]{led[47],led[48],led[49]},Is.EqualTo(new byte[]{40,80,120}));
        }
        [Test] public void FailedTriggerReportRetriesWithoutCachingAnUnsentEffect()
        {
            var effect=DualSenseTriggerEffect.Continuous(.3f,.2f);
            failCommand=true;
            Assert.That(DualSenseBluetoothRumble.Send(pad,0,0,DualSenseTriggerEffect.Off,effect),Is.False);
            failCommand=false;
            Assert.That(DualSenseBluetoothRumble.Send(pad,0,0,DualSenseTriggerEffect.Off,effect),Is.True);
            Assert.That(reports.Count,Is.EqualTo(2));
            Assert.That(reports[1][3]&4,Is.EqualTo(4),"Retry must still program the right trigger.");
        }
        [UnityTest] public IEnumerator ThrottledTriggerChangeRemainsPendingUntilItIsSent()
        {
            var first=DualSenseTriggerEffect.Continuous(.3f,.2f);
            var next=DualSenseTriggerEffect.Section(.35f,.65f,.2f);
            Assert.That(DualSenseBluetoothRumble.Send(pad,0,0,DualSenseTriggerEffect.Off,first),Is.True);
            bool sent=DualSenseBluetoothRumble.Send(pad,0,0,DualSenseTriggerEffect.Off,next);
            if(!sent)
            {
                yield return new WaitForSecondsRealtime(.04f);
                Assert.That(DualSenseBluetoothRumble.Send(pad,0,0,DualSenseTriggerEffect.Off,next),Is.True);
            }
            Assert.That(reports.FindLast(r => (r[3]&4)!=0)[13],Is.EqualTo((byte)DualSenseTriggerMode.Section));
        }
        [UnityTest] public IEnumerator SwitchingPadsReleasesThePreviousIdleTrigger()
        {
            CreatePlayer();yield return null;yield return null;
            ControllerGlyphService.UpdateAdaptiveTriggers(true);
            Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.True);
            yield return new WaitForSecondsRealtime(.04f);
            failCommand=true;
            ControllerGlyphService.UpdateAdaptiveTriggers(true);
            failCommand=false;
            var other=InputSystem.AddDevice<Gamepad>();
            try
            {
                other.MakeCurrent();yield return null;yield return null;
                Assert.That(reports.FindLast(r => (r[3]&4)!=0)[13],Is.EqualTo(5));
                Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.False);
            }
            finally{InputSystem.RemoveDevice(other);}
        }
        private LanternController CreatePlayer()
        {
            actor=new GameObject("Lifecycle player",typeof(Rigidbody2D),typeof(CapsuleCollider2D),typeof(PlayerMotor2D));
            actor.GetComponent<Rigidbody2D>().gravityScale=0;
            return actor.AddComponent<LanternController>();
        }
        [UnityTest] public IEnumerator AdaptiveTriggerHonorsVibrationOffAndComponentShutdown()
        {
            CreatePlayer();yield return null;yield return null;
            ControllerGlyphService.UpdateAdaptiveTriggers(true);
            Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.True);
            ControllerGlyphService.VibrationEnabled=false;yield return null;yield return null;
            Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.False);
            ControllerGlyphService.VibrationEnabled=true;yield return null;yield return null;
            Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.True);
            ControllerGlyphService.Instance.enabled=false;
            Assert.That(ControllerGlyphService.CurrentRightTriggerEffect.IsActive,Is.False);
            Assert.That(reports.FindLast(r => (r[3] & 4) != 0)[13],Is.EqualTo(5),"Idle trigger must explicitly release when service stops.");
        }
        [UnityTest] public IEnumerator DeathBlocksFlareAndHeldTriggerDoesNotSpendAgainAfterRespawn()
        {
            var lantern=CreatePlayer();
            var respawn=actor.AddComponent<PlayerRespawnController>();respawn.AutoRespawnEnabled=false;respawn.AllowManualRespawnInput=false;
            yield return null;yield return null;
            respawn.Kill(DeathReason.Hazard);
            Assert.That(GameControls.GameplayAllowed,Is.False);
            InputSystem.QueueDeltaStateEvent(pad.rightTrigger,(byte)255);yield return null;
            Assert.That(lantern.Fuel.TotalEmbers,Is.EqualTo(3));
            Assert.That(lantern.HasActiveScout,Is.False);
            respawn.Respawn();yield return null;yield return null;yield return null;
            Assert.That(GameControls.GameplayAllowed,Is.True);
            Assert.That(lantern.Fuel.TotalEmbers,Is.EqualTo(3));
        }
        [UnityTest] public IEnumerator DisabledPauseOwnerRestoresTimeAndInputWithoutAffectingOtherOwners()
        {
            actor=new GameObject("Pause owner",typeof(PauseMenuController));
            var menu=actor.GetComponent<PauseMenuController>();
            Time.timeScale=.5f;menu.Pause();
            var duplicate=new GameObject("Duplicate pause",typeof(PauseMenuController));
            Assert.That(PauseMenuController.IsPaused,Is.True,"Duplicate destruction must not resume the active owner.");
            Assert.That(Time.timeScale,Is.Zero);
            if(duplicate!=null)Object.DestroyImmediate(duplicate);
            menu.enabled=false;
            Assert.That(Time.timeScale,Is.EqualTo(.5f));
            Assert.That(PauseMenuController.IsPaused,Is.False);
            yield return null;yield return null;
            Assert.That(GameControls.GameplayAllowed,Is.True);
        }
        [UnityTest] public IEnumerator BackgroundLightbarStaysDarkDuringSpecialSequences()
        {
            ControllerLightbar.EnsureExists();ControllerLightbar.Enabled=true;
            var service=ControllerLightbar.Instance;
            service.SendMessage("OnApplicationFocus",false);
            service.TriggerDeathSequence();yield return null;yield return null;
            var led=reports[reports.Count-1];
            Assert.That(led[47]+led[48]+led[49],Is.Zero,"Special effects must respect application focus.");
        }
    }
}
