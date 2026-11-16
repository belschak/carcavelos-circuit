using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The transport registry (M2): "photon" is the default when it exists, a name nobody registered gives nothing.
    public class OnlineSessionsTests
    {
        [SetUp]
        [TearDown]
        public void Empty()
        {
            OnlineSessions.Clear();
        }

        [Test]
        public void NothingRegistered_NoDefault_NoSession()
        {
            Assert.IsNull(OnlineSessions.Default);
            Assert.IsNull(OnlineSessions.Create("photon"));
            Assert.IsNull(OnlineSessions.Create(""));
            Assert.IsNull(OnlineSessions.Create(null));
            Assert.IsFalse(OnlineSessions.Has("local"));
        }

        [Test]
        public void Register_ThenCreate()
        {
            int changed = 0;
            OnlineSessions.Changed += () => changed++;
            Assert.IsTrue(OnlineSessions.Register("local", () => new LoopbackSession()));
            Assert.AreEqual(1, changed, "the hook refreshes when a transport arrives");
            Assert.IsTrue(OnlineSessions.Has("local"));
            Assert.IsNull(OnlineSessions.Default, "local is never the default");
            IOnlineSession session = OnlineSessions.Create("local");
            Assert.IsNotNull(session);
            Assert.IsInstanceOf<LoopbackSession>(session);
            session.Dispose();
        }

        [Test]
        public void Photon_IsTheDefault()
        {
            OnlineSessions.Register("local", () => new LoopbackSession());
            OnlineSessions.Register("photon", () => new LoopbackSession()); // stands in for the Fusion adapter
            Assert.AreEqual("photon", OnlineSessions.Default);
        }

        [Test]
        public void BadRegistrations_AreRefused()
        {
            Assert.IsFalse(OnlineSessions.Register("", () => new LoopbackSession()));
            Assert.IsFalse(OnlineSessions.Register(null, () => new LoopbackSession()));
            Assert.IsFalse(OnlineSessions.Register("x", null));
            Assert.IsFalse(OnlineSessions.Has("x"));
        }
    }
}
