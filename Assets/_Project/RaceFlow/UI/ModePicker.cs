using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The three race modes on the start screen (Luc, 16 Nov 2026): RACE VS BOTS, TIME ATTACK, ONLINE RACE.
    // Built in code (like HudMenus' panels), so Main.unity and RaceHud.uxml need no change. RaceHud creates it and
    // calls Update() every frame.
    // Mario Kart style (team test 17 Nov 2026): mode -> driver -> go, for EVERY mode.
    // - Click or tap anywhere on a card: that mode is picked and the kart builder opens (UI/KartBuilder).
    //   The cards look like buttons: the mouse on a card lifts it (bigger, glass plate), a press pushes it in, and a
    //   small arrow on every card says "this goes on". The keycaps 1 2 3 stay as a hint for the keyboard.
    // - The orange follows the mouse (Luc, 18 Nov 2026): the card under the mouse turns orange, the picked card
    //   waits as glass with an orange edge, and when the mouse leaves the row the picked card is orange again.
    //   Only one card is orange at a time (RaceModeRules.OrangeCard). Touch and the dimmed Online card: no orange hover.
    // - Keys 1, 2, 3 do the same as a click on that card.
    // - Any other key or click opens the driver select in the highlighted mode (the last one played).
    // - ONLINE RACE (M2): the driver select comes first too; its READY then opens the online lobby (name + join,
    //   OnlineRaceHooks.OpenLobby), where the room starts the race for everyone. In a build without the online part
    //   the card is dimmed and picking it shows OnlineRaceHooks.UnavailableMessage (RaceManager.TrySelectMode refuses it too).
    // - Only a real click on a card is the card's: a key pressed while the mouse rests on a card is a normal key
    //   (MenuInputRules.ClickBelongsToButton; before, a parked mouse swallowed Space and Enter).
    public class ModePicker
    {
        private const float MessageSeconds = 2.2f;
        private const float HoverScale = 1.08f;
        private const float PressScale = 0.95f;
        private static readonly RaceMode[] Modes = { RaceMode.RaceVsBots, RaceMode.TimeAttack, RaceMode.OnlineRace };

        private readonly RaceManager raceManager;
        private readonly VisualElement hudRoot;
        private readonly VisualElement row;
        private readonly VisualElement[] cards = new VisualElement[Modes.Length];
        private readonly Label[] titles = new Label[Modes.Length];
        private readonly Label[] subtitles = new Label[Modes.Length];
        private readonly TriangleMark[] arrows = new TriangleMark[Modes.Length];
        private readonly Label message;
        private float messageUntil = -1f;  // real time until the refused chip hides
        private bool usedInputThisFrame;   // a key or click this frame belonged to the picker (not "start the race")
        private RaceMode shownMode = (RaceMode)(-1);
        private bool shownOnlineAvailable; // the Online assembly plugs in after the scene loaded: re-check in Update
        private int hovered = -1;          // the card under the mouse, -1 = none
        private int pressed = -1;          // the card the mouse button went down on, -1 = none

        // True while a click or key belongs to the picker, so it must not ALSO start the race (RaceManager.MenuUsesInput).
        public bool UsesInput { get; private set; }

        // The card under the mouse (Play Mode test and QA), -1 = none.
        public int Hovered { get { return hovered; } }

        // The card that is orange right now (RaceModeRules.OrangeCard, Play Mode test and QA), -1 = none.
        public int OrangeCard { get; private set; }

        // The refused chip ("ONLINE RACE NEEDS THE ONLINE BUILD") is on screen (Play Mode test).
        public bool IsMessageShown { get { return Time.unscaledTime < messageUntil; } }

        public ModePicker(VisualElement root, RaceManager raceManager)
        {
            this.raceManager = raceManager;
            hudRoot = root.Q<VisualElement>("hud-root");
            VisualElement prompt = root.Q<VisualElement>("press-any-key");
            VisualElement content = prompt.parent;

            row = new VisualElement { name = "mode-picker" };
            row.AddToClassList("intro");
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 36f;
            row.style.transitionDelay = new List<TimeValue> { new TimeValue(1000, TimeUnit.Millisecond), new TimeValue(1000, TimeUnit.Millisecond), new TimeValue(0f) };
            for (int i = 0; i < Modes.Length; i++)
            {
                cards[i] = MakeCard(i);
                row.Add(cards[i]);
            }
            content.Insert(content.IndexOf(prompt), row); // the cards right above the "PRESS ANY KEY" plate

            message = new Label("") { name = "mode-message" };
            message.AddToClassList("chip");
            message.AddToClassList("f-ui");
            message.style.position = Position.Absolute;
            message.style.alignSelf = Align.Center;
            message.style.top = -46f;
            message.style.fontSize = 20f;
            message.style.color = HudColors.Ink;
            message.style.paddingLeft = 22f;
            message.style.paddingRight = 22f;
            message.style.paddingTop = 6f;
            message.style.paddingBottom = 10f;
            message.style.opacity = 0f;
            message.pickingMode = PickingMode.Ignore;
            row.Add(message);
            row.style.position = Position.Relative;
            // Nothing in the row catches clicks except the cards, and those only on the start screen (Update):
            // the hidden start screen must never swallow a tap meant for the phone's driving buttons.
            HudFx.IgnoreClicksDeep(row);
            shownOnlineAvailable = OnlineRaceHooks.Available;
            ShowAvailability(shownOnlineAvailable);
            ShowSelected(raceManager.Mode);
        }

        // One mode card: number key, big title, small line. Picked = orange plate; a mode that cannot be picked
        // (Online without the online build) is dimmed by ShowAvailability.
        private VisualElement MakeCard(int index)
        {
            RaceMode mode = Modes[index];
            VisualElement card = new VisualElement { name = "mode-" + (index + 1) };
            card.AddToClassList("menu-pill");
            card.AddToClassList("plate");
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            card.style.height = 92f;
            card.style.width = 214f;
            card.style.marginLeft = 7f;
            card.style.marginRight = 7f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 4f;
            card.style.paddingBottom = 12f;

            VisualElement top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            VisualElement keycap = new VisualElement();
            keycap.AddToClassList("keycap");
            keycap.AddToClassList("keycap--small");
            Label key = new Label((index + 1).ToString());
            key.AddToClassList("f-ui");
            keycap.Add(key);
            top.Add(keycap);
            titles[index] = new Label(RaceModeRules.Title(mode));
            titles[index].AddToClassList("f-display");
            titles[index].style.fontSize = 21f;
            titles[index].style.marginLeft = 8f;
            titles[index].style.color = HudColors.Cream;
            top.Add(titles[index]);
            card.Add(top);

            subtitles[index] = new Label(RaceModeRules.Subtitle(mode));
            subtitles[index].AddToClassList("f-ui");
            subtitles[index].style.fontSize = 16f;
            subtitles[index].style.letterSpacing = 1f;
            subtitles[index].style.marginTop = 4f;
            subtitles[index].style.color = HudColors.WithAlpha(HudColors.Cream, 0.8f);
            card.Add(subtitles[index]);

            // "This card goes on": a small arrow in the bottom right corner (TriangleMark turned to point right).
            arrows[index] = new TriangleMark(HudColors.Cream, HudColors.WithAlpha(HudColors.Ink, 0.6f), 1.2f);
            arrows[index].style.position = Position.Absolute;
            arrows[index].style.right = 10f;
            arrows[index].style.bottom = 13f;
            arrows[index].style.width = 13f;
            arrows[index].style.height = 15f;
            arrows[index].style.rotate = new Rotate(Angle.Degrees(90f));
            card.Add(arrows[index]);

            // The whole card is the hit target: every child ignores clicks (HudFx.IgnoreClicksDeep in the
            // constructor), so a press on the title, the subtitle or the keycap lands on the card itself.
            card.RegisterCallback<PointerEnterEvent>(evt => SetHovered(index, true));
            card.RegisterCallback<PointerLeaveEvent>(evt => SetHovered(index, false));
            card.RegisterCallback<PointerDownEvent>(evt => SetPressed(index));
            card.RegisterCallback<PointerUpEvent>(evt => SetPressed(-1));
            card.RegisterCallback<ClickEvent>(evt =>
            {
                SetPressed(-1);
                Pick(mode, true);
            });
            return card;
        }

        // Dims the cards that cannot be picked right now (RaceModeRules.LooksDimmed): Online without the online
        // build. The Online assembly plugs itself in after the scene loaded, so Update calls this again when it did.
        private void ShowAvailability(bool onlineAvailable)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool dimmed = RaceModeRules.LooksDimmed(Modes[i], onlineAvailable);
                cards[i].style.opacity = dimmed ? 0.62f : 1f;
                if (dimmed)
                {
                    subtitles[i].style.color = HudColors.Sun;
                }
            }
        }

        // Called by RaceHud.Update every frame (before RaceManager reads the keys, DefaultExecutionOrder -50).
        public void Update(RaceState state, bool otherMenuOpen)
        {
            bool active = state == RaceState.Ready && raceManager.IsSetUp && !otherMenuOpen && !raceManager.IsPaused;
            for (int i = 0; i < cards.Length; i++)
            {
                HudFx.SetClickable(cards[i], active);
            }
            if (!active && (hovered >= 0 || pressed >= 0))
            {
                hovered = -1; // a card that cannot be clicked must not stay lifted (no PointerLeave comes then)
                pressed = -1;
                ShowSelected(raceManager.Mode);
            }
            // A press on a card belongs to the card (its ClickEvent picks the mode on release). A key while the
            // mouse only rests on a card is a key.
            bool cardClick = active && MenuInputRules.ClickBelongsToButton(RaceKeys.ClickPressed(), MouseOnCard());
            if (active)
            {
                int key = RaceKeys.ModeKeyPressed();
                if (key > 0)
                {
                    Pick(Modes[key - 1], false);
                }
                else if (!cardClick && RaceModeRules.OpensLobby(raceManager.Mode) && !raceManager.ExternalStartOnly
                         && !raceManager.MenuUsesInput && RaceKeys.AnyKeyOrClickPressed())
                {
                    // Online highlighted (back from its driver select): "any key" goes to the driver select again,
                    // like in the offline modes (RaceManager does that for them, it never starts an online race).
                    Pick(raceManager.Mode, false);
                }
            }
            if (shownOnlineAvailable != OnlineRaceHooks.Available)
            {
                shownOnlineAvailable = OnlineRaceHooks.Available;
                ShowAvailability(shownOnlineAvailable);
                ShowSelected(raceManager.Mode); // the subtitle colours depend on it too
            }
            if (shownMode != raceManager.Mode)
            {
                ShowSelected(raceManager.Mode);
            }
            message.style.opacity = IsMessageShown ? 1f : 0f;

            UsesInput = active && (usedInputThisFrame || cardClick);
            usedInputThisFrame = false;
        }

        // Pick a mode (a click or tap on its card, or its key) and open the driver select for it. Every mode goes
        // there first; READY on it starts the countdown, or online opens the lobby (UI/KartBuilder.Ready). The
        // click or key is used up here, so the RaceManager does not see it as "any key" as well.
        // A refused mode (Online without the online build): show the message, nothing else happens.
        // Public for the Play Mode tests (RaceModesPlayTests, DriverSelectPlayTests). byClick only says where it came from.
        public void Pick(RaceMode mode, bool byClick)
        {
            string refused;
            if (raceManager.TrySelectMode(mode, out refused))
            {
                messageUntil = -1f;
                ShowSelected(mode);
                usedInputThisFrame = true;
                if (MenuInputRules.OpensDriverSelect(mode, OnlineRaceHooks.Available))
                {
                    raceManager.RequestStart(); // opens the kart builder (UI/KartBuilder), READY there goes on
                }
                return;
            }
            if (refused.Length > 0)
            {
                message.text = refused;
                messageUntil = Time.unscaledTime + MessageSeconds;
                usedInputThisFrame = true; // the "3" key must not start a race in the old mode
            }
        }

        // Same as the mouse entering (on) or leaving (off) card index 0, 1 or 2. Public for the Play Mode test
        // (RaceModesPlayTests), the cards call it from their PointerEnter and PointerLeave events.
        public void HoverCard(int index, bool on)
        {
            if (index < 0 || index >= cards.Length)
            {
                return; // no such card: nothing to hover
            }
            SetHovered(index, on);
        }

        private void SetHovered(int index, bool on)
        {
            if (on)
            {
                hovered = index;
            }
            else if (hovered == index)
            {
                hovered = -1;
                if (pressed == index)
                {
                    pressed = -1; // dragged off the card: no click comes, the card springs back
                }
            }
            ShowSelected(raceManager.Mode);
        }

        private void SetPressed(int index)
        {
            pressed = index;
            ShowSelected(raceManager.Mode);
        }

        // One card orange (RaceModeRules.OrangeCard: the one under the mouse, else the picked one); the picked card
        // that waits while the mouse is on another card, and a hovered card that cannot turn orange, get the glass
        // plate with the orange edge. The card under the mouse is lifted (bigger), the pressed card pushed in.
        // Inline scale beats the menu-pill hover scale, the USS transition still animates it.
        private void ShowSelected(RaceMode mode)
        {
            shownMode = mode;
            bool reduced = hudRoot != null && hudRoot.ClassListContains("reduce-motion");
            bool touch = hudRoot != null && hudRoot.ClassListContains("hud--touch");
            int pickedCard = -1;
            for (int i = 0; i < Modes.Length; i++)
            {
                if (Modes[i] == mode)
                {
                    pickedCard = i;
                }
            }
            bool hoveredDimmed = hovered >= 0 && RaceModeRules.LooksDimmed(Modes[hovered], shownOnlineAvailable);
            OrangeCard = RaceModeRules.OrangeCard(pickedCard, hovered, hoveredDimmed, touch);
            for (int i = 0; i < cards.Length; i++)
            {
                bool orange = i == OrangeCard;
                bool lifted = i == hovered;
                bool glass = !orange && (lifted || i == pickedCard);
                cards[i].EnableInClassList("plate--orange", orange);
                cards[i].EnableInClassList("plate--blue", glass);
                float scale = i == pressed ? PressScale : (lifted ? HoverScale : 1f);
                if (reduced)
                {
                    scale = 1f; // reduced motion: the plate colour alone says "hovered"
                }
                cards[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
                arrows[i].SetFill(orange ? HudColors.Ink : (glass ? HudColors.Orange : HudColors.Cream));
                titles[i].style.color = orange ? HudColors.Ink : HudColors.Cream;
                if (orange)
                {
                    titles[i].style.unityTextOutlineWidth = 0f; // ink on orange needs no outline
                }
                else
                {
                    titles[i].style.unityTextOutlineWidth = StyleKeyword.Null; // back to the f-display outline
                }
                if (!RaceModeRules.LooksDimmed(Modes[i], shownOnlineAvailable))
                {
                    subtitles[i].style.color = orange ? HudColors.Ink : HudColors.WithAlpha(HudColors.Cream, 0.8f);
                }
            }
        }

        // Is the mouse over one of the cards right now? (Same picking test as HudMenus.)
        private bool MouseOnCard()
        {
            IPanel panel = hudRoot.panel;
            Vector2 mouse;
            if (panel == null || !RaceKeys.TryGetMousePosition(out mouse))
            {
                return false;
            }
            Vector2 point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouse.x, Screen.height - mouse.y));
            for (VisualElement picked = panel.Pick(point); picked != null; picked = picked.parent)
            {
                if (picked == row)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
