# DualSense textures and refined adaptive triggers

2026-10-26 — Unity 6000.3.2f1 / Input System 1.17.0.

## Overview & trigger feel refinement

DualSense adaptive triggers feature physical worm-gear driven resistance actuators beneath L2 and R2. In platforming games, mechanical triggers must **never fight or snap against the player's fingers** during standard movement, jumps, or landings. Trigger activation must be strictly contextual, state-based, and aligned with the player's physical input.

Following physical hardware verification on macOS Bluetooth HID:
1. **İki Aşamalı Mekanik Çakmak / Kilit Hissi (Two-Stage Flint Lock)**:
   - When the lantern is unlit (`!boundLantern.IsFlared`), R2 engages **Section Mode** (`0x02`):
     - Initial pull (0% to 35%): smooth, effortless travel.
     - Squeeze zone (35% to 65%): firm tactile resistance of the heavy mechanical flint striker wheel (`force = 0.55`).
     - Actuation point (65%): mechanical resistance snaps through with a tactile release, exactly synchronizing with `TriggerPressGate` (`0.65f`) to ignite the lantern into a blazing flare!
   - When the lantern is burning (`boundLantern.IsFlared`), R2 is completely released (`Off`), giving zero resistance under the player's finger so extinguishing is light and natural.
   - When extinguished, the flint lock smoothly re-engages, ready for the next strike.
2. **Zıplama ve İniş Rahatlığı (Zero Platforming Resistance)**: `Jump`, `Landing`, `EmberPickup`, `EmberBreak`, `Death`, and `ChamberComplete` maintain the active lantern trigger state without any clicking, twitching, or gear cycling. Tactile feedback for these platforming events is delivered exclusively through the dual body rumble motors (`report[5]` & `report[6]`).
3. **Akıllı Delta-Gating (60 Hz Servo Jitter & Motor Chatter Elimination)**:
   - In `DualSenseBluetoothRumble.Send`: Trigger update flags (`report[3] |= 0x0C`) are **only asserted when the trigger profile actually changes** (`!prevR.Equals(rightTrigger)`).
   - When trigger state remains constant across frames, update bits remain 0. The DualSense internal firmware holds its actuator position silently without 60 Hz PID re-initialization or gearbox buzzing.
4. **L2 Serbestliği**: L2 remains completely neutral (`Off`) across all platforming actions to keep the left hand relaxed.

## Hardware trigger mode capabilities

The underlying architecture in `DualSenseBluetoothRumble.cs` retains full multi-mode protocol support:
- `0x01` — Continuous Resistance (variable start, clamped force)
- `0x02` — Section / Breakpoint Snap (variable threshold, force)
- `0x06` — Trigger Vibration / Flutter (frequency, amplitude)
- `0x05` — Explicit Safe Release

## Cue mapping reference

| Cue | Body haptics (L/R) | Left trigger (L2) | Right trigger (R2) | Mechanical behavior |
| --- | ------------------ | ----------------- | ------------------ | ------------------- |
| **Flare** | Swelling flame rumble | Rest (`0x05`) | **Smooth Progressive** (`0x01`, 0.25 start, max 0.22 force) | Weighted hydraulic draw tension matching lantern charge |
| **Jump** | Short high-motor click | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Landing** | Heavy low-motor thud | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Ember Pickup** | Bright dual chimes | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Ember Break** | Triple crackle pulses | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Death** | Diminishing shudder | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Chamber Complete** | Crescendo pulse wave | Rest (`0x05`) | Rest (`0x05`) | Zero trigger movement; quiet finger rest |
| **Settings Preview** | L $\rightarrow$ R motor sweep | Rest (`0x05`) | **Smooth Preview** (`0x01`, max 0.20 force) | Silent demonstration of continuous resistance |

## Automated validation

- **`DualSensePatternTests` (7/7 Passed)**: Confirms Flare/Preview progressive resistance, resting triggers for jump/land/pickup/death, binary packet serialization, and CRC-32 integrity.
- **`DualSenseBluetoothTests` (5/5 Passed)**: Confirms Bluetooth report structure, delta gating, and stop reports.
- **`ControllerHapticsTests` (5/5 Passed)**: Confirms rumble timing and options persistence.
- **`MainMenuTests` (12/12 Passed)**: End-to-end UI navigation, options toggle, and haptic previews.
