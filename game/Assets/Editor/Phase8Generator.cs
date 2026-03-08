#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class Phase8Generator
    {
        private const string ArtRoot = "Assets/Art";
        private const string DataRoot = "Assets/Data";
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string AudioRoot = "Assets/Audio";

        [MenuItem("AshenThrone/Phase 8/Generate All Placeholder Art")]
        public static void GenerateAll()
        {
            GenerateHeroSprites();
            GenerateCardSprites();
            GenerateBuildingSprites();
            GenerateUISprites();
            GenerateEnvironmentTextures();
            GeneratePlaceholderAudio();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase8] All placeholder art generated.");
        }

        // ---------------------------------------------------------------
        // 8.1: Hero Sprites — 10 portraits (256x256) + 10 full-body (512x1024)
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Hero Sprites")]
        public static void GenerateHeroSprites()
        {
            var dir = $"{ArtRoot}/Characters/Heroes";
            EnsureDir(dir);

            var heroes = new (string id, Color baseColor)[]
            {
                ("lyra_thornveil", new Color(0.2f, 0.65f, 0.3f)),
                ("kael_ashwalker", new Color(0.85f, 0.25f, 0.15f)),
                ("thane_ironhold", new Color(0.6f, 0.7f, 0.85f)),
                ("zara_voidweaver", new Color(0.45f, 0.15f, 0.55f)),
                ("rowan_stoneward", new Color(0.85f, 0.75f, 0.3f)),
                ("mira_frostbane", new Color(0.3f, 0.7f, 0.9f)),
                ("vex_shadowstrike", new Color(0.3f, 0.1f, 0.35f)),
                ("sera_dawnblade", new Color(0.95f, 0.85f, 0.4f)),
                ("grim_bonecrusher", new Color(0.6f, 0.1f, 0.15f)),
                ("nyx_stormcaller", new Color(0.4f, 0.5f, 0.9f)),
            };

            int wiredCount = 0;
            foreach (var (id, col) in heroes)
            {
                // Portrait
                var portrait = CreateColoredTexture(256, 256, col, $"P\n{id}");
                SaveTexture(portrait, $"{dir}/{id}_portrait.png");

                // Full body
                var fullBody = CreateColoredTexture(512, 1024, col, $"FB\n{id}");
                SaveTexture(fullBody, $"{dir}/{id}_fullbody.png");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Wire to HeroData SOs
            foreach (var (id, col) in heroes)
            {
                var portraitPath = $"{dir}/{id}_portrait.png";
                var fullBodyPath = $"{dir}/{id}_fullbody.png";
                var heroPath = $"{DataRoot}/Heroes/Hero_{id}.asset";

                SetTextureImportSettings(portraitPath, 256);
                SetTextureImportSettings(fullBodyPath, 512);

                var heroSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(heroPath);
                if (heroSO != null)
                {
                    var so = new SerializedObject(heroSO);
                    var portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
                    var fullBodySprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullBodyPath);

                    var portraitProp = so.FindProperty("<Portrait>k__BackingField");
                    if (portraitProp == null) portraitProp = so.FindProperty("portrait");
                    if (portraitProp != null && portraitSprite != null)
                    {
                        portraitProp.objectReferenceValue = portraitSprite;
                        wiredCount++;
                    }

                    var fbProp = so.FindProperty("<FullBodySprite>k__BackingField");
                    if (fbProp == null) fbProp = so.FindProperty("fullBodySprite");
                    if (fbProp != null && fullBodySprite != null)
                        fbProp.objectReferenceValue = fullBodySprite;

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase8] Created 20 hero sprites, wired {wiredCount} portraits to HeroData SOs.");
        }

        // ---------------------------------------------------------------
        // 8.2: Card Sprites — 50 card frames (200x300) colored by element
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Card Sprites")]
        public static void GenerateCardSprites()
        {
            var dir = $"{ArtRoot}/UI/Cards";
            EnsureDir(dir);

            var elementColors = new Dictionary<string, Color>
            {
                { "Fire", new Color(0.9f, 0.3f, 0.1f) },
                { "Ice", new Color(0.3f, 0.7f, 0.95f) },
                { "Lightning", new Color(0.95f, 0.9f, 0.2f) },
                { "Nature", new Color(0.2f, 0.75f, 0.3f) },
                { "Shadow", new Color(0.25f, 0.1f, 0.35f) },
                { "Holy", new Color(0.95f, 0.9f, 0.6f) },
                { "Arcane", new Color(0.6f, 0.2f, 0.8f) },
                { "Physical", new Color(0.65f, 0.65f, 0.65f) },
            };

            // Generate one frame per element
            foreach (var (element, color) in elementColors)
            {
                var tex = CreateCardFrame(200, 300, color, element);
                SaveTexture(tex, $"{dir}/CardFrame_{element}.png");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Set import settings
            foreach (var element in elementColors.Keys)
                SetTextureImportSettings($"{dir}/CardFrame_{element}.png", 256);

            // Wire to AbilityCardData SOs
            int wiredCount = 0;
            var cardGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { $"{DataRoot}/Cards" });
            foreach (var guid in cardGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cardSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (cardSO == null) continue;

                var so = new SerializedObject(cardSO);
                var elementProp = so.FindProperty("<Element>k__BackingField");
                if (elementProp == null) elementProp = so.FindProperty("element");

                string elementName = "Physical";
                if (elementProp != null)
                    elementName = elementProp.enumNames[elementProp.enumValueIndex];

                if (!elementColors.ContainsKey(elementName))
                    elementName = "Physical";

                var artProp = so.FindProperty("<CardArtwork>k__BackingField");
                if (artProp == null) artProp = so.FindProperty("cardArtwork");

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/CardFrame_{elementName}.png");
                if (artProp != null && sprite != null)
                {
                    artProp.objectReferenceValue = sprite;
                    wiredCount++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase8] Created 8 card frame sprites, wired {wiredCount}/50 AbilityCardData SOs.");
        }

        // ---------------------------------------------------------------
        // 8.3: Building Sprites — 21 buildings × 3 tiers = 63 PNGs
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Building Sprites")]
        public static void GenerateBuildingSprites()
        {
            var dir = $"{ArtRoot}/Buildings";
            EnsureDir(dir);

            var buildings = new (string id, Color baseColor)[]
            {
                ("stronghold", new Color(0.75f, 0.55f, 0.25f)),
                ("barracks", new Color(0.6f, 0.2f, 0.2f)),
                ("training_ground", new Color(0.5f, 0.3f, 0.2f)),
                ("watch_tower", new Color(0.5f, 0.5f, 0.6f)),
                ("wall", new Color(0.4f, 0.4f, 0.45f)),
                ("armory", new Color(0.55f, 0.35f, 0.25f)),
                ("stone_quarry", new Color(0.6f, 0.6f, 0.55f)),
                ("iron_mine", new Color(0.45f, 0.45f, 0.5f)),
                ("grain_farm", new Color(0.4f, 0.7f, 0.3f)),
                ("arcane_tower", new Color(0.5f, 0.2f, 0.7f)),
                ("marketplace", new Color(0.7f, 0.6f, 0.3f)),
                ("academy", new Color(0.3f, 0.4f, 0.7f)),
                ("library", new Color(0.35f, 0.3f, 0.55f)),
                ("laboratory", new Color(0.4f, 0.55f, 0.5f)),
                ("observatory", new Color(0.25f, 0.35f, 0.6f)),
                ("archive", new Color(0.45f, 0.35f, 0.45f)),
                ("hero_shrine", new Color(0.7f, 0.5f, 0.6f)),
                ("guild_hall", new Color(0.55f, 0.45f, 0.35f)),
                ("enchanting_tower", new Color(0.6f, 0.3f, 0.65f)),
                ("forge", new Color(0.7f, 0.35f, 0.15f)),
                ("embassy", new Color(0.4f, 0.5f, 0.4f)),
            };

            foreach (var (id, baseColor) in buildings)
            {
                for (int tier = 1; tier <= 3; tier++)
                {
                    float brightness = 0.7f + (tier - 1) * 0.15f;
                    var tierColor = baseColor * brightness;
                    tierColor.a = 1f;
                    var tex = CreateBuildingSprite(128, 128, tierColor, $"{id}\nT{tier}");
                    SaveTexture(tex, $"{dir}/{id}_t{tier}.png");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Set import settings
            foreach (var (id, _) in buildings)
                for (int tier = 1; tier <= 3; tier++)
                    SetTextureImportSettings($"{dir}/{id}_t{tier}.png", 128);

            // Wire to BuildingData SOs
            int wiredCount = 0;
            foreach (var (id, _) in buildings)
            {
                var soPath = $"{DataRoot}/Buildings/Building_{id}.asset";
                var buildingSO = AssetDatabase.LoadAssetAtPath<ScriptableObject>(soPath);
                if (buildingSO == null) continue;

                var so = new SerializedObject(buildingSO);
                var spritesProp = so.FindProperty("<TierSprites>k__BackingField");
                if (spritesProp == null) spritesProp = so.FindProperty("tierSprites");
                if (spritesProp == null || !spritesProp.isArray) continue;

                spritesProp.arraySize = 3;
                for (int tier = 1; tier <= 3; tier++)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{id}_t{tier}.png");
                    if (sprite != null)
                        spritesProp.GetArrayElementAtIndex(tier - 1).objectReferenceValue = sprite;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                wiredCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase8] Created 63 building sprites, wired {wiredCount}/21 BuildingData SOs.");
        }

        // ---------------------------------------------------------------
        // 8.4: UI Sprites — resource icons, currency, buttons, bars
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/UI Sprites")]
        public static void GenerateUISprites()
        {
            var dir = $"{ArtRoot}/UI/Atlas";
            EnsureDir(dir);

            var sprites = new (string name, int w, int h, Color color)[]
            {
                ("icon_stone", 64, 64, new Color(0.6f, 0.6f, 0.55f)),
                ("icon_iron", 64, 64, new Color(0.45f, 0.45f, 0.5f)),
                ("icon_grain", 64, 64, new Color(0.85f, 0.75f, 0.3f)),
                ("icon_arcane", 64, 64, new Color(0.5f, 0.2f, 0.7f)),
                ("icon_gold", 64, 64, new Color(0.95f, 0.85f, 0.2f)),
                ("icon_gems", 64, 64, new Color(0.3f, 0.8f, 0.4f)),
                ("icon_xp", 64, 64, new Color(0.2f, 0.6f, 0.9f)),
                ("icon_bp", 64, 64, new Color(0.8f, 0.5f, 0.1f)),
                ("btn_primary", 256, 64, new Color(0.2f, 0.5f, 0.85f)),
                ("btn_secondary", 256, 64, new Color(0.4f, 0.4f, 0.45f)),
                ("btn_danger", 256, 64, new Color(0.85f, 0.2f, 0.15f)),
                ("btn_success", 256, 64, new Color(0.2f, 0.7f, 0.3f)),
                ("panel_dark", 512, 512, new Color(0.08f, 0.08f, 0.12f, 0.92f)),
                ("panel_light", 512, 512, new Color(0.15f, 0.15f, 0.2f, 0.85f)),
                ("bar_health_fill", 256, 32, new Color(0.2f, 0.8f, 0.2f)),
                ("bar_health_bg", 256, 32, new Color(0.3f, 0.1f, 0.1f)),
                ("bar_energy_fill", 256, 32, new Color(0.2f, 0.5f, 0.9f)),
                ("bar_energy_bg", 256, 32, new Color(0.1f, 0.15f, 0.3f)),
                ("bar_xp_fill", 256, 32, new Color(0.85f, 0.7f, 0.15f)),
                ("nav_combat", 64, 64, new Color(0.85f, 0.25f, 0.15f)),
                ("nav_empire", 64, 64, new Color(0.75f, 0.55f, 0.25f)),
                ("nav_worldmap", 64, 64, new Color(0.2f, 0.65f, 0.3f)),
                ("nav_alliance", 64, 64, new Color(0.3f, 0.4f, 0.8f)),
                ("status_burn", 32, 32, new Color(0.9f, 0.3f, 0.1f)),
                ("status_freeze", 32, 32, new Color(0.3f, 0.7f, 0.95f)),
                ("status_poison", 32, 32, new Color(0.3f, 0.8f, 0.2f)),
                ("status_shield", 32, 32, new Color(0.6f, 0.6f, 0.7f)),
                ("status_stun", 32, 32, new Color(0.95f, 0.9f, 0.2f)),
            };

            foreach (var (name, w, h, color) in sprites)
            {
                var tex = CreateColoredTexture(w, h, color, name.Replace("icon_", "").Replace("btn_", "").Replace("nav_", ""));
                SaveTexture(tex, $"{dir}/{name}.png");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var (name, w, _, _) in sprites)
                SetTextureImportSettings($"{dir}/{name}.png", Mathf.Max(w, 64));

            Debug.Log($"[Phase8] Created {sprites.Length} UI sprite placeholders.");
        }

        // ---------------------------------------------------------------
        // 8.5: Environment Textures
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Environment Textures")]
        public static void GenerateEnvironmentTextures()
        {
            var dir = $"{ArtRoot}/Environments";
            EnsureDir(dir);

            // Combat tiles
            var tiles = new (string name, Color color)[]
            {
                ("tile_stone", new Color(0.35f, 0.33f, 0.3f)),
                ("tile_grass", new Color(0.2f, 0.45f, 0.15f)),
                ("tile_sand", new Color(0.75f, 0.65f, 0.4f)),
                ("tile_lava", new Color(0.6f, 0.15f, 0.05f)),
                ("tile_ice", new Color(0.6f, 0.8f, 0.9f)),
                ("tile_void", new Color(0.15f, 0.05f, 0.2f)),
                ("tile_holy", new Color(0.9f, 0.85f, 0.6f)),
            };

            foreach (var (name, color) in tiles)
            {
                var tex = CreateTiledTexture(256, 256, color);
                SaveTexture(tex, $"{dir}/{name}.png");
            }

            // World map textures
            var maps = new (string name, Color color)[]
            {
                ("worldmap_plains", new Color(0.35f, 0.5f, 0.25f)),
                ("worldmap_mountains", new Color(0.45f, 0.4f, 0.35f)),
                ("worldmap_desert", new Color(0.75f, 0.65f, 0.35f)),
                ("worldmap_forest", new Color(0.15f, 0.35f, 0.12f)),
                ("worldmap_ocean", new Color(0.1f, 0.2f, 0.5f)),
            };

            foreach (var (name, color) in maps)
            {
                var tex = CreateColoredTexture(512, 512, color, name.Replace("worldmap_", ""));
                SaveTexture(tex, $"{dir}/{name}.png");
            }

            // Empire background
            var empireBg = CreateColoredTexture(1024, 1024, new Color(0.12f, 0.1f, 0.15f), "EMPIRE");
            SaveTexture(empireBg, $"{dir}/empire_background.png");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase8] Created 13 environment textures.");
        }

        // ---------------------------------------------------------------
        // 8.7: Placeholder Audio
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Placeholder Audio")]
        public static void GeneratePlaceholderAudio()
        {
            var musicDir = $"{AudioRoot}/Music";
            var sfxDir = $"{AudioRoot}/SFX";
            EnsureDir(musicDir);
            EnsureDir(sfxDir);

            // 3 music loops (1 second each for placeholder — saves space)
            GenerateWav($"{musicDir}/music_menu.wav", 440f, 1.0f, 0.3f);
            GenerateWav($"{musicDir}/music_combat.wav", 523f, 1.0f, 0.3f);
            GenerateWav($"{musicDir}/music_empire.wav", 392f, 1.0f, 0.3f);

            // 15 SFX clips
            var sfx = new (string name, float freq, float duration)[]
            {
                ("sfx_btn_click", 800f, 0.1f),
                ("sfx_btn_back", 400f, 0.15f),
                ("sfx_card_play", 600f, 0.2f),
                ("sfx_card_draw", 500f, 0.15f),
                ("sfx_hit_physical", 200f, 0.15f),
                ("sfx_hit_fire", 350f, 0.2f),
                ("sfx_hit_ice", 700f, 0.2f),
                ("sfx_hit_lightning", 900f, 0.1f),
                ("sfx_hit_critical", 150f, 0.25f),
                ("sfx_hero_death", 100f, 0.5f),
                ("sfx_building_complete", 660f, 0.3f),
                ("sfx_level_up", 880f, 0.4f),
                ("sfx_collect_resource", 550f, 0.15f),
                ("sfx_quest_complete", 740f, 0.3f),
                ("sfx_gacha_reveal", 440f, 0.5f),
            };

            foreach (var (name, freq, dur) in sfx)
                GenerateWav($"{sfxDir}/{name}.wav", freq, dur, 0.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase8] Created 3 music + 15 SFX placeholder audio clips.");
        }

        // ---------------------------------------------------------------
        // Texture Helpers
        // ---------------------------------------------------------------

        private static Texture2D CreateColoredTexture(int w, int h, Color color, string label = "")
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            // Add a subtle border
            var borderColor = color * 0.6f;
            borderColor.a = 1f;
            int borderWidth = Mathf.Max(1, Mathf.Min(w, h) / 32);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth)
                        pixels[y * w + x] = borderColor;

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateCardFrame(int w, int h, Color color, string element)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            // Card body (slightly darker)
            var bodyColor = color * 0.7f;
            bodyColor.a = 1f;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bodyColor;

            // Bright border
            int border = 4;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (x < border || x >= w - border || y < border || y >= h - border)
                        pixels[y * w + x] = color;

            // Top section (element color, brighter)
            for (int x = border; x < w - border; x++)
                for (int y = h - h / 3; y < h - border; y++)
                    pixels[y * w + x] = color;

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBuildingSprite(int w, int h, Color color, string label)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            // Building body
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Draw building shape (rectangle with roof)
            int margin = w / 8;
            int roofHeight = h / 4;
            var wallColor = color;
            var roofColor = color * 0.7f;
            roofColor.a = 1f;

            // Walls
            for (int x = margin; x < w - margin; x++)
                for (int y = 0; y < h - roofHeight; y++)
                    pixels[y * w + x] = wallColor;

            // Roof (triangle)
            for (int y = h - roofHeight; y < h; y++)
            {
                float progress = (float)(y - (h - roofHeight)) / roofHeight;
                int xStart = margin + (int)(progress * (w / 2 - margin));
                int xEnd = w - margin - (int)(progress * (w / 2 - margin));
                for (int x = xStart; x < xEnd; x++)
                    pixels[y * w + x] = roofColor;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateTiledTexture(int w, int h, Color baseColor)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            var lineColor = baseColor * 0.8f;
            lineColor.a = 1f;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    bool isGridLine = (x % 64 < 2) || (y % 64 < 2);
                    pixels[y * w + x] = isGridLine ? lineColor : baseColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void SaveTexture(Texture2D tex, string path)
        {
            var fullPath = Path.Combine(Application.dataPath, "..", path);
            var dirPath = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void SetTextureImportSettings(string path, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------
        // Audio Helper — generates simple WAV sine wave
        // ---------------------------------------------------------------
        private static void GenerateWav(string path, float frequency, float duration, float amplitude)
        {
            var fullPath = Path.Combine(Application.dataPath, "..", path);
            var dirPath = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            int sampleRate = 44100;
            int numSamples = (int)(sampleRate * duration);
            short[] samples = new short[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float fadeOut = 1f - (t / duration);
                float value = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * fadeOut;
                samples[i] = (short)(value * short.MaxValue);
            }

            using (var stream = new FileStream(fullPath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int byteRate = sampleRate * 2; // 16-bit mono
                int dataSize = numSamples * 2;

                // WAV header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // chunk size
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)2); // block align
                writer.Write((short)16); // bits per sample
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                foreach (var s in samples)
                    writer.Write(s);
            }
        }

        // ---------------------------------------------------------------
        // 8.6: Missing UI Prefabs
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/UI Prefabs")]
        public static void GenerateUIPrefabs()
        {
            EnsureDir(PrefabsRoot + "/UI");
            EnsureDir(PrefabsRoot + "/Particles");
            int count = 0;

            // --- Panel Prefabs ---
            count += CreatePanelPrefab("VictoryPanel", new Color(0.1f, 0.3f, 0.1f, 0.95f), "VICTORY!", 400, 300);
            count += CreatePanelPrefab("DefeatPanel", new Color(0.3f, 0.08f, 0.08f, 0.95f), "DEFEAT", 400, 300);
            count += CreatePanelPrefab("TutorialOverlay", new Color(0f, 0f, 0f, 0.7f), "", 1920, 1080);
            count += CreatePanelPrefab("EventBanner", new Color(0.15f, 0.1f, 0.25f, 0.95f), "EVENT", 600, 100);

            // --- Row/Slot Prefabs ---
            count += CreateRowPrefab("QuestRow", 500, 60, new Color(0.12f, 0.12f, 0.18f));
            count += CreateRowPrefab("LeaderboardRow", 500, 50, new Color(0.1f, 0.1f, 0.15f));
            count += CreateRowPrefab("BattlePassTierRow", 500, 80, new Color(0.12f, 0.1f, 0.16f));
            count += CreateRowPrefab("ChatBubble", 400, 60, new Color(0.15f, 0.15f, 0.2f));
            count += CreateRowPrefab("BuildingSlot", 120, 120, new Color(0.2f, 0.18f, 0.12f));
            count += CreateRowPrefab("WorldMapTile", 128, 128, new Color(0.25f, 0.35f, 0.2f));

            // --- Card Prefabs ---
            count += CreateCardPrefab("StoreProductCard", 180, 240, new Color(0.1f, 0.12f, 0.18f));
            count += CreateCardPrefab("HeroCard", 160, 220, new Color(0.15f, 0.1f, 0.2f));

            // --- Widget Prefabs ---
            count += CreateWidgetPrefab("EnergyBar", 200, 24, new Color(0.1f, 0.15f, 0.3f), new Color(0.2f, 0.5f, 0.9f));
            count += CreateWidgetPrefab("HealthBar", 200, 24, new Color(0.3f, 0.1f, 0.1f), new Color(0.2f, 0.8f, 0.2f));
            count += CreateWidgetPrefab("XPBar", 200, 20, new Color(0.15f, 0.12f, 0.05f), new Color(0.85f, 0.7f, 0.15f));

            // --- Settings Widget Prefabs ---
            count += CreateSettingsTogglePrefab();
            count += CreateSettingsSliderPrefab();

            // --- Hero Status Display Prefab ---
            count += CreateHeroStatusDisplayPrefab();

            // --- Particle Effect Prefabs ---
            count += CreateParticlePrefab("VFX_Construction", new Color(0.8f, 0.6f, 0.2f), 0.5f, 20);
            count += CreateParticlePrefab("VFX_LevelUp", new Color(0.9f, 0.85f, 0.3f), 1.0f, 40);
            count += CreateParticlePrefab("VFX_CardPlay", new Color(0.4f, 0.6f, 0.9f), 0.3f, 15);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Phase8] Created {count} UI prefabs and particle effects.");
        }

        private static int CreatePanelPrefab(string name, Color bgColor, string title, int w, int h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = bgColor;

            if (!string.IsNullOrEmpty(title))
            {
                var titleGo = new GameObject("Title", typeof(RectTransform));
                titleGo.transform.SetParent(go.transform, false);
                var titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0, 0.7f);
                titleRt.anchorMax = Vector2.one;
                titleRt.offsetMin = Vector2.zero;
                titleRt.offsetMax = Vector2.zero;
            }

            // Content container
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
            contentGo.transform.SetParent(go.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = new Vector2(1f, 0.7f);
            contentRt.offsetMin = new Vector2(16, 16);
            contentRt.offsetMax = new Vector2(-16, -8);

            var path = $"{PrefabsRoot}/UI/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateRowPrefab(string name, int w, int h, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.HorizontalLayoutGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<UnityEngine.UI.Image>().color = bgColor;

            var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Icon placeholder
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(h - 8, h - 8);
            iconGo.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0.3f);

            // Label placeholder
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);

            var path = $"{PrefabsRoot}/UI/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateCardPrefab(string name, int w, int h, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<UnityEngine.UI.Image>().color = bgColor;

            // Art area
            var artGo = new GameObject("Art", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            artGo.transform.SetParent(go.transform, false);
            var artRt = artGo.GetComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.05f, 0.35f);
            artRt.anchorMax = new Vector2(0.95f, 0.95f);
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            artGo.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0.2f);

            // Info area
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(go.transform, false);
            var infoRt = infoGo.GetComponent<RectTransform>();
            infoRt.anchorMin = Vector2.zero;
            infoRt.anchorMax = new Vector2(1f, 0.35f);
            infoRt.offsetMin = new Vector2(8, 4);
            infoRt.offsetMax = new Vector2(-8, -4);

            var path = $"{PrefabsRoot}/UI/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateWidgetPrefab(string name, int w, int h, Color bgColor, Color fillColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<UnityEngine.UI.Image>().color = bgColor;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.75f, 1f); // 75% fill for visual
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillGo.GetComponent<UnityEngine.UI.Image>().color = fillColor;

            var path = $"{PrefabsRoot}/UI/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateSettingsTogglePrefab()
        {
            var go = new GameObject("SettingsToggle", typeof(RectTransform),
                typeof(UnityEngine.UI.HorizontalLayoutGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 40);

            var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);

            // Toggle
            var toggleGo = new GameObject("Toggle", typeof(RectTransform),
                typeof(UnityEngine.UI.Toggle), typeof(UnityEngine.UI.Image));
            toggleGo.transform.SetParent(go.transform, false);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.sizeDelta = new Vector2(40, 40);
            toggleGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.25f);

            // Checkmark
            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            checkGo.transform.SetParent(toggleGo.transform, false);
            var checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0.15f, 0.15f);
            checkRt.anchorMax = new Vector2(0.85f, 0.85f);
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            checkGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.8f, 0.3f);

            var toggle = toggleGo.GetComponent<UnityEngine.UI.Toggle>();
            toggle.graphic = checkGo.GetComponent<UnityEngine.UI.Image>();

            var path = $"{PrefabsRoot}/UI/SettingsToggle.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateSettingsSliderPrefab()
        {
            var go = new GameObject("SettingsSlider", typeof(RectTransform),
                typeof(UnityEngine.UI.HorizontalLayoutGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 40);
            var layout = go.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);

            // Slider container
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(go.transform, false);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(180, 20);

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bgGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.25f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.6f, 0.9f);

            // Handle
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = Vector2.zero;
            handleAreaRt.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRtComp = handleGo.GetComponent<RectTransform>();
            handleRtComp.sizeDelta = new Vector2(20, 20);
            handleGo.GetComponent<UnityEngine.UI.Image>().color = Color.white;

            var slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRtComp;

            var path = $"{PrefabsRoot}/UI/SettingsSlider.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateHeroStatusDisplayPrefab()
        {
            var go = new GameObject("HeroStatusDisplay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 160);
            go.GetComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // Portrait
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            portraitGo.transform.SetParent(go.transform, false);
            var pRt = portraitGo.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.1f, 0.4f);
            pRt.anchorMax = new Vector2(0.9f, 0.95f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;
            portraitGo.GetComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.3f, 0.35f);

            // HP Bar
            var hpBg = new GameObject("HPBarBg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            hpBg.transform.SetParent(go.transform, false);
            var hpBgRt = hpBg.GetComponent<RectTransform>();
            hpBgRt.anchorMin = new Vector2(0.05f, 0.3f);
            hpBgRt.anchorMax = new Vector2(0.95f, 0.38f);
            hpBgRt.offsetMin = Vector2.zero;
            hpBgRt.offsetMax = Vector2.zero;
            hpBg.GetComponent<UnityEngine.UI.Image>().color = new Color(0.3f, 0.1f, 0.1f);

            var hpFill = new GameObject("HPFill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            hpFill.transform.SetParent(hpBg.transform, false);
            var hpFillRt = hpFill.GetComponent<RectTransform>();
            hpFillRt.anchorMin = Vector2.zero;
            hpFillRt.anchorMax = Vector2.one;
            hpFillRt.offsetMin = Vector2.zero;
            hpFillRt.offsetMax = Vector2.zero;
            hpFill.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.8f, 0.2f);

            // Status icon container
            var statusContainer = new GameObject("StatusIcons", typeof(RectTransform),
                typeof(UnityEngine.UI.HorizontalLayoutGroup));
            statusContainer.transform.SetParent(go.transform, false);
            var scRt = statusContainer.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(0.05f, 0.05f);
            scRt.anchorMax = new Vector2(0.95f, 0.28f);
            scRt.offsetMin = Vector2.zero;
            scRt.offsetMax = Vector2.zero;
            var scLayout = statusContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            scLayout.spacing = 2;

            // Active turn indicator (disabled by default)
            var turnIndicator = new GameObject("ActiveTurnIndicator", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            turnIndicator.transform.SetParent(go.transform, false);
            var tiRt = turnIndicator.GetComponent<RectTransform>();
            tiRt.anchorMin = Vector2.zero;
            tiRt.anchorMax = Vector2.one;
            tiRt.offsetMin = new Vector2(-4, -4);
            tiRt.offsetMax = new Vector2(4, 4);
            turnIndicator.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.85f, 0.2f, 0.6f);
            turnIndicator.SetActive(false);

            // Dead overlay (disabled by default)
            var deadOverlay = new GameObject("DeadOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            deadOverlay.transform.SetParent(go.transform, false);
            var doRt = deadOverlay.GetComponent<RectTransform>();
            doRt.anchorMin = Vector2.zero;
            doRt.anchorMax = Vector2.one;
            doRt.offsetMin = Vector2.zero;
            doRt.offsetMax = Vector2.zero;
            deadOverlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.6f);
            deadOverlay.SetActive(false);

            var path = $"{PrefabsRoot}/UI/HeroStatusDisplay.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateParticlePrefab(string name, Color color, float lifetime, int maxParticles)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = color;
            main.startLifetime = lifetime;
            main.maxParticles = maxParticles;
            main.startSize = 0.1f;
            main.startSpeed = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.duration = lifetime;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)maxParticles) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // Use default particle material
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = color;

            var path = $"{PrefabsRoot}/Particles/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        // ---------------------------------------------------------------
        // 8.8: Colorblind Filter Shader + Materials
        // ---------------------------------------------------------------
        [MenuItem("AshenThrone/Phase 8/Colorblind Shader")]
        public static void GenerateColorblindShader()
        {
            var shaderDir = "Assets/Shaders";
            var matDir = "Assets/Materials/Accessibility";
            EnsureDir(shaderDir);
            EnsureDir(matDir);

            // Write the shader file
            var shaderPath = $"{shaderDir}/ColorblindFilter.shader";
            var fullShaderPath = Path.Combine(Application.dataPath, "..", shaderPath);
            File.WriteAllText(fullShaderPath, GetColorblindShaderSource());

            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(shaderPath);

            var shader = Shader.Find("AshenThrone/ColorblindFilter");
            if (shader == null)
            {
                Debug.LogWarning("[Phase8] ColorblindFilter shader not found after import. Materials will use fallback.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            // Create 3 material instances for each mode
            var modes = new (string name, int modeIndex)[]
            {
                ("Protanopia", 0),
                ("Deuteranopia", 1),
                ("Tritanopia", 2),
            };

            foreach (var (name, modeIndex) in modes)
            {
                var mat = new Material(shader);
                mat.name = $"Colorblind_{name}";
                mat.SetInt("_Mode", modeIndex);
                mat.SetFloat("_Intensity", 1.0f);

                var matPath = $"{matDir}/Colorblind_{name}.mat";
                AssetDatabase.CreateAsset(mat, matPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase8] Created ColorblindFilter shader + 3 Daltonization materials.");
        }

        private static string GetColorblindShaderSource()
        {
            return @"Shader ""AshenThrone/ColorblindFilter""
{
    Properties
    {
        _MainTex (""Base Texture"", 2D) = ""white"" {}
        _Mode (""Colorblind Mode (0=Protan, 1=Deutan, 2=Tritan)"", Int) = 0
        _Intensity (""Filter Intensity"", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
        LOD 100

        Pass
        {
            Name ""ColorblindFilter""
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            int _Mode;
            float _Intensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            // Daltonization correction matrices
            // Simulate then correct (shift lost info into visible channels)
            half3 ApplyProtanopia(half3 c)
            {
                // Simulate protanopia
                half3 sim;
                sim.r = 0.567 * c.r + 0.433 * c.g;
                sim.g = 0.558 * c.r + 0.442 * c.g;
                sim.b = 0.242 * c.g + 0.758 * c.b;
                // Error and correction
                half3 err = c - sim;
                half3 correction;
                correction.r = 0;
                correction.g = err.r * 0.7 + err.g;
                correction.b = err.r * 0.7 + err.b;
                return c + correction;
            }

            half3 ApplyDeuteranopia(half3 c)
            {
                half3 sim;
                sim.r = 0.625 * c.r + 0.375 * c.g;
                sim.g = 0.7 * c.r + 0.3 * c.g;
                sim.b = 0.3 * c.g + 0.7 * c.b;
                half3 err = c - sim;
                half3 correction;
                correction.r = err.g * 0.7 + err.r;
                correction.g = 0;
                correction.b = err.g * 0.7 + err.b;
                return c + correction;
            }

            half3 ApplyTritanopia(half3 c)
            {
                half3 sim;
                sim.r = 0.95 * c.r + 0.05 * c.g;
                sim.g = 0.433 * c.g + 0.567 * c.b;
                sim.b = 0.475 * c.g + 0.525 * c.b;
                half3 err = c - sim;
                half3 correction;
                correction.r = err.b * 0.7 + err.r;
                correction.g = err.b * 0.7 + err.g;
                correction.b = 0;
                return c + correction;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 corrected = col.rgb;

                if (_Mode == 0) corrected = ApplyProtanopia(col.rgb);
                else if (_Mode == 1) corrected = ApplyDeuteranopia(col.rgb);
                else corrected = ApplyTritanopia(col.rgb);

                col.rgb = lerp(col.rgb, saturate(corrected), _Intensity);
                return col;
            }
            ENDHLSL
        }
    }
}
";
        }

        private static void EnsureDir(string assetPath)
        {
            var fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }
    }
}
#endif
