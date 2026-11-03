# Stamina: archive binding

The stamina meter borrows its material from the archive's books: a narrow strip of worn bookcloth, pale woven linen and two small folded iron fasteners. Its stepped edges, stitch holes and loose thread are drawn in code as point-filtered pixel sprites. The default 154-unit face uses a matching 154-pixel texture; there are no downloaded or generated image files.

The light weave shows remaining stamina from left to right. A short, muted trail retains the existing feedback for recently spent stamina. The loose thread marks the end of the remaining weave, and low reserve turns the weave to fired clay. **LOW** and **EXHAUSTED** remain textual warnings, so the state does not depend on color alone. The labels are larger than the previous design. Routine Ready/Rest/Sprinting text and the lungs icon have been removed.

The cloth stays still. On full recovery, the fasteners brighten briefly; there is no moving shine or continuous pulse. The meter keeps its existing reveal, full-reserve idle fade, safe-area offset and Reduced Motion behavior. Sprint costs, regeneration, exhaustion thresholds and input are unchanged.

Legacy scene settings with a three-pixel channel use a seven-pixel woven face at runtime. Scene files, artist assets and Kemal's paths are unchanged.

## Verification — 3 November 2026

- Unity 6000.3.2f1 compiled with zero errors/warnings. The player Play Mode assembly passed **164/164** tests in 67.55 seconds, including existing stamina fill, spending trail, exhaustion/recovery, idle reveal/hide, reduced-motion and rebinding cases. No gameplay rules or test expectations were changed for this design.
- Inspected the actual Game view at 60%, 20% and 0% stamina, plus the exhausted state at 1280×720. TMP reported no overflow for either label; the 64.61-unit exhausted text fits its 94-unit row. The detail image below consists of actual 1080p capture crops enlarged 2× with nearest-neighbor sampling.
- Captures freeze the gameplay clock in FurkanTutorial and set the actual PlayerStamina reserve to reproducible values. Those temporary runtime changes were not saved. Original preferences and the starting editor scene were restored. Standalone builds and other test assemblies were not rerun for this visual change.

[State details](previews/stamina-binding/states-detail.png) · [In the scene](previews/stamina-binding/reserve-1920.png) · [Exhausted at 720p](previews/stamina-binding/exhausted-1280.png)
