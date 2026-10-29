# Menu character and contact continuity

The main menu now uses Maksim's ten-frame **Idle** sequence from the same Resources catalog as the player. Both the saved MainMenu scene and its builder select the new first frame; runtime loading no longer depends on an Editor-only character lookup. The delivered PNGs, JSON timelines and sprite identities remain unchanged.

The full 70 × 70 cell uses a 420px base square (6× before the existing stage zoom), with a shared 6.5px foot registration so the feet stay on the existing overlook. The lantern bloom and small flame core follow the active frame's imported flame position. The authored sprite is not stretched by the previous procedural breathing effect. Reduced Motion freezes the current sprite/anchor and resumes from that position when disabled. Background images remain decorative and do not intercept pointer input.

## Small seams versus actual falls

The animator previously selected Jump immediately whenever the motor lost a ground contact, including brief seams and the upward contact response on the opposite edge. This also replayed take-off anticipation for ordinary walk-offs, and the following contact could trigger an unnecessary Landing flinch.

PlayerSpriteAnimator now waits through a **0.12 s visual contact grace** and checks that a supported character has actually dropped more than **0.16 units** while descending before starting a walk-off pose. A confirmed fall begins directly at the delivered airborne hold, without a fake take-off. A real OnJumped event bypasses these checks and plays the complete jump immediately. Landing is shown after a confirmed air pose; short contact interruptions retain locomotion. Reset, respawn and explicit contexts clear the tracking state.

These checks affect presentation only. IsGrounded, collision shape, gravity, movement, jump/coyote timing and the map's real gaps are unchanged. Kemal's protected scripts and scenes are unchanged.

## Validation

The new regression tests cross two real floor colliders separated by a narrow seam and require the motor to report an actual contact interruption while locomotion stays continuous. Separate tests verify a real walk-off and an immediate deliberate jump. Menu tests check the runtime catalog against an old art override, sprite timing, pixel scale, lantern registration, Reduced Motion and wraparound.

Final checks: **132/132 player tests** and **162/162 frontend tests** passed, with zero failures or skips. These are the two scoped assemblies, not a full-project run. The actual MainMenu scene displayed Idle_005 and then Idle_002 from the delivered Idle.png; their flame anchors changed with the art while character scale stayed (1, 1). The Game view was inspected at 1920 × 1080. The saved scene changes only its serialized character reference, and the prefab changes only the two presentation thresholds. The 26 menu preference keys were backed up for these tests and restored afterward.

[Main-menu preview](previews/animation-consistency/main-menu-updated.png) · [Validation summary](animation-consistency-validation.json)
