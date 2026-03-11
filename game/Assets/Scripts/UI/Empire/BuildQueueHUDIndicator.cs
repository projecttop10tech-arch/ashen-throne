using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style build queue indicator at bottom-left of Empire screen.
    /// Shows hammer icon + timer for each active build (max 2 slots).
    /// Tapping opens the BuildingInfoPopup for that building.
    /// </summary>
    public class BuildQueueHUDIndicator : MonoBehaviour
    {
        private BuildingManager _buildingManager;
        private RectTransform _container;
        private readonly List<QueueSlotUI> _slots = new();
        private EventSubscription _startedSub;
        private EventSubscription _completedSub;
        private EventSubscription _cancelledSub;

        private const int TotalSlots = 2; // P&C: always show 2 builder slots

        private static readonly Color SlotBg = new(0.06f, 0.04f, 0.10f, 0.90f);
        private static readonly Color SlotBorder = new(0.55f, 0.43f, 0.18f, 0.70f);
        private static readonly Color EmptySlotBg = new(0.04f, 0.03f, 0.08f, 0.65f);
        private static readonly Color EmptySlotBorder = new(0.35f, 0.28f, 0.14f, 0.45f);
        private static readonly Color FillColor = new(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color TimerColor = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color HeaderColor = new(0.83f, 0.66f, 0.26f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _startedSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(_ => RebuildSlots());
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(_ => RebuildSlots());
            _cancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(_ => RebuildSlots());
        }

        private void OnDisable()
        {
            _startedSub?.Dispose();
            _completedSub?.Dispose();
            _cancelledSub?.Dispose();
        }

        private void Start()
        {
            FindContainer();
            RebuildSlots();
        }

        private void Update()
        {
            if (_buildingManager == null) return;

            for (int i = 0; i < _slots.Count && i < _buildingManager.BuildQueue.Count; i++)
            {
                var slot = _slots[i];
                if (slot.TimerText == null || slot.FillRect == null) continue; // empty slot

                var entry = _buildingManager.BuildQueue[i];
                float total = slot.TotalSeconds;
                float remaining = entry.RemainingSeconds;
                float progress = total > 0 ? 1f - (remaining / total) : 1f;

                slot.TimerText.text = FormatTime(Mathf.CeilToInt(remaining));
                slot.FillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

                // Flash when < 10s
                if (remaining < 10f)
                    slot.TimerText.color = new Color(1f, 0.3f, 0.3f, 1f);
                else
                    slot.TimerText.color = TimerColor;
            }
        }

        private void FindContainer()
        {
            // Find or create the container
            var t = FindDeepChild(transform.root, "BuildQueueHUD");
            if (t != null)
            {
                _container = t.GetComponent<RectTransform>();
            }
            else
            {
                // Create it dynamically near bottom-left, above nav bar
                var go = new GameObject("BuildQueueHUD");
                go.transform.SetParent(transform, false);
                _container = go.AddComponent<RectTransform>();
                _container.anchorMin = new Vector2(0.01f, 0.11f);
                _container.anchorMax = new Vector2(0.40f, 0.25f);
                _container.offsetMin = Vector2.zero;
                _container.offsetMax = Vector2.zero;
            }
        }

        private GameObject _headerLabel;

        private void RebuildSlots()
        {
            // Clear existing
            foreach (var slot in _slots)
            {
                if (slot.Root != null)
                    Destroy(slot.Root);
            }
            _slots.Clear();
            if (_headerLabel != null) { Destroy(_headerLabel); _headerLabel = null; }

            if (_buildingManager == null || _container == null) return;

            // P&C: always show the HUD (even with 0 active builds)
            _container.gameObject.SetActive(true);

            int activeCount = _buildingManager.BuildQueue.Count;

            // Header: "Builder X/2"
            _headerLabel = new GameObject("BuilderHeader");
            _headerLabel.transform.SetParent(_container, false);
            var headerRect = _headerLabel.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 0.78f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            var headerText = _headerLabel.AddComponent<Text>();
            headerText.text = $"Builder {activeCount}/{TotalSlots}";
            headerText.fontSize = 9;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = HeaderColor;
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.raycastTarget = false;
            var headerShadow = _headerLabel.AddComponent<Shadow>();
            headerShadow.effectColor = new Color(0, 0, 0, 0.9f);
            headerShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Always create TotalSlots (2) + 1 locked premium slot
            int displaySlots = TotalSlots + 1; // 2 free + 1 locked
            float slotWidth = 1f / displaySlots;
            for (int i = 0; i < displaySlots; i++)
            {
                float xMin = i * slotWidth;
                float xMax = (i + 1) * slotWidth - 0.01f;

                if (i < activeCount)
                {
                    var entry = _buildingManager.BuildQueue[i];
                    var slot = CreateSlot(entry, xMin, xMax);
                    _slots.Add(slot);
                }
                else if (i < TotalSlots)
                {
                    var slot = CreateEmptySlot(i, xMin, xMax);
                    _slots.Add(slot);
                }
                else
                {
                    // P&C: Locked premium 3rd builder slot
                    var slot = CreateLockedSlot(xMin, xMax);
                    _slots.Add(slot);
                }
            }
        }

        private QueueSlotUI CreateSlot(BuildQueueEntry entry, float xMin, float xMax)
        {
            // Slot background (below header)
            var root = new GameObject($"QueueSlot_{entry.PlacedId}");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImg = root.AddComponent<Image>();
            bgImg.color = SlotBg;

            // Gold border
            var border = new GameObject("Border");
            border.transform.SetParent(root.transform, false);
            var borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderOutline = border.AddComponent<Outline>();
            borderOutline.effectColor = SlotBorder;
            borderOutline.effectDistance = new Vector2(1f, -1f);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0, 0, 0, 0);
            borderImg.raycastTarget = false;

            // Progress fill bar at bottom
            var fillBg = new GameObject("FillBg");
            fillBg.transform.SetParent(root.transform, false);
            var fillBgRect = fillBg.AddComponent<RectTransform>();
            fillBgRect.anchorMin = new Vector2(0f, 0f);
            fillBgRect.anchorMax = new Vector2(1f, 0.25f);
            fillBgRect.offsetMin = Vector2.zero;
            fillBgRect.offsetMax = Vector2.zero;
            var fillBgImg = fillBg.AddComponent<Image>();
            fillBgImg.color = new Color(0, 0, 0, 0.5f);
            fillBgImg.raycastTarget = false;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillBg.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.01f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = FillColor;
            fillImg.raycastTarget = false;

            // Building name label
            string displayName = GetBuildingName(entry.PlacedId);
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(root.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.45f);
            nameRect.anchorMax = new Vector2(0.95f, 0.95f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = displayName;
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = new Color(0.83f, 0.66f, 0.26f, 1f);
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontStyle = FontStyle.Bold;
            nameText.raycastTarget = false;
            var nameShadow = nameGO.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.8f);
            nameShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Timer text
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(root.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.05f, 0.0f);
            timerRect.anchorMax = new Vector2(0.95f, 0.50f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = FormatTime(Mathf.CeilToInt(entry.RemainingSeconds));
            timerText.fontSize = 11;
            timerText.alignment = TextAnchor.MiddleLeft;
            timerText.color = TimerColor;
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontStyle = FontStyle.Bold;
            timerText.raycastTarget = false;
            var timerShadow = timerGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.8f);
            timerShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Estimate total time
            float elapsed = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds;
            float total = elapsed + entry.RemainingSeconds;

            // P&C: Cancel button (small X in top-right corner)
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(root.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.78f, 0.55f);
            cancelRect.anchorMax = new Vector2(0.98f, 0.95f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.65f, 0.20f, 0.20f, 0.85f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            string cancelId = entry.PlacedId;
            cancelBtn.onClick.AddListener(() => OnCancelSlotPressed(cancelId));
            var cancelLabel = new GameObject("X");
            cancelLabel.transform.SetParent(cancelGO.transform, false);
            var clRect = cancelLabel.AddComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            var clText = cancelLabel.AddComponent<Text>();
            clText.text = "\u2716";
            clText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clText.fontSize = 9;
            clText.fontStyle = FontStyle.Bold;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.color = Color.white;
            clText.raycastTarget = false;

            return new QueueSlotUI
            {
                Root = root,
                FillRect = fillRect,
                TimerText = timerText,
                TotalSeconds = total,
                PlacedId = entry.PlacedId
            };
        }

        private QueueSlotUI CreateEmptySlot(int index, float xMin, float xMax)
        {
            var root = new GameObject($"QueueSlot_Empty_{index}");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImg = root.AddComponent<Image>();
            bgImg.color = EmptySlotBg;

            // Dimmer border
            var border = new GameObject("Border");
            border.transform.SetParent(root.transform, false);
            var borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderOutline = border.AddComponent<Outline>();
            borderOutline.effectColor = EmptySlotBorder;
            borderOutline.effectDistance = new Vector2(1f, -1f);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0, 0, 0, 0);
            borderImg.raycastTarget = false;

            // "IDLE" label
            var labelGO = new GameObject("IdleLabel");
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = "IDLE";
            labelText.fontSize = 10;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.45f, 0.40f, 0.35f, 0.7f);
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontStyle = FontStyle.Italic;
            labelText.raycastTarget = false;

            return new QueueSlotUI
            {
                Root = root,
                FillRect = null,
                TimerText = null,
                TotalSeconds = 0f
            };
        }

        /// <summary>P&C: Locked premium builder slot with "+" icon.</summary>
        private QueueSlotUI CreateLockedSlot(float xMin, float xMax)
        {
            var root = new GameObject("QueueSlot_Locked");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.02f, 0.06f, 0.55f);

            // Lock border (dimmer gold)
            var border = new GameObject("Border");
            border.transform.SetParent(root.transform, false);
            var borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderOutline = border.AddComponent<Outline>();
            borderOutline.effectColor = new Color(0.40f, 0.30f, 0.12f, 0.35f);
            borderOutline.effectDistance = new Vector2(1f, -1f);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0, 0, 0, 0);
            borderImg.raycastTarget = false;

            // "+" label
            var labelGO = new GameObject("PlusLabel");
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = "+";
            labelText.fontSize = 16;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.78f, 0.62f, 0.22f, 0.5f);
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;

            // Tap to show unlock prompt
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(OnLockedSlotTapped);

            return new QueueSlotUI
            {
                Root = root,
                FillRect = null,
                TimerText = null,
                TotalSeconds = 0f
            };
        }

        private void OnLockedSlotTapped()
        {
            Debug.Log("[BuildQueueHUD] Premium 3rd builder slot tapped — unlock via Battle Pass Season progression.");
        }

        private string GetBuildingName(string placedId)
        {
            if (_buildingManager != null && _buildingManager.PlacedBuildings.TryGetValue(placedId, out var placed))
            {
                if (placed.Data != null && !string.IsNullOrEmpty(placed.Data.displayName))
                    return placed.Data.displayName;
            }
            // Fallback: extract from instance ID
            string id = placedId.Contains("_") ? placedId.Substring(0, placedId.LastIndexOf('_')) : placedId;
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Done!";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}:{m:D2}:{s:D2}";
            return $"{m}:{s:D2}";
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

        private void OnCancelSlotPressed(string placedId)
        {
            if (_buildingManager == null) return;
            bool cancelled = _buildingManager.CancelUpgrade(placedId);
            if (cancelled)
                Debug.Log($"[BuildQueueHUD] Cancelled upgrade for {placedId}.");
        }

        private class QueueSlotUI
        {
            public GameObject Root;
            public RectTransform FillRect;
            public Text TimerText;
            public float TotalSeconds;
            public string PlacedId;
        }
    }
}
