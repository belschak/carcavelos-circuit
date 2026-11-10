using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The picture of an item in the HUD slot (task R7), drawn with UI Toolkit's vector API (Painter2D) like
    // AzulejoPattern: no image files, own work. Bica = an espresso cup on a saucer with steam, Triple Bica = three
    // cups, Onda = a curling Atlantic wave with foam. Drawn on a 72 x 72 grid and scaled to the element's size.
    // generateVisualContent only runs when MarkDirtyRepaint is called: on a new icon, or every frame while the
    // steam moves (SteamPhase).
    public class ItemIcon : VisualElement
    {
        private const float Grid = 72f;
        private static readonly Color Cup = new Color(0.97f, 0.95f, 0.90f, 1f);    // glazed white, like the box tiles
        private static readonly Color Coffee = new Color(0.36f, 0.20f, 0.10f, 1f);
        private static readonly Color Foam = new Color(1f, 1f, 1f, 1f);

        private ItemKind kind = ItemKind.None;
        private float steamPhase;

        public ItemIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public ItemKind Kind
        {
            get { return kind; }
            set
            {
                if (kind != value)
                {
                    kind = value;
                    MarkDirtyRepaint();
                }
            }
        }

        // 0..1, wraps. Moves the steam above the cups (the HUD sets it every frame while an item is held).
        public float SteamPhase
        {
            get { return steamPhase; }
            set
            {
                steamPhase = value;
                if (kind == ItemKind.Bica || kind == ItemKind.TripleBica)
                {
                    MarkDirtyRepaint();
                }
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f || kind == ItemKind.None)
            {
                return;
            }
            Painter2D painter = context.painter2D;
            float unit = Mathf.Min(rect.width, rect.height) / Grid;
            Vector2 origin = new Vector2((rect.width - Grid * unit) * 0.5f, (rect.height - Grid * unit) * 0.5f);

            if (kind == ItemKind.Bica)
            {
                DrawCup(painter, origin, unit, new Vector2(36f, 44f), 1.25f, 0f);
            }
            else if (kind == ItemKind.TripleBica)
            {
                DrawCup(painter, origin, unit, new Vector2(20f, 54f), 0.72f, 0.33f);
                DrawCup(painter, origin, unit, new Vector2(52f, 54f), 0.72f, 0.66f);
                DrawCup(painter, origin, unit, new Vector2(36f, 30f), 0.72f, 0f);
            }
            else if (kind == ItemKind.Onda)
            {
                DrawWave(painter, origin, unit);
            }
        }

        // One cup: saucer, body, coffee, handle and two curls of steam. "centre" and "size" in grid units.
        private void DrawCup(Painter2D painter, Vector2 origin, float unit, Vector2 centre, float size, float steamOffset)
        {
            float s = size * unit;
            Vector2 c = origin + centre * unit;

            // Saucer (sand, like the HUD's warm accent).
            painter.fillColor = HudColors.Sand;
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-17f, 10f) * s);
            painter.LineTo(c + new Vector2(17f, 10f) * s);
            painter.LineTo(c + new Vector2(13f, 14f) * s);
            painter.LineTo(c + new Vector2(-13f, 14f) * s);
            painter.ClosePath();
            painter.Fill();

            // Handle on the right.
            painter.strokeColor = Cup;
            painter.lineWidth = 3.2f * s;
            painter.BeginPath();
            painter.Arc(c + new Vector2(11f, 0f) * s, 5f * s, Angle.Degrees(-80f), Angle.Degrees(80f));
            painter.Stroke();

            // Body: wider at the top, rounded bottom.
            painter.fillColor = Cup;
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-12f, -8f) * s);
            painter.LineTo(c + new Vector2(12f, -8f) * s);
            painter.LineTo(c + new Vector2(9f, 6f) * s);
            painter.BezierCurveTo(c + new Vector2(8f, 10f) * s, c + new Vector2(-8f, 10f) * s, c + new Vector2(-9f, 6f) * s);
            painter.ClosePath();
            painter.Fill();

            // Coffee at the rim.
            painter.fillColor = Coffee;
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-10.5f, -7f) * s);
            painter.LineTo(c + new Vector2(10.5f, -7f) * s);
            painter.LineTo(c + new Vector2(10f, -4.5f) * s);
            painter.LineTo(c + new Vector2(-10f, -4.5f) * s);
            painter.ClosePath();
            painter.Fill();

            // Blue band: an azulejo-blue stripe round the cup.
            painter.fillColor = HudColors.Blue;
            painter.BeginPath();
            painter.MoveTo(c + new Vector2(-11f, -1f) * s);
            painter.LineTo(c + new Vector2(11f, -1f) * s);
            painter.LineTo(c + new Vector2(10.5f, 1.5f) * s);
            painter.LineTo(c + new Vector2(-10.5f, 1.5f) * s);
            painter.ClosePath();
            painter.Fill();

            // Steam: two S-curls that drift upwards and fade (phase wraps 0..1).
            for (int i = 0; i < 2; i++)
            {
                float phase = Mathf.Repeat(steamPhase + steamOffset + i * 0.5f, 1f);
                float rise = phase * 6f;
                float x = i == 0 ? -4f : 4f;
                painter.strokeColor = HudColors.WithAlpha(HudColors.White, 0.85f * Mathf.Sin(phase * Mathf.PI));
                painter.lineWidth = 2.4f * s;
                painter.BeginPath();
                Vector2 start = c + new Vector2(x, -11f - rise) * s;
                painter.MoveTo(start);
                painter.BezierCurveTo(start + new Vector2(-4f, -3f) * s, start + new Vector2(4f, -6f) * s, start + new Vector2(0f, -10f) * s);
                painter.Stroke();
            }
        }

        // A wave curling over to the left, white foam on the crest, a second smaller wave behind it.
        private static void DrawWave(Painter2D painter, Vector2 origin, float unit)
        {
            // Back wave (deep blue).
            painter.fillColor = HudColors.BlueDeep;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 36f, 66f));
            painter.BezierCurveTo(P(origin, unit, 46f, 40f), P(origin, unit, 60f, 32f), P(origin, unit, 70f, 34f));
            painter.LineTo(P(origin, unit, 70f, 66f));
            painter.ClosePath();
            painter.Fill();

            // Main wave: rises from the right, curls over to the left and down into its own hollow.
            painter.fillColor = HudColors.Blue;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 66f, 66f));
            painter.BezierCurveTo(P(origin, unit, 62f, 40f), P(origin, unit, 52f, 12f), P(origin, unit, 30f, 10f));
            painter.BezierCurveTo(P(origin, unit, 14f, 9f), P(origin, unit, 4f, 20f), P(origin, unit, 6f, 32f));
            painter.BezierCurveTo(P(origin, unit, 8f, 40f), P(origin, unit, 18f, 42f), P(origin, unit, 22f, 34f));
            painter.BezierCurveTo(P(origin, unit, 25f, 28f), P(origin, unit, 20f, 24f), P(origin, unit, 16f, 26f));
            painter.BezierCurveTo(P(origin, unit, 22f, 18f), P(origin, unit, 36f, 20f), P(origin, unit, 38f, 36f));
            painter.BezierCurveTo(P(origin, unit, 40f, 50f), P(origin, unit, 30f, 60f), P(origin, unit, 2f, 66f));
            painter.ClosePath();
            painter.Fill();

            // Foam along the crest and the lip of the curl.
            painter.strokeColor = Foam;
            painter.lineWidth = 3.2f * unit;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 58f, 30f));
            painter.BezierCurveTo(P(origin, unit, 52f, 16f), P(origin, unit, 42f, 11f), P(origin, unit, 30f, 11f));
            painter.BezierCurveTo(P(origin, unit, 16f, 11f), P(origin, unit, 6f, 21f), P(origin, unit, 7f, 31f));
            painter.Stroke();

            // Spray dots flying off the crest.
            painter.fillColor = Foam;
            DrawDot(painter, P(origin, unit, 62f, 20f), 2.2f * unit);
            DrawDot(painter, P(origin, unit, 67f, 26f), 1.6f * unit);
            DrawDot(painter, P(origin, unit, 56f, 12f), 1.8f * unit);
            painter.lineCap = LineCap.Butt;
        }

        private static void DrawDot(Painter2D painter, Vector2 centre, float radius)
        {
            painter.BeginPath();
            painter.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }

        private static Vector2 P(Vector2 origin, float unit, float x, float y)
        {
            return origin + new Vector2(x, y) * unit;
        }
    }
}
