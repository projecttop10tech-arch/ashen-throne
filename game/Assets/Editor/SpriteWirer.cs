#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Wires Kenney RPG UI 9-slice panels + styled buttons + production art to all scenes.
    /// Kenney assets provide proper borders/depth. Production art covers backgrounds/portraits/cards.
    /// Menu: AshenThrone → Wire Sprites
    /// </summary>
    public static class SpriteWirer
    {
        // === Kenney RPG UI Panel Assets (9-slice) ===
        const string KPanelBrown     = "Art/UI/Kenney/panel_brown";
        const string KPanelBeige     = "Art/UI/Kenney/panel_beige";
        const string KPanelBlue      = "Art/UI/Kenney/panel_blue";
        const string KPanelBeigeL    = "Art/UI/Kenney/panel_beigeLight";
        const string KInsetBrown     = "Art/UI/Kenney/panelInset_brown";
        const string KInsetBeige     = "Art/UI/Kenney/panelInset_beige";
        const string KInsetBlue      = "Art/UI/Kenney/panelInset_blue";
        const string KInsetBeigeL    = "Art/UI/Kenney/panelInset_beigeLight";

        // === Kenney Buttons ===
        const string KBtnBrown       = "Art/UI/Kenney/buttonLong_brown";
        const string KBtnBrownP      = "Art/UI/Kenney/buttonLong_brown_pressed";
        const string KBtnBlue        = "Art/UI/Kenney/buttonLong_blue";
        const string KBtnBlueP       = "Art/UI/Kenney/buttonLong_blue_pressed";
        const string KBtnBeige       = "Art/UI/Kenney/buttonLong_beige";
        const string KBtnBeigeP      = "Art/UI/Kenney/buttonLong_beige_pressed";
        const string KBtnGrey        = "Art/UI/Kenney/buttonLong_grey";
        const string KBtnGreyP       = "Art/UI/Kenney/buttonLong_grey_pressed";
        const string KBtnSqBrown     = "Art/UI/Kenney/buttonSquare_brown";
        const string KBtnSqBrownP    = "Art/UI/Kenney/buttonSquare_brown_pressed";
        const string KBtnSqBlue      = "Art/UI/Kenney/buttonSquare_blue";
        const string KBtnSqBlueP     = "Art/UI/Kenney/buttonSquare_blue_pressed";

        // === Kenney Bars ===
        const string KBarBackL       = "Art/UI/Kenney/barBack_horizontalLeft";
        const string KBarBackM       = "Art/UI/Kenney/barBack_horizontalMid";
        const string KBarBackR       = "Art/UI/Kenney/barBack_horizontalRight";
        const string KBarGreenL      = "Art/UI/Kenney/barGreen_horizontalLeft";
        const string KBarGreenM      = "Art/UI/Kenney/barGreen_horizontalMid";
        const string KBarRedL        = "Art/UI/Kenney/barRed_horizontalLeft";
        const string KBarRedM        = "Art/UI/Kenney/barRed_horizontalMid";
        const string KBarBlueL       = "Art/UI/Kenney/barBlue_horizontalLeft";
        const string KBarBlueM       = "Art/UI/Kenney/barBlue_horizontalBlue";
        const string KBarYellowL     = "Art/UI/Kenney/barYellow_horizontalLeft";
        const string KBarYellowM     = "Art/UI/Kenney/barYellow_horizontalMid";

        // === Kenney Icons ===
        const string KIconCheckBlue  = "Art/UI/Kenney/iconCheck_blue";
        const string KIconCrossBrown = "Art/UI/Kenney/iconCross_brown";
        const string KArrowLeft      = "Art/UI/Kenney/arrowBrown_left";
        const string KArrowRight     = "Art/UI/Kenney/arrowBrown_right";

        // === Production Art (gorgeous originals) ===
        const string Splash      = "Art/UI/Production/splash_screen";
        const string AppIcon     = "Art/UI/Production/app_icon";
        const string IconGold    = "Art/UI/Production/icon_currency_gold";
        const string IconGems    = "Art/UI/Production/icon_currency_gems";
        const string EmpireBg    = "Art/Environments/empire_bg";
        const string ForestMap   = "Art/Environments/worldmap_forest";
        const string Stronghold  = "Art/Buildings/stronghold_t1";

        // Hero portraits
        static readonly string[] HeroPortraits = {
            "Art/Characters/Heroes/kael_ashwalker_portrait",
            "Art/Characters/Heroes/lyra_thornveil_portrait",
            "Art/Characters/Heroes/thane_ironhold_portrait",
            "Art/Characters/Heroes/nyx_stormcaller_portrait",
            "Art/Characters/Heroes/vex_shadowstrike_portrait",
            "Art/Characters/Heroes/mira_frostbane_portrait",
            "Art/Characters/Heroes/grim_bonecrusher_portrait",
            "Art/Characters/Heroes/rowan_stoneward_portrait",
            "Art/Characters/Heroes/zara_voidweaver_portrait",
            "Art/Characters/Heroes/sera_dawnblade_portrait",
        };

        // Card frames
        static readonly string[] CardFrames = {
            "Art/UI/Cards/CardFrame_Fire",
            "Art/UI/Cards/CardFrame_Shadow",
            "Art/UI/Cards/CardFrame_Ice",
            "Art/UI/Cards/CardFrame_Lightning",
            "Art/UI/Cards/CardFrame_Nature",
            "Art/UI/Cards/CardFrame_Holy",
            "Art/UI/Cards/CardFrame_Arcane",
            "Art/UI/Cards/CardFrame_Physical",
        };

        // Buildings
        static readonly string[] Buildings = {
            "Art/Buildings/barracks_t1",
            "Art/Buildings/forge_t1",
            "Art/Buildings/marketplace_t1",
        };

        // Environment tiles
        static readonly string[] EnvTiles = {
            "Art/Environments/worldmap_forest",
            "Art/Environments/worldmap_mountain",
            "Art/Environments/worldmap_desert",
            "Art/Environments/worldmap_swamp",
            "Art/Environments/worldmap_volcanic",
            "Art/Environments/worldmap_ocean",
        };

        // Dark fantasy tint colors
        static readonly Color TintDark      = new Color(0.35f, 0.25f, 0.40f, 1f);  // Dark purple tint for panels
        static readonly Color TintWarm      = new Color(0.50f, 0.35f, 0.30f, 1f);  // Warm brown tint
        static readonly Color TintNavy      = new Color(0.25f, 0.25f, 0.45f, 1f);  // Navy blue tint
        static readonly Color TintDarkGold  = new Color(0.55f, 0.45f, 0.25f, 1f);  // Dark gold tint
        static readonly Color TintEmber     = new Color(0.65f, 0.35f, 0.20f, 1f);  // Ember tint for CTA buttons
        static readonly Color TintBlood     = new Color(0.60f, 0.20f, 0.25f, 1f);  // Blood red tint
        static readonly Color TintTeal      = new Color(0.20f, 0.50f, 0.45f, 1f);  // Teal accent
        static readonly Color TintPurple    = new Color(0.45f, 0.20f, 0.55f, 1f);  // Purple accent

        // ============================================================
        // IMPORT SETUP — Must run FIRST to set 9-slice borders
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/1. Import Kenney Sprites")]
        public static void ImportKenneySprites()
        {
            // Force Unity to detect new files copied from outside the editor
            AssetDatabase.Refresh();

            int count = 0;

            // Panels: 20px border on each side
            count += SetSpriteImport("panel_brown", 20);
            count += SetSpriteImport("panel_beige", 20);
            count += SetSpriteImport("panel_blue", 20);
            count += SetSpriteImport("panel_beigeLight", 20);
            count += SetSpriteImport("panelInset_brown", 16);
            count += SetSpriteImport("panelInset_beige", 16);
            count += SetSpriteImport("panelInset_blue", 16);
            count += SetSpriteImport("panelInset_beigeLight", 16);

            // Long buttons: 12px borders (thinner on top/bottom)
            count += SetSpriteImport("buttonLong_brown", 12, 8);
            count += SetSpriteImport("buttonLong_brown_pressed", 12, 8);
            count += SetSpriteImport("buttonLong_blue", 12, 8);
            count += SetSpriteImport("buttonLong_blue_pressed", 12, 8);
            count += SetSpriteImport("buttonLong_beige", 12, 8);
            count += SetSpriteImport("buttonLong_beige_pressed", 12, 8);
            count += SetSpriteImport("buttonLong_grey", 12, 8);
            count += SetSpriteImport("buttonLong_grey_pressed", 12, 8);

            // Square buttons: 10px all around
            count += SetSpriteImport("buttonSquare_brown", 10);
            count += SetSpriteImport("buttonSquare_brown_pressed", 10);
            count += SetSpriteImport("buttonSquare_blue", 10);
            count += SetSpriteImport("buttonSquare_blue_pressed", 10);

            // Round buttons: no 9-slice needed
            count += SetSpriteImport("buttonRound_brown", 0);
            count += SetSpriteImport("buttonRound_blue", 0);

            // Bar segments: no 9-slice (used as tiled fills)
            string[] barAssets = {
                "barBack_horizontalLeft", "barBack_horizontalMid", "barBack_horizontalRight",
                "barGreen_horizontalLeft", "barGreen_horizontalMid", "barGreen_horizontalRight",
                "barRed_horizontalLeft", "barRed_horizontalMid", "barRed_horizontalRight",
                "barBlue_horizontalLeft", "barBlue_horizontalBlue", "barBlue_horizontalRight",
                "barYellow_horizontalLeft", "barYellow_horizontalMid", "barYellow_horizontalRight",
            };
            foreach (var b in barAssets)
                count += SetSpriteImport(b, 0);

            // Icons + arrows
            string[] iconAssets = {
                "iconCheck_blue", "iconCheck_bronze", "iconCross_brown", "iconCross_blue",
                "arrowBrown_left", "arrowBrown_right", "arrowBlue_left", "arrowBlue_right",
                "arrowSilver_left", "arrowSilver_right",
                "iconCircle_brown", "iconCircle_blue",
            };
            foreach (var ic in iconAssets)
                count += SetSpriteImport(ic, 0);

            // Also fix ALL Art/ textures that aren't sprites yet
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                    count++;
                }
            }

            Debug.Log($"[SpriteWirer] Imported {count} sprites with 9-slice borders.");
        }

        static int SetSpriteImport(string filename, int border, int borderTB = -1)
        {
            string path = $"Assets/Art/UI/Kenney/{filename}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return 0;

            if (borderTB < 0) borderTB = border;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (border > 0)
            {
                var borderVec = new Vector4(border, borderTB, border, borderTB);
                if (importer.spriteBorder != borderVec)
                {
                    importer.spriteBorder = borderVec;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
                return 1;
            }
            return 0;
        }

        // ============================================================
        // MAIN ENTRY — Wire all scenes
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/2. Wire All Scenes")]
        public static void WireAll()
        {
            WireBootSprites();
            WireLobbySprites();
            WireCombatSprites();
            WireEmpireSprites();
            WireWorldMapSprites();
            WireAllianceSprites();
            Debug.Log("[SpriteWirer] All 6 scenes wired with Kenney + Production art.");
        }

        // ============================================================
        // BOOT — Splash screen with styled loading
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/Boot")]
        static void WireBootSprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot/Boot.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Full-screen splash art background
            SetSpriteFill(canvas, "Background", Splash);

            // Loading frame — brown 9-slice panel, tinted dark
            SetSlicedPanel(canvas, "LoadingFrame", KPanelBrown, TintDark);

            // Loading bar — use Kenney bar segments
            SetSpriteFill(canvas, "BarTrack", KBarBackM);
            SetSpriteTinted(canvas, "BarFill", KBarYellowM, new Color(0.9f, 0.75f, 0.3f));

            // Accent lines — gold tinted inset
            SetSlicedPanel(canvas, "TopAccent", KInsetBrown, TintDarkGold);
            SetSlicedPanel(canvas, "BottomAccent", KInsetBrown, TintDarkGold);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] Boot: splash + Kenney loading UI");
        }

        // ============================================================
        // LOBBY — Main hub with nav, events, heroes
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/Lobby")]
        static void WireLobbySprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby/Lobby.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Background — production empire city art
            SetSpriteFill(canvas, "Background", EmpireBg);

            // Header bar — brown 9-slice panel tinted dark
            SetSlicedPanel(canvas, "HeaderBar", KPanelBrown, TintDark);
            SetSlicedPanel(canvas, "AvatarFrame", KInsetBrown, TintDarkGold);
            SetSpritePreserve(canvas, "AvatarIcon", HeroPortraits[0]);

            // Resource bar — inset panel for recessed look
            SetSlicedPanel(canvas, "ResourceBar", KInsetBrown, TintDark);

            // Event banner — blue panel for emphasis, tinted dark purple
            SetSlicedPanel(canvas, "EventBanner", KPanelBlue, TintPurple);

            // Quest summary — beige panel tinted dark
            SetSlicedPanel(canvas, "QuestSummary", KPanelBeige, TintDark);

            // Battle pass bar — inset panel
            SetSlicedPanel(canvas, "BattlePassBar", KInsetBrown, TintDark);
            SetSpriteTinted(canvas, "BPFill", KBarYellowM, TintDarkGold);
            SetSpriteFill(canvas, "BPTrack", KBarBackM);

            // XP bar
            SetSpriteTinted(canvas, "XPBarFill", KBarBlueM, TintPurple);
            SetSpriteFill(canvas, "XPBarTrack", KBarBackM);

            // === BUTTONS — Variety! ===
            // Play button — blue styled (primary CTA), tinted blood red
            SetSlicedButton(canvas, "PlayButton", KBtnBlue, KBtnBlueP, TintBlood);

            // Quick action buttons — brown styled, different tints
            SetSlicedButton(canvas, "PvPBtn", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "VoidRiftBtn", KBtnBrown, KBtnBrownP, TintPurple);
            SetSlicedButton(canvas, "DailyBtn", KBtnBrown, KBtnBrownP, TintTeal);

            // BP Claim button — gold
            SetSlicedButton(canvas, "BPClaimBtn", KBtnBeige, KBtnBeigeP, TintDarkGold);

            // Bottom nav — brown buttons, different tints per section
            SetSlicedPanel(canvas, "BottomNavBar", KPanelBrown, TintDark);
            SetSlicedButton(canvas, "NavEmpire", KBtnBrown, KBtnBrownP, TintEmber);
            SetSlicedButton(canvas, "NavHeroes", KBtnBrown, KBtnBrownP, TintPurple);
            SetSlicedButton(canvas, "NavBattle", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "NavAlliance", KBtnBrown, KBtnBrownP, TintTeal);
            SetSlicedButton(canvas, "NavShop", KBtnBeige, KBtnBeigeP, TintDarkGold);

            // Currency icons — production quality
            WireCurrencyIcon(canvas, "GoldDisplay", IconGold);
            WireCurrencyIcon(canvas, "GemDisplay", IconGems);
            WireCurrencyIcon(canvas, "EnergyDisplay", IconGems);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] Lobby: Kenney panels + production backgrounds");
        }

        // ============================================================
        // COMBAT — Tactical card battle HUD
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/Combat")]
        static void WireCombatSprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Combat/Combat.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Top bar — dark brown panel
            SetSlicedPanel(canvas, "TopBar", KPanelBrown, TintDark);

            // Turn order panel — blue inset for tactical feel
            SetSlicedPanel(canvas, "TurnOrderPanel", KPanelBlue, TintNavy);
            // Turn order tokens — each in a small brown inset
            for (int i = 0; i < 6; i++)
            {
                SetSlicedPanel(canvas, $"Token_{i}", KInsetBrown, i < 3 ? TintNavy : TintBlood);
                SetSpritePreserve(canvas, $"TurnToken_{i}", HeroPortraits[i % HeroPortraits.Length]);
            }

            // Card tray — brown panel, tinted dark
            SetSlicedPanel(canvas, "CardTray", KPanelBrown, TintDark);

            // Individual cards — use elemental card frames (production art)
            for (int i = 0; i < 5; i++)
            {
                SetSlicedPanel(canvas, $"CardBg_{i}", KPanelBeige, TintDark);
                SetSpritePreserve(canvas, $"Card_{i}", CardFrames[i % CardFrames.Length]);
            }

            // Hero status panels — inset brown panels
            for (int i = 0; i < 3; i++)
            {
                SetSlicedPanel(canvas, $"PlayerHero_{i}", KInsetBrown, TintNavy);
                SetSpritePreserve(canvas, $"HeroPortrait_{i}", HeroPortraits[i]);
                // HP bars — green
                SetSpriteTinted(canvas, $"HPFill_{i}", KBarGreenM, Color.white);
                SetSpriteFill(canvas, $"HPBg_{i}", KBarBackM);
            }
            for (int i = 0; i < 3; i++)
            {
                SetSlicedPanel(canvas, $"EnemyHero_{i}", KInsetBrown, TintBlood);
                SetSpritePreserve(canvas, $"HeroPortrait_{(i+3)}", HeroPortraits[(i+3) % HeroPortraits.Length]);
                SetSpriteTinted(canvas, $"HPFill_{(i+3)}", KBarRedM, Color.white);
                SetSpriteFill(canvas, $"HPBg_{(i+3)}", KBarBackM);
            }

            // Energy panel — blue panel
            SetSlicedPanel(canvas, "EnergyPanel", KPanelBlue, TintNavy);
            SetSpriteTinted(canvas, "EnergyFill", KBarBlueM, Color.white);
            SetSpriteFill(canvas, "EnergyBg", KBarBackM);

            // Buttons — varied styles
            SetSlicedButton(canvas, "EndTurnButton", KBtnBlue, KBtnBlueP, TintBlood);
            SetSlicedButton(canvas, "RetreatBtn", KBtnGrey, KBtnGreyP, TintBlood);

            // Victory overlay — gold accented
            SetSlicedPanel(canvas, "Frame", KPanelBrown, TintDarkGold); // inside VictoryPanel
            SetSlicedButton(canvas, "ContinueBtn", KBtnBlue, KBtnBlueP, TintDarkGold);

            // Defeat overlay — blood red accented
            SetSlicedButton(canvas, "RetryBtn", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "QuitBtn", KBtnGrey, KBtnGreyP, TintWarm);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] Combat: Kenney panels + card frames + hero portraits");
        }

        // ============================================================
        // EMPIRE — City builder with buildings and resources
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/Empire")]
        static void WireEmpireSprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Empire/Empire.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Background — production empire city art
            SetSpriteFill(canvas, "Background", EmpireBg);

            // Resource HUD — dark brown panel
            SetSlicedPanel(canvas, "ResourceHUD", KPanelBrown, TintDark);

            // Resource widgets — inset for recessed look with colored fills
            SetSpriteTinted(canvas, "StoneFill", KBarGreenM, new Color(0.55f, 0.50f, 0.45f));
            SetSpriteTinted(canvas, "IronFill", KBarBlueM, new Color(0.50f, 0.55f, 0.65f));
            SetSpriteTinted(canvas, "GrainFill", KBarYellowM, new Color(0.80f, 0.72f, 0.25f));
            SetSpriteTinted(canvas, "ArcaneFill", KBarBlueM, TintPurple);

            // Resource icons
            SetSpritePreserve(canvas, "StoneIcon", IconGold);
            SetSpritePreserve(canvas, "IronIcon", IconGold);
            SetSpritePreserve(canvas, "GrainIcon", IconGold);
            SetSpritePreserve(canvas, "ArcaneIcon", IconGems);

            // Stronghold info — gold-tinted panel
            SetSlicedPanel(canvas, "StrongholdInfo", KPanelBrown, TintDarkGold);
            SetSpritePreserve(canvas, "StrongholdImage", Stronghold);

            // Build queue — ember-tinted panel
            SetSlicedPanel(canvas, "BuildQueueOverlay", KPanelBrown, TintWarm);
            for (int i = 0; i < 3; i++)
            {
                SetSlicedPanel(canvas, $"QueueSlot_{i}", KInsetBrown, TintDark);
                SetSpritePreserve(canvas, $"QueueIcon_{i}", Buildings[i % Buildings.Length]);
            }

            // Building info popup
            SetSlicedPanel(canvas, "BuildingInfoPopup", KPanelBrown, TintDark);

            // Bottom toolbar — dark brown panel
            SetSlicedPanel(canvas, "Toolbar", KPanelBrown, TintDark);

            // Toolbar buttons — different tints per function
            SetSlicedButton(canvas, "BuildBtn", KBtnBrown, KBtnBrownP, TintEmber);
            SetSlicedButton(canvas, "ResearchBtn", KBtnBrown, KBtnBrownP, TintNavy);
            SetSlicedButton(canvas, "HeroesBtn", KBtnBrown, KBtnBrownP, TintPurple);
            SetSlicedButton(canvas, "QuestsBtn", KBtnBrown, KBtnBrownP, TintTeal);
            SetSlicedButton(canvas, "BattleBtn", KBtnBlue, KBtnBlueP, TintBlood);
            SetSlicedButton(canvas, "UpgradeBtn", KBtnBeige, KBtnBeigeP, TintDarkGold);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] Empire: Kenney panels + production backgrounds");
        }

        // ============================================================
        // WORLD MAP — Territory grid with environment art
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/WorldMap")]
        static void WireWorldMapSprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WorldMap/WorldMap.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Background — forest map production art
            SetSpriteFill(canvas, "Background", ForestMap);

            // Top bar — dark panel
            SetSlicedPanel(canvas, "TopBar", KPanelBrown, TintDark);

            // Territory info — brown panel with gold tint
            SetSlicedPanel(canvas, "TerritoryInfoPanel", KPanelBrown, TintDarkGold);

            // Legend — inset panel
            SetSlicedPanel(canvas, "Legend", KInsetBrown, TintDark);

            // Mini map — blue inset
            SetSlicedPanel(canvas, "MiniMap", KInsetBlue, TintNavy);

            // Buttons
            SetSlicedButton(canvas, "BackBtn", KBtnGrey, KBtnGreyP, TintWarm);
            SetSlicedButton(canvas, "AttackBtn", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "ScoutBtn", KBtnBrown, KBtnBrownP, TintTeal);

            // Territory tiles — production environment art
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 6; c++)
                    SetSpritePreserve(canvas, $"Tile_{r}_{c}", EnvTiles[(r + c) % EnvTiles.Length]);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] WorldMap: Kenney panels + environment tiles");
        }

        // ============================================================
        // ALLIANCE — Guild chat and social
        // ============================================================
        [MenuItem("AshenThrone/Wire Sprites/Alliance")]
        static void WireAllianceSprites()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Alliance/Alliance.unity", OpenSceneMode.Single);
            var canvas = FindCanvas(scene);
            if (canvas == null) return;

            // Top bar + Tab bar — dark panels
            SetSlicedPanel(canvas, "TopBar", KPanelBrown, TintDark);
            SetSlicedPanel(canvas, "TabBar", KPanelBrown, TintDark);

            // Chat panel — beige panel tinted dark for message area
            SetSlicedPanel(canvas, "ChatPanel", KPanelBeige, TintDark);

            // Input bar — inset for text field feel
            SetSlicedPanel(canvas, "InputBar", KInsetBrown, TintDark);

            // Bottom actions
            SetSlicedPanel(canvas, "BottomActions", KPanelBrown, TintDark);

            // Buttons
            SetSlicedButton(canvas, "BackBtn", KBtnGrey, KBtnGreyP, TintWarm);
            SetSlicedButton(canvas, "SendBtn", KBtnBlue, KBtnBlueP, TintTeal);
            SetSlicedButton(canvas, "DonateBtn", KBtnBrown, KBtnBrownP, TintDarkGold);
            SetSlicedButton(canvas, "WarBtn", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "TerritoryBtn", KBtnBrown, KBtnBrownP, TintEmber);

            // Tab buttons — brown with different highlights
            SetSlicedButton(canvas, "TabChat", KBtnBrown, KBtnBrownP, TintTeal);
            SetSlicedButton(canvas, "TabMembers", KBtnBrown, KBtnBrownP, TintPurple);
            SetSlicedButton(canvas, "TabWar", KBtnBrown, KBtnBrownP, TintBlood);
            SetSlicedButton(canvas, "TabTerritory", KBtnBrown, KBtnBrownP, TintEmber);
            SetSlicedButton(canvas, "TabRanks", KBtnBrown, KBtnBrownP, TintDarkGold);

            // Chat messages — dark inset panels
            for (int i = 0; i < 6; i++)
            {
                SetSlicedPanel(canvas, $"Msg_{i}", KInsetBrown, TintDark);
                SetSpritePreserve(canvas, $"MsgAvatar_{i}", HeroPortraits[i % HeroPortraits.Length]);
            }

            // Alliance crest
            SetSpritePreserve(canvas, "AllianceCrest", AppIcon);

            SaveScene(scene);
            Debug.Log("[SpriteWirer] Alliance: Kenney panels + hero avatars");
        }

        // ============================================================
        // UTILITY METHODS
        // ============================================================

        static Transform FindCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Canvas>() != null)
                    return root.transform;
            Debug.LogError($"[SpriteWirer] No Canvas found in scene {scene.name}");
            return null;
        }

        /// <summary>Set sprite as 9-sliced panel with tint color.</summary>
        static void SetSlicedPanel(Transform root, string objectName, string artPath, Color tint)
        {
            var tf = FindDeep(root, objectName);
            if (tf == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{artPath}.png");
            if (sprite == null) return;
            var img = tf.GetComponent<Image>();
            if (img == null) return;

            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = tint;
            img.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>Set sprite as 9-sliced button with press state and tint.</summary>
        static void SetSlicedButton(Transform root, string objectName, string normalPath, string pressedPath, Color tint)
        {
            var tf = FindDeep(root, objectName);
            if (tf == null) return;
            var normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{normalPath}.png");
            var pressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{pressedPath}.png");
            if (normalSprite == null) return;

            var img = tf.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = normalSprite;
                img.type = Image.Type.Sliced;
                img.color = tint;
            }

            // Set press state on Button component
            var btn = tf.GetComponent<Button>();
            if (btn != null && pressedSprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var state = new SpriteState();
                state.pressedSprite = pressedSprite;
                state.highlightedSprite = normalSprite;
                state.selectedSprite = normalSprite;
                state.disabledSprite = normalSprite;
                btn.spriteState = state;
            }
        }

        /// <summary>Set sprite with Simple type, no aspect preserve (fills the rect).</summary>
        static void SetSpriteFill(Transform root, string objectName, string artPath)
        {
            var tf = FindDeep(root, objectName);
            if (tf == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{artPath}.png");
            if (sprite == null) return;
            var img = tf.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }
        }

        /// <summary>Set sprite with Simple type + tint color.</summary>
        static void SetSpriteTinted(Transform root, string objectName, string artPath, Color tint)
        {
            var tf = FindDeep(root, objectName);
            if (tf == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{artPath}.png");
            if (sprite == null) return;
            var img = tf.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = tint;
            }
        }

        /// <summary>Set sprite with aspect preserve (for icons/portraits).</summary>
        static void SetSpritePreserve(Transform root, string objectName, string artPath)
        {
            var tf = FindDeep(root, objectName);
            if (tf == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{artPath}.png");
            if (sprite == null) return;
            var img = tf.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        static void WireCurrencyIcon(Transform root, string displayName, string iconPath)
        {
            var display = FindDeep(root, displayName);
            if (display == null) return;
            var icon = FindDeep(display, "Icon");
            if (icon == null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/{iconPath}.png");
            if (sprite == null) return;
            var img = icon.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        static void SaveScene(UnityEngine.SceneManagement.Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
