using System;
using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Collectible non-consuming colored Ember (Orange, Cyan, or Crimson).</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ColoredEmberPickup2D : MonoBehaviour
    {
        public EmberColor ColorType = EmberColor.Orange;
        public bool IsCollected { get; private set; }

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer auraRenderer;
        private Vector3 baseScale = Vector3.one;
        private float phaseSeed;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            phaseSeed = transform.position.x * 0.8f + transform.position.y * 0.5f;
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
            EnsureAuraRenderer();
            RefreshVisual();
        }

        private void Update()
        {
            if (IsCollected) return;
            float t = Time.time * 2.8f + phaseSeed;
            float s = 1f + Mathf.Sin(t) * 0.06f;
            transform.localScale = baseScale * s;
            if (auraRenderer != null)
            {
                Color c = LanternLensController.GetEmberColorTint(ColorType);
                auraRenderer.color = new Color(c.r, c.g, c.b, 0.26f + Mathf.Sin(t) * 0.1f);
            }
        }

        public void RefreshVisual()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = Level1PixelArt.VeinEmberCrystal;
                spriteRenderer.color = LanternLensController.GetEmberColorTint(ColorType);
            }
        }

        private void EnsureAuraRenderer()
        {
            if (auraRenderer != null || spriteRenderer == null) return;
            var auraGo = new GameObject("EmberAura");
            auraGo.transform.SetParent(transform, false);
            auraGo.transform.localScale = Vector3.one * 2.4f;
            auraRenderer = auraGo.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = TheLightenRoad.UI.PixelSprites.Glow;
            auraRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            auraRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsCollected) return;
            var inv = other.GetComponentInParent<EmberInventoryController>();
            if (inv != null)
            {
                Collect(inv);
            }
        }

        public bool Collect(EmberInventoryController inventory)
        {
            if (IsCollected || inventory == null) return false;
            IsCollected = true;
            inventory.AddEmber(ColorType);
            gameObject.SetActive(false);
            return true;
        }
    }
}
