using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Handles player input during the Action phase:
    ///   1. Player taps a card in CardHandView → card enters 'pending play' state.
    ///   2. Player taps a valid target on the grid → card play is submitted to CardHandManager.
    ///   3. Player taps the End Turn button → calls TurnManager.EndActionPhase().
    ///
    /// Target selection works by mapping screen-space touch/click to grid position via CombatGrid.
    /// Does NOT modify game state directly — all state mutations go through CardHandManager.TryPlayCard.
    /// </summary>
    public class CombatInputHandler : MonoBehaviour
    {
        [SerializeField] private Camera _combatCamera;

        private TurnManager _turnManager;
        private CardHandManager _cardHandManager;
        private CombatGrid _combatGrid;

        private AbilityCardData _pendingCard;

        private EventSubscription _cardSelectedSub;
        private EventSubscription _phaseChangedSub;

        private void Awake()
        {
            _turnManager = GetComponent<TurnManager>();
            _cardHandManager = GetComponent<CardHandManager>();
            _combatGrid = GetComponent<CombatGrid>();

            if (_combatCamera == null)
                _combatCamera = Camera.main;

            if (_turnManager == null)
                Debug.LogError("[CombatInputHandler] TurnManager not found on same GameObject.", this);
            if (_cardHandManager == null)
                Debug.LogError("[CombatInputHandler] CardHandManager not found on same GameObject.", this);
            if (_combatGrid == null)
                Debug.LogError("[CombatInputHandler] CombatGrid not found on same GameObject.", this);
        }

        private void OnEnable()
        {
            _cardSelectedSub = EventBus.Subscribe<CardSelectedByPlayerEvent>(OnCardSelected);
            _phaseChangedSub = EventBus.Subscribe<CombatPhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            _cardSelectedSub?.Dispose();
            _phaseChangedSub?.Dispose();
            CancelPendingCard();
        }

        private void Update()
        {
            if (_pendingCard == null) return;
            if (_turnManager == null || _turnManager.CurrentPhase != CombatPhase.Action) return;

            // Poll for a target tap/click when a card is pending
            if (Input.GetMouseButtonDown(0))
                HandleTargetInput(Input.mousePosition);
        }

        /// <summary>
        /// Called by the End Turn UI button.
        /// </summary>
        public void OnEndTurnButtonPressed()
        {
            if (_turnManager == null) return;
            CancelPendingCard();
            _turnManager.EndActionPhase();
        }

        private void OnCardSelected(CardSelectedByPlayerEvent evt)
        {
            _pendingCard = evt.Card;
            EventBus.Publish(new CardPendingPlayEvent(evt.Card));
        }

        private void OnPhaseChanged(CombatPhaseChangedEvent evt)
        {
            // Cancel pending selection on any phase transition
            if (evt.Phase != CombatPhase.Action)
                CancelPendingCard();
        }

        private void HandleTargetInput(Vector3 screenPosition)
        {
            if (_combatCamera == null || _combatGrid == null || _pendingCard == null) return;

            GridPosition? gridPos = ScreenToGridPosition(screenPosition);
            if (!gridPos.HasValue)
            {
                // Tapped outside the grid — cancel pending card
                CancelPendingCard();
                return;
            }

            bool played = _cardHandManager.TryPlayCard(_pendingCard, gridPos.Value);
            if (played)
                _pendingCard = null;
            // If play failed (invalid target, not enough energy), keep pending so player can re-tap
        }

        private GridPosition? ScreenToGridPosition(Vector3 screenPos)
        {
            Ray ray = _combatCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return null;

            // Tiles are tagged "CombatTile" and carry a GridTileIdentifier component
            GridTileIdentifier tile = hit.collider.GetComponent<GridTileIdentifier>();
            if (tile == null) return null;

            return tile.GridPosition;
        }

        private void CancelPendingCard()
        {
            if (_pendingCard != null)
            {
                EventBus.Publish(new CardPendingCancelledEvent(_pendingCard));
                _pendingCard = null;
            }
        }
    }

    /// <summary>Published when a card enters pending-play state (waiting for target selection).</summary>
    public readonly struct CardPendingPlayEvent
    {
        public readonly AbilityCardData Card;
        public CardPendingPlayEvent(AbilityCardData card) { Card = card; }
    }

    /// <summary>Published when pending card selection is cancelled without playing.</summary>
    public readonly struct CardPendingCancelledEvent
    {
        public readonly AbilityCardData Card;
        public CardPendingCancelledEvent(AbilityCardData card) { Card = card; }
    }
}
