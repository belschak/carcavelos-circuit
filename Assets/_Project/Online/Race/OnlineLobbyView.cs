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
    //   Rooms       name field + one JOIN RACE button (the one room of the class, 17 Nov 2026) + my light + BACK
    //   Connecting  "CONNECTING..." + BACK (+ RACE THE BOTS while it tries again)
    //   Room        who is in (driver portrait in the room colour, name, driver, YOU, HOST), "7/15", START RACE for
    //               the host or a waiting line, my light
    //   Message     "the room is full" / "could not connect" + TRY AGAIN + RACE THE BOTS + BACK (never a dead end)
    //   Results     a small card at the top right next to the HUD's own results: the room's order, START AGAIN / Q
    // The light button (and T) only changes MY look: noon or sunset is each player's own choice, it never travels.
    // The view only draws and reports clicks (events); OnlineRaceDirector decides everything.
    public class OnlineLobbyView
    {
        public enum Page { None, Rooms, Connecting, Room, Message, Results }

        private const float CardWidth = 760f;
        private const float SideCardWidth = 360f;
        // RaceHud's local leaderboard beside the results card (RaceHud.uxml). Online it stays empty (online laps are
        // never saved) and the room results card sat on top of it (QA 17 Nov 2026), so it is hidden while the room
        // results show. Hidden, not removed: the results card keeps its place left of the room results.
        public const string LocalBoardName = "results-board-host";
        // The start screen's leaderboard: when the race time runs out the DNF player is put back on the start screen
        // and the room results card sat on top of it (live QA 18 Nov 2026: "LEADERBOARD TOP 10" showed through).
        public const string StartBoardName = "start-board-host";
        public const string CheckpointPanelName = "cp-panel"; // RaceHud's CHECKPOINT bar, top right under the room results
        // Heights on touch screens (HUD px), thumb-sized like the other touch menus (qa-phone 17 Nov 2026: 82 HUD px
        // = 36.4 CSS px on an iPhone SE in Safari 667x320, 38.7 on an iPhone 15 844x340).
        public const float TouchPillHeight = 82f;
        public const float TouchNameFieldHeight = 76f;

        private const float PortraitSize = 34f;

        // The player clicked JOIN RACE or pressed Enter on the rooms page: the typed name (not cleaned yet).
        public event Action<string> JoinRequested;
        public event Action BackRequested;    // BACK (rooms, connecting, message pages) and LEAVE (room page, results)
        public event Action StartRequested;   // START RACE / START AGAIN (the host)
        public event Action LightRequested;   // the NOON / SUNSET button: my own light, the other way round
        public event Action RetryRequested;   // TRY AGAIN on the message page (= Enter there)
        public event Action BotsRequested;    // RACE THE BOTS: out of the lobby into a race against the bots

        // The room this screen is in or joins (OnlineRooms.LabelFor, overflow rooms) and its seats ("7/15").
        public string RoomLabel { get; set; } = OnlineRooms.Label;
        public int Seats { get; set; } = OnlineRooms.MaxPlayers;
        // Does the message page now showing offer TRY AGAIN? (Enter means TRY AGAIN there)
        public bool OffersRetry { get; private set; }

        private readonly VisualElement root;
        private readonly VisualElement overlay;
        private readonly VisualElement background;
        private readonly VisualElement card;
        private readonly VisualElement sideCard;
        private TextField nameField;
        private Label problemLabel;
        private Label lightLabel;             // the text on the light button of the page now showing, null = none
        private VisualElement hiddenBoard;    // the local leaderboard while the room results hide it, null = shown
        private VisualElement hiddenStartBoard; // the start screen leaderboard while the room results hide it
        private VisualElement hiddenCheckpoints; // RaceHud's checkpoint bar while the room results hide it, null = shown
        // Phones and tablets (WebPage.TouchMode): no keyboard hints (ENTER, T, Q) and thumb-sized buttons, like the
        // other menus on touch (RaceHud.uss .hud--touch). Read again every time a page is built.
        private bool touch;
        private readonly bool forcedTouch;    // tests: touch decided by the test, not by the page
        private readonly bool touchIsForced;
        private string signature = "";
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> portraits =
            new System.Collections.Generic.Dictionary<string, Texture2D>(); // DriverPortraits, loaded once

        public Page Current { get; private set; } = Page.None;
        public bool IsOpen { get { return Current != Page.None; } }

        // The pages that sit over the start screen take every game key (O, M, 1, 2, 3, Enter ...): the lobby owns
        // the keyboard then. The results card is beside the HUD's own results, so the game keys stay alive there.
        public bool BlocksGameKeys { get { return IsOpen && Current != Page.Results; } }

        public string TypedName { get { return nameField != null ? nameField.value : ""; } }

        public OnlineLobbyView(VisualElement documentRoot) : this(documentRoot, false, false)
        {
        }

        // Tests: a lobby that is (or is not) on a touch screen, whatever the page says.
        public OnlineLobbyView(VisualElement documentRoot, bool touchScreen) : this(documentRoot, touchScreen, true)
        {
        }

        private OnlineLobbyView(VisualElement documentRoot, bool touchScreen, bool forced)
        {
            forcedTouch = touchScreen;
            touchIsForced = forced;
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

        // light = my look right now (TimeOfDayChoice), on the light button.
        public void ShowRooms(string name, string problem, TimeOfDay light)
        {
            if (!Open(Page.Rooms, "rooms"))
            {
                ShowProblem(problem);
                ShowLight(light);
                return;
            }
            card.Clear();
            Title("ONLINE RACE", card);
            Caption("RACE YOUR CLASS LIVE: ONE ROOM FOR EVERYONE, " + Seats + " SEATS, ONE LAP", card);

            VisualElement nameRow = Row(card);
            nameRow.style.marginTop = 18f;
            Caption("YOUR NAME", nameRow).style.marginRight = 12f;
            nameField = new TextField();
            nameField.AddToClassList("lb-name-field");
            nameField.maxLength = PlayerNameRules.MaxLength;
            nameField.isDelayed = false;
            nameField.value = name ?? "";
            nameField.style.width = 300f;
            nameField.style.height = touch ? TouchNameFieldHeight : 52f;
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

            VisualElement join = Row(card);
            join.style.marginTop = 16f;
            JoinCard(join);

            VisualElement look = Row(card);
            look.style.marginTop = 14f;
            LightButton(light, look);

            VisualElement keys = Row(card);
            keys.style.marginTop = 18f;
            KeyHint("ENTER", "JOIN", keys); // no T hint here: the cursor is usually in the name field, where T is a letter
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

        // The light button of the page now showing (if it has one) says this light.
        public void ShowLight(TimeOfDay light)
        {
            if (lightLabel != null)
            {
                lightLabel.text = LightText(light);
            }
        }

        // text = "CONNECTING..." or the retry line (OnlineJoinRetry.RetryLine). offerBots = also RACE THE BOTS (while the
        // lobby tries again, so nobody has to wait for the last try to play).
        public void ShowConnecting(string text)
        {
            ShowConnecting(text, false);
        }

        public void ShowConnecting(string text, bool offerBots)
        {
            if (!Open(Page.Connecting, "connecting:" + RoomLabel + ":" + text + ":" + offerBots))
            {
                return;
            }
            card.Clear();
            Title(RoomLabel, card);
            Label line = Chip(text, false, card);
            line.style.marginTop = 18f;
            VisualElement keys = Row(card);
            keys.style.marginTop = 18f;
            KeyHint("Q", "BACK", keys);
            Pill("BACK", false, () => Raise(BackRequested), keys).style.marginLeft = 18f;
            if (offerBots)
            {
                BotsButton(keys, 18f);
            }
        }

        // The room page. statusLine = the waiting / get ready / race in progress line ("" with the start button).
        // light = my look right now, on the light button (a new light only changes the button's text, see ShowLight).
        public void ShowRoom(IReadOnlyList<OnlinePlayer> players, int localId, int masterId, string statusLine, bool showStart, TimeOfDay light)
        {
            string sig = "room|" + RoomLabel + "|" + Seats + "|" + localId + "|" + masterId + "|" + statusLine + "|" + showStart;
            for (int i = 0; players != null && i < players.Count; i++)
            {
                sig += "|" + players[i].Id + ":" + players[i].DisplayName + ":" + players[i].ColourIndex + ":" + players[i].DriverIndex;
            }
            if (!Open(Page.Room, sig))
            {
                ShowLight(light);
                return;
            }
            card.Clear();
            int count = players == null ? 0 : players.Count;
            VisualElement head = Row(card);
            Title(RoomLabel, head);
            Label counter = Chip(OnlineRooms.CountLine(count, Seats), false, head);
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
                if (!string.IsNullOrEmpty(statusLine))
                {
                    Chip(statusLine, false, foot).style.marginLeft = 14f; // the host's auto start line
                }
            }
            else if (!string.IsNullOrEmpty(statusLine))
            {
                Chip(statusLine, false, foot);
            }

            VisualElement look = Row(card);
            look.style.marginTop = 12f;
            LightButton(light, look);

            VisualElement keys = Row(card);
            keys.style.marginTop = 14f;
            if (showStart)
            {
                KeyHint("ENTER", "START", keys);
            }
            KeyHint("T", "LIGHT", keys);
            KeyHint("Q", "LEAVE", keys);
            LeaveButton(keys, touch ? 0f : 18f);
        }

        public void ShowMessage(string text)
        {
            ShowMessage(text, false);
        }

        // offerRetry = TRY AGAIN (Enter) for a problem that may go away (full room, no connection); RACE THE BOTS and
        // BACK are always there, so the online card is never a dead end (20 Nov 2026).
        public void ShowMessage(string text, bool offerRetry)
        {
            if (!Open(Page.Message, "message|" + text + "|" + offerRetry))
            {
                return;
            }
            OffersRetry = offerRetry;
            card.Clear();
            Title("ONLINE RACE", card);
            Label line = Chip(text ?? "", true, card);
            line.style.marginTop = 18f;
            Caption("RACE VS BOTS AND TIME ATTACK WORK WITHOUT THE INTERNET", card).style.marginTop = 10f;
            VisualElement actions = Row(card);
            actions.style.marginTop = 18f;
            if (offerRetry)
            {
                VisualElement retry = Pill("TRY AGAIN", true, () => Raise(RetryRequested), actions);
                retry.name = "online-retry";
            }
            BotsButton(actions, offerRetry ? 14f : 0f);
            VisualElement keys = Row(card);
            keys.style.marginTop = 14f;
            if (offerRetry)
            {
                KeyHint("ENTER", "TRY AGAIN", keys);
            }
            KeyHint("Q", "BACK", keys);
            Pill("BACK", false, () => Raise(BackRequested), keys).style.marginLeft = 18f;
        }

        // RACE THE BOTS: straight into an offline race (the five bots need no internet).
        private void BotsButton(VisualElement parent, float marginLeft)
        {
            VisualElement pill = Pill("RACE THE BOTS", false, () => Raise(BotsRequested), parent);
            pill.name = "online-bots";
            pill.style.marginLeft = marginLeft;
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
                if (!string.IsNullOrEmpty(waitLine))
                {
                    VisualElement autoRow = Row(sideCard); // the host's auto start line under the button
                    autoRow.style.marginTop = 8f;
                    Chip(waitLine, false, autoRow).style.fontSize = 16f;
                }
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
            VisualElement leaveRow = Row(sideCard); // own row: the side card is too narrow for keys + button
            leaveRow.style.marginTop = 8f;
            LeaveButton(leaveRow, 0f);
        }

        // LEAVE the room (= Q): a button too, so a phone (no Q key) or a mouse-only player can get out of the room
        // and of the room results (QA 17 Nov 2026: on touch there was no way back to the start screen).
        private void LeaveButton(VisualElement parent, float marginLeft)
        {
            VisualElement pill = Pill("LEAVE", false, () => Raise(BackRequested), parent);
            pill.name = "online-leave";
            pill.style.marginLeft = marginLeft;
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
            lightLabel = null;
            HideLocalBoard(false);
            overlay.RemoveFromClassList("is-shown");
            overlay.RemoveFromHierarchy();
        }

        // Is the HUD's local leaderboard hidden by the room results right now? (tests)
        public bool HidesLocalBoard { get { return hiddenBoard != null; } }

        // Is the start screen leaderboard hidden by the room results right now? (tests)
        public bool HidesStartBoard { get { return hiddenStartBoard != null; } }

        // Is RaceHud's checkpoint bar hidden by the room results right now? (tests)
        public bool HidesCheckpoints { get { return hiddenCheckpoints != null; } }

        // hide = the room results are on screen: the empty local leaderboard under them goes invisible (keeps its
        // space); false = it comes back as RaceHud styled it.
        private void HideLocalBoard(bool hide)
        {
            if (hide)
            {
                if (hiddenBoard == null)
                {
                    hiddenBoard = root.Q<VisualElement>(LocalBoardName);
                    if (hiddenBoard != null)
                    {
                        hiddenBoard.style.visibility = Visibility.Hidden;
                    }
                }
                if (hiddenStartBoard == null)
                {
                    hiddenStartBoard = root.Q<VisualElement>(StartBoardName);
                    if (hiddenStartBoard != null)
                    {
                        hiddenStartBoard.style.visibility = Visibility.Hidden;
                    }
                }
                // the CHECKPOINT 13/13 bar sits under the card's top (QA desktop 18 Nov 2026: its text showed through).
                // Visibility, not display: RaceHud sets the bar's display every frame.
                if (hiddenCheckpoints == null)
                {
                    hiddenCheckpoints = root.Q<VisualElement>(CheckpointPanelName);
                    if (hiddenCheckpoints != null)
                    {
                        hiddenCheckpoints.style.visibility = Visibility.Hidden;
                    }
                }
                return;
            }
            if (hiddenBoard != null)
            {
                hiddenBoard.style.visibility = StyleKeyword.Null;
                hiddenBoard = null;
            }
            if (hiddenStartBoard != null)
            {
                hiddenStartBoard.style.visibility = StyleKeyword.Null;
                hiddenStartBoard = null;
            }
            if (hiddenCheckpoints != null)
            {
                hiddenCheckpoints.style.visibility = StyleKeyword.Null;
                hiddenCheckpoints = null;
            }
        }

        // Puts the overlay on screen for a page. False = this page with this content is already showing.
        private bool Open(Page page, string contentSignature)
        {
            bool samePage = Current == page;
            bool touchNow = touchIsForced ? forcedTouch : WebPage.TouchMode;
            if (samePage && signature == contentSignature && touchNow == touch)
            {
                return false;
            }
            touch = touchNow; // a touch laptop can switch: the page is built again with or without key hints
            if (!samePage)
            {
                nameField = null;
                problemLabel = null;
            }
            lightLabel = null; // the page is built again; its light button (if any) sets this anew
            Current = page;
            signature = contentSignature;
            bool side = page == Page.Results;
            background.style.display = side ? DisplayStyle.None : DisplayStyle.Flex;
            card.style.display = side ? DisplayStyle.None : DisplayStyle.Flex;
            sideCard.style.display = side ? DisplayStyle.Flex : DisplayStyle.None;
            HideLocalBoard(side);
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
        private VisualElement Pill(string text, bool orange, Action onClick, VisualElement parent)
        {
            VisualElement pill = new VisualElement();
            pill.AddToClassList("menu-pill");
            pill.AddToClassList("plate");
            if (orange)
            {
                pill.AddToClassList("plate--orange");
            }
            pill.style.height = touch ? TouchPillHeight : 56f;
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

        // A key and what it does (ENTER JOIN). Not on touch screens: there is no keyboard to press it on.
        private void KeyHint(string key, string text, VisualElement parent)
        {
            if (touch)
            {
                return;
            }
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

        // JOIN RACE: one big orange card for the one room; a click (or tap) joins with the typed name.
        private void JoinCard(VisualElement parent)
        {
            VisualElement cardElement = new VisualElement { name = "online-join" };
            cardElement.AddToClassList("menu-pill");
            cardElement.AddToClassList("plate");
            cardElement.AddToClassList("plate--orange");
            cardElement.style.flexDirection = FlexDirection.Column;
            cardElement.style.alignItems = Align.Center;
            cardElement.style.justifyContent = Justify.Center;
            cardElement.style.height = 104f;
            cardElement.style.width = 420f;
            cardElement.style.paddingBottom = 12f;
            Label title = new Label("JOIN RACE");
            title.pickingMode = PickingMode.Ignore;
            title.AddToClassList("f-display");
            title.style.fontSize = 30f;
            title.style.color = HudColors.Ink;
            title.style.unityTextOutlineWidth = 0f; // ink on orange needs no outline
            cardElement.Add(title);
            Label sub = new Label(RoomLabel + "  ·  " + Seats + " SEATS");
            sub.pickingMode = PickingMode.Ignore;
            sub.AddToClassList("f-ui");
            sub.style.fontSize = 16f;
            sub.style.letterSpacing = 1f;
            sub.style.marginTop = 4f;
            sub.style.color = HudColors.WithAlpha(HudColors.Ink, 0.8f);
            cardElement.Add(sub);
            cardElement.RegisterCallback<ClickEvent>(evt => Raise(JoinRequested, TypedName));
            parent.Add(cardElement);
        }

        // "LIGHT: NOON" / "LIGHT: SUNSET": a navy pill that flips my own look (the room has no light of its own).
        private void LightButton(TimeOfDay light, VisualElement parent)
        {
            VisualElement pill = Pill(LightText(light), false, () => Raise(LightRequested), parent);
            pill.name = "online-light";
            lightLabel = pill.Q<Label>();
        }

        public static string LightText(TimeOfDay light)
        {
            return "LIGHT: " + TimeOfDayChoice.Label(light);
        }

        // One player in the room: the driver's portrait in a frame of the player's room colour (the colour of their
        // name tag and map dot), the name, and under it the driver plus YOU / HOST.
        private static void PlayerRow(OnlinePlayer player, bool isMe, bool isHost, VisualElement parent)
        {
            VisualElement row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.width = 212f;
            row.style.marginTop = 4f;
            row.style.marginBottom = 4f;
            row.style.marginLeft = 6f;
            row.style.marginRight = 6f;
            parent.Add(row);

            DriverInfo driver = DriverRoster.Get(KartLoadout.FromCode(player.DriverIndex).Driver);
            Color colour = OnlineColours.Of(player.ColourIndex);
            VisualElement frame = new VisualElement();
            frame.pickingMode = PickingMode.Ignore;
            frame.style.width = PortraitSize;
            frame.style.height = PortraitSize;
            frame.style.flexShrink = 0f;
            frame.style.borderTopLeftRadius = 8f;
            frame.style.borderTopRightRadius = 8f;
            frame.style.borderBottomLeftRadius = 8f;
            frame.style.borderBottomRightRadius = 8f;
            frame.style.borderTopWidth = 3f;
            frame.style.borderBottomWidth = 3f;
            frame.style.borderLeftWidth = 3f;
            frame.style.borderRightWidth = 3f;
            frame.style.borderTopColor = colour;
            frame.style.borderBottomColor = colour;
            frame.style.borderLeftColor = colour;
            frame.style.borderRightColor = colour;
            frame.style.backgroundColor = HudColors.WithAlpha(driver.Colour, 0.55f);
            frame.style.marginRight = 8f;
            Texture2D portrait = Portrait(driver);
            if (portrait != null)
            {
                frame.style.backgroundImage = new StyleBackground(portrait);
                frame.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            row.Add(frame);

            VisualElement words = new VisualElement();
            words.pickingMode = PickingMode.Ignore;
            words.style.flexDirection = FlexDirection.Column;
            words.style.flexShrink = 1f;
            row.Add(words);

            Label name = new Label(string.IsNullOrEmpty(player.DisplayName) ? "PLAYER" : player.DisplayName);
            name.pickingMode = PickingMode.Ignore;
            name.AddToClassList("f-ui");
            name.style.fontSize = 20f;
            name.style.color = isMe ? HudColors.Sun : HudColors.Cream;
            words.Add(name);

            string line = driver.Name.ToUpperInvariant() + (isMe ? "  ·  YOU" : "") + (isHost ? "  ·  HOST" : "");
            Label tags = new Label(line);
            tags.pickingMode = PickingMode.Ignore;
            tags.AddToClassList("f-ui");
            tags.style.fontSize = 13f;
            tags.style.letterSpacing = 1f;
            tags.style.color = HudColors.WithAlpha(HudColors.Cream, 0.7f);
            words.Add(tags);
        }

        // The driver's portrait (Resources/DriverPortraits, the same pictures as the driver select), null if missing.
        private static Texture2D Portrait(DriverInfo driver)
        {
            Texture2D found;
            if (!portraits.TryGetValue(driver.PortraitFile, out found))
            {
                found = Resources.Load<Texture2D>("DriverPortraits/" + driver.PortraitFile);
                portraits[driver.PortraitFile] = found;
            }
            return found;
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

        private static void Raise(Action<string> action, string name)
        {
            if (action != null)
            {
                action(name);
            }
        }
    }
}
