namespace CarcavelosCircuit.RaceFlow
{
    // What kind of message the hint chip shows. The HUD picks the chip colour and icon from it.
    public enum HintKind
    {
        Warning,  // something went wrong ("Missed a checkpoint!"): orange chip with "!"
        Tip,      // coach tip ("Hold SPACE while steering to drift"): blue chip with "i"
        Praise    // coach praise ("Nice! ..."): sand chip with "+"
    }
}
