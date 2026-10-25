using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Shared cinematic lantern-lit focus for settings rows and controls.
    /// Handles both mouse hover and controller gamepad navigation selection,
    /// providing clear visual feedback via card background, glowing border and audio chime.
    /// </summary>
    public sealed class SettingsLightFocus : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        public Image Glow;
        public Image CardPlate;
        public Image BorderFrame;
        public TMP_Text Label;
        public bool IsCurrent;

        private bool hovered;
        private bool isSelected;
        private float blend;
        private Selectable control;

        private void Awake()
        {
            control = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (!hovered)
            {
                hovered = true;
                PlayHoverAudio();
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            hovered = false;
        }

        public void OnSelect(BaseEventData data)
        {
            if (!isSelected)
            {
                isSelected = true;
                PlayHoverAudio();
            }
        }

        public void OnDeselect(BaseEventData data)
        {
            isSelected = false;
        }

        private void OnDisable()
        {
            hovered = false;
            isSelected = false;
            blend = 0f;
        }

        private void PlayHoverAudio()
        {
            if (Application.isPlaying && SoundEffectManager.HasInstance && Time.timeSinceLevelLoad > 0.35f)
            {
                SoundEffectManager.Instance.PlayUIHover(0.7f);
            }
        }

        private void Update()
        {
            var curSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool childSelected = curSelected != null && (curSelected == gameObject || curSelected.transform.IsChildOf(transform));
            bool enabledState = control == null || control.IsInteractable();
            bool focus = (hovered || isSelected || childSelected) && enabledState;

            blend = Mathf.MoveTowards(blend, focus ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.2f));

            if (Glow != null)
            {
                Glow.color = new Color(1.0f, 0.72f, 0.28f, blend * 0.32f * pulse);
                float glowScale = Mathf.Lerp(0.92f, 1.05f, blend);
                Glow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            if (CardPlate != null)
            {
                Color idlePlate = new Color(0.012f, 0.020f, 0.038f, 0.65f);
                Color focusPlate = new Color(0.085f, 0.052f, 0.022f, 0.92f);
                CardPlate.color = Color.Lerp(idlePlate, focusPlate, blend);
            }

            if (BorderFrame != null)
            {
                Color idleBorder = new Color(0.48f, 0.62f, 0.82f, 0.25f);
                Color focusBorder = new Color(1.0f, 0.78f, 0.32f, 0.95f);
                BorderFrame.color = Color.Lerp(idleBorder, focusBorder, blend);
            }

            if (Label != null)
            {
                Color disabledCol = new Color(0.55f, 0.58f, 0.65f, 0.6f);
                Color idleCol = IsCurrent ? new Color(1.0f, 0.78f, 0.32f, 0.95f) : new Color(0.85f, 0.90f, 0.98f, 1f);
                Color focusCol = new Color(1.0f, 0.88f, 0.55f, 1f);
                Label.color = !enabledState ? disabledCol : Color.Lerp(idleCol, focusCol, blend);
            }
        }
    }
}
