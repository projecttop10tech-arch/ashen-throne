using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Manages PvE story encounter setup and teardown.
    /// Responsibilities:
    ///   - Load PveLevelData and build enemy combatants via CombatHeroFactory.
    ///   - Place player and enemy heroes on the grid.
    ///   - Apply terrain presets from the level definition.
    ///   - Signal battle start to TurnManager.
    ///   - Grant rewards on victory, publish analytics events.
    ///
    /// Requires these components on the same GameObject: CombatGrid, TurnManager, CardHandManager.
    /// HeroRoster and ProgressionConfig are fetched from ServiceLocator.
    /// </summary>
    public class PveEncounterManager : MonoBehaviour
    {
        [SerializeField] private CombatConfig _combatConfig;
        [SerializeField] private ProgressionConfig _progressionConfig;

        private CombatGrid _grid;
        private TurnManager _turnManager;
        private CardHandManager _cardHand;
        private HeroRoster _heroRoster;
        private CombatHeroFactory _factory;

        private PveLevelData _currentLevel;
        private List<CombatHero> _playerHeroes = new();
        private List<CombatHero> _enemyHeroes = new();

        private EventSubscription _battleEndedSub;

        public event Action<PveLevelData, bool> OnEncounterCompleted; // level, didWin

        private void Awake()
        {
            _grid = GetComponent<CombatGrid>();
            _turnManager = GetComponent<TurnManager>();
            _cardHand = GetComponent<CardHandManager>();

            if (_combatConfig == null)
                Debug.LogError("[PveEncounterManager] CombatConfig not assigned in Inspector.");
            if (_progressionConfig == null)
                Debug.LogError("[PveEncounterManager] ProgressionConfig not assigned in Inspector.");
        }

        private void OnEnable()
        {
            _battleEndedSub = EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        }

        private void OnDisable()
        {
            _battleEndedSub?.Dispose();
        }

        /// <summary>
        /// Start a PvE encounter using the provided level definition and player squad.
        /// </summary>
        /// <param name="level">The level to load.</param>
        /// <param name="playerHeroIds">Ordered list of heroIds for the player squad (max 3).</param>
        /// <exception cref="ArgumentNullException">level or playerHeroIds is null.</exception>
        /// <exception cref="InvalidOperationException">Required services not found.</exception>
        public void StartEncounter(PveLevelData level, List<string> playerHeroIds)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (playerHeroIds == null) throw new ArgumentNullException(nameof(playerHeroIds));
            if (_combatConfig == null || _progressionConfig == null)
                throw new InvalidOperationException("[PveEncounterManager] Missing required configs.");

            _heroRoster = ServiceLocator.Get<HeroRoster>();
            _factory = new CombatHeroFactory(_progressionConfig, _combatConfig.DeckSize);
            _currentLevel = level;

            _playerHeroes.Clear();
            _enemyHeroes.Clear();

            BuildPlayerSquad(playerHeroIds);
            BuildEnemySquad(level);
            ApplyTerrainPresets(level);

            var allHeroes = new List<CombatHero>(_playerHeroes);
            allHeroes.AddRange(_enemyHeroes);

            _turnManager.StartBattle(allHeroes);
            EventBus.Publish(new PveEncounterStartedEvent(level.levelId, allHeroes.Count));
        }

        private void BuildPlayerSquad(List<string> heroIds)
        {
            int placed = 0;
            foreach (var heroId in heroIds)
            {
                if (placed >= 3) break;

                OwnedHero ownedData = _heroRoster.GetHero(heroId);
                if (ownedData == null)
                {
                    Debug.LogWarning($"[PveEncounterManager] Hero '{heroId}' not in player roster — skipping.");
                    continue;
                }

                HeroData heroData = LoadHeroData(heroId);
                if (heroData == null)
                {
                    Debug.LogWarning($"[PveEncounterManager] HeroData asset not found for '{heroId}' — skipping.");
                    continue;
                }

                CombatHero hero = _factory.CreatePlayerHero(heroData, ownedData);
                _playerHeroes.Add(hero);

                var loadout = _factory.BuildPlayerLoadout(heroData, ownedData);
                if (_playerHeroes.Count == 1)
                {
                    // First hero's hand is active; other heroes managed per-turn in future phases
                    _cardHand.InitializeDeck(loadout);
                }

                placed++;
            }

            _factory.PlaceHeroesOnGrid(_playerHeroes, new List<CombatHero>(), _grid);
        }

        private void BuildEnemySquad(PveLevelData level)
        {
            var tempEnemyList = new List<CombatHero>();
            foreach (var entry in level.enemies)
            {
                if (entry.heroData == null) continue;
                CombatHero enemy = _factory.CreateEnemyHero(entry.heroData, entry.level);
                _enemyHeroes.Add(enemy);
                tempEnemyList.Add(enemy);
            }

            // Place enemies without touching player grid positions
            _factory.PlaceHeroesOnGrid(new List<CombatHero>(), tempEnemyList, _grid);
        }

        private void ApplyTerrainPresets(PveLevelData level)
        {
            if (level.terrainPresets == null) return;
            foreach (var preset in level.terrainPresets)
            {
                var pos = new GridPosition(preset.column, preset.row);
                if (_grid.IsInBounds(pos))
                    _grid.SetTileType(pos, preset.tileType);
            }
        }

        private void OnBattleEnded(BattleEndedEvent evt)
        {
            if (_currentLevel == null) return;

            bool playerWon = evt.Outcome == BattleOutcome.PlayerVictory;
            if (playerWon)
                GrantRewards(_currentLevel);

            OnEncounterCompleted?.Invoke(_currentLevel, playerWon);
            EventBus.Publish(new PveEncounterCompletedEvent(_currentLevel.levelId, playerWon, _currentLevel.xpReward));
        }

        private void GrantRewards(PveLevelData level)
        {
            // XP reward distributed evenly across the player squad
            int xpPerHero = level.xpReward / Mathf.Max(1, _playerHeroes.Count);
            foreach (var hero in _playerHeroes)
            {
                if (!string.IsNullOrEmpty(hero.Data?.heroId))
                    _heroRoster.AddXp(hero.Data.heroId, xpPerHero, _progressionConfig);
            }
        }

        /// <summary>
        /// Load a HeroData asset by heroId. Uses Resources.Load as fallback.
        /// In production this should use Addressables; for Phase 1 Resources.Load is acceptable.
        /// </summary>
        private static HeroData LoadHeroData(string heroId)
        {
            // Naming convention: Resources/Heroes/Hero_{heroId}
            return Resources.Load<HeroData>($"Heroes/Hero_{heroId}");
        }
    }

    // --- Events ---
    public readonly struct PveEncounterStartedEvent
    {
        public readonly string LevelId;
        public readonly int TotalHeroes;
        public PveEncounterStartedEvent(string id, int count) { LevelId = id; TotalHeroes = count; }
    }

    public readonly struct PveEncounterCompletedEvent
    {
        public readonly string LevelId;
        public readonly bool PlayerWon;
        public readonly int XpAwarded;
        public PveEncounterCompletedEvent(string id, bool won, int xp) { LevelId = id; PlayerWon = won; XpAwarded = xp; }
    }
}
