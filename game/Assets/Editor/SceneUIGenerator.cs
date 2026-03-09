#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AshenThrone.UI;
using AshenThrone.Core;

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

        [MenuItem("AshenThrone/Play Scene/Empire")]
        public static void PlayEmpireScene() => PlayScene("Empire");

        [MenuItem("AshenThrone/Play Scene/Lobby")]
        public static void PlayLobbyScene() => PlayScene("Lobby");

        [MenuItem("AshenThrone/Play Scene/Boot")]
        public static void PlayBootScene() => PlayScene("Boot");

        [MenuItem("AshenThrone/Play Scene/Combat")]
        public static void PlayCombatScene() => PlayScene("Combat");

        [MenuItem("AshenThrone/Play Scene/WorldMap")]
        public static void PlayWorldMapScene() => PlayScene("WorldMap");

        [MenuItem("AshenThrone/Play Scene/Alliance")]
        public static void PlayAllianceScene() => PlayScene("Alliance");

        static void PlayScene(string sceneName)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}/{sceneName}.unity", OpenSceneMode.Single);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Scenes/{sceneName}/{sceneName}.unity");
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

            // === DRAMATIC BACKGROUND — splash art with overlays ===
            var bg = AddPanel(canvasGo, "Background", BgDeep);
            StretchToParent(bg);
            // Use splash_screen as background art if available
            var splashSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/splash_screen.png");
            if (splashSpr != null)
            {
                bg.GetComponent<Image>().sprite = splashSpr;
                bg.GetComponent<Image>().preserveAspect = false;
                bg.GetComponent<Image>().color = new Color(0.35f, 0.30f, 0.40f, 1f); // dim tint so UI reads well
            }
            // Dark gradient overlay at bottom for text readability
            var botOverlay = AddPanel(canvasGo, "BottomOverlay", new Color(0.02f, 0.01f, 0.04f, 0.85f));
            SetAnchors(botOverlay, 0f, 0f, 1f, 0.55f);
            // Top vignette
            var topOverlay = AddPanel(canvasGo, "TopOverlay", new Color(0.02f, 0.01f, 0.04f, 0.40f));
            SetAnchors(topOverlay, 0f, 0.75f, 1f, 1f);
            // Side vignettes
            var vigL = AddPanel(canvasGo, "VignetteL", new Color(0, 0, 0, 0.25f));
            SetAnchors(vigL, 0f, 0f, 0.06f, 1f);
            var vigR = AddPanel(canvasGo, "VignetteR", new Color(0, 0, 0, 0.25f));
            SetAnchors(vigR, 0.94f, 0f, 1f, 1f);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === TITLE — large, dramatic, with heavy effects ===
            // Top gold accent line
            var topAccent = AddPanel(canvas, "TopAccent", new Color(0.72f, 0.56f, 0.22f, 0.60f));
            SetAnchors(topAccent, 0.12f, 0.785f, 0.88f, 0.79f);
            // Glow above accent
            var topGlow = AddPanel(canvas, "TopGlow", new Color(0.72f, 0.56f, 0.22f, 0.08f));
            SetAnchors(topGlow, 0.10f, 0.79f, 0.90f, 0.82f);

            var title = AddText(canvas, "Title", "ASHEN THRONE", 52, TextAnchor.MiddleCenter);
            SetAnchors(title, 0.05f, 0.65f, 0.95f, 0.78f);
            title.GetComponent<Text>().color = new Color(0.94f, 0.82f, 0.38f, 1f); // bright gold
            title.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var titleShadow = title.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0, 0, 0, 0.95f);
            titleShadow.effectDistance = new Vector2(3f, -3f);
            var titleOutline = title.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.40f, 0.28f, 0.08f, 0.70f);
            titleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Subtitle
            var sub = AddText(canvas, "Subtitle", "A Dark Fantasy Strategy RPG", 14, TextAnchor.MiddleCenter);
            SetAnchors(sub, 0.15f, 0.58f, 0.85f, 0.64f);
            sub.GetComponent<Text>().color = new Color(0.65f, 0.60f, 0.52f, 0.85f);
            sub.GetComponent<Text>().fontStyle = FontStyle.Italic;
            var subShadow = sub.AddComponent<Shadow>();
            subShadow.effectColor = new Color(0, 0, 0, 0.8f);
            subShadow.effectDistance = new Vector2(1f, -1f);

            // Bottom gold accent line
            var botAccent = AddPanel(canvas, "BottomAccent", new Color(0.72f, 0.56f, 0.22f, 0.60f));
            SetAnchors(botAccent, 0.12f, 0.575f, 0.88f, 0.58f);

            // === LOADING AREA — ornate frame with progress bar ===
            var loadFrame = AddPanel(canvas, "LoadingFrame", new Color(0.05f, 0.03f, 0.09f, 0.90f));
            SetAnchors(loadFrame, 0.10f, 0.30f, 0.90f, 0.52f);
            var loadOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (loadOrnateSpr != null)
            {
                loadFrame.GetComponent<Image>().sprite = loadOrnateSpr;
                loadFrame.GetComponent<Image>().type = Image.Type.Sliced;
                loadFrame.GetComponent<Image>().color = new Color(0.50f, 0.45f, 0.38f, 0.90f);
            }
            else { AddOutlinePanel(loadFrame, Border); }

            // Loading status text
            var statusText = AddText(loadFrame, "StatusText", "Initializing Services...", 13, TextAnchor.MiddleCenter);
            SetAnchors(statusText, 0.05f, 0.58f, 0.95f, 0.88f);
            statusText.GetComponent<Text>().color = TextLight;
            var statusShadow = statusText.AddComponent<Shadow>();
            statusShadow.effectColor = new Color(0, 0, 0, 0.8f);
            statusShadow.effectDistance = new Vector2(1f, -1f);

            // Loading bar track
            var barTrack = AddPanel(loadFrame, "BarTrack", new Color(0.04f, 0.02f, 0.06f, 1f));
            SetAnchors(barTrack, 0.06f, 0.18f, 0.94f, 0.45f);
            AddOutlinePanel(barTrack, new Color(0.42f, 0.34f, 0.18f, 0.50f));

            // Loading bar fill — gold gradient with glow edge
            var barFill = AddPanel(barTrack, "BarFill", new Color(0.85f, 0.68f, 0.28f, 1f));
            var fillRect = barFill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.01f, 0.08f);
            fillRect.anchorMax = new Vector2(0.70f, 0.92f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            // Glow at fill edge
            var fillGlow = AddPanel(barTrack, "FillGlow", new Color(0.95f, 0.80f, 0.35f, 0.40f));
            SetAnchors(fillGlow, 0.68f, 0.05f, 0.73f, 0.95f);
            // Bright highlight on bar
            var fillHighlight = AddPanel(barFill, "Highlight", new Color(1f, 0.95f, 0.70f, 0.25f));
            SetAnchors(fillHighlight, 0f, 0.55f, 1f, 0.95f);

            // Loading percentage
            var pctText = AddText(loadFrame, "Percentage", "70%", 12, TextAnchor.MiddleCenter);
            SetAnchors(pctText, 0.38f, 0.18f, 0.62f, 0.45f);
            pctText.GetComponent<Text>().color = TextWhite;
            pctText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pctShadow = pctText.AddComponent<Shadow>();
            pctShadow.effectColor = new Color(0, 0, 0, 0.8f);
            pctShadow.effectDistance = new Vector2(1f, -1f);

            // === TIP TEXT — framed with subtle border ===
            var tipFrame = AddPanel(canvas, "TipFrame", new Color(0.04f, 0.02f, 0.08f, 0.50f));
            SetAnchors(tipFrame, 0.08f, 0.16f, 0.92f, 0.27f);
            var tip = AddText(tipFrame, "TipText", "TIP: Upgrade your Stronghold to unlock new building types", 11, TextAnchor.MiddleCenter);
            StretchToParent(tip);
            tip.GetComponent<Text>().color = new Color(0.50f, 0.47f, 0.40f, 0.80f);
            tip.GetComponent<Text>().fontStyle = FontStyle.Italic;
            var tipShadow = tip.AddComponent<Shadow>();
            tipShadow.effectColor = new Color(0, 0, 0, 0.6f);
            tipShadow.effectDistance = new Vector2(1f, -1f);

            // Version + copyright
            var ver = AddText(canvas, "VersionLabel", "v0.1.0-alpha  |  Ashen Throne Studios", 10, TextAnchor.LowerCenter);
            SetAnchors(ver, 0.1f, 0.02f, 0.9f, 0.06f);
            ver.GetComponent<Text>().color = new Color(0.35f, 0.32f, 0.28f, 0.55f);
            var verShadow = ver.AddComponent<Shadow>();
            verShadow.effectColor = new Color(0, 0, 0, 0.5f);
            verShadow.effectDistance = new Vector2(0.5f, -0.5f);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Boot scene: polished loading screen");
        }

        // ===================================================================
        // LOBBY SCENE — Main menu hub (P&C quality)
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Lobby")]
        public static void SetupLobbyScene()
        {
            var scene = OpenScene("Lobby");
            var canvasGo = FindOrCreateCanvas(scene);

            // Background — dark fantasy with atmospheric gradients (matches Empire)
            var bg = AddPanel(canvasGo, "Background", new Color(0.03f, 0.02f, 0.06f, 0.94f));
            StretchToParent(bg);
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.08f, 0.05f, 0.16f, 0.45f));
            SetAnchors(skyGrad, 0f, 0.75f, 1f, 1f);
            var groundGrad = AddPanel(bg, "GroundGradient", new Color(0.02f, 0.01f, 0.03f, 0.5f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.25f);
            var vigLeft = AddPanel(bg, "VignetteL", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigLeft, 0f, 0f, 0.08f, 1f);
            var vigRight = AddPanel(bg, "VignetteR", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigRight, 0.92f, 0f, 1f, 1f);

            // Notch/Dynamic Island fill
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);
            var notchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.70f));
            SetAnchors(notchBorder, 0f, 0f, 1f, 0.008f);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === RESOURCE BAR (top strip — matches Empire exactly) ===
            var resBarBg = AddPanel(canvas, "ResourceBarBg", new Color(0.03f, 0.02f, 0.06f, 0.96f));
            SetAnchors(resBarBg, 0f, 0.957f, 1f, 0.995f);
            var resBarBorder = AddPanel(resBarBg, "BottomBorder", new Color(0.72f, 0.56f, 0.22f, 0.70f));
            SetAnchors(resBarBorder, 0f, 0f, 1f, 0.035f);

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

            AddResIconFlat(resBar, "Grain", GrainColor);
            AddResAmountFlat(resBar, "Grain", "15.0K");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Iron", IronColor);
            AddResAmountFlat(resBar, "Iron", "8.20K");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Stone", StoneColor);
            AddResAmountFlat(resBar, "Stone", "12.5K");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Arcane", ArcaneColor);
            AddResAmountFlat(resBar, "Arcane", "3.40K");
            AddResSeparator(resBar);
            AddResIconFlat(resBar, "Gems", GemsColor);
            AddResAmountFlat(resBar, "Gems", "385");

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(resBar.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

            var plusBtn = AddPanel(resBar, "AddBtn", new Color(0.72f, 0.56f, 0.22f, 0.95f));
            var plusLE = plusBtn.AddComponent<LayoutElement>();
            plusLE.preferredWidth = 26; plusLE.preferredHeight = 26; plusLE.minWidth = 22; plusLE.minHeight = 22; plusLE.flexibleWidth = 0;
            var plusRoundSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (plusRoundSpr != null) { plusBtn.GetComponent<Image>().sprite = plusRoundSpr; plusBtn.GetComponent<Image>().type = Image.Type.Sliced; plusBtn.GetComponent<Image>().color = new Color(0.25f, 0.72f, 0.38f, 1f); }
            plusBtn.AddComponent<Button>();
            var plusText = AddText(plusBtn, "Label", "+", 14, TextAnchor.MiddleCenter);
            StretchToParent(plusText);
            plusText.GetComponent<Text>().color = Color.white;
            plusText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === AVATAR BLOCK (matches Empire) ===
            var avatarBlock = AddPanel(canvas, "AvatarBlock", new Color(0.05f, 0.03f, 0.09f, 0.95f));
            SetAnchors(avatarBlock, 0.01f, 0.875f, 0.14f, 0.955f);
            AddOutlinePanel(avatarBlock, new Color(0.82f, 0.65f, 0.28f, 0.85f));
            var avatarInnerBorder = AddPanel(avatarBlock, "InnerBorder", new Color(0, 0, 0, 0));
            SetAnchors(avatarInnerBorder, 0.04f, 0.03f, 0.96f, 0.97f);
            AddOutlinePanel(avatarInnerBorder, new Color(0.55f, 0.42f, 0.18f, 0.50f));
            var avatarPortrait = AddPanel(avatarBlock, "Portrait", new Color(0.12f, 0.08f, 0.18f, 0.95f));
            SetAnchors(avatarPortrait, 0.06f, 0.05f, 0.94f, 0.95f);
            var avatarSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/avatar_default.png");
            if (avatarSpr != null) { avatarPortrait.GetComponent<Image>().sprite = avatarSpr; avatarPortrait.GetComponent<Image>().preserveAspect = true; avatarPortrait.GetComponent<Image>().color = Color.white; }
            var avatarHighlight = AddPanel(avatarPortrait, "Highlight", new Color(0.50f, 0.35f, 0.65f, 0.10f));
            SetAnchors(avatarHighlight, 0f, 0.60f, 1f, 1f);
            var lvlBadge = AddPanel(avatarBlock, "LevelBadge", new Color(0.72f, 0.56f, 0.22f, 1f));
            SetAnchors(lvlBadge, 0.22f, -0.06f, 0.78f, 0.12f);
            AddOutlinePanel(lvlBadge, new Color(0.45f, 0.34f, 0.12f, 0.9f));
            var lvlText = AddText(lvlBadge, "Level", "Lv.1", 8, TextAnchor.MiddleCenter);
            StretchToParent(lvlText);
            lvlText.GetComponent<Text>().color = TextWhite;
            lvlText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === INFO PANEL (right of avatar — matches Empire) ===
            var infoPanelBg = AddPanel(canvas, "InfoPanelBg", new Color(0.04f, 0.03f, 0.08f, 0.90f));
            SetAnchors(infoPanelBg, 0.15f, 0.910f, 0.72f, 0.955f);
            var infoTopGrad = AddPanel(infoPanelBg, "TopGrad", new Color(0.12f, 0.08f, 0.18f, 0.25f));
            SetAnchors(infoTopGrad, 0f, 0.55f, 1f, 1f);
            var infoLeftAccent = AddPanel(infoPanelBg, "LeftAccent", new Color(0.72f, 0.56f, 0.22f, 0.50f));
            SetAnchors(infoLeftAccent, 0f, 0.08f, 0.008f, 0.92f);
            var infoBotBorder = AddPanel(infoPanelBg, "BotBorder", new Color(0.55f, 0.42f, 0.18f, 0.30f));
            SetAnchors(infoBotBorder, 0.02f, 0f, 0.98f, 0.025f);

            var avatarName = AddText(infoPanelBg, "PlayerName", "Commander", 13, TextAnchor.MiddleLeft);
            SetAnchors(avatarName, 0.04f, 0.52f, 0.52f, 0.96f);
            avatarName.GetComponent<Text>().color = new Color(0.92f, 0.78f, 0.38f, 1f);
            avatarName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = avatarName.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1f, -1f);

            var vipOuter = AddPanel(infoPanelBg, "VipOuter", new Color(0.60f, 0.45f, 0.18f, 0.70f));
            SetAnchors(vipOuter, 0.54f, 0.56f, 0.78f, 0.94f);
            var vipBadge = AddPanel(vipOuter, "VipBadge", new Color(0.42f, 0.12f, 0.55f, 0.95f));
            SetAnchors(vipBadge, 0.04f, 0.06f, 0.96f, 0.94f);
            var vipShimmer = AddPanel(vipBadge, "Shimmer", new Color(0.65f, 0.40f, 0.85f, 0.20f));
            SetAnchors(vipShimmer, 0f, 0.45f, 1f, 1f);
            var vipText = AddText(vipBadge, "Label", "VIP 1", 10, TextAnchor.MiddleCenter);
            StretchToParent(vipText);
            vipText.GetComponent<Text>().color = new Color(1f, 0.95f, 0.75f, 1f);
            vipText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Power + Stronghold level row
            var powerIconText = AddText(infoPanelBg, "PowerIcon", "\u2694", 11, TextAnchor.MiddleCenter);
            SetAnchors(powerIconText, 0.03f, 0.06f, 0.10f, 0.50f);
            powerIconText.GetComponent<Text>().color = new Color(0.92f, 0.75f, 0.32f, 1f);
            var powerVal = AddText(infoPanelBg, "PowerValue", "12,450", 12, TextAnchor.MiddleLeft);
            SetAnchors(powerVal, 0.10f, 0.04f, 0.50f, 0.52f);
            powerVal.GetComponent<Text>().color = new Color(0.95f, 0.93f, 0.88f, 1f);
            powerVal.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var shLvl = AddText(infoPanelBg, "StrongholdLvl", "Stronghold Lv.1", 9, TextAnchor.MiddleRight);
            SetAnchors(shLvl, 0.55f, 0.06f, 0.98f, 0.50f);
            shLvl.GetComponent<Text>().color = new Color(0.62f, 0.58f, 0.50f, 0.85f);

            // === CURRENCY DISPLAY (right of info panel — Gold + Gems) ===
            var currPanel = AddPanel(canvas, "CurrencyPanel", new Color(0.04f, 0.03f, 0.08f, 0.88f));
            SetAnchors(currPanel, 0.73f, 0.910f, 0.99f, 0.955f);
            var currBotBorder = AddPanel(currPanel, "BotBorder", new Color(0.55f, 0.42f, 0.18f, 0.30f));
            SetAnchors(currBotBorder, 0.02f, 0f, 0.98f, 0.025f);
            var currLayout = currPanel.AddComponent<HorizontalLayoutGroup>();
            currLayout.spacing = 4; currLayout.padding = new RectOffset(6, 6, 4, 4);
            currLayout.childAlignment = TextAnchor.MiddleCenter;
            currLayout.childForceExpandWidth = false; currLayout.childForceExpandHeight = false;
            currLayout.childControlWidth = true; currLayout.childControlHeight = true;
            AddResIconFlat(currPanel, "Gold", Gold);
            var goldAmt = AddText(currPanel, "GoldAmt", "12,450", 11, TextAnchor.MiddleLeft);
            goldAmt.GetComponent<Text>().color = new Color(0.96f, 0.94f, 0.90f, 1f);
            goldAmt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            goldAmt.AddComponent<LayoutElement>().flexibleWidth = 1;
            var currSep = new GameObject("Sep", typeof(RectTransform), typeof(Image));
            currSep.transform.SetParent(currPanel.transform, false);
            currSep.GetComponent<Image>().color = new Color(0.40f, 0.35f, 0.25f, 0.30f);
            var csLE = currSep.AddComponent<LayoutElement>(); csLE.preferredWidth = 1; csLE.preferredHeight = 20;
            AddResIconFlat(currPanel, "Gems", GemsColor);
            var gemAmt = AddText(currPanel, "GemAmt", "385", 11, TextAnchor.MiddleLeft);
            gemAmt.GetComponent<Text>().color = new Color(0.96f, 0.94f, 0.90f, 1f);
            gemAmt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            gemAmt.AddComponent<LayoutElement>().flexibleWidth = 1;

            // === CENTER CONTENT ===
            // Game logo / title — ornate, premium
            var logoText = AddText(canvas, "LogoText", "ASHEN THRONE", 42, TextAnchor.MiddleCenter);
            SetAnchors(logoText, 0.1f, 0.72f, 0.9f, 0.85f);
            logoText.GetComponent<Text>().color = Gold;
            logoText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(logoText, new Color(0.2f, 0.12f, 0.03f), 2f);
            var logoShadow = logoText.AddComponent<Shadow>();
            logoShadow.effectColor = new Color(0, 0, 0, 0.9f);
            logoShadow.effectDistance = new Vector2(3f, -3f);

            // Featured event banner — ornate frame
            var eventBanner = AddPanel(canvas, "EventBanner", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(eventBanner, 0.06f, 0.56f, 0.94f, 0.70f);
            var bannerOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (bannerOrnateSpr != null) { eventBanner.GetComponent<Image>().sprite = bannerOrnateSpr; eventBanner.GetComponent<Image>().type = Image.Type.Sliced; eventBanner.GetComponent<Image>().color = new Color(0.75f, 0.68f, 0.58f, 1f); }
            else { AddOutlinePanel(eventBanner, Ember); }

            var eventTag = AddPanel(eventBanner, "EventTag", Ember);
            SetAnchors(eventTag, 0.0f, 0.78f, 0.18f, 1.0f);
            var eventTagText = AddText(eventTag, "TagText", "  EVENT", 10, TextAnchor.MiddleLeft);
            StretchToParent(eventTagText);
            eventTagText.GetComponent<Text>().color = TextWhite;
            eventTagText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Event icon — left side
            var eventIcon = AddPanel(eventBanner, "EventIcon", new Color(0, 0, 0, 0));
            SetAnchors(eventIcon, 0.02f, 0.10f, 0.16f, 0.78f);
            var evtSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_events.png");
            if (evtSpr != null) { eventIcon.GetComponent<Image>().sprite = evtSpr; eventIcon.GetComponent<Image>().preserveAspect = true; eventIcon.GetComponent<Image>().color = new Color(1f, 0.85f, 0.65f, 0.90f); }

            var eventTitle = AddText(eventBanner, "EventTitle", "Dragon Siege — World Boss Raid", 16, TextAnchor.MiddleLeft);
            SetAnchors(eventTitle, 0.17f, 0.38f, 0.70f, 0.76f);
            eventTitle.GetComponent<Text>().color = TextWhite;
            eventTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var etShadow = eventTitle.AddComponent<Shadow>();
            etShadow.effectColor = new Color(0, 0, 0, 0.9f);
            etShadow.effectDistance = new Vector2(1f, -1f);

            var eventDesc = AddText(eventBanner, "EventDesc", "Alliance-wide battle  \u2022  2d 14h remaining", 11, TextAnchor.MiddleLeft);
            SetAnchors(eventDesc, 0.17f, 0.08f, 0.70f, 0.38f);
            eventDesc.GetComponent<Text>().color = TextMid;

            var eventBtn = AddPanel(eventBanner, "JoinBtn", Ember);
            SetAnchors(eventBtn, 0.74f, 0.18f, 0.97f, 0.72f);
            var joinBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (joinBtnSpr != null) { eventBtn.GetComponent<Image>().sprite = joinBtnSpr; eventBtn.GetComponent<Image>().type = Image.Type.Sliced; eventBtn.GetComponent<Image>().color = new Color(0.91f, 0.45f, 0.16f, 1f); }
            eventBtn.AddComponent<Button>();
            var joinLabel = AddText(eventBtn, "Label", "JOIN", 12, TextAnchor.MiddleCenter);
            StretchToParent(joinLabel);
            joinLabel.GetComponent<Text>().color = Color.white;
            joinLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var jlShadow = joinLabel.AddComponent<Shadow>();
            jlShadow.effectColor = new Color(0, 0, 0, 0.8f);
            jlShadow.effectDistance = new Vector2(1f, -1f);

            // === MAIN PLAY BUTTON — ornate, premium ===
            var playBtn = AddPanel(canvas, "PlayButton", Blood);
            SetAnchors(playBtn, 0.22f, 0.43f, 0.78f, 0.54f);
            var playBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (playBtnSpr != null) { playBtn.GetComponent<Image>().sprite = playBtnSpr; playBtn.GetComponent<Image>().type = Image.Type.Sliced; playBtn.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.18f, 1f); }
            playBtn.AddComponent<Button>();
            // Inner glow
            var playGlow = AddPanel(playBtn, "Glow", new Color(1f, 0.35f, 0.15f, 0.12f));
            SetAnchors(playGlow, 0.05f, 0.10f, 0.95f, 0.90f);
            var playLabel = AddText(playBtn, "Label", "CAMPAIGN", 22, TextAnchor.MiddleCenter);
            StretchToParent(playLabel);
            playLabel.GetComponent<Text>().color = Color.white;
            playLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plShadow = playLabel.AddComponent<Shadow>();
            plShadow.effectColor = new Color(0, 0, 0, 0.9f);
            plShadow.effectDistance = new Vector2(2f, -2f);
            var plOutline = playLabel.AddComponent<Outline>();
            plOutline.effectColor = new Color(0.4f, 0.08f, 0.05f, 0.5f);
            plOutline.effectDistance = new Vector2(1f, -1f);

            // Quick action row — ornate buttons with icons
            var quickRow = AddPanel(canvas, "QuickActions", new Color(0, 0, 0, 0));
            SetAnchors(quickRow, 0.06f, 0.32f, 0.94f, 0.42f);
            var qrLayout = quickRow.AddComponent<HorizontalLayoutGroup>();
            qrLayout.spacing = 8;
            qrLayout.childForceExpandWidth = true;
            qrLayout.childForceExpandHeight = true;

            AddOrnateQuickAction(quickRow, "PvPBtn", "PVP ARENA", Blood, "icon_pvp");
            AddOrnateQuickAction(quickRow, "VoidRiftBtn", "VOID RIFT", Purple, "icon_arcane");
            AddOrnateQuickAction(quickRow, "DailyBtn", "DAILY QUESTS", Teal, "icon_quest");

            // === BATTLE PASS BAR — ornate frame ===
            var bpBar = AddPanel(canvas, "BattlePassBar", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(bpBar, 0.06f, 0.22f, 0.94f, 0.30f);
            if (bannerOrnateSpr != null) { bpBar.GetComponent<Image>().sprite = bannerOrnateSpr; bpBar.GetComponent<Image>().type = Image.Type.Sliced; bpBar.GetComponent<Image>().color = new Color(0.70f, 0.62f, 0.50f, 1f); }
            else { AddOutlinePanel(bpBar, GoldDim); }

            var bpLabel = AddText(bpBar, "BPLabel", "BATTLE PASS", 10, TextAnchor.MiddleLeft);
            SetAnchors(bpLabel, 0.03f, 0.55f, 0.25f, 0.95f);
            bpLabel.GetComponent<Text>().color = Gold;
            bpLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var bplShadow = bpLabel.AddComponent<Shadow>();
            bplShadow.effectColor = new Color(0, 0, 0, 0.8f);
            bplShadow.effectDistance = new Vector2(1f, -1f);

            var bpTier = AddText(bpBar, "BPTier", "Tier 12 / 50", 10, TextAnchor.MiddleLeft);
            SetAnchors(bpTier, 0.03f, 0.08f, 0.30f, 0.50f);
            bpTier.GetComponent<Text>().color = TextMid;

            var bpTrack = AddPanel(bpBar, "BPTrack", new Color(0.06f, 0.04f, 0.08f));
            SetAnchors(bpTrack, 0.30f, 0.25f, 0.82f, 0.75f);
            var bpFill = AddPanel(bpTrack, "BPFill", Gold);
            SetAnchors(bpFill, 0f, 0f, 0.24f, 1f);
            var bpGlow = AddPanel(bpTrack, "FillGlow", new Color(1f, 0.85f, 0.35f, 0.4f));
            SetAnchors(bpGlow, 0.22f, 0f, 0.27f, 1f);

            var bpClaimBtn = AddPanel(bpBar, "BPClaimBtn", Gold);
            SetAnchors(bpClaimBtn, 0.84f, 0.12f, 0.98f, 0.88f);
            if (playBtnSpr != null) { bpClaimBtn.GetComponent<Image>().sprite = playBtnSpr; bpClaimBtn.GetComponent<Image>().type = Image.Type.Sliced; bpClaimBtn.GetComponent<Image>().color = new Color(0.82f, 0.65f, 0.25f, 1f); }
            bpClaimBtn.AddComponent<Button>();
            var claimLabel = AddText(bpClaimBtn, "Label", "CLAIM", 10, TextAnchor.MiddleCenter);
            StretchToParent(claimLabel);
            claimLabel.GetComponent<Text>().color = Color.white;
            claimLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === DAILY QUEST SUMMARY — ornate frame ===
            var questSummary = AddPanel(canvas, "QuestSummary", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(questSummary, 0.06f, 0.12f, 0.94f, 0.20f);
            if (bannerOrnateSpr != null) { questSummary.GetComponent<Image>().sprite = bannerOrnateSpr; questSummary.GetComponent<Image>().type = Image.Type.Sliced; questSummary.GetComponent<Image>().color = new Color(0.58f, 0.62f, 0.68f, 1f); }
            else { AddOutlinePanel(questSummary, TealDim); }

            // Quest icon
            var questIcon = AddPanel(questSummary, "QuestIcon", new Color(0, 0, 0, 0));
            SetAnchors(questIcon, 0.02f, 0.15f, 0.10f, 0.85f);
            var qiSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_quest.png");
            if (qiSpr != null) { questIcon.GetComponent<Image>().sprite = qiSpr; questIcon.GetComponent<Image>().preserveAspect = true; questIcon.GetComponent<Image>().color = new Color(0.45f, 0.90f, 0.80f, 0.90f); }

            var questLabel = AddText(questSummary, "QLabel", "DAILY QUESTS", 10, TextAnchor.MiddleLeft);
            SetAnchors(questLabel, 0.11f, 0.55f, 0.40f, 0.95f);
            questLabel.GetComponent<Text>().color = Teal;
            questLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var qlShadow = questLabel.AddComponent<Shadow>();
            qlShadow.effectColor = new Color(0, 0, 0, 0.8f);
            qlShadow.effectDistance = new Vector2(1f, -1f);

            var questProgress = AddText(questSummary, "QProgress", "3/5 Complete  \u2022  Next: Win 2 PvP battles", 10, TextAnchor.MiddleLeft);
            SetAnchors(questProgress, 0.11f, 0.08f, 0.78f, 0.50f);
            questProgress.GetComponent<Text>().color = TextMid;

            // Quest dots — progress indicator
            var questDotsRow = AddPanel(questSummary, "QDots", new Color(0, 0, 0, 0));
            SetAnchors(questDotsRow, 0.45f, 0.58f, 0.78f, 0.90f);
            var dotsLayout = questDotsRow.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 6; dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 5; i++)
            {
                var dot = AddPanel(questDotsRow, $"Dot_{i}", i < 3 ? Teal : new Color(0.15f, 0.12f, 0.20f));
                var dle = dot.AddComponent<LayoutElement>(); dle.preferredWidth = 10; dle.preferredHeight = 10;
                if (plusRoundSpr != null) { dot.GetComponent<Image>().sprite = plusRoundSpr; dot.GetComponent<Image>().type = Image.Type.Sliced; }
            }

            // Go button for quests
            var questGoBtn = AddPanel(questSummary, "GoBtn", Teal);
            SetAnchors(questGoBtn, 0.84f, 0.15f, 0.97f, 0.85f);
            if (playBtnSpr != null) { questGoBtn.GetComponent<Image>().sprite = playBtnSpr; questGoBtn.GetComponent<Image>().type = Image.Type.Sliced; questGoBtn.GetComponent<Image>().color = new Color(0.18f, 0.72f, 0.65f, 1f); }
            questGoBtn.AddComponent<Button>();
            var goLabel = AddText(questGoBtn, "Label", "GO", 11, TextAnchor.MiddleCenter);
            StretchToParent(goLabel);
            goLabel.GetComponent<Text>().color = Color.white;
            goLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === BOTTOM NAV BAR — ornate, matches Empire exactly ===
            var navBarBg = AddPanel(canvasGo, "NavBarBg", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(navBarBg, 0f, 0f, 1f, 0.06f);

            var navBar = AddPanel(canvas, "BottomNavBar", new Color(0.04f, 0.03f, 0.07f, 0.98f));
            SetAnchors(navBar, 0f, 0f, 1f, 0.10f);

            // Triple-layer top border
            var navBorderGold = AddPanel(navBar, "TopBorderGold", new Color(0.85f, 0.68f, 0.28f, 0.95f));
            SetAnchors(navBorderGold, 0f, 0.972f, 1f, 1f);
            var navBorderDark = AddPanel(navBar, "TopBorderDark", new Color(0.35f, 0.25f, 0.10f, 0.80f));
            SetAnchors(navBorderDark, 0f, 0.955f, 1f, 0.972f);
            var navBorderThin = AddPanel(navBar, "TopBorderThin", new Color(0.72f, 0.56f, 0.22f, 0.55f));
            SetAnchors(navBorderThin, 0f, 0.948f, 1f, 0.955f);
            var navGlow1 = AddPanel(navBar, "TopGlow1", new Color(0.72f, 0.55f, 0.22f, 0.14f));
            SetAnchors(navGlow1, 0f, 0.90f, 1f, 0.948f);
            var navGlow2 = AddPanel(navBar, "TopGlow2", new Color(0.55f, 0.40f, 0.15f, 0.07f));
            SetAnchors(navGlow2, 0f, 0.85f, 1f, 0.90f);
            var navBotFade = AddPanel(navBar, "BotFade", new Color(0.02f, 0.01f, 0.04f, 0.6f));
            SetAnchors(navBotFade, 0f, 0f, 1f, 0.12f);

            // Nav items — 2 left, CENTER raised, 2 right
            var navLayoutLeft = AddPanel(navBar, "NavLeft", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutLeft, 0f, 0.02f, 0.38f, 0.94f);
            var nllLayout = navLayoutLeft.AddComponent<HorizontalLayoutGroup>();
            nllLayout.spacing = 0; nllLayout.padding = new RectOffset(4, 0, 4, 6);
            nllLayout.childForceExpandWidth = true; nllLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutLeft, "NavWorld", "WORLD", Ember, false, 0, SceneName.WorldMap);
            AddNavItem(navLayoutLeft, "NavHero", "HERO", Purple, false, 0, SceneName.Empire);
            AddNavItem(navLayoutLeft, "NavQuest", "QUEST", Teal, false, 17, SceneName.Empire);

            // Center raised button — "LOBBY" instead of "EMPIRE"
            var centerGlowOuter = AddPanel(navBar, "CenterGlowOuter", new Color(0.72f, 0.55f, 0.20f, 0.05f));
            SetAnchors(centerGlowOuter, 0.30f, 0.02f, 0.70f, 1.22f);
            var centerGlowMid = AddPanel(navBar, "CenterGlowMid", new Color(0.85f, 0.60f, 0.22f, 0.08f));
            SetAnchors(centerGlowMid, 0.33f, 0.05f, 0.67f, 1.18f);

            var centerBtn = AddPanel(navBar, "NavCenterBtn", new Color(0.08f, 0.05f, 0.14f, 0.98f));
            SetAnchors(centerBtn, 0.35f, 0.06f, 0.65f, 1.14f);
            var centerBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (centerBtnSpr != null) { var cImg = centerBtn.GetComponent<Image>(); cImg.sprite = centerBtnSpr; cImg.type = Image.Type.Sliced; cImg.color = new Color(0.82f, 0.68f, 0.40f, 1f); }
            else { AddOutlinePanel(centerBtn, new Color(0.85f, 0.68f, 0.28f, 0.95f)); }

            var centerInner = AddPanel(centerBtn, "Inner", new Color(0.04f, 0.02f, 0.08f, 0.95f));
            SetAnchors(centerInner, 0.08f, 0.06f, 0.92f, 0.94f);
            var centerHighlight = AddPanel(centerInner, "Highlight", new Color(0.20f, 0.14f, 0.28f, 0.35f));
            SetAnchors(centerHighlight, 0.04f, 0.58f, 0.96f, 0.96f);
            var centerEmberOuter = AddPanel(centerInner, "EmberOuter", new Color(0.42f, 0.12f, 0.55f, 0.10f));
            SetAnchors(centerEmberOuter, 0.05f, 0.18f, 0.95f, 0.85f);

            var centerIcon = AddPanel(centerInner, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(centerIcon, 0.12f, 0.22f, 0.88f, 0.85f);
            var empSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_empire.png");
            if (empSpr != null) { centerIcon.GetComponent<Image>().sprite = empSpr; centerIcon.GetComponent<Image>().preserveAspect = true; centerIcon.GetComponent<Image>().color = new Color(1f, 0.92f, 0.72f, 1f); }

            var centerLabel = AddText(centerInner, "Label", "LOBBY", 11, TextAnchor.MiddleCenter);
            SetAnchors(centerLabel, 0f, 0.01f, 1f, 0.22f);
            centerLabel.GetComponent<Text>().color = new Color(1f, 0.93f, 0.72f, 1f);
            centerLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clShadow = centerLabel.AddComponent<Shadow>();
            clShadow.effectColor = new Color(0, 0, 0, 0.95f);
            clShadow.effectDistance = new Vector2(1.5f, -1.5f);

            var centerTopAccent = AddPanel(centerBtn, "TopAccent", new Color(0.90f, 0.72f, 0.30f, 0.95f));
            SetAnchors(centerTopAccent, 0.06f, 0.978f, 0.94f, 1f);
            var centerBotAccent = AddPanel(centerBtn, "BotAccent", new Color(0.72f, 0.56f, 0.22f, 0.50f));
            SetAnchors(centerBotAccent, 0.10f, 0f, 0.90f, 0.025f);
            centerBtn.AddComponent<Button>();
            AddSceneNav(centerBtn, SceneName.Lobby);

            // Right nav items
            var navLayoutRight = AddPanel(navBar, "NavRight", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutRight, 0.62f, 0.02f, 1f, 0.94f);
            var nlrLayout = navLayoutRight.AddComponent<HorizontalLayoutGroup>();
            nlrLayout.spacing = 0; nlrLayout.padding = new RectOffset(0, 4, 4, 6);
            nlrLayout.childForceExpandWidth = true; nlrLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutRight, "NavBag", "BAG", GoldDim, false, 3, SceneName.Empire);
            AddNavItem(navLayoutRight, "NavMail", "MAIL", Sky, false, 5, SceneName.Empire);
            AddNavItem(navLayoutRight, "NavAlliance", "ALLIANCE", TealDim, false, 0, SceneName.Alliance);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Lobby scene: P&C-quality main menu hub");
        }

        // ===================================================================
        // COMBAT SCENE — Tactical battle HUD (P&C quality)
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Combat")]
        public static void SetupCombatScene()
        {
            var scene = OpenScene("Combat");
            var canvasGo = FindOrCreateCanvas(scene);

            // Semi-transparent combat overlay bg
            var bg = AddPanel(canvasGo, "CombatOverlay", new Color(0, 0, 0, 0));
            StretchToParent(bg);

            // Notch fill for iPhone
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);

            var canvas = CreateSafeArea(canvasGo);
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");

            // === TOP BAR — triple gold border ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.03f, 0.02f, 0.06f, 0.96f));
            SetAnchors(topBar, 0f, 0.93f, 1f, 0.995f);
            var topBorderGold = AddPanel(topBar, "BorderGold", new Color(0.85f, 0.68f, 0.28f, 0.90f));
            SetAnchors(topBorderGold, 0f, 0f, 1f, 0.035f);
            var topBorderDark = AddPanel(topBar, "BorderDark", new Color(0.35f, 0.25f, 0.10f, 0.65f));
            SetAnchors(topBorderDark, 0f, 0.035f, 1f, 0.06f);

            // Phase label — gold with glow
            var phaseLabel = AddText(topBar, "PhaseLabel", "ACTION PHASE", 16, TextAnchor.MiddleCenter);
            SetAnchors(phaseLabel, 0.28f, 0.08f, 0.72f, 0.92f);
            phaseLabel.GetComponent<Text>().color = Gold;
            phaseLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pShadow = phaseLabel.AddComponent<Shadow>();
            pShadow.effectColor = new Color(0, 0, 0, 0.9f);
            pShadow.effectDistance = new Vector2(1f, -1f);
            var pOutline = phaseLabel.AddComponent<Outline>();
            pOutline.effectColor = new Color(0.35f, 0.25f, 0.08f, 0.4f);
            pOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Turn counter — left side
            var turnCounter = AddText(topBar, "TurnCounter", "Turn 3", 12, TextAnchor.MiddleLeft);
            SetAnchors(turnCounter, 0.02f, 0.1f, 0.15f, 0.9f);
            turnCounter.GetComponent<Text>().color = TextLight;
            turnCounter.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Retreat button — ornate
            var retreatBtn = AddPanel(topBar, "RetreatBtn", BloodDark);
            SetAnchors(retreatBtn, 0.83f, 0.12f, 0.98f, 0.88f);
            if (btnOrnateSpr != null) { retreatBtn.GetComponent<Image>().sprite = btnOrnateSpr; retreatBtn.GetComponent<Image>().type = Image.Type.Sliced; retreatBtn.GetComponent<Image>().color = new Color(0.65f, 0.15f, 0.12f, 1f); }
            retreatBtn.AddComponent<Button>();
            var rtLabel = AddText(retreatBtn, "Label", "RETREAT", 10, TextAnchor.MiddleCenter);
            StretchToParent(rtLabel);
            rtLabel.GetComponent<Text>().color = Color.white;
            rtLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === TURN ORDER — ornate panel (right side) ===
            var turnPanel = AddPanel(canvas, "TurnOrderPanel", new Color(0.04f, 0.03f, 0.08f, 0.92f));
            SetAnchors(turnPanel, 0.89f, 0.32f, 0.99f, 0.925f);
            if (ornateSpr != null) { turnPanel.GetComponent<Image>().sprite = ornateSpr; turnPanel.GetComponent<Image>().type = Image.Type.Sliced; turnPanel.GetComponent<Image>().color = new Color(0.60f, 0.55f, 0.48f, 1f); }
            else { AddOutlinePanel(turnPanel, GoldDim); }

            var toTitle = AddText(turnPanel, "TOTitle", "TURN", 8, TextAnchor.MiddleCenter);
            SetAnchors(toTitle, 0f, 0.93f, 1f, 1f);
            toTitle.GetComponent<Text>().color = Gold;
            toTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var tokenArea = AddPanel(turnPanel, "TokenArea", new Color(0, 0, 0, 0));
            SetAnchors(tokenArea, 0.05f, 0.02f, 0.95f, 0.92f);
            var taLayout = tokenArea.AddComponent<VerticalLayoutGroup>();
            taLayout.spacing = 3;
            taLayout.padding = new RectOffset(2, 2, 2, 2);

            string[] heroNames = { "Kaelen", "Vorra", "Seraphyn", "Mordoc", "Lyra", "Skaros" };
            Color[] heroColors = { Blood, Ember, Purple, IronColor, Teal, BloodDark };
            float[] tokenHp = { 0.85f, 0.55f, 1.0f, 0.70f, 0.40f, 0.90f };
            for (int i = 0; i < 6; i++)
            {
                bool isActive = i == 0;
                Color tokenBg = i < 3 ? new Color(0.08f, 0.10f, 0.20f, 0.88f) : new Color(0.20f, 0.06f, 0.06f, 0.88f);
                var token = AddPanel(tokenArea, $"Token_{i}", tokenBg);
                token.AddComponent<LayoutElement>().preferredHeight = 34;
                // Active token — bright gold border + outer glow
                if (isActive)
                {
                    AddOutlinePanel(token, new Color(0.90f, 0.72f, 0.28f, 0.95f));
                    var activeGlow = AddPanel(token, "Glow", new Color(0.85f, 0.68f, 0.28f, 0.12f));
                    StretchToParent(activeGlow);
                    activeGlow.AddComponent<LayoutElement>().ignoreLayout = true;
                }
                else { AddOutlinePanel(token, new Color(0.30f, 0.25f, 0.18f, 0.3f)); }
                // Portrait with initial
                var tIcon = AddPanel(token, "Icon", new Color(heroColors[i].r * 0.4f, heroColors[i].g * 0.4f, heroColors[i].b * 0.4f, 1f));
                SetAnchors(tIcon, 0.04f, 0.08f, 0.36f, 0.92f);
                AddOutlinePanel(tIcon, new Color(0.55f, 0.42f, 0.18f, isActive ? 0.7f : 0.3f));
                var tInit = AddText(tIcon, "Init", heroNames[i][..1], 10, TextAnchor.MiddleCenter);
                StretchToParent(tInit);
                tInit.GetComponent<Text>().color = new Color(heroColors[i].r + 0.2f, heroColors[i].g + 0.2f, heroColors[i].b + 0.2f, 0.85f);
                tInit.GetComponent<Text>().fontStyle = FontStyle.Bold;
                // Name — show 4 chars
                int nameLen = Mathf.Min(4, heroNames[i].Length);
                var tName = AddText(token, "Name", heroNames[i][..nameLen], 7, TextAnchor.MiddleLeft);
                SetAnchors(tName, 0.40f, 0.35f, 1f, 0.95f);
                tName.GetComponent<Text>().color = isActive ? Gold : TextMid;
                tName.GetComponent<Text>().fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                // Mini HP bar in token
                var miniHpBg = AddPanel(token, "MiniHp", new Color(0.06f, 0.05f, 0.08f, 0.8f));
                SetAnchors(miniHpBg, 0.40f, 0.08f, 0.95f, 0.28f);
                Color tkHpColor = tokenHp[i] > 0.5f ? BarHpGreen : tokenHp[i] > 0.25f ? Ember : BarHpRed;
                var miniHpFill = AddPanel(miniHpBg, "Fill", tkHpColor);
                SetAnchors(miniHpFill, 0f, 0f, tokenHp[i], 1f);
            }

            // === PLAYER HERO STATUS — left side, ornate panels ===
            var playerStatus = AddPanel(canvas, "PlayerHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(playerStatus, 0.01f, 0.50f, 0.16f, 0.925f);
            var psLayout = playerStatus.AddComponent<VerticalLayoutGroup>();
            psLayout.spacing = 5;

            string[] pHeroes = { "Kaelen", "Vorra", "Seraphyn" };
            float[] pHp = { 0.85f, 0.55f, 1.0f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(playerStatus, pHeroes[i], pHp[i], heroColors[i], true);

            // === ENEMY HERO STATUS — right-center ===
            var enemyStatus = AddPanel(canvas, "EnemyHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(enemyStatus, 0.73f, 0.50f, 0.88f, 0.925f);
            var esLayout = enemyStatus.AddComponent<VerticalLayoutGroup>();
            esLayout.spacing = 5;

            string[] eHeroes = { "Mordoc", "Lyra", "Skaros" };
            float[] eHp = { 0.70f, 0.40f, 0.90f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(enemyStatus, eHeroes[i], eHp[i], heroColors[i + 3], false);

            // === ENERGY DISPLAY — ornate panel ===
            var energyPanel = AddPanel(canvas, "EnergyPanel", new Color(0.04f, 0.03f, 0.08f, 0.92f));
            SetAnchors(energyPanel, 0.01f, 0.20f, 0.16f, 0.32f);
            if (ornateSpr != null) { energyPanel.GetComponent<Image>().sprite = ornateSpr; energyPanel.GetComponent<Image>().type = Image.Type.Sliced; energyPanel.GetComponent<Image>().color = new Color(0.50f, 0.55f, 0.62f, 1f); }
            else { AddOutlinePanel(energyPanel, SkyDim); }

            var enLabel = AddText(energyPanel, "EnergyLabel", "ENERGY", 9, TextAnchor.MiddleCenter);
            SetAnchors(enLabel, 0f, 0.65f, 1f, 0.95f);
            enLabel.GetComponent<Text>().color = Sky;
            enLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var enlShadow = enLabel.AddComponent<Shadow>();
            enlShadow.effectColor = new Color(0, 0, 0, 0.8f);
            enlShadow.effectDistance = new Vector2(1f, -1f);

            var orbRow = AddPanel(energyPanel, "OrbRow", new Color(0, 0, 0, 0));
            SetAnchors(orbRow, 0.06f, 0.12f, 0.94f, 0.62f);
            var orbLayout = orbRow.AddComponent<HorizontalLayoutGroup>();
            orbLayout.spacing = 5;
            orbLayout.childAlignment = TextAnchor.MiddleCenter;

            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            for (int i = 0; i < 4; i++)
            {
                bool filled = i < 3;
                // Glow behind filled orbs
                var orbContainer = AddPanel(orbRow, $"OrbC_{i}", new Color(0, 0, 0, 0));
                orbContainer.AddComponent<LayoutElement>().preferredWidth = 24;
                if (filled)
                {
                    var orbGlow = AddPanel(orbContainer, "Glow", new Color(0.20f, 0.50f, 0.85f, 0.18f));
                    StretchToParent(orbGlow);
                    orbGlow.AddComponent<LayoutElement>().ignoreLayout = true;
                }
                var orb = AddPanel(orbContainer, $"Orb_{i}", filled ? BarEnergy : BarEnergyDim);
                SetAnchors(orb, 0.08f, 0.08f, 0.92f, 0.92f);
                if (circleSpr != null)
                {
                    orb.GetComponent<Image>().sprite = circleSpr;
                    orb.GetComponent<Image>().type = Image.Type.Sliced;
                    orb.GetComponent<Image>().color = filled ? new Color(0.28f, 0.68f, 0.95f, 1f) : new Color(0.10f, 0.10f, 0.18f, 0.55f);
                }
                if (filled) { AddOutlinePanel(orb, new Color(0.45f, 0.72f, 1f, 0.35f)); }
            }

            var enText = AddText(energyPanel, "EnergyCount", "3 / 4", 11, TextAnchor.MiddleCenter);
            SetAnchors(enText, 0f, 0.0f, 1f, 0.18f);
            enText.GetComponent<Text>().color = TextLight;
            enText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === CARD HAND — ornate card tray ===
            var cardTray = AddPanel(canvas, "CardTray", new Color(0.03f, 0.02f, 0.06f, 0.96f));
            SetAnchors(cardTray, 0.08f, 0f, 0.87f, 0.20f);
            if (ornateSpr != null) { cardTray.GetComponent<Image>().sprite = ornateSpr; cardTray.GetComponent<Image>().type = Image.Type.Sliced; cardTray.GetComponent<Image>().color = new Color(0.40f, 0.38f, 0.35f, 1f); }
            // Top gold border + glow for card tray
            var ctBorder = AddPanel(cardTray, "TopBorder", new Color(0.78f, 0.62f, 0.24f, 0.75f));
            SetAnchors(ctBorder, 0.01f, 0.97f, 0.99f, 1f);
            ctBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            var ctGlow = AddPanel(cardTray, "TopGlow", new Color(0.55f, 0.42f, 0.15f, 0.12f));
            SetAnchors(ctGlow, 0f, 0.88f, 1f, 0.97f);
            ctGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            // "HAND" label at top of tray
            var handLabel = AddText(cardTray, "HandLabel", "HAND", 8, TextAnchor.MiddleCenter);
            SetAnchors(handLabel, 0.42f, 0.90f, 0.58f, 1f);
            handLabel.GetComponent<Text>().color = new Color(0.75f, 0.60f, 0.25f, 0.65f);
            handLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            handLabel.AddComponent<LayoutElement>().ignoreLayout = true;

            var cardContainer = AddPanel(cardTray, "CardContainer", new Color(0, 0, 0, 0));
            SetAnchors(cardContainer, 0.01f, 0.03f, 0.99f, 0.95f);
            var ccLayout = cardContainer.AddComponent<HorizontalLayoutGroup>();
            ccLayout.spacing = 5;
            ccLayout.padding = new RectOffset(4, 4, 2, 2);
            ccLayout.childAlignment = TextAnchor.MiddleCenter;

            string[] cardNames = { "Fire Bolt", "Shield Wall", "Shadow Strike", "Heal Wave", "Ice Shard" };
            int[] cardCosts = { 1, 2, 3, 2, 1 };
            Color[] cardColors = { Ember, IronColor, Purple, Teal, Sky };
            string[] cardTypes = { "ATK", "DEF", "ATK", "HEAL", "ATK" };
            int[] cardDmg = { 45, 0, 72, 35, 38 };
            string[] cardFrames = { "Fire", "Physical", "Shadow", "Nature", "Ice" };

            for (int i = 0; i < 5; i++)
                AddCardWidget(cardContainer, cardNames[i], cardCosts[i], cardColors[i], cardTypes[i], cardDmg[i], cardFrames[i]);

            // === END TURN — ornate button with urgency glow ===
            var endTurnGlow = AddPanel(canvas, "EndTurnGlow", new Color(0.75f, 0.20f, 0.12f, 0.10f));
            SetAnchors(endTurnGlow, 0.86f, 0f, 1f, 0.22f);
            var endTurnBtn = AddPanel(canvas, "EndTurnButton", Blood);
            SetAnchors(endTurnBtn, 0.87f, 0.02f, 0.99f, 0.20f);
            if (btnOrnateSpr != null) { endTurnBtn.GetComponent<Image>().sprite = btnOrnateSpr; endTurnBtn.GetComponent<Image>().type = Image.Type.Sliced; endTurnBtn.GetComponent<Image>().color = new Color(0.82f, 0.22f, 0.15f, 1f); }
            AddOutlinePanel(endTurnBtn, new Color(0.95f, 0.45f, 0.20f, 0.35f));
            endTurnBtn.AddComponent<Button>();
            var etLabel = AddText(endTurnBtn, "Label", "END\nTURN", 11, TextAnchor.MiddleCenter);
            StretchToParent(etLabel);
            etLabel.GetComponent<Text>().color = Color.white;
            etLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var etShadow = etLabel.AddComponent<Shadow>();
            etShadow.effectColor = new Color(0, 0, 0, 0.9f);
            etShadow.effectDistance = new Vector2(1f, -1f);
            var etOutline = etLabel.AddComponent<Outline>();
            etOutline.effectColor = new Color(0.75f, 0.15f, 0.10f, 0.4f);
            etOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // === VICTORY PANEL — ornate frame ===
            var victoryPanel = AddPanel(canvasGo, "VictoryPanel", new Color(0.02f, 0.06f, 0.02f, 0.95f));
            StretchToParent(victoryPanel);
            var vicFrame = AddPanel(victoryPanel, "Frame", new Color(0.05f, 0.04f, 0.10f, 0.95f));
            SetAnchors(vicFrame, 0.12f, 0.25f, 0.88f, 0.75f);
            if (ornateSpr != null) { vicFrame.GetComponent<Image>().sprite = ornateSpr; vicFrame.GetComponent<Image>().type = Image.Type.Sliced; vicFrame.GetComponent<Image>().color = new Color(0.75f, 0.72f, 0.60f, 1f); }
            else { AddOutlinePanel(vicFrame, Gold); }
            var vicTitle = AddText(vicFrame, "Title", "VICTORY", 48, TextAnchor.MiddleCenter);
            SetAnchors(vicTitle, 0.1f, 0.6f, 0.9f, 0.9f);
            vicTitle.GetComponent<Text>().color = Gold;
            vicTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(vicTitle, new Color(0.3f, 0.2f, 0.05f), 2f);
            var vicShadow = vicTitle.AddComponent<Shadow>();
            vicShadow.effectColor = new Color(0, 0, 0, 0.9f);
            vicShadow.effectDistance = new Vector2(3f, -3f);
            var vicRewards = AddText(vicFrame, "Rewards", "+250 XP   +500 Gold   +3 Hero Shards", 14, TextAnchor.MiddleCenter);
            SetAnchors(vicRewards, 0.1f, 0.35f, 0.9f, 0.55f);
            vicRewards.GetComponent<Text>().color = TextLight;
            var vicContinue = AddPanel(vicFrame, "ContinueBtn", Gold);
            SetAnchors(vicContinue, 0.28f, 0.06f, 0.72f, 0.25f);
            if (btnOrnateSpr != null) { vicContinue.GetComponent<Image>().sprite = btnOrnateSpr; vicContinue.GetComponent<Image>().type = Image.Type.Sliced; vicContinue.GetComponent<Image>().color = new Color(0.82f, 0.65f, 0.25f, 1f); }
            vicContinue.AddComponent<Button>();
            var vcLabel = AddText(vicContinue, "Label", "CONTINUE", 14, TextAnchor.MiddleCenter);
            StretchToParent(vcLabel);
            vcLabel.GetComponent<Text>().color = Color.white;
            vcLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            victoryPanel.SetActive(false);

            // === DEFEAT PANEL — ornate frame ===
            var defeatPanel = AddPanel(canvasGo, "DefeatPanel", new Color(0.08f, 0.02f, 0.02f, 0.95f));
            StretchToParent(defeatPanel);
            var defFrame = AddPanel(defeatPanel, "Frame", new Color(0.05f, 0.03f, 0.06f, 0.95f));
            SetAnchors(defFrame, 0.12f, 0.25f, 0.88f, 0.75f);
            if (ornateSpr != null) { defFrame.GetComponent<Image>().sprite = ornateSpr; defFrame.GetComponent<Image>().type = Image.Type.Sliced; defFrame.GetComponent<Image>().color = new Color(0.65f, 0.45f, 0.42f, 1f); }
            else { AddOutlinePanel(defFrame, Blood); }
            var defTitle = AddText(defFrame, "Title", "DEFEAT", 48, TextAnchor.MiddleCenter);
            SetAnchors(defTitle, 0.1f, 0.6f, 0.9f, 0.9f);
            defTitle.GetComponent<Text>().color = Blood;
            defTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(defTitle, new Color(0.3f, 0.05f, 0.05f), 2f);
            var defShadow = defTitle.AddComponent<Shadow>();
            defShadow.effectColor = new Color(0, 0, 0, 0.9f);
            defShadow.effectDistance = new Vector2(3f, -3f);
            var defMsg = AddText(defFrame, "Message", "Your heroes have fallen. Regroup and try again.", 14, TextAnchor.MiddleCenter);
            SetAnchors(defMsg, 0.1f, 0.35f, 0.9f, 0.55f);
            defMsg.GetComponent<Text>().color = TextMid;
            var defRetry = AddPanel(defFrame, "RetryBtn", Blood);
            SetAnchors(defRetry, 0.08f, 0.06f, 0.48f, 0.25f);
            if (btnOrnateSpr != null) { defRetry.GetComponent<Image>().sprite = btnOrnateSpr; defRetry.GetComponent<Image>().type = Image.Type.Sliced; defRetry.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.15f, 1f); }
            defRetry.AddComponent<Button>();
            var drLabel = AddText(defRetry, "Label", "RETRY", 14, TextAnchor.MiddleCenter);
            StretchToParent(drLabel);
            drLabel.GetComponent<Text>().color = Color.white;
            drLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var defQuit = AddPanel(defFrame, "QuitBtn", new Color(0.3f, 0.25f, 0.2f));
            SetAnchors(defQuit, 0.52f, 0.06f, 0.92f, 0.25f);
            if (btnOrnateSpr != null) { defQuit.GetComponent<Image>().sprite = btnOrnateSpr; defQuit.GetComponent<Image>().type = Image.Type.Sliced; defQuit.GetComponent<Image>().color = new Color(0.45f, 0.38f, 0.30f, 1f); }
            defQuit.AddComponent<Button>();
            var dqLabel = AddText(defQuit, "Label", "RETREAT", 14, TextAnchor.MiddleCenter);
            StretchToParent(dqLabel);
            dqLabel.GetComponent<Text>().color = TextLight;
            dqLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            defeatPanel.SetActive(false);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Combat scene: premium battle HUD");
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
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.10f, 0.07f, 0.16f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);
            // Gold border at bottom of notch fill (matches resource bar border)
            var notchBorder = AddPanel(notchFill, "Border", new Color(0.78f, 0.62f, 0.24f, 0.85f));
            SetAnchors(notchBorder, 0f, 0f, 1f, 0.012f);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === RESOURCE BAR (top strip — slightly taller for readability) ===
            var resBarBg = AddPanel(canvas, "ResourceBarBg", new Color(0.10f, 0.07f, 0.16f, 0.96f));
            SetAnchors(resBarBg, 0f, 0.957f, 1f, 0.995f);
            // Gold bottom border — thicker and brighter for clear separation
            var resBarBorder = AddPanel(resBarBg, "BottomBorder", new Color(0.78f, 0.62f, 0.24f, 0.85f));
            SetAnchors(resBarBorder, 0f, 0f, 1f, 0.06f);
            // Top highlight for glass effect
            var resBarHighlight = AddPanel(resBarBg, "TopHighlight", new Color(0.18f, 0.12f, 0.28f, 0.30f));
            SetAnchors(resBarHighlight, 0f, 0.55f, 1f, 1f);

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

            // "+" button — small gold circular button (matches dark fantasy theme)
            var plusBtn = AddPanel(resBar, "AddBtn", new Color(0.72f, 0.56f, 0.22f, 0.95f));
            var plusLE = plusBtn.AddComponent<LayoutElement>();
            plusLE.preferredWidth = 26;
            plusLE.preferredHeight = 26;
            plusLE.minWidth = 22;
            plusLE.minHeight = 22;
            plusLE.flexibleWidth = 0;
            // Use Kenney round button for circular shape
            var plusRoundSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (plusRoundSpr != null)
            {
                plusBtn.GetComponent<Image>().sprite = plusRoundSpr;
                plusBtn.GetComponent<Image>().type = Image.Type.Sliced;
                plusBtn.GetComponent<Image>().color = new Color(0.25f, 0.72f, 0.38f, 1f); // emerald green
            }
            plusBtn.AddComponent<Button>();
            // "+" text — white, centered
            var plusText = AddText(plusBtn, "Label", "+", 14, TextAnchor.MiddleCenter);
            StretchToParent(plusText);
            plusText.GetComponent<Text>().color = Color.white;
            plusText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plusShadow = plusText.AddComponent<Shadow>();
            plusShadow.effectColor = new Color(0, 0, 0, 0.6f);
            plusShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === PLAYER AVATAR BLOCK — same width as build queue (0.01–0.18) ===
            var avatarBlock = AddPanel(canvas, "AvatarBlock", new Color(0.08f, 0.05f, 0.14f, 0.96f));
            SetAnchors(avatarBlock, 0.01f, 0.875f, 0.18f, 0.955f);
            // Use ornate frame for premium look (matches event buttons)
            var avatarOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (avatarOrnateSpr != null)
            {
                avatarBlock.GetComponent<Image>().sprite = avatarOrnateSpr;
                avatarBlock.GetComponent<Image>().type = Image.Type.Sliced;
                avatarBlock.GetComponent<Image>().color = new Color(0.70f, 0.60f, 0.48f, 1f);
            }
            else
            {
                // Fallback: double gold border
                AddOutlinePanel(avatarBlock, new Color(0.82f, 0.65f, 0.28f, 0.85f));
            }
            var avatarInnerBorder = AddPanel(avatarBlock, "InnerBorder", new Color(0, 0, 0, 0));
            SetAnchors(avatarInnerBorder, 0.04f, 0.03f, 0.96f, 0.97f);
            AddOutlinePanel(avatarInnerBorder, new Color(0.55f, 0.42f, 0.18f, 0.50f));
            // Portrait fill — use avatar_default sprite for proper look
            var avatarPortrait = AddPanel(avatarBlock, "Portrait", new Color(0.12f, 0.08f, 0.18f, 0.95f));
            SetAnchors(avatarPortrait, 0.06f, 0.05f, 0.94f, 0.95f);
            var avatarSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/avatar_default.png");
            if (avatarSpr != null)
            {
                avatarPortrait.GetComponent<Image>().sprite = avatarSpr;
                avatarPortrait.GetComponent<Image>().preserveAspect = true;
                avatarPortrait.GetComponent<Image>().color = Color.white;
            }
            // Subtle inner light on portrait (top highlight)
            var avatarHighlight = AddPanel(avatarPortrait, "Highlight", new Color(0.50f, 0.35f, 0.65f, 0.10f));
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

            // === INFO PANEL — right of avatar, ORNATE premium panel ===
            var infoPanelBg = AddPanel(canvas, "InfoPanelBg", new Color(0.08f, 0.06f, 0.14f, 0.96f));
            SetAnchors(infoPanelBg, 0.19f, 0.895f, 0.84f, 0.958f);
            var ornatePanelSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (ornatePanelSpr != null)
            {
                infoPanelBg.GetComponent<Image>().sprite = ornatePanelSpr;
                infoPanelBg.GetComponent<Image>().type = Image.Type.Sliced;
                infoPanelBg.GetComponent<Image>().color = new Color(0.68f, 0.60f, 0.50f, 1f); // warm visible tint matching event buttons
            }
            // Inner content fill — covers ornate frame columns so text is clean
            var infoContentFill = AddPanel(infoPanelBg, "ContentFill", new Color(0.06f, 0.04f, 0.12f, 0.92f));
            SetAnchors(infoContentFill, 0.04f, 0.06f, 0.96f, 0.94f);
            // Top highlight gradient for glass effect
            var infoTopGrad = AddPanel(infoContentFill, "TopGrad", new Color(0.18f, 0.12f, 0.25f, 0.30f));
            SetAnchors(infoTopGrad, 0f, 0.50f, 1f, 1f);

            // === TOP ROW: Player Name + VIP Badge ===
            // Player name — warm gold, clean typography with outline for readability
            var avatarName = AddText(infoContentFill, "PlayerName", "Commander", 13, TextAnchor.MiddleLeft);
            SetAnchors(avatarName, 0.02f, 0.52f, 0.50f, 0.98f);
            avatarName.GetComponent<Text>().color = new Color(0.92f, 0.78f, 0.38f, 1f);
            avatarName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = avatarName.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1f, -1f);
            var nameOutline = avatarName.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0.15f, 0.10f, 0.05f, 0.5f);
            nameOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // VIP badge — wider, layered purple/gold premium pill with glow
            var vipGlow = AddPanel(infoContentFill, "VipGlow", new Color(0.50f, 0.20f, 0.65f, 0.12f));
            SetAnchors(vipGlow, 0.48f, 0.48f, 0.82f, 1f);
            var vipOuter = AddPanel(infoContentFill, "VipOuter", new Color(0.65f, 0.48f, 0.20f, 0.85f));
            SetAnchors(vipOuter, 0.50f, 0.52f, 0.80f, 0.96f);
            var vipBadge = AddPanel(vipOuter, "VipBadge", new Color(0.42f, 0.12f, 0.55f, 0.95f));
            SetAnchors(vipBadge, 0.04f, 0.06f, 0.96f, 0.94f);
            // Inner shimmer — brighter
            var vipShimmer = AddPanel(vipBadge, "Shimmer", new Color(0.70f, 0.45f, 0.90f, 0.25f));
            SetAnchors(vipShimmer, 0f, 0.40f, 1f, 1f);
            var vipText = AddText(vipBadge, "Label", "VIP 11", 10, TextAnchor.MiddleCenter);
            StretchToParent(vipText);
            vipText.GetComponent<Text>().color = new Color(1f, 0.95f, 0.75f, 1f);
            vipText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vipShadow = vipText.AddComponent<Shadow>();
            vipShadow.effectColor = new Color(0, 0, 0, 0.85f);
            vipShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Server tag — small muted tag at right
            var serverTag = AddText(infoContentFill, "ServerTag", "S:142", 9, TextAnchor.MiddleRight);
            SetAnchors(serverTag, 0.82f, 0.56f, 0.98f, 0.96f);
            serverTag.GetComponent<Text>().color = new Color(0.50f, 0.48f, 0.42f, 0.75f);

            // === BOTTOM ROW: Power + Coordinates ===
            // Power icon — golden swords with glow
            var powerIconText = AddText(infoContentFill, "PowerIcon", "\u2694", 12, TextAnchor.MiddleCenter);
            SetAnchors(powerIconText, 0.01f, 0.04f, 0.09f, 0.50f);
            powerIconText.GetComponent<Text>().color = new Color(0.92f, 0.75f, 0.32f, 1f);
            var piShadow = powerIconText.AddComponent<Shadow>();
            piShadow.effectColor = new Color(0.50f, 0.35f, 0.10f, 0.5f);
            piShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Power value — bright white, larger, prominent with subtle glow
            var powerGlow = AddPanel(infoContentFill, "PowerGlow", new Color(0.85f, 0.65f, 0.25f, 0.08f));
            SetAnchors(powerGlow, 0f, 0f, 0.55f, 0.52f);
            var powerVal = AddText(infoContentFill, "PowerValue", "355.6M", 14, TextAnchor.MiddleLeft);
            SetAnchors(powerVal, 0.09f, 0.02f, 0.50f, 0.50f);
            powerVal.GetComponent<Text>().color = new Color(0.97f, 0.95f, 0.90f, 1f);
            powerVal.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pvShadow = powerVal.AddComponent<Shadow>();
            pvShadow.effectColor = new Color(0, 0, 0, 0.85f);
            pvShadow.effectDistance = new Vector2(1f, -1f);

            // Thin vertical separator before coords
            var coordSep = AddPanel(infoContentFill, "CoordSep", new Color(0.55f, 0.42f, 0.20f, 0.40f));
            SetAnchors(coordSep, 0.56f, 0.08f, 0.565f, 0.46f);

            // Coordinates — clean, right-aligned (P&C format)
            var coordText = AddText(infoContentFill, "Coords", "K:12 (482, 317)", 11, TextAnchor.MiddleRight);
            SetAnchors(coordText, 0.57f, 0.02f, 0.98f, 0.50f);
            coordText.GetComponent<Text>().color = new Color(0.62f, 0.58f, 0.50f, 0.90f);
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

            // === CHAT BAR — Tappable chat bar with channel tabs, messages, and input (P&C style) ===
            var chatBar = AddPanel(canvas, "ChatBar", new Color(0.08f, 0.06f, 0.12f, 0.92f));
            SetAnchors(chatBar, 0f, 0.142f, 0.84f, 0.225f);
            // Top gold trim
            var chatTopBorder = AddPanel(chatBar, "TopBorder", new Color(0.78f, 0.62f, 0.24f, 0.70f));
            SetAnchors(chatTopBorder, 0f, 0.96f, 1f, 1f);
            var chatTopGlow = AddPanel(chatBar, "TopGlow", new Color(0.55f, 0.42f, 0.15f, 0.15f));
            SetAnchors(chatTopGlow, 0f, 0.85f, 1f, 0.96f);
            // Bottom gold trim
            var chatBotBorder = AddPanel(chatBar, "BottomBorder", new Color(0.65f, 0.50f, 0.18f, 0.50f));
            SetAnchors(chatBotBorder, 0f, 0f, 1f, 0.04f);

            // Channel tabs — left side (World | Alliance | Private)
            var chatTabs = AddPanel(chatBar, "Tabs", new Color(0, 0, 0, 0));
            SetAnchors(chatTabs, 0.01f, 0.55f, 0.42f, 0.95f);
            var chatTabsHlg = chatTabs.AddComponent<HorizontalLayoutGroup>();
            chatTabsHlg.spacing = 2; chatTabsHlg.childForceExpandWidth = true; chatTabsHlg.childForceExpandHeight = true;
            chatTabsHlg.padding = new RectOffset(2, 2, 0, 0);
            // Active tab (World)
            var tabWorld = AddPanel(chatTabs, "TabWorld", new Color(0.18f, 0.12f, 0.06f, 0.65f));
            tabWorld.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tabWorldBorder = AddPanel(tabWorld, "Border", new Color(0.72f, 0.56f, 0.22f, 0.70f));
            SetAnchors(tabWorldBorder, 0f, 0f, 1f, 0.06f);
            var tabWorldText = AddText(tabWorld, "Text", "World", 8, TextAnchor.MiddleCenter);
            StretchToParent(tabWorldText);
            tabWorldText.GetComponent<Text>().color = new Color(1f, 0.92f, 0.72f, 1f);
            tabWorldText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            // Inactive tab (Alliance)
            var tabAlliance = AddPanel(chatTabs, "TabAlliance", new Color(0.06f, 0.04f, 0.10f, 0.40f));
            tabAlliance.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tabAlliText = AddText(tabAlliance, "Text", "Alliance", 8, TextAnchor.MiddleCenter);
            StretchToParent(tabAlliText);
            tabAlliText.GetComponent<Text>().color = new Color(0.55f, 0.50f, 0.45f, 0.80f);
            // Red dot on alliance tab for unread
            var tabAlliDot = AddPanel(tabAlliance, "UnreadDot", new Color(0.88f, 0.14f, 0.14f, 1f));
            SetAnchors(tabAlliDot, 0.82f, 0.65f, 0.95f, 0.90f);
            // Inactive tab (Private)
            var tabPm = AddPanel(chatTabs, "TabPM", new Color(0.06f, 0.04f, 0.10f, 0.40f));
            tabPm.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tabPmText = AddText(tabPm, "Text", "PM", 8, TextAnchor.MiddleCenter);
            StretchToParent(tabPmText);
            tabPmText.GetComponent<Text>().color = new Color(0.55f, 0.50f, 0.45f, 0.80f);

            // Chat message area — shows latest message
            var chatMsgArea = AddPanel(chatBar, "MessageArea", new Color(0, 0, 0, 0));
            SetAnchors(chatMsgArea, 0.01f, 0.06f, 0.78f, 0.55f);
            var chatMsg = AddText(chatMsgArea, "Message", "<color=#2EC7A6>NBAHeartless:</color> launched a rally at Lv. 17 Monster...", 9, TextAnchor.MiddleLeft);
            StretchToParent(chatMsg);
            chatMsg.GetComponent<Text>().color = TextLight;
            chatMsg.GetComponent<Text>().supportRichText = true;
            var chatMsgShadow = chatMsg.AddComponent<Shadow>();
            chatMsgShadow.effectColor = new Color(0, 0, 0, 0.8f);
            chatMsgShadow.effectDistance = new Vector2(1f, -1f);

            // Input field area — right side with ornate border
            var chatInputBg = AddPanel(chatBar, "InputBg", new Color(0.06f, 0.04f, 0.10f, 0.85f));
            SetAnchors(chatInputBg, 0.78f, 0.10f, 0.99f, 0.90f);
            AddOutlinePanel(chatInputBg, new Color(0.45f, 0.35f, 0.15f, 0.45f));
            var chatInputLabel = AddText(chatInputBg, "Placeholder", "Chat...", 9, TextAnchor.MiddleCenter);
            StretchToParent(chatInputLabel);
            chatInputLabel.GetComponent<Text>().color = new Color(0.45f, 0.40f, 0.35f, 0.65f);
            chatInputLabel.GetComponent<Text>().fontStyle = FontStyle.Italic;
            chatInputBg.AddComponent<Button>(); // Tappable to open full chat

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
            var navBarBg = AddPanel(canvasGo, "NavBarBg", new Color(0.08f, 0.06f, 0.12f, 1f));
            SetAnchors(navBarBg, 0f, 0f, 1f, 0.06f);

            var navBar = AddPanel(canvas, "BottomNavBar", new Color(0.10f, 0.07f, 0.16f, 0.98f));
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

            AddNavItem(navLayoutLeft, "NavWorld", "WORLD", Ember, true, 0, SceneName.WorldMap);
            AddNavItem(navLayoutLeft, "NavHero", "HERO", Purple, false, 0, SceneName.Lobby);
            AddNavItem(navLayoutLeft, "NavQuest", "QUEST", Teal, false, 17, SceneName.Lobby);

            // === CENTER BUTTON — raised ornate jewel button (extends above bar like P&C) ===
            // Outer warm glow halo behind the raised button
            var centerGlowOuter = AddPanel(navBar, "CenterGlowOuter", new Color(0.72f, 0.55f, 0.20f, 0.05f));
            SetAnchors(centerGlowOuter, 0.30f, 0.02f, 0.70f, 1.22f);
            var centerGlowMid = AddPanel(navBar, "CenterGlowMid", new Color(0.85f, 0.60f, 0.22f, 0.08f));
            SetAnchors(centerGlowMid, 0.33f, 0.05f, 0.67f, 1.18f);

            // Main button body — use btn_ornate sprite for premium frame
            var centerBtn = AddPanel(navBar, "NavCenterBtn", new Color(0.08f, 0.05f, 0.14f, 0.98f));
            SetAnchors(centerBtn, 0.35f, 0.06f, 0.65f, 1.14f);
            var centerBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (centerBtnSpr != null)
            {
                var cImg = centerBtn.GetComponent<Image>();
                cImg.sprite = centerBtnSpr;
                cImg.type = Image.Type.Sliced;
                cImg.color = new Color(0.82f, 0.68f, 0.40f, 1f); // warm gold ornate frame
            }
            else
            {
                // Fallback: triple gold border
                AddOutlinePanel(centerBtn, new Color(0.85f, 0.68f, 0.28f, 0.95f));
            }

            // Inner dark fill with gradient
            var centerInner = AddPanel(centerBtn, "Inner", new Color(0.04f, 0.02f, 0.08f, 0.95f));
            SetAnchors(centerInner, 0.08f, 0.06f, 0.92f, 0.94f);
            // Top highlight (glass reflection)
            var centerHighlight = AddPanel(centerInner, "Highlight", new Color(0.20f, 0.14f, 0.28f, 0.35f));
            SetAnchors(centerHighlight, 0.04f, 0.58f, 0.96f, 0.96f);
            // Ember glow behind icon — warm dual-layer radial
            var centerEmberOuter = AddPanel(centerInner, "EmberOuter", new Color(0.91f, 0.45f, 0.16f, 0.10f));
            SetAnchors(centerEmberOuter, 0.05f, 0.18f, 0.95f, 0.85f);
            var centerEmberInner = AddPanel(centerInner, "EmberInner", new Color(0.95f, 0.55f, 0.20f, 0.15f));
            SetAnchors(centerEmberInner, 0.15f, 0.28f, 0.85f, 0.75f);

            // Icon — empire castle sprite, large and centered
            var centerIcon = AddPanel(centerInner, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(centerIcon, 0.12f, 0.22f, 0.88f, 0.85f);
            var empSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_empire.png");
            if (empSpr != null)
            {
                centerIcon.GetComponent<Image>().sprite = empSpr;
                centerIcon.GetComponent<Image>().preserveAspect = true;
                centerIcon.GetComponent<Image>().color = new Color(1f, 0.92f, 0.72f, 1f); // warm golden tint
            }
            else { centerIcon.GetComponent<Image>().color = Ember; }

            // "EMPIRE" label — warm gold, bold, with shadow + outline
            var centerLabel = AddText(centerInner, "Label", "EMPIRE", 11, TextAnchor.MiddleCenter);
            SetAnchors(centerLabel, 0f, 0.01f, 1f, 0.22f);
            centerLabel.GetComponent<Text>().color = new Color(1f, 0.93f, 0.72f, 1f);
            centerLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clShadow = centerLabel.AddComponent<Shadow>();
            clShadow.effectColor = new Color(0, 0, 0, 0.95f);
            clShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var clOutline = centerLabel.AddComponent<Outline>();
            clOutline.effectColor = new Color(0.45f, 0.32f, 0.10f, 0.50f);
            clOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Top gold crown accent on raised button
            var centerTopAccent = AddPanel(centerBtn, "TopAccent", new Color(0.90f, 0.72f, 0.30f, 0.95f));
            SetAnchors(centerTopAccent, 0.06f, 0.978f, 0.94f, 1f);
            var centerTopAccent2 = AddPanel(centerBtn, "TopAccent2", new Color(0.60f, 0.45f, 0.20f, 0.55f));
            SetAnchors(centerTopAccent2, 0.10f, 0.962f, 0.90f, 0.978f);
            // Bottom accent strip
            var centerBotAccent = AddPanel(centerBtn, "BotAccent", new Color(0.72f, 0.56f, 0.22f, 0.50f));
            SetAnchors(centerBotAccent, 0.10f, 0f, 0.90f, 0.025f);
            centerBtn.AddComponent<Button>();
            AddSceneNav(centerBtn, SceneName.Empire);

            // Right nav items
            var navLayoutRight = AddPanel(navBar, "NavRight", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutRight, 0.62f, 0.02f, 1f, 0.94f);
            var nlrLayout = navLayoutRight.AddComponent<HorizontalLayoutGroup>();
            nlrLayout.spacing = 0;
            nlrLayout.padding = new RectOffset(0, 4, 4, 6);
            nlrLayout.childForceExpandWidth = true;
            nlrLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutRight, "NavBag", "BAG", GoldDim, false, 3, SceneName.Lobby);
            AddNavItem(navLayoutRight, "NavMail", "MAIL", Sky, false, 5, SceneName.Lobby);
            AddNavItem(navLayoutRight, "NavAlliance", "ALLIANCE", TealDim, false, 0, SceneName.Alliance);

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
        // WORLD MAP SCENE — Territory control (P&C quality)
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/World Map")]
        public static void SetupWorldMapScene()
        {
            var scene = OpenScene("WorldMap");
            var canvasGo = FindOrCreateCanvas(scene);
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");

            // Dark map background with atmospheric depth
            var bg = AddPanel(canvasGo, "MapBackground", new Color(0.04f, 0.06f, 0.03f, 1f));
            StretchToParent(bg);
            // Fog of war edges
            var fogTop = AddPanel(bg, "FogTop", new Color(0.02f, 0.03f, 0.01f, 0.5f));
            SetAnchors(fogTop, 0f, 0.85f, 1f, 1f);
            var fogBot = AddPanel(bg, "FogBot", new Color(0.02f, 0.02f, 0.01f, 0.4f));
            SetAnchors(fogBot, 0f, 0f, 1f, 0.15f);

            // Map grid overlay — territory tiles with labels and terrain types
            string[,] tileNames = {
                { "Ashfall", "Iron Wastes", "Duskfen", "Stonewatch", "Thornvale", "Grimreach" },
                { "Emberveil", "", "Blackhollow", "", "Wraithwood", "" },
                { "Goldvein", "Mistpeak", "", "Frostmere", "Shadowfen", "Deepforge" },
                { "", "Dragonspire", "Voidrift", "", "Skybreak", "" },
                { "Sunhaven", "", "Moonfall", "Irondeep", "", "Ashenmoor" }
            };
            int[,] tileTypes = { // 0=neutral, 1=allied, 2=enemy, 3=contested
                { 1, 1, 0, 2, 0, 2 },
                { 1, 0, 3, 0, 2, 0 },
                { 0, 1, 0, 0, 3, 2 },
                { 0, 1, 2, 0, 0, 0 },
                { 1, 0, 0, 2, 0, 2 }
            };
            Color[] tileTypeColors = {
                new Color(0.12f, 0.12f, 0.10f, 0.35f), // neutral
                new Color(0.12f, 0.22f, 0.10f, 0.45f), // allied (green)
                new Color(0.25f, 0.08f, 0.08f, 0.45f), // enemy (red)
                new Color(0.25f, 0.20f, 0.05f, 0.50f)  // contested (amber)
            };
            Color[] tileBorderColors = {
                new Color(0.25f, 0.23f, 0.18f, 0.30f), // neutral
                new Color(0.20f, 0.40f, 0.15f, 0.50f), // allied
                new Color(0.50f, 0.15f, 0.12f, 0.50f), // enemy
                new Color(0.55f, 0.45f, 0.12f, 0.55f)  // contested
            };

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    float x = 0.06f + c * 0.148f;
                    float y = 0.22f + r * 0.135f;
                    int tt = tileTypes[r, c];
                    var tile = AddPanel(canvasGo, $"Tile_{r}_{c}", tileTypeColors[tt]);
                    SetAnchors(tile, x, y, x + 0.13f, y + 0.12f);
                    AddOutlinePanel(tile, tileBorderColors[tt]);

                    // Territory name label
                    string name = tileNames[r, c];
                    if (!string.IsNullOrEmpty(name))
                    {
                        var nameLabel = AddText(tile, "Name", name, 7, TextAnchor.MiddleCenter);
                        SetAnchors(nameLabel, 0.02f, 0.55f, 0.98f, 0.90f);
                        nameLabel.GetComponent<Text>().color = tt == 1 ? new Color(0.55f, 0.82f, 0.45f, 0.9f) :
                            tt == 2 ? new Color(0.85f, 0.45f, 0.40f, 0.9f) :
                            tt == 3 ? new Color(0.90f, 0.78f, 0.30f, 0.9f) :
                            new Color(0.65f, 0.62f, 0.55f, 0.75f);
                        nameLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
                        var nSh = nameLabel.AddComponent<Shadow>();
                        nSh.effectColor = new Color(0, 0, 0, 0.9f);
                        nSh.effectDistance = new Vector2(0.5f, -0.5f);
                    }

                    // Resource/power icon for named territories
                    if (!string.IsNullOrEmpty(name))
                    {
                        string resIcon = tt == 1 ? "\u2694" : tt == 2 ? "\u2620" : tt == 3 ? "\u26A0" : "\u25C6";
                        var icon = AddText(tile, "Icon", resIcon, 9, TextAnchor.MiddleCenter);
                        SetAnchors(icon, 0.35f, 0.10f, 0.65f, 0.50f);
                        icon.GetComponent<Text>().color = tileBorderColors[tt];
                    }
                }
            }

            // Notch fill
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.04f, 0.02f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);

            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR — gold bordered ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.03f, 0.04f, 0.02f, 0.96f));
            SetAnchors(topBar, 0f, 0.93f, 1f, 0.995f);
            var topBorderGold = AddPanel(topBar, "BorderGold", new Color(0.85f, 0.68f, 0.28f, 0.90f));
            SetAnchors(topBorderGold, 0f, 0f, 1f, 0.035f);
            var topBorderDark = AddPanel(topBar, "BorderDark", new Color(0.35f, 0.25f, 0.10f, 0.65f));
            SetAnchors(topBorderDark, 0f, 0.035f, 1f, 0.06f);

            var mapTitle = AddText(topBar, "MapTitle", "WORLD MAP \u2014 Ashlands", 16, TextAnchor.MiddleCenter);
            SetAnchors(mapTitle, 0.2f, 0.08f, 0.8f, 0.92f);
            mapTitle.GetComponent<Text>().color = Gold;
            mapTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var mtShadow = mapTitle.AddComponent<Shadow>();
            mtShadow.effectColor = new Color(0, 0, 0, 0.9f);
            mtShadow.effectDistance = new Vector2(1f, -1f);

            var backBtn = AddPanel(topBar, "BackBtn", new Color(0.25f, 0.20f, 0.30f));
            SetAnchors(backBtn, 0.01f, 0.12f, 0.12f, 0.88f);
            if (btnOrnateSpr != null) { backBtn.GetComponent<Image>().sprite = btnOrnateSpr; backBtn.GetComponent<Image>().type = Image.Type.Sliced; backBtn.GetComponent<Image>().color = new Color(0.45f, 0.38f, 0.30f, 1f); }
            backBtn.AddComponent<Button>();
            var bbLabel = AddText(backBtn, "Label", "\u25C0 BACK", 11, TextAnchor.MiddleCenter);
            StretchToParent(bbLabel);
            bbLabel.GetComponent<Text>().color = TextLight;
            bbLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === COORDINATES DISPLAY ===
            var coordsPanel = AddPanel(canvas, "CoordsPanel", new Color(0.04f, 0.03f, 0.06f, 0.85f));
            SetAnchors(coordsPanel, 0.30f, 0.93f, 0.70f, 0.995f);
            if (ornateSpr != null) { coordsPanel.GetComponent<Image>().sprite = ornateSpr; coordsPanel.GetComponent<Image>().type = Image.Type.Sliced; coordsPanel.GetComponent<Image>().color = new Color(0.45f, 0.42f, 0.38f, 1f); }
            var coordText = AddText(coordsPanel, "Coords", "K:12  (482, 317)", 10, TextAnchor.MiddleCenter);
            StretchToParent(coordText);
            coordText.GetComponent<Text>().color = TextLight;
            coordText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === ZOOM CONTROLS ===
            var zoomIn = AddPanel(canvas, "ZoomIn", new Color(0.30f, 0.25f, 0.20f, 0.9f));
            SetAnchors(zoomIn, 0.91f, 0.58f, 0.98f, 0.65f);
            if (btnOrnateSpr != null) { zoomIn.GetComponent<Image>().sprite = btnOrnateSpr; zoomIn.GetComponent<Image>().type = Image.Type.Sliced; zoomIn.GetComponent<Image>().color = new Color(0.50f, 0.45f, 0.38f, 1f); }
            zoomIn.AddComponent<Button>();
            var ziLabel = AddText(zoomIn, "Label", "+", 16, TextAnchor.MiddleCenter);
            StretchToParent(ziLabel);
            ziLabel.GetComponent<Text>().color = Gold;
            ziLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var zoomOut = AddPanel(canvas, "ZoomOut", new Color(0.30f, 0.25f, 0.20f, 0.9f));
            SetAnchors(zoomOut, 0.91f, 0.50f, 0.98f, 0.57f);
            if (btnOrnateSpr != null) { zoomOut.GetComponent<Image>().sprite = btnOrnateSpr; zoomOut.GetComponent<Image>().type = Image.Type.Sliced; zoomOut.GetComponent<Image>().color = new Color(0.50f, 0.45f, 0.38f, 1f); }
            zoomOut.AddComponent<Button>();
            var zoLabel = AddText(zoomOut, "Label", "\u2212", 16, TextAnchor.MiddleCenter);
            StretchToParent(zoLabel);
            zoLabel.GetComponent<Text>().color = Gold;
            zoLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === SEARCH COORDS BUTTON ===
            var searchBtn = AddPanel(canvas, "SearchBtn", new Color(0.25f, 0.22f, 0.30f, 0.9f));
            SetAnchors(searchBtn, 0.88f, 0.93f, 0.99f, 0.995f);
            if (btnOrnateSpr != null) { searchBtn.GetComponent<Image>().sprite = btnOrnateSpr; searchBtn.GetComponent<Image>().type = Image.Type.Sliced; searchBtn.GetComponent<Image>().color = new Color(0.45f, 0.40f, 0.50f, 1f); }
            searchBtn.AddComponent<Button>();
            var srchLabel = AddText(searchBtn, "Label", "\uD83D\uDD0D", 12, TextAnchor.MiddleCenter);
            StretchToParent(srchLabel);
            srchLabel.GetComponent<Text>().color = TextLight;

            // === LEGEND — ornate frame with 4 types ===
            var legend = AddPanel(canvas, "Legend", new Color(0.04f, 0.03f, 0.06f, 0.92f));
            SetAnchors(legend, 0.78f, 0.78f, 0.99f, 0.925f);
            if (ornateSpr != null) { legend.GetComponent<Image>().sprite = ornateSpr; legend.GetComponent<Image>().type = Image.Type.Sliced; legend.GetComponent<Image>().color = new Color(0.55f, 0.52f, 0.45f, 1f); }
            else { AddOutlinePanel(legend, GoldDim); }
            var legTitle = AddText(legend, "LTitle", "LEGEND", 8, TextAnchor.UpperCenter);
            SetAnchors(legTitle, 0f, 0.82f, 1f, 1f);
            legTitle.GetComponent<Text>().color = Gold;
            legTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddLegendItem(legend, "Allied", new Color(0.20f, 0.40f, 0.15f), 0.60f);
            AddLegendItem(legend, "Enemy", new Color(0.50f, 0.15f, 0.12f), 0.40f);
            AddLegendItem(legend, "Contested", new Color(0.55f, 0.45f, 0.12f), 0.20f);
            AddLegendItem(legend, "Neutral", new Color(0.25f, 0.23f, 0.18f), 0.0f);

            // === TERRITORY INFO — ornate sidebar ===
            var infoPanel = AddPanel(canvas, "TerritoryInfo", new Color(0.04f, 0.03f, 0.08f, 0.92f));
            SetAnchors(infoPanel, 0.01f, 0.10f, 0.30f, 0.48f);
            if (ornateSpr != null) { infoPanel.GetComponent<Image>().sprite = ornateSpr; infoPanel.GetComponent<Image>().type = Image.Type.Sliced; infoPanel.GetComponent<Image>().color = new Color(0.62f, 0.58f, 0.50f, 1f); }
            else { AddOutlinePanel(infoPanel, GoldDim); }

            var tiTitle = AddText(infoPanel, "TerritoryName", "Iron Wastes", 16, TextAnchor.MiddleLeft);
            SetAnchors(tiTitle, 0.06f, 0.84f, 0.95f, 0.98f);
            tiTitle.GetComponent<Text>().color = Gold;
            tiTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var titShadow = tiTitle.AddComponent<Shadow>();
            titShadow.effectColor = new Color(0, 0, 0, 0.9f);
            titShadow.effectDistance = new Vector2(1f, -1f);

            var tiSep = AddPanel(infoPanel, "Separator", new Color(0.72f, 0.56f, 0.22f, 0.50f));
            SetAnchors(tiSep, 0.06f, 0.82f, 0.94f, 0.83f);

            var tiOwner = AddText(infoPanel, "Owner", "Controlled by: Iron Legion", 11, TextAnchor.MiddleLeft);
            SetAnchors(tiOwner, 0.06f, 0.67f, 0.95f, 0.80f);
            tiOwner.GetComponent<Text>().color = TextLight;

            var tiBonus = AddText(infoPanel, "Bonus", "Bonus: +15% Iron production", 10, TextAnchor.MiddleLeft);
            SetAnchors(tiBonus, 0.06f, 0.54f, 0.95f, 0.65f);
            tiBonus.GetComponent<Text>().color = Teal;

            var tiGarrison = AddText(infoPanel, "Garrison", "Garrison: 12,500 Power", 10, TextAnchor.MiddleLeft);
            SetAnchors(tiGarrison, 0.06f, 0.42f, 0.95f, 0.53f);
            tiGarrison.GetComponent<Text>().color = TextMid;

            // Ornate action buttons
            var tiAttackBtn = AddPanel(infoPanel, "AttackBtn", Blood);
            SetAnchors(tiAttackBtn, 0.06f, 0.06f, 0.48f, 0.30f);
            if (btnOrnateSpr != null) { tiAttackBtn.GetComponent<Image>().sprite = btnOrnateSpr; tiAttackBtn.GetComponent<Image>().type = Image.Type.Sliced; tiAttackBtn.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.15f, 1f); }
            tiAttackBtn.AddComponent<Button>();
            var atkLabel = AddText(tiAttackBtn, "Label", "ATTACK", 11, TextAnchor.MiddleCenter);
            StretchToParent(atkLabel);
            atkLabel.GetComponent<Text>().color = Color.white;
            atkLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var tiScoutBtn = AddPanel(infoPanel, "ScoutBtn", Sky);
            SetAnchors(tiScoutBtn, 0.52f, 0.06f, 0.94f, 0.30f);
            if (btnOrnateSpr != null) { tiScoutBtn.GetComponent<Image>().sprite = btnOrnateSpr; tiScoutBtn.GetComponent<Image>().type = Image.Type.Sliced; tiScoutBtn.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.72f, 1f); }
            tiScoutBtn.AddComponent<Button>();
            var scoutLabel = AddText(tiScoutBtn, "Label", "SCOUT", 11, TextAnchor.MiddleCenter);
            StretchToParent(scoutLabel);
            scoutLabel.GetComponent<Text>().color = Color.white;
            scoutLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === MINI-MAP — ornate with full detail ===
            var miniMap = AddPanel(canvas, "MiniMap", new Color(0.04f, 0.04f, 0.03f, 0.92f));
            SetAnchors(miniMap, 0.78f, 0.06f, 0.99f, 0.32f);
            if (ornateSpr != null) { miniMap.GetComponent<Image>().sprite = ornateSpr; miniMap.GetComponent<Image>().type = Image.Type.Sliced; miniMap.GetComponent<Image>().color = new Color(0.52f, 0.50f, 0.44f, 1f); }
            else { AddOutlinePanel(miniMap, GoldDim); }
            var mmLabel = AddText(miniMap, "Label", "MINI MAP", 8, TextAnchor.UpperCenter);
            SetAnchors(mmLabel, 0f, 0.88f, 1f, 1f);
            mmLabel.GetComponent<Text>().color = Gold;
            mmLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Terrain regions on minimap
            var mmAllied = AddPanel(miniMap, "AlliedZone", new Color(0.15f, 0.30f, 0.12f, 0.45f));
            SetAnchors(mmAllied, 0.08f, 0.25f, 0.45f, 0.65f);
            var mmEnemy = AddPanel(miniMap, "EnemyZone", new Color(0.30f, 0.10f, 0.10f, 0.45f));
            SetAnchors(mmEnemy, 0.55f, 0.40f, 0.92f, 0.80f);
            var mmContested = AddPanel(miniMap, "ContestedZone", new Color(0.30f, 0.25f, 0.08f, 0.40f));
            SetAnchors(mmContested, 0.35f, 0.45f, 0.60f, 0.65f);

            // View rectangle (gold outline showing current viewport)
            var viewRect = AddPanel(miniMap, "ViewRect", new Color(0.85f, 0.68f, 0.28f, 0.08f));
            SetAnchors(viewRect, 0.25f, 0.30f, 0.55f, 0.55f);
            AddOutlinePanel(viewRect, new Color(0.85f, 0.68f, 0.28f, 0.80f));

            // Player marker (gold diamond)
            var playerDot = AddPanel(miniMap, "PlayerDot", Gold);
            SetAnchors(playerDot, 0.36f, 0.38f, 0.44f, 0.48f);
            AddOutlinePanel(playerDot, new Color(1f, 0.85f, 0.45f, 0.6f));

            // Alliance markers (green dots)
            float[] allyX = { 0.18f, 0.30f, 0.22f };
            float[] allyY = { 0.35f, 0.52f, 0.58f };
            for (int i = 0; i < 3; i++)
            {
                var ally = AddPanel(miniMap, $"AllyDot_{i}", new Color(0.30f, 0.65f, 0.25f, 0.75f));
                SetAnchors(ally, allyX[i], allyY[i], allyX[i] + 0.05f, allyY[i] + 0.06f);
            }

            // Enemy markers (red dots)
            float[] enX = { 0.65f, 0.78f, 0.70f, 0.82f };
            float[] enY = { 0.55f, 0.48f, 0.68f, 0.62f };
            for (int i = 0; i < 4; i++)
            {
                var en = AddPanel(miniMap, $"EnemyDot_{i}", new Color(0.70f, 0.20f, 0.15f, 0.70f));
                SetAnchors(en, enX[i], enY[i], enX[i] + 0.04f, enY[i] + 0.05f);
            }

            // "Your City" label on minimap
            var mmCityLabel = AddText(miniMap, "CityLabel", "Your City", 6, TextAnchor.MiddleCenter);
            SetAnchors(mmCityLabel, 0.22f, 0.25f, 0.58f, 0.35f);
            mmCityLabel.GetComponent<Text>().color = new Color(0.85f, 0.72f, 0.35f, 0.7f);

            SaveScene();
            Debug.Log("[SceneUIGenerator] WorldMap scene: premium territory control HUD");
        }

        // ===================================================================
        // ALLIANCE SCENE — Social hub (P&C quality)
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Alliance")]
        public static void SetupAllianceScene()
        {
            var scene = OpenScene("Alliance");
            var canvasGo = FindOrCreateCanvas(scene);
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            var shieldSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_alliance.png");

            // Background — deep dark with subtle depth
            var bg = AddPanel(canvasGo, "Background", new Color(0.03f, 0.02f, 0.06f, 1f));
            StretchToParent(bg);
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.06f, 0.04f, 0.14f, 0.30f));
            SetAnchors(skyGrad, 0f, 0.70f, 1f, 1f);
            var groundGrad = AddPanel(bg, "GroundGrad", new Color(0.02f, 0.01f, 0.04f, 0.45f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.15f);
            // Left vignette
            var vigL = AddPanel(bg, "VignetteL", new Color(0f, 0f, 0.02f, 0.25f));
            SetAnchors(vigL, 0f, 0f, 0.06f, 1f);
            // Right vignette
            var vigR = AddPanel(bg, "VignetteR", new Color(0f, 0f, 0.02f, 0.25f));
            SetAnchors(vigR, 0.94f, 0f, 1f, 1f);

            // Notch fill
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);

            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR — ornate alliance header ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.04f, 0.03f, 0.08f, 0.97f));
            SetAnchors(topBar, 0f, 0.925f, 1f, 0.995f);
            // Gold double border
            var tbBorderBot = AddPanel(topBar, "BorderBot", new Color(0.82f, 0.65f, 0.25f, 0.85f));
            SetAnchors(tbBorderBot, 0f, 0f, 1f, 0.035f);
            tbBorderBot.AddComponent<LayoutElement>().ignoreLayout = true;
            var tbBorderGlow = AddPanel(topBar, "BorderGlow", new Color(0.60f, 0.45f, 0.15f, 0.15f));
            SetAnchors(tbBorderGlow, 0f, 0.035f, 1f, 0.10f);
            tbBorderGlow.AddComponent<LayoutElement>().ignoreLayout = true;

            // Back button — ornate
            var backBtn = AddPanel(topBar, "BackBtn", new Color(0.30f, 0.22f, 0.15f, 1f));
            SetAnchors(backBtn, 0.01f, 0.10f, 0.14f, 0.90f);
            if (btnOrnateSpr != null) { backBtn.GetComponent<Image>().sprite = btnOrnateSpr; backBtn.GetComponent<Image>().type = Image.Type.Sliced; }
            backBtn.AddComponent<Button>();
            var bkLbl = AddText(backBtn, "Lbl", "\u25C0 BACK", 11, TextAnchor.MiddleCenter);
            StretchToParent(bkLbl);
            bkLbl.GetComponent<Text>().color = TextLight;
            bkLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Shield emblem — larger, with gold circle bg
            var shieldBg = AddPanel(topBar, "ShieldBg", new Color(0.55f, 0.42f, 0.18f, 0.30f));
            SetAnchors(shieldBg, 0.155f, 0.05f, 0.23f, 0.95f);
            if (shieldSpr != null) { var shieldImg = shieldBg.GetComponent<Image>(); shieldImg.sprite = shieldSpr; shieldImg.preserveAspect = true; shieldImg.color = new Color(1f, 0.90f, 0.65f, 0.90f); }
            // Gold ring around shield
            AddOutlinePanel(shieldBg, new Color(0.82f, 0.65f, 0.25f, 0.50f));

            // Alliance name — big, gold, bold
            var aName = AddText(topBar, "AllianceName", "IRON LEGION", 17, TextAnchor.MiddleLeft);
            SetAnchors(aName, 0.24f, 0.15f, 0.72f, 0.90f);
            aName.GetComponent<Text>().color = Gold;
            aName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var anSh = aName.AddComponent<Shadow>();
            anSh.effectColor = new Color(0, 0, 0, 0.9f);
            anSh.effectDistance = new Vector2(1f, -1f);

            // Power + Member count — right side
            var pwrLabel = AddText(topBar, "Power", "\u2694 1.2M", 11, TextAnchor.MiddleRight);
            SetAnchors(pwrLabel, 0.72f, 0.50f, 0.98f, 0.95f);
            pwrLabel.GetComponent<Text>().color = Ember;
            pwrLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var memLabel = AddText(topBar, "Members", "42/50 Members", 9, TextAnchor.MiddleRight);
            SetAnchors(memLabel, 0.72f, 0.05f, 0.98f, 0.50f);
            memLabel.GetComponent<Text>().color = TextMid;

            // === TAB BAR — ornate segmented tabs ===
            var tabBar = AddPanel(canvas, "TabBar", new Color(0.035f, 0.025f, 0.07f, 0.96f));
            SetAnchors(tabBar, 0f, 0.865f, 1f, 0.925f);
            // Gold bottom border
            var tabBotBorder = AddPanel(tabBar, "BotBorder", new Color(0.65f, 0.50f, 0.20f, 0.50f));
            SetAnchors(tabBotBorder, 0f, 0f, 1f, 0.04f);
            tabBotBorder.AddComponent<LayoutElement>().ignoreLayout = true;

            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 3;
            tabLayout.padding = new RectOffset(4, 4, 3, 5);
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            // Tabs with ornate active state
            string[] tabNames = { "CHAT", "MEMBERS", "WAR", "TERRITORY", "RANKS" };
            Color[] tabColors = { Teal, Purple, Blood, Ember, Gold };
            for (int t = 0; t < tabNames.Length; t++)
            {
                bool active = t == 0;
                Color c = tabColors[t];
                var tab = AddPanel(tabBar, $"Tab_{tabNames[t]}", active ? new Color(c.r * 0.20f, c.g * 0.20f, c.b * 0.20f, 0.85f) : new Color(0.06f, 0.04f, 0.10f, 0.55f));
                tab.AddComponent<LayoutElement>().flexibleWidth = 1;
                if (btnOrnateSpr != null && active) { tab.GetComponent<Image>().sprite = btnOrnateSpr; tab.GetComponent<Image>().type = Image.Type.Sliced; tab.GetComponent<Image>().color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.90f); }
                tab.AddComponent<Button>();
                // Active indicator — thick gold bar at bottom
                if (active) { var ind = AddPanel(tab, "ActiveBar", c); SetAnchors(ind, 0.08f, 0f, 0.92f, 0.10f); ind.AddComponent<LayoutElement>().ignoreLayout = true; }
                // Inactive subtle border
                if (!active) { AddOutlinePanel(tab, new Color(0.25f, 0.20f, 0.15f, 0.20f)); }
                var tLbl = AddText(tab, "Label", tabNames[t], 10, TextAnchor.MiddleCenter);
                StretchToParent(tLbl);
                tLbl.GetComponent<Text>().color = active ? c : TextDim;
                tLbl.GetComponent<Text>().fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
                if (active) { var tSh = tLbl.AddComponent<Shadow>(); tSh.effectColor = new Color(0, 0, 0, 0.85f); tSh.effectDistance = new Vector2(1, -1); }
            }

            // === CHAT AREA — ornate framed panel, clearly distinct from background ===
            var chatPanel = AddPanel(canvas, "ChatPanel", new Color(0.08f, 0.06f, 0.14f, 1f));
            SetAnchors(chatPanel, 0.015f, 0.13f, 0.985f, 0.86f);
            if (ornateSpr != null) { chatPanel.GetComponent<Image>().sprite = ornateSpr; chatPanel.GetComponent<Image>().type = Image.Type.Sliced; chatPanel.GetComponent<Image>().color = new Color(0.42f, 0.38f, 0.35f, 1f); }
            else { AddOutlinePanel(chatPanel, new Color(0.55f, 0.42f, 0.18f, 0.50f)); }

            var chatMsgs = new (string sender, string msg, Color sColor, string time)[] {
                ("Kaelen", "Rally at sector 7! Enemy alliance incoming.", Ember, "2m"),
                ("Vorra", "I'll bring my siege squad. ETA 5 minutes.", Sky, "2m"),
                ("Commander", "Everyone focus fire on their Stronghold.", Gold, "1m"),
                ("Seraphyn", "Healing squad standing by. Let's go!", Teal, "1m"),
                ("Mordoc", "Their wall defenses are weak on east side.", Blood, "45s"),
                ("Lyra", "GG everyone! That was a great war.", Purple, "30s"),
                ("Kaelen", "Next target: sector 12. Regroup in 10.", Ember, "15s"),
            };

            int msgCount = chatMsgs.Length;
            float msgH = 1f / (msgCount + 0.5f); // evenly fill the space
            for (int i = 0; i < msgCount; i++)
            {
                float yTop = 1f - i * msgH - 0.02f;
                float yBot = yTop - msgH + 0.01f;

                var row = AddPanel(chatPanel, $"Msg_{i}", new Color(0.10f, 0.08f, 0.18f, i % 2 == 0 ? 0.90f : 0.70f));
                SetAnchors(row, 0.015f, yBot, 0.985f, yTop);
                AddOutlinePanel(row, new Color(0.25f, 0.20f, 0.30f, i % 2 == 0 ? 0.30f : 0.15f));
                // Left accent bar in sender color — thicker
                var accent = AddPanel(row, "Accent", new Color(chatMsgs[i].sColor.r, chatMsgs[i].sColor.g, chatMsgs[i].sColor.b, 0.65f));
                SetAnchors(accent, 0f, 0.05f, 0.012f, 0.95f);

                // Avatar circle with initial — brighter
                var avatar = AddPanel(row, "Avatar", new Color(chatMsgs[i].sColor.r * 0.45f, chatMsgs[i].sColor.g * 0.45f, chatMsgs[i].sColor.b * 0.45f, 0.85f));
                SetAnchors(avatar, 0.018f, 0.10f, 0.065f, 0.90f);
                AddOutlinePanel(avatar, new Color(chatMsgs[i].sColor.r * 0.65f, chatMsgs[i].sColor.g * 0.65f, chatMsgs[i].sColor.b * 0.65f, 0.45f));
                var initial = AddText(avatar, "Init", chatMsgs[i].sender.Substring(0, 1), 13, TextAnchor.MiddleCenter);
                StretchToParent(initial);
                initial.GetComponent<Text>().color = new Color(1f, 1f, 1f, 0.90f);
                initial.GetComponent<Text>().fontStyle = FontStyle.Bold;

                // Sender — colored bold
                var sender = AddText(row, "Sender", chatMsgs[i].sender, 11, TextAnchor.MiddleLeft);
                SetAnchors(sender, 0.075f, 0.52f, 0.40f, 0.98f);
                sender.GetComponent<Text>().color = chatMsgs[i].sColor;
                sender.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var sSh = sender.AddComponent<Shadow>();
                sSh.effectColor = new Color(0, 0, 0, 0.85f);
                sSh.effectDistance = new Vector2(1, -1);

                // Timestamp
                var ts = AddText(row, "Time", chatMsgs[i].time, 9, TextAnchor.MiddleRight);
                SetAnchors(ts, 0.88f, 0.55f, 0.99f, 0.98f);
                ts.GetComponent<Text>().color = TextDim;

                // Message text
                var msgTxt = AddText(row, "Text", chatMsgs[i].msg, 11, TextAnchor.MiddleLeft);
                SetAnchors(msgTxt, 0.075f, 0.02f, 0.98f, 0.55f);
                msgTxt.GetComponent<Text>().color = TextLight;
            }

            // === INPUT BAR — ornate with gold trim ===
            var inputBar = AddPanel(canvas, "InputBar", new Color(0.05f, 0.04f, 0.09f, 0.97f));
            SetAnchors(inputBar, 0f, 0.08f, 1f, 0.13f);
            // Gold top trim — brighter
            var inTopBorder = AddPanel(inputBar, "TopBorder", new Color(0.68f, 0.52f, 0.20f, 0.55f));
            SetAnchors(inTopBorder, 0f, 0.95f, 1f, 1f);
            inTopBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            // Gold bottom trim
            var inBotBorder = AddPanel(inputBar, "BotBorder", new Color(0.55f, 0.42f, 0.16f, 0.30f));
            SetAnchors(inBotBorder, 0f, 0f, 1f, 0.05f);
            inBotBorder.AddComponent<LayoutElement>().ignoreLayout = true;

            // Text field — inset dark
            var inputField = AddPanel(inputBar, "InputField", new Color(0.05f, 0.035f, 0.08f, 0.92f));
            SetAnchors(inputField, 0.02f, 0.10f, 0.78f, 0.90f);
            AddOutlinePanel(inputField, new Color(0.35f, 0.28f, 0.15f, 0.25f));
            var phTxt = AddText(inputField, "Placeholder", "  Type a message...", 11, TextAnchor.MiddleLeft);
            StretchToParent(phTxt);
            phTxt.GetComponent<Text>().color = TextDim;
            phTxt.GetComponent<Text>().fontStyle = FontStyle.Italic;

            // Send button — ornate teal
            var sendBtn = AddPanel(inputBar, "SendBtn", new Color(0.15f, 0.60f, 0.55f, 1f));
            SetAnchors(sendBtn, 0.80f, 0.08f, 0.98f, 0.92f);
            if (btnOrnateSpr != null) { sendBtn.GetComponent<Image>().sprite = btnOrnateSpr; sendBtn.GetComponent<Image>().type = Image.Type.Sliced; }
            sendBtn.AddComponent<Button>();
            var sendLbl = AddText(sendBtn, "Lbl", "SEND", 12, TextAnchor.MiddleCenter);
            StretchToParent(sendLbl);
            sendLbl.GetComponent<Text>().color = Color.white;
            sendLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var sendSh = sendLbl.AddComponent<Shadow>();
            sendSh.effectColor = new Color(0, 0, 0, 0.7f);
            sendSh.effectDistance = new Vector2(1, -1);

            // === BOTTOM ACTION BAR — ornate 4-button bar ===
            var bottomBar = AddPanel(canvas, "BottomBar", new Color(0.03f, 0.02f, 0.06f, 0.97f));
            SetAnchors(bottomBar, 0f, 0f, 1f, 0.08f);
            // Double gold border
            var bbTop = AddPanel(bottomBar, "TopBorder", new Color(0.78f, 0.60f, 0.22f, 0.75f));
            SetAnchors(bbTop, 0f, 0.96f, 1f, 1f);
            bbTop.AddComponent<LayoutElement>().ignoreLayout = true;
            var bbGlow = AddPanel(bottomBar, "TopGlow", new Color(0.55f, 0.42f, 0.15f, 0.10f));
            SetAnchors(bbGlow, 0f, 0.86f, 1f, 0.96f);
            bbGlow.AddComponent<LayoutElement>().ignoreLayout = true;

            var bbLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            bbLayout.spacing = 6;
            bbLayout.padding = new RectOffset(8, 8, 6, 6);
            bbLayout.childForceExpandWidth = true;
            bbLayout.childForceExpandHeight = true;

            AddOrnateToolbarBtn(bottomBar, "DonateBtn", "DONATE", Gold, btnOrnateSpr);
            AddOrnateToolbarBtn(bottomBar, "WarBtn", "DECLARE\nWAR", Blood, btnOrnateSpr);
            AddOrnateToolbarBtn(bottomBar, "RecruitBtn", "RECRUIT", Teal, btnOrnateSpr);
            AddOrnateToolbarBtn(bottomBar, "ShopBtn", "ALLIANCE\nSHOP", Purple, btnOrnateSpr);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Alliance scene: premium social hub v2");
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
                ? new Color(0.10f, 0.07f, 0.18f, 0.94f)
                : new Color(0.08f, 0.06f, 0.14f, 0.82f));
            SetAnchors(slot, 0f, yMin, 1f, yMax);
            // Use ornate panel for active queue slots
            var queueOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (queueOrnateSpr != null && active)
            {
                slot.GetComponent<Image>().sprite = queueOrnateSpr;
                slot.GetComponent<Image>().type = Image.Type.Sliced;
                slot.GetComponent<Image>().color = new Color(0.60f, 0.52f, 0.42f, 1f);
            }
            slot.AddComponent<Button>();

            // Border — gold for active, dim for idle
            AddOutlinePanel(slot, active ? new Color(0.68f, 0.54f, 0.22f, 0.7f) : new Color(0.30f, 0.25f, 0.15f, 0.35f));

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

            // Icon — transparent background, dedicated AI-generated sprite per event type
            var icon = AddPanel(btn, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.14f, 0.24f, 0.86f, 0.78f);
            // Each event button gets its own UNIQUE Runware-generated icon
            string spriteKey = label.ToLower() switch {
                "events" => "icon_events",   // flaming scroll calendar
                "vs"     => "icon_pvp",      // shield + crossed swords
                "rewards"=> "icon_rewards",  // treasure chest with gold
                "offer"  => "icon_offer",    // sparkling diamond gem
                "shop"   => "nav_shop",      // treasure chest (shop)
                "gifts"  => "icon_gifts",    // gift box with ribbon
                "arena"  => "icon_arena",    // colosseum arena gate
                _        => "icon_arcane"    // fallback: arcane crystal
            };
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
            if (spr != null)
            {
                icon.GetComponent<Image>().sprite = spr;
                icon.GetComponent<Image>().preserveAspect = true;
                // Tint icons to match their event color theme
                icon.GetComponent<Image>().color = new Color(
                    Mathf.Clamp01(color.r * 0.6f + 0.4f),
                    Mathf.Clamp01(color.g * 0.6f + 0.4f),
                    Mathf.Clamp01(color.b * 0.6f + 0.4f), 0.95f);
            }
            else
            {
                icon.GetComponent<Image>().color = color;
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

        /// <summary>P&C-style nav bar item — real sprite icons, circular badges, premium feel.</summary>
        static void AddNavItem(GameObject parent, string name, string label, Color color, bool active, int badgeCount, SceneName? targetScene = null)
        {
            var item = AddPanel(parent, name, new Color(0, 0, 0, 0));
            item.AddComponent<LayoutElement>().flexibleWidth = 1;
            item.AddComponent<Button>();
            if (targetScene.HasValue)
                AddSceneNav(item, targetScene.Value);

            // Active highlight bg — subtle warm glow
            if (active)
            {
                var activeBg = AddPanel(item, "ActiveBg", new Color(0.22f, 0.15f, 0.06f, 0.50f));
                SetAnchors(activeBg, 0.05f, 0.04f, 0.95f, 0.96f);
                // Top gold accent bar
                var activeBar = AddPanel(item, "ActiveBar", new Color(0.88f, 0.70f, 0.28f, 0.90f));
                SetAnchors(activeBar, 0.10f, 0.94f, 0.90f, 1f);
                // Warm glow under accent bar
                var activeGlow = AddPanel(item, "ActiveGlow", new Color(0.72f, 0.55f, 0.20f, 0.15f));
                SetAnchors(activeGlow, 0.08f, 0.82f, 0.92f, 0.94f);
            }

            // Icon — transparent bg, production sprite only (NO colored square fallback)
            var icon = AddPanel(item, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.18f, 0.30f, 0.82f, 0.88f);

            // Map to dedicated production sprites — each is unique and instantly recognizable
            string spriteKey = label.ToLower() switch {
                "world"    => "nav_empire",     // castle/fortress
                "hero"     => "nav_heroes",     // ornate helmet
                "quest"    => "icon_quest",     // glowing quest scroll with "!"
                "bag"      => "nav_shop",       // treasure chest
                "mail"     => "icon_mail",      // wax-sealed envelope
                "alliance" => "nav_alliance",   // shield with banner
                "rank"     => "icon_currency_gold",
                _          => null
            };

            Color activeTint = new Color(1f, 0.92f, 0.72f, 1f);       // bright warm gold
            Color inactiveTint = new Color(0.52f, 0.46f, 0.36f, 0.60f); // muted bronze

            if (spriteKey != null)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
                if (spr != null)
                {
                    icon.GetComponent<Image>().sprite = spr;
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().color = active ? activeTint : inactiveTint;
                }
            }

            // Circular notification badge (not rectangular!)
            if (badgeCount > 0)
            {
                var badge = AddPanel(item, "Badge", new Color(0.90f, 0.12f, 0.12f, 1f));
                SetAnchors(badge, 0.62f, 0.72f, 0.92f, 0.96f);
                // Use Kenney circular button sprite for round badge
                var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
                if (circleSpr != null)
                {
                    var badgeImg = badge.GetComponent<Image>();
                    badgeImg.sprite = circleSpr;
                    badgeImg.type = Image.Type.Sliced;
                    badgeImg.color = new Color(0.92f, 0.14f, 0.14f, 1f); // Red circular badge
                }
                // Dark outline ring for contrast
                var badgeRing = AddPanel(badge, "Ring", new Color(0.35f, 0.04f, 0.04f, 0.90f));
                SetAnchors(badgeRing, -0.08f, -0.08f, 1.08f, 1.08f);
                if (circleSpr != null) { badgeRing.GetComponent<Image>().sprite = circleSpr; badgeRing.GetComponent<Image>().type = Image.Type.Sliced; }
                badgeRing.transform.SetAsFirstSibling(); // behind badge

                var badgeText = AddText(badge, "Count", badgeCount.ToString(), 7, TextAnchor.MiddleCenter);
                StretchToParent(badgeText);
                badgeText.GetComponent<Text>().color = Color.white;
                badgeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var badgeShadow = badgeText.AddComponent<Shadow>();
                badgeShadow.effectColor = new Color(0, 0, 0, 0.6f);
                badgeShadow.effectDistance = new Vector2(0.5f, -0.5f);
            }

            // Label — warm gold active, cool gray inactive
            var lbl = AddText(item, "Label", label, 9, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0.02f, 1f, 0.28f);
            lbl.GetComponent<Text>().color = active
                ? new Color(1f, 0.93f, 0.72f, 1f)
                : new Color(0.50f, 0.50f, 0.55f, 0.72f);
            lbl.GetComponent<Text>().fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.90f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void AddHeroStatusPanel(GameObject parent, string heroName, float hpPct, Color heroColor, bool isPlayer)
        {
            var panel = AddPanel(parent, heroName, new Color(0.04f, 0.03f, 0.08f, 0.92f));
            panel.AddComponent<LayoutElement>().preferredHeight = 50;

            // Ornate panel frame
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/panel_ornate.png");
            if (ornateSpr != null)
            {
                panel.GetComponent<Image>().sprite = ornateSpr;
                panel.GetComponent<Image>().type = Image.Type.Sliced;
                Color frameColor = isPlayer ? new Color(0.35f, 0.42f, 0.58f, 1f) : new Color(0.55f, 0.30f, 0.28f, 1f);
                panel.GetComponent<Image>().color = frameColor;
            }
            else
            {
                Color borderColor = isPlayer ? new Color(0.25f, 0.35f, 0.55f, 0.7f) : new Color(0.50f, 0.15f, 0.15f, 0.7f);
                AddOutlinePanel(panel, borderColor);
            }

            // Portrait with hero initial — circular with gold border
            var portraitBg = AddPanel(panel, "PortraitBg", new Color(heroColor.r * 0.3f, heroColor.g * 0.3f, heroColor.b * 0.3f, 1f));
            SetAnchors(portraitBg, 0.03f, 0.08f, 0.28f, 0.92f);
            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (circleSpr != null) { portraitBg.GetComponent<Image>().sprite = circleSpr; portraitBg.GetComponent<Image>().type = Image.Type.Sliced; portraitBg.GetComponent<Image>().color = new Color(heroColor.r * 0.4f, heroColor.g * 0.4f, heroColor.b * 0.4f, 1f); }
            AddOutlinePanel(portraitBg, new Color(0.72f, 0.56f, 0.22f, 0.70f));
            // Hero initial letter
            var initText = AddText(portraitBg, "Initial", heroName[..1], 16, TextAnchor.MiddleCenter);
            StretchToParent(initText);
            initText.GetComponent<Text>().color = new Color(heroColor.r + 0.2f, heroColor.g + 0.2f, heroColor.b + 0.2f, 0.9f);
            initText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var initShadow = initText.AddComponent<Shadow>();
            initShadow.effectColor = new Color(0, 0, 0, 0.8f);
            initShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Hero name with team icon
            string teamIcon = isPlayer ? "\u2694 " : "\u2620 "; // swords or skull
            var nameLabel = AddText(panel, "Name", teamIcon + heroName, 9, TextAnchor.MiddleLeft);
            SetAnchors(nameLabel, 0.31f, 0.55f, 0.98f, 0.95f);
            nameLabel.GetComponent<Text>().color = TextWhite;
            nameLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nShadow = nameLabel.AddComponent<Shadow>();
            nShadow.effectColor = new Color(0, 0, 0, 0.8f);
            nShadow.effectDistance = new Vector2(1f, -1f);

            // HP percentage text — right-aligned above bar
            int hpCurrent = (int)(hpPct * 1000);
            var hpText = AddText(panel, "HpText", $"{(int)(hpPct * 100)}%", 8, TextAnchor.MiddleRight);
            SetAnchors(hpText, 0.72f, 0.55f, 0.98f, 0.95f);
            Color hpColor = hpPct > 0.5f ? BarHpGreen : hpPct > 0.25f ? Ember : BarHpRed;
            hpText.GetComponent<Text>().color = new Color(hpColor.r, hpColor.g, hpColor.b, 0.85f);
            hpText.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // HP bar with glow at fill edge
            var hpBarBg = AddPanel(panel, "HpBarBg", new Color(0.06f, 0.05f, 0.10f, 0.95f));
            SetAnchors(hpBarBg, 0.31f, 0.15f, 0.98f, 0.48f);
            AddOutlinePanel(hpBarBg, new Color(0.20f, 0.18f, 0.15f, 0.4f));
            var hpBarFill = AddPanel(hpBarBg, "Fill", hpColor);
            SetAnchors(hpBarFill, 0f, 0f, hpPct, 1f);
            // Glow at fill edge
            var hpGlow = AddPanel(hpBarBg, "Glow", new Color(hpColor.r + 0.3f, hpColor.g + 0.3f, hpColor.b + 0.15f, 0.45f));
            SetAnchors(hpGlow, Mathf.Max(0, hpPct - 0.05f), 0f, Mathf.Min(1f, hpPct + 0.03f), 1f);

            // Status effect slots (3 small circles below HP bar)
            for (int s = 0; s < 3; s++)
            {
                float sx = 0.31f + s * 0.08f;
                var slot = AddPanel(panel, $"Status_{s}", new Color(0.08f, 0.06f, 0.12f, 0.5f));
                SetAnchors(slot, sx, 0.02f, sx + 0.06f, 0.14f);
            }
        }

        static void AddCardWidget(GameObject parent, string cardName, int cost, Color color, string type, int value, string element = "Fire")
        {
            var card = AddPanel(parent, cardName.Replace(" ", ""), BgCard);
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = 105;
            le.preferredHeight = 150;

            // Use actual card frame sprite if available
            var frameSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Cards/CardFrame_{element}.png");
            if (frameSpr != null)
            {
                card.GetComponent<Image>().sprite = frameSpr;
                card.GetComponent<Image>().type = Image.Type.Sliced;
                card.GetComponent<Image>().color = new Color(0.85f, 0.82f, 0.78f, 1f);
            }
            else
            {
                AddOutlinePanel(card, color);
            }

            // Card art area — element-tinted with inner vignette
            var artArea = AddPanel(card, "Art", new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.95f));
            SetAnchors(artArea, 0.08f, 0.42f, 0.92f, 0.80f);
            // Element symbol in art area
            string elemSymbol = type == "ATK" ? "\u2694" : type == "HEAL" ? "\u2665" : "\u2726"; // swords, heart, diamond
            var elemIcon = AddText(artArea, "ElemIcon", elemSymbol, 22, TextAnchor.MiddleCenter);
            StretchToParent(elemIcon);
            elemIcon.GetComponent<Text>().color = new Color(color.r, color.g, color.b, 0.55f);

            // Cost badge — circular with glow
            var costGlow = AddPanel(card, "CostGlow", new Color(0.15f, 0.40f, 0.85f, 0.20f));
            SetAnchors(costGlow, 0f, 0.82f, 0.26f, 1f);
            var costBadge = AddPanel(card, "CostBadge", new Color(0.12f, 0.35f, 0.75f, 1f));
            SetAnchors(costBadge, 0.03f, 0.84f, 0.23f, 0.98f);
            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (circleSpr != null) { costBadge.GetComponent<Image>().sprite = circleSpr; costBadge.GetComponent<Image>().type = Image.Type.Sliced; costBadge.GetComponent<Image>().color = new Color(0.15f, 0.38f, 0.82f, 1f); }
            AddOutlinePanel(costBadge, new Color(0.55f, 0.72f, 1f, 0.5f));
            var costText = AddText(costBadge, "Cost", cost.ToString(), 13, TextAnchor.MiddleCenter);
            StretchToParent(costText);
            costText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            costText.GetComponent<Text>().color = Color.white;
            var costShadow = costText.AddComponent<Shadow>();
            costShadow.effectColor = new Color(0, 0, 0, 0.9f);
            costShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Type badge (top-right) — rounded with element color
            Color typeBgColor = type == "ATK" ? new Color(0.72f, 0.14f, 0.14f, 1f) :
                                type == "HEAL" ? new Color(0.12f, 0.62f, 0.48f, 1f) :
                                new Color(0.42f, 0.42f, 0.55f, 1f);
            var typeBadge = AddPanel(card, "TypeBadge", typeBgColor);
            SetAnchors(typeBadge, 0.65f, 0.84f, 0.97f, 0.97f);
            AddOutlinePanel(typeBadge, new Color(typeBgColor.r + 0.2f, typeBgColor.g + 0.1f, typeBgColor.b + 0.1f, 0.4f));
            var typeText = AddText(typeBadge, "Type", type, 7, TextAnchor.MiddleCenter);
            StretchToParent(typeText);
            typeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            typeText.GetComponent<Text>().color = Color.white;

            // Card name — gold for attacks, teal for heals
            var nameText = AddText(card, "Name", cardName, 9, TextAnchor.MiddleCenter);
            SetAnchors(nameText, 0.04f, 0.24f, 0.96f, 0.40f);
            nameText.GetComponent<Text>().color = color;
            nameText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = nameText.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.8f);
            nameShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Value text — with context label (DMG/HP/DEF)
            string valLabel = type == "ATK" ? "DMG" : type == "HEAL" ? "HP" : "DEF";
            string valStr = value > 0 ? $"{value} {valLabel}" : "";
            var valText = AddText(card, "Value", valStr, 11, TextAnchor.MiddleCenter);
            SetAnchors(valText, 0.04f, 0.06f, 0.96f, 0.22f);
            valText.GetComponent<Text>().color = type == "ATK" ? new Color(1f, 0.65f, 0.55f) : type == "HEAL" ? new Color(0.55f, 1f, 0.82f) : TextMid;
            valText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var valShadow = valText.AddComponent<Shadow>();
            valShadow.effectColor = new Color(0, 0, 0, 0.7f);
            valShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Element accent line — glowing
            var accent = AddPanel(card, "Accent", color);
            SetAnchors(accent, 0.08f, 0.40f, 0.92f, 0.42f);
            var accentGlow = AddPanel(card, "AccentGlow", new Color(color.r, color.g, color.b, 0.15f));
            SetAnchors(accentGlow, 0.06f, 0.38f, 0.94f, 0.44f);
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

        /// <summary>Ornate quick action button — uses btn_ornate sprite, icon + label.</summary>
        static void AddOrnateQuickAction(GameObject parent, string name, string label, Color color, string iconKey)
        {
            var btn = AddPanel(parent, name, new Color(0.05f, 0.04f, 0.10f, 0.95f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/btn_ornate.png");
            if (ornateSpr != null) { btn.GetComponent<Image>().sprite = ornateSpr; btn.GetComponent<Image>().type = Image.Type.Sliced; btn.GetComponent<Image>().color = new Color(color.r * 0.5f + 0.3f, color.g * 0.5f + 0.2f, color.b * 0.5f + 0.2f, 1f); }
            else { AddOutlinePanel(btn, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.5f)); }
            btn.AddComponent<Button>();

            // Icon
            var icon = AddPanel(btn, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.04f, 0.12f, 0.30f, 0.88f);
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{iconKey}.png");
            if (spr != null) { icon.GetComponent<Image>().sprite = spr; icon.GetComponent<Image>().preserveAspect = true; icon.GetComponent<Image>().color = new Color(Mathf.Clamp01(color.r * 0.5f + 0.5f), Mathf.Clamp01(color.g * 0.5f + 0.5f), Mathf.Clamp01(color.b * 0.5f + 0.5f), 0.90f); }

            var lbl = AddText(btn, "Label", label, 11, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0.28f, 0f, 1f, 1f);
            lbl.GetComponent<Text>().color = Color.white;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.9f);
            lblShadow.effectDistance = new Vector2(1f, -1f);
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

        /// <summary>Ornate toolbar button — uses btn_ornate sprite for premium feel.</summary>
        static void AddOrnateToolbarBtn(GameObject parent, string name, string label, Color color, Sprite ornateSpr)
        {
            var btn = AddPanel(parent, name, new Color(0.05f, 0.04f, 0.08f, 0.95f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (ornateSpr != null) { btn.GetComponent<Image>().sprite = ornateSpr; btn.GetComponent<Image>().type = Image.Type.Sliced; btn.GetComponent<Image>().color = new Color(color.r * 0.5f + 0.25f, color.g * 0.5f + 0.15f, color.b * 0.5f + 0.15f, 1f); }
            btn.AddComponent<Button>();
            var lbl = AddText(btn, "Label", label, 10, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().color = Color.white;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.9f);
            lblShadow.effectDistance = new Vector2(1f, -1f);
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

        /// <summary>Adds SceneNavigator component and sets target scene via SerializedObject.</summary>
        static void AddSceneNav(GameObject go, SceneName target)
        {
            var nav = go.AddComponent<SceneNavigator>();
            var so = new SerializedObject(nav);
            so.FindProperty("targetScene").enumValueIndex = (int)target;
            so.ApplyModifiedProperties();
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
