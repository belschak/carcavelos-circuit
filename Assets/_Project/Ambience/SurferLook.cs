using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // What one made-up surfer looks like: colours (already in the colour space the shader expects), a hair style and
    // a wetsuit style. Every surfer gets a different one, so they can be told apart from the racing line.
    public struct SurferLook
    {
        public Color32 Suit;      // wetsuit base: legs, hips, sleeves (dark slate / navy, never pure black)
        public Color32 SuitTop;   // second tone: the chest and shoulder panel
        public Color32 Accent;    // the bright stripe on the suit, the stripe down the board and the fins
        public Color32 Skin;
        public Color32 Hair;
        public Color32 Deck;      // top of the board
        public int HairStyle;     // 0 short, 1 long with a pony tail, 2 bun, 3 curly (bigger)
        public int SuitStyle;     // 0 full suit, 1 short arms, 2 short legs ("shorty")
    }
}
