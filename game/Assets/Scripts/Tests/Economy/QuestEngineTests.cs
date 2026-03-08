using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Economy;
using AshenThrone.Data;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Economy
{
    [TestFixture]
    public class QuestEngineTests
    {
        private GameObject _go;
        private QuestEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("QuestEngineTest");
            _engine = _go.AddComponent<QuestEngine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // --- Initialize ---

        [Test]
        public void Initialize_ThrowsArgumentNull_WhenDefinitionsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _engine.Initialize(null, new QuestSaveData()));
        }

        [Test]
        public void Initialize_IgnoresNullDefinitions()
        {
            var defs = new List<QuestDefinition> { MakeQuest("q1"), null, MakeQuest("q2") };
            Assert.DoesNotThrow(() => _engine.Initialize(defs, null));
            Assert.IsNotNull(_engine.GetProgress("q1"));
        }

        [Test]
        public void Initialize_RestoresSavedProgress()
        {
            var def = MakeQuest("q1", required: 5);
            var save = new QuestSaveData();
            save.QuestProgress["q1"] = 3;
            _engine.Initialize(new List<QuestDefinition> { def }, save);
            Assert.AreEqual(3, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void Initialize_ResetsProgress_WhenDailyResetNeeded()
        {
            var def = MakeQuest("q1", cadence: QuestCadence.Daily);
            var save = new QuestSaveData
            {
                LastDailyResetUtc = DateTime.UtcNow.AddDays(-2), // 2 days ago = needs reset
                QuestProgress = new Dictionary<string, int> { { "q1", 3 } }
            };
            _engine.Initialize(new List<QuestDefinition> { def }, save);
            Assert.AreEqual(0, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void Initialize_DoesNotResetProgress_WhenNoResetNeeded()
        {
            var def = MakeQuest("q1", cadence: QuestCadence.Daily);
            var save = new QuestSaveData
            {
                LastDailyResetUtc = DateTime.UtcNow, // today = no reset needed
                QuestProgress = new Dictionary<string, int> { { "q1", 3 } }
            };
            _engine.Initialize(new List<QuestDefinition> { def }, save);
            Assert.AreEqual(3, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void Initialize_DoesNotResetOneTimeQuests_Ever()
        {
            var def = MakeQuest("q1", cadence: QuestCadence.OneTime);
            var save = new QuestSaveData
            {
                LastDailyResetUtc = DateTime.UtcNow.AddDays(-10),
                QuestProgress = new Dictionary<string, int> { { "q1", 2 } }
            };
            _engine.Initialize(new List<QuestDefinition> { def }, save);
            Assert.AreEqual(2, _engine.GetProgress("q1").CurrentCount);
        }

        // --- RecordProgress ---

        [Test]
        public void RecordProgress_IncrementsMatchingQuest()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 3);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 1);
            Assert.AreEqual(1, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void RecordProgress_DoesNotIncrementWrongObjectiveType()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.UpgradeBuilding, 1);
            Assert.AreEqual(0, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void RecordProgress_IgnoresZeroAmount()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 0);
            Assert.AreEqual(0, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void RecordProgress_CapsAtRequiredCount()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 2);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 999);
            Assert.AreEqual(2, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void RecordProgress_MarksQuestCompleted_WhenReached()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 1);
            Assert.IsTrue(_engine.GetProgress("q1").IsCompleted);
        }

        [Test]
        public void RecordProgress_FiresQuestCompletedEvent()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            bool fired = false;
            var sub = AshenThrone.Core.EventBus.Subscribe<QuestCompletedEvent>(e => fired = true);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 1);
            sub.Dispose();
            Assert.IsTrue(fired);
        }

        [Test]
        public void RecordProgress_ContextTag_OnlyMatchesMatchingTag()
        {
            var def = MakeQuestWithTag("q1", QuestObjectiveType.UpgradeBuilding, required: 1, tag: "military");
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.UpgradeBuilding, 1, contextTag: "resource");
            Assert.AreEqual(0, _engine.GetProgress("q1").CurrentCount);
            _engine.RecordProgress(QuestObjectiveType.UpgradeBuilding, 1, contextTag: "military");
            Assert.AreEqual(1, _engine.GetProgress("q1").CurrentCount);
        }

        [Test]
        public void RecordProgress_NoContextTag_MatchesAny()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.UpgradeBuilding, required: 1);
            // Quest has no context tag — any context should match
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.UpgradeBuilding, 1, contextTag: "anything");
            Assert.AreEqual(1, _engine.GetProgress("q1").CurrentCount);
        }

        // --- ClaimReward ---

        [Test]
        public void ClaimReward_ReturnsFalse_WhenQuestNotFound()
        {
            _engine.Initialize(new List<QuestDefinition>(), null);
            Assert.IsFalse(_engine.ClaimReward("nonexistent"));
        }

        [Test]
        public void ClaimReward_ReturnsFalse_WhenQuestNotCompleted()
        {
            var def = MakeQuest("q1", required: 5);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            Assert.IsFalse(_engine.ClaimReward("q1"));
        }

        [Test]
        public void ClaimReward_ReturnsTrue_WhenCompleted()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 1);
            Assert.IsTrue(_engine.ClaimReward("q1"));
        }

        [Test]
        public void ClaimReward_ReturnsFalse_WhenAlreadyClaimed()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 1);
            _engine.ClaimReward("q1");
            Assert.IsFalse(_engine.ClaimReward("q1"));
        }

        // --- GetQuestsByCategory ---

        [Test]
        public void GetQuestsByCategory_ReturnsOnlyMatchingCadence()
        {
            var daily  = MakeQuest("d1", cadence: QuestCadence.Daily);
            var weekly = MakeQuest("w1", cadence: QuestCadence.Weekly);
            _engine.Initialize(new List<QuestDefinition> { daily, weekly }, null);
            var dailies = _engine.GetQuestsByCategory(QuestCadence.Daily);
            Assert.AreEqual(1, dailies.Count);
            Assert.AreEqual("d1", dailies[0].Definition.QuestId);
        }

        // --- GetUnclaimedCount ---

        [Test]
        public void GetUnclaimedCount_ReturnsNumberOfCompletedUnclaimedQuests()
        {
            var def1 = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            var def2 = MakeQuest("q2", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            _engine.Initialize(new List<QuestDefinition> { def1, def2 }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 2);
            Assert.AreEqual(2, _engine.GetUnclaimedCount());
            _engine.ClaimReward("q1");
            Assert.AreEqual(1, _engine.GetUnclaimedCount());
        }

        // --- Static helpers ---

        [Test]
        public void GetLastMidnightUtc_ReturnsToday()
        {
            DateTime midnight = QuestEngine.GetLastMidnightUtc();
            Assert.AreEqual(DateTime.UtcNow.Date, midnight.Date);
        }

        [Test]
        public void GetLastMondayUtc_ReturnsMonday()
        {
            DateTime monday = QuestEngine.GetLastMondayUtc();
            Assert.AreEqual(DayOfWeek.Monday, monday.DayOfWeek);
        }

        // --- BuildSaveData ---

        [Test]
        public void BuildSaveData_IncludesCurrentProgress()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 5);
            _engine.Initialize(new List<QuestDefinition> { def }, null);
            _engine.RecordProgress(QuestObjectiveType.WinCombatBattles, 2);
            var save = _engine.BuildSaveData();
            Assert.IsTrue(save.QuestProgress.ContainsKey("q1"));
            Assert.AreEqual(2, save.QuestProgress["q1"]);
        }

        // --- QuestProgress ---

        [Test]
        public void QuestProgress_Reset_ClearsCountAndClaimed()
        {
            var def = MakeQuest("q1", objectiveType: QuestObjectiveType.WinCombatBattles, required: 1);
            var progress = new QuestProgress(def, 1);
            progress.Claim();
            progress.Reset();
            Assert.AreEqual(0, progress.CurrentCount);
            Assert.IsFalse(progress.IsClaimed);
        }

        // --- Helpers ---

        private static QuestDefinition MakeQuest(string questId,
            QuestObjectiveType objectiveType = QuestObjectiveType.WinCombatBattles,
            int required = 3,
            QuestCadence cadence = QuestCadence.Daily)
        {
            var def = ScriptableObject.CreateInstance<QuestDefinition>();
            // Use reflection to set private fields
            SetField(def, "<QuestId>k__BackingField", questId);
            SetField(def, "<DisplayName>k__BackingField", questId);
            SetField(def, "<Description>k__BackingField", "test");
            SetField(def, "<Cadence>k__BackingField", cadence);
            SetField(def, "<ObjectiveType>k__BackingField", objectiveType);
            SetField(def, "<RequiredCount>k__BackingField", required);
            SetField(def, "<BattlePassPoints>k__BackingField", 100);
            SetField(def, "<ContextTag>k__BackingField", "");
            return def;
        }

        private static QuestDefinition MakeQuestWithTag(string questId,
            QuestObjectiveType objectiveType, int required, string tag)
        {
            var def = MakeQuest(questId, objectiveType, required);
            SetField(def, "<ContextTag>k__BackingField", tag);
            return def;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
