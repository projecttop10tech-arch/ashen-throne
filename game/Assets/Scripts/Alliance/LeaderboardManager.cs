using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Alliance
{
    // ---------------------------------------------------------------------------
    // Leaderboard data types
    // ---------------------------------------------------------------------------

    /// <summary>Available leaderboard categories.</summary>
    public enum LeaderboardType
    {
        SoloPower,       // Individual player power score
        AllianceScore,   // Alliance total score (power + territory + contribution)
        TerritoryCount   // Alliance territory count
    }

    /// <summary>A single entry in a leaderboard.</summary>
    public class LeaderboardEntry
    {
        /// <summary>PlayFab entity ID (player or alliance).</summary>
        public string EntityId { get; }

        /// <summary>Display name for the leaderboard row.</summary>
        public string DisplayName { get; }

        /// <summary>Numeric score for sorting.</summary>
        public long Score { get; }

        /// <summary>Server-assigned rank (1 = best). Rank 0 = unranked (beyond top N).</summary>
        public int Rank { get; }

        /// <summary>Whether this entry represents the local player or their alliance.</summary>
        public bool IsLocalPlayer { get; }

        /// <summary>Alliance tag shown next to player name, if applicable.</summary>
        public string AllianceTag { get; }

        public LeaderboardEntry(string entityId, string displayName, long score, int rank,
            bool isLocalPlayer = false, string allianceTag = null)
        {
            EntityId = entityId;
            DisplayName = displayName;
            Score = score;
            Rank = rank;
            IsLocalPlayer = isLocalPlayer;
            AllianceTag = allianceTag;
        }
    }

    /// <summary>Full leaderboard result for one category.</summary>
    public class LeaderboardResult
    {
        public LeaderboardType Type { get; }
        public IReadOnlyList<LeaderboardEntry> Entries { get; }

        /// <summary>Server-provided UTC time when this data was generated.</summary>
        public DateTime FetchedAtUtc { get; }

        /// <summary>
        /// The local player/alliance entry even if they are outside the top displayed range.
        /// Null if not retrieved.
        /// </summary>
        public LeaderboardEntry LocalEntry { get; }

        public LeaderboardResult(LeaderboardType type, IReadOnlyList<LeaderboardEntry> entries,
            DateTime fetchedAt, LeaderboardEntry localEntry = null)
        {
            Type = type;
            Entries = entries ?? Array.Empty<LeaderboardEntry>() as IReadOnlyList<LeaderboardEntry>;
            FetchedAtUtc = fetchedAt;
            LocalEntry = localEntry;
        }
    }

    // ---------------------------------------------------------------------------
    // LeaderboardManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages cached leaderboard data fetched from PlayFab.
    ///
    /// Architecture:
    /// - Leaderboard data is server-authoritative and read-only on the client.
    /// - Cached results are invalidated after CacheExpirySeconds.
    /// - RequestLeaderboard fires a PlayFab call (stubbed) and publishes result via EventBus.
    /// - All PlayFab callbacks dispatched to main thread (check #9).
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        /// <summary>Seconds before a cached leaderboard result is considered stale.</summary>
        public const int CacheExpirySeconds = 300;

        /// <summary>Number of entries per page fetched from the server.</summary>
        public const int PageSize = 100;

        private readonly Dictionary<LeaderboardType, LeaderboardResult> _cache = new();
        private readonly Dictionary<LeaderboardType, bool> _pendingRequests = new();

        private void Awake()
        {
            ServiceLocator.Register<LeaderboardManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<LeaderboardManager>();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Request a leaderboard refresh from the server.
        /// If a cached result exists and is not stale, returns it immediately via callback.
        /// Otherwise fires a PlayFab request (currently stubbed — checks #55 integration test marker).
        /// </summary>
        public void RequestLeaderboard(LeaderboardType type, Action<LeaderboardResult> onComplete)
        {
            if (onComplete == null) return;

            // Return cached result if fresh
            if (_cache.TryGetValue(type, out var cached))
            {
                double age = (DateTime.UtcNow - cached.FetchedAtUtc).TotalSeconds;
                if (age < CacheExpirySeconds)
                {
                    onComplete(cached);
                    return;
                }
            }

            // Prevent duplicate in-flight requests
            if (_pendingRequests.TryGetValue(type, out bool pending) && pending) return;
            _pendingRequests[type] = true;

            // PlayFab stub — in production, call PlayFabClientAPI.GetLeaderboard
            // Dispatched to main thread via MainThreadDispatcher when real SDK is installed
            FetchLeaderboardStub(type, result =>
            {
                _pendingRequests[type] = false;
                if (result != null)
                {
                    _cache[type] = result;
                    EventBus.Publish(new LeaderboardUpdatedEvent(type, result));
                }
                onComplete(result);
            });
        }

        /// <summary>Returns the cached leaderboard result, or null if not yet fetched.</summary>
        public LeaderboardResult GetCached(LeaderboardType type)
        {
            return _cache.TryGetValue(type, out var result) ? result : null;
        }

        /// <summary>Invalidate the cache for all leaderboard types (e.g., after a territory capture).</summary>
        public void InvalidateCache()
        {
            _cache.Clear();
        }

        /// <summary>Invalidate the cache for a specific leaderboard type.</summary>
        public void InvalidateCache(LeaderboardType type)
        {
            _cache.Remove(type);
        }

        // -------------------------------------------------------------------
        // Pure logic helpers (unit-testable)
        // -------------------------------------------------------------------

        /// <summary>
        /// Sort a flat list of entries by score descending and assign ranks.
        /// Handles ties: tied entries share the same rank; next rank is skipped.
        /// </summary>
        public static List<LeaderboardEntry> RankEntries(List<LeaderboardEntry> entries)
        {
            if (entries == null || entries.Count == 0) return new List<LeaderboardEntry>();

            entries.Sort((a, b) => b.Score.CompareTo(a.Score));

            var ranked = new List<LeaderboardEntry>(entries.Count);
            int currentRank = 1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0 && entries[i].Score < entries[i - 1].Score)
                    currentRank = i + 1;

                ranked.Add(new LeaderboardEntry(
                    entries[i].EntityId, entries[i].DisplayName,
                    entries[i].Score, currentRank,
                    entries[i].IsLocalPlayer, entries[i].AllianceTag));
            }
            return ranked;
        }

        // -------------------------------------------------------------------
        // Stub (replace with real PlayFab calls when SDK is active)
        // -------------------------------------------------------------------

        private void FetchLeaderboardStub(LeaderboardType type, Action<LeaderboardResult> callback)
        {
            // Stub returns empty result immediately on main thread
            // In production: PlayFabClientAPI.GetLeaderboard → MainThreadDispatcher → callback
            var empty = new LeaderboardResult(type, Array.Empty<LeaderboardEntry>(), DateTime.UtcNow);
            callback(empty);
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public readonly struct LeaderboardUpdatedEvent
    {
        public readonly LeaderboardType Type;
        public readonly LeaderboardResult Result;
        public LeaderboardUpdatedEvent(LeaderboardType t, LeaderboardResult r) { Type = t; Result = r; }
    }
}
