#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Empire;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates a large scrollable city for the Empire scene.
    /// 48x48 virtual isometric diamond grid (2:1 ratio), tiled dark ground,
    /// diamond wall border, 41 buildings with action indicators and level badges.
    /// Menu: Ashen Throne > Generate Empire City Layout
    /// </summary>
    public static class EmpireCityLayoutGenerator
    {
        const int GridCols = 48;
        const int GridRows = 48;
        const float CellSize = 64f;

        // Isometric projection (must match CityGridView)
        const float HalfW = CellSize * 0.5f;   // 32
        const float HalfH = CellSize * 0.25f;   // 16
        static readonly float IsoCenterY = (GridCols - 1 + GridRows - 1) * 0.5f * HalfH;

        // Playable area bounds (inside walls)
        const int PlayMinX = 2;
        const int PlayMinY = 2;
        const int PlayMaxX = 45;
        const int PlayMaxY = 45;

        const float Padding = 120f;

        // All buildings: (typeId, instanceId, gridX, gridY, tier)
        // Ultra-dense P&C-style layout: buildings packed shoulder-to-shoulder
        static readonly (string id, string inst, int gx, int gy, int tier)[] DefaultBuildings = new[]
        {
            // === Central Stronghold (4×4) at grid center ===
            ("stronghold",        "stronghold_0",        22, 22, 3),

            // === Inner Ring — Administration (touching stronghold) ===
            ("guild_hall",        "guild_hall_0",        19, 22, 2),  // west
            ("embassy",           "embassy_0",           19, 19, 2),  // southwest
            ("marketplace",       "marketplace_0",       26, 22, 2),  // east
            ("academy",           "academy_0",           26, 19, 2),  // southeast
            ("library",           "library_0",           22, 18, 1),  // south of stronghold
            ("archive",           "archive_0",           24, 18, 1),  // south of stronghold

            // === North of Stronghold — Military ===
            ("barracks",          "barracks_0",          19, 26, 2),  // NW tight
            ("barracks",          "barracks_1",          22, 26, 2),  // N tight
            ("barracks",          "barracks_2",          25, 26, 2),  // NE tight
            ("training_ground",   "training_ground_0",   19, 29, 1),
            ("training_ground",   "training_ground_1",   21, 29, 1),
            ("armory",            "armory_0",            23, 29, 1),
            ("armory",            "armory_1",            25, 29, 1),
            ("barracks",          "barracks_3",          27, 29, 2),

            // === East — Magic District ===
            ("arcane_tower",      "arcane_tower_0",      29, 22, 2),
            ("enchanting_tower",  "enchanting_tower_0",  29, 19, 2),
            ("observatory",       "observatory_0",       32, 22, 1),
            ("laboratory",        "laboratory_0",        32, 20, 1),
            ("arcane_tower",      "arcane_tower_1",      34, 22, 1),
            ("enchanting_tower",  "enchanting_tower_1",  34, 19, 1),
            ("library",           "library_1",           32, 18, 1),
            ("archive",           "archive_1",           34, 18, 1),

            // === West — Support District ===
            ("hero_shrine",       "hero_shrine_0",       16, 22, 2),
            ("forge",             "forge_0",             16, 19, 2),
            ("hero_shrine",       "hero_shrine_1",       14, 22, 1),
            ("forge",             "forge_1",             14, 19, 1),
            ("hero_shrine",       "hero_shrine_2",       12, 22, 1),
            ("forge",             "forge_2",             12, 19, 1),

            // === South — Resource District ===
            ("grain_farm",        "grain_farm_0",        16, 16, 2),
            ("grain_farm",        "grain_farm_1",        18, 16, 2),
            ("grain_farm",        "grain_farm_2",        20, 16, 1),
            ("grain_farm",        "grain_farm_3",        16, 14, 1),
            ("grain_farm",        "grain_farm_4",        18, 14, 1),
            ("iron_mine",         "iron_mine_0",         22, 16, 2),
            ("iron_mine",         "iron_mine_1",         24, 16, 1),
            ("iron_mine",         "iron_mine_2",         22, 14, 1),
            ("stone_quarry",      "stone_quarry_0",      26, 16, 2),
            ("stone_quarry",      "stone_quarry_1",      28, 16, 1),
            ("stone_quarry",      "stone_quarry_2",      26, 14, 1),

            // === Outer South — More Resources ===
            ("grain_farm",        "grain_farm_5",        14, 14, 1),
            ("iron_mine",         "iron_mine_3",         24, 14, 1),
            ("stone_quarry",      "stone_quarry_3",      28, 14, 1),
            ("grain_farm",        "grain_farm_6",        16, 12, 1),
            ("iron_mine",         "iron_mine_4",         20, 12, 1),
            ("stone_quarry",      "stone_quarry_4",      24, 12, 1),
            ("grain_farm",        "grain_farm_7",        28, 12, 1),

            // === Outer North — More Military ===
            ("barracks",          "barracks_4",          16, 29, 1),
            ("barracks",          "barracks_5",          30, 26, 1),
            ("armory",            "armory_2",            30, 29, 1),
            ("training_ground",   "training_ground_2",   32, 26, 1),
            ("barracks",          "barracks_6",          14, 26, 1),

            // === Outer East — More Magic ===
            ("laboratory",        "laboratory_1",        36, 22, 1),
            ("observatory",       "observatory_1",       36, 20, 1),
            ("enchanting_tower",  "enchanting_tower_2",  36, 18, 1),
            ("arcane_tower",      "arcane_tower_2",      29, 26, 1),

            // === Outer West — More Support ===
            ("forge",             "forge_3",             10, 22, 1),
            ("hero_shrine",       "hero_shrine_3",       10, 19, 1),
            ("forge",             "forge_4",             12, 16, 1),
            ("hero_shrine",       "hero_shrine_4",       10, 16, 1),

            // === Defense Towers — Perimeter Ring ===
            ("watch_tower",       "watch_tower_0",        8, 28, 1),
            ("watch_tower",       "watch_tower_1",       36, 28, 1),
            ("watch_tower",       "watch_tower_2",        8, 14, 1),
            ("watch_tower",       "watch_tower_3",       36, 14, 1),
            ("watch_tower",       "watch_tower_4",       22, 32, 1),
            ("watch_tower",       "watch_tower_5",       22, 10, 1),
            ("watch_tower",       "watch_tower_6",       38, 22, 1),
            ("watch_tower",       "watch_tower_7",        8, 22, 1),

            // === Gap Fillers — Additional buildings to eliminate empty patches ===
            ("marketplace",       "marketplace_1",       30, 16, 1),
            ("academy",           "academy_1",           12, 26, 1),
            ("laboratory",        "laboratory_2",        14, 16, 1),
            ("academy",           "academy_2",           32, 29, 1),
            ("marketplace",       "marketplace_2",       10, 26, 1),
            ("library",           "library_2",           20, 14, 1),
            ("archive",           "archive_2",           18, 12, 1),
            ("observatory",       "observatory_2",       22, 12, 1),

            // === Watch Tower Neighbors — fill gaps around perimeter towers ===
            ("grain_farm",        "grain_farm_8",        10, 28, 1),  // near NW tower
            ("barracks",          "barracks_7",          34, 28, 1),  // near NE tower
            ("stone_quarry",      "stone_quarry_5",      10, 14, 1),  // near SW tower
            ("iron_mine",         "iron_mine_5",         34, 14, 1),  // near SE tower
            ("armory",            "armory_3",            20, 32, 1),  // near N tower
            ("training_ground",   "training_ground_3",   24, 32, 1),  // near N tower
            ("grain_farm",        "grain_farm_9",        20, 10, 1),  // near S tower
            ("stone_quarry",      "stone_quarry_6",      24, 10, 1),  // near S tower
            ("forge",             "forge_5",             36, 16, 1),  // near E tower gap
            ("hero_shrine",       "hero_shrine_5",        8, 26, 1),  // near W tower gap

            // === Deep outer ring — close off remaining holes ===
            ("iron_mine",         "iron_mine_6",         12, 12, 1),
            ("grain_farm",        "grain_farm_10",       30, 12, 1),
            ("enchanting_tower",  "enchanting_tower_3",  34, 26, 1),
            ("laboratory",        "laboratory_3",        10, 12, 1),
            ("archive",           "archive_3",           26, 12, 1),
            ("library",           "library_3",           32, 16, 1),
            ("barracks",          "barracks_8",          16, 32, 1),
            ("barracks",          "barracks_9",          28, 32, 1),

            // === Ultra-dense infill — eliminate ALL visible gaps ===
            ("forge",             "forge_6",             14, 29, 1),   // between W support and N military
            ("grain_farm",        "grain_farm_11",       12, 29, 1),   // W outer gap
            ("observatory",       "observatory_3",       30, 19, 1),   // between E magic and inner
            ("laboratory",        "laboratory_4",        38, 19, 1),   // far E
            ("grain_farm",        "grain_farm_12",       14, 12, 1),   // SW corner
            ("stone_quarry",      "stone_quarry_7",      26, 10, 1),   // S gap
            ("iron_mine",         "iron_mine_7",         30, 14, 1),   // SE gap
            ("hero_shrine",       "hero_shrine_6",        8, 19, 1),   // far W
            ("forge",             "forge_7",             36, 26, 1),   // far NE
            ("academy",           "academy_3",           18, 29, 1),   // N inner
            ("marketplace",       "marketplace_3",       27, 12, 1),   // S outer
            ("enchanting_tower",  "enchanting_tower_4",  38, 16, 1),   // far SE
        };

        // ================================================================
        // Isometric coordinate helpers (must match CityGridView)
        // ================================================================

        static Vector2 GridToIso(float gx, float gy)
        {
            float isoX = (gx - gy) * HalfW;
            float isoY = (gx + gy) * HalfH - IsoCenterY;
            return new Vector2(isoX, isoY);
        }

        static Vector2 GridToIsoCenter(int gx, int gy, Vector2Int size)
        {
            float cx = gx + size.x * 0.5f;
            float cy = gy + size.y * 0.5f;
            return GridToIso(cx, cy);
        }

        static Vector2 FootprintSize(Vector2Int size)
        {
            // Buildings slightly wider than grid footprint for P&C-style overlap
            float w = (size.x + size.y) * HalfW * 1.2f;
            float h = w * 1.8f;
            return new Vector2(w, h);
        }

        // ================================================================
        // Main generator
        // ================================================================

        [MenuItem("Ashen Throne/Generate Empire City Layout")]
        public static void Generate()
        {
            // Reuse active scene if already loaded (works in play mode too)
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.Scene scene;
            if (activeScene.IsValid() && activeScene.name == "Empire")
            {
                scene = activeScene;
                Debug.Log("[EmpireCityLayout] Empire scene already active, reusing.");
            }
            else
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Empire/Empire.unity");
            }
            if (!scene.IsValid())
            {
                Debug.LogError("[EmpireCityLayout] Failed to open Empire scene");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[EmpireCityLayout] No Canvas found in Empire scene");
                return;
            }

            // Clean up old layout (keep Background from SceneUI for atmosphere behind city)
            DestroyChild(canvas.transform, "CityViewport");
            DestroyChild(canvas.transform, "BuildingPlots");

            // ================================================================
            // 1. Content area sizing for isometric diamond
            // ================================================================
            // The 48x48 grid in isometric spans:
            //   X: [-(GridCols-1)*HalfW, (GridCols-1)*HalfW] = [-1504, 1504] = 3008
            //   Y: [-IsoCenterY, IsoCenterY] roughly = [-752, 752] = 1504
            // Add padding and extra height for building sprites extending upward
            float contentW = (GridCols + GridRows) * HalfW + Padding * 2;  // ~3200
            float contentH = (GridCols + GridRows) * HalfH + 600 + Padding * 2; // ~2140 (extra for building height)

            // ================================================================
            // 2. Scrollable city viewport
            // ================================================================
            var viewport = CreateChild(canvas, "CityViewport");
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0, 0.1f);
            vpRect.anchorMax = new Vector2(1, 0.93f);
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            // Dark viewport background — catches any gaps in content coverage
            var vpBg = viewport.AddComponent<Image>();
            vpBg.color = new Color(0.03f, 0.03f, 0.04f, 1f);
            vpBg.raycastTarget = false;
            // Place CityViewport right after Background (index 1) so Background is behind
            // and all UI overlays (nav bar, info panel, etc.) are on top
            var bgTransform = canvas.transform.Find("Background");
            if (bgTransform != null)
                viewport.transform.SetSiblingIndex(bgTransform.GetSiblingIndex() + 1);
            else
                viewport.transform.SetAsFirstSibling();

            var content = CreateChild(viewport, "CityContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(contentW, contentH);
            // P&C-style default zoom so buildings fill the viewport vertically
            // This persists in the scene — CityGridView reads it at Start()
            contentRect.localScale = Vector3.one * 2.5f;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.1f;
            scroll.scrollSensitivity = 10f;

            // ================================================================
            // 3. Ground background (rich terrain art, covers entire content)
            // ================================================================
            var outerBG = CreateChild(content, "GroundBG");
            var outerRect = outerBG.AddComponent<RectTransform>();
            outerRect.anchorMin = Vector2.zero;
            outerRect.anchorMax = Vector2.one;
            outerRect.offsetMin = Vector2.zero;
            outerRect.offsetMax = Vector2.zero;
            var outerImg = outerBG.AddComponent<Image>();

            // Dark-tinted terrain texture — provides subtle variation without being bright
            var terrainSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/empire_terrain_bg.png");
            if (terrainSprite != null)
            {
                EnsureSpriteImportSettings("Assets/Art/Environments/empire_terrain_bg.png");
                terrainSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/empire_terrain_bg.png");
                outerImg.sprite = terrainSprite;
                // Neutral dark tint — terrain barely visible, just enough for texture variation
                outerImg.color = new Color(0.06f, 0.05f, 0.06f, 1f);
            }
            else
            {
                outerImg.color = new Color(0.08f, 0.06f, 0.12f, 1f);
            }
            outerImg.raycastTarget = true;

            // ================================================================
            // 4. Diamond wall border (isometric perimeter of playable area)
            // ================================================================
            // The 4 corners of the playable diamond in screen coords:
            var cornerBL = GridToIso(PlayMinX, PlayMinY);   // bottom vertex
            var cornerBR = GridToIso(PlayMaxX, PlayMinY);   // right vertex
            var cornerTR = GridToIso(PlayMaxX, PlayMaxY);   // top vertex
            var cornerTL = GridToIso(PlayMinX, PlayMaxY);   // left vertex

            EnsureSpriteImportSettings("Assets/Art/Environments/wall_border.png");
            var wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environments/wall_border.png");

            // Create 4 wall segments along the diamond edges
            CreateDiamondWall(content, "WallBottomRight", wallSprite, cornerBL, cornerBR);
            CreateDiamondWall(content, "WallRightTop", wallSprite, cornerBR, cornerTR);
            CreateDiamondWall(content, "WallTopLeft", wallSprite, cornerTR, cornerTL);
            CreateDiamondWall(content, "WallLeftBottom", wallSprite, cornerTL, cornerBL);

            // ================================================================
            // 5. Grid overlay (hidden by default, shown during move mode)
            // ================================================================
            CreateGridOverlayTexture();
            var gridOverlayGO = CreateChild(content, "GridOverlay");
            var goRect = gridOverlayGO.AddComponent<RectTransform>();
            // Size to cover the isometric diamond bounding box
            float diamondW = (PlayMaxX - PlayMinX + PlayMaxY - PlayMinY) * HalfW;
            float diamondH = (PlayMaxX - PlayMinX + PlayMaxY - PlayMinY) * HalfH;
            var diamondCenter = GridToIso(
                (PlayMinX + PlayMaxX) * 0.5f,
                (PlayMinY + PlayMaxY) * 0.5f);
            goRect.anchoredPosition = diamondCenter;
            goRect.sizeDelta = new Vector2(diamondW + CellSize, diamondH + CellSize);
            var goImg = gridOverlayGO.AddComponent<Image>();
            EnsureSpriteImportSettings("Assets/Art/UI/Production/grid_overlay_tile.png");
            var gridTileSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/grid_overlay_tile.png");
            if (gridTileSprite != null)
            {
                goImg.sprite = gridTileSprite;
                goImg.type = Image.Type.Tiled;
                goImg.color = Color.white;
            }
            else
            {
                goImg.color = new Color(0.4f, 0.8f, 0.3f, 0.15f);
            }
            goImg.raycastTarget = false;
            // Faint grid visible by default for ground texture (P&C-style diamond lines)
            goImg.color = new Color(0.35f, 0.55f, 0.30f, 0.06f);
            gridOverlayGO.SetActive(true);

            // ================================================================
            // 5b. Focal aura behind Stronghold (P&C dragon-equivalent)
            // ================================================================
            Vector2 shCenter = GridToIsoCenter(22, 22, new Vector2Int(6, 6));

            var radialSprite = GetOrCreateRadialGradient();

            // Multi-layer warm focal glow behind Stronghold — dramatic but warm-gold, NOT purple
            // Layer 1: Large soft ambient glow — widest, lowest alpha
            var auraOuter = CreateChild(content, "FocalAuraOuter");
            var aoRect = auraOuter.AddComponent<RectTransform>();
            aoRect.anchorMin = new Vector2(0.5f, 0.5f);
            aoRect.anchorMax = new Vector2(0.5f, 0.5f);
            aoRect.pivot = new Vector2(0.5f, 0.5f);
            aoRect.anchoredPosition = shCenter + Vector2.up * 80f;
            aoRect.sizeDelta = new Vector2(800, 650);
            var aoImg = auraOuter.AddComponent<Image>();
            aoImg.sprite = radialSprite;
            aoImg.color = new Color(0.65f, 0.35f, 0.08f, 0.07f);
            aoImg.raycastTarget = false;

            // Layer 2: Medium warm glow — brighter core
            var auraMid = CreateChild(content, "FocalAuraMid");
            var amRect = auraMid.AddComponent<RectTransform>();
            amRect.anchorMin = new Vector2(0.5f, 0.5f);
            amRect.anchorMax = new Vector2(0.5f, 0.5f);
            amRect.pivot = new Vector2(0.5f, 0.5f);
            amRect.anchoredPosition = shCenter + Vector2.up * 70f;
            amRect.sizeDelta = new Vector2(550, 450);
            var amImg = auraMid.AddComponent<Image>();
            amImg.sprite = radialSprite;
            amImg.color = new Color(0.80f, 0.50f, 0.12f, 0.10f);
            amImg.raycastTarget = false;

            // Layer 3: Inner hot core — smallest, warmest
            var auraInner = CreateChild(content, "FocalAuraInner");
            var aiRect = auraInner.AddComponent<RectTransform>();
            aiRect.anchorMin = new Vector2(0.5f, 0.5f);
            aiRect.anchorMax = new Vector2(0.5f, 0.5f);
            aiRect.pivot = new Vector2(0.5f, 0.5f);
            aiRect.anchoredPosition = shCenter + Vector2.up * 55f;
            aiRect.sizeDelta = new Vector2(350, 300);
            var aiImg = auraInner.AddComponent<Image>();
            aiImg.sprite = radialSprite;
            aiImg.color = new Color(0.90f, 0.60f, 0.15f, 0.12f);
            aiImg.raycastTarget = false;

            // ================================================================
            // 6. Buildings container
            // ================================================================
            var buildingsGO = CreateChild(content, "Buildings");
            var buildRect = buildingsGO.AddComponent<RectTransform>();
            buildRect.anchorMin = new Vector2(0.5f, 0.5f);
            buildRect.anchorMax = new Vector2(0.5f, 0.5f);
            buildRect.pivot = new Vector2(0.5f, 0.5f);
            buildRect.sizeDelta = new Vector2(contentW, contentH);

            // Targeted ground glow under key magical buildings only (not every building)
            var glowBuildings = new System.Collections.Generic.HashSet<string> {
                "stronghold", "arcane_tower", "enchanting_tower", "hero_shrine", "observatory"
            };
            var glowColors = new System.Collections.Generic.Dictionary<string, Color> {
                { "stronghold",       new Color(0.85f, 0.55f, 0.15f, 0.08f) },
                { "arcane_tower",     new Color(0.40f, 0.25f, 0.80f, 0.06f) },
                { "enchanting_tower", new Color(0.30f, 0.60f, 0.85f, 0.06f) },
                { "hero_shrine",      new Color(0.80f, 0.65f, 0.20f, 0.06f) },
                { "observatory",      new Color(0.25f, 0.50f, 0.70f, 0.05f) },
            };

            // ================================================================
            // 7. Place all buildings (isometric positions)
            // ================================================================
            int placed = 0;
            foreach (var (id, inst, gx, gy, tier) in DefaultBuildings)
            {
                if (!CityGridView.BuildingSizes.TryGetValue(id, out var size))
                    size = new Vector2Int(2, 2);

                string spritePath = $"Assets/Art/Buildings/{id}_t{tier}.png";
                EnsureSpriteImportSettings(spritePath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                // Create building visual at isometric position
                var bldgGO = CreateChild(buildingsGO, $"Building_{inst}");
                var bldgRect = bldgGO.AddComponent<RectTransform>();

                Vector2 isoPos = GridToIsoCenter(gx, gy, size);
                Vector2 footprint = FootprintSize(size);
                // Stronghold gets extra 50% size boost for dramatic dominance (P&C-style)
                if (id == "stronghold")
                    footprint *= 1.8f;
                bldgRect.anchoredPosition = isoPos;
                bldgRect.sizeDelta = footprint;

                var bldgImg = bldgGO.AddComponent<Image>();
                if (sprite != null)
                {
                    bldgImg.sprite = sprite;
                    bldgImg.preserveAspect = true;
                    bldgImg.color = Color.white;
                }
                else
                {
                    bldgImg.color = GetPlaceholderColor(id);
                }
                bldgImg.raycastTarget = true;

                // Ground glow for key magical buildings — colored light pool on terrain
                if (glowBuildings.Contains(id) && glowColors.TryGetValue(id, out var gc))
                {
                    var groundGlow = CreateChild(buildingsGO, $"GroundGlow_{inst}");
                    var ggRect = groundGlow.AddComponent<RectTransform>();
                    ggRect.anchoredPosition = isoPos - Vector2.up * (footprint.y * 0.15f);
                    float glowScale = id == "stronghold" ? 2.2f : 1.6f;
                    ggRect.sizeDelta = footprint * glowScale;
                    var ggImg = groundGlow.AddComponent<Image>();
                    ggImg.sprite = radialSprite;
                    ggImg.color = gc;
                    ggImg.raycastTarget = false;
                    groundGlow.transform.SetAsFirstSibling(); // behind buildings
                }

                // Building name label — only for placeholder buildings (no real sprite)
                if (sprite == null)
                    CreateNameLabel(bldgGO, id);

                // Level badge (bottom-right corner)
                CreateLevelBadge(bldgGO, tier);

                // Action indicator (top-right corner)
                CreateActionIndicator(bldgGO, inst);

                placed++;
            }

            // ================================================================
            // 7b. Atmospheric overlays (P&C-style mood layers)
            // ================================================================

            // Dramatic warm crown glow above Stronghold — visible focal point
            var topGlowOuter = CreateChild(content, "StrongholdTopGlowOuter");
            var tgoRect = topGlowOuter.AddComponent<RectTransform>();
            tgoRect.anchorMin = new Vector2(0.5f, 0.5f);
            tgoRect.anchorMax = new Vector2(0.5f, 0.5f);
            tgoRect.pivot = new Vector2(0.5f, 0.5f);
            tgoRect.anchoredPosition = shCenter + Vector2.up * 120f;
            tgoRect.sizeDelta = new Vector2(500, 400);
            var tgoImg = topGlowOuter.AddComponent<Image>();
            tgoImg.sprite = radialSprite;
            tgoImg.color = new Color(0.85f, 0.55f, 0.12f, 0.10f);
            tgoImg.raycastTarget = false;

            var topGlowInner = CreateChild(content, "StrongholdTopGlowInner");
            var tgiRect = topGlowInner.AddComponent<RectTransform>();
            tgiRect.anchorMin = new Vector2(0.5f, 0.5f);
            tgiRect.anchorMax = new Vector2(0.5f, 0.5f);
            tgiRect.pivot = new Vector2(0.5f, 0.5f);
            tgiRect.anchoredPosition = shCenter + Vector2.up * 100f;
            tgiRect.sizeDelta = new Vector2(280, 220);
            var tgiImg = topGlowInner.AddComponent<Image>();
            tgiImg.sprite = radialSprite;
            tgiImg.color = new Color(0.95f, 0.70f, 0.20f, 0.14f);
            tgiImg.raycastTarget = false;

            // Bottom dark fade — ground recedes into darkness (STRONGER)
            var botFog = CreateChild(content, "BottomDarkFade");
            var botFogRect = botFog.AddComponent<RectTransform>();
            botFogRect.anchorMin = new Vector2(0f, 0f);
            botFogRect.anchorMax = new Vector2(1f, 0.20f);
            botFogRect.offsetMin = Vector2.zero;
            botFogRect.offsetMax = Vector2.zero;
            var botFogImg = botFog.AddComponent<Image>();
            botFogImg.color = new Color(0.02f, 0.01f, 0.04f, 0.70f);
            botFogImg.raycastTarget = false;

            // Left vignette fog — dark fade on left edge
            var leftFog = CreateChild(content, "LeftVignetteFog");
            var leftFogRect = leftFog.AddComponent<RectTransform>();
            leftFogRect.anchorMin = new Vector2(0f, 0f);
            leftFogRect.anchorMax = new Vector2(0.15f, 1f);
            leftFogRect.offsetMin = Vector2.zero;
            leftFogRect.offsetMax = Vector2.zero;
            var leftFogImg = leftFog.AddComponent<Image>();
            leftFogImg.color = new Color(0.02f, 0.01f, 0.04f, 0.50f);
            leftFogImg.raycastTarget = false;

            // Right vignette fog — dark fade on right edge
            var rightFog = CreateChild(content, "RightVignetteFog");
            var rightFogRect = rightFog.AddComponent<RectTransform>();
            rightFogRect.anchorMin = new Vector2(0.85f, 0f);
            rightFogRect.anchorMax = new Vector2(1f, 1f);
            rightFogRect.offsetMin = Vector2.zero;
            rightFogRect.offsetMax = Vector2.zero;
            var rightFogImg = rightFog.AddComponent<Image>();
            rightFogImg.color = new Color(0.02f, 0.01f, 0.04f, 0.50f);
            rightFogImg.raycastTarget = false;

            // Focal aura moved to before buildings (section 6a) for correct z-order

            // Build queue sidebar removed — SceneUI's LeftSidebar handles this

            // ================================================================
            // 9. Wire CityGridView runtime controller
            // ================================================================
            var gridView = viewport.AddComponent<CityGridView>();
            var gvSO = new SerializedObject(gridView);
            gvSO.FindProperty("scrollRect").objectReferenceValue = scroll;
            gvSO.FindProperty("contentContainer").objectReferenceValue = contentRect;
            gvSO.FindProperty("buildingContainer").objectReferenceValue = buildRect;
            gvSO.FindProperty("gridOverlay").objectReferenceValue = gridOverlayGO;
            gvSO.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================
            // 9b. Resource bubble container + spawner
            // ================================================================
            var bubbleContainerGO = CreateChild(content, "ResourceBubbles");
            var bubbleContainerRect = bubbleContainerGO.AddComponent<RectTransform>();
            bubbleContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleContainerRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleContainerRect.sizeDelta = new Vector2(contentW, contentH);

            var bubbleSpawner = viewport.AddComponent<ResourceBubbleSpawner>();
            var bsSO = new SerializedObject(bubbleSpawner);
            bsSO.FindProperty("cityGrid").objectReferenceValue = gridView;
            bsSO.FindProperty("bubbleContainer").objectReferenceValue = bubbleContainerRect;
            bsSO.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================
            // 9c. Ambient city effects (sparkles, smoke, etc.)
            // ================================================================
            var ambientFX = viewport.AddComponent<CityAmbientEffects>();
            var fxSO = new SerializedObject(ambientFX);
            fxSO.FindProperty("cityGrid").objectReferenceValue = gridView;
            fxSO.ApplyModifiedPropertiesWithoutUndo();

            // ================================================================
            // 10. Wire UI sprites (resource bar, toolbar, etc.)
            // ================================================================
            int wired = 0;
            var uiWires = new (string path, string sprite, bool preserveAspect)[]
            {
                ("Canvas/ResourceHUD/Res_Stone/TopRow/Icon",  "Assets/Art/UI/Production/icon_stone.png", true),
                ("Canvas/ResourceHUD/Res_Iron/TopRow/Icon",   "Assets/Art/UI/Production/icon_iron.png", true),
                ("Canvas/ResourceHUD/Res_Grain/TopRow/Icon",  "Assets/Art/UI/Production/icon_grain.png", true),
                ("Canvas/ResourceHUD/Res_Arcane/TopRow/Icon", "Assets/Art/UI/Production/icon_arcane.png", true),
                ("Canvas/Toolbar/BattleBtn/Icon",    "Assets/Art/UI/Production/nav_battle.png", true),
                ("Canvas/Toolbar/HeroesBtn/Icon",    "Assets/Art/UI/Production/nav_heroes.png", true),
                ("Canvas/Toolbar/QuestsBtn/Icon",    "Assets/Art/UI/Production/nav_battle.png", true),
                ("Canvas/Toolbar/ResearchBtn/Icon",  "Assets/Art/UI/Production/nav_alliance.png", true),
                ("Canvas/ResourceHUD",    "Assets/Art/UI/Production/resource_bar.png", false),
                ("Canvas/Toolbar",        "Assets/Art/UI/Production/nav_bar.png", false),
                ("Canvas/StrongholdInfo", "Assets/Art/UI/Production/building_panel.png", false),
            };

            foreach (var (goPath, spriteFile, pa) in uiWires)
                wired += WireSprite(goPath, spriteFile, pa);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EmpireCityLayout] Isometric grid: {GridCols}x{GridRows}, placed {placed} buildings, wired {wired} UI sprites, content {contentW}x{contentH}");
        }

        // ================================================================
        // Helper: Create diamond wall segment between two screen-space points
        // ================================================================
        static void CreateDiamondWall(GameObject parent, string name, Sprite wallSprite,
            Vector2 from, Vector2 to)
        {
            var wallGO = CreateChild(parent, name);
            var wallRect = wallGO.AddComponent<RectTransform>();

            Vector2 mid = (from + to) * 0.5f;
            Vector2 delta = to - from;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float thickness = CellSize * 0.8f; // Thinner wall border

            wallRect.anchoredPosition = mid;
            wallRect.sizeDelta = new Vector2(length, thickness);
            wallRect.localRotation = Quaternion.Euler(0, 0, angle);

            var wallImg = wallGO.AddComponent<Image>();
            if (wallSprite != null)
            {
                wallImg.sprite = wallSprite;
                wallImg.type = Image.Type.Tiled;
                wallImg.color = Color.white;
            }
            else
            {
                wallImg.color = new Color(0.35f, 0.30f, 0.25f, 0.85f);
            }
            wallImg.raycastTarget = false;
        }

        // ================================================================
        // Helper: Create level badge on a building
        // ================================================================
        static void CreateLevelBadge(GameObject parent, int tier)
        {
            // Tiny square gold-framed level badge (bottom-center) — P&C style
            // Much smaller than before to avoid "dark rectangle" look
            var badgeGO = CreateChild(parent, "LevelBadge");
            var badgeRect = badgeGO.AddComponent<RectTransform>();
            // Tiny centered badge at bottom of building
            badgeRect.anchorMin = new Vector2(0.38f, 0.01f);
            badgeRect.anchorMax = new Vector2(0.62f, 0.065f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;

            // Outer gold frame (2px feel)
            var badgeBorder = badgeGO.AddComponent<Image>();
            badgeBorder.color = new Color(0.85f, 0.68f, 0.18f, 0.75f);

            // Inner dark fill
            var innerGO = CreateChild(badgeGO, "Inner");
            var innerRect = innerGO.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(1.5f, 1.5f);
            innerRect.offsetMax = new Vector2(-1.5f, -1.5f);
            var innerBg = innerGO.AddComponent<Image>();
            innerBg.color = new Color(0.08f, 0.05f, 0.15f, 0.80f);

            // Level number
            var lvlGO = CreateChild(innerGO, "LvlText");
            var lvlRect = lvlGO.AddComponent<RectTransform>();
            lvlRect.anchorMin = Vector2.zero;
            lvlRect.anchorMax = Vector2.one;
            lvlRect.offsetMin = Vector2.zero;
            lvlRect.offsetMax = Vector2.zero;
            var lvlText = lvlGO.AddComponent<Text>();
            lvlText.text = tier > 1 ? $"\u2605{tier}" : $"{tier}"; // Star for tier > 1
            lvlText.alignment = TextAnchor.MiddleCenter;
            lvlText.fontSize = 9;
            lvlText.fontStyle = FontStyle.Bold;
            lvlText.color = new Color(1f, 0.92f, 0.55f, 1f);
            lvlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lvlText.raycastTarget = false;
        }

        // ================================================================
        // Helper: Create action indicator overlay on a building
        // ================================================================
        static void CreateActionIndicator(GameObject parent, string instanceId)
        {
            var indicatorGO = CreateChild(parent, "ActionIndicator");

            // Small upgrade indicator — subtle green dot with up arrow, top-right
            var upgradeGO = CreateChild(indicatorGO, "UpgradeIcon");
            var upRect = upgradeGO.AddComponent<RectTransform>();
            upRect.anchorMin = new Vector2(0.82f, 0.85f);
            upRect.anchorMax = new Vector2(0.98f, 0.98f);
            upRect.offsetMin = Vector2.zero;
            upRect.offsetMax = Vector2.zero;
            var upBg = upgradeGO.AddComponent<Image>();
            upBg.color = new Color(0.15f, 0.65f, 0.15f, 0.85f);
            upBg.raycastTarget = false;

            var upTextGO = CreateChild(upgradeGO, "Arrow");
            var upTextRect = upTextGO.AddComponent<RectTransform>();
            upTextRect.anchorMin = Vector2.zero;
            upTextRect.anchorMax = Vector2.one;
            upTextRect.offsetMin = Vector2.zero;
            upTextRect.offsetMax = Vector2.zero;
            var upText = upTextGO.AddComponent<Text>();
            upText.text = "\u2191";
            upText.alignment = TextAnchor.MiddleCenter;
            upText.fontSize = 12;
            upText.fontStyle = FontStyle.Bold;
            upText.color = Color.white;
            upText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            upText.raycastTarget = false;

            var timerGO = CreateChild(indicatorGO, "TimerGroup");
            var tmRect = timerGO.AddComponent<RectTransform>();
            tmRect.anchorMin = new Vector2(0.1f, 0.85f);
            tmRect.anchorMax = new Vector2(0.9f, 1f);
            tmRect.offsetMin = Vector2.zero;
            tmRect.offsetMax = Vector2.zero;
            var tmBg = timerGO.AddComponent<Image>();
            tmBg.color = new Color(0.04f, 0.03f, 0.08f, 0.85f); // dark pill like P&C
            tmBg.raycastTarget = false;
            timerGO.SetActive(false);

            var tmTextGO = CreateChild(timerGO, "TimerText");
            var tmTextRect = tmTextGO.AddComponent<RectTransform>();
            tmTextRect.anchorMin = Vector2.zero;
            tmTextRect.anchorMax = Vector2.one;
            tmTextRect.offsetMin = Vector2.zero;
            tmTextRect.offsetMax = Vector2.zero;
            var tmText = tmTextGO.AddComponent<Text>();
            tmText.text = "00:00:00";
            tmText.alignment = TextAnchor.MiddleCenter;
            tmText.fontSize = 11;
            tmText.color = Color.white;
            tmText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tmText.raycastTarget = false;

            var collectGO = CreateChild(indicatorGO, "CollectIcon");
            var colRect = collectGO.AddComponent<RectTransform>();
            colRect.anchorMin = new Vector2(0.82f, 0.85f);
            colRect.anchorMax = new Vector2(0.98f, 0.98f);
            colRect.offsetMin = Vector2.zero;
            colRect.offsetMax = Vector2.zero;
            var colBg = collectGO.AddComponent<Image>();
            colBg.color = new Color(0.20f, 0.65f, 0.25f, 0.92f); // green "ready" badge
            colBg.raycastTarget = false;
            collectGO.SetActive(false);

            var colTextGO = CreateChild(collectGO, "Symbol");
            var colTextRect = colTextGO.AddComponent<RectTransform>();
            colTextRect.anchorMin = Vector2.zero;
            colTextRect.anchorMax = Vector2.one;
            colTextRect.offsetMin = Vector2.zero;
            colTextRect.offsetMax = Vector2.zero;
            var colText = colTextGO.AddComponent<Text>();
            colText.text = "!";
            colText.alignment = TextAnchor.MiddleCenter;
            colText.fontSize = 16;
            colText.fontStyle = FontStyle.Bold;
            colText.color = Color.white;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.raycastTarget = false;

            var indicator = indicatorGO.AddComponent<BuildingActionIndicator>();
            var indSO = new SerializedObject(indicator);
            indSO.FindProperty("upgradeIcon").objectReferenceValue = upgradeGO;
            indSO.FindProperty("timerGroup").objectReferenceValue = timerGO;
            indSO.FindProperty("timerText").objectReferenceValue = tmText;
            indSO.FindProperty("collectIcon").objectReferenceValue = collectGO;
            indSO.ApplyModifiedPropertiesWithoutUndo();

            if (instanceId.StartsWith("stronghold"))
                upgradeGO.SetActive(false);

            // Show construction timer on a few buildings for visual demo (P&C-style)
            if (instanceId == "barracks_1")
            {
                timerGO.SetActive(true);
                tmText.text = "2:34:15";
                upgradeGO.SetActive(false);
            }
            else if (instanceId == "arcane_tower_0")
            {
                timerGO.SetActive(true);
                tmText.text = "1d 06:42";
                upgradeGO.SetActive(false);
            }
            // Show collect icon on a couple resource buildings
            else if (instanceId == "grain_farm_0" || instanceId == "iron_mine_0")
            {
                collectGO.SetActive(true);
                upgradeGO.SetActive(false);
            }
        }

        // ================================================================
        // Helper: Placeholder color by building type
        // ================================================================
        static Color GetPlaceholderColor(string buildingId)
        {
            return buildingId switch
            {
                "stronghold"       => new Color(0.55f, 0.45f, 0.35f, 0.9f),
                "barracks"         => new Color(0.6f, 0.3f, 0.25f, 0.85f),
                "training_ground"  => new Color(0.65f, 0.35f, 0.3f, 0.85f),
                "armory"           => new Color(0.5f, 0.4f, 0.4f, 0.85f),
                "watch_tower"      => new Color(0.5f, 0.45f, 0.4f, 0.85f),
                "grain_farm"       => new Color(0.7f, 0.65f, 0.3f, 0.85f),
                "iron_mine"        => new Color(0.45f, 0.45f, 0.5f, 0.85f),
                "stone_quarry"     => new Color(0.55f, 0.55f, 0.5f, 0.85f),
                "marketplace"      => new Color(0.7f, 0.55f, 0.3f, 0.85f),
                "guild_hall"       => new Color(0.5f, 0.4f, 0.55f, 0.85f),
                "embassy"          => new Color(0.4f, 0.5f, 0.6f, 0.85f),
                "academy"          => new Color(0.45f, 0.45f, 0.65f, 0.85f),
                "arcane_tower"     => new Color(0.5f, 0.35f, 0.7f, 0.85f),
                "enchanting_tower" => new Color(0.55f, 0.3f, 0.6f, 0.85f),
                "observatory"      => new Color(0.35f, 0.45f, 0.6f, 0.85f),
                "library"          => new Color(0.5f, 0.4f, 0.3f, 0.85f),
                "laboratory"       => new Color(0.4f, 0.55f, 0.4f, 0.85f),
                "archive"          => new Color(0.45f, 0.4f, 0.35f, 0.85f),
                "hero_shrine"      => new Color(0.6f, 0.5f, 0.2f, 0.85f),
                "forge"            => new Color(0.65f, 0.4f, 0.2f, 0.85f),
                _                  => new Color(0.5f, 0.5f, 0.5f, 0.85f),
            };
        }

        // ================================================================
        // Helper: Building name label (centered)
        // ================================================================
        static void CreateNameLabel(GameObject parent, string buildingId)
        {
            string displayName = buildingId.Replace("_", " ");
            var words = displayName.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            displayName = string.Join(" ", words);

            // Compact pill-shaped label at bottom of building
            var labelGO = CreateChild(parent, "NameLabel");
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.05f, 0.13f);
            labelRect.anchorMax = new Vector2(0.95f, 0.28f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Dark pill bg with gold border
            var borderGO = CreateChild(labelGO, "Border");
            var borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.6f, 0.5f, 0.2f, 0.8f); // Gold border
            borderImg.raycastTarget = false;

            var innerGO = CreateChild(borderGO, "Inner");
            var innerRect = innerGO.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(1f, 1f);
            innerRect.offsetMax = new Vector2(-1f, -1f);
            var innerImg = innerGO.AddComponent<Image>();
            innerImg.color = new Color(0.08f, 0.06f, 0.12f, 0.9f); // Dark bg
            innerImg.raycastTarget = false;

            var textGO = CreateChild(innerGO, "Text");
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2, 0);
            textRect.offsetMax = new Vector2(-2, 0);
            var txt = textGO.AddComponent<Text>();
            txt.text = displayName;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(1f, 0.9f, 0.7f, 1f); // Warm cream text
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
        }

        // ================================================================
        // Helper: Generate dark ground texture WITHOUT grid lines
        // ================================================================
        static void CreateGrassTexture()
        {
            string path = "Assets/Art/Environments/tile_grass.png";
            int size = 128;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            // Dark earthy ground — subtle noise variation, NO grid lines
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Multi-octave tileable noise using PerlinNoise
                    // Frequencies that divide evenly into 128 give seamless tiling
                    float n1 = Mathf.PerlinNoise(x * 0.03125f + 0.5f, y * 0.03125f + 0.5f); // 32px period
                    float n2 = Mathf.PerlinNoise(x * 0.0625f + 10.5f, y * 0.0625f + 10.5f);  // 16px period
                    float n3 = Mathf.PerlinNoise(x * 0.125f + 20.5f, y * 0.125f + 20.5f);     // 8px period

                    float noise = n1 * 0.06f + n2 * 0.03f + n3 * 0.015f;

                    // Base: dark mossy earth
                    float r = 0.14f + noise;
                    float g = 0.19f + noise * 1.3f;
                    float b = 0.11f + noise * 0.7f;

                    tex.SetPixel(x, y, new Color(r, g, b, 1f));
                }
            }

            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!importer.alphaIsTransparency)
                { importer.alphaIsTransparency = true; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Repeat)
                { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (importer.filterMode != FilterMode.Bilinear)
                { importer.filterMode = FilterMode.Bilinear; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        // ================================================================
        // Helper: Generate grid overlay tile (diamond pattern for move mode)
        // ================================================================
        /// <summary>
        /// Creates a soft radial gradient sprite (white circle, alpha falloff from center to edge).
        /// Used for glow/aura effects so they look natural instead of rectangular.
        /// </summary>
        static Sprite GetOrCreateRadialGradient()
        {
            string path = "Assets/Art/UI/Production/radial_gradient.png";
            if (!System.IO.File.Exists(path))
            {
                int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                float center = size * 0.5f;
                float radius = center;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                        // Smooth falloff: cubic ease-out for softer edge
                        float alpha = Mathf.Clamp01(1f - dist);
                        alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                string dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }
            EnsureSpriteImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void CreateGridOverlayTexture()
        {
            string path = "Assets/Art/UI/Production/grid_overlay_tile.png";

            var tex = new Texture2D((int)CellSize, (int)CellSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            Color clear = new Color(0, 0, 0, 0);
            Color line = new Color(0.5f, 1f, 0.4f, 0.4f);

            int sz = (int)CellSize;
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    int d1 = (x + y) % sz;
                    int d2 = ((x - y) % sz + sz) % sz;
                    bool onDiag = d1 < 2 || d1 >= sz - 1 || d2 < 2 || d2 >= sz - 1;
                    tex.SetPixel(x, y, onDiag ? line : clear);
                }
            }

            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!importer.alphaIsTransparency)
                { importer.alphaIsTransparency = true; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Repeat)
                { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (importer.filterMode != FilterMode.Point)
                { importer.filterMode = FilterMode.Point; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        // ================================================================
        // Shared helpers
        // ================================================================
        static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void DestroyChild(Transform parent, string childName)
        {
            var child = parent.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        static bool EnsureSpriteImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!importer.alphaIsTransparency)
            { importer.alphaIsTransparency = true; changed = true; }
            if (changed) { importer.SaveAndReimport(); }
            return changed;
        }

        static int WireSprite(string goPath, string spritePath, bool preserveAspect)
        {
            var go = GameObject.Find(goPath);
            if (go == null) return 0;
            var img = go.GetComponent<Image>();
            if (img == null) return 0;
            EnsureSpriteImportSettings(spritePath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) return 0;
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = preserveAspect;
            return 1;
        }

    }
}
#endif
