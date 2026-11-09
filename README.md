# Carcavelos Circuit

Single-player 3D kart time trial around a stylised Nova SBE Carcavelos campus: through the beach tunnel, along the promenade, over the ramp between the D and B wings. One kart, one track, one timed lap, drift to charge a boost, retry. Student team project for Intro to Programming (Nova SBE, Fall 2026).

Unity **6000.3.28f1** (6.3 LTS) · C# · Blender (FBX) · Web build

**Play it: _deployed URL TBD_** (every build is logged in [docs/builds.md](docs/builds.md))

## Team
| Name | Student number | Role |
|---|---|---|
| David Belschak | _TBD_ | 4 Race flow, UI + sound |
| Luc Tippmann | _TBD_ | 3 Kart, visuals + web release |
| Alessandro Rossi | _TBD_ | 1 Driving + camera |
| Noah _TBD_ | _TBD_ | 2 Campus track + race rules |

## Run the app
- **Deployed:** open the URL above in Chrome, Edge or Firefox on a laptop and click to start. Keys: W/S or arrows to drive, Space to drift, R to recover, Enter to retry after the finish.
- **Locally:** install Unity Hub + Editor `6000.3.28f1` ([docs/setup.md](docs/setup.md)), then
  ```
  git clone https://github.com/belschak/carcavelos-circuit.git
  ```
  Unity Hub > Add project from disk > the cloned folder > open `Assets/_Project/RaceFlow/Scenes/Main.unity` > press Play.

- **Web build locally** (Unity closed, Windows):
  ```
  "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4WebBuild.Build -quit -logFile Logs/web-build.log
  python tools/serve_web_build.py
  ```
  then open `http://localhost:8000`. All Web settings come from that build script ([docs/setup.md](docs/setup.md#web-build-role-3-or-the-build-backup)).

## Run the tests
In the Unity Editor: Window > General > Test Runner > EditMode > Run All. Command line (Unity closed), Windows:
```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults/editmode.xml
```
Play Mode tests (a robot plays the real game in `Main.unity` on the track it loads: clean lap, skipped checkpoint, recover, retry): Test Runner > PlayMode > Run All, or the same command with `-testPlatform PlayMode -testResults TestResults/playmode.xml`.

macOS: `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity` with the same arguments.

## Quick start
1. Install [Unity Hub](https://unity.com/download) (official site only), then Editor `6000.3.28f1` with Web Build Support, plus Git, and accept the repo invite: [docs/setup.md](docs/setup.md).
2. `gh repo clone belschak/carcavelos-circuit`, then once per clone: `git config core.hooksPath .githooks`.
3. Open [TASKS.md](TASKS.md), pick a `todo` task of your role, follow the claim steps in [AGENTS.md](AGENTS.md).

S1 (the Unity project) is merged: pull `main`, then open the folder in Unity `6000.3.28f1` (no other version, otherwise `.meta` and settings files change).

## Working with an AI assistant
Point Claude Code, Codex, Cursor, Copilot or Gemini CLI at this folder. They read [AGENTS.md](AGENTS.md) (Claude via `CLAUDE.md`, Gemini via `GEMINI.md`, Copilot via `.github/copilot-instructions.md`): the Git workflow, ownership, contracts, what never to touch, definition of done. A chat AI that cannot see the folder (ChatGPT or Claude in the browser) does not know these rules: paste `AGENTS.md` into the chat first.

Start every AI session with this message (copy it as it is):
```
Read AGENTS.md and follow its Git workflow section A before anything else: check the hook and my Git identity, pull main, show me in 2-3 lines what the others merged since my last session and how it affects my task, then merge main into my branch. Only work on files my role owns, push after every finished step, never push to main or force-push.
```

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
| [docs/course-requirements.md](docs/course-requirements.md) | Official course rules we are graded against |

## Deadlines
| Date | |
|---|---|
| **Fri 13 Nov** | Every role's part merged into `main` |
| Tue 17 Nov | Feature freeze |
| Fri 20 Nov | Release candidate at the deployed URL |
| **Mon 23 Nov 23:59** | **Moodle submission** (public repo link, see [docs/course-requirements.md](docs/course-requirements.md)) |
| 24 Nov or 4 Dec | Presentation: live demo + code walkthrough |

## How to build
Web build steps: [docs/setup.md#web-build-role-3-or-the-build-backup](docs/setup.md#web-build-role-3-or-the-build-backup). Role 3 makes this reproducible by a second person (task V7).

## Credits
Map data © OpenStreetMap contributors (ODbL). Sound effects: elevenlabs.io (kart engine and drift sounds made by Luc Tippmann, boost by David Belschak), all other sounds and the music are our own synthesised work. Third-party assets: [THIRD_PARTY.md](THIRD_PARTY.md). Original game, not affiliated with Nintendo or Nova SBE.
