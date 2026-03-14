using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality busy indicators on buildings: glowing research progress bars with shimmer sweep,
    /// pulsing activity icons, animated military badges with faction-colored glow rings.
    /// </summary>
    public class BuildingBusyIndicator : MonoBehaviour
    {
        private CityGridView _gridView;
        private ResearchManager _researchManager;

        private readonly Dictionary<string, BusyIndicatorUI> _indicators = new();

        private static readonly HashSet<string> ResearchBuildings = new()
        {
            "library", "academy", "laboratory", "observatory", "archive"
        };

        private static readonly Dictionary<string, (string Label, Color Tint)> MilitaryBuildings = new()
        {
            { "barracks", ("\u2694", new Color(0.90f, 0.35f, 0.28f, 1f)) },
            { "training_ground", ("\u2694", new Color(0.90f, 0.58f, 0.22f, 1f)) },
            { "armory", ("\u2748", new Color(0.65f, 0.70f, 0.80f, 1f)) },
        };

        private static readonly Color ProgressBg = new(0.04f, 0.03f, 0.08f, 0.88f);
        private static readonly Color ProgressFill = new(0.30f, 0.78f, 1f, 1f);
        private static readonly Color ProgressGlow = new(0.40f, 0.85f, 1f, 0.30f);
        private static readonly Color ResearchGold = new(0.92f, 0.78f, 0.28f, 1f);

        private readonly Dictionary<string, MilitaryBadgeUI> _militaryBadges = new();
        private float _militaryRefreshTimer;
        private const float MilitaryRefreshInterval = 3f;

        private void Start()
        {
            _gridView = FindFirstObjectByType<CityGridView>();
            ServiceLocator.TryGet(out _researchManager);
        }

        private void Update()
        {
            if (_gridView == null) return;
            if (_researchManager != null)
                UpdateResearchIndicators();

            // Animate active research indicators
            AnimateResearchBars();

            // Animate military badges
            AnimateMilitaryBadges();

            _militaryRefreshTimer += Time.deltaTime;
            if (_militaryRefreshTimer >= MilitaryRefreshInterval)
            {
                _militaryRefreshTimer = 0f;
                UpdateMilitaryBadges();
            }
        }

        private void AnimateResearchBars()
        {
            foreach (var kvp in _indicators)
            {
                var ui = kvp.Value;
                if (ui.Root == null) continue;

                // Shimmer sweep across fill bar
                if (ui.ShimmerImage != null)
                {
                    float shimmerT = Mathf.Repeat(Time.time * 0.5f, 1.6f) - 0.3f;
                    var shimmerRect = ui.ShimmerImage.GetComponent<RectTransform>();
                    shimmerRect.anchorMin = new Vector2(Mathf.Clamp01(shimmerT - 0.10f), 0f);
                    shimmerRect.anchorMax = new Vector2(Mathf.Clamp01(shimmerT + 0.10f), 1f);
                    float sa = 0.20f + 0.12f * Mathf.Sin(Time.time * 3.5f);
                    ui.ShimmerImage.color = new Color(1f, 1f, 1f, sa);
                }

                // Fill glow pulse
                if (ui.GlowImage != null)
                {
                    float ga = 0.20f + 0.12f * Mathf.Sin(Time.time * 2.2f);
                    ui.GlowImage.color = new Color(ProgressGlow.r, ProgressGlow.g, ProgressGlow.b, ga);
                }

                // Icon pulse (scale + alpha)
                if (ui.IconText != null)
                {
                    float iconPulse = Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f;
                    float iconScale = Mathf.Lerp(0.9f, 1.15f, iconPulse);
                    ui.IconText.transform.localScale = Vector3.one * iconScale;
                    var c = ui.IconText.color;
                    c.a = Mathf.Lerp(0.70f, 1f, iconPulse);
                    ui.IconText.color = c;
                }
            }
        }

        private void AnimateMilitaryBadges()
        {
            foreach (var kvp in _militaryBadges)
            {
                var ui = kvp.Value;
                if (ui.Root == null) continue;

                float phase = Time.time + ui.PhaseOffset;
                float pulse = Mathf.Sin(phase * 2.5f) * 0.5f + 0.5f;

                // Glow ring breathe
                if (ui.GlowRing != null)
                {
                    var c = ui.GlowRing.color;
                    c.a = Mathf.Lerp(0.10f, 0.35f, pulse);
                    ui.GlowRing.color = c;
                }

                // Subtle scale pulse
                float s = Mathf.Lerp(0.96f, 1.04f, pulse);
                ui.Root.transform.localScale = Vector3.one * s;

                // Icon shimmer
                if (ui.IconText != null)
                {
                    var ic = ui.IconText.color;
                    ic.a = Mathf.Lerp(0.75f, 1f, pulse);
                    ui.IconText.color = ic;
                }
            }
        }

        private void UpdateResearchIndicators()
        {
            var queue = _researchManager.ResearchQueue;
            bool researchActive = queue != null && queue.Count > 0;

            var placements = _gridView.GetPlacements();
            var activeIds = new HashSet<string>();

            foreach (var p in placements)
            {
                if (!ResearchBuildings.Contains(p.BuildingId)) continue;

                if (researchActive)
                {
                    activeIds.Add(p.InstanceId);
                    EnsureIndicator(p, queue[0]);
                    break;
                }
            }

            var stale = new List<string>();
            foreach (var kvp in _indicators)
            {
                if (!activeIds.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }
            foreach (var key in stale)
                RemoveIndicator(key);
        }

        private void EnsureIndicator(CityBuildingPlacement placement, ResearchQueueEntry entry)
        {
            if (placement.VisualGO == null) return;

            if (_indicators.TryGetValue(placement.InstanceId, out var existing))
            {
                if (existing.Root != null)
                {
                    UpdateIndicator(existing, entry);
                    return;
                }
                _indicators.Remove(placement.InstanceId);
            }

            CreateIndicator(placement, entry);
        }

        private void CreateIndicator(CityBuildingPlacement placement, ResearchQueueEntry entry)
        {
            var go = new GameObject($"BusyIndicator_{placement.InstanceId}");
            go.transform.SetParent(placement.VisualGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.03f, -0.06f);
            rect.anchorMax = new Vector2(0.97f, 0.09f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Ornate background
            var bg = go.AddComponent<Image>();
            bg.color = ProgressBg;
            bg.raycastTarget = false;

            // Triple border: glow → gold → inner
            var glowBorder = new GameObject("GlowBorder");
            glowBorder.transform.SetParent(go.transform, false);
            var gbRect = glowBorder.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = glowBorder.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            var gbOutline = glowBorder.AddComponent<Outline>();
            gbOutline.effectColor = new Color(0.85f, 0.68f, 0.25f, 0.25f);
            gbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var goldBorder = new GameObject("GoldBorder");
            goldBorder.transform.SetParent(go.transform, false);
            var goRect = goldBorder.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero; goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero; goRect.offsetMax = Vector2.zero;
            var goImg = goldBorder.AddComponent<Image>();
            goImg.color = new Color(0, 0, 0, 0); goImg.raycastTarget = false;
            var goOutline = goldBorder.AddComponent<Outline>();
            goOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.60f);
            goOutline.effectDistance = new Vector2(0.8f, -0.8f);

            // Fill bar
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = new Vector2(1, 1);
            fillRect.offsetMax = new Vector2(-1, -1);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = ProgressFill;
            fillImg.raycastTarget = false;

            // Fill glow (pulsing aura behind bar)
            var fillGlow = new GameObject("FillGlow");
            fillGlow.transform.SetParent(go.transform, false);
            var fgRect = fillGlow.AddComponent<RectTransform>();
            fgRect.anchorMin = new Vector2(0f, -0.4f);
            fgRect.anchorMax = new Vector2(1f, 1.4f);
            fgRect.offsetMin = Vector2.zero;
            fgRect.offsetMax = Vector2.zero;
            var fgImg = fillGlow.AddComponent<Image>();
            fgImg.color = ProgressGlow;
            fgImg.raycastTarget = false;

            // Shimmer sweep
            var shimmer = new GameObject("Shimmer");
            shimmer.transform.SetParent(go.transform, false);
            var shRect = shimmer.AddComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0f, 0f);
            shRect.anchorMax = new Vector2(0.10f, 1f);
            shRect.offsetMin = new Vector2(1, 1);
            shRect.offsetMax = new Vector2(-1, -1);
            var shImg = shimmer.AddComponent<Image>();
            shImg.color = new Color(1f, 1f, 1f, 0.20f);
            shImg.raycastTarget = false;

            // Activity icon with glow (pulsing)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(-0.02f, -0.3f);
            iconRect.anchorMax = new Vector2(0.16f, 1.3f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = "\u2697";
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 10;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = ResearchGold;
            iconText.raycastTarget = false;
            var iconOutline = iconGO.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0, 0, 0, 0.8f);
            iconOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Timer text with outline
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(go.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.16f, 0f);
            timerRect.anchorMax = Vector2.one;
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 8;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = Color.white;
            timerText.raycastTarget = false;
            var timerOutline = timerGO.AddComponent<Outline>();
            timerOutline.effectColor = new Color(0, 0, 0, 0.8f);
            timerOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var ui = new BusyIndicatorUI
            {
                Root = go,
                FillRect = fillRect,
                TimerText = timerText,
                IconText = iconText,
                ShimmerImage = shImg,
                GlowImage = fgImg
            };
            _indicators[placement.InstanceId] = ui;
            UpdateIndicator(ui, entry);
        }

        private void UpdateIndicator(BusyIndicatorUI ui, ResearchQueueEntry entry)
        {
            if (ui.Root == null) return;

            if (ui.FillRect != null)
            {
                float pulse = 0.3f + 0.15f * Mathf.Sin(Time.time * 2f);
                ui.FillRect.anchorMax = new Vector2(
                    Mathf.Clamp01(1f - (entry.RemainingSeconds / 3600f) + pulse), 1f);
            }

            if (ui.TimerText != null)
                ui.TimerText.text = FormatTime(entry.RemainingSeconds);
        }

        private void RemoveIndicator(string instanceId)
        {
            if (_indicators.TryGetValue(instanceId, out var ui))
            {
                if (ui.Root != null) Destroy(ui.Root);
                _indicators.Remove(instanceId);
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds >= 3600f)
            {
                int h = (int)(seconds / 3600f);
                int m = (int)((seconds % 3600f) / 60f);
                return $"{h}h{m:D2}m";
            }
            if (seconds >= 60f)
            {
                int m = (int)(seconds / 60f);
                int s = (int)(seconds % 60f);
                return $"{m}:{s:D2}";
            }
            return $"{(int)seconds}s";
        }

        // ==================================================================
        // P&C: Military building troop capacity badges
        // ==================================================================

        private void UpdateMilitaryBadges()
        {
            if (_gridView == null) return;
            var placements = _gridView.GetPlacements();
            var activeMilIds = new HashSet<string>();

            foreach (var p in placements)
            {
                if (!MilitaryBuildings.TryGetValue(p.BuildingId, out var info)) continue;
                activeMilIds.Add(p.InstanceId);
                EnsureMilitaryBadge(p, info);
            }

            var stale = new List<string>();
            foreach (var kvp in _militaryBadges)
            {
                if (!activeMilIds.Contains(kvp.Key))
                    stale.Add(kvp.Key);
            }
            foreach (var key in stale)
            {
                if (_militaryBadges.TryGetValue(key, out var ui))
                {
                    if (ui.Root != null) Destroy(ui.Root);
                    _militaryBadges.Remove(key);
                }
            }
        }

        private void EnsureMilitaryBadge(CityBuildingPlacement placement, (string Label, Color Tint) info)
        {
            if (placement.VisualGO == null) return;

            if (_militaryBadges.TryGetValue(placement.InstanceId, out var existing))
            {
                if (existing.Root != null)
                {
                    if (existing.CapText != null)
                    {
                        int capacity = placement.Tier * 500;
                        existing.CapText.text = $"{info.Label}{capacity}";
                    }
                    return;
                }
                _militaryBadges.Remove(placement.InstanceId);
            }

            CreateMilitaryBadge(placement, info);
        }

        private void CreateMilitaryBadge(CityBuildingPlacement placement, (string Label, Color Tint) info)
        {
            var go = new GameObject($"MilBadge_{placement.InstanceId}");
            go.transform.SetParent(placement.VisualGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, -0.09f);
            rect.anchorMax = new Vector2(0.92f, 0.06f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Ornate background
            var bg = go.AddComponent<Image>();
            bg.color = new Color(info.Tint.r * 0.12f, info.Tint.g * 0.12f, info.Tint.b * 0.12f, 0.88f);
            bg.raycastTarget = false;

            // Triple border: glow → faction color → inner
            var glowBorder = new GameObject("GlowBorder");
            glowBorder.transform.SetParent(go.transform, false);
            var gbRect = glowBorder.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = glowBorder.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            var gbOutline = glowBorder.AddComponent<Outline>();
            gbOutline.effectColor = new Color(info.Tint.r, info.Tint.g, info.Tint.b, 0.20f);
            gbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Faction color border
            var factionBorder = new GameObject("FactionBorder");
            factionBorder.transform.SetParent(go.transform, false);
            var fbRect = factionBorder.AddComponent<RectTransform>();
            fbRect.anchorMin = Vector2.zero; fbRect.anchorMax = Vector2.one;
            fbRect.offsetMin = Vector2.zero; fbRect.offsetMax = Vector2.zero;
            var fbImg = factionBorder.AddComponent<Image>();
            fbImg.color = new Color(0, 0, 0, 0); fbImg.raycastTarget = false;
            var fbOutline = factionBorder.AddComponent<Outline>();
            fbOutline.effectColor = new Color(info.Tint.r, info.Tint.g, info.Tint.b, 0.65f);
            fbOutline.effectDistance = new Vector2(0.7f, -0.7f);

            // Glow ring (animated)
            Image glowRing = null;
            var glowRingGO = new GameObject("GlowRing");
            glowRingGO.transform.SetParent(go.transform, false);
            var grRect = glowRingGO.AddComponent<RectTransform>();
            grRect.anchorMin = new Vector2(-0.05f, -0.5f);
            grRect.anchorMax = new Vector2(1.05f, 1.5f);
            grRect.offsetMin = Vector2.zero;
            grRect.offsetMax = Vector2.zero;
            glowRing = glowRingGO.AddComponent<Image>();
            glowRing.color = new Color(info.Tint.r, info.Tint.g, info.Tint.b, 0.15f);
            glowRing.raycastTarget = false;
            var radial = Resources.Load<Sprite>("UI/Production/radial_gradient");
            if (radial != null) { glowRing.sprite = radial; glowRing.type = Image.Type.Simple; }

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(go.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.45f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero;
            glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.05f);
            glImg.raycastTarget = false;

            // Icon + capacity text
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0.20f, 1f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = info.Label;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 10;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = info.Tint;
            iconText.raycastTarget = false;
            var iconOutline = iconGO.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0, 0, 0, 0.8f);
            iconOutline.effectDistance = new Vector2(0.5f, -0.5f);

            int capacity = placement.Tier * 500;
            var textGO = new GameObject("CapText");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.18f, 0f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = $"{capacity}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = info.Tint;
            text.raycastTarget = false;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.4f, -0.4f);

            _militaryBadges[placement.InstanceId] = new MilitaryBadgeUI
            {
                Root = go,
                CapText = text,
                IconText = iconText,
                GlowRing = glowRing,
                PhaseOffset = Random.Range(0f, Mathf.PI * 2f)
            };
        }

        // === Data structures ===

        private class BusyIndicatorUI
        {
            public GameObject Root;
            public RectTransform FillRect;
            public Text TimerText;
            public Text IconText;
            public Image ShimmerImage;
            public Image GlowImage;
        }

        private class MilitaryBadgeUI
        {
            public GameObject Root;
            public Text CapText;
            public Text IconText;
            public Image GlowRing;
            public float PhaseOffset;
        }
    }
}
