# Walkthrough Q&A

Short answers to the questions the teaching staff asks in the repository walkthrough. Any member can be asked about any part, so every member should be able to say each answer in their own words and point at the file. Longer explanations: [architecture.md](architecture.md), [limits-and-next-steps.md](limits-and-next-steps.md).

## The product

**What is it, in one sentence?**
A 3D kart racer on a model of Nova SBE's Carcavelos campus that runs in the browser: race five bots, chase your own ghost in Time Attack, or race the whole class live in one online room.

**Why did you build a game and not a data app?**
We picked "start a new application" from the team project page. A game shows every grading point in a way the class can try live: invalid input (names, URL flags, a lost connection), clear feedback, and real multi-user interaction.

**Why Unity and not Python with Streamlit, like the course?**
The game needs three things the course's stack guide names as Streamlit's weak spots: 3D with physics at 60 frames a second (Streamlit redraws after each click and has no game loop), real-time interaction between users (15 players in one live race), and a mobile layout (touch controls on phones). Unity gives physics, a camera system, a Web export and a test runner. The guide says a language other than Python is agreed with the staff case by case; we asked through the Moodle question form on 7 Nov (task G1). Current status: [architecture.md](architecture.md#stack-approval).

## How it is built

**What are the main parts?**
One folder per module under `Assets/_Project/`: `Driving` (the kart), `Track` (checkpoints and lap rules), `Visuals` (campus, karts, bots, effects), `RaceFlow` (the game around the track: menus, race states, HUD, items, ghost, leaderboard), `Audio`, `Ambience` (waves and surfers), `Online` (online race), `Shared` (the contracts), `Tests`. Table: [architecture.md](architecture.md#modules-one-folder-one-owner-one-assembly).

**How do the parts talk to each other?**
Only through small interfaces in `Shared/`, for example `IKartState` (speed, drift, boost, grounded) and `IKartDrivable` (who steers the kart). Each module has its own assembly, so Unity refuses a forbidden reference. That is why a bot can drive the same kart as the player: it is just another input source.

**What happens from opening the link to the finish?**
The page checks the browser and loads the game; `RaceBootstrap` loads the campus track scene and the karts; the start screen offers three modes; `RaceStateMachine` runs Ready, Countdown, Racing, Results; every checkpoint crossing goes through `RaceRules`; at the finish the time is checked and saved. Step by step: [architecture.md](architecture.md#what-happens-when-you-open-the-link).

**How do you stop someone from cutting the track?**
`Track/RaceRules.cs` only counts a lap when every checkpoint was crossed in order. A skipped, repeated or backwards crossing is refused, and the race does not finish early. Recover (R) puts the kart back at the last valid checkpoint without giving progress. Edit Mode tests cover each of these, and a Play Mode test drives a robot through the real track.

**How do the bots work?**
Five bots, each a normal kart with a `BotDriver` as its "keyboard". Every decision is plain maths in `Visuals/Bots/BotRules.cs`: steer toward a point ahead on the racing line (pure pursuit), slow down before sharp bends, back up when stuck. No machine learning, so every rule can be explained and tested.

**How does the online race work?**
Photon Fusion 2 relays it. Each player's game drives its own kart and sends its position 15 times a second; the other karts are drawn slightly in the past and blended, so they move smoothly. The first player in the room is the host and starts the race; all screens count down to the same start time; the finish order is sorted the same way on every screen. Details: [online.md](online.md).

**How is it deployed?**
Every merge into `main` starts a GitHub Actions workflow that builds the Web version in Unity and publishes it to GitHub Pages, at the short public link (`nova-kart.github.io`, a copy made by the same workflow) and at this repository's own Pages address. Only `main` can deploy, so the code on `main` is the code that is live.

## Data

**Where is the data stored?**
- On the player's device, in the browser (Unity `PlayerPrefs`): best laps, the ghost of the best lap, settings, chosen mode and driver, the device leaderboard.
- Online, in one Supabase (Postgres) table `lap_times`: name, time, track, date. Only when the build has the two Supabase secrets; otherwise the board keeps times on the device and its header says "THIS DEVICE".
- Nothing about the online race is stored: Photon only passes messages between the players in the room.
- No accounts, no emails, no passwords.

**Who can change the online leaderboard?**
The game's public key may only read rows and add rows. Row Level Security and CHECK rules in `backend/supabase/001_lap_times.sql` refuse anything else: no update, no delete, names of 2 to 16 allowed characters, times between 5 s and 30 min. The game checks the same rules before sending (`PlayerNameRules`, `LapTimeRules`) and again on every row that comes back.

**Where are the keys?**
In GitHub Actions secrets only. The build writes them into the game at build time; they are never committed. The Supabase key in the game is the public one, safe because the database rules decide what it can do. Security details: [security.md](security.md).

## When something fails

**What if Supabase (the leaderboard) is unavailable?**
The time is always saved on the device first. If the online request fails or times out (6 s), the board shows the device's list with "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE". The race itself never waits for the leaderboard.

**What if Photon (the online race) is unavailable?**
Only the Online Race mode needs it; Race vs Bots and Time Attack keep working. The lobby shows what happened in plain words, for example "NO ANSWER FROM THE SERVER (TIMEOUT), CHECK THE WI-FI", "SERVER FULL (TOO MANY PLAYERS ONLINE), TRY AGAIN IN A MINUTE" or "THE ROOM IS FULL (15/15)". A failed or lost connection is retried by itself up to 3 times ("CONNECTING AGAIN (TRY 2 OF 3)"). Our plan B for the presentation is a recorded online race, shown only if Photon is down.

**What if the player types something invalid?**
- A name that is empty, too short, too long, has symbols or emoji, or is on the rude-word list: the save form says so ("TYPE A NAME FIRST", "PLEASE PICK ANOTHER NAME") and nothing is sent.
- Odd values in the page address (`?room=`, `?name=`, `?autostart=`): unknown ones are ignored, every value is cut to 200 characters, a bad name opens the name field instead (`Online/Rules/OnlineUrlFlags.cs`).
- An impossible lap time (a teleport or a bug): not saved.

**What if the browser cannot run the game?**
The page shows an error card that says why (no WebGL 2, the download failed) and what to try, instead of a black screen.

## Tests

**What do the tests cover?**
Over 1,300 Edit Mode tests on the rules in plain C#: lap validation, race states, timer, medals, item odds, bot decisions, name and time rules, leaderboard fallback, online room rules, pose blending, finish order, URL flags. 37 Play Mode tests start the real game scene and let a robot drive: a clean lap, a skipped checkpoint, recover, retry, bots, items, menus. Each rule has accepted, rejected and edge-case tests. They run on GitHub Actions. Coverage map: [verification.md](verification.md).

**What do they not cover?**
How the driving feels, how the campus looks, sound, frame rate on a slow laptop, and the real Photon connection (the SDK cannot be in the public CI). We checked those by hand and with recorded QA runs, including a 20-bot online scale test.

**How do I run them?**
See "Run the tests" in the [README](../README.md#run-the-tests).

## The team

**Who built what?**
Everyone commits their own code from their own account; the roles split the folders so nobody overwrote anyone.

| Member | Role | Main contributions |
|---|---|---|
| Alessandro Rossi | 1 Driving + camera | `KartController`: driving, drift that charges a boost, ramp jump, flying pose, trick after a jump, recover; Cinemachine chase camera; name tags; `IKartDrivable` so bots can drive the kart |
| Noah Schlag | 2 Campus track + race rules | Route from OpenStreetMap ([route.md](route.md)), `RaceRules` lap validation with tests, `TrackInfo` / `Checkpoint` / `SpawnPoint`, `TrackProgress` (distance along the lap), guardrails, Nova hall interior, atrium piano |
| Luc Tippmann | 3 Kart, visuals + web release | The campus world (generated from map data, Street View and photo surveys), kart and driver models, the five bots, item effects, three race modes and the finish cinematic, the lean Web build, GitHub Pages deploy, driver select |
| David Belschak | 4 Race flow, UI + sound | Project setup and contracts, race flow, HUD and menus, sound and music, items, ghost, medals, leaderboard (Supabase + device fallback), positions and minimap, waves and surfers, touch controls, the Web page, CI, the online race |

Every pull request names its role and task; the full history is in the merged pull requests and [TASKS.md](../TASKS.md).

**How did you work as a team with AI assistants?**
One rules file for every person and every AI: [AGENTS.md](../AGENTS.md) (Git workflow, who owns which folder, contracts, what never to touch). Every change goes through a pull request that a teammate reviews. Decisions with their reasons are logged in [decisions.md](decisions.md).

## Limits and change

**What are the limits?**
The biggest ones: no server decides the online race (each game reports its own time), no accounts on the leaderboard, a free Supabase project pauses after a week, 100 players at once on the free Photon plan, one track. Full list: [limits-and-next-steps.md](limits-and-next-steps.md).

**How would you add a new feature, for example a second track or accounts?**
A second track: a new scene with the `TrackInfo` contract and one line in `RaceSettings`; race, medals, ghost and leaderboard work unchanged. Accounts: Supabase Auth plus one RLS rule; the game side keeps the same `ILeaderboardService`. More: [limits-and-next-steps.md](limits-and-next-steps.md#how-it-could-change).
