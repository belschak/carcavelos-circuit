using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Driver select (Luc, 17 Nov 2026): puts another driver's kart model (Visuals/Kart/Prefabs/KartVisual_*) into
    // the "Visual" slot of a spawned kart (Role 1's Kart.prefab carries the Rookie's Classic there). Only the looks
    // change: KartController, colliders and the driving stay. The old model is detached and switched off at once,
    // so anything that searches the kart in the same frame (KartEffects.Rebind) only finds the new one.
    public static class KartVisualSwap
    {
        public const string SlotName = "Visual";
        public const string ModelPrefix = "KartVisual_";

        // Returns the model now in the slot (the new one, or the old one if it already was this model).
        // Null if the kart has no Visual slot or no model was given.
        public static GameObject Apply(GameObject kart, GameObject model)
        {
            if (kart == null || model == null)
            {
                return null;
            }
            Transform slot = kart.transform.Find(SlotName);
            if (slot == null)
            {
                return null;
            }
            Transform current = CurrentModel(slot);
            if (current != null && current.name == model.name)
            {
                return current.gameObject;
            }
            if (current != null)
            {
                current.SetParent(null, false);
                current.gameObject.SetActive(false);
                Object.Destroy(current.gameObject);
            }
            GameObject added = Object.Instantiate(model, slot, false);
            added.name = model.name;
            return added;
        }

        // The kart model in the slot (a child named KartVisual_...), null if there is none.
        public static Transform CurrentModel(Transform slot)
        {
            for (int i = 0; i < slot.childCount; i++)
            {
                if (slot.GetChild(i).name.StartsWith(ModelPrefix, System.StringComparison.Ordinal))
                {
                    return slot.GetChild(i);
                }
            }
            return null;
        }
    }
}
