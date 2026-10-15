# Contributing to The Lighten Road

## Start with the design

Read [the GDD](docs/GDD.md) and its open decisions before implementing a system. Discuss changes to mechanics or scope in an issue or pull request so the working design stays consistent with the game.

Read [the collaboration guide](docs/COLLABORATION.md) for personal workspaces, task branches, shared scene ownership, and integration into `Game/`. CODEOWNERS and PR / issue templates are included to support review; branch protection and required approvals are not configured.

## Working together

1. Pull the latest `main` and create a focused branch, such as `feature/lantern-fuel`, `art/archive-tiles`, or `docs/puzzle-rules`.
2. Keep each change small enough to review. Coordinate before editing the same Unity scene, prefab, or binary asset.
3. Update the GDD and README when behavior, setup, or project status changes.
4. Open a pull request describing what changed and how it was checked. For gameplay changes, include a short capture or clear reproduction steps when possible.
5. Merge after review and relevant checks. Never force-push shared history.

## Unity project conventions

The Unity project has not been added yet. Follow [Game/README.md](Game/README.md) to initialize it under `Game/`. When it is created:

- Record the exact editor version and package dependencies.
- Use visible `.meta` files and Force Text asset serialization.
- Commit `Assets/`, `Packages/`, `ProjectSettings/`, and associated `.meta` files together.
- Keep `Library/`, `Temp/`, logs, local settings, builds, and credentials out of Git.
- Check large assets before adding them; agree on Git LFS before introducing files that need it.
- Include asset source, license, and attribution for third-party art, audio, fonts, and packages.

## Validation

Documentation changes should have working links and accurately distinguish plans from implemented features. Gameplay changes should record the Unity version, scenes tested, expected result, observed result, and any unresolved limitations. Do not describe an untested build as playable or a timing estimate as a measured result.

## Access and credits

Repository collaborators can work in branches directly; other contributors can use forks and pull requests. Collaborator invitations must be accepted by the recipient. GitHub's Contributors list is based on qualifying commits, so an access invitation alone does not place someone in that list.
