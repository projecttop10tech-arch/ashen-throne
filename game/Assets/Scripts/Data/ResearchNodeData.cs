using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a single node in the empire research tree.
    /// Each node has: a unique id, a branch category, resource cost, research time,
    /// prerequisite node ids, and a list of effects applied on completion.
    /// 30 nodes total at launch across 4 branches: Military, Resource, Research, Hero.
    /// </summary>
    [CreateAssetMenu(fileName = "ResearchNode_", menuName = "AshenThrone/Research Node", order = 13)]
    public class ResearchNodeData : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>Unique key used in save data and ServiceLocator lookups. e.g. "military_combat_training_1".</summary>
        public string nodeId;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public ResearchBranch branch;
        /// <summary>Visual grid position for UI layout (not a game-world position).</summary>
        public Vector2Int gridPosition;

        [Header("Cost")]
        public int stoneCost;
        public int ironCost;
        public int grainCost;
        public int arcaneEssenceCost;

        [Header("Research Time")]
        /// <summary>Research duration in seconds. Not capped by build timer rules (research is a separate system).</summary>
        [Range(60, 86400)] public int researchTimeSeconds;

        [Header("Prerequisites")]
        /// <summary>All listed node IDs must be completed before this node can be researched.</summary>
        public string[] prerequisiteNodeIds;

        [Header("Effects")]
        /// <summary>Applied to the player's empire/combat state upon completion.</summary>
        public List<ResearchEffect> effects = new();

        [Header("Unlock Gate")]
        /// <summary>Academy tier required before this node can be queued.</summary>
        public int requiredAcademyTier = 1;
    }

    [System.Serializable]
    public class ResearchEffect
    {
        public ResearchEffectType effectType;
        /// <summary>Magnitude — interpretation depends on effectType (e.g. 10 = +10%).</summary>
        public float magnitude;
        /// <summary>Optional free-text tooltip shown in UI for complex effects.</summary>
        public string description;
    }

    public enum ResearchBranch
    {
        Military,
        Resource,
        Research,
        Hero
    }

    public enum ResearchEffectType
    {
        // Military
        CombatAttackPercent,
        CombatDefensePercent,
        CombatSpeedPercent,
        CombatCritChancePercent,
        CombatPowerPercent,

        // Resource
        IronProductionPercent,
        GrainProductionPercent,
        ArcaneProductionPercent,
        StoneProductionPercent,
        VaultCapacityPercent,
        BuildCostReductionPercent,

        // Research
        ResearchTimeReductionPercent,
        HealingReceivedPercent,
        AllStatsCombatPercent,

        // Hero
        HeroXpGainPercent,
        StarTierCostReductionPercent,
        PveCritChancePercent,
        AllianceContributionPercent,

        // Unlock gates (no numeric magnitude — presence = unlocked)
        UnlockFormations,
        UnlockSiegeWorkshop,
        UnlockEliteResearch,
        UnlockRareHeroShards,
        UnlockComboSkillsAtStarTier2
    }
}
