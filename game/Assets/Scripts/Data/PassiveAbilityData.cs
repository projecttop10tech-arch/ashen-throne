using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a hero's always-active passive ability.
    /// Applied at battle start, requires no card play or energy.
    /// </summary>
    [CreateAssetMenu(fileName = "Passive_", menuName = "AshenThrone/Passive Ability", order = 3)]
    public class PassiveAbilityData : ScriptableObject
    {
        public string passiveId;
        public string displayName;
        [TextArea(2, 4)] public string description;

        public PassiveTrigger trigger;
        public PassiveEffect effect;

        /// <summary>Numeric value for the effect (e.g. 0.15 = 15% bonus).</summary>
        public float effectValue;

        [Range(0, 10)] public int maxStacks = 1;
    }

    public enum PassiveTrigger
    {
        BattleStart,        // Applied once when battle begins
        OnHit,              // Triggered each time this hero hits an enemy
        OnTakeDamage,       // Triggered each time this hero takes damage
        OnKill,             // Triggered when this hero defeats an enemy
        OnAllyDeath,        // Triggered when any ally is defeated
        OnComboActivated,   // Triggered when a combo chain resolves
        OnTurnStart,        // Triggered at the start of each of this hero's turns
        OnBelowHpThreshold  // Triggered once when HP drops below effectValue (used as %)
    }

    public enum PassiveEffect
    {
        IncreaseAttack,
        IncreaseDefense,
        IncreaseSpeed,
        HealSelf,
        HealAllAllies,
        ApplyShield,
        GrantExtraCard,
        ReduceEnemyAttack,
        IncreaseComboBonus,
        ReviveOnce             // Revive with effectValue% HP once per battle
    }
}
