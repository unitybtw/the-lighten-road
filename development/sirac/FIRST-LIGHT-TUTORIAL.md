# First Light — standalone tutorial prologue

Open `Game/Assets/_Project/Scenes/Sandbox/Sirac/FirstLightTutorial.unity` and press Play. The scene is enabled in Build Settings. Its white passage leads to the existing first gameplay scene, `FurkanTutorial`. The main menu's existing route is preserved; this scene can be reviewed independently before inserting it into the menu flow.

## Player experience

1. **Awakening:** absolute black. An ember appears, with a quiet warm halo, a small orbit and restrained title typography. Decorative camera motion and rings respect Reduced Motion.
2. **Find the ember:** a short movement hint appears. Approach the ember using the existing keyboard or gamepad movement controls. The player cannot die while waiting in this initial darkness.
3. **First ignition:** the real pickup adds one ember. A warm light wave expands and the lantern fades into its normal Dim state. The HUD appears only after ignition. Normal darkness survival is restored.
4. **Carry the light:** walk through the decayed archive, jump a low fallen stone, then cross a 1.6-unit break in the floor. The tutorial listens to the real motor's jump event, rather than acknowledging a key press regardless of whether a jump occurred.
5. **Keep a reserve:** collect two more real ember pickups on the safe landing. The inventory reaches three embers without changing the game's shared four-ember maximum.
6. **Wake the seal:** stand beside the three sleeping marks and use Flare. Dim light does not charge them, and a remote flare outside the seal chamber does not solve the puzzle. Each visible mark charges over 1.2 seconds under sufficient actual lantern light. One successful Flare permanently spends one ember and reveals the route.
7. **A way through:** the three traces travel to the passage. Its white outline draws around an arch; bright white light fills it and slow light motes appear. The player is held briefly while the camera frames this reveal. The traces remain complete after the door becomes ready.
8. **White passage:** walk into the finished door. A white curtain conceals an asynchronous load of `FurkanTutorial`, fades away, and restores controls. The transition owns only its temporary curtain across the scene swap, never the tutorial geometry.

## Guidance, retry and accessibility

- All tutorial text is English, with short context-sensitive hints. The move, jump and Flare prompts come from `ControllerGlyphService`, so keyboard, Xbox and PlayStation wording follows existing controls.
- The first pickup and the seal have local retry positions. Death at the seal restores the existing three-ember chamber reserve, so an unsuccessful attempt cannot consume the only solution permanently.
- Flare illumination, cost, transition timing, HUD, movement and death use the current shared implementations. There is no second lantern or player controller.
- Pause stops the tutorial timeline. The scene transition blocks pause while its curtain is active. Disabling/unloading releases the tutorial's input block.
- Reduced Motion removes the orbit, ignition ring, decorative zoom and motes; the door outline appears without a travelling stroke, with the light fade retained.

## Editable implementation

The scene hierarchy contains named architecture, floor colliders, pickups, three mark positions, editable line paths, portal, cameras, lighting and cinematic UI. Runtime scripts live under `Game/Assets/_Project/Scripts/Tutorial/FirstLight/` in their own assembly. Generated point-filtered sprites, the unlit material and a restrained bloom profile live under `Game/Assets/_Project/Art/FirstLightTutorial/`.

`The Lighten Road > Tutorial > Create First Light Scene` is the reproducible initial builder. It refuses to overwrite the existing scene or discard a dirty open scene. Edit the existing scene objects to adjust the layout. Runtime presentation reads authored conduit paths before animating them.

Kemal's Interaction scripts, Mechanisms scripts and sandbox are not changed. Existing scenes, player and pickup prefabs remain intact. The only shared project setting change is registering the new scene in Build Settings.

## Validation

The scene-specific PlayMode suite covers the initial black screen, waiting safely without fuel, actual pickup/ignition, actual Input System jump, reserve collection, Dim/remote-Flare rejection, Flare cost and mark charging, ordered door reveal, checkpoint retry, transition cleanup, Reduced Motion and releasing input on disable. The separate walkthrough uses Input System movement/jump/Flare events and records its result in ignored `Game/Library/Readability/first-light-walkthrough.json`.

The walkthrough crossed both obstacles without teleporting or forcing tutorial states, solved the seal with one Flare and loaded `FurkanTutorial`. After the destination's fade completed, gameplay input was available and no transition curtain remained. Screenshots show the seed, ignition, Flare, forming door and completed white passage in `development/sirac/previews/first-light-*.png`.

Final results: 9/9 PlayMode tests passed, compilation reported zero errors/warnings, and the project integrity inspection passed for 552 assets including the new uncommitted scene/assets. The final keyboard walkthrough completed every stage and loaded the first level. Its 18.9-second duration measures a scripted player already knowing each jump, not a novice pacing estimate. Physical gamepad operation and listening checks were not repeated; the scene uses the existing controller bindings/prompts. Unity reported host audio-device and Metal memoryless-depth warnings during capture; no runtime exception was reported in the tutorial walkthrough.
