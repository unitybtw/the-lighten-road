using System;
using System.Collections.Generic;
using System.Reflection;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.Level1
{
    public enum LanternLensType
    {
        Normal = 0,
        Ethereal = 1,
        Invert = 2
    }

    public enum EmberColor
    {
        None = 0,
        Orange = 1,
        Cyan = 2,
        Crimson = 3
    }

    /// <summary>Implemented by scene mechanisms and veins that respond to Lantern Lens aura.</summary>
    public interface ILensReactive
    {
        void ApplyLensField(LanternLensType lens, bool inRange);
    }

    public enum VeinMechanismType
    {
        BlastDoor,
        Piston,
        Elevator,
        MovingPlatform,
        MovingBlock1x1,
        PusherMechanism,
        GhostDoor,
        GhostCrate,
        GhostElevator
    }

    public enum ConduitPipeShape
    {
        Straight,
        Corner,
        Tee,
        Cross
    }
}
