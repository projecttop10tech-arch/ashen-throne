using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Single card widget rendered in the CardHandView.
    /// Displays card name, element icon, energy cost, and effect description.
    /// Pooled — always call Bind() after retrieval and is reset on return.
    /// </summary>
    public class CardWidget : MonoBehaviour
    {
        [Header("Card Info")]
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _energyCostLabel;
        [SerializeField] private Image _elementIcon;
        [SerializeField] private Image _cardTypeIndicator;
        [SerializeField] private Image _comboIndicator;

        [Header("Interaction")]
        [SerializeField] private Button _button;
        [SerializeField] private CanvasGroup _canvasGroup;

        private AbilityCardData _card;
        private Action<AbilityCardData> _onClicked;

        public string CardId => _card != null ? _card.cardId : string.Empty;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>Bind card data and click callback. Call after pool retrieval.</summary>
        public void Bind(AbilityCardData card, Action<AbilityCardData> onClick)
        {
            _card = card ?? throw new ArgumentNullException(nameof(card));
            _onClicked = onClick;

            if (_nameLabel != null)
                _nameLabel.text = card.cardName;
            if (_descriptionLabel != null)
                _descriptionLabel.text = BuildDescriptionText(card);
            if (_energyCostLabel != null)
                _energyCostLabel.text = card.energyCost.ToString();
            if (_comboIndicator != null)
                _comboIndicator.gameObject.SetActive(card.requiresComboTag != ComboTag.None);
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = interactable ? 1f : 0.5f;
            }
        }

        private void HandleClick()
        {
            if (_card != null)
                _onClicked?.Invoke(_card);
        }

        private static string BuildDescriptionText(AbilityCardData card)
        {
            if (card.cardType == CardType.Attack)
                return $"Deal {card.baseEffectValue:F0} damage.";
            if (card.cardType == CardType.Heal)
                return $"Restore {card.baseEffectValue:F0} HP.";
            if (card.cardType == CardType.Buff || card.cardType == CardType.Debuff)
                return $"{card.statusEffect} for {card.statusDuration} turn(s).";
            return card.cardName;
        }
    }
}
