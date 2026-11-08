using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task P7): moving through settings rows and credit pages wraps around.
    public class MenuNavigationTests
    {
        [Test]
        public void Down_MovesOne_AndWrapsToTheTop()
        {
            Assert.AreEqual(1, MenuNavigation.Wrap(0, 1, 5));
            Assert.AreEqual(0, MenuNavigation.Wrap(4, 1, 5));
        }

        [Test]
        public void Up_FromTheTop_GoesToTheLastRow()
        {
            Assert.AreEqual(4, MenuNavigation.Wrap(0, -1, 5));
        }

        [Test]
        public void TwoPages_Flip()
        {
            Assert.AreEqual(1, MenuNavigation.Wrap(0, 1, 2));
            Assert.AreEqual(0, MenuNavigation.Wrap(1, 1, 2));
            Assert.AreEqual(1, MenuNavigation.Wrap(0, -1, 2));
        }

        [Test]
        public void EmptyList_IsSafe()
        {
            Assert.AreEqual(0, MenuNavigation.Wrap(3, 1, 0));
        }
    }
}
