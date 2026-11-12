using CarcavelosCircuit.Driving;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Track;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). The bot race around the team's normal race: it does what RaceBootstrap does
    // (spawn the player's kart, hand it to the RaceManager, point the camera at it) and adds five bot karts.
    // RaceFlow is not changed: this script only reads it (state, InputEnabled, the Restarted event) and calls its
    // public methods. It also keeps the race order and tells the RaceManager when the player's LAST lap is done.
    public class BotRace : MonoBehaviour
    {
        [Header("The team's race (from Main.unity)")]
        [SerializeField] private RaceManager raceManager;
        [SerializeField] private PlaceholderChaseCamera chaseCamera;
        [SerializeField, Tooltip("The kart everybody drives (Driving/Kart.prefab)")]
        private GameObject kartPrefab;
        [SerializeField, Tooltip("Spawned under the player's kart only (KartAudio)")]
        private GameObject[] playerAttachments;
        [SerializeField, Tooltip("What the RaceManager on this branch understands as 'the track'")]
        private PlaceholderTrack raceManagerTrack;
        [SerializeField, Tooltip("All its checkpoints use this one respawn point; BotRace moves it behind the player")]
        private Transform playerRespawn;

        [Header("Track")]
        [SerializeField] private TrackInfo trackInfo;
        [SerializeField] private BotRoute route;
        [SerializeField] private KartTuning tuning;
        [SerializeField, Tooltip("Six grid places behind the start line, front row first")]
        private Transform[] gridSlots;
        [SerializeField, Tooltip("Which grid place the player gets (5 = last)")]
        private int playerSlot = 5;

        [Header("Race")]
        [SerializeField] private int totalLaps = 3;
        [SerializeField] private BotProfile[] bots;
        [SerializeField, Tooltip("Bot karts are lighter than the player's (1), so in a bump the bot gives way")]
        private float botMass = 0.6f;

        [SerializeField, Tooltip("One knob for the whole field. Normal = the tuned bots, Easy = slower, Hard = faster")]
        private BotDifficulty difficulty = BotDifficulty.Normal;

        [Header("Calibration against a human")]
        [SerializeField, Tooltip("H: a clean human flying lap in seconds. Until Luc's real time is known: the fastest bot's own lap")]
        private float humanLapSeconds = 65.4f;
        [SerializeField, Tooltip("Measured: flying lap of the fastest bot at full pace (pace factor 1), in seconds")]
        private float leaderLapAtFullPace = 64.4f;
        [SerializeField, Tooltip("Fastest bot's flying lap as a multiple of H on Easy (research: 1.07 to 1.12)")]
        private float easyLeaderLap = 1.11f;
        [SerializeField, Tooltip("... on Normal (research: 1.00 to 1.04)")]
        private float normalLeaderLap = 0.99f;
        [SerializeField, Tooltip("... on Hard (research: 0.95 to 1.00)")]
        private float hardLeaderLap = 0.975f;

        [Header("Speed of all karts (player and bots)")]
        [SerializeField, Tooltip("Top speed of every kart as a share of KartTuning (0.85 = 15 % slower). The asset is not changed: each race uses a scaled copy")]
        private float speedScale = 0.85f;

        [Header("Camera (overrides the chase camera's values in this scene only; 0 = keep its own value)")]
        [SerializeField, Tooltip("Metres behind the kart (the team's camera: 6)")]
        private float cameraDistance = 4.2f;
        [SerializeField, Tooltip("Metres above the kart (the team's camera: 2.5)")]
        private float cameraHeight = 1.9f;
        [SerializeField, Tooltip("How fast the camera catches up; higher = the kart pulls away less at speed (the team's camera: 6)")]
        private float cameraFollowSharpness = 14f;
        [SerializeField, Tooltip("Field of view standing still (the team's camera: 65)")]
        private float cameraMinFieldOfView = 60f;
        [SerializeField, Tooltip("Field of view at top speed (the team's camera: 80)")]
        private float cameraMaxFieldOfView = 63f;

        [Header("Drift sparks and boost flame")]
        [SerializeField, Tooltip("Role 3's KartEffects prefab; spawned under every kart")]
        private GameObject kartEffectsPrefab;

        [Header("Open races")]
        [SerializeField, Tooltip("Race-day form: every race each bot's top speed and corner speed move by up to this much (0.015 = 1.5 %) up or down")]
        private float raceDayForm = 0.02f;
        [SerializeField, Tooltip("A kart must be this many metres ahead before a place change counts as an overtake")]
        private float overtakeMetres = 3f;
        [SerializeField, Tooltip("Shuffle the bots' grid places before every race (the player keeps his place)")]
        private bool shuffleGrid = true;

        [Header("Rubber band (small correction of the cruise speed, never above the shared top speed)")]
        [SerializeField, Tooltip("Cruise-speed bonus of a bot far BEHIND the player (0.03 = +3 %)")]
        private float catchUpBonus = 0.03f;
        [SerializeField, Tooltip("Cruise-speed cut of a bot far AHEAD of the player (0.02 = -2 %)")]
        private float leaderCut = 0.02f;
        [SerializeField, Tooltip("No correction while the bot is closer to the player than this, in metres")]
        private float rubberDeadZone = 15f;
        [SerializeField, Tooltip("Full correction from this distance, in metres")]
        private float rubberFullDistance = 75f;
        [SerializeField, Tooltip("Seconds the correction takes to follow (exponential smoothing)")]
        private float rubberSmoothSeconds = 2f;
        [SerializeField, Tooltip("No correction before the bot and the player have raced this far (through the first corner), in metres")]
        private float rubberOffAtStartMetres = 80f;
        [SerializeField, Tooltip("No correction for this long after a bot was put back on the road, in seconds")]
        private float rubberOffAfterRecoverySeconds = 4f;
        [SerializeField, Tooltip("The correction fades to nothing over the last metres of the race")]
        private float rubberFinishFadeMetres = 120f;

        [Header("Bot engine sound")]
        [SerializeField, Tooltip("Looping engine recording (Audio/Clips/kart_engine_low)")]
        private AudioClip botEngineClip;
        [SerializeField, Tooltip("Only this many bots are heard at once (the nearest ones): browsers have few voices")]
        private int maxBotVoices = 3;

        public RaceKart[] Karts { get; private set; } = new RaceKart[0];      // all six, element 0 = the player
        public RaceKart[] Standings { get; private set; } = new RaceKart[0];  // the same karts, leader first
        public RaceKart Player { get; private set; }
        public RaceManager RaceManager { get { return raceManager; } }
        public int TotalLaps { get { return totalLaps; } }
        public float RaceTime { get; private set; }     // seconds since GO
        public bool RaceRunning { get; private set; }   // true from GO until the next retry
        public bool IsSetUp { get { return Player != null; } }
        public BotDifficulty Difficulty { get { return difficulty; } set { difficulty = value; } }

        // The difficulty as one number: how much of its full pace the field uses (1 = the tuned bots).
        // Lap time is roughly 1 / pace, so to make the fastest bot lap in (tier target x H) seconds we use
        // pace = lap of the fastest bot at full pace / (tier target x H).
        public float PaceFactor
        {
            get
            {
                float target = difficulty == BotDifficulty.Easy ? easyLeaderLap : difficulty == BotDifficulty.Hard ? hardLeaderLap : normalLeaderLap;
                return Mathf.Clamp(leaderLapAtFullPace / (target * humanLapSeconds), 0.75f, 1.1f);
            }
        }

        // Test switches (BotRaceTest sets them before Start).
        public bool AutoPilotPlayer { get; set; }       // the player's kart is driven by a bot too
        public bool RubberBandOff { get; set; }
        public bool SlowBotsInFront { get; set; }
        public float[] ProbeLanes { get; set; }          // edge probe: one fixed line per kart (see BotDriver.ProbeLane)
        public float SpeedScaleOverride { get; set; }    // edge probe: another top-speed scale than the scene's (0 = none)
        public int TotalLapsOverride { get; set; }       // edge probe: shorter race (0 = none)
        public float AutoPilotTopSpeed { get; set; } = 0.97f; // pace of the autopilot on the player's kart
        public int Seed { get; set; }                   // start value for the bots' random numbers; 0 = a new one every session       // grid upside down, so the fast bots must overtake

        private BotDriver[] drivers = new BotDriver[0]; // drivers[i] drives Karts[i]; null for a human player
        private Transform[] kartSlots = new Transform[0];
        private bool muted;
        private System.Random gridRandom;
        private bool[,] clearlyAhead = new bool[0, 0];   // [a, b] = kart a was last seen clearly ahead of kart b
        private int checkpointsForwarded;
        private float[] rubberTarget = new float[0];
        private int stepCount;
        private float[] rubber = new float[0];          // current rubber-band correction per kart (0 = none, +0.03 = 3 % faster)
        private BotEngineSound[] engines = new BotEngineSound[0];
        private int playerLapsLogged;
        private float voiceTimer;

        private void Start()
        {
            Application.targetFrameRate = -1; // as RaceBootstrap: let the browser draw in step with the screen

            int count = bots.Length + 1;
            Karts = new RaceKart[count];
            Standings = new RaceKart[count];
            drivers = new BotDriver[count];
            kartSlots = new Transform[count];
            clearlyAhead = new bool[count, count];
            rubber = new float[count];
            rubberTarget = new float[count];
            engines = new BotEngineSound[count];
            for (int i = 0; i < count; i++)
            {
                rubber[i] = 0f;
            }
            if (Seed == 0)
            {
                Seed = System.Environment.TickCount;
            }
            ReadDifficultyFromAddress();

            // ?mute=1 in the page address: no sound at all (for checking a build quietly).
            string address = Application.absoluteURL == null ? "" : Application.absoluteURL.ToLowerInvariant();
            muted = address.Contains("mute=1");

            if (SpeedScaleOverride > 0f)
            {
                speedScale = SpeedScaleOverride;
            }
            if (TotalLapsOverride > 0)
            {
                totalLaps = TotalLapsOverride;
            }

            // One scaled copy of the kart tuning for this race. JsonUtility writes only the fields named here into
            // the copy, so everything else stays as Role 1 tuned it, and the asset on disk is never touched.
            KartTuning raceTuning = Instantiate(tuning);
            JsonUtility.FromJsonOverwrite("{\"maxSpeed\":" + Number(tuning.MaxSpeed * speedScale)
                + ",\"fullSteerSpeed\":" + Number(tuning.FullSteerSpeed * speedScale) + "}", raceTuning);
            tuning = raceTuning;
            Debug.Log("BOTRACE top speed " + Number(tuning.MaxSpeed * 3.6f) + " km/h (scale " + Number(speedScale) + ")");

            // The camera closer to the kart, same idea: only the named values change, the camera script is not edited.
            if (chaseCamera != null && cameraDistance > 0f)
            {
                JsonUtility.FromJsonOverwrite("{\"distance\":" + Number(cameraDistance) + ",\"height\":" + Number(cameraHeight)
                    + ",\"followSharpness\":" + Number(cameraFollowSharpness) + ",\"minFieldOfView\":" + Number(cameraMinFieldOfView)
                    + ",\"maxFieldOfView\":" + Number(cameraMaxFieldOfView) + "}", chaseCamera);
            }

            // The player's kart, exactly as RaceBootstrap makes it.
            Transform slot = gridSlots[playerSlot];
            GameObject playerObject = Instantiate(kartPrefab, slot.position, slot.rotation);
            playerObject.name = "Kart";
            for (int i = 0; i < playerAttachments.Length; i++)
            {
                if (playerAttachments[i] != null)
                {
                    Instantiate(playerAttachments[i], playerObject.transform);
                }
            }
            playerObject.GetComponent<KartController>().SetTuning(tuning);
            if (kartEffectsPrefab != null)
            {
                Instantiate(kartEffectsPrefab, playerObject.transform);
            }
            raceManager.SetUp(playerObject, raceManagerTrack, "BotRace"); // own name = own best-time save
            raceManager.Restarted += OnRestarted;
            if (chaseCamera != null)
            {
                chaseCamera.SetTarget(playerObject.transform);
                raceManager.Restarted += chaseCamera.SnapBehindTarget;
            }
            Player = playerObject.AddComponent<RaceKart>();
            Player.Number = 0;
            Player.SetUp(route, "YOU", Color.white, true);
            Karts[0] = Player;
            kartSlots[0] = slot;
            if (AutoPilotPlayer)
            {
                BotProfile autoPilot = new BotProfile();
                autoPilot.name = "YOU (autopilot)";
                autoPilot.topSpeed = AutoPilotTopSpeed;
                autoPilot.cornering = 0.88f;
                autoPilot.lane = 0.2f;
                drivers[0] = playerObject.AddComponent<BotDriver>();
                drivers[0].SetUp(this, route, tuning, trackInfo, autoPilot);
            }

            // The bots: the same kart prefab, a colour, a driver. They fill the other grid places front to back.
            for (int b = 0; b < bots.Length; b++)
            {
                // Bot number b takes the b-th free grid place (the player's place is skipped).
                // Test switch SlowBotsInFront: the bots line up in the opposite order.
                Transform botSlot = gridSlots[0]; // the real places are dealt out below (PlaceBotsOnGrid)
                GameObject botObject = Instantiate(kartPrefab, botSlot.position, botSlot.rotation);
                botObject.name = "Bot " + bots[b].name;
                botObject.GetComponent<Rigidbody>().mass = botMass;
                botObject.GetComponent<KartController>().SetTuning(tuning);
                if (kartEffectsPrefab != null)
                {
                    Instantiate(kartEffectsPrefab, botObject.transform);
                }
                BotKartPaint.Paint(botObject, bots[b].colour);
                if (botEngineClip != null)
                {
                    engines[b + 1] = botObject.AddComponent<BotEngineSound>();
                    engines[b + 1].SetUp(botEngineClip, b, bots.Length);
                }
                RaceKart racer = botObject.AddComponent<RaceKart>();
                racer.Number = b + 1;
                racer.SetUp(route, bots[b].name, bots[b].colour, false);
                BotDriver driver = botObject.AddComponent<BotDriver>();
                driver.SetUp(this, route, tuning, trackInfo, bots[b]);
                Karts[b + 1] = racer;
                drivers[b + 1] = driver;
                kartSlots[b + 1] = botSlot;
            }
            for (int i = 0; i < count; i++)
            {
                Standings[i] = Karts[i];
                Karts[i].Number = i;
            }
            if (ProbeLanes != null)
            {
                SetUpProbe();
            }
            gridRandom = new System.Random(Seed * 613 + 17);
            PlaceBotsOnGrid();
            for (int i = 0; i < count; i++)
            {
                if (drivers[i] != null)
                {
                    drivers[i].NewRace(raceDayForm);
                }
            }
        }

        private void OnDestroy()
        {
            if (allocations.Valid)
            {
                allocations.Dispose();
            }
            if (raceManager != null)
            {
                raceManager.Restarted -= OnRestarted;
                if (chaseCamera != null)
                {
                    raceManager.Restarted -= chaseCamera.SnapBehindTarget;
                }
            }
        }

        // One loop per physics step, always in the same order: where is everybody, who leads, then the bots drive.
        private void FixedUpdate()
        {
            if (!IsSetUp)
            {
                return;
            }
            // The bots start when the player may drive: the RaceManager switches the player's input on at GO.
            if (!RaceRunning && Player.Kart.InputEnabled)
            {
                RaceRunning = true;
                RaceTime = 0f;
            }
            if (RaceRunning)
            {
                RaceTime += Time.fixedDeltaTime;
            }

            stepCount++;
            long bytesBefore = 0;
            long ticksBefore = 0;
            if (MeasureCost)
            {
                bytesBefore = AllocatedSoFar();
                ticksBefore = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            for (int i = 0; i < Karts.Length; i++)
            {
                Karts[i].Track(RaceTime, totalLaps, RaceRunning);
            }
            TellRaceManagerAboutThePlayer();
            SortStandings();
            CountOvertakes();
            LogPlayerLaps();

            // The bots drive in race order, leader first. So when two bots want the same free lane in the same
            // step, the one further ahead decides first and the other sees the lane as taken (same progress: lower number).
            for (int place = 0; place < Standings.Length; place++)
            {
                int i = Standings[place].Number;
                if (drivers[i] == null)
                {
                    continue; // the human player
                }
                if (!Karts[i].IsPlayer)
                {
                    Karts[i].Kart.InputEnabled = RaceRunning; // bots keep racing after the player has finished
                }
                // The correction follows its target slowly (about rubberSmoothSeconds), so a bot never jolts.
                // The target is worked out 10 times a second, each bot in another step; the smoothing runs every step.
                if ((stepCount + i) % 5 == 0)
                {
                    rubberTarget[i] = RubberBandTarget(Karts[i], drivers[i]);
                }
                float follow = 1f - Mathf.Exp(-Time.fixedDeltaTime / rubberSmoothSeconds);
                rubber[i] = Mathf.Lerp(rubber[i], rubberTarget[i], follow);
                drivers[i].Tick(Time.fixedDeltaTime, rubber[i]);
                if (engines[i] != null)
                {
                    engines[i].Drive(Karts[i].Kart.Speed, Karts[i].Kart.MaxSpeed, drivers[i].Throttle);
                }
            }
            if (MeasureCost && RaceRunning && costSamples < costMilliseconds.Length)
            {
                long bytes = AllocatedSoFar() - bytesBefore;
                CostBytesTotal += bytes;
                if (bytes > 0)
                {
                    CostStepsWithAllocation++;
                }
                costMilliseconds[costSamples] = (System.Diagnostics.Stopwatch.GetTimestamp() - ticksBefore) * 1000f / System.Diagnostics.Stopwatch.Frequency;
                costSamples++;
            }
        }

        // Unity counts every managed allocation of the main thread ("GC.Alloc" samples). Reading the counter before and
        // after our code tells how many allocations our code made in between.
        private Unity.Profiling.ProfilerRecorder allocations;

        private long AllocatedSoFar()
        {
            if (!allocations.Valid)
            {
                allocations = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            }
            return allocations.CurrentValue;
        }

        // Test only: cost of everything BotRace does in one physics step (tracking, order, all bot decisions).
        public bool MeasureCost { get; set; }
        public long CostBytesTotal { get; private set; }
        public int CostStepsWithAllocation { get; private set; }
        public int CostSamples { get { return costSamples; } }
        private readonly float[] costMilliseconds = new float[30000];
        private int costSamples;

        // The value below which 95 % of the measured steps stay, in milliseconds.
        public float CostMilliseconds95()
        {
            if (costSamples == 0)
            {
                return 0f;
            }
            float[] sorted = new float[costSamples];
            System.Array.Copy(costMilliseconds, sorted, costSamples);
            System.Array.Sort(sorted);
            return sorted[(int)(costSamples * 0.95f)];
        }

        // The bot that drives this kart, or null for a human.
        public float RubberBandOf(RaceKart kart)
        {
            return rubber[kart.Number];
        }

        public BotDriver DriverOf(RaceKart kart)
        {
            return drivers[kart.Number];
        }

        // Keys 1 / 2 / 3 before GO pick Easy / Normal / Hard. Four times a second: which bots are heard.
        private void Update()
        {
            if (!IsSetUp)
            {
                return;
            }
            if (muted && AudioListener.volume != 0f)
            {
                AudioListener.volume = 0f; // every frame: the settings menu must not switch the sound back on
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !RaceRunning)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) difficulty = BotDifficulty.Easy;
                if (keyboard.digit2Key.wasPressedThisFrame) difficulty = BotDifficulty.Normal;
                if (keyboard.digit3Key.wasPressedThisFrame) difficulty = BotDifficulty.Hard;
            }
            voiceTimer -= Time.unscaledDeltaTime;
            if (voiceTimer <= 0f)
            {
                voiceTimer = 0.25f;
                PickBotVoices();
            }
        }

        // A web page address ending in ?difficulty=easy (or hard) sets the difficulty without opening Unity.
        private void ReadDifficultyFromAddress()
        {
            string address = Application.absoluteURL == null ? "" : Application.absoluteURL.ToLowerInvariant();
            if (address.Contains("difficulty=easy")) difficulty = BotDifficulty.Easy;
            if (address.Contains("difficulty=normal")) difficulty = BotDifficulty.Normal;
            if (address.Contains("difficulty=hard")) difficulty = BotDifficulty.Hard;
        }

        // Browsers can only play a limited number of sounds at once: the nearest bots to the camera are heard,
        // the others fade out.
        private void PickBotVoices()
        {
            Vector3 ear = chaseCamera != null ? chaseCamera.transform.position : Player.transform.position;
            for (int i = 0; i < Karts.Length; i++)
            {
                if (engines[i] == null)
                {
                    continue;
                }
                // Count the bots that are nearer than this one; fewer than the cap = this one is heard.
                float mine = (Karts[i].transform.position - ear).sqrMagnitude;
                int nearer = 0;
                for (int j = 0; j < Karts.Length; j++)
                {
                    if (j != i && engines[j] != null && (Karts[j].transform.position - ear).sqrMagnitude < mine)
                    {
                        nearer++;
                    }
                }
                engines[i].Audible = nearer < maxBotVoices;
            }
        }

        // For calibrating the bots against a real player: one console line per finished player lap.
        // "leader bot" = the fastest time any bot has set for that same lap number so far.
        private void LogPlayerLaps()
        {
            while (playerLapsLogged < Player.LapTimes.Count)
            {
                int lap = playerLapsLogged;
                playerLapsLogged++;
                float best = -1f;
                RaceKart bestKart = null;
                for (int i = 0; i < Karts.Length; i++)
                {
                    if (!Karts[i].IsPlayer && Karts[i].LapTimes.Count > lap && (best < 0f || Karts[i].LapTimes[lap] < best))
                    {
                        best = Karts[i].LapTimes[lap];
                        bestKart = Karts[i];
                    }
                }
                Debug.Log("BOTRACE lap " + (lap + 1) + ": player " + Seconds(Player.LapTimes[lap])
                    + " s, leader bot " + (best < 0f ? "not there yet" : Seconds(best) + " s")
                    + " (difficulty " + difficulty + ", H " + Seconds(humanLapSeconds) + " s)"
                    + " | sectors player" + SectorText(Player.LapSectors[lap])
                    + " | leader" + (bestKart == null ? " -" : SectorText(bestKart.LapSectors[lap])));
            }
        }

        // The RaceManager on this branch is a one-lap time trial. Its own checkpoint triggers are switched off in
        // this scene; instead we report the player's checkpoints of lap 1 (HUD pips, split times) and the
        // start/finish line only when the LAST lap is done. Then it stops the clock and shows the results as always.
        private void TellRaceManagerAboutThePlayer()
        {
            // R (recover) puts the player at this point: keep it at the checkpoint the player passed last.
            Transform respawn = Player.Lap < 0 ? kartSlots[0] : trackInfo.GetRespawnPoint(Player.LastCheckpoint);
            playerRespawn.SetPositionAndRotation(respawn.position, respawn.rotation);

            if (raceManager.State != RaceState.Racing || Player.Lap < 0)
            {
                return;
            }
            int passed = Player.Lap == 0 ? Player.LastCheckpoint : route.checkpointAt.Length - 1;
            while (checkpointsForwarded < passed)
            {
                checkpointsForwarded++;
                raceManager.OnCheckpointEntered(checkpointsForwarded);
            }
            if (Player.Finished)
            {
                raceManager.OnCheckpointEntered(0);
            }
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Seconds(float value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        // "4.91 5.20 ..." = seconds from each checkpoint to the next, starting at the start/finish line.
        public static string SectorText(float[] sectors)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < sectors.Length; i++)
            {
                text.Append(' ').Append(Seconds(sectors[i]));
            }
            return text.ToString();
        }

        // Rubber band: the correction a bot SHOULD have right now, as a share of its cruise speed (+0.03 = 3 % faster).
        // 0 inside the dead zone, then an S-shaped rise to the full value at rubberFullDistance.
        // It is switched off where it would feel unfair or cause chaos: through the first corner, right after a
        // recovery, for lapped karts, and it fades out over the last metres of the race.
        private float RubberBandTarget(RaceKart bot, BotDriver driver)
        {
            if (RubberBandOff || bot.IsPlayer || bot.Finished)
            {
                return 0f;
            }
            float behind = Player.Progress - bot.Progress; // metres, + = the bot trails the player
            bool lapped = Mathf.Abs(behind) > route.Length * 0.5f;
            bool atStart = bot.Progress < rubberOffAtStartMetres || Player.Progress < rubberOffAtStartMetres;
            if (lapped || atStart || driver.SecondsSinceRecovery < rubberOffAfterRecoverySeconds)
            {
                return 0f;
            }
            float x = Mathf.Clamp01((Mathf.Abs(behind) - rubberDeadZone) / (rubberFullDistance - rubberDeadZone));
            float s = x * x * (3f - 2f * x);
            float wanted = behind > 0f ? catchUpBonus * s : -leaderCut * s;
            float metresToFinish = totalLaps * route.Length - bot.Progress;
            return wanted * Mathf.Clamp01(metresToFinish / rubberFinishFadeMetres);
        }

        // Leader first. Finished karts by finish time, the others by metres raced. Six karts: a simple sort is enough.
        private void SortStandings()
        {
            for (int i = 1; i < Standings.Length; i++)
            {
                RaceKart moving = Standings[i];
                int j = i - 1;
                while (j >= 0 && IsAheadOf(moving, Standings[j]))
                {
                    Standings[j + 1] = Standings[j];
                    j--;
                }
                Standings[j + 1] = moving;
            }
        }

        // Overtakes on the track. For every pair of karts we remember who was clearly ahead (by more than
        // overtakeMetres). When the other one is clearly ahead later, that is one overtake. Side-by-side wobble
        // does not count, and neither does a place won because the other kart was put back on the road.
        private void CountOvertakes()
        {
            if (!RaceRunning || RaceTime < 3f)
            {
                return;
            }
            for (int a = 0; a < Karts.Length; a++)
            {
                for (int b = 0; b < Karts.Length; b++)
                {
                    if (a == b || Karts[a].Progress < Karts[b].Progress + overtakeMetres)
                    {
                        continue;
                    }
                    // a is clearly ahead of b now.
                    bool recovering = (drivers[a] != null && drivers[a].SecondsSinceRecovery < 5f)
                        || (drivers[b] != null && drivers[b].SecondsSinceRecovery < 5f);
                    if (clearlyAhead[b, a] && !recovering && !Karts[a].Finished && !Karts[b].Finished)
                    {
                        Karts[a].OvertakesMade++;
                        Karts[b].OvertakesSuffered++;
                    }
                    clearlyAhead[a, b] = true;
                    clearlyAhead[b, a] = false;
                }
            }
        }

        private static bool IsAheadOf(RaceKart a, RaceKart b)
        {
            if (a.Finished && b.Finished)
            {
                return a.FinishTime < b.FinishTime;
            }
            if (a.Finished != b.Finished)
            {
                return a.Finished;
            }
            return a.Progress > b.Progress;
        }

        // 1 = leading. 0 if the kart is not in the race.
        public int PositionOf(RaceKart kart)
        {
            for (int i = 0; i < Standings.Length; i++)
            {
                if (Standings[i] == kart)
                {
                    return i + 1;
                }
            }
            return 0;
        }

        // Edge probe: every kart holds one fixed line, and the karts pass through each other (no kart contacts),
        // so each one feels only the road.
        private void SetUpProbe()
        {
            shuffleGrid = false;
            raceDayForm = 0f;
            RubberBandOff = true;
            for (int i = 0; i < Karts.Length; i++)
            {
                Karts[i].LogLaunches = true;
                if (drivers[i] != null && i < ProbeLanes.Length)
                {
                    drivers[i].ProbeLane = ProbeLanes[i];
                }
                Collider[] mine = Karts[i].GetComponentsInChildren<Collider>();
                for (int j = i + 1; j < Karts.Length; j++)
                {
                    Collider[] theirs = Karts[j].GetComponentsInChildren<Collider>();
                    for (int a = 0; a < mine.Length; a++)
                    {
                        for (int b = 0; b < theirs.Length; b++)
                        {
                            Physics.IgnoreCollision(mine[a], theirs[b]);
                        }
                    }
                }
            }
        }

        // Deals the free grid places (all but the player's) to the bots: shuffled with the race's random numbers,
        // or in profile order when shuffling is off, or slowest first for the overtaking stress test.
        private void PlaceBotsOnGrid()
        {
            int[] order = new int[bots.Length];
            for (int b = 0; b < bots.Length; b++)
            {
                order[b] = SlowBotsInFront ? bots.Length - 1 - b : b;
            }
            if (shuffleGrid && !SlowBotsInFront)
            {
                for (int b = bots.Length - 1; b > 0; b--) // shuffle: swap each place with a random earlier one
                {
                    int other = gridRandom.Next(b + 1);
                    int keep = order[b];
                    order[b] = order[other];
                    order[other] = keep;
                }
            }
            for (int b = 0; b < bots.Length; b++)
            {
                int place = order[b] >= playerSlot ? order[b] + 1 : order[b];
                kartSlots[b + 1] = gridSlots[place];
                Karts[b + 1].Kart.ResetTo(kartSlots[b + 1].position, kartSlots[b + 1].rotation);
                Karts[b + 1].ResetForStart();
            }
        }

        // Retry (Enter): the RaceManager has already put the player back; we put the bots back and forget the race.
        private void OnRestarted()
        {
            RaceRunning = false;
            RaceTime = 0f;
            checkpointsForwarded = 0;
            playerLapsLogged = 0;
            System.Array.Clear(clearlyAhead, 0, clearlyAhead.Length);
            PlaceBotsOnGrid(); // a new grid order for the new race
            for (int i = 0; i < Karts.Length; i++)
            {
                if (!Karts[i].IsPlayer)
                {
                    Karts[i].Kart.InputEnabled = false;
                }
                Karts[i].ResetForStart();
                rubber[i] = 0f;
                rubberTarget[i] = 0f;
                if (drivers[i] != null)
                {
                    drivers[i].NewRace(raceDayForm); // a new race day: new form
                }
            }
        }
    }
}
