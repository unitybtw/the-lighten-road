# Reusable chamber completion

`ChamberExit.prefab` is a player-only exit trigger with a warm URP 2D light wave, restrained English chapter typography, an original short chime, a full-screen fade and optional travel. No mechanism or interaction scripts are required.

## Use in another chamber

1. Drag `Assets/_Project/Prefabs/ChamberExit.prefab` into the scene and position/resize its trigger at the exit. Keep this as a dedicated object, not a child containing other gameplay systems: it briefly persists across a scene change.
2. Duplicate `Resources/Completion/FirstLightCompletion.asset` or use **Create > The Lighten Road > Chamber Completion Profile**. Assign the new profile to the sequence. Set the title, subtitle, font, warm light color/radius/intensity, durations and optional sound clip/volume. Profiles can be shared when the presentation should match.
3. Set **Destination Scene** to the full path of an enabled Build Settings scene. Leave it empty to celebrate in the current scene; optionally assign **Destination Spawn** to move the player to a same-scene checkpoint under the opaque curtain. This restores the standard three-ember chamber budget.
4. Use **Started**, **Screen Covered**, **Finished** and **Cancelled** events for your own room bookkeeping. A puzzle can call `TryBegin(player)` directly instead of using the trigger. No global scene-name or build-index assumptions are embedded in the prefab.

The tutorial instance is wired to `ArchiveTestChamber.unity`, the existing Siraç prototype. This is a development route, not a newly authored story chapter. Reassign its destination when the team's next chapter is ready. The reusable prefab itself has no destination set.

## Sequence and recovery

Default timing: 1.15s light/title reveal, 1.45s hold, 0.8s fade to near-black, asynchronous scene load, 0.9s fade in. The loader is covered completely. The destination player is briefly gated during fade-in, then regains control. Source HUDs remain scene-owned; they are not carried to the destination.

The local player's motor, lantern input and survival controller are temporarily disabled and their previous enabled states restored. Rigidbody simulation/velocity are preserved. Global `Time.timeScale` is unchanged; presentation uses unscaled time. Existing Effects/Master audio preferences apply. Reduced Motion replaces the expanding ring with a stationary light fade and removes caption movement.

Repeated or simultaneous exits are rejected. A missing profile, dead player or invalid destination is rejected before control is locked. `Cancel()` restores control and removes the presentation before loading starts; once loading starts it must finish. Disabling/destroying the sequence also releases its locks. `ResetCompletion()` rearms a finished local exit for an authored retry. A cross-scene sequence destroys its temporary persistent root after fade-in. Generated ring texture/sprite and the audio source are cleaned up.

## Assets

`Resources/Completion/ArchiveArrival.wav` is an original procedural 2.7-second mono PCM chime, synthesized for this project with four soft, decaying notes. It has no third-party recording or licensing dependency. Replace it through the profile without editing code.

## Verification

Unity 6000.3.2f1 PlayMode coverage includes event ordering and a fully opaque load boundary, duplicate rejection, cancellation/disable recovery, previously disabled component preservation, zero-timescale operation, invalid destinations, a same-scene checkpoint and the actual tutorial trigger loading the archive. Test results and visual review are recorded in `development/sirac/README.md`. No standalone build is claimed by these editor checks.
