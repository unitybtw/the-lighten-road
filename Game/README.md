# The Lighten Road — Unity project

The shared Unity project uses the **Universal 2D** template in Unity **6000.3.2f1**. Siraç's sandbox currently includes tested movement, an early lantern fuel / Dim-Flare prototype, and a reusable ember pickup. Ember Veins, complete stages, and a standalone build are not implemented yet.

## Open the project

Open this repository in Unity Hub and select `Game/`, or open this folder directly with Unity Hub using editor version `6000.3.2f1`.

## Main menu

Open `Assets/_Project/Scenes/Frontend/MainMenu.unity` for the animated opening, Play, Settings, Credits and Quit. It is now the first build scene. Settings persists audio, fullscreen and reduced-motion preferences; Play opens the current Ruined Archive prototype. See the [menu setup, art prompt and validation](Assets/_Project/Scenes/Frontend/README.md).

## Current playable prototype

Open `Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity` and press Play. The scene now contains **The Ruined Archive**, a 72m prototype route with an optional upper office, three ember pickups, two rest checkpoints, floor spikes and two broken bridges. See the [level layout and asset notes](Assets/_Project/Scenes/Sandbox/Sirac/LevelMap/README.md).

**A/D or Left/Right** walks, **Shift** sprints, **Space** jumps, **F** starts a six-second Flare scout, and **Backspace** resets this route. Hold Space for a full jump. Walking restores stamina; sprinting and sprint jumps spend it. Each ember provides two scout charges. Dim is an infinite baseline light, and the game remains navigable without spending embers. The top-left stone reserve and contextual bottom-center stamina bar show their current state. A gamepad is bound but physical controller playtesting remains pending.

The current character, lantern, reactive ember pickups, death/respawn flow, particles and ambient music remain in this scene. This is a playable level prototype; puzzle machinery and final environment art are still pending team integration.

## Validation

In `Window > General > Test Runner`, select **PlayMode**. On **2026-10-16**, Unity 6000.3.2f1 through Unity MCP passed all **7 stamina HUD tests** and all **3 Ruined Archive integration tests**, including continuous keyboard traversal without fuel, the optional office reward, spike death, checkpoints and route reset.

The broader three-assembly run passed **79/81** tests. `FollowCamera2DTests.CameraLeadsAheadWhenTargetMovesHorizontally` missed its timing assertion and passed on an isolated rerun. The older `ArchiveTestChamberTests.FullRouteCanBeCompletedUsingMovementAndSingleJumps` still fails with the current movement tuning (six deaths, stopped at x=34.85); that separate older scene was not changed. These results do not establish final game feel or physical gamepad support.

[Project checks](../docs/TESTING.md) runs license-free project validation weekly, on relevant pull requests, and manually. GitHub-hosted Unity tests are configured but require repository license secrets before they can run; their skipped state is reported separately from passing integrity checks.

## Latest ember feedback

[Furkan's 2026-10-16 feedback](../docs/FEEDBACK-2026-10-16.md) is implemented in the current prototype: six-second scouts include their light transitions, first-use embers fade to half, second-use embers crack and disappear with a short placeholder sound, and the HUD uses the stone itself as the timer. Artist sprites and sound can be assigned in `LanternHUD`; final artwork remains pending. Siraç's archive chamber contains three pickups.

## Unity MCP tooling

[MCP for Unity](https://github.com/CoplayDev/unity-mcp/tree/v10.0.0) is pinned to **v10.0.0** in `Packages/manifest.json` and is distributed under MIT. The scene, player and lantern components, and Play Mode validation were handled through its editor connection.

To use automation, open `Window > MCP for Unity > Toggle MCP Window`, choose **HTTP Local**, and start the server at `http://127.0.0.1:8080`. The server needs Python and `uv` as described by the package. Normal editing and Play Mode do not require starting an MCP server. Keep this tooling local; no cloud project or shared credentials are needed.

## Project structure

```text
Assets/_Project/
  Scripts/          # Shared runtime code, organized by system
  Art/              # Approved sprites, tilesets, and animations
  Audio/            # Approved audio
  Prefabs/          # Reusable gameplay objects
  Scenes/
    Integration/    # Shared playable stages; coordinate edits
    Sandbox/
      Sirac/        # Developer test scenes
      Kemal/        # Developer test scenes
  Settings/         # Shared project-owned configuration
```

The `_Project` folders above have been created and Unity has generated their `.meta` files. Keep editable art sources in the repository's `art/` folders and puzzle documents in `design/`; import reviewed exports into this Unity project.

Before Ember Vein integration, review the [developer task split](../docs/DEVELOPMENT_PLAN.md) and agree on final input bindings, fuel tuning, the lantern interface, and recovery rules. Coordinate edits to shared scenes, prefabs, packages, and project settings using [the collaboration guide](../docs/COLLABORATION.md).

Unity-generated `Library/`, `Temp/`, logs, and local settings are excluded from Git.
