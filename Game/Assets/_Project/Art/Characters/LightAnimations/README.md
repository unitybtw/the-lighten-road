# Light animation delivery — Maksim

**Artist:** Maksim Martynenko — [@Ichnolith](https://github.com/Ichnolith). PNG and LibreSprite JSON files were supplied by Siraç and preserved byte for byte. Filenames are normalized (`EmberSwap`, `Sheathing`); JSON source image paths are informational and are never followed by the importer. See [delivery hashes](delivery-sha256.json).

| Animation | Frames | Canvas per frame | Authored duration | Runtime use |
| --- | ---: | --- | ---: | --- |
| Idle | 10 | 70 × 70 | 1.0 s loop* | Standing and the animated main-menu character |
| Walk | 10 | 70 × 70 | 1.0 s loop | Movement; playback rate follows walking/sprinting speed |
| Jump | 6 | 70 × 70 | 0.6 s | Real jump event; confirmed walk-offs enter the airborne hold directly after visual contact grace |
| Landing | 5 | 70 × 70 | 0.5 s | Real landing event; movement can interrupt |
| Flare | 12 | 70 × 70 | 1.2 s | Successful flare ignition |
| EmberSwap | 34 | 70 × 70 | 3.4 s | Fuel change during interaction input; failed requests/pickups/flare spending do not normally trigger it |
| Climb | 27 | 70 × 80 | 2.7 s | Collision-checked ledge traversal; height selects the entry pose; press W / Up or controller Up |
| Sheathing | 17 | 70 × 70 | 1.7 s | First seven frames prepare a grounded climb; airborne grabs skip stowing; full clip also available via context API |

*No Idle JSON was delivered. Idle uses ten uniform 100 ms frames, matching the other seven exports. Every supplied JSON duration is 100 ms; these durations are retained in the catalog and standard Unity preview clips. Empty trailing grid cells are excluded.

## Import and integration

Use **The Lighten Road → Player → Rebuild Maksim animations** outside Play Mode. The importer uses the installed 2D Sprite Editor data-provider API, stable sprite identities and the JSON rectangles/order. It generates eight normal `.anim` preview clips and the `Resources/Player/LightAnimations` catalog, then wires the existing PlayerPrototype prefab. Its scene instances inherit the integration; scene files are not rewritten. Saving the prefab also serializes the ParticleSystem/renderer required by its existing LanternSparksFX component, which Unity previously materialized on load; the existing FX script still configures them at runtime.

Textures use point filtering, no mipmaps, no compression/resampling, full rectangular sprite meshes, no generated physics shapes, and **36 pixels/unit**. Normal registration is `(35,35)` pixels; Climb uses `(35,30)` within its taller canvas to retain a comparable foot baseline. Sprite import leaves colliders and root scale unchanged. The separate PlayerLedgeClimb component now owns deliberate traversal motion; fuel costs and mechanism state remain unchanged.

`PlayerSpriteAnimator` consumes the actual motor jump/landing events and successful lantern flare event. It observes a fuel change in the same frame as E/controller interaction to show a transfer, without modifying any interaction or mechanism script. Legacy two-charge transfers only restart once per frame. For systems that can report an interaction explicitly, call `PlayEmberTransfer()`. The observation bridge cannot distinguish an incidental pickup occurring on the exact same input frame as an interaction; that edge case can use the explicit hook later.

The point light follows a per-frame visual anchor extracted from the small warm flame pixels. When the art occludes the flame, the previous anchor is retained, except during Climb where the stowed lantern follows the shirt's belt line. This changes position only, preserving the lantern controller's light/fuel rules. Procedural squash/stretch and tilt are disabled while the authored animation component owns the visuals and restored on disable. Pause/death freeze playback; respawn clears pending poses.

PlayerLedgeClimb prepares grounded starts with 0.7 s of Sheathing, blends into a height-appropriate Climb entry over 0.12 s, then plays the remaining authored frames. Airborne grabs enter Climb directly. The catalog stores two-dimensional grip, leading-foot and sole markers, keeping the hand on the corner, lifting both feet clear of the wall and matching Idle's standing baseline. One clock drives pose and movement; `SampleContextPose(sourceTime)` samples without advancing a separate animation clock. It requires an active explicit context pose.

The collider stays active and the lift/crossing path is checked throughout. A temporary input lock ends on finish, cancellation, disable or death. Cancellation preserves the visible feet before easing out any residual offset; death retains the actual pose until respawn. Pause freezes pose and movement together. Only stationary solid ledges qualify; moving machinery is excluded. No scene or Kemal-owned source file is rewritten. Both complete clips remain available through `PlayContextAnimation(LightAnimation.Climb)` and `PlayContextAnimation(LightAnimation.Sheathing, true)`. Standalone API callers still own position, input locking and clearing a held pose through `ClearContextAnimation()`.

See [current traversal behavior and verification](../../../../../../development/sirac/PLAYER-TRAVERSAL.md). Rebuilding twice produces identical source metadata, preview clips, catalog and player prefab; original delivery hashes remain unchanged.
