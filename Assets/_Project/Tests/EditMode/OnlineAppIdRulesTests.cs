using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    public class OnlineAppIdRulesTests
    {
        [Test]
        public void Guid_IsValid()
        {
            Assert.IsTrue(OnlineAppIdRules.IsValid("1a2b3c4d-1234-abcd-ABCD-0123456789ab"), "a Photon App ID is a GUID");
            Assert.IsTrue(OnlineAppIdRules.IsValid("  1a2b3c4d-1234-abcd-abcd-0123456789ab \n"), "spaces around a pasted secret are fine");
        }

        [Test]
        public void EmptyOrBroken_IsNotValid()
        {
            Assert.IsTrue(OnlineAppIdRules.IsEmpty(""), "empty = no online race, not an error");
            Assert.IsTrue(OnlineAppIdRules.IsEmpty(null));
            Assert.IsFalse(OnlineAppIdRules.IsValid(""));
            Assert.IsFalse(OnlineAppIdRules.IsValid("1a2b3c4d-1234-abcd-abcd-0123456789a"), "one digit short");
            Assert.IsFalse(OnlineAppIdRules.IsValid("1a2b3c4d12349abcd9abcd90123456789ab"), "no dashes");
            Assert.IsFalse(OnlineAppIdRules.IsValid("https://dashboard.photonengine.com/app/1a2b3c4d-1234-abcd-abcd-0123456789ab"), "a pasted URL");
            Assert.IsFalse(OnlineAppIdRules.IsValid("1a2b3c4d-1234-abcd-abcd-0123456789zz"), "not hex");
        }
    }
}
