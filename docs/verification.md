# Verification: what the tests cover, and what they do not

Graded criterion 4: "Tests cover accepted and rejected actions and edge cases, and all pass on `main`."
This page is the map from every workflow of the game to the tests that check it, the commands to run them, and an
honest list of what no automated test covers.

[![Tests](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml?query=branch%3Amain)
[![Security](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml/badge.svg?branch=main)](https://github.com/belschak/carcavelos-circuit/actions/workflows/security.yml?query=branch%3Amain)

## Running the tests

**On GitHub (the reference).** `.github/workflows/tests.yml` runs every Edit Mode and Play Mode test in Unity
6000.3.28f1 (GameCI Linux image) on every pull request into `main`, on every push to `main` and on branches named
`test/...`. A pull request is only merged when this run is green, so `main` is always tested. The run page shows
"passed / total" per mode; the NUnit XML files are in the run's `test-results` artifact.
All runs: <https://github.com/belschak/carcavelos-circuit/actions/workflows/tests.yml>

**On your computer.** Open the project in Unity 6000.3.28f1 and use Window > General > Test Runner (Edit Mode tab,
then Play Mode tab, "Run All"). Or from a terminal in the repository root (Unity closed):

```bash
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
| `Assets/_Project/Tests/PlayMode/` | Tests.PlayMode | The real track scene: a full lap, retry, bots, items, leaderboard form, menus, driver select |

Most logic sits in small plain C# classes (`...Rules`, `...Codec`, `...Format`) so it can be tested without a scene;
the MonoBehaviours only connect them to Unity. Play Mode tests then check that the pieces work together in the real scene.

## Coverage map

"Accepted" = a valid action works. "Rejected" = an invalid action is refused or bad input is handled without a crash.
"Edge" = boundaries (exactly the limit, one over), empty, NaN or infinity, duplicates, timing, failure of an outside service.
Test files are in `Assets/_Project/Tests/`; PlayMode files are marked (PM). Counts are test runs (each `[TestCase]` counts).

### 1. Time attack (lap timing, checkpoints, wrong way, recovery, retry, medals, personal best, ghost) about 240 runs

- **Accepted:** `RaceRulesTests` (in order then finish), `RaceTimerTests` (lap time from timestamps), `RaceStateMachineTests` (3-2-1-GO, retry), `MedalRulesTests`, `PersonalBestTests`, `GhostRecordingTests`, `GhostLoaderTests`, `WrongWayDetectorTests`, `StuckDetectorTests`, `FallCheckTests`; (PM) `RaceSmokeTests.CleanLap_FinishesOnce_WithATime_...`, `Retry_ResetsTimerAndCheckpoints_...`.
- **Rejected:** finish right after the start, missed or backwards checkpoints, same checkpoint twice, anything after the finish (`RaceRulesTests`); finish outside Racing (`RaceStateMachineTests`); negative frame times (`RaceTimerTests`); broken or foreign-road saves give "no record" (`PersonalBestTests.BrokenSave_MeansNoRecord`, `RecordFromAnotherRoad_IsDropped_...`); broken ghosts fall back to the shipped one (`GhostLoaderTests`); (PM) `SkippedCheckpoint_DoesNotFinish_AndRecoverKeepsTimeAndProgress`.
- **Edge:** exactly on a medal time, one millisecond slower; a 1-checkpoint track; long frames during the countdown; NaN, infinity and zero lap length (`LapProgressTests`); a save written on a Portuguese laptop (comma); a lap longer than the ghost budget.

### 2. Race against bots (grid, standings, bot driving, finish order) about 150 runs

- **Accepted:** `RacerGridTests`, `RaceStandingsTests`, `RacerRegistryTests`, `BotRulesTests`, `TestBotSteeringTests`, `DriverRosterTests`; (PM) `BotRacePlayTests.FiveBots_DriveAFullCampusLap_WithoutGettingStuck`, `RacersPlayTests`, `DriverSelectPlayTests`.
- **Rejected:** null or duplicate racers (`RacerRegistryTests`), bad numbers into the bot rules never steer or throw (`BotRulesTests.Steer_BadInput_DoesNothing`, `Stuck_BadNumbers_...`), the player never gets a name tag over their own kart.
- **Edge:** exact ties keep their order, null lists and no track (`RaceStandingsTests`), more racers than an online room, zero steering angle.

### 3. Online race (one room of 15, start, finish order, messages, leave) about 160 runs

- **Accepted:** `Online/LoopbackSessionTests` (two sessions share a room, the whole class meets, host moves on), `OnlineStartRulesTests`, `FinishOrderRulesTests`, `OnlineRaceLimitRulesTests`, `KartPoseCodecTests`, `RaceControlCodecTests`, `PoseBufferTests`, `OnlineLobbyViewTests`; (PM) `RaceModesPlayTests.Online_TheLobbyStartsResetsAndPlacesTheRace_KeysDoNothing`.
- **Rejected:** the 16th player is told the room is full (`LoopbackSessionTests.Cap_SixteenthPlayerIsTold_RoomFull`); bad joins fail cleanly; leaving is a DNF; keys never start an online race (`ExternalStartRulesTests`); broken room and pose messages are refused (`RaceControlCodecTests.BrokenLines_AreRefused`, `KartPoseCodecTests.Decode_RefusesWrongLength_Version_AndGarbage`, **new** `Online/OnlineHostileInputTests`: 65 players in a message, phase -1 or 99, NaN or infinite start time, a pose with a NaN or negative time, a pose text longer than one pose); (PM) online refused without the online build.
- **Edge:** ties (lower id first), a late room message still lands GO on time, network hitches, an empty room list, exactly 64 players in a message.

### 4. Leaderboard save and show (Supabase, offline fallback) about 110 runs

- **Accepted:** `LeaderboardServiceTests.Online_Works_...`, `LeaderboardJsonTests.GoodAnswer_IsRead`, `LeaderboardRankingTests`, `LeaderboardConfigCheckTests.GoodUrl_AndPublishableKey_AreAccepted`, `LapTimeRulesTests`; (PM) `LeaderboardPlayTests.FinishedLap_SaveAName_ItShowsInTheTop10`.
- **Rejected:** the online service fails: the time stays on the device and the screen says "offline" (`LeaderboardServiceTests.Online_Fails_TheDeviceKeepsTheTime_AndSaysOffline`); bad rows from the internet are dropped (HTML, SQL-like, rude, too fast, too slow); huge or broken answers are refused; bad URLs and secret keys are refused (`LeaderboardConfigCheckTests`); impossible lap times (`LapTimeRulesTests`); **new** `Verification/BuildKeyAndServiceInputTests`: a bad track name never becomes a web request.
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
- **Edge:** exactly level or exactly 2 s behind picks the right odds, a roll of 0.9999, zero boxes, a jump landing next to a row.

### 8. Phone and touch controls about 85 runs

- **Accepted:** `TouchPageTemplateTests` (every touch button sends a key the game knows), `PhoneTapTests` (thumb-sized buttons), `TouchMenuTextTests`, `Driving/InputPickerTests`, `MenuInputRulesTests`.
- **Rejected:** menu keys and system keys do not start a race (`RaceKeysTests`), no input while input is off, the page does not turn taps into mouse clicks.
- **Edge:** NaN or out-of-range touch input becomes 0 or is clamped, empty menu lists.

### 9. Address flags (`?net=`, `?room=`, `?name=`, `?autostart=`, `?driver=`, test flags) about 70 runs

- **Accepted:** `Online/OnlineUrlFlagsTests.AllFlags_Read`, `ItemPlacementTests.ItemTestFlags_Parse`.
- **Rejected:** unknown transport, unknown room, broken `%zz` escapes (`OnlineUrlFlagsTests.BadValues_AreRefused_NotCrashed`); **new** `Online/OnlineHostileInputTests`: a repeated key, a key without `=`, empty pairs, impossible autostart counts (-3, 0, 1, overflow), a name of only spaces does not join by itself, flags after `#`, unknown drivers.
- **Edge:** values cut to 200 characters (also when the cut splits an escape), autostart above 15 becomes 15.

### 10. Build keys (Photon App ID, Supabase key) about 35 runs

- **Accepted:** `OnlineAppIdRulesTests.Guid_IsValid`, **new** pasted with spaces and a line break.
- **Rejected:** `OnlineAppIdRulesTests.EmptyOrBroken_IsNotValid`, **new** `Verification/BuildKeyAndServiceInputTests` (braces, one digit more or less, a space inside, a dashboard URL, a secret Supabase key with spaces, a JWT whose payload is not base64).
- **Edge:** blank App ID = single-player build, not an error.

### 11. Web build, load time and repository rules about 50 runs

- **Accepted / edge:** `WebBuild/WebLoadTimeTests` (texture and mesh import budgets), `WebPageTests` (page bridge is off outside a Web build).
- **New** `Verification/RepoHygieneTests`: the leaderboard key and address are empty in Git, the Photon SDK and App ID file are ignored, every GitHub Action is pinned to a commit, every Unity package has an exact version equal to the lock file and comes from the Unity registry, every Python tool is pinned.

### Totals

Before this round: 1339 Edit Mode + 37 Play Mode runs, all green on `main`. This round adds 124 Edit Mode runs
in 6 new files (`Verification/*` and `Online/OnlineHostileInputTests`); no existing test was changed.

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
  when the tab loses focus, the loading screen) is checked by reading the template text, not by running it in a
  browser. Phones were checked by hand and with Playwright scripts on 7 phone profiles.
- **Total download size and frame rate on a phone:** need a finished build and a real device.
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

Log each build test in `docs/builds.md`: date, URL, commit, browser, result.
