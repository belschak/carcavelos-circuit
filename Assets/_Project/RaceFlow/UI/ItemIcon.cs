using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The picture of an item in the HUD slot (task R7), drawn with UI Toolkit's vector API (Painter2D) like
    // AzulejoPattern: no image files, own work after Luc's four item designs (items_showcase.png, Role 3):
    // Espresso = a cup on a saucer with steam, Carcavelos Wave = a curling turquoise wave with foam,
    // Dean's List = a graduation cap on a ring of gold coins, Seagull Delivery = a seagull with a red and white
    // lifebuoy. Drawn on a 72 x 72 grid and scaled to the element's size.
    // Luc's real icons (request Q16, Visuals/Items/Icons/<ItemKind>.png via ItemSettings) replace these: an item with
    // a picture (SetPicture) shows it as the element's background image and skips the drawing; an item without one
    // (picture missing or not imported yet) keeps the code-drawn icon, so the slot never stays empty.
    // generateVisualContent only runs when MarkDirtyRepaint is called: on a new icon, or every frame while the
    // steam moves (SteamPhase).
    public class ItemIcon : VisualElement
    {
        private const float Grid = 72f;
        private static readonly Color Cup = new Color(0.97f, 0.95f, 0.90f, 1f);    // glazed white, like the box tiles
        private static readonly Color Coffee = new Color(0.36f, 0.20f, 0.10f, 1f);
        private static readonly Color Foam = new Color(1f, 1f, 1f, 1f);
        private static readonly Color Turquoise = new Color(0.08f, 0.80f, 0.86f, 1f);   // Luc's wave
        private static readonly Color TurquoiseDeep = new Color(0.04f, 0.50f, 0.62f, 1f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);            // Dean's List coins
        private static readonly Color GoldDark = new Color(0.78f, 0.52f, 0.06f, 1f);
        private static readonly Color CapBlack = new Color(0.10f, 0.12f, 0.20f, 1f);     // Nova navy-black
        private static readonly Color BuoyRed = new Color(0.86f, 0.12f, 0.10f, 1f);
        private static readonly Color GullGrey = new Color(0.62f, 0.68f, 0.80f, 1f);
        private static readonly Color Beak = new Color(1f, 0.80f, 0.15f, 1f);

        private ItemKind kind = ItemKind.None;
        private float steamPhase;
        private readonly Texture2D[] pictures = new Texture2D[5]; // one per ItemKind value (0 = None stays empty)

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
                    ShowPicture();
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
                if (kind == ItemKind.Espresso && !HasPicture)
                {
                    MarkDirtyRepaint();
                }
            }
        }

        // True when the item shown now has Luc's picture (then nothing is drawn in code).
        public bool HasPicture { get { return PictureOf(kind) != null; } }

        // Gives an item its picture (null = back to the code-drawn icon). ItemKind.None and unknown values are ignored.
        public void SetPicture(ItemKind item, Texture2D picture)
        {
            int index = (int)item;
            if (index <= 0 || index >= pictures.Length)
            {
                return;
            }
            pictures[index] = picture;
            if (item == kind)
            {
                ShowPicture();
                MarkDirtyRepaint();
            }
        }

        private Texture2D PictureOf(ItemKind item)
        {
            int index = (int)item;
            if (index <= 0 || index >= pictures.Length)
            {
                return null;
            }
            return pictures[index];
        }

        // The picture fills the element (keeps its square shape); no picture = no background image, the drawing shows.
        private void ShowPicture()
        {
            Texture2D picture = PictureOf(kind);
            if (picture != null)
            {
                style.backgroundImage = new StyleBackground(picture);
                style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); // whole picture, scaled to fit
            }
            else
            {
                style.backgroundImage = StyleKeyword.Null; // clears the inline picture: nothing behind the drawing
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f || kind == ItemKind.None || HasPicture)
            {
                return; // nothing to draw, or Luc's picture is shown instead
            }
            Painter2D painter = context.painter2D;
            float unit = Mathf.Min(rect.width, rect.height) / Grid;
            Vector2 origin = new Vector2((rect.width - Grid * unit) * 0.5f, (rect.height - Grid * unit) * 0.5f);

            if (kind == ItemKind.Espresso)
            {
                DrawCup(painter, origin, unit, new Vector2(36f, 44f), 1.25f, 0f);
            }
            else if (kind == ItemKind.CarcavelosWave)
            {
                DrawWave(painter, origin, unit);
            }
            else if (kind == ItemKind.DeansList)
            {
                DrawCap(painter, origin, unit);
            }
            else if (kind == ItemKind.SeagullDelivery)
            {
                DrawSeagull(painter, origin, unit);
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
            // Back wave (deep turquoise).
            painter.fillColor = TurquoiseDeep;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 36f, 66f));
            painter.BezierCurveTo(P(origin, unit, 46f, 40f), P(origin, unit, 60f, 32f), P(origin, unit, 70f, 34f));
            painter.LineTo(P(origin, unit, 70f, 66f));
            painter.ClosePath();
            painter.Fill();

            // Main wave: rises from the right, curls over to the left and down into its own hollow.
            painter.fillColor = Turquoise;
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

        // Dean's List: a ring of gold coins (seen from the front: an oval of dots) with a mortarboard on top.
        private static void DrawCap(Painter2D painter, Vector2 origin, float unit)
        {
            // Coins: dots on an oval, the back half darker.
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                bool front = Mathf.Sin(a) > 0f;
                painter.fillColor = front ? Gold : GoldDark;
                DrawDot(painter, P(origin, unit, 36f + Mathf.Cos(a) * 26f, 56f + Mathf.Sin(a) * 7f), 5.2f * unit);
            }

            // Skull cap (the part that sits on the head).
            painter.fillColor = CapBlack;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 22f, 32f));
            painter.LineTo(P(origin, unit, 50f, 32f));
            painter.LineTo(P(origin, unit, 49f, 50f));
            painter.BezierCurveTo(P(origin, unit, 42f, 54f), P(origin, unit, 30f, 54f), P(origin, unit, 23f, 50f));
            painter.ClosePath();
            painter.Fill();

            // The square board, seen at an angle, with a gold rim.
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 4f, 26f));
            painter.LineTo(P(origin, unit, 36f, 14f));
            painter.LineTo(P(origin, unit, 68f, 26f));
            painter.LineTo(P(origin, unit, 36f, 38f));
            painter.ClosePath();
            painter.fillColor = CapBlack;
            painter.Fill();
            painter.strokeColor = Gold;
            painter.lineWidth = 2.4f * unit;
            painter.Stroke();

            // Tassel: from the button in the middle over the edge and down.
            painter.lineWidth = 2.2f * unit;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 36f, 26f));
            painter.LineTo(P(origin, unit, 58f, 29f));
            painter.LineTo(P(origin, unit, 58f, 42f));
            painter.Stroke();
            painter.fillColor = Gold;
            DrawDot(painter, P(origin, unit, 36f, 26f), 2.6f * unit);
            DrawDot(painter, P(origin, unit, 58f, 44f), 3f * unit);
        }

        // Seagull Delivery: a round white gull with a yellow beak, and its red and white lifebuoy beside it.
        private static void DrawSeagull(Painter2D painter, Vector2 origin, float unit)
        {
            // Lifebuoy (behind the gull): a white ring with four red pieces.
            Vector2 buoy = P(origin, unit, 50f, 46f);
            painter.lineWidth = 8f * unit;
            painter.strokeColor = Foam;
            painter.BeginPath();
            painter.Arc(buoy, 14f * unit, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Stroke();
            painter.strokeColor = BuoyRed;
            for (int i = 0; i < 4; i++)
            {
                float start = i * 90f + 22f;
                painter.BeginPath();
                painter.Arc(buoy, 14f * unit, Angle.Degrees(start), Angle.Degrees(start + 45f));
                painter.Stroke();
            }

            // Body, wing and head.
            painter.fillColor = Foam;
            DrawDot(painter, P(origin, unit, 24f, 48f), 14f * unit);
            painter.fillColor = GullGrey;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 14f, 42f));
            painter.BezierCurveTo(P(origin, unit, 22f, 38f), P(origin, unit, 34f, 44f), P(origin, unit, 34f, 54f));
            painter.BezierCurveTo(P(origin, unit, 26f, 56f), P(origin, unit, 16f, 52f), P(origin, unit, 14f, 42f));
            painter.Fill();
            painter.fillColor = Foam;
            DrawDot(painter, P(origin, unit, 24f, 24f), 11f * unit);

            // Beak pointing right, and an eye.
            painter.fillColor = Beak;
            painter.BeginPath();
            painter.MoveTo(P(origin, unit, 31f, 22f));
            painter.LineTo(P(origin, unit, 43f, 26f));
            painter.LineTo(P(origin, unit, 31f, 30f));
            painter.ClosePath();
            painter.Fill();
            painter.fillColor = CapBlack;
            DrawDot(painter, P(origin, unit, 26f, 21f), 2.2f * unit);
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
