using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Compact resource bar (P&C style) — icon + abbreviated number for each resource.
    /// Subscribes to ResourceChangedEvent for real-time updates.
    /// Tapping a resource slot opens the ResourceDetailPopup.
    /// </summary>
    public class ResourceHUD : MonoBehaviour
    {
        [Header("Amount Labels (abbreviated: 8.37M, 365K)")]
        [SerializeField] private TextMeshProUGUI _grainAmount;
        [SerializeField] private TextMeshProUGUI _ironAmount;
        [SerializeField] private TextMeshProUGUI _stoneAmount;
        [SerializeField] private TextMeshProUGUI _arcaneAmount;
        [SerializeField] private TextMeshProUGUI _gemsAmount;

        [Header("Resource Detail Popup")]
        [SerializeField] private GameObject _detailPopup;
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailCurrentValue;
        [SerializeField] private TextMeshProUGUI _detailCapacityValue;
        [SerializeField] private TextMeshProUGUI _detailProductionValue;
        [SerializeField] private Image _detailCapacityFill;

        private EventSubscription _resourceChangedSub;
        private ResourceManager _resourceManager;
        private long _gems; // Premium currency — tracked separately

        private void Awake()
        {
            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                _resourceManager = rm;
            else
                Debug.LogWarning("[ResourceHUD] ResourceManager not found — will retry on Enable.", this);
        }

        private void OnEnable()
        {
            if (_resourceManager == null)
                ServiceLocator.TryGet<ResourceManager>(out _resourceManager);

            _resourceChangedSub = EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            if (_resourceManager != null) RefreshAll();
        }

        private void OnDisable()
        {
            _resourceChangedSub?.Dispose();
        }

        private void RefreshAll()
        {
            SetText(_grainAmount, Abbreviate(_resourceManager.Grain));
            SetText(_ironAmount, Abbreviate(_resourceManager.Iron));
            SetText(_stoneAmount, Abbreviate(_resourceManager.Stone));
            SetText(_arcaneAmount, Abbreviate(_resourceManager.ArcaneEssence));
            SetText(_gemsAmount, Abbreviate(_gems));
        }

        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            TextMeshProUGUI label = evt.Type switch
            {
                ResourceType.Stone => _stoneAmount,
                ResourceType.Iron => _ironAmount,
                ResourceType.Grain => _grainAmount,
                ResourceType.ArcaneEssence => _arcaneAmount,
                _ => null
            };
            SetText(label, Abbreviate(evt.NewValue));
        }

        /// <summary>
        /// Show the resource detail popup for a specific resource.
        /// Called from UI button OnClick events on each resource slot.
        /// </summary>
        public void ShowDetail(int resourceIndex)
        {
            if (_detailPopup == null || _resourceManager == null) return;

            string name;
            long current, max;
            float perHour;

            switch (resourceIndex)
            {
                case 0: name = "GRAIN"; current = _resourceManager.Grain; max = _resourceManager.MaxGrain; perHour = _resourceManager.GrainPerSecond * 3600f; break;
                case 1: name = "IRON"; current = _resourceManager.Iron; max = _resourceManager.MaxIron; perHour = _resourceManager.IronPerSecond * 3600f; break;
                case 2: name = "STONE"; current = _resourceManager.Stone; max = _resourceManager.MaxStone; perHour = _resourceManager.StonePerSecond * 3600f; break;
                case 3: name = "ARCANE"; current = _resourceManager.ArcaneEssence; max = _resourceManager.MaxArcaneEssence; perHour = _resourceManager.ArcaneEssencePerSecond * 3600f; break;
                default: return;
            }

            SetText(_detailTitle, name);
            SetText(_detailCurrentValue, current.ToString("N0"));
            SetText(_detailCapacityValue, max.ToString("N0"));
            SetText(_detailProductionValue, $"+{Abbreviate((long)perHour)}/hr");
            if (_detailCapacityFill != null && max > 0)
                _detailCapacityFill.fillAmount = Mathf.Clamp01((float)current / max);
            _detailPopup.SetActive(true);
        }

        public void HideDetail()
        {
            if (_detailPopup != null) _detailPopup.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Number abbreviation: 1000 → 1K, 1500000 → 1.50M, 2300000000 → 2.30B
        // ---------------------------------------------------------------

        /// <summary>
        /// Abbreviates a number for compact display.
        /// Under 1,000: exact number.  1K–999K.  1.00M–999M.  1.00B+.
        /// </summary>
        public static string Abbreviate(long value)
        {
            if (value < 0) return "-" + Abbreviate(-value);
            if (value < 1_000) return value.ToString();
            if (value < 10_000) return $"{value / 1_000f:F2}K";          // 1.23K
            if (value < 1_000_000) return $"{value / 1_000f:F0}K";       // 365K
            if (value < 1_000_000_000L) return $"{value / 1_000_000f:F2}M"; // 8.37M
            return $"{value / 1_000_000_000f:F2}B";                      // 2.30B
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
