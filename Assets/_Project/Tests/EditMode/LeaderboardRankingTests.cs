using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R10): sorting, ranking, ties and the top-10 cut, on the device list too.
    public class LeaderboardRankingTests
    {
        private static List<LeaderboardEntry> Board(params float[] seconds)
        {
            List<LeaderboardEntry> list = new List<LeaderboardEntry>();
            for (int i = 0; i < seconds.Length; i++)
            {
                list.Add(new LeaderboardEntry("P" + i, seconds[i]));
            }
            return list;
        }

        [Test]
        public void Insert_PutsTheTimeInOrder_AndReturnsThePlace()
        {
            List<LeaderboardEntry> board = Board(60f, 62f, 70f);
            Assert.AreEqual(2, LeaderboardRanking.Insert(board, new LeaderboardEntry("New", 61f), 10));
            Assert.AreEqual("New", board[1].name);
            Assert.AreEqual(1, LeaderboardRanking.Insert(board, new LeaderboardEntry("Fast", 50f), 10));
            Assert.AreEqual(6, LeaderboardRanking.Insert(board, new LeaderboardEntry("Slow", 99f), 10));
        }

        [Test]
        public void Insert_IntoAnEmptyList_IsFirst()
        {
            List<LeaderboardEntry> board = new List<LeaderboardEntry>();
            Assert.AreEqual(1, LeaderboardRanking.Insert(board, new LeaderboardEntry("Ana", 61f), 10));
            Assert.AreEqual(1, board.Count);
        }

        [Test]
        public void Tie_TheEarlierTimeStaysInFront()
        {
            List<LeaderboardEntry> board = Board(60f, 61.5f, 70f);
            Assert.AreEqual(3, LeaderboardRanking.Insert(board, new LeaderboardEntry("Same", 61.5f), 10));
            Assert.AreEqual("P1", board[1].name);
            Assert.AreEqual("Same", board[2].name);
        }

        [Test]
        public void Tie_CountsInWholeMilliseconds()
        {
            // 61.5001 shows as 1:01.500 like 61.5: the same time on screen is a tie, not "faster".
            List<LeaderboardEntry> board = Board(61.5f);
            Assert.AreEqual(2, LeaderboardRanking.Insert(board, new LeaderboardEntry("Almost", 61.4999f), 10));
        }

        [Test]
        public void Insert_KeepsOnlyTheTop10()
        {
            List<LeaderboardEntry> board = Board(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            Assert.AreEqual(0, LeaderboardRanking.Insert(board, new LeaderboardEntry("Eleventh", 11f), 10), "not in the top 10");
            Assert.AreEqual(10, board.Count);
            Assert.AreEqual(10f, board[9].seconds);

            Assert.AreEqual(10, LeaderboardRanking.Insert(board, new LeaderboardEntry("Tenth", 9.5f), 10));
            Assert.AreEqual(10, board.Count, "the old 10th place drops out");
            Assert.AreEqual("Tenth", board[9].name);
        }

        [Test]
        public void Insert_TiedWithTheLastPlace_DoesNotGetIn()
        {
            List<LeaderboardEntry> board = Board(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            Assert.AreEqual(0, LeaderboardRanking.Insert(board, new LeaderboardEntry("Tied", 10f), 10), "you have to be faster to pass someone");
            Assert.AreEqual("P9", board[9].name);
        }

        [Test]
        public void Sorted_SortsMixedRows_KeepsTieOrder_AndCuts()
        {
            List<LeaderboardEntry> rows = new List<LeaderboardEntry>
            {
                new LeaderboardEntry("C", 70f), new LeaderboardEntry("A", 60f), new LeaderboardEntry("B1", 65f),
                new LeaderboardEntry("B2", 65f), new LeaderboardEntry("D", 80f),
            };
            List<LeaderboardEntry> sorted = LeaderboardRanking.Sorted(rows, 4);
            Assert.AreEqual(4, sorted.Count);
            Assert.AreEqual("A", sorted[0].name);
            Assert.AreEqual("B1", sorted[1].name);
            Assert.AreEqual("B2", sorted[2].name);
            Assert.AreEqual("C", sorted[3].name);
            Assert.AreEqual(5, rows.Count, "the input list is not changed");
        }

        [Test]
        public void Sorted_EmptyAndZeroKeep()
        {
            Assert.AreEqual(0, LeaderboardRanking.Sorted(new List<LeaderboardEntry>(), 10).Count);
            Assert.AreEqual(0, LeaderboardRanking.Sorted(Board(60f), 0).Count);
        }

        [Test]
        public void WithoutTooFast_DropsImpossibleTimesForThisTrack()
        {
            List<LeaderboardEntry> kept = LeaderboardRanking.WithoutTooFast(Board(5.1f, 24f, 61f), 24f);
            Assert.AreEqual(2, kept.Count);
            Assert.AreEqual(24f, kept[0].seconds);
        }

        [Test]
        public void RankOf_FindsNameAndTime_IgnoringCase()
        {
            List<LeaderboardEntry> board = new List<LeaderboardEntry>
            {
                new LeaderboardEntry("Ana", 60f), new LeaderboardEntry("Rui", 61f), new LeaderboardEntry("Ana", 62f),
            };
            Assert.AreEqual(3, LeaderboardRanking.RankOf(board, "ANA", 62f));
            Assert.AreEqual(1, LeaderboardRanking.RankOf(board, "ana", 60f));
            Assert.AreEqual(0, LeaderboardRanking.RankOf(board, "Ana", 61f), "Rui's time, not Ana's");
            Assert.AreEqual(0, LeaderboardRanking.RankOf(board, "Zoe", 60f));
        }

        [Test]
        public void LocalList_SavesAndLoads_SortedAndCut()
        {
            LocalLeaderboardList list = new LocalLeaderboardList();
            Assert.AreEqual(1, list.Add("Ana", 62f));
            Assert.AreEqual(1, list.Add("Rui", 61f));
            Assert.AreEqual(3, list.Add("Zoe", 63f));
            LocalLeaderboardList loaded = LocalLeaderboardList.FromSaveString(list.ToSaveString());
            List<LeaderboardEntry> top = loaded.Top(10);
            Assert.AreEqual(3, top.Count);
            Assert.AreEqual("Rui", top[0].name);
            Assert.AreEqual("Ana", top[1].name);
            Assert.AreEqual(2, loaded.Top(2).Count);
        }

        [Test]
        public void LocalList_KeepsTenPerTrack()
        {
            LocalLeaderboardList list = new LocalLeaderboardList();
            for (int i = 0; i < 12; i++)
            {
                list.Add("P" + i, 60f + i);
            }
            Assert.AreEqual(10, list.Count);
            Assert.AreEqual(0, list.Add("Slowest", 99f));
            Assert.AreEqual(1, list.Add("Fastest", 50f));
            Assert.AreEqual(10, list.Count);
        }

        [Test]
        public void LocalList_BrokenOrEditedSave_IsReadSafely()
        {
            Assert.AreEqual(0, LocalLeaderboardList.FromSaveString(null).Count);
            Assert.AreEqual(0, LocalLeaderboardList.FromSaveString("").Count);
            Assert.AreEqual(0, LocalLeaderboardList.FromSaveString("not json at all {").Count);
            Assert.AreEqual(0, LocalLeaderboardList.FromSaveString("{\"rows\":5}").Count);

            // A hand-edited save: the good row stays, a bad name, an impossible time and a missing name are dropped.
            string edited = "{\"rows\":[{\"name\":\"Ana\",\"seconds\":61.2},{\"name\":\"<b>x</b>\",\"seconds\":60},"
                + "{\"name\":\"Rui\",\"seconds\":1.5},{\"seconds\":62}]}";
            LocalLeaderboardList list = LocalLeaderboardList.FromSaveString(edited);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("Ana", list.Top(10)[0].name);
        }

        [Test]
        public void LocalList_Top_IsACopy()
        {
            LocalLeaderboardList list = new LocalLeaderboardList();
            list.Add("Ana", 61f);
            list.Top(10)[0].name = "Changed";
            Assert.AreEqual("Ana", list.Top(10)[0].name);
        }
    }
}
