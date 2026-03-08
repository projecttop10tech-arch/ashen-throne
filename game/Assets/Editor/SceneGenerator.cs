#if UNITY_EDITOR
// Run from Unity Editor menu: AshenThrone → Generate Scenes
// Creates all 6 scenes with cameras, canvases, and system GameObjects.
// Also adds them to EditorBuildSettings in correct order.
// Safe to re-run — existing scenes are overwritten.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshenThrone.Editor
{
    public static class SceneGenerator
    {
        private const string ScenesRoot = "Assets/Scenes";

        // Scene names must match the SceneName enum in GameManager.cs
        private static readonly string[] SceneNames =
            { "Boot", "Lobby", "Empire", "Combat", "WorldMap", "Alliance" };

        [MenuItem("AshenThrone/Generate Scenes")]
        public static void GenerateAll()
        {
            EnsureDirectories();

            var scenePaths = new List<string>();

            foreach (string sceneName in SceneNames)
            {
                string path = GenerateScene(sceneName);
                scenePaths.Add(path);
            }

            // Add all scenes to Build Settings in order
            var buildScenes = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
                buildScenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            EditorBuildSettings.scenes = buildScenes;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SceneGenerator] Generated {scenePaths.Count} scenes and updated Build Settings.");
        }

        private static string GenerateScene(string sceneName)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Main Camera with URP ---
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.1f); // dark fantasy purple-black
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cameraGo.AddComponent<AudioListener>();

            // --- UI Canvas (ScreenSpace-Overlay) ---
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // portrait mobile
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // --- EventSystem ---
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            // --- Directional Light ---
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- Scene-specific GameObjects ---
            AddSceneSpecificObjects(sceneName, canvasGo.transform);

            // Save
            string dir = Path.Combine(ScenesRoot, sceneName);
            string path = $"{dir}/{sceneName}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[SceneGenerator] Created scene: {path}");
            return path;
        }

        private static void AddSceneSpecificObjects(string sceneName, Transform canvasParent)
        {
            switch (sceneName)
            {
                case "Boot":
                    AddBootScene(canvasParent);
                    break;
                case "Lobby":
                    AddLobbyScene(canvasParent);
                    break;
                case "Empire":
                    AddEmpireScene(canvasParent);
                    break;
                case "Combat":
                    AddCombatScene(canvasParent);
                    break;
                case "WorldMap":
                    AddWorldMapScene(canvasParent);
                    break;
                case "Alliance":
                    AddAllianceScene(canvasParent);
                    break;
            }
        }

        // ─── Boot Scene ────────────────────────────────────────────────────────

        private static void AddBootScene(Transform canvas)
        {
            // GameManager (persists across scenes via DontDestroyOnLoad)
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<Core.GameManager>();

            // Loading UI
            var loadingPanel = CreateUIPanel("LoadingPanel", canvas);
            CreateTextElement("LoadingText", loadingPanel.transform, "Loading...",
                TextAnchor.MiddleCenter, 48);
            CreateTextElement("VersionText", loadingPanel.transform, "v0.1.0",
                TextAnchor.LowerRight, 24);

            // Splash logo placeholder
            var logoGo = new GameObject("SplashLogo");
            logoGo.transform.SetParent(canvas);
            var logoRect = logoGo.AddComponent<RectTransform>();
            logoRect.anchoredPosition = new Vector2(0, 100);
            logoRect.sizeDelta = new Vector2(400, 400);
            var logoImage = logoGo.AddComponent<Image>();
            logoImage.color = new Color(0.6f, 0.4f, 0.2f, 0.5f); // placeholder amber
        }

        // ─── Lobby Scene ───────────────────────────────────────────────────────

        private static void AddLobbyScene(Transform canvas)
        {
            // Navigation buttons
            var navPanel = CreateUIPanel("NavigationPanel", canvas);
            var navRect = navPanel.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0, 0);
            navRect.anchorMax = new Vector2(1, 0.12f);
            navRect.offsetMin = Vector2.zero;
            navRect.offsetMax = Vector2.zero;

            CreateButton("BtnCombat", navPanel.transform, "Combat");
            CreateButton("BtnEmpire", navPanel.transform, "Empire");
            CreateButton("BtnWorldMap", navPanel.transform, "Map");
            CreateButton("BtnAlliance", navPanel.transform, "Alliance");

            // Header
            var headerPanel = CreateUIPanel("HeaderPanel", canvas);
            var headerRect = headerPanel.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 0.92f);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            CreateTextElement("PlayerNameText", headerPanel.transform, "Player Name",
                TextAnchor.MiddleLeft, 32);
            CreateTextElement("CurrencyText", headerPanel.transform, "0",
                TextAnchor.MiddleRight, 28);

            // Hero roster area placeholder
            CreateUIPanel("HeroRosterArea", canvas);
        }

        // ─── Empire Scene ──────────────────────────────────────────────────────

        private static void AddEmpireScene(Transform canvas)
        {
            // ResourceHUD — top bar showing 4 resource types
            var resourceHud = CreateUIPanel("ResourceHUD", canvas);
            var hudRect = resourceHud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 0.93f);
            hudRect.anchorMax = new Vector2(1, 1);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            // Building grid area
            var buildArea = CreateUIPanel("BuildingGridArea", canvas);
            var buildRect = buildArea.GetComponent<RectTransform>();
            buildRect.anchorMin = new Vector2(0, 0.1f);
            buildRect.anchorMax = new Vector2(1, 0.93f);
            buildRect.offsetMin = Vector2.zero;
            buildRect.offsetMax = Vector2.zero;

            // BuildQueueOverlay — bottom bar with 2 queue slots
            var queueOverlay = CreateUIPanel("BuildQueueOverlay", canvas);
            var queueRect = queueOverlay.GetComponent<RectTransform>();
            queueRect.anchorMin = new Vector2(0, 0);
            queueRect.anchorMax = new Vector2(1, 0.1f);
            queueRect.offsetMin = Vector2.zero;
            queueRect.offsetMax = Vector2.zero;

            // Modal panels (hidden by default)
            var buildingPanel = CreateUIPanel("BuildingPanel", canvas);
            buildingPanel.SetActive(false);

            var researchPanel = CreateUIPanel("ResearchTreePanel", canvas);
            researchPanel.SetActive(false);
        }

        // ─── Combat Scene ──────────────────────────────────────────────────────

        private static void AddCombatScene(Transform canvas)
        {
            // Combat Grid (world-space, not UI)
            var gridGo = new GameObject("CombatGrid");
            gridGo.transform.position = Vector3.zero;
            // 7 columns × 5 rows placeholder tiles
            for (int col = 0; col < 7; col++)
            {
                for (int row = 0; row < 5; row++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tile.name = $"Tile_{col}_{row}";
                    tile.transform.SetParent(gridGo.transform);
                    tile.transform.localPosition = new Vector3(col - 3f, row - 2f, 0);
                    tile.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                    // Color zones: player (blue-tint), neutral (grey), enemy (red-tint)
                    var renderer = tile.GetComponent<Renderer>();
                    if (col <= 2)
                        renderer.sharedMaterial = null; // will use default — tint in code
                    else if (col == 3)
                        renderer.sharedMaterial = null;
                    else
                        renderer.sharedMaterial = null;

                    // Add collider for raycast input
                    if (tile.GetComponent<BoxCollider>() == null)
                        tile.AddComponent<BoxCollider>();
                }
            }

            // --- Combat UI ---

            // Card hand area — bottom of screen
            var cardHandArea = CreateUIPanel("CardHandArea", canvas);
            var cardRect = cardHandArea.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0, 0);
            cardRect.anchorMax = new Vector2(1, 0.18f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            // Energy display — above card hand
            var energyDisplay = CreateUIPanel("EnergyDisplay", canvas);
            var energyRect = energyDisplay.GetComponent<RectTransform>();
            energyRect.anchorMin = new Vector2(0, 0.18f);
            energyRect.anchorMax = new Vector2(0.15f, 0.24f);
            energyRect.offsetMin = Vector2.zero;
            energyRect.offsetMax = Vector2.zero;

            // Turn order display — top of screen
            var turnOrder = CreateUIPanel("TurnOrderDisplay", canvas);
            var turnRect = turnOrder.GetComponent<RectTransform>();
            turnRect.anchorMin = new Vector2(0.1f, 0.92f);
            turnRect.anchorMax = new Vector2(0.9f, 1f);
            turnRect.offsetMin = Vector2.zero;
            turnRect.offsetMax = Vector2.zero;

            // Hero status panels — left side
            var heroStatus = CreateUIPanel("HeroStatusArea", canvas);
            var statusRect = heroStatus.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0.24f);
            statusRect.anchorMax = new Vector2(0.18f, 0.92f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            // Damage popup container (world-space, pooled)
            var popupContainer = new GameObject("DamagePopupContainer");
            popupContainer.transform.position = Vector3.zero;
        }

        // ─── WorldMap Scene ────────────────────────────────────────────────────

        private static void AddWorldMapScene(Transform canvas)
        {
            // Map viewport
            var mapArea = CreateUIPanel("MapViewport", canvas);
            var mapRect = mapArea.GetComponent<RectTransform>();
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;

            // Territory info sidebar
            var sidebar = CreateUIPanel("TerritoryInfoSidebar", canvas);
            var sideRect = sidebar.GetComponent<RectTransform>();
            sideRect.anchorMin = new Vector2(0.75f, 0);
            sideRect.anchorMax = new Vector2(1, 1);
            sideRect.offsetMin = Vector2.zero;
            sideRect.offsetMax = Vector2.zero;
            sidebar.SetActive(false);

            // Mini-map
            var miniMap = CreateUIPanel("MiniMap", canvas);
            var miniRect = miniMap.GetComponent<RectTransform>();
            miniRect.anchorMin = new Vector2(0, 0.8f);
            miniRect.anchorMax = new Vector2(0.2f, 1);
            miniRect.offsetMin = Vector2.zero;
            miniRect.offsetMax = Vector2.zero;
        }

        // ─── Alliance Scene ───────────────────────────────────────────────────

        private static void AddAllianceScene(Transform canvas)
        {
            // Tab bar — top
            var tabBar = CreateUIPanel("TabBar", canvas);
            var tabRect = tabBar.GetComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0, 0.92f);
            tabRect.anchorMax = new Vector2(1, 1);
            tabRect.offsetMin = Vector2.zero;
            tabRect.offsetMax = Vector2.zero;

            CreateButton("BtnMembers", tabBar.transform, "Members");
            CreateButton("BtnChat", tabBar.transform, "Chat");
            CreateButton("BtnWars", tabBar.transform, "Wars");
            CreateButton("BtnLeaderboard", tabBar.transform, "Ranks");

            // Content area
            var contentArea = CreateUIPanel("ContentArea", canvas);
            var contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.92f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Sub-panels (toggled by tabs)
            CreateUIPanel("MembersPanel", contentArea.transform);
            var chatPanel = CreateUIPanel("ChatPanel", contentArea.transform);
            chatPanel.SetActive(false);
            var warsPanel = CreateUIPanel("WarsPanel", contentArea.transform);
            warsPanel.SetActive(false);
            var leaderboardPanel = CreateUIPanel("LeaderboardPanel", contentArea.transform);
            leaderboardPanel.SetActive(false);
        }

        // ─── UI Helpers ────────────────────────────────────────────────────────

        private static GameObject CreateUIPanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = new Color(0.1f, 0.08f, 0.12f, 0.7f); // semi-transparent dark
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 60);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.18f, 0.35f);
            go.AddComponent<Button>();

            CreateTextElement("Label", go.transform, label, TextAnchor.MiddleCenter, 22);
            return go;
        }

        private static GameObject CreateTextElement(string name, Transform parent, string text,
            TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.alignment = alignment;
            txt.fontSize = fontSize;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        // ─── Directory Setup ───────────────────────────────────────────────────

        private static void EnsureDirectories()
        {
            foreach (string sceneName in SceneNames)
            {
                string dir = Path.Combine(ScenesRoot, sceneName);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
        }
    }
}
#endif
