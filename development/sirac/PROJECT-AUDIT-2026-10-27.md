# Stability pass — 27 October 2026

Scope: player input/respawn, pause ownership, DualSense feedback and editor scene tooling. Kemal's Interaction, Mechanisms, sandbox and development files are unchanged. Existing local font and project-setting edits are excluded from this change.

## Corrections

- Lightbar Bluetooth updates now enable only the LED channels. They no longer reset motors or adaptive triggers, disturb their rate limiter, or overwrite their last applied state.
- Failed trigger writes remain retryable. Throttling does not report an unapplied trigger transition as successful; the idle trigger owner retries it on the next frame. The Bluetooth sequence registry is cleared under the same lock used by the speaker worker.
- Idle flint resistance observes the vibration master switch and service shutdown. Changing the current controller explicitly releases the previous controller's idle effect.
- Lightbar startup reads the saved preference. Focus loss and application pause keep both ambient and special sequences dark. New controllers receive their first color even when it matches a previously connected controller; failed writes are not cached as successful.
- Disabling a paused menu restores its previous time scale, gameplay input and suspended interaction polling. Destroying a duplicate pause controller cannot change the active controller's paused state.
- Death blocks gameplay actions while the death UI owns input. Respawn consumes the confirmation frame; holding R2 across death/retry does not spend an ember. Disabled/destroyed respawn controllers release their input ownership.
- Swaying-prop installation is explicitly invoked through **Tools → The Lighten Road → Install Swaying Props to Scenes**. Opening Unity no longer rewrites two scenes. Manual installation uses Unity's normal unsaved-scene handling and restores the original multi-scene setup.

## Verification

**235/235 Unity 6000.3.2f1 Play Mode tests passed, with no failures or skipped tests** (`Game/Library/ProjectAudit-full-tests.xml`, ignored). After the final controller-ownership refinement, **8/8 focused lifecycle regressions also passed** (`Game/Library/ProjectAudit-tests.xml`, ignored). This includes a failed trigger write followed by a controller switch and explicit release of the old controller. The full suite includes real scene routes as well as frontend, player, lantern, completion, relays and glyph tests. New synthetic-controller regression cases inspect outgoing HID packets and exercise shutdown, controller switching, retry, focus and death/pause input ownership. These tests establish software behavior; they do not establish physical haptic feel or speaker audibility.

Live Editor review: MainMenu entered Play Mode after cleanup; the menu rendered and the Console showed zero errors or warnings. The two scene files remained unchanged after refresh.

Local license-free checks: project integrity passed; 5 CI-checker tests and 4 team-activity tests passed. Tests preserve the user's saved options and chapter progress. Native controller feel, standalone builds and GitHub-hosted Unity execution remain separate verification targets.
