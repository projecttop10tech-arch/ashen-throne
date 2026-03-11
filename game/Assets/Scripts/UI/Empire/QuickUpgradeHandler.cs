using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style double-tap quick upgrade. When a building is double-tapped,
    /// immediately attempts to start the upgrade without opening the info popup.
    /// Shows a brief toast confirming the upgrade started or explaining why it failed.
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
                ShowQuickToast($"Upgrading {FormatBuildingName(evt.BuildingId)} to Lv.{evt.Tier + 1}!",
                    new Color(0.20f, 0.80f, 0.40f, 1f));
            }
            // If failed, BuildManager already published BuildFailedEvent which other UI handles
        }

        private void ShowQuickToast(string message, Color color)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var toastGO = new GameObject("QuickUpgradeToast");
            toastGO.transform.SetParent(canvas.transform, false);
            toastGO.transform.SetAsLastSibling();

            var rect = toastGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.55f);
            rect.anchorMax = new Vector2(0.85f, 0.60f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = toastGO.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.06f, 0.04f, 0.10f, 0.9f);
            bg.raycastTarget = false;

            var outline = toastGO.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toastGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = message;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;

            var cg = toastGO.AddComponent<CanvasGroup>();
            StartCoroutine(FadeToast(toastGO, cg));
        }

        private System.Collections.IEnumerator FadeToast(GameObject go, CanvasGroup cg)
        {
            yield return new WaitForSeconds(1.2f);
            float elapsed = 0f;
            float fadeDuration = 0.4f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeDuration);
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
