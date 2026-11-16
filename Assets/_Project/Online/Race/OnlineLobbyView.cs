using System;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Online
{
    // The online lobby on screen (M2), built in code into RaceHud's document like ModePicker and HudMenus, in the
    // same arcade look (plates, pills, chips, the display and UI faces). One overlay, one card, five pages:
    //   Rooms       name field + NOON ROOM / SUNSET ROOM cards + BACK
    //   Connecting  "CONNECTING..." + BACK
    //   Room        who is in (colour chip, name, YOU, HOST), "7/15", START RACE for the host or a waiting line
    //   Message     "this room is full" / "could not connect" + BACK
    //   Results     a small card at the top right next to the HUD's own results: the room's order, START AGAIN / Q
    // The view only draws and reports clicks (events); OnlineRaceDirector decides everything.
    public class OnlineLobbyView
    {
        public enum Page { None, Rooms, Connecting, Room, Message, Results }

        private const float CardWidth = 760f;
        private const float SideCardWidth = 360f;

        // The player clicked a room card, pressed Enter on the rooms page: room + the typed name (not cleaned yet).
        public event Action<TimeOfDay, string> JoinRequested;
        public event Action BackRequested;    // BACK button on the rooms, connecting and message pages
        public event Action StartRequested;   // START RACE / START AGAIN (the host)

        private readonly VisualElement root;
        private readonly VisualElement overlay;
        private readonly VisualElement background;
        private readonly VisualElement card;
        private readonly VisualElement sideCard;
        private TextField nameField;
        private Label problemLabel;
        private string signature = "";

        public Page Current { get; private set; } = Page.None;
        public TimeOfDay SelectedRoom { get; set; } = TimeOfDay.Noon;
        public bool IsOpen { get { return Current != Page.None; } }

        // The pages that sit over the start screen take every game key (O, M, 1, 2, 3, Enter ...): the lobby owns
        // the keyboard then. The results card is beside the HUD's own results, so the game keys stay alive there.
        public bool BlocksGameKeys { get { return IsOpen && Current != Page.Results; } }

        public string TypedName { get { return nameField != null ? nameField.value : ""; } }

        public OnlineLobbyView(VisualElement documentRoot)
        {
            root = documentRoot;
            overlay = new VisualElement { name = "online-lobby" };
            overlay.AddToClassList("menu-overlay");
            overlay.pickingMode = PickingMode.Ignore;

            background = new VisualElement();
            background.AddToClassList("menu-bg");
            overlay.Add(background); // catches the clicks beside the card, so nothing under the lobby is hit

            card = new VisualElement { name = "online-card" };
            card.AddToClassList("menu-card");
            card.AddToClassList("plate");
            card.AddToClassList("panel");
            card.style.width = CardWidth;
            overlay.Add(card);

            sideCard = new VisualElement { name = "online-results" };
            sideCard.AddToClassList("plate");
            sideCard.AddToClassList("panel");
            sideCard.style.position = Position.Absolute;
            sideCard.style.top = 24f;
            sideCard.style.right = 24f;
            sideCard.style.width = SideCardWidth;
            sideCard.style.alignItems = Align.Stretch;
            overlay.Add(sideCard);
        }

        // Is the cursor in the name field? UI Toolkit focuses an element inside the TextField, so look up the parents.
        public bool NameFieldHasFocus
        {
            get
            {
                if (nameField == null || Current != Page.Rooms)
                {
                    return false;
                }
                IPanel panel = nameField.panel;
                if (panel == null || panel.focusController == null)
                {
                    return false;
                }
                VisualElement focused = panel.focusController.focusedElement as VisualElement;
                for (VisualElement current = focused; current != null; current = current.parent)
                {
                    if (current == nameField)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        // ---- Pages ----

        public void ShowRooms(string name, string problem)
        {
            if (!Open(Page.Rooms, "rooms"))
            {
                ShowProblem(problem);
                return;
            }
            card.Clear();
            Title("ONLINE RACE", card);
            Caption("RACE YOUR CLASS LIVE: PICK A ROOM, 15 SEATS EACH, ONE LAP", card);

            VisualElement nameRow = Row(card);
            nameRow.style.marginTop = 18f;
            Caption("YOUR NAME", nameRow).style.marginRight = 12f;
            nameField = new TextField();
            nameField.AddToClassList("lb-name-field");
            nameField.maxLength = PlayerNameRules.MaxLength;
            nameField.isDelayed = false;
            nameField.value = name ?? "";
            nameField.style.width = 300f;
            nameField.style.height = 52f;
            nameRow.Add(nameField);
            if (WebPage.UsesNamePrompt)
            {
                // Phones: the browser's own text box brings the keyboard up reliably (same as the leaderboard form).
                nameField.focusable = false;
                nameField.RegisterCallback<ClickEvent>(evt => nameField.value = WebPage.PromptName(nameField.value));
            }
            nameField.RegisterValueChangedCallback(evt => ShowProblem(""));

            problemLabel = Chip("", true, card);
            problemLabel.style.marginTop = 10f;
            ShowProblem(problem);

            VisualElement rooms = Row(card);
            rooms.style.marginTop = 16f;
            RoomCard(TimeOfDay.Noon, "HIGH SUN", rooms);
            RoomCard(TimeOfDay.Sunset, "GOLDEN HOUR", rooms);

            VisualElement keys = Row(card);
            keys.style.marginTop = 18f;
            KeyHint("ENTER", "JOIN " + OnlineRooms.Label(SelectedRoom), keys);
            KeyHint("Q", "BACK", keys);
            Pill("BACK", false, () => Raise(BackRequested), keys).style.marginLeft = 18f;

            if (!WebPage.UsesNamePrompt && nameField.value.Length == 0)
            {
                nameField.schedule.Execute(() => nameField.Focus()).ExecuteLater(50);
            }
        }

        // The name was refused: say why under the field (our own words from PlayerNameRules.Message).
        public void ShowProblem(string problem)
        {
            if (problemLabel == null)
            {
                return;
            }
            string text = problem ?? "";
            problemLabel.text = text;
            problemLabel.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ShowConnecting(TimeOfDay room)
        {
            if (!Open(Page.Connecting, "connecting"))
            {
                return;
            }
            card.Clear();
            Title(OnlineRooms.Label(room), card);
            Label line = Chip("CONNECTING...", false, card);
            line.style.marginTop = 18f;
            VisualElement keys = Row(card);
            keys.style.marginTop = 18f;
            KeyHint("Q", "BACK", keys);
            Pill("BACK", false, () => Raise(BackRequested), keys).style.marginLeft = 18f;
        }

        // The room page. statusLine = the waiting / get ready / race in progress line ("" with the start button).
        public void ShowRoom(TimeOfDay room, IReadOnlyList<OnlinePlayer> players, int localId, int masterId, string statusLine, bool showStart)
        {
            string sig = "room|" + (int)room + "|" + localId + "|" + masterId + "|" + statusLine + "|" + showStart;
            for (int i = 0; players != null && i < players.Count; i++)
            {
                sig += "|" + players[i].Id + ":" + players[i].Name + ":" + players[i].ColourIndex;
            }
            if (!Open(Page.Room, sig))
            {
                return;
            }
            card.Clear();
            int count = players == null ? 0 : players.Count;
            VisualElement head = Row(card);
            Title(OnlineRooms.Label(room), head);
            Label counter = Chip(OnlineRooms.CountLine(count), false, head);
            counter.style.marginLeft = 16f;

            VisualElement list = new VisualElement();
            list.pickingMode = PickingMode.Ignore;
            list.style.marginTop = 14f;
            list.style.flexDirection = FlexDirection.Row;
            list.style.flexWrap = Wrap.Wrap;
            list.style.justifyContent = Justify.Center;
            list.style.maxWidth = CardWidth - 60f;
            card.Add(list);
            for (int i = 0; i < count; i++)
            {
                PlayerRow(players[i], players[i].Id == localId, players[i].Id == masterId, list);
            }

            VisualElement foot = Row(card);
            foot.style.marginTop = 18f;
            if (showStart)
            {
                Pill("START RACE", true, () => Raise(StartRequested), foot);
            }
            else if (!string.IsNullOrEmpty(statusLine))
            {
                Chip(statusLine, false, foot);
            }

            VisualElement keys = Row(card);
            keys.style.marginTop = 14f;
            if (showStart)
            {
                KeyHint("ENTER", "START", keys);
            }
            KeyHint("Q", "LEAVE", keys);
        }

        public void ShowMessage(string text)
        {
            if (!Open(Page.Message, "message|" + text))
            {
                return;
            }
            card.Clear();
            Title("ONLINE RACE", card);
            Label line = Chip(text ?? "", true, card);
            line.style.marginTop = 18f;
            Caption("RACE VS BOTS AND TIME ATTACK WORK WITHOUT THE INTERNET", card).style.marginTop = 10f;
            VisualElement keys = Row(card);
            keys.style.marginTop = 18f;
            KeyHint("Q", "BACK", keys);
            Pill("BACK", false, () => Raise(BackRequested), keys).style.marginLeft = 18f;
        }

        // The room's order beside the HUD's results card. rows come from FinishOrderRules (same on every screen).
        // waitLine = the line instead of START AGAIN (the time left, or the wait for the host), headline = the title.
        public void ShowResults(IReadOnlyList<FinishRow> rows, int localId, bool showStart, string waitLine, string headline)
        {
            string sig = "results|" + localId + "|" + showStart + "|" + waitLine + "|" + headline;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                sig += "|" + rows[i].Id + ":" + (int)rows[i].State + ":" + rows[i].Seconds.ToString("F3");
            }
            if (!Open(Page.Results, sig))
            {
                return;
            }
            sideCard.Clear();
            Label head = Caption(string.IsNullOrEmpty(headline) ? "ROOM RESULTS" : headline, sideCard);
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ResultRow(rows[i], i + 1, rows[i].Id == localId, sideCard);
            }
            VisualElement foot = Row(sideCard);
            foot.style.marginTop = 12f;
            if (showStart)
            {
                Pill("START AGAIN", true, () => Raise(StartRequested), foot);
            }
            else
            {
                Chip(string.IsNullOrEmpty(waitLine) ? "WAITING FOR THE HOST" : waitLine, false, foot);
            }
            VisualElement keys = Row(sideCard);
            keys.style.marginTop = 10f;
            if (showStart)
            {
                KeyHint("ENTER", "AGAIN", keys);
            }
            KeyHint("Q", "LEAVE", keys);
        }

        // Takes the lobby off the screen (racing, or the player left).
        public void Hide()
        {
            if (Current == Page.None)
            {
                return;
            }
            Current = Page.None;
            signature = "";
            nameField = null;
            problemLabel = null;
            overlay.RemoveFromClassList("is-shown");
            overlay.RemoveFromHierarchy();
        }

        // Puts the overlay on screen for a page. False = this page with this content is already showing.
        private bool Open(Page page, string contentSignature)
        {
            bool samePage = Current == page;
            if (samePage && signature == contentSignature)
            {
                return false;
            }
            if (!samePage)
            {
                nameField = null;
                problemLabel = null;
            }
            Current = page;
            signature = contentSignature;
            bool side = page == Page.Results;
            background.style.display = side ? DisplayStyle.None : DisplayStyle.Flex;
            card.style.display = side ? DisplayStyle.None : DisplayStyle.Flex;
            sideCard.style.display = side ? DisplayStyle.Flex : DisplayStyle.None;
            if (overlay.parent != root)
            {
                root.Add(overlay); // last child = drawn on top of the whole HUD
                overlay.schedule.Execute(() => overlay.AddToClassList("is-shown")).ExecuteLater(16); // next frame, so it fades in
            }
            return true;
        }

        // ---- Building blocks (the HUD's own classes, so the lobby looks like the rest) ----

        private static Label Title(string text, VisualElement parent)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("menu-title");
            label.AddToClassList("f-display");
            parent.Add(label);
            return label;
        }

        private static Label Caption(string text, VisualElement parent)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("caption");
            label.AddToClassList("f-ui");
            label.style.marginTop = 4f;
            parent.Add(label);
            return label;
        }

        private static VisualElement Row(VisualElement parent)
        {
            VisualElement row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            row.style.flexWrap = Wrap.Wrap;
            parent.Add(row);
            return row;
        }

        // A cream chip (coral = a problem) with ink letters.
        private static Label Chip(string text, bool coral, VisualElement parent)
        {
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("chip");
            label.AddToClassList("f-ui");
            if (coral)
            {
                label.AddToClassList("chip--coral");
            }
            label.style.fontSize = 20f;
            label.style.color = coral ? HudColors.Cream : HudColors.Ink;
            label.style.paddingLeft = 20f;
            label.style.paddingRight = 20f;
            label.style.paddingTop = 6f;
            label.style.paddingBottom = 10f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(label);
            return label;
        }

        // A button: the orange plate for the main action, navy for the rest.
        private static VisualElement Pill(string text, bool orange, Action onClick, VisualElement parent)
        {
            VisualElement pill = new VisualElement();
            pill.AddToClassList("menu-pill");
            pill.AddToClassList("plate");
            if (orange)
            {
                pill.AddToClassList("plate--orange");
            }
            pill.style.height = 56f;
            pill.style.paddingLeft = 22f;
            pill.style.paddingRight = 26f;
            pill.style.justifyContent = Justify.Center;
            Label label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("f-display");
            label.style.fontSize = 22f;
            label.style.color = orange ? HudColors.Ink : HudColors.Cream;
            if (orange)
            {
                label.style.unityTextOutlineWidth = 0f; // ink on orange needs no outline
            }
            pill.Add(label);
            pill.RegisterCallback<ClickEvent>(evt => onClick());
            parent.Add(pill);
            return pill;
        }

        private static void KeyHint(string key, string text, VisualElement parent)
        {
            VisualElement cap = new VisualElement();
            cap.pickingMode = PickingMode.Ignore;
            cap.AddToClassList("keycap");
            cap.AddToClassList("keycap--small");
            Label keyLabel = new Label(key);
            keyLabel.pickingMode = PickingMode.Ignore;
            keyLabel.AddToClassList("f-ui");
            cap.Add(keyLabel);
            parent.Add(cap);
            Label words = new Label(text);
            words.pickingMode = PickingMode.Ignore;
            words.AddToClassList("key-text");
            words.AddToClassList("f-ui");
            parent.Add(words);
        }

        // NOON ROOM / SUNSET ROOM: a tall card like the mode cards; a click joins that room with the typed name.
        private void RoomCard(TimeOfDay room, string mood, VisualElement parent)
        {
            VisualElement cardElement = new VisualElement();
            cardElement.AddToClassList("menu-pill");
            cardElement.AddToClassList("plate");
            if (room == SelectedRoom)
            {
                cardElement.AddToClassList("plate--blue");
            }
            cardElement.style.flexDirection = FlexDirection.Column;
            cardElement.style.alignItems = Align.Center;
            cardElement.style.justifyContent = Justify.Center;
            cardElement.style.height = 104f;
            cardElement.style.width = 250f;
            cardElement.style.marginLeft = 10f;
            cardElement.style.marginRight = 10f;
            cardElement.style.paddingBottom = 12f;
            Label title = new Label(OnlineRooms.Label(room));
            title.pickingMode = PickingMode.Ignore;
            title.AddToClassList("f-display");
            title.style.fontSize = 24f;
            title.style.color = HudColors.Cream;
            cardElement.Add(title);
            Label sub = new Label(mood + "  ·  " + OnlineRooms.MaxPlayers + " SEATS");
            sub.pickingMode = PickingMode.Ignore;
            sub.AddToClassList("f-ui");
            sub.style.fontSize = 16f;
            sub.style.letterSpacing = 1f;
            sub.style.marginTop = 4f;
            sub.style.color = HudColors.WithAlpha(HudColors.Cream, 0.8f);
            cardElement.Add(sub);
            cardElement.RegisterCallback<ClickEvent>(evt =>
            {
                SelectedRoom = room;
                Raise(JoinRequested, room, TypedName);
            });
            parent.Add(cardElement);
        }

        // One player in the room: colour square, name, YOU, HOST.
        private static void PlayerRow(OnlinePlayer player, bool isMe, bool isHost, VisualElement parent)
        {
            VisualElement row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.width = 220f;
            row.style.marginTop = 4f;
            row.style.marginBottom = 4f;
            row.style.marginLeft = 8f;
            row.style.marginRight = 8f;
            parent.Add(row);

            VisualElement square = new VisualElement();
            square.pickingMode = PickingMode.Ignore;
            square.style.width = 18f;
            square.style.height = 18f;
            square.style.borderTopLeftRadius = 5f;
            square.style.borderTopRightRadius = 5f;
            square.style.borderBottomLeftRadius = 5f;
            square.style.borderBottomRightRadius = 5f;
            square.style.backgroundColor = OnlineColours.Of(player.ColourIndex);
            square.style.marginRight = 8f;
            row.Add(square);

            Label name = new Label(string.IsNullOrEmpty(player.Name) ? "PLAYER" : player.Name);
            name.pickingMode = PickingMode.Ignore;
            name.AddToClassList("f-ui");
            name.style.fontSize = 22f;
            name.style.color = isMe ? HudColors.Sun : HudColors.Cream;
            row.Add(name);

            if (isMe)
            {
                Tag("YOU", row);
            }
            if (isHost)
            {
                Tag("HOST", row);
            }
        }

        private static void Tag(string text, VisualElement parent)
        {
            Label tag = new Label(text);
            tag.pickingMode = PickingMode.Ignore;
            tag.AddToClassList("f-ui");
            tag.style.fontSize = 14f;
            tag.style.letterSpacing = 1f;
            tag.style.marginLeft = 8f;
            tag.style.color = HudColors.WithAlpha(HudColors.Cream, 0.7f);
            parent.Add(tag);
        }

        // One results line: "1  ANA  1:02.345", "RACING..." or "DNF".
        private static void ResultRow(FinishRow row, int line, bool isMe, VisualElement parent)
        {
            VisualElement element = new VisualElement();
            element.pickingMode = PickingMode.Ignore;
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.marginTop = 3f;
            parent.Add(element);

            Label place = new Label(row.State == FinishState.Finished ? row.Place.ToString() : "-");
            place.pickingMode = PickingMode.Ignore;
            place.AddToClassList("f-num");
            place.style.width = 30f;
            place.style.fontSize = 20f;
            place.style.color = HudColors.WithAlpha(HudColors.Cream, 0.8f);
            element.Add(place);

            Label name = new Label(string.IsNullOrEmpty(row.Name) ? "PLAYER" : row.Name);
            name.pickingMode = PickingMode.Ignore;
            name.AddToClassList("f-ui");
            name.style.flexGrow = 1f;
            name.style.fontSize = 20f;
            name.style.color = isMe ? HudColors.Sun : HudColors.Cream;
            element.Add(name);

            string timeText;
            if (row.State == FinishState.Finished)
            {
                timeText = RaceTimer.Format((float)row.Seconds);
            }
            else
            {
                timeText = row.State == FinishState.Racing ? "RACING..." : "DNF";
            }
            Label time = new Label(timeText);
            time.pickingMode = PickingMode.Ignore;
            time.AddToClassList("f-num");
            time.style.fontSize = 20f;
            time.style.color = row.State == FinishState.Finished ? HudColors.Cream : HudColors.WithAlpha(HudColors.Cream, 0.6f);
            element.Add(time);
        }

        private static void Raise(Action action)
        {
            if (action != null)
            {
                action();
            }
        }

        private static void Raise(Action<TimeOfDay, string> action, TimeOfDay room, string name)
        {
            if (action != null)
            {
                action(room, name);
            }
        }
    }
}
