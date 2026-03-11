using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style resource production summary popup.
    /// Shows breakdown of hourly production per resource type, per building.
    /// Triggered by tapping the resource bar.
    /// </summary>
    public class ResourceProductionSummary : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;
        private EventSubscription _resourceBarTapSub;
        private GameObject _popup;

        private readonly Dictionary<string, BuildingData> _dataCache = new();

        private static readonly Color PopupBg = new(0.06f, 0.04f, 0.10f, 0.95f);
        private static readonly Color PopupBorder = new(0.83f, 0.66f, 0.26f, 0.80f);
        private static readonly Color HeaderColor = new(0.83f, 0.66f, 0.26f, 1f);
        private static readonly Color RowEven = new(0.10f, 0.08f, 0.16f, 0.50f);
        private static readonly Color RowOdd = new(0.08f, 0.06f, 0.12f, 0.50f);

        private static readonly Dictionary<string, (string Label, Color Tint)> ResourceColors = new()
        {
            { "stone", ("Stone", new Color(0.75f, 0.68f, 0.55f, 1f)) },
            { "iron", ("Iron", new Color(0.70f, 0.75f, 0.85f, 1f)) },
            { "grain", ("Grain", new Color(0.95f, 0.85f, 0.30f, 1f)) },
            { "arcane", ("Arcane Essence", new Color(0.65f, 0.45f, 0.90f, 1f)) }
        };

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
            ServiceLocator.TryGet(out _resourceManager);
            CacheData();
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();

            // P&C: Make resource bar tappable — find ResourceBarBg and add a Button
            WireResourceBarTap();
        }

        private void WireResourceBarTap()
        {
            var barBg = FindDeepChild(transform.root, "ResourceBarBg");
            if (barBg == null)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    barBg = FindDeepChild(root.transform, "ResourceBarBg");
                    if (barBg != null) break;
                }
            }
            if (barBg == null) return;

            var img = barBg.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var btn = barBg.GetComponent<Button>();
            if (btn == null) btn = barBg.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => EventBus.Publish(new ResourceBarTappedEvent()));
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

        private void OnEnable()
        {
            _resourceBarTapSub = EventBus.Subscribe<ResourceBarTappedEvent>(_ => ShowPopup());
        }

        private void OnDisable()
        {
            _resourceBarTapSub?.Dispose();
        }

        private void CacheData()
        {
            var all = Resources.LoadAll<BuildingData>("");
            foreach (var d in all)
                if (d != null && !string.IsNullOrEmpty(d.buildingId))
                    _dataCache[d.buildingId] = d;
        }

        private void ShowPopup()
        {
            ClosePopup();
            if (_canvasRect == null) return;

            _popup = new GameObject("ProductionSummary");
            _popup.transform.SetParent(_canvasRect, false);
            _popup.transform.SetAsLastSibling();

            // Overlay
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(_popup.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.50f);
            overlay.AddComponent<Button>().onClick.AddListener(ClosePopup);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_popup.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.20f);
            panelRect.anchorMax = new Vector2(0.95f, 0.80f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PopupBg;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = PopupBorder;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            CreateText(panel.transform, "Title", "PRODUCTION SUMMARY", 15, FontStyle.Bold,
                HeaderColor, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // Close btn
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.88f);
            closeRect.anchorMax = new Vector2(0.98f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.60f, 0.20f, 0.20f, 0.8f);
            closeGO.AddComponent<Button>().onClick.AddListener(ClosePopup);
            CreateText(closeGO.transform, "X", "\u2716", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one);

            // Calculate production totals
            var totals = CalculateProduction();

            // Scrollable content area
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(panel.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.02f, 0.02f);
            contentRect.anchorMax = new Vector2(0.98f, 0.86f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int rowIdx = 0;
            foreach (var kvp in totals)
            {
                string resKey = kvp.Key;
                if (!ResourceColors.TryGetValue(resKey, out var resInfo)) continue;

                // Resource header
                var headerGO = new GameObject($"Header_{resKey}");
                headerGO.transform.SetParent(contentGO.transform, false);
                var headerLayout = headerGO.AddComponent<LayoutElement>();
                headerLayout.preferredHeight = 22;
                var headerBg = headerGO.AddComponent<Image>();
                headerBg.color = new Color(resInfo.Tint.r * 0.3f, resInfo.Tint.g * 0.3f, resInfo.Tint.b * 0.3f, 0.50f);
                headerBg.raycastTarget = false;
                CreateText(headerGO.transform, "Label",
                    $"{resInfo.Label}  —  Total: +{kvp.Value.Total:F0}/hr",
                    12, FontStyle.Bold, resInfo.Tint, new Vector2(0.03f, 0), Vector2.one);

                // Per-building breakdown
                foreach (var entry in kvp.Value.Buildings)
                {
                    var rowGO = new GameObject($"Row_{entry.BuildingId}");
                    rowGO.transform.SetParent(contentGO.transform, false);
                    var rowLayout = rowGO.AddComponent<LayoutElement>();
                    rowLayout.preferredHeight = 18;
                    var rowBg = rowGO.AddComponent<Image>();
                    rowBg.color = rowIdx % 2 == 0 ? RowEven : RowOdd;
                    rowBg.raycastTarget = false;

                    string name = FormatBuildingName(entry.BuildingId);
                    CreateText(rowGO.transform, "Name",
                        $"  {name} (Lv.{entry.Tier + 1}) x{entry.Count}",
                        10, FontStyle.Normal, new Color(0.75f, 0.70f, 0.62f, 0.9f),
                        new Vector2(0.03f, 0), new Vector2(0.65f, 1));
                    CreateText(rowGO.transform, "Rate",
                        $"+{entry.Rate:F0}/hr",
                        10, FontStyle.Bold, new Color(0.40f, 0.88f, 0.50f, 1f),
                        new Vector2(0.66f, 0), new Vector2(0.97f, 1));
                    rowIdx++;
                }
            }

            // Vault capacity summary at bottom
            if (_resourceManager != null)
            {
                var vaultGO = new GameObject("VaultSummary");
                vaultGO.transform.SetParent(contentGO.transform, false);
                var vaultLayout = vaultGO.AddComponent<LayoutElement>();
                vaultLayout.preferredHeight = 24;
                var vaultBg = vaultGO.AddComponent<Image>();
                vaultBg.color = new Color(0.12f, 0.10f, 0.18f, 0.60f);
                vaultBg.raycastTarget = false;
                string vaultText = $"Vault: {FormatNum(_resourceManager.Stone)}/{FormatNum(_resourceManager.MaxStone)} St  |  "
                    + $"{FormatNum(_resourceManager.Iron)}/{FormatNum(_resourceManager.MaxIron)} Ir  |  "
                    + $"{FormatNum(_resourceManager.Grain)}/{FormatNum(_resourceManager.MaxGrain)} Gr  |  "
                    + $"{FormatNum(_resourceManager.ArcaneEssence)}/{FormatNum(_resourceManager.MaxArcaneEssence)} Ar";
                CreateText(vaultGO.transform, "VaultText", vaultText, 9, FontStyle.Normal,
                    new Color(0.65f, 0.60f, 0.55f, 0.85f), new Vector2(0.02f, 0), new Vector2(0.98f, 1));
            }
        }

        private void ClosePopup()
        {
            if (_popup != null)
            {
                Destroy(_popup);
                _popup = null;
            }
        }

        private Dictionary<string, ProductionTotal> CalculateProduction()
        {
            var result = new Dictionary<string, ProductionTotal>
            {
                ["stone"] = new ProductionTotal(),
                ["iron"] = new ProductionTotal(),
                ["grain"] = new ProductionTotal(),
                ["arcane"] = new ProductionTotal()
            };

            if (_buildingManager == null) return result;

            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                var placed = kvp.Value;
                if (placed.Data == null) continue;
                if (!_dataCache.TryGetValue(placed.Data.buildingId, out var data)) continue;
                var tierData = data.GetTier(placed.CurrentTier);
                if (tierData == null) continue;

                AddProduction(result, "stone", placed.Data.buildingId, placed.CurrentTier, tierData.stoneProduction);
                AddProduction(result, "iron", placed.Data.buildingId, placed.CurrentTier, tierData.ironProduction);
                AddProduction(result, "grain", placed.Data.buildingId, placed.CurrentTier, tierData.grainProduction);
                AddProduction(result, "arcane", placed.Data.buildingId, placed.CurrentTier, tierData.arcaneEssenceProduction);
            }

            return result;
        }

        private static void AddProduction(Dictionary<string, ProductionTotal> totals, string resKey,
            string buildingId, int tier, float rate)
        {
            if (rate <= 0) return;
            var total = totals[resKey];
            total.Total += rate;

            bool found = false;
            for (int i = 0; i < total.Buildings.Count; i++)
            {
                if (total.Buildings[i].BuildingId == buildingId && total.Buildings[i].Tier == tier)
                {
                    var entry = total.Buildings[i];
                    entry.Count++;
                    entry.Rate += rate;
                    total.Buildings[i] = entry;
                    found = true;
                    break;
                }
            }
            if (!found)
                total.Buildings.Add(new BuildingEntry { BuildingId = buildingId, Tier = tier, Count = 1, Rate = rate });
        }

        private static void CreateText(Transform parent, string name, string text, int size,
            FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = color;
            t.raycastTarget = false;
        }

        private static string FormatBuildingName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Building";
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        private static string FormatNum(long val)
        {
            if (val >= 1_000_000) return $"{val / 1_000_000f:F1}M";
            if (val >= 1_000) return $"{val / 1_000f:F1}K";
            return val.ToString();
        }

        private class ProductionTotal
        {
            public float Total;
            public readonly List<BuildingEntry> Buildings = new();
        }

        private struct BuildingEntry
        {
            public string BuildingId;
            public int Tier;
            public int Count;
            public float Rate;
        }
    }

    public readonly struct ResourceBarTappedEvent { }
}
