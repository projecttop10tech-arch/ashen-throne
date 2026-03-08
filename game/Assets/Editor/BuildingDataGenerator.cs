#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using AshenThrone.Data;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates 21 BuildingData ScriptableObject assets (Stronghold + 5 per district × 4 districts).
    /// Each building has 10 upgrade tiers with escalating costs and timers.
    /// Respects the 4-hour cap for normal buildings and 8-hour cap for Stronghold.
    /// Safe to re-run — overwrites existing assets.
    /// </summary>
    public static class BuildingDataGenerator
    {
        private const string OutputPath = "Assets/Data/Buildings";
        private const int TierCount = 10;
        private const int MaxBuildTimeNormal = 14400;    // 4 hours in seconds
        private const int MaxBuildTimeStronghold = 28800; // 8 hours in seconds

        [MenuItem("Ashen Throne/Generate Building Data")]
        public static void Generate()
        {
            EnsureDirectory();
            int count = 0;

            // Core district — Stronghold (unique)
            CreateBuilding("stronghold", "Stronghold", "The heart of your empire. Upgrading the Stronghold unlocks new buildings and features.",
                BuildingCategory.Core, BuildingDistrict.Core, true, 1, MaxBuildTimeStronghold,
                BuildingBonusType.BuildSpeedBonus);
            count++;

            // Military district (5 buildings)
            CreateBuilding("barracks", "Barracks", "Trains basic troops and unlocks military upgrades.",
                BuildingCategory.Military, BuildingDistrict.Military, false, 1, MaxBuildTimeNormal,
                BuildingBonusType.TroopCapacity);
            CreateBuilding("training_grounds", "Training Grounds", "Increases troop training speed for all military units.",
                BuildingCategory.Military, BuildingDistrict.Military, false, 2, MaxBuildTimeNormal,
                BuildingBonusType.TroopTrainingSpeed);
            CreateBuilding("siege_workshop", "Siege Workshop", "Constructs siege engines for territory warfare.",
                BuildingCategory.Military, BuildingDistrict.Military, false, 4, MaxBuildTimeNormal,
                BuildingBonusType.DefensePowerBonus);
            CreateBuilding("war_hall", "War Hall", "Coordinates alliance war efforts and boosts rally capacity.",
                BuildingCategory.Military, BuildingDistrict.Military, false, 6, MaxBuildTimeNormal,
                BuildingBonusType.AllianceContributionBonus);
            CreateBuilding("watch_tower", "Watch Tower", "Provides early warning of incoming attacks and boosts defense.",
                BuildingCategory.Military, BuildingDistrict.Military, false, 3, MaxBuildTimeNormal,
                BuildingBonusType.DefensePowerBonus);
            count += 5;

            // Resource district (5 buildings)
            CreateBuilding("stone_quarry", "Stone Quarry", "Produces stone over time. Upgrade for higher output.",
                BuildingCategory.Resource, BuildingDistrict.Resource, false, 1, MaxBuildTimeNormal,
                BuildingBonusType.StoneProductionBonus);
            CreateBuilding("iron_mine", "Iron Mine", "Produces iron over time. Upgrade for higher output.",
                BuildingCategory.Resource, BuildingDistrict.Resource, false, 1, MaxBuildTimeNormal,
                BuildingBonusType.IronProductionBonus);
            CreateBuilding("grain_farm", "Grain Farm", "Produces grain over time. Upgrade for higher output.",
                BuildingCategory.Resource, BuildingDistrict.Resource, false, 1, MaxBuildTimeNormal,
                BuildingBonusType.GrainProductionBonus);
            CreateBuilding("arcane_tower", "Arcane Tower", "Produces arcane essence. Required for hero abilities and enchanting.",
                BuildingCategory.Resource, BuildingDistrict.Resource, false, 3, MaxBuildTimeNormal,
                BuildingBonusType.ArcaneProductionBonus);
            CreateBuilding("vault", "Vault", "Protects resources from raids. Higher tiers protect more.",
                BuildingCategory.Resource, BuildingDistrict.Resource, false, 2, MaxBuildTimeNormal,
                BuildingBonusType.VaultCapacity);
            count += 5;

            // Research district (5 buildings)
            CreateBuilding("academy", "Academy", "Unlocks research nodes and boosts research speed.",
                BuildingCategory.Research, BuildingDistrict.Research, false, 2, MaxBuildTimeNormal,
                BuildingBonusType.ResearchSpeed);
            CreateBuilding("library", "Library", "Stores ancient knowledge. Boosts research speed further.",
                BuildingCategory.Research, BuildingDistrict.Research, false, 3, MaxBuildTimeNormal,
                BuildingBonusType.ResearchSpeed);
            CreateBuilding("laboratory", "Laboratory", "Enables advanced research and arcane experiments.",
                BuildingCategory.Research, BuildingDistrict.Research, false, 5, MaxBuildTimeNormal,
                BuildingBonusType.ResearchSpeed);
            CreateBuilding("observatory", "Observatory", "Reveals hidden map features and boosts scouting.",
                BuildingCategory.Research, BuildingDistrict.Research, false, 4, MaxBuildTimeNormal,
                BuildingBonusType.ResearchSpeed);
            CreateBuilding("archive", "Archive", "Preserves completed research bonuses and enables re-spec.",
                BuildingCategory.Research, BuildingDistrict.Research, false, 7, MaxBuildTimeNormal,
                BuildingBonusType.ResearchSpeed);
            count += 5;

            // Hero district (5 buildings)
            CreateBuilding("guild_hall", "Guild Hall", "Central hub for hero management. Unlocks hero recruitment.",
                BuildingCategory.HeroDistrict, BuildingDistrict.Hero, false, 1, MaxBuildTimeNormal,
                BuildingBonusType.HeroXpBonus);
            CreateBuilding("forge", "Forge", "Crafts and upgrades hero equipment.",
                BuildingCategory.HeroDistrict, BuildingDistrict.Hero, false, 3, MaxBuildTimeNormal,
                BuildingBonusType.HeroXpBonus);
            CreateBuilding("enchanting_tower", "Enchanting Tower", "Enchants equipment with magical bonuses.",
                BuildingCategory.HeroDistrict, BuildingDistrict.Hero, false, 5, MaxBuildTimeNormal,
                BuildingBonusType.HeroXpBonus);
            CreateBuilding("training_arena", "Training Arena", "Heroes gain XP passively while assigned here.",
                BuildingCategory.HeroDistrict, BuildingDistrict.Hero, false, 2, MaxBuildTimeNormal,
                BuildingBonusType.HeroXpBonus);
            CreateBuilding("hero_shrine", "Hero Shrine", "Increases hero shard drop rates from all sources.",
                BuildingCategory.HeroDistrict, BuildingDistrict.Hero, false, 6, MaxBuildTimeNormal,
                BuildingBonusType.HeroXpBonus);
            count += 5;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BuildingDataGenerator] Generated {count} BuildingData assets in {OutputPath}/");
        }

        private static void CreateBuilding(
            string id, string displayName, string description,
            BuildingCategory category, BuildingDistrict district,
            bool isUnique, int strongholdLevelRequired, int maxBuildTime,
            BuildingBonusType primaryBonus)
        {
            string assetPath = $"{OutputPath}/Building_{id}.asset";

            var building = ScriptableObject.CreateInstance<BuildingData>();
            building.buildingId = id;
            building.displayName = displayName;
            building.description = description;
            building.category = category;
            building.district = district;
            building.isUniquePerCity = isUnique;
            building.strongholdLevelRequired = strongholdLevelRequired;
            building.footprintSize = isUnique ? new Vector2Int(3, 3) : new Vector2Int(2, 2);
            building.tiers = GenerateTiers(id, maxBuildTime, category, primaryBonus);

            AssetDatabase.CreateAsset(building, assetPath);
        }

        private static BuildingTierData[] GenerateTiers(
            string buildingId, int maxBuildTime,
            BuildingCategory category, BuildingBonusType primaryBonus)
        {
            var tiers = new BuildingTierData[TierCount];
            bool isResourceBuilding = category == BuildingCategory.Resource;

            for (int i = 0; i < TierCount; i++)
            {
                int tier = i + 1;
                float progression = (float)i / (TierCount - 1); // 0.0 to 1.0

                var td = new BuildingTierData
                {
                    tierLabel = $"Level {tier}",

                    // Costs scale quadratically: tier 1 is cheap, tier 10 is expensive
                    stoneCost  = Mathf.RoundToInt(100 * tier * tier * 0.8f),
                    ironCost   = Mathf.RoundToInt(80  * tier * tier * 0.7f),
                    grainCost  = Mathf.RoundToInt(60  * tier * tier * 0.5f),
                    arcaneEssenceCost = tier >= 4 ? Mathf.RoundToInt(40 * (tier - 3) * tier * 0.6f) : 0,

                    // Build time: linearly approaches max, never exceeds it
                    buildTimeSeconds = Mathf.Min(
                        Mathf.RoundToInt(Mathf.Lerp(300, maxBuildTime, progression)),
                        maxBuildTime),

                    // Production only for Resource buildings
                    stoneProduction = isResourceBuilding && buildingId.Contains("stone") ? 50 + tier * 30 : 0,
                    ironProduction  = isResourceBuilding && buildingId.Contains("iron")  ? 40 + tier * 25 : 0,
                    grainProduction = isResourceBuilding && buildingId.Contains("grain") ? 60 + tier * 35 : 0,
                    arcaneEssenceProduction = isResourceBuilding && buildingId.Contains("arcane") ? 20 + tier * 15 : 0,

                    // Bonus per tier
                    bonusDescription = $"+{tier * 2}% {primaryBonus}",
                    bonuses = new System.Collections.Generic.List<BuildingBonus>
                    {
                        new BuildingBonus { bonusType = primaryBonus, bonusPercent = tier * 2f }
                    }
                };

                tiers[i] = td;
            }

            return tiers;
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);
        }
    }
}
#endif
