using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// Displays the 30-node research tree as a grid of ResearchNodeWidget buttons.
    /// Nodes are laid out by their ResearchNodeData.gridPosition within each branch tab.
    /// Active research shows a progress indicator.
    ///
    /// Subscribes to ResearchCompletedEvent and ResearchStartedEvent to update node states.
    /// Tapping an available node shows a confirm-research popup.
    /// </summary>
    public class ResearchTreePanel : MonoBehaviour
    {
        [Header("Branch Tabs")]
        [SerializeField] private Button _militaryTabButton;
        [SerializeField] private Button _resourceTabButton;
        [SerializeField] private Button _researchTabButton;
        [SerializeField] private Button _heroTabButton;

        [Header("Node Container")]
        [SerializeField] private Transform _nodeContainer;
        [SerializeField] private GameObject _nodeWidgetPrefab;

        [Header("Detail Panel")]
        [SerializeField] private ResearchDetailPanel _detailPanel;

        [Header("Active Research Bar")]
        [SerializeField] private GameObject _activeResearchBar;
        [SerializeField] private TextMeshProUGUI _activeNodeLabel;
        [SerializeField] private Slider _activeProgressBar;
        [SerializeField] private TextMeshProUGUI _activeTimeLabel;

        [SerializeField] private Button _closeButton;

        private ResearchManager _researchManager;
        private ResearchBranch _activeBranch = ResearchBranch.Military;
        private readonly List<ResearchNodeWidget> _activeWidgets = new(10);
        private float _activeTotalDuration;

        private EventSubscription _completedSub;
        private EventSubscription _startedSub;

        private void Awake()
        {
            _researchManager = ServiceLocator.Get<ResearchManager>();
            if (_researchManager == null)
                Debug.LogError("[ResearchTreePanel] ResearchManager not found in ServiceLocator.", this);

            BindTabButton(_militaryTabButton, ResearchBranch.Military);
            BindTabButton(_resourceTabButton, ResearchBranch.Resource);
            BindTabButton(_researchTabButton, ResearchBranch.Research);
            BindTabButton(_heroTabButton, ResearchBranch.Hero);

            if (_closeButton != null) _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        private void OnEnable()
        {
            _completedSub = EventBus.Subscribe<ResearchCompletedEvent>(OnResearchCompleted);
            _startedSub   = EventBus.Subscribe<ResearchStartedEvent>(OnResearchStarted);
            RefreshBranch(_activeBranch);
            RefreshActiveBar();
        }

        private void OnDisable()
        {
            _completedSub?.Dispose();
            _startedSub?.Dispose();
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;
            if (_researchManager == null || _researchManager.ResearchQueue.Count == 0)
            {
                if (_activeResearchBar != null) _activeResearchBar.SetActive(false);
                return;
            }

            ResearchQueueEntry active = _researchManager.ResearchQueue[0];
            float ratio = _activeTotalDuration > 0f
                ? 1f - (active.RemainingSeconds / _activeTotalDuration)
                : 1f;

            if (_activeProgressBar != null) _activeProgressBar.value = Mathf.Clamp01(ratio);
            if (_activeTimeLabel != null) _activeTimeLabel.text = FormatTime(Mathf.CeilToInt(active.RemainingSeconds));
        }

        private void BindTabButton(Button btn, ResearchBranch branch)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => ShowBranch(branch));
        }

        private void ShowBranch(ResearchBranch branch)
        {
            _activeBranch = branch;
            RefreshBranch(branch);
        }

        private void RefreshBranch(ResearchBranch branch)
        {
            if (_researchManager == null || _nodeWidgetPrefab == null) return;

            // Clear current widgets
            foreach (var w in _activeWidgets)
                Destroy(w.gameObject);
            _activeWidgets.Clear();

            // Rebuild for selected branch
            foreach (ResearchNodeData node in _researchManager.AllNodes)
            {
                if (node.branch != branch) continue;

                var go = Instantiate(_nodeWidgetPrefab, _nodeContainer);
                var widget = go.GetComponent<ResearchNodeWidget>();
                if (widget == null) continue;

                bool completed  = _researchManager.IsCompleted(node.nodeId);
                bool available  = _researchManager.IsAvailable(node.nodeId);
                bool inProgress = _researchManager.ResearchQueue.Count > 0 &&
                                  _researchManager.ResearchQueue[0].NodeId == node.nodeId;

                widget.Bind(node, completed, available, inProgress, OnNodeClicked);
                _activeWidgets.Add(widget);
            }
        }

        private void RefreshActiveBar()
        {
            if (_researchManager == null) return;
            bool hasActive = _researchManager.ResearchQueue.Count > 0;
            if (_activeResearchBar != null) _activeResearchBar.SetActive(hasActive);
            if (!hasActive) return;

            ResearchNodeData node = _researchManager.GetNode(_researchManager.ResearchQueue[0].NodeId);
            if (_activeNodeLabel != null)
                _activeNodeLabel.text = node != null ? node.displayName : "Researching...";
        }

        private void OnNodeClicked(ResearchNodeData node)
        {
            if (_detailPanel != null)
                _detailPanel.Show(node, _researchManager);
        }

        private void OnResearchCompleted(ResearchCompletedEvent evt)
        {
            RefreshBranch(_activeBranch);
            RefreshActiveBar();
        }

        private void OnResearchStarted(ResearchStartedEvent evt)
        {
            _activeTotalDuration = evt.Duration;
            RefreshBranch(_activeBranch);
            RefreshActiveBar();
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Complete";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }
    }

    /// <summary>
    /// Small detail popup shown when tapping a node in ResearchTreePanel.
    /// Displays costs, time, effects, and a Research/Close button.
    /// </summary>
    public class ResearchDetailPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _descriptionLabel;
        [SerializeField] private TextMeshProUGUI _stoneCostLabel;
        [SerializeField] private TextMeshProUGUI _ironCostLabel;
        [SerializeField] private TextMeshProUGUI _grainCostLabel;
        [SerializeField] private TextMeshProUGUI _arcaneCostLabel;
        [SerializeField] private TextMeshProUGUI _timeLabel;
        [SerializeField] private Button _researchButton;
        [SerializeField] private Button _closeButton;

        private ResearchNodeData _node;
        private ResearchManager _researchManager;

        private void Awake()
        {
            if (_researchButton != null) _researchButton.onClick.AddListener(OnResearchPressed);
            if (_closeButton != null) _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void OnDestroy()
        {
            if (_researchButton != null) _researchButton.onClick.RemoveAllListeners();
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        }

        public void Show(ResearchNodeData node, ResearchManager manager)
        {
            _node = node;
            _researchManager = manager;
            gameObject.SetActive(true);

            if (_nameLabel != null) _nameLabel.text = node.displayName;
            if (_descriptionLabel != null) _descriptionLabel.text = node.description;
            if (_stoneCostLabel != null) _stoneCostLabel.text = node.stoneCost.ToString("N0");
            if (_ironCostLabel != null) _ironCostLabel.text = node.ironCost.ToString("N0");
            if (_grainCostLabel != null) _grainCostLabel.text = node.grainCost.ToString("N0");
            if (_arcaneCostLabel != null) _arcaneCostLabel.text = node.arcaneEssenceCost.ToString("N0");
            if (_timeLabel != null) _timeLabel.text = FormatTime(node.researchTimeSeconds);

            bool available = manager.IsAvailable(node.nodeId);
            if (_researchButton != null) _researchButton.interactable = available;
        }

        private void OnResearchPressed()
        {
            if (_node == null || _researchManager == null) return;
            _researchManager.StartResearch(_node.nodeId);
            gameObject.SetActive(false);
        }

        private static string FormatTime(int seconds)
        {
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }
    }
}
