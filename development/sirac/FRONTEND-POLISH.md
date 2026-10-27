# Frontend polish pass

This pass improves the existing archive menu without changing gameplay or Kemal's systems.

- Credits include all five team members and their agreed roles: Siraç and Kemal as Game Developers, Can and Maksim as Artists, Furkan as Game Designer. The roster uses larger, clearly separated text and fits the responsive menu layer.
- Exit confirmation uses larger body text, explains chapter/settings persistence and unsaved chamber progress, and keeps directional controller focus between Cancel and Quit.
- Restore-defaults confirmation has larger copy and separated actions. Background actions and irrelevant reset/tab footer shortcuts are hidden while the dialog is open. Cancel keeps preferences unchanged; restoring defaults preserves chapter progress.
- Main and pause guides explain the permanent one-ember flare cost and temporary vein transfer. The keyboard jump description reflects variable jump height.

Validation: **99/99 frontend Play Mode tests passed**, zero failed or skipped. Project integrity passed. Unity 6000.3.2f1, MainMenu scene and frontend Play Mode tests. Rendered previews cover Credits at 1280×720 and 1024×768, and both confirmations at 1280×720, alongside the existing settings/controller resolution matrix. Results are recorded in ignored `Game/Library/Polish-tests.xml`; previews are in `Game/Library/Readability/`. These are Editor/software-input checks; no physical controller or standalone build was retested in this pass.
