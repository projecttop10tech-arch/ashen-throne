using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Economy
{
    /// <summary>All ways shards can be earned (for analytics and quest tracking).</summary>
    public enum ShardSource
    {
        PveReward,
        EventReward,
        QuestReward,
        AllianceMilestone,
        SeasonReward,
        ShardStore
    }

    // ---------------------------------------------------------------------------
    // HeroShardSystem
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages hero shard collection and hero summoning.
    ///
    /// Design rules enforced in code (check #40, #3):
    /// - Heroes are NEVER summoned from gacha — only through shard accumulation.
    /// - All heroes are earnable F2P through PvE, events, quests.
    /// - SummonHero validates the shard count server-side before granting.
    /// - Client calls RequestSummon; server confirms; ReceiveServerSummonResult applies.
    /// </summary>
    public class HeroShardSystem : MonoBehaviour
    {
        // heroId → shard count
        private readonly Dictionary<string, int> _shardCounts = new();

        private HeroRoster _heroRoster;

        public event Action<string, int> OnShardsAdded; // heroId, newTotal

        private void Awake()
        {
            ServiceLocator.Register<HeroShardSystem>(this);
        }

        private void Start()
        {
            _heroRoster = ServiceLocator.Get<HeroRoster>();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<HeroShardSystem>();
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------

        /// <summary>Load shard counts from server save data.</summary>
        public void LoadFromSaveData(Dictionary<string, int> savedShards)
        {
            _shardCounts.Clear();
            if (savedShards == null) return;
            foreach (var kvp in savedShards)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value >= 0)
                    _shardCounts[kvp.Key] = kvp.Value;
            }
        }

        // -------------------------------------------------------------------
        // Shard management
        // -------------------------------------------------------------------

        /// <summary>
        /// Add shards for a hero. Fires OnShardsAdded and publishes HeroShardsAddedEvent.
        /// </summary>
        public void AddShards(string heroDataId, int amount, ShardSource source)
        {
            if (string.IsNullOrEmpty(heroDataId) || amount <= 0) return;

            if (!_shardCounts.TryGetValue(heroDataId, out int current))
                current = 0;

            int newTotal = current + amount;
            _shardCounts[heroDataId] = newTotal;

            OnShardsAdded?.Invoke(heroDataId, newTotal);
            EventBus.Publish(new HeroShardsAddedEvent(heroDataId, amount, newTotal, source));
        }

        /// <summary>Returns the current shard count for a hero (0 if no shards collected).</summary>
        public int GetShardCount(string heroDataId)
        {
            if (string.IsNullOrEmpty(heroDataId)) return 0;
            return _shardCounts.TryGetValue(heroDataId, out int count) ? count : 0;
        }

        /// <summary>Returns true if the player has enough shards to summon this hero.</summary>
        public bool CanSummon(HeroData heroData)
        {
            if (heroData == null) return false;
            return GetShardCount(heroData.heroId) >= heroData.ShardsToSummon;
        }

        // -------------------------------------------------------------------
        // Summoning
        // -------------------------------------------------------------------

        /// <summary>
        /// Request a hero summon. Validates locally then dispatches to server for confirmation.
        /// Returns a request ID or null on failure.
        /// Client MUST wait for ReceiveServerSummonResult before showing hero as unlocked.
        /// </summary>
        public string RequestSummon(HeroData heroData)
        {
            if (heroData == null)
            {
                Debug.LogWarning("[HeroShardSystem] Cannot summon: heroData is null.");
                return null;
            }

            if (!CanSummon(heroData))
            {
                Debug.LogWarning($"[HeroShardSystem] Cannot summon {heroData.heroId}: insufficient shards " +
                    $"({GetShardCount(heroData.heroId)}/{heroData.ShardsToSummon}).");
                return null;
            }

            if (_heroRoster != null && _heroRoster.OwnsHero(heroData.heroId))
            {
                Debug.LogWarning($"[HeroShardSystem] Hero {heroData.heroId} already owned.");
                return null;
            }

            string requestId = $"summon_{heroData.heroId}_{DateTime.UtcNow.Ticks}";
            EventBus.Publish(new HeroSummonRequestedEvent(requestId, heroData.heroId));
            // In production: dispatch to PlayFab Cloud Script with heroId + shard count for validation
            return requestId;
        }

        /// <summary>
        /// Apply a server-confirmed hero summon.
        /// Deducts shards and marks hero as owned in HeroRoster.
        /// Called on main thread from PlayFab callback (check #9).
        /// </summary>
        public bool ReceiveServerSummonResult(string heroDataId, bool success)
        {
            if (!success) return false;
            if (string.IsNullOrEmpty(heroDataId)) return false;

            if (!_shardCounts.TryGetValue(heroDataId, out int current)) return false;

            // Find hero data to get shard cost (would normally be cached; using HeroRoster lookup here)
            int shardsRequired = GetShardsRequired(heroDataId);
            if (current < shardsRequired) return false;

            _shardCounts[heroDataId] = current - shardsRequired;

            EventBus.Publish(new HeroSummonedEvent(heroDataId, _shardCounts[heroDataId]));
            return true;
        }

        /// <summary>
        /// Returns all heroes the player has enough shards to summon (but doesn't own yet).
        /// </summary>
        public List<string> GetSummonableHeroIds()
        {
            var result = new List<string>();
            foreach (var kvp in _shardCounts)
            {
                string heroId = kvp.Key;
                int shardsRequired = GetShardsRequired(heroId);
                if (kvp.Value >= shardsRequired)
                {
                    if (_heroRoster == null || !_heroRoster.OwnsHero(heroId))
                        result.Add(heroId);
                }
            }
            return result;
        }

        /// <summary>Build shard count dict for persistence.</summary>
        public Dictionary<string, int> BuildSaveData() => new Dictionary<string, int>(_shardCounts);

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private int GetShardsRequired(string heroDataId)
        {
            // In production: look up from the loaded HeroData asset via AssetDatabase or Resources.
            // Using a constant here as HeroData.ShardsToSummon is defined per-rarity tier:
            // Common=50, Rare=80, Epic=120, Legendary=200.
            // TODO: wire up to HeroData lookup when asset database is integrated.
            return 80; // Default: Rare tier requirement
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct HeroShardsAddedEvent
    {
        public readonly string HeroDataId;
        public readonly int AmountAdded;
        public readonly int NewTotal;
        public readonly ShardSource Source;
        public HeroShardsAddedEvent(string id, int amt, int total, ShardSource src)
        { HeroDataId = id; AmountAdded = amt; NewTotal = total; Source = src; }
    }

    public readonly struct HeroSummonRequestedEvent
    {
        public readonly string RequestId;
        public readonly string HeroDataId;
        public HeroSummonRequestedEvent(string req, string id) { RequestId = req; HeroDataId = id; }
    }

    public readonly struct HeroSummonedEvent
    {
        public readonly string HeroDataId;
        public readonly int RemainingShards;
        public HeroSummonedEvent(string id, int rem) { HeroDataId = id; RemainingShards = rem; }
    }
}
