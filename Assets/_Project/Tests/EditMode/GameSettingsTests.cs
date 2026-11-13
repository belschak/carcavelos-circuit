using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task P7): settings defaults, 10 % steps, clamping, mute, and the save text round trip.
    public class GameSettingsTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Defaults_FullVolume_SoundOn_MotionOn()
        {
            GameSettings settings = new GameSettings();
            Assert.AreEqual(1f, settings.MasterVolume, Tolerance);
            Assert.AreEqual(1f, settings.MusicVolume, Tolerance);
            Assert.AreEqual(1f, settings.EffectsVolume, Tolerance);
            Assert.IsFalse(settings.Muted);
            Assert.IsFalse(settings.ReducedMotion);
            Assert.AreEqual(1f, settings.ListenerVolume, Tolerance);
        }

        [Test]
        public void Change_StepsTenPercent_AndStopsAtZeroAndFull()
        {
            GameSettings settings = new GameSettings();
            Assert.IsFalse(settings.Change(SettingsItem.Music, 1), "already at 100 %, nothing changes");
            Assert.IsTrue(settings.Change(SettingsItem.Music, -1));
            Assert.AreEqual(0.9f, settings.MusicVolume, Tolerance);
            for (int i = 0; i < 20; i++)
            {
                settings.Change(SettingsItem.Music, -1);
            }
            Assert.AreEqual(0f, settings.MusicVolume, Tolerance);
            Assert.IsFalse(settings.Change(SettingsItem.Music, -1), "already silent");
        }

        [Test]
        public void SetVolumeLevel_ClampsToZeroToTen()
        {
            GameSettings settings = new GameSettings();
            settings.SetVolumeLevel(SettingsItem.Effects, 3);
            Assert.AreEqual(0.3f, settings.EffectsVolume, Tolerance);
            settings.SetVolumeLevel(SettingsItem.Effects, 15);
            Assert.AreEqual(1f, settings.EffectsVolume, Tolerance);
            settings.SetVolumeLevel(SettingsItem.Effects, -4);
            Assert.AreEqual(0f, settings.EffectsVolume, Tolerance);
        }

        [Test]
        public void Mute_SilencesTheListener_AndKeepsTheMasterVolume()
        {
            GameSettings settings = new GameSettings();
            settings.SetVolumeLevel(SettingsItem.Master, 6);
            settings.ToggleMute();
            Assert.IsTrue(settings.Muted);
            Assert.AreEqual(0f, settings.ListenerVolume, Tolerance);
            Assert.AreEqual(0.6f, settings.MasterVolume, Tolerance);
            settings.ToggleMute();
            Assert.AreEqual(0.6f, settings.ListenerVolume, Tolerance, "unmute brings the old volume back");
        }

        [Test]
        public void TurningTheMasterUp_Unmutes_TurningItDown_DoesNot()
        {
            GameSettings settings = new GameSettings();
            settings.SetVolumeLevel(SettingsItem.Master, 5);
            settings.ToggleMute();
            settings.Change(SettingsItem.Master, -1);
            Assert.IsTrue(settings.Muted, "quieter while muted stays muted");
            settings.Change(SettingsItem.Master, 1);
            Assert.IsFalse(settings.Muted, "louder means: I want to hear it");
        }

        [Test]
        public void MutedAtFullVolume_RightOrAClickOnTheMasterBar_Unmutes()
        {
            GameSettings settings = new GameSettings();
            settings.ToggleMute();
            Assert.IsTrue(settings.Change(SettingsItem.Master, 1), "nothing louder than 100 %, but it unmutes");
            Assert.IsFalse(settings.Muted);
            settings.ToggleMute();
            Assert.IsTrue(settings.SetVolumeLevel(SettingsItem.Master, 10));
            Assert.IsFalse(settings.Muted);
        }

        [Test]
        public void MusicAndEffects_DoNotTouchMute()
        {
            GameSettings settings = new GameSettings();
            settings.ToggleMute();
            settings.SetVolumeLevel(SettingsItem.Music, 2);
            settings.Change(SettingsItem.Effects, -1);
            Assert.IsTrue(settings.Muted);
        }

        [Test]
        public void OnOffRows_LeftIsOff_RightIsOn_ClickFlips()
        {
            GameSettings settings = new GameSettings();
            Assert.IsTrue(settings.IsOn(SettingsItem.Sound));
            Assert.IsTrue(settings.Change(SettingsItem.Sound, -1));
            Assert.IsTrue(settings.Muted);
            Assert.IsFalse(settings.Change(SettingsItem.Sound, -1), "already off");
            Assert.IsTrue(settings.Change(SettingsItem.ReducedMotion, 1));
            Assert.IsTrue(settings.ReducedMotion);
            Assert.IsTrue(settings.Toggle(SettingsItem.ReducedMotion));
            Assert.IsFalse(settings.ReducedMotion);
        }

        [Test]
        public void SaveString_RoundTrip_UsesADot()
        {
            GameSettings settings = new GameSettings();
            settings.SetVolumeLevel(SettingsItem.Master, 8);
            settings.SetVolumeLevel(SettingsItem.Music, 3);
            settings.SetVolumeLevel(SettingsItem.Effects, 0);
            settings.ToggleMute();
            settings.Toggle(SettingsItem.ReducedMotion);

            string text = settings.ToSaveString();
            Assert.AreEqual("0.8|0.3|0|1|1", text);

            GameSettings loaded = GameSettings.FromSaveString(text);
            Assert.AreEqual(0.8f, loaded.MasterVolume, Tolerance);
            Assert.AreEqual(0.3f, loaded.MusicVolume, Tolerance);
            Assert.AreEqual(0f, loaded.EffectsVolume, Tolerance);
            Assert.IsTrue(loaded.Muted);
            Assert.IsTrue(loaded.ReducedMotion);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("rubbish")]
        [TestCase("|||")]
        public void EmptyOrBrokenSave_GivesDefaults(string text)
        {
            GameSettings loaded = GameSettings.FromSaveString(text);
            Assert.AreEqual(1f, loaded.MasterVolume, Tolerance);
            Assert.AreEqual(1f, loaded.MusicVolume, Tolerance);
            Assert.IsFalse(loaded.Muted);
            Assert.IsFalse(loaded.ReducedMotion);
        }

        [Test]
        public void BrokenParts_KeepTheirDefault_GoodPartsLoad()
        {
            GameSettings loaded = GameSettings.FromSaveString("0.5|abc|NaN|2|1");
            Assert.AreEqual(0.5f, loaded.MasterVolume, Tolerance);
            Assert.AreEqual(1f, loaded.MusicVolume, Tolerance, "abc = default");
            Assert.AreEqual(1f, loaded.EffectsVolume, Tolerance, "NaN = default");
            Assert.IsFalse(loaded.Muted, "2 is not a flag = default");
            Assert.IsTrue(loaded.ReducedMotion);
        }

        [Test]
        public void OutOfRangeSave_IsClamped_AndAnOldShortSaveStillLoads()
        {
            GameSettings loud = GameSettings.FromSaveString("7|-3|0.25");
            Assert.AreEqual(1f, loud.MasterVolume, Tolerance);
            Assert.AreEqual(0f, loud.MusicVolume, Tolerance);
            Assert.AreEqual(0.25f, loud.EffectsVolume, Tolerance);
            Assert.IsFalse(loud.Muted, "missing parts keep their default");
        }

        [Test]
        public void OddSavedVolume_SnapsToTheGridOnTheNextStep()
        {
            GameSettings settings = GameSettings.FromSaveString("1|0.83|1|0|0");
            settings.Change(SettingsItem.Music, 1);
            Assert.AreEqual(0.9f, settings.MusicVolume, Tolerance, "0.83 counts as 80 %, one step up = 90 %");
        }

        [Test]
        public void Level_RoundsToTheNearestStep()
        {
            Assert.AreEqual(0, GameSettings.Level(0f));
            Assert.AreEqual(8, GameSettings.Level(0.8f));
            Assert.AreEqual(8, GameSettings.Level(0.83f));
            Assert.AreEqual(3, GameSettings.Level(0.27f));
            Assert.AreEqual(10, GameSettings.Level(1f));
            Assert.AreEqual(10, GameSettings.Level(5f));
            Assert.AreEqual(0, GameSettings.Level(float.NaN));
        }
    }
}
