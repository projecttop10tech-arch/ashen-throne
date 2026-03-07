using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject holding all tunable empire/city balance constants.
    /// Default vault capacities, production rates, and build rules live here.
    /// </summary>
    [CreateAssetMenu(fileName = "EmpireConfig", menuName = "AshenThrone/Empire Config", order = 12)]
    public class EmpireConfig : ScriptableObject
    {
        [Header("Starting Resources")]
        public long StartingStone = 500;
        public long StartingIron = 200;
        public long StartingGrain = 300;
        public long StartingArcaneEssence = 50;

        [Header("Default Vault Capacities (before upgrades)")]
        public long DefaultMaxStone = 5000;
        public long DefaultMaxIron = 5000;
        public long DefaultMaxGrain = 5000;
        public long DefaultMaxArcaneEssence = 1000;

        [Header("Offline Earnings")]
        /// <summary>Maximum hours of offline production credited on login. Design promise: never feels punishing.</summary>
        [Range(1, 24)] public int MaxOfflineEarningsHours = 8;

        [Header("Build Queue")]
        /// <summary>Free build queue slots available to all players from day one.</summary>
        [Range(1, 5)] public int FreeQueueSlots = 2;
        /// <summary>Maximum build time in seconds for the Stronghold (core building). 28800 = 8 hours.</summary>
        public int MaxBuildTimeSecondsStronghold = 28800;
        /// <summary>Maximum build time in seconds for all other buildings. 14400 = 4 hours.</summary>
        public int MaxBuildTimeSecondsOther = 14400;

        [Header("Alliance Help")]
        /// <summary>Seconds reduced per alliance member help action.</summary>
        public int AllianceHelpSecondsPerMember = 120;
        /// <summary>Maximum alliance helpers per build queue entry.</summary>
        public int MaxAllianceHelpers = 20;

        [Header("Research")]
        /// <summary>Maximum simultaneous research operations.</summary>
        [Range(1, 3)] public int MaxResearchQueues = 1;
    }
}
