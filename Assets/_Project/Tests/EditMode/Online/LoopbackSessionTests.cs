using System.Collections.Generic;
using CarcavelosCircuit.Online;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The whole online flow over the in-process transport (M2): two sessions in one room, master election, the
    // master's control reaching everyone, poses, finishes, leaving (= DNF for the director), the 15 player cap.
    public class LoopbackSessionTests
    {
        private double clock;

        [SetUp]
        public void FreshHub()
        {
            LoopbackHub.Reset();
            clock = 100.0;
            LoopbackHub.Clock = () => clock;
        }

        [TearDown]
        public void DropHub()
        {
            LoopbackHub.Reset();
        }

        [Test]
        public void TwoSessions_ShareOneRoom_InJoinOrder()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join(OnlineRooms.SessionName, "Ana", 0, 2);
            clock += 1.0;
            b.Join(OnlineRooms.SessionName, "Bo", 1, 3);
            Assert.AreEqual(OnlineStatus.InRoom, a.Status);
            Assert.AreEqual(OnlineStatus.InRoom, b.Status);
            Assert.AreEqual(2, a.Players.Count);
            Assert.AreEqual(2, b.Players.Count);
            Assert.AreEqual(a.LocalId, a.Players[0].Id, "join order: Ana first on every screen");
            Assert.AreEqual(a.LocalId, b.Players[0].Id);
            Assert.AreEqual("Bo", a.Players[1].Name);
            Assert.AreEqual(1, a.Players[1].ColourIndex);
            Assert.AreEqual(3, a.Players[1].DriverIndex, "Bo's driver travels with Bo: Ana's screen builds Paulo's kart for Bo");
            Assert.AreEqual(2, b.Players[0].DriverIndex, "and Bo's screen builds Nova's kart for Ana");
            Assert.AreNotEqual(a.LocalId, b.LocalId);
            Assert.AreEqual(101.0, a.Now, 0.0, "the shared clock");
        }

        [Test]
        public void DifferentRooms_DoNotSeeEachOther()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("room-a", "Ana", 0, 0);
            b.Join("room-b", "Bo", 1, 0);
            Assert.AreEqual(1, a.Players.Count);
            Assert.AreEqual(1, b.Players.Count);
            Assert.IsTrue(a.IsMaster);
            Assert.IsTrue(b.IsMaster);
        }

        [Test]
        public void Master_IsTheEarliestJoin_AndMovesOnWhenItLeaves()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            LoopbackSession c = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            b.Join("r", "Bo", 1, 0);
            c.Join("r", "Cid", 2, 0);
            Assert.IsTrue(a.IsMaster);
            Assert.IsFalse(b.IsMaster);
            Assert.IsFalse(c.IsMaster);
            a.Leave();
            Assert.IsTrue(b.IsMaster, "the next in join order takes over");
            Assert.IsFalse(c.IsMaster);
            Assert.IsFalse(a.IsMaster, "a session outside the room is never master");
            Assert.AreEqual(OnlineStatus.Idle, a.Status);
            Assert.AreEqual(0, a.Players.Count);
        }

        [Test]
        public void Control_FromTheMaster_ReachesEveryone_OthersAreIgnored()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            b.Join("r", "Bo", 1, 0);
            int changedOnB = 0;
            b.Changed += () => changedOnB++;

            RaceControl start = new RaceControl(1, OnlinePhase.Countdown, OnlineStartRules.StartAtFor(a.Now), new[] { a.LocalId, b.LocalId });
            a.MasterSetControl(start);
            Assert.IsTrue(b.Control.SameAs(start), "B sees the same control");
            Assert.AreEqual(1, changedOnB);
            Assert.AreEqual(1, b.Control.IndexOf(b.LocalId), "B's grid slot is its roster index");

            b.MasterSetControl(new RaceControl(2, OnlinePhase.Countdown, 0.0, new[] { b.LocalId }));
            Assert.AreEqual(1, a.Control.RaceId, "a non-master's control is dropped");
            Assert.AreEqual(1, changedOnB, "and nobody hears about it");
        }

        [Test]
        public void Poses_ReachTheOthers_NotTheSender()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            b.Join("r", "Bo", 1, 0);
            List<int> seenByA = new List<int>();
            List<int> seenByB = new List<int>();
            a.PoseReceived += (id, pose) => seenByA.Add(id);
            b.PoseReceived += (id, pose) => seenByB.Add(id);

            KartPose pose = new KartPose();
            pose.Time = a.Now;
            pose.Position = new Vector3(1f, 2f, 3f);
            pose.Rotation = Quaternion.identity;
            a.SendPose(pose);
            Assert.AreEqual(0, seenByA.Count, "my own pose does not come back");
            Assert.AreEqual(1, seenByB.Count);
            Assert.AreEqual(a.LocalId, seenByB[0]);
            Assert.IsTrue(b.Players[0].HasPose);
            Assert.AreEqual(3f, b.Players[0].LatestPose.Position.z, 0.001f, "the newest pose is kept on the player");
        }

        [Test]
        public void Finish_IsSeenByEveryone_OnlyOncePerRace()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            b.Join("r", "Bo", 1, 0);
            a.MasterSetControl(new RaceControl(1, OnlinePhase.Countdown, 104.0, new[] { a.LocalId, b.LocalId }));
            int changedOnA = 0;
            a.Changed += () => changedOnA++;

            b.ReportFinish(1, 65.5);
            Assert.AreEqual(1, changedOnA);
            Assert.IsTrue(a.Players[1].HasFinished(1));
            Assert.AreEqual(65.5, a.Players[1].FinishSeconds, 0.0);
            b.ReportFinish(1, 60.0);
            Assert.AreEqual(65.5, a.Players[1].FinishSeconds, 0.0, "the first report per race counts");
            Assert.AreEqual(1, changedOnA, "a doubled report changes nothing");
            b.ReportFinish(2, 60.0);
            Assert.IsTrue(a.Players[1].HasFinished(2), "a new race id is a new finish");
            Assert.IsFalse(a.Players[1].HasFinished(1), "the old race is over");
            b.ReportFinish(3, double.NaN);
            Assert.IsFalse(a.Players[1].HasFinished(3), "a bad time is refused");
        }

        [Test]
        public void Leave_IsADnfForTheDirector()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            b.Join("r", "Bo", 1, 0);
            RaceControl race = new RaceControl(1, OnlinePhase.Countdown, 104.0, new[] { a.LocalId, b.LocalId });
            a.MasterSetControl(race);
            b.Leave();
            Assert.AreEqual(1, a.Players.Count, "B is gone from the room");
            Assert.IsTrue(a.Control.Contains(b.LocalId), "but still in the race's roster");
            // What the director does with it: B is in the roster and not present = DNF.
            List<FinishEntry> entries = new List<FinishEntry>
            {
                new FinishEntry(a.LocalId, "Ana", true, false, double.NaN),
                new FinishEntry(b.LocalId, "Bo", false, false, double.NaN),
            };
            List<FinishRow> rows = FinishOrderRules.Order(entries);
            Assert.AreEqual(FinishState.Dnf, rows[1].State);
        }

        [Test]
        public void Cap_SixteenthPlayerIsTold_RoomFull()
        {
            List<LoopbackSession> sessions = new List<LoopbackSession>();
            for (int i = 0; i < OnlineRooms.MaxPlayers; i++)
            {
                LoopbackSession s = new LoopbackSession();
                s.Join("r", "P" + i, i, 0);
                sessions.Add(s);
                Assert.AreEqual(OnlineStatus.InRoom, s.Status);
            }
            LoopbackSession late = new LoopbackSession();
            late.Join("r", "Late", 0, 0);
            Assert.AreEqual(OnlineStatus.Full, late.Status);
            Assert.AreEqual(0, late.Players.Count);
            Assert.AreEqual(OnlineRooms.MaxPlayers, sessions[0].Players.Count, "the room did not grow");
            sessions[3].Leave();
            LoopbackSession next = new LoopbackSession();
            next.Join("r", "Next", 0, 0);
            Assert.AreEqual(OnlineStatus.InRoom, next.Status, "a freed seat can be taken");
        }

        [Test]
        public void BadJoins_FailCleanly()
        {
            LoopbackSession a = new LoopbackSession();
            a.Join("", "Ana", 0, 0);
            Assert.AreEqual(OnlineStatus.Failed, a.Status);
            Assert.AreNotEqual("", a.FailReason);
            a.Leave();
            Assert.AreEqual(OnlineStatus.Idle, a.Status);
            a.Join("r", null, 0, 0);
            Assert.AreEqual(OnlineStatus.InRoom, a.Status);
            Assert.AreEqual("", a.Players[0].Name, "a missing name becomes empty, never null");
            a.Join("r", "Twice", 0, 0);
            Assert.AreEqual(1, a.Players.Count, "joining again while in a room does nothing");
            a.Dispose();
            Assert.AreEqual(OnlineStatus.Idle, a.Status, "dispose = leave");
            Assert.AreEqual(0, LoopbackHub.CountIn("r"));
        }

        [Test]
        public void Changed_FiresAfterTheStateIsAlreadyNew()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join("r", "Ana", 0, 0);
            int playersSeenInHandler = -1;
            a.Changed += () => playersSeenInHandler = a.Players.Count;
            b.Join("r", "Bo", 1, 0);
            Assert.AreEqual(2, playersSeenInHandler, "the handler already sees the newcomer");
        }

        [Test]
        public void DriverOutsideTheRoster_IsTheRookie()
        {
            LoopbackSession a = new LoopbackSession();
            LoopbackSession b = new LoopbackSession();
            a.Join(OnlineRooms.SessionName, "Ana", 0, 99);
            b.Join(OnlineRooms.SessionName, "Bo", 1, -4);
            Assert.AreEqual(0, b.Players[0].DriverIndex, "a broken or old client's number never breaks the kart");
            Assert.AreEqual(0, a.Players[1].DriverIndex);
        }

        [Test]
        public void OneRoom_TheWholeClassMeets()
        {
            // Feedback round 17 Nov 2026: one room for everyone, 15 seats, each player with their own driver.
            LoopbackSession[] sessions = new LoopbackSession[OnlineRooms.MaxPlayers];
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i] = new LoopbackSession();
                sessions[i].Join(OnlineRooms.SessionName, "P" + i, i, i % 4);
            }
            Assert.AreEqual(OnlineRooms.MaxPlayers, sessions[0].Players.Count, "all 15 in one room");
            for (int i = 0; i < sessions.Length; i++)
            {
                Assert.AreEqual(OnlineStatus.InRoom, sessions[i].Status);
                Assert.AreEqual(i % 4, sessions[0].Players[i].DriverIndex);
            }
            LoopbackSession sixteenth = new LoopbackSession();
            sixteenth.Join(OnlineRooms.SessionName, "Late", 0, 1);
            Assert.AreEqual(OnlineStatus.Full, sixteenth.Status, "the 16th waits for the next race");
        }
}
}
