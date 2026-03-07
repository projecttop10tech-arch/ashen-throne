// Editor-only script. Generates all 30 ResearchNodeData assets for the launch research tree.
// Menu: AshenThrone → Generate Research Tree
// Assets saved to: Assets/Data/Research/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AshenThrone.Data;

namespace AshenThrone.Editor
{
    public static class ResearchTreeGenerator
    {
        private const string OutputPath = "Assets/Data/Research";

        [MenuItem("AshenThrone/Generate Research Tree")]
        public static void GenerateResearchTree()
        {
            EnsureDirectory(OutputPath);
            var definitions = BuildDefinitions();
            int created = 0;
            foreach (var def in definitions)
            {
                string path = $"{OutputPath}/{def.Id}.asset";
                ResearchNodeData existing = AssetDatabase.LoadAssetAtPath<ResearchNodeData>(path);
                if (existing != null)
                {
                    Apply(existing, def);
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance<ResearchNodeData>();
                    Apply(asset, def);
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ResearchTreeGenerator] Done — {created} new, {definitions.Count - created} updated. Total: {definitions.Count} nodes.");
        }

        private static void Apply(ResearchNodeData asset, NodeDef def)
        {
            asset.nodeId = def.Id;
            asset.displayName = def.Name;
            asset.description = def.Description;
            asset.branch = def.Branch;
            asset.gridPosition = def.GridPos;
            asset.stoneCost = def.Stone;
            asset.ironCost = def.Iron;
            asset.grainCost = def.Grain;
            asset.arcaneEssenceCost = def.Arcane;
            asset.researchTimeSeconds = def.TimeSeconds;
            asset.prerequisiteNodeIds = def.Prerequisites ?? System.Array.Empty<string>();
            asset.requiredAcademyTier = def.AcademyTier;
            asset.effects = def.Effects ?? new List<ResearchEffect>();
        }

        private static List<NodeDef> BuildDefinitions()
        {
            return new List<NodeDef>
            {
                // ═══════════════ MILITARY BRANCH (8 nodes) ═══════════════
                new NodeDef("military_combat_training_1", "Combat Training I", "All heroes gain +5% attack.",
                    ResearchBranch.Military, new Vector2Int(0, 0),
                    stone: 200, iron: 150, grain: 100, arcane: 0, time: 300, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatAttackPercent, magnitude = 5f, description = "+5% attack for all heroes" }
                    }),

                new NodeDef("military_combat_training_2", "Combat Training II", "All heroes gain +10% attack (cumulative with Training I).",
                    ResearchBranch.Military, new Vector2Int(0, 1),
                    stone: 500, iron: 400, grain: 200, arcane: 0, time: 900, academyTier: 1,
                    prerequisites: new[] { "military_combat_training_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatAttackPercent, magnitude = 10f, description = "+10% attack for all heroes" }
                    }),

                new NodeDef("military_iron_discipline", "Iron Discipline", "All heroes gain +5% defense in combat.",
                    ResearchBranch.Military, new Vector2Int(1, 0),
                    stone: 300, iron: 200, grain: 100, arcane: 0, time: 600, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatDefensePercent, magnitude = 5f, description = "+5% defense for all heroes" }
                    }),

                new NodeDef("military_war_drums", "War Drums", "All heroes gain +10% speed in combat.",
                    ResearchBranch.Military, new Vector2Int(1, 1),
                    stone: 400, iron: 300, grain: 150, arcane: 50, time: 1200, academyTier: 2,
                    prerequisites: new[] { "military_iron_discipline" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatSpeedPercent, magnitude = 10f, description = "+10% speed for all heroes" }
                    }),

                new NodeDef("military_advanced_tactics", "Advanced Tactics", "Unlocks 3-hero formation bonuses in combat.",
                    ResearchBranch.Military, new Vector2Int(2, 0),
                    stone: 600, iron: 500, grain: 300, arcane: 100, time: 1800, academyTier: 2,
                    prerequisites: new[] { "military_combat_training_1", "military_iron_discipline" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.UnlockFormations, magnitude = 1f, description = "Unlocks formation bonuses" }
                    }),

                new NodeDef("military_siege_mastery", "Siege Mastery", "Unlocks Siege Workshop upgrades in the Military District.",
                    ResearchBranch.Military, new Vector2Int(2, 1),
                    stone: 800, iron: 700, grain: 400, arcane: 150, time: 2400, academyTier: 2,
                    prerequisites: new[] { "military_war_drums" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.UnlockSiegeWorkshop, magnitude = 1f, description = "Unlocks Siege Workshop" }
                    }),

                new NodeDef("military_elite_warfare", "Elite Warfare", "+15% crit chance for all heroes in combat.",
                    ResearchBranch.Military, new Vector2Int(3, 0),
                    stone: 1200, iron: 1000, grain: 600, arcane: 200, time: 3600, academyTier: 3,
                    prerequisites: new[] { "military_combat_training_2", "military_advanced_tactics" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatCritChancePercent, magnitude = 15f, description = "+15% crit chance" }
                    }),

                new NodeDef("military_battlefield_supremacy", "Battlefield Supremacy", "+15% total combat power for all heroes.",
                    ResearchBranch.Military, new Vector2Int(3, 1),
                    stone: 2000, iron: 1800, grain: 1000, arcane: 400, time: 7200, academyTier: 3,
                    prerequisites: new[] { "military_elite_warfare", "military_siege_mastery" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.CombatPowerPercent, magnitude = 15f, description = "+15% combat power" }
                    }),

                // ═══════════════ RESOURCE BRANCH (7 nodes) ═══════════════
                new NodeDef("resource_efficient_mining", "Efficient Mining", "+10% iron production from all mines.",
                    ResearchBranch.Resource, new Vector2Int(0, 0),
                    stone: 150, iron: 100, grain: 80, arcane: 0, time: 300, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.IronProductionPercent, magnitude = 10f, description = "+10% iron production" }
                    }),

                new NodeDef("resource_fertile_fields", "Fertile Fields", "+10% grain production from all farms.",
                    ResearchBranch.Resource, new Vector2Int(1, 0),
                    stone: 120, iron: 80, grain: 100, arcane: 0, time: 300, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.GrainProductionPercent, magnitude = 10f, description = "+10% grain production" }
                    }),

                new NodeDef("resource_arcane_tapping", "Arcane Tapping", "+10% arcane essence production.",
                    ResearchBranch.Resource, new Vector2Int(2, 0),
                    stone: 100, iron: 80, grain: 60, arcane: 100, time: 600, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.ArcaneProductionPercent, magnitude = 10f, description = "+10% arcane production" }
                    }),

                new NodeDef("resource_quarry_mastery", "Quarry Mastery", "+10% stone production from all quarries.",
                    ResearchBranch.Resource, new Vector2Int(0, 1),
                    stone: 200, iron: 100, grain: 80, arcane: 0, time: 450, academyTier: 1,
                    prerequisites: new[] { "resource_efficient_mining" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.StoneProductionPercent, magnitude = 10f, description = "+10% stone production" }
                    }),

                new NodeDef("resource_vault_expansion_1", "Vault Expansion I", "+20% vault capacity for all resource types.",
                    ResearchBranch.Resource, new Vector2Int(1, 1),
                    stone: 400, iron: 300, grain: 250, arcane: 50, time: 900, academyTier: 2,
                    prerequisites: new[] { "resource_fertile_fields", "resource_arcane_tapping" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.VaultCapacityPercent, magnitude = 20f, description = "+20% vault capacity" }
                    }),

                new NodeDef("resource_vault_expansion_2", "Vault Expansion II", "+40% vault capacity (cumulative with Expansion I).",
                    ResearchBranch.Resource, new Vector2Int(1, 2),
                    stone: 1000, iron: 800, grain: 600, arcane: 200, time: 2700, academyTier: 2,
                    prerequisites: new[] { "resource_vault_expansion_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.VaultCapacityPercent, magnitude = 40f, description = "+40% vault capacity" }
                    }),

                new NodeDef("resource_efficiency", "Resource Efficiency", "Reduces all building construction costs by 10%.",
                    ResearchBranch.Resource, new Vector2Int(2, 1),
                    stone: 800, iron: 600, grain: 500, arcane: 150, time: 2400, academyTier: 3,
                    prerequisites: new[] { "resource_quarry_mastery", "resource_vault_expansion_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.BuildCostReductionPercent, magnitude = 10f, description = "-10% building costs" }
                    }),

                // ═══════════════ RESEARCH BRANCH (7 nodes) ═══════════════
                new NodeDef("research_scholarship_1", "Scholarship", "-10% research time for all nodes.",
                    ResearchBranch.Research, new Vector2Int(0, 0),
                    stone: 100, iron: 80, grain: 60, arcane: 200, time: 600, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.ResearchTimeReductionPercent, magnitude = 10f, description = "-10% research time" }
                    }),

                new NodeDef("research_scholarship_2", "Advanced Scholarship", "-20% research time (cumulative with Scholarship).",
                    ResearchBranch.Research, new Vector2Int(0, 1),
                    stone: 300, iron: 200, grain: 150, arcane: 500, time: 1800, academyTier: 2,
                    prerequisites: new[] { "research_scholarship_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.ResearchTimeReductionPercent, magnitude = 20f, description = "-20% research time" }
                    }),

                new NodeDef("research_library_archives", "Library Archives", "Unlocks Tier 2 research nodes across all branches.",
                    ResearchBranch.Research, new Vector2Int(1, 0),
                    stone: 400, iron: 300, grain: 200, arcane: 300, time: 1200, academyTier: 2,
                    prerequisites: new[] { "research_scholarship_1" },
                    effects: new List<ResearchEffect>()), // Unlock effect handled implicitly by AcademyTier gating

                new NodeDef("research_alchemy", "Alchemy", "Heroes receive +10% healing from all sources.",
                    ResearchBranch.Research, new Vector2Int(1, 1),
                    stone: 200, iron: 150, grain: 100, arcane: 400, time: 1500, academyTier: 2,
                    prerequisites: new[] { "research_library_archives" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.HealingReceivedPercent, magnitude = 10f, description = "+10% healing received" }
                    }),

                new NodeDef("research_ancient_texts", "Ancient Texts", "Unlocks Elite-tier research nodes.",
                    ResearchBranch.Research, new Vector2Int(2, 0),
                    stone: 800, iron: 600, grain: 400, arcane: 800, time: 3600, academyTier: 3,
                    prerequisites: new[] { "research_scholarship_2", "research_library_archives" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.UnlockEliteResearch, magnitude = 1f, description = "Unlocks elite research tier" }
                    }),

                new NodeDef("research_master_alchemy", "Master Alchemy", "All hero stats are increased by +5% in combat.",
                    ResearchBranch.Research, new Vector2Int(1, 2),
                    stone: 600, iron: 500, grain: 350, arcane: 700, time: 2700, academyTier: 3,
                    prerequisites: new[] { "research_alchemy", "research_ancient_texts" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.AllStatsCombatPercent, magnitude = 5f, description = "+5% all hero stats" }
                    }),

                new NodeDef("research_forbidden_knowledge", "Forbidden Knowledge", "Unlocks the Void research sub-branch (passive bonuses only).",
                    ResearchBranch.Research, new Vector2Int(2, 1),
                    stone: 1500, iron: 1200, grain: 800, arcane: 2000, time: 7200, academyTier: 3,
                    prerequisites: new[] { "research_master_alchemy", "research_ancient_texts" },
                    effects: new List<ResearchEffect>()), // Structural unlock — sub-branch gated by this in tree UI

                // ═══════════════ HERO BRANCH (8 nodes) ═══════════════
                new NodeDef("hero_training_1", "Hero Training", "All heroes gain +5% XP from all sources.",
                    ResearchBranch.Hero, new Vector2Int(0, 0),
                    stone: 100, iron: 80, grain: 100, arcane: 150, time: 300, academyTier: 1,
                    prerequisites: null,
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.HeroXpGainPercent, magnitude = 5f, description = "+5% hero XP gain" }
                    }),

                new NodeDef("hero_training_2", "Advanced Training", "All heroes gain +10% XP (cumulative with Training I).",
                    ResearchBranch.Hero, new Vector2Int(0, 1),
                    stone: 300, iron: 200, grain: 250, arcane: 400, time: 900, academyTier: 2,
                    prerequisites: new[] { "hero_training_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.HeroXpGainPercent, magnitude = 10f, description = "+10% hero XP gain" }
                    }),

                new NodeDef("hero_star_forging_1", "Star Forging I", "Reduces hero star-tier upgrade costs by 10%.",
                    ResearchBranch.Hero, new Vector2Int(1, 0),
                    stone: 200, iron: 150, grain: 150, arcane: 300, time: 600, academyTier: 1,
                    prerequisites: new[] { "hero_training_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.StarTierCostReductionPercent, magnitude = 10f, description = "-10% star-tier costs" }
                    }),

                new NodeDef("hero_star_forging_2", "Star Forging II", "Reduces hero star-tier upgrade costs by 20% (cumulative).",
                    ResearchBranch.Hero, new Vector2Int(1, 1),
                    stone: 600, iron: 500, grain: 400, arcane: 700, time: 2400, academyTier: 2,
                    prerequisites: new[] { "hero_star_forging_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.StarTierCostReductionPercent, magnitude = 20f, description = "-20% star-tier costs" }
                    }),

                new NodeDef("hero_combat_instincts", "Combat Instincts", "+5% crit chance for all heroes in PvE battles.",
                    ResearchBranch.Hero, new Vector2Int(2, 0),
                    stone: 300, iron: 250, grain: 200, arcane: 350, time: 1200, academyTier: 2,
                    prerequisites: new[] { "hero_training_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.PveCritChancePercent, magnitude = 5f, description = "+5% PvE crit chance" }
                    }),

                new NodeDef("hero_alliance_bonds", "Alliance Bonds", "+10% contribution to alliance research and buildings.",
                    ResearchBranch.Hero, new Vector2Int(2, 1),
                    stone: 400, iron: 300, grain: 300, arcane: 400, time: 1500, academyTier: 2,
                    prerequisites: new[] { "hero_star_forging_1" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.AllianceContributionPercent, magnitude = 10f, description = "+10% alliance contribution" }
                    }),

                new NodeDef("hero_legendary_pursuit", "Legendary Pursuit", "Rare hero shard drops enabled from PvE content.",
                    ResearchBranch.Hero, new Vector2Int(3, 0),
                    stone: 1000, iron: 800, grain: 600, arcane: 1200, time: 5400, academyTier: 3,
                    prerequisites: new[] { "hero_training_2", "hero_combat_instincts" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.UnlockRareHeroShards, magnitude = 1f, description = "Unlocks rare hero shard drops" }
                    }),

                new NodeDef("hero_ancient_legacy", "Ancient Legacy", "Heroes unlock combo skill bonuses at Star Tier 2.",
                    ResearchBranch.Hero, new Vector2Int(3, 1),
                    stone: 1500, iron: 1200, grain: 900, arcane: 1800, time: 7200, academyTier: 3,
                    prerequisites: new[] { "hero_star_forging_2", "hero_alliance_bonds" },
                    effects: new List<ResearchEffect>
                    {
                        new ResearchEffect { effectType = ResearchEffectType.UnlockComboSkillsAtStarTier2, magnitude = 1f, description = "Combo skills unlocked at Star Tier 2" }
                    }),
            };
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        // ─── Internal definition helper ───────────────────────────────────────────

        private class NodeDef
        {
            public string Id, Name, Description;
            public ResearchBranch Branch;
            public Vector2Int GridPos;
            public int Stone, Iron, Grain, Arcane, TimeSeconds, AcademyTier;
            public string[] Prerequisites;
            public List<ResearchEffect> Effects;

            public NodeDef(string id, string name, string desc,
                ResearchBranch branch, Vector2Int gridPos,
                int stone, int iron, int grain, int arcane, int time, int academyTier,
                string[] prerequisites, List<ResearchEffect> effects)
            {
                Id = id; Name = name; Description = desc; Branch = branch; GridPos = gridPos;
                Stone = stone; Iron = iron; Grain = grain; Arcane = arcane;
                TimeSeconds = time; AcademyTier = academyTier;
                Prerequisites = prerequisites; Effects = effects;
            }
        }
    }
}
