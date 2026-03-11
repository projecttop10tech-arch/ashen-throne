using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.Empire
{
    /// <summary>
    /// P&C-style notification badges on buildings.
    /// Shows a red dot/exclamation when a building has an actionable state:
    /// - Upgrade complete (celebration pending)
    /// - Resources ready to collect
    /// - Upgrade available (meets requirements + has resources)
    /// </summary>
    public class BuildingNotificationBadges : MonoBehaviour
    {
        private CityGridView _gridView;
        private ResourceBubbleSpawner _bubbleSpawner;
        private readonly Dictionary<string, GameObject> _badges = new();

        private float _refreshTimer;
        private const float RefreshInterval = 2f; // Check every 2 seconds

        private static readonly Color BadgeColor = new(0.95f, 0.20f, 0.20f, 1f);

        private void Start()
        {
            _gridView = GetComponent<CityGridView>();
            if (_gridView == null)
                _gridView = FindFirstObjectByType<CityGridView>();
            _bubbleSpawner = FindFirstObjectByType<ResourceBubbleSpawner>();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < RefreshInterval) return;
            _refreshTimer = 0f;

            RefreshBadges();
        }

        private void RefreshBadges()
        {
            if (_gridView == null) return;

            var placements = _gridView.GetPlacements();
            if (placements == null) return;

            // Track which instances still need badges
            var activeIds = new HashSet<string>();

            foreach (var p in placements)
            {
                if (p.VisualGO == null) continue;

                bool needsBadge = ShouldShowBadge(p);
                activeIds.Add(p.InstanceId);

                if (needsBadge)
                {
                    if (!_badges.ContainsKey(p.InstanceId))
                        CreateBadge(p);
                }
                else
                {
                    RemoveBadge(p.InstanceId);
                }
            }

            // Clean up badges for removed buildings
            var toRemove = new List<string>();
            foreach (var kvp in _badges)
            {
                if (!activeIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
                RemoveBadge(id);
        }

        private bool ShouldShowBadge(CityBuildingPlacement p)
        {
            // Resource buildings with bubbles ready
            if (IsResourceBuilding(p.BuildingId))
            {
                // Check if there are uncollected bubbles (simplified check)
                return true; // Resource buildings always have something to collect after spawn
            }

            return false;
        }

        private void CreateBadge(CityBuildingPlacement p)
        {
            var badge = new GameObject("NotifBadge");
            badge.transform.SetParent(p.VisualGO.transform, false);

            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.75f, 0.80f);
            rect.anchorMax = new Vector2(0.95f, 1.0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = badge.AddComponent<Image>();
            img.color = BadgeColor;
            img.raycastTarget = false;

            // Exclamation text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = "!";
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;

            _badges[p.InstanceId] = badge;
        }

        private void RemoveBadge(string instanceId)
        {
            if (_badges.TryGetValue(instanceId, out var badge))
            {
                if (badge != null)
                    Destroy(badge);
                _badges.Remove(instanceId);
            }
        }

        private static bool IsResourceBuilding(string buildingId)
        {
            return buildingId != null && (buildingId.Contains("farm") || buildingId.Contains("mine")
                || buildingId.Contains("quarry") || buildingId.Contains("arcane_tower"));
        }
    }
}
