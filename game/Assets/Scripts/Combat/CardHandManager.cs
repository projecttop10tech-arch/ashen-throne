using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Manages the hero's card hand during combat.
    /// Deck: 15-card loadout, shuffled at battle start.
    /// Draw 3 per turn (if space in hand). Max hand size = 5. Unplayed cards persist.
    /// Energy: 4 max per turn, +2 regenerated each turn. Cards cost 1–5 energy.
    /// </summary>
    public class CardHandManager : MonoBehaviour
    {
        public const int DeckSize = 15;
        public const int MaxHandSize = 5;
        public const int MaxEnergy = 4;
        public const int EnergyRegenPerTurn = 2;
        public const int DrawsPerTurn = 3;

        public int CurrentEnergy { get; private set; }
        public IReadOnlyList<AbilityCardData> Hand => _hand;
        public int CardsInDeck => _deck.Count;

        private readonly List<AbilityCardData> _hand = new(MaxHandSize);
        private Queue<AbilityCardData> _deck;
        private readonly List<AbilityCardData> _discardPile = new();
        private ComboTag _activeComboTag = ComboTag.None;
        private EventSubscription _phaseSubscription;

        public event Action<AbilityCardData> OnCardDrawn;
        public event Action<AbilityCardData> OnCardPlayed;
        public event Action<AbilityCardData> OnCardDiscarded;
        public event Action<int> OnEnergyChanged;
        public event Action<ComboTag> OnComboTagChanged;

        private void OnEnable()
        {
            _phaseSubscription = EventBus.Subscribe<CombatPhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            _phaseSubscription?.Dispose();
        }

        /// <summary>
        /// Initialize with a validated 15-card loadout deck. Shuffles the deck.
        /// </summary>
        public void InitializeDeck(List<AbilityCardData> loadout)
        {
            if (loadout == null || loadout.Count != DeckSize)
                throw new ArgumentException($"[CardHandManager] Loadout must contain exactly {DeckSize} cards.");

            _deck = new Queue<AbilityCardData>(loadout.OrderBy(_ => UnityEngine.Random.value));
            _hand.Clear();
            _discardPile.Clear();
            CurrentEnergy = MaxEnergy;
            _activeComboTag = ComboTag.None;
        }

        /// <summary>
        /// Draw up to DrawsPerTurn cards (limited by MaxHandSize - current hand count).
        /// Reshuffles discard pile into deck when deck is empty.
        /// </summary>
        public void DrawForTurn()
        {
            int drawCount = Mathf.Min(DrawsPerTurn, MaxHandSize - _hand.Count);
            for (int i = 0; i < drawCount; i++)
            {
                if (_deck.Count == 0)
                    ReshuffleDiscardIntoDeck();
                if (_deck.Count == 0) break; // Truly empty (shouldn't happen with 15-card deck)

                AbilityCardData drawn = _deck.Dequeue();
                _hand.Add(drawn);
                OnCardDrawn?.Invoke(drawn);
                EventBus.Publish(new CardDrawnEvent(drawn.cardId));
            }

            RegenEnergy();
        }

        /// <summary>
        /// Attempt to play a card from hand. Validates energy cost and combo requirements.
        /// Returns false if the card cannot be played.
        /// </summary>
        public bool TryPlayCard(AbilityCardData card, GridPosition target)
        {
            if (!_hand.Contains(card))
            {
                Debug.LogWarning($"[CardHandManager] Card {card.cardId} is not in hand.");
                return false;
            }
            if (CurrentEnergy < card.energyCost)
            {
                EventBus.Publish(new CardPlayFailedEvent(card.cardId, "Insufficient energy"));
                return false;
            }

            // Check combo bonus activation
            bool comboActivated = card.requiresComboTag != ComboTag.None &&
                                  card.requiresComboTag == _activeComboTag;

            // Deduct energy and remove from hand
            SetEnergy(CurrentEnergy - card.energyCost);
            _hand.Remove(card);
            _discardPile.Add(card);

            // Update active combo tag
            if (card.outputsComboTag != ComboTag.None)
                SetActiveComboTag(card.outputsComboTag);
            else if (comboActivated)
                SetActiveComboTag(ComboTag.None); // Combo consumed

            OnCardPlayed?.Invoke(card);
            EventBus.Publish(new CardPlayedEvent(card, target, comboActivated));
            return true;
        }

        /// <summary>
        /// Discard a card from hand (used by specific abilities or max hand enforcement).
        /// </summary>
        public void DiscardCard(AbilityCardData card)
        {
            if (!_hand.Remove(card)) return;
            _discardPile.Add(card);
            OnCardDiscarded?.Invoke(card);
            EventBus.Publish(new CardDiscardedEvent(card.cardId));
        }

        private void RegenEnergy()
        {
            SetEnergy(Mathf.Min(CurrentEnergy + EnergyRegenPerTurn, MaxEnergy));
        }

        private void SetEnergy(int value)
        {
            CurrentEnergy = Mathf.Clamp(value, 0, MaxEnergy);
            OnEnergyChanged?.Invoke(CurrentEnergy);
            EventBus.Publish(new EnergyChangedEvent(CurrentEnergy));
        }

        private void SetActiveComboTag(ComboTag tag)
        {
            _activeComboTag = tag;
            OnComboTagChanged?.Invoke(tag);
            EventBus.Publish(new ComboTagChangedEvent(tag));
        }

        private void ReshuffleDiscardIntoDeck()
        {
            List<AbilityCardData> shuffled = _discardPile.OrderBy(_ => UnityEngine.Random.value).ToList();
            _discardPile.Clear();
            _deck = new Queue<AbilityCardData>(shuffled);
        }

        private void OnPhaseChanged(CombatPhaseChangedEvent evt)
        {
            if (evt.Phase == CombatPhase.Draw)
                DrawForTurn();
        }
    }

    // --- Events ---
    public readonly struct CardDrawnEvent { public readonly string CardId; public CardDrawnEvent(string id) { CardId = id; } }
    public readonly struct CardPlayedEvent
    {
        public readonly AbilityCardData Card;
        public readonly string CardId;     // Convenience alias for Card.cardId
        public readonly GridPosition Target;
        public readonly bool ComboActivated;
        public CardPlayedEvent(AbilityCardData card, GridPosition t, bool c)
        {
            Card = card;
            CardId = card?.cardId ?? string.Empty;
            Target = t;
            ComboActivated = c;
        }
    }
    public readonly struct CardDiscardedEvent { public readonly string CardId; public CardDiscardedEvent(string id) { CardId = id; } }
    public readonly struct CardPlayFailedEvent { public readonly string CardId; public readonly string Reason; public CardPlayFailedEvent(string id, string r) { CardId = id; Reason = r; } }
    public readonly struct EnergyChangedEvent { public readonly int CurrentEnergy; public EnergyChangedEvent(int e) { CurrentEnergy = e; } }
    public readonly struct ComboTagChangedEvent { public readonly ComboTag Tag; public ComboTagChangedEvent(ComboTag t) { Tag = t; } }
}
