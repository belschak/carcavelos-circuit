using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.RaceFlow
{
    // Task P7: the settings card. Shows GameSettings in the five rows of RaceHud.uxml and changes them with the
    // keys (W/S choose, A/D change, Space on/off) or the mouse (-, +, a diamond of the bar, a click on an on/off row).
    // Plain C#, not a MonoBehaviour: HudMenus creates it and calls HandleKeys() every frame while the card is open.
    // The rules (steps, clamping, unmute) are in GameSettings, so this class only draws and forwards clicks.
    public class SettingsPanel
    {
        // Row names in RaceHud.uxml, in SettingsItem order (Master, Music, Effects, Sound, ReducedMotion).
        private static readonly string[] RowNames = { "set-master", "set-music", "set-effects", "set-sound", "set-motion" };

        // "0 %" ... "100 %" made once, so redrawing a row builds no new strings.
        private static readonly string[] LevelTexts = MakeLevelTexts();

        private readonly GameSettings settings;
        private readonly Action changed; // HudMenus saves and applies the settings
        private readonly VisualElement[] rows = new VisualElement[RowNames.Length];
        private readonly Label[] values = new Label[RowNames.Length];
        private readonly VisualElement[][] cells = new VisualElement[RowNames.Length][];
        private readonly List<VisualElement> clickables = new List<VisualElement>();
        private int selected;

        public SettingsPanel(VisualElement card, GameSettings settings, Action changed)
        {
            this.settings = settings;
            this.changed = changed;
            for (int i = 0; i < RowNames.Length; i++)
            {
                SettingsItem item = (SettingsItem)i; // a copy per row, so each click handler below changes its own row
                VisualElement row = card.Q<VisualElement>(RowNames[i]);
                rows[i] = row;
                values[i] = row.Q<Label>(className: "set-value");
                clickables.Add(row);
                row.RegisterCallback<PointerEnterEvent>(evt => Select(item)); // the mouse moves the highlight too

                if (GameSettings.IsVolume(item))
                {
                    VisualElement down = row.Q<VisualElement>(className: "set-step--down");
                    VisualElement up = row.Q<VisualElement>(className: "set-step--up");
                    down.RegisterCallback<ClickEvent>(evt => Change(item, -1));
                    up.RegisterCallback<ClickEvent>(evt => Change(item, 1));
                    clickables.Add(down);
                    clickables.Add(up);
                    cells[i] = MakeCells(row.Q<VisualElement>(className: "set-bar"), item);
                }
                else
                {
                    row.RegisterCallback<ClickEvent>(evt => Toggle(item));
                }
            }
        }

        // Ten diamonds per volume bar. A click on diamond 7 sets 70 %.
        private VisualElement[] MakeCells(VisualElement bar, SettingsItem item)
        {
            VisualElement[] made = new VisualElement[GameSettings.MaxLevel];
            for (int i = 0; i < made.Length; i++)
            {
                int level = i + 1;
                VisualElement cell = new VisualElement();
                cell.AddToClassList("set-cell");
                VisualElement gem = new VisualElement();
                gem.AddToClassList("set-cell-gem");
                gem.pickingMode = PickingMode.Ignore; // the click goes to the bigger cell around it
                cell.Add(gem);
                cell.RegisterCallback<ClickEvent>(evt => SetLevel(item, level));
                bar.Add(cell);
                clickables.Add(cell);
                made[i] = cell;
            }
            return made;
        }

        // Every frame while the card is open.
        public void HandleKeys()
        {
            int vertical = RaceKeys.MenuVertical();
            if (vertical != 0)
            {
                selected = MenuNavigation.Wrap(selected, vertical, RowNames.Length);
                Refresh();
            }
            SettingsItem item = (SettingsItem)selected;
            int horizontal = RaceKeys.MenuHorizontal();
            if (horizontal != 0)
            {
                Change(item, horizontal);
            }
            if (RaceKeys.MenuTogglePressed() && !GameSettings.IsVolume(item))
            {
                Toggle(item);
            }
        }

        // Clicks only while the card is visible (a hidden card must not catch them).
        public void SetClickable(bool clickable)
        {
            for (int i = 0; i < clickables.Count; i++)
            {
                HudFx.SetClickable(clickables[i], clickable);
            }
        }

        // Draws every row from the settings. Cheap: only classes and cached strings.
        public void Refresh()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                SettingsItem item = (SettingsItem)i;
                rows[i].EnableInClassList("set-row--selected", i == selected);
                if (GameSettings.IsVolume(item))
                {
                    int level = GameSettings.Level(settings.VolumeOf(item));
                    for (int c = 0; c < cells[i].Length; c++)
                    {
                        cells[i][c].EnableInClassList("set-cell--on", c < level);
                    }
                    values[i].text = LevelTexts[level];
                    rows[i].EnableInClassList("set-row--muted", item == SettingsItem.Master && settings.Muted);
                }
                else
                {
                    bool on = settings.IsOn(item);
                    rows[i].EnableInClassList("set-row--on", on);
                    values[i].text = on ? "ON" : "OFF";
                }
            }
        }

        private void Select(SettingsItem item)
        {
            if ((int)item != selected)
            {
                selected = (int)item;
                Refresh();
            }
        }

        private void Change(SettingsItem item, int direction)
        {
            Select(item);
            if (settings.Change(item, direction))
            {
                changed();
            }
            Refresh();
        }

        private void SetLevel(SettingsItem item, int level)
        {
            Select(item);
            if (settings.SetVolumeLevel(item, level))
            {
                changed();
            }
            Refresh();
        }

        private void Toggle(SettingsItem item)
        {
            Select(item);
            if (settings.Toggle(item))
            {
                changed();
            }
            Refresh();
        }

        private static string[] MakeLevelTexts()
        {
            string[] texts = new string[GameSettings.MaxLevel + 1];
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = (i * 10) + " %";
            }
            return texts;
        }
    }
}
