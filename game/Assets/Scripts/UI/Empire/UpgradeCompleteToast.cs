using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style toast banner that slides in from the top when a building upgrade completes.
    /// Shows building icon + "Building Name upgraded to Level X!" with star burst fanfare.
    /// </summary>
    public class UpgradeCompleteToast : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private EventSubscription _completedSub;
        private BuildingManager _buildingManager;

        private static readonly Color ToastBg = new(0.05f, 0.03f, 0.09f, 0.94f);
        private static readonly Color ToastBorderGold = new(0.85f, 0.68f, 0.26f, 0.85f);
        private static readonly Color ToastBorderGlow = new(0.92f, 0.75f, 0.28f, 0.30f);
        private static readonly Color TextGold = new(0.90f, 0.72f, 0.28f, 1f);
        private static readonly Color StarColor = new(1f, 0.92f, 0.40f, 1f);
        private static readonly Color LevelUpGlow = new(0.92f, 0.78f, 0.22f, 0.35f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
        }

        private void OnDisable()
        {
            _completedSub?.Dispose();
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            string buildingName = GetBuildingName(evt.PlacedId);
            string buildingId = GetBuildingBaseId(evt.PlacedId);
            int level = evt.NewTier + 1;
            ShowToast(buildingName, buildingId, level);
        }

        private void ShowToast(string buildingName, string buildingId, int level)
        {
            if (_canvasRect == null) return;

            var toast = new GameObject("UpgradeToast");
            toast.transform.SetParent(_canvasRect, false);
            toast.transform.SetAsLastSibling();

            var rect = toast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.86f);
            rect.anchorMax = new Vector2(0.92f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Radial glow behind toast
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(toast.transform, false);
            var glowRect = glowGO.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(-0.08f, -0.6f);
            glowRect.anchorMax = new Vector2(1.08f, 1.6f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.color = LevelUpGlow;
            glowImg.raycastTarget = false;
            var radial = Resources.Load<Sprite>("UI/Production/radial_gradient");
            if (radial != null) { glowImg.sprite = radial; glowImg.type = Image.Type.Simple; }

            // Background
            var bg = toast.AddComponent<Image>();
            bg.color = ToastBg;
            bg.raycastTarget = false;

            // Triple border: outer glow → gold → inner
            var glowBorder = new GameObject("GlowBorder");
            glowBorder.transform.SetParent(toast.transform, false);
            var gbRect = glowBorder.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = glowBorder.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            var gbOut = glowBorder.AddComponent<Outline>();
            gbOut.effectColor = ToastBorderGlow;
            gbOut.effectDistance = new Vector2(2.5f, -2.5f);

            var goldBorder = new GameObject("GoldBorder");
            goldBorder.transform.SetParent(toast.transform, false);
            var goldr = goldBorder.AddComponent<RectTransform>();
            goldr.anchorMin = Vector2.zero; goldr.anchorMax = Vector2.one;
            goldr.offsetMin = Vector2.zero; goldr.offsetMax = Vector2.zero;
            var goldi = goldBorder.AddComponent<Image>();
            goldi.color = new Color(0, 0, 0, 0); goldi.raycastTarget = false;
            var goldo = goldBorder.AddComponent<Outline>();
            goldo.effectColor = ToastBorderGold;
            goldo.effectDistance = new Vector2(1.2f, -1.2f);

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(toast.transform, false);
            var glassRect = glass.AddComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0f, 0.45f);
            glassRect.anchorMax = Vector2.one;
            glassRect.offsetMin = Vector2.zero;
            glassRect.offsetMax = Vector2.zero;
            var glassImg = glass.AddComponent<Image>();
            glassImg.color = new Color(1f, 1f, 1f, 0.06f);
            glassImg.raycastTarget = false;

            // Building icon on left
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(toast.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.08f);
            iconRect.anchorMax = new Vector2(0.12f, 0.92f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            #if UNITY_EDITOR
            int tierIdx = Mathf.Clamp(level, 1, 3);
            var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Buildings/{buildingId}_t{tierIdx}.png");
            if (spr != null) iconImg.sprite = spr;
            else
            #endif
            iconImg.color = TextGold;

            // Level badge on icon
            var badgeGO = new GameObject("LvBadge");
            badgeGO.transform.SetParent(iconGO.transform, false);
            var badgeRect = badgeGO.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, -0.1f);
            badgeRect.anchorMax = new Vector2(1.3f, 0.35f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            var badgeBg = badgeGO.AddComponent<Image>();
            badgeBg.color = new Color(0.83f, 0.66f, 0.26f, 1f);
            badgeBg.raycastTarget = false;
            var badgeText = new GameObject("Lv");
            badgeText.transform.SetParent(badgeGO.transform, false);
            var btRect = badgeText.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            var bt = badgeText.AddComponent<Text>();
            bt.text = $"Lv{level}";
            bt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bt.fontSize = 8;
            bt.fontStyle = FontStyle.Bold;
            bt.alignment = TextAnchor.MiddleCenter;
            bt.color = Color.black;
            bt.raycastTarget = false;

            // Star burst decorations (4 stars around the icon)
            for (int i = 0; i < 4; i++)
            {
                var starGO = new GameObject($"Star_{i}");
                starGO.transform.SetParent(toast.transform, false);
                var starRect = starGO.AddComponent<RectTransform>();
                float angle = i * 90f + 45f;
                float rad = angle * Mathf.Deg2Rad;
                float cx = 0.07f + Mathf.Cos(rad) * 0.06f;
                float cy = 0.5f + Mathf.Sin(rad) * 0.5f;
                starRect.anchorMin = new Vector2(cx - 0.012f, cy - 0.12f);
                starRect.anchorMax = new Vector2(cx + 0.012f, cy + 0.12f);
                starRect.offsetMin = Vector2.zero;
                starRect.offsetMax = Vector2.zero;
                var starImg = starGO.AddComponent<Image>();
                starImg.color = StarColor;
                starImg.raycastTarget = false;
                // Rotate star
                starGO.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }

            // "LEVEL UP!" header with outline
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(toast.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.13f, 0.52f);
            headerRect.anchorMax = new Vector2(0.98f, 0.98f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            var headerText = headerGO.AddComponent<Text>();
            headerText.text = $"\u2726 LEVEL UP!  {buildingName}";
            headerText.fontSize = 13;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.color = Color.white;
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.raycastTarget = false;
            var headerOutline = headerGO.AddComponent<Outline>();
            headerOutline.effectColor = new Color(0, 0, 0, 0.9f);
            headerOutline.effectDistance = new Vector2(0.8f, -0.8f);
            var headerShadow = headerGO.AddComponent<Shadow>();
            headerShadow.effectColor = new Color(0, 0, 0, 0.5f);
            headerShadow.effectDistance = new Vector2(0.4f, -0.8f);

            // Sub-line: "Now Level X" with outline
            var subGO = new GameObject("SubText");
            subGO.transform.SetParent(toast.transform, false);
            var subRect = subGO.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.13f, 0.02f);
            subRect.anchorMax = new Vector2(0.98f, 0.52f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            var subText = subGO.AddComponent<Text>();
            subText.text = $"Now Level {level}  \u2014  New abilities unlocked!";
            subText.fontSize = 10;
            subText.alignment = TextAnchor.MiddleLeft;
            subText.color = new Color(0.72f, 0.68f, 0.60f, 0.95f);
            subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subText.raycastTarget = false;
            var subOutline = subGO.AddComponent<Outline>();
            subOutline.effectColor = new Color(0, 0, 0, 0.7f);
            subOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var cg = toast.AddComponent<CanvasGroup>();
            StartCoroutine(AnimateToast(toast, rect, cg));
        }

        private IEnumerator AnimateToast(GameObject toast, RectTransform rect, CanvasGroup cg)
        {
            // Slide in from above (start offset up)
            Vector2 targetMin = rect.anchorMin;
            Vector2 targetMax = rect.anchorMax;
            float offset = 0.08f;
            rect.anchorMin = targetMin + Vector2.up * offset;
            rect.anchorMax = targetMax + Vector2.up * offset;
            cg.alpha = 0f;
            toast.transform.localScale = Vector3.one * 0.7f;

            // Slide down + fade in + scale pop
            float slideTime = 0.35f;
            float elapsed = 0f;
            while (elapsed < slideTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideTime);
                // Overshoot ease for pop effect
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                float scalePop = t < 0.7f
                    ? Mathf.Lerp(0.7f, 1.08f, t / 0.7f)
                    : Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
                rect.anchorMin = Vector2.Lerp(targetMin + Vector2.up * offset, targetMin, ease);
                rect.anchorMax = Vector2.Lerp(targetMax + Vector2.up * offset, targetMax, ease);
                cg.alpha = ease;
                toast.transform.localScale = Vector3.one * scalePop;
                yield return null;
            }
            rect.anchorMin = targetMin;
            rect.anchorMax = targetMax;
            cg.alpha = 1f;
            toast.transform.localScale = Vector3.one;

            // Star burst shimmer — rapid shimmer + scale burst + glow pulse
            float shimmerTime = 0.8f;
            elapsed = 0f;
            var stars = new System.Collections.Generic.List<Image>();
            foreach (Transform child in toast.transform)
                if (child.name.StartsWith("Star_"))
                    stars.Add(child.GetComponent<Image>());

            // Also pulse the background glow
            var glowTransform = toast.transform.Find("Glow");
            Image backgroundGlow = glowTransform != null ? glowTransform.GetComponent<Image>() : null;

            while (elapsed < shimmerTime)
            {
                elapsed += Time.deltaTime;
                float fastPulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 14f);
                float burstScale = elapsed < 0.3f
                    ? Mathf.Lerp(0.5f, 1.4f, elapsed / 0.3f)
                    : Mathf.Lerp(1.4f, 1f, (elapsed - 0.3f) / 0.5f);

                for (int i = 0; i < stars.Count; i++)
                {
                    if (stars[i] == null) continue;
                    // Stagger each star's shimmer
                    float stagger = Mathf.Sin(elapsed * 14f + i * 1.5f) * 0.5f + 0.5f;
                    stars[i].color = new Color(StarColor.r, StarColor.g, StarColor.b, stagger);
                    stars[i].transform.localScale = Vector3.one * burstScale;
                }

                // Glow pulse during fanfare
                if (backgroundGlow != null)
                    backgroundGlow.color = new Color(LevelUpGlow.r, LevelUpGlow.g, LevelUpGlow.b,
                        LevelUpGlow.a + 0.15f * fastPulse);

                yield return null;
            }
            // Fade stars out with scale shrink
            float fadeStarTime = 0.3f;
            elapsed = 0f;
            while (elapsed < fadeStarTime)
            {
                elapsed += Time.deltaTime;
                float t2 = elapsed / fadeStarTime;
                foreach (var star in stars)
                {
                    if (star == null) continue;
                    star.color = new Color(StarColor.r, StarColor.g, StarColor.b, 1f - t2);
                    star.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t2);
                }
                yield return null;
            }
            foreach (var star in stars)
                if (star != null) star.color = new Color(StarColor.r, StarColor.g, StarColor.b, 0f);

            // Hold visible
            yield return new WaitForSeconds(2.0f);

            // Fade out
            float fadeTime = 0.5f;
            elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }

            Destroy(toast);
        }

        private string GetBuildingBaseId(string placedId)
        {
            if (_buildingManager != null && _buildingManager.PlacedBuildings.TryGetValue(placedId, out var placed))
            {
                if (placed.Data != null) return placed.Data.buildingId;
            }
            // Strip instance suffix (e.g., "grain_farm_2" -> "grain_farm")
            if (placedId.Length > 0 && char.IsDigit(placedId[placedId.Length - 1]))
            {
                int lastUnderscore = placedId.LastIndexOf('_');
                if (lastUnderscore > 0) return placedId.Substring(0, lastUnderscore);
            }
            return placedId;
        }

        private string GetBuildingName(string placedId)
        {
            if (_buildingManager != null && _buildingManager.PlacedBuildings.TryGetValue(placedId, out var placed))
            {
                if (placed.Data != null && !string.IsNullOrEmpty(placed.Data.displayName))
                    return placed.Data.displayName;
            }
            string id = placedId.Contains("_") ? placedId.Substring(0, placedId.LastIndexOf('_')) : placedId;
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
