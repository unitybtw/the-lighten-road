# Archive Test Chamber

A standalone test map owned by Siraç. Open `Assets/_Project/Scenes/Sandbox/Sirac/ArchiveTestChamber.unity` in Unity 6000.3.2f1, press Play, and focus the Game view.

**Controls:** A/D or arrows to move; hold Space for a full jump; F for the six-second Flare; Backspace to restart the complete route. Death also supports Space / Enter / R for an immediate retry.

The map uses the existing player, ember pickup, lantern, HUD, and respawn prefabs/components as dependencies. It does not change shared prefabs, Kemal's sandbox, machinery, or project build settings. Scene-only support code, typography, material, and tests live in this folder.

| Section | Position | What to test |
| --- | --- | --- |
| Intake | Start x=-5 | Safe movement, lantern visibility, the first pickup, and initial instructions. |
| Archive stacks | x=1–14 | 0.65m / 1m obstacles, variable jumps, camera look-ahead, optional 2m-high mezzanine, and an upper pickup. |
| Service crossing | x=14–28 | Checkpoint 01, two 2m trenches separated by a 3m island, hazard death, and a recoverable retry. |
| Blackout corridor | x=28–44 | Checkpoint 02, a 4.2m ceiling, obstacle silhouettes, 2D shadows, Flare scouting, and the final 2m trench. |
| Recovery / archive access | x=44–62 | Resource recovery, final jump obstacles, the exit trigger at x=57.5, and restart feedback. |

There is no global ambient illumination. Structural surfaces and background props use a local URP 2D lit material. World labels appear within the lantern's scouting range rather than revealing the entire route through darkness. The crate texture and character are existing project art; wall panels, shelves, pipework, and trench beds are editable blockout geometry.

## Retry and reset

Checkpoint triggers record safe spawn positions at x=14 and x=32.5. Later checkpoints take precedence over earlier ones. Hazards use the existing player death/respawn flow, restoring six charges on retry. Backspace restores the starting checkpoint, all eight ember pickups, and the exit state without reloading another teammate's scene or requiring build-settings changes.

## Verification

All three map integration tests passed in Unity 6000.3.2f1 via Unity MCP on 2026-10-16. The full authored main route was traversed with synthetic keyboard input and zero deaths. Scene validation found zero missing scripts and zero broken prefabs.

The local Play Mode suite `ArchiveTestChamberTests` loads the actual authored scene. It checks a full route with synthetic movement / single-jump input, checkpoint recovery after touching a trench, and a Backspace reset restoring pickups and the starting state. This does not establish a final human playtest duration or completed Office MVP.

`Editor/ArchiveTestChamberBuilder.cs` records the original construction layout. Its `Build()` method refuses to overwrite an existing scene; use normal scene editing for iteration.
