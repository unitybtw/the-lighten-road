# DualSense Bluetooth rumble fix

2026-10-25 — Unity 6000.3.2f1 / Input System 1.17.0.

## Reproduced cause

The connected Sony DualSense was present in macOS as a Bluetooth HID device and in Unity as `DualSenseGamepadHID` (vendor `054C`, product `0CE6`). Its descriptor reports **78-byte input** and a **547-byte maximum output report**. That maximum is not the length of the rumble packet.

The installed Input System's `DualShockGamepadHID.cs` explicitly leaves Bluetooth output disabled (`Bluetooth reports are not working`), calls its USB report constructor for DualSense haptics, and leaves the Bluetooth CRC implementation unfinished. The prior synthetic Gamepad tests bypassed this transport layer and therefore did not prove actual Bluetooth vibration.

## Project-local correction

`DualSenseBluetoothRumble` routes recognized Sony DualSense / Edge Bluetooth HID devices through a dedicated **78-byte** `HIDO` output command. It uses report ID `0x31`, the four-bit sequence tag, marker `0x10`, rumble-emulation flags, correctly ordered high/low motor bytes and little-endian CRC-32 over transaction prefix `0xA2` plus the first 74 report bytes. Zero-motor stop packets use the same complete transport and checksum.

Detection accepts the observed 78/547 descriptor as well as a literal 78-byte output descriptor. USB, Xbox, other vendors and non-DualSense layouts retain the normal Input System motor path. The initial correction enabled only rumble-control flags. The later [DualSense texture extension](DUALSENSE-TEXTURES.md) optionally controls light R2 resistance and explicitly releases it; lightbar and audio levels remain untouched. No package-cache files, native drivers, unsafe-code settings or Unity package versions were changed.

Every existing contextual cue and safe-stop path in `ControllerHaptics.SetMotors` uses this transport selection, including the Vibration settings preview.

Protocol references: [Linux PlayStation HID driver](https://kernel.googlesource.com/pub/scm/linux/kernel/git/stable/linux-stable/+/master/drivers/hid/hid-playstation.c), [SDL's DualSense HID implementation](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c). The version-pinned [Unity gamepad documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Gamepad.html) documents the stock backend's limitations; this correction applies specifically to the project-local DualSense Bluetooth motor path.

## Verification

- **Physical device:** sent a 0.55/0.55 motor pulse for approximately 0.6 seconds to the connected DualSense over Bluetooth. Native start and stop commands both returned `1` (accepted). The user explicitly confirmed feeling the vibration.
- **Unity PlayMode:** **40 passed, 0 failed, 0 skipped**. This includes the existing 36 haptics/menu/controller/completion tests and four new transport regressions for headers/motor order, independent CRC fixtures, command size/layout and Bluetooth-versus-USB/vendor routing.
- CRC fixtures were independently computed using Python `zlib.crc32`: 0.5/0.25 motors, sequence 3 → `A03C6E33`; zero motors, sequence 0 → `962E64CC`.
- Temporary Editor probes were removed. Hardware serial numbers and pairing data were not collected or committed. Kemal-owned scripts and scenes were not changed.

The user confirmation proves the test pulse on this Mac/DualSense Bluetooth connection. It does not establish subjective tuning for every gameplay cue, every controller firmware or a standalone-build/platform matrix.
