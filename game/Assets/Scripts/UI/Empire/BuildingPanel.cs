using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Modal panel shown when the player taps a placed building or an empty lot.
    /// Displays: building name, current tier, next tier costs, build time, active upgrade progress.
    /// Actions: Upgrade button, Cancel upgrade button, Close button.
    ///
    /// Driven by BuildQueueProgressEvent for real-time timer display without polling.
    /// </summary>
    public class BuildingPanel : MonoBehaviour
    {
        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _buildingNameLabel;
        [SerializeField] private TextMeshProUGUI _tierLabel;
        [SerializeField] private TextMeshProUGUI _upgradeDescriptionLabel;

        [Header("Cost")]
        [SerializeField] private TextMeshProUGUI _stoneCostLabel;
        [SerializeField] private TextMeshProUGUI _ironCostLabel;
        [SerializeField] private TextMeshProUGUI _grainCostLabel;
        [SerializeField] private TextMeshProUGUI _arcaneCostLabel;
        [SerializeField] private TextMeshProUGUI _buildTimeLabel;

        [Header("Queue Progress")]
        [SerializeField] private GameObject _progressSection;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _remainingTimeLabel;

        [Header("Buttons")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _closeButton;

        private BuildingManager _buildingManager;
        private string _currentPlacedId;
        private float _totalBuildSeconds;

        private EventSubscription _upgradeStartedSub;
        private EventSubscription _upgradeCompletedSub;

        private void Awake()
        {
            _buildingManager = ServiceLocator.Get<BuildingManager>();
            if (_buildingManager == null)
                Debug.LogError("[BuildingPanel] BuildingManager not found in ServiceLocator.", this);

            if (_upgradeButton != null) _upgradeButton.onClick.AddListener(OnUpgradePressed);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (_upgradeButton != null) _upgradeButton.onClick.RemoveListener(OnUpgradePressed);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        }

        private void OnEnable()
        {
            _upgradeStartedSub   = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _upgradeCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
        }

        private void OnDisable()
        {
            _upgradeStartedSub?.Dispose();
            _upgradeCompletedSub?.Dispose();
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_currentPlacedId)) return;

            // Update progress bar in real-time if this building is upgrading
            BuildQueueEntry entry = FindQueueEntry(_currentPlacedId);
            if (entry != null)
                UpdateProgressBar(entry.RemainingSeconds);
        }

        /// <summary>
        /// Open panel for a specific placed building.
        /// </summary>
        public void Show(string placedId)
        {
            if (_buildingManager == null) return;
            if (!_buildingManager.PlacedBuildings.TryGetValue(placedId, out PlacedBuilding placed))
            {
                Debug.LogWarning($"[BuildingPanel] Building '{placedId}' not found.", this);
                return;
            }

            _currentPlacedId = placedId;
            gameObject.SetActive(true);

            // Header
            SetText(_buildingNameLabel, placed.Data.displayName);
            SetText(_tierLabel, $"Level {placed.CurrentTier + 1}");

            // Next upgrade data
            BuildingTierData nextTier = placed.Data.GetTier(placed.CurrentTier + 1);
            if (nextTier != null)
            {
                SetText(_upgradeDescriptionLabel, nextTier.bonusDescription);
                SetText(_stoneCostLabel, nextTier.stoneCost.ToString("N0"));
                SetText(_ironCostLabel, nextTier.ironCost.ToString("N0"));
                SetText(_grainCostLabel, nextTier.grainCost.ToString("N0"));
                SetText(_arcaneCostLabel, nextTier.arcaneEssenceCost.ToString("N0"));
                SetText(_buildTimeLabel, FormatTime(nextTier.buildTimeSeconds));
                if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(true);
            }
            else
            {
                SetText(_upgradeDescriptionLabel, "Max level reached");
                if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(false);
            }

            // Progress section
            BuildQueueEntry entry = FindQueueEntry(placedId);
            bool isUpgrading = entry != null;
            if (_progressSection != null) _progressSection.SetActive(isUpgrading);
            if (isUpgrading)
            {
                _totalBuildSeconds = nextTier?.buildTimeSeconds ?? 60;
                UpdateProgressBar(entry.RemainingSeconds);
            }
            if (_upgradeButton != null) _upgradeButton.interactable = !isUpgrading;
        }

        private void Close()
        {
            _currentPlacedId = null;
            gameObject.SetActive(false);
        }

        private void OnUpgradePressed()
        {
            if (string.IsNullOrEmpty(_currentPlacedId) || _buildingManager == null) return;
            bool started = _buildingManager.StartUpgrade(_currentPlacedId);
            if (started && _upgradeButton != null)
                _upgradeButton.interactable = false;
        }

        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            if (evt.PlacedId != _currentPlacedId) return;
            _totalBuildSeconds = evt.BuildTimeSeconds;
            if (_progressSection != null) _progressSection.SetActive(true);
            if (_upgradeButton != null) _upgradeButton.interactable = false;
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            if (evt.PlacedId != _currentPlacedId) return;
            // Refresh to show the new tier
            Show(_currentPlacedId);
        }

        private void UpdateProgressBar(float remainingSeconds)
        {
            float ratio = _totalBuildSeconds > 0f
                ? 1f - (remainingSeconds / _totalBuildSeconds)
                : 1f;

            if (_progressBar != null) _progressBar.value = Mathf.Clamp01(ratio);
            SetText(_remainingTimeLabel, FormatTime(Mathf.CeilToInt(remainingSeconds)));
        }

        private BuildQueueEntry FindQueueEntry(string placedId)
        {
            if (_buildingManager == null) return null;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == placedId) return entry;
            }
            return null;
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Complete";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
