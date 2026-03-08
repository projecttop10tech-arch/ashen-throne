using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Renders the player's card hand as a row of card widgets.
    /// Uses a simple reuse pool (Instantiate + SetActive) — max hand is 5 widgets,
    /// so full ObjectPool overhead is not warranted.
    /// Delegates card play requests to CombatInputHandler via CardSelectedByPlayerEvent.
    /// </summary>
    public class CardHandView : MonoBehaviour
    {
        [SerializeField] private Transform _cardContainer;
        [SerializeField] private GameObject _cardWidgetPrefab;
        [SerializeField] private CanvasGroup _canvasGroup;

        private readonly List<CardWidget> _activeWidgets = new(5);
        private readonly List<CardWidget> _pooledWidgets = new(5); // inactive reuse pool

        private EventSubscription _cardDrawnSub;
        private EventSubscription _cardPlayedSub;
        private EventSubscription _cardDiscardedSub;

        private bool _interactable = false;

        private void Awake()
        {
            if (_cardWidgetPrefab == null)
                Debug.LogError("[CardHandView] CardWidget prefab not assigned.", this);
        }

        private void OnEnable()
        {
            _cardDrawnSub    = EventBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            _cardPlayedSub   = EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
            _cardDiscardedSub = EventBus.Subscribe<CardDiscardedEvent>(OnCardDiscarded);
        }

        private void OnDisable()
        {
            _cardDrawnSub?.Dispose();
            _cardPlayedSub?.Dispose();
            _cardDiscardedSub?.Dispose();
        }

        /// <summary>
        /// Enable or disable card interaction (only allowed in Action phase).
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = interactable;
                _canvasGroup.alpha = interactable ? 1f : 0.6f;
            }
            foreach (var widget in _activeWidgets)
                widget.SetInteractable(interactable);
        }

        /// <summary>
        /// Clear and rebuild hand display from scratch (called after InitializeDeck).
        /// </summary>
        public void RefreshHand(IReadOnlyList<AbilityCardData> hand)
        {
            ClearWidgets();
            foreach (var card in hand)
                AddWidget(card);
        }

        private void OnCardDrawn(CardDrawnEvent evt)
        {
            // CardDrawnEvent only carries cardId — CardHandManager.Hand is the source of truth.
            // We rebuild the display via PhaseChanged → DrawForTurn flow; see CombatInputHandler.
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            RemoveWidget(evt.CardId);
        }

        private void OnCardDiscarded(CardDiscardedEvent evt)
        {
            RemoveWidget(evt.CardId);
        }

        private void AddWidget(AbilityCardData card)
        {
            CardWidget widget;
            if (_pooledWidgets.Count > 0)
            {
                widget = _pooledWidgets[_pooledWidgets.Count - 1];
                _pooledWidgets.RemoveAt(_pooledWidgets.Count - 1);
                widget.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(_cardWidgetPrefab, _cardContainer);
                widget = go.GetComponent<CardWidget>();
                if (widget == null)
                {
                    Debug.LogError("[CardHandView] CardWidget component missing on prefab", go);
                    Destroy(go);
                    return;
                }
            }
            widget.transform.SetParent(_cardContainer, false);
            widget.Bind(card, OnWidgetClicked);
            widget.SetInteractable(_interactable);
            _activeWidgets.Add(widget);
        }

        private void RemoveWidget(string cardId)
        {
            for (int i = _activeWidgets.Count - 1; i >= 0; i--)
            {
                if (_activeWidgets[i].CardId == cardId)
                {
                    _activeWidgets[i].gameObject.SetActive(false);
                    _pooledWidgets.Add(_activeWidgets[i]);
                    _activeWidgets.RemoveAt(i);
                    break;
                }
            }
        }

        private void ClearWidgets()
        {
            foreach (var w in _activeWidgets)
            {
                w.gameObject.SetActive(false);
                _pooledWidgets.Add(w);
            }
            _activeWidgets.Clear();
        }

        private void OnWidgetClicked(AbilityCardData card)
        {
            if (!_interactable) return;
            EventBus.Publish(new CardSelectedByPlayerEvent(card));
        }
    }

    /// <summary>Published when the player taps a card in their hand.</summary>
    public readonly struct CardSelectedByPlayerEvent
    {
        public readonly AbilityCardData Card;
        public CardSelectedByPlayerEvent(AbilityCardData card) { Card = card; }
    }
}
