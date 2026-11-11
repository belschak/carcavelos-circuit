# Role 4 walkthrough: race flow, HUD, sound (David's cheat sheet)

For the live code walkthrough at the final presentation. Every file below is described as it really is in the repo on 2026-11-10 (polish pass: audit items 6, 9-13, 18-21 and Luc's Q10). Line numbers are approximate pointers (`File.cs:NN`), so you can jump there while presenting.

---

## 1. The 30-second pitch

"Role 4 turns a kart and a track into a game. `Main.unity` starts, loads the track scene on top of itself, spawns the kart and hands both to the `RaceManager`. The `RaceManager` runs a small state machine: Ready, Countdown, Racing, Results. It switches the kart's input on only while racing, counts the lap time, checks checkpoints through the race rules, handles Retry (Enter) and Recover (R) and saves the best time. The HUD only reads the `RaceManager` and draws it. Sounds listen to events. Everything talks to the kart only through the `IKartState` interface, so it works with any kart that follows the contract."

```
Main.unity
  |
  v
RaceBootstrap.Start()  (coroutine)
  |-- LoadSceneAsync("Track_Campus", Additive)   no track inside? -> next: Track_CampusWorld, Track_NovaCampus, RaceFlow_PlaceholderTrack
  |-- FindTrackIn(scene) -> TrackInfo            (Role 2, on the track scene)
  |-- Instantiate(kartPrefab) at SpawnPoint      + KartAudio prefab as a child
  |-- raceManager.SetUp(kart, track, sceneName)  checks the checkpoints, remembers the kart's colliders
  '-- chaseCamera.SetTarget(kart)                 ChaseCameraRig: Role 1's Cinemachine camera follows the kart (Q13)
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
  +--> RaceMusic             listens to the same events: music, seaside ambience and the mix levels per phase
  '--> KartAudio (Audio)     reads IKartState of its parent kart every frame (engine, drift, boost, bump, jump)
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
| Plain C# classes for logic (`RaceTimer`, `RaceStateMachine`, `PersonalBest`, `CheckpointValidation`, `FallCheck`, `ShownNumber`, `EnginePitch`), MonoBehaviours only as thin glue | Plain classes can be unit tested in Edit Mode with fake frame times, no scene needed. |
| Official lap time from two timestamps, added-up frame times only for the live timer | Adding thousands of float frame times drifts by milliseconds, and the finish happens inside a physics step between two frames. One subtraction of two `double` timestamps is exact. |
| HUD builds a new string only when the shown number changes | Strings built every frame are garbage; on the Web the garbage collector cleans up all at once, which shows as a hitch mid-lap. |
| Scenes and prefabs built by `Role4SceneBuilder` (editor script) | Nobody hand-edits scene YAML; same result on every laptop; re-runnable. The kart swap is automatic: it picks Role 1's `Kart` prefab as soon as it exists. |
| Track scenes may keep their own test cameras; `RaceBootstrap` switches them off | Each owner can test their scene alone, and the race still has exactly one camera and one AudioListener (Main's). |
| Bloom via one global Volume in Main | Luc's request: drift sparks and boost flames should glow. The profile asset is created once and then left alone, so Role 3 can tune it. |
| `RaceSettings` ScriptableObject | All tuning numbers in one asset, no magic numbers in code (team rule). |
| Medals, lap report and coach tips (task P5) | A fresh browser has no personal best, no splits, nothing to beat. Medal times give the first lap a goal, the lap report shows the drift system was used, and the coach teaches drifting without anyone explaining it at the demo. Each is one small plain C# class with tests. |
| Settings, credits, pause on focus loss (task P7) | Demo-room safety (mute the music while presenting with M, a tab switch never ruins a lap) and accessibility (reduced motion). Rules in small plain C# classes with tests; the UI is plain C# created by `RaceHud`, so the scene needed no change. |
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
- Menu keys (task P7): `SettingsPressed()` O, `CreditsPressed()` C, `MutePressed()` M, `MenuVertical()` / `MenuHorizontal()` (-1, 0, +1 from W/S/arrows and A/D/arrows), `MenuTogglePressed()` Space, `TryGetMousePosition`. O, C and M are not start keys: opening the settings must not also start the race.
- `PausePressed()` P (audit 9): pause or continue during the countdown and the race. P is not a start key either; `RaceManager` reads it on its own. Never Escape (it leaves browser fullscreen).
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

Lives on `RaceSystems` in `Main.unity`. Owns a `RaceTimer`, a `RaceStateMachine`, Noah's `RaceRules` (Role 2), `PersonalBest`, and references to the track (`TrackInfo`, Role 2) and the kart (`IKartState`).

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

- `IEnumerator Start()` (~line 28): a coroutine. Loads the campus scene, looks for Noah's `TrackInfo`; if none (an empty scene still being built), it logs a plain line, unloads the campus scene (`SceneManager.UnloadSceneAsync`, so its test camera or lights do not end up in the race) and loads the fallback placeholder scene; error if still none. Then `SetActiveScene(track scene)` (its lighting and sky are used), `TurnOffTestCameras(track scene)`, `Instantiate(kartPrefab, spawn.position, spawn.rotation)`, instantiates each attachment as a child of the kart, `raceManager.SetUp` first, and only then, if a chase camera is set, `chaseCamera.SetTarget` and `Restarted += chaseCamera.SnapBehindTarget`. "The race comes first: once the placeholder camera is removed the field is empty, and that must never stop the race from being set up." `OnDestroy` removes the listener again.
- `LoadSceneIfNeeded(name)`: skip if already loaded (both scenes open in the Editor); warn if not in the build list (`Application.CanStreamedLevelBeLoaded`); else `yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)`.
- `TurnOffTestCameras(name)` (~line 90): in the loaded track scene, every GameObject with a `Camera` is switched off (`SetActive(false)`) and every `AudioListener` disabled. "Owners may keep a test camera in their own scene. In the race only Main's camera may render and listen: two AudioListeners give a Unity warning, two cameras the wrong picture."
- `FindTrackIn(name)`: loops over the scene's root objects with `GetComponentInChildren<TrackInfo>()` (also finds the old `PlaceholderTrack`, which is now a subclass of `TrackInfo`). "No `GameObject.Find`, which is slow and breaks when someone renames an object."
- How I would explain it: "Additive loading means the track scene is added on top of Main instead of replacing it. Each owner has their own scene, so we never get Git merge conflicts in scene files. A coroutine lets Start wait for the loading without freezing the game."
- Detail you may be asked: because the track scene is made active before `Instantiate`, the spawned kart belongs to the track scene, not to Main.

### 3.11 `RaceFlow/RaceSounds.cs` (MonoBehaviour)

Race interface sounds on two 2D `AudioSource`s (one for sounds that play now, one for sounds that start a moment later). `OnEnable` subscribes to `CountdownStep`, `CheckpointPassed`, `LapFinished`, `Restarted`, `Recovered`; `OnDisable` unsubscribes. Volume is 1.0 for everything: the loudness of each sound lives in the file (`process_wavs.py`).

- `OnCountdownStep(n)`: low beep for 3, 2, 1, high GO beep for 0. On a retry the first beep waits 0.1 s (`PlayLater`), because `RaceManager.Retry()` fires beep "3" and the `Restarted` click in the same frame and the beep would hide the click. A retry is recognised by the state of the last frame (`stateLastFrame` was Racing or Results).
- `OnCheckpointPassed`: chime on its own `chimeSource`, pitched by `SplitChime` (audit 19): 2 semitones up when you are ahead of your best at this checkpoint, 2 down when behind, normal without a best lap. `OnLapFinished`: jingle; on a new record the sting starts 0.7 s later (`recordStingDelay`), so the two do not blur into one sound. `OnRestarted`: click (`ui_click`). `OnRecovered`: whoosh (`sfx_respawn`).
- `Update()`: plays `sfx_hint_warning` once when a new "Missed a checkpoint" hint appears. There is no hint event, but `HintVisibleUntil` moves forward every time a hint is shown, so "later than last frame" = new hint.
- Every stinger calls `music.Duck(dB, 0.6 x clip length)`: the music dips under it and comes back.
- `Play(clip)`: `source.PlayOneShot(clip, volume)`, skips missing clips. `PlayLater(clip, delay)`: `delayedSource.PlayDelayed(delay)`; a new one replaces a waiting one (a retry stops a record sting).
- Audit 19: `OnWrongWayShown` (from `RaceHud.WrongWayShown`, fired only on the frame the chip appears) plays the missed-checkpoint warning at 60 %, once per show. `OnPaused` / `OnResumed` (new `RaceManager.Paused` / `Resumed` events, fired by `StartPause` and `EndPause`) play the menu close / open notes at pitch 0.8 on the menu source, which ignores `AudioListener.pause`.
- Menu sounds (audit 10): `OnMenuSound(MenuSound)` answers `RaceHud.MenuSoundRequested` with one `switch`: `ui_open` / `ui_close` for a card, `ui_page` for a credits page, and the retry click (`ui_click`) for a volume step (pitch 1), an on/off row (0.85) and M (1.12). They play on a third source, `menuSource`; `RaceSounds.Awake` sets its `ignoreListenerPause = true` (that flag is not saved in scene files, so the builder cannot set it), so M still clicks while the race is paused. "HudMenus fires the event after it applied the new settings, so a step on the Effects bar clicks at the new level: that is the audible preview." Muting sets `AudioListener.volume` to 0 at once, so only "sound on" is heard.
- Concept: `OnEnable`/`OnDisable` are the right pair for subscribing, they also run when the component is switched on and off.

### 3.11b `RaceFlow/RaceMusic.cs` (MonoBehaviour, on the Music object)

The music and the overall mix, following the race. Four `LevelFade`s (race music, results music, engine, kart effects) and one `DuckEnvelope`, all in dB, turned into `AudioSource.volume` with `Decibels.ToVolume` every frame.

- Title screen: race music at -10 dB. Countdown (`CountdownStep` 3, 2, 1): music fades out in 0.3 s, engine -3 dB, effects -6 dB.
- GO (`CountdownStep(0)`): race music restarts from bar 1 (`timeSamples = 2048`, right after the loop padding) at normal pitch and fades in over 0.12 s. The music is 120 BPM, so its beats line up with the 1 s and 0.5 s countdown steps.
- Final stretch: when the last checkpoint before the finish is passed, the music glides up one semitone (pitch 1.0595 over 2 s, `Mathf.MoveTowards`) and gets 3 dB louder. The last checkpoint is `RaceManager.CheckpointCount - 1` (index 0 is the start/finish line), so the lift plays on every lap, the first one too.
- Finish (`LapFinished`): race music fades out, engine and effects -14 dB (`KartAudio.SetMixLevels`), the results loop starts 1.8 s later (`PlayDelayed`), after the jingle.
- Seaside ambience (audit 12): a fifth `LevelFade` for `amb_seaside_loop` (waves + distant gulls, own synth) on a third source on the Music object. It loops from the title screen on: -20 dB on the title and in the countdown, -24 dB while racing (engine and music fill the space), -18 dB on the results screen, always with 1.5 s fades. It follows the Effects slider and is never ducked ("the sea does not get quieter for a beep"). Point at: the `ambienceLevel.FadeTo(...)` line in each event handler. Likely question: "Why a loop and not a recording of the real beach?" Answer: "Own synth = no licence question, 8 s and 700 KB, and a loop has no start or end you could hear."
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

### 3.13 `RaceFlow/Placeholders/` (stand-ins, mostly gone since the swap on 2026-11-10)

- `PlaceholderLapRules` and its tests: deleted, RaceManager uses Noah's `RaceRules` (same API, his `RaceRulesTests` cover the same cases).
- `PlaceholderTrack : TrackInfo` and `PlaceholderCheckpoint : Checkpoint`: now EMPTY subclasses, kept only for the switch-over week. "Track scenes built before the swap have these components saved in them. Because they are subclasses with no fields of their own, Unity loads the saved `checkpoints`, `spawnPoint`, `index` and `respawnPoint` straight into Noah's fields with the same names, and `GetComponentInChildren<TrackInfo>()` finds them. So the campus world raced on day one of the swap without anyone rebuilding their scene." Deleted once both campus builders add `TrackInfo`/`Checkpoint` and their scenes are rebuilt.
- `PlaceholderKart : MonoBehaviour, IKartState` with `[RequireComponent(typeof(Rigidbody))]`. A sliding box: input read in `Update` (W/S/A/D, arrows, Space) only if `InputEnabled`; physics in `FixedUpdate` with `Time.fixedDeltaTime`: ground raycast for `IsGrounded`, accelerate/brake/roll out, remove sideways speed (less while drifting), cap at top speed (x1.35 while boosting), steer with `body.MoveRotation`. Drift: start with Space + steer + > 40 % speed (speed over the ground, not forward speed: a drifting kart points into the corner, so forward speed drops and the drift would cancel itself); while drifting the kart turns at 0.8x, keeps more slide and keeps its ground speed (the slide turns into forward speed, little drag), so a 1.6 s drift is possible; stage 1 at 0.8 s, stage 2 at 1.6 s; release = boost 0.4 s or 1.2 s; if `InputEnabled` goes false while drifting (finish line, results), the drift ends without a boost, so the kart does not surge after the finish. `ResetTo` sets transform and body, zeroes `linearVelocity` and `angularVelocity`, clears drift/boost.
  - "Input in Update, physics in FixedUpdate: Update runs once per drawn frame, FixedUpdate at a fixed 50 times per second, so physics behaves the same on fast and slow laptops."
  - "Unity 6 renamed `velocity` to `linearVelocity`."
- `ChaseCameraRig` (since Q13, replaces `PlaceholderChaseCamera`) on the Main Camera next to a `CinemachineBrain`. Role 1's prefab `Driving/ChaseCamera.prefab` (a `CinemachineCamera` with `CinemachineFollow` offset 0/3/-6 and a `CinemachineRotationComposer`) does the following and framing; the rig only does the race's part. `Awake`: copies the camera's near/far clip planes into the Cinemachine lens ("Cinemachine drives the lens; the campus world reaches past 1000 m"), remembers the base field of view, sets priority 100. `SetTarget`: `chaseCamera.Follow = kart`. `SnapBehindTarget` (on `Restarted` and `Recovered`): `PreviousStateIsValid = false`, "Cinemachine skips damping for one frame, so a teleport is a cut, not a swoop". `OnDisable`/`OnEnable` switch the brain off/on: "while MapFlyover flies, nobody else may move the camera". `TryGetRestPose`: the prefab's offset in the kart's yaw frame, the end of the fly-in glide. `LateUpdate`: field of view + up to 12 degrees with speed, `1 - Mathf.Exp(-sharpness * Time.deltaTime)` (frame-rate independent).

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
- `UpdateBoostAndLanding(dt, bumped)`: boost whoosh when `BoostCue.Update` says so (a new boost, or a chained one, see below); take-off whoosh (`sfx_takeoff_whoosh`) when `TakeOffDetector.Update` says so; landing thud after at least 0.3 s in the air.
- `UpdateWallBump(dt)` (audit 11): `WallBumpDetector.Update(Speed, IsGrounded, InputEnabled, dt)`; on a bump `sfx_wall_bump`, louder for a harder hit (`VolumeFor`: 0.5 at a 4 m/s drop, 1 at 15 m/s). It returns `bumped`, so a drift that a wall cancelled never sounds like a chained boost.
- The teleport check also resets the three detectors (a respawn stops the kart on purpose and may drop it onto the road: no thud, no take-off whoosh).
- How I would explain it: "Two engine recordings, low and high revs. Playing a recording faster raises its pitch like a tape, and between low and high revs one fades into the other. I remember last frame's values (`wasDrifting`, `wasBoosting`, `lastDriftStage`, `wasGrounded`) to detect the moment something starts."
- Concept: edge detection with a 'was' flag; `Audio` asmdef only references `Shared`.

### 3.15b `Audio/WallBumpDetector.cs` + `TakeOffDetector.cs` + `BoostCue.cs` (plain C#, audit 11)

Three guesses from `IKartState` alone, because the contract has no "hit a wall", "took off" or "boost extended" event. Each is one small class with an `Update(...)` that returns `true` on the one frame a sound should play.

- `WallBumpDetector.Update(speed, grounded, canDrive, dt)`: a bump = on the ground, input on, the speed fell by at least 4 m/s in ONE frame AND that drop divided by the frame time is above 60 m/s2. "The kart brakes at 30 m/s2 at most, so even a 0.2 s hitch on a slow laptop (6 m/s lost while braking) stays silent." Then a 0.4 s cooldown, so grinding along a wall is one thud. Reverse counts by size (`Mathf.Abs`). `Reset()` after a teleport. Point at: the two `if`s before `LastDrop = drop`.
- `TakeOffDetector.Update(grounded, canDrive, dt)`: counts air time; at 0.15 s it fires once per jump. It is only "armed" after the kart stood on the ground, so spawning above the grid or dropping onto a respawn point is no jump. Works with `IsGrounded`, so Role 1's ramp jump (D4) needs no contract change.
- `BoostCue.Update(boosting, drifting, stage, grounded, canDrive, bumped)`: whoosh when the boost starts (rising edge), or when a drift with sparks ends while a boost is still running (Role 1's `KartController` then makes the running boost longer, `if (earned > boostSecondsLeft)`, so `IsBoosting` never goes false and the edge never fires). Not in the air, not without input, not on a wall bump: those end the drift without a boost.
- Likely question: "How do you know the kart hit a wall if Role 1 never tells you?" Answer: "I do not know it, I guess it: a drop that is far faster than any brake can only be a hit. The rule is one line, and `WallBumpDetectorTests` proves a hard brake in a slow frame does not count."
- Honest limit: the thresholds are first guesses, written without a playtest on the campus track; tune `bumpMinSpeedDrop` / `bumpMinDeceleration` on the `KartAudio` prefab (through the builder) if the thud fires too often or never.

### 3.16 `Audio/MusicPlayer.cs` (deleted 2026-11-10, audit 18)

The first music script (loop one clip at volume 0.3) was replaced by `RaceFlow/RaceMusic` (3.11b) in P4 and had no reference left in any scene or prefab, so it and its builder allowance are gone. The synth clips `loop_drift`, `loop_engine`, `sfx_boost_whoosh`, `sfx_drift_stage1` are also unused on purpose: CC0 backups of Luc's ElevenLabs kart sounds (`THIRD_PARTY.md`), not referenced by any scene, so they are not in the Web build.

### 3.17 `RaceFlow/Editor/Role4SceneBuilder.cs` (editor only)

Menu **Carcavelos Circuit > Build Role 4 Scenes** (`[MenuItem("Carcavelos Circuit/Build Role 4 Scenes")]`), or batch: `-executeMethod CarcavelosCircuit.RaceFlow.Editor.Role4SceneBuilder.BuildAll`.

`BuildAll()` does, in order:
0. Safety first: `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` (stops if you press Cancel), then `MayRebuildMain()`: `FindHandEditsInMain()` opens Main and checks that the builder's own root objects (`Main Camera`, `PostProcessing`, `RaceSystems`, `HUD`, `Music`, `Ghost`, listed in `BuilderComponents`) have no children and no components the builder did not add. If they do, batch mode stops with an error and exit code 1, the menu asks "Rebuild anyway / Cancel".
1. Ensures folders.
2. `CreateSettings()`: loads or creates `RaceSettings.asset` (existing values kept) and calls `EditorUtility.SetDirty`, so fields added to `RaceSettings` later (like `hintSeconds`) are written into the file with their defaults.
3. `CreatePanelSettings()`: theme file + PanelSettings (scale with screen size, 1280x720, match 0.5).
0b. `SetUpAudioImport()` -> `AudioImportSetup.ApplyAll()` (own file, also menu **Carcavelos Circuit > Apply Audio Import Settings** or batch `-executeMethod CarcavelosCircuit.RaceFlow.Editor.AudioImportSetup.ApplyAll`): every clip in `Audio/Clips` gets Normalize off (Unity would push each clip to full peak and undo our loudness levels; no public property, so via `SerializedObject` field `m_Normalize`), Decompress On Load + Preload (also the music: only then does the Web build use the WAV loop points), Web override AAC with quality 1.0 (effects) or 0.7 (music). Only clips whose settings differ are re-imported.
4. `CreateKartAudioPrefab()`: 9 AudioSources + `KartAudio`, clips `kart_engine_low/high`, `kart_drift_squeal/sparks/start/stage_up` (Luc's), `amb_wind_loop`, `el_boost_whoosh`, `sfx_landing_thud`, `sfx_shift_pop`. Every source gets `dopplerLevel = 0`. Private fields are filled with `SerializedObject.FindProperty(...)`.
4b. `FindKartEffectsPrefab()` (Luc's request Q10): the prefab named exactly `KartEffects` under `Visuals/` (Role 3's drift smoke, sparks and boost flame, PR #46). If it exists, `RaceBootstrap.kartAttachments` gets it as a second entry next to the KartAudio prefab, so `RaceBootstrap` spawns it as a child of the kart; if not, the list stays at one entry and Main is unchanged. "Found by name like the kart model, so the day #46 is merged, one builder run adds the effects."
5. `FindRealKartPrefab()` (~line 199): `AssetDatabase.FindAssets("Kart t:Prefab")` under `Assets/_Project/Driving`; uses a prefab only if it is named exactly `Kart` and its root has a component implementing `IKartState`. "The kart swap is automatic: once Alessandro's prefab exists, re-running the builder puts it into Main."
6. Only if no real kart was found: `CreatePlaceholderKartPrefab()`: Rigidbody (150 kg, interpolate, continuous collision, X/Z rotation frozen), frictionless box collider, `PlaceholderKart`, a `Visual` child made of primitives.
7. `BuildPlaceholderTrackScene()`: built from Role 2's real parts (`TrackInfo`, `Checkpoint`, `SpawnPoint`, `RespawnPoint`), a ~230 m rectangular loop around a sand "building", walls, cones, a kicker ramp, 4 checkpoint triggers (15 m wide) each with a `RespawnPoint` child 4 m after it, and a `SpawnPoint` just after the start line.
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
- After loading, a "Click to play" screen: the click (or a key) gives the page keyboard focus, switches the sound on (`unlockSound()` resumes every `AudioContext` the game made) and fades the loader out. Audit 21: a key there ONLY closes it. A `keydown` listener on `window` in the capture phase is added before Unity starts, so it runs before any listener Unity adds later; while `closePlayScreen` is set it calls it, `preventDefault()` and `stopImmediatePropagation()`, so the game never sees that key and its own start screen (title + map fly-over) is not skipped. Tab, Shift, Ctrl, Alt, Meta and keys on an error card pass through. Checked in Chrome with a fake loader that records keys: the first key reached no game listener, the second reached all four, Tab did not close the screen, a click did.
- The controls dock under the loader lists the same keys as the start screen, P (pause) and C (credits) included (audit 20).
- Errors: no WebGL 2, a file that fails to download, or an error from Unity show a readable box instead of a blank page.

- How I would explain it: "The build is code, like the scenes. Whoever builds gets the same small build, and the page is ours, not Unity's default box."
- Concept: compression. GitHub Pages compresses files itself but cannot send the header that Unity's pre-compressed `.gz` files need, so we ship plain files and let the host compress them.

### 3.19 `tools/audio-synth/synth_sfx.py`

Pure Python (only standard library: `math`, `random`, `wave`, `array`). Builds sounds from sine partials, filtered noise and envelopes and writes 16-bit mono 44.1 kHz WAV. `SOUNDS` maps file names to functions: countdown beep (440 Hz), GO beep (880 Hz), checkpoint chime, finish jingle, new-record sting, drift pings, `ui_click`, `sfx_respawn`, the `amb_wind_loop`, plus synth versions of boost/engine/drift sounds (`sfx_boost_whoosh`, `loop_engine`, `loop_drift`, `sfx_drift_stage1/2`, kept as CC0 backups; the prefab uses Luc's `kart_*` files and the ElevenLabs boost) and a 32 s music loop (120 BPM, A minor). `loop_crossfade()` fades the extra tail over the start, so loops have no click. Seeded `random.Random(seed)` = the same output every run.

Since P4 it also makes `sfx_hint_warning` (missed checkpoint), `sfx_shift_pop` (gear change) and `music_results_loop` (16 s, 120 BPM, C major). Audit 11: `sfx_wall_bump` (low knock + body thump, 0.22 s) and `sfx_takeoff_whoosh` (rising band-passed air, 0.45 s).

Audit 12: `amb_seaside_loop` (8 s, `seaside_loop()`): low-passed noise rumble and band-passed wash hiss, both swelling with two wave envelopes per loop (the second one smaller, so it never sounds like a clock), plus three distant gull calls (`gull_call`: a falling, wobbly tone with harmonics, low-passed). Mastered to -22 dBFS RMS as a loop (pad + `smpl`).

Polish pass (2026-11-10): `ui_open` / `ui_close` (`menu_notes`: two quick blips E6 -> A6 and back, 0.15 s) and `ui_page` (band-passed noise sweeping up, a paper swish, 0.16 s) for the menus.

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
- Ghost chip (audit 13): `RaceHud.UpdateGhostChip(state)` reads `ghostPlayer.HasGhost`, `IsShippedGhost` and `GhostLapSeconds` (the builder wires the `GhostPlayer` into the HUD) and shows "GHOST: DEAN 1:02.4" (gold dot) or "GHOST: YOUR BEST 0:58.1" (blue dot) under the medal chip during the countdown and the race; no ghost = no chip. `HudFormat.TimeTenths` cuts to tenths like a stopwatch, `HudFormat.GhostName` picks the words. One `ShownNumber` holds both facts (`ms x 2 + 1 if Dean's`), so the labels are written only when the ghost changes (after a new record and a retry). In narrow windows the callouts moved down to 266 px to stay clear of the extra pill. Likely question: "Why x 2 + 1?" Answer: "So one int changes when either the time or the owner changes, and the HUD still builds no string per frame."
- How I would explain it: "The recorder writes, PlayerPrefs is the hand-over, the player reads. The ghost has no physics at all, it is only a picture we move every frame."

### 3.27 `RaceFlow/GameSettings.cs` + `SettingsItem.cs` + `SettingsStore.cs` + `SoundLevels.cs` (task P7, settings)

`GameSettings` is plain C#: `MasterVolume`, `MusicVolume`, `EffectsVolume` (0..1), `Muted`, `ReducedMotion`. `SettingsItem` is the enum of the five rows (Master, Music, Effects, Sound, ReducedMotion).

- Volumes move in 10 % steps: `Level(volume)` = 0..10, `SetVolumeLevel(item, level)` clamps to 0..10, `Change(item, direction)` is one step left or right (on/off rows: left = off, right = on), `Toggle(item)` flips an on/off row. Each returns `true` only if something changed, so the HUD saves only then.
- Mute keeps the master volume: `ListenerVolume` = 0 while muted, else `MasterVolume`. Turning the master up (also at 100 %) or clicking a diamond of the master bar unmutes ("I want to hear it"); turning it down while muted stays muted.
- `ToSaveString()` -> `"0.8|0.3|0|1|1"`, `FromSaveString(text)`: InvariantCulture, every broken or missing part keeps its default, NaN and out-of-range values are clamped.
- `SettingsStore` (static, the Unity glue): `Load()` / `Save()` with PlayerPrefs key `CarcavelosCircuit.Settings`; `Apply()` sets `AudioListener.volume` (master + mute for every sound at once) and `SoundLevels.Music` / `.Effects`.
- `SoundLevels` (static, two floats): read by `RaceMusic` and `RaceSounds` (P4) to scale music and effects. Static, because they are on other objects and need no extra Inspector reference.
- How I would explain it: "One small class holds the numbers and the rules, one static class saves and applies them. The HUD only draws them."

### 3.28 `RaceFlow/RacePause.cs` + the pause in `RaceManager` (task P7)

`RacePause` decides, `RaceManager` does it.

- `CanPauseIn(state)`: only Countdown and Racing (nothing runs on the start or results screen). `OnFocusLost(state)` and `OnPauseKey(state)` (audit 9, the P key) both call the same private `TryStart`, so both pauses follow exactly the same rules; it returns `true` only on the frame the pause starts (a second focus loss changes nothing). `TryResume(keyOrClick)` ends it only on a key or click; getting focus back alone does not. `Clear()` for Retry.
- `RaceManager.OnApplicationFocus(false)` / `OnApplicationPause(true)` (Unity calls them on every MonoBehaviour when the tab or window loses focus) -> `PauseForLostFocus()` -> `StartPause(pause.OnFocusLost(State))`. The P key in `Update()` -> `StartPause(pause.OnPauseKey(State))`. `StartPause(pauseStarted)` does only the Unity part: remembers `Time.timeScale`, sets it to 0, kart input off, `AudioListener.pause = true`.
- `Update()` while paused: input stays off, sound stays paused (P4's `RaceMusic` unpauses sound when focus comes back, the race is still paused), and a key, a click or P again calls `EndPause()`. Then `return`: the key that continues does nothing else, so Enter never also retries.
- The P key is listed in the start screen controls, in the countdown key bar (not in narrow windows, no room) and on the PAUSED card.
- Why the lap time is fair: `timeScale = 0` freezes physics, `Time.deltaTime` and `Time.timeAsDouble`; the official time is two `timeAsDouble` timestamps, so paused seconds never count. `OnDestroy` and `Retry` call `EndPause()`, so the game is never left frozen.
- `pauseOnFocusLoss` (Inspector, default on) switches it off for testing in the Editor.
- `MenuUsesInput` (set by the HUD): while true, the start screen ignores "any key", so a click on SETTINGS does not start the race.
- How I would explain it: "Focus lost or P during the race: time scale 0, everything stands still, the clock too. Any key, a click or P again continues."

### 3.29 `RaceFlow/UI/HudMenus.cs` + `SettingsPanel.cs` + `CreditsPanel.cs` (task P7, the menus)

Plain C# classes (no MonoBehaviour): `RaceHud.OnEnable` creates `HudMenus`, `RaceHud.Update` calls `menus.Update(state)`. That is why `Role4SceneBuilder` needed no change.

- `HudMenus`: loads and applies the settings; M = `ToggleMute()` with a toast for 1.4 s (`Time.unscaledTime`, works while paused); O opens/closes the settings (start and results screen), C the credits (start screen); closes the menus when the race starts (Enter on the results screen still retries); shows the PAUSED overlay while `raceManager.IsPaused`; puts `reduce-motion` on `hud-root`; switches clicks on only for visible buttons (`HudFx.SetClickable`).
- `MouseOnStartButton()`: `panel.Pick(point)` finds the element under the mouse (screen y flipped first: screen pixels start bottom left, the panel top left). On the start screen only the menu buttons catch clicks, so if the mouse is on one of them, `MenuUsesInput` is true before the click arrives.
- Clicks use `ClickEvent` (mouse button up), so the button press itself happened while the menu was still open or the mouse was on a button: it never leaks into "press any key".
- `SettingsPanel`: five rows from `RaceHud.uxml` (`set-master` ... `set-motion`); ten diamond cells per volume bar made in code; W/S choose (wraps, `MenuNavigation.Wrap`), A/D change, Space flips on/off; the mouse moves the highlight (`PointerEnterEvent`), `-`/`+`, a click on diamond 7 = 70 %, a click on an on/off row flips it. `Refresh()` only switches classes and uses cached "0 %" ... "100 %" strings.
- Credits page 2 "BUILT WITH" lists Cinemachine since Q13 (audit 20).
- `CreditsPanel`: two pages (team, tools and sources) that fade in with `is-shown`; A/D, Space or NEXT PAGE turn, the dots jump. Texts are in the UXML (names as in `docs/team.md`, credits as in `THIRD_PARTY.md`). `GoTo(page)` calls the `pageTurned` callback only if the page really changes.
- Menu sounds (audit 10): `HudMenus.SoundRequested` (an `Action<MenuSound>` event, enum in `RaceFlow/MenuSound.cs`) fires on open, close, page turn, a volume step, an on/off row and mute. `SoundFor(item)` (static, tested) picks `SliderStep` for the three volume rows and `Toggle` for the on/off rows. `SettingsPanel` reports which row changed (`Action<SettingsItem>`). Closing because the race starts (`HideMenus`) is silent, so it never covers the retry click. `RaceHud` passes the event on as `MenuSoundRequested`, because `HudMenus` is made new in every `RaceHud.OnEnable`, while `RaceSounds` needs one object to subscribe to.
- Reduced motion: one class on the root; the last block of `RaceHud.uss` sets `translate: 0 0`, `scale: 1 1` and the final rotation for every slide, shake, punch and stamp, one class more specific than the rule it beats. Fades stay. The breathing start pill is an inline style (beats all USS), so `RaceHud.UpdateStartScreen` checks `menus.ReducedMotion`.
- How I would explain it: "The menus are classes that switch CSS classes. The USS decides how it looks, the RaceManager decides what the race does."

### 3.29b `RaceFlow/SplitChime.cs` (static, plain C#, audit 19)

`Semitones(hasSplitDelta, deltaSeconds)`: no best lap = 0; otherwise the delta in rounded milliseconds (`RaceTimer.ToMilliseconds`, the same rounding as the split chip) below 0 = ahead = +2, else -2 (so "+0.000", which the chip calls BEHIND, is the low chime too). `Pitch(semitones) = 2^(semitones / 12)`: +2 = 1.1225, -2 = 0.8909, +12 = 2 (one octave). Point at: `RaceSounds.OnCheckpointPassed`, three lines. Likely question: "Why 2^(n/12)?" Answer: "Western music splits an octave (double frequency) into 12 equal steps, so one step multiplies the frequency by the 12th root of 2. Playing a clip faster by that factor raises it by exactly one semitone."

### 3.30 `RaceFlow/FinishMoment.cs` + `ConfettiSim.cs` + `UI/ConfettiBurst.cs` (task P6b, the finish moment)

When the kart crosses the line, the game slows down for a moment, the lap time grows on screen, then the results card comes in and, for a record or a new medal, confetti bursts out of the stamp.

- `FinishMoment` (plain C#) holds every time of the moment in one place, all in REAL seconds: `TimeScaleAt(t)` = 1 -> 0.3 in 0.15 s (ease-out), held to 0.55 s, back to 1 at 1.2 s (smoothstep, no jolt); 1 before, after and for NaN, so a mistake never leaves the game slow. `BeatFor(reducedMotion)` = 0.6 s (0 with reduced motion), `Celebrates(...)` = record or better medal, `ConfettiDelay(record)` = when the last stamp lands.
- `RaceManager`: `FinishLap()` notes `Time.unscaledTime` after the official time is taken. `UpdateSlowMotion()` runs at the top of `Update` and sets `Time.timeScale` from the curve. It does nothing while the pause holds 0 (the pause wins). `StopSlowMotion()` in `Retry`, `OnDisable` and `OnDestroy`: `Time.timeScale` is global and survives a scene load, so it must always go back to 1.
- `RaceHud`: in Results the root first gets `st-finish` only (the race HUD stays, the lap time panel grows and turns sand, drift meter, speed gauge and next-medal chip fade, every pip lights). After the beat `ShowResultsCard()` adds `st-results`, makes Retry clickable and starts the count-up. The count-up, the record shake and the confetti use `Time.unscaledTime`, so the slow motion never slows them.
- `ConfettiSim` (plain C#): 30 pieces as parallel arrays (`X`, `Y`, `Angle`, `Flip` ...), shot up in a cone, gravity 1400 px/s2, air drag, spin, a paper flip (width follows a cosine), fade over the last 0.4 s, all gone after 1.9 s. The same seed gives the same burst (tests rely on it); the arrays are reused, so a second burst makes no garbage.
- `ConfettiBurst` (a `VisualElement`, last child of the results screen): draws each piece as a turned rectangle with `painter2D` in white, blue, orange, sand. It ignores the mouse and only asks for a redraw while pieces fly.
- How I would explain it: "One class knows the timing, the RaceManager bends time, the HUD waits a beat and runs on real time, so the clock on screen is exact while the world slows down."

### 3.31 Small helpers that were missing from this sheet (audit 6)

Eleven Role 4 files the audit found unmentioned. Each: what it does, the function to point at, one likely question with an answer.

**`RaceFlow/MedalScaling.cs`** (static, plain C#). Makes the four medal times fit any track: `RaceSettings` holds them for a reference lap of `medalReferenceLapMetres` (158.1 m, the placeholder loop), and a longer track gets proportionally longer times.
- Point at: `LoopLength(points)` (straight lines checkpoint to checkpoint in driving order, the last one back to the start; under 2 points = 0) and `ScaleTimes(times, referenceLength, trackLength)` (times x `trackLength / referenceLength`, rounded to a tenth; if rounding would make two tiers equal on a tiny track it keeps the exact values; a missing length keeps the reference times). Called once in `RaceManager.SetUp`.
- Question: "Why measure straight lines, not the real driving line?" Answer: "It is shorter than the real line, but measured the same way on every track, and a ratio is all the scaling needs." (Honest: the reference times still come from the placeholder loop, so the final medal times must be re-tuned on the real track.)

**`RaceFlow/WrongWayDetector.cs`** (plain C#). Decides when the HUD shows WRONG WAY.
- Point at: `Update(kartForward, signedSpeed, toNextCheckpoint, dt)`: the dot product of the moving direction (forward, or backwards when reversing) and the direction to the next checkpoint, both flat (y = 0). Below -0.25 (more than about 105 degrees away) for 1 s = wrong way; above 0.15 for 0.4 s = right way again; in between nothing changes. Under 2 m/s it keeps its last answer. `RaceHud.UpdateCallouts` calls it; `Reset()` on GO, Retry and Recover.
- Question: "Why two thresholds and two timers?" Answer: "Hysteresis: with one threshold the warning would flicker on and off in every tight corner. The gap between -0.25 and 0.15 is a dead band where nothing changes."

**`RaceFlow/StuckDetector.cs`** (plain C#). Decides when the HUD shows "STUCK? R RECOVER".
- Point at: `Update(speed, dt)`: slower than 1.5 m/s for 3 s = stuck, but only once "armed" (the kart was faster than 3 m/s once since GO or the last recover), so a player waiting at the start is never told to press R.
- Question: "Why the arming?" Answer: "Without it the hint would pop up 3 s after GO for anyone who has not pressed W yet."

**`RaceFlow/FlyoverPath.cs`** (static, plain C#). The maths of the start-screen map fly-over (`MapFlyover` on the Main Camera moves the camera with it, then hands over to `ChaseCameraRig`).
- Point at: `Measure(points, out centre, out radius)` (box around the checkpoints, the farthest one gives the radius), `OrbitPosition(centre, radius, angle, minHeight)` (a circle above the track, high enough to see all of it), `Ease(t)` (smoothstep `t*t*(3-2t)`), `Glide(from, to, t, hump)` (lerp with a sine hump so the camera does not skim through buildings on the way down to the kart).
- Question: "Why a hump?" Answer: "A straight line from high above to just behind the kart can cut through roofs; `sin(e * pi)` lifts the middle of the path and is 0 at both ends, so start and end stay exact."

**`RaceFlow/CheckpointProgress.cs`** (static, plain C#). The checkpoint pips on the HUD.
- Point at: `CheckpointsToPass(count)` (count - 1, because checkpoint 0 is the start/finish line) and `StateOf(pip, passed)` (Passed / Next / Ahead). `RaceHud.UpdateCheckpoints` draws one diamond per checkpoint plus the finish.
- Question: "A track with 4 checkpoints shows how many pips?" Answer: "4: three to pass and the finish."

**`RaceFlow/HudEasing.cs`** (static, plain C#). The two HUD animations C# drives itself; everything else is a USS transition.
- Point at: `EaseOutCubic(t) = 1 - (1 - t)^3` (fast start, soft landing) and `CountUp(elapsed, delay, duration)` (the results time counting up after a delay; 0 s duration jumps at the delay).
- Question: "Why not a USS transition for the count-up?" Answer: "USS can animate a property, not the digits of a text. So C# writes the number each frame and `HudEasing` decides how far along it is."

**`RaceFlow/UI/SpeedArc.cs`** (a `VisualElement`). The round speed gauge, a 270-degree arc drawn with `painter2D`.
- Point at: `Set(value01, colour)` (redraws only if the change is visible: 0.2 % or a new colour, via `MarkDirtyRepaint`) and `Draw` (background arc, ten ticks, the fill from 135 degrees to `135 + 270 x value`).
- Question: "Does it redraw every frame?" Answer: "No. `generateVisualContent` only runs after `MarkDirtyRepaint`, and `Set` calls that only for a visible change."

**`RaceFlow/UI/HudColors.cs`** (static). The HUD palette for C# (the gauge and the gradient textures cannot read USS variables). Same values as the tokens at the top of `RaceHud.uss`, from `docs/art-style.md`.
- Point at: `DriftStage(stage)` (white, blue, orange) and `WithAlpha`.
- Question: "Why is the palette written twice?" Answer: "USS variables only exist for USS. Code-drawn parts need real `Color` values, so both lists must change together; the comment at the top says so."

**`RaceFlow/UI/HudTextures.cs`** (static). Unity 6.3's USS has no `linear-gradient`, so gradients are tiny 1x128 / 128x1 textures made at runtime and stretched with bilinear filtering.
- Point at: `VerticalGradient(stops)` (start and results backgrounds), `EdgeGlow(colour, alpha, left)` (the orange boost glow: `alpha x fade x fade`, very soft towards the middle), `Destroy` (called in `RaceHud.OnDisable`, textures made in code are not cleaned up by themselves).
- Question: "Why destroy them by hand?" Answer: "A `Texture2D` made with `new` lives until it is destroyed; without `Destroy` every re-enable of the HUD would leak four textures."

**`RaceFlow/UI/AzulejoPattern.cs`** (a `VisualElement`). The tile pattern behind the start, results and pause screens: per 72 px tile a diamond, a small inner diamond and a quarter circle in the corner that joins into full circles across tiles. Drawn once per resize, low opacity set in USS.
- Point at: `Draw`: one path for all tiles, one `Stroke()` at the end.
- Question: "Why one path?" Answer: "One stroke call for about 180 tiles is one mesh; a stroke per tile would be 180."

**`RaceFlow/UI/CalcadaWave.cs`** (a `VisualElement`). The Lisbon "mar largo" pavement wave used as a divider on the start, results, pause, settings and credits cards: a white band with black stone below a sine wave.
- Point at: `Draw`: white rectangle, then a filled shape whose top edge follows `middle + amplitude x sin(x / 28 px x 2 pi)`, sampled every 2 px.
- Question: "Where do the shapes come from, any asset?" Answer: "No asset: drawn in code with `painter2D`, our own work, so nothing to license."

### 3.32 Added in the polish pass (2026-11-10), where to find them

- P pause (audit 9): 3.6 and 3.28. Menu sounds (audit 10): 3.11 and 3.29, enum `RaceFlow/MenuSound.cs`. Wall bump, take-off, chained boost (audit 11): 3.15 and 3.15b. Seaside ambience (audit 12): 3.11b and 3.19. Ghost chip (audit 13): 3.26. Ahead/behind chime, wrong-way and pause cues (audit 19): 3.11 and 3.29b. Loader keys and credits (audit 20, 21): 3.18 and 3.29. KartEffects on the kart (Q10): 3.17 step 4b. `MusicPlayer` deleted (audit 18): 3.16.
- Every new clip is our own synth (`tools/audio-synth/synth_sfx.py`): `ui_open`, `ui_close`, `ui_page`, `sfx_wall_bump`, `sfx_takeoff_whoosh`, `amb_seaside_loop`.
- Item boxes (task R7): 3.33.

### 3.33 Item boxes (task R7, 2026-11-10)

Luc's four item designs (Role 3, `items_showcase.png`), all with no rivals in mind: they make YOU faster.
**Espresso** (2 s at x1.4, common), **Seagull Delivery** (3 lifebuoy rings on the road ahead, each ring = 0.9 s at x1.5, they chain), **Carcavelos Wave** (3 s at x1.6), **Dean's List** (rarest: 1.5 s golden boost + a shield for one wall bump). Boxes are azulejo tile cubes that spin, burst into shards and come back after 3 s.

Files (all Role 4): `Shared/IKartBoost.cs` (the optional contract), `RaceFlow/ItemKind.cs`, `ItemSlot.cs`, `ItemOdds.cs`, `ItemBoxState.cs`, `ItemBoxLayout.cs`, `BumpShield.cs`, `RingPath.cs` (plain C#, the rules), `ItemSettings.cs` (ScriptableObject, numbers in `Settings/ItemSettings.asset`), `ItemBoxTexture.cs`, `ItemBox.cs`, `SeagullRing.cs`, `DeansHalo.cs`, `ItemSystem.cs` (MonoBehaviours), `UI/ItemSlotHud.cs` + `UI/ItemIcon.cs` (the HUD slot), `RaceKeys.UseItemPressed()` (E). Built into `Main.unity` by `Role4SceneBuilder.BuildItems` as the root object "Items".

**Chain 1, driving through a box:** `ItemBox.OnTriggerEnter` (only if the box is there and the collider is our kart) -> `ItemSystem.OnBoxHit` (only while Racing, breaks the box, plays `el_item_box_break`) -> `ItemOdds.WeightsFor` + `ItemOdds.Pick` (catch-up odds from the last split against the personal best) -> `ItemSlot.TryStartRoll` (false if the slot is busy: the box still breaks, you get nothing).
**Chain 2, the roll:** `ItemSystem.Update` calls `slot.Tick` every frame; each new roll step plays the tick, the landing plays `el_item_ready` and fires `ItemReady` (the HUD pops).
**Chain 3, pressing E:** `ItemSystem.Update` -> `RaceKeys.UseItemPressed()` -> `ItemSystem.UseItem` (only Racing with input on) -> `ItemSlot.TryUse` -> `ItemSettings.BoostSecondsFor` -> `IKartBoost.AddBoost` (Espresso, Wave, Dean's List) -> then per item: Seagull -> `DropRings`; Dean's List -> `BumpShield.Activate` + `DeansHalo.Show`. Fires `ItemUsed` (HUD callout with the item's name, blue screen wave for the Wave).
**Chain 4, a ring:** `DropRings` -> `RoadAhead` (a straight line from the kart's nose: the first try through the next two checkpoints cut the bend and put the rings on the grass in the web build) -> `RingPath.Place` (18, 32 and 46 m along it) -> `TryPlaceRing` (ground + wall check, like a box) -> `SeagullRing`. Driving through: `SeagullRing.OnTriggerEnter` -> `ItemSystem.OnRingHit` -> `AddBoost`, chime one step higher per ring, `RingCollected` -> HUD "RING 1/3" ... "PERFECT DELIVERY!".
**Chain 5, the shield:** `ItemSystem.Update` -> `UpdateShield` -> `BumpShield.Tick(dt, Kart.Speed, ...)`: Blocked -> `AddBoost` (speed back), `el_item_shield_block`, coins fly off, HUD "BLOCKED!"; Expired -> `el_item_shield_pop`.

- Point at: `ItemOdds` rows (Espresso / Seagull / Wave / Dean's List %). Ahead 60/30/10/0, level 40/30/22/8, behind 25/30/30/15, more than 2 s behind 10/25/35/30. The further behind, the bigger the items: classic catch-up. Dean's List never comes when you are ahead.
- Point at: `BumpShield.IsBump`: a bump = the speed falls by at least 4 m/s AND at least 30 % in ONE frame. Braking loses about 0.5 m/s per frame, so it never counts. A recover (R) sets the speed to 0 on purpose: `ItemSystem.OnRecovered` -> `BumpShield.NoteTeleport` forgets the last speed, so that is not a bump either.
- Point at: `ItemBoxLayout.RowPositions`: three boxes per row across the road, at right angles to the driving direction, 14 m after every second checkpoint. The direction comes from `ItemBoxLayout.RowDirection`: the forward of the checkpoint's respawn point (`RaceManager.TrackForwards`), because the straight line to the next checkpoint cuts every bend and put rows on the grass (found in the web build, 2026-11-10). Placed in code from the checkpoints at race start, so the boxes follow Noah's and Luc's map without anyone editing their scenes.
- Question: "Why does the shield watch the speed instead of the collision?" Answer: "RaceFlow may only read the kart through its contracts. `IKartState` has `Speed` but no hit event, and adding one would change a contract for the whole team. A sudden speed drop is what a bump looks like from outside."
- Question: "Why a new interface instead of adding `AddBoost` to `IKartState`?" Answer: "Changing `IKartState` would break every class that implements it. `IKartBoost` is optional: `ItemSystem` looks it up with `GetComponent<IKartBoost>()` in `SpawnBoxes`; without it the item is still used, just without a boost, and a Console line says so."
- Question: "What happens if you hit a box while holding an item?" Answer: "`TryStartRoll` returns false, the box breaks anyway, the item you hold stays. One slot, no stacking."
- Question: "Why does the E that ends a pause not use the item?" Answer: "`resumeFrame` remembers the frame the pause ended (`OnResumed`); `Update` skips `UseItem` in that frame."
- Sounds: box break, ready, espresso (`el_item_bica`: bica = Lisbon's word for an espresso), wave (`el_item_onda`), seagull, ring, Dean's List fanfare, shield block and shield pop from ElevenLabs (David's Creator plan, `THIRD_PARTY.md`), the 25 ms roll tick from our own `synth_sfx.py`, all mastered by `process_wavs.py`.

### 3.34 Every racer, live positions and the minimap (tasks R8 + R9, 2026-11-11)

**What:** RaceFlow now runs the race for every racer, not only the player: bots (Luc's V10, later) stand on a grid behind the `SpawnPoint`, drive only while Racing, and Retry puts everyone back. The HUD shows your place ("2nd / 6") bottom right beside the speed gauge and a minimap at the right edge under the checkpoint panel.

Files (all Role 4): plain C# rules `RaceFlow/RacerGrid.cs` (slots), `LapProgress.cs` (start line crossings), `RaceStandings.cs` (who is ahead), `PositionFormat.cs` (1st 2nd 3rd), `MinimapFit.cs` (world -> map pixels), `MinimapPath.cs` (smooth line), `TrackProgressSource.cs` (T8 or the checkpoint fallback); MonoBehaviours `PlayerRacer.cs` (the player as `IRacer`), `RaceRoster.cs` (the race for everyone), `Dev/TestBot.cs` (dev-only bot), `UI/PositionsHud.cs` + `UI/MinimapLine.cs` + `UI/TriangleMark.cs` (the HUD). Built into `Main.unity` by `Editor/Role4RacersBuilder.cs` as the root object "Racers". `PlaceholderKart` implements `Shared/IKartDrivable` so the test bot can drive it.

**Chain 1, set-up:** `RaceBootstrap` -> `RaceManager.SetUp` (unchanged) -> next frame `RaceRoster.Update` sees `IsSetUp` -> `SetUp`: `TrackProgressSource.For(track, TrackPoints)` (Noah's `ITrackProgress` on the `TrackInfo` root if it exists, else a `TrackPolyline` through the checkpoints) -> `AddComponent<PlayerRacer>` on the kart (registers itself, `IsPlayer = true`, name "You", orange) -> dev test bots if `RaceSettings > Dev Test Bots` > 0 -> `PutBotsOnTheGrid`.
**Chain 2, the grid:** `PutBotsOnTheGrid` runs at set-up, when the first countdown starts (state Ready -> Countdown) and on `RaceManager.Restarted` (Retry). For every registered racer `RacerGrid.Assign` (player = slot 0 = the `SpawnPoint`, RaceManager puts the player there as before; bots = slot 1, 2, 3 ... staggered right/left, 2 m sideways, 3 m back per row, numbers in `RaceSettings`) -> `IKartState.ResetTo(slot, spawn rotation)`. A bot that registers in the middle of a race is not teleported: it gets the lowest free slot at the next of these three moments.
**Chain 3, driving:** every frame `RaceRoster.Update` sets `InputEnabled = (State == Racing && !IsPaused)` on every bot's kart, the same rule `RaceManager` uses for the player. Bots never get the player's checkpoints, timer, medals or item boxes: `RaceManager.IsKartCollider` now accepts only the player's own kart (before, any collider with an `IKartState` parent counted, so a bot would have passed your checkpoints).
**Chain 4, positions:** `RaceRoster.Update` -> `RaceStandings.Update(RacerRegistry.All, progress)` -> per racer `LapProgress.Feed(progress.DistanceAlongLap(root.position))` -> stable insertion sort (laps first, then distance). Frozen in Results, so the finish place stays. `PositionsHud.UpdatePosition` reads `roster.PlayerPosition` and `RacerCount`; hidden when you race alone.
**Chain 5, the minimap:** `PositionsHud.BuildMap` (once) -> `MinimapFit` (bounding box of the racing line, turned so the start direction points up, scaled to fit the 168 px tile with 17 px padding, same scale on both axes) -> `MinimapPath.Thin` + `SmoothClosed` (Catmull-Rom curve through the points) -> `MinimapLine` draws it with Painter2D (dark outline, light road, a chequered start/finish bar on a dark plate that sticks out on both sides of the road). The map tile and the position box use the HUD panel colour a little denser (0.92 and 0.90 instead of 0.82), so they keep their contrast over bright sand, sky and kerbs. Every frame: one dot per racer at `fit.ToMap(root.position)` in its `MapColor`; the player = orange dot, white ring, white arrow turned with `fit.ToMapAngle(forward)`, a soft halo, and an orange trail over the part of the lap already driven.

- Point at: `LapProgress.Feed`. The distance along the lap jumps from 1180 back to 0 at the line, so a kart on the grid 3 m behind the line would read 1177 m, "almost a lap ahead". Rule: the first value in the back half of the lap means "behind the line" (`LapsDone = -1`); after that a jump of more than half a lap backwards = crossed the line forwards (+1 lap), more than half a lap forwards = crossed it backwards (wrong way, -1 lap).
- Point at: `RaceStandings.SortStable`. Insertion sort that only moves a racer up while it is strictly ahead, so two karts that are exactly level never swap (no flicker on the grid). Six racers at most, no allocation, no LINQ.
- Point at: `RacerGrid.SlotOffset`: `row = (slot + 1) / 2`, odd slots right, even slots left. Slot 1 = (2, -3), slot 2 = (-2, -3), slot 3 = (2, -6) ... turned with the `SpawnPoint` rotation in `SlotPosition`.
- Question: "Why is the map fixed and not rotating with the kart?" Answer: "It is turned once so the start straight points up (at GO 'up' on the map is 'ahead' on screen), then it stays. The player learns one picture of the track; a spinning 168 px map on a phone is hard to read, and a fixed map needs no clipping or per-frame redraw of the line."
- Question: "Why is the minimap not in the very top right corner?" Answer: "The checkpoint panel is there. The map sits right under it at the right edge and the item slot moved one map width to the left (`ItemSlotHud`, `PositionsHud.MapColumnWidth`). Below about 290 px the phone's touch pedals cover the right side, so the map must stay in the top part of the screen."
- Question: "Why does the position sit beside the speed gauge on a laptop but above it on a 4:3 screen?" Answer: "On narrow screens the footer key bar (R / ENTER) moves into the gap beside the gauge (`hud--narrow` in `RaceHud.uss`); `PositionsHud.OnRootResized` uses the same 1230 px switch and moves the readout above the gauge."
- Question: "What does the dev test bot do?" Answer: "It is a stand-in for Luc's bots so the race flow can be tested: it aims at `ITrackProgress.PointAt(distance + 9 m)` and steers towards it (`TestBot.Steering`, Edit Mode tested). It brakes before a bend (`CornerThrottle`) and, if it gains less than 6 m along the lap in 2 s (wall, pile-up, driving in circles: `ProgressGain`) or falls off, it jumps to the next racing line point ahead (`LinePointAhead`), so a QA race never stalls. Off in the game (`Dev Test Bots = 0`); the Play Mode test spawns two with `RaceRoster.SpawnTestBots`."
- Limits: the checkpoint fallback line is straight from checkpoint to checkpoint, so before Noah's T8 the map line is a rounded version of that and positions are rougher in long bends; the test bots cut corners on it. Grid slots are offsets from the `SpawnPoint`, not checked against walls: on a very narrow start the outer slots could touch a barrier. Real bots need Role 1's D8 (`KartController : IKartDrivable`).
- How it could change: when T8 lands nothing changes in code (it is found with `GetComponent<ITrackProgress>()`). A results-screen place can read `RaceRoster.PlayerPosition` (frozen after the finish). The leaderboard name can go on the player with `PlayerRacer.SetDisplayName`.
### 3.35 Start screen "Carcavelos Round 3" + online leaderboard (tasks R11 + R10, 2026-11-11)

**Start screen (R11).** Same start screen states as before (RaceHud: `st-ready`, `intro-in`, breathing pill, best time; HudMenus: SETTINGS / CREDITS / SOUND), new look: the title lockup on the left, the top 10 on the right, the key legend at the bottom, the map fly-over still circling behind it. Files: `RaceHud.uxml` (start-screen block), `UI/StartScreen.uss`, `UI/TitleScreen.cs` (MonoBehaviour on the HUD object).
- **Opener:** racing stripes slide in, the letters of CARCAVELOS drop in one by one (one Label per letter, made by `TitleScreen.AddLetters`, each with its own `transition-delay`), the calcada wave wipes open, CIRCUIT slides in, the slogan CARCAVELOS ROUND 3 lands like the NEW RECORD stamp, then prompt, best time, menu and the board. All USS transitions; C# only sets the delays.
- **Light sweep:** every 4.5 s a gold glint runs over the letters. Point at: `TitleScreen.Update`: a narrow box with `overflow: hidden` (`title-glint`) moves right while a gold copy of the letters inside it (`title-glint-letters`) moves left by the same amount, so the copy stays on top of the white letters and only shows inside the band. Two translates per frame, only while it runs.
- **Reduced motion:** the USS sets every start value to its end value (`.reduce-motion .ts-letter` ...), so adding `intro-in` changes nothing; `TitleScreen` skips the sweep. No animation at all.
- Not built: the "Lab opener" (meaning still to be explained by David).

**Leaderboard (R10).** Every finished lap can be saved with a name; the top 10 of the track shows on the start screen and beside the results card. Online when the CI build has the two secrets (`backend/supabase/README.md`), otherwise on this device. Files in `RaceFlow/Leaderboard/`:
- Rules (plain C#, unit tested): `PlayerNameRules` (trim, single spaces, 2-16 characters, letters incl. accented Latin, digits, space, - and _, small rude-word list), `LapTimeRules` (5 s floor, 40 % of the Gold time, 30 min, no NaN; track names), `LeaderboardRanking` (sorting in whole ms, ties: the earlier time stays ahead, top-10 cut), `LeaderboardJson` (reads server answers, drops bad rows), `LeaderboardConfigCheck` (https + `*.supabase.co` only, public key only), `SupabaseRequests` (the exact web addresses), `LocalLeaderboardList` (device list as JSON).
- Services behind `ILeaderboardService` (`SubmitTime`, `FetchTop`, answer in a callback): `SupabaseLeaderboardService` (UnityWebRequest, 6 s timeout), `LocalLeaderboardService` (PlayerPrefs per track), `FallbackLeaderboardService` (always saves on the device first, online second, device list when offline), `FakeLeaderboardService` (tests).
- UI: `LeaderboardHud` (MonoBehaviour on the HUD, fills `start-board-host` and `results-board-host`), `LeaderboardPanel` (the card), `SaveTimeForm` (name field, SAVE, messages, "YOU ARE #4"), `UI/Leaderboard.uss`, `UI/HudBuild.cs` (two element helpers).
- Config: `LeaderboardConfig` (ScriptableObject, `Settings/LeaderboardConfig.asset`, EMPTY in the repo). `Editor/LeaderboardBuildConfig` fills it only during the CI Web build (arguments `-leaderboardUrl` / `-leaderboardKey` from the GitHub secrets) and empties it again in Role4WebBuild's `finally`; `Role4SceneBuilder.BuildAll` also always saves it empty.

**Chain 1, saving a time:** `RaceManager.LapFinished` -> `LeaderboardHud.OnLapFinished` (opens the form only if `LapTimeRules.IsAcceptable`) -> SAVE or Enter -> `SaveTypedName` -> `PlayerNameRules.Check` (refused: `form.ShowProblem(Message)`, nothing is sent) -> `SettingsStore.SavePlayerName` -> `FallbackLeaderboardService.SubmitTime` (device first, then `SupabaseLeaderboardService`: POST, then HEAD with `Prefer: count=exact` for the place) -> `form.ShowSaved` ("YOU ARE #4") -> `Fetch` the list again; the row just saved is bright orange, other times saved from THIS browser (`LocalLeaderboardService.SavedHere` + `LeaderboardRanking.IsMine`: same name AND same time to the ms) get the soft orange "you" tint, the same orange as the player on the minimap. A stranger who picks your name is not highlighted.
**Chain 2, typing:** `LeaderboardHud.UpdateTyping` sets `RaceKeys.TextEntryActive` while the cursor is in the field and one frame after; then every RaceKeys check (M, O, Enter = retry ...) reads "not pressed", so letters stay text. It runs at execution order -60, before RaceHud's menus (-50) and RaceManager (0) read the keys. Tab jumps into the field.
**Chain 3, offline:** a failed or timed-out request -> `Success = false` -> the fallback answers from the device with `Source = OfflineFallback` -> the card says "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE" once (`LeaderboardPanel.NoteFor`) with the OFFLINE chip, and the place reads "#1 ON THIS DEVICE" (`SaveTimeForm.RankLine`), never "YOU ARE #1", so a local place never looks like an online one. An empty board (fresh browser, nobody saved yet) says "NO TIMES YET - BE THE FIRST!"; no sample names ever ship (fake rows only in tests).
**Chain 4, the Web page (phones):** `RaceFlow/Web/WebPage.cs` + `CarcavelosWebPage.jslib` (two tiny browser functions; in the Editor and tests they do nothing). `LeaderboardHud` tells the page when a race runs (countdown or racing) with the browser event `cc-race-state`; the template's `style.css` hides the steer/pedal/Recover/Retry buttons otherwise, so on the title and results screens they never cover the board, the name field or SAVE (desktop: the touch layer is hidden anyway, nothing changes). On a phone the name field opens the browser's own text box (`window.prompt`) instead of focusing the game's field: a phone only opens its keyboard for a real finger tap on a real text box, and the game's tap reaches Unity a moment later as a synthetic click (iOS never opens the keyboard for that). The prompt closes before SAVE is needed, so the keyboard can never cover SAVE.

- Question: "What stops someone from sending a fake time or a rude name?" Answer: "Three walls. The game checks name and time before sending. The database checks them again with CHECK constraints and Row Level Security, so the public key can only add rows that pass and read; it can never update or delete. And everything that comes back is checked again (`LeaderboardJson.IsGoodRow`, `LeaderboardRanking.WithoutTooFast`), so a bad row is dropped, never shown. A script can still post valid-looking times; the optional rate limit and deleting rows in the dashboard handle that."
- Question: "The key is in the Web build, so why not in the repo?" Answer: "It is public by design and only allowed to insert and read. But a repo key lives in the Git history forever, scanners flag it, and forks would write into our database. A GitHub secret can be swapped in a minute without code."
- Question: "Why does Enter not retry while you type?" Answer: "`RaceKeys.TextEntryActive`: while the name field has the cursor, every game key reads as not pressed. It stays on for one frame after the Enter that saves, because RaceManager reads Enter later in the same frame."
- Question: "What if the server sends HTML or a giant answer?" Answer: "`TryParseRows` only accepts a JSON list under 64 KB; anything else counts as offline. Names are shown with rich text off, so even a tag could never become formatting."

### 3.36 Beach ambience: waves + surfers (task R12, 2026-11-11)

Carcavelos is a famous surf beach right next to the campus, so the seaside part of the lap gets rolling waves and a few surfers. Pure decoration: nothing in the race reads it, and if anything goes wrong it shows nothing and the race runs as before.

Files (all Role 4, own folder `Assets/_Project/Ambience/`, asmdef `CarcavelosCircuit.Ambience` references only `Shared`): `WaveTuning.cs`, `WaveSchedule.cs`, `WaveState.cs`, `WaveProfile.cs`, `WaveField.cs`, `WaveSample.cs`, `ISurfWaves.cs`, `SurferTuning.cs`, `SurferState.cs`, `SurferBrain.cs`, `SurferBody.cs` (rider + poses), `SurferBoard.cs`, `MeshShapes.cs` (round tube / ball / cap shapes), `SurferLook.cs`, `SprayTrail.cs`, `SeaLookup.cs`, `IGroundProbe.cs`, `BeachScanner.cs`, `BeachLine.cs`, `WaveMeshWriter.cs` (plain C#, the rules and the meshes), `PhysicsGroundProbe.cs` (one raycast), `AmbienceSettings.cs` (ScriptableObject, every number in `Ambience/Settings/AmbienceSettings.asset`), `SeasideAmbience.cs` (the MonoBehaviour on the "Ambience" object in `Main.unity`), `ISurferVisual.cs` + `SurferModelPuppet.cs` + `SurferRiders.cs` (swap seam: imported rider models), `ThirdParty/Rocketbox/` (2 Microsoft Rocketbox avatars, MIT, `LICENSE.md` next to them) + `Editor/RocketboxSurferBuilder.cs` (turns them into surfer prefabs), `Editor/AmbienceBuilder.cs` (makes the settings asset and the two materials, called by `Role4SceneBuilder`).

**Chain 1, finding the beach (once, after a track scene loads):** `SeasideAmbience.OnSceneLoaded` -> next `Update` -> `PlaceInLoadedScenes` -> `TryPlace(scene)` -> `FindSea` (loops over the scene's root objects and their renderers, exact names from `AmbienceSettings.SeaNames`, first `Backdrop_Sea` = Luc's sea; no `GameObject.Find`) -> `FindAnchors` (points on the racing line: Role 2's `ITrackProgress.RacingLine` if the scene has one, plus the objects named `SpawnPoint`, `Checkpoint`, `RespawnPoint`) -> `BeachScanner.Scan` -> `Build` (two meshes as children of the Ambience object).
**Chain 2, every frame:** `SeasideAmbience.Update` -> `Simulate(Time.time, Time.deltaTime)` -> `WaveField.Update` (which waves exist now) -> `SurferBrain.Step` per surfer -> `WaveMeshWriter.Write` (all waves into one mesh) -> `SurferBody.Write` per surfer (all surfers into one mesh).

- Point at: `BeachScanner.Scan`. Luc's sea is one flat plane 8 km wide that lies under the whole world, so its box cannot tell where the coast is. The ground can: from each racing-line point we walk out towards the sea (first guess of the direction: from the track to the middle of the sea's box, `SeaLookup.SeawardGuess`) and ask the ground height every 2 m (`IGroundProbe`, in the game a raycast against Luc's ground `MeshCollider`) until it drops below the sea level. The racing-line point with the shortest walk picks the beach. Then the waterline is measured at 80 points along 240 m of coast, the frame is turned to follow the coast, and each point remembers its waterline, the slope of the sand (for the foam running up) and whether the sea in front is open (a rock, pier or the fort = no waves there). On `Track_CampusWorld` it finds the beach in front of the promenade at about (-119, 0, -216).
- Point at: `WaveSchedule.ActiveWaves`. Surf comes in sets: 4 waves 8.5 s apart, then 14 s calm. Wave number n starts at `set x 48 s + place in set x 8.5 s`; the clock alone decides where every wave is, so the same time always gives the same sea (the tests rely on that). Negative wave numbers are the waves from before the race started, so the sea is never empty at the start.
- Point at: `WaveProfile.Broken`. A wave breaks first at its peak when it reaches 30 m from the waterline; then the break runs along the wave both ways at 5.2 m/s ("peeling"). Surfers ride just ahead of that edge at 5 to 7 m/s: a slow one gets caught by the foam and falls. Close to the sand every part has broken.
- Point at: `SurferBrain.IsAllowed`: the rule table. Paddling -> Waiting -> Catching -> Riding -> Finished -> Paddling, or Riding -> Falling -> Hidden -> Paddling. Every other change is refused (`TryChange` returns false), e.g. no riding without catching the wave first, and a fall always ends out of sight.
- Question: "Why does the ambience not need RaceFlow or Track?" Answer: "It listens to Unity's `SceneManager.sceneLoaded` itself and reads the track only through names and the Shared contract `ITrackProgress`. So no RaceFlow game code changed; the scene builder only adds one object (3 lines)."
- Question: "Why so few meshes and not one object per wave and per surfer?" Answer: "Draw calls cost the most in a browser and on a phone. All waves share one mesh and one material, all surfers another: three draw calls for the whole beach (water, foam lace incl. the surfers' spray, surfers). The triangles never change, only the points move every frame (about 3,600 wave points and about 3,600 surfer points for 4 surfers)."
- Question: "How are the surfers animated without an animation file?" Answer: "Each pose (lying and paddling, sitting, standing in a surf stance) is a list of 19 points: 18 joints plus the point the surfer looks at. Poses blend by moving the joints (`Vector3.Lerp`); the stance bobs, the arms keep balancing and the upper body leans into each carve. The body is smooth tubes through the joints (`MeshShapes.Chain`: thick at thigh and calf, thin at knee and wrist, each ring turned as little as possible so a bent leg never twists) plus balls for head, shoulders, hands and feet, a hair cap, a nose and ears. Made-up figures in two-tone wetsuits (dark slate, never pure black, with a coloured chest panel and stripe), no real person."
- Point at: `SurferBody.Write(..., drawRider)`, `ISurferVisual` and `SurferModelPuppet.Pose`. The riders are real rigged humans from the Microsoft Rocketbox Avatar Library (MIT, approved by David on 2026-11-11; only the two barefoot "Sports" avatars, 7,358 triangles each, textures cut to 1024). `RocketboxSurferBuilder` (editor, called by the scene builder) keeps one mesh, paints a wetsuit over the photo texture (each triangle gets suit, coloured panel or skin from the bone that moves it: hands and feet stay skin, chest and shoulders get the panel, the photo's light and dark stay so folds show), makes URP materials and a prefab with `SurferModelPuppet`, and puts the prefabs into `AmbienceSettings > Surfer Models`. No animation clips: every frame the puppet bends the model's bones into the code-built pose (`SurferBody.PoseJoints`): each bone is turned so it points from its joint to the next one (`Aim`, `Match`), the model is scaled so its legs are as long as the code-built ones, and moved so its hips sit where the pose wants them. So paddling, the pop-up, the stance, the lean and the fall come from one pose list. `SurferRiders.Spawn` makes one copy per surfer (models take turns); `SeasideAmbience` then builds only the boards in code. Empty list, or any model without a puppet = every rider built in code again (never a mix), one log line.
- Question: "What if the sea is not found?" Answer: "One plain Console line (`SeasideAmbience: no sea found in ...`) and nothing else. Same if the sea is found but no beach is near the racing line. The race never waits for it."
- Limits: the beach is found from the ground colliders, so a track scene whose ground has no colliders gets no waves (one log line says why). The waves are drawn see-through in the order "back, face, lip" for a camera on the beach looking out to sea (the race camera); seen from out at sea, overlapping parts can sort wrong. The waterline is measured once at load, not every frame. No sound of its own: `RaceMusic` already plays the seaside loop for the whole lap. From the racing line the surf is 60 to 100 m away, so it reads as moving white lines; the full detail shows in the map fly-over and from the sand.
- How it could change: more surfers or bigger waves = numbers in `AmbienceSettings.asset`; another track with a sea under a different name = add the name to Sea Names; a bodyboarder or a boat = one more pose list in `SurferBody`; another rider model = one more entry in `RocketboxSurferBuilder.Avatars` (or any rigged model with a `SurferModelPuppet`), no change to the surf rules.

---

## 4. Tests

All in `Assets/_Project/Tests/EditMode/` (asmdef references Shared, Track, RaceFlow, Audio, Ambience, Unity.InputSystem; editor only).

| File | What it proves |
|---|---|
| `RaceTimerTests` | No counting before Start; frame times add up; Stop freezes; Reset goes to 0; negative frame time ignored; official lap time comes from the timestamps (a minute of 60 fps frames, a long frame), a second Stop changes nothing, `SecondsSinceStart` never negative; `Format`/`FormatMilliseconds` (0:00.000, 1:23.456, 59.9996 -> 1:00.000, 100:00.000, negative -> 0); `FormatDelta` always signed, -0.0004 -> "+0.000". |
| `CheckpointValidationTests` | 0..N-1 in any order is fine; no checkpoints, numbering from 1, a duplicate or a negative index is reported. |
| `FallCheckTests` | Below the fall height = fallen; no second recover during the 0.5 s pause; a real fall after the pause counts. |
| `ShownNumberTests` | First value and new values count as changed, the same value does not; `Forget` makes the next one a change. |
| `RaceKeysTests` | Letters, Space, Enter, arrows start the race; Alt, Ctrl, Shift, Windows/Cmd, Tab, Escape, F1/F11/F12/F24, Print Screen do not. |
| `RaceStateMachineTests` | Starts in Ready with input off; countdown fires 3, 2, 1, 0 then Racing with input on; not over at 2.9 s; one 10 s frame still fires every step; Finish only while racing, no double finish, input off in Results; retry countdown at 0.5 s per step; StateChanged reports Countdown, Racing, Results; bad settings (0 steps, 0 s) cannot freeze the race. |
| `PersonalBestTests` | First lap is a record; slower or equal is not, faster is; no split delta without a best lap; deltas ahead (-0.5) and behind (+1); out-of-range index ignored; save string round trip; dot not comma; broken saves = no record; other checkpoint count keeps time, drops splits. |
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
| `GameSettingsTests` | Defaults (100 %, sound on, motion on); 10 % steps stop at 0 and 100 %; levels clamped to 0..10; mute silences the listener and keeps the master volume; master up unmutes, down does not; music/effects never touch mute; on/off rows (left off, right on, click flips); save string round trip with a dot; empty, broken or partly broken saves keep defaults; out-of-range values clamped; an odd saved volume snaps to the grid. |
| `RacePauseTests` | Focus loss pauses in Countdown and Racing, not on the start or results screen; a second focus loss changes nothing; stays paused until a key or click (focus alone does not resume); a key while not paused is no resume; Clear ends it. P key (audit 9): pauses in Countdown and Racing, not on the start or results screen, no second pause on top of a focus pause. |
| `MenuNavigationTests` | Down wraps to the top, up from the top goes to the last row, two pages flip, an empty list is safe. `RaceKeysTests` adds: O, C, M and P never start the race. |
| `FinishMomentTests` | Full speed at 0 s and after 1.2 s; 0.3 during the hold; NaN, negative and infinity = full speed; a 1 ms sweep never leaves 0.3..1; only down, then only up; no jump at the joins; `IsSlowMotionRunning` edges; beat 0.6 s (0 with reduced motion) ends during the speed-up; confetti for a record or a better medal only, never for the same or a worse medal, no medal, or with reduced motion; confetti waits for the stamp that lands last. |
| `ConfettiSimTests` | The asked number of pieces at the origin; the same seed gives the same burst; every piece starts going up; gravity pulls every piece down in the end; fresh = alpha 1, fading near the end; done after the longest life; a 3 s hitch is clamped to 0.05 s; zero, negative or NaN steps change nothing; Clear, a negative count and no burst at all = done; a second burst reuses the arrays. |
| `PersonalBestTests` (P5 additions) | Best medal only goes up; survives the save string; a save from before medals loads with no medal; a broken medal number keeps the time but no medal. `HudFormatTests` adds `OneDecimal` (0.84 -> "0.8", 1.25 -> "1.3", negative -> "0.0"), and for the ghost chip (audit 13) `TimeTenths` (62456 ms -> "1:02.4", 58199 -> "0:58.1", cut not rounded, negative -> "0:00.0", digits wrapped) and `GhostName`. |
| `GhostRecordingTests` | Empty recording has no pose; uneven frames still give samples exactly every 0.05 s; halfway between two samples = halfway position and rotation; clamped before the start and after the end; one sample works at any time; a teleport jumps instead of gliding (also inside one frame); repeated or old times add nothing; stops at 120 s; `Clear` starts over; end velocity; save text round trip (under 1 cm, under 0.2 degrees); yaw across 360 stays a small change; same text on a Portuguese laptop; a 60 s lap stays under 40 KB; other track or checkpoint count = ignored; broken texts = no ghost. |
| `GhostLoaderTests` | Own ghost wins over the shipped one; fresh browser races the shipped ghost; broken or outdated own ghost falls back to the shipped one; nothing valid (or a shipped ghost from the old layout) = no ghost; save key next to the personal best. |
| `MedalScalingTests` | A 10 m square is 40 m; the placeholder loop matches the reference length in `RaceSettings`; under 2 points = 0; same length keeps the times, twice the length doubles them; rounded to a tenth and still valid; a tiny track keeps the tiers apart; no usable length keeps the reference; null stays null; default track scene order. |
| `WrongWayDetectorTests` | Driving towards the checkpoint is fine; 1 s away = wrong way; turning round clears it after the exit time; sideways never triggers; standing still keeps the answer; reversing towards the checkpoint is fine; height is ignored; Reset clears it. |
| `StuckDetectorTests` | Waiting at the start is never stuck; after driving, 3 s still = stuck; grinding slower than 1.5 m/s counts as still; driving again clears it; reversing fast also arms it; Reset disarms. |
| `FlyoverPathTests` | Centre and radius of a square; no points = 0; a big track is seen from high and far enough, a tiny one never below the minimum; `Ease` stays in 0..1; `Glide` ends exactly at the target with a hump on the way. |
| `CheckpointProgressTests` | The start/finish line is not a checkpoint to pass; first pip next at the start; after two, two passed and the third next; after all, the finish pip is next. |
| `HudEasingTests` | Ease-out starts at 0, ends at 1, is fast at the start, clamps, never goes back; the count-up waits for its delay, a 0 s count-up jumps at the delay. |
| `SplitChimeTests` | Audit 19: ahead +2, behind -2, no best lap 0; exactly even and -0.4 ms count as behind like the chip, -0.6 ms as ahead; pitch 1, 1.1225, 0.8909, 2 for 0, +2, -2, +12. |
| `WallBumpDetectorTests` | Audit 11: a 15 m/s drop in one frame is a bump; the hardest brake in a 0.2 s frame is not; a small scrape, the air, the countdown, the first frame, a reset and a paused frame are not; grinding gives one thud, another after the cooldown; reverse counts; volume 0.5 to 1. |
| `TakeOffDetectorTests` | One whoosh after 0.15 s of air; a short hop is silent; the next jump fires again; nothing without input; after spawn or a reset it waits until the kart touched the ground. |
| `BoostCueTests` | One whoosh per new boost (same frame or one frame after the release); a chained boost whooshes again; a sparkless drift, a wall, the air or input off while boosting do not; Reset forgets the running boost. |
| `MenuSoundTests` | Audit 10: the three volume rows ask for the slider-step sound, the two on/off rows for the switch sound. |
| `GhostFadeTests` | Hidden close, full far away, half in the middle, never weaker further away; bad settings do not break it; end fade 1 to 0; the strongest fade wins (camera, kart, end). |
| `ItemSlotTests` | R7: new slot empty; the roll lands exactly once, then the item is ready; no use while rolling; a busy slot refuses a new item; None is never rolled; every item is one use; Clear empties even while rolling; roll steps grow from 0 to all, never go back and slow down towards the end; the icon cycles through the four items. |
| `ItemOddsTests` | R7: every row adds up to 100; no best lap yet = level odds; the split picks the right row (ahead, level, behind, far behind); further behind = more Wave and Dean's List; Dean's List never when ahead; `Pick` splits the roll by the weights and never returns None. |
| `BumpShieldTests` | R7: a big sudden drop is a bump, braking, speeding up or a too small drop is not; one bump is blocked once, then the shield is gone; no bump = runs out after its time; the first frame and a teleport (recover) never count. |
| `RingPathTests` | R7: the Seagull's rings follow the road: straight ahead, round the corner to the next piece, straight on past the end, upright on a slope, one spot per ring, an empty first piece is skipped. |
| `ItemBoxStateTests` | R7: a new box is there; a hit removes it and a second hit does nothing; it comes back after the respawn time, once; Reset brings it back at once. |
| `ItemBoxLayoutTests` | R7: which checkpoints get a row; direction ignores height; the row sits the given metres along the road and stays inside a short gap; boxes are spaced across the road, centred and at right angles to the driving direction; zero boxes = empty. |
| `WaveScheduleTests` | R12: spawn times follow the set pattern (also negative wave numbers); the sea is full at the start; waves roll in at their speed and stop at the waterline; sorted furthest out first; fewer waves in the calm gap; a too small buffer is never overrun; same time = same waves; middle waves of a set are the biggest; NaN or infinite time = no waves; bad tuning (zero speed, NaN period, break beyond the spawn) is made safe. |
| `WaveProfileTests` | R12: a swell grows towards the break and fades in; the peak breaks first, then the break peels both ways; everything has broken near the sand; the curl is strongest next to the break; foam shrinks to flat at the waterline; the ends fade; the cross-section starts and ends flat and invisible, stays in order for every shape, the curl throws the lip forward, broken = white; the foam runs up the sand; NaN in = 0 out. |
| `SurferBrainTests` | R12: paddles to the waiting spot; no wave = keeps waiting; a null sea is a calm sea; full ride (catch, ride away from the peak, kick out, paddle back); a too small wave is let through; a wave gone while catching; caught by the foam = fall, hidden, back out at sea; fall chance 1 = every ride falls; the end of the beach ends the ride; zero speeds = no NaN; bad time steps change nothing; the rule table allows the life cycle and refuses shortcuts; same seed = same surfing. |
| `BeachScannerTests` | R12 (made-up sloped ground): finds the waterline from the closest track point; a turned coast turns the frame; a hole in the ground is not the sea; the end of the ground mesh counts as open sea; starting in the water is not a beach; track too far from the sea, a reef across the bay, no track points or no ground = no beach, with a reason; gaps filled, spikes smoothed. |
| `SeaLookupTests` | R12: sea names match exactly in any case, lookalikes (SeaWall, the material name) do not, null or empty never; the seaward guess points to the middle of a sea plane under the world or straight at a sea beside the land, refused on its middle, for an empty box or NaN. |
| `AmbienceMeshTests` | R12: the beach frame puts points in the world (up the sand follows its slope), far out uses the smoothed coast, heading 0 faces the beach, NaN is safe; the wave mesh has matching sizes and valid triangles, never NaN or taller than 6 m over two minutes; closed sea = invisible; the wave field finds incoming waves only from the sea side. |
| `SurferBodyTests` | R12: every state with every hair and wetsuit style writes sane geometry (no NaN, unit normals, fits in 3.5 m); every point is in a triangle and every surfer uses only its own points in the shared mesh; no triangle is drawn inside out (standing, lying, sitting); standing is taller than lying with a deep knee bend and arms out; the rider leans into the turn, the feet stay put, a silly lean is cut; paddling arms take turns; board rocker (flat under the feet, nose higher than tail, NaN safe); hidden = folded away; spray only while riding, rises and fades, NaN and a clock that jumps back are safe; spray ribbons live only in the foam layer. |
| `SurferVisualTests` | R12 swap seam: without the rider only the board is built (rider points folded away); rider and board only move apart in a fall, the board floats a little higher while paddling; `SurferModelPuppet` follows the board, hides when the surfer is gone and comes back, no brain or no Animator = no crash; the puppet bends a stand-in skeleton into the surf stance (thigh points at the knee, shin at the foot, hips in place, legs scaled, the same pose twice gives the same result, too few joints = only moved); `SurferRiders` makes one hidden copy per surfer only when every model has a puppet (no models, empty list, no surfers, a plain or missing model = built in code, nothing copied). |
| `PlayerNameRulesTests` | R10: good names (2 and 16 characters, accents, Łukasz, spaces, - and _, "Dickens"/"Essex"/"Hancock") pass; spaces trimmed and collapsed; empty, null, only spaces; 1 character; 17 characters and a pasted wall; emoji, SQL-ish `'; drop`, `<b>`, tab, newline, other scripts' digits, × refused; rude words in four languages incl. spaced out, digit tricks and ß; stored names must already be clean; same name ignores case; one message per problem. |
| `LapTimeRulesTests` | R10: minimum = 40 % of Gold, never under 5 s, no medal times = 5 s; both edges accepted; too fast, too slow, 0, negative, NaN, infinity refused; track names only plain characters, at most 40. |
| `LeaderboardRankingTests` | R10: insert at the right place; empty list; ties keep the earlier time ahead, compared in whole ms; top-10 cut (11th out, a tie with 10th stays out); sorting mixed rows keeps tie order and does not change the input; impossible times for the track dropped; rank by name and time; device list save/load, keeps 10, broken or hand-edited saves read safely, Top is a copy. |
| `LeaderboardJsonTests` | R10: a good answer is read; empty list; bad rows (HTML, SQL-ish, too short/long, rude, unclean, too fast/slow, missing fields) dropped, good ones kept; error objects, HTML pages, cut-off JSON and huge answers refused; at most 10 rows; the insert body is valid JSON and escapes quotes; Content-Range totals. |
| `LeaderboardConfigCheckTests` | R10: empty = device only; half filled in = problem; good URL + publishable key and an anon JWT accepted; http, wrong domain, paths, ports, user names, look-alike hosts refused; secret and service_role keys refused; malformed and too long keys refused. |
| `LeaderboardServiceTests` | R10: online works = online answers; online fails = the device keeps the time and says offline (also for times saved while online); no online service = this device, not offline; a broken track fails cleanly; the fake ranks and fails as told; the request addresses; decimals with a dot on a Portuguese computer; readable track titles; the result line words; the place reads "#N ON THIS DEVICE" unless online; the empty and offline notes (offline said once); only times saved from this browser count as "mine", a broken track name gives an empty list. |
| `WebPageTests` | R10/R11: outside a Web build the page calls do nothing: no name prompt (the game's own field), the name is kept (empty, long, null), sending the race state never throws. |

Run: Unity Editor, Window > General > Test Runner > EditMode > Run All. Command line (Unity closed), Windows:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults/editmode.xml
```

Play Mode `LeaderboardPlayTests.cs` (R10/R11): the start screen has the 10 title letters, the slogan and a 10-row board; after a robot lap (it waits on the grid until the lap is long enough for `LapTimeRules`) a bad name is refused with a message and nothing is sent, a good name with extra spaces is sent cleaned, remembered, ranked #1 and shown highlighted in the results top 10 (fake service, no network); Retry closes the form.

Play Mode (`Assets/_Project/Tests/PlayMode/ItemBoxPlayTests.cs`, R7, loads `Main` on `Track_CampusWorld`): boxes are placed on the track (17 in 6 rows); driving into a box rolls an item and E uses it as a boost (`IsBoosting` true); a full slot breaks the box but keeps the item; a box comes back and Retry resets the slot and every box; no items before the race starts. Run with `-testPlatform PlayMode`.

How I would explain it: "Edit Mode tests run without pressing Play, in under a second. That only works because the rules are plain C# classes that do not need a scene."

---

## 5. Swap day checklist (when Noah's T4/T5 and Alessandro's D1 land)

**Status 2026-11-10:** A done (steps 1-4; step 5 skipped: `Track_Campus` is still empty, so the test loop stays the last fallback). B 7-9 done (Role 1's `Kart` drives Main, Play Mode tests pass with it on `Track_CampusWorld`). Step 4b: Luc's builder via help PR; `NovaCampusBuilder` still adds the old names, which keep working as subclasses. Steps 10-12 for the camera and the delete wait for D2 (#40) and the builder swaps; `PlaceholderKart` stays until Alessandro's `Steer` change is in.

Do it on a branch `role4/<task-id>-swap-contract`, after `git pull` of `main` with their PRs merged.

**A. Track (Noah: `TrackInfo`, `Checkpoint`, `RaceRules` in `CarcavelosCircuit.Track`)**
1. Read their code first and confirm the members match `docs/interfaces.md`: `TrackInfo.Checkpoints` (`Checkpoint[]`), `SpawnPoint`, `GetRespawnPoint(int)`; `Checkpoint.Index`; `RaceRules(int)`, `StartLap()`, `PassCheckpoint(int)`, `TryFinish()`, `LastValidCheckpoint`, `Reset()`.
2. Confirm the checkpoint 0 meaning is the same as in `PlaceholderLapRules` (now written into `docs/interfaces.md`; a wrong numbering shows a red `RaceManager` error at start): index 0 = start/finish line, it is never passed with `PassCheckpoint`; `PassCheckpoint` accepts only 1, 2, 3 ... in order; `TryFinish` is true once all of them were passed, and only once; `LastValidCheckpoint` is 0 before the first checkpoint. `RaceManager.OnCheckpointEntered` relies on exactly this (index 0 -> `TryFinish`, others -> `PassCheckpoint`). If Noah's rules differ, adapt `OnCheckpointEntered`, not his class.
3. `RaceManager.cs`: add `using CarcavelosCircuit.Track;`; field `private PlaceholderLapRules rules;` -> `private RaceRules rules;`; `private PlaceholderTrack track;` -> `private TrackInfo track;`; `new PlaceholderLapRules(checkpointCount)` -> `new RaceRules(checkpointCount)`; `SetUp(GameObject kartObject, PlaceholderTrack loadedTrack, ...)` -> `TrackInfo loadedTrack`. Delete the two "swap to ..." comments.
4. `RaceBootstrap.cs`: add `using CarcavelosCircuit.Track;`; `PlaceholderTrack track` (Start) -> `TrackInfo track`; `FindTrackIn` return type and `GetComponentInChildren<PlaceholderTrack>()` -> `TrackInfo`.
4b. Other track builders that use the placeholders: `Visuals/Editor/CampusWorldBuilder.cs` (Luc, `AddRaceParts`, two lines marked "swap to Role 2's TrackInfo/Checkpoint") and `tools/nova_track` (#31) if merged. Ask their owners to swap them in the same week, otherwise their editor code stops compiling when the placeholders are deleted (step 11).
5. Fallback: once `Track_Campus` has a `TrackInfo`, set `fallbackTrackSceneName` to empty in `RaceSettings.asset` (Inspector), so the placeholder scene is no longer needed.
6. Test cameras in `Track_Campus` are fine: `TurnOffTestCameras` switches off every Camera GameObject and AudioListener in the track scene. Only ask Noah to keep the test camera on its own GameObject (that whole object is switched off) and to make checkpoint colliders `isTrigger`.

**B. Kart (Alessandro: `Kart` prefab, root has `KartController : IKartState`)**
7. Check the kart's Rigidbody and colliders are on the `Kart` prefab root or its children when it is spawned (`RaceManager.IsKartCollider` remembers them at spawn, so the sphere may be unparented later in `Start`). A sphere that is a separate object never under the prefab would not count.
8. Check `InputEnabled = false` stops all input (and does not release a drift into a boost) and `ResetTo` zeroes velocity and clears drift/boost (request Q6 in `TASKS.md`).
9. No code change for the kart: the prefab must be named exactly `Kart` and live under `Assets/_Project/Driving/`, with `KartController` on the root. Then run **Carcavelos Circuit > Build Role 4 Scenes**; `FindRealKartPrefab()` picks it up and the Console says "Role4SceneBuilder: using Role 1's kart ...". Never set the prefab by hand in Main's Inspector: the builder rewrites its own objects in `Main.unity` on every run.
10. Camera: done in Q13. The builder puts a `CinemachineBrain` + `ChaseCameraRig` on the Main Camera and Role 1's `Driving/ChaseCamera.prefab` into Main as the root object `ChaseCamera`; `PlaceholderChaseCamera` is deleted.

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
3. **Why multiply by `Time.deltaTime`?** Frames have different lengths on different laptops. Adding the real frame time makes the timer and smoothing correct at any FPS. `Time.timeScale` is 1 except while paused (task P7): then it is 0, so `Time.deltaTime` is 0 and `Time.timeAsDouble` stands still, and the lap timer stops on its own. It is also below 1 for 1.2 s after the finish line (task P6b slow motion), after the lap time is taken. Things that must move during the pause (the toast, music fades) use `Time.unscaledTime` / `unscaledDeltaTime`.
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
23. **What happens when I switch the browser tab mid-lap?** `RaceManager.OnApplicationFocus(false)` sets `Time.timeScale = 0`: physics, `Time.deltaTime` and `Time.timeAsDouble` stand still, so the lap time (two `timeAsDouble` timestamps) gains nothing. The PAUSED overlay waits for a click or key, and that key does nothing else.
24. **How does reduced motion work without an `if` in every animation?** One class `reduce-motion` on the HUD root. The USS rules at the end of `RaceHud.uss` override every slide, shake and punch with its resting value, so the C# that adds the pop classes stays the same. Only the breathing pill (an inline style) needs one `if`.
25. **Why does the HUD use `Time.unscaledTime` at the finish?** The finish slow motion sets `Time.timeScale` to 0.3, and `Time.time` / `Time.deltaTime` follow it. The results count-up, the stamp timings and the confetti would then run three times slower and get out of step with the USS transitions (UI Toolkit animates in real time). `Time.unscaledTime` ignores the time scale. The lap time itself is safe either way: it was taken from two `Time.timeAsDouble` timestamps before the slow motion started.
26. **Why does P pause and not Escape?** In a browser Escape leaves fullscreen and cannot be caught reliably. P goes through exactly the same `RacePause` rule as a lost focus (`OnPauseKey` and `OnFocusLost` both call `TryStart`), so there is one pause, not two.
27. **How can sound play while the game is paused?** The pause sets `AudioListener.pause = true`, which stops every source except those with `ignoreListenerPause`. `RaceSounds.Awake` sets that on the menu source only, so M and the pause/resume cues are heard and the engine and music stay silent.

---

## 7. Still open (be honest if asked)

Fixed on 2026-11-08 and no longer weak spots: drift boost after the finish, drift cancelling itself on the placeholder kart, hard-coded hint time, campus scene staying loaded on fallback, the two build lists disagreeing, Retry button focus, manual kart swap. Robustness pass (audit, same day): race no longer depends on the placeholder camera, kart found by its Rigidbody/colliders, checkpoint check with red errors, builder keeps other people's work, input forced off outside Racing every frame, lap time from timestamps, HUD without per-frame strings, one recover per fall, no landing thud after a teleport, stage chimes no longer bend each other, Alt/F11 no longer start the race.

- **Leaderboard (R10) not yet online**: no Supabase project exists yet (BLOCKED on David, `backend/supabase/README.md`); until the two GitHub secrets are set, every build keeps times on the device ("THIS DEVICE"). The online path (`SupabaseLeaderboardService`) is checked by unit tests on its addresses and answers, never against a real server. Phone name entry: phones type the name in the browser's `window.prompt` (`WebPage.PromptName`); checked only in the code and the template preview, not yet on a real phone with a real Web build. The rude-word list blocks "Scunthorpe"-style names that contain a word from the "anywhere" list.
- **Sphere kart and checkpoints**: handled for a sphere that is under the prefab at spawn (`IsKartCollider`), but not yet tested with Alessandro's real kart. Check the day D1 lands (checklist step 7).
- **Checkpoint 0 meaning written down, not yet confirmed by Noah**: `docs/interfaces.md` now says index 0 = start/finish, indices exactly 0..N-1. A wrong numbering gives a red error at start instead of a silent unfinishable lap. Confirm before T5 is merged.
- **Personal bests reset on every itch.io re-upload** (decided and accepted in `docs/decisions.md`): Web PlayerPrefs live under a folder named after the page URL, and itch.io changes it per upload.
- **Cinemachine cameras in track scenes**: `TurnOffTestCameras` only finds `Camera` components and `AudioListener`s. A `CinemachineCamera` has neither, so once Role 1's camera with a `CinemachineBrain` is in Main, a Cinemachine test camera left in a track scene could take over the view. Ask owners not to leave Cinemachine cameras in their scenes, or extend the method then.
- **Camera object is switched off as a whole** (`RaceBootstrap.cs:97`, `SetActive(false)`): anything else on that GameObject or its children goes off too. Fine as long as test cameras sit on their own object.
- **Kart prefab name is strict** (`Role4SceneBuilder.FindRealKartPrefab`): exactly `Kart`, under `Driving/`, `IKartState` on the root. A prefab called "Kart Variant" or with the controller on a child is ignored and the placeholder stays, silently apart from the missing Console line.
- **Bloom not yet checked in a Web build with real effects**: it depends on Role 3's sparks and flames being bright enough (above threshold 0.9). Check it looks right and the frame rate stays fine on a normal laptop.
- **Polish-pass sounds not yet heard in a Web build** (P4 audio was, see `docs/builds.md`): menu sounds, wall bump, take-off, chained boost, seaside ambience, ahead/behind chime, wrong-way and pause cues are unit tested and compile, but loudness and feel need one lap in Chrome with headphones. The wall-bump thresholds (4 m/s in one frame, 60 m/s2) are first guesses.
- **New HUD parts not yet seen rendered**: the ghost chip under the medal chip, the P key in the countdown key bar (left out in narrow windows), the longer PAUSED pill text. Check at 1280x720 and in a 4:3 window.
- **Medal times are placeholders** (12.0 / 11.0 / 10.2 / 9.6 s on the placeholder track). Re-tune them in `RaceSettings.asset` once the campus track lands; the personal-best key is per track scene, so the new track starts fresh anyway.
- **Settings, credits, pause (P7) not yet seen in Unity**: compiled and unit tested outside Unity, but the card layout (row widths, credit page height, the results row with Retry + Enter + Settings) and the reduced-motion overrides were never rendered. Check at 1280x720 and in a small window.
- **Pause on the Web**: Unity stops the game loop when the tab loses focus (Run In Background is off), so the PAUSED card appears when the player comes back; the click that gives the canvas focus may or may not count as the "continue" click, a second click always does.
- **Coach tips are not yet playtested** with a first-time player (5 s trigger, wording). Medal colours, like coral, still need Luc's OK in `docs/art-style.md`.
- **Ghost (stretch item) not yet seen in Unity**: the code compiles and has tests, but the look (alpha 0.4, fade 1.5 to 8 m, colours), the transparent material in the Web build and the copy of Role 3's real kart meshes still need a Play Mode and a Web build check. The Dean's ghost must be recorded on the final track (after T6/D6) and again after every track change, otherwise the game ignores it (no error, simply no ghost). If the real kart's `Visual` child has effect meshes that are switched on at spawn, they are copied into the ghost too.
- **By design, worth remembering**: the builder rewrites its own root objects in `Main.unity` on every run (values set by hand on them are lost; added components or children make it stop and ask); other root objects, the Volume profile and `RaceSettings` values are kept.
