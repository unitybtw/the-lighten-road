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
    /// providing clear visual feedback via a soft lantern glow, selection ornaments and audio chime.
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
        public RectTransform LeftPointer;
        public RectTransform RightPointer;
        public float PointerOffset = 370f;
        public bool IsCurrent;
        public bool UseInkPalette;

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

            if (Glow != null)
            {
                Glow.color = new Color(1.0f, 0.72f, 0.28f, blend * 0.07f);
                float glowScale = Mathf.Lerp(0.92f, 1.05f, blend);
                Glow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            if (CardPlate != null)
            {
                CardPlate.color = Color.clear;
            }

            if (BorderFrame != null)
            {
                BorderFrame.color = Color.clear;
            }

            if (Label != null)
            {
                Color disabledCol = new Color(0.55f, 0.58f, 0.65f, 0.6f);
                Color idleCol = IsCurrent ? new Color(1.0f, 0.78f, 0.32f, 0.95f) : new Color(0.85f, 0.90f, 0.98f, 0.85f);
                Color focusCol = new Color(1.0f, 0.88f, 0.55f, 1f);
                if (UseInkPalette)
                {
                    disabledCol = new Color(.35f, .29f, .22f, .65f);
                    idleCol = new Color(.24f, .14f, .065f, 1f);
                    focusCol = new Color(.43f, .17f, .04f, 1f);
                }
                Label.color = !enabledState ? disabledCol : Color.Lerp(idleCol, focusCol, blend);
            }

            if (LeftPointer != null)
            {
                float slide = Mathf.Lerp(PointerOffset + 18f, PointerOffset, blend);
                LeftPointer.anchoredPosition = new Vector2(-slide, 0f);
                var lImg = LeftPointer.GetComponent<Image>();
                if (lImg != null) lImg.color = UseInkPalette ? new Color(.43f, .17f, .04f, blend) : new Color(1.0f, 0.94f, 0.85f, blend);
            }

            if (RightPointer != null)
            {
                float slide = Mathf.Lerp(PointerOffset + 18f, PointerOffset, blend);
                RightPointer.anchoredPosition = new Vector2(slide, 0f);
                var rImg = RightPointer.GetComponent<Image>();
                if (rImg != null) rImg.color = new Color(1.0f, 0.94f, 0.85f, blend);
            }
        }
    }
}
