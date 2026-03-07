using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Economy
{
    // ---------------------------------------------------------------------------
    // Gacha item definitions
    // ---------------------------------------------------------------------------

    /// <summary>Rarity tier for cosmetic gacha items.</summary>
    public enum GachaRarity
    {
        Common    = 0,
        Rare      = 1,
        Epic      = 2,
        Legendary = 3
    }

    /// <summary>Category of cosmetic item available in the gacha pool.</summary>
    public enum CosmeticType
    {
        HeroSkin,
        CityTheme,
        Emote,
        ProfileBanner,
        ChatBubble,
        AvatarFrame
    }

    /// <summary>
    /// A single item that can be pulled from the gacha pool.
    /// HARD RULE: Heroes are NEVER in the gacha pool (check #40).
    /// Only cosmetics and non-power items may be added here.
    /// </summary>
    [System.Serializable]
    public class GachaItem
    {
        /// <summary>Unique identifier for this cosmetic item.</summary>
        public string ItemId { get; }

        /// <summary>Display name shown in the gacha reveal screen.</summary>
        public string DisplayName { get; }

        public GachaRarity Rarity { get; }
        public CosmeticType CosmeticType { get; }

        /// <summary>
        /// Base weight in the pull pool (before pity adjustment).
        /// Higher weight = more frequent. Standard rates:
        /// Common=6000, Rare=3000, Epic=750, Legendary=250 (sums to 10000).
        /// </summary>
        public int BaseWeight { get; }

        /// <summary>
        /// If true, this item has been obtained by the player and cannot drop again
        /// (replaced by a duplicate token that converts to gacha currency).
        /// </summary>
        public bool IsOwned { get; set; }

        public GachaItem(string itemId, string displayName, GachaRarity rarity,
            CosmeticType cosmeticType, int baseWeight)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("ItemId cannot be empty.");
            if (baseWeight <= 0)
                throw new ArgumentException("BaseWeight must be > 0.");

            ItemId = itemId;
            DisplayName = displayName;
            Rarity = rarity;
            CosmeticType = cosmeticType;
            BaseWeight = baseWeight;
        }
    }

    /// <summary>Result of a single gacha pull.</summary>
    public class GachaPullResult
    {
        public GachaItem Item { get; }

        /// <summary>True if the player already owned this item (converted to duplicate tokens).</summary>
        public bool IsDuplicate { get; }

        /// <summary>Duplicate token amount granted (if IsDuplicate).</summary>
        public int DuplicateTokens { get; }

        /// <summary>The pity counter value at the time of this pull (for transparency — check #41).</summary>
        public int PityCounterAtPull { get; }

        public GachaPullResult(GachaItem item, bool isDuplicate, int duplicateTokens, int pityCounter)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            IsDuplicate = isDuplicate;
            DuplicateTokens = duplicateTokens;
            PityCounterAtPull = pityCounter;
        }
    }

    // ---------------------------------------------------------------------------
    // GachaSystem
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Cosmetic-only gacha system with full pity counter and transparent odds display.
    ///
    /// Design rules enforced in code:
    /// - Heroes are NEVER pullable (check #40 — items must be cosmetics only).
    /// - Pity counter guarantees Legendary at 50 pulls (check #41 — no hidden pity).
    /// - Odds are logged and must be displayable in UI per App Store requirements (check #44, #45).
    /// - All pulls validated server-side (check #49); client requests pull, server responds with result.
    ///
    /// The client-side roll here is for IMMEDIATE visual feedback only.
    /// Server result is authoritative and replaces the client prediction.
    /// </summary>
    public class GachaSystem : MonoBehaviour
    {
        /// <summary>Guaranteed Legendary pull after this many consecutive non-Legendary pulls.</summary>
        public const int PityThreshold = 50;

        /// <summary>Tokens per duplicate (awarded instead of the duplicate item).</summary>
        public const int DuplicateTokenAmount = 30;

        /// <summary>Tokens required to do a free Legendary exchange.</summary>
        public const int LegendaryExchangeCost = 300;

        private readonly List<GachaItem> _pool = new();
        private int _pityCounter = 0;
        private System.Random _rng = new System.Random();

        private void Awake()
        {
            ServiceLocator.Register<GachaSystem>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<GachaSystem>();
        }

        // -------------------------------------------------------------------
        // Pool management
        // -------------------------------------------------------------------

        /// <summary>
        /// Populate the gacha pool. Only cosmetic items allowed (no heroes — check #40).
        /// </summary>
        public void SetPool(IEnumerable<GachaItem> items)
        {
            _pool.Clear();
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                // Enforce: no heroes in gacha. CosmeticType must be a cosmetic.
                // All GachaItem types are CosmeticType enum values (no hero type exists).
                _pool.Add(item);
            }
        }

        /// <summary>Restore pity counter from save data.</summary>
        public void LoadPityCounter(int savedCounter)
        {
            _pityCounter = Mathf.Clamp(savedCounter, 0, PityThreshold);
        }

        /// <summary>Current pity counter value (displayed in UI for transparency).</summary>
        public int PityCounter => _pityCounter;

        /// <summary>Number of pulls remaining until guaranteed Legendary.</summary>
        public int PullsUntilPity => PityThreshold - _pityCounter;

        // -------------------------------------------------------------------
        // Pull simulation (client-side preview — server result is authoritative)
        // -------------------------------------------------------------------

        /// <summary>
        /// Simulate a single pull. Returns a preview result for immediate visual feedback.
        /// The server's response (via ReceiveServerPullResult) is the canonical result.
        ///
        /// NEVER commit inventory changes based on this local result alone.
        /// </summary>
        public GachaPullResult SimulatePull()
        {
            if (_pool.Count == 0)
            {
                Debug.LogWarning("[GachaSystem] Gacha pool is empty.");
                return null;
            }

            _pityCounter++;

            GachaItem selected;
            if (_pityCounter >= PityThreshold)
            {
                // Pity: force a Legendary pull
                selected = SelectFromPool(GachaRarity.Legendary);
                if (selected == null)
                {
                    // No legendary in pool — pick the highest-rarity available
                    selected = SelectHighestRarity();
                }
                _pityCounter = 0;
            }
            else
            {
                selected = SelectWeighted();
                if (selected?.Rarity == GachaRarity.Legendary)
                    _pityCounter = 0;
            }

            if (selected == null) return null;

            bool isDuplicate = selected.IsOwned;
            int tokens = isDuplicate ? DuplicateTokenAmount : 0;
            return new GachaPullResult(selected, isDuplicate, tokens, _pityCounter);
        }

        /// <summary>
        /// Apply a server-confirmed pull result. Marks the item as owned in the local pool.
        /// Called on main thread from PlayFab callback (check #9).
        /// </summary>
        public void ReceiveServerPullResult(string itemId, bool isDuplicate, int pityCounterFromServer)
        {
            _pityCounter = Mathf.Clamp(pityCounterFromServer, 0, PityThreshold);

            if (!isDuplicate)
            {
                foreach (var item in _pool)
                {
                    if (item.ItemId == itemId)
                    {
                        item.IsOwned = true;
                        break;
                    }
                }
            }

            EventBus.Publish(new GachaPullConfirmedEvent(itemId, isDuplicate, _pityCounter));
        }

        // -------------------------------------------------------------------
        // Odds display (required by App Store / Google Play — check #44, #45)
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns the displayed pull odds for each rarity tier.
        /// These are the rates the player sees in-game — must match actual rates (check #41).
        /// </summary>
        public Dictionary<GachaRarity, float> GetDisplayedOdds()
        {
            int totalWeight = 0;
            var weightByRarity = new Dictionary<GachaRarity, int>();

            foreach (var item in _pool)
            {
                if (!item.IsOwned) // Owned items are removed from pool
                {
                    totalWeight += item.BaseWeight;
                    if (!weightByRarity.ContainsKey(item.Rarity))
                        weightByRarity[item.Rarity] = 0;
                    weightByRarity[item.Rarity] += item.BaseWeight;
                }
            }

            var odds = new Dictionary<GachaRarity, float>();
            if (totalWeight > 0)
            {
                foreach (var kvp in weightByRarity)
                    odds[kvp.Key] = (float)kvp.Value / totalWeight;
            }
            return odds;
        }

        // -------------------------------------------------------------------
        // Pure C# helpers — unit-testable
        // -------------------------------------------------------------------

        /// <summary>Weighted random selection from the full pool.</summary>
        private GachaItem SelectWeighted()
        {
            int totalWeight = 0;
            foreach (var item in _pool)
                if (!item.IsOwned) totalWeight += item.BaseWeight;

            if (totalWeight <= 0) return null;

            int roll = _rng.Next(0, totalWeight);
            int cumulative = 0;
            foreach (var item in _pool)
            {
                if (item.IsOwned) continue;
                cumulative += item.BaseWeight;
                if (roll < cumulative) return item;
            }
            return null;
        }

        /// <summary>Select a random item of a specific rarity.</summary>
        private GachaItem SelectFromPool(GachaRarity rarity)
        {
            var candidates = new List<GachaItem>();
            foreach (var item in _pool)
                if (!item.IsOwned && item.Rarity == rarity) candidates.Add(item);

            if (candidates.Count == 0) return null;
            return candidates[_rng.Next(0, candidates.Count)];
        }

        /// <summary>Select a random item from the highest available rarity.</summary>
        private GachaItem SelectHighestRarity()
        {
            for (int r = (int)GachaRarity.Legendary; r >= 0; r--)
            {
                var item = SelectFromPool((GachaRarity)r);
                if (item != null) return item;
            }
            return null;
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct GachaPullConfirmedEvent
    {
        public readonly string ItemId;
        public readonly bool IsDuplicate;
        public readonly int NewPityCounter;
        public GachaPullConfirmedEvent(string id, bool dup, int pity)
        { ItemId = id; IsDuplicate = dup; NewPityCounter = pity; }
    }
}
