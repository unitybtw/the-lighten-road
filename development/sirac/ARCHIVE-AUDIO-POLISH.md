# Archive audio polish — 29 October 2026

Replaced the stock-tone feedback with edited, licensed foley: cloth and book handling for menus, recorded footwear for the archive floors, latch/door sounds for props, match ignition for Flare, and fire crackle for extinguishing/breaking embers. Crystal feedback uses softened material resonances rather than a generated major chord. The original source credits and every edit are recorded in the [67-cue manifest](../../Game/Assets/_Project/Resources/Audio/SFX/archive-sfx-manifest.json).

## Playback fixes

- Removed runtime waveform generators from SoundEffectManager, pause, the lantern HUD, glyphs/platforms and the FirstLight presentation. Missing optional surface banks use the authored stone bank. No substitute beep is created.
- Periodic player discovery no longer reapplies the 0.65-second spawn grace every two seconds. Footsteps require actual ground movement and stop advancing while paused, in a modal screen or during traversal. Exhaustion feedback follows the same input/death boundaries.
- Immediate repetition of the same footstep recording is avoided. Hover feedback has an 85 ms limit. Reusing a busy source replaces its current clip instead of stacking another PlayOneShot voice underneath it.
- Effects volume updates existing pooled voices, including a mute/unmute round trip. Pause and glyphs apply the Effects gain once. FirstLight no longer multiplies Master both locally and through AudioListener.
- Source edits use mono 44.1 kHz, short boundary fades, high/low-pass filtering, conservative gain and a maximum 4× boost. The 67 decoded deliverables stay below 0.65 full scale; their highest measured source peak is approximately 0.49. Existing asset GUIDs are retained.
- DualSense cues use the same foley palette. The packet builder pads short recordings before trimming, preserving the existing bounded Bluetooth playback windows. Transport and haptic code are unchanged; a new physical-controller listening check was not performed.

## Audition

[Play the short cue reel](previews/audio-polish/archive-sfx-preview.wav). This is a sequence of the edited assets with short silence between cues, rather than an in-game mix recording. [Cue order](previews/audio-polish/cue-order.json): hover, confirm, back, pause open/close, stone steps, wood, metal, water, Flare ignition/extinguish, pickup, ember crack and crystal activation.

## Sources and rebuilding

All added source material is CC0-1.0. Source pages were checked before use; the package archives are downloaded only to the local cache. These are pre-existing human-authored libraries/recordings, not newly generated audio.

| Author | Source | Role |
| --- | --- | --- |
| Kenney | [50 RPG Sound Effects](https://opengameart.org/content/50-rpg-sound-effects), [Interface Sounds](https://opengameart.org/content/interface-sounds) | Book, cloth, wood, metal and glass |
| OwlishMedia / owlstorm | [Sound Effects Pack](https://opengameart.org/content/sound-effects-pack) | Hard floor, metal impacts, landing, scuff and male breath |
| qubodup | [Flare ignition](https://opengameart.org/content/flare-ignition) | Recorded match ignition |
| AntumDeluge | [Fire Crackling](https://opengameart.org/content/fire-crackling) | Ember crack and fading fire |
| artisticdude | [Swishes Sound Pack](https://opengameart.org/content/swishes-sound-pack) | Falling air |
| Peludo | [Water Splash and sand footsteps](https://opengameart.org/content/water-splash-and-sand-footsteps) | Water footsteps |

Kenney's original notices are preserved in [RPG license](licenses/ArchiveSFX-Kenney-RPG.txt) and [Interface license](licenses/ArchiveSFX-Kenney-Interface.txt). The source manifest includes author, source page, download URL, archive hash, exact member, edit parameters and output hash for every cue.

Run `python3 scripts/audio/build_archive_sfx.py` with ffmpeg and Python soundfile installed. The default cache is `Game/Library/AudioSources`; `--cache /path/to/cache` can reuse downloaded archives. Source hashes must match before processing. Then run `python3 scripts/audio/build_controller_speaker.py` to rebuild the controller payloads. Only edited files and notices are committed; the large source archive is excluded. Vorbis/MP3 encoders may produce different binary container bytes across library versions; the manifest is refreshed by the build.

Existing music and book narration remain their own authored systems. Kemal's interaction, mechanism and sandbox sources were not edited. This pass changes their shared default audio assets without changing their state logic.

## Verification

**334/334 scoped Play Mode tests passed**: Player 145, Frontend 162, Glyphs 12, FirstLight 10 and Completion 5. All 67 clips were also decoded successfully through Unity, with non-silent data and peaks below 0.7. The source waveform audit passed for every cue. Saved menu preferences were restored and the editor returned to FirstLightTutorial outside Play Mode.

See [audio-polish-validation.json](audio-polish-validation.json) for the final scoped Unity results, imported asset checks and waveform audit. Physical output-device feel and a standalone build require separate playtesting.
