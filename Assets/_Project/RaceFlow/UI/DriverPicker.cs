using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Driver select on the start screen (Luc, 17 Nov 2026), a kart game's character select between the race mode
    // and the countdown. Built in code like ModePicker, so Main.unity and RaceHud.uxml need no change.
    // Flow (every mode, Mario Kart style): pick a mode (click a card, or any key) -> this panel replaces the mode
    // cards -> pick a driver -> READY. Offline READY starts the countdown; online READY opens the online lobby
    // (OnlineRaceHooks.OpenLobby: name + join), which then starts the race for the whole room.
    // - Four cards in a row (Luc's roster, each driver with their own kart): the driver's name above the portrait (Resources/DriverPortraits, made by Role 3's
    //   DriverPortraitBuilder). The mouse on a card makes it bigger and shows the driver's one-line description
    //   under the row. Click or tap = pick that driver (orange plate, a press pushes the card in); then the READY
    //   button appears below.
    // - Keys: A/D or left/right move the pick, Enter or Space = READY, Escape or Backspace = back to the modes.
    // - The last pick is saved (DriverChoice), so the next visit opens with it picked and READY already there.
    // Looks only: every driver drives the same kart (the Time Attack leaderboard stays fair).
    public class DriverPicker
    {
        private const float CardWidth = 184f;
        private const float PortraitSize = 160f;
        private const float HoverScale = 1.1f;

        private readonly RaceManager raceManager;
        private readonly VisualElement hudRoot;
        private readonly VisualElement panel;
        private readonly VisualElement[] cards = new VisualElement[DriverRoster.Count];
        private readonly Label[] names = new Label[DriverRoster.Count];
        private readonly Label line;
        private readonly VisualElement readyButton;
        private readonly VisualElement backButton;
        private readonly List<VisualElement> hiddenWhileOpen = new List<VisualElement>(); // mode cards, prompt, menu row
        private readonly List<Texture2D> portraits = new List<Texture2D>();
        private int hovered = -1;     // the card under the mouse, -1 = none
        private int picked = -1;      // the driver picked in this visit of the panel, -1 = none yet
        private int pressed = -1;     // the card the mouse button went down on, -1 = none
        private int closedFrame = -1; // the frame the panel closed in: its key (Backspace, Enter) is used up
        private bool readyThisFrame;  // a click on READY or BACK this frame (handled in Update)
        private bool backThisFrame;

        // The panel is on screen (it takes every key and click of the start screen while open).
        public bool IsOpen { get; private set; }

        // True while a click or key belongs to the panel, so it must not ALSO count as "any key" (MenuUsesInput).
        // Also in the frame it closed: Backspace (back) is a normal start key and would open the panel again at once.
        public bool UsesInput { get { return IsOpen || closedFrame == Time.frameCount; } }

        // The driver whose card is highlighted (Play Mode test), -1 = none.
        public int Picked { get { return picked; } }

        // Menu sounds, like HudMenus: RaceHud passes them on to RaceSounds.
        public event Action<MenuSound> SoundRequested;

        public DriverPicker(VisualElement root, RaceManager raceManager)
        {
            this.raceManager = raceManager;
            hudRoot = root.Q<VisualElement>("hud-root");
            VisualElement prompt = root.Q<VisualElement>("press-any-key");
            VisualElement content = prompt.parent;
            AddIfFound(root.Q<VisualElement>("mode-picker"));
            AddIfFound(prompt);
            AddIfFound(root.Q<VisualElement>("start-best"));
            AddIfFound(root.Q<VisualElement>("start-menu"));
            AddIfFound(root.Q<VisualElement>("start-board-host")); // the leaderboard would cover the last cards

            panel = new VisualElement { name = "driver-picker" };
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Center;
            panel.style.marginTop = 18f;
            panel.style.display = DisplayStyle.None;

            Label heading = new Label("CHOOSE YOUR DRIVER");
            heading.AddToClassList("f-display");
            heading.style.fontSize = 26f;
            heading.style.color = HudColors.Cream;
            heading.style.marginBottom = 10f;
            heading.pickingMode = PickingMode.Ignore;
            panel.Add(heading);

            VisualElement row = new VisualElement { name = "driver-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = MakeCard(i);
                row.Add(cards[i]);
            }
            panel.Add(row);

            // The hovered (or picked) driver's one line. Always there and only its text changes, so nothing jumps.
            line = new Label(" ") { name = "driver-line" };
            line.AddToClassList("chip");
            line.AddToClassList("f-ui");
            line.style.marginTop = 14f;
            line.style.fontSize = 18f;
            line.style.color = HudColors.Ink;
            line.style.paddingLeft = 20f;
            line.style.paddingRight = 20f;
            line.style.paddingTop = 6f;
            line.style.paddingBottom = 10f;
            line.style.opacity = 0f;
            line.pickingMode = PickingMode.Ignore;
            panel.Add(line);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.Center;
            buttons.style.marginTop = 14f;
            buttons.pickingMode = PickingMode.Ignore;
            backButton = MakeButton("driver-back", "ESC", "BACK", false);
            backButton.RegisterCallback<ClickEvent>(evt => backThisFrame = true);
            buttons.Add(backButton);
            readyButton = MakeButton("driver-ready", "ENTER", "READY!", true);
            readyButton.RegisterCallback<ClickEvent>(evt => readyThisFrame = true);
            buttons.Add(readyButton);
            panel.Add(buttons);

            content.Insert(content.IndexOf(prompt), panel);
            raceManager.StartRequested += Open;
        }

        // RaceHud.OnDisable: without the HUD, "any key" must start the race again (RaceManager.RequestStart).
        public void Dispose()
        {
            raceManager.StartRequested -= Open;
            for (int i = 0; i < portraits.Count; i++)
            {
                Resources.UnloadAsset(portraits[i]);
            }
            portraits.Clear();
        }

        private void AddIfFound(VisualElement element)
        {
            if (element != null)
            {
                hiddenWhileOpen.Add(element);
            }
        }

        // One driver card: name on top, portrait on a soft square in the driver's colour.
        private VisualElement MakeCard(int index)
        {
            DriverInfo driver = DriverRoster.Get(index);
            VisualElement card = new VisualElement { name = "driver-" + index };
            card.AddToClassList("plate");
            card.style.width = CardWidth;
            card.style.marginLeft = 6f;
            card.style.marginRight = 6f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 14f;
            card.style.alignItems = Align.Center;
            card.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
            card.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };

            names[index] = new Label(driver.Name.ToUpperInvariant());
            names[index].AddToClassList("f-display");
            names[index].style.fontSize = 20f;
            names[index].style.color = HudColors.Cream;
            names[index].style.marginBottom = 4f;
            names[index].pickingMode = PickingMode.Ignore;
            card.Add(names[index]);

            VisualElement frame = new VisualElement();
            frame.style.width = PortraitSize;
            frame.style.height = PortraitSize;
            SetRadius(frame, 12f);
            frame.style.backgroundColor = HudColors.WithAlpha(driver.Colour, 0.55f);
            frame.style.borderBottomWidth = 3f;
            frame.style.borderBottomColor = driver.Colour;
            frame.pickingMode = PickingMode.Ignore;
            Texture2D portrait = Resources.Load<Texture2D>("DriverPortraits/" + driver.PortraitFile);
            if (portrait != null)
            {
                portraits.Add(portrait);
                frame.style.backgroundImage = new StyleBackground(portrait);
                frame.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            card.Add(frame);

            card.RegisterCallback<PointerEnterEvent>(evt => SetHovered(index));
            card.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (pressed == index)
                {
                    pressed = -1; // dragged off the card: no click comes, the card springs back
                }
                if (hovered == index)
                {
                    SetHovered(-1);
                }
            });
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                pressed = index;
                ShowState();
            });
            card.RegisterCallback<PointerUpEvent>(evt =>
            {
                pressed = -1;
                ShowState();
            });
            card.RegisterCallback<ClickEvent>(evt => Pick(index));
            return card;
        }

        // READY (orange, big) and BACK (blue, small), each with its key on a keycap.
        private static VisualElement MakeButton(string name, string key, string text, bool main)
        {
            VisualElement button = new VisualElement { name = name };
            button.AddToClassList("menu-pill");
            button.AddToClassList("plate");
            if (main)
            {
                button.AddToClassList("plate--orange");
            }
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.height = main ? 64f : 48f;
            button.style.paddingLeft = 16f;
            button.style.paddingRight = 20f;
            button.style.paddingBottom = 6f;
            button.style.marginLeft = 8f;
            button.style.marginRight = 8f;

            VisualElement keycap = new VisualElement();
            keycap.AddToClassList("keycap");
            keycap.AddToClassList("keycap--small");
            keycap.pickingMode = PickingMode.Ignore;
            Label keyLabel = new Label(key);
            keyLabel.AddToClassList("f-ui");
            keyLabel.pickingMode = PickingMode.Ignore;
            keycap.Add(keyLabel);
            button.Add(keycap);

            Label label = new Label(text);
            label.AddToClassList(main ? "f-display" : "f-ui");
            label.style.fontSize = main ? 30f : 18f;
            label.style.marginLeft = 10f;
            label.style.color = main ? HudColors.Ink : HudColors.Cream;
            if (main)
            {
                label.style.unityTextOutlineWidth = 0f; // ink on orange needs no outline
            }
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);
            return button;
        }

        // "Any key" or a mode card on the start screen: show the drivers instead of the modes.
        private void Open()
        {
            if (IsOpen || raceManager.State != RaceState.Ready || !raceManager.IsSetUp || raceManager.IsPaused
                || raceManager.ExternalStartOnly)
            {
                return; // ExternalStartOnly: the online lobby is open and owns the start screen
            }
            IsOpen = true;
            hovered = -1;
            pressed = -1;
            picked = DriverChoice.HasPicked ? DriverChoice.Index : -1;
            panel.style.display = DisplayStyle.Flex;
            for (int i = 0; i < hiddenWhileOpen.Count; i++)
            {
                hiddenWhileOpen[i].style.display = DisplayStyle.None;
            }
            ShowState();
            PlaySound(MenuSound.Open);
        }

        // Back to the modes (BACK, Escape) or away because the race starts.
        private void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            closedFrame = Time.frameCount;
            pressed = -1;
            SetHovered(-1);
            panel.style.display = DisplayStyle.None;
            for (int i = 0; i < hiddenWhileOpen.Count; i++)
            {
                hiddenWhileOpen[i].style.display = StyleKeyword.Null;
            }
        }

        // Called by RaceHud.Update every frame (before RaceManager reads the keys, DefaultExecutionOrder -50).
        public void Update(RaceState state, bool otherMenuOpen)
        {
            if (IsOpen && state != RaceState.Ready)
            {
                Close(); // the race started (READY, or a test called BeginRace)
            }
            bool active = IsOpen && raceManager.IsSetUp && !otherMenuOpen && !raceManager.IsPaused;
            for (int i = 0; i < cards.Length; i++)
            {
                HudFx.SetClickable(cards[i], active);
            }
            HudFx.SetClickable(readyButton, active && picked >= 0);
            HudFx.SetClickable(backButton, active);
            if (!active)
            {
                readyThisFrame = false;
                backThisFrame = false;
                return;
            }

            int step = RaceKeys.MenuHorizontal();
            if (step != 0)
            {
                Pick(DriverRoster.Step(picked, step));
            }
            if (backThisFrame || RaceKeys.BackPressed())
            {
                backThisFrame = false;
                Close();
                PlaySound(MenuSound.Close);
                return;
            }
            if ((readyThisFrame || RaceKeys.ReadyPressed()) && picked >= 0)
            {
                readyThisFrame = false;
                Ready();
            }
            readyThisFrame = false;
        }

        // Pick a driver (click, tap or the arrow keys). Saved at once, so the bots and the kart follow on the grid.
        public void Pick(int index)
        {
            if (!DriverRoster.IsValid(index))
            {
                return;
            }
            picked = index;
            DriverChoice.Select(index);
            ShowState();
            PlaySound(MenuSound.Toggle);
        }

        // READY: offline the countdown begins in the mode picked before; online the lobby opens (the driver is
        // saved first, so the lobby can send it to the room: DriverChoice).
        public void Ready()
        {
            if (picked < 0)
            {
                return;
            }
            DriverChoice.Select(picked);
            Close();
            if (MenuInputRules.ReadyOpensLobby(raceManager.Mode))
            {
                if (OnlineRaceHooks.OpenLobby != null)
                {
                    OnlineRaceHooks.OpenLobby(raceManager);
                }
                return;
            }
            raceManager.BeginRace();
        }

        private void SetHovered(int index)
        {
            hovered = index;
            ShowState();
        }

        // Picked card orange, hovered card bigger, the line for the hovered (else the picked) driver, READY once picked.
        private void ShowState()
        {
            bool reduced = hudRoot != null && hudRoot.ClassListContains("reduce-motion");
            for (int i = 0; i < cards.Length; i++)
            {
                bool isPicked = i == picked;
                cards[i].EnableInClassList("plate--orange", isPicked);
                names[i].style.color = isPicked ? HudColors.Ink : HudColors.Cream;
                names[i].style.unityTextOutlineWidth = isPicked ? new StyleFloat(0f) : new StyleFloat(StyleKeyword.Null);
                float scale = i == pressed ? 0.96f : (i == hovered ? HoverScale : (isPicked ? 1.04f : 1f));
                cards[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
                cards[i].style.transitionDuration = new List<TimeValue> { new TimeValue(reduced ? 0 : 120, TimeUnit.Millisecond) };
            }
            int shown = hovered >= 0 ? hovered : picked;
            line.text = shown >= 0 ? DriverRoster.Get(shown).Line : " ";
            line.style.opacity = shown >= 0 ? 1f : 0f;
            readyButton.style.opacity = picked >= 0 ? 1f : 0f;
        }

        private void PlaySound(MenuSound sound)
        {
            if (SoundRequested != null)
            {
                SoundRequested(sound);
            }
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
