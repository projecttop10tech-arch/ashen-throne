using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Empire
{
    /// <summary>
    /// A single cell in the city grid. Cells can be empty (grass) or occupied by
    /// part of a building. Buildings span multiple cells (e.g., stronghold is 4x4).
    /// </summary>
    public class CityGridCell : MonoBehaviour
    {
        [SerializeField] private Image gridLineOverlay;

        public Vector2Int GridPosition { get; private set; }
        public string OccupyingBuildingId { get; private set; }
        public bool IsOccupied => !string.IsNullOrEmpty(OccupyingBuildingId);

        public void Initialize(Vector2Int position)
        {
            GridPosition = position;
            OccupyingBuildingId = null;
        }

        public void SetOccupied(string buildingPlacedId)
        {
            OccupyingBuildingId = buildingPlacedId;
        }

        public void ClearOccupied()
        {
            OccupyingBuildingId = null;
        }

        public void SetGridLineVisible(bool visible)
        {
            if (gridLineOverlay != null)
                gridLineOverlay.color = visible
                    ? new Color(0.4f, 0.8f, 0.3f, 0.2f)  // subtle green grid line
                    : new Color(1f, 1f, 1f, 0f);           // invisible
        }

        public void SetHighlight(bool on)
        {
            if (gridLineOverlay != null)
                gridLineOverlay.color = on
                    ? new Color(1f, 0.9f, 0.3f, 0.35f)    // yellow selection
                    : new Color(0.4f, 0.8f, 0.3f, 0.2f);  // back to green grid
        }
    }
}
