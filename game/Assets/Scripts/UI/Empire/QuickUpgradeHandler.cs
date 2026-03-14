using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality double-tap quick upgrade toast with ornate border,
    /// elastic pop-in, glow effect, slide-up entrance, and smooth fade.
    /// </summary>
    public class QuickUpgradeHandler : MonoBehaviour
    {
        private BuildingManager _buildingManager;
        private EventSubscription _doubleTapSub;

        private void Start()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _doubleTapSub = EventBus.Subscribe<BuildingDoubleTappedEvent>(OnDoubleTap);
        }

        private void OnDisable()
        {
            _doubleTapSub?.Dispose();
        }

        private void OnDoubleTap(BuildingDoubleTappedEvent evt)
        {
            if (_buildingManager == null) return;

            bool success = _buildingManager.StartUpgrade(evt.InstanceId);
            if (success)
            {
                Debug.Log($"[QuickUpgrade] Started upgrade for {evt.BuildingId} ({evt.InstanceId}) via double-tap.");
                ShowQuickToast($"\u2692 Upgrading {FormatBuildingName(evt.BuildingId)} to Lv.{evt.Tier + 1}!",
                    new Color(0.25f, 0.85f, 0.45f, 1f));
            }
        }

        private void ShowQuickToast(string message, Color color)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var toastGO = new GameObject("QuickUpgradeToast");
            toastGO.transform.SetParent(canvas.transform, false);
            toastGO.transform.SetAsLastSibling();

            var rect = toastGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.54f);
            rect.anchorMax = new Vector2(0.88f, 0.60f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Ornate background
            var bg = toastGO.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.09f, 0.93f);
            bg.raycastTarget = false;

            // Triple border: glow → gold → inner
            var glowBorder = new GameObject("GlowBorder");
            glowBorder.transform.SetParent(toastGO.transform, false);
            var gbr = glowBorder.AddComponent<RectTransform>();
            gbr.anchorMin = Vector2.zero; gbr.anchorMax = Vector2.one;
            gbr.offsetMin = Vector2.zero; gbr.offsetMax = Vector2.zero;
            var gbi = glowBorder.AddComponent<Image>();
            gbi.color = new Color(0, 0, 0, 0); gbi.raycastTarget = false;
            var gbo = glowBorder.AddComponent<Outline>();
            gbo.effectColor = new Color(color.r, color.g, color.b, 0.30f);
            gbo.effectDistance = new Vector2(2.5f, -2.5f);

            var goldBorder = new GameObject("GoldBorder");
            goldBorder.transform.SetParent(toastGO.transform, false);
            var goldr = goldBorder.AddComponent<RectTransform>();
            goldr.anchorMin = Vector2.zero; goldr.anchorMax = Vector2.one;
            goldr.offsetMin = Vector2.zero; goldr.offsetMax = Vector2.zero;
            var goldi = goldBorder.AddComponent<Image>();
            goldi.color = new Color(0, 0, 0, 0); goldi.raycastTarget = false;
            var goldo = goldBorder.AddComponent<Outline>();
            goldo.effectColor = new Color(color.r * 0.9f, color.g * 0.85f, color.b * 0.6f, 0.75f);
            goldo.effectDistance = new Vector2(1.2f, -1.2f);

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(toastGO.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.5f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero;
            glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.06f);
            glImg.raycastTarget = false;

            // Left accent icon glow
            var iconGlow = new GameObject("IconGlow");
            iconGlow.transform.SetParent(toastGO.transform, false);
            var igRect = iconGlow.AddComponent<RectTransform>();
            igRect.anchorMin = new Vector2(-0.01f, -0.2f);
            igRect.anchorMax = new Vector2(0.08f, 1.2f);
            igRect.offsetMin = Vector2.zero;
            igRect.offsetMax = Vector2.zero;
            var igImg = iconGlow.AddComponent<Image>();
            igImg.color = new Color(color.r, color.g, color.b, 0.12f);
            igImg.raycastTarget = false;

            // Text with outline + shadow
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toastGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = message;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.9f);
            textOutline.effectDistance = new Vector2(0.7f, -0.7f);
            var textShadow = textGO.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.5f);
            textShadow.effectDistance = new Vector2(0.3f, -0.6f);

            var cg = toastGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            toastGO.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            StartCoroutine(AnimateToast(toastGO, rect, cg));
        }

        private System.Collections.IEnumerator AnimateToast(GameObject go, RectTransform rect, CanvasGroup cg)
        {
            // Slide up + elastic pop-in (0.3s)
            float startY = rect.anchorMin.y - 0.03f;
            float targetMinY = rect.anchorMin.y;
            float targetMaxY = rect.anchorMax.y;
            float elapsed = 0f;
            const float popDuration = 0.30f;

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);

                // Elastic scale
                float scale;
                if (t < 0.55f)
                    scale = Mathf.Lerp(0.7f, 1.12f, t / 0.55f);
                else
                    scale = Mathf.Lerp(1.12f, 1f, (t - 0.55f) / 0.45f);
                go.transform.localScale = Vector3.one * scale;

                // Slide up
                float y = Mathf.Lerp(startY, targetMinY, t * t * (3f - 2f * t)); // smoothstep
                rect.anchorMin = new Vector2(rect.anchorMin.x, y);
                rect.anchorMax = new Vector2(rect.anchorMax.x, y + (targetMaxY - targetMinY));

                // Fade in
                cg.alpha = Mathf.Clamp01(t * 3f);

                yield return null;
            }

            go.transform.localScale = Vector3.one;
            cg.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(1.4f);

            // Slide up + fade out (0.35s)
            elapsed = 0f;
            const float fadeDuration = 0.35f;
            float fadeStartY = rect.anchorMin.y;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                cg.alpha = 1f - t;
                float y = fadeStartY + t * 0.02f;
                rect.anchorMin = new Vector2(rect.anchorMin.x, y);
                rect.anchorMax = new Vector2(rect.anchorMax.x, y + (targetMaxY - targetMinY));
                yield return null;
            }

            Destroy(go);
        }

        private static string FormatBuildingName(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return buildingId;
            return buildingId.Replace('_', ' ');
        }
    }
}
