using System;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // The door from RaceFlow's menus to Role 3's kart models (kart builder, 18 Nov 2026). RaceFlow must not reference the
    // Visuals assembly (Visuals references RaceFlow), so Visuals plugs its builder in here when the race scene starts
    // (BotSquad.OnEnable: KartComboLibrary) and the menus only call it. Same pattern as OnlineRaceHooks.
    // Template(loadout) returns a finished kart model for that driver + kart + tyres that sits switched off out of sight;
    // the caller makes its own copy with Object.Instantiate(template, parent, false). Null = no kart models in this scene
    // (a test scene): the builder then shows the driver portraits only.
    public static class KartModels
    {
        public static Func<KartLoadout, GameObject> Template;

        public static bool Available { get { return Template != null; } }

        // A switched-on copy of the model under parent, or null without models.
        public static GameObject Make(KartLoadout loadout, Transform parent)
        {
            if (Template == null)
            {
                return null;
            }
            GameObject template = Template(loadout);
            if (template == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(template, parent, false);
            copy.name = template.name;
            copy.SetActive(true);
            return copy;
        }
    }
}
