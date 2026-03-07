using NUnit.Framework;
using UnityEngine;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Heroes;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Combat
{
    /// <summary>
    /// Unit tests for CombatHero runtime state: damage, healing, shields, status effects, death.
    /// </summary>
    [TestFixture]
    public class CombatHeroTests
    {
        private HeroData _heroData;
        private ProgressionConfig _progressionConfig;

        [SetUp]
        public void SetUp()
        {
            CombatHero.ResetInstanceIdForTesting();

            _heroData = ScriptableObject.CreateInstance<HeroData>();
            _heroData.heroId = "test_hero";
            _heroData.displayName = "Test Hero";
            _heroData.baseHealth = 1000;
            _heroData.baseAttack = 100;
            _heroData.baseDefense = 80;
            _heroData.baseSpeed = 10;
            _heroData.baseCritChance = 0.1f;

            _progressionConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            _progressionConfig.StatGrowthPerLevel = 0.08f;
            _progressionConfig.MaxHeroLevel = 80;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_heroData);
            Object.DestroyImmediate(_progressionConfig);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private CombatHero MakeHero(int level = 1, bool playerOwned = true)
            => new CombatHero(_heroData, level, playerOwned, _progressionConfig);

        // -------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------

        [Test]
        public void Constructor_NullHeroData_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CombatHero(null, 1, true, _progressionConfig));
        }

        [Test]
        public void Constructor_NullProgressionConfig_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new CombatHero(_heroData, 1, true, null));
        }

        [Test]
        public void Constructor_Level1_SetsBaseStats()
        {
            var hero = MakeHero(level: 1);
            Assert.AreEqual(1000, hero.MaxHealth);
            Assert.AreEqual(1000, hero.CurrentHealth);
            Assert.AreEqual(100, hero.CurrentAttack);
            Assert.AreEqual(80, hero.CurrentDefense);
            Assert.AreEqual(10, hero.CurrentSpeed);
            Assert.IsTrue(hero.IsAlive);
        }

        [Test]
        public void Constructor_Level10_ScalesStats()
        {
            var hero = MakeHero(level: 10);
            // multiplier = 1 + 0.08 * (10-1) = 1.72
            int expectedHp = Mathf.RoundToInt(1000 * 1.72f);
            Assert.AreEqual(expectedHp, hero.MaxHealth);
            Assert.AreEqual(expectedHp, hero.CurrentHealth);
        }

        [Test]
        public void Constructor_AssignsUniqueInstanceIds()
        {
            var h1 = MakeHero();
            var h2 = MakeHero();
            Assert.AreNotEqual(h1.InstanceId, h2.InstanceId);
        }

        [Test]
        public void Constructor_SetsIsPlayerOwned()
        {
            var player = MakeHero(playerOwned: true);
            var enemy = MakeHero(playerOwned: false);
            Assert.IsTrue(player.IsPlayerOwned);
            Assert.IsFalse(enemy.IsPlayerOwned);
        }

        // -------------------------------------------------------------------
        // TakeDamage
        // -------------------------------------------------------------------

        [Test]
        public void TakeDamage_ReducesHealth()
        {
            var hero = MakeHero();
            hero.TakeDamage(200, DamageType.Physical);
            Assert.Less(hero.CurrentHealth, hero.MaxHealth);
        }

        [Test]
        public void TakeDamage_PhysicalMitigatedByDefense()
        {
            var hero = MakeHero();
            // mitigated = max(1, rawDamage - defense/4) = max(1, 200 - 80/4) = max(1, 180) = 180
            int dealt = hero.TakeDamage(200, DamageType.Physical);
            Assert.AreEqual(180, dealt);
        }

        [Test]
        public void TakeDamage_TrueDamage_BypassesDefense()
        {
            var hero = MakeHero();
            int dealt = hero.TakeDamage(200, DamageType.True);
            Assert.AreEqual(200, dealt);
        }

        [Test]
        public void TakeDamage_MinimumOneDamage()
        {
            var hero = MakeHero();
            // rawDamage 1, defense 80 → max(1, 1 - 20) = 1
            int dealt = hero.TakeDamage(1, DamageType.Physical);
            Assert.AreEqual(1, dealt);
        }

        [Test]
        public void TakeDamage_KillsHero_WhenHpReachesZero()
        {
            var hero = MakeHero();
            hero.TakeDamage(5000, DamageType.True);
            Assert.IsFalse(hero.IsAlive);
            Assert.AreEqual(0, hero.CurrentHealth);
        }

        [Test]
        public void TakeDamage_ReturnsZero_WhenAlreadyDead()
        {
            var hero = MakeHero();
            hero.TakeDamage(5000, DamageType.True);
            int dealt = hero.TakeDamage(100, DamageType.True);
            Assert.AreEqual(0, dealt);
        }

        [Test]
        public void TakeDamage_FiresOnDied_WhenKilled()
        {
            var hero = MakeHero();
            int diedId = -1;
            hero.OnDied += id => diedId = id;
            hero.TakeDamage(5000, DamageType.True);
            Assert.AreEqual(hero.InstanceId, diedId);
        }

        // -------------------------------------------------------------------
        // Heal
        // -------------------------------------------------------------------

        [Test]
        public void Heal_RestoresHealth()
        {
            var hero = MakeHero();
            hero.TakeDamage(500, DamageType.True);
            int healed = hero.Heal(200);
            Assert.AreEqual(200, healed);
            Assert.AreEqual(hero.MaxHealth - 300, hero.CurrentHealth);
        }

        [Test]
        public void Heal_ClampsToMaxHealth()
        {
            var hero = MakeHero();
            hero.TakeDamage(100, DamageType.True);
            int healed = hero.Heal(999);
            Assert.AreEqual(100, healed);
            Assert.AreEqual(hero.MaxHealth, hero.CurrentHealth);
        }

        [Test]
        public void Heal_ReturnsZero_WhenDead()
        {
            var hero = MakeHero();
            hero.TakeDamage(5000, DamageType.True);
            int healed = hero.Heal(500);
            Assert.AreEqual(0, healed);
        }

        [Test]
        public void Heal_NegativeAmount_ReturnsZero()
        {
            var hero = MakeHero();
            hero.TakeDamage(100, DamageType.True);
            int healed = hero.Heal(-50);
            Assert.AreEqual(0, healed);
        }

        // -------------------------------------------------------------------
        // Shield
        // -------------------------------------------------------------------

        [Test]
        public void Shield_AbsorbsDamage()
        {
            var hero = MakeHero();
            hero.ApplyShield(100);
            int dealt = hero.TakeDamage(50, DamageType.True);
            Assert.AreEqual(0, dealt); // all absorbed
            Assert.AreEqual(hero.MaxHealth, hero.CurrentHealth);
        }

        [Test]
        public void Shield_PartialAbsorb()
        {
            var hero = MakeHero();
            hero.ApplyShield(30);
            int dealt = hero.TakeDamage(100, DamageType.True);
            Assert.AreEqual(70, dealt); // 30 absorbed, 70 through
        }

        [Test]
        public void Shield_NegativeAmount_Ignored()
        {
            var hero = MakeHero();
            hero.ApplyShield(-50);
            int dealt = hero.TakeDamage(100, DamageType.True);
            Assert.AreEqual(100, dealt); // no shield
        }

        [Test]
        public void Shield_Stacks()
        {
            var hero = MakeHero();
            hero.ApplyShield(50);
            hero.ApplyShield(50);
            int dealt = hero.TakeDamage(80, DamageType.True);
            Assert.AreEqual(0, dealt); // 100 shield absorbs 80
        }

        // -------------------------------------------------------------------
        // Status effects
        // -------------------------------------------------------------------

        [Test]
        public void ApplyStatus_None_IsNoOp()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.None, 3);
            Assert.AreEqual(0, hero.StatusEffects.Count);
        }

        [Test]
        public void ApplyStatus_AddsEffect()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.Burn, 3, procChance: 1f);
            Assert.IsTrue(hero.HasStatus(StatusEffectType.Burn));
            Assert.AreEqual(1, hero.StatusEffects.Count);
        }

        [Test]
        public void ApplyStatus_RefreshesDuration_IfAlreadyActive()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.Burn, 2, procChance: 1f);
            hero.ApplyStatus(StatusEffectType.Burn, 5, procChance: 1f);
            Assert.AreEqual(1, hero.StatusEffects.Count);
            Assert.AreEqual(5, hero.StatusEffects[0].RemainingTurns);
        }

        [Test]
        public void TickStatusEffects_DecrementsAndRemoves()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.Bleed, 1, procChance: 1f);
            Assert.IsTrue(hero.HasStatus(StatusEffectType.Bleed));

            hero.TickStatusEffects();
            Assert.IsFalse(hero.HasStatus(StatusEffectType.Bleed));
            Assert.AreEqual(0, hero.StatusEffects.Count);
        }

        [Test]
        public void TickStatusEffects_DecrementsBut_DoesNotRemoveIfDurationRemains()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.Poison, 3, procChance: 1f);
            hero.TickStatusEffects();
            Assert.IsTrue(hero.HasStatus(StatusEffectType.Poison));
            Assert.AreEqual(2, hero.StatusEffects[0].RemainingTurns);
        }

        [Test]
        public void ApplyStatus_Slow_ReducesSpeed()
        {
            var hero = MakeHero();
            int speedBefore = hero.CurrentSpeed;
            hero.ApplyStatus(StatusEffectType.Slow, 2, procChance: 1f);
            Assert.AreEqual(speedBefore - 3, hero.CurrentSpeed);
        }

        // -------------------------------------------------------------------
        // Stat modifiers
        // -------------------------------------------------------------------

        [Test]
        public void ModifyAttack_ClampsToZero()
        {
            var hero = MakeHero();
            hero.ModifyAttack(-9999);
            Assert.AreEqual(0, hero.CurrentAttack);
        }

        [Test]
        public void ModifyDefense_ClampsToZero()
        {
            var hero = MakeHero();
            hero.ModifyDefense(-9999);
            Assert.AreEqual(0, hero.CurrentDefense);
        }

        [Test]
        public void ModifySpeed_ClampsToOne()
        {
            var hero = MakeHero();
            hero.ModifySpeed(-9999);
            Assert.AreEqual(1, hero.CurrentSpeed);
        }

        // -------------------------------------------------------------------
        // Death clears effects
        // -------------------------------------------------------------------

        [Test]
        public void Death_ClearsAllStatusEffects()
        {
            var hero = MakeHero();
            hero.ApplyStatus(StatusEffectType.Burn, 5, procChance: 1f);
            hero.ApplyStatus(StatusEffectType.Slow, 5, procChance: 1f);
            hero.TakeDamage(5000, DamageType.True);
            Assert.AreEqual(0, hero.StatusEffects.Count);
        }

        // -------------------------------------------------------------------
        // ResetInstanceIdForTesting
        // -------------------------------------------------------------------

        [Test]
        public void ResetInstanceIdForTesting_ResetsCounter()
        {
            var h1 = MakeHero();
            Assert.AreEqual(1, h1.InstanceId);
            CombatHero.ResetInstanceIdForTesting();
            var h2 = MakeHero();
            Assert.AreEqual(1, h2.InstanceId);
        }
    }
}
