using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10: brings the five bots into the race. Sits on the "Racers" object in Main.unity next to RaceFlow's
    // RaceRoster (Role 4's scene builder adds it). As soon as the roster has set the race up, it spawns one normal
    // Kart (Role 1's Kart.prefab, Role 3's kart model inside) per BotSettings profile, paints it and puts a BotDriver
    // on it. The bots register as IRacer, and RaceRoster does the rest for them like for the player: grid slots
    // (bots in front, the player last), input only while Racing, back to the grid on Retry.
    // Every frame it also tells each bot how far it is ahead of the player, for the light rubber band.
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
        private bool spawned;

        public IReadOnlyList<BotDriver> Bots { get { return bots; } }
        public bool HasSpawned { get { return spawned; } }

        private void Update()
        {
            if (!spawned)
            {
                if (roster == null || raceManager == null || !roster.IsSetUp)
                {
                    return; // the track is still loading
                }
                spawned = true;
                SpawnAll();
                return;
            }
            FeedRubberBand();
        }

        private void OnDestroy()
        {
            RemoveBots();
        }

        private void SpawnAll()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BotSettings>(); // the defaults written in BotSettings.cs
            }
            if (kartPrefab == null || roster.Progress == null)
            {
                Debug.LogWarning("BotSquad: no kart prefab or no racing line (ITrackProgress), so no bots in this race.");
                return;
            }
            BotTrackMap map = new BotTrackMap(roster.Progress);
            if (!map.IsValid)
            {
                Debug.LogWarning("BotSquad: the racing line is too short for bots, so no bots in this race.");
                return;
            }
            Transform spawn = raceManager.Track.SpawnPoint;
            for (int i = 0; i < settings.BotCount; i++)
            {
                BotProfile profile = settings.Bot(i);
                if (profile == null)
                {
                    continue;
                }
                // First appearance on the bot's own grid slot (bots fill the front slots), so no two karts ever
                // overlap; RaceRoster then lays out the whole grid again with the player in the last slot.
                Vector3 slot = spawn.position + spawn.rotation * RacerGrid.OffsetOf(i, 2f, 3f);
                GameObject kart = Instantiate(kartPrefab, slot, spawn.rotation);
                kart.name = "Bot " + profile.displayName;
                BotKartPaint.Paint(kart, profile.colour);
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

        // Takes every bot out of the race (tests, and a possible "race alone" option). RaceRoster then puts the
        // player back on slot 0 before the next start.
        public void RemoveBots()
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
