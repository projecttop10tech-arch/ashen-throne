using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

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
        }

        private void OnDisable()
        {
            _buildCompletedSub?.Dispose();
            _buildStartedSub?.Dispose();
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
            // Phase 2: opens a building placement browser.
            // Currently a stub — log intent for Phase 3 implementation.
            Debug.Log("[EmpireUIController] ShowBuildMenu — stub (Phase 3).");
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
