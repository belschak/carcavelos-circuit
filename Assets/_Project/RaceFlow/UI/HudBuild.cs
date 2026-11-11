using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Two tiny helpers for HUD parts that are built in code (the leaderboard card and its form, task R10):
    // a new element or label with its USS classes, added to a parent, ignoring clicks (decorations never catch
    // clicks; the few interactive parts switch their clicks on themselves).
    public static class HudBuild
    {
        public static VisualElement Element(string classNames, VisualElement parent)
        {
            VisualElement element = new VisualElement();
            element.pickingMode = PickingMode.Ignore;
            AddClasses(element, classNames);
            parent.Add(element);
            return element;
        }

        public static Label Text(string text, string classNames, VisualElement parent)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            AddClasses(label, classNames);
            parent.Add(label);
            return label;
        }

        // "lb-row lb-row--p1" -> two classes.
        private static void AddClasses(VisualElement element, string classNames)
        {
            string[] classes = classNames.Split(' ');
            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i].Length > 0)
                {
                    element.AddToClassList(classes[i]);
                }
            }
        }
    }
}
