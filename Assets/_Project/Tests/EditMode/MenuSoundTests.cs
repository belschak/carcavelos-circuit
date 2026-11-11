using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (audit 10): which menu sound a settings row asks for.
    public class MenuSoundTests
    {
        [TestCase(SettingsItem.Master)]
        [TestCase(SettingsItem.Music)]
        [TestCase(SettingsItem.Effects)]
        public void VolumeRows_SoundLikeASliderStep(SettingsItem item)
        {
            Assert.AreEqual(MenuSound.SliderStep, HudMenus.SoundFor(item), "the step click is the preview of the new level");
        }

        [TestCase(SettingsItem.Sound)]
        [TestCase(SettingsItem.ReducedMotion)]
        public void OnOffRows_SoundLikeASwitch(SettingsItem item)
        {
            Assert.AreEqual(MenuSound.Toggle, HudMenus.SoundFor(item));
        }
    }
}
