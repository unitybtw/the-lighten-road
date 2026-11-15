# The Lighten Road — Game Design Document

> **Document Status:** Updated Pre-Production & Vertical Slice Design · **Course:** DGD3004 Game Development I · **Engine:** Unity 6000.3.2f1 (Universal 2D) · **Updated:** 2026-11-12
>
> Structured according to the **GDD Blank Template** (Serket Studios layout) and synchronized with the [Developer Mechanics Contract](DEVELOPER-MECHANICS-2026-10-18.md), [Experience Direction](EXPERIENCE-DIRECTION.md), and [Homework 2B Lore Collectibles / Codex](HW2-PART-B.md) specifications.

---

## Packaging

- **Title:** The Lighten Road
- **Company:** The Lighten Road Team (DGD3004 — Furkan Aydın, Siraç Göktuğ Şimşek, Kemal Berke Aşık, Can Mutluşahinoğlu, Maksim Martynenko)
- **Splash Art:** *Light* raising the flickering ember lantern inside the pitch-black Corporate Archive (`development/sirac/previews/hud-clarity/flare-1920.png`)
- **Tagline / Hook:** *Manage every ember of a fading lantern, manipulate ancient circuits, and uncover the truth behind the Fall inside pitch-black corporate ruins.*

---

## 1. Introduction

### Summary / Pitch
*The Lighten Road* is a tense, atmospheric 2D side-scrolling puzzle-platformer in which **Light**, a condemned Ruin Researcher, must navigate pitch-black post-apocalyptic ruins using an anomalous ember lantern. Gameplay revolves around strict resource and sightline trade-offs: holding passive **Dim** light to survive, permanently spending **1 whole ember** on a **Flare** to scout hazards or charge seals, and temporarily transferring embers into ancient **Ember Veins** (**Power vs. Drain**) to reshape machinery, cross flooded corridors, and survive a 3-second darkness death limit.

### Inspirations
- **Other Games:** *Inside* & *Limbo* (silhouette clarity, solitary momentum, physical crate puzzles); *Little Nightmares* (oppressive dark architecture vs. human scale); *Dark Souls* & *Death Stranding* (asynchronous stored world logs via `StoredDelivery`); *Mass Effect*, *Horizon*, & *Destiny* (Codex & environmental datapoints); *Hollow Knight* (ivory-and-brass archive UI restraint).
- **Non-Games:** Brutalist abandoned office interiors, noir chiaroscuro lighting, bound archival ledgers with handwritten ink, and existential post-apocalyptic solitude 400 years after societal collapse.

### Player Experience
Players should feel the crushing isolation of being completely alone 400 years after the Fall, heightened fear of total darkness, constant tension when spending scarce embers for vision, relief when an Ember Vein circuit locks a deadly hazard safely off, and quiet curiosity when a recovered Codex log lights up both the backstory and +1 emergency ember.

### Platform
- **Primary Target:** PC (Windows & macOS) with full Keyboard/Mouse, Xbox Controller, and PlayStation DualSense / DualShock support.

### Software
- **Game Engine:** Unity **6000.3.2f1** (Universal 2D template, URP 2D Lighting, Input System 1.17.0, TextMeshPro, NUnit PlayMode Test Framework).
- **Art & Shaders:** High-contrast pixel-art sprite sheets (point-filtered, zero bilinear blur) and a procedural/authored 3D pixel-filtered Archive Codex model (`RenderTexture` + custom URP `ArchiveBookSurface` shader).
- **Audio & Narration:** 67 edited CC0 foley cues (`archive-sfx-manifest.json`, 44.1 kHz mono), atmospheric background music, offline neural TTS caching (`ArchiveNarration` using `kokoro-fp16-george-v2`), and native DualSense Bluetooth HID packets (`0x31` RGB lightbar & `0x35` 48 kHz Opus micro-speaker).
- **Documentation & Tooling:** Modular `.asmdef` C# assemblies, Python CI integrity verifier (`scripts/ci/check_project.py`), and Python/ReportLab document pipelines (`scripts/docs/`).

### Genre
2D Atmospheric Puzzle-Platformer / Light-Survival Side-Scroller.

### Target Audience
- **Bartle Types:**
  - **Primary — Explorer:** Driven to scout dark detours, examine ruins, and recover all hidden Old World Archive logs (`100% Decryption`).
  - **Secondary — Achiever:** Rewarded for mastering the strict 4-ember economy, avoiding wasted Flares, and solving coupled Power/Drain circuits efficiently.
- **Profile & Comparable Titles:** Ages 16–32, PC/console indie players who gravitate toward methodical, non-combat atmospheric puzzle-platformers (*Inside*, *Limbo*, *Little Nightmares*, *Celeste*, *Hollow Knight*).

---

## 2. Concept

### Core Loop
1. **Action:** Enter a dark sector, choose between passive Dim scouting or spending 1 ember to Flare, push crates/climb ledges, and interact with an Ember Vein (Power or Drain).
2. **Result:** Dynamic 2D light reveals traps and layouts; machinery activates (moving shelves/lifts, electrified water) or halts/purifies safely.
3. **Reward:** Cross the platforming obstacle, scavenge ember pickups, or recover an Archive Terminal log (+1 emergency ember on live discovery).
4. **New Decision:** Continue through the sealed chamber exit (carrying remaining embers forward) or risk an optional dark detour for extra lore and fuel.

Exiting a solved chamber seals the path behind the player and transfers remaining embers into the next dark stage, keeping fuel tension cumulative.

### Themes
- **Light vs. Darkness:** Visibility is never free; darkness is both an aesthetic veil and a 3-second lethal state.
- **Knowledge as Survival:** Uncovering pre-Fall Codex entries provides practical room clues and +1 emergency ember.
- **Trade-offs & Consequence:** Every Flare spends a permanent stone; every powered circuit steals an ember from your personal lantern.

### Primary Mechanics
- **Walking-Only Traversal & Physics:** Grounded horizontal walk speed (sprint retired so deliberate pacing holds), variable-height jump, rigidbody crate pushing (tuned weight/friction), and deliberate **Ledge Climb** (`W / Up / D-pad Up / Left stick up`, stowing lantern briefly to pull onto solid lips 0.25–1.35u high, with downward/jump cancellation). **No sprint, no double-jump, and no combat attacks.**
- **Lantern & 4-Ember Economy:** Capacity capped at **4 embers**; each chamber start and respawn sets **3 embers**. **Dim** light stays lit passively at 0 fuel drain while >=1 ember is held. **Flare** (`F` / `R2` / `RT`) permanently spends **1 whole ember** immediately for a wide, fading scout radius and light-seal charging. **Zero Embers:** holding 0 embers continuously for **> 3.0 seconds** triggers *Darkness Death* (paused when menu/Codex is open).
- **Ember Veins (Power vs. Drain):** Reversible 1-ember transfer (`E` / `Triangle` / `Y`).
  - **Power:** Deposits 1 ember to drive linked receivers (`RollingShelf` patrols waypoints, `SimpleLift` winches upward, `ElectrifiedWater` becomes lethal).
  - **Drain:** Recovers the stored 1 ember (blocked safely if lantern is already at 4/4) and switches devices off (`RollingShelf` freezes mid-path as a solid bridge, `SimpleLift` descends under gravity, `ElectrifiedWater` turns harmless). Shared two-node circuits share one authoritative state.

### Secondary Mechanics
- **Lore Collectibles / Archive Codex (Selected GDD System):** `DiegeticWhisper` desks trigger `CodexSession.Discover(id)`, raising `SocialEvent(kind: LogFound)` on `SocialChannel`. Using **Observer + Strategy** (`StoredDelivery` + `AmbientMembership`), three decoupled `ISocialListener`s respond: `ArchiveCodexMenu` records the entry & updates *Archive Decryption %*, `PartyFeedUI` displays recovery notices, and `LanternFuel` awards **+1 emergency ember** strictly once per live discovery (`if (e.isReplay) return;`).
- **Interactive 3D Pixel-Rendered Archive Book:** Reading opens `ArchiveBook.prefab` in a half-res point-filtered overlay with a live 2048x1464 handwriting canvas, seamless 4mm tucked gutter shader, any-edge mouse page dragging, real-time reverse spread preview, and bookmark.

### Tertiary Mechanics
- **Room Checkpoints & Safe Retry:** `PlayerRespawnController` restores player position and 3 embers at local checkpoints; puzzles/crates reset consistently.
- **Contextual Single-Cue Prompts:** `ControllerGlyphService` shows dynamic single-action glyphs matching active input (Keyboard / Xbox / PlayStation).
- **Lantern-Led Transitions:** Exiting a chamber fades the player's lantern out over dark slate, presents the chapter title, and relights inside the next stage.

### Combat
**None.** Combat is explicitly excluded; tension arises solely from environmental hazards (electrified water, abyss falls, spikes), route planning, and light starvation.

### Puzzles
Coupled multi-receiver Ember Vein circuits (one circuit simultaneously governing a walkway/shelf and hazard water), *First Light* Flare-reactive light seals (requiring 1.2s of active Flare light), crate positioning + ledge climbs in unlit shafts, and high-risk/high-reward detours to Archive Terminals near zero fuel.

### Quests
Linear chamber-to-chamber progression (*First Light Prologue* -> *Furkan Tutorial* -> *Corporate Office / Annex* -> *Subterranean Covert Lab*) paired with the optional Explorer objective of recovering **100% of Archive Codex records**.

### AI / NPCs
No hostile AI or living dialogue NPCs exist inside play areas, reinforcing total isolation. Automated machinery (`RollingShelf`, `SimpleLift`, `ElectrifiedWater`) reacts deterministically to circuit power, while **The Archivist** (`ArchiveNarration`) voices discovered records.

### Special Systems
1. **Observer + Strategy Architecture (`03_Observer`):** Decouples terminal triggers from Codex UI, notification feeds, and lantern ember rewards, supporting session replay (`StoredDelivery`) without double payouts (`isReplay`).
2. **DualSense Hardware Telemetry:** Native macOS Bluetooth HID (`0x31` / `0x35`) lightbar color mirroring, envelope haptics across 8 gameplay cues, adaptive R2 trigger tension, and bandpass-filtered (440 Hz–5.6 kHz) Opus micro-speaker foley.

### Progression
Cumulative ember carryover across sealed stages, stage-by-stage progression tracks unlocked in *Realms & Chapters*, and session-persistent Codex entries + decryption progress across deaths and room resets.

---

## 3. Story

### Narrative
- **Beginning:** Four centuries after "The Fall," condemned Ruin Researcher **Light** awakens inside pitch-black ruins (`First Light`) with no fuel, igniting a lone ember to enter the sealed facility and earn his freedom by scavenging Old World tech.
- **Middle:** Traversing the *Abandoned Corporate Archive* (`FurkanTutorial`, `Office`, `OfficeAnnex`), Light discovers frozen workfloors, flooded hallways, and glowing Ember Veins alongside forgotten employee logs detailing the final shifts before abandonment.
- **End & Conflict:** Descending toward the *Subterranean Covert Lab*, scattered records coalesce into the classified cause of the Fall. The core conflict pits survival greed (hoarding embers for darkness protection) against curiosity and mastery.

### Characters
- **Light (Protagonist):** 172 cm gaunt, pale researcher with unkempt dark hair, utilitarian shirt/trousers, and a hip ember pouch (`bag for embers`).
  - *Quirks:* Physically stows lantern onto his belt while climbing ledges.
  - *Strengths / Weaknesses:* Agile climber and sharp logical thinker; zero combat power and fragile against total darkness (>3s fatal timer).
  - *Motivation:* Freedom from condemnation and uncovering what destroyed the Old World.
- **The Archivist (Disembodied Narrator):** Crisp, dry vocal presence (`kokoro-fp16-george-v2` TTS profile) reading recovered Archive logs inside the 3D Codex.
- **Pre-Fall Clerks & Keepers:** Characterized solely through leftover records (`01_KeepersNote` through `04_OfficeMargin`).
- **Relationships:** Entirely solitary relationship between Light, his fading lantern, and dead bureaucratic voices of the past.

### Dialogue
No interactive NPC trees. Storytelling runs through four authored **Codex Records** (*The Keeper's Last Shift*, *A Margin in the Dark*, *The Unfinished Ledger*, *A Note Above the Dust*) with `RoomClue` annotations, subtle whisper hints, and terse status lines (*"The light went out"*, *"You fell"*, *"Caught in a hazard"*).

### Storyboards
1. *Awakening in Black:* Lone glowing ember pulses -> player walks to ignite first Dim radius and reveal HUD.
2. *Waking the Seal:* Collect 3 reserve embers -> Flare inside the 3-mark shrine -> white portal arch draws shut.
3. *Office Traverse:* Push wooden crate against raised tier -> stow lantern and climb ledge -> cross dark service shaft.
4. *Zero-Second Rescue:* Hit 0.0–1.2s remaining in Darkness timer while reaching an Archive Terminal -> live log fires +1 emergency ember, lighting the 3D Codex.

---

## 4. Art

### Design
- **2D Side-Scrolling High-Contrast Pixel Art:** Crisp point-filtered tiles and characters framed against oppressive abyss blacks (`Office` gray/blue tier separation, desks, cabinets, cracked monitors, broken windows).
- **3D Pixel-Filtered Codex Asset:** Authored meshes (bevelled leather spine, brass corners, cloth bookmark, folio pages) rendered at fractional display scale into a point-filtered buffer so 3D book curls match 2D pixel density.
- **Character & Concept Sheets:** Derived directly from Maksim Martynenko's *Light* concept art & pixel study and Can Mutluşahinoğlu's environmental tiles (36-cell Office Tile Palette).

### Visual Effects
`LanternSparksFX` ambient ember particles, expanding warm light waves on ignition and chamber completion, sub-25% fuel lantern flicker, animated *First Light* conduit paths, and respectful particle throttling/suppression under Reduced Motion.

### Lighting
URP 2D Lighting (`Light2D`) casting radial visibility circles and hard silhouette shadows; contrasting warm lantern amber against cold slate-blue reception lights and pulsing neon Ember Vein cables. Custom `ArchiveBookSurface.shader` applies 5 diffuse ramp bands, 5-bit color quantization, and an ambient spine valley shadow.

### Animation
9 sequences / **131 hand-authored pixel frames** (`Idle`, `Walk`, `Jump`, `Fall`, 10-frame `Push`, `Sheathing`, 27-frame `Climb`, etc.) managed by `PlayerSpriteAnimator` + `PlayerActionWalkLayer`, using pixel-derived hand, foot, and sole anchors for exact ledge corner alignment. Codex pages animate via real-time tessellated mesh curling and quintic smootherstep easing.

---

## 5. Audio

### Music
Restrained dark ambient synth/drone accompaniment (`BackgroundMusicManager`) designed to preserve quiet pockets so lantern cues and darkness tension remain impactful. Clean volume scaling and fade coordination across menu/transition states.

### Sound Effects
**67 curated, CC0-licensed, 44.1 kHz mono foley assets** documented in `archive-sfx-manifest.json` (zero procedural beeps):
- *Environment & Player:* Surface-specific footsteps (stone, wood, metal, water) with non-repeating shuffle, soft landing, crate slide.
- *Lantern & Circuits:* Match ignition for Flare, crackling fire extinguish/break, surge/latch mechanical shifts for Ember Veins, danger heartbeat below 25% fuel.
- *UI & Codex:* Authentic cloth/book handling and crisp leather/paper page turns.
- *DualSense Speaker:* Bandpass-filtered (440 Hz–5.6 kHz) 48 kHz Opus packets preventing micro-transducer rattle.

### Voice Acting
Offline content-addressed TTS synthesis (**The Archivist**, `ArchiveNarration` using `kokoro-fp16-george-v2` SHA-256 cached WAV clips) reading recovered Codex titles and text aloud with interactive Play/Pause/Replay controls and continuity across multi-page records.

---

## 6. Game Experience

### UI / UX
- **Style:** Dark obsidian/slate background (`#0C1014`), warm parchment ivory text (`#EFE8D8`), antique brass rules & selection accents (`#805D15` / `#C49A45`), `ArchiveSerif` display headers paired with crisp sans-serif body legibility.
- **Flow:** Minimal HUD during exploration -> seamless Pause overlay (`Continue`, `Settings`, `Button Guide`, `Restart Chamber`, `Return to Title`) -> clean frameless Death Screen -> tactile 3D Codex dossier.

### Controls
Full parity across **Keyboard/Mouse**, **Xbox**, and **PlayStation** controllers with live device-aware glyph swaps (`ControllerGlyphService`):

| Action | Keyboard / Mouse | Xbox Controller | PlayStation Controller |
| --- | --- | --- | --- |
| Move Left / Right | `A / D` or `Left / Right` | Left Stick / D-Pad | Left Stick / D-Pad |
| Ledge Climb (at ledge) | `W` or `Up Arrow` | D-Pad Up / Stick Up | D-Pad Up / Stick Up |
| Jump / Variable Jump | `Space` | `A` | `Cross` |
| Lantern Flare | `F` | `RT` (>= 65%) | `R2` (>= 65%) |
| Interact / Ember Vein / Desk | `E` | `Y` | `Triangle` |
| Open / Close Codex | `Tab` | `View` | `Create` |
| Codex Page Drag | Mouse Left-Drag (any edge) | `LB / RB` or D-Pad | `L1 / R1` or D-Pad |
| Pause / Resume | `Esc` or `P` | `Menu` | `Options` |

### Menus & HUD
- **Title Menu:** *Start Game*, *Realms & Chapters*, *Options*, *Button Guide*, *Credits*, *Quit Game*.
- **Pause Menu:** Dual-column layout (actions left; live chamber name + glowing lantern ember count + memento status right) with safe confirmation dialogs before destructive restart/exit.
- **Settings (4 Tabs):** *Audio* (Master, Music, SFX, DualSense Speaker + Test), *Display* (FPS Cap 60/120/Unlimited), *Controller* (Glyph Style, Vibration, Adaptive Triggers, Lightbar, Speaker, Stick Deadzone), *Accessibility* (Reduced Motion).
- **Gameplay HUD:** Upper-left 4 ember slot stones (concealed until first ignition; animated fracture on Flare spend) + contextual button hint + subtle bottom recovery feed.

### Diegetics
The physical lantern on Light's sprite reflects Dim vs. Flare vs. flicker; glowing Ember Vein conduits visually trace circuit energy; DualSense RGB Lightbar casts ambient state color (`#F29424` Amber, `#5AE6FF` Flare Cyan, `#F2140D` Critical Crimson) directly onto the player's hands; Archive Terminals sit as physical lit filing desks; the Codex behaves as an interactive ink journal rather than a flat database screen.

### Integration
- **Accessibility:** Full *Reduced Motion* mode (disables camera shake, orb rotation, flame pulse, dust drift, and snaps 3D page curls), non-color-only cues (text labels + heartbeat SFX + haptics), explicit glyph override (*Auto / Keyboard / Xbox / PlayStation*), and configurable deadzone (1% gamepad steps).
- **Scope:** Single-player atmospheric experience (no multiplayer chat/lobbies); survival timing driven by the 3.0s zero-ember darkness clock.

---

## 7. Market Requirements

### Priorities (MoSCoW)
- **Must Have:** Walking/jump/push/climb traversal; 4-ember Dim/Flare & 3s darkness death; Ember Vein Power/Drain circuits (shelf, lift, water); Lore Collectibles / Codex via Observer pattern; First Light + 2 Office chapters; Menus & dual Keyboard/Gamepad support.
- **Should Have:** 3D interactive pixel Codex book, Archivist TTS voiceover, 67 edited CC0 foley cues, Reduced Motion accessibility, DualSense lightbar/haptics/speaker.
- **Could Have:** Zone 2 (*Subterranean Covert Lab* with cryo-tanks, laser barriers, turbines), additional lore & audio logs, chapter completion stats.
- **Want to Have:** Full PC platform achievements, custom runtime key remapping UI, localized multi-language Codex text.

### Minimum Viable Product (MVP)
Complete playable progression through *Main Menu -> First Light -> FurkanTutorial -> Office / OfficeAnnex* in pure 2D pixel art, demonstrating all core mechanics (movement, push, climb, 4-ember lantern, darkness death), reversible Power/Drain Ember Veins, and the Observer-driven 4-record Archive Codex.

### Stretch Goals
Zone 2 (*Subterranean Covert Lab* with sci-fi hazards), full 15–20 minute 5-chamber narrative climax activating the deepest Old World Archive, extra audio logs, and achievement integration.

### Delivery
**DGD3004 Game Development I** Final Project release targeting standalone Windows & macOS executables (Unity 6000.3.2f1) alongside clean tracked source control (`main` trunk).

### Marketing
Community showcase on **itch.io** and university exhibition centered on stark chiaroscuro pixel visuals: dynamic Flare reveals in pitch black, tactical circuit flips turning electrified floods safe, and tactile 3D Archive Book page turns.

### Post-Launch
Structured playtest telemetry review (per-chamber clear time, death counts, average fuel left at exits, puzzle readability bottlenecks), cross-owner circuit bug hardening, standalone build controller QA across Windows USB & macOS Bluetooth, and potential Zone 2 DLC.

---

## 8. Technical Requirements

### Known Issues
- Cross-owner mechanism gaps documented in `DEVELOPER-MECHANICS-2026-10-18.md`: synchronizing shared multi-node `EmberVein` circuits, gating `Drain` against full 4/4 lantern inventory inside Kemal's circuit path, mid-path waypoint freezing on `RollingShelf`, gravity descent on `SimpleLift`, initial state wiring on `ElectrifiedWater`, and resetting mechanisms cleanly upon chamber retry.
- `Office.unity` exit transition requires linkage to its subsequent chapter scene once Zone 2 is built.
- Standalone macOS/Windows physical controller driver matrix (beyond macOS Editor Bluetooth testing) pending pre-release hardware QA.

### Systems
Clean modular `.asmdef` separation across the codebase:
- `TheLightenRoad.Lantern`: `LanternFuel`, `LanternController`, `EmberPickup`.
- `TheLightenRoad.Player`: `PlayerMotor2D`, `PlayerLedgeClimb`, `PlayerSpriteAnimator`, `PlayerActionWalkLayer`, `FollowCamera2D`, `PlayerRespawnController`, `ChamberCompletionSequence`.
- `TheLightenRoad.Observer`: `SocialChannel`, `ISocialListener`, `SocialEvent`, `SocialEventKind`, `StoredDelivery`, `AmbientMembership`, `PartyFeedUI`.
- `TheLightenRoad.Codex`: `ArchiveLog`, `ArchiveCatalog`, `CodexSession`.
- `TheLightenRoad.Frontend`: `MainMenuController`, `PauseMenuController`, `ArchiveCodexMenu`, `ArchiveBookPresentation`, `ArchiveBookPageTurn`, `ArchiveNarration`, `DiegeticWhisper`.
- `TheLightenRoad.Input`: `ControllerGlyphService`, `DualSenseLightbar`, `DualSenseSpeaker`, `DualSenseTextures`.
- `TheLightenRoad.Interaction` & `TheLightenRoad.Mechanisms`: `EmberVein`, `RollingShelf`, `SimpleLift`, `ElectrifiedWater`.
- `TheLightenRoad.Audio`: `BackgroundMusicManager`, `SoundEffectManager`, `PlayerAudioController`.

### Work Packages
- **WP1 — Core Foundation & Traversal:** Unity 6000.3.2f1 setup, walking/jump motor, camera bounds, crate physics, pixel-anchored ledge climbing.
- **WP2 — Lantern Economy & Survival Loop:** 4-ember capacity, Dim/Flare mechanics, 3.0s Darkness Death timer, Ember HUD & Death screen.
- **WP3 — Ember Veins & Environmental Circuits:** Multi-node circuit state, patrolling rolling shelves, gravity lifts, electrified water, retry reset sync.
- **WP4 — Lore Collectibles / Codex & 3D Book:** `03_Observer` pattern channel (`LogFound`), 3D pixel-filtered book shader & drag curl, Kokoro TTS narration.
- **WP5 — UI/UX, Audio Foley & Hardware Polish:** Dual-column Pause & 4-tab Settings, 67 CC0 foley SFX cues, DualSense RGB/haptics/speaker, Reduced Motion.
- **WP6 — Full Integration, Playtesting & Release:** End-to-end scene chaining, cross-owner bug fixes, standalone Windows/macOS packaging.

### Tasks & Team Ownership
| Member | Role | Owned Scope & Accountability |
| --- | --- | --- |
| **Siraç Göktuğ Şimşek** | Game Developer | `Player/`, `Lantern/`, `Observer/`, `Codex/`, `Frontend/`, `Input/`, `Audio/`, First Light & Office scenes, 430+ automated PlayMode tests. |
| **Kemal Berke Aşık** | Game Developer | `Interaction/`, `Mechanisms/`, `EmberVein` shared circuit synchronization, `RollingShelf`, `SimpleLift`, `ElectrifiedWater`. |
| **Furkan Aydın** | Game Designer | Core Loop design, ember economy tuning, chamber layouts, puzzle clues & Codex narrative prose, playtest metrics. |
| **Can Mutluşahinoğlu** | Artist | Environmental pixel art tilesets, Archive & Corporate Office props, background silhouettes, local lighting mood. |
| **Maksim Martynenko** | Artist | Protagonist *Light* concept & pixel study, 131-frame character sprite sheets (`Idle`, `Walk`, `Jump`, `Push`, `Sheathing`, `Climb`), ember concepts. |

### Activities & Production Passes
- **Pass 1 — Prototype Pass:** Movement sandbox, Lantern Fuel API, greybox Ember Vein interaction tests.
- **Pass 2 — Gameplay Pass:** *First Light* prologue, *FurkanTutorial*, *Office* / *OfficeAnnex* routes, Observer-driven Codex system, full-inventory/replay safeguards.
- **Pass 3 — Polish Pass:** 3D Archive Book shader & any-edge page physics, 67 edited foley cues, Pause/Death UI overhaul, walking-only balance refinement, DualSense feedback.
- **Pass 4 — Demo & Release Pass:** Cross-owner circuit integration merge, end-to-end playtesting & telemetry balancing, standalone Windows/macOS builds.
