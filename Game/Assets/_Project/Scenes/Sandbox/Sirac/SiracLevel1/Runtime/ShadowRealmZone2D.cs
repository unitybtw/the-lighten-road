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
    /// <summary>
    /// Enforces Shadow Realm rules while the player is inside:
    /// - Monochrome Red screen vision
    /// - Player's lantern extinguished; Flare and Ember usage disabled
    /// - Ambient environmental lights remain active as navigation beacons
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ShadowRealmZone2D : MonoBehaviour
    {
        public Light2D[] AmbientGuideLights;
        public Color MonochromeRedTint = new Color(0.62f, 0.05f, 0.08f, 0.44f);

        public bool IsPlayerInside { get; private set; }

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null) col.isTrigger = true;
            SyncAmbientLights();
        }

        private void OnDisable()
        {
            if (IsPlayerInside)
            {
                SetPlayerInside(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMotor2D>() != null)
            {
                SetPlayerInside(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerMotor2D>() != null)
            {
                SetPlayerInside(false);
            }
        }

        public void SetPlayerInside(bool inside)
        {
            IsPlayerInside = inside;
            var lens = FindFirstObjectByType<LanternLensController>();
            var lantern = FindFirstObjectByType<LanternController>();

            if (lens != null)
            {
                lens.IsSuppressedByShadowRealm = inside;
                lens.SyncLanternLight();
            }

            if (inside && lantern != null && lantern.IsFlared)
            {
                lantern.ResetToDim();
            }

            SyncAmbientLights();
        }

        private void SyncAmbientLights()
        {
            if (AmbientGuideLights == null) return;
            foreach (var l in AmbientGuideLights)
            {
                if (l == null) continue;
                l.enabled = true;
                l.color = IsPlayerInside ? new Color(1f, 0.24f, 0.20f, 1f) : new Color(0.95f, 0.55f, 0.35f, 1f);
            }
        }
    }
}
