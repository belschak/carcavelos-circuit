using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Kart builder (Luc's idea, 18 Nov 2026): the driver + kart + tyres set, its online code (accepted, refused and
    // edge cases) and the saved choice (a new driver keeps kart and tyres; old saves and bad saved numbers).
    public class KartLoadoutTests
    {
        private const string DriverKey = "CarcavelosCircuit.Driver";
        private const string KartKey = "CarcavelosCircuit.Kart";
        private const string TyresKey = "CarcavelosCircuit.Tyres";
        private readonly Dictionary<string, int> saved = new Dictionary<string, int>();

        [SetUp]
        public void SaveThePlayersChoice()
        {
            saved.Clear();
            foreach (string key in new[] { DriverKey, KartKey, TyresKey })
            {
                if (PlayerPrefs.HasKey(key))
                {
                    saved[key] = PlayerPrefs.GetInt(key);
                }
            }
        }

        [TearDown]
        public void PutThePlayersChoiceBack()
        {
            foreach (string key in new[] { DriverKey, KartKey, TyresKey })
            {
                if (saved.ContainsKey(key)) PlayerPrefs.SetInt(key, saved[key]);
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            DriverChoice.ReloadForTests();
        }

        [Test]
        public void FourPartsPerColumn_EachDriversOwnPartsShareTheirIndex()
        {
            Assert.AreEqual(DriverRoster.Count, KartLoadout.PartCount);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.KartIds.Length);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.KartNames.Length);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.TyreIds.Length);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.TyreNames.Length);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.KartLines.Length);
            Assert.AreEqual(KartLoadout.PartCount, KartLoadout.TyreLines.Length);
            for (int i = 0; i < KartLoadout.PartCount; i++)
            {
                // Luc's prefab KartVisual_<Kart>_<Driver>: index i of the kart list is driver i's own kart
                Assert.AreEqual("KartVisual_" + KartLoadout.KartIds[i] + "_" + DriverRoster.Get(i).Model, DriverRoster.Get(i).KartVisual);
                Assert.AreEqual(DriverRoster.Get(i).KartVisual, KartLoadout.Signature(i).ModelName, "the own set keeps Luc's prefab name");
                Assert.IsTrue(KartLoadout.KartLines[i].EndsWith(".") && KartLoadout.KartLines[i].Length <= 80);
                StringAssert.StartsWith(DriverRoster.Get(i).Name + "'s ", KartLoadout.KartLines[i], "the kart line names its driver");
                Assert.IsTrue(KartLoadout.TyreLines[i].EndsWith(".") && KartLoadout.TyreLines[i].Length <= 80);
            }
            Assert.AreEqual("KartVisual_Orbit_Tiago_Retro", new KartLoadout(1, 2, 3).ModelName);
            Assert.AreEqual("CLÁUDIO  /  ORBIT  /  RETRO", new KartLoadout(1, 2, 3).Title, "the shown name is the teacher's, the model name is Luc's");
        }

        [Test]
        public void EveryOneOfThe64Sets_HasItsOwnCode_ThatReadsBackTheSameSet()
        {
            HashSet<int> codes = new HashSet<int>();
            HashSet<string> names = new HashSet<string>();
            for (int d = 0; d < 4; d++)
            {
                for (int k = 0; k < 4; k++)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        KartLoadout set = new KartLoadout(d, k, t);
                        Assert.IsTrue(set.IsValid);
                        Assert.IsTrue(codes.Add(set.Code), "two sets share code " + set.Code);
                        Assert.IsTrue(names.Add(set.ModelName), "two sets share the model name " + set.ModelName);
                        Assert.IsTrue(KartLoadout.IsValidCode(set.Code));
                        Assert.AreEqual(set, KartLoadout.FromCode(set.Code), set.Title);
                    }
                }
            }
            Assert.AreEqual(64, codes.Count);
        }

        [Test]
        public void OldCodes0To3_AreTheDriversOwnSets()
        {
            for (int d = 0; d < 4; d++)
            {
                Assert.AreEqual(d, KartLoadout.Signature(d).Code, "a driver in their own set sends what older builds sent");
                Assert.AreEqual(KartLoadout.Signature(d), KartLoadout.FromCode(d));
                Assert.IsTrue(KartLoadout.FromCode(d).IsSignature);
            }
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(99)]
        [TestCase(164)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void BrokenCodes_AreRefused_AndReadAsTheRookiesSet(int code)
        {
            Assert.IsFalse(KartLoadout.IsValidCode(code));
            Assert.AreEqual(KartLoadout.Default, KartLoadout.FromCode(code));
        }

        [Test]
        public void EdgeCodes_FirstAndLastCustomCode()
        {
            Assert.AreEqual(new KartLoadout(0, 0, 0), KartLoadout.FromCode(KartLoadout.CustomBase), "100 = Rookie set written the long way");
            Assert.AreEqual(new KartLoadout(3, 3, 3), KartLoadout.FromCode(KartLoadout.CustomBase + 63));
            Assert.IsFalse(KartLoadout.IsValidCode(KartLoadout.CustomBase - 1));
        }

        [Test]
        public void InvalidSets_AreNotValid_AndSendTheRookiesCode()
        {
            KartLoadout bad = new KartLoadout(0, 7, 0);
            Assert.IsFalse(bad.IsValid);
            Assert.AreEqual(DriverRoster.Rookie, bad.Code);
            Assert.AreEqual(KartLoadout.Default.ModelName, bad.ModelName);
            Assert.IsFalse(new KartLoadout(-1, 0, 0).IsValid);
            Assert.IsFalse(new KartLoadout(0, 0, 4).IsValid);
        }

        [Test]
        public void Step_WrapsAroundInAColumn()
        {
            Assert.AreEqual(1, KartLoadout.Step(0, 1));
            Assert.AreEqual(3, KartLoadout.Step(0, -1));
            Assert.AreEqual(0, KartLoadout.Step(3, 1));
            Assert.AreEqual(0, KartLoadout.Step(-5, 1), "a bad index + down = the first part");
            Assert.AreEqual(3, KartLoadout.Step(9, -1));
        }

        [Test]
        public void Choice_FirstVisit_IsTheRookiesOwnSet()
        {
            PlayerPrefs.DeleteKey(DriverKey);
            PlayerPrefs.DeleteKey(KartKey);
            PlayerPrefs.DeleteKey(TyresKey);
            DriverChoice.ReloadForTests();
            Assert.AreEqual(KartLoadout.Default, DriverChoice.Loadout);
            Assert.IsFalse(DriverChoice.HasPicked);
        }

        [Test]
        public void Choice_SaveFromBeforeTheBuilder_StartsWithThatDriversOwnSet()
        {
            PlayerPrefs.SetInt(DriverKey, 2); // Nova, saved by the driver select of 17 Nov (no kart or tyres yet)
            PlayerPrefs.DeleteKey(KartKey);
            PlayerPrefs.DeleteKey(TyresKey);
            DriverChoice.ReloadForTests();
            Assert.AreEqual(KartLoadout.Signature(2), DriverChoice.Loadout, "a returning player sees Nova's Orbit again");
        }

        [Test]
        public void Choice_BadSavedNumbers_FallBackPartByPart()
        {
            PlayerPrefs.SetInt(DriverKey, 1);
            PlayerPrefs.SetInt(KartKey, 42);
            PlayerPrefs.SetInt(TyresKey, 2);
            DriverChoice.ReloadForTests();
            Assert.AreEqual(new KartLoadout(1, 1, 2), DriverChoice.Loadout, "a bad kart = the driver's own kart, the good tyres stay");
            PlayerPrefs.SetInt(DriverKey, -3);
            DriverChoice.ReloadForTests();
            Assert.AreEqual(0, DriverChoice.Loadout.Driver, "a bad driver = the Rookie");
        }

        [Test]
        public void Choice_NewDriverKeepsKartAndTyres_EventsSaySoOnce()
        {
            DriverChoice.SelectLoadout(KartLoadout.Default, false);
            int driverEvents = 0, setEvents = 0;
            System.Action<int> onDriver = d => driverEvents++;
            System.Action<KartLoadout> onSet = l => setEvents++;
            DriverChoice.Changed += onDriver;
            DriverChoice.LoadoutChanged += onSet;
            try
            {
                Assert.IsTrue(DriverChoice.SelectKart(1, false));
                Assert.IsTrue(DriverChoice.SelectTyres(2, false));
                Assert.AreEqual(0, driverEvents, "kart and tyres are no new driver (the bots stay)");
                Assert.AreEqual(2, setEvents);
                Assert.IsTrue(DriverChoice.Select(3, false));
                Assert.AreEqual(new KartLoadout(3, 1, 2), DriverChoice.Loadout, "Amara takes the buggy on balloon tyres");
                Assert.AreEqual(1, driverEvents);
                Assert.AreEqual(3, setEvents);
                Assert.IsTrue(DriverChoice.SelectKart(1, false), "the same kart again is accepted");
                Assert.AreEqual(3, setEvents, "but changes nothing, so no event");
            }
            finally
            {
                DriverChoice.Changed -= onDriver;
                DriverChoice.LoadoutChanged -= onSet;
            }
        }

        [Test]
        public void Choice_RefusesPartsOutsideTheLists()
        {
            DriverChoice.SelectLoadout(KartLoadout.Default, false);
            Assert.IsFalse(DriverChoice.SelectKart(4, false));
            Assert.IsFalse(DriverChoice.SelectKart(-1, false));
            Assert.IsFalse(DriverChoice.SelectTyres(9, false));
            Assert.IsFalse(DriverChoice.SelectLoadout(new KartLoadout(0, 5, 0), false));
            Assert.AreEqual(KartLoadout.Default, DriverChoice.Loadout, "nothing changed");
        }

        [Test]
        public void Choice_Saved_ComesBackNextVisit()
        {
            DriverChoice.SelectLoadout(new KartLoadout(2, 3, 1), true);
            DriverChoice.ReloadForTests();
            Assert.AreEqual(new KartLoadout(2, 3, 1), DriverChoice.Loadout);
            Assert.IsTrue(DriverChoice.HasPicked);
        }

        [Test]
        public void TouchTargets_TilesAndButtons_AtLeast44CssPixelsOnAnIPhoneSe()
        {
            // HUD px are 720 high; an iPhone SE in landscape in Safari (bars shown) is 320 CSS px high: one HUD px =
            // 0.44 CSS px (phone QA 18 Nov measured BACK at 37 px there before this was raised).
            float cssPerHud = 320f / 720f;
            Assert.GreaterOrEqual(KartBuilder.ButtonHeight(true, true) * cssPerHud, 44f);
            Assert.GreaterOrEqual(KartBuilder.ButtonHeight(false, true) * cssPerHud, 44f);
            Assert.GreaterOrEqual(KartBuilder.TouchBackMinWidth * cssPerHud, 44f, "BACK is not a small square");
            Assert.GreaterOrEqual(KartBuilder.TileHeight * cssPerHud, 44f, "every tile of the three columns");
            Assert.AreEqual("NOVA CLASSIC", KartBuilder.PartName(KartBuilder.KartColumn, 0));
            Assert.AreEqual("BALLOON", KartBuilder.PartName(KartBuilder.TyreColumn, 2));
            Assert.AreEqual("CLÁUDIO", KartBuilder.PartName(KartBuilder.DriverColumn, 1));
        }
    }
}
