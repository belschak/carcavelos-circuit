# Verification: what the tests cover, and what they do not

Graded criterion 4: "Tests cover accepted and rejected actions and edge cases, and all pass on `main`."
This page is the map from every workflow of the game to the tests that check it, the commands to run them, and an
honest list of what no automated test covers.

[![Tests](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml?query=branch%3Amain)
[![Security](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml?query=branch%3Amain)

## Running the tests

**On GitHub (the reference).** `.github/workflows/tests.yml` runs every Edit Mode and Play Mode test in Unity
6000.3.28f1 (GameCI Linux image) on every pull request into `main` (except pull requests that change only docs), on
every push to `main` and on branches named `test/...`. Every commit on `main` gets its own finished run, which is what
the badge shows. The team merges only pull requests whose run is green; this is our practice, not enforced by a GitHub
branch rule. The run page shows "passed / total" per mode; the NUnit XML files are in the run's `test-results` artifact.
All runs: <https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml>

**On your computer.** You need Unity 6000.3.28f1 with the **Web Build Support** module (Unity Hub > Installs >
Add modules): the build scripts use `UnityEditor.WebGL`, so the project does not compile without it. Open the project
and use Window > General > Test Runner (Edit Mode tab, then Play Mode tab, "Run All"). Or from a terminal, with Unity
closed:

```bash
cd carcavelos-circuit
"<path to Unity 6000.3.28f1>/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile -
"<path to Unity 6000.3.28f1>/Unity" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile -
```

On Windows the Unity path is usually `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`, on macOS
`/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity`. Exit code 0 means every test passed.
(On some Windows laptops Smart App Control blocks Unity's script compiler; then use the GitHub run, which is why it exists.)

## Where the tests live

| Folder | Assembly | What it tests |
|---|---|---|
| `Assets/_Project/Tests/EditMode/` | Tests.EditMode | Rules of the race, leaderboard, settings, items, UI text, page template (no scene needed) |
| `.../EditMode/Driving/` | Tests.Driving | Kart input, drift, tricks, name tags |
| `.../EditMode/Online/` | Tests.Online | Online room rules, message codecs, the in-memory test session |
| `.../EditMode/Visuals/`, `.../EditMode/WebBuild/` | Tests.Visuals, Tests.WebBuild | Kart effects, Web build import rules |
| `.../EditMode/Verification/` | Tests.EditMode | Database rules vs game rules, keys out of Git, pinning, tampered saves, bad keys and tracks |
| `.../EditMode/Drivers/` | Tests.EditMode | Driver roster, kart sets (`KartLoadoutTests`), building a kart from a set (`KartAssemblerTests`) |
| `Assets/_Project/Tests/PlayMode/` | Tests.PlayMode | The real track scene: a full lap, retry, bots, items, leaderboard form, menus, kart builder, teacher tribute |

Most logic sits in small plain C# classes (`...Rules`, `...Codec`, `...Format`) so it can be tested without a scene;
the MonoBehaviours only connect them to Unity. Play Mode tests then check that the pieces work together in the real scene.

## Coverage map

"Accepted" = a valid action works. "Rejected" = an invalid action is refused or bad input is handled without a crash.
"Edge" = boundaries (exactly the limit, one over), empty, NaN or infinity, duplicates, timing, failure of an outside service.
Test files are in `Assets/_Project/Tests/`; PlayMode files are marked (PM). Counts are test runs (each `[TestCase]` counts).

### 1. Time attack (lap timing, checkpoints, wrong way, recovery, retry, medals, personal best, ghost) about 240 runs

- **Accepted:** `RaceRulesTests` (in order then finish), `RaceTimerTests` (lap time from timestamps), `RaceStateMachineTests` (3-2-1-GO, retry), `MedalRulesTests`, `PersonalBestTests`, `GhostRecordingTests`, `GhostLoaderTests`, `WrongWayDetectorTests`, `StuckDetectorTests`, `FallCheckTests`; (PM) `RaceSmokeTests.CleanLap_FinishesOnce_WithATime_...`, `Retry_ResetsTimerAndCheckpoints_...`, `RaceModesPlayTests.BackToMenu_FromTheCountdownTheRaceAndThePause` (Q leaves an unfinished lap for the start screen, nothing stays frozen; `ExternalStartRulesTests.Q_GoesBackToTheMenu_...`).
- **Rejected:** finish right after the start, missed or backwards checkpoints, same checkpoint twice, anything after the finish (`RaceRulesTests`); Q on the start screen or while the online lobby owns the race (`ExternalStartRulesTests.Q_IsRefused_...`); finish outside Racing (`RaceStateMachineTests`); negative frame times (`RaceTimerTests`); broken or foreign-road saves give "no record" (`PersonalBestTests.BrokenSave_MeansNoRecord`, `RecordFromAnotherRoad_IsDropped_...`); broken ghosts fall back to the shipped one (`GhostLoaderTests`); (PM) `SkippedCheckpoint_DoesNotFinish_AndRecoverKeepsTimeAndProgress`.
- **Edge:** exactly on a medal time, one millisecond slower; a 1-checkpoint track; long frames during the countdown; NaN, infinity and zero lap length (`LapProgressTests`); a save written on a Portuguese laptop (comma); a lap longer than the ghost budget.

### 2. Race against bots (grid, standings, bot driving, finish order) about 150 runs

- **Accepted:** `RacerGridTests`, `RaceStandingsTests`, `RacerRegistryTests`, `BotRulesTests`, `TestBotSteeringTests`, `DriverRosterTests`; (PM) `BotRacePlayTests.FiveBots_DriveAFullCampusLap_WithoutGettingStuck`, `RacersPlayTests`, `DriverSelectPlayTests`.
- **Rejected:** null or duplicate racers (`RacerRegistryTests`), bad numbers into the bot rules never steer or throw (`BotRulesTests.Steer_BadInput_DoesNothing`, `Stuck_BadNumbers_...`), the player never gets a name tag over their own kart.
- **Edge:** exact ties keep their order, null lists and no track (`RaceStandingsTests`), more racers than an online room, zero steering angle.

### 3. Online race (one room of 15, start, finish order, messages, leave) about 160 runs

- **Accepted:** `Online/LoopbackSessionTests` (two sessions share a room, the whole class meets, host moves on), `OnlineStartRulesTests`, `FinishOrderRulesTests`, `OnlineRaceLimitRulesTests`, `KartPoseCodecTests`, `RaceControlCodecTests`, `PoseBufferTests`, `OnlineLobbyViewTests`; (PM) `RaceModesPlayTests.Online_TheLobbyStartsResetsAndPlacesTheRace_KeysDoNothing`.
- **Rejected:** the 16th player is told the room is full (`LoopbackSessionTests.Cap_SixteenthPlayerIsTold_RoomFull`); bad joins fail cleanly; leaving is a DNF; keys never start an online race (`ExternalStartRulesTests`); broken room and pose messages are refused (`RaceControlCodecTests.BrokenLines_AreRefused`, `KartPoseCodecTests.Decode_RefusesWrongLength_Version_AndGarbage`, **new** `Online/OnlineHostileInputTests`: 65 players in a message, phase -1 or 99, NaN or infinite start time, a pose with a NaN or negative time, a pose text longer than one pose); **new (19 Nov)** a finish under 5 s or over 30 min never counts, so a changed game that reports 0.01 s cannot win (`OnlineRaceLimitRulesTests.UnbelievableFinish_IsRefused`, `TooFastEntry_IsNotFinishedAfterTheLimit`), a pose with a position far outside the world is clamped (`KartPoseCodecTests.HugePositionInAPacket_IsClampedOnDecode`), and a pose stamped far in the future no longer freezes that kart (`PoseBufferTests.PoseFarInTheFuture_DoesNotFreezeTheKart`); (PM) online refused without the online build.
- **Edge:** ties (lower id first), a late room message still lands GO on time, network hitches, an empty room list, exactly 64 players in a message.

### 4. Leaderboard save and show (Supabase, offline fallback) about 110 runs

- **Accepted:** `LeaderboardServiceTests.Online_Works_...`, `LeaderboardJsonTests.GoodAnswer_IsRead`, `LeaderboardRankingTests`, `LeaderboardConfigCheckTests.GoodUrl_AndPublishableKey_AreAccepted`, `LapTimeRulesTests`; (PM) `LeaderboardPlayTests.FinishedLap_SaveAName_ItShowsInTheTop10`.
- **Rejected:** the online service fails: the time stays on the device and the screen says "offline" (`LeaderboardServiceTests.Online_Fails_TheDeviceKeepsTheTime_AndSaysOffline`); bad rows from the internet are dropped (HTML, SQL-like, rude, too fast, too slow); huge or broken answers are refused; bad URLs and secret keys are refused (`LeaderboardConfigCheckTests`); impossible lap times (`LapTimeRulesTests`); **new** `Verification/BuildKeyAndServiceInputTests`: a bad track name never becomes a web request; **new (19 Nov)** neither does a bad name (`<b>`, one letter, untrimmed) or a time that is NaN, under 5 s or over 30 min (`Service_BadNameOrTime_FailsAtOnce_WithoutARequest`).
- **Edge:** ties in whole milliseconds, exactly 10 rows kept, exactly 5 s and 30 min, a dot as decimal sign on every computer; **new** `Verification/DatabaseRulesMatchCodeTests`: the database CHECKs and access rules (read from `backend/supabase/*.sql`) equal the game's rules for every Latin character.

### 5. Name entry about 50 runs

- **Accepted:** `PlayerNameRulesTests.GoodNames_AreAccepted` (exactly 2 and 16 characters, accents, "Dickens"); (PM) the cleaned name is saved and remembered.
- **Rejected:** empty, 1 or 17 characters, emoji, HTML and rich text, SQL-like text, tabs, other scripts' digits, rude words incl. "Sh1t" and "s-h-i-t" (`PlayerNameRulesTests`); (PM) `<b>` keeps the form open with a message and sends nothing; **new** a tampered saved name is never offered (`Verification/StoredDataTamperTests.TamperedName_IsNotOffered`).
- **Edge:** spaces trimmed and doubled spaces joined, 5000-character input, a name tag never cuts an emoji in half.

### 6. Settings about 55 runs

- **Accepted:** `GameSettingsTests` (10 % steps, mute, unmute by clicking the bar), **new** `Verification/StoredDataTamperTests.Settings_SaveAndLoad_RoundTrip`; (PM) `MenuKeysPlayTests` (Esc closes the settings).
- **Rejected:** broken saves give the defaults; **new** tampered saves in the browser storage: infinity, NaN, 1e9, -5, a comma, flags other than 0/1.
- **Edge:** a volume between two steps snaps to the grid, an old shorter save still loads, a 100 000 character save.

### 7. Items about 150 runs

- **Accepted:** `ItemOddsTests`, `ItemSlotTests`, `ItemBoxStateTests`, `ItemBoxLayoutTests`, `ItemPlacementTests`, `ItemFxRulesTests`; (PM) `ItemBoxPlayTests.DrivingIntoABox_RollsAnItem_AndEUsesItAsABoost`, `EveryItem_CanBeUsed`.
- **Rejected:** a busy slot refuses a new item, an item cannot be used while rolling, a broken box cannot be hit twice; (PM) no items before the start, a full slot keeps its item.
- **Edge:** exactly level or exactly 2 s behind picks the right odds, a roll of 0.9999, zero boxes, a jump landing next to a row; **new (19 Nov)** a roll of exactly 1 (Unity's `Random.value` can return it) never gives an item with weight 0, so no Dean's List while ahead; a roll below 0 or NaN, no weights, all-zero or negative weights and a NaN gap give a defined item instead of a wrong one (`ItemOddsTests`).

### 8. Phone and touch controls about 85 runs

- **Accepted:** `TouchPageTemplateTests` (every touch button sends a key the game knows), `PhoneTapTests` (thumb-sized buttons), `TouchMenuTextTests`, `Driving/InputPickerTests`, `MenuInputRulesTests`.
- **Rejected:** menu keys and system keys do not start a race (`RaceKeysTests`), no input while input is off, the page does not turn taps into mouse clicks.
- **Edge:** NaN or out-of-range touch input becomes 0 or is clamped, empty menu lists.
- **Touch hold stress test** (`python tools/qa/touch_hold_check.py`, outside Unity, real CDP touch in headless Chrome, 19 Nov after the gas-hold bug): a held button never lets go while the finger is down (still, 1-3 px wobble, wobble over the edge, the strip the pressed shrink gives up, for Gas, Brake, Drift, Steer), slides Gas -> Drift keep W without a gap, two thumbs, and the release cases (finger up, pointercancel, tab hidden, blur, race end, pause) leave no key stuck. `--patch-from <branch>` tests a fix on the live build before CI, `--game` also reads the kart's real speed from its `?net=local` pose messages.

### 9. Address flags (`?net=`, `?room=`, `?name=`, `?autostart=`, `?driver=`, test flags) about 70 runs

- **Accepted:** `Online/OnlineUrlFlagsTests.AllFlags_Read`, `ItemPlacementTests.ItemTestFlags_Parse`.
- **Rejected:** unknown transport, unknown room, broken `%zz` escapes (`OnlineUrlFlagsTests.BadValues_AreRefused_NotCrashed`); **new** `Online/OnlineHostileInputTests`: a repeated key, a key without `=`, empty pairs, impossible autostart counts (-3, 0, 1, overflow), a name of only spaces does not join by itself, flags after `#`, unknown drivers.
- **Edge:** values cut to 200 characters (also when the cut splits an escape), autostart above 15 becomes 15.
- **Photo mode** (`?photo=1`, hides menus and HUD for screenshots), **new (19 Nov)** `PhotoModeFlagTests`: only `photo=1` as its own pair counts; `?photo=10`, `?nophoto=1`, `?photo=0` and `#photo=1` do not.

### 10. Build keys (Photon App ID, Supabase key) about 35 runs

- **Accepted:** `OnlineAppIdRulesTests.Guid_IsValid`, **new** pasted with spaces and a line break.
- **Rejected:** `OnlineAppIdRulesTests.EmptyOrBroken_IsNotValid`, **new** `Verification/BuildKeyAndServiceInputTests` (braces, one digit more or less, a space inside, a dashboard URL, a secret Supabase key with spaces, a JWT whose payload is not base64).
- **Edge:** blank App ID = single-player build, not an error.

### 11. Web build, load time, start screen fit and repository rules about 65 runs

- **Accepted / edge:** `WebBuild/WebLoadTimeTests` (texture and mesh import budgets), `WebPageTests` (page bridge is off outside a Web build).
- **New** `WebBuild/WorldFileTests` (the campus world as its own file, load time 18 Nov, 7 tests). Accepted: the player build leaves out only the world scene; the world file name is `world-<md5>.bundle` of its content (same world = same name, a changed world = a new name); stamping fills the DXT and ETC2 names and the world's share of the loading bar into `index.html`; the page caches `world-*.bundle` as `immutable`, waits for the track before "Click to play" and listens for the game's `cc-world` events; the page bridge (`CarcavelosWebPage.jslib`) has both world calls. Edge: without an ETC2 world the phone placeholder stays, so phones use the DXT world; an unstamped page (a build with the world inside the `.data`) loads the old way.
- **New** `StartScreenFitTests` (start screen on boxy windows, 15 runs incl. the kart builder and the start line-up). Accepted: 16:9, 16:10 and 3:2 keep the normal layout. Edge: 4:3 and 5:4 get the compact layout (`hud--compact`), and at 5:4 the mode cards end before the start leaderboard begins; the compact rules exist in `UI/StartScreen.uss`.
- **New** `Verification/RepoHygieneTests`: the leaderboard key and address are empty in Git, the Photon SDK and App ID file are ignored, every GitHub Action is pinned to a commit, every Unity package has an exact version equal to the lock file and comes from the Unity registry, every Python tool is pinned.

### 12. Kart builder, race modes and teacher tribute

- **Accepted:** `Drivers/KartLoadoutTests` (every one of the 64 sets has a code and back), `KartAssemblerTests` (the right driver, kart and tyres are built), `Online/OnlineLoadoutTests` (a set travels online), `RaceModeRulesTests` (three modes, saved mode), `TributeRulesTests`; (PM) `DriverSelectPlayTests.PickingKartAndTyres_RebuildsOnlyThePlayersKart`, `TeacherTributePlayTests`.
- **Rejected:** a broken set code is refused and read as the Rookie's set (`KartLoadoutTests.BrokenCodes_AreRefused_AndReadAsTheRookiesSet`), parts outside the lists are refused (`Choice_RefusesPartsOutsideTheLists`), bad saved numbers fall back part by part (`Choice_BadSavedNumbers_FallBackPartByPart`); a saved mode of -1, 3 or `int.MaxValue` starts the default mode (`RaceModeRulesTests.FromSaved_AcceptsKnownNumbers_RejectsTheRest`).
- **Edge:** the first and last code of each range, the builder fitting a 5:4 window (`StartScreenFitTests.KartBuilder_FitsAtFiveToFour`).

### Totals

The exact current numbers are in the summary of the newest Tests run on `main` (the badge above links to it): one
line per mode, "passed / total". The counts grow with every feature, so this page gives no fixed number. For scale:
on 18 Nov 2026 (`main` at `14f9190`) 1560 Edit Mode and 39 Play Mode runs, all passing; 124 of the Edit Mode runs came in 6 files
added that day for rejected and edge cases (`Verification/*` and `Online/OnlineHostileInputTests`).

## Not covered by automated tests (and how we check it instead)

- **The real Photon network.** No test talks to Photon's servers (that would need an App ID in CI and is not
  repeatable). The room rules run against an in-memory session (`LoopbackSessionTests`) that follows the same
  interface. Checked by hand and with scripted browser bots before each release: 15 real players in one room,
  the 16th gets "room full".
- **The real Supabase database.** Tests use a fake service and read the SQL file instead of running it on Postgres,
  so a mistake that only Postgres would show (for example a typo that makes the SQL fail) is not caught here. The
  SQL was run once in the Supabase dashboard; `backend/supabase/README.md` lists the steps.
- **How driving feels and looks:** drift timing, camera, jumps, audio, art. Checked by playing (table below).
- **Browser specifics:** the JavaScript in `Assets/WebGLTemplates/CarcavelosCircuit/index.html` (touch release
  when the tab loses focus, the loading screen) is checked by reading the template text; the touch buttons also by `tools/qa/touch_hold_check.py` in a real
  browser. Phones were checked by hand and with Playwright scripts on 7 phone profiles.
- **Total download size and frame rate on a phone:** need a finished build and a real device.
- **The campus world download** (`RaceFlow/WorldStream.cs`): the download itself, its one retry, the "The track did not download" card and the browser's IndexedDB cache of the world file run only in a real Web build, so `WorldFileTests` checks the build rules and the page text instead. Checked in a real browser: the 4 Mbit/s runs and a blocked world file on 18 Nov 2026 ([history/2026-11-18-load-time.md](history/2026-11-18-load-time.md)), and a load of the live build on 18 Nov 2026 (loading bar, "Click to play", start screen, 0 console errors).
- **A bit-identical bot race.** The Play Mode lap with five bots steps the game one physics step per frame and puts
  the frame and physics clocks in the same relation in every run (the bots steer from interpolated kart positions,
  which depend on it), so runs match to the centimetre for the first 15 s. A float-rounding rest remains, because each
  run's absolute clock differs, and contacts between karts can grow it; rarely a bot then needs the game's own
  recovery once. The test therefore checks that every bot finishes and that the field is put back at most once per lap
  (`BotRacePlayTests`), and logs every bot's position every 5 s so two runs can be compared. A fully identical race
  would need changes in the game's driving code.
- **Code that runs only with a live network or a real browser tab:** `Online/Session/BroadcastChannelSession.cs`
  (the `?net=local` test transport between tabs of one browser), the lobby and join screens of
  `Online/Race/OnlineRaceDirector.cs`, `Online/Fusion/*` (needs the Photon SDK, which is not in CI) and the success,
  timeout and "saved but rank failed" paths of `SupabaseLeaderboardService` (a real web request). Their rules are
  pulled out into plain classes that are tested (`OnlinePlayer`, `RaceControlCodec`, `KartPoseCodec`, `OnlineUrlFlags`,
  `LeaderboardJson`, `SupabaseRequests`), and the network classes only call those. Checked by hand with two browser
  tabs (`?net=local`) and the 15-player browser-bot run.
- **The kart physics itself** (`Driving/KartController.cs`): a Unity Rigidbody component. Its rules (drift stages,
  boost, input) are in tested helper classes, and the Play Mode laps drive the real kart through the whole track.
- **Page-only flags in `index.html`:** `?dpr=` (sharper pictures, only together with `?photo=1`), `?test=`, the room
  chip and `?cc-tex=`. They are read by the page's JavaScript, not by C#. A broken value is ignored (checked by hand:
  `?dpr=abc&perf=xyz` on the live build on 19 Nov 2026 loaded normally with 0 console errors).
- **Values that only our own code produces:** a few rule classes do not guard against a NaN lap time
  (for example `PersonalBest.FinishRun`). A lap time always comes from the race timer's timestamps, which cannot be
  NaN, and a NaN read from a save loads as "no record", so this was left as it is.

## Manual checks before a release

| Check | Expected result |
|---|---|
| Countdown | Kart cannot move, timer not started |
| Drift + boost | Holding drift while steering charges; release when charged = one short boost + visible cue; early release = no boost |
| Ramp jump | Kart lands upright, camera stays behind the kart |
| Recovery (R) | Kart returns to the last valid checkpoint, timer keeps running, no progress awarded |
| Audio | Starts after the first input, no errors in the browser console |
| Deployed build | A teammate on another computer opens the link and finishes two runs in a row |
| Online | Two browsers join the room, race, both see the same order |
| Fresh clone | Opens in the pinned Unity version with all scenes and prefabs, no missing scripts |

Every deployed build and its log are in Actions > Web build on `main` ([builds.md](builds.md)); the hand checks for
the presentation are in [demo-plan.md](demo-plan.md). Two scripts outside Unity check the demo itself in a real
headless Chrome: `tools/preflight/preflight.py` (GREEN/RED for the live site, the deploy, the game boot, the
leaderboard, a real Photon join and the laptop's sound, power and screen) and `tools/preflight/fallback_check.py`
(no internet, Photon blocked, leaderboard down, two players on the laptop copy, a player dropping mid-race).
