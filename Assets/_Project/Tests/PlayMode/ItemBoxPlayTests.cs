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
            yield return BotsOffForTests.RemoveFromRace(SetUpTimeoutSeconds); // the item rules on an empty road
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
            int chargesLeft = items.Slot.Charges; // 0 = slot empty again (every item is one use)
            Assert.AreEqual(chargesBefore - 1, chargesLeft, held + " lost exactly one charge");
            if (held == ItemKind.SeagullDelivery)
            {
                Assert.Greater(items.RingsWaiting, 0, "the Seagull dropped rings (its boost comes from the rings)");
            }
            else if (race.KartTransform.GetComponent<IKartBoost>() != null)
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

        [UnityTest]
        public IEnumerator EveryItem_CanBeUsed()
        {
            yield return StartAndWaitForGo();
            ItemKind[] all = { ItemKind.Espresso, ItemKind.SeagullDelivery, ItemKind.CarcavelosWave, ItemKind.DeansList };
            for (int i = 0; i < all.Length; i++)
            {
                Assert.IsTrue(items.Slot.TryStartRoll(all[i], 0f), all[i] + " goes into the empty slot");
                Assert.IsTrue(items.UseItem(), all[i] + " can be used");
                Assert.IsTrue(items.Slot.IsEmpty, all[i] + " empties the slot");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SeagullDelivery_DropsRings_AndARingBoostsTheKart()
        {
            yield return StartAndWaitForGo();
            items.Slot.TryStartRoll(ItemKind.SeagullDelivery, 0f);
            Assert.IsTrue(items.UseItem());
            Debug.Log("ItemBoxPlayTests: the gull dropped " + items.RingCount + " rings on " + race.TrackName);
            Assert.Greater(items.RingCount, 0, "at least one ring found road ahead of the kart");
            Assert.AreEqual(items.RingCount, items.RingsWaiting, "no ring is collected before the kart gets there");

            yield return new WaitForSeconds(0.8f); // the rings pop in one after the other
            SeagullRing ring = items.RingAt(0);
            Quaternion rotation = race.KartTransform.rotation;
            race.Kart.ResetTo(ring.transform.position + Vector3.up * 50f, rotation);
            yield return WaitPhysics();
            race.Kart.ResetTo(ring.transform.position, rotation);
            yield return WaitPhysics();
            Assert.IsTrue(ring == null || ring.IsCollected, "the kart drove through the ring");
            Assert.AreEqual(items.RingCount - 1, items.RingsWaiting, "one ring less on the road");
            if (race.KartTransform.GetComponent<IKartBoost>() != null)
            {
                Assert.IsTrue(race.Kart.IsBoosting, "the ring boost reached the kart through IKartBoost");
            }

            race.Retry();
            yield return null;
            Assert.AreEqual(0, items.RingsWaiting, "retry takes every ring away");
        }

        [UnityTest]
        public IEnumerator DeansList_ShieldIsOn_AndRetryClearsIt()
        {
            yield return StartAndWaitForGo();
            items.Slot.TryStartRoll(ItemKind.DeansList, 0f);
            Assert.IsTrue(items.UseItem());
            Assert.IsTrue(items.ShieldActive, "the shield is on after Dean's List");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(items.ShieldActive, "driving on without a bump keeps the shield");

            race.Recover(); // speed to 0 on purpose: must not count as a bump
            yield return null;
            yield return null;
            Assert.IsTrue(items.ShieldActive, "a recover is not a bump");

            race.Retry();
            yield return null;
            Assert.IsFalse(items.ShieldActive, "retry switches the shield off");
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
