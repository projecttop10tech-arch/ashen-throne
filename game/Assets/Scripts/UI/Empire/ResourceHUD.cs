using UnityEngine;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Displays all four resource stockpiles and their per-hour production rates.
    /// Subscribes to ResourceChangedEvent for real-time updates.
    /// Subscribes to ProductionRatesUpdatedEvent to refresh rate labels.
    /// </summary>
    public class ResourceHUD : MonoBehaviour
    {
        [Header("Stone")]
        [SerializeField] private TextMeshProUGUI _stoneLabel;
        [SerializeField] private TextMeshProUGUI _stoneRateLabel;

        [Header("Iron")]
        [SerializeField] private TextMeshProUGUI _ironLabel;
        [SerializeField] private TextMeshProUGUI _ironRateLabel;

        [Header("Grain")]
        [SerializeField] private TextMeshProUGUI _grainLabel;
        [SerializeField] private TextMeshProUGUI _grainRateLabel;

        [Header("Arcane Essence")]
        [SerializeField] private TextMeshProUGUI _arcaneLabel;
        [SerializeField] private TextMeshProUGUI _arcaneRateLabel;

        private EventSubscription _resourceChangedSub;
        private EventSubscription _productionRatesSub;

        private ResourceManager _resourceManager;

        private void Awake()
        {
            _resourceManager = ServiceLocator.Get<ResourceManager>();
            if (_resourceManager == null)
                Debug.LogError("[ResourceHUD] ResourceManager not found in ServiceLocator.", this);
        }

        private void OnEnable()
        {
            _resourceChangedSub  = EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            _productionRatesSub  = EventBus.Subscribe<ProductionRatesUpdatedEvent>(OnProductionRatesUpdated);

            // Force full refresh on enable
            if (_resourceManager != null) RefreshAll();
        }

        private void OnDisable()
        {
            _resourceChangedSub?.Dispose();
            _productionRatesSub?.Dispose();
        }

        private void RefreshAll()
        {
            UpdateResourceLabel(ResourceType.Stone, _resourceManager.Stone, _resourceManager.MaxStone);
            UpdateResourceLabel(ResourceType.Iron, _resourceManager.Iron, _resourceManager.MaxIron);
            UpdateResourceLabel(ResourceType.Grain, _resourceManager.Grain, _resourceManager.MaxGrain);
            UpdateResourceLabel(ResourceType.ArcaneEssence, _resourceManager.ArcaneEssence, _resourceManager.MaxArcaneEssence);
            RefreshRateLabels();
        }

        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            if (_resourceManager == null) return;
            long max = evt.Type switch
            {
                ResourceType.Stone => _resourceManager.MaxStone,
                ResourceType.Iron => _resourceManager.MaxIron,
                ResourceType.Grain => _resourceManager.MaxGrain,
                ResourceType.ArcaneEssence => _resourceManager.MaxArcaneEssence,
                _ => 0L
            };
            UpdateResourceLabel(evt.Type, evt.NewValue, max);
        }

        private void OnProductionRatesUpdated(ProductionRatesUpdatedEvent evt)
        {
            // Rates are per-second internally; convert to per-hour for display
            SetText(_stoneRateLabel, FormatRate(evt.Stone * 3600f));
            SetText(_ironRateLabel, FormatRate(evt.Iron * 3600f));
            SetText(_grainRateLabel, FormatRate(evt.Grain * 3600f));
            SetText(_arcaneRateLabel, FormatRate(evt.Arcane * 3600f));
        }

        private void RefreshRateLabels()
        {
            if (_resourceManager == null) return;
            SetText(_stoneRateLabel, FormatRate(_resourceManager.StonePerSecond * 3600f));
            SetText(_ironRateLabel, FormatRate(_resourceManager.IronPerSecond * 3600f));
            SetText(_grainRateLabel, FormatRate(_resourceManager.GrainPerSecond * 3600f));
            SetText(_arcaneRateLabel, FormatRate(_resourceManager.ArcaneEssencePerSecond * 3600f));
        }

        private void UpdateResourceLabel(ResourceType type, long current, long max)
        {
            TextMeshProUGUI label = type switch
            {
                ResourceType.Stone => _stoneLabel,
                ResourceType.Iron => _ironLabel,
                ResourceType.Grain => _grainLabel,
                ResourceType.ArcaneEssence => _arcaneLabel,
                _ => null
            };
            SetText(label, FormatStock(current, max));
        }

        private static string FormatStock(long current, long max)
        {
            // Compact format: "4,200 / 5,000" or "4.2K / 5K" for large values
            if (max >= 1_000_000)
                return $"{current / 1000f:F1}K / {max / 1000f:F0}K";
            return $"{current:N0} / {max:N0}";
        }

        private static string FormatRate(float perHour)
        {
            if (perHour <= 0f) return "+0/h";
            if (perHour >= 1000f) return $"+{perHour / 1000f:F1}K/h";
            return $"+{perHour:F0}/h";
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
