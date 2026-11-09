using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P6b: the results confetti, about 30 small paper squares in the Lisbon palette that burst out of the stamp
    // that lands last (NEW RECORD or the medal badge). ConfettiSim calculates the movement, this element only draws it
    // with UI Toolkit's vector API (Painter2D): no textures, no particle system, own work.
    // It covers its parent, ignores the mouse, and only asks for a redraw while pieces are flying. RaceHud creates it
    // in code as the last child of the results screen (so it is drawn on top of the card) and moves it with real
    // time, so the finish slow motion never slows the paper.
    public class ConfettiBurst : VisualElement
    {
        private readonly ConfettiSim sim = new ConfettiSim();
        private bool running;

        public ConfettiBurst()
        {
            pickingMode = PickingMode.Ignore; // clicks go through to the Retry button
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            AddToClassList("confetti");
            generateVisualContent += Draw;
        }

        public bool IsRunning { get { return running; } }

        // Starts a burst at a point in this element's own coordinates (RaceHud converts it with WorldToLocal).
        public void Burst(Vector2 localOrigin, int seed)
        {
            sim.Burst(localOrigin.x, localOrigin.y, ConfettiSim.DefaultCount, seed);
            running = true;
            MarkDirtyRepaint();
        }

        // Called every frame by RaceHud with Time.unscaledDeltaTime.
        public void Step(float unscaledDeltaTime)
        {
            if (!running)
            {
                return;
            }
            sim.Step(unscaledDeltaTime);
            if (sim.IsDone)
            {
                Stop(); // all pieces gone (about 2 s): nothing more to draw
                return;
            }
            MarkDirtyRepaint();
        }

        // Removes every piece at once (retry, leaving the results screen).
        public void Stop()
        {
            if (!running)
            {
                return;
            }
            sim.Clear();
            running = false;
            MarkDirtyRepaint(); // one last redraw with no pieces
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!running)
            {
                return;
            }
            Painter2D painter = context.painter2D;
            for (int i = 0; i < sim.Count; i++)
            {
                float alpha = sim.AlphaOf(i);
                if (alpha <= 0.01f)
                {
                    continue;
                }
                // A rectangle turned by its angle. Its width follows Flip, so it flutters like paper.
                float halfWidth = sim.Width[i] * 0.5f * Mathf.Max(sim.Flip[i], 0.08f);
                float halfHeight = sim.Height[i] * 0.5f;
                float radians = sim.Angle[i] * Mathf.Deg2Rad;
                Vector2 across = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * halfWidth;
                Vector2 along = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * halfHeight;
                Vector2 centre = new Vector2(sim.X[i], sim.Y[i]);

                painter.fillColor = HudColors.WithAlpha(ColourOf(sim.Colour[i]), alpha);
                painter.BeginPath();
                painter.MoveTo(centre - across - along);
                painter.LineTo(centre + across - along);
                painter.LineTo(centre + across + along);
                painter.LineTo(centre - across + along);
                painter.ClosePath();
                painter.Fill();
            }
        }

        // The four confetti colours (ConfettiSim.ColourCount): white, Atlantic blue, orange, sand.
        private static Color ColourOf(int index)
        {
            if (index == 1) return HudColors.Blue;
            if (index == 2) return HudColors.Orange;
            if (index == 3) return HudColors.Sand;
            return HudColors.White;
        }
    }
}
