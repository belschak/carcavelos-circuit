using System.Collections.Generic;
using CarcavelosCircuit.Online;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The room results card and RaceHud's local leaderboard (QA 17 Nov 2026): online the local board stays empty and
    // the room results card sat on top of it, so the board is invisible (space kept) while the room results show.
    public class OnlineLobbyViewTests
    {
        private static VisualElement DocumentWithBoard(out VisualElement board)
        {
            VisualElement root = new VisualElement();
            board = new VisualElement { name = OnlineLobbyView.LocalBoardName };
            root.Add(board);
            return root;
        }

        private static List<FinishRow> TwoRows()
        {
            List<FinishRow> rows = new List<FinishRow>();
            rows.Add(new FinishRow { Id = 1, Name = "Ana", State = FinishState.Finished, Seconds = 67.4, Place = 1 });
            rows.Add(new FinishRow { Id = 2, Name = "Bea", State = FinishState.Finished, Seconds = 69.1, Place = 2 });
            return rows;
        }

        [Test]
        public void RoomResults_HideTheLocalBoard_KeepItsSpace()
        {
            VisualElement board;
            OnlineLobbyView view = new OnlineLobbyView(DocumentWithBoard(out board));
            view.ShowResults(TwoRows(), 1, true, "", "ROOM RESULTS");
            Assert.IsTrue(view.HidesLocalBoard);
            Assert.AreEqual(Visibility.Hidden, board.style.visibility.value, "invisible, not display none: the results card keeps its place");
            Assert.AreNotEqual(DisplayStyle.None, board.style.display.value);
        }

        [Test]
        public void LeavingTheResults_BringsTheBoardBack()
        {
            VisualElement board;
            OnlineLobbyView view = new OnlineLobbyView(DocumentWithBoard(out board));
            view.ShowResults(TwoRows(), 1, false, "WAITING FOR THE HOST", "ROOM RESULTS");
            view.Hide(); // the next race starts, or the player left
            Assert.IsFalse(view.HidesLocalBoard);
            Assert.AreEqual(StyleKeyword.Null, board.style.visibility.keyword, "back to what RaceHud's style sheet says");
        }

        [Test]
        public void OtherPages_LeaveTheBoardAlone()
        {
            VisualElement board;
            OnlineLobbyView view = new OnlineLobbyView(DocumentWithBoard(out board));
            view.ShowResults(TwoRows(), 1, true, "", "ROOM RESULTS");
            view.ShowMessage(OnlineRooms.FullMessage()); // results -> message page: the board comes back
            Assert.IsFalse(view.HidesLocalBoard);
            Assert.AreEqual(StyleKeyword.Null, board.style.visibility.keyword);
            view.ShowConnecting("CONNECTING...");
            Assert.IsFalse(view.HidesLocalBoard);
        }

        [Test]
        public void RoomResults_HideTheCheckpointBar_AndBringItBack()
        {
            VisualElement board;
            VisualElement root = DocumentWithBoard(out board);
            VisualElement checkpoints = new VisualElement { name = OnlineLobbyView.CheckpointPanelName };
            root.Add(checkpoints);
            OnlineLobbyView view = new OnlineLobbyView(root);
            view.ShowResults(TwoRows(), 1, false, "WAITING FOR THE HOST", "ROOM RESULTS");
            Assert.IsTrue(view.HidesCheckpoints);
            Assert.AreEqual(Visibility.Hidden, checkpoints.style.visibility.value, "the card's top no longer shows CHECKPOINT 13/13 through it");
            view.Hide();
            Assert.IsFalse(view.HidesCheckpoints);
            Assert.AreEqual(StyleKeyword.Null, checkpoints.style.visibility.keyword, "back to what RaceHud says");
        }

        [Test]
        public void TimeUpResults_HideTheStartScreenBoard_AndBringItBack()
        {
            // Time up: the DNF player is back on the start screen, whose leaderboard showed through the room results
            // card (live QA 18 Nov 2026).
            VisualElement board;
            VisualElement root = DocumentWithBoard(out board);
            VisualElement startBoard = new VisualElement { name = OnlineLobbyView.StartBoardName };
            root.Add(startBoard);
            OnlineLobbyView view = new OnlineLobbyView(root);
            view.ShowResults(TwoRows(), 2, false, "WAITING FOR THE HOST", "TIME UP: ROOM RESULTS");
            Assert.IsTrue(view.HidesStartBoard);
            Assert.AreEqual(Visibility.Hidden, startBoard.style.visibility.value, "no LEADERBOARD TOP 10 under the room results");
            view.Hide();
            Assert.IsFalse(view.HidesStartBoard);
            Assert.AreEqual(StyleKeyword.Null, startBoard.style.visibility.keyword, "the start screen board is back");
        }

        [Test]
        public void NoBoardInTheDocument_NoProblem()
        {
            OnlineLobbyView view = new OnlineLobbyView(new VisualElement());
            view.ShowResults(TwoRows(), 2, false, "", "TIME UP: ROOM RESULTS");
            Assert.IsFalse(view.HidesLocalBoard, "nothing to hide");
            view.Hide();
            Assert.IsFalse(view.HidesLocalBoard);
        }
        // Phones (QA review 17 Nov 2026): the lobby showed ENTER / T / Q keycaps and 56 px buttons on touch screens.
        private static int CountClass(VisualElement element, string className)
        {
            int count = element.ClassListContains(className) ? 1 : 0;
            foreach (VisualElement child in element.Children())
            {
                count += CountClass(child, className);
            }
            return count;
        }

        private static float TallestPill(VisualElement element)
        {
            float tallest = element.ClassListContains("menu-pill") && element.style.height.keyword == StyleKeyword.Undefined
                ? element.style.height.value.value : 0f;
            foreach (VisualElement child in element.Children())
            {
                tallest = System.Math.Max(tallest, TallestPill(child));
            }
            return tallest;
        }

        [Test]
        public void TouchScreen_NoKeyHints_ThumbSizedButtons()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, true);
            view.ShowMessage(OnlineRooms.FullMessage());
            Assert.AreEqual(0, CountClass(root, "keycap"), "no Q BACK keycap on a phone");
            Assert.AreEqual(OnlineLobbyView.TouchPillHeight, TallestPill(root), "BACK is thumb-sized");
            view.ShowResults(TwoRows(), 1, true, "", "ROOM RESULTS");
            Assert.AreEqual(0, CountClass(root, "keycap"), "no ENTER AGAIN / Q LEAVE keycaps on a phone");
            Assert.AreEqual(OnlineLobbyView.TouchPillHeight, TallestPill(root), "START AGAIN is thumb-sized");
        }

        [Test]
        public void Keyboard_KeepsKeyHints_AndDesktopButtons()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, false);
            view.ShowMessage(OnlineRooms.FullMessage());
            Assert.AreEqual(1, CountClass(root, "keycap"), "Q BACK");
            Assert.AreEqual(56f, TallestPill(root));
        }
        [Test]
        public void TouchScreen_NameFieldThumbSized()
        {
            VisualElement root = new VisualElement();
            OnlineLobbyView view = new OnlineLobbyView(root, true);
            view.ShowRooms("Ana", "", CarcavelosCircuit.Shared.TimeOfDay.Noon);
            TextField field = root.Q<TextField>();
            Assert.IsNotNull(field);
            Assert.AreEqual(OnlineLobbyView.TouchNameFieldHeight, field.style.height.value.value);
            Assert.AreEqual(0, CountClass(root, "keycap"), "no ENTER JOIN / Q BACK on a phone");
        }
        // Leaving the room was only the Q key: no way out on a phone or for a mouse-only player (QA 17 Nov 2026).
        [Test]
        public void RoomPageAndResults_HaveALeaveButton_OnTouchAndDesktop()
        {
            for (int t = 0; t < 2; t++)
            {
                bool touchScreen = t == 1;
                VisualElement root = new VisualElement();
                OnlineLobbyView view = new OnlineLobbyView(root, touchScreen);
                List<OnlinePlayer> players = new List<OnlinePlayer>();
                players.Add(new OnlinePlayer(1, "Ana", 0, 0.0));
                players.Add(new OnlinePlayer(2, "Bea", 1, 0.0));
                view.ShowRoom(players, 2, 1, "WAITING FOR THE HOST", false, CarcavelosCircuit.Shared.TimeOfDay.Noon);
                Assert.IsNotNull(root.Q<VisualElement>("online-card").Q<VisualElement>("online-leave"), "room page, touch=" + touchScreen);
                view.ShowResults(TwoRows(), 2, false, "WAITING FOR THE HOST", "ROOM RESULTS");
                Assert.IsNotNull(root.Q<VisualElement>("online-results").Q<VisualElement>("online-leave"), "room results card, touch=" + touchScreen);
            }
        }
    }
}
