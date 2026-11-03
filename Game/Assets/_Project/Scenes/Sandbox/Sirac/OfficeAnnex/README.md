# Office Annex — playable tilemap study

Open `OfficeAnnex.unity` beside this folder, or use **Tools → The Lighten Road → Office Annex → Open test scene**. This is a separate sandbox entry in Build Settings; the main menu's chapter route is unchanged.

The short route uses the four delivered office sheets as real 32px Tilemap cells. Gray supports the entry and reading wing; blue identifies the higher records landing. The `_2` sheets supply the inside corners where floors meet walls. There are no colliders on the rear wall layer. Each solid section has its own Tilemap/CompositeCollider2D so the existing climb system can identify its actual top edge.

## Route

1. Walk through the abandoned reception and push the archive crate toward the high blue landing. Push is automatic during grounded side contact with movement into the crate. It works in both directions.
2. Jump onto the crate, then jump and press Up/W at the landing edge to climb. The authored landing is too high for a direct grounded grab. A checkpoint on the upper route prevents repeating this approach after a fall.
3. Sprint-jump the service shaft to the reading wing. The two ember pickups are reserves rather than a trail marking the solution.
4. Read the existing office ledger at the lamp-lit desk with E or the controller's north button. It uses the current Codex reader and record; no new lore was invented for the scene. The desk renders behind the character and does not block walking.
5. Enter the exit door area to play the existing lantern-led completion sequence. This is the end of this standalone route; it does not unlock or load an unfinished chapter.

The crate resets on respawn, including after the upper checkpoint. It uses a local rigidbody/contact script under Siraç's scene folder. No vein, lift, shelf, water or Kemal-owned scene/script is modified. The existing player motor still owns movement and jumping. The Push owner reports effort to PlayerSpriteAnimator; it does not replace the motor, teleport the actor or hold a modal input lock.

## Editing

The **Office Annex Palette** under `Art/TileMaps/OfficeAnnex` contains all 36 imported Tile assets. Textures retain their supplied bytes, point filtering, no mipmaps and no compression. The mostly transparent center of each inside-corner sheet has no collider.

The create command refuses to overwrite an existing scene. Edit the saved Tilemaps and objects normally. **Refresh tile joins and lamps** recalculates joins from currently painted solid cells, preserving the route's painted geometry; keep the Tilemaps at their shared grid origin. It also restores the authored local light settings. It never rewrites another scene.

The character delivery adds ten 100ms Push frames to the existing catalog without changing older enum indices. Its 42px horizontal registration aligns the extended hands with the capsule contact face; the original PNG is unmodified. Existing office props and the original archive desk pixels are reused. The desk sprite is baked from ArchiveTerminalVisual for an editor-visible, lit scene object.
