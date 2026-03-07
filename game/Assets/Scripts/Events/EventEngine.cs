using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Events
{
    /// <summary>
    /// Pluggable event engine. Events are defined as EventDefinition ScriptableObjects.
    /// Handles scheduling, activation, objective tracking, and reward distribution.
    /// Active events are synced with PlayFab for cross-session persistence.
    ///
    /// Performance notes:
    /// - Schedule check is throttled to once per ScheduleCheckIntervalSeconds (not every frame).
    /// - No LINQ in hot paths — explicit loops only (check #19).
    /// </summary>
    public class EventEngine : MonoBehaviour
    {
        /// <summary>How often (in seconds) to check event start/end schedules.</summary>
        private const float ScheduleCheckIntervalSeconds = 10f;

        [SerializeField] private List<EventDefinition> allEventDefinitions;

        private readonly List<ActiveGameEvent> _activeEvents = new();
        public IReadOnlyList<ActiveGameEvent> ActiveEvents => _activeEvents;

        public event Action<EventDefinition> OnEventStarted;
        public event Action<string> OnEventEnded;                     // eventId
        public event Action<string, float> OnObjectiveProgress;       // eventId, 0–1 progress

        private float _scheduleCheckTimer;

        private void Awake()
        {
            ServiceLocator.Register<EventEngine>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<EventEngine>();
        }

        private void Update()
        {
            // Throttled schedule check: not every frame (check #20 / #19)
            _scheduleCheckTimer += Time.deltaTime;
            if (_scheduleCheckTimer >= ScheduleCheckIntervalSeconds)
            {
                _scheduleCheckTimer = 0f;
                CheckEventSchedules();
            }
        }

        /// <summary>
        /// Load active event state from server. Called after authentication.
        /// </summary>
        public void LoadActiveEvents(List<EventSaveEntry> saves)
        {
            _activeEvents.Clear();
            if (saves == null) return;
            foreach (var save in saves)
            {
                if (save == null) continue;
                EventDefinition def = FindDefinitionById(save.EventId);
                if (def == null) continue;
                _activeEvents.Add(new ActiveGameEvent(def, save));
            }
        }

        /// <summary>
        /// Report player progress toward an event objective.
        /// </summary>
        public void ReportProgress(EventObjectiveType objectiveType, int amount)
        {
            if (amount <= 0) return;
            foreach (var active in _activeEvents)
            {
                if (!active.IsActive || active.Definition.objectiveType != objectiveType) continue;
                active.AddProgress(amount);
                float normalizedProgress = active.Definition.objectiveTarget > 0
                    ? Mathf.Clamp01((float)active.CurrentProgress / active.Definition.objectiveTarget)
                    : 1f;
                OnObjectiveProgress?.Invoke(active.Definition.eventId, normalizedProgress);
                EventBus.Publish(new EventProgressUpdatedEvent(
                    active.Definition.eventId, active.CurrentProgress, active.Definition.objectiveTarget));
            }
        }

        /// <summary>Returns the active event with the given ID, or null.</summary>
        public ActiveGameEvent GetActiveEvent(string eventId)
        {
            foreach (var active in _activeEvents)
            {
                if (active.Definition.eventId == eventId) return active;
            }
            return null;
        }

        private void CheckEventSchedules()
        {
            if (allEventDefinitions == null) return;
            DateTime now = DateTime.UtcNow;
            foreach (var def in allEventDefinitions)
            {
                if (def == null) continue;
                // Parse ISO-8601 strings to UTC DateTime (avoids UnityEngine.DateTime serialization issues)
                bool parsedStart = DateTime.TryParse(def.startTimeIso, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime startTime);
                bool parsedEnd   = DateTime.TryParse(def.endTimeIso, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime endTime);

                if (!parsedStart || !parsedEnd) continue;

                bool shouldBeActive = now >= startTime && now < endTime;
                bool isActive = IsEventActive(def.eventId);

                if (shouldBeActive && !isActive)
                    ActivateEvent(def);
                else if (!shouldBeActive && isActive)
                    DeactivateEvent(def.eventId);
            }
        }

        private void ActivateEvent(EventDefinition def)
        {
            var active = new ActiveGameEvent(def, null);
            _activeEvents.Add(active);
            OnEventStarted?.Invoke(def);
            EventBus.Publish(new GameEventStartedEvent(def.eventId, def.displayName));
        }

        private void DeactivateEvent(string eventId)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                if (_activeEvents[i].Definition.eventId == eventId)
                {
                    _activeEvents.RemoveAt(i);
                    break;
                }
            }
            OnEventEnded?.Invoke(eventId);
            EventBus.Publish(new GameEventEndedEvent(eventId));
        }

        private bool IsEventActive(string eventId)
        {
            foreach (var active in _activeEvents)
            {
                if (active.Definition.eventId == eventId) return true;
            }
            return false;
        }

        private EventDefinition FindDefinitionById(string eventId)
        {
            if (allEventDefinitions == null) return null;
            foreach (var def in allEventDefinitions)
            {
                if (def != null && def.eventId == eventId) return def;
            }
            return null;
        }
    }

    [CreateAssetMenu(fileName = "Event_", menuName = "AshenThrone/Event Definition", order = 6)]
    public class EventDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string eventId;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public EventScope scope;
        public Sprite bannerImage;

        [Header("Schedule (ISO-8601 UTC strings — avoids DateTime Inspector issues)")]
        /// <summary>
        /// Start time as ISO-8601 UTC string (e.g., "2026-04-01T18:00:00Z").
        /// Using string avoids Unity's broken DateTime Inspector serialization.
        /// Parse at runtime via DateTime.TryParse with RoundtripKind.
        /// </summary>
        public string startTimeIso;
        /// <summary>End time as ISO-8601 UTC string.</summary>
        public string endTimeIso;

        [Header("Objective")]
        public EventObjectiveType objectiveType;
        public int objectiveTarget;

        [Header("Rewards")]
        public List<EventReward> milestoneRewards;
        public List<EventReward> completionRewards;
    }

    public class ActiveGameEvent
    {
        public EventDefinition Definition { get; }
        public int CurrentProgress { get; private set; }

        public bool IsActive
        {
            get
            {
                if (!DateTime.TryParse(Definition.startTimeIso, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime start)) return false;
                if (!DateTime.TryParse(Definition.endTimeIso, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime end)) return false;
                DateTime now = DateTime.UtcNow;
                return now >= start && now < end;
            }
        }

        public ActiveGameEvent(EventDefinition def, EventSaveEntry save)
        {
            Definition = def ?? throw new ArgumentNullException(nameof(def));
            CurrentProgress = save?.Progress ?? 0;
        }

        /// <summary>Add progress, clamped to objectiveTarget.</summary>
        public void AddProgress(int amount)
        {
            if (amount <= 0) return;
            CurrentProgress = Mathf.Min(CurrentProgress + amount, Definition.objectiveTarget);
        }

        /// <summary>Returns a normalized completion ratio [0, 1].</summary>
        public float CompletionRatio =>
            Definition.objectiveTarget > 0
                ? Mathf.Clamp01((float)CurrentProgress / Definition.objectiveTarget)
                : 1f;
    }

    [System.Serializable]
    public class EventSaveEntry
    {
        public string EventId;
        public int Progress;
    }

    [System.Serializable]
    public class EventReward
    {
        public string RewardId;
        /// <summary>Progress value at which this milestone reward is earned.</summary>
        public int ProgressThreshold;
        public int ResourceAmount;
        public string ItemId;
    }

    public enum EventScope { Solo, Alliance, ServerWide }

    public enum EventObjectiveType
    {
        DamageDealt,          // Dragon Siege world boss
        TerritoryCaptures,    // Alliance Tournament
        ResourcesCollected,   // Harvest Crisis
        ShardsEarned,         // Shard Hunt
        DungeonFloorsCleared  // Void Rift
    }

    // --- Events ---
    public readonly struct GameEventStartedEvent { public readonly string EventId; public readonly string Name; public GameEventStartedEvent(string id, string n) { EventId = id; Name = n; } }
    public readonly struct GameEventEndedEvent { public readonly string EventId; public GameEventEndedEvent(string id) { EventId = id; } }
    public readonly struct EventProgressUpdatedEvent { public readonly string EventId; public readonly int Current; public readonly int Target; public EventProgressUpdatedEvent(string id, int c, int t) { EventId = id; Current = c; Target = t; } }
}
