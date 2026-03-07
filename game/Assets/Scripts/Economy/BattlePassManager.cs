using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Economy
{
    /// <summary>
    /// Manages Battle Pass state: tier progression, free/premium tracks, reward claiming.
    /// 50 tiers per season. Free track = genuine content. Premium = cosmetics + QoL only.
    /// HARD RULE: No reward in either track grants combat power advantage over non-premium players.
    /// </summary>
    public class BattlePassManager : MonoBehaviour
    {
        public const int TotalTiers = 50;

        public int CurrentTier { get; private set; }
        public int CurrentPoints { get; private set; }
        public bool IsPremiumActive { get; private set; }
        public BattlePassSeason CurrentSeason { get; private set; }

        private readonly HashSet<int> _claimedFreeRewards = new();
        private readonly HashSet<int> _claimedPremiumRewards = new();

        public event Action<int, int> OnPointsChanged;      // (old, new)
        public event Action<int> OnTierAdvanced;             // newTier
        public event Action<BattlePassReward> OnRewardClaimed;
        public event Action OnPremiumActivated;

        private void Awake()
        {
            ServiceLocator.Register<BattlePassManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<BattlePassManager>();
        }

        /// <summary>
        /// Load battle pass state from server. Called after authentication.
        /// </summary>
        public void LoadState(BattlePassSaveData save, BattlePassSeason season)
        {
            CurrentSeason = season ?? throw new ArgumentNullException(nameof(season));
            CurrentTier = Mathf.Clamp(save.CurrentTier, 0, TotalTiers);
            CurrentPoints = save.CurrentPoints;
            IsPremiumActive = save.IsPremiumActive;

            _claimedFreeRewards.Clear();
            _claimedPremiumRewards.Clear();

            foreach (int tier in save.ClaimedFreeRewardTiers) _claimedFreeRewards.Add(tier);
            foreach (int tier in save.ClaimedPremiumRewardTiers) _claimedPremiumRewards.Add(tier);
        }

        /// <summary>
        /// Add points to the battle pass. Advances tier if threshold reached.
        /// Points come from quests, events, daily logins — never from IAP directly.
        /// </summary>
        public void AddPoints(int amount, string source)
        {
            if (amount <= 0) return;
            if (CurrentTier >= TotalTiers) return;

            int old = CurrentPoints;
            CurrentPoints += amount;
            OnPointsChanged?.Invoke(old, CurrentPoints);
            EventBus.Publish(new BattlePassPointsAddedEvent(amount, source, CurrentPoints));

            AdvanceTiersIfEligible();
        }

        /// <summary>
        /// Activate the premium track. Must be called after successful IAP validation.
        /// Never call this without verified server-side IAP receipt confirmation.
        /// </summary>
        public void ActivatePremiumTrack()
        {
            if (IsPremiumActive) return;
            IsPremiumActive = true;
            OnPremiumActivated?.Invoke();
            EventBus.Publish(new BattlePassPremiumActivatedEvent());
        }

        /// <summary>
        /// Claim a reward for a specific tier. Returns the reward if successful, null otherwise.
        /// </summary>
        public BattlePassReward ClaimReward(int tier, bool isPremiumReward)
        {
            if (tier > CurrentTier)
            {
                Debug.LogWarning($"[BattlePassManager] Cannot claim tier {tier} reward: current tier is {CurrentTier}");
                return null;
            }

            if (isPremiumReward && !IsPremiumActive)
            {
                Debug.LogWarning("[BattlePassManager] Cannot claim premium reward: premium not active");
                return null;
            }

            HashSet<int> claimed = isPremiumReward ? _claimedPremiumRewards : _claimedFreeRewards;
            if (claimed.Contains(tier)) return null;

            BattlePassReward reward = isPremiumReward
                ? CurrentSeason.GetPremiumReward(tier)
                : CurrentSeason.GetFreeReward(tier);

            if (reward == null) return null;

            // Anti-P2W validation: premium rewards must be cosmetic or QoL only.
            if (reward.IsCombatPowerReward)
            {
                Debug.LogError($"[BattlePassManager] DESIGN VIOLATION: Tier {tier} premium reward grants combat power. This is not allowed.");
                return null;
            }

            claimed.Add(tier);
            OnRewardClaimed?.Invoke(reward);
            EventBus.Publish(new BattlePassRewardClaimedEvent(tier, isPremiumReward, reward.RewardId));
            return reward;
        }

        private void AdvanceTiersIfEligible()
        {
            if (CurrentSeason == null) return;
            while (CurrentTier < TotalTiers)
            {
                int pointsForNextTier = CurrentSeason.GetPointsForTier(CurrentTier + 1);
                if (CurrentPoints < pointsForNextTier) break;
                CurrentPoints -= pointsForNextTier;
                CurrentTier++;
                OnTierAdvanced?.Invoke(CurrentTier);
                EventBus.Publish(new BattlePassTierAdvancedEvent(CurrentTier));
            }
        }
    }

    [System.Serializable]
    public class BattlePassSaveData
    {
        public int CurrentTier;
        public int CurrentPoints;
        public bool IsPremiumActive;
        public List<int> ClaimedFreeRewardTiers = new();
        public List<int> ClaimedPremiumRewardTiers = new();
    }

    /// <summary>ScriptableObject defining a season's rewards and point thresholds.</summary>
    [CreateAssetMenu(fileName = "BattlePassSeason_", menuName = "AshenThrone/Battle Pass Season", order = 5)]
    public class BattlePassSeason : ScriptableObject
    {
        public string SeasonId;
        public string SeasonName;
        public DateTime StartDate;
        public DateTime EndDate;

        [Header("Point Thresholds")]
        /// <summary>Points required to advance each tier (index 0 = tier 1 cost). Array length = TotalTiers.</summary>
        public int[] PointsPerTier;

        [Header("Rewards (index 0 = tier 1)")]
        public BattlePassReward[] FreeRewards;
        public BattlePassReward[] PremiumRewards;

        public BattlePassReward GetFreeReward(int tier) =>
            (tier >= 1 && tier <= FreeRewards?.Length) ? FreeRewards[tier - 1] : null;

        public BattlePassReward GetPremiumReward(int tier) =>
            (tier >= 1 && tier <= PremiumRewards?.Length) ? PremiumRewards[tier - 1] : null;

        public int GetPointsForTier(int tier) =>
            (tier >= 1 && PointsPerTier != null && tier <= PointsPerTier.Length)
                ? PointsPerTier[tier - 1]
                : 500; // Default fallback
    }

    [System.Serializable]
    public class BattlePassReward
    {
        public string RewardId;
        public BattlePassRewardType RewardType;
        public string ItemId;         // For cosmetics: skin/decoration id
        public int Quantity;          // For resource/speedup rewards
        [Tooltip("MUST be false for all premium-track rewards. True = design violation.")]
        public bool IsCombatPowerReward;
        public Sprite RewardIcon;
        public string DisplayName;
    }

    public enum BattlePassRewardType
    {
        HeroShard,
        ResourceBundle,
        SpeedupBundle,
        HeroSkin,           // Premium track cosmetic
        CityDecoration,     // Premium track cosmetic
        Emote,              // Premium track cosmetic
        Banner,             // Premium track cosmetic
        GachaTicket,        // Cosmetic gacha only — never hero gacha
        ExperiencePotion,
        AlloyBundle
    }

    // --- Events ---
    public readonly struct BattlePassPointsAddedEvent { public readonly int Amount; public readonly string Source; public readonly int NewTotal; public BattlePassPointsAddedEvent(int a, string s, int t) { Amount = a; Source = s; NewTotal = t; } }
    public readonly struct BattlePassTierAdvancedEvent { public readonly int NewTier; public BattlePassTierAdvancedEvent(int t) { NewTier = t; } }
    public readonly struct BattlePassRewardClaimedEvent { public readonly int Tier; public readonly bool IsPremium; public readonly string RewardId; public BattlePassRewardClaimedEvent(int t, bool p, string r) { Tier = t; IsPremium = p; RewardId = r; } }
    public readonly struct BattlePassPremiumActivatedEvent { }
}
