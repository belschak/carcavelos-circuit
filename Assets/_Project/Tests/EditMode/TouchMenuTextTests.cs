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
        public void PauseNote_NamesQForTheMenu_OnlyWhereThereIsAKeyboard()
        {
            StringAssert.Contains("Q  BACK TO THE MENU", HudMenus.PauseNoteText(false));
            StringAssert.Contains("CLOCK IS STOPPED", HudMenus.PauseNoteText(false));
            Assert.AreEqual("THE CLOCK IS STOPPED", HudMenus.PauseNoteText(true), "a phone has no Q key");
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
        public void StartScreenTips_SpeakOfOneOnlineRoom_AndNameTheLightButton()
        {
            // Since the team test on 17 Nov 2026 the class races in ONE room (it was a noon and a sunset room).
            bool oneRoom = false;
            for (int i = 0; i < TitleScreen.SightCount; i++)
            {
                for (int touch = 0; touch < 2; touch++)
                {
                    string tip = TitleScreen.SightText(i, touch == 1);
                    StringAssert.DoesNotContain("rooms", tip, "tip " + i + ": there is only one online room");
                    oneRoom |= tip.Contains("One room");
                    if (touch == 1)
                    {
                        StringAssert.DoesNotContain("Press T", tip, "tip " + i + ": a phone has no T key");
                    }
                }
            }
            Assert.IsTrue(oneRoom, "a tip says the class races in one room");
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
