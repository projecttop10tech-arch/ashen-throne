using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AshenThrone.Combat;
using Object = UnityEngine.Object;

namespace AshenThrone.Tests.Combat
{
    /// <summary>
    /// Unit tests for CombatGrid: tile access, unit placement, movement, zones, tick effects.
    /// </summary>
    [TestFixture]
    public class CombatGridTests
    {
        private GameObject _go;
        private CombatGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatGridTest");
            _grid = _go.AddComponent<CombatGrid>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // -------------------------------------------------------------------
        // Grid dimensions
        // -------------------------------------------------------------------

        [Test]
        public void GridDimensions_Are7x5()
        {
            Assert.AreEqual(7, CombatGrid.Columns);
            Assert.AreEqual(5, CombatGrid.Rows);
        }

        // -------------------------------------------------------------------
        // GetTile
        // -------------------------------------------------------------------

        [Test]
        public void GetTile_ValidPosition_ReturnsNonNull()
        {
            var tile = _grid.GetTile(new GridPosition(0, 0));
            Assert.IsNotNull(tile);
        }

        [Test]
        public void GetTile_OutOfBounds_ReturnsNull()
        {
            Assert.IsNull(_grid.GetTile(new GridPosition(-1, 0)));
            Assert.IsNull(_grid.GetTile(new GridPosition(7, 0)));
            Assert.IsNull(_grid.GetTile(new GridPosition(0, -1)));
            Assert.IsNull(_grid.GetTile(new GridPosition(0, 5)));
        }

        [Test]
        public void GetTile_AllPositions_DefaultToNormal()
        {
            for (int c = 0; c < CombatGrid.Columns; c++)
            for (int r = 0; r < CombatGrid.Rows; r++)
            {
                var tile = _grid.GetTile(new GridPosition(c, r));
                Assert.AreEqual(TileType.Normal, tile.TileType, $"Tile ({c},{r}) not Normal");
            }
        }

        // -------------------------------------------------------------------
        // SetTileType
        // -------------------------------------------------------------------

        [Test]
        public void SetTileType_ChangesType()
        {
            var pos = new GridPosition(3, 2);
            _grid.SetTileType(pos, TileType.Fire);
            Assert.AreEqual(TileType.Fire, _grid.GetTile(pos).TileType);
        }

        [Test]
        public void SetTileType_OutOfBounds_LogsWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("out of bounds"));
            _grid.SetTileType(new GridPosition(99, 99), TileType.Water);
        }

        // -------------------------------------------------------------------
        // PlaceUnit
        // -------------------------------------------------------------------

        [Test]
        public void PlaceUnit_ValidEmpty_ReturnsTrue()
        {
            bool result = _grid.PlaceUnit(1, new GridPosition(0, 0));
            Assert.IsTrue(result);
        }

        [Test]
        public void PlaceUnit_TracksPosition()
        {
            _grid.PlaceUnit(42, new GridPosition(2, 3));
            var pos = _grid.GetUnitPosition(42);
            Assert.IsNotNull(pos);
            Assert.AreEqual(new GridPosition(2, 3), pos.Value);
        }

        [Test]
        public void PlaceUnit_OccupiedTile_ReturnsFalse()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already occupied"));
            bool result = _grid.PlaceUnit(2, new GridPosition(0, 0));
            Assert.IsFalse(result);
        }

        [Test]
        public void PlaceUnit_OutOfBounds_ReturnsFalse()
        {
            bool result = _grid.PlaceUnit(1, new GridPosition(-1, -1));
            Assert.IsFalse(result);
        }

        // -------------------------------------------------------------------
        // MoveUnit
        // -------------------------------------------------------------------

        [Test]
        public void MoveUnit_ValidMove_ReturnsTrue()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            bool result = _grid.MoveUnit(1, new GridPosition(1, 0));
            Assert.IsTrue(result);
        }

        [Test]
        public void MoveUnit_UpdatesPosition()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            _grid.MoveUnit(1, new GridPosition(1, 1));
            var pos = _grid.GetUnitPosition(1);
            Assert.AreEqual(new GridPosition(1, 1), pos.Value);
        }

        [Test]
        public void MoveUnit_ClearsOldTile()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            _grid.MoveUnit(1, new GridPosition(1, 0));
            var oldTile = _grid.GetTile(new GridPosition(0, 0));
            Assert.IsFalse(oldTile.OccupantId.HasValue);
        }

        [Test]
        public void MoveUnit_Occupied_ReturnsFalse()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            _grid.PlaceUnit(2, new GridPosition(1, 0));
            bool result = _grid.MoveUnit(1, new GridPosition(1, 0));
            Assert.IsFalse(result);
        }

        [Test]
        public void MoveUnit_UnknownUnit_ReturnsFalse()
        {
            bool result = _grid.MoveUnit(999, new GridPosition(1, 0));
            Assert.IsFalse(result);
        }

        [Test]
        public void MoveUnit_OutOfBounds_ReturnsFalse()
        {
            _grid.PlaceUnit(1, new GridPosition(0, 0));
            bool result = _grid.MoveUnit(1, new GridPosition(99, 99));
            Assert.IsFalse(result);
        }

        // -------------------------------------------------------------------
        // RemoveUnit
        // -------------------------------------------------------------------

        [Test]
        public void RemoveUnit_ClearsTileAndPosition()
        {
            _grid.PlaceUnit(1, new GridPosition(2, 2));
            _grid.RemoveUnit(1);
            Assert.IsNull(_grid.GetUnitPosition(1));
            Assert.IsFalse(_grid.GetTile(new GridPosition(2, 2)).OccupantId.HasValue);
        }

        [Test]
        public void RemoveUnit_UnknownId_IsNoOp()
        {
            Assert.DoesNotThrow(() => _grid.RemoveUnit(999));
        }

        // -------------------------------------------------------------------
        // GetUnitPosition
        // -------------------------------------------------------------------

        [Test]
        public void GetUnitPosition_NotFound_ReturnsNull()
        {
            Assert.IsNull(_grid.GetUnitPosition(123));
        }

        // -------------------------------------------------------------------
        // Zone queries
        // -------------------------------------------------------------------

        [Test]
        public void IsPlayerZone_Columns0To2()
        {
            Assert.IsTrue(_grid.IsPlayerZone(new GridPosition(0, 0)));
            Assert.IsTrue(_grid.IsPlayerZone(new GridPosition(2, 4)));
            Assert.IsFalse(_grid.IsPlayerZone(new GridPosition(3, 0)));
        }

        [Test]
        public void IsEnemyZone_Columns4To6()
        {
            Assert.IsTrue(_grid.IsEnemyZone(new GridPosition(4, 0)));
            Assert.IsTrue(_grid.IsEnemyZone(new GridPosition(6, 4)));
            Assert.IsFalse(_grid.IsEnemyZone(new GridPosition(3, 0)));
        }

        [Test]
        public void IsNeutralZone_Column3Only()
        {
            Assert.IsTrue(_grid.IsNeutralZone(new GridPosition(3, 0)));
            Assert.IsFalse(_grid.IsNeutralZone(new GridPosition(2, 0)));
            Assert.IsFalse(_grid.IsNeutralZone(new GridPosition(4, 0)));
        }

        // -------------------------------------------------------------------
        // IsInBounds
        // -------------------------------------------------------------------

        [Test]
        public void IsInBounds_ValidPositions()
        {
            Assert.IsTrue(_grid.IsInBounds(new GridPosition(0, 0)));
            Assert.IsTrue(_grid.IsInBounds(new GridPosition(6, 4)));
        }

        [Test]
        public void IsInBounds_InvalidPositions()
        {
            Assert.IsFalse(_grid.IsInBounds(new GridPosition(-1, 0)));
            Assert.IsFalse(_grid.IsInBounds(new GridPosition(7, 0)));
            Assert.IsFalse(_grid.IsInBounds(new GridPosition(0, 5)));
        }

        // -------------------------------------------------------------------
        // GetPositionsInRadius
        // -------------------------------------------------------------------

        [Test]
        public void GetPositionsInRadius_Center_Radius0_ReturnsOnlyCenter()
        {
            var results = _grid.GetPositionsInRadius(new GridPosition(3, 2), 0);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(new GridPosition(3, 2), results[0]);
        }

        [Test]
        public void GetPositionsInRadius_Center_Radius1_Returns5()
        {
            // Manhattan distance 1 from center (3,2): (3,2), (2,2), (4,2), (3,1), (3,3) = 5
            var results = _grid.GetPositionsInRadius(new GridPosition(3, 2), 1);
            Assert.AreEqual(5, results.Count);
        }

        [Test]
        public void GetPositionsInRadius_Corner_ClampsToBounds()
        {
            // Corner (0,0) radius 1: only (0,0), (1,0), (0,1) = 3
            var results = _grid.GetPositionsInRadius(new GridPosition(0, 0), 1);
            Assert.AreEqual(3, results.Count);
        }

        // -------------------------------------------------------------------
        // GetTickEffects
        // -------------------------------------------------------------------

        [Test]
        public void GetTickEffects_UnitOnFireTile_ReturnsDamage()
        {
            _grid.SetTileType(new GridPosition(1, 1), TileType.Fire);
            _grid.PlaceUnit(10, new GridPosition(1, 1));
            var effects = _grid.GetTickEffects();
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(10, effects[0].TargetHeroId);
            Assert.AreEqual(50, effects[0].Damage);
            Assert.AreEqual(DamageType.Fire, effects[0].Type);
        }

        [Test]
        public void GetTickEffects_UnitOnNormalTile_ReturnsEmpty()
        {
            _grid.PlaceUnit(10, new GridPosition(0, 0));
            var effects = _grid.GetTickEffects();
            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void GetTickEffects_MultipleUnitsOnFire()
        {
            _grid.SetTileType(new GridPosition(1, 1), TileType.Fire);
            _grid.SetTileType(new GridPosition(2, 2), TileType.Fire);
            _grid.PlaceUnit(10, new GridPosition(1, 1));
            _grid.PlaceUnit(20, new GridPosition(2, 2));
            var effects = _grid.GetTickEffects();
            Assert.AreEqual(2, effects.Count);
        }

        // -------------------------------------------------------------------
        // GridPosition
        // -------------------------------------------------------------------

        [Test]
        public void GridPosition_Equality()
        {
            var a = new GridPosition(3, 2);
            var b = new GridPosition(3, 2);
            Assert.AreEqual(a, b);
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void GridPosition_Inequality()
        {
            var a = new GridPosition(3, 2);
            var b = new GridPosition(4, 2);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void GridPosition_ToString()
        {
            var pos = new GridPosition(3, 2);
            Assert.AreEqual("(3,2)", pos.ToString());
        }
    }
}
