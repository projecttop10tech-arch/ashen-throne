using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Root coordinator for the Empire scene HUD.
    /// Manages panel visibility (ResourceHUD, BuildingPanel, ResearchTreePanel),
    /// handles building tap events from the empire grid input handler,
    /// and subscribes to relevant empire events.
    ///
    /// One instance lives on the EmpireUI root GameObject in the Empire scene.
    /// </summary>
    public class EmpireUIController : MonoBehaviour
    {
        [Header("Always Visible")]
        [SerializeField] private ResourceHUD _resourceHUD;

        [Header("Modal Panels")]
        [SerializeField] private BuildingPanel _buildingPanel;
        [SerializeField] private ResearchTreePanel _researchTreePanel;

        [Header("Navigation Buttons")]
        [SerializeField] private Button _openResearchButton;
        [SerializeField] private Button _openBuildMenuButton;

        [Header("Build Queue Overlay")]
        [SerializeField] private BuildQueueOverlay _buildQueueOverlay;

        private EventSubscription _buildCompletedSub;
        private EventSubscription _buildStartedSub;
        private EventSubscription _emptyCellSub;
        private EventSubscription _placementConfirmedSub;

        // P&C: Grid position where player tapped empty ground (for building placement)
        private Vector2Int _pendingPlacementPos;

        private void Awake()
        {
            ValidateReferences();

            if (_openResearchButton != null)
                _openResearchButton.onClick.AddListener(ShowResearchTree);
            if (_openBuildMenuButton != null)
                _openBuildMenuButton.onClick.AddListener(ShowBuildMenu);
        }

        private void OnDestroy()
        {
            if (_openResearchButton != null) _openResearchButton.onClick.RemoveListener(ShowResearchTree);
            if (_openBuildMenuButton != null) _openBuildMenuButton.onClick.RemoveListener(ShowBuildMenu);
        }

        private void OnEnable()
        {
            _buildCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildCompleted);
            _buildStartedSub   = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnBuildStarted);
            _emptyCellSub      = EventBus.Subscribe<EmptyCellTappedEvent>(OnEmptyCellTapped);
            _placementConfirmedSub = EventBus.Subscribe<PlacementConfirmedEvent>(OnPlacementConfirmed);
        }

        private void OnDisable()
        {
            _buildCompletedSub?.Dispose();
            _buildStartedSub?.Dispose();
            _emptyCellSub?.Dispose();
            _placementConfirmedSub?.Dispose();
        }

        /// <summary>
        /// Called when the player taps a building tile in the empire grid.
        /// </summary>
        public void ShowBuildingPanel(string placedId)
        {
            CloseAllPanels();
            if (_buildingPanel != null)
            {
                _buildingPanel.gameObject.SetActive(true);
                _buildingPanel.Show(placedId);
            }
        }

        private void ShowResearchTree()
        {
            CloseAllPanels();
            if (_researchTreePanel != null)
                _researchTreePanel.gameObject.SetActive(true);
        }

        private void ShowBuildMenu()
        {
            ShowBuildMenuAtPosition(default);
        }

        /// <summary>
        /// P&C: Open the building placement panel at a specific grid position.
        /// Called when player taps empty ground or uses the build button.
        /// </summary>
        private void ShowBuildMenuAtPosition(Vector2Int gridPos)
        {
            _pendingPlacementPos = gridPos;
            CloseAllPanels();
            if (_buildingPanel != null)
            {
                _buildingPanel.gameObject.SetActive(true);
                _buildingPanel.ShowCatalog(gridPos);
            }
        }

        private void OnEmptyCellTapped(EmptyCellTappedEvent evt)
        {
            ShowBuildMenuAtPosition(evt.GridPosition);
        }

        private void CloseAllPanels()
        {
            if (_buildingPanel != null) _buildingPanel.gameObject.SetActive(false);
            if (_researchTreePanel != null) _researchTreePanel.gameObject.SetActive(false);
        }

        private void OnBuildCompleted(BuildingUpgradeCompletedEvent evt)
        {
            _buildQueueOverlay?.OnBuildCompleted(evt.PlacedId);
        }

        private void OnBuildStarted(BuildingUpgradeStartedEvent evt)
        {
            _buildQueueOverlay?.OnBuildStarted(evt.PlacedId, evt.TargetTier, evt.BuildTimeSeconds);
        }

        /// <summary>
        /// P&C: Handle confirmed building placement from the grid.
        /// Finds the BuildingData, calls BuildingManager.PlaceBuilding,
        /// and adds the visual to CityGridView.
        /// </summary>
        private void OnPlacementConfirmed(PlacementConfirmedEvent evt)
        {
            // Find BuildingData for this building type
            var allBuildings = Resources.LoadAll<BuildingData>("");
            BuildingData data = null;
            foreach (var bd in allBuildings)
            {
                if (bd != null && bd.buildingId == evt.BuildingId)
                {
                    data = bd;
                    break;
                }
            }

            if (data == null)
            {
                Debug.LogWarning($"[EmpireUIController] No BuildingData found for '{evt.BuildingId}'.");
                return;
            }

            if (!ServiceLocator.TryGet<BuildingManager>(out var bm))
            {
                Debug.LogWarning("[EmpireUIController] BuildingManager not available.");
                return;
            }

            // Place via BuildingManager (validates cost, deducts resources)
            string placedId = bm.PlaceBuilding(data, evt.GridOrigin);
            if (string.IsNullOrEmpty(placedId))
            {
                Debug.Log($"[EmpireUIController] PlaceBuilding failed for {evt.BuildingId}.");
                return;
            }

            // Add the visual to the city grid
            var gridView = FindFirstObjectByType<CityGridView>();
            if (gridView != null)
                gridView.PlaceBuildingOnGrid(evt.BuildingId, placedId, 0, evt.GridOrigin);

            Debug.Log($"[EmpireUIController] Placed {evt.BuildingId} at {evt.GridOrigin} → {placedId}");
            CloseAllPanels();
        }

        private void ValidateReferences()
        {
            if (_resourceHUD == null)
                Debug.LogError("[EmpireUIController] ResourceHUD not assigned.", this);
            if (_buildingPanel == null)
                Debug.LogError("[EmpireUIController] BuildingPanel not assigned.", this);
            if (_researchTreePanel == null)
                Debug.LogError("[EmpireUIController] ResearchTreePanel not assigned.", this);
        }
    }
}
