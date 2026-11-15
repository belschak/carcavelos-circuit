using System.Collections;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CarcavelosCircuit.Tests.PlayMode
{
    // Task V34 (Role 3 effects, Role 4 hooks): in the real game (Main.unity) the item boxes and Seagull rings wear
    // Role 3's looks, and using an item shows its effect round the player's kart. Bots removed: items are the
    // player's only (O2), and the checks look at the player's kart.
    public class ItemFxPlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;

        private RaceManager race;
        private ItemSystem items;
        private ItemFx fx;

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
            Assert.IsTrue(race != null && race.IsSetUp, "the race was not set up in time");
            items = Object.FindAnyObjectByType<ItemSystem>();
            yield return BotsOffForTests.RemoveFromRace(SetUpTimeoutSeconds);
            yield return null; // ItemSystem places the boxes on its first Update after the set-up
            fx = race.KartTransform.GetComponentInChildren<ItemFx>();
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boxes_WearTheAzulejoLook_AndAGroundLight()
        {
            Assert.Greater(items.BoxCount, 0);
            ItemBox box = items.BoxAt(0);
            Transform cube = box.transform.Find("Cube");
            Assert.IsNotNull(cube, "ItemBox makes its spinning cube");
            Assert.IsFalse(cube.GetComponent<MeshRenderer>().enabled, "the plain cube is hidden, the look draws the box");
            Assert.IsNotNull(cube.GetComponentInChildren<MeshFilter>(), "the rounded azulejo body hangs in the cube");
            Assert.IsNotNull(box.GetComponentInChildren<ItemBoxLookFx>(), "the light on the road under the box");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerKart_HasItemFx_AndTheWaveRisesRoundIt()
        {
            Assert.IsNotNull(fx, "KartEffects carries ItemFx onto the player's kart");
            yield return StartAndWaitForGo();
            Assert.IsTrue(items.Slot.TryStartRoll(ItemKind.CarcavelosWave, 0f));
            Assert.IsTrue(items.UseItem());
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(fx.WaveRiding, "the wave is up round the kart");
            Renderer water = fx.transform.Find("WaveRide/Water").GetComponent<Renderer>();
            Assert.IsTrue(water.enabled, "the water is drawn");
            Vector3 centre = water.bounds.center;
            Assert.Less(Vector3.Distance(centre, race.KartTransform.position), 2.5f, "the water is round the kart");
            Assert.Greater(water.bounds.size.x, 2.5f, "wider than the kart: it surrounds it");

            race.Retry();
            yield return null;
            Assert.IsFalse(fx.WaveRiding, "retry clears the wave");
            Assert.IsFalse(water.enabled);
        }

        [UnityTest]
        public IEnumerator SeagullRings_AreLucsLifebuoys()
        {
            yield return StartAndWaitForGo();
            Assert.IsTrue(items.Slot.TryStartRoll(ItemKind.SeagullDelivery, 0f));
            Assert.IsTrue(items.UseItem());
            Assume.That(items.RingCount, Is.GreaterThan(0), "no road ahead for rings here");
            Transform buoy = items.RingAt(0).transform.Find("Lifebuoy");
            Assert.IsNotNull(buoy);
            Assert.AreEqual(1, buoy.childCount, "one lifebuoy look instead of eight cylinders");
        }

        [UnityTest]
        public IEnumerator DeansList_NoCoinRing_WhenRole3DrawsTheWreath()
        {
            yield return StartAndWaitForGo();
            Assert.IsTrue(items.Slot.TryStartRoll(ItemKind.DeansList, 0f));
            Assert.IsTrue(items.UseItem());
            yield return null;
            Assert.IsNull(items.transform.Find("DeansHalo"), "the coin ring is off (ItemSettings > Coin Halo)");
            Assert.IsTrue(items.ShieldActive);
            yield return new WaitForSeconds(0.5f);
            Transform wreath = fx.transform.Find("DeansFx/Laurel");
            Assert.IsNotNull(wreath);
            Assert.IsTrue(wreath.gameObject.activeInHierarchy, "the laurel wreath shows while the shield is on");
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
    }
}
