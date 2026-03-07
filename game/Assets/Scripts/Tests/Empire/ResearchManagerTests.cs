// Unity Test Framework (NUnit) tests for ResearchManager.
// Tests use AddComponent on temp GameObjects — no full scene required.
// Location: Assets/Scripts/Tests/Empire/ — Unity discovers automatically.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Empire;
using AshenThrone.Data;
using AshenThrone.Core;

namespace AshenThrone.Tests.Empire
{
    /// <summary>
    /// Unit tests for ResearchManager covering queue validation, prerequisite enforcement,
    /// effect application, speedup mechanic, and hydration from save data.
    /// </summary>
    [TestFixture]
    public class ResearchManagerTests
    {
        private GameObject _go;
        private ResearchManager _researchManager;
        private ResourceManager _resourceManager;
        private EmpireConfig _empireConfig;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ResearchManagerTest");
            _empireConfig = ScriptableObject.CreateInstance<EmpireConfig>();
            _empireConfig.MaxResearchQueues = 1;

            // Wire ResourceManager with generous resources so research can proceed
            var rmField = typeof(EmpireConfig).GetField("StartingStone");
            _empireConfig.StartingStone = 99999;
            _empireConfig.StartingIron = 99999;
            _empireConfig.StartingGrain = 99999;
            _empireConfig.StartingArcaneEssence = 99999;
            _empireConfig.DefaultMaxStone = 999999;
            _empireConfig.DefaultMaxIron = 999999;
            _empireConfig.DefaultMaxGrain = 999999;
            _empireConfig.DefaultMaxArcaneEssence = 999999;

            // Inject config via serialized field reflection
            _resourceManager = _go.AddComponent<ResourceManager>();
            SetPrivateField(_resourceManager, "_config", _empireConfig);

            _researchManager = _go.AddComponent<ResearchManager>();
            SetPrivateField(_researchManager, "_config", _empireConfig);

            // Pre-populate internal node dict with test nodes (bypass Resources.Load)
            var nodeDict = GetPrivateField<Dictionary<string, ResearchNodeData>>(_researchManager, "_allNodes");
            nodeDict["node_a"] = MakeNode("node_a", prerequisites: null, timeSeconds: 10);
            nodeDict["node_b"] = MakeNode("node_b", prerequisites: new[] { "node_a" }, timeSeconds: 10);
            nodeDict["node_c"] = MakeNode("node_c", prerequisites: null, timeSeconds: 10,
                effects: new List<ResearchEffect>
                {
                    new ResearchEffect { effectType = ResearchEffectType.CombatAttackPercent, magnitude = 10f }
                });

            // Pre-populate ResourceManager internal state via InitializeFromConfig
            InvokePrivateMethod(_resourceManager, "InitializeFromConfig");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_empireConfig);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static ResearchNodeData MakeNode(string id,
            string[] prerequisites = null,
            int timeSeconds = 60,
            List<ResearchEffect> effects = null)
        {
            var node = ScriptableObject.CreateInstance<ResearchNodeData>();
            node.nodeId = id;
            node.displayName = id;
            node.stoneCost = 10;
            node.ironCost = 10;
            node.grainCost = 10;
            node.arcaneEssenceCost = 10;
            node.researchTimeSeconds = timeSeconds;
            node.prerequisiteNodeIds = prerequisites ?? System.Array.Empty<string>();
            node.effects = effects ?? new List<ResearchEffect>();
            node.requiredAcademyTier = 1;
            return node;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)field?.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(target, null);
        }

        // ─── StartResearch — Happy Path ───────────────────────────────────────────

        [Test]
        public void StartResearch_ReturnsTrue_WhenValidNodeAndResourcesAvailable()
        {
            Assert.IsTrue(_researchManager.StartResearch("node_a"));
        }

        [Test]
        public void StartResearch_AddsToQueue_WhenSuccessful()
        {
            _researchManager.StartResearch("node_a");
            Assert.AreEqual(1, _researchManager.ResearchQueue.Count);
        }

        [Test]
        public void StartResearch_FiresOnResearchStarted_WhenSuccessful()
        {
            string startedId = null;
            _researchManager.OnResearchStarted += id => startedId = id;
            _researchManager.StartResearch("node_a");
            Assert.AreEqual("node_a", startedId);
        }

        // ─── StartResearch — Failure Cases ────────────────────────────────────────

        [Test]
        public void StartResearch_ReturnsFalse_WhenNodeIdNotFound()
        {
            Assert.IsFalse(_researchManager.StartResearch("nonexistent_node"));
        }

        [Test]
        public void StartResearch_ReturnsFalse_WhenPrerequisitesNotMet()
        {
            // node_b requires node_a — not yet completed
            Assert.IsFalse(_researchManager.StartResearch("node_b"));
        }

        [Test]
        public void StartResearch_ReturnsFalse_WhenAlreadyInQueue()
        {
            _researchManager.StartResearch("node_a");
            Assert.IsFalse(_researchManager.StartResearch("node_a"));
        }

        [Test]
        public void StartResearch_ReturnsFalse_WhenQueueFull()
        {
            // MaxResearchQueues = 1; queuing a second node should fail
            var nodeDict = GetPrivateField<Dictionary<string, ResearchNodeData>>(_researchManager, "_allNodes");
            nodeDict["node_d"] = MakeNode("node_d");

            _researchManager.StartResearch("node_a");
            Assert.IsFalse(_researchManager.StartResearch("node_d"));
        }

        [Test]
        public void StartResearch_ReturnsFalse_WhenAlreadyCompleted()
        {
            // Directly inject into completed set
            var completed = GetPrivateField<HashSet<string>>(_researchManager, "_completedNodeIds");
            completed.Add("node_a");
            Assert.IsFalse(_researchManager.StartResearch("node_a"));
        }

        // ─── Prerequisites ────────────────────────────────────────────────────────

        [Test]
        public void StartResearch_Succeeds_WhenPrerequisiteIsCompleted()
        {
            var completed = GetPrivateField<HashSet<string>>(_researchManager, "_completedNodeIds");
            completed.Add("node_a");
            Assert.IsTrue(_researchManager.StartResearch("node_b"));
        }

        [Test]
        public void IsAvailable_ReturnsFalse_WhenPrerequisitesMissing()
        {
            Assert.IsFalse(_researchManager.IsAvailable("node_b"));
        }

        [Test]
        public void IsAvailable_ReturnsTrue_WhenPrerequisitesMet()
        {
            var completed = GetPrivateField<HashSet<string>>(_researchManager, "_completedNodeIds");
            completed.Add("node_a");
            Assert.IsTrue(_researchManager.IsAvailable("node_b"));
        }

        [Test]
        public void IsAvailable_ReturnsFalse_WhenNodeAlreadyCompleted()
        {
            var completed = GetPrivateField<HashSet<string>>(_researchManager, "_completedNodeIds");
            completed.Add("node_a");
            Assert.IsFalse(_researchManager.IsAvailable("node_a"));
        }

        // ─── ApplySpeedup ─────────────────────────────────────────────────────────

        [Test]
        public void ApplySpeedup_ReducesRemainingTime()
        {
            _researchManager.StartResearch("node_a");
            float before = _researchManager.ResearchQueue[0].RemainingSeconds;
            _researchManager.ApplySpeedup(5);
            Assert.AreEqual(before - 5f, _researchManager.ResearchQueue[0].RemainingSeconds, 0.001f);
        }

        [Test]
        public void ApplySpeedup_ClampsToZero_WhenSpeedupExceedsRemaining()
        {
            _researchManager.StartResearch("node_a");
            _researchManager.ApplySpeedup(99999);
            Assert.AreEqual(0f, _researchManager.ResearchQueue[0].RemainingSeconds, 0.001f);
        }

        [Test]
        public void ApplySpeedup_DoesNotThrow_WhenQueueEmpty()
        {
            Assert.DoesNotThrow(() => _researchManager.ApplySpeedup(100));
        }

        // ─── Completion + Effects ─────────────────────────────────────────────────

        [Test]
        public void CompleteResearch_FiresOnResearchCompleted_AndAddsToCompletedSet()
        {
            string completedId = null;
            _researchManager.OnResearchCompleted += id => completedId = id;
            _researchManager.StartResearch("node_c");
            _researchManager.ApplySpeedup(99999); // instant

            // Simulate Update tick to process completion
            InvokePrivateMethod(_researchManager, "TickResearchQueue");

            Assert.AreEqual("node_c", completedId);
            Assert.IsTrue(_researchManager.IsCompleted("node_c"));
        }

        [Test]
        public void CompleteResearch_AppliesEffects_ToBonusState()
        {
            _researchManager.StartResearch("node_c");
            _researchManager.ApplySpeedup(99999);
            InvokePrivateMethod(_researchManager, "TickResearchQueue");

            Assert.AreEqual(10f, _researchManager.Bonuses.CombatAttackPercent, 0.001f);
        }

        // ─── HydrateCompletedNodes ─────────────────────────────────────────────────

        [Test]
        public void HydrateCompletedNodes_MarksNodesAsCompleted()
        {
            _researchManager.HydrateCompletedNodes(new[] { "node_a" });
            Assert.IsTrue(_researchManager.IsCompleted("node_a"));
        }

        [Test]
        public void HydrateCompletedNodes_AppliesEffects_FromHydratedNodes()
        {
            _researchManager.HydrateCompletedNodes(new[] { "node_c" });
            Assert.AreEqual(10f, _researchManager.Bonuses.CombatAttackPercent, 0.001f);
        }

        [Test]
        public void HydrateCompletedNodes_DoesNotThrow_WithNullInput()
        {
            Assert.DoesNotThrow(() => _researchManager.HydrateCompletedNodes(null));
        }
    }
}
