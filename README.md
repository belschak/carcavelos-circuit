# Carcavelos Circuit

Single-player 3D kart time trial around a stylised Nova SBE Carcavelos campus: through the beach tunnel, along the promenade, over the ramp between the D and B wings. One kart, one track, one timed lap, drift to charge a boost, retry. Student team project for Intro to Programming (Nova SBE, Fall 2026).

Unity **6000.3.28f1** (6.3 LTS) · C# · Blender (FBX) · Web build on itch.io (URL: TBD, see `docs/builds.md`)

## Quick start
1. Install [Unity Hub](https://unity.com/download) (official site only), then Editor `6000.3.28f1` with Web Build Support, plus Git, and accept the repo invite: [docs/setup.md](docs/setup.md).
2. `gh repo clone belschak/carcavelos-circuit`, then once per clone: `git config core.hooksPath .githooks`.
3. Open [TASKS.md](TASKS.md), pick a `todo` task of your role, follow the claim steps in [AGENTS.md](AGENTS.md).

**Do not open the project in Unity before task S1 is merged** (otherwise everyone creates different `.meta` files).

## Working with an AI assistant
Point Claude Code, Codex, Cursor or Copilot at this folder. They read [AGENTS.md](AGENTS.md) (Claude via `CLAUDE.md`): ownership, contracts, what never to touch, definition of done.

## Where things are
| File | What |
|---|---|
| [TASKS.md](TASKS.md) | The board: every task, owner role, due date, status |
| [docs/team.md](docs/team.md) | Roles, owners, folders, scenes |
| [docs/interfaces.md](docs/interfaces.md) | Contracts between modules (`IKartState`, `TrackInfo`, `RaceRules`) |
| [docs/plan.md](docs/plan.md) | Milestones + definition of done per role |
| [docs/decisions.md](docs/decisions.md) | Why things are the way they are |
| [docs/route.md](docs/route.md) | The campus route |
| [docs/verification.md](docs/verification.md) | What we test before each merge and release |
| [docs/builds.md](docs/builds.md) | Every Web build: URL, commit, result |
| [THIRD_PARTY.md](THIRD_PARTY.md) | Every external asset and its licence |

## Deadlines
| Date | |
|---|---|
| **Fri 13 Nov** | Every role's part merged into `main` |
| Tue 17 Nov | Feature freeze |
| Fri 20 Nov | Release candidate at the itch.io URL |
| 23 Nov | Presentation (confirm slot with the TA) |

## How to build
Web build steps: [docs/setup.md#web-build-role-4-or-the-build-backup](docs/setup.md#web-build-role-4-or-the-build-backup). Role 4 makes this reproducible by a second person (task R7).

## Credits
Map data © OpenStreetMap contributors (ODbL). Third-party assets: [THIRD_PARTY.md](THIRD_PARTY.md). Original game, not affiliated with Nintendo or Nova SBE.
