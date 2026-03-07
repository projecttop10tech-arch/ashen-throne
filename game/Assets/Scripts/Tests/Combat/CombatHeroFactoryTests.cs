// Unity Test Framework (NUnit) tests for CombatHeroFactory.
// Location: Assets/Scripts/Tests/Combat/ — Unity discovers automatically.
// All tests must pass before Phase 1 is marked complete.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Tests.Combat
{
    /// <summary>
    /// Unit tests for CombatHeroFactory. Tests pure C# logic only — no Unity scene required.
    /// </summary>
    [TestFixture]
    public class CombatHeroFactoryTests
    {
        private ProgressionConfig _progression;
        private CombatConfig _combatConfig;
        private HeroData _heroData;
        private OwnedHero _ownedHero;

        [SetUp]
        public void SetUp()
        {
            _progression = ScriptableObject.CreateInstance<ProgressionConfig>();
            _progression.StatGrowthPerLevel = 0.08f;
            _progression.MaxHeroLevel = 80;

            _combatConfig = ScriptableObject.CreateInstance<CombatConfig>();
            _combatConfig.DeckSize = 15;

            _heroData = MakeHeroData();
            _ownedHero = new OwnedHero("test_hero", level: 10, starTier: 2, loadout: null);

            CombatHero.ResetInstanceIdForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_progression);
            Object.DestroyImmediate(_combatConfig);
            Object.DestroyImmediate(_heroData);
            CombatHero.ResetInstanceIdForTesting();
        }

        // ─── Construction ────────────────────────────────────────────────────────

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenProgressionIsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CombatHeroFactory(null, 15));
        }

        [Test]
        public void Constructor_CreatesFactory_WithValidArgs()
        {
            Assert.DoesNotThrow(() => new CombatHeroFactory(_progression, 15));
        }

        // ─── CreatePlayerHero ─────────────────────────────────────────────────────

        [Test]
        public void CreatePlayerHero_ReturnsPlayerOwnedHero()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            CombatHero hero = factory.CreatePlayerHero(_heroData, _ownedHero);

            Assert.IsNotNull(hero);
            Assert.IsTrue(hero.IsPlayerOwned, "Hero created from roster should be player-owned");
        }

        [Test]
        public void CreatePlayerHero_ScalesStatsToProvidedLevel()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            CombatHero heroL1 = factory.CreatePlayerHero(_heroData,
                new OwnedHero("h", level: 1, starTier: 1, loadout: null));
            CombatHero heroL10 = factory.CreatePlayerHero(_heroData,
                new OwnedHero("h", level: 10, starTier: 1, loadout: null));

            Assert.Greater(heroL10.MaxHealth, heroL1.MaxHealth, "Level 10 hero should have more HP than level 1");
        }

        [Test]
        public void CreatePlayerHero_ThrowsArgumentNullException_WhenHeroDataNull()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            Assert.Throws<System.ArgumentNullException>(() =>
                factory.CreatePlayerHero(null, _ownedHero));
        }

        [Test]
        public void CreatePlayerHero_ThrowsArgumentNullException_WhenOwnedDataNull()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            Assert.Throws<System.ArgumentNullException>(() =>
                factory.CreatePlayerHero(_heroData, null));
        }

        // ─── CreateEnemyHero ──────────────────────────────────────────────────────

        [Test]
        public void CreateEnemyHero_ReturnsAiOwnedHero()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            CombatHero enemy = factory.CreateEnemyHero(_heroData, enemyLevel: 5);

            Assert.IsNotNull(enemy);
            Assert.IsFalse(enemy.IsPlayerOwned, "Enemy hero must not be player-owned");
        }

        [Test]
        public void CreateEnemyHero_ThrowsWhenLevelLessThanOne()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                factory.CreateEnemyHero(_heroData, enemyLevel: 0));
        }

        [Test]
        public void CreateEnemyHero_ClampsLevelToMaxHeroLevel()
        {
            _progression.MaxHeroLevel = 80;
            var factory = new CombatHeroFactory(_progression, 15);
            // Level 9999 should be clamped to 80 — must not throw
            Assert.DoesNotThrow(() => factory.CreateEnemyHero(_heroData, enemyLevel: 9999));
        }

        // ─── BuildPlayerLoadout ───────────────────────────────────────────────────

        [Test]
        public void BuildPlayerLoadout_ReturnsExactlyDeckSizeCards_WhenNoSavedLoadout()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            List<AbilityCardData> loadout = factory.BuildPlayerLoadout(_heroData, _ownedHero);

            Assert.AreEqual(15, loadout.Count, "Loadout must always contain exactly DeckSize cards");
        }

        [Test]
        public void BuildPlayerLoadout_ReturnsExactlyDeckSizeCards_WhenLoadoutShorterThanDeckSize()
        {
            // Save a loadout with only 3 cards — factory should pad to 15
            var shortLoadout = new List<string> { "card_a", "card_b", "card_c" };
            var owned = new OwnedHero("h", level: 1, starTier: 1, loadout: shortLoadout);
            AddCardsToPool(_heroData, new[] { "card_a", "card_b", "card_c" }, count: 3);

            var factory = new CombatHeroFactory(_progression, 15);
            List<AbilityCardData> loadout = factory.BuildPlayerLoadout(_heroData, owned);

            Assert.AreEqual(15, loadout.Count, "Factory must pad short loadout to full deck size");
        }

        [Test]
        public void BuildEnemyLoadout_ReturnsExactlyDeckSizeCards()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            List<AbilityCardData> loadout = factory.BuildEnemyLoadout(_heroData);

            Assert.AreEqual(15, loadout.Count, "Enemy loadout must always be full deck size");
        }

        [Test]
        public void BuildPlayerLoadout_ThrowsArgumentNullException_WhenHeroDataNull()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            Assert.Throws<System.ArgumentNullException>(() =>
                factory.BuildPlayerLoadout(null, _ownedHero));
        }

        // ─── PlaceHeroesOnGrid ────────────────────────────────────────────────────

        [Test]
        public void PlaceHeroesOnGrid_ThrowsArgumentNullException_WhenGridNull()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            var playerHero = factory.CreatePlayerHero(_heroData, _ownedHero);
            var playerList = new List<CombatHero> { playerHero };

            Assert.Throws<System.ArgumentNullException>(() =>
                factory.PlaceHeroesOnGrid(playerList, new List<CombatHero>(), null));
        }

        [Test]
        public void PlaceHeroesOnGrid_ThrowsArgumentException_WhenPlayerSquadExceedsThree()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            var squad = new List<CombatHero>
            {
                factory.CreatePlayerHero(_heroData, _ownedHero),
                factory.CreatePlayerHero(MakeHeroData(), new OwnedHero("h2", 1, 1, null)),
                factory.CreatePlayerHero(MakeHeroData(), new OwnedHero("h3", 1, 1, null)),
                factory.CreatePlayerHero(MakeHeroData(), new OwnedHero("h4", 1, 1, null)) // 4th hero
            };

            var grid = new GameObject("TestGrid").AddComponent<CombatGrid>();
            Assert.Throws<System.ArgumentException>(() =>
                factory.PlaceHeroesOnGrid(squad, new List<CombatHero>(), grid));
            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void PlaceHeroesOnGrid_PlacesHeroesInPlayerZone()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            var playerHero = factory.CreatePlayerHero(_heroData, _ownedHero);

            var grid = new GameObject("TestGrid").AddComponent<CombatGrid>();
            factory.PlaceHeroesOnGrid(
                new List<CombatHero> { playerHero },
                new List<CombatHero>(),
                grid);

            GridPosition? pos = grid.GetUnitPosition(playerHero.InstanceId);
            Assert.IsTrue(pos.HasValue, "Player hero must be placed on the grid");
            Assert.IsTrue(grid.IsPlayerZone(pos.Value), "Player hero must be in columns 0-2");
            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void PlaceHeroesOnGrid_PlacesEnemiesInEnemyZone()
        {
            var factory = new CombatHeroFactory(_progression, 15);
            var enemyHero = factory.CreateEnemyHero(_heroData, enemyLevel: 5);

            var grid = new GameObject("TestGrid").AddComponent<CombatGrid>();
            factory.PlaceHeroesOnGrid(
                new List<CombatHero>(),
                new List<CombatHero> { enemyHero },
                grid);

            GridPosition? pos = grid.GetUnitPosition(enemyHero.InstanceId);
            Assert.IsTrue(pos.HasValue, "Enemy hero must be placed on the grid");
            Assert.IsTrue(grid.IsEnemyZone(pos.Value), "Enemy hero must be in columns 4-6");
            Object.DestroyImmediate(grid.gameObject);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private HeroData MakeHeroData()
        {
            var data = ScriptableObject.CreateInstance<HeroData>();
            data.heroId = $"test_hero_{System.Guid.NewGuid():N}";
            data.baseHealth = 1000;
            data.baseAttack = 100;
            data.baseDefense = 80;
            data.baseSpeed = 10;
            data.baseCritChance = 0f;
            data.preferredRow = CombatRow.Front;
            data.abilityPool = new List<AbilityCardData>();

            // Populate the ability pool with 5 test cards (repeat to cover deck of 15)
            for (int i = 0; i < 5; i++)
            {
                var card = ScriptableObject.CreateInstance<AbilityCardData>();
                card.cardId = $"test_card_{i}_{System.Guid.NewGuid():N}";
                card.cardType = CardType.Attack;
                card.targetType = CardTargetType.SingleEnemy;
                card.energyCost = 2;
                card.requiredHeroStarTier = 1;
                data.abilityPool.Add(card);
            }
            return data;
        }

        private void AddCardsToPool(HeroData data, string[] cardIds, int count)
        {
            data.abilityPool.Clear();
            for (int i = 0; i < count; i++)
            {
                var card = ScriptableObject.CreateInstance<AbilityCardData>();
                card.cardId = cardIds[i];
                card.cardType = CardType.Attack;
                card.targetType = CardTargetType.SingleEnemy;
                card.energyCost = 2;
                card.requiredHeroStarTier = 1;
                data.abilityPool.Add(card);
            }
        }
    }
}
