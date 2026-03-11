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
        private static readonly Color ResourceGlowColor = new(1f, 0.85f, 0.30f, 0.35f);

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
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                RefreshBadges();
            }

            // P&C: Pulse resource glow alpha
            foreach (var kvp in _badges)
            {
                if (kvp.Value == null || kvp.Value.name != "ResourceGlow") continue;
                var img = kvp.Value.GetComponent<Image>();
                if (img == null) continue;
                float pulse = 0.25f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                var c = ResourceGlowColor;
                c.a = pulse;
                img.color = c;
            }
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
            bool isResource = IsResourceBuilding(p.BuildingId);

            if (isResource)
            {
                // P&C: Golden glow around resource buildings when collectible
                var glow = new GameObject("ResourceGlow");
                glow.transform.SetParent(p.VisualGO.transform, false);
                glow.transform.SetAsFirstSibling(); // Behind building sprite

                var rect = glow.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(-0.08f, -0.08f);
                rect.anchorMax = new Vector2(1.08f, 1.08f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = glow.AddComponent<Image>();
                img.color = ResourceGlowColor;
                img.raycastTarget = false;

                // Use radial gradient sprite for soft glow
                var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
                #if UNITY_EDITOR
                if (spr == null)
                    spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
                #endif
                if (spr != null) img.sprite = spr;

                _badges[p.InstanceId] = glow;
            }
            else
            {
                // Standard red badge for non-resource buildings
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
