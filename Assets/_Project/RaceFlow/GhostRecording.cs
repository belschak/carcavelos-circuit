using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // One recorded lap for the ghost kart: the kart's position and rotation every 0.05 s (20 times per second),
    // from GO to the finish. Plain C# (no MonoBehaviour), so Edit Mode tests can feed it fake frames.
    // - Record(time, position, rotation) is called every frame while racing; it stores a sample at every 0.05 s mark.
    // - Sample(time) gives the pose at any moment, smoothly blended between the two nearest samples.
    // - ToSaveString / FromSaveString turn it into a short text for PlayerPrefs (or the shipped Dean's ghost file).
    public class GhostRecording
    {
        public const float SampleInterval = 0.05f; // 20 samples per second
        public const int MaxSamples = 2400;         // 120 s; a longer lap is not kept as a ghost (save size)

        // A jump longer than this between two samples is a teleport (R or a fall), never driving:
        // 8 m in 0.05 s would be 576 km/h. The ghost then jumps too instead of flying through a wall.
        public const float TeleportMetres = 8f;

        // First part of every save string. Raise the number if the format ever changes, old saves are then ignored.
        private const string FormatTag = "G1";

        // Which track and checkpoint count the lap was driven on; a ghost from another layout is ignored.
        public string TrackName { get; private set; }
        public int CheckpointCount { get; private set; }

        // The official lap time of this lap, set by the recorder at the finish (-1 = not finished).
        public float LapSeconds { get; set; } = -1f;

        public int Count { get { return positions.Count; } }

        // Time of the last sample. 0 for one sample, -1 when empty.
        public float Duration { get { return Count == 0 ? -1f : (Count - 1) * SampleInterval; } }

        public bool IsFull { get { return Count >= MaxSamples; } }

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Quaternion> rotations = new List<Quaternion>();

        // The pose of the frame before, so a sample that falls between two frames can be blended exactly.
        private float lastFrameTime;
        private Vector3 lastFramePosition;
        private Quaternion lastFrameRotation;

        public GhostRecording(string trackName, int checkpointCount)
        {
            TrackName = trackName == null ? "" : trackName;
            CheckpointCount = checkpointCount;
        }

        // Starts an empty recording (keeps the lists' memory, so a retry creates no garbage).
        public void Clear()
        {
            positions.Clear();
            rotations.Clear();
            LapSeconds = -1f;
        }

        // Called every frame while racing, with the time since GO. Frames come at uneven times (16 ms, 33 ms ...),
        // samples must be exactly 0.05 s apart: so every sample mark between the last frame and this one gets a pose
        // blended between the two frames.
        public void Record(float time, Vector3 position, Quaternion rotation)
        {
            if (time < 0f || IsFull)
            {
                return;
            }
            if (Count == 0)
            {
                AddSample(position, rotation); // the first frame is sample 0 (time 0, the GO frame)
                RememberFrame(time, position, rotation);
                return;
            }
            if (time <= lastFrameTime)
            {
                return; // a paused frame (or an old time): nothing new to store
            }

            bool teleported = (position - lastFramePosition).sqrMagnitude > TeleportMetres * TeleportMetres;
            float nextSampleTime = Count * SampleInterval;
            while (nextSampleTime <= time && !IsFull)
            {
                // How far between the last frame (0) and this frame (1) the sample mark lies.
                float t = (nextSampleTime - lastFrameTime) / (time - lastFrameTime);
                if (teleported)
                {
                    t = 1f; // never store a pose halfway through a teleport
                }
                AddSample(Vector3.Lerp(lastFramePosition, position, t), Quaternion.Slerp(lastFrameRotation, rotation, t));
                nextSampleTime = Count * SampleInterval;
            }
            RememberFrame(time, position, rotation);
        }

        // The pose at any time since GO. Before the first sample = the first pose, after the last = the last pose.
        // False (and a zero pose) for an empty recording.
        public bool Sample(float time, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (Count == 0)
            {
                return false;
            }
            if (time <= 0f || Count == 1)
            {
                position = positions[0];
                rotation = rotations[0];
                return true;
            }
            if (time >= Duration)
            {
                position = positions[Count - 1];
                rotation = rotations[Count - 1];
                return true;
            }

            // Example: time 1.27 s -> between sample 25 (1.25 s) and 26 (1.30 s), 40 % of the way.
            float exact = time / SampleInterval;
            int index = Mathf.FloorToInt(exact);
            if (index > Count - 2)
            {
                index = Count - 2; // float rounding right at the end
            }
            float t = exact - index;
            Vector3 a = positions[index];
            Vector3 b = positions[index + 1];
            if ((b - a).sqrMagnitude > TeleportMetres * TeleportMetres)
            {
                t = t < 0.5f ? 0f : 1f; // teleport between these two samples: jump, do not glide
            }
            position = Vector3.Lerp(a, b, t);
            rotation = Quaternion.Slerp(rotations[index], rotations[index + 1], t);
            return true;
        }

        // How fast and where the ghost moved at the very end (metres per second), so after its last sample it can
        // roll on a little while fading out instead of freezing on the finish line.
        public Vector3 EndVelocity()
        {
            if (Count < 2)
            {
                return Vector3.zero;
            }
            Vector3 step = positions[Count - 1] - positions[Count - 2];
            if (step.sqrMagnitude > TeleportMetres * TeleportMetres)
            {
                return Vector3.zero;
            }
            return step / SampleInterval;
        }

        // Text for PlayerPrefs, for example "G1|Track_Campus|8|41.25|840|120,30,-450,0,900,0;95,0,12,0,4,0;...".
        // Header: format, track, checkpoint count, lap time, sample count. Then one group per sample:
        // position in centimetres and rotation (pitch, yaw, roll) in tenths of a degree, as whole numbers.
        // The first group is the real value, every later group only the CHANGE since the sample before
        // ("95" instead of "3245"): about 20 characters per sample, a 60 s lap is roughly 25 KB.
        // InvariantCulture everywhere: a Portuguese laptop must not write a different minus sign or a comma.
        public string ToSaveString()
        {
            StringBuilder text = new StringBuilder(64 + Count * 24);
            text.Append(FormatTag).Append('|');
            text.Append(TrackName).Append('|');
            text.Append(CheckpointCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            text.Append(LapSeconds.ToString("0.000", CultureInfo.InvariantCulture)).Append('|');
            text.Append(Count.ToString(CultureInfo.InvariantCulture)).Append('|');

            int[] previous = new int[6];
            int[] current = new int[6];
            for (int i = 0; i < Count; i++)
            {
                ToWholeNumbers(positions[i], rotations[i], current);
                if (i > 0)
                {
                    text.Append(';');
                }
                for (int k = 0; k < 6; k++)
                {
                    int value = current[k];
                    if (i > 0)
                    {
                        value = k < 3 ? current[k] - previous[k] : AngleChange(previous[k], current[k]);
                    }
                    if (k > 0)
                    {
                        text.Append(',');
                    }
                    text.Append(value.ToString(CultureInfo.InvariantCulture));
                    previous[k] = current[k];
                }
            }
            return text.ToString();
        }

        // The opposite of ToSaveString. Returns null for empty or broken text, an old format, or a ghost that was
        // recorded on another track or with another number of checkpoints (the track was changed since).
        public static GhostRecording FromSaveString(string text, string trackName, int checkpointCount)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] parts = text.Trim().Split('|');
            if (parts.Length != 6 || parts[0] != FormatTag)
            {
                return null;
            }
            if (parts[1] != (trackName == null ? "" : trackName))
            {
                return null;
            }
            int savedCheckpoints;
            float lapSeconds;
            int count;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out savedCheckpoints)
                || savedCheckpoints != checkpointCount
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out lapSeconds)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                || count < 1 || count > MaxSamples)
            {
                return null;
            }

            string[] groups = parts[5].Split(';');
            if (groups.Length != count)
            {
                return null;
            }
            GhostRecording result = new GhostRecording(trackName, checkpointCount);
            result.LapSeconds = lapSeconds;
            int[] values = new int[6]; // running totals: first group = real values, later groups = changes
            for (int i = 0; i < count; i++)
            {
                string[] numbers = groups[i].Split(',');
                if (numbers.Length != 6)
                {
                    return null;
                }
                for (int k = 0; k < 6; k++)
                {
                    int number;
                    if (!int.TryParse(numbers[k], NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        return null;
                    }
                    values[k] = i == 0 ? number : values[k] + number;
                }
                Vector3 position = new Vector3(values[0] * 0.01f, values[1] * 0.01f, values[2] * 0.01f);
                Quaternion rotation = Quaternion.Euler(values[3] * 0.1f, values[4] * 0.1f, values[5] * 0.1f);
                result.AddSample(position, rotation);
            }
            return result;
        }

        private void AddSample(Vector3 position, Quaternion rotation)
        {
            positions.Add(position);
            rotations.Add(rotation);
        }

        private void RememberFrame(float time, Vector3 position, Quaternion rotation)
        {
            lastFrameTime = time;
            lastFramePosition = position;
            lastFrameRotation = rotation;
        }

        // Position in whole centimetres, rotation (pitch, yaw, roll) in whole tenths of a degree (0..3599).
        private static void ToWholeNumbers(Vector3 position, Quaternion rotation, int[] into)
        {
            Vector3 euler = rotation.eulerAngles;
            into[0] = Mathf.RoundToInt(position.x * 100f);
            into[1] = Mathf.RoundToInt(position.y * 100f);
            into[2] = Mathf.RoundToInt(position.z * 100f);
            into[3] = Mathf.RoundToInt(euler.x * 10f) % 3600;
            into[4] = Mathf.RoundToInt(euler.y * 10f) % 3600;
            into[5] = Mathf.RoundToInt(euler.z * 10f) % 3600;
        }

        // The short way round between two angles in tenths of a degree: 359.5 -> 0.5 degrees is +10, not -3590.
        private static int AngleChange(int from, int to)
        {
            int change = (to - from) % 3600; // -3599 .. 3599
            if (change > 1800)
            {
                change -= 3600;
            }
            else if (change < -1800)
            {
                change += 3600;
            }
            return change;
        }
    }
}
