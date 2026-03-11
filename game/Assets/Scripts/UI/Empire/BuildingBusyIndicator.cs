using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style busy indicators on buildings performing active tasks.
    /// Shows a small progress bar + activity label on research buildings (when research active),
    /// barracks/training grounds (when build queue active), and any building being upgraded.
    /// Distinct from ConstructionOverlayManager which handles the scaffold/hammer overlay.
    /// This shows functional activity (research, training) not construction.
    /// </summary>
    public class BuildingBusyIndicator : MonoBehaviour
    {
        private CityGridView _gridView;
        private ResearchManager _researchManager;

        private readonly Dictionary<string, GameObject> _indicators = new();

        // Buildings that show research progress
        private static readonly HashSet<string> ResearchBuildings = new()
        {
            "library", "academy", "laboratory", "observatory", "archive"
        };

        private static readonly Color ProgressBg = new(0.08f, 0.06f, 0.12f, 0.85f);
        private static readonly Color ProgressFill = new(0.30f, 0.75f, 1f, 1f);
        private static readonly Color ResearchGold = new(0.90f, 0.78f, 0.30f, 1f);

        private void Start()
        {
            _gridView = FindFirstObjectByType<CityGridView>();
            ServiceLocator.TryGet(out _researchManager);
        }

        private void Update()
        {
            if (_gridView == null || _researchManager == null) return;
            UpdateResearchIndicators();
        }

        private void UpdateResearchIndicators()
        {
            // Check if research is active
            var queue = _researchManager.ResearchQueue;
            bool researchActive = queue != null && queue.Count > 0;

            var placements = _gridView.GetPlacements();
            var activeIds = new HashSet<string>();

            foreach (var p in placements)
            {
                if (!ResearchBuildings.Contains(p.BuildingId)) continue;

                if (researchActive)
                {
                    activeIds.Add(p.InstanceId);
                    // Show indicator on the first matching research building only
                    // (P&C shows it on the relevant lab/academy)
                    EnsureIndicator(p, queue[0]);
                    break; // Only one research at a time
                }
            }

            // Remove stale indicators
            var stale = new List<string>();
            foreach (var kvp in _indicators)
            {
                if (!activeIds.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }
            foreach (var key in stale)
                RemoveIndicator(key);
        }

        private void EnsureIndicator(CityBuildingPlacement placement, ResearchQueueEntry entry)
        {
            if (placement.VisualGO == null) return;

            if (_indicators.TryGetValue(placement.InstanceId, out var existing))
            {
                if (existing != null)
                {
                    UpdateIndicator(existing, entry);
                    return;
                }
                _indicators.Remove(placement.InstanceId);
            }

            CreateIndicator(placement, entry);
        }

        private void CreateIndicator(CityBuildingPlacement placement, ResearchQueueEntry entry)
        {
            var go = new GameObject($"BusyIndicator_{placement.InstanceId}");
            go.transform.SetParent(placement.VisualGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            // Bottom of building
            rect.anchorMin = new Vector2(0.05f, -0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.08f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Background bar
            var bg = go.AddComponent<Image>();
            bg.color = ProgressBg;
            bg.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.5f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            // Fill bar
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // Start empty
            fillRect.offsetMin = new Vector2(1, 1);
            fillRect.offsetMax = new Vector2(-1, -1);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = ProgressFill;
            fillImg.raycastTarget = false;

            // Activity icon (small book/beaker symbol)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0.15f, 1f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = "\u2697"; // ⚗ (alembic)
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 9;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = ResearchGold;
            iconText.raycastTarget = false;

            // Timer text
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(go.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.15f, 0f);
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 8;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = Color.white;
            timerText.raycastTarget = false;

            _indicators[placement.InstanceId] = go;
            UpdateIndicator(go, entry);
        }

        private void UpdateIndicator(GameObject indicator, ResearchQueueEntry entry)
        {
            if (indicator == null) return;

            // Update fill bar width based on progress
            // We don't have total duration stored, estimate from remaining
            // For display, show the timer countdown
            var fillRect = indicator.transform.Find("Fill")?.GetComponent<RectTransform>();
            if (fillRect != null)
            {
                // Estimate progress: as remaining goes down, fill goes up
                // Without total, we'll pulse the bar at a fixed rate as visual feedback
                float pulse = 0.3f + 0.15f * Mathf.Sin(Time.time * 2f);
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(1f - (entry.RemainingSeconds / 3600f) + pulse), 1f);
            }

            var timerText = indicator.transform.Find("Timer")?.GetComponent<Text>();
            if (timerText != null)
                timerText.text = FormatTime(entry.RemainingSeconds);
        }

        private void RemoveIndicator(string instanceId)
        {
            if (_indicators.TryGetValue(instanceId, out var go))
            {
                if (go != null) Destroy(go);
                _indicators.Remove(instanceId);
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds >= 3600f)
            {
                int h = (int)(seconds / 3600f);
                int m = (int)((seconds % 3600f) / 60f);
                return $"{h}h{m:D2}m";
            }
            if (seconds >= 60f)
            {
                int m = (int)(seconds / 60f);
                int s = (int)(seconds % 60f);
                return $"{m}:{s:D2}";
            }
            return $"{(int)seconds}s";
        }
    }
}
