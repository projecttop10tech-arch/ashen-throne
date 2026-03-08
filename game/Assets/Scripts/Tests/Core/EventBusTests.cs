using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Core;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class EventBusTests
    {
        // Test event structs
        private struct TestEvent
        {
            public int Value;
        }

        private struct OtherEvent
        {
            public string Message;
        }

        [SetUp]
        public void SetUp()
        {
            EventBus.Shutdown();
            EventBus.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Shutdown();
        }

        // -------------------------------------------------------------------
        // Subscribe + Publish
        // -------------------------------------------------------------------

        [Test]
        public void Subscribe_Publish_HandlerReceivesEvent()
        {
            int received = -1;
            EventBus.Subscribe<TestEvent>(e => received = e.Value);
            EventBus.Publish(new TestEvent { Value = 42 });
            Assert.AreEqual(42, received);
        }

        [Test]
        public void Subscribe_MultipleHandlers_AllReceive()
        {
            int countA = 0, countB = 0;
            EventBus.Subscribe<TestEvent>(_ => countA++);
            EventBus.Subscribe<TestEvent>(_ => countB++);
            EventBus.Publish(new TestEvent { Value = 1 });
            Assert.AreEqual(1, countA);
            Assert.AreEqual(1, countB);
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent { Value = 99 }));
        }

        [Test]
        public void Publish_DifferentEventType_DoesNotTriggerWrongHandler()
        {
            bool fired = false;
            EventBus.Subscribe<OtherEvent>(_ => fired = true);
            EventBus.Publish(new TestEvent { Value = 1 });
            Assert.IsFalse(fired);
        }

        [Test]
        public void Publish_MultipleEvents_EachHandlerReceivesCorrectEvent()
        {
            int testValue = 0;
            string otherMessage = null;
            EventBus.Subscribe<TestEvent>(e => testValue = e.Value);
            EventBus.Subscribe<OtherEvent>(e => otherMessage = e.Message);

            EventBus.Publish(new TestEvent { Value = 7 });
            EventBus.Publish(new OtherEvent { Message = "hello" });

            Assert.AreEqual(7, testValue);
            Assert.AreEqual("hello", otherMessage);
        }

        // -------------------------------------------------------------------
        // Unsubscribe
        // -------------------------------------------------------------------

        [Test]
        public void Unsubscribe_StopsReceivingEvents()
        {
            int count = 0;
            Action<TestEvent> handler = _ => count++;
            EventBus.Subscribe<TestEvent>(handler);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count);

            EventBus.Unsubscribe(handler);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count); // still 1
        }

        [Test]
        public void Unsubscribe_NotRegistered_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus.Unsubscribe<TestEvent>(_ => { }));
        }

        // -------------------------------------------------------------------
        // EventSubscription (Dispose pattern)
        // -------------------------------------------------------------------

        [Test]
        public void Subscription_Dispose_Unsubscribes()
        {
            int count = 0;
            var sub = EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count);

            sub.Dispose();
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Subscription_DoubleDispose_IsIdempotent()
        {
            int count = 0;
            var sub = EventBus.Subscribe<TestEvent>(_ => count++);
            sub.Dispose();
            Assert.DoesNotThrow(() => sub.Dispose());
        }

        [Test]
        public void Subscription_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EventSubscription(null));
        }

        // -------------------------------------------------------------------
        // Exception safety
        // -------------------------------------------------------------------

        [Test]
        public void Publish_HandlerThrows_OtherHandlersStillReceive()
        {
            int secondCount = 0;
            EventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            EventBus.Subscribe<TestEvent>(_ => secondCount++);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Handler threw exception"));
            EventBus.Publish(new TestEvent());

            Assert.AreEqual(1, secondCount);
        }

        // -------------------------------------------------------------------
        // Initialize / Shutdown
        // -------------------------------------------------------------------

        [Test]
        public void Shutdown_ClearsAllSubscriptions()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Shutdown();
            EventBus.Initialize();
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Initialize_DoubleCall_IsIdempotent()
        {
            int count = 0;
            EventBus.Subscribe<TestEvent>(_ => count++);
            EventBus.Initialize(); // second call should not clear
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, count);
        }

        // -------------------------------------------------------------------
        // Snapshot safety: unsubscribe during publish
        // -------------------------------------------------------------------

        [Test]
        public void Publish_UnsubscribeDuringDispatch_DoesNotThrow()
        {
            Action<TestEvent> handler = null;
            handler = _ => EventBus.Unsubscribe(handler);
            EventBus.Subscribe<TestEvent>(handler);
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent()));
        }
    }
}
