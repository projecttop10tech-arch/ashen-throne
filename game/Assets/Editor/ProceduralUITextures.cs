#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace AshenThrone.Editor
{
    public static class ProceduralUITextures
    {
        private const string OutputDir = "Assets/Art/UI/Generated";

        // Gold palette
        static readonly Color GoldBright = new Color(0.95f, 0.80f, 0.35f);
        static readonly Color GoldMid = new Color(0.75f, 0.58f, 0.22f);
        static readonly Color GoldDark = new Color(0.45f, 0.32f, 0.12f);
        static readonly Color GoldSheen = new Color(1f, 0.92f, 0.55f);

        // Panel palette
        static readonly Color PanelDark = new Color(0.06f, 0.04f, 0.10f);
        static readonly Color PanelMid = new Color(0.10f, 0.08f, 0.16f);
        static readonly Color PanelLight = new Color(0.16f, 0.13f, 0.24f);

        [MenuItem("AshenThrone/Generate UI Textures")]
        public static void GenerateAll()
        {
            if (!Directory.Exists(OutputDir))
                Directory.CreateDirectory(OutputDir);

            // Panels
            GeneratePanel("panel_dark", 512, 256, PanelDark, PanelMid, GoldMid, 12, 2, false);
            GeneratePanel("panel_ornate_gen", 512, 256, PanelDark, PanelLight, GoldBright, 12, 3, true);
            GeneratePanel("panel_info", 512, 128, new Color(0.05f, 0.04f, 0.09f), new Color(0.09f, 0.07f, 0.14f), GoldDark, 8, 2, false);

            // Buttons
            GenerateButton("btn_gold", 256, 96, GoldDark, GoldMid, GoldBright, 10);
            GenerateButton("btn_red", 256, 96,
                new Color(0.35f, 0.08f, 0.06f), new Color(0.55f, 0.14f, 0.10f), new Color(0.80f, 0.25f, 0.18f), 10);
            GenerateButton("btn_teal", 256, 96,
                new Color(0.06f, 0.20f, 0.22f), new Color(0.10f, 0.38f, 0.38f), new Color(0.18f, 0.58f, 0.55f), 10);
            GenerateButton("btn_purple", 256, 96,
                new Color(0.18f, 0.06f, 0.28f), new Color(0.30f, 0.12f, 0.45f), new Color(0.48f, 0.22f, 0.65f), 10);
            GenerateButton("btn_dark", 256, 96, PanelDark, PanelMid, PanelLight, 10);

            // Neutral/white button — can be tinted to ANY color via Image.color
            GenerateButton("btn_neutral", 256, 96,
                new Color(0.50f, 0.48f, 0.45f), new Color(0.72f, 0.70f, 0.66f), new Color(0.92f, 0.90f, 0.85f), 10);

            // Neutral panel — tintable to any color
            GeneratePanel("panel_neutral", 512, 256,
                new Color(0.40f, 0.38f, 0.36f), new Color(0.55f, 0.52f, 0.50f), new Color(0.75f, 0.72f, 0.68f), 12, 2, false);

            // Ornate frame (double-border with corner accents)
            GenerateOrnateFrame("frame_ornate", 512, 512, GoldBright, GoldDark, PanelDark, 14, 4);

            // Badges / pills
            GeneratePill("badge_gold", 128, 48, GoldDark, GoldBright, 24);
            GeneratePill("badge_red", 128, 48,
                new Color(0.55f, 0.10f, 0.08f), new Color(0.90f, 0.20f, 0.15f), 24);
            GeneratePill("badge_level", 96, 48,
                new Color(0.04f, 0.03f, 0.08f), GoldMid, 24);

            // Progress bars
            GenerateProgressBarBg("bar_bg", 256, 32, 16);
            GenerateProgressBarFill("bar_fill_green", 256, 32,
                new Color(0.15f, 0.55f, 0.20f), new Color(0.30f, 0.85f, 0.40f), 16);
            GenerateProgressBarFill("bar_fill_red", 256, 32,
                new Color(0.55f, 0.12f, 0.08f), new Color(0.90f, 0.25f, 0.18f), 16);
            GenerateProgressBarFill("bar_fill_blue", 256, 32,
                new Color(0.10f, 0.25f, 0.55f), new Color(0.22f, 0.50f, 0.90f), 16);
            GenerateProgressBarFill("bar_fill_gold", 256, 32,
                GoldDark, GoldBright, 16);

            // Tabs
            GenerateTab("tab_active", 256, 80, PanelLight, GoldBright, true, 8);
            GenerateTab("tab_inactive", 256, 80, PanelDark, GoldDark, false, 8);

            // Nav bar background
            GeneratePanel("nav_bar_bg", 1024, 128, new Color(0.08f, 0.05f, 0.12f), new Color(0.12f, 0.08f, 0.18f), GoldMid, 0, 3, true);

            // Resource bar background
            GeneratePanel("res_bar_bg", 1024, 64, new Color(0.04f, 0.03f, 0.07f), new Color(0.07f, 0.05f, 0.11f), GoldDark, 0, 2, false);

            AssetDatabase.Refresh();

            // Fix all import settings to Sprite
            FixImportSettings();

            Debug.Log($"[ProceduralUITextures] Generated {Directory.GetFiles(OutputDir, "*.png").Length} textures in {OutputDir}");
        }

        // ==============================================================
        // PANEL — rounded rect with gradient, border, inner shadow/highlight
        // ==============================================================
        static void GeneratePanel(string name, int w, int h, Color bottomColor, Color topColor, Color borderColor, int radius, int borderWidth, bool ornate)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float distToEdge = RoundedRectSDF(x, y, w, h, radius);

                    if (distToEdge > 0.5f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    // Gradient fill
                    Color fill = Color.Lerp(bottomColor, topColor, t);

                    // Add subtle noise for richness
                    float noise = PseudoNoise(x, y) * 0.02f;
                    fill.r = Mathf.Clamp01(fill.r + noise);
                    fill.g = Mathf.Clamp01(fill.g + noise * 0.8f);
                    fill.b = Mathf.Clamp01(fill.b + noise * 0.6f);

                    // Inner highlight (top edge glow — lit from above)
                    if (y > h - borderWidth - 8 && y <= h - borderWidth && distToEdge < -borderWidth)
                    {
                        float ht = 1f - (float)(h - borderWidth - y) / 8f;
                        fill = Color.Lerp(fill, new Color(fill.r + 0.12f, fill.g + 0.10f, fill.b + 0.08f, 1f), ht * 0.5f);
                    }

                    // Inner shadow (bottom edge — recessed)
                    if (y < borderWidth + 6 && y >= borderWidth && distToEdge < -borderWidth)
                    {
                        float st = (float)(y - borderWidth) / 6f;
                        fill = Color.Lerp(new Color(0.01f, 0.01f, 0.02f, 1f), fill, st);
                    }

                    // Border
                    if (distToEdge > -borderWidth && distToEdge <= 0.5f)
                    {
                        float borderT = Mathf.Clamp01((distToEdge + borderWidth) / (float)borderWidth);
                        Color bColor = borderColor;

                        // Metallic sheen on border — brighter at top
                        float sheen = Mathf.Clamp01(t * 1.5f - 0.2f);
                        bColor = Color.Lerp(bColor * 0.6f, bColor * 1.3f, sheen);

                        if (ornate)
                        {
                            // Ornate: double-line effect
                            float inner = Mathf.Abs(borderT - 0.5f) * 2f;
                            if (inner < 0.3f)
                                bColor = Color.Lerp(bColor, PanelDark, 0.6f); // gap between double border
                        }

                        fill = Color.Lerp(fill, bColor, Mathf.SmoothStep(0f, 1f, borderT));
                    }

                    // Anti-alias the outer edge
                    if (distToEdge > -0.5f && distToEdge <= 0.5f)
                    {
                        float aa = 1f - Mathf.Clamp01(distToEdge + 0.5f);
                        fill.a = aa;
                    }
                    else
                    {
                        fill.a = 1f;
                    }

                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, radius > 0 ? borderWidth + radius : borderWidth + 2);
        }

        // ==============================================================
        // BUTTON — raised look with bevel, gradient, thick border
        // ==============================================================
        static void GenerateButton(string name, int w, int h, Color darkColor, Color midColor, Color brightColor, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            int borderW = 3;

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = RoundedRectSDF(x, y, w, h, radius);

                    if (dist > 0.5f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    // Convex gradient — bright center fading to dark edges
                    float centerT = 1f - Mathf.Abs(t - 0.55f) * 2f; // peak slightly above center
                    centerT = Mathf.Clamp01(centerT);
                    Color fill = Color.Lerp(darkColor, midColor, centerT);

                    // Glossy highlight across top 35%
                    if (t > 0.65f)
                    {
                        float glossT = (t - 0.65f) / 0.35f;
                        glossT = glossT * glossT; // ease in
                        fill = Color.Lerp(fill, brightColor * 0.85f, glossT * 0.35f);
                    }

                    // Inner bevel — light top edge
                    if (dist < -borderW && dist > -borderW - 3 && t > 0.7f)
                    {
                        float bevelT = (dist + borderW + 3) / 3f;
                        fill = Color.Lerp(brightColor * 0.7f, fill, bevelT);
                    }
                    // Inner bevel — dark bottom edge
                    if (dist < -borderW && dist > -borderW - 2 && t < 0.3f)
                    {
                        float bevelT = (dist + borderW + 2) / 2f;
                        fill = Color.Lerp(darkColor * 0.5f, fill, bevelT);
                    }

                    // Noise
                    float noise = PseudoNoise(x, y) * 0.015f;
                    fill.r = Mathf.Clamp01(fill.r + noise);
                    fill.g = Mathf.Clamp01(fill.g + noise * 0.8f);
                    fill.b = Mathf.Clamp01(fill.b + noise * 0.6f);

                    // Border with metallic gradient
                    if (dist > -borderW && dist <= 0.5f)
                    {
                        float borderT = Mathf.Clamp01((dist + borderW) / (float)borderW);
                        Color bColor = Color.Lerp(darkColor * 0.8f, brightColor, t); // metallic top-to-bottom
                        fill = Color.Lerp(fill, bColor, Mathf.SmoothStep(0f, 1f, borderT));
                    }

                    // AA
                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    fill.a = aa;

                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, borderW + radius);
        }

        // ==============================================================
        // ORNATE FRAME — double border + corner diamonds
        // ==============================================================
        static void GenerateOrnateFrame(string name, int w, int h, Color borderBright, Color borderDark, Color fill, int radius, int borderW)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = RoundedRectSDF(x, y, w, h, radius);

                    if (dist > 1f)
                    {
                        pixels[y * w + x] = Color.clear;
                        continue;
                    }

                    // Fill gradient
                    Color c = Color.Lerp(fill, fill * 1.15f, t);
                    float noise = PseudoNoise(x, y) * 0.015f;
                    c.r = Mathf.Clamp01(c.r + noise);
                    c.g = Mathf.Clamp01(c.g + noise * 0.8f);
                    c.b = Mathf.Clamp01(c.b + noise);

                    // Outer border (bright gold)
                    int totalBorder = borderW * 2 + 2; // outer + gap + inner
                    if (dist > -totalBorder && dist <= 1f)
                    {
                        float bd = -dist;
                        if (bd < borderW) // outer border zone
                        {
                            float bt = bd / borderW;
                            Color bc = Color.Lerp(borderDark, borderBright, t * 0.8f + 0.1f); // metallic
                            bc = Color.Lerp(bc, GoldSheen, Mathf.Max(0, Mathf.Sin(t * 3.14f) * 0.2f)); // sheen
                            c = Color.Lerp(c, bc, Mathf.SmoothStep(0f, 1f, bt));
                        }
                        else if (bd < borderW + 2) // dark gap
                        {
                            c = Color.Lerp(c, fill * 0.5f, 0.7f);
                        }
                        else if (bd < totalBorder) // inner border
                        {
                            float bt = (bd - borderW - 2) / borderW;
                            Color bc = Color.Lerp(borderDark * 0.8f, borderBright * 0.7f, t);
                            c = Color.Lerp(c, bc, Mathf.SmoothStep(0f, 1f, bt) * 0.8f);
                        }
                    }

                    // Corner diamond accents
                    float cornerDist = CornerDiamondDist(x, y, w, h, radius + borderW);
                    if (cornerDist < 8f)
                    {
                        float ct = 1f - cornerDist / 8f;
                        c = Color.Lerp(c, GoldSheen, ct * 0.8f);
                    }

                    // Glass highlight at top
                    if (t > 0.88f && dist < -totalBorder)
                    {
                        float gt = (t - 0.88f) / 0.12f;
                        c = Color.Lerp(c, new Color(c.r + 0.06f, c.g + 0.05f, c.b + 0.08f, 1f), gt * 0.4f);
                    }

                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    c.a = aa;
                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, totalBorder: borderW * 2 + 2 + radius);
        }

        // ==============================================================
        // PILL / BADGE — capsule shape
        // ==============================================================
        static void GeneratePill(string name, int w, int h, Color darkColor, Color borderColor, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            int borderW = 2;

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = RoundedRectSDF(x, y, w, h, radius);
                    if (dist > 0.5f) { pixels[y * w + x] = Color.clear; continue; }

                    Color fill = Color.Lerp(darkColor, darkColor * 1.3f, t * 0.6f);
                    float noise = PseudoNoise(x, y) * 0.01f;
                    fill.r = Mathf.Clamp01(fill.r + noise);
                    fill.g = Mathf.Clamp01(fill.g + noise);
                    fill.b = Mathf.Clamp01(fill.b + noise);

                    // Glossy top highlight
                    if (t > 0.7f)
                    {
                        float g = (t - 0.7f) / 0.3f;
                        fill = Color.Lerp(fill, fill * 1.4f, g * 0.3f);
                    }

                    if (dist > -borderW)
                    {
                        float bt = Mathf.Clamp01((dist + borderW) / (float)borderW);
                        Color bc = Color.Lerp(borderColor * 0.6f, borderColor, t);
                        fill = Color.Lerp(fill, bc, Mathf.SmoothStep(0f, 1f, bt));
                    }

                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    fill.a = aa;
                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, radius);
        }

        // ==============================================================
        // PROGRESS BAR BACKGROUND — inset dark track
        // ==============================================================
        static void GenerateProgressBarBg(string name, int w, int h, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = RoundedRectSDF(x, y, w, h, radius);
                    if (dist > 0.5f) { pixels[y * w + x] = Color.clear; continue; }

                    // Dark inset look
                    Color fill = Color.Lerp(new Color(0.02f, 0.01f, 0.04f), new Color(0.06f, 0.04f, 0.08f), t);

                    // Top shadow (inset)
                    if (t > 0.75f && dist < -2)
                    {
                        float s = (t - 0.75f) / 0.25f;
                        fill = Color.Lerp(fill, new Color(0.01f, 0.01f, 0.02f), s * 0.4f);
                    }

                    // Border
                    if (dist > -2)
                    {
                        float bt = Mathf.Clamp01((dist + 2) / 2f);
                        fill = Color.Lerp(fill, new Color(0.25f, 0.20f, 0.15f), bt * 0.6f);
                    }

                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    fill.a = aa;
                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, radius);
        }

        // ==============================================================
        // PROGRESS BAR FILL — glossy colored fill
        // ==============================================================
        static void GenerateProgressBarFill(string name, int w, int h, Color dark, Color bright, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float dist = RoundedRectSDF(x, y, w, h, radius);
                    if (dist > 0.5f) { pixels[y * w + x] = Color.clear; continue; }

                    // Vertical gradient
                    Color fill = Color.Lerp(dark, bright, t * 0.7f);

                    // Glossy highlight across top 30%
                    if (t > 0.7f)
                    {
                        float g = (t - 0.7f) / 0.3f;
                        fill = Color.Lerp(fill, bright * 1.3f, g * 0.5f);
                    }

                    float noise = PseudoNoise(x, y) * 0.015f;
                    fill.r = Mathf.Clamp01(fill.r + noise);
                    fill.g = Mathf.Clamp01(fill.g + noise);
                    fill.b = Mathf.Clamp01(fill.b + noise);

                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    fill.a = aa;
                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, radius);
        }

        // ==============================================================
        // TAB — active or inactive state
        // ==============================================================
        static void GenerateTab(string name, int w, int h, Color fillColor, Color accentColor, bool active, int radius)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            int borderW = active ? 2 : 1;

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    // Only round top corners (bottom is flat)
                    float dist = RoundedRectSDF(x, y, w, h, y > h / 2 ? radius : 0);
                    if (dist > 0.5f) { pixels[y * w + x] = Color.clear; continue; }

                    Color fill = Color.Lerp(fillColor, fillColor * (active ? 1.25f : 1.05f), t);

                    if (active)
                    {
                        // Bottom accent bar
                        if (y < 6)
                        {
                            float barT = (float)y / 6f;
                            fill = Color.Lerp(accentColor, fill, barT);
                        }
                        // Glass top
                        if (t > 0.8f)
                        {
                            float g = (t - 0.8f) / 0.2f;
                            fill = Color.Lerp(fill, fill * 1.2f, g * 0.3f);
                        }
                    }

                    float noise = PseudoNoise(x, y) * 0.01f;
                    fill.r = Mathf.Clamp01(fill.r + noise);
                    fill.g = Mathf.Clamp01(fill.g + noise);
                    fill.b = Mathf.Clamp01(fill.b + noise);

                    // Border — only top and sides for active, all edges for inactive
                    if (dist > -borderW)
                    {
                        float bt = Mathf.Clamp01((dist + borderW) / (float)borderW);
                        Color bc = active ? accentColor : accentColor * 0.4f;
                        // Skip bottom border for active tab
                        if (active && y < 2) bt = 0;
                        fill = Color.Lerp(fill, bc, Mathf.SmoothStep(0f, 1f, bt) * 0.7f);
                    }

                    float aa = dist > -0.5f ? (1f - Mathf.Clamp01(dist + 0.5f)) : 1f;
                    fill.a = aa;
                    pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            SaveTexture(tex, name, w, h, borderW + radius);
        }

        // ==============================================================
        // HELPERS
        // ==============================================================

        /// <summary>Signed distance field for a rounded rectangle. Negative = inside, positive = outside.</summary>
        static float RoundedRectSDF(int px, int py, int w, int h, int radius)
        {
            float cx = Mathf.Max(0, Mathf.Max(radius - px, px - (w - 1 - radius)));
            float cy = Mathf.Max(0, Mathf.Max(radius - py, py - (h - 1 - radius)));
            float cornerDist = Mathf.Sqrt(cx * cx + cy * cy) - radius;
            float edgeDist = Mathf.Max(
                Mathf.Max(-px, px - (w - 1)),
                Mathf.Max(-py, py - (h - 1))
            );
            return radius > 0 ? Mathf.Max(cornerDist, edgeDist) : edgeDist;
        }

        /// <summary>Distance from corner diamond accents.</summary>
        static float CornerDiamondDist(int px, int py, int w, int h, int inset)
        {
            float minDist = float.MaxValue;
            // Four corners
            int[][] corners = {
                new[] { inset, inset },
                new[] { w - 1 - inset, inset },
                new[] { inset, h - 1 - inset },
                new[] { w - 1 - inset, h - 1 - inset }
            };
            foreach (var c in corners)
            {
                float dx = Mathf.Abs(px - c[0]);
                float dy = Mathf.Abs(py - c[1]);
                float d = dx + dy; // diamond/manhattan distance
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>Deterministic pseudo-noise for texture grain.</summary>
        static float PseudoNoise(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n & 0x7fffffff) / (float)0x7fffffff) - 0.5f;
        }

        /// <summary>Save texture and set up proper sprite import settings.</summary>
        static void SaveTexture(Texture2D tex, string name, int w, int h, int totalBorder)
        {
            string path = $"{OutputDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Set sprite import settings
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;

                // Set 9-slice border (left, bottom, right, top) — all equal for symmetric shapes
                int b = Mathf.Min(totalBorder + 2, Mathf.Min(w, h) / 3);
                importer.spriteBorder = new Vector4(b, b, b, b);

                importer.SaveAndReimport();
            }
        }

        static void FixImportSettings()
        {
            var files = Directory.GetFiles(OutputDir, "*.png");
            foreach (var file in files)
            {
                string assetPath = file.Replace(Application.dataPath.Replace("Assets", ""), "");
                if (!assetPath.StartsWith("Assets"))
                    assetPath = "Assets" + file.Substring(Application.dataPath.Length);

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
#endif
