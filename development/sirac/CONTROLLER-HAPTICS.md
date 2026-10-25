# Controller haptics

2026-10-25 — Unity 6000.3.2f1, Input System 1.17.0.

## Feel and event mapping

Feedback is brief and selective for the ruined archive's quiet atmosphere. There is no continuous idle vibration. Both motors use a short attack followed by a smooth release; Flare builds over 45 ms. Values are normalized output ceilings before the envelope and optional strength multiplier.

| Cue | Low motor | High motor | Duration | Trigger |
| --- | ---: | ---: | ---: | --- |
| Jump | 0.10 | 0.16 | 65 ms | Active player jump |
| Landing | 0.40 | 0.18 | 160 ms | Impact above 8 units/s; strength scales from 25% to 100% over 8–22 units/s |
| Flare | 0.32 | 0.42 | 280 ms | Lantern enters Flare |
| Ember pickup | 0.10 | 0.24 | 130 ms | Successful ember collection |
| Ember break | 0.18 | 0.40 | 150 ms | Active player's lantern raises EmberBroken |
| Death | 0.52 | 0.66 | 380 ms | Death overlay opens |
| Chamber complete | 0.28 | 0.36 | 480 ms | Exit presentation starts |
| Settings preview | 0.18 | 0.28 | 160 ms | Vibration is enabled in title/pause settings |

Named cues are available through `ControllerGlyphService.PlayHaptic(HapticCue.Flare, strength)`. Existing `Rumble(low, high, seconds)` callers remain supported. Named priority prevents jump/pickup feedback from replacing a death or chamber-completion cue. These are traditional dual-motor effects, not adaptive-trigger resistance or bespoke DualSense waveforms.

## Lifecycle and settings

- The service starts automatically after the initial scene loads; haptics no longer depend on first enabling vibration in Options.
- The existing saved Vibration toggle controls every cue. Switching it off immediately zeroes the recipient's motors.
- Gameplay feedback stops on pause and is rejected while timeScale is zero. The short settings preview is explicitly allowed during pause and uses unscaled time.
- Motors stop on focus loss, app suspension, scene changes, device removal/disconnection/reset, service disable/destruction and application quit. Focus and app-pause gates are tracked independently.
- Playback retains its recipient, so switching the current controller stops the original pad rather than leaving it vibrating. Disconnected/unsupported drivers cannot throw into gameplay.
- Raw durations are capped at two seconds; strengths and motor speeds are bounded. No packages or platform settings were changed.
- Kemal's Interaction, Mechanisms and sandbox files were not modified. The chamber-completion assembly now explicitly references UI for its feedback cue.

## Verification and hardware limits

**36 passed, 0 failed, 0 skipped** in Unity Editor PlayMode: 5 haptics, 10 existing controller-support, 11 main-menu, 5 pause and 5 chamber-completion tests. The five haptics tests use a registered synthetic Gamepad overriding motor output: they verify bounded fade and stop, immediate disable, important-cue priority, controller switching, pause/preview behavior, focus loss and removal. Project integrity is also checked before publishing.

The initial 36-test run verified game-side commands and lifecycle. A later [DualSense Bluetooth transport correction](DUALSENSE-BLUETOOTH-FIX.md) was verified with a physical test pulse acknowledged by the user and 40/40 tests. No complete USB/Bluetooth driver matrix or standalone player was tested. Actual rumble support depends on controller, operating system, connection and Unity's backend; see the [pinned Input System gamepad documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Gamepad.html#rumble). In particular, the pinned documentation lists Xbox rumble on Windows and PS4 rumble on Mac/Windows; do not infer that working button input proves rumble support on every platform.

For hardware acceptance, test the existing Vibration toggle in title and paused controls, then jump/land, Flare, pickup/break, death and exit in the gameplay scene. Confirm that unplugging, alt-tabbing and pausing always stop the old effect, and tune amplitude against the real device before release.
