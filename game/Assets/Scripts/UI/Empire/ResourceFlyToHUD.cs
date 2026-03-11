using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style: when resources are collected, an icon flies from the building
    /// to the corresponding resource display in the HUD bar.
    /// </summary>
    public class ResourceFlyToHUD : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private Canvas _canvas;
        private EventSubscription _collectSub;

        // Cached HUD icon positions (found by name)
        private readonly Dictionary<ResourceType, RectTransform> _hudIcons = new();

        private static readonly Dictionary<ResourceType, string> IconNames = new()
        {
            { ResourceType.Grain, "GrainResIcon" },
            { ResourceType.Iron, "IronResIcon" },
            { ResourceType.Stone, "StoneResIcon" },
            { ResourceType.ArcaneEssence, "ArcaneResIcon" },
        };

        private static readonly Dictionary<ResourceType, Color> FlyColors = new()
        {
            { ResourceType.Stone, new Color(0.85f, 0.82f, 0.76f, 1f) },
            { ResourceType.Iron, new Color(0.78f, 0.80f, 0.90f, 1f) },
            { ResourceType.Grain, new Color(1f, 0.92f, 0.45f, 1f) },
            { ResourceType.ArcaneEssence, new Color(0.80f, 0.55f, 1f, 1f) },
        };

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasRect = _canvas.GetComponent<RectTransform>();
            CacheHUDIcons();
        }

        private void OnEnable()
        {
            _collectSub = EventBus.Subscribe<ResourceCollectedEvent>(OnResourceCollected);
        }

        private void OnDisable()
        {
            _collectSub?.Dispose();
        }

        // P&C: Batch collection summary toast
        private readonly Dictionary<ResourceType, long> _batchTotals = new();
        private float _batchTimer;
        private const float BatchWindow = 0.5f; // Aggregate collections within 500ms
        private bool _batchActive;

        private void Update()
        {
            if (!_batchActive) return;
            _batchTimer -= Time.deltaTime;
            if (_batchTimer <= 0f)
            {
                ShowBatchSummaryToast();
                _batchActive = false;
            }
        }

        private void OnResourceCollected(ResourceCollectedEvent evt)
        {
            // P&C: Accumulate into batch for summary toast
            if (!_batchTotals.ContainsKey(evt.Type))
                _batchTotals[evt.Type] = 0;
            _batchTotals[evt.Type] += evt.Amount;
            _batchTimer = BatchWindow;
            _batchActive = true;

            if (!_hudIcons.TryGetValue(evt.Type, out var targetIcon)) return;
            if (_canvasRect == null) return;

            // Find the building visual to get start position
            Vector2 startPos = Vector2.zero;
            var gridView = FindFirstObjectByType<CityGridView>();
            if (gridView != null)
            {
                foreach (var p in gridView.GetPlacements())
                {
                    if (p.InstanceId == evt.BuildingInstanceId && p.VisualGO != null)
                    {
                        var buildingRect = p.VisualGO.GetComponent<RectTransform>();
                        if (buildingRect != null)
                            startPos = GetScreenPosition(buildingRect);
                        break;
                    }
                }
            }

            Vector2 endPos = GetScreenPosition(targetIcon);
            SpawnFlyIcon(evt.Type, startPos, endPos);
        }

        private void SpawnFlyIcon(ResourceType type, Vector2 startPos, Vector2 endPos)
        {
            var flyGO = new GameObject($"FlyIcon_{type}");
            flyGO.transform.SetParent(_canvasRect, false);
            // Ensure it renders on top
            flyGO.transform.SetAsLastSibling();

            var rect = flyGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(28, 28);
            rect.anchoredPosition = startPos;

            var img = flyGO.AddComponent<Image>();
            img.raycastTarget = false;
            FlyColors.TryGetValue(type, out var color);
            img.color = color;

            // Try to load resource icon sprite
            string iconName = type switch
            {
                ResourceType.Stone => "icon_stone",
                ResourceType.Iron => "icon_iron",
                ResourceType.Grain => "icon_grain",
                ResourceType.ArcaneEssence => "icon_arcane",
                _ => null
            };
            if (iconName != null)
            {
                var spr = Resources.Load<Sprite>($"UI/Production/{iconName}");
                #if UNITY_EDITOR
                if (spr == null)
                    spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Production/{iconName}.png");
                #endif
                if (spr != null)
                {
                    img.sprite = spr;
                    img.preserveAspect = true;
                    img.color = Color.white;
                }
            }

            // Add glow trail
            var cg = flyGO.AddComponent<CanvasGroup>();

            StartCoroutine(AnimateFly(flyGO, rect, cg, startPos, endPos));
        }

        private IEnumerator AnimateFly(GameObject go, RectTransform rect, CanvasGroup cg, Vector2 start, Vector2 end)
        {
            float duration = 0.6f;
            float elapsed = 0f;

            // Arc control point (higher than midpoint for parabolic path)
            Vector2 mid = (start + end) * 0.5f + Vector2.up * 80f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = t * t * (3f - 2f * t); // Smoothstep

                // Quadratic bezier curve
                Vector2 a = Vector2.Lerp(start, mid, ease);
                Vector2 b = Vector2.Lerp(mid, end, ease);
                rect.anchoredPosition = Vector2.Lerp(a, b, ease);

                // Scale: start big, shrink to normal
                float scale = Mathf.Lerp(1.4f, 0.8f, ease);
                rect.localScale = Vector3.one * scale;

                // Fade in at start, stay visible, slight fade at end
                cg.alpha = t < 0.1f ? t / 0.1f : (t > 0.85f ? Mathf.Lerp(1f, 0.6f, (t - 0.85f) / 0.15f) : 1f);

                yield return null;
            }

            // Flash at destination
            rect.anchoredPosition = end;
            rect.localScale = Vector3.one * 1.5f;
            cg.alpha = 1f;

            float flashDuration = 0.15f;
            float flashElapsed = 0f;
            while (flashElapsed < flashDuration)
            {
                flashElapsed += Time.deltaTime;
                float ft = flashElapsed / flashDuration;
                rect.localScale = Vector3.one * Mathf.Lerp(1.5f, 0f, ft);
                cg.alpha = 1f - ft;
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>P&C: Show a summary toast for batched resource collections (e.g., "+500 Stone, +300 Iron").</summary>
        private void ShowBatchSummaryToast()
        {
            if (_batchTotals.Count == 0) return;
            if (_canvasRect == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var kvp in _batchTotals)
            {
                if (kvp.Value <= 0) continue;
                if (sb.Length > 0) sb.Append("  ");
                string color = kvp.Key switch
                {
                    ResourceType.Stone => "#D4C8B4",
                    ResourceType.Iron => "#C8CCE6",
                    ResourceType.Grain => "#FFE844",
                    ResourceType.ArcaneEssence => "#CC88FF",
                    _ => "#FFFFFF"
                };
                string name = kvp.Key switch
                {
                    ResourceType.ArcaneEssence => "Arcane",
                    _ => kvp.Key.ToString()
                };
                sb.Append($"<color={color}>+{FormatAmount(kvp.Value)} {name}</color>");
            }
            _batchTotals.Clear();

            if (sb.Length == 0) return;

            // Create toast GO at top-center
            var toastGO = new GameObject("CollectionToast");
            toastGO.transform.SetParent(_canvasRect, false);
            toastGO.transform.SetAsLastSibling();

            var rect = toastGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.65f);
            rect.anchorMax = new Vector2(0.85f, 0.72f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = toastGO.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.10f, 0.85f);
            bg.raycastTarget = false;

            var outline = toastGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toastGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = sb.ToString();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = true;

            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Auto-destroy after 2 seconds with fade
            var cg = toastGO.AddComponent<CanvasGroup>();
            StartCoroutine(FadeAndDestroy(toastGO, cg, 2f));
        }

        private IEnumerator FadeAndDestroy(GameObject go, CanvasGroup cg, float displayTime)
        {
            yield return new WaitForSeconds(displayTime - 0.4f);
            float fadeTime = 0.4f;
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }
            Destroy(go);
        }

        private static string FormatAmount(long amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:F1}M";
            if (amount >= 1_000) return $"{amount / 1_000f:F1}K";
            return amount.ToString();
        }

        private Vector2 GetScreenPosition(RectTransform target)
        {
            if (_canvasRect == null || target == null) return Vector2.zero;
            // Convert world position to local position in canvas
            Vector3 worldPos = target.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, worldPos),
                _canvas.worldCamera, out var localPos);
            return localPos;
        }

        private void CacheHUDIcons()
        {
            foreach (var kvp in IconNames)
            {
                var found = FindDeepChild(transform.root, kvp.Value);
                if (found != null)
                    _hudIcons[kvp.Key] = found.GetComponent<RectTransform>();
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeepChild(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
