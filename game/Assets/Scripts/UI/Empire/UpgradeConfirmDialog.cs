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

            // Full-screen overlay (darker for premium feel)
            var overlay = CreateChild(_dialog, "Overlay", Vector2.zero, Vector2.one);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.65f);
            var overlayBtn = overlay.AddComponent<Button>();
            overlayBtn.onClick.AddListener(CloseDialog);

            // === MAIN PANEL: ornate gold-bordered frame ===
            var panel = CreateChild(_dialog, "Panel",
                new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.82f));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = DialogBg;
            // Triple border: outer glow → gold → inner
            var panelGlow = panel.AddComponent<Shadow>();
            panelGlow.effectColor = new Color(0.83f, 0.66f, 0.26f, 0.20f);
            panelGlow.effectDistance = new Vector2(3f, -3f);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = DialogBorder;
            panelOutline.effectDistance = new Vector2(2f, -2f);
            // Ornate sprite if available
            var ornateSpr = Resources.Load<Sprite>("UI/Generated/panel_ornate_gen");
            if (ornateSpr != null) { panelImg.sprite = ornateSpr; panelImg.type = Image.Type.Sliced;
                panelImg.color = new Color(0.65f, 0.55f, 0.38f, 1f); }

            // Inner fill for text contrast
            var innerFill = CreateChild(panel, "InnerFill",
                new Vector2(0.015f, 0.012f), new Vector2(0.985f, 0.988f));
            var innerImg = innerFill.AddComponent<Image>();
            innerImg.color = new Color(0.05f, 0.03f, 0.09f, 0.96f);
            innerImg.raycastTarget = false;
            // Warm top edge glow
            var edgeTop = CreateChild(innerFill, "EdgeTop",
                new Vector2(0f, 0.93f), new Vector2(1f, 1f));
            edgeTop.AddComponent<Image>().color = new Color(0.83f, 0.66f, 0.26f, 0.07f);
            edgeTop.GetComponent<Image>().raycastTarget = false;

            // === HEADER BAND: dark purple with gold accents ===
            var header = CreateChild(innerFill, "Header",
                new Vector2(0f, 0.87f), new Vector2(1f, 1f));
            var headerImg = header.AddComponent<Image>();
            headerImg.color = new Color(0.10f, 0.06f, 0.18f, 1f);
            // Glass highlight on header
            var headerGlass = CreateChild(header, "Glass",
                new Vector2(0f, 0.50f), new Vector2(1f, 1f));
            headerGlass.AddComponent<Image>().color = new Color(0.40f, 0.30f, 0.55f, 0.10f);
            headerGlass.GetComponent<Image>().raycastTarget = false;

            // Title: "UPGRADE [BUILDING NAME]"
            string displayName = placed.Data.displayName ?? buildingId.Replace('_', ' ');
            string titleStr = $"UPGRADE {displayName.ToUpper()}";
            var titleGO = CreateChild(header, "Title",
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f));
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = titleStr;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.88f, 0.52f, 1f); // Bright warm gold
            titleText.raycastTarget = false;
            var titleOutline = titleGO.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.45f, 0.30f, 0.08f, 0.85f);
            titleOutline.effectDistance = new Vector2(1f, -1f);
            var titleShadow = titleGO.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.95f);
            titleShadow.effectDistance = new Vector2(1.2f, -1.2f);

            // Gold separator below header
            var headerSep = CreateChild(innerFill, "HeaderSep",
                new Vector2(0.05f, 0.865f), new Vector2(0.95f, 0.872f));
            headerSep.AddComponent<Image>().color = new Color(0.83f, 0.66f, 0.26f, 0.55f);
            headerSep.GetComponent<Image>().raycastTarget = false;

            // === LEVEL INDICATOR: ornate level transition ===
            string levelStr = $"Level {currentTier}  \u2794  Level {targetTier}";
            var levelGO = CreateChild(innerFill, "Level",
                new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.86f));
            var levelText = levelGO.AddComponent<Text>();
            levelText.text = levelStr;
            levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            levelText.fontSize = 15;
            levelText.fontStyle = FontStyle.Bold;
            levelText.alignment = TextAnchor.MiddleCenter;
            levelText.color = Color.white;
            levelText.raycastTarget = false;
            var levelOutline = levelGO.AddComponent<Outline>();
            levelOutline.effectColor = new Color(0, 0, 0, 0.70f);
            levelOutline.effectDistance = new Vector2(0.8f, -0.8f);
            levelGO.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.80f);
            levelGO.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

            // === COST SECTION: recessed panel with ornate header ===
            // Section header
            var costHeaderSep = CreateChild(innerFill, "CostSep",
                new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.745f));
            costHeaderSep.AddComponent<Image>().color = new Color(0.83f, 0.66f, 0.26f, 0.30f);
            costHeaderSep.GetComponent<Image>().raycastTarget = false;

            AddLabel(innerFill, "CostLabel", "\u2726  UPGRADE COST  \u2726", 12, FontStyle.Bold,
                new Color(0.90f, 0.72f, 0.30f, 1f),
                new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.74f), TextAnchor.MiddleCenter);

            // Cost rows in recessed panel
            var costPanel = CreateChild(innerFill, "CostPanel",
                new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.68f));
            var costPanelImg = costPanel.AddComponent<Image>();
            costPanelImg.color = new Color(0.04f, 0.03f, 0.07f, 0.45f);
            costPanelImg.raycastTarget = false;
            costPanel.AddComponent<Outline>().effectColor = new Color(0.40f, 0.32f, 0.15f, 0.18f);
            costPanel.GetComponent<Outline>().effectDistance = new Vector2(0.6f, -0.6f);

            float costY = 0.88f; // relative to costPanel
            float costH = 0.18f;
            float costGap = 0.02f;

            if (tierData.stoneCost > 0)
            {
                AddCostRow(costPanel, "Stone", "\u25C8 Stone", tierData.stoneCost,
                    _resourceManager?.Stone ?? 0, hasStone, costY);
                costY -= costH + costGap;
            }
            if (tierData.ironCost > 0)
            {
                AddCostRow(costPanel, "Iron", "\u2666 Iron", tierData.ironCost,
                    _resourceManager?.Iron ?? 0, hasIron, costY);
                costY -= costH + costGap;
            }
            if (tierData.grainCost > 0)
            {
                AddCostRow(costPanel, "Grain", "\u2740 Grain", tierData.grainCost,
                    _resourceManager?.Grain ?? 0, hasGrain, costY);
                costY -= costH + costGap;
            }
            if (tierData.arcaneEssenceCost > 0)
            {
                AddCostRow(costPanel, "Arcane", "\u2726 Arcane", tierData.arcaneEssenceCost,
                    _resourceManager?.ArcaneEssence ?? 0, hasArcane, costY);
                costY -= costH + costGap;
            }

            // === BUILD TIME + BONUS ===
            string timeStr = FormatTime(buildTime);
            AddLabel(innerFill, "BuildTime", $"\u23F1  Build Time:  {timeStr}", 12, FontStyle.Bold,
                new Color(0.50f, 0.75f, 1f, 1f),
                new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.34f), TextAnchor.MiddleCenter);

            if (!string.IsNullOrEmpty(tierData.bonusDescription))
            {
                AddLabel(innerFill, "Bonus", $"\u2728 {tierData.bonusDescription}", 11, FontStyle.Italic,
                    new Color(0.55f, 0.80f, 0.95f, 0.90f),
                    new Vector2(0.05f, 0.19f), new Vector2(0.95f, 0.26f), TextAnchor.MiddleCenter);
            }

            // === STATUS MESSAGE ===
            if (alreadyQueued)
            {
                AddLabel(innerFill, "Status", "\u26A0 Already in build queue!", 12, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.19f),
                    TextAnchor.MiddleCenter);
            }
            else if (queueFull)
            {
                AddLabel(innerFill, "Status", "\u26A0 Build queue is full (2/2)", 12, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.19f),
                    TextAnchor.MiddleCenter);
            }
            else if (!canAfford)
            {
                AddLabel(innerFill, "Status", "\u26A0 Insufficient Resources", 12, FontStyle.Bold,
                    CantAffordColor, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.19f),
                    TextAnchor.MiddleCenter);
            }

            // === BUTTONS: Premium ornate style ===
            bool canConfirm = canAfford && !queueFull && !alreadyQueued;

            // CONFIRM button — wide green with glow
            var confirmGO = CreateChild(innerFill, "ConfirmBtn",
                new Vector2(0.52f, 0.02f), new Vector2(0.97f, 0.11f));
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = canConfirm ? ConfirmGreen : ConfirmLocked;
            var confirmGlow = confirmGO.AddComponent<Shadow>();
            confirmGlow.effectColor = canConfirm
                ? new Color(0.25f, 0.80f, 0.35f, 0.30f)
                : new Color(0, 0, 0, 0);
            confirmGlow.effectDistance = new Vector2(0, -2f);
            var confirmOutline = confirmGO.AddComponent<Outline>();
            confirmOutline.effectColor = canConfirm
                ? new Color(0.30f, 0.88f, 0.45f, 0.60f)
                : new Color(0.30f, 0.28f, 0.25f, 0.30f);
            confirmOutline.effectDistance = new Vector2(1.2f, -1.2f);
            // Glass highlight
            var confGlass = CreateChild(confirmGO, "Glass",
                new Vector2(0f, 0.50f), new Vector2(1f, 1f));
            confGlass.AddComponent<Image>().color = canConfirm
                ? new Color(0.55f, 1f, 0.65f, 0.10f)
                : new Color(0.40f, 0.38f, 0.35f, 0.05f);
            confGlass.GetComponent<Image>().raycastTarget = false;
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.interactable = canConfirm;
            confirmBtn.onClick.AddListener(OnConfirm);
            var confLabel = CreateChild(confirmGO, "Label", Vector2.zero, Vector2.one);
            var confText = confLabel.AddComponent<Text>();
            confText.text = "UPGRADE";
            confText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            confText.fontSize = 14;
            confText.fontStyle = FontStyle.Bold;
            confText.alignment = TextAnchor.MiddleCenter;
            confText.color = canConfirm ? Color.white : new Color(0.55f, 0.50f, 0.45f, 1f);
            confText.raycastTarget = false;
            confLabel.AddComponent<Outline>().effectColor = new Color(0.05f, 0.25f, 0.08f, canConfirm ? 0.75f : 0f);
            confLabel.GetComponent<Outline>().effectDistance = new Vector2(0.8f, -0.8f);
            confLabel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.90f);
            confLabel.GetComponent<Shadow>().effectDistance = new Vector2(0.8f, -0.8f);

            // CANCEL button — subtle dark with outline
            var cancelGO = CreateChild(innerFill, "CancelBtn",
                new Vector2(0.03f, 0.02f), new Vector2(0.48f, 0.11f));
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.22f, 0.18f, 0.28f, 1f);
            cancelGO.AddComponent<Outline>().effectColor = new Color(0.40f, 0.35f, 0.50f, 0.40f);
            cancelGO.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            // Glass highlight
            var canGlass = CreateChild(cancelGO, "Glass",
                new Vector2(0f, 0.50f), new Vector2(1f, 1f));
            canGlass.AddComponent<Image>().color = new Color(0.50f, 0.45f, 0.60f, 0.08f);
            canGlass.GetComponent<Image>().raycastTarget = false;
            var cancelBtnComp = cancelGO.AddComponent<Button>();
            cancelBtnComp.targetGraphic = cancelImg;
            cancelBtnComp.onClick.AddListener(CloseDialog);
            var canLabel = CreateChild(cancelGO, "Label", Vector2.zero, Vector2.one);
            var canText = canLabel.AddComponent<Text>();
            canText.text = "CANCEL";
            canText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canText.fontSize = 13;
            canText.fontStyle = FontStyle.Bold;
            canText.alignment = TextAnchor.MiddleCenter;
            canText.color = new Color(0.80f, 0.75f, 0.85f, 1f);
            canText.raycastTarget = false;
            canLabel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.80f);
            canLabel.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

            // Decorative corner accents
            float cs = 0.025f;
            float[][] cornerPositions = { new[]{0.01f, 0.96f}, new[]{0.96f, 0.96f}, new[]{0.01f, 0.01f}, new[]{0.96f, 0.01f} };
            for (int ci = 0; ci < cornerPositions.Length; ci++)
            {
                var ca = CreateChild(innerFill, $"Corner_{ci}",
                    new Vector2(cornerPositions[ci][0], cornerPositions[ci][1]),
                    new Vector2(cornerPositions[ci][0] + cs, cornerPositions[ci][1] + cs));
                ca.AddComponent<Image>().color = new Color(0.83f, 0.66f, 0.26f, 0.30f);
                ca.GetComponent<Image>().raycastTarget = false;
                ca.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        private void AddCostRow(GameObject parent, string name, string label,
            int required, long current, bool affordable, float yTop)
        {
            float yBot = yTop - 0.18f;

            // Subtle row bg for contrast
            var rowBg = CreateChild(parent, $"Cost_{name}_Bg",
                new Vector2(0.02f, yBot), new Vector2(0.98f, yTop));
            rowBg.AddComponent<Image>().color = affordable
                ? new Color(0.15f, 0.30f, 0.15f, 0.10f)
                : new Color(0.30f, 0.10f, 0.10f, 0.10f);
            rowBg.GetComponent<Image>().raycastTarget = false;

            // Check mark or X
            string checkStr = affordable ? "\u2713" : "\u2717";
            Color checkCol = affordable ? AffordColor : CantAffordColor;
            AddLabel(parent, $"Cost_{name}_Check", checkStr, 14, FontStyle.Bold,
                checkCol, new Vector2(0.04f, yBot), new Vector2(0.10f, yTop));

            // Resource icon + name
            AddLabel(parent, $"Cost_{name}_Label", label, 12, FontStyle.Normal,
                new Color(0.80f, 0.75f, 0.68f, 1f),
                new Vector2(0.11f, yBot), new Vector2(0.40f, yTop));

            // Required amount
            Color amtColor = affordable ? AffordColor : CantAffordColor;
            AddLabel(parent, $"Cost_{name}_Req", FormatNumber(required), 13, FontStyle.Bold,
                amtColor, new Vector2(0.42f, yBot), new Vector2(0.62f, yTop));

            // Current stock in parentheses
            string curStr = $"/ {FormatNumber(current)}";
            AddLabel(parent, $"Cost_{name}_Cur", curStr, 10, FontStyle.Normal,
                new Color(SubText.r, SubText.g, SubText.b, 0.75f),
                new Vector2(0.63f, yBot), new Vector2(0.96f, yTop));
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
            int fontSize, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = CreateChild(parent, name, anchorMin, anchorMax);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.55f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.85f);
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
