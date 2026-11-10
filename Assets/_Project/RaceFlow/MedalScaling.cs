using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // Medal times that fit any track. RaceSettings holds the four times for one reference lap length; a longer
    // track gets proportionally longer times (twice the length = twice the time). So a new campus track never
    // shows "Bronze 0:12.000" for a one-minute lap. Plain C#, unit tested.
    public static class MedalScaling
    {
        // Length of one lap measured as straight lines from checkpoint to checkpoint, in driving order,
        // and from the last one back to the start/finish line. Shorter than the real driving line, but measured the
        // same way on every track, which is all the scaling needs. Less than 2 points = 0.
        public static float LoopLength(Vector3[] checkpointPositions)
        {
            if (checkpointPositions == null || checkpointPositions.Length < 2)
            {
                return 0f;
            }
            float total = 0f;
            for (int i = 0; i < checkpointPositions.Length; i++)
            {
                Vector3 next = checkpointPositions[(i + 1) % checkpointPositions.Length]; // the last one goes back to 0
                total += Vector3.Distance(checkpointPositions[i], next);
            }
            return total;
        }

        // The reference times stretched to this track, rounded to a tenth of a second so the HUD target reads
        // "1:23.400" and not "1:23.417". Missing or broken lengths = the reference times unchanged.
        public static float[] ScaleTimes(float[] referenceTimes, float referenceLength, float trackLength)
        {
            if (referenceTimes == null)
            {
                return null;
            }
            float[] scaled = new float[referenceTimes.Length];
            bool canScale = referenceLength > 0f && trackLength > 0f;
            float factor = canScale ? trackLength / referenceLength : 1f;
            for (int i = 0; i < referenceTimes.Length; i++)
            {
                scaled[i] = Mathf.Round(referenceTimes[i] * factor * 10f) / 10f;
            }
            // Rounding must never make two tiers equal (on a very short track): then keep the exact values.
            if (!MedalRules.AreValid(scaled))
            {
                for (int i = 0; i < referenceTimes.Length; i++)
                {
                    scaled[i] = referenceTimes[i] * factor;
                }
            }
            return scaled;
        }
    }
}
