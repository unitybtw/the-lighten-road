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
