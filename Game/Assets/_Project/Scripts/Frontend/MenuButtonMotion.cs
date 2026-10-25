using System;
using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Frameless menu item motion controller.
    /// Supports both left-aligned vertical menu items with an animated pixel-art Flame Cursor
    /// and lantern glow, as well as centered sub-view action buttons.
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
        public RectTransform FlameCursorRect;
        public Image FlameCursorImage;
        public Image FlameCursorHalo;
        public RectTransform LeftWing;
        public RectTransform RightWing;
        public CanvasGroup WingGroup;
        public Image UnderlineFiligree;
        public Image BorderFrame;
        public Image CardPlate;
        public bool IsPrimaryAction;
        public bool LeftAlignedItem;
        public float BaseWingOffset = 145f;
        public Vector2 StageFocusCanvasPosition;
        public Vector2 StageCameraPanOffset;
        public Action<MenuButtonMotion> OnFocusedCallback;

        private bool hovered;
        private bool selected;
        private bool pressed;
        private float focusBlend;
        private float pressBlend;
        private Vector2 baseTextAnchoredPos;
        private Vector2 baseSubAnchoredPos;
        private Vector2 baseFlameAnchoredPos;
        private bool capturedBasePositions;

        private static readonly Color IdleIvory = new Color32(214, 224, 240, 225);
        private static readonly Color IdlePrimaryGold = new Color32(244, 248, 255, 255);
        private static readonly Color FocusedRadiantIvory = new Color32(255, 244, 214, 255);
        private static readonly Color FocusedAmberGold = new Color32(255, 196, 88, 255);
        private static readonly Color IdleSubtle = new Color32(148, 166, 194, 190);
        private static readonly Color FocusedSubtle = new Color32(255, 216, 148, 240);

        public void InitializeBasePositions(Vector2 textPos, Vector2 subPos, Vector2 flamePos)
        {
            baseTextAnchoredPos = textPos;
            baseSubAnchoredPos = subPos;
            baseFlameAnchoredPos = flamePos;
            capturedBasePositions = true;
        }

        private void CaptureBasePositionsIfNeeded()
        {
            if (capturedBasePositions) return;
            capturedBasePositions = true;
            if (Text != null) baseTextAnchoredPos = Text.rectTransform.anchoredPosition;
            if (SubtitleText != null) baseSubAnchoredPos = SubtitleText.rectTransform.anchoredPosition;
            if (FlameCursorRect != null) baseFlameAnchoredPos = FlameCursorRect.anchoredPosition;
        }

        private void OnEnable()
        {
            CaptureBasePositionsIfNeeded();
            hovered = false;
            selected = false;
            pressed = false;
            focusBlend = 0f;
            pressBlend = 0f;
            ApplyVisuals(instant: true);
        }

        private void Update()
        {
            CaptureBasePositionsIfNeeded();
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
            float t = Time.unscaledTime;
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.90f + 0.10f * Mathf.Sin(t * 4.5f));

            // 1. Typography scale, horizontal slide, lantern glow color & letter spacing
            if (Text != null)
            {
                Color baseCol = IsPrimaryAction ? IdlePrimaryGold : IdleIvory;
                Color targetCol = IsPrimaryAction ? FocusedAmberGold : FocusedRadiantIvory;
                Text.color = Color.Lerp(baseCol, targetCol, smooth);
                Text.characterSpacing = 1.4f;

                float scale = Mathf.Lerp(1f, 1.012f, smooth) - pressBlend * 0.025f;
                Text.rectTransform.localScale = new Vector3(scale, scale, 1f);

                if (LeftAlignedItem)
                {
                    float slideX = MenuPreferences.ReducedMotion ? 0f : Mathf.Lerp(0f, 12f, smooth);
                    Text.rectTransform.anchoredPosition = baseTextAnchoredPos + new Vector2(slideX, 0f);
                }
            }

            if (!LeftAlignedItem && transform is RectTransform selfRect)
            {
                float btnScale = MenuPreferences.ReducedMotion ? 1f : (Mathf.Lerp(1f, 1.008f, smooth) - pressBlend * 0.025f);
                selfRect.localScale = new Vector3(btnScale, btnScale, 1f);
            }

            if (SubtitleText != null)
            {
                SubtitleText.color = Color.Lerp(IdleSubtle, FocusedSubtle, smooth);
                if (LeftAlignedItem)
                {
                    float slideX = MenuPreferences.ReducedMotion ? 0f : Mathf.Lerp(0f, 12f, smooth);
                    SubtitleText.rectTransform.anchoredPosition = baseSubAnchoredPos + new Vector2(slideX, 0f);
                }
            }

            // 2. Animated Pixel-Art Flame Cursor on Left of Active Item
            if (FlameCursorRect != null)
            {
                float cursorVis = smooth;
                float bobY = MenuPreferences.ReducedMotion ? 0f : Mathf.Sin(t * 5.2f) * 3.0f * smooth;
                float flickerY = MenuPreferences.ReducedMotion ? 1f : (0.92f + 0.14f * Mathf.Sin(t * 9.5f));
                float flickerX = MenuPreferences.ReducedMotion ? 1f : (0.94f + 0.10f * Mathf.Cos(t * 7.1f));

                FlameCursorRect.anchoredPosition = baseFlameAnchoredPos + new Vector2(Mathf.Lerp(-6f, 2f, smooth), bobY);
                float baseScale = Mathf.Lerp(0.35f, 1.12f, smooth);
                FlameCursorRect.localScale = new Vector3(baseScale * flickerX, baseScale * flickerY, 1f);

                if (FlameCursorImage != null)
                {
                    Color flameCol = Color.Lerp(new Color(1.0f, 0.72f, 0.28f, cursorVis), new Color(1.0f, 0.94f, 0.74f, cursorVis), smooth);
                    FlameCursorImage.color = flameCol;
                }
                if (FlameCursorHalo != null)
                {
                    FlameCursorHalo.color = new Color(1.0f, 0.66f, 0.18f, cursorVis * 0.55f * pulse);
                }
            }

            // 3. Delicate Warm Candlelight Radiance behind text and card (only when focused/hovered)
            if (SoftGlow != null)
            {
                float targetAlpha = IsPrimaryAction ? 0.32f : 0.24f;
                SoftGlow.color = new Color(1.0f, 0.76f, 0.30f, Mathf.Lerp(0f, targetAlpha, smooth));
                float glowScale = Mathf.Lerp(0.90f, 1.06f, smooth);
                SoftGlow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            // 4. Symmetrical Wings (Hollow Knight / Lighten Road inward-gliding selection pointers)
            float wingSlide = Mathf.Lerp(BaseWingOffset + 18f, BaseWingOffset, smooth);
            if (LeftWing != null) LeftWing.anchoredPosition = new Vector2(-wingSlide, 0f);
            if (RightWing != null) RightWing.anchoredPosition = new Vector2(wingSlide, 0f);
            if (WingGroup != null) WingGroup.alpha = smooth;

            // 5. Center-Expanding Ember-Vein Filigree Underline (suppressed to prevent flickering lines)
            if (UnderlineFiligree != null)
            {
                UnderlineFiligree.color = Color.clear;
            }

            // Keep the hit surface transparent; focus is carried by text, glow and ornaments.
            if (CardPlate != null)
            {
                CardPlate.color = Color.clear;
            }

            if (BorderFrame != null)
            {
                BorderFrame.color = Color.clear;
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
                SoundEffectManager.Instance?.PlayUIClick();
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
                SoundEffectManager.Instance.PlayUIHover();
            }
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
