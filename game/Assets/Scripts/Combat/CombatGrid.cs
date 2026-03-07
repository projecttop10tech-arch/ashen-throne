using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Manages the 7x5 combat grid: tile state, unit positions, terrain effects.
    /// Column 0-2 = player team zone (front/mid/back). Column 3 = neutral. Column 4-6 = enemy zone (front/mid/back).
    /// Row 0-4 = battle rows (5 rows).
    /// </summary>
    public class CombatGrid : MonoBehaviour
    {
        public const int Columns = 7;
        public const int Rows = 5;

        private CombatTile[,] _grid;
        private readonly Dictionary<int, GridPosition> _unitPositions = new(); // heroInstanceId → position

        public event System.Action<GridPosition, TileType> OnTileTypeChanged;
        public event System.Action<int, GridPosition, GridPosition> OnUnitMoved; // heroId, from, to

        private void Awake()
        {
            EnsureGridInitialized();
        }

        private void EnsureGridInitialized()
        {
            if (_grid != null) return;
            _grid = new CombatTile[Columns, Rows];
            for (int c = 0; c < Columns; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    _grid[c, r] = new CombatTile(new GridPosition(c, r), TileType.Normal);
                }
            }
        }

        /// <summary>
        /// Returns the tile at the given grid position. Null if out of bounds.
        /// </summary>
        public CombatTile GetTile(GridPosition pos)
        {
            EnsureGridInitialized();
            if (!IsInBounds(pos)) return null;
            return _grid[pos.Column, pos.Row];
        }

        /// <summary>
        /// Set terrain type for a tile and fire the change event.
        /// </summary>
        public void SetTileType(GridPosition pos, TileType type)
        {
            EnsureGridInitialized();
            if (!IsInBounds(pos))
            {
                Debug.LogWarning($"[CombatGrid] SetTileType out of bounds: {pos}");
                return;
            }
            _grid[pos.Column, pos.Row].TileType = type;
            OnTileTypeChanged?.Invoke(pos, type);
            EventBus.Publish(new TileTypeChangedEvent(pos, type));
        }

        /// <summary>
        /// Place a unit on the grid. Fails if position is occupied.
        /// </summary>
        public bool PlaceUnit(int heroInstanceId, GridPosition pos)
        {
            EnsureGridInitialized();
            if (!IsInBounds(pos)) return false;
            if (_grid[pos.Column, pos.Row].OccupantId.HasValue)
            {
                Debug.LogWarning($"[CombatGrid] Cannot place unit {heroInstanceId} at {pos}: already occupied by {_grid[pos.Column, pos.Row].OccupantId}");
                return false;
            }

            _grid[pos.Column, pos.Row].OccupantId = heroInstanceId;
            _unitPositions[heroInstanceId] = pos;
            return true;
        }

        /// <summary>
        /// Move a unit to a new position. Clears old tile, occupies new tile.
        /// </summary>
        public bool MoveUnit(int heroInstanceId, GridPosition to)
        {
            EnsureGridInitialized();
            if (!_unitPositions.TryGetValue(heroInstanceId, out GridPosition from)) return false;
            if (!IsInBounds(to)) return false;
            if (_grid[to.Column, to.Row].OccupantId.HasValue) return false;

            _grid[from.Column, from.Row].OccupantId = null;
            _grid[to.Column, to.Row].OccupantId = heroInstanceId;
            _unitPositions[heroInstanceId] = to;

            OnUnitMoved?.Invoke(heroInstanceId, from, to);
            EventBus.Publish(new UnitMovedEvent(heroInstanceId, from, to));
            return true;
        }

        /// <summary>
        /// Remove a unit from the grid (on death or battle end).
        /// </summary>
        public void RemoveUnit(int heroInstanceId)
        {
            EnsureGridInitialized();
            if (!_unitPositions.TryGetValue(heroInstanceId, out GridPosition pos)) return;
            _grid[pos.Column, pos.Row].OccupantId = null;
            _unitPositions.Remove(heroInstanceId);
        }

        /// <summary>
        /// Returns all occupied positions within splashRadius of center (Manhattan distance).
        /// </summary>
        public List<GridPosition> GetPositionsInRadius(GridPosition center, int radius)
        {
            var result = new List<GridPosition>();
            for (int c = center.Column - radius; c <= center.Column + radius; c++)
            {
                for (int r = center.Row - radius; r <= center.Row + radius; r++)
                {
                    var pos = new GridPosition(c, r);
                    if (IsInBounds(pos) && ManhattanDistance(center, pos) <= radius)
                        result.Add(pos);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the current position of a unit, or null if not found.
        /// </summary>
        public GridPosition? GetUnitPosition(int heroInstanceId) =>
            _unitPositions.TryGetValue(heroInstanceId, out GridPosition pos) ? pos : (GridPosition?)null;

        public bool IsInBounds(GridPosition pos) =>
            pos.Column >= 0 && pos.Column < Columns && pos.Row >= 0 && pos.Row < Rows;

        /// <summary>Player zone: columns 0-2 (3 depth positions: front, mid, back).</summary>
        public bool IsPlayerZone(GridPosition pos) => pos.Column >= 0 && pos.Column <= 2;
        /// <summary>Enemy zone: columns 4-6 (3 depth positions: front, mid, back).</summary>
        public bool IsEnemyZone(GridPosition pos) => pos.Column >= 4 && pos.Column <= 6;
        /// <summary>Neutral center column. Contested tiles may grant bonuses.</summary>
        public bool IsNeutralZone(GridPosition pos) => pos.Column == 3;

        private int ManhattanDistance(GridPosition a, GridPosition b) =>
            Mathf.Abs(a.Column - b.Column) + Mathf.Abs(a.Row - b.Row);

        /// <summary>
        /// Apply per-turn tile effects (burn DOT, poison, etc.) to all units on affected tiles.
        /// Called by TurnManager at end of each turn.
        /// </summary>
        public List<TileTickEffect> GetTickEffects()
        {
            EnsureGridInitialized();
            var effects = new List<TileTickEffect>();
            foreach (var (id, pos) in _unitPositions)
            {
                CombatTile tile = _grid[pos.Column, pos.Row];
                if (tile.TileType == TileType.Fire)
                    effects.Add(new TileTickEffect(id, 50, DamageType.Fire));
            }
            return effects;
        }
    }

    [System.Serializable]
    public class CombatTile
    {
        public GridPosition Position;
        public TileType TileType;
        public int? OccupantId;
        /// <summary>Remaining turns until this tile reverts to Normal. 0 = permanent.</summary>
        public int TemporaryDurationTurns;

        public CombatTile(GridPosition pos, TileType type) { Position = pos; TileType = type; }
    }

    [System.Serializable]
    public struct GridPosition : System.IEquatable<GridPosition>
    {
        public int Column;
        public int Row;

        public GridPosition(int column, int row) { Column = column; Row = row; }
        public bool Equals(GridPosition other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is GridPosition gp && Equals(gp);
        public override int GetHashCode() => Column * 100 + Row;
        public override string ToString() => $"({Column},{Row})";
    }

    public enum TileType
    {
        Normal,
        HighGround,     // +25% ranged damage
        Water,          // Movement +1 action cost
        Fire,           // 50 fire DOT per turn
        FortressWall,   // Blocks movement/ranged
        ArcaneLayLine,  // -1 energy cost this turn
        ShadowVeil      // 30% miss chance
    }

    public enum DamageType { Physical, Fire, Ice, Lightning, Shadow, Holy, Arcane, Nature, True }

    public readonly struct TileTickEffect
    {
        public readonly int TargetHeroId;
        public readonly int Damage;
        public readonly DamageType Type;
        public TileTickEffect(int id, int dmg, DamageType t) { TargetHeroId = id; Damage = dmg; Type = t; }
    }

    // --- Events ---
    public readonly struct TileTypeChangedEvent { public readonly GridPosition Position; public readonly TileType NewType; public TileTypeChangedEvent(GridPosition p, TileType t) { Position = p; NewType = t; } }
    public readonly struct UnitMovedEvent { public readonly int HeroId; public readonly GridPosition From; public readonly GridPosition To; public UnitMovedEvent(int id, GridPosition f, GridPosition t) { HeroId = id; From = f; To = t; } }
}
