using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Audio
{
    // Task R5 (kart part): engine, drift, boost, landing and wind sounds.
    // Lives on the KartAudio prefab, which RaceBootstrap spawns as a child of the kart.
    // It only READS IKartState from its parent, so it works with any kart that follows the contract.
    // Engine and drift recordings are Luc's (Role 3), the rules for pitch and volume follow his handoff.
    // P4 polish: equal-power crossfade, a pretend 4-gear gearbox (revs climb and drop, short cut + pop on every
    // upshift), a throttle guess (lower and quieter when slowing down) and a tiny random pitch wobble.
    // Audit 11: a thud on a wall bump, a whoosh on take-off, and the boost whoosh again for a chained boost.
    // All three are guessed from IKartState alone by small plain C# classes (WallBumpDetector, TakeOffDetector,
    // BoostCue), so they are unit tested and need nothing new from Role 1.
    public class KartAudio : MonoBehaviour
    {
        [Header("Looping sounds (one AudioSource each)")]
        [SerializeField, Tooltip("Engine at low revs (kart_engine_low)")]
        private AudioSource engineLowSource;

        [SerializeField, Tooltip("Engine at high revs (kart_engine_high)")]
        private AudioSource engineHighSource;

        [SerializeField, Tooltip("Tyre squeal while drifting (kart_drift_squeal)")]
        private AudioSource squealSource;

        [SerializeField, Tooltip("Spark crackle while the drift has sparks (kart_drift_sparks)")]
        private AudioSource sparksSource;

        [SerializeField, Tooltip("Wind rush that grows with speed (amb_wind_loop)")]
        private AudioSource windSource;

        [Header("One-shot sounds")]
        [SerializeField, Tooltip("Own source for the stage-up chime, because its pitch changes per stage")]
        private AudioSource stageSource;

        [SerializeField, Tooltip("Second chime source. Stage 2 comes while the stage-1 chime still rings, so the two take turns")]
        private AudioSource secondStageSource;

        [SerializeField, Tooltip("Drift start chirp, boost, landing")]
        private AudioSource effectSource;

        [SerializeField, Tooltip("Own source for the gear-change pop, because its pitch is a bit random every time")]
        private AudioSource shiftSource;

        [SerializeField]
        private AudioClip driftStartClip;

        [SerializeField]
        private AudioClip stageUpClip;

        [SerializeField]
        private AudioClip boostClip;

        [SerializeField]
        private AudioClip landClip;

        [SerializeField, Tooltip("Short exhaust pop on every pretend upshift (sfx_shift_pop)")]
        private AudioClip shiftClip;

        [SerializeField, Tooltip("Thud when the kart hits a wall (sfx_wall_bump)")]
        private AudioClip bumpClip;

        [SerializeField, Tooltip("Airy whoosh when the kart takes off from a ramp or an edge (sfx_takeoff_whoosh)")]
        private AudioClip takeOffClip;

        [Header("Mix")]
        [SerializeField, Tooltip("How fast the revs may rise and fall, in 'full range' per second")]
        private float revRisePerSecond = 2.5f;

        [SerializeField]
        private float revFallPerSecond = 2.2f;

        [SerializeField, Tooltip("Extra revs while boosting or in the air (engine revs freely)")]
        private float revBonus = 0.1f;

        [Header("Pretend gearbox (the kart has no gears, the sound does)")]
        [SerializeField, Tooltip("Number of pretend gears. 4 suits a 35-60 s lap; more sounds busy")]
        private int gears = 4;

        [SerializeField, Range(0f, 0.95f), Tooltip("Revs right after an upshift (0..1). Lower = bigger drop")]
        private float revsAfterShift = 0.6f;

        [SerializeField, Tooltip("How long the engine goes quiet on an upshift (ignition cut), in seconds")]
        private float shiftCutSeconds = 0.07f;

        [SerializeField, Range(0f, 1f)]
        private float shiftVolume = 0.45f;

        [SerializeField, Range(0f, 1f)]
        private float engineIdleVolume = 0.45f;

        [SerializeField, Range(0f, 1f)]
        private float engineFullVolume = 0.75f;

        [SerializeField, Range(0f, 1f)]
        private float squealVolume = 0.6f;

        [SerializeField, Range(0f, 1f)]
        private float windFullVolume = 0.35f;

        [SerializeField, Tooltip("Air time needed before landing makes a thud")]
        private float minAirSeconds = 0.3f;

        [SerializeField, Tooltip("A jump further than this in one frame is a teleport (R, fall recover, retry), not driving")]
        private float teleportDistance = 10f;

        [Header("Wall bump and take-off (audit 11)")]
        [SerializeField, Tooltip("Speed lost in one frame (m/s) before it counts as a wall bump")]
        private float bumpMinSpeedDrop = 4f;

        [SerializeField, Tooltip("Speed lost in one frame at which the thud is at full volume (m/s)")]
        private float bumpFullSpeedDrop = 15f;

        [SerializeField, Tooltip("The drop per second must be above this (m/s2). The kart brakes at 30 at most, so a brake is never a bump")]
        private float bumpMinDeceleration = 60f;

        [SerializeField, Tooltip("Seconds after a thud before the next one can play (grinding along a wall = one thud)")]
        private float bumpCooldownSeconds = 0.4f;

        [SerializeField, Tooltip("Air time before the take-off whoosh plays (a small bump in the road is shorter)")]
        private float takeOffAirSeconds = 0.15f;

        private IKartState kart;
        private float revs;       // smoothed 0..1 revs inside the current gear: drives the PITCH
        private float speedRevs;  // smoothed 0..1 speed: drives the MIX of the two loops and the volume
        private VirtualGearbox gearbox;
        private readonly ThrottleGuess throttle = new ThrottleGuess();
        private float shiftCutLeft;      // seconds left of the ignition cut after an upshift
        private float engineLevel = 1f;  // set by RaceMusic: quieter engine on the results screen
        private float effectsLevel = 1f; // same for drift, wind, boost, landing
        private bool wasDrifting;
        private bool wasGrounded = true;
        private WallBumpDetector bumpDetector;
        private TakeOffDetector takeOffDetector;
        private readonly BoostCue boostCue = new BoostCue();
        private int lastDriftStage;
        private float airSeconds;
        private Vector3 lastPosition;
        private bool useSecondStageSource; // which chime source plays next

        private void Start()
        {
            kart = GetComponentInParent<IKartState>();
            if (kart == null)
            {
                Debug.LogWarning("KartAudio: no IKartState found on a parent object. Put the KartAudio prefab under the kart.");
                enabled = false;
                return;
            }
            // All loops run all the time; we only change their volume. Starting a loop late can click.
            StartSilent(engineLowSource);
            StartSilent(engineHighSource);
            StartSilent(squealSource);
            StartSilent(sparksSource);
            StartSilent(windSource);
            lastPosition = transform.position;
            gearbox = new VirtualGearbox(gears);
            bumpDetector = new WallBumpDetector(bumpMinSpeedDrop, bumpMinDeceleration, bumpCooldownSeconds);
            takeOffDetector = new TakeOffDetector(takeOffAirSeconds);
            takeOffDetector.Reset(); // the kart may settle onto the grid first: no whoosh for that
        }

        // Called by RaceMusic (RaceFlow) when the race phase changes, e.g. quieter engine on the results screen.
        // Plain volume factors 0..1; 1 = normal.
        public void SetMixLevels(float engine, float effects)
        {
            engineLevel = engine;
            effectsLevel = effects;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float speed01 = EnginePitch.Speed01(kart.Speed, kart.MaxSpeed);

            // Audio may only read IKartState, so it cannot listen to the RaceManager's Recovered event.
            // Instead: a big jump in one frame means the kart was teleported. Then the fall before it was not a
            // jump, and landing on the respawn point must not play a thud on top of the respawn whoosh.
            Vector3 position = transform.position;
            if ((position - lastPosition).sqrMagnitude > teleportDistance * teleportDistance)
            {
                airSeconds = 0f;
                wasGrounded = true;
                throttle.Reset(); // the speed jump of a teleport is not the driver accelerating
                bumpDetector.Reset(); // nor is the speed going to 0 a wall bump
                takeOffDetector.Reset(); // nor is dropping onto the respawn point a jump
                boostCue.Reset();
            }
            lastPosition = position;

            UpdateEngine(speed01, dt);
            UpdateDrift(dt);
            UpdateWind(speed01);
            bool bumped = UpdateWallBump(dt);
            UpdateBoostAndLanding(dt, bumped);
        }

        // Thud on a wall bump, louder for a harder hit (WallBumpDetector).
        private bool UpdateWallBump(float dt)
        {
            if (!bumpDetector.Update(kart.Speed, kart.IsGrounded, kart.InputEnabled, dt))
            {
                return false;
            }
            float loudness = WallBumpDetector.VolumeFor(bumpDetector.LastDrop, bumpMinSpeedDrop, bumpFullSpeedDrop);
            PlayOneShot(effectSource, bumpClip, 0.9f * loudness);
            return true;
        }

        // Two loops crossfaded by speed, pitched by pretend gear revs. Idle while the player cannot drive.
        // Pitch follows the revs (they climb and drop with every gear), the mix of the two loops follows the
        // speed, so the high loop does not fade out on every gear change.
        private void UpdateEngine(float speed01, float dt)
        {
            float targetRevs = 0f;
            float targetSpeed = 0f;
            if (kart.InputEnabled)
            {
                if (gearbox.Update(speed01))
                {
                    shiftCutLeft = shiftCutSeconds;
                    PlayShiftPop();
                }
                targetRevs = EnginePitch.GearRevs(speed01, gearbox.Gear, gears, revsAfterShift);
                targetSpeed = speed01;
                // Boost or air time: the engine revs a bit higher (smoothed below, so no pitch jump).
                if (kart.IsBoosting || !kart.IsGrounded)
                {
                    targetRevs += revBonus;
                }
            }
            else
            {
                gearbox.Reset(); // countdown, results: back in first gear
            }
            revs = EnginePitch.MoveLimited(revs, targetRevs, revRisePerSecond, revFallPerSecond, dt);
            speedRevs = EnginePitch.MoveLimited(speedRevs, targetSpeed, revRisePerSecond, revFallPerSecond, dt);

            // Lifting off the throttle: a little lower and quieter (guessed from the change in speed).
            float load = throttle.Tick(Mathf.Abs(kart.Speed), dt);
            // A tiny random wobble (Perlin noise) so the loop never sounds like a perfect machine.
            float wobble = 1f + 0.004f * (Mathf.PerlinNoise(Time.time * 6f, 0.1f) - 0.5f);
            float pitchFactor = Mathf.Lerp(0.94f, 1f, load) * wobble;

            float volume = Mathf.Lerp(engineIdleVolume, engineFullVolume, speedRevs);
            volume *= Mathf.Lerp(0.6f, 1f, load);
            if (shiftCutLeft > 0f)
            {
                shiftCutLeft -= dt;
                volume *= 0.4f; // ignition cut: the short gap you hear when a real engine shifts up
            }
            volume *= engineLevel;

            float highMix = EnginePitch.HighLoopMix(speedRevs);
            engineLowSource.pitch = EnginePitch.LowLoopPitch(revs) * pitchFactor;
            engineHighSource.pitch = EnginePitch.HighLoopPitch(revs) * pitchFactor;
            engineLowSource.volume = volume * EnginePitch.EqualPowerLow(highMix);
            engineHighSource.volume = volume * EnginePitch.EqualPowerHigh(highMix);
        }

        // The pop varies a little in pitch every time, so ten shifts per lap never sound copy-pasted.
        private void PlayShiftPop()
        {
            if (shiftSource == null || shiftClip == null)
            {
                return;
            }
            shiftSource.pitch = Random.Range(0.92f, 1.08f);
            shiftSource.PlayOneShot(shiftClip, shiftVolume * effectsLevel);
        }

        private void UpdateDrift(float dt)
        {
            bool drifting = kart.IsDrifting && kart.IsGrounded;
            int stage = drifting ? kart.DriftStage : 0;

            // Chirp when the drift starts.
            if (drifting && !wasDrifting)
            {
                PlayOneShot(effectSource, driftStartClip, 0.8f);
            }
            wasDrifting = drifting;

            // Squeal fades in and out over a few tenths of a second instead of a hard cut.
            float squealTarget = drifting ? squealVolume * effectsLevel : 0f;
            squealSource.volume = Mathf.MoveTowards(squealSource.volume, squealTarget, dt * 3f);

            // Sparks: louder and a bit higher with every stage.
            float sparksTarget = DriftSound.SparksVolume(stage) * effectsLevel;
            sparksSource.volume = Mathf.MoveTowards(sparksSource.volume, sparksTarget, dt * 4f);
            sparksSource.pitch = DriftSound.SparksPitch(stage);

            // Chime each time the drift reaches a new stage, higher for every stage. The chime is 1.2 s long and
            // stage 2 comes 0.8 s after stage 1, so two sources take turns: changing the pitch of a source that is
            // still ringing would bend the first chime too.
            if (stage > lastDriftStage)
            {
                AudioSource chimeSource = useSecondStageSource && secondStageSource != null ? secondStageSource : stageSource;
                useSecondStageSource = !useSecondStageSource;
                chimeSource.pitch = DriftSound.StageUpPitch(stage);
                PlayOneShot(chimeSource, stageUpClip, 0.8f);
            }
            lastDriftStage = stage;
        }

        // Wind: only really audible when fast (speed squared), so it makes speed feel fast.
        private void UpdateWind(float speed01)
        {
            windSource.volume = windFullVolume * speed01 * speed01 * effectsLevel;
            windSource.pitch = 0.8f + 0.4f * speed01;
        }

        private void UpdateBoostAndLanding(float dt, bool bumpedThisFrame)
        {
            // Boost: whoosh on the frame the boost starts, and again for a chained boost (BoostCue).
            if (boostCue.Update(kart.IsBoosting, kart.IsDrifting, kart.DriftStage, kart.IsGrounded, kart.InputEnabled, bumpedThisFrame))
            {
                PlayOneShot(effectSource, boostClip, 0.9f);
            }

            // Take-off: whoosh once the kart has been in the air long enough to be a real jump.
            if (takeOffDetector.Update(kart.IsGrounded, kart.InputEnabled, dt))
            {
                PlayOneShot(effectSource, takeOffClip, 0.7f);
            }

            // Landing after a jump.
            if (!kart.IsGrounded)
            {
                airSeconds += dt;
            }
            else
            {
                if (!wasGrounded && airSeconds >= minAirSeconds)
                {
                    PlayOneShot(effectSource, landClip, 0.8f);
                }
                airSeconds = 0f;
            }
            wasGrounded = kart.IsGrounded;
        }

        private static void StartSilent(AudioSource source)
        {
            source.volume = 0f;
            source.Play();
        }

        // PlayOneShot lets sounds overlap on one AudioSource. A missing clip is simply skipped.
        // effectsLevel makes the kart effects quieter on the results screen (set by RaceMusic).
        private void PlayOneShot(AudioSource source, AudioClip clip, float volume)
        {
            if (clip != null)
            {
                source.PlayOneShot(clip, volume * effectsLevel);
            }
        }
    }
}
