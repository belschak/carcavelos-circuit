using System;
using UnityEngine;

namespace CarcavelosCircuit.Shared
{
    // V28 (Role 3): the light mood of the campus world. The number is what PlayerPrefs stores, so keep the numbers.
    // Night comes later as 2 (it needs lit lamps first).
    public enum TimeOfDay
    {
        Noon = 0,     // the look the game always had: high summer sun from the south-west
        Sunset = 1    // low orange sun over the sea in the west-south-west
    }

    // The mood the player picked. The start screen (Role 4) changes it; the campus world (Role 3, CampusWorldLighting)
    // listens and switches sun, sky, haze and colour grade at once. Static, so neither side needs a scene reference.
    // Saved in PlayerPrefs under its own key, so it stays for the next race and the next visit.
    public static class TimeOfDayChoice
    {
        public const string PrefsKey = "timeOfDay";

        // How many moods there are; Next() cycles through them in this order.
        public const int Count = 2;

        private static bool loaded;
        private static TimeOfDay current;

        // Fired after the mood changed (not when Set gets the mood that is already on).
        public static event Action<TimeOfDay> Changed;

        // The chosen mood; the first read loads it from PlayerPrefs (Noon if nothing is saved yet).
        public static TimeOfDay Current
        {
            get
            {
                if (!loaded)
                {
                    current = FromSaved(PlayerPrefs.GetInt(PrefsKey, (int)TimeOfDay.Noon));
                    loaded = true;
                }
                return current;
            }
        }

        // Picks a mood, saves it and tells the listeners. Returns false if it was already the mood.
        public static bool Set(TimeOfDay mood)
        {
            mood = FromSaved((int)mood);
            if (mood == Current)
            {
                return false;
            }
            current = mood;
            PlayerPrefs.SetInt(PrefsKey, (int)mood);
            PlayerPrefs.Save();
            if (Changed != null)
            {
                Changed(mood);
            }
            return true;
        }

        // The menu button: Noon -> Sunset -> Noon. direction -1 goes the other way.
        public static TimeOfDay Next(TimeOfDay mood, int direction = 1)
        {
            int step = direction < 0 ? -1 : 1;
            int next = ((int)FromSaved((int)mood) + step) % Count;
            return (TimeOfDay)(next < 0 ? next + Count : next);
        }

        // A saved number back to a mood; anything unknown (an old or broken save, a later "Night" in an older build)
        // becomes Noon, so the game always starts with a valid look.
        public static TimeOfDay FromSaved(int saved)
        {
            return saved >= 0 && saved < Count ? (TimeOfDay)saved : TimeOfDay.Noon;
        }

        // The name the menu shows.
        public static string Label(TimeOfDay mood)
        {
            return mood == TimeOfDay.Sunset ? "SUNSET" : "NOON";
        }

        // Play Mode without domain reload keeps statics: start each run from the saved value with no old listeners.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            loaded = false;
            Changed = null;
        }
    }
}
