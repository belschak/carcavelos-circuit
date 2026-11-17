using System;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Track;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The race "brain" in Main.unity: runs Ready -> Countdown -> Racing -> Results,
    // decides when the kart may drive, handles retry (Enter) and recovery (R), and keeps the best time.
    // It only talks to the kart through IKartState and to the track through its contract.
    public class RaceManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Countdown length, retry speed, split popup time (RaceFlow/Settings/RaceSettings.asset)")]
        private RaceSettings settings;

        [SerializeField, Tooltip("Pause the countdown and the race when the browser tab or the game window loses focus (task P7)")]
        private bool pauseOnFocusLoss = true;

        // ---- What the HUD and the sounds read ----
        public RaceState State { get { return stateMachine == null ? RaceState.Ready : stateMachine.State; } }
        public int CountdownNumber { get { return stateMachine == null ? 0 : stateMachine.CountdownNumber; } }
        public float LapSeconds { get { return timer.ElapsedSeconds; } }
        public float BestSeconds { get { return personalBest == null ? -1f : personalBest.BestSeconds; } }
        public float PreviousBestSeconds { get; private set; } = -1f; // best time before the lap that just ended
        public bool LastLapWasRecord { get; private set; }
        public bool HasSplitDelta { get; private set; }
        public float LastSplitDelta { get; private set; }
        public float SplitVisibleUntil { get; private set; }
        public string HintText { get; private set; } = "";
        public float HintVisibleUntil { get; private set; }
        public HintKind CurrentHintKind { get; private set; } // warning, coach tip or praise (chip colour)
        public IKartState Kart { get { return kart; } }
        public bool IsSetUp { get { return kart != null; } }
        public int CountdownFrom { get { return settings.CountdownFrom; } }
        public Transform KartTransform { get { return kartTransform; } }
        public TrackInfo Track { get { return track; } } // the raced track (RaceRoster: grid slots behind its SpawnPoint, task R8)
        public string TrackName { get; private set; } = ""; // the track scene we race on (save keys, ghost check)
        public int LayoutStamp { get; private set; } = TrackLayout.Unknown; // the road we race on (TrackLayout): best time + ghost check
        public Vector3[] TrackPoints { get; private set; } = new Vector3[0]; // checkpoint positions in driving order (fly-over camera)
        public Vector3[] TrackForwards { get; private set; } = new Vector3[0]; // road direction at each checkpoint (its respawn point), zero if unknown
        public float LapMetres { get; private set; } // lap length checkpoint to checkpoint (medal times scale with it)
        public int CheckpointCount { get { return checkpointList == null ? 0 : checkpointList.Length; } } // incl. the start/finish line
        public int CheckpointsPassed { get { return rules == null ? 0 : rules.LastValidCheckpoint; } } // this lap, in order
        public int LapCount { get { return 1; } } // a time trial is one lap; the HUD shows a lap counter only if this is > 1

        // ---- Medals and the lap report (task P5) ----
        public Medal LastMedal { get; private set; }          // medal of the lap that just ended
        public Medal PreviousBestMedal { get; private set; }  // best medal before that lap (to spot a new medal)
        public Medal BestMedal { get { return personalBest == null ? Medal.None : personalBest.BestMedal; } }
        public Medal NextMedalTarget { get { return MedalRules.NextTarget(BestMedal); } } // None = top tier reached
        public RunStats Stats { get { return runStats; } }    // stats of the current (or just finished) lap, read only

        // ---- Pause and menus (task P7) ----
        public bool IsPaused { get { return pause.IsPaused; } } // the HUD shows "PAUSED" while this is true
        // Set by the HUD while a menu (settings, credits) is open or the mouse points at a menu button.
        // That key or click belongs to the menu, so it must not also start the race.
        public bool MenuUsesInput { get; set; }
        // Set by the HUD from the settings: with reduced motion the finish has no slow motion (task P6b).
        public bool ReducedMotion { get; set; }

        // ---- Race modes and the finish cinematic (Luc, 16 Nov 2026) ----
        public RaceMode Mode { get; private set; } = RaceModeRules.Default; // picked on the start screen, saved
        // Set by Role 3's BotSquad while its autopilot drives the player's kart after the line (the finish
        // cinematic): then the kart keeps its input in Results. Cleared on Retry and on the way back to the menu.
        public bool PlayerOnAutopilot { get; set; }
        // Written every frame by RaceRoster; after the finish they hold the place the player finished in.
        // Online the room's place wins while one is set (SetExternalPlace, ShownPlace).
        public int PlayerPlace { get { return shownPlace.Place; } set { shownPlace.RosterPlace = value; } }
        public int RacersInRace { get { return shownPlace.Racers; } set { shownPlace.RosterRacers = value; } }
        // Time.unscaledTime when the kart crossed the line, -1 = not finished (camera shot and HUD cinematic).
        public float FinishRealTime { get; private set; } = -1f;

        // ---- Online race (M2): the Online assembly drives the race through these (see OnlineRaceHooks) ----
        // True while the online lobby owns the race: no key or click starts, retries, leaves or pauses it
        // (ExternalStartRules). The lobby starts it with BeginRaceExternal and resets it with ResetToReadyExternal.
        public bool ExternalStartOnly { get; set; }
        // The Inspector switch "pause on focus loss". Online the lobby switches it off: the race goes on on
        // everyone else's screen, so a hidden tab must not freeze this player's clock. LeaveOnlineMode puts it back.
        public bool PauseOnFocusLoss { get { return pauseOnFocusLoss; } set { pauseOnFocusLoss = value; } }

        // ---- Events (sounds and effects listen, the HUD mostly reads the values above) ----
        public event Action<int> CountdownStep;          // 3, 2, 1, 0 = GO
        public event Action<int> CheckpointPassed;       // index of the checkpoint that counted
        public event Action<float, bool> LapFinished;    // lap time, new record?
        public event Action Restarted;                   // a new attempt: Retry, or Results -> start screen (BackToMenu)
        public event Action Recovered;                   // kart put back after R or a fall (respawn sound)
        public event Action Paused;                      // the pause started (focus lost or P), audit 19 pause cue
        public event Action Resumed;                     // the pause ended and the race runs again
        public event Action<RaceMode> ModeChanged;       // a new mode was picked on the start screen
        public event Action StartRequested;              // any key on the start screen, see RequestStart

        private readonly RaceTimer timer = new RaceTimer();
        private readonly RunStats runStats = new RunStats();
        private readonly RacePause pause = new RacePause();
        private readonly ShownPlace shownPlace = new ShownPlace();
        private bool pauseOnFocusLossFromScene = true; // the Inspector value, put back by LeaveOnlineMode
        private int leftOnlineFrame = -1; // Time.frameCount of the last LeaveOnlineMode, -1 = never (its key must not start a race)
        private float timeScaleBeforePause = 1f;
        private bool frozen; // true while this script holds Time.timeScale at 0
        private float slowMotionStart = -1f; // Time.unscaledTime when the finish slow motion began, -1 = none (task P6b)
        private float[] medalThresholds;       // from RaceSettings: Bronze, Silver, Gold, Dean's List (seconds)
        private OnboardingCoach coach;
        private int savedCoachFlags;
        private CoachHint waitingCoachHint;    // a tip that waits until the hint chip is free
        private const string CoachSaveKey = "CarcavelosCircuit.CoachDone"; // one key for all tracks: it is about the player
        private const string ModeSaveKey = "CarcavelosCircuit.RaceMode";    // the last mode played, for the next visit
        private RaceStateMachine stateMachine;
        private RaceRules rules;   // Role 2's lap rules (T5)
        private TrackInfo track;   // Role 2's track contract (T4) on the track scene's root
        private PersonalBest personalBest;
        private Checkpoint[] checkpointList; // the track's checkpoints (index 0 = start/finish), read by the HUD
        private FallCheck fallCheck;
        private IKartState kart;
        private Transform kartTransform;
        private Rigidbody[] kartBodies;    // the kart's Rigidbodies at spawn time (see IsKartCollider)
        private Collider[] kartColliders;  // the kart's colliders at spawn time
        private string saveKey;

        private void Awake()
        {
            pauseOnFocusLossFromScene = pauseOnFocusLoss;
        }

        // Called once by RaceBootstrap after the track is loaded and the kart is spawned.
        public void SetUp(GameObject kartObject, TrackInfo loadedTrack, string trackName)
        {
            kart = kartObject.GetComponent<IKartState>();
            if (kart == null)
            {
                Debug.LogError("RaceManager: the kart prefab has no component that implements IKartState.");
                return;
            }
            kartTransform = kartObject.transform;
            track = loadedTrack;

            // Remember the kart's physics parts now, while they are all still under the kart. A sphere controller
            // may unparent its physics sphere in its own Start, which runs after this.
            kartBodies = kartObject.GetComponentsInChildren<Rigidbody>(true);
            kartColliders = kartObject.GetComponentsInChildren<Collider>(true);

            Checkpoint[] checkpoints = track.Checkpoints;
            if (checkpoints == null)
            {
                checkpoints = new Checkpoint[0];
            }
            checkpointList = checkpoints;
            int checkpointCount = checkpoints.Length;
            CheckCheckpoints(checkpoints); // only logs errors, the race still starts so the kart can be tested
            rules = new RaceRules(checkpointCount);
            fallCheck = new FallCheck(settings.FallRecoverHeight, settings.FallCheckCooldownSeconds);

            // The best time is saved per track in PlayerPrefs (in the browser Unity keeps them in the site's IndexedDB storage).
            TrackName = trackName;
            // A best time from another road (same name, same checkpoints, but the road between them moved) is dropped.
            saveKey = "CarcavelosCircuit.Best." + trackName;
            LayoutStamp = TrackLayout.StampOf(TrackProgressSource.OwnOf(track));
            personalBest = PersonalBest.FromSaveString(PlayerPrefs.GetString(saveKey, ""), checkpointCount, LayoutStamp);

            // Medal times, stretched to this track's length (MedalScaling). A save from before medals existed gets the
            // medal its best time is worth.
            TrackPoints = CheckpointPositions(checkpoints);
            TrackForwards = CheckpointForwards(checkpoints);
            LapMetres = MedalScaling.LoopLength(TrackPoints);
            medalThresholds = MedalScaling.ScaleTimes(settings.MedalThresholds(), settings.MedalReferenceLapMetres, LapMetres);
            Debug.Log("RaceManager: racing " + trackName + ", " + checkpointCount + " checkpoints, lap about "
                + Mathf.RoundToInt(LapMetres) + " m checkpoint to checkpoint, Bronze " + RaceTimer.Format(medalThresholds[0])
                + ", Dean's List " + RaceTimer.Format(medalThresholds[MedalRules.TierCount - 1]) + ".");
            if (!MedalRules.AreValid(medalThresholds))
            {
                Debug.LogError("RaceManager: the medal times in RaceSettings must be positive and each faster than the one before (Bronze > Silver > Gold > Dean's List). No medals this session.");
            }
            if (personalBest.HasRecord)
            {
                personalBest.NoteMedal(MedalRules.For(personalBest.BestSeconds, medalThresholds));
            }

            // Coach tips already shown in an earlier session are not shown again.
            savedCoachFlags = PlayerPrefs.GetInt(CoachSaveKey, 0);
            coach = new OnboardingCoach(savedCoachFlags, settings.CoachNoDriftSeconds);

            // The last mode played. A save we cannot read (or Online, which is never remembered) starts as Race vs Bots.
            Mode = SavedOfflineMode();

            stateMachine = new RaceStateMachine(settings.CountdownFrom);
            stateMachine.StateChanged += OnStateChanged;
            stateMachine.CountdownStep += OnCountdownStep;

            // Every checkpoint trigger reports to us (see CheckpointRelay).
            for (int i = 0; i < checkpointCount; i++)
            {
                if (checkpoints[i] != null)
                {
                    AddRelays(checkpoints[i]);
                }
            }

            PutKartOnStartGrid();
            kart.InputEnabled = false;
        }

        // Where the checkpoints stand in driving order (index 0, 1, 2 ...), for the lap length. The list on the track
        // may be in any order, so we look each index up. A missing index is skipped.
        private static Vector3[] CheckpointPositions(Checkpoint[] checkpoints)
        {
            var positions = new System.Collections.Generic.List<Vector3>();
            for (int index = 0; index < checkpoints.Length; index++)
            {
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    if (checkpoints[i] != null && checkpoints[i].Index == index)
                    {
                        positions.Add(checkpoints[i].transform.position);
                        break;
                    }
                }
            }
            return positions.ToArray();
        }

        // The road direction at every checkpoint, same order as CheckpointPositions: the forward of its respawn point,
        // which Role 2 places on the road facing the way you drive. Zero when a checkpoint has no respawn point
        // (ItemBoxLayout then falls back to the straight line to the next checkpoint).
        private static Vector3[] CheckpointForwards(Checkpoint[] checkpoints)
        {
            var forwards = new System.Collections.Generic.List<Vector3>();
            for (int index = 0; index < checkpoints.Length; index++)
            {
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    if (checkpoints[i] != null && checkpoints[i].Index == index)
                    {
                        Transform respawn = checkpoints[i].RespawnPoint;
                        forwards.Add(respawn != null ? respawn.forward : Vector3.zero);
                        break;
                    }
                }
            }
            return forwards.ToArray();
        }

        // Unity sends OnTriggerEnter to the GameObject that owns the trigger collider. Role 2 may put the collider
        // on the checkpoint object itself or on a child, so a relay goes onto every object with a trigger collider.
        private void AddRelays(Checkpoint checkpoint)
        {
            Collider[] colliders = checkpoint.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                GameObject owner = colliders[i].gameObject;
                if (colliders[i].isTrigger && owner.GetComponent<CheckpointRelay>() == null)
                {
                    CheckpointRelay relay = owner.AddComponent<CheckpointRelay>();
                    relay.SetUp(this, checkpoint.Index);
                }
            }
        }

        // Swap-day safety net: a wrong track setup gives a clear red error instead of a lap that never finishes.
        // Rules (docs/interfaces.md): no empty entries, a trigger collider on every checkpoint, indices exactly 0..N-1.
        private void CheckCheckpoints(Checkpoint[] checkpoints)
        {
            int[] indices = new int[checkpoints.Length];
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] == null)
                {
                    Debug.LogError("RaceManager: entry " + i + " in the track's checkpoint list is empty. The lap can never finish.");
                    return;
                }
                if (!HasTriggerCollider(checkpoints[i]))
                {
                    Debug.LogError("RaceManager: checkpoint " + checkpoints[i].name + " has no collider with Is Trigger on itself or a child, so it can never count.");
                }
                indices[i] = checkpoints[i].Index;
            }
            string problem = CheckpointValidation.FindProblem(indices);
            if (problem != "")
            {
                Debug.LogError("RaceManager: " + problem + ". The lap can never finish.");
            }
        }

        private static bool HasTriggerCollider(Checkpoint checkpoint)
        {
            Collider[] colliders = checkpoint.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].isTrigger)
                {
                    return true;
                }
            }
            return false;
        }

        // For the HUD's wrong-way warning: where is the checkpoint the kart must drive through next?
        // After the last checkpoint that is the start/finish line (index 0). False before set-up or if it is missing.
        public bool TryGetNextCheckpointPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (checkpointList == null || checkpointList.Length == 0)
            {
                return false;
            }
            int next = CheckpointsPassed + 1;
            if (next >= checkpointList.Length)
            {
                next = 0;
            }
            for (int i = 0; i < checkpointList.Length; i++)
            {
                if (checkpointList[i] != null && checkpointList[i].Index == next)
                {
                    position = checkpointList[i].transform.position;
                    return true;
                }
            }
            return false;
        }

        // Called by CheckpointRelay: is this collider part of our kart? Two accepted ways (docs/interfaces.md):
        // 1. it is one of the kart's colliders, or it moves with one of the kart's Rigidbodies, as remembered at
        //    spawn (this also works for a physics sphere that is no longer a child of the kart), or
        // 2. it has the IKartState component on itself or on a parent.
        public bool IsKartCollider(Collider other)
        {
            if (!IsSetUp)
            {
                return false;
            }
            for (int i = 0; i < kartColliders.Length; i++)
            {
                if (kartColliders[i] == other)
                {
                    return true;
                }
            }
            Rigidbody body = other.attachedRigidbody;
            if (body != null)
            {
                for (int i = 0; i < kartBodies.Length; i++)
                {
                    if (kartBodies[i] == body)
                    {
                        return true;
                    }
                }
            }
            // Only OUR kart (task R8): a bot's kart also has an IKartState, and it must never pass the player's
            // checkpoints or take the player's item boxes.
            IKartState owner = other.GetComponentInParent<IKartState>();
            return owner != null && owner == kart;
        }

        // Time.timeScale is global and survives a scene load, so a slow motion that is cut off (scene unloaded,
        // object switched off) must hand back full speed, or the next scene would run at 30 %.
        private void OnDisable()
        {
            StopSlowMotion();
        }

        private void OnDestroy()
        {
            EndPause(); // never leave the game frozen (Time.timeScale is global)
            StopSlowMotion();
            if (stateMachine != null)
            {
                stateMachine.StateChanged -= OnStateChanged;
                stateMachine.CountdownStep -= OnCountdownStep;
            }
        }

        private void Update()
        {
            if (!IsSetUp)
            {
                return;
            }

            // Before the pause check: the slow motion must notice a pause (it then leaves Time.timeScale at 0).
            UpdateSlowMotion();

            // Paused (focus was lost or P was pressed): everything is frozen until a key, a click or P again.
            // That key does nothing else, so it never also retries or recovers.
            if (pause.IsPaused)
            {
                kart.InputEnabled = false;
                if (!AudioListener.pause)
                {
                    AudioListener.pause = true; // RaceMusic unpauses the sound when focus comes back; we are still paused
                }
                if (pause.TryResume(RaceKeys.AnyKeyOrClickPressed() || RaceKeys.PausePressed()))
                {
                    EndPause();
                }
                return;
            }

            // P pauses the countdown or the race by hand (audit 9): the same pause as a lost focus.
            // Not while the online lobby owns the race (the shared clock goes on without us).
            if (ExternalStartRules.MayPauseByHand(ExternalStartOnly) && RaceKeys.PausePressed() && StartPause(pause.OnPauseKey(State)))
            {
                return;
            }

            // Every frame, not only when the state changes: if the kart switched its own input on (for example in
            // its Start), this switches it off again before it can drive in Ready, Countdown or Results.
            kart.InputEnabled = InputAllowedNow();

            // Ready screen: wait for the first key or click (browsers only allow sound after that).
            // Not while the settings or credits use the keys and the mouse, and never online (the host starts).
            if (State == RaceState.Ready)
            {
                bool leftOnlineNow = ExternalStartRules.IsLeavingFrame(Time.frameCount, leftOnlineFrame); // Q or BACK just left the room
                if (ExternalStartRules.MayStartFromKeys(ExternalStartOnly, Mode, MenuUsesInput || leftOnlineNow) && RaceKeys.AnyKeyOrClickPressed())
                {
                    RequestStart();
                }
                return;
            }

            // Timer first, then the state machine, so the GO frame does not already count as race time.
            timer.Tick(Time.deltaTime);
            stateMachine.Tick(Time.deltaTime); // on the GO frame this switches the input on (OnStateChanged)

            if (State == RaceState.Racing)
            {
                runStats.Tick(Time.deltaTime, kart.Speed, kart.IsDrifting, kart.IsBoosting, kart.IsGrounded);
            }
            UpdateCoach();

            if ((State == RaceState.Racing || State == RaceState.Results) && RaceKeys.RetryPressed())
            {
                Retry();
                return;
            }

            if (State == RaceState.Results && !MenuUsesInput && RaceKeys.BackToMenuPressed())
            {
                BackToMenu();
                return;
            }

            if (State == RaceState.Racing)
            {
                bool fellOff = fallCheck.HasFallen(kartTransform.position.y, Time.time);
                if (RaceKeys.RecoverPressed() || fellOff)
                {
                    Recover();
                }
            }
        }

        // "Any key" on the start screen. With a driver select listening (the HUD's DriverPicker, Luc 17 Nov 2026)
        // the key opens it and READY then calls BeginRace; without one (Play Mode tests, a scene without the HUD)
        // the race starts at once as before.
        public void RequestStart()
        {
            if (StartRequested != null)
            {
                StartRequested();
            }
            else
            {
                BeginRace();
            }
        }

        // Leaves the start screen: the first countdown begins. Called by the first key or click, and by the
        // automated race test (Tests/PlayMode). Does nothing outside the start screen, and nothing online
        // (there only the lobby starts the race, with BeginRaceExternal).
        public void BeginRace()
        {
            if (IsSetUp && State == RaceState.Ready && !pause.IsPaused && RaceModeRules.IsPlayable(Mode)
                && ExternalStartRules.MayStartFromKeys(ExternalStartOnly, Mode, false))
            {
                stateMachine.BeginCountdown(settings.CountdownStepSeconds);
            }
        }

        // ---- Online race (M2): called by the Online assembly's lobby ----

        // Starts the countdown from the start screen, whatever the keys and ExternalStartOnly say. The lobby calls
        // it secondsPerStep * CountdownFrom before the shared GO moment, so GO lands at the same time on every
        // screen. A pause is ended first. False (nothing happens) outside Ready or before the race is set up.
        public bool BeginRaceExternal(float secondsPerStep)
        {
            if (!IsSetUp || !ExternalStartRules.MayBeginExternal(State, Mode))
            {
                return false;
            }
            EndPause();
            stateMachine.BeginCountdown(secondsPerStep);
            return true;
        }

        // From any state back onto the grid in Ready (online rematch, or leaving the room mid-race). Like the way
        // back to the menu: kart on its slot, lap, timer and finish shot cleared, the external place cleared.
        // Nothing is saved. Everyone who resets on Retry resets here too (Restarted).
        public void ResetToReadyExternal()
        {
            if (!IsSetUp)
            {
                return;
            }
            EndPause();
            StopSlowMotion();
            ClearRunForNewStart();
            ForgetAttempt();
            ClearExternalPlace();
            stateMachine.ResetToReady();
            kart.InputEnabled = false;
            if (Restarted != null) Restarted();
        }

        // The finish overlay and the results card show this place ("3RD", "OF 7") instead of RaceRoster's live
        // standings, until ClearExternalPlace. The online room knows everyone's reported times.
        public void SetExternalPlace(int place, int racers)
        {
            shownPlace.SetExternal(place, racers);
        }

        public void ClearExternalPlace()
        {
            shownPlace.ClearExternal();
        }

        // Leaves the online mode for the start screen in the last offline mode (Race vs Bots / Time Attack), from
        // any state. Keys, pause on focus loss and the shown place work as offline again. The lobby calls it when
        // the player leaves the room (Back, Q) or the connection fails.
        public void LeaveOnlineMode()
        {
            leftOnlineFrame = Time.frameCount; // the Q or click that left the room does not also start a race this frame
            ExternalStartOnly = false;
            pauseOnFocusLoss = pauseOnFocusLossFromScene;
            ClearExternalPlace();
            if (!IsSetUp)
            {
                return; // SetUp picks the saved offline mode anyway
            }
            if (State != RaceState.Ready)
            {
                ResetToReadyExternal();
            }
            if (Mode == RaceMode.OnlineRace)
            {
                Mode = SavedOfflineMode();
                if (ModeChanged != null) ModeChanged(Mode);
            }
        }

        // The mode saved for the next visit (only offline modes are ever saved, RaceModeRules.StartMode).
        private static RaceMode SavedOfflineMode()
        {
            return RaceModeRules.StartMode(PlayerPrefs.GetInt(ModeSaveKey, (int)RaceModeRules.Default));
        }

        // Full reset: kart back on the grid, rules, timer and effects cleared, quick countdown again.
        // Public so the HUD's Retry button can call it. Does nothing while the online lobby owns the race.
        public void Retry()
        {
            if (!IsSetUp || State == RaceState.Ready || !ExternalStartRules.MayRetryOrLeave(ExternalStartOnly))
            {
                return;
            }
            EndPause(); // only a safety net: a paused race cannot normally be retried
            StopSlowMotion(); // Enter during the finish slow motion: the new attempt runs at full speed at once
            ClearRunForNewStart();
            ForgetAttempt();
            stateMachine.BeginCountdown(settings.RetryCountdownStepSeconds);
            if (Restarted != null) Restarted();
        }

        // The old attempt's hint, split popup, record flag and stats go. Shared by Retry, BackToMenu and
        // ResetToReadyExternal.
        private void ForgetAttempt()
        {
            LastLapWasRecord = false;
            HasSplitDelta = false;
            HintText = "";
            HintVisibleUntil = 0f;
            waitingCoachHint = CoachHint.None; // a tip queued in the old attempt must not pop up right after GO
            runStats.Reset();
        }

        // Kart on the grid, rules and timer cleared, finish cinematic over. Shared by Retry and BackToMenu.
        private void ClearRunForNewStart()
        {
            PlayerOnAutopilot = false;
            FinishRealTime = -1f;
            PutKartOnStartGrid(); // ResetTo also clears drift and boost (temporary effects)
            rules.Reset();
            timer.Reset();
        }

        // Picks the mode for the next race. Only on the start screen, and only a mode RaceModeRules.CanPick allows:
        // Online without the online build gives back its message for the HUD and changes nothing. An offline mode
        // is saved, so the next visit starts in it; Online never is (RaceModeRules.IsRemembered).
        public bool TrySelectMode(RaceMode mode, out string message)
        {
            message = "";
            if (!IsSetUp || State != RaceState.Ready)
            {
                return false;
            }
            if (!RaceModeRules.CanPick(mode, OnlineRaceHooks.Available, out message))
            {
                return false;
            }
            if (mode != Mode)
            {
                Mode = mode;
                if (RaceModeRules.IsRemembered(mode))
                {
                    PlayerPrefs.SetInt(ModeSaveKey, (int)mode);
                    PlayerPrefs.Save();
                }
                if (ModeChanged != null) ModeChanged(mode);
            }
            return true;
        }

        // Results -> start screen, so the player can pick another mode. Public for the HUD's MENU button.
        // Does nothing while the online lobby owns the race (ExternalStartOnly).
        public void BackToMenu()
        {
            if (!IsSetUp || State != RaceState.Results || !ExternalStartRules.MayRetryOrLeave(ExternalStartOnly))
            {
                return;
            }
            EndPause();
            StopSlowMotion();
            ClearRunForNewStart();
            ForgetAttempt();
            stateMachine.BackToReady();
            kart.InputEnabled = false;
            // Everyone who resets on Retry (grid, items, ghost, leaderboard form, camera, finish autopilot) resets
            // here too; the state is Ready, so no countdown starts.
            if (Restarted != null) Restarted();
        }

        // The player's kart drives while racing, and after the line only while the finish autopilot steers it.
        private bool InputAllowedNow()
        {
            return stateMachine.InputAllowed || (State == RaceState.Results && PlayerOnAutopilot);
        }

        // Back to the last checkpoint that counted. The timer keeps running and no checkpoint is awarded.
        // Public for a possible HUD button, so it guards itself: only while racing (before set-up there is no track).
        public void Recover()
        {
            if (!IsSetUp || State != RaceState.Racing)
            {
                return;
            }
            Transform respawn = track.GetRespawnPoint(rules.LastValidCheckpoint);
            kart.ResetTo(respawn.position, respawn.rotation);
            fallCheck.NoteRecovered(Time.time); // pause the fall check, so one fall gives one recover
            runStats.NoteRecovery();
            if (Recovered != null) Recovered();
        }

        // Called by CheckpointRelay when the kart drives through a checkpoint trigger.
        public void OnCheckpointEntered(int index)
        {
            if (State != RaceState.Racing)
            {
                return;
            }

            // Checkpoint 0 is the start/finish line.
            if (index == 0)
            {
                if (rules.TryFinish())
                {
                    FinishLap();
                }
                else if (rules.LastValidCheckpoint > 0)
                {
                    ShowHint(WebPage.TouchMode ? "Missed a checkpoint! Tap RECOVER to go back" : "Missed a checkpoint! Press R to go back",
                        HintKind.Warning, settings.HintSeconds);
                }
                return;
            }

            if (rules.PassCheckpoint(index))
            {
                float delta;
                HasSplitDelta = personalBest.RecordSplit(index, timer.SecondsSinceStart(Time.timeAsDouble), out delta);
                LastSplitDelta = delta;
                SplitVisibleUntil = Time.time + settings.SplitPopupSeconds;
                if (CheckpointPassed != null) CheckpointPassed(index);
            }
        }

        private void FinishLap()
        {
            if (!stateMachine.Finish())
            {
                return;
            }
            // Official time = finish timestamp - start timestamp. We are inside OnTriggerEnter here,
            // so Time.timeAsDouble is the time of the physics step in which the kart crossed the line.
            timer.Stop(Time.timeAsDouble);
            float lapTime = timer.ElapsedSeconds;
            PreviousBestSeconds = personalBest.BestSeconds;
            PreviousBestMedal = personalBest.BestMedal;
            LastMedal = MedalRules.For(lapTime, medalThresholds);
            // Online laps (bumped, held up by others) never replace the time trial record or its medal: the card
            // shows the medal this lap is worth, but nothing is kept (RaceModeRules.KeepsRecords).
            if (RaceModeRules.KeepsRecords(Mode))
            {
                LastLapWasRecord = personalBest.FinishRun(lapTime);
                bool medalImproved = personalBest.NoteMedal(LastMedal); // can differ from a record only after a re-tune
                if (LastLapWasRecord || medalImproved)
                {
                    PlayerPrefs.SetString(saveKey, personalBest.ToSaveString());
                    PlayerPrefs.Save(); // in a Web build this writes to the browser right away
                }
            }
            else
            {
                LastLapWasRecord = false;
            }
            // The finish slow motion (task P6b) starts in the same physics step as the official time, which is
            // already taken, so slowing the game can never change the lap time.
            FinishRealTime = Time.unscaledTime;
            if (!ReducedMotion)
            {
                slowMotionStart = Time.unscaledTime;
            }
            if (LapFinished != null) LapFinished(lapTime, LastLapWasRecord);
        }

        // ---- Finish slow motion (task P6b) ----
        // Time.timeScale follows FinishMoment.TimeScaleAt over real time (Time.unscaledTime): the scene slows to
        // 30 % and comes back to full speed after 1.2 s. The HUD runs on real time, so it never slows down.
        // A pause wins: while frozen this leaves Time.timeScale at 0 and the curve simply goes on in real time.
        private void UpdateSlowMotion()
        {
            if (slowMotionStart < 0f || frozen)
            {
                return;
            }
            float seconds = Time.unscaledTime - slowMotionStart;
            if (FinishMoment.IsSlowMotionRunning(seconds))
            {
                Time.timeScale = FinishMoment.TimeScaleAt(seconds);
            }
            else
            {
                Time.timeScale = 1f;
                slowMotionStart = -1f;
            }
        }

        // Ends the slow motion at once (retry, scene unload). Safe to call when none is running.
        private void StopSlowMotion()
        {
            if (slowMotionStart < 0f)
            {
                return;
            }
            slowMotionStart = -1f;
            if (frozen)
            {
                timeScaleBeforePause = 1f; // a pause that ends later must come back at full speed, not at 30 %
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        // The kart may only drive while racing (rule from docs/interfaces.md).
        private void OnStateChanged(RaceState newState)
        {
            kart.InputEnabled = InputAllowedNow();
            if (newState == RaceState.Racing)
            {
                rules.StartLap();
                personalBest.StartRun();
                runStats.Reset();
                timer.Start(Time.timeAsDouble); // timestamp of the GO frame
            }
        }

        private void OnCountdownStep(int number)
        {
            if (CountdownStep != null) CountdownStep(number);
        }

        private void PutKartOnStartGrid()
        {
            kart.ResetTo(track.SpawnPoint.position, track.SpawnPoint.rotation);
        }

        private void ShowHint(string text, HintKind kind, float seconds)
        {
            HintText = text;
            CurrentHintKind = kind;
            HintVisibleUntil = Time.time + seconds;
        }

        // ---- Pause on focus loss (task P7) and on the P key (audit 9) ----
        // Unity calls these on every MonoBehaviour: OnApplicationFocus(false) when the browser tab or the window
        // loses focus, OnApplicationPause(true) when the app is sent to the background.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                PauseForLostFocus();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                PauseForLostFocus();
            }
        }

        private void PauseForLostFocus()
        {
            if (pauseOnFocusLoss && IsSetUp)
            {
                StartPause(pause.OnFocusLost(State));
            }
        }

        // Time.timeScale = 0 freezes physics, Time.deltaTime and Time.timeAsDouble. The lap time comes from
        // Time.timeAsDouble timestamps, so the paused seconds never count. RacePause decides if we may pause
        // (pauseStarted), this only does the Unity part. Returns pauseStarted, so the caller can stop its frame.
        private bool StartPause(bool pauseStarted)
        {
            if (!pauseStarted)
            {
                return false;
            }
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            frozen = true;
            kart.InputEnabled = false; // ends a drift without a boost (the kart's own rule), so resuming never surges
            AudioListener.pause = true;
            if (Paused != null) Paused();
            return true;
        }

        // Back to normal speed. Safe to call when not paused: then it changes nothing.
        private void EndPause()
        {
            pause.Clear();
            if (!frozen)
            {
                return; // we did not freeze anything, so we leave time and sound alone
            }
            frozen = false;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
            if (Resumed != null) Resumed();
        }

        // The medal time for one tier in seconds (for the HUD), -1 if there is none.
        public float MedalSeconds(Medal medal)
        {
            return MedalRules.ThresholdFor(medal, medalThresholds);
        }

        // Feeds the coach every frame. A tip waits while another hint (e.g. "Missed a checkpoint!") is showing,
        // and only appears while racing. New "already shown" flags are saved at once.
        private void UpdateCoach()
        {
            bool racing = State == RaceState.Racing;
            CoachHint hint = coach.Tick(Time.deltaTime, racing, kart.IsDrifting, kart.DriftStage, kart.IsBoosting);
            if (hint != CoachHint.None)
            {
                waitingCoachHint = hint;
            }
            if (racing && waitingCoachHint != CoachHint.None && Time.time >= HintVisibleUntil)
            {
                ShowHint(OnboardingCoach.TextFor(waitingCoachHint, WebPage.TouchMode), OnboardingCoach.KindFor(waitingCoachHint), settings.CoachHintSeconds);
                waitingCoachHint = CoachHint.None;
            }
            if (coach.DoneFlags != savedCoachFlags)
            {
                savedCoachFlags = coach.DoneFlags;
                PlayerPrefs.SetInt(CoachSaveKey, savedCoachFlags);
                PlayerPrefs.Save();
            }
        }
    }
}
