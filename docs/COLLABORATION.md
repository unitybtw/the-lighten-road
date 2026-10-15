# Team collaboration

## Work areas and ownership

The team uses a **streamlined direct-to-`main` (trunk-based) workflow** to keep development fast, transparent, and free of branching/pull-request overhead. To avoid merge conflicts (especially with Unity scenes and prefabs), development is isolated by scene and folder boundaries:

| Member | Role | Starting area | Primary code domain |
| --- | --- | --- | --- |
| Siraç — @unitybtw | Game Developer | [development/sirac/](../development/sirac/) | `Scripts/Player/`, `Scripts/Lantern/`, `Sandbox/Sirac/` |
| Kemal — @kemal-berke | Game Developer | [development/kemal/](../development/kemal/) | `Scripts/Interaction/`, `Scripts/Mechanisms/`, `Sandbox/Kemal/` |
| Can — @benesas | Artist | [art/can/](../art/can/) | `art/can/` sources and exports |
| Maksim — @Ichnolith | Artist | [art/maksim/](../art/maksim/) | `art/maksim/` concepts and exports |
| Furkan — @aeshmacodex | Game Designer | [design/](../design/) | `design/` level and balancing specs |

The shared Unity project is under [Game/](../Game/) and uses Unity **6000.3.2f1** with the **Universal 2D** template.

## Direct-to-`main` workflow

All core team members commit directly to `main` following these rules:

1. **Pull before you begin:**
   Always ensure your local repository is up to date before making changes:
   ```sh
   git pull --ff-only origin main
   ```
2. **Work in your own sandbox scene:**
   Never edit another developer's active sandbox scene at the same time. Unity scenes (`.unity`) and prefabs (`.prefab`) are serialized YAML; keeping separate scenes prevents merge conflicts entirely.
   - Siraç: `Game/Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity`
   - Kemal: `Game/Assets/_Project/Scenes/Sandbox/Kemal/`
3. **Respect script boundaries:**
   Keep runtime code within your designated folders (`Scripts/Player/` and `Scripts/Lantern/` for Siraç; `Scripts/Interaction/` and `Scripts/Mechanisms/` for Kemal). Use shared assemblies (`TheLightenRoad.Lantern`) to connect systems rather than editing shared files directly.
4. **Commit and push small, verified increments:**
   Verify changes in Unity (zero console errors, passing tests) before committing. Stage only your task's files, write descriptive commit messages, and push directly to `main`:
   ```sh
   git status
   git add <your-files>
   git commit -m "feat: description of change"
   git push origin main
   ```
5. **Never force-push (`git push --force` is forbidden).**

*(Note: Feature branches remain available as an optional tool for high-risk, experimental refactors if needed, but day-to-day progress is committed directly to `main`.)*

## Shared files and Unity scenes

- Agree one active editor for each scene, prefab, binary art source, `Packages/manifest.json`, and project settings file.
- Move and rename Unity assets inside the Unity editor so references and `.meta` GUIDs stay intact. Never regenerate a teammate's `.meta` file to resolve a conflict.
- If Git reports a scene or prefab conflict, coordinate with the other editor and inspect the result in Unity before resolving. Do not guess at serialized scene changes.
- Keep original art and concept studies in the artist's folder until reviewed. Developers import approved exports into `Game/Assets/_Project/Art/`.
- Designers describe puzzles in `design/levels/`; developers implement the agreed behavior and artists supply assets.

## Review and verification

- Record editor version, scene, reproduction steps, and observed results in your workspace notes.
- Verify tests pass using Unity Test Runner before pushing gameplay changes.
- Record untested behavior and provisional balance values clearly.
