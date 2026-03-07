using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Empire;
using AshenThrone.Combat;
using AshenThrone.Alliance;

namespace AshenThrone.Economy
{
    // ---------------------------------------------------------------------------
    // Quest runtime state
    // ---------------------------------------------------------------------------

    /// <summary>Runtime progress for a single quest instance.</summary>
    public class QuestProgress
    {
        public QuestDefinition Definition { get; }

        /// <summary>Current progress count toward RequiredCount.</summary>
        public int CurrentCount { get; private set; }

        /// <summary>Whether the quest is completed (count reached RequiredCount).</summary>
        public bool IsCompleted => CurrentCount >= Definition.RequiredCount;

        /// <summary>Whether the completion reward has been claimed.</summary>
        public bool IsClaimed { get; private set; }

        public QuestProgress(QuestDefinition definition, int savedCount = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentCount = Mathf.Max(0, savedCount);
        }

        /// <summary>
        /// Add progress. Returns true if this update caused the quest to complete.
        /// </summary>
        public bool AddProgress(int amount)
        {
            if (IsCompleted || amount <= 0) return false;
            int before = CurrentCount;
            CurrentCount = Mathf.Min(CurrentCount + amount, Definition.RequiredCount);
            return !IsCompletedAt(before) && IsCompleted;
        }

        /// <summary>
        /// Mark the quest reward as claimed. Returns false if already claimed or not completed.
        /// </summary>
        public bool Claim()
        {
            if (!IsCompleted || IsClaimed) return false;
            IsClaimed = true;
            return true;
        }

        /// <summary>Reset for a new cadence period.</summary>
        public void Reset()
        {
            CurrentCount = 0;
            IsClaimed = false;
        }

        private bool IsCompletedAt(int count) => count >= Definition.RequiredCount;
    }

    // ---------------------------------------------------------------------------
    // QuestEngine
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages all active daily, weekly, and one-time quests.
    ///
    /// Architecture:
    /// - Quest definitions are ScriptableObjects loaded via Resources at startup.
    /// - Progress is tracked in-memory and flushed to PlayFab on claim or on app pause.
    /// - Daily quests reset each UTC midnight; weekly quests reset each Monday UTC.
    /// - RecordProgress is the single entry point for all quest objective tracking —
    ///   other systems call it via EventBus, not directly (check #12).
    /// </summary>
    public class QuestEngine : MonoBehaviour
    {
        // questId → progress
        private readonly Dictionary<string, QuestProgress> _activeQuests = new();

        private BattlePassManager _battlePassManager;
        private ResourceManager _resourceManager;

        // UTC dates used for reset-window tracking
        private DateTime _lastDailyResetUtc;
        private DateTime _lastWeeklyResetUtc;

        // EventBus subscriptions to auto-track quest progress
        private EventSubscription _combatEndedSub;
        private EventSubscription _buildingUpgradedSub;
        private EventSubscription _researchCompletedSub;
        private EventSubscription _rallyJoinedSub;
        private EventSubscription _territoryCapturedSub;
        private EventSubscription _chatMessageSub;
        private EventSubscription _pvpResultSub;

        private void Awake()
        {
            ServiceLocator.Register<QuestEngine>(this);
        }

        private void Start()
        {
            _battlePassManager = ServiceLocator.Get<BattlePassManager>();
            _resourceManager   = ServiceLocator.Get<ResourceManager>();
        }

        private void OnEnable()
        {
            _combatEndedSub       = EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
            _buildingUpgradedSub  = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildingUpgraded);
            _researchCompletedSub = EventBus.Subscribe<ResearchCompletedEvent>(OnResearchCompleted);
            _rallyJoinedSub       = EventBus.Subscribe<RallyMemberJoinedEvent>(OnRallyJoined);
            _territoryCapturedSub = EventBus.Subscribe<TerritoryCapturedEvent>(OnTerritoryCaptured);
            _chatMessageSub       = EventBus.Subscribe<ChatMessageReceivedEvent>(OnChatMessage);
            _pvpResultSub         = EventBus.Subscribe<PvpReplayReceivedEvent>(OnPvpResult);
        }

        private void OnDisable()
        {
            _combatEndedSub?.Dispose();
            _buildingUpgradedSub?.Dispose();
            _researchCompletedSub?.Dispose();
            _rallyJoinedSub?.Dispose();
            _territoryCapturedSub?.Dispose();
            _chatMessageSub?.Dispose();
            _pvpResultSub?.Dispose();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<QuestEngine>();
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------

        /// <summary>
        /// Load quest definitions and restore saved progress.
        /// </summary>
        public void Initialize(IEnumerable<QuestDefinition> definitions, QuestSaveData save)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            _activeQuests.Clear();

            // Restore reset timestamps from save
            _lastDailyResetUtc  = save?.LastDailyResetUtc  ?? GetLastMidnightUtc();
            _lastWeeklyResetUtc = save?.LastWeeklyResetUtc ?? GetLastMondayUtc();

            // Check if reset is needed before restoring progress
            bool needsDailyReset  = DateTime.UtcNow >= _lastDailyResetUtc.AddDays(1);
            bool needsWeeklyReset = DateTime.UtcNow >= _lastWeeklyResetUtc.AddDays(7);

            foreach (var def in definitions)
            {
                if (def == null) continue;

                int savedCount = 0;
                bool savedClaimed = false;
                save?.QuestProgress?.TryGetValue(def.QuestId, out savedCount);
                save?.QuestClaimed?.TryGetValue(def.QuestId, out savedClaimed);

                // Wipe progress if the reset window has passed
                bool shouldReset = (def.Cadence == QuestCadence.Daily && needsDailyReset)
                                || (def.Cadence == QuestCadence.Weekly && needsWeeklyReset);

                var progress = new QuestProgress(def, shouldReset ? 0 : savedCount);
                if (!shouldReset && savedClaimed) progress.Claim();

                _activeQuests[def.QuestId] = progress;
            }

            if (needsDailyReset)  _lastDailyResetUtc  = GetLastMidnightUtc();
            if (needsWeeklyReset) _lastWeeklyResetUtc = GetLastMondayUtc();
        }

        // -------------------------------------------------------------------
        // Progress recording
        // -------------------------------------------------------------------

        /// <summary>
        /// Record progress toward an objective type. Call from any system when the
        /// relevant action occurs.
        ///
        /// contextTag: optional filter (e.g., building category, hero faction).
        /// Pass string.Empty or null to match quests with no context requirement.
        /// </summary>
        public void RecordProgress(QuestObjectiveType objectiveType, int amount = 1, string contextTag = null)
        {
            if (amount <= 0) return;

            foreach (var kvp in _activeQuests)
            {
                var progress = kvp.Value;
                if (progress.IsCompleted) continue;
                if (progress.Definition.ObjectiveType != objectiveType) continue;

                // Context tag filter: quest tag must match (or quest has no tag requirement)
                bool tagMatches = string.IsNullOrEmpty(progress.Definition.ContextTag)
                               || string.Equals(progress.Definition.ContextTag, contextTag,
                                                StringComparison.OrdinalIgnoreCase);
                if (!tagMatches) continue;

                bool justCompleted = progress.AddProgress(amount);
                if (justCompleted)
                {
                    EventBus.Publish(new QuestCompletedEvent(progress.Definition.QuestId, progress.Definition.DisplayName));
                }
            }
        }

        /// <summary>
        /// Claim the reward for a completed quest. Returns true if successful.
        /// Grants resources to ResourceManager and BP points to BattlePassManager.
        /// </summary>
        public bool ClaimReward(string questId)
        {
            if (!_activeQuests.TryGetValue(questId, out var progress)) return false;
            if (!progress.Claim()) return false;

            var def = progress.Definition;

            // Grant resources (check #49 — these are local grants; server validates on save)
            if (_resourceManager != null)
            {
                if (def.StoneReward  > 0) _resourceManager.AddResource(ResourceType.Stone,  def.StoneReward);
                if (def.IronReward   > 0) _resourceManager.AddResource(ResourceType.Iron,   def.IronReward);
                if (def.GrainReward  > 0) _resourceManager.AddResource(ResourceType.Grain,  def.GrainReward);
                if (def.ArcaneReward > 0) _resourceManager.AddResource(ResourceType.ArcaneEssence, def.ArcaneReward);
            }

            // Grant Battle Pass points
            if (_battlePassManager != null && def.BattlePassPoints > 0)
                _battlePassManager.AddPoints(def.BattlePassPoints, $"quest:{questId}");

            EventBus.Publish(new QuestRewardClaimedEvent(questId, def.BattlePassPoints));
            return true;
        }

        // -------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------

        /// <summary>Returns all quest progress entries matching the given cadence.</summary>
        public List<QuestProgress> GetQuestsByCategory(QuestCadence cadence)
        {
            var result = new List<QuestProgress>();
            foreach (var kvp in _activeQuests)
            {
                if (kvp.Value.Definition.Cadence == cadence)
                    result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>Returns progress for a specific quest ID, or null.</summary>
        public QuestProgress GetProgress(string questId)
        {
            return _activeQuests.TryGetValue(questId, out var p) ? p : null;
        }

        /// <summary>Number of completed-but-unclaimed quests (for notification badge).</summary>
        public int GetUnclaimedCount()
        {
            int count = 0;
            foreach (var kvp in _activeQuests)
            {
                if (kvp.Value.IsCompleted && !kvp.Value.IsClaimed) count++;
            }
            return count;
        }

        /// <summary>Build save data for persistence.</summary>
        public QuestSaveData BuildSaveData()
        {
            var save = new QuestSaveData
            {
                LastDailyResetUtc  = _lastDailyResetUtc,
                LastWeeklyResetUtc = _lastWeeklyResetUtc
            };
            foreach (var kvp in _activeQuests)
            {
                save.QuestProgress[kvp.Key] = kvp.Value.CurrentCount;
                save.QuestClaimed[kvp.Key]  = kvp.Value.IsClaimed;
            }
            return save;
        }

        // -------------------------------------------------------------------
        // EventBus handlers — auto-route game events to quest progress
        // -------------------------------------------------------------------

        private void OnBattleEnded(BattleEndedEvent evt)
        {
            RecordProgress(QuestObjectiveType.CompletePveLevels);
            if (evt.Outcome == BattleOutcome.PlayerVictory)
                RecordProgress(QuestObjectiveType.WinCombatBattles);
        }

        private void OnBuildingUpgraded(BuildingUpgradeCompletedEvent evt)
        {
            RecordProgress(QuestObjectiveType.UpgradeBuilding);
        }

        private void OnResearchCompleted(ResearchCompletedEvent evt)
        {
            RecordProgress(QuestObjectiveType.CompleteResearchNode);
        }

        private void OnRallyJoined(RallyMemberJoinedEvent evt)
        {
            RecordProgress(QuestObjectiveType.JoinRally);
        }

        private void OnTerritoryCaptured(TerritoryCapturedEvent evt)
        {
            RecordProgress(QuestObjectiveType.CaptureTerritory);
        }

        private void OnChatMessage(ChatMessageReceivedEvent evt)
        {
            // Only count messages sent by the local player (not received messages)
            // The local player's own messages are also delivered back via ReceiveMessage.
            // Check via PlayerMember tracking — skipped here as AllianceManager lookup
            // would create a cross-system dependency. In production, a separate
            // ChatMessageSentEvent from the send confirmation callback is cleaner.
        }

        private void OnPvpResult(PvpReplayReceivedEvent evt)
        {
            if (evt.Outcome == "PlayerVictory")
                RecordProgress(QuestObjectiveType.WinPvpBattles);
        }

        // -------------------------------------------------------------------
        // Static helpers — pure, testable
        // -------------------------------------------------------------------

        /// <summary>Returns the most recent midnight in UTC.</summary>
        public static DateTime GetLastMidnightUtc() => DateTime.UtcNow.Date;

        /// <summary>Returns the most recent Monday midnight in UTC.</summary>
        public static DateTime GetLastMondayUtc()
        {
            DateTime today = DateTime.UtcNow.Date;
            int daysBack = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return today.AddDays(-daysBack);
        }
    }

    // ---------------------------------------------------------------------------
    // Save data
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class QuestSaveData
    {
        public DateTime LastDailyResetUtc;
        public DateTime LastWeeklyResetUtc;
        /// <summary>questId → current progress count.</summary>
        public Dictionary<string, int>  QuestProgress = new();
        /// <summary>questId → whether reward has been claimed.</summary>
        public Dictionary<string, bool> QuestClaimed  = new();
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct QuestCompletedEvent
    {
        public readonly string QuestId;
        public readonly string DisplayName;
        public QuestCompletedEvent(string id, string name) { QuestId = id; DisplayName = name; }
    }

    public readonly struct QuestRewardClaimedEvent
    {
        public readonly string QuestId;
        public readonly int BattlePassPointsGranted;
        public QuestRewardClaimedEvent(string id, int pts) { QuestId = id; BattlePassPointsGranted = pts; }
    }
}
