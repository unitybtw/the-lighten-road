# Chamber completion and transition

The existing exit now reveals a restrained warm light, briefly identifies the completed chamber, and fades to darkness over the scene change before returning control. The central title uses the archive display font, with a soft local shadow so the level remains visible. The full-width notification strip, decorative expanding ring, and unsupported flavor line have been removed. The existing memento state is retained in a short, readable status when the chamber tracks it. The profile retains optional subtitle and sound fields for deliberate, authored uses.

The First Light profile uses a 0.9 s reveal, 0.85 s hold, 0.7 s cover and 0.75 s uncover. Reduced Motion suppresses title movement; the light and cover still communicate the transition. Existing input ownership, cancellation, destination validation and scene transfer logic remain in the reusable `ChamberCompletionSequence`.

## Reusing it

Assign a `ChamberCompletionProfile` and an enabled Build Settings scene path to `ChamberCompletionSequence` on a trigger with `ChamberExitTrigger`. The trigger accepts the entering player or an explicitly assigned player. For a transition within one scene, leave the destination path empty and assign `DestinationSpawn`. Empty path and spawn show the completion locally without moving the player. The existing First Light exit in `FurkanTutorial` leads to `ArchiveTestChamber`, which is currently a test room.

## Verification

Unity 6000.3.2f1 compiled without errors or warnings. All eight completion Play Mode tests passed, including actual scene transfer, cover ordering, input release, local destination and cancellation. Project integrity passed for 599 tracked assets. The [Game view preview](previews/completion-refined.png) shows the current level exit; its hold was temporarily extended in Play Mode to allow capture and was not saved. Saved preferences and the original FirstLightTutorial editor scene were restored. No standalone build, physical controller, or audio listening pass was performed.
