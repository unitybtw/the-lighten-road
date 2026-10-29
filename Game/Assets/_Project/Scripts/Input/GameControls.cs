using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Input
{
    /// <summary>Shared input contract. Keeps gameplay controls and their displayed guide in agreement.</summary>
    public static class GameControls
    {
        public const string DeadzoneKey = "TLR.Menu.StickDeadzone";
        public const string JumpPath = "<Gamepad>/buttonSouth";
        public const string SprintHoldPath = "<Gamepad>/leftShoulder";
        public const string SprintTogglePath = "<Gamepad>/leftStickPress";
        public const string FlarePath = "<Gamepad>/rightTrigger";
        private static readonly HashSet<int> blockers = new HashSet<int>();
        private static int suppressedThroughFrame = -1;
        /// <summary>Owned modal/traversal locks, independent of the simulation clock.</summary>
        public static bool HasGameplayBlockers => blockers.Count > 0;
        public static bool GameplayAllowed => GameplayAllowedExcept(null);
        /// <summary>A traversal can own an input block while still respecting pause and other owners.</summary>
        public static bool GameplayAllowedExcept(Object owner)
        {
            if (Time.timeScale <= 0f || Time.frameCount <= suppressedThroughFrame) return false;
            int excluded = owner != null ? owner.GetInstanceID() : 0;
            foreach (int blocker in blockers) if (blocker != excluded) return false;
            return true;
        }
        public static void BlockGameplay(Object owner, bool blocked)
        {
            if(owner == null) return;
            int key=owner.GetInstanceID();
            if(blocked) blockers.Add(key);
            else if(blockers.Remove(key)) suppressedThroughFrame=Mathf.Max(suppressedThroughFrame,Time.frameCount+1);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { blockers.Clear(); suppressedThroughFrame=-1; }
        public static Gamepad ActivePad
        {
            get
            {
                var current=Gamepad.current;
                if(current!=null && current.added && current.enabled)return current;
                foreach(var pad in Gamepad.all)if(pad.added && pad.enabled)return pad;
                return null;
            }
        }
        public static float ApplyDeadzone(float raw, float deadzone)
        {
            if(float.IsNaN(raw)||float.IsInfinity(raw))return 0;
            if(float.IsNaN(deadzone)||float.IsInfinity(deadzone))deadzone=.15f;
            deadzone=Mathf.Clamp(deadzone,.05f,.40f);
            float magnitude=Mathf.Clamp01(Mathf.Abs(raw));
            return magnitude<=deadzone?0:Mathf.Sign(raw)*(magnitude-deadzone)/(1-deadzone);
        }
        public static float MoveAxis(Gamepad pad)
        {
            if(pad==null || !pad.added || !pad.enabled)return 0;
            float digital=pad.dpad.ReadValue().x;
            if(Mathf.Abs(digital)>.5f)return Mathf.Sign(digital);
            // Read the raw axis so the configured game deadzone is applied exactly once.
            return ApplyDeadzone(pad.leftStick.ReadUnprocessedValue().x,PlayerPrefs.GetFloat(DeadzoneKey,.15f));
        }
        public static bool HasDeliberateActivity(Gamepad pad)
        {
            if(pad==null || !pad.added || !pad.enabled)return false;
            foreach(var button in pad.allControls)
                if(button is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)return true;
            return pad.leftStick.ReadUnprocessedValue().sqrMagnitude>.16f ||
                   pad.rightStick.ReadUnprocessedValue().sqrMagnitude>.16f;
        }
    }

    /// <summary>Separate press/release thresholds avoid repeated flares near a trigger's actuation point.</summary>
    public struct TriggerPressGate
    {
        private bool held;
        public void Seed(float value) => held=value>.35f;
        public bool Read(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))value=0;
            if(value<=.35f){held=false;return false;}
            if(held || value<.65f)return false;
            held=true;return true;
        }
    }
}
