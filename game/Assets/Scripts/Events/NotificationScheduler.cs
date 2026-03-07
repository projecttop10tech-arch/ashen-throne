using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Events
{
    // ---------------------------------------------------------------------------
    // Notification types
    // ---------------------------------------------------------------------------

    /// <summary>Category of in-game notification to fire.</summary>
    public enum NotificationType
    {
        EventStarting,      // War window / game event about to open
        EventEnding,        // Game event ending soon
        BuildComplete,      // Building upgrade timer finished
        ResearchComplete,   // Research node complete
        OfflineIncomeFull,  // Resource vault at capacity
        AllianceRally,      // Rally started by alliance member
        BossSpawned,        // World boss Dragon Siege spawned
        BattlePassTier,     // New battle pass tier unlocked
        QuestCompleted      // Quest ready to claim
    }

    /// <summary>A scheduled local push notification.</summary>
    public class ScheduledNotification
    {
        /// <summary>Unique ID for deduplication and cancellation.</summary>
        public string NotificationId { get; }

        public NotificationType Type { get; }

        /// <summary>Display title for the push notification.</summary>
        public string Title { get; }

        /// <summary>Display body text.</summary>
        public string Body { get; }

        /// <summary>UTC time at which the notification should fire.</summary>
        public DateTime FireAtUtc { get; }

        /// <summary>Whether the notification has been dispatched to the OS.</summary>
        public bool IsDispatched { get; set; }

        public ScheduledNotification(string id, NotificationType type,
            string title, string body, DateTime fireAtUtc)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("NotificationId cannot be empty.");

            NotificationId = id;
            Type = type;
            Title = title;
            Body = body;
            FireAtUtc = fireAtUtc;
        }
    }

    // ---------------------------------------------------------------------------
    // NotificationScheduler
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Schedules and manages local push notifications for time-sensitive game events.
    ///
    /// Architecture:
    /// - Uses Unity Mobile Notifications package (stubbed — install com.unity.mobile.notifications).
    /// - All notifications are local (no server push for time-sensitive timers).
    /// - Deduplication: same NotificationId is never scheduled twice.
    /// - All notifications are cancelled on app foreground (user is already playing).
    /// - EventBus-driven: auto-schedules notifications based on game events.
    /// - COPPA compliance: no personal data in notification payloads (check #46).
    /// </summary>
    public class NotificationScheduler : MonoBehaviour
    {
        /// <summary>
        /// How many minutes before an event starts to fire the "starting soon" notification.
        /// </summary>
        private const int EventStartWarningMinutes = 30;

        /// <summary>
        /// How many minutes before an event ends to fire the "ending soon" notification.
        /// </summary>
        private const int EventEndWarningMinutes = 60;

        private readonly Dictionary<string, ScheduledNotification> _scheduled = new();

        private EventSubscription _eventStartedSub;
        private EventSubscription _eventEndedSub;
        private EventSubscription _buildCompletedSub;
        private EventSubscription _researchCompletedSub;
        private EventSubscription _bossSpawnedSub;
        private EventSubscription _battlePassTierSub;
        private EventSubscription _questCompletedSub;
        private EventSubscription _rallyStartedSub;

        private void Awake()
        {
            ServiceLocator.Register<NotificationScheduler>(this);
        }

        private void OnEnable()
        {
            _eventStartedSub     = EventBus.Subscribe<GameEventStartedEvent>(OnEventStarted);
            _eventEndedSub       = EventBus.Subscribe<GameEventEndedEvent>(OnEventEnded);
            _buildCompletedSub   = EventBus.Subscribe<Empire.BuildingUpgradeCompletedEvent>(OnBuildCompleted);
            _researchCompletedSub = EventBus.Subscribe<Empire.ResearchCompletedEvent>(OnResearchCompleted);
            _bossSpawnedSub      = EventBus.Subscribe<WorldBossSpawnedEvent>(OnBossSpawned);
            _battlePassTierSub   = EventBus.Subscribe<Economy.BattlePassTierAdvancedEvent>(OnBattlePassTier);
            _questCompletedSub   = EventBus.Subscribe<Economy.QuestCompletedEvent>(OnQuestCompleted);
            _rallyStartedSub     = EventBus.Subscribe<Alliance.RallyStartedEvent>(OnRallyStarted);
        }

        private void OnDisable()
        {
            _eventStartedSub?.Dispose();
            _eventEndedSub?.Dispose();
            _buildCompletedSub?.Dispose();
            _researchCompletedSub?.Dispose();
            _bossSpawnedSub?.Dispose();
            _battlePassTierSub?.Dispose();
            _questCompletedSub?.Dispose();
            _rallyStartedSub?.Dispose();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<NotificationScheduler>();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Schedule a local push notification.
        /// Duplicate IDs are silently ignored (idempotent).
        /// </summary>
        public bool ScheduleNotification(ScheduledNotification notification)
        {
            if (notification == null) return false;
            if (_scheduled.ContainsKey(notification.NotificationId)) return false;
            if (notification.FireAtUtc <= DateTime.UtcNow) return false; // Past notifications not scheduled

            _scheduled[notification.NotificationId] = notification;
            DispatchToOsStub(notification);
            notification.IsDispatched = true;
            return true;
        }

        /// <summary>
        /// Cancel a previously scheduled notification.
        /// </summary>
        public bool CancelNotification(string notificationId)
        {
            if (!_scheduled.TryGetValue(notificationId, out var notification)) return false;
            CancelFromOsStub(notificationId);
            _scheduled.Remove(notificationId);
            return true;
        }

        /// <summary>
        /// Cancel all pending notifications (call when app comes to foreground).
        /// Prevents stale notifications from showing while player is already in-game.
        /// </summary>
        public void CancelAll()
        {
            foreach (var kvp in _scheduled)
                CancelFromOsStub(kvp.Key);
            _scheduled.Clear();
        }

        /// <summary>Returns true if a notification with the given ID is scheduled.</summary>
        public bool IsScheduled(string notificationId) => _scheduled.ContainsKey(notificationId);

        /// <summary>Total number of currently scheduled notifications.</summary>
        public int ScheduledCount => _scheduled.Count;

        // -------------------------------------------------------------------
        // EventBus handlers
        // -------------------------------------------------------------------

        private void OnEventStarted(GameEventStartedEvent evt)
        {
            // Schedule "ending soon" notification EventEndWarningMinutes before end
            // For simplicity, we use a fixed duration offset from now
            var fireAt = DateTime.UtcNow.AddHours(24 * 7).AddMinutes(-EventEndWarningMinutes);
            ScheduleNotification(new ScheduledNotification(
                $"event_ending_{evt.EventId}",
                NotificationType.EventEnding,
                "Event Ending Soon",
                $"{evt.Name} ends in {EventEndWarningMinutes} minutes!",
                fireAt));
        }

        private void OnEventEnded(GameEventEndedEvent evt)
        {
            CancelNotification($"event_ending_{evt.EventId}");
        }

        private void OnBuildCompleted(Empire.BuildingUpgradeCompletedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"build_done_{evt.PlacedId}_{DateTime.UtcNow.Ticks}",
                NotificationType.BuildComplete,
                "Build Complete!",
                "Your building has finished upgrading.",
                DateTime.UtcNow)); // Fires immediately on next OS check
        }

        private void OnResearchCompleted(Empire.ResearchCompletedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"research_done_{evt.NodeId}_{DateTime.UtcNow.Ticks}",
                NotificationType.ResearchComplete,
                "Research Complete!",
                $"Research '{evt.NodeId}' has been completed.",
                DateTime.UtcNow));
        }

        private void OnBossSpawned(WorldBossSpawnedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"boss_spawned_{evt.BossId}",
                NotificationType.BossSpawned,
                "Dragon Siege!",
                "The dragon has awakened. Rally your forces!",
                DateTime.UtcNow.AddSeconds(5)));
        }

        private void OnBattlePassTier(Economy.BattlePassTierAdvancedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"bp_tier_{evt.NewTier}",
                NotificationType.BattlePassTier,
                "Battle Pass Tier Unlocked!",
                $"You've reached Battle Pass tier {evt.NewTier}. Claim your reward!",
                DateTime.UtcNow.AddSeconds(2)));
        }

        private void OnQuestCompleted(Economy.QuestCompletedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"quest_done_{evt.QuestId}",
                NotificationType.QuestCompleted,
                "Quest Complete!",
                $"'{evt.DisplayName}' is ready to claim.",
                DateTime.UtcNow.AddSeconds(2)));
        }

        private void OnRallyStarted(Alliance.RallyStartedEvent evt)
        {
            ScheduleNotification(new ScheduledNotification(
                $"rally_{evt.RallyId}",
                NotificationType.AllianceRally,
                "Rally Started!",
                "Your alliance has started a rally. Join now!",
                DateTime.UtcNow.AddSeconds(10)));
        }

        // -------------------------------------------------------------------
        // OS dispatch stubs
        // -------------------------------------------------------------------

        private void DispatchToOsStub(ScheduledNotification notification)
        {
            // In production: call Unity Mobile Notifications API
            // Android: AndroidNotificationCenter.SendNotificationWithExplicitID(...)
            // iOS:     iOSNotificationCenter.ScheduleNotification(...)
            Debug.Log($"[NotificationScheduler] Scheduled: [{notification.Type}] '{notification.Title}' at {notification.FireAtUtc:O}");
        }

        private void CancelFromOsStub(string notificationId)
        {
            // In production: call platform cancel API with the notification ID
            Debug.Log($"[NotificationScheduler] Cancelled notification: {notificationId}");
        }
    }
}
