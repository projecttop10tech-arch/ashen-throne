using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Creates and manages construction progress overlays on buildings during upgrades.
    /// P&C-style: shows timer countdown + progress bar directly on the building sprite.
    /// Also shows "upgrade available" indicators on idle buildings.
    /// </summary>
    public class ConstructionOverlayManager : MonoBehaviour
    {
        private CityGridView _gridView;
        private BuildingManager _buildingManager;

        private readonly Dictionary<string, ConstructionOverlay> _overlays = new();

        private EventSubscription _upgradeStartedSub;
        private EventSubscription _upgradeCompletedSub;

        private static readonly Color ProgressBgColor = new(0f, 0f, 0f, 0.75f);
        private static readonly Color ProgressFillColor = new(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color TimerTextColor = new(0.98f, 0.96f, 0.92f, 1f);
        private static readonly Color UpgradeArrowColor = new(0.30f, 0.85f, 0.40f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void Start()
        {
            _gridView = GetComponent<CityGridView>();
            if (_gridView == null)
                _gridView = FindFirstObjectByType<CityGridView>();
        }

        private EventSubscription _upgradeCancelledSub;

        private void OnEnable()
        {
            _upgradeStartedSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _upgradeCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _upgradeCancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(OnUpgradeCancelled);
        }

        private void OnDisable()
        {
            _upgradeStartedSub?.Dispose();
            _upgradeCompletedSub?.Dispose();
            _upgradeCancelledSub?.Dispose();
        }

        private void Update()
        {
            // Update active construction overlays
            if (_buildingManager == null) return;

            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (_overlays.TryGetValue(entry.PlacedId, out var overlay))
                {
                    float total = overlay.TotalSeconds;
                    float remaining = entry.RemainingSeconds;
                    float progress = total > 0 ? 1f - (remaining / total) : 1f;

                    overlay.ProgressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                    // Fill turns gold near completion
                    overlay.ProgressFill.GetComponent<Image>().color = progress > 0.9f
                        ? new Color(0.83f, 0.66f, 0.26f, 1f) : ProgressFillColor;
                    overlay.TimerText.text = FormatTime(Mathf.CeilToInt(remaining));
                    // Timer turns red at < 10 seconds
                    overlay.TimerText.color = remaining < 10f
                        ? new Color(1f, 0.3f, 0.3f, 1f) : TimerTextColor;
                }
            }
        }

        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            // Find the building visual
            if (_gridView == null) return;

            CityBuildingPlacement placement = null;
            foreach (var p in _gridView.GetPlacements())
            {
                if (p.InstanceId == evt.PlacedId)
                {
                    placement = p;
                    break;
                }
            }

            if (placement?.VisualGO == null) return;

            // Remove any existing overlay
            RemoveOverlay(evt.PlacedId);

            // Create construction overlay on the building
            var overlay = CreateConstructionOverlay(placement.VisualGO, evt.BuildTimeSeconds);
            _overlays[evt.PlacedId] = overlay;
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            // Play completion celebration before removing overlay
            if (_overlays.TryGetValue(evt.PlacedId, out var overlay) && overlay.Root != null)
            {
                PlayCompletionCelebration(overlay.Root.transform.parent);
            }
            RemoveOverlay(evt.PlacedId);
        }

        private void OnUpgradeCancelled(BuildingUpgradeCancelledEvent evt)
        {
            RemoveOverlay(evt.PlacedId);
        }

        private void RemoveOverlay(string placedId)
        {
            if (_overlays.TryGetValue(placedId, out var overlay))
            {
                if (overlay.Root != null)
                    Destroy(overlay.Root);
                _overlays.Remove(placedId);
            }
        }

        private ConstructionOverlay CreateConstructionOverlay(GameObject buildingGO, float totalSeconds)
        {
            // Root container positioned at bottom of building
            var root = new GameObject("ConstructionOverlay");
            root.transform.SetParent(buildingGO.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.05f, 0.02f);
            rootRect.anchorMax = new Vector2(0.95f, 0.18f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Progress bar background
            var progBg = new GameObject("ProgressBg");
            progBg.transform.SetParent(root.transform, false);
            var bgRect = progBg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = progBg.AddComponent<Image>();
            bgImg.color = ProgressBgColor;
            bgImg.raycastTarget = false;

            // Progress fill
            var progFill = new GameObject("ProgressFill");
            progFill.transform.SetParent(progBg.transform, false);
            var fillRect = progFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.01f, 1f); // starts at 1%
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = progFill.AddComponent<Image>();
            fillImg.color = ProgressFillColor;
            fillImg.raycastTarget = false;

            // Timer text
            var timerGO = new GameObject("TimerText");
            timerGO.transform.SetParent(root.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = FormatTime(Mathf.CeilToInt(totalSeconds));
            timerText.fontSize = 11;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = TimerTextColor;
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontStyle = FontStyle.Bold;
            timerText.raycastTarget = false;
            var timerShadow = timerGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.9f);
            timerShadow.effectDistance = new Vector2(1f, -1f);

            // Construction icon (hammer) above the building
            var hammerGO = new GameObject("ConstructionIcon");
            hammerGO.transform.SetParent(buildingGO.transform, false);
            var hammerRect = hammerGO.AddComponent<RectTransform>();
            hammerRect.anchorMin = new Vector2(0.35f, 0.85f);
            hammerRect.anchorMax = new Vector2(0.65f, 1.10f);
            hammerRect.offsetMin = Vector2.zero;
            hammerRect.offsetMax = Vector2.zero;
            var hammerImg = hammerGO.AddComponent<Image>();
            hammerImg.raycastTarget = false;
            // Try to load the build icon
            var buildIconSpr = Resources.Load<Sprite>("UI/Production/icon_build");
            if (buildIconSpr == null)
            {
                #if UNITY_EDITOR
                buildIconSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/icon_build.png");
                #endif
            }
            if (buildIconSpr != null)
            {
                hammerImg.sprite = buildIconSpr;
                hammerImg.preserveAspect = true;
                hammerImg.color = Color.white;
            }
            else
            {
                hammerImg.color = UpgradeArrowColor;
            }

            return new ConstructionOverlay
            {
                Root = root,
                ProgressFill = fillRect,
                TimerText = timerText,
                HammerIcon = hammerGO,
                TotalSeconds = totalSeconds
            };
        }

        /// <summary>
        /// P&C-style golden glow burst when upgrade completes.
        /// Creates a temporary expanding glow that fades out.
        /// </summary>
        private void PlayCompletionCelebration(Transform buildingTransform)
        {
            if (buildingTransform == null) return;

            var glow = new GameObject("CompletionGlow");
            glow.transform.SetParent(buildingTransform, false);
            var rect = glow.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, -0.15f);
            rect.anchorMax = new Vector2(1.15f, 1.15f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = glow.AddComponent<Image>();
            // Try to load radial gradient for soft glow
            var gradientSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (gradientSpr == null)
                gradientSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (gradientSpr != null)
                img.sprite = gradientSpr;
            img.color = new Color(0.83f, 0.66f, 0.26f, 0.8f); // Gold glow
            img.raycastTarget = false;

            // Animate: scale up from 0.5 to 1.2 and fade out over 1 second
            StartCoroutine(AnimateCelebration(glow, rect));
        }

        private System.Collections.IEnumerator AnimateCelebration(GameObject glow, RectTransform rect)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            var img = glow.GetComponent<Image>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Scale: 0.5 → 1.3
                float scale = Mathf.Lerp(0.5f, 1.3f, t);
                rect.localScale = new Vector3(scale, scale, 1f);

                // Fade: full → 0
                float alpha = Mathf.Lerp(0.8f, 0f, t * t); // Quadratic ease-out
                if (img != null)
                    img.color = new Color(0.83f, 0.66f, 0.26f, alpha);

                yield return null;
            }

            Destroy(glow);
        }

        private static string FormatTime(int seconds)
        {
            if (seconds <= 0) return "Done!";
            int h = seconds / 3600;
            int m = (seconds % 3600) / 60;
            int s = seconds % 60;
            if (h > 0) return $"{h}:{m:D2}:{s:D2}";
            return $"{m}:{s:D2}";
        }

        private class ConstructionOverlay
        {
            public GameObject Root;
            public RectTransform ProgressFill;
            public Text TimerText;
            public GameObject HammerIcon;
            public float TotalSeconds;
        }
    }
}
