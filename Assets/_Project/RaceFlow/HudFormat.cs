namespace CarcavelosCircuit.RaceFlow
{
    // Builds the HUD's number texts with fixed-width digits, so a running timer does not wobble sideways.
    // Most fonts draw "1" narrower than "8". The rich-text tag <mspace=0.6em>...</mspace> (supported by UI Toolkit
    // labels) gives every character inside the same width. Only digit runs are wrapped: the ":" and "." keep their
    // own narrow width, so "1:23.456" does not look spaced out.
    // Plain C# with one reused char buffer, so each call creates exactly one string (the one the label needs).
    public static class HudFormat
    {
        // The one place to tune digit width. Change it together with the font (spec: 0.55em for Barlow Condensed).
        public const string DigitWidth = "0.6em";

        private const string Open = "<mspace=" + DigitWidth + ">";
        private const string Close = "</mspace>";
        private static readonly char[] Buffer = new char[160];

        // 83456 ms -> "1:23" (minutes and seconds, the big part of the lap timer).
        public static string TimeMain(int totalMilliseconds)
        {
            Split(totalMilliseconds, out int minutes, out int seconds, out _);
            int n = Digits(minutes, 1, 0);
            Buffer[n++] = ':';
            n = Digits(seconds, 2, n);
            return new string(Buffer, 0, n);
        }

        // 83456 ms -> ".456" (the small part of the lap timer).
        public static string TimeFraction(int totalMilliseconds)
        {
            Split(totalMilliseconds, out _, out _, out int milliseconds);
            int n = 0;
            Buffer[n++] = '.';
            n = Digits(milliseconds, 3, n);
            return new string(Buffer, 0, n);
        }

        // 83456 ms -> "1:23.456" in one string (best time, results).
        public static string Time(int totalMilliseconds)
        {
            Split(totalMilliseconds, out int minutes, out int seconds, out int milliseconds);
            int n = Digits(minutes, 1, 0);
            Buffer[n++] = ':';
            n = Digits(seconds, 2, n);
            Buffer[n++] = '.';
            n = Digits(milliseconds, 3, n);
            return new string(Buffer, 0, n);
        }

        // -1204 ms -> "-1.204", 380 -> "+0.380". withUnit adds " s" ("+0.380 s").
        // The sign comes from the rounded value, so it is never "-0.000" (same rule as RaceTimer.FormatDelta).
        public static string Delta(int deltaMilliseconds, bool withUnit)
        {
            int size = deltaMilliseconds < 0 ? -deltaMilliseconds : deltaMilliseconds;
            int n = Write(Open, 0);
            Buffer[n++] = deltaMilliseconds < 0 ? '-' : '+';
            n = WriteNumber(size / 1000, 1, n);
            n = Write(Close, n);
            Buffer[n++] = '.';
            n = Digits(size % 1000, 3, n);
            if (withUnit)
            {
                Buffer[n++] = ' ';
                Buffer[n++] = 's';
            }
            return new string(Buffer, 0, n);
        }

        // 87 -> "87" with fixed-width digits (speed).
        public static string Whole(int value)
        {
            if (value < 0)
            {
                value = 0;
            }
            int n = Digits(value, 1, 0);
            return new string(Buffer, 0, n);
        }

        // 0.84 -> "0.8" with fixed-width digits (air time on the results card). Always a dot, never a comma.
        // Halves round up (1.25 -> "1.3"), negative values show as "0.0".
        public static string OneDecimal(float value)
        {
            if (value < 0f)
            {
                value = 0f;
            }
            int tenths = (int)System.Math.Round(value * 10.0, System.MidpointRounding.AwayFromZero);
            int n = Digits(tenths / 10, 1, 0);
            Buffer[n++] = '.';
            n = Digits(tenths % 10, 1, n);
            return new string(Buffer, 0, n);
        }

        // Removes the <...> tags, e.g. for tests or a plain-text log: "<mspace=0.6em>1</mspace>:..." -> "1:...".
        public static string StripTags(string text)
        {
            int n = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length && n < Buffer.Length; i++)
            {
                char c = text[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (!inTag) Buffer[n++] = c;
            }
            return new string(Buffer, 0, n);
        }

        private static void Split(int totalMilliseconds, out int minutes, out int seconds, out int milliseconds)
        {
            if (totalMilliseconds < 0)
            {
                totalMilliseconds = 0;
            }
            minutes = totalMilliseconds / 60000;
            seconds = (totalMilliseconds / 1000) % 60;
            milliseconds = totalMilliseconds % 1000;
        }

        // Writes a number wrapped in the mspace tag: <mspace=0.6em>23</mspace>.
        private static int Digits(int value, int minDigits, int position)
        {
            position = Write(Open, position);
            position = WriteNumber(value, minDigits, position);
            return Write(Close, position);
        }

        private static int Write(string text, int position)
        {
            for (int i = 0; i < text.Length; i++)
            {
                Buffer[position + i] = text[i];
            }
            return position + text.Length;
        }

        // Writes value with leading zeros up to minDigits (7 with 3 digits = "007"). Returns the position after it.
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
            for (int i = digits - 1; i >= 0; i--)
            {
                Buffer[position + i] = (char)('0' + value % 10);
                value = value / 10;
            }
            return position + digits;
        }
    }
}
