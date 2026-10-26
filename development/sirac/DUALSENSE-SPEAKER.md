# DualSense controller speaker

Short lantern details now have a dedicated **Bluetooth DualSense speaker** path:

| Event | Controller detail |
| --- | --- |
| Flare begins | Brief ignition / flame rush |
| Flare ends | Soft extinguishing hiss |
| Ember collected | Short pickup chime |
| Ember breaks | Brief cracking sound |

On macOS the speaker uses **very brief transient accents** (40–55 ms of source audio, at most four audio reports per cue), rather than duplicating the whole desktop effect. This fits the much higher latency measured on the macOS Bluetooth HID control path. Full-length variants are provided for the other HID backend but are not hardware-verified there.

Normal game audio remains available on the normal audio output. Music, footsteps and continuous ambience are not streamed to the controller. No default system audio device is changed, and microphone capture is not enabled.

## Player controls

**Options → Audio → DualSense Speaker** toggles the feature independently of vibration and adaptive triggers. It is saved locally, enabled by default, and restored by Restore Defaults. Master Volume and Sound Effects both scale the speaker level; either at zero silences it. The native speaker level is capped at 64/127, and the source effects are attenuated and faded for close listening.

This implementation targets Sony DualSense / DualSense Edge over Bluetooth through the project's controller feedback bindings. On macOS, speaker reports use the system IOKit API; other platforms use Unity Input System HID output. USB speaker routing and non-Sony controllers use the existing normal game audio instead. Platform, driver and firmware behavior can differ; a successful HID write alone does not prove audible output.

## Runtime behavior

On macOS, a dedicated background worker sends audio on a monotonic clock so rendering cannot truncate the stream. It owns and closes its IOKit handles, captures only plain target/volume/frame data from the main thread, and never calls Unity, InputSystem or PlayerPrefs from the worker. Device routing requires the selected product and serial (or a unique matching Bluetooth device); ambiguous matches are rejected. No native plugin is bundled.

`DualSenseSpeaker` is created lazily and survives scene changes. It listens through the existing player feedback event bindings in `ControllerGlyphService`; it does not modify circuit or mechanism behavior.

- Speaker packets contain two 200-byte, 48 kHz stereo Opus frames in a 547-byte `0x39` Bluetooth report, sent every 1024/48000 seconds.
- A `0x32` setup report controls only speaker volume, output routing, preamp and mute state. Microphone capture, microphone volume, headset volume, LEDs and trigger settings are not enabled by speaker reports.
- Speaker and motor packets share a per-device sequence. Motor traffic is limited to 60 Hz, or 30 Hz during speaker playback; zero motor/trigger output is always immediate.
- Audio-haptic payloads are silent: existing motor patterns and light R2 feedback remain independent.
- No delayed queue accumulates. Pickup does not interrupt an existing cue; other cues replace it.
- A transient rejected write retries the same audio frame up to three times. Persistent failure stops that cue and applies a short device cooldown. Long frame stalls stop playback rather than flood the transport.
- Pause, scene change, focus loss, app suspension, device removal, disable, shutdown and zero volume stop playback and send speaker level zero where the device is still available.

Diagnostics: `LastCommandResult`, `LastErrorReportId`, `SuccessfulReports`, `FailedReports`, `LastPlaybackComplete` and `IsPlaying` report the most recent cue. Acceptance by the native driver is distinct from physical sound confirmation.

## Audio assets and rebuild

The `.bytes` assets under `Game/Assets/_Project/Resources/Audio/ControllerSpeaker/` contain complete pairs of CBR Opus packets and encoded silence. `Mac/` contains brief transient variants selected from the same source effects. No runtime codec, external audio player or background daemon is needed.

Rebuild with `python3 scripts/audio/build_controller_speaker.py` (requires ffmpeg with libopus). Ignite, Pickup and Break derive from the existing SFX assets and retain their original asset attribution. Extinguish uses the same procedural waveform as `SoundEffectManager`. The build script trims, filters, attenuates, fades and tempo-compensates the source, validates every 200-byte Opus packet, and only pads with an encoded silence packet.

## Protocol attribution

Bluetooth framing was adapted from the MIT-licensed [DualSense-Bluetooth-Audio](https://github.com/Kodzinho/DualSense-Bluetooth-Audio) and its MIT upstream [dualsense-bt-haptics](https://github.com/awalol/dualsense-bt-haptics). Their copyright and permission notices are preserved in [the audio prototype notice](licenses/DualSense-Bluetooth-Audio-MIT.txt) and [the upstream notice](licenses/dualsense-bt-haptics-MIT.txt). No external application binaries or native plugins are shipped.

## Verification — 2026-10-26

Unity 6000.3.2f1, macOS, physical PS5 DualSense over Bluetooth. A streamed test tone was heard from the controller speaker and explicitly confirmed by the user. The first very quiet tone was inaudible; the confirmed test used a moderate native output level. Final gameplay effects use the same packet format with quiet source attenuation.

The final frontend/controller regression run passed 55/55 Play Mode tests. Tests cover native command header and exact payload layout, independent CRC fixtures, microphone/trigger/LED flags, incomplete/overflowing frames, all four embedded audio resources, gain/mute behavior, saved switch navigation, cross-thread sequence allocation, safe missing-device cleanup and the existing menu, pause, haptic and chamber-completion behavior.

A direct IOKit probe streamed all four effect assets with 97/97 audio writes and 16/16 setup/mute writes accepted. Long clips exceeded the sustainable macOS HID write cadence and were truncated; that path is not used for the Mac cues. After the controller was reconnected, the final Unity review used the dedicated worker and brief transient variants: Ignite 7/7 writes, Extinguish 6/6, Pickup 6/6, Break 7/7, zero rejected writes, and `LastPlaybackComplete=true` for all four. Ignite also ran alongside the existing Flare motor/trigger feedback. The review explicitly simulated Game View focus in its temporary Editor harness; runtime focus cancellation remains active. The user confirmed the earlier physical test tone; the later effect sound quality was not separately confirmed by listening.

![Controller speaker setting](previews/controller-speaker-settings.png)

After selecting the final short Mac resources and adding complete-playback diagnostics, the nine speaker-specific tests were rerun: 9/9 passed. Project integrity checks passed. Standalone builds and non-Mac hardware playback have not been verified.
