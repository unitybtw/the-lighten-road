using System;
using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Tracks carried Embers (Orange/Cyan/Crimson). Embers are NEVER consumed;
    /// they are inserted into and recovered from AdvancedEmberVein sockets.
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
            if (!GameControls.GameplayAllowed) return;
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                CycleSelectedEmber();
            }
        }

        public void AddEmber(EmberColor color)
        {
            if (color == EmberColor.None) return;
            carriedEmbers.Add(color);
            if (SelectedColor == EmberColor.None) SelectedColor = color;
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
            if (color == EmberColor.None) return;
            carriedEmbers.Add(color);
            if (SelectedColor == EmberColor.None) SelectedColor = color;
            OnInventoryChanged?.Invoke();
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
