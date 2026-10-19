using System.Collections.Generic;
using UnityEngine;

namespace TheLightenRoad.Lantern.Glyphs
{
    /// <summary>
    /// Procedurally generated point-filtered pixel sprites for ancient ruin runes and secret glyphs.
    /// Operates completely self-contained with zero external asset dependencies.
    /// </summary>
    public static class GlyphSprites
    {
        private static readonly Dictionary<GlyphType, Sprite> cachedSprites = new Dictionary<GlyphType, Sprite>();

        // 16x16 Arrow pointing Up
        private static readonly string[] ArrowUpMap =
        {
            ".......##.......",
            "......####......",
            ".....######.....",
            "....##.##.##....",
            "...##..##..##...",
            "..##...##...##..",
            ".##....##....##.",
            "##.....##.....##",
            ".......##.......",
            ".......##.......",
            ".......##.......",
            ".......##.......",
            ".......##.......",
            ".....######.....",
            "......####......",
            ".......##.......",
        };

        // 16x16 Arrow pointing Right
        private static readonly string[] ArrowRightMap =
        {
            "................",
            ".......#........",
            ".......##.......",
            ".......###......",
            ".......####.....",
            ".......#####....",
            "##############..",
            "###############.",
            "###############.",
            "##############..",
            ".......#####....",
            ".......####.....",
            ".......###......",
            ".......##.......",
            ".......#........",
            "................",
        };

        // 16x16 Radiant Ember Eye
        private static readonly string[] EmberEyeMap =
        {
            "......####......",
            "....########....",
            "...##......##...",
            "..##...##...##..",
            ".##...####...##.",
            ".#...######...#.",
            "##..########..##",
            "##..###..###..##",
            "##..###..###..##",
            "##..########..##",
            ".#...######...#.",
            ".##...####...##.",
            "..##...##...##..",
            "...##......##...",
            "....########....",
            "......####......",
        };

        // 16x16 Old World Archive Seal
        private static readonly string[] ArchiveSealMap =
        {
            ".......##.......",
            "......####......",
            ".....##..##.....",
            "....##....##....",
            "...##..##..##...",
            "..##..####..##..",
            ".##..##..##..##.",
            "##..##....##..##",
            "##..##....##..##",
            ".##..##..##..##.",
            "..##..####..##..",
            "...##..##..##...",
            "....##....##....",
            ".....##..##.....",
            "......####......",
            ".......##.......",
        };

        // 16x16 Gateway / Portal Rune
        private static readonly string[] GatewayRuneMap =
        {
            "...##########...",
            "..############..",
            ".##############.",
            ".##...####...##.",
            ".##...####...##.",
            ".##...####...##.",
            ".##...####...##.",
            ".##..........##.",
            ".##..........##.",
            ".##...####...##.",
            ".##...####...##.",
            ".##...####...##.",
            ".##...####...##.",
            "################",
            ".##############.",
            "..############..",
        };

        // 16x16 Hidden Cache (Diamond enclosed by brackets)
        private static readonly string[] HiddenCacheMap =
        {
            "####........####",
            "###..........###",
            "##.....##.....##",
            "#.....####.....#",
            ".....######.....",
            "....##.##.##....",
            "...##..##..##...",
            "..##...##...##..",
            "..##...##...##..",
            "...##..##..##...",
            "....##.##.##....",
            ".....######.....",
            "#.....####.....#",
            "##.....##.....##",
            "###..........###",
            "####........####",
        };

        /// <summary>
        /// Retrieves or creates a point-filtered procedural sprite for the given glyph type.
        /// </summary>
        public static Sprite GetSprite(GlyphType type)
        {
            if (cachedSprites.TryGetValue(type, out var existing) && existing != null)
                return existing;

            Sprite created;
            switch (type)
            {
                case GlyphType.ArrowUp:
                    created = BuildFromMap(ArrowUpMap, 16);
                    break;
                case GlyphType.ArrowRight:
                    created = BuildFromMap(ArrowRightMap, 16);
                    break;
                case GlyphType.ArrowDown:
                    created = BuildFromMap(FlipVertical(ArrowUpMap), 16);
                    break;
                case GlyphType.ArrowLeft:
                    created = BuildFromMap(FlipHorizontal(ArrowRightMap), 16);
                    break;
                case GlyphType.EmberEye:
                    created = BuildFromMap(EmberEyeMap, 16);
                    break;
                case GlyphType.ArchiveSeal:
                    created = BuildFromMap(ArchiveSealMap, 16);
                    break;
                case GlyphType.GatewayRune:
                    created = BuildFromMap(GatewayRuneMap, 16);
                    break;
                case GlyphType.HiddenCache:
                    created = BuildFromMap(HiddenCacheMap, 16);
                    break;
                default:
                    created = BuildFromMap(EmberEyeMap, 16);
                    break;
            }

            cachedSprites[type] = created;
            return created;
        }

        private static string[] FlipVertical(string[] map)
        {
            var flipped = new string[map.Length];
            for (int i = 0; i < map.Length; i++)
                flipped[i] = map[map.Length - 1 - i];
            return flipped;
        }

        private static string[] FlipHorizontal(string[] map)
        {
            var flipped = new string[map.Length];
            for (int i = 0; i < map.Length; i++)
            {
                var chars = map[i].ToCharArray();
                System.Array.Reverse(chars);
                flipped[i] = new string(chars);
            }
            return flipped;
        }

        private static Sprite BuildFromMap(string[] map, int pixelsPerUnit)
        {
            int height = map.Length;
            int width = map[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "GlyphTexture_" + width + "x" + height
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = map[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = row[x];
                    pixels[y * width + x] = c == '#' ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
        }
    }
}
