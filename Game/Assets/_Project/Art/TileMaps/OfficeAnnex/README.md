# Office tile delivery

Four original 96×96 PNGs supplied by Siraç, preserved byte for byte. See `delivery-sha256.json`. Each sheet is imported as a 3×3 grid of 32×32 sprites at 32 pixels per world unit, with point filtering and uncompressed/no-mipmap textures.

- `_Gray_1` / `_Blue_1`: convex outside corners, edges and solid center.
- `_Gray_2` / `_Blue_2`: concave inside corners and an open center. The open center Tile has no collider; the other pieces use grid collision when painted on a collidable Tilemap.

`Office Annex Palette.prefab` is a normal Unity Tile Palette with all 36 pieces. Its four groups reproduce the original sheet layout, from left to right: gray outside, gray inside, blue outside, blue inside. These assets are used by the separate Siraç OfficeAnnex scene. Existing team tile sets and palettes are unchanged.
