# The Lighten Road — Unity project

The shared Unity project was created with the **Universal 2D** template in Unity **6000.3.2f1**. The project lives in this directory. It is an empty 2D URP starting point; no gameplay, scenes, or playable build have been implemented yet.

## Open the project

Open this repository in Unity Hub and select `Game/`, or open this folder directly with Unity Hub using editor version `6000.3.2f1`.

## Project structure

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

The `_Project` folders above have been created and Unity has generated their `.meta` files. Keep editable art sources in the repository's `art/` folders and puzzle documents in `design/`; import reviewed exports into this Unity project.

Before the first gameplay implementation, review the [proposed developer task split](../docs/DEVELOPMENT_PLAN.md) and agree on the active scene, input bindings, player / lantern interface, and recovery rules. Coordinate edits to shared scenes, prefabs, packages, and project settings using [the collaboration guide](../docs/COLLABORATION.md). Confirm package versions and input bindings with the team before gameplay work begins.

Unity-generated `Library/`, `Temp/`, logs, and local settings are excluded from Git.
