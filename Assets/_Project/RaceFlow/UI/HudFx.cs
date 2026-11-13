using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Tiny helpers for USS animations. Works like CSS: the USS class says what something looks like, C# only switches
    // classes. Two UI Toolkit facts this is built around:
    // 1. A transition cannot play from display:none, so HUD parts are hidden with opacity (class "is-shown"), not display.
    // 2. A transition needs a "before" style. A punch jumps into a pop class (which has transition-duration 0s),
    //    then removes it a moment later, so the element animates back to its normal look.
    public static class HudFx
    {
        // Jump into popClass, then remove it ~2 frames later so the base class transition plays back.
        public static void Punch(VisualElement element, string popClass)
        {
            element.AddToClassList(popClass);
            element.schedule.Execute(() => element.RemoveFromClassList(popClass)).ExecuteLater(33);
        }

        // Show or hide with a fade instead of display:none. Only writes when it changes.
        public static void SetShown(VisualElement element, bool shown)
        {
            if (element.ClassListContains("is-shown") != shown)
            {
                element.EnableInClassList("is-shown", shown);
            }
        }

        // An invisible element must not catch clicks (otherwise the hidden Retry button could be pressed).
        public static void SetClickable(VisualElement element, bool clickable)
        {
            PickingMode wanted = clickable ? PickingMode.Position : PickingMode.Ignore;
            if (element.pickingMode != wanted)
            {
                element.pickingMode = wanted;
            }
        }

        // Turns off clicks for an element and everything inside it (decorations, labels).
        public static void IgnoreClicksDeep(VisualElement element)
        {
            element.pickingMode = PickingMode.Ignore;
            foreach (VisualElement child in element.Children())
            {
                IgnoreClicksDeep(child);
            }
        }
    }
}
