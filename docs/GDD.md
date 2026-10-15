# The Lighten Road — Game Design Document

**Version:** 0.1 · **Status:** Pre-production design · **Updated:** 2026-10-15

This editable edition reorganizes the original three-page [team GDD](The-Lighten-Road-GDD.pdf), including its core-loop diagram and Elemental Tetrad table. Gameplay descriptions are design intentions. Implementation milestones and open questions at the end are planning additions; they are not claims about an existing build.

## 1. Overview and pitch

A tense, atmospheric 2D puzzle-platformer in which a frightened explorer must manage the fuel of a mysterious lantern to navigate pitch-black, post-apocalyptic ruins.

| Attribute | Design |
| --- | --- |
| Genre | Puzzle-platformer |
| Perspective | Pure 2D side-scrolling |
| Visual style | High-contrast pixel art |
| Core resource | Ember fuel |
| Primary audience | Explorers |
| Secondary audience | Achievers |
| Intended technology | Unity 2D, C#, Unity Tilemap, URP 2D Lighting |
| Full scope | 4–5 segmented chambers across two zones |
| Target run length | Approximately 15–20 minutes; chambers approximately 2–4 minutes |

Timing is an estimate from the source design and has not been validated through playtesting.

## 2. Team

| Team member | Role | GitHub |
| --- | --- | --- |
| Siraç Göktuğ Şimşek | Game Developer | [@unitybtw](https://github.com/unitybtw) |
| Kemal Berke Aşık | Game Developer | [@kemal-berke](https://github.com/kemal-berke) |
| Can Mutluşahinoğlu | Artist | — |
| Maksim Martynenko | Artist | — |
| Furkan Aydın | Game Designer | — |

## 3. Target player and experience

**Explorer:** Reveal hidden lore fragments, inspect atmospheric environments, and discover routes through the ruins.

**Achiever:** Master the fuel economy, optimize movement routes, and execute Power / Drain puzzle solutions efficiently.

The world imagines the aftermath of an apocalypse four hundred years in the future. The intended emotional progression combines loneliness, fear of darkness, uncertainty, satisfaction from solving puzzles, and the uneasy relief of illuminating a room only to discover that the player is still alone.

## 4. Story and setting

The protagonist, **Light**, is a condemned Ruin Researcher equipped with an anomalous ember lantern. Forced to scavenge ancient technology from a pre-Fall ruin in exchange for his freedom, he descends through a silent, decaying facility toward a classified subterranean archive containing the truth behind the Fall.

The two planned zones are:

1. **Abandoned Corporate Archive:** Decaying corporate interiors, archive shelves, lifts, and electrical hazards. This zone contains the two Office stages that form the MVP.
2. **Subterranean Covert Lab:** Cold, clinical corridors and advanced machinery. Proposed obstacles include cryo-stasis tanks, laser containment fields, and ventilation turbines.

## 5. Core loop

1. **Action:** Enter a dark area, spend ember fuel to scout with the lantern, and interact with an Ember Vein to change an obstacle's state.
2. **Result:** Reveal the layout and hazards; activate a moving platform, cut power to lethal water, or otherwise change the machinery.
3. **Reward:** Traverse the platforming challenge, uncover lore, and scavenge ember fuel.
4. **New decision:** Continue toward the exit or take a detour for more resources and collectibles.

Leaving a solved room seals the doorway behind the player and leads into the darkness of the next stage. Remaining fuel carries forward, so conservation is a cumulative challenge across the level.

### Session boundaries

**Start:** Enter a sealed sector with a basic ember supply, a dimly glowing lantern, and an unpowered route ahead.

**Success:** Reach the end of a stage and unlock the threshold to the next zone. The overall win condition is reaching the final stage and activating the Old World archive.

**Failure:** Enter a lethal hazard or exhaust lantern fuel. The design calls for death, loss of scavenged resources, and respawn at the beginning of the current stage. The exact reset and fuel restoration rules still need definition to ensure a solvable retry.

## 6. Player actions and mechanics

### Movement and carrying

Move left and right and jump. The source says the player cannot sprint, or jump while carrying heavy objects; whether sprinting is prohibited generally or only while carrying needs clarification. Heavy-object interaction details and input bindings are also undecided.

### Lantern: Dim / Flare

- **Dim:** Passive glow that conserves fuel and limits visibility.
- **Flare:** Wider visibility that reveals safe routes, hazards, and hidden items at a higher fuel cost.
- **At zero fuel:** Trigger the designed failure and respawn flow.

The source does not define exact light radii, capacity, drain rates, or whether Dim consumes any fuel. These remain tuning decisions.

### Ember Veins: Power / Drain

Interact with ancient environmental conduits to power or drain nearby machinery. Power can move platforms or other obstacles; Drain can stop machinery and disable hazards. The first implementation should cover rolling archive shelves, one lift, and electrified water.

The source does not establish transfer amounts, interaction range, activation time, or whether draining machinery replenishes the player's lantern. Those rules must be resolved before implementation.

### Meaningful choices

| Choice | Benefit | Cost / pressure |
| --- | --- | --- |
| Vision vs. survival | Flare reveals safer routes and hidden items | Faster fuel consumption |
| Dim vs. certainty | Dim preserves resources | Reduced visibility and riskier traversal |
| Power vs. Drain | Change machinery to open or secure a route | Timing and machine-state decisions |
| Exit vs. detour | Optional routes can reveal lore and resources | Additional exposure and fuel use |

## 7. Feedback and resource pressure

Activating an Ember Vein should produce a surge sound, glowing neon cables, and mechanical movement audio so the resulting state change is understandable.

Below **25% fuel**, the lantern should flicker and an ambient heartbeat should intensify.

The source calls the depletion system a negative feedback loop. Its described behavior is an escalating resource-pressure spiral: inefficient routing burns fuel, lower fuel limits light use, reduced visibility makes hazards harder to anticipate, and traversal becomes more difficult. That terminology is clarified here without changing the intended tension. Playtesting must check that the pressure remains recoverable rather than producing unavoidable repeated failure.

## 8. Elemental Tetrad

| Element | Design summary |
| --- | --- |
| Mechanics | Precision puzzle-platforming, dynamic vision radius, route memorization, and Ember Veins controlling environmental machinery. |
| Story | A condemned researcher seeks freedom through scavenging and discovers the truth behind the Fall. |
| Aesthetics | Pitch-black voids, a warm lantern glow, pulsing neon conduits, decaying brutalism, and clinical laboratory spaces. |
| Technology | Unity 2D, C#, Tilemap, and URP 2D Lighting for radial visibility and light masking / vision cones. |

## 9. Scope and MVP

### Full design target

4–5 segmented puzzle chambers across two thematic zones. A complete run targets 15–20 minutes, with individual chambers targeting 2–4 minutes. Transitions and exploration pacing must be evaluated in playtests rather than assumed from these estimates.

### MVP acceptance goals

- Two playable Office stages in pure 2D pixel art.
- Character movement and jumping.
- Lantern Dim / Flare states and fuel economy.
- Base Ember Vein Power / Drain interaction.
- Rolling archive shelves, one lift, and electrified water.
- Clear stage entry, completion, failure, resource-loss, and respawn behavior.

These are acceptance goals, not completed features.

### Contingency

If time runs short, cut the entire laboratory zone and its advanced science-fiction obstacles. Focus on the narrative arc, art, and puzzle pacing of the two Office stages as a self-contained playthrough.

## 10. Proposed implementation milestones

| Milestone | Reviewable outcome |
| --- | --- |
| Project foundation | Unity project committed with exact editor/package versions, scene organization, and reproducible opening instructions. |
| Mechanics prototype | Greybox room demonstrates movement, Dim / Flare fuel use, Power / Drain, and failure recovery. |
| Office MVP | Two connected stages demonstrate all MVP machinery and fuel carryover. |
| Playtest and polish | Record completion time, deaths, fuel remaining, puzzle confusion, and retry solvability; refine readability and atmosphere. |
| Scope decision | Add the laboratory only if the Office MVP is stable and the schedule allows it. |

## 11. Open decisions

- Unity editor version, URP package versions, target platforms, and input bindings.
- Sprint restrictions and heavy-object carrying rules.
- Fuel capacity, Dim / Flare costs, pickup values, and light radii.
- Whether Power / Drain consumes or transfers lantern fuel and whether machine states persist.
- Exact room / stage boundaries, checkpoint behavior, resource-loss rules, and guaranteed retry fuel.
- Chamber count, puzzle ordering, optional-route rewards, and pacing.
- Accessibility options for darkness, flicker, audio cues, and input remapping.
- Detailed task assignments, schedule, and asset licensing.

## 12. Source and change policy

The original PDF is preserved at [The-Lighten-Road-GDD.pdf](The-Lighten-Road-GDD.pdf). This Markdown document is the working design. Update it when the team makes a decision, and distinguish planned behavior from behavior verified in a playable build.
