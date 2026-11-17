using System.Collections;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Team test 17 Nov 2026, the start screen with real key presses in the real game (Main.unity). A test keyboard
    // (Input System) sends the keys, so RaceKeys reads them exactly like a player's:
    // - 1 / 2 / 3 open the driver select in that mode, Space opens it in the highlighted mode,
    // - Backspace goes back to the modes and stays there (it is also a start key, so it must not reopen it),
    // - Escape closes the settings and the credits; over the driver select it only closes the card.
    // The player's saved mode and driver are put back afterwards.
    public class MenuKeysPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string DriverKey = "CarcavelosCircuit.Driver";
        private const string ModeKey = "CarcavelosCircuit.RaceMode";

        private RaceManager race;
        private RaceHud hud;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior backgroundBefore;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehaviourBefore;
        private bool hadDriver;
        private int savedDriver;
        private bool hadMode;
        private int savedMode;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            hadDriver = PlayerPrefs.HasKey(DriverKey);
            savedDriver = PlayerPrefs.GetInt(DriverKey, DriverRoster.Rookie);
            hadMode = PlayerPrefs.HasKey(ModeKey);
            savedMode = PlayerPrefs.GetInt(ModeKey, 0);
            DriverChoice.Select(DriverRoster.Rookie, false);

            // The test runner's window may not have focus: let the test keyboard's keys through anyway.
            backgroundBefore = InputSystem.settings.backgroundBehavior;
            editorBehaviourBefore = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("MenuKeysTestKeyboard");
            keyboard.MakeCurrent();

            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((race == null || !race.IsSetUp || hud == null || hud.Menus == null) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                hud = Object.FindAnyObjectByType<RaceHud>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(race != null && race.IsSetUp, "the race was not set up within " + SetUpTimeoutSeconds + " s");
            Assert.IsNotNull(hud, "Main.unity has no RaceHud");
            race.PauseOnFocusLoss = false; // a test window without focus must not pause the start screen
            string message;
            Assert.IsTrue(race.TrySelectMode(RaceMode.RaceVsBots, out message));
            keyboard.MakeCurrent();
            yield return WaitFrames(3);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }
            InputSystem.settings.backgroundBehavior = backgroundBefore;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorBehaviourBefore;
            DriverChoice.Select(DriverRoster.IsValid(savedDriver) ? savedDriver : DriverRoster.Rookie, false);
            if (hadDriver) PlayerPrefs.SetInt(DriverKey, savedDriver);
            else PlayerPrefs.DeleteKey(DriverKey);
            if (hadMode) PlayerPrefs.SetInt(ModeKey, savedMode);
            else PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModeKey_OpensTheDriverSelectInThatMode()
        {
            yield return Press(Key.Digit2);
            Assert.AreEqual(RaceMode.TimeAttack, race.Mode, "2 = TIME ATTACK");
            Assert.IsTrue(hud.Drivers.IsOpen, "the key opens the driver select");
            Assert.AreEqual(RaceState.Ready, race.State, "no countdown before READY");

            yield return Press(Key.Enter);
            Assert.AreEqual(RaceState.Countdown, race.State, "Enter = READY starts the countdown");
        }

        [UnityTest]
        public IEnumerator Space_OpensTheDriverSelect_BackspaceGoesBack_AndStaysThere()
        {
            yield return Press(Key.Space);
            Assert.IsTrue(hud.Drivers.IsOpen, "any key opens the driver select in the highlighted mode");

            yield return Press(Key.Backspace);
            Assert.IsFalse(hud.Drivers.IsOpen, "Backspace goes back to the modes");
            yield return WaitFrames(3);
            Assert.IsFalse(hud.Drivers.IsOpen, "and the same key does not open it again");
            Assert.AreEqual(RaceState.Ready, race.State);

            yield return Press(Key.Space);
            Assert.IsTrue(hud.Drivers.IsOpen);
            yield return Press(Key.Escape);
            Assert.IsFalse(hud.Drivers.IsOpen, "Escape goes back too");
        }

        [UnityTest]
        public IEnumerator Escape_ClosesTheSettingsAndTheCredits()
        {
            yield return Press(Key.O);
            Assert.IsTrue(hud.Menus.IsMenuOpen, "O opens the settings");
            yield return Press(Key.Escape);
            Assert.IsFalse(hud.Menus.IsMenuOpen, "Escape closes the settings");
            Assert.IsFalse(hud.Drivers.IsOpen, "and starts nothing");

            yield return Press(Key.C);
            Assert.IsTrue(hud.Menus.IsMenuOpen, "C opens the credits");
            yield return Press(Key.Escape);
            Assert.IsFalse(hud.Menus.IsMenuOpen, "Escape closes the credits");

            yield return Press(Key.O);
            yield return Press(Key.O);
            Assert.IsFalse(hud.Menus.IsMenuOpen, "O still closes the settings as before");
            Assert.AreEqual(RaceState.Ready, race.State);
        }

        [UnityTest]
        public IEnumerator Escape_OverTheDriverSelect_OnlyClosesTheSettings()
        {
            yield return Press(Key.Space);
            Assert.IsTrue(hud.Drivers.IsOpen);
            yield return Press(Key.O);
            Assert.IsTrue(hud.Menus.IsMenuOpen, "the settings open over the driver select");
            yield return Press(Key.Escape);
            Assert.IsFalse(hud.Menus.IsMenuOpen, "Escape closes the settings");
            Assert.IsTrue(hud.Drivers.IsOpen, "the same Escape does not also leave the driver select");
        }

        // Down in one frame (RaceKeys sees wasPressedThisFrame in that frame's Updates), up in the next.
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }
    }
}
