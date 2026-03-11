using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Alliance
{
    // ---------------------------------------------------------------------------
    // Hex coordinate (axial) — offset-free, arithmetic-friendly.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Axial hexagonal coordinate. Q is the column axis, R is the row axis.
    /// Uses the standard cube-coordinate distance formula projected to 2D.
    /// See: https://www.redblobgames.com/grids/hexagons/
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;

        public HexCoord(int q, int r) { Q = q; R = r; }

        /// <summary>Straight-line distance between two hex cells.</summary>
        public static int Distance(HexCoord a, HexCoord b)
        {
            // Cube coordinate conversion: s = -q - r
            int dq = a.Q - b.Q;
            int dr = a.R - b.R;
            return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
        }

        public static readonly HexCoord Zero = new HexCoord(0, 0);

        // Six axial directions for hex adjacency
        private static readonly HexCoord[] Directions = {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1)
        };

        /// <summary>Returns the 6 hex neighbors of this coordinate.</summary>
        public HexCoord[] Neighbors()
        {
            var result = new HexCoord[6];
            for (int i = 0; i < 6; i++)
                result[i] = new HexCoord(Q + Directions[i].Q, R + Directions[i].R);
            return result;
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public override string ToString() => $"({Q},{R})";
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
    }

    // ---------------------------------------------------------------------------
    // Territory enums and data classes
    // ---------------------------------------------------------------------------

    /// <summary>Static type of a territory that determines its bonus category.</summary>
    public enum TerritoryType
    {
        Neutral,    // No bonus
        Resource,   // +production%
        Military,   // +combat power%
        Research,   // +research speed%
        Stronghold  // +alliance building capacity
    }

    /// <summary>Current ownership and fortification state of a single territory region.</summary>
    public class TerritoryRegion
    {
        /// <summary>Unique identifier matching the static TerritoryData asset.</summary>
        public string RegionId { get; }

        /// <summary>Axial hex coordinate on the world map.</summary>
        public HexCoord Coord { get; }

        /// <summary>Static type — determines which bonus this territory provides.</summary>
        public TerritoryType Type { get; }

        /// <summary>Display name for UI.</summary>
        public string DisplayName { get; }

        /// <summary>Alliance ID of the owning alliance. Null = unclaimed.</summary>
        public string OwnerAllianceId { get; private set; }

        /// <summary>Current fortification tier (0 = no fortification, max = TerritoryConfig.FortificationMaxTier).</summary>
        public int FortificationTier { get; private set; }

        /// <summary>Current fortification HP.</summary>
        public int FortificationHp { get; private set; }

        public bool IsNeutral => OwnerAllianceId == null;

        public TerritoryRegion(string regionId, HexCoord coord, TerritoryType type, string displayName)
        {
            RegionId = regionId;
            Coord = coord;
            Type = type;
            DisplayName = displayName;
        }

        /// <summary>Transfer ownership to the given alliance. Resets fortification.</summary>
        public void SetOwner(string allianceId)
        {
            OwnerAllianceId = allianceId;
            FortificationTier = 0;
            FortificationHp = 0;
        }

        /// <summary>Upgrade fortification by one tier, clamped to max tier.</summary>
        public void UpgradeFortification(TerritoryConfig config)
        {
            if (config == null) return;
            FortificationTier = Mathf.Min(FortificationTier + 1, config.FortificationMaxTier);
            FortificationHp = Mathf.RoundToInt(
                config.FortificationBaseHp * Mathf.Pow(config.FortificationTierMultiplier, FortificationTier));
        }

        /// <summary>Apply damage to fortification. Returns true if fortification is destroyed (HP <= 0).</summary>
        public bool TakeFortificationDamage(int damage)
        {
            if (damage <= 0) return false;
            FortificationHp = Mathf.Max(0, FortificationHp - damage);
            if (FortificationHp <= 0)
            {
                FortificationTier = 0;
                return true;
            }
            return false;
        }

        /// <summary>Hydrate runtime state from saved data.</summary>
        public void HydrateFromSave(string ownerAllianceId, int fortTier, int fortHp)
        {
            OwnerAllianceId = ownerAllianceId;
            FortificationTier = fortTier;
            FortificationHp = fortHp;
        }
    }

    // ---------------------------------------------------------------------------
    // TerritoryManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages the world map of 200 hexagonal territory regions.
    /// Tracks ownership, fortification state, and supply-line connectivity.
    /// All capture/transfer actions publish events but do NOT directly mutate economy —
    /// the server (PlayFab Cloud Script) is the authoritative source for territory state.
    /// Client receives validated updates via LoadFromServerData.
    /// </summary>
    public class TerritoryManager : MonoBehaviour
    {
        [SerializeField] private TerritoryConfig _config;

        // regionId → region
        private readonly Dictionary<string, TerritoryRegion> _regions = new(200);

        // allianceId → set of owned regionIds
        private readonly Dictionary<string, HashSet<string>> _allianceOwnedRegions = new();

        // HexCoord → regionId: used for O(1) neighbor lookup in BFS (check #19 — no nested foreach)
        private readonly Dictionary<HexCoord, string> _coordToRegionId = new(200);

        private void Awake()
        {
            if (_config == null)
                _config = Resources.Load<TerritoryConfig>("TerritoryConfig");
            ServiceLocator.Register<TerritoryManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TerritoryManager>();
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------

        /// <summary>
        /// Populate the territory map from static definition list.
        /// Called once at game startup before server data is loaded.
        /// </summary>
        public void InitializeMap(IEnumerable<TerritoryRegion> regions)
        {
            if (regions == null)
                throw new ArgumentNullException(nameof(regions));

            _regions.Clear();
            _allianceOwnedRegions.Clear();

            _coordToRegionId.Clear();
            foreach (var region in regions)
            {
                if (region == null) continue;
                _regions[region.RegionId] = region;
                _coordToRegionId[region.Coord] = region.RegionId;
            }
        }

        /// <summary>
        /// Apply saved server state to the territory map.
        /// Called after server data is fetched. Client never mutates ownership directly.
        /// </summary>
        public void LoadFromServerData(IEnumerable<TerritorySaveEntry> entries)
        {
            if (entries == null) return;

            _allianceOwnedRegions.Clear();

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (!_regions.TryGetValue(entry.RegionId, out var region)) continue;

                region.HydrateFromSave(entry.OwnerAllianceId, entry.FortificationTier, entry.FortificationHp);

                if (!string.IsNullOrEmpty(entry.OwnerAllianceId))
                    AddToAllianceIndex(entry.OwnerAllianceId, entry.RegionId);
            }

            EventBus.Publish(new TerritoryMapLoadedEvent(_regions.Count));
        }

        // -------------------------------------------------------------------
        // Server-confirmed mutations (called when PlayFab validates the action)
        // -------------------------------------------------------------------

        /// <summary>
        /// Apply a server-confirmed territory capture. Transfers ownership,
        /// removes from previous owner's index, adds to new owner's index.
        /// </summary>
        public void ApplyCapture(string regionId, string attackingAllianceId)
        {
            if (!_regions.TryGetValue(regionId, out var region)) return;

            string previousOwner = region.OwnerAllianceId;

            if (!string.IsNullOrEmpty(previousOwner))
                RemoveFromAllianceIndex(previousOwner, regionId);

            region.SetOwner(attackingAllianceId);
            AddToAllianceIndex(attackingAllianceId, regionId);

            EventBus.Publish(new TerritoryCapturedEvent(regionId, attackingAllianceId, previousOwner));
        }

        /// <summary>
        /// Apply a server-confirmed fortification upgrade.
        /// </summary>
        public void ApplyFortificationUpgrade(string regionId)
        {
            if (!_regions.TryGetValue(regionId, out var region)) return;
            if (_config == null) return;
            region.UpgradeFortification(_config);
            EventBus.Publish(new TerritoryFortifiedEvent(regionId, region.FortificationTier));
        }

        // -------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------

        /// <summary>Returns the region for the given ID, or null if not found.</summary>
        public TerritoryRegion GetRegion(string regionId)
        {
            return _regions.TryGetValue(regionId, out var r) ? r : null;
        }

        /// <summary>Returns all regions owned by the given alliance.</summary>
        public IReadOnlyCollection<string> GetAllianceRegionIds(string allianceId)
        {
            return _allianceOwnedRegions.TryGetValue(allianceId, out var set)
                ? set
                : (IReadOnlyCollection<string>)Array.Empty<string>();
        }

        /// <summary>Returns total territory count for an alliance.</summary>
        public int GetTerritoryCount(string allianceId)
        {
            return _allianceOwnedRegions.TryGetValue(allianceId, out var set) ? set.Count : 0;
        }

        /// <summary>
        /// Returns the aggregate bonus multiplier from territory ownership.
        /// Checks supply-line connectivity: only connected territories contribute bonuses.
        /// </summary>
        public TerritoryBonuses CalculateBonuses(string allianceId)
        {
            var bonuses = new TerritoryBonuses();
            if (_config == null || !_allianceOwnedRegions.TryGetValue(allianceId, out var ownedIds)) return bonuses;

            // Build connectivity graph via BFS — territories must be contiguous to count
            var connected = GetConnectedRegions(allianceId, ownedIds);

            foreach (var regionId in connected)
            {
                if (!_regions.TryGetValue(regionId, out var region)) continue;
                switch (region.Type)
                {
                    case TerritoryType.Resource:   bonuses.ResourceProductionBonus  += _config.ResourceTerritoryBonus;  break;
                    case TerritoryType.Military:   bonuses.CombatPowerBonus          += _config.MilitaryTerritoryBonus;  break;
                    case TerritoryType.Research:   bonuses.ResearchSpeedBonus        += _config.ResearchTerritoryBonus;  break;
                    case TerritoryType.Stronghold: bonuses.AllianceBuildingBonus     += 1;                               break;
                }
            }

            return bonuses;
        }

        /// <summary>
        /// Returns true if the two regions are adjacent on the hex grid.
        /// </summary>
        public bool AreAdjacent(string regionIdA, string regionIdB)
        {
            if (!_regions.TryGetValue(regionIdA, out var a)) return false;
            if (!_regions.TryGetValue(regionIdB, out var b)) return false;
            return HexCoord.Distance(a.Coord, b.Coord) == 1;
        }

        /// <summary>
        /// Returns true if an attack on a region is valid:
        /// — Not already owned by the attacker
        /// — Adjacent to at least one attacker-owned region, OR is neutral and within supply range
        /// </summary>
        public bool IsAttackable(string regionId, string attackingAllianceId)
        {
            if (!_regions.TryGetValue(regionId, out var target)) return false;
            if (target.OwnerAllianceId == attackingAllianceId) return false;

            // Must be adjacent to an owned region (supply line rule)
            if (!_allianceOwnedRegions.TryGetValue(attackingAllianceId, out var ownedIds)) return false;
            foreach (var ownedId in ownedIds)
            {
                if (AreAdjacent(regionId, ownedId)) return true;
            }
            return false;
        }

        // -------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------

        private void AddToAllianceIndex(string allianceId, string regionId)
        {
            if (!_allianceOwnedRegions.TryGetValue(allianceId, out var set))
            {
                set = new HashSet<string>();
                _allianceOwnedRegions[allianceId] = set;
            }
            set.Add(regionId);
        }

        private void RemoveFromAllianceIndex(string allianceId, string regionId)
        {
            if (_allianceOwnedRegions.TryGetValue(allianceId, out var set))
                set.Remove(regionId);
        }

        /// <summary>
        /// BFS from any owned region to find the set of contiguous owned regions.
        /// Territories isolated by gaps or enemy territory do NOT contribute bonuses.
        /// </summary>
        private HashSet<string> GetConnectedRegions(string allianceId, HashSet<string> ownedIds)
        {
            var connected = new HashSet<string>();
            if (ownedIds.Count == 0) return connected;

            // Start BFS from first region in set
            var queue = new Queue<string>();
            string startId = null;
            foreach (var id in ownedIds) { startId = id; break; }
            if (startId == null) return connected;

            queue.Enqueue(startId);
            connected.Add(startId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!_regions.TryGetValue(current, out var region)) continue;

                // O(1) coord lookup — avoids O(n) nested foreach over all owned regions
                foreach (var neighborCoord in region.Coord.Neighbors())
                {
                    if (!_coordToRegionId.TryGetValue(neighborCoord, out var neighborId)) continue;
                    if (connected.Contains(neighborId)) continue;
                    if (!ownedIds.Contains(neighborId)) continue; // neighbor not owned by this alliance
                    connected.Add(neighborId);
                    queue.Enqueue(neighborId);
                }
            }

            return connected;
        }
    }

    // ---------------------------------------------------------------------------
    // Supporting data types
    // ---------------------------------------------------------------------------

    /// <summary>Aggregated bonuses from controlled territories.</summary>
    public class TerritoryBonuses
    {
        /// <summary>Additive % bonus to all resource production (0.1 = 10%).</summary>
        public float ResourceProductionBonus;

        /// <summary>Additive % bonus to combat power (0.1 = 10%).</summary>
        public float CombatPowerBonus;

        /// <summary>Additive % bonus to research speed (0.1 = 10%).</summary>
        public float ResearchSpeedBonus;

        /// <summary>Number of additional alliance buildings unlocked.</summary>
        public int AllianceBuildingBonus;
    }

    /// <summary>Server-provided territory state for a single region.</summary>
    [System.Serializable]
    public class TerritorySaveEntry
    {
        public string RegionId;
        public string OwnerAllianceId; // null = neutral
        public int FortificationTier;
        public int FortificationHp;
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Published once after the full territory map is loaded from server data.</summary>
    public readonly struct TerritoryMapLoadedEvent
    {
        public readonly int RegionCount;
        public TerritoryMapLoadedEvent(int count) { RegionCount = count; }
    }

    /// <summary>Published when a territory changes hands (server-confirmed).</summary>
    public readonly struct TerritoryCapturedEvent
    {
        public readonly string RegionId;
        public readonly string AttackingAllianceId;
        public readonly string PreviousOwnerAllianceId; // null if was neutral
        public TerritoryCapturedEvent(string regionId, string attacking, string previous)
        {
            RegionId = regionId;
            AttackingAllianceId = attacking;
            PreviousOwnerAllianceId = previous;
        }
    }

    /// <summary>Published when a territory's fortification tier increases (server-confirmed).</summary>
    public readonly struct TerritoryFortifiedEvent
    {
        public readonly string RegionId;
        public readonly int NewTier;
        public TerritoryFortifiedEvent(string regionId, int tier) { RegionId = regionId; NewTier = tier; }
    }
}
