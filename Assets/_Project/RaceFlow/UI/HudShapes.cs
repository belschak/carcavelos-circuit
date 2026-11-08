using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // HUD shapes drawn with UI Toolkit's vector API (Painter2D): no textures, no assets, own work.
    // generateVisualContent runs only when the element is marked dirty (value change or resize), not every frame.
    // RaceHud creates them in code and adds them to empty placeholder elements from RaceHud.uxml.

    // Round speed gauge: a 270-degree arc (from bottom-left over the top to bottom-right). Fill = speed / max speed.
    public class SpeedArc : VisualElement
    {
        private static readonly Color TrackColor = new Color(0.918f, 0.918f, 0.925f, 0.18f);
        private static readonly Color TickColor = new Color(0.918f, 0.918f, 0.925f, 0.35f);
        private const float LineWidth = 12f;
        private float value01;
        private Color fillColor = HudColors.White;

        public SpeedArc()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("speed-arc");
            generateVisualContent += Draw;
        }

        // Only asks for a redraw when the change is visible (0.2 percent or a new colour).
        public void Set(float newValue01, Color newColor)
        {
            newValue01 = Mathf.Clamp01(newValue01);
            if (Mathf.Abs(newValue01 - value01) < 0.002f && newColor == fillColor)
            {
                return;
            }
            value01 = newValue01;
            fillColor = newColor;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect rect = contentRect;
            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - LineWidth;
            if (radius <= 0f)
            {
                return;
            }
            painter.lineCap = LineCap.Round;

            // Background track.
            painter.lineWidth = LineWidth;
            painter.strokeColor = TrackColor;
            painter.BeginPath();
            painter.Arc(centre, radius, Angle.Degrees(135f), Angle.Degrees(405f));
            painter.Stroke();

            // Ten small ticks outside the arc, like a dashboard.
            painter.lineWidth = 2f;
            painter.strokeColor = TickColor;
            painter.BeginPath();
            for (int i = 0; i <= 9; i++)
            {
                float angle = (135f + 30f * i) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                painter.MoveTo(centre + direction * (radius + LineWidth * 0.5f + 3f));
                painter.LineTo(centre + direction * (radius + LineWidth * 0.5f + 8f));
            }
            painter.Stroke();

            // The fill.
            if (value01 > 0.001f)
            {
                painter.lineWidth = LineWidth;
                painter.strokeColor = fillColor;
                painter.BeginPath();
                painter.Arc(centre, radius, Angle.Degrees(135f), Angle.Degrees(135f + 270f * value01));
                painter.Stroke();
            }
        }
    }

    // Lisbon "mar largo" pavement wave (calcada portuguesa): a white band with a black sine wave through it.
    // Used as a divider on the start and results screens.
    public class CalcadaWave : VisualElement
    {
        private const float Wavelength = 28f;

        public CalcadaWave()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("calcada");
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
            float amplitude = rect.height * 0.25f;
            float middle = rect.height * 0.5f;

            // White stone band.
            painter.fillColor = HudColors.White;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, 0f));
            painter.LineTo(new Vector2(rect.width, 0f));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.LineTo(new Vector2(0f, rect.height));
            painter.ClosePath();
            painter.Fill();

            // Black stone below a sine wave.
            painter.fillColor = HudColors.Black;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, rect.height));
            for (float x = 0f; x <= rect.width; x += 2f)
            {
                float y = middle + amplitude * Mathf.Sin(x / Wavelength * Mathf.PI * 2f);
                painter.LineTo(new Vector2(x, y));
            }
            painter.LineTo(new Vector2(rect.width, middle + amplitude * Mathf.Sin(rect.width / Wavelength * Mathf.PI * 2f)));
            painter.LineTo(new Vector2(rect.width, rect.height));
            painter.ClosePath();
            painter.Fill();
        }
    }

    // Azulejo tile pattern for the full-screen overlays: per tile a diamond through the edge midpoints, a small
    // inner diamond in the centre and quarter circles in the corners (they join into circles across tiles).
    // Drawn once per resize, shown at low opacity (set in USS).
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
