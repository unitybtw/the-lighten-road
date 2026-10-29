# Pause UX polish

The pause flow now makes restart, return to title and preference reset deliberate decisions. Each opens a spacious, frameless confirmation with a small pixel lantern, warm light, readable explanations, and a short unscaled fade. Reduced Motion shows it immediately.

- Cancel is selected first. Keyboard and controller navigation stay inside the decision. Escape, P, Start or the controller's back button cancel the decision before leaving pause. Cancelling restores focus to the original action.
- Controller quick options refresh when reopened, and the rumble shortcut is labelled as a toggle to match its action.
- Background pause pages are hidden during a decision. Inactive or lost selection can be recovered with keyboard navigation or a controller.
- Volume changes apply immediately. Disk writes wait for a short idle period; pending changes flush when leaving a page, resuming, disabling the menu or losing application focus. The settings page shows saving/saved feedback.
- Returning to title keeps gameplay frozen during the fade and asynchronous scene load. Repeated submissions cannot start another return operation.
- The prologue identifies itself as **PROLOGUE : FIRST LIGHT**. Memento status is shown only in a chamber that actually tracks that objective.
- Tutorial headings, hints and letterboxing hide while paused and return with their existing lesson state on resume. A darker pause backdrop keeps world details from competing with the menu. The two-action footer is centred when additional controls are absent.

## Ownership

Changed frontend pause code and Siraç's First Light presentation only. Kemal's Interaction, Mechanisms, Sandbox/Kemal and development folders are excluded. Previously modified dynamic font assets and local Python caches are excluded from this commit.

## Verification

- Unity 6000.3.2f1 compiled with zero script errors or warnings.
- Frontend Play Mode suite: **152 passed, 0 failed, 0 skipped**. The first aggregate run exposed a synthetic keyboard setup timing issue; the test now waits for UI initialization and previous overlay input suppression before sending input. The clean rerun passed.
- First Light Play Mode suite: **10 passed, 0 failed, 0 skipped**, including restoration of the lesson overlay and correct prologue objective information after pause.
- In the real tutorial scene, controller preference labels and the rumble shortcut were inspected after the final copy refresh. Returning to title kept time at zero and gameplay blocked during the transition; the destination was MainMenu with time scale 1 and input released.
- Visually inspected the actual Game view for the pause screen and lantern confirmation at 1280 × 720 and settings at 1024 × 768. Summary: [validation record](pause-polish-validation.json).

[Pause screen](previews/pause-polish/main-final.png) · [Confirmation](previews/pause-polish/confirmation-final.png) · [Settings](previews/pause-polish/settings-final.png)

These checks cover Unity Editor behavior and software input. Physical controllers and standalone builds were not retested. The existing local preferences captured before testing were restored. The Editor was left outside Play Mode on FirstLightTutorial.
