using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Kart builder (18 Nov 2026): one finished, switched-off model per driver + kart + tyres set (KartAssembler), made
    // the first time someone needs it and then reused. Everybody copies these the way they copied Luc's prefabs before:
    // the player's kart (BotSquad), the bots, the other players' karts online (ProxyKart), the builder's turntable and
    // the start screen's line-up (through RaceFlow's KartModels door).
    // The models sit under one switched-off object, so none of their scripts ever runs; a copy wakes up where it is put.
    public static class KartComboLibrary
    {
        private static readonly Dictionary<int, GameObject> made = new Dictionary<int, GameObject>();
        private static GameObject holder;
        private static BotSettings source;

        // The kart list to build from (BotSettings.kartVisuals: Luc's four KartVisual_* prefabs).
        public static void Use(BotSettings settings)
        {
            if (settings != source)
            {
                Clear();
                source = settings;
            }
        }

        public static bool HasSource { get { return source != null && source.KartVisual(DriverRoster.Get(0).KartVisual) != null; } }

        // The model of this set, null without a kart list. An invalid set = the Rookie's own.
        public static GameObject Template(KartLoadout loadout)
        {
            if (!loadout.IsValid)
            {
                loadout = KartLoadout.Default;
            }
            int key = loadout.GetHashCode();
            GameObject model;
            if (made.TryGetValue(key, out model) && model != null)
            {
                return model;
            }
            if (source == null)
            {
                return null;
            }
            GameObject driver = source.KartVisual(DriverRoster.Get(loadout.Driver).KartVisual);
            GameObject kart = source.KartVisual(DriverRoster.Get(loadout.Kart).KartVisual);
            GameObject tyres = source.KartVisual(DriverRoster.Get(loadout.Tyres).KartVisual);
            model = KartAssembler.Assemble(driver, kart, tyres, Holder(), loadout.ModelName);
            made[key] = model;
            return model;
        }

        // The model for one of Luc's prefab names (a bot seat: DriverSwap.Seat.KartVisual) = that driver's own set.
        // Null if no driver drives that prefab.
        public static GameObject TemplateForPrefab(string prefabName)
        {
            for (int i = 0; i < DriverRoster.Count; i++)
            {
                if (DriverRoster.Get(i).KartVisual == prefabName)
                {
                    return Template(KartLoadout.Signature(i));
                }
            }
            return null;
        }

        // Forgets every model (another kart list, or tests).
        public static void Clear()
        {
            foreach (GameObject model in made.Values)
            {
                if (model != null)
                {
                    Delete(model);
                }
            }
            made.Clear();
            if (holder != null)
            {
                Delete(holder);
            }
            holder = null;
            source = null;
        }

        private static Transform Holder()
        {
            if (holder == null)
            {
                holder = new GameObject("KartComboLibrary (switched off)");
                holder.SetActive(false);
                holder.transform.position = new Vector3(0f, -9000f, 0f);
                if (Application.isPlaying)
                {
                    Object.DontDestroyOnLoad(holder);
                }
            }
            return holder.transform;
        }

        private static void Delete(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
