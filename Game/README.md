# The Lighten Road — Unity project

The shared Unity project uses the **Universal 2D** template in Unity **6000.3.2f1**. Siraç's sandbox currently includes tested movement, an early lantern fuel / Dim-Flare prototype, and a reusable ember pickup. Ember Veins, complete stages, and a standalone build are not implemented yet.

## Open the project

Open this repository in Unity Hub and select `Game/`, or open this folder directly with Unity Hub using editor version `6000.3.2f1`.

## First movement checkpoint

Open `Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity`, press Play, and focus the Game view. **A/D or Left/Right** moves the amber placeholder character; **Space** jumps. A gamepad left stick and south button are also bound, but have not been tested with a physical gamepad. Controls and tuning are temporary.

The scene contains a floor, boundary walls, three ascending platforms, the reusable `PlayerPrototype` prefab, a following orthographic camera, and a reusable `EmberPickup` prefab placed along the route. Press **F** to switch between **Dim** and **Flare**. The meter in the upper-left shows fuel; the default prototype starts at 80 / 100, Dim costs no fuel, Flare drains 0.5 units/second, and the pickup restores 20 units. The pickup remains in the scene if the lantern is full. Those values and F are temporary, inspector-configurable choices for the first review. Below 25% fuel, the light pulses. Zero fuel switches back to Dim and raises a depletion event; the death / respawn flow is not implemented. Details and the next step are in [Siraç's workspace notes](../development/sirac/README.md).

## Validation

In `Window > General > Test Runner`, select **PlayMode** and run `TheLightenRoad.Player.PlayModeTests` and `TheLightenRoad.Sirac.TestChamber.PlayModeTests`. On **2026-10-16**, all **53 tests passed** in Unity 6000.3.2f1 through Unity MCP on macOS: movement, camera, lantern fuel and controller, pickups, HUD, particles, respawn, and the archive test chamber. These results do not establish final game feel or physical gamepad support.

[Project checks](../docs/TESTING.md) runs license-free project validation weekly, on relevant pull requests, and manually. GitHub-hosted Unity tests are configured but require repository license secrets before they can run; their skipped state is reported separately from passing integrity checks.

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
