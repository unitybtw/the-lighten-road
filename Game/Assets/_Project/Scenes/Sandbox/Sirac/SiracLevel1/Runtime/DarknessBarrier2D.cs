using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Solid darkness barrier that dissolves when struck by a light beam.</summary>
    public sealed class DarknessBarrier2D : MonoBehaviour
    {
        [Min(0.1f)] public float MeltDuration = 0.6f;
        public Collider2D BarrierCollider;
        public SpriteRenderer BarrierRenderer;

        public float MeltProgress { get; private set; }
        public bool IsMelted { get; private set; }

        private void Awake()
        {
            if (BarrierCollider == null) BarrierCollider = GetComponent<Collider2D>();
            if (BarrierRenderer == null) BarrierRenderer = GetComponent<SpriteRenderer>();
            if (BarrierRenderer != null && BarrierRenderer.sprite == null)
            {
                BarrierRenderer.sprite = Level1PixelArt.DarknessVeil;
            }
        }

        private void Update()
        {
            if (IsMelted || BarrierRenderer == null) return;
            float wave = Mathf.Sin(Time.time * 3.1f + transform.position.x) * 0.04f;
            float a = Mathf.Lerp(0.92f + wave, 0f, MeltProgress);
            BarrierRenderer.color = new Color(0.08f, 0.06f, 0.12f, Mathf.Clamp01(a));
        }

        public void ApplyBeamHit(float dt)
        {
            if (IsMelted) return;
            MeltProgress = Mathf.Clamp01(MeltProgress + Mathf.Max(0f, dt) / Mathf.Max(0.05f, MeltDuration));
            if (BarrierRenderer != null)
            {
                BarrierRenderer.color = new Color(0.08f, 0.06f, 0.12f, Mathf.Lerp(0.95f, 0f, MeltProgress));
            }
            if (MeltProgress >= 1f)
            {
                IsMelted = true;
                if (BarrierCollider != null) BarrierCollider.enabled = false;
                if (BarrierRenderer != null) BarrierRenderer.enabled = false;
            }
        }
    }
}
