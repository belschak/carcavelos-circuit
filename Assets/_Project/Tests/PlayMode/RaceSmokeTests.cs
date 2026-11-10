using System.Collections;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using CarcavelosCircuit.Track;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Role 4 Play Mode tests: the real game (Main.unity + whatever track RaceBootstrap picks) is played by a robot.
    // The robot does not steer: it teleports the kart into each checkpoint with ResetTo, the same call Retry and
    // Recover use. So these tests check the race flow on the real track scene (triggers, order, finish, recover,
    // retry, slow motion), not the driving. Run: docs/setup.md "Run the tests" (Play Mode).
    public class RaceSmokeTests
    {
        private const float SetUpTimeoutSeconds = 60f; // a big campus scene loads slowly on the first run

        private RaceManager race;
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private int savedCoachFlags;
        private bool hadCoachFlags;

        // The game saves the best time, ghost and coach tips in PlayerPrefs. The robot's laps must not overwrite a
        // real player's records on this computer, so we remember them first and put them back afterwards.
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
            Assert.IsTrue(race.IsSetUp, "the race was not set up within " + SetUpTimeoutSeconds + " s (see the Console for RaceBootstrap errors)");
            SaveRecords(race.TrackName);
        }

        [UnityTearDown]
        public IEnumerator RestoreRecords()
        {
            Time.timeScale = 1f;
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

        // Swap guard (Q5): RaceFlow reads Role 2's TrackInfo. If a campus scene's track parts were not found, the race
        // would quietly fall back to Role 4's small test loop and every other test here would still pass on it.
        [UnityTest]
        public IEnumerator RacesOnACampusTrack_NotTheTestLoop()
        {
            Assert.AreNotEqual("RaceFlow_PlaceholderTrack", race.TrackName,
                "the race fell back to the test loop: no campus scene in the build list has a TrackInfo (see the RaceBootstrap lines in the Console)");
            TrackInfo track = Object.FindAnyObjectByType<TrackInfo>();
            Assert.IsNotNull(track, "no TrackInfo in the loaded track scene");
            Assert.AreEqual(track.gameObject.scene.name, race.TrackName, "the race uses the TrackInfo of the scene it says it races on");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CleanLap_FinishesOnce_WithATime_AndFullSpeedAfterTheSlowMotion()
        {
            Debug.Log("RaceSmokeTests: track " + race.TrackName + ", " + race.CheckpointCount + " checkpoints, lap about " + Mathf.RoundToInt(race.LapMetres) + " m");
            Assert.AreEqual(RaceState.Ready, race.State);
            Assert.Greater(race.CheckpointCount, 1, "the track needs a start/finish line and at least one checkpoint");
            Assert.IsFalse(race.Kart.InputEnabled, "the kart must not drive on the start screen");

            yield return StartAndWaitForGo();
            Assert.IsTrue(race.Kart.InputEnabled, "input is on after GO");
            ChaseCameraRig chase = Object.FindAnyObjectByType<ChaseCameraRig>();
            if (chase != null && Object.FindAnyObjectByType<MapFlyover>() != null)
            {
                Assert.IsTrue(chase.enabled, "after GO the map fly-over has handed the camera back to the chase camera");
                Assert.IsTrue(chase.GetComponent<CinemachineBrain>().enabled, "after GO Cinemachine drives the Main Camera again");
                CinemachineCamera chaseCamera = Object.FindAnyObjectByType<CinemachineCamera>();
                Assert.IsNotNull(chaseCamera, "Main has Role 1's ChaseCamera (Q13)");
                Assert.IsNotNull(chaseCamera.Follow, "the chase camera has a target");
                Assert.AreSame(race.Kart, chaseCamera.Follow.GetComponent<IKartState>(), "the chase camera follows the spawned kart");
            }

            int finishedLaps = 0;
            float finishedTime = 0f;
            race.LapFinished += (seconds, record) => { finishedLaps++; finishedTime = seconds; };

            // Every checkpoint in driving order, then the start/finish line.
            for (int passed = 1; passed < race.CheckpointCount; passed++)
            {
                yield return DriveIntoNextCheckpoint();
                Assert.AreEqual(passed, race.CheckpointsPassed, "checkpoint " + passed + " did not count");
            }
            yield return DriveIntoNextCheckpoint();

            Assert.AreEqual(RaceState.Results, race.State, "crossing the line after all checkpoints ends the lap");
            Assert.AreEqual(1, finishedLaps, "exactly one lap time");
            Assert.Greater(finishedTime, 0f);
            Assert.IsFalse(race.Kart.InputEnabled, "no driving on the results screen");

            // The finish slow motion lasts 1.2 s of real time and must hand back full speed.
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time.timeScale must be 1 again after the finish moment");

            // Crossing the line again on the results screen changes nothing.
            yield return DriveIntoCheckpoint(0);
            Assert.AreEqual(1, finishedLaps, "a second crossing must not give a second time");
        }

        [UnityTest]
        public IEnumerator SkippedCheckpoint_DoesNotFinish_AndRecoverKeepsTimeAndProgress()
        {
            yield return StartAndWaitForGo();
            yield return DriveIntoNextCheckpoint();
            yield return DriveIntoNextCheckpoint();
            Assert.AreEqual(2, race.CheckpointsPassed);

            // Shortcut straight to the line: no finish.
            yield return DriveIntoCheckpoint(0);
            Assert.AreEqual(RaceState.Racing, race.State, "a lap that skipped checkpoints must not finish");

            // A checkpoint out of order does not count either.
            if (race.CheckpointCount > 4)
            {
                yield return DriveIntoCheckpoint(4);
                Assert.AreEqual(2, race.CheckpointsPassed, "checkpoint 4 straight after 2 must not count");
            }

            // Recover (key R): back to the last valid checkpoint, timer keeps running, nothing awarded.
            float before = race.LapSeconds;
            race.Recover();
            yield return WaitPhysics();
            Assert.AreEqual(2, race.CheckpointsPassed, "recover awards no checkpoint");
            Assert.Greater(race.LapSeconds, before, "the timer keeps running through a recover");
            Assert.AreEqual(RaceState.Racing, race.State);
        }

        [UnityTest]
        public IEnumerator Retry_ResetsTimerAndCheckpoints_AndCountsDownAgain()
        {
            yield return StartAndWaitForGo();
            yield return DriveIntoNextCheckpoint();
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(race.LapSeconds, 0f);

            race.Retry();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "retry goes straight to a new countdown");
            Assert.AreEqual(0, race.CheckpointsPassed, "retry clears the checkpoints");
            Assert.AreEqual(0f, race.LapSeconds, 0.0001f, "retry clears the timer");
            Assert.IsFalse(race.Kart.InputEnabled, "no driving during the countdown");
            Assert.AreEqual(1f, Time.timeScale, 0.001f);

            yield return WaitForState(RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State);
        }

        // ---------- helpers ----------

        private IEnumerator StartAndWaitForGo()
        {
            race.BeginRace();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State, "the first key starts the countdown");
            Assert.IsFalse(race.Kart.InputEnabled, "no driving during the countdown");
            yield return WaitForState(RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State, "GO after the countdown");
        }

        private IEnumerator WaitForState(RaceState wanted, float timeoutSeconds)
        {
            float waited = 0f;
            while (race.State != wanted && waited < timeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator DriveIntoNextCheckpoint()
        {
            Vector3 target;
            Assert.IsTrue(race.TryGetNextCheckpointPosition(out target), "no next checkpoint");
            yield return TeleportTo(target);
        }

        private IEnumerator DriveIntoCheckpoint(int index)
        {
            Checkpoint[] all = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Index == index)
                {
                    yield return TeleportTo(all[i].transform.position);
                    yield break;
                }
            }
            Assert.Fail("no checkpoint with index " + index);
        }

        // Out of the trigger first (high above it), then into its centre: one clean "enter".
        private IEnumerator TeleportTo(Vector3 target)
        {
            Quaternion rotation = race.KartTransform.rotation;
            race.Kart.ResetTo(target + Vector3.up * 50f, rotation);
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
            string[] keys = { "CarcavelosCircuit.Best." + trackName, GhostLoader.SaveKey(trackName) };
            for (int i = 0; i < keys.Length; i++)
            {
                savedStrings[keys[i]] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
            }
            hadCoachFlags = PlayerPrefs.HasKey("CarcavelosCircuit.CoachDone");
            savedCoachFlags = PlayerPrefs.GetInt("CarcavelosCircuit.CoachDone", 0);
        }
    }
}
