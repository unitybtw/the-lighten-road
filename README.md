<div align="center">

# The Lighten Road

**Every flare reveals a path. Every second costs an ember.**

An atmospheric 2D puzzle-platformer about light, isolation, and the cost of survival.

**Pre-production** · **Unity 2D / C#** · **Pixel art**

[Game Design Document](docs/GDD.md) · [Latest GDD (PDF)](docs/The-Lighten-Road-GDD.pdf) · [Contributing](CONTRIBUTING.md)

</div>

---

## The game

Four hundred years after an apocalypse, Light, a condemned Ruin Researcher, enters a sealed pre-Fall facility to recover ancient technology in exchange for his freedom. An anomalous ember lantern is his only light. Deep inside the ruins, survival leads him toward an archive holding the truth behind the Fall.

The Lighten Road pairs deliberate platforming with environmental puzzles. Light reveals the route, but the fuel that makes exploration possible is also the resource keeping you alive.

## Core design

| System | The decision |
| --- | --- |
| **Dim / Flare** | Preserve ember fuel with a passive glow, or spend it for a wider view of paths, hazards, and hidden items. |
| **Power / Drain** | Spend lantern fuel to power machinery, or drain an active device to recover energy into the lantern while shutting it down. |
| **Connected chambers** | Fuel carries forward between rooms. The doorway seals behind you, making earlier choices matter. |
| **Exploration** | Take a direct route to safety or risk a detour for lore and additional embers. |

```mermaid
flowchart LR
    A[Enter a dark chamber] --> B[Spend embers to scout]
    B --> C[Power or drain machinery]
    C --> D[Traverse and recover resources]
    D --> E{Exit or explore?}
    E -->|Detour| B
    E -->|Advance with remaining fuel| A
```

Movement consists of left/right movement and jumping, with no sprint, double-jump, or attack. Fuel depletion creates a **positive feedback loop (death spiral)**: less fuel limits visibility, making traversal harder and increasing survival pressure.

## Planned scope

The full design targets **4–5 puzzle chambers** across an **Abandoned Corporate Archive** and a **Subterranean Covert Lab**, with an estimated **15–20 minute** run. These are design targets, not measured playtest results.

The MVP focuses on **two Office stages** with movement and jumping, the lantern fuel economy, Ember Veins, rolling archive shelves, one lift, and electrified water. If time is limited, the laboratory zone will be cut so the Office stages can become a polished, self-contained experience.

## Current status

The shared Unity project uses **Unity 6000.3.2f1 / Universal 2D**. Siraç's sandbox combines horizontal movement, one grounded jump, camera follow, lantern fuel / Dim-Flare prototype, and an ember pickup. All **59 automated Play Mode tests passed locally on 2026-10-16** (movement, camera, lantern fuel API, controller mode switching, and pickup prefab integration). Ember Veins, Office stages, and a standalone playable build remain planned.

- [x] Latest supplied GDD archived
- [x] Editable GDD and project overview
- [x] Create the Unity 2D project and record the editor version
- [x] Establish streamlined direct-to-`main` development workflow
- [ ] Agree on final input bindings, fuel tuning, and shared system interfaces
- [x] Prototype movement, single jump, and camera follow in Siraç's sandbox
- [x] Add lantern fuel API and Dim / Flare sandbox prototype (with `TheLightenRoad.Lantern.asmdef`)
- [x] Add a reusable lantern fuel pickup to Siraç's sandbox
- [x] 59 automated Play Mode tests passing locally in Unity Test Runner (2026-10-16)
- [ ] Integrate Ember Veins with the shared lantern API
- [ ] Build and playtest the two Office stages
- [ ] Polish art, sound, puzzle readability, and recovery after failure
- [ ] Evaluate the laboratory zone after the MVP is stable

## Getting started

Clone the repository and open the Unity project:

```sh
git clone https://github.com/unitybtw/the-lighten-road.git
cd the-lighten-road
```

Open `Game/` in Unity Hub with editor **6000.3.2f1**, then open `Assets/_Project/Scenes/Sandbox/Sirac/MovementSandbox.unity` and press Play. Use **A/D or Left/Right** to move, **Space** to jump, and the current temporary **F** binding to toggle the lantern between Dim and Flare. The Game view shows the fuel meter. Start with [Game/README.md](Game/README.md) for setup and tests, and [docs/GDD.md](docs/GDD.md) for the design. Final input bindings, fuel balance, target platforms, and build instructions still need team review.

The latest [ember and lantern prototype update](docs/FEEDBACK-2026-10-16.md) adds gradual Dim/Flare transitions, two-use ember fading and cracking, a quieter HUD, and three pickups in Siraç's archive test chamber. Icons, crack audio, and backdrop are prototype assets.

## Automated checks

[Project checks](https://github.com/unitybtw/the-lighten-road/actions/workflows/project-checks.yml) runs weekly, on relevant pull requests, and manually. It checks Unity asset metadata, GUID collisions, project files, build scene links, and tooling regressions. Hosted Unity gameplay tests require license secrets and are explicitly marked **NOT RUN** until configured. The latest local Unity baseline is **59/59 passing Play Mode tests**; see [testing and CI setup](docs/TESTING.md) for scope, results, and activation.

## Working as a team

Each teammate has a dedicated workspace for prototypes, art sources, or puzzle design. Start with the [collaboration guide](docs/COLLABORATION.md) for scene isolation, folder ownership, and direct-to-`main` workflow. Review the [proposed developer task split](docs/DEVELOPMENT_PLAN.md) before implementation; assignments and interfaces there are a draft for the team to review.

## Repository guide

| Path | Contents |
| --- | --- |
| [docs/GDD.md](docs/GDD.md) | Editable design, MVP boundaries, and open decisions |
| [docs/COLLABORATION.md](docs/COLLABORATION.md) | Team workspaces and direct-to-`main` workflow rules |
| [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md) | Draft ownership split for Siraç and Kemal to review |
| [development/](development/) | Separate Siraç and Kemal prototype workspaces |
| [art/](art/) | Separate Can and Maksim art sources and exports |
| [design/](design/) | Furkan’s level designs and balance proposals |
| [Game/](Game/) | Shared Unity 6000.3.2f1 Universal 2D project |
| [docs/The-Lighten-Road-GDD.pdf](docs/The-Lighten-Road-GDD.pdf) | Latest three-page team design document |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branch, review, and Unity asset workflow |

## Team

<!-- TEAM-ACTIVITY:START -->
<a href="https://github.com/unitybtw/the-lighten-road/commits/main/">
  <picture>
    <source media="(prefers-reduced-motion: reduce)" srcset="docs/team-activity/team-activity.png">
    <img src="docs/team-activity/team-activity.gif" alt="Team activity: overlapping circular GitHub portraits with names and commit counts above one color-coded proportional bar. Siraç is blue." width="900">
  </picture>
</a>

<details>
<summary>Commit counts and methodology</summary>

| Team member | Role | Authored commits |
| --- | --- | ---: |
| [Siraç](https://github.com/unitybtw) | Game Developer | 29 |
| [Kemal](https://github.com/kemal-berke) | Game Developer | 1 |
| [Maksim](https://github.com/Ichnolith) | Artist | 1 |
| [Can](https://github.com/benesas) | Artist | 0 |
| [Furkan](https://github.com/aeshmacodex) | Game Designer | 0 |

Counts cover GitHub-linked commit authors on `main`, excluding merge commits and bots. Co-author trailers, issues, reviews, uncommitted assets and work outside Git are not counted. Commit volume is an activity signal, not a measure of effort or work quality.

[View the data](docs/team-activity/activity.json) · [Static image](docs/team-activity/team-activity.png) · [How updates work](docs/team-activity/README.md)

</details>
<!-- TEAM-ACTIVITY:END -->

| Team member | Role | GitHub |
| --- | --- | --- |
| Siraç Göktuğ Şimşek | Game Developer | [@unitybtw](https://github.com/unitybtw) |
| Kemal Berke Aşık | Game Developer | [@kemal-berke](https://github.com/kemal-berke) |
| Can Mutluşahinoğlu | Artist | [@benesas](https://github.com/benesas) |
| Maksim Martynenko | Artist | [@Ichnolith](https://github.com/Ichnolith) |
| Furkan Aydın | Game Designer | [@aeshmacodex](https://github.com/aeshmacodex) |

Team credit describes authorship of the design; GitHub access and commit-based contributor listings are managed separately.

## Rights

No open-source license has been selected. Public visibility does not grant a general license to reuse the project's code, artwork, audio, or design materials. Third-party assets must retain their own license and attribution information.
