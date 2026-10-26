# Controller input pass

The gameplay layout keeps each primary action on a separate control. Existing keyboard bindings are preserved.

| Action | PlayStation | Xbox | Keyboard |
| --- | --- | --- | --- |
| Move | Left stick / D-pad | Left stick / D-pad | A/D / arrows |
| Jump | Cross | A | Space |
| Sprint, hold | L1 | LB | Shift |
| Sprint, toggle | L3 | Left stick click | — |
| Flare | R2 | RT | F |
| Interact | Triangle | Y | E |
| Pause / resume | Options | Menu | Escape / P |
| Confirm in menus | Cross | A | Enter |
| Back in menus | Circle | B | Escape |
| Controller guide tabs | L1 / R1 | LB / RB | Q / E |

L3 toggles sprint while moving; returning movement to neutral, disabling the player or changing controllers clears the toggle. R2 no longer also sprints. Flare requires 65% trigger travel and rearms below 35%, preventing repeated ember spending from trigger jitter or holding the trigger. A controller connected with its trigger already held must release it first.

The gameplay stick uses one configurable inner deadzone, rescaled to retain the full analog range. Keyboard and D-pad movement stay digital. Automatic prompts follow deliberate keyboard/mouse or controller input; merely connecting a controller or small stick drift no longer forces controller prompts.

Pause preserves motor/lantern lifecycle and rigidbody momentum. Gameplay input is blocked through the resume frame and the following frame. The frontend temporarily suspends existing vein input polling while paused, then restores only the components it suspended after menu input has cleared. The circuit and mechanism implementations are unchanged. Their legacy extra interaction aliases remain supported, but the guide presents Triangle/Y as the canonical interaction button.

Options/Menu resumes directly from any pause page; Circle/B goes back a page. D-pad left/right no longer both navigates focus and switches device tabs. Destructive restart/default-reset shortcuts were removed; those actions remain available through their labeled menu buttons. Menu audio sliders use 5% controller steps; the deadzone slider uses 1% steps. Mouse dragging stays continuous.

## Validation

Unity 6000.3.2f1: **179/179 Play Mode tests passed**, covering frontend, haptics/speaker protocol regressions and player integration. New cases exercise digital D-pad movement, deadzone endpoints, trigger hysteresis/hotplug seeding, single-spend flares, independent sprint and sprint toggle, automatic keyboard prompts with a connected pad, precise slider steps, pause momentum, disabled actor preservation and deferred legacy-vein polling restoration with unchanged circuit state. Existing preferences and chamber completion progress were restored after the suite. Results are in ignored `Game/Library/Controls-tests.xml`.

Live Editor Game-view review confirmed the updated PlayStation controller diagram and keyboard bindings render correctly with no Console errors. Project integrity passed. Physical controller feel and standalone builds require separate hands-on validation.
