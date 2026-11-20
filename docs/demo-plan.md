# Demo plan: Carcavelos Circuit

The live demo of our team project, written along the course guide "Preparing the demo". The app runs at the deployed URL in front of the class: **https://nova-kart.github.io/** (the demo link). The old address https://belschak.github.io/carcavelos-circuit/ serves the same build (every merge to `main` publishes both). Slides or a video never replace the live app.

When and where: class TXC, 15:30, room D-110, on **Tue 24 Nov 2026** (presentations 1/2) or **Fri 4 Dec 2026** (2/2); half of the teams present each day. Which day is ours was not published yet when we last checked (course wiki "Course info" and the Moodle announcements, 20 Nov 2026): prepare for 24 Nov. Submission (code freeze for grading): Mon 23 Nov 23:59.

## 0. Presenter runbook (one page)

The rule of the course: the demo runs at the deployed URL. So we always start on the live site (plan A). Everything below exists so that no single failure ends the demo; each fallback was tried for real on 20 Nov 2026 (section 4).

**The presenter kit** on the demo laptop (and on the backup laptop): `python tools/preflight/presenter.py install` makes a folder with a copy of the deployed build (the exact files of the live site, from the GitHub Pages mirror, about 140 MB), a separate "demo Chrome" profile and three desktop shortcuts:

| Shortcut | What it does | When |
|---|---|---|
| **Carcavelos PREFLIGHT** | `tools/preflight/preflight.py`: GREEN / AMBER / RED for every dependency, each red line says the fix. About 10 s once the game is cached (the first run downloads the game: up to a minute) | 15 min before our turn, on the room's network |
| **Carcavelos LIVE** | The demo Chrome on https://nova-kart.github.io/ (own profile: no extensions, no bookmarks bar, no pop-ups, sound allowed before the first click) | Plan A, always first |
| **Carcavelos OFFLINE** | Starts a small local server and opens the same build from the laptop (http://127.0.0.1:8642/). Needs no internet. With internet it still has the online race and the online leaderboard (both tested from the laptop copy) | Plan B, when the live site or the network fails |

After the last merge (the day before): `presenter.py update` (newest build into the copy) and `presenter.py warm` (loads the live site once in the demo Chrome, so it opens from the cache). `presenter.py status` compares the copy with the live site. The plan D videos go into the kit folder too (`plan-b-video/`), so they play without internet.

**15 minutes before:** run PREFLIGHT. GREEN: open LIVE, click to play, leave it on the start screen. AMBER or RED: do what the line under it says, run PREFLIGHT again.

**If something fails during the demo** (say what happened, then switch; never restart in silence):

| What fails | What we see | Plan B | Plan C | Plan D |
|---|---|---|---|---|
| Classroom network slow or blocked | Loader stuck, online race "CONNECTING AGAIN", board OFFLINE | David's phone hotspot for the demo laptop | Carcavelos OFFLINE: race vs bots and time attack need no internet | The online race video (plan D) while we explain |
| GitHub Pages down, or a broken deploy | Error card, "THE GAME COULD NOT START", "The track did not download" | Carcavelos OFFLINE (same build from the laptop; say "the host is down, this is the same build served from this laptop") | Luc's laptop (preloaded tab or his own kit) | Video |
| Photon (online race) | "CONNECTION FAILED" after 3 tries, BACK | Phone hotspot, join again | Skip the online race, show race vs bots | The online race video |
| Supabase (leaderboard) | Badge OFFLINE, "TIMES FROM THIS DEVICE" | Nothing to do: SAVE still works on the device; say so | | |
| Demo laptop dies or freezes | | Luc continues at the same step on his laptop | | |
| Projector | Picture cut or tiny | Win+P > Duplicate or Extend; a 16:9 mode of at least 1280x720 | Press 1, 2, 3 for the modes (keys work even when a card is hidden) | |
| No sound | | Taskbar speaker: unmute, pick the speakers the room hears; in the game M = SOUND ON | Laptop speakers | |
| Stuck in a race we want to leave | A race has no MENU button before the finish | F5 (reload): back on the start screen in about 5-10 s from the cache | | |
| Slow frame rate | | Charger in, close other apps (the GPU slows down on battery) | Luc's laptop | |

Plan D (last resort, it does not count as the demo): the recorded online race of 17 Nov 2026 (`plan-b-video/` in the kit: the grid of all screens and a single screen, 1920x1080).

**The backup laptop (Luc):** the same kit (`presenter.py install`; on a Mac without shortcuts: `presenter.py live` / `presenter.py offline`), rehearsed once.

## 1. What we show, in this order (about 8 minutes)

| # | Workflow | What the class sees | Operates | Speaks |
|---|---|---|---|---|
| 1 | Open the game | Loader with progress and tips, "Click to play", start screen with the three mode cards, the leaderboard, NOON/SUNSET (T) | David | David: one link, no install, Unity Web build on GitHub Pages |
| 2 | Race vs bots | Kart builder (driver, kart, tyres: 64 sets), countdown, drive: drift (Space) and boost, an azulejo item box (E), a trick in the air (Shift), the five bots overtaking, position and minimap | David | Alessandro: kart controller, drift, camera, items. Luc: kart and driver models, the campus map, the bots' racing line |
| 3 | Race rules | Drive past a checkpoint, hit the finish: "Missed a checkpoint!", press R: back to the last checkpoint, the clock keeps running | David | Noah: track, checkpoints, `RaceRules`, recover |
| 4 | Finish and results | Finish camera, results card (place, medal, time, lap report), SAVE YOUR TIME: first type `<b>` and press SAVE (red "ONLY LETTERS, DIGITS, SPACE, - AND _"), then a real name: the row appears on the leaderboard | David | David: race flow, results, leaderboard, the three places the name rules run (form, rows coming back, database) |
| 5 | Time attack | Retry with Enter, the ghost of the best lap drives along, medal targets | David | David |
| 6 | Online race | Classmates open the same link on their phone or laptop, pick ONLINE RACE, type a name, JOIN. One room, 15 seats. David is host and presses START RACE; everyone counts down to the same GO; the finish order is the same on every screen | David hosts, Luc keeps the second laptop in the room | Luc: why Photon Fusion (no own server), what is sent (pose 15 times a second). David: one room, retries, what a full room says |
| 7 | When something fails | Only if it happens (or if we are asked): say what happened, show the message, continue (section 4) | whoever operates | whoever knows the part |

Rules for the demo: one Chrome window, only the game tab (and the backup tab on the second laptop). No dashboards, no `.env`, no agent window on the projected screen.

## 2. Who does what

- **Operator:** David on his laptop (knows every flow, hosts the online room).
- **Backup operator:** Luc on his laptop with the game preloaded in the same order. If David's laptop fails, Luc takes over at the same step. Rehearse once with Luc operating, so the demo never depends on one person.
- **Speakers:** each member explains the part they built (table above) and can answer for any other part (the repository walkthrough asks anyone about anything: read `docs/explain/`).
- **Phones for the online race:** Alessandro and Noah join from their phones first, so the room is never empty even if the class is slow to join.

## 3. Starting data

| Thing | At the start of the demo | How |
|---|---|---|
| Leaderboard (online, Supabase: the board shows "ONLINE") | Times of the team and the rehearsal with our first names, no test rows (`P3 ...`, `AAAA...`, `QA ...`) | The day before: Supabase Table Editor > `lap_times`, delete test rows. Never save test names on the live board |
| Leaderboard on this device (the fallback) | Not empty, in case the online board is down | The day before, on the demo laptop's Chrome profile: drive and save one lap each; every save is also kept in that browser |
| Personal best and ghost | One real lap, so time attack shows a ghost | Drive one clean lap in time attack the day before |
| Settings | Sound on, music at a level the room can hear, NOON | Settings (O) before the demo |
| Online room | Empty, nobody left in it from rehearsals | Close all rehearsal tabs; a forgotten tab is a player |

## 4. What the demo depends on, and what we do when it fails

Every row was tried on purpose on the live site on 18 Nov 2026 (blocked hosts, network off before and during a race, slow 3G, WebGL off, blocked build files) in a separate test browser; the messages in the third column are what the app showed.

Tried again on 20 Nov 2026 on the laptop copy of build `20cb18328164` (main `b0b3376`) with `tools/preflight/fallback_check.py` (its own headless Chrome, a fresh profile per case, screenshots read by hand):

| Case | How it was simulated | Result |
|---|---|---|
| No internet at all | Chrome resolves no host name; only the laptop server (an address, not a name) answers | Game loads in 10-18 s with a cold cache, board OFFLINE + "TIMES FROM THIS DEVICE", race vs bots runs, a reload comes back, ONLINE RACE: 3 tries, message, BACK, race vs bots runs again; no request left the laptop except the failed board read |
| Photon blocked, rest of the internet fine | Photon's host names resolve to nothing | Same message + BACK within about 10 s, race vs bots afterwards |
| Leaderboard database down | Every Supabase request answers HTTP 500 | Badge OFFLINE, race runs, 0 game errors |
| Live site down, internet fine | Two players on the laptop copy (http://127.0.0.1:8642/) | Both joined the class room on Photon, same GO (skew 12-21 ms), "1ST / 2" on screen, the other player gone from the room after its tab closed; the online board answers from the laptop copy too |
| A player loses the network mid-race | Two players racing, one goes offline | The other keeps racing; the dropped one retries 3 times, then the message |
| Preflight with no internet | `preflight.py --simulate-no-internet` | Live site RED, then it boots plan B by itself: game GREEN, board AMBER (expected offline), online race RED with "phone hotspot" |

| Dependency | What goes wrong | What the app shows (tested) | Check before | Fallback |
|---|---|---|---|---|
| **GitHub Pages** (host) | Old version deployed; the first visit downloads 52.7 MB: about 15 s at 50 Mbit/s, 48 s at 10 Mbit/s, measured 2-7 min on a weak 1-3 Mbit/s Wi-Fi, 4.4 min on fast 3G, 14 min on slow 3G | Loader with progress, tips and the key card; a failed download shows an error card with RELOAD THE PAGE | Actions > Web build: the run for the last merge on `main` is green incl. the `pages` and `mirror` jobs; `https://nova-kart.github.io/version.json` (and `https://belschak.github.io/carcavelos-circuit/version.json`) show the build id that the deploy log printed (an open page with an older build reloads itself once) | Preload the tab before our turn: a second visit loads from the browser cache in 8-20 s. Never cold-load on the classroom Wi-Fi. Right after a deploy the old page can point at removed files for up to 10 min ("THE GAME COULD NOT START"): no merges on the day; if it appears, reload after a minute |
| **The campus world file** (second download, load time 18 Nov) | The world file `Build/world-<hash>.bundle` failed to download twice (a dropped connection, or an old open page asking for a world file that a deploy replaced) | "The track did not download" ("Check the internet connection, then reload the page.") with Reload the page; the game never falls back to the test track (tested with the world file blocked on a local copy of the build, 18 Nov) | Same as GitHub Pages above: the preloaded tab already holds the world; no merges on the day | Reload the page: the game half comes from the browser cache, only the world downloads again. If it fails again: as in the GitHub Pages row, the backup laptop's preloaded tab (Luc); a bad classroom network: the phone hotspot (Classroom Wi-Fi row) |
| **Projector resolution** | A very small window (below 800x600) crowds the start screen (4:3 and 5:4 get the compact layout since #164/#168) | | Connect to the projector in the week before; use "Extend" or a 16:9 mode of at least 1280x720 (tested clean: 1280x720, 1280x800, 1366x768, 1440x900, 1536x864, 1920x1080) | Press 3 for the online race (the key works even when the card is covered) |
| **Browser / GPU** | WebGL 2 off, hardware acceleration off, an old browser | "THIS BROWSER CANNOT SHOW THE 3D TRACK" with what to switch on | Demo laptop: current Chrome, hardware acceleration on, charger plugged in (GPU throttles on battery) | Backup laptop (Luc) |
| **Photon Cloud** (online race) | Region down, classroom network blocks WebSockets, 100 players at once (free plan), the room is full (15) | "CONNECTING AGAIN (TRY 2 OF 3)", then a red message plus "RACE VS BOTS AND TIME ATTACK WORK WITHOUT THE INTERNET" and BACK; room full and server full have their own messages | Join the room from two devices on the deployed app the day before AND in the room before the class (step "On the day") | Say what happened, press BACK, continue with race vs bots (works without internet). If only the class Wi-Fi is the problem: David's phone hotspot for the demo laptop |
| **Classroom Wi-Fi** (eduroam) | Slow, drops, blocks ports | Our own kart never freezes; other karts may pause briefly; a lost connection retries 3 times, then the message above | Test the online race on eduroam on campus in the week before (WebGL uses WebSocket on port 443) | Phone hotspot for the demo laptop; single-player modes need no internet once the page is loaded |
| **Classmates' phones** | Slow first download on mobile data, a phone held upright, an old phone | Phone loader with the touch card; touch buttons in landscape | Give the link (QR code on a printed card or a phone, the link on the board) at the start of our turn, so the download runs while we show steps 1-5 | The online race also works with only our four devices |
| **Supabase** (online leaderboard) | The free project pauses after about a week without requests; the database is down or slow (6 s timeout) | Badge "OFFLINE" and "LEADERBOARD OFFLINE - TIMES FROM THIS DEVICE" on the start screen, "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE" after a save; SAVE still works (tested with the requests blocked and with HTTP 500) | The day before: Actions > the daily leaderboard keep-alive run (06:17 UTC, `.github/workflows/leaderboard-keepalive.yml`) is green, and the Supabase dashboard shows the project active, not paused | Continue: say "the database is not reachable, the game keeps the time on this device" |
| **Secrets on screen** | Photon or Supabase dashboard, GitHub settings or a `.env` visible on the projector | | Close every dashboard, settings page and the coding agent before sharing the screen | |
| **One member absent / laptop dead** | | | Backup operator has the tab preloaded and rehearsed once | Luc operates from step where David stopped |

No AI model, no user accounts and no paid API are involved, so there is no quota to burn in rehearsal and no demo account to remember. The only "manager" accounts are David's GitHub, Photon and Supabase dashboards; nobody logs in during the demo.

If something fails during the demo: say what happened, point at the message, continue with the next step. Explaining why the app shows a fallback message is a better demo than restarting in silence. Last resort, if neither laptop can run the game: the recorded online race from 17 Nov shows the feature while we explain, but it does not count as the demo.

## 5. Invalid input we can show on purpose (all tested live)

- Name `<b>` > "ONLY LETTERS, DIGITS, SPACE, - AND _"; empty > "TYPE A NAME FIRST"; `A` > "AT LEAST 2 CHARACTERS"; a rude word > "PLEASE PICK ANOTHER NAME"; more than 16 characters cannot be typed.
- The same rules in the online lobby and in the link (`?name=<script>...` opens the lobby with the message, never joins).
- Skipping a checkpoint > "Missed a checkpoint!", no finish.
- A link with garbage flags (`?autostart=abc&driver=99`) still loads the game.

## 6. Known rough edges (avoid on stage until fixed)

Found in the failure QA on 18 Nov 2026; none freezes the game. Remove a line once its fix is live.
- Name fields (results and online lobby): after typing, Esc does not leave the field and Q does nothing. Use the mouse (SAVE, BACK).
- Online race on a laptop: the key hint bar still shows ENTER RETRY and P PAUSE, which do nothing online. Do not press them.
- When Photon cannot be reached the message reads "CONNECTION FAILED: CONNECTION FAILED (Error)". Say: "Photon is not reachable from this network, the game tells you and offers BACK".
- A race has no way back to the start screen before the finish (Q MENU is only on the results card, P only pauses). To switch modes mid-race: F5, the page comes back from the cache in a few seconds.

## 7. Checklists

### The week before (by Sun 22 Nov, feature freeze)
- [ ] Everything we show is merged to `main` and live (Actions > Web build green incl. `pages`).
- [ ] Run the whole demo on the deployed URL, not in the Editor, in the order of section 1.
- [ ] Online race on eduroam on campus with at least 3 devices, one of them on mobile data.
- [ ] Try the failures on purpose (section 4): Wi-Fi off after loading (single player keeps working), Wi-Fi off in the online lobby (message + BACK), invalid names.
- [ ] Tests green on `main` (GitHub Actions "Tests"); every member can say what they cover and what not (`docs/verification.md`).
- [ ] Repository: public, README has the URL, the members and the commands; no credentials (`docs/security.md`).
- [ ] Every member has read `docs/explain/` for the parts they did not build.
- [ ] QR code card for the URL printed (or on a phone).

### The day before (Mon 23 Nov or Thu 3 Dec)
- [ ] Rehearse once from start to finish on the deployed URL, with David operating; once with Luc operating.
- [ ] Photon dashboard: app active, CCU well below 100. Supabase: project not paused, test rows deleted, the board shows "ONLINE" on the live site.
- [ ] Reset the starting data (section 3) on both laptops after the rehearsal.
- [ ] Stop merging. Nothing new goes to `main` after the rehearsal (after Mon 23 Nov 23:59 it would not be graded anyway).
- [ ] Charge both laptops and the phones; David's phone hotspot works.
- [ ] Presenter kit on both laptops: `presenter.py update`, then `presenter.py warm`; PREFLIGHT all green at home; open Carcavelos OFFLINE once with Wi-Fi off (it must reach the start screen and race vs bots).
- [ ] Restart Chrome once (a waiting update otherwise installs itself at the worst moment).

### On the day
- [ ] 15 min before: Carcavelos PREFLIGHT on the room's network (both laptops). Then Carcavelos LIVE, click to play once, so the game is loaded and cached; leave it on the start screen.
- [ ] Join the online room from one laptop and one phone, then leave again (the room is empty for the demo).
- [ ] One Chrome window, only the game tab, full screen (the button top right), sound on. Close dashboards, `.env` files and the agent.
- [ ] Hand out the link (QR card, link on the board) at the very start, so phones download while we show steps 1-5.
- [ ] If something fails: say it, show the message, continue.
