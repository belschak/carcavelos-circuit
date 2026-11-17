using System.Collections.Generic;
using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // A backed-up connection (scale test, 17 Nov 2026): every other kart's data is old, not just one quiet player's.
    public class ConnectionHealthRulesTests
    {
        [Test]
        public void AllOthersOld_LooksBackedUp()
        {
            Assert.IsTrue(ConnectionHealthRules.LooksBackedUp(new List<double> { 6.0, 7.5, 20.0 }));
        }

        [Test]
        public void MostKartsFresh_MeansMyLineIsFine()
        {
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 30.0, 0.3, 0.4, 0.5 }), "one hidden tab elsewhere");
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 30.0, 25.0, 0.3, 0.4 }), "two of four quiet");
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 0.4, 0.5 }));
        }

        [Test]
        public void OneOddFreshKart_DoesNotHideABackedUpLine()
        {
            List<double> ages = new List<double>();
            for (int i = 0; i < 12; i++)
            {
                ages.Add(16.0);
            }
            ages.Add(0.2); // scale run 2: one kart looked fresh while 12 were 16 s late
            Assert.IsTrue(ConnectionHealthRules.LooksBackedUp(ages));
        }

        [Test]
        public void FewerThanTwoOthers_NeverDecides()
        {
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 60.0 }));
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double>()));
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(null));
        }

        [Test]
        public void Reconnects_OnlyAfterItLastedThreeSeconds()
        {
            double s = 0.0;
            for (int i = 0; i < 29; i++)
            {
                s = ConnectionHealthRules.Sustained(s, true, 0.1);
            }
            Assert.IsFalse(ConnectionHealthRules.ShouldReconnect(s), "2.9 s");
            s = ConnectionHealthRules.Sustained(s, true, 0.11);
            Assert.IsTrue(ConnectionHealthRules.ShouldReconnect(s));
            Assert.AreEqual(0.0, ConnectionHealthRules.Sustained(s, false, 0.1), "one good frame resets it");
        }

        [Test]
        public void OneLongHitch_IsNotABackedUpLine()
        {
            double s = ConnectionHealthRules.Sustained(0.0, true, 15.0); // the laptop froze for 15 s
            Assert.IsFalse(ConnectionHealthRules.ShouldReconnect(s));
            s = ConnectionHealthRules.Sustained(s, false, 0.016);        // fresh poses the next frame
            Assert.AreEqual(0.0, s);
        }
    }
}
