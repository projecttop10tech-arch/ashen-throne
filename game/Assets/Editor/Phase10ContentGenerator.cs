#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class Phase10ContentGenerator
    {
        private const string DataRoot = "Assets/Data";
        private const string ResourcesRoot = "Assets/Resources";

        [MenuItem("AshenThrone/Phase 10/Generate All Content")]
        public static void GenerateAll()
        {
            GenerateBattlePassSeason1();
            GenerateGachaPool();
            GenerateExpandedLocalization();
            GenerateBalanceSheets();
            TuneQuestRewards();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase10] All content generated.");
        }

        // ---------------------------------------------------------------
        // 10.6: Battle Pass Season 1
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 10/Battle Pass Season 1")]
        public static void GenerateBattlePassSeason1()
        {
            var dir = $"{DataRoot}/BattlePass";
            EnsureDir(dir);

            var season = ScriptableObject.CreateInstance<Economy.BattlePassSeason>();
            season.SeasonId = "season_1";
            season.SeasonName = "Ashes of Dawn";
            season.StartDate = new DateTime(2026, 4, 1);
            season.EndDate = new DateTime(2026, 5, 31);

            // Points per tier: starts at 100, increases by 20 per tier
            season.PointsPerTier = new int[50];
            for (int i = 0; i < 50; i++)
                season.PointsPerTier[i] = 100 + i * 20;

            // Free rewards: resources, XP, hero shards, speedups
            season.FreeRewards = new Economy.BattlePassReward[50];
            for (int i = 0; i < 50; i++)
            {
                var reward = new Economy.BattlePassReward();
                int tier = i + 1;
                reward.IsCombatPowerReward = false;

                if (tier % 10 == 0) // Milestone tiers: hero shards
                {
                    reward.RewardId = $"bp_free_{tier}_shards";
                    reward.RewardType = Economy.BattlePassRewardType.HeroShard;
                    reward.ItemId = "random_hero";
                    reward.Quantity = 5 + tier / 10;
                    reward.DisplayName = $"{reward.Quantity} Hero Shards";
                }
                else if (tier % 5 == 0) // Every 5th: XP potion
                {
                    reward.RewardId = $"bp_free_{tier}_xp";
                    reward.RewardType = Economy.BattlePassRewardType.ExperiencePotion;
                    reward.Quantity = 2 + tier / 10;
                    reward.DisplayName = $"{reward.Quantity} XP Potions";
                }
                else if (tier % 3 == 0) // Every 3rd: speedup
                {
                    reward.RewardId = $"bp_free_{tier}_speedup";
                    reward.RewardType = Economy.BattlePassRewardType.SpeedupBundle;
                    reward.Quantity = 5 + tier / 5;
                    reward.DisplayName = $"{reward.Quantity}min Speedup";
                }
                else // Default: resource bundle
                {
                    reward.RewardId = $"bp_free_{tier}_resources";
                    reward.RewardType = Economy.BattlePassRewardType.ResourceBundle;
                    reward.Quantity = 200 + tier * 50;
                    reward.DisplayName = $"{reward.Quantity} Resources";
                }
                season.FreeRewards[i] = reward;
            }

            // Premium rewards: cosmetics and QoL ONLY (zero combat power)
            season.PremiumRewards = new Economy.BattlePassReward[50];
            var cosmeticTypes = new[]
            {
                Economy.BattlePassRewardType.HeroSkin,
                Economy.BattlePassRewardType.CityDecoration,
                Economy.BattlePassRewardType.Emote,
                Economy.BattlePassRewardType.Banner,
                Economy.BattlePassRewardType.GachaTicket,
            };
            var cosmeticNames = new[] { "Hero Skin", "City Decoration", "Emote", "Banner", "Gacha Ticket" };

            for (int i = 0; i < 50; i++)
            {
                var reward = new Economy.BattlePassReward();
                int tier = i + 1;
                reward.IsCombatPowerReward = false; // CRITICAL: always false

                if (tier == 50) // Final tier: legendary skin
                {
                    reward.RewardId = "bp_prem_50_legendary_skin";
                    reward.RewardType = Economy.BattlePassRewardType.HeroSkin;
                    reward.ItemId = "skin_ashwalker_phoenix";
                    reward.Quantity = 1;
                    reward.DisplayName = "Legendary: Phoenix Ashwalker Skin";
                }
                else if (tier % 10 == 0) // Milestone: epic cosmetics
                {
                    reward.RewardId = $"bp_prem_{tier}_epic";
                    reward.RewardType = Economy.BattlePassRewardType.HeroSkin;
                    reward.ItemId = $"skin_epic_{tier / 10}";
                    reward.Quantity = 1;
                    reward.DisplayName = $"Epic Skin #{tier / 10}";
                }
                else
                {
                    int typeIdx = i % cosmeticTypes.Length;
                    reward.RewardId = $"bp_prem_{tier}";
                    reward.RewardType = cosmeticTypes[typeIdx];
                    reward.ItemId = $"cosmetic_{tier}";
                    reward.Quantity = reward.RewardType == Economy.BattlePassRewardType.GachaTicket ? 3 : 1;
                    reward.DisplayName = cosmeticNames[typeIdx];
                }
                season.PremiumRewards[i] = reward;
            }

            AssetDatabase.CreateAsset(season, $"{dir}/BattlePassSeason_1.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase10] Created Battle Pass Season 1 (50 tiers, zero P2W in premium track).");
        }

        // ---------------------------------------------------------------
        // 10.7: Gacha Pool — 40 cosmetic items
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 10/Gacha Pool Config")]
        public static void GenerateGachaPool()
        {
            var dir = $"{ResourcesRoot}";
            EnsureDir(dir);

            var gachaConfig = ScriptableObject.CreateInstance<GachaPoolConfig>();
            gachaConfig.items = new List<GachaPoolConfig.GachaItemDef>();

            // 15 Common (weight 6000 total → 400 each)
            var commonItems = new[]
            {
                ("emote_wave", "Wave Emote"), ("emote_cheer", "Cheer Emote"),
                ("emote_taunt", "Taunt Emote"), ("emote_dance", "Dance Emote"),
                ("emote_salute", "Salute Emote"),
                ("frame_bronze_1", "Bronze Frame I"), ("frame_bronze_2", "Bronze Frame II"),
                ("frame_bronze_3", "Bronze Frame III"),
                ("bubble_plain", "Plain Chat Bubble"), ("bubble_dark", "Dark Chat Bubble"),
                ("banner_basic_1", "Basic Banner I"), ("banner_basic_2", "Basic Banner II"),
                ("banner_basic_3", "Basic Banner III"),
                ("city_flag_1", "City Flag I"), ("city_flag_2", "City Flag II"),
            };
            foreach (var (id, name) in commonItems)
                gachaConfig.items.Add(new GachaPoolConfig.GachaItemDef
                    { itemId = id, displayName = name, rarity = 0, weight = 400 });

            // 12 Rare (weight 3000 total → 250 each)
            var rareItems = new[]
            {
                ("emote_fire_breath", "Fire Breath Emote"), ("emote_shadow_cloak", "Shadow Cloak Emote"),
                ("frame_silver_1", "Silver Frame I"), ("frame_silver_2", "Silver Frame II"),
                ("bubble_ornate", "Ornate Chat Bubble"), ("bubble_flame", "Flame Chat Bubble"),
                ("banner_faction_iron", "Iron Legion Banner"), ("banner_faction_ash", "Ash Cult Banner"),
                ("city_torch_1", "City Torches"), ("city_garden_1", "Dark Garden"),
                ("skin_kaelen_iron", "Kaelen: Iron Guard"), ("skin_lyra_forest", "Lyra: Forest Shade"),
            };
            foreach (var (id, name) in rareItems)
                gachaConfig.items.Add(new GachaPoolConfig.GachaItemDef
                    { itemId = id, displayName = name, rarity = 1, weight = 250 });

            // 8 Epic (weight 750 total → ~94 each)
            var epicItems = new[]
            {
                ("frame_gold_1", "Gold Frame"), ("frame_gold_2", "Platinum Frame"),
                ("emote_void_rift", "Void Rift Emote"), ("emote_thunder_call", "Thunder Call Emote"),
                ("skin_mira_ice", "Mira: Frostfire"), ("skin_vex_night", "Vex: Nightblade"),
                ("city_statue_hero", "Hero Statue"), ("banner_legendary_fire", "Legendary Fire Banner"),
            };
            foreach (var (id, name) in epicItems)
                gachaConfig.items.Add(new GachaPoolConfig.GachaItemDef
                    { itemId = id, displayName = name, rarity = 2, weight = 94 });

            // 5 Legendary (weight 250 total → 50 each)
            var legendaryItems = new[]
            {
                ("skin_sera_divine", "Sera: Divine Radiance"),
                ("skin_grim_reaper", "Grim: Death Walker"),
                ("frame_mythic", "Mythic Frame"),
                ("city_throne_room", "Ashen Throne Room"),
                ("emote_ascension", "Ascension Emote"),
            };
            foreach (var (id, name) in legendaryItems)
                gachaConfig.items.Add(new GachaPoolConfig.GachaItemDef
                    { itemId = id, displayName = name, rarity = 3, weight = 50 });

            AssetDatabase.CreateAsset(gachaConfig, $"{dir}/GachaPoolConfig.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[Phase10] Created gacha pool: 40 cosmetic items (15 common, 12 rare, 8 epic, 5 legendary). Zero heroes.");
        }

        // ---------------------------------------------------------------
        // 10.5: Expanded Localization (~400 keys)
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 10/Expand Localization")]
        public static void GenerateExpandedLocalization()
        {
            var locDir = Path.Combine(Application.dataPath, "StreamingAssets", "Localization");
            Directory.CreateDirectory(locDir);

            var keys = BuildLocalizationKeys();
            var languages = new[] { "en", "es", "fr", "de", "pt", "ja", "ko", "zh" };

            foreach (var lang in languages)
            {
                var filePath = Path.Combine(locDir, $"{lang}.json");
                var sb = new StringBuilder();
                sb.AppendLine("{");
                int count = 0;
                foreach (var (key, enValue) in keys)
                {
                    count++;
                    string value = lang == "en" ? enValue : $"[{lang.ToUpper()}] {enValue}";
                    string comma = count < keys.Count ? "," : "";
                    sb.AppendLine($"  \"{key}\": \"{EscapeJson(value)}\"{comma}");
                }
                sb.AppendLine("}");
                File.WriteAllText(filePath, sb.ToString());
            }

            Debug.Log($"[Phase10] Expanded localization: {keys.Count} keys across {languages.Length} languages.");
        }

        private static List<(string key, string enValue)> BuildLocalizationKeys()
        {
            var keys = new List<(string, string)>();

            // Core UI
            keys.Add(("ui.menu.play", "Play"));
            keys.Add(("ui.menu.settings", "Settings"));
            keys.Add(("ui.menu.quit", "Quit"));
            keys.Add(("ui.common.confirm", "Confirm"));
            keys.Add(("ui.common.cancel", "Cancel"));
            keys.Add(("ui.common.ok", "OK"));
            keys.Add(("ui.common.back", "Back"));
            keys.Add(("ui.common.loading", "Loading..."));
            keys.Add(("ui.common.error", "Error"));
            keys.Add(("ui.common.retry", "Retry"));
            keys.Add(("ui.common.close", "Close"));
            keys.Add(("ui.common.max", "MAX"));
            keys.Add(("ui.common.locked", "Locked"));
            keys.Add(("ui.common.claim", "Claim"));
            keys.Add(("ui.common.collect", "Collect"));

            // Combat
            keys.Add(("ui.combat.energy", "Energy"));
            keys.Add(("ui.combat.turn", "Turn {0}"));
            keys.Add(("ui.combat.victory", "Victory!"));
            keys.Add(("ui.combat.defeat", "Defeat"));
            keys.Add(("ui.combat.draw", "Draw"));
            keys.Add(("ui.combat.draw_phase", "Draw Phase"));
            keys.Add(("ui.combat.action_phase", "Action Phase"));
            keys.Add(("ui.combat.resolve_phase", "Resolving..."));
            keys.Add(("ui.combat.your_turn", "Your Turn"));
            keys.Add(("ui.combat.enemy_turn", "Enemy Turn"));
            keys.Add(("ui.combat.critical_hit", "Critical Hit!"));
            keys.Add(("ui.combat.miss", "Miss"));
            keys.Add(("ui.combat.no_energy", "Not enough energy"));
            keys.Add(("ui.combat.deck_empty", "Deck empty"));
            keys.Add(("ui.combat.hand_full", "Hand full"));

            // Empire
            keys.Add(("ui.empire.resources", "Resources"));
            keys.Add(("ui.empire.buildings", "Buildings"));
            keys.Add(("ui.empire.research", "Research"));
            keys.Add(("ui.empire.build_queue", "Build Queue"));
            keys.Add(("ui.empire.upgrade", "Upgrade"));
            keys.Add(("ui.empire.build_time", "Build Time: {0}"));
            keys.Add(("ui.empire.cost", "Cost"));
            keys.Add(("ui.empire.production", "Production: {0}/hr"));
            keys.Add(("ui.empire.max_level", "Max Level"));
            keys.Add(("ui.empire.insufficient_resources", "Insufficient Resources"));
            keys.Add(("ui.empire.queue_full", "Build queue full"));
            keys.Add(("ui.empire.building_complete", "Building Complete!"));

            // Alliance
            keys.Add(("ui.alliance.chat", "Alliance Chat"));
            keys.Add(("ui.alliance.war", "War"));
            keys.Add(("ui.alliance.territory", "Territory"));
            keys.Add(("ui.alliance.members", "Members"));
            keys.Add(("ui.alliance.leaderboard", "Leaderboard"));
            keys.Add(("ui.alliance.join", "Join Alliance"));
            keys.Add(("ui.alliance.create", "Create Alliance"));
            keys.Add(("ui.alliance.leave", "Leave Alliance"));
            keys.Add(("ui.alliance.rally", "Rally Attack"));
            keys.Add(("ui.alliance.donate", "Donate"));
            keys.Add(("ui.alliance.war_starting", "War starting in {0}"));
            keys.Add(("ui.alliance.territory_captured", "Territory captured!"));

            // Heroes
            keys.Add(("ui.heroes.roster", "Hero Roster"));
            keys.Add(("ui.heroes.shards", "Shards: {0}/{1}"));
            keys.Add(("ui.heroes.summon", "Summon"));
            keys.Add(("ui.heroes.level_up", "Level Up"));
            keys.Add(("ui.heroes.max_level", "Max Level Reached"));
            keys.Add(("ui.heroes.star_up", "Star Up"));
            keys.Add(("ui.heroes.locked", "Locked — Need {0} shards"));
            keys.Add(("ui.heroes.equip", "Equip"));
            keys.Add(("ui.heroes.stats", "Stats"));
            keys.Add(("ui.heroes.abilities", "Abilities"));

            // Economy
            keys.Add(("ui.economy.battle_pass", "Battle Pass"));
            keys.Add(("ui.economy.shop", "Shop"));
            keys.Add(("ui.economy.quests", "Quests"));
            keys.Add(("ui.economy.daily_quests", "Daily Quests"));
            keys.Add(("ui.economy.weekly_quests", "Weekly Quests"));
            keys.Add(("ui.economy.premium_pass", "Premium Pass"));
            keys.Add(("ui.economy.tier", "Tier {0}"));
            keys.Add(("ui.economy.free_track", "Free Track"));
            keys.Add(("ui.economy.premium_track", "Premium Track"));
            keys.Add(("ui.economy.gacha", "Cosmetic Summon"));
            keys.Add(("ui.economy.gacha_pity", "Pity: {0}/50"));
            keys.Add(("ui.economy.purchase", "Purchase"));
            keys.Add(("ui.economy.gems", "Gems"));
            keys.Add(("ui.economy.restore_purchases", "Restore Purchases"));

            // Settings
            keys.Add(("ui.settings.language", "Language"));
            keys.Add(("ui.settings.sound", "Sound Effects"));
            keys.Add(("ui.settings.music", "Music"));
            keys.Add(("ui.settings.notifications", "Notifications"));
            keys.Add(("ui.settings.graphics", "Graphics Quality"));
            keys.Add(("ui.settings.colorblind", "Colorblind Mode"));
            keys.Add(("ui.settings.colorblind.off", "Off"));
            keys.Add(("ui.settings.colorblind.protanopia", "Protanopia"));
            keys.Add(("ui.settings.colorblind.deuteranopia", "Deuteranopia"));
            keys.Add(("ui.settings.colorblind.tritanopia", "Tritanopia"));
            keys.Add(("ui.settings.haptics", "Haptic Feedback"));
            keys.Add(("ui.settings.account", "Account"));
            keys.Add(("ui.settings.privacy", "Privacy Policy"));
            keys.Add(("ui.settings.tos", "Terms of Service"));
            keys.Add(("ui.settings.support", "Customer Support"));
            keys.Add(("ui.settings.version", "Version {0}"));

            // Tutorial
            keys.Add(("tutorial.welcome", "Welcome to Ashen Throne!"));
            keys.Add(("tutorial.first_build", "Tap a building slot to construct your first building."));
            keys.Add(("tutorial.first_battle", "Let's begin your first battle!"));
            keys.Add(("tutorial.draw_cards", "Draw cards at the start of each turn."));
            keys.Add(("tutorial.play_card", "Tap a card, then tap a target to play it."));
            keys.Add(("tutorial.energy", "Cards cost energy. You regenerate {0} per turn."));
            keys.Add(("tutorial.hero_summon", "You've earned enough shards! Summon your new hero."));
            keys.Add(("tutorial.complete", "Tutorial complete! The throne awaits."));

            // World Map
            keys.Add(("ui.worldmap.territories", "Territories"));
            keys.Add(("ui.worldmap.capture", "Capture Territory"));
            keys.Add(("ui.worldmap.defend", "Defend"));
            keys.Add(("ui.worldmap.neutral", "Neutral Territory"));
            keys.Add(("ui.worldmap.supply_line", "Supply Line"));
            keys.Add(("ui.worldmap.war_window", "War Window"));

            // Hero names and descriptions
            var heroes = new[]
            {
                ("hero.lyra_thornveil", "Lyra Thornveil", "A nature mage who weaves vines and thorns to protect allies."),
                ("hero.kael_ashwalker", "Kael Ashwalker", "A fire sorcerer who channels the fury of the Ashen Wastes."),
                ("hero.thane_ironhold", "Thane Ironhold", "An armored defender of the Iron Legion, unbreakable in battle."),
                ("hero.zara_voidweaver", "Zara Voidweaver", "A void mystic who bends shadow to confuse and weaken foes."),
                ("hero.rowan_stoneward", "Rowan Stoneward", "A steadfast guardian with the strength of living stone."),
                ("hero.mira_frostbane", "Mira Frostbane", "An ice mage whose frost slows all who oppose her."),
                ("hero.vex_shadowstrike", "Vex Shadowstrike", "A shadow assassin who strikes from the darkness."),
                ("hero.sera_dawnblade", "Sera Dawnblade", "A holy knight radiating divine light to heal and smite."),
                ("hero.grim_bonecrusher", "Grim Bonecrusher", "A berserker who grows stronger as battle rages."),
                ("hero.nyx_stormcaller", "Nyx Stormcaller", "A lightning mage who chains destruction across the field."),
            };
            foreach (var (baseKey, name, desc) in heroes)
            {
                keys.Add(($"{baseKey}.name", name));
                keys.Add(($"{baseKey}.desc", desc));
            }

            // Building names and descriptions
            var buildings = new[]
            {
                ("building.stronghold", "Stronghold", "The heart of your empire. Upgrade to unlock new districts."),
                ("building.barracks", "Barracks", "Train soldiers to bolster your military might."),
                ("building.training_ground", "Training Ground", "Sharpen your heroes' combat skills."),
                ("building.watch_tower", "Watch Tower", "Provides early warning of incoming attacks."),
                ("building.wall", "Wall", "Stone fortifications protecting your city."),
                ("building.armory", "Armory", "Stores weapons and armor for your troops."),
                ("building.stone_quarry", "Stone Quarry", "Extracts stone from the mountainside."),
                ("building.iron_mine", "Iron Mine", "Mines iron ore from deep underground."),
                ("building.grain_farm", "Grain Farm", "Cultivates grain to feed your people."),
                ("building.arcane_tower", "Arcane Tower", "Harnesses magical essence from the ley lines."),
                ("building.marketplace", "Marketplace", "Trade goods to boost your economy."),
                ("building.academy", "Academy", "Research technologies to advance your empire."),
                ("building.library", "Library", "Ancient knowledge speeds research."),
                ("building.laboratory", "Laboratory", "Experimental research for advanced upgrades."),
                ("building.observatory", "Observatory", "Study the stars for strategic advantage."),
                ("building.archive", "Archive", "Preserves scrolls of forgotten lore."),
                ("building.hero_shrine", "Hero Shrine", "Honor fallen heroes to gain their blessing."),
                ("building.guild_hall", "Guild Hall", "Coordinate with allies for joint operations."),
                ("building.enchanting_tower", "Enchanting Tower", "Imbue equipment with magical properties."),
                ("building.forge", "Forge", "Craft weapons and armor from raw materials."),
                ("building.embassy", "Embassy", "Establish diplomatic ties with other alliances."),
            };
            foreach (var (baseKey, name, desc) in buildings)
            {
                keys.Add(($"{baseKey}.name", name));
                keys.Add(($"{baseKey}.desc", desc));
            }

            // Resource names
            keys.Add(("resource.stone", "Stone"));
            keys.Add(("resource.iron", "Iron"));
            keys.Add(("resource.grain", "Grain"));
            keys.Add(("resource.arcane", "Arcane Essence"));
            keys.Add(("resource.gold", "Gold"));
            keys.Add(("resource.gems", "Gems"));

            // Quest descriptions
            keys.Add(("quest.daily.collect_resources", "Collect {0} resources"));
            keys.Add(("quest.daily.complete_battles", "Complete {0} battles"));
            keys.Add(("quest.daily.upgrade_building", "Upgrade a building"));
            keys.Add(("quest.daily.use_cards", "Play {0} ability cards"));
            keys.Add(("quest.daily.alliance_chat", "Send a message in alliance chat"));
            keys.Add(("quest.daily.login", "Log in today"));
            keys.Add(("quest.weekly.win_battles", "Win {0} battles"));
            keys.Add(("quest.weekly.research", "Complete a research"));
            keys.Add(("quest.weekly.level_hero", "Level up a hero"));
            keys.Add(("quest.weekly.pvp", "Complete {0} PvP battles"));
            keys.Add(("quest.weekly.rally", "Participate in a rally"));
            keys.Add(("quest.onetime.first_build", "Construct your first building"));
            keys.Add(("quest.onetime.first_hero", "Summon your first hero"));
            keys.Add(("quest.onetime.join_alliance", "Join an alliance"));
            keys.Add(("quest.onetime.first_research", "Complete your first research"));
            keys.Add(("quest.onetime.chapter_1", "Complete Chapter 1"));

            // Status effects
            keys.Add(("status.burn", "Burning"));
            keys.Add(("status.freeze", "Frozen"));
            keys.Add(("status.poison", "Poisoned"));
            keys.Add(("status.shield", "Shielded"));
            keys.Add(("status.stun", "Stunned"));
            keys.Add(("status.slow", "Slowed"));
            keys.Add(("status.enraged", "Enraged"));
            keys.Add(("status.marked", "Marked"));
            keys.Add(("status.heal_over_time", "Regenerating"));

            // Error messages
            keys.Add(("error.network", "Network error. Please check your connection."));
            keys.Add(("error.auth_failed", "Authentication failed. Please try again."));
            keys.Add(("error.purchase_failed", "Purchase failed: {0}"));
            keys.Add(("error.server", "Server error. Please try again later."));
            keys.Add(("error.session_expired", "Session expired. Please log in again."));

            // Notifications
            keys.Add(("notif.building_complete", "Your {0} is ready!"));
            keys.Add(("notif.war_starting", "Alliance War starts in 30 minutes!"));
            keys.Add(("notif.daily_reset", "Daily quests have reset!"));
            keys.Add(("notif.battle_pass_expiring", "Battle Pass ends in {0} days"));
            keys.Add(("notif.come_back", "Your troops miss you! Come back and claim resources."));

            // Events
            keys.Add(("event.void_rift", "Void Rift"));
            keys.Add(("event.void_rift.desc", "Dark portals have opened across the land. Defeat the void creatures!"));
            keys.Add(("event.world_boss", "World Boss"));
            keys.Add(("event.world_boss.desc", "A mighty beast threatens the realm. Rally your alliance!"));
            keys.Add(("event.alliance_tournament", "Alliance Tournament"));
            keys.Add(("event.alliance_tournament.desc", "Compete against other alliances for glory and rewards!"));
            keys.Add(("event.launch_celebration", "Launch Celebration"));
            keys.Add(("event.launch_celebration.desc", "Celebrate the launch of Ashen Throne with bonus rewards!"));

            return keys;
        }

        // ---------------------------------------------------------------
        // 10.2: Tune Quest Rewards
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 10/Tune Quest Rewards")]
        public static void TuneQuestRewards()
        {
            var questGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{DataRoot}/Quests" });
            int tuned = 0;

            foreach (var guid in questGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var questSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (questSO == null) continue;

                var so = new SerializedObject(questSO);

                // Try to set battle pass points based on cadence
                var cadenceProp = so.FindProperty("<Cadence>k__BackingField");
                if (cadenceProp == null) cadenceProp = so.FindProperty("cadence");

                var bpPointsProp = so.FindProperty("<BattlePassPoints>k__BackingField");
                if (bpPointsProp == null) bpPointsProp = so.FindProperty("battlePassPoints");

                var goldProp = so.FindProperty("<GoldReward>k__BackingField");
                if (goldProp == null) goldProp = so.FindProperty("goldReward");

                if (cadenceProp != null && bpPointsProp != null)
                {
                    int cadence = cadenceProp.intValue;
                    // Daily: 100 BP points, Weekly: 250, One-time: 500
                    bpPointsProp.intValue = cadence == 0 ? 100 : cadence == 1 ? 250 : 500;
                }

                if (goldProp != null)
                {
                    int cadence = cadenceProp?.intValue ?? 0;
                    goldProp.intValue = cadence == 0 ? 50 : cadence == 1 ? 200 : 500;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                tuned++;
            }

            // Also tune quests in Resources/Quests/
            var resQuestGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{ResourcesRoot}/Quests" });
            foreach (var guid in resQuestGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var questSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (questSO == null) continue;

                var so = new SerializedObject(questSO);
                var cadenceProp = so.FindProperty("<Cadence>k__BackingField");
                if (cadenceProp == null) cadenceProp = so.FindProperty("cadence");
                var bpPointsProp = so.FindProperty("<BattlePassPoints>k__BackingField");
                if (bpPointsProp == null) bpPointsProp = so.FindProperty("battlePassPoints");

                if (cadenceProp != null && bpPointsProp != null)
                {
                    int cadence = cadenceProp.intValue;
                    bpPointsProp.intValue = cadence == 0 ? 100 : cadence == 1 ? 250 : 500;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                tuned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase10] Tuned rewards for {tuned} quest definitions.");
        }

        // ---------------------------------------------------------------
        // 10.8: Balance Sheets
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 10/Generate Balance Sheets")]
        public static void GenerateBalanceSheets()
        {
            var toolsDir = Path.Combine(Application.dataPath, "..", "..", "tools", "BalanceSheets");
            Directory.CreateDirectory(toolsDir);

            GenerateBuildingBalanceSheet(toolsDir);
            GenerateHeroBalanceSheet(toolsDir);
            GenerateEconomyFlowSheet(toolsDir);

            Debug.Log("[Phase10] Generated 3 balance sheet CSVs in tools/BalanceSheets/.");
        }

        private static void GenerateBuildingBalanceSheet(string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Building,Tier,StoneCost,IronCost,GrainCost,ArcaneCost,BuildTimeSec,ProductionPerHr,Bonus%");

            var buildingGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{DataRoot}/Buildings" });
            foreach (var guid in buildingGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var buildingSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (buildingSO == null) continue;

                var so = new SerializedObject(buildingSO);
                var idProp = so.FindProperty("<BuildingId>k__BackingField");
                if (idProp == null) idProp = so.FindProperty("buildingId");
                string buildingId = idProp?.stringValue ?? buildingSO.name;

                var tiersProp = so.FindProperty("<Tiers>k__BackingField");
                if (tiersProp == null) tiersProp = so.FindProperty("tiers");

                if (tiersProp != null && tiersProp.isArray)
                {
                    for (int i = 0; i < tiersProp.arraySize && i < 10; i++)
                    {
                        var tier = tiersProp.GetArrayElementAtIndex(i);
                        int stone = 0, iron = 0, grain = 0, arcane = 0, time = 0, prod = 0;
                        float bonus = 0;

                        var stoneProp = tier.FindPropertyRelative("<StoneCost>k__BackingField");
                        if (stoneProp == null) stoneProp = tier.FindPropertyRelative("stoneCost");
                        if (stoneProp != null) stone = stoneProp.intValue;

                        var ironProp = tier.FindPropertyRelative("<IronCost>k__BackingField");
                        if (ironProp == null) ironProp = tier.FindPropertyRelative("ironCost");
                        if (ironProp != null) iron = ironProp.intValue;

                        var grainProp = tier.FindPropertyRelative("<GrainCost>k__BackingField");
                        if (grainProp == null) grainProp = tier.FindPropertyRelative("grainCost");
                        if (grainProp != null) grain = grainProp.intValue;

                        var arcaneProp = tier.FindPropertyRelative("<ArcaneCost>k__BackingField");
                        if (arcaneProp == null) arcaneProp = tier.FindPropertyRelative("arcaneCost");
                        if (arcaneProp != null) arcane = arcaneProp.intValue;

                        var timeProp = tier.FindPropertyRelative("<BuildTimeSeconds>k__BackingField");
                        if (timeProp == null) timeProp = tier.FindPropertyRelative("buildTimeSeconds");
                        if (timeProp != null) time = timeProp.intValue;

                        var prodProp = tier.FindPropertyRelative("<ProductionPerHour>k__BackingField");
                        if (prodProp == null) prodProp = tier.FindPropertyRelative("productionPerHour");
                        if (prodProp != null) prod = prodProp.intValue;

                        var bonusProp = tier.FindPropertyRelative("<BonusPercent>k__BackingField");
                        if (bonusProp == null) bonusProp = tier.FindPropertyRelative("bonusPercent");
                        if (bonusProp != null) bonus = bonusProp.floatValue;

                        sb.AppendLine($"{buildingId},{i + 1},{stone},{iron},{grain},{arcane},{time},{prod},{bonus:F1}");
                    }
                }
            }

            File.WriteAllText(Path.Combine(dir, "building_balance.csv"), sb.ToString());
        }

        private static void GenerateHeroBalanceSheet(string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hero,Faction,Rarity,HP,ATK,DEF,SPD,CritRate,Role,ShardsToUnlock");

            var heroGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{DataRoot}/Heroes" });
            foreach (var guid in heroGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var heroSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (heroSO == null) continue;

                var so = new SerializedObject(heroSO);
                string id = heroSO.name;

                int hp = GetInt(so, "BaseHp", "baseHp");
                int atk = GetInt(so, "BaseAttack", "baseAttack");
                int def = GetInt(so, "BaseDefense", "baseDefense");
                int spd = GetInt(so, "BaseSpeed", "baseSpeed");
                float crit = GetFloat(so, "BaseCritRate", "baseCritRate");
                int rarity = GetInt(so, "Rarity", "rarity");
                int faction = GetInt(so, "Faction", "faction");
                int shardsToUnlock = GetInt(so, "ShardsToUnlock", "shardsToUnlock");

                sb.AppendLine($"{id},{faction},{rarity},{hp},{atk},{def},{spd},{crit:F2},,{shardsToUnlock}");
            }

            File.WriteAllText(Path.Combine(dir, "hero_balance.csv"), sb.ToString());
        }

        private static void GenerateEconomyFlowSheet(string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Day,StoneIncome,IronIncome,GrainIncome,ArcaneIncome,QuestGold,BPPoints,Notes");

            // Model a F2P player's daily economy
            // Production: assume 3 quarries, 3 mines, 3 farms at tier 3 (165/hr each)
            int prodPerResource = 165 * 3; // 495/hr
            int onlineHours = 4; // average session time
            int offlineHours = 8; // capped offline earnings

            int stoneDaily = prodPerResource * (onlineHours + offlineHours);
            int ironDaily = prodPerResource * (onlineHours + offlineHours);
            int grainDaily = prodPerResource * (onlineHours + offlineHours);
            int arcaneDaily = 50 * (onlineHours + offlineHours); // lower arcane production

            // Daily quests: 10 quests * 100 BP points + 50 gold each
            int dailyQuestGold = 500;
            int dailyBP = 1000;

            for (int day = 1; day <= 30; day++)
            {
                string notes = "";
                if (day == 1) notes = "Day 1 — starter resources + quest tutorial";
                if (day == 7) notes = "Week 1 — weekly quests complete";
                if (day == 14) notes = "Week 2 — mid-tier buildings reachable";
                if (day == 30) notes = "Month 1 — BP should be ~tier 30";

                sb.AppendLine($"{day},{stoneDaily},{ironDaily},{grainDaily},{arcaneDaily},{dailyQuestGold},{dailyBP},{notes}");
            }

            File.WriteAllText(Path.Combine(dir, "economy_flow.csv"), sb.ToString());
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static int GetInt(SerializedObject so, string backingName, string fallbackName)
        {
            var prop = so.FindProperty($"<{backingName}>k__BackingField");
            if (prop == null) prop = so.FindProperty(fallbackName);
            return prop?.intValue ?? 0;
        }

        private static float GetFloat(SerializedObject so, string backingName, string fallbackName)
        {
            var prop = so.FindProperty($"<{backingName}>k__BackingField");
            if (prop == null) prop = so.FindProperty(fallbackName);
            return prop?.floatValue ?? 0f;
        }

        private static void EnsureDir(string assetPath)
        {
            var fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }

    // ScriptableObject for gacha pool configuration
    [CreateAssetMenu(fileName = "GachaPoolConfig", menuName = "AshenThrone/Gacha Pool Config", order = 6)]
    public class GachaPoolConfig : ScriptableObject
    {
        public List<GachaItemDef> items = new();

        [System.Serializable]
        public class GachaItemDef
        {
            public string itemId;
            public string displayName;
            public int rarity; // 0=Common, 1=Rare, 2=Epic, 3=Legendary
            public int weight;
        }
    }
}
#endif
