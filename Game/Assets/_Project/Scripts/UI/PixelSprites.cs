using UnityEngine;

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Procedurally generated point-filtered sprites for the HUD, so the UI works before
    /// final art from Can / Maksim is imported. Replace with imported sprites later.
    /// </summary>
    internal static class PixelSprites
    {
        private static Sprite white;
        private static Sprite glow;
        private static Sprite flame;
        private static Sprite lanternFrame;

        public static Sprite White => white != null ? white : white = Build(new[] { "#" },
            c => Color.white);

        // 11x15 lantern frame. '#' dark brass outline, '+' brass highlight, 'o' ring, '.' transparent.
        private static readonly string[] LanternMap =
        {
            "....ooo....",
            "...o...o...",
            "....ooo....",
            "..#######..",
            "..#+++++#..",
            "..#.....#..",
            "..+.....#..",
            "..+.....#..",
            "..+.....#..",
            "..+.....#..",
            "..#.....#..",
            "..#######..",
            ".#+++++++#.",
            ".#########.",
            "...........",
        };

        // 5x6 flame, '@' core, '*' body.
        private static readonly string[] FlameMap =
        {
            "..*..",
            "..*..",
            ".*@*.",
            ".*@*.",
            "*@@@*",
            ".*@*.",
        };

        public static Sprite LanternFrame => lanternFrame != null ? lanternFrame : lanternFrame = Build(LanternMap, c =>
        {
            switch (c)
            {
                case '#': return new Color32(58, 38, 24, 255);
                case '+': return new Color32(176, 124, 64, 255);
                case 'o': return new Color32(110, 80, 52, 255);
                default: return Color.clear;
            }
        });

        public static Sprite Flame => flame != null ? flame : flame = Build(FlameMap, c =>
        {
            switch (c)
            {
                case '@': return Color.white;
                case '*': return new Color(1f, 0.72f, 0.5f, 1f);
                default: return Color.clear;
            }
        });

        public static Sprite Glow
        {
            get
            {
                if (glow != null)
                    return glow;

                const int size = 32;
                var texture = NewTexture(size, size);
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    // Quantised falloff gives a stepped, pixel-art halo.
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Round(a * a * 6f) / 6f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                texture.Apply();
                return glow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            }
        }

        private static Sprite Build(string[] map, System.Func<char, Color> palette)
        {
            int height = map.Length;
            int width = map[0].Length;
            var texture = NewTexture(width, height);
            for (int row = 0; row < height; row++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, height - 1 - row, palette(map[row][x]));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }
    }
}
