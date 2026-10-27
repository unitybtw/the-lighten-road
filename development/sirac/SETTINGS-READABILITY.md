# Settings and menu readability

The options pages and controls guides now use a wider, more readable layout while retaining the archive typography, warm lantern focus, animated slider handles and existing settings behavior. Kemal's files and gameplay/HUD layouts are unchanged.

## Layout

- Settings rows grow from 680/740 to 900 reference units. Titles use 24-point text, body copy uses 20 points, and device tabs have larger labels and 56-unit selection targets.
- Toggles accept clicks across their full 76-unit row. Sliders have a transparent 56-unit hit area around their existing lantern handle and track. Pause guide bumper badges have actual pointer targets.
- Controller settings have dedicated space for all five rows, saved status, Back/Defaults buttons and footer prompts. The status no longer overlaps Stick Deadzone.
- The pause menu uses the same row sizing and typography. Long sprint mappings have space beside their device icon. Keyboard/PlayStation/Xbox guides retain their navigation and selected states.
- A separate responsive layer enlarges these menus up to 120% while fitting a 90% safe region. It respects the main menu safe-area container and remains separate from entrance animation transforms. Body copy projects to at least 14 pixels in the checked resolution matrix.
- Focus ornaments and halos are resized with their controls. The main Options index retains its centered Back action; subpages keep Back beside Restore defaults. Preferences, progress and button bindings are preserved.

## Verification

**96/96 frontend Play Mode tests passed in Unity 6000.3.2f1**, with zero failures or skipped tests. This includes eight new readability/layout cases alongside the existing settings, pause, navigation, controller feedback and lifecycle regressions. Project integrity also passed. XML: ignored `Game/Library/Readability-tests.xml`.

New layout checks cover 1920×1080, 1280×720, 1280×800, 1024×768 and 2560×1080. Tests project the UI into off-screen render textures using the shipped CanvasScaler's width/height blend. They check safe fit, body text size, visible control bounds, toggle/slider hit areas, saved-status separation and icon/key-label spacing. Four settings pages, the controller guide and pause settings/controls have rendered previews in ignored `Game/Library/Readability/`.

Native Game-view review also confirmed the enlarged Controller page after mouse/keyboard navigation, with separated rows, saved status and footer controls.

These are Editor layout/rendering and software-input checks, not physical-monitor, handheld, TV or controller-device certification. Chapter progress and saved preferences are restored after the test suite. Project-facing copy remains English.
