using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style auto-upgrade toggle. Shows a small "AUTO" button on the construction overlay
    /// for buildings currently being upgraded. When enabled, the next tier upgrade starts
    /// automatically when the current one completes (if resources are sufficient).
    /// </summary>
    public class AutoUpgradeToggle : MonoBehaviour
    {
        private BuildingManager _buildingManager;
        private EventSubscription _upgStartSub;
        private EventSubscription _upgCompSub;
        private EventSubscription _autoSub;

        private readonly System.Collections.Generic.Dictionary<string, GameObject> _toggleButtons = new();

        private static readonly Color EnabledColor = new(0.20f, 0.75f, 0.35f, 0.90f);
        private static readonly Color DisabledColor = new(0.35f, 0.35f, 0.40f, 0.75f);
        private static readonly Color GoldBorder = new(0.78f, 0.62f, 0.22f, 0.7f);

        private void Start()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _upgStartSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _upgCompSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _autoSub = EventBus.Subscribe<AutoUpgradeToggledEvent>(OnAutoToggled);
        }

        private void OnDisable()
        {
            _upgStartSub?.Dispose();
            _upgCompSub?.Dispose();
            _autoSub?.Dispose();
        }

        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            // Find the building visual and attach an AUTO button
            var gridView = FindFirstObjectByType<CityGridView>();
            if (gridView == null) return;

            foreach (var p in gridView.GetPlacements())
            {
                if (p.InstanceId != evt.PlacedId || p.VisualGO == null) continue;
                CreateToggleButton(p.InstanceId, p.VisualGO);
                break;
            }
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            RemoveToggleButton(evt.PlacedId);
        }

        private void OnAutoToggled(AutoUpgradeToggledEvent evt)
        {
            UpdateButtonAppearance(evt.PlacedId, evt.Enabled);
        }

        private void CreateToggleButton(string placedId, GameObject buildingGO)
        {
            if (_toggleButtons.ContainsKey(placedId)) return;

            var btnGO = new GameObject($"AutoUpgrade_{placedId}");
            btnGO.transform.SetParent(buildingGO.transform, false);

            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.65f, 0.88f);
            rect.anchorMax = new Vector2(0.98f, 0.99f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = btnGO.AddComponent<Image>();
            bool isEnabled = _buildingManager != null && _buildingManager.IsAutoUpgradeEnabled(placedId);
            bg.color = isEnabled ? EnabledColor : DisabledColor;
            bg.raycastTarget = true;

            // Double border: outer glow → gold
            var outerGlow = btnGO.AddComponent<Outline>();
            outerGlow.effectColor = new Color(0.90f, 0.72f, 0.28f, 0.20f);
            outerGlow.effectDistance = new Vector2(1.2f, -1.2f);
            var goldOutline = btnGO.AddComponent<Shadow>();
            goldOutline.effectColor = GoldBorder;
            goldOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(btnGO.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.45f); glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero; glRect.offsetMax = Vector2.zero;
            glass.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            glass.GetComponent<Image>().raycastTarget = false;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            string capturedId = placedId;
            btn.onClick.AddListener(() => OnAutoButtonPressed(capturedId));

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelGO.AddComponent<Text>();
            text.text = "AUTO";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            labelGO.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.80f);
            labelGO.GetComponent<Outline>().effectDistance = new Vector2(0.4f, -0.4f);

            _toggleButtons[placedId] = btnGO;
        }

        private void OnAutoButtonPressed(string placedId)
        {
            if (_buildingManager == null) return;
            _buildingManager.ToggleAutoUpgrade(placedId);
        }

        private void UpdateButtonAppearance(string placedId, bool enabled)
        {
            if (!_toggleButtons.TryGetValue(placedId, out var btnGO)) return;
            if (btnGO == null) return;
            var bg = btnGO.GetComponent<Image>();
            if (bg != null)
                bg.color = enabled ? EnabledColor : DisabledColor;
        }

        private void RemoveToggleButton(string placedId)
        {
            if (_toggleButtons.TryGetValue(placedId, out var go))
            {
                if (go != null) Destroy(go);
                _toggleButtons.Remove(placedId);
            }
        }
    }
}
