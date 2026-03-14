using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;
using AshenThrone.Data;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality upgrade recommendation banner with ornate triple border,
    /// glass highlight, warm inner glow, pulsing arrow, and animated GO button.
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
        private Text _arrowText;
        private GameObject _goBtn;

        private EventSubscription _upgradeSub;
        private EventSubscription _placedSub;
        private float _refreshTimer;
        private const float RefreshInterval = 5f;

        private static readonly Color BannerBg = new(0.07f, 0.05f, 0.12f, 0.93f);
        private static readonly Color BannerGoldBorder = new(0.83f, 0.66f, 0.26f, 0.75f);
        private static readonly Color BannerGlow = new(0.90f, 0.72f, 0.28f, 0.25f);
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

            // Animate arrow pulse
            if (_arrowText != null && _banner != null && _banner.activeSelf)
            {
                float pulse = Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f;
                _arrowText.color = new Color(ArrowColor.r, ArrowColor.g, ArrowColor.b,
                    Mathf.Lerp(0.5f, 1f, pulse));
                float arrowScale = Mathf.Lerp(0.9f, 1.1f, pulse);
                _arrowText.transform.localScale = Vector3.one * arrowScale;
            }

            // GO button glow pulse
            if (_goBtn != null && _banner != null && _banner.activeSelf)
            {
                float goPulse = Mathf.Sin(Time.time * 2.5f) * 0.5f + 0.5f;
                float goScale = Mathf.Lerp(0.96f, 1.06f, goPulse);
                _goBtn.transform.localScale = Vector3.one * goScale;
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

            // Triple border: outer glow → gold → inner
            var glowOutline = new GameObject("GlowBorder");
            glowOutline.transform.SetParent(_banner.transform, false);
            var gbRect = glowOutline.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = glowOutline.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            var gbOut = glowOutline.AddComponent<Outline>();
            gbOut.effectColor = BannerGlow;
            gbOut.effectDistance = new Vector2(2.5f, -2.5f);

            var goldOutline = new GameObject("GoldBorder");
            goldOutline.transform.SetParent(_banner.transform, false);
            var goRect = goldOutline.AddComponent<RectTransform>();
            goRect.anchorMin = Vector2.zero; goRect.anchorMax = Vector2.one;
            goRect.offsetMin = Vector2.zero; goRect.offsetMax = Vector2.zero;
            var goImg = goldOutline.AddComponent<Image>();
            goImg.color = new Color(0, 0, 0, 0); goImg.raycastTarget = false;
            var goOut = goldOutline.AddComponent<Outline>();
            goOut.effectColor = BannerGoldBorder;
            goOut.effectDistance = new Vector2(1.2f, -1.2f);

            // Glass highlight (top half)
            var glass = new GameObject("Glass");
            glass.transform.SetParent(_banner.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.45f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero;
            glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.05f);
            glImg.raycastTarget = false;

            // Warm inner glow on left edge
            var warmEdge = new GameObject("WarmEdge");
            warmEdge.transform.SetParent(_banner.transform, false);
            var weRect = warmEdge.AddComponent<RectTransform>();
            weRect.anchorMin = new Vector2(0f, 0f);
            weRect.anchorMax = new Vector2(0.12f, 1f);
            weRect.offsetMin = Vector2.zero;
            weRect.offsetMax = Vector2.zero;
            var weImg = warmEdge.AddComponent<Image>();
            weImg.color = new Color(0.90f, 0.72f, 0.28f, 0.06f);
            weImg.raycastTarget = false;

            // Arrow indicator on left (pulsing, animated in Update)
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(_banner.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.01f, 0.1f);
            arrowRect.anchorMax = new Vector2(0.06f, 0.9f);
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            _arrowText = arrowGO.AddComponent<Text>();
            _arrowText.text = "\u25B6";
            _arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _arrowText.fontSize = 14;
            _arrowText.alignment = TextAnchor.MiddleCenter;
            _arrowText.color = ArrowColor;
            _arrowText.raycastTarget = false;
            var arrowOutline = arrowGO.AddComponent<Outline>();
            arrowOutline.effectColor = new Color(0, 0, 0, 0.7f);
            arrowOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Recommendation text with outline
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(_banner.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.07f, 0f);
            textRect.anchorMax = new Vector2(0.83f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _bannerText = textGO.AddComponent<Text>();
            _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bannerText.fontSize = 11;
            _bannerText.fontStyle = FontStyle.Bold;
            _bannerText.alignment = TextAnchor.MiddleLeft;
            _bannerText.color = new Color(0.92f, 0.87f, 0.72f, 1f);
            _bannerText.raycastTarget = false;
            _bannerText.supportRichText = true;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.9f);
            textOutline.effectDistance = new Vector2(0.7f, -0.7f);
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0.3f, -0.6f);

            // Ornate "GO" button on right (animated in Update)
            _goBtn = new GameObject("GoBtn");
            _goBtn.transform.SetParent(_banner.transform, false);
            var goBtnRect = _goBtn.AddComponent<RectTransform>();
            goBtnRect.anchorMin = new Vector2(0.84f, 0.08f);
            goBtnRect.anchorMax = new Vector2(0.99f, 0.92f);
            goBtnRect.offsetMin = Vector2.zero;
            goBtnRect.offsetMax = Vector2.zero;
            var goBtnBg = _goBtn.AddComponent<Image>();
            goBtnBg.color = new Color(0.18f, 0.68f, 0.32f, 1f);
            // Triple border on GO button
            var goBtnGlow = _goBtn.AddComponent<Outline>();
            goBtnGlow.effectColor = new Color(0.25f, 0.85f, 0.40f, 0.35f);
            goBtnGlow.effectDistance = new Vector2(1.5f, -1.5f);
            var goBtnGold = new GameObject("GoldEdge");
            goBtnGold.transform.SetParent(_goBtn.transform, false);
            var ggRect = goBtnGold.AddComponent<RectTransform>();
            ggRect.anchorMin = Vector2.zero; ggRect.anchorMax = Vector2.one;
            ggRect.offsetMin = Vector2.zero; ggRect.offsetMax = Vector2.zero;
            var ggImg = goBtnGold.AddComponent<Image>();
            ggImg.color = new Color(0, 0, 0, 0); ggImg.raycastTarget = false;
            var ggOut = goBtnGold.AddComponent<Outline>();
            ggOut.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.55f);
            ggOut.effectDistance = new Vector2(0.7f, -0.7f);

            // Glass on GO button
            var goGlass = new GameObject("Glass");
            goGlass.transform.SetParent(_goBtn.transform, false);
            var gogRect = goGlass.AddComponent<RectTransform>();
            gogRect.anchorMin = new Vector2(0f, 0.45f);
            gogRect.anchorMax = Vector2.one;
            gogRect.offsetMin = Vector2.zero;
            gogRect.offsetMax = Vector2.zero;
            var gogImg = goGlass.AddComponent<Image>();
            gogImg.color = new Color(1f, 1f, 1f, 0.10f);
            gogImg.raycastTarget = false;

            var goBtn = _goBtn.AddComponent<Button>();
            goBtn.targetGraphic = goBtnBg;
            goBtn.onClick.AddListener(OnGoBtnPressed);

            var goLabel = new GameObject("Label");
            goLabel.transform.SetParent(_goBtn.transform, false);
            var goLabelRect = goLabel.AddComponent<RectTransform>();
            goLabelRect.anchorMin = Vector2.zero;
            goLabelRect.anchorMax = Vector2.one;
            goLabelRect.offsetMin = Vector2.zero;
            goLabelRect.offsetMax = Vector2.zero;
            var goText = goLabel.AddComponent<Text>();
            goText.text = "GO";
            goText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goText.fontSize = 12;
            goText.fontStyle = FontStyle.Bold;
            goText.alignment = TextAnchor.MiddleCenter;
            goText.color = Color.white;
            goText.raycastTarget = false;
            var goTextOutline = goLabel.AddComponent<Outline>();
            goTextOutline.effectColor = new Color(0, 0, 0, 0.8f);
            goTextOutline.effectDistance = new Vector2(0.6f, -0.6f);

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
