using System;
using System.Collections.Generic;
using TheLightenRoad.Frontend;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Tracks carried Vein Embers (Orange/Cyan/Crimson). Embers are inserted into
    /// and recovered from AdvancedEmberVein sockets without being destroyed, with at
    /// most one instance per color.
    /// </summary>
    public sealed class EmberInventoryController : MonoBehaviour
    {
        [SerializeField] private List<EmberColor> startingEmbers = new List<EmberColor>();

        private readonly List<EmberColor> carriedEmbers = new List<EmberColor>();
        private LanternLensController lensController;

        public IReadOnlyList<EmberColor> CarriedEmbers => carriedEmbers;
        public int EmberCount => carriedEmbers.Count;
        public EmberColor SelectedColor { get; private set; } = EmberColor.None;
        public bool IsUsageBlocked => lensController != null && lensController.IsSuppressedByShadowRealm;

        public event Action OnInventoryChanged;

        private void Awake()
        {
            lensController = GetComponent<LanternLensController>();
            foreach (var c in startingEmbers)
            {
                if (c != EmberColor.None && !carriedEmbers.Contains(c))
                    carriedEmbers.Add(c);
            }
            SyncSelectedColor();
        }

        private void Update()
        {
            if (!GameControls.GameplayAllowed || ArchiveCodexMenu.IsOpen || ArchiveCodexMenu.ClosedThisFrame)
                return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.cKey.wasPressedThisFrame)
            {
                CycleSelectedEmber();
            }
            else if (kb.digit1Key.wasPressedThisFrame && carriedEmbers.Count >= 1)
            {
                SelectColor(carriedEmbers[0]);
            }
            else if (kb.digit2Key.wasPressedThisFrame && carriedEmbers.Count >= 2)
            {
                SelectColor(carriedEmbers[1]);
            }
            else if (kb.digit3Key.wasPressedThisFrame && carriedEmbers.Count >= 3)
            {
                SelectColor(carriedEmbers[2]);
            }
        }

        public void AddEmber(EmberColor color)
        {
            if (color == EmberColor.None) return;
            if (!carriedEmbers.Contains(color))
            {
                carriedEmbers.Add(color);
            }
            SelectedColor = color;
            OnInventoryChanged?.Invoke();
        }

        public void SelectColor(EmberColor color)
        {
            if (color == EmberColor.None || !carriedEmbers.Contains(color)) return;
            SelectedColor = color;
            OnInventoryChanged?.Invoke();
        }

        public EmberColor CycleSelectedEmber()
        {
            if (carriedEmbers.Count == 0)
            {
                SelectedColor = EmberColor.None;
                return SelectedColor;
            }
            int idx = carriedEmbers.IndexOf(SelectedColor);
            idx = (idx + 1) % carriedEmbers.Count;
            SelectedColor = carriedEmbers[idx];
            OnInventoryChanged?.Invoke();
            return SelectedColor;
        }

        public bool HasEmber(EmberColor color) => carriedEmbers.Contains(color);

        public bool TryTakeForVein(EmberColor preferred, out EmberColor taken)
        {
            if (IsUsageBlocked || carriedEmbers.Count == 0)
            {
                taken = EmberColor.None;
                return false;
            }

            EmberColor candidate = preferred != EmberColor.None && carriedEmbers.Contains(preferred)
                ? preferred
                : (SelectedColor != EmberColor.None && carriedEmbers.Contains(SelectedColor) ? SelectedColor : carriedEmbers[0]);

            carriedEmbers.Remove(candidate);
            taken = candidate;
            SyncSelectedColor();
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void ReturnFromVein(EmberColor color)
        {
            AddEmber(color);
        }

        private void SyncSelectedColor()
        {
            if (carriedEmbers.Count == 0)
            {
                SelectedColor = EmberColor.None;
                return;
            }
            if (SelectedColor == EmberColor.None || !carriedEmbers.Contains(SelectedColor))
            {
                SelectedColor = carriedEmbers[0];
            }
        }
    }
}
