# Role 4 walkthrough: race flow, HUD, sound (David's cheat sheet)

For the live code walkthrough at the final presentation. Every file below is described as it really is in the repo on 2026-11-08. Line numbers are approximate pointers (`File.cs:NN`), so you can jump there while presenting.

---

## 1. The 30-second pitch

"Role 4 turns a kart and a track into a game. `Main.unity` starts, loads the track scene on top of itself, spawns the kart and hands both to the `RaceManager`. The `RaceManager` runs a small state machine: Ready, Countdown, Racing, Results. It switches the kart's input on only while racing, counts the lap time, checks checkpoints through the race rules, handles Retry (Enter) and Recover (R) and saves the best time. The HUD only reads the `RaceManager` and draws it. Sounds listen to events. Everything talks to the kart only through the `IKartState` interface, so it works with any kart that follows the contract."

```
Main.unity
  |
  v
RaceBootstrap.Start()  (coroutine)
  |-- LoadSceneAsync("Track_Campus", Additive)   no track inside? -> load "RaceFlow_PlaceholderTrack"
  |-- FindTrackIn(scene) -> PlaceholderTrack     (later: TrackInfo)
  |-- Instantiate(kartPrefab) at SpawnPoint      + KartAudio prefab as a child
  |-- raceManager.SetUp(kart, track, sceneName)  checks the checkpoints, remembers the kart's colliders
  '-- chaseCamera.SetTarget(kart)                 only if a chase camera is set (it is removed once Role 1's camera exists)
          |
          v
RaceManager.Update()  every frame
  Ready --any key/click--> Countdown --3,2,1,GO--> Racing --finish line, all checkpoints--> Results
                              ^                                                              |
                              '--------------------- Retry (Enter or button) ----------------'
  |  reads/writes kart only via IKartState (InputEnabled every frame, ResetTo, Speed ...)
  |  checkpoint triggers -> CheckpointRelay -> IsKartCollider? -> OnCheckpointEntered -> lap rules
  |
  +--> RaceHud (UI Toolkit)  polls State, LapSeconds, BestSeconds, Kart ... every frame, new text only on change
  +--> RaceSounds            listens to events CountdownStep, CheckpointPassed, LapFinished, Restarted, Recovered
  '--> KartAudio (Audio)     reads IKartState of its parent kart every frame; MusicPlayer loops music
```

Suggested order for the live walkthrough (about 5 minutes):

1. `IKartState` (the contract, 20 s).
2. `RaceStateMachine` + its test (the idea: plain C#, events, the while loop).
3. `RaceManager.Update()` and `OnCheckpointEntered()` (the race in 30 lines).
4. `RaceBootstrap.Start()` (additive loading, coroutine).
5. `RaceHud.Update()` + one line of `RaceHud.uxml` / `.uss` (UI Toolkit = HTML/CSS).
6. `KartAudio.Update()` + `EnginePitch` (pitch by speed, edge detection).
7. Show the Test Runner going green.

---

## 2. Design decisions, one line each

| Decision | Why |
|---|---|
| Placeholders (`PlaceholderTrack`, `PlaceholderCheckpoint`, `PlaceholderLapRules`, `PlaceholderKart`, `PlaceholderChaseCamera`) with the same public API as Role 1 and 2's contract | I could build and test R1 to R5 now without waiting for, or writing, Noah's and Alessandro's graded code. Swap day = rename types, delete placeholders. |
| Race sounds in `RaceFlow/RaceSounds` and `RaceFlow/RaceMusic`, kart sounds in `Audio/KartAudio` | The `Audio` assembly may only reference `Shared`, so it cannot see `RaceManager`. Kart sounds only need `IKartState`, race sounds and music need the `RaceManager` events. `RaceFlow` references `Audio` (one direction only) for the mix maths and `KartAudio.SetMixLevels`. |
| No AudioMixer: every mix level is a script fade in dB (`LevelFade`, `DuckEnvelope`) | Unity's Web build only supports mixer group volume (no Duck Volume, compressor, limiter), and there is no public API to create a mixer from code. Script fades do the same and behave identically in the Editor and the browser. |
| HUD = UI Toolkit (`.uxml` + `.uss`) | Text files that work like HTML and CSS, no TextMesh Pro import, easy to read in a PR. PanelSettings scale from a 1280x720 reference. |
| Plain C# classes for logic (`RaceTimer`, `RaceStateMachine`, `PersonalBest`, `PlaceholderLapRules`, `CheckpointValidation`, `FallCheck`, `ShownNumber`, `EnginePitch`), MonoBehaviours only as thin glue | Plain classes can be unit tested in Edit Mode with fake frame times, no scene needed. |
| Official lap time from two timestamps, added-up frame times only for the live timer | Adding thousands of float frame times drifts by milliseconds, and the finish happens inside a physics step between two frames. One subtraction of two `double` timestamps is exact. |
| HUD builds a new string only when the shown number changes | Strings built every frame are garbage; on the Web the garbage collector cleans up all at once, which shows as a hitch mid-lap. |
| Scenes and prefabs built by `Role4SceneBuilder` (editor script) | Nobody hand-edits scene YAML; same result on every laptop; re-runnable. The kart swap is automatic: it picks Role 1's `Kart` prefab as soon as it exists. |
| Track scenes may keep their own test cameras; `RaceBootstrap` switches them off | Each owner can test their scene alone, and the race still has exactly one camera and one AudioListener (Main's). |
| Bloom via one global Volume in Main | Luc's request: drift sparks and boost flames should glow. The profile asset is created once and then left alone, so Role 3 can tune it. |
| `RaceSettings` ScriptableObject | All tuning numbers in one asset, no magic numbers in code (team rule). |
| Medals, lap report and coach tips (task P5) | A fresh browser has no personal best, no splits, nothing to beat. Medal times give the first lap a goal, the lap report shows the drift system was used, and the coach teaches drifting without anyone explaining it at the demo. Each is one small plain C# class with tests. |
| Countdown 1 s per step on the first start, 0.5 s on retry | Time-trial players retry a lot, a quick restart feels better. |
| "Press any key to start" screen | Browsers block sound until the first click or key press; our start screen forces that press before the race. |
| Enter = retry, not Escape | In a browser Escape leaves fullscreen. |
| Own synthesised CC0 sounds + ElevenLabs kart sounds, WAV sources mastered by `process_wavs.py` | Licence is clean (`THIRD_PARTY.md`). Unity re-encodes every clip to AAC for the Web build anyway, and the Windows AAC encoder changes the first 1024 samples, so loops get 2048 samples of silence and WAV loop points (`smpl` chunk) that skip it. WAV is the only format that carries those loop points. |
| Every Web build setting set by `Role4WebBuild` in code | Some settings (Code Optimization) are saved per laptop in `Library/`; in code the build is the same whoever runs it. Size before/after in section 3.18. |
| Own Web template, canvas = whole window | Unity's default is a fixed 960x600 box that is cut off in small windows (playtest F1) and tiny on a big screen. |
| One asmdef per module | Compiler enforces who may talk to whom (no hidden dependencies), and recompiles are faster. |
| Ghost = recorded poses replayed on the race timer, meshes only, no physics | You race your own best lap (or the team's on a fresh browser) without the ghost ever touching the kart or a checkpoint. Stretch item, logged in `docs/decisions.md`. |

---

## 3. File by file

### 3.1 `Shared/IKartState.cs` (the contract)

Interface in `CarcavelosCircuit.Shared`. The kart implements it, everyone else (RaceFlow, HUD, Audio, Visuals) only reads it.

- `Speed` (m/s, forward), `MaxSpeed`: used for the speedometer, engine pitch, camera FOV.
- `IsDrifting`, `DriftCharge01` (0..1), `DriftStage` (0, 1 blue, 2 orange), `IsBoosting`, `IsGrounded`: state flags for HUD boost bar and sounds.
- `InputEnabled { get; set; }`: the only property others may write. RaceFlow turns driving off outside Racing.
- `ResetTo(position, rotation)`: teleport, zero velocity, clear drift and boost. Used for Retry and Recover.
- How I would explain it: "An interface is a promise: any class that implements it has these members. My code only knows the promise, not the kart class, so Alessandro's kart can replace my placeholder without changing my code."
- Concept: interfaces, assembly definitions (`Shared` references nothing, so it cannot create cycles).

### 3.2 `RaceFlow/RaceState.cs`

`enum RaceState { Ready, Countdown, Racing, Results }`. "An enum is a named list of options, safer than using numbers or strings."

### 3.3 `RaceFlow/RaceStateMachine.cs` (plain C#)

Decides which phase we are in. No Unity types, only numbers and events, so fully unit tested.

- `State` (starts `Ready`), `CountdownNumber` (3, 2, 1, then 0 = GO).
- `InputAllowed`: `true` only when `State == Racing`. "One line decides when the kart may drive."
- `event Action<RaceState> StateChanged`, `event Action<int> CountdownStep`: "Events are a list of methods to call when something happens. The state machine does not know who listens."
- Constructor `RaceStateMachine(int countdownFrom)`: `Math.Max(1, ...)`, "a bad setting can never skip the countdown".
- `BeginCountdown(float secondsPerStep)` (~line 38): clamps the step to at least 0.05 s, sets the remaining time to `countdownFrom * step`, fires `CountdownStep(3)` and switches to Countdown. Used for the first start and for every retry, with a different step length.
- `Tick(float deltaTime)` (~line 52): only during Countdown; subtracts time; a `while` loop lowers the number each time we cross a step boundary and fires `CountdownStep`; at 0 it switches to Racing. "A while, not an if: if one frame is very long (browser tab in the background) we still fire 2, 1 and GO, none is skipped."
- `Finish()`: Racing -> Results, returns `false` otherwise. "Prevents a double finish."
- `SetState()` (private): sets the state and fires `StateChanged`.

### 3.4 `RaceFlow/RaceTimer.cs` (plain C#)

- `ElapsedSeconds`, `IsRunning`; `Start(startTime)` (from 0, running, remembers the GO timestamp), `Stop(stopTime)` (freeze; `ElapsedSeconds` becomes `stopTime - startTime`, the official lap time; a second call does nothing), `Reset()` (0, stopped).
- `Tick(deltaTime)`: adds time only while running and only if `deltaTime > 0`. Only for the live timer on the HUD.
- `SecondsSinceStart(now)`: exact time since GO from the timestamps, never negative (used for checkpoint splits).
- `static ToMilliseconds(seconds)`: rounds once with `Math.Round`. The HUD compares these ints to know when a text must change.
- `static Format(seconds)` / `FormatMilliseconds(ms)` -> `"1:23.456"`: splits whole milliseconds into minutes, seconds, ms. "Rounding once at the start means 59.9996 s becomes 1:00.000, never 0:60.000." Built in one reused `char[]` buffer (`WriteNumber` fills digits from the right), so only the final string is created.
- `static FormatDelta(delta)` / `FormatDeltaMilliseconds(ms)` -> `"-1.204"` / `"+0.500"`: always with a sign, and the sign comes from the rounded value, so -0.0004 s shows "+0.000", never "-0.000".
- How I would explain it: "The timer does not read the clock itself. The RaceManager passes it `Time.timeAsDouble` at GO and at the finish line, and `Time.deltaTime` every frame for the live display, so in tests I can feed fake times. Inside `OnTriggerEnter` Unity returns the time of the physics step, so the finish is measured at the physics step where the kart crossed the line, not at the next drawn frame."

### 3.5 `RaceFlow/RaceSettings.cs` (ScriptableObject)

`[CreateAssetMenu]` so it can be created from the Assets menu; the asset is `RaceFlow/Settings/RaceSettings.asset`. Fields: `trackSceneName = "Track_Campus"`, `fallbackTrackSceneName = "RaceFlow_PlaceholderTrack"`, `countdownFrom = 3`, `countdownStepSeconds = 1`, `retryCountdownStepSeconds = 0.5`, `splitPopupSeconds = 2`, `hintSeconds = 2.5` (how long "Missed a checkpoint!" stays), `fallRecoverHeight = -20`, `fallCheckCooldownSeconds = 0.5` (pause of the fall check after a recover). Read-only properties expose them.

- How I would explain it: "A ScriptableObject is a data asset that lives in the project, not in a scene. Designers change numbers in the Inspector without touching code, and several scripts share the same asset."
- Concept: `[SerializeField] private` = private in code (nobody else can change it), but Unity saves it and shows it in the Inspector. `[Tooltip]` = hover help.

### 3.6 `RaceFlow/RaceKeys.cs` (static helper, new Input System)

- `AnyKeyOrClickPressed()`: left mouse button, or a key that `CountsAsStartKey`. First a quick `anyKey.wasPressedThisFrame` check, then a `for` loop over `keyboard.allKeys` (a `for`, not `foreach`, so no garbage).
- `CountsAsStartKey(Key)`: false for modifiers (Shift, Ctrl, Alt, Windows/Cmd), F1 to F24 and system keys (Escape, Tab, Caps Lock, Print Screen ...). "Alt-Tab or F11 for fullscreen must not start the countdown by accident." Public so `RaceKeysTests` can check it.
- `RetryPressed()`: Enter or numpad Enter.
- `RecoverPressed()`: R.
- Every method checks `Keyboard.current != null` (no keyboard = null).
- How I would explain it: "All race keys in one place. `wasPressedThisFrame` is true only in the one frame the key goes down, so holding Enter does not retry 60 times a second."
- Concept: Input System (new). The kart uses `isPressed` (held), race flow uses `wasPressedThisFrame` (tap).

### 3.7 `RaceFlow/CheckpointRelay.cs` (MonoBehaviour)

Added by `RaceManager.SetUp` at runtime, not to the checkpoint object itself but to every object under it that owns a trigger collider (Unity sends `OnTriggerEnter` only to the object with the collider, and Noah may put it on a child). `SetUp(manager, index)` stores both. `OnTriggerEnter(Collider other)`: if `raceManager.IsKartCollider(other)`, call `raceManager.OnCheckpointEntered(index)`.

- How I would explain it: "The track should not know about the race. So I glue a tiny messenger onto each checkpoint trigger. It only says 'the kart touched checkpoint N'; the rules decide if that counts."
- Concept: triggers. A collider with `isTrigger` does not block, it only reports. `OnTriggerEnter` needs a Rigidbody on at least one of the two objects (the kart has one). `Collider.attachedRigidbody` = the Rigidbody that moves this collider.

### 3.8 `RaceFlow/PersonalBest.cs` (plain C#)

Best lap time plus the time at each checkpoint of that best lap (for the "-0.412" split).

- `BestSeconds` (-1 = none), `HasRecord` (`BestSeconds > 0`).
- Two arrays: `bestSplits`, `currentSplits` (-1 = unknown).
- `StartRun()`: clears the current splits, keeps the record.
- `RecordSplit(index, time, out delta)`: stores the split; returns `true` with `delta = time - bestSplit` only if the best lap has a split there. Out-of-range index returns `false`.
- `FinishRun(lapSeconds)`: new record only if faster (a tie is not a record); then copies current splits into best splits.
- `ToSaveString()`: `"41.25|10.5;22.25;-1"` with `CultureInfo.InvariantCulture`. "A Portuguese laptop would write 41,25 with a comma and break loading."
- `static FromSaveString(text, checkpointCount)`: parses; broken text = no record; if the checkpoint count changed, keeps the time but drops the splits.
- Concept: `out` parameter (a method returning two values), PlayerPrefs only stores simple strings/ints/floats, hence the text format.

### 3.9 `RaceFlow/RaceManager.cs` (MonoBehaviour, the race brain)

Lives on `RaceSystems` in `Main.unity`. Owns a `RaceTimer`, a `RaceStateMachine`, `PlaceholderLapRules`, `PersonalBest`, and references to the track and the kart (`IKartState`).

Public read-only values for the HUD: `State`, `CountdownNumber`, `LapSeconds`, `BestSeconds`, `PreviousBestSeconds`, `LastLapWasRecord`, `HasSplitDelta`, `LastSplitDelta`, `SplitVisibleUntil`, `HintText`, `HintVisibleUntil`, `Kart`, `IsSetUp`. "`{ get; private set; }`: everyone can read, only the RaceManager can write."

Events: `CountdownStep`, `CheckpointPassed`, `LapFinished(float time, bool record)`, `Restarted` (the camera snaps behind the kart, RaceSounds clicks), `Recovered` (RaceSounds plays the respawn whoosh).

- `SetUp(kartObject, track, trackName)` (~line 50): gets `IKartState` from the kart (error if missing); remembers all Rigidbodies and colliders of the kart (`GetComponentsInChildren`, right after spawning, before the kart's own `Start` could unparent a physics sphere); `CheckCheckpoints` (red errors for an empty entry, a checkpoint without a trigger collider, or indices that are not exactly 0..N-1 via `CheckpointValidation.FindProblem`); creates the lap rules and the `FallCheck`; loads the best time from `PlayerPrefs` with key `"CarcavelosCircuit.Best." + trackName`; creates the state machine and subscribes `OnStateChanged` and `OnCountdownStep` with `+=`; `AddRelays` for every checkpoint; puts the kart on the grid; `InputEnabled = false`. "Called once by the bootstrap, not in Start, because the kart does not exist yet when the scene starts."
- `IsKartCollider(other)`: true if `other` is one of the remembered kart colliders, or its `attachedRigidbody` is one of the remembered kart Rigidbodies, or it has `IKartState` on itself or a parent. "Sphere controllers often unparent their physics sphere, then the parent check alone would never see the kart."
- `OnDestroy()`: unsubscribes with `-=`. "Always remove listeners you added, otherwise a destroyed object can still be called."
- `Update()` (~line 89), the heart:
  1. Not set up yet: do nothing.
  2. `kart.InputEnabled = stateMachine.InputAllowed`, every frame. "If the kart switched its own input on in its Start, this switches it off again, so it can never drive before GO."
  3. Ready: wait for any start key or click, then `BeginCountdown(CountdownStepSeconds)`.
  4. `timer.Tick` then `stateMachine.Tick` with `Time.deltaTime`. "Timer first: on the GO frame the timer is not running yet, so the GO frame does not count as race time."
  5. Racing or Results + Enter: `Retry()`.
  6. Racing + R or `fallCheck.HasFallen(height, Time.time)`: `Recover()`.
- `Retry()` (public, also called by the HUD button): ignored in Ready; kart back to the grid (`ResetTo` also clears drift/boost), `rules.Reset()`, `timer.Reset()`, clears record/split/hint flags, quick countdown, fires `Restarted`.
- `Recover()` (public, guarded: only when set up and Racing): `track.GetRespawnPoint(rules.LastValidCheckpoint)` then `kart.ResetTo(...)`, `fallCheck.NoteRecovered(Time.time)`, fires `Recovered`. Timer keeps running, no checkpoint awarded. "Recovering costs you time, that is the penalty."
- `OnCheckpointEntered(index)`: only while Racing. Index 0 = start/finish: `rules.TryFinish()` -> `FinishLap()`; if it fails after at least one checkpoint, hint "Missed a checkpoint! Press R to go back". Other index: `rules.PassCheckpoint(index)`; if valid, record the split with `timer.SecondsSinceStart(Time.timeAsDouble)`, show it for `SplitPopupSeconds`, fire `CheckpointPassed`.
- `FinishLap()` (private): `stateMachine.Finish()` (guards double finish), `timer.Stop(Time.timeAsDouble)` (we are inside `OnTriggerEnter`, so this is the physics step's time), remember `PreviousBestSeconds`, `personalBest.FinishRun`; if record: `PlayerPrefs.SetString` + `PlayerPrefs.Save()`; fire `LapFinished`.
- `OnStateChanged(newState)`: `kart.InputEnabled = stateMachine.InputAllowed`; when entering Racing: `rules.StartLap()`, `personalBest.StartRun()`, `timer.Start(Time.timeAsDouble)`.
- `OnCountdownStep(number)`: forwards the state machine's event to RaceManager's own `CountdownStep` (so HUD and sounds only need to know the RaceManager).
- `PutKartOnStartGrid()`, `ShowHint(text)` (visible for `settings.HintSeconds`).
- Helpers in their own plain C# files: `CheckpointValidation.FindProblem(indices)` returns "" or a sentence ("checkpoint index 3 is outside 0..2", "used twice"); `FallCheck` (`HasFallen(height, now)` is false during `fallCheckCooldownSeconds` after `NoteRecovered`, so a physics kart that only reaches the respawn point in the next physics step is not recovered several times).
- Concepts: MonoBehaviour lifecycle (`Update` every frame, `OnDestroy`), events/delegates (`Action<T>`, `+=`/`-=`, null check before invoking), PlayerPrefs (on Web stored in the browser, IndexedDB), `Time.time` for "visible until" timestamps.

### 3.10 `RaceFlow/RaceBootstrap.cs` (MonoBehaviour)

Entry point of `Main.unity` (task R1). Serialized: `settings`, `kartPrefab`, `kartAttachments[]` (the KartAudio prefab), `raceManager`, `chaseCamera`.

- `IEnumerator Start()` (~line 28): a coroutine. Loads the campus scene, looks for a track; if none, it logs a yellow warning (on swap day Noah's `TrackInfo` may already be there while this code still looks for `PlaceholderTrack`, so the warning says "do the swap"), unloads the campus scene (`SceneManager.UnloadSceneAsync`, so its test camera or lights do not end up in the race) and loads the fallback placeholder scene; error if still none. Then `SetActiveScene(track scene)` (its lighting and sky are used), `TurnOffTestCameras(track scene)`, `Instantiate(kartPrefab, spawn.position, spawn.rotation)`, instantiates each attachment as a child of the kart, `raceManager.SetUp` first, and only then, if a chase camera is set, `chaseCamera.SetTarget` and `Restarted += chaseCamera.SnapBehindTarget`. "The race comes first: once the placeholder camera is removed the field is empty, and that must never stop the race from being set up." `OnDestroy` removes the listener again.
- `LoadSceneIfNeeded(name)`: skip if already loaded (both scenes open in the Editor); warn if not in the build list (`Application.CanStreamedLevelBeLoaded`); else `yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)`.
- `TurnOffTestCameras(name)` (~line 90): in the loaded track scene, every GameObject with a `Camera` is switched off (`SetActive(false)`) and every `AudioListener` disabled. "Owners may keep a test camera in their own scene. In the race only Main's camera may render and listen: two AudioListeners give a Unity warning, two cameras the wrong picture."
- `FindTrackIn(name)`: loops over the scene's root objects with `GetComponentInChildren<PlaceholderTrack>()`. "No `GameObject.Find`, which is slow and breaks when someone renames an object."
- How I would explain it: "Additive loading means the track scene is added on top of Main instead of replacing it. Each owner has their own scene, so we never get Git merge conflicts in scene files. A coroutine lets Start wait for the loading without freezing the game."
- Detail you may be asked: because the track scene is made active before `Instantiate`, the spawned kart belongs to the track scene, not to Main.

### 3.11 `RaceFlow/RaceSounds.cs` (MonoBehaviour)

Race interface sounds on two 2D `AudioSource`s (one for sounds that play now, one for sounds that start a moment later). `OnEnable` subscribes to `CountdownStep`, `CheckpointPassed`, `LapFinished`, `Restarted`, `Recovered`; `OnDisable` unsubscribes. Volume is 1.0 for everything: the loudness of each sound lives in the file (`process_wavs.py`).

- `OnCountdownStep(n)`: low beep for 3, 2, 1, high GO beep for 0. On a retry the first beep waits 0.1 s (`PlayLater`), because `RaceManager.Retry()` fires beep "3" and the `Restarted` click in the same frame and the beep would hide the click. A retry is recognised by the state of the last frame (`stateLastFrame` was Racing or Results).
- `OnCheckpointPassed`: chime. `OnLapFinished`: jingle; on a new record the sting starts 0.7 s later (`recordStingDelay`), so the two do not blur into one sound. `OnRestarted`: click (`ui_click`). `OnRecovered`: whoosh (`sfx_respawn`).
- `Update()`: plays `sfx_hint_warning` once when a new "Missed a checkpoint" hint appears. There is no hint event, but `HintVisibleUntil` moves forward every time a hint is shown, so "later than last frame" = new hint.
- Every stinger calls `music.Duck(dB, 0.6 x clip length)`: the music dips under it and comes back.
- `Play(clip)`: `source.PlayOneShot(clip, volume)`, skips missing clips. `PlayLater(clip, delay)`: `delayedSource.PlayDelayed(delay)`; a new one replaces a waiting one (a retry stops a record sting).
- Concept: `OnEnable`/`OnDisable` are the right pair for subscribing, they also run when the component is switched on and off.

### 3.11b `RaceFlow/RaceMusic.cs` (MonoBehaviour, on the Music object)

The music and the overall mix, following the race. Four `LevelFade`s (race music, results music, engine, kart effects) and one `DuckEnvelope`, all in dB, turned into `AudioSource.volume` with `Decibels.ToVolume` every frame.

- Title screen: race music at -10 dB. Countdown (`CountdownStep` 3, 2, 1): music fades out in 0.3 s, engine -3 dB, effects -6 dB.
- GO (`CountdownStep(0)`): race music restarts from bar 1 (`timeSamples = 2048`, right after the loop padding) at normal pitch and fades in over 0.12 s. The music is 120 BPM, so its beats line up with the 1 s and 0.5 s countdown steps.
- Final stretch: when the last checkpoint before the finish is passed, the music glides up one semitone (pitch 1.0595 over 2 s, `Mathf.MoveTowards`) and gets 3 dB louder. The last checkpoint is `RaceManager.CheckpointCount - 1` (index 0 is the start/finish line), so the lift plays on every lap, the first one too.
- Finish (`LapFinished`): race music fades out, engine and effects -14 dB (`KartAudio.SetMixLevels`), the results loop starts 1.8 s later (`PlayDelayed`), after the jingle.
- `Duck(db, hold)`: called by `RaceSounds`. Down at 200 dB/s, hold, back at 15 dB/s: "the slow return is what makes it sound like a radio mix pumping, not a volume jump".
- `OnApplicationFocus` / `OnApplicationPause` -> `AudioListener.pause`: in a hidden browser tab Unity stops updating, but the browser would keep playing the engine and music loops; this pauses every source.
- The kart is spawned at runtime, so the `KartAudio` is looked up once it exists: `(raceManager.Kart as Component).GetComponentInChildren<KartAudio>()`.
- How I would explain it: "A mixer snapshot is just a set of volumes plus a fade time. The Web build cannot do more than volumes, so I wrote those fades myself in four lines of plain C#, which I can also unit test."

### 3.12 `RaceFlow/RaceHud.cs` + `UI/RaceHud.uxml` + `UI/RaceHud.uss`

`RaceHud.uxml`: the layout, like HTML. Named elements: `timer`, `best`, `split`, `hint`, `countdown`, `boost-label`, `boost-fill`, `speed`, `start-screen` (with `press-any-key`, `start-best`), `results-screen` (with `record`, `final-time`, `final-delta`, `retry-button`). It links `RaceHud.uss` with `<Style src=...>`.

`RaceHud.uss`: the look, like CSS. Colours from the art style (white #EAEAEC, black #1D1D1F, blue #2D8CD6, orange #FF8A1F). Classes toggled from code: `countdown-go` (orange GO), `split-ahead` (blue) / `split-behind` (red), `boost-stage-1` (blue) / `boost-stage-2` (orange), `boost-active`. `position: absolute` overlays; Unity-only properties start with `-unity-` (`-unity-text-align`, `-unity-font-style`, `-unity-text-outline-*`).

`RaceHud.cs`: `[RequireComponent(typeof(UIDocument))]` (Unity adds the UIDocument automatically and stops you removing it).

- One `ShownNumber` per label (`shownTimer`, `shownSpeed`, `shownCountdown` ...): `Changed(value)` is true only when the number on screen is different, then the HUD builds the new text. Timer and best are compared in whole milliseconds, speed in km/h, the boost bar in half-percent steps.
- `OnEnable()`: `root.Q<Label>("timer")` etc. finds every element by name; `ForgetShownNumbers()` (new labels, so everything is written once); sets `retryButton.focusable = false` and subscribes the button (`retryButton.clicked += OnRetryClicked`), `CountdownStep` and `LapFinished`. `OnDisable()` unsubscribes. "Not focusable: otherwise, after a mouse click, the button keeps keyboard focus and Space (drift) or Enter in the next race would press it again."
- `OnRetryClicked()`: `raceManager.Retry()`.
- `OnCountdownStep(0)`: remembers `goVisibleUntil = Time.time + 0.8 s`.
- `OnLapFinished(time, record)`: fills the results screen once per lap: final time, "NEW RECORD!", delta to the previous best or "First lap on this track".
- `Update()`: shows the start screen only in Ready, results only in Results; updates the start screen only in Ready, then the other parts. "The HUD only reads the RaceManager, it never changes the race (except the Retry button, which calls the same public `Retry()` as the Enter key)."
- `UpdateStartScreen()`: blinking "Press any key" with `Mathf.Sin(Time.time)`; "Loading track..." until `IsSetUp`; "Your best: ...".
- `UpdateCountdown()`: number during Countdown, "GO!" for 0.8 s after.
- `UpdateTimes()`: `RaceTimer.FormatMilliseconds` for lap and best (`--:--.---` if none), only when the millisecond value changed.
- `UpdateSplitAndHint()`: split only if racing, there is a delta, and its time has not run out; colour from the rounded delta.
- `UpdateKartInfo()`: km/h = `|Speed| * 3.6`; boost bar width = `Length.Percent(...)`; label "DRIFT", "DRIFT  1", "DRIFT  MAX", "BOOST!" and the stage classes only when stage or boost change.
- `SetVisible()`: `style.display = Flex / None`, only written when it changes.
- Concept: polling (HUD reads every frame) vs events (sounds and the results screen react once). Garbage: every new string is memory the garbage collector must clean; in a Web build it cleans all at once at the end of a frame, so building 20 strings per frame shows as small hitches.

### 3.13 `RaceFlow/Placeholders/` (stand-ins, deleted on swap day)

- `PlaceholderCheckpoint`: `Index` (0 = start/finish) and `RespawnPoint` (serialized). Same idea as Noah's `Checkpoint`.
- `PlaceholderTrack`: `Checkpoints[]`, `SpawnPoint`, `GetRespawnPoint(lastIndex)` (loops for the matching index, falls back to the spawn point). Same members as `TrackInfo`.
- `PlaceholderLapRules` (plain C#): exactly the `RaceRules` API. `nextExpected` starts at 1; `PassCheckpoint(i)` true only if `i == nextExpected` and `0 < i < count`; `TryFinish()` true only when every checkpoint was passed, then sets `nextExpected = count + 1` so a second call fails; `LastValidCheckpoint` (0 = start); `StartLap()` and `Reset()`.
- `PlaceholderKart : MonoBehaviour, IKartState` with `[RequireComponent(typeof(Rigidbody))]`. A sliding box: input read in `Update` (W/S/A/D, arrows, Space) only if `InputEnabled`; physics in `FixedUpdate` with `Time.fixedDeltaTime`: ground raycast for `IsGrounded`, accelerate/brake/roll out, remove sideways speed (less while drifting), cap at top speed (x1.35 while boosting), steer with `body.MoveRotation`. Drift: start with Space + steer + > 40 % speed (speed over the ground, not forward speed: a drifting kart points into the corner, so forward speed drops and the drift would cancel itself); while drifting the kart turns at 0.8x, keeps more slide and keeps its ground speed (the slide turns into forward speed, little drag), so a 1.6 s drift is possible; stage 1 at 0.8 s, stage 2 at 1.6 s; release = boost 0.4 s or 1.2 s; if `InputEnabled` goes false while drifting (finish line, results), the drift ends without a boost, so the kart does not surge after the finish. `ResetTo` sets transform and body, zeroes `linearVelocity` and `angularVelocity`, clears drift/boost.
  - "Input in Update, physics in FixedUpdate: Update runs once per drawn frame, FixedUpdate at a fixed 50 times per second, so physics behaves the same on fast and slow laptops."
  - "Unity 6 renamed `velocity` to `linearVelocity`."
- `PlaceholderChaseCamera` with `[RequireComponent(typeof(Camera))]`: `Awake` caches the Camera; `SetTarget` + `SnapBehindTarget`; `LateUpdate` follows with `1 - Mathf.Exp(-sharpness * Time.deltaTime)` (frame-rate independent), snaps if more than 20 m away (teleport), widens FOV 65 to 80 with speed. "LateUpdate runs after all Updates, so the kart has already moved when the camera follows it."

### 3.14 `Audio/EnginePitch.cs` and `Audio/DriftSound.cs` (static, plain C#)

The sound maths, kept out of MonoBehaviours so it can be unit tested. All numbers come from Luc's audio handoff (8 Nov).

- `EnginePitch.Speed01(speed, maxSpeed)`: `|speed| / maxSpeed` capped at 1, 0 if `maxSpeed <= 0` (no divide by zero).
- `EnginePitch.MoveLimited(current, target, rise, fall, dt)`: moves towards the target but at most `rise` (1.4) per second up and `fall` (2.2) per second down. "The revs never jump, and they drop a bit faster than they rise, like a real engine."
- `EnginePitch.HighLoopMix(r)`: share of the high engine loop. 0 below r = 0.2, 1 above 0.7, smoothstep `t * t * (3 - 2t)` in between. "Smoothstep starts and ends flat, so you never hear where the fade begins."
- `EnginePitch.LowLoopPitch(r) = 0.75 + 0.85r`, `HighLoopPitch(r) = 0.8 + 0.55r`.
- P4: `EqualPowerLow(mix) = cos(mix * pi/2)`, `EqualPowerHigh(mix) = sin(mix * pi/2)`. "The two recordings are different sounds, so a straight 50/50 split is about 3 dB quieter in the middle. With cos and sin, low^2 + high^2 is always 1: same loudness."
- P4: `GearTop(gear, gears) = (gear / gears)^0.85`: where a pretend gear runs out of revs (4 gears: 0.31, 0.55, 0.78, 1; lower gears are shorter). `GearRevs(speed01, gear, gears, 0.6)`: revs inside the gear, from 0 (gear 1) or 0.6 (after an upshift) to 0.97 at the top of the gear.
- P4 plain classes: `VirtualGearbox` (`Update(speed01)` shifts up at 98.5 % of the gear top and returns true for the pop, shifts down only below 90 % of the change point, so it never flutters), `ThrottleGuess` (`IKartState` has no throttle: smoothed change in speed > 0.4 m/s^2 = full load, < -0.8 = no load, else 0.55), `Decibels.ToVolume(db) = 10^(db/20)` (-80 dB = exactly 0), `LevelFade` (glide to a dB target in a given time), `DuckEnvelope` (duck with hold; the deeper duck wins).
- `DriftSound.StageUpPitch(stage) = 1 + 0.12 (stage - 1)`, `SparksVolume(stage) = 0.55 + 0.15 stage` (0 without sparks, capped at 1), `SparksPitch(stage) = 0.95 + 0.07 stage`. "Written for any number of stages, so it still works if Role 1 changes the drift to three stages (Luc's request Q1)."

### 3.15 `Audio/KartAudio.cs` (MonoBehaviour, on the KartAudio prefab under the kart)

Nine 2D AudioSources: five loops that always run (engine low, engine high, tyre squeal, sparks, wind; only their volume changes, because starting a loop late can click) and four one-shot sources (two stage-chime sources that take turns, effects, and the gear-change pop).

- `Start()`: `GetComponentInParent<IKartState>()`; if none, warn and `enabled = false`. Starts every loop at volume 0.
- `UpdateEngine` (P4): two smoothed values. `revs` = revs inside the pretend gear (`VirtualGearbox` + `GearRevs`, +0.1 while boosting or airborne, rise 2.5/s, fall 2.2/s) drives the **pitch**, so you hear the engine climb, drop on the upshift and climb again. `speedRevs` = smoothed speed drives the **mix** of the two loops (`HighLoopMix` + equal power) and the volume (0.45 to 0.75), so the high loop does not fade out on every shift. On an upshift: 0.07 s at 40 % volume (ignition cut) plus `sfx_shift_pop` at a random pitch 0.92-1.08. `ThrottleGuess`: off the throttle the pitch sags up to 6 % and the volume drops to 60 %. A Perlin-noise wobble of +-0.2 % keeps it from sounding like a machine. While `InputEnabled` is false: target 0, back to first gear.
- `SetMixLevels(engine, effects)`: volume factors from `RaceMusic` (quieter on the results screen).
- Teleport check at the start of `Update`: if the kart moved more than `teleportDistance` (10 m) in one frame, it was put back (R, fall, retry), so `airSeconds = 0` and `wasGrounded = true`. "Audio may only read `IKartState`, it cannot hear the RaceManager's `Recovered` event. Without this, landing on the respawn point after a fall would play a thud on top of the whoosh."
- `UpdateDrift`: chirp on the frame a drift starts; squeal fades in/out with `Mathf.MoveTowards`; sparks volume/pitch from `DriftSound` by stage; chime each time the stage goes up, pitched higher per stage, alternating between `stageSource` and `secondStageSource`. "The chime is 1.2 s long and stage 2 comes 0.8 s after stage 1. Changing the pitch of a source that still rings would bend the first chime."
- `UpdateWind`: volume `0.35 * speed01^2` (only audible when fast), pitch 0.8 to 1.2. "The cheapest way to make speed feel fast."
- `UpdateBoostAndLanding`: whoosh on the first frame of a boost (`IsBoosting && !wasBoosting`); landing thud after at least 0.3 s in the air.
- How I would explain it: "Two engine recordings, low and high revs. Playing a recording faster raises its pitch like a tape, and between low and high revs one fades into the other. I remember last frame's values (`wasDrifting`, `wasBoosting`, `lastDriftStage`, `wasGrounded`) to detect the moment something starts."
- Concept: edge detection with a 'was' flag; `Audio` asmdef only references `Shared`.

### 3.16 `Audio/MusicPlayer.cs` (no longer used in Main since P4)

`[RequireComponent(typeof(AudioSource))]`; `Start()` sets loop, volume 0.3, 2D, plays the clip if set. Replaced in `Main.unity` by `RaceFlow/RaceMusic` (3.11b); the file stays until it is deleted in the Unity Editor. "Browsers mute audio until the first input; Unity resumes it then, and the start screen guarantees that input."

### 3.17 `RaceFlow/Editor/Role4SceneBuilder.cs` (editor only)

Menu **Carcavelos Circuit > Build Role 4 Scenes** (`[MenuItem("Carcavelos Circuit/Build Role 4 Scenes")]`), or batch: `-executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4SceneBuilder.BuildAll`.

`BuildAll()` does, in order:
0. Safety first: `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` (stops if you press Cancel), then `MayRebuildMain()`: `FindHandEditsInMain()` opens Main and checks that the builder's own root objects (`Main Camera`, `PostProcessing`, `RaceSystems`, `HUD`, `Music`, `Ghost`, listed in `BuilderComponents`) have no children and no components the builder did not add. If they do, batch mode stops with an error and exit code 1, the menu asks "Rebuild anyway / Cancel".
1. Ensures folders.
2. `CreateSettings()`: loads or creates `RaceSettings.asset` (existing values kept) and calls `EditorUtility.SetDirty`, so fields added to `RaceSettings` later (like `hintSeconds`) are written into the file with their defaults.
3. `CreatePanelSettings()`: theme file + PanelSettings (scale with screen size, 1280x720, match 0.5).
0b. `SetUpAudioImport()` -> `AudioImportSetup.ApplyAll()` (own file, also menu **Carcavelos Circuit > Apply Audio Import Settings** or batch `-executeMethod CarcavelosCircuit.RaceFlow.Editor.AudioImportSetup.ApplyAll`): every clip in `Audio/Clips` gets Normalize off (Unity would push each clip to full peak and undo our loudness levels; no public property, so via `SerializedObject` field `m_Normalize`), Decompress On Load + Preload (also the music: only then does the Web build use the WAV loop points), Web override AAC with quality 1.0 (effects) or 0.7 (music). Only clips whose settings differ are re-imported.
4. `CreateKartAudioPrefab()`: 9 AudioSources + `KartAudio`, clips `kart_engine_low/high`, `kart_drift_squeal/sparks/start/stage_up` (Luc's), `amb_wind_loop`, `el_boost_whoosh`, `sfx_landing_thud`, `sfx_shift_pop`. Every source gets `dopplerLevel = 0`. Private fields are filled with `SerializedObject.FindProperty(...)`.
5. `FindRealKartPrefab()` (~line 199): `AssetDatabase.FindAssets("Kart t:Prefab")` under `Assets/_Project/Driving`; uses a prefab only if it is named exactly `Kart` and its root has a component implementing `IKartState`. "The kart swap is automatic: once Alessandro's prefab exists, re-running the builder puts it into Main."
6. Only if no real kart was found: `CreatePlaceholderKartPrefab()`: Rigidbody (150 kg, interpolate, continuous collision, X/Z rotation frozen), frictionless box collider, `PlaceholderKart`, a `Visual` child made of primitives.
7. `BuildPlaceholderTrackScene()`: a ~230 m rectangular loop around a sand "building", walls, cones, a kicker ramp, 4 checkpoint triggers (15 m wide) each with a `RespawnPoint` child 4 m after it, and a `SpawnPoint` just after the start line.
8. `BuildMainScene()`: rebuilds Main in place: opens the existing `Main.unity`, deletes only the builder's own root objects and keeps every other root object (logged as "kept ..."), so Role 1 can add a camera rig as its own root object. Then camera + AudioListener + chase camera, with `GetUniversalAdditionalCameraData().renderPostProcessing = true`; a global `Volume` object "PostProcessing" using `CreateVolumeProfile()`; `RaceSystems` (RaceManager, RaceBootstrap, two AudioSources, RaceSounds), `HUD` (UIDocument + RaceHud), `Music` (race music + results music AudioSources, RaceMusic), `Ghost` (GhostRecorder + GhostPlayer, see 3.17b). All references wired.
9. `CreateVolumeProfile()` (called from step 8): `RaceFlow/Settings/RaceCameraVolume.asset` with Bloom (threshold 0.9 = only very bright pixels glow, intensity 0.6, scatter 0.6). If the asset already exists it is returned unchanged, so Role 3's tuning survives a rebuild.
10. `MergeBuildScenes()`: Main, Track_Campus, RaceFlow_PlaceholderTrack first (ticked), then every other scene that was already in the build list, with its tick box. Merged, never replaced. (`ProjectSetup.SetBuildScenes` in Shared/Editor still writes only the three; run the builder after it if someone added scenes.)

The editor asmdef references `Shared`, `Shared.Editor`, `RaceFlow`, `Audio` and, for the Volume and Bloom types, `Unity.RenderPipelines.Core.Runtime` and `Unity.RenderPipelines.Universal.Runtime`. All six are recorded in `docs/decisions.md`.

- How I would explain it: "Scene files are huge YAML that merge badly in Git and nobody can review. So the scene is the output of code: anyone can rebuild it identically, and the code is what we review."
- Concept: `SerializedObject` is how editor code sets `[SerializeField] private` fields, the same way the Inspector does.
- Concept: post-processing in URP = the camera must allow it, and a `Volume` (global = everywhere) applies a `VolumeProfile` asset with effects such as Bloom.

### 3.17b `RaceFlow/Editor/DeansGhostExporter.cs` (editor only)

Menu **Carcavelos Circuit > Save My Best Ghost As Dean's Ghost**. Reads the ghost the game saved in the Editor's PlayerPrefs (campus track first, then the test loop) and writes it into `RaceFlow/Settings/DeansGhost.txt`, the `TextAsset` that `Main.unity` already points to (the builder creates the file with a placeholder line, so the reference always exists). Recording the Dean's ghost: play Main in the Editor, set a personal best, stop Play Mode, click the menu, commit the `.txt`. Re-record after every track change.

The builder (3.17) also makes the `Ghost` root object in Main (`GhostRecorder` + `GhostPlayer`, listed in `BuilderComponents`) and `RaceFlow/Materials/Ghost.mat` (`CreateGhostMaterial()`: URP Lit with `_Surface` = 1 transparent, alpha blending, no depth write, no shadows, the same properties and keywords URP's material Inspector sets).

### 3.18 `RaceFlow/Editor/Role4WebBuild.cs` + `Role4WebQuality.cs` + the Web template

Menu **Carcavelos Circuit > Web Build**, or from the command line (Unity closed):
```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4WebBuild.Build -quit -logFile Logs/web-build.log
```
What `Build()` does, in order:
1. `ApplyWebSettings()` sets every Web setting in code, one line each with the reason: small WebAssembly (`UserBuildSettings.codeOptimization = DiskSize`; the "with LTO" variant, link time optimisation, ran over an hour and failed on our laptop), IL2CPP Master, Managed Stripping High, Wasm 2023, exceptions "explicitly thrown only", compression Disabled + hashed file names, data caching, 256 MB starting memory, no Unity splash, static batching, our template. `-webHost=itch` switches compression to Gzip.
2. `Role4WebQuality.SetUp()` makes sure `RaceFlow/Settings/Web_RPAsset.asset` (copy of the Mobile URP asset, render scale 1.0, MSAA 2x, created once, Luc tunes it) and the quality level "Web" exist, and that Web uses only that level. Quality levels have no "add" method, so it uses `SerializedObject`, like the Project Settings window does.
3. During the build `GraphicsSettings.defaultRenderPipeline` is the Web asset (so the PC asset's shader variants stay out); `finally` puts the editor's asset back even if the build fails.
4. Deletes the old `Build-Web/` (hashed names would pile up), builds, logs all effective settings and every file with its size and gzip size, exits with code 1 in batch mode if the build failed.

SIZES_PLACEHOLDER

The template `Assets/WebGLTemplates/CarcavelosCircuit/` (`index.html` + `TemplateData/style.css`) is plain HTML/CSS/JS. Unity fills in the `{{{ ... }}}` parts (file names, product name) when it builds.
- The canvas is `position: fixed; inset: 0` = always the whole window, no scrollbars. Unity renders at that size times `devicePixelRatio`, which we cap at 1.5 (sharp on a laptop, but a 4K screen does not cost 4x the GPU).
- Loading screen: `createUnityInstance(canvas, config, onProgress)` calls `onProgress` with 0..1; we move the road fill and the kart dot. 0 to 0.9 is the download, 0.9 to 1 is starting the engine.
- There is no separate "Click to start" layer: the game's start screen already asks for any key, and a click on the canvas counts as a key and unlocks the sound (browsers only allow audio after a click or key).
- Errors: no WebGL 2, a file that fails to download, or an error from Unity show a readable box instead of a blank page.

- How I would explain it: "The build is code, like the scenes. Whoever builds gets the same small build, and the page is ours, not Unity's default box."
- Concept: compression. GitHub Pages compresses files itself but cannot send the header that Unity's pre-compressed `.gz` files need, so we ship plain files and let the host compress them.

### 3.19 `tools/audio-synth/synth_sfx.py`

Pure Python (only standard library: `math`, `random`, `wave`, `array`). Builds sounds from sine partials, filtered noise and envelopes and writes 16-bit mono 44.1 kHz WAV. `SOUNDS` maps file names to functions: countdown beep (440 Hz), GO beep (880 Hz), checkpoint chime, finish jingle, new-record sting, drift pings, `ui_click`, `sfx_respawn`, the `amb_wind_loop`, plus synth versions of boost/engine/drift sounds (`sfx_boost_whoosh`, `loop_engine`, `loop_drift`, `sfx_drift_stage1/2`, kept as CC0 backups; the prefab uses Luc's `kart_*` files and the ElevenLabs boost) and a 32 s music loop (120 BPM, A minor). `loop_crossfade()` fades the extra tail over the start, so loops have no click. Seeded `random.Random(seed)` = the same output every run.

Since P4 it also makes `sfx_hint_warning` (missed checkpoint), `sfx_shift_pop` (gear change) and `music_results_loop` (16 s, 120 BPM, C major).

`tools/audio-synth/process_wavs.py` is the mastering step, run after the synth script (standard library only):
1. Re-level by loudness (RMS) to a target per sound (for example countdown beep -15 dBFS, engine loops unchanged at about -16.5), never a peak above -1 dBFS. "Peak normalising makes a short beep and a long engine loop look equally loud but sound very different; RMS is closer to what we hear."
2. Loops: 2048 samples of silence at the start plus a `smpl` chunk (loop start 2048, loop end = last sample).
3. Safe to run twice (a padded file is un-padded first; a second run gives the same bytes). Only touches the files in its table; `.meta` files are never touched, so the GUIDs and references stay.

```
python tools/audio-synth/synth_sfx.py Assets/_Project/Audio/Clips
python -I tools/audio-synth/process_wavs.py Assets/_Project/Audio/Clips
```

### 3.20 `tools/serve_web_build.py`

Tiny local web server for `Build-Web/` on `http://127.0.0.1:8000`, standard library only. Our default build is uncompressed, so it gzips `.wasm`/`.data`/`.js` on the fly when the browser asks for gzip, like GitHub Pages does (each file is compressed once and kept in memory). For an itch.io build (`-webHost=itch`, pre-gzipped `.gz` files) it adds `Content-Encoding: gzip` and the right MIME type (`foo.wasm.gz` as `application/wasm`). Always `Cache-Control: no-store`, so a reload gets the newest build.

```
python tools/serve_web_build.py
```

### 3.21 `RaceFlow/MedalRules.cs` + `Medal.cs` (plain C#, task P5)

`Medal` enum: `None = 0, Bronze, Silver, Gold, DeansList = 4` (bigger number = better medal, the number is what gets saved). `MedalRules` is static:

- `AreValid(thresholds)`: exactly 4 positive times, each strictly faster than the one before (Bronze 12.0 > Silver 11.0 > Gold 10.2 > Dean's List 9.6, from `RaceSettings.MedalThresholds()`). If not, `RaceManager.SetUp` logs a red error and there are no medals.
- `For(lapSeconds, thresholds)`: loops over the tiers and keeps the best one the lap is fast enough for. Compares whole milliseconds (`RaceTimer.ToMilliseconds`), so "exactly on the time" counts and a lap that reads 0:10.200 always earns a medal that reads 0:10.200.
- `NextTarget(medal)` (one tier up, `None` at the top), `ThresholdFor`, `Name` ("DEAN'S LIST"), `ColourClass` ("medal--gold").
- Saved by `PersonalBest`: `BestMedal`, `NoteMedal(medal)` (only goes up), save text `"10.1|splits|3"`. A save from before medals had no third part, so it loads as `None` and `SetUp` gives it the medal its best time is worth.
- HUD: pill under the time panel "GOLD 0:10.200" during countdown and racing (the next tier above your best medal); once the running time passes it, the pill dims and the gem turns hollow. Results: the medal badge stamps in at 900 ms in its own row (never over other text), with "NEXT  DEAN'S LIST  0:09.600" or "NEW MEDAL!" under it.
- How I would explain it: "Four numbers in the settings, one loop that finds the best tier you beat. The HUD only shows the result."

### 3.22 `RaceFlow/RunStats.cs` (plain C#, lap report)

`RaceManager.Update` calls `Tick(deltaTime, Speed, IsDrifting, IsBoosting, IsGrounded)` every racing frame, all from `IKartState`. `Reset()` at GO and on Retry, `NoteRecovery()` in `Recover()`.

- Rising edge: `if (drifting && !wasDrifting) Drifts++;`, same for boosts. A 3 s drift is one drift, not 180 frames.
- `TopSpeed` keeps the maximum of `|Speed|`; `AirSeconds` adds frame time while not grounded; `LongestJumpSeconds` keeps the longest single jump (reset on landing or a recover).
- Results card row: TOP KM/H, DRIFTS, BOOSTS, AIR TIME (`HudFormat.OneDecimal`, always a dot). 0 drifts or 0 boosts are dimmed.
- Concept: rising-edge detection (compare with last frame), the same idea as `wasPressedThisFrame` for keys.

### 3.23 `RaceFlow/OnboardingCoach.cs` + `CoachHint.cs` + `HintKind.cs` (plain C#, smart hints)

`RaceManager.UpdateCoach()` feeds it every frame: `Tick(deltaTime, racing, IsDrifting, DriftStage, IsBoosting)` returns a `CoachHint` or `None`.

- `HowToDrift`: 5 s of racing (`RaceSettings.coachNoDriftSeconds`, counted across retries, never during countdown or results) without a drift. A drift before that marks it done without showing it.
- `HoldLonger`: a drift ended while its highest stage was 0 (no sparks, no boost). The kart's `DriftStage` drops to 0 when the drift ends, so the coach remembers the highest stage while drifting.
- `BigBoost` (praise): a boost starts within 0.25 s after a stage 2 drift ended. It also marks `HoldLonger` done (they have learned it).
- `CoachHint` values are bit flags (1, 2, 4), so "already shown" fits in one int: `PlayerPrefs` key `CarcavelosCircuit.CoachDone`, saved the moment it changes. Returning players get no tips.
- A tip waits while another hint is on screen ("Missed a checkpoint!" wins) and uses the same `ShowHint`, so `HintText`/`HintVisibleUntil` stay the only hint channel. `HintKind` (Warning, Tip, Praise) picks the chip colour: orange "!", blue "i", sand "+".
- How I would explain it: "Three if-statements on the kart's flags, each tip once, a saved bit per tip."
### 3.24 `RaceFlow/GhostRecording.cs` (plain C#)

One recorded lap: the kart's position and rotation every 0.05 s (20 Hz) from GO.
- `Record(time, position, rotation)` is called every frame while racing, with the race timer. Frames come at uneven times but samples must be exactly 0.05 s apart, so each sample mark between the last frame and this one gets a pose blended between the two frames (`Vector3.Lerp`, `Quaternion.Slerp`). Max 2400 samples (120 s); a longer lap is not kept.
- `Sample(time, out position, out rotation)`: index = time / 0.05, blend between that sample and the next. Before the start = first pose, after the end = last pose, empty = `false`. If two samples are more than 8 m apart (a teleport after R or a fall; 8 m in 0.05 s = 576 km/h), it jumps instead of gliding through a wall.
- `EndVelocity()`: the last step per second, so the ghost can roll on past the line while fading out.
- `ToSaveString()` / `static FromSaveString(text, track, checkpointCount)`: `"G1|Track_Campus|8|41.250|840|120,30,-450,0,900,0;95,0,12,0,4,0;..."` = format, track, checkpoint count, lap time, sample count, then per sample the position in whole cm and pitch/yaw/roll in whole tenths of a degree. Only the first sample is the real value, later ones are the change since the sample before (yaw the short way round: 359.5 to 0.5 degrees is +10, not -3590). About 20 characters per sample, a 60 s lap about 23 KB. InvariantCulture everywhere. Wrong format, other track or other checkpoint count = `null` (ignored).
- How I would explain it: "A ghost is a list of where the kart was, 20 times a second. To show it at 12.37 s we take the two samples around that time and blend between them, so it moves smoothly at any frame rate."
- Concepts: interpolation (lerp/slerp), quantising (store 12.34 m as the whole number 1234), delta encoding (store the changes, they are small numbers).

### 3.25 `RaceFlow/GhostLoader.cs` + `RaceFlow/GhostFade.cs` (static, plain C#)

- `GhostLoader.Choose(ownSave, shippedText, track, checkpointCount, out isShipped)`: the player's own ghost if valid, else the shipped Dean's ghost, else `null`. `SaveKey(track)` = `CarcavelosCircuit.Ghost.<track>`, next to `CarcavelosCircuit.Best.<track>`.
- `GhostFade.ByDistance(distance, hide, full)`: 0 closer than `hide`, 1 further than `full`, a smooth S-curve (`Mathf.SmoothStep`) in between. `ByEnd(secondsPastEnd, fadeSeconds)`: 1 while driving, then down to 0. `Combined(...)`: the smallest of the three (near the kart, near the camera, after the lap).

### 3.26 `RaceFlow/GhostRecorder.cs` + `RaceFlow/GhostPlayer.cs` (MonoBehaviours on the `Ghost` object in Main)

- `GhostRecorder.LateUpdate()`: on the first racing frame (GO, also after a retry) `Clear()`, then `Record(raceManager.LapSeconds, kart pose)` every frame. `LateUpdate` so it stores the pose the player sees. `OnLapFinished(time, newRecord)`: only on a record, save `ToSaveString()` in PlayerPrefs (a lap over 120 s deletes the old ghost instead, so the ghost never belongs to a slower lap than the best time).
- `GhostPlayer.LateUpdate()`: once the race is set up, `BuildBody()` copies only the meshes of the kart's `Visual` child into a new `GhostKart` object (same place relative to the kart, every sub-mesh with our copy of `Ghost.mat`, no shadows). No Rigidbody, no collider, no script, so it can never hit the kart or count a checkpoint; no meshes = a kart-sized box. The ghost's clock: 0 in Ready and Countdown, exactly `LapSeconds` while racing (the same clock as the recording, so it is in sync), keeps counting in Results. After its last sample it rolls on, slowing to a stop, while fading out. Alpha = `maxAlpha` (0.4) x `GhostFade.Combined(distance to kart, distance to camera, time after the end)`: at GO it stands on your kart and is invisible, it appears as you separate, and it never fills the screen in front of the camera. Your own ghost is light blue, the Dean's ghost warm gold. `Restarted` reloads (a record from the lap before drives along from now on); the save text is only parsed again when it changed.
- How I would explain it: "The recorder writes, PlayerPrefs is the hand-over, the player reads. The ghost has no physics at all, it is only a picture we move every frame."

---

## 4. Tests

All in `Assets/_Project/Tests/EditMode/` (asmdef references Shared, Track, RaceFlow, Audio, Unity.InputSystem; editor only).

| File | What it proves |
|---|---|
| `RaceTimerTests` | No counting before Start; frame times add up; Stop freezes; Reset goes to 0; negative frame time ignored; official lap time comes from the timestamps (a minute of 60 fps frames, a long frame), a second Stop changes nothing, `SecondsSinceStart` never negative; `Format`/`FormatMilliseconds` (0:00.000, 1:23.456, 59.9996 -> 1:00.000, 100:00.000, negative -> 0); `FormatDelta` always signed, -0.0004 -> "+0.000". |
| `CheckpointValidationTests` | 0..N-1 in any order is fine; no checkpoints, numbering from 1, a duplicate or a negative index is reported. |
| `FallCheckTests` | Below the fall height = fallen; no second recover during the 0.5 s pause; a real fall after the pause counts. |
| `ShownNumberTests` | First value and new values count as changed, the same value does not; `Forget` makes the next one a change. |
| `RaceKeysTests` | Letters, Space, Enter, arrows start the race; Alt, Ctrl, Shift, Windows/Cmd, Tab, Escape, F1/F11/F12/F24, Print Screen do not. |
| `RaceStateMachineTests` | Starts in Ready with input off; countdown fires 3, 2, 1, 0 then Racing with input on; not over at 2.9 s; one 10 s frame still fires every step; Finish only while racing, no double finish, input off in Results; retry countdown at 0.5 s per step; StateChanged reports Countdown, Racing, Results; bad settings (0 steps, 0 s) cannot freeze the race. |
| `PersonalBestTests` | First lap is a record; slower or equal is not, faster is; no split delta without a best lap; deltas ahead (-0.5) and behind (+1); out-of-range index ignored; save string round trip; dot not comma; broken saves = no record; other checkpoint count keeps time, drops splits. |
| `PlaceholderLapRulesTests` | In order then finish works; skipped checkpoint cannot finish; same checkpoint twice counts once; no double finish; `LastValidCheckpoint` for recovery; Reset starts over. |
| `EnginePitchTests` | `Speed01` in 0..1 (capped while boosting, reverse counts, `MaxSpeed` 0 safe); `MoveLimited` respects rise/fall rates and stops at the target; `HighLoopMix` 0 / 0.5 / 1 at r 0.2 / 0.45 / 0.7 and flat near the edges; loop pitches match Luc's numbers. |
| `DriftSoundTests` | Stage-up chime 12 % higher per stage; sparks silent at stage 0, louder per stage, capped at 1; sparks pitch rises per stage. |
| `EnginePitchTests` (P4 part) | Equal-power crossfade keeps low^2 + high^2 = 1 and starts/ends with one loop; last gear ends at top speed and lower gears are longer; gear 1 starts at 0 revs, every gear tops out at 0.97, revs drop to 0.6 when the next gear starts. |
| `VirtualGearboxTests` | Starts in gear 1; upshift just before the gear top, true only on that frame; never past the last gear; no flutter around a change point; downshift when clearly slower (no pop); Reset to gear 1. |
| `ThrottleGuessTests` | Speeding up = load 1, braking = 0, cruising = 0.55; one hard frame does not jump; zero frame time changes nothing; Reset forgets a teleport. |
| `MixLevelTests` | dB to volume (0 dB = 1, -6 = 0.5, -80 = exactly 0); a fade takes the given time and stays; 0 s jumps; a new target starts from where it is. |
| `DuckEnvelopeTests` | No duck at the start; down at the attack rate and stops at the depth; held, then back at the release rate; the deeper duck wins; Clear is immediate. |
| `MedalRulesTests` | Default times valid; each tier from 13 s (none) to 9.1 s (Dean's List); exactly on a threshold earns it, 1 ms slower does not; a time that reads the same on screen earns it; no lap = no medal; out of order, equal, missing or zero times rejected (and give no medal); next target one tier up, none at the top; names and colour classes. |
| `RunStatsTests` | A 3 s drift counts once, two drifts twice; boosts on the rising edge; top speed keeps the max (reverse counts); air time sums only airborne frames, longest jump keeps the max; recoveries counted and end the jump; negative frame time ignored; Reset clears everything. |
| `OnboardingCoachTests` | "How to drift" after 5 s, once; not at all if the player drifted first; "hold longer" on a stage 0 release, once, not after a stage 1 release; praise on the first stage 2 boost (also one frame late), once, and no "hold longer" after it; nothing while not racing and that time does not count; a drift ended by the finish gives no tip; saved flags stop all tips; every tip has text and a kind. |
| `PersonalBestTests` (P5 additions) | Best medal only goes up; survives the save string; a save from before medals loads with no medal; a broken medal number keeps the time but no medal. `HudFormatTests` adds `OneDecimal` (0.84 -> "0.8", 1.25 -> "1.3", negative -> "0.0"). |
| `GhostRecordingTests` | Empty recording has no pose; uneven frames still give samples exactly every 0.05 s; halfway between two samples = halfway position and rotation; clamped before the start and after the end; one sample works at any time; a teleport jumps instead of gliding (also inside one frame); repeated or old times add nothing; stops at 120 s; `Clear` starts over; end velocity; save text round trip (under 1 cm, under 0.2 degrees); yaw across 360 stays a small change; same text on a Portuguese laptop; a 60 s lap stays under 40 KB; other track or checkpoint count = ignored; broken texts = no ghost. |
| `GhostLoaderTests` | Own ghost wins over the shipped one; fresh browser races the shipped ghost; broken or outdated own ghost falls back to the shipped one; nothing valid (or a shipped ghost from the old layout) = no ghost; save key next to the personal best. |
| `GhostFadeTests` | Hidden close, full far away, half in the middle, never weaker further away; bad settings do not break it; end fade 1 to 0; the strongest fade wins (camera, kart, end). |

Run: Unity Editor, Window > General > Test Runner > EditMode > Run All. Command line (Unity closed), Windows:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults/editmode.xml
```

How I would explain it: "Edit Mode tests run without pressing Play, in under a second. That only works because the rules are plain C# classes that do not need a scene."

---

## 5. Swap day checklist (when Noah's T4/T5 and Alessandro's D1 land)

Do it on a branch `role4/<task-id>-swap-contract`, after `git pull` of `main` with their PRs merged.

**A. Track (Noah: `TrackInfo`, `Checkpoint`, `RaceRules` in `CarcavelosCircuit.Track`)**
1. Read their code first and confirm the members match `docs/interfaces.md`: `TrackInfo.Checkpoints` (`Checkpoint[]`), `SpawnPoint`, `GetRespawnPoint(int)`; `Checkpoint.Index`; `RaceRules(int)`, `StartLap()`, `PassCheckpoint(int)`, `TryFinish()`, `LastValidCheckpoint`, `Reset()`.
2. Confirm the checkpoint 0 meaning is the same as in `PlaceholderLapRules` (now written into `docs/interfaces.md`; a wrong numbering shows a red `RaceManager` error at start): index 0 = start/finish line, it is never passed with `PassCheckpoint`; `PassCheckpoint` accepts only 1, 2, 3 ... in order; `TryFinish` is true once all of them were passed, and only once; `LastValidCheckpoint` is 0 before the first checkpoint. `RaceManager.OnCheckpointEntered` relies on exactly this (index 0 -> `TryFinish`, others -> `PassCheckpoint`). If Noah's rules differ, adapt `OnCheckpointEntered`, not his class.
3. `RaceManager.cs`: add `using CarcavelosCircuit.Track;`; field `private PlaceholderLapRules rules;` -> `private RaceRules rules;`; `private PlaceholderTrack track;` -> `private TrackInfo track;`; `new PlaceholderLapRules(checkpointCount)` -> `new RaceRules(checkpointCount)`; `SetUp(GameObject kartObject, PlaceholderTrack loadedTrack, ...)` -> `TrackInfo loadedTrack`. Delete the two "swap to ..." comments.
4. `RaceBootstrap.cs`: add `using CarcavelosCircuit.Track;`; `PlaceholderTrack track` (Start) -> `TrackInfo track`; `FindTrackIn` return type and `GetComponentInChildren<PlaceholderTrack>()` -> `TrackInfo`.
5. Fallback: once `Track_Campus` has a `TrackInfo`, set `fallbackTrackSceneName` to empty in `RaceSettings.asset` (Inspector), so the placeholder scene is no longer needed.
6. Test cameras in `Track_Campus` are fine: `TurnOffTestCameras` switches off every Camera GameObject and AudioListener in the track scene. Only ask Noah to keep the test camera on its own GameObject (that whole object is switched off) and to make checkpoint colliders `isTrigger`.

**B. Kart (Alessandro: `Kart` prefab, root has `KartController : IKartState`)**
7. Check the kart's Rigidbody and colliders are on the `Kart` prefab root or its children when it is spawned (`RaceManager.IsKartCollider` remembers them at spawn, so the sphere may be unparented later in `Start`). A sphere that is a separate object never under the prefab would not count.
8. Check `InputEnabled = false` stops all input (and does not release a drift into a boost) and `ResetTo` zeroes velocity and clears drift/boost (request Q6 in `TASKS.md`).
9. No code change for the kart: the prefab must be named exactly `Kart` and live under `Assets/_Project/Driving/`, with `KartController` on the root. Then run **Carcavelos Circuit > Build Role 4 Scenes**; `FindRealKartPrefab()` picks it up and the Console says "Role4SceneBuilder: using Role 1's kart ...". Never set the prefab by hand in Main's Inspector: the builder rewrites its own objects in `Main.unity` on every run.
10. Camera: keep `PlaceholderChaseCamera` until Role 1's Cinemachine camera (D2) exists. Role 1's camera rig goes into Main as its own root object (the builder keeps it). Then remove the placeholder camera from `BuildMainScene` and from `BuilderComponents` ("Main Camera" entry), and in `RaceBootstrap` the `chaseCamera` field and its `if` block; the race already works with the field empty.

**C. Delete and rebuild**
11. In the Unity Editor (never in Explorer, keeps `.meta` files right): delete `PlaceholderTrack`, `PlaceholderCheckpoint`, `PlaceholderLapRules`, `PlaceholderKart` (and `PlaceholderChaseCamera` once D2 is in), `Tests/EditMode/PlaceholderLapRulesTests.cs`, `RaceFlow/Prefabs/PlaceholderKart.prefab`, `RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity`.
12. `Role4SceneBuilder.cs`: remove `CreatePlaceholderKartPrefab()` and the `if (kart == null)` fallback in `BuildAll()`, `BuildPlaceholderTrackScene()`, the `Block()` helper, and `TrackScenePath` from the build list (they reference deleted types). `ProjectSetup` needs no change: it adds the placeholder track only if the file exists.
13. Wait for compile, Console 0 errors. Run **Carcavelos Circuit > Build Role 4 Scenes**.
14. Run EditMode tests (command above). Play `Main.unity`: start, countdown, all checkpoints, finish, record saved, R, Enter, Retry button, falling off, drift glow (Bloom).
15. Run **Carcavelos Circuit > Web Build**, then `python tools/serve_web_build.py`, test sound starts after the first key.
16. Log the swap in `docs/decisions.md`, commit, push, PR.

---

## 6. Likely examiner questions

1. **Why is the race logic not all in one MonoBehaviour?** Plain C# classes (`RaceStateMachine`, `RaceTimer`, `PersonalBest`) can be unit tested without a scene. The MonoBehaviour `RaceManager` only connects them to Unity (frames, keys, kart).
2. **What is the difference between `Awake`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`?** Awake: once, when the object is created (cache components). Start: once, before the first frame (our bootstrap). Update: every frame (input, race logic, HUD). FixedUpdate: fixed physics step (kart forces). LateUpdate: after all Updates (camera).
3. **Why multiply by `Time.deltaTime`?** Frames have different lengths on different laptops. Adding the real frame time makes the timer and smoothing correct at any FPS. We never change `Time.timeScale`, so scaled and unscaled time are equal; if we added a pause with `timeScale = 0`, the lap timer would stop on its own (`Time.unscaledDeltaTime` would keep running).
4. **How does the HUD know what to show?** It reads public read-only properties of the `RaceManager` every frame (polling). Sounds use events instead, because they should play once, not every frame.
5. **What is an event and why `-=` in `OnDisable`/`OnDestroy`?** A list of methods to call when something happens; the sender does not know the listeners. Unsubscribing prevents calls to destroyed objects.
6. **How does a checkpoint count?** Trigger collider -> `CheckpointRelay.OnTriggerEnter` -> `RaceManager.OnCheckpointEntered(index)` -> lap rules decide (only the next expected one counts). Finish = index 0 after all others.
7. **What is additive scene loading and why use it?** The track scene is added on top of Main instead of replacing it. Each owner edits only their own scene, so no Git merge conflicts.
8. **Where is the best time saved, also online?** `PlayerPrefs` under `CarcavelosCircuit.Best.<scene name>`. In a Web build Unity stores PlayerPrefs in the browser (IndexedDB), so it survives a reload on the same browser.
9. **Why InvariantCulture in the save string?** So 41.25 is always written with a dot. On a Portuguese system it would be "41,25" and loading could fail.
10. **Why no sound before the first key press?** Browsers block audio until the user interacts with the page. The start screen makes that interaction happen before the countdown.
11. **What happens on Retry vs Recover?** Retry: full reset (kart to grid, rules, timer, quick countdown). Recover: kart to the last valid checkpoint's respawn point, timer keeps running, no checkpoint awarded.
12. **What are asmdefs?** Assembly definition files split the code into separate assemblies with allowed references (`Audio -> Shared` only, `RaceFlow -> Shared, Track, Audio`). The compiler stops forbidden dependencies and cycles.
13. **What is a ScriptableObject vs a MonoBehaviour?** ScriptableObject = data asset in the project (`RaceSettings`), not attached to a scene object. MonoBehaviour = component on a GameObject with a lifecycle.
14. **Why `[SerializeField] private` instead of `public`?** Other scripts cannot change the value by accident, but Unity still saves it and shows it in the Inspector.
15. **What happens if a track scene has its own camera?** `RaceBootstrap.TurnOffTestCameras` switches it and any AudioListener off after loading, so only Main's camera renders and only one AudioListener hears.
16. **Why is the Web build uncompressed?** Not on the wire: the host (GitHub Pages) gzips it. Unity's own `.gz` files need a header Pages cannot send, so they would not start there. Hashed file names make sure a browser never mixes an old and a new build.
17. **Why is the canvas not just 1280x720?** Players have different window sizes. The canvas fills the window and the HUD scales from a 1280x720 reference (PanelSettings "scale with screen size").
18. **Why no limiter or AudioMixer effects?** Unity's Web build only supports volume changes. We gain-stage instead: every file has a loudness target, Normalize is off, peaks stay under -1 dBFS, and music ducks in script under the important sounds.
19. **How can an engine without gears "shift"?** The sound pretends: the speed range is split into 4 gears; inside a gear the pitch climbs, at the top it drops back to 60 % and climbs again, with a 0.07 s volume cut and a pop. The kart itself has no gears.
20. **How does the drift counter not count every frame?** Rising edge: `RunStats` remembers last frame's `IsDrifting` and counts only the frame it turns from false to true.
21. **How are the coach tips remembered?** Each tip is one bit (1, 2, 4) of an int saved in PlayerPrefs. `(DoneFlags & 2) != 0` means tip 2 was already shown.
22. **How does the ghost stay in sync with you?** Recording and replay use the same clock, the race timer `LapSeconds` (0 at GO). At 12.3 s on your timer the ghost shows where the recorded kart was at 12.3 s, blended between the two nearest of its 20-per-second samples. It has no Rigidbody or collider, so it cannot push you or count a checkpoint.

---

## 7. Still open (be honest if asked)

Fixed on 2026-11-08 and no longer weak spots: drift boost after the finish, drift cancelling itself on the placeholder kart, hard-coded hint time, campus scene staying loaded on fallback, the two build lists disagreeing, Retry button focus, manual kart swap. Robustness pass (audit, same day): race no longer depends on the placeholder camera, kart found by its Rigidbody/colliders, checkpoint check with red errors, builder keeps other people's work, input forced off outside Racing every frame, lap time from timestamps, HUD without per-frame strings, one recover per fall, no landing thud after a teleport, stage chimes no longer bend each other, Alt/F11 no longer start the race.

- **Sphere kart and checkpoints**: handled for a sphere that is under the prefab at spawn (`IsKartCollider`), but not yet tested with Alessandro's real kart. Check the day D1 lands (checklist step 7).
- **Checkpoint 0 meaning written down, not yet confirmed by Noah**: `docs/interfaces.md` now says index 0 = start/finish, indices exactly 0..N-1. A wrong numbering gives a red error at start instead of a silent unfinishable lap. Confirm before T5 is merged.
- **Personal bests reset on every itch.io re-upload** (decided and accepted in `docs/decisions.md`): Web PlayerPrefs live under a folder named after the page URL, and itch.io changes it per upload.
- **Cinemachine cameras in track scenes**: `TurnOffTestCameras` only finds `Camera` components and `AudioListener`s. A `CinemachineCamera` has neither, so once Role 1's camera with a `CinemachineBrain` is in Main, a Cinemachine test camera left in a track scene could take over the view. Ask owners not to leave Cinemachine cameras in their scenes, or extend the method then.
- **Camera object is switched off as a whole** (`RaceBootstrap.cs:97`, `SetActive(false)`): anything else on that GameObject or its children goes off too. Fine as long as test cameras sit on their own object.
- **Kart prefab name is strict** (`Role4SceneBuilder.FindRealKartPrefab`): exactly `Kart`, under `Driving/`, `IKartState` on the root. A prefab called "Kart Variant" or with the controller on a child is ignored and the placeholder stays, silently apart from the missing Console line.
- **Bloom not yet checked in a Web build with real effects**: it depends on Role 3's sparks and flames being bright enough (above threshold 0.9). Check it looks right and the frame rate stays fine on a normal laptop.
- **P4 audio not yet heard in a real build** (written without opening Unity): gear-change feel, ducking amounts, music start on GO and gapless loops (smpl + 2048 pad) must be checked in the Web build with headphones (`audio-polish-plan.md` section 7).
- **Medal times are placeholders** (12.0 / 11.0 / 10.2 / 9.6 s on the placeholder track). Re-tune them in `RaceSettings.asset` once the campus track lands; the personal-best key is per track scene, so the new track starts fresh anyway.
- **Coach tips are not yet playtested** with a first-time player (5 s trigger, wording). Medal colours, like coral, still need Luc's OK in `docs/art-style.md`.
- **Ghost (stretch item) not yet seen in Unity**: the code compiles and has tests, but the look (alpha 0.4, fade 1.5 to 8 m, colours), the transparent material in the Web build and the copy of Role 3's real kart meshes still need a Play Mode and a Web build check. The Dean's ghost must be recorded on the final track (after T6/D6) and again after every track change, otherwise the game ignores it (no error, simply no ghost). If the real kart's `Visual` child has effect meshes that are switched on at spawn, they are copied into the ghost too.
- **By design, worth remembering**: the builder rewrites its own root objects in `Main.unity` on every run (values set by hand on them are lost; added components or children make it stop and ask); other root objects, the Volume profile and `RaceSettings` values are kept.
