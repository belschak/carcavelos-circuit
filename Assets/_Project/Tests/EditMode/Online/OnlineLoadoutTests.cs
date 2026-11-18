using CarcavelosCircuit.Online;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Kart builder (18 Nov 2026) online: my whole set (driver + kart + tyres) travels as one number in the room, so every
    // screen builds my kart the same way. Round trip over the in-process transport for all 64 sets, old clients' plain
    // driver numbers, refused numbers, and the ?kart= / ?tyres= test flags.
    public class OnlineLoadoutTests
    {
        private double clock;

        [SetUp]
        public void FreshHub()
        {
            LoopbackHub.Reset();
            clock = 100.0;
            LoopbackHub.Clock = () => clock;
        }

        [TearDown]
        public void DropHub()
        {
            LoopbackHub.Reset();
        }

        [Test]
        public void All64Sets_ArriveOnTheOtherScreen_AsTheSameSet()
        {
            for (int d = 0; d < 4; d++)
            {
                for (int k = 0; k < 4; k++)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        LoopbackHub.Reset();
                        LoopbackHub.Clock = () => clock;
                        KartLoadout mine = new KartLoadout(d, k, t);
                        LoopbackSession me = new LoopbackSession();
                        LoopbackSession other = new LoopbackSession();
                        me.Join(OnlineRooms.SessionName, "Ana", 0, mine.Code);
                        clock += 1.0;
                        other.Join(OnlineRooms.SessionName, "Bo", 1, KartLoadout.Default.Code);
                        Assert.AreEqual(2, other.Players.Count);
                        Assert.AreEqual(mine, other.Players[0].Loadout, mine.Title + " arrives as itself");
                        Assert.AreEqual(KartLoadout.Default, me.Players[1].Loadout);
                    }
                }
            }
        }

        [Test]
        public void OldClientsPlainDriverNumbers_AreThatDriversOwnSet()
        {
            LoopbackSession old = new LoopbackSession();
            LoopbackSession me = new LoopbackSession();
            old.Join(OnlineRooms.SessionName, "Old", 0, 1); // a build from before the builder sends only Tiago (1)
            clock += 1.0;
            me.Join(OnlineRooms.SessionName, "New", 1, new KartLoadout(2, 0, 3).Code);
            Assert.AreEqual(KartLoadout.Signature(1), me.Players[0].Loadout, "Tiago in his own buggy on offroad tyres");
        }

        [TestCase(99)]
        [TestCase(-4)]
        [TestCase(164)]
        [TestCase(1000)]
        public void BrokenNumbers_AreTheRookiesOwnSet(int code)
        {
            Assert.AreEqual(DriverRoster.Rookie, OnlinePlayer.SafeDriver(code));
            OnlinePlayer player = new OnlinePlayer(1, "X", 0, 0.0, code);
            Assert.AreEqual(KartLoadout.Default, player.Loadout);
        }

        [Test]
        public void TestFlags_KartAndTyres_ByNameOrNumber_BadOnesIgnored()
        {
            OnlineUrlFlags flags = OnlineUrlFlags.Parse("https://x.test/index.html?net=local&driver=tiago&kart=Orbit&tyres=3");
            Assert.AreEqual(1, flags.Driver);
            Assert.AreEqual(2, flags.Kart);
            Assert.AreEqual(3, flags.Tyres);
            OnlineUrlFlags bad = OnlineUrlFlags.Parse("https://x.test/index.html?kart=rocket&tyres=9");
            Assert.AreEqual(-1, bad.Kart, "an unknown kart is ignored");
            Assert.AreEqual(-1, bad.Tyres, "a number outside the list is ignored");
            Assert.AreEqual(-1, OnlineUrlFlags.Parse("https://x.test/index.html").Kart, "no flag = no change");
        }
    }
}
