using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style upgrade confirmation dialog. Shows resource costs (green/red for
    /// affordable/unaffordable), build time, and power increase before committing.
    /// Subscribes to UpgradeConfirmRequestedEvent.
    /// </summary>
    public class UpgradeConfirmDialog : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;
        private EventSubscription _requestSub;

        private GameObject _dialog;
        private string _pendingPlacedId;

        private static readonly Color DialogBg = new(0.06f, 0.04f, 0.10f, 0.96f);
        private static readonly Color DialogBorder = new(0.83f, 0.66f, 0.26f, 0.85f);
        private static readonly Color HeaderBg = new(0.12f, 0.08f, 0.18f, 0.95f);
        private static readonly Color ConfirmGreen = new(0.20f, 0.65f, 0.30f, 1f);
        private static readonly Color ConfirmLocked = new(0.35f, 0.30f, 0.28f, 0.80f);
        private static readonly Color CancelColor = new(0.35f, 0.30f, 0.25f, 0.90f);
        private static readonly Color GoldText = new(0.83f, 0.66f, 0.26f, 1f);
        private static readonly Color AffordColor = new(0.45f, 0.92f, 0.45f, 1f);
        private static readonly Color CantAffordColor = new(0.95f, 0.35f, 0.30f, 1f);
        private static readonly Color SubText = new(0.70f, 0.65f, 0.58f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
            ServiceLocator.TryGet(out _resourceManager);
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            _requestSub = EventBus.Subscribe<UpgradeConfirmRequestedEvent>(OnUpgradeRequested);
        }

        private void OnDisable()
        {
            _requestSub?.Dispose();
        }

        private void OnUpgradeRequested(UpgradeConfirmRequestedEvent evt)
        {
            _pendingPlacedId = evt.PlacedId;
            ShowDialog(evt.PlacedId, evt.BuildingId, evt.CurrentTier);
        }

        private void ShowDialog(string placedId, string buildingId, int currentTier)
        {
            if (_canvasRect == null || _buildingManager == null) return;
            CloseDialog();

            // Look up building data
            if (!_buildingManager.PlacedBuildings.TryGetValue(placedId, out PlacedBuilding placed))
                return;

            int targetTier = currentTier + 1;
            BuildingTierData tierData = placed.Data.GetTier(targetTier);
            if (tierData == null) return;

            // Calculate affordability
            bool hasStone = _resourceManager != null && _resourceManager.Stone >= tierData.stoneCost;
            bool hasIron = _resourceManager != null && _resourceManager.Iron >= tierData.ironCost;
            bool hasGrain = _resourceManager != null && _resourceManager.Grain >= tierData.grainCost;
            bool hasArcane = _resourceManager != null && _resourceManager.ArcaneEssence >= tierData.arcaneEssenceCost;
            bool canAfford = hasStone && hasIron && hasGrain && hasArcane;

            // Build time (capped)
            int maxTime = placed.Data.category == BuildingCategory.Core
                ? BuildingManager.MaxBuildTimeSecondsStronghold
                : BuildingManager.MaxBuildTimeSecondsOther;
            int buildTime = Mathf.Min(tierData.buildTimeSeconds, maxTime);

            // Queue check
            bool queueFull = _buildingManager.BuildQueue.Count >= 2;
            bool alreadyQueued = false;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == placedId) { alreadyQueued = true; break; }
            }

            _dialog = new GameObject("UpgradeConfirmDialog");
            _dialog.transform.SetParent(_canvasRect, false);
            _dialog.transform.SetAsLastSibling();

            // Full-screen overlay
            var overlay = CreateChild(_dialog, "Overlay", Vector2.zero, Vector2.one);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.55f);
            var overlayBtn = overlay.AddComponent<Button>();
            overlayBtn.onClick.AddListener(CloseDialog);

            // Dialog panel
            var panel = CreateChild(_dialog, "Panel",
                new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.75f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = DialogBg;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = DialogBorder;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Header bar
            var header = CreateChild(panel, "Header",
                new Vector2(0f, 0.82f), new Vector2(1f, 1f));
            var headerImg = header.AddComponent<Image>();
            headerImg.color = HeaderBg;
            var headerOutline = header.AddComponent<Outline>();
            headerOutline.effectColor = DialogBorder;
            headerOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title: "Upgrade [Building Name]"
            string displayName = placed.Data.displayName ?? buildingId.Replace('_', ' ');
            string titleStr = $"UPGRADE {displayName.ToUpper()}";
            AddLabel(header, "Title", titleStr, 14, FontStyle.Bold, GoldText,
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f));

            // Level indicator
            string levelStr = $"Level {currentTier} \u2192 Level {targetTier}";
            AddLabel(panel, "Level", levelStr, 13, FontStyle.Bold, Color.white,
                new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.82f));

            // Resource cost section
            float costY = 0.64f;
            float costH = 0.08f;
            float costGap = 0.005f;

            // Stone
            if (tierData.stoneCost > 0)
            {
                AddCostRow(panel, "Stone", "\u25A3 Stone", tierData.stoneCost,
                    _resourceManager?.Stone ?? 0, hasStone, costY);
                costY -= costH + costGap;
            }
            // Iron
            if (tierData.ironCost > 0)
            {
                AddCostRow(panel, "Iron", "\u2692 Iron", tierData.ironCost,
                    _resourceManager?.Iron ?? 0, hasIron, costY);
                costY -= costH + costGap;
            }
            // Grain
            if (tierData.grainCost > 0)
            {
                AddCostRow(panel, "Grain", "\u2E3A Grain", tierData.grainCost,
                    _resourceManager?.Grain ?? 0, hasGrain, costY);
                costY -= costH + costGap;
            }
            // Arcane Essence
            if (tierData.arcaneEssenceCost > 0)
            {
                AddCostRow(panel, "Arcane", "\u2726 Arcane", tierData.arcaneEssenceCost,
                    _resourceManager?.ArcaneEssence ?? 0, hasArcane, costY);
                costY -= costH + costGap;
            }

            // Build time
            string timeStr = FormatTime(buildTime);
            AddLabel(panel, "BuildTime", $"\u23F1 Build Time: {timeStr}", 11, FontStyle.Normal,
                SubText, new Vector2(0.05f, costY - 0.02f), new Vector2(0.95f, costY + 0.06f));
            costY -= 0.08f;

            // Bonus description
            if (!string.IsNullOrEmpty(tierData.bonusDescription))
            {
                AddLabel(panel, "Bonus", $"\u2728 {tierData.bonusDescription}", 10, FontStyle.Italic,
                    new Color(0.60f, 0.80f, 0.95f, 0.9f),
                    new Vector2(0.05f, costY - 0.02f), new Vector2(0.95f, costY + 0.06f));
            }

            // Status message if queue full or already queued
            if (alreadyQueued)
            {
                AddLabel(panel, "Status", "Already in build queue!", 11, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.20f));
            }
            else if (queueFull)
            {
                AddLabel(panel, "Status", "Build queue is full (2/2)", 11, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.20f));
            }
            else if (!canAfford)
            {
                AddLabel(panel, "Status", "Insufficient Resources", 11, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.20f));
            }

            // Confirm button
            bool canConfirm = canAfford && !queueFull && !alreadyQueued;
            var confirmGO = CreateChild(panel, "ConfirmBtn",
                new Vector2(0.52f, 0.02f), new Vector2(0.95f, 0.12f));
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = canConfirm ? ConfirmGreen : ConfirmLocked;
            var confirmOutline = confirmGO.AddComponent<Outline>();
            confirmOutline.effectColor = canConfirm
                ? new Color(0.30f, 0.85f, 0.40f, 0.7f)
                : new Color(0.30f, 0.28f, 0.25f, 0.4f);
            confirmOutline.effectDistance = new Vector2(1f, -1f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.interactable = canConfirm;
            confirmBtn.onClick.AddListener(OnConfirm);
            AddLabel(confirmGO, "Label", "UPGRADE", 13, FontStyle.Bold,
                canConfirm ? Color.white : new Color(0.6f, 0.55f, 0.5f),
                Vector2.zero, Vector2.one);

            // Cancel button
            var cancelGO = CreateChild(panel, "CancelBtn",
                new Vector2(0.05f, 0.02f), new Vector2(0.48f, 0.12f));
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = CancelColor;
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(CloseDialog);
            AddLabel(cancelGO, "Label", "CANCEL", 13, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one);
        }

        private void AddCostRow(GameObject parent, string name, string label,
            int required, long current, bool affordable, float yTop)
        {
            float yBot = yTop - 0.08f;
            // Resource icon + name
            AddLabel(parent, $"Cost_{name}_Label", label, 11, FontStyle.Normal,
                SubText, new Vector2(0.05f, yBot), new Vector2(0.35f, yTop));

            // Required amount
            Color amtColor = affordable ? AffordColor : CantAffordColor;
            string amtStr = $"{FormatNumber(required)}";
            AddLabel(parent, $"Cost_{name}_Req", amtStr, 12, FontStyle.Bold,
                amtColor, new Vector2(0.36f, yBot), new Vector2(0.60f, yTop));

            // Current / Required
            string curStr = $"({FormatNumber(current)})";
            AddLabel(parent, $"Cost_{name}_Cur", curStr, 10, FontStyle.Normal,
                new Color(SubText.r, SubText.g, SubText.b, 0.7f),
                new Vector2(0.61f, yBot), new Vector2(0.95f, yTop));
        }

        private void OnConfirm()
        {
            if (_buildingManager != null && !string.IsNullOrEmpty(_pendingPlacedId))
            {
                bool started = _buildingManager.StartUpgrade(_pendingPlacedId);
                if (started)
                    Debug.Log($"[UpgradeConfirm] Upgrade started for {_pendingPlacedId}.");
                else
                    Debug.LogWarning($"[UpgradeConfirm] Upgrade failed for {_pendingPlacedId}.");
            }
            CloseDialog();
        }

        private void CloseDialog()
        {
            if (_dialog != null)
            {
                Destroy(_dialog);
                _dialog = null;
            }
            _pendingPlacedId = null;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static GameObject CreateChild(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static void AddLabel(GameObject parent, string name, string text,
            int fontSize, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = CreateChild(parent, name, anchorMin, anchorMax);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = color;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
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

        private static string FormatNumber(long val)
        {
            if (val >= 1_000_000) return $"{val / 1_000_000f:F1}M";
            if (val >= 1_000) return $"{val / 1_000f:F1}K";
            return val.ToString();
        }
    }

    /// <summary>
    /// Published by BuildingQuickActionMenu when player taps Upgrade.
    /// UpgradeConfirmDialog subscribes and shows confirmation.
    /// </summary>
    public readonly struct UpgradeConfirmRequestedEvent
    {
        public readonly string PlacedId;
        public readonly string BuildingId;
        public readonly int CurrentTier;
        public UpgradeConfirmRequestedEvent(string pid, string bid, int tier)
        { PlacedId = pid; BuildingId = bid; CurrentTier = tier; }
    }
}
