# Tasks (the board)

Status: `todo` -> `claimed (<name>, <date>)` -> `in review` -> `done`. Claim protocol in `AGENTS.md`. Requests for another role: add a row with that role's prefix.
Order matters: **S1 first**, nobody opens Unity on the shared repo before S1 is merged (otherwise everyone generates different `.meta` GUIDs).

## Setup (blocks everything)
| ID | Task | Role | Due | Status |
|---|---|---|---|---|
| S1 | Create the Unity project (6000.3.28f1, Universal 3D template) at the repo root; folders `Assets/_Project/{Driving,Track,Visuals,RaceFlow,Audio,Shared,Tests/EditMode}`; one asmdef per module as in `docs/interfaces.md`; empty scenes `Driving_TestBed`, `Track_Campus`, `Visuals_Showroom`, `Main`; Force Text + "Reduce version control noise" on; commit with all `.meta` files | 4 | Sat 7 | todo |
| S2 | `IKartState` interface in `Shared/` exactly as in `docs/interfaces.md` | 4 | Sun 8 | todo |
| S3 | Minimal Web build (one cube, one camera) uploaded to itch.io, opened on another laptop; URL in `docs/builds.md` | 3 | Sun 8 | todo |
| S4 | Every teammate: `docs/setup.md` done, project opens from a fresh clone, name + GitHub handle in `docs/team.md` | all | Sun 8 | todo |

## Role 1: Driving + camera (Alessandro)
| ID | Task | Due | Status |
|---|---|---|---|
| D1 | Placeholder kart prefab (box + 4 cylinders, or Kenney CC0 kart) with a sphere-based `KartController` implementing `IKartState` (approach in `docs/interfaces.md`): accelerate, brake/reverse, steer on `Driving_TestBed` | Mon 9 | todo |
| D2 | Chase camera (Cinemachine) that follows smoothly | Mon 9 | todo |
| D3 | Drift that charges a boost; release when charged = short boost; early release = nothing | Wed 11 | todo |
| D4 | Ramp jump: airborne state, lands upright, no flipping; `IsGrounded` | Wed 11 | todo |
| D5 | `ResetTo(position, rotation)` for recovery and retry (zero velocity, clear drift/boost) | Wed 11 | todo |
| D6 | All tuning values in `KartTuning` ScriptableObject; tune on the grey campus track | Fri 13 | todo |

## Role 2: Campus track + race rules (Noah)
| ID | Task | Due | Status |
|---|---|---|---|
| T1 | Final route in `docs/route.md` from the campus walk (sketch, sections, lengths, ramp size) | Sat 7 | todo |
| T2 | Grey-box loop in `Track_Campus` at real scale: road, walls, main buildings as blocks, elevation | Mon 9 | todo |
| T3 | Ramp D/B wing, tunnel, beach promenade in grey | Wed 11 | todo |
| T4 | `Checkpoint`, `SpawnPoint`, respawn points, `TrackInfo` | Tue 10 | todo |
| T5 | `RaceRules` (plain C#) + Edit Mode tests: order, wrong way, duplicate finish, missed checkpoint | Wed 11 | todo |
| T6 | Lap time 35-60 s check with the real kart; adjust route | Fri 13 | todo |

## Role 3: Kart, visuals + web release (Luc)
| ID | Task | Due | Status |
|---|---|---|---|
| V1 | `docs/art-style.md`: palette (5-6 colours), shape language, 1 reference image | Sun 8 | todo |
| V2 | Scale test: 1 m cube from Blender as FBX imports at 1 m, forward = +Z; record export settings in `docs/art-style.md` | Sun 8 | todo |
| V3 | Kart + simple seated driver FBX, fits `Kart/Visual` (agree pivot/size with Role 1) | Wed 11 | todo |
| V4 | 2-3 low-poly campus landmarks from the photos (e.g. library facade, EDP Plaza, D/B ramp) | Fri 13 | todo |
| V5 | `KartEffects`: drift sparks + boost flame, reads `IKartState` only | Thu 12 | todo |
| V6 | Complete race at the itch.io URL, logged in `docs/builds.md` | Thu 12 | todo |
| V7 | README "How to build" reproducible by a second person | Fri 13 | todo |

## Role 4: Race flow, UI + sound (David)
| ID | Task | Due | Status |
|---|---|---|---|
| R1 | `Main.unity` loads `Track_Campus` additively and spawns the kart at `SpawnPoint` | Tue 10 | todo |
| R2 | `RaceManager`: Ready -> Countdown -> Racing -> Results, input off outside Racing | Wed 11 | todo |
| R3 | HUD: countdown, timer, final time, boost cue, retry button, "Press any key to start" screen | Wed 11 | todo |
| R4 | Retry (full reset) + recovery key R (respawn at last valid checkpoint, timer keeps running) | Thu 12 | todo |
| R5 | Audio (ElevenLabs or CC0): engine loop (pitch by speed), drift, boost, countdown beeps, finish jingle, music. WAV sources, loops tested in the Web build. Free ElevenLabs plan = credit "Sound effects: elevenlabs.io" in game + itch page. Every file listed in `THIRD_PARTY.md` | Fri 13 | todo |

## Course submission (official, `docs/course-requirements.md`)
| ID | Task | Who | Due | Status |
|---|---|---|---|---|
| G1 | Written OK from the teaching team for a from-scratch Unity game: email from a Nova student account, whole team in CC, mention Luc's verbal TA OK; save the reply as a screenshot in the team Drive | Noah | Tue 10 | claimed (Noah, 2026-11-07) |
| G2 | README Team table: your name + student number + role | each member | Fri 13 | todo |
| G3 | At least one merged PR with your own C# code, committed from your own GitHub account | each member | Fri 13 | todo |
| G4 | Final check (no secrets, no personal data, no raw media), then David switches the repo to public and adds a `main` ruleset | David | Sun 15 | todo |
| G5 | Deployed URL in README, deployed build = a `main` commit in `docs/builds.md`, one member submits the repo link on Moodle | Role 3 + Role 4 | **Mon 23, 23:59** | todo |

## Requests between roles
| ID | From -> To | Request | Status |
|---|---|---|---|
