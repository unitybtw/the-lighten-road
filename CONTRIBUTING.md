# Contributing to The Lighten Road

## Start with the design

Read [the GDD](docs/GDD.md) and its open decisions before implementing a system. Discuss changes to mechanics or scope in an issue or pull request so the working design stays consistent with the game.

Read [the collaboration guide](docs/COLLABORATION.md) for personal workspaces, shared scene ownership, and integration into `Game/`. The team uses a direct-to-`main` trunk-based workflow to keep development fast.

## Working together

1. Pull the latest `main` before starting work (`git pull --ff-only origin main`).
2. Keep work isolated within your assigned sandbox scene and folder ownership (e.g. `Sandbox/Sirac/` vs `Sandbox/Kemal/`) to prevent scene merge conflicts.
3. Keep each change small and verified in Unity (clean console, passing tests).
4. Update the GDD and workspace notes when behavior, setup, or project status changes.
5. Commit and push directly to `main` with descriptive messages. Never force-push shared history.

## Unity project conventions

The shared Unity project is under `Game/` and uses Unity **6000.3.2f1** with the **Universal 2D** template. Follow [Game/README.md](Game/README.md) for opening and collaboration notes:

- Keep the exact editor version and package dependencies recorded.
- Use visible `.meta` files and Force Text asset serialization.
- Commit `Assets/`, `Packages/`, `ProjectSettings/`, and associated `.meta` files together.
- Keep `Library/`, `Temp/`, logs, local settings, builds, and credentials out of Git.
- Check large assets before adding them; agree on Git LFS before introducing files that need it.
- Include asset source, license, and attribution for third-party art, audio, fonts, and packages.

## Validation

Documentation changes should have working links and accurately distinguish plans from implemented features. Gameplay changes should record the Unity version, scenes tested, expected result, observed result, and any unresolved limitations. Do not describe an untested build as playable or a timing estimate as a measured result.

## Access and credits

Repository collaborators can work in branches directly; other contributors can use forks and pull requests. Collaborator invitations must be accepted by the recipient. GitHub's Contributors list is based on qualifying commits, so an access invitation alone does not place someone in that list.
