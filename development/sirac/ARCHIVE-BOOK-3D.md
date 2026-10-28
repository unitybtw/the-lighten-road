# Archive book: 3D model and direct page manipulation

The reader now instantiates `Game/Assets/_Project/Resources/UI/ArchiveBook3D/ArchiveBook.prefab`. Its hardcover panels, bevelled spine, raised bands, brass fittings, cloth bookmark, folio blocks, and paper leaves are separate editable mesh objects. Meshes and materials are saved beside the prefab. `The Lighten Road > Archive > Rebuild 3D Book Model` regenerates these authored assets from `ArchiveBookModelBuilder`.

The source handwriting canvas renders to a private 2048 × 1464 texture. The texture is printed onto paper geometry rather than displayed over a picture of a book. A held sheet snapshots its front-side ink, deforms through a tessellated bend, and exposes the opposite side as it crosses the spine. The following spread is rendered into a separate preview texture underneath it. Previewing restores the logical record/page immediately and never publishes discovery events, awards fuel, or changes narration. Cancelling restores the original texture as well as the original selection. Geometry shading, leather grain, paper edges, and a moving gutter shadow come from the archive surface shader.

## Controls

- Hold the left mouse button on either outer page edge, then drag across the spine.
- Releasing before the halfway point returns the sheet and leaves the selected page unchanged.
- Releasing past halfway completes exactly one forward/backward page turn.
- Previous/Next, Left/Right, and the controller bumpers remain available.
- Close or losing application focus cancels an unfinished gesture. The cover lifts towards the reader, and the left folios rotate continuously with it instead of appearing halfway through the opening. Reading continues to pause gameplay.
- Reduced Motion keeps direct manipulation but snaps the release and button-driven transitions.

Voice controls retain their own click targets. Button-origin drags never capture a sheet; the modal pixel display also blocks clicks into the world behind it. A mesh UV raycaster routes clicks to those controls before treating blank paper as a draggable edge. The original text, clues, discovery state, and narrator queue remain in the Codex menu. Within-record page turns keep narration continuous.

## Rendering and lifecycle

The live ink canvas uses layer 30, the frozen following-spread canvas uses layer 29, and the model uses layer 31. Both private cameras and render textures are owned by the reader. The recovery feed and the screen footer have separate overlay canvases. Existing external overlay canvases are hidden only while the modal reader is open and restored on close. Previously disabled canvases stay disabled.

The model's dynamic sheet mesh, cloned paper materials, texture snapshot, and ink render texture are released with the reader. The prefab's original meshes and materials are not mutated at runtime. The model framing fits narrow and wide aspect ratios independently from the fixed ink layout. During opening or a held turn, framing also accounts for the raised mesh bounds so the lifted paper stays inside the view. The camera eases back after the sheet settles.

## Editor reload safety

The menu rebinds nonserialized gesture delegates when enabled and rebuilds only its own hierarchy if runtime native resources were lost during a script reload. Uninitialized pointer receivers ignore input. Narration cleanup accepts empty request paths after Unity serialization and releases owned clips on disable.

## Pixel art rendering

The 3D stage renders at half the display dimensions (one third on very tall displays) into a point-filtered target. A full-screen, noninteractive overlay presents these exact pixels; pointer coordinates are mapped through viewport space so lower render resolution never changes hit targets. Exported previews also use nearest-neighbour 2× enlargement. Surface shading uses five diffuse bands, a 5-bit colour ramp, and coarse procedural texels instead of fine photoreal grain. Handwritten story copy uses a stronger weight for legibility at this resolution.
