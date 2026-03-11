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

        private EventSubscription _tapSub;
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
            _tapSub = EventBus.Subscribe<BuildingTappedEvent>(OnBuildingTapped);
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _cancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(OnUpgradeCancelled);
        }

        private void OnDisable()
        {
            _tapSub?.Dispose();
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
            if (_popup == null)
                FindPopupElements();
            if (_popup == null) return;

            _currentBuildingId = evt.BuildingId;
            _currentInstanceId = evt.InstanceId;
            _currentTier = evt.Tier;

            PopulatePopup(evt);
            _popup.SetActive(true);
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
                _nameLabel.text = displayName.ToUpper();

            if (_levelLabel != null)
                _levelLabel.text = $"Level {displayLevel}";

            if (_descLabel != null)
            {
                // P&C: Show power increase preview
                int powerIncrease = EstimatePowerIncrease(evt.BuildingId, evt.Tier + 1);
                string powerPreview = $"\n<color=#44FF66>+{FormatNumber(powerIncrease)} Power</color>";

                // P&C: Show bonus description from next tier's BuildingTierData
                string bonusLine = "";
                if (data != null)
                {
                    int nextTier = evt.Tier + 1;
                    BuildingTierData nextTierData = data.GetTier(nextTier);
                    if (nextTierData != null && !string.IsNullOrEmpty(nextTierData.bonusDescription))
                        bonusLine = $"\n<color=#FFD966>{nextTierData.bonusDescription}</color>";
                    else
                    {
                        // Show current tier bonus if at max level
                        BuildingTierData currentTierData = data.GetTier(evt.Tier);
                        if (currentTierData != null && !string.IsNullOrEmpty(currentTierData.bonusDescription))
                            bonusLine = $"\n<color=#FFD966>{currentTierData.bonusDescription}</color>";
                    }
                }

                if (data != null && !string.IsNullOrEmpty(data.description))
                    _descLabel.text = data.description + bonusLine + powerPreview;
                else
                    _descLabel.text = GetCategoryDescription(evt.BuildingId) + bonusLine + powerPreview;
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

        /// <summary>P&C: Format costs with checkmark (green) or X (red) based on current resources.</summary>
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

            string FormatRes(int cost, string name, ResourceType type)
            {
                if (cost <= 0) return null;
                bool canAfford = GetRes(type) >= cost;
                string mark = canAfford ? "<color=#44FF66>\u2713</color>" : "<color=#FF4444>\u2717</color>";
                return $"{mark} {FormatNumber(cost)} {name}";
            }

            var parts = new System.Collections.Generic.List<string>();
            var s = FormatRes(tier.stoneCost, "Stone", ResourceType.Stone);
            if (s != null) parts.Add(s);
            var i = FormatRes(tier.ironCost, "Iron", ResourceType.Iron);
            if (i != null) parts.Add(i);
            var g = FormatRes(tier.grainCost, "Grain", ResourceType.Grain);
            if (g != null) parts.Add(g);
            var a = FormatRes(tier.arcaneEssenceCost, "Arcane", ResourceType.ArcaneEssence);
            if (a != null) parts.Add(a);

            return parts.Count > 0 ? string.Join("  ", parts) : "Free";
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

        private int GetStrongholdLevel()
        {
            if (_buildingManager == null) return 99; // No manager = demo mode, allow all
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == "stronghold")
                    return kvp.Value.CurrentTier;
            }
            return 0;
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

        /// <summary>P&C: Demolish building with double-tap confirmation.</summary>
        private void OnDemolishPressed()
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_currentInstanceId)) return;
            if (_currentBuildingId == "stronghold") return;

            if (!_demolishConfirmPending)
            {
                _demolishConfirmPending = true;
                _demolishConfirmTimer = CancelConfirmTimeout;
                SetButtonLabel(_demolishBtn, "CONFIRM?");
                var btnImg = _demolishBtn?.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.color = new Color(0.95f, 0.15f, 0.15f, 1f);
                return;
            }

            // Second tap — actually demolish
            _demolishConfirmPending = false;
            bool demolished = _buildingManager.DemolishBuilding(_currentInstanceId);
            if (demolished)
            {
                Debug.Log($"[BuildingInfoPopup] Demolished {_currentBuildingId}.");
                ClosePopup();
            }
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
