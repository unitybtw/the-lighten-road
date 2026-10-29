using TMPro;
using UnityEngine;

namespace TheLightenRoad.UI
{
    /// <summary>Shared archive typography, palette and canvas fit for frontend and gameplay overlays.</summary>
    public static class ArchiveInterfaceStyle
    {
        private static TMP_FontAsset displayFont, bodyFont;
        private static Sprite softGlow;
        public static TMP_FontAsset DisplayFont => displayFont != null ? displayFont : (displayFont = Resources.Load<TMP_FontAsset>("UI/ArchiveSerif"));
        public static TMP_FontAsset BodyFont => bodyFont != null ? bodyFont : (bodyFont = Resources.Load<TMP_FontAsset>("UI/MenuType"));
        public static readonly Color Ivory = new Color32(236, 242, 252, 255);
        public static readonly Color Silver = new Color32(180, 198, 224, 240);
        public static readonly Color Amber = new Color32(255, 196, 88, 255);
        public static bool ReducedMotion => PlayerPrefs.GetInt("TLR.Menu.ReducedMotion", 0) == 1;

        public static Sprite SoftGlow
        {
            get
            {
                if (softGlow != null) return softGlow;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Archive soft light", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (x + .5f) / size * 2 - 1, ny = (y + .5f) / size * 2 - 1;
                        float falloff = Mathf.Clamp01(1 - nx * nx - ny * ny);
                        pixels[y * size + x] = new Color(1, 1, 1, falloff * falloff * falloff);
                    }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                softGlow = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f);
                softGlow.hideFlags = HideFlags.HideAndDontSave;
                return softGlow;
            }
        }

        public static float FitScale(Vector2 available, Vector2 content)
        {
            if (available.x <= 0 || available.y <= 0 || content.x <= 0 || content.y <= 0) return 1;
            return Mathf.Min(1.2f, available.x * .9f / content.x, available.y * .9f / content.y);
        }
    }
}
