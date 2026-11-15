using System;
using System.Collections.Generic;
using System.Reflection;
using TheLightenRoad.Input;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Climbable vertical ladder supporting W/S or Up/Down traversal without gravity drop.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ClimbableLadder2D : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float height = 4f;
        [SerializeField, Min(0.5f)] private float climbSpeed = 3.4f;

        private BoxCollider2D triggerCollider;

        public float Height
        {
            get => height;
            set
            {
                height = Mathf.Max(0.5f, value);
                SyncColliderSize();
            }
        }

        public float ClimbSpeed { get => climbSpeed; set => climbSpeed = Mathf.Max(0.5f, value); }
        public float BottomY => transform.position.y;
        public float TopY => transform.position.y + height;
        public float CenterX => transform.position.x;

        private void Awake()
        {
            SyncColliderSize();
        }

        public void SyncColliderSize()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider2D>();
            if (triggerCollider == null) return;
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(0.9f, height);
            triggerCollider.offset = new Vector2(0f, height * 0.5f);
        }

        public bool ContainsPosition(Vector3 worldPos)
        {
            return Mathf.Abs(worldPos.x - CenterX) <= 0.75f
                   && worldPos.y >= BottomY - 0.35f
                   && worldPos.y <= TopY + 0.35f;
        }
    }
}
