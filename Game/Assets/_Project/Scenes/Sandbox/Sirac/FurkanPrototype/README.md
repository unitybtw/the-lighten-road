# The First Light — Furkan tutorial prototype

Open `../FurkanTutorial.unity` in Unity **6000.3.2f1**. It is an editable, authored scene, registered in Build Settings and used by the main menu's first realm. Existing MovementSandbox and Kemal's scenes remain available.

## Source and interpretation

Based on Furkan's [Tutorial / First Chamber sketch](https://raw.githubusercontent.com/unitybtw/the-lighten-road/64929995edb2459bd46b192ff2cdfb8bcc09169a/design/levels/visual.png), also stored at `design/levels/visual.png`. The checked-in image matches the linked commit's Git blob exactly. This is a playable blockout, not final environment art.

The design preserves the sketch's order and learning goals:

1. **Chamber 01 / Tutorial:** safe scenic floor, two rising jump blocks, a raised reception landing and a static fallen desk. Walking and normal jumping are sufficient; sprinting is optional.
2. **Chamber 02 / Beginnings:** entry restores three embers and closes the return shutter; a compact pit uses two stepping stones. A crossing guidance glyph (`Glyph / Crossing Guide`) etched above stepping stone A ignites with radiant cyan light under Flare, revealing an ancient inscription and illuminating the crossing below (directly reflecting the sketch's "FLARE / LIGHT CAN'T SEE THIS" eye symbol). A distant ember is outside the initial Dim radius and can be spotted with Flare before approaching.
3. **Shared Vein circuit:** node A deposits one ember and powers the moving archive shelf. Node B recovers that same ember and freezes the shelf at its exact location. Four waypoints loop in the annotated order, with a one-second stop at every corner.
4. **Optional upper route & secret vault:** jump from the upper platform stop to recover an archive fragment. Right next to this ledge, a hidden archive vault is sealed behind a secret stone partition (`SecretPassage`). Flaring near the ancient vault seal glyph (`Glyph / Vault Seal`) charges the rune, opens the secret partition and reveals a bonus ember cache (fulfilling the sketch's top-right "COLLECTIBLE" path). Taking this detour is optional; the main exit remains on the far landing.

Distances are adapted to the existing player's real walk speed and jump height. The first blocks rise 0.9m and 1.8m, reception is at 2.4m, the moving shelf spans four stops, and the optional ledge is at 6.7m. The complete footprint is approximately 44m. Scene objects are named by their role and can be moved normally in Unity.

## Resources and retry

Carrying capacity is **four embers**; chamber entry and respawn restore **three**, leaving room for one found reserve. Flare permanently spends one whole ember. Depositing in a Vein is temporary; recovery refuses a full lantern without losing the circuit's stored ember.

On death, this prototype resets its circuit, platform, ember pickups and optional fragment to their authored starting state, together with the player's chamber retry location and three-ember budget. Backspace restarts both chambers during gameplay. E handles only the closest node, so one keypress cannot toggle the shared circuit twice. Menu and reader locks also block these scene-local controls. Controller restart is available through the pause menu's confirmation, rather than a single touchpad/view-button press.

## Guidance and blockout notes

Nearby cues introduce walking, jumping, sprinting and Flare. The bottom prompt is reserved for a node interaction or brief event feedback; it clears during exploration. Node prompts use the shared keyboard/Xbox/PlayStation style and accept E or the controller's north button (Y / Triangle).

The eleven floating blockout labels are kept under **Blockout annotations (editor reference)**, disabled by default and tagged `EditorOnly` so builds omit them. Enable that group in the editor when reviewing chapter names, node identities, shelf timing and the optional ledge. The physical nodes, guide rail, collectible, exit lighting and contextual prompts remain part of gameplay. Jump/desk instruction volumes that repeated the nearby cues have been removed.

Verified on 2026-11-02 with Unity CLI / 6000.3.2f1: **17/17 related Play Mode tests passed**, including real traversal through the shelf to the exit, resource conservation, guidance, input styles and pause/modal locks. Actual 1920×1080 Game captures of arrival, the PlayStation node prompt and completion are stored in `development/sirac/previews/archive-guidance/`. Completion's hold was extended and scene loading disabled on a runtime-only copy for its still image; authored timing and routing were preserved. No standalone or physical-controller validation was performed for this pass.

## Ownership and code

All prototype-specific components are isolated here under Siraç's scene folder. `ArchiveCircuit`, `ArchiveCircuitNode`, and `ArchivePatrolPlatform` are scene-local implementations for this reference layout. They do not modify or claim to replace Kemal's shared Interaction / Mechanisms implementation. There is no electrified-water or gravity-lift puzzle in this sketch, so those mechanisms were not added.

`FurkanPrototypeBuilder` creates the initial scene once and refuses to overwrite it afterward. The floor, blocks, shelves, lamps, path marks, nodes and triggers are ordinary authored GameObjects; the level is not regenerated at runtime. Existing player, lantern, HUD, stamina, death and ambient audio components are reused.

## Controls

A / D or arrows: move. Space: jump. Shift: optional sprint. F: spend an ember to scout. E: deposit/recover at a node. Backspace: restart. All prompts are English.

## Validation

Validation results are recorded after the Unity Play Mode tests. Tests exercise real movement through the jump tutorial, compact crossing and moving platform, along with node resource conservation, frozen positions, retry state and the optional collectible. A standalone build and physical controller playtest are separate validation steps.

Unity MCP / Unity 6000.3.2f1: **76/76 Play Mode tests passed** in the completed combined run `e3ea7b85ff9e4660916a7ac1f9ef16d0` (77.53 seconds of test execution). Six tests cover this new scene: shared-node conservation/full-inventory rejection, exact freeze, chamber retry, real tutorial traversal, compact crossing + platform ride + exit, and the reachable optional fragment. The remaining cases cover fuel/HUD/pickup/respawn, particles, audio, menu routing and the existing MovementSandbox route. Earlier development failures were corrected before this final run. No standalone build was produced.

## Exit presentation

The exit at x=40.5 uses `ChamberExit.prefab` with the `FirstLightCompletion` profile. It marks this tutorial complete, reveals a warm light wave/title, plays the arrival chime and fades into the existing `ArchiveTestChamber` prototype. Edit the instance's Destination Scene for another chapter. See [reusable completion setup](../../../../Scripts/Player/Completion/README.md).
