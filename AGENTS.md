# Carcavelos Circuit: rules for every AI assistant (and every human)

Single-player 3D kart time trial on Nova SBE's Carcavelos campus. Unity **6000.3.28f1** (6.3 LTS, never upgrade), C#, Blender models as FBX, Web build on itch.io. Student team project (Intro to Programming): the code must stay simple enough that every teammate can explain any file in the final walkthrough.

Deadlines: every role's part merged **Fri 13 Nov 2026**, feature freeze 17 Nov, release candidate 20 Nov, **Moodle submission Mon 23 Nov 23:59**, presentations W11/W12 (24 Nov or 4 Dec).

**Official course rules we are graded against: `docs/course-requirements.md` (read it once).** The three that affect every session: the repo becomes **public** (never commit secrets or personal data), **every member must commit their own code from their own GitHub account**, and the deployed code must be on `main`.

## Git workflow: every session, every AI, step by step (non-negotiable)
Four people and their AIs push into one repo every day. These steps keep everyone building on the newest version instead of overwriting each other. If a step fails or something looks unexpected: stop and tell your human, do not improvise with Git.

**A. Start of every session, before reading or writing any code**
1. Close Unity (Unity open during a pull or merge can rewrite files).
2. Check the safety hook: `git config core.hooksPath` must print `.githooks`. If not: `git config core.hooksPath .githooks`. It is the only thing that blocks pushes to `main` (GitHub cannot protect `main` on our plan).
3. Check you commit as your human: `git config user.name` and `git config user.email` must be your human's own GitHub account (graded: every member commits their own code).
4. Get the newest version: `git fetch --all --prune`, then `git switch main` and `git pull --ff-only`.
5. **Look at what everyone else built since the last session**, then build on top of it, never re-implement or undo it:
   - `git log --oneline -20 main` and `gh pr list --state all --limit 15` (merged and open PRs, including drafts = who works on what right now).
   - For every merged PR that touches a file, scene or contract you use: read it (`gh pr view <n>` and `gh pr diff <n>`).
   - New rows in `docs/decisions.md`, `docs/interfaces.md`, `TASKS.md` (also "Requests between roles", maybe one is for you).
   - Tell your human in 2 or 3 lines what changed and how it affects the task.
6. Bring your branch up to date: `git switch role<N>/<task-id>-<short-name>`, then `git merge main`. Conflict in your own files: resolve it. Conflict in a file someone else owns: stop and ask your human, never just keep "your" version of their file.
7. Only now open Unity and start working.

**B. While working**
- One branch per task, always created from a freshly pulled `main` (steps 4 and 5 first). Never work on `main`, never commit to someone else's branch unless they asked (see "Helping another role").
- Commit small and **push after every finished step** (a script compiles, a test passes, a task row changes) and at the latest before the session ends, even work in progress (`wip:` prefix). Unpushed work does not exist for the team.
- Before every commit: `git status` must show only paths your role owns (table below). Add files by name (`git add <files>`), never a blind `git add -A` or `git add .`. Commit each script together with its `.meta`.
- Others merge every day: run steps 4 and 6 again at least once a day while your branch is open.
- Your draft PR is your live status: teammates see what you did without asking.

**C. Finishing a task**
1. Steps 4 and 6 again (newest `main` merged into your branch), open Unity, Console clean, play once, tests if you touched rules, push.
2. Fill the PR template, mark the PR ready, ask a teammate to review. The owner of the paths (see `.github/CODEOWNERS`) merges with **Squash and merge**.
3. After the merge: `git switch main`, `git pull --ff-only`, delete the old branch (`git branch -d <branch>`), and start the next task on a **new** branch. Never keep committing on a branch whose PR is already merged (squash merges make its old commits conflict with `main`).
4. Tell the group chat: "merged <task>, please pull".

**D. Git commands an AI must never run** (they destroy other people's work or hide it): `git push --force` / `-f`, `git reset --hard`, `git rebase` of a pushed branch, `git commit --amend` of a pushed commit, `git clean -fd`, `git checkout -- .` / `git restore .` on files you did not change, `git stash` without telling your human, pushing to `main`, `git add -f` on ignored files.

## Before you start a task
1. Section A above (pull, read what the others built).
2. Read `TASKS.md`, `docs/team.md` (who owns what), `docs/interfaces.md` (contracts), `docs/decisions.md`. For track work also `docs/route.md`.
3. Find the task in `TASKS.md`. If it is not `todo`, or it belongs to another role: **stop and ask your human**.
4. Claim it: create the branch `role<N>/<task-id>-<short-name>` (e.g. `role1/D3-drift-boost`), make the first commit only the `TASKS.md` status change to `claimed (<name>, <date>)`, push, and open a **draft PR** right away (`gh pr create --draft --fill`). Open draft PRs = who is working on what. Check them (`gh pr list`) before claiming.

## Ownership: the owner (or their AI) edits these paths; others only via "Helping another role" below
| Path | Role |
|---|---|
| `Assets/_Project/Driving/**` | 1 Driving + camera |
| `Assets/_Project/Track/**`, `docs/route.md` | 2 Track + race rules |
| `Assets/_Project/Visuals/**`, `docs/art-style.md`, `docs/builds.md` | 3 Kart, visuals + web release |
| `Assets/_Project/RaceFlow/**`, `Assets/_Project/Audio/**`, `Assets/_Project/Shared/**`, `ProjectSettings/**`, `Packages/**` | 4 Race flow, UI + sound (integrator) |
| `Assets/_Project/Tests/**` | each role adds tests for its own code only |
Names and handles: `docs/team.md`. Need a change outside your paths? Add a row under "Requests between roles" in `TASKS.md` addressed to that role.

## Helping another role (allowed, the owner stays the owner)
Everyone does their own tasks first. Helping is welcome when the owner asked or agreed (chat or a `TASKS.md` request row):
- Branch `help/role<N>-<task-id>-<your-name>`, commit messages start with `help(role<N>):` so everyone sees who did what for whom (AI help too: say "done by <name>'s Claude/ChatGPT" in the commit body).
- Push, open a PR and request the owner as reviewer. **Only the owner merges it.** Never take over their task row or their scene.
- Small fix in someone's open branch: only if they asked; push to their branch, then tell them in the group chat.

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
- Never commit secrets (API keys incl. ElevenLabs, tokens, `.env`, passwords) or personal data (emails, phone numbers, photos of people; student numbers only in the README team table). This repo will be public.
- Never commit on behalf of another teammate. Each person commits their own work from their own GitHub account (graded: "every member must contribute code").

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
Stop and ask your human. Don't guess ownership, don't "fix" another module, don't expand scope (out of scope: items, AI opponents, multiplayer, kart selection; touch controls are in scope since 2026-11-10, see `docs/decisions.md`).
