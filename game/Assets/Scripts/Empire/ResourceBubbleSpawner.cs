using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Spawns collectible resource bubbles above resource-producing buildings.
    /// Bubbles appear at intervals and accumulate until tapped.
    /// Integrates with CityGridView to position bubbles above building visuals.
    /// </summary>
    public class ResourceBubbleSpawner : MonoBehaviour
    {
        [SerializeField] private CityGridView cityGrid;
        [SerializeField] private RectTransform bubbleContainer;

        private const float SpawnInterval = 20f;     // seconds between bubble spawns
        private const int MaxBubblesPerBuilding = 1;  // keep it clean — one per building
        private const long BaseCollectAmount = 50;    // base amount per bubble

        private float _spawnTimer;
        private readonly Dictionary<string, List<ResourceCollectBubble>> _activeBubbles = new();

        // Which building types produce which resource
        private static readonly Dictionary<string, ResourceType> ProducerMap = new()
        {
            { "grain_farm", ResourceType.Grain },
            { "iron_mine", ResourceType.Iron },
            { "stone_quarry", ResourceType.Stone },
            { "arcane_tower", ResourceType.ArcaneEssence },
        };

        private void Start()
        {
            _spawnTimer = SpawnInterval * 0.5f; // First spawn comes sooner
        }

        private void Update()
        {
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                SpawnBubbles();
            }

            // Clean up destroyed bubbles from tracking
            CleanupDestroyedBubbles();
        }

        private void SpawnBubbles()
        {
            if (cityGrid == null) return;

            var placements = cityGrid.GetPlacements();
            if (placements == null) return;

            foreach (var placement in placements)
            {
                if (!ProducerMap.TryGetValue(placement.BuildingId, out var resourceType))
                    continue;

                if (placement.VisualGO == null) continue;

                // Check bubble cap
                if (_activeBubbles.TryGetValue(placement.InstanceId, out var bubbles))
                {
                    if (bubbles.Count >= MaxBubblesPerBuilding)
                        continue;
                }

                SpawnBubble(placement, resourceType);
            }
        }

        private void SpawnBubble(CityBuildingPlacement placement, ResourceType type)
        {
            var container = bubbleContainer != null ? bubbleContainer : cityGrid.GetComponent<RectTransform>();
            if (container == null) return;

            var bubbleGO = new GameObject($"Bubble_{placement.InstanceId}_{type}");
            bubbleGO.transform.SetParent(container, false);

            var bubble = bubbleGO.AddComponent<ResourceCollectBubble>();

            // Scale amount by building tier
            long amount = BaseCollectAmount * placement.Tier;
            bubble.Initialize(type, amount, placement.InstanceId);

            // Position above the building
            var buildingRect = placement.VisualGO.GetComponent<RectTransform>();
            if (buildingRect != null)
            {
                // Get the building's position in the container's coordinate space
                var buildingPos = buildingRect.anchoredPosition;

                // Count existing bubbles for offset
                int existingCount = 0;
                if (_activeBubbles.TryGetValue(placement.InstanceId, out var existing))
                    existingCount = existing.Count;

                // Offset each bubble slightly so they don't stack perfectly
                float xOffset = (existingCount - 1) * 18f;
                float yOffset = buildingRect.sizeDelta.y * 0.4f + existingCount * 14f;

                bubble.SetBasePosition(buildingPos + new Vector2(xOffset, yOffset));
            }

            // Track the bubble
            if (!_activeBubbles.ContainsKey(placement.InstanceId))
                _activeBubbles[placement.InstanceId] = new List<ResourceCollectBubble>();
            _activeBubbles[placement.InstanceId].Add(bubble);
        }

        private void CleanupDestroyedBubbles()
        {
            foreach (var kvp in _activeBubbles)
            {
                kvp.Value.RemoveAll(b => b == null);
            }
        }

        /// <summary>
        /// Collect all bubbles for a specific building instance.
        /// </summary>
        public void CollectAllForBuilding(string instanceId)
        {
            if (!_activeBubbles.TryGetValue(instanceId, out var bubbles)) return;
            foreach (var bubble in bubbles)
            {
                if (bubble != null)
                    bubble.OnPointerClick(null);
            }
        }

        /// <summary>
        /// P&C: Collect all bubbles for ALL instances of a given building type.
        /// E.g., tapping "COLLECT ALL" on any grain_farm collects from all grain_farms.
        /// </summary>
        public void CollectAllForBuildingType(string buildingId)
        {
            if (cityGrid == null) return;
            int collected = 0;
            foreach (var placement in cityGrid.GetPlacements())
            {
                if (placement.BuildingId != buildingId) continue;
                if (!_activeBubbles.TryGetValue(placement.InstanceId, out var bubbles)) continue;
                foreach (var bubble in bubbles)
                {
                    if (bubble != null)
                    {
                        bubble.OnPointerClick(null);
                        collected++;
                    }
                }
            }
            if (collected > 0)
                Debug.Log($"[ResourceBubbleSpawner] Collected {collected} bubbles from all {buildingId} instances.");
        }
    }
}
