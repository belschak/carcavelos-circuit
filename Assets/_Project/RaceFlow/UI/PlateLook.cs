using UnityEngine;

namespace CarcavelosCircuit.RaceFlow
{
    // What one plate picture looks like (PlateArt.Plate): colours and sizes in texture pixels.
    // A plain struct so Editor/HudArtBuilder can list every plate in one place.
    public struct PlateLook
    {
        public Color FillTop;      // gradient colour at the top of the body
        public Color FillBottom;   // ... and at the bottom
        public Color Stroke;       // the light outline round the body
        public Color Shadow;       // the hard shadow (or coloured lip) straight below the body
        public float RadiusPx;     // corner radius
        public float StrokePx;     // outline width
        public float BevelPx;      // height of the bright line under the top edge (0 = none)
        public float ShadowPx;     // how far the shadow sticks out below the body

        public PlateLook(Color fillTop, Color fillBottom, Color stroke, Color shadow, float radiusPx, float strokePx, float bevelPx, float shadowPx)
        {
            FillTop = fillTop;
            FillBottom = fillBottom;
            Stroke = stroke;
            Shadow = shadow;
            RadiusPx = radiusPx;
            StrokePx = strokePx;
            BevelPx = bevelPx;
            ShadowPx = shadowPx;
        }
    }
}
