using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style radial quick-action menu that appears around a tapped building.
    /// Shows circular action buttons (Upgrade, Info, Move) arranged in a semi-circle
    /// above the building. Dismisses on tap-away or timeout.
    /// Intercepts BuildingTappedEvent — the full info popup only opens on "Info" button.
    /// </summary>
    public class BuildingQuickActionMenu : MonoBehaviour
    {
        private CityGridView _gridView;
        private BuildingManager _buildingManager;
        private BuildingInfoPopupController _infoPopup;

        private GameObject _menuRoot;
        private readonly List<GameObject> _buttons = new();
        private string _activeInstanceId;
        private string _activeBuildingId;
        private int _activeTier;
        private float _dismissTimer;
        private const float DismissTimeout = 5f;
        private bool _menuVisible;

        private EventSubscription _tapSub;

        // P&C: Selection ring around tapped building
        private GameObject _selectionRing;

        private static readonly Color ButtonBg = new(0.08f, 0.05f, 0.14f, 0.92f);
        private static readonly Color ButtonBorder = new(0.78f, 0.62f, 0.22f, 0.85f);
        private static readonly Color UpgradeColor = new(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color InfoColor = new(0.40f, 0.65f, 0.90f, 1f);
        private static readonly Color MoveColor = new(0.85f, 0.70f, 0.25f, 1f);
        private static readonly Color LockedColor = new(0.45f, 0.40f, 0.38f, 0.7f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void Start()
        {
            _gridView = FindFirstObjectByType<CityGridView>();
            _infoPopup = FindFirstObjectByType<BuildingInfoPopupController>();
        }

        private void OnEnable()
        {
            _tapSub = EventBus.Subscribe<BuildingTappedEvent>(OnBuildingTapped);
        }

        private void OnDisable()
        {
            _tapSub?.Dispose();
        }

        private void Update()
        {
            if (!_menuVisible) return;

            _dismissTimer -= Time.deltaTime;
            if (_dismissTimer <= 0f)
                DismissMenu();

            // Pulse the selection ring
            if (_selectionRing != null)
            {
                var img = _selectionRing.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.5f + 0.15f * Mathf.Sin(Time.time * 3f);
                    var c = ButtonBorder;
                    c.a = pulse;
                    img.color = c;
                }
            }
        }

        private void OnBuildingTapped(BuildingTappedEvent evt)
        {
            // If menu already showing for this building, dismiss
            if (_menuVisible && _activeInstanceId == evt.InstanceId)
            {
                DismissMenu();
                return;
            }

            // If menu showing for different building, dismiss and reshow
            if (_menuVisible)
                DismissMenu();

            _activeInstanceId = evt.InstanceId;
            _activeBuildingId = evt.BuildingId;
            _activeTier = evt.Tier;

            ShowMenu(evt);
        }

        private void ShowMenu(BuildingTappedEvent evt)
        {
            if (_gridView == null) return;

            // Find the building visual
            GameObject buildingGO = null;
            RectTransform buildingRect = null;
            foreach (var p in _gridView.GetPlacements())
            {
                if (p.InstanceId == evt.InstanceId && p.VisualGO != null)
                {
                    buildingGO = p.VisualGO;
                    buildingRect = buildingGO.GetComponent<RectTransform>();
                    break;
                }
            }
            if (buildingGO == null || buildingRect == null) return;

            // Find canvas for overlay
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Create menu root
            _menuRoot = new GameObject("QuickActionMenu");
            _menuRoot.transform.SetParent(canvas.transform, false);
            _menuRoot.transform.SetAsLastSibling();

            // Full-screen dismiss backdrop (transparent)
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(_menuRoot.transform, false);
            var bdRect = backdrop.AddComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.offsetMin = Vector2.zero;
            bdRect.offsetMax = Vector2.zero;
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = new Color(0, 0, 0, 0.01f); // Nearly invisible but catches taps
            bdImg.raycastTarget = true;
            var bdBtn = backdrop.AddComponent<Button>();
            bdBtn.targetGraphic = bdImg;
            bdBtn.onClick.AddListener(DismissMenu);

            // P&C: Selection ring around the building
            CreateSelectionRing(buildingGO, buildingRect);

            // Determine which buttons to show
            bool isUpgrading = IsUpgrading(evt.InstanceId);
            bool canUpgrade = !isUpgrading && CanUpgradeBuilding(evt.BuildingId, evt.Tier);

            // Convert building position to canvas space
            Vector3 worldPos = buildingRect.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPos),
                canvas.worldCamera, out var menuCenter);

            float buildingHeight = buildingRect.sizeDelta.y * buildingRect.lossyScale.y;

            // Button definitions
            var actions = new List<(string label, string icon, Color color, System.Action action, bool enabled)>();

            if (isUpgrading)
            {
                actions.Add(("SPEED", "\u26A1", UpgradeColor, OnSpeedUpPressed, true));
            }
            else
            {
                actions.Add(("UPGRADE", "\u2B06", canUpgrade ? UpgradeColor : LockedColor,
                    OnUpgradePressed, canUpgrade));
            }
            actions.Add(("INFO", "\u2139", InfoColor, OnInfoPressed, true));
            actions.Add(("MOVE", "\u2725", MoveColor, OnMovePressed, !isUpgrading));

            // Arrange in semi-circle above building
            float radius = 55f;
            float startAngle = 120f; // Degrees from right, going counter-clockwise
            float endAngle = 60f;
            float angleStep = actions.Count > 1 ? (startAngle - endAngle) / (actions.Count - 1) : 0f;

            for (int i = 0; i < actions.Count; i++)
            {
                var (label, icon, color, action, enabled) = actions[i];
                float angle = (startAngle - i * angleStep) * Mathf.Deg2Rad;
                float bx = menuCenter.x + Mathf.Cos(angle) * radius;
                float by = menuCenter.y + buildingHeight * 0.3f + Mathf.Sin(angle) * radius;

                var btnGO = CreateActionButton(label, icon, color, action, enabled, new Vector2(bx, by));
                _buttons.Add(btnGO);

                // Animate: pop in with scale
                StartCoroutine(AnimateButtonIn(btnGO, i * 0.05f));
            }

            _menuVisible = true;
            _dismissTimer = DismissTimeout;
        }

        private GameObject CreateActionButton(string label, string icon, Color iconColor,
            System.Action onClick, bool enabled, Vector2 position)
        {
            var btnGO = new GameObject($"QA_{label}");
            btnGO.transform.SetParent(_menuRoot.transform, false);

            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(44, 44);
            rect.localScale = Vector3.zero; // Start invisible for animation

            // Circular background
            var bg = btnGO.AddComponent<Image>();
            bg.color = enabled ? ButtonBg : new Color(0.06f, 0.04f, 0.08f, 0.75f);
            bg.raycastTarget = true;

            // Gold border
            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = enabled ? ButtonBorder : new Color(0.4f, 0.35f, 0.25f, 0.5f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            // Button component
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = enabled;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());

            // Icon text
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(btnGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.25f);
            iconRect.anchorMax = new Vector2(1f, 0.95f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = icon;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 16;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = enabled ? iconColor : LockedColor;
            iconText.raycastTarget = false;
            var iconShadow = iconGO.AddComponent<Shadow>();
            iconShadow.effectColor = new Color(0, 0, 0, 0.8f);
            iconShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Label text
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.30f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 7;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = enabled ? Color.white : LockedColor;
            labelText.raycastTarget = false;

            return btnGO;
        }

        private void CreateSelectionRing(GameObject buildingGO, RectTransform buildingRect)
        {
            _selectionRing = new GameObject("SelectionRing");
            _selectionRing.transform.SetParent(buildingGO.transform, false);
            var ringRect = _selectionRing.AddComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(-0.05f, -0.05f);
            ringRect.anchorMax = new Vector2(1.05f, 1.05f);
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;

            var ringImg = _selectionRing.AddComponent<Image>();
            ringImg.color = new Color(0.78f, 0.62f, 0.22f, 0.5f);
            ringImg.raycastTarget = false;

            // Use radial gradient for soft glow
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) ringImg.sprite = spr;

            var outline = _selectionRing.AddComponent<Outline>();
            outline.effectColor = ButtonBorder;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private System.Collections.IEnumerator AnimateButtonIn(GameObject btnGO, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Overshoot ease
                float scale = t < 0.7f
                    ? Mathf.Lerp(0f, 1.15f, t / 0.7f)
                    : Mathf.Lerp(1.15f, 1f, (t - 0.7f) / 0.3f);
                btnGO.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            btnGO.transform.localScale = Vector3.one;
        }

        // ================================================================
        // Action handlers
        // ================================================================

        private void OnUpgradePressed()
        {
            DismissMenu();
            // Try direct upgrade — publish tap event so info popup can handle
            if (_buildingManager != null && _activeInstanceId != null)
            {
                // Quick upgrade: start immediately if affordable
                bool started = _buildingManager.StartUpgrade(_activeInstanceId);
                if (!started)
                {
                    // Can't afford or other issue — open info popup for details
                    OpenInfoPopup();
                }
            }
        }

        private void OnSpeedUpPressed()
        {
            DismissMenu();
            // Open info popup which has the speedup flow
            OpenInfoPopup();
        }

        private void OnInfoPressed()
        {
            DismissMenu();
            OpenInfoPopup();
        }

        private void OnMovePressed()
        {
            DismissMenu();
            if (_gridView != null && _activeInstanceId != null)
                _gridView.EnterMoveModeForBuilding(_activeInstanceId);
        }

        private void OpenInfoPopup()
        {
            // Re-publish the tap event for the info popup to handle
            var gridView = _gridView;
            if (gridView == null) return;

            foreach (var p in gridView.GetPlacements())
            {
                if (p.InstanceId == _activeInstanceId)
                {
                    EventBus.Publish(new BuildingInfoRequestedEvent(
                        p.BuildingId, p.InstanceId, p.Tier));
                    break;
                }
            }
        }

        private void DismissMenu()
        {
            if (_menuRoot != null)
                Destroy(_menuRoot);
            _menuRoot = null;

            if (_selectionRing != null)
                Destroy(_selectionRing);
            _selectionRing = null;

            _buttons.Clear();
            _menuVisible = false;
            _activeInstanceId = null;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private bool IsUpgrading(string instanceId)
        {
            if (_buildingManager == null) return false;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == instanceId) return true;
            }
            return false;
        }

        private bool CanUpgradeBuilding(string buildingId, int currentTier)
        {
            if (_buildingManager == null) return false;
            // Check if build queue has a free slot
            if (_buildingManager.BuildQueue.Count >= 2) return false;
            // Simplified: assume upgrade is possible if not at max tier
            return currentTier < 3;
        }
    }

    /// <summary>
    /// Event for requesting the full info popup (used by quick action menu).
    /// Separate from BuildingTappedEvent to avoid infinite loops.
    /// </summary>
    public readonly struct BuildingInfoRequestedEvent
    {
        public readonly string BuildingId;
        public readonly string InstanceId;
        public readonly int Tier;
        public BuildingInfoRequestedEvent(string bid, string iid, int t)
        { BuildingId = bid; InstanceId = iid; Tier = t; }
    }
}
