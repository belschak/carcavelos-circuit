namespace CarcavelosCircuit.RaceFlow
{
    // Why a player name was refused (task R10). None = the name is fine. PlayerNameRules.Message turns it into the
    // plain words the save form shows.
    public enum NameProblem
    {
        None,
        Empty,         // nothing typed, or only spaces
        TooShort,      // 1 character
        TooLong,       // more than 16 characters
        BadCharacter,  // something other than letters, digits, space, - and _ (emoji, < > ' ; ...)
        Blocked,       // on the small rude-word list
    }
}
