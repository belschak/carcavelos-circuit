using System;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // "SAVE YOUR TIME" on the results card (task R10): a name field, a SAVE button, a line for problems, and after
    // saving the place ("YOU ARE #4"). Plain C# that builds its elements in code into the leaderboard card's form
    // slot; LeaderboardHud decides what happens (checks the name, calls the service). Look: UI/Leaderboard.uss.
    // The name field is a UI Toolkit TextField: a click (or a tap, which the Web page turns into a click) puts the
    // cursor in it, and in a phone browser Unity then opens the phone's keyboard. Tab also jumps into it.
    public class SaveTimeForm
    {
        // SAVE was clicked (Enter while typing is read by LeaderboardHud, which then does the same).
        public event Action SaveRequested;

        private readonly VisualElement root;
        private readonly VisualElement editBox;
        private readonly VisualElement resultBox;
        private readonly Label timeLabel;
        private readonly TextField field;
        private readonly Label placeholder;
        private readonly Button saveButton;
        private readonly VisualElement keysIdle;
        private readonly VisualElement keysTyping;
        private readonly Label message;
        private readonly Label resultRank;
        private readonly Label resultText;
        private bool clickable;

        public SaveFormState State { get; private set; }
        public string TypedName { get { return field.value; } }
        public TextField Field { get { return field; } }

        public SaveTimeForm(VisualElement slot)
        {
            root = HudBuild.Element("lb-form", slot);

            editBox = HudBuild.Element("lb-form-edit", root);
            VisualElement head = HudBuild.Element("lb-form-head", editBox);
            HudBuild.Text("SAVE YOUR TIME", "lb-form-title f-ui", head);
            timeLabel = HudBuild.Text("", "lb-form-time f-num", head);

            VisualElement row = HudBuild.Element("lb-form-row", editBox);
            VisualElement wrap = HudBuild.Element("lb-field-wrap", row);
            field = new TextField();
            field.AddToClassList("lb-name-field");
            field.maxLength = PlayerNameRules.MaxLength;
            field.isDelayed = false;
            wrap.Add(field);
            placeholder = HudBuild.Text("YOUR NAME", "lb-placeholder f-ui", wrap); // TextField has no placeholder of its own we can style
            saveButton = new Button(OnSaveClicked);
            saveButton.text = "SAVE";
            saveButton.AddToClassList("lb-save");
            saveButton.AddToClassList("f-ui");
            saveButton.focusable = false; // after a click it must not keep focus, or Space/Enter would press it again
            row.Add(saveButton);

            keysIdle = HudBuild.Element("lb-form-keys", editBox);
            Keycap("TAB", keysIdle);
            HudBuild.Text("TYPE YOUR NAME", "lb-key-text f-ui", keysIdle);
            keysTyping = HudBuild.Element("lb-form-keys", editBox);
            Keycap("ENTER", keysTyping);
            HudBuild.Text("SAVE", "lb-key-text f-ui", keysTyping);
            message = HudBuild.Text("", "lb-msg f-ui", editBox);

            resultBox = HudBuild.Element("lb-form-result", root);
            resultRank = HudBuild.Text("", "lb-result-rank f-display", resultBox);
            resultText = HudBuild.Text("", "lb-result-text f-ui", resultBox);

            field.RegisterValueChangedCallback(evt => OnTyped());
            clickable = true;
            SetClickable(false); // clicks only while the form is open on the results card
            Hide();
        }

        // A new lap that may be saved: the remembered name is filled in, the time shown.
        public void Open(string rememberedName, int lapMilliseconds)
        {
            State = SaveFormState.Editing;
            field.SetValueWithoutNotify(rememberedName);
            timeLabel.text = HudFormat.Time(lapMilliseconds);
            ShowMessage("", false);
            SetTyping(false);
            Refresh();
        }

        // Not on the results screen, or this lap cannot be saved: the card shows only the list.
        public void Hide()
        {
            State = SaveFormState.Hidden;
            Blur();
            Refresh();
        }

        // The name was refused: say why in our own words (PlayerNameRules.Message) and keep the field open.
        public void ShowProblem(string text)
        {
            ShowMessage(text, true);
            HudFx.Punch(message, "lb-msg--pop");
        }

        public void ShowSaving(string cleanedName)
        {
            State = SaveFormState.Saving;
            field.SetValueWithoutNotify(cleanedName); // the name as it will be saved (trimmed, single spaces)
            ShowMessage("SAVING...", false);
            Blur();
            Refresh();
        }

        // The answer is here: "YOU ARE #4" and where the time is kept.
        public void ShowSaved(LeaderboardReply reply)
        {
            State = SaveFormState.Saved;
            if (!reply.Success)
            {
                resultRank.text = "";
                resultText.text = "THIS TIME COULD NOT BE SAVED";
            }
            else
            {
                resultRank.text = reply.Rank > 0 ? "YOU ARE #" + reply.Rank : "SAVED";
                resultText.text = ResultLine(reply);
            }
            resultBox.EnableInClassList("lb-form-result--offline", reply.Source == LeaderboardSource.OfflineFallback);
            Refresh();
        }

        // The line under "YOU ARE #4". Public and static so an Edit Mode test can check the words.
        public static string ResultLine(LeaderboardReply reply)
        {
            string where;
            switch (reply.Source)
            {
                case LeaderboardSource.Online: where = "ON THE ONLINE LEADERBOARD"; break;
                case LeaderboardSource.ThisDevice: where = "ON THIS DEVICE"; break;
                default: where = "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE"; break;
            }
            if (reply.Rank == 0)
            {
                return "OUTSIDE THE TOP " + LocalLeaderboardList.Keep + "  ·  " + where;
            }
            return where;
        }

        // The key hint follows what the keys do right now: Tab to start typing, Enter to save while typing.
        public void SetTyping(bool typing)
        {
            keysIdle.EnableInClassList("lb-form-keys--on", !typing);
            keysTyping.EnableInClassList("lb-form-keys--on", typing);
        }

        // Only a visible form may catch clicks (the HUD ignores clicks everywhere else).
        public void SetClickable(bool canClick)
        {
            bool wanted = canClick && State == SaveFormState.Editing;
            if (wanted == clickable)
            {
                return;
            }
            clickable = wanted;
            SetPickingDeep(field, wanted);
            HudFx.SetClickable(saveButton, wanted);
            if (!wanted)
            {
                Blur();
            }
        }

        // Is the cursor in the name field? UI Toolkit focuses an element inside the TextField, so we look up the parents.
        public bool HasFocus()
        {
            IPanel panel = field.panel;
            if (panel == null || panel.focusController == null)
            {
                return false;
            }
            VisualElement focused = panel.focusController.focusedElement as VisualElement;
            for (VisualElement current = focused; current != null; current = current.parent)
            {
                if (current == field)
                {
                    return true;
                }
            }
            return false;
        }

        public void Focus()
        {
            if (State == SaveFormState.Editing)
            {
                field.Focus();
            }
        }

        public void Blur()
        {
            if (HasFocus())
            {
                field.Blur();
            }
        }

        private void OnSaveClicked()
        {
            if (State == SaveFormState.Editing && SaveRequested != null)
            {
                SaveRequested();
            }
        }

        private void OnTyped()
        {
            if (message.ClassListContains("lb-msg--error"))
            {
                ShowMessage("", false); // the old problem goes away as soon as the player changes the name
            }
            Refresh();
        }

        private void Refresh()
        {
            root.EnableInClassList("lb-form--hidden", State == SaveFormState.Hidden);
            root.EnableInClassList("lb-form--saving", State == SaveFormState.Saving);
            editBox.EnableInClassList("lb-form-edit--off", State == SaveFormState.Saved);
            resultBox.EnableInClassList("lb-form-result--on", State == SaveFormState.Saved);
            placeholder.EnableInClassList("lb-placeholder--off", field.value.Length > 0);
        }

        private void ShowMessage(string text, bool isError)
        {
            message.text = text;
            message.EnableInClassList("lb-msg--error", isError);
        }

        private static void SetPickingDeep(VisualElement element, bool canClick)
        {
            element.pickingMode = canClick ? PickingMode.Position : PickingMode.Ignore;
            foreach (VisualElement child in element.Children())
            {
                SetPickingDeep(child, canClick);
            }
        }

        private static void Keycap(string key, VisualElement parent)
        {
            VisualElement cap = HudBuild.Element("keycap", parent);
            cap.AddToClassList("keycap--small");
            cap.AddToClassList("keycap--wide-small");
            HudBuild.Text(key, "f-ui", cap);
        }
    }
}
