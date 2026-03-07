using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Displays the upcoming turn order as a horizontal strip of hero portrait tokens.
    /// Shows the next N heroes (default 6) in initiative order.
    /// Subscribes to CombatPhaseChangedEvent to refresh on each new turn.
    /// </summary>
    public class TurnOrderDisplay : MonoBehaviour
    {
        [SerializeField] private Transform _tokenContainer;
        [SerializeField] private GameObject _turnTokenPrefab;
        [SerializeField] private int _visibleSlots = 6;

        private readonly List<TurnTokenWidget> _tokens = new(6);
        private readonly List<TurnTokenWidget> _pooledTokens = new(6); // inactive reuse pool

        private EventSubscription _phaseChangedSub;
        private EventSubscription _heroDiedSub;

        private void Awake()
        {
            if (_turnTokenPrefab == null)
                Debug.LogError("[TurnOrderDisplay] TurnTokenPrefab not assigned.", this);
        }

        private void OnEnable()
        {
            _phaseChangedSub = EventBus.Subscribe<CombatPhaseChangedEvent>(OnPhaseChanged);
            _heroDiedSub     = EventBus.Subscribe<HeroDiedEvent>(OnHeroDied);
        }

        private void OnDisable()
        {
            _phaseChangedSub?.Dispose();
            _heroDiedSub?.Dispose();
        }

        /// <summary>
        /// Rebuild the token strip from the provided ordered hero list.
        /// Called by CombatUIController after battle starts.
        /// </summary>
        public void SetTurnOrder(IReadOnlyList<CombatHero> heroes)
        {
            ClearTokens();
            int count = Mathf.Min(_visibleSlots, heroes.Count);
            for (int i = 0; i < count; i++)
            {
                TurnTokenWidget token;
                if (_pooledTokens.Count > 0)
                {
                    token = _pooledTokens[_pooledTokens.Count - 1];
                    _pooledTokens.RemoveAt(_pooledTokens.Count - 1);
                    token.gameObject.SetActive(true);
                }
                else
                {
                    var go = Instantiate(_turnTokenPrefab, _tokenContainer);
                    token = go.GetComponent<TurnTokenWidget>();
                }
                token.transform.SetParent(_tokenContainer, false);
                token.Bind(heroes[i], i == 0);
                _tokens.Add(token);
            }
        }

        private void OnPhaseChanged(CombatPhaseChangedEvent evt)
        {
            // Highlight the first token as active at the start of each Draw phase
            if (evt.Phase == CombatPhase.Draw && _tokens.Count > 0)
                _tokens[0].SetActiveTurn(true);
        }

        private void OnHeroDied(HeroDiedEvent evt)
        {
            // Dim token for any hero who died
            foreach (var token in _tokens)
            {
                if (token.BoundHeroInstanceId == evt.HeroId)
                {
                    token.SetDead();
                    break;
                }
            }
        }

        private void ClearTokens()
        {
            foreach (var t in _tokens)
            {
                t.gameObject.SetActive(false);
                _pooledTokens.Add(t);
            }
            _tokens.Clear();
        }
    }
}
