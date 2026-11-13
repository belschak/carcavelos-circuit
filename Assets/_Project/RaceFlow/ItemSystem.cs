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
        public bool ShieldActive { get { return shield.IsActive; } }
        public float ShieldSecondsLeft { get { return shield.SecondsLeft; } }
        public int RingsWaiting { get { return CountRingsWaiting(); } } // rings still on the road
        public event Action<ItemKind> ItemReady;     // the roll landed
        public event Action<ItemKind> ItemUsed;      // E was pressed and the item started
        public event Action<int, int> RingCollected; // rings driven through so far, rings dropped
        public event Action ShieldBlocked;           // Dean's List took a bump and gave the speed back

        private readonly ItemSlot slot = new ItemSlot();
        private readonly List<ItemBox> boxes = new List<ItemBox>();
        private readonly List<SeagullRing> rings = new List<SeagullRing>();
        private readonly BumpShield shield = new BumpShield();
        private Material liveMaterial;    // copy of boxMaterial with the tile texture
        private Texture2D tileTexture;
        private Material ringRed;
        private Material ringWhite;
        private Material gold;
        private DeansHalo halo;
        private IKartBoost kartBoost;     // null if the kart cannot be boosted from outside
        private bool spawned;
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
            if (raceManager.IsPaused)
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
            if (!raceManager.IsSetUp || raceManager.State != RaceState.Racing || !raceManager.Kart.InputEnabled)
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
            if (ItemUsed != null) ItemUsed(used);
            return true;
        }

        // Called by ItemBox when the kart drives through it.
        public void OnBoxHit(ItemBox box)
        {
            if (raceManager.State != RaceState.Racing || !box.IsAvailable)
            {
                return;
            }
            box.Break(settings.RespawnSeconds);
            Play(boxBreak, 1f);
            int[] weights = ItemOdds.WeightsFor(raceManager.HasSplitDelta, raceManager.LastSplitDelta,
                settings.LevelSeconds, settings.FarBehindSeconds);
            ItemKind item = ItemOdds.Pick(weights, UnityEngine.Random.value);
            slot.TryStartRoll(item, settings.RollSeconds); // false while the slot is busy: the box still breaks, no item
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
            }
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted -= OnRestarted;
                raceManager.Resumed -= OnResumed;
                raceManager.Recovered -= OnRecovered;
            }
        }

        private void OnDestroy()
        {
            // Runtime-made textures and materials are not cleaned up by Unity on their own.
            if (liveMaterial != null) Destroy(liveMaterial);
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
            ring.SetUp(this, ringRed, ringWhite, settings.RingRadius, 0.25f + number * 0.15f, settings.RingLifeSeconds);
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
            halo = new GameObject("DeansHalo").AddComponent<DeansHalo>();
            halo.transform.SetParent(transform, false);
            halo.SetUp(raceManager.KartTransform, gold);

            Vector3[] points = raceManager.TrackPoints;
            Vector3[] forwards = raceManager.TrackForwards;
            int rows = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (!ItemBoxLayout.HasRow(i, settings.RowEveryCheckpoints))
                {
                    continue;
                }
                Vector3 from = points[i];
                Vector3 to = points[(i + 1) % points.Length];
                Vector3 roadForward = i < forwards.Length ? forwards[i] : Vector3.zero;
                Vector3 direction = ItemBoxLayout.RowDirection(from, to, roadForward);
                Vector3 centre = ItemBoxLayout.RowCentre(from, to, direction, settings.RowAheadMetres);
                Vector3[] spots = ItemBoxLayout.RowPositions(centre, direction, settings.BoxesPerRow, settings.BoxSpacing);
                int before = boxes.Count;
                for (int b = 0; b < spots.Length; b++)
                {
                    TryPlaceBox(spots[b], rows * 10 + b);
                }
                if (boxes.Count > before)
                {
                    rows++;
                }
            }
            Debug.Log("ItemSystem: " + boxes.Count + " item boxes in " + rows + " rows.");
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

        // A box only goes where there is ground below it and no wall or building inside it.
        private void TryPlaceBox(Vector3 spot, int number)
        {
            RaycastHit hit;
            Vector3 rayStart = spot + Vector3.up * 3f;
            if (!Physics.Raycast(rayStart, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                return; // off the edge of the world
            }
            Vector3 position = hit.point + Vector3.up * settings.HoverHeight;
            int notBoxes = ~(1 << 2); // everything except the Ignore Raycast layer (other boxes)
            if (Physics.CheckSphere(position, settings.BoxSize * 0.6f, notBoxes, QueryTriggerInteraction.Ignore))
            {
                return; // inside a wall, a tree or a building
            }

            GameObject go = new GameObject("ItemBox " + number);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            ItemBox box = go.AddComponent<ItemBox>();
            box.SetUp(this, liveMaterial, settings.BoxSize, number * 0.7f);
            boxes.Add(box);
        }
    }
}
