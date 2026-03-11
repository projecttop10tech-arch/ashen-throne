using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Runtime controller for the BuildingInfoPopup created by SceneUIGenerator.
    /// Subscribes to BuildingTappedEvent, populates building info, shows/hides the popup.
    /// P&C-style: tap building → info popup with name, level, costs, upgrade button.
    /// </summary>
    public class BuildingInfoPopupController : MonoBehaviour
    {
        private GameObject _popup;
        private Text _nameLabel;
        private Text _levelLabel;
        private Text _descLabel;
        private Text _costLabel;
        private Text _timeLabel;
        private Button _upgradeBtn;
        private Button _closeBtn;
        private Image _buildingPreview;

        private string _currentBuildingId;
        private string _currentInstanceId;
        private int _currentTier;

        private EventSubscription _tapSub;
        private BuildingManager _buildingManager;
        private Dictionary<string, BuildingData> _buildingDataCache;

        private void Awake()
        {
            CacheBuildingData();
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _tapSub = EventBus.Subscribe<BuildingTappedEvent>(OnBuildingTapped);
        }

        private void OnDisable()
        {
            _tapSub?.Dispose();
        }

        private void Start()
        {
            FindPopupElements();
            if (_popup != null)
                _popup.SetActive(false);
        }

        private void OnBuildingTapped(BuildingTappedEvent evt)
        {
            if (_popup == null)
                FindPopupElements();
            if (_popup == null) return;

            _currentBuildingId = evt.BuildingId;
            _currentInstanceId = evt.InstanceId;
            _currentTier = evt.Tier;

            PopulatePopup(evt);
            _popup.SetActive(true);
        }

        private void PopulatePopup(BuildingTappedEvent evt)
        {
            string displayName = FormatDisplayName(evt.BuildingId);
            int displayLevel = evt.Tier + 1;

            // Try to get real BuildingData for costs/description
            BuildingData data = null;
            _buildingDataCache?.TryGetValue(evt.BuildingId, out data);

            if (_nameLabel != null)
                _nameLabel.text = displayName.ToUpper();

            if (_levelLabel != null)
                _levelLabel.text = $"Level {displayLevel}";

            if (_descLabel != null)
            {
                if (data != null)
                    _descLabel.text = data.description;
                else
                    _descLabel.text = GetCategoryDescription(evt.BuildingId);
            }

            // Upgrade costs — next tier
            int nextTier = evt.Tier + 1;
            if (data != null)
            {
                BuildingTierData tierData = data.GetTier(nextTier);
                if (tierData != null)
                {
                    if (_costLabel != null)
                        _costLabel.text = $"{FormatNumber(tierData.stoneCost)} Stone \u2022 {FormatNumber(tierData.ironCost)} Iron \u2022 {FormatNumber(tierData.grainCost)} Grain";
                    if (_timeLabel != null)
                        _timeLabel.text = $"\u23F1 {FormatTime(tierData.buildTimeSeconds)}";
                    if (_upgradeBtn != null)
                    {
                        _upgradeBtn.gameObject.SetActive(true);
                        _upgradeBtn.interactable = true;
                    }
                }
                else
                {
                    ShowMaxLevel();
                }
            }
            else
            {
                // Placeholder costs based on tier
                int baseCost = (nextTier + 1) * 500;
                if (_costLabel != null)
                    _costLabel.text = $"{FormatNumber(baseCost)} Stone \u2022 {FormatNumber(baseCost / 2)} Iron \u2022 {FormatNumber(baseCost / 3)} Grain";
                if (_timeLabel != null)
                    _timeLabel.text = $"\u23F1 {FormatTime((nextTier + 1) * 1800)}";
                if (_upgradeBtn != null)
                {
                    _upgradeBtn.gameObject.SetActive(true);
                    _upgradeBtn.interactable = true;
                }
            }

            // Building sprite preview
            if (_buildingPreview != null)
            {
                string spritePath = $"Assets/Art/Buildings/{evt.BuildingId}_t{evt.Tier + 1}.png";
                #if UNITY_EDITOR
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                {
                    _buildingPreview.sprite = sprite;
                    _buildingPreview.preserveAspect = true;
                    _buildingPreview.color = Color.white;
                }
                #endif
            }

            // Check if currently upgrading
            if (_buildingManager != null)
            {
                foreach (var entry in _buildingManager.BuildQueue)
                {
                    if (entry.PlacedId == evt.InstanceId)
                    {
                        if (_upgradeBtn != null)
                            _upgradeBtn.interactable = false;
                        if (_timeLabel != null)
                            _timeLabel.text = $"\u23F1 {FormatTime(Mathf.CeilToInt(entry.RemainingSeconds))} remaining";
                        break;
                    }
                }
            }
        }

        private void ShowMaxLevel()
        {
            if (_costLabel != null)
                _costLabel.text = "Maximum level reached";
            if (_timeLabel != null)
                _timeLabel.text = "";
            if (_upgradeBtn != null)
                _upgradeBtn.gameObject.SetActive(false);
        }

        private void ClosePopup()
        {
            if (_popup != null)
                _popup.SetActive(false);
            _currentBuildingId = null;
            _currentInstanceId = null;
        }

        private void OnUpgradePressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId))
            {
                Debug.Log($"[BuildingInfoPopup] Upgrade tapped for {_currentBuildingId} — BuildingManager not available (demo mode).");
                ClosePopup();
                return;
            }

            bool started = _buildingManager.StartUpgrade(_currentInstanceId);
            if (started)
            {
                if (_upgradeBtn != null)
                    _upgradeBtn.interactable = false;
                // Refresh to show progress
                Debug.Log($"[BuildingInfoPopup] Upgrade started for {_currentBuildingId}.");
            }
        }

        private void FindPopupElements()
        {
            // Find the popup by name in the scene hierarchy
            var popupTransform = FindDeepChild(transform, "BuildingInfoPopup");
            if (popupTransform == null)
            {
                // Search entire scene
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    popupTransform = FindDeepChild(root.transform, "BuildingInfoPopup");
                    if (popupTransform != null) break;
                }
            }

            if (popupTransform == null)
            {
                Debug.LogWarning("[BuildingInfoPopupController] BuildingInfoPopup not found in scene.");
                return;
            }

            _popup = popupTransform.gameObject;

            // Find child elements by name
            _nameLabel = FindTextChild(popupTransform, "BuildingName");
            _levelLabel = FindTextChild(popupTransform, "LevelBadge");
            _descLabel = FindTextChild(popupTransform, "Description");
            _costLabel = FindTextChild(popupTransform, "CostText");
            _timeLabel = FindTextChild(popupTransform, "TimeText");

            // Find building preview image
            var previewTransform = FindDeepChild(popupTransform, "BuildingPreview");
            if (previewTransform != null)
                _buildingPreview = previewTransform.GetComponent<Image>();

            // Wire buttons
            var upgradeBtnTransform = FindDeepChild(popupTransform, "UpgradeBtn");
            if (upgradeBtnTransform != null)
            {
                _upgradeBtn = upgradeBtnTransform.GetComponent<Button>();
                if (_upgradeBtn != null)
                    _upgradeBtn.onClick.AddListener(OnUpgradePressed);
            }

            var closeBtnTransform = FindDeepChild(popupTransform, "CloseBtn");
            if (closeBtnTransform != null)
            {
                _closeBtn = closeBtnTransform.GetComponent<Button>();
                if (_closeBtn != null)
                    _closeBtn.onClick.AddListener(ClosePopup);
            }

            // Also close on overlay tap
            var overlayTransform = FindDeepChild(popupTransform, "Overlay");
            if (overlayTransform != null)
            {
                var overlayBtn = overlayTransform.GetComponent<Button>();
                if (overlayBtn != null)
                    overlayBtn.onClick.AddListener(ClosePopup);
            }
        }

        private void CacheBuildingData()
        {
            _buildingDataCache = new Dictionary<string, BuildingData>();
            // Try loading from Resources first
            var allData = Resources.LoadAll<BuildingData>("");
            foreach (var data in allData)
            {
                if (!string.IsNullOrEmpty(data.buildingId))
                    _buildingDataCache[data.buildingId] = data;
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeepChild(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private static Text FindTextChild(Transform parent, string name)
        {
            var child = FindDeepChild(parent, name);
            return child?.GetComponent<Text>();
        }

        private static string FormatDisplayName(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return "Building";
            // Convert snake_case to Title Case
            var parts = buildingId.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static string GetCategoryDescription(string buildingId)
        {
            if (buildingId.Contains("farm") || buildingId.Contains("mine") || buildingId.Contains("quarry"))
                return "Produces resources over time. Upgrade to increase production rate.";
            if (buildingId.Contains("barracks") || buildingId.Contains("training"))
                return "Trains troops for battle. Upgrade to unlock stronger units.";
            if (buildingId.Contains("stronghold"))
                return "The heart of your empire. Upgrade to unlock new buildings and raise level caps.";
            if (buildingId.Contains("wall") || buildingId.Contains("tower"))
                return "Defends your city from enemy attacks. Upgrade to increase defense power.";
            if (buildingId.Contains("academy") || buildingId.Contains("library") || buildingId.Contains("laboratory"))
                return "Advances your research. Upgrade to unlock new technologies.";
            if (buildingId.Contains("forge") || buildingId.Contains("armory"))
                return "Crafts and enhances equipment. Upgrade to unlock higher-tier gear.";
            return "Upgrade this building to unlock new abilities and increase its power.";
        }

        private static string FormatNumber(int value)
        {
            if (value >= 1000000) return $"{value / 1000000f:F1}M";
            if (value >= 1000) return $"{value / 1000f:F1}K";
            return value.ToString("N0");
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Instant";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }
    }
}
