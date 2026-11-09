using System;
using CarcavelosCircuit.Shared;
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
        public string TrackName { get; private set; } = ""; // the track scene we race on (save keys, ghost check)
        public Vector3[] TrackPoints { get; private set; } = new Vector3[0]; // checkpoint positions in driving order (fly-over camera)
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

        // ---- Events (sounds and effects listen, the HUD mostly reads the values above) ----
        public event Action<int> CountdownStep;          // 3, 2, 1, 0 = GO
        public event Action<int> CheckpointPassed;       // index of the checkpoint that counted
        public event Action<float, bool> LapFinished;    // lap time, new record?
        public event Action Restarted;
        public event Action Recovered;                   // kart put back after R or a fall (respawn sound)

        private readonly RaceTimer timer = new RaceTimer();
        private readonly RunStats runStats = new RunStats();
        private readonly RacePause pause = new RacePause();
        private float timeScaleBeforePause = 1f;
        private bool frozen; // true while this script holds Time.timeScale at 0
        private float slowMotionStart = -1f; // Time.unscaledTime when the finish slow motion began, -1 = none (task P6b)
        private float[] medalThresholds;       // from RaceSettings: Bronze, Silver, Gold, Dean's List (seconds)
        private OnboardingCoach coach;
        private int savedCoachFlags;
        private CoachHint waitingCoachHint;    // a tip that waits until the hint chip is free
        private const string CoachSaveKey = "CarcavelosCircuit.CoachDone"; // one key for all tracks: it is about the player
        private RaceStateMachine stateMachine;
        private PlaceholderLapRules rules;   // swap to RaceRules (Role 2) once T5 is merged
        private PlaceholderTrack track;      // swap to TrackInfo (Role 2) once T4 is merged
        private PersonalBest personalBest;
        private PlaceholderCheckpoint[] checkpointList; // the track's checkpoints (index 0 = start/finish), read by the HUD
        private FallCheck fallCheck;
        private IKartState kart;
        private Transform kartTransform;
        private Rigidbody[] kartBodies;    // the kart's Rigidbodies at spawn time (see IsKartCollider)
        private Collider[] kartColliders;  // the kart's colliders at spawn time
        private string saveKey;

        // Called once by RaceBootstrap after the track is loaded and the kart is spawned.
        public void SetUp(GameObject kartObject, PlaceholderTrack loadedTrack, string trackName)
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

            PlaceholderCheckpoint[] checkpoints = track.Checkpoints;
            if (checkpoints == null)
            {
                checkpoints = new PlaceholderCheckpoint[0];
            }
            checkpointList = checkpoints;
            int checkpointCount = checkpoints.Length;
            CheckCheckpoints(checkpoints); // only logs errors, the race still starts so the kart can be tested
            rules = new PlaceholderLapRules(checkpointCount);
            fallCheck = new FallCheck(settings.FallRecoverHeight, settings.FallCheckCooldownSeconds);

            // The best time is saved per track in PlayerPrefs (in the browser Unity keeps them in the site's IndexedDB storage).
            TrackName = trackName;
            saveKey = "CarcavelosCircuit.Best." + trackName;
            personalBest = PersonalBest.FromSaveString(PlayerPrefs.GetString(saveKey, ""), checkpointCount);

            // Medal times, stretched to this track's length (MedalScaling). A save from before medals existed gets the
            // medal its best time is worth.
            TrackPoints = CheckpointPositions(checkpoints);
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
        private static Vector3[] CheckpointPositions(PlaceholderCheckpoint[] checkpoints)
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

        // Unity sends OnTriggerEnter to the GameObject that owns the trigger collider. Role 2 may put the collider
        // on the checkpoint object itself or on a child, so a relay goes onto every object with a trigger collider.
        private void AddRelays(PlaceholderCheckpoint checkpoint)
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
        private void CheckCheckpoints(PlaceholderCheckpoint[] checkpoints)
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

        private static bool HasTriggerCollider(PlaceholderCheckpoint checkpoint)
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
            return other.GetComponentInParent<IKartState>() != null;
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

            // Paused (focus was lost): everything is frozen until a key or click. That key does nothing else,
            // so it never also retries or recovers.
            if (pause.IsPaused)
            {
                kart.InputEnabled = false;
                if (!AudioListener.pause)
                {
                    AudioListener.pause = true; // RaceMusic unpauses the sound when focus comes back; we are still paused
                }
                if (pause.TryResume(RaceKeys.AnyKeyOrClickPressed()))
                {
                    EndPause();
                }
                return;
            }

            // Every frame, not only when the state changes: if the kart switched its own input on (for example in
            // its Start), this switches it off again before it can drive in Ready, Countdown or Results.
            kart.InputEnabled = stateMachine.InputAllowed;

            // Ready screen: wait for the first key or click (browsers only allow sound after that).
            // Not while the settings or credits use the keys and the mouse.
            if (State == RaceState.Ready)
            {
                if (!MenuUsesInput && RaceKeys.AnyKeyOrClickPressed())
                {
                    BeginRace();
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

            if (State == RaceState.Racing)
            {
                bool fellOff = fallCheck.HasFallen(kartTransform.position.y, Time.time);
                if (RaceKeys.RecoverPressed() || fellOff)
                {
                    Recover();
                }
            }
        }

        // Leaves the start screen: the first countdown begins. Called by the first key or click, and by the
        // automated race test (Tests/PlayMode). Does nothing outside the start screen.
        public void BeginRace()
        {
            if (IsSetUp && State == RaceState.Ready && !pause.IsPaused)
            {
                stateMachine.BeginCountdown(settings.CountdownStepSeconds);
            }
        }

        // Full reset: kart back on the grid, rules, timer and effects cleared, quick countdown again.
        // Public so the HUD's Retry button can call it.
        public void Retry()
        {
            if (!IsSetUp || State == RaceState.Ready)
            {
                return;
            }
            EndPause(); // only a safety net: a paused race cannot normally be retried
            StopSlowMotion(); // Enter during the finish slow motion: the new attempt runs at full speed at once
            PutKartOnStartGrid(); // ResetTo also clears drift and boost (temporary effects)
            rules.Reset();
            timer.Reset();
            LastLapWasRecord = false;
            HasSplitDelta = false;
            HintText = "";
            HintVisibleUntil = 0f;
            waitingCoachHint = CoachHint.None; // a tip queued in the old attempt must not pop up right after GO
            runStats.Reset();
            stateMachine.BeginCountdown(settings.RetryCountdownStepSeconds);
            if (Restarted != null) Restarted();
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
                    ShowHint("Missed a checkpoint! Press R to go back", HintKind.Warning, settings.HintSeconds);
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
            LastLapWasRecord = personalBest.FinishRun(lapTime);
            PreviousBestMedal = personalBest.BestMedal;
            LastMedal = MedalRules.For(lapTime, medalThresholds);
            bool medalImproved = personalBest.NoteMedal(LastMedal); // can differ from a record only after a re-tune
            if (LastLapWasRecord || medalImproved)
            {
                PlayerPrefs.SetString(saveKey, personalBest.ToSaveString());
                PlayerPrefs.Save(); // in a Web build this writes to the browser right away
            }
            // The finish slow motion (task P6b) starts in the same physics step as the official time, which is
            // already taken, so slowing the game can never change the lap time.
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
            kart.InputEnabled = stateMachine.InputAllowed;
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

        // ---- Pause on focus loss (task P7) ----
        // Unity calls these on every MonoBehaviour: OnApplicationFocus(false) when the browser tab or the window
        // loses focus, OnApplicationPause(true) when the app is sent to the background.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StartPause();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                StartPause();
            }
        }

        // Time.timeScale = 0 freezes physics, Time.deltaTime and Time.timeAsDouble. The lap time comes from
        // Time.timeAsDouble timestamps, so the paused seconds never count. RacePause decides if we may pause.
        private void StartPause()
        {
            if (!pauseOnFocusLoss || !IsSetUp || !pause.OnFocusLost(State))
            {
                return;
            }
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            frozen = true;
            kart.InputEnabled = false; // ends a drift without a boost (the kart's own rule), so resuming never surges
            AudioListener.pause = true;
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
                ShowHint(OnboardingCoach.TextFor(waitingCoachHint), OnboardingCoach.KindFor(waitingCoachHint), settings.CoachHintSeconds);
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
