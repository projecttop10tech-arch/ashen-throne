using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Manages the empire research tree.
    /// Responsibilities:
    ///   - Load all ResearchNodeData assets from Resources/Research/
    ///   - Track which nodes are completed, in-progress, or locked
    ///   - Validate prerequisites before queuing a node
    ///   - Tick research timers in Update
    ///   - Apply ResearchEffects to the player's state on completion
    ///   - Persist completed node IDs (hydrated from PlayFab on session start)
    ///
    /// ResearchEffects that modify combat stats are applied as modifiers stored in a
    /// ResearchBonusState struct that CombatHeroFactory reads when creating heroes.
    /// Zero direct MonoBehaviour coupling to combat — all communication via EventBus.
    /// </summary>
    public class ResearchManager : MonoBehaviour
    {
        [SerializeField] private EmpireConfig _config;

        private readonly Dictionary<string, ResearchNodeData> _allNodes = new();
        private readonly HashSet<string> _completedNodeIds = new();
        private readonly List<ResearchQueueEntry> _researchQueue = new();
        private readonly ResearchBonusState _bonuses = new();

        private ResourceManager _resourceManager;

        public IReadOnlyCollection<string> CompletedNodeIds => _completedNodeIds;
        public IReadOnlyList<ResearchQueueEntry> ResearchQueue => _researchQueue;
        public ResearchBonusState Bonuses => _bonuses;

        public event Action<string> OnResearchCompleted; // nodeId
        public event Action<string> OnResearchStarted;   // nodeId

        private void Awake()
        {
            _resourceManager = GetComponent<ResourceManager>() ?? ServiceLocator.Get<ResourceManager>();
            ServiceLocator.Register<ResearchManager>(this);
            LoadAllNodes();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ResearchManager>();
        }

        private void Update()
        {
            TickResearchQueue();
        }

        /// <summary>
        /// Hydrate completed research from persisted save data.
        /// Call on session start after loading player data from PlayFab.
        /// </summary>
        public void HydrateCompletedNodes(IEnumerable<string> completedIds)
        {
            if (completedIds == null) return;
            foreach (string id in completedIds)
            {
                if (_allNodes.TryGetValue(id, out ResearchNodeData node))
                {
                    _completedNodeIds.Add(id);
                    ApplyEffects(node); // Restore bonuses from all prior research
                }
            }
        }

        /// <summary>
        /// Attempt to queue a research node.
        /// </summary>
        /// <returns>True if successfully queued.</returns>
        public bool StartResearch(string nodeId)
        {
            if (!_allNodes.TryGetValue(nodeId, out ResearchNodeData node))
            {
                Debug.LogWarning($"[ResearchManager] Node '{nodeId}' not found.");
                return false;
            }

            if (_completedNodeIds.Contains(nodeId))
            {
                EventBus.Publish(new ResearchFailedEvent(nodeId, "Already completed"));
                return false;
            }

            if (_researchQueue.Exists(e => e.NodeId == nodeId))
            {
                EventBus.Publish(new ResearchFailedEvent(nodeId, "Already in queue"));
                return false;
            }

            int maxQueues = _config != null ? _config.MaxResearchQueues : 1;
            if (_researchQueue.Count >= maxQueues)
            {
                EventBus.Publish(new ResearchFailedEvent(nodeId, "Research queue full"));
                return false;
            }

            if (!ArePrerequisitesMet(node))
            {
                EventBus.Publish(new ResearchFailedEvent(nodeId, "Prerequisites not met"));
                return false;
            }

            if (_resourceManager == null || !_resourceManager.CanAfford(node.stoneCost, node.ironCost, node.grainCost, node.arcaneEssenceCost))
            {
                EventBus.Publish(new ResearchFailedEvent(nodeId, "Insufficient resources"));
                return false;
            }

            _resourceManager.Spend(node.stoneCost, node.ironCost, node.grainCost, node.arcaneEssenceCost);

            float duration = CalculateResearchTime(node);
            _researchQueue.Add(new ResearchQueueEntry(nodeId, duration));
            OnResearchStarted?.Invoke(nodeId);
            EventBus.Publish(new ResearchStartedEvent(nodeId, duration));
            return true;
        }

        /// <summary>
        /// Apply a speedup to the first active research entry.
        /// </summary>
        public void ApplySpeedup(int speedupSeconds)
        {
            if (_researchQueue.Count == 0) return;
            _researchQueue[0].RemainingSeconds = Mathf.Max(0f, _researchQueue[0].RemainingSeconds - speedupSeconds);
            EventBus.Publish(new ResearchSpeedupAppliedEvent(_researchQueue[0].NodeId, speedupSeconds));
        }

        public bool IsCompleted(string nodeId) => _completedNodeIds.Contains(nodeId);

        public bool IsAvailable(string nodeId)
        {
            if (!_allNodes.TryGetValue(nodeId, out ResearchNodeData node)) return false;
            if (_completedNodeIds.Contains(nodeId)) return false;
            return ArePrerequisitesMet(node);
        }

        public ResearchNodeData GetNode(string nodeId)
        {
            _allNodes.TryGetValue(nodeId, out ResearchNodeData node);
            return node;
        }

        public IReadOnlyCollection<ResearchNodeData> AllNodes => _allNodes.Values;

        private void TickResearchQueue()
        {
            if (_researchQueue.Count == 0) return;

            ResearchQueueEntry active = _researchQueue[0];
            active.RemainingSeconds -= Time.deltaTime;
            if (active.RemainingSeconds <= 0f)
                CompleteResearch(active.NodeId);
        }

        private void CompleteResearch(string nodeId)
        {
            _researchQueue.RemoveAt(0);
            _completedNodeIds.Add(nodeId);

            if (_allNodes.TryGetValue(nodeId, out ResearchNodeData node))
                ApplyEffects(node);

            OnResearchCompleted?.Invoke(nodeId);
            EventBus.Publish(new ResearchCompletedEvent(nodeId));
        }

        private void ApplyEffects(ResearchNodeData node)
        {
            foreach (var effect in node.effects)
                _bonuses.Apply(effect);
            EventBus.Publish(new ResearchBonusesUpdatedEvent(_bonuses));
        }

        private bool ArePrerequisitesMet(ResearchNodeData node)
        {
            if (node.prerequisiteNodeIds == null || node.prerequisiteNodeIds.Length == 0) return true;
            foreach (string prereq in node.prerequisiteNodeIds)
            {
                if (!_completedNodeIds.Contains(prereq)) return false;
            }
            return true;
        }

        private float CalculateResearchTime(ResearchNodeData node)
        {
            float reduction = _bonuses.ResearchTimeReductionPercent / 100f;
            return node.researchTimeSeconds * Mathf.Max(0.1f, 1f - reduction);
        }

        private void LoadAllNodes()
        {
            ResearchNodeData[] nodes = Resources.LoadAll<ResearchNodeData>("Research");
            foreach (ResearchNodeData node in nodes)
            {
                if (string.IsNullOrEmpty(node.nodeId))
                {
                    Debug.LogWarning($"[ResearchManager] ResearchNodeData '{node.name}' has no nodeId — skipping.");
                    continue;
                }
                if (_allNodes.ContainsKey(node.nodeId))
                {
                    Debug.LogWarning($"[ResearchManager] Duplicate nodeId '{node.nodeId}' — skipping duplicate.");
                    continue;
                }
                _allNodes[node.nodeId] = node;
            }
        }
    }

    /// <summary>
    /// Aggregated combat and empire bonuses from all completed research nodes.
    /// Read by CombatHeroFactory and ResourceManager to apply percentage modifiers.
    /// All values are cumulative percentages (10 = +10%).
    /// </summary>
    public class ResearchBonusState
    {
        // Combat
        public float CombatAttackPercent { get; private set; }
        public float CombatDefensePercent { get; private set; }
        public float CombatSpeedPercent { get; private set; }
        public float CombatCritChancePercent { get; private set; }
        public float CombatPowerPercent { get; private set; }
        public float HealingReceivedPercent { get; private set; }
        public float AllStatsCombatPercent { get; private set; }
        public float PveCritChancePercent { get; private set; }

        // Resource
        public float StoneProductionPercent { get; private set; }
        public float IronProductionPercent { get; private set; }
        public float GrainProductionPercent { get; private set; }
        public float ArcaneProductionPercent { get; private set; }
        public float VaultCapacityPercent { get; private set; }
        public float BuildCostReductionPercent { get; private set; }

        // Research
        public float ResearchTimeReductionPercent { get; private set; }

        // Hero
        public float HeroXpGainPercent { get; private set; }
        public float StarTierCostReductionPercent { get; private set; }
        public float AllianceContributionPercent { get; private set; }

        // Unlock flags
        public bool FormationsUnlocked { get; private set; }
        public bool SiegeWorkshopUnlocked { get; private set; }
        public bool EliteResearchUnlocked { get; private set; }
        public bool RareHeroShardsUnlocked { get; private set; }
        public bool ComboSkillsAtStarTier2Unlocked { get; private set; }

        public void Apply(ResearchEffect effect)
        {
            switch (effect.effectType)
            {
                case ResearchEffectType.CombatAttackPercent:           CombatAttackPercent += effect.magnitude; break;
                case ResearchEffectType.CombatDefensePercent:          CombatDefensePercent += effect.magnitude; break;
                case ResearchEffectType.CombatSpeedPercent:            CombatSpeedPercent += effect.magnitude; break;
                case ResearchEffectType.CombatCritChancePercent:       CombatCritChancePercent += effect.magnitude; break;
                case ResearchEffectType.CombatPowerPercent:            CombatPowerPercent += effect.magnitude; break;
                case ResearchEffectType.HealingReceivedPercent:        HealingReceivedPercent += effect.magnitude; break;
                case ResearchEffectType.AllStatsCombatPercent:         AllStatsCombatPercent += effect.magnitude; break;
                case ResearchEffectType.PveCritChancePercent:          PveCritChancePercent += effect.magnitude; break;

                case ResearchEffectType.StoneProductionPercent:        StoneProductionPercent += effect.magnitude; break;
                case ResearchEffectType.IronProductionPercent:         IronProductionPercent += effect.magnitude; break;
                case ResearchEffectType.GrainProductionPercent:        GrainProductionPercent += effect.magnitude; break;
                case ResearchEffectType.ArcaneProductionPercent:       ArcaneProductionPercent += effect.magnitude; break;
                case ResearchEffectType.VaultCapacityPercent:          VaultCapacityPercent += effect.magnitude; break;
                case ResearchEffectType.BuildCostReductionPercent:     BuildCostReductionPercent += effect.magnitude; break;

                case ResearchEffectType.ResearchTimeReductionPercent:  ResearchTimeReductionPercent += effect.magnitude; break;

                case ResearchEffectType.HeroXpGainPercent:             HeroXpGainPercent += effect.magnitude; break;
                case ResearchEffectType.StarTierCostReductionPercent:  StarTierCostReductionPercent += effect.magnitude; break;
                case ResearchEffectType.AllianceContributionPercent:   AllianceContributionPercent += effect.magnitude; break;

                case ResearchEffectType.UnlockFormations:              FormationsUnlocked = true; break;
                case ResearchEffectType.UnlockSiegeWorkshop:           SiegeWorkshopUnlocked = true; break;
                case ResearchEffectType.UnlockEliteResearch:           EliteResearchUnlocked = true; break;
                case ResearchEffectType.UnlockRareHeroShards:          RareHeroShardsUnlocked = true; break;
                case ResearchEffectType.UnlockComboSkillsAtStarTier2:  ComboSkillsAtStarTier2Unlocked = true; break;
            }
        }
    }

    public class ResearchQueueEntry
    {
        public string NodeId { get; }
        public float RemainingSeconds { get; set; }
        public ResearchQueueEntry(string id, float duration) { NodeId = id; RemainingSeconds = duration; }
    }

    // --- Events ---
    public readonly struct ResearchStartedEvent { public readonly string NodeId; public readonly float Duration; public ResearchStartedEvent(string id, float d) { NodeId = id; Duration = d; } }
    public readonly struct ResearchCompletedEvent { public readonly string NodeId; public ResearchCompletedEvent(string id) { NodeId = id; } }
    public readonly struct ResearchFailedEvent { public readonly string NodeId; public readonly string Reason; public ResearchFailedEvent(string id, string r) { NodeId = id; Reason = r; } }
    public readonly struct ResearchSpeedupAppliedEvent { public readonly string NodeId; public readonly int SecondsApplied; public ResearchSpeedupAppliedEvent(string id, int s) { NodeId = id; SecondsApplied = s; } }
    public readonly struct ResearchBonusesUpdatedEvent { public readonly ResearchBonusState Bonuses; public ResearchBonusesUpdatedEvent(ResearchBonusState b) { Bonuses = b; } }
}
