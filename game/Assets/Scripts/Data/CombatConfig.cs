using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject holding all tunable combat balance constants.
    /// ALL numeric combat values live here — nothing hardcoded in logic classes.
    /// Register with ServiceLocator at boot so systems can access without direct reference.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "AshenThrone/Combat Config", order = 11)]
    public class CombatConfig : ScriptableObject
    {
        [Header("Card Hand")]
        /// <summary>Number of cards in a hero's loadout deck. Fixed at 15 for balance.</summary>
        public int DeckSize = 15;
        /// <summary>Maximum cards a player can hold in hand at once. Overflow discards oldest.</summary>
        public int MaxHandSize = 5;
        /// <summary>Cards drawn at the start of each hero's turn.</summary>
        public int DrawsPerTurn = 3;

        [Header("Energy")]
        /// <summary>Maximum energy a hero can hold at once.</summary>
        public int MaxEnergy = 4;
        /// <summary>Energy regenerated at the start of each hero's turn.</summary>
        public int EnergyRegenPerTurn = 2;

        [Header("Tile Effects")]
        /// <summary>Flat fire damage dealt per turn to units standing on Fire tiles.</summary>
        public int FireTileDamagePerTurn = 50;
        /// <summary>Bonus damage multiplier for units on High Ground tiles (1.25 = +25%).</summary>
        [Range(1f, 2f)] public float HighGroundDamageMultiplier = 1.25f;
        /// <summary>Miss chance (0-1) for attackers targeting units in Shadow Veil.</summary>
        [Range(0f, 1f)] public float ShadowVeilMissChance = 0.30f;
        /// <summary>Energy cost reduction for cards played while standing on Arcane Ley Line.</summary>
        public int ArcaneLayLineEnergyCostReduction = 1;

        [Header("Combat Balance")]
        /// <summary>Minimum damage dealt regardless of defense or mitigation. Prevents zero-damage attacks.</summary>
        [Range(1, 10)] public int MinimumDamage = 1;
        /// <summary>Minimum healing from any heal effect. Prevents zero-heal cards.</summary>
        [Range(0, 10)] public int MinimumHeal = 1;
        /// <summary>Defense mitigation divisor: mitigatedDmg = max(MinimumDamage, rawDmg - defense / MitigationDivisor).</summary>
        [Range(2, 8)] public int MitigationDivisor = 4;
        /// <summary>Critical hit damage multiplier (1.5 = 150% normal damage).</summary>
        [Range(1.1f, 3f)] public float CriticalHitMultiplier = 1.5f;
        /// <summary>Speed modifier applied per Slow status stack (-3 speed per stack).</summary>
        public int SlowSpeedPenalty = 3;
        /// <summary>Attack bonus multiplier for Enraged status (0.5 = +50% attack).</summary>
        [Range(0.1f, 1f)] public float EnragedAttackBonus = 0.5f;
        /// <summary>Defense penalty for Enraged status (0.33 = -33% defense).</summary>
        [Range(0.1f, 0.9f)] public float EnragedDefensePenalty = 0.333f;
        /// <summary>Bonus damage multiplier when attacking a Marked target (1.25 = +25%).</summary>
        [Range(1f, 2f)] public float MarkedDamageBonus = 1.25f;

        [Header("AI & Timing")]
        /// <summary>Delay in seconds between AI hero card plays. Controls visual pacing.</summary>
        [Range(0.1f, 2f)] public float AiActionDelaySeconds = 0.6f;
        /// <summary>Seconds before the player's action phase auto-ends (safety fallback for stalled input).</summary>
        [Range(10f, 120f)] public float PlayerActionTimeoutSeconds = 30f;

        [Header("Status Effect DOT Values")]
        /// <summary>Fire damage dealt per turn to a hero with the Burn status.</summary>
        [Range(1, 200)] public int BurnDamagePerTurn = 30;
        /// <summary>Physical damage dealt per turn to a hero with the Bleed status.</summary>
        [Range(1, 200)] public int BleedDamagePerTurn = 25;
        /// <summary>Arcane damage dealt per turn to a hero with the Poison status.</summary>
        [Range(1, 200)] public int PoisonDamagePerTurn = 20;
        /// <summary>HP restored per turn to a hero with the Regenerating status.</summary>
        [Range(1, 200)] public int RegenerationHealPerTurn = 40;

        [Header("Faction Synergy Bonuses")]
        /// <summary>Damage bonus for 2 heroes of the same faction in the squad (0.10 = +10%).</summary>
        [Range(0f, 0.5f)] public float FactionBonus2Heroes = 0.10f;
        /// <summary>Damage bonus for 3 heroes of the same faction in the squad (0.15 = +15%).</summary>
        [Range(0f, 0.5f)] public float FactionBonus3Heroes = 0.15f;
    }
}
