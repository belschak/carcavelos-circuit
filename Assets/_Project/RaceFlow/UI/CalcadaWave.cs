using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Lisbon "mar largo" pavement wave (calcada portuguesa): a white band with a black sine wave through it.
    // Used as a divider on the start and results screens.
    // Drawn with UI Toolkit's vector API (Painter2D): no textures, no assets, own work. generateVisualContent runs
    // only when the element is marked dirty (value change or resize), not every frame. RaceHud creates it in code
    // and adds it to an empty "...-host" element from RaceHud.uxml.
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
}
