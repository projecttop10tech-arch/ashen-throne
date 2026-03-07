// Unity Test Framework (NUnit) tests for CardHandManager.
// Tests use AddComponent on a temp GameObject — no full scene required.
// Location: Assets/Scripts/Tests/Combat/ — Unity discovers automatically.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AshenThrone.Combat;
using AshenThrone.Data;
using AshenThrone.Core;

namespace AshenThrone.Tests.Combat
{
    /// <summary>
    /// Unit tests for CardHandManager covering deck initialization, drawing, card play,
    /// discard, energy management, and combo tag transitions.
    /// </summary>
    [TestFixture]
    public class CardHandManagerTests
    {
        private GameObject _go;
        private CardHandManager _hand;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CardHandManagerTest");
            _hand = _go.AddComponent<CardHandManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Create a minimal valid 15-card loadout.</summary>
        private static List<AbilityCardData> MakeDeck(int size = 15,
            int energyCost = 1,
            ComboTag outputTag = ComboTag.None,
            ComboTag requireTag = ComboTag.None)
        {
            var deck = new List<AbilityCardData>(size);
            for (int i = 0; i < size; i++)
            {
                var card = ScriptableObject.CreateInstance<AbilityCardData>();
                card.cardId = $"test_card_{i}_{System.Guid.NewGuid():N}";
                card.cardType = CardType.Attack;
                card.targetType = CardTargetType.SingleEnemy;
                card.energyCost = energyCost;
                card.baseEffectValue = 10f;
                card.statMultiplier = 0f;
                card.outputsComboTag = outputTag;
                card.requiresComboTag = requireTag;
                deck.Add(card);
            }
            return deck;
        }

        private static AbilityCardData MakeCard(string id,
            int energyCost = 1,
            ComboTag outputTag = ComboTag.None,
            ComboTag requireTag = ComboTag.None)
        {
            var card = ScriptableObject.CreateInstance<AbilityCardData>();
            card.cardId = id;
            card.cardType = CardType.Attack;
            card.targetType = CardTargetType.SingleEnemy;
            card.energyCost = energyCost;
            card.outputsComboTag = outputTag;
            card.requiresComboTag = requireTag;
            return card;
        }

        // ─── InitializeDeck ───────────────────────────────────────────────────────

        [Test]
        public void InitializeDeck_ThrowsArgumentException_WhenLoadoutIsNull()
        {
            Assert.Throws<System.ArgumentException>(() => _hand.InitializeDeck(null));
        }

        [Test]
        public void InitializeDeck_ThrowsArgumentException_WhenLoadoutTooSmall()
        {
            var short14 = MakeDeck(14);
            Assert.Throws<System.ArgumentException>(() => _hand.InitializeDeck(short14));
        }

        [Test]
        public void InitializeDeck_ThrowsArgumentException_WhenLoadoutTooLarge()
        {
            var big16 = MakeDeck(16);
            Assert.Throws<System.ArgumentException>(() => _hand.InitializeDeck(big16));
        }

        [Test]
        public void InitializeDeck_SetsEnergyToMax()
        {
            _hand.InitializeDeck(MakeDeck());
            Assert.AreEqual(CardHandManager.MaxEnergy, _hand.CurrentEnergy);
        }

        [Test]
        public void InitializeDeck_HandIsEmpty_AfterInit()
        {
            _hand.InitializeDeck(MakeDeck());
            Assert.AreEqual(0, _hand.Hand.Count);
        }

        [Test]
        public void InitializeDeck_DeckContainsAllCards_AfterInit()
        {
            _hand.InitializeDeck(MakeDeck());
            Assert.AreEqual(CardHandManager.DeckSize, _hand.CardsInDeck);
        }

        [Test]
        public void InitializeDeck_ResetsStateFromPreviousGame()
        {
            // First game: draw some cards and spend energy
            _hand.InitializeDeck(MakeDeck());
            _hand.DrawForTurn();
            // Second game
            _hand.InitializeDeck(MakeDeck());
            Assert.AreEqual(0, _hand.Hand.Count);
            Assert.AreEqual(CardHandManager.DeckSize, _hand.CardsInDeck);
            Assert.AreEqual(CardHandManager.MaxEnergy, _hand.CurrentEnergy);
        }

        // ─── DrawForTurn ──────────────────────────────────────────────────────────

        [Test]
        public void DrawForTurn_DrawsExactlyDrawsPerTurn_WhenHandEmpty()
        {
            _hand.InitializeDeck(MakeDeck());
            _hand.DrawForTurn();
            Assert.AreEqual(CardHandManager.DrawsPerTurn, _hand.Hand.Count);
        }

        [Test]
        public void DrawForTurn_DoesNotExceedMaxHandSize()
        {
            _hand.InitializeDeck(MakeDeck());
            // Fill hand to max - 1 so one more draw fits
            _hand.DrawForTurn(); // 3 cards
            _hand.DrawForTurn(); // +2 more = 5 (MaxHandSize=5, DrawsPerTurn=3 but capped)
            Assert.LessOrEqual(_hand.Hand.Count, CardHandManager.MaxHandSize);
        }

        [Test]
        public void DrawForTurn_RegeneratesEnergy()
        {
            _hand.InitializeDeck(MakeDeck(energyCost: 2));
            // Spend 2 energy on a card play first to reduce energy below max
            _hand.DrawForTurn();
            var card = _hand.Hand[0];
            _hand.TryPlayCard(card, new GridPosition(0, 0));
            int energyAfterPlay = _hand.CurrentEnergy;

            // Draw again — should regen EnergyRegenPerTurn
            _hand.DrawForTurn();
            int expected = Mathf.Min(energyAfterPlay + CardHandManager.EnergyRegenPerTurn, CardHandManager.MaxEnergy);
            Assert.AreEqual(expected, _hand.CurrentEnergy);
        }

        [Test]
        public void DrawForTurn_DecrementsDeckCount()
        {
            _hand.InitializeDeck(MakeDeck());
            int before = _hand.CardsInDeck;
            _hand.DrawForTurn();
            Assert.AreEqual(before - CardHandManager.DrawsPerTurn, _hand.CardsInDeck);
        }

        [Test]
        public void DrawForTurn_FiresOnCardDrawnEvent_ForEachCard()
        {
            _hand.InitializeDeck(MakeDeck());
            int drawEventCount = 0;
            _hand.OnCardDrawn += _ => drawEventCount++;
            _hand.DrawForTurn();
            Assert.AreEqual(CardHandManager.DrawsPerTurn, drawEventCount);
        }

        [Test]
        public void DrawForTurn_ReshufflesDiscardWhenDeckExhausted()
        {
            // Use a 15-card deck, draw everything, ensure reshuffle lets us draw again
            _hand.InitializeDeck(MakeDeck());
            // Draw and discard until deck is empty (5 rounds of 3 = 15)
            for (int i = 0; i < 5; i++)
            {
                _hand.DrawForTurn();
                // Discard entire hand to keep space for next draw
                var hand = new List<AbilityCardData>(_hand.Hand);
                foreach (var c in hand) _hand.DiscardCard(c);
            }
            // Deck should be empty now; drawing again should reshuffle discard
            Assert.AreEqual(0, _hand.CardsInDeck);
            Assert.DoesNotThrow(() => _hand.DrawForTurn());
            Assert.Greater(_hand.Hand.Count, 0, "Should draw from reshuffled discard pile");
        }

        // ─── TryPlayCard ──────────────────────────────────────────────────────────

        [Test]
        public void TryPlayCard_ReturnsFalse_WhenCardNotInHand()
        {
            _hand.InitializeDeck(MakeDeck());
            var outsider = MakeCard("not_in_hand");
            Assert.IsFalse(_hand.TryPlayCard(outsider, new GridPosition(0, 0)));
        }

        [Test]
        public void TryPlayCard_ReturnsFalse_WhenInsufficientEnergy()
        {
            // Cards cost 3 energy each; max energy = 4, so playing two consecutively should fail
            _hand.InitializeDeck(MakeDeck(energyCost: 3));
            _hand.DrawForTurn();

            var first = _hand.Hand[0];
            _hand.TryPlayCard(first, new GridPosition(0, 0)); // spends 3, leaves 1
            var second = _hand.Hand[0];
            bool result = _hand.TryPlayCard(second, new GridPosition(0, 0)); // needs 3, only 1 left
            Assert.IsFalse(result);
        }

        [Test]
        public void TryPlayCard_ReturnsTrue_AndRemovesCardFromHand()
        {
            _hand.InitializeDeck(MakeDeck(energyCost: 1));
            _hand.DrawForTurn();
            int handSizeBefore = _hand.Hand.Count;
            var card = _hand.Hand[0];
            bool played = _hand.TryPlayCard(card, new GridPosition(0, 0));
            Assert.IsTrue(played);
            Assert.AreEqual(handSizeBefore - 1, _hand.Hand.Count);
        }

        [Test]
        public void TryPlayCard_DecrementsEnergy_ByCardCost()
        {
            _hand.InitializeDeck(MakeDeck(energyCost: 2));
            _hand.DrawForTurn();
            int energyBefore = _hand.CurrentEnergy;
            _hand.TryPlayCard(_hand.Hand[0], new GridPosition(0, 0));
            Assert.AreEqual(energyBefore - 2, _hand.CurrentEnergy);
        }

        [Test]
        public void TryPlayCard_FiresCardPlayedEvent()
        {
            _hand.InitializeDeck(MakeDeck());
            _hand.DrawForTurn();
            bool fired = false;
            _hand.OnCardPlayed += _ => fired = true;
            _hand.TryPlayCard(_hand.Hand[0], new GridPosition(0, 0));
            Assert.IsTrue(fired);
        }

        // ─── Combo Tag Logic ──────────────────────────────────────────────────────

        [Test]
        public void TryPlayCard_SetsOutputComboTag_WhenCardOutputsTag()
        {
            // Deck with first card outputting Ignite combo tag
            var deck = MakeDeck(energyCost: 1);
            // Replace first card with one that outputs a combo tag
            Object.DestroyImmediate(deck[0]);
            deck[0] = MakeCard("ignite_card", energyCost: 1, outputTag: ComboTag.Ignite);

            _hand.InitializeDeck(deck);
            _hand.DrawForTurn();

            // Find and play the ignite card
            AbilityCardData igniteCard = null;
            foreach (var c in _hand.Hand)
            {
                if (c.cardId == "ignite_card") { igniteCard = c; break; }
            }

            if (igniteCard == null)
            {
                // Not drawn this turn — skip test (random shuffle)
                Assert.Ignore("Ignite card was not drawn this turn (random shuffle).");
                return;
            }

            ComboTag receivedTag = ComboTag.None;
            _hand.OnComboTagChanged += t => receivedTag = t;
            _hand.TryPlayCard(igniteCard, new GridPosition(0, 0));
            Assert.AreEqual(ComboTag.Ignite, receivedTag);
        }

        [Test]
        public void TryPlayCard_SetsComboActivated_WhenRequiredTagMatches()
        {
            // Use a targeted deck: first card outputs Ignite, second requires Ignite
            var deck = new List<AbilityCardData>();
            for (int i = 0; i < 13; i++) deck.Add(MakeCard($"plain_{i}"));
            var setupCard = MakeCard("setup", energyCost: 1, outputTag: ComboTag.Ignite);
            var payoffCard = MakeCard("payoff", energyCost: 1, requireTag: ComboTag.Ignite);
            deck.Add(setupCard);
            deck.Add(payoffCard);

            _hand.InitializeDeck(deck);

            // Force-draw by directly observing the CardPlayedEvent's ComboActivated flag
            bool comboActivatedReceived = false;
            var sub = EventBus.Subscribe<CardPlayedEvent>(e =>
            {
                if (e.Card == payoffCard) comboActivatedReceived = e.ComboActivated;
            });

            // Play setup card to set Ignite combo tag
            // We need both cards in hand — draw until we have them
            int attempts = 0;
            bool setupInHand = false, payoffInHand = false;
            while (!(setupInHand && payoffInHand) && attempts < 20)
            {
                _hand.DrawForTurn();
                setupInHand = _hand.Hand.Contains(setupCard);
                payoffInHand = _hand.Hand.Contains(payoffCard);
                if (!setupInHand || !payoffInHand)
                {
                    var copy = new List<AbilityCardData>(_hand.Hand);
                    foreach (var c in copy) _hand.DiscardCard(c);
                }
                attempts++;
            }

            if (!setupInHand || !payoffInHand)
            {
                sub.Dispose();
                Assert.Ignore("Could not draw both combo cards in 20 attempts (random shuffle).");
                return;
            }

            _hand.TryPlayCard(setupCard, new GridPosition(0, 0));
            _hand.TryPlayCard(payoffCard, new GridPosition(0, 0));

            sub.Dispose();
            Assert.IsTrue(comboActivatedReceived, "Combo should activate when requiresComboTag matches activeComboTag");
        }

        // ─── DiscardCard ──────────────────────────────────────────────────────────

        [Test]
        public void DiscardCard_RemovesCardFromHand()
        {
            _hand.InitializeDeck(MakeDeck());
            _hand.DrawForTurn();
            int before = _hand.Hand.Count;
            var card = _hand.Hand[0];
            _hand.DiscardCard(card);
            Assert.AreEqual(before - 1, _hand.Hand.Count);
            Assert.IsFalse(_hand.Hand.Contains(card));
        }

        [Test]
        public void DiscardCard_DoesNotThrow_WhenCardNotInHand()
        {
            _hand.InitializeDeck(MakeDeck());
            var outsider = MakeCard("not_in_hand");
            Assert.DoesNotThrow(() => _hand.DiscardCard(outsider));
        }

        [Test]
        public void DiscardCard_FiresOnCardDiscardedEvent()
        {
            _hand.InitializeDeck(MakeDeck());
            _hand.DrawForTurn();
            bool fired = false;
            _hand.OnCardDiscarded += _ => fired = true;
            _hand.DiscardCard(_hand.Hand[0]);
            Assert.IsTrue(fired);
        }

        // ─── Energy Clamp ─────────────────────────────────────────────────────────

        [Test]
        public void Energy_NeverExceedsMaxEnergy()
        {
            _hand.InitializeDeck(MakeDeck(energyCost: 1));
            // Multiple draw calls should not push energy above MaxEnergy
            for (int i = 0; i < 5; i++) _hand.DrawForTurn();
            Assert.LessOrEqual(_hand.CurrentEnergy, CardHandManager.MaxEnergy);
        }

        [Test]
        public void Energy_NeverDropsBelowZero()
        {
            _hand.InitializeDeck(MakeDeck(energyCost: 1));
            _hand.DrawForTurn();
            // Play all cards in hand
            while (_hand.Hand.Count > 0 && _hand.CurrentEnergy > 0)
                _hand.TryPlayCard(_hand.Hand[0], new GridPosition(0, 0));
            Assert.GreaterOrEqual(_hand.CurrentEnergy, 0);
        }
    }
}
