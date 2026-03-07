// Unity Test Framework (NUnit) tests for BuildingManager.
// Location: Assets/Scripts/Tests/Empire/ — Unity discovers automatically.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.Tests.Empire
{
    /// <summary>
    /// Unit tests for BuildingManager covering placement, upgrade queue, speedup,
    /// timer limits, unique building enforcement, and queue-full rejection.
    /// </summary>
    [TestFixture]
    public class BuildingManagerTests
    {
        private GameObject _go;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;
        private EmpireConfig _empireConfig;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BuildingManagerTest");

            _empireConfig = ScriptableObject.CreateInstance<EmpireConfig>();
            _empireConfig.StartingStone = 99999;
            _empireConfig.StartingIron = 99999;
            _empireConfig.StartingGrain = 99999;
            _empireConfig.StartingArcaneEssence = 99999;
            _empireConfig.DefaultMaxStone = 999999;
            _empireConfig.DefaultMaxIron = 999999;
            _empireConfig.DefaultMaxGrain = 999999;
            _empireConfig.DefaultMaxArcaneEssence = 999999;

            _resourceManager = _go.AddComponent<ResourceManager>();
            SetPrivateField(_resourceManager, "_config", _empireConfig);
            InvokePrivateMethod(_resourceManager, "InitializeFromConfig");

            _buildingManager = _go.AddComponent<BuildingManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_empireConfig);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static BuildingData MakeBuildingData(string id,
            BuildingCategory category = BuildingCategory.Resource,
            bool isUnique = false,
            int tierCount = 3)
        {
            var data = ScriptableObject.CreateInstance<BuildingData>();
            data.buildingId = id;
            data.displayName = id;
            data.category = category;
            data.isUniquePerCity = isUnique;

            var tiers = new BuildingTierData[tierCount];
            for (int i = 0; i < tierCount; i++)
            {
                tiers[i] = new BuildingTierData
                {
                    tierLabel = $"Level {i + 1}",
                    stoneCost = 100 * (i + 1),
                    ironCost = 80 * (i + 1),
                    grainCost = 60 * (i + 1),
                    arcaneEssenceCost = 0,
                    buildTimeSeconds = 60 * (i + 1),
                    bonuses = new List<BuildingBonus>()
                };
            }
            data.tiers = tiers;
            return data;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(target, null);
        }

        // ─── PlaceBuilding ────────────────────────────────────────────────────────

        [Test]
        public void PlaceBuilding_ThrowsArgumentNullException_WhenDataIsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                _buildingManager.PlaceBuilding(null, new Vector2Int(0, 0)));
        }

        [Test]
        public void PlaceBuilding_ReturnsPlacedId_WhenSuccessful()
        {
            var data = MakeBuildingData("farm_1");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            Assert.IsNotNull(id);
            Assert.IsTrue(id.StartsWith("farm_1_"));
        }

        [Test]
        public void PlaceBuilding_AddsToPlacedBuildings_WhenSuccessful()
        {
            var data = MakeBuildingData("farm_2");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            Assert.IsTrue(_buildingManager.PlacedBuildings.ContainsKey(id));
        }

        [Test]
        public void PlaceBuilding_DeductsResources_WhenSuccessful()
        {
            var data = MakeBuildingData("farm_3");
            long stoneBefore = _resourceManager.Stone;
            _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            Assert.Less(_resourceManager.Stone, stoneBefore);
        }

        [Test]
        public void PlaceBuilding_ReturnsNull_WhenUniqueAndAlreadyBuilt()
        {
            var data = MakeBuildingData("stronghold", isUnique: true);
            _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            string second = _buildingManager.PlaceBuilding(data, new Vector2Int(1, 0));
            Assert.IsNull(second);
        }

        [Test]
        public void PlaceBuilding_FiresOnBuildingPlacedEvent()
        {
            bool fired = false;
            _buildingManager.OnBuildingPlaced += _ => fired = true;
            _buildingManager.PlaceBuilding(MakeBuildingData("mine_1"), new Vector2Int(0, 0));
            Assert.IsTrue(fired);
        }

        // ─── StartUpgrade ─────────────────────────────────────────────────────────

        [Test]
        public void StartUpgrade_ReturnsFalse_WhenPlacedIdNotFound()
        {
            Assert.IsFalse(_buildingManager.StartUpgrade("nonexistent_id"));
        }

        [Test]
        public void StartUpgrade_ReturnsTrue_WhenValidAndResourcesAvailable()
        {
            var data = MakeBuildingData("mine_2");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            Assert.IsTrue(_buildingManager.StartUpgrade(id));
        }

        [Test]
        public void StartUpgrade_AddsToQueue_WhenSuccessful()
        {
            var data = MakeBuildingData("mine_3");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            Assert.AreEqual(1, _buildingManager.BuildQueue.Count);
        }

        [Test]
        public void StartUpgrade_ReturnsFalse_WhenAlreadyInQueue()
        {
            var data = MakeBuildingData("mine_4");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            Assert.IsFalse(_buildingManager.StartUpgrade(id));
        }

        [Test]
        public void StartUpgrade_ReturnsFalse_WhenQueueFull()
        {
            // Queue cap = FreeQueueSlots = 2
            var data1 = MakeBuildingData("mine_5a");
            var data2 = MakeBuildingData("mine_5b");
            var data3 = MakeBuildingData("mine_5c");
            string id1 = _buildingManager.PlaceBuilding(data1, new Vector2Int(0, 0));
            string id2 = _buildingManager.PlaceBuilding(data2, new Vector2Int(1, 0));
            string id3 = _buildingManager.PlaceBuilding(data3, new Vector2Int(2, 0));
            _buildingManager.StartUpgrade(id1);
            _buildingManager.StartUpgrade(id2);
            // Third should fail — queue full (FreeQueueSlots = 2)
            Assert.IsFalse(_buildingManager.StartUpgrade(id3));
        }

        [Test]
        public void StartUpgrade_ReturnsFalse_WhenBuildingAtMaxTier()
        {
            var data = MakeBuildingData("maxTierBuilding", tierCount: 1); // Only 1 tier
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            Assert.IsFalse(_buildingManager.StartUpgrade(id)); // Already at tier 0, no tier 1
        }

        // ─── Build Timer Hard Limit ───────────────────────────────────────────────

        [Test]
        public void StartUpgrade_ClampsTimerToMaxBuildTimeOther_ForNonCoreBuildings()
        {
            var data = MakeBuildingData("oversized_farm");
            // Override tier build time to exceed the 4h cap
            data.tiers[1] = new BuildingTierData
            {
                tierLabel = "Level 2",
                stoneCost = 100, ironCost = 80, grainCost = 60, arcaneEssenceCost = 0,
                buildTimeSeconds = 99999, // Way over the 4h cap
                bonuses = new List<BuildingBonus>()
            };
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            float queuedTime = _buildingManager.BuildQueue[0].RemainingSeconds;
            Assert.LessOrEqual(queuedTime, BuildingManager.MaxBuildTimeSecondsOther);
        }

        [Test]
        public void StartUpgrade_ClampsTimerToMaxBuildTimeStronghold_ForCoreBuildings()
        {
            var data = MakeBuildingData("stronghold_core", category: BuildingCategory.Core);
            data.tiers[1] = new BuildingTierData
            {
                stoneCost = 100, ironCost = 80, grainCost = 60, arcaneEssenceCost = 0,
                buildTimeSeconds = 99999,
                bonuses = new List<BuildingBonus>()
            };
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            float queuedTime = _buildingManager.BuildQueue[0].RemainingSeconds;
            Assert.LessOrEqual(queuedTime, BuildingManager.MaxBuildTimeSecondsStronghold);
        }

        // ─── ApplySpeedup ─────────────────────────────────────────────────────────

        [Test]
        public void ApplySpeedup_ReducesRemainingTime()
        {
            var data = MakeBuildingData("speedup_test");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            float before = _buildingManager.BuildQueue[0].RemainingSeconds;
            _buildingManager.ApplySpeedup(id, 30);
            Assert.AreEqual(before - 30f, _buildingManager.BuildQueue[0].RemainingSeconds, 0.001f);
        }

        [Test]
        public void ApplySpeedup_ClampsToZero()
        {
            var data = MakeBuildingData("speedup_clamp");
            string id = _buildingManager.PlaceBuilding(data, new Vector2Int(0, 0));
            _buildingManager.StartUpgrade(id);
            _buildingManager.ApplySpeedup(id, 99999);
            Assert.AreEqual(0f, _buildingManager.BuildQueue[0].RemainingSeconds, 0.001f);
        }

        [Test]
        public void ApplySpeedup_DoesNotThrow_WhenBuildingNotInQueue()
        {
            Assert.DoesNotThrow(() => _buildingManager.ApplySpeedup("not_in_queue", 30));
        }
    }
}
