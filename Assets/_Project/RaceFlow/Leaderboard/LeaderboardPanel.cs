using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The top-10 card (task R10), drawn twice: on the start screen and beside the results card. Plain C# that builds
    // its elements in code (like UI/ItemSlotHud) into an empty "...-board-host" from RaceHud.uxml; the look is in
    // UI/Leaderboard.uss. LeaderboardHud tells it what to show; it never talks to a server itself.
    // States: loading (grey placeholder rows), the list, an empty list (empty arcade slots "- - -"), and a note
    // when the times come from this device ("Leaderboard offline - ...").
    // Player names are shown with rich text OFF, so a name can never become formatting (they are checked by
    // PlayerNameRules anyway, which refuses < and >).
    public class LeaderboardPanel
    {
        private const int StaggerMs = 40; // between two rows sliding in

        private readonly VisualElement root;
        private readonly VisualElement formSlot;
        private readonly Label subtitle;
        private readonly VisualElement sourceChip;
        private readonly Label sourceText;
        private readonly Label note;
        private readonly VisualElement rowsBox;
        private readonly VisualElement[] rows;
        private readonly Label[] ranks;
        private readonly Label[] names;
        private readonly Label[] times;
        private readonly bool onResults;

        public VisualElement Root { get { return root; } }

        // The place for the "save your time" form (results card only), between the head and the list.
        public VisualElement FormSlot { get { return formSlot; } }

        public LeaderboardPanel(VisualElement host, int rowCount, bool onResults)
        {
            this.onResults = onResults;
            root = HudBuild.Element("lb", host);
            root.AddToClassList(onResults ? "lb--results" : "lb--start");

            VisualElement head = HudBuild.Element("lb-head", root);
            HudBuild.Element("lb-gem", head);
            VisualElement titles = HudBuild.Element("lb-titles", head);
            HudBuild.Text("LEADERBOARD", "lb-title f-display", titles);
            subtitle = HudBuild.Text("TOP " + rowCount, "lb-sub f-ui", titles);
            sourceChip = HudBuild.Element("lb-source", head);
            HudBuild.Element("lb-source-dot", sourceChip);
            sourceText = HudBuild.Text("", "lb-source-text f-ui", sourceChip);

            formSlot = HudBuild.Element("lb-form-slot", root);
            note = HudBuild.Text("", "lb-note f-ui", root);

            rowsBox = HudBuild.Element("lb-rows", root);
            rows = new VisualElement[rowCount];
            ranks = new Label[rowCount];
            names = new Label[rowCount];
            times = new Label[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = HudBuild.Element("lb-row", rowsBox);
                rows[i].EnableInClassList("lb-row--p1", i == 0);
                rows[i].EnableInClassList("lb-row--p2", i == 1);
                rows[i].EnableInClassList("lb-row--p3", i == 2);
                rows[i].style.transitionDelay = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(i * StaggerMs, TimeUnit.Millisecond) });
                VisualElement badge = HudBuild.Element("lb-rank", rows[i]);
                ranks[i] = HudBuild.Text((i + 1).ToString(), "lb-rank-text f-num", badge);
                names[i] = HudBuild.Text("", "lb-name f-ui", rows[i]);
                names[i].enableRichText = false; // a name is shown as typed, never as formatting
                times[i] = HudBuild.Text("", "lb-time f-num", rows[i]);
            }
            ShowLoading();
        }

        // "Track_CampusWorld" -> "TOP 10  ·  CAMPUS WORLD" under the title.
        public void SetTrack(string trackName, int rowCount)
        {
            string title = TrackTitle(trackName);
            subtitle.text = title.Length > 0 ? "TOP " + rowCount + "  ·  " + title : "TOP " + rowCount;
        }

        // Grey placeholder rows while the answer is on its way.
        public void ShowLoading()
        {
            SetSource("lb-source--loading", "LOADING");
            SetNote("LOADING TIMES...");
            for (int i = 0; i < rows.Length; i++)
            {
                SetRowState(i, "lb-row--skeleton");
                names[i].text = "";
                times[i].text = "";
            }
            Replay();
        }

        // The answer is here. mine = the times saved from this browser (rows that match get the "you" colour);
        // newRank = the row just saved (1-based, 0 = none) is brighter still.
        public void Show(List<LeaderboardEntry> entries, LeaderboardSource source, List<LeaderboardEntry> mine, int newRank)
        {
            switch (source)
            {
                case LeaderboardSource.Online:
                    SetSource("lb-source--online", "ONLINE");
                    break;
                case LeaderboardSource.ThisDevice:
                    SetSource("lb-source--device", "THIS DEVICE");
                    break;
                default:
                    SetSource("lb-source--offline", "OFFLINE");
                    break;
            }
            SetNote(NoteFor(source, entries.Count, onResults));
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < entries.Count)
                {
                    bool isMine = LeaderboardRanking.IsMine(entries[i], mine);
                    SetRowState(i, i + 1 == newRank ? "lb-row--new" : (isMine ? "lb-row--mine" : ""));
                    names[i].text = entries[i].name;
                    times[i].text = HudFormat.Time(RaceTimer.ToMilliseconds(entries[i].seconds));
                }
                else
                {
                    SetRowState(i, "lb-row--empty"); // an empty arcade slot
                    names[i].text = "- - -";
                    times[i].text = "-:--.---";
                }
            }
            Replay();
        }

        // The line above the rows. An empty board (a fresh browser, or nobody saved yet) invites the first time;
        // offline is said here and only here. Public and static so an Edit Mode test can check the words.
        public static string NoteFor(LeaderboardSource source, int entryCount, bool onResults)
        {
            if (source == LeaderboardSource.OfflineFallback)
            {
                return onResults ? "LEADERBOARD OFFLINE - YOUR TIME IS SAVED ON THIS DEVICE" : "LEADERBOARD OFFLINE - TIMES FROM THIS DEVICE";
            }
            return entryCount == 0 ? "NO TIMES YET - BE THE FIRST!" : "";
        }

        // Scene name to a readable title: "Track_CampusWorld" -> "CAMPUS WORLD", "Track_NovaCampus" -> "NOVA CAMPUS".
        // Public and static so an Edit Mode test can check it.
        public static string TrackTitle(string trackName)
        {
            if (string.IsNullOrEmpty(trackName))
            {
                return "";
            }
            string name = trackName.StartsWith("Track_") ? trackName.Substring(6) : trackName;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_' || c == '-')
                {
                    builder.Append(' ');
                    continue;
                }
                bool newWord = i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]); // "CampusWorld": a space before W
                if (newWord)
                {
                    builder.Append(' ');
                }
                builder.Append(char.ToUpperInvariant(c));
            }
            return builder.ToString().Trim();
        }

        // Rows slide in one after another: take the "in" class off, put it back two frames later (HudFx pattern).
        private void Replay()
        {
            rowsBox.RemoveFromClassList("lb-rows--in");
            rowsBox.schedule.Execute(() => rowsBox.AddToClassList("lb-rows--in")).ExecuteLater(33);
        }

        private void SetRowState(int i, string stateClass)
        {
            rows[i].EnableInClassList("lb-row--skeleton", stateClass == "lb-row--skeleton");
            rows[i].EnableInClassList("lb-row--empty", stateClass == "lb-row--empty");
            rows[i].EnableInClassList("lb-row--mine", stateClass == "lb-row--mine");
            rows[i].EnableInClassList("lb-row--new", stateClass == "lb-row--new");
        }

        private void SetSource(string sourceClass, string text)
        {
            sourceChip.EnableInClassList("lb-source--loading", sourceClass == "lb-source--loading");
            sourceChip.EnableInClassList("lb-source--online", sourceClass == "lb-source--online");
            sourceChip.EnableInClassList("lb-source--device", sourceClass == "lb-source--device");
            sourceChip.EnableInClassList("lb-source--offline", sourceClass == "lb-source--offline");
            sourceText.text = text;
        }

        private void SetNote(string text)
        {
            note.text = text;
            note.EnableInClassList("lb-note--offline", text.StartsWith("LEADERBOARD OFFLINE"));
            note.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None; // no gap when there is nothing to say
        }
    }
}
