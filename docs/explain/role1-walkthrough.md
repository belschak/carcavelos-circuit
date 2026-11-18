# Role 1 walkthrough: Driving + camera (Alessandro Rossi)

This part is the kart itself: how it speeds up, brakes, steers, drifts to charge a boost, jumps off ramps, spins a trick in the air and is put back on the road after a crash. It also holds the chase camera that follows the kart and the floating name tags above the other racers. The same kart is driven by the player, by the five bots and by the finish autopilot.

## Files
| File | What it does |
|---|---|
| `Driving/KartController.cs` | The kart: one Rigidbody ball does all the physics. Reads input, drives, drifts, boosts, flies, tricks, resets. Implements `IKartState`, `IKartBoost`, `IKartDrivable` |
| `Driving/KartTuning.cs` + `Driving/Settings/KartTuning.asset` | Every driving number in one ScriptableObject (top speed, grip, drift times, boost lengths, air and trick values) |
| `Driving/DriftCharge.cs` | Plain C# drift rules (D3): charge time, stage 0/1/2, boost seconds on release |
| `Driving/TrickRule.cs` | Plain C# trick rules (D11): one trick per jump, boost only if finished before landing |
| `Driving/FlyingPose.cs` | Plain C# maths for the model's nose angle and wobble in the air (D10) |
| `Driving/InputPicker.cs` | Plain C# answer to "who drives this frame": nobody, a bot, or the keyboard (D8) |
| `Driving/NameTagManager.cs` + `Driving/NameTag.cs` | One floating name per racer in `RacerRegistry`, facing the camera (D9) |
| `Driving/NameTagRules.cs` | Plain C# tag rules: who gets a tag, name cleaning, fade by distance |
| `Driving/Kart.prefab` | The kart root (Rigidbody, SphereCollider radius 0.5, `KartController`) with a `Visual` child for the model |
| `Driving/ChaseCamera.prefab` | Cinemachine camera (D2): Follow with offset 0/3/-6 plus Rotation Composer aimed 1 m above the kart |
| `Shared/IKartState.cs` | The read contract everyone uses: speed, drift, boost, grounded, steer, `InputEnabled`, `ResetTo` |
| `Shared/IKartDrivable.cs` + `Shared/KartInput.cs` | The hook that lets a bot or the autopilot plug in as the kart's "keyboard" |
| `Driving/NameTagTestRacer.cs`, `Driving/Scenes/Driving_TestBed.unity` | Test-scene helpers only: a pretend racer and a flat test ground with ramps |

## How it works
0. **Set-up (`Awake`).** Rotation X and Z are frozen, interpolation is on for a smooth camera, collision detection is continuous so a fast kart cannot pass through a wall, and the ball gets a frictionless material (grip is done in code). `DriftCharge` and `TrickRule` are built from the tuning; without tuning the kart does not drive and `Start` logs a warning.
1. **Input, every frame (`Update`).** `InputPicker.Pick` decides who drives. Input off (countdown, results, pause): nothing, and a bot is not even asked. `InputSource` set (bot or `FinishCruise`): its `ReadInput().Clamped()`. Otherwise `ReadKeyboard()`: W/S or arrows for throttle, A/D or arrows for steer, Space for drift. Shift sets `trickPressed`, only when no source is plugged in.
2. **Physics, every step (`FixedUpdate`).** `CheckGround` casts one ray down from the ball's centre. On the ground the velocity is split into forward, sideways and up. `ApplyPedals` moves the forward part towards top speed (18 m/s), reverse (8 m/s) or zero. Grip removes most of the sideways part (`sideGrip` 10 per second), so the kart goes where it points. `TurnKart` turns only the heading, and steering needs speed (full at 5 m/s).
3. **Drift (`UpdateDrift`).** Space + steer above 0.5 + faster than 40 % of top speed starts `DriftCharge.Begin`. Grip drops to 30 %, the slide turns into forward speed (10 % loss per second). Holding charges: blue sparks at 0.8 s, orange at 1.6 s, faster when steering into the turn (x1.3) than against (x0.7). Release gives 0.4 s or 1.2 s of boost; too early gives nothing. Too slow, airborne, input off or a wall hit (`OnCollisionEnter`, contact normal y below 0.5) cancel with no boost.
4. **Boost.** `GiveBoost` keeps the longer of the running and the new boost. While boosting the kart pushes towards 1.45 x top speed (26.1 m/s) at 40 m/s per second. Item boxes call `AddBoost`, which adds seconds and keeps the higher multiplier.
5. **Ramp jump (D4).** No ground under the ray means airborne: `FlyThroughAir` adds 1.5 x gravity while falling and leaves only 25 % steering. Rotation X and Z are frozen in `Awake`, so the kart always lands upright.
6. **Trick (D11, `UpdateTrick`).** Take-off calls `TrickRule.TakeOff`; Shift in the air calls `TryStart`; landing calls `Land`, which pays 0.6 s boost only if the 0.5 s spin finished in time.
7. **Looks (`LateUpdate`).** The `Visual` child tilts to the slope on the ground. In the air `FlyingPose` gives nose up while rising, nose down while falling (max 25 degrees, full at 8 m/s) and a 3 degree wobble. A running trick spins the model 360 degrees. The physics body never tilts.
8. **Reset (`ResetTo`, D5).** Teleport, zero velocity, clear input, drift, boost, item multiplier and trick. Retry and Recover use it.
9. **Camera.** Role 4's `RaceFlow/ChaseCameraRig.cs` sets the prefab's Follow to the spawned kart and cuts the camera after a teleport (decision 2026-11-10).
10. **Name tags (`NameTagManager.LateUpdate`).** On `RacerRegistry.Changed` all tags are rebuilt; each frame every tag moves above its kart (1.6 m), faces the camera and fades: hidden up to 3 m, full from 6 to 45 m, gone at 60 m. The player's own tag is hidden.

## Why it is built this way
- **Own sphere controller, not WheelColliders or the Karting Microgame** (2026-11-07): the team must explain every line, a ball cannot flip, and it needs the fewest tuning values.
- **One Rigidbody + SphereCollider on the root, X and Z rotation locked** (D1, 2026-11-09): same idea as a rolling ball, but the root and its colliders stay together, which RaceFlow expects at spawn.
- **Rules in plain C# classes** (`DriftCharge`, `TrickRule`, `FlyingPose`, `InputPicker`, `NameTagRules`): no Unity objects, so Edit Mode tests can check them in milliseconds.
- **All numbers in `KartTuning`** (D6, PR #59): nothing hard-coded, tuning in Play Mode. D7 (PR #73) lowered top speed from 25 to 18 m/s and raised the boost from 1.35 to 1.45 so a first campus lap is controllable.
- **`IKartDrivable` input hook** (D8, decision 2026-11-11): one kart physics for players and bots, so bots cannot cheat by code; `Clamped()` turns a bot's NaN or too-big value into a legal input; `InputEnabled` still wins.
- **`Steer` on `IKartState`** (2026-11-10, request Q11): the model's steering wheel and front wheels follow the real steering.
- **Cinemachine prefab, wired by Role 4** (2026-11-10, Q13): Role 1 owns how the camera follows, Role 4 only connects it to the race and the fly-over.
- **One boost path** (`GiveBoost`): drift and trick boosts never stack endlessly.
- **Frictionless ball, angular velocity zeroed every step** (`Awake`, `FixedUpdate`): the ground and walls never grab or spin the kart; only steering turns it, so grip is one number to explain.
- **Input off also ends a boost** (`FixedUpdate`): at the finish or in a pause the kart rolls out instead of being pushed on, and a running drift ends with no boost (Q6 rule).

## Limits
- Only the heading turns: on a slope or a bank the physics body stays level and only the `Visual` model leans, so the kart can never roll, also not on purpose.
- One ground ray from the ball's centre (`CheckGround`, 0.3 m extra): a kart half over a ramp edge counts as airborne a little early.
- Keys are hard-coded in `ReadKeyboard` (no Input Actions asset): no rebinding and no gamepad. Phones work only because the Web template sends the same key presses.
- Bots never trick: Shift is read only when `InputSource` is null, and `KartInput` has no trick field.
- Item boosts add up in `AddBoost`, so a chain of items can give a long boost; drift and trick boosts do not.
- `KartTuning.asset` does not list the D10 and D11 fields yet, so the code defaults in `KartTuning.cs` apply (25, 8, 3, 1.2, 0.5, 0.6).
- No wall-hit event in `IKartState`: Audio guesses bumps from speed drops (decision 2026-11-10, audit 11).
- `NameTagManager.Rebuild` throws all tags away when any racer joins or leaves; fine for 6 racers. Tags use the built-in `TextMesh`, names cut at 16 characters.

## How it could change
- Gamepad and rebinding: replace `ReadKeyboard` in `Driving/KartController.cs` with an Input Actions asset.
- Bots doing tricks: add a `Trick` field to `Shared/KartInput.cs` (team OK needed), read it in `KartController.Update`, set it in `Visuals/Bots/BotDriver.cs`.
- Different kart per driver: one `KartTuning` asset per driver, chosen at spawn (`Driving/Kart.prefab` + Role 4's driver roster).
- A real wall-hit event: an `event` on `KartController` from `OnCollisionEnter`, exposed through a new Shared interface, so `Audio/WallBumpDetector` can stop guessing.
- Smarter ground check: several rays or a `SphereCast` in `CheckGround` for cleaner ramp edges.

## Tests
- `Tests/EditMode/Driving/DriftChargeTests.cs` (11 tests): stages at the right times, early release = no boost, stage 1 and 2 boost lengths, cancel clears the charge, steering into the turn charges faster, `Charge01` capped at 1, release or tick without a drift does nothing, `Begin` restarts the charge.
- `Tests/EditMode/Driving/TrickRuleTests.cs` (12): finished trick pays, exactly on time pays, still spinning at landing pays nothing, refused on the ground and for a second trick per jump, a new jump allows a new trick, landing twice pays once, reset cancels, bad tuning numbers are safe.
- `Tests/EditMode/Driving/FlyingPoseTests.cs` (10): level on the ground, nose up rising, down falling, clamped at the maximum, NaN stays level, wobble zero at take-off, never above its amplitude, grows in.
- `Tests/EditMode/Driving/InputPickerTests.cs` (9): keyboard without source, source wins, input off gives nothing and the source is not asked, asked once per pick, too-big, NaN and infinity values clamped.
- `Tests/EditMode/Driving/NameTagRulesTests.cs` (18): player and empty names get no tag, trimming, new lines removed, long names cut, emoji never cut in half, fade in, full, fade out, bad distances and bad settings hidden.
- Play Mode tests on `Main.unity` (written by other roles, they drive this kart):
  - `Tests/PlayMode/RaceSmokeTests.cs`: no driving on the start screen or in the countdown, the chase camera follows the spawned kart after GO, and a robot lap teleports the kart with `ResetTo`.
  - `Tests/PlayMode/RacersPlayTests.cs`: one name tag per racer, hidden over the player, in the HUD font.
  - `Tests/PlayMode/BotRacePlayTests.cs`: five bots finish a campus lap on this kart (through `IKartDrivable`) without being put back.
- Not tested: `KartController` physics itself, driving feel, camera smoothness and how tags look. These were checked by hand in `Driving_TestBed.unity` and the race, following the manual "Drift + boost" and "Ramp jump" rows in `docs/verification.md`.
