using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Combat;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Spawns floating damage/heal numbers above heroes when they take damage or are healed.
    /// Uses a simple GameObject reuse pool — popups are brief-lived, max ~6 visible at once.
    ///
    /// Popup lifetime: fade-in (0.1s) → float upward for 0.8s → fade-out (0.3s) → return to pool.
    /// Caller does NOT need to manage popup lifetime.
    ///
    /// WorldToScreen conversion requires a reference to the combat camera.
    /// Hero world positions are looked up via the grid's tile positions + hero InstanceId.
    /// </summary>
    public class DamagePopupManager : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject _popupPrefab;
        [SerializeField] private Canvas _worldCanvas;
        [SerializeField] private int _poolSize = 10;

        [Header("Float Animation")]
        [SerializeField] private float _floatDistance = 1.5f;
        [SerializeField] private float _lifetime = 1.2f;

        [Header("Colors")]
        [SerializeField] private Color _physicalDamageColor   = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _fireDamageColor       = new Color(1f, 0.35f, 0f);
        [SerializeField] private Color _arcaneDamageColor     = new Color(0.6f, 0.2f, 1f);
        [SerializeField] private Color _trueDamageColor       = new Color(1f, 1f, 1f);
        [SerializeField] private Color _healColor             = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color _critColor             = new Color(1f, 0.2f, 0.2f);

        private readonly List<DamagePopup> _pool = new(10);
        private readonly List<DamagePopup> _active = new(10);

        private CombatGrid _grid;
        private Camera _combatCamera;

        private EventSubscription _heroDamagedSub;
        private EventSubscription _heroHealedSub;

        private void Awake()
        {
            // CombatGrid lives on the same root GameObject as TurnManager; use ServiceLocator as fallback.
            _grid = GetComponentInParent<CombatGrid>() ?? ServiceLocator.Get<CombatGrid>();
            _combatCamera = Camera.main;

            if (_popupPrefab == null)
                Debug.LogError("[DamagePopupManager] Popup prefab not assigned.", this);
            if (_worldCanvas == null)
                Debug.LogError("[DamagePopupManager] World canvas not assigned.", this);

            PrewarmPool();
        }

        private void OnEnable()
        {
            _heroDamagedSub = EventBus.Subscribe<HeroDamagedEvent>(OnHeroDamaged);
            _heroHealedSub  = EventBus.Subscribe<HeroHealedEvent>(OnHeroHealed);
        }

        private void OnDisable()
        {
            _heroDamagedSub?.Dispose();
            _heroHealedSub?.Dispose();
            // Stop all in-flight animations and return popups to pool cleanly
            StopAllCoroutines();
            var snapshot = new List<DamagePopup>(_active);
            foreach (var p in snapshot) ReturnToPool(p);
        }

        private void OnHeroDamaged(HeroDamagedEvent evt)
        {
            if (evt.Damage <= 0) return;
            Vector3? worldPos = GetHeroWorldPosition(evt.HeroId);
            if (!worldPos.HasValue) return;

            Color color = evt.DamageType switch
            {
                DamageType.Fire    => _fireDamageColor,
                DamageType.Arcane  => _arcaneDamageColor,
                DamageType.True    => _trueDamageColor,
                _                  => _physicalDamageColor
            };

            SpawnPopup($"-{evt.Damage}", worldPos.Value, color, isCrit: false);
        }

        private void OnHeroHealed(HeroHealedEvent evt)
        {
            if (evt.Amount <= 0) return;
            Vector3? worldPos = GetHeroWorldPosition(evt.HeroId);
            if (!worldPos.HasValue) return;

            SpawnPopup($"+{evt.Amount}", worldPos.Value, _healColor, isCrit: false);
        }

        private void SpawnPopup(string text, Vector3 worldPosition, Color color, bool isCrit)
        {
            DamagePopup popup = GetFromPool();
            if (popup == null) return;

            popup.gameObject.SetActive(true);
            popup.Initialize(text, color, isCrit, worldPosition);
            _active.Add(popup);
            StartCoroutine(AnimateAndReturn(popup));
        }

        private IEnumerator AnimateAndReturn(DamagePopup popup)
        {
            float elapsed = 0f;
            Vector3 startPos = popup.transform.position;
            CanvasGroup cg = popup.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lifetime;

                // Float upward
                popup.transform.position = startPos + Vector3.up * (_floatDistance * t);

                // Fade in fast, fade out near end
                if (cg != null)
                    cg.alpha = t < 0.1f ? t / 0.1f : (t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f);

                yield return null;
            }

            ReturnToPool(popup);
        }

        private Vector3? GetHeroWorldPosition(int heroInstanceId)
        {
            if (_grid == null) return null;
            GridPosition? pos = _grid.GetUnitPosition(heroInstanceId);
            if (!pos.HasValue) return null;

            // Convert grid position to world position via the tile identifier
            // Tiles are tagged by column/row; use a simple offset formula as fallback
            return new Vector3(pos.Value.Column * 1.2f, 0f, pos.Value.Row * 1.2f);
        }

        private void PrewarmPool()
        {
            if (_popupPrefab == null) return;
            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_popupPrefab, _worldCanvas.transform);
                var popup = go.GetComponent<DamagePopup>();
                if (popup == null) popup = go.AddComponent<DamagePopup>();
                go.SetActive(false);
                _pool.Add(popup);
            }
        }

        private DamagePopup GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var p = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                return p;
            }
            // Pool exhausted — soft fail; no popup shown rather than allocating
            return null;
        }

        private void ReturnToPool(DamagePopup popup)
        {
            if (popup == null) return;
            _active.Remove(popup);
            popup.gameObject.SetActive(false);
            _pool.Add(popup);
        }
    }

    /// <summary>
    /// Component on each damage popup prefab. Holds the text label and sets initial values.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private float _critScaleMultiplier = 1.4f;

        private Vector3 _defaultScale;

        private void Awake()
        {
            _defaultScale = transform.localScale;
        }

        public void Initialize(string text, Color color, bool isCrit, Vector3 worldPosition)
        {
            if (_label != null)
            {
                _label.text = text;
                _label.color = color;
            }
            transform.position = worldPosition;
            transform.localScale = isCrit ? _defaultScale * _critScaleMultiplier : _defaultScale;
        }
    }
}
