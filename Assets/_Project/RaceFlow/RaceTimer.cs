namespace CarcavelosCircuit.RaceFlow
{
    // Counts the lap time. Plain C# (no MonoBehaviour), so Edit Mode tests can feed it fake frame times.
    public class RaceTimer
    {
        public float ElapsedSeconds { get; private set; }
        public bool IsRunning { get; private set; }

        // Starts from zero (called on GO).
        public void Start()
        {
            ElapsedSeconds = 0f;
            IsRunning = true;
        }

        // Freezes the time (called when the lap is finished).
        public void Stop()
        {
            IsRunning = false;
        }

        // Back to 0:00.000 and not running (called on retry).
        public void Reset()
        {
            ElapsedSeconds = 0f;
            IsRunning = false;
        }

        // Called once per frame with Time.deltaTime. Only counts while running.
        public void Tick(float deltaTime)
        {
            if (IsRunning && deltaTime > 0f)
            {
                ElapsedSeconds += deltaTime;
            }
        }

        // 83.456 seconds -> "1:23.456". We count in whole milliseconds so rounding never shows "0:60.000".
        public static string Format(float seconds)
        {
            if (seconds < 0f)
            {
                seconds = 0f;
            }
            int totalMilliseconds = (int)System.Math.Round(seconds * 1000.0);
            int minutes = totalMilliseconds / 60000;
            int wholeSeconds = (totalMilliseconds / 1000) % 60;
            int milliseconds = totalMilliseconds % 1000;
            return minutes + ":" + wholeSeconds.ToString("00") + "." + milliseconds.ToString("000");
        }

        // A difference to the best time: -1.204 -> "-1.204", 0.5 -> "+0.500". Always with a sign.
        public static string FormatDelta(float deltaSeconds)
        {
            string sign = deltaSeconds < 0f ? "-" : "+";
            int totalMilliseconds = (int)System.Math.Round(System.Math.Abs(deltaSeconds) * 1000.0);
            int wholeSeconds = totalMilliseconds / 1000;
            int milliseconds = totalMilliseconds % 1000;
            return sign + wholeSeconds + "." + milliseconds.ToString("000");
        }
    }
}
