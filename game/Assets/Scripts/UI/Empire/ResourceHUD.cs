using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality resource bar — icon + abbreviated number for each resource.
    /// Vault overflow: color warning + scale pulse. Detail popup with pop-in animation.
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
        private long _gems;
        private Coroutine _popupAnim;

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

        // P&C: Vault overflow warning colors
        private static readonly Color NormalColor = Color.white;
        private static readonly Color WarningColor = new(1f, 0.65f, 0.20f, 1f);
        private static readonly Color CriticalColor = new(1f, 0.25f, 0.25f, 1f);
        private static readonly Color FullColor = new(1f, 0.15f, 0.15f, 1f);

        private float _flashPhase;

        private void Update()
        {
            if (_resourceManager == null) return;
            _flashPhase += Time.deltaTime * 3.5f;
            float flash = 0.5f + 0.5f * Mathf.Sin(_flashPhase);

            PulseIfFull(_grainAmount, _resourceManager.Grain, _resourceManager.MaxGrain, flash);
            PulseIfFull(_ironAmount, _resourceManager.Iron, _resourceManager.MaxIron, flash);
            PulseIfFull(_stoneAmount, _resourceManager.Stone, _resourceManager.MaxStone, flash);
            PulseIfFull(_arcaneAmount, _resourceManager.ArcaneEssence, _resourceManager.MaxArcaneEssence, flash);
        }

        private static void PulseIfFull(TextMeshProUGUI label, long current, long max, float flash)
        {
            if (label == null || max <= 0) return;
            float ratio = (float)current / max;
            if (ratio >= 0.95f)
            {
                // P&C: Alpha pulse + scale pulse for critical/full state
                var c = label.color;
                label.color = new Color(c.r, c.g, c.b, 0.5f + 0.5f * flash);
                float scale = 1f + 0.05f * flash;
                label.transform.localScale = Vector3.one * scale;
            }
            else
            {
                label.transform.localScale = Vector3.one;
            }
        }

        private void RefreshAll()
        {
            UpdateResourceDisplay(_grainAmount, _resourceManager.Grain, _resourceManager.MaxGrain);
            UpdateResourceDisplay(_ironAmount, _resourceManager.Iron, _resourceManager.MaxIron);
            UpdateResourceDisplay(_stoneAmount, _resourceManager.Stone, _resourceManager.MaxStone);
            UpdateResourceDisplay(_arcaneAmount, _resourceManager.ArcaneEssence, _resourceManager.MaxArcaneEssence);
            SetText(_gemsAmount, Abbreviate(_gems));
        }

        private void OnResourceChanged(ResourceChangedEvent evt)
        {
            TextMeshProUGUI label;
            long max;
            switch (evt.Type)
            {
                case ResourceType.Stone: label = _stoneAmount; max = _resourceManager?.MaxStone ?? long.MaxValue; break;
                case ResourceType.Iron: label = _ironAmount; max = _resourceManager?.MaxIron ?? long.MaxValue; break;
                case ResourceType.Grain: label = _grainAmount; max = _resourceManager?.MaxGrain ?? long.MaxValue; break;
                case ResourceType.ArcaneEssence: label = _arcaneAmount; max = _resourceManager?.MaxArcaneEssence ?? long.MaxValue; break;
                default: return;
            }
            UpdateResourceDisplay(label, evt.NewValue, max);
        }

        private static void UpdateResourceDisplay(TextMeshProUGUI label, long current, long max)
        {
            if (label == null) return;
            label.text = Abbreviate(current);

            if (max <= 0) { label.color = NormalColor; return; }
            float ratio = (float)current / max;
            if (ratio >= 1f) label.color = FullColor;
            else if (ratio >= 0.95f) label.color = CriticalColor;
            else if (ratio >= 0.80f) label.color = WarningColor;
            else label.color = NormalColor;
        }

        /// <summary>P&C: Show resource detail popup with elastic pop-in animation.</summary>
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

            // P&C: Elastic pop-in animation on the frame
            if (_popupAnim != null) StopCoroutine(_popupAnim);
            _popupAnim = StartCoroutine(PopInDetailPopup());
        }

        private IEnumerator PopInDetailPopup()
        {
            var frame = _detailPopup.transform.Find("Frame");
            var target = frame != null ? frame : _detailPopup.transform;

            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = t < 0.55f
                    ? Mathf.Lerp(0.7f, 1.08f, t / 0.55f)
                    : Mathf.Lerp(1.08f, 1f, (t - 0.55f) / 0.45f);
                target.localScale = Vector3.one * scale;
                yield return null;
            }
            target.localScale = Vector3.one;
            _popupAnim = null;
        }

        public void HideDetail()
        {
            if (_detailPopup != null) _detailPopup.SetActive(false);
        }

        public static string Abbreviate(long value)
        {
            if (value < 0) return "-" + Abbreviate(-value);
            if (value < 1_000) return value.ToString();
            if (value < 10_000) return $"{value / 1_000f:F2}K";
            if (value < 1_000_000) return $"{value / 1_000f:F0}K";
            if (value < 1_000_000_000L) return $"{value / 1_000_000f:F2}M";
            return $"{value / 1_000_000_000f:F2}B";
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
