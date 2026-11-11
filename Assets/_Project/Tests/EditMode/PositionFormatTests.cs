using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R9): English ordinals for the position readout.
    public class PositionFormatTests
    {
        [TestCase(1, "1st")]
        [TestCase(2, "2nd")]
        [TestCase(3, "3rd")]
        [TestCase(4, "4th")]
        [TestCase(10, "10th")]
        [TestCase(11, "11th")]
        [TestCase(12, "12th")]
        [TestCase(13, "13th")]
        [TestCase(21, "21st")]
        [TestCase(22, "22nd")]
        [TestCase(23, "23rd")]
        [TestCase(101, "101st")]
        [TestCase(111, "111th")]
        [TestCase(112, "112th")]
        public void Ordinal_IsEnglish(int position, string expected)
        {
            Assert.AreEqual(expected, PositionFormat.Ordinal(position));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NoPosition_IsEmpty(int position)
        {
            Assert.AreEqual("", PositionFormat.Ordinal(position));
            Assert.AreEqual("", PositionFormat.Suffix(position));
        }

        [Test]
        public void OfTotal_ShowsTheNumberOfRacers()
        {
            Assert.AreEqual("/ 6", PositionFormat.OfTotal(6));
            Assert.AreEqual("", PositionFormat.OfTotal(0));
        }
    }
}
