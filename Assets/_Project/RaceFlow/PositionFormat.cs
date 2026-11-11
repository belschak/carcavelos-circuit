namespace CarcavelosCircuit.RaceFlow
{
    // Task R9, plain C# (Edit Mode tested): English ordinals for the position readout ("1st", "2nd", "13th", "22nd").
    // Rule: 11, 12 and 13 (also 111, 112, 113 ...) always take "th"; otherwise the last digit decides: 1 st, 2 nd, 3 rd.
    public static class PositionFormat
    {
        // "st", "nd", "rd" or "th". "" for 0 or less (no position).
        public static string Suffix(int position)
        {
            if (position <= 0)
            {
                return "";
            }
            int lastTwo = position % 100;
            if (lastTwo >= 11 && lastTwo <= 13)
            {
                return "th";
            }
            int last = position % 10;
            if (last == 1) return "st";
            if (last == 2) return "nd";
            if (last == 3) return "rd";
            return "th";
        }

        // "2nd". "" for 0 or less.
        public static string Ordinal(int position)
        {
            if (position <= 0)
            {
                return "";
            }
            return position + Suffix(position);
        }

        // "/ 6" under the big number. "" for 0 or less racers.
        public static string OfTotal(int racers)
        {
            return racers <= 0 ? "" : "/ " + racers;
        }
    }
}
