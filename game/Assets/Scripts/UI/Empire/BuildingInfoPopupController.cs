using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Runtime controller for the BuildingInfoPopup created by SceneUIGenerator.
    /// P&C-style: tap building → info popup with name, level, costs, upgrade/speedup/cancel.
    /// Real-time timer updates while popup is open and building is upgrading.
    /// </summary>
    public class BuildingInfoPopupController : MonoBehaviour
    {
        private GameObject _popup;
        private Text _nameLabel;
        private Text _levelLabel;
        private Text _descLabel;
        private Text _costLabel;
        private Text _timeLabel;
        private Image _buildingPreview;

        // Idle state buttons
        private Button _upgradeBtn;
        private Button _closeBtn;
        private Button _moveBtn; // P&C: Relocate building
        private Button _demolishBtn; // P&C: Remove building
        private bool _demolishConfirmPending;
        private float _demolishConfirmTimer;

        // Upgrading state buttons
        private Button _speedUpBtn;
        private Button _cancelBtn;
        private Button _helpBtn;

        // Timer progress bar
        private RectTransform _timerBarBg;
        private RectTransform _timerBarFill;

        private string _currentBuildingId;
        private string _currentInstanceId;
        private int _currentTier;
        private bool _isUpgrading;
        private float _totalBuildSeconds;

        // P&C: Free speedup threshold (5 minutes)
        private const float FreeSpeedupThreshold = 300f;
        // Gems per minute of speedup (P&C-style scaling)
        private const int GemsPerMinute = 10;

        // P&C: Navigation arrows to cycle between same-type buildings
        private Button _prevBtn;
        private Button _nextBtn;
        private Text _navCountLabel;
        private List<(string instanceId, int tier)> _sameTypeList = new();
        private int _sameTypeIndex;

        private EventSubscription _tapSub;
        private EventSubscription _infoRequestSub;
        private EventSubscription _completedSub;
        private EventSubscription _cancelledSub;
        private BuildingManager _buildingManager;
        private Dictionary<string, BuildingData> _buildingDataCache;

        private void Awake()
        {
            CacheBuildingData();
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            // P&C: Info popup now opens via BuildingInfoRequestedEvent (from quick action menu)
            // Keep BuildingTappedEvent as fallback for direct open
            _tapSub = EventBus.Subscribe<BuildingTappedEvent>(OnBuildingTapped);
            _infoRequestSub = EventBus.Subscribe<BuildingInfoRequestedEvent>(OnInfoRequested);
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _cancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(OnUpgradeCancelled);
        }

        private void OnDisable()
        {
            _tapSub?.Dispose();
            _infoRequestSub?.Dispose();
            _completedSub?.Dispose();
            _cancelledSub?.Dispose();
        }

        private void Start()
        {
            FindPopupElements();
            if (_popup != null)
                _popup.SetActive(false);
        }

        private void Update()
        {
            if (_popup == null || !_popup.activeSelf) return;

            // P&C: Cancel confirm timeout — revert to normal after 3s
            if (_cancelConfirmPending)
            {
                _cancelConfirmTimer -= Time.deltaTime;
                if (_cancelConfirmTimer <= 0f)
                    ResetCancelConfirm();
            }

            // P&C: Demolish confirm timeout
            if (_demolishConfirmPending)
            {
                _demolishConfirmTimer -= Time.deltaTime;
                if (_demolishConfirmTimer <= 0f)
                    ResetDemolishConfirm();
            }

            if (!_isUpgrading || _buildingManager == null) return;

            // Find the active queue entry and update timer in real-time
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == _currentInstanceId)
                {
                    float remaining = entry.RemainingSeconds;
                    float progress = _totalBuildSeconds > 0 ? 1f - (remaining / _totalBuildSeconds) : 1f;

                    if (_timeLabel != null)
                    {
                        string color = remaining < 10f ? "<color=#FF4444>" : "";
                        string endColor = remaining < 10f ? "</color>" : "";
                        _timeLabel.text = $"{color}\u23F1 {FormatTime(Mathf.CeilToInt(remaining))} remaining{endColor}";
                    }

                    if (_timerBarFill != null)
                        _timerBarFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

                    // P&C: Update Speed Up button — FREE when < 5min, show gems cost otherwise
                    UpdateSpeedUpLabel(remaining);

                    return;
                }
            }

            // If we were upgrading but entry is gone, upgrade completed while popup was open
            if (_isUpgrading)
                RefreshAfterCompletion();
        }

        private void OnBuildingTapped(BuildingTappedEvent evt)
        {
            // P&C: Quick action menu handles first tap.
            // Info popup only opens via BuildingInfoRequestedEvent.
            // Keep this as fallback when no quick action menu is present.
            var quickMenu = FindFirstObjectByType<BuildingQuickActionMenu>();
            if (quickMenu != null) return; // Quick action menu will handle it

            OpenPopupForBuilding(evt.BuildingId, evt.InstanceId, evt.Tier);
        }

        private void OnInfoRequested(BuildingInfoRequestedEvent evt)
        {
            OpenPopupForBuilding(evt.BuildingId, evt.InstanceId, evt.Tier);
        }

        private void OpenPopupForBuilding(string buildingId, string instanceId, int tier)
        {
            if (_popup == null)
                FindPopupElements();
            if (_popup == null) return;

            _currentBuildingId = buildingId;
            _currentInstanceId = instanceId;
            _currentTier = tier;

            // P&C: Build same-type list for arrow navigation
            BuildSameTypeList(buildingId, instanceId);

            var tapEvt = new BuildingTappedEvent(buildingId, instanceId, tier, default);
            PopulatePopup(tapEvt);
            _popup.SetActive(true);
        }

        /// <summary>P&C: Build a list of all instances of the same building type for cycling.</summary>
        private void BuildSameTypeList(string buildingId, string currentInstanceId)
        {
            _sameTypeList.Clear();
            _sameTypeIndex = 0;

            if (_buildingManager == null) return;

            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == buildingId)
                    _sameTypeList.Add((kvp.Key, kvp.Value.CurrentTier));
            }

            // Find current index
            for (int i = 0; i < _sameTypeList.Count; i++)
            {
                if (_sameTypeList[i].instanceId == currentInstanceId)
                {
                    _sameTypeIndex = i;
                    break;
                }
            }

            // Show/hide navigation arrows
            bool showNav = _sameTypeList.Count > 1;
            if (_prevBtn != null) _prevBtn.gameObject.SetActive(showNav);
            if (_nextBtn != null) _nextBtn.gameObject.SetActive(showNav);
            if (_navCountLabel != null)
            {
                _navCountLabel.gameObject.SetActive(showNav);
                if (showNav)
                    _navCountLabel.text = $"{_sameTypeIndex + 1}/{_sameTypeList.Count}";
            }
        }

        private void OnPrevBuilding()
        {
            if (_sameTypeList.Count < 2) return;
            _sameTypeIndex = (_sameTypeIndex - 1 + _sameTypeList.Count) % _sameTypeList.Count;
            NavigateToBuilding();
        }

        private void OnNextBuilding()
        {
            if (_sameTypeList.Count < 2) return;
            _sameTypeIndex = (_sameTypeIndex + 1) % _sameTypeList.Count;
            NavigateToBuilding();
        }

        private void NavigateToBuilding()
        {
            var (instanceId, tier) = _sameTypeList[_sameTypeIndex];
            _currentInstanceId = instanceId;
            _currentTier = tier;

            if (_navCountLabel != null)
                _navCountLabel.text = $"{_sameTypeIndex + 1}/{_sameTypeList.Count}";

            var evt = new BuildingTappedEvent(_currentBuildingId, instanceId, tier, default);
            PopulatePopup(evt);

            // P&C: Smooth zoom + pan to the new building
            var cityGrid = Object.FindFirstObjectByType<CityGridView>();
            if (cityGrid != null)
                cityGrid.ZoomToBuildingSmooth(instanceId);
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            if (_popup == null || !_popup.activeSelf) return;
            if (evt.PlacedId != _currentInstanceId) return;
            RefreshAfterCompletion();
        }

        private void OnUpgradeCancelled(BuildingUpgradeCancelledEvent evt)
        {
            if (_popup == null || !_popup.activeSelf) return;
            if (evt.PlacedId != _currentInstanceId) return;
            RefreshAfterCompletion();
        }

        private void RefreshAfterCompletion()
        {
            _isUpgrading = false;
            // Re-populate with updated tier
            if (_buildingManager != null)
            {
                // Try to get updated tier from BuildingManager
                foreach (var entry in _buildingManager.BuildQueue)
                {
                    if (entry.PlacedId == _currentInstanceId)
                    {
                        _currentTier = entry.TargetTier - 1;
                        break;
                    }
                }
            }
            var refreshEvt = new BuildingTappedEvent(_currentBuildingId, _currentInstanceId, _currentTier, default);
            PopulatePopup(refreshEvt);
        }

        private void PopulatePopup(BuildingTappedEvent evt)
        {
            string displayName = FormatDisplayName(evt.BuildingId);
            int displayLevel = evt.Tier + 1;

            BuildingData data = null;
            _buildingDataCache?.TryGetValue(evt.BuildingId, out data);

            if (_nameLabel != null)
            {
                _nameLabel.text = displayName.ToUpper();
                // P&C: Category-colored header — tint the header bg by building type
                ApplyCategoryHeaderColor(evt.BuildingId);
            }

            if (_levelLabel != null)
            {
                // P&C: Show building count for non-unique buildings (e.g., "Level 3 | 2/5")
                int buildingCount = CountBuildings(evt.BuildingId);
                bool isUnique = data != null && data.isUniquePerCity;

                // P&C: "Recommended!" tag for stronghold or lowest-level buildings
                string recTag = IsRecommendedUpgrade(evt.BuildingId, evt.Tier)
                    ? "  <color=#FFD966>\u2605 Recommended!</color>" : "";

                if (!isUnique && buildingCount > 1)
                    _levelLabel.text = $"Level {displayLevel}  |  {buildingCount} built{recTag}";
                else
                    _levelLabel.text = $"Level {displayLevel}{recTag}";
            }

            if (_descLabel != null)
            {
                // P&C: Show power increase preview
                int powerIncrease = EstimatePowerIncrease(evt.BuildingId, evt.Tier + 1);
                string powerPreview = $"\n<color=#44FF66>+{FormatNumber(powerIncrease)} Power</color>";

                // P&C: Show bonus description from next tier's BuildingTierData
                string bonusLine = "";
                string statsPreview = "";
                if (data != null)
                {
                    int nextTier = evt.Tier + 1;
                    BuildingTierData nextTierData = data.GetTier(nextTier);
                    BuildingTierData currentTierData = data.GetTier(evt.Tier);

                    if (nextTierData != null && !string.IsNullOrEmpty(nextTierData.bonusDescription))
                        bonusLine = $"\n<color=#FFD966>{nextTierData.bonusDescription}</color>";
                    else if (currentTierData != null && !string.IsNullOrEmpty(currentTierData.bonusDescription))
                        bonusLine = $"\n<color=#FFD966>{currentTierData.bonusDescription}</color>";

                    // P&C: Before -> After stat comparison
                    statsPreview = BuildStatsPreview(currentTierData, nextTierData);
                }

                string baseDesc = (data != null && !string.IsNullOrEmpty(data.description))
                    ? data.description : GetCategoryDescription(evt.BuildingId);
                _descLabel.text = baseDesc + bonusLine + statsPreview + powerPreview;
            }

            // Check if currently upgrading
            _isUpgrading = false;
            _totalBuildSeconds = 0;
            if (_buildingManager != null)
            {
                foreach (var entry in _buildingManager.BuildQueue)
                {
                    if (entry.PlacedId == evt.InstanceId)
                    {
                        _isUpgrading = true;
                        // Estimate total from start time
                        float elapsed = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds;
                        _totalBuildSeconds = elapsed + entry.RemainingSeconds;
                        break;
                    }
                }
            }

            if (_isUpgrading)
            {
                ShowUpgradingState();
            }
            else
            {
                ShowIdleState(evt, data);
            }

            // Building sprite preview
            LoadBuildingPreview(evt.BuildingId, evt.Tier);
        }

        private void ShowIdleState(BuildingTappedEvent evt, BuildingData data)
        {
            // Show upgrade/close, hide speedup/cancel
            SetButtonGroupActive(true);

            // P&C: Check Stronghold requirement
            int requiredSHLevel = data != null ? data.strongholdLevelRequired : (evt.Tier + 1);
            if (evt.BuildingId == "stronghold") requiredSHLevel = 0;
            int currentSHLevel = GetStrongholdLevel();
            bool meetsSHReq = requiredSHLevel <= currentSHLevel || evt.BuildingId == "stronghold";

            int nextTier = evt.Tier + 1;

            // P&C: Check category-based prerequisites (e.g., "Requires Academy Lv.5")
            string categoryReq = CheckCategoryPrerequisite(evt.BuildingId, nextTier);
            bool meetsAllReqs = meetsSHReq && categoryReq == null;
            if (data != null)
            {
                BuildingTierData tierData = data.GetTier(nextTier);
                if (tierData != null)
                {
                    // P&C: Detailed cost with met/unmet checkmarks
                    if (_costLabel != null)
                        _costLabel.text = FormatCostWithChecks(tierData);
                    if (_timeLabel != null)
                    {
                        string timeStr = $"\u23F1 {FormatTime(tierData.buildTimeSeconds)}";
                        if (!meetsSHReq)
                            timeStr = $"<color=#FF6644>Requires Stronghold Lv.{requiredSHLevel}</color>";
                        else if (categoryReq != null)
                            timeStr = $"<color=#FF6644>{categoryReq}</color>";
                        // P&C: Show production rate for resource buildings
                        string prodRate = GetProductionRateText(tierData);
                        if (!string.IsNullOrEmpty(prodRate))
                            timeStr += $"  |  {prodRate}";
                        _timeLabel.text = timeStr;
                    }
                    if (_upgradeBtn != null)
                    {
                        _upgradeBtn.gameObject.SetActive(true);
                        _upgradeBtn.interactable = meetsAllReqs;
                        SetButtonLabel(_upgradeBtn, meetsAllReqs ? GetActionLabel(evt.BuildingId) : "LOCKED");
                    }
                }
                else
                {
                    ShowMaxLevel();
                    // Show current production for max-level resource buildings
                    BuildingTierData currentTier = data.GetTier(evt.Tier);
                    if (currentTier != null && _timeLabel != null)
                    {
                        string prodRate = GetProductionRateText(currentTier);
                        if (!string.IsNullOrEmpty(prodRate))
                            _timeLabel.text = prodRate;
                    }
                }
            }
            else
            {
                int baseCost = (nextTier + 1) * 500;
                if (_costLabel != null)
                    _costLabel.text = $"{FormatNumber(baseCost)} Stone \u2022 {FormatNumber(baseCost / 2)} Iron \u2022 {FormatNumber(baseCost / 3)} Grain";
                if (_timeLabel != null)
                {
                    _timeLabel.text = $"\u23F1 {FormatTime((nextTier + 1) * 1800)}";
                    if (!meetsSHReq)
                        _timeLabel.text = $"<color=#FF6644>Requires Stronghold Lv.{requiredSHLevel}</color>";
                    else if (categoryReq != null)
                        _timeLabel.text = $"<color=#FF6644>{categoryReq}</color>";
                }
                if (_upgradeBtn != null)
                {
                    _upgradeBtn.gameObject.SetActive(true);
                    _upgradeBtn.interactable = meetsAllReqs;
                    SetButtonLabel(_upgradeBtn, meetsAllReqs ? GetActionLabel(evt.BuildingId) : "LOCKED");
                }
            }

            // Hide timer progress bar in idle state
            if (_timerBarBg != null)
                _timerBarBg.gameObject.SetActive(false);
        }

        /// <summary>
        /// P&C: Apply category-based color tint to the popup header background.
        /// Military=red, Resource=green, Research=blue, Defense=orange, Crafting=purple.
        /// </summary>
        private void ApplyCategoryHeaderColor(string buildingId)
        {
            if (_popup == null) return;
            var header = FindDeepChild(_popup.transform, "Header");
            if (header == null) return;
            var headerImg = header.GetComponent<Image>();
            if (headerImg == null) return;

            Color catColor;
            if (buildingId.Contains("barracks") || buildingId.Contains("training") || buildingId.Contains("armory"))
                catColor = new Color(0.28f, 0.08f, 0.08f, 1f); // Military — deep red
            else if (buildingId.Contains("farm") || buildingId.Contains("mine") || buildingId.Contains("quarry"))
                catColor = new Color(0.08f, 0.22f, 0.08f, 1f); // Resource — deep green
            else if (buildingId.Contains("academy") || buildingId.Contains("library") || buildingId.Contains("laboratory") || buildingId.Contains("observatory"))
                catColor = new Color(0.08f, 0.12f, 0.28f, 1f); // Research — deep blue
            else if (buildingId.Contains("wall") || buildingId.Contains("watch_tower"))
                catColor = new Color(0.28f, 0.18f, 0.06f, 1f); // Defense — deep amber
            else if (buildingId.Contains("forge") || buildingId.Contains("enchanting"))
                catColor = new Color(0.20f, 0.08f, 0.28f, 1f); // Crafting — deep purple
            else if (buildingId.Contains("stronghold"))
                catColor = new Color(0.22f, 0.16f, 0.06f, 1f); // Stronghold — regal gold
            else
                catColor = new Color(0.12f, 0.08f, 0.20f, 1f); // Default — dark purple

            headerImg.color = catColor;
        }

        /// <summary>P&C: Format costs with colored resource icons and checkmark/X affordability.</summary>
        private string FormatCostWithChecks(BuildingTierData tier)
        {
            ResourceManager rm = null;
            ServiceLocator.TryGet(out rm);

            long GetRes(ResourceType type)
            {
                if (rm == null) return long.MaxValue;
                return type switch
                {
                    ResourceType.Stone => rm.Stone,
                    ResourceType.Iron => rm.Iron,
                    ResourceType.Grain => rm.Grain,
                    ResourceType.ArcaneEssence => rm.ArcaneEssence,
                    _ => 0
                };
            }

            // P&C: Each resource has a colored circle icon + affordability check
            string FormatRes(int cost, string name, ResourceType type, string iconColor, string icon)
            {
                if (cost <= 0) return null;
                bool canAfford = GetRes(type) >= cost;
                string mark = canAfford ? "<color=#44FF66>\u2713</color>" : "<color=#FF4444>\u2717</color>";
                return $"{mark} <color={iconColor}>{icon}</color> {FormatNumber(cost)} <color={iconColor}>{name}</color>";
            }

            var parts = new System.Collections.Generic.List<string>();
            var s = FormatRes(tier.stoneCost, "Stone", ResourceType.Stone, "#D4C8B4", "\u25C9");
            if (s != null) parts.Add(s);
            var i = FormatRes(tier.ironCost, "Iron", ResourceType.Iron, "#C8CCE6", "\u25C9");
            if (i != null) parts.Add(i);
            var g = FormatRes(tier.grainCost, "Grain", ResourceType.Grain, "#FFE844", "\u25C9");
            if (g != null) parts.Add(g);
            var a = FormatRes(tier.arcaneEssenceCost, "Arcane", ResourceType.ArcaneEssence, "#CC88FF", "\u25C9");
            if (a != null) parts.Add(a);

            return parts.Count > 0 ? string.Join("\n", parts) : "Free";
        }

        /// <summary>P&C: Show per-hour production rate for resource buildings.</summary>
        private static string GetProductionRateText(BuildingTierData tier)
        {
            if (tier.stoneProduction > 0) return $"<color=#D4C8B4>+{tier.stoneProduction:F0} Stone/hr</color>";
            if (tier.ironProduction > 0) return $"<color=#C8CCE6>+{tier.ironProduction:F0} Iron/hr</color>";
            if (tier.grainProduction > 0) return $"<color=#FFE844>+{tier.grainProduction:F0} Grain/hr</color>";
            if (tier.arcaneEssenceProduction > 0) return $"<color=#CC88FF>+{tier.arcaneEssenceProduction:F0} Arcane/hr</color>";
            return null;
        }

        /// <summary>P&C: Build a before→after stat comparison string for upgrade preview.</summary>
        private static string BuildStatsPreview(BuildingTierData current, BuildingTierData next)
        {
            if (current == null || next == null) return "";
            var lines = new System.Collections.Generic.List<string>();

            // Production rate comparison
            AddStatLine(lines, "Stone/hr", current.stoneProduction, next.stoneProduction, "#D4C8B4");
            AddStatLine(lines, "Iron/hr", current.ironProduction, next.ironProduction, "#C8CCE6");
            AddStatLine(lines, "Grain/hr", current.grainProduction, next.grainProduction, "#FFE844");
            AddStatLine(lines, "Arcane/hr", current.arcaneEssenceProduction, next.arcaneEssenceProduction, "#CC88FF");

            // Bonus comparison
            if (next.bonuses != null)
            {
                foreach (var bonus in next.bonuses)
                {
                    float prevVal = 0f;
                    if (current.bonuses != null)
                    {
                        foreach (var cb in current.bonuses)
                            if (cb.bonusType == bonus.bonusType) { prevVal = cb.bonusPercent; break; }
                    }
                    if (bonus.bonusPercent != prevVal)
                    {
                        string bName = FormatBonusName(bonus.bonusType);
                        lines.Add($"<color=#AAAAAA>{bName}:</color> {prevVal:F0}% <color=#44FF66>→ {bonus.bonusPercent:F0}%</color>");
                    }
                }
            }

            if (lines.Count == 0) return "";
            return "\n" + string.Join("  |  ", lines);
        }

        private static void AddStatLine(System.Collections.Generic.List<string> lines, string name, float current, float next, string color)
        {
            if (next <= 0 && current <= 0) return;
            if (next != current)
                lines.Add($"<color={color}>{name}:</color> {current:F0} <color=#44FF66>→ {next:F0}</color>");
            else if (current > 0)
                lines.Add($"<color={color}>{name}: {current:F0}</color>");
        }

        private static string FormatBonusName(BuildingBonusType type)
        {
            switch (type)
            {
                case BuildingBonusType.TroopTrainingSpeed: return "Train Speed";
                case BuildingBonusType.ResearchSpeed: return "Research Speed";
                case BuildingBonusType.VaultCapacity: return "Vault Capacity";
                case BuildingBonusType.TroopCapacity: return "Troop Cap";
                case BuildingBonusType.HeroXpBonus: return "Hero XP";
                default: return type.ToString();
            }
        }

        private int GetStrongholdLevel()
        {
            if (_buildingManager == null) return 99;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == "stronghold")
                    return kvp.Value.CurrentTier;
            }
            return 0;
        }

        private int CountBuildings(string buildingId)
        {
            if (_buildingManager == null) return 1;
            int count = 0;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == buildingId)
                    count++;
            }
            return Mathf.Max(1, count);
        }

        /// <summary>
        /// P&C: Get the highest tier of a specific building type across all placed instances.
        /// Used for category prerequisites (e.g., "Requires Academy Lv.5").
        /// </summary>
        private int GetBuildingTypeLevel(string buildingId)
        {
            if (_buildingManager == null) return 99;
            int maxTier = -1;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == buildingId)
                    maxTier = Mathf.Max(maxTier, kvp.Value.CurrentTier);
            }
            return maxTier;
        }

        /// <summary>
        /// P&C: Check category-based prerequisites for upgrading a building.
        /// Returns null if met, or a requirement string like "Requires Academy Lv.3".
        /// </summary>
        private string CheckCategoryPrerequisite(string buildingId, int targetTier)
        {
            if (_buildingManager == null) return null;

            // P&C-style prerequisite rules by category
            string reqBuilding = null;
            int reqLevel = 0;

            if (buildingId.Contains("barracks") || buildingId.Contains("training") || buildingId.Contains("armory"))
            {
                // Military buildings require Stronghold at matching tier
                reqBuilding = "stronghold";
                reqLevel = targetTier;
            }
            else if (buildingId.Contains("academy") || buildingId.Contains("library") || buildingId.Contains("laboratory") || buildingId.Contains("observatory"))
            {
                // Research buildings require Stronghold - 1
                reqBuilding = "stronghold";
                reqLevel = Mathf.Max(0, targetTier - 1);
            }
            else if (buildingId.Contains("forge") || buildingId.Contains("enchanting"))
            {
                // Crafting buildings require Academy at half tier
                reqBuilding = "academy";
                reqLevel = Mathf.Max(0, targetTier / 2);
            }
            else if (buildingId.Contains("wall") || buildingId.Contains("watch_tower"))
            {
                // Defense buildings require Barracks
                reqBuilding = "barracks";
                reqLevel = Mathf.Max(0, targetTier - 1);
            }

            if (reqBuilding == null) return null;

            int currentLevel = reqBuilding == "stronghold" ? GetStrongholdLevel() : GetBuildingTypeLevel(reqBuilding);
            if (currentLevel >= reqLevel) return null;

            string displayName = FormatDisplayName(reqBuilding);
            return $"Requires {displayName} Lv.{reqLevel + 1}";
        }

        private void ShowUpgradingState()
        {
            // Hide upgrade/close, show speedup/cancel
            SetButtonGroupActive(false);

            if (_costLabel != null)
                _costLabel.text = "Upgrade in progress...";

            // Show timer progress bar
            if (_timerBarBg != null)
                _timerBarBg.gameObject.SetActive(true);
            if (_timerBarFill != null)
                _timerBarFill.anchorMax = new Vector2(0.01f, 1f);
        }

        private void SetButtonGroupActive(bool idle)
        {
            if (_upgradeBtn != null) _upgradeBtn.gameObject.SetActive(idle);
            if (_closeBtn != null) _closeBtn.gameObject.SetActive(idle);
            bool canRelocate = idle && _currentBuildingId != "stronghold";
            if (_moveBtn != null) _moveBtn.gameObject.SetActive(canRelocate);
            if (_demolishBtn != null) _demolishBtn.gameObject.SetActive(canRelocate);
            if (_speedUpBtn != null) _speedUpBtn.gameObject.SetActive(!idle);
            if (_cancelBtn != null) _cancelBtn.gameObject.SetActive(!idle);
            if (_helpBtn != null) _helpBtn.gameObject.SetActive(!idle);
        }

        private void ShowMaxLevel()
        {
            if (_costLabel != null)
                _costLabel.text = "Maximum level reached";
            if (_timeLabel != null)
                _timeLabel.text = "";
            if (_upgradeBtn != null)
                _upgradeBtn.gameObject.SetActive(false);
            if (_timerBarBg != null)
                _timerBarBg.gameObject.SetActive(false);
        }

        private void ClosePopup()
        {
            if (_popup != null)
                _popup.SetActive(false);
            _currentBuildingId = null;
            _currentInstanceId = null;
            _isUpgrading = false;
            ResetCancelConfirm();
            ResetDemolishConfirm();
        }

        private void ResetDemolishConfirm()
        {
            if (!_demolishConfirmPending) return;
            _demolishConfirmPending = false;
            SetButtonLabel(_demolishBtn, "DEMOLISH");
            var btnImg = _demolishBtn?.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = new Color(0.50f, 0.18f, 0.18f, 1f);
        }

        private void OnUpgradePressed()
        {
            // P&C: COLLECT ALL for resource buildings — collects from ALL instances of the same type
            if (IsResourceBuilding(_currentBuildingId))
            {
                var spawner = FindFirstObjectByType<ResourceBubbleSpawner>();
                if (spawner != null)
                {
                    spawner.CollectAllForBuildingType(_currentBuildingId);
                    Debug.Log($"[BuildingInfoPopup] Collected all resources from all {_currentBuildingId} instances.");
                }
                ClosePopup();
                return;
            }

            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId))
            {
                Debug.Log($"[BuildingInfoPopup] Upgrade tapped for {_currentBuildingId} — BuildingManager not available (demo mode).");
                ClosePopup();
                return;
            }

            bool started = _buildingManager.StartUpgrade(_currentInstanceId);
            if (started)
            {
                Debug.Log($"[BuildingInfoPopup] Upgrade started for {_currentBuildingId}.");
                // Refresh popup to show upgrading state
                var evt = new BuildingTappedEvent(_currentBuildingId, _currentInstanceId, _currentTier, default);
                PopulatePopup(evt);
            }
        }

        /// <summary>P&C: Enter move/relocate mode for the current building.</summary>
        private void OnMovePressed()
        {
            if (string.IsNullOrEmpty(_currentInstanceId)) return;
            var cityGrid = Object.FindFirstObjectByType<CityGridView>();
            if (cityGrid != null)
            {
                ClosePopup();
                cityGrid.EnterMoveModeForBuilding(_currentInstanceId);
                Debug.Log($"[BuildingInfoPopup] Entering move mode for {_currentBuildingId}.");
            }
        }

        /// <summary>P&C: Demolish building via confirmation dialog.</summary>
        private void OnDemolishPressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId)) return;
            if (_currentBuildingId == "stronghold") return;

            // Publish event to open the demolish confirmation dialog
            EventBus.Publish(new DemolishRequestedEvent(_currentInstanceId, _currentBuildingId, _currentTier));
            ClosePopup();
        }

        private void OnSpeedUpPressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId)) return;

            // Find current remaining time
            float remaining = 0f;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == _currentInstanceId)
                {
                    remaining = entry.RemainingSeconds;
                    break;
                }
            }

            if (remaining <= FreeSpeedupThreshold)
            {
                // P&C: FREE instant complete when < 5 minutes
                _buildingManager.ApplySpeedup(_currentInstanceId, Mathf.CeilToInt(remaining));
                Debug.Log($"[BuildingInfoPopup] FREE speed up — instant complete for {_currentBuildingId}.");
            }
            else
            {
                // P&C style: Speed Up reduces by 10 minutes (600s), costs gems
                int speedupAmount = 600;
                int gemsCost = Mathf.CeilToInt(remaining / 60f) * GemsPerMinute;
                // TODO: Check/deduct gems from economy system
                _buildingManager.ApplySpeedup(_currentInstanceId, speedupAmount);
                Debug.Log($"[BuildingInfoPopup] Speed up applied: {speedupAmount}s for {_currentBuildingId} (cost: {gemsCost} gems).");
            }
        }

        private void UpdateSpeedUpLabel(float remainingSeconds)
        {
            if (_speedUpBtn == null) return;
            if (remainingSeconds <= FreeSpeedupThreshold)
            {
                SetButtonLabel(_speedUpBtn, "FREE");
                // Change button color to green for free
                var btnImg = _speedUpBtn.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = new Color(0.20f, 0.78f, 0.35f, 1f);
            }
            else
            {
                int gemsCost = Mathf.CeilToInt(remainingSeconds / 60f) * GemsPerMinute;
                SetButtonLabel(_speedUpBtn, $"SPEED UP\n{gemsCost} Gems");
                var btnImg = _speedUpBtn.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = new Color(0.30f, 0.75f, 0.95f, 1f);
            }
        }

        private bool _cancelConfirmPending;
        private float _cancelConfirmTimer;
        private const float CancelConfirmTimeout = 3f;

        private void OnCancelPressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId)) return;

            if (!_cancelConfirmPending)
            {
                // P&C: First tap — show confirmation state
                _cancelConfirmPending = true;
                _cancelConfirmTimer = CancelConfirmTimeout;
                SetButtonLabel(_cancelBtn, "CONFIRM?");
                var btnImg = _cancelBtn?.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = new Color(0.95f, 0.20f, 0.20f, 1f); // Bright red
                return;
            }

            // Second tap — actually cancel
            _cancelConfirmPending = false;
            bool cancelled = _buildingManager.CancelUpgrade(_currentInstanceId);
            if (cancelled)
            {
                Debug.Log($"[BuildingInfoPopup] Upgrade cancelled for {_currentBuildingId}.");
                ClosePopup();
            }
        }

        private void ResetCancelConfirm()
        {
            if (!_cancelConfirmPending) return;
            _cancelConfirmPending = false;
            SetButtonLabel(_cancelBtn, "CANCEL");
            var btnImg = _cancelBtn?.GetComponent<Image>();
            if (btnImg != null)
                btnImg.color = new Color(0.85f, 0.25f, 0.25f, 1f); // Normal red
        }

        /// <summary>
        /// P&C: Request alliance help to reduce build time.
        /// Each alliance member can reduce by a fixed amount (e.g., 1 minute per help).
        /// </summary>
        private void OnHelpPressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId)) return;

            // P&C: Alliance help reduces by 60 seconds per member (simulated)
            int helpReduction = 60;
            _buildingManager.ApplySpeedup(_currentInstanceId, helpReduction);
            Debug.Log($"[BuildingInfoPopup] Alliance help requested for {_currentBuildingId} (-{helpReduction}s).");

            // Disable help button after requesting (one request per upgrade)
            if (_helpBtn != null)
                _helpBtn.interactable = false;
        }

        private void LoadBuildingPreview(string buildingId, int tier)
        {
            if (_buildingPreview == null) return;
            string spritePath = $"Assets/Art/Buildings/{buildingId}_t{tier + 1}.png";
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

        private void FindPopupElements()
        {
            var popupTransform = FindDeepChild(transform, "BuildingInfoPopup");
            if (popupTransform == null)
            {
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

            _nameLabel = FindTextChild(popupTransform, "BuildingName");
            _levelLabel = FindTextChild(popupTransform, "LevelBadge");
            _descLabel = FindTextChild(popupTransform, "Description");
            _costLabel = FindTextChild(popupTransform, "CostText");
            _timeLabel = FindTextChild(popupTransform, "TimeText");

            var previewTransform = FindDeepChild(popupTransform, "BuildingPreview");
            if (previewTransform != null)
                _buildingPreview = previewTransform.GetComponent<Image>();

            // Timer bar
            var timerBarBgT = FindDeepChild(popupTransform, "TimerBarBg");
            if (timerBarBgT != null)
                _timerBarBg = timerBarBgT.GetComponent<RectTransform>();
            var timerBarFillT = FindDeepChild(popupTransform, "TimerBarFill");
            if (timerBarFillT != null)
                _timerBarFill = timerBarFillT.GetComponent<RectTransform>();

            // Idle buttons
            WireButton(popupTransform, "UpgradeBtn", ref _upgradeBtn, OnUpgradePressed);
            WireButton(popupTransform, "CloseBtn", ref _closeBtn, ClosePopup);
            WireButton(popupTransform, "MoveBtn", ref _moveBtn, OnMovePressed);
            WireButton(popupTransform, "DemolishBtn", ref _demolishBtn, OnDemolishPressed);

            // Upgrading buttons
            WireButton(popupTransform, "SpeedUpBtn", ref _speedUpBtn, OnSpeedUpPressed);
            WireButton(popupTransform, "CancelBtn", ref _cancelBtn, OnCancelPressed);
            WireButton(popupTransform, "HelpBtn", ref _helpBtn, OnHelpPressed);

            // Overlay tap to close
            var overlayTransform = FindDeepChild(popupTransform, "Overlay");
            if (overlayTransform != null)
            {
                var overlayBtn = overlayTransform.GetComponent<Button>();
                if (overlayBtn != null)
                    overlayBtn.onClick.AddListener(ClosePopup);
            }

            // P&C: Create navigation arrows for same-type building cycling
            CreateNavigationArrows(popupTransform);
        }

        /// <summary>P&C: Create left/right arrow buttons for cycling between same-type buildings.</summary>
        private void CreateNavigationArrows(Transform popupRoot)
        {
            var frame = FindDeepChild(popupRoot, "Frame");
            if (frame == null) return;

            // Left arrow (< previous)
            var prevGO = CreateNavArrowButton(frame, "PrevBuildingBtn", "\u25C0", new Vector2(-0.02f, 0.44f), new Vector2(0.06f, 0.56f));
            _prevBtn = prevGO.GetComponent<Button>();
            if (_prevBtn != null) _prevBtn.onClick.AddListener(OnPrevBuilding);
            prevGO.SetActive(false);

            // Right arrow (> next)
            var nextGO = CreateNavArrowButton(frame, "NextBuildingBtn", "\u25B6", new Vector2(0.94f, 0.44f), new Vector2(1.02f, 0.56f));
            _nextBtn = nextGO.GetComponent<Button>();
            if (_nextBtn != null) _nextBtn.onClick.AddListener(OnNextBuilding);
            nextGO.SetActive(false);

            // Counter label ("2/5") at top-right of header
            var counterGO = new GameObject("NavCount");
            counterGO.transform.SetParent(frame, false);
            var counterRect = counterGO.AddComponent<RectTransform>();
            counterRect.anchorMin = new Vector2(0.75f, 0.90f);
            counterRect.anchorMax = new Vector2(0.98f, 0.98f);
            counterRect.offsetMin = Vector2.zero;
            counterRect.offsetMax = Vector2.zero;
            _navCountLabel = counterGO.AddComponent<Text>();
            _navCountLabel.text = "1/1";
            _navCountLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _navCountLabel.fontSize = 11;
            _navCountLabel.fontStyle = FontStyle.Bold;
            _navCountLabel.alignment = TextAnchor.MiddleRight;
            _navCountLabel.color = new Color(0.70f, 0.65f, 0.55f, 0.9f);
            _navCountLabel.raycastTarget = false;
            counterGO.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.75f);
            counterGO.GetComponent<Outline>().effectDistance = new Vector2(0.5f, -0.5f);
            counterGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.50f);
            counterGO.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.5f);
            counterGO.SetActive(false);
        }

        private static GameObject CreateNavArrowButton(Transform parent, string name, string symbol, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.14f, 0.90f);
            bg.raycastTarget = true;

            // Triple border: outer glow → gold → inner
            var outerGlow = go.AddComponent<Outline>();
            outerGlow.effectColor = new Color(0.90f, 0.72f, 0.28f, 0.25f);
            outerGlow.effectDistance = new Vector2(2f, -2f);
            var goldBorder = new GameObject("GoldBorder");
            goldBorder.transform.SetParent(go.transform, false);
            var gbRect = goldBorder.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = goldBorder.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            goldBorder.AddComponent<Outline>().effectColor = new Color(0.78f, 0.62f, 0.22f, 0.70f);
            goldBorder.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(go.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.45f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero; glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.06f);
            glImg.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = symbol;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.83f, 0.66f, 0.26f, 1f);
            text.raycastTarget = false;
            textGO.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.80f);
            textGO.GetComponent<Outline>().effectDistance = new Vector2(0.6f, -0.6f);

            return go;
        }

        private static void WireButton(Transform parent, string name, ref Button btn, UnityEngine.Events.UnityAction action)
        {
            var t = FindDeepChild(parent, name);
            if (t == null) return;
            btn = t.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(action);
        }

        private static void SetButtonLabel(Button btn, string label)
        {
            if (btn == null) return;
            var labelT = btn.transform.Find("Label");
            if (labelT != null)
            {
                var txt = labelT.GetComponent<Text>();
                if (txt != null) txt.text = label;
            }
        }

        private void CacheBuildingData()
        {
            _buildingDataCache = new Dictionary<string, BuildingData>();
            var allData = Resources.LoadAll<BuildingData>("");
            foreach (var data in allData)
            {
                if (!string.IsNullOrEmpty(data.buildingId))
                    _buildingDataCache[data.buildingId] = data;
            }
        }

        private static string GetActionLabel(string buildingId)
        {
            if (buildingId.Contains("barracks") || buildingId.Contains("training"))
                return "TRAIN";
            if (buildingId.Contains("academy") || buildingId.Contains("library") || buildingId.Contains("laboratory"))
                return "RESEARCH";
            if (IsResourceBuilding(buildingId))
                return "COLLECT ALL";
            if (buildingId.Contains("forge") || buildingId.Contains("armory"))
                return "CRAFT";
            return "UPGRADE";
        }

        private static bool IsResourceBuilding(string buildingId)
        {
            return buildingId != null && (buildingId.Contains("farm") || buildingId.Contains("mine")
                || buildingId.Contains("quarry") || buildingId.Contains("arcane_tower"));
        }

        /// <summary>
        /// P&C: Determines if a building upgrade is "Recommended" — shown for Stronghold
        /// (always a bottleneck) and buildings at tier 0 (not yet upgraded).
        /// </summary>
        private bool IsRecommendedUpgrade(string buildingId, int currentTier)
        {
            // Stronghold is always recommended (gates everything else)
            if (buildingId == "stronghold") return true;
            // Tier 0 buildings are recommended to upgrade first
            if (currentTier == 0) return true;
            // Buildings below Stronghold level - 2 are falling behind
            int shLevel = GetStrongholdLevel();
            if (shLevel >= 2 && currentTier < shLevel - 1) return true;
            return false;
        }

        /// <summary>P&C-style power estimate: tier × base power per building category.</summary>
        private static int EstimatePowerIncrease(string buildingId, int nextTier)
        {
            int basePower = 100;
            if (buildingId.Contains("stronghold")) basePower = 500;
            else if (buildingId.Contains("barracks") || buildingId.Contains("training")) basePower = 200;
            else if (buildingId.Contains("wall") || buildingId.Contains("tower") || buildingId.Contains("watch")) basePower = 150;
            else if (buildingId.Contains("forge") || buildingId.Contains("armory")) basePower = 180;
            return basePower * (nextTier + 1);
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
