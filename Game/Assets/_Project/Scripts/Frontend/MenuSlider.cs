using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Predictable controller steps; pointer dragging retains continuous precision.</summary>
    public sealed class MenuSlider : Slider
    {
        public float NavigationStep = .05f;
        public override void OnMove(AxisEventData eventData)
        {
            if (IsActive() && IsInteractable() &&
                (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                value += (eventData.moveDir == MoveDirection.Right ? 1f : -1f) * NavigationStep;
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }
    }
}
