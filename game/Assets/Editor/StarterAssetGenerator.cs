#if UNITY_EDITOR
// Run this from the Unity Editor menu: AshenThrone → Generate Starter Assets
// Creates all 10 hero ScriptableObjects, 50 ability card ScriptableObjects,
// and 20 PvE level ScriptableObjects in their respective Data/ folders.
// Safe to run multiple times — existing assets are overwritten.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AshenThrone.Data;

namespace AshenThrone.Editor
{
    public static class StarterAssetGenerator
    {
        private const string HeroesPath    = "Assets/Data/Heroes";
        private const string CardsPath     = "Assets/Data/Cards";
        private const string LevelsPath    = "Assets/Data/Levels";
        private const string ResourcesPath = "Assets/Resources/Heroes";

        [MenuItem("AshenThrone/Generate Starter Assets")]
        public static void GenerateAll()
        {
            EnsureDirectories();
            var cards = GenerateCards();
            var heroes = GenerateHeroes(cards);
            GenerateLevels(heroes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StarterAssetGenerator] All starter assets generated successfully.");
        }

        // ─── Card Generation ─────────────────────────────────────────────────────

        private static Dictionary<string, AbilityCardData> GenerateCards()
        {
            var allCards = new Dictionary<string, AbilityCardData>();

            // ── Kaelen Ironwrath (Tank, Iron Legion) ─────────────────────────────
            allCards["shield_slam"] = Card("shield_slam", "Shield Slam",
                "Deal physical damage and apply Stun for 1 turn.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 2, baseVal: 80f, mult: 0.7f, stat: StatType.Defense,
                status: StatusEffectType.Stun, duration: 1, procChance: 0.60f);

            allCards["iron_fortress"] = Card("iron_fortress", "Iron Fortress",
                "Grant a 200-point shield to yourself.",
                CardType.Defense, CardTargetType.Self, AbilityElement.Physical,
                cost: 2, baseVal: 200f, mult: 0.5f, stat: StatType.Defense);

            allCards["taunt_strike"] = Card("taunt_strike", "Taunt Strike",
                "Deal physical damage and apply Marked to enemy for 2 turns.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 100f, mult: 0.8f, stat: StatType.Attack,
                status: StatusEffectType.Marked, duration: 2, procChance: 1f,
                combo: ComboTag.None, requires: ComboTag.None, comboMult: 1f);

            allCards["bulwark_aura"] = Card("bulwark_aura", "Bulwark Aura",
                "Shield all allies for 80 points.",
                CardType.Defense, CardTargetType.AllAllies, AbilityElement.Physical,
                cost: 3, baseVal: 80f, mult: 0.3f, stat: StatType.Defense);

            allCards["shatter_charge"] = Card("shatter_charge", "Shatter Charge",
                "Charge through the enemy line dealing 150% attack to all enemies.",
                CardType.Attack, CardTargetType.AllEnemies, AbilityElement.Physical,
                cost: 4, baseVal: 60f, mult: 1.5f, stat: StatType.Attack);

            // ── Vorra Steelborn (Warrior, Iron Legion) ───────────────────────────
            allCards["siege_hammer"] = Card("siege_hammer", "Siege Hammer",
                "Massive physical blow dealing high damage. Outputs Shatter tag.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 120f, mult: 1.2f, stat: StatType.Attack,
                combo: ComboTag.Shatter);

            allCards["war_cry"] = Card("war_cry", "War Cry",
                "Apply Enraged status to yourself for 2 turns (+50% ATK, -30% DEF).",
                CardType.Buff, CardTargetType.Self, AbilityElement.Physical,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Enraged, duration: 2, procChance: 1f);

            allCards["battering_ram"] = Card("battering_ram", "Battering Ram",
                "Heavy attack dealing splash damage to adjacent enemies.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 100f, mult: 1.0f, stat: StatType.Attack,
                splashRadius: 1);

            allCards["heavy_blow"] = Card("heavy_blow", "Heavy Blow",
                "Powerful strike. If target is Marked, deals combo bonus damage.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 2, baseVal: 110f, mult: 1.0f, stat: StatType.Attack,
                requires: ComboTag.Shatter, comboMult: 1.5f);

            allCards["iron_resolve"] = Card("iron_resolve", "Iron Resolve",
                "Remove all debuffs from yourself and heal for 150 HP.",
                CardType.Heal, CardTargetType.Self, AbilityElement.Holy,
                cost: 2, baseVal: 150f, mult: 0f, stat: StatType.MaxHealth);

            // ── Seraphyn Ashveil (Mage, Ash Cult) ────────────────────────────────
            allCards["cinders_call"] = Card("cinders_call", "Cinder's Call",
                "Hurl a fireball dealing fire damage and applying Burn for 2 turns. Outputs Ignite.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Fire,
                cost: 2, baseVal: 90f, mult: 1.0f, stat: StatType.Attack,
                status: StatusEffectType.Burn, duration: 2, procChance: 0.75f,
                combo: ComboTag.Ignite);

            allCards["ash_nova"] = Card("ash_nova", "Ash Nova",
                "Explosion of ash deals fire damage to all enemies and applies Burn.",
                CardType.Attack, CardTargetType.AllEnemies, AbilityElement.Fire,
                cost: 4, baseVal: 70f, mult: 0.8f, stat: StatType.Attack,
                status: StatusEffectType.Burn, duration: 2, procChance: 0.60f);

            allCards["shadow_veil_cast"] = Card("shadow_veil_cast", "Shadow Veil",
                "Shroud a tile in shadow. Units on that tile have 30% miss chance against them.",
                CardType.TerrainEffect, CardTargetType.TargetTile, AbilityElement.Shadow,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack);

            allCards["pyroclasm"] = Card("pyroclasm", "Pyroclasm",
                "Massive fire blast. Deals +50% damage if target is already Burning (Ignite combo).",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Fire,
                cost: 4, baseVal: 160f, mult: 1.3f, stat: StatType.Attack,
                requires: ComboTag.Ignite, comboMult: 1.5f);

            allCards["ember_step"] = Card("ember_step", "Ember Step",
                "Set a target tile on fire, dealing 50 fire damage per turn to occupants.",
                CardType.TerrainEffect, CardTargetType.TargetTile, AbilityElement.Fire,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack);

            // ── Mordoc the Sundered (Support, Ash Cult) ──────────────────────────
            allCards["soul_chains"] = Card("soul_chains", "Soul Chains",
                "Bind an enemy in shadow chains, applying Slow and Marked.",
                CardType.Debuff, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Slow, duration: 2, procChance: 1f,
                combo: ComboTag.BloodMark);

            allCards["void_drain"] = Card("void_drain", "Void Drain",
                "Drain life force, dealing shadow damage and healing yourself for 50% of damage dealt.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 3, baseVal: 100f, mult: 0.9f, stat: StatType.Attack);

            allCards["shadow_mark"] = Card("shadow_mark", "Shadow Mark",
                "Apply Marked to all enemies for 2 turns. Outputs BloodMark combo.",
                CardType.Debuff, CardTargetType.AllEnemies, AbilityElement.Shadow,
                cost: 3, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Marked, duration: 2, procChance: 1f,
                combo: ComboTag.BloodMark);

            allCards["dark_pact"] = Card("dark_pact", "Dark Pact",
                "Sacrifice 10% of max HP to deal massive shadow damage to a single enemy.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 3, baseVal: 200f, mult: 1.5f, stat: StatType.Attack,
                requires: ComboTag.BloodMark, comboMult: 1.6f);

            allCards["corruption_pulse"] = Card("corruption_pulse", "Corruption Pulse",
                "Poison all enemies. Applies Poison status for 3 turns.",
                CardType.Debuff, CardTargetType.AllEnemies, AbilityElement.Arcane,
                cost: 3, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Poison, duration: 3, procChance: 0.80f);

            // ── Lyra Thornveil (Ranger, Wild Hunters) ────────────────────────────
            allCards["piercing_shot"] = Card("piercing_shot", "Piercing Shot",
                "Ranged arrow attack with high critical hit chance. Outputs Overcharge.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 2, baseVal: 100f, mult: 1.1f, stat: StatType.Attack,
                combo: ComboTag.Overcharge);

            allCards["volley"] = Card("volley", "Volley",
                "Rain of arrows hits all enemies for physical damage.",
                CardType.Attack, CardTargetType.AllEnemies, AbilityElement.Physical,
                cost: 4, baseVal: 60f, mult: 0.9f, stat: StatType.Attack);

            allCards["hunters_mark"] = Card("hunters_mark", "Hunter's Mark",
                "Mark a single enemy target. All allies deal +25% damage to them for 3 turns.",
                CardType.Debuff, CardTargetType.SingleEnemy, AbilityElement.Nature,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Marked, duration: 3, procChance: 1f);

            allCards["swift_feet"] = Card("swift_feet", "Swift Feet",
                "Dash to High Ground tile. Next attack gains High Ground bonus for this turn.",
                CardType.Utility, CardTargetType.Self, AbilityElement.Nature,
                cost: 1, baseVal: 0f, mult: 0f, stat: StatType.Speed);

            allCards["deadeye_strike"] = Card("deadeye_strike", "Deadeye Strike",
                "Precision shot. Deals 200% damage if target is Marked (Overcharge combo).",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 130f, mult: 1.4f, stat: StatType.Attack,
                requires: ComboTag.Overcharge, comboMult: 2.0f);

            // ── Zeph Wildmane (Warrior, Wild Hunters) ────────────────────────────
            allCards["feral_strike"] = Card("feral_strike", "Feral Strike",
                "Wild claw attack dealing physical damage. Applies Bleed for 2 turns.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 2, baseVal: 90f, mult: 1.0f, stat: StatType.Attack,
                status: StatusEffectType.Bleed, duration: 2, procChance: 0.70f);

            allCards["beast_rage"] = Card("beast_rage", "Beast Rage",
                "Enter a frenzy. Apply Enraged to yourself for 3 turns.",
                CardType.Buff, CardTargetType.Self, AbilityElement.Nature,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Enraged, duration: 3, procChance: 1f);

            allCards["rend"] = Card("rend", "Rend",
                "Tear into the enemy, applying Bleed and reducing their defense this turn.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 110f, mult: 1.1f, stat: StatType.Attack,
                status: StatusEffectType.Bleed, duration: 3, procChance: 0.90f);

            allCards["alpha_howl"] = Card("alpha_howl", "Alpha Howl",
                "Intimidating roar Slows all enemies for 2 turns.",
                CardType.Debuff, CardTargetType.AllEnemies, AbilityElement.Nature,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Slow, duration: 2, procChance: 0.85f);

            allCards["pack_hunt"] = Card("pack_hunt", "Pack Hunt",
                "Coordinated strike hits a single target twice. Deals extra if target is Bleeding.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Physical,
                cost: 3, baseVal: 100f, mult: 1.0f, stat: StatType.Attack);

            // ── Aldric Stoneguard (Healer, Stone Sanctum) ────────────────────────
            allCards["ancient_mend"] = Card("ancient_mend", "Ancient Mend",
                "Restore a large amount of HP to one ally.",
                CardType.Heal, CardTargetType.SingleAlly, AbilityElement.Holy,
                cost: 2, baseVal: 250f, mult: 0.5f, stat: StatType.MaxHealth);

            allCards["rune_barrier"] = Card("rune_barrier", "Rune Barrier",
                "Apply a 150-point shield to all allies.",
                CardType.Defense, CardTargetType.AllAllies, AbilityElement.Arcane,
                cost: 3, baseVal: 150f, mult: 0.3f, stat: StatType.Defense);

            allCards["sacred_ground"] = Card("sacred_ground", "Sacred Ground",
                "Sanctify a tile. Units on it gain Regenerating for 2 turns.",
                CardType.TerrainEffect, CardTargetType.TargetTile, AbilityElement.Holy,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack);

            allCards["purify"] = Card("purify", "Purify",
                "Remove all debuffs from lowest HP ally and heal them for 100 HP.",
                CardType.Heal, CardTargetType.LowestHpAlly, AbilityElement.Holy,
                cost: 2, baseVal: 100f, mult: 0.2f, stat: StatType.MaxHealth);

            allCards["lifestone_pulse"] = Card("lifestone_pulse", "Lifestone Pulse",
                "Heal all allies for a moderate amount. Outputs Illuminate combo.",
                CardType.Heal, CardTargetType.AllAllies, AbilityElement.Holy,
                cost: 4, baseVal: 150f, mult: 0.4f, stat: StatType.MaxHealth,
                combo: ComboTag.Illuminate);

            // ── Mira of the Pale Stone (Support, Stone Sanctum) ──────────────────
            allCards["rune_of_power"] = Card("rune_of_power", "Rune of Power",
                "Inscribe a power rune on an ally, applying Enraged for 2 turns.",
                CardType.Buff, CardTargetType.SingleAlly, AbilityElement.Arcane,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Enraged, duration: 2, procChance: 1f);

            allCards["ley_line_tap"] = Card("ley_line_tap", "Ley Line Tap",
                "Set a tile as an Arcane Ley Line. Heroes on it pay 1 less energy for cards.",
                CardType.TerrainEffect, CardTargetType.TargetTile, AbilityElement.Arcane,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack);

            allCards["stone_ward"] = Card("stone_ward", "Stone Ward",
                "Grant Regenerating to all allies for 2 turns and apply a 100-point shield.",
                CardType.Buff, CardTargetType.AllAllies, AbilityElement.Holy,
                cost: 4, baseVal: 100f, mult: 0.2f, stat: StatType.Defense,
                status: StatusEffectType.Regenerating, duration: 2, procChance: 1f);

            allCards["chain_heal"] = Card("chain_heal", "Chain Heal",
                "Heal the lowest HP ally. If Illuminate is active, heal all allies.",
                CardType.Heal, CardTargetType.LowestHpAlly, AbilityElement.Holy,
                cost: 3, baseVal: 200f, mult: 0.3f, stat: StatType.MaxHealth,
                requires: ComboTag.Illuminate, comboMult: 1f,
                combo: ComboTag.Illuminate);

            allCards["arcane_infusion"] = Card("arcane_infusion", "Arcane Infusion",
                "Infuse an ally with arcane energy, granting Regenerating for 3 turns.",
                CardType.Buff, CardTargetType.SingleAlly, AbilityElement.Arcane,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.MaxHealth,
                status: StatusEffectType.Regenerating, duration: 3, procChance: 1f);

            // ── Skaros Nightfall (Assassin, Void Reapers) ────────────────────────
            allCards["void_step"] = Card("void_step", "Void Step",
                "Vanish and reappear. Apply Invisible to yourself for 1 turn.",
                CardType.Buff, CardTargetType.Self, AbilityElement.Shadow,
                cost: 1, baseVal: 0f, mult: 0f, stat: StatType.Speed,
                status: StatusEffectType.Invisible, duration: 1, procChance: 1f,
                combo: ComboTag.BloodMark);

            allCards["shadow_stab"] = Card("shadow_stab", "Shadow Stab",
                "Precise shadow stab dealing high damage. Applies Bleed for 2 turns.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 3, baseVal: 150f, mult: 1.3f, stat: StatType.Attack,
                status: StatusEffectType.Bleed, duration: 2, procChance: 0.85f);

            allCards["midnight_flurry"] = Card("midnight_flurry", "Midnight Flurry",
                "Rapid combo attack hitting twice. Second hit deals bonus if BloodMark active.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 3, baseVal: 90f, mult: 1.1f, stat: StatType.Attack,
                requires: ComboTag.BloodMark, comboMult: 1.8f);

            allCards["void_rend"] = Card("void_rend", "Void Rend",
                "Tear through dimensional space to deal arcane damage to all enemies.",
                CardType.Attack, CardTargetType.AllEnemies, AbilityElement.Arcane,
                cost: 4, baseVal: 80f, mult: 1.0f, stat: StatType.Attack);

            allCards["death_mark"] = Card("death_mark", "Death Mark",
                "Apply Marked and Poison to a single target. Outputs BloodMark.",
                CardType.Debuff, CardTargetType.SingleEnemy, AbilityElement.Shadow,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Poison, duration: 3, procChance: 1f,
                combo: ComboTag.BloodMark);

            // ── Vex the Unbound (Mage, Void Reapers) ─────────────────────────────
            allCards["rift_bolt"] = Card("rift_bolt", "Rift Bolt",
                "Fire a bolt of dimensional energy dealing arcane damage. Outputs ArcaneResonance.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Arcane,
                cost: 2, baseVal: 110f, mult: 1.1f, stat: StatType.Attack,
                combo: ComboTag.ArcaneResonance);

            allCards["entropy_field"] = Card("entropy_field", "Entropy Field",
                "Create an unstable field dealing arcane splash damage.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Arcane,
                cost: 3, baseVal: 90f, mult: 1.0f, stat: StatType.Attack,
                splashRadius: 1);

            allCards["dimensional_anchor"] = Card("dimensional_anchor", "Dimensional Anchor",
                "Freeze an enemy in dimensional lock (Freeze status for 1 turn).",
                CardType.Debuff, CardTargetType.SingleEnemy, AbilityElement.Arcane,
                cost: 2, baseVal: 0f, mult: 0f, stat: StatType.Attack,
                status: StatusEffectType.Freeze, duration: 1, procChance: 0.75f);

            allCards["void_cascade"] = Card("void_cascade", "Void Cascade",
                "Chain arcane explosions. Massive damage if ArcaneResonance is active.",
                CardType.Attack, CardTargetType.SingleEnemy, AbilityElement.Arcane,
                cost: 4, baseVal: 180f, mult: 1.5f, stat: StatType.Attack,
                requires: ComboTag.ArcaneResonance, comboMult: 1.7f);

            allCards["chaos_infusion"] = Card("chaos_infusion", "Chaos Infusion",
                "Infuse chaos energy into all enemies, applying a random debuff to each.",
                CardType.Debuff, CardTargetType.AllEnemies, AbilityElement.Arcane,
                cost: 3, baseVal: 50f, mult: 0.5f, stat: StatType.Attack,
                status: StatusEffectType.Slow, duration: 2, procChance: 0.70f);

            // Save all cards
            foreach (var kvp in allCards)
            {
                string path = $"{CardsPath}/Card_{kvp.Key}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(kvp.Value, path);
            }

            return allCards;
        }

        // ─── Hero Generation ─────────────────────────────────────────────────────

        private static List<HeroData> GenerateHeroes(Dictionary<string, AbilityCardData> cards)
        {
            var heroList = new List<HeroData>();

            heroList.Add(Hero(
                id: "kaelen_ironwrath", name: "Kaelen Ironwrath",
                lore: "The unbreakable shield of the Iron Legion. No wall has ever fallen while Kaelen stood before it.",
                rarity: HeroRarity.Rare, role: HeroRole.Tank, secondary: HeroRole.Warrior,
                faction: HeroFaction.IronLegion, row: CombatRow.Front,
                hp: 1500, atk: 80, def: 150, spd: 8, crit: 0.03f,
                shardsUnlock: 60,
                cards: new[] { "shield_slam", "iron_fortress", "taunt_strike", "bulwark_aura", "shatter_charge" },
                allCards: cards));

            heroList.Add(Hero(
                id: "vorra_steelborn", name: "Vorra Steelborn",
                lore: "A siege-breaker born in the furnaces of the Iron Keep. Her hammer has cracked fortress walls that held for a century.",
                rarity: HeroRarity.Uncommon, role: HeroRole.Warrior, secondary: HeroRole.Tank,
                faction: HeroFaction.IronLegion, row: CombatRow.Front,
                hp: 1200, atk: 130, def: 100, spd: 12, crit: 0.07f,
                shardsUnlock: 40,
                cards: new[] { "siege_hammer", "war_cry", "battering_ram", "heavy_blow", "iron_resolve" },
                allCards: cards));

            heroList.Add(Hero(
                id: "seraphyn_ashveil", name: "Seraphyn Ashveil",
                lore: "She walked through the Ashfall and emerged unchanged. The cult calls her blessed; the survivors call her a nightmare.",
                rarity: HeroRarity.Epic, role: HeroRole.Mage, secondary: HeroRole.Support,
                faction: HeroFaction.AshCult, row: CombatRow.Back,
                hp: 800, atk: 160, def: 50, spd: 11, crit: 0.12f,
                shardsUnlock: 80,
                cards: new[] { "cinders_call", "ash_nova", "shadow_veil_cast", "pyroclasm", "ember_step" },
                allCards: cards));

            heroList.Add(Hero(
                id: "mordoc_sundered", name: "Mordoc the Sundered",
                lore: "Once a scholar of the arcane. The void that claimed his soul left something far more dangerous in its place.",
                rarity: HeroRarity.Rare, role: HeroRole.Support, secondary: HeroRole.Mage,
                faction: HeroFaction.AshCult, row: CombatRow.Back,
                hp: 950, atk: 120, def: 70, spd: 10, crit: 0.08f,
                shardsUnlock: 60,
                cards: new[] { "soul_chains", "void_drain", "shadow_mark", "dark_pact", "corruption_pulse" },
                allCards: cards));

            heroList.Add(Hero(
                id: "lyra_thornveil", name: "Lyra Thornveil",
                lore: "She can thread an arrow through the eye of a storm at five hundred paces. The Wild Hunters' most celebrated scout.",
                rarity: HeroRarity.Rare, role: HeroRole.Ranger, secondary: HeroRole.Assassin,
                faction: HeroFaction.WildHunters, row: CombatRow.Back,
                hp: 900, atk: 145, def: 60, spd: 16, crit: 0.18f,
                shardsUnlock: 60,
                cards: new[] { "piercing_shot", "volley", "hunters_mark", "swift_feet", "deadeye_strike" },
                allCards: cards));

            heroList.Add(Hero(
                id: "zeph_wildmane", name: "Zeph Wildmane",
                lore: "Half-feral, half-unstoppable. The Wild Hunters found him in the deep forest fighting a bear. The bear lost.",
                rarity: HeroRarity.Common, role: HeroRole.Warrior, secondary: HeroRole.Tank,
                faction: HeroFaction.WildHunters, row: CombatRow.Front,
                hp: 1100, atk: 135, def: 85, spd: 14, crit: 0.09f,
                shardsUnlock: 30,
                cards: new[] { "feral_strike", "beast_rage", "rend", "alpha_howl", "pack_hunt" },
                allCards: cards));

            heroList.Add(Hero(
                id: "aldric_stoneguard", name: "Aldric Stoneguard",
                lore: "Keeper of the ancient runes, each carved into his flesh at birth. When Aldric heals, the stone itself seems to breathe.",
                rarity: HeroRarity.Rare, role: HeroRole.Healer, secondary: HeroRole.Support,
                faction: HeroFaction.StoneSanctum, row: CombatRow.Back,
                hp: 1000, atk: 80, def: 80, spd: 10, crit: 0.04f,
                shardsUnlock: 60,
                cards: new[] { "ancient_mend", "rune_barrier", "sacred_ground", "purify", "lifestone_pulse" },
                allCards: cards));

            heroList.Add(Hero(
                id: "mira_pale_stone", name: "Mira of the Pale Stone",
                lore: "Her voice can silence a battlefield. Her runes can turn a rout into a rally. The Sanctum calls her the Architect of Survival.",
                rarity: HeroRarity.Uncommon, role: HeroRole.Support, secondary: HeroRole.Healer,
                faction: HeroFaction.StoneSanctum, row: CombatRow.Middle,
                hp: 1100, atk: 90, def: 90, spd: 12, crit: 0.05f,
                shardsUnlock: 40,
                cards: new[] { "rune_of_power", "ley_line_tap", "stone_ward", "chain_heal", "arcane_infusion" },
                allCards: cards));

            heroList.Add(Hero(
                id: "skaros_nightfall", name: "Skaros Nightfall",
                lore: "Nobody hears Skaros coming. Or going. Or standing right behind them.",
                rarity: HeroRarity.Epic, role: HeroRole.Assassin, secondary: HeroRole.Support,
                faction: HeroFaction.VoidReapers, row: CombatRow.Middle,
                hp: 950, atk: 165, def: 50, spd: 19, crit: 0.20f,
                shardsUnlock: 80,
                cards: new[] { "void_step", "shadow_stab", "midnight_flurry", "void_rend", "death_mark" },
                allCards: cards));

            heroList.Add(Hero(
                id: "vex_unbound", name: "Vex the Unbound",
                lore: "Physics suggested he should not exist. He disagreed, tore a hole in reality, and proved it wrong.",
                rarity: HeroRarity.Legendary, role: HeroRole.Mage, secondary: HeroRole.Support,
                faction: HeroFaction.VoidReapers, row: CombatRow.Back,
                hp: 780, atk: 170, def: 45, spd: 12, crit: 0.15f,
                shardsUnlock: 100,
                cards: new[] { "rift_bolt", "entropy_field", "dimensional_anchor", "void_cascade", "chaos_infusion" },
                allCards: cards));

            foreach (var hero in heroList)
            {
                string path = $"{HeroesPath}/Hero_{hero.heroId}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(hero, path);

                // Also create a copy in Resources for runtime loading by PveEncounterManager
                string resourcePath = $"{ResourcesPath}/Hero_{hero.heroId}.asset";
                AssetDatabase.DeleteAsset(resourcePath);
                AssetDatabase.CopyAsset(path, resourcePath);
            }

            return heroList;
        }

        // ─── Level Generation ─────────────────────────────────────────────────────

        private static void GenerateLevels(List<HeroData> heroes)
        {
            var heroMap = new Dictionary<string, HeroData>();
            foreach (var h in heroes) heroMap[h.heroId] = h;

            // Chapter 1: The Ashfall — 5 levels introducing the Iron Legion vs Ash Cult conflict
            Level("ch1_l1", "The Burning Road", 1, 1,
                "The Ash Road is choked with smoke. Cultist scouts block the pass.",
                "The road is clear. Press on to the Iron Keep.",
                "The cultists hold the pass. The road to the Iron Keep remains closed.",
                LevelDifficulty.Easy, xp: 50,
                enemies: new[] { ("zeph_wildmane", 1) }, heroMap);

            Level("ch1_l2", "Cultist Ambush", 1, 2,
                "Two ash cultists spring from the shadows. Their fire burns with purpose.",
                "The ambush broken. The cultists scatter into the ash.",
                "Overwhelmed. Retreat to recover and try again.",
                LevelDifficulty.Easy, xp: 75,
                enemies: new[] { ("zeph_wildmane", 2), ("lyra_thornveil", 2) }, heroMap,
                required: "ch1_l1");

            Level("ch1_l3", "The Scorched Village", 1, 3,
                "A village burns. Iron Legion soldiers defend the last survivor cluster.",
                "The village is saved — for now. The cultists fall back.",
                "The village falls. Another mark on the Ash Cult's tally.",
                LevelDifficulty.Normal, xp: 100,
                enemies: new[] { ("seraphyn_ashveil", 3), ("zeph_wildmane", 3) }, heroMap,
                required: "ch1_l2");

            Level("ch1_l4", "Vorra's Challenge", 1, 4,
                "Vorra Steelborn blocks the mountain pass. Her hammer gleams in the ash light.",
                "Vorra falls. The pass to the cultist stronghold is open.",
                "Vorra holds the pass. Her reputation well-earned.",
                LevelDifficulty.Normal, xp: 125,
                enemies: new[] { ("vorra_steelborn", 5) }, heroMap,
                required: "ch1_l3");

            Level("ch1_l5", "Seraphyn's Pyre", 1, 5,
                "At the centre of the Ashfall stands Seraphyn Ashveil. She has been waiting.",
                "The pyre extinguished. Chapter one of the Ashfall ends here.",
                "The pyre grows. Seraphyn laughs as you retreat into the smoke.",
                LevelDifficulty.Hard, xp: 200, isBoss: true,
                enemies: new[] { ("seraphyn_ashveil", 8), ("mordoc_sundered", 7) }, heroMap,
                required: "ch1_l4");

            // Chapter 2: The Wild Hunt — 5 levels in the WildHunters' forest
            Level("ch2_l1", "Into the Deep Forest", 2, 1,
                "The forest is thick. Wild Hunters watch from the canopy.",
                "First patrol neutralised. Move deeper into the hunters' territory.",
                "The forest takes you back. Regroup at the forest edge.",
                LevelDifficulty.Easy, xp: 100,
                enemies: new[] { ("zeph_wildmane", 6) }, heroMap,
                required: "ch1_l5");

            Level("ch2_l2", "The Canopy Ambush", 2, 2,
                "Arrows from above. Lyra's scouts have been watching you for hours.",
                "The ambush neutralised. Lyra's scouts fall back.",
                "Pinned and outranged. The forest has too many eyes.",
                LevelDifficulty.Normal, xp: 125,
                enemies: new[] { ("lyra_thornveil", 7), ("zeph_wildmane", 7) }, heroMap,
                required: "ch2_l1");

            Level("ch2_l3", "Moonlit Glade", 2, 3,
                "A sacred grove. The Wild Hunters have fortified it with terrain traps.",
                "The glade is cleared. The Hunters regroup deeper in the forest.",
                "The glade holds. The Hunters celebrate another repelled incursion.",
                LevelDifficulty.Normal, xp: 150,
                enemies: new[] { ("lyra_thornveil", 8), ("aldric_stoneguard", 7) }, heroMap,
                required: "ch2_l2",
                terrain: new[] { (1, 2, TileType.HighGround), (5, 2, TileType.HighGround) });

            Level("ch2_l4", "Zeph's Rampage", 2, 4,
                "Zeph charges alone. He needs no backup, he says. He has a point.",
                "Zeph's rampage ends. The beast, briefly, is caged.",
                "Zeph howls in triumph. Run, before he comes back.",
                LevelDifficulty.Hard, xp: 175,
                enemies: new[] { ("zeph_wildmane", 12) }, heroMap,
                required: "ch2_l3");

            Level("ch2_l5", "Lyra's Last Stand", 2, 5,
                "Lyra takes to the High Ground. She will not abandon this forest.",
                "Lyra's bow falls silent. She acknowledges the better shot.",
                "Lyra holds the High Ground. The forest remains hers.",
                LevelDifficulty.Hard, xp: 225, isBoss: true,
                enemies: new[] { ("lyra_thornveil", 14), ("zeph_wildmane", 13) }, heroMap,
                required: "ch2_l4",
                terrain: new[] { (5, 1, TileType.HighGround), (5, 3, TileType.HighGround) });

            // Chapter 3: The Stone Sanctum — 5 levels confronting the healer faction
            Level("ch3_l1", "Pale Stones", 3, 1,
                "The Stone Sanctum's outer wards. Rune-carved sentinels patrol the perimeter.",
                "The outer wards fall silent. The sanctum's inner chambers lie ahead.",
                "The wards hold. The runes burn with warning.",
                LevelDifficulty.Normal, xp: 150,
                enemies: new[] { ("aldric_stoneguard", 10) }, heroMap,
                required: "ch2_l5");

            Level("ch3_l2", "Mira's Mirrors", 3, 2,
                "Mira of the Pale Stone has arranged her allies in layered defensive formations.",
                "The formation breaks. Mira retreats deeper into the sanctum.",
                "The formation holds. Mira smiles serenely as you withdraw.",
                LevelDifficulty.Normal, xp: 175,
                enemies: new[] { ("mira_pale_stone", 11), ("aldric_stoneguard", 11) }, heroMap,
                required: "ch3_l1");

            Level("ch3_l3", "The Sacred Hall", 3, 3,
                "The Sanctum's great hall. Ley lines hum beneath the floor — everyone feels their energy.",
                "The hall falls. Ancient power dims.",
                "The hall holds. Ley lines surge as your forces retreat.",
                LevelDifficulty.Hard, xp: 200,
                enemies: new[] { ("mira_pale_stone", 12), ("aldric_stoneguard", 12) }, heroMap,
                required: "ch3_l2",
                terrain: new[] { (2, 2, TileType.ArcaneLayLine), (4, 2, TileType.ArcaneLayLine) });

            Level("ch3_l4", "Aldric's Judgment", 3, 4,
                "Aldric Stoneguard stands alone before the inner sanctum. His runes are fully awakened.",
                "The guardian falls. The inner sanctum opens.",
                "Aldric endures. The sanctum is beyond reach for now.",
                LevelDifficulty.Hard, xp: 225,
                enemies: new[] { ("aldric_stoneguard", 15) }, heroMap,
                required: "ch3_l3");

            Level("ch3_l5", "The Pale Stone's Heart", 3, 5,
                "Both Mira and Aldric make their final stand. The sanctum's ley lines pulse with terrifying strength.",
                "The Pale Stone's Heart is stilled. Chapter three ends.",
                "The Heart pulses on. The Sanctum is unbroken.",
                LevelDifficulty.Hard, xp: 300, isBoss: true,
                enemies: new[] { ("aldric_stoneguard", 18), ("mira_pale_stone", 17) }, heroMap,
                required: "ch3_l4",
                terrain: new[] { (3, 1, TileType.ArcaneLayLine), (3, 3, TileType.ArcaneLayLine) });

            // Chapter 4: The Void — 5 levels facing the Void Reapers
            Level("ch4_l1", "Through the Rift", 4, 1,
                "A dimensional rift tears reality. Void Reapers slip through it like shadows.",
                "The first wave repelled. The rift crackles but holds.",
                "The rift swallows your advance. Fall back.",
                LevelDifficulty.Hard, xp: 200,
                enemies: new[] { ("skaros_nightfall", 15) }, heroMap,
                required: "ch3_l5");

            Level("ch4_l2", "The Dark Vanguard", 4, 2,
                "Skaros and Vex have formed a lethal pairing. Neither can be seen until it is too late.",
                "The pairing broken. Skaros and Vex retreat into the dark.",
                "The vanguard overwhelms. Void energy floods the battlefield.",
                LevelDifficulty.Hard, xp: 250,
                enemies: new[] { ("skaros_nightfall", 16), ("vex_unbound", 15) }, heroMap,
                required: "ch4_l1",
                terrain: new[] { (4, 2, TileType.ShadowVeil) });

            Level("ch4_l3", "Dimensional Crossroads", 4, 3,
                "Reality folds here. Attacks miss. Space distorts. The Reapers thrive in this confusion.",
                "The crossroads stabilise. A brief respite.",
                "The crossroads consume your squad. The void expands.",
                LevelDifficulty.Elite, xp: 300,
                enemies: new[] { ("vex_unbound", 18), ("skaros_nightfall", 17) }, heroMap,
                required: "ch4_l2",
                terrain: new[] { (3, 1, TileType.ShadowVeil), (3, 2, TileType.ShadowVeil), (3, 3, TileType.ShadowVeil) });

            Level("ch4_l4", "Vex Unchained", 4, 4,
                "Vex has shed every remaining anchor to this reality. He is pure chaos.",
                "Vex is bound. The void shrinks slightly.",
                "Vex laughs as you scatter. He is beyond your reach.",
                LevelDifficulty.Elite, xp: 325,
                enemies: new[] { ("vex_unbound", 22) }, heroMap,
                required: "ch4_l3");

            Level("ch4_l5", "The Ashen Throne", 4, 5,
                "At the heart of the void stands the Ashen Throne itself. Both Skaros and Vex defend it. Whoever sits the Throne decides the fate of the realm.",
                "The Throne is yours. Chapter four — and the campaign's first arc — ends. But the Throne's power has only begun to stir.",
                "The Throne is defended. The void claims this realm.",
                LevelDifficulty.Boss, xp: 500, isBoss: true,
                enemies: new[] { ("vex_unbound", 25), ("skaros_nightfall", 24) }, heroMap,
                required: "ch4_l4",
                terrain: new[] { (3, 0, TileType.ShadowVeil), (3, 4, TileType.ShadowVeil), (4, 2, TileType.Fire) });
        }

        // ─── Factory Helpers ──────────────────────────────────────────────────────

        private static AbilityCardData Card(
            string id, string name, string desc,
            CardType type, CardTargetType targetType, AbilityElement element,
            int cost, float baseVal, float mult, StatType stat,
            StatusEffectType status = StatusEffectType.None, int duration = 0, float procChance = 1f,
            ComboTag combo = ComboTag.None, ComboTag requires = ComboTag.None, float comboMult = 1f,
            int splashRadius = 0)
        {
            var card = ScriptableObject.CreateInstance<AbilityCardData>();
            card.cardId = id;
            card.displayName = name;
            card.effectDescription = desc;
            card.cardType = type;
            card.targetType = targetType;
            card.element = element;
            card.energyCost = cost;
            card.baseEffectValue = baseVal;
            card.statMultiplier = mult;
            card.scalingStat = stat;
            card.applyStatusEffect = status;
            card.statusDuration = duration;
            card.statusProcChance = procChance;
            card.outputsComboTag = combo;
            card.requiresComboTag = requires;
            card.comboBonusMultiplier = comboMult > 1f ? comboMult : 1f;
            card.splashRadius = splashRadius;
            card.requiredHeroStarTier = 1;
            return card;
        }

        private static HeroData Hero(
            string id, string name, string lore,
            HeroRarity rarity, HeroRole role, HeroRole secondary,
            HeroFaction faction, CombatRow row,
            int hp, int atk, int def, int spd, float crit,
            int shardsUnlock,
            string[] cards,
            Dictionary<string, AbilityCardData> allCards)
        {
            var hero = ScriptableObject.CreateInstance<HeroData>();
            hero.heroId = id;
            hero.displayName = name;
            hero.loreDescription = lore;
            hero.rarity = rarity;
            hero.primaryRole = role;
            hero.secondaryRole = secondary;
            hero.faction = faction;
            hero.preferredRow = row;
            hero.baseHealth = hp;
            hero.baseAttack = atk;
            hero.baseDefense = def;
            hero.baseSpeed = spd;
            hero.baseCritChance = crit;
            hero.shardsToUnlock = shardsUnlock;
            hero.shardsPerStarTier = new[] { 20, 40, 60, 80, 100 };
            hero.abilityPool = new System.Collections.Generic.List<AbilityCardData>();
            foreach (var cardId in cards)
            {
                if (allCards.TryGetValue(cardId, out AbilityCardData card))
                    hero.abilityPool.Add(card);
            }
            return hero;
        }

        private static void Level(
            string id, string displayName, int chapter, int levelNum,
            string opening, string victory, string defeat,
            LevelDifficulty difficulty, int xp,
            (string heroId, int level)[] enemies,
            Dictionary<string, HeroData> heroMap,
            string required = null,
            (int col, int row, TileType type)[] terrain = null,
            bool isBoss = false)
        {
            var level = ScriptableObject.CreateInstance<PveLevelData>();
            level.levelId = id;
            level.displayName = displayName;
            level.chapterNumber = chapter;
            level.levelNumber = levelNum;
            level.openingNarrative = opening;
            level.victoryNarrative = victory;
            level.defeatNarrative = defeat;
            level.difficulty = difficulty;
            level.xpReward = xp;
            level.isBossLevel = isBoss;
            level.requiredLevelId = required ?? string.Empty;
            level.requiredStrongholdLevel = 1;

            level.enemies = new System.Collections.Generic.List<EnemyEntry>();
            foreach (var (heroId, lvl) in enemies)
            {
                if (heroMap.TryGetValue(heroId, out HeroData heroData))
                {
                    level.enemies.Add(new EnemyEntry
                    {
                        heroData = heroData,
                        level = lvl,
                        rowOverride = heroData.preferredRow
                    });
                }
            }

            level.terrainPresets = new System.Collections.Generic.List<TerrainPreset>();
            if (terrain != null)
            {
                foreach (var (col, row, type) in terrain)
                    level.terrainPresets.Add(new TerrainPreset { column = col, row = row, tileType = type });
            }

            string path = $"{LevelsPath}/Level_{id}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(level, path);
        }

        private static void EnsureDirectories()
        {
            foreach (var dir in new[] { HeroesPath, CardsPath, LevelsPath, ResourcesPath })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir);
                    string folderName = Path.GetFileName(dir);
                    AssetDatabase.CreateFolder(parent, folderName);
                }
            }
        }
    }
}
#endif
