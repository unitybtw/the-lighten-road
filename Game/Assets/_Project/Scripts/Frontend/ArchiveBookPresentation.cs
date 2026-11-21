using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Pure 2D pixel-art Archive Book presentation: darkened gameplay vignette,
    /// warm flickering amber lantern halo, drifting pixel dust motes, frame-by-frame
    /// opening & page-turn sprites, and responsive layout fitting across all aspect ratios.
    /// </summary>
    public sealed class ArchiveBookPresentation : MonoBehaviour
    {
        public static readonly Vector2 StageReferenceSize = new Vector2(1280f, 864f);
        private static readonly Vector2 SafeFitBounds = new Vector2(1460f, 990f);

        private struct DustMote
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Phase;
            public float BaseAlpha;
            public bool IsBurst;
            public float Life;
            public float MaxLife;
        }

        public ArchiveBookPageTurn Turn { get; private set; }
        public RectTransform BookStage { get; private set; }
        public RectTransform BookmarkRail { get; private set; }
        public Image BookArtImage { get; private set; }
        public Image PageTurnOverlayImage { get; private set; }
        public Image BackdropImage { get; private set; }
        public Image LanternHaloImage { get; private set; }
        public Canvas BookCanvas { get; private set; }
        public Canvas FooterCanvas { get; private set; }

        public bool Visible { get; private set; }
        public int CurrentOpeningFrame { get; private set; }
        public int CurrentTurnFrame { get; private set; } = -1;
        public bool IsInitialized => BookStage != null && BookArtImage != null && PageTurnOverlayImage != null && Turn != null;

        private CanvasGroup inkGroup;
        private CanvasGroup bookmarkGroup;
        private RectTransform overlayRoot;
        private float openingProgress = 1f;
        private float ambientClock;
        private readonly List<Canvas> hiddenOverlays = new List<Canvas>();
        private readonly List<DustMote> ambientMotes = new List<DustMote>();
        private readonly List<DustMote> burstMotes = new List<DustMote>();

        public void Initialize(
            Canvas bookCanvas,
            Canvas footerCanvas,
            RectTransform overlayContainer,
            RectTransform inkContainer,
            CanvasGroup bookInkGroup,
            Func<bool> canTurn,
            Action<int> commit)
        {
            BookCanvas = bookCanvas;
            FooterCanvas = footerCanvas;
            overlayRoot = overlayContainer;
            inkGroup = bookInkGroup;

            // 1. Darkened gameplay vignette backdrop (keeps the paused game world subtly visible behind!)
            var backdropGo = CreateStretch("Darkened World Vignette", overlayRoot);
            backdropGo.SetAsFirstSibling();
            BackdropImage = backdropGo.gameObject.AddComponent<Image>();
            BackdropImage.sprite = PixelArchiveBook.VignetteBackdrop;
            BackdropImage.color = Color.white;
            BackdropImage.raycastTarget = true;

            // 2. Warm amber lantern glow halo behind the open book
            var haloRect = CreateCentered("Lantern Ambient Halo", overlayRoot, new Vector2(1580f, 1140f), new Vector2(0f, 34f));
            LanternHaloImage = haloRect.gameObject.AddComponent<Image>();
            LanternHaloImage.sprite = PixelArchiveBook.LanternGlow;
            LanternHaloImage.color = new Color(1f, 0.78f, 0.42f, 0.20f);
            LanternHaloImage.raycastTarget = false;

            // 3. Ambient drifting pixel dust motes layer
            var dustRoot = CreateStretch("Lantern Dust Motes", overlayRoot);
            BuildDustPool(dustRoot);

            // 4. Crisp 4x Pixel-Art Book Stage (320x216 art grid -> 1280x864 reference rect)
            BookStage = CreateCentered("Pixel Book Stage", overlayRoot, StageReferenceSize, new Vector2(0f, 36f));

            // 4a. Right-edge Bookmark Tabs Rail (tucked directly behind the right cover edge!)
            BookmarkRail = CreateCentered("Bookmark Tabs Rail", BookStage, new Vector2(160f, 660f), new Vector2(612f, 16f));
            bookmarkGroup = BookmarkRail.gameObject.AddComponent<CanvasGroup>();

            // 4b. Frame-by-Frame Pixel-Art Book Sprite
            var artRect = CreateCentered("Pixel Book Art", BookStage, StageReferenceSize, Vector2.zero);
            BookArtImage = artRect.gameObject.AddComponent<Image>();
            BookArtImage.sprite = PixelArchiveBook.OpenSpread;
            BookArtImage.preserveAspect = true;
            BookArtImage.raycastTarget = false;

            // 4c. Ink & Interactive Page Controls layered inside BookStage above BookArt
            inkContainer.SetParent(BookStage, false);
            inkContainer.anchorMin = inkContainer.anchorMax = inkContainer.pivot = new Vector2(0.5f, 0.5f);
            inkContainer.anchoredPosition = Vector2.zero;
            inkContainer.sizeDelta = StageReferenceSize;

            // 4d. Frame-by-Frame 2D Pixel-Art Page Turn Overlay (on top of ink during turn!)
            var turnRect = CreateCentered("Pixel Page Turn Overlay", BookStage, StageReferenceSize, Vector2.zero);
            PageTurnOverlayImage = turnRect.gameObject.AddComponent<Image>();
            PageTurnOverlayImage.sprite = PixelArchiveBook.PageTurnFrames[0];
            PageTurnOverlayImage.preserveAspect = true;
            PageTurnOverlayImage.raycastTarget = false;
            PageTurnOverlayImage.gameObject.SetActive(false);

            Turn = gameObject.AddComponent<ArchiveBookPageTurn>();
            Turn.Initialize(this, canTurn, commit);

            SetOpening(1f);
            FitStage();
            Show(false);
        }

        public void Rebind(Func<bool> canTurn, Action<int> commit)
        {
            if (Turn != null) Turn.Initialize(this, canTurn, commit);
        }

        public void SetOpening(float progress)
        {
            openingProgress = Mathf.Clamp01(progress);
            if (BookArtImage == null || BookStage == null) return;

            var frames = PixelArchiveBook.OpeningFrames;
            int idx = openingProgress >= 0.995f
                ? frames.Length - 1
                : Mathf.Clamp(Mathf.FloorToInt(openingProgress * frames.Length), 0, frames.Length - 1);
            CurrentOpeningFrame = idx;
            BookArtImage.sprite = frames[idx];

            // Center the closed book cover horizontally, then glide smoothly into the centered open spread.
            float slideEase = 1f - Mathf.Pow(1f - openingProgress, 3f);
            float xOffset = Mathf.Lerp(-260f, 0f, slideEase);
            float yRise = Mathf.Lerp(-16f, 36f, slideEase);
            BookStage.anchoredPosition = new Vector2(xOffset, yRise);

            // Fade in page ink & bookmark tabs cleanly only once the cover lands open.
            float contentReveal = openingProgress >= 0.995f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 0.98f, openingProgress));

            if (inkGroup != null) inkGroup.alpha = contentReveal;
            if (bookmarkGroup != null)
            {
                bookmarkGroup.alpha = contentReveal;
                bookmarkGroup.interactable = contentReveal >= 0.99f;
                bookmarkGroup.blocksRaycasts = contentReveal >= 0.99f;
            }
        }

        public void SetPageTurnFrame(int frameIndex, int direction, bool visible)
        {
            if (PageTurnOverlayImage == null) return;
            PageTurnOverlayImage.gameObject.SetActive(visible);
            if (!visible)
            {
                CurrentTurnFrame = -1;
                return;
            }

            var frames = PixelArchiveBook.PageTurnFrames;
            int clamped = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            CurrentTurnFrame = clamped;
            PageTurnOverlayImage.sprite = frames[clamped];
            // Mirror horizontally when turning backwards (left -> right)
            PageTurnOverlayImage.rectTransform.localScale = new Vector3(direction >= 0 ? 1f : -1f, 1f, 1f);
        }

        public void SpawnPageTurnDust(int direction)
        {
            if (MenuPreferences.ReducedMotion || burstMotes.Count == 0) return;

            for (int i = 0; i < burstMotes.Count; i++)
            {
                var mote = burstMotes[i];
                float spreadY = UnityEngine.Random.Range(-240f, 220f);
                float startX = (direction >= 0 ? 340f : -340f) + UnityEngine.Random.Range(-90f, 90f);
                mote.Position = new Vector2(startX, spreadY);
                mote.Velocity = new Vector2(
                    -direction * UnityEngine.Random.Range(65f, 160f),
                    UnityEngine.Random.Range(24f, 95f));
                mote.Life = 0f;
                mote.MaxLife = UnityEngine.Random.Range(0.36f, 0.62f);
                mote.BaseAlpha = UnityEngine.Random.Range(0.55f, 0.92f);
                mote.Rect.anchoredPosition = mote.Position;
                mote.Image.gameObject.SetActive(true);
                burstMotes[i] = mote;
            }
        }

        public void Show(bool value)
        {
            if (value && !Visible)
            {
                foreach (var overlay in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (overlay.enabled && overlay.isRootCanvas && overlay.renderMode == RenderMode.ScreenSpaceOverlay && !overlay.transform.IsChildOf(transform))
                    {
                        hiddenOverlays.Add(overlay);
                        overlay.enabled = false;
                    }
                }
            }
            if (!value)
            {
                foreach (var overlay in hiddenOverlays)
                {
                    if (overlay != null) overlay.enabled = true;
                }
                hiddenOverlays.Clear();
                if (Turn != null) Turn.Cancel();
            }

            Visible = value;
            if (value)
            {
                EnsureArtSpritesIntact();
                FitStage();
            }
        }

        private void EnsureArtSpritesIntact()
        {
            if (BookArtImage != null && (BookArtImage.sprite == null || BookArtImage.sprite.texture == null))
            {
                SetOpening(openingProgress);
            }
            if (BackdropImage != null && (BackdropImage.sprite == null || BackdropImage.sprite.texture == null))
            {
                BackdropImage.sprite = PixelArchiveBook.VignetteBackdrop;
            }
            if (LanternHaloImage != null && (LanternHaloImage.sprite == null || LanternHaloImage.sprite.texture == null))
            {
                LanternHaloImage.sprite = PixelArchiveBook.LanternGlow;
            }
            for (int i = 0; i < ambientMotes.Count; i++)
            {
                var img = ambientMotes[i].Image;
                if (img != null && (img.sprite == null || img.sprite.texture == null)) img.sprite = PixelArchiveBook.DustPixel;
            }
            for (int i = 0; i < burstMotes.Count; i++)
            {
                var img = burstMotes[i].Image;
                if (img != null && (img.sprite == null || img.sprite.texture == null)) img.sprite = PixelArchiveBook.DustPixel;
            }
        }

        private void LateUpdate()
        {
            if (!Visible) return;
            for (int i = hiddenOverlays.Count - 1; i >= 0; i--)
            {
                var overlay = hiddenOverlays[i];
                if (overlay != null && overlay.enabled) overlay.enabled = false;
            }

            EnsureArtSpritesIntact();
            FitStage();
            TickAtmosphere(Time.unscaledDeltaTime);
        }

        public void FitStage()
        {
            if (BookStage == null || BookCanvas == null) return;
            var rootRect = (RectTransform)BookCanvas.transform;
            Vector2 available = rootRect.rect.size;
            if (available.x <= 1f || available.y <= 1f) available = new Vector2(1920f, 1080f);

            // Reserve comfortable margin for footer controls & right-edge bookmark tabs
            float scale = MenuReadability.FitScale(available, SafeFitBounds);
            BookStage.localScale = Vector3.one * scale;
        }

        private void TickAtmosphere(float dt)
        {
            ambientClock += dt;
            bool reduced = MenuPreferences.ReducedMotion;

            if (LanternHaloImage != null)
            {
                float pulse = reduced ? 0.19f : (0.17f + 0.045f * Mathf.Sin(ambientClock * 2.1f) + 0.018f * Mathf.Sin(ambientClock * 5.3f));
                LanternHaloImage.color = new Color(1f, 0.78f, 0.42f, pulse);
            }

            for (int i = 0; i < ambientMotes.Count; i++)
            {
                var mote = ambientMotes[i];
                if (mote.Rect == null || mote.Image == null) continue;
                if (!reduced)
                {
                    mote.Position += mote.Velocity * dt;
                    mote.Position.x += Mathf.Sin(ambientClock * 0.9f + mote.Phase) * 8f * dt;
                    if (mote.Position.y > 540f) mote.Position.y = -520f;
                    if (mote.Position.x > 940f) mote.Position.x = -940f;
                    if (mote.Position.x < -940f) mote.Position.x = 940f;
                    mote.Rect.anchoredPosition = mote.Position;
                }

                float twinkle = reduced ? 0.65f : (0.45f + 0.55f * Mathf.Sin(ambientClock * 1.8f + mote.Phase));
                mote.Image.color = new Color(1f, 0.87f, 0.56f, mote.BaseAlpha * twinkle);
                ambientMotes[i] = mote;
            }

            for (int i = 0; i < burstMotes.Count; i++)
            {
                var mote = burstMotes[i];
                if (mote.Rect == null || mote.Image == null || !mote.Image.gameObject.activeSelf) continue;

                mote.Life += dt;
                if (mote.Life >= mote.MaxLife || reduced)
                {
                    mote.Image.gameObject.SetActive(false);
                }
                else
                {
                    float t = mote.Life / mote.MaxLife;
                    mote.Position += mote.Velocity * dt;
                    mote.Velocity *= Mathf.Exp(-3.2f * dt);
                    mote.Rect.anchoredPosition = mote.Position;
                    mote.Image.color = new Color(1f, 0.90f, 0.64f, mote.BaseAlpha * (1f - t));
                }
                burstMotes[i] = mote;
            }
        }

        private void BuildDustPool(RectTransform dustRoot)
        {
            // 16 subtle ambient lantern dust motes placed around the frame perimeter
            for (int i = 0; i < 16; i++)
            {
                bool leftSide = i % 2 == 0;
                float x = leftSide
                    ? Mathf.Lerp(-860f, -520f, ((i * 37) % 100) / 100f)
                    : Mathf.Lerp(520f, 860f, ((i * 53) % 100) / 100f);
                float y = Mathf.Lerp(-440f, 440f, ((i * 71) % 100) / 100f);
                float size = (i % 3 == 0) ? 10f : 7f;

                var rect = CreateCentered("Dust Mote " + i, dustRoot, new Vector2(size, size), new Vector2(x, y));
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = PixelArchiveBook.DustPixel;
                img.color = new Color(1f, 0.86f, 0.55f, 0.45f);
                img.raycastTarget = false;

                ambientMotes.Add(new DustMote
                {
                    Rect = rect,
                    Image = img,
                    Position = new Vector2(x, y),
                    Velocity = new Vector2(leftSide ? 4f : -4f, 14f + (i % 4) * 5f),
                    Phase = i * 0.95f,
                    BaseAlpha = 0.24f + (i % 3) * 0.14f,
                    IsBurst = false
                });
            }

            // 10 page-turn amber dust puff motes
            for (int i = 0; i < 10; i++)
            {
                float size = (i % 2 == 0) ? 11f : 8f;
                var rect = CreateCentered("Turn Dust " + i, dustRoot, new Vector2(size, size), Vector2.zero);
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = PixelArchiveBook.DustPixel;
                img.raycastTarget = false;
                img.gameObject.SetActive(false);

                burstMotes.Add(new DustMote
                {
                    Rect = rect,
                    Image = img,
                    IsBurst = true
                });
            }
        }

        /// <summary>
        /// Renders a crisp offline preview PNG of the 2D pixel-art book at any resolution for tests & art review.
        /// </summary>
        public void CapturePreview(int width, int height, string path)
        {
            if (BookCanvas == null) return;

            var oldBookMode = BookCanvas.renderMode;
            var oldBookCam = BookCanvas.worldCamera;
            var oldBookPlane = BookCanvas.planeDistance;
            var oldFooterMode = FooterCanvas != null ? FooterCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
            var oldFooterCam = FooterCanvas != null ? FooterCanvas.worldCamera : null;
            var oldFooterPlane = FooterCanvas != null ? FooterCanvas.planeDistance : 100f;

            var camGo = new GameObject("ArchiveBook2D Preview Camera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.055f, 0.068f, 1f);
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            Texture2D tex = null;

            try
            {
                cam.targetTexture = rt;
                cam.aspect = (float)width / Mathf.Max(1, height);

                BookCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                BookCanvas.worldCamera = cam;
                BookCanvas.planeDistance = 20f;
                if (FooterCanvas != null)
                {
                    FooterCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    FooterCanvas.worldCamera = cam;
                    FooterCanvas.planeDistance = 10f;
                }

                Canvas.ForceUpdateCanvases();
                FitStage();
                Canvas.ForceUpdateCanvases();

                RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                BookCanvas.renderMode = oldBookMode;
                BookCanvas.worldCamera = oldBookCam;
                BookCanvas.planeDistance = oldBookPlane;
                if (FooterCanvas != null)
                {
                    FooterCanvas.renderMode = oldFooterMode;
                    FooterCanvas.worldCamera = oldFooterCam;
                    FooterCanvas.planeDistance = oldFooterPlane;
                }
                Canvas.ForceUpdateCanvases();
                FitStage();
                RenderTexture.active = prevActive;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) DestroyImmediate(tex);
                DestroyImmediate(camGo);
            }
        }

        private static RectTransform CreateStretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        private static RectTransform CreateCentered(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        private void OnDisable() => Show(false);
    }
}
