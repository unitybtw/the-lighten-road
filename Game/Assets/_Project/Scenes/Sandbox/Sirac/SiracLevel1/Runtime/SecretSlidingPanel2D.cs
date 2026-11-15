using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Secret sliding wall/panel that opens when powered by ConduitGridController.</summary>
    public sealed class SecretSlidingPanel2D : MonoBehaviour
    {
        public Vector3 ClosedPos;
        public Vector3 OpenPos;
        public Collider2D PanelCollider;
        [Min(0.2f)] public float SlideSpeed = 4.0f;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (PanelCollider == null) PanelCollider = GetComponent<Collider2D>();
            if (ClosedPos == Vector3.zero && OpenPos == Vector3.zero)
            {
                ClosedPos = transform.position;
                OpenPos = transform.position + Vector3.up * 2.4f;
            }
        }

        private void Update()
        {
            Vector3 target = IsOpen ? OpenPos : ClosedPos;
            transform.position = Vector3.MoveTowards(transform.position, target, SlideSpeed * Time.deltaTime);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (PanelCollider != null)
            {
                PanelCollider.enabled = !open;
            }
        }
    }
}
