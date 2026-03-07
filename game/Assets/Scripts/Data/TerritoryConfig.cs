using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject containing all tunable territory war configuration values.
    /// All balance numbers live here — never hardcode in WarEngine or TerritorySystem.
    /// </summary>
    [CreateAssetMenu(fileName = "TerritoryConfig", menuName = "AshenThrone/Configs/Territory Config")]
    public class TerritoryConfig : ScriptableObject
    {
        [Header("World Map")]
        /// <summary>Total number of territory regions on the world map.</summary>
        [field: SerializeField] public int TotalRegions { get; private set; } = 200;

        /// <summary>Hex grid axial radius (regions within this many hex steps of center are valid).</summary>
        [field: SerializeField] public int MapRadius { get; private set; } = 8;

        [Header("War Windows")]
        /// <summary>Duration of each war window in seconds (default 2 hours).</summary>
        [field: SerializeField] public int WarWindowDurationSeconds { get; private set; } = 7200;

        /// <summary>Number of war windows per day.</summary>
        [field: SerializeField] public int WarWindowsPerDay { get; private set; } = 2;

        /// <summary>UTC hour at which the first war window opens each day (0–23).</summary>
        [field: SerializeField] public int WarWindowStartHourUtc { get; private set; } = 18;

        [Header("Territory Capture")]
        /// <summary>Base attack power required to capture an undefended neutral territory.</summary>
        [field: SerializeField] public int NeutralCapturePower { get; private set; } = 500;

        /// <summary>Minimum number of attackers required for a standard attack.</summary>
        [field: SerializeField] public int MinAttackers { get; private set; } = 1;

        /// <summary>Minimum number of attackers for a rally attack.</summary>
        [field: SerializeField] public int RallyMinAttackers { get; private set; } = 10;

        /// <summary>Maximum number of attackers in a rally.</summary>
        [field: SerializeField] public int RallyMaxAttackers { get; private set; } = 50;

        /// <summary>How long a rally remains open for members to join, in seconds.</summary>
        [field: SerializeField] public int RallyOpenDurationSeconds { get; private set; } = 300;

        [Header("Fortification")]
        /// <summary>Base fortification HP for a freshly built wall (tier 1).</summary>
        [field: SerializeField] public int FortificationBaseHp { get; private set; } = 5000;

        /// <summary>HP multiplier per fortification tier (linear).</summary>
        [field: SerializeField] public float FortificationTierMultiplier { get; private set; } = 1.5f;

        /// <summary>Maximum fortification tier.</summary>
        [field: SerializeField] public int FortificationMaxTier { get; private set; } = 5;

        [Header("Supply Lines")]
        /// <summary>
        /// Maximum hex distance between two alliance territories that can share supply bonuses.
        /// Territories farther apart are isolated.
        /// </summary>
        [field: SerializeField] public int SupplyLineMaxRange { get; private set; } = 3;

        [Header("Resource Bonuses (% per connected territory)")]
        /// <summary>Bonus resource production % per territory of type Resource owned.</summary>
        [field: SerializeField] public float ResourceTerritoryBonus { get; private set; } = 0.05f;

        /// <summary>Bonus combat power % per territory of type Military owned.</summary>
        [field: SerializeField] public float MilitaryTerritoryBonus { get; private set; } = 0.03f;

        /// <summary>Bonus research speed % per territory of type Research owned.</summary>
        [field: SerializeField] public float ResearchTerritoryBonus { get; private set; } = 0.04f;

        [Header("Contribution Points")]
        /// <summary>Alliance contribution points awarded for capturing a territory.</summary>
        [field: SerializeField] public int ContributionForCapture { get; private set; } = 500;

        /// <summary>Alliance contribution points awarded for successfully defending a territory.</summary>
        [field: SerializeField] public int ContributionForDefend { get; private set; } = 300;

        /// <summary>Alliance contribution points awarded for participating in a rally.</summary>
        [field: SerializeField] public int ContributionForRally { get; private set; } = 100;
    }
}
