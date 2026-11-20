using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Animates ornate UI filigree dividers with a symmetrical center-out wipe
    /// and a gentle idle golden-ivory shimmer, respecting MenuPreferences.ReducedMotion.
    /// </summary>
    public sealed class FiligreeReveal : MonoBehaviour
    {
        private RectTransform maskRect;
        private Image filigreeImage;
        private Vector2 fullSize;
        private Color baseColor = Color.white;
        private Color shimmerTint = new Color(1f, 0.96f, 0.84f, 1f);

        private bool manualReveal;
        private bool revealing;
        private float revealDuration = 0.42f;
        private float revealElapsed;
        private float revealDelay;
        private float progress = 1f;
        private float shimmerPhase;

        public RectTransform RectTransform => maskRect;
        public Image Image => filigreeImage;

        /// <summary>
        /// Creates a masked filigree container whose child Image stays at natural aspect
        /// while the centered RectMask2D reveals it symmetrically from the center outward.
        /// </summary>
        public static FiligreeReveal Create(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 size,
            Vector2 anchoredPosition,
            Color tint,
            bool manualReveal = false,
            float duration = 0.42f,
            float phaseOffset = 0f)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(RectMask2D), typeof(FiligreeReveal));
            var maskRect = host.GetComponent<RectTransform>();
            maskRect.SetParent(parent, false);
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = anchoredPosition;
            maskRect.sizeDelta = size;

            var artGo = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.SetParent(maskRect, false);
            artRect.anchorMin = new Vector2(0.5f, 0.5f);
            artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;
            artRect.sizeDelta = size;

            var img = artGo.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var reveal = host.GetComponent<FiligreeReveal>();
            reveal.Initialize(maskRect, img, size, tint, manualReveal, duration, phaseOffset);
            return reveal;
        }

        public void Initialize(
            RectTransform mask,
            Image image,
            Vector2 targetSize,
            Color tint,
            bool isManualReveal = false,
            float duration = 0.42f,
            float phaseOffset = 0f)
        {
            maskRect = mask;
            filigreeImage = image;
            fullSize = targetSize;
            baseColor = tint;
            manualReveal = isManualReveal;
            revealDuration = Mathf.Max(0.08f, duration);
            shimmerPhase = phaseOffset;

            if (MenuPreferences.ReducedMotion)
            {
                progress = 1f;
                revealing = false;
            }
            else if (manualReveal)
            {
                progress = 0f;
                revealing = false;
            }
            else
            {
                progress = 1f;
                revealing = false;
            }

            ApplyVisuals(Time.unscaledTime);
        }

        public void SetReveal(float value)
        {
            progress = MenuPreferences.ReducedMotion ? 1f : Mathf.Clamp01(value);
            revealing = false;
            ApplyVisuals(Time.unscaledTime);
        }

        public void PlayReveal(float delay = 0f)
        {
            if (MenuPreferences.ReducedMotion)
            {
                progress = 1f;
                revealing = false;
                ApplyVisuals(Time.unscaledTime);
                return;
            }

            revealDelay = Mathf.Max(0f, delay);
            revealElapsed = 0f;
            progress = 0f;
            revealing = true;
            ApplyVisuals(Time.unscaledTime);
        }

        private void OnEnable()
        {
            ApplyVisuals(Time.unscaledTime);
        }

        private void Update()
        {
            if (MenuPreferences.ReducedMotion)
            {
                if (progress < 1f || revealing)
                {
                    progress = 1f;
                    revealing = false;
                }
                ApplyVisuals(Time.unscaledTime);
                return;
            }

            if (revealing)
            {
                if (revealDelay > 0f)
                {
                    revealDelay -= Time.unscaledDeltaTime;
                }
                else
                {
                    revealElapsed += Time.unscaledDeltaTime;
                    progress = Mathf.Clamp01(revealElapsed / revealDuration);
                    if (progress >= 1f)
                    {
                        revealing = false;
                    }
                }
            }

            ApplyVisuals(Time.unscaledTime);
        }

        private void ApplyVisuals(float now)
        {
            if (maskRect == null || filigreeImage == null)
            {
                return;
            }

            float p = MenuPreferences.ReducedMotion ? 1f : Mathf.Clamp01(progress);
            // Smooth ease-out cubic so the line work draws briskly from the center lantern outward and settles cleanly.
            float inv = 1f - p;
            float eased = 1f - inv * inv * inv;

            float width = fullSize.x * eased;
            maskRect.sizeDelta = new Vector2(width, fullSize.y + 8f);

            if (MenuPreferences.ReducedMotion)
            {
                filigreeImage.color = baseColor;
                return;
            }

            // Subtle draw-in spark near completion + slow idle breath afterward.
            float arrivalFlash = Mathf.Sin(p * Mathf.PI) * 0.16f;
            float shimmerWave = 0.5f + 0.5f * Mathf.Sin(now * 1.65f + shimmerPhase);
            float shimmerAmount = Mathf.Lerp(0f, 0.22f, shimmerWave) * eased + arrivalFlash;

            Color c = Color.Lerp(baseColor, shimmerTint, Mathf.Clamp01(shimmerAmount));
            float alphaFactor = Mathf.Lerp(0.75f, 1f, eased);
            c.a = baseColor.a * (0.90f + 0.10f * shimmerWave) * alphaFactor;
            filigreeImage.color = c;
        }
    }
}
