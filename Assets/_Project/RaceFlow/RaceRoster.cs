using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R8 + R9: RaceFlow owns the race for EVERY racer, not only the player (docs/interfaces.md, Round 2).
    // On the "Racers" object in Main.unity. When RaceManager has set the race up it
    // - makes the player's kart an IRacer (PlayerRacer, IsPlayer = true),
    // - finds the track's progress (Role 2's ITrackProgress, else a line through the checkpoints),
    // - puts every registered bot on its grid slot behind the SpawnPoint (RacerGrid), at set-up, at the race start
    //   and on every Retry; a bot that registers later waits for the next of those (no teleport mid-race),
    // - switches every bot's input on only while Racing (exactly like the player's), off on pause and at the finish,
    // - keeps the live standings (RaceStandings) that the positions readout and the minimap show.
    // Bots never get the player's checkpoints, timer, medals or items: RaceManager.IsKartCollider only counts the player.
    // Optional dev test bots (RaceSettings > Dev Test Bots, 0 in the game) are spawned here too.
    public class RaceRoster : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Grid spacing and the dev test bot count (RaceFlow/Settings/RaceSettings.asset)")]
        private RaceSettings settings;

        [SerializeField, Tooltip("DEV ONLY: kart the test bots drive (RaceFlow's PlaceholderKart prefab, it implements IKartDrivable)")]
        private GameObject testBotKart;

        private static readonly Color[] TestBotColours = { HudColors.Blue, HudColors.Coral, HudColors.Sand, HudColors.White, HudColors.BlueDeep };

        private readonly RaceStandings standings = new RaceStandings();
        private RacerGrid grid;
        private ITrackProgress progress;
        private PlayerRacer player;
        private bool isSetUp;
        private RaceState lastState = RaceState.Ready;
        private int testBotsSpawned;

        public bool IsSetUp { get { return isSetUp; } }
        public ITrackProgress Progress { get { return progress; } } // null if the track has neither T8 nor checkpoints
        public RaceStandings Standings { get { return standings; } }
        public IRacer Player { get { return player; } }
        public int PlayerPosition { get { return standings.PositionOf(player); } } // 1 = leading, 0 = unknown
        public int RacerCount { get { return standings.Count; } }

        private void OnEnable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted += PutBotsOnTheGrid;
            }
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted -= PutBotsOnTheGrid;
            }
        }

        private void Update()
        {
            if (!isSetUp)
            {
                if (raceManager == null || settings == null || !raceManager.IsSetUp)
                {
                    return; // RaceBootstrap is still loading the track
                }
                SetUp();
            }

            // The race starts (first countdown after the start screen): everyone onto the grid.
            RaceState state = raceManager.State;
            if (lastState == RaceState.Ready && state == RaceState.Countdown)
            {
                PutBotsOnTheGrid();
            }
            lastState = state;

            // Same rule as the player's kart: drive only while Racing, never while paused.
            bool mayDrive = state == RaceState.Racing && !raceManager.IsPaused;
            IReadOnlyList<IRacer> racers = RacerRegistry.All;
            for (int i = 0; i < racers.Count; i++)
            {
                if (!racers[i].IsPlayer && racers[i].Kart != null)
                {
                    racers[i].Kart.InputEnabled = mayDrive;
                }
            }

            // After the finish the standings freeze, so the results can show the place the player finished in.
            if (state != RaceState.Results)
            {
                standings.Update(racers, progress);
            }
        }

        private void SetUp()
        {
            isSetUp = true;
            grid = new RacerGrid(settings.GridSidewaysMetres, settings.GridRowMetres);
            progress = TrackProgressSource.For(raceManager.Track, raceManager.TrackPoints);
            if (progress == null)
            {
                Debug.LogWarning("RaceRoster: the track has no TrackProgress and fewer than 3 checkpoints, so there are no positions and no minimap.");
            }

            GameObject kart = raceManager.KartTransform.gameObject;
            player = kart.GetComponent<PlayerRacer>();
            if (player == null)
            {
                player = kart.AddComponent<PlayerRacer>(); // registers itself (OnEnable)
            }
            SpawnTestBots(settings.DevTestBots);
            PutBotsOnTheGrid();
            lastState = raceManager.State;
        }

        // Every bot onto its slot, standing still, input off. The player is put on slot 0 by RaceManager itself
        // (the SpawnPoint), so the time trial starts exactly as before. Also the start of new standings.
        private void PutBotsOnTheGrid()
        {
            if (!isSetUp)
            {
                return;
            }
            IReadOnlyList<IRacer> racers = RacerRegistry.All;
            grid.KeepOnly(racers);
            Transform spawn = raceManager.Track.SpawnPoint;
            for (int i = 0; i < racers.Count; i++)
            {
                int slot = grid.Assign(racers[i]);
                IKartState kart = racers[i].Kart;
                if (racers[i].IsPlayer || kart == null)
                {
                    continue;
                }
                kart.ResetTo(grid.SlotPosition(slot, spawn.position, spawn.rotation), spawn.rotation);
                kart.InputEnabled = false;
            }
            standings.Reset();
        }

        // DEV ONLY: spawns simple line-following bots (also used by the Play Mode test). They go straight onto their slots.
        public void SpawnTestBots(int count)
        {
            if (!isSetUp || count <= 0)
            {
                return;
            }
            if (testBotKart == null || testBotKart.GetComponent<IKartDrivable>() == null)
            {
                Debug.LogWarning("RaceRoster: no test bot kart with IKartDrivable assigned, no test bots.");
                return;
            }
            Transform spawn = raceManager.Track.SpawnPoint;
            for (int i = 0; i < count; i++)
            {
                testBotsSpawned++;
                GameObject bot = Instantiate(testBotKart, spawn.position, spawn.rotation);
                bot.name = "TestBot " + testBotsSpawned;
                TestBot driver = bot.AddComponent<TestBot>(); // registers itself (OnEnable)
                driver.SetUp(progress, "Bot " + testBotsSpawned, TestBotColours[(testBotsSpawned - 1) % TestBotColours.Length]);
                int slot = grid.Assign(driver);
                driver.Kart.ResetTo(grid.SlotPosition(slot, spawn.position, spawn.rotation), spawn.rotation);
                driver.Kart.InputEnabled = false;
            }
        }

        // Where a racer's slot is right now (Play Mode test). False if it has no slot yet.
        public bool TryGetSlotPosition(IRacer racer, out Vector3 position)
        {
            position = Vector3.zero;
            if (!isSetUp || grid.SlotOf(racer) < 0)
            {
                return false;
            }
            Transform spawn = raceManager.Track.SpawnPoint;
            position = grid.SlotPosition(grid.SlotOf(racer), spawn.position, spawn.rotation);
            return true;
        }
    }
}
