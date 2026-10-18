# Developer mechanics contract — 18 October 2026

This update supersedes the older fuel, Flare and chamber-reset rules in the pre-production GDD. It records the team's supplied specification and the owner's subsequent clarification: **the lantern can carry at most three embers; excess pickups must remain in the world. There is no +1 / +2 overflow inventory.**

## Resource rules

- Enter each chamber with three embers. A death/respawn restores three embers at that chamber's start.
- Dim is passive while any ember remains; it does not consume fuel. With no held embers, the passive light goes out.
- More than three continuous seconds at zero held embers causes Darkness death. Recovering any fuel cancels the countdown. Pausing gameplay pauses this timer.
- F permanently spends one whole ember immediately and starts the timed, smoothly fading scout light. Turning Flare off early never refunds the ember. The three-second zero-inventory countdown also applies while the last spent ember's Flare is still animating.
- E temporarily puts one ember into a circuit or retrieves that same ember. This is a reversible transfer, not a permanent spend.
- Three inventory icons are available. A spent stone may fade and crack in an empty icon position during the scout animation, but is no longer held or spendable. Each whole stone finishes in one use; the former two-use half-ember design is superseded.

## Current player implementation

`LanternFuel` retains two internal charge units per ember so Kemal's existing two-charge mechanism API stays source-compatible. The inventory is capped at six units / three embers. `TrySpendEmber()` spends two units atomically. Old serialized capacities and overflow flags cannot increase this limit. `ResetForChamber()` restores three embers.

`EmberPickup` rejects collection unless its complete reward fits and leaves the object available. Trigger-stay retries collection if the player spends an ember while still standing on it.

`LanternController` spends one whole ember per timed scout and animates full-to-empty wear in a single use. `LanternHUD` always creates three slots, including when old scenes serialized five.

`PlayerRespawnController` owns the darkness grace timer. `BeginChamber(start)` resets fuel, stamina and lantern presentation and records the next retry location. The existing Siraç prototype `SetCheckpoint` API now enters a chamber segment; existing route markers therefore serve as prototype chamber boundaries. Respawn restores the player's resources and position. The mechanism/world-state reset still requires coordination with the circuit owner; it is not implemented by these player changes.

## Circuit ideology

Powered means active force: movement, a raised shutter/lift, or lethal electricity. Drained means a fixed shelf, lowered shutter/lift, or safe water. A drained mechanism is an intentional puzzle state.

One circuit may drive several consequences at once. For example, power both raises an exit shutter and electrifies the water in front of it. One authoritative state must notify all linked receivers.

Two interaction nodes on opposite sides of an obstacle must share that same state and stored ember. A player can insert at A and recover at B. Independent inventories or independent state flags are not equivalent. One E press must be handled only once even if both nodes are within range.

| Receiver | Powered | Drained |
| --- | --- | --- |
| RollingShelf | Patrol A → B → C → … and back continuously | Stop immediately at the exact current position; remain a solid bridge |
| SimpleLift / vertical shutter | Winch toward the upper target | Descend smoothly under gravity toward the lower target |
| ElectrifiedWater | Enable electricity and lethal hazard | Disable electricity and hazard; water is safe |

## Read-only review of Kemal's current implementation

The following observations are from source inspection, not a claim about tested scene wiring. The `Interaction/`, `Mechanisms/` and Kemal scene files were not edited.

- **EmberVein:** default cost is already two charge units / one ember. `OnPowered` and `OnDrained` UnityEvents can notify multiple targets, so coupled consequences are supported by the event structure. Scene receiver lists still need checking.
- **Multi-node state:** `currentState` is stored on each EmberVein component; no shared circuit state/node association is present. Each nearby component also handles E independently.
- **Full-inventory recovery:** the current drain path calls `AddFuel` and then unconditionally clears the circuit. With the hard cap, draining into a full lantern would lose the stored ember. The circuit owner must check complete capacity before changing state and only clear the stored ember after successful recovery.
- **RollingShelf:** currently moves once to an offset and returns to start when closed. It needs waypoint patrol plus a drain operation that freezes the current position.
- **SimpleLift:** currently moves to top/bottom at constant speed. A gravity descent is not implemented.
- **ElectrifiedWater:** Electrify enables the hazard and Purify disables it, which are the needed effects. Existing comments describe opposite event wiring, and Start unconditionally electrifies the water. Initialization and wiring must follow the authoritative circuit state.
- **Chamber retry:** when the player is restored to three embers, circuit state and deposited embers must reset consistently to the chamber's authored initial state. Otherwise retrying could duplicate recovered energy. This requires a circuit-owner integration change.

## Validation

Validation results are recorded after the focused Unity Play Mode suite. Standalone build behaviour and cross-owner circuit integration require separate checks.

Unity 6000.3.2f1 / Unity MCP Play Mode: **62/62 focused tests passed** (`213f90b116dc4197914e1ca99a433aaa`), covering fuel, lantern, HUD, pickup, respawn, particle and sound behaviour. All **3/3 real-scene route tests passed** (`bf5df8f029904ca1b528044282ca4c3f`): the main route under the chamber budget, optional office pickups, and hazard/checkpoint/restart behaviour. The route fixture now refreshes held simulated keys each frame to tolerate Editor focus changes; traversal assertions and physical geometry were retained. An earlier broad run was interrupted by an Editor domain reload and is not counted as a pass. No standalone build or multi-node circuit integration was tested.
