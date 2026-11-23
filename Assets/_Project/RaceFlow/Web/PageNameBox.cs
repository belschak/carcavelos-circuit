using UnityEngine;
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
        private string lastReason;  // why the box was hidden the last time ("" = shown), for the console

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
                if (lastReason != "not a phone")
                {
                    lastReason = "not a phone";
                    Debug.Log("PageNameBox " + owner + ": not used (no phone, no touch mode)");
                }
                Hide();
                field.focusable = true; // a touch laptop after a key press: the game's own field again
                return;
            }
            field.focusable = false; // the finger types in the page box, never in the game's field
            VisualElement screen = field.panel == null ? null : field.panel.visualTree;
            string hiddenBecause = screen == null ? "no panel" : WhyHidden(field, screen);
            if (hiddenBecause != lastReason)
            {
                lastReason = hiddenBecause;
                Debug.Log("PageNameBox " + owner + ": " + (hiddenBecause.Length == 0 ? "shown" : "hidden (" + hiddenBecause + ")"));
            }
            if (hiddenBecause.Length > 0)
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

        // "" when the field is on screen and can be used, else why not (written to the console once per change, so a
        // phone report can be checked in the browser console).
        private static string WhyHidden(VisualElement element, VisualElement screen)
        {
            if (screen.worldBound.width < 1f || screen.worldBound.height < 1f)
            {
                return "no screen size";
            }
            if (element.pickingMode == PickingMode.Ignore || !element.enabledInHierarchy)
            {
                return "not clickable"; // e.g. the save form after SAVE (SaveTimeForm.SetClickable)
            }
            if (element.worldBound.width < 1f || element.worldBound.height < 1f)
            {
                return "no size";
            }
            if (element.resolvedStyle.visibility == Visibility.Hidden)
            {
                return "invisible"; // the field's own value already includes what its parents pass on
            }
            float opacity = 1f;
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                {
                    return "display none";
                }
                opacity *= current.resolvedStyle.opacity;
            }
            return opacity < 0.05f ? "faded out" : "";
        }
    }
}
