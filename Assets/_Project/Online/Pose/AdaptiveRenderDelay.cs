using System;

namespace CarcavelosCircuit.Online
{
    // How far in the past one remote kart is drawn (its PoseBuffer sample moment = now - Seconds). Scale test, 17 Nov
    // 2026: over real Photon (Lisbon -> Amsterdam -> Lisbon) the newest pose of a remote kart was older than the fixed
    // 0.22 s in 29 to 45 % of the frames (up to 0.9 s once), so the kart had to guess (extrapolate or hold) far too
    // often. A fixed number cannot fit both a PC on cable and a laptop on busy campus Wi-Fi, so every remote kart
    // measures it: each frame we note how old its newest pose is, and the delay becomes the 95th percentile of the
    // last 3 s of these ages plus a small margin, never below Luc's 0.22 s (review, 16 Nov) and never above 0.8 s.
    // It rises at once when the network gets worse (no stutter) and sinks slowly when it gets better (no jumps).
    // An age above OutageSeconds is a pause (Wi-Fi gone, tab hidden), not jitter: it is not counted, PoseBuffer's
    // short guess + hold handles it.
    // Plain C#, tested in Edit Mode (AdaptiveRenderDelayTests).
    public class AdaptiveRenderDelay
    {
        public const double MinSeconds = PoseBuffer.RenderDelaySeconds;
        public const double MaxSeconds = 0.8;
        public const double MarginSeconds = 0.03;
        public const double WindowSeconds = 3.0;
        public const double OutageSeconds = 1.5;
        public const double FallPerSecond = 0.1;     // how fast the delay may shrink, seconds per second
        public const double UpdateEverySeconds = 0.25;
        public const double Percentile = 0.95;
        private const int Capacity = 512;            // ~3 s at 144 fps; older samples drop out of the ring anyway

        private readonly double[] times = new double[Capacity];
        private readonly double[] ages = new double[Capacity];
        private readonly double[] scratch = new double[Capacity];
        private int start;
        private int count;
        private double nextUpdateAt = double.NegativeInfinity;
        private double lastNow = double.NaN;
        private double target = MinSeconds;

        public double Seconds { get; private set; } = MinSeconds;

        // One frame: how old the newest pose of this kart is right now (now - its pose time). Negative ages (a pose
        // stamped a hair ahead of our clock) count as 0.
        public void Add(double now, double newestAge)
        {
            double dt = double.IsNaN(lastNow) ? 0.0 : Math.Max(0.0, now - lastNow);
            lastNow = now;
            if (!double.IsNaN(newestAge) && !double.IsInfinity(newestAge) && newestAge <= OutageSeconds)
            {
                Push(now, Math.Max(0.0, newestAge));
            }
            Drop(now - WindowSeconds);
            if (now >= nextUpdateAt)
            {
                nextUpdateAt = now + UpdateEverySeconds;
                target = Clamp(PercentileOfWindow() + MarginSeconds);
            }
            if (target >= Seconds)
            {
                Seconds = target;                                        // worse network: follow at once
            }
            else
            {
                Seconds = Math.Max(target, Seconds - FallPerSecond * dt); // better network: ease down
            }
        }

        public void Reset()
        {
            start = 0;
            count = 0;
            nextUpdateAt = double.NegativeInfinity;
            lastNow = double.NaN;
            target = MinSeconds;
            Seconds = MinSeconds;
        }

        private void Push(double now, double age)
        {
            if (count == Capacity)
            {
                start = (start + 1) % Capacity;
                count--;
            }
            int at = (start + count) % Capacity;
            times[at] = now;
            ages[at] = age;
            count++;
        }

        private void Drop(double before)
        {
            while (count > 0 && times[start] < before)
            {
                start = (start + 1) % Capacity;
                count--;
            }
        }

        private double PercentileOfWindow()
        {
            if (count == 0)
            {
                return 0.0;
            }
            for (int i = 0; i < count; i++)
            {
                scratch[i] = ages[(start + i) % Capacity];
            }
            Array.Sort(scratch, 0, count);
            int index = (int)Math.Ceiling(Percentile * count) - 1;
            return scratch[Math.Max(0, Math.Min(count - 1, index))];
        }

        private static double Clamp(double seconds)
        {
            return seconds < MinSeconds ? MinSeconds : (seconds > MaxSeconds ? MaxSeconds : seconds);
        }
    }
}
