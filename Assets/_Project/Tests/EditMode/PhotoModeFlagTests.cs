using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // G-audit (19 Nov): photo mode (no menus, no HUD) only for photo=1 as its own pair in the page address.
    public class PhotoModeFlagTests
    {
        [TestCase("https://nova-kart.github.io/?photo=1")]
        [TestCase("https://nova-kart.github.io/?dpr=2&photo=1")]
        [TestCase("https://nova-kart.github.io/?photo=1#top")]
        public void PhotoOne_IsPhotoMode(string url)
        {
            Assert.IsTrue(TitleScreen.IsPhotoMode(url));
        }

        [TestCase("https://nova-kart.github.io/?photo=10")]
        [TestCase("https://nova-kart.github.io/?nophoto=1")]
        [TestCase("https://nova-kart.github.io/?photo=0")]
        [TestCase("https://nova-kart.github.io/#photo=1")]    // after the # is not the query
        [TestCase("https://nova-kart.github.io/photo=1")]     // no query at all
        [TestCase("https://nova-kart.github.io/")]
        [TestCase("")]
        [TestCase(null)]
        public void AnythingElse_IsNotPhotoMode(string url)
        {
            Assert.IsFalse(TitleScreen.IsPhotoMode(url));
        }
    }
}
