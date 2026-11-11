# Round 2 briefs (one per person, 11 Nov 2026)

Who does what in round 2, written so your AI can start without asking. The board with IDs and due dates is `TASKS.md` > "Round 2". The contracts are `docs/interfaces.md` > "Round 2". Grading (same grade for all four of us, Security counts 10 %): `docs/course-requirements.md`.

**How to use this file:** open a new chat with your AI in the repo folder and paste the prompt from your section. It already contains the Git steps from `AGENTS.md`. Let it work one task at a time, one branch and one PR per task.

Rules that apply to everyone:
- Pull first, every session (`AGENTS.md` section A). Branch `role<N>/<task-id>-<short-name>` from a fresh `main`, first commit = your `TASKS.md` row set to `claimed (<name>, <date>)`, draft PR right away.
- Only your own folder (`AGENTS.md` ownership table). Need something from another role: a request row in `TASKS.md` + a message in the group.
- Every rule you write gets Edit Mode tests for what is **accepted**, what is **rejected** and the **edge cases** (criterion 4). Run them before you mark the PR ready.
- Add a short section to a walkthrough file in `docs/explain/` (`role1-walkthrough.md`, `role2-...`, `role3-...`): what you built, why this way, its limits, what you would change. Anyone may be asked about it.
- Names and faces: made-up cartoon characters only. No real people's photos (no LinkedIn pictures), no Nintendo, Mario Kart or F1 names. Real professors' names only if they said yes.
- Never commit keys, passwords or personal data. The repo goes public.

---

## Alessandro (Role 1: driving + camera)

**Goal:** a kart that feels great at a controllable speed, that a bot can drive, with name tags and fun jumps.

Order: **#61 first** (review + merge, gives the item boxes their speed), then **D7 today**, then D8 (Luc's bots wait for it), then D9-D11.

| Task | What "done" means | Tests (Edit Mode) |
|---|---|---|
| D7 slower driving | Lower top speed and acceleration in `KartTuning` (only the asset values, maybe one new field). A campus lap is controllable for a first-time player; drift and boost still feel strong. Post the new top speed in the group (Role 4 re-tunes medals and the Dean's ghost after it, R13) | Existing tests stay green |
| D8 `IKartDrivable` | `KartController : IKartDrivable`. `InputSource == null` = keyboard + phone exactly as today. Set = read `InputSource.ReadInput().Clamped()` once per frame instead (throttle -1..1, steer -1..1, drift). `InputEnabled == false` = no input from anyone | Put the "which input wins" rule in a tiny plain C# class (e.g. `InputPicker`) and test: null source = keyboard, source set = source, input off = nothing, NaN or 5.0 from a source = clamped |
| D9 name tags | One `NameTag` per registered racer (`RacerRegistry.All`, listen to `RacerRegistry.Changed`), above the kart, faces the camera, `DisplayName` + `MapColor`, hidden for `IsPlayer`, fades out beyond ~60 m, never blocks the road view | Fade-by-distance and "show for this racer?" as plain C#: player hidden, empty name hidden, too far = alpha 0 |
| D10 flying animation | While `!IsGrounded` the kart model (not the physics) tilts: nose up after take-off, nose down before landing, small wobble; back to upright on landing | Tilt curve as plain C#: grounded = 0, rising = up, falling = down, clamped angles |
| D11 trick | A key in the air (e.g. Shift; add a phone button request to Role 4) spins/flips the model once. Finished before landing = small boost on landing (through the same boost path as drift); still spinning at landing = no boost; one trick per jump | Trick rule as plain C#: no trick = no boost, trick done = boost, trick late = none, second trick in the same jump = rejected |

Files: `Assets/_Project/Driving/**` and tests for them. Contracts used: `IKartDrivable`, `KartInput`, `IKartInputSource`, `IRacer`, `RacerRegistry` (all in `Shared/`, read only for you).

**Prompt to paste:**
```
You are my coding agent for our Unity team game Carcavelos Circuit (I am Alessandro, Role 1: driving + camera). Before any code: read AGENTS.md and follow section A step by step (close Unity, check the hook, check my git name/email, git fetch --all --prune, git switch main, git pull --ff-only, read what changed). Then read TASKS.md section "Round 2", docs/round2-briefs.md section "Alessandro", docs/interfaces.md section "Round 2" and the IKartDrivable/KartInput files in Assets/_Project/Shared/. First review help PR #61 with me (gh pr view 61, gh pr diff 61) and merge it with Squash and merge if I agree. Then do one task at a time in this order: D7, D8, D9, D10, D11. For each: new branch role1/<task-id>-<short-name> from fresh main, first commit only my TASKS.md row as claimed, push, open a draft PR. Only edit Assets/_Project/Driving/ and my own tests. Put every rule in a small plain C# class with Edit Mode tests for accepted, rejected and edge cases. Never text-edit scenes, prefabs or .meta files: give me exact Unity Inspector steps instead. Tell me when to switch to Unity, wait for the compile and check the Console. Add a short section to docs/explain/role1-walkthrough.md (what, why, limits). Push after every step. When a task is done: merge main into the branch, run the tests, mark the PR ready and tell me what to post in the group chat.
```

---

## Noah (Role 2: track + race rules)

**Goal:** a track that knows where every kart is, barriers that keep karts on it, and a campus that looks like the videos inside and on the beach.

Order: **T8 today** (positions, minimap and bots wait for it), T7 (your draft #63), then T9-T11 by Tue 17.

| Task | What "done" means | Tests (Edit Mode) |
|---|---|---|
| T8 `TrackProgress` | `TrackProgress : MonoBehaviour, ITrackProgress` on the same object as `TrackInfo` in the race track scene. A racing line along the middle of the road in driving order, first point at the start/finish line, more points in corners (every ~5-10 m is fine). Easiest: build a `Shared/TrackPolyline` from the points and forward the five members to it. Draw the line with `OnDrawGizmos` so you can see it in the Scene view. Tell Luc how the points are made if they come from his campus world builder (that scene is his) | `TrackPolyline` is already tested. Add tests for anything of your own (e.g. a helper that turns your road points into the line: start point first, order kept, no duplicates) |
| T7 barriers | Guardrails/walls along the road edges where a kart could fall or cut a corner; colliders on, no gaps at joints; kerbs stay drivable | If you write placement maths (spacing, gaps), test it: no gap wider than the kart, none on the racing line |
| T9 main building inside | Atrium, KPMG gallery and staircase from your help PRs #44/#45, made by your own builder under `Track/` as a prefab. Luc places it in his campus world (request Q18 in `TASKS.md`: send him the prefab path) | Placement/size maths only |
| T10 piano | A piano in the atrium; driving past or bumping it plays a few notes (clip + small script in your folder; CC0 or own recording, listed in `THIRD_PARTY.md`; no copyrighted songs) | "Which note / how loud" from speed and distance as plain C# |
| T11 volleyball | 1-2 beach volleyball courts (nets, lines, posts) beside the racing line, with colliders | none needed |

Files: `Assets/_Project/Track/**`, `docs/route.md`, your tests. Contract you provide: `ITrackProgress`.

**Prompt to paste:**
```
You are my coding agent for our Unity team game Carcavelos Circuit (I am Noah, Role 2: track + race rules). Before any code: read AGENTS.md and follow section A step by step (close Unity, check the hook, check my git name/email, git fetch --all --prune, git switch main, git pull --ff-only, read what changed). Then read TASKS.md section "Round 2", docs/round2-briefs.md section "Noah", docs/interfaces.md section "Round 2" and Assets/_Project/Shared/ITrackProgress.cs + TrackPolyline.cs. Start with T8 today: new branch role2/T8-track-progress from fresh main, first commit only my TASKS.md row as claimed, push, open a draft PR. TrackProgress implements ITrackProgress on the TrackInfo object and may wrap a TrackPolyline. Then T7 (my draft PR #63), then T9, T10, T11, one branch and PR each. Only edit Assets/_Project/Track/, docs/route.md and my own tests; the campus world scene belongs to Luc, so for anything placed there write a request row (Q18) and send him the prefab path. Every rule in a small plain C# class with Edit Mode tests for accepted, rejected and edge cases. Never text-edit scenes, prefabs or .meta files: give me exact Unity Inspector steps instead. Tell me when to switch to Unity, wait for the compile and check the Console. Add a short section to docs/explain/role2-walkthrough.md (what, why, limits). Push after every step. When a task is done: merge main into the branch, run the tests, mark the PR ready and tell me what to post in the group chat.
```

---

## Luc (Role 3: visuals, bots + web release)

**Goal:** five rule-based bots that race like an arcade kart game (close, fun, rammable), plus the world look and the deployed build.

Your other items from the 10 Nov list are in the **open pool** (`TASKS.md` O1-O5) for now; nobody owns them yet.

| Task | What "done" means | Tests (Edit Mode) |
|---|---|---|
| V10 bots | 5 bots: your 3 NPC drivers, the Dean, one professor (made-up cartoon characters and names; a real professor's name only with their OK). Each = a normal `Driving/Kart.prefab` instance + your `BotDriver : MonoBehaviour, IKartInputSource, IRacer` that sets itself as `IKartDrivable.InputSource` and registers in `OnEnable` / unregisters in `OnDisable` (`RacerRegistry`). Driving rule: target = `ITrackProgress.PointAt(myDistance + lookAhead)`, steer towards it, ease off the throttle when the next bend is sharp, drift on long bends, small random line offset per bot so they do not drive in a queue, light rubber band (a bit faster when far behind the player, a bit slower when far ahead, never above the kart's max). Stuck for 2 s = reverse and steer out. Never call `InputEnabled` or `ResetTo`: RaceFlow does that (R8, request Q17). Spawned where RaceFlow says (Role 4 adds them to the grid); you can ram them | Bot decisions as plain C#: steer sign towards a target left/right, throttle lower for a sharp bend, rubber band never above 1.0 throttle and never negative speed, stuck detection after 2 s and not before, NaN-safe |
| V9 world look | your draft #64 (trees, moving sea, grass, sand) | as you like |
| V6 deploy | complete race at the deployed URL, `docs/builds.md` with the `main` commit (with David) | none |

Until D8 (Alessandro) is merged, test your bot logic on a copy in your showroom scene or with a tiny stand-in kart in your folder; the contract does not change. Until T8 (Noah) is merged, `TrackPolyline` built from the checkpoint positions gives you an `ITrackProgress` to drive against.

Files: `Assets/_Project/Visuals/**` (bots can live in `Visuals/Bots/`), `docs/art-style.md`, `docs/builds.md`, your tests. Contracts used: `IRacer`, `RacerRegistry`, `ITrackProgress`, `IKartDrivable`, `KartInput`, `IKartInputSource`, `IKartState` (read only).

**Prompt to paste:**
```
You are my coding agent for our Unity team game Carcavelos Circuit (I am Luc, Role 3: kart, visuals, bots + web release). Before any code: read AGENTS.md and follow section A step by step (close Unity, check the hook, check my git name/email, git fetch --all --prune, git switch main, git pull --ff-only, read what changed). Then read TASKS.md section "Round 2", docs/round2-briefs.md section "Luc", docs/interfaces.md section "Round 2" and the Shared contracts IRacer, RacerRegistry, ITrackProgress, TrackPolyline, KartInput, IKartInputSource, IKartDrivable. Task V10: new branch role3/V10-ai-bots from fresh main, first commit only my TASKS.md row, push, open a draft PR. Build 5 rule-based bots (no machine learning): BotDriver implements IKartInputSource and IRacer, drives a normal Kart prefab through IKartDrivable, follows ITrackProgress.PointAt(distance + look-ahead), registers in RacerRegistry. Never set InputEnabled or call ResetTo (RaceFlow does that). Made-up cartoon names and looks only, no real people's photos, no Nintendo/F1 names. Only edit Assets/_Project/Visuals/ and my own tests. Every decision rule in a small plain C# class with Edit Mode tests for accepted, rejected and edge cases. Never text-edit scenes, prefabs or .meta files: give me exact Unity Inspector steps instead. Tell me when to switch to Unity, wait for the compile and check the Console. Add a short section to docs/explain/role3-walkthrough.md (what, why, limits). Push after every step. When done: merge main into the branch, run the tests, mark the PR ready and tell me what to post in the group chat.
```

---

## David (Role 4: race flow, UI, sound, integration)

R8 race flow for every racer · R9 positions + minimap · R10 online leaderboard · R11 start screen "Carcavelos Round 3" · R12 waves + surfers · R13 medals + Dean's ghost after D7 · R14 integration + deploy. Files: `RaceFlow/`, `Audio/`, `Shared/`, new `Ambience/`, `Main.unity`, `ProjectSettings/`, `Packages/`, CI. David's PRs are built by his Claude sessions; same rules as above.
