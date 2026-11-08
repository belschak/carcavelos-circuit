using UnityEngine.InputSystem;

namespace CarcavelosCircuit.RaceFlow
{
    // All race-flow keys in one place (new Input System). Controls are listed in docs/interfaces.md.
    // Keyboard.current is null when no keyboard exists, so every check guards against that.
    public static class RaceKeys
    {
        // "Press any key to start": any key or a mouse click. The click also unlocks sound in the browser.
        public static bool AnyKeyOrClickPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool key = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            return key || click;
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
