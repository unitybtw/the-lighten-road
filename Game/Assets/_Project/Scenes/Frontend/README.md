# The Lighten Road — Main Menu

Open `MainMenu.unity` in Unity 6000.3.2f1 and press Play. It is the first enabled scene in Build Settings. Play fades into Siraç's `MovementSandbox`; the other existing build scene is retained.

The 3.7-second opening reveals a ruined archive with a restrained camera-style push, warm lantern spill and rising ash. Enter, click or the gamepad south button skips the opening. Buttons support pointer hover and Unity UI navigation. Escape / gamepad east returns from a panel or opens the quit confirmation from Home.

Settings provides master, music and effects volume, fullscreen and reduced motion. Preferences are stored under `TLR.Menu.*` and reapplied to the gameplay audio and HUDs after scene loading. Music scales the existing ambient manager's resting 0.25 volume. Fullscreen is saved in the Editor and applied only in standalone players. Reduced motion skips the intro and removes drift, decorative ash movement and focus sliding. Credits lists the five team members and their roles. Quit asks for confirmation and exits Play Mode in the Editor.

Runtime presentation is built by `Scripts/Frontend/MainMenuController.cs`; behavior and settings are isolated in the new Frontend assembly. Gameplay scenes, artist sources, audio systems and Kemal's scripts are not modified. The Editor authoring helper refuses to overwrite an existing menu scene.

## Art and typography

`Art/Frontend/ArchiveMenuBackdrop.png` was generated using the built-in Imagegen tool as a menu illustration; it is not attributed to a team artist. The UI, live glow and particles are rendered separately in Unity. `Resources/UI/MenuType.asset` is a static 1024px SDF atlas derived from the existing Liberation Sans font; the source and OFL license remain in `Assets/TextMesh Pro/Fonts/`.

Final generation prompt:

> Use case: stylized-concept. Asset type: 16:9 high resolution background painting for a premium Unity 2D puzzle-platformer main menu titled The Lighten Road. No typography or UI baked into the image. Scene: a silent decaying brutalist subterranean archive, old concrete pillars, a broken arch and rusted archive shelves fading into deep charcoal mist. A single small old iron ember lantern rests on a broken stone plinth at about 74% of image width and 62% of image height; a warm restrained amber light illuminates the cracked stone immediately around it. The flame is the quiet focal point. The left 48% is very dark atmospheric negative space for readable title and menu, with only subtle ruin silhouettes, not a flat black rectangle. Right foreground has layered ruined architecture framing the lantern, distant corridor with faint cool gray depth. Style: art-directed hand-painted 2D game concept illustration, expressive rough brush texture, subtle film grain, elegant restrained composition, ominous melancholic atmosphere. Palette: soot black, desaturated slate gray, muted olive stone and localized pale gold ember glow. Wide cinematic landscape 16:9, detailed high resolution, edges dark, generous breathing room. No characters, no giant flame, no sparks baked in, no fantasy castle, no blue neon, no sci-fi holograms, no interface, no words, no logo, no watermark.

## Validation — 2026-10-17

Unity MCP Play Mode: all 3 `MainMenuTests` passed, covering intro skip and panel navigation, audio sliders and saved preferences, and reduced-motion behavior on the next menu. The menu and Settings were visually inspected in the actual Game view, and the user approved the design. Play's actual asynchronous transition into MovementSandbox was checked separately. The Console had no errors during these checks. Standalone fullscreen and physical gamepad behavior require platform playtesting; no standalone build was produced in this pass.

Repository integrity check: four existing files in Interaction / Mechanisms (`EmberVein.cs`, `ElectrifiedWater.cs`, `RollingShelf.cs`, `SimpleLift.cs`) fail UTF-8 decoding, including in the pre-menu HEAD. Those ownership-protected files were not changed in this menu commit. All new menu assets passed the integrity checks.
