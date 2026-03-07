using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>Type of objective a quest tracks.</summary>
    public enum QuestObjectiveType
    {
        WinCombatBattles,
        CompletePveLevels,
        UpgradeBuilding,
        CompleteResearchNode,
        JoinRally,
        CaptureTerritory,
        SendAllianceChatMessages,
        CollectResources,
        LevelUpHero,
        SpendSpeedups,
        LoginDays,
        WinPvpBattles,
        ClaimBattlePassRewards
    }

    /// <summary>Cadence at which a quest resets.</summary>
    public enum QuestCadence
    {
        Daily,
        Weekly,
        OneTime
    }

    /// <summary>
    /// ScriptableObject defining a single quest (daily, weekly, or one-time).
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_", menuName = "AshenThrone/Quest Definition")]
    public class QuestDefinition : ScriptableObject
    {
        /// <summary>Unique stable identifier for this quest (used in save data).</summary>
        [field: SerializeField] public string QuestId { get; private set; }

        /// <summary>Display name shown in the quest log.</summary>
        [field: SerializeField] public string DisplayName { get; private set; }

        /// <summary>Short description of what the player must do.</summary>
        [field: SerializeField] public string Description { get; private set; }

        /// <summary>How often this quest resets.</summary>
        [field: SerializeField] public QuestCadence Cadence { get; private set; } = QuestCadence.Daily;

        /// <summary>What action to track for completion.</summary>
        [field: SerializeField] public QuestObjectiveType ObjectiveType { get; private set; }

        /// <summary>How many times the objective must be completed.</summary>
        [field: SerializeField] public int RequiredCount { get; private set; } = 1;

        /// <summary>Battle Pass points awarded on completion.</summary>
        [field: SerializeField] public int BattlePassPoints { get; private set; } = 100;

        /// <summary>Resource rewards on completion (stone, iron, grain, arcane).</summary>
        [field: SerializeField] public int StoneReward     { get; private set; }
        [field: SerializeField] public int IronReward      { get; private set; }
        [field: SerializeField] public int GrainReward     { get; private set; }
        [field: SerializeField] public int ArcaneReward    { get; private set; }

        /// <summary>Hero shard rewards on completion (0 = none).</summary>
        [field: SerializeField] public int HeroShardReward { get; private set; }

        /// <summary>
        /// Optional tag that must match the context string passed to QuestEngine.RecordProgress.
        /// E.g., ObjectiveType=UpgradeBuilding with ContextTag="military" only counts Military buildings.
        /// Empty = any context counts.
        /// </summary>
        [field: SerializeField] public string ContextTag { get; private set; } = "";
    }
}
