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
    // Race modes + finish cinematic (Luc, 16 Nov 2026), in the real game (Main.unity). Like RaceSmokeTests the robot
    // teleports the kart through the checkpoints; after the line it checks the autopilot, the camera swing and the
    // way back to the menu. The player's saved mode, records and coach tips are put back afterwards.
    public class RaceModesPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string ModeKey = "CarcavelosCircuit.RaceMode";

        private RaceManager race;
        private BotSquad squad;
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private bool hadMode;
        private int savedMode;
        private bool hadCoachFlags;
        private int savedCoachFlags;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            hadMode = PlayerPrefs.HasKey(ModeKey);
            savedMode = PlayerPrefs.GetInt(ModeKey, 0);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((race == null || !race.IsSetUp || squad == null || !squad.HasSpawned) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                squad = Object.FindAnyObjectByType<BotSquad>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(race, "Main.unity has no RaceManager");
            Assert.IsTrue(race.IsSetUp, "the race was not set up within " + SetUpTimeoutSeconds + " s");
            Assert.IsNotNull(squad, "Main.unity has no BotSquad");
            string[] keys = { "CarcavelosCircuit.Best." + race.TrackName, GhostLoader.SaveKey(race.TrackName) };
            for (int i = 0; i < keys.Length; i++)
            {
                savedStrings[keys[i]] = PlayerPrefs.HasKey(keys[i]) ? PlayerPrefs.GetString(keys[i]) : null;
            }
            hadCoachFlags = PlayerPrefs.HasKey("CarcavelosCircuit.CoachDone");
            savedCoachFlags = PlayerPrefs.GetInt("CarcavelosCircuit.CoachDone", 0);
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
            savedStrings.Clear();
            if (hadCoachFlags) PlayerPrefs.SetInt("CarcavelosCircuit.CoachDone", savedCoachFlags);
            else PlayerPrefs.DeleteKey("CarcavelosCircuit.CoachDone");
            if (hadMode) PlayerPrefs.SetInt(ModeKey, savedMode);
            else PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Modes_OnlineRefused_TimeAttackAlone_BotsComeBack()
        {
            string message;
            Assert.IsFalse(race.TrySelectMode(RaceMode.OnlineRace, out message), "online is not playable yet");
            Assert.AreEqual(RaceModeRules.ComingSoonMessage, message);
            Assert.AreNotEqual(RaceMode.OnlineRace, race.Mode, "a refused mode changes nothing");
            yield return null;
            Assert.AreEqual(RaceState.Ready, race.State, "picking Online does not start a race");

            Assert.IsTrue(race.TrySelectMode(RaceMode.TimeAttack, out message));
            yield return WaitFrames(3); // BotSquad removes the bots, Destroy at the end of the frame, roster re-grids
            Assert.AreEqual(0, squad.Bots.Count, "no bots in Time Attack");
            Assert.AreEqual(1, RacerRegistry.Count, "only the player races");

            Assert.IsTrue(race.TrySelectMode(RaceMode.RaceVsBots, out message));
            yield return WaitFrames(3);
            Assert.AreEqual(5, squad.Bots.Count, "the five bots are back for Race vs Bots");
            Assert.AreEqual(6, RacerRegistry.Count);

            race.BeginRace();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.IsFalse(race.TrySelectMode(RaceMode.TimeAttack, out message), "no mode change once the countdown runs");
            Assert.AreEqual(RaceMode.RaceVsBots, race.Mode);
        }

        [UnityTest]
        public IEnumerator Finish_AutopilotDrives_CameraSwingsToTheFront_MenuGoesBack()
        {
            string message;
            Assert.IsTrue(race.TrySelectMode(RaceMode.TimeAttack, out message)); // empty road: the cruise is not blocked
            yield return WaitFrames(3);
            race.BeginRace();
            yield return WaitForState(RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State);
            for (int passed = 1; passed < race.CheckpointCount; passed++)
            {
                yield return DriveIntoNextCheckpoint();
            }
            yield return DriveIntoNextCheckpoint();
            Assert.AreEqual(RaceState.Results, race.State, "the lap is over");
            yield return null;

            Assert.IsTrue(squad.IsCruising, "the finish cruise took the wheel");
            Assert.IsTrue(race.PlayerOnAutopilot);
            Assert.IsTrue(race.Kart.InputEnabled, "the kart drives on after the line, on the autopilot");
            Vector3 atLine = race.KartTransform.position;

            // Past the swing (1.0 s + 1.8 s of real time): the camera is in front of the kart, looking back at it.
            yield return new WaitForSecondsRealtime(FinishCinematic.SwingStartSeconds + FinishCinematic.SwingSeconds + 0.3f);
            ChaseCameraRig rig = Object.FindAnyObjectByType<ChaseCameraRig>();
            if (rig != null)
            {
                Assert.IsTrue(rig.IsFinishShotActive, "the finish shot moves the camera");
                Vector3 toCamera = rig.transform.position - race.KartTransform.position;
                Assert.Greater(Vector3.Dot(toCamera, race.KartTransform.forward), 2f, "the camera is in front of the kart");
            }
            float rolled = Vector3.Distance(atLine, race.KartTransform.position);
            Debug.Log("RaceModesPlayTests: the cruise rolled " + rolled.ToString("0.0") + " m after the line.");
            Assert.Greater(rolled, 5f, "the kart rolled on after the line");

            race.BackToMenu();
            yield return null;
            Assert.AreEqual(RaceState.Ready, race.State, "back on the start screen");
            Assert.IsFalse(race.PlayerOnAutopilot, "the autopilot let go");
            Assert.IsFalse(squad.IsCruising);
            Assert.IsFalse(race.Kart.InputEnabled, "nobody drives on the start screen");
            Assert.AreEqual(0, race.CheckpointsPassed);
        }

        // ---------- helpers (as in RaceSmokeTests) ----------

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
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
    }
}
