using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P7: everything around the race that is not the race. RaceHud creates it in OnEnable and calls Update().
    // - Settings card (key O or the SETTINGS button, start and results screen) and credits (key C or CREDITS, start screen).
    // - Mute (key M or the SOUND button, any time) with a short "SOUND OFF" / "SOUND ON" toast.
    // - "PAUSED" overlay while RaceManager.IsPaused (the RaceManager does the pausing, this only shows it).
    // - The "reduce-motion" class on the HUD root (the USS removes slides, shakes and punches).
    // - Tells the RaceManager when a menu uses the keys and the mouse, so they do not also start the race.
    // Plain C# (no MonoBehaviour), so the scene needs no new component. Escape is never used: it leaves browser fullscreen.
    public class HudMenus
    {
        private const float ToastSeconds = 1.4f;

        private readonly RaceManager raceManager;
        private readonly GameSettings settings;
        private readonly SettingsPanel settingsPanel;
        private readonly CreditsPanel creditsPanel;

        private readonly VisualElement hudRoot;
        private readonly VisualElement startSettings;
        private readonly VisualElement startCredits;
        private readonly VisualElement startSound;
        private readonly Label startSoundText;
        private readonly VisualElement resultsSettings;
        private readonly VisualElement settingsScreen;
        private readonly VisualElement settingsBg;
        private readonly VisualElement settingsCard;
        private readonly VisualElement settingsClose;
        private readonly VisualElement creditsScreen;
        private readonly VisualElement creditsBg;
        private readonly VisualElement creditsCard;
        private readonly VisualElement creditsClose;
        private readonly VisualElement pauseScreen;
        private readonly VisualElement toast;
        private readonly Label toastText;

        private bool settingsOpen;
        private bool creditsOpen;
        private float toastVisibleUntil; // unscaled time: the toast also works while the race is paused

        public bool ReducedMotion
        {
            get { return settings.ReducedMotion; }
        }

        public HudMenus(VisualElement root, RaceManager raceManager)
        {
            this.raceManager = raceManager;
            hudRoot = root.Q<VisualElement>("hud-root");
            startSettings = root.Q<VisualElement>("start-settings");
            startCredits = root.Q<VisualElement>("start-credits");
            startSound = root.Q<VisualElement>("start-sound");
            startSoundText = root.Q<Label>("start-sound-text");
            resultsSettings = root.Q<VisualElement>("results-settings");
            settingsScreen = root.Q<VisualElement>("settings-screen");
            settingsBg = root.Q<VisualElement>("settings-bg");
            settingsCard = root.Q<VisualElement>("settings-card");
            settingsClose = root.Q<VisualElement>("settings-close");
            creditsScreen = root.Q<VisualElement>("credits-screen");
            creditsBg = root.Q<VisualElement>("credits-bg");
            creditsCard = root.Q<VisualElement>("credits-card");
            creditsClose = root.Q<VisualElement>("credits-close");
            pauseScreen = root.Q<VisualElement>("pause-screen");
            toast = root.Q<VisualElement>("toast");
            toastText = root.Q<Label>("toast-text");

            // The same Lisbon shapes as the start and results screens (drawn in code, see UI/CalcadaWave.cs).
            root.Q<VisualElement>("settings-wave-host").Add(new CalcadaWave());
            root.Q<VisualElement>("credits-wave-host").Add(new CalcadaWave());
            root.Q<VisualElement>("pause-wave-host").Add(new CalcadaWave());
            root.Q<VisualElement>("pause-pattern-host").Add(new AzulejoPattern());

            // Load the saved settings and use them at once (volume and reduced motion are right from the first frame).
            settings = SettingsStore.Load();
            SettingsStore.Apply(settings);
            settingsPanel = new SettingsPanel(settingsCard, settings, OnSettingsChanged);
            creditsPanel = new CreditsPanel(creditsCard);
            ShowSettingsEverywhere();

            // ClickEvent fires when the mouse button goes UP on the element. The press itself happened while the
            // mouse was already on a menu button, so the RaceManager ignored it (see Update).
            startSettings.RegisterCallback<ClickEvent>(evt => OpenSettings(!settingsOpen));
            resultsSettings.RegisterCallback<ClickEvent>(evt => OpenSettings(!settingsOpen));
            startCredits.RegisterCallback<ClickEvent>(evt => OpenCredits(!creditsOpen));
            startSound.RegisterCallback<ClickEvent>(evt => ToggleMute());
            settingsClose.RegisterCallback<ClickEvent>(evt => CloseMenus());
            settingsBg.RegisterCallback<ClickEvent>(evt => CloseMenus()); // a click next to the card closes it
            creditsClose.RegisterCallback<ClickEvent>(evt => CloseMenus());
            creditsBg.RegisterCallback<ClickEvent>(evt => CloseMenus());
            ShowMenus();
        }

        // Called by RaceHud.Update every frame.
        public void Update(RaceState state)
        {
            if (RaceKeys.MutePressed())
            {
                ToggleMute();
            }

            // Menus only on the start and results screen. Retry (Enter) from the results closes them by itself.
            bool menusAllowed = (state == RaceState.Ready || state == RaceState.Results) && !raceManager.IsPaused;
            if (!menusAllowed)
            {
                if (settingsOpen || creditsOpen)
                {
                    CloseMenus();
                }
            }
            else if (RaceKeys.SettingsPressed())
            {
                OpenSettings(!settingsOpen);
            }
            else if (RaceKeys.CreditsPressed() && (state == RaceState.Ready || creditsOpen))
            {
                OpenCredits(!creditsOpen);
            }
            else if (settingsOpen)
            {
                settingsPanel.HandleKeys();
            }
            else if (creditsOpen)
            {
                creditsPanel.HandleKeys();
            }

            HudFx.SetShown(pauseScreen, raceManager.IsPaused);
            HudFx.SetShown(toast, Time.unscaledTime < toastVisibleUntil);

            // Menu buttons can be clicked only while they are visible and no menu covers them.
            bool noMenu = !settingsOpen && !creditsOpen;
            bool ready = state == RaceState.Ready;
            HudFx.SetClickable(startSettings, ready && noMenu);
            HudFx.SetClickable(startCredits, ready && noMenu);
            HudFx.SetClickable(startSound, ready && noMenu);
            HudFx.SetClickable(resultsSettings, state == RaceState.Results && noMenu);

            // While a menu is open, or the mouse points at a menu button on the start screen, the keys and clicks
            // belong to the menu: "press any key to start" must not fire as well.
            raceManager.MenuUsesInput = !noMenu || (ready && MouseOnStartButton());
        }

        // Which HUD element is under the mouse? Only the menu buttons can be picked on the start screen
        // (everything else ignores clicks), so this is true exactly when a click would hit a menu button.
        private bool MouseOnStartButton()
        {
            IPanel panel = hudRoot.panel;
            Vector2 mouse;
            if (panel == null || !RaceKeys.TryGetMousePosition(out mouse))
            {
                return false;
            }
            // Screen pixels start at the bottom left, the panel starts at the top left: flip y first.
            Vector2 point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouse.x, Screen.height - mouse.y));
            VisualElement picked = panel.Pick(point);
            return IsInside(picked, startSettings) || IsInside(picked, startCredits) || IsInside(picked, startSound);
        }

        private static bool IsInside(VisualElement element, VisualElement container)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current == container)
                {
                    return true;
                }
            }
            return false;
        }

        private void OpenSettings(bool open)
        {
            settingsOpen = open;
            creditsOpen = false;
            if (open)
            {
                settingsPanel.Refresh();
            }
            ShowMenus();
        }

        private void OpenCredits(bool open)
        {
            creditsOpen = open;
            settingsOpen = false;
            if (open)
            {
                creditsPanel.Open();
            }
            ShowMenus();
        }

        private void CloseMenus()
        {
            settingsOpen = false;
            creditsOpen = false;
            ShowMenus();
        }

        // Fade the cards in or out and switch their clicks on or off with them.
        private void ShowMenus()
        {
            HudFx.SetShown(settingsScreen, settingsOpen);
            HudFx.SetShown(creditsScreen, creditsOpen);
            HudFx.SetClickable(settingsBg, settingsOpen);
            HudFx.SetClickable(settingsCard, settingsOpen); // the card catches clicks, so only a click NEXT to it closes
            HudFx.SetClickable(settingsClose, settingsOpen);
            settingsPanel.SetClickable(settingsOpen);
            HudFx.SetClickable(creditsBg, creditsOpen);
            HudFx.SetClickable(creditsCard, creditsOpen);
            HudFx.SetClickable(creditsClose, creditsOpen);
            creditsPanel.SetClickable(creditsOpen);
        }

        private void ToggleMute()
        {
            settings.ToggleMute();
            OnSettingsChanged();
            toastText.text = settings.Muted ? "SOUND OFF" : "SOUND ON";
            toast.EnableInClassList("toast--off", settings.Muted);
            toastVisibleUntil = Time.unscaledTime + ToastSeconds;
        }

        // Any change (panel, M key, SOUND button): use it, save it, show it.
        private void OnSettingsChanged()
        {
            SettingsStore.Apply(settings);
            SettingsStore.Save(settings);
            ShowSettingsEverywhere();
            settingsPanel.Refresh();
        }

        // The parts outside the settings card that show a setting.
        private void ShowSettingsEverywhere()
        {
            hudRoot.EnableInClassList("reduce-motion", settings.ReducedMotion);
            startSoundText.text = settings.Muted ? "SOUND OFF" : "SOUND ON";
            startSound.EnableInClassList("menu-pill--muted", settings.Muted);
        }
    }
}
