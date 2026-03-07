using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Combat;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Displays a single hero's health bar, name, status effect icons, and shield indicator.
    /// Bound to a CombatHero at encounter start. Updates via CombatUIController event routing.
    /// One component per slot (player0, player1, player2, enemy0, enemy1, enemy2).
    /// </summary>
    public class HeroStatusDisplay : MonoBehaviour
    {
        [Header("Hero Info")]
        [SerializeField] private TextMeshProUGUI _heroNameLabel;
        [SerializeField] private Image _heroPortrait;

        [Header("Health Bar")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private TextMeshProUGUI _hpLabel;

        [Header("Status Effects")]
        [SerializeField] private Transform _statusIconContainer;
        [SerializeField] private GameObject _statusIconPrefab;

        [Header("Active Turn Indicator")]
        [SerializeField] private GameObject _activeTurnIndicator;

        [Header("Dead State")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _deadOverlay;

        private CombatHero _hero;
        private readonly List<StatusIconWidget> _statusIcons = new(6);

        public int BoundHeroInstanceId => _hero?.InstanceId ?? -1;

        /// <summary>
        /// Bind to a CombatHero. Initializes all display elements immediately.
        /// </summary>
        public void Bind(CombatHero hero)
        {
            _hero = hero ?? throw new System.ArgumentNullException(nameof(hero));

            gameObject.SetActive(true);
            if (_deadOverlay != null) _deadOverlay.SetActive(false);
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            if (_activeTurnIndicator != null) _activeTurnIndicator.SetActive(false);

            if (_heroNameLabel != null)
                _heroNameLabel.text = hero.Data?.heroName ?? hero.Data?.heroId ?? "Hero";

            RefreshHealth();
            RefreshStatusEffects();
        }

        /// <summary>Update health bar and HP text from bound hero's current values.</summary>
        public void RefreshHealth()
        {
            if (_hero == null) return;

            float ratio = _hero.MaxHealth > 0
                ? (float)_hero.CurrentHealth / _hero.MaxHealth
                : 0f;

            if (_healthBar != null) _healthBar.value = ratio;
            if (_hpLabel != null) _hpLabel.text = $"{_hero.CurrentHealth}/{_hero.MaxHealth}";
        }

        /// <summary>Rebuild status effect icon row from bound hero's current effects.</summary>
        public void RefreshStatusEffects()
        {
            if (_hero == null || _statusIconContainer == null) return;

            // Clear existing icons
            foreach (var icon in _statusIcons)
                Destroy(icon.gameObject);
            _statusIcons.Clear();

            // Rebuild from live status list
            foreach (var effect in _hero.StatusEffects)
            {
                if (_statusIconPrefab == null) break;
                var go = Instantiate(_statusIconPrefab, _statusIconContainer);
                var widget = go.GetComponent<StatusIconWidget>();
                widget?.Bind(effect.Type, effect.RemainingTurns);
                _statusIcons.Add(widget);
            }
        }

        /// <summary>Mark hero as dead — fade out and show dead overlay.</summary>
        public void SetDead()
        {
            if (_deadOverlay != null) _deadOverlay.SetActive(true);
            if (_canvasGroup != null) _canvasGroup.alpha = 0.4f;
            if (_healthBar != null) _healthBar.value = 0f;
            if (_hpLabel != null) _hpLabel.text = "0/" + (_hero?.MaxHealth.ToString() ?? "?");
        }

        /// <summary>Show or hide the active-turn highlight ring.</summary>
        public void SetActiveTurn(bool isActive)
        {
            if (_activeTurnIndicator != null)
                _activeTurnIndicator.SetActive(isActive);
        }
    }
}
