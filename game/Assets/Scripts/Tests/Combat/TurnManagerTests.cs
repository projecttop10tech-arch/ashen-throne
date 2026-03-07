// Unity Test Framework (NUnit) tests for TurnManager phase logic.
// Tests use a minimal MonoBehaviour setup (no full scene) via AddComponent on temp GameObjects.
// Location: Assets/Scripts/Tests/Combat/ — Unity discovers automatically.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Tests.Combat
{
    /// <summary>
    /// Integration-style tests for TurnManager. Requires Unity runtime (MonoBehaviour).
    /// Tests phase transitions, win conditions, and turn order logic.
    /// </summary>
    [TestFixture]
    public class TurnManagerTests
    {
        private GameObject _go;
        private TurnManager _turnManager;
        private CombatGrid _grid;
        private CardHandManager _cardHand;
        private CombatConfig _config;
        private ProgressionConfig _progression;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TurnManagerTest");
            _grid = _go.AddComponent<CombatGrid>();
            _cardHand = _go.AddComponent<CardHandManager>();
            _turnManager = _go.AddComponent<TurnManager>();

            // Use reflection to inject CombatConfig (Inspector-assigned field)
            _config = ScriptableObject.CreateInstance<CombatConfig>();
            _config.MinimumDamage = 1;
            _config.MinimumHeal = 1;
            _config.MitigationDivisor = 4;
            _config.CriticalHitMultiplier = 1.5f;
            _config.ShadowVeilMissChance = 0f;
            _config.MarkedDamageBonus = 1.25f;
            _config.HighGroundDamageMultiplier = 1.25f;
            _config.BurnDamagePerTurn = 30;
            _config.BleedDamagePerTurn = 25;
            _config.PoisonDamagePerTurn = 20;
            _config.RegenerationHealPerTurn = 40;
            _config.AiActionDelaySeconds = 0.6f;
            _config.PlayerActionTimeoutSeconds = 30f;

            _progression = ScriptableObject.CreateInstance<ProgressionConfig>();
            _progression.StatGrowthPerLevel = 0.08f;
            _progression.MaxHeroLevel = 80;

            // Inject config via private field using reflection
            var configField = typeof(TurnManager).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(_turnManager, _config);

            CombatHero.ResetInstanceIdForTesting();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_progression);
            CombatHero.ResetInstanceIdForTesting();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private CombatHero MakeHero(bool isPlayer, int hp = 1000, int spd = 10)
        {
            var data = ScriptableObject.CreateInstance<HeroData>();
            data.heroId = $"h_{System.Guid.NewGuid():N}";
            data.baseHealth = hp;
            data.baseAttack = 100;
            data.baseDefense = 0;
            data.baseSpeed = spd;
            data.baseCritChance = 0f;
            data.preferredRow = CombatRow.Front;
            data.abilityPool = new List<AbilityCardData>();
            return new CombatHero(data, 1, isPlayer, _progression);
        }

        private List<AbilityCardData> MakeMinimalDeck()
        {
            var deck = new List<AbilityCardData>();
            for (int i = 0; i < 15; i++)
            {
                var card = ScriptableObject.CreateInstance<AbilityCardData>();
                card.cardId = $"test_card_{i}";
                card.cardType = CardType.Attack;
                card.targetType = CardTargetType.SingleEnemy;
                card.energyCost = 1;
                card.baseEffectValue = 10f;
                card.statMultiplier = 0f;
                card.outputsComboTag = ComboTag.None;
                card.requiresComboTag = ComboTag.None;
                deck.Add(card);
            }
            return deck;
        }

        private void PlaceHeroes(List<CombatHero> playerHeroes, List<CombatHero> enemies)
        {
            for (int i = 0; i < playerHeroes.Count; i++)
                _grid.PlaceUnit(playerHeroes[i].InstanceId, new GridPosition(0, i));
            for (int i = 0; i < enemies.Count; i++)
                _grid.PlaceUnit(enemies[i].InstanceId, new GridPosition(6, i));
        }

        // ─── StartBattle ─────────────────────────────────────────────────────────

        [Test]
        public void StartBattle_ThrowsArgumentException_WhenHeroListEmpty()
        {
            Assert.Throws<System.ArgumentException>(() =>
                _turnManager.StartBattle(new List<CombatHero>()));
        }

        [Test]
        public void StartBattle_ThrowsArgumentException_WhenHeroListNull()
        {
            Assert.Throws<System.ArgumentException>(() =>
                _turnManager.StartBattle(null));
        }

        [Test]
        public void StartBattle_SetsCurrentPhaseToDrawAfterStarting()
        {
            var player = MakeHero(isPlayer: true);
            var enemy = MakeHero(isPlayer: false);
            PlaceHeroes(new List<CombatHero> { player }, new List<CombatHero> { enemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());

            var heroes = new List<CombatHero> { player, enemy };
            _turnManager.StartBattle(heroes);

            // After StartBattle the state machine is in Draw (or Action — Draw transitions immediately)
            Assert.IsTrue(
                _turnManager.CurrentPhase == CombatPhase.Draw ||
                _turnManager.CurrentPhase == CombatPhase.Action,
                "Phase should be Draw or Action after StartBattle");
        }

        // ─── Turn Order ───────────────────────────────────────────────────────────

        [Test]
        public void StartBattle_HighestSpeedHeroActsFirst()
        {
            var slowPlayer = MakeHero(isPlayer: true, spd: 5);
            var fastEnemy = MakeHero(isPlayer: false, spd: 20);
            PlaceHeroes(new List<CombatHero> { slowPlayer }, new List<CombatHero> { fastEnemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());

            var heroes = new List<CombatHero> { slowPlayer, fastEnemy };
            _turnManager.StartBattle(heroes);

            Assert.AreEqual(fastEnemy.InstanceId, _turnManager.ActiveHeroInstanceId,
                "Fastest hero should take first turn");
        }

        // ─── MarkHeroDead ─────────────────────────────────────────────────────────

        [Test]
        public void MarkHeroDead_DoesNotThrow_WhenHeroIdNotInTurnOrder()
        {
            var player = MakeHero(isPlayer: true);
            var enemy = MakeHero(isPlayer: false);
            PlaceHeroes(new List<CombatHero> { player }, new List<CombatHero> { enemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());
            _turnManager.StartBattle(new List<CombatHero> { player, enemy });

            // 9999 is not in the turn order — must not throw
            Assert.DoesNotThrow(() => _turnManager.MarkHeroDead(9999));
        }

        // ─── BattleOver Detection ─────────────────────────────────────────────────

        [Test]
        public void BattleEnded_Fires_WhenAllEnemiesDie()
        {
            var player = MakeHero(isPlayer: true, hp: 5000);
            var enemy = MakeHero(isPlayer: false, hp: 1);
            PlaceHeroes(new List<CombatHero> { player }, new List<CombatHero> { enemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());

            BattleOutcome? receivedOutcome = null;
            _turnManager.OnBattleEnded += o => receivedOutcome = o;

            var heroes = new List<CombatHero> { player, enemy };
            _turnManager.StartBattle(heroes);

            // Kill the enemy manually — this should trigger HeroDiedEvent → BattleOver
            enemy.TakeDamage(9999, DamageType.True);

            // Flush state machine by calling EndActionPhase
            _turnManager.EndActionPhase();

            Assert.IsTrue(
                receivedOutcome.HasValue,
                "OnBattleEnded should fire when all enemies die");
            Assert.AreEqual(BattleOutcome.PlayerVictory, receivedOutcome.Value);
        }

        // ─── EndActionPhase ───────────────────────────────────────────────────────

        [Test]
        public void EndActionPhase_TransitionsToResolveOrBattleOver_NotThrow()
        {
            var player = MakeHero(isPlayer: true);
            var enemy = MakeHero(isPlayer: false);
            PlaceHeroes(new List<CombatHero> { player }, new List<CombatHero> { enemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());
            _turnManager.StartBattle(new List<CombatHero> { player, enemy });

            // Force into Action phase then call EndActionPhase
            Assert.DoesNotThrow(() => _turnManager.EndActionPhase());
        }

        // ─── CombatPhaseChangedEvent ──────────────────────────────────────────────

        [Test]
        public void StartBattle_PublishesCombatPhaseChangedEvent()
        {
            var player = MakeHero(isPlayer: true);
            var enemy = MakeHero(isPlayer: false);
            PlaceHeroes(new List<CombatHero> { player }, new List<CombatHero> { enemy });
            _cardHand.InitializeDeck(MakeMinimalDeck());

            var phasesReceived = new List<CombatPhase>();
            var sub = AshenThrone.Core.EventBus.Subscribe<CombatPhaseChangedEvent>(
                e => phasesReceived.Add(e.Phase));

            _turnManager.StartBattle(new List<CombatHero> { player, enemy });

            sub.Dispose();

            Assert.Greater(phasesReceived.Count, 0, "CombatPhaseChangedEvent should fire at battle start");
            Assert.Contains(CombatPhase.Draw, phasesReceived,
                "Draw phase event should be published during battle start");
        }
    }
}
