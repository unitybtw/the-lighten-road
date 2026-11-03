# Office Annex — 3 November 2026

The separate `Assets/_Project/Scenes/Sandbox/Sirac/OfficeAnnex.unity` playtest uses the supplied gray/blue office tiles and Push animation. The original PNG/JSON deliveries are preserved byte for byte and have SHA-256 manifests. The main chapter route and Kemal-owned files are unchanged.

The route starts in reception, uses a physically pushed crate to reach a higher records landing, then crosses a service shaft to the ledger desk and exit. The upper landing records a checkpoint. The existing office ledger opens in the current Codex reader; the scene reuses the authored office atlas and archive desk pixels. Gray and blue tiles distinguish the two floor heights, and warm local lamps identify the arrival, landing and reading areas. There are two ember reserves rather than a dense pickup trail.

The editable Tile Palette contains 36 cells. The new scene contains separate solid Tilemap/CompositeCollider2D sections and a non-colliding rear layer. Inside-corner tiles join floors and walls; transparent center tiles have no collider. The refresh command derives joins from painted geometry and the create command refuses to overwrite an existing scene. See the [scene authoring notes](../../../Game/Assets/_Project/Scenes/Sandbox/Sirac/OfficeAnnex/README.md).

## Validation

- Compilation: zero errors/warnings on Unity 6000.3.2f1.
- **6/6 new-scene Play Mode tests**, 20.59 seconds: tile-source/collision integration, physical pushing both ways and pose release, pause/jump behavior, crate/checkpoint retry, actual keyboard traversal through the full route with E/Escape reader controls, and the existing lantern completion/input cleanup.
- **164/164 player Play Mode tests**, 67.32 seconds: existing animation, climb, movement, lantern and HUD behavior. The catalog assertion now expects nine sequences / 131 frames, reflecting the ten supplied Push frames; prior animation indices and source assets remain unchanged.
- The first contact run correctly showed Push but exposed excessive crate friction/mass. The scene's crate was retuned to mass 6 / friction .12 and both directional movement tests then passed. Final successful reports are summarized in [the validation record](office-annex-validation.json).
- Inspected actual Game captures of arrival, pushing, the reading corner and the whole route. The Push capture followed real synthetic D input, with the crate moved 3.76 units and the animator on Push frame 3. Reading/overview photos reposition the runtime camera/player for inspection; those changes were not saved. The exit fixture was moved above the door after the visual pass; the final change is lighting placement only.
- Personal progress/preferences and temporary input settings/devices were restored. The new scene is left open outside Play Mode. No standalone build, physical-controller check or further complete-project suite was run.

[Arrival](previews/office-annex/arrival-1920.png) · [Push contact](previews/office-annex/push-1920.png) · [Ledger corner](previews/office-annex/ledger-1920.png) · [Whole route](previews/office-annex/overview-1920.png)
