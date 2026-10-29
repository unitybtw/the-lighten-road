# Lantern stow and ledge climb

The shared PlayerPrototype now connects Maksim's Sheathing and Climb sprites to a deliberate ledge traversal. Existing instances inherit it; authored scenes and Kemal's protected paths are unchanged.

## Controls

- Face a nearby ledge and press **W / Up Arrow**, **D-pad Up**, or deliberately push the **left stick upward**. Releasing Up does not interrupt a started climb.
- **S / Down Arrow**, **D-pad Down** or a fresh **Jump** press cancels the traversal. Cancellation releases the player at the current safe position; gravity resumes. It does not teleport the player back or add a jump.
- Horizontal movement alone never starts a climb. A controller connected with its stick already held up must return to neutral before an upward stick press can begin one.
- The main-menu keyboard/controller guide and pause guide include the climb binding.

## Animation and movement

The first **0.7 seconds** of Sheathing put the lantern away. The full **2.7-second Climb** then plays; its final frames already show the lantern being drawn again. Running the remaining Sheathing frames before Climb would draw it prematurely, so they remain in the original clip and the independent context API instead. Source sheets, individual frame durations and pivots are unchanged. This is a contextual stow, with no new permanent lantern-off toggle.

Climb raises the character beside the lip before crossing onto it. The importer derives a horizontal reach anchor for each Climb frame from the upper silhouette. A temporary visual registration lets that hand reach the detected lip, then eases back to normal as the body crosses. Its calculation uses physics-relative coordinates, so a delayed interpolated Transform after a relocation cannot send the sprite away from the player. The visual translation is restored on interruption and completion. Its full capsule remains enabled; there is no root scaling, collider resizing, noclip or root teleport. The motion uses the Rigidbody2D and checks both the vertical lift and horizontal crossing before starting and during traversal. No ember is spent, no light-survival rule is suspended, and no circuit or mechanism state is changed. An active Flare must finish before the player can stow the lantern to climb.

Player movement and lantern input are temporarily blocked during the action. Pause and other input owners freeze the sprite timeline together with the physical traversal. Finish, cancellation, component disable, removed/moved support, new obstruction and death restore the owned gravity/input/animation-clock state. Respawn clears traversal. Legacy vein input polling uses shared leases with pause/death so releasing one owner cannot re-enable it while another still owns a lock; powered state and machinery keep their existing behavior.

## Level-authoring limits

Only solid, stationary, near-vertical ledges with a near-horizontal top are accepted. The lip must be between **0.25 and 1.35 units above the collider centre**, within **0.32 units beyond its front edge**, with a clear starting capsule and space above the player, along the lift and across the entire landing capsule. Both edges of the landing footprint need support. Thin pillars, tall walls, triggers, one-way platform effectors and dynamic/kinematic machinery are excluded. Static transform-driven geometry cancels the climb if it moves during the action. This deliberately avoids taking over Kemal's lifts and rolling shelves.

Tune reach, lip-height range, landing inset and stow duration on **PlayerLedgeClimb** in PlayerPrototype. The importer keeps the component on rebuild. Runtime detection uses fixed query buffers, with conservative rejection if a query fills its buffer. No per-frame scene scan is performed; the legacy polling lease scans only when a lock is acquired.

## Verification

Results and actual-scene captures are recorded in [traversal-validation.json](traversal-validation.json). The new tests exercise the real shared prefab and actual input/physics, including both directions, airborne grab, controller intent, collision rejection, interruption, pause, death/respawn and nested polling leases. Physical controller feel and a standalone build require separate playtesting; synthetic controller tests do not establish either.

Final results: **129/129 player tests**, including 13 traversal tests; **10/10 pause tests** and **11/11 menu readability tests**. The earlier broad run was 335/337, and the following frontend run was 158/160; their two failures were addressed in the final focused reruns. Combining the latest result for each distinct case gives **338 passing outcomes**, including the added start-overlap regression. This is not a single clean 338-case full run. The final player run includes the physics-relative registration correction and per-frame reach data.

FurkanTutorial: positioned the player beside its existing **Far side and exit** ledge, adjusted the camera only in Play Mode, and advanced normal runtime traversal. Sheathing_005 and Climb_007 were captured; after completion the character was grounded at approximately (35.70, 3.20), the capsule remained enabled, gravity returned to 3, all six charges remained, and gameplay input was available. The camera/player setup was for inspection, not a full-route proof, and no scene edits were saved.

[Stowing](previews/player-traversal/live-stow.png) · [Climbing](previews/player-traversal/live-climb.png) · [Back on the platform](previews/player-traversal/live-finished.png)
