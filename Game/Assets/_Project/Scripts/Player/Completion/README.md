# Reusable chamber completion

`ChamberExit.prefab` hands the transition to the player's own lantern: extinguish,
show the completed chamber title in darkness, load under cover, then relight the
destination and return control. It does not add an independent light at the exit.

## Use in another chamber

1. Place `Assets/_Project/Prefabs/ChamberExit.prefab` at the exit and resize its trigger.
   Use a dedicated object: its root temporarily persists across a scene change.
2. Assign a `ChamberCompletionProfile`. Set the English title, optional subtitle,
   font, accent color and durations. The shared First Light profile can be duplicated
   through **Create > The Lighten Road > Chamber Completion Profile**.
3. Set **Destination Scene** to an enabled Build Settings scene path. Leave it empty
   for a local completion; optionally set **Destination Spawn** for a same-scene
   checkpoint, which restores the standard three-ember budget under cover.
4. **Started**, **Screen Covered**, **Finished** and **Cancelled** support authored
   room bookkeeping. A puzzle can call `TryBegin(player)` without using the trigger.

The tutorial instance routes to Siraç's `ArchiveTestChamber.unity` prototype. Assign
its destination when the next story chapter is ready. The reusable prefab has no
scene destination set.

## Timing and presentation

The default profile uses:

- **Extinguish — 0.85 s:** the real lantern's intensity, radius and spark emission
  decrease while the screen fades to black. Fuel and scout time are unchanged.
- **Reveal — 0.75 s; hold — 0.85 s:** archive typography appears above the opaque
  curtain. A slight rise and tightening letter spacing settle into a still title.
  Existing memento status remains available for chambers that track it.
- **Fade out — 0.4 s:** the text disappears; the curtain stays fully opaque.
  **Screen Covered** then fires before any scene or checkpoint transfer.
- **Load; fade in — 1.25 s:** destination Awake/Start completes under cover. The
  destination lantern starts dark and grows back to its normal light as the chamber
  becomes visible. Control returns when this reveal is complete.

Reduced Motion keeps the text stationary with fixed letter spacing; smooth light
and opacity changes remain. No flash, expanding ring or camera shake is added.
The existing licensed extinguish/ignite clips play quietly from a transition-owned
AudioSource so a destination's scene-start sound suppression does not swallow ignition.
Master/Effects settings remain respected. **Completion Sound** is an optional authored
accent; the First Light profile leaves it empty.

## State and recovery

The local motor, lantern controller and survival controller are gated temporarily.
Rigidbody simulation/velocity, component enabled states and the lantern's previous
presentation gain are restored. `SetPresentationVisibility` changes actual emitted
light without spending fuel or advancing Flare. Source HUDs remain scene-owned.
`Time.timeScale` is unchanged and transition timing uses unscaled time.

Duplicate/simultaneous exits, dead or traversal-locked players, and invalid destinations
are rejected before locking. `Cancel()` restores the source light and control until
loading begins. Once loading starts it finishes under cover. Disable/destroy also
cleans up locks and visibility. A completed local exit can be rearmed with
`ResetCompletion()`. Cross-scene presentation destroys its persistent root on finish.
Event listener exceptions are logged without stranding the player; listeners should
still handle their own errors because UnityEvent can stop later callbacks on a throw.

## Verification

Completion tests cover actual emitted light and ordering, unchanged fuel/scout time,
cancellation/disable recovery, duplicate rejection, input ownership, zero-timescale
operation, invalid destinations, local checkpoints, listener failures and real scene
transfer. Lantern/particle tests also check visual gain and spark emission. Current
run results and preview evidence are recorded in `development/sirac/CHAMBER-TRANSITION.md`.
