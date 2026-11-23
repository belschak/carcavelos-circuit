# Carcavelos Circuit

[![Tests](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml?query=branch%3Amain) [![Security](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml?query=branch%3Amain)

A 3D kart racer on a model of Nova SBE's Carcavelos campus that runs in the browser. Race five bots through the beach tunnel, along the promenade and over the ramp between the campus wings; chase your own ghost in Time Attack; or race the whole class live in one online room. Team project for Introduction to Programming (2477), Nova SBE, Fall 2026.

**Goal:** a game anyone in our class can open in a browser and play right away, on a track that looks like our own campus, with the lap rules, leaderboard and online race checked by automated tests.

**Play it: https://nova-kart.github.io/** (Chrome, Edge or Firefox on a laptop; phones work with touch buttons)

Also live at https://belschak.github.io/carcavelos-circuit/: the same build of `main`, published to both addresses by the same workflow ([why](docs/architecture.md#build-and-deploy)).

Built with Unity 6000.3.28f1 (C#) instead of the course's Python + Streamlit, a stack approved by the teaching staff (18.11.2026) ([why](docs/architecture.md#stack-approval)); compiled to WebAssembly, deployed from `main` to GitHub Pages by GitHub Actions. The code lives in this repository only. Online services: Supabase (the global leaderboard) and Photon Fusion 2 (the online race); if either is down, the rest of the game keeps working and says what failed.

## Team

| Name | Student number | Role |
|---|---|---|
| David Belschak | 78842 | 4 Race flow, UI + sound |
| Luc Tippmann | 77964 | 3 Kart, visuals + web release |
| Alessandro Rossi | 78527 | 1 Driving + camera |
| Noah Schlag | 78142 | 2 Campus track + race rules |

Who built what: [docs/walkthrough-qa.md](docs/walkthrough-qa.md#the-team).

## How to play

Pick a mode on the start screen (click it, or press 1, 2 or 3), build your kart (driver, kart, tyres), press READY and drive one lap. A lap only counts if you pass every checkpoint in order.

| Key | Action |
|---|---|
| A/D, W/S or arrows (kart builder) | Column, then the tile inside it; Enter or Space = READY, Esc = back |
| W A S D or arrows | Drive and steer |
| Space | Drift (hold while steering to charge a boost, release to fire it) |
| Shift | Trick after a jump |
| E | Use the item you picked up |
| R | Recover to the last checkpoint (the timer keeps running) |
| Enter | Retry |
| P or Esc | Pause: the PAUSED card has RESUME, RETRY and BACK TO MENU buttons (P or Esc again = resume). Online the race cannot pause for everyone: P opens LEAVE THE RACE? (CONTINUE / LEAVE RACE) and the race keeps running |
| O / C / M | Settings / credits / sound on or off |
| T | Noon or sunset light |
| Q | Back to the menu (mid-race the first Q pauses, Q again leaves; online mid-race the first Q asks, Q again leaves the race as DNF; in the lobby and on the results: leave the room) |

On a phone the game shows touch buttons instead. The pause button (top left, next to the lap time) opens the same card with tappable buttons, online too.

## Features, and where each lives

| Feature | What it does | Code |
|---|---|---|
| Kart physics | Driving, drift that charges a two-stage boost, ramp jumps, tricks, recover | `Assets/_Project/Driving/KartController.cs` |
| Campus track | Route from OpenStreetMap; campus, beach, promenade and Nova hall built from map data and photo surveys | `Assets/_Project/Visuals/CampusWorld/`, `tools/world_gen/`, [docs/route.md](docs/route.md) |
| Lap rules | Ordered checkpoints; skipped, repeated or backwards crossings are refused | `Assets/_Project/Track/RaceRules.cs` |
| Race flow | Ready, countdown, race, results; timer, retry, recover, pause | `Assets/_Project/RaceFlow/RaceStateMachine.cs`, `RaceManager.cs` |
| Three modes | Race vs Bots, Time Attack, Online Race | `Assets/_Project/RaceFlow/RaceModeRules.cs` |
| Five AI bots | Rule-based drivers on the same kart physics as the player | `Assets/_Project/Visuals/Bots/` |
| Items | Four campus items from boxes on the road; better items when you are behind | `Assets/_Project/RaceFlow/ItemSystem.cs`, `ItemOdds.cs`, `Visuals/ItemFx/` |
| Positions, minimap, name tags | Live place of every racer | `Assets/_Project/RaceFlow/RaceStandings.cs`, `MinimapPath.cs`, `Driving/NameTagManager.cs` |
| Ghost and medals | Replay of your best lap, Bronze/Silver/Gold scaled to the track | `Assets/_Project/RaceFlow/Ghost*.cs`, `MedalRules.cs` |
| Kart builder | Driver, kart and tyres in three columns (64 looks-only sets) on a turntable; the four drivers on the first screen | `Assets/_Project/RaceFlow/UI/KartBuilder.cs`, `Assets/_Project/Visuals/Kart/KartAssembler.cs` |
| Leaderboard | Global top 10 with names, shared by every player (Supabase). Every time is also saved on the device first, so when the internet or the service is down the board shows the device's times and says so | `Assets/_Project/RaceFlow/Leaderboard/`, `backend/supabase/` |
| Online race | One room for the class (15 players), shared countdown, same finish order on every screen | `Assets/_Project/Online/`, [docs/online.md](docs/online.md) |
| Menus and HUD | Start screen, settings, credits, results, plain-language errors | `Assets/_Project/RaceFlow/UI/` |
| Sound | Engine pitch from speed, drift and boost sounds, music with ducking | `Assets/_Project/Audio/`, `RaceFlow/RaceMusic.cs` |
| Beach life | Waves rolling onto the beach, surfers riding them | `Assets/_Project/Ambience/` |
| Teacher tribute | A thank-you to the course's teaching staff: every driver carries a teacher's name (Ricardo and Cláudio, and Luísa and Paula, the female forms of Luís and Paulo, for Luc's two female characters), and Luís and Paulo themselves drive the two repainted Classics, SPECIAL THANKS and THANK YOU, TEACHERS pages in the credits, four thank-you boards beside checkpoint gates on the track, and "YOU WON THE INTRODUCTION TO RACING CUP" on a Gold or Dean's List lap | `Assets/_Project/RaceFlow/Tribute/`, art from `tools/textures/make_tribute_boards.py` |
| Web page | Loader, error cards, touch controls, update check | `Assets/WebGLTemplates/CarcavelosCircuit/index.html` |

How these parts fit together, and why they were built this way: [docs/architecture.md](docs/architecture.md).

## Run the app locally

You need **Unity Hub** (https://unity.com/download) and **Unity Editor 6000.3.28f1** with the module **Web Build Support**. Use exactly this version. Step-by-step install: [docs/setup.md](docs/setup.md).

```
git clone https://github.com/belschak/carcavelos-circuit.git
```

1. Unity Hub > Projects > Add > Add project from disk > the `carcavelos-circuit` folder. The first import takes several minutes.
2. Open the scene `Assets/_Project/RaceFlow/Scenes/Main.unity` and press Play.

Race vs Bots and Time Attack work on a fresh clone; the leaderboard keeps times on your device there. The Online Race needs the Photon SDK, which its licence keeps out of this repo; without it the mode says "ONLINE RACE NEEDS THE ONLINE BUILD" ([docs/online.md](docs/online.md#for-developers-the-online-race-in-your-local-editor)). The global leaderboard needs two keys that exist only as GitHub secrets for the deployed build ([backend/supabase/README.md](backend/supabase/README.md)).

**The Web version on your own machine** (Unity closed). Windows:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4WebBuild.Build -quit -logFile Logs/web-build.log
```

```
python tools/serve_web_build.py
```

Then open http://localhost:8000. macOS: the Unity path is `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity`, same arguments. Every Web setting comes from `Role4WebBuild.cs`, so the local build matches the deployed one.

## Run the tests

**On GitHub (the reference):** `.github/workflows/tests.yml` runs every Edit Mode and Play Mode test in Unity on Linux on every push to `main` and on every pull request into `main` that changes code; docs-only pull requests skip it (badge at the top). Actions > **Tests** > Run workflow starts it by hand.

**On your computer** (needs the **Web Build Support** module, see above): in the Unity Editor, Window > General > Test Runner > **EditMode** > Run All, then **PlayMode** > Run All.

From the command line (Unity closed), inside the cloned folder (`cd carcavelos-circuit`), Windows:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults/editmode.xml
```

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform PlayMode -testResults TestResults/playmode.xml
```

macOS: the Unity path above, same arguments. Exit code 0 means every test passed; the results are NUnit XML files in `TestResults/`.

What the tests cover for each workflow (accepted, rejected and edge cases) and what they do not: [docs/verification.md](docs/verification.md).

## Repository map

| Path | What is in it |
|---|---|
| `Assets/_Project/` | All our game code, one folder per module (`Driving`, `Track`, `Visuals`, `RaceFlow`, `Audio`, `Ambience`, `Online`, `Shared`) and `Tests/` (`EditMode`, `PlayMode`) |
| `Assets/WebGLTemplates/` | The web page around the game |
| `backend/supabase/` | Leaderboard database: table, access rules, setup steps |
| `tools/` | Python scripts that generate the campus world, props, textures, sounds and UI art (not needed to run the game) |
| `.github/workflows/` | CI: tests, security checks (secret scan, pinning), the Web build + deploy, the leaderboard keep-alive |
| `docs/` | How it is built, why, limits, decisions: start at [docs/README.md](docs/README.md) |
| `AGENTS.md` | The rules for every teammate and every AI assistant (Git workflow, ownership, contracts) |
| `TASKS.md` | The task board: every task, owner and status |
| `THIRD_PARTY.md` | Every external asset and its licence |
| `SECURITY.md`, `LICENSE` | How to report a security problem; the MIT licence for our code |
| `Packages/`, `ProjectSettings/` | Unity package list (pinned) and project settings |

## Documentation

| Read | For |
|---|---|
| [docs/architecture.md](docs/architecture.md) | The parts, the data flow, where data is stored, why each choice |
| [docs/walkthrough-qa.md](docs/walkthrough-qa.md) | Short answers to the walkthrough questions |
| [docs/limits-and-next-steps.md](docs/limits-and-next-steps.md) | What it cannot do and how it could change |
| [docs/verification.md](docs/verification.md) | What the tests cover and what they do not |
| [docs/security.md](docs/security.md) | Keys, access control, input validation, dependencies |
| [docs/README.md](docs/README.md) | Every other document |

## Credits

Map data © OpenStreetMap contributors (ODbL). Sound effects: elevenlabs.io (kart engine and drift sounds made by Luc Tippmann, boost by David Belschak); all other sounds and the music are our own synthesised work. Characters: Microsoft Rocketbox Avatar Library (MIT, © 2020 Microsoft). All third-party assets and their licences: [THIRD_PARTY.md](THIRD_PARTY.md). Original game, not affiliated with Nintendo or Nova SBE.

## License

Our code (C#, Python, SQL, HTML, CSS, JavaScript) is under the MIT licence: [LICENSE](LICENSE). Our own synthesised sounds and code-drawn art are CC0, as listed in [THIRD_PARTY.md](THIRD_PARTY.md). Every third-party asset (map data, fonts, characters, generated sounds and models) keeps its own licence, listed there. The Photon SDK is not part of this repository.
