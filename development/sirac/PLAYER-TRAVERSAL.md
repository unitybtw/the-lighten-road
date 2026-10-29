# Lantern stow and ledge climb

The shared PlayerPrototype now connects Maksim's Sheathing and Climb sprites to a deliberate ledge traversal. Existing instances inherit it; authored scenes and Kemal's protected paths are unchanged.

## Controls

- Face a nearby ledge and press **W / Up Arrow**, **D-pad Up**, or deliberately push the **left stick upward**. Releasing Up does not interrupt a started climb.
- **S / Down Arrow**, **D-pad Down** or a fresh **Jump** press cancels the traversal. The visible foot position is preserved while the collider is reconciled along a checked clear path, then any remaining visual offset eases out over 0.16 seconds. Gravity resumes without an added jump.
- Up has a **0.2-second request buffer**. Simultaneous left/right input waits for the matching facing direction. Jump takes priority over Up, allowing a normal jump followed by an airborne grab.
- Horizontal movement alone never starts a climb. A controller connected with its stick already held up must return to neutral before an upward stick press can begin one.
- The main-menu keyboard/controller guide and pause guide include the climb binding.

## Animation and movement

On a grounded start, the first **0.7 seconds** of Sheathing put the lantern away. An airborne grab skips this preparation. A **0.12-second reach** blends into Climb, whose ending already draws the lantern again. Low ledges choose an appropriate entry within frames 0–10 instead of forcing an overhead hanging pose through the floor. Higher ledges retain the complete hanging start. The source Climb remains 27 frames / 2.7 seconds; every played frame retains its original duration. Both complete source clips remain available through the independent context API. There is no new permanent lantern-off toggle.

Climb raises the capsule beside the lip before crossing onto it. The importer derives two-dimensional hand, leading-foot and sole markers from Maksim's original pixels. Frames before 15 pin the hand to the actual corner; frames 15–19 lift the leading foot over the edge while keeping the trailing foot outside the wall; frames 20 onward register the sole to the same landing baseline as Idle. The stowed lantern's light follows the belt instead of floating at an invisible raised hand. Source sheets, pivots, sprite identities and preview-clip timing are unchanged.

One fixed-step clock drives the physical path and source pose. LateUpdate registers the sprite against the rendered Transform, preserving contact with an interpolated Rigidbody. The full capsule remains enabled; there is no root scaling, collider resizing or collision bypass. Movement checks both the vertical lift and horizontal crossing before starting and during traversal. Cancellation can reconcile the body to the visible pose only along a collision-checked path. No ember is spent, no light-survival rule is suspended, and no circuit or mechanism state is changed. An active Flare must finish before the player can stow the lantern to climb.

Player movement and lantern input are temporarily blocked during the action. Pause and other input owners freeze the sprite timeline together with the physical traversal. Finish, cancellation, component disable, removed/moved/rotated support, new obstruction and death release the owned gravity/input/animation-clock state. Death retains the actual visible climb pose until respawn instead of snapping back to the capsule; respawn clears it. Legacy vein input polling uses shared leases with pause/death so releasing one owner cannot re-enable it while another still owns a lock; powered state and machinery keep their existing behavior.

## Level-authoring limits

Only solid, stationary, near-vertical ledges with a near-horizontal top are accepted. The lip must be between **0.25 and 1.35 units above the collider centre**, within **0.32 units beyond its front edge**, with a clear starting capsule and space above the player, along the lift and across the entire landing capsule. Both edges of the landing footprint need support. Thin pillars, tall walls, triggers, one-way platform effectors and dynamic/kinematic machinery are excluded. Static transform-driven geometry cancels the climb if it moves during the action. This deliberately avoids taking over Kemal's lifts and rolling shelves.

Tune reach, lip-height range, landing inset and stow duration on **PlayerLedgeClimb** in PlayerPrototype. The importer keeps the component on rebuild. Runtime detection uses fixed query buffers, with conservative rejection if a query fills its buffer. No per-frame scene scan is performed; the legacy polling lease scans only when a lock is acquired.

## Verification

Latest verification: **141/141 Player tests**, including **22 ledge-climb cases**, and **10/10 FirstLight tutorial tests** in Unity 6000.3.2f1. The actual shared prefab is exercised with real input and physics; a late-frame probe checks hand contact, standing feet, trailing-foot clearance and the return to Idle. Both directions, heights, jump priority, input buffering, interruption, pause, death/respawn and nested polling leases are covered.

See [climb polish and runtime recordings](CLIMB-POLISH.md) and [current validation](climb-polish-validation.json). This is a scoped 151-case result, not a complete-project rerun. Physical-controller feel and a standalone build remain separate playtesting. [Initial integration evidence](traversal-validation.json) is retained as history.
