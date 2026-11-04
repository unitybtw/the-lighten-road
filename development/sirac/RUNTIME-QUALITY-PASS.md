# Runtime quality pass

This pass removes unnecessary runtime work and inconsistent behavior while retaining
the current HUD, book art, menu composition and direct Push animation.

## Changes

- Narration caches normalized speech and its content key per record. Title or story
  edits invalidate the cache, and authored audio still takes precedence.
- Book framing stops scanning renderer bounds once the camera settles. Opening,
  reopening and aspect changes invalidate it. A closed reader defers render-target
  resizing until it is shown; preview captures preserve the live camera's pending fit.
- An automatically installed codex belongs to the room containing its player. An
  additive load cannot accidentally attach it to the previous active scene and lose
  the reader when that scene unloads.
- Removed hidden music/effects keyboard mute and volume controls. Volume remains
  controlled by the visible audio settings; explicit scripting APIs remain available.
- Reduced Motion keeps menu text, optional flame markers and selection ornaments
  stationary while retaining focus feedback. It no longer suppresses the recorded
  menu lantern sound. Skipping an already completed intro does not replay that cue.
- MovementSandbox and ArchiveTestChamber now use 1.6 m gaps, with matching floor,
  hazard and edge markings. The gallery has three spikes instead of four. Their
  builders and full-route keyboard drivers use the walking-only rules.
- Removed stale sprint expectations from controller and control-guide checks.

Kemal's Interaction, Mechanisms, sandbox and development directories were not edited.

## Measurements

Warm Unity 6000.3.2f1 Editor microbenchmarks in Office, 1,000 calls per sample:

| Work | Microseconds per call |
| --- | ---: |
| Legacy normalization and hashing for a narration query pair | 78.61 |
| Cached narration query pair | 0.14 |
| Full book renderer-bounds refit | 16.95 |
| Settled book framing check | 0.012 |

The narration comparison isolates the previous repeated normalization/hash work.
The book comparison uses a forced full refit versus the settled path. These are
CPU microbenchmarks, not frame-rate, GPU, standalone-build or total-memory results.

This Mono runtime returned zero from `GetAllocatedBytesForCurrentThread` even for
an allocation control, so those readings are omitted. A separate legacy-query
control increased the Mono used heap by about 6.6 MB over 1,000 pairs. The regression
test checks warmed-query heap growth with the native profiler counter instead.

## Verification

- Full Play Mode sweep: **435/436 passed**. The remaining failure was the manual
  respawn fixture's fixed-frame input timing; it also passed in isolation before the
  fixture correction. The corrected fixture waits for delivered/released input and
  natural gameplay eligibility, without resetting or bypassing gameplay locks.
- After that test-only correction, **PlayerRespawnTests: 9/9 passed**. The full
  436-test sweep was not repeated after this fixture-only change.
- Separate integration runs: **Office: 12/12**, **Ruined Archive route: 3/3**.
  The later full sweep also passed both walking-only room routes and the new
  additive-reader lifecycle test.
- Compilation completed without errors or warnings. Project metadata/GUID checks
  passed. The book and the revised service crossing were visually reviewed.

Tests used Unity Editor with synthetic keyboard/controller input. A standalone
build, physical-controller playthrough and overall FPS/GPU profile were not run.
Local reports, benchmark source and previews are under
`Game/Library/Readability/QualityPass/` (ignored by Git).
