using TheLightenRoad.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Drives interactive glass-card states, glowing ember bars, diamond markers,
    /// micro-typography shifts, and tactile audio feedback for Main Menu buttons.
    /// </summary>
    public sealed class MenuButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler
    {
        public RectTransform Caption;
        public TMP_Text Text;
        public TMP_Text SubtitleText;
        public TMP_Text IndexText;
        public Image CardBackground;
        public Image Rule;
        public Image EmberBar;
        public Image Marker;
        public bool IsPrimaryAction;
        public bool IsActiveTab;

        public Color RestTextColor = new Color32(178, 172, 156, 255);
        public Color ActiveTextColor = new Color32(248, 238, 214, 255);
        public Color PrimaryTextColor = new Color32(255, 224, 166, 255);

        public Color RestCardColor = new Color(0.06f, 0.075f, 0.09f, 0.62f);
        public Color HoverCardColor = new Color(0.16f, 0.11f, 0.06f, 0.86f);
        public Color PrimaryRestCardColor = new Color(0.18f, 0.11f, 0.05f, 0.78f);
        public Color PrimaryHoverCardColor = new Color(0.28f, 0.17f, 0.07f, 0.94f);

        private bool hovered;
        private bool selected;
        private bool pressed;
        private float blend;
        private float pressBlend;
        private float baseCaptionX = 38f;

        private void Start()
        {
            if (Caption != null)
            {
                baseCaptionX = Caption.anchoredPosition.x;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hovered)
            {
                hovered = true;
                PlayHoverSound();
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
                PlayHoverSound();
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

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayClickSound();
        }

        private void OnDisable()
        {
            hovered = false;
            selected = false;
            pressed = false;
            blend = IsActiveTab ? 1f : 0f;
            pressBlend = 0f;
            ApplyVisuals(0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float target = (hovered || selected || IsActiveTab) ? 1f : 0f;
            float pressTarget = pressed ? 1f : 0f;

            if (MenuPreferences.ReducedMotion)
            {
                blend = target;
                pressBlend = pressTarget;
            }
            else
            {
                blend = Mathf.MoveTowards(blend, target, dt * 7.5f);
                pressBlend = Mathf.MoveTowards(pressBlend, pressTarget, dt * 14f);
            }

            ApplyVisuals(Time.unscaledTime);
        }

        private void ApplyVisuals(float time)
        {
            float smooth = blend * blend * (3f - 2f * blend);
            float pulse = MenuPreferences.ReducedMotion ? 1f : (0.9f + 0.1f * Mathf.Sin(time * 4.2f));

            if (Caption != null)
            {
                float offset = 10f * smooth - 3f * pressBlend;
                Caption.anchoredPosition = new Vector2(baseCaptionX + offset, Caption.anchoredPosition.y);
            }

            if (Text != null)
            {
                Color baseCol = IsPrimaryAction ? PrimaryTextColor : RestTextColor;
                Text.color = Color.Lerp(baseCol, ActiveTextColor, smooth);
            }

            if (SubtitleText != null)
            {
                SubtitleText.color = Color.Lerp(
                    new Color(0.58f, 0.56f, 0.50f, 0.75f),
                    new Color(0.92f, 0.78f, 0.55f, 0.95f),
                    smooth);
            }

            if (IndexText != null)
            {
                IndexText.color = Color.Lerp(
                    new Color(0.68f, 0.52f, 0.28f, 0.55f),
                    new Color(1.0f, 0.76f, 0.36f, 1.0f),
                    smooth);
            }

            if (CardBackground != null)
            {
                Color cRest = IsPrimaryAction ? PrimaryRestCardColor : RestCardColor;
                Color cHover = IsPrimaryAction ? PrimaryHoverCardColor : HoverCardColor;
                CardBackground.color = Color.Lerp(cRest, cHover, smooth);
            }

            if (EmberBar != null)
            {
                float height = Mathf.Lerp(IsPrimaryAction ? 26f : 12f, 52f, smooth);
                EmberBar.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(3f, 4.5f, smooth), height);
                EmberBar.color = Color.Lerp(
                    new Color(0.75f, 0.48f, 0.18f, IsPrimaryAction ? 0.75f : 0.28f),
                    new Color(1.0f, 0.68f, 0.24f, 1.0f * pulse),
                    smooth);
            }

            if (Rule != null)
            {
                Rule.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 360f, smooth), 1.5f);
                Rule.color = new Color(0.95f, 0.65f, 0.25f, smooth * 0.85f);
            }

            if (Marker != null)
            {
                Marker.color = Color.Lerp(
                    new Color(0.78f, 0.55f, 0.26f, 0.45f),
                    new Color(1.0f, 0.76f, 0.32f, 1.0f),
                    smooth);
                float angle = MenuPreferences.ReducedMotion ? 45f : (45f + 90f * smooth);
                Marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                float scale = 1f + 0.28f * smooth - 0.12f * pressBlend;
                Marker.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void PlayHoverSound()
        {
            if (!Application.isPlaying) return;
            var sfx = SoundEffectManager.Instance;
            if (sfx != null && sfx.WalkClips != null && sfx.WalkClips.Length > 0 && sfx.WalkClips[0] != null)
            {
                sfx.PlayOneShot(sfx.WalkClips[0], 0.22f, 1.35f);
            }
        }

        private void PlayClickSound()
        {
            if (!Application.isPlaying) return;
            var sfx = SoundEffectManager.Instance;
            if (sfx == null) return;

            if (IsPrimaryAction)
            {
                sfx.PlayFlareIgnite();
            }
            else
            {
                sfx.PlayEmberPickup();
            }
        }
    }
}
