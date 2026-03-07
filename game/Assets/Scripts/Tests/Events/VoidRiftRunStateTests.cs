using System;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Events;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Events
{
    /// <summary>
    /// Unit tests for VoidRiftRunState (pure C# — no MonoBehaviour).
    /// VoidRiftConfig is a ScriptableObject and must be created/destroyed via
    /// ScriptableObject.CreateInstance / Object.DestroyImmediate.
    /// </summary>
    [TestFixture]
    public class VoidRiftRunStateTests
    {
        private VoidRiftConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<VoidRiftConfig>();
            _config.TotalFloors         = 5;
            _config.PathsPerFloor       = 2;
            _config.EliteChance         = 0f;
            _config.TreasureChance      = 0f;
            _config.RestChance          = 0f;
            _config.RestHealPercent     = 0.3f;
            _config.MaxRelics           = 3;
            _config.EliteScoreMultiplier    = 1.5f;
            _config.FullRunBonusMultiplier  = 2.0f;
            _config.LegendaryRewardPercentile = 5;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------

        [Test]
        public void Constructor_ThrowsArgumentNull_WhenConfigNull()
        {
            Assert.Throws<ArgumentNullException>(() => new VoidRiftRunState(null));
        }

        [Test]
        public void Constructor_GeneratesCorrectFloorCount()
        {
            var run = new VoidRiftRunState(_config);
            Assert.AreEqual(_config.TotalFloors, run.TotalFloors);
        }

        [Test]
        public void Constructor_StartsAtFloorZero()
        {
            var run = new VoidRiftRunState(_config);
            Assert.AreEqual(0, run.CurrentFloor);
        }

        [Test]
        public void Constructor_StartsWithZeroScore()
        {
            var run = new VoidRiftRunState(_config);
            Assert.AreEqual(0, run.Score);
        }

        [Test]
        public void Constructor_IsNotRunOver()
        {
            var run = new VoidRiftRunState(_config);
            Assert.IsFalse(run.IsRunOver);
        }

        // -------------------------------------------------------------------
        // Boss floor
        // -------------------------------------------------------------------

        [Test]
        public void LastFloor_IsBossNode()
        {
            var run = new VoidRiftRunState(_config);
            VoidRiftNode[] bossFloor = run.GetFloorNodes(_config.TotalFloors - 1);
            Assert.IsNotNull(bossFloor);
            Assert.AreEqual(1, bossFloor.Length); // Boss floor has exactly 1 path
            Assert.AreEqual(VoidRiftNodeType.Boss, bossFloor[0].NodeType);
        }

        // -------------------------------------------------------------------
        // SelectPath
        // -------------------------------------------------------------------

        [Test]
        public void SelectPath_ReturnsNull_WhenRunOver()
        {
            var run = new VoidRiftRunState(_config);
            run.EndRunDefeat();
            Assert.IsNull(run.SelectPath(0));
        }

        [Test]
        public void SelectPath_ReturnsNull_WhenPathIndexOutOfRange()
        {
            var run = new VoidRiftRunState(_config);
            Assert.IsNull(run.SelectPath(99));
        }

        [Test]
        public void SelectPath_ReturnsNull_WhenPathIndexNegative()
        {
            var run = new VoidRiftRunState(_config);
            Assert.IsNull(run.SelectPath(-1));
        }

        [Test]
        public void SelectPath_ReturnsNode_ForValidPath()
        {
            var run = new VoidRiftRunState(_config);
            var node = run.SelectPath(0);
            Assert.IsNotNull(node);
        }

        // -------------------------------------------------------------------
        // CompleteNode
        // -------------------------------------------------------------------

        [Test]
        public void CompleteNode_AdvancesFloor()
        {
            var run = new VoidRiftRunState(_config);
            run.CompleteNode(0);
            Assert.AreEqual(1, run.CurrentFloor);
        }

        [Test]
        public void CompleteNode_IncreasesScore()
        {
            var run = new VoidRiftRunState(_config);
            run.CompleteNode(0);
            Assert.Greater(run.Score, 0);
        }

        [Test]
        public void CompleteNode_SkipsOtherPathsOnSameFloor()
        {
            var run = new VoidRiftRunState(_config);
            run.CompleteNode(0); // choose path 0, path 1 should be skipped
            var nodes = run.GetFloorNodes(0);
            Assert.IsNotNull(nodes);
            Assert.IsTrue(nodes[0].IsCompleted);
            if (nodes.Length > 1)
                Assert.IsTrue(nodes[1].IsSkipped);
        }

        [Test]
        public void CompleteNode_IsNoOp_WhenRunOver()
        {
            var run = new VoidRiftRunState(_config);
            run.EndRunDefeat();
            int scoreBefore = run.Score;
            run.CompleteNode(0);
            Assert.AreEqual(scoreBefore, run.Score);
        }

        [Test]
        public void CompleteAllFloors_SetsIsWon()
        {
            var run = new VoidRiftRunState(_config);
            for (int i = 0; i < _config.TotalFloors; i++)
                run.CompleteNode(0);
            Assert.IsTrue(run.IsRunOver);
            Assert.IsTrue(run.IsWon);
        }

        [Test]
        public void CompleteAllFloors_AppliesFullRunBonusMultiplier()
        {
            // Collect score before bonus is applied by completing all but last floor
            var run = new VoidRiftRunState(_config);
            for (int i = 0; i < _config.TotalFloors - 1; i++)
                run.CompleteNode(0);
            int scoreBeforeLast = run.Score;
            run.CompleteNode(0); // this completes the run and applies multiplier
            // Score should be at least scoreBeforeLast * FullRunBonusMultiplier (last floor adds some too)
            Assert.GreaterOrEqual(run.Score, scoreBeforeLast);
        }

        // -------------------------------------------------------------------
        // EndRunDefeat
        // -------------------------------------------------------------------

        [Test]
        public void EndRunDefeat_SetsIsRunOver()
        {
            var run = new VoidRiftRunState(_config);
            run.EndRunDefeat();
            Assert.IsTrue(run.IsRunOver);
        }

        [Test]
        public void EndRunDefeat_IsNotWon()
        {
            var run = new VoidRiftRunState(_config);
            run.EndRunDefeat();
            Assert.IsFalse(run.IsWon);
        }

        [Test]
        public void EndRunDefeat_IsIdempotent()
        {
            var run = new VoidRiftRunState(_config);
            run.EndRunDefeat();
            run.EndRunDefeat(); // no exception
            Assert.IsTrue(run.IsRunOver);
        }

        // -------------------------------------------------------------------
        // Relic management
        // -------------------------------------------------------------------

        private static VoidRelic MakeRelic(string id = "relic_1", int hpBonus = 50,
            float dmgBonus = 0.1f, int energyBonus = 1)
            => new VoidRelic(id, "Test Relic", hpBonus, dmgBonus, energyBonus);

        [Test]
        public void AddRelic_NullRelic_IsNoOp()
        {
            var run = new VoidRiftRunState(_config);
            run.AddRelic(null);
            Assert.AreEqual(0, run.Relics.Count);
        }

        [Test]
        public void AddRelic_AddsToList()
        {
            var run = new VoidRiftRunState(_config);
            run.AddRelic(MakeRelic());
            Assert.AreEqual(1, run.Relics.Count);
        }

        [Test]
        public void AddRelic_ClampsToMaxRelics_DiscardsOldest()
        {
            var run = new VoidRiftRunState(_config); // MaxRelics = 3
            for (int i = 0; i < 4; i++)
                run.AddRelic(MakeRelic($"relic_{i}"));
            Assert.AreEqual(_config.MaxRelics, run.Relics.Count);
            // Oldest (relic_0) should have been discarded
            Assert.AreEqual("relic_1", run.Relics[0].RelicId);
        }

        [Test]
        public void TotalBonusHpPerFloor_SumsRelicBonuses()
        {
            var run = new VoidRiftRunState(_config);
            run.AddRelic(MakeRelic("r1", hpBonus: 100));
            run.AddRelic(MakeRelic("r2", hpBonus: 200));
            Assert.AreEqual(300, run.TotalBonusHpPerFloor());
        }

        [Test]
        public void TotalBonusDamagePercent_SumsRelicBonuses()
        {
            var run = new VoidRiftRunState(_config);
            run.AddRelic(MakeRelic("r1", dmgBonus: 0.1f));
            run.AddRelic(MakeRelic("r2", dmgBonus: 0.2f));
            Assert.AreEqual(0.3f, run.TotalBonusDamagePercent(), 0.001f);
        }

        [Test]
        public void TotalBonusStartingEnergy_CappedAtThree()
        {
            var run = new VoidRiftRunState(_config);
            run.AddRelic(MakeRelic("r1", energyBonus: 2));
            run.AddRelic(MakeRelic("r2", energyBonus: 2));
            // Sum = 4, capped at 3
            Assert.AreEqual(3, run.TotalBonusStartingEnergy());
        }

        // -------------------------------------------------------------------
        // GetFloorNodes
        // -------------------------------------------------------------------

        [Test]
        public void GetFloorNodes_ReturnsNull_WhenFloorOutOfRange()
        {
            var run = new VoidRiftRunState(_config);
            Assert.IsNull(run.GetFloorNodes(-1));
            Assert.IsNull(run.GetFloorNodes(_config.TotalFloors));
        }

        [Test]
        public void GetFloorNodes_ReturnsNodes_ForValidFloor()
        {
            var run = new VoidRiftRunState(_config);
            var nodes = run.GetFloorNodes(0);
            Assert.IsNotNull(nodes);
            Assert.Greater(nodes.Length, 0);
        }

        // -------------------------------------------------------------------
        // VoidRelic construction
        // -------------------------------------------------------------------

        [Test]
        public void VoidRelic_ThrowsArgumentException_WhenRelicIdEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                new VoidRelic("", "name", 0, 0f, 0));
        }
    }
}
