using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Manages city buildings: placement, upgrade queue, timer tracking.
    /// Hard-limits build timers (ENFORCED: max 4h for normal, 8h for Stronghold).
    /// All timer state persisted via PlayFab; this class manages runtime state only.
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        // Timer hard limits — non-negotiable design promise.
        public const int MaxBuildTimeSecondsStronghold = 28800; // 8 hours
        public const int MaxBuildTimeSecondsOther = 14400;      // 4 hours
        public const int FreeQueueSlots = 2;

        private readonly Dictionary<string, PlacedBuilding> _placedBuildings = new(); // placedId → building
        private readonly List<BuildQueueEntry> _buildQueue = new(FreeQueueSlots);
        private readonly HashSet<string> _autoUpgradeQueue = new(); // placedIds that auto-upgrade on completion
        private ResourceManager _resourceManager;

        public IReadOnlyList<BuildQueueEntry> BuildQueue => _buildQueue;
        public IReadOnlyDictionary<string, PlacedBuilding> PlacedBuildings => _placedBuildings;

        public event Action<PlacedBuilding> OnBuildingPlaced;
        public event Action<string, int> OnBuildingUpgradeStarted;  // placedId, targetTier
        public event Action<string> OnBuildingUpgradeCompleted;      // placedId
        public event Action<string> OnBuildingUpgradeCancelled;

        private void Awake()
        {
            _resourceManager = GetComponent<ResourceManager>();
            ServiceLocator.Register<BuildingManager>(this);
        }

        private void OnDestroy() => ServiceLocator.Unregister<BuildingManager>();

        private ResourceManager EnsureResourceManager()
        {
            if (_resourceManager == null)
            {
                _resourceManager = GetComponent<ResourceManager>();
                if (_resourceManager == null)
                    ServiceLocator.TryGet<ResourceManager>(out _resourceManager);
            }
            return _resourceManager;
        }

        private void Update()
        {
            TickBuildQueue();
        }

        /// <summary>
        /// Place a new building at a grid position. Deducts construction cost from resources.
        /// Returns the placed building id, or null on failure.
        /// </summary>
        public string PlaceBuilding(BuildingData data, Vector2Int gridPosition)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            BuildingTierData tier0 = data.GetTier(0);
            if (tier0 == null)
            {
                Debug.LogError($"[BuildingManager] {data.buildingId} has no tier data.");
                return null;
            }

            if (!EnsureResourceManager().CanAfford(tier0.stoneCost, tier0.ironCost, tier0.grainCost, tier0.arcaneEssenceCost))
            {
                EventBus.Publish(new BuildFailedEvent(data.buildingId, "Insufficient resources"));
                return null;
            }

            if (data.isUniquePerCity && IsBuilt(data.buildingId))
            {
                EventBus.Publish(new BuildFailedEvent(data.buildingId, "Already built (unique)"));
                return null;
            }

            EnsureResourceManager().Spend(tier0.stoneCost, tier0.ironCost, tier0.grainCost, tier0.arcaneEssenceCost);

            string placedId = $"{data.buildingId}_{System.Guid.NewGuid():N}";
            var placed = new PlacedBuilding(placedId, data, gridPosition, currentTier: 0);
            _placedBuildings[placedId] = placed;

            OnBuildingPlaced?.Invoke(placed);
            EventBus.Publish(new BuildingPlacedEvent(placedId, data.buildingId, gridPosition));
            return placedId;
        }

        /// <summary>
        /// Start upgrading a placed building to the next tier.
        /// Adds to build queue if a free slot is available.
        /// </summary>
        public bool StartUpgrade(string placedId)
        {
            if (!_placedBuildings.TryGetValue(placedId, out PlacedBuilding placed))
            {
                Debug.LogWarning($"[BuildingManager] StartUpgrade: building {placedId} not found.");
                return false;
            }

            int targetTier = placed.CurrentTier + 1;
            BuildingTierData tierData = placed.Data.GetTier(targetTier);
            if (tierData == null)
            {
                EventBus.Publish(new BuildFailedEvent(placedId, "Already at max tier"));
                return false;
            }

            if (_buildQueue.Count >= GetAvailableQueueSlots())
            {
                EventBus.Publish(new BuildFailedEvent(placedId, "Build queue full"));
                return false;
            }

            if (_buildQueue.Exists(e => e.PlacedId == placedId))
            {
                EventBus.Publish(new BuildFailedEvent(placedId, "Already in queue"));
                return false;
            }

            if (!EnsureResourceManager().CanAfford(tierData.stoneCost, tierData.ironCost, tierData.grainCost, tierData.arcaneEssenceCost))
            {
                EventBus.Publish(new BuildFailedEvent(placedId, "Insufficient resources"));
                return false;
            }

            EnsureResourceManager().Spend(tierData.stoneCost, tierData.ironCost, tierData.grainCost, tierData.arcaneEssenceCost);

            int maxTime = placed.Data.category == BuildingCategory.Core
                ? MaxBuildTimeSecondsStronghold
                : MaxBuildTimeSecondsOther;

            int buildTime = Mathf.Min(tierData.buildTimeSeconds, maxTime);

            var entry = new BuildQueueEntry(placedId, targetTier, buildTime, DateTime.UtcNow);
            _buildQueue.Add(entry);

            OnBuildingUpgradeStarted?.Invoke(placedId, targetTier);
            EventBus.Publish(new BuildingUpgradeStartedEvent(placedId, targetTier, buildTime));
            return true;
        }

        /// <summary>
        /// Apply a speedup item to the first matching queue entry.
        /// speedupSeconds: seconds to reduce from remaining time.
        /// </summary>
        public void ApplySpeedup(string placedId, int speedupSeconds)
        {
            BuildQueueEntry entry = _buildQueue.Find(e => e.PlacedId == placedId);
            if (entry == null) return;
            entry.RemainingSeconds = Mathf.Max(0, entry.RemainingSeconds - speedupSeconds);
            EventBus.Publish(new SpeedupAppliedEvent(placedId, speedupSeconds, entry.RemainingSeconds));
        }

        /// <summary>
        /// Cancel an in-progress upgrade. Removes from queue but does NOT refund resources.
        /// </summary>
        public bool CancelUpgrade(string placedId)
        {
            int idx = _buildQueue.FindIndex(e => e.PlacedId == placedId);
            if (idx < 0) return false;
            _buildQueue.RemoveAt(idx);
            OnBuildingUpgradeCancelled?.Invoke(placedId);
            EventBus.Publish(new BuildingUpgradeCancelledEvent(placedId));
            return true;
        }

        private void TickBuildQueue()
        {
            for (int i = _buildQueue.Count - 1; i >= 0; i--)
            {
                BuildQueueEntry entry = _buildQueue[i];
                entry.RemainingSeconds -= Time.deltaTime;
                if (entry.RemainingSeconds <= 0)
                    CompleteBuild(entry, i);
            }
        }

        private void CompleteBuild(BuildQueueEntry entry, int queueIndex)
        {
            _buildQueue.RemoveAt(queueIndex);
            if (!_placedBuildings.TryGetValue(entry.PlacedId, out PlacedBuilding placed)) return;
            placed.CurrentTier = entry.TargetTier;
            OnBuildingUpgradeCompleted?.Invoke(entry.PlacedId);
            EventBus.Publish(new BuildingUpgradeCompletedEvent(entry.PlacedId, entry.TargetTier));

            // P&C: Auto-upgrade — immediately start next tier if queued
            if (_autoUpgradeQueue.Contains(entry.PlacedId))
            {
                _autoUpgradeQueue.Remove(entry.PlacedId);
                bool started = StartUpgrade(entry.PlacedId);
                if (started)
                    Debug.Log($"[BuildingManager] Auto-upgrade started for {entry.PlacedId} to tier {entry.TargetTier + 1}.");
            }
        }

        /// <summary>P&C: Toggle auto-upgrade for a building. When current upgrade finishes, next starts automatically.</summary>
        public bool ToggleAutoUpgrade(string placedId)
        {
            if (_autoUpgradeQueue.Contains(placedId))
            {
                _autoUpgradeQueue.Remove(placedId);
                EventBus.Publish(new AutoUpgradeToggledEvent(placedId, false));
                return false;
            }
            _autoUpgradeQueue.Add(placedId);
            EventBus.Publish(new AutoUpgradeToggledEvent(placedId, true));
            return true;
        }

        /// <summary>Check if a building has auto-upgrade enabled.</summary>
        public bool IsAutoUpgradeEnabled(string placedId) => _autoUpgradeQueue.Contains(placedId);

        /// <summary>
        /// P&C: Demolish a placed building. Removes from map. Cannot demolish Stronghold.
        /// Does NOT refund resources (P&C style — demolish is free but irreversible).
        /// </summary>
        public bool DemolishBuilding(string placedId)
        {
            if (!_placedBuildings.TryGetValue(placedId, out PlacedBuilding placed))
                return false;
            if (placed.Data != null && placed.Data.buildingId == "stronghold")
                return false; // Cannot demolish Stronghold

            // Cancel any active upgrade first
            CancelUpgrade(placedId);

            _placedBuildings.Remove(placedId);
            EventBus.Publish(new BuildingDemolishedEvent(placedId, placed.Data?.buildingId ?? ""));
            return true;
        }

        private int GetAvailableQueueSlots() => FreeQueueSlots; // Extended to 3 via season progression (not purchase)

        private bool IsBuilt(string buildingId)
        {
            // Avoid LINQ in potentially hot path — use explicit foreach.
            foreach (PlacedBuilding b in _placedBuildings.Values)
            {
                if (b.Data.buildingId == buildingId) return true;
            }
            return false;
        }
    }

    public class PlacedBuilding
    {
        public string PlacedId { get; }
        public BuildingData Data { get; }
        public Vector2Int GridPosition { get; }
        public int CurrentTier { get; set; }

        public PlacedBuilding(string id, BuildingData data, Vector2Int pos, int currentTier)
        {
            PlacedId = id; Data = data; GridPosition = pos; CurrentTier = currentTier;
        }
    }

    public class BuildQueueEntry
    {
        public string PlacedId { get; }
        public int TargetTier { get; }
        public float RemainingSeconds { get; set; }
        public DateTime StartTime { get; }

        public BuildQueueEntry(string id, int tier, int durationSeconds, DateTime start)
        {
            PlacedId = id; TargetTier = tier; RemainingSeconds = durationSeconds; StartTime = start;
        }
    }

    // --- Events ---
    public readonly struct BuildingPlacedEvent { public readonly string PlacedId; public readonly string BuildingId; public readonly Vector2Int Position; public BuildingPlacedEvent(string pid, string bid, Vector2Int pos) { PlacedId = pid; BuildingId = bid; Position = pos; } }
    public readonly struct BuildingUpgradeStartedEvent { public readonly string PlacedId; public readonly int TargetTier; public readonly int BuildTimeSeconds; public BuildingUpgradeStartedEvent(string id, int t, int s) { PlacedId = id; TargetTier = t; BuildTimeSeconds = s; } }
    public readonly struct BuildingUpgradeCompletedEvent { public readonly string PlacedId; public readonly int NewTier; public BuildingUpgradeCompletedEvent(string id, int t) { PlacedId = id; NewTier = t; } }
    public readonly struct BuildFailedEvent { public readonly string Target; public readonly string Reason; public BuildFailedEvent(string t, string r) { Target = t; Reason = r; } }
    public readonly struct SpeedupAppliedEvent { public readonly string PlacedId; public readonly int SecondsApplied; public readonly float RemainingSeconds; public SpeedupAppliedEvent(string id, int s, float r) { PlacedId = id; SecondsApplied = s; RemainingSeconds = r; } }
    public readonly struct BuildingUpgradeCancelledEvent { public readonly string PlacedId; public BuildingUpgradeCancelledEvent(string id) { PlacedId = id; } }
    public readonly struct BuildingDemolishedEvent { public readonly string PlacedId; public readonly string BuildingId; public BuildingDemolishedEvent(string pid, string bid) { PlacedId = pid; BuildingId = bid; } }
    public readonly struct SpeedupRequestedEvent { public readonly string PlacedId; public readonly int GemCost; public readonly float RemainingSeconds; public SpeedupRequestedEvent(string id, int g, float r) { PlacedId = id; GemCost = g; RemainingSeconds = r; } }
    public readonly struct AllianceHelpRequestedEvent { public readonly string PlacedId; public AllianceHelpRequestedEvent(string id) { PlacedId = id; } }
    public readonly struct AutoUpgradeToggledEvent { public readonly string PlacedId; public readonly bool Enabled; public AutoUpgradeToggledEvent(string id, bool e) { PlacedId = id; Enabled = e; } }
}
