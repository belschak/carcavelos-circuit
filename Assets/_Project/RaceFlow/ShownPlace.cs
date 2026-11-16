namespace CarcavelosCircuit.RaceFlow
{
    // The place the finish overlay and the results card show (plain C#, Edit Mode tests in ShownPlaceTests).
    // Normally RaceRoster writes the live standings every frame. Online the room knows better (everyone's
    // reported lap times, the same order on every screen), so the lobby can set that place instead; it wins
    // until it is cleared. Bad numbers from outside are stored as 0 = unknown, and RaceModeRules.HasKnownPlace
    // then shows the lap time instead of a place.
    public class ShownPlace
    {
        private int externalPlace;
        private int externalRacers;

        // RaceRoster's standings, written every frame (also while an external place is shown).
        public int RosterPlace { get; set; }
        public int RosterRacers { get; set; }

        public bool IsExternal { get; private set; }
        public int Place { get { return IsExternal ? externalPlace : RosterPlace; } }
        public int Racers { get { return IsExternal ? externalRacers : RosterRacers; } }

        // The online room's place: "3rd of 7". Negative numbers become 0 (unknown).
        public void SetExternal(int place, int racers)
        {
            externalPlace = place > 0 ? place : 0;
            externalRacers = racers > 0 ? racers : 0;
            IsExternal = true;
        }

        // Back to RaceRoster's place.
        public void ClearExternal()
        {
            IsExternal = false;
            externalPlace = 0;
            externalRacers = 0;
        }
    }
}
