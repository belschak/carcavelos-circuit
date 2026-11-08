using System;

namespace CarcavelosCircuit.RaceFlow
{
    // Counts the lap time. Plain C# (no MonoBehaviour), so Edit Mode tests can feed it fake times.
    // Two clocks on purpose:
    // - ElapsedSeconds adds up the frame times. It is only the live timer on the HUD while driving.
    // - The official lap time is "finish timestamp minus start timestamp" (see Stop). Adding up thousands of float
    //   frame times drifts by a few milliseconds, one subtraction of two double timestamps does not.
    public class RaceTimer
    {
        public float ElapsedSeconds { get; private set; }
        public bool IsRunning { get; private set; }

        private double startTime;

        // One shared buffer for Format and FormatDelta, so building a time text creates only the final string.
        private static readonly char[] Buffer = new char[16];

        // Starts from zero (called on GO). startTime = Time.timeAsDouble of the GO frame.
        public void Start(double startTime)
        {
            this.startTime = startTime;
            ElapsedSeconds = 0f;
            IsRunning = true;
        }

        // Freezes the time (called at the finish line). stopTime = Time.timeAsDouble at the finish trigger;
        // inside a physics callback like OnTriggerEnter Unity returns the time of that physics step.
        // From here on ElapsedSeconds is the official lap time from the two timestamps.
        public void Stop(double stopTime)
        {
            if (!IsRunning)
            {
                return;
            }
            IsRunning = false;
            ElapsedSeconds = SecondsSinceStart(stopTime);
        }

        // Back to 0:00.000 and not running (called on retry).
        public void Reset()
        {
            ElapsedSeconds = 0f;
            IsRunning = false;
        }

        // Called once per frame with Time.deltaTime. Only counts while running. Only for the live display.
        public void Tick(float deltaTime)
        {
            if (IsRunning && deltaTime > 0f)
            {
                ElapsedSeconds += deltaTime;
            }
        }

        // Exact time since GO from the timestamps (used for checkpoint splits). Never negative.
        public float SecondsSinceStart(double now)
        {
            double seconds = now - startTime;
            return seconds > 0.0 ? (float)seconds : 0f;
        }

        // Seconds to whole milliseconds, rounded once. The HUD compares these ints to see if a text must change.
        public static int ToMilliseconds(float seconds)
        {
            return (int)Math.Round(seconds * 1000.0);
        }

        // 83.456 seconds -> "1:23.456". We count in whole milliseconds so rounding never shows "0:60.000".
        public static string Format(float seconds)
        {
            return FormatMilliseconds(ToMilliseconds(seconds));
        }

        // 83456 ms -> "1:23.456". Negative times show as 0:00.000.
        public static string FormatMilliseconds(int totalMilliseconds)
        {
            if (totalMilliseconds < 0)
            {
                totalMilliseconds = 0;
            }
            int minutes = totalMilliseconds / 60000;
            int wholeSeconds = (totalMilliseconds / 1000) % 60;
            int milliseconds = totalMilliseconds % 1000;

            int length = WriteNumber(minutes, 1, 0);
            Buffer[length] = ':';
            length = WriteNumber(wholeSeconds, 2, length + 1);
            Buffer[length] = '.';
            length = WriteNumber(milliseconds, 3, length + 1);
            return new string(Buffer, 0, length);
        }

        // A difference to the best time: -1.204 -> "-1.204", 0.5 -> "+0.500". Always with a sign.
        public static string FormatDelta(float deltaSeconds)
        {
            return FormatDeltaMilliseconds(ToMilliseconds(deltaSeconds));
        }

        // -1204 ms -> "-1.204". The sign comes from the rounded value, so -0.0004 s shows "+0.000", never "-0.000".
        public static string FormatDeltaMilliseconds(int deltaMilliseconds)
        {
            Buffer[0] = deltaMilliseconds < 0 ? '-' : '+';
            int size = deltaMilliseconds < 0 ? -deltaMilliseconds : deltaMilliseconds;
            int length = WriteNumber(size / 1000, 1, 1);
            Buffer[length] = '.';
            length = WriteNumber(size % 1000, 3, length + 1);
            return new string(Buffer, 0, length);
        }

        // Writes value into Buffer at position, with leading zeros up to minDigits (7 with 3 digits = "007").
        // Returns the position right after the last digit.
        private static int WriteNumber(int value, int minDigits, int position)
        {
            int digits = 1;
            for (int rest = value / 10; rest > 0; rest = rest / 10)
            {
                digits++;
            }
            if (digits < minDigits)
            {
                digits = minDigits;
            }
            // Fill from the right: the last digit is value % 10, then divide by 10 for the next one.
            for (int i = digits - 1; i >= 0; i--)
            {
                Buffer[position + i] = (char)('0' + value % 10);
                value = value / 10;
            }
            return position + digits;
        }
    }
}
