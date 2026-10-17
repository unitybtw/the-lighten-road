using System;
using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Frameless Hollow-Knight / Ori style menu item motion controller.
    /// Animates symmetrical glowing lantern-wing flourishes, soft radial ember aura,
    /// letter-spacing expansion, and triggers live stage lantern surges & energy streams on focus.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuButtonMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public TMP_Text Text;
        public TMP_Text SubtitleText;
        public Image SoftGlow;
        public RectTransform LeftWing;
        public RectTransform RightWing;
        public CanvasGroup WingGroup;
        public Image UnderlineFiligree;
        public bool IsPrimaryAction;
        public float BaseWingOffset = 145f;
        public Vector2 StageFocusCanvasPosition;
        public Vector2 StageCameraPanOffset;
        public Action<MenuButtonMotion> OnFocusedCallback;

        private bool hovered;
        private bool selected;
        private bool pressed;
        private float focusBlend;
        private float pressBlend;

        private static readonly Color IdleIvory = new Color32(226, 220, 206, 225);
        private static readonly Color IdlePrimaryGold = new Color32(255, 206, 120, 255);
        private static readonly Color FocusedRadiantIvory = new Color32(255, 248, 232, 255);
        private static readonly Color FocusedAmberGold = new Color32(255, 186, 72, 255);
        private static readonly Color IdleSubtle = new Color32(142, 152, 174, 175);
        private static readonly Color FocusedSubtle = new Color32(236, 204, 148, 240);

        private void OnEnable()
        {
            hovered = false;
            selected = false;
            pressed = false;
            focusBlend = 0f;
            pressBlend = 0f;
            ApplyVisuals(instant: true);
        }

        private void Update()
        {
            bool active = hovered || selected;
            float dt = Time.unscaledDeltaTime;
            float speed = MenuPreferences.ReducedMotion ? 25f : 11f;

            focusBlend = Mathf.MoveTowards(focusBlend, active ? 1f : 0f, dt * speed);
            pressBlend = Mathf.MoveTowards(pressBlend, pressed ? 1f : 0f, dt * 18f);

            ApplyVisuals(instant: false);
        }

        private void ApplyVisuals(bool instant)
        {
            float smooth = SmoothStep(focusBlend);
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.90f + 0.10f * Mathf.Sin(Time.unscaledTime * 4.2f));

            // 1. Typography scale, glow color & letter spacing expansion
            if (Text != null)
            {
                Color baseCol = IsPrimaryAction ? IdlePrimaryGold : IdleIvory;
                Color targetCol = IsPrimaryAction ? FocusedAmberGold : FocusedRadiantIvory;
                Text.color = Color.Lerp(baseCol, targetCol, smooth);
                Text.characterSpacing = Mathf.Lerp(IsPrimaryAction ? 6.5f : 5.0f, IsPrimaryAction ? 10.5f : 8.5f, smooth);

                float scale = Mathf.Lerp(1f, 1.06f, smooth) - pressBlend * 0.03f;
                Text.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (SubtitleText != null)
            {
                SubtitleText.color = Color.Lerp(IdleSubtle, FocusedSubtle, smooth);
            }

            // 2. Frameless Soft Radial Lantern Aura behind text
            if (SoftGlow != null)
            {
                float baseAlpha = IsPrimaryAction ? 0.14f : 0f;
                float targetAlpha = (IsPrimaryAction ? 0.38f : 0.28f) * pulse;
                SoftGlow.color = new Color(1.0f, 0.66f, 0.20f, Mathf.Lerp(baseAlpha, targetAlpha, smooth));
                float glowScale = Mathf.Lerp(0.82f, 1.12f, smooth);
                SoftGlow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            // 3. Symmetrical Left & Right Ornate Lantern Wings (◄── ◆   TEXT   ◆ ──►)
            float wingSlide = MenuPreferences.ReducedMotion ? BaseWingOffset : Mathf.Lerp(BaseWingOffset - 26f, BaseWingOffset + 12f, smooth);
            if (LeftWing != null)
            {
                LeftWing.anchoredPosition = new Vector2(-wingSlide, 0f);
            }
            if (RightWing != null)
            {
                RightWing.anchoredPosition = new Vector2(wingSlide, 0f);
            }
            if (WingGroup != null)
            {
                WingGroup.alpha = Mathf.Lerp(IsPrimaryAction ? 0.35f : 0f, 1f, smooth);
            }

            // 4. Delicate Center-Out Filigree Underline
            if (UnderlineFiligree != null)
            {
                var r = UnderlineFiligree.rectTransform;
                float width = Mathf.Lerp(IsPrimaryAction ? 120f : 0f, 280f, smooth);
                r.sizeDelta = new Vector2(width, 2f);
                UnderlineFiligree.color = new Color(1.0f, 0.74f, 0.30f, Mathf.Lerp(IsPrimaryAction ? 0.35f : 0f, 0.85f, smooth));
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
            if (Application.isPlaying)
            {
                SoundEffectManager.Instance?.PlayEmberPickup();
            }
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
