using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Combat;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a single PvE story encounter.
    /// Specifies which enemy heroes appear, their levels, placement, the narrative
    /// context, and the rewards granted on completion.
    /// One asset per level. Levels are loaded by PveEncounterManager at battle start.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "AshenThrone/PvE Level", order = 5)]
    public class PveLevelData : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>Unique ID. Used for save progress tracking. Never change after ship.</summary>
        public string levelId;
        public string displayName;
        /// <summary>Chapter number (1-based). Levels within a chapter share a story arc.</summary>
        public int chapterNumber;
        /// <summary>Level number within the chapter (1-based).</summary>
        public int levelNumber;

        [Header("Narrative")]
        [TextArea(3, 8)] public string openingNarrative;
        [TextArea(3, 8)] public string victoryNarrative;
        [TextArea(3, 8)] public string defeatNarrative;

        [Header("Enemy Configuration")]
        /// <summary>Enemy hero definitions for this encounter. Max 3.</summary>
        public List<EnemyEntry> enemies = new();
        /// <summary>Special terrain tiles pre-set at battle start. Applied before combat begins.</summary>
        public List<TerrainPreset> terrainPresets = new();

        [Header("Difficulty")]
        public LevelDifficulty difficulty = LevelDifficulty.Normal;
        /// <summary>True if this is a boss encounter (boss has higher HP/special mechanics).</summary>
        public bool isBossLevel;

        [Header("Victory Rewards")]
        public int xpReward;
        public int[] heroShardRewards; // Array of hero shard counts indexed to heroes array
        /// <summary>Specific hero whose shards are rewarded on first clear.</summary>
        public HeroData firstClearHeroShardReward;
        public int firstClearShardAmount = 5;

        [Header("Unlock Requirements")]
        /// <summary>Level that must be completed before this one unlocks. Empty = always available.</summary>
        public string requiredLevelId;
        /// <summary>Minimum player Stronghold level to attempt this encounter.</summary>
        public int requiredStrongholdLevel = 1;
    }

    [System.Serializable]
    public class EnemyEntry
    {
        /// <summary>The enemy hero definition ScriptableObject.</summary>
        public HeroData heroData;
        /// <summary>Level the enemy scales to for this encounter.</summary>
        [Range(1, 80)] public int level = 1;
        /// <summary>Override preferred row for this encounter. Uses HeroData.preferredRow if None.</summary>
        public CombatRow rowOverride = CombatRow.Front;
    }

    [System.Serializable]
    public class TerrainPreset
    {
        public int column;
        [Range(0, 4)] public int row;
        public TileType tileType;
    }

    public enum LevelDifficulty { Easy, Normal, Hard, Elite, Boss }
}
