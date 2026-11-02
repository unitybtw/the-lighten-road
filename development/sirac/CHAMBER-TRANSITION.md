# Lantern-led chamber transition

The player leaves the chamber through their own lantern. It loses intensity and
radius over 0.85 s, with spark emission falling with it; the scene fades to black.
Only then does the completed chapter title appear: a 0.75 s fade with a small rise
and tightening letter spacing, followed by a 0.85 s hold and 0.4 s fade out. Memento
status is retained when the chamber tracks it. There is no separate exit light wave.

Scene loading stays behind the opaque black curtain. After the destination's
Awake/Start, its actual lantern starts dark and relights over 1.25 s as the new room
is revealed. Control stays gated until the reveal finishes. The visual transition
does not spend an ember, reset an active scout or advance its timer. The normal
chamber-entry budget still belongs to the destination/local checkpoint setup.

Reduced Motion disables the title movement and tracking animation. Quiet, existing
licensed lantern extinguish/ignite clips follow the two light actions and respect
Master/Effects volume. The default profile no longer plays the synthetic arrival
chime; an authored completion clip remains optional.

## Reuse and recovery

Assign a profile and enabled destination to `ChamberCompletionSequence` on a
`ChamberExitTrigger`. The same profile controls all five timing stages. Leave the
scene path empty for a local completion and optionally set a checkpoint. The First
Light exit in FurkanTutorial still routes to ArchiveTestChamber, a development room.
Its fallback exit to the menu now uses the same profile defaults.

Cancellation restores component states, body simulation, velocity and the prior
lantern presentation gain. Input ownership remains held through cross-scene loading.
The persistent root cleans up after arrival. Listener failures are logged and do
not prevent completion/cleanup; synchronous cancellation from Started remains safe.
No Kemal-owned scripts or scenes are changed.

## Verification

Unity 6000.3.2f1 / Unity CLI: final compilation reported **0 errors, 0 warnings**.
The final focused run passed **12/12 completion tests**, including the real scene
swap, zero-timescale operation, interrupted light recovery and unchanged scout/fuel
state. The real-time clock starts with the animation rather than consuming the
triggering frame's previous delta, preventing an initial jump after a long frame.

The earlier broad run passed **400/401 Play Mode tests**. Its one failure was
`PlayerRespawnTests.RespawnRecoveryManualSkipWaitsUntilTheWorldIsUnpaused`; an isolated
rerun passed without changes to respawn production/test code. A clean full-suite run
is therefore not claimed. Lantern and spark tests passed in that broad run.

Actual 1920×1080 Game captures show [extinguishing](previews/lantern-transition/01-extinguishing.png),
[the title in darkness](previews/lantern-transition/02-title.png),
[the destination relighting](previews/lantern-transition/03-relighting.png) and
[arrival](previews/lantern-transition/04-arrived.png). These use the authored timing
and real FurkanTutorial → ArchiveTestChamber load; the source player was placed at
the exit for capture. Capture metadata records the scene, title/curtain opacity and
lantern visibility. No timing extension or fake world light was used.

Project integrity passed (599 tracked assets). Personal preferences and the original
FurkanTutorial editor scene were restored after verification. No standalone build,
physical-controller test or audio listening pass is claimed.
