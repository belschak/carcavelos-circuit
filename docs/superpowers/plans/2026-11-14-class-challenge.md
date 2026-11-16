# Class Challenge: everyone qualifies, the best 8 race the final

Status: **proposal, needs the team's OK** (Luc, 2026-11-14). Nothing here is built yet.
Research behind the network choice: Photon Fusion 2 comparison of 14 Nov (Render, Unity Relay, Supabase Realtime and others were compared; summary in "Why Photon" below).

## Goal
At our presentation (Tue 24 Nov or Fri 4 Dec, in class, about 50-60 students) **every student plays on their own Mac** from one link, and the 8 fastest race each other live on the projector. Nobody installs anything.

If the online final fails on the day, the challenge still works: the fastest qualifying time wins.

## What the players see
1. **We start it.** On the projector laptop: menu > **Class Challenge** > team PIN. The projector shows the link and a short code, e.g. `https://belschak.github.io/carcavelos-circuit/?event=NOVA24`, plus the live top 10.
2. **Qualifying (everyone).** A student opens the link, types a name and drives the normal one-lap time trial (solo, with bots/ghost as today). Every finished lap is saved with the event code. Laps **slower than 1:30.00 do not qualify**: the student sees "Too slow to qualify (1:42.10), the limit is 1:30. Try again!" and can keep driving. Nobody is thrown out.
3. **We close qualifying** (button on the projector, PIN again). The game takes each player's best qualifying lap and picks the **top 8**.
4. **Forwarding.** Within a few seconds the 8 finalists' screens show "You're in the final! Grid position 3" and they join the final automatically, with no new link. Everyone else sees "The final is on, watch the projector" and can keep practising.
5. **Final.** 8 karts, **2 laps**, grid in qualifying order (fastest on pole). Karts drive through each other (no kart-to-kart collisions), no bots, no items that hit others. The results screen shows the same finishing order on all 8 screens and on the projector. The winner gets the prize.

## How it works
| Part | Where | What |
|---|---|---|
| Game files | GitHub Pages (unchanged) | Same Web build for solo, qualifying and final |
| Event state + lap times | Supabase (the R10 project) | Who is in which event, which phase, the 8 finalists |
| Final race (live karts) | Photon Fusion 2.1, Shared Mode, region `eu` | Only the 8 finalists, only during the final |

### Supabase (new file `backend/supabase/003_class_challenge.sql`)
- `lap_times` gets two optional columns: `event` (code, `^[A-Z0-9]{4,8}$`) and `device` (random id the browser creates once and keeps in PlayerPrefs, so two players called "Max" stay apart). Old rows and solo laps leave both empty.
- New table `events`: `code`, `phase` (`quali` | `final` | `done`), `cutoff_seconds` (90), `finalists` (up to 8 rows of device, name, best time, grid position), `created_at`. The public key may **read** it, never write it.
- The two team buttons call **functions** (`event_start(pin)`, `event_close_quali(code, pin)`) that check the PIN against a hash stored in a private table. **The PIN is set in the Supabase dashboard and is never in the repo.** `event_close_quali` computes the top 8 itself in the database (best lap per device, only laps <= cutoff, ties by the earlier lap), so a player cannot pick the finalists.
- **Rate limit (002) must be raised for the event:** it refuses new rows once everybody together posted 60 in a minute. 60 students lapping every ~70-90 s is about 40-50 laps a minute, too close. Proposal: 300 per minute for everybody, the per-name limit stays 5.

### Game
- `?event=CODE` in the URL (read through `WebPage` like the name prompt) starts qualifying directly after the name.
- Every few seconds (5 s) the game reads its event row. 60 laptops = about 12 small reads a second, fine for the free plan. Phase `final` + own device in `finalists` = join the final; otherwise show "watch the projector".
- `?board=CODE` = projector view: big live top 10 of the event, team buttons behind the PIN.
- Final: `RaceManager.LapCount` is hard-coded to 1 today ("a time trial is one lap"); the final needs 2 laps (lap counter in the HUD already shows when > 1). Grid from qualifying order, `RacerGrid` needs 8 slots for humans.
- Online karts: each player's own kart drives locally exactly as today (Alessandro's `KartController` unchanged). Photon only carries its pose and the `IKartState` values (speed, drift stage, boost, grounded, steer). The other 7 karts are visual-only proxies that implement `IRacer` + `IKartState`, so positions list, minimap, name tags and kart effects work for them without new code.
- Start: the Photon master client sets one shared start time; every kart unlocks itself at that time. Finish: each kart reports its 2-lap time once; the master accepts one result per player, sorts by time (tie: player id) and publishes the order. A player who leaves is DNF.
- Final times are **not** written to the leaderboard (they are reported by the clients and are only for this race).

## When something fails
| Fails | What players see | Plan |
|---|---|---|
| Photon (campus Wi-Fi blocks it, final does not connect within 30 s) | "The final could not connect, the fastest qualifier wins" | Projector shows the qualifying podium |
| Supabase down | Event link: "Class Challenge is offline right now, here is the normal time trial" | Solo game keeps working, times stay on the device (existing fallback) |
| A finalist leaves mid-race | DNF in the results | Race goes on |
| Bad or old event code in the link | "This challenge does not exist or is over" + normal time trial | |
| Loading 60 x ~148 MB at once | Slow start | Send the link before class so browsers cache the game; finish the texture diet (V24 #102) |

## Why Photon (and not Render)
- Render free sleeps after 15 min idle and takes about a minute to wake: a cold first join can freeze the live demo, and we would have to write our own race server.
- Supabase Realtime: free limit 100 events/s; 8 karts at 10 updates/s already need about 640. Fine for the event state, not for live karts.
- Unity Relay: works, but a browser tab is the host and its leaving ends the race for everyone.
- Photon Fusion Shared: managed rooms, WebGL supported, free up to 100 players at once, region Amsterdam (~35-60 ms from Lisbon).

## Tests (Edit Mode, plain C# rule classes like the rest of the project)
- Event code: `nova24 ` -> `NOVA24` accepted; empty, 3 or 9 characters, spaces inside, symbols rejected.
- Qualifying cutoff: 89.99 and 90.00 qualify, 90.01 does not; impossible times rejected (existing `LapTimeRules`).
- Top 8: best lap per device; a device with 5 laps counts once; fewer than 8 qualified -> only those; nobody qualified -> no final, message instead; tie -> earlier lap first.
- Final order: sorted by time, tie by player id; a second finish report from the same player is ignored; leaver = DNF at the end.
- Two laps: finish only after the second crossing; driving backwards over the line does not count (existing `LapProgress` rule).
- Play Mode: two simulated finishers end the final with the same order on both.
- Not testable automatically (goes into `docs/verification.md`): campus Wi-Fi, Safari/Chrome/Firefox mix, 8 real Macs. Checked by hand in the rehearsal.

## Order of work (each step works without the next)
| When | Step | Who (proposal) |
|---|---|---|
| Sun 15 | Supabase project live (unblocks R10) + secrets as CI secrets | David |
| Mon 16 evening | **Photon go / no-go:** a moving cube in the deployed build, two Macs (Chrome + Safari) on Nova Wi-Fi, second Mac joins through `?room=` link. No-go = we drop the live final, the rest of this plan stays | Luc |
| Wed 18 | Qualifying on `main`: 003 SQL, event link, device id, cutoff message, projector board, PIN buttons, top-8 selection | David (board, SQL), Luc (URL, device id) |
| Thu 19 | Final: lobby/forwarding, shared start, 2 laps, 8-slot grid, finish order | Luc (Photon, kart proxies), Noah (2-lap rule + grid), Alessandro (kart waits for the shared start), David (results screen) |
| Fri 20 | Rehearsal on campus: 8+ laptops, swap who opens the projector, one finalist leaves on purpose, a wrong code | all |
| Sun 22 | Freeze: the tested build is the `main` commit in `docs/builds.md` | Luc + David |

## Open questions for the team
1. Is the 1:30 a **lap time** limit (this plan) or should qualifying simply **end after 1:30 minutes**?
2. Our presentation day and slot length. The challenge needs about 15 min (link, ~10 min qualifying, final). With a shorter slot, start qualifying before our slot or in the break.
3. Prize?
4. Projector during the final: one finalist's screen (easy) or a spectator camera (extra work, only if time is left)?
5. Photon SDK in a public repo: check the licence before importing; list it in `THIRD_PARTY.md`.
