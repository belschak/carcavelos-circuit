using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R5 (race part) + P4 polish: countdown beeps, checkpoint chime, finish jingle, new-record sting,
    // click on retry, whoosh on respawn, warning cue for a missed checkpoint.
    // P6b: a soft thud when the medal badge lands on the results card, plus a small ping for a new medal.
    // Audit 10: menu sounds (open, close, page turn, slider step, on/off, mute), asked for by the HUD's menus.
    // Audit 19: the checkpoint chime is 2 semitones higher when you are ahead of your best, 2 lower when behind;
    // one quiet warning each time the WRONG WAY chip appears; a short cue when the race pauses and resumes.
    // Results card sounds wait for the finish beat (FinishMoment.BeatFor) and count in REAL time, because the
    // finish slow motion changes Time.timeScale (PlayDelayed and Time.unscaledTime both ignore it).
    // It listens to the RaceManager's events. The sound files live in Audio/Clips; their loudness is set in the
    // files (tools/audio-synth/process_wavs.py), so every sound plays at full volume here.
    // Kart sounds (engine, drift, boost) are in the Audio module (KartAudio), because they only read IKartState.
    public class RaceSounds : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Ducks the music under the important sounds (RaceMusic on the Music object)")]
        private RaceMusic music;

        [SerializeField, Tooltip("A 2D AudioSource (Spatial Blend 0) for interface sounds")]
        private AudioSource source;

        [SerializeField, Tooltip("Second 2D AudioSource for sounds that start a moment later (record sting, retry beep)")]
        private AudioSource delayedSource;

        [SerializeField, Tooltip("The HUD: its menus ask for menu sounds through RaceHud.MenuSoundRequested (audit 10)")]
        private RaceHud hud;

        [SerializeField, Tooltip("2D AudioSource for menu sounds. Awake makes it ignore AudioListener.pause, so M still clicks while the race is paused")]
        private AudioSource menuSource;

        [SerializeField, Tooltip("2D AudioSource only for the checkpoint chime: its pitch says ahead or behind (audit 19)")]
        private AudioSource chimeSource;

        [SerializeField, Tooltip("Short low beep for 3, 2, 1")]
        private AudioClip countdownBeep;

        [SerializeField, Tooltip("Longer, higher beep for GO")]
        private AudioClip goBeep;

        [SerializeField]
        private AudioClip checkpointChime;

        [SerializeField]
        private AudioClip finishJingle;

        [SerializeField]
        private AudioClip newRecordSting;

        [SerializeField, Tooltip("Soft thud when the medal badge lands on the results card (task P6b, sfx_landing_thud)")]
        private AudioClip medalStamp;

        [SerializeField, Tooltip("Small ping right after the thud when the lap earned a better medal than ever (task P6b, sfx_drift_stage2)")]
        private AudioClip newMedalSparkle;

        [SerializeField, Tooltip("Short click when the player restarts (Enter or the Retry button)")]
        private AudioClip retryClick;

        [SerializeField, Tooltip("Airy whoosh when the kart is put back on the track (R or a fall)")]
        private AudioClip respawnWhoosh;

        [SerializeField, Tooltip("Two falling notes when the hint 'Missed a checkpoint' appears (sfx_hint_warning)")]
        private AudioClip hintWarning;

        [SerializeField, Tooltip("Settings or credits card opens: two quick rising notes (ui_open)")]
        private AudioClip menuOpen;

        [SerializeField, Tooltip("A card closes: the same two notes falling (ui_close)")]
        private AudioClip menuClose;

        [SerializeField, Tooltip("Credits turn to the other page: a short paper swish (ui_page)")]
        private AudioClip pageTurn;

        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField, Tooltip("The new-record sting waits this long after the finish jingle, so the two do not stack into one muddy sound and the sting lands with the NEW RECORD stamp on the results card")]
        private float recordStingDelay = 1.25f;

        [SerializeField, Tooltip("On a retry the first beep waits this long, so the retry click is not hidden under it")]
        private float retryBeepDelay = 0.1f;

        [SerializeField, Range(0f, 1f), Tooltip("Coach tips and praise reuse the retry click this much quieter (a gentle 'look here', not an alarm)")]
        private float coachClickVolume = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("The medal thud sits under the music and the jingle: a felt landing, not a bang")]
        private float medalStampVolume = 0.7f;

        [SerializeField, Range(0f, 1f), Tooltip("The new-medal ping, quieter than the record sting")]
        private float newMedalSparkleVolume = 0.55f;

        [SerializeField, Range(0f, 1f), Tooltip("WRONG WAY reuses the missed-checkpoint warning this much quieter (audit 19)")]
        private float wrongWayVolume = 0.6f;

        [SerializeField, Tooltip("Pitch of the pause and resume cues (the menu close and open notes, a bit lower)")]
        private float pauseCuePitch = 0.8f;

        private RaceState stateLastFrame = RaceState.Ready;
        private float lastHintUntil;
        private float stampAt = -1f;    // Time.unscaledTime when the medal thud plays, -1 = none waiting
        private float sparkleAt = -1f;  // same for the new-medal ping

        // ignoreListenerPause is not saved in scene files (it only exists at runtime), so it is set here, not by the
        // builder. With it, the menu source keeps playing while the pause holds AudioListener.pause on.
        private void Awake()
        {
            if (menuSource != null)
            {
                menuSource.ignoreListenerPause = true;
            }
        }

        private void OnEnable()
        {
            if (hud != null) hud.MenuSoundRequested += OnMenuSound;
            if (hud != null) hud.WrongWayShown += OnWrongWayShown;
            raceManager.Paused += OnPaused;
            raceManager.Resumed += OnResumed;
            raceManager.CountdownStep += OnCountdownStep;
            raceManager.CheckpointPassed += OnCheckpointPassed;
            raceManager.LapFinished += OnLapFinished;
            raceManager.Restarted += OnRestarted;
            raceManager.Recovered += OnRecovered;
        }

        private void OnDisable()
        {
            if (hud != null) hud.MenuSoundRequested -= OnMenuSound;
            if (hud != null) hud.WrongWayShown -= OnWrongWayShown;
            raceManager.Paused -= OnPaused;
            raceManager.Resumed -= OnResumed;
            raceManager.CountdownStep -= OnCountdownStep;
            raceManager.CheckpointPassed -= OnCheckpointPassed;
            raceManager.LapFinished -= OnLapFinished;
            raceManager.Restarted -= OnRestarted;
            raceManager.Recovered -= OnRecovered;
        }

        private void Update()
        {
            // A hint has no event, but RaceManager pushes its end time forward every time it shows one.
            // A later end time = a new hint = one sound. Only a real warning ("Missed a checkpoint!") gets the
            // two falling notes; a coach tip or praise gets a soft click, so a friendly tip never sounds like an error.
            if (raceManager.HintText != "" && raceManager.HintVisibleUntil > lastHintUntil)
            {
                if (raceManager.CurrentHintKind == HintKind.Warning)
                {
                    Play(hintWarning);
                    Duck(-4f, hintWarning);
                }
                else
                {
                    Play(retryClick, coachClickVolume);
                }
            }
            lastHintUntil = raceManager.HintVisibleUntil;

            // The medal sounds wait in real time (task P6b). They play on the first source, so they never replace
            // the record sting that waits on the second one.
            if (stampAt >= 0f && Time.unscaledTime >= stampAt)
            {
                stampAt = -1f;
                Play(medalStamp, medalStampVolume);
            }
            if (sparkleAt >= 0f && Time.unscaledTime >= sparkleAt)
            {
                sparkleAt = -1f;
                Play(newMedalSparkle, newMedalSparkleVolume);
            }

            // Remembered for OnCountdownStep: a countdown that starts after Racing or Results is a retry.
            stateLastFrame = raceManager.State;
        }

        private void OnCountdownStep(int number)
        {
            if (number == 0)
            {
                Play(goBeep);
                Duck(-3f, goBeep);
                return;
            }

            // Retry: RaceManager fires the first beep and the Restarted click in the same frame, and the beep would
            // hide the click. So on a retry the first beep starts a moment later on the second source.
            // (This also cancels a record sting that was still waiting.)
            bool firstStepOfRetry = stateLastFrame == RaceState.Racing || stateLastFrame == RaceState.Results;
            if (firstStepOfRetry)
            {
                PlayLater(countdownBeep, retryBeepDelay);
            }
            else
            {
                Play(countdownBeep);
            }
            Duck(-3f, countdownBeep);
        }

        // RaceManager has already compared this checkpoint with your best lap (HasSplitDelta, LastSplitDelta),
        // so the chime can say ahead or behind (SplitChime). It has its own source, so changing its pitch never
        // bends another sound.
        private void OnCheckpointPassed(int index)
        {
            int semitones = SplitChime.Semitones(raceManager.HasSplitDelta, raceManager.LastSplitDelta);
            if (chimeSource != null && checkpointChime != null)
            {
                chimeSource.pitch = SplitChime.Pitch(semitones);
                chimeSource.PlayOneShot(checkpointChime, volume * SoundLevels.Effects);
            }
            else
            {
                Play(checkpointChime); // an old scene without the chime source: normal pitch
            }
            Duck(-4f, checkpointChime);
        }

        private void OnWrongWayShown()
        {
            Play(hintWarning, wrongWayVolume);
        }

        // The menu notes, lower: falling = "stopped", rising = "go on". On the menu source, because the pause has
        // just switched AudioListener.pause on and that source ignores it.
        private void OnPaused()
        {
            PlayMenu(menuClose, pauseCuePitch);
        }

        private void OnResumed()
        {
            PlayMenu(menuOpen, pauseCuePitch);
        }

        private void OnLapFinished(float lapSeconds, bool newRecord)
        {
            Play(finishJingle);
            Duck(-12f, finishJingle);

            // The results card comes in after the finish beat (0.6 s, none with reduced motion), and its stamps
            // land at fixed times after that (FinishMoment, matched to RaceHud.uss).
            float beat = FinishMoment.BeatFor(raceManager.ReducedMotion);
            Medal medal = raceManager.LastMedal;
            if (medal != Medal.None)
            {
                stampAt = Time.unscaledTime + beat + FinishMoment.MedalStampLandsSeconds;
                bool newMedal = medal > raceManager.PreviousBestMedal;
                sparkleAt = newMedal ? stampAt + 0.04f : -1f; // just after the thud, so both are heard
            }
            if (newRecord)
            {
                // With the NEW RECORD stamp, not on top of the jingle's first notes.
                float stingDelay = beat + recordStingDelay;
                PlayLater(newRecordSting, stingDelay);
                if (music != null && newRecordSting != null)
                {
                    music.Duck(-12f, stingDelay + newRecordSting.length * 0.6f);
                }
            }
        }

        private void OnRestarted()
        {
            // A sting still waiting from the old lap is stopped by the delayed first beep (PlayLater replaces it).
            // The medal sounds of the old lap are cancelled here.
            stampAt = -1f;
            sparkleAt = -1f;
            Play(retryClick);
        }

        private void OnRecovered()
        {
            Play(respawnWhoosh);
        }

        // Audit 10. The volume rows reuse the retry click: SoundLevels.Effects was already set to the new level, so
        // the click is an audible preview of it. Different pitches tell a step, an on/off and the mute key apart.
        private void OnMenuSound(MenuSound sound)
        {
            switch (sound)
            {
                case MenuSound.Open: PlayMenu(menuOpen, 1f); break;
                case MenuSound.Close: PlayMenu(menuClose, 1f); break;
                case MenuSound.PageTurn: PlayMenu(pageTurn, 1f); break;
                case MenuSound.SliderStep: PlayMenu(retryClick, 1f); break;
                case MenuSound.Toggle: PlayMenu(retryClick, 0.85f); break;
                case MenuSound.Mute: PlayMenu(retryClick, 1.12f); break;
            }
        }

        // Menu sounds have their own source: it ignores AudioListener.pause (set in Awake), and its
        // pitch can change without bending a race sound that is still ringing on the first source.
        private void PlayMenu(AudioClip clip, float pitch)
        {
            if (menuSource == null)
            {
                Play(clip); // an old scene without the menu source: no pitch change on the shared source
                return;
            }
            if (clip != null)
            {
                menuSource.pitch = pitch;
                menuSource.PlayOneShot(clip, volume * SoundLevels.Effects);
            }
        }

        // PlayOneShot lets several sounds overlap on one AudioSource. A missing clip is simply skipped.
        private void Play(AudioClip clip, float scale = 1f)
        {
            if (clip != null && source != null)
            {
                source.PlayOneShot(clip, volume * scale * SoundLevels.Effects); // settings slider (P7)
            }
        }

        // Starts a clip after "delay" seconds on the second source. Only one waiting sound at a time:
        // a new one replaces the old (a retry during the record sting stops the sting).
        private void PlayLater(AudioClip clip, float delay)
        {
            if (clip == null || delayedSource == null)
            {
                Play(clip); // no second source in an old scene: play right away
                return;
            }
            delayedSource.Stop();
            delayedSource.clip = clip;
            delayedSource.volume = volume * SoundLevels.Effects;
            delayedSource.PlayDelayed(delay);
        }

        // The music dips while this clip plays (hold = 60 % of the clip, then it comes back slowly).
        private void Duck(float db, AudioClip clip)
        {
            if (music != null && clip != null)
            {
                music.Duck(db, clip.length * 0.6f);
            }
        }
    }
}
