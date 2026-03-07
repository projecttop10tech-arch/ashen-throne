using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Alliance;
using AshenThrone.Data;

namespace AshenThrone.Tests.Alliance
{
    [TestFixture]
    public class TerritorySystemTests
    {
        private GameObject _go;
        private TerritoryManager _manager;
        private TerritoryConfig _config;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TerritorySystemTest");
            _manager = _go.AddComponent<TerritoryManager>();

            _config = ScriptableObject.CreateInstance<TerritoryConfig>();

            // Inject config via reflection
            var field = typeof(TerritoryManager).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_manager, _config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_config);
        }

        // --- HexCoord ---

        [Test]
        public void HexCoord_Distance_AdjacentCells_ReturnsOne()
        {
            var a = new HexCoord(0, 0);
            var b = new HexCoord(1, 0);
            Assert.AreEqual(1, HexCoord.Distance(a, b));
        }

        [Test]
        public void HexCoord_Distance_SameCell_ReturnsZero()
        {
            var a = new HexCoord(3, -2);
            Assert.AreEqual(0, HexCoord.Distance(a, a));
        }

        [Test]
        public void HexCoord_Distance_TwoCellsApart_ReturnsTwo()
        {
            var a = new HexCoord(0, 0);
            var b = new HexCoord(2, 0);
            Assert.AreEqual(2, HexCoord.Distance(a, b));
        }

        [Test]
        public void HexCoord_Neighbors_ReturnsExactlySix()
        {
            var neighbors = new HexCoord(0, 0).Neighbors();
            Assert.AreEqual(6, neighbors.Length);
        }

        [Test]
        public void HexCoord_Equality_SameValues_AreEqual()
        {
            Assert.AreEqual(new HexCoord(1, 2), new HexCoord(1, 2));
        }

        [Test]
        public void HexCoord_Equality_DifferentValues_NotEqual()
        {
            Assert.AreNotEqual(new HexCoord(1, 2), new HexCoord(1, 3));
        }

        // --- InitializeMap ---

        [Test]
        public void InitializeMap_Throws_WhenRegionsIsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => _manager.InitializeMap(null));
        }

        [Test]
        public void InitializeMap_PopulatesRegions()
        {
            var regions = MakeRegions(3);
            _manager.InitializeMap(regions);
            Assert.IsNotNull(_manager.GetRegion("region_0"));
            Assert.IsNotNull(_manager.GetRegion("region_1"));
            Assert.IsNotNull(_manager.GetRegion("region_2"));
        }

        [Test]
        public void InitializeMap_IgnoresNullEntries()
        {
            var regions = new List<TerritoryRegion> { MakeRegion("r1", 0, 0), null, MakeRegion("r2", 1, 0) };
            Assert.DoesNotThrow(() => _manager.InitializeMap(regions));
            Assert.IsNotNull(_manager.GetRegion("r1"));
        }

        // --- LoadFromServerData ---

        [Test]
        public void LoadFromServerData_SetsOwnership()
        {
            _manager.InitializeMap(MakeRegions(2));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = "alliance_A", FortificationTier = 1, FortificationHp = 5000 }
            });
            var region = _manager.GetRegion("region_0");
            Assert.AreEqual("alliance_A", region.OwnerAllianceId);
            Assert.AreEqual(1, region.FortificationTier);
        }

        [Test]
        public void LoadFromServerData_NeutralRegions_HaveNullOwner()
        {
            _manager.InitializeMap(MakeRegions(1));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = null }
            });
            Assert.IsTrue(_manager.GetRegion("region_0").IsNeutral);
        }

        // --- GetTerritoryCount ---

        [Test]
        public void GetTerritoryCount_ReturnsCorrectCount_AfterLoad()
        {
            _manager.InitializeMap(MakeRegions(3));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = "A" },
                new TerritorySaveEntry { RegionId = "region_1", OwnerAllianceId = "A" },
                new TerritorySaveEntry { RegionId = "region_2", OwnerAllianceId = "B" }
            });
            Assert.AreEqual(2, _manager.GetTerritoryCount("A"));
            Assert.AreEqual(1, _manager.GetTerritoryCount("B"));
        }

        [Test]
        public void GetTerritoryCount_ReturnsZero_ForUnknownAlliance()
        {
            _manager.InitializeMap(MakeRegions(1));
            Assert.AreEqual(0, _manager.GetTerritoryCount("nonexistent"));
        }

        // --- ApplyCapture ---

        [Test]
        public void ApplyCapture_ChangesOwnership()
        {
            _manager.InitializeMap(MakeRegions(2));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = "A" }
            });
            _manager.ApplyCapture("region_0", "B");
            Assert.AreEqual("B", _manager.GetRegion("region_0").OwnerAllianceId);
        }

        [Test]
        public void ApplyCapture_UpdatesAllianceIndexes()
        {
            _manager.InitializeMap(MakeRegions(1));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = "A" }
            });
            _manager.ApplyCapture("region_0", "B");
            Assert.AreEqual(0, _manager.GetTerritoryCount("A"));
            Assert.AreEqual(1, _manager.GetTerritoryCount("B"));
        }

        // --- AreAdjacent ---

        [Test]
        public void AreAdjacent_AdjacentHexes_ReturnsTrue()
        {
            var r1 = MakeRegion("r1", 0, 0);
            var r2 = MakeRegion("r2", 1, 0);
            _manager.InitializeMap(new List<TerritoryRegion> { r1, r2 });
            Assert.IsTrue(_manager.AreAdjacent("r1", "r2"));
        }

        [Test]
        public void AreAdjacent_NonAdjacentHexes_ReturnsFalse()
        {
            var r1 = MakeRegion("r1", 0, 0);
            var r2 = MakeRegion("r2", 3, 0);
            _manager.InitializeMap(new List<TerritoryRegion> { r1, r2 });
            Assert.IsFalse(_manager.AreAdjacent("r1", "r2"));
        }

        // --- IsAttackable ---

        [Test]
        public void IsAttackable_ReturnsFalse_WhenAllianceOwnsRegion()
        {
            _manager.InitializeMap(MakeRegions(1));
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "region_0", OwnerAllianceId = "A" }
            });
            Assert.IsFalse(_manager.IsAttackable("region_0", "A"));
        }

        [Test]
        public void IsAttackable_ReturnsFalse_WhenNoAdjacentOwnedRegion()
        {
            var r1 = MakeRegion("r1", 0, 0, TerritoryType.Neutral);
            var r2 = MakeRegion("r2", 5, 5, TerritoryType.Neutral);
            _manager.InitializeMap(new List<TerritoryRegion> { r1, r2 });
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "r1", OwnerAllianceId = "A" }
            });
            Assert.IsFalse(_manager.IsAttackable("r2", "A")); // r2 is not adjacent to r1
        }

        [Test]
        public void IsAttackable_ReturnsTrue_WhenAdjacentOwnedRegionExists()
        {
            var r1 = MakeRegion("r1", 0, 0, TerritoryType.Resource);
            var r2 = MakeRegion("r2", 1, 0, TerritoryType.Neutral);
            _manager.InitializeMap(new List<TerritoryRegion> { r1, r2 });
            _manager.LoadFromServerData(new List<TerritorySaveEntry>
            {
                new TerritorySaveEntry { RegionId = "r1", OwnerAllianceId = "A" }
            });
            Assert.IsTrue(_manager.IsAttackable("r2", "A"));
        }

        // --- TerritoryRegion fortification ---

        [Test]
        public void TerritoryRegion_TakeFortificationDamage_ReducesHp()
        {
            var region = MakeRegion("r", 0, 0);
            region.HydrateFromSave("A", 1, 5000);
            region.TakeFortificationDamage(1000);
            Assert.AreEqual(4000, region.FortificationHp);
        }

        [Test]
        public void TerritoryRegion_TakeFortificationDamage_ReturnsTrue_WhenDestroyed()
        {
            var region = MakeRegion("r", 0, 0);
            region.HydrateFromSave("A", 1, 100);
            bool destroyed = region.TakeFortificationDamage(200);
            Assert.IsTrue(destroyed);
            Assert.AreEqual(0, region.FortificationHp);
        }

        [Test]
        public void TerritoryRegion_TakeFortificationDamage_DoesNotGoBelowZero()
        {
            var region = MakeRegion("r", 0, 0);
            region.HydrateFromSave("A", 1, 100);
            region.TakeFortificationDamage(99999);
            Assert.AreEqual(0, region.FortificationHp);
        }

        // --- Helpers ---

        private static List<TerritoryRegion> MakeRegions(int count)
        {
            var list = new List<TerritoryRegion>(count);
            for (int i = 0; i < count; i++)
                list.Add(MakeRegion($"region_{i}", i, 0));
            return list;
        }

        private static TerritoryRegion MakeRegion(string id, int q, int r, TerritoryType type = TerritoryType.Resource)
        {
            return new TerritoryRegion(id, new HexCoord(q, r), type, id);
        }
    }
}
