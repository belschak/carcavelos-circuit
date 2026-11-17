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
        public void OneFreshKart_MeansMyLineIsFine()
        {
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 30.0, 0.3, 25.0 }), "one hidden tab elsewhere");
            Assert.IsFalse(ConnectionHealthRules.LooksBackedUp(new List<double> { 0.4, 0.5 }));
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
    }
}
