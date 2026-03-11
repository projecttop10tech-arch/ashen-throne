using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style: dynamically calculates and displays total city power
    /// based on building tiers. Updates on upgrade complete and placement events.
    /// </summary>
    public class CityPowerHUD : MonoBehaviour
    {
        private Text _powerLabel;
        private BuildingManager _buildingManager;

        private EventSubscription _upgradeSub;
        private EventSubscription _placedSub;
        private EventSubscription _demolishedSub;

        // Power per building tier by category
        private const int CorePowerPerTier = 500;
        private const int MilitaryPowerPerTier = 200;
        private const int ResourcePowerPerTier = 100;
        private const int ResearchPowerPerTier = 150;
        private const int HeroPowerPerTier = 180;
        private const int DefensePowerPerTier = 150;

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _upgradeSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(_ => Refresh());
            _placedSub = EventBus.Subscribe<BuildingPlacedEvent>(_ => Refresh());
            _demolishedSub = EventBus.Subscribe<BuildingDemolishedEvent>(_ => Refresh());
        }

        private void OnDisable()
        {
            _upgradeSub?.Dispose();
            _placedSub?.Dispose();
            _demolishedSub?.Dispose();
        }

        private void Start()
        {
            FindLabel();
            Refresh();
        }

        private void FindLabel()
        {
            var t = FindDeepChild(transform.root, "PowerValue");
            if (t != null) _powerLabel = t.GetComponent<Text>();
        }

        private void Refresh()
        {
            if (_powerLabel == null) return;
            long totalPower = CalculateTotalPower();
            _powerLabel.text = FormatPower(totalPower);
        }

        private long CalculateTotalPower()
        {
            if (_buildingManager == null) return 12450; // Demo fallback
            long total = 0;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                var b = kvp.Value;
                if (b.Data == null) continue;
                int tierMultiplier = b.CurrentTier + 1;
                int basePower = GetBasePower(b.Data.buildingId);
                total += basePower * tierMultiplier;
            }
            return total;
        }

        private static int GetBasePower(string buildingId)
        {
            if (buildingId.Contains("stronghold")) return CorePowerPerTier;
            if (buildingId.Contains("barracks") || buildingId.Contains("training")) return MilitaryPowerPerTier;
            if (buildingId.Contains("wall") || buildingId.Contains("tower") || buildingId.Contains("watch")) return DefensePowerPerTier;
            if (buildingId.Contains("forge") || buildingId.Contains("armory") || buildingId.Contains("enchanting")) return HeroPowerPerTier;
            if (buildingId.Contains("academy") || buildingId.Contains("library") || buildingId.Contains("laboratory") || buildingId.Contains("observatory")) return ResearchPowerPerTier;
            if (buildingId.Contains("farm") || buildingId.Contains("mine") || buildingId.Contains("quarry") || buildingId.Contains("arcane_tower")) return ResourcePowerPerTier;
            return 100;
        }

        private static string FormatPower(long power)
        {
            if (power >= 1_000_000_000L) return $"{power / 1_000_000_000f:F2}B";
            if (power >= 1_000_000) return $"{power / 1_000_000f:F1}M";
            if (power >= 1_000) return $"{power / 1_000f:F1}K";
            return power.ToString("N0");
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeepChild(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
