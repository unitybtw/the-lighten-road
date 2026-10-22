using System;
using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Interactive card motion and micro-animation controller for sub-view cards (e.g. Realm selection, Credits blocks).
    /// Provides smooth hover/focus scaling, ambient aura breathing, and tactile feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractiveCardMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public Image CardBackground;
        public Image CardAura;
        public TMP_Text TitleText;
        public TMP_Text StatusBadgeText;
        public Image UnderlineFiligree;
        public bool IsSelected;
        public Action<InteractiveCardMotion> OnFocusedCallback;

        private RectTransform rectTransform;
        private bool hovered;
        private bool selected;
        private bool pressed;
        private float focusBlend;
        private float pressBlend;

        private static readonly Color IdleTitleColor = new Color32(220, 228, 242, 235);
        private static readonly Color ActiveTitleColor = new Color32(255, 230, 160, 255);

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            hovered = false;
            selected = false;
            pressed = false;
            focusBlend = 0f;
            pressBlend = 0f;
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
        }

        private void Update()
        {
            bool active = hovered || selected || IsSelected;
            float dt = Time.unscaledDeltaTime;
            float speed = MenuPreferences.ReducedMotion ? 25f : 12f;

            focusBlend = Mathf.MoveTowards(focusBlend, active ? 1f : 0f, dt * speed);
            pressBlend = Mathf.MoveTowards(pressBlend, pressed ? 1f : 0f, dt * 20f);

            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            float smooth = SmoothStep(focusBlend);
            float t = Time.unscaledTime;
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.92f + 0.08f * Mathf.Sin(t * 3.6f));

            // Smooth scale punch on hover / focus
            if (rectTransform != null)
            {
                float targetScale = MenuPreferences.ReducedMotion
                    ? 1f
                    : (Mathf.Lerp(1f, 1.020f, smooth) - pressBlend * 0.015f);
                rectTransform.localScale = new Vector3(targetScale, targetScale, 1f);
            }

            // Title color transition
            if (TitleText != null)
            {
                TitleText.color = Color.Lerp(IdleTitleColor, ActiveTitleColor, IsSelected ? 1f : smooth * 0.75f);
            }

            // Aura breathing & intensity
            if (CardAura != null)
            {
                float baseAlpha = IsSelected ? 0.32f : (smooth * 0.18f);
                Color c = IsSelected
                    ? new Color(1.0f, 0.72f, 0.25f, baseAlpha * pulse)
                    : new Color(0.65f, 0.75f, 0.95f, baseAlpha * pulse);
                CardAura.color = c;
            }

            // Filigree line width / brightness
            if (UnderlineFiligree != null)
            {
                float targetW = Mathf.Lerp(IsSelected ? 520f : 240f, IsSelected ? 620f : 420f, smooth);
                UnderlineFiligree.rectTransform.sizeDelta = new Vector2(targetW, 4f);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hovered)
            {
                hovered = true;
                NotifyFocus();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!selected)
            {
                selected = true;
                NotifyFocus();
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        private void NotifyFocus()
        {
            OnFocusedCallback?.Invoke(this);
            if (Application.isPlaying && Time.timeSinceLevelLoad > 0.35f && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayWalkFootstep();
            }
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
