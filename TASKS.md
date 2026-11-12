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
| D5 | `ResetTo(position, rotation)` for recovery and retry (zero velocity, clear drift/boost) | Wed 11 | done (Alessandro, PR #56) |
| D6 | All tuning values in `KartTuning` ScriptableObject; tune on the grey campus track | Fri 13 | in review (Alessandro, PR #59) |

## Role 2: Campus track + race rules (Noah)
| ID | Task | Due | Status |
|---|---|---|---|
| T1 | Final route in `docs/route.md` from the campus walk (sketch, sections, lengths, ramp size) | Sat 7 | todo |
| T2 | Grey-box loop in `Track_Campus` at real scale: road, walls, main buildings as blocks, elevation | Mon 9 | todo |
| T3 | Ramp D/B wing, tunnel, beach promenade in grey | Wed 11 | todo |
| T4 | `Checkpoint`, `SpawnPoint`, respawn points, `TrackInfo` | Tue 10 | done (Noah, #48) |
| T5 | `RaceRules` (plain C#) + Edit Mode tests: order, wrong way, duplicate finish, missed checkpoint | Wed 11 | done (Noah, #47) |
| T6 | Lap time 35-60 s check with the real kart; adjust route | Fri 13 | todo |
| T7 | Low white fence (posts + two flat boards) along both road edges of the campus world `Track_CampusWorld`, none in the tunnel or at the terrace jump; prefab `Track/Prefabs/WhiteGuardrails.prefab` built by `WhiteGuardrailBuilder` from Role 3's `track.json` | Fri 13 | in review (Noah, PR #63) |

## Role 3: Kart, visuals + web release (Luc)
| ID | Task | Due | Status |
|---|---|---|---|
| V1 | `docs/art-style.md`: palette (5-6 colours), shape language, 1 reference image | Sun 8 | todo |
| V2 | Scale test: 1 m cube from Blender as FBX imports at 1 m, forward = +Z; record export settings in `docs/art-style.md` | Sun 8 | todo |
| V3 | Kart + simple seated driver FBX, fits `Kart/Visual` (agree pivot/size with Role 1) | Wed 11 | in review (#34): `KartVisual_Classic_Rookie` prefab, steering wheel and hands follow `Steer` (own two-bone IK, no package), playtested by Luc 10 Nov |
| V4 | 2-3 low-poly campus landmarks from the photos (e.g. library facade, EDP Plaza, D/B ramp) | Fri 13 | done (#33): whole campus world `Track_CampusWorld` on Noah's route, built by `CampusWorldBuilder` |
| V5 | `KartEffects`: drift sparks + boost flame, reads `IKartState` only | Thu 12 | todo |
| V6 | Complete race at the itch.io URL, logged in `docs/builds.md` | Thu 12 | todo |
| V7 | README "How to build" reproducible by a second person | Fri 13 | todo |
| V8 | `Visuals/CampusWorld/CampusWorld.prefab`: the whole campus world as one prefab (colliders, trees and props, sun and sky light) for Role 2's `Track_Campus`; `Track_CampusWorld` stays Role 3's preview with the race placeholders | Tue 10 | done (Luc, #37) |
| V9 | Look pass for the campus world: soft trees with wind, moving sea, grass and sand detail, grass tufts (own shaders in `Visuals/Shaders/`, fallback to URP Lit) | – | done (#64) |
| V14 | World pass from the photo/orthophoto/Street View surveys: real coastline and beach (no sea through the land), beachfront restaurants and promenade, street section and Capricciosa forecourt, campus buildings, upper plaza with frosted-glass skylights, sunken courtyard, library and bridge passage, drivable stairs, real jump kicker. Two race-line changes proposed to Role 2 (tunnel exit, line past the library) | Fri 13 | in review (Luc, branch `role3/V14-coast-campus-rebuild`) |
| V15 | Campus world from the Google Maps photosphere survey (reports V14a/V15 in the local atlas): start plaza (young staked trees, calçada with drain channels, seat blocks, lamps, car-park ramp), NOVA letters + space frame at the west entrance, Milestone facade, north loop (colonnade, drop-off setts, Student Central, bins, housing), promenade bollards, flood masts, seat-walls. Route unchanged; stacked on V14 (#85) | Tue 17 | claimed (Luc, 2026-11-11, branch `role3/V15-campus-streetview`) |
| V17 | "AAA" look pass on the campus world: stronger golden-hour sun against a darker fill, ACES tone curve + warm/cool split toning, warm sea haze (exponential fog), sea with fresnel reflection, sun glints, light through the waves and received shadows, lawn backlight + micro shadows, tree translucency + darker crown inside. Camera post-processing is a separate help PR for Role 4. Stacked on V15 (#86) | Tue 17 | claimed (Luc, 2026-11-11, branch `role3/V17-aaa-look`) |
| V18 | Campus world pass from the photosphere survey and the team walk of 7 Nov: the eight V16 area passes (plaza + stairs, campus east + hall G, campus west, north + start plaza, coast + cove + tunnel, railings/planting, Capricciosa forecourt, sharp backdrop) merged, then corrected with the walk findings (G's single tall column, Capricciosa louvred entrance + stair, hairpin island tip, promenade edge, upper promenade path, tunnel forecourt, north loop, start plaza slabs). Generator only, route unchanged; stacked on V17 (#87) | Tue 17 | claimed (Luc, 2026-11-11, branch `role3/V18-world-pass`) |

## Role 4: Race flow, UI + sound (David)
| ID | Task | Due | Status |
|---|---|---|---|
| R1 | `Main.unity` loads `Track_Campus` additively and spawns the kart at `SpawnPoint` | Tue 10 | done (David, 2026-11-08) |
| R2 | `RaceManager`: Ready -> Countdown -> Racing -> Results, input off outside Racing | Wed 11 | done (David, 2026-11-08) |
| R3 | HUD: countdown, timer, final time, boost cue, retry button, "Press any key to start" screen | Wed 11 | done (David, 2026-11-08) |
| R4 | Retry (full reset) + recovery key R (respawn at last valid checkpoint, timer keeps running) | Thu 12 | done (David, 2026-11-08) |
| P9 | Touch controls for phones and tablets (David, 2026-11-10: "the game should be playable on phone and computer"): HTML overlay in the Web template that sends the keys the game already reads (steer, gas, brake, drift, recover, retry, reserved item slot), taps on the game = mouse clicks, rotate hint, page fullscreen. No game code changes. See `docs/decisions.md` 2026-11-10 | Fri 13 | done (David, #57) |
| R5 | Audio (ElevenLabs or CC0): engine loop (pitch by speed), drift, boost, countdown beeps, finish jingle, music. WAV sources, loops tested in the Web build. Free ElevenLabs plan = credit "Sound effects: elevenlabs.io" in game + itch page. Every file listed in `THIRD_PARTY.md` | Fri 13 | done (David, 2026-11-08) |
| R6 | Polish from the 2026-11-10 audit: P pause, menu sounds, wall bump / take-off / chained boost sounds, seaside ambience, ghost chip, ahead/behind chime, wrong-way and pause cues, loader keys, KartEffects on the kart when Luc's prefab exists (his request Q10, #46), MusicPlayer deleted, walkthrough complete | Fri 13 | done (David, #58) |
| R7 | Item boxes (David's call, `docs/decisions.md` 2026-11-10): azulejo boxes after every second checkpoint, one HUD slot, Luc's four items Espresso / Seagull Delivery / Carcavelos Wave / Dean's List used with E (placeholder looks drawn in code until Q16), catch-up odds, own sounds, Edit + Play Mode tests. Needs Q15 for real speed | Fri 13 | done (David, #62); real speed needs Q15 (#61, Alessandro merges) |

## Round 2 (split in the group chat 10 Nov, updated 11 Nov): everyone's features
Deadlines: **Wed 11** slower driving (D7) + `TrackProgress` (T8) · **Fri 13** every feature merged · **Tue 17** the map looks like the campus videos · **Sun 22** feature freeze (only fixes) · **Mon 23 23:59** Moodle submission. Contracts for bots, positions and the minimap: `docs/interfaces.md` section "Round 2". Your brief with a paste-ready prompt for your AI: **`docs/round2-briefs.md`**. All members get the same grade (course wiki), so everyone reads `docs/course-requirements.md` once.

### Role 1: Alessandro
| ID | Task | Due | Status |
|---|---|---|---|
| D7 | **Slower driving, first** (medals, bots and lap times depend on it): lower top speed and acceleration in `KartTuning` so the campus lap feels controllable; post the new top speed in the group | Wed 11 | todo |
| D8 | `IKartDrivable` on `KartController`: when `InputSource` is set the kart reads `InputSource.ReadInput().Clamped()` instead of the keyboard; `InputEnabled` still wins. Luc's bots need it | Thu 12 | todo |
| D9 | Name tags above every kart (reads `IRacer.DisplayName` + `MapColor` from `RacerRegistry`), always facing the camera, hidden for the player's own kart, fade with distance | Fri 13 | todo |
| D10 | Flying animation in the air: the kart body tilts with the jump (nose up on take-off, down before landing), small wobble, lands upright (`IsGrounded` unchanged) | Fri 13 | todo |
| D11 | Trick after a jump: a key in the air spins/flips the model only; a clean landing gives a small boost (same boost path as drift), a trick still running at landing gives nothing. Edit Mode tests for the rule | Fri 13 | todo |
| Q15 | Review + merge help PR #61 (`KartController` implements `IKartBoost`, so the item boxes give speed) | Wed 11 | in review (#61) |

### Role 2: Noah
| ID | Task | Due | Status |
|---|---|---|---|
| T7 | Barriers / guardrails along the road (your draft #63) so karts cannot fall off or cut corners | Fri 13 | in review (Noah, #63) |
| T8 | **`TrackProgress` implements `ITrackProgress`** on the `TrackInfo` root: racing line in driving order (start line first), `DistanceAlongLap`, `PointAt`, `DirectionAt`. May wrap `Shared/TrackPolyline`. Positions, minimap and bots read it | Wed 11 | in review (Noah, #76) |
| T9 | Realistic main building inside: atrium, KPMG gallery, staircase (from your help PRs #44/#45), made by your own builder under `Track/` as a prefab; Luc places it in the campus world (request Q18) | Tue 17 | todo |
| T10 | Piano in the atrium + piano sounds when a kart drives past or hits it (clip + script in your folder, CC0 or own recording, listed in `THIRD_PARTY.md`) | Tue 17 | todo |
| T11 | Volleyball fields on the beach (nets, lines, posts; colliders, placed beside the racing line, not on it) | Tue 17 | todo |

### Role 3: Luc
| ID | Task | Due | Status |
|---|---|---|---|
| V10 | **AI bots**: 5 rule-based bot drivers (your 3 NPC drivers, the Dean, one professor; made-up cartoon characters and names only), each a `Kart` driven by a bot `IKartInputSource` (follow `ITrackProgress.PointAt(distance + look-ahead)`, slow for corners, drift on long bends, light rubber band so the race stays close), registered as `IRacer`; you can ram them. Arcade style, no machine learning | Fri 13 (drive) / Tue 17 (polish) | claimed (Luc, 2026-11-10) |
| V12 | **Nova hall in the campus world** (Noah's interior, help PR #45, plus his white fence #63 = request Q19): `CampusWorldBuilder` switches off the old Edificio Principal block, cuts ground and road out of its footprint, lays straight road pieces into the four hall doors (`HallDoorApproach`), puts `NovaHallWorld.fbx` in with colliders and the white fence at 0/0/0 (metal railings along the road and the hall's lilac rails off). `tools/world_gen/hall_route.py` puts Noah's two indoor lanes into `track.json` (lap 1344 -> 1390 m, two checkpoints now in the hall); rerun it after every `export_unity_bundle.py` | Fri 13 | in review (#77) |
| V6 | Complete race at the deployed URL, logged in `docs/builds.md` (with David) | Sun 22 | todo |

### Open pool (was on Luc's list, unassigned: claim one in the group chat before starting)
| ID | Task | Status |
|---|---|---|
| O1 | More items: giant (kart grows, pushes others), invincibility (own look, nothing like a Nintendo star), traps on the road. Items live in RaceFlow, so pair with Role 4 | open |
| O2 | Book item thrown by bots (bots use items) | open |
| O3 | Dean of Nova character model (made-up cartoon, no real person's face) for the Dean bot and the Dean's ghost | open |
| O4 | Realistic outside map: tunnel, ramp, stairs, plazas, terrace, lawns, promenade (Luc's campus world: Luc's OK first) | open |
| O5 | Outdoor tables and chairs (cafes, terrace) | open |

### Role 4: David
| ID | Task | Due | Status |
|---|---|---|---|
| R8 | Race flow for every racer: grid slots behind the `SpawnPoint` for registered bots, `InputEnabled` on GO / off at the finish for every racer, Retry puts every racer back; the player registers as `IRacer` | Thu 12 | done (David, #69) |
| R9 | Live positions ("2nd / 6") + minimap top right (racing line + racer dots, player highlighted); works with `ITrackProgress` or the checkpoint fallback | Fri 13 | done (David, #69) |
| R10 | Online time leaderboard with names: every finished time saved and ranked, top 10 on the start screen; name validation, offline fallback, no credentials in the repo (BLOCKED part: David creates the free database account) | Fri 13 | done (David, #68; online part BLOCKED until David creates the Supabase project, offline "this device" board works) |
| R11 | Start screen "Carcavelos Round 3" (title animation, slot for the leaderboard) + "Lab opener" (meaning to be confirmed by David) | Fri 13 | done (David, #68; "Lab opener" waits for David) |
| R12 | Waves rolling onto the beach + 2-4 surfers riding them (new Role 4 folder `Ambience/`, placed at runtime, no edit to Luc's scene) | Tue 17 | done (David, #66) |
| R13 | After D7: new medal times + re-record the Dean's ghost on the final track | Tue 17 | todo (waits for D7 #73 to merge: new top speed 18 m/s, 26.1 m/s boosted) |
| R14 | Integration, deploy check with Luc (V6/G5), README for the submission | Sun 22 | todo |
| R15 | Arcade UI restyle per art direction: OFL fonts, colour tokens, plates, motion; title, HUD, results, leaderboard, name entry | Fri 13 | done (David, #71) |

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
| Q11 | 3 -> 1 | When the `Kart` prefab exists: put `Visuals/Kart/Prefabs/KartVisual_Classic_Rookie.prefab` under `Kart/Visual` (pivot at ground centre, forward +Z, 1 m). It reads only `IKartState`; a `Steer` value in `IKartState` would make the steering wheel exact (now estimated from the turn rate) | done 2026-11-10: pivot at ground + empty `Visual` (#53), `Steer` (#55); model inside `Kart.prefab` = help PR #60 for Alessandro |
| Q12 | 3 -> 4 | Same as Q3 (PR #9): OK to add the Animation Rigging package (`com.unity.animation.rigging`)? Only for the drivers' hands to follow the steering wheel (IK) | answered 2026-11-10 (David): OK, but only after the Friday 13 freeze |
| Q13 | 1 -> 4 | Chase camera (D2) is ready as the prefab `Driving/ChaseCamera.prefab` (Cinemachine: Follow + Rotation Composer, offset 0/3/-6). To use it in `Main.unity` instead of `PlaceholderChaseCamera`: put a `CinemachineBrain` on the Main Camera, add the prefab, and when the kart spawns set the prefab's `CinemachineCamera.Follow` (Tracking Target) to the kart. After `ResetTo` (retry / recovery) call `CinemachineCamera.OnTargetObjectWarped(kartTransform, newPosition - oldPosition)` so the camera jumps with the kart instead of flying across the track. Your decision when to swap (Role 4 owns `Main.unity`); I can pair on it | done 2026-11-10 (#51): `ChaseCameraRig` puts a CinemachineBrain on the Main Camera, sets Follow to the spawned kart and snaps the camera after retry/recovery; `PlaceholderChaseCamera` removed |
| Q16 | 4 -> 3 | Item looks for R7 (your four designs are now the items): HUD icons as PNG (256 px, transparent) and, if you like, the models (lifebuoy ring, gold coin halo, seagull) as prefabs under `Assets/_Project/Visuals/Items/`. Tell David the paths; RaceFlow then swaps its code-drawn placeholders (`UI/ItemIcon.cs`, `SeagullRing.cs`, `DeansHalo.cs`) for them via a slot in `ItemSettings`. No other change needed on your side | open |
| Q15 | 4 -> 1 | Item boxes (R7) give their boosts through the new optional contract `Shared/IKartBoost` (`AddBoost(seconds, speedMultiplier)`, `docs/interfaces.md`). Please let `KartController` implement it: help PR `help/role1-Q15-item-boost-david` (about 15 lines: boost time is added, the higher multiplier wins, `ResetTo` and the end of the boost clear it). You review and merge, or change it however you like | in review: help PR #61, Alessandro merges |
| Q17 | 3 -> 4 | Bots in the race (V10 needs R8): RaceFlow switches `InputEnabled` for every registered `IRacer` and puts them on grid slots on GO and Retry; bots only register and drive | done (#69: RaceFlow handles every registered IRacer) |
| Q18 | 2 -> 3 | Place Noah's prefabs (T9 atrium/gallery/staircase, T10 piano, T11 volleyball) in `CampusWorldBuilder`, one line each; Noah sends the prefab paths | open |
| Q19 | 2 -> 3 | Guardrails (T7): `Track/Prefabs/WhiteGuardrails.prefab` sits at 0/0/0 and lines up with the road from `track.json`. Please add it to `Track_CampusWorld` (one line in `CampusWorldBuilder.BuildScene`: `PrefabUtility.InstantiatePrefab` of that prefab under the root, if it exists), and switch off your own railings along the road (`Prop_RailingSegment` near the road, the hall's track railing) so there is only one kind of fence on the whole map | open |
| Q20 | 2 -> 3 | T8 racing line in the race scene: in `CampusWorldBuilder.AddRaceParts`, right after `race.AddComponent<TrackInfo>()`, add `var roadPoints = new List<Vector3>(); for (int i = 0; i < data.points.Length; i++) { roadPoints.Add(Pos(data.points[i])); } race.AddComponent<TrackProgress>().SetRoadPoints(roadPoints); // Role 2's racing line (T8)`, then rebuild `Track_CampusWorld`. Start and direction come from checkpoint 0 by themselves. Needs Noah's PR #76 on `main` first | done (Luc) |
