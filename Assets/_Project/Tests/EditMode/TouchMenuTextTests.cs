using System.IO;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Feedback round 17.11 (Role 4): on a phone the menus talk about taps and touch buttons, not keys, and the menu
    // buttons are thumb-sized. The look itself is checked in a real Web build (docs/verification.md "Not covered").
    public class TouchMenuTextTests
    {
        [Test]
        public void PauseHint_SaysTapOnTouch_AndKeysOnAComputer()
        {
            Assert.AreEqual("TAP TO CONTINUE", HudMenus.PauseHintText(true));
            StringAssert.Contains("PRESS P", HudMenus.PauseHintText(false));
        }

        [Test]
        public void StartScreenTips_NameTheTouchButtons_OnTouch()
        {
            for (int i = 0; i < TitleScreen.SightCount; i++)
            {
                string touch = TitleScreen.SightText(i, true);
                StringAssert.DoesNotContain("SPACE", touch, "tip " + i + " on a phone");
                StringAssert.DoesNotContain("SHIFT", touch, "tip " + i + " on a phone");
            }
            bool keysOnComputer = false;
            for (int i = 0; i < TitleScreen.SightCount; i++)
            {
                keysOnComputer |= TitleScreen.SightText(i, false).Contains("SPACE");
            }
            Assert.IsTrue(keysOnComputer, "the computer tips still name the keys");
        }

        [Test]
        public void Hud_HidesKeycapsAndGrowsMenuPills_OnTouch()
        {
            string uss = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "RaceFlow", "UI", "RaceHud.uss"));
            StringAssert.Contains(".hud--touch .ts-controls", uss);
            StringAssert.Contains(".hud--touch .menu-pill .keycap", uss);
            StringAssert.Contains(".hud--touch .menu-keys", uss);
            StringAssert.Contains(".hud--touch .menu-pill {", uss);
            string uxml = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "RaceFlow", "UI", "RaceHud.uxml"));
            StringAssert.Contains("name=\"pause-hint\"", uxml);
        }
    }
}
