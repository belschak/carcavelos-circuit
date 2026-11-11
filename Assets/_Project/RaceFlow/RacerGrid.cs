using System.Collections.Generic;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R8, plain C# (Edit Mode tested): the starting grid behind the track's SpawnPoint.
    // Slot 0 = the SpawnPoint itself, always the player's (pole position, the time trial starts exactly as before).
    // Bots take slots 1, 2, 3 ... in a staggered grid: slot 1 right and one row back, slot 2 left and one row back,
    // slot 3 right two rows back, and so on (docs/interfaces.md: 2 m sideways, 3 m back per row, numbers in
    // RaceSettings). A bot keeps its slot for the whole session, so Retry always puts it back on the same spot.
    // A slot that is freed (a bot left) goes to the next bot that needs one: lowest free number first.
    public class RacerGrid
    {
        private readonly float sidewaysMetres;
        private readonly float rowMetres;
        private readonly List<IRacer> racers = new List<IRacer>(); // racers[i] holds slots[i]
        private readonly List<int> slots = new List<int>();

        public RacerGrid(float sidewaysMetres, float rowMetres)
        {
            // A broken number in the asset must not stack every kart on one spot: fall back to the defaults.
            this.sidewaysMetres = IsUsable(sidewaysMetres) ? sidewaysMetres : 2f;
            this.rowMetres = IsUsable(rowMetres) ? rowMetres : 3f;
        }

        // How many racers have a slot right now.
        public int Count { get { return racers.Count; } }

        // Where a slot is, seen from the SpawnPoint: x = metres to the right, z = metres forward (negative = behind).
        public Vector3 SlotOffset(int slot)
        {
            if (slot <= 0)
            {
                return Vector3.zero;
            }
            int row = (slot + 1) / 2;              // 1,2 -> row 1; 3,4 -> row 2 ...
            float side = slot % 2 == 1 ? 1f : -1f; // odd = right, even = left
            return new Vector3(side * sidewaysMetres, 0f, -row * rowMetres);
        }

        // The slot's world position: the offset turned with the SpawnPoint, so the grid follows any start direction
        // (and the slope of a tilted SpawnPoint).
        public Vector3 SlotPosition(int slot, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            return spawnPosition + spawnRotation * SlotOffset(slot);
        }

        // The racer's slot, or -1 if it has none yet.
        public int SlotOf(IRacer racer)
        {
            int i = racers.IndexOf(racer);
            return i < 0 ? -1 : slots[i];
        }

        // Gives the racer a slot and returns it: the player always gets 0, a bot keeps the slot it already has or gets
        // the lowest free one from 1 up. Returns -1 for null.
        public int Assign(IRacer racer)
        {
            if (racer == null)
            {
                return -1;
            }
            int existing = SlotOf(racer);
            if (existing >= 0)
            {
                return existing;
            }
            int slot = racer.IsPlayer ? 0 : LowestFreeSlot();
            racers.Add(racer);
            slots.Add(slot);
            return slot;
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

        // 1, 2, 3 ... the first number no bot holds (slot 0 is never given to a bot).
        private int LowestFreeSlot()
        {
            int slot = 1;
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
