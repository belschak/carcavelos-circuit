using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9: a small filled triangle pointing up (turn it with style.rotate), drawn with Painter2D.
    // The player's heading arrow on the minimap and the "gained / lost a place" chevron next to the position.
    // Own shape instead of a text arrow: Unity's default font has no reliable triangle glyph.
    public class TriangleMark : VisualElement
    {
        private Color fill;
        private readonly Color outline;
        private readonly float outlineWidth;

        public TriangleMark(Color fill, Color outline, float outlineWidth)
        {
            this.fill = fill;
            this.outline = outline;
            this.outlineWidth = outlineWidth;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetFill(Color colour)
        {
            if (colour != fill)
            {
                fill = colour;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            float inset = outlineWidth * 0.5f;
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.width * 0.5f, inset));
            painter.LineTo(new Vector2(rect.width - inset, rect.height - inset));
            painter.LineTo(new Vector2(rect.width * 0.5f, rect.height * 0.74f)); // notch at the back = an arrow head, not a flat triangle
            painter.LineTo(new Vector2(inset, rect.height - inset));
            painter.ClosePath();
            painter.fillColor = fill;
            painter.Fill();
            if (outlineWidth > 0f)
            {
                painter.lineWidth = outlineWidth;
                painter.strokeColor = outline;
                painter.Stroke();
            }
        }
    }
}
