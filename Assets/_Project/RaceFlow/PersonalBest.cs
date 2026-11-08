using System.Globalization;

namespace CarcavelosCircuit.RaceFlow
{
    // Remembers the best lap time and the time at every checkpoint of that best lap,
    // so the HUD can show "-0.412" (ahead of your best) or "+1.030" (behind) at each checkpoint.
    public class PersonalBest
    {
        // Best lap time in seconds, or -1 when nobody has finished a lap yet.
        public float BestSeconds { get; private set; } = -1f;

        public bool HasRecord
        {
            get { return BestSeconds > 0f; }
        }

        private readonly float[] bestSplits;     // checkpoint times of the best lap (-1 = unknown)
        private readonly float[] currentSplits;  // checkpoint times of the lap we are driving now

        public PersonalBest(int checkpointCount)
        {
            int count = checkpointCount < 0 ? 0 : checkpointCount;
            bestSplits = new float[count];
            currentSplits = new float[count];
            Fill(bestSplits, -1f);
            Fill(currentSplits, -1f);
        }

        // A new attempt: forget the splits of the last attempt (the best lap stays).
        public void StartRun()
        {
            Fill(currentSplits, -1f);
        }

        // Stores the time at a checkpoint. Returns true and the difference to the best lap if we have one.
        public bool RecordSplit(int checkpointIndex, float timeSeconds, out float deltaToBest)
        {
            deltaToBest = 0f;
            if (checkpointIndex < 0 || checkpointIndex >= currentSplits.Length)
            {
                return false;
            }
            currentSplits[checkpointIndex] = timeSeconds;
            if (bestSplits[checkpointIndex] < 0f)
            {
                return false;
            }
            deltaToBest = timeSeconds - bestSplits[checkpointIndex];
            return true;
        }

        // The lap is over. Returns true if it is a new record (and then remembers its splits too).
        public bool FinishRun(float lapSeconds)
        {
            if (lapSeconds <= 0f)
            {
                return false;
            }
            if (HasRecord && lapSeconds >= BestSeconds)
            {
                return false;
            }
            BestSeconds = lapSeconds;
            for (int i = 0; i < bestSplits.Length; i++)
            {
                bestSplits[i] = currentSplits[i];
            }
            return true;
        }

        // Turns the record into text for PlayerPrefs, e.g. "41.250|10.5;22.25;-1".
        // InvariantCulture = always a dot as decimal sign (a Portuguese laptop would write a comma).
        public string ToSaveString()
        {
            string text = BestSeconds.ToString(CultureInfo.InvariantCulture) + "|";
            for (int i = 0; i < bestSplits.Length; i++)
            {
                if (i > 0)
                {
                    text += ";";
                }
                text += bestSplits[i].ToString(CultureInfo.InvariantCulture);
            }
            return text;
        }

        // The opposite of ToSaveString. Broken or old text simply gives "no record yet".
        public static PersonalBest FromSaveString(string text, int checkpointCount)
        {
            PersonalBest result = new PersonalBest(checkpointCount);
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            string[] parts = text.Split('|');
            float best;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out best) || best <= 0f)
            {
                return result;
            }
            result.BestSeconds = best;

            if (parts.Length > 1)
            {
                string[] splits = parts[1].Split(';');
                // Only use the splits if the track still has the same number of checkpoints.
                if (splits.Length == result.bestSplits.Length)
                {
                    for (int i = 0; i < splits.Length; i++)
                    {
                        float split;
                        if (float.TryParse(splits[i], NumberStyles.Float, CultureInfo.InvariantCulture, out split))
                        {
                            result.bestSplits[i] = split;
                        }
                    }
                }
            }
            return result;
        }

        private static void Fill(float[] values, float value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = value;
            }
        }
    }
}
