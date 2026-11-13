using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Task V10, looks only: every bot drives the same kart model as the player (Role 3's KartVisual), so this paints
    // the body of one kart in the bot's colour, and six karts can be told apart. The material file is not changed:
    // a MaterialPropertyBlock overrides the colour for this one kart only, the player's kart keeps its own paint.
    public static class BotKartPaint
    {
        private const string PaintMaterial = "NK_Paint_Body"; // the kart model's body paint (Visuals/Kart/Materials)
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        // Returns how many body parts got the colour (0 = the model has no NK_Paint_Body material).
        public static int Paint(GameObject kart, Color colour)
        {
            if (kart == null)
            {
                return 0;
            }
            int painted = 0;
            Renderer[] renderers = kart.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] materials = renderers[r].sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null && materials[m].name.StartsWith(PaintMaterial))
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderers[r].GetPropertyBlock(block, m);
                        block.SetColor(BaseColour, colour);
                        renderers[r].SetPropertyBlock(block, m);
                        painted++;
                    }
                }
            }
            return painted;
        }
    }
}
