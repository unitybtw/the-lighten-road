# Shared Unity project location

This directory reserves the location of the future shared Unity project. **There is no Unity project or playable build here yet.**

One developer should initialize it so the team does not commit competing project settings:

1. Agree the Unity editor version and 2D URP template with the team.
2. In Unity Hub, create the project in a separate temporary directory. Copy its generated `Assets/`, `Packages/`, and `ProjectSettings/` into this directory, keeping this README. Do not copy `Library/`, `Temp/`, or logs.
3. Open `Game/` through Unity Hub. Set Asset Serialization to Force Text and Version Control to Visible Meta Files.
4. Inside Unity, create the shared folders below, letting Unity generate `.meta` files. Commit the project and matching `.meta` files together.
5. Record the editor version, opening steps, entry scene, and checks in this README before merging.

## Planned runtime structure

```text
Assets/_Project/
  Scripts/          # Shared runtime code, organized by system
  Art/              # Approved sprites, tilesets, and animations
  Audio/            # Approved audio
  Prefabs/          # Reusable gameplay objects
  Scenes/
    Integration/    # Shared playable stages; coordinate edits
    Sandbox/
      Sirac/        # Developer test scenes
      Kemal/        # Developer test scenes
  Settings/         # Shared project-owned configuration
```

These are intended folders, not generated Unity assets. Keep editable art sources in `art/` and supporting puzzle documents in `design/`. Agreed code moves here from `development/`; the integrated project has one implementation per system.
