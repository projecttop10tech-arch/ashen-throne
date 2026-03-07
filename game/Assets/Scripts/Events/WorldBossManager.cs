using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Events
{
    // ---------------------------------------------------------------------------
    // World Boss data
    // ---------------------------------------------------------------------------

    /// <summary>
    /// ScriptableObject defining a world boss encounter (Dragon Siege event).
    /// All balance numbers live here — never hardcoded in WorldBossManager (check #8).
    /// </summary>
    [CreateAssetMenu(fileName = "WorldBoss_", menuName = "AshenThrone/World Boss Definition")]
    public class WorldBossDefinition : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>Unique ID matching the EventDefinition.eventId for Dragon Siege.</summary>
        [field: SerializeField] public string BossId { get; private set; }
        [field: SerializeField] public string BossName { get; private set; }
        [field: SerializeField] public string BossLore { get; private set; }

        [Header("Stats")]
        /// <summary>Total HP across the server-wide boss encounter.</summary>
        [field: SerializeField] public long TotalHp { get; private set; } = 50_000_000L;

        /// <summary>Minimum damage a single player attack can deal (floor for participation reward).</summary>
        [field: SerializeField] public int MinDamagePerAttack { get; private set; } = 1000;

        /// <summary>Maximum attacks per player per boss spawn.</summary>
        [field: SerializeField] public int MaxAttacksPerPlayer { get; private set; } = 5;

        [Header("Rewards")]
        /// <summary>Server-wide HP % thresholds at which milestone rewards drop (e.g., 0.75, 0.5, 0.25).</summary>
        [field: SerializeField] public float[] MilestoneHpPercents { get; private set; } = { 0.75f, 0.5f, 0.25f };

        /// <summary>Hero shard reward amount per participant milestone.</summary>
        [field: SerializeField] public int ParticipationShardReward { get; private set; } = 20;

        /// <summary>Top-damage leaderboard rewards (place → hero shard count).</summary>
        [field: SerializeField] public int[] LeaderboardShardRewards { get; private set; } = { 200, 150, 100, 80, 60 };
    }

    // ---------------------------------------------------------------------------
    // WorldBossManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages the Dragon Siege world boss event.
    ///
    /// Architecture:
    /// - Boss HP is server-authoritative (managed in PlayFab).
    /// - Client submits attack results; server validates damage and updates shared HP.
    /// - `ReceiveServerHpUpdate` is the single point where boss HP changes client-side.
    /// - Milestone rewards are unlocked server-side; client receives notifications via events.
    /// - All attack requests are validated against MaxAttacksPerPlayer on server.
    /// </summary>
    public class WorldBossManager : MonoBehaviour
    {
        [SerializeField] private WorldBossDefinition _definition;

        /// <summary>Server-reported current HP. Client never sets this directly.</summary>
        public long CurrentHp { get; private set; }

        /// <summary>Whether the boss is currently alive (HP > 0) and event is active.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Number of attacks the local player has used this boss spawn.</summary>
        public int LocalAttacksUsed { get; private set; }

        /// <summary>Total damage dealt by the local player this boss spawn.</summary>
        public long LocalDamageDealt { get; private set; }

        // Indices of milestones already triggered (server-confirmed)
        private readonly HashSet<int> _triggeredMilestones = new();

        private EventEngine _eventEngine;

        private void Awake()
        {
            ServiceLocator.Register<WorldBossManager>(this);
        }

        private void Start()
        {
            _eventEngine = ServiceLocator.Get<EventEngine>();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<WorldBossManager>();
        }

        // -------------------------------------------------------------------
        // Boss state management (server-driven)
        // -------------------------------------------------------------------

        /// <summary>
        /// Initialize the boss for a new spawn. Called when the Dragon Siege event opens.
        /// </summary>
        public void InitializeBossSpawn()
        {
            if (_definition == null)
            {
                Debug.LogError("[WorldBossManager] WorldBossDefinition not assigned.", this);
                return;
            }
            CurrentHp = _definition.TotalHp;
            IsAlive = true;
            LocalAttacksUsed = 0;
            LocalDamageDealt = 0;
            _triggeredMilestones.Clear();
            EventBus.Publish(new WorldBossSpawnedEvent(_definition.BossId, _definition.TotalHp));
        }

        /// <summary>
        /// Apply a server-confirmed HP update. Checks milestones and defeat condition.
        /// Called on main thread from PlayFab callback (check #9).
        /// </summary>
        public void ReceiveServerHpUpdate(long newHp)
        {
            if (_definition == null) return;
            long previousHp = CurrentHp;
            CurrentHp = Math.Max(0L, Math.Min(newHp, _definition.TotalHp));

            if (IsAlive && CurrentHp <= 0)
            {
                IsAlive = false;
                EventBus.Publish(new WorldBossDefeatedEvent(_definition.BossId));
            }

            CheckMilestones(previousHp, CurrentHp);
            EventBus.Publish(new WorldBossHpUpdatedEvent(_definition.BossId, CurrentHp, _definition.TotalHp));
        }

        // -------------------------------------------------------------------
        // Local player attack
        // -------------------------------------------------------------------

        /// <summary>
        /// Submit an attack on the world boss.
        /// Returns false if the player is out of attacks.
        /// Damage is client-calculated for preview; server validates before applying.
        /// </summary>
        public bool RequestAttack(int attackPower)
        {
            if (_definition == null || !IsAlive) return false;
            if (LocalAttacksUsed >= _definition.MaxAttacksPerPlayer)
            {
                Debug.LogWarning($"[WorldBossManager] Player has used all {_definition.MaxAttacksPerPlayer} attacks.");
                return false;
            }

            // Client-side damage preview (server will validate and may adjust)
            int estimatedDamage = Mathf.Max(_definition.MinDamagePerAttack, attackPower);
            LocalAttacksUsed++;
            LocalDamageDealt += estimatedDamage;

            EventBus.Publish(new WorldBossAttackSubmittedEvent(
                _definition.BossId, estimatedDamage, LocalAttacksUsed));

            // ReportProgress to EventEngine for the DamageDealt objective
            _eventEngine?.ReportProgress(EventObjectiveType.DamageDealt, estimatedDamage);
            return true;
        }

        /// <summary>Remaining attacks available to the local player.</summary>
        public int AttacksRemaining =>
            _definition != null
                ? Mathf.Max(0, _definition.MaxAttacksPerPlayer - LocalAttacksUsed)
                : 0;

        /// <summary>Server-reported HP as a [0,1] ratio for progress bar display.</summary>
        public float HpRatio =>
            (_definition != null && _definition.TotalHp > 0)
                ? (float)CurrentHp / _definition.TotalHp
                : 0f;

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private void CheckMilestones(long previousHp, long currentHp)
        {
            if (_definition?.MilestoneHpPercents == null) return;

            for (int i = 0; i < _definition.MilestoneHpPercents.Length; i++)
            {
                if (_triggeredMilestones.Contains(i)) continue;
                float threshold = _definition.MilestoneHpPercents[i];
                long thresholdHp = (long)(_definition.TotalHp * threshold);
                if (currentHp <= thresholdHp && previousHp > thresholdHp)
                {
                    _triggeredMilestones.Add(i);
                    EventBus.Publish(new WorldBossMilestoneEvent(_definition.BossId, i, threshold));
                }
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct WorldBossSpawnedEvent
    {
        public readonly string BossId;
        public readonly long TotalHp;
        public WorldBossSpawnedEvent(string id, long hp) { BossId = id; TotalHp = hp; }
    }

    public readonly struct WorldBossHpUpdatedEvent
    {
        public readonly string BossId;
        public readonly long CurrentHp;
        public readonly long TotalHp;
        public WorldBossHpUpdatedEvent(string id, long current, long total)
        { BossId = id; CurrentHp = current; TotalHp = total; }
    }

    public readonly struct WorldBossMilestoneEvent
    {
        public readonly string BossId;
        public readonly int MilestoneIndex;
        public readonly float HpThreshold;
        public WorldBossMilestoneEvent(string id, int idx, float threshold)
        { BossId = id; MilestoneIndex = idx; HpThreshold = threshold; }
    }

    public readonly struct WorldBossDefeatedEvent
    {
        public readonly string BossId;
        public WorldBossDefeatedEvent(string id) { BossId = id; }
    }

    public readonly struct WorldBossAttackSubmittedEvent
    {
        public readonly string BossId;
        public readonly int EstimatedDamage;
        public readonly int AttacksUsed;
        public WorldBossAttackSubmittedEvent(string id, int dmg, int atk)
        { BossId = id; EstimatedDamage = dmg; AttacksUsed = atk; }
    }
}
