#if UNITY_EDITOR
// Run from Unity Editor menu: AshenThrone → Generate Art Assets
// Creates all placeholder art: combat grid prefab, UI prefab hierarchies,
// hero placeholder sprites, and scene UI setup.
// Safe to re-run — existing assets are overwritten.

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Editor
{
    public static class ArtAssetGenerator
    {
        private const string PrefabsPath = "Assets/Prefabs";
        private const string MaterialsPath = "Assets/Art/Materials";

        [MenuItem("AshenThrone/Generate Art Assets")]
        public static void GenerateAll()
        {
            GenerateCombatGridPrefab();
            GenerateCardWidgetPrefab();
            GenerateDamagePopupPrefab();
            GenerateStatusIconPrefab();
            GenerateTurnTokenPrefab();
            GenerateResearchNodeWidgetPrefab();
            GenerateHeroPlaceholderPrefab();
            GenerateBuildingPlaceholderPrefabs();
            GenerateResourceIconPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtAssetGenerator] All art placeholder assets generated.");
        }

        // ---------------------------------------------------------------
        // Combat Grid — 7x5 tile prefab
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Combat Grid")]
        public static void GenerateCombatGridPrefab()
        {
            var root = new GameObject("CombatGrid");
            float tileSize = 1.1f;
            float tileHeight = 0.1f;

            var playerMat = LoadOrDefaultMat("GridTile_Player");
            var neutralMat = LoadOrDefaultMat("GridTile_Neutral");
            var enemyMat = LoadOrDefaultMat("GridTile_Enemy");

            for (int col = 0; col < 7; col++)
            {
                for (int row = 0; row < 5; row++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{col}_{row}";
                    tile.transform.SetParent(root.transform);
                    tile.transform.localPosition = new Vector3(col * tileSize, 0, row * tileSize);
                    tile.transform.localScale = new Vector3(1f, tileHeight, 1f);

                    Material mat;
                    if (col <= 2) mat = playerMat;
                    else if (col == 3) mat = neutralMat;
                    else mat = enemyMat;

                    var renderer = tile.GetComponent<Renderer>();
                    if (renderer != null && mat != null)
                        renderer.sharedMaterial = mat;
                }
            }

            SavePrefab(root, $"{PrefabsPath}/CombatGridRoot.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created CombatGridRoot.prefab (7x5 grid)");
        }

        // ---------------------------------------------------------------
        // CardWidget — full UI hierarchy
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Card Widget")]
        public static void GenerateCardWidgetPrefab()
        {
            var root = CreateUIRoot("CardWidget", 160, 220);

            // Card background
            var bg = AddImage(root, "Background", new Color(0.15f, 0.12f, 0.1f, 1f));
            StretchToParent(bg);

            // Card art area
            var artArea = AddImage(root, "CardArt", new Color(0.3f, 0.3f, 0.35f, 1f));
            var artRect = artArea.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.05f, 0.4f);
            artRect.anchorMax = new Vector2(0.95f, 0.95f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            // Energy cost badge (top-left)
            var costBadge = AddImage(root, "CostBadge", new Color(0.2f, 0.5f, 0.9f, 1f));
            var costRect = costBadge.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0, 1);
            costRect.anchorMax = new Vector2(0, 1);
            costRect.pivot = new Vector2(0, 1);
            costRect.sizeDelta = new Vector2(36, 36);
            costRect.anchoredPosition = new Vector2(4, -4);

            var costLabel = AddText(costBadge, "CostLabel", "3", 18, TextAnchor.MiddleCenter);
            StretchToParent(costLabel);

            // Card name
            var nameLabel = AddText(root, "NameLabel", "Card Name", 14, TextAnchor.MiddleCenter);
            var nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.28f);
            nameRect.anchorMax = new Vector2(1, 0.4f);
            nameRect.offsetMin = new Vector2(4, 0);
            nameRect.offsetMax = new Vector2(-4, 0);

            // Element icon (top-right)
            var elementIcon = AddImage(root, "ElementIcon", new Color(1f, 0.45f, 0.1f, 1f));
            var elemRect = elementIcon.GetComponent<RectTransform>();
            elemRect.anchorMin = new Vector2(1, 1);
            elemRect.anchorMax = new Vector2(1, 1);
            elemRect.pivot = new Vector2(1, 1);
            elemRect.sizeDelta = new Vector2(28, 28);
            elemRect.anchoredPosition = new Vector2(-4, -4);

            // Description area
            var descLabel = AddText(root, "DescriptionLabel", "Card effect description text", 10, TextAnchor.UpperLeft);
            var descRect = descLabel.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0.05f);
            descRect.anchorMax = new Vector2(1, 0.28f);
            descRect.offsetMin = new Vector2(8, 0);
            descRect.offsetMax = new Vector2(-8, 0);

            // Combo indicator (bottom-right)
            var comboIndicator = AddImage(root, "ComboIndicator", new Color(1f, 0.8f, 0.2f, 1f));
            var comboRect = comboIndicator.GetComponent<RectTransform>();
            comboRect.anchorMin = new Vector2(1, 0);
            comboRect.anchorMax = new Vector2(1, 0);
            comboRect.pivot = new Vector2(1, 0);
            comboRect.sizeDelta = new Vector2(24, 24);
            comboRect.anchoredPosition = new Vector2(-4, 4);
            comboIndicator.SetActive(false);

            root.AddComponent<Button>();
            root.AddComponent<CanvasGroup>();

            SavePrefab(root, $"{PrefabsPath}/CardWidget.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created CardWidget.prefab with full UI hierarchy");
        }

        // ---------------------------------------------------------------
        // DamagePopup — floating damage number
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Damage Popup")]
        public static void GenerateDamagePopupPrefab()
        {
            var root = CreateUIRoot("DamagePopup", 120, 40);
            root.AddComponent<CanvasGroup>();

            var label = AddText(root, "DamageLabel", "999", 24, TextAnchor.MiddleCenter);
            StretchToParent(label);
            var text = label.GetComponent<Text>();
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 28;

            // Outline for readability
            var outline = label.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);

            SavePrefab(root, $"{PrefabsPath}/DamagePopup.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created DamagePopup.prefab");
        }

        // ---------------------------------------------------------------
        // StatusIcon — status effect badge
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Status Icon")]
        public static void GenerateStatusIconPrefab()
        {
            var root = CreateUIRoot("StatusIcon", 36, 36);

            var bg = AddImage(root, "IconBg", new Color(0.2f, 0.2f, 0.25f, 0.9f));
            StretchToParent(bg);

            var icon = AddImage(root, "Icon", Color.white);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.1f);
            iconRect.anchorMax = new Vector2(0.9f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var durationLabel = AddText(root, "DurationLabel", "3", 12, TextAnchor.MiddleCenter);
            var durRect = durationLabel.GetComponent<RectTransform>();
            durRect.anchorMin = new Vector2(0.5f, 0);
            durRect.anchorMax = new Vector2(1, 0.5f);
            durRect.offsetMin = Vector2.zero;
            durRect.offsetMax = Vector2.zero;

            SavePrefab(root, $"{PrefabsPath}/StatusIcon.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created StatusIcon.prefab");
        }

        // ---------------------------------------------------------------
        // TurnToken — turn order display token
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Turn Token")]
        public static void GenerateTurnTokenPrefab()
        {
            var root = CreateUIRoot("TurnToken", 48, 48);

            var border = AddImage(root, "Border", new Color(0.6f, 0.7f, 0.85f, 1f));
            StretchToParent(border);

            var portrait = AddImage(root, "Portrait", new Color(0.4f, 0.4f, 0.45f, 1f));
            var porRect = portrait.GetComponent<RectTransform>();
            porRect.anchorMin = new Vector2(0.08f, 0.08f);
            porRect.anchorMax = new Vector2(0.92f, 0.92f);
            porRect.offsetMin = Vector2.zero;
            porRect.offsetMax = Vector2.zero;

            var glow = AddImage(root, "ActiveGlow", new Color(1f, 0.9f, 0.3f, 0.4f));
            StretchToParent(glow);
            glow.SetActive(false);

            SavePrefab(root, $"{PrefabsPath}/TurnToken.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created TurnToken.prefab");
        }

        // ---------------------------------------------------------------
        // ResearchNodeWidget — research tree node
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Research Node Widget")]
        public static void GenerateResearchNodeWidgetPrefab()
        {
            var root = CreateUIRoot("ResearchNodeWidget", 80, 80);

            var bg = AddImage(root, "Background", new Color(0.3f, 0.3f, 0.3f, 1f));
            StretchToParent(bg);

            var icon = AddImage(root, "NodeIcon", new Color(0.5f, 0.5f, 0.6f, 1f));
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.15f, 0.25f);
            iconRect.anchorMax = new Vector2(0.85f, 0.9f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var nameLabel = AddText(root, "NodeName", "Research", 9, TextAnchor.MiddleCenter);
            var nameRect = nameLabel.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(1, 0.25f);
            nameRect.offsetMin = new Vector2(2, 0);
            nameRect.offsetMax = new Vector2(-2, 0);

            var checkmark = AddText(root, "Checkmark", "\u2713", 28, TextAnchor.MiddleCenter);
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.6f, 0.6f);
            checkRect.anchorMax = new Vector2(1, 1);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            checkmark.GetComponent<Text>().color = new Color(0.2f, 0.9f, 0.3f, 1f);
            checkmark.SetActive(false);

            var progress = AddImage(root, "ProgressIndicator", new Color(1f, 0.8f, 0.2f, 0.8f));
            var progRect = progress.GetComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0, 0);
            progRect.anchorMax = new Vector2(1, 0.05f);
            progRect.offsetMin = Vector2.zero;
            progRect.offsetMax = Vector2.zero;
            progress.SetActive(false);

            root.AddComponent<Button>();

            SavePrefab(root, $"{PrefabsPath}/ResearchNodeWidget.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[ArtAssetGenerator] Created ResearchNodeWidget.prefab");
        }

        // ---------------------------------------------------------------
        // Hero placeholder — 3D capsule with faction-colored material
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Hero Placeholders")]
        public static void GenerateHeroPlaceholderPrefab()
        {
            var heroDir = $"{PrefabsPath}/Heroes";
            EnsureDirectory(heroDir);

            var factionColors = new (string name, Color color)[]
            {
                ("IronLegion", new Color(0.6f, 0.7f, 0.85f)),
                ("AshCult", new Color(0.45f, 0.15f, 0.55f)),
                ("WildHunters", new Color(0.2f, 0.65f, 0.3f)),
                ("StoneSanctum", new Color(0.85f, 0.75f, 0.3f)),
                ("VoidReapers", new Color(0.6f, 0.1f, 0.15f)),
            };

            foreach (var (factionName, color) in factionColors)
            {
                var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hero.name = $"Hero_{factionName}";
                hero.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);

                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/Factions/Faction_{factionName}.mat");
                if (mat != null)
                    hero.GetComponent<Renderer>().sharedMaterial = mat;

                var labelGo = new GameObject("NameLabel");
                labelGo.transform.SetParent(hero.transform);
                labelGo.transform.localPosition = new Vector3(0, 1.2f, 0);

                SavePrefab(hero, $"{heroDir}/Hero_{factionName}.prefab");
                Object.DestroyImmediate(hero);
            }
            Debug.Log("[ArtAssetGenerator] Created 5 Hero placeholder prefabs (one per faction)");
        }

        // ---------------------------------------------------------------
        // Building placeholders — cubes with building materials
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Building Placeholders")]
        public static void GenerateBuildingPlaceholderPrefabs()
        {
            var buildDir = $"{PrefabsPath}/Buildings";
            EnsureDirectory(buildDir);

            var buildings = new (string name, float height)[]
            {
                ("Stronghold", 2.5f),
                ("Barracks", 1.5f),
                ("Farm", 1.0f),
                ("Mine", 1.2f),
                ("Quarry", 1.0f),
                ("ArcaneTower", 2.0f),
                ("Academy", 1.8f),
                ("GuildHall", 1.6f),
                ("Forge", 1.3f),
                ("Wall", 1.8f),
                ("Watchtower", 2.2f),
                ("Marketplace", 1.4f),
                ("Hospital", 1.5f),
                ("Embassy", 1.6f),
                ("TrainingGround", 0.8f),
            };

            foreach (var (bName, height) in buildings)
            {
                var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = $"Building_{bName}";
                building.transform.localScale = new Vector3(1f, height, 1f);
                building.transform.localPosition = new Vector3(0, height / 2f, 0);

                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/Buildings/Building_{bName}.mat");
                if (mat != null)
                    building.GetComponent<Renderer>().sharedMaterial = mat;

                if (height > 1.5f)
                {
                    var roof = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    roof.name = "Roof";
                    roof.transform.SetParent(building.transform);
                    roof.transform.localPosition = new Vector3(0, 0.6f, 0);
                    roof.transform.localScale = new Vector3(1.1f, 0.15f, 1.1f);
                    if (mat != null) roof.GetComponent<Renderer>().sharedMaterial = mat;
                }

                SavePrefab(building, $"{buildDir}/Building_{bName}.prefab");
                Object.DestroyImmediate(building);
            }
            Debug.Log($"[ArtAssetGenerator] Created {buildings.Length} Building placeholder prefabs");
        }

        // ---------------------------------------------------------------
        // Resource icon prefabs — colored spheres for Stone/Iron/Grain/Arcane
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Generate Art Assets/Resource Icons")]
        public static void GenerateResourceIconPrefabs()
        {
            var iconDir = $"{PrefabsPath}/ResourceIcons";
            EnsureDirectory(iconDir);

            var resources = new (string name, Color color)[]
            {
                ("Stone", new Color(0.6f, 0.55f, 0.5f)),
                ("Iron", new Color(0.45f, 0.45f, 0.5f)),
                ("Grain", new Color(0.85f, 0.75f, 0.2f)),
                ("ArcaneEssence", new Color(0.5f, 0.2f, 0.8f)),
            };

            foreach (var (rName, color) in resources)
            {
                var iconRoot = CreateUIRoot($"ResourceIcon_{rName}", 32, 32);
                var img = AddImage(iconRoot, "Icon", color);
                StretchToParent(img);

                var label = AddText(iconRoot, "Label", rName[0].ToString(), 16, TextAnchor.MiddleCenter);
                StretchToParent(label);
                label.GetComponent<Text>().color = Color.white;

                SavePrefab(iconRoot, $"{iconDir}/ResourceIcon_{rName}.prefab");
                Object.DestroyImmediate(iconRoot);
            }
            Debug.Log("[ArtAssetGenerator] Created 4 Resource icon prefabs");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static GameObject CreateUIRoot(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return go;
        }

        private static GameObject AddImage(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject AddText(GameObject parent, string name, string text, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent.transform, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return go;
        }

        private static void StretchToParent(GameObject child)
        {
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Material LoadOrDefaultMat(string matName)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/{matName}.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/GridTile_Neutral.mat");
        }

        private static void SavePrefab(GameObject go, string path)
        {
            var dir = Path.GetDirectoryName(path);
            EnsureDirectory(dir);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
