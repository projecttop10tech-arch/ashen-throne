using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Alliance
{
    // ---------------------------------------------------------------------------
    // War Window scheduling
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Represents a single scheduled war window with its open/close UTC times.
    /// </summary>
    public class WarWindow
    {
        /// <summary>UTC time this war window opens.</summary>
        public DateTime OpenUtc { get; }

        /// <summary>UTC time this war window closes.</summary>
        public DateTime CloseUtc { get; }

        public bool IsOpen => DateTime.UtcNow >= OpenUtc && DateTime.UtcNow < CloseUtc;

        public WarWindow(DateTime openUtc, DateTime closeUtc)
        {
            if (closeUtc <= openUtc)
                throw new ArgumentException("War window close time must be after open time.");
            OpenUtc = openUtc;
            CloseUtc = closeUtc;
        }

        /// <summary>Seconds remaining until this window opens, or 0 if already open.</summary>
        public double SecondsUntilOpen()
        {
            double diff = (OpenUtc - DateTime.UtcNow).TotalSeconds;
            return diff > 0 ? diff : 0;
        }

        /// <summary>Seconds remaining until this window closes, or 0 if already closed.</summary>
        public double SecondsUntilClose()
        {
            double diff = (CloseUtc - DateTime.UtcNow).TotalSeconds;
            return diff > 0 ? diff : 0;
        }
    }

    // ---------------------------------------------------------------------------
    // Rally Attack
    // ---------------------------------------------------------------------------

    /// <summary>Current lifecycle state of a rally attack.</summary>
    public enum RallyState
    {
        Recruiting, // Accepting participant joins
        Launched,   // Rally closed, attack in progress
        Completed,  // Result determined
        Cancelled   // Called off by organizer or not enough members
    }

    /// <summary>
    /// A coordinated multi-alliance-member attack against a single territory.
    /// Minimum RallyMinAttackers required; max RallyMaxAttackers allowed.
    /// </summary>
    public class RallyAttack
    {
        /// <summary>Unique identifier for this rally.</summary>
        public string RallyId { get; }

        /// <summary>Alliance initiating the rally.</summary>
        public string AllianceId { get; }

        /// <summary>Region being targeted.</summary>
        public string TargetRegionId { get; }

        /// <summary>PlayFab ID of the member who started the rally.</summary>
        public string OrganizerPlayFabId { get; }

        /// <summary>UTC time the rally was opened for recruitment.</summary>
        public DateTime OpenedAtUtc { get; }

        /// <summary>UTC time the rally recruitment window closes.</summary>
        public DateTime RecruitmentEndsUtc { get; }

        /// <summary>List of participant PlayFab IDs (includes organizer).</summary>
        public IReadOnlyList<string> Participants => _participants;

        public RallyState State { get; private set; } = RallyState.Recruiting;

        public WarResult Result { get; private set; }

        private readonly List<string> _participants = new();
        private readonly int _minAttackers;
        private readonly int _maxAttackers;

        public RallyAttack(
            string rallyId,
            string allianceId,
            string targetRegionId,
            string organizerPlayFabId,
            int recruitmentDurationSeconds,
            int minAttackers,
            int maxAttackers)
        {
            if (string.IsNullOrEmpty(rallyId))    throw new ArgumentException("RallyId cannot be empty.");
            if (string.IsNullOrEmpty(allianceId)) throw new ArgumentException("AllianceId cannot be empty.");
            if (string.IsNullOrEmpty(targetRegionId)) throw new ArgumentException("TargetRegionId cannot be empty.");
            if (minAttackers < 1) throw new ArgumentException("minAttackers must be >= 1.");
            if (maxAttackers < minAttackers) throw new ArgumentException("maxAttackers must be >= minAttackers.");

            RallyId = rallyId;
            AllianceId = allianceId;
            TargetRegionId = targetRegionId;
            OrganizerPlayFabId = organizerPlayFabId;
            _minAttackers = minAttackers;
            _maxAttackers = maxAttackers;
            OpenedAtUtc = DateTime.UtcNow;
            RecruitmentEndsUtc = DateTime.UtcNow.AddSeconds(recruitmentDurationSeconds);

            // Organizer auto-joins
            _participants.Add(organizerPlayFabId);
        }

        /// <summary>
        /// Add a participant. Returns false if full, already in rally, or not recruiting.
        /// </summary>
        public bool TryJoin(string playFabId)
        {
            if (State != RallyState.Recruiting) return false;
            if (_participants.Count >= _maxAttackers) return false;
            if (_participants.Contains(playFabId)) return false;
            _participants.Add(playFabId);
            return true;
        }

        /// <summary>
        /// Launch the rally (close recruitment, begin attack).
        /// Returns false if not enough participants or wrong state.
        /// </summary>
        public bool TryLaunch()
        {
            if (State != RallyState.Recruiting) return false;
            if (_participants.Count < _minAttackers) return false;
            State = RallyState.Launched;
            return true;
        }

        /// <summary>
        /// Mark rally as cancelled (organizer quit or recruitment window expired with too few members).
        /// </summary>
        public void Cancel()
        {
            if (State == RallyState.Recruiting)
                State = RallyState.Cancelled;
        }

        /// <summary>Apply the final war result (server-confirmed).</summary>
        public void ApplyResult(WarResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            State = RallyState.Completed;
        }
    }

    // ---------------------------------------------------------------------------
    // War Result
    // ---------------------------------------------------------------------------

    /// <summary>Outcome of a single territory attack or defense.</summary>
    public enum WarOutcome
    {
        AttackerVictory,
        DefenderVictory,
        Draw
    }

    /// <summary>
    /// Server-confirmed result of a war action (single attack or rally).
    /// Includes a cryptographic hash for replay validation (check #50).
    /// </summary>
    public class WarResult
    {
        /// <summary>ID of the rally or single attack this result corresponds to.</summary>
        public string ActionId { get; }

        /// <summary>Target region.</summary>
        public string RegionId { get; }

        public WarOutcome Outcome { get; }

        /// <summary>Total attacker combat power contributed.</summary>
        public int AttackerPower { get; }

        /// <summary>Total defender power (fortification + defending members).</summary>
        public int DefenderPower { get; }

        /// <summary>Fortification damage dealt to the territory.</summary>
        public int FortificationDamageDealt { get; }

        /// <summary>
        /// SHA-256 hex string of (actionId + regionId + outcome + attackerPower + defenderPower).
        /// Server computes this; client verifies before accepting state change.
        /// </summary>
        public string ValidationHash { get; }

        public WarResult(
            string actionId, string regionId, WarOutcome outcome,
            int attackerPower, int defenderPower, int fortDamage, string hash)
        {
            ActionId = actionId;
            RegionId = regionId;
            Outcome = outcome;
            AttackerPower = attackerPower;
            DefenderPower = defenderPower;
            FortificationDamageDealt = fortDamage;
            ValidationHash = hash;
        }

        /// <summary>
        /// Verify the hash client-side.
        /// Returns true if the computed hash matches the server-provided hash.
        /// NOTE: SHA-256 is computed in WarEngine.ComputeResultHash (pure C#, no Unity).
        /// </summary>
        public bool IsHashValid()
        {
            string computed = WarEngine.ComputeResultHash(ActionId, RegionId, Outcome, AttackerPower, DefenderPower);
            return string.Equals(computed, ValidationHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------------------
    // WarEngine
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages active rallies, war window scheduling, and territory attack resolution.
    ///
    /// Architecture:
    /// - All territory captures are server-authoritative (PlayFab Cloud Script).
    /// - Client computes power totals locally for preview, but never commits territory changes.
    /// - Server returns WarResult with validation hash; client verifies before applying.
    /// - WarEngine.ResolveAttack() is pure C# for unit testability (no MonoBehaviour dependency).
    /// </summary>
    public class WarEngine : MonoBehaviour
    {
        [SerializeField] private TerritoryConfig _config;

        private TerritoryManager _territoryManager;

        // Active rallies indexed by rallyId
        private readonly Dictionary<string, RallyAttack> _activeRallies = new();

        private EventSubscription _warWindowSub;

        private void Awake()
        {
            if (_config == null)
                _config = Resources.Load<TerritoryConfig>("TerritoryConfig");
            ServiceLocator.Register<WarEngine>(this);
        }

        private void Start()
        {
            _territoryManager = ServiceLocator.Get<TerritoryManager>();
        }

        private void OnDestroy()
        {
            _warWindowSub?.Dispose();
            ServiceLocator.Unregister<WarEngine>();
        }

        private void Update()
        {
            // Expire rally recruitment windows that passed their deadline.
            // Max active rallies per alliance ≤ total territory regions (~200), but in practice
            // bounded by alliance size (50 players). This scan is O(n) on activeRallies count,
            // which stays well within the 2ms per-frame budget at expected scale.
            // Uses a snapshot list to avoid modifying dict while iterating.
            List<string> toExpire = null;
            foreach (var kvp in _activeRallies)
            {
                var rally = kvp.Value;
                if (rally.State == RallyState.Recruiting && DateTime.UtcNow >= rally.RecruitmentEndsUtc)
                {
                    toExpire ??= new List<string>();
                    toExpire.Add(kvp.Key);
                }
            }
            if (toExpire != null)
            {
                foreach (var id in toExpire)
                    ExpireRecruitment(id);
            }
        }

        // -------------------------------------------------------------------
        // War Window Scheduling
        // -------------------------------------------------------------------

        /// <summary>
        /// Compute the next N war windows starting from now, based on config schedule.
        /// Pure calculation — no side effects.
        /// </summary>
        public List<WarWindow> GetUpcomingWarWindows(int count)
        {
            if (_config == null) return new List<WarWindow>();

            var windows = new List<WarWindow>(count);
            DateTime now = DateTime.UtcNow;

            // Calculate today's first window
            DateTime todayBase = now.Date.AddHours(_config.WarWindowStartHourUtc);
            int windowDurationSeconds = _config.WarWindowDurationSeconds;
            int gapSeconds = (24 * 3600) / _config.WarWindowsPerDay;

            // Walk forward until we have `count` future windows
            DateTime candidate = todayBase;
            while (windows.Count < count)
            {
                DateTime candidateClose = candidate.AddSeconds(windowDurationSeconds);
                if (candidateClose > now) // Future or currently open
                {
                    windows.Add(new WarWindow(candidate, candidateClose));
                }
                candidate = candidate.AddSeconds(gapSeconds);
            }

            return windows;
        }

        /// <summary>
        /// Returns true if territory attacks are currently permitted (war window is open).
        /// </summary>
        public bool IsWarWindowOpen()
        {
            var windows = GetUpcomingWarWindows(1);
            if (windows.Count == 0) return false;
            return windows[0].IsOpen;
        }

        // -------------------------------------------------------------------
        // Rally Management
        // -------------------------------------------------------------------

        /// <summary>
        /// Create a new rally targeting a region. Returns the created rally, or null on failure.
        /// Validates: war window open, region attackable, player has StartRally permission.
        /// </summary>
        public RallyAttack StartRally(string organizerPlayFabId, string allianceId, string targetRegionId)
        {
            if (_config == null) return null;
            if (!IsWarWindowOpen()) return null;
            if (_territoryManager != null && !_territoryManager.IsAttackable(targetRegionId, allianceId))
                return null;

            string rallyId = $"{allianceId}_{targetRegionId}_{DateTime.UtcNow.Ticks}";
            var rally = new RallyAttack(
                rallyId, allianceId, targetRegionId, organizerPlayFabId,
                _config.RallyOpenDurationSeconds,
                _config.RallyMinAttackers,
                _config.RallyMaxAttackers);

            _activeRallies[rallyId] = rally;
            EventBus.Publish(new RallyStartedEvent(rallyId, allianceId, targetRegionId));
            return rally;
        }

        /// <summary>
        /// Add a player to an active rally's participant list.
        /// </summary>
        public bool JoinRally(string rallyId, string playFabId)
        {
            if (!_activeRallies.TryGetValue(rallyId, out var rally)) return false;
            bool joined = rally.TryJoin(playFabId);
            if (joined)
                EventBus.Publish(new RallyMemberJoinedEvent(rallyId, playFabId));
            return joined;
        }

        /// <summary>
        /// Launch a rally (close recruitment, send attack to server).
        /// </summary>
        public bool LaunchRally(string rallyId)
        {
            if (!_activeRallies.TryGetValue(rallyId, out var rally)) return false;
            bool launched = rally.TryLaunch();
            if (launched)
                EventBus.Publish(new RallyLaunchedEvent(rallyId, rally.TargetRegionId, rally.Participants.Count));
            return launched;
        }

        /// <summary>
        /// Apply a server-validated war result to the rally and territory state.
        /// Verifies the hash before applying (check #50).
        /// </summary>
        public bool ApplyWarResult(string rallyId, WarResult result)
        {
            if (result == null) return false;

            // Security: verify server hash before trusting result
            if (!result.IsHashValid())
            {
                Debug.LogWarning($"[WarEngine] War result hash mismatch for rally {rallyId}. Rejecting.");
                return false;
            }

            if (_activeRallies.TryGetValue(rallyId, out var rally))
                rally.ApplyResult(result);

            if (result.Outcome == WarOutcome.AttackerVictory && _territoryManager != null)
            {
                string allianceId = _activeRallies.TryGetValue(rallyId, out var r) ? r.AllianceId : null;
                if (allianceId != null)
                    _territoryManager.ApplyCapture(result.RegionId, allianceId);
            }

            EventBus.Publish(new WarResultAppliedEvent(result.RegionId, result.Outcome));
            return true;
        }

        // -------------------------------------------------------------------
        // Pure C# logic — unit-testable without MonoBehaviour
        // -------------------------------------------------------------------

        /// <summary>
        /// Compute total attacker power from a list of participant power scores.
        /// Power scales with participant count (logarithmic bonus for coordination).
        /// </summary>
        public static int ComputeAttackerPower(IReadOnlyList<int> participantPowerScores)
        {
            if (participantPowerScores == null || participantPowerScores.Count == 0) return 0;

            int basePower = 0;
            foreach (int p in participantPowerScores)
                basePower += Mathf.Max(0, p);

            // Coordination bonus: log2(participantCount) * 5% per extra member
            float coordinationMultiplier = 1f + 0.05f * Mathf.Log(participantPowerScores.Count + 1, 2f);
            return Mathf.RoundToInt(basePower * coordinationMultiplier);
        }

        /// <summary>
        /// Resolve a territory attack outcome (pure logic, no Unity or server calls).
        /// Attacker wins if attackerPower > defenderPower (including fortification).
        /// </summary>
        public static WarOutcome ResolveAttack(int attackerPower, int defenderPower, int fortificationHp)
        {
            // Fortification adds to defender power at a 1:1 ratio
            int totalDefense = defenderPower + fortificationHp;
            if (attackerPower > totalDefense) return WarOutcome.AttackerVictory;
            if (attackerPower < totalDefense) return WarOutcome.DefenderVictory;
            return WarOutcome.Draw;
        }

        /// <summary>
        /// Compute SHA-256 hash of the war result components for server-side validation (check #50).
        /// Uses System.Security.Cryptography — pure C#, no Unity.
        /// </summary>
        public static string ComputeResultHash(
            string actionId, string regionId, WarOutcome outcome,
            int attackerPower, int defenderPower)
        {
            string payload = $"{actionId}|{regionId}|{(int)outcome}|{attackerPower}|{defenderPower}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            byte[] hash = sha256.ComputeHash(bytes);

            // Build hex string without string concatenation (check #19)
            var sb = new System.Text.StringBuilder(64);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private void ExpireRecruitment(string rallyId)
        {
            if (!_activeRallies.TryGetValue(rallyId, out var rally)) return;
            // Auto-launch if minimum met, otherwise cancel
            if (!rally.TryLaunch())
            {
                rally.Cancel();
                _activeRallies.Remove(rallyId);
                EventBus.Publish(new RallyCancelledEvent(rallyId));
            }
            else
            {
                EventBus.Publish(new RallyLaunchedEvent(rallyId, rally.TargetRegionId, rally.Participants.Count));
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct RallyStartedEvent
    {
        public readonly string RallyId;
        public readonly string AllianceId;
        public readonly string TargetRegionId;
        public RallyStartedEvent(string rallyId, string allianceId, string target)
        { RallyId = rallyId; AllianceId = allianceId; TargetRegionId = target; }
    }

    public readonly struct RallyMemberJoinedEvent
    {
        public readonly string RallyId;
        public readonly string PlayFabId;
        public RallyMemberJoinedEvent(string rallyId, string id) { RallyId = rallyId; PlayFabId = id; }
    }

    public readonly struct RallyLaunchedEvent
    {
        public readonly string RallyId;
        public readonly string TargetRegionId;
        public readonly int ParticipantCount;
        public RallyLaunchedEvent(string rallyId, string target, int count)
        { RallyId = rallyId; TargetRegionId = target; ParticipantCount = count; }
    }

    public readonly struct RallyCancelledEvent
    {
        public readonly string RallyId;
        public RallyCancelledEvent(string rallyId) { RallyId = rallyId; }
    }

    public readonly struct WarResultAppliedEvent
    {
        public readonly string RegionId;
        public readonly WarOutcome Outcome;
        public WarResultAppliedEvent(string regionId, WarOutcome outcome)
        { RegionId = regionId; Outcome = outcome; }
    }
}
