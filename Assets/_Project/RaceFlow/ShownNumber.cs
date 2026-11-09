namespace CarcavelosCircuit.RaceFlow
{
    // Remembers the number one HUD label is showing right now (milliseconds, km/h, countdown digit ...).
    // The HUD builds a new string only when Changed says the number is different. Building strings every frame
    // creates garbage, and in a Web build the garbage collector cleans up all at once, which shows as a small hitch.
    // Plain C#, so Edit Mode tests can check it.
    public class ShownNumber
    {
        private int shown;
        private bool hasValue;

        // True the first time and whenever value differs from what is on screen. Then value counts as shown.
        public bool Changed(int value)
        {
            if (hasValue && value == shown)
            {
                return false;
            }
            shown = value;
            hasValue = true;
            return true;
        }

        // The next Changed call returns true again, whatever the value (e.g. after the label was reused).
        public void Forget()
        {
            hasValue = false;
        }
    }
}
