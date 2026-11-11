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
    // Look (task R15): a navy plate with a blue header plate, a podium (places 1-3 taller, with shields), a round avatar
    // per racer (LeaderboardAvatar), and your own row as an orange plate with a YOU tag that slides in last with a punch.
    public class LeaderboardPanel
    {
        private const int StaggerMs = 45;      // between two rows sliding in
        private const int OwnRowExtraMs = 120; // your own row comes last, a moment after the others

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
        private readonly VisualElement[] avatars;
        private readonly bool onResults;

        public VisualElement Root { get { return root; } }

        // The place for the "save your time" form (results card only), between the head and the list.
        public VisualElement FormSlot { get { return formSlot; } }

        public LeaderboardPanel(VisualElement host, int rowCount, bool onResults)
        {
            this.onResults = onResults;
            root = HudBuild.Element("lb plate", host);
            root.AddToClassList(onResults ? "lb--results" : "lb--start");

            VisualElement head = HudBuild.Element("lb-head plate plate--blue", root);
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
            avatars = new VisualElement[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                rows[i] = HudBuild.Element("lb-row", rowsBox);
                rows[i].EnableInClassList("lb-row--p1", i == 0);
                rows[i].EnableInClassList("lb-row--p2", i == 1);
                rows[i].EnableInClassList("lb-row--p3", i == 2);
                rows[i].EnableInClassList("lb-row--alt", i % 2 == 1); // alternating row colours
                SetDelay(i, i * StaggerMs);
                VisualElement badge = HudBuild.Element("lb-rank", rows[i]);
                bool podium = i < 3;
                if (podium)
                {
                    badge.AddToClassList("shield"); // gold, silver or bronze shield instead of a disc
                    badge.EnableInClassList("shield--silver", i == 1);
                    badge.EnableInClassList("shield--bronze", i == 2);
                }
                ranks[i] = HudBuild.Text((i + 1).ToString(), podium ? "lb-rank-text f-display" : "lb-rank-text f-num", badge);
                avatars[i] = HudBuild.Element("lb-avatar", rows[i]);
                names[i] = HudBuild.Text("", podium ? "lb-name f-display" : "lb-name f-ui", rows[i]);
                names[i].enableRichText = false; // a name is shown as typed, never as formatting
                HudBuild.Text("YOU", "lb-you f-ui", rows[i]);
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
                avatars[i].style.backgroundColor = StyleKeyword.Null;
                SetDelay(i, i * StaggerMs);
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
            int ownRow = -1; // the row that slides in last: the one just saved, or else your best one
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < entries.Count)
                {
                    bool isMine = LeaderboardRanking.IsMine(entries[i], mine);
                    SetRowState(i, i + 1 == newRank ? "lb-row--new" : (isMine ? "lb-row--mine" : ""));
                    names[i].text = entries[i].name;
                    times[i].text = HudFormat.Time(RaceTimer.ToMilliseconds(entries[i].seconds));
                    avatars[i].style.backgroundColor = LeaderboardAvatar.ColourFor(entries[i].name);
                    if (i + 1 == newRank || (isMine && ownRow < 0 && newRank <= 0))
                    {
                        ownRow = i;
                    }
                }
                else
                {
                    SetRowState(i, "lb-row--empty"); // an empty arcade slot
                    names[i].text = "- - -";
                    times[i].text = "-:--.---";
                    avatars[i].style.backgroundColor = StyleKeyword.Null; // the USS dims it
                }
            }
            for (int i = 0; i < rows.Length; i++)
            {
                SetDelay(i, RowDelayMs(i, ownRow, rows.Length));
            }
            Replay();
            if (ownRow >= 0)
            {
                VisualElement row = rows[ownRow];
                int landsMs = 33 + RowDelayMs(ownRow, ownRow, rows.Length) + 220; // after it has slid in
                row.schedule.Execute(() => HudFx.Punch(row, "lb-row--pop")).ExecuteLater(landsMs);
            }
        }

        // When a row starts to slide in (milliseconds): one after another, but your own row (ownRow, -1 = none)
        // waits until all the others are in, plus a moment. Public and static for an Edit Mode test.
        public static int RowDelayMs(int row, int ownRow, int rowCount)
        {
            if (row < 0)
            {
                return 0;
            }
            if (row == ownRow)
            {
                return rowCount * StaggerMs + OwnRowExtraMs;
            }
            return row * StaggerMs;
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

        private void SetDelay(int i, int milliseconds)
        {
            rows[i].style.transitionDelay = new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(milliseconds, TimeUnit.Millisecond) });
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
