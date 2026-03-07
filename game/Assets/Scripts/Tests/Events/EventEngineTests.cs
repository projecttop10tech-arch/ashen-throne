using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Events;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Events
{
    /// <summary>
    /// Unit tests for EventEngine MonoBehaviour.
    /// Tests focus on LoadActiveEvents and ReportProgress — the stateful, testable
    /// public surface. CheckEventSchedules runs in Update (not directly testable in EditMode).
    /// </summary>
    [TestFixture]
    public class EventEngineTests
    {
        private GameObject _go;
        private EventEngine _engine;
        private readonly List<EventDefinition> _defs = new();

        [SetUp]
        public void SetUp()
        {
            _go     = new GameObject("EventEngineTest");
            _engine = _go.AddComponent<EventEngine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (var d in _defs)
                if (d != null) Object.DestroyImmediate(d);
            _defs.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private EventDefinition MakeDef(string id = "evt_1", int target = 100,
            EventObjectiveType objType = EventObjectiveType.DamageDealt,
            bool active = true)
        {
            var def = ScriptableObject.CreateInstance<EventDefinition>();
            def.eventId        = id;
            def.displayName    = id;
            def.objectiveType  = objType;
            def.objectiveTarget = target;
            if (active)
            {
                def.startTimeIso = DateTime.UtcNow.AddHours(-1).ToString("O");
                def.endTimeIso   = DateTime.UtcNow.AddHours(1).ToString("O");
            }
            else
            {
                def.startTimeIso = DateTime.UtcNow.AddHours(-2).ToString("O");
                def.endTimeIso   = DateTime.UtcNow.AddHours(-1).ToString("O");
            }
            _defs.Add(def);
            return def;
        }

        private EventSaveEntry MakeSave(string id, int progress = 0)
            => new EventSaveEntry { EventId = id, Progress = progress };

        // -------------------------------------------------------------------
        // LoadActiveEvents
        // -------------------------------------------------------------------

        [Test]
        public void LoadActiveEvents_Null_ClearsActiveEvents()
        {
            // First add something, then clear with null
            var def  = MakeDef();
            // Inject the definition via allEventDefinitions field (reflection)
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave(def.eventId) });
            _engine.LoadActiveEvents(null);
            Assert.AreEqual(0, _engine.ActiveEvents.Count);
        }

        [Test]
        public void LoadActiveEvents_NullEntriesInList_IgnoresNulls()
        {
            var saves = new List<EventSaveEntry> { null, null };
            Assert.DoesNotThrow(() => _engine.LoadActiveEvents(saves));
            Assert.AreEqual(0, _engine.ActiveEvents.Count);
        }

        [Test]
        public void LoadActiveEvents_RestoresProgressFromSave()
        {
            var def = MakeDef("evt_progress", target: 50);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("evt_progress", 25) });
            var active = _engine.GetActiveEvent("evt_progress");
            Assert.IsNotNull(active);
            Assert.AreEqual(25, active.CurrentProgress);
        }

        [Test]
        public void LoadActiveEvents_IgnoresSave_WhenDefinitionNotFound()
        {
            // No definition injected — save for unknown event should be ignored
            InjectDefinitions(); // inject empty list
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("ghost_event") });
            Assert.AreEqual(0, _engine.ActiveEvents.Count);
        }

        [Test]
        public void LoadActiveEvents_ReplacesExistingActiveEvents()
        {
            var def1 = MakeDef("evt_a");
            var def2 = MakeDef("evt_b");
            InjectDefinitions(def1, def2);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("evt_a") });
            Assert.AreEqual(1, _engine.ActiveEvents.Count);
            // Load again with only evt_b
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("evt_b") });
            Assert.AreEqual(1, _engine.ActiveEvents.Count);
            Assert.AreEqual("evt_b", _engine.ActiveEvents[0].Definition.eventId);
        }

        // -------------------------------------------------------------------
        // ReportProgress
        // -------------------------------------------------------------------

        [Test]
        public void ReportProgress_ZeroAmount_IsNoOp()
        {
            var def = MakeDef(objType: EventObjectiveType.DamageDealt, target: 100);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave(def.eventId) });
            _engine.ReportProgress(EventObjectiveType.DamageDealt, 0);
            var active = _engine.GetActiveEvent(def.eventId);
            Assert.AreEqual(0, active.CurrentProgress);
        }

        [Test]
        public void ReportProgress_NegativeAmount_IsNoOp()
        {
            var def = MakeDef(objType: EventObjectiveType.DamageDealt, target: 100);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave(def.eventId) });
            _engine.ReportProgress(EventObjectiveType.DamageDealt, -10);
            var active = _engine.GetActiveEvent(def.eventId);
            Assert.AreEqual(0, active.CurrentProgress);
        }

        [Test]
        public void ReportProgress_WrongObjectiveType_IsNoOp()
        {
            var def = MakeDef(objType: EventObjectiveType.DamageDealt, target: 100);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave(def.eventId) });
            _engine.ReportProgress(EventObjectiveType.ShardsEarned, 50);
            var active = _engine.GetActiveEvent(def.eventId);
            Assert.AreEqual(0, active.CurrentProgress);
        }

        [Test]
        public void ReportProgress_AdvancesProgress_WhenTypeMatches()
        {
            var def = MakeDef(objType: EventObjectiveType.DamageDealt, target: 100);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave(def.eventId) });
            _engine.ReportProgress(EventObjectiveType.DamageDealt, 30);
            var active = _engine.GetActiveEvent(def.eventId);
            Assert.AreEqual(30, active.CurrentProgress);
        }

        [Test]
        public void ReportProgress_DoesNotAdvance_WhenEventIsInactive()
        {
            var def = MakeDef("past_evt", active: false);
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("past_evt") });
            _engine.ReportProgress(EventObjectiveType.DamageDealt, 50);
            var active = _engine.GetActiveEvent("past_evt");
            Assert.IsNotNull(active);
            Assert.AreEqual(0, active.CurrentProgress);
        }

        // -------------------------------------------------------------------
        // GetActiveEvent
        // -------------------------------------------------------------------

        [Test]
        public void GetActiveEvent_ReturnsNull_WhenNoActiveEvents()
        {
            Assert.IsNull(_engine.GetActiveEvent("nonexistent"));
        }

        [Test]
        public void GetActiveEvent_ReturnsNull_ForMissingId()
        {
            var def = MakeDef("real_event");
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("real_event") });
            Assert.IsNull(_engine.GetActiveEvent("wrong_id"));
        }

        [Test]
        public void GetActiveEvent_ReturnsEvent_ByCorrectId()
        {
            var def = MakeDef("find_me");
            InjectDefinitions(def);
            _engine.LoadActiveEvents(new List<EventSaveEntry> { MakeSave("find_me") });
            var result = _engine.GetActiveEvent("find_me");
            Assert.IsNotNull(result);
            Assert.AreEqual("find_me", result.Definition.eventId);
        }

        // -------------------------------------------------------------------
        // Helper: inject definitions into the engine's allEventDefinitions field
        // -------------------------------------------------------------------

        private void InjectDefinitions(params EventDefinition[] defs)
        {
            var list = new List<EventDefinition>(defs);
            var field = typeof(EventEngine).GetField("allEventDefinitions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "EventEngine.allEventDefinitions field not found via reflection.");
            field.SetValue(_engine, list);
        }
    }
}
