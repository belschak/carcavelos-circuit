using System.Collections;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Role 4 Play Mode test (task R10): the real game, a robot lap (teleports through the checkpoints like
    // RaceSmokeTests), then the results card's "SAVE YOUR TIME" with a fake leaderboard (no network): a bad name is
    // refused and nothing is sent, a good name is sent, remembered, and shows up highlighted in the top 10.
    // A teleport lap would be far too fast for the leaderboard (LapTimeRules), so the robot first waits on the grid
    // until the lap is as long as the fastest lap the game believes on this track.
    public class LeaderboardPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string RobotName = "Robot Tester";

        private RaceManager race;
        private LeaderboardHud leaderboard;
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private int savedCoachFlags;
        private bool hadCoachFlags;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((race == null || !race.IsSetUp) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(race, "Main.unity has no RaceManager");
            Assert.IsTrue(race.IsSetUp, "the race was not set up in time");
            leaderboard = Object.FindAnyObjectByType<LeaderboardHud>();
            Assert.IsNotNull(leaderboard, "Main.unity has no LeaderboardHud on the HUD (run Role4SceneBuilder.BuildAll)");
            SaveRecords(race.TrackName);
        }

        // The robot's lap must not overwrite this computer's real records, settings or leaderboard.
        [UnityTearDown]
        public IEnumerator RestoreRecords()
        {
            Time.timeScale = 1f;
            RaceKeys.TextEntryActive = false;
            foreach (KeyValuePair<string, string> pair in savedStrings)
            {
                if (pair.Value == null)
                {
                    PlayerPrefs.DeleteKey(pair.Key);
                }
                else
                {
                    PlayerPrefs.SetString(pair.Key, pair.Value);
                }
            }
            if (hadCoachFlags)
            {
                PlayerPrefs.SetInt("CarcavelosCircuit.CoachDone", savedCoachFlags);
            }
            else
            {
                PlayerPrefs.DeleteKey("CarcavelosCircuit.CoachDone");
            }
            PlayerPrefs.Save();
            savedStrings.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FinishedLap_SaveAName_ItShowsInTheTop10()
        {
            FakeLeaderboardService fake = new FakeLeaderboardService();
            fake.SubmitTime(race.TrackName, "Fast Ana", 1000f, reply => { }); // somebody is already on the board
            leaderboard.UseService(fake);
            yield return null;
            int submitsBefore = fake.SubmitCalls;

            // Start, wait on the grid until the lap may go on the board, then the robot lap.
            race.BeginRace();
            yield return WaitUntil(() => race.State == RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State);
            float wanted = leaderboard.MinLapSeconds + 0.5f;
            Debug.Log("LeaderboardPlayTests: waiting until the lap is " + wanted.ToString("0.0") + " s long (the fastest lap the game believes here)");
            yield return WaitUntil(() => race.LapSeconds >= wanted, wanted + 30f);
            for (int passed = 1; passed < race.CheckpointCount; passed++)
            {
                yield return DriveIntoNextCheckpoint();
            }
            yield return DriveIntoNextCheckpoint();
            Assert.AreEqual(RaceState.Results, race.State, "the lap ended");
            Assert.IsTrue(LapTimeRules.IsAcceptable(race.LapSeconds, race.MedalSeconds(Medal.Gold)), "the robot lap is long enough for the board");

            // The results card comes in after the finish beat; the form is open there.
            VisualElement root = leaderboard.GetComponent<UIDocument>().rootVisualElement;
            VisualElement hudRoot = root.Q<VisualElement>("hud-root");
            yield return WaitUntil(() => hudRoot.ClassListContains("st-results"), 5f);
            Assert.IsTrue(hudRoot.ClassListContains("st-results"), "the results card is in");
            Assert.AreEqual(SaveFormState.Editing, leaderboard.Form.State, "SAVE YOUR TIME is offered");

            // A rejected name: a clear message, nothing sent.
            leaderboard.Form.Field.value = "<b>";
            leaderboard.SaveTypedName();
            Assert.AreEqual(SaveFormState.Editing, leaderboard.Form.State, "a bad name keeps the form open");
            Assert.AreEqual(submitsBefore, fake.SubmitCalls, "a bad name is never sent");
            Assert.IsTrue(HasLabel(root, "lb-msg", PlayerNameRules.Message(NameProblem.BadCharacter)), "the form says what is wrong");

            // A good name (with extra spaces, which are cleaned): sent, remembered, in the list and highlighted.
            leaderboard.Form.Field.value = "  Robot   Tester ";
            leaderboard.SaveTypedName();
            Assert.AreEqual(submitsBefore + 1, fake.SubmitCalls);
            Assert.AreEqual(RobotName, fake.LastName, "the cleaned name is sent");
            Assert.AreEqual(SaveFormState.Saved, leaderboard.Form.State);
            Assert.AreEqual(RobotName, SettingsStore.LoadPlayerName(), "the name is remembered for next time");
            Assert.IsTrue(HasLabel(root, "lb-result-rank", "YOU ARE #1"), "faster than Fast Ana: first place");
            yield return null;

            VisualElement row = RowWithName(root.Q<VisualElement>("results-board-host"), RobotName);
            Assert.IsNotNull(row, "the saved name is in the results top 10");
            Assert.IsTrue(row.ClassListContains("lb-row--new"), "the row just saved is highlighted");
            Assert.IsNotNull(RowWithName(root.Q<VisualElement>("results-board-host"), "Fast Ana"), "the others are still there");
            Assert.IsFalse(RaceKeys.TextEntryActive, "the game keys are on again after saving");

            // Retry closes the form.
            race.Retry();
            yield return null;
            yield return null;
            Assert.AreEqual(SaveFormState.Hidden, leaderboard.Form.State);
        }

        [UnityTest]
        public IEnumerator StartScreen_ShowsTheTitle_AndABoard()
        {
            VisualElement root = leaderboard.GetComponent<UIDocument>().rootVisualElement;
            Assert.AreEqual(RaceState.Ready, race.State);
            Assert.AreEqual(10, root.Q<VisualElement>("title-letters").childCount, "C-A-R-C-A-V-E-L-O-S, one label each");
            Assert.IsTrue(HasLabel(root, "ts-slogan", "CARCAVELOS ROUND 3"), "the team's slogan is on the start screen");
            Assert.AreEqual(10, root.Q<VisualElement>("start-board-host").Query<VisualElement>(className: "lb-row").ToList().Count, "ten rows on the start board");
            yield return null;
        }

        // ---------- helpers ----------

        private static bool HasLabel(VisualElement root, string className, string text)
        {
            List<Label> labels = root.Query<Label>(className: className).ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].text == text)
                {
                    return true;
                }
            }
            return false;
        }

        private static VisualElement RowWithName(VisualElement board, string name)
        {
            List<VisualElement> rows = board.Query<VisualElement>(className: "lb-row").ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                Label label = rows[i].Q<Label>(className: "lb-name");
                if (label != null && label.text == name)
                {
                    return rows[i];
                }
            }
            return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, float timeoutSeconds)
        {
            float waited = 0f;
            while (!done() && waited < timeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator DriveIntoNextCheckpoint()
        {
            Vector3 target;
            Assert.IsTrue(race.TryGetNextCheckpointPosition(out target), "no next checkpoint");
            Quaternion rotation = race.KartTransform.rotation;
            race.Kart.ResetTo(target + Vector3.up * 50f, rotation); // out of the trigger first, then into it: one clean "enter"
            yield return WaitPhysics();
            race.Kart.ResetTo(target, rotation);
            yield return WaitPhysics();
        }

        private static IEnumerator WaitPhysics()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private void SaveRecords(string trackName)
        {
            string[] keys =
            {
                "CarcavelosCircuit.Best." + trackName, GhostLoader.SaveKey(trackName),
                LocalLeaderboardService.SaveKey(trackName), "CarcavelosCircuit.PlayerName",
            };
            for (int i = 0; i < keys.Length; i++)
            {
                savedStrings[keys[i]] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
            }
            hadCoachFlags = PlayerPrefs.HasKey("CarcavelosCircuit.CoachDone");
            savedCoachFlags = PlayerPrefs.GetInt("CarcavelosCircuit.CoachDone", 0);
        }
    }
}
