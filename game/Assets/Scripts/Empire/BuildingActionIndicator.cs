using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Empire
{
    /// <summary>
    /// P&C-quality animated overlay indicator for building actions: upgrade available,
    /// construction timer, resource collection ready. Pulses, glows, and bounces
    /// to draw player attention like Puzzles &amp; Chaos building badges.
    /// </summary>
    public class BuildingActionIndicator : MonoBehaviour
    {
        [SerializeField] private GameObject upgradeIcon;
        [SerializeField] private GameObject timerGroup;
        [SerializeField] private Text timerText;
        [SerializeField] private GameObject collectIcon;

        // Animation state
        private enum IndicatorState { None, Upgrade, Timer, Collect }
        private IndicatorState _state = IndicatorState.None;
        private float _phase;
        private float _spawnTimer;
        private bool _spawning;

        // Cached transforms & images for animation
        private RectTransform _upgradeRect;
        private Image _upgradeGlow;
        private RectTransform _collectRect;
        private Image _collectGlow;
        private Image _timerBg;

        // Timer color warning
        private float _totalBuildTime;
        private float _remainingTime;

        // P&C animation constants
        private const float PulseSpeed = 2.8f;
        private const float BounceSpeed = 3.5f;
        private const float GlowPulseMin = 0.15f;
        private const float GlowPulseMax = 0.50f;
        private const float ScalePulseMin = 0.92f;
        private const float ScalePulseMax = 1.12f;
        private const float SpawnDuration = 0.35f;
        private const float CollectBounceAmplitude = 4f;

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            if (upgradeIcon != null)
            {
                _upgradeRect = upgradeIcon.GetComponent<RectTransform>();
                var glow = upgradeIcon.transform.Find("Glow");
                if (glow != null) _upgradeGlow = glow.GetComponent<Image>();
            }
            if (collectIcon != null)
            {
                _collectRect = collectIcon.GetComponent<RectTransform>();
                var glow = collectIcon.transform.Find("Glow");
                if (glow != null) _collectGlow = glow.GetComponent<Image>();
            }
            if (timerGroup != null)
            {
                _timerBg = timerGroup.GetComponent<Image>();
            }
        }

        private void Update()
        {
            if (_state == IndicatorState.None) return;

            _phase += Time.deltaTime;

            // Spawn pop-in animation
            if (_spawning)
            {
                _spawnTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_spawnTimer / SpawnDuration);
                // Elastic ease-out: overshoot then settle
                float scale = t < 0.6f
                    ? Mathf.Lerp(0f, 1.25f, t / 0.6f)
                    : Mathf.Lerp(1.25f, 1f, (t - 0.6f) / 0.4f);

                switch (_state)
                {
                    case IndicatorState.Upgrade when _upgradeRect != null:
                        _upgradeRect.localScale = Vector3.one * scale;
                        break;
                    case IndicatorState.Collect when _collectRect != null:
                        _collectRect.localScale = Vector3.one * scale;
                        break;
                    case IndicatorState.Timer:
                        if (timerGroup != null)
                            timerGroup.transform.localScale = Vector3.one * scale;
                        break;
                }

                if (t >= 1f) _spawning = false;
                return;
            }

            switch (_state)
            {
                case IndicatorState.Upgrade:
                    AnimateUpgrade();
                    break;
                case IndicatorState.Timer:
                    AnimateTimer();
                    break;
                case IndicatorState.Collect:
                    AnimateCollect();
                    break;
            }
        }

        /// <summary>P&C: Upgrade icon pulses scale + glow breathes in/out</summary>
        private void AnimateUpgrade()
        {
            float sin = Mathf.Sin(_phase * PulseSpeed);
            float norm = sin * 0.5f + 0.5f; // 0..1

            // Scale pulse
            if (_upgradeRect != null)
            {
                float s = Mathf.Lerp(ScalePulseMin, ScalePulseMax, norm);
                _upgradeRect.localScale = Vector3.one * s;
            }

            // Glow alpha breathe
            if (_upgradeGlow != null)
            {
                var c = _upgradeGlow.color;
                c.a = Mathf.Lerp(GlowPulseMin, GlowPulseMax, norm);
                _upgradeGlow.color = c;
            }
        }

        /// <summary>P&C: Timer pill color shifts as time runs out + gentle pulse</summary>
        private void AnimateTimer()
        {
            if (_timerBg == null) return;

            // Color warning based on remaining time ratio
            float ratio = _totalBuildTime > 0 ? _remainingTime / _totalBuildTime : 1f;

            Color bgColor;
            Color textColor;
            if (ratio > 0.5f)
            {
                // Normal: dark pill, white text
                bgColor = new Color(0.06f, 0.04f, 0.12f, 0.90f);
                textColor = Color.white;
            }
            else if (ratio > 0.15f)
            {
                // Warning: warm amber tint
                float t = 1f - (ratio - 0.15f) / 0.35f;
                bgColor = Color.Lerp(
                    new Color(0.06f, 0.04f, 0.12f, 0.90f),
                    new Color(0.35f, 0.22f, 0.05f, 0.92f),
                    t);
                textColor = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.40f), t);
            }
            else
            {
                // Almost done: bright gold pulse
                float pulse = Mathf.Sin(_phase * 5f) * 0.5f + 0.5f;
                bgColor = Color.Lerp(
                    new Color(0.35f, 0.22f, 0.05f, 0.92f),
                    new Color(0.50f, 0.35f, 0.08f, 0.95f),
                    pulse);
                textColor = Color.Lerp(
                    new Color(1f, 0.85f, 0.40f),
                    new Color(1f, 0.95f, 0.65f),
                    pulse);
            }

            _timerBg.color = bgColor;
            if (timerText != null)
                timerText.color = textColor;

            // Subtle scale breathe on the timer pill
            if (timerGroup != null)
            {
                float breath = 1f + 0.02f * Mathf.Sin(_phase * 2f);
                timerGroup.transform.localScale = Vector3.one * breath;
            }
        }

        /// <summary>P&C: Collect icon bounces vertically + glow pulses stronger than upgrade</summary>
        private void AnimateCollect()
        {
            float sin = Mathf.Sin(_phase * BounceSpeed);
            float norm = sin * 0.5f + 0.5f;

            if (_collectRect != null)
            {
                // Vertical bounce (bob up and down to say "tap me!")
                float bobY = Mathf.Sin(_phase * BounceSpeed) * CollectBounceAmplitude;
                var pos = _collectRect.anchoredPosition;
                // Only adjust Y relative to base (anchors handle positioning)
                _collectRect.anchoredPosition = new Vector2(pos.x, bobY);

                // Scale wiggle — quicker, more playful
                float s = Mathf.Lerp(0.90f, 1.15f, norm);
                _collectRect.localScale = Vector3.one * s;
            }

            // Glow breathe — stronger and faster than upgrade
            if (_collectGlow != null)
            {
                var c = _collectGlow.color;
                c.a = Mathf.Lerp(0.20f, 0.65f, norm);
                _collectGlow.color = c;
            }
        }

        // === Public API ===

        public void ShowUpgrade()
        {
            HideAll();
            _state = IndicatorState.Upgrade;
            if (upgradeIcon != null)
            {
                upgradeIcon.SetActive(true);
                StartSpawnAnimation();
            }
        }

        public void ShowTimer(string timeDisplay)
        {
            bool wasTimer = _state == IndicatorState.Timer;
            if (!wasTimer) HideAll();

            _state = IndicatorState.Timer;
            if (timerGroup != null)
            {
                timerGroup.SetActive(true);
                if (!wasTimer) StartSpawnAnimation();
            }
            if (timerText != null) timerText.text = timeDisplay;
        }

        /// <summary>
        /// Show timer with time-awareness for color warning animation.
        /// </summary>
        public void ShowTimer(string timeDisplay, float remainingSec, float totalSec)
        {
            _remainingTime = remainingSec;
            _totalBuildTime = totalSec;
            ShowTimer(timeDisplay);
        }

        public void ShowCollect()
        {
            HideAll();
            _state = IndicatorState.Collect;
            if (collectIcon != null)
            {
                collectIcon.SetActive(true);
                StartSpawnAnimation();
            }
        }

        public void HideAll()
        {
            _state = IndicatorState.None;
            _spawning = false;
            _phase = 0f;

            if (upgradeIcon != null)
            {
                upgradeIcon.SetActive(false);
                if (_upgradeRect != null) _upgradeRect.localScale = Vector3.one;
            }
            if (timerGroup != null)
            {
                timerGroup.SetActive(false);
                timerGroup.transform.localScale = Vector3.one;
            }
            if (collectIcon != null)
            {
                collectIcon.SetActive(false);
                if (_collectRect != null)
                {
                    _collectRect.localScale = Vector3.one;
                    _collectRect.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void StartSpawnAnimation()
        {
            _spawning = true;
            _spawnTimer = 0f;
            _phase = Random.Range(0f, Mathf.PI * 2f); // Random phase offset
        }
    }
}
