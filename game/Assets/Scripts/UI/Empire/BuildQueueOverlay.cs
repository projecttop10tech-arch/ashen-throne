using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Persistent bottom overlay showing the active build queue (max 2 slots).
    /// Each slot shows: building name, target tier, progress bar, remaining time.
    /// Subscribes to BuildingUpgradeStartedEvent / CompletedEvent.
    /// </summary>
    public class BuildQueueOverlay : MonoBehaviour
    {
        [SerializeField] private BuildQueueSlotWidget[] _slots; // Max 2 slots

        private BuildingManager _buildingManager;

        private EventSubscription _startedSub;
        private EventSubscription _completedSub;

        private void Awake()
        {
            _buildingManager = ServiceLocator.Get<BuildingManager>();
            foreach (var slot in _slots)
                slot?.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _startedSub   = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnBuildStarted);
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildCompleted);
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            _startedSub?.Dispose();
            _completedSub?.Dispose();
        }

        private void Update()
        {
            if (_buildingManager == null) return;
            for (int i = 0; i < _slots.Length && i < _buildingManager.BuildQueue.Count; i++)
            {
                if (_slots[i] == null) continue;
                var entry = _buildingManager.BuildQueue[i];
                _slots[i].UpdateProgress(entry.RemainingSeconds);
            }
        }

        public void OnBuildStarted(string placedId, int targetTier, int buildTimeSeconds)
        {
            RefreshAllSlots();
        }

        public void OnBuildCompleted(string placedId)
        {
            RefreshAllSlots();
        }

        private void OnBuildStarted(BuildingUpgradeStartedEvent evt) =>
            OnBuildStarted(evt.PlacedId, evt.TargetTier, evt.BuildTimeSeconds);

        private void OnBuildCompleted(BuildingUpgradeCompletedEvent evt) =>
            OnBuildCompleted(evt.PlacedId);

        private void RefreshAllSlots()
        {
            if (_buildingManager == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;

                if (i < _buildingManager.BuildQueue.Count)
                {
                    BuildQueueEntry entry = _buildingManager.BuildQueue[i];
                    if (_buildingManager.PlacedBuildings.TryGetValue(entry.PlacedId, out PlacedBuilding placed))
                    {
                        _slots[i].gameObject.SetActive(true);
                        _slots[i].Bind(placed.Data.displayName, entry.TargetTier + 1, entry.RemainingSeconds);
                    }
                }
                else
                {
                    _slots[i].gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Single slot in the BuildQueueOverlay.
    /// </summary>
    public class BuildQueueSlotWidget : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _buildingNameLabel;
        [SerializeField] private TextMeshProUGUI _tierLabel;
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [SerializeField] private Slider _progressBar;

        private float _totalSeconds;

        public void Bind(string buildingName, int tier, float remainingSeconds)
        {
            _totalSeconds = remainingSeconds;
            if (_buildingNameLabel != null) _buildingNameLabel.text = buildingName;
            if (_tierLabel != null) _tierLabel.text = $"→ Lv {tier}";
            UpdateProgress(remainingSeconds);
        }

        public void UpdateProgress(float remainingSeconds)
        {
            float ratio = _totalSeconds > 0f ? 1f - (remainingSeconds / _totalSeconds) : 1f;
            if (_progressBar != null) _progressBar.value = Mathf.Clamp01(ratio);
            if (_timeLabel != null) _timeLabel.text = FormatTime(Mathf.CeilToInt(remainingSeconds));
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Done";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }
    }
}
