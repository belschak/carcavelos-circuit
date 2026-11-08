using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CarcavelosCircuit.RaceFlow
{
    // All race-flow keys in one place (new Input System). Controls are listed in docs/interfaces.md.
    // Keyboard.current is null when no keyboard exists, so every check guards against that.
    public static class RaceKeys
    {
        // "Press any key to start": any normal key or a mouse click. The click also unlocks sound in the browser.
        // Not every key: Alt-Tab, Ctrl, F11 (fullscreen) or Escape should not start the countdown by accident.
        public static bool AnyKeyOrClickPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
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

        // False for modifier keys (Shift, Ctrl, Alt, Windows/Cmd), function keys (F1 to F24) and system keys
        // (Escape, Tab, Caps Lock, Print Screen ...). Public so an Edit Mode test can check the list.
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
                    return false;
                default:
                    return true;
            }
        }

        // Enter = retry (full reset). Not Escape: in a browser Escape leaves fullscreen.
        public static bool RetryPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        // R = recover (back to the last valid checkpoint, timer keeps running).
        public static bool RecoverPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
        }
    }
}
