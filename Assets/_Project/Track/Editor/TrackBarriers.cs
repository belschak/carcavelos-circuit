using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // V21: reads the optional list of extra barrier lines from Role 3's track.json:
    //   "barriers": [ { "name": "start plaza median", "points": [[x, y, z], [x, y, z], ...] }, ... ]
    // (Unity metres, y = height of the paving, each line runs along the middle of its wall).
    // Unity's JsonUtility cannot read lists inside lists, so this small reader walks through the text itself.
    // No "barriers" in the file, an empty list or broken entries: those are simply left out, nothing fails.
    public static class TrackBarriers
    {
        public static List<Vector3[]> Read(string json)
        {
            List<Vector3[]> lines = new List<Vector3[]>();
            int key = json == null ? -1 : json.IndexOf("\"barriers\"");
            int start = key < 0 ? -1 : json.IndexOf('[', key);
            if (start < 0)
            {
                return lines;
            }
            // Depth 1 = the list of barriers, 2 = the points of one barrier, 3 = the three numbers of one point.
            int depth = 0;
            bool inText = false;
            int numberStart = -1;
            bool broken = false;
            List<float> numbers = new List<float>();
            List<Vector3> line = new List<Vector3>();
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (inText)
                {
                    if (c == '\\')
                    {
                        i++; // an escaped character inside a name
                    }
                    else if (c == '"')
                    {
                        inText = false;
                    }
                    continue;
                }
                bool numberChar = char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E';
                if (numberChar && depth == 3)
                {
                    if (numberStart < 0)
                    {
                        numberStart = i;
                    }
                    continue;
                }
                if (numberStart >= 0)
                {
                    float value;
                    if (float.TryParse(json.Substring(numberStart, i - numberStart), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    {
                        numbers.Add(value);
                    }
                    else
                    {
                        broken = true;
                    }
                    numberStart = -1;
                }
                if (c == '"')
                {
                    inText = true;
                }
                else if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    if (depth == 3)
                    {
                        if (numbers.Count == 3 && !broken)
                        {
                            line.Add(new Vector3(numbers[0], numbers[1], numbers[2]));
                        }
                        numbers.Clear();
                        broken = false;
                    }
                    else if (depth == 2)
                    {
                        if (line.Count >= 2)
                        {
                            lines.Add(line.ToArray()); // a wall needs at least two points
                        }
                        line.Clear();
                    }
                    depth--;
                    if (depth == 0)
                    {
                        break; // end of the barrier list
                    }
                }
            }
            return lines;
        }
    }
}
