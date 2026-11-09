using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: the HUD only rebuilds a label's text when the number on screen changes.
    public class ShownNumberTests
    {
        [Test]
        public void FirstValue_AlwaysCountsAsChanged()
        {
            ShownNumber shown = new ShownNumber();
            Assert.IsTrue(shown.Changed(0));
        }

        [Test]
        public void SameValue_IsNotAChange()
        {
            ShownNumber shown = new ShownNumber();
            shown.Changed(83456);
            Assert.IsFalse(shown.Changed(83456));
            Assert.IsFalse(shown.Changed(83456));
        }

        [Test]
        public void NewValue_IsAChange_AndIsRemembered()
        {
            ShownNumber shown = new ShownNumber();
            shown.Changed(120);
            Assert.IsTrue(shown.Changed(121));
            Assert.IsFalse(shown.Changed(121));
        }

        [Test]
        public void Forget_MakesTheNextValueAChange()
        {
            ShownNumber shown = new ShownNumber();
            shown.Changed(5);
            shown.Forget();
            Assert.IsTrue(shown.Changed(5));
        }
    }
}
