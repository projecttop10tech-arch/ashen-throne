using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Events;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Events
{
    /// <summary>
    /// Unit tests for NotificationScheduler.
    /// Note: ScheduleNotification and CancelFromOsStub each call Debug.Log — these are
    /// informational and expected by design; LogAssert.ignoreFailingMessages is not needed
    /// since Debug.Log does not fail tests (only Debug.LogError/LogAssertion do).
    /// </summary>
    [TestFixture]
    public class NotificationSchedulerTests
    {
        private GameObject _go;
        private NotificationScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _go        = new GameObject("NotificationSchedulerTest");
            _scheduler = _go.AddComponent<NotificationScheduler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static ScheduledNotification FutureNotification(string id = "notif_1",
            NotificationType type = NotificationType.BossSpawned)
            => new ScheduledNotification(id, type, "Title", "Body",
                DateTime.UtcNow.AddHours(1));

        private static ScheduledNotification PastNotification(string id = "past_notif")
            => new ScheduledNotification(id, NotificationType.BuildComplete, "T", "B",
                DateTime.UtcNow.AddHours(-1));

        // -------------------------------------------------------------------
        // ScheduledNotification construction
        // -------------------------------------------------------------------

        [Test]
        public void ScheduledNotification_ThrowsArgumentException_WhenIdEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                new ScheduledNotification("", NotificationType.BossSpawned, "T", "B",
                    DateTime.UtcNow.AddHours(1)));
        }

        // -------------------------------------------------------------------
        // ScheduleNotification
        // -------------------------------------------------------------------

        [Test]
        public void ScheduleNotification_ReturnsFalse_WhenNotificationNull()
        {
            Assert.IsFalse(_scheduler.ScheduleNotification(null));
        }

        [Test]
        public void ScheduleNotification_ReturnsFalse_WhenFireAtUtcInPast()
        {
            Assert.IsFalse(_scheduler.ScheduleNotification(PastNotification()));
        }

        [Test]
        public void ScheduleNotification_ReturnsTrue_ForFutureNotification()
        {
            Assert.IsTrue(_scheduler.ScheduleNotification(FutureNotification()));
        }

        [Test]
        public void ScheduleNotification_IsIdempotent_OnDuplicateId()
        {
            _scheduler.ScheduleNotification(FutureNotification("dup"));
            bool second = _scheduler.ScheduleNotification(FutureNotification("dup"));
            Assert.IsFalse(second);
            Assert.AreEqual(1, _scheduler.ScheduledCount);
        }

        [Test]
        public void ScheduleNotification_SetsIsDispatched()
        {
            var notif = FutureNotification();
            _scheduler.ScheduleNotification(notif);
            Assert.IsTrue(notif.IsDispatched);
        }

        [Test]
        public void ScheduleNotification_IncreasesScheduledCount()
        {
            _scheduler.ScheduleNotification(FutureNotification("n1"));
            _scheduler.ScheduleNotification(FutureNotification("n2"));
            Assert.AreEqual(2, _scheduler.ScheduledCount);
        }

        // -------------------------------------------------------------------
        // IsScheduled
        // -------------------------------------------------------------------

        [Test]
        public void IsScheduled_ReturnsFalse_WhenNotScheduled()
        {
            Assert.IsFalse(_scheduler.IsScheduled("nonexistent"));
        }

        [Test]
        public void IsScheduled_ReturnsTrue_AfterScheduling()
        {
            _scheduler.ScheduleNotification(FutureNotification("check_me"));
            Assert.IsTrue(_scheduler.IsScheduled("check_me"));
        }

        // -------------------------------------------------------------------
        // CancelNotification
        // -------------------------------------------------------------------

        [Test]
        public void CancelNotification_ReturnsFalse_WhenNotScheduled()
        {
            Assert.IsFalse(_scheduler.CancelNotification("not_there"));
        }

        [Test]
        public void CancelNotification_ReturnsTrue_WhenScheduled()
        {
            _scheduler.ScheduleNotification(FutureNotification("to_cancel"));
            Assert.IsTrue(_scheduler.CancelNotification("to_cancel"));
        }

        [Test]
        public void CancelNotification_RemovesFromScheduled()
        {
            _scheduler.ScheduleNotification(FutureNotification("remove_me"));
            _scheduler.CancelNotification("remove_me");
            Assert.IsFalse(_scheduler.IsScheduled("remove_me"));
            Assert.AreEqual(0, _scheduler.ScheduledCount);
        }

        // -------------------------------------------------------------------
        // CancelAll
        // -------------------------------------------------------------------

        [Test]
        public void CancelAll_ClearsAllNotifications()
        {
            _scheduler.ScheduleNotification(FutureNotification("n1"));
            _scheduler.ScheduleNotification(FutureNotification("n2"));
            _scheduler.ScheduleNotification(FutureNotification("n3"));
            _scheduler.CancelAll();
            Assert.AreEqual(0, _scheduler.ScheduledCount);
        }

        [Test]
        public void CancelAll_DoesNotThrow_WhenEmpty()
        {
            Assert.DoesNotThrow(() => _scheduler.CancelAll());
        }
    }
}
