# Kemal — Game Developer

Personal workspace for technical notes, sprint milestones, and isolated prototypes.

**Owner:** [@kemal-berke](https://github.com/kemal-berke)  
**Assigned Scope:** Ember Vein Power / Drain interactions and prototype machinery (`Scripts/Interaction/`, `Scripts/Mechanisms/`).  
**Sandbox Scene:** [`SceneKemal.unity`](../../Game/Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity)

---

## 🛠️ Sandbox Scene Adjustments Applied

The following technical calibrations were applied to [`SceneKemal.unity`](../../Game/Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity) to ensure smooth physics and visual grid fidelity:

1. **Pixels Per Unit (PPU) Calibration (32 PPU):**
   - Texture: [`Box.png`](../../Game/Assets/_Project/Art/Sprites/Box.png)
   - Updated `spritePixelsPerUnit` from `30` to `32`.
   - **Benefit:** Eliminates the 6.6% unit grid overlap and prevents camera pixel shimmering during movement.
2. **Seamless Composite Physics:**
   - Tilemap: `Grid/Tilemap`
   - Added `CompositeCollider2D` (Geometry Type: `Polygons`) and `Rigidbody2D` (`Body Type: Static`).
   - Enabled `Used By Composite` (`m_CompositeOperation = Merge`) on `TilemapCollider2D`.
   - **Benefit:** Merged all 215 tiles into a single continuous polygon collider, eliminating internal edge-snagging ("ghost collisions") when the player walks or lands.

---

## 🎯 Next Milestones for [@kemal-berke](https://github.com/kemal-berke)

Refer to the main [Development Plan](../../docs/DEVELOPMENT_PLAN.md) for the active milestone roadmap:

- [ ] **Ember Vein Interaction (`Scripts/Interaction/`):**
  - Implement interaction trigger detection (input key `E`).
  - Connect to [`LanternFuel`](../../Game/Assets/_Project/Scripts/Lantern/LanternFuel.cs):
    - Power Vein: `LanternFuel.TrySpendFuel(cost)`
    - Drain Vein: `LanternFuel.AddFuel(recovery)`
- [ ] **First Test Machinery (`Scripts/Mechanisms/`):**
  - Connect an Ember Vein to a simple test lift or gate mechanism inside [`SceneKemal.unity`](../../Game/Assets/_Project/Scenes/Sandbox/Kemal/SceneKemal.unity).
  - Verify state change (powered -> lift moves; drained -> lift halts).
- [ ] **Tilemap Palette Enhancement (Visual):**
  - When expanding the underground tileset, consider setting up a Unity **Rule Tile** for automated ground crusts, ceiling edges, and corners.
