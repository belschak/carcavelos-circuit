using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: brings the five bots into the race. Sits on the "Racers" object in Main.unity next to RaceFlow's
    // RaceRoster (Role 4's scene builder adds it). As soon as the roster has set the race up, it spawns one normal
    // Kart (Role 1's Kart.prefab, Role 3's kart model inside) per seat of DriverSwap.Lineup, gives it that driver's kart
    // model (or paints a Rookie copy) and puts a BotDriver
    // on it. The bots register as IRacer, and RaceRoster does the rest for them like for the player: grid slots
    // (bots in front, the player last), input only while Racing, back to the grid on Retry.
    // Every frame it also tells each bot how far it is ahead of the player, for the light rubber band.
    // Race modes (Luc, 16 Nov 2026): bots only race in a mode that has them (RaceModeRules.HasBots): in Time Attack
    // the squad leaves the grid on the start screen and comes back when Race vs Bots is picked again.
    // Finish cinematic: after the player's line, FinishCruise drives the PLAYER's kart on by itself (a lap of honour
    // for the camera in front of it) until Retry or the way back to the menu.
    public class BotSquad : MonoBehaviour
    {
        [SerializeField, Tooltip("RaceFlow's roster on this object: when it is set up, the track and the standings are known")]
        private RaceRoster roster;

        [SerializeField, Tooltip("RaceFlow's RaceManager: the bots first appear behind the track's SpawnPoint")]
        private RaceManager raceManager;

        [SerializeField, Tooltip("The kart each bot drives: Role 1's Driving/Kart.prefab (it has IKartDrivable)")]
        private GameObject kartPrefab;

        [SerializeField, Tooltip("Names, colours and tuning (Visuals/Bots/BotSettings.asset). Empty = the built-in defaults")]
        private BotSettings settings;

        private readonly List<BotDriver> bots = new List<BotDriver>();
        private bool spawned;           // set up once the track is there (bots or not, depending on the mode)
        private bool spawnFailed;       // no prefab or no racing line: do not try again every frame
        private bool keptOut;           // RemoveBots() was called (Play Mode tests): no respawn on the start screen
        private bool driverChanged = true; // a new driver was picked: repaint the player, swap the bots (start screen)
        private BotTrackMap map;        // the racing line, shared by the bots and the finish cruise; null = none
        private FinishCruise cruise;    // drives the player's kart after the line, null = the player drives

        public IReadOnlyList<BotDriver> Bots { get { return bots; } }
        public BotSettings Settings { get { return settings; } } // the online race builds the other players' karts from its kart list
        public bool HasSpawned { get { return spawned; } }
        public bool IsCruising { get { return cruise != null; } } // Play Mode test: the finish autopilot drives

        private void OnEnable()
        {
            if (raceManager != null)
            {
                raceManager.LapFinished += OnPlayerFinished;
                raceManager.Restarted += StopCruise;
            }
            DriverChoice.Changed += OnDriverChanged;
            driverChanged = true;
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.LapFinished -= OnPlayerFinished;
                raceManager.Restarted -= StopCruise;
            }
            DriverChoice.Changed -= OnDriverChanged;
            StopCruise();
        }

        private void Update()
        {
            if (!spawned)
            {
                if (roster == null || raceManager == null || !roster.IsSetUp)
                {
                    return; // the track is still loading
                }
                spawned = true;
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<BotSettings>(); // the defaults written in BotSettings.cs
                }
            }
            FollowMode();
            FeedRubberBand();
            if (cruise != null)
            {
                cruise.Tick(Time.deltaTime);
            }
        }

        // On the start screen the squad follows the picked mode: in or out of the race. Never during a race.
        private void FollowMode()
        {
            if (raceManager.State != RaceState.Ready)
            {
                return;
            }
            if (driverChanged && raceManager.KartTransform != null)
            {
                // Driver select: the player gets the picked driver's kart, and the bots are lined up again (the three
                // drivers not picked + two repainted Rookies, DriverSwap). The old bots go this frame and the new
                // ones come next frame (Destroy finishes at the frame's end).
                driverChanged = false;
                ShowPlayerDriver();
                if (bots.Count > 0)
                {
                    DestroyBots();
                    return;
                }
            }
            bool wanted = RaceModeRules.HasBots(raceManager.Mode) && !keptOut;
            if (wanted && bots.Count == 0 && !spawnFailed)
            {
                SpawnAll();
            }
            else if (!wanted && bots.Count > 0)
            {
                DestroyBots(); // RaceRoster then moves the player to slot 0 (racing alone)
            }
        }

        private void OnDestroy()
        {
            DestroyBots();
        }

        // The racing line for the bots and the finish cruise, built once. Null if the track has none.
        private BotTrackMap EnsureMap()
        {
            if (map == null && roster != null && roster.Progress != null)
            {
                BotTrackMap built = new BotTrackMap(roster.Progress);
                if (built.IsValid)
                {
                    map = built;
                }
            }
            return map;
        }

        // The player's kart takes the picked driver's model; the boost and drift effects move to its wheels and pipes.
        private void ShowPlayerDriver()
        {
            GameObject kart = raceManager.KartTransform.gameObject;
            GameObject model = settings != null ? settings.KartVisual(DriverChoice.Current.KartVisual) : null;
            if (model == null)
            {
                return; // no kart list in BotSettings yet: the player keeps the Rookie's Classic
            }
            Transform slot = kart.transform.Find(KartVisualSwap.SlotName);
            Transform before = slot != null ? KartVisualSwap.CurrentModel(slot) : null;
            GameObject now = KartVisualSwap.Apply(kart, model);
            if (now != null && (before == null || before.gameObject != now))
            {
                KartEffects effects = kart.GetComponentInChildren<KartEffects>(true);
                if (effects != null)
                {
                    effects.Rebind();
                }
            }
        }

        private void OnDriverChanged(int driver)
        {
            driverChanged = true;
        }

        private void SpawnAll()
        {
            if (kartPrefab == null || EnsureMap() == null)
            {
                spawnFailed = true;
                Debug.LogWarning("BotSquad: no kart prefab or no usable racing line (ITrackProgress), so no bots in this race.");
                return;
            }
            Transform spawn = raceManager.Track.SpawnPoint;
            List<DriverSwap.Seat> seats = DriverSwap.Lineup(DriverChoice.Index, settings);
            for (int i = 0; i < seats.Count; i++)
            {
                BotProfile profile = seats[i].Profile;
                // First appearance on the bot's own grid slot (bots fill the front slots), so no two karts ever
                // overlap; RaceRoster then lays out the whole grid again with the player in the last slot.
                Vector3 slot = spawn.position + spawn.rotation * RacerGrid.OffsetOf(i, 2f, 3f);
                GameObject kart = Instantiate(kartPrefab, slot, spawn.rotation);
                kart.name = "Bot " + profile.displayName;
                KartVisualSwap.Apply(kart, settings.KartVisual(seats[i].KartVisual)); // same frame: before any Start
                if (seats[i].Repaint)
                {
                    BotKartPaint.Paint(kart, profile.colour); // the two Rookie copies; the roster karts keep their paint
                }
                BotDriver driver = kart.AddComponent<BotDriver>(); // registers itself (OnEnable)
                driver.SetUp(profile, settings, map);
                bots.Add(driver);
            }
        }

        // Rubber band input: metres this bot is ahead of the player (laps count), 0 while either has no progress yet.
        private void FeedRubberBand()
        {
            LapProgress player = roster.Player != null ? roster.Standings.ProgressOf(roster.Player) : null;
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] == null)
                {
                    continue;
                }
                LapProgress mine = roster.Standings.ProgressOf(bots[i]);
                bool known = player != null && mine != null && player.HasValue && mine.HasValue;
                bots[i].MetresAheadOfPlayer = known ? mine.Total - player.Total : 0f;
            }
        }

        // The player crossed the line: from now on FinishCruise drives the player's kart (RaceManager keeps its input
        // on while PlayerOnAutopilot is set). Without a racing line the kart simply stops as before.
        private void OnPlayerFinished(float lapSeconds, bool newRecord)
        {
            if (EnsureMap() == null || settings == null || raceManager.KartTransform == null)
            {
                return;
            }
            IKartDrivable drivable = raceManager.KartTransform.GetComponent<IKartDrivable>();
            if (drivable == null)
            {
                return;
            }
            cruise = new FinishCruise(raceManager.KartTransform, raceManager.Kart, map, settings, FinishCruise.DefaultSpeedShare);
            cruise.Begin();
            drivable.InputSource = cruise;
            raceManager.PlayerOnAutopilot = true;
        }

        // Retry, back to the menu, or this object goes away: the keyboard drives the player's kart again.
        private void StopCruise()
        {
            if (cruise == null)
            {
                return;
            }
            cruise = null;
            if (raceManager != null)
            {
                raceManager.PlayerOnAutopilot = false;
                IKartDrivable drivable = raceManager.KartTransform != null ? raceManager.KartTransform.GetComponent<IKartDrivable>() : null;
                if (drivable != null)
                {
                    drivable.InputSource = null;
                }
            }
        }

        // Takes every bot out of the race for good, whatever the mode (Play Mode tests that need an empty road).
        // RaceRoster then puts the player back on slot 0 before the next start.
        public void RemoveBots()
        {
            keptOut = true;
            DestroyBots();
        }

        private void DestroyBots()
        {
            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] != null)
                {
                    Destroy(bots[i].gameObject);
                }
            }
            bots.Clear();
        }
    }
}
