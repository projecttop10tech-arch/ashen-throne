using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Pure C# factory that creates CombatHero instances for battle.
    /// Bridges HeroRoster (persistent data) with CombatHero (transient combat runtime).
    ///
    /// Grid placement convention:
    ///   Player zone — column based on preferredRow: Front=0, Middle=1, Back=2.
    ///   Enemy zone  — column based on preferredRow: Front=6, Middle=5, Back=4.
    ///   Row: squad index mapped to [2, 1, 3] (centre first, then up, then down).
    ///
    /// Default loadout: if OwnedHero has no saved loadout, selects the first affordable
    /// cards from HeroData.abilityPool. Repeats the last card to fill 15 slots.
    /// </summary>
    public class CombatHeroFactory
    {
        private static readonly int[] RowBySquadIndex = { 2, 1, 3 }; // Max 3 heroes per side

        private readonly ProgressionConfig _progression;
        private readonly int _defaultDeckSize;

        /// <param name="progression">Config owning stat growth rate and max level.</param>
        /// <param name="defaultDeckSize">Number of cards in a loadout deck (from CombatConfig.DeckSize).</param>
        /// <exception cref="ArgumentNullException">progression is null.</exception>
        public CombatHeroFactory(ProgressionConfig progression, int defaultDeckSize = 15)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _defaultDeckSize = defaultDeckSize;
        }

        /// <summary>
        /// Create a player-owned CombatHero from roster data.
        /// </summary>
        /// <param name="data">Hero definition ScriptableObject.</param>
        /// <param name="ownedData">Player's roster entry with level, star tier, and saved loadout.</param>
        /// <returns>Fully initialised CombatHero ready for placement.</returns>
        /// <exception cref="ArgumentNullException">data or ownedData is null.</exception>
        public CombatHero CreatePlayerHero(HeroData data, OwnedHero ownedData)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (ownedData == null) throw new ArgumentNullException(nameof(ownedData));
            return new CombatHero(data, ownedData.Level, isPlayerOwned: true, _progression);
        }

        /// <summary>
        /// Create an AI-owned CombatHero for PvE encounters.
        /// Uses provided level; no roster entry required.
        /// </summary>
        /// <param name="data">Hero definition ScriptableObject.</param>
        /// <param name="enemyLevel">Enemy's effective level for stat scaling.</param>
        /// <returns>Fully initialised CombatHero ready for placement.</returns>
        /// <exception cref="ArgumentNullException">data is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">enemyLevel is less than 1.</exception>
        public CombatHero CreateEnemyHero(HeroData data, int enemyLevel)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (enemyLevel < 1) throw new ArgumentOutOfRangeException(nameof(enemyLevel), "Enemy level must be >= 1.");
            int clampedLevel = Mathf.Clamp(enemyLevel, 1, _progression.MaxHeroLevel);
            return new CombatHero(data, clampedLevel, isPlayerOwned: false, _progression);
        }

        /// <summary>
        /// Build an AbilityCardData loadout for a player hero.
        /// Uses the saved cardId list in ownedData. Falls back to the first N cards from
        /// abilityPool when no loadout is saved or a cardId cannot be found in the pool.
        /// </summary>
        /// <param name="data">Hero definition (card pool source).</param>
        /// <param name="ownedData">Roster entry with optional saved loadout (list of cardIds).</param>
        /// <returns>List of exactly DeckSize AbilityCardData assets.</returns>
        public List<AbilityCardData> BuildPlayerLoadout(HeroData data, OwnedHero ownedData)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (ownedData == null) throw new ArgumentNullException(nameof(ownedData));

            bool hasSavedLoadout = ownedData.Loadout != null && ownedData.Loadout.Count > 0;
            if (hasSavedLoadout)
            {
                var loadout = ResolveLoadoutFromIds(data, ownedData.Loadout, ownedData.StarTier);
                if (loadout.Count == _defaultDeckSize) return loadout;
                // Saved loadout had missing/invalid cards — pad with defaults
                PadLoadout(loadout, data, ownedData.StarTier);
                return loadout;
            }
            return BuildDefaultLoadout(data, starTier: ownedData.StarTier);
        }

        /// <summary>
        /// Build a default AbilityCardData loadout for an AI enemy hero.
        /// Selects the first <c>DeckSize</c> cards from abilityPool. Repeats if needed.
        /// </summary>
        public List<AbilityCardData> BuildEnemyLoadout(HeroData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return BuildDefaultLoadout(data, starTier: 1);
        }

        /// <summary>
        /// Place player and enemy hero squads on the grid at their default positions.
        /// Player squad: columns 0-2 based on preferredRow. Enemy squad: columns 4-6.
        /// Rows are assigned from the centre outward (row 2, then 1, then 3).
        /// </summary>
        /// <param name="playerHeroes">Ordered list of player CombatHero instances (max 3).</param>
        /// <param name="enemyHeroes">Ordered list of enemy CombatHero instances (max 3).</param>
        /// <param name="grid">The grid to place units on.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        /// <exception cref="ArgumentException">Either squad exceeds 3 heroes.</exception>
        public void PlaceHeroesOnGrid(
            List<CombatHero> playerHeroes,
            List<CombatHero> enemyHeroes,
            CombatGrid grid)
        {
            if (playerHeroes == null) throw new ArgumentNullException(nameof(playerHeroes));
            if (enemyHeroes == null) throw new ArgumentNullException(nameof(enemyHeroes));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (playerHeroes.Count > 3) throw new ArgumentException("Player squad cannot exceed 3 heroes.");
            if (enemyHeroes.Count > 3) throw new ArgumentException("Enemy squad cannot exceed 3 heroes.");

            for (int i = 0; i < playerHeroes.Count; i++)
            {
                CombatHero hero = playerHeroes[i];
                int col = RowPreferenceToPlayerColumn(hero.Data.preferredRow);
                int row = RowBySquadIndex[i];
                if (!grid.PlaceUnit(hero.InstanceId, new GridPosition(col, row)))
                    Debug.LogWarning($"[CombatHeroFactory] Could not place player hero {hero.Data.heroId} at ({col},{row}).");
            }

            for (int i = 0; i < enemyHeroes.Count; i++)
            {
                CombatHero hero = enemyHeroes[i];
                int col = RowPreferenceToEnemyColumn(hero.Data.preferredRow);
                int row = RowBySquadIndex[i];
                if (!grid.PlaceUnit(hero.InstanceId, new GridPosition(col, row)))
                    Debug.LogWarning($"[CombatHeroFactory] Could not place enemy hero {hero.Data.heroId} at ({col},{row}).");
            }
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        private List<AbilityCardData> ResolveLoadoutFromIds(HeroData data, List<string> cardIds, int starTier)
        {
            // Build a lookup from cardId → card for affordable cards
            var poolLookup = new Dictionary<string, AbilityCardData>();
            foreach (var card in data.abilityPool)
            {
                if (card != null && card.requiredHeroStarTier <= starTier)
                    poolLookup[card.cardId] = card;
            }

            var loadout = new List<AbilityCardData>(_defaultDeckSize);
            foreach (var id in cardIds)
            {
                if (loadout.Count >= _defaultDeckSize) break;
                if (!string.IsNullOrEmpty(id) && poolLookup.TryGetValue(id, out AbilityCardData card))
                    loadout.Add(card);
                // Skip cards not found in pool (may have been from a higher star tier)
            }
            return loadout;
        }

        private List<AbilityCardData> BuildDefaultLoadout(HeroData data, int starTier)
        {
            var affordable = new List<AbilityCardData>();
            foreach (var card in data.abilityPool)
            {
                if (card != null && card.requiredHeroStarTier <= starTier)
                    affordable.Add(card);
            }

            var loadout = new List<AbilityCardData>(_defaultDeckSize);
            for (int i = 0; i < _defaultDeckSize; i++)
            {
                if (affordable.Count == 0)
                {
                    Debug.LogWarning($"[CombatHeroFactory] Hero {data.heroId} has no ability cards. Using null card — will cause errors.");
                    loadout.Add(null);
                }
                else
                {
                    loadout.Add(affordable[i % affordable.Count]);
                }
            }
            return loadout;
        }

        private void PadLoadout(List<AbilityCardData> loadout, HeroData data, int starTier)
        {
            if (loadout.Count == 0)
            {
                var defaults = BuildDefaultLoadout(data, starTier);
                loadout.AddRange(defaults);
                return;
            }
            AbilityCardData padCard = loadout[loadout.Count - 1];
            while (loadout.Count < _defaultDeckSize)
                loadout.Add(padCard);
        }

        private static int RowPreferenceToPlayerColumn(CombatRow row) => row switch
        {
            CombatRow.Front  => 0,
            CombatRow.Middle => 1,
            CombatRow.Back   => 2,
            _                => 0
        };

        private static int RowPreferenceToEnemyColumn(CombatRow row) => row switch
        {
            CombatRow.Front  => 6,
            CombatRow.Middle => 5,
            CombatRow.Back   => 4,
            _                => 6
        };
    }
}
