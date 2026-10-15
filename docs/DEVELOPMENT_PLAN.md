# Proposed Developer Task Split

**Status: Siraç's player movement, camera, lantern API, and ember pickup foundation are implemented and covered by 22 passing automated Play Mode tests; Kemal's machinery work remains planned.** The lantern API is isolated in its own assembly (`TheLightenRoad.Lantern`) and ready for Kemal's Ember Vein Power / Drain interactions. Temporary bindings and tuning remain provisional for team review.

**2026-10-15 checkpoint:** `MovementSandbox` contains the tested player movement / camera prototype, the lantern prototype, and the ember pickup. All 22 automated Play Mode tests passed in Unity MCP (4 movement/camera, 11 lantern fuel, 4 lantern controller, 3 ember pickup integration). The lantern API exposes `TrySpendFuel`, `CanSpendFuel`, `AddFuel`, `Initialize`, capacity/fuel queries, and change / depletion events. See [Siraç's notes](../development/sirac/README.md) for controls, tuning, test suites, and next steps.

The shared project is `Game/`, created with Unity **6000.3.2f1** and the **Universal 2D** template. Both developers should use that editor version for this project.

## Siraç — player and lantern foundation

Suggested branch: `feature/sirac/player-lantern`

1. Build the player movement prototype: left / right movement, jump, grounded checks, collision behavior, and a basic camera follow. Keep to the GDD limits: no sprint, double-jump, or attack.
2. Own the lantern's fuel state and its shared API. **First pass implemented:** `LanternFuel` provides `TrySpendFuel`, `AddFuel`, `CurrentFuel`, `Capacity`, and change / depletion events; `LanternController` handles Dim / Flare, low-fuel light pulsing, and the visible meter; `EmberPickup` restores fuel on contact and stays available while the lantern is full. All numbers and the F binding are temporary because the GDD leaves tuning and controls open.
3. Keep the player and lantern together in `Game/Assets/_Project/Scenes/Sandbox/Sirac/` so movement and fuel can be explored without editing the shared integration scene.

Suggested review outcome: a controllable player can move and jump on a simple greybox floor; the lantern can switch modes, show its fuel state, and accept fuel changes through `LanternFuel`. Review the prototype's F binding and default tuning with the team before treating them as shared decisions.

## Kemal — Ember Veins and machinery

Suggested branch: `feature/kemal/ember-veins`

1. Prototype the Ember Vein interaction and the Power / Drain state flow. Power requests fuel from Siraç's lantern API before activating a device. Drain returns the agreed amount of energy and turns its linked device off.
2. Implement small, separate mechanism prototypes for rolling archive shelves, one lift, and electrified water. Keep mechanism behavior configurable and test one device at a time.
3. Provide an interaction and machinery sandbox scene at `Game/Assets/_Project/Scenes/Sandbox/Kemal/` with simple placeholder visuals and clear powered / unpowered feedback.

Suggested review outcome: an in-range player can activate a test Ember Vein; Power succeeds only when enough fuel is available; Drain shuts down the linked mechanism and requests fuel recovery. The accepted prototype should demonstrate the interaction using the shared API, not a second copy of the lantern fuel logic.

## Shared decisions before integration

Before either feature depends on the other, agree on:

- Which input action means interact, and how input bindings will be configured.
- Review `LanternFuel.TrySpendFuel(float)` and `LanternFuel.AddFuel(float)`: insufficient spending returns `false`, while recovery is capped at `Capacity` and returns the amount accepted.
- Initial test values for fuel capacity, Power cost, Drain recovery, interaction range, movement speed, jump height, and Dim / Flare behavior. Keep them as temporary tuning values until Furkan and the team review them against the GDD.
- Whether a drained mechanism can be powered again, whether device state persists, and how failed interactions are communicated.
- Which prefabs or scripts each person owns. Each person should make a separate sandbox scene and avoid editing the same scene, prefab, project setting, or package manifest at once.

Current code boundaries:

```text
Game/Assets/_Project/Scripts/Player/       Siraç: movement and player-facing components
Game/Assets/_Project/Scripts/Lantern/      Siraç: fuel state and lantern modes
Game/Assets/_Project/Scripts/Interaction/  Kemal: interaction detection and Ember Vein flow
Game/Assets/_Project/Scripts/Mechanisms/   Kemal: lift, shelves, and electrified water
```

## First integration checkpoint

1. Review both sandbox scenes and agree on the shared inputs and fuel API.
2. Connect one Ember Vein to one simple test device in a coordinated integration scene.
3. Verify the full loop in Unity: movement, Power fuel check, mechanism activation, Drain recovery, and mechanism shutdown.
4. Only after that loop is stable, expand the device set and begin the two Office stages with Furkan's level plans and reviewed art exports.

Keep the tasks on individual branches and merge reviewed work through pull requests as described in [the collaboration guide](COLLABORATION.md). Record what is implemented and tested in each pull request. Features outside the checkpoint recorded above remain planned.
