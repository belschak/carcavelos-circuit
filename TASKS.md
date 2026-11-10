# Tasks (the board)

Status: `todo` -> `claimed (<name>, <date>)` -> `in review` -> `done`. Claim protocol in `AGENTS.md`. Requests for another role: add a row with that role's prefix.
Order matters: **S1 first**, nobody opens Unity on the shared repo before S1 is merged (otherwise everyone generates different `.meta` GUIDs).

## Setup (blocks everything)
| ID | Task | Role | Due | Status |
|---|---|---|---|---|
| S1 | Create the Unity project (6000.3.28f1, Universal 3D template) at the repo root; folders `Assets/_Project/{Driving,Track,Visuals,RaceFlow,Audio,Shared,Tests/EditMode}`; one asmdef per module as in `docs/interfaces.md`; empty scenes `Driving_TestBed`, `Track_Campus`, `Visuals_Showroom`, `Main`; Force Text + "Reduce version control noise" on; commit with all `.meta` files | 4 | Sat 7 | done (David, 2026-11-08) |
| S2 | `IKartState` interface in `Shared/` exactly as in `docs/interfaces.md` | 4 | Sun 8 | done (David, 2026-11-08) |
| S3 | Minimal Web build (one cube, one camera) uploaded to itch.io, opened on another laptop; URL in `docs/builds.md` | 3 | Sun 8 | todo |
| S4 | Every teammate: `docs/setup.md` done, project opens from a fresh clone, name + GitHub handle in `docs/team.md` | all | Sun 8 | todo |

## Role 1: Driving + camera (Alessandro)
| ID | Task | Due | Status |
|---|---|---|---|
| D1 | Placeholder kart prefab (box + 4 cylinders, or Kenney CC0 kart) with a sphere-based `KartController` implementing `IKartState` (approach in `docs/interfaces.md`): accelerate, brake/reverse, steer on `Driving_TestBed` | Mon 9 | done (Alessandro, 2026-11-10) |
| D2 | Chase camera (Cinemachine) that follows smoothly | Mon 9 | done (Alessandro, 2026-11-10) |
| D3 | Drift that charges a boost; release when charged = short boost; early release = nothing | Wed 11 | done (Alessandro, 2026-11-10) |
| D4 | Ramp jump: airborne state, lands upright, no flipping; `IsGrounded` | Wed 11 | done (Alessandro, 2026-11-10) |
| D5 | `ResetTo(position, rotation)` for recovery and retry (zero velocity, clear drift/boost) | Wed 11 | todo |
| D4 | Ramp jump: airborne state, lands upright, no flipping; `IsGrounded` | Wed 11 | in review (Alessandro, 2026-11-09) |
| D5 | `ResetTo(position, rotation)` for recovery and retry (zero velocity, clear drift/boost) | Wed 11 | done (Alessandro, 2026-11-10) |
| D6 | All tuning values in `KartTuning` ScriptableObject; tune on the grey campus track | Fri 13 | todo |

## Role 2: Campus track + race rules (Noah)
| ID | Task | Due | Status |
|---|---|---|---|
| T1 | Final route in `docs/route.md` from the campus walk (sketch, sections, lengths, ramp size) | Sat 7 | todo |
| T2 | Grey-box loop in `Track_Campus` at real scale: road, walls, main buildings as blocks, elevation | Mon 9 | todo |
| T3 | Ramp D/B wing, tunnel, beach promenade in grey | Wed 11 | todo |
| T4 | `Checkpoint`, `SpawnPoint`, respawn points, `TrackInfo` | Tue 10 | in review (Noah, PR #48) |
| T5 | `RaceRules` (plain C#) + Edit Mode tests: order, wrong way, duplicate finish, missed checkpoint | Wed 11 | in review (Noah, PR #47) |
| T6 | Lap time 35-60 s check with the real kart; adjust route | Fri 13 | todo |

## Role 3: Kart, visuals + web release (Luc)
| ID | Task | Due | Status |
|---|---|---|---|
| V1 | `docs/art-style.md`: palette (5-6 colours), shape language, 1 reference image | Sun 8 | todo |
| V2 | Scale test: 1 m cube from Blender as FBX imports at 1 m, forward = +Z; record export settings in `docs/art-style.md` | Sun 8 | todo |
| V3 | Kart + simple seated driver FBX, fits `Kart/Visual` (agree pivot/size with Role 1) | Wed 11 | todo |
| V4 | 2-3 low-poly campus landmarks from the photos (e.g. library facade, EDP Plaza, D/B ramp) | Fri 13 | done (#33): whole campus world `Track_CampusWorld` on Noah's route, built by `CampusWorldBuilder` |
| V3 | Kart + simple seated driver FBX, fits `Kart/Visual` (agree pivot/size with Role 1) | Wed 11 | in review: `KartVisual_Classic_Rookie` prefab (quick version: hands don't follow the wheel yet, needs Animation Rigging) |
| V4 | 2-3 low-poly campus landmarks from the photos (e.g. library facade, EDP Plaza, D/B ramp) | Fri 13 | todo |
| V5 | `KartEffects`: drift sparks + boost flame, reads `IKartState` only | Thu 12 | todo |
| V5 | `KartEffects`: drift sparks + boost flame, reads `IKartState` only | Thu 12 | claimed (Luc, 2026-11-09) |
| V6 | Complete race at the itch.io URL, logged in `docs/builds.md` | Thu 12 | todo |
| V7 | README "How to build" reproducible by a second person | Fri 13 | todo |
| V8 | `Visuals/CampusWorld/CampusWorld.prefab`: the whole campus world as one prefab (colliders, trees and props, sun and sky light) for Role 2's `Track_Campus`; `Track_CampusWorld` stays Role 3's preview with the race placeholders | Tue 10 | claimed (Luc, 2026-11-09) |

## Role 4: Race flow, UI + sound (David)
| ID | Task | Due | Status |
|---|---|---|---|
| R1 | `Main.unity` loads `Track_Campus` additively and spawns the kart at `SpawnPoint` | Tue 10 | done (David, 2026-11-08) |
| R2 | `RaceManager`: Ready -> Countdown -> Racing -> Results, input off outside Racing | Wed 11 | done (David, 2026-11-08) |
| R3 | HUD: countdown, timer, final time, boost cue, retry button, "Press any key to start" screen | Wed 11 | done (David, 2026-11-08) |
| R4 | Retry (full reset) + recovery key R (respawn at last valid checkpoint, timer keeps running) | Thu 12 | done (David, 2026-11-08) |
| R5 | Audio (ElevenLabs or CC0): engine loop (pitch by speed), drift, boost, countdown beeps, finish jingle, music. WAV sources, loops tested in the Web build. Free ElevenLabs plan = credit "Sound effects: elevenlabs.io" in game + itch page. Every file listed in `THIRD_PARTY.md` | Fri 13 | done (David, 2026-11-08) |
| R6 | Polish from the 2026-11-10 audit: P pause, menu sounds, wall bump / take-off / chained boost sounds, seaside ambience, ghost chip, ahead/behind chime, wrong-way and pause cues, loader keys, KartEffects on the kart when Luc's prefab exists (his request Q10, #46), MusicPlayer deleted, walkthrough complete | Fri 13 | in review (David, 2026-11-10, stacked on #51) |

## Course submission (official, `docs/course-requirements.md`)
| ID | Task | Who | Due | Status |
|---|---|---|---|---|
| G1 | Teaching-team OK for a from-scratch Unity game: email to Prof. Ricardo Pereira + Cláudio Vieira from a Nova student account, team in CC; save the reply as a screenshot in the team Drive | Noah | Tue 10 | claimed (Noah, 2026-11-07) |
| G2 | README Team table: your name + student number + role | each member | Fri 13 | todo |
| G3 | At least one merged PR with your own C# code, committed from your own GitHub account | each member | Fri 13 | todo |
| G4 | Final check (no secrets, no personal data, no raw media), then David switches the repo to public and adds a `main` ruleset | David | Sun 15 | todo |
| G5 | Deployed URL in README, deployed build = a `main` commit in `docs/builds.md`, one member submits the repo link on Moodle | Role 3 + Role 4 | **Mon 23, 23:59** | todo |

## Requests between roles
| ID | From -> To | Request | Status |
|---|---|---|---|
| Q5 | 4 -> 2 | `TrackInfo`, `Checkpoint`, `RaceRules` exactly as in `docs/interfaces.md` (T4/T5). RaceFlow already runs against look-alike placeholders in `RaceFlow/Placeholders/`; when yours are on `main`, Role 4 swaps them in (steps: `docs/explain/role4-walkthrough.md`). Put `TrackInfo` on the root of `Track_Campus`, `SpawnPoint` + one `RespawnPoint` per checkpoint. Meaning used by RaceFlow: checkpoint 0 = start/finish line, `PassCheckpoint(i)` for 1..N-1 in order, `TryFinish()` when crossing 0 again (same as `PlaceholderLapRules`). Your own test camera in `Track_Campus` is fine, RaceFlow switches it off in the race | done 2026-11-10 (#50): RaceFlow reads only `TrackInfo`/`Checkpoint`/`RaceRules`; `PlaceholderTrack`/`PlaceholderCheckpoint` stay one week as empty subclasses so already-saved scenes race; deleted after the builders swap (Luc: help PR #49, `NovaCampusBuilder` lines ~749/770 still to do) |
| Q6 | 4 -> 1 | `Kart` prefab whose root has `KartController : IKartState` (D1). Name the prefab `Kart` and keep it under `Assets/_Project/Driving/`: the Role 4 scene builder then picks it up automatically for Main.unity (no Inspector change needed). Until then `PlaceholderKart` drives. `InputEnabled = false` must stop all driving input, `ResetTo` must zero velocity and clear drift/boost | open |
| Q7 | 3 -> 2 | Route decision: Luc's sketch route in `Visuals/Scenes/Track_CampusWorld.unity` (1,184 m, atrium drive-through, terrace jump) vs. help PR #31 (968 m), or a mix. When T4 is on `main`, swap the race placeholders in `CampusWorldBuilder.AddRaceParts` to `TrackInfo`/`Checkpoint` (two lines, marked) | done 2026-11-09: the team takes the campus world with Noah's route (Q9, PR #35); built into `Track_CampusWorld` |
| Q8 | 3 -> 4 | If the team picks the campus world: add `Track_CampusWorld` to the build list and `RaceSettings` (checkpoints and spawn are already there for RaceFlow's placeholders) | done 2026-11-10 (#50): `Track_CampusWorld` + `Track_NovaCampus` in the committed build list, `RaceSettings` already tried them since #36 |
| Q13 | 1 -> 4 | Chase camera (D2) is ready as the prefab `Driving/ChaseCamera.prefab` (Cinemachine: Follow + Rotation Composer, offset 0/3/-6). To use it in `Main.unity` instead of `PlaceholderChaseCamera`: put a `CinemachineBrain` on the Main Camera, add the prefab, and when the kart spawns set the prefab's `CinemachineCamera.Follow` (Tracking Target) to the kart. After `ResetTo` (retry / recovery) call `CinemachineCamera.OnTargetObjectWarped(kartTransform, newPosition - oldPosition)` so the camera jumps with the kart instead of flying across the track. Your decision when to swap (Role 4 owns `Main.unity`); I can pair on it | in review 2026-11-10 (Role 4 PR, stacked on #50): `ChaseCameraRig` puts a CinemachineBrain on the Main Camera, sets Follow to the spawned kart and snaps the camera after retry/recovery; `PlaceholderChaseCamera` removed |
| Q5 | 4 -> 2 | `TrackInfo`, `Checkpoint`, `RaceRules` exactly as in `docs/interfaces.md` (T4/T5). RaceFlow already runs against look-alike placeholders in `RaceFlow/Placeholders/`; when yours are on `main`, Role 4 swaps them in (steps: `docs/explain/role4-walkthrough.md`). Put `TrackInfo` on the root of `Track_Campus`, `SpawnPoint` + one `RespawnPoint` per checkpoint. Meaning used by RaceFlow: checkpoint 0 = start/finish line, `PassCheckpoint(i)` for 1..N-1 in order, `TryFinish()` when crossing 0 again (same as `PlaceholderLapRules`). Your own test camera in `Track_Campus` is fine, RaceFlow switches it off in the race | open |
| Q6 | 4 -> 1 | `Kart` prefab whose root has `KartController : IKartState` (D1). Name the prefab `Kart` and keep it under `Assets/_Project/Driving/`: the Role 4 scene builder then picks it up automatically for Main.unity (no Inspector change needed). Until then `PlaceholderKart` drives. `InputEnabled = false` must stop all driving input, `ResetTo` must zero velocity and clear drift/boost | half done: the `Kart` prefab is on main (#39), the swap in `Main.unity` is still open, see Q14 |
| Q7 | 3 -> 2 | Route decision: Luc's sketch route in `Visuals/Scenes/Track_CampusWorld.unity` (1,184 m, atrium drive-through, terrace jump) vs. help PR #31 (968 m), or a mix. When T4 is on `main`, swap the race placeholders in `CampusWorldBuilder.AddRaceParts` to `TrackInfo`/`Checkpoint` (two lines, marked) | done 2026-11-09: the team takes the campus world with Noah's route (Q9, PR #35); built into `Track_CampusWorld` |
| Q8 | 3 -> 4 | If the team picks the campus world: add `Track_CampusWorld` to the build list and `RaceSettings` (checkpoints and spawn are already there for RaceFlow's placeholders) | open |
| Q13 | 1 -> 4 | Chase camera (D2) is ready as the prefab `Driving/ChaseCamera.prefab` (Cinemachine: Follow + Rotation Composer, offset 0/3/-6). To use it in `Main.unity` instead of `PlaceholderChaseCamera`: put a `CinemachineBrain` on the Main Camera, add the prefab, and when the kart spawns set the prefab's `CinemachineCamera.Follow` (Tracking Target) to the kart. After `ResetTo` (retry / recovery) call `CinemachineCamera.OnTargetObjectWarped(kartTransform, newPosition - oldPosition)` so the camera jumps with the kart instead of flying across the track. Your decision when to swap (Role 4 owns `Main.unity`); I can pair on it | open |
| Q14 | 1 -> 4 | `Main.unity` still spawns the stand-in: `RaceSystems` > Race Bootstrap > **Kart Prefab** points at `RaceFlow/Prefabs/PlaceholderKart.prefab`, so the race never uses the real kart (Q6). Please run your scene builder with `Assets/_Project/Driving/Kart.prefab` and commit `Main.unity`. I tested it by hand (Kart Prefab slot swapped in memory, scene NOT saved): with the real kart R recovery (plain, mid-drift, right after a boost) and Retry after the finish all behave (D5 done). Findings for you: (1) the stand-in kart keeps spinning after it hits a wall or pole until R, because only `ResetTo` zeroes its angular velocity (its Y rotation is free); the real kart zeroes it every physics step, so the swap fixes it, no need to fix the stand-in. (2) Console on Play in `Main`: "There are 2 audio listeners in the scene" (probably Main Camera plus another one). (3) `RaceBootstrap` skips `Track_CampusWorld` and `Track_NovaCampus` ("not in the build scene list") and races your placeholder loop, and `Track_Campus` has no track it recognises yet; I cannot tune D6 on the real campus track until a real track is wired in. (4) Your D6 note about a boost that keeps pushing after input goes off at the finish is on my list. Kart look: the real kart is a grey box until Luc's `KartVisual` goes under `Kart/Visual` (his Q11, my #53) | open |
| Q11 | 3 -> 1 | When the `Kart` prefab exists: put `Visuals/Kart/Prefabs/KartVisual_Classic_Rookie.prefab` under `Kart/Visual` (pivot at ground centre, forward +Z, 1 m). It reads only `IKartState`; a `Steer` value in `IKartState` would make the steering wheel exact (now estimated from the turn rate) | open |
| Q12 | 3 -> 4 | Same as Q3 (PR #9): OK to add the Animation Rigging package (`com.unity.animation.rigging`)? Only for the drivers' hands to follow the steering wheel (IK) | open |
| Q10 | 3 -> 4 | Kart effects (V5): add `Visuals/KartEffects/Prefabs/KartEffects.prefab` to `RaceBootstrap` > Kart Attachments, next to the KartAudio prefab (in `Role4SceneBuilder`). It finds `IKartState` on the kart by itself and needs nothing else; the Bloom volume (`RaceCameraVolume`) makes the sparks glow | open |
