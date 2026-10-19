# Flare Glyphs & Secret Runes

`FlareGlyph` is an ancient ruin marking etched into walls or stone floors that remains completely dormant/invisible in darkness and normal Dim light, but absorbs high-intensity light and ignites with radiant luminescent energy when struck by **Flare (F)** scout pulses.

## Gameplay Value

1. **Strategic Flare Decisions:** In accordance with GDD Section 6 & 7, Flare costs 1 whole permanent ember. Giving Flare the ability to reveal secret runes and hidden paths gives players a compelling reason to spend embers for exploration, not just survival.
2. **Environmental Navigation:** Runes can point to hidden crawlspaces, warn of invisible trenches, or indicate secret stepping stones.
3. **Secret Passages & Caches:** Wiring `OnFullyCharged` to a `SecretPassage` component dissolves a fake wall partition, revealing hidden ember caches or lore fragments.

## Prefabs

Ready-to-use prefabs are available in `Game/Assets/_Project/Prefabs/`:
- `FlareGlyph.prefab`: Pre-configured with point Light2D, procedural rune sprite binding, and resonance chime.
- `SecretPassage.prefab`: Pre-configured with BoxCollider2D, SpriteRenderer, and smooth fade opening logic.

## Usage in a Chamber

1. Drag `FlareGlyph.prefab` into the scene where you want the rune (e.g. on a wall or arch).
2. Attach / configure `FlareGlyph`:
   - Set **Glyph Type** (`ArrowUp`, `ArrowRight`, `EmberEye`, `ArchiveSeal`, `GatewayRune`, `HiddenCache`, or `Custom`).
   - Set **Glow Color** (e.g. Ancient Cyan `#4ef2d2` or Warm Gold `#ffd166`).
   - Tune **Activation Distance** (default `9.5m`) and **Linger Duration** (default `3.5s`).
3. Optional **Translated Inscription:** Type an English lore sentence into **Inscription Text** (e.g. *"Only in high light shall the path be known"*). When the player stands near the charged rune, this translation gently fades in above the glyph.
4. Optional **Secret Passage / Puzzle Hook:** Drag `SecretPassage.prefab` into the scene, link `FlareGlyph.OnFullyCharged` to `SecretPassage.Open()`, and watch the wall dissolve to reveal hidden rewards.

## Performance & Self-Contained Design

- Uses `GlyphSprites.cs` to procedurally build point-filtered pixel sprites at runtime. No external texture files or texture import settings are required.
- Features a built-in procedural sinusoidal resonance chime if no audio clip is explicitly assigned.
- Supports `MenuPreferences.ReducedMotion` by disabling decorative pulsation and using smooth linear transitions.
- Fully tested with automated PlayMode suites in `Tests/FlareGlyphTests.cs`.

