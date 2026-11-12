using UnityEngine;

namespace CarcavelosCircuit.Bots
{
    // LOCAL PROTOTYPE (bots). Looks only: every bot drives the same kart model as the player (Role 3's KartVisual).
    // This gives the painted body parts of one kart their own colour, so six karts can be told apart.
    // The material files are not changed: a MaterialPropertyBlock overrides the colour for this one kart only.
    public static class BotKartPaint
    {
        private const string PaintMaterial = "NK_Paint_Body"; // the body paint of the kart model
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        public static void Paint(GameObject kart, Color colour)
        {
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
                    }
                }
            }
        }
    }
}
