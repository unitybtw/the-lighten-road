# Maksim animation integration

The shared PlayerPrototype now uses Maksim's eight Light sprite sheets. Its existing scene instances inherit the new visual component; the authored scene files and Kemal's Interaction, Mechanisms, Sandbox/Kemal and development folders were not edited. Artist credit and delivery details: [animation pack](../../Game/Assets/_Project/Art/Characters/LightAnimations/README.md).

Standing, moving, jumping, landing, successful Flare and ember transfer use the supplied sprites. Sprint retimes the walk loop to movement speed. Transfer art is observed from fuel changes during interaction input and can be called explicitly; it does not alter circuits. Climb and Sheathing are included as ready contextual clips/API hooks, because the current motor has no climbing or stow mechanic. Idle's missing JSON timeline is provisionally 100 ms per frame.

The importer creates stable slices from the actual JSON rectangles (including 70×80 Climb cells), eight standard Unity animation clips and a catalog with the original timings. A repeated build changed no generated asset bytes/identities; all eight normalized case-sensitive paths and sprite references were checked. Source PNG/JSON bytes match the 15 delivered files.

## Verification

- Player Play Mode assembly: **116 passed**, zero failures/skips, including six new animation cases. Movement, facing, jump/landing, successful and failed flare, fuel-transfer intent, death/respawn reset, pause, context hooks, collider/root preservation and all 121 frame records are covered. The legacy procedural squash test explicitly opts into that optional effect; the new art uses no procedural distortion by default.
- First Light tutorial: **10 passed**, zero failures/skips.
- FurkanTutorial, Office and FirstLightTutorial loaded the new prefab component/catalog in Play Mode. Furkan Game view was visually inspected for idle and the event-triggered Flare pose. The inspection camera was moved closer and the sprite clock frozen on selected frames for the two screenshots; this is visual evidence, not a frame-rate benchmark or a full traversal.
- Preview clip lengths were checked against the delivered durations. Unity's automatic final sample interval is accounted for, avoiding an extra duplicate 100 ms endpoint.
- Captures and [validation summary](player-animation-validation.json) are included. No standalone build or physical controller validation was repeated. Existing dirty dynamic font files and Python caches are outside this commit.

[Idle close-up](previews/light-animations/live-idle.png) · [Flare close-up](previews/light-animations/live-flare.png)
