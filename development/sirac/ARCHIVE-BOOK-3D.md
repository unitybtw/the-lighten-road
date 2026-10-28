# Archive book: 3D model and direct page manipulation

The reader now instantiates `Game/Assets/_Project/Resources/UI/ArchiveBook3D/ArchiveBook.prefab`. Its hardcover panels, bevelled spine, raised bands, brass fittings, cloth bookmark, folio blocks, spine gutter backing, and paper leaves are separate editable mesh objects. Meshes and materials are saved beside the prefab. `The Lighten Road > Archive > Rebuild 3D Book Model` regenerates these authored assets from `ArchiveBookModelBuilder`.

The source handwriting canvas renders to a private 2048 × 1464 texture. The texture is printed onto paper geometry rather than displayed over a picture of a book. A held sheet snapshots its front-side ink and deforms through a travelling, tessellated curl. Its reverse is printed from the following spread, rather than mirroring the old record. The following spread is rendered into a separate preview texture underneath it. Previewing restores the logical record/page immediately and never publishes discovery events, awards fuel, or changes narration. Cancelling restores the original texture as well as the original selection. Geometry shading, leather grain, paper edges, seamless spine crease overlap, an ambient gutter fold shadow, and a moving page shadow come from the archive surface shader.

## Controls

- Hold the left mouse button on either page's top, bottom, outer edge or blank paper. The narrow sewn binding stays pinned; voice and navigation buttons retain their own targets.
- The picked material point tracks both screen axes. Diagonal pulls deform the free paper locally; vertical displacement uses the strip's available length instead of simply stretching its width. Pull distance is bounded by the distance from the sewn binding. Holding still never advances the turn.
- Releasing while the picked point remains on its original side of the spine returns the sheet and leaves the selected page unchanged. A vertical-only pull also returns without advancing.
- Releasing after the picked point crosses the spine completes exactly one forward/backward page turn, including when grabbed near the spine at a top or bottom edge.
- Next / Previous buttons trigger a 1.2-second quintic smootherstep turn. Mouse release settles over 0.38–1.06 seconds according to the distance remaining, with a gentle start and landing. Selection and narration change only once the sheet lands.
- Previous/Next, Left/Right, and the controller bumpers remain available.
- Close or losing application focus cancels an unfinished gesture. The cover lifts towards the reader, and the left folios rotate continuously with it instead of appearing halfway through the opening. Reading continues to pause gameplay.
- Reduced Motion keeps direct manipulation but snaps the release and button-driven transitions.

Voice controls retain their own click targets. Button-origin drags never capture a sheet; the modal pixel display also blocks clicks into the world behind it. A mesh UV raycaster routes clicks to those controls before treating blank paper as a draggable edge. The original text, clues, discovery state, and narrator queue remain in the Codex menu. Within-record page turns keep narration continuous.

## Spine binding and crease continuity

- The left and right readable paper leaves meet at the spine centerline with a 4mm seamless tuck overlap, eliminating any vertical gap, seam, or background void between the pages.
- A dedicated `Spine gutter backing` mesh bridges across both folio blocks directly beneath the valley.
- The paper geometry curves naturally into the binding valley (+Z depth into the book) rather than bulging outwards toward the camera.
- The archive surface shader calculates an ambient contact shadow along the gutter fold (`abs(uv.x - 0.5)`), creating a rich, physical spine valley.

## Rendering and lifecycle

The live ink canvas uses layer 30, the frozen following-spread canvas uses layer 29, and the model uses layer 31. Both private cameras and render textures are owned by the reader. The recovery feed and the screen footer have separate overlay canvases. Existing external overlay canvases are hidden only while the modal reader is open and restored on close. Previously disabled canvases stay disabled.

The model's dynamic sheet mesh, cloned paper materials, texture snapshot, and ink render texture are released with the reader. The prefab's original meshes and materials are not mutated at runtime. The model framing fits narrow and wide aspect ratios independently from the fixed ink layout. Opening framing accounts for the raised cover. The reading frame reserves the full curled-paper sweep in advance; the camera stays fixed while holding or settling a sheet, so perspective changes cannot pull the paper away from the cursor.

## Editor reload safety

The menu rebinds nonserialized gesture delegates when enabled and rebuilds only its own hierarchy if runtime native resources were lost during a script reload. Uninitialized pointer receivers ignore input. Narration cleanup accepts empty request paths after Unity serialization and releases owned clips on disable.

## Pixel art rendering

The 3D stage renders at half the display dimensions (one third on very tall displays) into a point-filtered target. A full-screen modal overlay presents these exact pixels; pointer coordinates are mapped through viewport space so lower render resolution never changes hit targets. Exported previews also use nearest-neighbour 2× enlargement. Surface shading uses five diffuse bands, a 5-bit colour ramp, and coarse procedural texels instead of fine photoreal grain. Handwritten story copy uses a stronger weight for legibility at this resolution.

## Model and interaction repairs

Binding corner radii are constrained by each part's width and height, and bevel depth by its actual thickness. This prevents narrow fittings and thin signatures from folding inside themselves. The opening cover remains the same physical object after it reaches the left side; no replacement cover pops into view at the end. The original opposite page remains visible while the next page is previewed beneath the held sheet. The reverse ink comes from its own preview texture. Page gestures retain a single pointer owner and button-origin drags do not capture paper.

Regression coverage checks horizontal cursor tracking (within 1.5 screen pixels), two-axis grip tracking from top/bottom/outer edges (within 3.5 screen pixels), stationary holds, gradual release, commit-on-landing, cancellation, backward turns, Input System mouse events, paper-strip lengths, thin-part bounds and triangles, reverse ink, camera stability, and pixel filtering. Runtime checks use the Unity CLI and the existing Office scene.

## Validation, 28 October 2026

- Unity CLI compilation: no errors or warnings.
- Archive/Codex/narration suite: 38/38 passed after the interaction and paper repairs.
- Final interaction suite, including the added gutter-depth regression: 19/19 passed after moving the backing behind the paper.
- Office Play Mode: opened the real ledger and inspected flat, early, middle and late turns at 1920 × 1080. The reader is left open for manual review.
- The current Console reports no errors or warnings. Project integrity and `git diff --check` pass. Interaction, Mechanisms and Kemal's sandbox remain unchanged.

Previews: [open](previews/archive-book-refined-open.png), [early curl](previews/archive-book-refined-early.png), [middle curl](previews/archive-book-refined-middle.png), [late curl](previews/archive-book-refined-late.png).

## Any-edge dragging and bookmark repair

The gesture solver handles horizontal and vertical cursor coordinates together, compensating for perspective and for the changing paper bend. The paper remains attached along the sewn spine. A release eases both the turn and the vertical deformation back onto the stack. Clicking without moving returns immediately without blocking the reader. The camera cannot reframe during a held or settling gesture; offscreen preview exports may fit their temporary capture frame independently.

The cloth bookmark's head is routed behind the printed paper, and the tail exits below the cover. Its exposed length is reduced to about 0.24 model units and ends in a small V notch. It no longer protrudes through the narration controls as a long square strip.

Office captures for the any-edge update: [open book and tucked bookmark](previews/archive-book-anyedge-open.png), [top-edge pull](previews/archive-book-anyedge-top.png), [bottom-edge pull](previews/archive-book-anyedge-bottom.png), [left-page pull](previews/archive-book-anyedge-left.png).

Final any-edge validation: Unity CLI compilation has no errors or warnings; all 45 Archive/Codex/narration PlayMode tests pass. The suite includes top/bottom/outer grabs on both sides, two-axis tracking, vertical-only cancellation, near-spine release decisions, pinned binding, no paper/stack penetration, and the tucked bookmark. Office captures were visually inspected after the final deformation change. Project integrity and whitespace checks pass; Kemal-owned paths remain unchanged.
