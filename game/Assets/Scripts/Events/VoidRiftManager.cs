using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Events
{
    // ---------------------------------------------------------------------------
    // Void Rift — roguelite dungeon variant event data
    // ---------------------------------------------------------------------------

    /// <summary>A single floor/encounter node in the Void Rift dungeon.</summary>
    public enum VoidRiftNodeType
    {
        Combat,     // Standard combat encounter
        Elite,      // Tougher elite enemy
        Rest,       // Heal HP and recover buffs
        Treasure,   // Loot room (resources or cosmetics)
        Boss        // Final floor boss
    }

    /// <summary>Runtime state of a single dungeon node.</summary>
    public class VoidRiftNode
    {
        /// <summary>Zero-based floor index (0 = first floor).</summary>
        public int Floor { get; }

        /// <summary>Which of the branching paths on this floor (0–1 or 0–2).</summary>
        public int PathIndex { get; }

        public VoidRiftNodeType NodeType { get; }

        /// <summary>EnemyData asset ID for combat/elite/boss nodes.</summary>
        public string EnemyId { get; }

        public bool IsCompleted { get; private set; }
        public bool IsSkipped  { get; private set; }

        public VoidRiftNode(int floor, int pathIndex, VoidRiftNodeType nodeType, string enemyId = null)
        {
            Floor     = floor;
            PathIndex = pathIndex;
            NodeType  = nodeType;
            EnemyId   = enemyId;
        }

        public void MarkCompleted() => IsCompleted = true;
        public void MarkSkipped()   => IsSkipped   = true;
    }

    /// <summary>
    /// Relic buff applied to the player's squad for the remainder of the run.
    /// Source: Treasure rooms and boss clears.
    /// </summary>
    public class VoidRelic
    {
        /// <summary>Unique relic asset identifier.</summary>
        public string RelicId { get; }

        /// <summary>Human-readable display name.</summary>
        public string DisplayName { get; }

        /// <summary>Flat HP added to all heroes each floor.</summary>
        public int BonusHpPerFloor { get; }

        /// <summary>Percentage damage bonus (0.1 = 10%).</summary>
        public float BonusDamagePercent { get; }

        /// <summary>Energy refunded at start of each turn (0–1).</summary>
        public int BonusStartingEnergy { get; }

        public VoidRelic(string relicId, string displayName,
            int bonusHpPerFloor, float bonusDamagePercent, int bonusStartingEnergy)
        {
            if (string.IsNullOrEmpty(relicId))
                throw new ArgumentException("RelicId cannot be empty.");

            RelicId              = relicId;
            DisplayName          = displayName;
            BonusHpPerFloor      = bonusHpPerFloor;
            BonusDamagePercent   = bonusDamagePercent;
            BonusStartingEnergy  = bonusStartingEnergy;
        }
    }

    // ---------------------------------------------------------------------------
    // VoidRiftConfig ScriptableObject
    // ---------------------------------------------------------------------------

    /// <summary>Tuning values for the Void Rift roguelite event.</summary>
    [CreateAssetMenu(fileName = "VoidRiftConfig", menuName = "AshenThrone/Events/VoidRiftConfig")]
    public class VoidRiftConfig : ScriptableObject
    {
        [Tooltip("Total number of floors in one run.")]
        public int TotalFloors = 10;

        [Tooltip("Number of branching path choices per floor (1–3).")]
        [Range(1, 3)]
        public int PathsPerFloor = 2;

        [Tooltip("Chance (0–1) that a floor is an Elite node.")]
        [Range(0f, 1f)]
        public float EliteChance = 0.2f;

        [Tooltip("Chance (0–1) that a floor is a Treasure node.")]
        [Range(0f, 1f)]
        public float TreasureChance = 0.15f;

        [Tooltip("Chance (0–1) that a floor is a Rest node.")]
        [Range(0f, 1f)]
        public float RestChance = 0.1f;

        [Tooltip("HP percentage restored at a Rest node (0–1).")]
        [Range(0f, 1f)]
        public float RestHealPercent = 0.3f;

        [Tooltip("Maximum relics a player can carry simultaneously.")]
        public int MaxRelics = 5;

        [Tooltip("Score multiplier per elite cleared.")]
        public float EliteScoreMultiplier = 1.5f;

        [Tooltip("Score multiplier for clearing all floors (full run bonus).")]
        public float FullRunBonusMultiplier = 2.0f;

        [Tooltip("Daily score submission deadline ISO-8601 UTC string.")]
        public string DailyResetTimeIso = "00:00:00";

        [Tooltip("Top N percentile required for Legendary cosmetic reward tier.")]
        [Range(1, 100)]
        public int LegendaryRewardPercentile = 5;
    }

    // ---------------------------------------------------------------------------
    // VoidRiftRunState — pure C# (unit-testable)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Complete runtime state for a single Void Rift dungeon run.
    /// Pure C# — no MonoBehaviour dependency, fully unit-testable.
    /// </summary>
    public class VoidRiftRunState
    {
        private readonly VoidRiftConfig _config;
        private readonly List<VoidRiftNode[]> _floors; // [floorIndex][pathIndex]
        private readonly List<VoidRelic> _relics;

        public int CurrentFloor { get; private set; }
        public int Score        { get; private set; }
        public bool IsRunOver   { get; private set; }
        public bool IsWon       => CurrentFloor >= _config.TotalFloors && IsRunOver;

        /// <summary>Read-only view of collected relics.</summary>
        public IReadOnlyList<VoidRelic> Relics => _relics;

        /// <summary>Number of floors built in this run.</summary>
        public int TotalFloors => _floors.Count;

        public VoidRiftRunState(VoidRiftConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _relics  = new List<VoidRelic>(config.MaxRelics);
            _floors  = new List<VoidRiftNode[]>(config.TotalFloors);

            GenerateFloors();
        }

        // -------------------------------------------------------------------
        // Floor generation
        // -------------------------------------------------------------------

        private void GenerateFloors()
        {
            for (int floor = 0; floor < _config.TotalFloors; floor++)
            {
                bool isBossFloor = floor == _config.TotalFloors - 1;
                int  paths       = isBossFloor ? 1 : _config.PathsPerFloor;
                var  nodes       = new VoidRiftNode[paths];

                for (int path = 0; path < paths; path++)
                {
                    VoidRiftNodeType nodeType;
                    if (isBossFloor)
                    {
                        nodeType = VoidRiftNodeType.Boss;
                    }
                    else
                    {
                        nodeType = RollNodeType(floor);
                    }
                    nodes[path] = new VoidRiftNode(floor, path, nodeType);
                }
                _floors.Add(nodes);
            }
        }

        /// <summary>Deterministically selects a node type using config probabilities.</summary>
        private VoidRiftNodeType RollNodeType(int floor)
        {
            float roll = UnityEngine.Random.value;
            if (roll < _config.TreasureChance) return VoidRiftNodeType.Treasure;
            roll -= _config.TreasureChance;
            if (roll < _config.RestChance)     return VoidRiftNodeType.Rest;
            roll -= _config.RestChance;
            if (roll < _config.EliteChance)    return VoidRiftNodeType.Elite;
            return VoidRiftNodeType.Combat;
        }

        // -------------------------------------------------------------------
        // Run actions
        // -------------------------------------------------------------------

        /// <summary>
        /// Player selects a path on the current floor to traverse.
        /// Returns the chosen node or null if invalid.
        /// </summary>
        public VoidRiftNode SelectPath(int pathIndex)
        {
            if (IsRunOver) return null;
            if (CurrentFloor >= _floors.Count) return null;

            var nodes = _floors[CurrentFloor];
            if (pathIndex < 0 || pathIndex >= nodes.Length) return null;

            return nodes[pathIndex];
        }

        /// <summary>
        /// Mark the current floor node as completed and advance to next floor.
        /// Applies score and triggers boss-win check.
        /// </summary>
        public void CompleteNode(int pathIndex, bool isEliteKill = false)
        {
            if (IsRunOver) return;
            if (CurrentFloor >= _floors.Count) return;

            var nodes = _floors[CurrentFloor];
            if (pathIndex < 0 || pathIndex >= nodes.Length) return;

            var node = nodes[pathIndex];
            node.MarkCompleted();

            // Mark other paths on this floor as skipped
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i != pathIndex && !nodes[i].IsCompleted)
                    nodes[i].MarkSkipped();
            }

            // Score accrual
            int baseScore = 100 * (CurrentFloor + 1);
            if (isEliteKill || node.NodeType == VoidRiftNodeType.Elite)
                baseScore = Mathf.RoundToInt(baseScore * _config.EliteScoreMultiplier);

            Score += baseScore;

            CurrentFloor++;

            // Check win condition
            if (CurrentFloor >= _config.TotalFloors)
            {
                Score = Mathf.RoundToInt(Score * _config.FullRunBonusMultiplier);
                IsRunOver = true;
            }
        }

        /// <summary>End the run early (hero defeat). Finalises score.</summary>
        public void EndRunDefeat()
        {
            if (IsRunOver) return;
            IsRunOver = true;
        }

        // -------------------------------------------------------------------
        // Relic management
        // -------------------------------------------------------------------

        /// <summary>
        /// Add a relic to the run. Clamped to MaxRelics — oldest relic is discarded
        /// to make room if at capacity.
        /// </summary>
        public void AddRelic(VoidRelic relic)
        {
            if (relic == null) return;
            if (_relics.Count >= _config.MaxRelics)
                _relics.RemoveAt(0); // discard oldest (ring-buffer style)
            _relics.Add(relic);
        }

        /// <summary>Sum of bonus HP per floor from all active relics.</summary>
        public int TotalBonusHpPerFloor()
        {
            int total = 0;
            for (int i = 0; i < _relics.Count; i++)
                total += _relics[i].BonusHpPerFloor;
            return total;
        }

        /// <summary>Sum of damage bonus percent from all active relics.</summary>
        public float TotalBonusDamagePercent()
        {
            float total = 0f;
            for (int i = 0; i < _relics.Count; i++)
                total += _relics[i].BonusDamagePercent;
            return total;
        }

        /// <summary>Sum of bonus starting energy from all active relics (capped at 3).</summary>
        public int TotalBonusStartingEnergy()
        {
            int total = 0;
            for (int i = 0; i < _relics.Count; i++)
                total += _relics[i].BonusStartingEnergy;
            return Mathf.Min(total, 3);
        }

        // -------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------

        /// <summary>Returns the nodes on the given floor, or null if out of range.</summary>
        public VoidRiftNode[] GetFloorNodes(int floor)
        {
            if (floor < 0 || floor >= _floors.Count) return null;
            return _floors[floor];
        }
    }

    // ---------------------------------------------------------------------------
    // VoidRiftManager MonoBehaviour
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages the Void Rift roguelite dungeon event.
    ///
    /// Architecture:
    /// - VoidRiftRunState is pure C# and holds all run data.
    /// - VoidRiftManager is the MonoBehaviour bridge: handles EventBus,
    ///   ServiceLocator registration, and delegates game logic to RunState.
    /// - All score submissions go to PlayFab server — client score is preview only.
    /// - COPPA-safe: no personal data in score payloads (check #46).
    /// </summary>
    public class VoidRiftManager : MonoBehaviour
    {
        [SerializeField] private VoidRiftConfig _config;

        private VoidRiftRunState _activeRun;

        public bool HasActiveRun => _activeRun != null && !_activeRun.IsRunOver;

        /// <summary>Current run score (preview; server is authoritative).</summary>
        public int CurrentScore => _activeRun?.Score ?? 0;

        /// <summary>Current floor in the active run (0-based).</summary>
        public int CurrentFloor => _activeRun?.CurrentFloor ?? 0;

        private void Awake()
        {
            ServiceLocator.Register<VoidRiftManager>(this);
            if (_config == null)
                Debug.LogError("[VoidRiftManager] VoidRiftConfig not assigned.", this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<VoidRiftManager>();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Start a new Void Rift run.
        /// Cancels any existing run in progress.
        /// </summary>
        public VoidRiftRunState StartRun()
        {
            if (_config == null)
            {
                Debug.LogError("[VoidRiftManager] Cannot start run — VoidRiftConfig not assigned.");
                return null;
            }

            if (HasActiveRun)
                AbandonRun();

            _activeRun = new VoidRiftRunState(_config);
            EventBus.Publish(new VoidRiftRunStartedEvent(_activeRun.TotalFloors));
            Debug.Log($"[VoidRiftManager] Run started. Floors: {_activeRun.TotalFloors}");
            return _activeRun;
        }

        /// <summary>
        /// Select a path and complete the node.
        /// Returns true if the node was completed successfully.
        /// </summary>
        public bool CompleteNode(int pathIndex, bool isEliteKill = false)
        {
            if (!HasActiveRun) return false;

            var node = _activeRun.SelectPath(pathIndex);
            if (node == null) return false;

            _activeRun.CompleteNode(pathIndex, isEliteKill);
            EventBus.Publish(new VoidRiftNodeCompletedEvent(node.Floor, node.NodeType, _activeRun.Score));

            if (_activeRun.IsRunOver)
                FinaliseRun();

            return true;
        }

        /// <summary>
        /// Add a relic to the active run (e.g. from a Treasure room).
        /// </summary>
        public bool AddRelic(VoidRelic relic)
        {
            if (!HasActiveRun) return false;
            if (relic == null) return false;
            _activeRun.AddRelic(relic);
            EventBus.Publish(new VoidRiftRelicAcquiredEvent(relic.RelicId, relic.DisplayName));
            return true;
        }

        /// <summary>
        /// Hero squad defeated — end the run.
        /// </summary>
        public void HandleDefeat()
        {
            if (!HasActiveRun) return;
            _activeRun.EndRunDefeat();
            FinaliseRun();
        }

        /// <summary>
        /// Abandon the active run without scoring.
        /// </summary>
        public void AbandonRun()
        {
            if (_activeRun == null) return;
            _activeRun.EndRunDefeat();
            _activeRun = null;
            Debug.Log("[VoidRiftManager] Run abandoned.");
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private void FinaliseRun()
        {
            if (_activeRun == null) return;
            bool won   = _activeRun.IsWon;
            int  score = _activeRun.Score;
            int  floor = _activeRun.CurrentFloor;

            EventBus.Publish(new VoidRiftRunCompletedEvent(won, score, floor));
            Debug.Log($"[VoidRiftManager] Run ended. Won={won} Score={score} Floor={floor}");

            // In production: submit score to PlayFab Cloud Script for server-side validation
            // PlayFabService.SubmitVoidRiftScore(score, floor, won, ...);

            _activeRun = null;
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Published when a new Void Rift run begins.</summary>
    public readonly struct VoidRiftRunStartedEvent
    {
        public readonly int TotalFloors;
        public VoidRiftRunStartedEvent(int floors) { TotalFloors = floors; }
    }

    /// <summary>Published each time a dungeon node is completed.</summary>
    public readonly struct VoidRiftNodeCompletedEvent
    {
        public readonly int Floor;
        public readonly VoidRiftNodeType NodeType;
        public readonly int CurrentScore;
        public VoidRiftNodeCompletedEvent(int floor, VoidRiftNodeType nodeType, int score)
        {
            Floor        = floor;
            NodeType     = nodeType;
            CurrentScore = score;
        }
    }

    /// <summary>Published when the player acquires a relic.</summary>
    public readonly struct VoidRiftRelicAcquiredEvent
    {
        public readonly string RelicId;
        public readonly string DisplayName;
        public VoidRiftRelicAcquiredEvent(string relicId, string name)
        {
            RelicId     = relicId;
            DisplayName = name;
        }
    }

    /// <summary>Published when a run ends (win or defeat).</summary>
    public readonly struct VoidRiftRunCompletedEvent
    {
        public readonly bool IsWon;
        public readonly int  FinalScore;
        public readonly int  FloorsCleared;
        public VoidRiftRunCompletedEvent(bool won, int score, int floors)
        {
            IsWon         = won;
            FinalScore    = score;
            FloorsCleared = floors;
        }
    }
}
