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

        public static readonly List<LightMirror2D> ActiveMirrors = new List<LightMirror2D>(8);

        private void Awake()
        {
            if (!ActiveMirrors.Contains(this)) ActiveMirrors.Add(this);
            if (MirrorRenderer == null) MirrorRenderer = GetComponent<SpriteRenderer>();
            SyncVisual();
        }

        private void OnEnable()
        {
            if (!ActiveMirrors.Contains(this)) ActiveMirrors.Add(this);
        }

        private void OnDisable()
        {
            ActiveMirrors.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveMirrors.Remove(this);
        }

        private void Update()
        {
            if (!GameControls.GameplayAllowed) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                var motor = Player.PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<Player.PlayerMotor2D>();
                var player = motor != null ? motor.gameObject : GameObject.FindGameObjectWithTag("Player");
                if (player != null && IsClosestMirrorTo(player.transform.position, 2.5f))
                {
                    RotateMirror(45f);
                }
            }
        }

        private bool IsClosestMirrorTo(Vector2 playerPos, float maxRadius)
        {
            float myDistSq = ((Vector2)transform.position - playerPos).sqrMagnitude;
            if (myDistSq > maxRadius * maxRadius) return false;

            for (int i = ActiveMirrors.Count - 1; i >= 0; i--)
            {
                var other = ActiveMirrors[i];
                if (other == null)
                {
                    ActiveMirrors.RemoveAt(i);
                    continue;
                }
                if (other == this || !other.isActiveAndEnabled) continue;
                float otherDistSq = ((Vector2)other.transform.position - playerPos).sqrMagnitude;
                if (otherDistSq < myDistSq - 0.0001f ||
                    (Mathf.Abs(otherDistSq - myDistSq) <= 0.0001f && other.GetInstanceID() < GetInstanceID()))
                {
                    return false;
                }
            }
            return true;
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
