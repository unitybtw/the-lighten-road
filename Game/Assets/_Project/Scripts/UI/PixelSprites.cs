using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("TheLightenRoad.Player.PlayModeTests")]

namespace TheLightenRoad.UI
{
    /// <summary>
    /// Procedurally generated point-filtered sprites for the HUD and overlays, so the UI works
    /// before final art from Can / Maksim is imported.
    /// </summary>
    public static class PixelSprites
    {
        private static Sprite white;
        private static Sprite glow;
        private static Sprite flame;
        private static Sprite lanternFrame;
        private static Sprite vignette;
        private static Sprite extinguishedFlame;
        private static Sprite skullIcon;
        private static Sprite emberFull;
        private static Sprite emberHalf;
        private static Sprite emberEmpty;
        private static Sprite emberCracks;
        private static Sprite emberFragments;
        private static Sprite spark;

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

        // 7x8 extinguished flame / wisp
        private static readonly string[] ExtinguishedFlameMap =
        {
            "...~...",
            "..~....",
            "...~...",
            "....~..",
            "..~~...",
            ".~..~..",
            "..~~...",
            ".......",
        };

        // 8x8 pixel skull
        private static readonly string[] SkullMap =
        {
            "..####..",
            ".######.",
            ".##..##.",
            ".######.",
            "..####..",
            "..####..",
            "..#.##..",
            "........",
        };

        // Asymmetric rough stone placeholders. Replace through LanternHUD's art slots.
        private static readonly string[] EmberFullMap =
        {
            "...##.....", "..#++#....", "..#+@+#...", ".#++@++#..", ".#+@@@+#..",
            ".#+@@++#..", "..#++@++#.", "..#+++++#.", "...#+++#..", "....###...",
        };
        private static readonly string[] EmberHalfMap =
        {
            "...##.....", "..#--#....", "..#-+-#...", ".#--+--#..", ".#-+++-#..",
            ".#-++--#..", "..#--+--#.", "..#-----#.", "...#---#..", "....###...",
        };
        private static readonly string[] EmberEmptyMap =
        {
            "...##.....", "..#..#....", "..#...#...", ".#.....#..", ".#.....#..",
            ".#.....#..", "..#.....#.", "..#.....#.", "...#...#..", "....###...",
        };
        private static readonly string[] EmberCrackMap =
        {
            "....#.....", "....#.....", "...#......", "...##.....", "....#.....",
            "...#.##...", "..#...#...", "......#...", ".....#....", ".....#....",
        };
        public static Sprite EmberCracks => emberCracks != null ? emberCracks : emberCracks = Build(EmberCrackMap,
            c => c == '#' ? Color.white : Color.clear);
        private static readonly string[] EmberFragmentMap =
        {
            "..##......", "..#....#..", "......##..", "..........", "...#......",
            "..##..#...", "......##..", "..#.......", "...#......", "..........",
        };
        public static Sprite EmberFragments => emberFragments != null ? emberFragments : emberFragments = Build(EmberFragmentMap,
            c => c == '#' ? new Color(0.6f, 0.56f, 0.48f, 1f) : Color.clear);

        private static readonly string[] SparkMap =
        {
            ".#.",
            "###",
            ".#.",
        };
        public static Sprite Spark => spark != null ? spark : spark = Build(SparkMap,
            c => c == '#' ? Color.white : Color.clear);

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

        public static Sprite ExtinguishedFlame => extinguishedFlame != null ? extinguishedFlame : extinguishedFlame = Build(ExtinguishedFlameMap, c =>
        {
            switch (c)
            {
                case '~': return new Color(0.75f, 0.7f, 0.7f, 0.85f);
                default: return Color.clear;
            }
        });

        public static Sprite SkullIcon => skullIcon != null ? skullIcon : skullIcon = Build(SkullMap, c =>
        {
            switch (c)
            {
                case '#': return new Color32(235, 225, 215, 255);
                default: return Color.clear;
            }
        });

        public static Sprite EmberFull => emberFull != null ? emberFull : emberFull = Build(EmberFullMap, c =>
        {
            switch (c)
            {
                case '#': return new Color32(48, 24, 12, 255);
                case '+': return new Color32(190, 112, 46, 255);
                case '@': return new Color32(244, 210, 135, 255);
                default: return Color.clear;
            }
        });

        public static Sprite EmberHalf => emberHalf != null ? emberHalf : emberHalf = Build(EmberHalfMap, c =>
        {
            switch (c)
            {
                case '#': return new Color32(48, 24, 12, 255);
                case '+': return new Color32(142, 88, 44, 255);
                case '@': return new Color32(196, 151, 86, 255);
                case '-': return new Color32(75, 58, 48, 200);
                default: return Color.clear;
            }
        });

        public static Sprite EmberEmpty => emberEmpty != null ? emberEmpty : emberEmpty = Build(EmberEmptyMap, c =>
        {
            switch (c)
            {
                case '#': return new Color32(65, 52, 44, 180);
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

        public static Sprite Vignette
        {
            get
            {
                if (vignette != null)
                    return vignette;

                const int size = 64;
                var texture = NewTexture(size, size);
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    // Stepped pixel vignette: center clear, outer edge completely black
                    float a = Mathf.Clamp01((d - 0.32f) / 0.68f);
                    a = Mathf.Round(a * 10f) / 10f;
                    texture.SetPixel(x, y, new Color(0.02f, 0.02f, 0.04f, a));
                }
                texture.Apply();
                return vignette = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
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
