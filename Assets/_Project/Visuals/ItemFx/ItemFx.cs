using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // The item effects round the player's kart (task V34). Sits on the KartEffects prefab next to KartEffects, so it
    // reaches the kart the same way (RaceBootstrap's "Kart Attachments"): no scene or RaceFlow change needed.
    // It only listens to Role 4's ItemSystem (ItemUsed, RingCollected, ShieldBlocked, ShieldActive) and the race
    // (Restarted, Recovered) and draws: WaveRide, EspressoFx, SeagullFx, DeansFx. It never changes speed, items or
    // the race; the boosts still come from ItemSystem through IKartBoost.
    // On a kart that is not the race's player kart (a bot, the demo kart) it stays quiet.
    public class ItemFx : MonoBehaviour
    {
        [SerializeField, Tooltip("Materials and models (Visuals/ItemFx/ItemFxLook.asset, made by Build Kart Effects)")]
        private ItemFxLook look;

        [Header("Lengths, the same as RaceFlow/Settings/ItemSettings.asset (Role 4)")]
        [SerializeField, Tooltip("Carcavelos Wave boost seconds (ItemSettings: 3 s)")]
        private float waveSeconds = 3f;

        [SerializeField, Tooltip("Espresso boost seconds (ItemSettings: 2 s)")]
        private float espressoSeconds = 2f;

        [SerializeField, Tooltip("A jump further than this (m) in one frame is a teleport (retry, recovery): effects are cleared")]
        private float teleportDistance = 8f;

        private WaveRide wave;
        private EspressoFx espresso;
        private SeagullFx seagull;
        private DeansFx deans;
        private ItemSystem items;
        private RaceManager race;
        private bool listening;
        private bool notOurs;      // the race is set up and its kart is another one: stay quiet for good
        private Vector3 lastPosition;
        private float speed;

        // For KartEffects: no exhaust fire while the kart rides the wave (it is water), hotter fire on the Espresso.
        public bool WaveRiding { get { return wave != null && wave.IsRiding; } }
        public bool EspressoKick { get { return espresso != null && espresso.IsActive; } }

        private void Start()
        {
            Setup();
            Listen();
        }

        // Builds the four effects once. Public so the builder can run them in Edit Mode for the check renders.
        public void Setup()
        {
            if (wave != null || look == null)
            {
                if (look == null)
                {
                    Debug.LogWarning("ItemFx: no ItemFxLook, item effects stay off (run Build Kart Effects).");
                    enabled = false;
                }
                return;
            }
            wave = new WaveRide(transform, look);
            espresso = new EspressoFx(transform, look);
            seagull = new SeagullFx(look);
            deans = new DeansFx(transform, look);
            lastPosition = transform.position;
        }

        // Plays an item's effect now. Public for the builder's check renders (ItemSystem calls it through ItemUsed).
        public void Play(ItemKind kind)
        {
            if (wave == null)
            {
                return;
            }
            if (kind == ItemKind.CarcavelosWave)
            {
                wave.Start(waveSeconds);
            }
            else if (kind == ItemKind.Espresso)
            {
                espresso.Start(espressoSeconds);
            }
            else if (kind == ItemKind.SeagullDelivery)
            {
                StartSeagull();
            }
            // Dean's List: DeansFx follows ItemSystem.ShieldActive in Tick, nothing to start here.
        }

        // For the builder: the Seagull with made-up ring spots (in the game they come from ItemSystem).
        public void PlaySeagullAt(Vector3[] spots, Vector3[] facing, float radius, float kartSpeed)
        {
            float[] radii = new float[spots.Length];
            for (int i = 0; i < radii.Length; i++) radii[i] = radius;
            seagull.Start(KartRoot(), spots, facing, radii, spots.Length, kartSpeed);
        }

        // One frame of every effect. shieldOn / shieldLeft come from ItemSystem (the builder passes its own).
        public void Tick(float dt, bool shieldOn, float shieldLeft)
        {
            if (wave == null)
            {
                return;
            }
            if (Vector3.Distance(transform.position, lastPosition) > teleportDistance)
            {
                ClearMoving(); // a teleport: trails and water must not stretch across the map
            }
            if (dt > 0f)
            {
                speed = Mathf.Lerp(speed, Vector3.Distance(transform.position, lastPosition) / dt, 0.2f);
            }
            lastPosition = transform.position;
            wave.Tick(dt, speed);
            espresso.Tick(dt, speed);
            seagull.Tick(dt);
            deans.Tick(dt, shieldOn, shieldLeft);
        }

        public void ShieldBlocked()
        {
            if (deans != null) deans.Block();
        }

        private void LateUpdate()
        {
            if (!listening && !notOurs)
            {
                Listen(); // the race may set itself up after this kart's Start
            }
            if (!listening)
            {
                return;
            }
            Tick(Time.deltaTime, items.ShieldActive, items.ShieldSecondsLeft);
        }

        private void OnDestroy()
        {
            StopListening();
            if (seagull != null) seagull.Destroy();
        }

        // Only the race's own kart gets item effects (ItemSystem has one slot, for the player).
        private void Listen()
        {
            if (listening || wave == null)
            {
                return;
            }
            race = FindAnyObjectByType<RaceManager>();
            items = FindAnyObjectByType<ItemSystem>();
            if (race == null || items == null)
            {
                notOurs = true; // no race or no items in this scene (the effects demo)
                items = null;
                return;
            }
            if (!race.IsSetUp || race.KartTransform == null)
            {
                items = null; // ask again next frame
                return;
            }
            if (race.KartTransform != KartRoot())
            {
                notOurs = true; // a bot or another copy
                items = null;
                return;
            }
            items.ItemUsed += Play;
            items.RingCollected += OnRingCollected;
            items.ShieldBlocked += ShieldBlocked;
            race.Restarted += ClearAll;
            race.Recovered += ClearMoving;
            listening = true;
        }

        private void StopListening()
        {
            if (!listening)
            {
                return;
            }
            if (items != null)
            {
                items.ItemUsed -= Play;
                items.RingCollected -= OnRingCollected;
                items.ShieldBlocked -= ShieldBlocked;
            }
            if (race != null)
            {
                race.Restarted -= ClearAll;
                race.Recovered -= ClearMoving;
            }
            listening = false;
        }

        private Transform KartRoot()
        {
            return transform.parent != null ? transform.parent : transform;
        }

        // Rings ItemSystem just dropped (ItemUsed comes right after DropRings).
        private void StartSeagull()
        {
            if (items == null)
            {
                seagull.Start(KartRoot(), new Vector3[0], new Vector3[0], new float[0], 0, speed);
                return;
            }
            int count = items.RingCount;
            Vector3[] spots = new Vector3[count];
            Vector3[] facing = new Vector3[count];
            float[] radius = new float[count];
            for (int i = 0; i < count; i++)
            {
                SeagullRing ring = items.RingAt(i);
                spots[i] = ring.transform.position;
                facing[i] = ring.transform.forward;
                BoxCollider trigger = ring.GetComponent<BoxCollider>();
                radius[i] = trigger != null ? trigger.size.x / 2.2f : 1.2f; // SeagullRing: trigger = radius x 2.2
            }
            seagull.Start(KartRoot(), spots, facing, radius, count, speed);
        }

        // RingCollected says how many so far; the ring driven through is the one marked collected and not yet seen.
        private void OnRingCollected(int collected, int dropped)
        {
            if (items == null)
            {
                return;
            }
            Vector3 spot = KartRoot().position + Vector3.up;
            float best = float.MaxValue;
            for (int i = 0; i < items.RingCount; i++)
            {
                SeagullRing ring = items.RingAt(i);
                if (ring == null || !ring.IsCollected)
                {
                    continue;
                }
                float d = (ring.transform.position - KartRoot().position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    spot = ring.transform.position;
                }
            }
            seagull.RingCollected(spot);
        }

        private void ClearAll()
        {
            ClearMoving();
            if (deans != null) deans.Clear();
        }

        // Recovery keeps the item and the Dean's List shield (ItemSystem rule), so only the moving effects go.
        private void ClearMoving()
        {
            if (wave == null)
            {
                return;
            }
            wave.Clear();
            espresso.Clear();
            seagull.Clear();
        }
    }
}
