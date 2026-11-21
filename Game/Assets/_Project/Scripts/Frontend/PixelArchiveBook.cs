using UnityEngine;

namespace TheLightenRoad.Frontend
{
    /// <summary>
    /// Handcrafted 2D pixel-art sprite library for the Field Archive Codex.
    /// Every book frame, page-turn overlay, bookmark tab, and pixel UI trim is
    /// authored on an integer pixel grid with point filtering (no 3D meshes or blur).
    /// </summary>
    public static class PixelArchiveBook
    {
        public const int CanvasWidth = 320;
        public const int CanvasHeight = 216;
        public const int FrameCount = 6;

        // Palette: Dark Archive Leather, Antique Brass & Warm Amber Lantern, Aged Parchment
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(20, 15, 18, 255);
        private static readonly Color32 ShadowSoft = new Color32(10, 7, 10, 115);
        private static readonly Color32 ShadowDeep = new Color32(12, 8, 10, 170);

        private static readonly Color32 LeatherDark = new Color32(34, 24, 24, 255);
        private static readonly Color32 LeatherMid = new Color32(50, 36, 33, 255);
        private static readonly Color32 LeatherLight = new Color32(68, 49, 43, 255);
        private static readonly Color32 LeatherHi = new Color32(84, 61, 52, 255);

        private static readonly Color32 BrassDark = new Color32(106, 73, 35, 255);
        private static readonly Color32 BrassMid = new Color32(166, 118, 52, 255);
        private static readonly Color32 BrassBright = new Color32(226, 169, 76, 255);
        private static readonly Color32 AmberGlow = new Color32(255, 219, 134, 255);
        private static readonly Color32 AmberCore = new Color32(255, 243, 196, 255);

        private static readonly Color32 PaperHi = new Color32(236, 219, 184, 255);
        private static readonly Color32 PaperBase = new Color32(221, 199, 160, 255);
        private static readonly Color32 PaperWarm = new Color32(206, 182, 139, 255);
        private static readonly Color32 PaperShade = new Color32(179, 151, 108, 255);
        private static readonly Color32 PaperDeep = new Color32(138, 110, 74, 255);
        private static readonly Color32 GutterDark = new Color32(84, 62, 41, 255);

        private static readonly Color32 RibbonDark = new Color32(94, 28, 28, 255);
        private static readonly Color32 RibbonMid = new Color32(148, 46, 40, 255);
        private static readonly Color32 RibbonHi = new Color32(194, 72, 56, 255);

        private static Sprite[] openingFrames;
        private static Sprite[] pageTurnFrames;
        private static Sprite bookmarkTab;
        private static Sprite pixelButton;
        private static Sprite pixelBarFrame;
        private static Sprite pixelRule;
        private static Sprite lanternEmblem;
        private static Sprite vignetteBackdrop;
        private static Sprite lanternGlow;
        private static Sprite dustPixel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            openingFrames = null;
            pageTurnFrames = null;
            bookmarkTab = null;
            pixelButton = null;
            pixelBarFrame = null;
            pixelRule = null;
            lanternEmblem = null;
            vignetteBackdrop = null;
            lanternGlow = null;
            dustPixel = null;
        }

        private static bool IsValid(Sprite s) => s != null && s.texture != null;
        private static bool IsValidArray(Sprite[] arr)
        {
            if (arr == null || arr.Length != FrameCount) return false;
            for (int i = 0; i < arr.Length; i++)
                if (!IsValid(arr[i])) return false;
            return true;
        }

        public static Sprite[] OpeningFrames => IsValidArray(openingFrames) ? openingFrames : (openingFrames = BuildOpeningFrames());
        public static Sprite[] PageTurnFrames => IsValidArray(pageTurnFrames) ? pageTurnFrames : (pageTurnFrames = BuildPageTurnFrames());
        public static Sprite OpenSpread => OpeningFrames[FrameCount - 1];
        public static Sprite ClosedCover => OpeningFrames[0];
        public static Sprite BookmarkTab => IsValid(bookmarkTab) ? bookmarkTab : (bookmarkTab = BuildBookmarkTab());
        public static Sprite PixelButton => IsValid(pixelButton) ? pixelButton : (pixelButton = BuildPixelButton());
        public static Sprite PixelBarFrame => IsValid(pixelBarFrame) ? pixelBarFrame : (pixelBarFrame = BuildPixelBarFrame());
        public static Sprite PixelRule => IsValid(pixelRule) ? pixelRule : (pixelRule = BuildPixelRule());
        public static Sprite LanternEmblem => IsValid(lanternEmblem) ? lanternEmblem : (lanternEmblem = BuildLanternEmblem());
        public static Sprite VignetteBackdrop => IsValid(vignetteBackdrop) ? vignetteBackdrop : (vignetteBackdrop = BuildVignetteBackdrop());
        public static Sprite LanternGlow => IsValid(lanternGlow) ? lanternGlow : (lanternGlow = BuildLanternGlow());
        public static Sprite DustPixel => IsValid(dustPixel) ? dustPixel : (dustPixel = BuildDustPixel());

        // Muted archival tab colors so each discovered record has an unmistakable bookmark identity.
        public static readonly Color[] BookmarkPalette =
        {
            new Color32(214, 148, 62, 255),  // Lantern Amber
            new Color32(168, 62, 54, 255),   // Wax Crimson
            new Color32(92, 128, 152, 255),  // Slate Archive
            new Color32(106, 138, 92, 255),  // Moss Ledger
            new Color32(174, 134, 84, 255),  // Ochre Folder
            new Color32(138, 92, 128, 255)   // Dusk Plum
        };

        public static Color GetBookmarkColor(int index)
        {
            return BookmarkPalette[Mathf.Abs(index) % BookmarkPalette.Length];
        }

        // =====================================================================
        // OPENING SEQUENCE (6 HANDCRAFTED PIXEL FRAMES: CLOSED -> OPEN SPREAD)
        // =====================================================================

        private static Sprite[] BuildOpeningFrames()
        {
            var frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = BuildOpeningFrame(i);
            }
            return frames;
        }

        private static Sprite BuildOpeningFrame(int frame)
        {
            var px = new Color32[CanvasWidth * CanvasHeight];
            int cx = CanvasWidth / 2; // 160

            if (frame == FrameCount - 1)
            {
                DrawOpenSpread(px);
                return MakeSprite("ArchiveBook_Open", px, CanvasWidth, CanvasHeight);
            }

            // Cover unfold parameters per frame (0 = closed, 1..4 = lifting & unfolding)
            // Closed book sits with spine at x=156 and front cover spanning x=156..304.
            // During frames 1..4, the right base stays anchored while the front cover swings left.
            int coverTop = 200;
            int coverBot = 20;
            int spineX = 156;
            int rightEdge = 302;

            // Cast drop shadow on the surface behind/under the book
            if (frame == 0)
            {
                FillRect(px, spineX - 2, coverBot - 6, rightEdge + 5, coverBot + 2, ShadowSoft);
                FillRect(px, rightEdge, coverBot - 4, rightEdge + 5, coverTop - 4, ShadowSoft);
                // Hanging crimson ribbon peeking below the closed cover
                DrawRibbonTail(px, 246, coverBot - 15, coverBot + 1);
                // Thick stacked page block visible along right & bottom edges of closed book
                DrawClosedFrontCover(px, spineX, coverBot, rightEdge, coverTop);
                return MakeSprite("ArchiveBook_Open_0", px, CanvasWidth, CanvasHeight);
            }

            // Frames 1..4: right half of the open book is already resting in place underneath
            int leftSpreadReach = frame == 1 ? 0 : frame == 2 ? 24 : frame == 3 ? 86 : 132;
            int leftCoverOuter = spineX - leftSpreadReach;

            // Drop shadow under visible base
            FillRect(px, Mathf.Min(spineX - 4, leftCoverOuter - 2), coverBot - 6, rightEdge + 4, coverBot + 2, ShadowSoft);

            // Right resting back cover + right page stack + right parchment sheet
            DrawRightBookHalf(px, spineX, coverBot, rightEdge, coverTop);

            if (frame == 1)
            {
                // Cover cracks open ~20%: front cover shifts slightly left & lifts with bright rim
                int frontRight = rightEdge - 10;
                int frontTopLift = 4;
                FillRect(px, spineX + 2, coverBot + 2, frontRight + 8, coverTop - 2, ShadowDeep);
                DrawForeshortenedCover(px, spineX - 2, coverBot + 1, frontRight, coverTop + frontTopLift, isFrontFace: true, tiltLift: 5);
                DrawRibbonTail(px, 240, coverBot - 14, coverBot + 1);
            }
            else if (frame == 2)
            {
                // Cover stands ~45° upright on the right side of the spine, casting wide shadow on right page
                int frontRight = spineX + 76;
                DrawCastShadowGradient(px, spineX + 2, coverBot + 8, spineX + 106, coverTop - 8, 125);
                DrawForeshortenedCover(px, spineX - 4, coverBot + 2, frontRight, coverTop + 9, isFrontFace: true, tiltLift: 10);
                DrawRibbonTail(px, spineX + 6, coverBot - 14, coverBot + 2);
            }
            else if (frame == 3)
            {
                // Cover has swung past center (~70% open): inside of left cover + unfolding left page visible
                DrawLeftBookHalfPartial(px, leftCoverOuter, spineX, coverBot, coverTop, liftPixels: 8);
                DrawCastShadowGradient(px, spineX - 42, coverBot + 8, spineX + 24, coverTop - 8, 90);
                DrawSpineDetail(px, spineX, coverBot, coverTop);
                DrawRibbonTail(px, spineX - 10, coverBot - 15, coverBot + 3);
            }
            else if (frame == 4)
            {
                // Nearly flat (~92% open): both pages almost settled, light gutter shadow
                DrawLeftBookHalfPartial(px, leftCoverOuter, spineX, coverBot, coverTop, liftPixels: 3);
                DrawCastShadowGradient(px, spineX - 18, coverBot + 8, spineX + 16, coverTop - 8, 55);
                DrawSpineDetail(px, spineX, coverBot, coverTop);
                DrawRibbonTail(px, spineX - 12, coverBot - 15, coverBot + 4);
            }

            return MakeSprite("ArchiveBook_Open_" + frame, px, CanvasWidth, CanvasHeight);
        }

        private static void DrawClosedFrontCover(Color32[] px, int x0, int y0, int x1, int y1)
        {
            // Exposed paper block peeking on right & bottom (adds rich book thickness)
            FillRect(px, x1 - 4, y0 + 2, x1 + 2, y1 - 3, Outline);
            for (int y = y0 + 3; y < y1 - 4; y++)
            {
                Color32 edge = (y % 2 == 0) ? PaperWarm : PaperShade;
                FillRect(px, x1 - 3, y, x1 + 1, y + 1, edge);
            }

            // Main leather front cover rectangle (rounded 2px corners)
            int coverRight = x1 - 4;
            DrawLeatherPanel(px, x0, y0, coverRight, y1, hasSpineBands: true);

            // Embossed inner gold stitched frame
            DrawStitchedBrassBorder(px, x0 + 18, y0 + 12, coverRight - 10, y1 - 12);

            // Heavy antique brass corner clasps
            DrawBrassCorner(px, x0 + 14, y1 - 10, rightSide: false, topSide: true);
            DrawBrassCorner(px, coverRight - 10, y1 - 10, rightSide: true, topSide: true);
            DrawBrassCorner(px, x0 + 14, y0 + 10, rightSide: false, topSide: false);
            DrawBrassCorner(px, coverRight - 10, y0 + 10, rightSide: true, topSide: false);

            // Central Amber Lantern Medallion
            int mx = (x0 + 14 + coverRight) / 2;
            int my = (y0 + y1) / 2 + 4;
            DrawCoverLanternMedallion(px, mx, my);

            // Subtle embossed title bars under the medallion
            FillRect(px, mx - 24, my - 38, mx + 24, my - 36, BrassMid);
            FillRect(px, mx - 16, my - 43, mx + 16, my - 42, BrassDark);
            SetPx(px, mx, my - 43, BrassBright);
        }

        private static void DrawForeshortenedCover(Color32[] px, int x0, int y0, int x1, int y1, bool isFrontFace, int tiltLift)
        {
            int w = Mathf.Max(14, x1 - x0);
            for (int x = x0; x <= x1; x++)
            {
                float u = (x - x0) / (float)w;
                int lift = Mathf.RoundToInt(u * tiltLift);
                int colBot = y0 + lift;
                int colTop = y1 - Mathf.RoundToInt((1f - u) * 2f) + lift / 2;

                for (int y = colBot; y <= colTop; y++)
                {
                    bool border = x == x0 || x == x1 || y == colBot || y == colTop;
                    if (border)
                    {
                        SetPx(px, x, y, Outline);
                        continue;
                    }
                    bool rim = x == x0 + 1 || x == x1 - 1 || y == colBot + 1 || y == colTop - 1;
                    if (rim)
                    {
                        SetPx(px, x, y, x == x1 - 1 ? BrassBright : LeatherHi);
                        continue;
                    }
                    int n = Hash(x, y);
                    Color32 col = isFrontFace
                        ? (u > 0.75f ? LeatherLight : (n < 4 ? LeatherLight : n < 10 ? LeatherDark : LeatherMid))
                        : LeatherDark;
                    SetPx(px, x, y, col);
                }
            }

            // Foreshortened brass bands and glowing medallion sliver
            int mx = (x0 + x1) / 2;
            int my = (y0 + y1) / 2 + tiltLift / 2;
            int mw = Mathf.Max(6, w / 5);
            FillRect(px, mx - mw, my - 16, mx + mw, my + 16, Outline);
            FillRect(px, mx - mw + 1, my - 15, mx + mw - 1, my + 15, BrassMid);
            FillRect(px, mx - mw + 3, my - 11, mx + mw - 3, my + 11, LeatherDark);
            FillRect(px, mx - Mathf.Max(1, mw - 5), my - 6, mx + Mathf.Max(1, mw - 5), my + 6, BrassBright);
            FillRect(px, mx - 1, my - 3, mx + 1, my + 3, AmberGlow);
        }

        private static void DrawOpenSpread(Color32[] px)
        {
            int leftCover = 18;
            int rightCover = 302;
            int botCover = 20;
            int topCover = 200;
            int spineX = 160;

            // Soft drop shadow beneath the open book
            FillRect(px, leftCover - 2, botCover - 6, rightCover + 4, botCover + 1, ShadowSoft);
            FillRect(px, leftCover - 3, botCover - 3, leftCover, topCover - 5, ShadowSoft);
            FillRect(px, rightCover, botCover - 3, rightCover + 4, topCover - 5, ShadowSoft);

            // Outer dark weathered leather binding
            DrawLeatherPanel(px, leftCover, botCover, rightCover, topCover, hasSpineBands: false);

            // Brass corner brackets on all 4 outer cover corners
            DrawBrassCorner(px, leftCover + 3, topCover - 3, rightSide: false, topSide: true);
            DrawBrassCorner(px, rightCover - 3, topCover - 3, rightSide: true, topSide: true);
            DrawBrassCorner(px, leftCover + 3, botCover + 3, rightSide: false, topSide: false);
            DrawBrassCorner(px, rightCover - 3, botCover + 3, rightSide: true, topSide: false);

            // Stacked parchment page edges on left & right (4 stepped layers for 2D depth)
            DrawPageStackEdge(px, leftSide: true, xOuter: 24, xInner: 32, yBot: 26, yTop: 194);
            DrawPageStackEdge(px, leftSide: false, xOuter: 296, xInner: 288, yBot: 26, yTop: 194);

            // Bottom page-thickness bevel under both pages
            FillRect(px, 26, 23, 294, 28, Outline);
            FillRect(px, 27, 24, 158, 27, PaperShade);
            FillRect(px, 162, 24, 293, 27, PaperShade);
            FillRect(px, 28, 26, 158, 27, PaperWarm);
            FillRect(px, 162, 26, 292, 27, PaperWarm);

            // Left & Right readable parchment spreads
            DrawParchmentPage(px, 32, 28, 156, 192, isLeftPage: true);
            DrawParchmentPage(px, 164, 28, 288, 192, isLeftPage: false);

            // Central stitched spine gutter
            DrawSpineDetail(px, spineX - 4, botCover, topCover);

            // Woven crimson bookmark ribbon tucked into the top gutter & peeking below the bottom cover
            DrawSpineRibbon(px, spineX - 10, 26, 194);
            DrawRibbonTail(px, spineX - 11, 4, 24);
        }

        private static void DrawRightBookHalf(Color32[] px, int spineX, int botCover, int rightCover, int topCover)
        {
            DrawLeatherPanel(px, spineX - 4, botCover, rightCover, topCover, hasSpineBands: false);
            DrawBrassCorner(px, rightCover - 3, topCover - 3, rightSide: true, topSide: true);
            DrawBrassCorner(px, rightCover - 3, botCover + 3, rightSide: true, topSide: false);
            DrawPageStackEdge(px, leftSide: false, xOuter: 296, xInner: 288, yBot: 26, yTop: 194);
            FillRect(px, spineX, 23, 294, 28, Outline);
            FillRect(px, spineX + 2, 24, 293, 27, PaperShade);
            DrawParchmentPage(px, spineX + 8, 28, 288, 192, isLeftPage: false);
        }

        private static void DrawLeftBookHalfPartial(Color32[] px, int leftOuter, int spineX, int botCover, int topCover, int liftPixels)
        {
            int x0 = Mathf.Max(18, leftOuter);
            int width = Mathf.Max(12, spineX - x0);
            for (int x = x0; x <= spineX; x++)
            {
                float u = 1f - (x - x0) / (float)width; // 0 at outer left edge, 1 at spine
                int lift = Mathf.RoundToInt((1f - u) * liftPixels);
                int y0 = botCover + lift;
                int y1 = topCover + lift / 2;

                for (int y = y0; y <= y1; y++)
                {
                    bool outer = x == x0 || y == y0 || y == y1;
                    if (outer)
                    {
                        SetPx(px, x, y, Outline);
                        continue;
                    }
                    bool inPaper = x >= x0 + 8 && y >= y0 + 8 && y <= y1 - 8;
                    if (inPaper)
                    {
                        int distSpine = spineX - x;
                        Color32 p = distSpine < 4 ? PaperDeep : distSpine < 10 ? PaperShade : distSpine < 16 ? PaperWarm : PaperBase;
                        SetPx(px, x, y, p);
                    }
                    else
                    {
                        SetPx(px, x, y, (x == x0 + 1 || y == y1 - 1) ? LeatherLight : LeatherMid);
                    }
                }
            }
            if (width > 40)
            {
                DrawBrassCorner(px, x0 + 3, topCover + liftPixels / 2 - 3, rightSide: false, topSide: true);
                DrawBrassCorner(px, x0 + 3, botCover + liftPixels + 3, rightSide: false, topSide: false);
            }
        }

        private static void DrawLeatherPanel(Color32[] px, int x0, int y0, int x1, int y1, bool hasSpineBands)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    // Trim 2px rounded corners
                    int dx = Mathf.Min(x - x0, x1 - x);
                    int dy = Mathf.Min(y - y0, y1 - y);
                    if (dx + dy < 2) continue;

                    bool border = x == x0 || x == x1 || y == y0 || y == y1 || (dx + dy == 2);
                    if (border)
                    {
                        SetPx(px, x, y, Outline);
                        continue;
                    }

                    bool bevelTop = y == y1 - 1 || x == x0 + 1;
                    bool bevelBot = y == y0 + 1 || x == x1 - 1;
                    if (bevelTop)
                    {
                        SetPx(px, x, y, LeatherHi);
                        continue;
                    }
                    if (bevelBot)
                    {
                        SetPx(px, x, y, LeatherDark);
                        continue;
                    }

                    int n = Hash(x, y);
                    Color32 baseLeather = n < 3 ? LeatherLight : n < 9 ? LeatherDark : LeatherMid;
                    SetPx(px, x, y, baseLeather);
                }
            }

            if (hasSpineBands)
            {
                // Raised leather spine ridge on the left of closed cover
                FillRect(px, x0 + 11, y0 + 3, x0 + 13, y1 - 3, Outline);
                FillRect(px, x0 + 9, y0 + 3, x0 + 11, y1 - 3, LeatherDark);
                FillRect(px, x0 + 4, y0 + 3, x0 + 6, y1 - 3, LeatherLight);

                // Horizontal brass spine ribs
                for (int y = y0 + 20; y <= y1 - 20; y += 28)
                {
                    FillRect(px, x0 + 2, y, x0 + 12, y + 4, BrassDark);
                    FillRect(px, x0 + 3, y + 2, x0 + 11, y + 3, BrassBright);
                    FillRect(px, x0 + 2, y - 1, x0 + 12, y, Outline);
                }
            }
        }

        private static void DrawStitchedBrassBorder(Color32[] px, int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
            {
                if ((x - x0) % 4 < 2)
                {
                    SetPx(px, x, y0, BrassMid);
                    SetPx(px, x, y1, BrassMid);
                }
            }
            for (int y = y0; y <= y1; y++)
            {
                if ((y - y0) % 4 < 2)
                {
                    SetPx(px, x0, y, BrassMid);
                    SetPx(px, x1, y, BrassMid);
                }
            }
        }

        private static void DrawBrassCorner(Color32[] px, int cx, int cy, bool rightSide, bool topSide)
        {
            int sx = rightSide ? -1 : 1;
            int sy = topSide ? -1 : 1;

            // L-shaped ornate brass clasp (12x12 footprint)
            for (int i = 0; i < 12; i++)
            {
                for (int t = 0; t < 4; t++)
                {
                    SetPx(px, cx + sx * i, cy + sy * t, t == 3 ? Outline : (t == 1 ? BrassBright : BrassMid));
                    SetPx(px, cx + sx * t, cy + sy * i, t == 3 ? Outline : (t == 1 ? BrassBright : BrassMid));
                }
            }
            // Corner rivet dot
            SetPx(px, cx + sx * 2, cy + sy * 2, AmberGlow);
            SetPx(px, cx + sx * 9, cy + sy * 1, AmberGlow);
            SetPx(px, cx + sx * 1, cy + sy * 9, AmberGlow);
        }

        private static void DrawCoverLanternMedallion(Color32[] px, int mx, int my)
        {
            // Outer octagonal brass frame
            for (int dy = -26; dy <= 26; dy++)
            {
                for (int dx = -20; dx <= 20; dx++)
                {
                    int manh = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (manh > 34) continue;
                    bool edge = Mathf.Abs(dx) == 20 || Mathf.Abs(dy) == 26 || manh == 34;
                    bool brassRim = Mathf.Abs(dx) >= 17 || Mathf.Abs(dy) >= 23 || manh >= 31;
                    if (edge) SetPx(px, mx + dx, my + dy, Outline);
                    else if (brassRim) SetPx(px, mx + dx, my + dy, (dy > 0 && dx < 6) ? BrassBright : BrassMid);
                    else SetPx(px, mx + dx, my + dy, LeatherDark);
                }
            }

            // Pixel Lantern silhouette inside the medallion
            // Top handle ring
            FillRect(px, mx - 4, my + 14, mx + 5, my + 16, BrassBright);
            FillRect(px, mx - 5, my + 11, mx - 3, my + 14, BrassMid);
            FillRect(px, mx + 4, my + 11, mx + 6, my + 14, BrassMid);

            // Lantern roof cap
            FillRect(px, mx - 9, my + 8, mx + 10, my + 11, BrassMid);
            FillRect(px, mx - 7, my + 10, mx + 8, my + 11, BrassBright);

            // Lantern glass chamber with stepped warm amber ember glow
            FillRect(px, mx - 8, my - 10, mx + 9, my + 8, BrassDark);
            FillRect(px, mx - 6, my - 9, mx + 7, my + 7, new Color32(150, 82, 24, 255));
            FillRect(px, mx - 4, my - 7, mx + 5, my + 5, BrassBright);
            FillRect(px, mx - 2, my - 4, mx + 3, my + 3, AmberGlow);
            FillRect(px, mx - 1, my - 2, mx + 2, my + 1, AmberCore);

            // Vertical lantern cage bars
            FillRect(px, mx - 7, my - 10, mx - 5, my + 8, BrassMid);
            FillRect(px, mx + 6, my - 10, mx + 8, my + 8, BrassMid);

            // Lantern base pedestal
            FillRect(px, mx - 10, my - 13, mx + 11, my - 10, BrassMid);
            FillRect(px, mx - 8, my - 12, mx + 9, my - 11, BrassBright);
            FillRect(px, mx - 6, my - 16, mx + 7, my - 13, BrassDark);
        }

        private static void DrawPageStackEdge(Color32[] px, bool leftSide, int xOuter, int xInner, int yBot, int yTop)
        {
            int minX = Mathf.Min(xOuter, xInner);
            int maxX = Mathf.Max(xOuter, xInner);
            for (int x = minX; x <= maxX; x++)
            {
                int depth = leftSide ? (x - xOuter) : (xOuter - x);
                int y0 = yBot + (depth == 0 ? 1 : 0);
                int y1 = yTop - (depth == 0 ? 1 : 0);
                for (int y = y0; y <= y1; y++)
                {
                    if (depth == 0 || y == y0 || y == y1)
                    {
                        SetPx(px, x, y, Outline);
                        continue;
                    }
                    // Alternating crisp 1px parchment leaf stripes
                    Color32 leaf = (depth % 2 == 1) ? PaperShade : PaperWarm;
                    if (y == y1 - 1) leaf = PaperHi;
                    if (y == y0 + 1) leaf = PaperDeep;
                    SetPx(px, x, y, leaf);
                }
            }
        }

        private static void DrawParchmentPage(Color32[] px, int x0, int y0, int x1, int y1, bool isLeftPage)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    bool outerBorder = (isLeftPage && x == x0) || (!isLeftPage && x == x1) || y == y0 || y == y1;
                    if (outerBorder)
                    {
                        SetPx(px, x, y, PaperDeep);
                        continue;
                    }

                    int distGutter = isLeftPage ? (x1 - x) : (x - x0);
                    int distOuter = isLeftPage ? (x - x0) : (x1 - x);
                    int distVert = Mathf.Min(y - y0, y1 - y);

                    Color32 col = PaperBase;

                    // Soft curved crease shading toward the central spine gutter
                    if (distGutter < 2) col = GutterDark;
                    else if (distGutter < 5) col = PaperDeep;
                    else if (distGutter < 10) col = PaperShade;
                    else if (distGutter < 18) col = PaperWarm;
                    else if (distOuter < 2 || distVert < 2) col = PaperWarm;
                    else if (distOuter < 6 && y > y1 - 24) col = PaperHi; // warm top corner light catch
                    else
                    {
                        // Subtle aged paper texture strictly along margins (keeps reading center clean!)
                        bool inMargin = distOuter < 11 || distVert < 9;
                        if (inMargin && Hash(x, y) < 3) col = PaperWarm;
                    }

                    SetPx(px, x, y, col);
                }
            }

            // Delicate inner archival border corners (strictly in corners, away from text!)
            int bx0 = x0 + 6;
            int bx1 = x1 - 6;
            int by0 = y0 + 6;
            int by1 = y1 - 6;
            for (int i = 0; i < 6; i++)
            {
                // Top outer corner L
                int ox = isLeftPage ? bx0 + i : bx1 - i;
                SetPx(px, ox, by1, PaperShade);
                SetPx(px, isLeftPage ? bx0 : bx1, by1 - i, PaperShade);
                // Bottom outer corner L
                SetPx(px, ox, by0, PaperShade);
                SetPx(px, isLeftPage ? bx0 : bx1, by0 + i, PaperShade);
            }
        }

        private static void DrawSpineDetail(Color32[] px, int spineX, int yBot, int yTop)
        {
            // Deep gutter groove between left and right pages
            FillRect(px, spineX + 1, yBot + 4, spineX + 7, yTop - 4, Outline);
            FillRect(px, spineX + 2, yBot + 6, spineX + 6, yTop - 6, GutterDark);
            FillRect(px, spineX + 3, yBot + 8, spineX + 5, yTop - 8, LeatherDark);

            // Spine cap brass studs at top & bottom of binding
            FillRect(px, spineX, yTop - 5, spineX + 8, yTop - 1, BrassDark);
            FillRect(px, spineX + 1, yTop - 4, spineX + 7, yTop - 2, BrassMid);
            FillRect(px, spineX, yBot + 1, spineX + 8, yBot + 5, BrassDark);
            FillRect(px, spineX + 1, yBot + 2, spineX + 7, yBot + 4, BrassMid);

            // 3 stitched archival binding cords across the gutter
            foreach (int sy in new[] { 62, 110, 158 })
            {
                FillRect(px, spineX + 1, sy, spineX + 7, sy + 2, PaperShade);
                FillRect(px, spineX + 2, sy + 1, spineX + 6, sy + 2, PaperHi);
            }
        }

        private static void DrawSpineRibbon(Color32[] px, int rx, int yBot, int yTop)
        {
            // Subtle woven crimson bookmark resting alongside the gutter
            for (int y = yBot; y <= yTop; y++)
            {
                int sway = (y < 46) ? -1 : 0;
                SetPx(px, rx + sway, y, RibbonDark);
                SetPx(px, rx + 1 + sway, y, (y % 4 == 0) ? RibbonHi : RibbonMid);
                SetPx(px, rx + 2 + sway, y, RibbonDark);
            }
        }

        private static void DrawRibbonTail(Color32[] px, int rx, int yBot, int yTop)
        {
            // Forked swallowtail crimson bookmark peeking out beneath the bottom cover
            for (int y = yBot; y <= yTop; y++)
            {
                int dy = y - yBot;
                for (int dx = 0; dx < 7; dx++)
                {
                    // Fork notch at bottom center
                    if (dy < 4 && dx >= 3 - (3 - dy) / 2 && dx <= 3 + (3 - dy) / 2) continue;
                    bool edge = dx == 0 || dx == 6 || dy == 0;
                    Color32 col = edge ? Outline : dx == 2 ? RibbonHi : dx == 5 ? RibbonDark : RibbonMid;
                    SetPx(px, rx + dx, y, col);
                }
            }
        }

        private static void DrawCastShadowGradient(Color32[] px, int x0, int y0, int x1, int y1, byte maxAlpha)
        {
            int w = Mathf.Max(1, x1 - x0);
            for (int x = x0; x <= x1; x++)
            {
                float t = 1f - Mathf.Abs((x - x0) / (float)w * 2f - 1f);
                byte a = (byte)Mathf.RoundToInt(maxAlpha * t);
                var shadow = new Color32(14, 9, 8, a);
                for (int y = y0; y <= y1; y++)
                {
                    BlendPx(px, x, y, shadow);
                }
            }
        }

        // =====================================================================
        // PAGE TURN OVERLAY (6 HANDCRAFTED PIXEL FRAMES, RIGHT -> LEFT SWEEP)
        // =====================================================================

        private static Sprite[] BuildPageTurnFrames()
        {
            var frames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                frames[i] = BuildPageTurnFrame(i);
            }
            return frames;
        }

        private static Sprite BuildPageTurnFrame(int frame)
        {
            var px = new Color32[CanvasWidth * CanvasHeight];
            int spineX = 160;
            int yBot = 28;
            int yTop = 192;

            // Normalized turn stage 0..5 sweeping right page (164..288) -> left page (32..156)
            switch (frame)
            {
                case 0:
                    // Outer bottom-right corner lifts cleanly with crisp diagonal pixel fold
                    DrawCornerLift(px, 288, yBot, foldSize: 42);
                    break;
                case 1:
                    // Sheet peels 25% across right page
                    DrawSweepingSheet(px, spineX, edgeX: 252, curlWidth: 34, yBot, yTop, archLift: 8, shadowRight: 288);
                    break;
                case 2:
                    // Sheet arches up tall just right of the spine
                    DrawSweepingSheet(px, spineX, edgeX: 192, curlWidth: 26, yBot, yTop, archLift: 14, shadowRight: 232);
                    break;
                case 3:
                    // Sheet crosses center spine onto the left page (midpoint swap frame!)
                    DrawSweepingSheet(px, spineX, edgeX: 132, curlWidth: 26, yBot, yTop, archLift: 14, shadowRight: 168);
                    break;
                case 4:
                    // Sheet flattens 75% across the left page
                    DrawSweepingSheet(px, spineX, edgeX: 70, curlWidth: 32, yBot, yTop, archLift: 7, shadowRight: 112);
                    break;
                case 5:
                    // Final gentle settle near left edge
                    DrawLeftSettle(px, 32, yBot, yTop);
                    break;
            }

            return MakeSprite("ArchiveBook_Turn_" + frame, px, CanvasWidth, CanvasHeight);
        }

        private static void DrawCornerLift(Color32[] px, int rightX, int botY, int foldSize)
        {
            for (int dy = 0; dy <= foldSize; dy++)
            {
                for (int dx = 0; dx <= foldSize - dy; dx++)
                {
                    int x = rightX - dx;
                    int y = botY + dy;
                    // Underlying shadow exposed where the corner lifted
                    BlendPx(px, x, y, new Color32(18, 12, 9, 130));
                }
            }

            // Curled parchment triangle folded inward
            for (int dy = 0; dy <= foldSize; dy++)
            {
                for (int dx = 0; dx <= foldSize - dy; dx++)
                {
                    int fx = rightX - foldSize + dx;
                    int fy = botY + foldSize - dy;
                    bool crease = (dx + dy == foldSize);
                    bool edge = dx == 0 || dy == 0;
                    Color32 col = edge ? Outline : crease ? PaperDeep : (dx + dy > foldSize - 6 ? PaperHi : PaperWarm);
                    SetPx(px, fx, fy, col);
                }
            }
        }

        private static void DrawSweepingSheet(
            Color32[] px, int spineX, int edgeX, int curlWidth, int yBot, int yTop, int archLift, int shadowRight)
        {
            // Cast shadow onto underlying spread
            int shMin = Mathf.Min(edgeX, spineX) - 8;
            int shMax = Mathf.Max(shadowRight, edgeX + 12);
            for (int x = shMin; x <= shMax; x++)
            {
                float d = Mathf.Abs(x - edgeX) / (float)Mathf.Max(14, curlWidth + 14);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - d) * 125f);
                if (a < 8) continue;
                var sh = new Color32(16, 10, 8, a);
                for (int y = yBot; y <= yTop; y++)
                {
                    BlendPx(px, x, y, sh);
                }
            }

            // Sweeping parchment leaf strip between spine and moving outer edge
            int minX = Mathf.Min(spineX, edgeX);
            int maxX = Mathf.Max(spineX, edgeX);
            int span = Mathf.Max(1, maxX - minX);

            for (int x = minX; x <= maxX; x++)
            {
                float u = Mathf.Abs(x - spineX) / (float)span; // 0 at spine, 1 at moving edge
                // Subtle arched 2D pixel perspective lift at the moving edge
                int lift = Mathf.RoundToInt(Mathf.Sin(u * Mathf.PI * 0.65f) * archLift);
                int colBot = yBot + lift;
                int colTop = yTop + lift / 2;

                for (int y = colBot; y <= colTop; y++)
                {
                    bool border = x == minX || x == maxX || y == colBot || y == colTop;
                    if (border)
                    {
                        SetPx(px, x, y, Outline);
                        continue;
                    }

                    // Lit crest along the outer curve, warm parchment body, deeper shade near spine
                    Color32 col;
                    if (u > 0.84f) col = PaperHi;
                    else if (u > 0.55f) col = PaperBase;
                    else if (u > 0.22f) col = PaperWarm;
                    else col = PaperShade;

                    // Crisp 1px horizontal faint ink lines hinting at text on the turning leaf
                    if (u > 0.25f && u < 0.82f && y > colBot + 24 && y < colTop - 24 && (y % 9 == 0) && ((x + y) % 5 != 0))
                    {
                        col = PaperDeep;
                    }

                    SetPx(px, x, y, col);
                }
            }
        }

        private static void DrawLeftSettle(Color32[] px, int leftX, int yBot, int yTop)
        {
            for (int x = leftX; x <= leftX + 26; x++)
            {
                float u = (x - leftX) / 26f;
                byte a = (byte)Mathf.RoundToInt((1f - u) * 78f);
                var sh = new Color32(18, 12, 9, a);
                for (int y = yBot; y <= yTop; y++)
                {
                    BlendPx(px, x, y, sh);
                }
            }
            // Crisp highlight flash along the landed outer edge
            FillRect(px, leftX + 1, yBot + 4, leftX + 4, yTop - 4, new Color32(242, 226, 192, 165));
        }

        // =====================================================================
        // PIXEL UI CHROME SPRITES (TABS, BUTTONS, PROGRESS FRAMES, DUST, GLOW)
        // =====================================================================

        private static Sprite BuildBookmarkTab()
        {
            // White-tinted grayscale pixel ribbon tab (28x16) so Image.color tints each record cleanly!
            const int w = 28, h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Swallowtail / notched right tip
                    int notch = Mathf.Abs(y - h / 2);
                    if (x >= w - 3 && notch < 3 - (w - 1 - x)) continue;

                    bool border = y == 0 || y == h - 1 || x == 0 || (x == w - 1 && notch >= 3) || (x >= w - 4 && notch == 3 - (w - 1 - x));
                    if (border)
                    {
                        px[y * w + x] = new Color32(28, 22, 20, 255);
                    }
                    else if (y == h - 2)
                    {
                        px[y * w + x] = new Color32(255, 252, 244, 255); // top highlight
                    }
                    else if (y == 1)
                    {
                        px[y * w + x] = new Color32(150, 140, 132, 255); // bottom bevel shade
                    }
                    else if (x < 4)
                    {
                        px[y * w + x] = new Color32(172, 162, 152, 255); // tuck shadow behind book cover
                    }
                    else if ((y == 3 || y == h - 4) && x > 5 && x < w - 5 && x % 3 == 0)
                    {
                        px[y * w + x] = new Color32(245, 238, 225, 255); // stitched thread dots
                    }
                    else
                    {
                        px[y * w + x] = new Color32(226, 220, 212, 255);
                    }
                }
            }
            return MakeSprite("ArchiveBook_BookmarkTab", px, w, h, new Vector4(6, 4, 8, 4));
        }

        private static Sprite BuildPixelButton()
        {
            const int w = 32, h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x);
                    int dy = Mathf.Min(y, h - 1 - y);
                    if (dx + dy == 0) continue; // crisp 1px clipped corners

                    bool outer = x == 0 || x == w - 1 || y == 0 || y == h - 1 || (dx + dy == 1);
                    if (outer)
                    {
                        px[y * w + x] = new Color32(94, 67, 38, 235);
                    }
                    else if (y == h - 2 || x == 1)
                    {
                        px[y * w + x] = new Color32(248, 236, 206, 230);
                    }
                    else if (y == 1 || x == w - 2)
                    {
                        px[y * w + x] = new Color32(186, 158, 114, 215);
                    }
                    else
                    {
                        px[y * w + x] = new Color32(224, 204, 166, 185);
                    }
                }
            }
            return MakeSprite("ArchiveBook_PixelButton", px, w, h, new Vector4(5, 4, 5, 4));
        }

        private static Sprite BuildPixelBarFrame()
        {
            const int w = 24, h = 8;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    px[y * w + x] = border ? new Color32(98, 70, 38, 220) : new Color32(174, 148, 108, 130);
                }
            }
            return MakeSprite("ArchiveBook_BarFrame", px, w, h, new Vector4(3, 2, 3, 2));
        }

        private static Sprite BuildPixelRule()
        {
            const int w = 64, h = 5;
            var px = new Color32[w * h];
            int midX = w / 2;
            for (int x = 0; x < w; x++)
            {
                float edge = 1f - Mathf.Abs(x - midX) / (float)midX;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge * 1.4f) * 210f);
                px[2 * w + x] = new Color32(255, 255, 255, a);
            }
            // Tiny crisp 3x3 pixel diamond in center
            px[0 * w + midX] = new Color32(255, 255, 255, 235);
            px[1 * w + (midX - 1)] = new Color32(255, 255, 255, 235);
            px[1 * w + midX] = new Color32(255, 255, 255, 255);
            px[1 * w + (midX + 1)] = new Color32(255, 255, 255, 235);
            px[2 * w + (midX - 2)] = new Color32(255, 255, 255, 235);
            px[2 * w + (midX + 2)] = new Color32(255, 255, 255, 235);
            px[3 * w + (midX - 1)] = new Color32(255, 255, 255, 235);
            px[3 * w + midX] = new Color32(255, 255, 255, 255);
            px[3 * w + (midX + 1)] = new Color32(255, 255, 255, 235);
            px[4 * w + midX] = new Color32(255, 255, 255, 235);
            return MakeSprite("ArchiveBook_PixelRule", px, w, h);
        }

        private static Sprite BuildLanternEmblem()
        {
            const int w = 14, h = 18;
            var px = new Color32[w * h];
            void B(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                        if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
            }
            // Handle
            B(5, 15, 9, 17, BrassBright);
            B(4, 13, 6, 15, BrassMid);
            B(8, 13, 10, 15, BrassMid);
            // Cap
            B(2, 11, 12, 13, Outline);
            B(3, 12, 11, 13, BrassBright);
            // Glass & flame
            B(3, 4, 11, 11, Outline);
            B(4, 5, 10, 10, BrassDark);
            B(5, 5, 9, 10, BrassBright);
            B(6, 6, 8, 9, AmberCore);
            // Base
            B(2, 2, 12, 4, Outline);
            B(3, 3, 11, 4, BrassMid);
            B(4, 1, 10, 2, BrassDark);
            return MakeSprite("ArchiveBook_LanternEmblem", px, w, h);
        }

        private static Sprite BuildVignetteBackdrop()
        {
            const int w = 128, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "ArchiveBook_VignetteBackdrop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float ny = (y / (float)(h - 1)) * 2f - 1f;
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float dist = Mathf.Clamp01(Mathf.Sqrt(nx * nx * 0.85f + ny * ny));
                    // Keeps the paused gameplay world subtly visible through a dark archival veil
                    float alpha = Mathf.Lerp(0.56f, 0.88f, dist * dist);
                    px[y * w + x] = new Color32(10, 9, 13, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildLanternGlow()
        {
            const int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "ArchiveBook_LanternGlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[s * s];
            float half = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float falloff = Mathf.Pow(1f - d, 2.2f);
                    px[y * s + x] = new Color32(255, 186, 82, (byte)Mathf.RoundToInt(falloff * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildDustPixel()
        {
            const int s = 5;
            var px = new Color32[s * s];
            // Crisp 5x5 pixel star/spark mote
            px[2 * s + 2] = AmberCore;
            px[1 * s + 2] = AmberGlow;
            px[3 * s + 2] = AmberGlow;
            px[2 * s + 1] = AmberGlow;
            px[2 * s + 3] = AmberGlow;
            return MakeSprite("ArchiveBook_DustPixel", px, s, s);
        }

        // =====================================================================
        // PIXEL DRAWING HELPERS
        // =====================================================================

        private static Sprite MakeSprite(string name, Color32[] px, int w, int h, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void SetPx(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || x >= CanvasWidth || y < 0 || y >= CanvasHeight) return;
            px[y * CanvasWidth + x] = c;
        }

        private static void BlendPx(Color32[] px, int x, int y, Color32 src)
        {
            if (x < 0 || x >= CanvasWidth || y < 0 || y >= CanvasHeight || src.a == 0) return;
            int idx = y * CanvasWidth + x;
            Color32 dst = px[idx];
            if (dst.a == 0)
            {
                px[idx] = src;
                return;
            }
            float a = src.a / 255f;
            float inv = 1f - a;
            px[idx] = new Color32(
                (byte)Mathf.RoundToInt(src.r * a + dst.r * inv),
                (byte)Mathf.RoundToInt(src.g * a + dst.g * inv),
                (byte)Mathf.RoundToInt(src.b * a + dst.b * inv),
                (byte)Mathf.Min(255, dst.a + Mathf.RoundToInt(src.a * (1f - dst.a / 255f))));
        }

        private static void FillRect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    SetPx(px, x, y, c);
                }
            }
        }

        private static int Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 31;
        }
    }
}
