using System.Collections.Generic;
using TheLightenRoad.Lantern;
using TheLightenRoad.Player;
using TheLightenRoad.Player.Completion;
using TheLightenRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>Collectible Memento lore relic (standard or hidden secret Memento).</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MementoCollectible2D : MonoBehaviour
    {
        public string MementoId = "MEMENTO_01";
        public string Title = "Forgotten Note";
        public bool IsSecretMemento;

        public bool IsCollected { get; private set; }

        private SpriteRenderer relicRenderer;
        private SpriteRenderer auraRenderer;
        private Vector3 baseScale = Vector3.one;
        private float phaseSeed;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
            relicRenderer = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            phaseSeed = transform.position.x * 1.1f;

            if (relicRenderer != null)
            {
                if (relicRenderer.sprite == null) relicRenderer.sprite = Level1PixelArt.MementoSealLit;
                var auraGo = new GameObject("RelicAura");
                auraGo.transform.SetParent(transform, false);
                auraGo.transform.localScale = Vector3.one * 2.3f;
                auraRenderer = auraGo.AddComponent<SpriteRenderer>();
                auraRenderer.sprite = PixelSprites.Glow;
                auraRenderer.sortingLayerID = relicRenderer.sortingLayerID;
                auraRenderer.sortingOrder = relicRenderer.sortingOrder - 1;
            }
        }

        private void Update()
        {
            if (IsCollected) return;
            float t = Time.time * 2.4f + phaseSeed;
            transform.localScale = baseScale * (1f + Mathf.Sin(t) * 0.05f);
            if (auraRenderer != null)
            {
                Color tint = IsSecretMemento
                    ? new Color(0.84f, 0.56f, 1.0f, 0.28f + Mathf.Sin(t) * 0.1f)
                    : new Color(0.95f, 0.80f, 0.45f, 0.24f + Mathf.Sin(t) * 0.08f);
                auraRenderer.color = tint;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsCollected && other.GetComponentInParent<PlayerMotor2D>() != null)
            {
                Collect();
            }
        }

        public bool Collect()
        {
            if (IsCollected) return false;
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
