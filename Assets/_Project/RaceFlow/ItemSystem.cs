using System;
using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Item boxes (task R7, decision 2026-11-10). On the "Items" object in Main.unity.
    // When the race is set up it puts rows of azulejo boxes across the road after some checkpoints (ItemBoxLayout).
    // Drive through a box: ItemOdds picks an item (better ones when you are behind your best lap), the HUD slot
    // rolls (ItemSlot), then E uses it. Every item is a boost, given to the kart through Shared/IKartBoost.
    // Retry empties the slot and puts every box back; recovery (R) keeps your item.
    // Like the rest of RaceFlow it only talks to the kart through its contracts and to the track through checkpoints.
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

        [SerializeField, Tooltip("A box bursts into tile shards")]
        private AudioClip boxBreak;

        [SerializeField, Tooltip("One click of the spinning slot (plays once per icon change)")]
        private AudioClip rollTick;

        [SerializeField, Tooltip("The slot stops: the item is ready")]
        private AudioClip itemReady;

        [SerializeField, Tooltip("Bica and Triple Bica used: espresso hiss")]
        private AudioClip bicaUse;

        [SerializeField, Tooltip("Onda used: a big wave")]
        private AudioClip ondaUse;

        // ---- What the HUD reads ----
        public ItemSlot Slot { get { return slot; } }
        public int BoxCount { get { return boxes.Count; } }
        public event Action<ItemKind> ItemReady;   // the roll landed
        public event Action<ItemKind> ItemUsed;     // E was pressed and the boost was given

        private readonly ItemSlot slot = new ItemSlot();
        private readonly List<ItemBox> boxes = new List<ItemBox>();
        private Material liveMaterial;    // copy of boxMaterial with the tile texture
        private Texture2D tileTexture;
        private IKartBoost kartBoost;     // null if the kart cannot be boosted from outside
        private bool spawned;
        private int resumeFrame = -1;     // the key that ends a pause must not also use the item

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
            if (kartBoost != null)
            {
                kartBoost.AddBoost(settings.BoostSecondsFor(used), settings.BoostMultiplierFor(used));
            }
            Play(used == ItemKind.Onda ? ondaUse : bicaUse, 1f);
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

        // For ItemBox: does this collider belong to our kart? Same rule as the checkpoints (RaceManager).
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

        private void OnEnable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted += OnRestarted;
                raceManager.Resumed += OnResumed;
            }
        }

        private void OnDisable()
        {
            if (raceManager != null)
            {
                raceManager.Restarted -= OnRestarted;
                raceManager.Resumed -= OnResumed;
            }
        }

        private void OnDestroy()
        {
            // Runtime-made textures and materials are not cleaned up by Unity on their own.
            if (liveMaterial != null) Destroy(liveMaterial);
            if (tileTexture != null) Destroy(tileTexture);
        }

        private void OnRestarted()
        {
            slot.Clear();
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i].ResetBox();
            }
        }

        private void OnResumed()
        {
            resumeFrame = Time.frameCount;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volume * SoundLevels.Effects);
            }
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
