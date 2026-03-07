using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Single research node button in the ResearchTreePanel.
    /// Visual states: Locked (grey), Available (highlighted), In-Progress (animated), Completed (checkmark).
    /// Calls onClicked callback when tapped.
    /// </summary>
    public class ResearchNodeWidget : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Header("Visuals")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _completedCheckmark;
        [SerializeField] private GameObject _inProgressIndicator;

        [Header("Colors")]
        [SerializeField] private Color _lockedColor   = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color _availableColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color _completedColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color _inProgressColor = new Color(1f, 0.8f, 0.2f);

        [Header("Interaction")]
        [SerializeField] private Button _button;

        private ResearchNodeData _node;
        private Action<ResearchNodeData> _onClicked;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// Bind this widget to a research node and its current state.
        /// </summary>
        public void Bind(ResearchNodeData node, bool completed, bool available, bool inProgress,
            Action<ResearchNodeData> onClicked)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _onClicked = onClicked;

            if (_nameLabel != null) _nameLabel.text = node.displayName;

            if (completed)
            {
                SetState(_completedColor, "Done", buttonInteractable: false);
                if (_completedCheckmark != null) _completedCheckmark.gameObject.SetActive(true);
                if (_inProgressIndicator != null) _inProgressIndicator.SetActive(false);
            }
            else if (inProgress)
            {
                SetState(_inProgressColor, "Researching", buttonInteractable: false);
                if (_completedCheckmark != null) _completedCheckmark.gameObject.SetActive(false);
                if (_inProgressIndicator != null) _inProgressIndicator.SetActive(true);
            }
            else if (available)
            {
                SetState(_availableColor, "Available", buttonInteractable: true);
                if (_completedCheckmark != null) _completedCheckmark.gameObject.SetActive(false);
                if (_inProgressIndicator != null) _inProgressIndicator.SetActive(false);
            }
            else
            {
                SetState(_lockedColor, "Locked", buttonInteractable: false);
                if (_completedCheckmark != null) _completedCheckmark.gameObject.SetActive(false);
                if (_inProgressIndicator != null) _inProgressIndicator.SetActive(false);
            }
        }

        private void SetState(Color bgColor, string status, bool buttonInteractable)
        {
            if (_background != null) _background.color = bgColor;
            if (_statusLabel != null) _statusLabel.text = status;
            if (_button != null) _button.interactable = buttonInteractable;
        }

        private void HandleClick()
        {
            if (_node != null) _onClicked?.Invoke(_node);
        }
    }
}
