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
        static readonly Color BgCard       = new Color(0.12f, 0.08f, 0.16f, 1f);     // Card background

        static readonly Color Gold         = new Color(0.83f, 0.66f, 0.26f, 1f);     // #D4A843
        static readonly Color GoldDim      = new Color(0.55f, 0.43f, 0.18f, 1f);     // Muted gold
        static readonly Color Ember        = new Color(0.91f, 0.45f, 0.16f, 1f);     // #E8732A
        static readonly Color EmberDim     = new Color(0.65f, 0.32f, 0.12f, 1f);
        static readonly Color Blood        = new Color(0.75f, 0.15f, 0.20f, 1f);     // #C02633
        static readonly Color BloodDark    = new Color(0.45f, 0.08f, 0.12f, 1f);
        static readonly Color Teal         = new Color(0.18f, 0.78f, 0.65f, 1f);     // #2EC7A6
        static readonly Color TealDim      = new Color(0.12f, 0.50f, 0.42f, 1f);
        static readonly Color Purple       = new Color(0.55f, 0.22f, 0.72f, 1f);     // #8C38B8
        static readonly Color Sky          = new Color(0.30f, 0.55f, 0.90f, 1f);     // #4D8CE6
        static readonly Color SkyDim       = new Color(0.20f, 0.35f, 0.60f, 1f);

        static readonly Color TextLight    = new Color(0.91f, 0.87f, 0.78f, 1f);     // #E8DEC8
        static readonly Color TextMid      = new Color(0.65f, 0.60f, 0.52f, 1f);     // Muted
        static readonly Color TextDim      = new Color(0.50f, 0.46f, 0.42f, 1f);     // Dim (WCAG AA compliant)
        static readonly Color TextWhite    = new Color(0.95f, 0.93f, 0.90f, 1f);

        static readonly Color Border       = new Color(0.42f, 0.34f, 0.18f, 0.8f);   // Gold border

        static readonly Color BarHpGreen   = new Color(0.20f, 0.70f, 0.30f, 1f);
        static readonly Color BarHpRed     = new Color(0.75f, 0.15f, 0.15f, 1f);
        static readonly Color BarEnergy    = new Color(0.25f, 0.55f, 0.95f, 1f);
        static readonly Color BarEnergyDim = new Color(0.12f, 0.15f, 0.25f, 1f);

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
            string scenePath = $"Assets/Scenes/{sceneName}/{sceneName}.unity";
            // Only open the scene if it's not already active (avoids unnecessary domain reload)
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
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
                bg.GetComponent<Image>().color = new Color(0.70f, 0.65f, 0.72f, 1f); // visible art, UI overlays for readability
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

            // === ATMOSPHERIC GLOW — radial light behind title area ===
            var titleGlowOuter = AddPanel(canvas, "TitleGlowOuter", new Color(0.72f, 0.50f, 0.18f, 0.06f));
            SetAnchors(titleGlowOuter, 0.02f, 0.55f, 0.98f, 0.95f);
            var titleGlowMid = AddPanel(canvas, "TitleGlowMid", new Color(0.80f, 0.58f, 0.22f, 0.08f));
            SetAnchors(titleGlowMid, 0.10f, 0.60f, 0.90f, 0.92f);
            var titleGlowInner = AddPanel(canvas, "TitleGlowInner", new Color(0.90f, 0.70f, 0.30f, 0.06f));
            SetAnchors(titleGlowInner, 0.20f, 0.65f, 0.80f, 0.88f);

            // === STUDIO LOGO — small elegant text above crest ===
            var studioText = AddText(canvas, "StudioLabel", "ASHEN  THRONE  STUDIOS", 10, TextAnchor.MiddleCenter);
            SetAnchors(studioText, 0.15f, 0.95f, 0.85f, 0.99f);
            studioText.GetComponent<Text>().color = new Color(0.55f, 0.48f, 0.38f, 0.55f);
            studioText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            studioText.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
            studioText.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

            // === GOLDEN CREST / EMBLEM above title ===
            // Outer crest glow
            var crestGlow = AddPanel(canvas, "CrestGlow", new Color(0.80f, 0.60f, 0.20f, 0.10f));
            SetAnchors(crestGlow, 0.30f, 0.82f, 0.70f, 0.96f);
            // Crest body — dark shield shape
            var crestBody = AddPanel(canvas, "CrestBody", new Color(0.08f, 0.05f, 0.14f, 0.90f));
            SetAnchors(crestBody, 0.38f, 0.84f, 0.62f, 0.94f);
            AddOutlinePanel(crestBody, new Color(0.80f, 0.62f, 0.22f, 0.85f));
            // Inner crest fill — warm gradient
            var crestInner = AddPanel(crestBody, "CrestInner", new Color(0.18f, 0.10f, 0.25f, 0.80f));
            SetAnchors(crestInner, 0.08f, 0.08f, 0.92f, 0.92f);
            // Crest glass highlight
            var crestGlass = AddPanel(crestBody, "CrestGlass", new Color(0.90f, 0.75f, 0.40f, 0.08f));
            SetAnchors(crestGlass, 0.05f, 0.50f, 0.95f, 0.95f);
            // Crown/flame emblem text inside crest
            var crestIcon = AddText(crestBody, "CrestIcon", "\u2726", 28, TextAnchor.MiddleCenter);
            StretchToParent(crestIcon);
            crestIcon.GetComponent<Text>().color = new Color(0.92f, 0.75f, 0.30f, 0.90f);
            crestIcon.AddComponent<Shadow>().effectColor = new Color(0.60f, 0.40f, 0.10f, 0.50f);
            crestIcon.GetComponent<Shadow>().effectDistance = new Vector2(0, -1f);
            // Side crest wings — left
            var wingL = AddPanel(canvas, "WingL", new Color(0.72f, 0.56f, 0.22f, 0.35f));
            SetAnchors(wingL, 0.22f, 0.865f, 0.39f, 0.875f);
            // Side crest wings — right
            var wingR = AddPanel(canvas, "WingR", new Color(0.72f, 0.56f, 0.22f, 0.35f));
            SetAnchors(wingR, 0.61f, 0.865f, 0.78f, 0.875f);
            // Small dots at wing ends
            var dotL = AddPanel(canvas, "DotL", new Color(0.85f, 0.68f, 0.28f, 0.50f));
            SetAnchors(dotL, 0.20f, 0.86f, 0.23f, 0.88f);
            var dotR = AddPanel(canvas, "DotR", new Color(0.85f, 0.68f, 0.28f, 0.50f));
            SetAnchors(dotR, 0.77f, 0.86f, 0.80f, 0.88f);

            // === TITLE — large, dramatic, with heavy effects ===
            // Top ornate divider — triple line with glow
            var topDivGlow = AddPanel(canvas, "TopDivGlow", new Color(0.72f, 0.56f, 0.22f, 0.10f));
            SetAnchors(topDivGlow, 0.08f, 0.78f, 0.92f, 0.83f);
            var topDivOuter = AddPanel(canvas, "TopDivOuter", new Color(0.72f, 0.56f, 0.22f, 0.30f));
            SetAnchors(topDivOuter, 0.10f, 0.800f, 0.90f, 0.804f);
            var topDivMain = AddPanel(canvas, "TopDivMain", new Color(0.85f, 0.68f, 0.28f, 0.70f));
            SetAnchors(topDivMain, 0.12f, 0.793f, 0.88f, 0.798f);
            var topDivInner = AddPanel(canvas, "TopDivInner", new Color(0.72f, 0.56f, 0.22f, 0.30f));
            SetAnchors(topDivInner, 0.10f, 0.786f, 0.90f, 0.790f);
            // Diamond accents at divider center
            var topDiamond = AddPanel(canvas, "TopDiamond", new Color(0.90f, 0.72f, 0.30f, 0.80f));
            SetAnchors(topDiamond, 0.485f, 0.784f, 0.515f, 0.806f);
            topDiamond.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);

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
            // Second outline for extra weight
            var titleOutline2 = title.AddComponent<Outline>();
            titleOutline2.effectColor = new Color(0.20f, 0.14f, 0.04f, 0.40f);
            titleOutline2.effectDistance = new Vector2(2.5f, -2.5f);

            // Subtitle
            var sub = AddText(canvas, "Subtitle", "A  D A R K  F A N T A S Y  S T R A T E G Y  R P G", 11, TextAnchor.MiddleCenter);
            SetAnchors(sub, 0.10f, 0.60f, 0.90f, 0.65f);
            sub.GetComponent<Text>().color = new Color(0.70f, 0.64f, 0.55f, 0.90f);
            var subShadow = sub.AddComponent<Shadow>();
            subShadow.effectColor = new Color(0, 0, 0, 0.8f);
            subShadow.effectDistance = new Vector2(1f, -1f);

            // Bottom ornate divider — triple line with glow (mirrors top)
            var botDivGlow = AddPanel(canvas, "BotDivGlow", new Color(0.72f, 0.56f, 0.22f, 0.10f));
            SetAnchors(botDivGlow, 0.08f, 0.56f, 0.92f, 0.61f);
            var botDivOuter = AddPanel(canvas, "BotDivOuter", new Color(0.72f, 0.56f, 0.22f, 0.30f));
            SetAnchors(botDivOuter, 0.10f, 0.588f, 0.90f, 0.592f);
            var botDivMain = AddPanel(canvas, "BotDivMain", new Color(0.85f, 0.68f, 0.28f, 0.70f));
            SetAnchors(botDivMain, 0.12f, 0.581f, 0.88f, 0.586f);
            var botDivInner = AddPanel(canvas, "BotDivInner", new Color(0.72f, 0.56f, 0.22f, 0.30f));
            SetAnchors(botDivInner, 0.10f, 0.574f, 0.90f, 0.578f);
            // Diamond accent at bottom divider center
            var botDiamond = AddPanel(canvas, "BotDiamond", new Color(0.90f, 0.72f, 0.30f, 0.80f));
            SetAnchors(botDiamond, 0.485f, 0.572f, 0.515f, 0.594f);
            botDiamond.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);

            // === LOADING AREA — ornate frame with progress bar ===
            // Outer glow around loading frame
            var loadGlow = AddPanel(canvas, "LoadFrameGlow", new Color(0.72f, 0.56f, 0.22f, 0.06f));
            SetAnchors(loadGlow, 0.07f, 0.27f, 0.93f, 0.55f);
            var loadFrame = AddPanel(canvas, "LoadingFrame", new Color(0.05f, 0.03f, 0.09f, 0.92f));
            SetAnchors(loadFrame, 0.10f, 0.30f, 0.90f, 0.52f);
            var loadOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (loadOrnateSpr != null)
            {
                loadFrame.GetComponent<Image>().sprite = loadOrnateSpr;
                loadFrame.GetComponent<Image>().type = Image.Type.Sliced;
                loadFrame.GetComponent<Image>().color = new Color(0.62f, 0.55f, 0.45f, 0.92f);
            }
            else { AddOutlinePanel(loadFrame, Border); }
            // Glass highlight for depth
            var loadGlass = AddPanel(loadFrame, "GlassTop", new Color(0.22f, 0.20f, 0.30f, 0.20f));
            SetAnchors(loadGlass, 0.02f, 0.82f, 0.98f, 0.98f);
            // Inner shadow for depth
            var loadInnerShadow = AddPanel(loadFrame, "InnerShadow", new Color(0.02f, 0.01f, 0.04f, 0.25f));
            SetAnchors(loadInnerShadow, 0.02f, 0.02f, 0.98f, 0.18f);
            // Warm inner edge glow
            var loadWarmth = AddPanel(loadFrame, "Warmth", new Color(0.80f, 0.60f, 0.20f, 0.04f));
            SetAnchors(loadWarmth, 0.03f, 0.03f, 0.97f, 0.97f);
            // Corner diamond accents — ornate detail on loading frame
            var ldTL = AddPanel(loadFrame, "DiamondTL", new Color(0.90f, 0.72f, 0.30f, 0.65f));
            SetAnchors(ldTL, -0.01f, 0.94f, 0.03f, 1.02f);
            ldTL.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);
            var ldTR = AddPanel(loadFrame, "DiamondTR", new Color(0.90f, 0.72f, 0.30f, 0.65f));
            SetAnchors(ldTR, 0.97f, 0.94f, 1.01f, 1.02f);
            ldTR.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);
            var ldBL = AddPanel(loadFrame, "DiamondBL", new Color(0.90f, 0.72f, 0.30f, 0.65f));
            SetAnchors(ldBL, -0.01f, -0.02f, 0.03f, 0.06f);
            ldBL.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);
            var ldBR = AddPanel(loadFrame, "DiamondBR", new Color(0.90f, 0.72f, 0.30f, 0.65f));
            SetAnchors(ldBR, 0.97f, -0.02f, 1.01f, 0.06f);
            ldBR.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);

            // Loading status text — with step indicator
            var statusText = AddText(loadFrame, "StatusText", "Awakening the Ashen Realm...", 13, TextAnchor.MiddleCenter);
            SetAnchors(statusText, 0.05f, 0.60f, 0.95f, 0.82f);
            statusText.GetComponent<Text>().color = TextLight;
            var statusShadow = statusText.AddComponent<Shadow>();
            statusShadow.effectColor = new Color(0, 0, 0, 0.8f);
            statusShadow.effectDistance = new Vector2(1f, -1f);
            // Step count subtext
            var stepText = AddText(loadFrame, "StepText", "Forging connections...", 10, TextAnchor.MiddleCenter);
            SetAnchors(stepText, 0.30f, 0.46f, 0.70f, 0.60f);
            stepText.GetComponent<Text>().color = new Color(0.50f, 0.46f, 0.38f, 0.65f);
            stepText.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
            stepText.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

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

            // === TIP TEXT — ornate framed with gold border ===
            var tipFrame = AddPanel(canvas, "TipFrame", new Color(0.04f, 0.02f, 0.08f, 0.60f));
            SetAnchors(tipFrame, 0.10f, 0.16f, 0.90f, 0.27f);
            AddOutlinePanel(tipFrame, new Color(0.55f, 0.42f, 0.18f, 0.35f));
            // Tip glass highlight
            var tipGlass = AddPanel(tipFrame, "TipGlass", new Color(0.20f, 0.18f, 0.28f, 0.12f));
            SetAnchors(tipGlass, 0.02f, 0.70f, 0.98f, 0.98f);
            // Tip label
            var tipLabel = AddText(tipFrame, "TipLabel", "TIP", 10, TextAnchor.MiddleCenter);
            SetAnchors(tipLabel, 0.40f, 0.72f, 0.60f, 0.98f);
            tipLabel.GetComponent<Text>().color = new Color(0.85f, 0.68f, 0.28f, 0.70f);
            tipLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            tipLabel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            tipLabel.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);
            // Tip body text
            var tip = AddText(tipFrame, "TipText", "Upgrade your Stronghold to unlock new building types\nand expand your empire's power.", 11, TextAnchor.MiddleCenter);
            SetAnchors(tip, 0.06f, 0.04f, 0.94f, 0.72f);
            tip.GetComponent<Text>().color = new Color(0.55f, 0.50f, 0.43f, 0.85f);
            tip.GetComponent<Text>().fontStyle = FontStyle.Italic;
            var tipShadow = tip.AddComponent<Shadow>();
            tipShadow.effectColor = new Color(0, 0, 0, 0.6f);
            tipShadow.effectDistance = new Vector2(1f, -1f);

            // === TAP TO CONTINUE prompt ===
            var tapGlow = AddPanel(canvas, "TapGlow", new Color(0.80f, 0.65f, 0.25f, 0.04f));
            SetAnchors(tapGlow, 0.20f, 0.07f, 0.80f, 0.14f);
            var tapText = AddText(canvas, "TapToContinue", "TAP  TO  CONTINUE", 13, TextAnchor.MiddleCenter);
            SetAnchors(tapText, 0.20f, 0.08f, 0.80f, 0.13f);
            tapText.GetComponent<Text>().color = new Color(0.80f, 0.70f, 0.45f, 0.65f);
            tapText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var tapShadow = tapText.AddComponent<Shadow>();
            tapShadow.effectColor = new Color(0, 0, 0, 0.7f);
            tapShadow.effectDistance = new Vector2(1f, -1f);

            // Version + copyright
            var ver = AddText(canvas, "VersionLabel", "v0.1.0-alpha  \u2022  Ashen Throne Studios", 10, TextAnchor.LowerCenter);
            SetAnchors(ver, 0.1f, 0.03f, 0.9f, 0.06f);
            ver.GetComponent<Text>().color = new Color(0.35f, 0.32f, 0.28f, 0.50f);
            var verShadow = ver.AddComponent<Shadow>();
            verShadow.effectColor = new Color(0, 0, 0, 0.5f);
            verShadow.effectDistance = new Vector2(0.5f, -0.5f);
            // Copyright line
            var copyright = AddText(canvas, "Copyright", "\u00A9 2024 Ashen Throne Studios. All rights reserved.", 10, TextAnchor.LowerCenter);
            SetAnchors(copyright, 0.1f, 0.005f, 0.9f, 0.03f);
            copyright.GetComponent<Text>().color = new Color(0.30f, 0.28f, 0.24f, 0.38f);
            copyright.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.4f);
            copyright.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.3f);

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

            // Background — P&C-style: visible terrain with dark sky, NOT near-black
            var bg = AddPanel(canvasGo, "Background", new Color(0.12f, 0.14f, 0.10f, 1f));
            StretchToParent(bg);
            // City art background
            var bgArt = AddPanel(bg, "CityArt", Color.white);
            StretchToParent(bgArt);
            var bgSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/empire_bg.png");
            if (bgSpr != null) { bgArt.GetComponent<Image>().sprite = bgSpr; bgArt.GetComponent<Image>().preserveAspect = false; bgArt.GetComponent<Image>().color = new Color(0.7f, 0.75f, 0.65f, 1f); }
            // Very subtle darkening overlay — keep terrain visible
            var bgDarken = AddPanel(bg, "DarkenOverlay", new Color(0.04f, 0.03f, 0.08f, 0.10f));
            StretchToParent(bgDarken);
            // Sky gradient — subtle darkening at top for UI contrast
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.05f, 0.04f, 0.10f, 0.25f));
            SetAnchors(skyGrad, 0f, 0.80f, 1f, 1f);
            // Ground gradient — mild darkening at very bottom for nav bar
            var groundGrad = AddPanel(bg, "GroundGradient", new Color(0.03f, 0.02f, 0.05f, 0.30f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.12f);
            // Side vignettes — very subtle
            var vigLeft = AddPanel(bg, "VignetteL", new Color(0.02f, 0.02f, 0.04f, 0.20f));
            SetAnchors(vigLeft, 0f, 0f, 0.05f, 1f);
            var vigRight = AddPanel(bg, "VignetteR", new Color(0.02f, 0.02f, 0.04f, 0.20f));
            SetAnchors(vigRight, 0.95f, 0f, 1f, 1f);

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
            // Soft drop shadow below resource bar for depth separation
            var resBarShadow = AddPanel(canvas, "ResBarShadow", new Color(0f, 0f, 0f, 0.20f));
            SetAnchors(resBarShadow, 0f, 0.942f, 1f, 0.957f);

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
            SetButtonFeedback(plusBtn.AddComponent<Button>());
            AddSceneNav(plusBtn, SceneName.Lobby);
            var plusText = AddText(plusBtn, "Label", "+", 16, TextAnchor.MiddleCenter);
            StretchToParent(plusText);
            plusText.GetComponent<Text>().color = Color.white;
            plusText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plusSh = plusText.AddComponent<Shadow>();
            plusSh.effectColor = new Color(0, 0, 0, 0.8f);
            plusSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === AVATAR BLOCK (matches Empire — ornate frame) ===
            var avatarBlock = AddPanel(canvas, "AvatarBlock", new Color(0.08f, 0.05f, 0.14f, 0.96f));
            SetAnchors(avatarBlock, 0.01f, 0.875f, 0.14f, 0.955f);
            var avatarOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (avatarOrnateSpr != null)
            {
                avatarBlock.GetComponent<Image>().sprite = avatarOrnateSpr;
                avatarBlock.GetComponent<Image>().type = Image.Type.Sliced;
                avatarBlock.GetComponent<Image>().color = new Color(0.70f, 0.60f, 0.48f, 1f);
            }
            else
            {
                AddOutlinePanel(avatarBlock, new Color(0.82f, 0.65f, 0.28f, 0.85f));
            }
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
            var lvlText = AddText(lvlBadge, "Level", "Lv.1", 10, TextAnchor.MiddleCenter);
            StretchToParent(lvlText);
            lvlText.GetComponent<Text>().color = TextWhite;
            lvlText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lvlShadow = lvlText.AddComponent<Shadow>();
            lvlShadow.effectColor = new Color(0, 0, 0, 0.7f);
            lvlShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === INFO PANEL — right of avatar, premium dark panel with visible gold frame ===
            var infoPanelBg = AddPanel(canvas, "InfoPanelBg", new Color(0.08f, 0.05f, 0.14f, 0.98f));
            SetAnchors(infoPanelBg, 0.15f, 0.885f, 0.72f, 0.960f);
            var lobbyOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (lobbyOrnateSpr != null)
            {
                infoPanelBg.GetComponent<Image>().sprite = lobbyOrnateSpr;
                infoPanelBg.GetComponent<Image>().type = Image.Type.Sliced;
                infoPanelBg.GetComponent<Image>().color = new Color(0.72f, 0.62f, 0.50f, 1f);
            }
            // Outer gold border for extra definition
            var infoOuterBorder = AddPanel(infoPanelBg, "OuterBorder", new Color(0.78f, 0.60f, 0.25f, 0.45f));
            SetAnchors(infoOuterBorder, -0.003f, -0.04f, 1.003f, 1.04f);
            infoOuterBorder.transform.SetAsFirstSibling();
            // Inner content fill
            var infoContentFill = AddPanel(infoPanelBg, "ContentFill", new Color(0.07f, 0.04f, 0.12f, 0.98f));
            SetAnchors(infoContentFill, 0.03f, 0.05f, 0.97f, 0.95f);
            // Subtle top gradient for glass depth
            var infoTopGrad = AddPanel(infoContentFill, "TopGrad", new Color(0.18f, 0.12f, 0.25f, 0.22f));
            SetAnchors(infoTopGrad, 0f, 0.50f, 1f, 1f);
            // Thin gold midline separator between rows
            var infoMidLine = AddPanel(infoContentFill, "MidLine", new Color(0.68f, 0.52f, 0.22f, 0.28f));
            SetAnchors(infoMidLine, 0.03f, 0.48f, 0.97f, 0.52f);
            // Inner top edge glow — lit-from-above premium effect
            var infoEdgeGlow = AddPanel(infoContentFill, "EdgeGlow", new Color(0.82f, 0.68f, 0.38f, 0.12f));
            SetAnchors(infoEdgeGlow, 0.02f, 0.88f, 0.98f, 1f);
            // Inner bottom edge shadow — subtle depth
            var infoEdgeShadow = AddPanel(infoContentFill, "EdgeShadow", new Color(0f, 0f, 0f, 0.15f));
            SetAnchors(infoEdgeShadow, 0.02f, 0f, 0.98f, 0.08f);

            // === TOP ROW: Player Name + VIP Badge ===
            var avatarName = AddText(infoContentFill, "PlayerName", "Commander", 18, TextAnchor.MiddleLeft);
            SetAnchors(avatarName, 0.03f, 0.52f, 0.43f, 0.98f);
            avatarName.GetComponent<Text>().color = new Color(0.98f, 0.90f, 0.55f, 1f);
            avatarName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = avatarName.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.95f);
            nameShadow.effectDistance = new Vector2(1.2f, -1.2f);
            var nameOutline = avatarName.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0.30f, 0.18f, 0.05f, 0.40f);
            nameOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // VIP badge — multi-layer ornate pill with gradient, glow, and shimmer
            var vipGlow = AddPanel(infoContentFill, "VipGlow", new Color(0.60f, 0.25f, 0.80f, 0.12f));
            SetAnchors(vipGlow, 0.42f, 0.48f, 0.84f, 1.04f);
            var vipRing = AddPanel(infoContentFill, "VipRing", new Color(0.92f, 0.75f, 0.32f, 1f));
            SetAnchors(vipRing, 0.44f, 0.54f, 0.82f, 0.98f);
            var vipInset = AddPanel(vipRing, "VipInset", new Color(0.22f, 0.08f, 0.32f, 1f));
            SetAnchors(vipInset, 0.025f, 0.06f, 0.975f, 0.94f);
            var vipFill = AddPanel(vipInset, "VipFill", new Color(0.68f, 0.22f, 0.90f, 1f));
            SetAnchors(vipFill, 0.02f, 0.04f, 0.98f, 0.96f);
            var vipShimmer = AddPanel(vipFill, "Shimmer", new Color(0.90f, 0.70f, 1f, 0.35f));
            SetAnchors(vipShimmer, 0.06f, 0.48f, 0.94f, 0.92f);
            var vipBotGrad = AddPanel(vipFill, "BotGrad", new Color(0.35f, 0.10f, 0.50f, 0.40f));
            SetAnchors(vipBotGrad, 0.06f, 0.04f, 0.94f, 0.35f);
            var vipText = AddText(vipFill, "Label", "VIP 1", 14, TextAnchor.MiddleCenter);
            StretchToParent(vipText);
            vipText.GetComponent<Text>().color = new Color(1f, 0.98f, 0.88f, 1f);
            vipText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vipShadow = vipText.AddComponent<Shadow>();
            vipShadow.effectColor = new Color(0.12f, 0.02f, 0.20f, 0.95f);
            vipShadow.effectDistance = new Vector2(1f, -1f);
            var vipOutline = vipText.AddComponent<Outline>();
            vipOutline.effectColor = new Color(0.85f, 0.55f, 1f, 0.30f);
            vipOutline.effectDistance = new Vector2(0.3f, -0.3f);

            // Server tag
            var serverTag = AddText(infoContentFill, "ServerTag", "S:142", 12, TextAnchor.MiddleRight);
            SetAnchors(serverTag, 0.83f, 0.58f, 0.98f, 0.96f);
            serverTag.GetComponent<Text>().color = new Color(0.65f, 0.60f, 0.52f, 0.88f);
            serverTag.GetComponent<Text>().fontStyle = FontStyle.Italic;

            // === BOTTOM ROW: Power + Stronghold Level Pill ===
            var powerIconText = AddText(infoContentFill, "PowerIcon", "\u2694", 18, TextAnchor.MiddleCenter);
            SetAnchors(powerIconText, 0.02f, 0.02f, 0.10f, 0.48f);
            powerIconText.GetComponent<Text>().color = new Color(0.98f, 0.82f, 0.38f, 1f);
            var piShadow = powerIconText.AddComponent<Shadow>();
            piShadow.effectColor = new Color(0.45f, 0.30f, 0.08f, 0.8f);
            piShadow.effectDistance = new Vector2(1f, -1f);
            var piOutline = powerIconText.AddComponent<Outline>();
            piOutline.effectColor = new Color(0.70f, 0.50f, 0.15f, 0.25f);
            piOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var powerVal = AddText(infoContentFill, "PowerValue", "12,450", 20, TextAnchor.MiddleLeft);
            SetAnchors(powerVal, 0.10f, 0.02f, 0.52f, 0.48f);
            powerVal.GetComponent<Text>().color = new Color(1f, 0.98f, 0.94f, 1f);
            powerVal.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pvShadow = powerVal.AddComponent<Shadow>();
            pvShadow.effectColor = new Color(0, 0, 0, 0.95f);
            pvShadow.effectDistance = new Vector2(1.2f, -1.2f);
            var pvOutline = powerVal.AddComponent<Outline>();
            pvOutline.effectColor = new Color(0.40f, 0.28f, 0.10f, 0.40f);
            pvOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Stronghold level pill — dark pill with gold border
            var shPill = AddPanel(infoContentFill, "ShPill", new Color(0.06f, 0.04f, 0.10f, 0.80f));
            SetAnchors(shPill, 0.54f, 0.06f, 0.98f, 0.44f);
            var shPillBorder = AddPanel(shPill, "Border", new Color(0.60f, 0.48f, 0.22f, 0.45f));
            SetAnchors(shPillBorder, -0.01f, -0.04f, 1.01f, 1.04f);
            shPillBorder.transform.SetAsFirstSibling();
            var shLvl = AddText(shPill, "StrongholdLvl", "Stronghold Lv.1", 13, TextAnchor.MiddleCenter);
            StretchToParent(shLvl);
            shLvl.GetComponent<Text>().color = new Color(0.78f, 0.74f, 0.65f, 0.95f);
            var shShadow = shLvl.AddComponent<Shadow>();
            shShadow.effectColor = new Color(0, 0, 0, 0.7f);
            shShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === LOGIN STREAK BADGE — small "Day 3" indicator on avatar corner ===
            var loginBadge = AddPanel(avatarBlock, "LoginBadge", new Color(0.85f, 0.12f, 0.10f, 0.95f));
            SetAnchors(loginBadge, 0.58f, 0.78f, 1.08f, 1.08f);
            AddOutlinePanel(loginBadge, new Color(0.55f, 0.08f, 0.05f, 0.70f));
            var loginDayText = AddText(loginBadge, "DayText", "Day 3", 10, TextAnchor.MiddleCenter);
            StretchToParent(loginDayText);
            loginDayText.GetComponent<Text>().color = Color.white;
            loginDayText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var ldSh = loginDayText.AddComponent<Shadow>();
            ldSh.effectColor = new Color(0, 0, 0, 0.8f);
            ldSh.effectDistance = new Vector2(0.3f, -0.3f);

            // === CURRENCY DISPLAY (right of info panel — Gold + Gems) ===
            var currPanel = AddPanel(canvas, "CurrencyPanel", new Color(0.08f, 0.05f, 0.14f, 0.98f));
            SetAnchors(currPanel, 0.73f, 0.885f, 0.99f, 0.960f);
            if (lobbyOrnateSpr != null)
            {
                currPanel.GetComponent<Image>().sprite = lobbyOrnateSpr;
                currPanel.GetComponent<Image>().type = Image.Type.Sliced;
                currPanel.GetComponent<Image>().color = new Color(0.72f, 0.62f, 0.50f, 1f);
            }
            var currOuterBorder = AddPanel(currPanel, "OuterBorder", new Color(0.78f, 0.60f, 0.25f, 0.45f));
            SetAnchors(currOuterBorder, -0.006f, -0.04f, 1.006f, 1.04f);
            currOuterBorder.transform.SetAsFirstSibling();
            var currInnerFill = AddPanel(currPanel, "ContentFill", new Color(0.07f, 0.04f, 0.12f, 0.98f));
            SetAnchors(currInnerFill, 0.03f, 0.05f, 0.97f, 0.95f);
            var currLayout = currPanel.AddComponent<HorizontalLayoutGroup>();
            currLayout.spacing = 4; currLayout.padding = new RectOffset(6, 6, 4, 4);
            currLayout.childAlignment = TextAnchor.MiddleCenter;
            currLayout.childForceExpandWidth = false; currLayout.childForceExpandHeight = false;
            currLayout.childControlWidth = true; currLayout.childControlHeight = true;
            AddResIconFlat(currPanel, "Gold", Gold);
            var goldAmt = AddText(currPanel, "GoldAmt", "12,450", 11, TextAnchor.MiddleLeft);
            goldAmt.GetComponent<Text>().color = new Color(0.96f, 0.94f, 0.90f, 1f);
            goldAmt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var gaSh = goldAmt.AddComponent<Shadow>(); gaSh.effectColor = new Color(0, 0, 0, 0.7f); gaSh.effectDistance = new Vector2(0.5f, -0.5f);
            goldAmt.AddComponent<LayoutElement>().flexibleWidth = 1;
            var currSep = new GameObject("Sep", typeof(RectTransform), typeof(Image));
            currSep.transform.SetParent(currPanel.transform, false);
            currSep.GetComponent<Image>().color = new Color(0.40f, 0.35f, 0.25f, 0.30f);
            var csLE = currSep.AddComponent<LayoutElement>(); csLE.preferredWidth = 1; csLE.preferredHeight = 20;
            AddResIconFlat(currPanel, "Gems", GemsColor);
            var gemAmt = AddText(currPanel, "GemAmt", "385", 11, TextAnchor.MiddleLeft);
            gemAmt.GetComponent<Text>().color = new Color(0.96f, 0.94f, 0.90f, 1f);
            gemAmt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var gmSh = gemAmt.AddComponent<Shadow>(); gmSh.effectColor = new Color(0, 0, 0, 0.7f); gmSh.effectDistance = new Vector2(0.5f, -0.5f);
            gemAmt.AddComponent<LayoutElement>().flexibleWidth = 1;

            // === HERO SHOWCASE — large character silhouette behind UI ===
            var heroShowcase = AddPanel(canvas, "HeroShowcase", Color.white);
            SetAnchors(heroShowcase, 0.20f, 0.18f, 0.80f, 0.88f);
            var heroSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/kael_ashwalker_fullbody.png");
            if (heroSpr != null)
            {
                var heroImg = heroShowcase.GetComponent<Image>();
                heroImg.sprite = heroSpr;
                heroImg.preserveAspect = true;
                heroImg.color = new Color(0.75f, 0.70f, 0.72f, 0.65f); // atmospheric silhouette — more prominent
                heroImg.raycastTarget = false;
            }
            else
            {
                heroShowcase.GetComponent<Image>().color = new Color(0, 0, 0, 0); // invisible fallback
            }

            // === RIGHT SIDEBAR — stacked event notification buttons (P&C style) ===
            string[] rSideIcons = { "\u2B50", "\uD83C\uDF81", "\u2694", "\u2726" };
            string[] rSideLabels = { "REWARDS", "OFFERS", "ARENA", "RIFT" };
            string[] rSideTimers = { "10:04:49", "23:59:52", "", "1d 08h" };
            Color[] rSideColors = { new Color(0.82f, 0.65f, 0.22f, 0.95f), new Color(0.65f, 0.30f, 0.80f, 0.95f), new Color(0.85f, 0.25f, 0.18f, 0.95f), new Color(0.30f, 0.55f, 0.80f, 0.95f) };
            for (int rs = 0; rs < 4; rs++)
            {
                float rsY = 0.66f - rs * 0.088f;
                var rsBtn = AddPanel(canvas, $"RightSideBtn_{rs}", rSideColors[rs]);
                SetAnchors(rsBtn, 0.83f, rsY, 0.99f, rsY + 0.080f);
                var rsBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
                if (rsBtnSpr != null) { rsBtn.GetComponent<Image>().sprite = rsBtnSpr; rsBtn.GetComponent<Image>().type = Image.Type.Sliced; rsBtn.GetComponent<Image>().color = rSideColors[rs]; }
                // Gold ornate border for premium feel
                AddOutlinePanel(rsBtn, new Color(0.82f, 0.65f, 0.26f, 0.60f));
                // Inner dark fill for depth
                var rsInner = AddPanel(rsBtn, "InnerFill", new Color(0.04f, 0.02f, 0.08f, 0.25f));
                SetAnchors(rsInner, 0.06f, 0.06f, 0.94f, 0.94f);
                SetButtonFeedback(rsBtn.AddComponent<Button>());
                AddSceneNav(rsBtn, SceneName.Lobby);
                var rsIcon = AddText(rsBtn, "Icon", rSideIcons[rs], 18, TextAnchor.MiddleCenter);
                SetAnchors(rsIcon, 0.05f, 0.35f, 0.95f, 0.95f);
                rsIcon.GetComponent<Text>().color = Color.white;
                rsIcon.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
                rsIcon.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);
                var rsLbl = AddText(rsBtn, "Label", rSideLabels[rs], 10, TextAnchor.MiddleCenter);
                SetAnchors(rsLbl, 0f, 0.02f, 1f, 0.36f);
                rsLbl.GetComponent<Text>().color = new Color(1f, 0.95f, 0.82f, 1f);
                rsLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
                rsLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
                rsLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.4f, -0.4f);
                // Timer badge if applicable
                if (!string.IsNullOrEmpty(rSideTimers[rs]))
                {
                    var rsTmr = AddPanel(rsBtn, "Timer", new Color(0.04f, 0.03f, 0.08f, 0.80f));
                    SetAnchors(rsTmr, -0.08f, 0.70f, 0.80f, 0.98f);
                    var rsTmrTxt = AddText(rsTmr, "Txt", rSideTimers[rs], 10, TextAnchor.MiddleCenter);
                    StretchToParent(rsTmrTxt);
                    rsTmrTxt.GetComponent<Text>().color = new Color(1f, 0.85f, 0.45f, 0.90f);
                    rsTmrTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
                    rsTmrTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.2f, -0.2f);
                }
            }

            // === CENTER CONTENT ===
            // Title radial glow
            var titleGlow = AddPanel(canvas, "TitleGlow", new Color(0.80f, 0.60f, 0.20f, 0.05f));
            SetAnchors(titleGlow, 0.05f, 0.70f, 0.95f, 0.88f);
            // Game logo / title — ornate, premium
            var logoText = AddText(canvas, "LogoText", "ASHEN THRONE", 42, TextAnchor.MiddleCenter);
            SetAnchors(logoText, 0.1f, 0.72f, 0.9f, 0.85f);
            logoText.GetComponent<Text>().color = Gold;
            logoText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(logoText, new Color(0.2f, 0.12f, 0.03f), 2f);
            // Season subtitle — brighter, spaced lettering for premium feel
            var seasonGlow = AddPanel(canvas, "SeasonGlow", new Color(0.72f, 0.56f, 0.22f, 0.06f));
            SetAnchors(seasonGlow, 0.12f, 0.69f, 0.88f, 0.75f);
            var seasonText = AddText(canvas, "SeasonText", "S E A S O N   1   \u2022   D R A G O N ' S   W R A T H", 10, TextAnchor.MiddleCenter);
            SetAnchors(seasonText, 0.08f, 0.70f, 0.92f, 0.74f);
            seasonText.GetComponent<Text>().color = new Color(0.85f, 0.70f, 0.32f, 0.90f);
            seasonText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var ssSh = seasonText.AddComponent<Shadow>(); ssSh.effectColor = new Color(0, 0, 0, 0.85f); ssSh.effectDistance = new Vector2(0.5f, -0.5f);
            var ssOut = seasonText.AddComponent<Outline>(); ssOut.effectColor = new Color(0.35f, 0.25f, 0.08f, 0.30f); ssOut.effectDistance = new Vector2(0.3f, -0.3f);
            var logoShadow = logoText.AddComponent<Shadow>();
            logoShadow.effectColor = new Color(0, 0, 0, 0.9f);
            logoShadow.effectDistance = new Vector2(3f, -3f);

            // Featured event banner — ornate frame
            var eventBanner = AddPanel(canvas, "EventBanner", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(eventBanner, 0.06f, 0.56f, 0.94f, 0.70f);
            var bannerOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (bannerOrnateSpr != null) { eventBanner.GetComponent<Image>().sprite = bannerOrnateSpr; eventBanner.GetComponent<Image>().type = Image.Type.Sliced; eventBanner.GetComponent<Image>().color = new Color(0.75f, 0.68f, 0.58f, 1f); }
            else { AddOutlinePanel(eventBanner, Ember); }

            var eventTag = AddPanel(eventBanner, "EventTag", Ember);
            SetAnchors(eventTag, 0.0f, 0.78f, 0.22f, 1.0f);
            var eventTagText = AddText(eventTag, "TagText", " \uD83D\uDD25 EVENT", 10, TextAnchor.MiddleLeft);
            StretchToParent(eventTagText);
            eventTagText.GetComponent<Text>().color = TextWhite;
            eventTagText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var etgSh = eventTagText.AddComponent<Shadow>();
            etgSh.effectColor = new Color(0, 0, 0, 0.8f);
            etgSh.effectDistance = new Vector2(0.5f, -0.5f);
            // "LIMITED" badge — red urgency pill
            var limitedBadge = AddPanel(eventBanner, "LimitedBadge", new Color(0.85f, 0.12f, 0.10f, 0.95f));
            SetAnchors(limitedBadge, 0.22f, 0.80f, 0.38f, 0.98f);
            var limitedTxt = AddText(limitedBadge, "Txt", "LIMITED", 10, TextAnchor.MiddleCenter);
            StretchToParent(limitedTxt);
            limitedTxt.GetComponent<Text>().color = Color.white;
            limitedTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            limitedTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            limitedTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.3f);

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
            var edSh = eventDesc.AddComponent<Shadow>(); edSh.effectColor = new Color(0, 0, 0, 0.6f); edSh.effectDistance = new Vector2(0.5f, -0.5f);

            // Countdown timer badge — urgency
            var countdownBg = AddPanel(eventBanner, "CountdownBg", new Color(0.06f, 0.03f, 0.10f, 0.75f));
            SetAnchors(countdownBg, 0.70f, 0.74f, 0.98f, 0.98f);
            var countdownTxt = AddText(countdownBg, "Timer", "\u23F1 2d 14:23:07", 10, TextAnchor.MiddleCenter);
            StretchToParent(countdownTxt);
            countdownTxt.GetComponent<Text>().color = new Color(1f, 0.80f, 0.40f, 0.90f);
            countdownTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            countdownTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            countdownTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

            var eventBtn = AddPanel(eventBanner, "JoinBtn", Ember);
            SetAnchors(eventBtn, 0.74f, 0.18f, 0.97f, 0.72f);
            var joinBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (joinBtnSpr != null) { eventBtn.GetComponent<Image>().sprite = joinBtnSpr; eventBtn.GetComponent<Image>().type = Image.Type.Sliced; eventBtn.GetComponent<Image>().color = new Color(0.91f, 0.45f, 0.16f, 1f); }
            SetButtonFeedback(eventBtn.AddComponent<Button>());
            AddSceneNav(eventBtn, SceneName.Empire);
            var joinLabel = AddText(eventBtn, "Label", "JOIN", 14, TextAnchor.MiddleCenter);
            StretchToParent(joinLabel);
            joinLabel.GetComponent<Text>().color = Color.white;
            joinLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var jlShadow = joinLabel.AddComponent<Shadow>();
            jlShadow.effectColor = new Color(0, 0, 0, 0.8f);
            jlShadow.effectDistance = new Vector2(1f, -1f);

            // === MAIN PLAY BUTTON — ornate, premium, with dramatic glow ===
            // Outer pulsing glow behind button
            var playOuterGlow = AddPanel(canvas, "PlayBtnGlow", new Color(0.90f, 0.25f, 0.12f, 0.08f));
            SetAnchors(playOuterGlow, 0.15f, 0.41f, 0.85f, 0.57f);
            var playMidGlow = AddPanel(canvas, "PlayBtnMidGlow", new Color(0.95f, 0.35f, 0.15f, 0.06f));
            SetAnchors(playMidGlow, 0.18f, 0.42f, 0.82f, 0.56f);
            var playBtn = AddPanel(canvas, "PlayButton", Blood);
            SetAnchors(playBtn, 0.22f, 0.43f, 0.78f, 0.55f);
            var playBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (playBtnSpr != null) { playBtn.GetComponent<Image>().sprite = playBtnSpr; playBtn.GetComponent<Image>().type = Image.Type.Sliced; playBtn.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.18f, 1f); }
            SetButtonFeedback(playBtn.AddComponent<Button>());
            AddSceneNav(playBtn, SceneName.Combat);
            // Inner warm glow
            var playGlow = AddPanel(playBtn, "Glow", new Color(1f, 0.40f, 0.15f, 0.15f));
            SetAnchors(playGlow, 0.05f, 0.08f, 0.95f, 0.92f);
            // Glass highlight top
            var playGlass = AddPanel(playBtn, "Glass", new Color(1f, 0.85f, 0.70f, 0.10f));
            SetAnchors(playGlass, 0.08f, 0.55f, 0.92f, 0.92f);
            var playLabel = AddText(playBtn, "Label", "\u2694  CAMPAIGN", 22, TextAnchor.MiddleCenter);
            StretchToParent(playLabel);
            playLabel.GetComponent<Text>().color = Color.white;
            playLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plShadow = playLabel.AddComponent<Shadow>();
            plShadow.effectColor = new Color(0, 0, 0, 0.9f);
            plShadow.effectDistance = new Vector2(2f, -2f);
            var plOutline = playLabel.AddComponent<Outline>();
            plOutline.effectColor = new Color(0.4f, 0.08f, 0.05f, 0.5f);
            plOutline.effectDistance = new Vector2(1f, -1f);

            // Quick action row — ornate buttons with icons, taller for visibility
            var quickRow = AddPanel(canvas, "QuickActions", new Color(0, 0, 0, 0));
            SetAnchors(quickRow, 0.06f, 0.31f, 0.94f, 0.42f);
            var qrLayout = quickRow.AddComponent<HorizontalLayoutGroup>();
            qrLayout.spacing = 6;
            qrLayout.childForceExpandWidth = true;
            qrLayout.childForceExpandHeight = true;

            var pvpBtn = AddOrnateQuickAction(quickRow, "PvPBtn", "PVP ARENA", Blood, "icon_pvp");
            AddSceneNav(pvpBtn, SceneName.Combat);
            var voidBtn = AddOrnateQuickAction(quickRow, "VoidRiftBtn", "VOID RIFT", Purple, "icon_arcane");
            AddSceneNav(voidBtn, SceneName.Combat);
            var dailyBtn = AddOrnateQuickAction(quickRow, "DailyBtn", "DAILY QUESTS", Teal, "icon_quest");
            AddSceneNav(dailyBtn, SceneName.Lobby);

            // === BATTLE PASS BAR — ornate frame ===
            var bpBar = AddPanel(canvas, "BattlePassBar", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(bpBar, 0.06f, 0.22f, 0.94f, 0.30f);
            if (bannerOrnateSpr != null) { bpBar.GetComponent<Image>().sprite = bannerOrnateSpr; bpBar.GetComponent<Image>().type = Image.Type.Sliced; bpBar.GetComponent<Image>().color = new Color(0.70f, 0.62f, 0.50f, 1f); }
            else { AddOutlinePanel(bpBar, GoldDim); }

            var bpLabel = AddText(bpBar, "BPLabel", "BATTLE PASS", 11, TextAnchor.MiddleLeft);
            SetAnchors(bpLabel, 0.03f, 0.55f, 0.25f, 0.95f);
            bpLabel.GetComponent<Text>().color = Gold;
            bpLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var bplShadow = bpLabel.AddComponent<Shadow>();
            bplShadow.effectColor = new Color(0, 0, 0, 0.8f);
            bplShadow.effectDistance = new Vector2(1f, -1f);

            var bpTier = AddText(bpBar, "BPTier", "Tier 12 / 50", 10, TextAnchor.MiddleLeft);
            SetAnchors(bpTier, 0.03f, 0.08f, 0.30f, 0.50f);
            bpTier.GetComponent<Text>().color = TextMid;
            var bptShadow = bpTier.AddComponent<Shadow>();
            bptShadow.effectColor = new Color(0, 0, 0, 0.7f);
            bptShadow.effectDistance = new Vector2(0.5f, -0.5f);

            var bpTrack = AddPanel(bpBar, "BPTrack", new Color(0.06f, 0.04f, 0.08f));
            SetAnchors(bpTrack, 0.30f, 0.15f, 0.82f, 0.85f);
            AddOutlinePanel(bpTrack, new Color(0.42f, 0.34f, 0.18f, 0.35f));
            var bpFill = AddPanel(bpTrack, "BPFill", Gold);
            SetAnchors(bpFill, 0f, 0f, 0.24f, 1f);
            var bpGlow = AddPanel(bpTrack, "FillGlow", new Color(1f, 0.85f, 0.35f, 0.4f));
            SetAnchors(bpGlow, 0.22f, 0f, 0.27f, 1f);
            // Reward tier markers along the track
            string[] tierIcons = { "\u2726", "\u2694", "\u2B50", "\u2665", "\u265F" };
            float[] tierPositions = { 0.10f, 0.30f, 0.50f, 0.70f, 0.90f };
            for (int ti = 0; ti < 5; ti++)
            {
                bool claimed = tierPositions[ti] < 0.24f;
                // Gold glow ring around each reward node
                var tierGlow = AddPanel(bpTrack, $"TierGlow_{ti}", claimed ? new Color(1f, 0.85f, 0.35f, 0.30f) : new Color(0.60f, 0.48f, 0.22f, 0.15f));
                SetAnchors(tierGlow, tierPositions[ti] - 0.055f, -0.08f, tierPositions[ti] + 0.055f, 1.08f);
                var tierMark = AddPanel(bpTrack, $"Tier_{ti}", claimed ? new Color(0.75f, 0.60f, 0.20f, 0.95f) : new Color(0.10f, 0.08f, 0.16f, 0.98f));
                SetAnchors(tierMark, tierPositions[ti] - 0.045f, 0.02f, tierPositions[ti] + 0.045f, 0.98f);
                // Ornate gold border
                AddOutlinePanel(tierMark, claimed ? new Color(0.95f, 0.78f, 0.30f, 0.90f) : new Color(0.65f, 0.50f, 0.22f, 0.55f));
                // Inner highlight for depth
                var tierInner = AddPanel(tierMark, "Inner", claimed ? new Color(1f, 0.90f, 0.50f, 0.25f) : new Color(0.30f, 0.22f, 0.40f, 0.30f));
                SetAnchors(tierInner, 0.06f, 0.45f, 0.94f, 0.92f);
                var tierIcon = AddText(tierMark, "Icon", tierIcons[ti], 11, TextAnchor.MiddleCenter);
                StretchToParent(tierIcon);
                tierIcon.GetComponent<Text>().color = claimed ? Color.white : new Color(0.70f, 0.60f, 0.48f, 0.85f);
                tierIcon.GetComponent<Text>().fontStyle = FontStyle.Bold;
                tierIcon.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
                tierIcon.GetComponent<Shadow>().effectDistance = new Vector2(0.4f, -0.4f);
            }

            var bpClaimBtn = AddPanel(bpBar, "BPClaimBtn", Gold);
            SetAnchors(bpClaimBtn, 0.84f, 0.12f, 0.98f, 0.88f);
            if (playBtnSpr != null) { bpClaimBtn.GetComponent<Image>().sprite = playBtnSpr; bpClaimBtn.GetComponent<Image>().type = Image.Type.Sliced; bpClaimBtn.GetComponent<Image>().color = new Color(0.82f, 0.65f, 0.25f, 1f); }
            SetButtonFeedback(bpClaimBtn.AddComponent<Button>());
            AddSceneNav(bpClaimBtn, SceneName.Lobby);
            var claimLabel = AddText(bpClaimBtn, "Label", "CLAIM", 11, TextAnchor.MiddleCenter);
            StretchToParent(claimLabel);
            claimLabel.GetComponent<Text>().color = Color.white;
            claimLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clSh2 = claimLabel.AddComponent<Shadow>();
            clSh2.effectColor = new Color(0, 0, 0, 0.8f);
            clSh2.effectDistance = new Vector2(0.5f, -0.5f);
            // "NEW" red pip on claim button
            var bpNewPip = AddPanel(bpClaimBtn, "NewPip", new Color(0.92f, 0.15f, 0.10f, 1f));
            SetAnchors(bpNewPip, 0.55f, 0.72f, 1.12f, 1.10f);
            var bpNewTxt = AddText(bpNewPip, "New", "!", 10, TextAnchor.MiddleCenter);
            StretchToParent(bpNewTxt);
            bpNewTxt.GetComponent<Text>().color = Color.white;
            bpNewTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            bpNewTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            bpNewTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.3f);

            // === LEFT SIDEBAR — floating reward/gift buttons (P&C style, wider) ===
            string[] sideLabels = { "\uD83C\uDF81", "\u2B50", "\u2694" };
            string[] sideTips = { "GIFTS", "LOGIN", "POWER" };
            Color[] sideColors = { new Color(0.85f, 0.25f, 0.20f, 0.95f), new Color(0.82f, 0.65f, 0.22f, 0.95f), new Color(0.50f, 0.30f, 0.80f, 0.95f) };
            bool[] sideHasNew = { true, true, false };
            for (int sb = 0; sb < 3; sb++)
            {
                float sideY = 0.44f - sb * 0.095f;
                var sideBtn = AddPanel(canvas, $"SideBtn_{sb}", sideColors[sb]);
                SetAnchors(sideBtn, 0.01f, sideY, 0.16f, sideY + 0.088f);
                var sideBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
                if (sideBtnSpr != null) { sideBtn.GetComponent<Image>().sprite = sideBtnSpr; sideBtn.GetComponent<Image>().type = Image.Type.Sliced; sideBtn.GetComponent<Image>().color = sideColors[sb]; }
                // Gold ornate border for premium feel
                AddOutlinePanel(sideBtn, new Color(0.82f, 0.65f, 0.26f, 0.65f));
                // Inner dark fill for depth contrast
                var sideInner = AddPanel(sideBtn, "InnerFill", new Color(0.05f, 0.03f, 0.08f, 0.30f));
                SetAnchors(sideInner, 0.06f, 0.06f, 0.94f, 0.94f);
                SetButtonFeedback(sideBtn.AddComponent<Button>());
                AddSceneNav(sideBtn, SceneName.Lobby);
                var sideIcon = AddText(sideBtn, "Icon", sideLabels[sb], 20, TextAnchor.MiddleCenter);
                SetAnchors(sideIcon, 0f, 0.32f, 1f, 0.95f);
                sideIcon.GetComponent<Text>().color = Color.white;
                var siSh = sideIcon.AddComponent<Shadow>();
                siSh.effectColor = new Color(0, 0, 0, 0.7f);
                siSh.effectDistance = new Vector2(0.5f, -0.5f);
                var sideLbl = AddText(sideBtn, "Tip", sideTips[sb], 11, TextAnchor.MiddleCenter);
                SetAnchors(sideLbl, 0f, 0.02f, 1f, 0.34f);
                sideLbl.GetComponent<Text>().color = new Color(1f, 0.95f, 0.82f, 1f);
                sideLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var slSh = sideLbl.AddComponent<Shadow>();
                slSh.effectColor = new Color(0, 0, 0, 0.85f);
                slSh.effectDistance = new Vector2(0.4f, -0.4f);
                // "NEW" red notification pip — circular badge
                if (sideHasNew[sb])
                {
                    var newPip = AddPanel(sideBtn, "NewPip", new Color(0.92f, 0.15f, 0.10f, 1f));
                    SetAnchors(newPip, 0.62f, 0.70f, 1.08f, 1.06f);
                    var roundSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
                    if (roundSpr != null) { newPip.GetComponent<Image>().sprite = roundSpr; newPip.GetComponent<Image>().type = Image.Type.Sliced; newPip.GetComponent<Image>().color = new Color(0.92f, 0.15f, 0.10f, 1f); }
                    // Dark outline ring
                    var pipRing = AddPanel(newPip, "Ring", new Color(0.40f, 0.05f, 0.04f, 0.85f));
                    SetAnchors(pipRing, -0.06f, -0.06f, 1.06f, 1.06f);
                    if (roundSpr != null) { pipRing.GetComponent<Image>().sprite = roundSpr; pipRing.GetComponent<Image>().type = Image.Type.Sliced; }
                    pipRing.transform.SetAsFirstSibling();
                    var newTxt = AddText(newPip, "New", "!", 10, TextAnchor.MiddleCenter);
                    StretchToParent(newTxt);
                    newTxt.GetComponent<Text>().color = Color.white;
                    newTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    newTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
                    newTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.3f);
                }
            }

            // Ornate thin divider between BP and quest
            var midSep = AddPanel(canvas, "MidSeparator", new Color(0.72f, 0.56f, 0.22f, 0.25f));
            SetAnchors(midSep, 0.20f, 0.207f, 0.80f, 0.212f);
            var midSepDiamond = AddPanel(canvas, "MidSepDiamond", new Color(0.85f, 0.68f, 0.28f, 0.50f));
            SetAnchors(midSepDiamond, 0.48f, 0.200f, 0.52f, 0.220f);
            midSepDiamond.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 45);

            // === DAILY QUEST SUMMARY — ornate frame ===
            var questSummary = AddPanel(canvas, "QuestSummary", new Color(0.05f, 0.04f, 0.10f, 0.94f));
            SetAnchors(questSummary, 0.06f, 0.12f, 0.94f, 0.20f);
            if (bannerOrnateSpr != null) { questSummary.GetComponent<Image>().sprite = bannerOrnateSpr; questSummary.GetComponent<Image>().type = Image.Type.Sliced; questSummary.GetComponent<Image>().color = new Color(0.70f, 0.62f, 0.50f, 1f); }
            else { AddOutlinePanel(questSummary, TealDim); }

            // Quest icon
            var questIcon = AddPanel(questSummary, "QuestIcon", new Color(0, 0, 0, 0));
            SetAnchors(questIcon, 0.02f, 0.15f, 0.10f, 0.85f);
            var qiSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_quest.png");
            if (qiSpr != null) { questIcon.GetComponent<Image>().sprite = qiSpr; questIcon.GetComponent<Image>().preserveAspect = true; questIcon.GetComponent<Image>().color = new Color(0.45f, 0.90f, 0.80f, 0.90f); }

            var questLabel = AddText(questSummary, "QLabel", "DAILY QUESTS", 11, TextAnchor.MiddleLeft);
            SetAnchors(questLabel, 0.11f, 0.55f, 0.40f, 0.95f);
            questLabel.GetComponent<Text>().color = Teal;
            questLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var qlShadow = questLabel.AddComponent<Shadow>();
            qlShadow.effectColor = new Color(0, 0, 0, 0.8f);
            qlShadow.effectDistance = new Vector2(1f, -1f);

            var questProgress = AddText(questSummary, "QProgress", "3/5 Complete  \u2022  Next: Win 2 PvP battles", 10, TextAnchor.MiddleLeft);
            SetAnchors(questProgress, 0.11f, 0.08f, 0.78f, 0.50f);
            questProgress.GetComponent<Text>().color = TextMid;
            var qpSh = questProgress.AddComponent<Shadow>(); qpSh.effectColor = new Color(0, 0, 0, 0.6f); qpSh.effectDistance = new Vector2(0.5f, -0.5f);

            // Quest dots — small progress indicator pips, positioned manually
            for (int i = 0; i < 5; i++)
            {
                float dotX = 0.48f + i * 0.065f;
                var dot = AddPanel(questSummary, $"Dot_{i}", i < 3 ? Teal : new Color(0.15f, 0.12f, 0.20f));
                SetAnchors(dot, dotX, 0.65f, dotX + 0.035f, 0.85f);
                if (plusRoundSpr != null) { dot.GetComponent<Image>().sprite = plusRoundSpr; dot.GetComponent<Image>().type = Image.Type.Sliced; }
            }

            // Go button for quests
            var questGoBtn = AddPanel(questSummary, "GoBtn", Teal);
            SetAnchors(questGoBtn, 0.84f, 0.15f, 0.97f, 0.85f);
            if (playBtnSpr != null) { questGoBtn.GetComponent<Image>().sprite = playBtnSpr; questGoBtn.GetComponent<Image>().type = Image.Type.Sliced; questGoBtn.GetComponent<Image>().color = new Color(0.18f, 0.72f, 0.65f, 1f); }
            SetButtonFeedback(questGoBtn.AddComponent<Button>());
            AddSceneNav(questGoBtn, SceneName.Empire);
            var goLabel = AddText(questGoBtn, "Label", "GO", 13, TextAnchor.MiddleCenter);
            StretchToParent(goLabel);
            goLabel.GetComponent<Text>().color = Color.white;
            goLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var goSh = goLabel.AddComponent<Shadow>();
            goSh.effectColor = new Color(0, 0, 0, 0.8f);
            goSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === BOTTOM NAV BAR — warm dark base with multi-layer gold borders, visible depth ===
            var navBarBg = AddPanel(canvasGo, "NavBarBg", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(navBarBg, 0f, 0f, 1f, 0.06f);

            var navBar = AddPanel(canvas, "BottomNavBar", new Color(0.10f, 0.07f, 0.16f, 1f));
            SetAnchors(navBar, 0f, 0f, 1f, 0.10f);
            // Triple gold border — outermost bright
            var navBorder1 = AddPanel(navBar, "TopBorder1", new Color(0.92f, 0.74f, 0.32f, 1f));
            SetAnchors(navBorder1, 0f, 0.96f, 1f, 1f);
            navBorder1.AddComponent<LayoutElement>().ignoreLayout = true;
            // Middle dark inset
            var navBorder2 = AddPanel(navBar, "TopBorder2", new Color(0.30f, 0.22f, 0.10f, 0.85f));
            SetAnchors(navBorder2, 0f, 0.93f, 1f, 0.96f);
            navBorder2.AddComponent<LayoutElement>().ignoreLayout = true;
            // Inner thin gold line
            var navBorder3 = AddPanel(navBar, "TopBorder3", new Color(0.75f, 0.58f, 0.24f, 0.65f));
            SetAnchors(navBorder3, 0f, 0.915f, 1f, 0.93f);
            navBorder3.AddComponent<LayoutElement>().ignoreLayout = true;
            // Warm golden glow beneath borders
            var navTopGlow = AddPanel(navBar, "TopGlow", new Color(0.75f, 0.55f, 0.20f, 0.18f));
            SetAnchors(navTopGlow, 0f, 0.82f, 1f, 0.915f);
            navTopGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            // Upper half lighter for glass depth
            var navHighlight = AddPanel(navBar, "Highlight", new Color(0.16f, 0.11f, 0.24f, 0.50f));
            SetAnchors(navHighlight, 0f, 0.50f, 1f, 0.82f);
            navHighlight.AddComponent<LayoutElement>().ignoreLayout = true;
            // Bottom fade to black
            var navBotFade = AddPanel(navBar, "BotFade", new Color(0.03f, 0.02f, 0.05f, 0.50f));
            SetAnchors(navBotFade, 0f, 0f, 1f, 0.15f);
            navBotFade.AddComponent<LayoutElement>().ignoreLayout = true;

            // Gold diamond accent where center button meets left border
            var navDiamondL = AddPanel(navBar, "DiamondL", new Color(0.92f, 0.74f, 0.32f, 0.85f));
            SetAnchors(navDiamondL, 0.325f, 0.88f, 0.355f, 1.04f);
            navDiamondL.AddComponent<LayoutElement>().ignoreLayout = true;
            navDiamondL.transform.localRotation = Quaternion.Euler(0, 0, 45);
            // Gold diamond accent where center button meets right border
            var navDiamondR = AddPanel(navBar, "DiamondR", new Color(0.92f, 0.74f, 0.32f, 0.85f));
            SetAnchors(navDiamondR, 0.645f, 0.88f, 0.675f, 1.04f);
            navDiamondR.AddComponent<LayoutElement>().ignoreLayout = true;
            navDiamondR.transform.localRotation = Quaternion.Euler(0, 0, 45);

            // === Nav items — 3 left, CENTER raised button, 3 right ===
            var navLayoutLeft = AddPanel(navBar, "NavLeft", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutLeft, 0f, 0.02f, 0.38f, 0.94f);
            var nllLayout = navLayoutLeft.AddComponent<HorizontalLayoutGroup>();
            nllLayout.spacing = 0;
            nllLayout.padding = new RectOffset(4, 0, 4, 6);
            nllLayout.childForceExpandWidth = true;
            nllLayout.childForceExpandHeight = true;

            AddNavItem(navLayoutLeft, "NavWorld", "WORLD", Ember, false, 0, SceneName.WorldMap);
            AddNavItem(navLayoutLeft, "NavHero", "HERO", Purple, false, 2, SceneName.Lobby);
            AddNavItem(navLayoutLeft, "NavQuest", "QUEST", Teal, false, 17, SceneName.Lobby);

            // === CENTER BUTTON — dramatic raised ornate jewel button with multi-layer glow ===
            var centerGlow1 = AddPanel(navBar, "CenterGlow1", new Color(0.72f, 0.52f, 0.18f, 0.06f));
            SetAnchors(centerGlow1, 0.26f, -0.02f, 0.74f, 1.28f);
            var centerGlow2 = AddPanel(navBar, "CenterGlow2", new Color(0.88f, 0.62f, 0.22f, 0.10f));
            SetAnchors(centerGlow2, 0.30f, 0.02f, 0.70f, 1.22f);
            var centerGlow3 = AddPanel(navBar, "CenterGlow3", new Color(0.95f, 0.55f, 0.18f, 0.12f));
            SetAnchors(centerGlow3, 0.33f, 0.04f, 0.67f, 1.18f);

            var centerBtn = AddPanel(navBar, "NavCenterBtn", new Color(0.10f, 0.06f, 0.16f, 1f));
            SetAnchors(centerBtn, 0.34f, 0.04f, 0.66f, 1.16f);
            var centerBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (centerBtnSpr != null)
            {
                var cImg = centerBtn.GetComponent<Image>();
                cImg.sprite = centerBtnSpr;
                cImg.type = Image.Type.Sliced;
                cImg.color = new Color(0.88f, 0.74f, 0.45f, 1f);
            }
            else
            {
                AddOutlinePanel(centerBtn, new Color(0.90f, 0.72f, 0.30f, 0.95f));
            }

            // Inner fill — warm dark purple with glass layers
            var centerInner = AddPanel(centerBtn, "Inner", new Color(0.06f, 0.03f, 0.10f, 0.96f));
            SetAnchors(centerInner, 0.07f, 0.05f, 0.93f, 0.95f);
            var centerHighlight = AddPanel(centerInner, "Highlight", new Color(0.25f, 0.18f, 0.35f, 0.40f));
            SetAnchors(centerHighlight, 0.04f, 0.60f, 0.96f, 0.96f);
            var centerWarmth = AddPanel(centerInner, "Warmth", new Color(0.14f, 0.08f, 0.20f, 0.30f));
            SetAnchors(centerWarmth, 0.04f, 0.25f, 0.96f, 0.60f);
            // Ember glow behind icon — three concentric layers
            var centerEmber1 = AddPanel(centerInner, "Ember1", new Color(0.88f, 0.42f, 0.14f, 0.08f));
            SetAnchors(centerEmber1, 0.02f, 0.15f, 0.98f, 0.88f);
            var centerEmber2 = AddPanel(centerInner, "Ember2", new Color(0.92f, 0.50f, 0.18f, 0.14f));
            SetAnchors(centerEmber2, 0.10f, 0.22f, 0.90f, 0.80f);
            var centerEmber3 = AddPanel(centerInner, "Ember3", new Color(0.98f, 0.60f, 0.22f, 0.12f));
            SetAnchors(centerEmber3, 0.20f, 0.30f, 0.80f, 0.72f);

            // Icon — empire castle sprite
            var centerIcon = AddPanel(centerInner, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(centerIcon, 0.10f, 0.20f, 0.90f, 0.86f);
            var empSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_empire.png");
            if (empSpr != null)
            {
                centerIcon.GetComponent<Image>().sprite = empSpr;
                centerIcon.GetComponent<Image>().preserveAspect = true;
                centerIcon.GetComponent<Image>().color = new Color(1f, 0.94f, 0.78f, 1f);
            }
            else { centerIcon.GetComponent<Image>().color = Ember; }

            // "EMPIRE" label — bright warm gold, bold, crisp
            var centerLabel = AddText(centerInner, "Label", "EMPIRE", 12, TextAnchor.MiddleCenter);
            SetAnchors(centerLabel, 0f, 0.01f, 1f, 0.22f);
            centerLabel.GetComponent<Text>().color = new Color(1f, 0.95f, 0.75f, 1f);
            centerLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clShadow = centerLabel.AddComponent<Shadow>();
            clShadow.effectColor = new Color(0, 0, 0, 0.98f);
            clShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var clOutline = centerLabel.AddComponent<Outline>();
            clOutline.effectColor = new Color(0.50f, 0.35f, 0.12f, 0.55f);
            clOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // Crown accent — triple gold top strip
            var centerCrown1 = AddPanel(centerBtn, "Crown1", new Color(0.94f, 0.76f, 0.32f, 1f));
            SetAnchors(centerCrown1, 0.05f, 0.980f, 0.95f, 1f);
            var centerCrown2 = AddPanel(centerBtn, "Crown2", new Color(0.55f, 0.42f, 0.18f, 0.70f));
            SetAnchors(centerCrown2, 0.08f, 0.962f, 0.92f, 0.980f);
            var centerCrown3 = AddPanel(centerBtn, "Crown3", new Color(0.80f, 0.62f, 0.25f, 0.45f));
            SetAnchors(centerCrown3, 0.12f, 0.948f, 0.88f, 0.962f);
            // Bottom accent strip
            var centerBotAccent = AddPanel(centerBtn, "BotAccent", new Color(0.78f, 0.60f, 0.24f, 0.55f));
            SetAnchors(centerBotAccent, 0.08f, 0f, 0.92f, 0.028f);
            // Side gold accents
            var centerLeftAccent = AddPanel(centerBtn, "LeftAccent", new Color(0.82f, 0.64f, 0.26f, 0.40f));
            SetAnchors(centerLeftAccent, 0f, 0.10f, 0.02f, 0.90f);
            var centerRightAccent = AddPanel(centerBtn, "RightAccent", new Color(0.82f, 0.64f, 0.26f, 0.40f));
            SetAnchors(centerRightAccent, 0.98f, 0.10f, 1f, 0.90f);
            SetButtonFeedback(centerBtn.AddComponent<Button>());
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
            AddNavItem(navLayoutRight, "NavAlliance", "ALLIANCE", TealDim, false, 10, SceneName.Alliance);
            AddNavItem(navLayoutRight, "NavRank", "RANK", EmberDim, false, 0, SceneName.Lobby);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Lobby scene: P&C-quality main menu hub — v29 with RANK nav");
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
            var combatNotchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.45f));
            SetAnchors(combatNotchBorder, 0f, 0f, 1f, 0.012f);

            var canvas = CreateSafeArea(canvasGo);
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");

            // === TOP BAR — solid dark with visible gold accents (no ornate sprite) ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.10f, 0.08f, 0.16f, 0.98f));
            SetAnchors(topBar, 0f, 0.915f, 1f, 0.995f);
            // Triple gold bottom border
            var topBorderGold = AddPanel(topBar, "BorderGold", new Color(0.90f, 0.72f, 0.30f, 1f));
            SetAnchors(topBorderGold, 0f, 0f, 1f, 0.05f);
            topBorderGold.AddComponent<LayoutElement>().ignoreLayout = true;
            var topBorderMid = AddPanel(topBar, "BorderMid", new Color(0.60f, 0.45f, 0.15f, 0.70f));
            SetAnchors(topBorderMid, 0f, 0.05f, 1f, 0.09f);
            topBorderMid.AddComponent<LayoutElement>().ignoreLayout = true;
            var topBorderDark = AddPanel(topBar, "BorderDark", new Color(0.35f, 0.25f, 0.10f, 0.40f));
            SetAnchors(topBorderDark, 0f, 0.09f, 1f, 0.12f);
            topBorderDark.AddComponent<LayoutElement>().ignoreLayout = true;
            // Warm glass highlight at top
            var topGlass = AddPanel(topBar, "GlassTop", new Color(0.35f, 0.28f, 0.45f, 0.15f));
            SetAnchors(topGlass, 0f, 0.85f, 1f, 1f);
            topGlass.AddComponent<LayoutElement>().ignoreLayout = true;

            // Phase label — BRIGHT gold, unmissable
            // Warm glow behind phase label
            var phaseGlow = AddPanel(topBar, "PhaseGlow", new Color(0.80f, 0.55f, 0.15f, 0.10f));
            SetAnchors(phaseGlow, 0.18f, 0.05f, 0.82f, 0.98f);
            phaseGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            var phaseLabel = AddText(topBar, "PhaseLabel", "ACTION PHASE", 22, TextAnchor.MiddleCenter);
            SetAnchors(phaseLabel, 0.20f, 0.10f, 0.80f, 0.95f);
            phaseLabel.GetComponent<Text>().color = new Color(1f, 0.88f, 0.42f, 1f);
            phaseLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pShadow = phaseLabel.AddComponent<Shadow>();
            pShadow.effectColor = new Color(0, 0, 0, 0.98f);
            pShadow.effectDistance = new Vector2(2f, -2f);
            var pOutline = phaseLabel.AddComponent<Outline>();
            pOutline.effectColor = new Color(0.50f, 0.35f, 0.08f, 0.65f);
            pOutline.effectDistance = new Vector2(1f, -1f);

            // Turn counter — left side, bright white
            var turnCounter = AddText(topBar, "TurnCounter", "Turn 3", 14, TextAnchor.MiddleLeft);
            SetAnchors(turnCounter, 0.02f, 0.1f, 0.20f, 0.9f);
            turnCounter.GetComponent<Text>().color = new Color(0.90f, 0.85f, 0.75f, 1f);
            turnCounter.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var tcSh = turnCounter.AddComponent<Shadow>();
            tcSh.effectColor = new Color(0, 0, 0, 0.95f);
            tcSh.effectDistance = new Vector2(1f, -1f);

            // Retreat button — bright red, ornate
            var retreatBtn = AddPanel(topBar, "RetreatBtn", BloodDark);
            SetAnchors(retreatBtn, 0.80f, 0.10f, 0.98f, 0.90f);
            if (btnOrnateSpr != null) { retreatBtn.GetComponent<Image>().sprite = btnOrnateSpr; retreatBtn.GetComponent<Image>().type = Image.Type.Sliced; retreatBtn.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.15f, 1f); }
            SetButtonFeedback(retreatBtn.AddComponent<Button>());
            AddSceneNav(retreatBtn, SceneName.Empire);
            var rtLabel = AddText(retreatBtn, "Label", "RETREAT", 12, TextAnchor.MiddleCenter);
            StretchToParent(rtLabel);
            rtLabel.GetComponent<Text>().color = Color.white;
            rtLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var rtSh = rtLabel.AddComponent<Shadow>();
            rtSh.effectColor = new Color(0, 0, 0, 0.95f);
            rtSh.effectDistance = new Vector2(1f, -1f);
            var rtOutline = rtLabel.AddComponent<Outline>();
            rtOutline.effectColor = new Color(0.80f, 0.15f, 0.10f, 0.5f);
            rtOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // === TURN ORDER — ornate panel (right side), wider for readability ===
            var turnPanel = AddPanel(canvas, "TurnOrderPanel", new Color(0.04f, 0.03f, 0.08f, 0.94f));
            SetAnchors(turnPanel, 0.84f, 0.32f, 0.99f, 0.91f);
            if (ornateSpr != null) { turnPanel.GetComponent<Image>().sprite = ornateSpr; turnPanel.GetComponent<Image>().type = Image.Type.Sliced; turnPanel.GetComponent<Image>().color = new Color(0.52f, 0.48f, 0.40f, 1f); }
            else { AddOutlinePanel(turnPanel, GoldDim); }
            var turnGlass = AddPanel(turnPanel, "GlassTop", new Color(0.25f, 0.20f, 0.32f, 0.15f));
            SetAnchors(turnGlass, 0.05f, 0.93f, 0.95f, 0.99f);
            // Inner shadow for depth
            var turnInnerShadow = AddPanel(turnPanel, "InnerShadow", new Color(0.02f, 0.01f, 0.04f, 0.20f));
            SetAnchors(turnInnerShadow, 0.02f, 0f, 0.98f, 0.04f);
            turnInnerShadow.AddComponent<LayoutElement>().ignoreLayout = true;

            var toTitle = AddText(turnPanel, "TOTitle", "TURN ORDER", 12, TextAnchor.MiddleCenter);
            SetAnchors(toTitle, 0f, 0.93f, 1f, 1f);
            toTitle.GetComponent<Text>().color = Gold;
            toTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var toSh = toTitle.AddComponent<Shadow>(); toSh.effectColor = new Color(0, 0, 0, 0.90f); toSh.effectDistance = new Vector2(0.8f, -0.8f);
            var toOutline = toTitle.AddComponent<Outline>(); toOutline.effectColor = new Color(0.40f, 0.28f, 0.08f, 0.35f); toOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Round indicator
            var roundLabel = AddText(turnPanel, "Round", "ROUND 3", 10, TextAnchor.MiddleCenter);
            SetAnchors(roundLabel, 0.15f, 0.90f, 0.85f, 0.935f);
            roundLabel.GetComponent<Text>().color = new Color(0.70f, 0.62f, 0.45f, 0.75f);
            roundLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var rlSh = roundLabel.AddComponent<Shadow>(); rlSh.effectColor = new Color(0, 0, 0, 0.7f); rlSh.effectDistance = new Vector2(0.3f, -0.3f);
            // Gold separator line
            var toSep = AddPanel(turnPanel, "Separator", new Color(0.78f, 0.60f, 0.24f, 0.50f));
            SetAnchors(toSep, 0.08f, 0.892f, 0.92f, 0.898f);
            toSep.AddComponent<LayoutElement>().ignoreLayout = true;

            var tokenArea = AddPanel(turnPanel, "TokenArea", new Color(0, 0, 0, 0));
            SetAnchors(tokenArea, 0.04f, 0.02f, 0.96f, 0.89f);
            var taLayout = tokenArea.AddComponent<VerticalLayoutGroup>();
            taLayout.spacing = 4;
            taLayout.padding = new RectOffset(2, 2, 3, 3);

            string[] heroNames = { "Kaelen", "Vorra", "Seraphyn", "Mordoc", "Lyra", "Skaros" };
            Color[] heroColors = { Blood, Ember, Purple, IronColor, Teal, BloodDark };
            float[] tokenHp = { 0.85f, 0.55f, 1.0f, 0.70f, 0.40f, 0.90f };
            // Hero portrait sprites for turn order tokens
            string[] tokenPortraitKeys = { "kael_ashwalker", "lyra_thornveil", "sera_dawnblade", "grim_bonecrusher", "nyx_stormcaller", "vex_shadowstrike" };
            for (int i = 0; i < 6; i++)
            {
                bool isActive = i == 0;
                Color tokenBg = i < 3 ? new Color(0.08f, 0.12f, 0.25f, 0.92f) : new Color(0.25f, 0.08f, 0.08f, 0.92f);
                var token = AddPanel(tokenArea, $"Token_{i}", tokenBg);
                token.AddComponent<LayoutElement>().preferredHeight = 50;
                // Active token — bright gold border + outer glow
                if (isActive)
                {
                    AddOutlinePanel(token, new Color(0.92f, 0.75f, 0.30f, 1f));
                    var activeGlow = AddPanel(token, "Glow", new Color(0.85f, 0.68f, 0.28f, 0.18f));
                    StretchToParent(activeGlow);
                    activeGlow.AddComponent<LayoutElement>().ignoreLayout = true;
                }
                else { AddOutlinePanel(token, new Color(0.35f, 0.28f, 0.20f, 0.4f)); }
                // Portrait — use actual hero portrait sprite
                var tokPortSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Characters/Heroes/{tokenPortraitKeys[i]}_portrait.png");
                var tIcon = AddPanel(token, "Icon", tokPortSpr != null ? Color.white : new Color(heroColors[i].r * 0.45f, heroColors[i].g * 0.45f, heroColors[i].b * 0.45f, 1f));
                SetAnchors(tIcon, 0.04f, 0.06f, 0.38f, 0.94f);
                if (tokPortSpr != null) { tIcon.GetComponent<Image>().sprite = tokPortSpr; tIcon.GetComponent<Image>().preserveAspect = true; }
                AddOutlinePanel(tIcon, new Color(0.60f, 0.48f, 0.20f, isActive ? 0.8f : 0.35f));
                // Fallback initial if no sprite
                if (tokPortSpr == null)
                {
                    var tInit = AddText(tIcon, "Init", heroNames[i][..1], 12, TextAnchor.MiddleCenter);
                    StretchToParent(tInit);
                    tInit.GetComponent<Text>().color = new Color(Mathf.Min(1f, heroColors[i].r + 0.3f), Mathf.Min(1f, heroColors[i].g + 0.3f), Mathf.Min(1f, heroColors[i].b + 0.3f), 0.90f);
                    tInit.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    var tiSh = tInit.AddComponent<Shadow>();
                    tiSh.effectColor = new Color(0, 0, 0, 0.90f);
                    tiSh.effectDistance = new Vector2(0.5f, -0.5f);
                }
                // Name — show 6 chars for readability
                int nameLen = Mathf.Min(6, heroNames[i].Length);
                var tName = AddText(token, "Name", heroNames[i][..nameLen], 12, TextAnchor.MiddleLeft);
                SetAnchors(tName, 0.42f, 0.40f, 1f, 0.96f);
                tName.GetComponent<Text>().color = isActive ? Gold : TextLight;
                tName.GetComponent<Text>().fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal;
                var tnSh = tName.AddComponent<Shadow>(); tnSh.effectColor = new Color(0, 0, 0, 0.7f); tnSh.effectDistance = new Vector2(0.5f, -0.5f);
                // Mini HP bar in token — wider
                var miniHpBg = AddPanel(token, "MiniHp", new Color(0.06f, 0.05f, 0.08f, 0.85f));
                SetAnchors(miniHpBg, 0.42f, 0.06f, 0.96f, 0.36f);
                Color tkHpColor = tokenHp[i] > 0.5f ? BarHpGreen : tokenHp[i] > 0.25f ? Ember : BarHpRed;
                var miniHpFill = AddPanel(miniHpBg, "Fill", tkHpColor);
                SetAnchors(miniHpFill, 0f, 0f, tokenHp[i], 1f);
            }

            // === PLAYER HERO STATUS — left side, wider panels ===
            var playerStatus = AddPanel(canvas, "PlayerHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(playerStatus, 0.01f, 0.52f, 0.19f, 0.91f);
            var psLayout = playerStatus.AddComponent<VerticalLayoutGroup>();
            psLayout.spacing = 4;

            string[] pHeroes = { "Kaelen", "Vorra", "Seraphyn" };
            float[] pHp = { 0.85f, 0.55f, 1.0f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(playerStatus, pHeroes[i], pHp[i], heroColors[i], true);

            // === ENEMY HERO STATUS — right-center, wider ===
            var enemyStatus = AddPanel(canvas, "EnemyHeroStatus", new Color(0, 0, 0, 0));
            SetAnchors(enemyStatus, 0.68f, 0.52f, 0.86f, 0.91f);
            var esLayout = enemyStatus.AddComponent<VerticalLayoutGroup>();
            esLayout.spacing = 4;

            string[] eHeroes = { "Mordoc", "Lyra", "Skaros" };
            float[] eHp = { 0.70f, 0.40f, 0.90f };
            for (int i = 0; i < 3; i++)
                AddHeroStatusPanel(enemyStatus, eHeroes[i], eHp[i], heroColors[i + 3], false);

            // === COMBAT GRID — 7×5 tile grid fills center area ===
            var gridContainer = AddPanel(canvas, "CombatGrid", new Color(0.03f, 0.03f, 0.05f, 0.96f));
            SetAnchors(gridContainer, 0.02f, 0.22f, 0.86f, 0.915f);
            // Load tile sprites for terrain variety
            Sprite[] tileSprites = {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_stone.png"),  // 0 default
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_lava.png"),   // 1
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_ice.png"),    // 2
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_holy.png"),   // 3
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_void.png"),   // 4
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/tile_grass.png"),  // 5
            };
            // Grid layout: 7 cols × 5 rows. Columns 0-2 = player, 3 = neutral, 4-6 = enemy
            int[,] gridTerrain = {
                { 0, 0, 5, 3, 4, 0, 0 },
                { 0, 5, 0, 0, 0, 1, 0 },
                { 5, 0, 0, 3, 0, 0, 4 },
                { 0, 5, 0, 0, 0, 1, 0 },
                { 0, 0, 5, 3, 4, 0, 0 },
            };
            // Hero positions: (row, col) -> hero index. -1 = empty
            int[,] heroGrid = {
                { -1, -1,  0, -1, -1, -1,  3 },
                { -1,  1, -1, -1, -1,  4, -1 },
                {  2, -1, -1, -1, -1, -1,  5 },
                { -1,  1, -1, -1, -1,  4, -1 },
                { -1, -1,  0, -1, -1, -1,  3 },
            };
            // Hero fullbody sprites
            Sprite[] heroSprites = {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/kael_ashwalker_fullbody.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/lyra_thornveil_fullbody.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/sera_dawnblade_fullbody.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/grim_bonecrusher_fullbody.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/nyx_stormcaller_fullbody.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/Heroes/vex_shadowstrike_fullbody.png"),
            };
            Color[] heroTints = {
                new Color(0.15f, 0.25f, 0.50f, 0.35f), // player blue
                new Color(0.15f, 0.25f, 0.50f, 0.35f),
                new Color(0.15f, 0.25f, 0.50f, 0.35f),
                new Color(0.50f, 0.12f, 0.10f, 0.35f), // enemy red
                new Color(0.50f, 0.12f, 0.10f, 0.35f),
                new Color(0.50f, 0.12f, 0.10f, 0.35f),
            };
            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    int ti = gridTerrain[row, col];
                    float cx0 = (float)col / 7f, cy0 = (float)row / 5f;
                    float cx1 = (float)(col + 1) / 7f, cy1 = (float)(row + 1) / 5f;
                    var cell = AddPanel(gridContainer, $"Cell_{row}_{col}", Color.white);
                    SetAnchors(cell, cx0, cy0, cx1, cy1);
                    var cellImg = cell.GetComponent<Image>();
                    if (tileSprites[ti] != null)
                    {
                        cellImg.sprite = tileSprites[ti];
                        cellImg.type = Image.Type.Simple;
                        cellImg.preserveAspect = false;
                        cellImg.color = new Color(0.88f, 0.86f, 0.82f, 1f); // bright terrain
                    }
                    else { cellImg.color = new Color(0.10f, 0.10f, 0.12f, 1f); }
                    // Grid cell border — gold tint for visibility
                    AddOutlinePanel(cell, new Color(0.40f, 0.32f, 0.15f, 0.50f));

                    // Zone tint: player zone blue, enemy zone red, neutral gold — STRONG differentiation
                    if (col < 3)
                    {
                        var zoneTint = AddPanel(cell, "ZoneTint", new Color(0.08f, 0.18f, 0.50f, 0.35f));
                        StretchToParent(zoneTint);
                        zoneTint.GetComponent<Image>().raycastTarget = false;
                    }
                    else if (col > 3)
                    {
                        var zoneTint = AddPanel(cell, "ZoneTint", new Color(0.50f, 0.08f, 0.06f, 0.35f));
                        StretchToParent(zoneTint);
                        zoneTint.GetComponent<Image>().raycastTarget = false;
                    }
                    else // neutral column 3
                    {
                        var zoneTint = AddPanel(cell, "ZoneTint", new Color(0.50f, 0.42f, 0.12f, 0.28f));
                        StretchToParent(zoneTint);
                        zoneTint.GetComponent<Image>().raycastTarget = false;
                    }

                    // Tile effect icon — small indicator for special terrain
                    if (ti > 0) // not default stone
                    {
                        string[] tileIcons = { "", "\uD83D\uDD25", "\u2744", "\u2726", "\u2620", "\uD83C\uDF3F" };
                        Color[] tileIconColors = { Color.clear, new Color(1f, 0.55f, 0.15f, 0.55f), new Color(0.50f, 0.85f, 1f, 0.50f), new Color(1f, 0.90f, 0.45f, 0.50f), new Color(0.70f, 0.30f, 0.90f, 0.50f), new Color(0.40f, 0.85f, 0.35f, 0.45f) };
                        if (ti < tileIcons.Length)
                        {
                            var tileIcon = AddText(cell, "TileEffect", tileIcons[ti], 12, TextAnchor.LowerRight);
                            SetAnchors(tileIcon, 0.55f, 0.02f, 0.98f, 0.40f);
                            tileIcon.GetComponent<Text>().color = tileIconColors[ti];
                            tileIcon.GetComponent<Text>().raycastTarget = false;
                        }
                    }

                    // Place hero sprite if present
                    int hi = heroGrid[row, col];
                    if (hi >= 0 && hi < heroSprites.Length && heroSprites[hi] != null)
                    {
                        var heroGO = AddPanel(cell, $"Hero_{hi}", Color.white);
                        SetAnchors(heroGO, 0.05f, 0.12f, 0.95f, 0.98f);
                        var hImg = heroGO.GetComponent<Image>();
                        hImg.sprite = heroSprites[hi];
                        hImg.preserveAspect = true;
                        hImg.raycastTarget = false;
                        var hSh = heroGO.AddComponent<Shadow>();
                        hSh.effectColor = new Color(0, 0, 0, 0.85f);
                        hSh.effectDistance = new Vector2(2f, -2.5f);
                        // HP bar background
                        var hpBg = AddPanel(cell, $"HpBg_{hi}", new Color(0.08f, 0.06f, 0.10f, 0.85f));
                        SetAnchors(hpBg, 0.08f, 0.02f, 0.92f, 0.10f);
                        AddOutlinePanel(hpBg, new Color(0.25f, 0.22f, 0.18f, 0.50f));
                        // HP fill — player green, enemy red
                        bool isPlayer = col < 3;
                        float hpPct = isPlayer ? (0.60f + hi * 0.12f) : (0.40f + (hi - 3) * 0.15f);
                        hpPct = Mathf.Clamp01(hpPct);
                        var hpFill = AddPanel(hpBg, "Fill", isPlayer ? new Color(0.20f, 0.82f, 0.30f, 1f) : new Color(0.88f, 0.22f, 0.15f, 1f));
                        SetAnchors(hpFill, 0.03f, 0.08f, 0.03f + 0.94f * hpPct, 0.92f);
                        // HP glow at fill edge
                        var hpGlow = AddPanel(hpBg, "Glow", isPlayer ? new Color(0.50f, 1f, 0.60f, 0.35f) : new Color(1f, 0.50f, 0.35f, 0.35f));
                        SetAnchors(hpGlow, 0.03f + 0.94f * hpPct - 0.05f, 0f, 0.03f + 0.94f * hpPct + 0.02f, 1f);
                    }
                }
            }
            // VS emblem in center neutral column — dramatic with glow
            var vsOuterGlow = AddPanel(gridContainer, "VsOuterGlow", new Color(0.85f, 0.65f, 0.20f, 0.18f));
            SetAnchors(vsOuterGlow, 0.34f, 0.30f, 0.66f, 0.70f);
            vsOuterGlow.GetComponent<Image>().raycastTarget = false;
            var vsBg = AddPanel(gridContainer, "VsBg", new Color(0.06f, 0.04f, 0.10f, 0.75f));
            SetAnchors(vsBg, 0.39f, 0.36f, 0.61f, 0.64f);
            vsBg.GetComponent<Image>().raycastTarget = false;
            AddOutlinePanel(vsBg, new Color(0.85f, 0.68f, 0.28f, 0.65f));
            var vsText = AddText(gridContainer, "VsText", "VS", 32, TextAnchor.MiddleCenter);
            SetAnchors(vsText, 0.38f, 0.38f, 0.62f, 0.62f);
            vsText.GetComponent<Text>().color = new Color(1f, 0.85f, 0.35f, 0.95f);
            vsText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vsSh = vsText.AddComponent<Shadow>(); vsSh.effectColor = new Color(0, 0, 0, 0.95f); vsSh.effectDistance = new Vector2(2f, -2f);
            var vsOut = vsText.AddComponent<Outline>(); vsOut.effectColor = new Color(0.65f, 0.45f, 0.10f, 0.55f); vsOut.effectDistance = new Vector2(1.2f, -1.2f);

            // Column zone labels — bigger, clearer
            var playerZoneLabel = AddText(gridContainer, "PlayerZone", "PLAYER ZONE", 14, TextAnchor.MiddleCenter);
            SetAnchors(playerZoneLabel, 0f, 0.955f, 0.43f, 1f);
            playerZoneLabel.GetComponent<Text>().color = new Color(0.60f, 0.82f, 1f, 0.90f);
            playerZoneLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plShadow = playerZoneLabel.AddComponent<Shadow>();
            plShadow.effectColor = new Color(0, 0, 0, 0.95f);
            plShadow.effectDistance = new Vector2(1.2f, -1.2f);
            var plOutline = playerZoneLabel.AddComponent<Outline>();
            plOutline.effectColor = new Color(0.08f, 0.18f, 0.45f, 0.5f);
            plOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var enemyZoneLabel = AddText(gridContainer, "EnemyZone", "ENEMY ZONE", 14, TextAnchor.MiddleCenter);
            SetAnchors(enemyZoneLabel, 0.57f, 0.955f, 1f, 1f);
            enemyZoneLabel.GetComponent<Text>().color = new Color(1f, 0.55f, 0.45f, 0.90f);
            enemyZoneLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var enShadow = enemyZoneLabel.AddComponent<Shadow>();
            enShadow.effectColor = new Color(0, 0, 0, 0.95f);
            enShadow.effectDistance = new Vector2(1.2f, -1.2f);
            var enOutline = enemyZoneLabel.AddComponent<Outline>();
            enOutline.effectColor = new Color(0.45f, 0.10f, 0.08f, 0.5f);
            enOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // === ENERGY DISPLAY — ornate panel, bigger, polished ===
            var energyPanel = AddPanel(canvas, "EnergyPanel", new Color(0.06f, 0.05f, 0.12f, 0.96f));
            SetAnchors(energyPanel, 0.01f, 0.20f, 0.20f, 0.35f);
            if (ornateSpr != null) { energyPanel.GetComponent<Image>().sprite = ornateSpr; energyPanel.GetComponent<Image>().type = Image.Type.Sliced; energyPanel.GetComponent<Image>().color = new Color(0.52f, 0.55f, 0.65f, 1f); }
            else { AddOutlinePanel(energyPanel, SkyDim); }
            // Blue glow behind panel
            var enPanelGlow = AddPanel(energyPanel, "PanelGlow", new Color(0.18f, 0.38f, 0.72f, 0.18f));
            StretchToParent(enPanelGlow);
            enPanelGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            var enGlass = AddPanel(energyPanel, "GlassTop", new Color(0.35f, 0.50f, 0.70f, 0.15f));
            SetAnchors(enGlass, 0.05f, 0.86f, 0.95f, 0.97f);
            enGlass.AddComponent<LayoutElement>().ignoreLayout = true;

            var enLabel = AddText(energyPanel, "EnergyLabel", "ENERGY", 12, TextAnchor.MiddleCenter);
            SetAnchors(enLabel, 0f, 0.68f, 1f, 0.95f);
            enLabel.GetComponent<Text>().color = Sky;
            enLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var enlShadow = enLabel.AddComponent<Shadow>();
            enlShadow.effectColor = new Color(0, 0, 0, 0.85f);
            enlShadow.effectDistance = new Vector2(1f, -1f);
            var enlOutline = enLabel.AddComponent<Outline>();
            enlOutline.effectColor = new Color(0.10f, 0.25f, 0.55f, 0.4f);
            enlOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var orbRow = AddPanel(energyPanel, "OrbRow", new Color(0, 0, 0, 0));
            SetAnchors(orbRow, 0.04f, 0.18f, 0.96f, 0.65f);
            var orbLayout = orbRow.AddComponent<HorizontalLayoutGroup>();
            orbLayout.spacing = 6;
            orbLayout.childAlignment = TextAnchor.MiddleCenter;

            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            for (int i = 0; i < 4; i++)
            {
                bool filled = i < 3;
                var orbContainer = AddPanel(orbRow, $"OrbC_{i}", new Color(0, 0, 0, 0));
                orbContainer.AddComponent<LayoutElement>().preferredWidth = 28;
                // Glow behind filled orbs — bright blue halo
                if (filled)
                {
                    var orbGlow = AddPanel(orbContainer, "Glow", new Color(0.20f, 0.50f, 0.90f, 0.30f));
                    StretchToParent(orbGlow);
                    orbGlow.AddComponent<LayoutElement>().ignoreLayout = true;
                }
                var orb = AddPanel(orbContainer, $"Orb_{i}", filled ? BarEnergy : BarEnergyDim);
                SetAnchors(orb, 0.06f, 0.06f, 0.94f, 0.94f);
                if (circleSpr != null)
                {
                    orb.GetComponent<Image>().sprite = circleSpr;
                    orb.GetComponent<Image>().type = Image.Type.Sliced;
                    orb.GetComponent<Image>().color = filled ? new Color(0.30f, 0.72f, 1f, 1f) : new Color(0.10f, 0.10f, 0.18f, 0.60f);
                }
                if (filled) { AddOutlinePanel(orb, new Color(0.50f, 0.78f, 1f, 0.50f)); }
                else { AddOutlinePanel(orb, new Color(0.35f, 0.30f, 0.50f, 0.45f)); }
            }

            var enText = AddText(energyPanel, "EnergyCount", "3 / 4", 13, TextAnchor.MiddleCenter);
            SetAnchors(enText, 0f, 0.0f, 1f, 0.22f);
            enText.GetComponent<Text>().color = TextWhite;
            enText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var enTSh = enText.AddComponent<Shadow>(); enTSh.effectColor = new Color(0, 0, 0, 0.8f); enTSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === COMBO COUNTER — floating indicator below energy ===
            var comboPanel = AddPanel(canvas, "ComboCounter", new Color(0.06f, 0.04f, 0.10f, 0.90f));
            SetAnchors(comboPanel, 0.03f, 0.14f, 0.18f, 0.20f);
            AddOutlinePanel(comboPanel, new Color(0.85f, 0.55f, 0.15f, 0.55f));
            var comboLabel = AddText(comboPanel, "Label", "\u2B50 COMBO x3", 12, TextAnchor.MiddleCenter);
            StretchToParent(comboLabel);
            comboLabel.GetComponent<Text>().color = new Color(1f, 0.85f, 0.35f, 0.95f);
            comboLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var cmbSh = comboLabel.AddComponent<Shadow>();
            cmbSh.effectColor = new Color(0, 0, 0, 0.85f);
            cmbSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === CARD HAND — ornate card tray ===
            var cardTray = AddPanel(canvas, "CardTray", new Color(0.06f, 0.04f, 0.10f, 0.97f));
            SetAnchors(cardTray, 0.08f, 0f, 0.87f, 0.20f);
            if (ornateSpr != null) { cardTray.GetComponent<Image>().sprite = ornateSpr; cardTray.GetComponent<Image>().type = Image.Type.Sliced; cardTray.GetComponent<Image>().color = new Color(0.68f, 0.62f, 0.52f, 1f); }
            // Warm inner fill for card contrast
            var ctInnerWarm = AddPanel(cardTray, "InnerWarm", new Color(0.12f, 0.08f, 0.16f, 0.40f));
            SetAnchors(ctInnerWarm, 0.02f, 0.02f, 0.98f, 0.98f);
            ctInnerWarm.AddComponent<LayoutElement>().ignoreLayout = true;
            // Top gold border + glow for card tray
            var ctBorder = AddPanel(cardTray, "TopBorder", new Color(0.88f, 0.70f, 0.28f, 0.85f));
            SetAnchors(ctBorder, 0.01f, 0.97f, 0.99f, 1f);
            ctBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            var ctGlow = AddPanel(cardTray, "TopGlow", new Color(0.60f, 0.45f, 0.18f, 0.18f));
            SetAnchors(ctGlow, 0f, 0.88f, 1f, 0.97f);
            ctGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            // "HAND" label at top of tray
            var handLabel = AddText(cardTray, "HandLabel", "HAND", 11, TextAnchor.MiddleCenter);
            SetAnchors(handLabel, 0.40f, 0.89f, 0.60f, 1f);
            handLabel.GetComponent<Text>().color = new Color(0.85f, 0.68f, 0.30f, 0.80f);
            handLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var handSh = handLabel.AddComponent<Shadow>();
            handSh.effectColor = new Color(0, 0, 0, 0.85f);
            handSh.effectDistance = new Vector2(0.5f, -0.5f);
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

            // Deck remaining counter — left of card tray
            var deckCountBg = AddPanel(canvas, "DeckCount", new Color(0.06f, 0.04f, 0.10f, 0.88f));
            SetAnchors(deckCountBg, 0.01f, 0.06f, 0.08f, 0.18f);
            AddOutlinePanel(deckCountBg, new Color(0.55f, 0.42f, 0.18f, 0.45f));
            var deckCountNum = AddText(deckCountBg, "Num", "12", 14, TextAnchor.MiddleCenter);
            SetAnchors(deckCountNum, 0f, 0.30f, 1f, 0.95f);
            deckCountNum.GetComponent<Text>().color = new Color(0.85f, 0.78f, 0.60f, 0.90f);
            deckCountNum.GetComponent<Text>().fontStyle = FontStyle.Bold;
            deckCountNum.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            deckCountNum.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);
            var deckCountLabel = AddText(deckCountBg, "Label", "DECK", 10, TextAnchor.MiddleCenter);
            SetAnchors(deckCountLabel, 0f, 0f, 1f, 0.35f);
            deckCountLabel.GetComponent<Text>().color = new Color(0.55f, 0.50f, 0.40f, 0.70f);
            deckCountLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Discard pile counter — right of end turn
            var discardBg = AddPanel(canvas, "DiscardPile", new Color(0.06f, 0.04f, 0.10f, 0.88f));
            SetAnchors(discardBg, 0.91f, 0.22f, 0.99f, 0.30f);
            AddOutlinePanel(discardBg, new Color(0.45f, 0.35f, 0.18f, 0.35f));
            var discardNum = AddText(discardBg, "Num", "8", 13, TextAnchor.MiddleCenter);
            SetAnchors(discardNum, 0f, 0.30f, 1f, 0.95f);
            discardNum.GetComponent<Text>().color = new Color(0.70f, 0.55f, 0.45f, 0.80f);
            discardNum.GetComponent<Text>().fontStyle = FontStyle.Bold;
            discardNum.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            discardNum.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);
            var discardLabel = AddText(discardBg, "Label", "DISC", 10, TextAnchor.MiddleCenter);
            SetAnchors(discardLabel, 0f, 0f, 1f, 0.35f);
            discardLabel.GetComponent<Text>().color = new Color(0.50f, 0.42f, 0.35f, 0.65f);
            discardLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === END TURN — HUGE fiery button, impossible to miss ===
            // Outer red halo
            var endTurnGlow3 = AddPanel(canvas, "EndTurnGlow3", new Color(0.85f, 0.15f, 0.08f, 0.06f));
            SetAnchors(endTurnGlow3, 0.78f, 0f, 1f, 0.28f);
            var endTurnGlow2 = AddPanel(canvas, "EndTurnGlow2", new Color(0.90f, 0.30f, 0.10f, 0.12f));
            SetAnchors(endTurnGlow2, 0.82f, 0f, 1f, 0.25f);
            var endTurnGlow = AddPanel(canvas, "EndTurnGlow", new Color(0.95f, 0.40f, 0.12f, 0.20f));
            SetAnchors(endTurnGlow, 0.84f, 0.01f, 1f, 0.23f);
            var endTurnBtn = AddPanel(canvas, "EndTurnButton", Blood);
            SetAnchors(endTurnBtn, 0.85f, 0.02f, 0.99f, 0.21f);
            if (btnOrnateSpr != null) { endTurnBtn.GetComponent<Image>().sprite = btnOrnateSpr; endTurnBtn.GetComponent<Image>().type = Image.Type.Sliced; endTurnBtn.GetComponent<Image>().color = new Color(0.88f, 0.28f, 0.18f, 1f); }
            else { AddOutlinePanel(endTurnBtn, new Color(1f, 0.55f, 0.25f, 0.50f)); }
            SetButtonFeedback(endTurnBtn.AddComponent<Button>());
            AddSceneNav(endTurnBtn, SceneName.Combat);
            // Inner warmth + glass highlight
            var etInnerDark = AddPanel(endTurnBtn, "InnerDark", new Color(0.12f, 0.04f, 0.03f, 0.40f));
            SetAnchors(etInnerDark, 0.08f, 0.08f, 0.92f, 0.92f);
            etInnerDark.AddComponent<LayoutElement>().ignoreLayout = true;
            var etInnerGlow = AddPanel(endTurnBtn, "InnerGlow", new Color(1f, 0.60f, 0.25f, 0.22f));
            SetAnchors(etInnerGlow, 0.1f, 0.55f, 0.9f, 0.92f);
            etInnerGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            var etLabel = AddText(endTurnBtn, "Label", "END\nTURN", 16, TextAnchor.MiddleCenter);
            StretchToParent(etLabel);
            etLabel.GetComponent<Text>().color = Color.white;
            etLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var etShadow = etLabel.AddComponent<Shadow>();
            etShadow.effectColor = new Color(0, 0, 0, 0.98f);
            etShadow.effectDistance = new Vector2(2f, -2f);
            var etOutline = etLabel.AddComponent<Outline>();
            etOutline.effectColor = new Color(0.90f, 0.25f, 0.12f, 0.6f);
            etOutline.effectDistance = new Vector2(1f, -1f);

            // === VICTORY PANEL — ornate frame ===
            var victoryPanel = AddPanel(canvasGo, "VictoryPanel", new Color(0.02f, 0.06f, 0.02f, 0.95f));
            StretchToParent(victoryPanel);
            var vicFrame = AddPanel(victoryPanel, "Frame", new Color(0.05f, 0.04f, 0.10f, 0.95f));
            SetAnchors(vicFrame, 0.12f, 0.25f, 0.88f, 0.75f);
            if (ornateSpr != null) { vicFrame.GetComponent<Image>().sprite = ornateSpr; vicFrame.GetComponent<Image>().type = Image.Type.Sliced; vicFrame.GetComponent<Image>().color = new Color(0.75f, 0.72f, 0.60f, 1f); }
            else { AddOutlinePanel(vicFrame, Gold); }
            // Victory star crown
            var vicStarGlow = AddPanel(vicFrame, "StarGlow", new Color(0.90f, 0.75f, 0.30f, 0.12f));
            SetAnchors(vicStarGlow, 0.30f, 0.78f, 0.70f, 0.98f);
            var vicStar = AddText(vicFrame, "Star", "\u2605 \u2605 \u2605", 22, TextAnchor.MiddleCenter);
            SetAnchors(vicStar, 0.25f, 0.82f, 0.75f, 0.96f);
            vicStar.GetComponent<Text>().color = new Color(1f, 0.88f, 0.35f, 0.90f);
            vicStar.AddComponent<Shadow>().effectColor = new Color(0.50f, 0.35f, 0.10f, 0.80f);
            vicStar.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            var vicTitle = AddText(vicFrame, "Title", "VICTORY", 48, TextAnchor.MiddleCenter);
            SetAnchors(vicTitle, 0.1f, 0.58f, 0.9f, 0.82f);
            vicTitle.GetComponent<Text>().color = Gold;
            vicTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(vicTitle, new Color(0.3f, 0.2f, 0.05f), 2f);
            var vicShadow = vicTitle.AddComponent<Shadow>();
            vicShadow.effectColor = new Color(0, 0, 0, 0.9f);
            vicShadow.effectDistance = new Vector2(3f, -3f);
            var vicSep = AddPanel(vicFrame, "Sep", new Color(0.20f, 0.18f, 0.30f, 0.5f));
            SetAnchors(vicSep, 0.08f, 0.555f, 0.92f, 0.56f);
            var vicRewards = AddText(vicFrame, "Rewards", "+250 XP   +500 Gold   +3 Hero Shards", 14, TextAnchor.MiddleCenter);
            SetAnchors(vicRewards, 0.1f, 0.35f, 0.9f, 0.55f);
            vicRewards.GetComponent<Text>().color = TextLight;
            var vrSh = vicRewards.AddComponent<Shadow>();
            vrSh.effectColor = new Color(0, 0, 0, 0.7f);
            vrSh.effectDistance = new Vector2(1f, -1f);
            var vicContinue = AddPanel(vicFrame, "ContinueBtn", Gold);
            SetAnchors(vicContinue, 0.28f, 0.06f, 0.72f, 0.25f);
            if (btnOrnateSpr != null) { vicContinue.GetComponent<Image>().sprite = btnOrnateSpr; vicContinue.GetComponent<Image>().type = Image.Type.Sliced; vicContinue.GetComponent<Image>().color = new Color(0.82f, 0.65f, 0.25f, 1f); }
            SetButtonFeedback(vicContinue.AddComponent<Button>());
            AddSceneNav(vicContinue, SceneName.Empire);
            var vcLabel = AddText(vicContinue, "Label", "CONTINUE", 14, TextAnchor.MiddleCenter);
            StretchToParent(vcLabel);
            vcLabel.GetComponent<Text>().color = Color.white;
            vcLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vcSh = vcLabel.AddComponent<Shadow>();
            vcSh.effectColor = new Color(0, 0, 0, 0.8f);
            vcSh.effectDistance = new Vector2(1f, -1f);
            victoryPanel.SetActive(false);

            // === DEFEAT PANEL — ornate frame ===
            var defeatPanel = AddPanel(canvasGo, "DefeatPanel", new Color(0.08f, 0.02f, 0.02f, 0.95f));
            StretchToParent(defeatPanel);
            var defFrame = AddPanel(defeatPanel, "Frame", new Color(0.05f, 0.03f, 0.06f, 0.95f));
            SetAnchors(defFrame, 0.12f, 0.25f, 0.88f, 0.75f);
            if (ornateSpr != null) { defFrame.GetComponent<Image>().sprite = ornateSpr; defFrame.GetComponent<Image>().type = Image.Type.Sliced; defFrame.GetComponent<Image>().color = new Color(0.65f, 0.45f, 0.42f, 1f); }
            else { AddOutlinePanel(defFrame, Blood); }
            // Broken sword / skull dramatic visual
            var defSkullGlow = AddPanel(defFrame, "SkullGlow", new Color(0.70f, 0.12f, 0.08f, 0.10f));
            SetAnchors(defSkullGlow, 0.30f, 0.78f, 0.70f, 0.98f);
            var defSkull = AddText(defFrame, "Skull", "\u2620", 28, TextAnchor.MiddleCenter);
            SetAnchors(defSkull, 0.35f, 0.80f, 0.65f, 0.96f);
            defSkull.GetComponent<Text>().color = new Color(0.85f, 0.25f, 0.18f, 0.80f);
            defSkull.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            defSkull.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            var defTitle = AddText(defFrame, "Title", "DEFEAT", 48, TextAnchor.MiddleCenter);
            SetAnchors(defTitle, 0.1f, 0.58f, 0.9f, 0.82f);
            defTitle.GetComponent<Text>().color = Blood;
            defTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            AddOutline(defTitle, new Color(0.3f, 0.05f, 0.05f), 2f);
            var defShadow = defTitle.AddComponent<Shadow>();
            defShadow.effectColor = new Color(0, 0, 0, 0.9f);
            defShadow.effectDistance = new Vector2(3f, -3f);
            var defSep = AddPanel(defFrame, "Sep", new Color(0.20f, 0.18f, 0.30f, 0.5f));
            SetAnchors(defSep, 0.08f, 0.575f, 0.92f, 0.58f);
            var defMsg = AddText(defFrame, "Message", "Your heroes have fallen. Regroup and try again.", 14, TextAnchor.MiddleCenter);
            SetAnchors(defMsg, 0.1f, 0.35f, 0.9f, 0.55f);
            defMsg.GetComponent<Text>().color = TextMid;
            var dmSh = defMsg.AddComponent<Shadow>();
            dmSh.effectColor = new Color(0, 0, 0, 0.7f);
            dmSh.effectDistance = new Vector2(1f, -1f);
            var defRetry = AddPanel(defFrame, "RetryBtn", Blood);
            SetAnchors(defRetry, 0.08f, 0.06f, 0.48f, 0.25f);
            if (btnOrnateSpr != null) { defRetry.GetComponent<Image>().sprite = btnOrnateSpr; defRetry.GetComponent<Image>().type = Image.Type.Sliced; defRetry.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.15f, 1f); }
            SetButtonFeedback(defRetry.AddComponent<Button>());
            AddSceneNav(defRetry, SceneName.Combat);
            var drLabel = AddText(defRetry, "Label", "RETRY", 14, TextAnchor.MiddleCenter);
            StretchToParent(drLabel);
            drLabel.GetComponent<Text>().color = Color.white;
            drLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var drSh = drLabel.AddComponent<Shadow>();
            drSh.effectColor = new Color(0, 0, 0, 0.8f);
            drSh.effectDistance = new Vector2(1f, -1f);
            var defQuit = AddPanel(defFrame, "QuitBtn", new Color(0.3f, 0.25f, 0.2f));
            SetAnchors(defQuit, 0.52f, 0.06f, 0.92f, 0.25f);
            if (btnOrnateSpr != null) { defQuit.GetComponent<Image>().sprite = btnOrnateSpr; defQuit.GetComponent<Image>().type = Image.Type.Sliced; defQuit.GetComponent<Image>().color = new Color(0.45f, 0.38f, 0.30f, 1f); }
            SetButtonFeedback(defQuit.AddComponent<Button>());
            AddSceneNav(defQuit, SceneName.Empire);
            var dqLabel = AddText(defQuit, "Label", "RETREAT", 14, TextAnchor.MiddleCenter);
            StretchToParent(dqLabel);
            dqLabel.GetComponent<Text>().color = TextLight;
            dqLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var dqSh = dqLabel.AddComponent<Shadow>();
            dqSh.effectColor = new Color(0, 0, 0, 0.8f);
            dqSh.effectDistance = new Vector2(1f, -1f);
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
            // Sky gradient at top — very subtle dark atmospheric effect (NOT purple)
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.04f, 0.03f, 0.06f, 0.25f));
            SetAnchors(skyGrad, 0f, 0.85f, 1f, 1f);
            // Ground gradient at bottom — darker for depth
            var groundGrad = AddPanel(bg, "GroundGradient", new Color(0.02f, 0.01f, 0.03f, 0.5f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.25f);
            // Vignette left edge
            var vigLeft = AddPanel(bg, "VignetteL", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigLeft, 0f, 0f, 0.08f, 1f);
            // Vignette right edge
            var vigRight = AddPanel(bg, "VignetteR", new Color(0.01f, 0.01f, 0.02f, 0.3f));
            SetAnchors(vigRight, 0.92f, 0f, 1f, 1f);

            // === NEXUS GLOW — single very subtle warm accent behind upper city ===
            var nexusGlow = AddPanel(bg, "NexusGlow", new Color(0.40f, 0.20f, 0.15f, 0.04f));
            SetAnchors(nexusGlow, 0.30f, 0.60f, 0.70f, 0.78f);
            nexusGlow.GetComponent<Image>().raycastTarget = false;
            // Ground fog — single subtle dark layer for depth
            var fogLayer = AddPanel(bg, "FogLayer", new Color(0.05f, 0.03f, 0.08f, 0.06f));
            SetAnchors(fogLayer, 0f, 0.15f, 1f, 0.35f);
            fogLayer.GetComponent<Image>().raycastTarget = false;
            // Additional atmospheric light particles — scattered across mid-screen
            float[] sparkX = { 0.08f, 0.15f, 0.42f, 0.58f, 0.85f, 0.92f, 0.32f, 0.72f, 0.50f, 0.18f, 0.68f, 0.38f };
            float[] sparkY = { 0.45f, 0.52f, 0.48f, 0.42f, 0.50f, 0.38f, 0.55f, 0.40f, 0.35f, 0.42f, 0.52f, 0.38f };
            for (int s = 0; s < sparkX.Length; s++)
            {
                float sa = s % 3 == 0 ? 0.15f : (s % 3 == 1 ? 0.10f : 0.07f);
                Color sparkColor = s % 2 == 0 ? new Color(0.90f, 0.70f, 0.30f, sa) : new Color(0.95f, 0.75f, 0.35f, sa * 0.6f);
                var spark = AddPanel(bg, $"Spark_{s}", sparkColor);
                float sz = s % 3 == 0 ? 0.012f : 0.008f;
                SetAnchors(spark, sparkX[s], sparkY[s], sparkX[s] + sz, sparkY[s] + sz * 0.8f);
                spark.GetComponent<Image>().raycastTarget = false;
            }

            // Notch/Dynamic Island fill — dark bar that extends above safe area
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.06f, 0.04f, 0.10f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);
            var notchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.55f));
            SetAnchors(notchBorder, 0f, 0f, 1f, 0.012f);

            // Dark strip behind home indicator (outside safe area, full screen) — must be BEFORE SafeArea so it renders behind
            var navBarBg = AddPanel(canvasGo, "NavBarBg", new Color(0.04f, 0.03f, 0.06f, 1f));
            SetAnchors(navBarBg, 0f, 0f, 1f, 0.06f);

            // Safe area for all interactive UI
            var canvas = CreateSafeArea(canvasGo);

            // === RESOURCE BAR — solid dark bg matching nav bar, taller ===
            var resBarBg = AddPanel(canvas, "ResourceBarBg", new Color(0.10f, 0.07f, 0.16f, 1f));
            SetAnchors(resBarBg, 0f, 0.950f, 1f, 0.998f);
            // Gold bottom border (mirrors nav bar's top border)
            var resBarBorder = AddPanel(resBarBg, "BottomBorder", new Color(0.92f, 0.74f, 0.32f, 1f));
            SetAnchors(resBarBorder, 0f, 0f, 1f, 0.03f);
            resBarBorder.AddComponent<LayoutElement>().ignoreLayout = true;

            // Layout container — equal spacing across full width
            var resBar = AddPanel(canvas, "ResourceBar", new Color(0, 0, 0, 0));
            SetAnchors(resBar, 0f, 0.952f, 1f, 0.996f);

            var resLayout = resBar.AddComponent<HorizontalLayoutGroup>();
            resLayout.spacing = 0;
            resLayout.padding = new RectOffset(6, 6, 2, 2);
            resLayout.childAlignment = TextAnchor.MiddleCenter;
            resLayout.childControlWidth = true;
            resLayout.childControlHeight = true;
            resLayout.childForceExpandWidth = false;
            resLayout.childForceExpandHeight = false;

            // Resources equally spaced with | dividers
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

            // "+" button — emerald green circular button with plus icon
            var plusBtn = AddPanel(resBar, "AddBtn", new Color(0.20f, 0.62f, 0.32f, 1f));
            var plusLE = plusBtn.AddComponent<LayoutElement>();
            plusLE.preferredWidth = 36;
            plusLE.preferredHeight = 36;
            plusLE.minWidth = 30;
            plusLE.minHeight = 30;
            plusLE.flexibleWidth = 0;
            var plusRoundSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (plusRoundSpr != null)
            {
                plusBtn.GetComponent<Image>().sprite = plusRoundSpr;
                plusBtn.GetComponent<Image>().type = Image.Type.Sliced;
                plusBtn.GetComponent<Image>().color = new Color(0.22f, 0.68f, 0.35f, 1f);
            }
            SetButtonFeedback(plusBtn.AddComponent<Button>());
            AddSceneNav(plusBtn, SceneName.Empire);
            // Plus symbol — bold white
            var plusText = AddText(plusBtn, "Label", "+", 16, TextAnchor.MiddleCenter);
            StretchToParent(plusText);
            plusText.GetComponent<Text>().color = Color.white;
            plusText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var plusShadow = plusText.AddComponent<Shadow>();
            plusShadow.effectColor = new Color(0, 0, 0, 0.7f);
            plusShadow.effectDistance = new Vector2(1f, -1f);

            // === PLAYER AVATAR BLOCK — same width as build queue (0.01–0.18) ===
            var avatarBlock = AddPanel(canvas, "AvatarBlock", new Color(0.08f, 0.05f, 0.14f, 0.96f));
            SetAnchors(avatarBlock, 0.01f, 0.870f, 0.18f, 0.945f);
            // Use ornate frame for premium look (matches event buttons)
            var avatarOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
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
            var lvlText = AddText(lvlBadge, "Level", "Lv.42", 10, TextAnchor.MiddleCenter);
            StretchToParent(lvlText);
            lvlText.GetComponent<Text>().color = TextWhite;
            lvlText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lvlShadow = lvlText.AddComponent<Shadow>();
            lvlShadow.effectColor = new Color(0, 0, 0, 0.7f);
            lvlShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === INFO PANEL — right of avatar, dark panel with gold border ===
            var infoPanelBg = AddPanel(canvas, "InfoPanelBg", new Color(0.10f, 0.07f, 0.16f, 0.85f));
            SetAnchors(infoPanelBg, 0.19f, 0.875f, 0.88f, 0.945f);
            AddOutlinePanel(infoPanelBg, new Color(0.82f, 0.65f, 0.28f, 0.70f));

            // === TOP ROW: VIP badge (left) + Name ===
            // VIP badge — purple pill, top-left
            var vipBadge = AddPanel(infoPanelBg, "VipBadge", new Color(0.55f, 0.18f, 0.78f, 1f));
            SetAnchors(vipBadge, 0.02f, 0.60f, 0.17f, 0.92f);
            AddOutlinePanel(vipBadge, new Color(0.80f, 0.60f, 0.28f, 0.70f));
            var vipText = AddText(vipBadge, "Label", "VIP 11", 13, TextAnchor.MiddleCenter);
            StretchToParent(vipText);
            vipText.GetComponent<Text>().color = Color.white;
            vipText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var vipShadow = vipText.AddComponent<Shadow>();
            vipShadow.effectColor = new Color(0, 0, 0, 0.8f);
            vipShadow.effectDistance = new Vector2(1f, -1f);

            // Player name — right of VIP badge
            var avatarName = AddText(infoPanelBg, "PlayerName", "Commander", 20, TextAnchor.MiddleLeft);
            SetAnchors(avatarName, 0.18f, 0.52f, 0.70f, 0.96f);
            avatarName.GetComponent<Text>().color = new Color(0.98f, 0.90f, 0.55f, 1f);
            avatarName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nameShadow = avatarName.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1f, -1f);

            // Thin gold midline
            var infoMidLine = AddPanel(infoPanelBg, "MidLine", new Color(0.82f, 0.65f, 0.28f, 0.30f));
            SetAnchors(infoMidLine, 0.03f, 0.48f, 0.97f, 0.52f);

            // === BOTTOM ROW: Power icon (sprite) | Server | Coordinates ===
            var powerIcon = AddPanel(infoPanelBg, "PowerIcon", Color.white);
            SetAnchors(powerIcon, 0.02f, 0.06f, 0.08f, 0.46f);
            var powerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_pvp.png");
            if (powerSpr != null)
            {
                powerIcon.GetComponent<Image>().sprite = powerSpr;
                powerIcon.GetComponent<Image>().preserveAspect = true;
                powerIcon.GetComponent<Image>().color = new Color(0.98f, 0.82f, 0.38f, 1f);
            }

            var powerVal = AddText(infoPanelBg, "PowerValue", "355.6M", 18, TextAnchor.MiddleLeft);
            SetAnchors(powerVal, 0.08f, 0.04f, 0.35f, 0.48f);
            powerVal.GetComponent<Text>().color = Color.white;
            powerVal.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pvShadow = powerVal.AddComponent<Shadow>();
            pvShadow.effectColor = new Color(0, 0, 0, 0.9f);
            pvShadow.effectDistance = new Vector2(1f, -1f);

            // Server tag
            var serverTag = AddText(infoPanelBg, "ServerTag", "S:142", 14, TextAnchor.MiddleCenter);
            SetAnchors(serverTag, 0.36f, 0.04f, 0.52f, 0.48f);
            serverTag.GetComponent<Text>().color = new Color(0.60f, 0.56f, 0.50f, 0.90f);
            serverTag.GetComponent<Text>().fontStyle = FontStyle.Italic;

            // Coordinates
            var coordText = AddText(infoPanelBg, "Coords", "K:12 (482, 317)", 14, TextAnchor.MiddleRight);
            SetAnchors(coordText, 0.55f, 0.04f, 0.97f, 0.48f);
            coordText.GetComponent<Text>().color = new Color(0.72f, 0.68f, 0.60f, 0.90f);
            var coordShadow = coordText.AddComponent<Shadow>();
            coordShadow.effectColor = new Color(0, 0, 0, 0.7f);
            coordShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // === LEFT SIDEBAR — Build/Research/Training queue (square icons stacked vertically) ===
            var leftSidebar = AddPanel(canvas, "LeftSidebar", new Color(0, 0, 0, 0));
            SetAnchors(leftSidebar, 0.01f, 0.48f, 0.14f, 0.860f);

            // 4 square slots stacked with small gaps (each ~23.5% of sidebar height)
            float qSlotH = 0.235f;
            float qGap = 0.02f;
            float qTop = 1.0f;
            AddQueueSlot(leftSidebar, "BuildSlot1", "Build", "2:34:15", Ember, true, qTop - qSlotH, qTop);
            qTop -= qSlotH + qGap;
            AddQueueSlot(leftSidebar, "BuildSlot2", "Build", "IDLE", EmberDim, false, qTop - qSlotH, qTop);
            qTop -= qSlotH + qGap;
            AddQueueSlot(leftSidebar, "ResearchSlot", "Research", "IDLE", Sky, false, qTop - qSlotH, qTop);
            qTop -= qSlotH + qGap;
            AddQueueSlot(leftSidebar, "TrainingSlot", "Training", "IDLE", Purple, false, qTop - qSlotH, qTop);

            // === RIGHT SIDEBAR — Event buttons (P&C: compact, nearly square, tight stacking) ===
            float rbX0 = 0.89f, rbX1 = 0.995f; // 10.5% wide (P&C-style compact)
            float rbH = 0.055f; // ~5.5% tall each (smaller for more city space)
            float rbGap = 0.004f;
            float rbTop = 0.940f;
            AddEventButton(canvas, "EventBtn1", "Events", Ember,   rbX0, rbTop - rbH, rbX1, rbTop, "10:04:49", SceneName.Lobby);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn2", "VS", new Color(0.4f, 0.3f, 0.8f, 1f), rbX0, rbTop - rbH, rbX1, rbTop, "2:15:33", SceneName.Combat);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn3", "Rewards", Gold,   rbX0, rbTop - rbH, rbX1, rbTop, "05:42:18", SceneName.Lobby);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn4", "Offer", Blood,    rbX0, rbTop - rbH, rbX1, rbTop, "1d 14:22", SceneName.Lobby);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn5", "Gifts", Purple,   rbX0, rbTop - rbH, rbX1, rbTop, "23:59:52", SceneName.Lobby);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn6", "Shop", Teal,      rbX0, rbTop - rbH, rbX1, rbTop, "6:33:45", SceneName.Lobby);
            rbTop -= rbH + rbGap;
            AddEventButton(canvas, "EventBtn7", "Arena", new Color(0.6f, 0.25f, 0.15f, 1f), rbX0, rbTop - rbH, rbX1, rbTop, "03:28:11", SceneName.Combat);

            // === CHAT BAR — full-width minimal icon bar, taps to open Alliance chat ===
            var chatBar = AddPanel(canvas, "ChatBar", new Color(0.08f, 0.06f, 0.14f, 0.90f));
            SetAnchors(chatBar, 0f, 0.142f, 1f, 0.185f);
            // Gold top border
            var chatTopBorder = AddPanel(chatBar, "TopBorder", new Color(0.80f, 0.62f, 0.24f, 0.60f));
            SetAnchors(chatTopBorder, 0f, 0.90f, 1f, 1f);
            chatTopBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            // Entire bar is tappable → opens Alliance/Chat scene
            SetButtonFeedback(chatBar.AddComponent<Button>());
            AddSceneNav(chatBar, SceneName.Alliance);

            // NavChat icon — shows alliance shield if in alliance chat, speech bubble for realm/server
            var chatNavIcon = AddPanel(chatBar, "NavChatIcon", new Color(0, 0, 0, 0));
            SetAnchors(chatNavIcon, 0.02f, 0.10f, 0.08f, 0.88f);
            // Load alliance icon (swap at runtime based on last active channel)
            var chatIconSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_chat.png");
            if (chatIconSpr != null) { chatNavIcon.GetComponent<Image>().sprite = chatIconSpr; chatNavIcon.GetComponent<Image>().preserveAspect = true; chatNavIcon.GetComponent<Image>().color = new Color(0.85f, 0.70f, 0.30f, 0.90f); }
            else
            {
                // Fallback: emoji chat icon
                var chatIconText = AddText(chatNavIcon, "Icon", "\uD83D\uDCAC", 16, TextAnchor.MiddleCenter);
                StretchToParent(chatIconText);
                chatIconText.GetComponent<Text>().color = new Color(0.85f, 0.70f, 0.30f, 0.90f);
            }

            // Latest message preview — scrolling ticker text
            var chatMsgArea = AddPanel(chatBar, "MessageArea", new Color(0, 0, 0, 0));
            SetAnchors(chatMsgArea, 0.09f, 0.08f, 0.92f, 0.92f);
            var chatMsg = AddText(chatMsgArea, "Message", "<color=#2EC7A6>[Alliance]</color> NBAHeartless: Rally at Lv.17 Monster!", 11, TextAnchor.MiddleLeft);
            StretchToParent(chatMsg);
            chatMsg.GetComponent<Text>().color = new Color(0.78f, 0.75f, 0.68f, 0.88f);
            chatMsg.GetComponent<Text>().supportRichText = true;
            var chatMsgShadow = chatMsg.AddComponent<Shadow>();
            chatMsgShadow.effectColor = new Color(0, 0, 0, 0.8f);
            chatMsgShadow.effectDistance = new Vector2(1f, -1f);

            // Unread notification dot — right side
            var chatUnread = AddPanel(chatBar, "UnreadDot", new Color(0.92f, 0.15f, 0.10f, 1f));
            SetAnchors(chatUnread, 0.94f, 0.30f, 0.97f, 0.70f);
            var roundSprChat = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (roundSprChat != null) { chatUnread.GetComponent<Image>().sprite = roundSprChat; chatUnread.GetComponent<Image>().type = Image.Type.Sliced; chatUnread.GetComponent<Image>().color = new Color(0.92f, 0.15f, 0.10f, 1f); }

            // === UPGRADE BANNER (above nav) — bright gold banner like P&C ===
            var upgradeBanner = AddPanel(canvas, "UpgradeBanner", new Color(0.28f, 0.20f, 0.08f, 0.96f));
            SetAnchors(upgradeBanner, 0.03f, 0.102f, 0.97f, 0.14f);
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (btnOrnateSpr != null) { upgradeBanner.GetComponent<Image>().sprite = btnOrnateSpr; upgradeBanner.GetComponent<Image>().type = Image.Type.Sliced; upgradeBanner.GetComponent<Image>().color = new Color(0.88f, 0.78f, 0.55f, 1f); }
            else { AddOutlinePanel(upgradeBanner, new Color(0.70f, 0.55f, 0.22f, 0.9f)); }
            // Inner warm gold fill for banner glow
            var upgInnerFill = AddPanel(upgradeBanner, "InnerFill", new Color(0.35f, 0.25f, 0.10f, 0.4f));
            StretchToParent(upgInnerFill);
            // Top gradient highlight for depth
            var upgGrad = AddPanel(upgradeBanner, "Gradient", new Color(0.40f, 0.30f, 0.12f, 0.3f));
            SetAnchors(upgGrad, 0.02f, 0.50f, 0.98f, 1f);
            // Left gold chevron accent
            var upgLeftArrow = AddPanel(upgradeBanner, "LeftArrow", new Color(0.80f, 0.64f, 0.24f, 0.90f));
            SetAnchors(upgLeftArrow, 0.01f, 0.18f, 0.04f, 0.82f);
            var upgLeftInner = AddPanel(upgradeBanner, "LeftArrowInner", new Color(0.65f, 0.50f, 0.18f, 0.6f));
            SetAnchors(upgLeftInner, 0.045f, 0.25f, 0.06f, 0.75f);
            // Center text with stronger styling
            var upgradeText = AddText(upgradeBanner, "Text", "Upgrade Stronghold to Lv.6", 16, TextAnchor.MiddleCenter);
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
            SetButtonFeedback(upgradeBanner.AddComponent<Button>());
            AddSceneNav(upgradeBanner, SceneName.Empire);

            // === BOTTOM NAV BAR — clean with single gold top border ===
            var navBar = AddPanel(canvas, "BottomNavBar", new Color(0.10f, 0.07f, 0.16f, 1f));
            SetAnchors(navBar, 0f, 0f, 1f, 0.10f);
            // Single gold top border
            var navBorder1 = AddPanel(navBar, "TopBorder1", new Color(0.92f, 0.74f, 0.32f, 1f));
            SetAnchors(navBorder1, 0f, 0.96f, 1f, 1f);
            navBorder1.AddComponent<LayoutElement>().ignoreLayout = true;
            // Upper half lighter for glass depth
            var navHighlight = AddPanel(navBar, "Highlight", new Color(0.16f, 0.11f, 0.24f, 0.50f));
            SetAnchors(navHighlight, 0f, 0.50f, 1f, 0.96f);
            navHighlight.AddComponent<LayoutElement>().ignoreLayout = true;
            // Bottom fade to black
            var navBotFade = AddPanel(navBar, "BotFade", new Color(0.03f, 0.02f, 0.05f, 0.50f));
            SetAnchors(navBotFade, 0f, 0f, 1f, 0.15f);
            navBotFade.AddComponent<LayoutElement>().ignoreLayout = true;

            // === Nav items — 3 left, CENTER raised button, 3 right ===
            var navLayoutLeft = AddPanel(navBar, "NavLeft", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutLeft, 0f, 0.02f, 0.34f, 0.94f);
            var nllLayout = navLayoutLeft.AddComponent<HorizontalLayoutGroup>();
            nllLayout.spacing = 0;
            nllLayout.padding = new RectOffset(4, 4, 4, 6);
            nllLayout.childForceExpandWidth = true;
            nllLayout.childForceExpandHeight = true;
            nllLayout.childAlignment = TextAnchor.MiddleCenter;

            AddNavItem(navLayoutLeft, "NavWorld", "WORLD", Ember, false, 0, SceneName.WorldMap);
            AddNavItem(navLayoutLeft, "NavHero", "HERO", Purple, false, 2, SceneName.Lobby);
            AddNavItem(navLayoutLeft, "NavQuest", "QUEST", Teal, false, 17, SceneName.Lobby);

            // === CENTER BUTTON — raised ornate, no ember glow ===
            // Gold diamond accents at junctions
            var navDiamondL = AddPanel(navBar, "DiamondL", new Color(0.92f, 0.74f, 0.32f, 0.85f));
            SetAnchors(navDiamondL, 0.325f, 0.88f, 0.355f, 1.04f);
            navDiamondL.AddComponent<LayoutElement>().ignoreLayout = true;
            navDiamondL.transform.localRotation = Quaternion.Euler(0, 0, 45);
            var navDiamondR = AddPanel(navBar, "DiamondR", new Color(0.92f, 0.74f, 0.32f, 0.85f));
            SetAnchors(navDiamondR, 0.645f, 0.88f, 0.675f, 1.04f);
            navDiamondR.AddComponent<LayoutElement>().ignoreLayout = true;
            navDiamondR.transform.localRotation = Quaternion.Euler(0, 0, 45);

            // Main button body
            var centerBtn = AddPanel(navBar, "NavCenterBtn", new Color(0.10f, 0.06f, 0.16f, 1f));
            SetAnchors(centerBtn, 0.34f, 0.04f, 0.66f, 1.16f);
            var centerBtnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (centerBtnSpr != null)
            {
                var cImg = centerBtn.GetComponent<Image>();
                cImg.sprite = centerBtnSpr;
                cImg.type = Image.Type.Sliced;
                cImg.color = new Color(0.88f, 0.74f, 0.45f, 1f);
            }
            else { AddOutlinePanel(centerBtn, new Color(0.90f, 0.72f, 0.30f, 0.95f)); }

            // Inner fill — clean dark purple, no ember
            var centerInner = AddPanel(centerBtn, "Inner", new Color(0.06f, 0.03f, 0.10f, 0.96f));
            SetAnchors(centerInner, 0.07f, 0.05f, 0.93f, 0.95f);
            var centerHighlight = AddPanel(centerInner, "Highlight", new Color(0.25f, 0.18f, 0.35f, 0.40f));
            SetAnchors(centerHighlight, 0.04f, 0.60f, 0.96f, 0.96f);

            // Icon — empire castle sprite
            var centerIcon = AddPanel(centerInner, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(centerIcon, 0.10f, 0.20f, 0.90f, 0.86f);
            var empSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_empire.png");
            if (empSpr != null)
            {
                centerIcon.GetComponent<Image>().sprite = empSpr;
                centerIcon.GetComponent<Image>().preserveAspect = true;
                centerIcon.GetComponent<Image>().color = new Color(1f, 0.94f, 0.78f, 1f);
            }
            else { centerIcon.GetComponent<Image>().color = Ember; }

            // "EMPIRE" label
            var centerLabel = AddText(centerInner, "Label", "EMPIRE", 12, TextAnchor.MiddleCenter);
            SetAnchors(centerLabel, 0f, 0.01f, 1f, 0.22f);
            centerLabel.GetComponent<Text>().color = new Color(1f, 0.95f, 0.75f, 1f);
            centerLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var clShadow = centerLabel.AddComponent<Shadow>();
            clShadow.effectColor = new Color(0, 0, 0, 0.98f);
            clShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Crown accent
            var centerCrown1 = AddPanel(centerBtn, "Crown1", new Color(0.94f, 0.76f, 0.32f, 1f));
            SetAnchors(centerCrown1, 0.05f, 0.980f, 0.95f, 1f);
            SetButtonFeedback(centerBtn.AddComponent<Button>());
            AddSceneNav(centerBtn, SceneName.Empire);

            // Right nav items
            var navLayoutRight = AddPanel(navBar, "NavRight", new Color(0, 0, 0, 0));
            SetAnchors(navLayoutRight, 0.66f, 0.02f, 1f, 0.94f);
            var nlrLayout = navLayoutRight.AddComponent<HorizontalLayoutGroup>();
            nlrLayout.spacing = 0;
            nlrLayout.padding = new RectOffset(4, 4, 4, 6);
            nlrLayout.childForceExpandWidth = true;
            nlrLayout.childForceExpandHeight = true;
            nlrLayout.childAlignment = TextAnchor.MiddleCenter;

            AddNavItem(navLayoutRight, "NavBag", "BAG", GoldDim, false, 3, SceneName.Lobby);
            AddNavItem(navLayoutRight, "NavMail", "MAIL", Sky, false, 5, SceneName.Lobby);
            AddNavItem(navLayoutRight, "NavAlliance", "ALLIANCE", TealDim, false, 10, SceneName.Alliance);
            AddNavItem(navLayoutRight, "NavRank", "RANK", EmberDim, false, 0, SceneName.Lobby);

            // === RESOURCE DETAIL POPUP (hidden, full screen overlay) ===
            var resPopup = AddPanel(canvasGo, "ResourceDetailPopup", new Color(0, 0, 0, 0.6f));
            StretchToParent(resPopup);

            var resFrame = AddPanel(resPopup, "Frame", ResBarBg);
            SetAnchors(resFrame, 0.08f, 0.30f, 0.92f, 0.80f);
            var resOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (resOrnateSpr != null) { resFrame.GetComponent<Image>().sprite = resOrnateSpr; resFrame.GetComponent<Image>().type = Image.Type.Sliced; resFrame.GetComponent<Image>().color = new Color(0.65f, 0.58f, 0.48f, 1f); }
            else { AddOutlinePanel(resFrame, GoldDim); }

            // Glass highlight
            var resGlass = AddPanel(resFrame, "GlassTop", new Color(0.20f, 0.18f, 0.28f, 0.15f));
            SetAnchors(resGlass, 0.03f, 0.92f, 0.97f, 0.99f);

            // Header
            var resHeader = AddPanel(resFrame, "Header", new Color(0.08f, 0.10f, 0.18f, 1f));
            SetAnchors(resHeader, 0f, 0.88f, 1f, 1f);
            var resTitle = AddText(resHeader, "Title", "STONE", 18, TextAnchor.MiddleCenter);
            StretchToParent(resTitle);
            resTitle.GetComponent<Text>().color = Gold;
            resTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var rtSh2 = resTitle.AddComponent<Shadow>();
            rtSh2.effectColor = new Color(0, 0, 0, 0.85f);
            rtSh2.effectDistance = new Vector2(1f, -1f);
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
            var rcvSh = resCurrentVal.AddComponent<Shadow>();
            rcvSh.effectColor = new Color(0, 0, 0, 0.7f);
            rcvSh.effectDistance = new Vector2(0.5f, -0.5f);

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
            AddOutlinePanel(resCapBarBg, new Color(0.42f, 0.34f, 0.18f, 0.35f));
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
            var rsrcSh = resSrcTitle.AddComponent<Shadow>();
            rsrcSh.effectColor = new Color(0, 0, 0, 0.8f);
            rsrcSh.effectDistance = new Vector2(0.5f, -0.5f);

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
            AddSceneNav(resCloseBtn, SceneName.Empire);

            resPopup.SetActive(false);

            // === BUILDING INFO POPUP — Premium P&C-quality (hidden, shown on building tap) ===
            var infoPopup = AddPanel(canvasGo, "BuildingInfoPopup", new Color(0, 0, 0, 0));
            StretchToParent(infoPopup);
            var infoOverlay = AddPanel(infoPopup, "Overlay", new Color(0, 0, 0, 0.65f));
            StretchToParent(infoOverlay);
            infoOverlay.AddComponent<Button>();

            // --- Main frame: taller, wider for premium feel ---
            var infoFrame = AddPanel(infoPopup, "Frame", BgPanel);
            SetAnchors(infoFrame, 0.04f, 0.12f, 0.96f, 0.88f);
            var infoOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (infoOrnateSpr != null) { infoFrame.GetComponent<Image>().sprite = infoOrnateSpr; infoFrame.GetComponent<Image>().type = Image.Type.Sliced; infoFrame.GetComponent<Image>().color = new Color(0.70f, 0.58f, 0.38f, 1f); }
            // Triple gold border: outer glow → mid gold → inner edge
            var frameGlowBorder = AddPanel(infoFrame, "FrameGlow", new Color(0.83f, 0.66f, 0.26f, 0.18f));
            SetAnchors(frameGlowBorder, -0.005f, -0.003f, 1.005f, 1.003f);
            var frameOuterBorder = AddPanel(infoFrame, "FrameOuterBorder", new Color(0.83f, 0.66f, 0.26f, 0.65f));
            SetAnchors(frameOuterBorder, 0f, 0f, 1f, 1f);
            frameOuterBorder.AddComponent<Outline>().effectColor = new Color(0.90f, 0.72f, 0.30f, 0.70f);
            frameOuterBorder.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            frameOuterBorder.GetComponent<Image>().raycastTarget = false;

            // Inner fill: deep dark with subtle warm gradient
            var infoInner = AddPanel(infoFrame, "InnerFill", new Color(0.05f, 0.03f, 0.09f, 0.96f));
            SetAnchors(infoInner, 0.015f, 0.012f, 0.985f, 0.988f);
            // Inner warm edge glow (lit from above)
            var innerEdgeTop = AddPanel(infoInner, "InnerEdgeTop", new Color(0.83f, 0.66f, 0.26f, 0.08f));
            SetAnchors(innerEdgeTop, 0f, 0.92f, 1f, 1f);
            innerEdgeTop.GetComponent<Image>().raycastTarget = false;
            var innerEdgeBot = AddPanel(infoInner, "InnerEdgeBot", new Color(0.03f, 0.02f, 0.06f, 0.30f));
            SetAnchors(innerEdgeBot, 0f, 0f, 1f, 0.06f);
            innerEdgeBot.GetComponent<Image>().raycastTarget = false;

            // === HEADER BAND: ornate dark purple with gold accents ===
            var infoHeader = AddPanel(infoInner, "Header", new Color(0.10f, 0.06f, 0.18f, 1f));
            SetAnchors(infoHeader, 0f, 0.89f, 1f, 1f);
            // Header gradient highlight (glass effect from top)
            var headerGlass = AddPanel(infoHeader, "HeaderGlass", new Color(0.40f, 0.30f, 0.55f, 0.12f));
            SetAnchors(headerGlass, 0f, 0.50f, 1f, 1f);
            headerGlass.GetComponent<Image>().raycastTarget = false;
            // Header warmth layer
            var headerWarmth = AddPanel(infoHeader, "HeaderWarmth", new Color(0.83f, 0.55f, 0.18f, 0.06f));
            SetAnchors(headerWarmth, 0.10f, 0.10f, 0.90f, 0.90f);
            headerWarmth.GetComponent<Image>().raycastTarget = false;

            // Building name — large gold with strong glow
            var infoTitle = AddText(infoHeader, "BuildingName", "BARRACKS", 24, TextAnchor.MiddleCenter);
            StretchToParent(infoTitle);
            infoTitle.GetComponent<Text>().color = new Color(1f, 0.88f, 0.52f, 1f); // Bright warm gold
            infoTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var infoTitleOutline = infoTitle.AddComponent<Outline>();
            infoTitleOutline.effectColor = new Color(0.45f, 0.30f, 0.08f, 0.90f);
            infoTitleOutline.effectDistance = new Vector2(1.2f, -1.2f);
            var infoTitleSh = infoTitle.AddComponent<Shadow>();
            infoTitleSh.effectColor = new Color(0, 0, 0, 0.95f);
            infoTitleSh.effectDistance = new Vector2(1.5f, -1.5f);

            // Gold separator line below header
            var infoHeaderBorder = AddPanel(infoInner, "HeaderBorder", new Color(0.83f, 0.66f, 0.26f, 0.70f));
            SetAnchors(infoHeaderBorder, 0.04f, 0.885f, 0.96f, 0.892f);
            infoHeaderBorder.GetComponent<Image>().raycastTarget = false;
            // Subtle glow under separator
            var headerBorderGlow = AddPanel(infoInner, "HeaderBorderGlow", new Color(0.83f, 0.66f, 0.26f, 0.10f));
            SetAnchors(headerBorderGlow, 0.08f, 0.87f, 0.92f, 0.89f);
            headerBorderGlow.GetComponent<Image>().raycastTarget = false;

            // === BUILDING PREVIEW: large left panel with ornate frame + radial glow ===
            // Preview outer glow
            var previewGlow = AddPanel(infoInner, "PreviewGlow", new Color(0.55f, 0.40f, 0.20f, 0.12f));
            SetAnchors(previewGlow, 0.01f, 0.42f, 0.42f, 0.87f);
            previewGlow.GetComponent<Image>().raycastTarget = false;
            var radialSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            if (radialSpr != null) { previewGlow.GetComponent<Image>().sprite = radialSpr; }
            // Preview recessed panel
            var infoPreview = AddPanel(infoInner, "BuildingPreview", new Color(0.06f, 0.04f, 0.12f, 0.90f));
            SetAnchors(infoPreview, 0.03f, 0.44f, 0.40f, 0.86f);
            var previewOutline = infoPreview.AddComponent<Outline>();
            previewOutline.effectColor = new Color(0.72f, 0.56f, 0.22f, 0.65f);
            previewOutline.effectDistance = new Vector2(1.5f, -1.5f);
            // Second border layer
            var previewBorder2 = infoPreview.AddComponent<Shadow>();
            previewBorder2.effectColor = new Color(0.83f, 0.66f, 0.26f, 0.25f);
            previewBorder2.effectDistance = new Vector2(2.5f, -2.5f);
            var previewSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/barracks_t1.png");
            if (previewSpr != null) { infoPreview.GetComponent<Image>().sprite = previewSpr; infoPreview.GetComponent<Image>().preserveAspect = true; infoPreview.GetComponent<Image>().color = Color.white; }
            // Inner vignette on preview
            var previewVignette = AddPanel(infoPreview, "Vignette", new Color(0.03f, 0.02f, 0.06f, 0f));
            StretchToParent(previewVignette);
            previewVignette.GetComponent<Image>().raycastTarget = false;
            previewVignette.AddComponent<Outline>().effectColor = new Color(0.03f, 0.02f, 0.06f, 0.40f);
            previewVignette.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);

            // Level badge: ornate gold shield overlapping preview bottom
            var infoLvlBadge = AddPanel(infoInner, "LevelBadgeFrame", new Color(0.14f, 0.10f, 0.22f, 0.96f));
            SetAnchors(infoLvlBadge, 0.08f, 0.42f, 0.35f, 0.48f);
            var lvlBadgeBorder = infoLvlBadge.AddComponent<Outline>();
            lvlBadgeBorder.effectColor = new Color(0.83f, 0.66f, 0.26f, 0.80f);
            lvlBadgeBorder.effectDistance = new Vector2(1.2f, -1.2f);
            // Badge inner gold bar
            var lvlBadgeInner = AddPanel(infoLvlBadge, "BadgeInner", new Color(0.83f, 0.66f, 0.26f, 0.15f));
            SetAnchors(lvlBadgeInner, 0.04f, 0.10f, 0.96f, 0.90f);
            lvlBadgeInner.GetComponent<Image>().raycastTarget = false;
            var infoLvl = AddText(infoLvlBadge, "LevelBadge", "Level 5", 13, TextAnchor.MiddleCenter);
            StretchToParent(infoLvl);
            infoLvl.GetComponent<Text>().color = new Color(1f, 0.90f, 0.55f, 1f);
            infoLvl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lvlSh = infoLvl.AddComponent<Shadow>();
            lvlSh.effectColor = new Color(0, 0, 0, 0.90f);
            lvlSh.effectDistance = new Vector2(0.8f, -0.8f);

            // === DESCRIPTION: right of preview, generous space ===
            var infoDesc = AddText(infoInner, "Description", "Trains military units.\nIncreases army capacity by 50 per level.", 14, TextAnchor.UpperLeft);
            SetAnchors(infoDesc, 0.43f, 0.52f, 0.97f, 0.86f);
            infoDesc.GetComponent<Text>().color = TextLight;
            infoDesc.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
            infoDesc.GetComponent<Text>().verticalOverflow = VerticalWrapMode.Truncate;
            var infoDescOutline = infoDesc.AddComponent<Outline>();
            infoDescOutline.effectColor = new Color(0, 0, 0, 0.60f);
            infoDescOutline.effectDistance = new Vector2(0.8f, -0.8f);
            var infoDescSh = infoDesc.AddComponent<Shadow>();
            infoDescSh.effectColor = new Color(0, 0, 0, 0.70f);
            infoDescSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === UPGRADE COST SECTION: ornate header + separator + cost grid ===
            // Section separator: ornate gold line with diamond center
            var costSectionLine = AddPanel(infoInner, "CostSectionLine", new Color(0.83f, 0.66f, 0.26f, 0.40f));
            SetAnchors(costSectionLine, 0.04f, 0.405f, 0.96f, 0.41f);
            costSectionLine.GetComponent<Image>().raycastTarget = false;
            // Diamond accent at center of separator
            var costDiamond = AddPanel(infoInner, "CostDiamond", new Color(0.83f, 0.66f, 0.26f, 0.55f));
            SetAnchors(costDiamond, 0.46f, 0.395f, 0.54f, 0.42f);
            costDiamond.transform.localRotation = Quaternion.Euler(0, 0, 45);
            costDiamond.GetComponent<Image>().raycastTarget = false;

            var infoCostHeader = AddText(infoInner, "CostHeader", "\u2726  UPGRADE COST  \u2726", 12, TextAnchor.MiddleCenter);
            SetAnchors(infoCostHeader, 0.04f, 0.36f, 0.96f, 0.405f);
            infoCostHeader.GetComponent<Text>().color = new Color(0.90f, 0.72f, 0.30f, 1f);
            infoCostHeader.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var costHeaderSh = infoCostHeader.AddComponent<Shadow>();
            costHeaderSh.effectColor = new Color(0, 0, 0, 0.80f);
            costHeaderSh.effectDistance = new Vector2(0.5f, -0.5f);

            var infoCostSep = AddPanel(infoInner, "CostSep", new Color(0.83f, 0.66f, 0.26f, 0.20f));
            SetAnchors(infoCostSep, 0.08f, 0.355f, 0.92f, 0.358f);
            infoCostSep.GetComponent<Image>().raycastTarget = false;

            // Cost text area: dark recessed panel for contrast
            var costPanel = AddPanel(infoInner, "CostPanel", new Color(0.04f, 0.03f, 0.08f, 0.50f));
            SetAnchors(costPanel, 0.03f, 0.20f, 0.97f, 0.355f);
            costPanel.AddComponent<Outline>().effectColor = new Color(0.40f, 0.32f, 0.15f, 0.20f);
            costPanel.GetComponent<Outline>().effectDistance = new Vector2(0.8f, -0.8f);
            costPanel.GetComponent<Image>().raycastTarget = false;
            var infoCosts = AddText(costPanel, "CostText", "\u2713 \u25C8 1.2K Stone      \u2713 \u2666 800 Iron\n\u2713 \u2740 600 Grain       \u2713 \u2726 50 Arcane", 13, TextAnchor.MiddleCenter);
            StretchToParent(infoCosts);
            infoCosts.GetComponent<Text>().color = TextWhite;
            infoCosts.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;
            var infoCostsOutline = infoCosts.AddComponent<Outline>();
            infoCostsOutline.effectColor = new Color(0, 0, 0, 0.65f);
            infoCostsOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var infoCostsSh = infoCosts.AddComponent<Shadow>();
            infoCostsSh.effectColor = new Color(0, 0, 0, 0.70f);
            infoCostsSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === TIMER SECTION: ornate progress bar with glow ===
            var timerSection = AddPanel(infoInner, "TimerSection", new Color(0, 0, 0, 0));
            SetAnchors(timerSection, 0.03f, 0.13f, 0.97f, 0.20f);
            timerSection.GetComponent<Image>().raycastTarget = false;
            // Timer bar: rounded dark bg with gold border
            var timerBarBg = AddPanel(timerSection, "TimerBarBg", new Color(0.04f, 0.03f, 0.08f, 0.95f));
            SetAnchors(timerBarBg, 0f, 0.35f, 1f, 0.95f);
            var timerBorder = timerBarBg.AddComponent<Outline>();
            timerBorder.effectColor = new Color(0.72f, 0.56f, 0.22f, 0.55f);
            timerBorder.effectDistance = new Vector2(1f, -1f);
            // Glow fill bar
            var timerBarFill = AddPanel(timerBarBg, "TimerBarFill", new Color(0.25f, 0.82f, 0.40f, 1f));
            SetAnchors(timerBarFill, 0.005f, 0.08f, 0.50f, 0.92f); // 50% default, inset slightly
            var fillGlow = timerBarFill.AddComponent<Shadow>();
            fillGlow.effectColor = new Color(0.25f, 0.82f, 0.40f, 0.35f);
            fillGlow.effectDistance = new Vector2(0, -2f);
            // Time text below bar
            var infoTime = AddText(timerSection, "TimeText", "\u23F1  2h 30m remaining", 12, TextAnchor.MiddleCenter);
            SetAnchors(infoTime, 0f, -0.15f, 1f, 0.35f);
            infoTime.GetComponent<Text>().color = new Color(0.50f, 0.75f, 1f, 1f); // Bright sky
            infoTime.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var infoTimeSh = infoTime.AddComponent<Shadow>();
            infoTimeSh.effectColor = new Color(0, 0, 0, 0.80f);
            infoTimeSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === BUTTON ROW: Premium ornate buttons with glow ===
            // Idle state: Upgrade (wide green) + Move (gold) + Demolish (red) + Close (gray)
            var infoBtnRow = AddPanel(infoInner, "BtnRow", new Color(0, 0, 0, 0));
            SetAnchors(infoBtnRow, 0.02f, 0.02f, 0.98f, 0.12f);
            infoBtnRow.GetComponent<Image>().raycastTarget = false;

            // UPGRADE button — premium green with glow accent
            var infoUpBtn = AddPanel(infoBtnRow, "UpgradeBtn", new Color(0.15f, 0.58f, 0.28f, 1f));
            SetAnchors(infoUpBtn, 0f, 0f, 0.32f, 1f);
            SetButtonFeedback(infoUpBtn.AddComponent<Button>());
            var upBtnGlow = infoUpBtn.AddComponent<Outline>();
            upBtnGlow.effectColor = new Color(0.25f, 0.82f, 0.40f, 0.45f);
            upBtnGlow.effectDistance = new Vector2(1.5f, -1.5f);
            var upBtnShadow = infoUpBtn.AddComponent<Shadow>();
            upBtnShadow.effectColor = new Color(0.08f, 0.35f, 0.15f, 0.80f);
            upBtnShadow.effectDistance = new Vector2(0, -2f);
            var upBtnGlass = AddPanel(infoUpBtn, "Glass", new Color(0.55f, 1f, 0.65f, 0.12f));
            SetAnchors(upBtnGlass, 0f, 0.50f, 1f, 1f);
            upBtnGlass.GetComponent<Image>().raycastTarget = false;
            var upBtnDark = AddPanel(infoUpBtn, "DarkOverlay", new Color(0.08f, 0.30f, 0.12f, 0.35f));
            SetAnchors(upBtnDark, 0f, 0f, 1f, 0.45f);
            upBtnDark.GetComponent<Image>().raycastTarget = false;
            var upLbl = AddText(infoUpBtn, "Label", "UPGRADE", 14, TextAnchor.MiddleCenter);
            StretchToParent(upLbl);
            upLbl.GetComponent<Text>().color = Color.white;
            upLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            upLbl.AddComponent<Outline>().effectColor = new Color(0.05f, 0.25f, 0.08f, 0.80f);
            upLbl.GetComponent<Outline>().effectDistance = new Vector2(0.8f, -0.8f);
            upLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.90f);
            upLbl.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);

            // MOVE button — warm gold
            var moveBtn = AddPanel(infoBtnRow, "MoveBtn", new Color(0.55f, 0.42f, 0.18f, 1f));
            SetAnchors(moveBtn, 0.34f, 0f, 0.54f, 1f);
            SetButtonFeedback(moveBtn.AddComponent<Button>());
            moveBtn.AddComponent<Outline>().effectColor = new Color(0.72f, 0.56f, 0.22f, 0.45f);
            moveBtn.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            var moveBtnGlass = AddPanel(moveBtn, "Glass", new Color(0.80f, 0.65f, 0.30f, 0.10f));
            SetAnchors(moveBtnGlass, 0f, 0.50f, 1f, 1f);
            moveBtnGlass.GetComponent<Image>().raycastTarget = false;
            var moveBtnDark = AddPanel(moveBtn, "DarkOverlay", new Color(0.30f, 0.22f, 0.08f, 0.35f));
            SetAnchors(moveBtnDark, 0f, 0f, 1f, 0.45f);
            moveBtnDark.GetComponent<Image>().raycastTarget = false;
            var moveLbl = AddText(moveBtn, "Label", "MOVE", 13, TextAnchor.MiddleCenter);
            StretchToParent(moveLbl);
            moveLbl.GetComponent<Text>().color = new Color(1f, 0.93f, 0.75f, 1f);
            moveLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            moveLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            moveLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.8f, -0.8f);

            // DEMOLISH button — deep red
            var demolishBtn = AddPanel(infoBtnRow, "DemolishBtn", new Color(0.55f, 0.14f, 0.14f, 1f));
            SetAnchors(demolishBtn, 0.56f, 0f, 0.76f, 1f);
            SetButtonFeedback(demolishBtn.AddComponent<Button>());
            demolishBtn.AddComponent<Outline>().effectColor = new Color(0.75f, 0.22f, 0.22f, 0.45f);
            demolishBtn.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            var demBtnGlass = AddPanel(demolishBtn, "Glass", new Color(1f, 0.40f, 0.40f, 0.10f));
            SetAnchors(demBtnGlass, 0f, 0.50f, 1f, 1f);
            demBtnGlass.GetComponent<Image>().raycastTarget = false;
            var demBtnDark = AddPanel(demolishBtn, "DarkOverlay", new Color(0.30f, 0.06f, 0.06f, 0.35f));
            SetAnchors(demBtnDark, 0f, 0f, 1f, 0.45f);
            demBtnDark.GetComponent<Image>().raycastTarget = false;
            var demLbl = AddText(demolishBtn, "Label", "DEMOLISH", 11, TextAnchor.MiddleCenter);
            StretchToParent(demLbl);
            demLbl.GetComponent<Text>().color = new Color(1f, 0.80f, 0.80f, 1f);
            demLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            demLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            demLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.8f, -0.8f);

            // CLOSE button — subtle dark
            var infoClose = AddPanel(infoBtnRow, "CloseBtn", new Color(0.22f, 0.18f, 0.28f, 1f));
            SetAnchors(infoClose, 0.78f, 0f, 0.98f, 1f);
            SetButtonFeedback(infoClose.AddComponent<Button>());
            infoClose.AddComponent<Outline>().effectColor = new Color(0.40f, 0.35f, 0.50f, 0.40f);
            infoClose.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            var closeBtnGlass = AddPanel(infoClose, "Glass", new Color(0.50f, 0.45f, 0.60f, 0.08f));
            SetAnchors(closeBtnGlass, 0f, 0.50f, 1f, 1f);
            closeBtnGlass.GetComponent<Image>().raycastTarget = false;
            var closeBtnDark = AddPanel(infoClose, "DarkOverlay", new Color(0.10f, 0.08f, 0.14f, 0.30f));
            SetAnchors(closeBtnDark, 0f, 0f, 1f, 0.45f);
            closeBtnDark.GetComponent<Image>().raycastTarget = false;
            var closeLbl = AddText(infoClose, "Label", "CLOSE", 12, TextAnchor.MiddleCenter);
            StretchToParent(closeLbl);
            closeLbl.GetComponent<Text>().color = new Color(0.80f, 0.75f, 0.85f, 1f);
            closeLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            closeLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.80f);
            closeLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);

            // Upgrading state: Speed Up (wide blue glow) + Cancel (red) + Help (gold)
            var speedUpBtn = AddPanel(infoBtnRow, "SpeedUpBtn", new Color(0.18f, 0.55f, 0.85f, 1f));
            SetAnchors(speedUpBtn, 0f, 0f, 0.48f, 1f);
            SetButtonFeedback(speedUpBtn.AddComponent<Button>());
            speedUpBtn.AddComponent<Outline>().effectColor = new Color(0.35f, 0.75f, 1f, 0.50f);
            speedUpBtn.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            var spBtnGlass = AddPanel(speedUpBtn, "Glass", new Color(0.50f, 0.80f, 1f, 0.12f));
            SetAnchors(spBtnGlass, 0f, 0.50f, 1f, 1f);
            spBtnGlass.GetComponent<Image>().raycastTarget = false;
            var spBtnDark = AddPanel(speedUpBtn, "DarkOverlay", new Color(0.08f, 0.25f, 0.45f, 0.35f));
            SetAnchors(spBtnDark, 0f, 0f, 1f, 0.45f);
            spBtnDark.GetComponent<Image>().raycastTarget = false;
            var spLbl = AddText(speedUpBtn, "Label", "SPEED UP", 14, TextAnchor.MiddleCenter);
            StretchToParent(spLbl);
            spLbl.GetComponent<Text>().color = Color.white;
            spLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            spLbl.AddComponent<Outline>().effectColor = new Color(0.05f, 0.20f, 0.40f, 0.80f);
            spLbl.GetComponent<Outline>().effectDistance = new Vector2(0.8f, -0.8f);
            spLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.90f);
            spLbl.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            speedUpBtn.SetActive(false);

            var cancelBtn = AddPanel(infoBtnRow, "CancelBtn", new Color(0.65f, 0.16f, 0.18f, 1f));
            SetAnchors(cancelBtn, 0.50f, 0f, 0.73f, 1f);
            SetButtonFeedback(cancelBtn.AddComponent<Button>());
            cancelBtn.AddComponent<Outline>().effectColor = new Color(0.80f, 0.25f, 0.25f, 0.45f);
            cancelBtn.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            var canBtnDark = AddPanel(cancelBtn, "DarkOverlay", new Color(0.35f, 0.06f, 0.08f, 0.35f));
            SetAnchors(canBtnDark, 0f, 0f, 1f, 0.45f);
            canBtnDark.GetComponent<Image>().raycastTarget = false;
            var canLbl = AddText(cancelBtn, "Label", "CANCEL", 13, TextAnchor.MiddleCenter);
            StretchToParent(canLbl);
            canLbl.GetComponent<Text>().color = new Color(1f, 0.82f, 0.82f, 1f);
            canLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            canLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            canLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.8f, -0.8f);
            cancelBtn.SetActive(false);

            // Alliance Help button
            var helpBtn = AddPanel(infoBtnRow, "HelpBtn", new Color(0.62f, 0.48f, 0.18f, 1f));
            SetAnchors(helpBtn, 0.75f, 0f, 0.98f, 1f);
            SetButtonFeedback(helpBtn.AddComponent<Button>());
            helpBtn.AddComponent<Outline>().effectColor = new Color(0.83f, 0.66f, 0.26f, 0.50f);
            helpBtn.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            var helpBtnGlass = AddPanel(helpBtn, "Glass", new Color(0.90f, 0.75f, 0.35f, 0.10f));
            SetAnchors(helpBtnGlass, 0f, 0.50f, 1f, 1f);
            helpBtnGlass.GetComponent<Image>().raycastTarget = false;
            var helpLbl = AddText(helpBtn, "Label", "HELP", 13, TextAnchor.MiddleCenter);
            StretchToParent(helpLbl);
            helpLbl.GetComponent<Text>().color = new Color(1f, 0.93f, 0.65f, 1f);
            helpLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            helpLbl.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            helpLbl.GetComponent<Shadow>().effectDistance = new Vector2(0.8f, -0.8f);
            helpBtn.SetActive(false);

            // === Decorative corner accents (gold diamonds at frame corners) ===
            float cornerSize = 0.035f;
            float[][] corners = { new[]{0.01f, 0.97f}, new[]{0.97f, 0.97f}, new[]{0.01f, 0.01f}, new[]{0.97f, 0.01f} };
            for (int ci = 0; ci < corners.Length; ci++)
            {
                var corner = AddPanel(infoInner, $"CornerAccent_{ci}", new Color(0.83f, 0.66f, 0.26f, 0.35f));
                SetAnchors(corner, corners[ci][0], corners[ci][1], corners[ci][0] + cornerSize, corners[ci][1] + cornerSize);
                corner.transform.localRotation = Quaternion.Euler(0, 0, 45);
                corner.GetComponent<Image>().raycastTarget = false;
            }

            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildingInfoPopupController>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.ResourceFlyToHUD>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildQueueHUDIndicator>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.UpgradeCompleteToast>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildCatalogController>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.CityPowerHUD>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.SpeedupConfirmDialog>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.UpgradeConfirmDialog>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.ResourceProductionSummary>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.UpgradeRecommendationBanner>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildingNotificationBadge>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.OfflineEarningsPopup>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildingBusyIndicator>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.QuickUpgradeHandler>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildFailedToast>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.DemolishConfirmDialog>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.AutoUpgradeToggle>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildingConstructionOverlay>();
            canvasGo.AddComponent<AshenThrone.UI.Empire.BuildingQuickActionMenu>();
            infoPopup.SetActive(false);

            // P&C: Building Catalog Popup (shown when tapping empty ground)
            var catalogPopup = new GameObject("BuildCatalogPopup");
            catalogPopup.transform.SetParent(canvasGo.transform, false);
            catalogPopup.AddComponent<RectTransform>();
            var catBg = catalogPopup.AddComponent<Image>();
            catBg.color = BgDark;
            SetAnchors(catalogPopup, 0.05f, 0.15f, 0.95f, 0.85f);
            // Inner frame
            var catInner = new GameObject("CatalogInner");
            catInner.transform.SetParent(catalogPopup.transform, false);
            catInner.AddComponent<RectTransform>();
            var catInnerBg = catInner.AddComponent<Image>();
            catInnerBg.color = BgMid;
            SetAnchors(catInner, 0.02f, 0.02f, 0.98f, 0.98f);
            AddOutline(catInner, new Color(0.78f, 0.62f, 0.22f, 0.8f), 1.5f);
            // Title
            var catTitle = AddText(catInner, "CatalogTitle", "BUILD", 14, TextAnchor.MiddleCenter);
            SetAnchors(catTitle, 0.10f, 0.88f, 0.90f, 0.98f);
            catTitle.GetComponent<Text>().color = new Color(0.83f, 0.66f, 0.26f, 1f);
            catTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            // Tab container (HorizontalLayoutGroup)
            var tabContainer = new GameObject("TabContainer");
            tabContainer.transform.SetParent(catInner.transform, false);
            tabContainer.AddComponent<RectTransform>();
            SetAnchors(tabContainer, 0.04f, 0.78f, 0.96f, 0.87f);
            var tabHLG = tabContainer.AddComponent<HorizontalLayoutGroup>();
            tabHLG.spacing = 4;
            tabHLG.childForceExpandWidth = true;
            tabHLG.childForceExpandHeight = true;
            // List container (VerticalLayoutGroup)
            var listContainer = new GameObject("ListContainer");
            listContainer.transform.SetParent(catInner.transform, false);
            listContainer.AddComponent<RectTransform>();
            SetAnchors(listContainer, 0.04f, 0.08f, 0.96f, 0.76f);
            var listVLG = listContainer.AddComponent<VerticalLayoutGroup>();
            listVLG.spacing = 4;
            listVLG.childForceExpandWidth = true;
            listVLG.childForceExpandHeight = false;
            listVLG.padding = new RectOffset(2, 2, 2, 2);
            // Close button
            var catCloseBtn = AddStyledButton(catInner, "CatalogCloseBtn", "X", new Color(0.30f, 0.25f, 0.35f, 1f), BgMid);
            SetAnchors(catCloseBtn, 0.88f, 0.88f, 0.97f, 0.98f);
            catCloseBtn.transform.Find("Label").GetComponent<Text>().fontSize = 12;
            // Overlay for tap-to-close
            var catOverlay = new GameObject("CatalogOverlay");
            catOverlay.transform.SetParent(catalogPopup.transform, false);
            catOverlay.transform.SetAsFirstSibling();
            var catOverlayRect = catOverlay.AddComponent<RectTransform>();
            catOverlayRect.anchorMin = Vector2.zero;
            catOverlayRect.anchorMax = Vector2.one;
            catOverlayRect.offsetMin = Vector2.zero;
            catOverlayRect.offsetMax = Vector2.zero;
            var catOverlayImg = catOverlay.AddComponent<Image>();
            catOverlayImg.color = new Color(0, 0, 0, 0.01f);
            catOverlay.AddComponent<Button>();
            catalogPopup.SetActive(false);

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
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");

            // === BUILDING SPRITES for world map objects ===
            var strongholdSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/stronghold_t3.png");
            var strongholdT1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/stronghold_t1.png");
            var strongholdT2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/stronghold_t2.png");
            var grainSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/grain_farm_t1.png");
            var ironSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/iron_mine_t1.png");
            var stoneSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/stone_quarry_t1.png");
            var arcaneSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/arcane_tower_t1.png");
            var towerSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/watch_tower_t1.png");
            var barracksSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Buildings/barracks_t1.png");
            // Terrain feature sprites
            var forestSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/worldmap_forest.png");
            var swampSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/worldmap_swamp.png");

            // === P&C-STYLE OPEN WORLD — continuous green terrain with scattered objects ===

            // Green terrain background
            var bg = AddPanel(canvasGo, "MapBackground", new Color(0.32f, 0.48f, 0.20f, 1f)); // vivid P&C green grass
            StretchToParent(bg);

            // Scrollable viewport
            var viewport = AddPanel(canvasGo, "MapViewport", new Color(0.28f, 0.42f, 0.18f, 1f));
            StretchToParent(viewport);
            viewport.AddComponent<RectMask2D>();
            viewport.GetComponent<Image>().raycastTarget = false;

            float contentW = 4000f, contentH = 4500f; // large open world
            var mapContent = new GameObject("MapContent");
            mapContent.transform.SetParent(viewport.transform, false);
            var contentRect = mapContent.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(contentW, contentH);
            contentRect.localScale = Vector3.one * 1.0f;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.10f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;

            // --- TERRAIN VARIATION: scatter forest/swamp patches on green base ---
            var terrainPatches = new (float x, float y, float w, float h, Sprite spr, float alpha)[] {
                (-1200, -800, 700, 600, forestSpr, 0.40f),
                (800, 1200, 650, 550, forestSpr, 0.35f),
                (-600, 1400, 600, 500, swampSpr, 0.30f),
                (1400, -400, 580, 520, forestSpr, 0.38f),
                (-1500, 600, 500, 450, swampSpr, 0.28f),
                (400, -1600, 650, 580, forestSpr, 0.32f),
                (1200, 800, 450, 400, swampSpr, 0.25f),
            };
            foreach (var patch in terrainPatches)
            {
                if (patch.spr == null) continue;
                var pGO = new GameObject("TerrainPatch");
                pGO.transform.SetParent(mapContent.transform, false);
                var pRect = pGO.AddComponent<RectTransform>();
                pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.5f);
                pRect.pivot = new Vector2(0.5f, 0.5f);
                pRect.anchoredPosition = new Vector2(patch.x, patch.y);
                pRect.sizeDelta = new Vector2(patch.w, patch.h);
                var pImg = pGO.AddComponent<Image>();
                pImg.sprite = patch.spr;
                pImg.preserveAspect = false;
                pImg.color = new Color(1f, 1f, 1f, patch.alpha);
                pImg.raycastTarget = false;
            }

            // --- TERRAIN DECORATION: scatter small rocks, tree clusters ---
            var decorations = new (float x, float y, string symbol, Color color, float size)[] {
                (-300, 200, "\u2663", new Color(0.20f, 0.38f, 0.15f, 0.60f), 28f),   // tree
                (500, -300, "\u2663", new Color(0.22f, 0.40f, 0.18f, 0.55f), 24f),
                (-800, -500, "\u25C6", new Color(0.40f, 0.38f, 0.32f, 0.45f), 18f),  // rock
                (900, 600, "\u2663", new Color(0.18f, 0.35f, 0.14f, 0.50f), 30f),
                (-1100, 300, "\u25C6", new Color(0.38f, 0.35f, 0.28f, 0.40f), 16f),
                (200, 800, "\u2663", new Color(0.22f, 0.42f, 0.16f, 0.55f), 26f),
                (-400, -1000, "\u2663", new Color(0.20f, 0.36f, 0.14f, 0.48f), 22f),
                (1300, -800, "\u25C6", new Color(0.35f, 0.32f, 0.25f, 0.38f), 20f),
                (-700, 1000, "\u2663", new Color(0.24f, 0.44f, 0.18f, 0.52f), 28f),
                (600, 1500, "\u25C6", new Color(0.36f, 0.34f, 0.28f, 0.42f), 15f),
                (0, -600, "\u2663", new Color(0.22f, 0.40f, 0.16f, 0.50f), 24f),
                (-1400, -200, "\u2663", new Color(0.18f, 0.35f, 0.12f, 0.45f), 26f),
            };
            foreach (var d in decorations)
            {
                var dGO = new GameObject("Decor");
                dGO.transform.SetParent(mapContent.transform, false);
                var dRect = dGO.AddComponent<RectTransform>();
                dRect.anchorMin = dRect.anchorMax = new Vector2(0.5f, 0.5f);
                dRect.pivot = new Vector2(0.5f, 0.5f);
                dRect.anchoredPosition = new Vector2(d.x, d.y);
                dRect.sizeDelta = new Vector2(d.size * 2f, d.size * 2f);
                var dText = dGO.AddComponent<Text>();
                dText.text = d.symbol;
                dText.fontSize = (int)d.size;
                dText.alignment = TextAnchor.MiddleCenter;
                dText.color = d.color;
                dText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                dText.raycastTarget = false;
            }

            // Helper: place a map object (castle, resource, monster)
            System.Action<float, float, float, float, Sprite, string, string, Color, bool, string> PlaceMapObject =
                (float px, float py, float w, float h, Sprite spr, string label, string sublabel, Color labelColor, bool isShielded, string goName) =>
            {
                var obj = new GameObject(goName);
                obj.transform.SetParent(mapContent.transform, false);
                var objRect = obj.AddComponent<RectTransform>();
                objRect.anchorMin = objRect.anchorMax = new Vector2(0.5f, 0.5f);
                objRect.pivot = new Vector2(0.5f, 0.5f);
                objRect.anchoredPosition = new Vector2(px, py);
                objRect.sizeDelta = new Vector2(w, h);

                // Building/object sprite
                if (spr != null)
                {
                    var img = obj.AddComponent<Image>();
                    img.sprite = spr;
                    img.preserveAspect = true;
                    img.color = Color.white;
                    img.raycastTarget = true;
                    var sh = obj.AddComponent<Shadow>();
                    sh.effectColor = new Color(0, 0, 0, 0.85f);
                    sh.effectDistance = new Vector2(2f, -3f);
                }

                // Shield dome effect
                if (isShielded)
                {
                    var shield = AddPanel(obj, "Shield", new Color(0.30f, 0.60f, 0.90f, 0.18f));
                    SetAnchors(shield, -0.15f, -0.10f, 1.15f, 1.10f);
                    var shieldOut = shield.AddComponent<Outline>();
                    shieldOut.effectColor = new Color(0.40f, 0.70f, 1f, 0.40f);
                    shieldOut.effectDistance = new Vector2(2f, -2f);
                }

                // Name label above
                if (!string.IsNullOrEmpty(label))
                {
                    var labelGO = new GameObject("Label");
                    labelGO.transform.SetParent(obj.transform, false);
                    var lRect = labelGO.AddComponent<RectTransform>();
                    lRect.anchorMin = new Vector2(0f, 1f);
                    lRect.anchorMax = new Vector2(1f, 1f);
                    lRect.pivot = new Vector2(0.5f, 0f);
                    lRect.anchoredPosition = new Vector2(0, 4f);
                    lRect.sizeDelta = new Vector2(w * 1.4f, 22f);
                    // Dark pill bg
                    var lBg = labelGO.AddComponent<Image>();
                    lBg.color = new Color(0.02f, 0.02f, 0.04f, 0.80f);
                    lBg.raycastTarget = false;
                    var lText = AddText(labelGO, "Text", label, 10, TextAnchor.MiddleCenter);
                    StretchToParent(lText);
                    lText.GetComponent<Text>().color = labelColor;
                    lText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    var lSh = lText.AddComponent<Shadow>();
                    lSh.effectColor = new Color(0, 0, 0, 1f);
                    lSh.effectDistance = new Vector2(0.5f, -0.5f);
                }

                // Sub-label (power, level) below
                if (!string.IsNullOrEmpty(sublabel))
                {
                    var subGO = new GameObject("SubLabel");
                    subGO.transform.SetParent(obj.transform, false);
                    var sRect = subGO.AddComponent<RectTransform>();
                    sRect.anchorMin = new Vector2(0f, 0f);
                    sRect.anchorMax = new Vector2(1f, 0f);
                    sRect.pivot = new Vector2(0.5f, 1f);
                    sRect.anchoredPosition = new Vector2(0, -2f);
                    sRect.sizeDelta = new Vector2(w * 1.2f, 16f);
                    var sBg = subGO.AddComponent<Image>();
                    sBg.color = new Color(0.02f, 0.02f, 0.04f, 0.65f);
                    sBg.raycastTarget = false;
                    var sText = AddText(subGO, "Text", sublabel, 8, TextAnchor.MiddleCenter);
                    StretchToParent(sText);
                    sText.GetComponent<Text>().color = new Color(0.75f, 0.72f, 0.60f, 0.90f);
                    var sSh = sText.AddComponent<Shadow>();
                    sSh.effectColor = new Color(0, 0, 0, 0.85f);
                    sSh.effectDistance = new Vector2(0.3f, -0.3f);
                }
            };

            // ======= YOUR CASTLE — center of the map, largest =======
            PlaceMapObject(0, 0, 130, 165, strongholdSpr,
                "[ASH] Lord Kael", "\u2694 1.2M Power", new Color(0.55f, 0.95f, 0.42f, 1f), true, "MyCastle");

            // ======= ALLIED CASTLES — green names, clustered near you =======
            var alliedCastles = new (float x, float y, string name, string power, Sprite spr, bool shield)[] {
                (-280, 180, "[ASH] Lady Sera", "\u2694 980K", strongholdT2, true),
                (320, 250, "[ASH] Capt. Lyra", "\u2694 750K", strongholdT2, false),
                (-150, -320, "[ASH] Warden Rowan", "\u2694 1.1M", strongholdT2, true),
                (180, -200, "[ASH] Scout Wren", "\u2694 420K", strongholdT1, false),
                (-400, -80, "[ASH] Sir Aldric", "\u2694 680K", strongholdT1, true),
                (450, 100, "[ASH] Knight Sera", "\u2694 850K", strongholdT2, false),
            };
            foreach (var ac in alliedCastles)
                PlaceMapObject(ac.x, ac.y, 90, 110, ac.spr, ac.name, ac.power,
                    new Color(0.50f, 0.92f, 0.38f, 1f), ac.shield, "AlliedCastle");

            // ======= ENEMY CASTLES — red names, scattered further out =======
            var enemyCastles = new (float x, float y, string name, string power, Sprite spr, bool shield)[] {
                (800, 600, "[DRK] Overlord Nyx", "\u2694 2.1M", strongholdT2, true),
                (-900, 700, "[BLD] Warlord Grim", "\u2694 1.8M", strongholdT2, false),
                (700, -800, "[SHA] Shadow Queen", "\u2694 1.5M", strongholdT1, true),
                (-600, -900, "[DRK] Baron Ash", "\u2694 900K", strongholdT1, false),
                (1100, -200, "[BLD] Count Dread", "\u2694 1.3M", strongholdT2, true),
                (-1000, -400, "[SHA] Dark Host", "\u2694 700K", strongholdT1, false),
                (500, 1000, "[DRK] Lord Doom", "\u2694 1.6M", strongholdT2, false),
                (-800, 1200, "[SHA] Skulltaker", "\u2694 1.1M", strongholdT1, true),
            };
            foreach (var ec in enemyCastles)
                PlaceMapObject(ec.x, ec.y, 82, 100, ec.spr, ec.name, ec.power,
                    new Color(0.98f, 0.38f, 0.32f, 1f), ec.shield, "EnemyCastle");

            // ======= RESOURCE GATHERING NODES =======
            var resources = new (float x, float y, Sprite spr, string label, string goName)[] {
                // Grain fields
                (-500, 350, grainSpr, "Grain Lv.4", "Res_Grain"),
                (250, 500, grainSpr, "Grain Lv.3", "Res_Grain"),
                (600, -150, grainSpr, "Grain Lv.5", "Res_Grain"),
                (-200, -550, grainSpr, "Grain Lv.2", "Res_Grain"),
                (900, 350, grainSpr, "Grain Lv.3", "Res_Grain"),
                (-1100, 150, grainSpr, "Grain Lv.4", "Res_Grain"),
                // Iron deposits
                (400, -500, ironSpr, "Iron Lv.4", "Res_Iron"),
                (-650, -200, ironSpr, "Iron Lv.3", "Res_Iron"),
                (150, 700, ironSpr, "Iron Lv.5", "Res_Iron"),
                (-350, 900, ironSpr, "Iron Lv.2", "Res_Iron"),
                (1000, -600, ironSpr, "Iron Lv.3", "Res_Iron"),
                // Stone quarries
                (-150, 400, stoneSpr, "Stone Lv.3", "Res_Stone"),
                (550, 200, stoneSpr, "Stone Lv.4", "Res_Stone"),
                (-800, -600, stoneSpr, "Stone Lv.2", "Res_Stone"),
                (350, -700, stoneSpr, "Stone Lv.5", "Res_Stone"),
                // Arcane essence
                (750, 450, arcaneSpr, "Arcane Lv.5", "Res_Arcane"),
                (-450, 600, arcaneSpr, "Arcane Lv.4", "Res_Arcane"),
                (100, -400, arcaneSpr, "Arcane Lv.3", "Res_Arcane"),
            };
            foreach (var res in resources)
                PlaceMapObject(res.x, res.y, 55, 68, res.spr, res.label, null,
                    new Color(0.90f, 0.85f, 0.55f, 1f), false, res.goName);

            // ======= MONSTER DENS — red-tinted with skull icon and level =======
            var monsters = new (float x, float y, string name, int level)[] {
                (-350, -150, "Wolf Pack", 8),
                (650, -400, "Wraith", 15),
                (-750, 400, "Golem", 22),
                (300, 350, "Bandit Camp", 12),
                (-500, -700, "Undead Horde", 18),
                (900, -100, "Dragon Whelp", 25),
                (-200, 800, "Troll Den", 10),
                (500, -900, "Demon Gate", 30),
                (-900, -100, "Shadow Beast", 20),
                (1200, 500, "Void Spawn", 28),
                (-300, 1100, "Dark Lich", 35),
                (100, -1200, "Ancient Wyrm", 40),
                (-1100, 800, "Bone Giant", 32),
                (750, 1100, "Fire Imp", 6),
            };
            foreach (var m in monsters)
            {
                var mGO = new GameObject("Monster_" + m.name);
                mGO.transform.SetParent(mapContent.transform, false);
                var mRect = mGO.AddComponent<RectTransform>();
                mRect.anchorMin = mRect.anchorMax = new Vector2(0.5f, 0.5f);
                mRect.pivot = new Vector2(0.5f, 0.5f);
                mRect.anchoredPosition = new Vector2(m.x, m.y);
                mRect.sizeDelta = new Vector2(60, 55);
                // Red-tinted circle bg
                var mBg = mGO.AddComponent<Image>();
                mBg.color = new Color(0.45f, 0.10f, 0.08f, 0.75f);
                // Skull icon
                var skull = AddText(mGO, "Icon", "\u2620", 22, TextAnchor.MiddleCenter);
                SetAnchors(skull, 0f, 0.25f, 1f, 0.95f);
                skull.GetComponent<Text>().color = new Color(1f, 0.40f, 0.28f, 0.95f);
                var skSh = skull.AddComponent<Shadow>();
                skSh.effectColor = new Color(0, 0, 0, 0.90f);
                skSh.effectDistance = new Vector2(1f, -1f);
                // Level badge
                var lvl = AddText(mGO, "Level", $"Lv.{m.level}", 9, TextAnchor.MiddleCenter);
                SetAnchors(lvl, 0f, 0f, 1f, 0.28f);
                lvl.GetComponent<Text>().color = new Color(1f, 0.85f, 0.50f, 1f);
                lvl.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var lvSh = lvl.AddComponent<Shadow>();
                lvSh.effectColor = new Color(0, 0, 0, 0.85f);
                lvSh.effectDistance = new Vector2(0.5f, -0.5f);
                // Name label above
                var nameLbl = new GameObject("Name");
                nameLbl.transform.SetParent(mGO.transform, false);
                var nRect = nameLbl.AddComponent<RectTransform>();
                nRect.anchorMin = new Vector2(0f, 1f);
                nRect.anchorMax = new Vector2(1f, 1f);
                nRect.pivot = new Vector2(0.5f, 0f);
                nRect.anchoredPosition = new Vector2(0, 2f);
                nRect.sizeDelta = new Vector2(100, 16f);
                var nBg = nameLbl.AddComponent<Image>();
                nBg.color = new Color(0.30f, 0.05f, 0.04f, 0.75f);
                nBg.raycastTarget = false;
                var nText = AddText(nameLbl, "Txt", m.name, 8, TextAnchor.MiddleCenter);
                StretchToParent(nText);
                nText.GetComponent<Text>().color = new Color(1f, 0.70f, 0.50f, 0.95f);
                nText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            }

            // ======= ALLIANCE TERRITORY OVERLAY — green-tinted zone around allied castles =======
            var alliTerritory = AddPanel(mapContent.gameObject, "AllianceTerritory", new Color(0.10f, 0.50f, 0.08f, 0.06f));
            var atRect = alliTerritory.GetComponent<RectTransform>();
            atRect.anchorMin = atRect.anchorMax = new Vector2(0.5f, 0.5f);
            atRect.pivot = new Vector2(0.5f, 0.5f);
            atRect.anchoredPosition = new Vector2(0, 0);
            atRect.sizeDelta = new Vector2(1000, 900);
            atRect.SetAsFirstSibling(); // render behind everything
            var atOut = alliTerritory.AddComponent<Outline>();
            atOut.effectColor = new Color(0.20f, 0.60f, 0.15f, 0.35f);
            atOut.effectDistance = new Vector2(3f, -3f);

            // ======= CENTRAL WONDER (DRAGONIA) — kingdom center landmark =======
            var wonderGO = new GameObject("CentralWonder");
            wonderGO.transform.SetParent(mapContent.transform, false);
            var wRect = wonderGO.AddComponent<RectTransform>();
            wRect.anchorMin = wRect.anchorMax = new Vector2(0.5f, 0.5f);
            wRect.pivot = new Vector2(0.5f, 0.5f);
            wRect.anchoredPosition = new Vector2(0, 1500); // north of player
            wRect.sizeDelta = new Vector2(200, 240);
            if (strongholdSpr != null) {
                var wImg = wonderGO.AddComponent<Image>();
                wImg.sprite = strongholdSpr;
                wImg.preserveAspect = true;
                wImg.color = new Color(1f, 0.90f, 0.70f, 1f); // golden tint
            }
            var wLabel = new GameObject("WonderLabel");
            wLabel.transform.SetParent(wonderGO.transform, false);
            var wlRect = wLabel.AddComponent<RectTransform>();
            wlRect.anchorMin = new Vector2(0f, 1f);
            wlRect.anchorMax = new Vector2(1f, 1f);
            wlRect.pivot = new Vector2(0.5f, 0f);
            wlRect.anchoredPosition = new Vector2(0, 6f);
            wlRect.sizeDelta = new Vector2(220, 28f);
            var wlBg = wLabel.AddComponent<Image>();
            wlBg.color = new Color(0.35f, 0.25f, 0.08f, 0.85f);
            var wlText = AddText(wLabel, "Txt", "\u2726 DRAGONIA \u2726", 14, TextAnchor.MiddleCenter);
            StretchToParent(wlText);
            wlText.GetComponent<Text>().color = new Color(1f, 0.88f, 0.42f, 1f);
            wlText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var wlSh = wlText.AddComponent<Shadow>();
            wlSh.effectColor = new Color(0, 0, 0, 1f);
            wlSh.effectDistance = new Vector2(1f, -1f);

            // Notch fill
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.05f, 1f));
            SetAnchors(notchFill, 0f, 0.955f, 1f, 1f);
            var wmNotchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.45f));
            SetAnchors(wmNotchBorder, 0f, 0f, 1f, 0.012f);

            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.10f, 0.08f, 0.16f, 0.95f));
            SetAnchors(topBar, 0f, 0.905f, 1f, 0.965f);
            var topBorderGold = AddPanel(topBar, "BorderGold", new Color(0.90f, 0.72f, 0.30f, 1f));
            SetAnchors(topBorderGold, 0f, 0f, 1f, 0.05f);
            topBorderGold.AddComponent<LayoutElement>().ignoreLayout = true;

            var mapTitle = AddText(topBar, "MapTitle", "KINGDOM MAP \u2014 K:12 Ashlands", 16, TextAnchor.MiddleCenter);
            SetAnchors(mapTitle, 0.15f, 0.05f, 0.85f, 0.95f);
            mapTitle.GetComponent<Text>().color = new Color(1f, 0.88f, 0.42f, 1f);
            mapTitle.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var mtShadow = mapTitle.AddComponent<Shadow>();
            mtShadow.effectColor = new Color(0, 0, 0, 0.98f);
            mtShadow.effectDistance = new Vector2(1f, -1f);

            var backBtn = AddPanel(topBar, "BackBtn", new Color(0.25f, 0.20f, 0.30f));
            SetAnchors(backBtn, 0.01f, 0.08f, 0.14f, 0.92f);
            if (btnOrnateSpr != null) { backBtn.GetComponent<Image>().sprite = btnOrnateSpr; backBtn.GetComponent<Image>().type = Image.Type.Sliced; backBtn.GetComponent<Image>().color = new Color(0.55f, 0.45f, 0.38f, 1f); }
            SetButtonFeedback(backBtn.AddComponent<Button>());
            AddSceneNav(backBtn, SceneName.Empire);
            var bbLabel = AddText(backBtn, "Label", "\u25C0", 14, TextAnchor.MiddleCenter);
            StretchToParent(bbLabel);
            bbLabel.GetComponent<Text>().color = Color.white;
            bbLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Search button
            var searchBtn = AddPanel(topBar, "SearchBtn", new Color(0.25f, 0.22f, 0.30f));
            SetAnchors(searchBtn, 0.86f, 0.08f, 0.99f, 0.92f);
            if (btnOrnateSpr != null) { searchBtn.GetComponent<Image>().sprite = btnOrnateSpr; searchBtn.GetComponent<Image>().type = Image.Type.Sliced; searchBtn.GetComponent<Image>().color = new Color(0.50f, 0.44f, 0.55f, 1f); }
            SetButtonFeedback(searchBtn.AddComponent<Button>());
            AddSceneNav(searchBtn, SceneName.WorldMap);
            var srchLabel = AddText(searchBtn, "Label", "\uD83D\uDD0D", 14, TextAnchor.MiddleCenter);
            StretchToParent(srchLabel);
            srchLabel.GetComponent<Text>().color = TextLight;

            // === COORDINATES — bottom left ===
            var coordBg = AddPanel(canvas, "CoordDisplay", new Color(0.02f, 0.02f, 0.04f, 0.75f));
            SetAnchors(coordBg, 0.01f, 0.02f, 0.28f, 0.06f);
            var coordTxt = AddText(coordBg, "Coords", "K:12  X:482  Y:317", 10, TextAnchor.MiddleCenter);
            StretchToParent(coordTxt);
            coordTxt.GetComponent<Text>().color = new Color(0.78f, 0.72f, 0.55f, 0.90f);
            coordTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === ZOOM CONTROLS — right side ===
            var zoomIn = AddPanel(canvas, "ZoomIn", new Color(0.30f, 0.25f, 0.20f, 0.90f));
            SetAnchors(zoomIn, 0.90f, 0.52f, 0.98f, 0.60f);
            if (btnOrnateSpr != null) { zoomIn.GetComponent<Image>().sprite = btnOrnateSpr; zoomIn.GetComponent<Image>().type = Image.Type.Sliced; zoomIn.GetComponent<Image>().color = new Color(0.55f, 0.48f, 0.40f, 1f); }
            SetButtonFeedback(zoomIn.AddComponent<Button>());
            AddSceneNav(zoomIn, SceneName.WorldMap);
            var ziLabel = AddText(zoomIn, "Label", "+", 18, TextAnchor.MiddleCenter);
            StretchToParent(ziLabel);
            ziLabel.GetComponent<Text>().color = Gold;
            ziLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            var zoomOut = AddPanel(canvas, "ZoomOut", new Color(0.30f, 0.25f, 0.20f, 0.90f));
            SetAnchors(zoomOut, 0.90f, 0.43f, 0.98f, 0.51f);
            if (btnOrnateSpr != null) { zoomOut.GetComponent<Image>().sprite = btnOrnateSpr; zoomOut.GetComponent<Image>().type = Image.Type.Sliced; zoomOut.GetComponent<Image>().color = new Color(0.55f, 0.48f, 0.40f, 1f); }
            SetButtonFeedback(zoomOut.AddComponent<Button>());
            AddSceneNav(zoomOut, SceneName.WorldMap);
            var zoLabel = AddText(zoomOut, "Label", "\u2212", 18, TextAnchor.MiddleCenter);
            StretchToParent(zoLabel);
            zoLabel.GetComponent<Text>().color = Gold;
            zoLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // === MARCH TIMER — active troop movement ===
            var marchBg = AddPanel(canvas, "MarchTimer", new Color(0.04f, 0.03f, 0.08f, 0.88f));
            SetAnchors(marchBg, 0.20f, 0.86f, 0.80f, 0.905f);
            if (ornateSpr != null) { marchBg.GetComponent<Image>().sprite = ornateSpr; marchBg.GetComponent<Image>().type = Image.Type.Sliced; marchBg.GetComponent<Image>().color = new Color(0.55f, 0.30f, 0.25f, 1f); }
            var marchLabel = AddText(marchBg, "Label", "\u2694 March to Iron Lv.4  \u23F1 4:32", 11, TextAnchor.MiddleCenter);
            StretchToParent(marchLabel);
            marchLabel.GetComponent<Text>().color = TextWhite;
            marchLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var marchSh = marchLabel.AddComponent<Shadow>();
            marchSh.effectColor = new Color(0, 0, 0, 0.85f);
            marchSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === MINI-MAP — compact, bottom right ===
            var miniMap = AddPanel(canvas, "MiniMap", new Color(0.15f, 0.22f, 0.10f, 0.90f));
            SetAnchors(miniMap, 0.78f, 0.06f, 0.99f, 0.20f);
            if (ornateSpr != null) { miniMap.GetComponent<Image>().sprite = ornateSpr; miniMap.GetComponent<Image>().type = Image.Type.Sliced; miniMap.GetComponent<Image>().color = new Color(0.45f, 0.42f, 0.36f, 1f); }
            // Player dot (gold)
            var playerDot = AddPanel(miniMap, "PlayerDot", Gold);
            SetAnchors(playerDot, 0.44f, 0.42f, 0.56f, 0.58f);
            // View rect (gold outline)
            var viewRect = AddPanel(miniMap, "ViewRect", new Color(0.85f, 0.68f, 0.28f, 0.10f));
            SetAnchors(viewRect, 0.30f, 0.30f, 0.70f, 0.70f);
            AddOutlinePanel(viewRect, new Color(0.90f, 0.72f, 0.30f, 0.75f));

            // Territory info panel (hidden, shown on tap)
            var infoPanel = AddPanel(canvas, "TerritoryInfo", new Color(0.04f, 0.03f, 0.08f, 0.94f));
            SetAnchors(infoPanel, 0.01f, 0.20f, 0.24f, 0.46f);
            infoPanel.SetActive(false);
            // (info panel contents populated at runtime when tapped)

            SaveScene();
            Debug.Log("[SceneUIGenerator] WorldMap scene: P&C-style open world with castles, resources, monsters");
        }

        // ===================================================================
        // ALLIANCE SCENE — Social hub (P&C quality)
        // ===================================================================
        [MenuItem("AshenThrone/Generate Scene UI/Alliance")]
        public static void SetupAllianceScene()
        {
            var scene = OpenScene("Alliance");
            var canvasGo = FindOrCreateCanvas(scene);
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            var btnOrnateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            var shieldSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/nav_alliance.png");

            // Background — dark fantasy cityscape
            var bg = AddPanel(canvasGo, "Background", new Color(0.03f, 0.02f, 0.06f, 1f));
            StretchToParent(bg);
            var bgArt = AddPanel(bg, "CityArt", Color.white);
            StretchToParent(bgArt);
            var bgSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/empire_bg.png");
            if (bgSpr != null) { bgArt.GetComponent<Image>().sprite = bgSpr; bgArt.GetComponent<Image>().preserveAspect = false; bgArt.GetComponent<Image>().color = new Color(0.65f, 0.60f, 0.65f, 1f); }
            var bgDarken = AddPanel(bg, "DarkenOverlay", new Color(0.02f, 0.01f, 0.06f, 0.30f));
            StretchToParent(bgDarken);
            var skyGrad = AddPanel(bg, "SkyGradient", new Color(0.06f, 0.04f, 0.14f, 0.30f));
            SetAnchors(skyGrad, 0f, 0.70f, 1f, 1f);
            var groundGrad = AddPanel(bg, "GroundGrad", new Color(0.02f, 0.01f, 0.04f, 0.45f));
            SetAnchors(groundGrad, 0f, 0f, 1f, 0.15f);
            // Left vignette
            var vigL = AddPanel(bg, "VignetteL", new Color(0f, 0f, 0.02f, 0.30f));
            SetAnchors(vigL, 0f, 0f, 0.06f, 1f);
            // Right vignette
            var vigR = AddPanel(bg, "VignetteR", new Color(0f, 0f, 0.02f, 0.30f));
            SetAnchors(vigR, 0.94f, 0f, 1f, 1f);

            // Notch fill with gold accent
            var notchFill = AddPanel(canvasGo, "NotchFill", new Color(0.03f, 0.02f, 0.06f, 1f));
            SetAnchors(notchFill, 0f, 0.91f, 1f, 1f);
            var alliNotchBorder = AddPanel(notchFill, "Border", new Color(0.72f, 0.56f, 0.22f, 0.55f));
            SetAnchors(alliNotchBorder, 0f, 0f, 1f, 0.008f);

            var canvas = CreateSafeArea(canvasGo);

            // === TOP BAR — solid dark, bright gold text (no ornate) ===
            var topBar = AddPanel(canvas, "TopBar", new Color(0.10f, 0.08f, 0.16f, 0.98f));
            SetAnchors(topBar, 0f, 0.925f, 1f, 0.995f);
            // Bright gold bottom border
            var tbBorderBot = AddPanel(topBar, "BorderBot", new Color(0.90f, 0.72f, 0.30f, 1f));
            SetAnchors(tbBorderBot, 0f, 0f, 1f, 0.05f);
            tbBorderBot.AddComponent<LayoutElement>().ignoreLayout = true;
            var tbBorderMid = AddPanel(topBar, "BorderMid", new Color(0.60f, 0.45f, 0.15f, 0.55f));
            SetAnchors(tbBorderMid, 0f, 0.05f, 1f, 0.09f);
            tbBorderMid.AddComponent<LayoutElement>().ignoreLayout = true;
            // Glass highlight
            var tbGlass = AddPanel(topBar, "GlassTop", new Color(0.35f, 0.28f, 0.45f, 0.12f));
            SetAnchors(tbGlass, 0f, 0.88f, 1f, 1f);
            tbGlass.AddComponent<LayoutElement>().ignoreLayout = true;

            // Back button — ornate, visible
            var backBtn = AddPanel(topBar, "BackBtn", new Color(0.30f, 0.22f, 0.15f, 1f));
            SetAnchors(backBtn, 0.01f, 0.10f, 0.14f, 0.90f);
            if (btnOrnateSpr != null) { backBtn.GetComponent<Image>().sprite = btnOrnateSpr; backBtn.GetComponent<Image>().type = Image.Type.Sliced; backBtn.GetComponent<Image>().color = new Color(0.55f, 0.45f, 0.38f, 1f); }
            SetButtonFeedback(backBtn.AddComponent<Button>());
            AddSceneNav(backBtn, SceneName.Empire);
            var bkLbl = AddText(backBtn, "Lbl", "\u25C0 BACK", 12, TextAnchor.MiddleCenter);
            StretchToParent(bkLbl);
            bkLbl.GetComponent<Text>().color = Color.white;
            bkLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var bkSh = bkLbl.AddComponent<Shadow>();
            bkSh.effectColor = new Color(0, 0, 0, 0.92f);
            bkSh.effectDistance = new Vector2(0.5f, -0.5f);

            // Shield emblem — larger, with gold circle bg
            var shieldBg = AddPanel(topBar, "ShieldBg", new Color(0.55f, 0.42f, 0.18f, 0.40f));
            SetAnchors(shieldBg, 0.155f, 0.05f, 0.23f, 0.95f);
            if (shieldSpr != null) { var shieldImg = shieldBg.GetComponent<Image>(); shieldImg.sprite = shieldSpr; shieldImg.preserveAspect = true; shieldImg.color = new Color(1f, 0.92f, 0.70f, 1f); }
            AddOutlinePanel(shieldBg, new Color(0.88f, 0.70f, 0.28f, 0.60f));

            // Alliance name — BRIGHT gold, unmissable
            var aName = AddText(topBar, "AllianceName", "IRON LEGION", 18, TextAnchor.MiddleLeft);
            SetAnchors(aName, 0.24f, 0.15f, 0.72f, 0.90f);
            aName.GetComponent<Text>().color = new Color(1f, 0.88f, 0.42f, 1f);
            aName.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var anSh = aName.AddComponent<Shadow>();
            anSh.effectColor = new Color(0, 0, 0, 0.95f);
            anSh.effectDistance = new Vector2(1.5f, -1.5f);
            var anOutline = aName.AddComponent<Outline>();
            anOutline.effectColor = new Color(0.45f, 0.32f, 0.08f, 0.5f);
            anOutline.effectDistance = new Vector2(0.8f, -0.8f);

            // Power + Member count — right side, brighter
            var pwrLabel = AddText(topBar, "Power", "\u2694 1.2M", 12, TextAnchor.MiddleRight);
            SetAnchors(pwrLabel, 0.72f, 0.50f, 0.98f, 0.95f);
            pwrLabel.GetComponent<Text>().color = new Color(1f, 0.65f, 0.30f, 1f);
            pwrLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var pwrSh = pwrLabel.AddComponent<Shadow>();
            pwrSh.effectColor = new Color(0, 0, 0, 0.90f);
            pwrSh.effectDistance = new Vector2(0.5f, -0.5f);
            var memLabel = AddText(topBar, "Members", "42/50 Members", 11, TextAnchor.MiddleRight);
            SetAnchors(memLabel, 0.72f, 0.05f, 0.98f, 0.50f);
            memLabel.GetComponent<Text>().color = TextLight;
            var memSh = memLabel.AddComponent<Shadow>(); memSh.effectColor = new Color(0, 0, 0, 0.75f); memSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === WAR STATUS STRIP — between top bar and tabs, red urgency ===
            var warStrip = AddPanel(canvas, "WarStatusStrip", new Color(0.50f, 0.06f, 0.04f, 0.95f));
            SetAnchors(warStrip, 0f, 0.912f, 1f, 0.930f);
            // Red pulsing glow border top
            var wsGlowTop = AddPanel(warStrip, "GlowTop", new Color(0.90f, 0.20f, 0.10f, 0.40f));
            SetAnchors(wsGlowTop, 0f, 0.85f, 1f, 1f);
            wsGlowTop.AddComponent<LayoutElement>().ignoreLayout = true;
            // Gold bottom accent
            var wsBotLine = AddPanel(warStrip, "BotLine", new Color(0.75f, 0.55f, 0.18f, 0.50f));
            SetAnchors(wsBotLine, 0f, 0f, 1f, 0.08f);
            wsBotLine.AddComponent<LayoutElement>().ignoreLayout = true;
            var warStripIcon = AddText(warStrip, "Icon", "\u2694", 12, TextAnchor.MiddleLeft);
            SetAnchors(warStripIcon, 0.02f, 0f, 0.08f, 1f);
            warStripIcon.GetComponent<Text>().color = new Color(1f, 0.70f, 0.25f, 1f);
            warStripIcon.GetComponent<Text>().fontStyle = FontStyle.Bold;
            warStripIcon.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            warStripIcon.GetComponent<Shadow>().effectDistance = new Vector2(0.3f, -0.3f);
            var warStripTxt = AddText(warStrip, "Text", "WAR: Iron Legion vs Shadow Pact  \u2022  Phase 2  \u2022  1d 08h", 11, TextAnchor.MiddleLeft);
            SetAnchors(warStripTxt, 0.08f, 0f, 0.92f, 1f);
            warStripTxt.GetComponent<Text>().color = new Color(1f, 0.88f, 0.60f, 1f);
            warStripTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
            warStripTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
            warStripTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.4f, -0.4f);

            // === TAB BAR — brighter segmented tabs ===
            var tabBar = AddPanel(canvas, "TabBar", new Color(0.08f, 0.06f, 0.14f, 0.98f));
            SetAnchors(tabBar, 0f, 0.845f, 1f, 0.912f);
            // Gold bottom border
            var tabBotBorder = AddPanel(tabBar, "BotBorder", new Color(0.80f, 0.62f, 0.24f, 0.75f));
            SetAnchors(tabBotBorder, 0f, 0f, 1f, 0.06f);
            tabBotBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            // Top gold accent
            var tabTopBorder = AddPanel(tabBar, "TopBorder", new Color(0.60f, 0.45f, 0.18f, 0.35f));
            SetAnchors(tabTopBorder, 0f, 0.94f, 1f, 1f);
            tabTopBorder.AddComponent<LayoutElement>().ignoreLayout = true;

            var tabLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 3;
            tabLayout.padding = new RectOffset(4, 4, 4, 6);
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;

            // Tabs — active is BRIGHT, inactive is readable
            string[] tabNames = { "CHAT", "MEMBERS", "WAR", "TERRITORY", "RANKS" };
            Color[] tabColors = { Teal, Purple, Blood, Ember, Gold };
            for (int t = 0; t < tabNames.Length; t++)
            {
                bool active = t == 0;
                Color c = tabColors[t];
                var tab = AddPanel(tabBar, $"Tab_{tabNames[t]}", active ? new Color(c.r * 0.35f + 0.08f, c.g * 0.35f + 0.06f, c.b * 0.35f + 0.08f, 0.95f) : new Color(0.10f, 0.08f, 0.16f, 0.80f));
                tab.AddComponent<LayoutElement>().flexibleWidth = 1;
                if (btnOrnateSpr != null && active) { tab.GetComponent<Image>().sprite = btnOrnateSpr; tab.GetComponent<Image>().type = Image.Type.Sliced; tab.GetComponent<Image>().color = new Color(c.r * 0.50f + 0.12f, c.g * 0.50f + 0.10f, c.b * 0.50f + 0.12f, 1f); }
                SetButtonFeedback(tab.AddComponent<Button>());
                AddSceneNav(tab, SceneName.Alliance);
                // Active indicator — thick colored bar at bottom
                if (active) { var ind = AddPanel(tab, "ActiveBar", c); SetAnchors(ind, 0.05f, 0f, 0.95f, 0.14f); ind.AddComponent<LayoutElement>().ignoreLayout = true; }
                // Inactive — visible border with warm tint
                if (!active) { AddOutlinePanel(tab, new Color(0.38f, 0.30f, 0.22f, 0.40f)); }
                var tLbl = AddText(tab, "Label", tabNames[t], 12, TextAnchor.MiddleCenter);
                StretchToParent(tLbl);
                tLbl.GetComponent<Text>().color = active ? new Color(Mathf.Min(1f, c.r + 0.35f), Mathf.Min(1f, c.g + 0.35f), Mathf.Min(1f, c.b + 0.35f), 1f) : new Color(0.72f, 0.68f, 0.60f, 0.90f);
                tLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var tSh = tLbl.AddComponent<Shadow>(); tSh.effectColor = new Color(0, 0, 0, 0.92f); tSh.effectDistance = new Vector2(1f, -1f);
                if (active) { var tOut = tLbl.AddComponent<Outline>(); tOut.effectColor = new Color(0, 0, 0, 0.4f); tOut.effectDistance = new Vector2(0.5f, -0.5f); }
                // Notification badges — red circle with count
                int[] tabBadges = { 0, 0, 3, 1, 0 }; // WAR=3, TERRITORY=1
                if (tabBadges[t] > 0)
                {
                    var badge = AddPanel(tab, "Badge", new Color(0.88f, 0.15f, 0.12f, 1f));
                    SetAnchors(badge, 0.72f, 0.60f, 0.96f, 0.94f);
                    badge.AddComponent<LayoutElement>().ignoreLayout = true;
                    var badgeSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
                    if (badgeSpr != null) { badge.GetComponent<Image>().sprite = badgeSpr; badge.GetComponent<Image>().type = Image.Type.Sliced; badge.GetComponent<Image>().color = new Color(0.92f, 0.15f, 0.10f, 1f); }
                    AddOutlinePanel(badge, new Color(0.60f, 0.08f, 0.06f, 0.6f));
                    var bTxt = AddText(badge, "Count", tabBadges[t].ToString(), 10, TextAnchor.MiddleCenter);
                    StretchToParent(bTxt);
                    bTxt.GetComponent<Text>().color = Color.white;
                    bTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    var bSh = bTxt.AddComponent<Shadow>(); bSh.effectColor = new Color(0, 0, 0, 0.5f); bSh.effectDistance = new Vector2(0.3f, -0.3f);
                }
            }

            // === ALLIANCE INFO STRIP — territory + level + gifts between tabs and chat ===
            var alliInfoStrip = AddPanel(canvas, "AlliInfoStrip", new Color(0.06f, 0.04f, 0.12f, 0.95f));
            SetAnchors(alliInfoStrip, 0f, 0.810f, 1f, 0.850f);
            // Subtle gold bottom accent
            var aisBot = AddPanel(alliInfoStrip, "BotAccent", new Color(0.55f, 0.42f, 0.18f, 0.30f));
            SetAnchors(aisBot, 0f, 0f, 1f, 0.06f);
            aisBot.AddComponent<LayoutElement>().ignoreLayout = true;
            // Inner content
            var aisLvl = AddText(alliInfoStrip, "Level", "\u2B50 Lv.12", 11, TextAnchor.MiddleCenter);
            SetAnchors(aisLvl, 0.02f, 0.1f, 0.20f, 0.9f);
            aisLvl.GetComponent<Text>().color = new Color(1f, 0.88f, 0.42f, 0.90f);
            aisLvl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var aisLvlSh = aisLvl.AddComponent<Shadow>(); aisLvlSh.effectColor = new Color(0, 0, 0, 0.8f); aisLvlSh.effectDistance = new Vector2(0.5f, -0.5f);
            var aisTerr = AddText(alliInfoStrip, "Territory", "\U0001F3F0 8 Territories", 11, TextAnchor.MiddleCenter);
            SetAnchors(aisTerr, 0.22f, 0.1f, 0.52f, 0.9f);
            aisTerr.GetComponent<Text>().color = TextLight;
            aisTerr.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var aisTerrSh = aisTerr.AddComponent<Shadow>(); aisTerrSh.effectColor = new Color(0, 0, 0, 0.7f); aisTerrSh.effectDistance = new Vector2(0.5f, -0.5f);
            var aisGifts = AddText(alliInfoStrip, "Gifts", "\U0001F381 3 Gifts", 11, TextAnchor.MiddleCenter);
            SetAnchors(aisGifts, 0.54f, 0.1f, 0.74f, 0.9f);
            aisGifts.GetComponent<Text>().color = new Color(0.55f, 0.88f, 0.55f, 0.90f);
            aisGifts.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var aisGiftsSh = aisGifts.AddComponent<Shadow>(); aisGiftsSh.effectColor = new Color(0, 0, 0, 0.7f); aisGiftsSh.effectDistance = new Vector2(0.5f, -0.5f);
            var aisOnline = AddText(alliInfoStrip, "Online", "\u25CF 18 Online", 11, TextAnchor.MiddleRight);
            SetAnchors(aisOnline, 0.76f, 0.1f, 0.98f, 0.9f);
            aisOnline.GetComponent<Text>().color = new Color(0.30f, 0.82f, 0.40f, 0.90f);
            aisOnline.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var aisOnlineSh = aisOnline.AddComponent<Shadow>(); aisOnlineSh.effectColor = new Color(0, 0, 0, 0.7f); aisOnlineSh.effectDistance = new Vector2(0.5f, -0.5f);

            // === CHAT AREA — ornate framed panel, brighter frame ===
            var chatPanel = AddPanel(canvas, "ChatPanel", new Color(0.12f, 0.10f, 0.20f, 1f));
            SetAnchors(chatPanel, 0.015f, 0.13f, 0.985f, 0.810f);
            if (ornateSpr != null) { chatPanel.GetComponent<Image>().sprite = ornateSpr; chatPanel.GetComponent<Image>().type = Image.Type.Sliced; chatPanel.GetComponent<Image>().color = new Color(0.55f, 0.48f, 0.42f, 1f); }
            else { AddOutlinePanel(chatPanel, new Color(0.60f, 0.48f, 0.22f, 0.60f)); }
            // Inner glass highlight for depth
            var chatHighlight = AddPanel(chatPanel, "GlassTop", new Color(0.20f, 0.18f, 0.28f, 0.15f));
            SetAnchors(chatHighlight, 0.01f, 0.92f, 0.99f, 1f);
            // Inner shadow at bottom for depth
            var chatBotShadow = AddPanel(chatPanel, "BotShadow", new Color(0.02f, 0.01f, 0.04f, 0.25f));
            SetAnchors(chatBotShadow, 0.01f, 0f, 0.99f, 0.03f);

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

                var row = AddPanel(chatPanel, $"Msg_{i}", new Color(i % 2 == 0 ? 0.16f : 0.11f, i % 2 == 0 ? 0.14f : 0.09f, i % 2 == 0 ? 0.28f : 0.20f, i % 2 == 0 ? 0.98f : 0.92f));
                SetAnchors(row, 0.015f, yBot, 0.985f, yTop);
                AddOutlinePanel(row, new Color(0.65f, 0.52f, 0.30f, i % 2 == 0 ? 0.60f : 0.40f));
                // Glass highlight at top for lit-from-above look
                var rowHighlight = AddPanel(row, "TopHL", new Color(0.50f, 0.45f, 0.62f, 0.18f));
                SetAnchors(rowHighlight, 0.02f, 0.82f, 0.98f, 1f);
                rowHighlight.AddComponent<LayoutElement>().ignoreLayout = true;
                // Inner bottom shadow for depth
                var rowBotShadow = AddPanel(row, "BotShadow", new Color(0.02f, 0.01f, 0.04f, 0.20f));
                SetAnchors(rowBotShadow, 0.02f, 0f, 0.98f, 0.12f);
                rowBotShadow.AddComponent<LayoutElement>().ignoreLayout = true;
                // Left accent bar in sender color — thick and vivid
                var accent = AddPanel(row, "Accent", new Color(chatMsgs[i].sColor.r, chatMsgs[i].sColor.g, chatMsgs[i].sColor.b, 0.90f));
                SetAnchors(accent, 0f, 0.04f, 0.02f, 0.96f);

                // Avatar — use hero portrait sprite if available
                var chatPortraitMap = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Kaelen", "kael_ashwalker" }, { "Vorra", "vex_shadowstrike" },
                    { "Seraphyn", "sera_dawnblade" }, { "Mordoc", "grim_bonecrusher" },
                    { "Lyra", "lyra_thornveil" }, { "Commander", "thane_ironhold" },
                };
                Sprite chatPortSpr = null;
                if (chatPortraitMap.TryGetValue(chatMsgs[i].sender, out var cpKey))
                    chatPortSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Characters/Heroes/{cpKey}_portrait.png");

                var avatar = AddPanel(row, "Avatar", chatPortSpr != null ? Color.white : new Color(chatMsgs[i].sColor.r * 0.50f, chatMsgs[i].sColor.g * 0.50f, chatMsgs[i].sColor.b * 0.50f, 0.90f));
                SetAnchors(avatar, 0.018f, 0.06f, 0.09f, 0.94f);
                if (chatPortSpr != null)
                {
                    avatar.GetComponent<Image>().sprite = chatPortSpr;
                    avatar.GetComponent<Image>().preserveAspect = true;
                }
                else
                {
                    var initial = AddText(avatar, "Init", chatMsgs[i].sender.Substring(0, 1), 13, TextAnchor.MiddleCenter);
                    StretchToParent(initial);
                    initial.GetComponent<Text>().color = new Color(1f, 1f, 1f, 0.90f);
                    initial.GetComponent<Text>().fontStyle = FontStyle.Bold;
                    var inSh = initial.AddComponent<Shadow>(); inSh.effectColor = new Color(0, 0, 0, 0.7f); inSh.effectDistance = new Vector2(0.5f, -0.5f);
                }
                AddOutlinePanel(avatar, new Color(chatMsgs[i].sColor.r * 0.65f, chatMsgs[i].sColor.g * 0.65f, chatMsgs[i].sColor.b * 0.65f, 0.45f));
                // Online indicator dot — green for recent messages
                bool isOnline = i < 5; // first 5 senders are "online"
                var onlineDot = AddPanel(avatar, "OnlineDot", isOnline ? new Color(0.20f, 0.80f, 0.30f, 1f) : new Color(0.45f, 0.40f, 0.35f, 0.6f));
                SetAnchors(onlineDot, 0.70f, 0.70f, 1f, 1f);
                onlineDot.AddComponent<LayoutElement>().ignoreLayout = true;
                if (isOnline) AddOutlinePanel(onlineDot, new Color(0.10f, 0.50f, 0.15f, 0.5f));

                // Sender — colored bold, with role tag
                string[] chatRoles = { "R4", "R4", "R5", "R3", "R4", "R3", "R4" };
                var sender = AddText(row, "Sender", $"[{chatRoles[i]}] {chatMsgs[i].sender}", 13, TextAnchor.MiddleLeft);
                SetAnchors(sender, 0.10f, 0.52f, 0.50f, 0.98f);
                sender.GetComponent<Text>().color = new Color(Mathf.Min(1f, chatMsgs[i].sColor.r + 0.15f), Mathf.Min(1f, chatMsgs[i].sColor.g + 0.15f), Mathf.Min(1f, chatMsgs[i].sColor.b + 0.15f), 1f);
                sender.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var sSh = sender.AddComponent<Shadow>();
                sSh.effectColor = new Color(0, 0, 0, 0.90f);
                sSh.effectDistance = new Vector2(1, -1);

                // Timestamp — brighter
                var ts = AddText(row, "Time", chatMsgs[i].time, 10, TextAnchor.MiddleRight);
                SetAnchors(ts, 0.88f, 0.55f, 0.99f, 0.98f);
                ts.GetComponent<Text>().color = TextMid;
                var tsSh = ts.AddComponent<Shadow>(); tsSh.effectColor = new Color(0, 0, 0, 0.7f); tsSh.effectDistance = new Vector2(0.5f, -0.5f);

                // Message text — BRIGHTER white
                var msgTxt = AddText(row, "Text", chatMsgs[i].msg, 12, TextAnchor.MiddleLeft);
                SetAnchors(msgTxt, 0.10f, 0.02f, 0.98f, 0.55f);
                msgTxt.GetComponent<Text>().color = TextWhite;
                var mtSh = msgTxt.AddComponent<Shadow>(); mtSh.effectColor = new Color(0, 0, 0, 0.65f); mtSh.effectDistance = new Vector2(0.5f, -0.5f);
            }

            // === QUICK PHRASES — preset message buttons above input ===
            var quickPhraseBar = AddPanel(canvas, "QuickPhrases", new Color(0.06f, 0.04f, 0.10f, 0.85f));
            SetAnchors(quickPhraseBar, 0f, 0.13f, 1f, 0.165f);
            var qpBotLine = AddPanel(quickPhraseBar, "BotLine", new Color(0.45f, 0.35f, 0.18f, 0.20f));
            SetAnchors(qpBotLine, 0f, 0f, 1f, 0.06f);
            qpBotLine.AddComponent<LayoutElement>().ignoreLayout = true;
            var qpLayout = quickPhraseBar.AddComponent<HorizontalLayoutGroup>();
            qpLayout.spacing = 4;
            qpLayout.padding = new RectOffset(6, 6, 3, 3);
            qpLayout.childForceExpandWidth = false;
            qpLayout.childForceExpandHeight = true;
            string[] phrases = { "Rally!", "Help!", "GG", "On my way", "Attack!" };
            Color[] phraseColors = { Blood, Ember, Teal, Sky, Blood };
            for (int p = 0; p < phrases.Length; p++)
            {
                var pill = AddPanel(quickPhraseBar, $"QP_{p}", new Color(phraseColors[p].r * 0.30f + 0.06f, phraseColors[p].g * 0.30f + 0.05f, phraseColors[p].b * 0.30f + 0.06f, 0.92f));
                pill.AddComponent<LayoutElement>().preferredWidth = 70;
                if (btnOrnateSpr != null) { pill.GetComponent<Image>().sprite = btnOrnateSpr; pill.GetComponent<Image>().type = Image.Type.Sliced; pill.GetComponent<Image>().color = new Color(phraseColors[p].r * 0.40f + 0.10f, phraseColors[p].g * 0.40f + 0.08f, phraseColors[p].b * 0.40f + 0.10f, 1f); }
                SetButtonFeedback(pill.AddComponent<Button>());
                AddSceneNav(pill, SceneName.Alliance);
                // Inner glow accent
                var qpGlow = AddPanel(pill, "Glow", new Color(phraseColors[p].r, phraseColors[p].g, phraseColors[p].b, 0.12f));
                SetAnchors(qpGlow, 0.05f, 0.55f, 0.95f, 0.92f);
                qpGlow.AddComponent<LayoutElement>().ignoreLayout = true;
                var qpTxt = AddText(pill, "Txt", phrases[p], 10, TextAnchor.MiddleCenter);
                StretchToParent(qpTxt);
                qpTxt.GetComponent<Text>().color = new Color(Mathf.Min(1f, phraseColors[p].r + 0.35f), Mathf.Min(1f, phraseColors[p].g + 0.35f), Mathf.Min(1f, phraseColors[p].b + 0.35f), 1f);
                qpTxt.GetComponent<Text>().fontStyle = FontStyle.Bold;
                qpTxt.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.80f);
                qpTxt.GetComponent<Shadow>().effectDistance = new Vector2(0.4f, -0.4f);
            }

            // === INPUT BAR — ornate with gold trim ===
            var inputBar = AddPanel(canvas, "InputBar", new Color(0.08f, 0.06f, 0.12f, 0.98f));
            SetAnchors(inputBar, 0f, 0.09f, 1f, 0.13f);
            // Gold top trim — brighter
            var inTopBorder = AddPanel(inputBar, "TopBorder", new Color(0.75f, 0.58f, 0.22f, 0.65f));
            SetAnchors(inTopBorder, 0f, 0.94f, 1f, 1f);
            inTopBorder.AddComponent<LayoutElement>().ignoreLayout = true;
            // Gold bottom trim
            var inBotBorder = AddPanel(inputBar, "BotBorder", new Color(0.60f, 0.45f, 0.18f, 0.35f));
            SetAnchors(inBotBorder, 0f, 0f, 1f, 0.06f);
            inBotBorder.AddComponent<LayoutElement>().ignoreLayout = true;

            // Text field — inset dark with visible gold border
            var inputField = AddPanel(inputBar, "InputField", new Color(0.05f, 0.035f, 0.08f, 0.92f));
            SetAnchors(inputField, 0.02f, 0.10f, 0.78f, 0.90f);
            AddOutlinePanel(inputField, new Color(0.50f, 0.40f, 0.18f, 0.45f));
            var phTxt = AddText(inputField, "Placeholder", "  Type a message...", 11, TextAnchor.MiddleLeft);
            StretchToParent(phTxt);
            phTxt.GetComponent<Text>().color = TextDim;
            phTxt.GetComponent<Text>().fontStyle = FontStyle.Italic;

            // Typing indicator
            var typingTxt = AddText(inputBar, "Typing", "Vorra is typing...", 10, TextAnchor.MiddleLeft);
            SetAnchors(typingTxt, 0.03f, 0.88f, 0.60f, 1f);
            typingTxt.GetComponent<Text>().color = new Color(0.55f, 0.50f, 0.42f, 0.70f);
            typingTxt.GetComponent<Text>().fontStyle = FontStyle.Italic;
            typingTxt.AddComponent<LayoutElement>().ignoreLayout = true;

            // Emoji button — compact, next to input
            var emojiBtn = AddPanel(inputBar, "EmojiBtn", new Color(0.18f, 0.14f, 0.28f, 0.85f));
            SetAnchors(emojiBtn, 0.78f, 0.12f, 0.86f, 0.88f);
            SetButtonFeedback(emojiBtn.AddComponent<Button>());
            AddSceneNav(emojiBtn, SceneName.Alliance);
            AddOutlinePanel(emojiBtn, new Color(0.45f, 0.38f, 0.22f, 0.35f));
            var emojiIcon = AddText(emojiBtn, "Icon", "\U0001F600", 14, TextAnchor.MiddleCenter);
            StretchToParent(emojiIcon);

            // Send button — ornate gold-teal with visible styling
            var sendBtn = AddPanel(inputBar, "SendBtn", new Color(0.15f, 0.60f, 0.55f, 1f));
            SetAnchors(sendBtn, 0.87f, 0.08f, 0.98f, 0.92f);
            if (btnOrnateSpr != null) { sendBtn.GetComponent<Image>().sprite = btnOrnateSpr; sendBtn.GetComponent<Image>().type = Image.Type.Sliced; sendBtn.GetComponent<Image>().color = new Color(0.18f, 0.62f, 0.55f, 1f); }
            SetButtonFeedback(sendBtn.AddComponent<Button>());
            AddSceneNav(sendBtn, SceneName.Alliance);
            var sendLbl = AddText(sendBtn, "Lbl", "SEND", 12, TextAnchor.MiddleCenter);
            StretchToParent(sendLbl);
            sendLbl.GetComponent<Text>().color = Color.white;
            sendLbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var sendSh = sendLbl.AddComponent<Shadow>();
            sendSh.effectColor = new Color(0, 0, 0, 0.7f);
            sendSh.effectDistance = new Vector2(1, -1);

            // === BOTTOM ACTION BAR — ornate 4-button bar, brighter ===
            var bottomBar = AddPanel(canvas, "BottomBar", new Color(0.10f, 0.07f, 0.16f, 0.98f));
            SetAnchors(bottomBar, 0f, 0f, 1f, 0.09f);
            // Bright gold border
            var bbTop = AddPanel(bottomBar, "TopBorder", new Color(0.90f, 0.72f, 0.30f, 1f));
            SetAnchors(bbTop, 0f, 0.96f, 1f, 1f);
            bbTop.AddComponent<LayoutElement>().ignoreLayout = true;
            var bbMid = AddPanel(bottomBar, "MidBorder", new Color(0.65f, 0.48f, 0.18f, 0.55f));
            SetAnchors(bbMid, 0f, 0.90f, 1f, 0.96f);
            bbMid.AddComponent<LayoutElement>().ignoreLayout = true;
            var bbGlow = AddPanel(bottomBar, "TopGlow", new Color(0.60f, 0.45f, 0.18f, 0.20f));
            SetAnchors(bbGlow, 0f, 0.80f, 1f, 0.90f);
            bbGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            // Glass highlight
            var bbGlass = AddPanel(bottomBar, "Glass", new Color(0.25f, 0.20f, 0.35f, 0.10f));
            SetAnchors(bbGlass, 0f, 0.85f, 1f, 1f);
            bbGlass.AddComponent<LayoutElement>().ignoreLayout = true;

            var bbLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            bbLayout.spacing = 6;
            bbLayout.padding = new RectOffset(8, 8, 8, 8);
            bbLayout.childForceExpandWidth = true;
            bbLayout.childForceExpandHeight = true;

            var donateBtn = AddOrnateToolbarBtn(bottomBar, "DonateBtn", "DONATE", Gold, btnOrnateSpr);
            AddSceneNav(donateBtn, SceneName.Alliance);
            var warBtn = AddOrnateToolbarBtn(bottomBar, "WarBtn", "DECLARE\nWAR", Blood, btnOrnateSpr);
            AddSceneNav(warBtn, SceneName.Alliance);
            var recruitBtn = AddOrnateToolbarBtn(bottomBar, "RecruitBtn", "RECRUIT", Teal, btnOrnateSpr);
            AddSceneNav(recruitBtn, SceneName.Alliance);
            var shopBtn = AddOrnateToolbarBtn(bottomBar, "ShopBtn", "ALLIANCE\nSHOP", Purple, btnOrnateSpr);
            AddSceneNav(shopBtn, SceneName.Alliance);

            SaveScene();
            Debug.Log("[SceneUIGenerator] Alliance scene: premium social hub v2");
        }

        // ===================================================================
        // HELPER METHODS — Complex widgets
        // ===================================================================


        /// <summary>Flat resource icon — 20px outer circle bg + inner sprite, compact like P&C.</summary>
        static void AddResIconFlat(GameObject parent, string resName, Color accentColor)
        {
            // Icon container — no background, just the sprite
            var outer = new GameObject($"Icon_{resName}", typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(parent.transform, false);
            var le = outer.AddComponent<LayoutElement>();
            le.preferredWidth = 72;
            le.preferredHeight = 72;
            le.minWidth = 60;
            le.minHeight = 60;
            le.flexibleWidth = 0;

            string spritePath = $"Assets/Art/UI/Production/icon_{resName.ToLower()}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var img = outer.GetComponent<Image>();
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

        /// <summary>Flat resource amount — bold white text, left-aligned.</summary>
        static void AddResAmountFlat(GameObject parent, string resName, string amount)
        {
            var amtGo = AddText(parent, $"{resName}Amt", amount, 32, TextAnchor.MiddleLeft);
            var txt = amtGo.GetComponent<Text>();
            txt.color = new Color(0.98f, 0.96f, 0.92f, 1f);
            txt.fontStyle = FontStyle.Bold;
            var shadow = amtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.95f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var le = amtGo.AddComponent<LayoutElement>();
            le.minWidth = 50;
            le.preferredWidth = 70;
            le.flexibleWidth = 1;
        }

        /// <summary>Visible | divider between resource pairs in the resource bar.</summary>
        static void AddResSeparator(GameObject parent)
        {
            var sep = new GameObject("ResSep", typeof(RectTransform), typeof(Image));
            sep.transform.SetParent(parent.transform, false);
            sep.GetComponent<Image>().color = new Color(0.65f, 0.58f, 0.45f, 0.50f);
            var le = sep.AddComponent<LayoutElement>();
            le.preferredWidth = 1;
            le.minWidth = 1;
            le.preferredHeight = 30;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;
        }

        /// <summary>Left sidebar queue slot — square icon with progress bar/timer below.</summary>
        static void AddQueueSlot(GameObject parent, string name, string label, string status,
            Color color, bool active, float yMin, float yMax)
        {
            var slot = AddPanel(parent, name, new Color(0, 0, 0, 0));
            SetAnchors(slot, 0f, yMin, 1f, yMax);
            SetButtonFeedback(slot.AddComponent<Button>());
            AddSceneNav(slot, SceneName.Empire);

            // Icon area — top 82% of the square, semi-transparent so city shows through
            var iconArea = AddPanel(slot, "IconArea", new Color(0.10f, 0.07f, 0.16f, 0.55f));
            SetAnchors(iconArea, 0f, 0.18f, 1f, 1f);
            // Subtle purple border
            AddOutlinePanel(iconArea, new Color(0.30f, 0.22f, 0.42f, 0.60f));

            // Sprite icon — fills 90% of icon area
            var icon = AddPanel(iconArea, "Icon", Color.white);
            SetAnchors(icon, 0.05f, 0.05f, 0.95f, 0.95f);
            string spriteKey = label.ToLower() switch {
                "build" => "icon_build", "research" => "icon_research", "training" => "icon_training", _ => null
            };
            bool spriteLoaded = false;
            if (spriteKey != null)
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{spriteKey}.png");
                if (spr != null)
                {
                    var img = icon.GetComponent<Image>();
                    img.sprite = spr;
                    img.preserveAspect = true;
                    img.color = active ? new Color(1f, 0.95f, 0.85f, 1f) : new Color(0.75f, 0.72f, 0.65f, 0.80f);
                    spriteLoaded = true;
                }
            }
            if (!spriteLoaded)
            {
                icon.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                var letterText = AddText(iconArea, "Letter", label[..1], 20, TextAnchor.MiddleCenter);
                StretchToParent(letterText);
                letterText.GetComponent<Text>().color = active ? Color.white : new Color(0.72f, 0.68f, 0.60f, 0.80f);
                letterText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            }

            // Progress bar — bottom 16%, solid black bg with green fill
            var progBg = AddPanel(slot, "ProgressBar", new Color(0f, 0f, 0f, 1f));
            SetAnchors(progBg, 0f, 0f, 1f, 0.16f);

            if (active)
            {
                var progFill = AddPanel(progBg, "Fill", new Color(0.20f, 0.78f, 0.35f, 1f));
                SetAnchors(progFill, 0f, 0f, 0.35f, 1f);
            }

            // Timer / IDLE text centered inside the progress bar
            var timerText = AddText(progBg, "Timer", status, 13, TextAnchor.MiddleCenter);
            StretchToParent(timerText);
            timerText.GetComponent<Text>().color = active ? new Color(1f, 1f, 1f, 1f) : new Color(0.55f, 0.52f, 0.46f, 0.85f);
            timerText.GetComponent<Text>().fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var timerShadow = timerText.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.8f);
            timerShadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>P&C-style event button — ornate frame with icon, glow, and timer.</summary>
        static void AddEventButton(GameObject parent, string name, string label, Color color,
            float xMin, float yMin, float xMax, float yMax, string timer, SceneName? targetScene = null)
        {
            // Bright button panel — vivid colored base per event type (P&C style)
            var btn = AddPanel(parent, name, color);
            SetAnchors(btn, xMin, yMin, xMax, yMax);
            var btnImg = btn.GetComponent<Image>();
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
            if (ornateSpr != null)
            {
                btnImg.sprite = ornateSpr;
                btnImg.type = Image.Type.Sliced;
                // Bright saturated tint — event-colored, NOT dark
                btnImg.color = new Color(
                    Mathf.Clamp01(color.r * 0.7f + 0.30f),
                    Mathf.Clamp01(color.g * 0.7f + 0.25f),
                    Mathf.Clamp01(color.b * 0.7f + 0.25f), 1f);
            }
            AddOutlinePanel(btn, new Color(0.85f, 0.68f, 0.28f, 0.60f));
            SetButtonFeedback(btn.AddComponent<Button>());
            if (targetScene.HasValue)
                AddSceneNav(btn, targetScene.Value);

            // Dark inner fill — creates contrast for icon readability
            var innerFill = AddPanel(btn, "InnerFill", new Color(0.08f, 0.05f, 0.12f, 0.50f));
            SetAnchors(innerFill, 0.08f, 0.08f, 0.92f, 0.92f);
            // Center color glow — vibrant event-colored spotlight
            var centerGlow = AddPanel(btn, "CenterGlow", new Color(
                Mathf.Clamp01(color.r * 0.5f + 0.15f),
                Mathf.Clamp01(color.g * 0.5f + 0.10f),
                Mathf.Clamp01(color.b * 0.5f + 0.10f), 0.45f));
            SetAnchors(centerGlow, 0.12f, 0.20f, 0.88f, 0.80f);

            // Icon — transparent background, fills most of button area
            var icon = AddPanel(btn, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.08f, 0.18f, 0.92f, 0.84f);
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
            var lbl = AddText(btn, "Label", label, 11, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0.02f, 1f, 0.25f);
            lbl.GetComponent<Text>().color = Color.white;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.92f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var lblOutline = lbl.AddComponent<Outline>();
            lblOutline.effectColor = new Color(0, 0, 0, 0.5f);
            lblOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Timer badge (if any) — top strip
            if (!string.IsNullOrEmpty(timer))
            {
                var timerBg = AddPanel(btn, "TimerBg", new Color(0.02f, 0.02f, 0.06f, 0.88f));
                SetAnchors(timerBg, 0.02f, 0.82f, 0.98f, 1f);
                var timerText = AddText(timerBg, "Timer", timer, 11, TextAnchor.MiddleCenter);
                StretchToParent(timerText);
                timerText.GetComponent<Text>().color = new Color(0.3f, 1f, 0.75f, 1f);
                timerText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var timerShadow = timerText.AddComponent<Shadow>();
                timerShadow.effectColor = new Color(0, 0, 0, 0.85f);
                timerShadow.effectDistance = new Vector2(1f, -1f);
            }
        }

        /// <summary>P&C-style nav bar item — real sprite icons, circular badges, premium feel.</summary>
        static void AddNavItem(GameObject parent, string name, string label, Color color, bool active, int badgeCount, SceneName? targetScene = null)
        {
            var item = AddPanel(parent, name, new Color(0, 0, 0, 0));
            item.AddComponent<LayoutElement>().flexibleWidth = 1;
            SetButtonFeedback(item.AddComponent<Button>());
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

            // Icon — transparent bg, production sprite only, 1.5x scale (both X and Y)
            var icon = AddPanel(item, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.02f, 0.16f, 0.98f, 1.02f);

            // Map to dedicated production sprites — each is unique and instantly recognizable
            string spriteKey = label.ToLower() switch {
                "world"    => "nav_world",      // globe with compass
                "hero"     => "nav_heroes",     // ornate helmet
                "quest"    => "icon_quest",     // glowing quest scroll
                "bag"      => "nav_shop",       // treasure chest
                "mail"     => "icon_mail",      // wax-sealed envelope
                "alliance" => "nav_alliance",   // shield with banner
                "rank"     => "icon_currency_gold",
                _          => null
            };

            Color activeTint = new Color(1f, 0.94f, 0.76f, 1f);       // bright warm gold
            Color inactiveTint = new Color(0.88f, 0.80f, 0.65f, 0.92f); // brighter bronze for visibility

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
                SetAnchors(badge, 0.67f, 0.76f, 1.12f, 1.12f);
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

                var badgeText = AddText(badge, "Count", badgeCount.ToString(), 14, TextAnchor.MiddleCenter);
                StretchToParent(badgeText);
                badgeText.GetComponent<Text>().color = Color.white;
                badgeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var badgeShadow = badgeText.AddComponent<Shadow>();
                badgeShadow.effectColor = new Color(0, 0, 0, 0.6f);
                badgeShadow.effectDistance = new Vector2(0.5f, -0.5f);
            }

            // Label — warm gold active, visible silver-gold inactive, 16px
            var lbl = AddText(item, "Label", label, 16, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0f, 0.0f, 1f, 0.28f);
            lbl.GetComponent<Text>().color = active
                ? new Color(1f, 0.95f, 0.75f, 1f)
                : new Color(0.80f, 0.75f, 0.66f, 0.92f);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.92f);
            lblShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void AddHeroStatusPanel(GameObject parent, string heroName, float hpPct, Color heroColor, bool isPlayer)
        {
            var panel = AddPanel(parent, heroName, new Color(0.04f, 0.03f, 0.08f, 0.94f));
            panel.AddComponent<LayoutElement>().preferredHeight = 62;

            // Ornate panel frame
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/panel_ornate_gen.png");
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

            // Portrait — use actual hero portrait sprite if available
            var portraitNameMap = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Kaelen", "kael_ashwalker" }, { "Vorra", "vex_shadowstrike" },
                { "Seraphyn", "sera_dawnblade" }, { "Mordoc", "grim_bonecrusher" },
                { "Lyra", "lyra_thornveil" }, { "Skaros", "nyx_stormcaller" },
            };
            Sprite portraitSpr = null;
            if (portraitNameMap.TryGetValue(heroName, out var pKey))
                portraitSpr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Characters/Heroes/{pKey}_portrait.png");

            var portraitBg = AddPanel(panel, "PortraitBg", new Color(heroColor.r * 0.3f, heroColor.g * 0.3f, heroColor.b * 0.3f, 1f));
            SetAnchors(portraitBg, 0.03f, 0.08f, 0.28f, 0.92f);
            if (portraitSpr != null)
            {
                portraitBg.GetComponent<Image>().sprite = portraitSpr;
                portraitBg.GetComponent<Image>().preserveAspect = true;
                portraitBg.GetComponent<Image>().color = Color.white;
            }
            else
            {
                var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
                if (circleSpr != null) { portraitBg.GetComponent<Image>().sprite = circleSpr; portraitBg.GetComponent<Image>().type = Image.Type.Sliced; portraitBg.GetComponent<Image>().color = new Color(heroColor.r * 0.4f, heroColor.g * 0.4f, heroColor.b * 0.4f, 1f); }
                var initText = AddText(portraitBg, "Initial", heroName[..1], 16, TextAnchor.MiddleCenter);
                StretchToParent(initText);
                initText.GetComponent<Text>().color = new Color(heroColor.r + 0.2f, heroColor.g + 0.2f, heroColor.b + 0.2f, 0.9f);
                initText.GetComponent<Text>().fontStyle = FontStyle.Bold;
                var itSh = initText.AddComponent<Shadow>(); itSh.effectColor = new Color(0, 0, 0, 0.7f); itSh.effectDistance = new Vector2(0.5f, -0.5f);
            }
            AddOutlinePanel(portraitBg, new Color(0.72f, 0.56f, 0.22f, 0.70f));

            // Hero name with team icon
            string teamIcon = isPlayer ? "\u2694 " : "\u2620 "; // swords or skull
            var nameLabel = AddText(panel, "Name", teamIcon + heroName, 12, TextAnchor.MiddleLeft);
            SetAnchors(nameLabel, 0.31f, 0.55f, 0.98f, 0.95f);
            nameLabel.GetComponent<Text>().color = new Color(1f, 0.96f, 0.90f, 1f);
            nameLabel.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var nShadow = nameLabel.AddComponent<Shadow>();
            nShadow.effectColor = new Color(0, 0, 0, 0.90f);
            nShadow.effectDistance = new Vector2(1f, -1f);

            // HP percentage text — right-aligned above bar
            int hpCurrent = (int)(hpPct * 1000);
            var hpText = AddText(panel, "HpText", $"{(int)(hpPct * 100)}%", 11, TextAnchor.MiddleRight);
            SetAnchors(hpText, 0.68f, 0.55f, 0.98f, 0.95f);
            Color hpColor = hpPct > 0.5f ? BarHpGreen : hpPct > 0.25f ? Ember : BarHpRed;
            hpText.GetComponent<Text>().color = new Color(hpColor.r, hpColor.g, hpColor.b, 0.85f);
            hpText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var hpSh = hpText.AddComponent<Shadow>(); hpSh.effectColor = new Color(0, 0, 0, 0.7f); hpSh.effectDistance = new Vector2(0.5f, -0.5f);

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
            var artArea = AddPanel(card, "Art", new Color(color.r * 0.35f + 0.05f, color.g * 0.35f + 0.04f, color.b * 0.35f + 0.05f, 0.95f));
            SetAnchors(artArea, 0.08f, 0.42f, 0.92f, 0.80f);
            // Element symbol in art area
            string elemSymbol = type == "ATK" ? "\u2694" : type == "HEAL" ? "\u2665" : "\u2726"; // swords, heart, diamond
            var elemIcon = AddText(artArea, "ElemIcon", elemSymbol, 22, TextAnchor.MiddleCenter);
            StretchToParent(elemIcon);
            elemIcon.GetComponent<Text>().color = new Color(color.r, color.g, color.b, 0.55f);
            var elemSh = elemIcon.AddComponent<Shadow>();
            elemSh.effectColor = new Color(0, 0, 0, 0.6f);
            elemSh.effectDistance = new Vector2(1.5f, -1.5f);

            // Cost badge — circular with glow
            var costGlow = AddPanel(card, "CostGlow", new Color(0.15f, 0.40f, 0.85f, 0.20f));
            SetAnchors(costGlow, 0f, 0.82f, 0.26f, 1f);
            var costBadge = AddPanel(card, "CostBadge", new Color(0.12f, 0.35f, 0.75f, 1f));
            SetAnchors(costBadge, 0.03f, 0.84f, 0.23f, 0.98f);
            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Kenney/buttonRound_brown.png");
            if (circleSpr != null) { costBadge.GetComponent<Image>().sprite = circleSpr; costBadge.GetComponent<Image>().type = Image.Type.Sliced; costBadge.GetComponent<Image>().color = new Color(0.15f, 0.38f, 0.82f, 1f); }
            else { AddOutlinePanel(costBadge, new Color(0.55f, 0.72f, 1f, 0.5f)); }
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
            var typeText = AddText(typeBadge, "Type", type, 10, TextAnchor.MiddleCenter);
            StretchToParent(typeText);
            typeText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            typeText.GetComponent<Text>().color = Color.white;
            var typeSh = typeText.AddComponent<Shadow>();
            typeSh.effectColor = new Color(0, 0, 0, 0.8f);
            typeSh.effectDistance = new Vector2(0.5f, -0.5f);

            // Card name — gold for attacks, teal for heals
            var nameText = AddText(card, "Name", cardName, 11, TextAnchor.MiddleCenter);
            SetAnchors(nameText, 0.04f, 0.24f, 0.96f, 0.40f);
            nameText.GetComponent<Text>().color = new Color(Mathf.Min(1f, color.r + 0.20f), Mathf.Min(1f, color.g + 0.15f), Mathf.Min(1f, color.b + 0.15f), 1f);
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

        static void AddLegendItem(GameObject parent, string label, Color color, float yPos)
        {
            var dot = AddPanel(parent, $"Leg_{label}", color);
            SetAnchors(dot, 0.1f, yPos, 0.25f, yPos + 0.25f);
            var text = AddText(parent, $"LegText_{label}", label, 10, TextAnchor.MiddleLeft);
            SetAnchors(text, 0.3f, yPos, 0.95f, yPos + 0.25f);
            text.GetComponent<Text>().color = TextMid;
            var legSh = text.AddComponent<Shadow>();
            legSh.effectColor = new Color(0, 0, 0, 0.7f);
            legSh.effectDistance = new Vector2(0.5f, -0.5f);
        }

        // ===================================================================
        // HELPER METHODS — Basic widgets
        // ===================================================================

        static GameObject AddStyledButton(GameObject parent, string name, string label, Color bgColor, Color darkColor)
        {
            var btn = AddPanel(parent, name, bgColor);
            SetButtonFeedback(btn.AddComponent<Button>());
            AddOutlinePanel(btn, new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f, 0.5f));

            var dark = AddPanel(btn, "DarkOverlay", new Color(darkColor.r, darkColor.g, darkColor.b, 0.3f));
            SetAnchors(dark, 0f, 0f, 1f, 0.5f);

            var lbl = AddText(btn, "Label", label, 13, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblSh = lbl.AddComponent<Shadow>();
            lblSh.effectColor = new Color(0, 0, 0, 0.8f);
            lblSh.effectDistance = new Vector2(0.5f, -0.5f);
            return btn;
        }

        /// <summary>Ornate quick action button — uses btn_ornate sprite, icon + label.</summary>
        static GameObject AddOrnateQuickAction(GameObject parent, string name, string label, Color color, string iconKey)
        {
            var btn = AddPanel(parent, name, new Color(0.05f, 0.04f, 0.10f, 0.95f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            var ornateSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Generated/btn_neutral.png");
            if (ornateSpr != null) { btn.GetComponent<Image>().sprite = ornateSpr; btn.GetComponent<Image>().type = Image.Type.Sliced; btn.GetComponent<Image>().color = new Color(color.r * 0.5f + 0.3f, color.g * 0.5f + 0.2f, color.b * 0.5f + 0.2f, 1f); }
            // Gold outline border for ornate feel
            AddOutlinePanel(btn, new Color(0.78f, 0.60f, 0.24f, 0.55f));
            SetButtonFeedback(btn.AddComponent<Button>());

            // Inner color glow for warmth
            var innerGlow = AddPanel(btn, "InnerGlow", new Color(color.r * 0.20f, color.g * 0.20f, color.b * 0.20f, 0.20f));
            SetAnchors(innerGlow, 0.04f, 0.04f, 0.96f, 0.96f);
            innerGlow.AddComponent<LayoutElement>().ignoreLayout = true;
            // Glass highlight
            var glass = AddPanel(btn, "Glass", new Color(0.50f, 0.45f, 0.55f, 0.10f));
            SetAnchors(glass, 0.05f, 0.55f, 0.95f, 0.95f);
            glass.AddComponent<LayoutElement>().ignoreLayout = true;

            // Icon — larger
            var icon = AddPanel(btn, "Icon", new Color(0, 0, 0, 0));
            SetAnchors(icon, 0.03f, 0.08f, 0.32f, 0.92f);
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{iconKey}.png");
            if (spr != null) { icon.GetComponent<Image>().sprite = spr; icon.GetComponent<Image>().preserveAspect = true; icon.GetComponent<Image>().color = new Color(Mathf.Clamp01(color.r * 0.5f + 0.5f), Mathf.Clamp01(color.g * 0.5f + 0.5f), Mathf.Clamp01(color.b * 0.5f + 0.5f), 0.95f); }

            var lbl = AddText(btn, "Label", label, 11, TextAnchor.MiddleCenter);
            SetAnchors(lbl, 0.28f, 0f, 1f, 1f);
            lbl.GetComponent<Text>().color = Color.white;
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.95f);
            lblShadow.effectDistance = new Vector2(1f, -1f);
            var lblOutline = lbl.AddComponent<Outline>();
            lblOutline.effectColor = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.30f);
            lblOutline.effectDistance = new Vector2(0.5f, -0.5f);
            return btn;
        }

        /// <summary>Ornate toolbar button — uses btn_ornate sprite for premium feel.</summary>
        static GameObject AddOrnateToolbarBtn(GameObject parent, string name, string label, Color color, Sprite ornateSpr)
        {
            var btn = AddPanel(parent, name, new Color(color.r * 0.35f + 0.08f, color.g * 0.35f + 0.06f, color.b * 0.35f + 0.08f, 0.98f));
            btn.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (ornateSpr != null) { btn.GetComponent<Image>().sprite = ornateSpr; btn.GetComponent<Image>().type = Image.Type.Sliced; btn.GetComponent<Image>().color = new Color(Mathf.Min(1f, color.r * 0.65f + 0.32f), Mathf.Min(1f, color.g * 0.65f + 0.22f), Mathf.Min(1f, color.b * 0.65f + 0.22f), 1f); }
            SetButtonFeedback(btn.AddComponent<Button>());
            // Inner dark fill for text contrast
            var innerFill = AddPanel(btn, "InnerFill", new Color(0.06f, 0.04f, 0.10f, 0.30f));
            SetAnchors(innerFill, 0.06f, 0.06f, 0.94f, 0.94f);
            innerFill.AddComponent<LayoutElement>().ignoreLayout = true;
            var lbl = AddText(btn, "Label", label, 12, TextAnchor.MiddleCenter);
            StretchToParent(lbl);
            lbl.GetComponent<Text>().color = new Color(1f, 0.96f, 0.88f, 1f);
            lbl.GetComponent<Text>().fontStyle = FontStyle.Bold;
            var lblShadow = lbl.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.95f);
            lblShadow.effectDistance = new Vector2(1.2f, -1.2f);
            var lblOutline = lbl.AddComponent<Outline>();
            lblOutline.effectColor = new Color(0, 0, 0, 0.45f);
            lblOutline.effectDistance = new Vector2(0.5f, -0.5f);
            return btn;
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
            // If the scene is already loaded (e.g. in play mode), reuse it
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name == name)
            {
                Debug.Log($"[SceneUIGenerator] Scene '{name}' already active, reusing.");
                return activeScene;
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

        /// <summary>Configures button press feedback — darker on press, lighter on hover.</summary>
        static void SetButtonFeedback(Button btn)
        {
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.92f, 0.90f, 0.85f, 1f);
            cb.pressedColor = new Color(0.65f, 0.60f, 0.55f, 1f);
            cb.selectedColor = new Color(0.88f, 0.85f, 0.80f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
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
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                ?? Font.CreateDynamicFontFromOSFont("Arial", size);
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
