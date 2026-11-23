using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Android name fix (23 Nov 2026). On a phone a name is typed in a real text box of the Web page that lies exactly
    // over the game's name field (the online lobby and the leaderboard save form). Why: a phone browser opens its
    // keyboard only for a real page text box that a real finger tapped. The game's own field never gets that tap, and
    // window.prompt (the old way) did nothing on a Pixel 9 Pro in Chrome. The page box (CarcavelosWebPage.jslib,
    // CarcavelosNameBox...) needs no Unity click at all: the finger lands on the box itself.
    // Ten times a second the box is moved to where the field is now and its text is copied into the field, so the game
    // checks the name exactly like a typed one (PlayerNameRules). On a desktop without touch nothing changes.
    public class PageNameBox
    {
        private static int nextOwner = 1;

        private readonly TextField field;
        private readonly int owner;   // one box for the whole page: the field that showed it last owns it
        private bool shown;
        private string synced = ""; // the text the box and the field last agreed on

        public PageNameBox(TextField field)
        {
            this.field = field;
            owner = nextOwner;
            nextOwner++;
            field.schedule.Execute(Follow).Every(100);
            field.RegisterCallback<DetachFromPanelEvent>(evt => Hide()); // the page changed: the box goes with the field
        }

        // True while the phone keyboard types into the box (the game keeps its own keys off then).
        public bool HasFocus
        {
            get { return shown && WebPage.NameBoxFocused(owner); }
        }

        // Puts the box over the field (or takes it away) and copies its text into the field.
        private void Follow()
        {
            if (!WebPage.UsesNamePrompt)
            {
                Hide();
                field.focusable = true; // a touch laptop after a key press: the game's own field again
                return;
            }
            field.focusable = false; // the finger types in the page box, never in the game's field
            VisualElement screen = field.panel == null ? null : field.panel.visualTree;
            if (screen == null || !IsShown(field) || screen.worldBound.width < 1f || screen.worldBound.height < 1f)
            {
                Hide();
                return;
            }
            // reset: the box takes the field's text when it opens or when the game changed the field itself.
            bool reset = !shown || field.value != synced;
            shown = true;
            WebPage.ShowNameBox(owner, field.worldBound, screen.worldBound, field.value, field.maxLength, reset);
            if (reset)
            {
                synced = field.value;
                return;
            }
            string typed = WebPage.NameBoxValue(owner);
            if (typed != field.value)
            {
                synced = typed;
                field.value = typed; // fires the field's value change: the lobby clears its old warning
            }
        }

        private void Hide()
        {
            if (shown)
            {
                shown = false;
                WebPage.HideNameBox(owner);
            }
        }

        // The field is on screen and can be used: attached, not hidden by itself or a parent, not switched off.
        private static bool IsShown(VisualElement element)
        {
            if (element.pickingMode == PickingMode.Ignore || !element.enabledInHierarchy)
            {
                return false; // e.g. the save form after SAVE (SaveTimeForm.SetClickable)
            }
            if (element.worldBound.width < 1f || element.worldBound.height < 1f)
            {
                return false;
            }
            for (VisualElement current = element; current != null; current = current.parent)
            {
                IResolvedStyle style = current.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity < 0.05f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
