using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Events;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Events
{
    /// <summary>
    /// Unit tests for WorldBossManager.
    /// WorldBossDefinition fields use [field: SerializeField] with private setters,
    /// so reflection is used to inject test values into the definition.
    /// </summary>
    [TestFixture]
    public class WorldBossManagerTests
    {
        private GameObject _go;
        private WorldBossManager _manager;
        private WorldBossDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _go      = new GameObject("WorldBossManagerTest");
            _manager = _go.AddComponent<WorldBossManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (_definition != null)
                Object.DestroyImmediate(_definition);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Creates and injects a WorldBossDefinition with test values into the manager.
        /// Uses reflection to set the private [SerializeField] _definition field and
        /// the [field: SerializeField] backing fields on the definition.
        /// </summary>
        private WorldBossDefinition InjectDefinition(long totalHp = 1_000_000L,
            int minDmg = 1000, int maxAttacks = 5)
        {
            _definition = ScriptableObject.CreateInstance<WorldBossDefinition>();

            // Set backing fields via reflection (they are auto-property backing fields: <PropName>k__BackingField)
            SetBackingField(_definition, "BossId",              "dragon_boss");
            SetBackingField(_definition, "BossName",            "The Ashen Dragon");
            SetBackingField(_definition, "BossLore",            "Ancient terror.");
            SetBackingField(_definition, "TotalHp",             totalHp);
            SetBackingField(_definition, "MinDamagePerAttack",  minDmg);
            SetBackingField(_definition, "MaxAttacksPerPlayer", maxAttacks);
            SetBackingField(_definition, "MilestoneHpPercents", new float[] { 0.75f, 0.5f, 0.25f });
            SetBackingField(_definition, "ParticipationShardReward", 20);
            SetBackingField(_definition, "LeaderboardShardRewards",  new int[] { 200, 150, 100 });

            // Inject into manager via the private _definition field
            var field = typeof(WorldBossManager).GetField("_definition",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "WorldBossManager._definition field not found via reflection.");
            field.SetValue(_manager, _definition);

            return _definition;
        }

        private static void SetBackingField(object target, string propertyName, object value)
        {
            string backingFieldName = $"<{propertyName}>k__BackingField";
            var field = target.GetType().GetField(backingFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }

        // -------------------------------------------------------------------
        // Without definition (null guard paths)
        // -------------------------------------------------------------------

        [Test]
        public void InitializeBossSpawn_DoesNotThrow_WhenDefinitionNull()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("WorldBossDefinition not assigned"));
            Assert.DoesNotThrow(() => _manager.InitializeBossSpawn());
        }

        [Test]
        public void RequestAttack_ReturnsFalse_WhenDefinitionNull()
        {
            Assert.IsFalse(_manager.RequestAttack(5000));
        }

        [Test]
        public void AttacksRemaining_ReturnsZero_WhenDefinitionNull()
        {
            Assert.AreEqual(0, _manager.AttacksRemaining);
        }

        [Test]
        public void HpRatio_ReturnsZero_WhenDefinitionNull()
        {
            Assert.AreEqual(0f, _manager.HpRatio, 0.001f);
        }

        // -------------------------------------------------------------------
        // InitializeBossSpawn
        // -------------------------------------------------------------------

        [Test]
        public void InitializeBossSpawn_SetsBossAlive()
        {
            InjectDefinition();
            _manager.InitializeBossSpawn();
            Assert.IsTrue(_manager.IsAlive);
        }

        [Test]
        public void InitializeBossSpawn_SetsCurrentHpToTotalHp()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            Assert.AreEqual(1_000_000L, _manager.CurrentHp);
        }

        [Test]
        public void InitializeBossSpawn_ResetsLocalAttacks()
        {
            InjectDefinition(maxAttacks: 5);
            _manager.InitializeBossSpawn();
            _manager.RequestAttack(1000);
            _manager.InitializeBossSpawn(); // re-spawn
            Assert.AreEqual(0, _manager.LocalAttacksUsed);
        }

        // -------------------------------------------------------------------
        // RequestAttack
        // -------------------------------------------------------------------

        [Test]
        public void RequestAttack_ReturnsTrue_WhenBossAliveAndAttacksRemaining()
        {
            InjectDefinition();
            _manager.InitializeBossSpawn();
            Assert.IsTrue(_manager.RequestAttack(5000));
        }

        [Test]
        public void RequestAttack_IncrementsLocalAttacksUsed()
        {
            InjectDefinition();
            _manager.InitializeBossSpawn();
            _manager.RequestAttack(1000);
            Assert.AreEqual(1, _manager.LocalAttacksUsed);
        }

        [Test]
        public void RequestAttack_ReturnsFalse_WhenMaxAttacksExhausted()
        {
            InjectDefinition(maxAttacks: 2);
            _manager.InitializeBossSpawn();
            _manager.RequestAttack(1000);
            _manager.RequestAttack(1000);
            Assert.IsFalse(_manager.RequestAttack(1000));
        }

        [Test]
        public void RequestAttack_ReturnsFalse_WhenBossNotAlive()
        {
            InjectDefinition(totalHp: 1000);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(0); // boss dies
            Assert.IsFalse(_manager.RequestAttack(500));
        }

        [Test]
        public void AttacksRemaining_DecreasesAfterAttack()
        {
            InjectDefinition(maxAttacks: 5);
            _manager.InitializeBossSpawn();
            int before = _manager.AttacksRemaining;
            _manager.RequestAttack(1000);
            Assert.AreEqual(before - 1, _manager.AttacksRemaining);
        }

        // -------------------------------------------------------------------
        // ReceiveServerHpUpdate
        // -------------------------------------------------------------------

        [Test]
        public void ReceiveServerHpUpdate_UpdatesCurrentHp()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(500_000L);
            Assert.AreEqual(500_000L, _manager.CurrentHp);
        }

        [Test]
        public void ReceiveServerHpUpdate_ClampsToZero()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(-999L);
            Assert.AreEqual(0L, _manager.CurrentHp);
        }

        [Test]
        public void ReceiveServerHpUpdate_SetsIsAliveToFalse_WhenHpReachesZero()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(0L);
            Assert.IsFalse(_manager.IsAlive);
        }

        [Test]
        public void ReceiveServerHpUpdate_DoesNotThrow_WhenDefinitionNull()
        {
            Assert.DoesNotThrow(() => _manager.ReceiveServerHpUpdate(500_000L));
        }

        // -------------------------------------------------------------------
        // HpRatio
        // -------------------------------------------------------------------

        [Test]
        public void HpRatio_ReturnsOneAtFullHp()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            Assert.AreEqual(1f, _manager.HpRatio, 0.001f);
        }

        [Test]
        public void HpRatio_ReturnsZeroAtZeroHp()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(0L);
            Assert.AreEqual(0f, _manager.HpRatio, 0.001f);
        }

        [Test]
        public void HpRatio_ReturnsHalfAtHalfHp()
        {
            InjectDefinition(totalHp: 1_000_000L);
            _manager.InitializeBossSpawn();
            _manager.ReceiveServerHpUpdate(500_000L);
            Assert.AreEqual(0.5f, _manager.HpRatio, 0.001f);
        }
    }
}
