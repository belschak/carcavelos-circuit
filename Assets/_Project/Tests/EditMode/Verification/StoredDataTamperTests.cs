using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // In the browser, PlayerPrefs is the site's own storage, which any player can edit in the developer tools.
    // So everything the game reads back from it is treated like input: a tampered value is refused or clamped,
    // never shown or sent as it is. Rejected and edge cases for SettingsStore and the saved settings text.
    public class StoredDataTamperTests
    {
        private const string NameKey = "CarcavelosCircuit.PlayerName";
        private const string SettingsKey = "CarcavelosCircuit.Settings";

        private bool hadName;
        private string savedName;
        private bool hadSettings;
        private string savedSettings;

        [SetUp]
        public void RememberThePlayersOwnValues()
        {
            hadName = PlayerPrefs.HasKey(NameKey);
            savedName = PlayerPrefs.GetString(NameKey, "");
            hadSettings = PlayerPrefs.HasKey(SettingsKey);
            savedSettings = PlayerPrefs.GetString(SettingsKey, "");
        }

        [TearDown]
        public void PutThemBack()
        {
            Restore(NameKey, hadName, savedName);
            Restore(SettingsKey, hadSettings, savedSettings);
            PlayerPrefs.Save();
        }

        private static void Restore(string key, bool had, string value)
        {
            if (had)
            {
                PlayerPrefs.SetString(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void SavedName_ComesBack_ForTheNextSaveForm()
        {
            SettingsStore.SavePlayerName("José Müller");
            Assert.AreEqual("José Müller", SettingsStore.LoadPlayerName());
        }

        [Test]
        public void NoSavedName_IsEmpty()
        {
            PlayerPrefs.DeleteKey(NameKey);
            Assert.AreEqual("", SettingsStore.LoadPlayerName());
        }

        [TestCase("<b>Ana</b>")]                 // rich text would style the leaderboard
        [TestCase("<color=red>X</color>")]
        [TestCase(" Ana")]                       // not clean: the form would save something else
        [TestCase("Ana  Rita")]
        [TestCase("A")]                          // too short
        [TestCase("Seventeen_Chars17")]          // too long
        [TestCase("'; drop table lap_times; --")]
        [TestCase("Sh1t")]                       // blocked word
        [TestCase("Ana😀")]
        public void TamperedName_IsNotOffered(string tampered)
        {
            PlayerPrefs.SetString(NameKey, tampered);
            Assert.AreEqual("", SettingsStore.LoadPlayerName(), "a name edited by hand is ignored, the field starts empty");
        }

        [Test]
        public void Settings_SaveAndLoad_RoundTrip()
        {
            GameSettings settings = new GameSettings();
            settings.SetVolumeLevel(SettingsItem.Music, 3);
            settings.ToggleMute();
            SettingsStore.Save(settings);
            GameSettings loaded = SettingsStore.Load();
            Assert.AreEqual(0.3f, loaded.MusicVolume, 0.0001f);
            Assert.IsTrue(loaded.Muted);
        }

        [Test]
        public void NoSavedSettings_GiveTheDefaults()
        {
            PlayerPrefs.DeleteKey(SettingsKey);
            GameSettings loaded = SettingsStore.Load();
            Assert.AreEqual(1f, loaded.MasterVolume);
            Assert.AreEqual(1f, loaded.MusicVolume);
            Assert.AreEqual(1f, loaded.EffectsVolume);
            Assert.IsFalse(loaded.Muted);
            Assert.IsFalse(loaded.ReducedMotion);
        }

        // Volumes outside 0..1 or not numbers at all: clamped or the default, never louder than full.
        [TestCase("Infinity|1|1|0|0", 1f)]
        [TestCase("-Infinity|1|1|0|0", 1f)]
        [TestCase("NaN|1|1|0|0", 1f)]
        [TestCase("1e9|1|1|0|0", 1f)]
        [TestCase("-5|1|1|0|0", 0f)]
        [TestCase("0,5|1|1|0|0", 1f)]            // a comma is not our format: default
        [TestCase("0x1|1|1|0|0", 1f)]
        public void TamperedVolume_IsClampedOrDefault(string save, float expectedMaster)
        {
            PlayerPrefs.SetString(SettingsKey, save);
            GameSettings loaded = SettingsStore.Load();
            Assert.AreEqual(expectedMaster, loaded.MasterVolume, 0.0001f);
            Assert.That(loaded.ListenerVolume, Is.InRange(0f, 1f));
        }

        // Flags are only "0" or "1"; anything else keeps the default (off).
        [TestCase("1|1|1|2|0")]
        [TestCase("1|1|1|true|0")]
        [TestCase("1|1|1|-1|0")]
        public void TamperedFlag_KeepsTheDefault(string save)
        {
            PlayerPrefs.SetString(SettingsKey, save);
            Assert.IsFalse(SettingsStore.Load().Muted);
        }

        [Test]
        public void HugeSave_DoesNotBreakLoading()
        {
            PlayerPrefs.SetString(SettingsKey, new string('|', 100000));
            GameSettings loaded = SettingsStore.Load();
            Assert.AreEqual(1f, loaded.MasterVolume);
        }
    }
}
