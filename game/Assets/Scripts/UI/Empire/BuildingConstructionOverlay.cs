using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style construction progress overlay on buildings during upgrades.
    /// Shows a semi-transparent construction tint, progress bar, and timer
    /// directly on the building sprite. On completion, plays a level-up burst.
    /// </summary>
    public class BuildingConstructionOverlay : MonoBehaviour
    {
        private BuildingManager _buildingManager;
        private CityGridView _gridView;
        private EventSubscription _startSub;
        private EventSubscription _completeSub;
        private EventSubscription _cancelSub;

        private readonly Dictionary<string, OverlayState> _overlays = new();

        private static readonly Color ConstructionTint = new(0.15f, 0.10f, 0.05f, 0.35f);
        private static readonly Color BarBgColor = new(0.05f, 0.03f, 0.08f, 0.85f);
        private static readonly Color BarFillColor = new(0.20f, 0.78f, 0.35f, 1f);
        private static readonly Color BarBorderColor = new(0.78f, 0.62f, 0.22f, 0.7f);
        private static readonly Color TimerTextColor = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color HammerColor = new(0.83f, 0.66f, 0.26f, 0.9f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void Start()
        {
            _gridView = FindFirstObjectByType<CityGridView>();
            // Check for any builds already in progress
            if (_buildingManager != null && _gridView != null)
            {
                foreach (var entry in _buildingManager.BuildQueue)
                    TryCreateOverlay(entry.PlacedId, entry);
            }
        }

        private void OnEnable()
        {
            _startSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _completeSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompleted);
            _cancelSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(OnUpgradeCancelled);
        }

        private void OnDisable()
        {
            _startSub?.Dispose();
            _completeSub?.Dispose();
            _cancelSub?.Dispose();
        }

        private void Update()
        {
            if (_buildingManager == null) return;

            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (!_overlays.TryGetValue(entry.PlacedId, out var state)) continue;
                if (state.Root == null) continue;

                float remaining = entry.RemainingSeconds;
                float progress = state.TotalSeconds > 0 ? 1f - (remaining / state.TotalSeconds) : 1f;

                // Update progress bar
                if (state.FillRect != null)
                    state.FillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

                // Update timer text
                if (state.TimerText != null)
                    state.TimerText.text = FormatTime(Mathf.CeilToInt(remaining));

                // Animate hammer bob
                if (state.HammerText != null)
                {
                    float bob = Mathf.Sin(Time.time * 3f + state.PhaseOffset) * 2f;
                    state.HammerText.transform.localRotation = Quaternion.Euler(0, 0, bob * 8f);
                }

                // Flash bar when < 10s
                if (state.FillImg != null)
                {
                    if (remaining < 10f)
                    {
                        float flash = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                        state.FillImg.color = Color.Lerp(BarFillColor, new Color(1f, 0.9f, 0.2f, 1f), flash);
                    }
                    else
                    {
                        state.FillImg.color = BarFillColor;
                    }
                }
            }
        }

        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            if (_buildingManager == null) return;
            foreach (var entry in _buildingManager.BuildQueue)
            {
                if (entry.PlacedId == evt.PlacedId)
                {
                    TryCreateOverlay(evt.PlacedId, entry);
                    break;
                }
            }
        }

        private void OnUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            if (_overlays.TryGetValue(evt.PlacedId, out var state))
            {
                // Play level-up burst before destroying
                if (state.Root != null)
                    SpawnLevelUpBurst(state.Root.transform.parent);
                DestroyOverlay(evt.PlacedId);
            }
        }

        private void OnUpgradeCancelled(BuildingUpgradeCancelledEvent evt)
        {
            DestroyOverlay(evt.PlacedId);
        }

        private void TryCreateOverlay(string placedId, BuildQueueEntry entry)
        {
            if (_gridView == null || _overlays.ContainsKey(placedId)) return;

            GameObject buildingGO = null;
            foreach (var p in _gridView.GetPlacements())
            {
                if (p.InstanceId == placedId && p.VisualGO != null)
                {
                    buildingGO = p.VisualGO;
                    break;
                }
            }
            if (buildingGO == null) return;

            float elapsed = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds;
            float totalSeconds = elapsed + entry.RemainingSeconds;

            var root = new GameObject("ConstructionOverlay");
            root.transform.SetParent(buildingGO.transform, false);

            // Semi-transparent construction tint over building
            var tintRect = root.AddComponent<RectTransform>();
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = Vector2.zero;
            tintRect.offsetMax = Vector2.zero;
            var tintImg = root.AddComponent<Image>();
            tintImg.color = ConstructionTint;
            tintImg.raycastTarget = false;

            // Progress bar background (bottom 12% of building)
            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(root.transform, false);
            var barBgRect = barBg.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.02f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.10f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = BarBgColor;
            barBgImg.raycastTarget = false;
            var barBorder = barBg.AddComponent<Outline>();
            barBorder.effectColor = BarBorderColor;
            barBorder.effectDistance = new Vector2(0.5f, -0.5f);

            // Progress bar fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            float initialProgress = totalSeconds > 0 ? elapsed / totalSeconds : 0f;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(initialProgress), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = BarFillColor;
            fillImg.raycastTarget = false;

            // Timer text (above the progress bar)
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(root.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.05f, 0.10f);
            timerRect.anchorMax = new Vector2(0.95f, 0.22f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = FormatTime(Mathf.CeilToInt(entry.RemainingSeconds));
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 8;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = TimerTextColor;
            timerText.raycastTarget = false;
            var timerShadow = timerGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.9f);
            timerShadow.effectDistance = new Vector2(0.8f, -0.8f);

            // Hammer icon (animated, center-top of building)
            var hammerGO = new GameObject("Hammer");
            hammerGO.transform.SetParent(root.transform, false);
            var hammerRect = hammerGO.AddComponent<RectTransform>();
            hammerRect.anchorMin = new Vector2(0.35f, 0.72f);
            hammerRect.anchorMax = new Vector2(0.65f, 0.92f);
            hammerRect.offsetMin = Vector2.zero;
            hammerRect.offsetMax = Vector2.zero;
            var hammerText = hammerGO.AddComponent<Text>();
            hammerText.text = "\u2692"; // ⚒
            hammerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hammerText.fontSize = 14;
            hammerText.alignment = TextAnchor.MiddleCenter;
            hammerText.color = HammerColor;
            hammerText.raycastTarget = false;
            var hammerOutline = hammerGO.AddComponent<Outline>();
            hammerOutline.effectColor = new Color(0, 0, 0, 0.85f);
            hammerOutline.effectDistance = new Vector2(1f, -1f);

            _overlays[placedId] = new OverlayState
            {
                Root = root,
                FillRect = fillRect,
                FillImg = fillImg,
                TimerText = timerText,
                HammerText = hammerText,
                TotalSeconds = totalSeconds,
                PhaseOffset = Random.Range(0f, Mathf.PI * 2f)
            };
        }

        private void SpawnLevelUpBurst(Transform buildingParent)
        {
            if (buildingParent == null) return;

            // P&C: Brief golden "level up" flash + star burst
            var burstGO = new GameObject("LevelUpBurst");
            burstGO.transform.SetParent(buildingParent, false);

            var burstRect = burstGO.AddComponent<RectTransform>();
            burstRect.anchorMin = new Vector2(-0.1f, -0.1f);
            burstRect.anchorMax = new Vector2(1.1f, 1.1f);
            burstRect.offsetMin = Vector2.zero;
            burstRect.offsetMax = Vector2.zero;

            var burstImg = burstGO.AddComponent<Image>();
            burstImg.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            burstImg.raycastTarget = false;

            var radial = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (radial == null)
                radial = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (radial != null) burstImg.sprite = radial;

            var cg = burstGO.AddComponent<CanvasGroup>();

            // Stars text
            var starsGO = new GameObject("Stars");
            starsGO.transform.SetParent(burstGO.transform, false);
            var starsRect = starsGO.AddComponent<RectTransform>();
            starsRect.anchorMin = Vector2.zero;
            starsRect.anchorMax = Vector2.one;
            starsRect.offsetMin = Vector2.zero;
            starsRect.offsetMax = Vector2.zero;
            var starsText = starsGO.AddComponent<Text>();
            starsText.text = "\u2605 LEVEL UP \u2605";
            starsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            starsText.fontSize = 11;
            starsText.fontStyle = FontStyle.Bold;
            starsText.alignment = TextAnchor.MiddleCenter;
            starsText.color = new Color(1f, 0.95f, 0.7f, 1f);
            starsText.raycastTarget = false;
            var starsOutline = starsGO.AddComponent<Outline>();
            starsOutline.effectColor = new Color(0.6f, 0.3f, 0f, 0.9f);
            starsOutline.effectDistance = new Vector2(1f, -1f);

            // Auto-destroy with scale+fade animation
            StartCoroutine(AnimateBurst(burstGO, burstRect, cg));
        }

        private System.Collections.IEnumerator AnimateBurst(GameObject go, RectTransform rect, CanvasGroup cg)
        {
            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 peakScale = Vector3.one * 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.3f)
                {
                    // Expand quickly
                    float expand = t / 0.3f;
                    rect.localScale = Vector3.Lerp(startScale, peakScale, expand * expand);
                    cg.alpha = Mathf.Min(1f, expand * 3f);
                }
                else
                {
                    // Settle and fade
                    float fade = (t - 0.3f) / 0.7f;
                    rect.localScale = Vector3.Lerp(peakScale, Vector3.one * 1.1f, fade);
                    cg.alpha = 1f - fade;
                }
                yield return null;
            }
            Destroy(go);
        }

        private void DestroyOverlay(string placedId)
        {
            if (_overlays.TryGetValue(placedId, out var state))
            {
                if (state.Root != null)
                    Destroy(state.Root);
                _overlays.Remove(placedId);
            }
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

        private class OverlayState
        {
            public GameObject Root;
            public RectTransform FillRect;
            public Image FillImg;
            public Text TimerText;
            public Text HammerText;
            public float TotalSeconds;
            public float PhaseOffset;
        }
    }
}
