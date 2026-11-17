using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Which other karts are drawn online (review F6 + F7): never before the first pose, never a late joiner mid-race.
    public class OnlineProxyRulesTests
    {
        [Test]
        public void NoPoseYet_IsNeverShown()
        {
            Assert.IsFalse(OnlineProxyRules.Shows(false, RaceControl.Lobby, 5), "would stand at the world origin");
            Assert.IsFalse(OnlineProxyRules.Shows(false, new RaceControl(1, OnlinePhase.Racing, 10.0, new[] { 5 }), 5));
        }

        [Test]
        public void InTheLobby_EveryoneIsShown()
        {
            Assert.IsTrue(OnlineProxyRules.Shows(true, RaceControl.Lobby, 5));
            Assert.IsTrue(OnlineProxyRules.Shows(true, null, 5));
        }

        [Test]
        public void DuringARace_OnlyTheRosterIsShown()
        {
            RaceControl race = new RaceControl(2, OnlinePhase.Racing, 10.0, new[] { 1, 5 });
            Assert.IsTrue(OnlineProxyRules.Shows(true, race, 5));
            Assert.IsFalse(OnlineProxyRules.Shows(true, race, 9), "a late joiner watches, it is no racer");
        }
    }
}
