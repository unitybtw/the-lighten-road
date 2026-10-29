# Archive death screen

Death recovery now uses the same archive display font, silver body text, warm focus light, delicate ornaments and frameless actions as the title/pause menus. A shared UI style owns the display font, palette, soft light and responsive fit calculation. The frontend uses that same font and fit source.

All three death causes use one visual layout, with contextual English copy and a subtle cooler accent for the depths. A small lantern loses its flame as the message appears. Ash stays outside the reading column. Loud red flashes, fluorescent lighting, screen shake, rectangular button borders and prominent death counters have been removed from this overlay.

- **Rekindle the lantern** returns to the existing checkpoint through PlayerRespawnController. Fuel rules, checkpoint locations and mechanism logic are unchanged. The screen temporarily owns both automatic and legacy manual respawn inputs, so the decision remains available without a competing timer. Original controller policy is restored on respawn, target replacement or screen shutdown. Scenes with no screen retain automatic recovery.
- **Return to title** fades the content over an opaque backdrop and loads the existing main menu asynchronously. Gameplay remains blocked until the screen is gone. Keyboard Escape/controller back first selects this action; leaving still requires confirmation.
- The reveal waits for held confirm/jump/mouse buttons to be released. Navigation is rate limited, and clicking works even without moving the pointer on the click frame. The closing fade consumes the retry input before returning movement.
- Reduced Motion removes drifting, breathing and positional entrance motion and shortens the fade. Text/actions fit the safe canvas. PlayStation/Xbox prompts follow the configured controller style.
- The death overlay suppresses pause input and the prologue captions/letterboxing. Raw legacy vein input polling is suspended for the overlay lifetime using the existing pause boundary; only components enabled beforehand are restored, with circuit state left intact. PreviewDeath no longer increments the persistent death count or changes the controller's recovery policy. Development number-key preview shortcuts have been removed from normal gameplay.

Kemal's Interaction, Mechanisms, Sandbox/Kemal and development files are excluded. Existing dirty dynamic font assets and Python caches are excluded from publication.

## Verification

- Compilation completed with zero script errors/warnings. The eight new death-screen cases pass in the full suite: manual recovery ownership, held submit protection, same-frame recovery cleanup, stationary pointer clicks, controller back/pause isolation, controller-policy restoration, legacy polling ownership and responsive typography/layout.
- The final full run passed **318/319**, with all new recovery and existing gameplay cases passing. The remaining failure was the synthetic Escape event in an older pause test: the key was no longer pressed when the paused runtime frame ran. Its harness now pumps a complete input frame explicitly; the focused pause suite then passed **10/10**. Only that test harness changed after the full run. The latest outcomes across **319 distinct cases** pass; a further complete 319/319 run was not performed. [Validation record](death-screen-validation.json).
- The first broad passes exposed automatic-respawn assumptions in two authored route tests, which now choose retry through the actual screen before asserting checkpoint position/fuel. An immediate kill-and-respawn case exposed an invisible input blocker before the first render; production cleanup was fixed and covered by a regression test. Cancelled runs are not counted as passing evidence.
- Live FurkanTutorial smoke check: the real player remained Dead with automatic recovery disabled, retry restored Alive/3 embers/input with no overlay, and the title action loaded MainMenu with time scale 1, input released and no death screen. These two live actions invoked the screen's action handler; the automated cases separately exercise keyboard, pointer and synthetic controller input.
- Visually inspected all three causes at 1280×720, 1024×768 and 2560×1080 using offscreen UI projections. Also inspected an actual 1920×1080 Game view with PlayStation prompts. Reduced Motion disables drift/positional motion; safe-canvas bounds and text overflow are asserted by the tests.

[Darkness](previews/death-screen/death-Darkness-1280x720.png) · [Depths](previews/death-screen/death-Void-2560x1080.png) · [Hazard](previews/death-screen/death-Hazard-1024x768.png) · [Live PlayStation layout](previews/death-screen/hazard-playstation.png)

Testing uses Unity Editor and software input. Physical controllers and standalone builds were not retested. Captured local preferences were restored, and the Editor was left outside Play Mode on FirstLightTutorial.
