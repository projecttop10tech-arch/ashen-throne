using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style upgrade recommendation banner.
    /// Shows a persistent banner suggesting the next priority upgrade.
    /// Priority: Stronghold > lowest-tier military > lowest-tier resource.
    /// Tapping the banner opens the building info popup for that building.
    /// </summary>
    public class UpgradeRecommendationBanner : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private BuildingManager _buildingManager;
        private ResourceManager _resourceManager;
        private CityGridView _gridView;

        private GameObject _banner;
        private Text _bannerText;
        private string _recommendedInstanceId;

        private EventSubscription _upgradeSub;
        private EventSubscription _placedSub;
        private float _refreshTimer;
        private const float RefreshInterval = 5f;

        private static readonly Color BannerBg = new(0.10f, 0.07f, 0.16f, 0.90f);
        private static readonly Color BannerBorder = new(0.83f, 0.66f, 0.26f, 0.60f);
        private static readonly Color ArrowColor = new(0.95f, 0.78f, 0.20f, 1f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
            ServiceLocator.TryGet(out _resourceManager);
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _canvasRect = canvas.GetComponent<RectTransform>();
            _gridView = FindFirstObjectByType<CityGridView>();
            CreateBanner();
            Refresh();
        }

        private void OnEnable()
        {
            _upgradeSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(_ => Refresh());
            _placedSub = EventBus.Subscribe<BuildingPlacedEvent>(_ => Refresh());
        }

        private void OnDisable()
        {
            _upgradeSub?.Dispose();
            _placedSub?.Dispose();
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                Refresh();
            }
        }

        private void CreateBanner()
        {
            if (_canvasRect == null) return;

            _banner = new GameObject("UpgradeRecommendation");
            _banner.transform.SetParent(_canvasRect, false);

            var rect = _banner.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.03f, 0.145f);
            rect.anchorMax = new Vector2(0.97f, 0.185f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _banner.AddComponent<Image>();
            bg.color = BannerBg;
            bg.raycastTarget = true;
            var outline = _banner.AddComponent<Outline>();
            outline.effectColor = BannerBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            // Arrow indicator on left
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(_banner.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.01f, 0.1f);
            arrowRect.anchorMax = new Vector2(0.06f, 0.9f);
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            var arrowText = arrowGO.AddComponent<Text>();
            arrowText.text = "\u25B6"; // ▶
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 14;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = ArrowColor;
            arrowText.raycastTarget = false;

            // Recommendation text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(_banner.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.07f, 0f);
            textRect.anchorMax = new Vector2(0.93f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _bannerText = textGO.AddComponent<Text>();
            _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bannerText.fontSize = 11;
            _bannerText.fontStyle = FontStyle.Bold;
            _bannerText.alignment = TextAnchor.MiddleLeft;
            _bannerText.color = new Color(0.90f, 0.85f, 0.70f, 1f);
            _bannerText.raycastTarget = false;
            _bannerText.supportRichText = true;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);

            // "GO" button on right
            var goGO = new GameObject("GoBtn");
            goGO.transform.SetParent(_banner.transform, false);
            var goRect = goGO.AddComponent<RectTransform>();
            goRect.anchorMin = new Vector2(0.85f, 0.10f);
            goRect.anchorMax = new Vector2(0.99f, 0.90f);
            goRect.offsetMin = Vector2.zero;
            goRect.offsetMax = Vector2.zero;
            var goImg = goGO.AddComponent<Image>();
            goImg.color = new Color(0.20f, 0.72f, 0.35f, 1f);
            var goBtn = goGO.AddComponent<Button>();
            goBtn.targetGraphic = goImg;
            goBtn.onClick.AddListener(OnGoBtnPressed);
            var goLabel = new GameObject("Label");
            goLabel.transform.SetParent(goGO.transform, false);
            var goLabelRect = goLabel.AddComponent<RectTransform>();
            goLabelRect.anchorMin = Vector2.zero;
            goLabelRect.anchorMax = Vector2.one;
            goLabelRect.offsetMin = Vector2.zero;
            goLabelRect.offsetMax = Vector2.zero;
            var goText = goLabel.AddComponent<Text>();
            goText.text = "GO";
            goText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goText.fontSize = 11;
            goText.fontStyle = FontStyle.Bold;
            goText.alignment = TextAnchor.MiddleCenter;
            goText.color = Color.white;
            goText.raycastTarget = false;

            // Also make entire banner tappable
            var bannerBtn = _banner.AddComponent<Button>();
            bannerBtn.onClick.AddListener(OnGoBtnPressed);
        }

        private void Refresh()
        {
            if (_buildingManager == null || _banner == null) return;

            _recommendedInstanceId = null;
            string message = null;

            // Priority 1: Stronghold upgrade
            PlacedBuilding stronghold = null;
            int lowestSHTier = int.MaxValue;
            foreach (var kvp in _buildingManager.PlacedBuildings)
            {
                if (kvp.Value.Data != null && kvp.Value.Data.buildingId == "stronghold")
                {
                    if (kvp.Value.CurrentTier < lowestSHTier)
                    {
                        lowestSHTier = kvp.Value.CurrentTier;
                        stronghold = kvp.Value;
                    }
                }
            }

            // Check if SH is already upgrading
            bool shUpgrading = false;
            if (stronghold != null)
            {
                foreach (var entry in _buildingManager.BuildQueue)
                    if (entry.PlacedId == stronghold.PlacedId) { shUpgrading = true; break; }
            }

            if (stronghold != null && !shUpgrading)
            {
                _recommendedInstanceId = stronghold.PlacedId;
                int nextLv = stronghold.CurrentTier + 2;
                message = $"Upgrade <color=#FFD966>Stronghold to Lv.{nextLv}</color> to unlock new buildings!";
            }
            else
            {
                // Priority 2: Lowest-tier non-upgrading building
                PlacedBuilding lowest = null;
                int lowestTier = int.MaxValue;
                foreach (var kvp in _buildingManager.PlacedBuildings)
                {
                    var b = kvp.Value;
                    if (b.Data == null) continue;
                    if (b.Data.buildingId == "stronghold") continue;
                    bool upgrading = false;
                    foreach (var entry in _buildingManager.BuildQueue)
                        if (entry.PlacedId == b.PlacedId) { upgrading = true; break; }
                    if (upgrading) continue;
                    if (b.CurrentTier < lowestTier)
                    {
                        lowestTier = b.CurrentTier;
                        lowest = b;
                    }
                }

                if (lowest != null)
                {
                    _recommendedInstanceId = lowest.PlacedId;
                    string name = lowest.Data.displayName;
                    if (string.IsNullOrEmpty(name)) name = FormatName(lowest.Data.buildingId);
                    message = $"Upgrade <color=#FFD966>{name} to Lv.{lowest.CurrentTier + 2}</color> to increase power!";
                }
            }

            if (message != null)
            {
                _banner.SetActive(true);
                _bannerText.text = message;
            }
            else
            {
                _banner.SetActive(false);
            }
        }

        private void OnGoBtnPressed()
        {
            if (string.IsNullOrEmpty(_recommendedInstanceId)) return;
            if (_buildingManager == null) return;
            if (!_buildingManager.PlacedBuildings.TryGetValue(_recommendedInstanceId, out var placed)) return;

            // Publish a building tapped event to open the info popup
            EventBus.Publish(new BuildingTappedEvent(
                _recommendedInstanceId, placed.Data?.buildingId ?? "", placed.CurrentTier, placed.GridPosition));
        }

        private static string FormatName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Building";
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
