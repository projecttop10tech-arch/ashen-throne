using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style demolish confirmation popup. Shows building name, tier, and a
    /// warning that this action is irreversible. Replaces the inline double-tap confirm.
    /// Triggered by DemolishRequestedEvent.
    /// </summary>
    public class DemolishConfirmDialog : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _popup;
        private EventSubscription _sub;

        private string _pendingInstanceId;
        private string _pendingBuildingId;

        private static readonly Color WarningRed = new(0.85f, 0.20f, 0.18f, 1f);
        private static readonly Color PanelBg = new(0.06f, 0.04f, 0.10f, 0.96f);
        private static readonly Color GoldBorder = new(0.78f, 0.62f, 0.22f, 1f);

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _sub = EventBus.Subscribe<DemolishRequestedEvent>(OnDemolishRequested);
        }

        private void OnDisable()
        {
            _sub?.Dispose();
        }

        private void OnDemolishRequested(DemolishRequestedEvent evt)
        {
            _pendingInstanceId = evt.InstanceId;
            _pendingBuildingId = evt.BuildingId;
            ShowDialog(evt.BuildingId, evt.Tier);
        }

        private void ShowDialog(string buildingId, int tier)
        {
            if (_popup != null) Destroy(_popup);
            if (_canvas == null) return;

            _popup = new GameObject("DemolishConfirmDialog");
            _popup.transform.SetParent(_canvas.transform, false);
            _popup.transform.SetAsLastSibling();

            // Dim overlay
            var dimRect = _popup.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _popup.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.7f);
            dimImg.raycastTarget = true;

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_popup.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.35f);
            panelRect.anchorMax = new Vector2(0.88f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = PanelBg;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = WarningRed;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string displayName = buildingId.Replace('_', ' ');

            // Warning icon + title
            CreateLabel(panel.transform, "Title",
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f),
                $"\u26A0 DEMOLISH {displayName.ToUpper()}?", 16, FontStyle.Bold, WarningRed);

            // Tier info
            CreateLabel(panel.transform, "Info",
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.72f),
                $"Level {tier} — This action cannot be undone.\nResources spent will NOT be refunded.", 11, FontStyle.Normal, new Color(0.8f, 0.8f, 0.8f));

            // Confirm button (red)
            var confirmGO = CreateButton(panel.transform, "ConfirmBtn",
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.38f),
                "DEMOLISH", WarningRed);
            confirmGO.GetComponent<Button>().onClick.AddListener(OnConfirm);

            // Cancel button (grey)
            var cancelGO = CreateButton(panel.transform, "CancelBtn",
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.38f),
                "CANCEL", new Color(0.4f, 0.4f, 0.4f, 1f));
            cancelGO.GetComponent<Button>().onClick.AddListener(OnCancel);
        }

        private void OnConfirm()
        {
            if (_pendingInstanceId != null)
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                    bm.DemolishBuilding(_pendingInstanceId);
            }
            Close();
        }

        private void OnCancel()
        {
            Close();
        }

        private void Close()
        {
            _pendingInstanceId = null;
            _pendingBuildingId = null;
            if (_popup != null) { Destroy(_popup); _popup = null; }
        }

        private static void CreateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(0.6f, -0.6f);
        }

        private static GameObject CreateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(bgColor.r + 0.2f, bgColor.g + 0.2f, bgColor.b + 0.2f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            go.AddComponent<Button>().targetGraphic = bg;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lRect = labelGO.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;
            var t = labelGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;

            return go;
        }
    }

    public readonly struct DemolishRequestedEvent
    {
        public readonly string InstanceId;
        public readonly string BuildingId;
        public readonly int Tier;
        public DemolishRequestedEvent(string id, string bid, int t) { InstanceId = id; BuildingId = bid; Tier = t; }
    }
}
