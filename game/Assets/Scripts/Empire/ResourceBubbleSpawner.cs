using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Spawns collectible resource bubbles above resource-producing buildings.
    /// Bubbles appear at intervals and accumulate until tapped.
    /// Integrates with CityGridView to position bubbles above building visuals.
    /// P&C: Manages a floating "Collect All" button that appears when bubbles exist.
    /// </summary>
    public class ResourceBubbleSpawner : MonoBehaviour
    {
        [SerializeField] private CityGridView cityGrid;
        [SerializeField] private RectTransform bubbleContainer;

        public const float SpawnInterval = 20f;     // seconds between bubble spawns
        private const int MaxBubblesPerBuilding = 1;  // keep it clean — one per building
        private const long BaseCollectAmount = 50;    // base amount per bubble

        private float _spawnTimer;

        /// <summary>Seconds until next bubble spawn wave.</summary>
        public float SecondsUntilNextSpawn => Mathf.Max(0f, SpawnInterval - _spawnTimer);

        /// <summary>Whether a specific building already has max bubbles (no new one will spawn).</summary>
        public bool HasMaxBubbles(string instanceId)
        {
            return _activeBubbles.TryGetValue(instanceId, out var bubbles) && bubbles.Count >= MaxBubblesPerBuilding;
        }
        private readonly Dictionary<string, List<ResourceCollectBubble>> _activeBubbles = new();

        // Which building types produce which resource
        private static readonly Dictionary<string, ResourceType> ProducerMap = new()
        {
            { "grain_farm", ResourceType.Grain },
            { "iron_mine", ResourceType.Iron },
            { "stone_quarry", ResourceType.Stone },
            { "arcane_tower", ResourceType.ArcaneEssence },
        };

        private System.IDisposable _batchCollectSub;

        private void Start()
        {
            _spawnTimer = SpawnInterval * 0.5f; // First spawn comes sooner
            // P&C: Listen for batch-collect events (tap one bubble → collect all same type)
            _batchCollectSub = EventBus.Subscribe<ResourceBubbleBatchCollectEvent>(OnBatchCollect);
        }

        private void OnDestroy()
        {
            _batchCollectSub?.Dispose();
        }

        /// <summary>P&C: When any bubble is tapped, collect ALL bubbles producing the same resource type.</summary>
        private void OnBatchCollect(ResourceBubbleBatchCollectEvent evt)
        {
            foreach (var kvp in _activeBubbles)
            {
                foreach (var bubble in kvp.Value)
                {
                    if (bubble != null && bubble.ResourceType == evt.Type)
                        bubble.OnPointerClick(null); // null eventData = programmatic collect
                }
            }
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

        // P&C: Floating "Collect All" button
        private GameObject _collectAllButton;
        private float _collectAllCheckTimer;
        private const float CollectAllCheckInterval = 1f;

        private void LateUpdate()
        {
            // Show/hide Collect All button based on active bubble count
            _collectAllCheckTimer += Time.deltaTime;
            if (_collectAllCheckTimer >= CollectAllCheckInterval)
            {
                _collectAllCheckTimer = 0f;
                UpdateCollectAllButton();
            }
        }

        private void UpdateCollectAllButton()
        {
            int totalBubbles = GetTotalActiveBubbleCount();
            bool shouldShow = totalBubbles > 0;

            if (shouldShow && _collectAllButton == null)
                CreateCollectAllButton();
            else if (!shouldShow && _collectAllButton != null)
            {
                Destroy(_collectAllButton);
                _collectAllButton = null;
            }

            // Update count label
            if (_collectAllButton != null && totalBubbles > 0)
            {
                var label = _collectAllButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"COLLECT ALL ({totalBubbles})";
            }
        }

        private void CreateCollectAllButton()
        {
            // Find the Canvas to parent to (above building container)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _collectAllButton = new GameObject("CollectAllButton");
            _collectAllButton.transform.SetParent(canvas.transform, false);
            var rect = _collectAllButton.AddComponent<RectTransform>();
            // P&C: Bottom-center, above nav bar (avoid overlap with auto-collect toggle at right)
            rect.anchorMin = new Vector2(0.25f, 0.14f);
            rect.anchorMax = new Vector2(0.70f, 0.19f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Background
            var bg = _collectAllButton.AddComponent<Image>();
            bg.color = new Color(0.30f, 0.15f, 0.55f, 0.92f); // dark fantasy purple
            bg.raycastTarget = true;

            // Gold border
            var outline = _collectAllButton.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Button
            var btn = _collectAllButton.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(CollectAll);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_collectAllButton.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelGO.AddComponent<Text>();
            text.text = "COLLECT ALL";
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.8f, -0.8f);

            // P&C: Pulse animation to draw attention
            StartCoroutine(PulseCollectAllButton());
        }

        private System.Collections.IEnumerator PulseCollectAllButton()
        {
            while (_collectAllButton != null)
            {
                var img = _collectAllButton.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
                    float purpleBase = 0.55f;
                    img.color = new Color(0.30f * pulse, 0.15f, purpleBase * pulse, 0.92f);
                }
                // Subtle scale pulse
                float scale = 1f + 0.03f * Mathf.Sin(Time.time * 2.5f);
                _collectAllButton.transform.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        /// <summary>P&C: Collect ALL active bubbles with staggered cascade animation.</summary>
        public void CollectAll()
        {
            StartCoroutine(CollectAllCascade());
        }

        private System.Collections.IEnumerator CollectAllCascade()
        {
            var toCollect = new List<ResourceCollectBubble>();
            foreach (var kvp in _activeBubbles)
            {
                foreach (var bubble in kvp.Value)
                {
                    if (bubble != null) toCollect.Add(bubble);
                }
            }
            if (toCollect.Count == 0) yield break;

            // P&C: Stagger collection with 0.08s delay per bubble for cascade feel
            int collected = 0;
            foreach (var bubble in toCollect)
            {
                if (bubble != null)
                {
                    bubble.OnPointerClick(null);
                    collected++;
                    yield return new UnityEngine.WaitForSeconds(0.08f);
                }
            }
            if (collected > 0)
                Debug.Log($"[ResourceBubbleSpawner] Cascade-collected {collected} bubbles.");
        }

        /// <summary>Total number of active (uncollected) bubbles across all buildings.</summary>
        public int GetTotalActiveBubbleCount()
        {
            int count = 0;
            foreach (var kvp in _activeBubbles)
            {
                foreach (var bubble in kvp.Value)
                {
                    if (bubble != null) count++;
                }
            }
            return count;
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
