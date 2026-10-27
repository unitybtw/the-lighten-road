# Project checks

[Project checks on GitHub](https://github.com/unitybtw/the-lighten-road/actions/workflows/project-checks.yml) runs every Monday at **03:23 UTC (06:23 in Istanbul)**, for pull requests changing the game or CI tooling, and through **Actions → Project checks → Run workflow**. Scheduled starts can be delayed by GitHub. It does not run on every push to `main`.

The workflow reports failures in Actions job logs and the run summary, with downloadable reports retained for 14 days. It does not create commits, change gameplay code, add tests automatically, or open issues. New regression tests committed to the existing test assemblies are picked up by future runs. A new gameplay failure should get a focused regression test when it is fixed; passing tests cannot establish that every bug has been found.

## Checks available now

The license-free job validates tracked Unity files for missing asset metadata, duplicate GUIDs, paths differing only by case, merge conflict markers, invalid project JSON, duplicate assembly names, and enabled build scenes with missing assets or mismatched GUIDs. VS Code JSON-with-comments settings are exempt from strict JSON parsing. Unity package references and arbitrary serialized object references are not resolved by this script.

It also runs the integrity checker's regression tests and the existing contribution-counting tests. These checks do **not** compile C# or simulate gameplay.

Run them locally from the repository root:

```sh
python scripts/ci/check_project.py
python -m unittest discover -s scripts/ci -p 'test_*.py'
python -m pip install -r scripts/team_activity/requirements.txt
python -m unittest discover -s scripts/team_activity -p 'test_*.py'
```

## Unity Play Mode in GitHub Actions

The workflow is configured to use the editor version in `Game/ProjectSettings/ProjectVersion.txt`, currently **6000.3.2f1**, and run Play Mode tests in the Editor through [GameCI](https://game.ci/docs/github/test-runner/). This covers the project's current gameplay tests, including the authored test-chamber scene and prefab integration tests that would be skipped in a standalone test player. GameCI imports the project and compiles its scripts before running tests. Coverage instrumentation is disabled for this initial setup.

**Activation is not configured yet.** Until the following secrets are present, the Unity job is skipped and the run summary explicitly says **Unity Play Mode: NOT RUN**. A green integrity job must not be described as passing gameplay tests.

In repository **Settings → Secrets and variables → Actions**, add the secrets required by [GameCI's activation guide](https://game.ci/docs/github/activation/):

| License | Required repository secrets |
| --- | --- |
| Personal | `UNITY_LICENSE` (activated `.ulf` contents), `UNITY_EMAIL`, `UNITY_PASSWORD` |
| Pro | `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` |

Enter these through GitHub's secret settings, not in source files, issues, commits, or chat messages. Use the guide for the license type attached to your Unity account. This workflow does not transfer a local machine's license or install a persistent runner on a teammate's computer.

After adding the secrets, manually run **Project checks** and confirm **Unity Play Mode** actually executes. A hosted Linux result is not verified until that job finishes successfully and produces test XML. GitHub's standard Actions notifications apply to failed runs according to each user's notification settings. Pull requests from forks run the license-free checks only; Unity credentials are not supplied to fork code.

## Latest local gameplay baseline

On **2026-10-27**, Unity **6000.3.2f1** ran **235 Play Mode tests: 235 passed, 0 failed, 0 skipped**, across all eight project test assemblies on macOS. Coverage includes real tutorial/archive route traversal, player movement and respawn, lantern/fuel/HUD, frontend settings and pause, chamber completion, glyphs, relays and controller feedback. After the final controller-ownership refinement, the eight focused lifecycle regression cases were rerun: **8 passed, 0 failed, 0 skipped**. Test options and chapter progress were restored afterward.

The license-free integrity checker and its **5 tests**, plus **4 team-activity tests**, also passed. Details and limitations are in [the stability pass notes](../development/sirac/PROJECT-AUDIT-2026-10-27.md). These are local Editor/software results; physical controller feel, standalone builds and GitHub-hosted Unity runs are not established by them.

The README team activity generator remains a separate, manually triggered workflow. Its bot commits are excluded from the contribution graphic.
