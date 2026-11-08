using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests: Ready -> Countdown -> Racing -> Results, and that input is only on while racing.
    public class RaceStateMachineTests
    {
        [Test]
        public void StartsInReady_WithInputOff()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            Assert.AreEqual(RaceState.Ready, machine.State);
            Assert.IsFalse(machine.InputAllowed);
        }

        [Test]
        public void Countdown_Fires3_2_1_Go_ThenRacing()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            List<int> steps = new List<int>();
            machine.CountdownStep += number => steps.Add(number);

            machine.BeginCountdown(1f);
            Assert.AreEqual(RaceState.Countdown, machine.State);
            Assert.IsFalse(machine.InputAllowed, "no driving during the countdown");

            for (int frame = 0; frame < 40; frame++)
            {
                machine.Tick(0.1f); // 40 frames of 0.1 s = 4 s, more than the 3 s countdown
            }

            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, steps);
            Assert.AreEqual(RaceState.Racing, machine.State);
            Assert.IsTrue(machine.InputAllowed);
        }

        [Test]
        public void Countdown_IsNotOverEarly()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            machine.BeginCountdown(1f);
            machine.Tick(2.9f);
            Assert.AreEqual(RaceState.Countdown, machine.State);
            Assert.AreEqual(1, machine.CountdownNumber);
        }

        [Test]
        public void LongFrame_StillFiresEveryStep()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            List<int> steps = new List<int>();
            machine.CountdownStep += number => steps.Add(number);
            machine.BeginCountdown(1f);
            machine.Tick(10f); // one huge frame (e.g. the browser tab was in the background)
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, steps);
            Assert.AreEqual(RaceState.Racing, machine.State);
        }

        [Test]
        public void Finish_OnlyWorksWhileRacing()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            Assert.IsFalse(machine.Finish(), "cannot finish before the race started");

            machine.BeginCountdown(1f);
            Assert.IsFalse(machine.Finish(), "cannot finish during the countdown");

            machine.Tick(3f);
            Assert.IsTrue(machine.Finish());
            Assert.AreEqual(RaceState.Results, machine.State);
            Assert.IsFalse(machine.InputAllowed, "kart locked on the results screen");
            Assert.IsFalse(machine.Finish(), "no double finish");
        }

        [Test]
        public void Retry_FromResults_StartsANewCountdown()
        {
            RaceStateMachine machine = new RaceStateMachine(3);
            machine.BeginCountdown(1f);
            machine.Tick(3f);
            machine.Finish();

            machine.BeginCountdown(0.5f);
            Assert.AreEqual(RaceState.Countdown, machine.State);
            Assert.AreEqual(3, machine.CountdownNumber);
            machine.Tick(1.5f); // the retry countdown is quicker: 3 x 0.5 s
            Assert.AreEqual(RaceState.Racing, machine.State);
        }

        [Test]
        public void StateChanged_ReportsEveryPhase()
        {
            RaceStateMachine machine = new RaceStateMachine(1);
            List<RaceState> states = new List<RaceState>();
            machine.StateChanged += state => states.Add(state);
            machine.BeginCountdown(1f);
            machine.Tick(1f);
            machine.Finish();
            CollectionAssert.AreEqual(new[] { RaceState.Countdown, RaceState.Racing, RaceState.Results }, states);
        }

        [Test]
        public void BadSettings_CannotFreezeTheRace()
        {
            RaceStateMachine machine = new RaceStateMachine(0); // treated as 1
            machine.BeginCountdown(0f);                         // treated as 0.05 s
            machine.Tick(0.1f);
            Assert.AreEqual(RaceState.Racing, machine.State);
        }
    }
}
