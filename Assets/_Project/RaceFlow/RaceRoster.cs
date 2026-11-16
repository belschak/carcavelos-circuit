using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R8 + R9: RaceFlow owns the race for EVERY racer, not only the player (docs/interfaces.md, Round 2).
    // On the "Racers" object in Main.unity. When RaceManager has set the race up it
    // - makes the player's kart an IRacer (PlayerRacer, IsPlayer = true),
    // - finds the track's progress (Role 2's ITrackProgress, else a line through the checkpoints),
    // - puts every racer on its grid slot at the SpawnPoint (RacerGrid: pairs side by side, bots from the front, the
    //   player LAST; alone = time trial = slot 0, front row left), at set-up, at the race start and on every Retry,
    //   and before the race (Ready) whenever a racer joins or leaves; a bot that registers during a race waits for
    //   the next start or Retry (no teleport mid-race),
    // - switches every bot's input on while Racing and after the player's finish (the bots race on behind the finish
    //   cinematic), off on pause, on the grid and back on the start screen,
    // - keeps the live standings (RaceStandings) that the positions readout and the minimap show.
    // Bots never get the player's checkpoints, timer, medals or items: RaceManager.IsKartCollider only counts the player.
    // Optional dev test bots (RaceSettings > Dev Test Bots, 0 in the game) are spawned here too.
    // Online (M2) the lobby sets PlayerSlotOverride: the player stands on its slot in the room's roster, and the
    // other players' karts (proxies, IsPlayer = false) are treated like bots here (their ResetTo does nothing).
    public class RaceRoster : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Grid spacing and the dev test bot count (RaceFlow/Settings/RaceSettings.asset)")]
        private RaceSettings settings;

        [SerializeField, Tooltip("DEV ONLY: kart the test bots drive (RaceFlow's PlaceholderKart prefab, it implements IKartDrivable)")]
        private GameObject testBotKart;

        // Five colours that stay apart on the dark map tile and on the white road (dev bots only; Role 3 picks the real ones).
        private static readonly Color[] TestBotColours =
        {
            HudColors.Blue, HudColors.Coral, HudColors.Sand, new Color(0.35f, 0.76f, 0.49f, 1f), new Color(0.65f, 0.55f, 0.98f, 1f)
        };

        private readonly RaceStandings standings = new RaceStandings();
        private RacerGrid grid;
        private ITrackProgress progress;
        private PlayerRacer player;
        private bool isSetUp;
        private RaceState lastState = RaceState.Ready;
        private int testBotsSpawned;
        private int racersOnTheGrid = -1; // how many racers the grid was last laid out for
        private int playerSlotOverride = RacerGrid.NoOverride;

        public bool IsSetUp { get { return isSetUp; } }
        public ITrackProgress Progress { get { return progress; } } // null if the track has neither T8 nor checkpoints
        public RaceStandings Standings { get { return standings; } }
        public IRacer Player { get { return player; } }
        public int PlayerPosition { get { return standings.PositionOf(player); } } // 1 = leading, 0 = unknown
        public int RacerCount { get { return standings.Count; } }

        // Online (M2): the grid slot the room gave the player (index in its roster), -1 = off (behind the bots).
        // Set on the start screen, the player moves there at once; else at the next start or rematch.
        // Leaving the Online mode switches it off again.
        public int PlayerSlotOverride
        {
            get { return playerSlotOverride; }
            set
            {
                int wanted = value < 0 ? RacerGrid.NoOverride : value;
                if (wanted == playerSlotOverride)
                {
                    return;
                }
                playerSlotOverride = wanted;
                if (grid != null)
                {
                    grid.PlayerSlotOverride = wanted;
                }
                if (isSetUp && raceManager.State == RaceState.Ready)
                {
                    PutEveryoneOnTheGrid();
                }
            }
        }

        private void OnEnable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted += PutEveryoneOnTheGrid;
                raceManager.ModeChanged += OnModeChanged;
            }
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted -= PutEveryoneOnTheGrid;
                raceManager.ModeChanged -= OnModeChanged;
            }
        }

        // An offline mode never keeps the online grid slot.
        private void OnModeChanged(RaceMode mode)
        {
            if (mode != RaceMode.OnlineRace)
            {
                PlayerSlotOverride = RacerGrid.NoOverride;
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
            // Before that (Ready, nobody drives yet) the grid also follows racers who join or leave, so the player
            // already waits in the last slot when the countdown begins.
            RaceState state = raceManager.State;
            if ((lastState == RaceState.Ready && state == RaceState.Countdown)
                || (state == RaceState.Ready && RacerRegistry.All.Count != racersOnTheGrid))
            {
                PutEveryoneOnTheGrid();
            }
            lastState = state;

            // Drive only while Racing, never while paused. After the player's finish the bots race on, so the
            // finish cinematic (camera in front of the player) shows a living race, not karts frozen on the road.
            bool mayDrive = (state == RaceState.Racing || state == RaceState.Results) && !raceManager.IsPaused;
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
            // The finish cinematic shows this place in the middle of the screen (race modes, 16 Nov 2026).
            raceManager.PlayerPlace = PlayerPosition;
            raceManager.RacersInRace = RacerCount;
        }

        private void SetUp()
        {
            isSetUp = true;
            grid = new RacerGrid(settings.GridSidewaysMetres, settings.GridRowMetres);
            grid.PlayerSlotOverride = playerSlotOverride; // the lobby may have set it before we were set up
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
            PutEveryoneOnTheGrid();
            lastState = raceManager.State;
        }

        // Every racer onto its slot, standing still. RaceManager has just put the player on the SpawnPoint (set-up,
        // Retry); the SpawnPoint is the middle of the front row, so we move the player on to its slot: slot 0 alone
        // (time trial), else the last slot behind every bot. The player's input stays RaceManager's business; the bots'
        // input goes off. Also the start of new standings.
        private void PutEveryoneOnTheGrid()
        {
            if (!isSetUp)
            {
                return;
            }
            IReadOnlyList<IRacer> racers = RacerRegistry.All;
            grid.KeepOnly(racers);
            racersOnTheGrid = racers.Count;
            Transform spawn = raceManager.Track.SpawnPoint;
            // Bots first: the player's slot depends on which slots the bots hold.
            for (int i = 0; i < racers.Count; i++)
            {
                if (!racers[i].IsPlayer)
                {
                    grid.Assign(racers[i]);
                }
            }
            for (int i = 0; i < racers.Count; i++)
            {
                int slot = grid.Assign(racers[i]);
                IKartState kart = racers[i].Kart;
                if (kart == null)
                {
                    continue;
                }
                kart.ResetTo(grid.SlotPosition(slot, spawn.position, spawn.rotation), spawn.rotation);
                if (!racers[i].IsPlayer)
                {
                    kart.InputEnabled = false;
                }
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
