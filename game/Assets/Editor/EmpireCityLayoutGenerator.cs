#if UNITY_EDITOR
using System.Collections.Generic;
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
        // P&C-style DENSE layout: buildings packed tight with 1-cell gaps.
        // Every district filled, multiple resource copies, decorative wall segments.
        // Districts: Military N, Magic NE, Admin ring, Support W, Resources S.
        static readonly (string id, string inst, int gx, int gy, int tier)[] DefaultBuildings = new[]
        {
            // === Central Stronghold (6×6) — occupies [21-26, 21-26] ===
            ("stronghold",        "stronghold_0",        21, 21, 3),

            // === Administration — tight ring around stronghold ===
            ("guild_hall",        "guild_hall_0",        16, 23, 2),  // 4×3 — west
            ("embassy",           "embassy_0",           16, 19, 2),  // 3×3 — southwest
            ("marketplace",       "marketplace_0",       28, 23, 2),  // 3×3 — east
            ("academy",           "academy_0",           28, 19, 2),  // 3×3 — southeast
            ("library",           "library_0",           22, 18, 1),  // 3×2 — south
            ("archive",           "archive_0",           26, 18, 1),  // 2×2 — south-right

            // === Military — north of stronghold (tight cluster) ===
            ("barracks",          "barracks_0",          20, 28, 2),  // 4×3 — north center
            ("training_ground",   "training_ground_0",   25, 28, 1),  // 3×3 — north right
            ("armory",            "armory_0",            15, 28, 1),  // 3×3 — north left
            ("wall",              "wall_0",              20, 32, 2),  // 3×1 — north perimeter
            ("wall",              "wall_1",              25, 32, 2),  // 3×1 — north perimeter

            // === Magic — east district (tight cluster) ===
            ("arcane_tower",      "arcane_tower_0",      32, 24, 2),  // 2×3 — NE
            ("enchanting_tower",  "enchanting_tower_0",  32, 20, 2),  // 2×3 — E
            ("observatory",       "observatory_0",       35, 22, 1),  // 2×3 — far E
            ("laboratory",        "laboratory_0",        32, 17, 1),  // 3×2 — SE
            ("arcane_tower",      "arcane_tower_1",      35, 18, 1),  // 2×3 — extra arcane

            // === Support — west district (tight cluster) ===
            ("hero_shrine",       "hero_shrine_0",       12, 24, 2),  // 3×3 — W
            ("forge",             "forge_0",             12, 20, 2),  // 3×2 — W
            ("forge",             "forge_1",              9, 22, 1),  // 3×2 — far W extra

            // === Resources — south, packed clusters ===
            // Grain farms — southwest cluster (tight 2-wide rows)
            ("grain_farm",        "grain_farm_0",        11, 15, 2),  // 2×2
            ("grain_farm",        "grain_farm_1",        14, 15, 2),  // 2×2
            ("grain_farm",        "grain_farm_2",        11, 12, 1),  // 2×2
            ("grain_farm",        "grain_farm_3",        14, 12, 1),  // 2×2
            ("grain_farm",        "grain_farm_4",        17, 14, 1),  // 2×2
            ("grain_farm",        "grain_farm_5",         8, 14, 1),  // 2×2 — extra farm
            // Iron mines — south center cluster
            ("iron_mine",         "iron_mine_0",         20, 15, 2),  // 2×2
            ("iron_mine",         "iron_mine_1",         23, 15, 1),  // 2×2
            ("iron_mine",         "iron_mine_2",         20, 12, 1),  // 2×2
            ("iron_mine",         "iron_mine_3",         23, 12, 1),  // 2×2 — extra mine
            // Stone quarries — southeast cluster
            ("stone_quarry",      "stone_quarry_0",      28, 15, 2),  // 2×2
            ("stone_quarry",      "stone_quarry_1",      31, 15, 1),  // 2×2
            ("stone_quarry",      "stone_quarry_2",      28, 12, 1),  // 2×2
            ("stone_quarry",      "stone_quarry_3",      31, 12, 1),  // 2×2 — extra quarry

            // === Defense — perimeter watch towers + walls ===
            ("watch_tower",       "watch_tower_0",       10, 30, 1),  // NW
            ("watch_tower",       "watch_tower_1",       34, 30, 1),  // NE
            ("watch_tower",       "watch_tower_2",       10, 10, 1),  // SW
            ("watch_tower",       "watch_tower_3",       34, 10, 1),  // SE
            ("watch_tower",       "watch_tower_4",       22, 33, 1),  // N center
            ("watch_tower",       "watch_tower_5",       22,  9, 1),  // S center
            ("wall",              "wall_2",              14, 32, 1),  // NW wall
            ("wall",              "wall_3",              30, 32, 1),  // NE wall
            ("wall",              "wall_4",              14, 10, 1),  // SW wall
            ("wall",              "wall_5",              30, 10, 1),  // SE wall
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
            // Must match CityGridView.FootprintScreenSize for consistent layout
            float w = (size.x + size.y) * HalfW * 0.90f;
            float h = w * 1.4f; // Taller bounding box — P&C buildings rise above footprint
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
            // Viewport background — dark green to match P&C terrain (catches any gaps)
            var vpBg = viewport.AddComponent<Image>();
            vpBg.color = new Color(0.04f, 0.03f, 0.06f, 1f); // dark purple-black matching terrain art edges
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
            // P&C-style zoomed-in default — buildings large and dominant on screen
            // Content is ~3200x2140. At 1.3x on 1080x1920 screen, buildings fill viewport
            // This persists in the scene — CityGridView reads it at Start()
            contentRect.localScale = Vector3.one * 1.3f;

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f; // P&C: subtle bounce at edges
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f; // P&C: smooth, not abrupt
            scroll.scrollSensitivity = 12f;

            // ================================================================
            // 3a. Warm stone base — P&C-style bright ground under terrain art
            // ================================================================
            var warmBase = CreateChild(content, "WarmBase");
            var warmBaseRect = warmBase.AddComponent<RectTransform>();
            warmBaseRect.anchorMin = Vector2.zero;
            warmBaseRect.anchorMax = Vector2.one;
            warmBaseRect.offsetMin = Vector2.zero;
            warmBaseRect.offsetMax = Vector2.zero;
            var warmBaseImg = warmBase.AddComponent<Image>();
            warmBaseImg.color = new Color(0.55f, 0.44f, 0.30f, 1f); // P&C brighter warm stone base
            warmBaseImg.raycastTarget = false;

            // ================================================================
            // 3b. Ground background (terrain art at reduced opacity over warm base)
            // ================================================================
            var outerBG = CreateChild(content, "GroundBG");
            var outerRect = outerBG.AddComponent<RectTransform>();
            outerRect.anchorMin = Vector2.zero;
            outerRect.anchorMax = Vector2.one;
            outerRect.offsetMin = Vector2.zero;
            outerRect.offsetMax = Vector2.zero;
            var outerImg = outerBG.AddComponent<Image>();

            // Use AI-generated terrain art — reduced opacity lets warm base show through
            string terrainPath = "Assets/Art/Environments/empire_terrain_bg.png";
            EnsureSpriteImportSettings(terrainPath);
            var terrainSprite = AssetDatabase.LoadAssetAtPath<Sprite>(terrainPath);
            if (terrainSprite != null)
            {
                outerImg.sprite = terrainSprite;
                outerImg.type = Image.Type.Simple;
                outerImg.preserveAspect = false;
                // P&C brightness: terrain at 50% opacity, warm base shows through dark areas
                outerImg.color = new Color(1.45f, 1.30f, 1.10f, 0.55f); // P&C: brighter terrain overlay
            }
            else
            {
                outerImg.color = new Color(0.35f, 0.28f, 0.18f, 1f);
            }
            outerImg.raycastTarget = true;

            // ================================================================
            // 3c. Warm amber wash — P&C ambient lighting
            // ================================================================
            var ambientWash = CreateChild(content, "AmbientWash");
            var washRect = ambientWash.AddComponent<RectTransform>();
            washRect.anchorMin = Vector2.zero;
            washRect.anchorMax = Vector2.one;
            washRect.offsetMin = Vector2.zero;
            washRect.offsetMax = Vector2.zero;
            var washImg = ambientWash.AddComponent<Image>();
            washImg.color = new Color(0.55f, 0.42f, 0.22f, 0.22f); // P&C: stronger warm ambient
            washImg.raycastTarget = false;

            // ================================================================
            // 4. Edge fog vignette (P&C-style natural boundary fade)
            // ================================================================
            // Instead of hard wall segments, create gradient fog panels on each
            // edge that fade from transparent (at playable area) to dark purple-black.
            var cornerBL = GridToIso(PlayMinX, PlayMinY);   // bottom vertex
            var cornerBR = GridToIso(PlayMaxX, PlayMinY);   // right vertex
            var cornerTR = GridToIso(PlayMaxX, PlayMaxY);   // top vertex
            var cornerTL = GridToIso(PlayMinX, PlayMaxY);   // left vertex

            CreateEdgeFogTexture();
            var fogSprite = LoadOrCreateSprite("Assets/Art/UI/Production/edge_fog.png");
            Color fogColor = new Color(0.04f, 0.03f, 0.08f, 0.55f); // dark purple edge fog matching terrain art

            // 4 fog panels along each diamond edge, extending outward
            float fogThickness = CellSize * 6f; // thick fog band
            CreateEdgeFog(content, "FogBottom", fogSprite, fogColor, cornerBL, cornerBR, fogThickness, true);
            CreateEdgeFog(content, "FogRight", fogSprite, fogColor, cornerBR, cornerTR, fogThickness, true);
            CreateEdgeFog(content, "FogTop", fogSprite, fogColor, cornerTR, cornerTL, fogThickness, false);
            CreateEdgeFog(content, "FogLeft", fogSprite, fogColor, cornerTL, cornerBL, fogThickness, false);

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
            // Grid overlay hidden by default — only shown during move mode
            goImg.color = new Color(0.12f, 0.18f, 0.10f, 0.12f); // subtle grid when active
            gridOverlayGO.SetActive(false);

            // ================================================================
            // 5b. Focal aura behind Stronghold (P&C dragon-equivalent)
            // ================================================================
            Vector2 shCenter = GridToIsoCenter(21, 21, new Vector2Int(6, 6));

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
            // 5b. P&C-style connecting road paths between buildings
            // ================================================================
            var roadsGO = CreateChild(content, "Roads");
            var roadsRect = roadsGO.AddComponent<RectTransform>();
            roadsRect.anchorMin = new Vector2(0.5f, 0.5f);
            roadsRect.anchorMax = new Vector2(0.5f, 0.5f);
            roadsRect.pivot = new Vector2(0.5f, 0.5f);
            roadsRect.sizeDelta = new Vector2(contentW, contentH);

            // Road segments: pairs of grid coordinates forming a dense path network
            // Connect stronghold center (24,24) to all districts
            var roadSegments = new (int fromX, int fromY, int toX, int toY)[]
            {
                // Main roads from stronghold center to cardinal directions
                (24, 24, 29, 24), // Stronghold → Marketplace (east)
                (24, 24, 19, 24), // Stronghold → Guild Hall (west)
                (24, 24, 24, 19), // Stronghold → Library (south)
                (24, 24, 24, 29), // Stronghold → Barracks (north)
                // Cross connections to districts
                (29, 24, 33, 23), // Marketplace → Magic District
                (19, 24, 14, 24), // Guild Hall → Support District
                (24, 19, 24, 16), // Library → Resource District
                (24, 29, 24, 33), // Barracks → Military outer
                // Extended roads
                (33, 23, 36, 22), // Magic → Far East
                (14, 24, 10, 23), // Support → Far West
                (24, 16, 18, 14), // Resources → SW farms
                (24, 16, 30, 14), // Resources → SE quarries
                // Ring road connecting districts
                (19, 29, 14, 29), // Military W connector
                (25, 29, 30, 29), // Military E connector
                (14, 24, 14, 16), // West corridor
                (33, 23, 33, 16), // East corridor
            };

            Color roadColor = new Color(0.14f, 0.12f, 0.08f, 0.50f);     // dark stone path on P&C terrain
            Color roadEdge = new Color(0.10f, 0.08f, 0.05f, 0.35f);      // slightly darker edge
            float roadWidth = 5f;                                          // thin path

            foreach (var (fx, fy, tx, ty) in roadSegments)
            {
                Vector2 from = GridToIso(fx + 0.5f, fy + 0.5f);
                Vector2 to = GridToIso(tx + 0.5f, ty + 0.5f);
                CreateRoadSegment(roadsGO, from, to, roadColor, roadEdge, roadWidth);
            }

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
                "stronghold", "arcane_tower", "enchanting_tower", "hero_shrine", "observatory",
                "forge", "laboratory", "guild_hall"
            };
            var glowColors = new System.Collections.Generic.Dictionary<string, Color> {
                { "stronghold",       new Color(0.90f, 0.60f, 0.15f, 0.18f) },  // dramatic amber glow
                { "arcane_tower",     new Color(0.45f, 0.25f, 0.85f, 0.14f) },  // vivid purple
                { "enchanting_tower", new Color(0.30f, 0.65f, 0.90f, 0.12f) },  // bright cyan
                { "hero_shrine",      new Color(0.85f, 0.70f, 0.20f, 0.14f) },  // warm gold
                { "observatory",      new Color(0.25f, 0.55f, 0.80f, 0.10f) },  // sky blue
                { "forge",            new Color(0.90f, 0.40f, 0.10f, 0.14f) },  // forge fire orange
                { "laboratory",       new Color(0.35f, 0.80f, 0.40f, 0.10f) },  // alchemical green
                { "guild_hall",       new Color(0.80f, 0.65f, 0.15f, 0.10f) },  // warm gold
            };

            // ================================================================
            // 7. Place all buildings (isometric positions)
            // ================================================================
            int placed = 0;
            var occupiedCells = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (var (id, inst, gx, gy, tier) in DefaultBuildings)
            {
                if (!CityGridView.BuildingSizes.TryGetValue(id, out var size))
                    size = new Vector2Int(2, 2);

                // Overlap safety check — skip buildings that collide with already-placed ones
                bool overlaps = false;
                var cells = new System.Collections.Generic.List<Vector2Int>();
                for (int ox = 0; ox < size.x; ox++)
                    for (int oy = 0; oy < size.y; oy++)
                    {
                        var cell = new Vector2Int(gx + ox, gy + oy);
                        cells.Add(cell);
                        if (occupiedCells.Contains(cell))
                            overlaps = true;
                    }
                if (overlaps)
                {
                    Debug.LogWarning($"[EmpireCityLayout] Skipping {inst} at ({gx},{gy}) size {size} — overlaps existing building");
                    continue;
                }
                foreach (var c in cells) occupiedCells.Add(c);

                string spritePath = $"Assets/Art/Buildings/{id}_t{tier}.png";
                EnsureSpriteImportSettings(spritePath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                // Create building visual at isometric position
                var bldgGO = CreateChild(buildingsGO, $"Building_{inst}");
                var bldgRect = bldgGO.AddComponent<RectTransform>();

                Vector2 isoPos = GridToIsoCenter(gx, gy, size);
                Vector2 footprint = FootprintSize(size);
                // Stronghold gets size boost for dramatic dominance (P&C-style)
                if (id == "stronghold")
                    footprint *= 1.2f; // Moderate boost — 6×6 base is already dominant
                // Offset upward so building base sits on diamond footprint
                float yOffset = footprint.y * 0.15f;
                bldgRect.anchoredPosition = isoPos + Vector2.up * yOffset;
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
                    float glowScale = id == "stronghold" ? 1.4f : 1.2f;
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

                // P&C: Category icon (top-left corner)
                CreateCategoryIcon(bldgGO, id);

                // Action indicator (top-right corner)
                CreateActionIndicator(bldgGO, inst);

                placed++;
            }

            // ================================================================
            // 7a. Decorative elements to fill gaps (P&C-style dense city)
            // ================================================================
            // P&C cities have NO empty ground — every gap has trees, rocks, or decor.
            // Place small decorative elements in gaps between buildings.
            var decorPositions = new (int gx, int gy, string symbol, Color color, float size)[]
            {
                // Trees between admin district buildings
                (20, 19, "\u2663", new Color(0.25f, 0.55f, 0.20f, 0.70f), 18f),  // ♣ tree
                (27, 20, "\u2663", new Color(0.20f, 0.50f, 0.18f, 0.65f), 16f),
                (15, 22, "\u2663", new Color(0.28f, 0.52f, 0.22f, 0.70f), 20f),
                // Trees around military
                (18, 31, "\u2663", new Color(0.22f, 0.48f, 0.18f, 0.65f), 17f),
                (29, 31, "\u2663", new Color(0.25f, 0.50f, 0.20f, 0.60f), 15f),
                // Rocks in resource district
                (17, 11, "\u25C6", new Color(0.45f, 0.42f, 0.38f, 0.55f), 12f),  // ◆ rock
                (26, 11, "\u25C6", new Color(0.40f, 0.38f, 0.35f, 0.50f), 14f),
                (19, 10, "\u25C6", new Color(0.42f, 0.40f, 0.36f, 0.50f), 10f),
                // Fountain/wells in central area
                (20, 22, "\u2756", new Color(0.40f, 0.65f, 0.90f, 0.65f), 14f),  // ❖ fountain
                (27, 22, "\u2756", new Color(0.35f, 0.60f, 0.85f, 0.60f), 12f),
                // Lanterns along roads
                (24, 27, "\u2739", new Color(0.90f, 0.75f, 0.25f, 0.55f), 10f),  // ✹ lantern
                (24, 20, "\u2739", new Color(0.85f, 0.70f, 0.20f, 0.50f), 10f),
                (19, 20, "\u2739", new Color(0.88f, 0.72f, 0.22f, 0.50f), 10f),
                (29, 20, "\u2739", new Color(0.88f, 0.72f, 0.22f, 0.50f), 10f),
                // More trees around edges
                (8, 18, "\u2663", new Color(0.20f, 0.45f, 0.18f, 0.60f), 22f),
                (36, 18, "\u2663", new Color(0.22f, 0.48f, 0.20f, 0.55f), 20f),
                (8, 28, "\u2663", new Color(0.18f, 0.42f, 0.15f, 0.65f), 24f),
                (36, 28, "\u2663", new Color(0.20f, 0.45f, 0.18f, 0.60f), 22f),
                // Fill gaps in magic district
                (35, 26, "\u2726", new Color(0.55f, 0.30f, 0.80f, 0.40f), 12f),  // ✦ arcane crystal
                (31, 19, "\u2726", new Color(0.50f, 0.28f, 0.75f, 0.35f), 10f),
                // Fill gaps in support district
                (9, 18, "\u2663", new Color(0.22f, 0.50f, 0.20f, 0.55f), 18f),
                (9, 26, "\u2663", new Color(0.25f, 0.52f, 0.22f, 0.60f), 16f),
                // Resource district infill
                (16, 10, "\u2663", new Color(0.25f, 0.48f, 0.20f, 0.50f), 14f),
                (26, 14, "\u25C6", new Color(0.38f, 0.36f, 0.32f, 0.45f), 11f),
                (34, 14, "\u25C6", new Color(0.42f, 0.40f, 0.36f, 0.50f), 12f),
            };

            foreach (var (gx, gy, symbol, color, fontSize) in decorPositions)
            {
                Vector2 pos = GridToIso(gx + 0.5f, gy + 0.5f);
                var decorGO = CreateChild(buildingsGO, $"Decor_{gx}_{gy}");
                var decorRect = decorGO.AddComponent<RectTransform>();
                decorRect.anchoredPosition = pos;
                decorRect.sizeDelta = new Vector2(fontSize * 2f, fontSize * 2f);
                var decorText = decorGO.AddComponent<Text>();
                decorText.text = symbol;
                decorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                decorText.fontSize = (int)fontSize;
                decorText.alignment = TextAnchor.MiddleCenter;
                decorText.color = color;
                decorText.raycastTarget = false;
                // Push decorations behind building sprites
                decorGO.transform.SetAsFirstSibling();
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
            tgoRect.anchoredPosition = shCenter + Vector2.up * 80f;
            tgoRect.sizeDelta = new Vector2(300, 240);
            var tgoImg = topGlowOuter.AddComponent<Image>();
            tgoImg.sprite = radialSprite;
            tgoImg.color = new Color(0.85f, 0.55f, 0.12f, 0.06f);
            tgoImg.raycastTarget = false;

            var topGlowInner = CreateChild(content, "StrongholdTopGlowInner");
            var tgiRect = topGlowInner.AddComponent<RectTransform>();
            tgiRect.anchorMin = new Vector2(0.5f, 0.5f);
            tgiRect.anchorMax = new Vector2(0.5f, 0.5f);
            tgiRect.pivot = new Vector2(0.5f, 0.5f);
            tgiRect.anchoredPosition = shCenter + Vector2.up * 60f;
            tgiRect.sizeDelta = new Vector2(180, 140);
            var tgiImg = topGlowInner.AddComponent<Image>();
            tgiImg.sprite = radialSprite;
            tgiImg.color = new Color(0.95f, 0.70f, 0.20f, 0.08f);
            tgiImg.raycastTarget = false;

            // Bottom subtle darken — very light on bright terrain
            var botFog = CreateChild(content, "BottomDarkFade");
            var botFogRect = botFog.AddComponent<RectTransform>();
            botFogRect.anchorMin = new Vector2(0f, 0f);
            botFogRect.anchorMax = new Vector2(1f, 0.12f);
            botFogRect.offsetMin = Vector2.zero;
            botFogRect.offsetMax = Vector2.zero;
            var botFogImg = botFog.AddComponent<Image>();
            botFogImg.color = new Color(0.03f, 0.02f, 0.05f, 0.35f);
            botFogImg.raycastTarget = false;

            // Left vignette — very subtle on bright terrain
            var leftFog = CreateChild(content, "LeftVignetteFog");
            var leftFogRect = leftFog.AddComponent<RectTransform>();
            leftFogRect.anchorMin = new Vector2(0f, 0f);
            leftFogRect.anchorMax = new Vector2(0.10f, 1f);
            leftFogRect.offsetMin = Vector2.zero;
            leftFogRect.offsetMax = Vector2.zero;
            var leftFogImg = leftFog.AddComponent<Image>();
            leftFogImg.color = new Color(0.03f, 0.02f, 0.05f, 0.25f);
            leftFogImg.raycastTarget = false;

            // Right vignette — very subtle
            var rightFog = CreateChild(content, "RightVignetteFog");
            var rightFogRect = rightFog.AddComponent<RectTransform>();
            rightFogRect.anchorMin = new Vector2(0.90f, 0f);
            rightFogRect.anchorMax = new Vector2(1f, 1f);
            rightFogRect.offsetMin = Vector2.zero;
            rightFogRect.offsetMax = Vector2.zero;
            var rightFogImg = rightFog.AddComponent<Image>();
            rightFogImg.color = new Color(0.03f, 0.02f, 0.05f, 0.25f);
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
            // 9c. Construction progress overlays during upgrades
            // ================================================================
            viewport.AddComponent<ConstructionOverlayManager>();

            // 9c2. Notification badges on buildings (P&C-style red dots)
            viewport.AddComponent<BuildingNotificationBadges>();

            // ================================================================
            // 9d. Ambient city effects (sparkles, smoke, etc.)
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
        // P&C: Category icon in top-left corner of building
        // ================================================================
        static readonly Dictionary<string, (string Symbol, Color Tint)> CategoryIcons = new()
        {
            // Core
            { "stronghold", ("\u265B", new Color(1f, 0.85f, 0.30f)) },       // ♛ gold
            // Military
            { "barracks", ("\u2694", new Color(0.85f, 0.35f, 0.30f)) },       // ⚔ red
            { "training_ground", ("\u2694", new Color(0.85f, 0.55f, 0.25f)) },// ⚔ orange
            { "armory", ("\u2748", new Color(0.60f, 0.65f, 0.75f)) },         // ❈ steel
            { "wall", ("\u2616", new Color(0.55f, 0.55f, 0.55f)) },           // ☖ grey
            { "watch_tower", ("\u25B2", new Color(0.55f, 0.55f, 0.55f)) },    // ▲ grey
            // Resource
            { "grain_farm", ("\u2740", new Color(0.90f, 0.82f, 0.30f)) },     // ❀ gold
            { "iron_mine", ("\u2666", new Color(0.60f, 0.65f, 0.80f)) },      // ♦ steel blue
            { "stone_quarry", ("\u25C8", new Color(0.75f, 0.72f, 0.65f)) },   // ◈ tan
            { "arcane_tower", ("\u2726", new Color(0.65f, 0.35f, 0.90f)) },   // ✦ purple
            { "marketplace", ("\u2617", new Color(0.85f, 0.68f, 0.25f)) },    // ☗ gold
            // Research
            { "library", ("\u2697", new Color(0.30f, 0.70f, 0.90f)) },        // ⚗ blue
            { "academy", ("\u2697", new Color(0.30f, 0.70f, 0.90f)) },        // ⚗ blue
            { "laboratory", ("\u2697", new Color(0.30f, 0.70f, 0.90f)) },     // ⚗ blue
            { "observatory", ("\u2729", new Color(0.40f, 0.60f, 0.90f)) },    // ✩ light blue
            { "archive", ("\u2710", new Color(0.40f, 0.60f, 0.90f)) },        // ✐ light blue
            // Hero
            { "guild_hall", ("\u2655", new Color(0.85f, 0.55f, 0.20f)) },     // ♕ amber
            { "hero_shrine", ("\u2606", new Color(0.90f, 0.70f, 0.25f)) },    // ☆ gold
            { "forge", ("\u2692", new Color(0.80f, 0.45f, 0.20f)) },          // ⚒ copper
            { "enchanting_tower", ("\u2735", new Color(0.70f, 0.40f, 0.90f)) }, // ✵ purple
            { "embassy", ("\u2691", new Color(0.30f, 0.65f, 0.85f)) },        // ⚑ blue
        };

        static void CreateCategoryIcon(GameObject parent, string buildingId)
        {
            if (!CategoryIcons.TryGetValue(buildingId, out var info)) return;

            var iconGO = CreateChild(parent, "CategoryIcon");
            var rect = iconGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.02f, 0.88f);
            rect.anchorMax = new Vector2(0.18f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = iconGO.AddComponent<Image>();
            bg.color = new Color(info.Tint.r * 0.2f, info.Tint.g * 0.2f, info.Tint.b * 0.2f, 0.7f);
            bg.raycastTarget = false;

            var textGO = CreateChild(iconGO, "Symbol");
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = info.Symbol;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = info.Tint;
            text.raycastTarget = false;
        }

        // ================================================================
        // Helper: Create action indicator overlay on a building
        // ================================================================
        static void CreateActionIndicator(GameObject parent, string instanceId)
        {
            var indicatorGO = CreateChild(parent, "ActionIndicator");

            // P&C: Circular upgrade indicator with glow — top-right
            var radialSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Production/radial_gradient.png");
            if (radialSpr == null)
                radialSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            if (radialSpr == null)
                radialSpr = GetOrCreateRadialGradient();

            var upgradeGO = CreateChild(indicatorGO, "UpgradeIcon");
            var upRect = upgradeGO.AddComponent<RectTransform>();
            upRect.anchorMin = new Vector2(0.78f, 0.82f);
            upRect.anchorMax = new Vector2(1.02f, 1.02f);
            upRect.offsetMin = Vector2.zero;
            upRect.offsetMax = Vector2.zero;

            // Glow ring behind
            var upGlow = CreateChild(upgradeGO, "Glow");
            var upGlowRect = upGlow.AddComponent<RectTransform>();
            upGlowRect.anchorMin = new Vector2(-0.30f, -0.30f);
            upGlowRect.anchorMax = new Vector2(1.30f, 1.30f);
            upGlowRect.offsetMin = Vector2.zero;
            upGlowRect.offsetMax = Vector2.zero;
            var upGlowImg = upGlow.AddComponent<Image>();
            upGlowImg.color = new Color(0.20f, 0.75f, 0.25f, 0.30f);
            if (radialSpr != null) upGlowImg.sprite = radialSpr;
            upGlowImg.raycastTarget = false;

            var upBg = upgradeGO.AddComponent<Image>();
            upBg.color = new Color(0.15f, 0.65f, 0.15f, 0.92f);
            if (radialSpr != null) { upBg.sprite = radialSpr; upBg.type = Image.Type.Simple; }
            upBg.raycastTarget = false;

            var upTextGO = CreateChild(upgradeGO, "Arrow");
            var upTextRect = upTextGO.AddComponent<RectTransform>();
            upTextRect.anchorMin = Vector2.zero;
            upTextRect.anchorMax = Vector2.one;
            upTextRect.offsetMin = Vector2.zero;
            upTextRect.offsetMax = Vector2.zero;
            var upText = upTextGO.AddComponent<Text>();
            upText.text = "\u2B06";
            upText.alignment = TextAnchor.MiddleCenter;
            upText.fontSize = 11;
            upText.fontStyle = FontStyle.Bold;
            upText.color = Color.white;
            upText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            upText.raycastTarget = false;
            var upTextShadow = upTextGO.AddComponent<Shadow>();
            upTextShadow.effectColor = new Color(0, 0, 0, 0.8f);
            upTextShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // P&C: Timer pill with gold border for construction
            var timerGO = CreateChild(indicatorGO, "TimerGroup");
            var tmRect = timerGO.AddComponent<RectTransform>();
            tmRect.anchorMin = new Vector2(0.05f, 0.88f);
            tmRect.anchorMax = new Vector2(0.95f, 1.02f);
            tmRect.offsetMin = Vector2.zero;
            tmRect.offsetMax = Vector2.zero;
            var tmBg = timerGO.AddComponent<Image>();
            tmBg.color = new Color(0.06f, 0.04f, 0.12f, 0.90f);
            tmBg.raycastTarget = false;
            var tmOutline = timerGO.AddComponent<Outline>();
            tmOutline.effectColor = new Color(0.85f, 0.65f, 0.18f, 0.65f);
            tmOutline.effectDistance = new Vector2(0.8f, -0.8f);
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

            // P&C: Circular collect indicator with glow
            var collectGO = CreateChild(indicatorGO, "CollectIcon");
            var colRect = collectGO.AddComponent<RectTransform>();
            colRect.anchorMin = new Vector2(0.78f, 0.82f);
            colRect.anchorMax = new Vector2(1.02f, 1.02f);
            colRect.offsetMin = Vector2.zero;
            colRect.offsetMax = Vector2.zero;

            var colGlow = CreateChild(collectGO, "Glow");
            var colGlowRect = colGlow.AddComponent<RectTransform>();
            colGlowRect.anchorMin = new Vector2(-0.30f, -0.30f);
            colGlowRect.anchorMax = new Vector2(1.30f, 1.30f);
            colGlowRect.offsetMin = Vector2.zero;
            colGlowRect.offsetMax = Vector2.zero;
            var colGlowImg = colGlow.AddComponent<Image>();
            colGlowImg.color = new Color(0.85f, 0.65f, 0.15f, 0.30f);
            if (radialSpr != null) colGlowImg.sprite = radialSpr;
            colGlowImg.raycastTarget = false;

            var colBg = collectGO.AddComponent<Image>();
            colBg.color = new Color(0.80f, 0.60f, 0.15f, 0.92f); // gold "ready" badge
            if (radialSpr != null) { colBg.sprite = radialSpr; colBg.type = Image.Type.Simple; }
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
            colText.fontSize = 14;
            colText.fontStyle = FontStyle.Bold;
            colText.color = Color.white;
            colText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            colText.raycastTarget = false;
            var colShadow = colTextGO.AddComponent<Shadow>();
            colShadow.effectColor = new Color(0, 0, 0, 0.8f);
            colShadow.effectDistance = new Vector2(0.5f, -0.5f);

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

            // Compact pill-shaped label BELOW building (P&C-style name plate)
            var labelGO = CreateChild(parent, "NameLabel");
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(-0.05f, -0.08f);
            labelRect.anchorMax = new Vector2(1.05f, 0.06f);
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
            int size = 256; // Larger texture for less obvious tiling at multiple zoom levels

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            // P&C-style terrain: dark but RICH — mossy greens, stone greys, warm earth
            // P&C is dark but not flat — it has depth, texture variation, warm undertones
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Multi-octave noise for natural terrain variation
                    float n1 = Mathf.PerlinNoise(x * 0.012f + 0.5f, y * 0.012f + 0.5f);     // large biome patches
                    float n2 = Mathf.PerlinNoise(x * 0.035f + 10.5f, y * 0.035f + 10.5f);    // medium detail
                    float n3 = Mathf.PerlinNoise(x * 0.08f + 20.5f, y * 0.08f + 20.5f);      // fine texture grain
                    float n4 = Mathf.PerlinNoise(x * 0.018f + 30.5f, y * 0.018f + 30.5f);    // earthy/stone patches
                    float n5 = Mathf.PerlinNoise(x * 0.06f + 50.5f, y * 0.06f + 50.5f);      // warm highlights

                    float grassNoise = n1 * 0.08f + n2 * 0.04f + n3 * 0.02f;
                    float earthPatch = n4 > 0.50f ? (n4 - 0.50f) * 0.14f : 0f; // stone/earth patches
                    float warmSpot = n5 > 0.60f ? (n5 - 0.60f) * 0.10f : 0f;   // subtle warm highlights

                    // Base: dark forest floor with mossy greens + warm earth undertones
                    float r = 0.10f + grassNoise * 0.5f + earthPatch * 1.0f + warmSpot * 1.5f;
                    float g = 0.14f + grassNoise * 0.8f - earthPatch * 0.2f + warmSpot * 0.4f;
                    float b = 0.08f + grassNoise * 0.3f + earthPatch * 0.15f - warmSpot * 0.3f;

                    // Clamp to keep in range
                    r = Mathf.Clamp01(r);
                    g = Mathf.Clamp01(g);
                    b = Mathf.Clamp01(b);

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

            // Isometric diamond tile: CellSize wide × CellSize/2 tall (2:1 ratio)
            int tw = (int)CellSize;     // 64
            int th = (int)(CellSize / 2); // 32
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            Color clear = new Color(0, 0, 0, 0);
            Color line = new Color(0.55f, 0.35f, 0.85f, 0.5f); // dark fantasy purple grid

            int hw = tw / 2; // 32
            int hh = th / 2; // 16
            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    // Diamond edge: |x - hw| / hw + |y - hh| / hh == 1
                    // Scaled: |x - hw| * hh + |y - hh| * hw == hw * hh
                    int dist = Mathf.Abs(x - hw) * hh + Mathf.Abs(y - hh) * hw;
                    int target = hw * hh;
                    bool onEdge = Mathf.Abs(dist - target) < hw; // ~1px line
                    tex.SetPixel(x, y, onEdge ? line : clear);
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

        /// <summary>Create a thin road segment between two isometric positions.</summary>
        static void CreateRoadSegment(GameObject parent, Vector2 from, Vector2 to, Color color, Color edgeColor, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1f) return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Vector2 center = (from + to) * 0.5f;

            // Edge (slightly wider, darker)
            var edgeGO = CreateChild(parent, "RoadEdge");
            var edgeRect = edgeGO.AddComponent<RectTransform>();
            edgeRect.anchoredPosition = center;
            edgeRect.sizeDelta = new Vector2(length, width + 2f);
            edgeRect.localRotation = Quaternion.Euler(0, 0, angle);
            var edgeImg = edgeGO.AddComponent<Image>();
            edgeImg.color = edgeColor;
            edgeImg.raycastTarget = false;

            // Road fill
            var roadGO = CreateChild(parent, "Road");
            var roadRect = roadGO.AddComponent<RectTransform>();
            roadRect.anchoredPosition = center;
            roadRect.sizeDelta = new Vector2(length, width);
            roadRect.localRotation = Quaternion.Euler(0, 0, angle);
            var roadImg = roadGO.AddComponent<Image>();
            roadImg.color = color;
            roadImg.raycastTarget = false;
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

        // ================================================================
        // Edge fog (P&C-style natural boundary)
        // ================================================================

        static void CreateEdgeFogTexture()
        {
            string path = "Assets/Art/UI/Production/edge_fog.png";
            // Gradient: transparent on one edge → opaque on the other
            int w = 8, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                // y=0 is transparent (near playable area), y=h-1 is opaque (outer)
                float t = y / (float)(h - 1);
                // Ease-in for smooth fog: t^2 ramp
                float alpha = t * t;
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
            EnsureSpriteImportSettings(path);
        }

        static Sprite LoadOrCreateSprite(string path)
        {
            EnsureSpriteImportSettings(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Create a fog panel along one edge of the diamond, extending outward.
        /// The fog gradient goes from transparent (at the edge) to opaque (away from playable area).
        /// </summary>
        static void CreateEdgeFog(GameObject parent, string name, Sprite fogSprite, Color fogColor,
            Vector2 from, Vector2 to, float thickness, bool outwardDown)
        {
            var fogGO = CreateChild(parent, name);
            var fogRect = fogGO.AddComponent<RectTransform>();

            Vector2 mid = (from + to) * 0.5f;
            Vector2 delta = to - from;
            float length = delta.magnitude + thickness; // extend past corners
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // Position the fog strip along the edge, offset outward by half thickness
            Vector2 edgeNormal = new Vector2(-delta.y, delta.x).normalized;
            if (!outwardDown) edgeNormal = -edgeNormal;
            // Shift center outward so the transparent edge aligns with playable boundary
            fogRect.anchoredPosition = mid + edgeNormal * (thickness * 0.5f);
            fogRect.sizeDelta = new Vector2(length, thickness);
            fogRect.localRotation = Quaternion.Euler(0, 0, angle);

            var fogImg = fogGO.AddComponent<Image>();
            if (fogSprite != null)
            {
                fogImg.sprite = fogSprite;
                fogImg.type = Image.Type.Simple;
            }
            fogImg.color = fogColor;
            fogImg.raycastTarget = false;
            // Flip the gradient so transparent side faces the playable area
            if (!outwardDown)
                fogRect.localScale = new Vector3(1, -1, 1);
        }
    }
}
#endif
