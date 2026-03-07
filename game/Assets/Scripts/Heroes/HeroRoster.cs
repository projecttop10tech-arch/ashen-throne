using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AshenThrone.Combat;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Heroes
{
    /// <summary>
    /// Manages the player's unlocked hero collection, star tiers, levels, and shard counts.
    /// Persistent state — synced with PlayFab on all mutations.
    /// </summary>
    public class HeroRoster : MonoBehaviour
    {
        private readonly Dictionary<string, OwnedHero> _ownedHeroes = new(); // heroId → OwnedHero
        private readonly Dictionary<string, int> _shardCounts = new();       // heroId → shard count

        public IReadOnlyDictionary<string, OwnedHero> OwnedHeroes => _ownedHeroes;

        public event Action<string> OnHeroUnlocked;           // heroId
        public event Action<string, int> OnHeroLeveledUp;     // heroId, newLevel
        public event Action<string, int> OnHeroStarPromoted;  // heroId, newStarTier
        public event Action<string, int> OnShardsAdded;       // heroId, newTotal

        private void Awake()
        {
            ServiceLocator.Register<HeroRoster>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<HeroRoster>();
        }

        /// <summary>
        /// Load hero roster from server save data. Called during boot after authentication.
        /// </summary>
        public void LoadFromSaveData(List<HeroSaveEntry> entries, Dictionary<string, int> shards)
        {
            _ownedHeroes.Clear();
            _shardCounts.Clear();

            foreach (var entry in entries)
            {
                _ownedHeroes[entry.HeroId] = new OwnedHero(entry.HeroId, entry.Level, entry.StarTier, entry.Loadout);
            }
            foreach (var kvp in shards)
            {
                _shardCounts[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Add shards for a hero. Automatically unlocks the hero if threshold is reached.
        /// </summary>
        public void AddShards(string heroId, HeroData heroData, int amount)
        {
            if (heroData == null) throw new ArgumentNullException(nameof(heroData));
            if (amount <= 0) return;

            _shardCounts.TryGetValue(heroId, out int current);
            int newTotal = current + amount;
            _shardCounts[heroId] = newTotal;

            OnShardsAdded?.Invoke(heroId, newTotal);
            EventBus.Publish(new ShardsAddedEvent(heroId, amount, newTotal));

            // Auto-unlock if not yet owned and threshold met
            if (!_ownedHeroes.ContainsKey(heroId) && newTotal >= heroData.shardsToUnlock)
            {
                UnlockHero(heroId, heroData);
            }
        }

        /// <summary>
        /// Unlock a hero directly (story reward, achievement). Does not cost shards.
        /// </summary>
        public void UnlockHero(string heroId, HeroData heroData)
        {
            if (_ownedHeroes.ContainsKey(heroId))
            {
                Debug.LogWarning($"[HeroRoster] Hero {heroId} already unlocked.");
                return;
            }
            _ownedHeroes[heroId] = new OwnedHero(heroId, level: 1, starTier: 1, loadout: null);
            OnHeroUnlocked?.Invoke(heroId);
            EventBus.Publish(new HeroUnlockedEvent(heroId));
        }

        /// <summary>
        /// Level up a hero using the provided XP amount. Returns true if leveled up.
        /// Max level is 80. XP thresholds defined in ProgressionConfig.
        /// </summary>
        public bool AddXp(string heroId, int xp, ProgressionConfig config)
        {
            if (!_ownedHeroes.TryGetValue(heroId, out OwnedHero hero)) return false;
            if (hero.Level >= config.MaxHeroLevel) return false;

            hero.CurrentXp += xp;
            int xpRequired = config.GetXpForLevel(hero.Level);
            if (hero.CurrentXp < xpRequired) return false;

            hero.CurrentXp -= xpRequired;
            hero.Level++;
            OnHeroLeveledUp?.Invoke(heroId, hero.Level);
            EventBus.Publish(new HeroLeveledUpEvent(heroId, hero.Level));
            return true;
        }

        /// <summary>
        /// Promote a hero's star tier using shards. Validates shard cost.
        /// </summary>
        public bool PromoteStar(string heroId, HeroData heroData)
        {
            if (!_ownedHeroes.TryGetValue(heroId, out OwnedHero hero)) return false;
            if (hero.StarTier >= 6) return false;

            int shardIndex = hero.StarTier - 1; // 0-based index into shardsPerStarTier
            if (shardIndex >= heroData.shardsPerStarTier.Length) return false;

            int shardsRequired = heroData.shardsPerStarTier[shardIndex];
            _shardCounts.TryGetValue(heroId, out int currentShards);

            if (currentShards < shardsRequired) return false;

            _shardCounts[heroId] = currentShards - shardsRequired;
            hero.StarTier++;
            OnHeroStarPromoted?.Invoke(heroId, hero.StarTier);
            EventBus.Publish(new HeroStarPromotedEvent(heroId, hero.StarTier));
            return true;
        }

        /// <summary>
        /// Save a card loadout for a hero. Validates deck size.
        /// </summary>
        public bool SaveLoadout(string heroId, List<string> cardIds)
        {
            if (!_ownedHeroes.TryGetValue(heroId, out OwnedHero hero)) return false;
            if (cardIds == null || cardIds.Count != CardHandManager.DeckSize)
            {
                Debug.LogWarning($"[HeroRoster] Loadout for {heroId} must have exactly {CardHandManager.DeckSize} cards.");
                return false;
            }
            hero.Loadout = new List<string>(cardIds);
            EventBus.Publish(new HeroLoadoutSavedEvent(heroId));
            return true;
        }

        public int GetShardCount(string heroId) =>
            _shardCounts.TryGetValue(heroId, out int count) ? count : 0;

        public OwnedHero GetHero(string heroId) =>
            _ownedHeroes.TryGetValue(heroId, out OwnedHero hero) ? hero : null;
    }

    public class OwnedHero
    {
        public string HeroId { get; }
        public int Level { get; set; }
        public int StarTier { get; set; }
        public int CurrentXp { get; set; }
        public List<string> Loadout { get; set; } // cardIds, length = 15

        public OwnedHero(string id, int level, int starTier, List<string> loadout)
        {
            HeroId = id;
            Level = level;
            StarTier = starTier;
            Loadout = loadout ?? new List<string>();
        }
    }

    [System.Serializable]
    public class HeroSaveEntry
    {
        public string HeroId;
        public int Level;
        public int StarTier;
        public List<string> Loadout;
    }

    // --- Events ---
    public readonly struct HeroUnlockedEvent { public readonly string HeroId; public HeroUnlockedEvent(string id) { HeroId = id; } }
    public readonly struct HeroLeveledUpEvent { public readonly string HeroId; public readonly int NewLevel; public HeroLeveledUpEvent(string id, int l) { HeroId = id; NewLevel = l; } }
    public readonly struct HeroStarPromotedEvent { public readonly string HeroId; public readonly int NewStarTier; public HeroStarPromotedEvent(string id, int s) { HeroId = id; NewStarTier = s; } }
    public readonly struct ShardsAddedEvent { public readonly string HeroId; public readonly int Amount; public readonly int NewTotal; public ShardsAddedEvent(string id, int a, int t) { HeroId = id; Amount = a; NewTotal = t; } }
    public readonly struct HeroLoadoutSavedEvent { public readonly string HeroId; public HeroLoadoutSavedEvent(string id) { HeroId = id; } }
}
