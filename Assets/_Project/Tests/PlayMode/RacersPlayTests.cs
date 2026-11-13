using System.Collections;
using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Role 4 Play Mode tests (tasks R8 + R9): the real game (Main.unity + the track RaceBootstrap picks) with two of
    // RaceFlow's dev test bots. Checks the race flow for every racer: grid slots, input only while Racing, Retry puts
    // everyone back, bots never pass the player's checkpoints, and the HUD shows positions and the minimap.
    public class RacersPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const string CoachKey = "CarcavelosCircuit.CoachDone";

        private RaceManager race;
        private RaceRoster roster;
        private PositionsHud hud;
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();
        private int savedCoachFlags;
        private bool hadCoachFlags;

        [UnitySetUp]
        public IEnumerator LoadTheGame()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            float waited = 0f;
            while ((race == null || !race.IsSetUp || roster == null || !roster.IsSetUp) && waited < SetUpTimeoutSeconds)
            {
                race = Object.FindAnyObjectByType<RaceManager>();
                roster = Object.FindAnyObjectByType<RaceRoster>();
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(race, "Main.unity has no RaceManager");
            Assert.IsNotNull(roster, "Main.unity has no Racers object (Role4SceneBuilder)");
            Assert.IsTrue(roster.IsSetUp, "the roster was not set up in time");
            hud = Object.FindAnyObjectByType<PositionsHud>();
            Assert.IsNotNull(hud, "Main.unity has no PositionsHud");
            yield return BotsOffForTests.RemoveFromRace(SetUpTimeoutSeconds); // RaceFlow alone + its dev test bots

            // The robot must not change a real player's records or coach tips on this computer (same as RaceSmokeTests).
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
        public IEnumerator Player_IsARacer_AloneThereIsAMapButNoPosition()
        {
            IRacer player = RacerRegistry.FindPlayer();
            Assert.IsNotNull(player, "the player's kart registers as IRacer");
            Assert.IsTrue(player.IsPlayer);
            Assert.AreSame(race.KartTransform, player.Root);
            Assert.AreSame(race.Kart, player.Kart);
            Assert.AreEqual("You", player.DisplayName);
            Assert.AreEqual(1, RacerRegistry.Count, "no dev test bots in the game (RaceSettings > Dev Test Bots = 0)");

            yield return StartAndWaitForGo();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, roster.RacerCount);
            Assert.IsFalse(hud.IsPositionShown, "alone = the plain time trial, no 1st / 1");
            Assert.IsTrue(hud.IsMapShown, "the minimap shows the track even alone");
        }

        [UnityTest]
        public IEnumerator TwoBots_GridCountdownRaceRetry_AndPositions()
        {
            roster.SpawnTestBots(2);
            yield return null;
            Assert.AreEqual(3, RacerRegistry.Count, "player + 2 test bots");
            List<IRacer> bots = Bots();
            Assert.AreEqual(2, bots.Count);
            AssertOnTheirSlots(bots, "after spawning");
            Assert.AreNotEqual(Flat(bots[0].Root.position), Flat(bots[1].Root.position), "two different slots");

            // Countdown: everyone waits.
            race.BeginRace();
            yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State);
            Vector3[] before = Positions(bots);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(RaceState.Countdown, race.State, "the first countdown lasts 3 s");
            for (int i = 0; i < bots.Count; i++)
            {
                Assert.IsFalse(bots[i].Kart.InputEnabled, "no bot drives in the countdown");
                Assert.Less(Vector3.Distance(Flat(before[i]), Flat(bots[i].Root.position)), 0.3f, "a bot moved in the countdown");
            }

            // GO: the bots drive.
            yield return WaitForState(RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State);
            Vector3[] atGo = Positions(bots);
            yield return new WaitForSeconds(2f);
            for (int i = 0; i < bots.Count; i++)
            {
                Assert.IsTrue(bots[i].Kart.InputEnabled, "bots drive while racing");
                float moved = Vector3.Distance(Flat(atGo[i]), Flat(bots[i].Root.position));
                Debug.Log("RacersPlayTests: " + bots[i].DisplayName + " drove " + moved.ToString("0.0") + " m in 2 s");
                Assert.Greater(moved, 2f, bots[i].DisplayName + " did not drive");
            }

            // Positions: three racers, the player has a place and the readout shows it.
            Assert.AreEqual(3, roster.RacerCount);
            int place = roster.PlayerPosition;
            Assert.That(place, Is.InRange(1, 3));
            Assert.IsTrue(hud.IsPositionShown, "position readout with more than one racer");
            Assert.AreEqual(PositionFormat.Ordinal(place).ToUpperInvariant(), hud.PositionText.ToUpperInvariant());
            Assert.IsTrue(hud.IsMapShown);

            // A bot in the next checkpoint does not count for the player (checkpoints are the player's).
            Vector3 next;
            Assert.IsTrue(race.TryGetNextCheckpointPosition(out next));
            int passed = race.CheckpointsPassed;
            bots[0].Kart.ResetTo(next + Vector3.up * 50f, bots[0].Root.rotation);
            yield return WaitPhysics();
            bots[0].Kart.ResetTo(next, bots[0].Root.rotation);
            yield return WaitPhysics();
            Assert.AreEqual(passed, race.CheckpointsPassed, "a bot must never pass the player's checkpoint");

            // Retry: everyone back on the grid, input off for the new countdown.
            race.Retry();
            yield return WaitPhysics();
            Assert.AreEqual(RaceState.Countdown, race.State);
            AssertOnTheirSlots(bots, "after Retry");
            Vector3 playerSlot;
            Assert.IsTrue(roster.TryGetSlotPosition(roster.Player, out playerSlot), "the player has a slot after Retry");
            Assert.Less(Vector3.Distance(Flat(race.KartTransform.position), Flat(playerSlot)), 0.5f, "the player is back on its slot");
            // V23: the player starts last, behind both bots: slot 2 = second row left (2 m left, 3 m back; RaceSettings).
            Transform spawnPoint = race.Track.SpawnPoint;
            Vector3 lastSlot = spawnPoint.position + spawnPoint.rotation * RacerGrid.OffsetOf(2, 2f, 3f);
            Assert.Less(Vector3.Distance(Flat(playerSlot), Flat(lastSlot)), 0.05f, "with two bots the player's slot is the third (last) one");
            for (int i = 0; i < bots.Count; i++)
            {
                Assert.IsFalse(bots[i].Kart.InputEnabled, "no bot drives in the retry countdown");
            }
        }

        // ---------- helpers ----------

        private static List<IRacer> Bots()
        {
            List<IRacer> bots = new List<IRacer>();
            for (int i = 0; i < RacerRegistry.Count; i++)
            {
                if (!RacerRegistry.All[i].IsPlayer)
                {
                    bots.Add(RacerRegistry.All[i]);
                }
            }
            return bots;
        }

        private void AssertOnTheirSlots(List<IRacer> bots, string when)
        {
            for (int i = 0; i < bots.Count; i++)
            {
                Vector3 slot;
                Assert.IsTrue(roster.TryGetSlotPosition(bots[i], out slot), bots[i].DisplayName + " has no slot " + when);
                Assert.Less(Vector3.Distance(Flat(slot), Flat(bots[i].Root.position)), 0.5f, bots[i].DisplayName + " is not on its slot " + when);
            }
        }

        private static Vector3[] Positions(List<IRacer> racers)
        {
            Vector3[] positions = new Vector3[racers.Count];
            for (int i = 0; i < racers.Count; i++)
            {
                positions[i] = racers[i].Root.position;
            }
            return positions;
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        private IEnumerator StartAndWaitForGo()
        {
            race.BeginRace();
            yield return null;
            yield return WaitForState(RaceState.Racing, 10f);
            Assert.AreEqual(RaceState.Racing, race.State);
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

        private static IEnumerator WaitPhysics()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
