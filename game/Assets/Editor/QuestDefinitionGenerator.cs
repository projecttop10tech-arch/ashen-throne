#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AshenThrone.Data;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates 30 QuestDefinition ScriptableObject assets:
    /// 10 daily, 10 weekly, 10 one-time quests.
    /// Uses reflection to set properties with private setters.
    /// Safe to re-run — overwrites existing assets.
    /// </summary>
    public static class QuestDefinitionGenerator
    {
        private const string OutputPath = "Assets/Data/Quests";

        [MenuItem("Ashen Throne/Generate Quest Definitions")]
        public static void Generate()
        {
            EnsureDirectory();
            int count = 0;

            // --- Daily Quests (10) ---
            CreateQuest("daily_win_combat_3", "Battlefield Victor", "Win 3 combat battles.",
                QuestCadence.Daily, QuestObjectiveType.WinCombatBattles, 3,
                bpPoints: 50, stone: 500, iron: 300);
            CreateQuest("daily_complete_pve_2", "Dungeon Delver", "Complete 2 PvE levels.",
                QuestCadence.Daily, QuestObjectiveType.CompletePveLevels, 2,
                bpPoints: 50, stone: 400, grain: 400);
            CreateQuest("daily_upgrade_building", "Master Builder", "Upgrade any building once.",
                QuestCadence.Daily, QuestObjectiveType.UpgradeBuilding, 1,
                bpPoints: 40, iron: 600);
            CreateQuest("daily_collect_resources_5", "Harvest Time", "Collect resources 5 times.",
                QuestCadence.Daily, QuestObjectiveType.CollectResources, 5,
                bpPoints: 30, grain: 500);
            CreateQuest("daily_collect_resources_10", "Stockpile", "Collect resources 10 times.",
                QuestCadence.Daily, QuestObjectiveType.CollectResources, 10,
                bpPoints: 60, stone: 300, iron: 300, grain: 300);
            CreateQuest("daily_level_hero", "Hero Training", "Level up any hero once.",
                QuestCadence.Daily, QuestObjectiveType.LevelUpHero, 1,
                bpPoints: 50, arcane: 200);
            CreateQuest("daily_send_chat", "Social Butterfly", "Send 3 alliance chat messages.",
                QuestCadence.Daily, QuestObjectiveType.SendAllianceChatMessages, 3,
                bpPoints: 20, grain: 300);
            CreateQuest("daily_complete_pve_5", "Campaign Hero", "Complete 5 PvE levels.",
                QuestCadence.Daily, QuestObjectiveType.CompletePveLevels, 5,
                bpPoints: 80, stone: 600, iron: 400, shards: 1);
            CreateQuest("daily_login", "Daily Check-In", "Log in today.",
                QuestCadence.Daily, QuestObjectiveType.LoginDays, 1,
                bpPoints: 30, stone: 200, iron: 200, grain: 200, arcane: 100);
            CreateQuest("daily_win_combat_5", "Warmonger", "Win 5 combat battles.",
                QuestCadence.Daily, QuestObjectiveType.WinCombatBattles, 5,
                bpPoints: 80, iron: 800, shards: 1);
            count += 10;

            // --- Weekly Quests (10) ---
            CreateQuest("weekly_win_pvp_5", "Arena Champion", "Win 5 PvP battles this week.",
                QuestCadence.Weekly, QuestObjectiveType.WinPvpBattles, 5,
                bpPoints: 200, stone: 2000, iron: 2000, shards: 3);
            CreateQuest("weekly_capture_territory", "Land Grab", "Capture any territory.",
                QuestCadence.Weekly, QuestObjectiveType.CaptureTerritory, 1,
                bpPoints: 150, stone: 1500, iron: 1500);
            CreateQuest("weekly_research_2", "Scholar", "Complete 2 research nodes.",
                QuestCadence.Weekly, QuestObjectiveType.CompleteResearchNode, 2,
                bpPoints: 150, arcane: 1000);
            CreateQuest("weekly_join_rally", "Rally Cry", "Join an alliance rally.",
                QuestCadence.Weekly, QuestObjectiveType.JoinRally, 1,
                bpPoints: 100, grain: 2000);
            CreateQuest("weekly_spend_speedups_5", "Time Bender", "Use 5 speedup items.",
                QuestCadence.Weekly, QuestObjectiveType.SpendSpeedups, 5,
                bpPoints: 120, stone: 1000, iron: 1000);
            CreateQuest("weekly_upgrade_building_5", "Architect", "Upgrade buildings 5 times.",
                QuestCadence.Weekly, QuestObjectiveType.UpgradeBuilding, 5,
                bpPoints: 180, stone: 2500, grain: 1500);
            CreateQuest("weekly_win_combat_20", "Veteran", "Win 20 combat battles.",
                QuestCadence.Weekly, QuestObjectiveType.WinCombatBattles, 20,
                bpPoints: 250, iron: 3000, shards: 5);
            CreateQuest("weekly_level_hero_3", "Mentor", "Level up heroes 3 times.",
                QuestCadence.Weekly, QuestObjectiveType.LevelUpHero, 3,
                bpPoints: 150, arcane: 800, shards: 2);
            CreateQuest("weekly_claim_bp_3", "Pass Progress", "Claim 3 Battle Pass rewards.",
                QuestCadence.Weekly, QuestObjectiveType.ClaimBattlePassRewards, 3,
                bpPoints: 100, stone: 500, iron: 500, grain: 500, arcane: 500);
            CreateQuest("weekly_complete_pve_15", "Campaign Crusader", "Complete 15 PvE levels.",
                QuestCadence.Weekly, QuestObjectiveType.CompletePveLevels, 15,
                bpPoints: 300, stone: 3000, iron: 2000, grain: 2000, shards: 5);
            count += 10;

            // --- One-Time Quests (10) ---
            CreateQuest("onetime_first_building", "Foundation", "Build your first building.",
                QuestCadence.OneTime, QuestObjectiveType.UpgradeBuilding, 1,
                bpPoints: 100, stone: 1000, iron: 500);
            CreateQuest("onetime_complete_chapter1", "Chapter 1 Complete", "Complete all 5 levels of Chapter 1.",
                QuestCadence.OneTime, QuestObjectiveType.CompletePveLevels, 5,
                bpPoints: 300, stone: 3000, iron: 2000, shards: 5);
            CreateQuest("onetime_join_alliance", "Band Together", "Join an alliance for the first time.",
                QuestCadence.OneTime, QuestObjectiveType.JoinRally, 1,
                bpPoints: 200, grain: 3000);
            CreateQuest("onetime_first_research", "First Discovery", "Complete your first research node.",
                QuestCadence.OneTime, QuestObjectiveType.CompleteResearchNode, 1,
                bpPoints: 150, arcane: 500);
            CreateQuest("onetime_win_pvp_1", "Debut Duel", "Win your first PvP battle.",
                QuestCadence.OneTime, QuestObjectiveType.WinPvpBattles, 1,
                bpPoints: 200, iron: 2000, shards: 3);
            CreateQuest("onetime_stronghold_5", "Rising Power", "Upgrade Stronghold to Level 5.",
                QuestCadence.OneTime, QuestObjectiveType.UpgradeBuilding, 5,
                bpPoints: 500, stone: 5000, iron: 5000, grain: 3000, arcane: 2000, shards: 10,
                contextTag: "stronghold");
            CreateQuest("onetime_hero_level_20", "Elite Training", "Get any hero to level 20.",
                QuestCadence.OneTime, QuestObjectiveType.LevelUpHero, 20,
                bpPoints: 400, arcane: 3000, shards: 8);
            CreateQuest("onetime_capture_3_territories", "Conqueror", "Capture 3 territories.",
                QuestCadence.OneTime, QuestObjectiveType.CaptureTerritory, 3,
                bpPoints: 300, stone: 4000, iron: 4000, shards: 5);
            CreateQuest("onetime_complete_chapter4", "Story Master", "Complete all levels through Chapter 4.",
                QuestCadence.OneTime, QuestObjectiveType.CompletePveLevels, 20,
                bpPoints: 500, stone: 5000, iron: 5000, grain: 5000, arcane: 5000, shards: 15);
            CreateQuest("onetime_win_100_battles", "Centurion", "Win 100 combat battles total.",
                QuestCadence.OneTime, QuestObjectiveType.WinCombatBattles, 100,
                bpPoints: 1000, stone: 10000, iron: 10000, shards: 20);
            count += 10;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[QuestDefinitionGenerator] Generated {count} QuestDefinition assets in {OutputPath}/");
        }

        private static void CreateQuest(
            string questId, string displayName, string description,
            QuestCadence cadence, QuestObjectiveType objectiveType, int requiredCount,
            int bpPoints = 0, int stone = 0, int iron = 0, int grain = 0, int arcane = 0,
            int shards = 0, string contextTag = "")
        {
            string assetPath = $"{OutputPath}/Quest_{questId}.asset";
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();

            // QuestDefinition uses [field: SerializeField] with private setters.
            // In Editor code, we use SerializedObject to set these values.
            AssetDatabase.CreateAsset(quest, assetPath);

            var so = new SerializedObject(quest);
            SetProperty(so, "<QuestId>k__BackingField", questId);
            SetProperty(so, "<DisplayName>k__BackingField", displayName);
            SetProperty(so, "<Description>k__BackingField", description);
            SetPropertyEnum(so, "<Cadence>k__BackingField", (int)cadence);
            SetPropertyEnum(so, "<ObjectiveType>k__BackingField", (int)objectiveType);
            SetPropertyInt(so, "<RequiredCount>k__BackingField", requiredCount);
            SetPropertyInt(so, "<BattlePassPoints>k__BackingField", bpPoints);
            SetPropertyInt(so, "<StoneReward>k__BackingField", stone);
            SetPropertyInt(so, "<IronReward>k__BackingField", iron);
            SetPropertyInt(so, "<GrainReward>k__BackingField", grain);
            SetPropertyInt(so, "<ArcaneReward>k__BackingField", arcane);
            SetPropertyInt(so, "<HeroShardReward>k__BackingField", shards);
            SetProperty(so, "<ContextTag>k__BackingField", contextTag);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetProperty(SerializedObject so, string fieldName, string value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetPropertyInt(SerializedObject so, string fieldName, int value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.intValue = value;
        }

        private static void SetPropertyEnum(SerializedObject so, string fieldName, int value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.enumValueIndex = value;
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);
        }
    }
}
#endif
