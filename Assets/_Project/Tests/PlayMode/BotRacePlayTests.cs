using System.Collections;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Role 3 Play Mode tests (task V10): the real game (Main.unity + the campus track RaceBootstrap picks) with
    // Luc's five bots. The player's kart just waits on its grid slot; the bots must line up in front of it and then
    // drive a full lap of the campus on their own, without being put back on the racing line once.
    public class BotRacePlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const float LapTimeoutSeconds = 240f; // game seconds; a bot lap of the campus takes about 80 to 100
        private const string CoachKey = "CarcavelosCircuit.CoachDone";

        private RaceManager race;
        private RaceRoster roster;
        private BotSquad squad;
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private int savedCoachFlags;
        private bool hadCoachFlags;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((squad == null || !squad.HasSpawned || roster == null || !roster.IsSetUp) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                roster = Object.FindAnyObjectByType<RaceRoster>();
                squad = Object.FindAnyObjectByType<BotSquad>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(race, "Main.unity has no RaceManager");
            Assert.IsNotNull(squad, "Main.unity has no BotSquad (Role4SceneBuilder puts it on the Racers object)");
            Assert.IsTrue(squad.HasSpawned, "the bots were not spawned within " + SetUpTimeoutSeconds + " s");
            yield return null; // RaceRoster lays out the grid again in the frame after the bots joined
            yield return null;

            // The robot must not change a real player's records or coach tips on this computer (as RacersPlayTests).
            string[] keys = { "CarcavelosCircuit.Best." + race.TrackName, GhostLoader.SaveKey(race.TrackName) };
            for (int i = 0; i < keys.Length; i++)
            {
                savedStrings[keys[i]] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
            }
            hadCoachFlags = PlayerPrefs.HasKey(CoachKey);
            savedCoachFlags = PlayerPrefs.GetInt(CoachKey, 0);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            foreach (KeyValuePair<string, string> pair in savedStrings)
            {
                if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                else PlayerPrefs.SetString(pair.Key, pair.Value);
            }
            if (hadCoachFlags) PlayerPrefs.SetInt(CoachKey, savedCoachFlags);
            else PlayerPrefs.DeleteKey(CoachKey);
            PlayerPrefs.Save();
            savedStrings.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveBots_OnThePairGrid_PlayerStartsLast()
        {
            Assert.AreEqual(5, squad.Bots.Count, "five bots (BotSettings)");
            Assert.AreEqual(6, RacerRegistry.Count, "the player + five bots");
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                BotDriver bot = squad.Bots[i];
                Assert.IsFalse(bot.IsPlayer);
                Assert.IsTrue(names.Add(bot.DisplayName), "two bots called " + bot.DisplayName);
                Assert.That(bot.DisplayName.Length, Is.InRange(1, 16), "a name tag fits one short line");
                Assert.AreNotEqual(PlayerRacer.PlayerColour, bot.MapColor, "only the player is orange");
                Vector3 slot;
                Assert.IsTrue(roster.TryGetSlotPosition(bot, out slot), bot.DisplayName + " has a grid slot");
                Assert.Less(Flat(bot.Root.position - slot).magnitude, 0.5f, bot.DisplayName + " stands on its slot");
                Assert.IsFalse(bot.Kart.InputEnabled, "nobody drives before GO");
            }

            // Pairs side by side, bots in front, the player alone in the last slot (slot 5 = third row, right).
            Transform spawn = race.Track.SpawnPoint;
            Vector3 playerSlot;
            Assert.IsTrue(roster.TryGetSlotPosition(roster.Player, out playerSlot));
            Vector3 lastSlot = spawn.position + spawn.rotation * RacerGrid.OffsetOf(5, 2f, 3f);
            Assert.Less(Flat(playerSlot - lastSlot).magnitude, 0.05f, "the player starts last, behind all five bots");
            Assert.Less(Flat(race.KartTransform.position - playerSlot).magnitude, 0.5f, "the player's kart is on that slot");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveBots_DriveAFullCampusLap_WithoutGettingStuck()
        {
            Debug.Log("BotRacePlayTests: racing on " + race.TrackName + ", lap " + roster.Progress.LapLength.ToString("0") + " m.");
            Assert.AreNotEqual("RaceFlow_PlaceholderTrack", race.TrackName, "the bots must race the campus, not the test loop");
            Time.timeScale = 2f; // same physics per game second, the test just takes half as long
            race.BeginRace();
            float waited = 0f;
            while (race.State != RaceState.Racing && waited < 10f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(RaceState.Racing, race.State);

            float start = Time.time;
            float[] lapTimes = new float[squad.Bots.Count];
            int finished = 0;
            while (finished < squad.Bots.Count && Time.time - start < LapTimeoutSeconds)
            {
                for (int i = 0; i < squad.Bots.Count; i++)
                {
                    LapProgress progress = roster.Standings.ProgressOf(squad.Bots[i]);
                    if (lapTimes[i] == 0f && progress != null && progress.LapsDone >= 1)
                    {
                        lapTimes[i] = Time.time - start;
                        finished++;
                    }
                }
                yield return null;
            }
            Time.timeScale = 1f;

            for (int i = 0; i < squad.Bots.Count; i++)
            {
                BotDriver bot = squad.Bots[i];
                LapProgress progress = roster.Standings.ProgressOf(bot);
                Debug.Log("BotRacePlayTests: " + bot.DisplayName + (lapTimes[i] > 0f ? " lap in " + lapTimes[i].ToString("0.0") + " s" : " did NOT finish")
                    + ", drifted " + bot.DriftCount + "x, backed out " + bot.ReverseCount + "x, put back " + bot.PutBackCount + "x, at " + (progress != null ? progress.Total.ToString("0") : "?") + " m");
            }
            for (int i = 0; i < squad.Bots.Count; i++)
            {
                BotDriver bot = squad.Bots[i];
                Assert.Greater(lapTimes[i], 0f, bot.DisplayName + " did not finish a lap in " + LapTimeoutSeconds + " s");
                Assert.AreEqual(0, bot.PutBackCount, bot.DisplayName + " got stuck or fell off and had to be put back");
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
