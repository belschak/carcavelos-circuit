using System;
using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.Serialization;

namespace CarcavelosCircuit.RaceFlow
{
    // Item boxes (task R7, decision 2026-11-10). On the "Items" object in Main.unity.
    // When the race is set up it puts rows of azulejo boxes across the road after some checkpoints (ItemBoxLayout).
    // Drive through a box: ItemOdds picks an item (better ones when you are behind your best lap), the HUD slot
    // rolls (ItemSlot), then E uses it. The items are Luc's four designs (ItemKind). Every speed-up reaches the
    // kart through Shared/IKartBoost:
    //   Espresso and Carcavelos Wave = one boost.
    //   Seagull Delivery = lifebuoy rings on the road ahead (RingPath, SeagullRing), each ring = a short boost.
    //   Dean's List = a golden boost, plus a shield (BumpShield, DeansHalo) that gives the speed back after a bump.
    // Retry empties the slot, clears rings and shield and puts every box back; recovery (R) keeps your item.
    // Like the rest of RaceFlow it only talks to the kart through its contracts and to the track through checkpoints
    // and its racing line (ITrackProgress, for the Seagull rings).
    public class ItemSystem : MonoBehaviour
    {
        [SerializeField]
        private RaceManager raceManager;

        [SerializeField, Tooltip("Item tuning (RaceFlow/Settings/ItemSettings.asset)")]
        private ItemSettings settings;

        [SerializeField, Tooltip("URP Lit material for the boxes (RaceFlow/Materials/ItemBox.mat); the tile picture is added in code")]
        private Material boxMaterial;

        [SerializeField, Tooltip("2D AudioSource for the item sounds")]
        private AudioSource source;

        [SerializeField, Tooltip("Second 2D AudioSource for the ring chime: its pitch rises with every ring in a row")]
        private AudioSource ringSource;

        [SerializeField, Tooltip("A box bursts into tile shards")]
        private AudioClip boxBreak;

        [SerializeField, Tooltip("One click of the spinning slot (plays once per icon change)")]
        private AudioClip rollTick;

        [SerializeField, Tooltip("The slot stops: the item is ready")]
        private AudioClip itemReady;

        [SerializeField, FormerlySerializedAs("bicaUse"), Tooltip("Espresso used: clink, steam hiss, kick")]
        private AudioClip espressoUse;

        [SerializeField, FormerlySerializedAs("ondaUse"), Tooltip("Carcavelos Wave used: a big wave")]
        private AudioClip waveUse;

        [SerializeField, Tooltip("Seagull Delivery used: the courier gull cries and flaps")]
        private AudioClip seagullCall;

        [SerializeField, Tooltip("A lifebuoy ring driven through: whoosh and chime")]
        private AudioClip ringCollect;

        [SerializeField, Tooltip("Dean's List used: golden fanfare")]
        private AudioClip deansOn;

        [SerializeField, Tooltip("The shield took a bump")]
        private AudioClip shieldBlock;

        [SerializeField, Tooltip("The shield ran out without a bump")]
        private AudioClip shieldPop;

        // ---- What the HUD reads ----
        public ItemSlot Slot { get { return slot; } }
        public int BoxCount { get { return boxes.Count; } }
        public int PlannedBoxes { get; private set; }          // rows x boxes per row + wave spots: BoxCount should match
        public int StopCount { get { return stops.Count; } }   // rows first, then the wave boxes (ItemRowTour)
        public ItemStop StopAt(int index) { return stops[index]; }
        public bool ShieldActive { get { return shield.IsActive; } }
        public float ShieldSecondsLeft { get { return shield.SecondsLeft; } }
        public int RingsWaiting { get { return CountRingsWaiting(); } } // rings still on the road
        public event Action<ItemKind> ItemReady;     // the roll landed
        public event Action<ItemKind> ItemUsed;      // E was pressed and the item started
        public event Action<int, int> RingCollected; // rings driven through so far, rings dropped
        public event Action ShieldBlocked;           // Dean's List took a bump and gave the speed back

        private readonly ItemSlot slot = new ItemSlot();
        private readonly List<ItemBox> boxes = new List<ItemBox>();
        private static readonly Color WaveTurquoise = new Color(0.08f, 0.80f, 0.86f, 1f); // Luc's wave (UI/ItemIcon.cs)
        private readonly List<SeagullRing> rings = new List<SeagullRing>();
        private readonly BumpShield shield = new BumpShield();
        private Material liveMaterial;    // copy of boxMaterial with the tile texture
        private Material waveMaterial;    // the same, tinted turquoise: the wave boxes (WaveBoxSpot)
        private Texture2D tileTexture;
        private Material ringRed;
        private Material ringWhite;
        private Material gold;
        private DeansHalo halo;
        private IKartBoost kartBoost;     // null if the kart cannot be boosted from outside
        private bool spawned;
        private bool boxesShown = true;   // false in a mode without items (RaceModeRules.HasItems): the boxes are switched off
        private const int NotItems = ~(1 << 2); // every layer except Ignore Raycast (the boxes and rings themselves)
        private readonly List<ItemStop> stops = new List<ItemStop>();          // every row and wave box, for the log and ?test=itemrows
        private readonly List<Vector3> placedPositions = new List<Vector3>(); // where the boxes stand (no two overlap)
        private readonly RaycastHit[] rayHits = new RaycastHit[16];
        private readonly Collider[] overlapHits = new Collider[8];
        private bool logPlacements;       // ?test=itemrows or ?test=itemlog: one Console line per box, hit and use
        private int resumeFrame = -1;     // the key that ends a pause must not also use the item
        private int ringsDropped;         // rings of the last Seagull Delivery ...
        private int ringsCollected;       // ... and how many of them you drove through

        private void Update()
        {
            if (raceManager == null || settings == null || !raceManager.IsSetUp)
            {
                return;
            }
            if (!spawned)
            {
                SpawnBoxes();
            }
            // Every mode has items now, online too (RaceModeRules.HasItems); the switch stays for a mode without them.
            bool wantBoxes = RaceModeRules.HasItems(raceManager.Mode);
            if (wantBoxes != boxesShown)
            {
                ShowBoxes(wantBoxes);
            }
            if (!wantBoxes || raceManager.IsPaused)
            {
                return;
            }

            int stepsBefore = slot.RollSteps;
            bool landed = slot.Tick(Time.deltaTime);
            if (slot.IsRolling && slot.RollSteps != stepsBefore)
            {
                Play(rollTick, 0.7f);
            }
            if (landed)
            {
                Play(itemReady, 1f);
                if (ItemReady != null) ItemReady(slot.Held);
            }

            if (RaceKeys.UseItemPressed() && Time.frameCount != resumeFrame)
            {
                UseItem();
            }
            UpdateShield();
        }

        // Uses the item in the slot (E, or the phone's touch button which sends E). Public for the Play Mode test.
        // Only while racing with input on: not on the start screen, in the countdown or after the finish line.
        public bool UseItem()
        {
            if (!raceManager.IsSetUp || raceManager.State != RaceState.Racing || !raceManager.Kart.InputEnabled
                || !RaceModeRules.HasItems(raceManager.Mode))
            {
                return false;
            }
            ItemKind used = slot.TryUse();
            if (used == ItemKind.None)
            {
                return false;
            }
            float boostSeconds = settings.BoostSecondsFor(used);
            if (kartBoost != null && boostSeconds > 0f)
            {
                kartBoost.AddBoost(boostSeconds, settings.BoostMultiplierFor(used));
            }
            if (used == ItemKind.Espresso)
            {
                Play(espressoUse, 1f);
            }
            else if (used == ItemKind.CarcavelosWave)
            {
                Play(waveUse, 1f);
            }
            else if (used == ItemKind.SeagullDelivery)
            {
                Play(seagullCall, 1f);
                DropRings();
            }
            else if (used == ItemKind.DeansList)
            {
                Play(deansOn, 1f);
                shield.Activate(settings.ShieldSeconds);
                if (halo != null) halo.Show(settings.ShieldSeconds);
            }
            if (logPlacements)
            {
                Debug.Log("ItemSystem: used " + used + " (" + raceManager.Mode + "), boost " + boostSeconds + " s, kart boost "
                    + (kartBoost != null ? "on" : "missing") + ".");
            }
            if (ItemUsed != null) ItemUsed(used);
            return true;
        }

        // Called by ItemBox when the kart drives through it.
        public void OnBoxHit(ItemBox box)
        {
            if (raceManager.State != RaceState.Racing || !box.IsAvailable || !RaceModeRules.HasItems(raceManager.Mode))
            {
                return;
            }
            box.Break(settings.RespawnSeconds);
            Play(boxBreak, 1f);
            int[] weights = ItemOdds.WeightsFor(raceManager.HasSplitDelta, raceManager.LastSplitDelta,
                settings.LevelSeconds, settings.FarBehindSeconds);
            ItemKind item = ItemOdds.PickForBox(box.GivesWave, weights, UnityEngine.Random.value);
            bool rolls = slot.TryStartRoll(item, settings.RollSeconds); // false while the slot is busy: the box still breaks, no item
            if (logPlacements)
            {
                Debug.Log("ItemSystem: box " + box.name + " hit (" + raceManager.Mode + "), " + (rolls ? "rolling " + item : "slot busy") + ".");
            }
        }

        // Called by SeagullRing when the kart drives through it: a short boost; the chime climbs with every ring.
        public void OnRingHit(SeagullRing ring)
        {
            if (raceManager.State != RaceState.Racing)
            {
                return;
            }
            ringsCollected++;
            if (kartBoost != null)
            {
                kartBoost.AddBoost(settings.RingBoostSeconds, settings.RingBoostMultiplier);
            }
            if (ringSource != null && ringCollect != null)
            {
                ringSource.pitch = 1f + 0.12f * (ringsCollected - 1); // each ring a step higher
                ringSource.PlayOneShot(ringCollect, SoundLevels.Effects);
            }
            if (RingCollected != null) RingCollected(ringsCollected, ringsDropped);
        }

        // For ItemBox and SeagullRing: does this collider belong to our kart? Same rule as the checkpoints.
        public bool IsKart(Collider other)
        {
            return raceManager.IsKartCollider(other);
        }

        // For ItemBox: the shards fly along with the kart.
        public Vector3 KartForward()
        {
            Transform kart = raceManager.KartTransform;
            return kart == null ? Vector3.zero : kart.forward;
        }

        // For the Play Mode test.
        public ItemBox BoxAt(int index)
        {
            return boxes[index];
        }

        // For the Play Mode test: the rings of the last Seagull Delivery (also the ones already driven through).
        public int RingCount { get { return rings.Count; } }

        public SeagullRing RingAt(int index)
        {
            return rings[index];
        }

        private void OnEnable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted += OnRestarted;
                raceManager.Resumed += OnResumed;
                raceManager.Recovered += OnRecovered;
                raceManager.ModeChanged += OnModeChanged;
            }
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted -= OnRestarted;
                raceManager.Resumed -= OnResumed;
                raceManager.Recovered -= OnRecovered;
                raceManager.ModeChanged -= OnModeChanged;
            }
        }

        // Into or out of the online room: nothing from the last race carries over (slot, rings, shield, broken boxes).
        private void OnModeChanged(RaceMode mode)
        {
            OnRestarted();
        }

        private void OnDestroy()
        {
            // Runtime-made textures and materials are not cleaned up by Unity on their own.
            if (liveMaterial != null) Destroy(liveMaterial);
            if (waveMaterial != null) Destroy(waveMaterial);
            if (tileTexture != null) Destroy(tileTexture);
            if (ringRed != null) Destroy(ringRed);
            if (ringWhite != null) Destroy(ringWhite);
            if (gold != null) Destroy(gold);
        }

        private void OnRestarted()
        {
            slot.Clear();
            shield.Clear();
            if (halo != null) halo.Hide();
            ClearRings();
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i].ResetBox();
            }
        }

        // Switches every box off (online race) or back on, whole and ready. Off also empties the slot and clears
        // rings and shield, so nothing from an offline lap carries into the online room.
        private void ShowBoxes(bool shown)
        {
            boxesShown = shown;
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i].gameObject.SetActive(shown);
                if (shown)
                {
                    boxes[i].ResetBox();
                }
            }
            if (!shown)
            {
                slot.Clear();
                shield.Clear();
                if (halo != null) halo.Hide();
                ClearRings();
            }
        }

        private void OnResumed()
        {
            resumeFrame = Time.frameCount;
        }

        // The kart was put back with speed 0: the shield must not mistake that for a bump.
        private void OnRecovered()
        {
            shield.NoteTeleport();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volume * SoundLevels.Effects);
            }
        }

        // Dean's List: watch the speed for a bump while racing (BumpShield has the rule).
        private void UpdateShield()
        {
            if (!shield.IsActive || raceManager.State != RaceState.Racing)
            {
                return;
            }
            BumpShield.Result result = shield.Tick(Time.deltaTime, raceManager.Kart.Speed,
                settings.BumpMinDrop, settings.BumpDropShare);
            if (result == BumpShield.Result.Blocked)
            {
                if (kartBoost != null)
                {
                    kartBoost.AddBoost(settings.ShieldRefundSeconds, settings.ShieldRefundMultiplier);
                }
                Play(shieldBlock, 1f);
                if (halo != null) halo.Burst();
                if (ShieldBlocked != null) ShieldBlocked();
            }
            else if (result == BumpShield.Result.Expired)
            {
                Play(shieldPop, 0.8f);
                if (halo != null) halo.Burst();
            }
        }

        // Seagull Delivery: the rings stand in a line ahead of the kart (RoadAhead, RingPath).
        private void DropRings()
        {
            ClearRings();
            int count = Mathf.Max(1, settings.RingCount);
            float[] distances = new float[count];
            for (int i = 0; i < count; i++)
            {
                distances[i] = settings.RingFirstMetres + i * settings.RingSpacingMetres;
            }
            bool onTrackLine;
            Vector3[] path = RoadAhead(distances[count - 1] + RingPath.StepMetres, out onTrackLine);
            Vector3[] positions = new Vector3[count];
            Vector3[] facings = new Vector3[count];
            RingPath.Place(path, distances, positions, facings);

            ringsDropped = 0;
            ringsCollected = 0;
            for (int i = 0; i < count; i++)
            {
                if (TryPlaceRing(positions[i], facings[i], ringsDropped, onTrackLine))
                {
                    ringsDropped++;
                }
            }
        }

        // Where the rings go: along the track's own racing line when it has one (T8) and the kart is on it, so they
        // follow the bends, the hall and the stairs (on the V15 line a straight line put half of them off the road,
        // 2026-11-11). Otherwise straight ahead of the kart's nose, where the player is looking.
        // (First version went kart -> next checkpoint -> the one after; in the web build that line cut the bend
        // and put the rings on the grass, 2026-11-10. A ring that would stand in a wall is skipped.)
        private Vector3[] RoadAhead(float metres, out bool onTrackLine)
        {
            Vector3 kart = raceManager.KartTransform.position;
            Vector3 forward = raceManager.KartTransform.forward;
            ITrackProgress line = TrackProgressSource.OwnOf(raceManager.Track); // never the checkpoint fallback
            Vector3[] alongTrack = RingPath.AlongTrack(line, kart, forward, metres);
            onTrackLine = alongTrack != null;
            if (onTrackLine)
            {
                return alongTrack;
            }
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            return new Vector3[] { kart, kart + forward.normalized * 100f };
        }

        // Like a box: only where there is ground below and no wall inside the ring.
        // On the racing line the spot is the road surface itself: the ray starts just 2 m above it (under the hall
        // and tunnel ceilings) and the ground must be close below the line (no ring over the jump gap).
        private bool TryPlaceRing(Vector3 spot, Vector3 facing, int number, bool onTrackLine)
        {
            RaycastHit hit;
            float above = onTrackLine ? 2f : 4f;
            if (!Physics.Raycast(spot + Vector3.up * above, Vector3.down, out hit, 14f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false; // off the edge of the world
            }
            if (onTrackLine && !RingPath.IsGroundNearLine(spot.y, hit.point.y))
            {
                return false; // a gap under the line (the jump): nobody drives through a ring up there
            }
            Vector3 centre = hit.point + Vector3.up * (settings.RingRadius + 0.15f);
            int notItems = ~(1 << 2); // everything except the Ignore Raycast layer (boxes, rings)
            if (Physics.CheckSphere(centre, settings.RingRadius * 0.5f, notItems, QueryTriggerInteraction.Ignore))
            {
                return false; // inside a wall, a tree or a building
            }
            GameObject go = new GameObject("SeagullRing " + number);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(facing, Vector3.up));
            SeagullRing ring = go.AddComponent<SeagullRing>();
            ring.SetUp(this, ringRed, ringWhite, settings.RingRadius, 0.25f + number * 0.15f, settings.RingLifeSeconds,
                settings.RingLook);
            rings.Add(ring);
            return true;
        }

        private int CountRingsWaiting()
        {
            int waiting = 0;
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] != null && !rings[i].IsCollected)
                {
                    waiting++;
                }
            }
            return waiting;
        }

        private void ClearRings()
        {
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] != null)
                {
                    Destroy(rings[i].gameObject);
                }
            }
            rings.Clear();
        }

        // Once, when the race is set up: one row of boxes after checkpoint 0, then after every Nth checkpoint.
        private void SpawnBoxes()
        {
            spawned = true;
            kartBoost = raceManager.KartTransform.GetComponent<IKartBoost>();
            if (kartBoost == null)
            {
                Debug.Log("ItemSystem: this kart has no IKartBoost (docs/interfaces.md), so items show up but give no speed.");
            }

            tileTexture = ItemBoxTexture.Create();
            liveMaterial = boxMaterial != null ? new Material(boxMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            liveMaterial.name = "ItemBox (live)";
            liveMaterial.SetTexture("_BaseMap", tileTexture);
            liveMaterial.SetTexture("_EmissionMap", tileTexture);
            liveMaterial.SetColor("_EmissionColor", new Color(0.45f, 0.45f, 0.45f, 1f)); // glows a little: easy to spot in shade
            liveMaterial.EnableKeyword("_EMISSION");

            ringRed = PlainColour("Ring red", new Color(0.86f, 0.12f, 0.10f, 1f), 0.25f);
            ringWhite = PlainColour("Ring white", new Color(0.97f, 0.96f, 0.93f, 1f), 0.2f);
            gold = PlainColour("Dean's List gold", new Color(1f, 0.76f, 0.15f, 1f), 0.6f);
            if (settings.CoinHalo) // V34: off when Role 3's ItemFx draws the laurel wreath; halo == null is handled
            {
                halo = new GameObject("DeansHalo").AddComponent<DeansHalo>();
                halo.transform.SetParent(transform, false);
                halo.SetUp(raceManager.KartTransform, gold);
            }
            // V19 wave boxes are turquoise copies of the box material.
            waveMaterial = new Material(liveMaterial);
            waveMaterial.name = "ItemBox wave (live)";
            waveMaterial.SetColor("_BaseColor", WaveTurquoise);
            waveMaterial.SetColor("_EmissionColor", WaveTurquoise * 0.45f);

            ItemTestFlags flags = ItemTestFlags.Parse(Application.absoluteURL);
            logPlacements = flags.Log;

            // Package C (17.11.2026): on a track with its own racing line the rows stand on that line (ItemBoxLayout),
            // so they follow the bends and the road height (hall, tunnel, stairs) and keep out of the jump landing.
            ITrackProgress line = TrackProgressSource.OwnOf(raceManager.Track);
            Vector3[] points = raceManager.TrackPoints;
            Vector3[] forwards = raceManager.TrackForwards;
            int rows = 0;
            int planned = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (!ItemBoxLayout.HasRow(i, settings.RowEveryCheckpoints))
                {
                    continue;
                }
                Vector3 centre;
                Vector3 direction;
                float lineHeight;
                bool onLine = line != null;
                if (onLine)
                {
                    float checkpoint = line.DistanceAlongLap(points[i]);
                    float gap = Wrap(line.DistanceAlongLap(points[(i + 1) % points.Length]) - checkpoint, line.LapLength);
                    float along = ItemBoxLayout.RowDistance(checkpoint, gap, settings.RowAheadMetres);
                    along = ItemBoxLayout.OutOfJumpLanding(checkpoint, along, gap, d => line.PointAt(d).y);
                    centre = line.PointAt(along);
                    direction = line.DirectionAt(along);
                    lineHeight = centre.y;
                }
                else
                {
                    // No racing line (a test track): the old rule, straight ahead along the checkpoint's road direction.
                    Vector3 from = points[i];
                    Vector3 to = points[(i + 1) % points.Length];
                    Vector3 roadForward = i < forwards.Length ? forwards[i] : Vector3.zero;
                    direction = ItemBoxLayout.RowDirection(from, to, roadForward);
                    centre = ItemBoxLayout.RowCentre(from, to, direction, settings.RowAheadMetres);
                    lineHeight = from.y;
                }
                Vector3[] spots = ItemBoxLayout.RowPositions(centre, direction, settings.BoxesPerRow, settings.BoxSpacing);
                int before = boxes.Count;
                string row = "row " + (stops.Count + 1) + " (after checkpoint " + i + ")";
                for (int b = 0; b < spots.Length; b++)
                {
                    planned++;
                    PlaceBox(spots[b], centre, direction, lineHeight, onLine, rows * 10 + b, false, row + " box " + (b + 1));
                }
                stops.Add(new ItemStop(row, centre, direction, boxes.Count - before, spots.Length));
                if (boxes.Count > before)
                {
                    rows++;
                }
            }
            // V19: one box on every WaveBoxSpot the track scene has (none = only the rows). The spot's own height is
            // the road there (CampusWorldBuilder put it on the lane by raycast).
            WaveBoxSpot[] waveSpots = FindObjectsByType<WaveBoxSpot>(FindObjectsSortMode.None);
            Array.Sort(waveSpots, (a, b) => string.CompareOrdinal(a.name, b.name)); // same order on every load
            int waves = 0;
            for (int k = 0; k < waveSpots.Length; k++)
            {
                planned++;
                Vector3 spot = waveSpots[k].transform.position;
                Vector3 facing = Flat(waveSpots[k].transform.forward);
                string label = "wave box " + (k + 1) + " (" + waveSpots[k].name + ")";
                int before = boxes.Count;
                if (PlaceBox(spot, spot, facing, spot.y, true, 900 + k, true, label))
                {
                    waves++;
                }
                stops.Add(new ItemStop(label, spot, facing, boxes.Count - before, 1));
            }
            PlannedBoxes = planned;
            Debug.Log("ItemSystem: " + boxes.Count + " item boxes in " + rows + " rows, " + waves + " wave boxes. Planned "
                + planned + ", skipped " + (planned - boxes.Count)
                + (line != null ? ", rows on the racing line." : ", rows from the checkpoints (no racing line)."));
            if (flags.Tour)
            {
                gameObject.AddComponent<ItemRowTour>().SetUp(this, raceManager);
            }
        }

        private static float Wrap(float distance, float lap)
        {
            if (lap <= 0f)
            {
                return distance;
            }
            float wrapped = distance % lap;
            return wrapped < 0f ? wrapped + lap : wrapped;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        // A one-colour copy of the box material (same shader, so the Web build already has it), glowing a little.
        private Material PlainColour(string name, Color colour, float glow)
        {
            Material material = new Material(liveMaterial);
            material.name = name;
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_EmissionColor", colour * glow);
            return material;
        }

        // A box only goes on the road: ground close below the line height (never a roof above it or the beach below
        // the jump), facing up, and no wall, guard, pillar or building inside the box. A spot that does not fit is
        // nudged along the road or towards the row's middle (ItemBoxLayout.Nudges). A box that still finds no place
        // is logged with the reason, never skipped silently. givesWave: a wave box (always the Carcavelos Wave,
        // turquoise). knownHeight false = a track without its own line: any height, the one closest to the checkpoint.
        private bool PlaceBox(Vector3 spot, Vector3 rowCentre, Vector3 roadForward, float lineHeight, bool knownHeight,
            int number, bool givesWave, string label)
        {
            Vector3 forward = Flat(roadForward);
            Vector3 towardsMiddle = rowCentre - spot;
            towardsMiddle.y = 0f;
            string reason = "no ground";
            string firstReason = null;
            for (int n = 0; n < ItemBoxLayout.Nudges.Length; n++)
            {
                Vector2 nudge = ItemBoxLayout.Nudges[n];
                Vector3 candidate = spot + forward * nudge.x + towardsMiddle * nudge.y;
                Vector3 ground;
                if (!FindRoad(candidate, lineHeight, knownHeight, out ground, out reason))
                {
                    if (firstReason == null) firstReason = reason;
                    continue;
                }
                Vector3 position = ground + Vector3.up * settings.HoverHeight;
                Collider wall = FirstBlocker(position, settings.BoxSize * 0.6f);
                if (wall != null)
                {
                    reason = "blocked by " + wall.name;
                    if (firstReason == null) firstReason = reason;
                    continue;
                }
                if (!ItemBoxLayout.IsClearOfBoxes(position, placedPositions, settings.BoxSize * 1.5f))
                {
                    reason = "too close to another box";
                    if (firstReason == null) firstReason = reason;
                    continue;
                }
                if (n > 0)
                {
                    Debug.Log("ItemSystem: " + label + " moved " + nudge.x + " m along the road and " + nudge.y
                        + " of the way to the middle (" + firstReason + "), now at " + Short(position) + ".");
                }
                else if (logPlacements)
                {
                    Debug.Log("ItemSystem: " + label + " at " + Short(position) + ", road " + (ground.y - lineHeight).ToString("0.00")
                        + " m from the line height.");
                }
                CreateBox(position, number, givesWave);
                return true;
            }
            Debug.LogWarning("ItemSystem: skipped " + label + " at " + Short(spot) + ": " + (firstReason ?? reason) + ".");
            return false;
        }

        // The road under a spot. With a known height (racing line, wave spot): a ray from just above that height (under
        // the hall and tunnel ceilings), the upward hit closest to it. Without: from 3 m above, any height.
        private bool FindRoad(Vector3 spot, float lineHeight, bool knownHeight, out Vector3 ground, out string reason)
        {
            ground = spot;
            float above = knownHeight ? ItemBoxLayout.LineRayAboveMetres : 3f;
            float length = knownHeight ? above + ItemBoxLayout.MaxGroundOffLineMetres + 0.5f : 12f;
            Vector3 start = new Vector3(spot.x, lineHeight + above, spot.z);
            int count = Physics.RaycastNonAlloc(start, Vector3.down, rayHits, length, NotItems, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                reason = knownHeight ? "no road near the line height (a gap or off the map)" : "off the edge of the world";
                return false;
            }
            float[] heights = new float[count];
            float[] normals = new float[count];
            for (int i = 0; i < count; i++)
            {
                heights[i] = rayHits[i].point.y;
                normals[i] = rayHits[i].normal.y;
            }
            int pick = ItemBoxLayout.PickGround(lineHeight, heights, normals,
                knownHeight ? ItemBoxLayout.MaxGroundOffLineMetres : -1f);
            if (pick < 0)
            {
                reason = "only " + rayHits[0].collider.name + " there (a wall or a roof, not road)";
                return false;
            }
            ground = rayHits[pick].point;
            reason = null;
            return true;
        }

        // The first solid collider inside the box (walls, guards, trees, buildings; never triggers or other boxes).
        private Collider FirstBlocker(Vector3 position, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, overlapHits, NotItems, QueryTriggerInteraction.Ignore);
            return count > 0 ? overlapHits[0] : null;
        }

        private void CreateBox(Vector3 position, int number, bool givesWave)
        {
            GameObject go = new GameObject("ItemBox " + number);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            ItemBox box = go.AddComponent<ItemBox>();
            box.SetUp(this, givesWave ? waveMaterial : liveMaterial, settings.BoxSize, number * 0.7f, givesWave,
                givesWave ? settings.WaveBoxLook : settings.BoxLook, settings.BoxGroundLook, settings.HoverHeight);
            boxes.Add(box);
            placedPositions.Add(position);
        }

        private static string Short(Vector3 v)
        {
            return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";
        }
    }
}
