using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a building's identity, upgrade tiers, costs, and production output.
    /// One asset per building type (not per level). All tiers embedded as BuildingTierData array.
    /// </summary>
    [CreateAssetMenu(fileName = "Building_", menuName = "AshenThrone/Building Data", order = 4)]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public string buildingId;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public BuildingCategory category;
        public BuildingDistrict district;

        [Header("Visual")]
        /// <summary>One sprite per upgrade tier. Index matches tier (0-based).</summary>
        public Sprite[] tierSprites;
        public GameObject constructionParticlePrefab;
        public AudioClip constructionCompleteSound;

        [Header("Placement")]
        /// <summary>Grid size in empire tiles (width x height).</summary>
        public Vector2Int footprintSize = new(2, 2);
        public bool isUniquePerCity = false;

        [Header("Unlock")]
        /// <summary>Stronghold level required to place this building for the first time.</summary>
        public int strongholdLevelRequired = 1;

        [Header("Upgrade Tiers")]
        /// <summary>Tiers array index = tier number (0 = Tier 1). Max 20 tiers.</summary>
        public BuildingTierData[] tiers;

        /// <summary>Safe accessor returning null if tier index is out of range.</summary>
        public BuildingTierData GetTier(int tierIndex)
        {
            if (tierIndex < 0 || tierIndex >= tiers.Length) return null;
            return tiers[tierIndex];
        }
    }

    [System.Serializable]
    public class BuildingTierData
    {
        /// <summary>Human-readable tier label shown in upgrade UI (e.g. "Level 5").</summary>
        public string tierLabel;

        [Header("Construction Cost")]
        public int stoneCost;
        public int ironCost;
        public int grainCost;
        public int arcaneEssenceCost;

        [Header("Construction Time")]
        /// <summary>Base build time in seconds. Max enforced value: 28800 (8 hours) for Stronghold; 14400 (4h) for others.</summary>
        public int buildTimeSeconds;

        [Header("Production (per hour, if applicable)")]
        public float stoneProduction;
        public float ironProduction;
        public float grainProduction;
        public float arcaneEssenceProduction;

        [Header("Bonus Effects")]
        /// <summary>Free-text description of this tier's bonus (displayed in UI). Keep under 80 characters.</summary>
        public string bonusDescription;
        public List<BuildingBonus> bonuses = new();
    }

    [System.Serializable]
    public class BuildingBonus
    {
        public BuildingBonusType bonusType;
        /// <summary>Percentage value (e.g. 5 = +5%). Applied additively with other bonuses of same type.</summary>
        public float bonusPercent;
    }

    public enum BuildingBonusType
    {
        TroopTrainingSpeed,
        ResearchSpeed,
        StoneProductionBonus,
        IronProductionBonus,
        GrainProductionBonus,
        ArcaneProductionBonus,
        VaultCapacity,
        TroopCapacity,
        HeroXpBonus,
        AllianceContributionBonus,
        BuildSpeedBonus,
        DefensePowerBonus
    }

    public enum BuildingCategory
    {
        Core,           // Stronghold — unique, gates all progress
        Military,       // Barracks, Training Grounds, Siege Workshop
        Resource,       // Farms, Mines, Lumber Mills, Vaults
        Research,       // Academy, Library, Laboratory
        HeroDistrict,   // Guild Hall, Forge, Enchanting Tower
        Decoration      // Cosmetic only, no production
    }

    public enum BuildingDistrict
    {
        Core,
        Military,
        Resource,
        Research,
        Hero
    }
}
