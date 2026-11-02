# Experience direction

The Lighten Road is a tense pixel-art puzzle platformer about navigating a dark,
decaying corporate archive with a scarce light source. Use the [GDD](GDD.md), the
current [mechanics contract](DEVELOPER-MECHANICS-2026-10-18.md), and team playtest
feedback as the basis for decisions. An attractive effect is not sufficient reason
to change the world's identity or the player's understanding of its rules.

## Applying that direction

- Keep darkness and quiet available. Contrast gives the lantern and important
  events their value; continuous glow, motion and sound can erase that contrast.
- Tie feedback to a cause. The menu's opening ignition can briefly affect its
  light. Moving focus between menu choices should identify the chosen item,
  without repeatedly reigniting the environment or accelerating its particles.
- Keep useful animation: authored character motion, a readable focus change and
  a transition that explains entering another screen. Complexity is not a goal.
- Use the team's character and environmental art as the visual reference. A
  coherent palette, pixel scale and material language matter more than adding
  another shader or effect. Do not infer an asset's authorship from its appearance.
- Do not introduce lore simply to fill a heading. The removed "Sanctuary of the
  Eternal Flame" subtitle did not belong to the corporate-archive setting.
- Evaluate implementation by player-visible behavior. An invisible object kept
  solely to satisfy an existence assertion is not a working feature; remove it
  and test the actual light, input, animation or transition instead.

These are project-specific review criteria, not a ban on ornament, procedural
graphics or AI-assisted tools. The aim is a deliberate, consistent experience.
Changes still need rendering/playback checks where appearance or feel matters;
passing an automated test alone does not establish either.
