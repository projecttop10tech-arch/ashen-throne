using System.IO;
using UnityEditor;
using UnityEngine;
using AshenThrone.Events;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates the 5 launch EventDefinition ScriptableObject assets for Phase 5.
    ///
    /// Events created:
    ///   1. Dragon Siege       — ServerWide world boss, DamageDealt objective
    ///   2. Alliance Tournament — Alliance war, TerritoryCaptures objective
    ///   3. Harvest Crisis      — Solo resource PvE, ResourcesCollected objective
    ///   4. Shard Hunt          — Solo collection race, ShardsEarned objective
    ///   5. Void Rift           — Solo roguelite dungeon, DungeonFloorsCleared objective
    ///
    /// Run once via: Tools → AshenThrone → Generate Launch Events
    /// Assets are placed in Assets/Data/Events/ and require no further setup.
    /// </summary>
    public static class LaunchEventGenerator
    {
        private const string OutputPath = "Assets/Data/Events";

        [MenuItem("Tools/AshenThrone/Generate Launch Events")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Application.dataPath + "/../" + OutputPath);
            AssetDatabase.Refresh();

            GenerateDragonSiege();
            GenerateAllianceTournament();
            GenerateHarvestCrisis();
            GenerateShardHunt();
            GenerateVoidRift();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LaunchEventGenerator] 5 launch event assets generated at " + OutputPath);
        }

        // -------------------------------------------------------------------
        // 1. Dragon Siege — ServerWide world boss
        // -------------------------------------------------------------------

        private static void GenerateDragonSiege()
        {
            var def = CreateOrLoad("Event_DragonSiege");
            def.eventId      = "dragon_siege";
            def.displayName  = "Dragon Siege";
            def.description  = "The Ashen Dragon awakens. All alliances must unite — deal damage to the dragon before it destroys the capital city.";
            def.scope        = EventScope.ServerWide;
            def.startTimeIso = "2026-04-01T18:00:00Z";
            def.endTimeIso   = "2026-04-03T18:00:00Z";
            def.objectiveType   = EventObjectiveType.DamageDealt;
            def.objectiveTarget = 10_000_000; // 10M total damage to dragon

            def.milestoneRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "dragon_siege_reward_1", ProgressThreshold = 1_000_000,  ResourceAmount = 500,  ItemId = "resource_iron" },
                new EventReward { RewardId = "dragon_siege_reward_2", ProgressThreshold = 5_000_000,  ResourceAmount = 1000, ItemId = "resource_iron" },
                new EventReward { RewardId = "dragon_siege_reward_3", ProgressThreshold = 10_000_000, ResourceAmount = 200,  ItemId = "hero_shard_token" }
            };
            def.completionRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "dragon_siege_complete", ProgressThreshold = 10_000_000, ResourceAmount = 0, ItemId = "cosmetic_dragon_banner" }
            };

            EditorUtility.SetDirty(def);
        }

        // -------------------------------------------------------------------
        // 2. Alliance Tournament — Alliance territory war
        // -------------------------------------------------------------------

        private static void GenerateAllianceTournament()
        {
            var def = CreateOrLoad("Event_AllianceTournament");
            def.eventId      = "alliance_tournament";
            def.displayName  = "Alliance Tournament";
            def.description  = "Alliances compete in bracket-based territory wars. Capture the most regions to advance through rounds and claim the Champion's Throne.";
            def.scope        = EventScope.Alliance;
            def.startTimeIso = "2026-04-07T12:00:00Z";
            def.endTimeIso   = "2026-04-14T12:00:00Z";
            def.objectiveType   = EventObjectiveType.TerritoryCaptures;
            def.objectiveTarget = 50; // Capture 50 territories as an alliance

            def.milestoneRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "tournament_reward_1", ProgressThreshold = 10, ResourceAmount = 300, ItemId = "resource_arcane_essence" },
                new EventReward { RewardId = "tournament_reward_2", ProgressThreshold = 30, ResourceAmount = 600, ItemId = "resource_arcane_essence" },
                new EventReward { RewardId = "tournament_reward_3", ProgressThreshold = 50, ResourceAmount = 500, ItemId = "hero_shard_token" }
            };
            def.completionRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "tournament_complete", ProgressThreshold = 50, ResourceAmount = 0, ItemId = "cosmetic_champion_throne_skin" }
            };

            EditorUtility.SetDirty(def);
        }

        // -------------------------------------------------------------------
        // 3. Harvest Crisis — Solo resource PvE
        // -------------------------------------------------------------------

        private static void GenerateHarvestCrisis()
        {
            var def = CreateOrLoad("Event_HarvestCrisis");
            def.eventId      = "harvest_crisis";
            def.displayName  = "Harvest Crisis";
            def.description  = "Void Raiders are pillaging the harvest fields. Defend and collect as many resources as you can before the season ends.";
            def.scope        = EventScope.Solo;
            def.startTimeIso = "2026-04-15T00:00:00Z";
            def.endTimeIso   = "2026-04-22T00:00:00Z";
            def.objectiveType   = EventObjectiveType.ResourcesCollected;
            def.objectiveTarget = 100_000; // Collect 100K total resources

            def.milestoneRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "harvest_reward_1", ProgressThreshold = 10_000, ResourceAmount = 200, ItemId = "resource_grain" },
                new EventReward { RewardId = "harvest_reward_2", ProgressThreshold = 50_000, ResourceAmount = 400, ItemId = "resource_grain" },
                new EventReward { RewardId = "harvest_reward_3", ProgressThreshold = 100_000, ResourceAmount = 150, ItemId = "hero_shard_token" }
            };
            def.completionRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "harvest_complete", ProgressThreshold = 100_000, ResourceAmount = 0, ItemId = "cosmetic_harvest_keep_skin" }
            };

            EditorUtility.SetDirty(def);
        }

        // -------------------------------------------------------------------
        // 4. Shard Hunt — Solo hero shard collection race
        // -------------------------------------------------------------------

        private static void GenerateShardHunt()
        {
            var def = CreateOrLoad("Event_ShardHunt");
            def.eventId      = "shard_hunt";
            def.displayName  = "Shard Hunt";
            def.description  = "Ancient power crystals scatter across the realm. Be the first to collect enough hero shards to claim the legendary Ashenmourne.";
            def.scope        = EventScope.Solo;
            def.startTimeIso = "2026-04-22T00:00:00Z";
            def.endTimeIso   = "2026-04-29T00:00:00Z";
            def.objectiveType   = EventObjectiveType.ShardsEarned;
            def.objectiveTarget = 500; // Earn 500 hero shards during the event

            def.milestoneRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "shard_hunt_reward_1", ProgressThreshold = 100, ResourceAmount = 100, ItemId = "resource_arcane_essence" },
                new EventReward { RewardId = "shard_hunt_reward_2", ProgressThreshold = 300, ResourceAmount = 200, ItemId = "resource_arcane_essence" },
                new EventReward { RewardId = "shard_hunt_reward_3", ProgressThreshold = 500, ResourceAmount = 0,   ItemId = "hero_ashenmourne_fragment" }
            };
            def.completionRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "shard_hunt_complete", ProgressThreshold = 500, ResourceAmount = 0, ItemId = "cosmetic_shard_hunter_emote" }
            };

            EditorUtility.SetDirty(def);
        }

        // -------------------------------------------------------------------
        // 5. Void Rift — Roguelite dungeon variant
        // -------------------------------------------------------------------

        private static void GenerateVoidRift()
        {
            var def = CreateOrLoad("Event_VoidRift");
            def.eventId      = "void_rift";
            def.displayName  = "Void Rift";
            def.description  = "A rift in reality tears open the dungeon floors. Descend as deep as you dare — each floor cleared earns score toward the weekly leaderboard.";
            def.scope        = EventScope.Solo;
            def.startTimeIso = "2026-04-29T00:00:00Z";
            def.endTimeIso   = "2026-05-06T00:00:00Z";
            def.objectiveType   = EventObjectiveType.DungeonFloorsCleared;
            def.objectiveTarget = 100; // Clear 100 total dungeon floors across all runs

            def.milestoneRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "void_rift_reward_1", ProgressThreshold = 10,  ResourceAmount = 150, ItemId = "resource_stone" },
                new EventReward { RewardId = "void_rift_reward_2", ProgressThreshold = 50,  ResourceAmount = 300, ItemId = "resource_iron" },
                new EventReward { RewardId = "void_rift_reward_3", ProgressThreshold = 100, ResourceAmount = 200, ItemId = "hero_shard_token" }
            };
            def.completionRewards = new System.Collections.Generic.List<EventReward>
            {
                new EventReward { RewardId = "void_rift_complete", ProgressThreshold = 100, ResourceAmount = 0, ItemId = "cosmetic_void_rift_portal_skin" }
            };

            EditorUtility.SetDirty(def);
        }

        // -------------------------------------------------------------------
        // Helper
        // -------------------------------------------------------------------

        private static EventDefinition CreateOrLoad(string assetName)
        {
            string fullPath = OutputPath + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<EventDefinition>(fullPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<EventDefinition>();
            AssetDatabase.CreateAsset(asset, fullPath);
            return asset;
        }
    }
}
