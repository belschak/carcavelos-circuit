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

        private readonly RaceTimer timer = new RaceTimer();
        private RaceStateMachine stateMachine;
        private PlaceholderLapRules rules;   // swap to RaceRules (Role 2) once T5 is merged
        private PlaceholderTrack track;      // swap to TrackInfo (Role 2) once T4 is merged
        private PersonalBest personalBest;
        private IKartState kart;
        private Transform kartTransform;
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

            int checkpointCount = track.Checkpoints.Length;
            rules = new PlaceholderLapRules(checkpointCount);

            // The best time is saved per track in PlayerPrefs (in the browser Unity keeps them in the site's IndexedDB storage).
            saveKey = "CarcavelosCircuit.Best." + trackName;
            personalBest = PersonalBest.FromSaveString(PlayerPrefs.GetString(saveKey, ""), checkpointCount);

            stateMachine = new RaceStateMachine(settings.CountdownFrom);
            stateMachine.StateChanged += OnStateChanged;
            stateMachine.CountdownStep += OnCountdownStep;

            // Every checkpoint trigger reports to us (see CheckpointRelay).
            for (int i = 0; i < checkpointCount; i++)
            {
                CheckpointRelay relay = track.Checkpoints[i].gameObject.AddComponent<CheckpointRelay>();
                relay.SetUp(this, track.Checkpoints[i].Index);
            }

            PutKartOnStartGrid();
            kart.InputEnabled = false;
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
            stateMachine.Tick(Time.deltaTime);

            if ((State == RaceState.Racing || State == RaceState.Results) && RaceKeys.RetryPressed())
            {
                Retry();
                return;
            }

            if (State == RaceState.Racing)
            {
                bool fellOff = kartTransform.position.y < settings.FallRecoverHeight;
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
        public void Recover()
        {
            Transform respawn = track.GetRespawnPoint(rules.LastValidCheckpoint);
            kart.ResetTo(respawn.position, respawn.rotation);
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
                HasSplitDelta = personalBest.RecordSplit(index, timer.ElapsedSeconds, out delta);
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
            timer.Stop();
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
                timer.Start();
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
