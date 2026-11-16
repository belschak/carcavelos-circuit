using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // The three race modes on the start screen (Luc, 16 Nov 2026): RACE VS BOTS, TIME ATTACK, ONLINE RACE.
    // Built in code (like HudMenus' panels), so Main.unity and RaceHud.uxml need no change. RaceHud creates it and
    // calls Update() every frame.
    // - Click or tap a card: that mode is picked and the race starts at once (one tap to the next step).
    // - Keys 1, 2, 3 pick a mode; the same key press then starts the race like any other key.
    // - Any other key or click starts the race in the highlighted mode (the last one played).
    // - ONLINE RACE is not playable yet: its card is dimmed, picking it shows "ONLINE RACE IS COMING SOON" and
    //   the race does not start (RaceManager.TrySelectMode refuses it too).
    public class ModePicker
    {
        private const float MessageSeconds = 2.2f;
        private static readonly RaceMode[] Modes = { RaceMode.RaceVsBots, RaceMode.TimeAttack, RaceMode.OnlineRace };

        private readonly RaceManager raceManager;
        private readonly VisualElement hudRoot;
        private readonly VisualElement row;
        private readonly VisualElement[] cards = new VisualElement[Modes.Length];
        private readonly Label[] titles = new Label[Modes.Length];
        private readonly Label[] subtitles = new Label[Modes.Length];
        private readonly Label message;
        private float messageUntil = -1f;  // real time until the "coming soon" chip hides
        private bool usedInputThisFrame;   // a key or click this frame belonged to the picker (not "start the race")
        private RaceMode shownMode = (RaceMode)(-1);

        // True while a click or key belongs to the picker, so it must not ALSO start the race (RaceManager.MenuUsesInput).
        public bool UsesInput { get; private set; }

        // The "coming soon" chip is on screen (Play Mode test).
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

            message = new Label(RaceModeRules.ComingSoonMessage) { name = "mode-message" };
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
            ShowSelected(raceManager.Mode);
        }

        // One mode card: number key, big title, small line. Picked = orange plate, Online = dimmed with "SOON".
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

            if (!RaceModeRules.IsPlayable(mode))
            {
                card.style.opacity = 0.62f;
                subtitles[index].style.color = HudColors.Sun;
            }
            card.RegisterCallback<ClickEvent>(evt => Pick(mode, true));
            return card;
        }

        // Called by RaceHud.Update every frame (before RaceManager reads the keys, DefaultExecutionOrder -50).
        public void Update(RaceState state, bool otherMenuOpen)
        {
            bool active = state == RaceState.Ready && raceManager.IsSetUp && !otherMenuOpen && !raceManager.IsPaused;
            for (int i = 0; i < cards.Length; i++)
            {
                HudFx.SetClickable(cards[i], active);
            }
            if (active)
            {
                int key = RaceKeys.ModeKeyPressed();
                if (key > 0)
                {
                    Pick(Modes[key - 1], false);
                }
            }
            if (shownMode != raceManager.Mode)
            {
                ShowSelected(raceManager.Mode);
            }
            message.style.opacity = IsMessageShown ? 1f : 0f;

            // The mouse on a card: its press must not start the race before the ClickEvent picks the mode.
            UsesInput = active && (usedInputThisFrame || MouseOnCard());
            usedInputThisFrame = false;
        }

        // Pick a mode. A playable mode by click starts the race straight away; by key the key press itself starts
        // it (RaceManager sees it as "any key"). Online: show the message and keep this input away from the race.
        private void Pick(RaceMode mode, bool byClick)
        {
            string refused;
            if (raceManager.TrySelectMode(mode, out refused))
            {
                messageUntil = -1f;
                ShowSelected(mode);
                if (byClick)
                {
                    raceManager.BeginRace();
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

        private void ShowSelected(RaceMode mode)
        {
            shownMode = mode;
            for (int i = 0; i < cards.Length; i++)
            {
                bool picked = Modes[i] == mode;
                cards[i].EnableInClassList("plate--orange", picked);
                titles[i].style.color = picked ? HudColors.Ink : HudColors.Cream;
                if (picked)
                {
                    titles[i].style.unityTextOutlineWidth = 0f; // ink on orange needs no outline
                }
                else
                {
                    titles[i].style.unityTextOutlineWidth = StyleKeyword.Null; // back to the f-display outline
                }
                if (RaceModeRules.IsPlayable(Modes[i]))
                {
                    subtitles[i].style.color = picked ? HudColors.Ink : HudColors.WithAlpha(HudColors.Cream, 0.8f);
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
