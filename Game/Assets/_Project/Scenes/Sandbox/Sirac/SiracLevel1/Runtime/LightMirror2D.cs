using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Rotatable reflective mirror. Corrupted mirrors absorb incoming beams instead of reflecting.</summary>
    public sealed class LightMirror2D : MonoBehaviour
    {
        public float MirrorAngleDegrees = 135f;
        public bool IsCorrupted;
        public MirrorRail2D AttachedRail;
        public SpriteRenderer MirrorRenderer;

        private void Awake()
        {
            if (MirrorRenderer == null) MirrorRenderer = GetComponent<SpriteRenderer>();
            SyncVisual();
        }

        private void Update()
        {
            if (!GameControls.GameplayAllowed) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                var motor = Player.PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<Player.PlayerMotor2D>();
                var player = motor != null ? motor.gameObject : GameObject.FindGameObjectWithTag("Player");
                if (player != null && Vector2.Distance(transform.position, player.transform.position) <= 2.5f)
                {
                    RotateMirror(45f);
                }
            }
        }

        public void RotateMirror(float stepDegrees = 45f)
        {
            MirrorAngleDegrees = Mathf.Repeat(MirrorAngleDegrees + stepDegrees, 360f);
            SyncVisual();
        }

        public void SetAngleDegrees(float angleDegrees)
        {
            MirrorAngleDegrees = Mathf.Repeat(angleDegrees, 360f);
            SyncVisual();
        }

        public Vector2 GetNormal()
        {
            float rad = MirrorAngleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        public void SyncVisual()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, MirrorAngleDegrees);
            if (MirrorRenderer != null)
            {
                MirrorRenderer.color = IsCorrupted
                    ? new Color(0.28f, 0.12f, 0.32f, 0.95f)
                    : new Color(0.84f, 0.96f, 1.0f, 1f);
            }
        }
    }
}
