using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style red error toast when a build/upgrade action fails.
    /// Subscribes to BuildFailedEvent and shows a brief slide-in notification.
    /// </summary>
    public class BuildFailedToast : MonoBehaviour
    {
        private Canvas _canvas;
        private EventSubscription _sub;
        private GameObject _currentToast;

        private static readonly Color ErrorBg = new(0.55f, 0.12f, 0.12f, 0.92f);
        private static readonly Color ErrorBorder = new(0.90f, 0.30f, 0.25f, 0.8f);

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _sub = EventBus.Subscribe<BuildFailedEvent>(OnBuildFailed);
        }

        private void OnDisable()
        {
            _sub?.Dispose();
        }

        private void OnBuildFailed(BuildFailedEvent evt)
        {
            ShowToast(evt.Reason);
        }

        private void ShowToast(string reason)
        {
            if (_canvas == null) return;
            if (_currentToast != null) Destroy(_currentToast);

            _currentToast = new GameObject("BuildFailedToast");
            _currentToast.transform.SetParent(_canvas.transform, false);
            _currentToast.transform.SetAsLastSibling();

            var rect = _currentToast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.52f);
            rect.anchorMax = new Vector2(0.90f, 0.58f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _currentToast.AddComponent<Image>();
            bg.color = ErrorBg;
            bg.raycastTarget = false;

            var outline = _currentToast.AddComponent<Outline>();
            outline.effectColor = ErrorBorder;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            // Warning icon + message
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(_currentToast.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);
            var text = textGO.AddComponent<Text>();
            text.text = $"\u26A0 {reason}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(0.6f, -0.6f);

            var cg = _currentToast.AddComponent<CanvasGroup>();
            StartCoroutine(AnimateToast(_currentToast, rect, cg));
        }

        private System.Collections.IEnumerator AnimateToast(GameObject go, RectTransform rect, CanvasGroup cg)
        {
            // Slide in from right
            float slideDistance = 30f;
            Vector2 startOffset = new Vector2(slideDistance, 0);
            Vector2 baseMin = rect.offsetMin;
            Vector2 baseMax = rect.offsetMax;

            float slideIn = 0.2f;
            float elapsed = 0f;
            while (elapsed < slideIn)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideIn;
                float ease = t * (2f - t);
                rect.offsetMin = baseMin + startOffset * (1f - ease);
                rect.offsetMax = baseMax + startOffset * (1f - ease);
                cg.alpha = ease;
                yield return null;
            }
            rect.offsetMin = baseMin;
            rect.offsetMax = baseMax;
            cg.alpha = 1f;

            // Brief shake
            for (int i = 0; i < 3; i++)
            {
                rect.offsetMin = baseMin + new Vector2(3f, 0);
                rect.offsetMax = baseMax + new Vector2(3f, 0);
                yield return null;
                yield return null;
                rect.offsetMin = baseMin + new Vector2(-3f, 0);
                rect.offsetMax = baseMax + new Vector2(-3f, 0);
                yield return null;
                yield return null;
            }
            rect.offsetMin = baseMin;
            rect.offsetMax = baseMax;

            // Hold
            yield return new WaitForSeconds(1.5f);

            // Fade out
            float fadeOut = 0.3f;
            elapsed = 0f;
            while (elapsed < fadeOut)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeOut);
                yield return null;
            }
            Destroy(go);
        }
    }
}
