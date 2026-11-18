# Security: what is protected, how, and what is public on purpose

Graded criterion 5: "Access control and input validation are enforced; dependencies are minimal, pinned, and without
known vulnerabilities." This page explains each part and where to check it. The short policy for reporting a problem
is in [`SECURITY.md`](../SECURITY.md); the tests behind these claims are listed in [verification.md](verification.md).

## 1. The system in one picture

The game is served by GitHub Pages at <https://nova-kart.github.io/> (main address) and, unchanged, at the older
<https://belschak.github.io/carcavelos-circuit/>. Both are built by the same workflow from the same `main` commit.

```
Browser (Unity Web build, GitHub Pages: nova-kart.github.io and belschak.github.io/carcavelos-circuit)
  |-- online leaderboard --> Supabase (Postgres + REST API), public "anon" key, Row Level Security
  |-- online race --------> Photon Fusion cloud (rooms of 15), public App ID
  '-- browser storage ----> settings, personal bests, ghost, last typed name (this device only)
GitHub Actions --> builds the game with keys from repository secrets --> publishes to GitHub Pages
```

There are no user accounts and no passwords: a player only types a nickname. So the things to protect are the
leaderboard data, the keys, the build pipeline and the players' browsers.

## 2. Keys and secrets

| Key | Where it lives | Public? | Why |
|---|---|---|---|
| Unity licence, Unity email + password | GitHub repository secrets | No | Only CI needs them to run Unity |
| `PHOTON_SDK_DEPLOY_KEY` | GitHub repository secret | No | Read-only deploy key for the private repo with the licensed Photon SDK |
| `NOVA_KART_DEPLOY_KEY` | GitHub repository secret | No | Deploy key that lets the Web build workflow publish the build to the `nova-kart.github.io` site repository, nothing else |
| Supabase project URL + **anon / publishable** key | Repository secrets, copied into the Web build | **Yes, by design** | Every browser must call the API with it. It can only do what Row Level Security allows (section 3) |
| Photon App ID | Repository secret, copied into the Web build | **Yes, by design** | Every browser must join the room with it. It only lets clients join rooms of this app, and the plan's concurrent-user limit caps abuse |
| Supabase **service_role / secret** key | Nowhere in the project | Never | It bypasses Row Level Security. `LeaderboardConfigCheck` stops a build that tries to ship it |

How the build keeps them out of Git: the workflow passes them to Unity as arguments; `LeaderboardBuildConfig` and
`OnlineBuildConfig` check their format (https, `*.supabase.co`, publishable key only, App ID must be a GUID), write
them into the build and empty `LeaderboardConfig.asset` again afterwards; the App ID file and the Photon SDK folder are
in `.gitignore`. The test `RepoHygieneTests` and the Security workflow fail if any of this changes.

**History scan (18 Nov 2026).** gitleaks 8.30.1 over every commit of every branch (738 commits, 1.55 GB) and again over
GitHub's pull request refs, which keep commits of deleted branches public (757 commits): **no secret found**. A
search of the whole history for Supabase URLs, JWTs, publishable keys and GUID-shaped App IDs found none either.
`.github/workflows/security.yml` repeats the gitleaks scan on every push, every pull request and weekly.

## 3. Access control

**Leaderboard database** (`backend/supabase/001_lap_times.sql`, `002_rate_limit.sql`):
- Row Level Security is on for `lap_times`; all rights are revoked first, then the public key gets back only
  `SELECT` on four columns and `INSERT` on three (`track`, `name`, `seconds`). `id` and `created_at` are set by the database.
- There is no `UPDATE` or `DELETE` policy or grant: a saved time can never be changed or removed through the API.
  Only the project owner can delete a fake row in the Supabase dashboard.
- The insert policy repeats every CHECK, so calling the REST API directly (without the game) meets the same rules.
- A trigger refuses more than 5 rows per name or 60 rows in total per minute (flooding). Its function is
  `security definer` with a fixed `search_path` and cannot be called through the API.
- `DatabaseRulesMatchCodeTests` reads both SQL files and fails if any of this is loosened.

**Online race:** a room holds at most 15 players (the 16th is told the room is full); only the room's host can send
race-control messages (others are ignored); every message from another player goes through a codec that refuses
anything that is not exactly our format (`RaceControlCodec`, `KartPoseCodec`). Tests: `LoopbackSessionTests`,
`OnlineHostileInputTests`.

**Build and deploy pipeline:** workflows run with `contents: read`; only the separate Pages job may write to Pages;
the private SDK is checked out with a read-only deploy key and `persist-credentials: false`; builds started on other
branches are never published; a Git hook blocks accidental pushes to `main` (`docs/setup.md`).

## 4. Input validation (in the game AND in the database)

| Input | In the game | In the database / elsewhere |
|---|---|---|
| Player name | `PlayerNameRules`: 2-16 characters, letters incl. accents, digits, space, - and _; no HTML, emoji, quotes or rude words | Same length and character CHECK, in the table and the insert policy |
| Lap time | `LapTimeRules`: 5 s to 30 min and not faster than 40 % of the Gold time; NaN and infinity refused | CHECK 5 to 1800 s (NaN fails too) |
| Track name | `LapTimeRules.IsValidTrack`: 1-40 of `A-Z a-z 0-9 _ -` (it goes into a URL) | Same CHECK |
| Leaderboard answers | Max 64 KB, every row re-checked with the rules above, bad rows dropped, never shown | |
| Page address flags | `OnlineUrlFlags`: known keys only, values cut to 200 characters, numbers range-checked | |
| Messages from other players | Exact length/format, version byte, real numbers only, roster at most 64 | |
| Browser storage (any player can edit it) | Settings clamped or default, saved names re-checked, broken saves give "no record" | |
| Build keys | `LeaderboardConfigCheck`, `OnlineAppIdRules` stop the build on a wrong key | |

All text the game shows from outside sources is plain text (rich text tags are refused in names), so a name cannot
style or break the screen. The only check that lives in the game alone is the rude-word list: words change, and a
database list would need a migration for every addition; a rude name sent past the game can be deleted by the owner.

### Test and demo switches in the live build

The live build reads a few switches from its own page address. They exist so the team can check the real deployed
build (phone layouts, item placement, a full online room filled with browser bots for the scale test) without a
separate test build. They stay in the live build on purpose, because a test build would not be the build players get.

| Switch | What it does | Why it is harmless |
|---|---|---|
| `?net=local` | Online race between tabs of the same browser (BroadcastChannel), no Photon | Never leaves the visitor's browser |
| `?room=`, `?name=`, `?driver=`, `?autostart=N` | Join the room with this name and driver; host starts at N players | Same checks as typing the name or clicking; the room cap of 15 still holds |
| `?autopilot=1` | The kart drives itself (online only) | Online races are not saved anywhere: no leaderboard, record or ghost |
| `?render=0` | Cameras off, HUD only (scale-test bots on machines without a GPU) | Only changes what this browser draws |
| `?test=itemrows`, `itemlog`, `uirects` | Camera tour of the item boxes; extra lines in the browser console | Only this browser; nothing is sent or stored |
| `?test=mobile`, `nowebgl`, `error` | Shows the loader's phone / no-WebGL / error card | Only the loading page of this browser |
| `?photo=1` | No menus or HUD (screenshots) | Only this browser |

Every switch only changes the visitor's own browser. None gives more rights at Supabase or Photon: those use the
same public keys and the same server-side rules (section 3) as a normal visit, so anything a switch could do, a
player could also do by calling the public API directly, and the database rules stop it there. Every value is cut to
200 characters and checked (`OnlineUrlFlags`, `ItemTestFlags`; tests in `OnlineHostileInputTests`). The small test
helpers that ship with the code (`TestBot`, `FakeLeaderboardService`, `NameTagTestRacer`) are not used by the game
scenes; only the tests and the driving test scene create them.

## 5. Personal data

- **Public:** the nickname a player chooses, their lap time, the track and the time of saving, on the leaderboard.
  Nothing else is collected: no email, no account, no location.
- **On the player's device only:** settings, personal bests, the ghost lap and the last typed name (browser storage).
- **Third parties:** Photon and Supabase see the player's IP address, as any web service does. GitHub Pages serves the
  game. Unity Analytics, performance reporting and crash reporting are switched off (`ProjectSettings/UnityConnectSettings.asset`).
- **The repository:** names and student numbers of the team are in the README because the course requires it.
  On 18 Nov 2026 every file of every commit, including binary 3D models, was scanned for e-mail addresses, personal
  folder paths (`C:\Users\...`, `/Users/...`) and phone numbers. No phone numbers and no Windows user folders were
  found. Exported FBX models keep the exporting computer's file path in their metadata: some contain a teammate's
  macOS user name, and the item models contain the Google Drive folder path of the teammate who made them, which
  includes their e-mail address. This metadata is not used by the game. Cleaning it from the models and from the Git
  history is a team decision, because rewriting history changes every teammate's clone.
- **Commit metadata:** every Git commit records its author's name and e-mail address, and some team members committed
  with a personal address. That is visible to anyone who reads the history; it is not a credential. We do not rewrite
  history to change it (that would break every clone and every open pull request). New commits should use GitHub's
  private `...@users.noreply.github.com` address (GitHub > Settings > Emails > "Keep my email addresses private").

## 6. Dependencies: minimal, pinned, no known vulnerabilities

**Minimal.** `Packages/manifest.json` lists 20 entries. On 18 Nov 2026 23 were removed after checking that no code,
scene, prefab or asset uses them (NavMesh, Timeline, Cloth, Terrain physics, Tilemap, Video, XR/VR, Wind, Vehicles,
ScreenCapture, Accessibility, Adaptive Performance, Android JNI, Vector Graphics, Umbra, WebRequest audio/texture/WWW,
the Rider IDE package) or because something else already brings them (Terrain: URP's core package depends on it, so it
stays installed but is no longer listed by us). The CI tests (Edit Mode + Play Mode) and a Web build with the Photon
SDK proved the game still builds and passes. Kept on purpose:

| Package | Why |
|---|---|
| Universal Render Pipeline, Cinemachine, Input System, uGUI | Rendering, chase camera, keyboard/touch/gamepad, HUD |
| Test Framework | The tests |
| Visual Studio IDE package | Editor only, never in the build: script editing and debugging in Visual Studio / VS Code |
| Modules: animation, audio, imageconversion, imgui, jsonserialize, particlesystem, physics, ui, uielements, unitywebrequest | Used by the game or required by the packages above |
| physics2d | The Photon Fusion SDK calls `Physics2D` |
| assetbundle, unitywebrequestassetbundle | Keep the Photon SDK's compile options unchanged |
| unityanalytics | The Input System and URP editor code reference it; analytics stays switched off |

The Photon Fusion 2 SDK (2.1.4) is not in this repository (its licence forbids publishing it); CI fetches a fixed
copy from a private repository with a read-only key.

**Pinned.**
- Unity packages: exact versions in `manifest.json`, resolved versions and sources in `packages-lock.json`, all from
  the Unity registry or built in. Unity editor: `ProjectSettings/ProjectVersion.txt` (6000.3.28f1).
- GitHub Actions: every `uses:` is pinned to a full commit SHA (with the version as a comment).
- gitleaks in CI: a fixed release whose SHA-256 is checked before it runs.
- The test run's tools: the GameCI CLI at a fixed release (`cliVersion` in `tests.yml`) and the Unity Docker image
  by its SHA-256 digest, so a re-pushed tag cannot change what runs the tests.
- Python art tools (`tools/requirements.txt`): exact `==` versions. They only make textures and icons offline;
  the game, its tests and its build need no Python.
- The Security workflow's pinning check and `RepoHygieneTests` fail on any unpinned dependency.
- Dependabot (`.github/dependabot.yml`) opens a pull request each week when a pinned action or Python tool has a new
  release or a security fix, so pinning does not mean falling behind; each such PR runs the same tests.

**No known vulnerabilities (checked 18 Nov 2026).**
- GitHub Advisory Database: no advisory for any of the 8 actions we use.
- OSV / GitHub advisories for Python: Pillow 12.3.0 had 40+ advisories (image parsing), all fixed in 13.0.0, which
  is what `tools/requirements.txt` pins now (no known advisory). Playwright 1.63.0: none.
- Unity editor 6000.3.28f1 is newer than every fix Unity has published for the 6.3 line:
  CVE-2025-59489 (runtime argument injection on Android, Windows, macOS and Linux players; fixed in 6000.3.0b4; Web
  builds were never affected, [advisory](https://unity.com/security/sept-2025-01)) and the October 2026 Brotli decoder
  and eMudhra certificate advisories (fixed in 6000.3.27f2,
  [advisory](https://unity.com/security/unity-brotli-decoder-vulnerability)).
- Unity packages have no public advisory feed; they come only from Unity's registry at exact versions.

## 7. Limits (what this does not protect against) and how it could change

- **A fake but plausible lap time** can be posted by calling the API directly, because the public key must be able to
  insert. The CHECKs, the 40 %-of-Gold rule in the game and the rate limit make it small; the owner can delete rows.
  *Could change:* send the ghost recording with the time and let a Supabase Edge Function re-check the lap before
  inserting, or add sign-in so a name belongs to one person.
- **Names are not owned:** anyone can type "Ana". *Could change:* accounts (Supabase Auth).
- **The online race trusts each client's own kart position** (no server authority), so a modified client could
  teleport its kart or claim an early finish. The room only refuses malformed messages. Online results are shown
  in the room and then forgotten: nothing from an online race is saved, so no stored data can be falsified this way.
  *Could change:* Photon server/host authority with physics on the host.
- **The service_role check is a text check** on the decoded key (it looks for `"role":"anon"`). It protects against
  pasting the wrong key, not against a deliberately crafted one; only the team can set the secret.
- **Rude-word filtering is a short list** and can never catch everything.
- **If Supabase is down** the time stays on the device and the screen says "offline"; **if Photon is down** the lobby tries
  to connect 3 times, then shows the error; time attack and the race against bots still work (docs/online.md).
