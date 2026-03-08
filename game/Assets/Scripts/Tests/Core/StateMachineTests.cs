using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Core;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class StateMachineTests
    {
        private enum Phase { Idle, Running, Done }

        private class MockState : IState<Phase>
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public Phase LastEnterPrevious;
            public Phase LastEnterCurrent;
            public float LastTickDelta;

            public void Enter(Phase previousState, Phase currentState)
            {
                EnterCount++;
                LastEnterPrevious = previousState;
                LastEnterCurrent = currentState;
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
                LastTickDelta = deltaTime;
            }

            public void Exit(Phase currentState, Phase nextState)
            {
                ExitCount++;
            }
        }

        private StateMachine<Phase> _sm;
        private MockState _idle;
        private MockState _running;
        private MockState _done;

        [SetUp]
        public void SetUp()
        {
            _sm = new StateMachine<Phase>();
            _idle = new MockState();
            _running = new MockState();
            _done = new MockState();
            _sm.AddState(Phase.Idle, _idle);
            _sm.AddState(Phase.Running, _running);
            _sm.AddState(Phase.Done, _done);
        }

        // -------------------------------------------------------------------
        // AddState
        // -------------------------------------------------------------------

        [Test]
        public void AddState_NullHandler_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _sm.AddState(Phase.Idle, null));
        }

        // -------------------------------------------------------------------
        // Initialize
        // -------------------------------------------------------------------

        [Test]
        public void Initialize_SetsCurrentState()
        {
            _sm.Initialize(Phase.Idle);
            Assert.AreEqual(Phase.Idle, _sm.CurrentState);
        }

        [Test]
        public void Initialize_CallsEnterOnInitialState()
        {
            _sm.Initialize(Phase.Idle);
            Assert.AreEqual(1, _idle.EnterCount);
        }

        [Test]
        public void Initialize_UnregisteredState_Throws()
        {
            var sm = new StateMachine<Phase>();
            Assert.Throws<InvalidOperationException>(() => sm.Initialize(Phase.Running));
        }

        // -------------------------------------------------------------------
        // TransitionTo
        // -------------------------------------------------------------------

        [Test]
        public void TransitionTo_ChangesCurrentState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(Phase.Running, _sm.CurrentState);
        }

        [Test]
        public void TransitionTo_SetsPreviousState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(Phase.Idle, _sm.PreviousState);
        }

        [Test]
        public void TransitionTo_CallsExitOnOldState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(1, _idle.ExitCount);
        }

        [Test]
        public void TransitionTo_CallsEnterOnNewState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(1, _running.EnterCount);
            Assert.AreEqual(Phase.Idle, _running.LastEnterPrevious);
            Assert.AreEqual(Phase.Running, _running.LastEnterCurrent);
        }

        [Test]
        public void TransitionTo_SameState_IsNoOp()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Idle);
            Assert.AreEqual(1, _idle.EnterCount); // only from Initialize
            Assert.AreEqual(0, _idle.ExitCount);
        }

        [Test]
        public void TransitionTo_BeforeInitialize_LogsError()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Cannot transition before Initialize"));
            _sm.TransitionTo(Phase.Running);
        }

        [Test]
        public void TransitionTo_UnregisteredState_LogsError()
        {
            var sm = new StateMachine<Phase>();
            sm.AddState(Phase.Idle, new MockState());
            sm.Initialize(Phase.Idle);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No handler registered"));
            sm.TransitionTo(Phase.Done); // Done not registered in this sm
        }

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------

        [Test]
        public void OnStateEntered_FiresOnTransition()
        {
            _sm.Initialize(Phase.Idle);
            Phase enteredPrev = default, enteredNew = default;
            _sm.OnStateEntered += (prev, curr) => { enteredPrev = prev; enteredNew = curr; };
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(Phase.Idle, enteredPrev);
            Assert.AreEqual(Phase.Running, enteredNew);
        }

        [Test]
        public void OnStateExited_FiresOnTransition()
        {
            _sm.Initialize(Phase.Idle);
            Phase exitedPrev = default, exitedNext = default;
            _sm.OnStateExited += (prev, next) => { exitedPrev = prev; exitedNext = next; };
            _sm.TransitionTo(Phase.Running);
            Assert.AreEqual(Phase.Idle, exitedPrev);
            Assert.AreEqual(Phase.Running, exitedNext);
        }

        // -------------------------------------------------------------------
        // Tick
        // -------------------------------------------------------------------

        [Test]
        public void Tick_ForwardsToCurrentState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.Tick(0.016f);
            Assert.AreEqual(1, _idle.TickCount);
            Assert.That(_idle.LastTickDelta, Is.EqualTo(0.016f).Within(0.001f));
        }

        [Test]
        public void Tick_BeforeInitialize_IsNoOp()
        {
            Assert.DoesNotThrow(() => _sm.Tick(0.016f));
            Assert.AreEqual(0, _idle.TickCount);
        }

        [Test]
        public void Tick_AfterTransition_TicksNewState()
        {
            _sm.Initialize(Phase.Idle);
            _sm.TransitionTo(Phase.Running);
            _sm.Tick(0.033f);
            Assert.AreEqual(0, _idle.TickCount);
            Assert.AreEqual(1, _running.TickCount);
        }

        // -------------------------------------------------------------------
        // Full lifecycle
        // -------------------------------------------------------------------

        [Test]
        public void FullLifecycle_Idle_Running_Done()
        {
            _sm.Initialize(Phase.Idle);
            _sm.Tick(0.1f);
            _sm.TransitionTo(Phase.Running);
            _sm.Tick(0.2f);
            _sm.TransitionTo(Phase.Done);

            Assert.AreEqual(Phase.Done, _sm.CurrentState);
            Assert.AreEqual(Phase.Running, _sm.PreviousState);
            Assert.AreEqual(1, _idle.EnterCount);
            Assert.AreEqual(1, _idle.ExitCount);
            Assert.AreEqual(1, _idle.TickCount);
            Assert.AreEqual(1, _running.EnterCount);
            Assert.AreEqual(1, _running.ExitCount);
            Assert.AreEqual(1, _running.TickCount);
            Assert.AreEqual(1, _done.EnterCount);
            Assert.AreEqual(0, _done.ExitCount);
        }
    }
}
