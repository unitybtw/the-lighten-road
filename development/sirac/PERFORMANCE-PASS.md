# Runtime optimization pass

The pass preserves lighting quality, movement/flare tuning, frame-rate preferences, pixel style and the authored scene layouts. Kemal-owned Interaction, Mechanisms and sandbox paths are unchanged.

## Changes

- **Archive paper:** one reusable vertex/UV/index workspace per animated leaf. UVs and triangles are uploaded only when the page side/topology changes. Fixed curve samples are precomputed; row constants are evaluated once per strip instead of thousands of times in the cursor solver. The inverse cursor constraint and the displayed geometry share the same curve.
- **Reader framing:** cache the full paper-sweep sample points instead of integrating the page curve for every sample on every idle frame. Paper raycasts no longer allocate a temporary collider array. Repeated opening poses skip redundant transform updates.
- **Scene entry:** automatic Codex installation now creates a lightweight standby listener and discovery toast. The book model, handwriting canvases, private cameras, large render textures and narrator component are created on the first valid read and reused thereafter. Discovery and stored replay remain live before the reader is built. Only the currently selected recording is requested on this automatic path; it no longer starts preparing every recording at scene entry.
- **Reader lifecycle:** only the reader owning the pause may restore time/input. An unopened standby cannot unpause another open reader. Narration clock text is reformatted only when the displayed second or clip changes.
- **Tutorial:** reusable line traces cache segment lengths and skip unchanged strokes; hints rebuild only when the lesson, input style or seal-region condition changes. Completed paths stay complete after the reveal.
- **HUD/controller prompts:** glyph dictionaries are keyed by the existing glyph name, avoiding repeated resource-path construction on cache hits. Flare prompt text/icons refresh when bindings or controller style actually change.
- **Pickup effect:** absorption bursts share the existing particle material. Destroying a burst previously left its newly instantiated material behind.

## Measurements

Warm synchronous microbenchmarks in Unity 6000.3.2f1 Editor on this Mac, same scripted inputs and loop counts before/after. These measure the named C# operations, **not whole-frame time or build FPS**. Smaller microsecond-level changes are noisy; the large paper/framing differences are the useful signal.

| Operation | Before | After | Time reduction |
|---|---:|---:|---:|
| `book_mouse_drag` | 3.9732 ms | 1.6835 ms | 57.6% |
| `book_mesh_curl` | 0.6141 ms | 0.2176 ms | 64.6% |
| `book_idle_framing` | 0.8281 ms | 0.0188 ms | 97.7% |
| `tutorial_door_ready` | 0.0097 ms | 0.0067 ms | 30.9% |
| `lantern_hud` | 0.0039 ms | 0.0034 ms | 13.4% |

The old held-sheet update created roughly 69 KB of logical managed arrays each call (1,625 vertices, 1,625 UVs and 9,216 triangle indices, plus array headers). The host's coarse positive Mono-heap deltas showed about 77,824 bytes/call before and no positive growth in the warmed mesh/drag loops afterwards. This is not a complete allocation counter: small allocations or memory reused inside the committed heap can be missed. The zero-returning `GC.GetAllocatedBytesForCurrentThread` on this Editor was rejected after a 1 MiB sanity allocation; it is not used as evidence.

`standby.json` confirms that the automatic reader is installed at tutorial entry, with **no book cameras, no built presentation and no narrator component**. The discovery feed remains available. The first actual read still incurs its necessary construction; the optimization moves that cost away from every ordinary level load, rather than eliminating it.

## Verification

- Full PlayMode run: **305 passed, 0 failed, 0 skipped**. New regressions exercise lazy creation/reuse, the discovery toast before opening, pause ownership, changing controller/binding prompts and shared burst materials. Existing page grip, paper bounds, narration, movement, HUD, tutorial and completion tests also passed.
- Compilation: no errors or warnings. Project integrity and whitespace checks pass.
- The first aggregate attempt hit a Unity Test Framework play-mode transition error and was cancelled; it is not counted. The clean full run is the one recorded in `tests-summary.json`.
- Metrics and reproducible harness: `before.json`, `after.json`, and `scripts/performance/measure_runtime.py`. In Play Mode with FirstLightTutorial active, run `python3 scripts/performance/measure_runtime.py`. The harness pauses the tutorial and opens/closes the book; stop Play Mode afterwards to reset it. It explicitly collects garbage only outside timed measurements; no forced collection is introduced into gameplay.
- Existing dirty font-atlas assets are not part of the authored optimization changes. No contributor-owned scene or mechanism was edited.

The final input-only tutorial walkthrough again completed Awakening → FindEmber → Ignition → Move → Jump → Gather → Scout → DrawingDoor → DoorReady → Departing and loaded FurkanTutorial. The destination had gameplay input available, no remaining white curtain and an unbuilt standby reader. Its screenshot was visually checked against the prior door scene; smoke-test images remain in ignored Library/Readability/optimization-previews. Unity was left in Edit Mode on FirstLightTutorial for review.
