using System.Globalization;

namespace CarcavelosCircuit.RaceFlow
{
    // The web addresses the online leaderboard calls (task R10). Plain C#, unit tested (SupabaseRequestsTests), so
    // the exact requests can be read and checked without a server. Supabase puts a REST API (PostgREST) in front of
    // the database table: GET reads rows, POST adds one. Filters are part of the address, e.g. track=eq.Track_Campus.
    // The track is checked by LapTimeRules.IsValidTrack before it goes in (letters, digits, _ and - only), so it
    // never needs escaping and can never add a filter of its own.
    public static class SupabaseRequests
    {
        public const string Table = "/rest/v1/lap_times";

        // Top N of one track: fastest first; equal times in the order they were saved.
        public static string TopUrl(string baseUrl, string track, int count)
        {
            return baseUrl + Table + "?select=name,seconds&track=eq." + track
                + "&order=seconds.asc,created_at.asc&limit=" + count.ToString(CultureInfo.InvariantCulture);
        }

        // Where a new time is posted.
        public static string InsertUrl(string baseUrl)
        {
            return baseUrl + Table;
        }

        // How many saved times of this track are faster than ours (asked with "Prefer: count=exact", the answer
        // header Content-Range says "*/N"). Our place = N + 1, also when we are not in the top 10.
        // "R" writes the float exactly as it was sent ("61.2345"), so our own row is never counted as faster.
        public static string FasterCountUrl(string baseUrl, string track, float seconds)
        {
            return baseUrl + Table + "?select=seconds&track=eq." + track
                + "&seconds=lt." + seconds.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
