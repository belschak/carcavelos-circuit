using System;
using System.Text;

namespace CarcavelosCircuit.RaceFlow
{
    // Is the leaderboard connection safe to use (task R10)? Plain C#, unit tested (LeaderboardConfigCheckTests).
    // Runs in the game (before the first request) and in the CI Web build (a bad secret stops the build there).
    // - Both empty = no online leaderboard, the game keeps times on this device. That is not an error.
    // - The address must be exactly https://<project>.supabase.co: https so nobody can read or change the answers on
    //   the way, and the supabase.co domain so a wrong or planted address can never receive the players' names.
    // - The key must be the PUBLIC one (anon / publishable). A secret or service_role key can change and delete every
    //   row, so the check refuses it even if someone pastes it into the CI secret by mistake.
    public static class LeaderboardConfigCheck
    {
        private const string Scheme = "https://";
        private const string Domain = ".supabase.co";
        private const int MaxProjectLength = 63; // one part of a web address
        private const int MinKeyLength = 20;
        private const int MaxKeyLength = 2000;

        public static bool IsEmpty(string url, string key)
        {
            return string.IsNullOrEmpty(Trimmed(url)) && string.IsNullOrEmpty(Trimmed(key));
        }

        // "" if url and key can be used, otherwise a short reason for the log (never shown to players).
        public static string Problem(string url, string key)
        {
            if (string.IsNullOrEmpty(Trimmed(url)))
            {
                return "the project URL is empty but a key is set";
            }
            if (string.IsNullOrEmpty(Trimmed(key)))
            {
                return "the anon key is empty but a URL is set";
            }
            string urlProblem = UrlProblem(url);
            if (urlProblem != "")
            {
                return urlProblem;
            }
            return KeyProblem(key);
        }

        // "https://abcd.supabase.co/" -> "https://abcd.supabase.co" (requests add "/rest/v1/..." to it).
        public static string BaseUrl(string url)
        {
            string trimmed = Trimmed(url);
            if (trimmed.EndsWith("/"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }
            return trimmed.ToLowerInvariant();
        }

        public static string UrlProblem(string url)
        {
            string trimmed = BaseUrl(url);
            if (!trimmed.StartsWith(Scheme))
            {
                return "the project URL must start with https://";
            }
            if (!trimmed.EndsWith(Domain))
            {
                return "the project URL must end with .supabase.co";
            }
            // What is left in the middle is the project name: letters, digits and - only. That also rules out
            // paths, ports, user names and tricks like https://evil.example/x.supabase.co.
            string project = trimmed.Substring(Scheme.Length, trimmed.Length - Scheme.Length - Domain.Length);
            if (project.Length == 0 || project.Length > MaxProjectLength)
            {
                return "the project URL has no project name";
            }
            for (int i = 0; i < project.Length; i++)
            {
                char c = project[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                if (!ok)
                {
                    return "the project URL must be exactly https://<project>.supabase.co";
                }
            }
            return "";
        }

        public static string KeyProblem(string key)
        {
            string trimmed = Trimmed(key);
            if (trimmed.Length < MinKeyLength || trimmed.Length > MaxKeyLength)
            {
                return "the key has an impossible length";
            }
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!ok)
                {
                    return "the key contains characters a Supabase key never has";
                }
            }
            if (trimmed.StartsWith("sb_secret_"))
            {
                return "this is a SECRET key: use the publishable (anon) key";
            }
            if (trimmed.StartsWith("sb_publishable_"))
            {
                return "";
            }
            if (!IsJwt(trimmed))
            {
                return "the key is neither a publishable key nor an anon JWT";
            }
            string payload = JwtPayload(trimmed);
            if (payload.Contains("\"role\":\"anon\""))
            {
                return "";
            }
            return "this JWT is not the anon key (service_role keys must never ship)";
        }

        // The older Supabase keys are JWTs ("xxx.yyy.zzz"). They are also sent as "Authorization: Bearer";
        // the newer sb_publishable_ keys go only in the "apikey" header.
        public static bool IsJwt(string key)
        {
            string trimmed = Trimmed(key);
            string[] parts = trimmed.Split('.');
            return parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0 && parts[2].Length > 0;
        }

        // The middle part of a JWT is base64url text of a small JSON object, e.g. {"role":"anon",...}.
        // Spaces are removed so {"role": "anon"} matches too. "" if it cannot be read.
        private static string JwtPayload(string jwt)
        {
            string part = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
            while (part.Length % 4 != 0)
            {
                part += "=";
            }
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(part)).Replace(" ", "");
            }
            catch (FormatException)
            {
                return "";
            }
        }

        private static string Trimmed(string text)
        {
            return text == null ? "" : text.Trim();
        }
    }
}
