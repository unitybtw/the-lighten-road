---
name: game-ui-ux
description: Comprehensive framework, design methodology, and engineering guidelines for video game UI/UX design. Activate this skill whenever designing, auditing, restructuring, styling, or coding game interfaces—including main menus, pause screens, HUDs, diegetic and spatial interfaces, inventory, settings, controller/keyboard navigation, procedural UI sprites/shaders, typography, sound synchronization, and game UI juice in Unity, Unreal, Godot, or custom engines.
---

# Game UI/UX Design & Engineering Skill

A comprehensive methodology and reference guide for designing and implementing world-class video game user interfaces and experiences, balancing atmospheric immersion, clarity, controller ergonomics, and visual feedback ("juice").

---

## 1. The Four Cornerstones of Game UI

Unlike web or desktop productivity software, game UI must serve narrative immersion, gameplay pacing, and visceral feedback alongside functional usability. Always categorize and balance UI elements across four dimensions:

| Dimension | In Game World Space? | In Game Fiction (Story/Lore)? | Examples | Key Rules |
| :--- | :---: | :---: | :--- | :--- |
| **Diegetic** | **Yes** | **Yes** | Lantern fuel glass, wrist watch (*Metro*), physical map journal, car dashboard gauges. | Maximum immersion. Keep readable from player camera angle; pair with physical tactile sounds. |
| **Non-Diegetic** | **No** | **No** | Screen-space HP bars, Pause Menu, Options sliders, ammo counters, crosshair. | Crisp clarity, high contrast, non-intrusive. Never clutter the gameplay viewport center unnecessarily. |
| **Spatial** | **Yes** | **No** | World-space `[E]` interact prompts, 3D waypoint beacons, player nameplates. | Must scale or fade with distance to avoid visual noise; occlude behind solid geometry unless highlighted. |
| **Meta** | **No** | **Yes** | Blood/frost vignette on screen edges, lantern flame flickering at screen borders, cracked visor. | Represents character physical/mental state directly on the "lens" of the player's perspective. |

---

## 2. Atmospheric & Stylized UI Design (Hollow Knight, Soulslike, Ori, Dark Souls)

### Avoiding "Web Card Syndrome"
* **The Anti-Pattern**: Enclosing every button, menu row, or option in heavy rounded rectangular cards or boxes. This makes fantasy, atmospheric, or gothic games feel like modern SaaS dashboards.
* **The Solution**: 
  - **Frameless Floating Typography**: Let elegant serif or chiseled display type float freely over atmospheric backgrounds (sanctuary arches, dark vaults, subtle mist veils).
  - **Filigree Dividing Rules**: Replace solid box borders with delicate tapered horizontal filigree lines (`filigreeSprite` tapering to zero alpha/width at the edges).
  - **Ornate Crests & Header Arches**: Frame titles with an ornate top crown arch above and an emblematic thematic crest below (e.g., lantern core, runic sigil, leaf motif).

### Dynamic Selection Pointers & Focus Hierarchy
* **Inward-Gliding Pointers**: On hover or controller focus, have ornate symmetrical pointers (`<` and `>`) glide smoothly inward toward the active option.
* **Breathing & Oscillation**: Add subtle unscaled breathing math (`Mathf.Sin(time * 3.5f) * 2f`) to active pointers or cursor flames to give the UI life.
* **Center-Expanding Glow Underlines**: When an item receives focus, smoothly expand an ember-vein or mana line from width `0` outward beneath the active title.
* **Warm Radial Auras**: Soft radial glow blooms behind focused text, igniting warm colors (e.g., cool moonlight silver transitioning to warm lantern gold/amber on selection).

---

## 3. Unity UI (uGUI & UI Toolkit) Architecture & Performance

### 1. Canvas Layering & Draw Call Optimization
* **Separate Dynamic vs. Static Canvases**: 
  - Place frequently animating elements (floating motes, flickering lanterns, moving sliders, pulsing icons) on a sub-canvas. 
  - Modifying a single element triggers mesh regeneration for the *entire* canvas it belongs to.
* **Raycast Target Discipline**:
  - **MANDATORY**: Uncheck `raycastTarget` on every non-interactive graphic (`Image`, `TMP_Text`, glows, background vignettes, filigree dividers).
  - Only leave `raycastTarget = true` on the dedicated hit graphic of clickable buttons or sliders.
* **Resolution Independence**:
  - `CanvasScaler.uiScaleMode = ScaleWithScreenSize`
  - Reference resolution: `1920 x 1080`, `matchWidthOrHeight = 0.5f`.
  - Use `anchorMin`/`anchorMax` and anchors relative to safe frames (`Screen.safeArea`).

### 2. High-Fidelity Procedural UI Sprites
Avoid fuzzy low-res assets or texture banding by generating smooth procedural sprites at runtime:
* **Circular / Capsule Sliders**: Anti-aliased mathematical distance functions with 9-slice borders.
* **Soft Radial Vignettes & Glows**: Smooth quadratic/cubic alpha falloff (`Mathf.Pow(1f - d, 2.2f)`) rather than linear gradients to eliminate color banding.
* **Clean Memory Cleanup**: Store generated procedural `Sprite` and `Texture2D` instances in a list and call `Destroy()` in `OnDestroy()` to avoid memory leaks.

### 3. Typography & TextMesh Pro Best Practices
* **Display vs Body Typography**: Use a distinct atmospheric display font for titles/crests and a clean, high-legibility font for descriptions and subtitles.
* **Letter Spacing & Casing**:
  - Major menu titles: All-caps with generous character spacing (`characterSpacing: 4.5f .. 9.0f`).
  - Mechanics whispers & subtitles: Lower or sentence case with tighter spacing (`1.5f .. 2.5f`) and muted luminance.
* **SDF Padding**: Ensure TextMesh Pro font assets have sufficient SDF padding to allow soft glowing dilations, outlines, or dropshadows without clipping.

---

## 4. Input, Navigation & Controller Ergonomics

### 1. Seamless Multi-Input Support (Gamepad, Keyboard & Mouse)
* **Explicit Navigation Graphs**:
  - Do not rely solely on `Navigation.Mode.Automatic` when menus have tabs, sidebars, or modal overlays. Set explicit `selectOnLeft`, `selectOnRight`, `selectOnUp`, `selectOnDown`.
* **Auto-Focus on View Entry**:
  - Whenever entering a menu or switching tabs, immediately set `EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject)`.
  - Prevent "lost focus" when a player clicks into empty dead space by maintaining last active selection or re-focusing on mouse click.
* **Ghost Highlight Elimination**:
  - Clear selection (`EventSystem.current.SetSelectedGameObject(null)`) during scene transitions or modal animations so buttons do not get stuck in highlighted state.

### 2. Tactical Pause vs. Unscaled Animation
* When pausing gameplay via `Time.timeScale = 0f`:
  - **All UI animations, tweens, and coroutines MUST use `Time.unscaledDeltaTime` and `Time.unscaledTime`**.
  - Audio playback for UI clicks/chimes must route to an AudioSource set to `ignoreListenerPause = true`.
  - Suspend player motors, physics updates, and camera cinemachines cleanly, restoring them upon resume.

---

## 5. Audiovisual "Juice" & Tactile Polish

Every player interaction should feel tactile, immediate, and satisfying:

1. **Audio Feedback Hierarchy**:
   - **Hover / Navigation Focus**: Subtle, soft whisper, gentle air whoosh, or hollow chime (`pitch ~ 1.1x`, low volume `0.2 - 0.35`).
   - **Click / Confirm**: Crisp, resonant snap, metallic click, or flame ignition (`volume 0.6 - 0.8`).
   - **Cancel / Back**: Deeper downward tone or breath sound.
   - **Slider Drag / Scrub**: Frequency-modulated tick or smooth continuous murmur.
2. **Visual Micro-Motion**:
   - Primary action buttons: +3% to +5% scale pop on focus, micro squash on pointer down (`-2.5%`).
   - Text color transitions: Never pop abruptly; interpolate across 80-120ms (`Mathf.MoveTowards` with `smoothstep`).
3. **Reduced Motion Accessibility**:
   - Provide a dedicated "Reduced Motion" option that disables screen shake, parallax camera sway, and background oscillations while keeping transitions snappy (instant fade / 0.04s).

---

## 6. Implementation Checklist for any Game UI Task

When designing or revising a game menu or HUD:
- [ ] **Aesthetic Direction**: Is it free of boxy "web cards"? Does it match the game's atmosphere (e.g. Hollow Knight floating serif, Cyberpunk neon data, Retro pixel art)?
- [ ] **Alignment & Focus**: Is the layout intentional (e.g. 100% centered sanctuary stack vs left-aligned cinematic list)?
- [ ] **Diegetic Accents**: Are there world-specific sigils, sparks, embers, or icons framing the title and options?
- [ ] **Raycast Audit**: Is `raycastTarget` disabled on every text label, icon, aura, and decorative background?
- [ ] **Gamepad / Keyboard Navigation**: Can the menu be traversed 100% using solely Arrow keys / D-pad / Gamepad stick, `[Space]`/`[Enter]`/`[A]`, and `[Esc]`/`[B]`?
- [ ] **Audio Feedback**: Does every selectable play an audio cue on focus and click?
- [ ] **Accessibility & Settings**: Are audio sliders, fullscreen toggle, v-sync, frame limit, and reduced motion integrated?
- [ ] **Memory & Leaks**: Are procedural textures destroyed on teardown? Is EventSystem cleanly handled?
