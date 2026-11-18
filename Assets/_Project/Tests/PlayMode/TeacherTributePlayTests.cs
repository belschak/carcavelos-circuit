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
    // Task P4 (teacher tribute) in the real game (Main.unity + the campus world it loads):
    // - one thank-you board per teacher stands beside a checkpoint gate (never the start gate), without colliders,
    // - the results card shows the Gil Pereira Cup line for a gold or Dean's List medal, and hides it otherwise.
    public class TeacherTributePlayTests
    {
        private const float SetUpTimeoutSeconds = 60f;
        private const float TributeTimeoutSeconds = 5f;

        private RaceManager race;

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
            Assert.IsTrue(race != null && race.IsSetUp, "the race was not set up within " + SetUpTimeoutSeconds + " s");
            race.PauseOnFocusLoss = false;
        }

        [UnityTest]
        public IEnumerator Boards_OnePerTeacher_BesideCheckpointGates_NoColliders()
        {
            List<Transform> boards = Boards();
            float waited = 0f;
            while (boards.Count < TributeRules.Teachers.Length && waited < TributeTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
                boards = Boards();
            }
            Assert.AreEqual(TributeRules.Teachers.Length, boards.Count, "thank-you boards in the race");
            var gates = new HashSet<int>();
            foreach (Transform board in boards)
            {
                int gate = TributeRules.GateNumber(board.parent != null ? board.parent.name : "");
                Assert.Greater(gate, 0, board.parent + ": a board stands at a checkpoint gate, never the start line");
                Assert.IsTrue(gates.Add(gate), "two boards at gate " + gate);
                Assert.IsEmpty(board.GetComponentsInChildren<Collider>(), "a board never stops a kart");
                Renderer names = board.Find("Names").GetComponent<Renderer>();
                Assert.IsNotNull(names.sharedMaterial, "the board shows the names picture");
                Assert.AreEqual("TributeBoards", names.sharedMaterial.mainTexture.name);
            }
        }

        [UnityTest]
        public IEnumerator CupLine_GoldOrDeansList_Shown_OtherMedals_Hidden()
        {
            VisualElement badge = null;
            Label cup = null;
            float waited = 0f;
            while (cup == null && waited < TributeTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
                foreach (UIDocument document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                {
                    VisualElement root = document.rootVisualElement;
                    if (root != null && root.Q<Label>("res-cup") != null)
                    {
                        badge = root.Q<VisualElement>("res-medal-badge");
                        cup = root.Q<Label>("res-cup");
                    }
                }
            }
            Assert.IsNotNull(cup, "the cup line was added to the results card");
            Assert.AreEqual(TributeRules.CupLine, cup.text);

            string[] medalClasses = { "medal--none", "medal--bronze", "medal--silver", "medal--gold", "medal--deans" };
            string before = null;
            foreach (string c in medalClasses)
            {
                if (badge.ClassListContains(c))
                {
                    before = c;
                }
            }
            try
            {
                yield return ShowMedal(badge, medalClasses, "medal--gold");
                Assert.AreEqual(DisplayStyle.Flex, cup.style.display.value, "gold wins the cup");
                yield return ShowMedal(badge, medalClasses, "medal--deans");
                Assert.AreEqual(DisplayStyle.Flex, cup.style.display.value, "the Dean's List wins the cup too");
                yield return ShowMedal(badge, medalClasses, "medal--silver");
                Assert.AreEqual(DisplayStyle.None, cup.style.display.value, "silver does not");
                yield return ShowMedal(badge, medalClasses, "medal--none");
                Assert.AreEqual(DisplayStyle.None, cup.style.display.value, "no medal does not");
            }
            finally
            {
                foreach (string c in medalClasses)
                {
                    badge.RemoveFromClassList(c);
                }
                badge.AddToClassList(before ?? "medal--none");
            }
        }

        // The badge as RaceHud sets it at the finish (MedalRules.ColourClass), then two frames for TeacherTribute
        // and the panel's layout.
        private static IEnumerator ShowMedal(VisualElement badge, string[] all, string shown)
        {
            foreach (string c in all)
            {
                badge.RemoveFromClassList(c);
            }
            badge.AddToClassList(shown);
            yield return null;
            yield return null;
        }

        private static List<Transform> Boards()
        {
            var found = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "Teacher Board")
                {
                    found.Add(t);
                }
            }
            return found;
        }
    }
}
