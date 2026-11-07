# Team, roles and ownership

One owner per role. The owner is the only person (and the only AI) who edits the files in their folder. Anything outside your folder: ask the owner in the WhatsApp group first, or add a row under "Requests between roles" in `TASKS.md`.

| Role | Owner | Backup reviewer | Owns (folders and files) |
|---|---|---|---|
| 1 Driving + camera | _TBD_ | Role 4 | `Assets/_Project/Driving/` (kart controller, input, drift/boost, recovery, chase camera, `Driving_TestBed.unity`) |
| 2 Campus track + race rules | _TBD_ | Role 3 | `Assets/_Project/Track/` (track geometry, colliders, spawn, checkpoints, `RaceRules.cs`, `Track_Campus.unity`) |
| 3 Kart, visuals + web release | _TBD_ | Role 2 | `Assets/_Project/Visuals/` (kart + driver mesh, palette, landmarks, materials, `KartEffects.cs`, `Visuals_Showroom.unity`), Web build + itch.io page + `docs/builds.md`; `art-source/` stays outside the repo |
| 4 Race flow, UI + sound | _TBD_ | Role 1 | `Assets/_Project/RaceFlow/`, `Assets/_Project/Audio/` (all sound + music), `Assets/_Project/Shared/`, `Main.unity`, `ProjectSettings/`, `Packages/` |
| Everyone | | | `Assets/_Project/Tests/` (each owner adds tests for their own rules), `docs/` |

Team: David Belschak, Luc Tippmann, Alessandro Rossi, Noah. Write your name and GitHub handle in the Owner column when roles are fixed. The four roles are meant to be equal in workload.


## Scenes: one scene per owner (avoids merge conflicts)

| Scene | Owner | What it is |
|---|---|---|
| `Driving/Scenes/Driving_TestBed.unity` | 1 | Flat test surface with a few ramps and corners to tune the kart |
| `Track/Scenes/Track_Campus.unity` | 2 | Track geometry, colliders, checkpoints, spawn. No UI, no kart |
| `Visuals/Scenes/Visuals_Showroom.unity` | 3 | Kart and landmark models under neutral light, scale check |
| `RaceFlow/Scenes/Main.unity` | 4 | The game: loads `Track_Campus` additively, spawns the kart prefab, runs the race flow and HUD |

Rule: you never save a scene you do not own. If Unity says a scene you don't own is modified, close it without saving.

## Shared files (change only after the whole team agrees in the group chat)
`ProjectSettings/`, `Packages/manifest.json`, `docs/interfaces.md`, the `Kart` prefab root (Role 1) and its contract, tags and layers, input actions.
