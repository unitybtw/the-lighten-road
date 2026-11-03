# Pause menu redesign — 2 November 2026

The pause screen now puts actions in a left column and the current chamber and lantern in a separate right column. A small amber marker identifies the current selection; its explanation appears below the list. Recovery/exit actions are separated from ordinary navigation. Existing confirmation, input ownership and time-scale behavior are retained.

The warm ivory/brass palette replaces the blue title bloom and decorative crowns. Body text uses a readable sans font alongside the archive display font. Ember count and flame read the active lantern state; the count is hidden when no active lantern is available, including the opening tutorial before ignition. Memento status appears only in chambers which track it, without adding an invented lore quotation or a puzzle solution.

Settings and controls keep their existing functionality with clearer body text. The controls footer has separate space for all four shortcuts. Restart, return-to-title and reset-default decisions retain cancellation as their initial selection.

## Validation

Unity 6000.3.2f1 compiled with zero errors/warnings. The final CLI run passed all **27 pause-related Play Mode tests**, covering menu actions, input ownership, restart/exit confirmations, tutorial resumption and layout. The broader readability suite also passed 11/11 cases before the final footer spacing pass; its pause case was included in the final run. Project integrity passed for 599 tracked assets.

Home renders were inspected at 1920×1080, 1280×720 and 1024×768. A transient offscreen capture omitted submenu text, so the shipped Screen Space Overlay UI was also opened and captured directly from the running FirstLightTutorial Game view: both home and controller guide displayed their text correctly. No standalone build or physical controller test was performed in this pass.

Saved preferences were restored and Unity returned to the original clean FirstLightTutorial scene in Edit Mode. Test-created font/quality caches are excluded, as are the pre-existing local font/recovery changes. Kemal's protected sources and sandbox are unchanged.

## Previews

- [Actual Game view: home](previews/pause-redesign/in-game-home.png)
- [Actual Game view: controller guide](previews/pause-redesign/in-game-controls.png)
- [Home with a three-ember lantern, 1080p](previews/pause-redesign/home-1920.png)
- [Home layout at 1024×768](previews/pause-redesign/home-1024.png)
- [Restart confirmation](previews/pause-redesign/restart-confirmation.png)

## Controller settings and clearer navigation — 3 November 2026

Pause Settings now has separate Audio, Display, Controller and Accessibility tabs.
Controller contains button-symbol style, vibration, adaptive triggers, lightbar,
speaker and stick deadzone preferences. These use the existing shared settings
model, including save/reset behavior; opening the page does not change a saved
deadzone. Q/E and controller shoulders switch the visible section. Navigation
links stay on the active page and reset still requires confirmation.

The former Controls action is now **Button guide** in both the pause and title
menus. Device-guide tabs only preview bindings; they do not force a button-symbol
preference or toggle vibration. A Controller settings link opens the complete
settings page. Gamepad climbing instructions describe directional up, and static
key legends no longer use the same bright glow as a selected action.

Returning from Settings or Button guide restores the originating pause action;
a newly opened pause menu starts on Continue. Mouse hover and keyboard/controller
navigation share the EventSystem selection, including slider children, so a
stationary mouse cannot leave another settings row highlighted. The title menu
also consistently names its settings pages Settings, Display and Button guide.

Unity 6000.3.2f1 / Unity CLI compiled with **zero errors and warnings**. The final
Frontend Play Mode run passed **174/174 tests**, covering preference persistence,
confirmation, input ownership, focus, menu routing and all four pause settings
sections at 1280×720 and 1024×768. The navigation test walks the explicit page graph
without relying on the hardware mouse position left by another resolution test.

Actual Game view captures were reviewed for the pause home screen, Controller
page, PlayStation button guide and title settings index. The header, save status and controls were checked after the views settled.
Personal preferences were restored. Pre-existing font/recovery changes and
Kemal-owned paths are excluded. No standalone build or physical-controller test
was performed in this pass.

- [ESC home](previews/pause-controller-fix/home-1920.png)
- [Controller settings, 1080p](previews/pause-controller-fix/controller-1920.png)
- [Controller settings, 720p](previews/pause-controller-fix/controller-1280.png)
- [PlayStation button guide](previews/pause-controller-fix/guide-1920.png)
- [Title settings index](previews/pause-controller-fix/title-settings-1920.png)
