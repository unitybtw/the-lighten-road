# Team collaboration

## Work areas

| Member | Role | Starting area | Example branch |
| --- | --- | --- | --- |
| Siraç — @unitybtw | Game Developer | [development/sirac/](../development/sirac/) | `feature/sirac/player-controller` |
| Kemal — @kemal-berke | Game Developer | [development/kemal/](../development/kemal/) | `feature/kemal/lantern-fuel` |
| Can — @benesas | Artist | [art/can/](../art/can/) | `art/can/archive-tiles` |
| Maksim — @Ichnolith | Artist | [art/maksim/](../art/maksim/) | `art/maksim/lantern-animation` |
| Furkan — @aeshmacodex | Game Designer | [design/](../design/) | `design/furkan/office-01` |

Branch names are examples, not permanent personal branches or fixed feature assignments. Agree actual tasks before starting. Personal folders help organize work; they do not impose access restrictions. The team uses one shared Unity project under [Game/](../Game/), once initialized.

## Start a task

Accept the repository invitation, clone the repository, then run the following example with your own branch name:

```sh
git switch main
git pull --ff-only origin main
git switch -c feature/sirac/player-controller
```

Create one branch per task. State the owner, affected files, expected outcome, and dependencies in an issue or PR. Before two developers work on adjacent systems, agree the interface: inputs, outputs, events, and who edits shared files. Do not duplicate shared controllers or fuel systems in personal folders.

## Submit work

Stage only the files belonging to your task, review the staged diff, commit, and push your branch. This example applies to a personal prototype:

```sh
git add development/sirac/
git diff --cached
git commit -m "feat: prototype player movement"
git push -u origin HEAD
```

Open a pull request targeting `main` and use the provided template. Request a relevant teammate's review, resolve feedback, then merge. After merging, start the next task from updated `main`. Do not force-push shared history. Keep `main` for reviewed integrated work.

This is the agreed workflow; protected-branch enforcement and required approvals have not been configured. CODEOWNERS suggests reviewers when eligible; it does not prevent edits or grant access. Pending invitees become eligible after accepting access.

## Shared files and Unity scenes

- Agree one active editor for each scene, prefab, binary art source, `Packages/manifest.json`, and project settings file.
- Work in separate sandbox scenes; coordinate integration into the shared stage. Prefabs reduce the need to edit the same scene.
- Move and rename Unity assets inside the Unity editor so references and `.meta` GUIDs stay intact. Never regenerate a teammate's `.meta` file to resolve a conflict.
- If Git reports a scene or prefab conflict, coordinate with the other editor and inspect the result in Unity before merging. Do not guess at serialized scene changes.
- Keep original art and exports in the artist's folder until review. Developers import approved exports into the shared project.
- Designers describe puzzles in `design/levels/`; developers integrate the agreed behavior and artists supply its assets.

## Review and verification

Developers should record editor version, scene, reproduction steps, and observed results. Artists should include previews and export settings. Design changes should explain the intended solution, failure recovery, and affected GDD rules. Record untested behavior clearly.

The PR template and issue templates are ready for use. There is no CI pipeline, Unity runtime validation, or automatic merge gate configured at this stage.
