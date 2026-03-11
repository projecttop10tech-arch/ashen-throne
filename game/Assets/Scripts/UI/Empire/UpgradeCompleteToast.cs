using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style toast banner that slides in from the top when a building upgrade completes.
    /// Shows "Building Name upgraded to Level X!" then fades out after 3 seconds.
    /// </summary>
    public class UpgradeCompleteToast : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private EventSubscription _completedSub;
        private BuildingManager _buildingManager;

        private static readonly Color ToastBg = new(0.06f, 0.04f, 0.10f, 0.92f);
        private static readonly Color ToastBorder = new(0.83f, 0.66f, 0.26f, 0.80f);
        private static readonly Color TextGold = new(0.83f, 0.66f, 0.26f, 1f);

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
            int level = evt.NewTier + 1;
            ShowToast($"{buildingName} upgraded to Level {level}!");
        }

        private void ShowToast(string message)
        {
            if (_canvasRect == null) return;

            var toast = new GameObject("UpgradeToast");
            toast.transform.SetParent(_canvasRect, false);
            toast.transform.SetAsLastSibling();

            var rect = toast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.88f);
            rect.anchorMax = new Vector2(0.90f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Background
            var bg = toast.AddComponent<Image>();
            bg.color = ToastBg;
            bg.raycastTarget = false;

            // Gold border
            var outline = toast.AddComponent<Outline>();
            outline.effectColor = ToastBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Message text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toast.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            var text = textGO.AddComponent<Text>();
            text.text = message;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = TextGold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);

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

            // Slide down + fade in
            float slideTime = 0.3f;
            float elapsed = 0f;
            while (elapsed < slideTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideTime;
                float ease = t * t * (3f - 2f * t);
                rect.anchorMin = Vector2.Lerp(targetMin + Vector2.up * offset, targetMin, ease);
                rect.anchorMax = Vector2.Lerp(targetMax + Vector2.up * offset, targetMax, ease);
                cg.alpha = ease;
                yield return null;
            }
            rect.anchorMin = targetMin;
            rect.anchorMax = targetMax;
            cg.alpha = 1f;

            // Hold visible
            yield return new WaitForSeconds(2.5f);

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
