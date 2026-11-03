# Ember HUD clarity — 3 November 2026

The HUD distinguishes held fuel from the stone already spent on a Flare. When the last held ember is consumed, **NO EMBERS** takes priority over the active-Flare label. Minimal mode still shows this warning; recovering fuel returns to the chosen minimal layout. This changes presentation only, including neither the darkness grace period nor the fuel rules.

The optional action row now shows **Flare / -1 ember** before use, **End flare** while it can be stopped, and **Dimming** during release. A legacy half-charge reports **Need 1 ember**, without advertising an input that cannot start Flare. Controller glyphs and remapped keyboard keys continue to use the existing input service. “Fading” and “Cracking” text has been removed because the stone already shows that progression.

Held stones remain still. The spent stone keeps its existing fade/fracture animation, but its smaller light diminishes with the actual scout progress. Brief reactions to inventory changes remain; continuous breathing and random aura scaling have been removed. Optional sparks are smaller, slower and less frequent. Reduced Motion still suppresses particles and decorative movement. The empty warning uses a larger text size and the full action-row width, so minimal mode does not hide essential survival feedback.

Losing a target now hides its stale inventory. Automatic discovery prefers the active lantern and retries at a bounded interval when none exists. Rebinding clears presentation and subscribes to the actual new target without inventing a pickup. Existing art slots, sounds, fuel transactions and Kemal's code/scenes are unchanged.

## Verification

- Unity 6000.3.2f1 compiled with zero errors/warnings. The final player Play Mode assembly passed **164/164** tests in 67.31 seconds. Four new regressions cover the last-Flare warning in minimal mode, action availability/cost, stable reserves and progress-driven light, and target destruction/replacement. Existing key/glyph caching, fuel, crack, spark, pickup and reduced-motion cases also passed.
- Inspected Game-view captures of idle, burning, empty/minimal and PlayStation states at 1080p/720p. The empty warning was enlarged after the first visual pass, then the entire player assembly was rerun successfully.
- Captures use the normal FurkanTutorial scene with the gameplay clock frozen and the actual lantern scout advanced to 35% used. That makes the inventory state reproducible without triggering a darkness death during inspection. The scene was not saved; preferences and the starting editor scene were restored afterward.
- No standalone build or physical-controller pass was performed. The frontend and other test assemblies were not rerun for this HUD-only change.

[Idle and cost](previews/hud-clarity/idle-1920.png) · [Flare](previews/hud-clarity/flare-1920.png) · [Empty/minimal 720p](previews/hud-clarity/empty-minimal-1280.png) · [PlayStation](previews/hud-clarity/playstation-1280.png)
