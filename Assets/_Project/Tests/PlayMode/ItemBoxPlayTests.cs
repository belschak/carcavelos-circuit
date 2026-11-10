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
    // Role 4 Play Mode tests (task R7): item boxes in the real game (Main.unity + the track RaceBootstrap picks).
    // Like RaceSmokeTests the robot teleports the kart (ResetTo) instead of steering: into a box, then presses
    // "use" through ItemSystem.UseItem (the same call the E key makes).
    public class ItemBoxPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;

        private RaceManager race;
        private ItemSystem items;
        private const string CoachKey = "CarcavelosCircuit.CoachDone";
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
            items = Object.FindAnyObjectByType<ItemSystem>();
            Assert.IsNotNull(items, "Main.unity has no Items object (Role4SceneBuilder)");
            yield return null; // ItemSystem places the boxes on its first Update after the set-up

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
        public IEnumerator Boxes_ArePlacedOnTheTrack()
        {
            Debug.Log("ItemBoxPlayTests: " + items.BoxCount + " boxes on " + race.TrackName);
            Assert.Greater(items.BoxCount, 0, "no item box found ground on this track (see the ItemSystem line in the Console)");
            for (int i = 0; i < items.BoxCount; i++)
            {
                Assert.IsTrue(items.BoxAt(i).IsAvailable, "every box is there at the start");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DrivingIntoABox_RollsAnItem_AndEUsesItAsABoost()
        {
            yield return StartAndWaitForGo();
            ItemBox box = items.BoxAt(0);

            // Into the box: its trigger must notice the real kart.
            Quaternion rotation = race.KartTransform.rotation;
            race.Kart.ResetTo(box.transform.position + Vector3.up * 50f, rotation);
            yield return WaitPhysics();
            race.Kart.ResetTo(box.transform.position, rotation);
            yield return WaitPhysics();
            Assert.IsFalse(box.IsAvailable, "the box broke when the kart drove into it");
            Assert.IsTrue(items.Slot.IsRolling, "the slot rolls after a box");
            Assert.IsFalse(items.UseItem(), "a rolling item cannot be used yet");

            yield return new WaitForSeconds(1.5f); // roll time is 1.1 s
            Assert.IsTrue(items.Slot.IsReady, "the item is ready after the roll");
            ItemKind held = items.Slot.Held;
            int chargesBefore = items.Slot.Charges;

            Assert.IsTrue(items.UseItem(), "E uses a ready item");
            int chargesLeft = items.Slot.Charges; // 0 = slot empty again (Bica, Onda), 2 = Triple Bica
            Assert.AreEqual(chargesBefore - 1, chargesLeft, held + " lost exactly one charge");
            if (race.KartTransform.GetComponent<IKartBoost>() != null)
            {
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(race.Kart.IsBoosting, "the item boost reached the kart through IKartBoost");
            }
            else
            {
                Debug.LogWarning("ItemBoxPlayTests: this kart has no IKartBoost yet (help PR for Role 1), boost not checked.");
            }
        }

        [UnityTest]
        public IEnumerator FullSlot_BreaksTheBox_ButKeepsTheItem()
        {
            yield return StartAndWaitForGo();
            items.OnBoxHit(items.BoxAt(0));
            ItemKind first = items.Slot.Held;
            Assert.AreNotEqual(ItemKind.None, first);
            int second = items.BoxCount > 1 ? 1 : 0;
            if (second == 0)
            {
                Assert.Ignore("only one box on this track");
            }
            items.OnBoxHit(items.BoxAt(second));
            Assert.IsFalse(items.BoxAt(second).IsAvailable, "the second box still breaks");
            Assert.AreEqual(first, items.Slot.Held, "the slot keeps its first item");
        }

        [UnityTest]
        public IEnumerator Box_ComesBack_AndRetryResetsEverything()
        {
            yield return StartAndWaitForGo();
            ItemBox box = items.BoxAt(0);
            items.OnBoxHit(box);
            Assert.IsFalse(box.IsAvailable);
            yield return new WaitForSeconds(3.3f); // respawn time is 3 s
            Assert.IsTrue(box.IsAvailable, "the box grew back");

            items.OnBoxHit(box);
            race.Retry();
            yield return null;
            Assert.IsTrue(box.IsAvailable, "retry puts every box back");
            Assert.IsTrue(items.Slot.IsEmpty, "retry empties the slot");
        }

        [UnityTest]
        public IEnumerator NoItems_BeforeTheRaceStarts()
        {
            Assert.AreEqual(RaceState.Ready, race.State);
            items.OnBoxHit(items.BoxAt(0));
            Assert.IsTrue(items.BoxAt(0).IsAvailable, "boxes do nothing on the start screen");
            Assert.IsTrue(items.Slot.IsEmpty);
            Assert.IsFalse(items.UseItem());
            yield return null;
        }

        private IEnumerator StartAndWaitForGo()
        {
            race.BeginRace();
            float waited = 0f;
            while (race.State != RaceState.Racing && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(RaceState.Racing, race.State, "GO after the countdown");
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
