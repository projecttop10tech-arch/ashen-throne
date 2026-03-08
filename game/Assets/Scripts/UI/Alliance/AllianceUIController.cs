using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.UI.Alliance
{
    /// <summary>
    /// Controls the Alliance screen: tab switching between Members, Chat, Wars, and Leaderboard panels.
    /// </summary>
    public class AllianceUIController : MonoBehaviour
    {
        [Header("Tab Buttons")]
        [SerializeField] private Button _btnMembers;
        [SerializeField] private Button _btnChat;
        [SerializeField] private Button _btnWars;
        [SerializeField] private Button _btnLeaderboard;

        [Header("Panels (toggled by tabs)")]
        [SerializeField] private GameObject _membersPanel;
        [SerializeField] private GameObject _chatPanel;
        [SerializeField] private GameObject _warsPanel;
        [SerializeField] private GameObject _leaderboardPanel;

        [Header("Navigation")]
        [SerializeField] private Button _btnBackToLobby;

        private GameManager _gameManager;
        private GameObject _activePanel;

        private void Start()
        {
            ServiceLocator.TryGet(out _gameManager);

            if (_btnMembers != null)     _btnMembers.onClick.AddListener(() => SwitchTab(_membersPanel));
            if (_btnChat != null)        _btnChat.onClick.AddListener(() => SwitchTab(_chatPanel));
            if (_btnWars != null)        _btnWars.onClick.AddListener(() => SwitchTab(_warsPanel));
            if (_btnLeaderboard != null) _btnLeaderboard.onClick.AddListener(() => SwitchTab(_leaderboardPanel));
            if (_btnBackToLobby != null) _btnBackToLobby.onClick.AddListener(OnBackPressed);

            // Default to Members tab
            SwitchTab(_membersPanel);
        }

        private void OnDestroy()
        {
            if (_btnMembers != null)     _btnMembers.onClick.RemoveAllListeners();
            if (_btnChat != null)        _btnChat.onClick.RemoveAllListeners();
            if (_btnWars != null)        _btnWars.onClick.RemoveAllListeners();
            if (_btnLeaderboard != null) _btnLeaderboard.onClick.RemoveAllListeners();
            if (_btnBackToLobby != null) _btnBackToLobby.onClick.RemoveAllListeners();
        }

        private void SwitchTab(GameObject targetPanel)
        {
            if (targetPanel == null) return;
            if (_activePanel != null) _activePanel.SetActive(false);

            targetPanel.SetActive(true);
            _activePanel = targetPanel;
        }

        private void OnBackPressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.Lobby);
        }
    }
}
