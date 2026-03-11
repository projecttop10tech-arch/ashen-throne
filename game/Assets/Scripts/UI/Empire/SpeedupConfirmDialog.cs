using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style gem speedup confirmation dialog.
    /// Subscribes to SpeedupRequestedEvent, shows cost and confirm/cancel.
    /// </summary>
    public class SpeedupConfirmDialog : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private BuildingManager _buildingManager;
        private EventSubscription _requestSub;

        private GameObject _dialog;
        private Text _messageText;
        private Text _costText;
        private string _pendingPlacedId;
        private int _pendingGemCost;

        private static readonly Color DialogBg = new(0.06f, 0.04f, 0.10f, 0.95f);
        private static readonly Color DialogBorder = new(0.83f, 0.66f, 0.26f, 0.85f);
        private static readonly Color ConfirmColor = new(0.55f, 0.35f, 0.85f, 1f);
        private static readonly Color CancelColor = new(0.35f, 0.30f, 0.25f, 0.90f);
        private static readonly Color GoldText = new(0.83f, 0.66f, 0.26f, 1f);
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

            // Full-screen overlay to block input
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(_dialog.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.60f);
            var overlayBtn = overlay.AddComponent<Button>();
            overlayBtn.onClick.AddListener(CloseDialog);

            // Dialog panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_dialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.35f);
            panelRect.anchorMax = new Vector2(0.88f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = DialogBg;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = DialogBorder;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.75f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "SPEED UP";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = GoldText;
            titleText.raycastTarget = false;
            var titleShadow = titleGO.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.8f);
            titleShadow.effectDistance = new Vector2(1f, -1f);

            // Message
            string timeStr = FormatTime(Mathf.CeilToInt(remainingSeconds));
            var msgGO = new GameObject("Message");
            msgGO.transform.SetParent(panel.transform, false);
            var msgRect = msgGO.AddComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.05f, 0.45f);
            msgRect.anchorMax = new Vector2(0.95f, 0.72f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
            _messageText = msgGO.AddComponent<Text>();
            _messageText.text = $"Complete upgrade instantly?\nTime remaining: {timeStr}";
            _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _messageText.fontSize = 12;
            _messageText.alignment = TextAnchor.MiddleCenter;
            _messageText.color = new Color(0.80f, 0.75f, 0.68f, 1f);
            _messageText.raycastTarget = false;
            _messageText.supportRichText = true;

            // Gem cost display
            var costGO = new GameObject("Cost");
            costGO.transform.SetParent(panel.transform, false);
            var costRect = costGO.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.20f, 0.28f);
            costRect.anchorMax = new Vector2(0.80f, 0.45f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;
            _costText = costGO.AddComponent<Text>();
            _costText.text = $"\u2666 {gemCost} Gems";
            _costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _costText.fontSize = 18;
            _costText.fontStyle = FontStyle.Bold;
            _costText.alignment = TextAnchor.MiddleCenter;
            _costText.color = GemColor;
            _costText.raycastTarget = false;
            var costShadow = costGO.AddComponent<Shadow>();
            costShadow.effectColor = new Color(0, 0, 0, 0.8f);
            costShadow.effectDistance = new Vector2(1f, -1f);

            // Confirm button
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(panel.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.52f, 0.05f);
            confirmRect.anchorMax = new Vector2(0.95f, 0.25f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = ConfirmColor;
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.onClick.AddListener(OnConfirm);
            AddButtonLabel(confirmGO, "USE GEMS");

            // Cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(panel.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.05f, 0.05f);
            cancelRect.anchorMax = new Vector2(0.48f, 0.25f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = CancelColor;
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(CloseDialog);
            AddButtonLabel(cancelGO, "CANCEL");
        }

        private void OnConfirm()
        {
            if (_buildingManager != null && !string.IsNullOrEmpty(_pendingPlacedId))
            {
                // Apply full speedup
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
            if (_dialog != null)
            {
                Destroy(_dialog);
                _dialog = null;
            }
            _pendingPlacedId = null;
        }

        private static void AddButtonLabel(GameObject parent, string label)
        {
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(parent.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
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
