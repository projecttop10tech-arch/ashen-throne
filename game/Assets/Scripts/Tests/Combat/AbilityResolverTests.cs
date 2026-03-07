// Unity Test Framework (NUnit) tests for AbilityResolver.
// These tests run in Unity's Test Runner (Edit Mode).
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
    /// Unit tests for AbilityResolver. Tests pure C# logic only — no Unity scene required.
    /// </summary>
    [TestFixture]
    public class AbilityResolverTests
    {
        private CombatConfig _config;
        private ProgressionConfig _progression;

        [SetUp]
        public void SetUp()
        {
            // Create minimal config ScriptableObjects for testing
            _config = ScriptableObject.CreateInstance<CombatConfig>();
            _config.MinimumDamage = 1;
            _config.MinimumHeal = 1;
            _config.MitigationDivisor = 4;
            _config.CriticalHitMultiplier = 1.5f;
            _config.ShadowVeilMissChance = 0f; // Disable misses in tests for determinism
            _config.MarkedDamageBonus = 1.25f;
            _config.HighGroundDamageMultiplier = 1.25f;
            _config.FactionBonus2Heroes = 0.10f;
            _config.FactionBonus3Heroes = 0.15f;

            _progression = ScriptableObject.CreateInstance<ProgressionConfig>();
            _progression.StatGrowthPerLevel = 0.08f;
            _progression.MaxHeroLevel = 80;

            CombatHero.ResetInstanceIdForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_progression);
            CombatHero.ResetInstanceIdForTesting();
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private HeroData MakeHeroData(int baseHp = 1000, int baseAtk = 100, int baseDef = 80)
        {
            var data = ScriptableObject.CreateInstance<HeroData>();
            data.heroId = $"test_hero_{System.Guid.NewGuid():N}";
            data.baseHealth = baseHp;
            data.baseAttack = baseAtk;
            data.baseDefense = baseDef;
            data.baseSpeed = 10;
            data.baseCritChance = 0f; // Disable crits in most tests
            return data;
        }

        private AbilityCardData MakeAttackCard(
            float baseValue = 100f,
            float statMult = 1f,
            int energyCost = 2,
            CardTargetType targetType = CardTargetType.SingleEnemy,
            StatusEffectType statusEffect = StatusEffectType.None,
            int statusDuration = 0)
        {
            var card = ScriptableObject.CreateInstance<AbilityCardData>();
            card.cardId = $"card_{System.Guid.NewGuid():N}";
            card.cardType = CardType.Attack;
            card.targetType = targetType;
            card.element = AbilityElement.Physical;
            card.baseEffectValue = baseValue;
            card.statMultiplier = statMult;
            card.scalingStat = StatType.Attack;
            card.energyCost = energyCost;
            card.applyStatusEffect = statusEffect;
            card.statusDuration = statusDuration;
            card.statusProcChance = 1f;
            card.splashRadius = 0;
            card.outputsComboTag = ComboTag.None;
            card.requiresComboTag = ComboTag.None;
            card.comboBonusMultiplier = 1f;
            return card;
        }

        private AbilityCardData MakeHealCard(float baseValue = 150f)
        {
            var card = ScriptableObject.CreateInstance<AbilityCardData>();
            card.cardId = $"card_{System.Guid.NewGuid():N}";
            card.cardType = CardType.Heal;
            card.targetType = CardTargetType.SingleAlly;
            card.element = AbilityElement.Holy;
            card.baseEffectValue = baseValue;
            card.statMultiplier = 0f;
            card.scalingStat = StatType.Attack;
            card.energyCost = 2;
            card.outputsComboTag = ComboTag.None;
            card.requiresComboTag = ComboTag.None;
            return card;
        }

        // ─── Attack Tests ────────────────────────────────────────────────────────

        [Test]
        public void AttackCard_DealsExpectedDamage_ToEnemy()
        {
            // Arrange
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData(baseAtk: 100);
            var targetData = MakeHeroData(baseDef: 0); // No defense for clean calculation
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, false, _progression);

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 2));
            grid.PlaceUnit(target.InstanceId, new GridPosition(4, 2));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard(baseValue: 100f, statMult: 1f);

            // Act — baseValue(100) + statMult(1) * attack(100) = 200 expected damage
            var result = resolver.Resolve(card, caster, new GridPosition(4, 2), comboActivated: false);

            // Assert
            Assert.AreEqual(1, result.Damages.Count, "Should have exactly one damage entry");
            Assert.AreEqual(200, result.Damages[0].Amount, "Damage should be baseValue + statMult * attack");
            Assert.AreEqual(target.InstanceId, result.Damages[0].TargetId);

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void AttackCard_NeverDealsZeroDamage_WhenTargetHasHighDefense()
        {
            // Arrange — extreme defense scenario
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData(baseAtk: 1);
            var targetData = MakeHeroData(baseDef: 9999);
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, false, _progression);

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(target.InstanceId, new GridPosition(4, 0));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard(baseValue: 1f, statMult: 0f);

            // Act
            var result = resolver.Resolve(card, caster, new GridPosition(4, 0), comboActivated: false);

            // Assert — minimum damage rule must hold
            Assert.GreaterOrEqual(result.Damages[0].Amount, _config.MinimumDamage,
                "Attack must always deal at least MinimumDamage even against extreme defense");

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void AttackCard_WithComboBonus_DealsIncreasedDamage()
        {
            // Arrange
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData(baseAtk: 0); // Zero attack so only baseValue matters
            var targetData = MakeHeroData(baseDef: 0);
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, false, _progression);

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(target.InstanceId, new GridPosition(4, 0));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard(baseValue: 100f, statMult: 0f);
            card.comboBonusMultiplier = 1.5f;
            card.requiresComboTag = ComboTag.Ignite;

            // Act — with combo activated
            var result = resolver.Resolve(card, caster, new GridPosition(4, 0), comboActivated: true);

            // Assert — 100 * 1.5 = 150
            Assert.AreEqual(150, result.Damages[0].Amount, "Combo bonus should increase damage by comboBonusMultiplier");

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void AttackCard_DoesNotHitDeadTarget()
        {
            // Arrange
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData();
            var targetData = MakeHeroData();
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, false, _progression);

            // Kill target before the attack
            target.TakeDamage(999999, DamageType.True);
            Assert.IsFalse(target.IsAlive, "Pre-condition: target must be dead");

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(target.InstanceId, new GridPosition(4, 0));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard();

            // Act
            var result = resolver.Resolve(card, caster, new GridPosition(4, 0), comboActivated: false);

            // Assert
            Assert.AreEqual(0, result.Damages.Count, "Should not damage dead targets");

            Object.DestroyImmediate(grid.gameObject);
        }

        // ─── Heal Tests ──────────────────────────────────────────────────────────

        [Test]
        public void HealCard_RestoresExpectedHp_ToAlly()
        {
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData();
            var targetData = MakeHeroData(baseHp: 1000);
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, true, _progression); // Ally

            target.TakeDamage(300, DamageType.True); // Damage first
            int hpBeforeHeal = target.CurrentHealth;

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(target.InstanceId, new GridPosition(1, 0));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeHealCard(baseValue: 150f);

            var result = resolver.Resolve(card, caster, new GridPosition(1, 0), comboActivated: false);

            Assert.AreEqual(1, result.Heals.Count);
            Assert.AreEqual(150, result.Heals[0].Amount, "Heal amount should match baseEffectValue");
            Assert.AreEqual(hpBeforeHeal + 150, target.CurrentHealth);

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void HealCard_DoesNotOverheal_BeyondMaxHealth()
        {
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData();
            var targetData = MakeHeroData(baseHp: 1000);
            var caster = new CombatHero(casterData, 1, true, _progression);
            var target = new CombatHero(targetData, 1, true, _progression);

            // Target is at full HP — heal should return 0 actual healed
            target.TakeDamage(0, DamageType.True); // No damage
            int maxHp = target.MaxHealth;

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(target.InstanceId, new GridPosition(1, 0));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [target.InstanceId] = target
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeHealCard(baseValue: 9999f);

            resolver.Resolve(card, caster, new GridPosition(1, 0), comboActivated: false);

            Assert.AreEqual(maxHp, target.CurrentHealth, "HP must not exceed MaxHealth after heal");

            Object.DestroyImmediate(grid.gameObject);
        }

        // ─── Null/Edge Cases ─────────────────────────────────────────────────────

        [Test]
        public void Resolve_ThrowsArgumentNullException_WhenCardIsNull()
        {
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var heroById = new Dictionary<int, CombatHero>();
            var resolver = new AbilityResolver(grid, heroById, _config);
            var casterData = MakeHeroData();
            var caster = new CombatHero(casterData, 1, true, _progression);

            Assert.Throws<System.ArgumentNullException>(() =>
                resolver.Resolve(null, caster, new GridPosition(0, 0), false));

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void Resolve_ThrowsArgumentNullException_WhenCasterIsNull()
        {
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var heroById = new Dictionary<int, CombatHero>();
            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard();

            Assert.Throws<System.ArgumentNullException>(() =>
                resolver.Resolve(card, null, new GridPosition(0, 0), false));

            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenGridIsNull()
        {
            var heroById = new Dictionary<int, CombatHero>();
            Assert.Throws<System.ArgumentNullException>(() =>
                new AbilityResolver(null, heroById, _config));
        }

        [Test]
        public void AllEnemies_TargetType_HitsAllLivingEnemies()
        {
            var grid = new GameObject("Grid").AddComponent<CombatGrid>();
            var casterData = MakeHeroData(baseAtk: 0);
            var enemy1Data = MakeHeroData(baseDef: 0);
            var enemy2Data = MakeHeroData(baseDef: 0);
            var caster = new CombatHero(casterData, 1, true, _progression);
            var enemy1 = new CombatHero(enemy1Data, 1, false, _progression);
            var enemy2 = new CombatHero(enemy2Data, 1, false, _progression);

            grid.PlaceUnit(caster.InstanceId, new GridPosition(0, 0));
            grid.PlaceUnit(enemy1.InstanceId, new GridPosition(4, 0));
            grid.PlaceUnit(enemy2.InstanceId, new GridPosition(5, 2));

            var heroById = new Dictionary<int, CombatHero>
            {
                [caster.InstanceId] = caster,
                [enemy1.InstanceId] = enemy1,
                [enemy2.InstanceId] = enemy2
            };

            var resolver = new AbilityResolver(grid, heroById, _config);
            var card = MakeAttackCard(baseValue: 50f, statMult: 0f, targetType: CardTargetType.AllEnemies);

            var result = resolver.Resolve(card, caster, new GridPosition(4, 0), false);

            Assert.AreEqual(2, result.Damages.Count, "AllEnemies should hit every living enemy");

            Object.DestroyImmediate(grid.gameObject);
        }
    }
}
