using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Q14 bridge: Role 1's Kart prefab has an empty "Visual" slot with a grey stand-in cube ("Box") until Role 3
    // puts the real kart model there (Luc's Q11). Until then the race puts Role 3's KartVisual model into the
    // slot of the spawned kart and hides the cube. It never edits the prefab, and it does nothing once the
    // prefab already carries a model, so the day Q11 is done this simply stops working on its own.
    public static class KartModelSwap
    {
        public const string VisualSlotName = "Visual";
        public const string StandInName = "Box";

        // Returns true if the model was added. kart = the spawned kart, model = Role 3's KartVisual prefab.
        public static bool AddModelIfMissing(GameObject kart, GameObject model)
        {
            if (kart == null || model == null)
            {
                return false;
            }
            Transform slot = kart.transform.Find(VisualSlotName);
            if (slot == null || HasModel(slot))
            {
                return false;
            }

            GameObject added = Object.Instantiate(model, slot, false); // false = keep the model's own local position
            added.name = model.name;
            Transform standIn = slot.Find(StandInName);
            if (standIn != null)
            {
                standIn.gameObject.SetActive(false);
            }
            return true;
        }

        // A model counts as there if any child of the slot is something other than the grey stand-in cube.
        private static bool HasModel(Transform slot)
        {
            for (int i = 0; i < slot.childCount; i++)
            {
                if (slot.GetChild(i).name != StandInName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
