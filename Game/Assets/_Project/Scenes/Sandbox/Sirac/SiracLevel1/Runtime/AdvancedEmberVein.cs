using System;
using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// 4-state Ember Vein socket:
    /// 0 = Stopped/Drained, 1 = Normal (Orange), -1 = Invert (Crimson), 2 = Ethereal (Cyan).
    /// Supports corruption blocking (cleansed by light mirror beams) and visual cues (glow, spinning gear, barricade).
    /// </summary>
    public sealed class AdvancedEmberVein : MonoBehaviour, ILensReactive
    {
        [Header("Initial Socket & Corruption")]
        [SerializeField] private EmberColor initialEmber = EmberColor.None;
        [SerializeField] private bool isCorrupted;

        [Header("Visual Cues")]
        public SpriteRenderer GlowRenderer;
        public Transform GearTransform;
        public SpriteRenderer BarricadeRenderer;
        [SerializeField] private float gearSpinSpeed = 95f;

        public static readonly List<AdvancedEmberVein> ActiveVeins = new List<AdvancedEmberVein>(16);

        private EmberColor slottedEmber = EmberColor.None;
        private LanternLensType? activeLensOverride;
        private EmberInventoryController nearbyInventory;
        private int lastPublishedState = int.MinValue;

        public EmberColor SlottedEmber => slottedEmber;
        public bool IsCorrupted
        {
            get => isCorrupted;
            set
            {
                isCorrupted = value;
                RecomputeStateAndVisuals();
            }
        }

        public int VeinStateValue { get; private set; }

        public event Action<int> OnVeinStateChanged;

        private void Awake()
        {
            if (!ActiveVeins.Contains(this)) ActiveVeins.Add(this);
            LanternLensController.RegisterReactive(this);
            slottedEmber = initialEmber;
            RecomputeStateAndVisuals();
        }

        private void OnEnable()
        {
            if (!ActiveVeins.Contains(this)) ActiveVeins.Add(this);
            LanternLensController.RegisterReactive(this);
        }

        private void OnDestroy()
        {
            ActiveVeins.Remove(this);
            LanternLensController.UnregisterReactive(this);
        }

        private void Update()
        {
            if (GameControls.GameplayAllowed)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    var inv = nearbyInventory ?? FindFirstObjectByType<EmberInventoryController>();
                    if (inv != null && Vector2.Distance(transform.position, inv.transform.position) <= 2.4f)
                    {
                        TryInteract(inv);
                    }
                }
            }

            if (GearTransform != null && !isCorrupted)
            {
                float dir = VeinStateValue == 1 ? -1f : (VeinStateValue == -1 ? 1f : 0f);
                if (Mathf.Abs(dir) > 0.01f)
                {
                    GearTransform.Rotate(0f, 0f, dir * gearSpinSpeed * Time.deltaTime);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var inv = other.GetComponentInParent<EmberInventoryController>();
            if (inv != null) nearbyInventory = inv;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var inv = other.GetComponentInParent<EmberInventoryController>();
            if (inv != null && inv == nearbyInventory) nearbyInventory = null;
        }

        public bool TryInteract(EmberInventoryController inventory)
        {
            if (isCorrupted || inventory == null || inventory.IsUsageBlocked)
                return false;

            if (slottedEmber == EmberColor.None)
            {
                if (!inventory.TryTakeForVein(inventory.SelectedColor, out var taken))
                    return false;
                slottedEmber = taken;
            }
            else
            {
                var recovered = slottedEmber;
                slottedEmber = EmberColor.None;
                inventory.ReturnFromVein(recovered);
            }

            RecomputeStateAndVisuals();
            return true;
        }

        public void SetSlottedEmberDirect(EmberColor color)
        {
            slottedEmber = color;
            RecomputeStateAndVisuals();
        }

        public void ApplyLensOverride(LanternLensType? lens)
        {
            if (activeLensOverride == lens) return;
            activeLensOverride = lens;
            RecomputeStateAndVisuals();
        }

        public void ApplyLensField(LanternLensType lens, bool inRange)
        {
            if (!inRange || lens == LanternLensType.Normal)
            {
                ApplyLensOverride(null);
            }
            else
            {
                ApplyLensOverride(lens);
            }
        }

        public void CleanseCorruption()
        {
            if (!isCorrupted) return;
            isCorrupted = false;
            RecomputeStateAndVisuals();
        }

        public static int MapEmberToState(EmberColor color)
        {
            switch (color)
            {
                case EmberColor.Orange: return 1;
                case EmberColor.Crimson: return -1;
                case EmberColor.Cyan: return 2;
                default: return 0;
            }
        }

        private void RecomputeStateAndVisuals()
        {
            if (isCorrupted)
            {
                VeinStateValue = 0;
            }
            else if (activeLensOverride == LanternLensType.Invert)
            {
                VeinStateValue = -1;
            }
            else if (activeLensOverride == LanternLensType.Ethereal)
            {
                VeinStateValue = 2;
            }
            else
            {
                VeinStateValue = MapEmberToState(slottedEmber);
            }

            UpdateVisualCues();

            if (VeinStateValue != lastPublishedState)
            {
                lastPublishedState = VeinStateValue;
                OnVeinStateChanged?.Invoke(VeinStateValue);
            }
        }

        private void UpdateVisualCues()
        {
            if (GlowRenderer != null)
            {
                if (isCorrupted)
                    GlowRenderer.color = new Color(0.45f, 0.14f, 0.55f, 0.9f);
                else if (VeinStateValue == 1)
                    GlowRenderer.color = new Color(1f, 0.62f, 0.22f, 1f);
                else if (VeinStateValue == -1)
                    GlowRenderer.color = new Color(1f, 0.22f, 0.34f, 1f);
                else if (VeinStateValue == 2)
                    GlowRenderer.color = new Color(0.22f, 0.92f, 1f, 1f);
                else
                    GlowRenderer.color = new Color(0.25f, 0.28f, 0.34f, 0.7f);
            }

            if (BarricadeRenderer != null)
            {
                BarricadeRenderer.enabled = isCorrupted || VeinStateValue == 0;
                BarricadeRenderer.color = isCorrupted
                    ? new Color(0.32f, 0.10f, 0.36f, 0.95f)
                    : new Color(0.45f, 0.45f, 0.50f, 0.85f);
            }
        }
    }
}
