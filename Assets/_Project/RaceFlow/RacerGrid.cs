using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R8, plain C# (Edit Mode tested): the starting grid at the track's SpawnPoint.
    // V23 (Luc, 13 Nov 2026): the grid is pairs side by side, like the boxes painted on the start plaza.
    // Slot 0 = box 1 = front row left, slot 1 = box 2 = front row right, slot 2 = box 3 = second row left, and so on.
    // The front row is centred on the SpawnPoint (the SpawnPoint itself is the free middle between boxes 1 and 2).
    // Numbers in RaceSettings (docs/interfaces.md: 2 m sideways, 3 m back per row).
    // KEEP IN SYNC: tools/world_gen/start_plan.py slot_offset() is the same formula in Python; the generator paints
    // the boxes with it. Change one, change the other (RacerGridTests.SlotOffsets_AreTheTableTheGeneratorPaints).
    // Who stands where:
    // - Bots take the slots from the front: 0, 1, 2 ... A bot keeps its slot for the whole session, so Retry always
    //   puts it back on the same spot. A freed slot (a bot left) goes to the next bot that needs one, lowest first.
    // - The player always starts LAST, in the slot right behind the last bot. Alone (time trial) that is slot 0.
    public class RacerGrid
    {
        private readonly float sidewaysMetres;
        private readonly float rowMetres;
        private readonly List<IRacer> racers = new List<IRacer>(); // racers[i] holds slots[i]
        private readonly List<int> slots = new List<int>();
        private const int NoStoredSlot = -1; // the player's entry in slots (see SlotOf)

        public RacerGrid(float sidewaysMetres, float rowMetres)
        {
            // A broken number in the asset must not stack every kart on one spot: fall back to the defaults.
            this.sidewaysMetres = IsUsable(sidewaysMetres) ? sidewaysMetres : 2f;
            this.rowMetres = IsUsable(rowMetres) ? rowMetres : 3f;
        }

        // How many racers have a slot right now.
        public int Count { get { return racers.Count; } }

        // The player's slot in a time trial (no bots): front row, box 1.
        public const int TimeTrialSlot = 0;

        // Where a slot is, seen from the SpawnPoint: x = metres to the right, z = metres forward (negative = behind).
        public Vector3 SlotOffset(int slot)
        {
            return OffsetOf(slot, sidewaysMetres, rowMetres);
        }

        // The one grid formula (mirrored by start_plan.py slot_offset, see the note at the top).
        public static Vector3 OffsetOf(int slot, float sidewaysMetres, float rowMetres)
        {
            if (slot < 0)
            {
                slot = 0;
            }
            int row = slot / 2;                    // 0,1 -> front row; 2,3 -> second row ...
            float side = slot % 2 == 0 ? -1f : 1f; // even slot (odd box number) = left, odd slot = right
            return new Vector3(side * sidewaysMetres, 0f, -row * rowMetres);
        }

        // The slot's world position: the offset turned with the SpawnPoint, so the grid follows any start direction
        // (and the slope of a tilted SpawnPoint).
        public Vector3 SlotPosition(int slot, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            return spawnPosition + spawnRotation * SlotOffset(slot);
        }

        // The racer's slot, or -1 if it has none yet. The player's slot is not stored: it is always the one behind
        // the last bot, so it moves back when a bot joins and forward when the last bot leaves.
        public int SlotOf(IRacer racer)
        {
            int i = racers.IndexOf(racer);
            if (i < 0)
            {
                return -1;
            }
            return racer.IsPlayer ? PlayerSlot : slots[i];
        }

        // Where the player starts: behind every bot (highest bot slot + 1), or TimeTrialSlot if there is no bot.
        public int PlayerSlot
        {
            get
            {
                int last = TimeTrialSlot - 1;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!racers[i].IsPlayer && slots[i] > last)
                    {
                        last = slots[i];
                    }
                }
                return last + 1;
            }
        }

        // Gives the racer a slot and returns it: a bot keeps the slot it already has or gets the lowest free one
        // from 0 up, the player gets PlayerSlot (the last one). Returns -1 for null.
        public int Assign(IRacer racer)
        {
            if (racer == null)
            {
                return -1;
            }
            if (racers.IndexOf(racer) < 0)
            {
                racers.Add(racer);
                slots.Add(racer.IsPlayer ? NoStoredSlot : LowestFreeSlot());
            }
            return SlotOf(racer);
        }

        // Frees the racer's slot. False if it had none.
        public bool Release(IRacer racer)
        {
            int i = racers.IndexOf(racer);
            if (i < 0)
            {
                return false;
            }
            racers.RemoveAt(i);
            slots.RemoveAt(i);
            return true;
        }

        // Frees the slots of every racer that is not in the list any more (it unregistered or was destroyed).
        public void KeepOnly(IReadOnlyList<IRacer> present)
        {
            for (int i = racers.Count - 1; i >= 0; i--)
            {
                bool found = false;
                if (present != null)
                {
                    for (int j = 0; j < present.Count; j++)
                    {
                        if (present[j] == racers[i])
                        {
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    racers.RemoveAt(i);
                    slots.RemoveAt(i);
                }
            }
        }

        // 0, 1, 2 ... the first number no bot holds (the player's entry holds NoStoredSlot, so it never blocks one).
        private int LowestFreeSlot()
        {
            int slot = 0;
            while (slots.Contains(slot))
            {
                slot++;
            }
            return slot;
        }

        private static bool IsUsable(float metres)
        {
            return !float.IsNaN(metres) && !float.IsInfinity(metres) && metres > 0.1f && metres < 50f;
        }
    }
}
