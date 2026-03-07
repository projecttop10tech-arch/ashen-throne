using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Manages the player's four resource stockpiles: Stone, Iron, Grain, Arcane Essence.
    /// Handles idle production calculation including offline earnings (capped at 8 hours).
    /// All mutations are server-validated via PlayFab before acceptance.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        [SerializeField] private Data.EmpireConfig _config;

        // Current stockpiles — initialized from EmpireConfig.StartingResources
        public long Stone { get; private set; }
        public long Iron { get; private set; }
        public long Grain { get; private set; }
        public long ArcaneEssence { get; private set; }

        // Production rates (per second, computed from buildings via RecalculateProductionRates)
        public float StonePerSecond { get; private set; }
        public float IronPerSecond { get; private set; }
        public float GrainPerSecond { get; private set; }
        public float ArcaneEssencePerSecond { get; private set; }

        // Vault capacities — initialized from EmpireConfig.DefaultMax*, then scaled by building bonuses
        public long MaxStone { get; private set; }
        public long MaxIron { get; private set; }
        public long MaxGrain { get; private set; }
        public long MaxArcaneEssence { get; private set; }

        public event Action<ResourceType, long, long> OnResourceChanged; // type, oldVal, newVal

        private float _accumulatedStone;
        private float _accumulatedIron;
        private float _accumulatedGrain;
        private float _accumulatedArcane;

        private EventSubscription _buildingCompletedSubscription;

        private void OnEnable()
        {
            _buildingCompletedSubscription = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildingUpgradeCompleted);
        }

        private void OnDisable()
        {
            _buildingCompletedSubscription?.Dispose();
        }

        private void Awake()
        {
            if (_config == null)
                Debug.LogError("[ResourceManager] EmpireConfig not assigned in Inspector. Assign it on the Empire scene's ResourceManager component.");
            else
                InitializeFromConfig();
            ServiceLocator.Register<ResourceManager>(this);
        }

        private void InitializeFromConfig()
        {
            MaxStone = _config.DefaultMaxStone;
            MaxIron = _config.DefaultMaxIron;
            MaxGrain = _config.DefaultMaxGrain;
            MaxArcaneEssence = _config.DefaultMaxArcaneEssence;
            Stone = _config.StartingStone;
            Iron = _config.StartingIron;
            Grain = _config.StartingGrain;
            ArcaneEssence = _config.StartingArcaneEssence;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ResourceManager>();
        }

        private void Update()
        {
            AccumulateProduction(Time.deltaTime);
        }

        /// <summary>
        /// Calculate and apply offline earnings when the player returns.
        /// offlineSeconds: time elapsed since last session (from server timestamp comparison).
        /// Capped at MaxOfflineEarningsHours.
        /// </summary>
        public void ApplyOfflineEarnings(float offlineSeconds)
        {
            int maxHours = _config != null ? _config.MaxOfflineEarningsHours : 8;
            float cappedSeconds = Mathf.Min(offlineSeconds, maxHours * 3600f);
            AddResource(ResourceType.Stone, (long)(StonePerSecond * cappedSeconds));
            AddResource(ResourceType.Iron, (long)(IronPerSecond * cappedSeconds));
            AddResource(ResourceType.Grain, (long)(GrainPerSecond * cappedSeconds));
            AddResource(ResourceType.ArcaneEssence, (long)(ArcaneEssencePerSecond * cappedSeconds));
        }

        /// <summary>
        /// Check if the player has enough of all four resources.
        /// </summary>
        public bool CanAfford(long stone, long iron, long grain, long arcane) =>
            Stone >= stone && Iron >= iron && Grain >= grain && ArcaneEssence >= arcane;

        /// <summary>
        /// Deduct resources. Call CanAfford first. Throws if insufficient (should not happen in normal flow).
        /// </summary>
        public void Spend(long stone, long iron, long grain, long arcane)
        {
            if (!CanAfford(stone, iron, grain, arcane))
                throw new InvalidOperationException("[ResourceManager] Spend called with insufficient resources.");

            if (stone > 0) SetResource(ResourceType.Stone, Stone - stone);
            if (iron > 0) SetResource(ResourceType.Iron, Iron - iron);
            if (grain > 0) SetResource(ResourceType.Grain, Grain - grain);
            if (arcane > 0) SetResource(ResourceType.ArcaneEssence, ArcaneEssence - arcane);
        }

        /// <summary>
        /// Add a specific resource, capped at vault capacity.
        /// </summary>
        public void AddResource(ResourceType type, long amount)
        {
            if (amount <= 0) return;
            switch (type)
            {
                case ResourceType.Stone: SetResource(type, Math.Min(Stone + amount, MaxStone)); break;
                case ResourceType.Iron: SetResource(type, Math.Min(Iron + amount, MaxIron)); break;
                case ResourceType.Grain: SetResource(type, Math.Min(Grain + amount, MaxGrain)); break;
                case ResourceType.ArcaneEssence: SetResource(type, Math.Min(ArcaneEssence + amount, MaxArcaneEssence)); break;
            }
        }

        /// <summary>
        /// Recalculate all production rates from current building states.
        /// Call after any building upgrade completes.
        /// </summary>
        public void RecalculateProductionRates(IEnumerable<PlacedBuilding> buildings)
        {
            float stone = 0, iron = 0, grain = 0, arcane = 0;
            // Baseline vault capacities from config — building bonuses are applied multiplicatively on top.
            long maxStone = _config != null ? _config.DefaultMaxStone : 5000;
            long maxIron = _config != null ? _config.DefaultMaxIron : 5000;
            long maxGrain = _config != null ? _config.DefaultMaxGrain : 5000;
            long maxArcane = _config != null ? _config.DefaultMaxArcaneEssence : 1000;

            foreach (PlacedBuilding b in buildings)
            {
                BuildingTierData tier = b.Data.GetTier(b.CurrentTier);
                if (tier == null) continue;

                stone += tier.stoneProduction;
                iron += tier.ironProduction;
                grain += tier.grainProduction;
                arcane += tier.arcaneEssenceProduction;

                foreach (var bonus in tier.bonuses)
                {
                    switch (bonus.bonusType)
                    {
                        case BuildingBonusType.VaultCapacity:
                            maxStone = (long)(maxStone * (1f + bonus.bonusPercent / 100f));
                            maxIron = (long)(maxIron * (1f + bonus.bonusPercent / 100f));
                            maxGrain = (long)(maxGrain * (1f + bonus.bonusPercent / 100f));
                            maxArcane = (long)(maxArcane * (1f + bonus.bonusPercent / 100f));
                            break;
                    }
                }
            }

            StonePerSecond = stone / 3600f;
            IronPerSecond = iron / 3600f;
            GrainPerSecond = grain / 3600f;
            ArcaneEssencePerSecond = arcane / 3600f;

            MaxStone = maxStone;
            MaxIron = maxIron;
            MaxGrain = maxGrain;
            MaxArcaneEssence = maxArcane;

            EventBus.Publish(new ProductionRatesUpdatedEvent(StonePerSecond, IronPerSecond, GrainPerSecond, ArcaneEssencePerSecond));
        }

        private void AccumulateProduction(float deltaTime)
        {
            _accumulatedStone += StonePerSecond * deltaTime;
            _accumulatedIron += IronPerSecond * deltaTime;
            _accumulatedGrain += GrainPerSecond * deltaTime;
            _accumulatedArcane += ArcaneEssencePerSecond * deltaTime;

            if (_accumulatedStone >= 1f) { AddResource(ResourceType.Stone, (long)_accumulatedStone); _accumulatedStone %= 1f; }
            if (_accumulatedIron >= 1f) { AddResource(ResourceType.Iron, (long)_accumulatedIron); _accumulatedIron %= 1f; }
            if (_accumulatedGrain >= 1f) { AddResource(ResourceType.Grain, (long)_accumulatedGrain); _accumulatedGrain %= 1f; }
            if (_accumulatedArcane >= 1f) { AddResource(ResourceType.ArcaneEssence, (long)_accumulatedArcane); _accumulatedArcane %= 1f; }
        }

        private void SetResource(ResourceType type, long value)
        {
            long clamped = Math.Max(0, value);
            long old;
            switch (type)
            {
                case ResourceType.Stone: old = Stone; Stone = clamped; break;
                case ResourceType.Iron: old = Iron; Iron = clamped; break;
                case ResourceType.Grain: old = Grain; Grain = clamped; break;
                case ResourceType.ArcaneEssence: old = ArcaneEssence; ArcaneEssence = clamped; break;
                default: return;
            }
            OnResourceChanged?.Invoke(type, old, clamped);
            EventBus.Publish(new ResourceChangedEvent(type, old, clamped));
        }

        private void OnBuildingUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                RecalculateProductionRates(bm.PlacedBuildings.Values);
        }
    }

    public enum ResourceType { Stone, Iron, Grain, ArcaneEssence }

    // --- Events ---
    public readonly struct ResourceChangedEvent { public readonly ResourceType Type; public readonly long OldValue; public readonly long NewValue; public ResourceChangedEvent(ResourceType t, long o, long n) { Type = t; OldValue = o; NewValue = n; } }
    public readonly struct ProductionRatesUpdatedEvent { public readonly float Stone; public readonly float Iron; public readonly float Grain; public readonly float Arcane; public ProductionRatesUpdatedEvent(float s, float i, float g, float a) { Stone = s; Iron = i; Grain = g; Arcane = a; } }
}
