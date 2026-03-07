using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Data
{
    /// <summary>
    /// ScriptableObject defining a hero's identity, base stats, roles, and ability card pool.
    /// One asset per hero. All runtime stat scaling is computed from these base values plus player level data.
    /// </summary>
    [CreateAssetMenu(fileName = "Hero_", menuName = "AshenThrone/Hero Data", order = 1)]
    public class HeroData : ScriptableObject
    {
        [Header("Identity")]
        /// <summary>Unique string key used for all lookups, save data, and analytics. Never change after ship.</summary>
        public string heroId;
        public string displayName;
        [TextArea(2, 5)] public string loreDescription;
        public HeroRarity rarity;
        public HeroRole primaryRole;
        public HeroRole secondaryRole;
        public HeroFaction faction;

        [Header("Visuals")]
        public Sprite portrait;
        public Sprite fullBodySprite;
        /// <summary>Spine skeleton data asset for animated combat presentation.</summary>
        public Object spineSkeletonData;
        public Color factionColor = Color.white;

        [Header("Base Stats (Level 1)")]
        /// <summary>Maximum hit points at level 1. Scaled by ProgressionCurve.statGrowthPerLevel.</summary>
        public int baseHealth = 1000;
        public int baseAttack = 100;
        public int baseDefense = 80;
        public int baseSpeed = 10;
        /// <summary>0.0–1.0 chance to critically strike for 1.5x damage.</summary>
        [Range(0f, 1f)] public float baseCritChance = 0.05f;

        [Header("Row Preference")]
        /// <summary>Preferred combat row. AI and auto-placement use this. Players can override.</summary>
        public CombatRow preferredRow = CombatRow.Front;

        [Header("Ability Cards")]
        /// <summary>The full pool of ability cards this hero can appear on. Players choose 15 for their loadout deck.</summary>
        public List<AbilityCardData> abilityPool = new();

        [Header("Passive Ability")]
        /// <summary>Always-active passive effect. Applied at battle start, requires no card.</summary>
        public PassiveAbilityData passiveAbility;

        [Header("Shard Economy")]
        /// <summary>Number of shards required to unlock this hero at level 1.</summary>
        public int shardsToUnlock = 60;
        /// <summary>Shards required for each star promotion tier (index 0 = 2-star, index 4 = 6-star).</summary>
        public int[] shardsPerStarTier = { 20, 40, 60, 80, 100 };

        [Header("Voice Lines")]
        public AudioClip voiceSelect;
        public AudioClip voiceCombatStart;
        public AudioClip voiceVictory;
        public AudioClip voiceDefeated;
    }

    public enum HeroRarity { Common, Uncommon, Rare, Epic, Legendary }

    public enum HeroRole
    {
        Tank,           // Front-row absorbs damage, has taunt abilities
        Warrior,        // Melee DPS, strong single-target damage
        Ranger,         // Back-row ranged DPS, high crit
        Mage,           // Area-of-effect spells, terrain manipulation
        Healer,         // Restore HP, remove debuffs
        Support,        // Buffs, chain combos, tactical enablers
        Assassin        // High burst damage, stealth mechanics
    }

    public enum HeroFaction
    {
        IronLegion,     // Armored warriors, fortress builders
        AshCult,        // Dark mages, shadow manipulation
        WildHunters,    // Rangers, beast companions
        StoneSanctum,   // Healers, ancient rune magic
        VoidReapers     // Assassins, dimensional magic
    }

    public enum CombatRow { Front, Middle, Back }
}
