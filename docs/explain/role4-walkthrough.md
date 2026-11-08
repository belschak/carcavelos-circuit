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
  |-- chaseCamera.SetTarget(kart)
  '-- raceManager.SetUp(kart, track, sceneName)
          |
          v
RaceManager.Update()  every frame
  Ready --any key/click--> Countdown --3,2,1,GO--> Racing --finish line, all checkpoints--> Results
                              ^                                                              |
                              '--------------------- Retry (Enter or button) ----------------'
  |  reads/writes kart only via IKartState (InputEnabled, ResetTo, Speed ...)
  |  checkpoint triggers -> CheckpointRelay -> OnCheckpointEntered -> lap rules
  |
  +--> RaceHud (UI Toolkit)  polls State, LapSeconds, BestSeconds, Kart ... every frame
  +--> RaceSounds            listens to events CountdownStep, CheckpointPassed, LapFinished
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
| Race sounds in `RaceFlow/RaceSounds`, kart sounds in `Audio/KartAudio` | The `Audio` assembly may only reference `Shared`, so it cannot see `RaceManager`. Kart sounds only need `IKartState`, race sounds need the `RaceManager` events. |
| HUD = UI Toolkit (`.uxml` + `.uss`) | Text files that work like HTML and CSS, no TextMesh Pro import, easy to read in a PR. PanelSettings scale from a 1280x720 reference. |
| Plain C# classes for logic (`RaceTimer`, `RaceStateMachine`, `PersonalBest`, `PlaceholderLapRules`, `EnginePitch`), MonoBehaviours only as thin glue | Plain classes can be unit tested in Edit Mode with fake frame times, no scene needed. |
| Scenes and prefabs built by `Role4SceneBuilder` (editor script) | Nobody hand-edits scene YAML; same result on every laptop; re-runnable. The kart swap is automatic: it picks Role 1's `Kart` prefab as soon as it exists. |
| Track scenes may keep their own test cameras; `RaceBootstrap` switches them off | Each owner can test their scene alone, and the race still has exactly one camera and one AudioListener (Main's). |
| Bloom via one global Volume in Main | Luc's request: drift sparks and boost flames should glow. The profile asset is created once and then left alone, so Role 3 can tune it. |
| `RaceSettings` ScriptableObject | All tuning numbers in one asset, no magic numbers in code (team rule). |
| Countdown 1 s per step on the first start, 0.5 s on retry | Time-trial players retry a lot, a quick restart feels better. |
| "Press any key to start" screen | Browsers block sound until the first click or key press; our start screen forces that press before the race. |
| Enter = retry, not Escape | In a browser Escape leaves fullscreen. |
| Own synthesised CC0 sounds + 3 ElevenLabs loops, WAV only | Licence is clean (`THIRD_PARTY.md`); WAV avoids the small gaps compressed loops can have on Web. |
| One asmdef per module | Compiler enforces who may talk to whom (no hidden dependencies), and recompiles are faster. |

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

- `ElapsedSeconds`, `IsRunning`; `Start()` (from 0, running), `Stop()` (freeze), `Reset()` (0, stopped).
- `Tick(deltaTime)`: adds time only while running and only if `deltaTime > 0`.
- `static Format(seconds)` -> `"1:23.456"`: converts to whole milliseconds first with `Math.Round`, then splits into minutes, seconds, ms. "Rounding once at the start means 59.9996 s becomes 1:00.000, never 0:60.000."
- `static FormatDelta(delta)` -> `"-1.204"` / `"+0.500"`: always with a sign.
- How I would explain it: "The timer does not read the clock itself. The RaceManager feeds it `Time.deltaTime` every frame, so in tests I can feed fake frame times."

### 3.5 `RaceFlow/RaceSettings.cs` (ScriptableObject)

`[CreateAssetMenu]` so it can be created from the Assets menu; the asset is `RaceFlow/Settings/RaceSettings.asset`. Fields: `trackSceneName = "Track_Campus"`, `fallbackTrackSceneName = "RaceFlow_PlaceholderTrack"`, `countdownFrom = 3`, `countdownStepSeconds = 1`, `retryCountdownStepSeconds = 0.5`, `splitPopupSeconds = 2`, `hintSeconds = 2.5` (how long "Missed a checkpoint!" stays), `fallRecoverHeight = -20`. Read-only properties expose them.

- How I would explain it: "A ScriptableObject is a data asset that lives in the project, not in a scene. Designers change numbers in the Inspector without touching code, and several scripts share the same asset."
- Concept: `[SerializeField] private` = private in code (nobody else can change it), but Unity saves it and shows it in the Inspector. `[Tooltip]` = hover help.

### 3.6 `RaceFlow/RaceKeys.cs` (static helper, new Input System)

- `AnyKeyOrClickPressed()`: `Keyboard.current.anyKey.wasPressedThisFrame` or left mouse button.
- `RetryPressed()`: Enter or numpad Enter.
- `RecoverPressed()`: R.
- Every method checks `Keyboard.current != null` (no keyboard = null).
- How I would explain it: "All race keys in one place. `wasPressedThisFrame` is true only in the one frame the key goes down, so holding Enter does not retry 60 times a second."
- Concept: Input System (new). The kart uses `isPressed` (held), race flow uses `wasPressedThisFrame` (tap).

### 3.7 `RaceFlow/CheckpointRelay.cs` (MonoBehaviour)

Added by `RaceManager.SetUp` at runtime to every checkpoint object. `SetUp(manager, index)` stores both. `OnTriggerEnter(Collider other)`: if the thing that entered has an `IKartState` on itself or a parent, call `raceManager.OnCheckpointEntered(index)`.

- How I would explain it: "The track should not know about the race. So I glue a tiny messenger onto each checkpoint. It only says 'the kart touched checkpoint N'; the rules decide if that counts."
- Concept: triggers. A collider with `isTrigger` does not block, it only reports. `OnTriggerEnter` needs a Rigidbody on at least one of the two objects (the kart has one). `GetComponentInParent` works with interfaces.

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

Events: `CountdownStep`, `CheckpointPassed`, `LapFinished(float time, bool record)`, `Restarted` (the camera listens to snap behind the kart).

- `SetUp(kartObject, track, trackName)` (~line 47): gets `IKartState` from the kart (error if missing); creates the lap rules with the number of checkpoints; loads the best time from `PlayerPrefs` with key `"CarcavelosCircuit.Best." + trackName`; creates the state machine and subscribes `OnStateChanged` and `OnCountdownStep` with `+=`; adds a `CheckpointRelay` to every checkpoint; puts the kart on the grid; `InputEnabled = false`. "Called once by the bootstrap, not in Start, because the kart does not exist yet when the scene starts."
- `OnDestroy()`: unsubscribes with `-=`. "Always remove listeners you added, otherwise a destroyed object can still be called."
- `Update()` (~line 89), the heart:
  1. Not set up yet: do nothing.
  2. Ready: wait for any key or click, then `BeginCountdown(CountdownStepSeconds)`.
  3. `timer.Tick` then `stateMachine.Tick` with `Time.deltaTime`. "Timer first: on the GO frame the timer is not running yet, so the GO frame does not count as race time."
  4. Racing or Results + Enter: `Retry()`.
  5. Racing + R or kart below `FallRecoverHeight`: `Recover()`.
- `Retry()` (public, also called by the HUD button): ignored in Ready; kart back to the grid (`ResetTo` also clears drift/boost), `rules.Reset()`, `timer.Reset()`, clears record/split/hint flags, quick countdown, fires `Restarted`.
- `Recover()`: `track.GetRespawnPoint(rules.LastValidCheckpoint)` then `kart.ResetTo(...)`. Timer keeps running, no checkpoint awarded. "Recovering costs you time, that is the penalty."
- `OnCheckpointEntered(index)`: only while Racing. Index 0 = start/finish: `rules.TryFinish()` -> `FinishLap()`; if it fails after at least one checkpoint, hint "Missed a checkpoint! Press R to go back". Other index: `rules.PassCheckpoint(index)`; if valid, record the split, show it for `SplitPopupSeconds`, fire `CheckpointPassed`.
- `FinishLap()` (private): `stateMachine.Finish()` (guards double finish), stop the timer, remember `PreviousBestSeconds`, `personalBest.FinishRun`; if record: `PlayerPrefs.SetString` + `PlayerPrefs.Save()`; fire `LapFinished`.
- `OnStateChanged(newState)`: `kart.InputEnabled = stateMachine.InputAllowed`; when entering Racing: `rules.StartLap()`, `personalBest.StartRun()`, `timer.Start()`.
- `OnCountdownStep(number)`: forwards the state machine's event to RaceManager's own `CountdownStep` (so HUD and sounds only need to know the RaceManager).
- `PutKartOnStartGrid()`, `ShowHint(text)` (visible for `settings.HintSeconds`).
- Concepts: MonoBehaviour lifecycle (`Update` every frame, `OnDestroy`), events/delegates (`Action<T>`, `+=`/`-=`, null check before invoking), PlayerPrefs (on Web stored in the browser, IndexedDB), `Time.time` for "visible until" timestamps.

### 3.10 `RaceFlow/RaceBootstrap.cs` (MonoBehaviour)

Entry point of `Main.unity` (task R1). Serialized: `settings`, `kartPrefab`, `kartAttachments[]` (the KartAudio prefab), `raceManager`, `chaseCamera`.

- `IEnumerator Start()` (~line 28): a coroutine. Loads the campus scene, looks for a track; if none, it first unloads the empty campus scene (`SceneManager.UnloadSceneAsync`, so its test camera or lights do not end up in the race) and loads the fallback placeholder scene; error if still none. Then `SetActiveScene(track scene)` (its lighting and sky are used), `TurnOffTestCameras(track scene)`, `Instantiate(kartPrefab, spawn.position, spawn.rotation)`, instantiates each attachment as a child of the kart, `chaseCamera.SetTarget`, `raceManager.SetUp`, and subscribes `chaseCamera.SnapBehindTarget` to `Restarted`.
- `LoadSceneIfNeeded(name)`: skip if already loaded (both scenes open in the Editor); warn if not in the build list (`Application.CanStreamedLevelBeLoaded`); else `yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)`.
- `TurnOffTestCameras(name)` (~line 90): in the loaded track scene, every GameObject with a `Camera` is switched off (`SetActive(false)`) and every `AudioListener` disabled. "Owners may keep a test camera in their own scene. In the race only Main's camera may render and listen: two AudioListeners give a Unity warning, two cameras the wrong picture."
- `FindTrackIn(name)`: loops over the scene's root objects with `GetComponentInChildren<PlaceholderTrack>()`. "No `GameObject.Find`, which is slow and breaks when someone renames an object."
- How I would explain it: "Additive loading means the track scene is added on top of Main instead of replacing it. Each owner has their own scene, so we never get Git merge conflicts in scene files. A coroutine lets Start wait for the loading without freezing the game."
- Detail you may be asked: because the track scene is made active before `Instantiate`, the spawned kart belongs to the track scene, not to Main.

### 3.11 `RaceFlow/RaceSounds.cs` (MonoBehaviour)

Race interface sounds on one 2D `AudioSource`. `OnEnable` subscribes to `CountdownStep`, `CheckpointPassed`, `LapFinished`; `OnDisable` unsubscribes.

- `OnCountdownStep(n)`: low beep for 3, 2, 1, high GO beep for 0.
- `OnCheckpointPassed`: chime. `OnLapFinished`: jingle, plus a sting if new record.
- `Play(clip)`: `source.PlayOneShot(clip, volume)`, skips missing clips. "PlayOneShot lets sounds overlap on the same source."
- Concept: `OnEnable`/`OnDisable` are the right pair for subscribing, they also run when the component is switched on and off.

### 3.12 `RaceFlow/RaceHud.cs` + `UI/RaceHud.uxml` + `UI/RaceHud.uss`

`RaceHud.uxml`: the layout, like HTML. Named elements: `timer`, `best`, `split`, `hint`, `countdown`, `boost-label`, `boost-fill`, `speed`, `start-screen` (with `press-any-key`, `start-best`), `results-screen` (with `record`, `final-time`, `final-delta`, `retry-button`). It links `RaceHud.uss` with `<Style src=...>`.

`RaceHud.uss`: the look, like CSS. Colours from the art style (white #EAEAEC, black #1D1D1F, blue #2D8CD6, orange #FF8A1F). Classes toggled from code: `countdown-go` (orange GO), `split-ahead` (blue) / `split-behind` (red), `boost-stage-1` (blue) / `boost-stage-2` (orange), `boost-active`. `position: absolute` overlays; Unity-only properties start with `-unity-` (`-unity-text-align`, `-unity-font-style`, `-unity-text-outline-*`).

`RaceHud.cs`: `[RequireComponent(typeof(UIDocument))]` (Unity adds the UIDocument automatically and stops you removing it).

- `OnEnable()`: `root.Q<Label>("timer")` etc. finds every element by name; sets `retryButton.focusable = false` and subscribes the button (`retryButton.clicked += OnRetryClicked`) and `CountdownStep`. `OnDisable()` unsubscribes. "Not focusable: otherwise, after a mouse click, the button keeps keyboard focus and Space (drift) or Enter in the next race would press it again."
- `OnRetryClicked()`: `raceManager.Retry()`.
- `OnCountdownStep(0)`: remembers `goVisibleUntil = Time.time + 0.8 s`.
- `Update()`: shows the start screen only in Ready, results only in Results, then updates each part. "The HUD only reads the RaceManager, it never changes the race (except the Retry button, which calls the same public `Retry()` as the Enter key)."
- `UpdateStartScreen()`: blinking "Press any key" with `Mathf.Sin(Time.time)`; "Loading track..." until `IsSetUp`; "Your best: ...".
- `UpdateCountdown()`: number during Countdown, "GO!" for 0.8 s after.
- `UpdateTimes()`: `RaceTimer.Format` for lap and best (`--:--.---` if none).
- `UpdateSplitAndHint()`: split only if racing, there is a delta, and its time has not run out.
- `UpdateKartInfo()`: km/h = `|Speed| * 3.6`; boost bar width = `Length.Percent(DriftCharge01 * 100)`; label "DRIFT", "DRIFT  1", "DRIFT  MAX", "BOOST!".
- `UpdateResults()`: final time, "NEW RECORD!", delta to the previous best or "First lap on this track".
- `SetVisible()`: `style.display = Flex / None`.
- Concept: polling (HUD reads every frame) vs events (sounds react once). For labels that change every frame, polling is simpler.

### 3.13 `RaceFlow/Placeholders/` (stand-ins, deleted on swap day)

- `PlaceholderCheckpoint`: `Index` (0 = start/finish) and `RespawnPoint` (serialized). Same idea as Noah's `Checkpoint`.
- `PlaceholderTrack`: `Checkpoints[]`, `SpawnPoint`, `GetRespawnPoint(lastIndex)` (loops for the matching index, falls back to the spawn point). Same members as `TrackInfo`.
- `PlaceholderLapRules` (plain C#): exactly the `RaceRules` API. `nextExpected` starts at 1; `PassCheckpoint(i)` true only if `i == nextExpected` and `0 < i < count`; `TryFinish()` true only when every checkpoint was passed, then sets `nextExpected = count + 1` so a second call fails; `LastValidCheckpoint` (0 = start); `StartLap()` and `Reset()`.
- `PlaceholderKart : MonoBehaviour, IKartState` with `[RequireComponent(typeof(Rigidbody))]`. A sliding box: input read in `Update` (W/S/A/D, arrows, Space) only if `InputEnabled`; physics in `FixedUpdate` with `Time.fixedDeltaTime`: ground raycast for `IsGrounded`, accelerate/brake/roll out, remove sideways speed (less while drifting), cap at top speed (x1.35 while boosting), steer with `body.MoveRotation`. Drift: start with Space + steer + > 40 % speed (speed over the ground, not forward speed: a drifting kart points into the corner, so forward speed drops and the drift would cancel itself); while drifting the kart turns at 0.8x, keeps more slide and keeps its ground speed (the slide turns into forward speed, little drag), so a 1.6 s drift is possible; stage 1 at 0.8 s, stage 2 at 1.6 s; release = boost 0.4 s or 1.2 s; if `InputEnabled` goes false while drifting (finish line, results), the drift ends without a boost, so the kart does not surge after the finish. `ResetTo` sets transform and body, zeroes `linearVelocity` and `angularVelocity`, clears drift/boost.
  - "Input in Update, physics in FixedUpdate: Update runs once per drawn frame, FixedUpdate at a fixed 50 times per second, so physics behaves the same on fast and slow laptops."
  - "Unity 6 renamed `velocity` to `linearVelocity`."
- `PlaceholderChaseCamera` with `[RequireComponent(typeof(Camera))]`: `Awake` caches the Camera; `SetTarget` + `SnapBehindTarget`; `LateUpdate` follows with `1 - Mathf.Exp(-sharpness * Time.deltaTime)` (frame-rate independent), snaps if more than 20 m away (teleport), widens FOV 65 to 80 with speed. "LateUpdate runs after all Updates, so the kart has already moved when the camera follows it."

### 3.14 `Audio/EnginePitch.cs` (static, plain C#)

- `Speed01(speed, maxSpeed)`: `|speed| / maxSpeed` capped at 1, 0 if `maxSpeed <= 0` (no divide by zero).
- `FromSpeed01(s, min, max)`: straight line from min pitch (0.8) to max pitch (1.8).
- `Smooth(current, target, sharpness, dt)`: moves by `1 - exp(-sharpness * dt)` of the gap. "Two small frames land in the same place as one big frame, so the engine sounds the same at 30 and 144 FPS." (This is tested.)

### 3.15 `Audio/KartAudio.cs` (MonoBehaviour, on the KartAudio prefab under the kart)

Three AudioSources: engine loop, drift screech loop, one-shot effects.

- `Start()`: `GetComponentInParent<IKartState>()`; if none, warn and `enabled = false`. Starts the engine and the drift loop (drift at volume 0).
- `Update()`: engine pitch = smoothed `FromSpeed01` (+0.15 while boosting or airborne), volume 0.35 to 0.7 with speed; drift loop fades in/out with `Mathf.MoveTowards`; stage ping when `DriftStage` goes up; whoosh on the first frame of a boost (`IsBoosting && !wasBoosting`); landing thud after at least 0.3 s in the air.
- How I would explain it: "One engine recording played faster sounds like higher revs, like a tape. I remember last frame's values (`wasBoosting`, `lastDriftStage`, `wasGrounded`) to detect the moment something starts."
- Concept: edge detection with a 'was' flag; `Audio` asmdef only references `Shared`.

### 3.16 `Audio/MusicPlayer.cs`

`[RequireComponent(typeof(AudioSource))]`; `Start()` sets loop, volume 0.3, 2D, plays the clip if set. "Browsers mute audio until the first input; Unity resumes it then, and the start screen guarantees that input."

### 3.17 `RaceFlow/Editor/Role4SceneBuilder.cs` (editor only)

Menu **Carcavelos Circuit > Build Role 4 Scenes** (`[MenuItem("Carcavelos Circuit/Build Role 4 Scenes")]`), or batch: `-executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4SceneBuilder.BuildAll`.

`BuildAll()` does, in order:
1. Ensures folders.
2. `CreateSettings()`: loads or creates `RaceSettings.asset` (existing values kept) and calls `EditorUtility.SetDirty`, so fields added to `RaceSettings` later (like `hintSeconds`) are written into the file with their defaults.
3. `CreatePanelSettings()`: theme file + PanelSettings (scale with screen size, 1280x720, match 0.5).
4. `CreateKartAudioPrefab()`: 3 AudioSources + `KartAudio`, clips `el_engine_loop`, `el_drift_loop`, `el_boost_whoosh`, `sfx_drift_stage1/2`, `sfx_landing_thud`. Private fields are filled with `SerializedObject.FindProperty(...)`.
5. `FindRealKartPrefab()` (~line 199): `AssetDatabase.FindAssets("Kart t:Prefab")` under `Assets/_Project/Driving`; uses a prefab only if it is named exactly `Kart` and its root has a component implementing `IKartState`. "The kart swap is automatic: once Alessandro's prefab exists, re-running the builder puts it into Main."
6. Only if no real kart was found: `CreatePlaceholderKartPrefab()`: Rigidbody (150 kg, interpolate, continuous collision, X/Z rotation frozen), frictionless box collider, `PlaceholderKart`, a `Visual` child made of primitives.
7. `BuildPlaceholderTrackScene()`: a ~230 m rectangular loop around a sand "building", walls, cones, a kicker ramp, 4 checkpoint triggers (15 m wide) each with a `RespawnPoint` child 4 m after it, and a `SpawnPoint` just after the start line.
8. `BuildMainScene()`: camera + AudioListener + chase camera, with `GetUniversalAdditionalCameraData().renderPostProcessing = true`; a global `Volume` object "PostProcessing" using `CreateVolumeProfile()`; `RaceSystems` (RaceManager, RaceBootstrap, UI AudioSource, RaceSounds), `HUD` (UIDocument + RaceHud), `Music`. All references wired.
9. `CreateVolumeProfile()` (called from step 8): `RaceFlow/Settings/RaceCameraVolume.asset` with Bloom (threshold 0.9 = only very bright pixels glow, intensity 0.6, scatter 0.6). If the asset already exists it is returned unchanged, so Role 3's tuning survives a rebuild.
10. Build list: Main, Track_Campus, RaceFlow_PlaceholderTrack. `ProjectSetup.SetBuildScenes` (Shared/Editor) now writes the same three, the placeholder track only if its file exists, so both menus agree.

The editor asmdef references `Shared`, `Shared.Editor`, `RaceFlow`, `Audio` and, for the Volume and Bloom types, `Unity.RenderPipelines.Core.Runtime` and `Unity.RenderPipelines.Universal.Runtime`. All six are recorded in `docs/decisions.md`.

- How I would explain it: "Scene files are huge YAML that merge badly in Git and nobody can review. So the scene is the output of code: anyone can rebuild it identically, and the code is what we review."
- Concept: `SerializedObject` is how editor code sets `[SerializeField] private` fields, the same way the Inspector does.
- Concept: post-processing in URP = the camera must allow it, and a `Volume` (global = everywhere) applies a `VolumeProfile` asset with effects such as Bloom.

### 3.18 `RaceFlow/Editor/Role4WebBuild.cs`

Menu **Carcavelos Circuit > Web Smoke Build**: `BuildPipeline.BuildPlayer` for WebGL into `Build-Web/` (git-ignored), logs result, size and errors; exits with code 1 in batch mode if it failed. Only a smoke test, the release build is Role 3's job.

### 3.19 `tools/audio-synth/synth_sfx.py`

Pure Python (only standard library: `math`, `random`, `wave`, `array`). Builds sounds from sine partials, filtered noise and envelopes and writes 16-bit mono 44.1 kHz WAV. `SOUNDS` maps file names to functions: countdown beep (440 Hz), GO beep (880 Hz), checkpoint chime, finish jingle, new-record sting, drift pings, plus synth versions of boost/engine/drift loops (`sfx_boost_whoosh`, `loop_engine`, `loop_drift`, kept as CC0 backups; the prefab uses the ElevenLabs `el_*` files) and a 32 s music loop (120 BPM, A minor). `loop_crossfade()` fades the extra tail over the start, so loops have no click. Seeded `random.Random(seed)` = the same output every run.

```
python tools/audio-synth/synth_sfx.py Assets/_Project/Audio/Clips
```

### 3.20 `tools/serve_web_build.py`

Tiny local web server for `Build-Web/` on `http://127.0.0.1:8000`. Our build is gzip without decompression fallback, so it adds `Content-Encoding: gzip` for `.gz` files and the right MIME type (`foo.wasm.gz` as `application/wasm`), plus `Cache-Control: no-store`. Plain `python -m http.server` would not start the game.

```
python tools/serve_web_build.py
```

---

## 4. Tests

All in `Assets/_Project/Tests/EditMode/` (asmdef references Shared, Track, RaceFlow, Audio; editor only).

| File | What it proves |
|---|---|
| `RaceTimerTests` | No counting before Start; frame times add up; Stop freezes; Reset goes to 0; negative frame time ignored; `Format` (0:00.000, 1:23.456, 59.9996 -> 1:00.000, negative -> 0); `FormatDelta` always signed. |
| `RaceStateMachineTests` | Starts in Ready with input off; countdown fires 3, 2, 1, 0 then Racing with input on; not over at 2.9 s; one 10 s frame still fires every step; Finish only while racing, no double finish, input off in Results; retry countdown at 0.5 s per step; StateChanged reports Countdown, Racing, Results; bad settings (0 steps, 0 s) cannot freeze the race. |
| `PersonalBestTests` | First lap is a record; slower or equal is not, faster is; no split delta without a best lap; deltas ahead (-0.5) and behind (+1); out-of-range index ignored; save string round trip; dot not comma; broken saves = no record; other checkpoint count keeps time, drops splits. |
| `PlaceholderLapRulesTests` | In order then finish works; skipped checkpoint cannot finish; same checkpoint twice counts once; no double finish; `LastValidCheckpoint` for recovery; Reset starts over. |
| `EnginePitchTests` | `Speed01` in 0..1 (capped while boosting, reverse counts, `MaxSpeed` 0 safe); pitch maps 0 -> 0.8, 1 -> 1.8; Smooth does not overshoot and is frame-rate independent. |

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
2. Confirm the checkpoint 0 meaning is the same as in `PlaceholderLapRules`: index 0 = start/finish line, it is never passed with `PassCheckpoint`; `PassCheckpoint` accepts only 1, 2, 3 ... in order; `TryFinish` is true once all of them were passed, and only once; `LastValidCheckpoint` is 0 before the first checkpoint. `RaceManager.OnCheckpointEntered` relies on exactly this (index 0 -> `TryFinish`, others -> `PassCheckpoint`). If Noah's rules differ, adapt `OnCheckpointEntered`, not his class.
3. `RaceManager.cs`: add `using CarcavelosCircuit.Track;`; field `private PlaceholderLapRules rules;` -> `private RaceRules rules;`; `private PlaceholderTrack track;` -> `private TrackInfo track;`; `new PlaceholderLapRules(checkpointCount)` -> `new RaceRules(checkpointCount)`; `SetUp(GameObject kartObject, PlaceholderTrack loadedTrack, ...)` -> `TrackInfo loadedTrack`. Delete the two "swap to ..." comments.
4. `RaceBootstrap.cs`: add `using CarcavelosCircuit.Track;`; `PlaceholderTrack track` (Start) -> `TrackInfo track`; `FindTrackIn` return type and `GetComponentInChildren<PlaceholderTrack>()` -> `TrackInfo`.
5. Fallback: once `Track_Campus` has a `TrackInfo`, set `fallbackTrackSceneName` to empty in `RaceSettings.asset` (Inspector), so the placeholder scene is no longer needed.
6. Test cameras in `Track_Campus` are fine: `TurnOffTestCameras` switches off every Camera GameObject and AudioListener in the track scene. Only ask Noah to keep the test camera on its own GameObject (that whole object is switched off) and to make checkpoint colliders `isTrigger`.

**B. Kart (Alessandro: `Kart` prefab, root has `KartController : IKartState`)**
7. Check the collider that touches the checkpoint triggers sits on the `KartController` object or a child of it (`CheckpointRelay` uses `GetComponentInParent<IKartState>()`). A sphere controller whose sphere is a separate, unparented object would never trigger checkpoints.
8. Check `InputEnabled = false` stops all input (and does not release a drift into a boost) and `ResetTo` zeroes velocity and clears drift/boost (request Q6 in `TASKS.md`).
9. No code change for the kart: the prefab must be named exactly `Kart` and live under `Assets/_Project/Driving/`, with `KartController` on the root. Then run **Carcavelos Circuit > Build Role 4 Scenes**; `FindRealKartPrefab()` picks it up and the Console says "Role4SceneBuilder: using Role 1's kart ...". Never set the prefab by hand in Main's Inspector: the builder rewrites `Main.unity` on every run.
10. Camera: keep `PlaceholderChaseCamera` until Role 1's Cinemachine camera (D2) exists; then remove it from `BuildMainScene` and from `RaceBootstrap` (`chaseCamera` field, `SetTarget`, the `Restarted += SnapBehindTarget` line).

**C. Delete and rebuild**
11. In the Unity Editor (never in Explorer, keeps `.meta` files right): delete `PlaceholderTrack`, `PlaceholderCheckpoint`, `PlaceholderLapRules`, `PlaceholderKart` (and `PlaceholderChaseCamera` once D2 is in), `Tests/EditMode/PlaceholderLapRulesTests.cs`, `RaceFlow/Prefabs/PlaceholderKart.prefab`, `RaceFlow/Scenes/RaceFlow_PlaceholderTrack.unity`.
12. `Role4SceneBuilder.cs`: remove `CreatePlaceholderKartPrefab()` and the `if (kart == null)` fallback in `BuildAll()`, `BuildPlaceholderTrackScene()`, the `Block()` helper, and `TrackScenePath` from the build list (they reference deleted types). `ProjectSetup` needs no change: it adds the placeholder track only if the file exists.
13. Wait for compile, Console 0 errors. Run **Carcavelos Circuit > Build Role 4 Scenes**.
14. Run EditMode tests (command above). Play `Main.unity`: start, countdown, all checkpoints, finish, record saved, R, Enter, Retry button, falling off, drift glow (Bloom).
15. Run **Carcavelos Circuit > Web Smoke Build**, then `python tools/serve_web_build.py`, test sound starts after the first key.
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
12. **What are asmdefs?** Assembly definition files split the code into separate assemblies with allowed references (`Audio -> Shared` only, `RaceFlow -> Shared, Track`). The compiler stops forbidden dependencies and cycles.
13. **What is a ScriptableObject vs a MonoBehaviour?** ScriptableObject = data asset in the project (`RaceSettings`), not attached to a scene object. MonoBehaviour = component on a GameObject with a lifecycle.
14. **Why `[SerializeField] private` instead of `public`?** Other scripts cannot change the value by accident, but Unity still saves it and shows it in the Inspector.
15. **What happens if a track scene has its own camera?** `RaceBootstrap.TurnOffTestCameras` switches it and any AudioListener off after loading, so only Main's camera renders and only one AudioListener hears.

---

## 7. Still open (be honest if asked)

Fixed on 2026-11-08 and no longer weak spots: drift boost after the finish, unused `Recovered` event, hard-coded hint time, campus scene staying loaded on fallback, the two build lists disagreeing, Retry button focus, manual kart swap.

- **Sphere kart and checkpoints** (`CheckpointRelay.cs:23`): checkpoints only count if the collider that enters the trigger has the `IKartState` component on itself or a parent. If Alessandro's physics sphere is a separate object that is not a child of `KartController`, no checkpoint ever counts. Check this the day D1 lands (checklist step 7).
- **Checkpoint 0 meaning not yet confirmed with Noah**: `docs/interfaces.md` only says "the finish line is checkpoint 0 / the last crossing". My code assumes the `PlaceholderLapRules` meaning (checklist step 2). Confirm before T5 is merged.
- **Cinemachine cameras in track scenes**: `TurnOffTestCameras` only finds `Camera` components and `AudioListener`s. A `CinemachineCamera` has neither, so once Role 1's camera with a `CinemachineBrain` is in Main, a Cinemachine test camera left in a track scene could take over the view. Ask owners not to leave Cinemachine cameras in their scenes, or extend the method then.
- **Camera object is switched off as a whole** (`RaceBootstrap.cs:97`, `SetActive(false)`): anything else on that GameObject or its children goes off too. Fine as long as test cameras sit on their own object.
- **Kart prefab name is strict** (`Role4SceneBuilder.FindRealKartPrefab`): exactly `Kart`, under `Driving/`, `IKartState` on the root. A prefab called "Kart Variant" or with the controller on a child is ignored and the placeholder stays, silently apart from the missing Console line.
- **Bloom not yet checked in a Web build with real effects**: it depends on Role 3's sparks and flames being bright enough (above threshold 0.9). Check it looks right and the frame rate stays fine on a normal laptop.
- **By design, worth remembering**: the builder rewrites `Main.unity` on every run, so any hand edit in Main is lost; the Volume profile and `RaceSettings` values are kept.
