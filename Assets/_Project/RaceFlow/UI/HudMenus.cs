using System;
using CarcavelosCircuit.Shared;
using UnityEngine;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P7: everything around the race that is not the race. RaceHud creates it in OnEnable and calls Update().
    // - Settings card (key O or the SETTINGS button, start and results screen) and credits (key C or CREDITS, start screen).
    // - Mute (key M or the SOUND button, any time) with a short "SOUND OFF" / "SOUND ON" toast.
    // - V28 time of day (key T or the NOON/SUNSET button, start screen): TimeOfDayChoice; the campus world (Role 3)
    //   switches its light, MapFlyover shows it from above the sea.
    // - "PAUSED" overlay while RaceManager.IsPaused (the RaceManager does the pausing, this only shows it).
    // - The "reduce-motion" class on the HUD root (the USS removes slides, shakes and punches).
    // - Tells the RaceManager when a menu uses the keys and the mouse, so they do not also start the race.
    // - Says when a menu sound should play (event SoundRequested, audit 10). RaceHud passes it on to RaceSounds,
    //   which picks the clip. So this class stays silent itself and needs no AudioSource.
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
        private readonly VisualElement startMood;
        private readonly Label startMoodText;
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
        private int closedFrame = -1; // frame in which a menu was closed (its click must not start the race)

        // Open, close, page turn, slider step, on/off, mute. Fired AFTER the new settings are applied, so a slider
        // step's click already plays at the new volume (an audible preview).
        public event Action<MenuSound> SoundRequested;

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
            startMood = root.Q<VisualElement>("start-mood");
            startMoodText = root.Q<Label>("start-mood-text");
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
            settingsPanel = new SettingsPanel(settingsCard, settings, OnPanelChanged);
            creditsPanel = new CreditsPanel(creditsCard, OnPageTurned);
            ShowSettingsEverywhere();

            // ClickEvent fires when the mouse button goes UP on the element. The press itself happened while the
            // mouse was already on a menu button, so the RaceManager ignored it (see Update).
            startSettings.RegisterCallback<ClickEvent>(evt => OpenSettings(!settingsOpen));
            resultsSettings.RegisterCallback<ClickEvent>(evt => OpenSettings(!settingsOpen));
            startCredits.RegisterCallback<ClickEvent>(evt => OpenCredits(!creditsOpen));
            startSound.RegisterCallback<ClickEvent>(evt => ToggleMute());
            startMood.RegisterCallback<ClickEvent>(evt => NextMood());
            startMoodText.text = TimeOfDayChoice.Label(TimeOfDayChoice.Current);
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
                    HideMenus(); // the race starts (Enter on the results): no close sound on top of the retry click
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
            else if (RaceKeys.TimeOfDayPressed() && state == RaceState.Ready && !settingsOpen && !creditsOpen)
            {
                NextMood();
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
            HudFx.SetClickable(startMood, ready && noMenu);
            HudFx.SetClickable(resultsSettings, state == RaceState.Results && noMenu);

            // While a menu is open, or the mouse points at a menu button on the start screen, the keys and clicks
            // belong to the menu: "press any key to start" must not fire as well.
            raceManager.MenuUsesInput = !noMenu || (ready && (MouseOnStartButton() || Time.frameCount - closedFrame <= 1));
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
            return IsInside(picked, startSettings) || IsInside(picked, startCredits) || IsInside(picked, startSound) ||
                   IsInside(picked, startMood);
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
            PlaySound(open ? MenuSound.Open : MenuSound.Close);
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
            PlaySound(open ? MenuSound.Open : MenuSound.Close);
        }

        // The player closed a card (CLOSE button or a click next to it).
        private void CloseMenus()
        {
            HideMenus();
            PlaySound(MenuSound.Close);
        }

        private void HideMenus()
        {
            settingsOpen = false;
            creditsOpen = false;
            closedFrame = Time.frameCount; // the click that closed the menu must not also start the race
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

        // V28: noon -> sunset -> noon. Saved at once; the world and the camera listen to TimeOfDayChoice.Changed.
        private void NextMood()
        {
            TimeOfDayChoice.Set(TimeOfDayChoice.Next(TimeOfDayChoice.Current));
            startMoodText.text = TimeOfDayChoice.Label(TimeOfDayChoice.Current);
            PlaySound(MenuSound.Toggle);
        }

        private void ToggleMute()
        {
            settings.ToggleMute();
            OnSettingsChanged();
            toastText.text = settings.Muted ? "SOUND OFF" : "SOUND ON";
            toast.EnableInClassList("toast--off", settings.Muted);
            toastVisibleUntil = Time.unscaledTime + ToastSeconds;
            PlaySound(MenuSound.Mute);
        }

        // A row of the settings card changed: use it first, then the sound, so a volume step is heard at its new level.
        private void OnPanelChanged(SettingsItem item)
        {
            OnSettingsChanged();
            PlaySound(SoundFor(item));
        }

        // A volume row clicks like a slider step, an on/off row (sound, reduced motion) like a switch.
        // Public and static so an Edit Mode test can check it without a HUD.
        public static MenuSound SoundFor(SettingsItem item)
        {
            return GameSettings.IsVolume(item) ? MenuSound.SliderStep : MenuSound.Toggle;
        }

        private void OnPageTurned()
        {
            PlaySound(MenuSound.PageTurn);
        }

        // Any change (panel, M key, SOUND button): use it, save it, show it.
        private void OnSettingsChanged()
        {
            SettingsStore.Apply(settings);
            SettingsStore.Save(settings);
            ShowSettingsEverywhere();
            settingsPanel.Refresh();
        }

        private void PlaySound(MenuSound sound)
        {
            if (SoundRequested != null) SoundRequested(sound);
        }

        // The parts outside the settings card that show a setting.
        private void ShowSettingsEverywhere()
        {
            hudRoot.EnableInClassList("reduce-motion", settings.ReducedMotion);
            raceManager.ReducedMotion = settings.ReducedMotion; // no finish slow motion with reduced motion (task P6b)
            startSoundText.text = settings.Muted ? "SOUND OFF" : "SOUND ON";
            startSound.EnableInClassList("menu-pill--muted", settings.Muted);
        }
    }
}
