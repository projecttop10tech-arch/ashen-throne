using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Tests.Integration
{
    [TestFixture]
    public class CombatFlowTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            CombatHero.ResetInstanceIdForTesting();
            _go = new GameObject("CombatFlowTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void CombatGrid_Creates_AsMonoBehaviour()
        {
            var grid = _go.AddComponent<CombatGrid>();
            Assert.IsNotNull(grid);
            Assert.AreEqual(7, CombatGrid.Columns);
            Assert.AreEqual(5, CombatGrid.Rows);
        }

        [Test]
        public void CombatGrid_PlaceUnit_SucceedsAtValidPosition()
        {
            var grid = _go.AddComponent<CombatGrid>();
            bool placed = grid.PlaceUnit(1, new GridPosition(1, 2));
            Assert.IsTrue(placed);
        }

        [Test]
        public void CombatGrid_PlaceUnit_FailsAtOccupiedPosition()
        {
            var grid = _go.AddComponent<CombatGrid>();
            grid.PlaceUnit(1, new GridPosition(1, 2));
            bool placed = grid.PlaceUnit(2, new GridPosition(1, 2));
            Assert.IsFalse(placed);
        }

        [Test]
        public void CombatHero_TakeDamage_ReducesHealth()
        {
            var hero = CreateTestHero();
            int initialHp = hero.CurrentHealth;
            hero.TakeDamage(200, DamageType.Physical);
            Assert.Less(hero.CurrentHealth, initialHp);
        }

        [Test]
        public void CombatHero_TakeFatalDamage_KillsHero()
        {
            var hero = CreateTestHero();
            hero.TakeDamage(99999, DamageType.Physical);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void CombatHero_Heal_DoesNotExceedMaxHealth()
        {
            var hero = CreateTestHero();
            hero.TakeDamage(100, DamageType.Physical);
            hero.Heal(99999);
            Assert.AreEqual(hero.MaxHealth, hero.CurrentHealth);
        }

        [Test]
        public void CombatHero_Dead_CannotHeal()
        {
            var hero = CreateTestHero();
            hero.TakeDamage(99999, DamageType.Physical);
            Assert.IsFalse(hero.IsAlive);
            hero.Heal(500);
            Assert.IsFalse(hero.IsAlive);
        }

        [Test]
        public void CardHandManager_Creates_AsMonoBehaviour()
        {
            var manager = _go.AddComponent<CardHandManager>();
            Assert.IsNotNull(manager);
        }

        [Test]
        public void EventBus_Publish_ReceivesCombatEvent()
        {
            bool received = false;
            var sub = EventBus.Subscribe<AppStateChangedEvent>(e => received = true);
            EventBus.Publish(new AppStateChangedEvent(AppState.Boot, AppState.Combat));
            Assert.IsTrue(received);
            sub.Dispose();
        }

        [Test]
        public void CombatGrid_IsInBounds_ValidPosition()
        {
            var grid = _go.AddComponent<CombatGrid>();
            Assert.IsTrue(grid.IsInBounds(new GridPosition(3, 2)));
        }

        [Test]
        public void CombatGrid_IsInBounds_InvalidPosition()
        {
            var grid = _go.AddComponent<CombatGrid>();
            Assert.IsFalse(grid.IsInBounds(new GridPosition(10, 10)));
        }

        private CombatHero CreateTestHero()
        {
            var heroData = ScriptableObject.CreateInstance<HeroData>();
            var progConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            return new CombatHero(heroData, 1, true, progConfig);
        }
    }
}
