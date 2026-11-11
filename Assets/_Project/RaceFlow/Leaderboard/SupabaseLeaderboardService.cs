using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CarcavelosCircuit.RaceFlow
{
    // The online leaderboard (task R10): a Supabase table "lap_times" read and written through its REST API
    // (PostgREST) with UnityWebRequest, the only network code in the game. Setup: backend/supabase/README.md.
    // - Only the PUBLIC key is used. The database itself only lets that key add and read rows (Row Level Security),
    //   and checks name and time again (CHECK constraints), so a changed game can do no more than a fair one.
    // - Every request gives up after a few seconds; then the reply says Success = false and the
    //   FallbackLeaderboardService shows this device's times with the "offline" note.
    // - Answers are checked by LeaderboardJson (bad rows dropped). Server error texts are only logged, never shown.
    // No coroutine is needed: SendWebRequest returns an operation whose "completed" event runs on the main thread.
    public class SupabaseLeaderboardService : ILeaderboardService
    {
        private readonly string baseUrl;
        private readonly string key;
        private readonly bool keyIsJwt;
        private readonly int timeoutSeconds;

        // url and key must have passed LeaderboardConfigCheck.Problem (LeaderboardHud checks before making this).
        public SupabaseLeaderboardService(string url, string key, int timeoutSeconds)
        {
            baseUrl = LeaderboardConfigCheck.BaseUrl(url);
            this.key = key.Trim();
            keyIsJwt = LeaderboardConfigCheck.IsJwt(this.key);
            this.timeoutSeconds = timeoutSeconds;
        }

        public void SubmitTime(string track, string name, float seconds, Action<LeaderboardReply> done)
        {
            if (!LapTimeRules.IsValidTrack(track))
            {
                done(LeaderboardReply.Failed());
                return;
            }
            byte[] body = Encoding.UTF8.GetBytes(LeaderboardJson.InsertBody(track, name, seconds));
            UnityWebRequest request = new UnityWebRequest(SupabaseRequests.InsertUrl(baseUrl), UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal"); // we do not need the new row back
            Send(request, () =>
            {
                if (!IsOk(request, "save"))
                {
                    done(LeaderboardReply.Failed());
                    return;
                }
                FetchRank(track, seconds, done); // saved: now ask for our place
            });
        }

        public void FetchTop(string track, int count, Action<LeaderboardReply> done)
        {
            if (!LapTimeRules.IsValidTrack(track))
            {
                done(LeaderboardReply.Failed());
                return;
            }
            UnityWebRequest request = UnityWebRequest.Get(SupabaseRequests.TopUrl(baseUrl, track, count));
            Send(request, () =>
            {
                List<LeaderboardEntry> rows;
                if (!IsOk(request, "top list") || !LeaderboardJson.TryParseRows(request.downloadHandler.text, count, out rows))
                {
                    done(LeaderboardReply.Failed());
                    return;
                }
                LeaderboardReply reply = new LeaderboardReply();
                reply.Success = true;
                reply.Entries = LeaderboardRanking.Sorted(rows, count);
                done(reply);
            });
        }

        // Our place = how many saved times are faster + 1. HEAD asks only for the count, no rows.
        // The time is already saved, so even if this fails the save counts as done (place unknown = 0).
        private void FetchRank(string track, float seconds, Action<LeaderboardReply> done)
        {
            UnityWebRequest request = UnityWebRequest.Head(SupabaseRequests.FasterCountUrl(baseUrl, track, seconds));
            request.SetRequestHeader("Prefer", "count=exact");
            Send(request, () =>
            {
                LeaderboardReply reply = new LeaderboardReply();
                reply.Success = true;
                int faster;
                if (IsOk(request, "rank") && LeaderboardJson.TryParseTotal(request.GetResponseHeader("Content-Range"), out faster))
                {
                    reply.Rank = faster + 1;
                }
                done(reply);
            });
        }

        // Adds the key headers and the timeout, sends, runs whenDone once the answer (or the timeout) is there,
        // and frees the request afterwards (UnityWebRequest holds native memory).
        private void Send(UnityWebRequest request, Action whenDone)
        {
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("apikey", key);
            if (keyIsJwt)
            {
                request.SetRequestHeader("Authorization", "Bearer " + key); // older anon keys are JWTs
            }
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += op =>
            {
                try
                {
                    whenDone();
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        // True for a 2xx answer. Anything else is logged with its HTTP code only (never the body: it is the
        // server's text, and the HUD shows only our own words).
        private static bool IsOk(UnityWebRequest request, string what)
        {
            if (request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300)
            {
                return true;
            }
            Debug.LogWarning("Leaderboard: " + what + " failed (" + request.result + ", HTTP " + request.responseCode + "). Using this device's times.");
            return false;
        }
    }
}
