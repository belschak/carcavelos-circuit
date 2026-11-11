using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task R9: a line on the minimap, drawn with UI Toolkit's vector API (Painter2D, like SpeedArc): no textures, own work.
    // Two uses (PositionsHud makes both):
    // - the road: the whole closed racing line, a dark outline under a light road, plus a chequered start/finish bar;
    // - the trail: the part of the lap the player has driven, in the player's colour on top of the road.
    // generateVisualContent only runs after MarkDirtyRepaint, so the road is drawn once and the trail only when it grows.
    public class MinimapLine : VisualElement
    {
        private static readonly Color OutlineColour = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RoadColour = new Color(0.918f, 0.918f, 0.925f, 0.92f); // --cc-white

        private readonly bool isRoad;
        private readonly Color trailColour;
        private List<Vector2> points = new List<Vector2>(); // map pixels, in driving order, first = start line
        private int drawCount;                               // trail only: how many points to draw (from the start)

        // isRoad = true: closed road with outline and start bar. false: open trail in trailColour.
        public MinimapLine(bool isRoad, Color trailColour)
        {
            this.isRoad = isRoad;
            this.trailColour = trailColour;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            generateVisualContent += Draw;
        }

        public void SetPoints(List<Vector2> mapPoints)
        {
            points = mapPoints != null ? mapPoints : new List<Vector2>();
            MarkDirtyRepaint();
        }

        // Trail: draw the first count points. Only redraws when the number changes.
        public void SetDrawCount(int count)
        {
            count = Mathf.Clamp(count, 0, points.Count);
            if (count != drawCount)
            {
                drawCount = count;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            if (points.Count < 2)
            {
                return;
            }
            Painter2D painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            if (isRoad)
            {
                StrokePath(painter, points.Count, true, 11f, OutlineColour);
                StrokePath(painter, points.Count, true, 6f, RoadColour);
                DrawStartBar(painter);
            }
            else if (drawCount >= 2)
            {
                StrokePath(painter, drawCount, false, 3.5f, trailColour);
            }
        }

        private void StrokePath(Painter2D painter, int count, bool closed, float width, Color colour)
        {
            painter.lineWidth = width;
            painter.strokeColor = colour;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < count; i++)
            {
                painter.LineTo(points[i]);
            }
            if (closed)
            {
                painter.ClosePath();
            }
            painter.Stroke();
        }

        // A short chequered bar across the road at the start/finish line: 2 rows of 4 little squares, black and white.
        private void DrawStartBar(Painter2D painter)
        {
            Vector2 along = points[1] - points[0];
            if (along.sqrMagnitude < 0.0001f)
            {
                return;
            }
            along.Normalize();
            Vector2 across = new Vector2(-along.y, along.x);
            const float square = 3f;
            Vector2 corner = points[0] - across * (2f * square) - along * square;
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    bool white = (row + column) % 2 == 0;
                    painter.fillColor = white ? Color.white : HudColors.Black;
                    Vector2 a = corner + across * (column * square) + along * (row * square);
                    painter.BeginPath();
                    painter.MoveTo(a);
                    painter.LineTo(a + across * square);
                    painter.LineTo(a + across * square + along * square);
                    painter.LineTo(a + along * square);
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
