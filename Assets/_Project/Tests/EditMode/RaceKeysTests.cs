using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: which keys may start the countdown on the "Press any key" screen.
    public class RaceKeysTests
    {
        [TestCase(Key.A)]
        [TestCase(Key.W)]
        [TestCase(Key.Space)]
        [TestCase(Key.Enter)]
        [TestCase(Key.UpArrow)]
        [TestCase(Key.Digit1)]
        public void NormalKeys_StartTheRace(Key key)
        {
            Assert.IsTrue(RaceKeys.CountsAsStartKey(key));
        }

        [TestCase(Key.LeftAlt)]   // Alt-Tab
        [TestCase(Key.RightAlt)]
        [TestCase(Key.LeftCtrl)]
        [TestCase(Key.RightCtrl)]
        [TestCase(Key.LeftShift)]
        [TestCase(Key.LeftMeta)]  // Windows / Cmd key
        [TestCase(Key.Tab)]
        [TestCase(Key.Escape)]    // leaves fullscreen in the browser
        [TestCase(Key.F1)]
        [TestCase(Key.F11)]       // fullscreen in the browser
        [TestCase(Key.F12)]       // browser developer tools
        [TestCase(Key.F24)]
        [TestCase(Key.PrintScreen)]
        public void ModifierFunctionAndSystemKeys_DoNotStartTheRace(Key key)
        {
            Assert.IsFalse(RaceKeys.CountsAsStartKey(key));
        }

        [TestCase(Key.O)] // settings
        [TestCase(Key.C)] // credits
        [TestCase(Key.M)] // mute
        [TestCase(Key.P)] // pause: RaceManager reads P by itself, so it must not also start the race
        public void MenuKeys_DoNotStartTheRaceOrEndThePause(Key key)
        {
            Assert.IsFalse(RaceKeys.CountsAsStartKey(key));
        }
    }
}
