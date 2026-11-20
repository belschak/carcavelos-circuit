using CarcavelosCircuit.Online;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The lobby's own second and third try (scale test, 17 Nov 2026: ERR_CONNECTION_RESET on one machine).
    public class OnlineJoinRetryTests
    {
        [Test]
        public void NetworkTrouble_IsTriedAgain_UpToThreeTriesInAll()
        {
            Assert.IsTrue(OnlineJoinRetry.ShouldRetry("COULD NOT CONNECT (ExceptionOnConnect)", 1));
            Assert.IsTrue(OnlineJoinRetry.ShouldRetry("TIMEOUT", 2));
            Assert.IsTrue(OnlineJoinRetry.ShouldRetry("TIMEOUT", 4));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("TIMEOUT", 5), "the fifth failed try is the last");
            Assert.AreEqual(2f, OnlineJoinRetry.DelayAfter(1));
            Assert.AreEqual(3f, OnlineJoinRetry.DelayAfter(2));
            Assert.AreEqual(8f, OnlineJoinRetry.DelayAfter(4));
            Assert.AreEqual(8f, OnlineJoinRetry.DelayAfter(9), "never longer than 8 s");
            Assert.AreEqual(2f, OnlineJoinRetry.DelayAfter(0));
            Assert.IsTrue(OnlineJoinRetry.ShouldRetry("CONNECTION LOST (Timeout)", 0), "a drop after being in the room");
            Assert.IsTrue(OnlineJoinRetry.ShouldRetry(null, 1));
        }

        [Test]
        public void FinalAnswers_AreNeverRetried()
        {
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("ROOM FULL", 1));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("SERVER FULL (TOO MANY PLAYERS ONLINE), TRY AGAIN IN A MINUTE", 1));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("THE PHOTON APP ID OF THIS BUILD WAS REFUSED", 1));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("NO PHOTON SETTINGS IN THIS BUILD", 1));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("THE ROOM RUNS ANOTHER VERSION OF THE GAME", 1));
            Assert.IsFalse(OnlineJoinRetry.ShouldRetry("CONNECTING WAS CANCELLED", 1));
        }

        [Test]
        public void RetryLine_SaysWhichTry()
        {
            Assert.AreEqual("CONNECTING AGAIN (TRY 2 OF 5)", OnlineJoinRetry.RetryLine(2));
        }
    }
}
