using System.Collections;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Driver select (Luc, 17 Nov 2026) in the real game (Main.unity): "any key" opens the driver select instead of
    // starting the race, the player gets the picked driver's kart, the other drivers race as bots,
    // and READY starts the countdown. The player's saved driver and mode are put back afterwards.
    public class DriverSelectPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string DriverKey = "CarcavelosCircuit.Driver";
        private const string ModeKey = "CarcavelosCircuit.RaceMode";

        private RaceManager race;
        private RaceHud hud;
        private BotSquad squad;
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
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((race == null || !race.IsSetUp || squad == null || !squad.HasSpawned || hud == null) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                squad = Object.FindAnyObjectByType<BotSquad>();
                hud = Object.FindAnyObjectByType<RaceHud>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(race != null && race.IsSetUp, "the race was not set up within " + SetUpTimeoutSeconds + " s");
            Assert.IsNotNull(hud, "Main.unity has no RaceHud");
            string message;
            Assert.IsTrue(race.TrySelectMode(RaceMode.RaceVsBots, out message));
            yield return WaitFrames(3);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            DriverChoice.Select(DriverRoster.IsValid(savedDriver) ? savedDriver : DriverRoster.Rookie, false);
            if (hadDriver) PlayerPrefs.SetInt(DriverKey, savedDriver);
            else PlayerPrefs.DeleteKey(DriverKey);
            if (hadMode) PlayerPrefs.SetInt(ModeKey, savedMode);
            else PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnyKey_OpensTheDriverSelect_ReadyStartsTheCountdown()
        {
            Assert.IsFalse(hud.Drivers.IsOpen);
            race.RequestStart(); // what any key or a mode card does on the start screen
            yield return null;
            Assert.IsTrue(hud.Drivers.IsOpen, "any key opens the driver select");
            Assert.AreEqual(RaceState.Ready, race.State, "and does not start the race yet");

            hud.Drivers.Ready();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "READY starts the countdown");
            Assert.IsFalse(hud.Drivers.IsOpen, "the driver select is gone in the countdown");
        }

        // Team test 17 Nov 2026: a click or tap on a mode card is enough (no 1/2/3 needed), and every mode goes
        // through the driver select: card -> driver -> READY -> countdown.
        [UnityTest]
        public IEnumerator ModeCard_OpensTheDriverSelect_InThatMode_ReadyStartsTheCountdown()
        {
            Assert.IsFalse(hud.Drivers.IsOpen);
            hud.ModePicker.Pick(RaceMode.TimeAttack, true); // what the card's ClickEvent does
            yield return null;
            Assert.IsTrue(hud.Drivers.IsOpen, "the card opens the driver select");
            Assert.AreEqual(RaceMode.TimeAttack, race.Mode, "in the mode of the card");
            Assert.AreEqual(RaceState.Ready, race.State, "no countdown before READY");
            yield return WaitFrames(3);
            Assert.IsTrue(hud.Drivers.IsOpen, "and it stays open (the click is used up, nothing starts by itself)");

            hud.Drivers.Pick(DriverRoster.Rookie);
            hud.Drivers.Ready();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "READY starts the countdown");
            Assert.AreEqual(RaceMode.TimeAttack, race.Mode);
        }

        [UnityTest]
        public IEnumerator PickingTiago_GivesThePlayerTheBuggy_AndTheOthersRaceAsBots()
        {
            race.RequestStart();
            yield return null;
            int tiago = DriverRoster.IndexOf("Tiago");
            hud.Drivers.Pick(tiago);
            yield return WaitFrames(4); // old bots go at the frame's end, the new squad comes next frame

            Assert.AreEqual(tiago, DriverChoice.Index);
            Transform slot = race.KartTransform.Find(KartVisualSwap.SlotName);
            Assert.AreEqual("KartVisual_Buggy_Tiago", KartVisualSwap.CurrentModel(slot).name, "the player drives Tiago's buggy");
            Assert.AreEqual(1, CountModels(slot), "only one kart model in the slot");
            Assert.AreEqual(DriverRoster.Get(tiago).Colour, RacerRegistry.FindPlayer().MapColor, "the player takes Tiago's colour");
            Assert.AreEqual(5, squad.Bots.Count, "still five bots");
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                Assert.AreNotEqual("Tiago", squad.Bots[i].DisplayName, "no second Tiago on the grid");
            }
            Assert.IsTrue(HasBot("Rookie") && HasBot("Nova") && HasBot("Amara") && HasBot("Gaivota") && HasBot("Sardinha"));
            Assert.AreEqual("KartVisual_StreetGT_Amara", BotModel("Amara"), "Amara races in her own kart");

            hud.Drivers.Pick(DriverRoster.Rookie);
            yield return WaitFrames(4);
            Assert.AreEqual("KartVisual_Classic_Rookie", KartVisualSwap.CurrentModel(slot).name, "back in the Classic");
            Assert.IsTrue(HasBot("Tiago"), "Tiago is a bot again");
            Assert.AreEqual(PlayerRacer.PlayerColour, RacerRegistry.FindPlayer().MapColor);
        }

        private bool HasBot(string name)
        {
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                if (squad.Bots[i].DisplayName == name) return true;
            }
            return false;
        }

        private string BotModel(string name)
        {
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                if (squad.Bots[i].DisplayName == name)
                {
                    Transform model = KartVisualSwap.CurrentModel(squad.Bots[i].transform.Find(KartVisualSwap.SlotName));
                    return model != null ? model.name : null;
                }
            }
            return null;
        }

        private static int CountModels(Transform slot)
        {
            int count = 0;
            for (int i = 0; i < slot.childCount; i++)
            {
                if (slot.GetChild(i).name.StartsWith(KartVisualSwap.ModelPrefix)) count++;
            }
            return count;
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
