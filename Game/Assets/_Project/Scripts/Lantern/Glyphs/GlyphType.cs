namespace TheLightenRoad.Lantern.Glyphs
{
    /// <summary>
    /// Supported ancient glyph and rune symbols.
    /// </summary>
    public enum GlyphType
    {
        /// <summary>Upward chevron indicating an elevated ledge, climb, or high secret.</summary>
        ArrowUp = 0,

        /// <summary>Rightward chevron pointing toward hidden side passages or exits.</summary>
        ArrowRight = 1,

        /// <summary>Downward chevron indicating drops, lower trenches, or descent paths.</summary>
        ArrowDown = 2,

        /// <summary>Leftward chevron pointing back or toward left secret alcoves.</summary>
        ArrowLeft = 3,

        /// <summary>Ancient radiant eye symbol indicating a hidden sight, illusion, or mystery.</summary>
        EmberEye = 4,

        /// <summary>Concentric diamond seal of the Old World archive representing classified lore.</summary>
        ArchiveSeal = 5,

        /// <summary>Archway and threshold rune representing a hidden portal, door, or barrier.</summary>
        GatewayRune = 6,

        /// <summary>Corner brackets enclosing a diamond core indicating a hidden ember cache.</summary>
        HiddenCache = 7,

        /// <summary>User-provided custom sprite.</summary>
        Custom = 8
    }
}
