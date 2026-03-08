using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.UI.Lobby
{
    /// <summary>
    /// Main lobby screen controller. Handles navigation to game scenes
    /// and displays player header info (name, currency).
    /// </summary>
    public class LobbyUIController : MonoBehaviour
    {
        [Header("Navigation Buttons")]
        [SerializeField] private Button _btnCombat;
        [SerializeField] private Button _btnEmpire;
        [SerializeField] private Button _btnWorldMap;
        [SerializeField] private Button _btnAlliance;

        [Header("Header")]
        [SerializeField] private Text _playerNameText;
        [SerializeField] private Text _currencyText;

        private GameManager _gameManager;

        private void Start()
        {
            ServiceLocator.TryGet(out _gameManager);

            if (_btnCombat != null)   _btnCombat.onClick.AddListener(OnCombatPressed);
            if (_btnEmpire != null)   _btnEmpire.onClick.AddListener(OnEmpirePressed);
            if (_btnWorldMap != null)  _btnWorldMap.onClick.AddListener(OnWorldMapPressed);
            if (_btnAlliance != null)  _btnAlliance.onClick.AddListener(OnAlliancePressed);
        }

        private void OnDestroy()
        {
            if (_btnCombat != null)   _btnCombat.onClick.RemoveListener(OnCombatPressed);
            if (_btnEmpire != null)   _btnEmpire.onClick.RemoveListener(OnEmpirePressed);
            if (_btnWorldMap != null)  _btnWorldMap.onClick.RemoveListener(OnWorldMapPressed);
            if (_btnAlliance != null)  _btnAlliance.onClick.RemoveListener(OnAlliancePressed);
        }

        /// <summary>
        /// Refresh header with current player data. Called by external systems
        /// after player data loads.
        /// </summary>
        public void RefreshHeader(string playerName, int premiumCurrency)
        {
            if (_playerNameText != null)
                _playerNameText.text = playerName;
            if (_currencyText != null)
                _currencyText.text = premiumCurrency.ToString("N0");
        }

        private void OnCombatPressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.Combat);
        }

        private void OnEmpirePressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.Empire);
        }

        private void OnWorldMapPressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.WorldMap);
        }

        private void OnAlliancePressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.Alliance);
        }
    }
}
