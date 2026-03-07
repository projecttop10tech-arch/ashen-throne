using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;

namespace AshenThrone.Alliance
{
    // ---------------------------------------------------------------------------
    // Loadout Record — what gets serialized as the "ghost" for async PvP
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Serializable snapshot of a single hero's state as configured for an async PvP battle.
    /// This is what gets stored server-side and used by the simulator when an opponent attacks.
    /// </summary>
    [System.Serializable]
    public class PvpHeroRecord
    {
        /// <summary>ID of the hero data asset (e.g., "kaelen_ironwrath").</summary>
        public string HeroDataId;

        /// <summary>Hero level at time of recording.</summary>
        public int Level;

        /// <summary>Star tier (0–5), affects stat scaling.</summary>
        public int StarTier;

        /// <summary>Ordered list of 15 ability card IDs in the player's deck.</summary>
        public List<string> DeckCardIds = new(15);

        /// <summary>Preferred grid row for placement.</summary>
        public CombatRow PreferredRow;
    }

    /// <summary>
    /// A full recorded loadout: 3 heroes + metadata, stored server-side for async defense.
    /// </summary>
    [System.Serializable]
    public class PvpLoadoutRecord
    {
        /// <summary>PlayFab ID of the player who recorded this loadout.</summary>
        public string OwnerPlayFabId;

        /// <summary>Up to 3 hero records in preferred placement order.</summary>
        public List<PvpHeroRecord> Heroes = new(3);

        /// <summary>UTC ISO-8601 string when this loadout was last saved.</summary>
        public string RecordedAtUtc;

        /// <summary>
        /// SHA-256 hash of the serialized hero records + player server stats.
        /// Server validates this before using the loadout in battle to prevent spoofing.
        /// </summary>
        public string IntegrityHash;

        /// <summary>
        /// Season or patch version this record was created under.
        /// Records from old patch versions are treated as expired (stats change with patches).
        /// </summary>
        public string GameVersion;
    }

    // ---------------------------------------------------------------------------
    // Battle replay record
    // ---------------------------------------------------------------------------

    /// <summary>A single action in a replay: which hero, which card, which target.</summary>
    [System.Serializable]
    public class ReplayAction
    {
        /// <summary>Turn number (1-indexed).</summary>
        public int TurnNumber;

        /// <summary>InstanceId of the acting hero (index into the HeroRecord list for determinism).</summary>
        public int HeroIndex;

        /// <summary>Card ID played. Null = end turn without playing.</summary>
        public string CardId;

        /// <summary>Target grid position.</summary>
        public int TargetColumn;
        public int TargetRow;

        /// <summary>Damage dealt or healing applied by this action.</summary>
        public int EffectValue;

        /// <summary>Whether a combo was activated.</summary>
        public bool ComboActivated;
    }

    /// <summary>
    /// Full record of an async PvP battle, used for both replay display and server validation.
    /// </summary>
    [System.Serializable]
    public class CombatReplayData
    {
        /// <summary>Unique ID for this replay.</summary>
        public string ReplayId;

        /// <summary>Attacker's recorded loadout (the challenger).</summary>
        public PvpLoadoutRecord AttackerLoadout;

        /// <summary>Defender's recorded loadout (the ghost being attacked).</summary>
        public PvpLoadoutRecord DefenderLoadout;

        /// <summary>Ordered list of actions taken during the battle (deterministic replay).</summary>
        public List<ReplayAction> Actions = new();

        /// <summary>
        /// Final outcome of the battle.
        /// NOTE: BattleOutcome is defined in TurnManager.cs (Combat namespace).
        /// Using string here for serialization isolation between Alliance and Combat namespaces.
        /// Valid values: "PlayerVictory", "PlayerDefeat", "Draw".
        /// </summary>
        public string Outcome;

        /// <summary>Total turns the battle lasted.</summary>
        public int TotalTurns;

        /// <summary>UTC ISO-8601 timestamp of when the battle was simulated.</summary>
        public string SimulatedAtUtc;

        /// <summary>
        /// SHA-256 of (ReplayId + AttackerLoadout.IntegrityHash + DefenderLoadout.IntegrityHash
        ///             + Outcome + TotalTurns).
        /// Server computes this; client verifies before displaying or accepting result.
        /// </summary>
        public string ValidationHash;

        /// <summary>Verify the replay's hash client-side (check #50).</summary>
        public bool IsHashValid()
        {
            if (AttackerLoadout == null || DefenderLoadout == null) return false;
            string computed = AsyncPvpManager.ComputeReplayHash(
                ReplayId, AttackerLoadout.IntegrityHash,
                DefenderLoadout.IntegrityHash, Outcome, TotalTurns);
            return string.Equals(computed, ValidationHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------------------
    // AsyncPvpManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages async PvP: recording player loadouts, queuing attack requests,
    /// receiving server-simulated results, and providing replay data for playback.
    ///
    /// Architecture:
    /// - The server (PlayFab Cloud Script) runs the authoritative battle simulation.
    /// - Client records the player's loadout and submits it; never simulates its own defense wins.
    /// - Replays are deterministic: given the same seed + loadouts + actions, the result is identical.
    /// - All results include a validation hash (check #50).
    /// </summary>
    public class AsyncPvpManager : MonoBehaviour
    {
        /// <summary>Maximum number of replays kept in local history before oldest is evicted.</summary>
        public const int MaxReplayHistory = 50;

        private readonly List<CombatReplayData> _replayHistory = new(MaxReplayHistory);

        // Cached loadout for the local player (sent to server; used as defense ghost)
        private PvpLoadoutRecord _localLoadout;

        public event Action<CombatReplayData> OnReplayReceived;

        private void Awake()
        {
            ServiceLocator.Register<AsyncPvpManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AsyncPvpManager>();
        }

        // -------------------------------------------------------------------
        // Loadout management
        // -------------------------------------------------------------------

        /// <summary>
        /// Record the local player's current hero loadout for async PvP defense.
        /// Computes the integrity hash and sends to server (stubbed).
        /// </summary>
        public bool RecordLoadout(string ownerPlayFabId, List<PvpHeroRecord> heroes, string gameVersion)
        {
            if (string.IsNullOrEmpty(ownerPlayFabId))
            {
                Debug.LogWarning("[AsyncPvpManager] Cannot record loadout: ownerPlayFabId is null/empty.");
                return false;
            }
            if (heroes == null || heroes.Count == 0 || heroes.Count > 3)
            {
                Debug.LogWarning("[AsyncPvpManager] Loadout must contain 1–3 heroes.");
                return false;
            }

            var loadout = new PvpLoadoutRecord
            {
                OwnerPlayFabId = ownerPlayFabId,
                Heroes = new List<PvpHeroRecord>(heroes),
                RecordedAtUtc = DateTime.UtcNow.ToString("O"),
                GameVersion = gameVersion ?? "1.0.0"
            };
            loadout.IntegrityHash = ComputeLoadoutHash(loadout);

            _localLoadout = loadout;
            EventBus.Publish(new PvpLoadoutRecordedEvent(ownerPlayFabId));
            return true;
        }

        /// <summary>Returns the most recently recorded loadout for the local player.</summary>
        public PvpLoadoutRecord GetLocalLoadout() => _localLoadout;

        // -------------------------------------------------------------------
        // Attack & result handling
        // -------------------------------------------------------------------

        /// <summary>
        /// Submit an attack request against a defender's loadout.
        /// Returns a request ID. Server will respond asynchronously via ReceiveReplay.
        /// </summary>
        public string RequestAttack(string attackerPlayFabId, string defenderPlayFabId)
        {
            if (string.IsNullOrEmpty(attackerPlayFabId) || string.IsNullOrEmpty(defenderPlayFabId))
                return null;
            if (_localLoadout == null)
            {
                Debug.LogWarning("[AsyncPvpManager] Cannot attack: local loadout not recorded.");
                return null;
            }

            string requestId = $"pvp_{attackerPlayFabId}_{defenderPlayFabId}_{DateTime.UtcNow.Ticks}";
            EventBus.Publish(new PvpAttackRequestedEvent(requestId, attackerPlayFabId, defenderPlayFabId));
            // In production: dispatch to PlayFab Cloud Script with requestId + loadout hashes
            return requestId;
        }

        /// <summary>
        /// Receive a server-simulated replay result.
        /// Verifies the hash before storing (check #50). Dispatched to main thread from PlayFab callback.
        /// </summary>
        public bool ReceiveReplay(CombatReplayData replay)
        {
            if (replay == null) return false;

            if (!replay.IsHashValid())
            {
                Debug.LogWarning($"[AsyncPvpManager] Replay {replay.ReplayId} failed hash validation. Discarding.");
                return false;
            }

            // Ring buffer eviction
            if (_replayHistory.Count >= MaxReplayHistory)
                _replayHistory.RemoveAt(0);

            _replayHistory.Add(replay);
            OnReplayReceived?.Invoke(replay);
            EventBus.Publish(new PvpReplayReceivedEvent(replay.ReplayId, replay.Outcome));
            return true;
        }

        /// <summary>Returns the full replay history (newest last).</summary>
        public IReadOnlyList<CombatReplayData> GetReplayHistory() => _replayHistory;

        /// <summary>Returns a replay by ID, or null if not found.</summary>
        public CombatReplayData GetReplay(string replayId)
        {
            if (string.IsNullOrEmpty(replayId)) return null;
            foreach (var r in _replayHistory)
            {
                if (r.ReplayId == replayId) return r;
            }
            return null;
        }

        // -------------------------------------------------------------------
        // Pure C# hash computation — unit-testable, no Unity dependency
        // -------------------------------------------------------------------

        /// <summary>
        /// Compute SHA-256 integrity hash for a PvpLoadoutRecord.
        /// Uses heroDataId + level + starTier for each hero to prevent stat-spoofing.
        /// </summary>
        public static string ComputeLoadoutHash(PvpLoadoutRecord loadout)
        {
            if (loadout?.Heroes == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append(loadout.OwnerPlayFabId);
            sb.Append('|');
            foreach (var hero in loadout.Heroes)
            {
                if (hero == null) continue;
                sb.Append(hero.HeroDataId).Append(':').Append(hero.Level).Append(':').Append(hero.StarTier).Append('|');
            }

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            byte[] hash = sha256.ComputeHash(bytes);

            var hex = new System.Text.StringBuilder(64);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        /// <summary>
        /// Compute SHA-256 validation hash for a combat replay result (check #50).
        /// </summary>
        public static string ComputeReplayHash(
            string replayId, string attackerHash, string defenderHash,
            string outcome, int totalTurns)
        {
            string payload = $"{replayId}|{attackerHash}|{defenderHash}|{outcome}|{totalTurns}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
            byte[] hash = sha256.ComputeHash(bytes);

            var hex = new System.Text.StringBuilder(64);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct PvpLoadoutRecordedEvent
    {
        public readonly string PlayFabId;
        public PvpLoadoutRecordedEvent(string id) { PlayFabId = id; }
    }

    public readonly struct PvpAttackRequestedEvent
    {
        public readonly string RequestId;
        public readonly string AttackerPlayFabId;
        public readonly string DefenderPlayFabId;
        public PvpAttackRequestedEvent(string req, string atk, string def)
        { RequestId = req; AttackerPlayFabId = atk; DefenderPlayFabId = def; }
    }

    public readonly struct PvpReplayReceivedEvent
    {
        public readonly string ReplayId;
        public readonly string Outcome;
        public PvpReplayReceivedEvent(string id, string outcome) { ReplayId = id; Outcome = outcome; }
    }
}
