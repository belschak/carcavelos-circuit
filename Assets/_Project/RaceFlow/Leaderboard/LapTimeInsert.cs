using System;

namespace CarcavelosCircuit.RaceFlow
{
    // The body of one "save my time" request to the database (task R10): {"track":"...","name":"...","seconds":61.234}.
    // Built with JsonUtility, so quotes or backslashes can never break out of the text (they are refused by
    // PlayerNameRules anyway). Field names = the table's columns.
    [Serializable]
    public class LapTimeInsert
    {
        public string track;
        public string name;
        public float seconds;
    }
}
