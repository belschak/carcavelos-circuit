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
    // Driver select (Luc, 17 Nov 2026), kart builder since 18 Nov 2026, in the real game (Main.unity): "any key" opens
    // the builder instead of starting the race, the player gets the picked driver + kart + tyres, the other drivers race
    // as bots, and READY starts the countdown. The player's saved set and mode are put back afterwards.
    public class DriverSelectPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string DriverKey = "CarcavelosCircuit.Driver";
        private const string ModeKey = "CarcavelosCircuit.RaceMode";
        private const string KartKey = "CarcavelosCircuit.Kart";
        private const string TyresKey = "CarcavelosCircuit.Tyres";

        private RaceManager race;
        private RaceHud hud;
        private BotSquad squad;
        private bool hadDriver;
        private int savedDriver;
        private bool hadMode;
        private int savedMode;
        private bool hadKart;
        private int savedKart;
        private bool hadTyres;
        private int savedTyres;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            hadDriver = PlayerPrefs.HasKey(DriverKey);
            savedDriver = PlayerPrefs.GetInt(DriverKey, DriverRoster.Rookie);
            hadMode = PlayerPrefs.HasKey(ModeKey);
            savedMode = PlayerPrefs.GetInt(ModeKey, 0);
            hadKart = PlayerPrefs.HasKey(KartKey);
            savedKart = PlayerPrefs.GetInt(KartKey, 0);
            hadTyres = PlayerPrefs.HasKey(TyresKey);
            savedTyres = PlayerPrefs.GetInt(TyresKey, 0);
            DriverChoice.SelectLoadout(KartLoadout.Default, false);
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
            if (hadDriver) PlayerPrefs.SetInt(DriverKey, savedDriver);
            else PlayerPrefs.DeleteKey(DriverKey);
            if (hadMode) PlayerPrefs.SetInt(ModeKey, savedMode);
            else PlayerPrefs.DeleteKey(ModeKey);
            if (hadKart) PlayerPrefs.SetInt(KartKey, savedKart);
            else PlayerPrefs.DeleteKey(KartKey);
            if (hadTyres) PlayerPrefs.SetInt(TyresKey, savedTyres);
            else PlayerPrefs.DeleteKey(TyresKey);
            PlayerPrefs.Save();
            DriverChoice.ReloadForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnyKey_OpensTheDriverSelect_ReadyStartsTheCountdown()
        {
            Assert.IsFalse(hud.Builder.IsOpen);
            race.RequestStart(); // what any key or a mode card does on the start screen
            yield return null;
            Assert.IsTrue(hud.Builder.IsOpen, "any key opens the driver select");
            Assert.AreEqual(RaceState.Ready, race.State, "and does not start the race yet");

            hud.Builder.Ready();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "READY starts the countdown");
            Assert.IsFalse(hud.Builder.IsOpen, "the driver select is gone in the countdown");
        }

        // Team test 17 Nov 2026: a click or tap on a mode card is enough (no 1/2/3 needed), and every mode goes
        // through the driver select: card -> driver -> READY -> countdown.
        [UnityTest]
        public IEnumerator ModeCard_OpensTheDriverSelect_InThatMode_ReadyStartsTheCountdown()
        {
            Assert.IsFalse(hud.Builder.IsOpen);
            hud.ModePicker.Pick(RaceMode.TimeAttack, true); // what the card's ClickEvent does
            yield return null;
            Assert.IsTrue(hud.Builder.IsOpen, "the card opens the driver select");
            Assert.AreEqual(RaceMode.TimeAttack, race.Mode, "in the mode of the card");
            Assert.AreEqual(RaceState.Ready, race.State, "no countdown before READY");
            yield return WaitFrames(3);
            Assert.IsTrue(hud.Builder.IsOpen, "and it stays open (the click is used up, nothing starts by itself)");

            hud.Builder.PickDriver(DriverRoster.Rookie);
            hud.Builder.Ready();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "READY starts the countdown");
            Assert.AreEqual(RaceMode.TimeAttack, race.Mode);
        }

        [UnityTest]
        public IEnumerator PickingClaudio_GivesThePlayerTheBuggy_AndTheOthersRaceAsBots()
        {
            race.RequestStart();
            yield return null;
            int tiago = DriverRoster.IndexOf("Cláudio"); // Cláudio drives Luc's Tiago model (the beach buggy)
            Assert.AreEqual(1, tiago);
            hud.Builder.PickDriver(tiago);
            Assert.AreEqual(new KartLoadout(tiago, 0, 0), DriverChoice.Loadout, "a new driver keeps the kart and tyres");
            hud.Builder.Pick(KartBuilder.KartColumn, tiago);
            hud.Builder.Pick(KartBuilder.TyreColumn, tiago);
            yield return WaitFrames(4); // old bots go at the frame's end, the new squad comes next frame

            Assert.AreEqual(tiago, DriverChoice.Index);
            Transform slot = race.KartTransform.Find(KartVisualSwap.SlotName);
            Assert.AreEqual("KartVisual_Buggy_Tiago", KartVisualSwap.CurrentModel(slot).name, "the player drives Cláudio's buggy");
            Assert.AreEqual(1, CountModels(slot), "only one kart model in the slot");
            Assert.AreEqual(DriverRoster.Get(tiago).Colour, RacerRegistry.FindPlayer().MapColor, "the player takes Cláudio's colour");
            Assert.AreEqual(5, squad.Bots.Count, "still five bots");
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                CollectionAssert.Contains(TributeRules.GivenNames, squad.Bots[i].DisplayName, "every bot is a teacher's first name");
                Assert.AreNotEqual("KartVisual_Buggy_Tiago", BotModel(squad.Bots[i].DisplayName), "no second buggy: the player has it");
            }
            Assert.IsTrue(HasBot("Ricardo") && HasBot("Luís") && HasBot("Paulo") && HasBot("Cláudio") && HasBot("João"));
            Assert.AreEqual("KartVisual_StreetGT_Amara", BotModel("Paulo"), "Paulo races in the Street GT");
            Assert.AreEqual("KartVisual_Classic_Rookie", BotModel("Cláudio"), "Cláudio lent the player the buggy and drives a Classic");

            hud.Builder.PickDriver(DriverRoster.Rookie);
            yield return WaitFrames(4);
            Assert.AreEqual("KartVisual_Buggy_Rookie_Offroad", KartVisualSwap.CurrentModel(slot).name,
                "Ricardo takes over the buggy and tyres (a new driver keeps the kart)");
            Assert.AreEqual("KartVisual_Buggy_Tiago", BotModel("Cláudio"), "Cláudio is back in the buggy");
            Assert.AreEqual("KartVisual_Classic_Rookie", BotModel("Ricardo"), "and Ricardo's bot drives a repainted Classic");
            Assert.AreEqual(PlayerRacer.PlayerColour, RacerRegistry.FindPlayer().MapColor);
        }

        // Kart builder: a new kart or tyres rebuild only the player's kart (the bots stay as they are), the turntable shows
        // the same set, and the set is what the online room would get.
        [UnityTest]
        public IEnumerator PickingKartAndTyres_RebuildsOnlyThePlayersKart()
        {
            race.RequestStart();
            yield return WaitFrames(4);
            Assert.IsTrue(hud.Builder.IsOpen);
            Assert.AreEqual(5, squad.Bots.Count);
            BotDriver firstBot = squad.Bots[0];

            int tiago = DriverRoster.IndexOf("Cláudio");
            hud.Builder.PickDriver(tiago);
            yield return WaitFrames(4);
            firstBot = squad.Bots[0]; // a new driver lines the bots up again (Cláudio moves to a Classic)
            hud.Builder.Pick(KartBuilder.KartColumn, 2);  // Orbit
            hud.Builder.Pick(KartBuilder.TyreColumn, 3);  // Retro
            yield return WaitFrames(4);

            KartLoadout set = new KartLoadout(tiago, 2, 3);
            Assert.AreEqual(set, DriverChoice.Loadout);
            Transform slot = race.KartTransform.Find(KartVisualSwap.SlotName);
            Assert.AreEqual("KartVisual_Orbit_Tiago_Retro", KartVisualSwap.CurrentModel(slot).name, "Cláudio in the Orbit on Retro tyres");
            Assert.AreEqual(1, CountModels(slot), "only one kart model in the slot");
            Assert.AreSame(firstBot, squad.Bots[0], "kart and tyres leave the bots alone");
            Assert.AreEqual(set.Code, KartLoadout.FromCode(set.Code).Code, "the online code carries the whole set");

            Assert.IsNotNull(hud.Builder.Stage, "the builder made its turntable studio");
            Assert.AreEqual(1, hud.Builder.Stage.KartCount, "one kart on the turntable");
            Assert.IsNotNull(hud.Builder.Stage.Picture, "the turntable draws into a picture");
            Assert.IsTrue(hud.Builder.Stage.Rendering, "and draws while the builder is open");

            hud.Builder.Ready();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "READY starts the countdown");
            Assert.IsFalse(hud.Builder.Stage.Rendering, "the turntable rests during the race");
            Assert.AreEqual("KartVisual_Orbit_Tiago_Retro", KartVisualSwap.CurrentModel(slot).name, "the race keeps the set");
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
