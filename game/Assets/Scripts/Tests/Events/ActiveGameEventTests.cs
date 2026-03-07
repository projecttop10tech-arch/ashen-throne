using System;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Events;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Events
{
    /// <summary>
    /// Unit tests for ActiveGameEvent (pure C# state container).
    /// </summary>
    [TestFixture]
    public class ActiveGameEventTests
    {
        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static EventDefinition MakeDefinition(int objectiveTarget = 10,
            string startIso = null, string endIso = null)
        {
            var def = ScriptableObject.CreateInstance<EventDefinition>();
            def.eventId        = "test_event";
            def.displayName    = "Test Event";
            def.objectiveType  = EventObjectiveType.DamageDealt;
            def.objectiveTarget = objectiveTarget;
            // Active window: started 1 hour ago, ends 1 hour from now
            def.startTimeIso   = startIso ?? DateTime.UtcNow.AddHours(-1).ToString("O");
            def.endTimeIso     = endIso   ?? DateTime.UtcNow.AddHours(1).ToString("O");
            return def;
        }

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------

        [Test]
        public void Constructor_ThrowsArgumentNull_WhenDefinitionNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ActiveGameEvent(null, null));
        }

        [Test]
        public void Constructor_SetsProgressToZero_WhenNoSave()
        {
            var def = MakeDefinition();
            var evt = new ActiveGameEvent(def, null);
            Assert.AreEqual(0, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void Constructor_RestoresProgress_FromSave()
        {
            var def  = MakeDefinition();
            var save = new EventSaveEntry { EventId = "test_event", Progress = 7 };
            var evt  = new ActiveGameEvent(def, save);
            Assert.AreEqual(7, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        // -------------------------------------------------------------------
        // IsActive
        // -------------------------------------------------------------------

        [Test]
        public void IsActive_ReturnsTrue_WhenWithinScheduleWindow()
        {
            var def = MakeDefinition(); // default: active right now
            var evt = new ActiveGameEvent(def, null);
            Assert.IsTrue(evt.IsActive);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void IsActive_ReturnsFalse_WhenEventAlreadyEnded()
        {
            var def = MakeDefinition(
                startIso: DateTime.UtcNow.AddHours(-2).ToString("O"),
                endIso:   DateTime.UtcNow.AddHours(-1).ToString("O"));
            var evt = new ActiveGameEvent(def, null);
            Assert.IsFalse(evt.IsActive);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void IsActive_ReturnsFalse_WhenEventNotYetStarted()
        {
            var def = MakeDefinition(
                startIso: DateTime.UtcNow.AddHours(1).ToString("O"),
                endIso:   DateTime.UtcNow.AddHours(2).ToString("O"));
            var evt = new ActiveGameEvent(def, null);
            Assert.IsFalse(evt.IsActive);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void IsActive_ReturnsFalse_WhenIsoStringsInvalid()
        {
            var def = MakeDefinition(startIso: "not-a-date", endIso: "also-bad");
            var evt = new ActiveGameEvent(def, null);
            Assert.IsFalse(evt.IsActive);
            Object.DestroyImmediate(def);
        }

        // -------------------------------------------------------------------
        // AddProgress
        // -------------------------------------------------------------------

        [Test]
        public void AddProgress_IncreasesCurrentProgress()
        {
            var def = MakeDefinition(objectiveTarget: 100);
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(5);
            Assert.AreEqual(5, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AddProgress_ClampedToObjectiveTarget()
        {
            var def = MakeDefinition(objectiveTarget: 10);
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(50);
            Assert.AreEqual(10, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AddProgress_IgnoresZeroAmount()
        {
            var def = MakeDefinition();
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(0);
            Assert.AreEqual(0, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void AddProgress_IgnoresNegativeAmount()
        {
            var def = MakeDefinition();
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(-5);
            Assert.AreEqual(0, evt.CurrentProgress);
            Object.DestroyImmediate(def);
        }

        // -------------------------------------------------------------------
        // CompletionRatio
        // -------------------------------------------------------------------

        [Test]
        public void CompletionRatio_ReturnsOne_WhenTargetIsZero()
        {
            var def = MakeDefinition(objectiveTarget: 0);
            var evt = new ActiveGameEvent(def, null);
            Assert.AreEqual(1f, evt.CompletionRatio, 0.001f);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void CompletionRatio_ReturnsCorrectRatio()
        {
            var def = MakeDefinition(objectiveTarget: 10);
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(5);
            Assert.AreEqual(0.5f, evt.CompletionRatio, 0.001f);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void CompletionRatio_ClampedToOne()
        {
            var def = MakeDefinition(objectiveTarget: 10);
            var evt = new ActiveGameEvent(def, null);
            evt.AddProgress(9999);
            Assert.AreEqual(1f, evt.CompletionRatio, 0.001f);
            Object.DestroyImmediate(def);
        }
    }
}
