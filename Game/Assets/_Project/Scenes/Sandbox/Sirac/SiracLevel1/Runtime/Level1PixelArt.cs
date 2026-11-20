using System.Collections.Generic;
using UnityEngine;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Procedural point-filtered pixel art library for SiracLevel1 UI and archive mechanisms.
    /// Texture builders are exposed so the Editor builder can bake identical PNG assets
    /// while runtime callers (HUD and dynamic test objects) share cached sprites.
    /// </summary>
    public static class Level1PixelArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>();

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Ink = new Color32(18, 15, 13, 255);
        private static readonly Color32 IronDark = new Color32(52, 49, 48, 255);
        private static readonly Color32 IronMid = new Color32(88, 84, 80, 255);
        private static readonly Color32 IronLight = new Color32(138, 132, 124, 255);
        private static readonly Color32 BrassDark = new Color32(118, 82, 42, 255);
        private static readonly Color32 BrassMid = new Color32(186, 138, 76, 255);
        private static readonly Color32 BrassHi = new Color32(236, 198, 132, 255);
        private static readonly Color32 WoodDark = new Color32(72, 48, 32, 255);
        private static readonly Color32 WoodMid = new Color32(112, 76, 48, 255);
        private static readonly Color32 StoneDark = new Color32(58, 56, 62, 255);
        private static readonly Color32 StoneMid = new Color32(94, 92, 98, 255);
        private static readonly Color32 StoneLight = new Color32(136, 134, 140, 255);
        private static readonly Color32 PureHi = new Color32(252, 250, 244, 255);
        private static readonly Color32 PureMid = new Color32(210, 206, 198, 255);
        private static readonly Color32 PureLow = new Color32(148, 144, 138, 255);

        public static Sprite LensFrame => GetOrCreate("LensFrame", BuildLensFrameTexture);
        public static Sprite LensCore => GetOrCreate("LensCore", BuildLensCoreTexture);
        public static Sprite SelectorDiamond => GetOrCreate("SelectorDiamond", BuildSelectorDiamondTexture);
        public static Sprite VeinEmberCrystal => GetOrCreate("VeinEmberCrystal", BuildVeinEmberCrystalTexture);
        public static Sprite MementoSealEmpty => GetOrCreate("MementoSealEmpty", () => BuildMementoSealTexture(false));
        public static Sprite MementoSealLit => GetOrCreate("MementoSealLit", () => BuildMementoSealTexture(true));
        public static Sprite CorePrismEmpty => GetOrCreate("CorePrismEmpty", () => BuildCorePrismTexture(false));
        public static Sprite CorePrismLit => GetOrCreate("CorePrismLit", () => BuildCorePrismTexture(true));
        public static Sprite FiligreeRule => GetOrCreate("FiligreeRule", BuildFiligreeRuleTexture, new Vector4(20f, 0f, 20f, 0f));

        public static Sprite LadderRungTile => GetOrCreate("LadderRungTile", BuildLadderRungTexture);
        public static Sprite GearCog => GetOrCreate("GearCog", BuildGearCogTexture);
        public static Sprite BarricadeClasp => GetOrCreate("BarricadeClasp", BuildBarricadeClaspTexture, new Vector4(3f, 0f, 3f, 0f));
        public static Sprite BeamGirder => GetOrCreate("BeamGirder", BuildBeamGirderTexture, new Vector4(5f, 3f, 5f, 3f));
        public static Sprite PusherRam => GetOrCreate("PusherRam", BuildPusherRamTexture, new Vector4(4f, 3f, 4f, 3f));
        public static Sprite EtchedBlock => GetOrCreate("EtchedBlock", BuildEtchedBlockTexture);
        public static Sprite MirrorTrack => GetOrCreate("MirrorTrack", BuildMirrorTrackTexture, new Vector4(4f, 2f, 4f, 2f));
        public static Sprite MirrorBlade => GetOrCreate("MirrorBlade", BuildMirrorBladeTexture);
        public static Sprite DarknessVeil => GetOrCreate("DarknessVeil", BuildDarknessVeilTexture, new Vector4(3f, 3f, 3f, 3f));
        public static Sprite BreakawayPlank => GetOrCreate("BreakawayPlank", BuildBreakawayPlankTexture, new Vector4(4f, 2f, 4f, 2f));
        public static Sprite ArchiveWallTile => GetOrCreate("ArchiveWallTile", BuildArchiveWallTileTexture);
        public static Sprite HazardWater => GetOrCreate("HazardWater", BuildHazardWaterTexture);
        public static Sprite SecretDoor => GetOrCreate("SecretDoor", BuildSecretDoorTexture, new Vector4(3f, 3f, 3f, 3f));
        public static Sprite SteelBookcase => GetOrCreate("SteelBookcase", BuildSteelBookcaseTexture);
        public static Sprite NoticeBoard => GetOrCreate("NoticeBoard", BuildNoticeBoardTexture);
        public static Sprite WallClock => GetOrCreate("WallClock", BuildWallClockTexture);
        public static Sprite RoomSignPlate => GetOrCreate("RoomSignPlate", BuildRoomSignPlateTexture);
        public static Sprite PlasterWallPatch => GetOrCreate("PlasterWallPatch", BuildPlasterWallPatchTexture);
        public static Sprite RebarBeamEnd => GetOrCreate("RebarBeamEnd", BuildRebarBeamEndTexture);

        public static Sprite GetConduitPipe(ConduitPipeShape shape)
        {
            switch (shape)
            {
                case ConduitPipeShape.Corner: return GetOrCreate("ConduitCorner", () => BuildConduitPipeTexture(ConduitPipeShape.Corner));
                case ConduitPipeShape.Tee: return GetOrCreate("ConduitTee", () => BuildConduitPipeTexture(ConduitPipeShape.Tee));
                case ConduitPipeShape.Cross: return GetOrCreate("ConduitCross", () => BuildConduitPipeTexture(ConduitPipeShape.Cross));
                default: return GetOrCreate("ConduitStraight", () => BuildConduitPipeTexture(ConduitPipeShape.Straight));
            }
        }

        public static IEnumerable<(string key, Texture2D tex, Vector4 border)> EnumerateWorldTextures()
        {
            yield return ("LadderRungTile", BuildLadderRungTexture(), Vector4.zero);
            yield return ("GearCog", BuildGearCogTexture(), Vector4.zero);
            yield return ("BarricadeClasp", BuildBarricadeClaspTexture(), new Vector4(3f, 0f, 3f, 0f));
            yield return ("BeamGirder", BuildBeamGirderTexture(), new Vector4(5f, 3f, 5f, 3f));
            yield return ("PusherRam", BuildPusherRamTexture(), new Vector4(4f, 3f, 4f, 3f));
            yield return ("EtchedBlock", BuildEtchedBlockTexture(), Vector4.zero);
            yield return ("ConduitStraight", BuildConduitPipeTexture(ConduitPipeShape.Straight), Vector4.zero);
            yield return ("ConduitCorner", BuildConduitPipeTexture(ConduitPipeShape.Corner), Vector4.zero);
            yield return ("ConduitTee", BuildConduitPipeTexture(ConduitPipeShape.Tee), Vector4.zero);
            yield return ("ConduitCross", BuildConduitPipeTexture(ConduitPipeShape.Cross), Vector4.zero);
            yield return ("MirrorTrack", BuildMirrorTrackTexture(), new Vector4(4f, 2f, 4f, 2f));
            yield return ("MirrorBlade", BuildMirrorBladeTexture(), Vector4.zero);
            yield return ("DarknessVeil", BuildDarknessVeilTexture(), new Vector4(3f, 3f, 3f, 3f));
            yield return ("BreakawayPlank", BuildBreakawayPlankTexture(), new Vector4(4f, 2f, 4f, 2f));
            yield return ("MementoRelic", BuildMementoSealTexture(true), Vector4.zero);
            yield return ("VeinEmberCrystal", BuildVeinEmberCrystalTexture(), Vector4.zero);
            yield return ("ArchiveWallTile", BuildArchiveWallTileTexture(), Vector4.zero);
            yield return ("HazardWater", BuildHazardWaterTexture(), Vector4.zero);
            yield return ("SecretDoor", BuildSecretDoorTexture(), new Vector4(3f, 3f, 3f, 3f));
            yield return ("SteelBookcase", BuildSteelBookcaseTexture(), Vector4.zero);
            yield return ("NoticeBoard", BuildNoticeBoardTexture(), Vector4.zero);
            yield return ("WallClock", BuildWallClockTexture(), Vector4.zero);
            yield return ("RoomSignPlate", BuildRoomSignPlateTexture(), Vector4.zero);
            yield return ("PlasterWallPatch", BuildPlasterWallPatchTexture(), Vector4.zero);
            yield return ("RebarBeamEnd", BuildRebarBeamEndTexture(), Vector4.zero);
        }

        private static Sprite GetOrCreate(string key, System.Func<Texture2D> builder, Vector4 border = default)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            Texture2D tex = builder();
            tex.name = $"L1_{key}_Tex";
            tex.Apply(false, false);

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                16f,
                0u,
                SpriteMeshType.FullRect,
                border);
            sprite.name = $"L1_{key}";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            Borders[key] = border;
            return sprite;
        }

        private static Texture2D NewTex(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            tex.SetPixels32(px);
            return tex;
        }

        private static void SetPx(Texture2D tex, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, c);
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                SetPx(tex, x, y, c);
        }

        private static Texture2D BuildLensFrameTexture()
        {
            var tex = NewTex(16, 16);
            float cx = 7.5f, cy = 7.5f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > 7.7f || d < 3.6f) continue;
                if (d >= 6.8f || d <= 4.3f) SetPx(tex, x, y, Ink);
                else if (y > 8 && x < 9) SetPx(tex, x, y, BrassHi);
                else if (y >= 6) SetPx(tex, x, y, BrassMid);
                else SetPx(tex, x, y, BrassDark);
            }
            // Four cardinal notches like an archive astrolabe bezel
            SetPx(tex, 7, 15, BrassHi); SetPx(tex, 8, 15, BrassHi);
            SetPx(tex, 7, 0, BrassDark); SetPx(tex, 8, 0, BrassDark);
            SetPx(tex, 0, 7, BrassMid); SetPx(tex, 0, 8, BrassMid);
            SetPx(tex, 15, 7, BrassMid); SetPx(tex, 15, 8, BrassMid);
            return tex;
        }

        private static Texture2D BuildLensCoreTexture()
        {
            var tex = NewTex(16, 16);
            float cx = 7.5f, cy = 7.5f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > 4.2f) continue;
                if (d <= 1.6f) SetPx(tex, x, y, PureHi);
                else if (y >= 8 && x <= 8) SetPx(tex, x, y, PureHi);
                else if (d <= 3.1f) SetPx(tex, x, y, PureMid);
                else SetPx(tex, x, y, PureLow);
            }
            // Specular catchlight
            SetPx(tex, 6, 9, PureHi);
            SetPx(tex, 5, 9, PureHi);
            return tex;
        }

        private static Texture2D BuildSelectorDiamondTexture()
        {
            var tex = NewTex(7, 7);
            int cx = 3, cy = 3;
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                int manhattan = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
                if (manhattan == 3) SetPx(tex, x, y, Ink);
                else if (manhattan == 2) SetPx(tex, x, y, PureMid);
                else if (manhattan <= 1) SetPx(tex, x, y, PureHi);
            }
            return tex;
        }

        private static Texture2D BuildVeinEmberCrystalTexture()
        {
            var tex = NewTex(12, 16);
            for (int y = 1; y <= 14; y++)
            {
                int halfW;
                if (y <= 3) halfW = y;
                else if (y <= 10) halfW = 4;
                else halfW = 15 - y;

                for (int dx = -halfW; dx <= halfW; dx++)
                {
                    int x = 5 + dx;
                    if (x < 0 || x >= 12) continue;
                    bool edge = Mathf.Abs(dx) == halfW || y == 1 || y == 14;
                    if (edge)
                    {
                        SetPx(tex, x, y, Ink);
                    }
                    else if (dx <= -1 && y >= 5 && y <= 11)
                    {
                        SetPx(tex, x, y, PureHi);
                    }
                    else if (dx <= 0 || y >= 9)
                    {
                        SetPx(tex, x, y, PureMid);
                    }
                    else
                    {
                        SetPx(tex, x, y, PureLow);
                    }
                }
            }
            SetPx(tex, 4, 10, PureHi);
            SetPx(tex, 5, 11, PureHi);
            return tex;
        }

        private static Texture2D BuildMementoSealTexture(bool lit)
        {
            var tex = NewTex(11, 11);
            int cx = 5, cy = 5;
            for (int y = 0; y < 11; y++)
            for (int x = 0; x < 11; x++)
            {
                int m = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
                if (m > 5) continue;
                if (m == 5)
                {
                    SetPx(tex, x, y, Ink);
                }
                else if (m == 4)
                {
                    SetPx(tex, x, y, lit ? PureLow : IronLight);
                }
                else if (lit)
                {
                    if (m <= 1) SetPx(tex, x, y, PureHi);
                    else SetPx(tex, x, y, y >= cy && x <= cx ? PureHi : PureMid);
                }
                else if (m == 3)
                {
                    SetPx(tex, x, y, IronMid);
                }
                else if (m <= 2)
                {
                    SetPx(tex, x, y, new Color32(42, 38, 36, 210));
                }
            }
            return tex;
        }

        private static Texture2D BuildCorePrismTexture(bool lit)
        {
            var tex = NewTex(9, 13);
            for (int y = 0; y < 13; y++)
            {
                int halfW = y < 3 ? y + 1 : (y > 9 ? 13 - y : 3);
                for (int dx = -halfW; dx <= halfW; dx++)
                {
                    int x = 4 + dx;
                    bool edge = Mathf.Abs(dx) == halfW || y == 0 || y == 12;
                    if (edge)
                    {
                        SetPx(tex, x, y, Ink);
                    }
                    else if (!lit)
                    {
                        bool rim = Mathf.Abs(dx) == halfW - 1 || y == 1 || y == 11;
                        SetPx(tex, x, y, rim ? IronLight : new Color32(46, 42, 40, 210));
                    }
                    else
                    {
                        if (dx <= 0 && y >= 4 && y <= 8) SetPx(tex, x, y, PureHi);
                        else if (Mathf.Abs(dx) <= 1) SetPx(tex, x, y, PureMid);
                        else SetPx(tex, x, y, PureLow);
                    }
                }
            }
            return tex;
        }

        private static Texture2D BuildFiligreeRuleTexture()
        {
            var tex = NewTex(96, 5);
            for (int x = 4; x < 92; x++)
            {
                int dist = Mathf.Abs(x - 48);
                byte a = dist < 24 ? (byte)220 : (byte)Mathf.RoundToInt(Mathf.Lerp(220f, 0f, (dist - 24f) / 24f));
                SetPx(tex, x, 2, new Color32(190, 148, 86, a));
                if (x % 8 == 0 && dist > 6 && dist < 34)
                {
                    SetPx(tex, x, 3, new Color32(150, 114, 66, (byte)(a * 0.65f)));
                    SetPx(tex, x, 1, new Color32(150, 114, 66, (byte)(a * 0.65f)));
                }
            }
            // Central archive diamond clasp
            for (int y = 0; y < 5; y++)
            for (int x = 46; x <= 50; x++)
            {
                int m = Mathf.Abs(x - 48) + Mathf.Abs(y - 2);
                if (m == 2) SetPx(tex, x, y, BrassMid);
                else if (m <= 1) SetPx(tex, x, y, BrassHi);
            }
            return tex;
        }

        private static Texture2D BuildLadderRungTexture()
        {
            var tex = NewTex(16, 16);
            // Left and right wooden/iron rails
            FillRect(tex, 2, 0, 2, 15, Ink);
            FillRect(tex, 3, 0, 3, 15, WoodMid);
            FillRect(tex, 4, 0, 4, 15, Ink);
            FillRect(tex, 11, 0, 11, 15, Ink);
            FillRect(tex, 12, 0, 12, 15, WoodDark);
            FillRect(tex, 13, 0, 13, 15, Ink);

            // Two iron rungs per 16px tile
            int[] rungY = { 3, 11 };
            foreach (int ry in rungY)
            {
                FillRect(tex, 3, ry - 1, 12, ry - 1, Ink);
                FillRect(tex, 3, ry, 12, ry, IronLight);
                FillRect(tex, 3, ry + 1, 12, ry + 1, Ink);
                SetPx(tex, 3, ry, BrassHi);
                SetPx(tex, 12, ry, BrassMid);
            }
            return tex;
        }

        private static Texture2D BuildGearCogTexture()
        {
            var tex = NewTex(16, 16);
            float cx = 7.5f, cy = 7.5f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                bool tooth = Mathf.Cos(ang * 8f) > 0.2f;
                float outerR = tooth ? 7.6f : 5.7f;

                if (r > outerR || r < 1.9f) continue;
                if (r >= outerR - 0.9f || r <= 2.7f) SetPx(tex, x, y, Ink);
                else if (r <= 3.7f) SetPx(tex, x, y, BrassMid);
                else if (dy > 0f && dx < 2f) SetPx(tex, x, y, PureHi);
                else SetPx(tex, x, y, PureMid);
            }
            return tex;
        }

        private static Texture2D BuildBarricadeClaspTexture()
        {
            var tex = NewTex(16, 8);
            FillRect(tex, 0, 1, 15, 6, Ink);
            FillRect(tex, 1, 2, 14, 5, IronMid);
            FillRect(tex, 1, 4, 14, 5, IronLight);
            // Diagonal warning hash marks + center lock stud
            for (int x = 2; x <= 13; x += 4)
            {
                SetPx(tex, x, 2, BrassDark);
                SetPx(tex, x + 1, 3, BrassMid);
                SetPx(tex, x + 2, 4, BrassHi);
            }
            FillRect(tex, 6, 1, 9, 6, Ink);
            FillRect(tex, 7, 2, 8, 5, BrassMid);
            return tex;
        }

        private static Texture2D BuildBeamGirderTexture()
        {
            var tex = NewTex(32, 12);
            FillRect(tex, 0, 0, 31, 11, Ink);
            FillRect(tex, 1, 1, 30, 10, IronDark);
            FillRect(tex, 1, 8, 30, 10, IronLight);
            FillRect(tex, 1, 4, 30, 7, IronMid);
            // Brass end caps (preserved cleanly by 9-slice border)
            FillRect(tex, 1, 1, 3, 10, BrassDark);
            FillRect(tex, 1, 8, 3, 10, BrassHi);
            FillRect(tex, 2, 4, 2, 7, BrassMid);
            FillRect(tex, 28, 1, 30, 10, BrassDark);
            FillRect(tex, 28, 8, 30, 10, BrassHi);
            FillRect(tex, 29, 4, 29, 7, BrassMid);
            // Rivets along girder face
            for (int x = 7; x <= 25; x += 6)
            {
                SetPx(tex, x, 5, Ink);
                SetPx(tex, x, 6, BrassHi);
            }
            return tex;
        }

        private static Texture2D BuildPusherRamTexture()
        {
            var tex = NewTex(20, 14);
            FillRect(tex, 0, 0, 19, 13, Ink);
            FillRect(tex, 1, 1, 18, 12, BrassDark);
            FillRect(tex, 1, 9, 18, 12, BrassHi);
            FillRect(tex, 2, 4, 17, 9, BrassMid);
            // Recessed bronze compression grooves
            for (int x = 5; x <= 14; x += 3)
            {
                FillRect(tex, x, 2, x, 11, Ink);
                FillRect(tex, x + 1, 3, x + 1, 10, BrassHi);
            }
            return tex;
        }

        private static Texture2D BuildEtchedBlockTexture()
        {
            var tex = NewTex(16, 16);
            FillRect(tex, 0, 0, 15, 15, Ink);
            FillRect(tex, 1, 1, 14, 14, StoneDark);
            FillRect(tex, 1, 13, 14, 14, StoneLight);
            FillRect(tex, 1, 1, 2, 14, StoneLight);
            FillRect(tex, 3, 3, 12, 12, StoneMid);
            // Inner carved rune diamond
            for (int y = 4; y <= 11; y++)
            for (int x = 4; x <= 11; x++)
            {
                int m = Mathf.Abs(x - 7) + Mathf.Abs(y - 7);
                if (m == 3) SetPx(tex, x, y, Ink);
                else if (m == 2) SetPx(tex, x, y, PureMid);
                else if (m <= 1) SetPx(tex, x, y, BrassHi);
            }
            return tex;
        }

        private static Texture2D BuildConduitPipeTexture(ConduitPipeShape shape)
        {
            var tex = NewTex(16, 16);
            bool north = shape == ConduitPipeShape.Straight || shape == ConduitPipeShape.Corner || shape == ConduitPipeShape.Tee || shape == ConduitPipeShape.Cross;
            bool east = shape == ConduitPipeShape.Corner || shape == ConduitPipeShape.Tee || shape == ConduitPipeShape.Cross;
            bool south = shape == ConduitPipeShape.Straight || shape == ConduitPipeShape.Cross;
            bool west = shape == ConduitPipeShape.Tee || shape == ConduitPipeShape.Cross;

            // Dark outer iron housing with port flanges
            DrawConduitArm(tex, north, east, south, west, 4, 5, Ink);
            DrawConduitArm(tex, north, east, south, west, 3, 4, PureLow);
            DrawConduitArm(tex, north, east, south, west, 1, 2, PureMid);
            DrawConduitArm(tex, north, east, south, west, 0, 1, PureHi);

            // End flanges so pipe orientation reads clearly at a glance
            if (north) { FillRect(tex, 3, 14, 12, 15, Ink); FillRect(tex, 4, 14, 11, 14, BrassMid); FillRect(tex, 7, 14, 8, 15, PureHi); }
            if (south) { FillRect(tex, 3, 0, 12, 1, Ink); FillRect(tex, 4, 1, 11, 1, BrassMid); FillRect(tex, 7, 0, 8, 1, PureHi); }
            if (east) { FillRect(tex, 14, 3, 15, 12, Ink); FillRect(tex, 14, 4, 14, 11, BrassMid); FillRect(tex, 14, 7, 15, 8, PureHi); }
            if (west) { FillRect(tex, 0, 3, 1, 12, Ink); FillRect(tex, 1, 4, 1, 11, BrassMid); FillRect(tex, 0, 7, 1, 8, PureHi); }

            // Junction collar highlight for Tee and Cross
            if (shape == ConduitPipeShape.Tee || shape == ConduitPipeShape.Cross)
            {
                FillRect(tex, 5, 5, 10, 10, Ink);
                FillRect(tex, 6, 6, 9, 9, BrassHi);
                FillRect(tex, 7, 7, 8, 8, PureHi);
            }
            return tex;
        }

        private static void DrawConduitArm(Texture2D tex, bool n, bool e, bool s, bool w, int halfMinus, int halfPlus, Color32 color)
        {
            int min = 7 - halfMinus;
            int max = 8 + halfMinus;
            FillRect(tex, min, min, max, max, color);
            if (n) FillRect(tex, min, 7, max, 15, color);
            if (s) FillRect(tex, min, 0, max, 8, color);
            if (e) FillRect(tex, 7, min, 15, max, color);
            if (w) FillRect(tex, 0, min, 8, max, color);
            _ = halfPlus;
        }

        private static Texture2D BuildMirrorTrackTexture()
        {
            var tex = NewTex(32, 8);
            FillRect(tex, 0, 1, 31, 6, Ink);
            FillRect(tex, 1, 2, 30, 5, IronDark);
            FillRect(tex, 1, 4, 30, 4, BrassDark);
            for (int x = 3; x <= 28; x += 5)
            {
                SetPx(tex, x, 3, IronLight);
                SetPx(tex, x, 5, BrassMid);
            }
            // Brass rail stops
            FillRect(tex, 1, 1, 2, 6, BrassMid);
            FillRect(tex, 29, 1, 30, 6, BrassMid);
            return tex;
        }

        private static Texture2D BuildMirrorBladeTexture()
        {
            var tex = NewTex(16, 16);
            // Diagonal brass mount & silvered mirror face
            FillRect(tex, 6, 1, 9, 14, Ink);
            FillRect(tex, 7, 2, 8, 13, PureHi);
            FillRect(tex, 6, 2, 6, 13, PureLow);
            FillRect(tex, 9, 2, 9, 13, BrassMid);
            // Top & bottom swivel finials
            FillRect(tex, 5, 0, 10, 2, Ink);
            FillRect(tex, 6, 1, 9, 1, BrassHi);
            FillRect(tex, 5, 13, 10, 15, Ink);
            FillRect(tex, 6, 14, 9, 14, BrassHi);
            return tex;
        }

        private static Texture2D BuildDarknessVeilTexture()
        {
            var tex = NewTex(16, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float wave = Mathf.Sin((x * 0.7f) + (y * 0.45f));
                byte a = (byte)(x == 0 || x == 15 ? 160 : (wave > 0.1f ? 245 : 205));
                byte v = (byte)(wave > 0.35f ? 210 : (wave > -0.2f ? 150 : 105));
                SetPx(tex, x, y, new Color32(v, (byte)(v * 0.85f), (byte)Mathf.Min(255, v + 30), a));
            }
            return tex;
        }

        private static Texture2D BuildBreakawayPlankTexture()
        {
            var tex = NewTex(24, 8);
            FillRect(tex, 0, 1, 23, 6, Ink);
            FillRect(tex, 1, 2, 22, 5, WoodMid);
            FillRect(tex, 1, 5, 22, 5, BrassMid);
            FillRect(tex, 1, 2, 22, 2, WoodDark);
            // Jagged stress crack across center plank
            SetPx(tex, 11, 5, Ink);
            SetPx(tex, 12, 4, Ink);
            SetPx(tex, 11, 3, Ink);
            SetPx(tex, 12, 2, Ink);
            return tex;
        }

        private static Texture2D BuildArchiveWallTileTexture()
        {
            var tex = NewTex(16, 16);
            var fill = new Color32(78, 82, 88, 255);
            var seam = new Color32(66, 70, 76, 255);
            var spec = new Color32(88, 92, 98, 255);
            FillRect(tex, 0, 0, 15, 15, fill);
            // Subtle large concrete block seam
            FillRect(tex, 0, 0, 15, 0, seam);
            FillRect(tex, 0, 0, 0, 15, seam);
            SetPx(tex, 4, 11, spec);
            SetPx(tex, 11, 5, spec);
            return tex;
        }

        private static Texture2D BuildHazardWaterTexture()
        {
            var tex = NewTex(16, 16);
            var deep = new Color32(150, 164, 174, 215);
            var mid = new Color32(182, 198, 208, 225);
            var crest = new Color32(240, 248, 255, 245);
            FillRect(tex, 0, 0, 15, 15, deep);
            FillRect(tex, 0, 11, 15, 14, mid);
            for (int x = 0; x < 16; x++)
            {
                if (x % 4 != 2) SetPx(tex, x, 15, crest);
                if (x % 4 == 1) SetPx(tex, x, 14, crest);
                if (x % 5 == 0) SetPx(tex, x, 8, mid);
                if (x % 5 == 3) SetPx(tex, x, 4, mid);
            }
            return tex;
        }

        private static Texture2D BuildSecretDoorTexture()
        {
            var tex = NewTex(16, 24);
            FillRect(tex, 0, 0, 15, 23, Ink);
            FillRect(tex, 1, 1, 14, 22, StoneDark);
            FillRect(tex, 2, 2, 13, 21, StoneMid);
            FillRect(tex, 3, 10, 12, 13, StoneDark);
            FillRect(tex, 6, 11, 9, 12, BrassMid);
            for (int y = 4; y <= 19; y += 5)
            {
                SetPx(tex, 3, y, StoneLight);
                SetPx(tex, 12, y, StoneLight);
            }
            return tex;
        }

        private static Texture2D BuildSteelBookcaseTexture()
        {
            // 46x58 Maksim-palette desaturated steel 3-shelf archive bookcase
            var tex = NewTex(46, 58);
            var steelDark = new Color32(42, 44, 46, 255);
            var steelMid = new Color32(76, 80, 84, 255);
            var steelHi = new Color32(118, 122, 126, 255);
            var cavity = new Color32(18, 19, 21, 255);
            var bookA = new Color32(138, 140, 142, 255);
            var bookB = new Color32(96, 98, 102, 255);
            var bookC = new Color32(166, 168, 168, 255);

            FillRect(tex, 0, 0, 45, 57, Ink);
            FillRect(tex, 1, 1, 44, 56, steelDark);
            FillRect(tex, 2, 2, 43, 55, steelMid);
            FillRect(tex, 2, 54, 43, 55, steelHi);

            // Three recessed shelf bays (y bottom->top: 4..18, 21..35, 38..52)
            int[] shelfYs = { 4, 21, 38 };
            for (int s = 0; s < shelfYs.Length; s++)
            {
                int y0 = shelfYs[s];
                int y1 = y0 + 13;
                FillRect(tex, 4, y0, 41, y1, Ink);
                FillRect(tex, 5, y0 + 1, 40, y1 - 1, cavity);
                // Glass glare & shelf ledge
                FillRect(tex, 4, y0, 41, y0 + 1, steelHi);
                if (s == 2)
                {
                    // Subtle cracked glass corner in top bay matching reference
                    SetPx(tex, 7, y1 - 2, steelHi); SetPx(tex, 8, y1 - 3, steelHi);
                    SetPx(tex, 9, y1 - 3, steelHi); SetPx(tex, 8, y1 - 4, steelHi);
                    SetPx(tex, 10, y1 - 5, steelHi);
                }

                // Books & archive folders on shelf
                int bx = s == 0 ? 7 : (s == 1 ? 15 : 8);
                int count = s == 2 ? 7 : 6;
                for (int b = 0; b < count; b++)
                {
                    int bw = (b % 3 == 1) ? 3 : 2;
                    int bh = 7 + ((b * 3 + s * 2) % 5);
                    if (bx + bw >= 39) break;
                    Color32 col = b % 3 == 0 ? bookA : (b % 3 == 1 ? bookC : bookB);
                    FillRect(tex, bx, y0 + 2, bx + bw - 1, y0 + 1 + bh, col);
                    FillRect(tex, bx, y0 + 2, bx, y0 + 1 + bh, steelDark);
                    bx += bw + (b == 2 ? 4 : 1);
                }
            }
            return tex;
        }

        private static Texture2D BuildNoticeBoardTexture()
        {
            var tex = NewTex(36, 26);
            var frame = new Color32(82, 85, 88, 255);
            var backing = new Color32(148, 150, 152, 255);
            var paper = new Color32(216, 214, 206, 255);
            var line = new Color32(72, 74, 78, 255);
            FillRect(tex, 0, 0, 35, 25, Ink);
            FillRect(tex, 1, 1, 34, 24, frame);
            FillRect(tex, 3, 3, 32, 22, Ink);
            FillRect(tex, 4, 4, 31, 21, backing);
            // Pinned documents
            FillRect(tex, 6, 11, 12, 19, paper);
            FillRect(tex, 14, 13, 21, 20, paper);
            FillRect(tex, 23, 10, 29, 18, paper);
            FillRect(tex, 9, 5, 16, 10, paper);
            FillRect(tex, 19, 5, 26, 11, paper);
            FillRect(tex, 7, 16, 11, 16, line);
            FillRect(tex, 15, 17, 20, 17, line);
            FillRect(tex, 24, 14, 28, 14, line);
            return tex;
        }

        private static Texture2D BuildWallClockTexture()
        {
            var tex = NewTex(14, 14);
            var face = new Color32(186, 188, 184, 255);
            var rim = new Color32(72, 74, 78, 255);
            float cx = 6.5f, cy = 6.5f;
            for (int y = 0; y < 14; y++)
            for (int x = 0; x < 14; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > 6.6f) continue;
                if (d >= 5.6f) SetPx(tex, x, y, Ink);
                else if (d >= 4.7f) SetPx(tex, x, y, rim);
                else SetPx(tex, x, y, face);
            }
            SetPx(tex, 6, 6, Ink);
            SetPx(tex, 6, 8, Ink); SetPx(tex, 6, 9, Ink);
            SetPx(tex, 8, 6, Ink); SetPx(tex, 9, 7, Ink);
            return tex;
        }

        private static Texture2D BuildRoomSignPlateTexture()
        {
            var tex = NewTex(20, 6);
            var plate = new Color32(68, 70, 74, 255);
            var text = new Color32(178, 180, 176, 255);
            FillRect(tex, 0, 0, 19, 5, Ink);
            FillRect(tex, 1, 1, 18, 4, plate);
            // Tiny glyph pixels for "LE401"
            for (int x = 3; x <= 16; x += 3)
            {
                FillRect(tex, x, 2, x + 1, 3, text);
            }
            return tex;
        }

        private static Texture2D BuildPlasterWallPatchTexture()
        {
            // 320x128 crisp 32-PPU concrete plaster patch (10x4 units at 0.5 scale) with organic torn right edge & diagonal crack
            const int w = 320, h = 128;
            var tex = NewTex(w, h);
            var concrete = new Color32(114, 118, 120, 255);
            var mid = new Color32(94, 98, 100, 255);
            var darkStain = new Color32(76, 80, 82, 255);
            var edgeInk = new Color32(28, 30, 32, 255);

            for (int y = 0; y < h; y++)
            {
                int cutX = 278 + (int)(Mathf.Sin(y * 0.08f) * 20f + Mathf.Cos(y * 0.21f) * 11f);
                if (y > 92) cutX -= (y - 92) / 2;
                for (int x = 0; x < w && x <= cutX; x++)
                {
                    if (x >= cutX - 1)
                    {
                        SetPx(tex, x, y, edgeInk);
                        continue;
                    }
                    uint hash = (uint)(x * 73856093 ^ y * 19349663);
                    bool speck = (hash % 23u) == 0u;
                    bool topDamp = y > 108 && ((x + (int)(hash % 7u)) % 11 < 3);
                    SetPx(tex, x, y, topDamp ? darkStain : (speck ? mid : concrete));
                }
            }

            // Crisp 1-pixel diagonal hairline stress crack (matching 32-PPU prop line weight)
            for (int i = 0; i < 118; i++)
            {
                int cx = 36 + i;
                int cy = 96 - (i / 3) + ((i % 13 == 0) ? 1 : 0);
                if (cx < 260) SetPx(tex, cx, cy, edgeInk);
            }
            // Secondary small crack lower right
            for (int i = 0; i < 48; i++)
            {
                int cx = 175 + i;
                int cy = 54 + (i / 4) - ((i % 9 == 0) ? 1 : 0);
                if (cx < 265) SetPx(tex, cx, cy, edgeInk);
            }
            return tex;
        }

        private static Texture2D BuildRebarBeamEndTexture()
        {
            var tex = NewTex(16, 12);
            var metal = new Color32(74, 76, 80, 255);
            FillRect(tex, 0, 7, 13, 9, Ink);
            FillRect(tex, 0, 8, 11, 8, metal);
            FillRect(tex, 0, 2, 15, 4, Ink);
            FillRect(tex, 0, 3, 13, 3, metal);
            SetPx(tex, 13, 10, Ink); SetPx(tex, 14, 1, Ink);
            return tex;
        }
    }
}
