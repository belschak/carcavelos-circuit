using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Reading what comes back from the internet and writing what we send (task R10). Plain C#, unit tested
    // (LeaderboardJsonTests). Everything from outside is treated as untrusted:
    // - Not a JSON list, too long, or broken: the whole answer is refused (the HUD then says "offline").
    // - Every row is checked like a name typed here (PlayerNameRules) and a lap time (LapTimeRules); a bad row is
    //   dropped, the good ones are kept. No text from the server is ever shown except names that passed the rules.
    public static class LeaderboardJson
    {
        // A top-10 answer is about 500 bytes. Anything far bigger is not ours.
        public const int MaxAnswerLength = 64 * 1024;

        // Rows from "[{"name":"Ana","seconds":61.2}, ...]". False if the text is not a JSON list we can read.
        public static bool TryParseRows(string json, int maxRows, out List<LeaderboardEntry> rows)
        {
            rows = new List<LeaderboardEntry>();
            if (json == null || json.Length > MaxAnswerLength)
            {
                return false;
            }
            string trimmed = json.Trim();
            if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
            {
                return false; // PostgREST answers a select with a list; anything else (an error object, HTML) is refused
            }

            LeaderboardRows parsed;
            try
            {
                parsed = JsonUtility.FromJson<LeaderboardRows>("{\"rows\":" + trimmed + "}");
            }
            catch (System.ArgumentException)
            {
                return false; // broken JSON
            }
            if (parsed == null || parsed.rows == null)
            {
                return false;
            }
            for (int i = 0; i < parsed.rows.Length && rows.Count < maxRows; i++)
            {
                if (IsGoodRow(parsed.rows[i]))
                {
                    rows.Add(parsed.rows[i]);
                }
            }
            return true;
        }

        // A row we are willing to show: a clean name and a lap time inside the general window (5 s to 30 min).
        public static bool IsGoodRow(LeaderboardEntry row)
        {
            return row != null && PlayerNameRules.IsValidStoredName(row.name) && LapTimeRules.IsAcceptable(row.seconds, 0f);
        }

        // The body of a "save my time" request.
        public static string InsertBody(string track, string name, float seconds)
        {
            LapTimeInsert insert = new LapTimeInsert();
            insert.track = track;
            insert.name = name;
            insert.seconds = seconds;
            return JsonUtility.ToJson(insert);
        }

        // The total from a "Content-Range" answer header: "0-9/37" or "*/37" -> 37 (how many rows matched).
        // Only plain digits after the slash are accepted, at most 9 of them.
        public static bool TryParseTotal(string contentRange, out int total)
        {
            total = 0;
            if (string.IsNullOrEmpty(contentRange))
            {
                return false;
            }
            int slash = contentRange.LastIndexOf('/');
            if (slash < 0 || slash == contentRange.Length - 1)
            {
                return false;
            }
            string digits = contentRange.Substring(slash + 1).Trim();
            if (digits.Length == 0 || digits.Length > 9)
            {
                return false;
            }
            int value = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                char c = digits[i];
                if (c < '0' || c > '9')
                {
                    return false; // "*" (total unknown) or anything else
                }
                value = value * 10 + (c - '0');
            }
            total = value;
            return true;
        }
    }
}
