using System;
using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Minimalist AAA menu item & interactive card motion controller.
    /// Drives symmetrical filigree wing glide, rotating diamond tips, radial lantern bloom,
    /// letter-spacing breathing, and stage surge callbacks.
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
        public Image SoftGlow;
        public Image CardBorder;
        public RectTransform LeftWing;
        public RectTransform RightWing;
        public RectTransform LeftDiamond;
        public RectTransform RightDiamond;
        public CanvasGroup WingGroup;
        public Image UnderlineFiligree;
        public bool IsPrimaryAction;
        public float BaseWingOffset = 135f;
        public float BaseUnderlineWidth = 0f;
        public float FocusedUnderlineWidth = 240f;
        public string ContextHint;
        public Vector2 StageFocusCanvasPosition;
        public Vector2 StageCameraPanOffset;
        public Action<MenuButtonMotion> OnFocusedCallback;

        private bool hovered;
        private bool selected;
        private bool pressed;
        private float focusBlend;
        private float pressBlend;
        private float spinAngle;

        private static readonly Color IdleIvory = new Color32(222, 228, 240, 215);
        private static readonly Color IdlePrimaryGold = new Color32(255, 204, 112, 255);
        private static readonly Color FocusedRadiantIvory = new Color32(255, 250, 238, 255);
        private static readonly Color FocusedAmberGold = new Color32(255, 190, 76, 255);

        private void OnEnable()
        {
            hovered = false;
            selected = false;
            pressed = false;
            focusBlend = 0f;
            pressBlend = 0f;
            ApplyVisuals();
        }

        private void Update()
        {
            bool active = hovered || selected;
            float dt = Time.unscaledDeltaTime;
            float speed = MenuPreferences.ReducedMotion ? 25f : 11f;

            focusBlend = Mathf.MoveTowards(focusBlend, active ? 1f : 0f, dt * speed);
            pressBlend = Mathf.MoveTowards(pressBlend, pressed ? 1f : 0f, dt * 18f);

            if (active && !MenuPreferences.ReducedMotion)
            {
                spinAngle += dt * 110f;
            }

            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            float smooth = SmoothStep(focusBlend);
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 4.5f));

            if (Text != null)
            {
                Color baseCol = IsPrimaryAction ? IdlePrimaryGold : IdleIvory;
                Color targetCol = IsPrimaryAction ? FocusedAmberGold : FocusedRadiantIvory;
                Text.color = Color.Lerp(baseCol, targetCol, smooth);
                Text.characterSpacing = Mathf.Lerp(IsPrimaryAction ? 8f : 6.5f, IsPrimaryAction ? 13f : 10.5f, smooth);

                float scale = Mathf.Lerp(1f, 1.07f, smooth) - pressBlend * 0.035f;
                Text.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (SoftGlow != null)
            {
                float baseAlpha = IsPrimaryAction ? 0.12f : 0f;
                float targetAlpha = (IsPrimaryAction ? 0.38f : 0.28f) * pulse;
                SoftGlow.color = new Color(1.0f, 0.66f, 0.20f, Mathf.Lerp(baseAlpha, targetAlpha, smooth));
                float glowScale = Mathf.Lerp(0.84f, 1.14f, smooth);
                SoftGlow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
            }

            if (CardBorder != null)
            {
                CardBorder.color = Color.Lerp(
                    new Color(0.55f, 0.68f, 0.92f, 0.16f),
                    new Color(1.0f, 0.74f, 0.28f, 0.75f),
                    smooth);
            }

            float wingSlide = MenuPreferences.ReducedMotion
                ? BaseWingOffset
                : Mathf.Lerp(BaseWingOffset - 24f, BaseWingOffset + 14f, smooth);

            if (LeftWing != null) LeftWing.anchoredPosition = new Vector2(-wingSlide, 0f);
            if (RightWing != null) RightWing.anchoredPosition = new Vector2(wingSlide, 0f);

            if (LeftDiamond != null) LeftDiamond.localEulerAngles = new Vector3(0f, 0f, 45f + spinAngle);
            if (RightDiamond != null) RightDiamond.localEulerAngles = new Vector3(0f, 0f, 45f - spinAngle);

            if (WingGroup != null)
            {
                WingGroup.alpha = Mathf.Lerp(IsPrimaryAction ? 0.35f : 0f, 1f, smooth);
            }

            if (UnderlineFiligree != null)
            {
                var r = UnderlineFiligree.rectTransform;
                float width = Mathf.Lerp(IsPrimaryAction ? Mathf.Max(90f, BaseUnderlineWidth) : BaseUnderlineWidth, FocusedUnderlineWidth, smooth);
                r.sizeDelta = new Vector2(width, r.sizeDelta.y);
                UnderlineFiligree.color = new Color(1.0f, 0.74f, 0.30f, Mathf.Lerp(IsPrimaryAction ? 0.35f : 0f, 0.88f, smooth));
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
            if (Application.isPlaying && SoundEffectManager.HasInstance)
            {
                SoundEffectManager.Instance.PlayEmberPickup();
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
