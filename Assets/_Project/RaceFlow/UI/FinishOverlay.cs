using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The finish cinematic on screen (Luc, 16 Nov 2026): a waving checkered flag right after the line, then the
    // place ("2ND" + "OF 6 · 1:02.345") or, in Time Attack, the lap time in the middle of the screen while the
    // camera shows the driver from the front. Timing in FinishCinematic. Built in code and added on top of the HUD,
    // so RaceHud.uxml stays as it is. Never catches clicks. RaceHud creates it and calls Update() every frame.
    public class FinishOverlay
    {
        private const int Columns = 8;
        private const int Rows = 5;
        private const float Square = 34f;     // px per flag square (reference resolution 1280 x 720)
        private const float FadeSeconds = 0.2f;

        private readonly VisualElement layer;
        private readonly VisualElement flag;
        private readonly VisualElement[] flagColumns = new VisualElement[Columns];
        private readonly VisualElement headlineBox;
        private readonly Label headline;
        private readonly Label subline;
        private bool textWritten;

        public bool IsFlagShown { get; private set; }      // Play Mode test
        public bool IsHeadlineShown { get; private set; }  // Play Mode test
        public string HeadlineText { get { return headline.text; } }

        public FinishOverlay(VisualElement hudRoot)
        {
            layer = new VisualElement { name = "finish-overlay" };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.right = 0f;
            layer.style.top = 0f;
            layer.style.bottom = 0f;
            layer.style.alignItems = Align.Center;
            layer.style.justifyContent = Justify.Center;

            // The flag: one element per column, so each column can sway on its own (a travelling wave).
            flag = new VisualElement { name = "finish-flag" };
            flag.style.position = Position.Absolute;
            flag.style.top = Length.Percent(14f);
            flag.style.flexDirection = FlexDirection.Row;
            flag.style.borderTopWidth = 4f;
            flag.style.borderBottomWidth = 4f;
            flag.style.borderLeftWidth = 4f;
            flag.style.borderRightWidth = 4f;
            SetBorderColour(flag, HudColors.Ink);
            for (int c = 0; c < Columns; c++)
            {
                VisualElement column = new VisualElement();
                for (int r = 0; r < Rows; r++)
                {
                    VisualElement square = new VisualElement();
                    square.style.width = Square;
                    square.style.height = Square;
                    square.style.backgroundColor = (c + r) % 2 == 0 ? HudColors.Ink : HudColors.Cream;
                    column.Add(square);
                }
                flagColumns[c] = column;
                flag.Add(column);
            }
            layer.Add(flag);

            // The place or the lap time, big, in the middle; a chip with the details under it.
            headlineBox = new VisualElement { name = "finish-headline" };
            headlineBox.style.alignItems = Align.Center;
            headline = new Label("");
            headline.AddToClassList("f-display");
            headline.style.fontSize = 132f;
            headline.style.color = HudColors.Sun;
            headline.style.unityTextOutlineWidth = 5f;
            headlineBox.Add(headline);
            subline = new Label("");
            subline.AddToClassList("chip");
            subline.AddToClassList("f-ui");
            subline.style.fontSize = 28f;
            subline.style.letterSpacing = 2f;
            subline.style.color = HudColors.Ink;
            subline.style.marginTop = 4f;
            subline.style.paddingLeft = 26f;
            subline.style.paddingRight = 26f;
            subline.style.paddingTop = 6f;
            subline.style.paddingBottom = 12f;
            headlineBox.Add(subline);
            layer.Add(headlineBox);

            flag.style.opacity = 0f;
            headlineBox.style.opacity = 0f;
            HudFx.IgnoreClicksDeep(layer);
            hudRoot.Add(layer); // last child = drawn above the race HUD; the results card hides it again (Update)
        }

        // seconds = real seconds since the line, -1 = not finished. resultsShown = the card is in: the overlay goes.
        public void Update(float seconds, bool resultsShown, bool reducedMotion, RaceMode mode, int place, int racers,
            int lapMilliseconds, bool newRecord)
        {
            bool finished = seconds >= 0f;
            if (!finished)
            {
                textWritten = false;
            }

            IsFlagShown = finished && !resultsShown && FinishCinematic.FlagShown(seconds);
            IsHeadlineShown = finished && !resultsShown && FinishCinematic.HeadlineShown(seconds, reducedMotion);

            if (IsHeadlineShown && !textWritten)
            {
                // Written once, when it appears: the place is final by then (the standings froze at the line).
                string time = HudFormat.Time(lapMilliseconds);
                headline.text = RaceModeRules.FinishHeadline(mode, place, racers, time);
                subline.text = RaceModeRules.FinishSubline(mode, place, racers, time, newRecord);
                headline.style.color = RaceModeRules.ShowsPlace(mode) && place == 1 ? HudColors.Sun : HudColors.Cream;
                textWritten = true;
            }

            Fade(flag, IsFlagShown);
            Fade(headlineBox, IsHeadlineShown);

            // The flag pops in and waves; the headline lands with a small overshoot. Nothing moves with reduced motion.
            float flagAge = finished ? seconds : 0f;
            float pop = reducedMotion ? 1f : Mathf.Clamp01(flagAge / 0.25f);
            float flagScale = 0.6f + 0.4f * EaseOutBack(pop);
            flag.style.scale = new Scale(new Vector3(flagScale, flagScale, 1f));
            for (int c = 0; c < Columns; c++)
            {
                float sway = IsFlagShown ? FinishCinematic.FlagWave(seconds, c, reducedMotion) : 0f;
                flagColumns[c].style.translate = new Translate(0f, sway);
            }
            float headlineAge = finished ? seconds - FinishCinematic.HeadlineInSeconds : 0f;
            float land = reducedMotion ? 1f : Mathf.Clamp01(headlineAge / 0.35f);
            float headlineScale = 1.6f - 0.6f * EaseOutBack(land);
            headlineBox.style.scale = new Scale(new Vector3(headlineScale, headlineScale, 1f));
        }

        // Opacity steps towards shown / hidden each frame (fades are allowed with reduced motion: they are not motion).
        private static void Fade(VisualElement element, bool shown)
        {
            float now = element.resolvedStyle.opacity;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            element.style.opacity = Mathf.MoveTowards(now, shown ? 1f : 0f, step);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static void SetBorderColour(VisualElement element, Color colour)
        {
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }
    }
}
