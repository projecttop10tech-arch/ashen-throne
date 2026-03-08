#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates polished dark fantasy UI for all 6 game scenes.
    /// Safe to re-run — clears existing UI before rebuilding.
    /// Menu: AshenThrone → Generate Scene UI
    /// </summary>
    public static class SceneUIGenerator
    {
        // ===================================================================
        // COLOR PALETTE — Dark Fantasy
        // ===================================================================
        static readonly Color BgDeep       = new Color(0.04f, 0.02f, 0.08f, 1f);     // #0A0514
        static readonly Color BgDark       = new Color(0.08f, 0.05f, 0.14f, 1f);     // #140D24
        static readonly Color BgMid        = new Color(0.12f, 0.08f, 0.20f, 1f);     // #1F1433
        static readonly Color BgPanel      = new Color(0.10f, 0.07f, 0.18f, 0.95f);  // Panel overlay
        static readonly Color BgPanelLight = new Color(0.14f, 0.10f, 0.22f, 0.9f);   // Lighter panel
        static readonly Color BgCard       = new Color(0.12f, 0.08f, 0.16f, 1f);     // Card background
        static readonly Color BgInput      = new Color(0.06f, 0.04f, 0.10f, 1f);     // Input field bg

        static readonly Color Gold         = new Color(0.83f, 0.66f, 0.26f, 1f);     // #D4A843
        static readonly Color GoldDim      = new Color(0.55f, 0.43f, 0.18f, 1f);     // Muted gold
        static readonly Color GoldBright   = new Color(0.94f, 0.78f, 0.31f, 1f);     // Bright gold
        static readonly Color Ember        = new Color(0.91f, 0.45f, 0.16f, 1f);     // #E8732A
        static readonly Color EmberDim     = new Color(0.65f, 0.32f, 0.12f, 1f);
        static readonly Color Blood        = new Color(0.75f, 0.15f, 0.20f, 1f);     // #C02633
        static readonly Color BloodDark    = new Color(0.45f, 0.08f, 0.12f, 1f);
        static readonly Color Teal         = new Color(0.18f, 0.78f, 0.65f, 1f);     // #2EC7A6
        static readonly Color TealDim      = new Color(0.12f, 0.50f, 0.42f, 1f);
        static readonly Color Purple       = new Color(0.55f, 0.22f, 0.72f, 1f);     // #8C38B8
        static readonly Color PurpleDim    = new Color(0.35f, 0.14f, 0.48f, 1f);
        static readonly Color Sky          = new Color(0.30f, 0.55f, 0.90f, 1f);     // #4D8CE6
        static readonly Color SkyDim       = new Color(0.20f, 0.35f, 0.60f, 1f);

        static readonly Color TextLight    = new Color(0.91f, 0.87f, 0.78f, 1f);     // #E8DEC8
        static readonly Color TextMid      = new Color(0.65f, 0.60f, 0.52f, 1f);     // Muted
        static readonly Color TextDim      = new Color(0.40f, 0.37f, 0.33f, 1f);     // Very dim
        static readonly Color TextWhite    = new Color(0.95f, 0.93f, 0.90f, 1f);

        static readonly Color Border       = new Color(0.42f, 0.34f, 0.18f, 0.8f);   // Gold border
        static readonly Color BorderDim    = new Color(0.25f, 0.20f, 0.12f, 0.6f);

        static readonly Color BarHpGreen   = new Color(0.20f, 0.70f, 0.30f, 1f);
        static readonly Color BarHpRed     = new Color(0.75f, 0.15f, 0.15f, 1f);
        static readonly Color BarHpBg      = new Color(0.15f, 0.10f, 0.10f, 1f);
        static readonly Color BarEnergy    = new Color(0.25f, 0.55f, 0.95f, 1f);
        static readonly Color BarEnergyDim = new Color(0.12f, 0.15f, 0.25f, 1f);
        static readonly Color BarXp        = new Color(0.60f, 0.45f, 0.90f, 1f);

        // Resource colors
        static readonly Color StoneColor   = new Color(0.55f, 0.50f, 0.45f, 1f);
        static readonly Color IronColor    = new Color(0.50f, 0.55f, 0.65f, 1f);
        static readonly Color GrainColor   = new Color(0.80f, 0.72f, 0.25f, 1f);
        static readonly Color ArcaneColor  = new Color(0.55f, 0.25f, 0.85f, 1f);
        static readonly Color GemsColor    = new Color(0.30f, 0.75f, 0.95f, 1f);     // Cyan gems
        static readonly Color ResBarBg     = new Color(0.06f, 0.07f, 0.14f, 0.95f);  // Dark navy

        // ===================================================================
        // ENTRY POINT
        // ===================================================================

        [MenuItem("AshenThrone/Play Empire Scene")]
        public static void PlayEmpireScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Empire/Empire.unity", OpenSceneMode.Single);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Empire/Empire.unity");
            EditorSceneManager.playModeStartScene = sceneAsset;
            EditorApplication.isPlaying = true;
        }


        [MenuItem("AshenThrone/Generate Scene UI")]
        public static void GenerateAll()
        {
            SetupBootScene();
            SetupLobbyScene();
            SetupCombatScene();
            SetupEmpireScene();
            SetupWorldMapScene();
            SetupAllianceScene();
            Debug.Log("[SceneUIGenerator] All 6 scenes populated with polished UI.");
        }

        // ===================================================================
        // BOOT SCENE — Epic loading screen
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Boot")]
        public static void SetupBootScene()
        {
            var scene = OpenScene("Boot");
            var canvasGo = FindOrCreateCanvas(scene);

            // Backgrounds (full screen, behind safe area)
            var bg = AddPanel(canvasGo, "Background", BgDeep);
            StretchToParent(bg);
            var vignette = AddPanel(canvasGo, "Vignette", new Color(0, 0, 0, 0.3f));
            StretchToParent(vignette);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // Decorative top accent line
            var topAccent = AddPanel(canvas, "TopAccent", GoldDim);
            SetAnchors(topAccent, 0.15f, 0.78f, 0.85f, 0.785f);

            // Title
            var title = AddText(canvas, "Title", "ASHEN THRONE", 52, TextAnchor.MiddleCenter);
            SetAnchors(title, 0.05f, 0.65f, 0.95f, 0.78f);
            title.GetComponent<Text>().color = Gold;
            AddOutline(title, new Color(0.3f, 0.2f, 0.05f), 2f);

            // Subtitle
            var sub = AddText(canvas, "Subtitle", "A Dark Fantasy Strategy RPG", 16, TextAnchor.MiddleCenter);
            SetAnchors(sub, 0.15f, 0.58f, 0.85f, 0.64f);
            sub.GetComponent<Text>().color = TextMid;

            // Decorative bottom accent line
            var botAccent = AddPanel(canvas, "BottomAccent", GoldDim);
            SetAnchors(botAccent, 0.15f, 0.575f, 0.85f, 0.58f);

            // Loading area frame
            var loadFrame = AddPanel(canvas, "LoadingFrame", new Color(0.08f, 0.06f, 0.12f, 0.8f));
            SetAnchors(loadFrame, 0.15f, 0.32f, 0.85f, 0.50f);
            AddOutlinePanel(loadFrame, BorderDim);

            // Loading status text
            var statusText = AddText(loadFrame, "StatusText", "Initializing Services...", 13, TextAnchor.MiddleCenter);
            SetAnchors(statusText, 0.05f, 0.55f, 0.95f, 0.85f);
            statusText.GetComponent<Text>().color = TextMid;

            // Loading bar track
            var barTrack = AddPanel(loadFrame, "BarTrack", new Color(0.06f, 0.04f, 0.08f, 1f));
            SetAnchors(barTrack, 0.08f, 0.2f, 0.92f, 0.42f);
            AddOutlinePanel(barTrack, BorderDim);

            // Loading bar fill
            var barFill = AddPanel(barTrack, "BarFill", Gold);
            var fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.01f, 0.1f);
            fillRect.anchorMax = new Vector2(0.70f, 0.9f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Loading percentage
            var pctText = AddText(loadFrame, "Percentage", "70%", 12, TextAnchor.MiddleCenter);
            SetAnchors(pctText, 0.4f, 0.2f, 0.6f, 0.42f);
            pctText.GetComponent<Text>().color = TextWhite;

            // Tip text at bottom
            var tip = AddText(canvas, "TipText", "TIP: Upgrade your Stronghold to unlock new building types", 11, TextAnchor.MiddleCenter);
            SetAnchors(tip, 0.1f, 0.18f, 0.9f, 0.26f);
            tip.GetComponent<Text>().color = TextDim;
            tip.GetComponent<Text>().fontStyle = FontStyle.Italic;

            // Version + copyright
            var ver = AddText(canvas, "VersionLabel", "v0.1.0-alpha  |  Ashen Throne Studios", 10, TextAnchor.LowerCenter);
            SetAnchors(ver, 0.1f, 0.02f, 0.9f, 0.06f);
            ver.GetComponent<Text>().color = TextDim;

            SaveScene();
            Debug.Log("[SceneUIGenerator] Boot scene: polished loading screen");
        }

        // ===================================================================
        // LOBBY SCENE — Main menu hub
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Lobby")]
        public static void SetupLobbyScene()
        {
            var scene = OpenScene("Lobby");
            var canvasGo = FindOrCreateCanvas(scene);

            // Background (full screen, behind safe area)
            var bg = AddPanel(canvasGo, "Background", BgDeep);
            StretchToParent(bg);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // --- TOP HEADER BAR ---
            var header = AddPanel(canvas, "HeaderBar", BgPanel);
            SetAnchors(header, 0f, 0.93f, 1f, 1f);
            AddOutlinePanel(header, BorderDim);

            // Player avatar frame
            var avatarFrame = AddPanel(header, "AvatarFrame", BgMid);
            SetAnchors(avatarFrame, 0.01f, 0.1f, 0.06f, 0.9f);
            AddOutlinePanel(avatarFrame, Gold);
            var avatarIcon = AddPanel(avatarFrame, "AvatarIcon", Purple);
            SetAnchors(avatarIcon, 0.1f, 0.1f, 0.9f, 0.9f);

            // Player name + level
            var playerName = AddText(header, "PlayerName", "Commander", 15, TextAnchor.MiddleLeft);
            SetAnchors(playerName, 0.07f, 0.5f, 0.22f, 0.95f);
            playerName.GetComponent<Text>().color = Gold;

            var playerLvl = AddText(header, "PlayerLevel", "Level 1  •  Stronghold Lv.1", 10, TextAnchor.MiddleLeft);
            SetAnchors(playerLvl, 0.07f, 0.08f, 0.25f, 0.48f);
            playerLvl.GetComponent<Text>().color = TextMid;

            // XP bar
            var xpTrack = AddPanel(header, "XPBarTrack", new Color(0.06f, 0.04f, 0.10f));
            SetAnchors(xpTrack, 0.07f, 0.02f, 0.22f, 0.12f);
            var xpFill = AddPanel(xpTrack, "XPBarFill", BarXp);
            SetAnchors(xpFill, 0f, 0f, 0.35f, 1f);

            // Currency displays (right side)
            AddCurrencyDisplay(header, "GoldDisplay", "12,450", Gold, 0.60f);
            AddCurrencyDisplay(header, "GemDisplay", "385", Purple, 0.75f);
            AddCurrencyDisplay(header, "EnergyDisplay", "120/120", Teal, 0.88f);

            // --- RESOURCE BAR (below header) ---
            var resBar = AddPanel(canvas, "ResourceBar", new Color(0.06f, 0.04f, 0.10f, 0.9f));
            SetAnchors(resBar, 0f, 0.895f, 1f, 0.93f);

            AddResourceDisplay(resBar, "Stone", "12,500", StoneColor, 0.02f);
            AddResourceDisplay(resBar, "Iron", "8,200", IronColor, 0.26f);
            AddResourceDisplay(resBar, "Grain", "15,000", GrainColor, 0.50f);
            AddResourceDisplay(resBar, "Arcane", "3,400", ArcaneColor, 0.74f);

            // --- CENTER CONTENT ---
            // Game logo / title
            var logoText = AddText(canvas, "LogoText", "ASHEN THRONE", 42, TextAnchor.MiddleCenter);
            SetAnchors(logoText, 0.1f, 0.72f, 0.9f, 0.85f);
            logoText.GetComponent<Text>().color = Gold;
            AddOutline(logoText, new Color(0.2f, 0.12f, 0.03f), 2f);

            // Featured event banner
            var eventBanner = AddPanel(canvas, "EventBanner", new Color(0.18f, 0.10f, 0.28f, 0.9f));
            SetAnchors(eventBanner, 0.08f, 0.55f, 0.92f, 0.70f);
            AddOutlinePanel(eventBanner, Ember);

            var eventTag = AddPanel(eventBanner, "EventTag", Ember);
            SetAnchors(eventTag, 0.0f, 0.75f, 0.22f, 1.0f);
            var eventTagText = AddText(eventTag, "TagText", "  EVENT", 10, TextAnchor.MiddleLeft);
            StretchToParent(eventTagText);
            eventTagText.GetComponent<Text>().color = TextWhite;
            eventTagText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var eventTitle = AddText(eventBanner, "EventTitle", "Dragon Siege — World Boss Raid", 18, TextAnchor.MiddleLeft);
            SetAnchors(eventTitle, 0.04f, 0.35f, 0.70f, 0.72f);
            eventTitle.GetComponent<Text>().color = TextWhite;

            var eventDesc = AddText(eventBanner, "EventDesc", "Alliance-wide battle • 2d 14h remaining", 11, TextAnchor.MiddleLeft);
            SetAnchors(eventDesc, 0.04f, 0.08f, 0.70f, 0.35f);
            eventDesc.GetComponent<Text>().color = TextMid;

            var eventBtn = AddStyledButton(eventBanner, "JoinBtn", "JOIN NOW", Ember, EmberDim);
            SetAnchors(eventBtn, 0.72f, 0.15f, 0.96f, 0.70f);

            // --- MAIN PLAY BUTTON ---
            var playBtn = AddStyledButton(canvas, "PlayButton", "CAMPAIGN", Blood, BloodDark);
            SetAnchors(playBtn, 0.25f, 0.42f, 0.75f, 0.52f);
            playBtn.transform.Find("Label").GetComponent<Text>().fontSize = 22;

            // Quick action row
            var quickRow = AddPanel(canvas, "QuickActions", new Color(0, 0, 0, 0));
            SetAnchors(quickRow, 0.08f, 0.30f, 0.92f, 0.40f);
            var qrLayout = quickRow.AddComponent<HorizontalLayoutGroup>();
            qrLayout.spacing = 12;
            qrLayout.childForceExpandWidth = true;

            AddQuickActionButton(quickRow, "PvPBtn", "PVP ARENA", Blood);
            AddQuickActionButton(quickRow, "VoidRiftBtn", "VOID RIFT", Purple);
            AddQuickActionButton(quickRow, "DailyBtn", "DAILY QUESTS", Teal);

            // --- BOTTOM NAVIGATION BAR ---
            var navBar = AddPanel(canvas, "BottomNavBar", BgPanel);
            SetAnchors(navBar, 0f, 0f, 1f, 0.08f);
            AddOutlinePanel(navBar, BorderDim);

            var navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
            navLayout.spacing = 2;
            navLayout.padding = new RectOffset(4, 4, 2, 2);
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = true;

            AddNavButton(navBar, "NavEmpire", "EMPIRE", EmberDim, true);
            AddNavButton(navBar, "NavHeroes", "HEROES", PurpleDim, false);
            AddNavButton(navBar, "NavBattle", "BATTLE", Blood, false);
            AddNavButton(navBar, "NavAlliance", "ALLIANCE", TealDim, false);
            AddNavButton(navBar, "NavShop", "SHOP", GoldDim, false);

            // Battle Pass progress bar
            var bpBar = AddPanel(canvas, "BattlePassBar", new Color(0.06f, 0.04f, 0.10f, 0.9f));
            SetAnchors(bpBar, 0.08f, 0.20f, 0.92f, 0.27f);
            AddOutlinePanel(bpBar, GoldDim);

            var bpLabel = AddText(bpBar, "BPLabel", "BATTLE PASS", 9, TextAnchor.MiddleLeft);
            SetAnchors(bpLabel, 0.03f, 0.55f, 0.25f, 0.95f);
            bpLabel.GetComponent<Text>().color = Gold;
            bpLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var bpTier = AddText(bpBar, "BPTier", "Tier 12 / 50", 10, TextAnchor.MiddleLeft);
            SetAnchors(bpTier, 0.03f, 0.08f, 0.30f, 0.50f);
            bpTier.GetComponent<Text>().color = TextMid;

            var bpTrack = AddPanel(bpBar, "BPTrack", new Color(0.06f, 0.04f, 0.08f));
            SetAnchors(bpTrack, 0.30f, 0.25f, 0.85f, 0.75f);
            var bpFill = AddPanel(bpTrack, "BPFill", Gold);
            SetAnchors(bpFill, 0f, 0f, 0.24f, 1f);

            var bpClaimBtn = AddStyledButton(bpBar, "BPClaimBtn", "CLAIM", Gold, GoldDim);
            SetAnchors(bpClaimBtn, 0.87f, 0.15f, 0.98f, 0.85f);
            bpClaimBtn.transform.Find("Label").GetComponent<Text>().fontSize = 10;

            // Daily quest summary
            var questSummary = AddPanel(canvas, "QuestSummary", new Color(0.06f, 0.04f, 0.10f, 0.9f));
            SetAnchors(questSummary, 0.08f, 0.09f, 0.92f, 0.18f);
            AddOutlinePanel(questSummary, TealDim);

            var questLabel = AddText(questSummary, "QLabel", "DAILY QUESTS", 9, TextAnchor.MiddleLeft);
            SetAnchors(questLabel, 0.03f, 0.55f, 0.25f, 0.95f);
            questLabel.GetComponent<Text>().color = Teal;
            questLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var questProgress = AddText(questSummary, "QProgress", "3/5 Complete  •  Next: Win 2 PvP battles", 10, TextAnchor.MiddleLeft);
            SetAnchors(questProgress, 0.03f, 0.08f, 0.80f, 0.50f);
            questProgress.GetComponent<Text>().color = TextMid;

            var questDotsRow = AddPanel(questSummary, "QDots", new Color(0, 0, 0, 0));
            SetAnchors(questDotsRow, 0.30f, 0.55f, 0.70f, 0.90f);
            var dotsLayout = questDotsRow.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 8;
            for (int i = 0; i < 5; i++)
            {
                var dot = AddPanel(questDotsRow, $"Dot_{i}", i < 3 ? Teal : new Color(0.15f, 0.12f, 0.20f));
                dot.AddComponent<LayoutElement>().preferredWidth = 12;
            }

            SaveScene();
            Debug.Log("[SceneUIGenerator] Lobby scene: polished main menu hub");
        }

        // ===================================================================
        // COMBAT SCENE — Tactical battle HUD
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Combat")]
        public static void SetupCombatScene()
        {
            var scene = OpenScene("Combat");
            var canvasGo = FindOrCreateCanvas(scene);

            // Semi-transparent combat overlay bg (full screen)
            var bg = AddPanel(canvasGo, "CombatOverlay", new Color(0, 0, 0, 0));
            StretchToParent(bg);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR ===
            var topBar = AddPanel(canvas, "TopBar", BgPanel);
            SetAnchors(topBar, 0f, 0.93f, 1f, 1f);
            AddOutlinePanel(topBar, BorderDim);

            var phaseLabel = AddText(topBar, "PhaseLabel", "ACTION PHASE", 16, TextAnchor.MiddleCenter);
            SetAnchors(phaseLabel, 0.3f, 0.1f, 0.7f, 0.9f);
            phaseLabel.GetComponent<Text>().color = Gold;
            phaseLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var turnCounter = AddText(topBar, "TurnCounter", "Turn 3", 12, TextAnchor.MiddleLeft);
            SetAnchors(turnCounter, 0.02f, 0.1f, 0.15f, 0.9f);
            turnCounter.GetComponent<Text>().color = TextMid;

            var retreatBtn = AddStyledButton(topBar, "RetreatBtn", "RETREAT", BloodDark, new Color(0.3f, 0.05f, 0.08f));
            SetAnchors(retreatBtn, 0.85f, 0.15f, 0.98f, 0.85f);
            retreatBtn.transform.Find("Label").GetComponent<Text>().fontSize = 10;

            // === TURN ORDER (right side) ===
            var turnPanel = AddPanel(canvas, "TurnOrderPanel", BgPanel);
            SetAnchors(turnPanel, 0.90f, 0.35f, 0.99f, 0.92f);
            AddOutlinePanel(turnPanel, BorderDim);

            var toTitle = AddText(turnPanel, "TOTitle", "TURN ORDER", 8, TextAnchor.MiddleCenter);
            SetAnchors(toTitle, 0f, 0.92f, 1f, 1f);
            toTitle.GetComponent<Text>().color = GoldDim;

            var tokenArea = AddPanel(turnPanel, "TokenArea", new Color(0, 0, 0, 0));
            SetAnchors(tokenArea, 0.05f, 0.02f, 0.95f, 0.90f);
            var taLayout = tokenArea.AddComponent<VerticalLayoutGroup>();
            taLayout.spacing = 4;
            taLayout.padding = new RectOffset(2, 2, 2, 2);

            string[] heroNames = { "Kaelen", "Vorra", "Seraphyn", "Mordoc", "Lyra", "Skaros" };
            Color[] heroColors = { Blood, Ember, Purple, IronColor, Teal, BloodDark };
            for (int i = 0; i < 6; i++)
            {
                var token = AddPanel(tokenArea, $"Token_{i}", i < 3 ? new Color(0.12f, 0.15f, 0.25f) : new Color(0.25f, 0.10f, 0.10f));
                token.AddComponent<LayoutElement>().preferredHeight = 28;
                AddOutlinePanel(token, i == 0 ? Gold : BorderDim);
                var tIcon = AddPanel(token, "Icon", heroColors[i]);
                SetAnchors(tIcon, 0.05f, 0.1f, 0.35f, 0.9f);
                var tName = AddText(token, "Name", heroNames[i], 7, TextAnchor.MiddleLeft);
                SetAnchors(tName, 0.40f, 0f, 0.95f, 1f);
                tName.GetComponent<Text>().color = i == 0 ? Gold : TextMid;
            }

            // === PLAYER HERO STATUS (left side) ===
            var playerStatus = AddPanel(canvas, "PlayerHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(playerStatus, 0.01f, 0.55f, 0.14f, 0.92f);
            var psLayout = playerStatus.AddComponent<VerticalLayoutGroup>();
            psLayout.spacing = 4;

            string[] pHeroes = { "Kaelen", "Vorra", "Seraphyn" };
            float[] pHp = { 0.85f, 0.55f, 1.0f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(playerStatus, pHeroes[i], pHp[i], heroColors[i], true);

            // === ENEMY HERO STATUS (right-center) ===
            var enemyStatus = AddPanel(canvas, "EnemyHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(enemyStatus, 0.76f, 0.55f, 0.89f, 0.92f);
            var esLayout = enemyStatus.AddComponent<VerticalLayoutGroup>();
            esLayout.spacing = 4;

            string[] eHeroes = { "Mordoc", "Lyra", "Skaros" };
            float[] eHp = { 0.70f, 0.40f, 0.90f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(enemyStatus, eHeroes[i], eHp[i], heroColors[i + 3], false);

            // === ENERGY DISPLAY ===
            var energyPanel = AddPanel(canvas, "EnergyPanel", BgPanel);
            SetAnchors(energyPanel, 0.01f, 0.19f, 0.14f, 0.30f);
            AddOutlinePanel(energyPanel, SkyDim);

            var enLabel = AddText(energyPanel, "EnergyLabel", "ENERGY", 9, TextAnchor.MiddleCenter);
            SetAnchors(enLabel, 0f, 0.65f, 1f, 0.95f);
            enLabel.GetComponent<Text>().color = Sky;
            enLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var orbRow = AddPanel(energyPanel, "OrbRow", new Color(0, 0, 0, 0));
            SetAnchors(orbRow, 0.05f, 0.1f, 0.95f, 0.60f);
            var orbLayout = orbRow.AddComponent<HorizontalLayoutGroup>();
            orbLayout.spacing = 6;
            orbLayout.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < 4; i++)
            {
                var orb = AddPanel(orbRow, $"Orb_{i}", i < 3 ? BarEnergy : BarEnergyDim);
                orb.AddComponent<LayoutElement>().preferredWidth = 18;
                AddOutlinePanel(orb, i < 3 ? Sky : new Color(0.15f, 0.15f, 0.25f));
            }

            var enText = AddText(energyPanel, "EnergyCount", "3 / 4", 11, TextAnchor.MiddleCenter);
            SetAnchors(enText, 0f, 0.0f, 1f, 0.15f);
            enText.GetComponent<Text>().color = TextMid;

            // === CARD HAND (bottom) ===
            var cardTray = AddPanel(canvas, "CardTray", BgPanel);
            SetAnchors(cardTray, 0.10f, 0f, 0.88f, 0.19f);
            AddOutlinePanel(cardTray, BorderDim);

            var cardContainer = AddPanel(cardTray, "CardContainer", new Color(0, 0, 0, 0));
            SetAnchors(cardContainer, 0.01f, 0.03f, 0.99f, 0.97f);
            var ccLayout = cardContainer.AddComponent<HorizontalLayoutGroup>();
            ccLayout.spacing = 6;
            ccLayout.padding = new RectOffset(4, 4, 4, 4);
            ccLayout.childAlignment = TextAnchor.MiddleCenter;

            string[] cardNames = { "Fire Bolt", "Shield Wall", "Shadow Strike", "Heal Wave", "Ice Shard" };
            int[] cardCosts = { 1, 2, 3, 2, 1 };
            Color[] cardColors = { Ember, IronColor, Purple, Teal, Sky };
            string[] cardTypes = { "ATK", "DEF", "ATK", "HEAL", "ATK" };
            int[] cardDmg = { 45, 0, 72, 35, 38 };

            for (int i = 0; i < 5; i++)
                AddCardWidget(cardContainer, cardNames[i], cardCosts[i], cardColors[i], cardTypes[i], cardDmg[i]);

            // === END TURN BUTTON ===
            var endTurnBtn = AddStyledButton(canvas, "EndTurnButton", "END TURN", Blood, BloodDark);
            SetAnchors(endTurnBtn, 0.88f, 0.02f, 0.99f, 0.10f);
            endTurnBtn.transform.Find("Label").GetComponent<Text>().fontSize = 12;

            // === VICTORY PANEL (hidden, full screen overlay) ===
            var victoryPanel = AddPanel(canvasGo, "VictoryPanel", new Color(0.02f, 0.06f, 0.02f, 0.95f));
            StretchToParent(victoryPanel);
            var vicFrame = AddPanel(victoryPanel, "Frame", BgPanel);
            SetAnchors(vicFrame, 0.15f, 0.25f, 0.85f, 0.75f);
            AddOutlinePanel(vicFrame, Gold);
            var vicTitle = AddText(vicFrame, "Title", "VICTORY", 48, TextAnchor.MiddleCenter);
            SetAnchors(vicTitle, 0.1f, 0.6f, 0.9f, 0.9f);
            vicTitle.GetComponent<Text>().color = Gold;
            AddOutline(vicTitle, new Color(0.3f, 0.2f, 0.05f), 2f);
            var vicRewards = AddText(vicFrame, "Rewards", "+250 XP   +500 Gold   +3 Hero Shards", 14, TextAnchor.MiddleCenter);
            SetAnchors(vicRewards, 0.1f, 0.35f, 0.9f, 0.55f);
            vicRewards.GetComponent<Text>().color = TextLight;
            var vicContinue = AddStyledButton(vicFrame, "ContinueBtn", "CONTINUE", Gold, GoldDim);
            SetAnchors(vicContinue, 0.3f, 0.08f, 0.7f, 0.25f);
            victoryPanel.SetActive(false);

            // === DEFEAT PANEL (hidden, full screen overlay) ===
            var defeatPanel = AddPanel(canvasGo, "DefeatPanel", new Color(0.08f, 0.02f, 0.02f, 0.95f));
            StretchToParent(defeatPanel);
            var defFrame = AddPanel(defeatPanel, "Frame", BgPanel);
            SetAnchors(defFrame, 0.15f, 0.25f, 0.85f, 0.75f);
            AddOutlinePanel(defFrame, Blood);
            var defTitle = AddText(defFrame, "Title", "DEFEAT", 48, TextAnchor.MiddleCenter);
            SetAnchors(defTitle, 0.1f, 0.6f, 0.9f, 0.9f);
            defTitle.GetComponent<Text>().color = Blood;
            AddOutline(defTitle, new Color(0.3f, 0.05f, 0.05f), 2f);
            var defMsg = AddText(defFrame, "Message", "Your heroes have fallen. Regroup and try again.", 14, TextAnchor.MiddleCenter);
            SetAnchors(defMsg, 0.1f, 0.35f, 0.9f, 0.55f);
            defMsg.GetComponent<Text>().color = TextMid;
            var defRetry = AddStyledButton(defFrame, "RetryBtn", "RETRY", Blood, BloodDark);
            SetAnchors(defRetry, 0.1f, 0.08f, 0.48f, 0.25f);
            var defQuit = AddStyledButton(defFrame, "QuitBtn", "RETREAT", new Color(0.3f, 0.25f, 0.2f), BgMid);
            SetAnchors(defQuit, 0.52f, 0.08f, 0.9f, 0.25f);
            defeatPanel.SetActive(false);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Combat scene: full battle HUD with cards, heroes, energy");
        }

        // ===================================================================
        // EMPIRE SCENE — City builder HUD
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Empire")]
        public static void SetupEmpireScene()
        {
            var scene = OpenScene("Empire");
            var canvasGo = FindOrCreateCanvas(scene);

            // Background — dark fantasy base with layered gradients for depth
            var bg = AddPanel(canvasGo, "Background", new Color(0.03f, 0.02f, 0.06f, 0.94f));
            StretchToParent(bg);
            // Sky gradient at top — subtle purple/blue atmospheric effect
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.08f, 0.05f, 0.16f, 0.45f));
            SetAnchors(skyGrad, 0f, 0.75f, 1f, 1f);
            // Ground gradient at bottom — darker for depth
            var groundGrad = AddPanel(bg, "GroundGradient", new Color(0.02f, 0.01f, 0.03f, 0.5f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.25f);
            // Vignette left edge
            var vigLeft = AddPanel(bg, "VignetteL", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigLeft, 0f, 0f, 0.08f, 1f);
            // Vignette right edge
            var vigRight = AddPanel(bg, "VignetteR", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigRight, 0.92f, 0f, 1f, 1f);

            // Notch/Dynamic Island fill — dark bar that extends above safe area
            // Covers the full top area behind iPhone notch/Dynamic Island so it blends
            // with the resource bar background. Fully opaque to hide the cutout.
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);
            // Gold border at bottom of notch fill (matches resource bar border)
            var notchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.70f));
            SetAnchors(notchBorder, 0f, 0f, 1f, 0.008f);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === RESOURCE BAR (top strip — slightly taller for readability) ===
            var resBarBg = AddPanel(canvas, "ResourceBarBg", new Color(0.03f, 0.02f, 0.06f, 0.96f));
            SetAnchors(resBarBg, 0f, 0.957f, 1f, 0.995f);
            // Gold bottom border
            var resBarBorder = AddPanel(resBarBg, "BottomBorder", new Color(0.72f, 0.56f, 0.22f, 0.70f));
            SetAnchors(resBarBorder, 0f, 0f, 1f, 0.035f);

            // Layout container — sits on top of the bar background
            var resBar = AddPanel(canvas, "ResourceBar", new Color(0, 0, 0, 0));
            SetAnchors(resBar, 0f, 0.957f, 1f, 0.995f);

            var resLayout = resBar.AddComponent<HorizontalLayoutGroup>();
            resLayout.spacing = 2;
            resLayout.padding = new RectOffset(8, 6, 4, 4);
            resLayout.childAlignment = TextAnchor.MiddleCenter;
            resLayout.childControlWidth = true;
            resLayout.childControlHeight = true;
            resLayout.childForceExpandWidth = false;
            resLayout.childForceExpandHeight = false;

            // Flat layout: icon+amount pairs with thin separators between
            AddResIconFlat(resBar, "Grain", GrainColor);
            AddResAmountFlat(resBar, "Grain", "8.37M");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Iron", IronColor);
            AddResAmountFlat(resBar, "Iron", "365K");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Stone", StoneColor);
            AddResAmountFlat(resBar, "Stone", "4.73M");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Arcane", ArcaneColor);
            AddResAmountFlat(resBar, "Arcane", "3.69M");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Gems", GemsColor);
            AddResAmountFlat(resBar, "Gems", "8.38K");

            // Flexible spacer to push "+" to the right
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(resBar.transform, false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1;

            // "+" button — bright green panel (AddPanel is proven to render)
            var plusBtn = AddPanel(resBar, "AddBtn", new Color(0.20f, 0.65f, 0.32f, 1f));
            var plusLE = plusBtn.AddComponent<LayoutElement>();
            plusLE.preferredWidth = 32;
            plusLE.preferredHeight = 32;
            plusLE.minWidth = 28;
            plusLE.minHeight = 28;
            plusLE.flexibleWidth = 0;
            plusBtn.AddComponent<Button>();
            // "+" text
            var plusText = AddText(plusBtn, "Label", "+", 16, TextAnchor.MiddleCenter);
            StretchToParent(plusText);
            plusText.GetComponent<Text>().color = Color.white;
            plusText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === PLAYER AVATAR BLOCK — same width as build queue (0.01–0.18) ===
            var avatarBlock = AddPanel(canvas, "AvatarBlock", new Color(0.05f, 0.03f, 0.09f, 0.95f));
            SetAnchors(avatarBlock, 0.01f, 0.875f, 0.18f, 0.955f);
            // Double gold border — outer + inner with gap for premium frame
            AddOutlinePanel(avatarBlock, new Color(0.82f, 0.65f, 0.28f, 0.85f));
            var avatarInnerBorder = AddPanel(avatarBlock, "InnerBorder", new Color(0, 0, 0, 0));
            SetAnchors(avatarInnerBorder, 0.04f, 0.03f, 0.96f, 0.97f);
            AddOutlinePanel(avatarInnerBorder, new Color(0.55f, 0.42f, 0.18f, 0.50f));
            // Portrait fill
            var avatarPortrait = AddPanel(avatarBlock, "Portrait", new Color(0.28f, 0.14f, 0.42f, 0.95f));
            SetAnchors(avatarPortrait, 0.06f, 0.05f, 0.94f, 0.95f);
            // Subtle inner light on portrait (top highlight)
            var avatarHighlight = AddPanel(avatarPortrait, "Highlight", new Color(0.50f, 0.35f, 0.65f, 0.15f));
            SetAnchors(avatarHighlight, 0f, 0.60f, 1f, 1f);
            // Level badge — gold pill, bottom-center, overlapping frame
            var lvlBadge = AddPanel(avatarBlock, "LevelBadge", new Color(0.72f, 0.56f, 0.22f, 1f));
            SetAnchors(lvlBadge, 0.22f, -0.06f, 0.78f, 0.12f);
            AddOutlinePanel(lvlBadge, new Color(0.45f, 0.34f, 0.12f, 0.9f));
            var lvlText = AddText(lvlBadge, "Level", "Lv.42", 8, TextAnchor.MiddleCenter);
            StretchToParent(lvlText);
            lvlText.GetComponent<Text>().color = TextWhite;
            lvlText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lvlShadow = lvlText.AddComponent<Shadow>();
            lvlShadow.effectColor = new Color(0, 0, 0, 0.7f);
            lvlShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === INFO PANEL — right of avatar, premium dark panel with layered depth ===
            // Background panel with subtle gradient
            var infoPanelBg = AddPanel(canvas, "InfoPanelBg", new Color(0.04f, 0.03f, 0.08f, 0.90f));
            SetAnchors(infoPanelBg, 0.19f, 0.910f, 0.84f, 0.955f);
            // Top highlight gradient for glass effect
            var infoTopGrad = AddPanel(infoPanelBg, "TopGrad", new Color(0.12f, 0.08f, 0.18f, 0.25f));
            SetAnchors(infoTopGrad, 0f, 0.55f, 1f, 1f);
            // Left accent strip — thin gold vertical line
            var infoLeftAccent = AddPanel(infoPanelBg, "LeftAccent", new Color(0.72f, 0.56f, 0.22f, 0.50f));
            SetAnchors(infoLeftAccent, 0f, 0.08f, 0.008f, 0.92f);
            // Bottom border — subtle gold
            var infoBotBorder = AddPanel(infoPanelBg, "BotBorder", new Color(0.55f, 0.42f, 0.18f, 0.30f));
            SetAnchors(infoBotBorder, 0.02f, 0f, 0.98f, 0.025f);

            // === TOP ROW: Player Name + VIP Badge ===
            // Player name — warm gold, clean typography with outline for readability
            var avatarName = AddText(infoPanelBg, "PlayerName", "Commander", 13, TextAnchor.MiddleLeft);
            SetAnchors(avatarName, 0.04f, 0.52f, 0.52f, 0.96f);
            avatarName.GetComponent<Text>().color = new Color(0.92f, 0.78f, 0.38f, 1f);
            avatarName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = avatarName.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1f, -1f);
            var nameOutline = avatarName.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0.15f, 0.10f, 0.05f, 0.5f);
            nameOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // VIP badge — layered purple/gold premium pill
            var vipOuter = AddPanel(infoPanelBg, "VipOuter", new Color(0.60f, 0.45f, 0.18f, 0.70f));
            SetAnchors(vipOuter, 0.54f, 0.56f, 0.78f, 0.94f);
            var vipBadge = AddPanel(vipOuter, "VipBadge", new Color(0.42f, 0.12f, 0.55f, 0.95f));
            SetAnchors(vipBadge, 0.04f, 0.06f, 0.96f, 0.94f);
            // Inner shimmer
            var vipShimmer = AddPanel(vipBadge, "Shimmer", new Color(0.65f, 0.40f, 0.85f, 0.20f));
            SetAnchors(vipShimmer, 0f, 0.45f, 1f, 1f);
            var vipText = AddText(vipBadge, "Label", "VIP 11", 9, TextAnchor.MiddleCenter);
            StretchToParent(vipText);
            vipText.GetComponent<Text>().color = new Color(1f, 0.95f, 0.75f, 1f);
            vipText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vipShadow = vipText.AddComponent<Shadow>();
            vipShadow.effectColor = new Color(0, 0, 0, 0.8f);
            vipShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Server tag — small muted tag at right
            var serverTag = AddText(infoPanelBg, "ServerTag", "S:142", 8, TextAnchor.MiddleRight);
            SetAnchors(serverTag, 0.80f, 0.58f, 0.98f, 0.94f);
            serverTag.GetComponent<Text>().color = new Color(0.40f, 0.38f, 0.35f, 0.65f);

            // === BOTTOM ROW: Power + Coordinates ===
            // Power icon — golden swords with glow
            var powerIconText = AddText(infoPanelBg, "PowerIcon", "\u2694", 11, TextAnchor.MiddleCenter);
            SetAnchors(powerIconText, 0.03f, 0.06f, 0.10f, 0.50f);
            powerIconText.GetComponent<Text>().color = new Color(0.92f, 0.75f, 0.32f, 1f);
            var piShadow = powerIconText.AddComponent<Shadow>();
            piShadow.effectColor = new Color(0.50f, 0.35f, 0.10f, 0.5f);
            piShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Power value — bright white, larger, prominent
            var powerVal = AddText(infoPanelBg, "PowerValue", "355,582,021", 12, TextAnchor.MiddleLeft);
            SetAnchors(powerVal, 0.10f, 0.04f, 0.58f, 0.52f);
            powerVal.GetComponent<Text>().color = new Color(0.95f, 0.93f, 0.88f, 1f);
            powerVal.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pvShadow = powerVal.AddComponent<Shadow>();
            pvShadow.effectColor = new Color(0, 0, 0, 0.85f);
            pvShadow.effectDistance = new Vector2(1f, -1f);

            // Thin vertical separator before coords
            var coordSep = AddPanel(infoPanelBg, "CoordSep", new Color(0.40f, 0.32f, 0.18f, 0.25f));
            SetAnchors(coordSep, 0.60f, 0.12f, 0.605f, 0.46f);

            // Coordinates — clean, muted, right-aligned
            var coordText = AddText(infoPanelBg, "Coords", "K:12  X:482  Y:317", 8, TextAnchor.MiddleRight);
            SetAnchors(coordText, 0.62f, 0.06f, 0.98f, 0.50f);
            coordText.GetComponent<Text>().color = new Color(0.50f, 0.48f, 0.42f, 0.75f);
            var coordShadow = coordText.AddComponent<Shadow>();
            coordShadow.effectColor = new Color(0, 0, 0, 0.6f);
            coordShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === LEFT SIDEBAR — Build/Research queue (below avatar block, same width) ===
            var leftSidebar = AddPanel(canvas, "LeftSidebar", new Color(0, 0, 0, 0));
            SetAnchors(leftSidebar, 0.01f, 0.55f, 0.18f, 0.873f);

            // 4 compact queue strips with tight spacing
            AddQueueSlot(leftSidebar, "BuildSlot1", "Build", "2:34:15", Ember, true, 0.77f, 0.99f);
            AddQueueSlot(leftSidebar, "BuildSlot2", "Build", "IDLE", EmberDim, false, 0.53f, 0.75f);
            AddQueueSlot(leftSidebar, "ResearchSlot", "Research", "IDLE", Sky, false, 0.29f, 0.51f);
            AddQueueSlot(leftSidebar, "TrainingSlot", "Training", "IDLE", Purple, false, 0.05f, 0.27f);

            // === RIGHT SIDEBAR — Event buttons (P&C: compact, nearly square, tight stacking) ===
            float rbX0 = 0.86f, rbX1 = 0.99f; // 13% wide
            float rbH = 0.068f; // ~7% tall each
            float rbGap = 0.005f;
            float rbTop = 0.955f;
            AddEventButton(canvas, "EventBtn1", "Events", Ember,   rbX0, rbTop - rbH, rbX1, rbTop, "10:04:49");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn2", "VS", new Color(0.4f, 0.3f, 0.8f, 1f), rbX0, rbTop - rbH, rbX1, rbTop, "");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn3", "Rewards", Gold,   rbX0, rbTop - rbH, rbX1, rbTop, "");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn4", "Offer", Blood,    rbX0, rbTop - rbH, rbX1, rbTop, "");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn5", "Gifts", Purple,   rbX0, rbTop - rbH, rbX1, rbTop, "23:59:52");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn6", "Shop", Teal,      rbX0, rbTop - rbH, rbX1, rbTop, "");
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn7", "Arena", new Color(0.6f, 0.25f, 0.15f, 1f), rbX0, rbTop - rbH, rbX1, rbTop, "");

            // === CHAT TICKER — Alliance messages (semi-transparent bar with gold trim like P&C) ===
            var chatTicker = AddPanel(canvas, "ChatTicker", new Color(0.03f, 0.02f, 0.06f, 0.82f));
            SetAnchors(chatTicker, 0f, 0.14f, 1f, 0.185f);
            // Top gold trim — brighter
            var chatTopBorder = AddPanel(chatTicker, "TopBorder", new Color(0.68f, 0.52f, 0.20f, 0.55f));
            SetAnchors(chatTopBorder, 0f, 0.95f, 1f, 1f);
            // Top glow
            var chatTopGlow = AddPanel(chatTicker, "TopGlow", new Color(0.50f, 0.38f, 0.12f, 0.12f));
            SetAnchors(chatTopGlow, 0f, 0.82f, 1f, 0.95f);
            // Bottom gold trim
            var chatBotBorder = AddPanel(chatTicker, "BottomBorder", new Color(0.55f, 0.42f, 0.15f, 0.35f));
            SetAnchors(chatBotBorder, 0f, 0f, 1f, 0.05f);
            // Chat horn/megaphone icon (teal like P&C)
            var chatIcon = AddPanel(chatTicker, "ChatIcon", new Color(0.2f, 0.70f, 0.55f, 0.95f));
            SetAnchors(chatIcon, 0.02f, 0.18f, 0.055f, 0.82f);
            // Line 1 — top half
            var chatLine1 = AddText(chatTicker, "ChatLine1", "NBAHeartless: launched a rally at Lv. 17 Monster De...", 10, TextAnchor.MiddleLeft);
            SetAnchors(chatLine1, 0.07f, 0.50f, 0.98f, 1f);
            chatLine1.GetComponent<Text>().color = TextLight;
            var chat1Shadow = chatLine1.AddComponent<Shadow>();
            chat1Shadow.effectColor = new Color(0, 0, 0, 0.8f);
            chat1Shadow.effectDistance = new Vector2(1f, -1f);
            // Line 2 — bottom half (P&C shows 2 lines)
            var chatLine2 = AddText(chatTicker, "ChatLine2", "TrueDictator237: launched a rally at Lv. 39 Monster...", 10, TextAnchor.MiddleLeft);
            SetAnchors(chatLine2, 0.07f, 0f, 0.98f, 0.50f);
            chatLine2.GetComponent<Text>().color = new Color(0.3f, 0.85f, 0.55f, 0.85f);
            var chat2Shadow = chatLine2.AddComponent<Shadow>();
            chat2Shadow.effectColor = new Color(0, 0, 0, 0.8f);
            chat2Shadow.effectDistance = new Vector2(1f, -1f);

            // === UPGRADE BANNER (above nav) — gradient button with gold accents like P&C ===
            var upgradeBanner = AddPanel(canvas, "UpgradeBanner", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(upgradeBanner, 0.03f, 0.102f, 0.97f, 0.14f);
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (btnOrnateSpr != null) { upgradeBanner.GetComponent<Image>().sprite = btnOrnateSpr; upgradeBanner.GetComponent<Image>().type = Image.Type.Sliced; upgradeBanner.GetComponent<Image>().color = new Color(0.80f, 0.72f, 0.60f, 1f); }
            else { AddOutlinePanel(upgradeBanner, new Color(0.55f, 0.42f, 0.18f, 0.7f)); }
            // Top gradient highlight for depth
            var upgGrad = AddPanel(upgradeBanner, "Gradient", new Color(0.20f, 0.15f, 0.08f, 0.25f));
            SetAnchors(upgGrad, 0.02f, 0.50f, 0.98f, 1f);
            // Left gold chevron accent
            var upgLeftArrow = AddPanel(upgradeBanner, "LeftArrow", new Color(0.80f, 0.64f, 0.24f, 0.90f));
            SetAnchors(upgLeftArrow, 0.01f, 0.18f, 0.04f, 0.82f);
            var upgLeftInner = AddPanel(upgradeBanner, "LeftArrowInner", new Color(0.65f, 0.50f, 0.18f, 0.6f));
            SetAnchors(upgLeftInner, 0.045f, 0.25f, 0.06f, 0.75f);
            // Center text with stronger styling
            var upgradeText = AddText(upgradeBanner, "Text", "Upgrade Stronghold to Lv.6", 14, TextAnchor.MiddleCenter);
            SetAnchors(upgradeText, 0.07f, 0f, 0.93f, 1f);
            upgradeText.GetComponent<Text>().color = new Color(1f, 0.96f, 0.78f, 1f);
            upgradeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var upgShadow = upgradeText.AddComponent<Shadow>();
            upgShadow.effectColor = new Color(0, 0, 0, 0.9f);
            upgShadow.effectDistance = new Vector2(2f, -2f);
            var upgOutline = upgradeText.AddComponent<Outline>();
            upgOutline.effectColor = new Color(0, 0, 0, 0.4f);
            upgOutline.effectDistance = new Vector2(0.5f, -0.5f);
            // Right gold chevron accent
            var upgRightArrow = AddPanel(upgradeBanner, "RightArrow", new Color(0.80f, 0.64f, 0.24f, 0.90f));
            SetAnchors(upgRightArrow, 0.96f, 0.18f, 0.99f, 0.82f);
            var upgRightInner = AddPanel(upgradeBanner, "RightArrowInner", new Color(0.65f, 0.50f, 0.18f, 0.6f));
            SetAnchors(upgRightInner, 0.94f, 0.25f, 0.955f, 0.75f);
            upgradeBanner.AddComponent<Button>();

            // === BOTTOM NAV BAR — exceeds P&C: layered ornate dark bar with raised center ===
            // Dark strip behind home indicator (outside safe area, full screen)
            var navBarBg = AddPanel(canvasGo, "NavBarBg", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(navBarBg, 0f, 0f, 1f, 0.06f);

            var navBar = AddPanel(canvas, "BottomNavBar", new Color(0.04f, 0.03f, 0.07f, 0.98f));
            SetAnchors(navBar, 0f, 0f, 1f, 0.10f);

            // === Triple-layer top border (P&C uses double — we use triple for premium feel) ===
            var navBorderGold = AddPanel(navBar, "TopBorderGold", new Color(0.85f, 0.68f, 0.28f, 0.95f));
            SetAnchors(navBorderGold, 0f, 0.972f, 1f, 1f);
            var navBorderDark = AddPanel(navBar, "TopBorderDark", new Color(0.35f, 0.25f, 0.10f, 0.80f));
            SetAnchors(navBorderDark, 0f, 0.955f, 1f, 0.972f);
            var navBorderThin = AddPanel(navBar, "TopBorderThin", new Color(0.72f, 0.56f, 0.22f, 0.55f));
            SetAnchors(navBorderThin, 0f, 0.948f, 1f, 0.955f);
            // Warm glow cascade (3 layers, fading down)
            var navGlow1 = AddPanel(navBar, "TopGlow1", new Color(0.72f, 0.55f, 0.22f, 0.14f));
            SetAnchors(navGlow1, 0f, 0.90f, 1f, 0.948f);
            var navGlow2 = AddPanel(navBar, "TopGlow2", new Color(0.55f, 0.40f, 0.15f, 0.07f));
            SetAnchors(navGlow2, 0f, 0.85f, 1f, 0.90f);
            var navGlow3 = AddPanel(navBar, "TopGlow3", new Color(0.40f, 0.28f, 0.10f, 0.03f));
            SetAnchors(navGlow3, 0f, 0.80f, 1f, 0.85f);
            // Bottom fade for home indicator
            var navBotFade = AddPanel(navBar, "BotFade", new Color(0.02f, 0.01f, 0.04f, 0.6f));
            SetAnchors(navBotFade, 0f, 0f, 1f, 0.12f);

            // === Nav items — 3 left, CENTER raised button, 3 right ===
            var navLayoutLeft = AddPanel(navBar, "NavLeft", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutLeft, 0f, 0.02f, 0.38f, 0.94f);
            var nllLayout = navLayoutLeft.AddComponent<HorizontalLayoutGroup>();
            nllLayout.spacing = 0;
            nllLayout.padding = new RectOffset(4, 0, 4, 6);
            nllLayout.childForceExpandWidth = true;
            nllLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutLeft, "NavWorld", "WORLD", Ember, true, 0);
            AddNavItem(navLayoutLeft, "NavHero", "HERO", Purple, false, 0);
            AddNavItem(navLayoutLeft, "NavQuest", "QUEST", Teal, false, 17);

            // === CENTER BUTTON — raised ornate button (extends above bar like P&C) ===
            // Outer glow/shadow behind the raised button
            var centerShadow = AddPanel(navBar, "CenterShadow", new Color(0.72f, 0.55f, 0.20f, 0.06f));
            SetAnchors(centerShadow, 0.33f, 0.05f, 0.67f, 1.18f);
            // Main button body
            var centerBtn = AddPanel(navBar, "NavCenterBtn", new Color(0.08f, 0.05f, 0.14f, 0.98f));
            SetAnchors(centerBtn, 0.35f, 0.06f, 0.65f, 1.14f);
            // Triple gold border: outer bright, dark gap, inner warm
            AddOutlinePanel(centerBtn, new Color(0.85f, 0.68f, 0.28f, 0.95f));
            var centerBorderMid = AddPanel(centerBtn, "BorderMid", new Color(0, 0, 0, 0));
            SetAnchors(centerBorderMid, 0.02f, 0.02f, 0.98f, 0.98f);
            AddOutlinePanel(centerBorderMid, new Color(0.35f, 0.25f, 0.10f, 0.70f));
            var centerBorderInner = AddPanel(centerBtn, "BorderInner", new Color(0, 0, 0, 0));
            SetAnchors(centerBorderInner, 0.04f, 0.03f, 0.96f, 0.97f);
            AddOutlinePanel(centerBorderInner, new Color(0.65f, 0.50f, 0.20f, 0.45f));
            // Inner dark fill
            var centerInner = AddPanel(centerBtn, "Inner", new Color(0.05f, 0.03f, 0.09f, 0.95f));
            SetAnchors(centerInner, 0.05f, 0.04f, 0.95f, 0.96f);
            // Top highlight (glass effect)
            var centerHighlight = AddPanel(centerInner, "Highlight", new Color(0.15f, 0.10f, 0.22f, 0.30f));
            SetAnchors(centerHighlight, 0.05f, 0.55f, 0.95f, 0.95f);
            // Ember glow behind icon — warm radial
            var centerGlow = AddPanel(centerInner, "Glow", new Color(0.91f, 0.45f, 0.16f, 0.12f));
            SetAnchors(centerGlow, 0.08f, 0.22f, 0.92f, 0.82f);
            var centerGlowInner = AddPanel(centerInner, "GlowInner", new Color(0.95f, 0.55f, 0.20f, 0.08f));
            SetAnchors(centerGlowInner, 0.18f, 0.32f, 0.82f, 0.72f);
            // Icon — transparent bg, sprite only
            var centerIcon = AddPanel(centerInner, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(centerIcon, 0.15f, 0.26f, 0.85f, 0.82f);
            var empSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_empire.png");
            if (empSpr != null)
            {
                centerIcon.GetComponent<Image>().sprite = empSpr;
                centerIcon.GetComponent<Image>().preserveAspect = true;
                centerIcon.GetComponent<Image>().color = new Color(1f, 0.92f, 0.72f, 1f);
            }
            else { centerIcon.GetComponent<Image>().color = Ember; }
            // Label — warm gold with outline for crispness
            var centerLabel = AddText(centerInner, "Label", "EMPIRE", 10, TextAnchor.MiddleCenter);
            SetAnchors(centerLabel, 0f, 0.02f, 1f, 0.24f);
            centerLabel.GetComponent<Text>().color = new Color(1f, 0.92f, 0.70f, 1f);
            centerLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clShadow = centerLabel.AddComponent<Shadow>();
            clShadow.effectColor = new Color(0, 0, 0, 0.9f);
            clShadow.effectDistance = new Vector2(1f, -1f);
            // Top gold accent crown on raised button
            var centerTopAccent = AddPanel(centerBtn, "TopAccent", new Color(0.88f, 0.70f, 0.28f, 0.92f));
            SetAnchors(centerTopAccent, 0.08f, 0.975f, 0.92f, 1f);
            // Second accent — darker below
            var centerTopAccent2 = AddPanel(centerBtn, "TopAccent2", new Color(0.55f, 0.42f, 0.18f, 0.50f));
            SetAnchors(centerTopAccent2, 0.12f, 0.96f, 0.88f, 0.975f);
            centerBtn.AddComponent<Button>();

            // Right nav items
            var navLayoutRight = AddPanel(navBar, "NavRight", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutRight, 0.62f, 0.02f, 1f, 0.94f);
            var nlrLayout = navLayoutRight.AddComponent<HorizontalLayoutGroup>();
            nlrLayout.spacing = 0;
            nlrLayout.padding = new RectOffset(0, 4, 4, 6);
            nlrLayout.childForceExpandWidth = true;
            nlrLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutRight, "NavBag", "BAG", GoldDim, false, 3);
            AddNavItem(navLayoutRight, "NavMail", "MAIL", Sky, false, 5);
            AddNavItem(navLayoutRight, "NavAlliance", "ALLIANCE", TealDim, false, 0);

            // === RESOURCE DETAIL POPUP (hidden, full screen overlay) ===
            var resPopup = AddPanel(canvasGo, "ResourceDetailPopup", new Color(0, 0, 0, 0.6f));
            StretchToParent(resPopup);

            var resFrame = AddPanel(resPopup, "Frame", ResBarBg);
            SetAnchors(resFrame, 0.08f, 0.30f, 0.92f, 0.80f);
            AddOutlinePanel(resFrame, GoldDim);

            // Header
            var resHeader = AddPanel(resFrame, "Header", new Color(0.08f, 0.10f, 0.18f, 1f));
            SetAnchors(resHeader, 0f, 0.88f, 1f, 1f);
            var resTitle = AddText(resHeader, "Title", "STONE", 18, TextAnchor.MiddleCenter);
            StretchToParent(resTitle);
            resTitle.GetComponent<Text>().color = Gold;
            resTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            // Header bottom border
            var resHeaderBorder = AddPanel(resFrame, "HeaderBorder", GoldDim);
            SetAnchors(resHeaderBorder, 0.03f, 0.875f, 0.97f, 0.88f);

            // Current amount row
            var resCurrentLabel = AddText(resFrame, "CurrentLabel", "Current:", 12, TextAnchor.MiddleLeft);
            SetAnchors(resCurrentLabel, 0.05f, 0.74f, 0.40f, 0.85f);
            resCurrentLabel.GetComponent<Text>().color = TextMid;
            var resCurrentVal = AddText(resFrame, "CurrentValue", "4,730,000", 16, TextAnchor.MiddleRight);
            SetAnchors(resCurrentVal, 0.50f, 0.74f, 0.95f, 0.85f);
            resCurrentVal.GetComponent<Text>().color = TextWhite;
            resCurrentVal.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Capacity row
            var resCapLabel = AddText(resFrame, "CapacityLabel", "Capacity:", 12, TextAnchor.MiddleLeft);
            SetAnchors(resCapLabel, 0.05f, 0.63f, 0.40f, 0.74f);
            resCapLabel.GetComponent<Text>().color = TextMid;
            var resCapVal = AddText(resFrame, "CapacityValue", "10,000,000", 14, TextAnchor.MiddleRight);
            SetAnchors(resCapVal, 0.50f, 0.63f, 0.95f, 0.74f);
            resCapVal.GetComponent<Text>().color = TextLight;

            // Capacity bar
            var resCapBarBg = AddPanel(resFrame, "CapBarBg", new Color(0.10f, 0.08f, 0.16f, 1f));
            SetAnchors(resCapBarBg, 0.05f, 0.57f, 0.95f, 0.62f);
            var resCapBarFill = AddPanel(resCapBarBg, "Fill", StoneColor);
            SetAnchors(resCapBarFill, 0f, 0f, 0.47f, 1f); // 47% full

            // Production rate
            var resProdLabel = AddText(resFrame, "ProductionLabel", "Production:", 12, TextAnchor.MiddleLeft);
            SetAnchors(resProdLabel, 0.05f, 0.45f, 0.40f, 0.55f);
            resProdLabel.GetComponent<Text>().color = TextMid;
            var resProdVal = AddText(resFrame, "ProductionValue", "+12,500/hr", 14, TextAnchor.MiddleRight);
            SetAnchors(resProdVal, 0.50f, 0.45f, 0.95f, 0.55f);
            resProdVal.GetComponent<Text>().color = Teal;

            // Protected amount
            var resProtLabel = AddText(resFrame, "ProtectedLabel", "Protected:", 12, TextAnchor.MiddleLeft);
            SetAnchors(resProtLabel, 0.05f, 0.34f, 0.40f, 0.44f);
            resProtLabel.GetComponent<Text>().color = TextMid;
            var resProtVal = AddText(resFrame, "ProtectedValue", "2,000,000", 14, TextAnchor.MiddleRight);
            SetAnchors(resProtVal, 0.50f, 0.34f, 0.95f, 0.44f);
            resProtVal.GetComponent<Text>().color = Sky;

            // Separator
            var resDetailSep = AddPanel(resFrame, "DetailSep", new Color(0.20f, 0.18f, 0.30f, 0.5f));
            SetAnchors(resDetailSep, 0.05f, 0.28f, 0.95f, 0.285f);

            // Sources section
            var resSrcTitle = AddText(resFrame, "SourcesTitle", "SOURCES", 11, TextAnchor.MiddleLeft);
            SetAnchors(resSrcTitle, 0.05f, 0.20f, 0.40f, 0.28f);
            resSrcTitle.GetComponent<Text>().color = Gold;
            resSrcTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var resSrc1 = AddText(resFrame, "Source1", "Stone Quarry Lv.5  ×3", 10, TextAnchor.MiddleLeft);
            SetAnchors(resSrc1, 0.05f, 0.13f, 0.60f, 0.21f);
            resSrc1.GetComponent<Text>().color = TextLight;
            var resSrc1Val = AddText(resFrame, "Source1Val", "+9,000/hr", 10, TextAnchor.MiddleRight);
            SetAnchors(resSrc1Val, 0.60f, 0.13f, 0.95f, 0.21f);
            resSrc1Val.GetComponent<Text>().color = Teal;

            var resSrc2 = AddText(resFrame, "Source2", "Alliance Bonus", 10, TextAnchor.MiddleLeft);
            SetAnchors(resSrc2, 0.05f, 0.06f, 0.60f, 0.14f);
            resSrc2.GetComponent<Text>().color = TextLight;
            var resSrc2Val = AddText(resFrame, "Source2Val", "+3,500/hr", 10, TextAnchor.MiddleRight);
            SetAnchors(resSrc2Val, 0.60f, 0.06f, 0.95f, 0.14f);
            resSrc2Val.GetComponent<Text>().color = Teal;

            // Close button
            var resCloseBtn = AddStyledButton(resFrame, "CloseBtn", "CLOSE", new Color(0.25f, 0.22f, 0.30f), BgMid);
            SetAnchors(resCloseBtn, 0.35f, 0.01f, 0.65f, 0.08f);
            resCloseBtn.transform.Find("Label").GetComponent<Text>().fontSize = 11;

            resPopup.SetActive(false);

            // === BUILDING INFO POPUP (hidden, full screen overlay) ===
            var infoPopup = AddPanel(canvasGo, "BuildingInfoPopup", new Color(0, 0, 0, 0.6f));
            StretchToParent(infoPopup);
            var infoFrame = AddPanel(infoPopup, "Frame", BgPanel);
            SetAnchors(infoFrame, 0.10f, 0.25f, 0.90f, 0.75f);
            AddOutlinePanel(infoFrame, Gold);
            var infoTitle = AddText(infoFrame, "Title", "BARRACKS", 22, TextAnchor.MiddleCenter);
            SetAnchors(infoTitle, 0.1f, 0.80f, 0.9f, 0.95f);
            infoTitle.GetComponent<Text>().color = Gold;
            var infoDesc = AddText(infoFrame, "Desc", "Trains military units. Increases army capacity by 50 per level.", 13, TextAnchor.MiddleCenter);
            SetAnchors(infoDesc, 0.1f, 0.55f, 0.9f, 0.75f);
            infoDesc.GetComponent<Text>().color = TextLight;
            var infoCosts = AddText(infoFrame, "Costs", "Upgrade Cost:  1,200 Stone  •  800 Iron  •  600 Grain", 11, TextAnchor.MiddleCenter);
            SetAnchors(infoCosts, 0.05f, 0.38f, 0.95f, 0.52f);
            infoCosts.GetComponent<Text>().color = TextMid;
            var infoUpBtn = AddStyledButton(infoFrame, "UpgradeBtn", "UPGRADE  (2h 30m)", Gold, GoldDim);
            SetAnchors(infoUpBtn, 0.15f, 0.10f, 0.55f, 0.30f);
            var infoClose = AddStyledButton(infoFrame, "CloseBtn", "CLOSE", new Color(0.3f, 0.25f, 0.2f), BgMid);
            SetAnchors(infoClose, 0.60f, 0.10f, 0.85f, 0.30f);
            infoPopup.SetActive(false);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Empire scene: resource HUD, build queue, toolbar");
        }

        // ===================================================================
        // WORLD MAP SCENE — Territory control
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/World Map")]
        public static void SetupWorldMapScene()
        {
            var scene = OpenScene("WorldMap");
            var canvasGo = FindOrCreateCanvas(scene);

            // Dark map background (full screen)
            var bg = AddPanel(canvasGo, "MapBackground", new Color(0.06f, 0.08f, 0.04f, 1f));
            StretchToParent(bg);

            // Map grid overlay (full screen, behind safe area)
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    float x = 0.08f + c * 0.145f;
                    float y = 0.25f + r * 0.13f;
                    Color tileColor;
                    if ((r + c) % 3 == 0) tileColor = new Color(0.15f, 0.25f, 0.12f, 0.4f); // Allied
                    else if ((r * c) % 5 == 0) tileColor = new Color(0.25f, 0.10f, 0.10f, 0.4f); // Enemy
                    else tileColor = new Color(0.12f, 0.12f, 0.10f, 0.3f); // Neutral

                    var tile = AddPanel(canvasGo, $"Tile_{r}_{c}", tileColor);
                    SetAnchors(tile, x, y, x + 0.12f, y + 0.11f);
                    AddOutlinePanel(tile, new Color(0.2f, 0.2f, 0.15f, 0.3f));
                }
            }

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR ===
            var topBar = AddPanel(canvas, "TopBar", BgPanel);
            SetAnchors(topBar, 0f, 0.93f, 1f, 1f);
            AddOutlinePanel(topBar, BorderDim);

            var mapTitle = AddText(topBar, "MapTitle", "WORLD MAP — Ashlands", 16, TextAnchor.MiddleCenter);
            SetAnchors(mapTitle, 0.2f, 0.1f, 0.8f, 0.9f);
            mapTitle.GetComponent<Text>().color = Gold;
            mapTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var backBtn = AddStyledButton(topBar, "BackBtn", "< BACK", new Color(0.25f, 0.20f, 0.30f), BgMid);
            SetAnchors(backBtn, 0.01f, 0.1f, 0.12f, 0.9f);
            backBtn.transform.Find("Label").GetComponent<Text>().fontSize = 11;

            // === LEGEND ===
            var legend = AddPanel(canvas, "Legend", BgPanel);
            SetAnchors(legend, 0.80f, 0.85f, 0.99f, 0.93f);
            AddOutlinePanel(legend, BorderDim);
            var legTitle = AddText(legend, "LTitle", "LEGEND", 8, TextAnchor.UpperCenter);
            SetAnchors(legTitle, 0f, 0.7f, 1f, 1f);
            legTitle.GetComponent<Text>().color = TextDim;
            AddLegendItem(legend, "Allied", new Color(0.15f, 0.25f, 0.12f), 0.45f);
            AddLegendItem(legend, "Enemy", new Color(0.25f, 0.10f, 0.10f), 0.15f);

            // === TERRITORY INFO SIDEBAR ===
            var infoPanel = AddPanel(canvas, "TerritoryInfo", BgPanel);
            SetAnchors(infoPanel, 0.01f, 0.10f, 0.28f, 0.45f);
            AddOutlinePanel(infoPanel, GoldDim);

            var tiTitle = AddText(infoPanel, "TerritoryName", "Iron Wastes", 16, TextAnchor.MiddleLeft);
            SetAnchors(tiTitle, 0.05f, 0.82f, 0.95f, 0.98f);
            tiTitle.GetComponent<Text>().color = Gold;

            var tiSep = AddPanel(infoPanel, "Separator", GoldDim);
            SetAnchors(tiSep, 0.05f, 0.80f, 0.95f, 0.81f);

            var tiOwner = AddText(infoPanel, "Owner", "Controlled by: Iron Legion", 11, TextAnchor.MiddleLeft);
            SetAnchors(tiOwner, 0.05f, 0.65f, 0.95f, 0.78f);
            tiOwner.GetComponent<Text>().color = TextLight;

            var tiBonus = AddText(infoPanel, "Bonus", "Bonus: +15% Iron production", 10, TextAnchor.MiddleLeft);
            SetAnchors(tiBonus, 0.05f, 0.52f, 0.95f, 0.64f);
            tiBonus.GetComponent<Text>().color = Teal;

            var tiGarrison = AddText(infoPanel, "Garrison", "Garrison: 12,500 Power", 10, TextAnchor.MiddleLeft);
            SetAnchors(tiGarrison, 0.05f, 0.40f, 0.95f, 0.52f);
            tiGarrison.GetComponent<Text>().color = TextMid;

            var tiAttackBtn = AddStyledButton(infoPanel, "AttackBtn", "ATTACK", Blood, BloodDark);
            SetAnchors(tiAttackBtn, 0.05f, 0.05f, 0.48f, 0.28f);

            var tiScoutBtn = AddStyledButton(infoPanel, "ScoutBtn", "SCOUT", SkyDim, BgMid);
            SetAnchors(tiScoutBtn, 0.52f, 0.05f, 0.95f, 0.28f);

            // === MINI-MAP ===
            var miniMap = AddPanel(canvas, "MiniMap", new Color(0.04f, 0.04f, 0.03f, 0.8f));
            SetAnchors(miniMap, 0.80f, 0.10f, 0.99f, 0.30f);
            AddOutlinePanel(miniMap, BorderDim);
            var mmLabel = AddText(miniMap, "Label", "MINI MAP", 8, TextAnchor.UpperCenter);
            SetAnchors(mmLabel, 0f, 0.85f, 1f, 1f);
            mmLabel.GetComponent<Text>().color = TextDim;

            // Player position dot
            var playerDot = AddPanel(miniMap, "PlayerDot", Gold);
            SetAnchors(playerDot, 0.4f, 0.4f, 0.5f, 0.55f);

            SaveScene();
            Debug.Log("[SceneUIGenerator] WorldMap scene: territory grid, info panel, mini-map");
        }

        // ===================================================================
        // ALLIANCE SCENE — Social hub
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Alliance")]
        public static void SetupAllianceScene()
        {
            var scene = OpenScene("Alliance");
            var canvasGo = FindOrCreateCanvas(scene);

            var bg = AddPanel(canvasGo, "Background", BgDeep);
            StretchToParent(bg);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR ===
            var topBar = AddPanel(canvas, "TopBar", BgPanel);
            SetAnchors(topBar, 0f, 0.93f, 1f, 1f);
            AddOutlinePanel(topBar, BorderDim);

            var allianceName = AddText(topBar, "AllianceName", "IRON LEGION", 18, TextAnchor.MiddleCenter);
            SetAnchors(allianceName, 0.2f, 0.1f, 0.8f, 0.9f);
            allianceName.GetComponent<Text>().color = Gold;
            allianceName.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var memberCount = AddText(topBar, "MemberCount", "42/50 Members", 10, TextAnchor.MiddleRight);
            SetAnchors(memberCount, 0.70f, 0.1f, 0.98f, 0.9f);
            memberCount.GetComponent<Text>().color = TextMid;

            var backBtn = AddStyledButton(topBar, "BackBtn", "< BACK", new Color(0.25f, 0.20f, 0.30f), BgMid);
            SetAnchors(backBtn, 0.01f, 0.1f, 0.12f, 0.9f);
            backBtn.transform.Find("Label").GetComponent<Text>().fontSize = 11;

            // === TAB BAR ===
            var tabBar = AddPanel(canvas, "TabBar", new Color(0.06f, 0.04f, 0.10f, 0.95f));
            SetAnchors(tabBar, 0f, 0.86f, 1f, 0.93f);

            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 2;
            tabLayout.padding = new RectOffset(4, 4, 2, 2);
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            AddTabButton(tabBar, "ChatTab", "CHAT", Teal, true);
            AddTabButton(tabBar, "MembersTab", "MEMBERS", Purple, false);
            AddTabButton(tabBar, "WarTab", "WAR", Blood, false);
            AddTabButton(tabBar, "TerritoryTab", "TERRITORY", Ember, false);
            AddTabButton(tabBar, "LeaderboardTab", "RANKS", Gold, false);

            // === CHAT PANEL ===
            var chatPanel = AddPanel(canvas, "ChatPanel", new Color(0, 0, 0, 0));
            SetAnchors(chatPanel, 0.01f, 0.08f, 0.99f, 0.855f);

            // Message area
            var msgArea = AddPanel(chatPanel, "MessageArea", BgInput);
            SetAnchors(msgArea, 0f, 0.12f, 1f, 1f);
            AddOutlinePanel(msgArea, BorderDim);

            // Sample chat messages
            var chatMsgs = new (string sender, string msg, Color sColor)[] {
                ("Kaelen", "Rally at sector 7! Enemy alliance incoming.", Ember),
                ("Vorra", "I'll bring my siege squad. ETA 5 minutes.", Sky),
                ("Commander", "Everyone focus fire on their Stronghold.", Gold),
                ("Seraphyn", "Healing squad standing by. Let's go!", Teal),
                ("Mordoc", "Their wall defenses are weak on the east side.", Blood),
                ("Lyra", "GG everyone! That was a great war.", Purple),
            };

            for (int i = 0; i < chatMsgs.Length; i++)
            {
                float yBase = 0.85f - i * 0.14f;
                var msgBubble = AddPanel(msgArea, $"Msg_{i}", new Color(0.10f, 0.08f, 0.14f, 0.6f));
                SetAnchors(msgBubble, 0.02f, yBase - 0.12f, 0.98f, yBase);

                var sender = AddText(msgBubble, "Sender", chatMsgs[i].sender, 10, TextAnchor.MiddleLeft);
                SetAnchors(sender, 0.02f, 0.5f, 0.25f, 1f);
                sender.GetComponent<Text>().color = chatMsgs[i].sColor;
                sender.GetComponent<Text>().fontStyle = FontStyle.Bold;

                var msgText = AddText(msgBubble, "Text", chatMsgs[i].msg, 11, TextAnchor.MiddleLeft);
                SetAnchors(msgText, 0.02f, 0f, 0.98f, 0.55f);
                msgText.GetComponent<Text>().color = TextLight;
            }

            // Input area
            var inputBar = AddPanel(chatPanel, "InputBar", BgPanel);
            SetAnchors(inputBar, 0f, 0f, 1f, 0.10f);
            AddOutlinePanel(inputBar, BorderDim);

            var inputField = AddPanel(inputBar, "InputField", BgInput);
            SetAnchors(inputField, 0.02f, 0.12f, 0.80f, 0.88f);
            AddOutlinePanel(inputField, BorderDim);
            var placeholder = AddText(inputField, "Placeholder", "  Type a message...", 12, TextAnchor.MiddleLeft);
            StretchToParent(placeholder);
            placeholder.GetComponent<Text>().color = TextDim;

            var sendBtn = AddStyledButton(inputBar, "SendBtn", "SEND", Teal, TealDim);
            SetAnchors(sendBtn, 0.82f, 0.12f, 0.98f, 0.88f);
            sendBtn.transform.Find("Label").GetComponent<Text>().fontSize = 12;

            // === BOTTOM BAR (alliance actions) ===
            var bottomBar = AddPanel(canvas, "BottomBar", BgPanel);
            SetAnchors(bottomBar, 0f, 0f, 1f, 0.07f);
            AddOutlinePanel(bottomBar, BorderDim);

            var bbLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            bbLayout.spacing = 8;
            bbLayout.padding = new RectOffset(8, 8, 4, 4);
            bbLayout.childForceExpandWidth = true;
            bbLayout.childForceExpandHeight = true;

            AddToolbarBtn(bottomBar, "DonateBtn", "DONATE", GoldDim, new Color(0.25f, 0.20f, 0.10f));
            AddToolbarBtn(bottomBar, "WarDeclareBtn", "DECLARE WAR", Blood, BloodDark);
            AddToolbarBtn(bottomBar, "RecruitBtn", "RECRUIT", Teal, TealDim);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Alliance scene: chat, tabs, member list");
        }

        // ===================================================================
        // HELPER METHODS — Complex widgets
        // ===================================================================

        static void AddCurrencyDisplay(GameObject parent, string name, string amount, Color iconColor, float xPos)
        {
            var group = AddPanel(parent, name, new Color(0, 0, 0, 0));
            SetAnchors(group, xPos, 0.1f, xPos + 0.12f, 0.9f);

            var icon = AddPanel(group, "Icon", iconColor);
            SetAnchors(icon, 0f, 0.15f, 0.22f, 0.85f);

            var text = AddText(group, "Amount", amount, 13, TextAnchor.MiddleLeft);
            SetAnchors(text, 0.26f, 0f, 1f, 1f);
            text.GetComponent<Text>().color = TextWhite;
        }

        static void AddResourceDisplay(GameObject parent, string name, string amount, Color color, float xPos)
        {
            var icon = AddPanel(parent, $"{name}Icon", color);
            SetAnchors(icon, xPos, 0.2f, xPos + 0.02f, 0.8f);

            var text = AddText(parent, $"{name}Amt", $"{name[0]}: {amount}", 10, TextAnchor.MiddleLeft);
            SetAnchors(text, xPos + 0.025f, 0f, xPos + 0.20f, 1f);
            text.GetComponent<Text>().color = TextLight;
        }

        /// <summary>Flat resource icon — 20px outer circle bg + inner sprite, compact like P&C.</summary>
        static void AddResIconFlat(GameObject parent, string resName, Color accentColor)
        {
            // Outer container — subtle colored circle background for dark sprite visibility
            var outer = new GameObject($"IconBg_{resName}", typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(parent.transform, false);
            var le = outer.AddComponent<LayoutElement>();
            le.preferredWidth = 32;
            le.preferredHeight = 32;
            le.minWidth = 26;
            le.minHeight = 26;
            le.flexibleWidth = 0;
            var outerImg = outer.GetComponent<Image>();
            outerImg.color = new Color(
                Mathf.Max(0.10f, accentColor.r * 0.35f),
                Mathf.Max(0.08f, accentColor.g * 0.35f),
                Mathf.Max(0.10f, accentColor.b * 0.35f),
                0.55f);

            // Inner sprite — fills 85% of the circle
            var icon = new GameObject($"Icon_{resName}", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(outer.transform, false);
            var iconRT = icon.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.075f, 0.075f);
            iconRT.anchorMax = new Vector2(0.925f, 0.925f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            string spritePath = $"Assets/Art/UI/Production/icon_{resName.ToLower()}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var img = icon.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(
                    Mathf.Min(1f, accentColor.r + 0.3f),
                    Mathf.Min(1f, accentColor.g + 0.3f),
                    Mathf.Min(1f, accentColor.b + 0.3f), 1f);
            }
        }

        /// <summary>Flat resource amount — flexible text, left-aligned like P&C, 11px compact.</summary>
        static void AddResAmountFlat(GameObject parent, string resName, string amount)
        {
            var amtGo = AddText(parent, $"{resName}Amt", amount, 13, TextAnchor.MiddleLeft);
            var txt = amtGo.GetComponent<Text>();
            txt.color = new Color(0.96f, 0.94f, 0.90f, 1f);
            txt.fontStyle = FontStyle.Bold;
            var shadow = amtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.92f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var le = amtGo.AddComponent<LayoutElement>();
            le.minWidth = 34;
            le.preferredWidth = 50;
            le.flexibleWidth = 0;
        }

        /// <summary>Thin vertical separator between resource pairs in the resource bar.</summary>
        static void AddResSeparator(GameObject parent)
        {
            var sep = new GameObject("ResSep", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(parent.transform, false);
            sep.GetComponent<Image>().color = new Color(0.40f, 0.35f, 0.25f, 0.30f);
            var le = sep.AddComponent<LayoutElement>();
            le.preferredWidth = 1;
            le.minWidth = 1;
            le.preferredHeight = 20;
            le.flexibleWidth = 0;
        }

        /// <summary>Left sidebar queue slot — P&C style: compact strip with colored emblem + label + status + progress bar.</summary>
        static void AddQueueSlot(GameObject parent, string name, string label, string status,
            Color color, bool active, float yMin, float yMax)
        {
            var slot = AddPanel(parent, name, active
                ? new Color(0.06f, 0.04f, 0.12f, 0.90f)
                : new Color(0.04f, 0.03f, 0.08f, 0.70f));
            SetAnchors(slot, 0f, yMin, 1f, yMax);
            slot.AddComponent<Button>();

            // Border — gold for active, dim for idle
            AddOutlinePanel(slot, active ? new Color(0.60f, 0.48f, 0.20f, 0.6f) : new Color(0.22f, 0.18f, 0.10f, 0.25f));

            // Left color accent strip
            var strip = AddPanel(slot, "AccentStrip", active ? color : new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.4f));
            SetAnchors(strip, 0f, 0.06f, active ? 0.05f : 0.03f, 0.94f);

            // Active slot: subtle gradient glow from left
            if (active)
            {
                var glow = AddPanel(slot, "ActiveGlow", new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 0.3f));
                SetAnchors(glow, 0f, 0f, 0.5f, 1f);
            }

            // Colored emblem — square with sprite icon
            var emblemBg = AddPanel(slot, "EmblemBg", new Color(
                color.r * 0.18f + 0.05f, color.g * 0.18f + 0.04f, color.b * 0.18f + 0.05f,
                active ? 0.80f : 0.35f));
            SetAnchors(emblemBg, 0.06f, 0.10f, 0.30f, 0.90f);

            // Sprite icon — fills emblem with padding
            var emblemIcon = AddPanel(emblemBg, "Icon", Color.white);
            SetAnchors(emblemIcon, 0.10f, 0.10f, 0.90f, 0.90f);
            string spriteKey = label.ToLower() switch {
                "build" => "icon_iron", "research" => "icon_arcane", "training" => "icon_iron", _ => null
            };
            bool spriteLoaded = false;
            if (spriteKey != null)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
                if (spr != null)
                {
                    var img = emblemIcon.GetComponent<Image>();
                    img.sprite = spr;
                    img.preserveAspect = true;
                    img.color = active ? new Color(1f, 0.95f, 0.85f, 1f) : new Color(0.55f, 0.50f, 0.45f, 0.5f);
                    spriteLoaded = true;
                }
            }
            if (!spriteLoaded)
            {
                emblemIcon.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                var letterText = AddText(emblemBg, "Letter", label[..1], 16, TextAnchor.MiddleCenter);
                StretchToParent(letterText);
                letterText.GetComponent<Text>().color = active ? Color.white : new Color(0.5f, 0.48f, 0.45f, 0.6f);
                letterText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            }

            // Label — right of emblem, upper portion
            var lbl = AddText(slot, "Label", label, 11, TextAnchor.MiddleLeft);
            SetAnchors(lbl, 0.32f, 0.55f, 0.95f, 0.95f);
            lbl.GetComponent<Text>().color = active ? Color.white : TextMid;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.9f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Status — right of emblem, middle (bright green timer or dim IDLE)
            var statusText = AddText(slot, "Status", status, 10, TextAnchor.MiddleLeft);
            SetAnchors(statusText, 0.32f, 0.22f, 0.95f, 0.55f);
            statusText.GetComponent<Text>().color = active ? new Color(0.3f, 1f, 0.75f, 1f) : TextDim;
            statusText.GetComponent<Text>().fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var statusShadow = statusText.AddComponent<Shadow>();
            statusShadow.effectColor = new Color(0, 0, 0, 0.8f);
            statusShadow.effectDistance = new Vector2(1f, -1f);

            // Progress bar at bottom (P&C shows thin progress bars on active slots)
            if (active)
            {
                var progBg = AddPanel(slot, "ProgressBg", new Color(0.08f, 0.06f, 0.12f, 0.9f));
                SetAnchors(progBg, 0.32f, 0.06f, 0.95f, 0.18f);
                var progFill = AddPanel(progBg, "ProgressFill", new Color(color.r, color.g, color.b, 0.85f));
                SetAnchors(progFill, 0f, 0f, 0.35f, 1f); // 35% progress
                // Glow at fill edge
                var progGlow = AddPanel(progBg, "ProgressGlow", new Color(color.r * 0.8f + 0.2f, color.g * 0.8f + 0.2f, color.b * 0.3f + 0.2f, 0.5f));
                SetAnchors(progGlow, 0.33f, 0f, 0.38f, 1f);
            }
        }

        /// <summary>P&C-style event button — ornate frame with icon, glow, and timer.</summary>
        static void AddEventButton(GameObject parent, string name, string label, Color color,
            float xMin, float yMin, float xMax, float yMax, string timer)
        {
            // Dark button panel — always dark base, ornate sprite overlaid with warm tint
            var btn = AddPanel(parent, name, new Color(0.06f, 0.04f, 0.10f, 0.95f));
            SetAnchors(btn, xMin, yMin, xMax, yMax);
            var btnImg = btn.GetComponent<Image>();
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (ornateSpr != null)
            {
                btnImg.sprite = ornateSpr;
                btnImg.type = Image.Type.Sliced;
                btnImg.color = new Color(0.75f, 0.68f, 0.58f, 1f); // warm dark tint, no white flash
            }
            AddOutlinePanel(btn, new Color(0.55f, 0.42f, 0.18f, 0.45f));
            btn.AddComponent<Button>();

            // Subtle color tint overlay
            var tint = AddPanel(btn, "Tint", new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 0.20f));
            SetAnchors(tint, 0.06f, 0.06f, 0.94f, 0.94f);
            // Center glow behind icon
            var centerGlow = AddPanel(btn, "CenterGlow", new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.12f));
            SetAnchors(centerGlow, 0.18f, 0.22f, 0.82f, 0.78f);

            // Icon — transparent background, sprite only (no white rect behind it)
            var icon = AddPanel(btn, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.14f, 0.24f, 0.86f, 0.78f);
            string spriteKey = label.ToLower() switch {
                "events" => "icon_iron",
                "vs"     => "icon_gems",
                "rewards"=> "icon_grain",
                "offer"  => "icon_gems",
                "shop"   => "icon_arcane",
                "gifts"  => "icon_arcane",
                "arena"  => "icon_iron",
                _        => "icon_stone"
            };
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
            if (spr != null)
            {
                icon.GetComponent<Image>().sprite = spr;
                icon.GetComponent<Image>().preserveAspect = true;
                icon.GetComponent<Image>().color = Color.white; // full brightness, sprite handles color
            }
            else
            {
                icon.GetComponent<Image>().color = color; // fallback: colored square
            }

            // Label — bottom of icon area
            var lbl = AddText(btn, "Label", label, 10, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0.02f, 1f, 0.25f);
            lbl.GetComponent<Text>().color = Color.white;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.9f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Timer badge (if any) — top strip
            if (!string.IsNullOrEmpty(timer))
            {
                var timerBg = AddPanel(btn, "TimerBg", new Color(0.02f, 0.02f, 0.06f, 0.85f));
                SetAnchors(timerBg, 0.05f, 0.84f, 0.95f, 1f);
                var timerText = AddText(timerBg, "Timer", timer, 9, TextAnchor.MiddleCenter);
                StretchToParent(timerText);
                timerText.GetComponent<Text>().color = new Color(0.3f, 1f, 0.75f, 1f);
                timerText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var timerShadow = timerText.AddComponent<Shadow>();
                timerShadow.effectColor = new Color(0, 0, 0, 0.8f);
                timerShadow.effectDistance = new Vector2(1f, -1f);
            }
        }

        /// <summary>P&C-style nav bar item — golden icon + label + optional badge.</summary>
        static void AddNavItem(GameObject parent, string name, string label, Color color, bool active, int badgeCount)
        {
            var item = AddPanel(parent, name, active ? new Color(0.18f, 0.12f, 0.06f, 0.45f) : new Color(0, 0, 0, 0));
            item.AddComponent<LayoutElement>().flexibleWidth = 1;
            item.AddComponent<Button>();

            // Golden icon emblem — use best-matching production sprite per nav item
            var iconBg = AddPanel(item, "IconBg", active
                ? new Color(0.28f, 0.22f, 0.10f, 0.5f)
                : new Color(0, 0, 0, 0));
            SetAnchors(iconBg, 0.15f, 0.32f, 0.85f, 0.88f);

            var icon = AddPanel(iconBg, "Icon", Color.white);
            SetAnchors(icon, 0.10f, 0.05f, 0.90f, 0.95f);

            // Map nav items to best production sprites
            string spriteKey = label.ToLower() switch {
                "world" => "nav_empire", "hero" => "nav_heroes", "quest" => "icon_arcane",
                "bag" => "icon_grain", "mail" => "icon_gems", "alliance" => "nav_alliance",
                "rank" => "icon_gold", _ => null
            };

            Color activeTint = new Color(1f, 0.92f, 0.72f, 1f); // Warm golden
            Color inactiveTint = new Color(0.60f, 0.52f, 0.38f, 0.55f); // Dim bronze

            bool spriteSet = false;
            if (spriteKey != null)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
                if (spr != null)
                {
                    icon.GetComponent<Image>().sprite = spr;
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().color = active ? activeTint : inactiveTint;
                    spriteSet = true;
                }
            }
            if (!spriteSet)
            {
                icon.GetComponent<Image>().color = active ? color : inactiveTint;
            }

            // Active gold accent bar at top
            if (active)
            {
                var glow = AddPanel(item, "ActiveGlow", new Color(0.85f, 0.68f, 0.25f, 0.85f));
                SetAnchors(glow, 0.08f, 0.92f, 0.92f, 1f);
            }

            // Red notification badge (like P&C)
            if (badgeCount > 0)
            {
                var badge = AddPanel(item, "Badge", new Color(0.88f, 0.14f, 0.14f, 1f));
                SetAnchors(badge, 0.60f, 0.68f, 0.98f, 0.94f);
                AddOutlinePanel(badge, new Color(0.55f, 0.08f, 0.08f, 0.9f));
                var badgeText = AddText(badge, "Count", badgeCount.ToString(), 8, TextAnchor.MiddleCenter);
                StretchToParent(badgeText);
                badgeText.GetComponent<Text>().color = Color.white;
                badgeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            }

            // Label — warm gold for active, steel blue-gray for inactive (P&C style)
            var lbl = AddText(item, "Label", label, 9, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0f, 1f, 0.30f);
            lbl.GetComponent<Text>().color = active ? new Color(1f, 0.92f, 0.70f, 1f) : new Color(0.52f, 0.52f, 0.58f, 0.75f);
            lbl.GetComponent<Text>().fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.9f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void AddHeroStatusPanel(GameObject parent, string heroName, float hpPct, Color heroColor, bool isPlayer)
        {
            var panel = AddPanel(parent, heroName, BgPanel);
            panel.AddComponent<LayoutElement>().preferredHeight = 38;
            AddOutlinePanel(panel, isPlayer ? new Color(0.15f, 0.20f, 0.35f, 0.5f) : new Color(0.35f, 0.12f, 0.12f, 0.5f));

            var portrait = AddPanel(panel, "Portrait", heroColor);
            SetAnchors(portrait, 0.02f, 0.08f, 0.28f, 0.92f);
            AddOutlinePanel(portrait, isPlayer ? SkyDim : BloodDark);

            var nameLabel = AddText(panel, "Name", heroName, 9, TextAnchor.MiddleLeft);
            SetAnchors(nameLabel, 0.32f, 0.55f, 0.98f, 0.95f);
            nameLabel.GetComponent<Text>().color = TextWhite;

            var hpBarBg = AddPanel(panel, "HpBarBg", BarHpBg);
            SetAnchors(hpBarBg, 0.32f, 0.15f, 0.98f, 0.45f);

            var hpBarFill = AddPanel(hpBarBg, "Fill", hpPct > 0.5f ? BarHpGreen : hpPct > 0.25f ? Ember : BarHpRed);
            SetAnchors(hpBarFill, 0f, 0f, hpPct, 1f);

            var hpText = AddText(panel, "HpText", $"{(int)(hpPct * 1000)}/1000", 7, TextAnchor.MiddleRight);
            SetAnchors(hpText, 0.60f, 0.55f, 0.98f, 0.92f);
            hpText.GetComponent<Text>().color = TextDim;
        }

        static void AddCardWidget(GameObject parent, string cardName, int cost, Color color, string type, int value)
        {
            var card = AddPanel(parent, cardName.Replace(" ", ""), BgCard);
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = 105;
            le.preferredHeight = 150;
            AddOutlinePanel(card, color);

            // Card art area
            var artArea = AddPanel(card, "Art", new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f));
            SetAnchors(artArea, 0.06f, 0.40f, 0.94f, 0.82f);

            // Cost badge (top-left)
            var costBadge = AddPanel(card, "CostBadge", BarEnergy);
            SetAnchors(costBadge, 0.02f, 0.82f, 0.22f, 0.98f);
            var costText = AddText(costBadge, "Cost", cost.ToString(), 12, TextAnchor.MiddleCenter);
            StretchToParent(costText);
            costText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Type badge (top-right)
            var typeBadge = AddPanel(card, "TypeBadge",
                type == "ATK" ? Blood : type == "HEAL" ? Teal : IronColor);
            SetAnchors(typeBadge, 0.70f, 0.82f, 0.98f, 0.98f);
            var typeText = AddText(typeBadge, "Type", type, 7, TextAnchor.MiddleCenter);
            StretchToParent(typeText);
            typeText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Card name
            var nameText = AddText(card, "Name", cardName, 9, TextAnchor.MiddleCenter);
            SetAnchors(nameText, 0.04f, 0.22f, 0.96f, 0.38f);
            nameText.GetComponent<Text>().color = color;
            nameText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Value text
            var valText = AddText(card, "Value", value > 0 ? value.ToString() : "", 11, TextAnchor.MiddleCenter);
            SetAnchors(valText, 0.04f, 0.04f, 0.96f, 0.22f);
            valText.GetComponent<Text>().color = TextMid;

            // Element accent line
            var accent = AddPanel(card, "Accent", color);
            SetAnchors(accent, 0.06f, 0.38f, 0.94f, 0.40f);
        }

        static void AddBuildQueueSlot(GameObject parent, int index, string buildingName, string timer, float progress, bool active)
        {
            float yTop = 0.85f - index * 0.30f;
            float yBot = yTop - 0.27f;

            var slot = AddPanel(parent, $"QueueSlot_{index}", active ? BgPanelLight : BgInput);
            SetAnchors(slot, 0.04f, yBot, 0.96f, yTop);
            AddOutlinePanel(slot, active ? EmberDim : BorderDim);

            var nameText = AddText(slot, "Name", buildingName, 11, TextAnchor.MiddleLeft);
            SetAnchors(nameText, 0.05f, 0.55f, 0.65f, 0.95f);
            nameText.GetComponent<Text>().color = active ? TextWhite : TextDim;

            var timerText = AddText(slot, "Timer", timer, 12, TextAnchor.MiddleRight);
            SetAnchors(timerText, 0.55f, 0.55f, 0.95f, 0.95f);
            timerText.GetComponent<Text>().color = active ? Ember : TextDim;
            if (active) timerText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            if (active)
            {
                var progressBg = AddPanel(slot, "ProgressBg", new Color(0.06f, 0.04f, 0.08f));
                SetAnchors(progressBg, 0.05f, 0.12f, 0.95f, 0.40f);
                var progressFill = AddPanel(progressBg, "Fill", Ember);
                SetAnchors(progressFill, 0f, 0f, progress, 1f);
            }
        }

        static void AddLegendItem(GameObject parent, string label, Color color, float yPos)
        {
            var dot = AddPanel(parent, $"Leg_{label}", color);
            SetAnchors(dot, 0.1f, yPos, 0.25f, yPos + 0.25f);
            var text = AddText(parent, $"LegText_{label}", label, 8, TextAnchor.MiddleLeft);
            SetAnchors(text, 0.3f, yPos, 0.95f, yPos + 0.25f);
            text.GetComponent<Text>().color = TextMid;
        }

        // ===================================================================
        // HELPER METHODS — Basic widgets
        // ===================================================================

        static GameObject AddStyledButton(GameObject parent, string name, string label, Color bgColor, Color darkColor)
        {
            var btn = AddPanel(parent, name, bgColor);
            btn.AddComponent<Button>();
            AddOutlinePanel(btn, new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f, 0.5f));

            var dark = AddPanel(btn, "DarkOverlay", new Color(darkColor.r, darkColor.g, darkColor.b, 0.3f));
            SetAnchors(dark, 0f, 0f, 1f, 0.5f);

            var lbl = AddText(btn, "Label", label, 13, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            return btn;
        }

        static void AddNavButton(GameObject parent, string name, string label, Color color, bool isActive)
        {
            var btn = AddPanel(parent, name, isActive ? color : new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.4f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            btn.AddComponent<Button>();

            if (isActive)
            {
                var indicator = AddPanel(btn, "ActiveIndicator", Gold);
                SetAnchors(indicator, 0.2f, 0.85f, 0.8f, 0.92f);
            }

            var lbl = AddText(btn, "Label", label, 10, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().color = isActive ? TextWhite : TextMid;
            lbl.GetComponent<Text>().fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        }

        static void AddQuickActionButton(GameObject parent, string name, string label, Color color)
        {
            var btn = AddPanel(parent, name, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            btn.AddComponent<Button>();
            AddOutlinePanel(btn, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.5f));

            var lbl = AddText(btn, "Label", label, 11, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().color = new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        static void AddTabButton(GameObject parent, string name, string label, Color color, bool isActive)
        {
            var tab = AddPanel(parent, name, isActive ? new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f) : BgMid);
            tab.AddComponent<LayoutElement>().flexibleWidth = 1;
            tab.AddComponent<Button>();

            if (isActive)
            {
                var indicator = AddPanel(tab, "ActiveBar", color);
                SetAnchors(indicator, 0.1f, 0f, 0.9f, 0.06f);
            }

            var lbl = AddText(tab, "Label", label, 11, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().color = isActive ? color : TextDim;
            lbl.GetComponent<Text>().fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
        }

        static void AddToolbarBtn(GameObject parent, string name, string label, Color color, Color darkColor)
        {
            var btn = AddPanel(parent, name, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            btn.AddComponent<Button>();
            AddOutlinePanel(btn, new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.4f));

            var iconArea = AddPanel(btn, "Icon", new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.8f));
            SetAnchors(iconArea, 0.3f, 0.55f, 0.7f, 0.88f);

            var lbl = AddText(btn, "Label", label, 9, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0.02f, 1f, 0.45f);
            lbl.GetComponent<Text>().color = color;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        // ===================================================================
        // HELPER METHODS — Primitives
        // ===================================================================

        /// <summary>Creates a SafeArea panel under the canvas that adjusts for notch/Dynamic Island.</summary>
        static GameObject CreateSafeArea(GameObject canvasGo)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var safeAreaType = System.Type.GetType("AshenThrone.UI.SafeAreaPanel, AshenThrone");
            if (safeAreaType != null) go.AddComponent(safeAreaType);
            return go;
        }

        static UnityEngine.SceneManagement.Scene OpenScene(string name)
        {
            string path = $"Assets/Scenes/{name}/{name}.unity";
            if (!File.Exists(path))
            {
                Debug.LogError($"[SceneUIGenerator] Scene not found: {path}");
                return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            }
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        static GameObject FindOrCreateCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var existing = root.GetComponentInChildren<Canvas>();
                if (existing != null)
                {
                    for (int i = existing.transform.childCount - 1; i >= 0; i--)
                        Object.DestroyImmediate(existing.transform.GetChild(i).gameObject);
                    // Ensure proper scaler
                    var scaler = existing.GetComponent<CanvasScaler>();
                    if (scaler != null)
                    {
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1080, 1920);
                        scaler.matchWidthOrHeight = 0.5f;
                    }
                    return existing.gameObject;
                }
            }

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = canvasGo.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920);
            cs.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            bool hasES = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<EventSystem>() != null) { hasES = true; break; }
            if (!hasES)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            return canvasGo;
        }

        static GameObject AddPanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject AddText(GameObject parent, string name, string text, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TextLight;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return go;
        }

        static void AddOutline(GameObject go, Color color, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        static void AddOutlinePanel(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void StretchToParent(GameObject child)
        {
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SaveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
