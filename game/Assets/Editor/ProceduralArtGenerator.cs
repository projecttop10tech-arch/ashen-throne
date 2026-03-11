#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates high-quality procedural dark fantasy art assets using Texture2D.
    /// Creates: splash screens, backgrounds, panels, icons, card frames, bars, nav icons.
    /// Menu: AshenThrone → Generate Procedural Art
    /// </summary>
    public static class ProceduralArtGenerator
    {
        // === Color Palette ===
        static readonly Color DeepBlack   = new Color(0.02f, 0.01f, 0.04f);
        static readonly Color DarkPurple  = new Color(0.08f, 0.04f, 0.14f);
        static readonly Color MidPurple   = new Color(0.15f, 0.08f, 0.25f);
        static readonly Color Gold        = new Color(0.85f, 0.68f, 0.25f);
        static readonly Color GoldDim     = new Color(0.55f, 0.42f, 0.15f);
        static readonly Color GoldBright  = new Color(1f, 0.85f, 0.35f);
        static readonly Color Ember       = new Color(0.92f, 0.42f, 0.12f);
        static readonly Color Blood       = new Color(0.78f, 0.12f, 0.18f);
        static readonly Color Teal        = new Color(0.15f, 0.75f, 0.62f);
        static readonly Color Sky         = new Color(0.28f, 0.52f, 0.88f);
        static readonly Color IceBlue     = new Color(0.55f, 0.78f, 0.95f);
        static readonly Color Shadow      = new Color(0.25f, 0.08f, 0.35f);
        static readonly Color DarkGreen   = new Color(0.05f, 0.12f, 0.06f);
        static readonly Color ForestGreen = new Color(0.12f, 0.28f, 0.10f);

        [MenuItem("AshenThrone/Generate Procedural Art")]
        public static void GenerateAll()
        {
            EnsureFolders();
            GenerateSplashScreen();
            GenerateBackgrounds();
            GeneratePanels();
            GenerateButtons();
            GenerateCardFrames();
            GenerateResourceIcons();
            GenerateNavIcons();
            GenerateStatusBars();
            GenerateHeroFrames();
            GenerateMiscIcons();
            AssetDatabase.Refresh();
            Debug.Log("[ProceduralArt] All procedural art generated successfully.");
        }

        static void EnsureFolders()
        {
            string[] dirs = {
                "Assets/Art/Procedural",
                "Assets/Art/Procedural/Backgrounds",
                "Assets/Art/Procedural/Panels",
                "Assets/Art/Procedural/Buttons",
                "Assets/Art/Procedural/Cards",
                "Assets/Art/Procedural/Icons",
                "Assets/Art/Procedural/Icons/Nav",
                "Assets/Art/Procedural/Icons/Resources",
                "Assets/Art/Procedural/Icons/Misc",
                "Assets/Art/Procedural/Bars",
                "Assets/Art/Procedural/Heroes",
            };
            foreach (var d in dirs)
            {
                string full = Path.Combine(Application.dataPath, "..", d);
                Directory.CreateDirectory(full);
            }
        }

        // ============================================================
        // SPLASH SCREEN — Dramatic dark fantasy with glowing tower
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Splash Screen")]
        static void GenerateSplashScreen()
        {
            int w = 1080, h = 1920;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;

                    // Base: deep gradient from black at bottom to dark purple at top
                    Color bg = Color.Lerp(DeepBlack, DarkPurple, ny * 0.6f);

                    // Add atmospheric fog bands
                    float fog = Mathf.Sin(ny * 8f + nx * 2f) * 0.03f;
                    bg += new Color(fog, fog * 0.5f, fog * 1.2f, 0);

                    // Central light pillar (tower glow)
                    float cx = Mathf.Abs(nx - 0.5f);
                    float pillarWidth = 0.08f + ny * 0.04f;
                    float pillar = Mathf.Max(0, 1f - cx / pillarWidth);
                    pillar = pillar * pillar * pillar;
                    float pillarHeight = Mathf.Clamp01((ny - 0.25f) / 0.55f);
                    pillar *= pillarHeight;

                    // Pillar color: ice blue core, gold edges
                    Color pillarColor = Color.Lerp(IceBlue, GoldBright, cx / 0.15f);
                    bg = Color.Lerp(bg, pillarColor, pillar * 0.7f);

                    // Radial glow from center-top
                    float dist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.72f));
                    float glow = Mathf.Max(0, 1f - dist / 0.5f);
                    glow = glow * glow;
                    bg += new Color(glow * 0.08f, glow * 0.12f, glow * 0.25f, 0);

                    // Mountain silhouettes
                    float mountainY = 0.32f + Mathf.Sin(nx * 12f) * 0.04f + Mathf.Sin(nx * 5.3f) * 0.06f + Mathf.Sin(nx * 23f) * 0.015f;
                    if (ny < mountainY)
                    {
                        float depth = Mathf.Clamp01((mountainY - ny) / 0.15f);
                        bg = Color.Lerp(bg, DeepBlack, depth * 0.8f);
                    }

                    // Second mountain range (foreground, darker)
                    float mt2 = 0.22f + Mathf.Sin(nx * 8f + 1.5f) * 0.05f + Mathf.Sin(nx * 18f) * 0.02f;
                    if (ny < mt2)
                    {
                        float depth = Mathf.Clamp01((mt2 - ny) / 0.12f);
                        bg = Color.Lerp(bg, new Color(0.01f, 0.005f, 0.02f), depth * 0.9f);
                    }

                    // Stars in upper portion
                    if (ny > 0.55f)
                    {
                        float starHash = Frac(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f);
                        if (starHash > 0.998f)
                        {
                            float twinkle = 0.5f + 0.5f * Mathf.Sin(starHash * 100f);
                            bg += new Color(twinkle * 0.4f, twinkle * 0.4f, twinkle * 0.5f, 0);
                        }
                    }

                    // Particle/ember effects rising from bottom
                    float emberHash = Frac(Mathf.Sin(x * 73.7f + y * 199.3f) * 21573.1f);
                    if (emberHash > 0.9985f && ny < 0.5f && ny > 0.05f)
                    {
                        float size = 0.3f + emberHash * 0.7f;
                        bg += new Color(size * 0.6f, size * 0.25f, size * 0.05f, 0);
                    }

                    // Vignette (darken edges)
                    float vignette = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    vignette = Mathf.Clamp01(vignette - 0.3f) / 0.7f;
                    bg = Color.Lerp(bg, DeepBlack, vignette * 0.6f);

                    // Bottom area darker for text readability
                    if (ny < 0.18f)
                    {
                        float darkFade = 1f - ny / 0.18f;
                        bg = Color.Lerp(bg, DeepBlack, darkFade * 0.7f);
                    }

                    bg.a = 1f;
                    tex.SetPixel(x, y, bg);
                }
            }

            tex.Apply();
            SaveTexture(tex, "Art/Procedural/Backgrounds/splash_dark_fantasy.png");
            Object.DestroyImmediate(tex);
            Debug.Log("[ProceduralArt] Splash screen generated.");
        }

        // ============================================================
        // BACKGROUNDS — Scene-specific atmospheric backgrounds
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Backgrounds")]
        static void GenerateBackgrounds()
        {
            GenerateGradientBg("lobby_bg", DarkPurple, DeepBlack, 0.5f, 0.6f, Gold, 0.08f);
            GenerateGradientBg("combat_bg", new Color(0.06f, 0.03f, 0.10f), DeepBlack, 0.5f, 0.5f, Blood, 0.05f);
            GenerateGradientBg("empire_bg_proc", DarkGreen, DeepBlack, 0.5f, 0.65f, Ember, 0.06f);
            GenerateGradientBg("worldmap_bg", new Color(0.04f, 0.06f, 0.03f), DeepBlack, 0.5f, 0.55f, ForestGreen, 0.04f);
            GenerateGradientBg("alliance_bg", new Color(0.04f, 0.04f, 0.10f), DeepBlack, 0.5f, 0.6f, Teal, 0.05f);
            Debug.Log("[ProceduralArt] 5 backgrounds generated.");
        }

        static void GenerateGradientBg(string name, Color top, Color bottom, float glowX, float glowY, Color glowColor, float glowIntensity)
        {
            int w = 1080, h = 1920;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    Color c = Color.Lerp(bottom, top, ny);

                    // Radial glow
                    float dist = Vector2.Distance(new Vector2(nx, ny), new Vector2(glowX, glowY));
                    float glow = Mathf.Max(0, 1f - dist / 0.6f);
                    glow = glow * glow;
                    c += glowColor * glow * glowIntensity;

                    // Subtle noise texture
                    float noise = Frac(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) * 0.02f;
                    c += new Color(noise, noise, noise, 0);

                    // Vignette
                    float vig = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    vig = Mathf.Clamp01(vig - 0.35f) / 0.65f;
                    c = Color.Lerp(c, DeepBlack, vig * 0.5f);

                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Backgrounds/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // PANELS — Ornate dark fantasy panels with gold borders
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Panels")]
        static void GeneratePanels()
        {
            GenerateOrnatePanel("panel_main", 512, 512, DarkPurple, Gold, 6, 3);
            GenerateOrnatePanel("panel_header", 1024, 128, DarkPurple, GoldDim, 4, 2);
            GenerateOrnatePanel("panel_card", 256, 384, new Color(0.10f, 0.06f, 0.16f), Gold, 5, 3);
            GenerateOrnatePanel("panel_tooltip", 400, 300, new Color(0.06f, 0.04f, 0.10f), GoldDim, 3, 2);
            GenerateOrnatePanel("panel_inset", 512, 512, new Color(0.04f, 0.02f, 0.06f), new Color(0.30f, 0.25f, 0.15f), 4, 1);
            GenerateOrnatePanel("panel_blood", 512, 512, new Color(0.12f, 0.03f, 0.05f), Blood, 5, 2);
            GenerateOrnatePanel("panel_teal", 512, 512, new Color(0.03f, 0.08f, 0.07f), Teal, 5, 2);
            Debug.Log("[ProceduralArt] 7 panels generated.");
        }

        static void GenerateOrnatePanel(string name, int w, int h, Color bg, Color border, int borderWidth, int cornerSize)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;

                    // Distance from edges
                    int dLeft = x, dRight = w - 1 - x, dTop = h - 1 - y, dBot = y;
                    int dEdge = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBot));

                    Color c;
                    if (dEdge < borderWidth)
                    {
                        // Border region — gold with brightness variation
                        float edgeFactor = dEdge / (float)borderWidth;
                        float brightness = 0.7f + 0.3f * edgeFactor;

                        // Corner ornaments — brighter at corners
                        float cornerDist = Mathf.Min(
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), new Vector2(0, 0)),
                                      Vector2.Distance(new Vector2(nx, ny), new Vector2(1, 0))),
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), new Vector2(0, 1)),
                                      Vector2.Distance(new Vector2(nx, ny), new Vector2(1, 1))));
                        float cornerGlow = Mathf.Max(0, 1f - cornerDist / 0.15f);
                        brightness += cornerGlow * 0.4f;

                        c = border * brightness;
                    }
                    else if (dEdge < borderWidth + 2)
                    {
                        // Inner shadow line
                        c = bg * 0.5f;
                    }
                    else
                    {
                        // Interior — subtle gradient with inner glow
                        c = bg;

                        // Top-to-bottom subtle gradient (lighter at top)
                        c = Color.Lerp(c, c * 1.3f, ny * 0.3f);

                        // Inner glow from center
                        float centerDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.55f));
                        float innerGlow = Mathf.Max(0, 1f - centerDist / 0.5f);
                        innerGlow = innerGlow * innerGlow;
                        c += new Color(innerGlow * 0.03f, innerGlow * 0.02f, innerGlow * 0.04f, 0);

                        // Subtle noise
                        float noise = Frac(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f);
                        c += new Color(noise * 0.01f, noise * 0.008f, noise * 0.015f, 0);
                    }

                    c.a = 0.95f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Panels/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // BUTTONS — Styled dark fantasy buttons with glow
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Buttons")]
        static void GenerateButtons()
        {
            GenerateStyledButton("btn_primary", 384, 96, Blood, Gold);
            GenerateStyledButton("btn_primary_pressed", 384, 96, Blood * 0.7f, GoldDim);
            GenerateStyledButton("btn_secondary", 384, 96, DarkPurple, GoldDim);
            GenerateStyledButton("btn_secondary_pressed", 384, 96, DarkPurple * 0.7f, new Color(0.35f, 0.28f, 0.12f));
            GenerateStyledButton("btn_gold", 384, 96, new Color(0.30f, 0.22f, 0.08f), Gold);
            GenerateStyledButton("btn_gold_pressed", 384, 96, new Color(0.20f, 0.15f, 0.05f), GoldDim);
            GenerateStyledButton("btn_ember", 384, 96, new Color(0.28f, 0.12f, 0.04f), Ember);
            GenerateStyledButton("btn_teal", 384, 96, new Color(0.04f, 0.18f, 0.15f), Teal);
            GenerateStyledButton("btn_nav", 192, 80, new Color(0.06f, 0.04f, 0.10f), GoldDim);
            GenerateStyledButton("btn_nav_active", 192, 80, MidPurple, Gold);
            Debug.Log("[ProceduralArt] 10 buttons generated.");
        }

        static void GenerateStyledButton(string name, int w, int h, Color bg, Color border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            int bw = 3;
            int radius = 8;

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;

                    // Rounded rectangle mask
                    float mask = RoundedRectMask(x, y, w, h, radius);
                    if (mask < 0.5f) { tex.SetPixel(x, y, Color.clear); continue; }

                    int dLeft = x, dRight = w - 1 - x, dTop = h - 1 - y, dBot = y;
                    int dEdge = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBot));

                    Color c;
                    if (dEdge < bw)
                    {
                        float edgeBrightness = 0.8f + 0.2f * (dEdge / (float)bw);
                        c = border * edgeBrightness;
                    }
                    else
                    {
                        c = bg;
                        // Vertical gradient — lighter at top for 3D bevel effect
                        float bevel = Mathf.Lerp(1.3f, 0.8f, ny);
                        c *= bevel;

                        // Horizontal highlight streak at top
                        if (ny > 0.65f && ny < 0.85f)
                        {
                            float highlight = Mathf.Clamp01(1f - Mathf.Abs(nx - 0.5f) / 0.35f);
                            c += new Color(0.05f, 0.04f, 0.06f, 0) * highlight * (ny - 0.65f) * 5f;
                        }
                    }

                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Buttons/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // CARD FRAMES — Elemental card borders with glow effects
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Card Frames")]
        static void GenerateCardFrames()
        {
            GenerateCardFrame("card_fire", Ember, new Color(0.95f, 0.55f, 0.10f));
            GenerateCardFrame("card_ice", IceBlue, new Color(0.40f, 0.70f, 0.95f));
            GenerateCardFrame("card_shadow", Shadow, new Color(0.50f, 0.20f, 0.65f));
            GenerateCardFrame("card_nature", ForestGreen, new Color(0.25f, 0.65f, 0.20f));
            GenerateCardFrame("card_holy", Gold, GoldBright);
            GenerateCardFrame("card_lightning", new Color(0.65f, 0.55f, 0.95f), new Color(0.80f, 0.70f, 1.0f));
            GenerateCardFrame("card_arcane", new Color(0.30f, 0.10f, 0.55f), new Color(0.55f, 0.25f, 0.85f));
            GenerateCardFrame("card_physical", new Color(0.40f, 0.35f, 0.30f), new Color(0.60f, 0.55f, 0.45f));
            Debug.Log("[ProceduralArt] 8 card frames generated.");
        }

        static void GenerateCardFrame(string name, Color borderColor, Color glowColor)
        {
            int w = 256, h = 384;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            int bw = 8;

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;

                    int dLeft = x, dRight = w - 1 - x, dTop = h - 1 - y, dBot = y;
                    int dEdge = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBot));

                    Color c;
                    if (dEdge < bw)
                    {
                        float edgeFactor = dEdge / (float)bw;
                        c = borderColor * (0.6f + 0.4f * edgeFactor);

                        // Corner ornament glow
                        float cornerDist = Mathf.Min(
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), new Vector2(0, 0)),
                                      Vector2.Distance(new Vector2(nx, ny), new Vector2(1, 0))),
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), new Vector2(0, 1)),
                                      Vector2.Distance(new Vector2(nx, ny), new Vector2(1, 1))));
                        float cornerGlow = Mathf.Max(0, 1f - cornerDist / 0.12f);
                        c = Color.Lerp(c, glowColor, cornerGlow * 0.6f);
                    }
                    else if (dEdge < bw + 4)
                    {
                        // Inner glow ring
                        float glowFade = (dEdge - bw) / 4f;
                        c = Color.Lerp(glowColor * 0.3f, new Color(0.08f, 0.05f, 0.12f), glowFade);
                    }
                    else
                    {
                        // Interior — dark with element-tinted gradient
                        c = new Color(0.08f, 0.05f, 0.12f);
                        // Element tint gradient from bottom
                        c = Color.Lerp(c, borderColor * 0.15f, (1f - ny) * 0.3f);
                    }

                    // Outer glow (extends outside border)
                    if (dEdge < 3)
                    {
                        float outerGlow = 1f - dEdge / 3f;
                        c = Color.Lerp(c, glowColor, outerGlow * 0.4f);
                    }

                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Cards/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // RESOURCE ICONS — Stylized gem/coin/ingot/wheat icons
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Resource Icons")]
        static void GenerateResourceIcons()
        {
            GenerateCoinIcon("icon_gold", Gold, GoldBright);
            GenerateGemIcon("icon_gems", new Color(0.50f, 0.20f, 0.80f), new Color(0.75f, 0.45f, 1f));
            GenerateIngotIcon("icon_stone", new Color(0.50f, 0.48f, 0.45f), new Color(0.65f, 0.62f, 0.58f));
            GenerateIngotIcon("icon_iron", new Color(0.45f, 0.50f, 0.58f), new Color(0.60f, 0.65f, 0.72f));
            GenerateWheatIcon("icon_grain", new Color(0.80f, 0.68f, 0.20f), new Color(0.95f, 0.82f, 0.30f));
            GenerateGemIcon("icon_arcane", new Color(0.35f, 0.12f, 0.60f), new Color(0.60f, 0.30f, 0.90f));
            GenerateGemIcon("icon_energy", Teal, new Color(0.30f, 0.90f, 0.75f));
            GenerateStarIcon("icon_star", Gold, GoldBright);
            GenerateStarIcon("icon_star_empty", GoldDim, new Color(0.35f, 0.28f, 0.12f));
            Debug.Log("[ProceduralArt] 9 resource icons generated.");
        }

        static void GenerateCoinIcon(string name, Color main, Color highlight)
        {
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float center = s / 2f, radius = s * 0.42f;

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist > radius + 1) { tex.SetPixel(x, y, Color.clear); continue; }

                    float ndist = dist / radius;
                    float edge = Mathf.Clamp01(radius + 1 - dist); // anti-alias
                    Color c;

                    if (ndist > 0.88f)
                        c = main * 0.6f; // rim
                    else if (ndist > 0.82f)
                        c = main * 0.9f; // inner rim
                    else
                    {
                        // Face with 3D shading
                        float lightAngle = Mathf.Atan2(y - center, x - center);
                        float shade = 0.8f + 0.3f * Mathf.Cos(lightAngle - 0.8f);
                        shade += (1f - ndist) * 0.2f; // brighter at center
                        c = Color.Lerp(main, highlight, (1f - ndist) * 0.5f) * shade;

                        // Embossed inner circle
                        if (ndist > 0.35f && ndist < 0.42f)
                            c *= 0.85f;

                        // Center symbol (cross/diamond shape)
                        float cx = Mathf.Abs(x - center) / radius;
                        float cy = Mathf.Abs(y - center) / radius;
                        if (cx + cy < 0.2f)
                            c = Color.Lerp(c, highlight, 0.3f);
                    }

                    c.a = edge;
                    tex.SetPixel(x, y, c);
                }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Resources/{name}.png");
            Object.DestroyImmediate(tex);
        }

        static void GenerateGemIcon(string name, Color main, Color highlight)
        {
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float cx = s / 2f, cy = s / 2f;

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float nx = (x - cx) / (s * 0.4f);
                    float ny = (y - cy) / (s * 0.45f);

                    // Diamond/hexagonal shape
                    float diamond = Mathf.Abs(nx) + Mathf.Abs(ny);
                    if (diamond > 1.05f) { tex.SetPixel(x, y, Color.clear); continue; }

                    float edge = Mathf.Clamp01(1.05f - diamond) * 10f;
                    edge = Mathf.Clamp01(edge);

                    // Faceted shading
                    float facet = Mathf.Abs(Mathf.Sin(nx * 3.14f)) * Mathf.Abs(Mathf.Sin(ny * 3.14f));
                    Color c = Color.Lerp(main, highlight, facet * 0.6f + (1f - diamond) * 0.3f);

                    // Bright highlight at top-left
                    float hl = Mathf.Max(0, 1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.3f, 0.3f)) / 0.4f);
                    c = Color.Lerp(c, Color.white, hl * 0.35f);

                    // Rim darkening
                    if (diamond > 0.85f)
                        c *= Mathf.Lerp(0.5f, 1f, (1.05f - diamond) / 0.2f);

                    c.a = edge;
                    tex.SetPixel(x, y, c);
                }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Resources/{name}.png");
            Object.DestroyImmediate(tex);
        }

        static void GenerateIngotIcon(string name, Color main, Color highlight)
        {
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float nx = x / (float)s, ny = y / (float)s;

                    // Trapezoid shape (ingot)
                    float topWidth = 0.3f, botWidth = 0.4f;
                    float width = Mathf.Lerp(botWidth, topWidth, ny);
                    float leftEdge = 0.5f - width;
                    float rightEdge = 0.5f + width;

                    bool inside = nx > leftEdge && nx < rightEdge && ny > 0.25f && ny < 0.75f;
                    if (!inside) { tex.SetPixel(x, y, Color.clear); continue; }

                    float shade = 0.7f + 0.4f * ny; // lighter at top
                    float edgeDist = Mathf.Min(nx - leftEdge, rightEdge - nx) / width;
                    shade *= 0.7f + 0.3f * edgeDist;

                    Color c = Color.Lerp(main, highlight, ny * 0.5f) * shade;

                    // Top face highlight
                    if (ny > 0.60f)
                        c = Color.Lerp(c, highlight, (ny - 0.60f) / 0.15f * 0.3f);

                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Resources/{name}.png");
            Object.DestroyImmediate(tex);
        }

        static void GenerateWheatIcon(string name, Color main, Color highlight)
        {
            int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);

            // Clear
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, Color.clear);

            // Draw wheat stalks
            for (int stalk = -1; stalk <= 1; stalk++)
            {
                float baseX = s / 2f + stalk * 18;
                for (int y = 15; y < 110; y++)
                {
                    float sway = Mathf.Sin(y * 0.05f + stalk) * 3f;
                    int px = (int)(baseX + sway);
                    float thickness = y > 70 ? 2f : 1.5f;

                    // Stalk
                    for (int dx = -(int)thickness; dx <= (int)thickness; dx++)
                    {
                        int finalX = px + dx;
                        if (finalX >= 0 && finalX < s)
                            tex.SetPixel(finalX, y, Color.Lerp(main * 0.6f, main, y / 110f));
                    }

                    // Grain kernels (leaves/seeds along stalk)
                    if (y > 55 && y % 8 < 3)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            for (int dy = 0; dy < 6; dy++)
                            {
                                int kx = px + side * (3 + dy);
                                int ky = y + dy * side;
                                if (kx >= 0 && kx < s && ky >= 0 && ky < s)
                                    tex.SetPixel(kx, ky, Color.Lerp(main, highlight, dy / 6f));
                            }
                        }
                    }
                }

                // Top grain head
                for (int dy = 0; dy < 20; dy++)
                {
                    float headWidth = 4f * (1f - dy / 20f);
                    int headY = 90 + dy;
                    float headSway = Mathf.Sin((90 + dy) * 0.05f + stalk) * 3f;
                    int headX = (int)(baseX + headSway);
                    for (int dx = -(int)headWidth; dx <= (int)headWidth; dx++)
                    {
                        int fx = headX + dx;
                        if (fx >= 0 && fx < s && headY < s)
                            tex.SetPixel(fx, headY, Color.Lerp(main, highlight, dy / 20f));
                    }
                }
            }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Resources/{name}.png");
            Object.DestroyImmediate(tex);
        }

        static void GenerateStarIcon(string name, Color main, Color highlight)
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float cx = s / 2f, cy = s / 2f;

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float angle = Mathf.Atan2(dy, dx);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Star shape: 5 points
                    float starAngle = angle + Mathf.PI / 2f;
                    float starRadius = s * 0.38f * (0.5f + 0.5f * Mathf.Cos(starAngle * 5f / 2f));
                    starRadius = Mathf.Lerp(s * 0.18f, s * 0.42f,
                        Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(starAngle * 2.5f)));

                    if (dist > starRadius + 1) { tex.SetPixel(x, y, Color.clear); continue; }

                    float edge = Mathf.Clamp01(starRadius + 1 - dist);
                    float ndist = dist / starRadius;
                    Color c = Color.Lerp(highlight, main, ndist);

                    // Bright center
                    if (ndist < 0.3f)
                        c = Color.Lerp(Color.white, c, ndist / 0.3f);

                    c.a = edge;
                    tex.SetPixel(x, y, c);
                }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Resources/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // NAV ICONS — Clean symbolic icons for bottom navigation
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Nav Icons")]
        static void GenerateNavIcons()
        {
            // Home/Empire — castle turret shape
            GenerateNavIcon("nav_empire", (tex, s) => {
                DrawRect(tex, s/4, 5, s/4+s/8, s/2, Gold);      // left tower
                DrawRect(tex, s*5/8, 5, s*5/8+s/8, s/2, Gold);  // right tower
                DrawRect(tex, s/3, s/6, s*2/3, s/2, Gold);       // center wall
                DrawRect(tex, s*3/8, s/2-2, s*5/8, s*3/4, GoldBright); // gate
                // Battlements
                for (int i = 0; i < 3; i++)
                    DrawRect(tex, s/3 + i*s/8, s/2, s/3 + i*s/8 + s/16, s/2+s/12, Gold);
            });

            // Heroes — shield shape
            GenerateNavIcon("nav_heroes", (tex, s) => {
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++) {
                        float nx = (x - s/2f) / (s*0.35f);
                        float ny = (y - s*0.45f) / (s*0.45f);
                        float shield = nx*nx + ny * Mathf.Abs(ny);
                        if (shield < 1f && ny > -0.8f) {
                            Color c = shield < 0.7f ? Gold : GoldDim;
                            if (Mathf.Abs(nx) < 0.08f || Mathf.Abs(ny + 0.1f) < 0.08f) c = GoldBright;
                            tex.SetPixel(x, y, c);
                        }
                    }
            });

            // Battle — crossed swords
            GenerateNavIcon("nav_battle", (tex, s) => {
                for (int i = 0; i < s; i++) {
                    int w2 = 2;
                    // Sword 1: bottom-left to top-right
                    int x1 = i * s / (s-1), y1 = i * s / (s-1);
                    DrawRect(tex, Mathf.Clamp(x1-w2,0,s-1), Mathf.Clamp(y1-w2,0,s-1),
                        Mathf.Clamp(x1+w2,0,s-1), Mathf.Clamp(y1+w2,0,s-1), Gold);
                    // Sword 2: bottom-right to top-left
                    int x2 = s-1-i, y2 = i;
                    DrawRect(tex, Mathf.Clamp(x2-w2,0,s-1), Mathf.Clamp(y2-w2,0,s-1),
                        Mathf.Clamp(x2+w2,0,s-1), Mathf.Clamp(y2+w2,0,s-1), Gold);
                }
                // Hilts
                DrawRect(tex, s/4-4, s*3/4-2, s/4+4, s*3/4+6, GoldBright);
                DrawRect(tex, s*3/4-4, s*3/4-2, s*3/4+4, s*3/4+6, GoldBright);
            });

            // Alliance — people/group
            GenerateNavIcon("nav_alliance", (tex, s) => {
                // Three figures
                for (int f = -1; f <= 1; f++) {
                    int cx = s/2 + f * s/4;
                    int headY = s*2/3 + (f == 0 ? 4 : 0);
                    int headR = f == 0 ? s/8 : s/10;
                    DrawCircle(tex, cx, headY, headR, f == 0 ? GoldBright : Gold);
                    DrawRect(tex, cx - headR, s/8, cx + headR, headY - headR, f == 0 ? Gold : GoldDim);
                }
            });

            // Shop — diamond/gem
            GenerateNavIcon("nav_shop", (tex, s) => {
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++) {
                        float nx = Mathf.Abs(x - s/2f) / (s*0.35f);
                        float ny = (y - s/2f) / (s*0.4f);
                        float diamond = nx + Mathf.Abs(ny);
                        if (diamond < 1f) {
                            float brightness = 1f - diamond * 0.4f;
                            Color c = Color.Lerp(Gold, GoldBright, brightness);
                            if (ny > 0 && Mathf.Abs(nx) < 0.1f) c = GoldBright;
                            tex.SetPixel(x, y, c);
                        }
                    }
            });

            // Quest — scroll
            GenerateNavIcon("nav_quest", (tex, s) => {
                // Main scroll body
                DrawRect(tex, s/5, s/5, s*4/5, s*4/5, Gold);
                // Top roll
                DrawRect(tex, s/6, s*3/4, s*5/6, s*4/5+4, GoldBright);
                // Bottom roll
                DrawRect(tex, s/6, s/5-4, s*5/6, s/5+2, GoldBright);
                // Text lines
                for (int line = 0; line < 3; line++)
                    DrawRect(tex, s/4, s/3+line*s/7, s*3/4-line*4, s/3+line*s/7+3, GoldDim);
            });

            // Mail — envelope
            GenerateNavIcon("nav_mail", (tex, s) => {
                DrawRect(tex, s/6, s/4, s*5/6, s*3/4, Gold);
                // Flap (triangle approximation)
                for (int y = s/2; y < s*3/4; y++) {
                    float progress = (float)(y - s/2) / (s/4);
                    int left = (int)Mathf.Lerp(s/6, s/2, progress);
                    int right = (int)Mathf.Lerp(s*5/6, s/2, progress);
                    DrawRect(tex, left, y, right, y+1, GoldBright);
                }
            });

            Debug.Log("[ProceduralArt] 7 nav icons generated.");
        }

        static void GenerateNavIcon(string name, System.Action<Texture2D, int> drawFunc)
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, Color.clear);

            drawFunc(tex, s);
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Icons/Nav/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // STATUS BARS — HP, Energy, XP bars with gradient + glow
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Bars")]
        static void GenerateStatusBars()
        {
            GenerateBar("bar_hp_fill", new Color(0.15f, 0.70f, 0.25f), new Color(0.30f, 0.90f, 0.35f));
            GenerateBar("bar_hp_low", Blood, new Color(0.95f, 0.30f, 0.25f));
            GenerateBar("bar_energy", Sky, IceBlue);
            GenerateBar("bar_xp", new Color(0.50f, 0.35f, 0.85f), new Color(0.70f, 0.55f, 0.95f));
            GenerateBar("bar_gold", GoldDim, Gold);
            GenerateBarBg("bar_bg", new Color(0.08f, 0.06f, 0.12f), new Color(0.04f, 0.03f, 0.06f));
            Debug.Log("[ProceduralArt] 6 bars generated.");
        }

        static void GenerateBar(string name, Color left, Color right)
        {
            int w = 256, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    Color c = Color.Lerp(left, right, nx);

                    // Vertical bevel
                    float bevel = ny > 0.5f ? 1.1f + (ny - 0.5f) * 0.3f : 0.85f + ny * 0.3f;
                    c *= bevel;

                    // Top highlight
                    if (ny > 0.75f && ny < 0.9f)
                        c += new Color(0.1f, 0.1f, 0.1f, 0) * Mathf.Clamp01(1f - Mathf.Abs(nx - 0.5f));

                    int radius = 6;
                    float mask = RoundedRectMask(x, y, w, h, radius);
                    c.a = mask;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Bars/{name}.png");
            Object.DestroyImmediate(tex);
        }

        static void GenerateBarBg(string name, Color outer, Color inner)
        {
            int w = 256, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Lerp(inner, outer, Mathf.Abs(ny - 0.5f) * 2f);
                    int radius = 6;
                    float mask = RoundedRectMask(x, y, w, h, radius);
                    c.a = mask * 0.9f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Bars/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // HERO FRAMES — Portrait borders with star slots
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Hero Frames")]
        static void GenerateHeroFrames()
        {
            GenerateHeroFrame("hero_frame_common", new Color(0.45f, 0.42f, 0.38f), 1);
            GenerateHeroFrame("hero_frame_rare", Sky, 2);
            GenerateHeroFrame("hero_frame_epic", Shadow, 3);
            GenerateHeroFrame("hero_frame_legendary", Gold, 5);
            Debug.Log("[ProceduralArt] 4 hero frames generated.");
        }

        static void GenerateHeroFrame(string name, Color border, int stars)
        {
            int s = 192;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            int bw = 6;

            for (int y = 0; y < s; y++)
            {
                float ny = y / (float)s;
                for (int x = 0; x < s; x++)
                {
                    float nx = x / (float)s;
                    int dLeft = x, dRight = s-1-x, dTop = s-1-y, dBot = y;
                    int dEdge = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dTop, dBot));

                    Color c;
                    if (dEdge < bw)
                    {
                        float brightness = 0.7f + 0.3f * (dEdge / (float)bw);
                        c = border * brightness;

                        // Corner flourish
                        float cornerDist = Mathf.Min(
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), Vector2.zero),
                                      Vector2.Distance(new Vector2(nx, ny), Vector2.right)),
                            Mathf.Min(Vector2.Distance(new Vector2(nx, ny), Vector2.up),
                                      Vector2.Distance(new Vector2(nx, ny), Vector2.one)));
                        if (cornerDist < 0.1f)
                            c = Color.Lerp(c, border * 1.5f, (0.1f - cornerDist) * 10f);
                    }
                    else if (dEdge < bw + 2)
                    {
                        c = border * 0.3f;
                    }
                    else
                    {
                        c = Color.clear; // transparent center for portrait
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            // Draw star slots at bottom
            int starY = 12;
            int starSpacing = 16;
            int startX = s/2 - (stars - 1) * starSpacing / 2;
            for (int i = 0; i < stars; i++)
            {
                DrawCircle(tex, startX + i * starSpacing, starY, 5, GoldBright);
            }

            tex.Apply();
            SaveTexture(tex, $"Art/Procedural/Heroes/{name}.png");
            Object.DestroyImmediate(tex);
        }

        // ============================================================
        // MISC ICONS — Check, cross, arrows, etc.
        // ============================================================
        [MenuItem("AshenThrone/Generate Procedural Art/Misc Icons")]
        static void GenerateMiscIcons()
        {
            // Circular back button
            GenerateNavIcon("icon_back", (tex, s) => {
                DrawCircle(tex, s/2, s/2, s/3, new Color(0.2f, 0.15f, 0.1f, 0.8f));
                // Arrow pointing left
                for (int i = 0; i < s/3; i++) {
                    int tipX = s/4 + i;
                    int topY = s/2 + i/2;
                    int botY = s/2 - i/2;
                    DrawRect(tex, tipX, botY, tipX+2, topY, Gold);
                }
                DrawRect(tex, s/4, s/2-2, s*3/4, s/2+2, Gold);
            });

            // Settings gear
            GenerateNavIcon("icon_settings", (tex, s) => {
                DrawCircle(tex, s/2, s/2, s/3, GoldDim);
                DrawCircle(tex, s/2, s/2, s/5, DeepBlack);
                // Teeth
                for (int t = 0; t < 8; t++) {
                    float angle = t * Mathf.PI * 2f / 8f;
                    int tx = (int)(s/2 + Mathf.Cos(angle) * s/3);
                    int ty = (int)(s/2 + Mathf.Sin(angle) * s/3);
                    DrawRect(tex, tx-3, ty-3, tx+3, ty+3, Gold);
                }
            });

            // Info circle
            GenerateNavIcon("icon_info", (tex, s) => {
                DrawCircle(tex, s/2, s/2, s/3, Sky);
                DrawRect(tex, s/2-2, s/4, s/2+2, s/2+s/6, Color.white);
                DrawCircle(tex, s/2, s*2/3, 3, Color.white);
            });

            Debug.Log("[ProceduralArt] 3 misc icons generated.");
        }

        // ============================================================
        // DRAWING HELPERS
        // ============================================================

        static void DrawRect(Texture2D tex, int x1, int y1, int x2, int y2, Color c)
        {
            x1 = Mathf.Clamp(x1, 0, tex.width - 1);
            x2 = Mathf.Clamp(x2, 0, tex.width - 1);
            y1 = Mathf.Clamp(y1, 0, tex.height - 1);
            y2 = Mathf.Clamp(y2, 0, tex.height - 1);
            for (int y = y1; y <= y2; y++)
                for (int x = x1; x <= x2; x++)
                    tex.SetPixel(x, y, c);
        }

        static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist <= r + 0.5f)
                    {
                        float alpha = Mathf.Clamp01(r + 0.5f - dist);
                        Color existing = tex.GetPixel(x, y);
                        tex.SetPixel(x, y, Color.Lerp(existing, c, alpha));
                    }
                }
        }

        static float RoundedRectMask(int x, int y, int w, int h, int radius)
        {
            int cx = x < radius ? radius : (x > w - 1 - radius ? w - 1 - radius : x);
            int cy = y < radius ? radius : (y > h - 1 - radius ? h - 1 - radius : y);
            if (cx != x || cy != y)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                return Mathf.Clamp01(radius + 0.5f - dist);
            }
            return 1f;
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        static void SaveTexture(Texture2D tex, string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", relativePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        }
    }
}
#endif
