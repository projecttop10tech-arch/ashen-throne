using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Tests.Heroes
{
    [TestFixture]
    public class HeroRosterTests
    {
        private GameObject _go;
        private HeroRoster _roster;
        private HeroData _heroData;
        private ProgressionConfig _config;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();

            _go = new GameObject("HeroRosterTest");
            _roster = _go.AddComponent<HeroRoster>();

            _heroData = ScriptableObject.CreateInstance<HeroData>();
            _heroData.heroId = "test_hero_01";
            _heroData.displayName = "Test Hero";
            _heroData.rarity = HeroRarity.Rare;
            _heroData.baseHealth = 1000;
            _heroData.baseAttack = 100;
            _heroData.baseDefense = 80;
            _heroData.baseSpeed = 10;
            _heroData.shardsToUnlock = 80;
            _heroData.shardsPerStarTier = new int[] { 20, 40, 60, 80, 100 };

            _config = ScriptableObject.CreateInstance<ProgressionConfig>();
            _config.MaxHeroLevel = 80;
            _config.BaseXp = 100;
            _config.GrowthFactor = 1.12f;
            _config.XpPerLevel = new int[79];
            for (int i = 0; i < 79; i++)
                _config.XpPerLevel[i] = Mathf.RoundToInt(100 * Mathf.Pow(1.12f, i));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_heroData);
            UnityEngine.Object.DestroyImmediate(_config);
            ServiceLocator.Initialize();
            EventBus.Initialize();
        }

        // --- LoadFromSaveData ---

        [Test]
        public void LoadFromSaveData_PopulatesOwnedHeroes()
        {
            var entries = new List<HeroSaveEntry>
            {
                new HeroSaveEntry { HeroId = "hero_a", Level = 5, StarTier = 2, Loadout = null },
                new HeroSaveEntry { HeroId = "hero_b", Level = 10, StarTier = 3, Loadout = null }
            };
            var shards = new Dictionary<string, int> { { "hero_a", 50 }, { "hero_c", 30 } };

            _roster.LoadFromSaveData(entries, shards);

            Assert.AreEqual(2, _roster.OwnedHeroes.Count);
            Assert.AreEqual(5, _roster.OwnedHeroes["hero_a"].Level);
            Assert.AreEqual(10, _roster.OwnedHeroes["hero_b"].Level);
            Assert.AreEqual(50, _roster.GetShardCount("hero_a"));
            Assert.AreEqual(30, _roster.GetShardCount("hero_c"));
        }

        [Test]
        public void LoadFromSaveData_ClearsPreviousState()
        {
            var entries1 = new List<HeroSaveEntry>
            {
                new HeroSaveEntry { HeroId = "hero_old", Level = 1, StarTier = 1, Loadout = null }
            };
            _roster.LoadFromSaveData(entries1, new Dictionary<string, int>());

            var entries2 = new List<HeroSaveEntry>
            {
                new HeroSaveEntry { HeroId = "hero_new", Level = 3, StarTier = 1, Loadout = null }
            };
            _roster.LoadFromSaveData(entries2, new Dictionary<string, int>());

            Assert.IsFalse(_roster.OwnedHeroes.ContainsKey("hero_old"));
            Assert.IsTrue(_roster.OwnedHeroes.ContainsKey("hero_new"));
        }

        // --- UnlockHero ---

        [Test]
        public void UnlockHero_AddsToOwnedHeroes()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            Assert.IsTrue(_roster.OwnedHeroes.ContainsKey("test_hero_01"));
            Assert.AreEqual(1, _roster.OwnedHeroes["test_hero_01"].Level);
            Assert.AreEqual(1, _roster.OwnedHeroes["test_hero_01"].StarTier);
        }

        [Test]
        public void UnlockHero_FiresOnHeroUnlockedEvent()
        {
            string unlockedId = null;
            _roster.OnHeroUnlocked += id => unlockedId = id;

            _roster.UnlockHero("test_hero_01", _heroData);

            Assert.AreEqual("test_hero_01", unlockedId);
        }

        [Test]
        public void UnlockHero_FiresEventBusEvent()
        {
            string busEventId = null;
            var sub = EventBus.Subscribe<HeroUnlockedEvent>(e => busEventId = e.HeroId);

            _roster.UnlockHero("test_hero_01", _heroData);

            Assert.AreEqual("test_hero_01", busEventId);
            sub.Dispose();
        }

        [Test]
        public void UnlockHero_DoesNotDuplicate_IfAlreadyOwned()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            int countBefore = _roster.OwnedHeroes.Count;

            _roster.UnlockHero("test_hero_01", _heroData);

            Assert.AreEqual(countBefore, _roster.OwnedHeroes.Count);
        }

        // --- AddShards ---

        [Test]
        public void AddShards_IncreasesShardCount()
        {
            _roster.AddShards("test_hero_01", _heroData, 30);

            Assert.AreEqual(30, _roster.GetShardCount("test_hero_01"));
        }

        [Test]
        public void AddShards_AccumulatesMultipleCalls()
        {
            _roster.AddShards("test_hero_01", _heroData, 30);
            _roster.AddShards("test_hero_01", _heroData, 20);

            Assert.AreEqual(50, _roster.GetShardCount("test_hero_01"));
        }

        [Test]
        public void AddShards_FiresOnShardsAddedEvent()
        {
            int reportedTotal = -1;
            _roster.OnShardsAdded += (id, total) => reportedTotal = total;

            _roster.AddShards("test_hero_01", _heroData, 40);

            Assert.AreEqual(40, reportedTotal);
        }

        [Test]
        public void AddShards_IgnoresZeroAmount()
        {
            _roster.AddShards("test_hero_01", _heroData, 0);

            Assert.AreEqual(0, _roster.GetShardCount("test_hero_01"));
        }

        [Test]
        public void AddShards_IgnoresNegativeAmount()
        {
            _roster.AddShards("test_hero_01", _heroData, -10);

            Assert.AreEqual(0, _roster.GetShardCount("test_hero_01"));
        }

        [Test]
        public void AddShards_ThrowsOnNullHeroData()
        {
            Assert.Throws<ArgumentNullException>(() => _roster.AddShards("test_hero_01", null, 10));
        }

        [Test]
        public void AddShards_AutoUnlocksHero_WhenThresholdMet()
        {
            _roster.AddShards("test_hero_01", _heroData, 80); // shardsToUnlock = 80

            Assert.IsTrue(_roster.OwnedHeroes.ContainsKey("test_hero_01"));
        }

        [Test]
        public void AddShards_AutoUnlocksHero_WhenThresholdExceeded()
        {
            _roster.AddShards("test_hero_01", _heroData, 100);

            Assert.IsTrue(_roster.OwnedHeroes.ContainsKey("test_hero_01"));
        }

        [Test]
        public void AddShards_DoesNotAutoUnlock_BelowThreshold()
        {
            _roster.AddShards("test_hero_01", _heroData, 79);

            Assert.IsFalse(_roster.OwnedHeroes.ContainsKey("test_hero_01"));
        }

        [Test]
        public void AddShards_DoesNotReUnlock_IfAlreadyOwned()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            int unlockCount = 0;
            _roster.OnHeroUnlocked += _ => unlockCount++;

            _roster.AddShards("test_hero_01", _heroData, 100);

            Assert.AreEqual(0, unlockCount); // Should not fire again
        }

        // --- AddXp ---

        [Test]
        public void AddXp_LevelsUpHero_WhenXpSufficient()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            int xpNeeded = _config.GetXpForLevel(1); // XP to go from 1 to 2
            bool result = _roster.AddXp("test_hero_01", xpNeeded, _config);

            Assert.IsTrue(result);
            Assert.AreEqual(2, _roster.OwnedHeroes["test_hero_01"].Level);
        }

        [Test]
        public void AddXp_DoesNotLevelUp_WhenXpInsufficient()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            bool result = _roster.AddXp("test_hero_01", 1, _config);

            Assert.IsFalse(result);
            Assert.AreEqual(1, _roster.OwnedHeroes["test_hero_01"].Level);
        }

        [Test]
        public void AddXp_ReturnsFalse_ForUnownedHero()
        {
            bool result = _roster.AddXp("nonexistent", 1000, _config);

            Assert.IsFalse(result);
        }

        [Test]
        public void AddXp_ReturnsFalse_AtMaxLevel()
        {
            var entries = new List<HeroSaveEntry>
            {
                new HeroSaveEntry { HeroId = "test_hero_01", Level = 80, StarTier = 1, Loadout = null }
            };
            _roster.LoadFromSaveData(entries, new Dictionary<string, int>());

            bool result = _roster.AddXp("test_hero_01", 99999, _config);

            Assert.IsFalse(result);
        }

        [Test]
        public void AddXp_FiresOnHeroLeveledUpEvent()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            int reportedLevel = -1;
            _roster.OnHeroLeveledUp += (id, level) => reportedLevel = level;

            int xpNeeded = _config.GetXpForLevel(1);
            _roster.AddXp("test_hero_01", xpNeeded, _config);

            Assert.AreEqual(2, reportedLevel);
        }

        [Test]
        public void AddXp_CarriesOverExcessXp()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            int xpNeeded = _config.GetXpForLevel(1);
            _roster.AddXp("test_hero_01", xpNeeded + 50, _config);

            Assert.AreEqual(50, _roster.OwnedHeroes["test_hero_01"].CurrentXp);
        }

        // --- PromoteStar ---

        [Test]
        public void PromoteStar_IncreasesStarTier()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            _roster.AddShards("test_hero_01", _heroData, 200); // Plenty of shards

            bool result = _roster.PromoteStar("test_hero_01", _heroData);

            Assert.IsTrue(result);
            Assert.AreEqual(2, _roster.OwnedHeroes["test_hero_01"].StarTier);
        }

        [Test]
        public void PromoteStar_CostsShards()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            _roster.AddShards("test_hero_01", _heroData, 200);
            int shardsBefore = _roster.GetShardCount("test_hero_01");

            _roster.PromoteStar("test_hero_01", _heroData);

            int expectedCost = _heroData.shardsPerStarTier[0]; // Star 1→2 cost
            Assert.AreEqual(shardsBefore - expectedCost, _roster.GetShardCount("test_hero_01"));
        }

        [Test]
        public void PromoteStar_ReturnsFalse_WithInsufficientShards()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            // No shards added — cost is 20 for tier 1→2

            bool result = _roster.PromoteStar("test_hero_01", _heroData);

            Assert.IsFalse(result);
            Assert.AreEqual(1, _roster.OwnedHeroes["test_hero_01"].StarTier);
        }

        [Test]
        public void PromoteStar_ReturnsFalse_AtMaxStarTier()
        {
            var entries = new List<HeroSaveEntry>
            {
                new HeroSaveEntry { HeroId = "test_hero_01", Level = 1, StarTier = 6, Loadout = null }
            };
            _roster.LoadFromSaveData(entries, new Dictionary<string, int> { { "test_hero_01", 999 } });

            bool result = _roster.PromoteStar("test_hero_01", _heroData);

            Assert.IsFalse(result);
        }

        [Test]
        public void PromoteStar_ReturnsFalse_ForUnownedHero()
        {
            bool result = _roster.PromoteStar("nonexistent", _heroData);

            Assert.IsFalse(result);
        }

        [Test]
        public void PromoteStar_FiresOnHeroStarPromotedEvent()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            _roster.AddShards("test_hero_01", _heroData, 200);
            int reportedStar = -1;
            _roster.OnHeroStarPromoted += (id, star) => reportedStar = star;

            _roster.PromoteStar("test_hero_01", _heroData);

            Assert.AreEqual(2, reportedStar);
        }

        // --- SaveLoadout ---

        [Test]
        public void SaveLoadout_StoresCardIds()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            var loadout = MakeDummyLoadout(15);

            bool result = _roster.SaveLoadout("test_hero_01", loadout);

            Assert.IsTrue(result);
            Assert.AreEqual(15, _roster.OwnedHeroes["test_hero_01"].Loadout.Count);
        }

        [Test]
        public void SaveLoadout_ReturnsFalse_ForWrongDeckSize()
        {
            _roster.UnlockHero("test_hero_01", _heroData);
            var loadout = MakeDummyLoadout(10); // Should be 15

            bool result = _roster.SaveLoadout("test_hero_01", loadout);

            Assert.IsFalse(result);
        }

        [Test]
        public void SaveLoadout_ReturnsFalse_ForNullCardIds()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            bool result = _roster.SaveLoadout("test_hero_01", null);

            Assert.IsFalse(result);
        }

        [Test]
        public void SaveLoadout_ReturnsFalse_ForUnownedHero()
        {
            var loadout = MakeDummyLoadout(15);

            bool result = _roster.SaveLoadout("nonexistent", loadout);

            Assert.IsFalse(result);
        }

        // --- GetHero / GetShardCount ---

        [Test]
        public void GetHero_ReturnsNull_ForUnownedHero()
        {
            Assert.IsNull(_roster.GetHero("nonexistent"));
        }

        [Test]
        public void GetHero_ReturnsOwnedHero()
        {
            _roster.UnlockHero("test_hero_01", _heroData);

            var hero = _roster.GetHero("test_hero_01");

            Assert.IsNotNull(hero);
            Assert.AreEqual("test_hero_01", hero.HeroId);
        }

        [Test]
        public void GetShardCount_ReturnsZero_ForUnknownHero()
        {
            Assert.AreEqual(0, _roster.GetShardCount("unknown"));
        }

        // --- Helpers ---

        private List<string> MakeDummyLoadout(int count)
        {
            var list = new List<string>();
            for (int i = 0; i < count; i++)
                list.Add($"card_{Guid.NewGuid():N}");
            return list;
        }
    }
}
