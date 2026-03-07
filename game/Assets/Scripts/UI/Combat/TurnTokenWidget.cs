using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Combat;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Single hero portrait token in the TurnOrderDisplay strip.
    /// Shows hero portrait, player/enemy color border, and active-turn highlight.
    /// </summary>
    public class TurnTokenWidget : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private Image _border;
        [SerializeField] private GameObject _activeTurnGlow;
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private Color _playerBorderColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color _enemyBorderColor  = new Color(1f, 0.3f, 0.2f);

        private CombatHero _hero;

        public int BoundHeroInstanceId => _hero?.InstanceId ?? -1;

        public void Bind(CombatHero hero, bool isActiveTurn)
        {
            _hero = hero;
            if (_border != null)
                _border.color = hero.IsPlayerOwned ? _playerBorderColor : _enemyBorderColor;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            SetActiveTurn(isActiveTurn);
        }

        public void SetActiveTurn(bool isActive)
        {
            if (_activeTurnGlow != null)
                _activeTurnGlow.SetActive(isActive);
        }

        public void SetDead()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0.3f;
            SetActiveTurn(false);
        }
    }
}
