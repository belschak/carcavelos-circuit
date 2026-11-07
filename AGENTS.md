# Carcavelos Circuit: rules for every AI assistant (and every human)

Single-player 3D kart time trial on Nova SBE's Carcavelos campus. Unity **6000.3.28f1** (6.3 LTS, never upgrade), C#, Blender models as FBX, Web build on itch.io. Student team project (Intro to Programming): the code must stay simple enough that every teammate can explain any file in the final walkthrough.

Deadlines: every role's part merged **Fri 13 Nov 2026**, feature freeze 17 Nov, release candidate 20 Nov, presentation 23 Nov.

## Before you do anything
1. `git switch main && git pull`.
2. Read `TASKS.md`, `docs/team.md` (who owns what), `docs/interfaces.md` (contracts), `docs/decisions.md`. For track work also `docs/route.md`.
3. Find the task in `TASKS.md`. If it is not `todo`, or it belongs to another role: **stop and ask your human**.
4. Claim it: create the branch `role<N>/<task-id>-<short-name>` (e.g. `role1/D3-drift-boost`), make the first commit only the `TASKS.md` status change to `claimed (<name>, <date>)`, push, and open a **draft PR** right away (`gh pr create --draft --fill`). Open draft PRs = who is working on what. Check them (`gh pr list`) before claiming.

## Ownership: only the owner (or their AI) edits these paths
| Path | Role |
|---|---|
| `Assets/_Project/Driving/**` | 1 Driving + camera |
| `Assets/_Project/Track/**`, `docs/route.md` | 2 Track + race rules |
| `Assets/_Project/Visuals/**`, `docs/art-style.md`, `docs/builds.md` | 3 Kart, visuals + web release |
| `Assets/_Project/RaceFlow/**`, `Assets/_Project/Audio/**`, `Assets/_Project/Shared/**`, `ProjectSettings/**`, `Packages/**` | 4 Race flow, UI + sound (integrator) |
| `Assets/_Project/Tests/**` | each role adds tests for its own code only |
Names and handles: `docs/team.md`. Need a change outside your paths? Add a row to `TASKS.md` addressed to that role. Do not do it yourself.

## Contracts: the only way modules talk
Read `docs/interfaces.md`. Kart exposes `IKartState`, the track exposes `TrackInfo` + checkpoints, `RaceRules` is plain C#. RaceFlow, Visuals and Audio only read those contracts and never reach into another module's internals. Changing a contract needs the team's OK (decision in `docs/decisions.md`).

## NEVER
- Never text-edit `.unity`, `.prefab`, `.asset`, `.mat`, `.controller`, `.anim` YAML or any `.meta` file. Scene and prefab work = you write C# and give the human exact Inspector steps.
- Never create, delete, move or rename assets outside the Unity Editor (breaks `.meta` GUIDs and references).
- Never open-and-save a scene or prefab another role owns. One scene per owner (see `docs/team.md`).
- Never touch `Library/ Temp/ Logs/ obj/ UserSettings/ Build-Web/`. `ProjectSettings/` and `Packages/` = Role 4 only, after a team OK.
- Never rename a serialized field without `[FormerlySerializedAs("oldName")]`.
- Never put `.blend`, `.psd`, raw photos or videos in the repo. Export FBX/PNG, respect the size budget (textures max 2048 px kart, 1024 props, 512 small props; single file < 25 MB).
- Never commit to `main` directly, never force-push, never rewrite history.
- Never add packages, Asset Store imports, MCP servers or external code without a team OK. Record every third-party asset in `THIRD_PARTY.md` with its licence.
- Never use Nintendo / Mario Kart names, characters, logos, music or sounds. Original or CC0 only.
- Never publish builds, change repo settings or visibility, or spend money.

## C# style (beginner-readable on purpose)
- One class per file, file name = class name, namespace `CarcavelosCircuit.<Module>`.
- Plain fields, `if`, loops, small methods with a one-line comment saying *why*. No clever patterns, no reflection, no LINQ chains, no `GameObject.Find`.
- `[SerializeField] private` fields with `[Tooltip]`; tuning numbers live in ScriptableObjects (`KartTuning`, `RaceSettings`), not hard-coded.
- Rules that can be tested (lap validation, timer, boost charge) go in plain C# classes without `MonoBehaviour`, with Edit Mode tests.
- After you create a script: tell the human "switch to Unity, wait for compile, check the Console", then commit the `.cs` together with its `.meta`.

## Definition of done (for every task)
- Unity Console shows 0 errors and 0 new warnings after recompile.
- Played in Play Mode in your own test scene; for gameplay also in `Main.unity` once it exists.
- Edit Mode tests pass if you touched rules (`docs/setup.md` has the command).
- `git status` shows only paths you own; script + `.meta` + prefab committed together.
- `TASKS.md` row set to `in review`, PR opened with the template filled, one teammate reviews and asks you to explain one function.
- Never claim "tested" or "works" unless a human actually ran it. Say what still needs a playtest.

## When unsure
Stop and ask your human. Don't guess ownership, don't "fix" another module, don't expand scope (out of scope: items, AI opponents, multiplayer, kart selection, touch controls).
