using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality gem speedup confirmation dialog with ornate triple border,
    /// glass highlight, corner accents, ornate buttons, and elastic pop-in.
    /// Subscribes to SpeedupRequestedEvent, shows cost and confirm/cancel.
    /// </summary>
    public class SpeedupConfirmDialog : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private BuildingManager _buildingManager;
        private EventSubscription _requestSub;

        private GameObject _dialog;
        private GameObject _panel;
        private Text _messageText;
        private Text _costText;
        private string _pendingPlacedId;
        private int _pendingGemCost;

        private static readonly Color DialogBg = new(0.06f, 0.04f, 0.10f, 0.96f);
        private static readonly Color GoldBorder = new(0.83f, 0.66f, 0.26f, 0.75f);
        private static readonly Color GoldBorderGlow = new(0.90f, 0.72f, 0.28f, 0.25f);
        private static readonly Color ConfirmColor = new(0.55f, 0.35f, 0.85f, 1f);
        private static readonly Color ConfirmGlow = new(0.65f, 0.45f, 0.95f, 0.35f);
        private static readonly Color CancelColor = new(0.30f, 0.28f, 0.25f, 0.90f);
        private static readonly Color GoldText = new(0.92f, 0.87f, 0.72f, 1f);
        private static readonly Color GemColor = new(0.70f, 0.50f, 0.95f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            _requestSub = EventBus.Subscribe<SpeedupRequestedEvent>(OnSpeedupRequested);
        }

        private void OnDisable()
        {
            _requestSub?.Dispose();
        }

        private void OnSpeedupRequested(SpeedupRequestedEvent evt)
        {
            _pendingPlacedId = evt.PlacedId;
            _pendingGemCost = evt.GemCost;
            ShowDialog(evt.GemCost, evt.RemainingSeconds);
        }

        private void ShowDialog(int gemCost, float remainingSeconds)
        {
            if (_canvasRect == null) return;
            CloseDialog();

            _dialog = new GameObject("SpeedupConfirmDialog");
            _dialog.transform.SetParent(_canvasRect, false);
            _dialog.transform.SetAsLastSibling();

            // Full-screen overlay
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(_dialog.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.70f);
            overlay.AddComponent<Button>().onClick.AddListener(CloseDialog);

            // Dialog panel
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_dialog.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.30f);
            panelRect.anchorMax = new Vector2(0.90f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = DialogBg;

            // Triple border
            var outerGlow = _panel.AddComponent<Outline>();
            outerGlow.effectColor = GoldBorderGlow;
            outerGlow.effectDistance = new Vector2(3f, -3f);
            AddBorderLayer(_panel.transform, GoldBorder, new Vector2(1.5f, -1.5f));
            AddBorderLayer(_panel.transform, new Color(0.55f, 0.40f, 0.85f, 0.30f), new Vector2(0.7f, -0.7f));

            // Glass highlight
            AddGlass(_panel.transform, new Vector2(0f, 0.48f), Vector2.one, 0.04f);

            // Warm bottom glow (gem purple tint)
            var warmBot = new GameObject("WarmBot");
            warmBot.transform.SetParent(_panel.transform, false);
            var wbRect = warmBot.AddComponent<RectTransform>();
            wbRect.anchorMin = Vector2.zero;
            wbRect.anchorMax = new Vector2(1f, 0.12f);
            wbRect.offsetMin = Vector2.zero; wbRect.offsetMax = Vector2.zero;
            var wbImg = warmBot.AddComponent<Image>();
            wbImg.color = new Color(0.55f, 0.35f, 0.85f, 0.06f);
            wbImg.raycastTarget = false;

            // Corner diamond accents
            float cs = 0.04f;
            float[][] corners = { new[]{0.01f, 0.96f}, new[]{0.96f, 0.96f}, new[]{0.01f, 0.01f}, new[]{0.96f, 0.01f} };
            for (int ci = 0; ci < corners.Length; ci++)
                CreateCornerDiamond(_panel.transform, new Vector2(corners[ci][0], corners[ci][1]),
                    new Vector2(corners[ci][0] + cs, corners[ci][1] + cs));

            // Header strip
            var headerBg = new GameObject("HeaderBg");
            headerBg.transform.SetParent(_panel.transform, false);
            var hRect = headerBg.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.02f, 0.80f);
            hRect.anchorMax = new Vector2(0.98f, 0.97f);
            hRect.offsetMin = Vector2.zero; hRect.offsetMax = Vector2.zero;
            var hImg = headerBg.AddComponent<Image>();
            hImg.color = new Color(0.12f, 0.08f, 0.20f, 0.85f);
            hImg.raycastTarget = false;

            // Title
            AddOrnateText(_panel.transform, "Title", "\u2666 SPEED UP \u2666",
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.97f), 15, FontStyle.Bold, GoldText);

            // Gold separator
            var sep = new GameObject("Sep");
            sep.transform.SetParent(_panel.transform, false);
            var sepRect = sep.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.06f, 0.79f);
            sepRect.anchorMax = new Vector2(0.94f, 0.795f);
            sepRect.offsetMin = Vector2.zero; sepRect.offsetMax = Vector2.zero;
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.40f);
            sepImg.raycastTarget = false;

            // Message
            string timeStr = FormatTime(Mathf.CeilToInt(remainingSeconds));
            AddOrnateText(_panel.transform, "Message",
                $"Complete upgrade instantly?\nTime remaining: <color=#FFD966>{timeStr}</color>",
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.78f), 11, FontStyle.Normal,
                new Color(0.80f, 0.75f, 0.68f, 1f), true);

            // P&C: Speedup item shortcuts (5m, 15m, 1h, 3h)
            float[] itemMinutes = { 5, 15, 60, 180 };
            string[] itemLabels = { "5m", "15m", "1h", "3h" };
            float itemBtnWidth = 0.22f;
            float itemStartX = 0.03f;
            for (int idx = 0; idx < itemMinutes.Length; idx++)
            {
                float mins = itemMinutes[idx];
                string label = itemLabels[idx];
                bool hasItem = mins <= 60;
                Color itemBg = hasItem ? new Color(0.18f, 0.45f, 0.22f, 0.90f) : new Color(0.20f, 0.18f, 0.22f, 0.60f);
                Color itemGlow = hasItem ? new Color(0.30f, 0.70f, 0.35f, 0.30f) : new Color(0.30f, 0.28f, 0.32f, 0.15f);
                float xStart = itemStartX + idx * (itemBtnWidth + 0.015f);
                var itemGO = CreateOrnateButton(_panel.transform, $"SpeedItem_{label}",
                    new Vector2(xStart, 0.44f), new Vector2(xStart + itemBtnWidth, 0.57f),
                    $"\u23F1{label}", itemBg, itemGlow);
                var itemBtn = itemGO.GetComponent<Button>();
                itemBtn.interactable = hasItem;
                float capturedMins = mins;
                itemBtn.onClick.AddListener(() => OnUseSpeedupItem(capturedMins));
                var itemLblText = itemGO.GetComponentInChildren<Text>();
                if (itemLblText != null)
                    itemLblText.color = hasItem ? Color.white : new Color(0.50f, 0.48f, 0.45f, 0.7f);
            }

            // Gem cost display
            AddOrnateText(_panel.transform, "Cost", $"\u2666 {gemCost} Gems",
                new Vector2(0.20f, 0.28f), new Vector2(0.80f, 0.42f), 17, FontStyle.Bold, GemColor);

            // Confirm button (gem purple, ornate)
            var confirmGO = CreateOrnateButton(_panel.transform, "ConfirmBtn",
                new Vector2(0.52f, 0.04f), new Vector2(0.96f, 0.25f),
                "USE GEMS", ConfirmColor, ConfirmGlow);
            confirmGO.GetComponent<Button>().onClick.AddListener(OnConfirm);

            // Cancel button (muted, ornate)
            var cancelGO = CreateOrnateButton(_panel.transform, "CancelBtn",
                new Vector2(0.04f, 0.04f), new Vector2(0.48f, 0.25f),
                "CANCEL", CancelColor, new Color(0.50f, 0.45f, 0.40f, 0.20f));
            cancelGO.GetComponent<Button>().onClick.AddListener(CloseDialog);

            // Elastic pop-in
            StartCoroutine(PopInPanel());
        }

        private IEnumerator PopInPanel()
        {
            if (_panel == null) yield break;
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = t < 0.55f
                    ? Mathf.Lerp(0f, 1.12f, t / 0.55f)
                    : Mathf.Lerp(1.12f, 1f, (t - 0.55f) / 0.45f);
                if (_panel != null) _panel.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (_panel != null) _panel.transform.localScale = Vector3.one;
        }

        private void OnUseSpeedupItem(float minutes)
        {
            if (_buildingManager == null || string.IsNullOrEmpty(_pendingPlacedId)) return;
            int seconds = Mathf.CeilToInt(minutes * 60f);
            _buildingManager.ApplySpeedup(_pendingPlacedId, seconds);
            Debug.Log($"[SpeedupConfirm] Used {minutes}m speedup item on {_pendingPlacedId}.");
            CloseDialog();
        }

        private void OnConfirm()
        {
            if (_buildingManager != null && !string.IsNullOrEmpty(_pendingPlacedId))
            {
                foreach (var entry in _buildingManager.BuildQueue)
                {
                    if (entry.PlacedId == _pendingPlacedId)
                    {
                        _buildingManager.ApplySpeedup(_pendingPlacedId, Mathf.CeilToInt(entry.RemainingSeconds));
                        break;
                    }
                }
                Debug.Log($"[SpeedupConfirm] Applied gem speedup to {_pendingPlacedId} for {_pendingGemCost} gems.");
            }
            CloseDialog();
        }

        private void CloseDialog()
        {
            if (_dialog != null) { Destroy(_dialog); _dialog = null; }
            _panel = null;
            _pendingPlacedId = null;
        }

        private static void AddBorderLayer(Transform parent, Color borderColor, Vector2 dist)
        {
            var go = new GameObject("Border");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); img.raycastTarget = false;
            go.AddComponent<Outline>().effectColor = borderColor;
            go.GetComponent<Outline>().effectDistance = dist;
        }

        private static void AddGlass(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float alpha)
        {
            var go = new GameObject("Glass");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        private static void CreateCornerDiamond(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Corner");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0, 0, 45f);
            var img = go.AddComponent<Image>();
            img.color = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.50f);
            img.raycastTarget = false;
        }

        private static void AddOrnateText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, FontStyle style, Color color, bool richText = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = richText;
            go.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            go.GetComponent<Outline>().effectDistance = new Vector2(0.7f, -0.7f);
            go.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.60f);
            go.GetComponent<Shadow>().effectDistance = new Vector2(0.4f, -0.7f);
        }

        private static GameObject CreateOrnateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string label, Color bgColor, Color glowColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;
            go.AddComponent<Outline>().effectColor = glowColor;
            go.GetComponent<Outline>().effectDistance = new Vector2(1.8f, -1.8f);
            AddBorderLayer(go.transform, new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.45f),
                new Vector2(0.8f, -0.8f));
            AddGlass(go.transform, new Vector2(0f, 0.45f), Vector2.one, 0.07f);
            go.AddComponent<Button>().targetGraphic = bg;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lRect = labelGO.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero; lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero; lRect.offsetMax = Vector2.zero;
            var t = labelGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            labelGO.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.80f);
            labelGO.GetComponent<Outline>().effectDistance = new Vector2(0.6f, -0.6f);
            return go;
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "0:00";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}:{m:D2}:{s:D2}";
            return $"{m}:{s:D2}";
        }
    }
}
