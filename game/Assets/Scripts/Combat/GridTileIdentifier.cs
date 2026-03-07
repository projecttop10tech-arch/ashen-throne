using UnityEngine;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Attached to each grid tile GameObject. Stores its grid position so CombatInputHandler
    /// can resolve a raycast hit back to a GridPosition without scene-position math.
    /// Set by PveEncounterManager or the combat scene setup script when tiles are created.
    /// </summary>
    public class GridTileIdentifier : MonoBehaviour
    {
        [SerializeField] private int _column;
        [SerializeField] private int _row;

        public GridPosition GridPosition => new GridPosition(_column, _row);

        /// <summary>
        /// Initialize tile identity. Called during grid tile instantiation.
        /// </summary>
        public void Initialize(int column, int row)
        {
            _column = column;
            _row = row;
        }
    }
}
