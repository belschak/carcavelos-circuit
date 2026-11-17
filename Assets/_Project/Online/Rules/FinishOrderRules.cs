using System.Collections.Generic;

namespace CarcavelosCircuit.Online
{
    // The results order of an online race (plain C#, tested in FinishOrderRulesTests). The same rule runs on every
    // screen from the same reported times, so everyone sees the same list:
    // finished players first by lap time (a tie: the lower id first), then everyone still racing (roster order),
    // then DNF (left the room) in roster order. The entries come in roster order.
    public static class FinishOrderRules
    {
        public static List<FinishRow> Order(IReadOnlyList<FinishEntry> entries)
        {
            List<FinishRow> finished = new List<FinishRow>();
            List<FinishRow> racing = new List<FinishRow>();
            List<FinishRow> gone = new List<FinishRow>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    FinishEntry entry = entries[i];
                    FinishRow row = new FinishRow();
                    row.Id = entry.Id;
                    row.Name = entry.Name ?? "";
                    bool validTime = entry.HasFinished && !double.IsNaN(entry.Seconds) && !double.IsInfinity(entry.Seconds) && entry.Seconds >= 0.0;
                    if (validTime)
                    {
                        row.State = FinishState.Finished;
                        row.Seconds = entry.Seconds;
                        InsertByTime(finished, row);
                    }
                    else if (entry.Present)
                    {
                        row.State = FinishState.Racing;
                        racing.Add(row);
                    }
                    else
                    {
                        row.State = FinishState.Dnf;
                        gone.Add(row);
                    }
                }
            }
            List<FinishRow> rows = new List<FinishRow>(finished.Count + racing.Count + gone.Count);
            for (int i = 0; i < finished.Count; i++)
            {
                FinishRow row = finished[i];
                row.Place = i + 1;
                rows.Add(row);
            }
            rows.AddRange(racing);
            rows.AddRange(gone);
            return rows;
        }

        // The place of a player in the ordered rows, 0 when that player has not finished (or is not there).
        public static int PlaceOf(IReadOnlyList<FinishRow> rows, int playerId)
        {
            if (rows == null)
            {
                return 0;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Id == playerId)
                {
                    return rows[i].Place;
                }
            }
            return 0;
        }

        // Is the race over for everyone: nobody is still racing (all finished or gone)?
        public static bool AllDone(IReadOnlyList<FinishRow> rows)
        {
            if (rows == null)
            {
                return true;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].State == FinishState.Racing)
                {
                    return false;
                }
            }
            return true;
        }

        // Insertion by time, lower id first on the same time (a small list, no sort call needed).
        private static void InsertByTime(List<FinishRow> list, FinishRow row)
        {
            int at = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                bool earlier = row.Seconds < list[i].Seconds || (row.Seconds == list[i].Seconds && row.Id < list[i].Id);
                if (earlier)
                {
                    at = i;
                    break;
                }
            }
            list.Insert(at, row);
        }
    }
}
