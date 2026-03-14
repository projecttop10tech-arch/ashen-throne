using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style tabbed building catalog shown when tapping empty ground.
    /// Category tabs (Resource, Military, Research, Hero) filter the building list.
    /// Each building entry shows name, unlock status, and tap to enter placement mode.
    /// </summary>
    public class BuildCatalogController : MonoBehaviour
    {
        private GameObject _catalogPopup;
        private Transform _tabContainer;
        private Transform _listContainer;
        private Text _titleLabel;

        private BuildingManager _buildingManager;
        private Dictionary<string, BuildingData> _buildingDataCache;
        private EventSubscription _emptyCellSub;

        private Vector2Int _targetGridPos;
        private BuildingCategory _selectedCategory = BuildingCategory.Resource;

        private static readonly Color TabActiveColor = new(0.78f, 0.62f, 0.22f, 1f);
        private static readonly Color TabInactiveColor = new(0.25f, 0.20f, 0.30f, 0.8f);
        private static readonly Color EntryUnlocked = new(0.10f, 0.08f, 0.16f, 0.90f);
        private static readonly Color EntryLocked = new(0.06f, 0.04f, 0.10f, 0.70f);
        private static readonly Color GoldText = new(0.83f, 0.66f, 0.26f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
            CacheBuildingData();
        }

        private void OnEnable()
        {
            _emptyCellSub = EventBus.Subscribe<EmptyCellTappedEvent>(OnEmptyCellTapped);
        }

        private void OnDisable()
        {
            _emptyCellSub?.Dispose();
        }

        private void Start()
        {
            FindPopupElements();
            if (_catalogPopup != null)
                _catalogPopup.SetActive(false);
        }

        private void OnEmptyCellTapped(EmptyCellTappedEvent evt)
        {
            _targetGridPos = evt.GridPosition;
            ShowCatalog();
        }

        private void ShowCatalog()
        {
            if (_catalogPopup == null)
                FindPopupElements();
            if (_catalogPopup == null) return;

            _catalogPopup.SetActive(true);
            if (_titleLabel != null)
                _titleLabel.text = $"BUILD AT ({_targetGridPos.x}, {_targetGridPos.y})";

            PopulateTabs();
            PopulateList();
        }

        private void CloseCatalog()
        {
            if (_catalogPopup != null)
                _catalogPopup.SetActive(false);
        }

        private void PopulateTabs()
        {
            if (_tabContainer == null) return;
            // Clear existing tabs
            for (int i = _tabContainer.childCount - 1; i >= 0; i--)
                Destroy(_tabContainer.GetChild(i).gameObject);

            var categories = new[] { BuildingCategory.Resource, BuildingCategory.Military, BuildingCategory.Research, BuildingCategory.HeroDistrict };
            string[] labels = { "RESOURCE", "MILITARY", "RESEARCH", "HERO" };

            for (int idx = 0; idx < categories.Length; idx++)
            {
                var cat = categories[idx];
                var label = labels[idx];

                var tabGO = new GameObject($"Tab_{cat}");
                tabGO.transform.SetParent(_tabContainer, false);
                var tabRect = tabGO.AddComponent<RectTransform>();
                tabRect.sizeDelta = new Vector2(0, 30); // Height; width from layout

                var tabImg = tabGO.AddComponent<Image>();
                tabImg.color = cat == _selectedCategory ? TabActiveColor : TabInactiveColor;

                var btn = tabGO.AddComponent<Button>();
                btn.targetGraphic = tabImg;
                var capturedCat = cat;
                btn.onClick.AddListener(() => { _selectedCategory = capturedCat; PopulateTabs(); PopulateList(); });

                var textGO = new GameObject("Label");
                textGO.transform.SetParent(tabGO.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGO.AddComponent<Text>();
                text.text = label;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 10;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = cat == _selectedCategory ? Color.black : Color.white;
                text.raycastTarget = false;
            }
        }

        private void PopulateList()
        {
            if (_listContainer == null) return;
            // Clear existing entries
            for (int i = _listContainer.childCount - 1; i >= 0; i--)
                Destroy(_listContainer.GetChild(i).gameObject);

            int shLevel = GetStrongholdLevel();

            foreach (var kvp in _buildingDataCache)
            {
                var data = kvp.Value;
                if (data.category != _selectedCategory) continue;

                bool unlocked = data.strongholdLevelRequired <= shLevel;
                int currentCount = CountPlacedBuildings(data.buildingId);
                int maxCount = data.isUniquePerCity ? 1 : 10;

                CreateBuildingEntry(data, unlocked, currentCount, maxCount);
            }
        }

        private void CreateBuildingEntry(BuildingData data, bool unlocked, int count, int max)
        {
            var entryGO = new GameObject($"Entry_{data.buildingId}");
            entryGO.transform.SetParent(_listContainer, false);
            var entryRect = entryGO.AddComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0, 52); // Taller for premium feel

            var entryImg = entryGO.AddComponent<Image>();
            entryImg.color = unlocked ? EntryUnlocked : EntryLocked;

            // Gold border — brighter for unlocked
            var outline = entryGO.AddComponent<Outline>();
            outline.effectColor = unlocked
                ? new Color(0.72f, 0.56f, 0.22f, 0.55f)
                : new Color(0.25f, 0.20f, 0.15f, 0.25f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            // Glass highlight on top half (premium depth)
            var entryGlass = new GameObject("Glass");
            entryGlass.transform.SetParent(entryGO.transform, false);
            var glassRect = entryGlass.AddComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0f, 0.50f);
            glassRect.anchorMax = new Vector2(1f, 1f);
            glassRect.offsetMin = Vector2.zero;
            glassRect.offsetMax = Vector2.zero;
            var glassImg = entryGlass.AddComponent<Image>();
            glassImg.color = unlocked
                ? new Color(0.40f, 0.30f, 0.55f, 0.06f)
                : new Color(0.20f, 0.18f, 0.22f, 0.04f);
            glassImg.raycastTarget = false;

            // Building icon preview — recessed with border
            var iconBg = new GameObject("IconBg");
            iconBg.transform.SetParent(entryGO.transform, false);
            var iconBgRect = iconBg.AddComponent<RectTransform>();
            iconBgRect.anchorMin = new Vector2(0.015f, 0.08f);
            iconBgRect.anchorMax = new Vector2(0.17f, 0.92f);
            iconBgRect.offsetMin = Vector2.zero;
            iconBgRect.offsetMax = Vector2.zero;
            var iconBgImg = iconBg.AddComponent<Image>();
            iconBgImg.color = new Color(0.04f, 0.03f, 0.08f, 0.70f);
            iconBgImg.raycastTarget = false;
            var iconBgOutline = iconBg.AddComponent<Outline>();
            iconBgOutline.effectColor = unlocked
                ? new Color(0.55f, 0.43f, 0.18f, 0.40f)
                : new Color(0.20f, 0.18f, 0.15f, 0.20f);
            iconBgOutline.effectDistance = new Vector2(0.8f, -0.8f);

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(entryGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.10f);
            iconRect.anchorMax = new Vector2(0.16f, 0.90f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;
            #if UNITY_EDITOR
            var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Buildings/{data.buildingId}_t1.png");
            if (spr != null) { iconImg.sprite = spr; iconImg.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.6f); }
            else
            #endif
            { iconImg.color = unlocked ? GoldText : new Color(0.3f, 0.3f, 0.3f, 0.5f); }

            // Name — larger, bolder
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(entryGO.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.19f, 0.50f);
            nameRect.anchorMax = new Vector2(0.74f, 0.95f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = string.IsNullOrEmpty(data.displayName) ? FormatName(data.buildingId) : data.displayName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 12;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = unlocked ? new Color(1f, 0.88f, 0.52f, 1f) : new Color(0.50f, 0.45f, 0.40f, 0.7f);
            nameText.raycastTarget = false;
            var nameOutline = nameGO.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.55f);
            nameOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var nameShadow = nameGO.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.85f);
            nameShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Status line
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(entryGO.transform, false);
            var statusRect = statusGO.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.19f, 0.05f);
            statusRect.anchorMax = new Vector2(0.74f, 0.50f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            var statusText = statusGO.AddComponent<Text>();
            if (unlocked)
                statusText.text = $"Built: {count}/{max}";
            else
                statusText.text = $"<color=#FF6644>\u26D4 Requires SH Lv.{data.strongholdLevelRequired + 1}</color>";
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 10;
            statusText.alignment = TextAnchor.MiddleLeft;
            statusText.color = new Color(0.70f, 0.65f, 0.58f, 0.85f);
            statusText.raycastTarget = false;
            statusText.supportRichText = true;
            var statusShadow = statusGO.AddComponent<Shadow>();
            statusShadow.effectColor = new Color(0, 0, 0, 0.60f);
            statusShadow.effectDistance = new Vector2(0.3f, -0.3f);

            // BUILD button — premium green with glow
            if (unlocked)
            {
                var btnGO = new GameObject("BuildBtn");
                btnGO.transform.SetParent(entryGO.transform, false);
                var btnRect = btnGO.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.76f, 0.12f);
                btnRect.anchorMax = new Vector2(0.98f, 0.88f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;
                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.15f, 0.58f, 0.28f, 1f);
                var btnOutline = btnGO.AddComponent<Outline>();
                btnOutline.effectColor = new Color(0.25f, 0.82f, 0.40f, 0.40f);
                btnOutline.effectDistance = new Vector2(1f, -1f);
                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                var buildingId = data.buildingId;
                btn.onClick.AddListener(() => OnBuildPressed(buildingId));

                // Glass highlight on button
                var btnGlass = new GameObject("BtnGlass");
                btnGlass.transform.SetParent(btnGO.transform, false);
                var btnGlassRect = btnGlass.AddComponent<RectTransform>();
                btnGlassRect.anchorMin = new Vector2(0f, 0.50f);
                btnGlassRect.anchorMax = Vector2.one;
                btnGlassRect.offsetMin = Vector2.zero;
                btnGlassRect.offsetMax = Vector2.zero;
                btnGlass.AddComponent<Image>().color = new Color(0.55f, 1f, 0.65f, 0.10f);
                btnGlass.GetComponent<Image>().raycastTarget = false;

                var btnLabel = new GameObject("Label");
                btnLabel.transform.SetParent(btnGO.transform, false);
                var btnLabelRect = btnLabel.AddComponent<RectTransform>();
                btnLabelRect.anchorMin = Vector2.zero;
                btnLabelRect.anchorMax = Vector2.one;
                btnLabelRect.offsetMin = Vector2.zero;
                btnLabelRect.offsetMax = Vector2.zero;
                var btnText = btnLabel.AddComponent<Text>();
                btnText.text = "BUILD";
                btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnText.fontSize = 11;
                btnText.fontStyle = FontStyle.Bold;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                btnText.raycastTarget = false;
                var btnLabelOutline = btnLabel.AddComponent<Outline>();
                btnLabelOutline.effectColor = new Color(0.05f, 0.25f, 0.08f, 0.70f);
                btnLabelOutline.effectDistance = new Vector2(0.6f, -0.6f);
                btnLabel.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
                btnLabel.GetComponent<Shadow>().effectDistance = new Vector2(0.5f, -0.5f);
            }
        }

        private void OnBuildPressed(string buildingId)
        {
            CloseCatalog();
            // Enter placement mode on CityGridView
            var gridView = FindFirstObjectByType<CityGridView>();
            if (gridView != null)
            {
                gridView.EnterPlacementMode(buildingId, _targetGridPos);
                Debug.Log($"[BuildCatalog] Entering placement mode for {buildingId} at {_targetGridPos}.");
            }
        }

        private int GetStrongholdLevel()
        {
            if (_buildingManager == null) return 99;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == "stronghold")
                    return kvp.Value.CurrentTier;
            }
            return 0;
        }

        private int CountPlacedBuildings(string buildingId)
        {
            if (_buildingManager == null) return 0;
            int count = 0;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == buildingId)
                    count++;
            }
            return count;
        }

        private void CacheBuildingData()
        {
            _buildingDataCache = new Dictionary<string, BuildingData>();
            var allData = Resources.LoadAll<BuildingData>("");
            foreach (var d in allData)
            {
                if (d != null && !string.IsNullOrEmpty(d.buildingId))
                    _buildingDataCache[d.buildingId] = d;
            }
        }

        private void FindPopupElements()
        {
            var t = FindDeepChild(transform.root, "BuildCatalogPopup");
            if (t == null)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    t = FindDeepChild(root.transform, "BuildCatalogPopup");
                    if (t != null) break;
                }
            }
            if (t == null) return;

            _catalogPopup = t.gameObject;
            _titleLabel = FindTextChild(t, "CatalogTitle");
            _tabContainer = FindDeepChild(t, "TabContainer");
            _listContainer = FindDeepChild(t, "ListContainer");

            // Close button
            var closeT = FindDeepChild(t, "CatalogCloseBtn");
            if (closeT != null)
            {
                var btn = closeT.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(CloseCatalog);
            }

            // Overlay tap to close
            var overlayT = FindDeepChild(t, "CatalogOverlay");
            if (overlayT != null)
            {
                var btn = overlayT.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(CloseCatalog);
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

        private static Text FindTextChild(Transform parent, string name)
        {
            var child = FindDeepChild(parent, name);
            return child?.GetComponent<Text>();
        }

        private static string FormatName(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return "Building";
            var parts = buildingId.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
