using UnityEngine;

namespace TheLightenRoad.UI
{
    /// <summary>Point-filtered bookcloth, woven thread and folded iron fasteners for the stamina HUD.</summary>
    internal static class StaminaBindingArt
    {
        private static Sprite cloth, weave, clip, knot;

        internal static Sprite Cloth => cloth != null ? cloth : cloth = Ribbon(false);
        internal static Sprite Weave => weave != null ? weave : weave = Ribbon(true);
        internal static Sprite Clip => clip != null ? clip : clip = Map(new[]
        {
            "..###..", ".#hhh#.", "##shh##", "#soos##", "#ssssh#", "#sttsh#",
            "#sttsh#", "#ssss##", "#soos##", "##ssh##", ".#ssh#.", "..###.."
        });
        internal static Sprite Knot => knot != null ? knot : knot = Map(new[]
        {
            ".tt.", "tttt", ".th.", "tttt", ".ts.", "..t.", ".t.."
        });

        private static Sprite Ribbon(bool thread)
        {
            const int width = 154;
            int height = thread ? 7 : 11;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Stepped selvage and a few missing fibres; no random noise or animated texture.
                    int inset = y == 0 || y == height - 1 ? 3 : y == 1 || y == height - 2 ? 1 : 0;
                    if (x < inset || x >= width - inset) continue;
                    if ((y == 0 && (x % 29 == 8 || x % 37 == 14)) ||
                        (y == height - 1 && (x % 31 == 19 || x % 23 == 7))) continue;
                    Color32 ink;
                    if (thread)
                    {
                        bool stitch = y == height / 2 && (x % 12 == 5 || x % 12 == 6);
                        if (stitch) continue;
                        byte value = (byte)(y == 0 ? 145 : y == height - 1 ? 215
                            : x % 9 == 3 && y % 2 == 0 ? 215 : y == 1 || y == height - 2 ? 235 : 255);
                        ink = new Color32(value, value, value, 255);
                    }
                    else
                    {
                        bool stitch = y == height / 2 && (x % 12 == 5 || x % 12 == 6);
                        ink = y == 0 || stitch ? new Color32(22, 20, 17, 255)
                            : y == height - 1 ? new Color32(89, 75, 54, 255)
                            : y == 2 || y == height - 3 ? new Color32(54, 47, 36, 255)
                            : new Color32(42, 37, 29, 255);
                    }
                    pixels[y * width + x] = ink;
                }
            return Create(thread ? "Stamina woven linen" : "Stamina worn bookcloth", width, height, pixels);
        }

        private static Sprite Map(string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[(height - 1 - y) * width + x] = rows[y][x] switch
                    {
                        '#' => new Color32(29, 27, 23, 255),
                        'h' => new Color32(133, 122, 100, 255),
                        's' => new Color32(69, 65, 54, 255),
                        'o' => new Color32(14, 14, 12, 255),
                        't' => new Color32(211, 192, 151, 255),
                        _ => new Color32(0, 0, 0, 0)
                    };
            return Create("Stamina binding detail", width, height, pixels);
        }

        private static Sprite Create(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 1);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
