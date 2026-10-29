# Runtime lifecycle fixes — 29 October 2026

This maintenance pass addresses music playback intent, resuming the FirstLight tutorial, and ownership of controls during chamber completion. Scene geometry and Kemal's interaction/mechanism sources are unchanged.

## Reproduced before editing runtime code

- **Music:** after Stop or Mute finished, Update treated the `playOnStart` preference as a permanent instruction and restarted the track. Both new regression cases failed against the previous implementation.
- **FirstLight:** disabling the director unsubscribed from jump/respawn and released its gate, but reenabling never restored those subscriptions or the current lesson's survival/input rules. New intro and post-ignition resume cases both failed.
- **Completion:** the exit disabled the motor/lantern/respawn components but did not own a shared gameplay lock or suspend legacy raw E polling. It could also start while the motor was already climbing. Both new regression cases failed.

The six failing outcomes and final passing statuses are retained in the validation report. These are actual Play Mode failures reproduced before the fixes, rather than assumptions from source inspection.

## Resulting behavior

Background music retries only a pending Play request. Stop cancels that request; mute remains in effect when settings are applied; changing the base volume during a fade-out does not cancel Stop. An explicitly unmuted track uses the latest requested volume. Non-looping clips finish once. Fade-in waits for the requested clip to become available instead of finishing while it is still loading. Startup still uses the authored play-on-start preference.

FirstLight reenables its event subscriptions and restores the gate, lantern and darkness rules for the existing stage. It keeps stage time, pickup progress and completed lessons. A director that has not initialized does not change the player's survival rule when disabled. Initial disable still releases its own gate and restores the prior survival setting.

Chamber completion rejects an active traversal or another modal/input owner before freezing the player. Once started, its own input and legacy-polling leases span the entire transition, including the gap between source and destination players during a scene load. The destination's legacy pollers are leased after it loads. Finish, cancel and disable release only this sequence's ownership. An unrelated owner remains authoritative. The existing unscaled presentation contract still allows a programmatic sequence at zero time scale when no other owner is blocking it.

## Verification

**366/366 tests passed in one full-project Play Mode run**, with no failures, skips or inconclusive results. Compilation is clean, and project integrity passes for 599 tracked assets.

See [runtime-lifecycle-validation.json](runtime-lifecycle-validation.json) for the final Unity test results and project-integrity check. Verification includes the new failure reproductions, the existing actual-scene travel test, nested ownership, and tutorial input/respawn behavior. Saved menu preferences are restored after testing; no test-generated scene or font changes are included. A standalone build and physical-controller check were not repeated.
