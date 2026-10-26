# DualSense & DualShock Dynamic RGB Lightbar

2026-10-26 — Unity 6000.3.2f1 / Input System 1.17.0.

## Overview

The controller's lightbar (RGB LED array surrounding the DualSense touchpad and on DualShock 4) now serves as a **diegetic extension of the player's lantern**. Instead of displaying a static, arbitrary color, the lightbar breathes with the lantern flame and dynamically reacts to gameplay moments, fuel levels, hazards, and UI states.

## Bluetooth HID Report Architecture (`0x31`)

DualSense Bluetooth requires explicit feature enablement flags and trailing CRC32 over the 78-byte payload:
- **`report[4]` (ValidFlag1)**: Bit 2 (`0x04`) enables lightbar control (`DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE`).
- **`report[41]` (ValidFlag2)**: Bit 1 (`0x02`) enables lightbar setup control (`DS_OUTPUT_VALID_FLAG2_LIGHTBAR_SETUP_CONTROL_ENABLE`).
- **`report[44]` (LightbarSetup)**: Set to `0x02` (`DS_OUTPUT_LIGHTBAR_SETUP_LIGHT_OUT`) to override console system animations with custom host RGB.
- **`report[45]` (LedBrightness)**: `0x00` (Full brightness).
- **`report[47]`**: Red intensity (`0x00` – `0xFF`).
- **`report[48]`**: Green intensity (`0x00` – `0xFF`).
- **`report[49]`**: Blue intensity (`0x00` – `0xFF`).
- **`report[74..77]`**: 32-bit CRC (computed over transaction prefix `0xA2` + 74 payload bytes).

DualShock 4 and USB controllers are simultaneously updated via `DualShockGamepad.SetLightBarColor(color)`.

## Diegetic Color Palette & Gameplay States

| Gameplay State | Lightbar Color | Hex Code | Visual Behavior |
| -------------- | -------------- | -------- | --------------- |
| **Living Lantern (Ambient)** | Warm Amber Gold | `#F29424` | Organic, subtle breathing flicker matching the player's physical lantern. |
| **Flare (R2 Ignition)** | Radiant Sunlight Cyan | `#5AE6FF` | Blazes with an intense flash illuminating the player's hands, smoothly blending back to amber. |
| **Ember Pickup** | Crystal Emerald Mint | `#26FFA6` | Crisp 0.45s crystal chime flash upon collecting light embers. |
| **Ember Break / Damage** | Hot Ember Orange | `#FF660D` | Crackling spark pulse when an ember barrier shatters. |
| **Low Fuel / Darkness Danger** | Deep Crimson Heartbeat | `#F2140D` | Slow, ominous pulse at 3.8 rad/s when fuel is critically low (<= 1 ember) or trapped in darkness. |
| **Death (Extinction)** | Crimson $\rightarrow$ Cold Black | `#FF0D0D` $\rightarrow$ `#000000` | Sharp red hit flash followed by complete blackout as the light dies. |
| **Chamber Complete** | Auroral Shimmer | `#59F2CC` $\rightarrow$ `#B366F2` $\rightarrow$ Gold | Triumphal shifting auroral wave celebrating chamber mastery. |
| **Pause Menu** | Ruined Archive Slate Blue | `#3359A6` | Calm, cool sapphire matching the pause modal aesthetic. |

## User Control & Performance

- **Toggle Option**: Controlled via **Options → Controller → DualSense Lightbar**. Persisted independently in `PlayerPrefs` (`"TLR.Menu.ControllerLightbar"`) and defaults to ON.
- **Throttling & Battery Conservation**: Lightbar color writes are delta-gated and capped at 25 Hz. When the color is stable, redundant Bluetooth reports are avoided.
- **Fail-Safe Blackout**: Application pause, suspension, unfocus, or quitting automatically transmits `(0, 0, 0)` so the controller LEDs never remain stuck on.

## Automated Verification

- **`ControllerLightbarTests` (5/5 Passed)**: Validates report bitflags (`0x04`, `0x02`), CRC checksum integrity, command struct marshaling, preferences persistence, and haptic event reactivity.
- **`MainMenuTests` (12/12 Passed)**: End-to-end options navigation and settings synchronization.
- **`DualSensePatternTests` (7/7 Passed)**: Confirms 100% regression compatibility with existing legacy packet fixtures.
