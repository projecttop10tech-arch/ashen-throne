using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Small icon representing a single active status effect with a turn duration counter.
    /// Instantiated by HeroStatusDisplay into the status icon container.
    /// </summary>
    public class StatusIconWidget : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _durationLabel;
        [SerializeField] private Sprite[] _statusSprites; // Indexed by StatusEffectType cast to int

        public StatusEffectType BoundType { get; private set; }

        public void Bind(StatusEffectType type, int remainingTurns)
        {
            BoundType = type;
            if (_durationLabel != null)
                _durationLabel.text = remainingTurns.ToString();

            int spriteIndex = (int)type;
            if (_icon != null && _statusSprites != null && spriteIndex < _statusSprites.Length)
                _icon.sprite = _statusSprites[spriteIndex];
        }
    }
}
