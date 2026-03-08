using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Alliance;

namespace AshenThrone.UI.WorldMap
{
    /// <summary>
    /// Controls the world map screen UI: territory info sidebar, mini-map,
    /// and back-navigation to the lobby.
    /// </summary>
    public class WorldMapUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _territoryInfoSidebar;
        [SerializeField] private GameObject _miniMap;

        [Header("Territory Info")]
        [SerializeField] private Text _territoryNameText;
        [SerializeField] private Text _territoryOwnerText;
        [SerializeField] private Text _territoryBonusText;
        [SerializeField] private Button _btnAttackTerritory;
        [SerializeField] private Button _btnCloseSidebar;

        [Header("Navigation")]
        [SerializeField] private Button _btnBackToLobby;

        private GameManager _gameManager;
        private EventSubscription _territoryCapturedSub;

        private void Start()
        {
            ServiceLocator.TryGet(out _gameManager);

            if (_btnBackToLobby != null)
                _btnBackToLobby.onClick.AddListener(OnBackPressed);
            if (_btnCloseSidebar != null)
                _btnCloseSidebar.onClick.AddListener(CloseSidebar);

            CloseSidebar();
        }

        private void OnEnable()
        {
            _territoryCapturedSub = EventBus.Subscribe<TerritoryCapturedEvent>(OnTerritoryCaptured);
        }

        private void OnDisable()
        {
            _territoryCapturedSub?.Dispose();
        }

        private void OnDestroy()
        {
            if (_btnBackToLobby != null)
                _btnBackToLobby.onClick.RemoveListener(OnBackPressed);
            if (_btnCloseSidebar != null)
                _btnCloseSidebar.onClick.RemoveListener(CloseSidebar);
        }

        /// <summary>
        /// Show territory details in the sidebar when the player taps a territory.
        /// </summary>
        public void ShowTerritoryInfo(string territoryName, string ownerName, string bonusDescription)
        {
            if (_territoryNameText != null)  _territoryNameText.text = territoryName;
            if (_territoryOwnerText != null)  _territoryOwnerText.text = ownerName;
            if (_territoryBonusText != null)  _territoryBonusText.text = bonusDescription;

            if (_territoryInfoSidebar != null)
                _territoryInfoSidebar.SetActive(true);
        }

        private void CloseSidebar()
        {
            if (_territoryInfoSidebar != null)
                _territoryInfoSidebar.SetActive(false);
        }

        private void OnTerritoryCaptured(TerritoryCapturedEvent evt)
        {
            // Refresh map visuals when a territory changes hands
            // Concrete map rendering will be implemented with art assets in Phase 8
        }

        private void OnBackPressed()
        {
            if (_gameManager != null)
                _gameManager.LoadSceneAsync(SceneName.Lobby);
        }
    }
}
