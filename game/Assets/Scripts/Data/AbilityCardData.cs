using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a single ability card's properties.
    /// Cards are drawn from hero loadout decks during combat turns.
    /// All effect values must be readable by a new player within 5 seconds from card text alone.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "AshenThrone/Ability Card", order = 2)]
    public class AbilityCardData : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>Unique key. Never change after ship — used in save data and replay hashes.</summary>
        public string cardId;
        public string displayName;
        /// <summary>Clear description of the effect. Must be self-contained: no hidden mechanics.</summary>
        [TextArea(2, 6)] public string effectDescription;

        [Header("Classification")]
        public CardType cardType;
        public CardTargetType targetType;
        public AbilityElement element;

        [Header("Cost & Timing")]
        /// <summary>Energy cost to play. Range: 1–5. Balance: damage output must be within 15% of tier benchmark per energy spent.</summary>
        [Range(1, 5)] public int energyCost = 2;
        /// <summary>If true, this card is played at the start of the opponent's turn instead.</summary>
        public bool isReaction;

        [Header("Primary Effect")]
        /// <summary>Base damage/heal value before stat multipliers. Verified against ProgressionCurve benchmark.</summary>
        public float baseEffectValue = 100f;
        /// <summary>Stat multiplier applied to baseEffectValue. E.g. 1.5 means 150% of attack stat.</summary>
        public float statMultiplier = 1f;
        public StatType scalingStat = StatType.Attack;

        [Header("Secondary Effect (Optional)")]
        public StatusEffectType applyStatusEffect;
        /// <summary>Duration of the status effect in turns. 0 means no status applied.</summary>
        [Range(0, 5)] public int statusDuration;
        /// <summary>Chance (0–1) for the secondary status effect to proc.</summary>
        [Range(0f, 1f)] public float statusProcChance = 1f;

        [Header("Area Effect")]
        /// <summary>If > 0, hits all targets in a radius around the primary target (grid tiles).</summary>
        [Range(0, 3)] public int splashRadius;

        [Header("Combo Properties")]
        /// <summary>Tag that this card outputs for combo chain detection.</summary>
        public ComboTag outputsComboTag;
        /// <summary>Tag this card consumes to trigger its bonus combo effect.</summary>
        public ComboTag requiresComboTag;
        /// <summary>Bonus damage multiplier applied when requiresComboTag is active (e.g. 1.5 = +50%).</summary>
        public float comboBonusMultiplier = 1f;
        [TextArea(1, 3)] public string comboBonusDescription;

        [Header("Visuals & Audio")]
        public Sprite cardArtwork;
        public Color cardFrameColor = Color.white;
        public ParticleSystem[] impactParticles;
        public AudioClip castSound;
        public AudioClip impactSound;

        [Header("Unlock")]
        /// <summary>Minimum hero star tier required to include this card in a loadout deck.</summary>
        [Range(1, 6)] public int requiredHeroStarTier = 1;
    }

    public enum CardType
    {
        Attack,         // Deals damage
        Defense,        // Reduces incoming damage or shields
        Heal,           // Restores HP
        Buff,           // Positive status on ally
        Debuff,         // Negative status on enemy
        TerrainEffect,  // Modifies a grid tile
        Summon,         // Places a unit on the grid
        Utility         // Chain, reposition, draw
    }

    public enum CardTargetType
    {
        SingleEnemy,
        SingleAlly,
        AllEnemies,
        AllAllies,
        Self,
        TargetTile,
        RandomEnemy,
        LowestHpAlly
    }

    public enum AbilityElement { Physical, Fire, Ice, Lightning, Shadow, Holy, Arcane, Nature }

    public enum StatType { Attack, Defense, Speed, MaxHealth }

    public enum StatusEffectType
    {
        None,
        Burn,           // DOT fire damage per turn
        Freeze,         // Skip next turn
        Stun,           // Skip next turn, reduced defense
        Bleed,          // DOT physical damage per turn
        Slow,           // Reduce speed, acts later in turn order
        Poison,         // DOT arcane damage per turn
        Shielded,       // Block next X damage
        Enraged,        // +50% attack, -30% defense
        Marked,         // Allies deal +25% damage to this target
        Regenerating,   // Heal HP per turn
        Invisible       // Cannot be directly targeted (AoE still hits)
    }

    public enum ComboTag
    {
        None,
        Ignite,         // Fire chain starter
        Shatter,        // Freeze combo finisher (bonus vs frozen)
        Overcharge,     // Lightning chain
        BloodMark,      // Shadow chain
        Illuminate,     // Holy chain
        ArcaneResonance // Arcane chain
    }
}
