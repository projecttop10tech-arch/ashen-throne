#if UNITY_EDITOR
// Run from Unity Editor menu: AshenThrone → Generate Configs
// Creates config ScriptableObject instances in Assets/Resources/ so they can
// be loaded at runtime via Resources.Load<T>("ConfigName").
// Safe to re-run — existing assets are overwritten.

using System.IO;
using UnityEditor;
using UnityEngine;
using AshenThrone.Data;
using AshenThrone.Heroes;
using AshenThrone.UI.Accessibility;

namespace AshenThrone.Editor
{
    public static class ConfigGenerator
    {
        private const string ResourcesPath = "Assets/Resources";

        [MenuItem("AshenThrone/Generate Configs")]
        public static void GenerateAll()
        {
            EnsureDirectory(ResourcesPath);

            GenerateCombatConfig();
            GenerateEmpireConfig();
            GenerateProgressionConfig();
            GenerateTerritoryConfig();
            GenerateQuestDefinitions();
            GenerateAccessibilityConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ConfigGenerator] All config assets generated in Assets/Resources/.");
        }

        private static void GenerateCombatConfig()
        {
            var config = ScriptableObject.CreateInstance<CombatConfig>();
            // All defaults are set in the class definition — no overrides needed
            CreateOrReplace(config, $"{ResourcesPath}/CombatConfig.asset");
            Debug.Log("[ConfigGenerator] Created CombatConfig.asset");
        }

        private static void GenerateEmpireConfig()
        {
            var config = ScriptableObject.CreateInstance<EmpireConfig>();
            // All defaults are set in the class definition — no overrides needed
            CreateOrReplace(config, $"{ResourcesPath}/EmpireConfig.asset");
            Debug.Log("[ConfigGenerator] Created EmpireConfig.asset");
        }

        private static void GenerateProgressionConfig()
        {
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            // Populate the XP curve array from the formula
            config.XpPerLevel = new int[config.MaxHeroLevel - 1];
            for (int i = 0; i < config.XpPerLevel.Length; i++)
                config.XpPerLevel[i] = Mathf.RoundToInt(config.BaseXp * Mathf.Pow(config.GrowthFactor, i));
            CreateOrReplace(config, $"{ResourcesPath}/ProgressionConfig.asset");
            Debug.Log("[ConfigGenerator] Created ProgressionConfig.asset (79-level XP curve populated)");
        }

        private static void GenerateTerritoryConfig()
        {
            var config = ScriptableObject.CreateInstance<TerritoryConfig>();
            // All defaults are set in the class definition — no overrides needed
            CreateOrReplace(config, $"{ResourcesPath}/TerritoryConfig.asset");
            Debug.Log("[ConfigGenerator] Created TerritoryConfig.asset");
        }

        private static void GenerateQuestDefinitions()
        {
            var questDir = "Assets/Resources/Quests";
            EnsureDirectory(questDir);

            var dailyQuests = new (string id, string name, string desc, QuestObjectiveType obj, int count, int bp, int stone, int iron, int grain, int arcane)[]
            {
                ("daily_win_3",    "Daily Victor",       "Win 3 combat battles",       QuestObjectiveType.WinCombatBattles,   3, 100, 500,  500,  200, 0),
                ("daily_pve_2",    "Dungeon Delver",     "Complete 2 PvE levels",       QuestObjectiveType.CompletePveLevels,  2, 100, 300,  300,  100, 50),
                ("daily_upgrade",  "Builder",            "Upgrade any building",        QuestObjectiveType.UpgradeBuilding,    1, 50,  200,  200,  100, 0),
                ("daily_collect",  "Resource Hoarder",   "Collect 1000 resources",      QuestObjectiveType.CollectResources, 1000, 75,  0,    0,    0,   100),
                ("daily_chat",     "Social Butterfly",   "Send 5 alliance messages",    QuestObjectiveType.SendAllianceChatMessages, 5, 25, 100, 100, 100, 0),
            };

            var weeklyQuests = new (string id, string name, string desc, QuestObjectiveType obj, int count, int bp, int stone, int iron, int grain, int arcane, int shards)[]
            {
                ("weekly_win_20",   "War Veteran",       "Win 20 combat battles",       QuestObjectiveType.WinCombatBattles,  20, 500, 2000, 2000, 1000, 200, 5),
                ("weekly_pvp_10",   "Arena Champion",    "Win 10 PvP battles",          QuestObjectiveType.WinPvpBattles,     10, 400, 1500, 1500, 500,  300, 3),
                ("weekly_research", "Scholar",           "Complete 3 research nodes",   QuestObjectiveType.CompleteResearchNode, 3, 300, 1000, 1000, 500, 500, 0),
                ("weekly_levelup",  "Mentor",            "Level up a hero 5 times",     QuestObjectiveType.LevelUpHero,        5, 350, 500,  500,  500,  200, 5),
                ("weekly_rally",    "Rally Commander",   "Join 3 rallies",              QuestObjectiveType.JoinRally,           3, 250, 1000, 1000, 500,  100, 0),
            };

            foreach (var q in dailyQuests)
            {
                var quest = ScriptableObject.CreateInstance<QuestDefinition>();
                SetQuestFields(quest, q.id, q.name, q.desc, QuestCadence.Daily, q.obj, q.count, q.bp, q.stone, q.iron, q.grain, q.arcane, 0);
                CreateOrReplace(quest, $"{questDir}/Quest_{q.id}.asset");
            }

            foreach (var q in weeklyQuests)
            {
                var quest = ScriptableObject.CreateInstance<QuestDefinition>();
                SetQuestFields(quest, q.id, q.name, q.desc, QuestCadence.Weekly, q.obj, q.count, q.bp, q.stone, q.iron, q.grain, q.arcane, q.shards);
                CreateOrReplace(quest, $"{questDir}/Quest_{q.id}.asset");
            }

            Debug.Log($"[ConfigGenerator] Created {dailyQuests.Length + weeklyQuests.Length} QuestDefinition assets in {questDir}");
        }

        private static void SetQuestFields(QuestDefinition quest, string id, string name, string desc,
            QuestCadence cadence, QuestObjectiveType obj, int count, int bp,
            int stone, int iron, int grain, int arcane, int shards)
        {
            // Use SerializedObject to set [field: SerializeField] backing fields
            var so = new SerializedObject(quest);
            so.FindProperty("<QuestId>k__BackingField").stringValue = id;
            so.FindProperty("<DisplayName>k__BackingField").stringValue = name;
            so.FindProperty("<Description>k__BackingField").stringValue = desc;
            so.FindProperty("<Cadence>k__BackingField").enumValueIndex = (int)cadence;
            so.FindProperty("<ObjectiveType>k__BackingField").enumValueIndex = (int)obj;
            so.FindProperty("<RequiredCount>k__BackingField").intValue = count;
            so.FindProperty("<BattlePassPoints>k__BackingField").intValue = bp;
            so.FindProperty("<StoneReward>k__BackingField").intValue = stone;
            so.FindProperty("<IronReward>k__BackingField").intValue = iron;
            so.FindProperty("<GrainReward>k__BackingField").intValue = grain;
            so.FindProperty("<ArcaneReward>k__BackingField").intValue = arcane;
            so.FindProperty("<HeroShardReward>k__BackingField").intValue = shards;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void GenerateAccessibilityConfig()
        {
            var config = ScriptableObject.CreateInstance<AccessibilityConfig>();
            // All defaults are set in the class definition — no overrides needed
            CreateOrReplace(config, $"{ResourcesPath}/AccessibilityConfig.asset");
            Debug.Log("[ConfigGenerator] Created AccessibilityConfig.asset");
        }

        private static void CreateOrReplace(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
