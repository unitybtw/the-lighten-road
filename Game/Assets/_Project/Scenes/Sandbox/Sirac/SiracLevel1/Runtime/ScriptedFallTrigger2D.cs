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
    /// <summary>Scripted breakaway floor drop introducing lower floor progression.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ScriptedFallTrigger2D : MonoBehaviour
    {
        public Collider2D BreakawayFloorCollider;
        public SpriteRenderer BreakawayFloorRenderer;

        public bool HasTriggeredFall { get; private set; }

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasTriggeredFall && other.GetComponentInParent<PlayerMotor2D>() != null)
            {
                TriggerFall();
            }
        }

        public void TriggerFall()
        {
            HasTriggeredFall = true;
            if (BreakawayFloorCollider != null) BreakawayFloorCollider.enabled = false;
            if (BreakawayFloorRenderer != null) BreakawayFloorRenderer.enabled = false;
        }
    }
}
