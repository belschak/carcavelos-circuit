using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Azulejo tile pattern for the full-screen overlays: per tile a diamond through the edge midpoints, a small
    // inner diamond in the centre and quarter circles in the corners (they join into circles across tiles).
    // Drawn once per resize, shown at low opacity (set in USS).
    // Drawn with UI Toolkit's vector API (Painter2D): no textures, no assets, own work. generateVisualContent runs
    // only when the element is marked dirty (value change or resize), not every frame. RaceHud creates it in code
    // and adds it to an empty "...-host" element from RaceHud.uxml.
    public class AzulejoPattern : VisualElement
    {
        private const float Tile = 72f; // bigger tiles = fewer vertices (about 180 tiles at 1280x720)

        public AzulejoPattern()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("azulejo");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            int columns = Mathf.CeilToInt(rect.width / Tile);
            int rows = Mathf.CeilToInt(rect.height / Tile);
            float half = Tile * 0.5f;

            painter.lineWidth = 2f;
            painter.strokeColor = HudColors.White;
            painter.BeginPath();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Vector2 corner = new Vector2(column * Tile, row * Tile);
                    Vector2 centre = corner + new Vector2(half, half);

                    // Diamond through the four edge midpoints.
                    painter.MoveTo(corner + new Vector2(half, 0f));
                    painter.LineTo(corner + new Vector2(Tile, half));
                    painter.LineTo(corner + new Vector2(half, Tile));
                    painter.LineTo(corner + new Vector2(0f, half));
                    painter.LineTo(corner + new Vector2(half, 0f));

                    // Small inner diamond (the tile's "flower"), kept to straight lines so the mesh stays small.
                    painter.MoveTo(centre + new Vector2(0f, -8f));
                    painter.LineTo(centre + new Vector2(8f, 0f));
                    painter.LineTo(centre + new Vector2(0f, 8f));
                    painter.LineTo(centre + new Vector2(-8f, 0f));
                    painter.LineTo(centre + new Vector2(0f, -8f));

                    // Quarter circle in the top-left corner of every tile = full circles where four tiles meet.
                    painter.MoveTo(corner + new Vector2(12f, 0f));
                    painter.Arc(corner, 12f, Angle.Degrees(0f), Angle.Degrees(90f));
                }
            }
            painter.Stroke();
        }
    }
}
