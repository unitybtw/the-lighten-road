# Recovery and reader boundaries — 29 October 2026

This pass corrects the recovery notification contract, book ownership of interactions, and narration gain. Seven new Play Mode cases failed against the previous runtime code before applying the fixes.

## Recovery

`OnPlayerRespawned` used to run while the player still reported Respawning and the motor was disabled. If a listener threw, execution never reached the code that restored the player; subsequent Respawn calls then returned immediately because the controller was still Respawning. A listener reporting a new hazard was ignored for the same reason.

Recovery now synchronizes the checkpoint position on the Transform and Rigidbody, restores simulation, inventory, stamina, lantern, camera, motor and Alive state, and releases its own input lock before notifying observers. Callback errors still propagate instead of being hidden, but cannot leave the core recovery unfinished. A new death reported during notification is not overwritten afterward. Other UI owners and the existing short input-release suppression remain authoritative.

The fallback manual retry path respects time scale and other input owners while excluding its own death lock. Space/submit during pause no longer revives the player behind the menu. Controller retry uses the shared active-pad selection. The death screen's existing ownership of manual/automatic retry is preserved.

## Book and narration

- Opening the book acquires the existing legacy-polling lease as well as its modern gameplay lock, preventing raw E interaction behind the reader. Close and disable release only the reader's ownership; another modal's lease remains in force. Kemal's scripts and mechanism state logic are untouched.
- A direct Open request cannot take over another modal or traversal. This is checked before creating the lazy reader presentation. Changing records within the current reader remains allowed.
- Narration applies Effects on its AudioSource and Master through AudioListener once. Previously `Master × Effects` on the source was multiplied by Master again globally, making narration disproportionately quiet at reduced volume.

The player recovery test fixture also restores the existing total-death preference after each case, keeping test deaths out of the user's counter.

## Verification

**373/373 tests passed in one full-project Play Mode run**, with no failures, skips or inconclusive results. All seven reproduced failures pass after the fixes. Compilation is clean and project integrity passes for 599 tracked assets. The 27 saved preferences, including total deaths, were restored; the editor returned to FirstLightTutorial outside Play Mode.

See [recovery-reader-validation.json](recovery-reader-validation.json) for the before/after failures and final Unity results. Tests cover callback failure, callback-triggered hazards, checkpoint state during notification, paused retry input, nested polling leases, blocked lazy opening and narration volume. Saved preferences are restored and test-generated settings/fonts are excluded. No standalone build or physical-controller listening/feel check is claimed.
