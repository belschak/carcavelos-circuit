using UnityEngine;

namespace CarcavelosCircuit.Online
{
    // The last poses of one remote kart, oldest first, and the pose to draw at any moment (snapshot interpolation).
    // Poses come about 15 times per second, the screen draws 60 times per second, so we show the kart a little in the
    // past (RenderDelaySeconds behind the shared clock) and blend between the two poses around that moment. Then the
    // kart moves smoothly even when packets arrive unevenly.
    // - A pose older than (or as old as) the newest one is dropped: newest wins, a late or doubled packet is ignored.
    // - After the newest pose the kart keeps moving in a straight line for at most MaxExtrapolationSeconds, then it
    //   holds still (a sender whose Wi-Fi drops does not fly off into the sea).
    // Plain C#, tested in Edit Mode (PoseBufferTests).
    public class PoseBuffer
    {
        public const double RenderDelaySeconds = 0.12;
        public const double MaxExtrapolationSeconds = 0.25;
        public const int Capacity = 32; // about 2 s at 15 Hz, more than we ever need

        private readonly KartPose[] poses = new KartPose[Capacity];
        private int count;

        public int Count { get { return count; } }

        // The newest pose time, -1 when empty.
        public double NewestTime { get { return count == 0 ? -1.0 : poses[count - 1].Time; } }

        // Adds a pose. False (nothing added) if it is not newer than the newest pose we have.
        public bool Add(KartPose pose)
        {
            if (count > 0 && pose.Time <= poses[count - 1].Time)
            {
                return false;
            }
            if (count == Capacity)
            {
                // Full: forget the oldest (shift left by one). 32 structs, cheap.
                for (int i = 1; i < Capacity; i++)
                {
                    poses[i - 1] = poses[i];
                }
                count--;
            }
            poses[count] = pose;
            count++;
            return true;
        }

        public void Clear()
        {
            count = 0;
        }

        // The pose to draw at "time" on the shared clock (usually Now - RenderDelaySeconds). False when empty.
        public bool TrySample(double time, out KartPose result)
        {
            result = new KartPose();
            if (count == 0)
            {
                return false;
            }
            // One pose only, or a moment before the oldest pose: show the oldest pose as it is.
            if (count == 1 || time <= poses[0].Time)
            {
                result = poses[0];
                return true;
            }
            KartPose newest = poses[count - 1];
            if (time >= newest.Time)
            {
                result = Extrapolate(poses[count - 2], newest, time);
                return true;
            }
            // Find the two poses around "time" and blend.
            for (int i = 1; i < count; i++)
            {
                if (poses[i].Time >= time)
                {
                    result = Blend(poses[i - 1], poses[i], time);
                    return true;
                }
            }
            result = newest; // not reached: time < newest.Time was checked above
            return true;
        }

        // Between two poses: position and numbers blended, rotation turned smoothly, yes/no values from the nearer pose.
        public static KartPose Blend(KartPose a, KartPose b, double time)
        {
            double span = b.Time - a.Time;
            float t = span <= 0.0 ? 1f : (float)((time - a.Time) / span);
            t = Mathf.Clamp01(t);
            KartPose near = t < 0.5f ? a : b;
            KartPose result = near;
            result.Time = time;
            result.Position = Vector3.Lerp(a.Position, b.Position, t);
            result.Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
            result.Speed = Mathf.Lerp(a.Speed, b.Speed, t);
            result.Steer = Mathf.Lerp(a.Steer, b.Steer, t);
            result.DriftCharge01 = Mathf.Lerp(a.DriftCharge01, b.DriftCharge01, t);
            return result;
        }

        // After the newest pose: keep the last velocity for at most MaxExtrapolationSeconds, then hold.
        public static KartPose Extrapolate(KartPose before, KartPose newest, double time)
        {
            KartPose result = newest;
            double span = newest.Time - before.Time;
            double ahead = time - newest.Time;
            if (ahead > MaxExtrapolationSeconds)
            {
                ahead = MaxExtrapolationSeconds;
            }
            if (span > 0.0001 && ahead > 0.0)
            {
                Vector3 velocity = (newest.Position - before.Position) / (float)span;
                result.Position = newest.Position + velocity * (float)ahead;
            }
            result.Time = newest.Time + (ahead > 0.0 ? ahead : 0.0);
            return result;
        }
    }
}
