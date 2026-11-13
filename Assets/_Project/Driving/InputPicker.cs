using CarcavelosCircuit.Shared;

namespace CarcavelosCircuit.Driving
{
    // Task D8: who drives the kart this frame? Plain C# with no Unity objects, so it can be unit tested.
    // 1. Input switched off (countdown, results, pause): nobody drives, not even a bot.
    // 2. A bot is plugged in (a source): the kart does what the bot says, kept inside -1..1.
    // 3. Otherwise: the keyboard (and the phone buttons, which act like keys).
    public static class InputPicker
    {
        public static KartInput Pick(bool inputActive, IKartInputSource source, KartInput keyboard)
        {
            if (!inputActive)
            {
                return KartInput.None; // the source is not even asked
            }
            if (source != null)
            {
                return source.ReadInput().Clamped(); // a bot's maths error must never beat a player
            }
            return keyboard;
        }
    }
}
