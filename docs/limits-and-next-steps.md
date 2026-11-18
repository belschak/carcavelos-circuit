# Limits and how the project could change

What the game does not do, where it can break, and what we would build next. Each limit names the file or decision behind it, so it can be checked. How the parts work: [architecture.md](architecture.md).

## Limits we know about

### Online race (Photon)
- **100 players at once** on the free Photon plan, counted across every open game page (a forgotten tab counts too). Above that Photon refuses and the game says "SERVER FULL (TOO MANY PLAYERS ONLINE), TRY AGAIN IN A MINUTE". One room holds 15 players; the 16th sees "THE ROOM IS FULL (15/15), TRY AGAIN AFTER THIS RACE" (`Online/Rules/OnlineRooms.cs`).
- **No server decides anything.** Every game drives its own kart and reports its own lap time (Fusion Shared Mode). A modified client could report a fake time. Fine for a class race, not for prizes.
- **Karts pass through each other online** (no physics between players), on purpose: it keeps every player's own drive honest and simple.
- **Online has no bots, no ghost, no leaderboard saving.** These were built for one player and were switched off online rather than half-synced (`RaceModeRules`). Items do work online (since 17 Nov 2026), but only on your own kart: boxes are per player, and the others see you speed up through your synced position, not the item effect itself.
- **The host blocks the room if they walk away**; Q or closing the page hands the host role to the next player.
- **A browser tab in the background gets no frames**, so it drops out of the room after a few seconds and counts as DNF.
- **Slow or lossy Wi-Fi** shows as short freezes of other karts (never your own). Joining takes about 8 s and the game waits up to 35 s, retries 3 times, then says what failed. Measured in a 20-bot scale test on 17 Nov 2026: [online.md](online.md#limits).
- **The Photon SDK is not in the repo** (its licence forbids it). A fresh clone plays single player; the online mode says "ONLINE RACE NEEDS THE ONLINE BUILD" until the SDK is imported ([online.md](online.md#for-developers-the-online-race-in-your-local-editor)).

### Leaderboard (Supabase)
- **No accounts.** A name is whatever a player types (2 to 16 allowed characters, a short rude-word list). Two people can use the same name.
- **Fake but valid times are possible.** The public key can add any row that passes the CHECK rules (5 s to 30 min, clean name), for example from a script. The optional rate limit (`backend/supabase/002_rate_limit.sql`) slows that down; the game also hides times faster than 40 % of the Gold medal time (`LapTimeRules`).
- **A free Supabase project pauses** after about a week without requests. A daily GitHub Actions read (`.github/workflows/leaderboard-keepalive.yml`) keeps it awake; if it pauses anyway, the game shows "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE" until someone presses Restore in the dashboard.
- **A local build has no online board.** The two Supabase keys exist only as GitHub secrets, so a build made on a laptop keeps times on that device ("THIS DEVICE"); the deployed build is online.

### Game and browser
- **The short link depends on a mirror step.** `nova-kart.github.io` is a copy that the Web build workflow pushes after each `main` build. If that step fails or its deploy key is missing, the short link keeps the previous build while this repository's own Pages address has the new one (same game, one deploy behind).
- **Download about 50 MB** (after the lean-build work of 16 and 17 Nov). The first visit on slow Wi-Fi takes time; later visits come from the browser cache.
- **WebGL 2 and WebAssembly are required.** Old browsers or switched-off hardware acceleration get an error card that says so, instead of a black screen.
- **Saved data lives in one browser.** Best laps, ghost and settings are in `PlayerPrefs`; another browser, a private window or clearing site data starts fresh.
- **Bots do not use items**, and their skill comes from a light rubber band, not from learning.
- **One track.** The track list (`RaceSettings`) supports more, but only the Carcavelos campus is built.
- **Phones work, but the laptop is the main target**: the touch buttons are an HTML layer over the game (`Assets/WebGLTemplates/`), not Unity UI, and a 50 MB download on mobile data is heavy.

### Tests
- The tests check rules, not feel. Driving feel, how the campus looks, sound and frame rate are checked by hand and in recorded QA runs, not by tests.
- No test can reference Photon (the public CI has no SDK), so the Fusion adapter is checked by the CI Web build and by real multi-client runs, not by unit tests.
- The full list of what is and is not covered: [verification.md](verification.md).

### Team process
- Unity scenes do not merge, so every role owns its own scene and `Main.unity` is rebuilt by code. A change to the race layout still needs Unity, which one teammate's laptop cannot run locally (Windows Smart App Control blocks Unity's compiler there), so tests run on GitHub Actions.

## How it could change

| Change | How, with the current code |
|---|---|
| **Add a track** | Build a scene with a `TrackInfo`, `SpawnPoint` and ordered `Checkpoint`s (contract in [interfaces.md](interfaces.md#track-role-2-provides)), add its name to `RaceFlow/Settings/RaceSettings.asset` and the build list. Race flow, medals (scaled to track length), ghost and leaderboard (keyed by track name) work without code changes |
| **Add an item** | Add a value to `ItemKind`, a row of odds in `ItemOdds` (each row must add up to 100, a test checks it), its effect in `ItemSystem` through `IKartBoost`, and its look in `Visuals/ItemFx/` |
| **Add a driver** | A new entry in `DriverRoster` with its kart model; driver select, bots and online proxies read the roster |
| **Accounts instead of typed names** | Supabase Auth (email link or Google), then an RLS rule "a row's user_id must be the signed-in user". The game side stays the same `ILeaderboardService` |
| **Stop fake times** | Move the insert behind a Supabase Edge Function that checks the lap against the track (for example the ghost data or checkpoint splits) before writing; the public key then loses INSERT |
| **Server-decided online race** | Fusion Host or Server Mode instead of Shared Mode (one authority checks laps and collisions). Only `Online/Fusion/` changes; the rest talks to `IOnlineSession` |
| **Another online service** | Write a new `IOnlineSession` implementation (for example on Supabase Realtime); the lobby, rules and race control in `Online/` stay |
| **Bots that use items** | `BotDriver` already reads the race; it would call `ItemSystem` the way the E key does, with a rule in `BotRules` for when to use which item |
| **Class challenge** (whole class qualifies, top 8 race a final) | Specified but not built: [history/plans/2026-11-14-class-challenge.md](history/plans/2026-11-14-class-challenge.md) |
| **Change the public address** | The address is a copy target, not where the code lives: point the `mirror` job in `.github/workflows/web-build.yml` at another Pages repository (or a custom domain) and update the link at the top of `README.md`; the repository itself does not move |
