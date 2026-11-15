using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Rotatable conduit pipe or multi-branch junction node.</summary>
    public sealed class ConduitNode2D : MonoBehaviour
    {
        public Vector2Int GridPos;
        public ConduitPipeShape Shape = ConduitPipeShape.Straight;
        public int RotationSteps;
        public int SolvedRotationSteps;
        public bool IsJunction;
        public bool Locked;
        public SpriteRenderer NodeRenderer;

        [HideInInspector] public ConduitGridController Grid;

        public bool IsPowered { get; internal set; }

        private Vector3 baseScale = Vector3.one;
        private float interactPulse;
        private SpriteRenderer auraRenderer;

        private void Awake()
        {
            if (NodeRenderer == null) NodeRenderer = GetComponent<SpriteRenderer>();
            if (NodeRenderer != null && NodeRenderer.sprite == null)
            {
                NodeRenderer.sprite = Level1PixelArt.GetConduitPipe(Shape);
            }
            baseScale = transform.localScale;
            EnsureAuraRenderer();
            ApplyRotationVisual();
        }

        private void Update()
        {
            if (interactPulse > 0f)
            {
                interactPulse = Mathf.MoveTowards(interactPulse, 0f, Time.deltaTime / 0.18f);
                float bump = 1f + Mathf.Sin(interactPulse * Mathf.PI) * 0.14f;
                transform.localScale = baseScale * bump;
            }

            if (auraRenderer != null && IsPowered)
            {
                float pulse = 0.22f + Mathf.Sin(Time.time * 3.8f + GridPos.x * 0.7f) * 0.08f;
                auraRenderer.color = new Color(1.0f, 0.72f, 0.25f, pulse);
            }

            if (!GameControls.GameplayAllowed || Locked) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                var motor = Player.PlayerMotor2D.ActiveInstance ?? FindFirstObjectByType<Player.PlayerMotor2D>();
                var player = motor != null ? motor.gameObject : GameObject.FindGameObjectWithTag("Player");
                if (player != null && Vector2.Distance(transform.position, player.transform.position) <= 2.4f)
                {
                    RotateClockwise();
                }
            }
        }

        public void RotateClockwise()
        {
            if (Locked) return;
            interactPulse = 1f;
            SetRotationSteps((RotationSteps + 1) % 4);
        }

        public void SetRotationSteps(int steps)
        {
            RotationSteps = ((steps % 4) + 4) % 4;
            ApplyRotationVisual();
            if (Grid != null) Grid.EvaluateNetwork();
        }

        public void ApplyRotationVisual()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, -90f * RotationSteps);
            RefreshPowerVisual();
        }

        public void RefreshPowerVisual()
        {
            if (NodeRenderer == null) NodeRenderer = GetComponent<SpriteRenderer>();
            if (NodeRenderer == null) return;
            if (NodeRenderer.sprite == null) NodeRenderer.sprite = Level1PixelArt.GetConduitPipe(Shape);
            NodeRenderer.color = IsPowered
                ? new Color(1.0f, 0.78f, 0.34f, 1f)
                : (IsJunction ? new Color(0.56f, 0.76f, 0.88f, 1f) : new Color(0.45f, 0.56f, 0.66f, 1f));
            if (auraRenderer != null)
            {
                auraRenderer.enabled = IsPowered;
            }
        }

        private void EnsureAuraRenderer()
        {
            if (auraRenderer != null || NodeRenderer == null) return;
            var auraGo = new GameObject("PoweredAura");
            auraGo.transform.SetParent(transform, false);
            auraGo.transform.localScale = Vector3.one * 1.9f;
            auraRenderer = auraGo.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = TheLightenRoad.UI.PixelSprites.Glow;
            auraRenderer.sortingLayerID = NodeRenderer.sortingLayerID;
            auraRenderer.sortingOrder = NodeRenderer.sortingOrder - 1;
            auraRenderer.enabled = IsPowered;
        }

        /// <summary>Directions: 0 = North, 1 = East, 2 = South, 3 = West.</summary>
        public bool HasPort(int direction)
        {
            int dir = ((direction % 4) + 4) % 4;
            int local = ((dir - RotationSteps) % 4 + 4) % 4;
            switch (Shape)
            {
                case ConduitPipeShape.Straight: return local == 0 || local == 2;
                case ConduitPipeShape.Corner: return local == 0 || local == 1;
                case ConduitPipeShape.Tee: return local == 0 || local == 1 || local == 3;
                case ConduitPipeShape.Cross: return true;
                default: return false;
            }
        }
    }
}
