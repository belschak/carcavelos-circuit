using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Round speed gauge: a 270-degree arc (from bottom-left over the top to bottom-right). Fill = speed / max speed.
    // Drawn with UI Toolkit's vector API (Painter2D): no textures, no assets, own work. generateVisualContent runs
    // only when the element is marked dirty (value change or resize), not every frame. RaceHud creates it in code
    // and adds it to an empty "...-host" element from RaceHud.uxml.
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
}
