using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Creates and manages construction progress overlays on buildings during upgrades.
    /// P&C-style: shows timer countdown + progress bar directly on the building sprite.
    /// Also shows "upgrade available" green arrow indicators on idle buildings.
    /// </summary>
    public class ConstructionOverlayManager : MonoBehaviour
    {
        private CityGridView _gridView;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;

        private readonly Dictionary<string, ConstructionOverlay> _overlays = new();
        private readonly Dictionary<string, GameObject> _upgradeArrows = new();
        private readonly Dictionary<string, BuildingData> _buildingDataCache = new();

        private EventSubscription _upgradeStartedSub;
        private EventSubscription _upgradeCompletedSub;

        private float _arrowRefreshTimer;
        private const float ArrowRefreshInterval = 2f;
        private float _arrowPulsePhase;

        private static readonly Color ProgressBgColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color ProgressFillColor = new(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color TimerTextColor = new(0.98f, 0.96f, 0.92f, 1f);
        private static readonly Color UpgradeArrowColor = new(0.30f, 0.85f, 0.40f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
            ServiceLocator.TryGet(out _resourceManager);
            CacheBuildingData();
        }

        private void Start()
        {
            _gridView = GetComponent<CityGridView>();
            if (_gridView == null)
                _gridView = FindFirstObjectByType<CityGridView>();
            // Initial arrow refresh after a short delay
            _arrowRefreshTimer = ArrowRefreshInterval - 0.5f;
        }

        private void CacheBuildingData()
        {
            var allData = Resources.LoadAll<BuildingData>("");
            foreach (var d in allData)
            {
                if (d != null && !string.IsNullOrEmpty(d.buildingId))
                    _buildingDataCache[d.buildingId] = d;
            }
        }

        private EventSubscription _upgradeCancelledSub;
        private EventSubscription _speedupAppliedSub;
        private readonly HashSet<string> _instantCompletePending = new();

        private void OnEnable()
        {
            _upgradeStartedSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _upgradeCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _upgradeCancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(OnUpgradeCancelled);
            _speedupAppliedSub = EventBus.Subscribe<SpeedupAppliedEvent>(OnSpeedupApplied);
        }

        private void OnDisable()
        {
            _upgradeStartedSub?.Dispose();
            _upgradeCompletedSub?.Dispose();
            _upgradeCancelledSub?.Dispose();
            _speedupAppliedSub?.Dispose();
        }

        private void Update()
        {
            if (_buildingManager == null) return;

            // Update active construction overlays
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (_overlays.TryGetValue(entry.PlacedId, out var overlay))
                {
                    float total = overlay.TotalSeconds;
                    float remaining = entry.RemainingSeconds;
                    float progress = total > 0 ? 1f - (remaining / total) : 1f;

                    overlay.ProgressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                    // Fill turns gold near completion
                    overlay.ProgressFill.GetComponent<Image>().color = progress > 0.9f
                        ? new Color(0.83f, 0.66f, 0.26f, 1f) : ProgressFillColor;
                    // P&C: Show timer + percentage
                    int pct = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
                    overlay.TimerText.text = $"{FormatTime(Mathf.CeilToInt(remaining))} ({pct}%)";
                    // Timer turns red at < 10 seconds
                    overlay.TimerText.color = remaining < 10f
                        ? new Color(1f, 0.3f, 0.3f, 1f) : TimerTextColor;

                    // P&C: Hammer swing animation — oscillating rotation
                    if (overlay.HammerIcon != null)
                    {
                        float swing = Mathf.Sin(Time.time * 4f) * 15f; // ±15° at 4Hz
                        overlay.HammerIcon.transform.localRotation = Quaternion.Euler(0, 0, swing);
                    }
                }
            }

            // P&C: Refresh upgrade-available arrows periodically
            _arrowRefreshTimer += Time.deltaTime;
            if (_arrowRefreshTimer >= ArrowRefreshInterval)
            {
                _arrowRefreshTimer = 0f;
                RefreshUpgradeArrows();
            }

            // Pulse animation on all visible arrows
            _arrowPulsePhase += Time.deltaTime * 3f;
            float pulse = 0.7f + 0.3f * Mathf.Sin(_arrowPulsePhase);
            foreach (var kvp in _upgradeArrows)
            {
                if (kvp.Value != null)
                {
                    var img = kvp.Value.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(UpgradeArrowColor.r, UpgradeArrowColor.g, UpgradeArrowColor.b, pulse);
                }
            }
        }

        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            // Find the building visual
            if (_gridView == null) return;

            CityBuildingPlacement placement = null;
            foreach (var p in _gridView.GetPlacements())
            {
                if (p.InstanceId == evt.PlacedId)
                {
                    placement = p;
                    break;
                }
            }

            if (placement?.VisualGO == null) return;

            // Remove any existing overlay
            RemoveOverlay(evt.PlacedId);

            // Create construction overlay on the building
            var overlay = CreateConstructionOverlay(placement.VisualGO, evt.BuildTimeSeconds);
            _overlays[evt.PlacedId] = overlay;
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            // P&C: Check if this was an instant-complete (free speedup)
            bool isInstant = _instantCompletePending.Remove(evt.PlacedId);

            if (_overlays.TryGetValue(evt.PlacedId, out var overlay) && overlay.Root != null)
            {
                PlayCompletionCelebration(overlay.Root.transform.parent, isInstant);
            }
            RemoveOverlay(evt.PlacedId);
        }

        /// <summary>
        /// P&C: Track speedup events. When remaining hits 0, mark as instant complete
        /// so the celebration uses a white flash instead of gold glow.
        /// </summary>
        private void OnSpeedupApplied(SpeedupAppliedEvent evt)
        {
            if (evt.RemainingSeconds <= 0f)
                _instantCompletePending.Add(evt.PlacedId);
        }

        private void OnUpgradeCancelled(BuildingUpgradeCancelledEvent evt)
        {
            RemoveOverlay(evt.PlacedId);
            _instantCompletePending.Remove(evt.PlacedId);
        }

        private void RemoveOverlay(string placedId)
        {
            if (_overlays.TryGetValue(placedId, out var overlay))
            {
                if (overlay.Root != null)
                    Destroy(overlay.Root);
                _overlays.Remove(placedId);
            }
        }

        private ConstructionOverlay CreateConstructionOverlay(GameObject buildingGO, float totalSeconds)
        {
            // Root container positioned at bottom of building
            var root = new GameObject("ConstructionOverlay");
            root.transform.SetParent(buildingGO.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.05f, 0.02f);
            rootRect.anchorMax = new Vector2(0.95f, 0.18f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Progress bar background
            var progBg = new GameObject("ProgressBg");
            progBg.transform.SetParent(root.transform, false);
            var bgRect = progBg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = progBg.AddComponent<Image>();
            bgImg.color = ProgressBgColor;
            bgImg.raycastTarget = false;

            // Progress fill
            var progFill = new GameObject("ProgressFill");
            progFill.transform.SetParent(progBg.transform, false);
            var fillRect = progFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.01f, 1f); // starts at 1%
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = progFill.AddComponent<Image>();
            fillImg.color = ProgressFillColor;
            fillImg.raycastTarget = false;

            // Timer text
            var timerGO = new GameObject("TimerText");
            timerGO.transform.SetParent(root.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = FormatTime(Mathf.CeilToInt(totalSeconds));
            timerText.fontSize = 11;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = TimerTextColor;
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontStyle = FontStyle.Bold;
            timerText.raycastTarget = false;
            var timerShadow = timerGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.9f);
            timerShadow.effectDistance = new Vector2(1f, -1f);

            // Construction icon (hammer) above the building
            var hammerGO = new GameObject("ConstructionIcon");
            hammerGO.transform.SetParent(buildingGO.transform, false);
            var hammerRect = hammerGO.AddComponent<RectTransform>();
            hammerRect.anchorMin = new Vector2(0.35f, 0.85f);
            hammerRect.anchorMax = new Vector2(0.65f, 1.10f);
            hammerRect.offsetMin = Vector2.zero;
            hammerRect.offsetMax = Vector2.zero;
            var hammerImg = hammerGO.AddComponent<Image>();
            hammerImg.raycastTarget = false;
            // Try to load the build icon
            var buildIconSpr = Resources.Load<Sprite>("UI/Production/icon_build");
            if (buildIconSpr == null)
            {
                #if UNITY_EDITOR
                buildIconSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_build.png");
                #endif
            }
            if (buildIconSpr != null)
            {
                hammerImg.sprite = buildIconSpr;
                hammerImg.preserveAspect = true;
                hammerImg.color = Color.white;
            }
            else
            {
                hammerImg.color = UpgradeArrowColor;
            }

            return new ConstructionOverlay
            {
                Root = root,
                ProgressFill = fillRect,
                TimerText = timerText,
                HammerIcon = hammerGO,
                TotalSeconds = totalSeconds
            };
        }

        // ====================================================================
        // P&C: Upgrade-available arrows on idle buildings
        // ====================================================================

        private static readonly Color RecommendedArrowColor = new(0.95f, 0.78f, 0.20f, 1f); // Gold

        private void RefreshUpgradeArrows()
        {
            if (_gridView == null) return;

            // Build a set of currently upgrading building IDs
            var upgrading = new HashSet<string>();
            foreach (var entry in _buildingManager.BuildQueue)
                upgrading.Add(entry.PlacedId);

            // P&C: Find recommended building — Stronghold is always priority if upgradeable
            string recommendedId = null;
            int lowestStrongholdTier = int.MaxValue;

            var upgradeable = new List<(CityBuildingPlacement placement, bool canAfford)>();

            foreach (var placement in _gridView.GetPlacements())
            {
                if (upgrading.Contains(placement.InstanceId)) continue;
                if (!_buildingDataCache.TryGetValue(placement.BuildingId, out var data)) continue;

                var nextTier = data.GetTier(placement.Tier + 1);
                if (nextTier == null) continue;

                bool canAfford = _resourceManager != null && _resourceManager.CanAfford(
                    nextTier.stoneCost, nextTier.ironCost,
                    nextTier.grainCost, nextTier.arcaneEssenceCost);
                bool queueFree = _buildingManager.BuildQueue.Count < BuildingManager.FreeQueueSlots;

                if (canAfford && queueFree)
                    upgradeable.Add((placement, true));

                // Track Stronghold as recommended
                if (placement.BuildingId == "stronghold" && placement.Tier < lowestStrongholdTier)
                {
                    lowestStrongholdTier = placement.Tier;
                    if (canAfford && queueFree)
                        recommendedId = placement.InstanceId;
                }
            }

            // Show/hide arrows
            var shown = new HashSet<string>();
            foreach (var (placement, _) in upgradeable)
            {
                bool isRecommended = placement.InstanceId == recommendedId;
                ShowUpgradeArrow(placement, isRecommended);
                shown.Add(placement.InstanceId);
            }

            // Hide arrows for buildings that are no longer upgradeable
            var toHide = new List<string>();
            foreach (var kvp in _upgradeArrows)
            {
                if (!shown.Contains(kvp.Key))
                    toHide.Add(kvp.Key);
            }
            foreach (var id in toHide)
                HideUpgradeArrow(id);
        }

        private void ShowUpgradeArrow(CityBuildingPlacement placement, bool recommended)
        {
            if (placement.VisualGO == null) return;

            // If already shown, check if recommended state changed
            if (_upgradeArrows.TryGetValue(placement.InstanceId, out var existing))
            {
                if (existing != null)
                {
                    // Update color based on recommended state
                    var existingImg = existing.GetComponent<Image>();
                    if (existingImg != null)
                        existingImg.color = recommended ? RecommendedArrowColor : UpgradeArrowColor;
                }
                return;
            }

            Color arrowColor = recommended ? RecommendedArrowColor : UpgradeArrowColor;

            var arrowGO = new GameObject("UpgradeArrow");
            arrowGO.transform.SetParent(placement.VisualGO.transform, false);
            var rect = arrowGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.30f, 0.90f);
            rect.anchorMax = new Vector2(0.70f, 1.15f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = arrowGO.AddComponent<Image>();
            img.raycastTarget = true; // Clickable for quick-upgrade
            img.color = arrowColor;

            // P&C: Quick-upgrade — tapping arrow starts upgrade directly
            var btn = arrowGO.AddComponent<Button>();
            btn.targetGraphic = img;
            string instanceId = placement.InstanceId;
            btn.onClick.AddListener(() => OnQuickUpgradePressed(instanceId));

            // Arrow symbol: ▲ for normal, ★ for recommended
            var textGO = new GameObject("ArrowText");
            textGO.transform.SetParent(arrowGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = recommended ? "\u2605" : "\u25B2"; // ★ or ▲
            text.fontSize = recommended ? 16 : 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.8f, -0.8f);

            _upgradeArrows[placement.InstanceId] = arrowGO;
        }

        private void HideUpgradeArrow(string instanceId)
        {
            if (_upgradeArrows.TryGetValue(instanceId, out var arrowGO))
            {
                if (arrowGO != null)
                    Destroy(arrowGO);
                _upgradeArrows.Remove(instanceId);
            }
        }

        /// <summary>
        /// P&C: Quick-upgrade — tapping the arrow directly starts the upgrade.
        /// </summary>
        private void OnQuickUpgradePressed(string instanceId)
        {
            if (_buildingManager == null) return;
            bool started = _buildingManager.StartUpgrade(instanceId);
            if (started)
            {
                Debug.Log($"[ConstructionOverlay] Quick-upgrade started for {instanceId}.");
                HideUpgradeArrow(instanceId);
            }
        }

        private void ClearAllArrows()
        {
            foreach (var kvp in _upgradeArrows)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _upgradeArrows.Clear();
        }

        /// <summary>
        /// P&C-style celebration when upgrade completes.
        /// Normal: golden glow burst. Instant (free speedup): bright white flash.
        /// </summary>
        private void PlayCompletionCelebration(Transform buildingTransform, bool instant = false)
        {
            if (buildingTransform == null) return;

            Color glowColor = instant
                ? new Color(1f, 1f, 1f, 0.95f)       // White flash for instant
                : new Color(0.83f, 0.66f, 0.26f, 0.8f); // Gold glow for normal

            var glow = new GameObject("CompletionGlow");
            glow.transform.SetParent(buildingTransform, false);
            var rect = glow.AddComponent<RectTransform>();
            float spread = instant ? 0.25f : 0.15f;
            rect.anchorMin = new Vector2(-spread, -spread);
            rect.anchorMax = new Vector2(1f + spread, 1f + spread);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = glow.AddComponent<Image>();
            var gradientSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (gradientSpr == null)
                gradientSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (gradientSpr != null)
                img.sprite = gradientSpr;
            img.color = glowColor;
            img.raycastTarget = false;

            float duration = instant ? 0.6f : 1.0f;
            StartCoroutine(AnimateCelebration(glow, rect, glowColor, duration, instant));
        }

        private System.Collections.IEnumerator AnimateCelebration(
            GameObject glow, RectTransform rect, Color baseColor, float duration, bool instant)
        {
            float elapsed = 0f;
            var img = glow.GetComponent<Image>();
            float startScale = instant ? 0.3f : 0.5f;
            float endScale = instant ? 1.6f : 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = Mathf.Lerp(startScale, endScale, t);
                rect.localScale = new Vector3(scale, scale, 1f);

                float alpha = Mathf.Lerp(baseColor.a, 0f, t * t);
                if (img != null)
                    img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                yield return null;
            }

            Destroy(glow);
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

        private class ConstructionOverlay
        {
            public GameObject Root;
            public RectTransform ProgressFill;
            public Text TimerText;
            public GameObject HammerIcon;
            public float TotalSeconds;
        }
    }
}
