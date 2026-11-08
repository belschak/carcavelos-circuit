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
        public IKartState Kart { get { return kart; } }
        public bool IsSetUp { get { return kart != null; } }

        // ---- Events (sounds and effects listen, the HUD mostly reads the values above) ----
        public event Action<int> CountdownStep;          // 3, 2, 1, 0 = GO
        public event Action<int> CheckpointPassed;       // index of the checkpoint that counted
        public event Action<float, bool> LapFinished;    // lap time, new record?
        public event Action Restarted;
        public event Action Recovered;                   // kart put back after R or a fall (respawn sound)

        private readonly RaceTimer timer = new RaceTimer();
        private RaceStateMachine stateMachine;
        private PlaceholderLapRules rules;   // swap to RaceRules (Role 2) once T5 is merged
        private PlaceholderTrack track;      // swap to TrackInfo (Role 2) once T4 is merged
        private PersonalBest personalBest;
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
            int checkpointCount = checkpoints.Length;
            CheckCheckpoints(checkpoints); // only logs errors, the race still starts so the kart can be tested
            rules = new PlaceholderLapRules(checkpointCount);
            fallCheck = new FallCheck(settings.FallRecoverHeight, settings.FallCheckCooldownSeconds);

            // The best time is saved per track in PlayerPrefs (in the browser Unity keeps them in the site's IndexedDB storage).
            saveKey = "CarcavelosCircuit.Best." + trackName;
            personalBest = PersonalBest.FromSaveString(PlayerPrefs.GetString(saveKey, ""), checkpointCount);

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

        private void OnDestroy()
        {
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

            // Every frame, not only when the state changes: if the kart switched its own input on (for example in
            // its Start), this switches it off again before it can drive in Ready, Countdown or Results.
            kart.InputEnabled = stateMachine.InputAllowed;

            // Ready screen: wait for the first key or click (browsers only allow sound after that).
            if (State == RaceState.Ready)
            {
                if (RaceKeys.AnyKeyOrClickPressed())
                {
                    stateMachine.BeginCountdown(settings.CountdownStepSeconds);
                }
                return;
            }

            // Timer first, then the state machine, so the GO frame does not already count as race time.
            timer.Tick(Time.deltaTime);
            stateMachine.Tick(Time.deltaTime); // on the GO frame this switches the input on (OnStateChanged)

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

        // Full reset: kart back on the grid, rules, timer and effects cleared, quick countdown again.
        // Public so the HUD's Retry button can call it.
        public void Retry()
        {
            if (!IsSetUp || State == RaceState.Ready)
            {
                return;
            }
            PutKartOnStartGrid(); // ResetTo also clears drift and boost (temporary effects)
            rules.Reset();
            timer.Reset();
            LastLapWasRecord = false;
            HasSplitDelta = false;
            HintText = "";
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
                    ShowHint("Missed a checkpoint! Press R to go back");
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
            if (LastLapWasRecord)
            {
                PlayerPrefs.SetString(saveKey, personalBest.ToSaveString());
                PlayerPrefs.Save(); // in a Web build this writes to the browser right away
            }
            if (LapFinished != null) LapFinished(lapTime, LastLapWasRecord);
        }

        // The kart may only drive while racing (rule from docs/interfaces.md).
        private void OnStateChanged(RaceState newState)
        {
            kart.InputEnabled = stateMachine.InputAllowed;
            if (newState == RaceState.Racing)
            {
                rules.StartLap();
                personalBest.StartRun();
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

        private void ShowHint(string text)
        {
            HintText = text;
            HintVisibleUntil = Time.time + settings.HintSeconds;
        }
    }
}
