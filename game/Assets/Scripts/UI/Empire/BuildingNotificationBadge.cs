using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style notification badges on buildings that need player attention.
    /// Red "!" for upgradeable buildings, green arrow for idle buildings with full resources,
    /// and pulsing glow for upgrade-complete buildings.
    /// </summary>
    public class BuildingNotificationBadge : MonoBehaviour
    {
        private CityGridView _gridView;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;

        private readonly Dictionary<string, GameObject> _badges = new();
        private float _refreshTimer;
        private const float RefreshInterval = 2f;

        private EventSubscription _upgradeSub;
        private EventSubscription _placeSub;
        private EventSubscription _demolishSub;

        // Badge colors
        private static readonly Color UpgradeReadyColor = new(0.90f, 0.25f, 0.20f, 1f);   // Red "!"
        private static readonly Color IdleColor = new(1f, 0.78f, 0.20f, 1f);               // Gold arrow
        private static readonly Color JustCompletedColor = new(0.20f, 0.85f, 0.40f, 1f);   // Green check

        // Track recently completed buildings for brief green flash
        private readonly Dictionary<string, float> _recentlyCompleted = new();
        private const float CompletedFlashDuration = 5f;

        private void Start()
        {
            _gridView = FindFirstObjectByType<CityGridView>();
            ServiceLocator.TryGet(out _buildingManager);
            ServiceLocator.TryGet(out _resourceManager);
        }

        private void OnEnable()
        {
            _upgradeSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _placeSub = EventBus.Subscribe<BuildingPlacedEvent>(e => _refreshTimer = RefreshInterval);
            _demolishSub = EventBus.Subscribe<BuildingDemolishedEvent>(OnDemolished);
        }

        private void OnDisable()
        {
            _upgradeSub?.Dispose();
            _placeSub?.Dispose();
            _demolishSub?.Dispose();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RefreshAllBadges();
            }

            // Tick down recently-completed timers
            var expired = new List<string>();
            foreach (var kvp in _recentlyCompleted)
            {
                if (Time.time > kvp.Value)
                    expired.Add(kvp.Key);
            }
            foreach (var key in expired)
                _recentlyCompleted.Remove(key);
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            _recentlyCompleted[evt.PlacedId] = Time.time + CompletedFlashDuration;
            _refreshTimer = RefreshInterval; // Force immediate refresh
        }

        private void OnDemolished(BuildingDemolishedEvent evt)
        {
            RemoveBadge(evt.PlacedId);
            _recentlyCompleted.Remove(evt.PlacedId);
        }

        private void RefreshAllBadges()
        {
            if (_gridView == null || _buildingManager == null) return;

            var placements = _gridView.GetPlacements();
            var activePlacedIds = new HashSet<string>();

            foreach (var p in placements)
            {
                activePlacedIds.Add(p.InstanceId);
                var state = GetBuildingState(p);

                if (state == BadgeState.None)
                {
                    RemoveBadge(p.InstanceId);
                    continue;
                }

                EnsureBadge(p, state);
            }

            // Clean up badges for removed buildings
            var stale = new List<string>();
            foreach (var kvp in _badges)
            {
                if (!activePlacedIds.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }
            foreach (var key in stale)
                RemoveBadge(key);
        }

        private BadgeState GetBuildingState(CityBuildingPlacement placement)
        {
            // Recently completed → green check
            if (_recentlyCompleted.ContainsKey(placement.InstanceId))
                return BadgeState.JustCompleted;

            // Currently upgrading → no badge (construction overlay handles it)
            if (IsInBuildQueue(placement.InstanceId))
                return BadgeState.None;

            // Can afford next tier upgrade → red "!" (action needed)
            if (_buildingManager.PlacedBuildings.TryGetValue(placement.InstanceId, out var placed))
            {
                int nextTier = placed.CurrentTier + 1;
                var tierData = placed.Data.GetTier(nextTier);
                if (tierData != null && _resourceManager != null)
                {
                    if (_resourceManager.CanAfford(tierData.stoneCost, tierData.ironCost,
                        tierData.grainCost, tierData.arcaneEssenceCost))
                    {
                        return BadgeState.UpgradeReady;
                    }
                }
            }

            return BadgeState.None;
        }

        private bool IsInBuildQueue(string placedId)
        {
            if (_buildingManager == null) return false;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == placedId) return true;
            }
            return false;
        }

        private void EnsureBadge(CityBuildingPlacement placement, BadgeState state)
        {
            if (placement.VisualGO == null) return;

            // Check if badge already exists with correct state
            if (_badges.TryGetValue(placement.InstanceId, out var existing))
            {
                if (existing == null)
                {
                    _badges.Remove(placement.InstanceId);
                }
                else
                {
                    // Update badge appearance if state changed
                    UpdateBadgeAppearance(existing, state);
                    return;
                }
            }

            CreateBadge(placement, state);
        }

        private void CreateBadge(CityBuildingPlacement placement, BadgeState state)
        {
            var badgeGO = new GameObject($"Badge_{placement.InstanceId}");
            badgeGO.transform.SetParent(placement.VisualGO.transform, false);

            var rect = badgeGO.AddComponent<RectTransform>();
            // Top-right corner of building
            rect.anchorMin = new Vector2(0.75f, 0.75f);
            rect.anchorMax = new Vector2(0.75f, 0.75f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(22, 22);

            // Circle background
            var bg = badgeGO.AddComponent<Image>();
            bg.raycastTarget = false;

            // Border
            var outline = badgeGO.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Symbol text
            var textGO = new GameObject("Symbol");
            textGO.transform.SetParent(badgeGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);

            _badges[placement.InstanceId] = badgeGO;
            UpdateBadgeAppearance(badgeGO, state);
        }

        private void UpdateBadgeAppearance(GameObject badgeGO, BadgeState state)
        {
            if (badgeGO == null) return;

            var bg = badgeGO.GetComponent<Image>();
            var text = badgeGO.GetComponentInChildren<Text>();
            if (bg == null || text == null) return;

            // Store state for pulse animation
            badgeGO.name = $"Badge_{state}";

            switch (state)
            {
                case BadgeState.UpgradeReady:
                    bg.color = UpgradeReadyColor;
                    text.text = "!";
                    text.fontSize = 16;
                    break;
                case BadgeState.JustCompleted:
                    bg.color = JustCompletedColor;
                    text.text = "\u2713"; // ✓
                    text.fontSize = 14;
                    break;
                case BadgeState.Idle:
                    bg.color = IdleColor;
                    text.text = "\u2191"; // ↑
                    text.fontSize = 14;
                    break;
            }

            // Gentle pulse scale animation
            float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 3f);
            badgeGO.transform.localScale = Vector3.one * pulse;
        }

        private void RemoveBadge(string placedId)
        {
            if (_badges.TryGetValue(placedId, out var badge))
            {
                if (badge != null) Destroy(badge);
                _badges.Remove(placedId);
            }
        }

        private void LateUpdate()
        {
            // Animate pulse on all active badges
            foreach (var kvp in _badges)
            {
                if (kvp.Value == null) continue;
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 4f);
                kvp.Value.transform.localScale = Vector3.one * pulse;
            }
        }

        private enum BadgeState { None, UpgradeReady, JustCompleted, Idle }
    }
}
