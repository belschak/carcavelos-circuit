using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // One-line console messages about the online race, all starting with "CCNET " (M2 scale test, 16 Nov 2026).
    // In the Web build Debug.Log lands in the browser console, so a test script reads every client's console and checks
    // that all screens of a room show the same results, that GO lands at the same moment and how smooth the other
    // karts are. A handful of lines per race plus one STAT line every 10 s: nothing a player notices.
    // The text builders are plain C# (tested in OnlineTestLogTests); Write() only hands the line to Debug.Log.
    public static class OnlineTestLog
    {
        public const string Prefix = "CCNET ";
        public const float StatEverySeconds = 10f;

        public static void Write(string line)
        {
            Debug.Log(Prefix + line);
        }

        // Names go into key=value lines: no spaces, no separators of this format.
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "-";
            }
            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                sb.Append(char.IsWhiteSpace(c) || c == '|' || c == ':' || c == '=' ? '_' : c);
            }
            return sb.ToString();
        }

        public static string Num(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "nan";
            }
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        public static string Room(TimeOfDay room)
        {
            return room == TimeOfDay.Sunset ? "sunset" : "noon";
        }

        // The finish order as one token: name:seconds for a finish, name:RACING, name:DNF; in the rows' order.
        public static string Order(IReadOnlyList<FinishRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return "-";
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('|');
                }
                FinishRow row = rows[i];
                sb.Append(Clean(row.Name)).Append('#').Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append(':');
                switch (row.State)
                {
                    case FinishState.Finished:
                        sb.Append(Num(row.Seconds, 3));
                        break;
                    case FinishState.Racing:
                        sb.Append("RACING");
                        break;
                    default:
                        sb.Append("DNF");
                        break;
                }
            }
            return sb.ToString();
        }

        public static string Result(TimeOfDay room, int raceId, string myName, IReadOnlyList<FinishRow> rows)
        {
            return "RESULT room=" + Room(room) + " race=" + raceId.ToString(CultureInfo.InvariantCulture)
                + " me=" + Clean(myName) + " order=" + Order(rows);
        }
    }

    // How smooth the other karts are on this screen: a remote kart is "starved" in a frame when its newest pose is
    // older than the render delay, so PoseBuffer has to guess (extrapolate or hold) instead of blending real poses.
    // The director feeds it every frame and prints it in the STAT and RESULT lines. Plain C#.
    public class ProxySmoothness
    {
        public int Samples { get; private set; }
        public int Starved { get; private set; }
        public double MaxAgeSeconds { get; private set; }

        public void Add(double newestPoseTime, double now)
        {
            if (newestPoseTime < 0.0)
            {
                return; // no pose yet
            }
            double age = now - newestPoseTime;
            Samples++;
            if (age > PoseBuffer.RenderDelaySeconds)
            {
                Starved++;
            }
            if (age > MaxAgeSeconds)
            {
                MaxAgeSeconds = age;
            }
        }

        public double StarvedPercent
        {
            get { return Samples == 0 ? 0.0 : 100.0 * Starved / Samples; }
        }

        public string Line()
        {
            return "starved_pct=" + OnlineTestLog.Num(StarvedPercent, 1) + " max_age_ms=" + OnlineTestLog.Num(MaxAgeSeconds * 1000.0, 0)
                + " samples=" + Samples.ToString(CultureInfo.InvariantCulture);
        }

        public void Reset()
        {
            Samples = 0;
            Starved = 0;
            MaxAgeSeconds = 0.0;
        }
    }
}
