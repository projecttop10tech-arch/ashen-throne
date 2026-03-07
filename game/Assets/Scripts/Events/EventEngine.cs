using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Events
{
    /// <summary>
    /// Pluggable event engine. Events are defined as EventDefinition ScriptableObjects.
    /// Handles scheduling, activation, objective tracking, and reward distribution.
    /// Active events are synced with PlayFab for cross-session persistence.
    /// </summary>
    public class EventEngine : MonoBehaviour
    {
        [SerializeField] private List<EventDefinition> allEventDefinitions;

        private readonly List<ActiveGameEvent> _activeEvents = new();
        public IReadOnlyList<ActiveGameEvent> ActiveEvents => _activeEvents;

        public event Action<EventDefinition> OnEventStarted;
        public event Action<string> OnEventEnded;                     // eventId
        public event Action<string, float> OnObjectiveProgress;       // eventId, 0–1 progress

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
            CheckEventSchedules();
        }

        /// <summary>
        /// Load active event state from server. Called after authentication.
        /// </summary>
        public void LoadActiveEvents(List<EventSaveEntry> saves)
        {
            _activeEvents.Clear();
            foreach (var save in saves)
            {
                EventDefinition def = allEventDefinitions.Find(e => e.eventId == save.EventId);
                if (def == null) continue;
                _activeEvents.Add(new ActiveGameEvent(def, save));
            }
        }

        /// <summary>
        /// Report player progress toward an event objective.
        /// </summary>
        public void ReportProgress(EventObjectiveType objectiveType, int amount)
        {
            foreach (var active in _activeEvents)
            {
                if (!active.IsActive || active.Definition.objectiveType != objectiveType) continue;
                active.CurrentProgress += amount;
                float normalizedProgress = Mathf.Clamp01((float)active.CurrentProgress / active.Definition.objectiveTarget);
                OnObjectiveProgress?.Invoke(active.Definition.eventId, normalizedProgress);
                EventBus.Publish(new EventProgressUpdatedEvent(active.Definition.eventId, active.CurrentProgress, active.Definition.objectiveTarget));
            }
        }

        private void CheckEventSchedules()
        {
            DateTime now = DateTime.UtcNow;
            foreach (var def in allEventDefinitions)
            {
                bool shouldBeActive = now >= def.startTime && now < def.endTime;
                bool isCurrentlyActive = _activeEvents.Exists(a => a.Definition.eventId == def.eventId);

                if (shouldBeActive && !isCurrentlyActive)
                    ActivateEvent(def);
                else if (!shouldBeActive && isCurrentlyActive)
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
            _activeEvents.RemoveAll(a => a.Definition.eventId == eventId);
            OnEventEnded?.Invoke(eventId);
            EventBus.Publish(new GameEventEndedEvent(eventId));
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

        [Header("Schedule")]
        public DateTime startTime;
        public DateTime endTime;

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
        public int CurrentProgress { get; set; }
        public bool IsActive => DateTime.UtcNow >= Definition.startTime && DateTime.UtcNow < Definition.endTime;

        public ActiveGameEvent(EventDefinition def, EventSaveEntry save)
        {
            Definition = def;
            CurrentProgress = save?.Progress ?? 0;
        }
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
        public int ProgressThreshold; // Progress required to earn this reward
        public int ResourceAmount;
        public string ItemId;
    }

    public enum EventScope { Solo, Alliance, ServerWide }

    public enum EventObjectiveType
    {
        DamageDealt,        // Dragon Siege boss
        TerritoryCaptures,  // Alliance Tournament
        ResourcesCollected, // Harvest Crisis
        ShardsEarned,       // Shard Hunt
        DungeonFloorsCleared // Void Rift
    }

    // --- Events ---
    public readonly struct GameEventStartedEvent { public readonly string EventId; public readonly string Name; public GameEventStartedEvent(string id, string n) { EventId = id; Name = n; } }
    public readonly struct GameEventEndedEvent { public readonly string EventId; public GameEventEndedEvent(string id) { EventId = id; } }
    public readonly struct EventProgressUpdatedEvent { public readonly string EventId; public readonly int Current; public readonly int Target; public EventProgressUpdatedEvent(string id, int c, int t) { EventId = id; Current = c; Target = t; } }
}
