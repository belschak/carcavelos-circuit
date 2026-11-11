using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CarcavelosCircuit.RaceFlow
{
    // All race-flow keys in one place (new Input System). Controls are listed in docs/interfaces.md.
    // Keyboard.current is null when no keyboard exists, so every check guards against that.
    public static class RaceKeys
    {
        // True while the player types a name on the results card (task R10, set by LeaderboardHud). Then every
        // letter is text, not a game key: M must not mute, O not open the settings, Enter not retry.
        public static bool TextEntryActive { get; set; }

        // The keyboard for game keys, or null while a name is typed (every check below then says "not pressed").
        private static Keyboard GameKeyboard()
        {
            return TextEntryActive ? null : Keyboard.current;
        }

        // While typing a name: Enter saves it (read here on purpose without GameKeyboard, which is off then).
        public static bool NameSubmitPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        // Tab jumps into the name field on the results card, so the leaderboard works without a mouse.
        public static bool NameFieldKeyPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.tabKey.wasPressedThisFrame;
        }

        // "Press any key to start": any normal key or a mouse click. The click also unlocks sound in the browser.
        // Not every key: Alt-Tab, Ctrl, F11 (fullscreen) or Escape should not start the countdown by accident.
        public static bool AnyKeyOrClickPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = GameKeyboard();
            if (keyboard == null || !keyboard.anyKey.wasPressedThisFrame)
            {
                return false; // quick exit: nothing was pressed, no need to look at every key
            }
            // A for loop over the keys (not foreach), so it creates no garbage.
            for (int i = 0; i < keyboard.allKeys.Count; i++)
            {
                KeyControl key = keyboard.allKeys[i];
                if (key.wasPressedThisFrame && CountsAsStartKey(key.keyCode))
                {
                    return true;
                }
            }
            return false;
        }

        // False for modifier keys (Shift, Ctrl, Alt, Windows/Cmd), function keys (F1 to F24), system keys
        // (Escape, Tab, Caps Lock, Print Screen ...) and the menu keys O, C, M, P (they open settings, credits,
        // mute or pause, and must not also start the race). Public so an Edit Mode test can check the list.
        public static bool CountsAsStartKey(Key key)
        {
            if ((key >= Key.F1 && key <= Key.F12) || (key >= Key.F13 && key <= Key.F24))
            {
                return false;
            }
            switch (key)
            {
                case Key.None:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftMeta:
                case Key.RightMeta:
                case Key.ContextMenu:
                case Key.Escape:
                case Key.Tab:
                case Key.CapsLock:
                case Key.NumLock:
                case Key.ScrollLock:
                case Key.PrintScreen:
                case Key.Pause:
                case Key.O: // settings
                case Key.C: // credits
                case Key.M: // mute
                case Key.P: // pause (RaceManager reads it on its own, see PausePressed)
                    return false;
                default:
                    return true;
            }
        }

        // Enter = retry (full reset). Not Escape: in a browser Escape leaves fullscreen.
        public static bool RetryPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        // R = recover (back to the last valid checkpoint, timer keeps running).
        public static bool RecoverPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
        }

        // E = use the item in the HUD slot (task R7). The phone's touch "use item" button sends the same E key.
        public static bool UseItemPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        // ---- Menu keys (task P7). Never Escape: in a browser it leaves fullscreen. ----

        // O = open or close the settings (start and results screen).
        public static bool SettingsPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.oKey.wasPressedThisFrame;
        }

        // C = open or close the credits (start screen).
        public static bool CreditsPressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.cKey.wasPressedThisFrame;
        }

        // P = pause or continue, during the countdown and the race (audit 9). Never Escape: it leaves fullscreen.
        public static bool PausePressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.pKey.wasPressedThisFrame;
        }

        // M = sound on/off, at any time (also during the race and the pause).
        public static bool MutePressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.mKey.wasPressedThisFrame;
        }

        // Inside a menu: W/S or up/down choose a row, A/D or left/right change it (or turn a page).
        // -1, 0 or +1, so the menu code needs no if per key.
        public static int MenuVertical()
        {
            Keyboard keyboard = GameKeyboard();
            if (keyboard == null) return 0;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) return -1;
            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) return 1;
            return 0;
        }

        public static int MenuHorizontal()
        {
            Keyboard keyboard = GameKeyboard();
            if (keyboard == null) return 0;
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) return -1;
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) return 1;
            return 0;
        }

        // Where the mouse is, in screen pixels (0,0 = bottom left). False without a mouse.
        // The HUD uses it to see if the mouse points at a menu button before the click arrives.
        public static bool TryGetMousePosition(out Vector2 position)
        {
            Mouse mouse = Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null;
        }

        // Space flips an on/off row (Enter stays retry, so it is not used in menus).
        public static bool MenuTogglePressed()
        {
            Keyboard keyboard = GameKeyboard();
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }
    }
}
