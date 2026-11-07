# Decision log

Newest on top. One line per decision: date, decision, why. Agents: never contradict a decision here without asking the team.

- 2026-11-07: Kart controller = **our own sphere-based Rigidbody controller** (~200 lines), not WheelColliders and not the Karting Microgame. Why: the team must explain every line; a sphere cannot flip; fewest tuning values. Fallback if the kart does not drive well by Wed 11 Nov: copy UniKart (MIT) at a pinned commit and add our drift charge.
- 2026-11-07: Placeholders = Kenney Racing Kit / Car Kit (CC0). Every external asset goes into `THIRD_PARTY.md`.
- 2026-11-07: Board = `TASKS.md` only (no GitHub Issues). Who works on what = draft PRs.
- 2026-11-07: Name "Carcavelos Circuit", no Nova SBE logo or real brand marks on buildings until Nova Communications agrees. Photos are reference only, never shipped with people in them.
- 2026-11-07: Unity Editor pinned to **6000.3.28f1** (newest 6.3 LTS patch, 3 Nov 2026). Nobody upgrades until after the presentation.
- 2026-11-07: **No Git LFS.** Free LFS quota is 10 GiB download/month for the repo owner; 4 clones + pulls in demo week could lock it. Instead a size budget: repo < 500 MB, textures <= 2048 px (kart) / 1024 (props) / 512 (small), no photos, videos, .blend or .psd in the repo.
- 2026-11-07: Repo private on `belschak` (free plan: no branch protection on private repos). Protection by convention: PRs only, `.githooks/pre-push` blocks pushes to `main`, `AGENTS.md` rules. If David's GitHub Education (Pro) comes through, add a ruleset on `main`.
- 2026-11-07: Web build on itch.io, gzip, Decompression Fallback off, click-to-play.
- 2026-11-07: Scope baseline = Luc's brief (1 kart, 1 track, timed lap, drift boost, recovery, retry). Items, AI opponents, kart selection, split screen are out. Why: 17 days, 4 beginners, live demo must work.
- 2026-11-07: Route idea from the team: through the tunnel, along the beach promenade, ramp jump at the ramp structure between the D and B wings (level 1). Final route fixed after the campus walk (`docs/route.md`).
- 2026-11-07: One scene per owner + `Main.unity` loads the track additively. Why: Unity scenes merge badly.
- 2026-11-07: Grey box first (route, main buildings, elevation), colours and textures later. Why: Luc's point, saves time and AI tokens.
- 2026-11-07: Original art and names only, "inspired by" arcade kart racers. No Nintendo characters, names, music or logos.
- 2026-11-06: Unity 6.3 LTS + C#, Blender FBX, Web build on itch.io. TA approved a Unity game (per Luc).
