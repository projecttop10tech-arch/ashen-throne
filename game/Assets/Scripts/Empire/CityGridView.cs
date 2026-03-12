using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Manages a large virtual city grid (48x48) with isometric diamond layout.
    /// No per-cell GameObjects — occupancy tracked in a dictionary.
    /// Grid overlay + placement highlight shown only during move mode.
    /// Coordinate conversion uses 2:1 isometric projection.
    /// </summary>
    public class CityGridView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public const int GridColumns = 48;
        public const int GridRows = 48;
        public const float CellSize = 64f;

        // Isometric projection constants (2:1 ratio)
        public const float HalfW = CellSize * 0.5f;   // 32 — half tile width
        public const float HalfH = CellSize * 0.25f;   // 16 — half tile height

        // Center offset so grid center maps to screen origin
        public static readonly float IsoCenterY = (GridColumns - 1 + GridRows - 1) * 0.5f * HalfH;

        // Playable area (inside walls)
        public const int PlayableMinX = 2;
        public const int PlayableMinY = 2;
        public const int PlayableMaxX = 45; // exclusive
        public const int PlayableMaxY = 45; // exclusive

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private RectTransform buildingContainer;
        [SerializeField] private GameObject gridOverlay;

        // Building size definitions (width x height in grid cells)
        public static readonly Dictionary<string, Vector2Int> BuildingSizes = new()
        {
            { "stronghold",       new Vector2Int(6, 6) },
            { "barracks",         new Vector2Int(3, 2) },
            { "forge",            new Vector2Int(2, 2) },
            { "marketplace",      new Vector2Int(3, 2) },
            { "academy",          new Vector2Int(2, 3) },
            { "grain_farm",       new Vector2Int(2, 2) },
            { "iron_mine",        new Vector2Int(2, 2) },
            { "stone_quarry",     new Vector2Int(2, 2) },
            { "arcane_tower",     new Vector2Int(2, 3) },
            { "wall",             new Vector2Int(2, 1) },
            { "watch_tower",      new Vector2Int(2, 2) },
            { "guild_hall",       new Vector2Int(3, 2) },
            { "embassy",          new Vector2Int(3, 2) },
            { "training_ground",  new Vector2Int(2, 2) },
            { "hero_shrine",      new Vector2Int(2, 2) },
            { "laboratory",       new Vector2Int(2, 2) },
            { "library",          new Vector2Int(2, 2) },
            { "armory",           new Vector2Int(2, 2) },
            { "enchanting_tower", new Vector2Int(2, 3) },
            { "observatory",      new Vector2Int(2, 2) },
            { "archive",          new Vector2Int(2, 2) },
            // Decorations / cosmetic items (future)
            { "road",             new Vector2Int(1, 1) },
            { "fountain",         new Vector2Int(1, 1) },
            { "tree",             new Vector2Int(1, 1) },
            { "statue",           new Vector2Int(1, 1) },
            { "lamp_post",        new Vector2Int(1, 1) },
            { "garden",           new Vector2Int(2, 2) },
            { "banner",           new Vector2Int(1, 1) },
        };

        /// <summary>P&C: Max allowed instances per building type. Types not listed default to 1 (unique).</summary>
        private static readonly Dictionary<string, int> MaxBuildingCountPerType = new()
        {
            { "grain_farm", 5 }, { "iron_mine", 3 }, { "stone_quarry", 3 }, { "arcane_tower", 2 },
            { "barracks", 2 }, { "training_ground", 2 }, { "wall", 4 },
        };

        // Virtual grid occupancy: cell position -> instance ID
        private readonly Dictionary<Vector2Int, string> _occupancy = new();
        private readonly List<CityBuildingPlacement> _placements = new();

        /// <summary>Read-only access to placed buildings for external systems (e.g., resource bubbles).</summary>
        public IReadOnlyList<CityBuildingPlacement> GetPlacements() => _placements;

        // Move mode state
        private bool _moveMode;
        private CityBuildingPlacement _movingBuilding;
        private GameObject _dragGhost;
        private GameObject _dragShadow;
        private float _holdTimer;
        private bool _holdStarted;

        // P&C: Double-tap quick-upgrade detection
        private string _lastTappedInstanceId;
        private float _lastTapTime;
        private const float DoubleTapWindow = 0.4f;
        private Vector2 _holdStartPos;
        private const float LongPressTime = 0.5f;
        private const float DragThreshold = 10f;

        // P&C: Free instant-complete threshold (seconds)
        private const int FreeSpeedUpThresholdSeconds = 300; // 5 minutes

        // Placement highlight (shows snap target during drag) — iso diamond cells
        private static readonly Color HighlightValid = new(0.15f, 0.9f, 0.3f, 0.25f);
        private static readonly Color HighlightInvalid = new(0.95f, 0.15f, 0.15f, 0.25f);
        private static readonly Color BorderValid = new(0.2f, 1f, 0.4f, 0.55f);
        private static readonly Color BorderInvalid = new(1f, 0.2f, 0.2f, 0.55f);

        // P&C: Building footprint highlight on tap
        private static readonly Color FootprintColor = new(0.78f, 0.62f, 0.22f, 0.18f);
        private static readonly Color FootprintBorder = new(0.83f, 0.66f, 0.26f, 0.45f);
        private readonly List<GameObject> _footprintCells = new();
        private string _footprintInstanceId;
        private GameObject _selectionRing;

        // Pinch-zoom state
        private const float ZoomMin = 0.4f;
        private const float ZoomMax = 2.5f;
        private const float ZoomSpeed = 0.005f; // per pixel of pinch delta
        private const float MouseScrollZoomSpeed = 0.15f;
        private const float ZoomLerpSpeed = 8f; // P&C: smooth zoom interpolation
        private const float DefaultZoom = 2.5f;
        private float _currentZoom = DefaultZoom;
        private float _targetZoom = DefaultZoom;
        private Vector2 _zoomPivotScreen;
        private bool _isPinching;
        private float _lastPinchDistance;
        private int _touchCount;

        private EventSubscription _upgradeCompletedSub;
        private EventSubscription _demolishedSub;
        private EventSubscription _doubleTapZoomSub;

        // P&C: Smooth zoom-to-building on double-tap
        private Coroutine _smoothZoomCoroutine;
        private const float DoubleTapZoomTarget = 2.0f;
        private const float SmoothZoomDuration = 0.35f;

        // P&C: Building category mini-icons (visible at medium zoom)
        private static readonly Dictionary<string, string> CategoryIcons = new()
        {
            { "barracks", "\u2694" }, { "training_ground", "\u2694" }, { "armory", "\u2694" },
            { "wall", "\u26E8" }, { "watch_tower", "\u26E8" },
            { "grain_farm", "\u26CF" }, { "iron_mine", "\u26CF" }, { "stone_quarry", "\u26CF" },
            { "arcane_tower", "\u2728" }, { "enchanting_tower", "\u2728" }, { "hero_shrine", "\u2728" },
            { "academy", "\u2609" }, { "library", "\u2609" }, { "archive", "\u2609" },
            { "observatory", "\u2609" }, { "laboratory", "\u2609" },
            { "guild_hall", "\u265F" }, { "embassy", "\u265F" },
        };
        private static readonly Color CategoryIconColor = new(1f, 0.92f, 0.72f, 0.85f);

        // P&C: Empty-ground double-tap zoom toggle
        private float _lastEmptyTapTime;
        private const float EmptyDoubleTapWindow = 0.4f;
        private const float ZoomOverview = 0.6f; // zoomed out overview level

        // P&C: Building info popup on tap
        private GameObject _infoPopup;
        private string _infoPopupInstanceId;
        private Coroutine _popupAutoDismiss;
        private EventSubscription _buildingTappedSub;

        // P&C: Move mode confirm/cancel bar
        private GameObject _moveConfirmBar;
        private Vector2Int _moveOriginalOrigin;

        // P&C: Upgrade-available arrows + builder count + scaffolding
        private float _upgradeArrowRefreshTimer;
        private const float UpgradeArrowRefreshInterval = 2f;
        private GameObject _builderCountHUD;
        private Text _builderCountText;

        // P&C: Long-press hold indicator
        private GameObject _holdIndicator;
        private Image _holdFillImage;

        // P&C: Soft-center on single tap
        private Coroutine _softCenterCoroutine;
        private const float SoftCenterDuration = 0.25f;
        private const float SoftCenterStrength = 0.6f; // 60% toward center (subtle)

        // P&C: Audio feedback
        private AudioClip _sfxTap;
        private AudioClip _sfxCollect;
        private AudioClip _sfxBuildComplete;
        private AudioClip _sfxLevelUp;
        private EventSubscription _collectSub;
        private EventSubscription _buildCompleteSfxSub;
        private EventSubscription _upgradeStartedSub;
        private EventSubscription _emptyCellTappedSub;

        private void OnEnable()
        {
            _upgradeCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildingUpgradeCompleted);
            _demolishedSub = EventBus.Subscribe<BuildingDemolishedEvent>(OnBuildingDemolished);
            _doubleTapZoomSub = EventBus.Subscribe<BuildingDoubleTappedEvent>(OnDoubleTapZoom);
            _collectSub = EventBus.Subscribe<ResourceCollectedEvent>(OnResourceCollected);
            _buildCompleteSfxSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnUpgradeCompletedSfx);
            _upgradeStartedSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(OnUpgradeStarted);
            _buildingTappedSub = EventBus.Subscribe<BuildingTappedEvent>(OnBuildingTappedShowPopup);
            _emptyCellTappedSub = EventBus.Subscribe<EmptyCellTappedEvent>(OnEmptyCellTapped);
        }

        private void OnDisable()
        {
            _upgradeCompletedSub?.Dispose();
            _demolishedSub?.Dispose();
            _doubleTapZoomSub?.Dispose();
            _collectSub?.Dispose();
            _buildCompleteSfxSub?.Dispose();
            _upgradeStartedSub?.Dispose();
            _buildingTappedSub?.Dispose();
            _emptyCellTappedSub?.Dispose();
        }

        private void Start()
        {
            SetGridOverlayVisible(false);
            RegisterSceneBuildings();
            // P&C: Load SFX clips
            _sfxTap = Resources.Load<AudioClip>("Audio/SFX/sfx_btn_click");
            _sfxCollect = Resources.Load<AudioClip>("Audio/SFX/sfx_collect_resource");
            _sfxBuildComplete = Resources.Load<AudioClip>("Audio/SFX/sfx_building_complete");
            _sfxLevelUp = Resources.Load<AudioClip>("Audio/SFX/sfx_level_up");
            // Read initial zoom from content scale (set by generator, persisted in scene)
            if (contentContainer != null)
            {
                _currentZoom = contentContainer.localScale.x;
                _targetZoom = _currentZoom;
            }
            // P&C: Sort buildings by isometric depth (higher grid Y = closer to camera = higher sibling index)
            SortBuildingsByDepth();
            RefreshInstanceCountBadges();
            RefreshUpgradeIndicators();
            RefreshUpgradeArrows();
            CreateBuilderCountHUD();
            // P&C: Ambient tint and mini-map
            CreateAmbientTintOverlay();
            CreateMiniMap();
            // Center on stronghold after layout rebuild
            StartCoroutine(DelayedCenterOnStronghold());
            // P&C: Show "Welcome back" offline earnings banner
            StartCoroutine(ShowOfflineEarningsBanner());
        }

        /// <summary>P&C: Sort building GameObjects by isometric depth so front buildings overlap back ones.</summary>
        private void SortBuildingsByDepth()
        {
            if (buildingContainer == null) return;
            // Sort placements by grid Y+X (isometric depth: higher sum = closer to camera)
            _placements.Sort((a, b) =>
            {
                int depthA = a.GridOrigin.x + a.GridOrigin.y;
                int depthB = b.GridOrigin.x + b.GridOrigin.y;
                return depthA.CompareTo(depthB); // Lower depth first (further back)
            });
            // Reorder siblings to match
            for (int i = 0; i < _placements.Count; i++)
            {
                if (_placements[i].VisualGO != null)
                    _placements[i].VisualGO.transform.SetSiblingIndex(i);
            }
        }

        private IEnumerator DelayedCenterOnStronghold()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            CenterOnStronghold();
        }

        /// <summary>
        /// Center the scroll view on the stronghold area at startup.
        /// The stronghold is near content center (Y≈48 out of 2376), so (0.5, 0.5) is close enough.
        /// Direct anchoredPosition = 0 is the most reliable approach since normalizedPosition
        /// depends on viewport size being correct (which can fail on early frames).
        /// </summary>
        private void CenterOnStronghold()
        {
            if (scrollRect == null || contentContainer == null) return;
            contentContainer.anchoredPosition = Vector2.zero;
        }

        /// <summary>P&C: "Welcome back" banner showing simulated offline resource earnings.</summary>
        private IEnumerator ShowOfflineEarningsBanner()
        {
            yield return new WaitForSeconds(1.5f); // Wait for scene to settle

            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) yield break;

            // Simulate offline earnings (placeholder — real data comes from PlayFab)
            long grainEarned = rm.Grain > 0 ? (long)Mathf.Min(500, rm.Grain) : 100L;
            long ironEarned = rm.Iron > 0 ? (long)Mathf.Min(300, rm.Iron) : 50L;
            long stoneEarned = rm.Stone > 0 ? (long)Mathf.Min(300, rm.Stone) : 50L;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) yield break;

            var banner = new GameObject("OfflineEarningsBanner");
            banner.transform.SetParent(canvas.transform, false);
            var bannerRect = banner.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.08f, 0.35f);
            bannerRect.anchorMax = new Vector2(0.92f, 0.65f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;

            var bg = banner.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.12f, 0.95f);
            bg.raycastTarget = true;
            var outline = banner.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Title
            AddInfoPanelText(banner.transform, "Title", "\u2728 Welcome Back!", 16, FontStyle.Bold,
                new Color(1f, 0.88f, 0.35f),
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);

            // Resource earnings
            string earnings = $"While you were away, your empire earned:\n" +
                $"+{grainEarned} Grain  |  +{ironEarned} Iron  |  +{stoneEarned} Stone";
            AddInfoPanelText(banner.transform, "Earnings", earnings, 11, FontStyle.Normal,
                new Color(0.80f, 0.82f, 0.75f),
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.72f), TextAnchor.MiddleCenter);

            // Collect button
            var collectGO = new GameObject("CollectBtn");
            collectGO.transform.SetParent(banner.transform, false);
            var collectRect = collectGO.AddComponent<RectTransform>();
            collectRect.anchorMin = new Vector2(0.25f, 0.06f);
            collectRect.anchorMax = new Vector2(0.75f, 0.28f);
            collectRect.offsetMin = Vector2.zero;
            collectRect.offsetMax = Vector2.zero;
            var collectImg = collectGO.AddComponent<Image>();
            collectImg.color = new Color(0.15f, 0.60f, 0.25f, 0.92f);
            collectImg.raycastTarget = true;
            var collectOutline = collectGO.AddComponent<Outline>();
            collectOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.6f);
            collectOutline.effectDistance = new Vector2(0.8f, -0.8f);
            var collectBtn = collectGO.AddComponent<Button>();
            collectBtn.targetGraphic = collectImg;
            collectBtn.onClick.AddListener(() => { if (banner != null) Destroy(banner); });
            AddInfoPanelText(collectGO.transform, "Label", "COLLECT", 13, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = banner.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));

            // Auto-dismiss after 8 seconds
            yield return new WaitForSeconds(8f);
            if (banner != null) Destroy(banner);
        }

        /// <summary>P&C: Expanding circle ripple VFX when a building is tapped.</summary>
        private void SpawnTapRipple(GameObject building)
        {
            if (building == null) return;
            var ripple = new GameObject("TapRipple");
            ripple.transform.SetParent(building.transform, false);
            var rect = ripple.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.3f, 0.3f);
            rect.anchorMax = new Vector2(0.7f, 0.7f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = ripple.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.30f, 0.5f);
            img.raycastTarget = false;
            // Load radial gradient for circle shape
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) img.sprite = spr;
            StartCoroutine(AnimateTapRipple(ripple, rect, img));
        }

        private IEnumerator AnimateTapRipple(GameObject ripple, RectTransform rect, Image img)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration && ripple != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Expand from center
                float expand = Mathf.Lerp(0.3f, -0.2f, t);
                rect.anchorMin = new Vector2(expand, expand);
                rect.anchorMax = new Vector2(1f - expand, 1f - expand);
                // Fade out
                img.color = new Color(1f, 0.85f, 0.30f, 0.5f * (1f - t));
                yield return null;
            }
            if (ripple != null) Destroy(ripple);
        }

        /// <summary>
        /// P&C: Smoothly pan the camera to center on a specific building.
        /// Called from queue HUD slot tap to show the building being upgraded.
        /// </summary>
        public void CenterOnBuilding(string placedId)
        {
            if (scrollRect == null || contentContainer == null) return;
            CityBuildingPlacement target = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == placedId) { target = p; break; }
            }
            if (target?.VisualGO == null) return;

            var buildingRect = target.VisualGO.GetComponent<RectTransform>();
            if (buildingRect == null) return;

            // Target: building position in content space, scaled by current zoom
            Vector2 buildingPos = buildingRect.anchoredPosition * _currentZoom;
            // Move content so building is at viewport center
            contentContainer.anchoredPosition = -buildingPos;

            // Also select the building
            ShowBuildingFootprint(target);
            StartCoroutine(BounceBuilding(target.VisualGO.transform));
        }

        private void Update()
        {
            // Long press detection with P&C hold indicator
            if (_holdStarted && !_moveMode && !_isPinching)
            {
                _holdTimer += Time.deltaTime;
                UpdateHoldIndicator();
                if (_holdTimer >= LongPressTime)
                    TryEnterMoveMode();
            }
            else if (_holdIndicator != null)
            {
                DestroyHoldIndicator();
            }

            // P&C: Periodic upgrade arrow refresh
            _upgradeArrowRefreshTimer -= Time.deltaTime;
            if (_upgradeArrowRefreshTimer <= 0f)
            {
                _upgradeArrowRefreshTimer = UpgradeArrowRefreshInterval;
                RefreshUpgradeArrows();
                UpdateBuilderCountHUD();
                UpdatePowerRatingHUD();
                RefreshResourceCapWarnings();
                RefreshStrongholdUpgradeBanner();
                RefreshAllNotificationBadges();
            }

            // P&C: Pulse selection ring glow
            if (_selectionRing != null)
            {
                var ringImg = _selectionRing.GetComponent<Image>();
                if (ringImg != null)
                {
                    float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 3f);
                    ringImg.color = new Color(0.90f, 0.75f, 0.25f, pulse);
                }
            }

            // P&C: Pulse placement ghost transparency
            if (_placementMode && _placementGhost != null)
            {
                var gImg = _placementGhost.GetComponent<Image>();
                if (gImg != null)
                {
                    float pulse = 0.45f + 0.20f * Mathf.Sin(Time.time * 3f);
                    gImg.color = new Color(gImg.color.r, gImg.color.g, gImg.color.b, pulse);
                }
            }

            // Pinch-zoom (mobile multi-touch)
            _touchCount = Input.touchCount;
            if (_touchCount == 2 && !_moveMode)
            {
                var t0 = Input.GetTouch(0);
                var t1 = Input.GetTouch(1);
                float dist = Vector2.Distance(t0.position, t1.position);

                if (!_isPinching)
                {
                    _isPinching = true;
                    _lastPinchDistance = dist;
                    // Cancel any long-press in progress
                    _holdStarted = false;
                    _holdTimer = 0f;
                    // Disable scroll while pinching
                    if (scrollRect != null) scrollRect.enabled = false;
                }
                else
                {
                    float delta = dist - _lastPinchDistance;
                    float newZoom = Mathf.Clamp(_currentZoom + delta * ZoomSpeed, ZoomMin, ZoomMax);
                    ApplyZoom(newZoom, (t0.position + t1.position) * 0.5f);
                    _targetZoom = _currentZoom; // Sync target with direct pinch
                    _lastPinchDistance = dist;
                }
            }
            else if (_isPinching)
            {
                _isPinching = false;
                if (scrollRect != null && !_moveMode) scrollRect.enabled = true;
            }

            // Mouse scroll zoom (editor / desktop testing) — P&C: smooth interpolated
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f && !_moveMode && !_isPinching)
            {
                _targetZoom = Mathf.Clamp(_targetZoom + scroll * MouseScrollZoomSpeed, ZoomMin, ZoomMax);
                _zoomPivotScreen = Input.mousePosition;
            }

            // Smooth zoom interpolation
            if (!Mathf.Approximately(_currentZoom, _targetZoom))
            {
                float smoothed = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * ZoomLerpSpeed);
                if (Mathf.Abs(smoothed - _targetZoom) < 0.001f) smoothed = _targetZoom;
                ApplyZoom(smoothed, _zoomPivotScreen);
            }
        }

        private void ApplyZoom(float newZoom, Vector2 screenPivot)
        {
            if (contentContainer == null || Mathf.Approximately(newZoom, _currentZoom)) return;

            // Convert screen pivot to local content position before zoom
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentContainer, screenPivot, null, out var pivotBefore);

            _currentZoom = newZoom;
            contentContainer.localScale = Vector3.one * _currentZoom;

            // After scaling, the pivot point has moved. Adjust anchoredPosition to keep it stable.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentContainer, screenPivot, null, out var pivotAfter);

            var diff = pivotAfter - pivotBefore;
            contentContainer.anchoredPosition += diff * _currentZoom;

            // P&C: Hide detail labels at far zoom levels
            UpdateZoomDetailVisibility();
        }

        /// <summary>
        /// P&C: Hide level badges and name labels when zoomed out far
        /// to keep the view clean at strategic overview zoom levels.
        /// </summary>
        private void UpdateZoomDetailVisibility()
        {
            // P&C: 4-tier zoom detail levels — strategic overview → city view → close-up → inspection
            bool showFar = _currentZoom >= 0.55f;           // Upgrade progress bars (always important)
            bool showMedium = _currentZoom >= 0.7f;         // Arrows, count badges, category icons
            bool showClose = _currentZoom >= 1.0f;          // Level badges, names, production rates, buffs
            bool showInspection = _currentZoom >= 1.4f;     // Garrison labels, buff details
            bool showCategoryIcons = _currentZoom >= 0.6f && _currentZoom < 1.3f;

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var t = p.VisualGO.transform;

                // Level badge — close zoom only
                var badge = t.Find("LevelBadge");
                if (badge != null) badge.gameObject.SetActive(showClose);

                // Name label — close zoom only
                var nameLabel = t.Find("NameLabel");
                if (nameLabel != null) nameLabel.gameObject.SetActive(showClose);

                // P&C: Category mini-icons visible at medium zoom (hides at close to avoid clutter)
                var catIcon = t.Find("CategoryIcon");
                if (catIcon != null) catIcon.gameObject.SetActive(showCategoryIcons);

                // Upgrade arrow — medium+ zoom
                var arrow = t.Find("UpgradeArrow");
                if (arrow != null) arrow.gameObject.SetActive(showMedium);

                // Production rate label — close zoom only
                var prodRate = t.Find("ProductionRate");
                if (prodRate != null) prodRate.gameObject.SetActive(showClose);

                // Instance count badge — medium+ zoom
                var countBadge = t.Find("CountBadge");
                if (countBadge != null) countBadge.gameObject.SetActive(showMedium);

                // P&C: Buff indicators — close zoom only (small, need zoom to read)
                for (int i = 0; i < 3; i++)
                {
                    var buff = t.Find($"Buff_{i}");
                    if (buff != null) buff.gameObject.SetActive(showClose);
                }

                // P&C: Garrison labels — inspection zoom only (deep detail)
                var garrison = t.Find("GarrisonLabel");
                if (garrison != null) garrison.gameObject.SetActive(showInspection);

                // P&C: NEW badge — medium+ zoom (noticeable)
                var newBadge = t.Find("NewBadge");
                if (newBadge != null) newBadge.gameObject.SetActive(showMedium);

                // P&C: Scaffolding — always visible (important visual feedback)
                // Upgrade progress bar — far+ zoom (needs to be visible at overview)
                var progressBar = t.Find("UpgradeProgressBar");
                if (progressBar != null) progressBar.gameObject.SetActive(showFar);

                // Queue position label — medium+ zoom
                var queueLabel = t.Find("QueuePosLabel");
                if (queueLabel != null) queueLabel.gameObject.SetActive(showMedium);

                // P&C: Alliance flag — close zoom only
                var allianceFlag = t.Find("AllianceFlag");
                if (allianceFlag != null) allianceFlag.gameObject.SetActive(showClose);

                // P&C: Notification dot — medium+ zoom (important alerts)
                var notifDot = t.Find("NotifDot");
                if (notifDot != null) notifDot.gameObject.SetActive(showMedium);

                // P&C: VIP boost badge — close zoom only
                var vipBoost = t.Find("VIPBoost");
                if (vipBoost != null) vipBoost.gameObject.SetActive(showClose);

                // P&C: Construction dust — always visible when present
                var dust = t.Find("ConstructionDust");
                if (dust != null) dust.gameObject.SetActive(showFar);
            }

            // Builder HUD + Collect All button: always visible (screen-space UI)
        }

        /// <summary>P&C: On double-tap, smoothly zoom in and center on the building.</summary>
        private void OnDoubleTapZoom(BuildingDoubleTappedEvent evt)
        {
            CityBuildingPlacement target = null;
            foreach (var p in _placements)
                if (p.InstanceId == evt.InstanceId) { target = p; break; }
            if (target?.VisualGO == null || contentContainer == null) return;

            if (_smoothZoomCoroutine != null) StopCoroutine(_smoothZoomCoroutine);
            _smoothZoomCoroutine = StartCoroutine(SmoothZoomToBuilding(target));

            // P&C: Haptic feedback on double-tap
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif
        }

        private IEnumerator SmoothZoomToBuilding(CityBuildingPlacement target)
        {
            var buildingRect = target.VisualGO.GetComponent<RectTransform>();
            if (buildingRect == null) yield break;

            float startZoom = _currentZoom;
            float endZoom = Mathf.Max(startZoom, DoubleTapZoomTarget);
            Vector2 startPos = contentContainer.anchoredPosition;
            Vector2 targetBuildingPos = buildingRect.anchoredPosition * endZoom;
            Vector2 endPos = -targetBuildingPos;

            float elapsed = 0f;
            while (elapsed < SmoothZoomDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SmoothZoomDuration;
                // Ease-out cubic
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);

                float zoom = Mathf.Lerp(startZoom, endZoom, ease);
                _currentZoom = zoom;
                _targetZoom = endZoom;
                contentContainer.localScale = Vector3.one * zoom;

                Vector2 pos = Vector2.Lerp(startPos, endPos, ease);
                contentContainer.anchoredPosition = pos;

                UpdateZoomDetailVisibility();
                yield return null;
            }

            _currentZoom = endZoom;
            _targetZoom = endZoom;
            contentContainer.localScale = Vector3.one * endZoom;
            contentContainer.anchoredPosition = endPos;
            UpdateZoomDetailVisibility();
            _smoothZoomCoroutine = null;
        }

        /// <summary>P&C: Smooth zoom to target level centered on screen position (for empty-ground double-tap).</summary>
        private IEnumerator SmoothZoomToggle(float targetZoomLevel, Vector2 screenPos)
        {
            float startZoom = _currentZoom;
            Vector2 startPos = contentContainer.anchoredPosition;

            // Calculate end position to keep screen point stable
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentContainer, screenPos, null, out var pivotLocal);
            float elapsed = 0f;
            while (elapsed < SmoothZoomDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SmoothZoomDuration;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                float zoom = Mathf.Lerp(startZoom, targetZoomLevel, ease);
                _currentZoom = zoom;
                _targetZoom = targetZoomLevel;
                contentContainer.localScale = Vector3.one * zoom;

                // Keep pivot stable
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    contentContainer, screenPos, null, out var pivotNow);
                contentContainer.anchoredPosition += (pivotNow - pivotLocal) * zoom * 0.1f;

                UpdateZoomDetailVisibility();
                yield return null;
            }
            _currentZoom = targetZoomLevel;
            _targetZoom = targetZoomLevel;
            contentContainer.localScale = Vector3.one * targetZoomLevel;
            UpdateZoomDetailVisibility();
            _smoothZoomCoroutine = null;
        }

        /// <summary>P&C: Create a small category icon on a building for at-a-glance identification at medium zoom.</summary>
        private void EnsureCategoryIcon(CityBuildingPlacement placement)
        {
            if (placement?.VisualGO == null) return;
            if (placement.VisualGO.transform.Find("CategoryIcon") != null) return;
            if (!CategoryIcons.TryGetValue(placement.BuildingId, out var symbol)) return;

            var iconGO = new GameObject("CategoryIcon");
            iconGO.transform.SetParent(placement.VisualGO.transform, false);

            var rect = iconGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.35f, 0.02f);
            rect.anchorMax = new Vector2(0.65f, 0.18f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark pill background
            var bg = iconGO.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.14f, 0.75f);
            bg.raycastTarget = false;

            var textGO = new GameObject("Symbol");
            textGO.transform.SetParent(iconGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = symbol;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 10;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = CategoryIconColor;
            text.raycastTarget = false;

            // Start hidden — UpdateZoomDetailVisibility controls visibility
            bool visible = _currentZoom >= 0.6f && _currentZoom < 1.3f;
            iconGO.SetActive(visible);
        }

        /// <summary>P&C: Gently nudge viewport toward tapped building without zoom change.</summary>
        private void SoftCenterOnBuilding(CityBuildingPlacement target)
        {
            if (target?.VisualGO == null || contentContainer == null) return;
            if (_softCenterCoroutine != null) StopCoroutine(_softCenterCoroutine);
            _softCenterCoroutine = StartCoroutine(SoftCenterRoutine(target));
        }

        private IEnumerator SoftCenterRoutine(CityBuildingPlacement target)
        {
            var buildingRect = target.VisualGO.GetComponent<RectTransform>();
            if (buildingRect == null) yield break;

            Vector2 startPos = contentContainer.anchoredPosition;
            Vector2 fullCenter = -(buildingRect.anchoredPosition * _currentZoom);
            // Only move SoftCenterStrength (60%) toward center — subtle nudge
            Vector2 endPos = Vector2.Lerp(startPos, fullCenter, SoftCenterStrength);

            float elapsed = 0f;
            while (elapsed < SoftCenterDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / SoftCenterDuration;
                float ease = 1f - (1f - t) * (1f - t); // ease-out quadratic
                contentContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
                yield return null;
            }
            contentContainer.anchoredPosition = endPos;
            _softCenterCoroutine = null;
        }

        /// <summary>P&C: Play SFX clip via AudioManager.</summary>
        private void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            if (ServiceLocator.TryGet<AudioManager>(out var audio))
                audio.PlaySfx(clip);
        }

        /// <summary>Current zoom level (1.0 = default).</summary>
        public float CurrentZoom => _currentZoom;

        /// <summary>Reset zoom to default (1x).</summary>
        public void ResetZoom()
        {
            _currentZoom = 1f;
            if (contentContainer != null)
                contentContainer.localScale = Vector3.one;
        }

        // ====================================================================
        // Isometric coordinate conversion (2:1 projection)
        // ====================================================================

        /// <summary>
        /// Convert a grid cell position to local UI position (isometric).
        /// </summary>
        public static Vector2 GridToLocal(Vector2Int gridPos)
        {
            float isoX = (gridPos.x - gridPos.y) * HalfW;
            float isoY = (gridPos.x + gridPos.y) * HalfH - IsoCenterY;
            return new Vector2(isoX, isoY);
        }

        /// <summary>
        /// Convert the center of a building footprint to local UI position (isometric).
        /// </summary>
        public static Vector2 GridToLocalCenter(Vector2Int origin, Vector2Int size)
        {
            float cx = origin.x + size.x * 0.5f;
            float cy = origin.y + size.y * 0.5f;
            float isoX = (cx - cy) * HalfW;
            float isoY = (cx + cy) * HalfH - IsoCenterY;
            return new Vector2(isoX, isoY);
        }

        /// <summary>
        /// Convert a local UI position back to the nearest grid cell (isometric inverse).
        /// </summary>
        public static Vector2Int LocalToGrid(Vector2 localPos)
        {
            float adjustedY = localPos.y + IsoCenterY;
            float gx = (localPos.x / HalfW + adjustedY / HalfH) * 0.5f;
            float gy = (adjustedY / HalfH - localPos.x / HalfW) * 0.5f;
            return new Vector2Int(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
        }

        /// <summary>
        /// Screen size for a building's isometric footprint.
        /// Width = diamond width, Height = diamond width * 1.25 (building extends upward).
        /// </summary>
        public static Vector2 FootprintScreenSize(Vector2Int size)
        {
            // Buildings slightly wider than grid footprint for P&C-style overlap
            float w = (size.x + size.y) * HalfW * 1.2f;
            float h = w * 1.8f;
            return new Vector2(w, h);
        }

        // ====================================================================
        // Grid overlay visibility
        // ====================================================================

        public void SetGridOverlayVisible(bool visible)
        {
            if (gridOverlay == null) return;
            // Grid overlay always stays active — toggle between faint texture and bright move-mode
            gridOverlay.SetActive(true);
            var img = gridOverlay.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
                img.color = visible
                    ? new Color(0.35f, 0.55f, 0.30f, 0.20f)  // bright move-mode
                    : new Color(0.35f, 0.55f, 0.30f, 0.06f);  // faint background texture
        }

        // ====================================================================
        // Occupancy helpers
        // ====================================================================

        public bool IsCellOccupied(Vector2Int pos) => _occupancy.ContainsKey(pos);

        public string GetOccupant(Vector2Int pos) =>
            _occupancy.TryGetValue(pos, out var id) ? id : null;

        // ====================================================================
        // Building placement
        // ====================================================================

        public CityBuildingPlacement PlaceBuildingOnGrid(string buildingId, string instanceId, int tier, Vector2Int origin)
        {
            if (!BuildingSizes.TryGetValue(buildingId, out var size))
                size = new Vector2Int(2, 2);

            if (!CanPlaceAt(origin, size, null))
            {
                Debug.LogWarning($"[CityGrid] Cannot place {instanceId} at {origin}");
                return null;
            }

            var placement = new CityBuildingPlacement
            {
                InstanceId = instanceId,
                BuildingId = buildingId,
                Tier = tier,
                GridOrigin = origin,
                Size = size,
            };

            MarkCells(origin, size, instanceId);
            _placements.Add(placement);
            CreateBuildingVisual(placement);
            EnsureCategoryIcon(placement);

            return placement;
        }

        public bool CanPlaceAt(Vector2Int origin, Vector2Int size, CityBuildingPlacement exclude)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
            {
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    if (x < PlayableMinX || x >= PlayableMaxX || y < PlayableMinY || y >= PlayableMaxY)
                        return false;

                    var pos = new Vector2Int(x, y);
                    if (_occupancy.TryGetValue(pos, out var occupant))
                    {
                        if (exclude == null || occupant != exclude.InstanceId)
                            return false;
                    }
                }
            }
            return true;
        }

        private void MarkCells(Vector2Int origin, Vector2Int size, string instanceId)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
                for (int y = origin.y; y < origin.y + size.y; y++)
                    _occupancy[new Vector2Int(x, y)] = instanceId;
        }

        private void ClearCells(Vector2Int origin, Vector2Int size)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
                for (int y = origin.y; y < origin.y + size.y; y++)
                    _occupancy.Remove(new Vector2Int(x, y));
        }

        private void CreateBuildingVisual(CityBuildingPlacement placement)
        {
            if (buildingContainer == null) return;

            string goName = $"Building_{placement.InstanceId}";
            var existing = buildingContainer.Find(goName);
            if (existing != null) Object.Destroy(existing.gameObject);

            var go = new GameObject(goName);
            go.transform.SetParent(buildingContainer, false);

            var rect = go.AddComponent<RectTransform>();
            PositionBuildingRect(rect, placement);

            // P&C: Drop shadow behind building for depth
            CreateBuildingShadow(go, placement.Size);

            var img = go.AddComponent<Image>();
            string spriteName = $"{placement.BuildingId}_t{placement.Tier}";
            var sprite = Resources.Load<Sprite>($"Buildings/{spriteName}");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            img.raycastTarget = true;

            CreateLevelBadge(go, placement.Tier);
            CreateBuildingCountBadge(go, placement.BuildingId);

            // P&C-style production rate label on resource buildings
            CreateProductionLabel(go, placement.BuildingId, placement.Tier);

            // P&C: Garrison display on military buildings
            CreateGarrisonLabel(go, placement.BuildingId, placement.Tier);

            // P&C: Stronghold gets a special golden glow aura
            if (placement.BuildingId == "stronghold")
                CreateStrongholdGlow(go);

            // P&C: Buff indicator icons (research/alliance bonuses)
            CreateBuffIndicators(go, placement.BuildingId);

            // P&C: Alliance territory flag on military/defense buildings
            CreateAllianceFlag(go, placement.BuildingId);

            // P&C: VIP boost indicator on resource/military buildings
            AddVIPBoostIndicator(go, placement.BuildingId);

            // P&C: Event notification badges (red dot for pending actions)
            RefreshNotificationBadge(go, placement);

            // P&C: Subtle idle breathing animation for life
            StartCoroutine(IdleBreathAnimation(go, placement.BuildingId));

            placement.VisualGO = go;
        }

        /// <summary>P&C: Subtle oval drop shadow behind each building for depth.</summary>
        private void CreateBuildingShadow(GameObject building, Vector2Int size)
        {
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(building.transform, false);
            shadowGO.transform.SetAsFirstSibling(); // Behind building sprite

            var rect = shadowGO.AddComponent<RectTransform>();
            // Shadow is slightly wider and sits at the bottom of the building
            rect.anchorMin = new Vector2(-0.05f, -0.08f);
            rect.anchorMax = new Vector2(1.05f, 0.15f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = shadowGO.AddComponent<Image>();
            img.color = new Color(0.02f, 0.01f, 0.05f, 0.30f);
            img.raycastTarget = false;

            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) img.sprite = spr;
        }

        /// <summary>P&C: Subtle idle breathing animation — buildings gently scale-pulse for life.</summary>
        private IEnumerator IdleBreathAnimation(GameObject building, string buildingId)
        {
            if (building == null) yield break;

            // Vary intensity by building type — stronghold breathes more
            float intensity = buildingId == "stronghold" ? 0.015f : 0.008f;
            // Random phase offset so buildings don't pulse in sync
            float phase = Random.Range(0f, Mathf.PI * 2f);
            // Slower speed for subtle feel
            float speed = Random.Range(1.2f, 1.8f);

            var rect = building.GetComponent<RectTransform>();
            if (rect == null) yield break;

            Vector3 baseScale = rect.localScale;
            while (building != null)
            {
                float breath = 1f + intensity * Mathf.Sin(Time.time * speed + phase);
                rect.localScale = baseScale * breath;
                yield return null;
            }
        }

        /// <summary>P&C: Stronghold gets a special multi-layer golden glow aura that pulses.</summary>
        private void CreateStrongholdGlow(GameObject building)
        {
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif

            // Layer 1: Large outer warm glow
            var glow1 = new GameObject("StrongholdGlow_Outer");
            glow1.transform.SetParent(building.transform, false);
            glow1.transform.SetAsFirstSibling();
            var rect1 = glow1.AddComponent<RectTransform>();
            rect1.anchorMin = new Vector2(-0.25f, -0.15f);
            rect1.anchorMax = new Vector2(1.25f, 1.15f);
            rect1.offsetMin = Vector2.zero;
            rect1.offsetMax = Vector2.zero;
            var img1 = glow1.AddComponent<Image>();
            img1.color = new Color(0.85f, 0.65f, 0.15f, 0.12f);
            img1.raycastTarget = false;
            if (spr != null) img1.sprite = spr;

            // Layer 2: Inner bright gold glow
            var glow2 = new GameObject("StrongholdGlow_Inner");
            glow2.transform.SetParent(building.transform, false);
            glow2.transform.SetSiblingIndex(1);
            var rect2 = glow2.AddComponent<RectTransform>();
            rect2.anchorMin = new Vector2(-0.10f, -0.05f);
            rect2.anchorMax = new Vector2(1.10f, 1.05f);
            rect2.offsetMin = Vector2.zero;
            rect2.offsetMax = Vector2.zero;
            var img2 = glow2.AddComponent<Image>();
            img2.color = new Color(1f, 0.85f, 0.30f, 0.08f);
            img2.raycastTarget = false;
            if (spr != null) img2.sprite = spr;

            StartCoroutine(PulseStrongholdGlow(img1, img2));
        }

        private IEnumerator PulseStrongholdGlow(Image outer, Image inner)
        {
            float phase = 0f;
            while (outer != null && inner != null)
            {
                phase += Time.deltaTime * 1.2f;
                float outerAlpha = 0.10f + 0.06f * Mathf.Sin(phase);
                float innerAlpha = 0.06f + 0.04f * Mathf.Sin(phase * 1.5f + 1f);
                outer.color = new Color(0.85f, 0.65f, 0.15f, outerAlpha);
                inner.color = new Color(1f, 0.85f, 0.30f, innerAlpha);
                yield return null;
            }
        }

        /// <summary>P&C: Category-specific popup header color.</summary>
        private static Color GetCategoryHeaderColor(string buildingId)
        {
            return buildingId switch
            {
                "stronghold" => new Color(1f, 0.85f, 0.25f),
                "barracks" or "training_ground" or "armory" => new Color(0.90f, 0.40f, 0.35f),
                "wall" or "watch_tower" => new Color(0.70f, 0.70f, 0.75f),
                "grain_farm" or "iron_mine" or "stone_quarry" => new Color(0.60f, 0.85f, 0.40f),
                "arcane_tower" or "enchanting_tower" or "hero_shrine" => new Color(0.70f, 0.45f, 1f),
                "academy" or "library" or "archive" or "observatory" or "laboratory" => new Color(0.45f, 0.70f, 1f),
                "guild_hall" or "embassy" => new Color(1f, 0.75f, 0.40f),
                "marketplace" or "forge" => new Color(0.85f, 0.60f, 0.30f),
                _ => new Color(1f, 0.90f, 0.50f)
            };
        }

        /// <summary>P&C: Category-specific bounce intensity (stronghold = bigger bounce).</summary>
        private static float GetCategoryBounceScale(string buildingId)
        {
            return buildingId switch
            {
                "stronghold" => 0.12f,    // Bigger, more dramatic
                "barracks" or "training_ground" or "armory" => 0.09f,
                _ => 0.08f                 // Standard bounce
            };
        }

        /// <summary>P&C: Show "x3" count badge on buildings with multiple instances of the same type.</summary>
        private void RefreshInstanceCountBadges()
        {
            // Count instances per building type
            var counts = new Dictionary<string, int>();
            foreach (var p in _placements)
            {
                if (!counts.ContainsKey(p.BuildingId)) counts[p.BuildingId] = 0;
                counts[p.BuildingId]++;
            }

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var existing = p.VisualGO.transform.Find("CountBadge");

                if (counts.TryGetValue(p.BuildingId, out int count) && count > 1)
                {
                    if (existing == null)
                        CreateCountBadge(p.VisualGO, count);
                    else
                    {
                        var txt = existing.GetComponentInChildren<Text>();
                        if (txt != null) txt.text = $"x{count}";
                    }
                }
                else if (existing != null)
                {
                    Destroy(existing.gameObject);
                }
            }
        }

        private void CreateCountBadge(GameObject building, int count)
        {
            var badgeGO = new GameObject("CountBadge");
            badgeGO.transform.SetParent(building.transform, false);

            var rect = badgeGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.70f, 0.82f);
            rect.anchorMax = new Vector2(0.98f, 1.0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badgeGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.10f, 0.25f, 0.85f);
            bg.raycastTarget = false;
            var outline = badgeGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.70f, 0.60f, 0.40f, 0.7f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            var textGO = new GameObject("CountText");
            textGO.transform.SetParent(badgeGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = $"x{count}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.90f, 0.80f, 0.55f, 1f);
            text.raycastTarget = false;
        }

        private static readonly Dictionary<string, (string Name, Color Tint)> ResourceBuildingTypes = new()
        {
            { "grain_farm", ("Grain", new Color(0.95f, 0.85f, 0.30f, 1f)) },   // yellow
            { "iron_mine", ("Iron", new Color(0.70f, 0.75f, 0.85f, 1f)) },      // silver-blue
            { "stone_quarry", ("Stone", new Color(0.75f, 0.68f, 0.55f, 1f)) },  // tan
            { "arcane_tower", ("Arcane", new Color(0.65f, 0.45f, 0.90f, 1f)) }  // purple
        };

        private void CreateProductionLabel(GameObject parent, string buildingId, int tier)
        {
            if (!ResourceBuildingTypes.TryGetValue(buildingId, out var resInfo)) return;

            int rate = (tier + 1) * 250; // base production per hour per tier
            string rateText = rate >= 1000 ? $"+{rate / 1000f:F1}K/hr" : $"+{rate}/hr";

            var labelGO = new GameObject("ProductionRate");
            labelGO.transform.SetParent(parent.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(-0.05f, 0.85f);
            labelRect.anchorMax = new Vector2(0.60f, 1.02f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Dark semi-transparent bg pill
            var bgImg = labelGO.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.04f, 0.10f, 0.78f);
            bgImg.raycastTarget = false;
            // Subtle resource-colored border
            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor = new Color(resInfo.Tint.r, resInfo.Tint.g, resInfo.Tint.b, 0.50f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            // Small resource color dot on left
            var dotGO = new GameObject("ResDot");
            dotGO.transform.SetParent(labelGO.transform, false);
            var dotRect = dotGO.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.04f, 0.22f);
            dotRect.anchorMax = new Vector2(0.16f, 0.78f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.color = resInfo.Tint;
            dotImg.raycastTarget = false;

            var textGO = new GameObject("RateText");
            textGO.transform.SetParent(labelGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.18f, 0f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = rateText;
            text.fontSize = 9;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = resInfo.Tint;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        /// <summary>P&C: Garrison troop count label on military buildings.</summary>
        private void CreateGarrisonLabel(GameObject parent, string buildingId, int tier)
        {
            bool isMilitary = buildingId == "barracks" || buildingId == "training_ground" || buildingId == "armory";
            if (!isMilitary) return;

            int troops = buildingId switch
            {
                "barracks" => 500 * (tier + 1),
                "training_ground" => 300 * (tier + 1),
                "armory" => 200 * (tier + 1),
                _ => 0
            };
            string troopStr = troops >= 1000 ? $"{troops / 1000f:F1}K" : $"{troops}";

            var labelGO = new GameObject("GarrisonLabel");
            labelGO.transform.SetParent(parent.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.40f, 0.85f);
            labelRect.anchorMax = new Vector2(1.05f, 1.02f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var bgImg = labelGO.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.04f, 0.10f, 0.78f);
            bgImg.raycastTarget = false;
            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.30f, 0.25f, 0.50f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            // Sword icon
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(labelGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.10f);
            iconRect.anchorMax = new Vector2(0.20f, 0.90f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = "\u2694"; // ⚔
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 9;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = new Color(0.90f, 0.40f, 0.35f);
            iconText.raycastTarget = false;

            var textGO = new GameObject("TroopText");
            textGO.transform.SetParent(labelGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.20f, 0f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = troopStr;
            text.fontSize = 9;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.90f, 0.40f, 0.35f);
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        private void CreateLevelBadge(GameObject parent, int tier)
        {
            var badge = new GameObject("LevelBadge");
            badge.transform.SetParent(parent.transform, false);
            var badgeRect = badge.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.65f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0.15f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            // Gold outer frame
            var badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(0.78f, 0.62f, 0.22f, 0.95f);
            badgeBg.raycastTarget = false;

            // Dark inner fill
            var innerGO = new GameObject("InnerFill");
            innerGO.transform.SetParent(badge.transform, false);
            var innerRect = innerGO.AddComponent<RectTransform>();
            innerRect.anchorMin = new Vector2(0.06f, 0.10f);
            innerRect.anchorMax = new Vector2(0.94f, 0.90f);
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            var innerImg = innerGO.AddComponent<Image>();
            innerImg.color = new Color(0.08f, 0.05f, 0.14f, 0.92f);
            innerImg.raycastTarget = false;

            // Level text — warm gold, bold
            var lvlGO = new GameObject("LvlText");
            lvlGO.transform.SetParent(badge.transform, false);
            var lvlRect = lvlGO.AddComponent<RectTransform>();
            lvlRect.anchorMin = Vector2.zero;
            lvlRect.anchorMax = Vector2.one;
            lvlRect.offsetMin = Vector2.zero;
            lvlRect.offsetMax = Vector2.zero;
            var lvlText = lvlGO.AddComponent<Text>();
            lvlText.text = tier > 1 ? $"★{tier}" : $"{tier}";
            lvlText.alignment = TextAnchor.MiddleCenter;
            lvlText.fontSize = 11;
            lvlText.color = new Color(0.95f, 0.85f, 0.50f, 1f);
            lvlText.fontStyle = FontStyle.Bold;
            lvlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lvlText.raycastTarget = false;
            var shadow = lvlGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(0.8f, -0.8f);
        }

        /// <summary>P&C: Small count badge showing "#/max" for building types that allow multiples.</summary>
        private void CreateBuildingCountBadge(GameObject parent, string buildingId)
        {
            if (!MaxBuildingCountPerType.TryGetValue(buildingId, out int maxCount)) return;
            if (maxCount <= 1) return;

            // Count how many of this type exist
            int current = 0;
            foreach (var p in _placements)
            {
                if (p.BuildingId == buildingId) current++;
            }

            var badge = new GameObject("CountBadge");
            badge.transform.SetParent(parent.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.35f, 0.13f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.10f, 0.20f, 0.88f);
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.6f, 0.5f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = $"{current}/{maxCount}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = current >= maxCount ? new Color(0.85f, 0.4f, 0.3f) : new Color(0.70f, 0.75f, 0.80f);
            text.raycastTarget = false;
        }

        /// <summary>P&C: Temporary "NEW" badge that appears after upgrade and fades after 10s.</summary>
        /// <summary>P&C: Small buff icons on buildings that benefit from research/alliance bonuses.</summary>
        private void CreateBuffIndicators(GameObject building, string buildingId)
        {
            // Determine which buffs apply to this building type
            var buffs = new List<(string Icon, Color Tint)>();

            // Research buffs (simulated — real data from ResearchManager)
            if (ServiceLocator.TryGet<ResearchManager>(out var rm))
            {
                // Check if any relevant research is complete
                if (buildingId == "grain_farm" || buildingId == "iron_mine" || buildingId == "stone_quarry")
                    buffs.Add(("\u2B06", new Color(0.40f, 0.85f, 0.45f))); // ⬆ green = production boost
                if (buildingId == "barracks" || buildingId == "training_ground")
                    buffs.Add(("\u2694", new Color(0.90f, 0.45f, 0.35f))); // ⚔ red = combat boost
            }

            if (buffs.Count == 0) return;

            for (int i = 0; i < buffs.Count && i < 3; i++)
            {
                var (icon, tint) = buffs[i];
                var buffGO = new GameObject($"Buff_{i}");
                buffGO.transform.SetParent(building.transform, false);
                var rect = buffGO.AddComponent<RectTransform>();
                float x0 = 0.02f + i * 0.12f;
                rect.anchorMin = new Vector2(x0, 0.14f);
                rect.anchorMax = new Vector2(x0 + 0.10f, 0.26f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var bg = buffGO.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.06f, 0.14f, 0.75f);
                bg.raycastTarget = false;

                var textGO = new GameObject("Icon");
                textGO.transform.SetParent(buffGO.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGO.AddComponent<Text>();
                text.text = icon;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 8;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = tint;
                text.raycastTarget = false;
            }
        }

        /// <summary>P&C: Alliance shield flag on military/defense/social buildings, top-left corner.</summary>
        private void CreateAllianceFlag(GameObject building, string buildingId)
        {
            // Only show on relevant building types (military, defense, guild hall, embassy)
            bool isMilitary = buildingId == "barracks" || buildingId == "training_ground" || buildingId == "armory";
            bool isDefense = buildingId == "wall" || buildingId == "watch_tower";
            bool isSocial = buildingId == "guild_hall" || buildingId == "embassy";
            if (!isMilitary && !isDefense && !isSocial) return;

            var flagGO = new GameObject("AllianceFlag");
            flagGO.transform.SetParent(building.transform, false);
            var flagRect = flagGO.AddComponent<RectTransform>();
            flagRect.anchorMin = new Vector2(0.0f, 0.72f);
            flagRect.anchorMax = new Vector2(0.16f, 0.88f);
            flagRect.offsetMin = Vector2.zero;
            flagRect.offsetMax = Vector2.zero;

            // Shield background
            var shieldBg = flagGO.AddComponent<Image>();
            shieldBg.color = new Color(0.15f, 0.30f, 0.65f, 0.85f);
            shieldBg.raycastTarget = false;
            var shieldOutline = flagGO.AddComponent<Outline>();
            shieldOutline.effectColor = new Color(0.85f, 0.68f, 0.20f, 0.6f);
            shieldOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // Alliance emblem (simplified — unicode shield)
            var emblGO = new GameObject("Emblem");
            emblGO.transform.SetParent(flagGO.transform, false);
            var emblRect = emblGO.AddComponent<RectTransform>();
            emblRect.anchorMin = Vector2.zero;
            emblRect.anchorMax = Vector2.one;
            emblRect.offsetMin = Vector2.zero;
            emblRect.offsetMax = Vector2.zero;
            var emblText = emblGO.AddComponent<Text>();
            emblText.text = "\u2694"; // ⚔ — alliance emblem
            emblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emblText.fontSize = 7;
            emblText.alignment = TextAnchor.MiddleCenter;
            emblText.color = new Color(0.95f, 0.85f, 0.40f);
            emblText.raycastTarget = false;
        }

        /// <summary>P&C: Refresh notification badges on all buildings (periodic).</summary>
        private void RefreshAllNotificationBadges()
        {
            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                RefreshNotificationBadge(p.VisualGO, p);
            }
        }

        /// <summary>
        /// P&C: Show/hide a red notification dot on buildings with pending actions.
        /// - Academy/Laboratory: active research completed
        /// - Barracks/Training: troops finished training (simulated)
        /// - Marketplace: trade offer available
        /// - Resource buildings: vault near capacity
        /// </summary>
        private void RefreshNotificationBadge(GameObject building, CityBuildingPlacement placement)
        {
            bool hasNotification = false;
            string notifText = "!";

            // Resource buildings: check if vault is near cap (>90%)
            if (placement.BuildingId == "grain_farm" || placement.BuildingId == "iron_mine" ||
                placement.BuildingId == "stone_quarry" || placement.BuildingId == "arcane_tower")
            {
                if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                {
                    float ratio = placement.BuildingId switch
                    {
                        "grain_farm" => rm.MaxGrain > 0 ? (float)rm.Grain / rm.MaxGrain : 0,
                        "iron_mine" => rm.MaxIron > 0 ? (float)rm.Iron / rm.MaxIron : 0,
                        "stone_quarry" => rm.MaxStone > 0 ? (float)rm.Stone / rm.MaxStone : 0,
                        "arcane_tower" => rm.MaxArcaneEssence > 0 ? (float)rm.ArcaneEssence / rm.MaxArcaneEssence : 0,
                        _ => 0
                    };
                    if (ratio >= 0.90f)
                    {
                        hasNotification = true;
                        notifText = "!";
                    }
                }
            }

            // Academy/Laboratory: show notification when no research is active (idle)
            if (placement.BuildingId == "academy" || placement.BuildingId == "laboratory")
            {
                if (ServiceLocator.TryGet<ResearchManager>(out var resM))
                {
                    if (resM.ResearchQueue.Count == 0)
                    {
                        hasNotification = true;
                        notifText = "?";
                    }
                }
            }

            // Barracks/Training: simulated troop readiness notification
            if (placement.BuildingId == "barracks" || placement.BuildingId == "training_ground")
            {
                // Simulated: show notification periodically (every ~40s in-game)
                float cycle = (Time.time + placement.InstanceId.GetHashCode() % 100) % 40f;
                if (cycle < 10f)
                {
                    hasNotification = true;
                    notifText = "\u2694"; // ⚔
                }
            }

            // Marketplace: simulated trade available
            if (placement.BuildingId == "marketplace")
            {
                float cycle = (Time.time + 17f) % 60f;
                if (cycle < 15f)
                {
                    hasNotification = true;
                    notifText = "$";
                }
            }

            // Find or create/destroy the notification dot
            var existingDot = building.transform.Find("NotifDot");
            if (!hasNotification)
            {
                if (existingDot != null) existingDot.gameObject.SetActive(false);
                return;
            }

            if (existingDot != null)
            {
                existingDot.gameObject.SetActive(true);
                var dotText = existingDot.GetComponentInChildren<Text>();
                if (dotText != null) dotText.text = notifText;
                return;
            }

            // Create notification dot
            var dotGO = new GameObject("NotifDot");
            dotGO.transform.SetParent(building.transform, false);
            var dotRect = dotGO.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.78f, 0.82f);
            dotRect.anchorMax = new Vector2(0.98f, 0.98f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;

            var dotBg = dotGO.AddComponent<Image>();
            dotBg.color = new Color(0.90f, 0.20f, 0.15f, 0.92f);
            dotBg.raycastTarget = false;
            var dotOutline = dotGO.AddComponent<Outline>();
            dotOutline.effectColor = new Color(0.5f, 0.1f, 0.1f, 0.6f);
            dotOutline.effectDistance = new Vector2(0.6f, -0.6f);

            var dotTextGO = new GameObject("Text");
            dotTextGO.transform.SetParent(dotGO.transform, false);
            var dtRect = dotTextGO.AddComponent<RectTransform>();
            dtRect.anchorMin = Vector2.zero;
            dtRect.anchorMax = Vector2.one;
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = Vector2.zero;
            var dt = dotTextGO.AddComponent<Text>();
            dt.text = notifText;
            dt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dt.fontSize = 8;
            dt.fontStyle = FontStyle.Bold;
            dt.alignment = TextAnchor.MiddleCenter;
            dt.color = Color.white;
            dt.raycastTarget = false;
        }

        private void CreateNewBadge(GameObject building)
        {
            // Remove existing NEW badge if any
            var existing = building.transform.Find("NewBadge");
            if (existing != null) Destroy(existing.gameObject);

            var badge = new GameObject("NewBadge");
            badge.transform.SetParent(building.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.60f, 0.80f);
            rect.anchorMax = new Vector2(0.95f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.90f, 0.20f, 0.15f, 0.95f);
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.5f, 0.3f, 0.6f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = "NEW";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            StartCoroutine(FadeAndDestroyNewBadge(badge));
        }

        private IEnumerator FadeAndDestroyNewBadge(GameObject badge)
        {
            // Stay visible for 8 seconds
            yield return new WaitForSeconds(8f);
            if (badge == null) yield break;

            // Fade out over 2 seconds
            var cg = badge.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < 2f && badge != null)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / 2f);
                yield return null;
            }
            if (badge != null) Destroy(badge);
        }

        private void PositionBuildingRect(RectTransform rect, CityBuildingPlacement placement)
        {
            var center = GridToLocalCenter(placement.GridOrigin, placement.Size);
            var screenSize = FootprintScreenSize(placement.Size);
            rect.anchoredPosition = center;
            rect.sizeDelta = screenSize;
        }

        // ====================================================================
        // Register buildings already in scene (placed by editor generator)
        // ====================================================================

        private void RegisterSceneBuildings()
        {
            if (buildingContainer == null) return;

            for (int i = 0; i < buildingContainer.childCount; i++)
            {
                var child = buildingContainer.GetChild(i);
                if (!child.name.StartsWith("Building_")) continue;

                string instanceId = child.name.Substring("Building_".Length);
                int lastUnderscore = instanceId.LastIndexOf('_');
                string buildingId = lastUnderscore > 0 ? instanceId.Substring(0, lastUnderscore) : instanceId;

                if (!BuildingSizes.TryGetValue(buildingId, out var size))
                    size = new Vector2Int(2, 2);

                var rect = child.GetComponent<RectTransform>();
                if (rect == null) continue;

                // Reverse-calculate grid origin from isometric screen position
                Vector2 screenPos = rect.anchoredPosition;
                // screenPos is the center of the footprint, so we need to find the origin
                // center = GridToLocalCenter(origin, size)
                // We solve: origin = LocalToGrid(screenPos) adjusted for size offset
                float adjustedY = screenPos.y + IsoCenterY;
                float gcx = (screenPos.x / HalfW + adjustedY / HalfH) * 0.5f;
                float gcy = (adjustedY / HalfH - screenPos.x / HalfW) * 0.5f;
                int gx = Mathf.RoundToInt(gcx - size.x * 0.5f);
                int gy = Mathf.RoundToInt(gcy - size.y * 0.5f);

                // Parse tier from LevelBadge text
                int tier = 1;
                var badgeText = child.GetComponentInChildren<Text>();
                if (badgeText != null)
                {
                    // Badge text is now just a number or star+number (no 'Lv.' prefix)
                    string tierStr = badgeText.text.TrimStart('★');
                    int.TryParse(tierStr, out tier);
                }

                var placement = new CityBuildingPlacement
                {
                    InstanceId = instanceId,
                    BuildingId = buildingId,
                    Tier = tier,
                    GridOrigin = new Vector2Int(gx, gy),
                    Size = size,
                    VisualGO = child.gameObject,
                };

                // Only mark cells that aren't already occupied (handles overlapping layout)
                bool hasOverlap = false;
                for (int cx = placement.GridOrigin.x; cx < placement.GridOrigin.x + size.x; cx++)
                {
                    for (int cy = placement.GridOrigin.y; cy < placement.GridOrigin.y + size.y; cy++)
                    {
                        var cell = new Vector2Int(cx, cy);
                        if (_occupancy.ContainsKey(cell))
                        {
                            if (!hasOverlap)
                            {
                                Debug.LogWarning($"[CityGrid] Building {instanceId} overlaps at cell {cell} " +
                                    $"(occupied by {_occupancy[cell]}). Registering without claiming overlapping cells.");
                                hasOverlap = true;
                            }
                        }
                        else
                        {
                            _occupancy[cell] = instanceId;
                        }
                    }
                }
                _placements.Add(placement);
                EnsureCategoryIcon(placement);
            }
        }

        // ====================================================================
        // P&C: Update building visual on upgrade completion
        // ====================================================================

        private void OnBuildingUpgradeCompleted(BuildingUpgradeCompletedEvent evt)
        {
            CityBuildingPlacement placement = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == evt.PlacedId)
                {
                    placement = p;
                    break;
                }
            }
            if (placement == null) return;

            // Update tier on the placement data
            placement.Tier = evt.NewTier;

            // Swap the sprite to the new tier
            if (placement.VisualGO != null)
            {
                var img = placement.VisualGO.GetComponent<Image>();
                if (img != null)
                {
                    string spriteName = $"{placement.BuildingId}_t{evt.NewTier}";
                    var sprite = Resources.Load<Sprite>($"Buildings/{spriteName}");
                    #if UNITY_EDITOR
                    if (sprite == null)
                        sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                            $"Assets/Art/Buildings/{placement.BuildingId}_t{evt.NewTier}.png");
                    #endif
                    if (sprite != null)
                    {
                        // P&C: Smooth crossfade to new tier sprite
                        StartCoroutine(CrossfadeSprite(img, sprite));
                    }
                }

                // Update level badge text
                var badgeText = FindLevelBadgeText(placement.VisualGO.transform);
                if (badgeText != null)
                    badgeText.text = evt.NewTier > 1 ? $"\u2605{evt.NewTier}" : $"{evt.NewTier}";

                // Update production label if resource building
                UpdateProductionLabel(placement.VisualGO, placement.BuildingId, evt.NewTier);

                // P&C: Celebration burst particle effect
                StartCoroutine(UpgradeCelebrationBurst(placement.VisualGO));

                // P&C: "NEW" badge that fades after 10 seconds
                CreateNewBadge(placement.VisualGO);

                // Update count badges (tier changed)
                var oldCountBadge = placement.VisualGO.transform.Find("CountBadge");
                if (oldCountBadge != null) Destroy(oldCountBadge.gameObject);
                CreateBuildingCountBadge(placement.VisualGO, placement.BuildingId);
            }
        }

        /// <summary>P&C: Smooth crossfade from old sprite to new tier sprite.</summary>
        private IEnumerator CrossfadeSprite(Image img, Sprite newSprite)
        {
            // Flash white briefly then swap
            var originalColor = img.color;
            float flashDur = 0.15f;
            float elapsed = 0f;
            while (elapsed < flashDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDur;
                img.color = Color.Lerp(originalColor, Color.white * 1.5f, t);
                yield return null;
            }

            img.sprite = newSprite;
            img.preserveAspect = true;

            // Fade back from white
            elapsed = 0f;
            float fadeDur = 0.2f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                img.color = Color.Lerp(Color.white * 1.5f, Color.white, t);
                yield return null;
            }
            img.color = Color.white;
        }

        /// <summary>
        /// P&C-style upgrade celebration: expanding light rays + scattered sparkle particles
        /// that burst outward from the building, then fade. Pure UI-based (no ParticleSystem).
        /// </summary>
        private IEnumerator UpgradeCelebrationBurst(GameObject buildingGO)
        {
            if (buildingGO == null) yield break;

            var container = buildingGO.transform.parent;
            if (container == null) yield break;

            var buildingRect = buildingGO.GetComponent<RectTransform>();
            Vector2 center = buildingRect != null ? buildingRect.anchoredPosition : Vector2.zero;
            center.y += buildingRect != null ? buildingRect.sizeDelta.y * 0.3f : 0;

            // Spawn 8 light rays radiating outward
            int rayCount = 8;
            var rays = new List<(RectTransform rect, CanvasGroup cg)>();
            for (int i = 0; i < rayCount; i++)
            {
                float angle = (i / (float)rayCount) * Mathf.PI * 2f;
                var rayGO = new GameObject($"Ray_{i}");
                rayGO.transform.SetParent(container, false);
                rayGO.transform.SetAsLastSibling();

                var rect = rayGO.AddComponent<RectTransform>();
                rect.anchoredPosition = center;
                rect.sizeDelta = new Vector2(6, 30);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);

                var img = rayGO.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(1f, 0.88f, 0.35f, 0.9f); // Gold

                var cg = rayGO.AddComponent<CanvasGroup>();
                rays.Add((rect, cg));
            }

            // Spawn 12 sparkle dots
            int sparkleCount = 12;
            var sparkles = new List<(RectTransform rect, CanvasGroup cg, Vector2 dir)>();
            for (int i = 0; i < sparkleCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var sparkGO = new GameObject($"Sparkle_{i}");
                sparkGO.transform.SetParent(container, false);
                sparkGO.transform.SetAsLastSibling();

                var rect = sparkGO.AddComponent<RectTransform>();
                rect.anchoredPosition = center;
                rect.sizeDelta = new Vector2(Random.Range(4f, 8f), Random.Range(4f, 8f));

                var img = sparkGO.AddComponent<Image>();
                img.raycastTarget = false;
                float hue = Random.Range(0.08f, 0.15f); // Gold-orange range
                img.color = Color.HSVToRGB(hue, 0.6f, 1f);

                // Try radial gradient for soft sparkle
                var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
                #if UNITY_EDITOR
                if (spr == null)
                    spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
                #endif
                if (spr != null) img.sprite = spr;

                var cg = sparkGO.AddComponent<CanvasGroup>();
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(60f, 120f);
                sparkles.Add((rect, cg, dir));
            }

            // Animate over 0.8 seconds
            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Rays expand and fade
                foreach (var (rect, cg) in rays)
                {
                    if (rect == null) continue;
                    float rayLen = Mathf.Lerp(10f, 80f, t);
                    rect.sizeDelta = new Vector2(Mathf.Lerp(6f, 2f, t), rayLen);
                    cg.alpha = 1f - t;
                }

                // Sparkles fly outward and fade
                foreach (var (rect, cg, dir) in sparkles)
                {
                    if (rect == null) continue;
                    float ease = t * (2f - t); // EaseOut
                    rect.anchoredPosition = center + dir * ease;
                    float scale = Mathf.Lerp(1.5f, 0.3f, t);
                    rect.localScale = Vector3.one * scale;
                    cg.alpha = t < 0.2f ? t / 0.2f : 1f - ((t - 0.2f) / 0.8f);
                }

                yield return null;
            }

            // Cleanup
            foreach (var (rect, cg) in rays)
                if (rect != null) Destroy(rect.gameObject);
            foreach (var (rect, cg, dir) in sparkles)
                if (rect != null) Destroy(rect.gameObject);
        }

        // ====================================================================
        // P&C: Upgrade started/completed SFX + queue indicator
        // ====================================================================

        /// <summary>P&C: Play SFX and show builder indicator when upgrade begins.</summary>
        private void OnUpgradeStarted(BuildingUpgradeStartedEvent evt)
        {
            PlaySfx(_sfxLevelUp);

            // Find the placement and add a builder/hammer indicator
            foreach (var p in _placements)
            {
                if (p.InstanceId == evt.PlacedId && p.VisualGO != null)
                {
                    CreateUpgradeIndicator(p.VisualGO, evt.BuildTimeSeconds, evt.PlacedId);
                    AddScaffoldingOverlay(p.VisualGO);
                    AddConstructionDustEffect(p.VisualGO);
                    // Remove upgrade arrow since building is now upgrading
                    var arrow = p.VisualGO.transform.Find("UpgradeArrow");
                    if (arrow != null) Destroy(arrow.gameObject);
                    break;
                }
            }
        }

        // ====================================================================
        // P&C: Resource Collection Summary Toast
        // ====================================================================

        private float _collectToastCooldown;
        private long _collectToastAccum;
        private ResourceType _collectToastType;

        private void OnResourceCollected(ResourceCollectedEvent evt)
        {
            PlaySfx(_sfxCollect);

            // P&C: Spawn fly-to-bar particle
            SpawnResourceFlyParticle(evt.Type, evt.BuildingInstanceId);

            // Accumulate collections within a short window to batch into one toast
            if (_collectToastCooldown > 0f && _collectToastType == evt.Type)
            {
                _collectToastAccum += evt.Amount;
            }
            else
            {
                _collectToastAccum = evt.Amount;
                _collectToastType = evt.Type;
            }
            _collectToastCooldown = 0.8f; // Reset cooldown
            StartCoroutine(ShowCollectToastDelayed());
        }

        private IEnumerator ShowCollectToastDelayed()
        {
            // Wait for accumulation window to close
            while (_collectToastCooldown > 0f)
            {
                _collectToastCooldown -= Time.deltaTime;
                yield return null;
            }
            if (_collectToastAccum <= 0) yield break;

            long amount = _collectToastAccum;
            ResourceType type = _collectToastType;
            _collectToastAccum = 0;

            string resName = type switch
            {
                ResourceType.Stone => "Stone",
                ResourceType.Iron => "Iron",
                ResourceType.Grain => "Grain",
                ResourceType.ArcaneEssence => "Arcane",
                _ => "Resources"
            };
            Color resColor = type switch
            {
                ResourceType.Stone => new Color(0.75f, 0.68f, 0.55f),
                ResourceType.Iron => new Color(0.70f, 0.75f, 0.85f),
                ResourceType.Grain => new Color(0.55f, 0.85f, 0.40f),
                ResourceType.ArcaneEssence => new Color(0.65f, 0.45f, 0.90f),
                _ => Color.white
            };

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) yield break;

            var toast = new GameObject("CollectToast");
            toast.transform.SetParent(canvas.transform, false);
            var rect = toast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.30f, 0.45f);
            rect.anchorMax = new Vector2(0.70f, 0.50f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toast.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            string amountStr = amount >= 1000 ? $"{amount / 1000f:F1}K" : amount.ToString();
            text.text = $"+{amountStr} {resName}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = resColor;
            text.raycastTarget = false;
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Float up and fade out
            float duration = 1.2f;
            float elapsed = 0f;
            Vector2 startMin = rect.anchorMin;
            Vector2 startMax = rect.anchorMax;
            while (elapsed < duration && toast != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float yOffset = t * 0.08f;
                rect.anchorMin = startMin + new Vector2(0, yOffset);
                rect.anchorMax = startMax + new Vector2(0, yOffset);
                text.color = new Color(resColor.r, resColor.g, resColor.b, 1f - t * t);
                yield return null;
            }
            if (toast != null) Destroy(toast);
        }

        /// <summary>P&C: Spawn a small colored particle that flies from the building toward the resource bar at top.</summary>
        private void SpawnResourceFlyParticle(ResourceType type, string buildingInstanceId)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Find source building position
            Vector2 startAnchor = new Vector2(0.5f, 0.5f);
            foreach (var p in _placements)
            {
                if (p.InstanceId == buildingInstanceId && p.VisualGO != null)
                {
                    var bRect = p.VisualGO.GetComponent<RectTransform>();
                    if (bRect != null)
                    {
                        // Convert building world position to screen-space normalized anchor
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, bRect.position);
                        startAnchor = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
                    }
                    break;
                }
            }

            Color particleColor = type switch
            {
                ResourceType.Stone => new Color(0.75f, 0.68f, 0.55f),
                ResourceType.Iron => new Color(0.70f, 0.75f, 0.85f),
                ResourceType.Grain => new Color(0.55f, 0.85f, 0.40f),
                ResourceType.ArcaneEssence => new Color(0.65f, 0.45f, 0.90f),
                _ => Color.white
            };

            // Spawn 3 particles with slight offset
            for (int i = 0; i < 3; i++)
            {
                var particle = new GameObject($"FlyParticle_{i}");
                particle.transform.SetParent(canvas.transform, false);
                var rect = particle.AddComponent<RectTransform>();
                float xJitter = (i - 1) * 0.02f;
                rect.anchorMin = startAnchor + new Vector2(xJitter - 0.01f, -0.01f);
                rect.anchorMax = startAnchor + new Vector2(xJitter + 0.01f, 0.01f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = particle.AddComponent<Image>();
                img.color = particleColor;
                img.raycastTarget = false;

                // Target: resource bar area at top of screen
                Vector2 targetAnchor = new Vector2(0.3f + i * 0.1f, 0.96f);
                StartCoroutine(AnimateFlyParticle(particle, rect, img, startAnchor, targetAnchor, i * 0.05f));
            }
        }

        private IEnumerator AnimateFlyParticle(GameObject particle, RectTransform rect, Image img,
            Vector2 startAnchor, Vector2 targetAnchor, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration && particle != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Ease-in curve for acceleration effect
                float curved = t * t;
                Vector2 pos = Vector2.Lerp(startAnchor, targetAnchor, curved);
                // Arc upward slightly
                float arc = Mathf.Sin(t * Mathf.PI) * 0.04f;
                pos.x += arc;
                rect.anchorMin = pos + new Vector2(-0.008f, -0.008f);
                rect.anchorMax = pos + new Vector2(0.008f, 0.008f);
                // Shrink as it reaches target
                float scale = 1f - curved * 0.5f;
                rect.localScale = Vector3.one * scale;
                // Brighten then fade at end
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                img.color = new Color(img.color.r, img.color.g, img.color.b, alpha);
                yield return null;
            }
            if (particle != null) Destroy(particle);
        }

        /// <summary>P&C: Play SFX and remove builder indicator when upgrade completes.</summary>
        private void OnUpgradeCompletedSfx(BuildingUpgradeCompletedEvent evt)
        {
            PlaySfx(_sfxBuildComplete);

            // Remove the builder indicator and celebrate
            foreach (var p in _placements)
            {
                if (p.InstanceId == evt.PlacedId && p.VisualGO != null)
                {
                    RemoveUpgradeIndicator(p.VisualGO);
                    RemoveScaffoldingOverlay(p.VisualGO);
                    RemoveConstructionDustEffect(p.VisualGO);

                    // P&C: Update tier on placement data
                    p.Tier = evt.NewTier;

                    // P&C: Swap building sprite to new tier
                    RefreshBuildingSprite(p.VisualGO, p.BuildingId, evt.NewTier);

                    // P&C: Refresh the level badge to show new tier
                    RefreshLevelBadge(p.VisualGO, evt.NewTier);

                    // P&C: Refresh production label if resource building
                    RefreshProductionLabel(p.VisualGO, p.BuildingId, evt.NewTier);

                    // P&C: Upgrade completion celebration — flash + burst + bounce
                    StartCoroutine(UpgradeCompletionCelebration(p.VisualGO));

                    // P&C: Show slide-in banner notification
                    string displayName = BuildingDisplayNames.TryGetValue(p.BuildingId, out var dn) ? dn : p.BuildingId;
                    ShowUpgradeCompleteBanner(displayName, evt.NewTier);

                    RefreshInstanceCountBadges();
                    UpdateBuilderCountHUD();
                    break;
                }
            }
        }

        /// <summary>P&C: Slide-in banner from top when an upgrade completes.</summary>
        private void ShowUpgradeCompleteBanner(string buildingName, int newTier)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var banner = new GameObject("UpgradeCompleteBanner");
            banner.transform.SetParent(canvas.transform, false);
            var rect = banner.AddComponent<RectTransform>();
            // Start off-screen above, slide to top area
            rect.anchorMin = new Vector2(0.08f, 0.88f);
            rect.anchorMax = new Vector2(0.92f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = banner.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.18f, 0.92f);
            bg.raycastTarget = false;

            var outline = banner.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.70f, 0.20f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(banner.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);
            var text = textGO.AddComponent<Text>();
            string tierStar = newTier > 1 ? $" \u2605{newTier}" : "";
            text.text = $"\u2714 {buildingName} upgraded to Lv {newTier}{tierStar}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.95f, 0.85f, 0.35f);
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(0.6f, -0.6f);

            StartCoroutine(AnimateUpgradeCompleteBanner(banner, rect));
        }

        private IEnumerator AnimateUpgradeCompleteBanner(GameObject banner, RectTransform rect)
        {
            // Slide in from above
            float slideIn = 0.3f;
            float hold = 2.5f;
            float slideOut = 0.4f;

            Vector2 offMin = rect.anchorMin;
            Vector2 offMax = rect.anchorMax;
            float height = offMax.y - offMin.y;

            // Start above screen
            rect.anchorMin = new Vector2(offMin.x, 1f);
            rect.anchorMax = new Vector2(offMax.x, 1f + height);

            // Slide down
            float elapsed = 0f;
            while (elapsed < slideIn && banner != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideIn);
                rect.anchorMin = Vector2.Lerp(new Vector2(offMin.x, 1f), offMin, t);
                rect.anchorMax = Vector2.Lerp(new Vector2(offMax.x, 1f + height), offMax, t);
                yield return null;
            }

            // Hold
            yield return new WaitForSeconds(hold);

            // Slide back up
            elapsed = 0f;
            var cg = banner.AddComponent<CanvasGroup>();
            while (elapsed < slideOut && banner != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideOut;
                rect.anchorMin = Vector2.Lerp(offMin, new Vector2(offMin.x, 1f), t);
                rect.anchorMax = Vector2.Lerp(offMax, new Vector2(offMax.x, 1f + height), t);
                cg.alpha = 1f - t;
                yield return null;
            }

            if (banner != null) Destroy(banner);
        }

        /// <summary>P&C: Celebration when upgrade completes — white flash, golden burst particles, big bounce.</summary>
        private IEnumerator UpgradeCompletionCelebration(GameObject building)
        {
            if (building == null) yield break;

            // --- White flash overlay ---
            var flashGO = new GameObject("UpgradeFlash");
            flashGO.transform.SetParent(building.transform, false);
            var flashRect = flashGO.AddComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            var flashImg = flashGO.AddComponent<Image>();
            flashImg.color = new Color(1f, 0.95f, 0.7f, 0.85f);
            flashImg.raycastTarget = false;

            // Flash fade out over 0.4s
            float flashDuration = 0.4f;
            float elapsed = 0f;
            while (elapsed < flashDuration && flashGO != null)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0.85f, 0f, elapsed / flashDuration);
                flashImg.color = new Color(1f, 0.95f, 0.7f, alpha);
                yield return null;
            }
            if (flashGO != null) Destroy(flashGO);

            // --- Golden burst particles (8 small squares flying outward) ---
            var buildingRect = building.GetComponent<RectTransform>();
            if (buildingRect != null)
            {
                var parent = buildingRect.parent as RectTransform;
                if (parent != null)
                {
                    int burstCount = 8;
                    for (int i = 0; i < burstCount; i++)
                    {
                        var particleGO = new GameObject($"UpgradeBurst_{i}");
                        particleGO.transform.SetParent(parent, false);
                        var pRect = particleGO.AddComponent<RectTransform>();
                        pRect.sizeDelta = new Vector2(6f, 6f);
                        pRect.anchoredPosition = buildingRect.anchoredPosition + new Vector2(0, buildingRect.sizeDelta.y * 0.3f);

                        var pImg = particleGO.AddComponent<Image>();
                        // Alternate gold and warm white
                        pImg.color = (i % 2 == 0)
                            ? new Color(0.95f, 0.78f, 0.20f, 1f)
                            : new Color(1f, 0.92f, 0.60f, 1f);
                        pImg.raycastTarget = false;

                        float angle = (360f / burstCount) * i * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        StartCoroutine(AnimateBurstParticle(particleGO, pRect, dir));
                    }
                }
            }

            // --- Big celebratory bounce ---
            if (building != null)
                yield return BounceBuilding(building.transform, 0.15f);
        }

        private IEnumerator AnimateBurstParticle(GameObject go, RectTransform rect, Vector2 direction)
        {
            float duration = 0.6f;
            float speed = 80f;
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            var img = go.GetComponent<Image>();

            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = startPos + direction * speed * t;
                rect.sizeDelta = Vector2.Lerp(new Vector2(6f, 6f), new Vector2(2f, 2f), t);
                if (img != null)
                    img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - t);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        /// <summary>Update the level badge on a building visual to reflect new tier.</summary>
        private void RefreshLevelBadge(GameObject building, int newTier)
        {
            var existing = building.transform.Find("LevelBadge");
            if (existing != null) Destroy(existing.gameObject);
            CreateLevelBadge(building, newTier);
        }

        /// <summary>P&C: Swap the building Image sprite to the new tier artwork.</summary>
        private void RefreshBuildingSprite(GameObject building, string buildingId, int newTier)
        {
            var img = building.GetComponent<Image>();
            if (img == null) return;
            string spriteName = $"{buildingId}_t{newTier}";
            var sprite = Resources.Load<Sprite>($"Buildings/{spriteName}");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
        }

        /// <summary>P&C: Refresh production rate label after tier change.</summary>
        private void RefreshProductionLabel(GameObject building, string buildingId, int newTier)
        {
            var existing = building.transform.Find("ProductionRate");
            if (existing != null) Destroy(existing.gameObject);
            CreateProductionLabel(building, buildingId, newTier);
        }

        // ====================================================================
        // P&C: Speed-Up Confirmation Dialog
        // ====================================================================

        private GameObject _speedUpDialog;

        /// <summary>P&C: Confirmation dialog before spending gems to speed up an upgrade.</summary>
        private void ShowSpeedUpDialog(string instanceId, int gemCost, int remainingSeconds)
        {
            DismissSpeedUpDialog();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _speedUpDialog = new GameObject("SpeedUpDialog");
            _speedUpDialog.transform.SetParent(canvas.transform, false);

            // Full-screen dim overlay
            var dimRect = _speedUpDialog.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _speedUpDialog.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;

            // Tap dim to dismiss
            var dimBtn = _speedUpDialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissSpeedUpDialog);

            // Dialog panel (center of screen)
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_speedUpDialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.35f);
            panelRect.anchorMax = new Vector2(0.88f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelImg.raycastTarget = true; // block click-through
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.8f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.72f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.AddComponent<Text>();
            titleText.text = "Speed Up Construction";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.95f, 0.82f, 0.35f);
            titleText.raycastTarget = false;

            // Cost info
            string timeStr = FormatTimeRemaining(remainingSeconds);
            var costGO = new GameObject("CostInfo");
            costGO.transform.SetParent(panel.transform, false);
            var costRect = costGO.AddComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0.05f, 0.38f);
            costRect.anchorMax = new Vector2(0.95f, 0.70f);
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;
            var costText = costGO.AddComponent<Text>();
            costText.text = $"Skip {timeStr} remaining?\n\u25C6 {gemCost} Gems";
            costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            costText.fontSize = 14;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = Color.white;
            costText.raycastTarget = false;

            // Confirm button (green)
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(panel.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.52f, 0.06f);
            confirmRect.anchorMax = new Vector2(0.94f, 0.34f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = new Color(0.15f, 0.60f, 0.25f, 0.92f);
            confirmImg.raycastTarget = true;
            var confirmOutline = confirmGO.AddComponent<Outline>();
            confirmOutline.effectColor = new Color(0.4f, 1f, 0.5f, 0.5f);
            confirmOutline.effectDistance = new Vector2(1f, -1f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            string capId = instanceId;
            int capGems = gemCost;
            int capSecs = remainingSeconds;
            confirmBtn.onClick.AddListener(() => {
                EventBus.Publish(new SpeedupRequestedEvent(capId, capGems, capSecs));
                DismissSpeedUpDialog();
            });
            AddDialogButtonLabel(confirmGO, "CONFIRM");

            // Cancel button (dark)
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(panel.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.06f, 0.06f);
            cancelRect.anchorMax = new Vector2(0.48f, 0.34f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.25f, 0.20f, 0.30f, 0.90f);
            cancelImg.raycastTarget = true;
            var cancelOutline = cancelGO.AddComponent<Outline>();
            cancelOutline.effectColor = new Color(0.6f, 0.5f, 0.7f, 0.4f);
            cancelOutline.effectDistance = new Vector2(1f, -1f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(DismissSpeedUpDialog);
            AddDialogButtonLabel(cancelGO, "CANCEL");

            // Fade in
            var cg = _speedUpDialog.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void AddDialogButtonLabel(GameObject parent, string label)
        {
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(parent.transform, false);
            var lblRect = lblGO.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;
            var text = lblGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = lblGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.6f, -0.6f);
        }

        private IEnumerator FadeInDialog(CanvasGroup cg)
        {
            float elapsed = 0f;
            while (elapsed < 0.2f && cg != null)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / 0.2f);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
        }

        private void DismissSpeedUpDialog()
        {
            if (_speedUpDialog != null)
            {
                Destroy(_speedUpDialog);
                _speedUpDialog = null;
            }
        }

        private static string FormatTimeRemaining(int totalSeconds)
        {
            if (totalSeconds >= 3600)
            {
                int h = totalSeconds / 3600;
                int m = (totalSeconds % 3600) / 60;
                return m > 0 ? $"{h}h {m}m" : $"{h}h";
            }
            if (totalSeconds >= 60)
            {
                int m = totalSeconds / 60;
                int s = totalSeconds % 60;
                return s > 0 ? $"{m}m {s}s" : $"{m}m";
            }
            return $"{totalSeconds}s";
        }

        /// <summary>P&C: Hammer icon + progress bar + speed-up button overlay on building during upgrade.</summary>
        private void CreateUpgradeIndicator(GameObject building, int buildTimeSeconds, string instanceId = null)
        {
            // Remove any existing indicator first
            RemoveUpgradeIndicator(building);

            var indicator = new GameObject("UpgradeIndicator");
            indicator.transform.SetParent(building.transform, false);

            var rect = indicator.AddComponent<RectTransform>();
            // Position at top-center of building
            rect.anchorMin = new Vector2(0.05f, 0.70f);
            rect.anchorMax = new Vector2(0.95f, 1.0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark semi-transparent background pill
            var bg = indicator.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.1f, 0.88f);
            bg.raycastTarget = false;

            // Gold outline for visibility
            var outline = indicator.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            // --- Progress bar background (bottom strip of indicator) ---
            var barBgGO = new GameObject("BarBg");
            barBgGO.transform.SetParent(indicator.transform, false);
            var barBgRect = barBgGO.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.05f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.25f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBgGO.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.12f, 0.20f, 0.9f);
            barBgImg.raycastTarget = false;

            // --- Progress bar fill ---
            var barFillGO = new GameObject("BarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            var barFillRect = barFillGO.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(0f, 1f); // starts empty, filled by coroutine
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
            var barFillImg = barFillGO.AddComponent<Image>();
            barFillImg.color = new Color(0.95f, 0.75f, 0.15f, 0.95f); // Gold fill
            barFillImg.raycastTarget = false;

            // --- Hammer symbol + time text (left portion) ---
            var textGO = new GameObject("TimerText");
            textGO.transform.SetParent(indicator.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.25f);
            textRect.anchorMax = new Vector2(0.62f, 1f);
            textRect.offsetMin = new Vector2(2, 0);
            textRect.offsetMax = new Vector2(-1, 0);

            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.35f);
            text.raycastTarget = false;

            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.9f);
            textOutline.effectDistance = new Vector2(1f, -1f);

            // --- P&C: Speed-up gem button (right portion) ---
            var speedBtnGO = new GameObject("SpeedUpBtn");
            speedBtnGO.transform.SetParent(indicator.transform, false);
            var speedRect = speedBtnGO.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0.64f, 0.28f);
            speedRect.anchorMax = new Vector2(0.96f, 0.95f);
            speedRect.offsetMin = Vector2.zero;
            speedRect.offsetMax = Vector2.zero;

            var speedImg = speedBtnGO.AddComponent<Image>();
            speedImg.color = new Color(0.20f, 0.65f, 0.25f, 0.95f); // Green "boost" color
            speedImg.raycastTarget = true;

            var speedOutline = speedBtnGO.AddComponent<Outline>();
            speedOutline.effectColor = new Color(0.5f, 1f, 0.5f, 0.4f);
            speedOutline.effectDistance = new Vector2(0.6f, -0.6f);

            var speedBtn = speedBtnGO.AddComponent<Button>();
            speedBtn.targetGraphic = speedImg;
            string capturedId = instanceId;
            int capturedSecs = buildTimeSeconds;
            speedBtn.onClick.AddListener(() => {
                if (capturedId == null) return;
                // P&C: Free instant complete for timers under 5 minutes
                if (capturedSecs <= FreeSpeedUpThresholdSeconds)
                {
                    EventBus.Publish(new SpeedupRequestedEvent(capturedId, 0, capturedSecs));
                    return;
                }
                int gemCost = Mathf.Max(1, Mathf.CeilToInt(capturedSecs / 60f));
                ShowSpeedUpDialog(capturedId, gemCost, capturedSecs);
            });

            var speedTextGO = new GameObject("Label");
            speedTextGO.transform.SetParent(speedBtnGO.transform, false);
            var speedTextRect = speedTextGO.AddComponent<RectTransform>();
            speedTextRect.anchorMin = Vector2.zero;
            speedTextRect.anchorMax = Vector2.one;
            speedTextRect.offsetMin = Vector2.zero;
            speedTextRect.offsetMax = Vector2.zero;

            var speedText = speedTextGO.AddComponent<Text>();
            // P&C: Show "FREE" for short timers, lightning bolt otherwise
            speedText.text = buildTimeSeconds <= FreeSpeedUpThresholdSeconds ? "FREE" : "\u26A1";
            speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            speedText.fontSize = buildTimeSeconds <= FreeSpeedUpThresholdSeconds ? 8 : 11;
            speedText.fontStyle = FontStyle.Bold;
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = buildTimeSeconds <= FreeSpeedUpThresholdSeconds
                ? new Color(0.3f, 1f, 0.4f) : Color.white;
            speedText.raycastTarget = false;

            // --- P&C: Alliance help (handshake) button below speed-up ---
            var helpBtnGO = new GameObject("AllianceHelpBtn");
            helpBtnGO.transform.SetParent(indicator.transform, false);
            var helpRect = helpBtnGO.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0.64f, 0.02f);
            helpRect.anchorMax = new Vector2(0.96f, 0.26f);
            helpRect.offsetMin = Vector2.zero;
            helpRect.offsetMax = Vector2.zero;

            var helpImg = helpBtnGO.AddComponent<Image>();
            helpImg.color = new Color(0.20f, 0.40f, 0.70f, 0.90f); // Blue alliance color
            helpImg.raycastTarget = true;

            var helpBtn = helpBtnGO.AddComponent<Button>();
            helpBtn.targetGraphic = helpImg;
            string helpId = instanceId;
            helpBtn.onClick.AddListener(() => {
                if (helpId != null)
                    EventBus.Publish(new AllianceHelpRequestedEvent(helpId));
            });

            var helpTextGO = new GameObject("Label");
            helpTextGO.transform.SetParent(helpBtnGO.transform, false);
            var helpTextRect = helpTextGO.AddComponent<RectTransform>();
            helpTextRect.anchorMin = Vector2.zero;
            helpTextRect.anchorMax = Vector2.one;
            helpTextRect.offsetMin = Vector2.zero;
            helpTextRect.offsetMax = Vector2.zero;
            var helpText = helpTextGO.AddComponent<Text>();
            helpText.text = "Help";
            helpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            helpText.fontSize = 8;
            helpText.fontStyle = FontStyle.Bold;
            helpText.alignment = TextAnchor.MiddleCenter;
            helpText.color = Color.white;
            helpText.raycastTarget = false;

            // Start live countdown + progress fill coroutine
            StartCoroutine(AnimateUpgradeIndicator(indicator, barFillRect, text, buildTimeSeconds));
        }

        /// <summary>P&C: Live countdown timer + progress bar fill on upgrade indicator.</summary>
        private IEnumerator AnimateUpgradeIndicator(GameObject indicator, RectTransform barFill, Text timerText, int totalSeconds)
        {
            if (indicator == null) yield break;
            var cg = indicator.AddComponent<CanvasGroup>();
            float remaining = totalSeconds;
            float pulsePhase = 0f;

            while (indicator != null && remaining > 0f)
            {
                remaining -= Time.deltaTime;
                if (remaining < 0f) remaining = 0f;

                // Update progress bar fill (right anchor X = progress ratio)
                float progress = 1f - (remaining / totalSeconds);
                barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

                // Update timer text
                int secs = Mathf.CeilToInt(remaining);
                string timeStr;
                if (secs >= 3600)
                    timeStr = $"{secs / 3600}h{(secs % 3600) / 60:D2}m";
                else if (secs >= 60)
                    timeStr = $"{secs / 60}m{secs % 60:D2}s";
                else
                    timeStr = $"{secs}s";
                timerText.text = $"\u2692 {timeStr}"; // ⚒

                // Gentle pulse
                pulsePhase += Time.deltaTime * 2.5f;
                cg.alpha = 0.75f + 0.25f * Mathf.Sin(pulsePhase);

                yield return null;
            }

            // Timer expired — indicator will be cleaned up by OnUpgradeCompletedSfx event
        }

        /// <summary>Detect in-progress upgrades on Start and show indicators.</summary>
        private void RefreshUpgradeIndicators()
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;
            foreach (var entry in bm.BuildQueue)
            {
                foreach (var p in _placements)
                {
                    if (p.InstanceId == entry.PlacedId && p.VisualGO != null)
                    {
                        CreateUpgradeIndicator(p.VisualGO, Mathf.CeilToInt(entry.RemainingSeconds), entry.PlacedId);
                        AddScaffoldingOverlay(p.VisualGO);
                        AddConstructionDustEffect(p.VisualGO);
                        break;
                    }
                }
            }
        }

        private void RemoveUpgradeIndicator(GameObject building)
        {
            if (building == null) return;
            var existing = building.transform.Find("UpgradeIndicator");
            if (existing != null)
                Destroy(existing.gameObject);
        }

        // ====================================================================
        // P&C: Move mode confirm/cancel bar
        // ====================================================================

        /// <summary>P&C: Show confirm/cancel buttons at bottom of screen during move mode.</summary>
        private void ShowMoveConfirmBar()
        {
            DestroyMoveConfirmBar();

            // Create bar anchored to bottom of the screen (on the main Canvas, not building container)
            Transform canvasRoot = transform;
            // Walk up to find the root Canvas
            while (canvasRoot.parent != null && canvasRoot.parent.GetComponent<Canvas>() != null)
                canvasRoot = canvasRoot.parent;

            var bar = new GameObject("MoveConfirmBar");
            bar.transform.SetParent(canvasRoot, false);
            bar.transform.SetAsLastSibling();

            var barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.15f, 0.12f);
            barRect.anchorMax = new Vector2(0.85f, 0.18f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;

            var barBg = bar.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.04f, 0.10f, 0.90f);
            barBg.raycastTarget = false;

            var barOutline = bar.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.85f, 0.68f, 0.20f, 0.6f);
            barOutline.effectDistance = new Vector2(1f, -1f);

            // Cancel button (left)
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(bar.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.02f, 0.1f);
            cancelRect.anchorMax = new Vector2(0.48f, 0.9f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;

            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.65f, 0.15f, 0.15f, 0.9f);
            cancelImg.raycastTarget = true;

            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(CancelMoveMode);

            var cancelTextGO = new GameObject("Label");
            cancelTextGO.transform.SetParent(cancelGO.transform, false);
            var cancelTextRect = cancelTextGO.AddComponent<RectTransform>();
            cancelTextRect.anchorMin = Vector2.zero;
            cancelTextRect.anchorMax = Vector2.one;
            cancelTextRect.offsetMin = Vector2.zero;
            cancelTextRect.offsetMax = Vector2.zero;
            var cancelText = cancelTextGO.AddComponent<Text>();
            cancelText.text = "\u2716 Cancel";
            cancelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cancelText.fontSize = 12;
            cancelText.fontStyle = FontStyle.Bold;
            cancelText.alignment = TextAnchor.MiddleCenter;
            cancelText.color = Color.white;
            cancelText.raycastTarget = false;

            // Confirm button (right)
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(bar.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.52f, 0.1f);
            confirmRect.anchorMax = new Vector2(0.98f, 0.9f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;

            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = new Color(0.15f, 0.60f, 0.20f, 0.9f);
            confirmImg.raycastTarget = true;

            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.onClick.AddListener(ConfirmMoveMode);

            var confirmTextGO = new GameObject("Label");
            confirmTextGO.transform.SetParent(confirmGO.transform, false);
            var confirmTextRect = confirmTextGO.AddComponent<RectTransform>();
            confirmTextRect.anchorMin = Vector2.zero;
            confirmTextRect.anchorMax = Vector2.one;
            confirmTextRect.offsetMin = Vector2.zero;
            confirmTextRect.offsetMax = Vector2.zero;
            var confirmText = confirmTextGO.AddComponent<Text>();
            confirmText.text = "\u2714 Confirm";
            confirmText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            confirmText.fontSize = 12;
            confirmText.fontStyle = FontStyle.Bold;
            confirmText.alignment = TextAnchor.MiddleCenter;
            confirmText.color = Color.white;
            confirmText.raycastTarget = false;

            _moveConfirmBar = bar;
        }

        /// <summary>P&C: Confirm move — commit building to current ghost position.</summary>
        private void ConfirmMoveMode()
        {
            if (!_moveMode || _movingBuilding == null) return;
            // Reuse existing exit logic which handles placement/swap
            ExitMoveMode(null);
        }

        /// <summary>P&C: Cancel move — return building to original position.</summary>
        private void CancelMoveMode()
        {
            if (!_moveMode || _movingBuilding == null) return;

            // Restore building to original position
            if (_movingBuilding.VisualGO != null)
                _movingBuilding.VisualGO.GetComponent<Image>().color = Color.white;

            // Cleanup ghost/shadow without committing move
            if (_dragGhost != null) Destroy(_dragGhost);
            if (_dragShadow != null) Destroy(_dragShadow);
            _dragGhost = null;
            _dragShadow = null;

            // P&C: Restore all buildings to full brightness
            foreach (var p in _placements)
            {
                if (p.VisualGO != null)
                    p.VisualGO.GetComponent<Image>().color = Color.white;
            }

            _movingBuilding = null;
            _moveMode = false;

            PlaySfx(_sfxTap);
            HideMoveGridCells();
            DestroyHighlight();
            DestroyMoveConfirmBar();
            SetGridOverlayVisible(false);
            if (scrollRect != null) scrollRect.enabled = true;
        }

        private void DestroyMoveConfirmBar()
        {
            if (_moveConfirmBar != null)
            {
                Destroy(_moveConfirmBar);
                _moveConfirmBar = null;
            }
        }

        // ====================================================================
        // P&C: Move mode coordinate label
        // ====================================================================

        private GameObject _moveCoordLabel;

        /// <summary>P&C: Floating grid coordinate label near the drag ghost during move mode.</summary>
        private void UpdateMoveCoordLabel(Vector2 localPoint)
        {
            if (_movingBuilding == null) return;
            var snapOrigin = SnapToGrid(localPoint, _movingBuilding.Size);
            bool valid = CanPlaceAt(snapOrigin, _movingBuilding.Size, _movingBuilding);

            if (_moveCoordLabel == null)
            {
                _moveCoordLabel = new GameObject("MoveCoordLabel");
                _moveCoordLabel.transform.SetParent(buildingContainer, false);
                var rect = _moveCoordLabel.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(60, 18);
                var bg = _moveCoordLabel.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.03f, 0.10f, 0.80f);
                bg.raycastTarget = false;
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(_moveCoordLabel.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGO.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 9;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
            }

            var coordRect = _moveCoordLabel.GetComponent<RectTransform>();
            coordRect.anchoredPosition = localPoint + new Vector2(0, -25);
            var coordText = _moveCoordLabel.GetComponentInChildren<Text>();
            if (coordText != null)
            {
                coordText.text = $"({snapOrigin.x}, {snapOrigin.y})";
                coordText.color = valid ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.3f);
            }
        }

        private void DestroyMoveCoordLabel()
        {
            if (_moveCoordLabel != null)
            {
                Destroy(_moveCoordLabel);
                _moveCoordLabel = null;
            }
        }

        // ====================================================================
        // P&C: Building info popup on tap
        // ====================================================================

        private static readonly Dictionary<string, string> BuildingDisplayNames = new()
        {
            { "stronghold", "Stronghold" }, { "barracks", "Barracks" }, { "forge", "Forge" },
            { "marketplace", "Marketplace" }, { "academy", "Academy" }, { "grain_farm", "Grain Farm" },
            { "iron_mine", "Iron Mine" }, { "stone_quarry", "Stone Quarry" }, { "arcane_tower", "Arcane Tower" },
            { "wall", "Wall" }, { "watch_tower", "Watch Tower" }, { "guild_hall", "Guild Hall" },
            { "embassy", "Embassy" }, { "training_ground", "Training Ground" }, { "hero_shrine", "Hero Shrine" },
            { "laboratory", "Laboratory" }, { "library", "Library" }, { "armory", "Armory" },
            { "enchanting_tower", "Enchanting Tower" }, { "observatory", "Observatory" }, { "archive", "Archive" },
        };

        /// <summary>P&C: Show radial context menu above tapped building — circular buttons in a semicircle arc.</summary>
        private void OnBuildingTappedShowPopup(BuildingTappedEvent evt)
        {
            DismissInfoPopup();

            if (evt.VisualGO == null) return;

            // Don't show popup if we're in move mode
            if (_moveMode) return;

            _infoPopupInstanceId = evt.InstanceId;

            // Container positioned at building center
            var popup = new GameObject("InfoPopup");
            popup.transform.SetParent(evt.VisualGO.transform.parent, false);
            popup.transform.SetAsLastSibling();

            var popupRect = popup.AddComponent<RectTransform>();
            var buildingRect = evt.VisualGO.GetComponent<RectTransform>();
            Vector2 buildingPos = buildingRect != null ? buildingRect.anchoredPosition : Vector2.zero;
            float buildingHeight = buildingRect != null ? buildingRect.sizeDelta.y : 80f;
            popupRect.anchoredPosition = buildingPos + new Vector2(0, buildingHeight * 0.35f);
            popupRect.sizeDelta = new Vector2(200, 200); // Large enough to contain radial

            // Name plate in center (pill-shaped)
            string displayName = BuildingDisplayNames.TryGetValue(evt.BuildingId, out var dn) ? dn : evt.BuildingId;
            var namePlate = new GameObject("NamePlate");
            namePlate.transform.SetParent(popup.transform, false);
            var nameRect = namePlate.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.15f, 0.38f);
            nameRect.anchorMax = new Vector2(0.85f, 0.52f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameBg = namePlate.AddComponent<Image>();
            nameBg.color = new Color(0.06f, 0.04f, 0.12f, 0.92f);
            nameBg.raycastTarget = false;
            var nameOutline = namePlate.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0.85f, 0.68f, 0.20f, 0.7f);
            nameOutline.effectDistance = new Vector2(1f, -1f);
            var nameTextGO = new GameObject("Text");
            nameTextGO.transform.SetParent(namePlate.transform, false);
            var nameTextRect = nameTextGO.AddComponent<RectTransform>();
            nameTextRect.anchorMin = Vector2.zero;
            nameTextRect.anchorMax = Vector2.one;
            nameTextRect.offsetMin = Vector2.zero;
            nameTextRect.offsetMax = Vector2.zero;
            var nameText = nameTextGO.AddComponent<Text>();
            nameText.text = $"{displayName}  Lv.{evt.Tier}";
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 10;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = GetCategoryHeaderColor(evt.BuildingId);
            nameText.raycastTarget = false;
            var nameTextOutline = nameTextGO.AddComponent<Outline>();
            nameTextOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameTextOutline.effectDistance = new Vector2(1f, -1f);

            // P&C: Check upgrade state
            bool isUpgrading = false;
            float upgradeRemaining = 0f;
            if (ServiceLocator.TryGet<BuildingManager>(out var popupBm))
            {
                foreach (var qe in popupBm.BuildQueue)
                {
                    if (qe.PlacedId == evt.InstanceId)
                    {
                        isUpgrading = true;
                        upgradeRemaining = qe.RemainingSeconds;
                        break;
                    }
                }
            }

            string costStr = GetUpgradeCostString(evt.InstanceId, evt.Tier);
            bool isMaxLevel = costStr == "MAX LEVEL";

            // Build radial button list
            var radialButtons = new List<(string Icon, string Label, Color BgColor, System.Action OnClick)>();

            string upgInstanceId = evt.InstanceId;
            string upgBuildingId = evt.BuildingId;
            int upgTier = evt.Tier;

            if (isUpgrading)
            {
                string timerLabel = FormatTimeRemaining(Mathf.RoundToInt(upgradeRemaining));
                radialButtons.Add(("\u2692", timerLabel, new Color(0.55f, 0.45f, 0.15f, 0.9f), null));
            }
            else
            {
                radialButtons.Add(("\u2B06", isMaxLevel ? "MAX" : "Upgrade",
                    isMaxLevel ? new Color(0.30f, 0.30f, 0.30f, 0.7f) : new Color(0.15f, 0.60f, 0.20f, 0.9f),
                    isMaxLevel ? (System.Action)null : () => {
                        string warning = GetUpgradeBlockReason(upgInstanceId, upgTier);
                        if (warning != null) { ShowUpgradeBlockedToast(warning); return; }
                        DismissInfoPopup();
                        ShowUpgradeConfirmDialog(upgInstanceId, upgBuildingId, upgTier);
                    }));
            }

            string capBuildingId = evt.BuildingId;
            string capInstanceId = evt.InstanceId;
            int capTier = evt.Tier;
            radialButtons.Add(("\u2139", "Info", new Color(0.20f, 0.35f, 0.65f, 0.9f), () => {
                DismissInfoPopup();
                ShowBuildingInfoPanel(capBuildingId, capInstanceId, capTier);
            }));
            radialButtons.Add(("\u2725", "Move", new Color(0.55f, 0.40f, 0.15f, 0.9f), () => {
                DismissInfoPopup();
                EnterMoveModeForBuilding(evt.InstanceId);
            }));

            // P&C: Arrange buttons in semicircle arc above the name plate
            float radius = 55f; // pixels from center
            float startAngle = 150f; // degrees — leftmost position
            float endAngle = 30f;    // degrees — rightmost position
            float btnSize = 38f;

            for (int i = 0; i < radialButtons.Count; i++)
            {
                var (icon, label, bgColor, onClick) = radialButtons[i];
                float t = radialButtons.Count > 1 ? (float)i / (radialButtons.Count - 1) : 0.5f;
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius + 15f; // offset upward

                CreateRadialButton(popup.transform, icon, label, bgColor, onClick,
                    new Vector2(100f + x - btnSize * 0.5f, 90f + y - btnSize * 0.5f), btnSize);
            }

            // P&C: Upgrade cost line below name plate
            if (!string.IsNullOrEmpty(costStr) && costStr != "MAX LEVEL")
            {
                var costGO = new GameObject("CostLine");
                costGO.transform.SetParent(popup.transform, false);
                var costRect = costGO.AddComponent<RectTransform>();
                costRect.anchorMin = new Vector2(0.10f, 0.28f);
                costRect.anchorMax = new Vector2(0.90f, 0.38f);
                costRect.offsetMin = Vector2.zero;
                costRect.offsetMax = Vector2.zero;
                var costText = costGO.AddComponent<Text>();
                costText.text = costStr;
                costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                costText.fontSize = 8;
                costText.alignment = TextAnchor.MiddleCenter;
                costText.color = new Color(0.75f, 0.72f, 0.65f);
                costText.raycastTarget = false;
                var costOutline = costGO.AddComponent<Outline>();
                costOutline.effectColor = new Color(0, 0, 0, 0.7f);
                costOutline.effectDistance = new Vector2(0.7f, -0.7f);
            }

            // Upgrading? Start live timer on the timer button
            if (isUpgrading)
            {
                var timerBtnText = popup.transform.Find("RadialBtn_0")?.GetComponentInChildren<Text>();
                if (timerBtnText != null)
                {
                    string timerInstanceId = evt.InstanceId;
                    StartCoroutine(UpdatePopupTimer(timerBtnText, timerInstanceId, popup));
                }
            }

            // Fade-in + scale-up animation
            var cg = popup.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _infoPopup = popup;
            StartCoroutine(AnimateRadialPopupIn(popup, cg));

            // P&C: Auto-dismiss popup after 5 seconds of no interaction
            if (_popupAutoDismiss != null) StopCoroutine(_popupAutoDismiss);
            _popupAutoDismiss = StartCoroutine(AutoDismissPopup(5f));
        }

        /// <summary>P&C: Create a circular radial menu button at a pixel offset from parent origin.</summary>
        private void CreateRadialButton(Transform parent, string icon, string label, Color bgColor,
            System.Action onClick, Vector2 pixelPos, float size)
        {
            int idx = parent.childCount;
            var btnGO = new GameObject($"RadialBtn_{idx}");
            btnGO.transform.SetParent(parent, false);
            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pixelPos + new Vector2(size * 0.5f, size * 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            // Circular background
            var bg = btnGO.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = true;

            // Gold ring border
            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.68f, 0.20f, 0.75f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            var outline2 = btnGO.AddComponent<Outline>();
            outline2.effectColor = new Color(0.4f, 0.3f, 0.1f, 0.4f);
            outline2.effectDistance = new Vector2(-0.8f, 0.8f);

            // Icon text (large)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(btnGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.30f);
            iconRect.anchorMax = new Vector2(0.9f, 0.95f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = icon;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 14;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.raycastTarget = false;

            // Label text (small, below icon)
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRect = lblGO.AddComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0.0f, 0.0f);
            lblRect.anchorMax = new Vector2(1.0f, 0.32f);
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;
            var lblText = lblGO.AddComponent<Text>();
            lblText.text = label;
            lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lblText.fontSize = 7;
            lblText.alignment = TextAnchor.MiddleCenter;
            lblText.color = new Color(0.90f, 0.88f, 0.82f);
            lblText.raycastTarget = false;
            var lblShadow = lblGO.AddComponent<Shadow>();
            lblShadow.effectColor = new Color(0, 0, 0, 0.8f);
            lblShadow.effectDistance = new Vector2(0.5f, -0.5f);

            if (onClick != null)
            {
                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => onClick());
            }
        }

        /// <summary>P&C: Animate radial popup: scale from 0.3 to 1.0 + fade in over 0.2s.</summary>
        private IEnumerator AnimateRadialPopupIn(GameObject popup, CanvasGroup cg)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            popup.transform.localScale = Vector3.one * 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out back (slight overshoot for snappy feel)
                float ease = 1f + 1.2f * (t - 1f) * (t - 1f) * (t - 1f) + 0.2f * (t - 1f);
                cg.alpha = Mathf.Clamp01(t * 2f); // Fade in faster than scale
                popup.transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, ease);
                yield return null;
            }
            cg.alpha = 1f;
            popup.transform.localScale = Vector3.one;
        }

        private void CreatePopupButton(Transform parent, string label, string icon, Vector2 anchorMin, Vector2 anchorMax,
            Color btnColor, System.Action onClick)
        {
            var btnGO = new GameObject($"Btn_{label}");
            btnGO.transform.SetParent(parent, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = anchorMin;
            btnRect.anchorMax = anchorMax;
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = btnColor;
            btnImg.raycastTarget = true;

            var btnOutline = btnGO.AddComponent<Outline>();
            btnOutline.effectColor = new Color(1f, 0.85f, 0.35f, 0.4f);
            btnOutline.effectDistance = new Vector2(0.8f, -0.8f);

            // Button using Unity's built-in Button component
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Label text
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(btnGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = $"{icon}\n{label}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.8f, -0.8f);
        }

        private IEnumerator FadeInPopup(CanvasGroup cg)
        {
            float elapsed = 0f;
            while (elapsed < 0.15f && cg != null)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / 0.15f);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
        }

        /// <summary>P&C: Live countdown in popup while building is upgrading.</summary>
        private IEnumerator UpdatePopupTimer(Text timerText, string instanceId, GameObject popup)
        {
            while (timerText != null && popup != null)
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                {
                    float remaining = -1f;
                    foreach (var qe in bm.BuildQueue)
                    {
                        if (qe.PlacedId == instanceId)
                        {
                            remaining = qe.RemainingSeconds;
                            break;
                        }
                    }
                    if (remaining < 0)
                    {
                        // Upgrade finished while popup was open
                        timerText.text = "\u2714 Complete!";
                        timerText.color = new Color(0.3f, 1f, 0.4f);
                        yield break;
                    }
                    timerText.text = "\u2692 " + FormatTimeRemaining(Mathf.RoundToInt(remaining));
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void DismissInfoPopup()
        {
            if (_popupAutoDismiss != null) { StopCoroutine(_popupAutoDismiss); _popupAutoDismiss = null; }
            if (_infoPopup != null)
            {
                Destroy(_infoPopup);
                _infoPopup = null;
            }
            _infoPopupInstanceId = null;
        }

        /// <summary>P&C: Auto-dismiss popup after timeout — fade out then destroy.</summary>
        private IEnumerator AutoDismissPopup(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_infoPopup != null)
            {
                var cg = _infoPopup.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float elapsed = 0f;
                    while (elapsed < 0.25f && cg != null)
                    {
                        elapsed += Time.deltaTime;
                        cg.alpha = 1f - Mathf.Clamp01(elapsed / 0.25f);
                        yield return null;
                    }
                }
                DismissInfoPopup();
            }
            _popupAutoDismiss = null;
        }

        // ====================================================================
        // P&C: Building Info Detail Panel
        // ====================================================================

        private GameObject _buildingInfoPanel;
        private GameObject _strongholdUpgradeBanner;

        /// <summary>P&C: Full-screen building info panel with stats, description, production info.</summary>
        private void ShowBuildingInfoPanel(string buildingId, string instanceId, int tier)
        {
            DismissBuildingInfoPanel();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _buildingInfoPanel = new GameObject("BuildingInfoPanel");
            _buildingInfoPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _buildingInfoPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _buildingInfoPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _buildingInfoPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissBuildingInfoPanel);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_buildingInfoPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.15f);
            panelRect.anchorMax = new Vector2(0.94f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelImg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            Color headerColor = GetCategoryHeaderColor(buildingId);

            // Title bar
            AddInfoPanelText(panel.transform, "Title", displayName, 18, FontStyle.Bold, headerColor,
                new Vector2(0.05f, 0.88f), new Vector2(0.70f, 0.97f), TextAnchor.MiddleLeft);

            // Level display
            string tierStar = tier > 1 ? $" \u2605" : "";
            AddInfoPanelText(panel.transform, "Level", $"Level {tier}{tierStar}", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.72f, 0.88f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleRight);

            // Separator line
            var sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(panel.transform, false);
            var sepRect = sepGO.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.05f, 0.865f);
            sepRect.anchorMax = new Vector2(0.95f, 0.87f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = new Color(0.85f, 0.65f, 0.15f, 0.4f);
            sepImg.raycastTarget = false;

            // P&C: Tier sprite preview — current vs next tier side by side
            int nextTier = tier + 1;
            bool hasNextTier = nextTier <= 3;
            {
                // Container for sprite preview row
                var previewRow = new GameObject("TierPreview");
                previewRow.transform.SetParent(panel.transform, false);
                var previewRect = previewRow.AddComponent<RectTransform>();
                previewRect.anchorMin = new Vector2(0.05f, 0.68f);
                previewRect.anchorMax = new Vector2(0.95f, 0.86f);
                previewRect.offsetMin = Vector2.zero;
                previewRect.offsetMax = Vector2.zero;

                // Current tier sprite
                Sprite curSprite = LoadBuildingSprite(buildingId, tier);
                if (curSprite != null)
                {
                    var curGO = new GameObject("CurrentTier");
                    curGO.transform.SetParent(previewRow.transform, false);
                    var curRect = curGO.AddComponent<RectTransform>();
                    curRect.anchorMin = hasNextTier ? new Vector2(0.05f, 0.10f) : new Vector2(0.25f, 0.10f);
                    curRect.anchorMax = hasNextTier ? new Vector2(0.35f, 0.95f) : new Vector2(0.75f, 0.95f);
                    curRect.offsetMin = Vector2.zero;
                    curRect.offsetMax = Vector2.zero;
                    var curImg = curGO.AddComponent<Image>();
                    curImg.sprite = curSprite;
                    curImg.preserveAspect = true;
                    curImg.raycastTarget = false;

                    // "Current" label below
                    AddInfoPanelText(previewRow.transform, "CurLabel", $"Lv.{tier}", 9, FontStyle.Normal,
                        new Color(0.75f, 0.72f, 0.65f),
                        curRect.anchorMin - new Vector2(0, 0.10f), new Vector2(curRect.anchorMax.x, curRect.anchorMin.y),
                        TextAnchor.MiddleCenter);
                }

                // Next tier sprite preview (if not max)
                if (hasNextTier)
                {
                    // Arrow between sprites
                    AddInfoPanelText(previewRow.transform, "Arrow", "\u2192", 16, FontStyle.Bold,
                        new Color(0.95f, 0.82f, 0.35f),
                        new Vector2(0.38f, 0.30f), new Vector2(0.52f, 0.70f), TextAnchor.MiddleCenter);

                    Sprite nextSprite = LoadBuildingSprite(buildingId, nextTier);
                    if (nextSprite != null)
                    {
                        var nextGO = new GameObject("NextTier");
                        nextGO.transform.SetParent(previewRow.transform, false);
                        var nextRect = nextGO.AddComponent<RectTransform>();
                        nextRect.anchorMin = new Vector2(0.55f, 0.10f);
                        nextRect.anchorMax = new Vector2(0.85f, 0.95f);
                        nextRect.offsetMin = Vector2.zero;
                        nextRect.offsetMax = Vector2.zero;
                        var nextImg = nextGO.AddComponent<Image>();
                        nextImg.sprite = nextSprite;
                        nextImg.preserveAspect = true;
                        nextImg.raycastTarget = false;

                        // Gold glow behind next tier to highlight it
                        var glowGO = new GameObject("NextGlow");
                        glowGO.transform.SetParent(nextGO.transform, false);
                        glowGO.transform.SetAsFirstSibling();
                        var glowRect = glowGO.AddComponent<RectTransform>();
                        glowRect.anchorMin = new Vector2(-0.15f, -0.08f);
                        glowRect.anchorMax = new Vector2(1.15f, 1.08f);
                        glowRect.offsetMin = Vector2.zero;
                        glowRect.offsetMax = Vector2.zero;
                        var glowImg = glowGO.AddComponent<Image>();
                        glowImg.color = new Color(0.95f, 0.82f, 0.25f, 0.15f);
                        glowImg.raycastTarget = false;

                        // "Next" label
                        AddInfoPanelText(previewRow.transform, "NextLabel", $"Lv.{nextTier}", 9, FontStyle.Bold,
                            new Color(0.95f, 0.82f, 0.35f),
                            new Vector2(0.55f, 0.0f), new Vector2(0.85f, 0.10f), TextAnchor.MiddleCenter);
                    }

                    // P&C: Stat comparison (what improves at next tier)
                    string statDelta = GetTierUpgradeStatDelta(buildingId, tier, nextTier);
                    if (!string.IsNullOrEmpty(statDelta))
                    {
                        AddInfoPanelText(previewRow.transform, "StatDelta", statDelta, 8, FontStyle.Normal,
                            new Color(0.50f, 0.90f, 0.50f),
                            new Vector2(0.87f, 0.20f), new Vector2(1.0f, 0.80f), TextAnchor.MiddleLeft);
                    }
                }
            }

            // Building description
            string desc = GetBuildingDescription(buildingId);
            AddInfoPanelText(panel.transform, "Description", desc, 11, FontStyle.Normal,
                new Color(0.80f, 0.78f, 0.75f),
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.67f), TextAnchor.UpperLeft);

            // Stats section
            float statsY = 0.52f;
            AddInfoPanelText(panel.transform, "StatsHeader", "STATS", 12, FontStyle.Bold,
                new Color(0.70f, 0.85f, 1f),
                new Vector2(0.05f, statsY - 0.02f), new Vector2(0.50f, statsY + 0.04f), TextAnchor.MiddleLeft);

            // Resource production info (if applicable)
            if (ResourceBuildingTypes.TryGetValue(buildingId, out var resInfo))
            {
                int rate = (tier + 1) * 250;
                string rateText = rate >= 1000 ? $"{rate / 1000f:F1}K" : $"{rate}";
                AddInfoPanelText(panel.transform, "ProdRate",
                    $"Production: +{rateText}/hr ({resInfo.Name})", 11, FontStyle.Normal, resInfo.Tint,
                    new Vector2(0.05f, statsY - 0.10f), new Vector2(0.95f, statsY - 0.03f), TextAnchor.MiddleLeft);
                statsY -= 0.07f;
            }

            // Upgrade costs for next tier
            string costStr = GetUpgradeCostString(instanceId, tier);
            if (costStr != null)
            {
                string costLabel = costStr == "MAX LEVEL" ? "Upgrade: MAX LEVEL" : $"Next upgrade: {costStr}";
                Color costColor = costStr == "MAX LEVEL"
                    ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.90f, 0.85f, 0.70f);
                AddInfoPanelText(panel.transform, "UpgradeCost", costLabel, 11, FontStyle.Normal, costColor,
                    new Vector2(0.05f, statsY - 0.10f), new Vector2(0.95f, statsY - 0.03f), TextAnchor.MiddleLeft);
            }

            // P&C: Power contribution
            int powerContrib = GetBuildingPowerContribution(buildingId, tier);
            if (powerContrib > 0)
            {
                string powerStr = powerContrib >= 1000 ? $"{powerContrib / 1000f:F1}K" : $"{powerContrib}";
                AddInfoPanelText(panel.transform, "Power", $"\u2694 Power: +{powerStr}", 11, FontStyle.Normal,
                    new Color(0.95f, 0.70f, 0.35f),
                    new Vector2(0.05f, 0.20f), new Vector2(0.50f, 0.27f), TextAnchor.MiddleLeft);
            }

            // P&C: Demolish button (not for stronghold)
            if (buildingId != "stronghold")
            {
                var demGO = new GameObject("DemolishBtn");
                demGO.transform.SetParent(panel.transform, false);
                var demRect = demGO.AddComponent<RectTransform>();
                demRect.anchorMin = new Vector2(0.05f, 0.03f);
                demRect.anchorMax = new Vector2(0.45f, 0.12f);
                demRect.offsetMin = Vector2.zero;
                demRect.offsetMax = Vector2.zero;
                var demImg = demGO.AddComponent<Image>();
                demImg.color = new Color(0.65f, 0.18f, 0.15f, 0.85f);
                demImg.raycastTarget = true;
                var demOutline = demGO.AddComponent<Outline>();
                demOutline.effectColor = new Color(0.9f, 0.3f, 0.2f, 0.5f);
                demOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var demBtn = demGO.AddComponent<Button>();
                demBtn.targetGraphic = demImg;
                string demInstanceId = instanceId;
                string demBuildingId = buildingId;
                demBtn.onClick.AddListener(() => ShowDemolishConfirmDialog(demInstanceId, demBuildingId));
                AddInfoPanelText(demGO.transform, "Label", "\u2620 Demolish", 11, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Upgrade button in info panel
            {
                string infoCostStr = GetUpgradeCostString(instanceId, tier);
                bool infoMaxLevel = infoCostStr == "MAX LEVEL";
                var upgGO = new GameObject("UpgradeBtn");
                upgGO.transform.SetParent(panel.transform, false);
                var upgRect = upgGO.AddComponent<RectTransform>();
                upgRect.anchorMin = new Vector2(0.55f, 0.03f);
                upgRect.anchorMax = new Vector2(0.95f, 0.12f);
                upgRect.offsetMin = Vector2.zero;
                upgRect.offsetMax = Vector2.zero;
                var upgImg = upgGO.AddComponent<Image>();
                upgImg.color = infoMaxLevel
                    ? new Color(0.30f, 0.30f, 0.30f, 0.7f)
                    : new Color(0.15f, 0.60f, 0.20f, 0.90f);
                upgImg.raycastTarget = true;
                var upgOutline = upgGO.AddComponent<Outline>();
                upgOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.5f);
                upgOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var upgBtn = upgGO.AddComponent<Button>();
                upgBtn.targetGraphic = upgImg;
                string upgInstanceId = instanceId;
                string upgBuildingIdLocal = buildingId;
                int upgTierLocal = tier;
                upgBtn.onClick.AddListener(() =>
                {
                    if (infoMaxLevel) return;
                    string warning = GetUpgradeBlockReason(upgInstanceId, upgTierLocal);
                    if (warning != null) { ShowUpgradeBlockedToast(warning); return; }
                    DismissBuildingInfoPanel();
                    ShowUpgradeConfirmDialog(upgInstanceId, upgBuildingIdLocal, upgTierLocal);
                });
                AddInfoPanelText(upgGO.transform, "Label", infoMaxLevel ? "MAX LEVEL" : "\u2B06 Upgrade", 11,
                    FontStyle.Bold, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // Close button (X in top-right)
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.90f);
            closeRect.anchorMax = new Vector2(0.98f, 1.0f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(DismissBuildingInfoPanel);
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _buildingInfoPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void AddInfoPanelText(Transform parent, string name, string content, int fontSize,
            FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        /// <summary>P&C: Load a building sprite by buildingId and tier number (1-3).</summary>
        private static Sprite LoadBuildingSprite(string buildingId, int tier)
        {
            string spriteName = $"{buildingId}_t{tier}";
            var sprite = Resources.Load<Sprite>($"Buildings/{spriteName}");
            #if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Buildings/{spriteName}.png");
            #endif
            return sprite;
        }

        /// <summary>P&C: Get stat delta description for upgrading from current to next tier.</summary>
        private static string GetTierUpgradeStatDelta(string buildingId, int currentTier, int nextTier)
        {
            var lines = new List<string>();

            // Resource production delta
            if (buildingId == "grain_farm" || buildingId == "iron_mine" || buildingId == "stone_quarry" || buildingId == "arcane_tower")
            {
                int curRate = (currentTier + 1) * 250;
                int nextRate = (nextTier + 1) * 250;
                lines.Add($"+{nextRate - curRate}/hr");
            }

            // Power delta
            int curPower = GetBuildingPowerContribution(buildingId, currentTier);
            int nextPower = GetBuildingPowerContribution(buildingId, nextTier);
            if (nextPower > curPower)
            {
                int delta = nextPower - curPower;
                string deltaStr = delta >= 1000 ? $"+{delta / 1000f:F1}K" : $"+{delta}";
                lines.Add($"{deltaStr} \u2694");
            }

            // Military capacity
            if (buildingId == "barracks")
                lines.Add($"+{500} troops");
            else if (buildingId == "training_ground")
                lines.Add($"+{300} troops");

            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        private void DismissBuildingInfoPanel()
        {
            if (_buildingInfoPanel != null)
            {
                Destroy(_buildingInfoPanel);
                _buildingInfoPanel = null;
            }
        }

        private static string GetBuildingDescription(string buildingId)
        {
            return buildingId switch
            {
                "stronghold" => "The heart of your empire. Upgrade to unlock new buildings and raise level caps for all structures.",
                "barracks" => "Trains soldiers for your armies. Higher tiers unlock elite troop types and increase training speed.",
                "forge" => "Crafts weapons and armor for your heroes. Upgrade for access to higher quality equipment.",
                "marketplace" => "Facilitates trade between players. Higher tiers reduce trade fees and unlock rare goods.",
                "academy" => "Research facility for unlocking technology upgrades that enhance your empire.",
                "grain_farm" => "Produces grain to sustain your population and armies. Upgrade for higher yields.",
                "iron_mine" => "Extracts iron ore for military equipment and construction. Higher tiers boost output.",
                "stone_quarry" => "Quarries stone for building construction. Essential for expanding your empire.",
                "arcane_tower" => "Channels arcane essence from ley lines. Required for magical research and enchanting.",
                "wall" => "Fortified perimeter defense. Higher tiers increase durability and unlock guard towers.",
                "watch_tower" => "Provides scouting intelligence on approaching threats. Upgrade for longer detection range.",
                "guild_hall" => "Coordinates alliance operations. Upgrade to increase rally capacity for joint attacks.",
                "embassy" => "Houses allied reinforcements. Higher tiers allow more troops from alliance members.",
                "training_ground" => "Military staging area for drills. Provides passive combat stat bonuses.",
                "hero_shrine" => "Revives and heals fallen heroes. Higher tiers reduce revival time and costs.",
                "laboratory" => "Conducts advanced experiments. Unlocks special abilities and enhancement recipes.",
                "library" => "Repository of ancient knowledge. Provides research speed bonuses.",
                "armory" => "Stores and maintains equipment. Upgrade to increase gear storage capacity.",
                "enchanting_tower" => "Imbues equipment with magical properties. Higher tiers unlock stronger enchantments.",
                "observatory" => "Monitors the skies for celestial events and incoming threats from afar.",
                "archive" => "Preserves historical records and tactical manuals. Provides experience bonuses.",
                _ => "A building in your empire."
            };
        }

        /// <summary>P&C: Approximate power contribution per building type+tier.</summary>
        private static int GetBuildingPowerContribution(string buildingId, int tier)
        {
            int basePower = buildingId switch
            {
                "stronghold" => 5000,
                "barracks" or "training_ground" => 1500,
                "wall" or "watch_tower" => 1000,
                "armory" or "forge" => 800,
                "academy" or "laboratory" or "library" => 600,
                "guild_hall" or "embassy" => 500,
                "hero_shrine" => 400,
                "grain_farm" or "iron_mine" or "stone_quarry" or "arcane_tower" => 300,
                "enchanting_tower" or "observatory" or "archive" => 350,
                "marketplace" => 250,
                _ => 200
            };
            return basePower * (tier + 1);
        }

        /// <summary>P&C: Demolish confirmation dialog with "Are you sure?" prompt.</summary>
        private GameObject _demolishDialog;

        private void ShowDemolishConfirmDialog(string instanceId, string buildingId)
        {
            DismissBuildingInfoPanel();
            if (_demolishDialog != null) { Destroy(_demolishDialog); _demolishDialog = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _demolishDialog = new GameObject("DemolishDialog");
            _demolishDialog.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _demolishDialog.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _demolishDialog.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.5f);
            dimImg.raycastTarget = true;
            var dimBtn = _demolishDialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissDemolishDialog);

            // Dialog panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_demolishDialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.35f);
            panelRect.anchorMax = new Vector2(0.85f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.05f, 0.14f, 0.96f);
            panelImg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.25f, 0.15f, 0.8f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;

            // Warning icon + title
            AddInfoPanelText(panel.transform, "Title", $"\u2620 Demolish {displayName}?", 14, FontStyle.Bold,
                new Color(0.95f, 0.35f, 0.25f),
                new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);

            // Warning text
            AddInfoPanelText(panel.transform, "Warning", "This action is irreversible.\nNo resources will be refunded.", 11, FontStyle.Normal,
                new Color(0.80f, 0.75f, 0.65f),
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.68f), TextAnchor.MiddleCenter);

            // Cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(panel.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.08f, 0.06f);
            cancelRect.anchorMax = new Vector2(0.45f, 0.30f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.30f, 0.30f, 0.35f, 0.90f);
            cancelImg.raycastTarget = true;
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(DismissDemolishDialog);
            AddInfoPanelText(cancelGO.transform, "Label", "Cancel", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Confirm demolish button
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(panel.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.55f, 0.06f);
            confirmRect.anchorMax = new Vector2(0.92f, 0.30f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = new Color(0.75f, 0.20f, 0.15f, 0.92f);
            confirmImg.raycastTarget = true;
            var confirmOutline = confirmGO.AddComponent<Outline>();
            confirmOutline.effectColor = new Color(1f, 0.4f, 0.3f, 0.5f);
            confirmOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            string capId = instanceId;
            confirmBtn.onClick.AddListener(() =>
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                    bm.DemolishBuilding(capId);
                DismissDemolishDialog();
            });
            AddInfoPanelText(confirmGO.transform, "Label", "\u2620 DEMOLISH", 12, FontStyle.Bold,
                Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _demolishDialog.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void DismissDemolishDialog()
        {
            if (_demolishDialog != null) { Destroy(_demolishDialog); _demolishDialog = null; }
        }

        // ====================================================================
        // P&C: Upgrade Confirmation Dialog
        // ====================================================================

        private GameObject _upgradeConfirmDialog;

        /// <summary>P&C: Show upgrade confirmation dialog with resource cost breakdown, build time, tier preview.</summary>
        private void ShowUpgradeConfirmDialog(string instanceId, string buildingId, int currentTier)
        {
            DismissUpgradeConfirmDialog();

            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;
            if (!bm.PlacedBuildings.TryGetValue(instanceId, out var placed) || placed.Data == null) return;
            var nextTierData = placed.Data.GetTier(currentTier);
            if (nextTierData == null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _upgradeConfirmDialog = new GameObject("UpgradeConfirmDialog");
            _upgradeConfirmDialog.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _upgradeConfirmDialog.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _upgradeConfirmDialog.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _upgradeConfirmDialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissUpgradeConfirmDialog);

            // Center panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_upgradeConfirmDialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.25f);
            panelRect.anchorMax = new Vector2(0.92f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelImg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            int nextTier = currentTier + 1;

            // Title: "Upgrade [Building]?"
            AddInfoPanelText(panel.transform, "Title", $"Upgrade {displayName}?", 16, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);

            // Tier change: "Lv.X → Lv.Y"
            AddInfoPanelText(panel.transform, "TierChange", $"Lv.{currentTier} \u2192 Lv.{nextTier}", 13, FontStyle.Bold,
                new Color(0.80f, 0.90f, 0.50f),
                new Vector2(0.30f, 0.78f), new Vector2(0.70f, 0.87f), TextAnchor.MiddleCenter);

            // Tier sprites side by side
            Sprite curSprite = LoadBuildingSprite(buildingId, currentTier);
            Sprite nextSprite = LoadBuildingSprite(buildingId, nextTier);
            if (curSprite != null)
            {
                var curGO = new GameObject("CurSprite");
                curGO.transform.SetParent(panel.transform, false);
                var curRect = curGO.AddComponent<RectTransform>();
                curRect.anchorMin = new Vector2(0.10f, 0.55f);
                curRect.anchorMax = new Vector2(0.35f, 0.78f);
                curRect.offsetMin = Vector2.zero;
                curRect.offsetMax = Vector2.zero;
                var curImg = curGO.AddComponent<Image>();
                curImg.sprite = curSprite;
                curImg.preserveAspect = true;
                curImg.raycastTarget = false;
                curImg.color = new Color(1, 1, 1, 0.6f); // Dim current
            }
            AddInfoPanelText(panel.transform, "Arrow", "\u2192", 18, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.38f, 0.62f), new Vector2(0.62f, 0.72f), TextAnchor.MiddleCenter);
            if (nextSprite != null)
            {
                var nextGO = new GameObject("NextSprite");
                nextGO.transform.SetParent(panel.transform, false);
                var nextRect = nextGO.AddComponent<RectTransform>();
                nextRect.anchorMin = new Vector2(0.65f, 0.55f);
                nextRect.anchorMax = new Vector2(0.90f, 0.78f);
                nextRect.offsetMin = Vector2.zero;
                nextRect.offsetMax = Vector2.zero;
                var nextImg = nextGO.AddComponent<Image>();
                nextImg.sprite = nextSprite;
                nextImg.preserveAspect = true;
                nextImg.raycastTarget = false;
            }

            // Resource cost breakdown with afford indicators
            ServiceLocator.TryGet<ResourceManager>(out var rm);
            float costY = 0.48f;

            void AddCostRow(string icon, string resName, int cost, long current, Color tint)
            {
                if (cost <= 0) return;
                bool canAfford = current >= cost;
                string costStr = FormatCost(cost);
                string curStr = FormatCost((int)Mathf.Min(current, 999999999));
                Color rowColor = canAfford ? new Color(0.50f, 0.90f, 0.50f) : new Color(0.95f, 0.40f, 0.35f);

                AddInfoPanelText(panel.transform, $"Cost_{resName}", $"{icon} {resName}", 11, FontStyle.Normal, tint,
                    new Vector2(0.05f, costY - 0.06f), new Vector2(0.35f, costY + 0.01f), TextAnchor.MiddleLeft);
                AddInfoPanelText(panel.transform, $"CostAmt_{resName}", costStr, 11, FontStyle.Bold, rowColor,
                    new Vector2(0.38f, costY - 0.06f), new Vector2(0.58f, costY + 0.01f), TextAnchor.MiddleRight);
                AddInfoPanelText(panel.transform, $"CostCur_{resName}", $"/{curStr}", 9, FontStyle.Normal,
                    new Color(0.6f, 0.6f, 0.6f),
                    new Vector2(0.59f, costY - 0.06f), new Vector2(0.80f, costY + 0.01f), TextAnchor.MiddleLeft);

                string check = canAfford ? "\u2713" : "\u2717"; // ✓ or ✗
                AddInfoPanelText(panel.transform, $"CostChk_{resName}", check, 12, FontStyle.Bold, rowColor,
                    new Vector2(0.82f, costY - 0.06f), new Vector2(0.95f, costY + 0.01f), TextAnchor.MiddleCenter);
                costY -= 0.08f;
            }

            long curStone = rm != null ? rm.Stone : 0;
            long curIron = rm != null ? rm.Iron : 0;
            long curGrain = rm != null ? rm.Grain : 0;
            long curArcane = rm != null ? rm.ArcaneEssence : 0;

            AddCostRow("\u25C8", "Stone", nextTierData.stoneCost, curStone, new Color(0.70f, 0.70f, 0.75f));
            AddCostRow("\u2666", "Iron", nextTierData.ironCost, curIron, new Color(0.80f, 0.65f, 0.50f));
            AddCostRow("\u2740", "Grain", nextTierData.grainCost, curGrain, new Color(0.65f, 0.85f, 0.45f));
            AddCostRow("\u2726", "Arcane", nextTierData.arcaneEssenceCost, curArcane, new Color(0.60f, 0.50f, 0.90f));

            // Build time
            float buildSeconds = nextTierData.buildTimeSeconds;
            string timeStr = FormatTimeRemaining(Mathf.RoundToInt(buildSeconds));
            AddInfoPanelText(panel.transform, "BuildTime", $"\u23F1 Build Time: {timeStr}", 11, FontStyle.Normal,
                new Color(0.80f, 0.78f, 0.70f),
                new Vector2(0.05f, 0.14f), new Vector2(0.60f, 0.22f), TextAnchor.MiddleLeft);

            // Cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(panel.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.05f, 0.03f);
            cancelRect.anchorMax = new Vector2(0.45f, 0.12f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.45f, 0.20f, 0.20f, 0.85f);
            cancelImg.raycastTarget = true;
            var cancelOutline = cancelGO.AddComponent<Outline>();
            cancelOutline.effectColor = new Color(0.8f, 0.3f, 0.2f, 0.5f);
            cancelOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(DismissUpgradeConfirmDialog);
            AddInfoPanelText(cancelGO.transform, "Label", "Cancel", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Upgrade button
            bool canAffordAll = curStone >= nextTierData.stoneCost && curIron >= nextTierData.ironCost
                && curGrain >= nextTierData.grainCost && curArcane >= nextTierData.arcaneEssenceCost;
            var upgGO = new GameObject("UpgradeBtn");
            upgGO.transform.SetParent(panel.transform, false);
            var upgRect = upgGO.AddComponent<RectTransform>();
            upgRect.anchorMin = new Vector2(0.55f, 0.03f);
            upgRect.anchorMax = new Vector2(0.95f, 0.12f);
            upgRect.offsetMin = Vector2.zero;
            upgRect.offsetMax = Vector2.zero;
            var upgImg = upgGO.AddComponent<Image>();
            upgImg.color = canAffordAll
                ? new Color(0.15f, 0.60f, 0.20f, 0.90f)
                : new Color(0.35f, 0.35f, 0.35f, 0.70f);
            upgImg.raycastTarget = true;
            var upgOutline = upgGO.AddComponent<Outline>();
            upgOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.5f);
            upgOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var upgBtn = upgGO.AddComponent<Button>();
            upgBtn.targetGraphic = upgImg;
            string capId = instanceId;
            string capBid = buildingId;
            int capTier = currentTier;
            upgBtn.onClick.AddListener(() =>
            {
                if (!canAffordAll) { ShowUpgradeBlockedToast("Not enough resources!"); return; }
                DismissUpgradeConfirmDialog();
                // Publish upgrade start event (BuildingManager handles queue)
                EventBus.Publish(new BuildingUpgradeStartedEvent(capId, capTier + 1, nextTierData.buildTimeSeconds));
                if (ServiceLocator.TryGet<BuildingManager>(out var mgr))
                    mgr.StartUpgrade(capId);
            });
            AddInfoPanelText(upgGO.transform, "Label", canAffordAll ? "\u2B06 UPGRADE" : "Can't Afford", 12,
                FontStyle.Bold, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _upgradeConfirmDialog.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void DismissUpgradeConfirmDialog()
        {
            if (_upgradeConfirmDialog != null) { Destroy(_upgradeConfirmDialog); _upgradeConfirmDialog = null; }
        }

        // ====================================================================
        // P&C: Empty Cell → Building Placement Selector
        // ====================================================================

        private GameObject _buildSelectorPanel;
        private Vector2Int _buildSelectorGridPos;

        /// <summary>P&C: Category-grouped building types available for placement.</summary>
        private static readonly (string Category, Color Color, string[] Buildings)[] BuildCategories = new[]
        {
            ("Military", new Color(0.85f, 0.30f, 0.25f), new[] { "barracks", "training_ground", "armory" }),
            ("Resource", new Color(0.30f, 0.75f, 0.35f), new[] { "grain_farm", "iron_mine", "stone_quarry", "arcane_tower" }),
            ("Research", new Color(0.35f, 0.55f, 0.90f), new[] { "academy", "laboratory", "library", "archive", "observatory" }),
            ("Magic", new Color(0.65f, 0.40f, 0.90f), new[] { "enchanting_tower", "hero_shrine" }),
            ("Defense", new Color(0.55f, 0.60f, 0.70f), new[] { "wall", "watch_tower" }),
            ("Social", new Color(0.85f, 0.70f, 0.25f), new[] { "marketplace", "guild_hall", "embassy", "forge" }),
        };

        private void OnEmptyCellTapped(EmptyCellTappedEvent evt)
        {
            if (_moveMode) return; // Don't show selector during move mode
            DismissInfoPopup();
            ShowBuildSelector(evt.GridPosition);
        }

        private void ShowBuildSelector(Vector2Int gridPos)
        {
            DismissBuildSelector();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _buildSelectorGridPos = gridPos;
            _buildSelectorPanel = new GameObject("BuildSelectorPanel");
            _buildSelectorPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _buildSelectorPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _buildSelectorPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _buildSelectorPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissBuildSelector);

            // Scrollable panel at bottom half
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_buildSelectorPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.03f, 0.10f);
            panelRect.anchorMax = new Vector2(0.97f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelImg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.8f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            AddInfoPanelText(panel.transform, "Title", "BUILD NEW", 16, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.90f), new Vector2(0.70f, 0.98f), TextAnchor.MiddleLeft);

            // Grid position label
            AddInfoPanelText(panel.transform, "Coords", $"at ({gridPos.x}, {gridPos.y})", 10, FontStyle.Normal,
                new Color(0.6f, 0.6f, 0.6f),
                new Vector2(0.72f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleRight);

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.90f, 0.90f);
            closeRect.anchorMax = new Vector2(0.98f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(DismissBuildSelector);
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Build category rows
            float yTop = 0.87f;
            float rowHeight = 0.13f;
            float gap = 0.01f;

            foreach (var (category, color, buildings) in BuildCategories)
            {
                float rowBot = yTop - rowHeight;

                // Category icon + label
                string catIcon = category switch
                {
                    "Military" => "\u2694", // ⚔
                    "Resource" => "\u26CF", // ⛏
                    "Research" => "\u2697", // ⚗
                    "Magic" => "\u2728",    // ✨
                    "Defense" => "\u26E8",  // ⛨
                    "Social" => "\u2764",   // ❤
                    _ => "\u25A0"
                };
                AddInfoPanelText(panel.transform, $"Cat_{category}", $"{catIcon} {category}", 10, FontStyle.Bold, color,
                    new Vector2(0.03f, rowBot), new Vector2(0.18f, yTop), TextAnchor.MiddleLeft);

                // Building buttons in row
                float btnWidth = 0.78f / buildings.Length;

                // P&C: Get current stronghold level for lock checking
                int currentSHLevel = 0;
                if (ServiceLocator.TryGet<BuildingManager>(out var selectorBm))
                {
                    foreach (var pb in selectorBm.PlacedBuildings.Values)
                    {
                        if (pb.Data != null && pb.Data.buildingId == "stronghold")
                        { currentSHLevel = pb.CurrentTier; break; }
                    }
                }

                for (int i = 0; i < buildings.Length; i++)
                {
                    string bid = buildings[i];
                    string displayName = BuildingDisplayNames.TryGetValue(bid, out var dn) ? dn : bid;
                    float x0 = 0.20f + i * btnWidth;
                    float x1 = x0 + btnWidth - 0.005f;

                    // P&C: Check if building is locked (stronghold level requirement)
                    int requiredSH = GetBuildingUnlockLevel(bid);
                    bool isLocked = currentSHLevel < requiredSH;

                    var btnGO = new GameObject($"Build_{bid}");
                    btnGO.transform.SetParent(panel.transform, false);
                    var btnRect = btnGO.AddComponent<RectTransform>();
                    btnRect.anchorMin = new Vector2(x0, rowBot + 0.01f);
                    btnRect.anchorMax = new Vector2(x1, yTop - 0.01f);
                    btnRect.offsetMin = Vector2.zero;
                    btnRect.offsetMax = Vector2.zero;

                    var btnBg = btnGO.AddComponent<Image>();
                    btnBg.color = isLocked
                        ? new Color(0.15f, 0.15f, 0.18f, 0.85f)  // Grayed out
                        : new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.85f);
                    btnBg.raycastTarget = true;
                    var btnOutline = btnGO.AddComponent<Outline>();
                    btnOutline.effectColor = isLocked
                        ? new Color(0.3f, 0.3f, 0.3f, 0.4f)
                        : new Color(color.r, color.g, color.b, 0.5f);
                    btnOutline.effectDistance = new Vector2(0.8f, -0.8f);

                    var btn = btnGO.AddComponent<Button>();
                    btn.targetGraphic = btnBg;
                    string capBid = bid;
                    Vector2Int capPos = gridPos;
                    int capReqSH = requiredSH;
                    btn.onClick.AddListener(() => {
                        if (currentSHLevel < capReqSH)
                        {
                            ShowUpgradeBlockedToast($"Requires Stronghold Lv.{capReqSH}");
                            return;
                        }
                        DismissBuildSelector();
                        EventBus.Publish(new PlacementConfirmedEvent(capBid, capPos,
                            BuildingSizes.TryGetValue(capBid, out var sz) ? sz : new Vector2Int(2, 2)));
                    });

                    // P&C: Building sprite thumbnail (top portion of button)
                    Sprite thumbSprite = LoadBuildingSprite(bid, 1);
                    if (thumbSprite != null)
                    {
                        var thumbGO = new GameObject("Thumb");
                        thumbGO.transform.SetParent(btnGO.transform, false);
                        var thumbRect = thumbGO.AddComponent<RectTransform>();
                        thumbRect.anchorMin = new Vector2(0.15f, 0.28f);
                        thumbRect.anchorMax = new Vector2(0.85f, 0.92f);
                        thumbRect.offsetMin = Vector2.zero;
                        thumbRect.offsetMax = Vector2.zero;
                        var thumbImg = thumbGO.AddComponent<Image>();
                        thumbImg.sprite = thumbSprite;
                        thumbImg.preserveAspect = true;
                        thumbImg.raycastTarget = false;
                    }

                    // Building name text (below thumbnail)
                    var lblGO = new GameObject("Label");
                    lblGO.transform.SetParent(btnGO.transform, false);
                    var lblRect = lblGO.AddComponent<RectTransform>();
                    lblRect.anchorMin = new Vector2(0, 0);
                    lblRect.anchorMax = new Vector2(1, 0.28f);
                    lblRect.offsetMin = new Vector2(1, 0);
                    lblRect.offsetMax = new Vector2(-1, 0);
                    var lblText = lblGO.AddComponent<Text>();
                    lblText.text = displayName;
                    lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    lblText.fontSize = 7;
                    lblText.fontStyle = FontStyle.Bold;
                    lblText.alignment = TextAnchor.MiddleCenter;
                    lblText.color = Color.white;
                    lblText.raycastTarget = false;
                    var lblShadow = lblGO.AddComponent<Shadow>();
                    lblShadow.effectColor = new Color(0, 0, 0, 0.8f);
                    lblShadow.effectDistance = new Vector2(0.5f, -0.5f);

                    // P&C: Production/function hint on resource building buttons
                    string prodHint = GetBuildingSelectorHint(bid);
                    if (!string.IsNullOrEmpty(prodHint))
                    {
                        var hintGO = new GameObject("Hint");
                        hintGO.transform.SetParent(btnGO.transform, false);
                        var hintRect = hintGO.AddComponent<RectTransform>();
                        hintRect.anchorMin = new Vector2(0, 0.90f);
                        hintRect.anchorMax = new Vector2(1, 1.0f);
                        hintRect.offsetMin = Vector2.zero;
                        hintRect.offsetMax = Vector2.zero;
                        var hintText = hintGO.AddComponent<Text>();
                        hintText.text = prodHint;
                        hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        hintText.fontSize = 6;
                        hintText.alignment = TextAnchor.MiddleCenter;
                        hintText.color = new Color(0.65f, 0.90f, 0.65f);
                        hintText.raycastTarget = false;
                    }

                    // P&C: Lock overlay for buildings that require higher stronghold
                    if (isLocked)
                    {
                        var lockOverlay = new GameObject("LockOverlay");
                        lockOverlay.transform.SetParent(btnGO.transform, false);
                        var lockRect = lockOverlay.AddComponent<RectTransform>();
                        lockRect.anchorMin = Vector2.zero;
                        lockRect.anchorMax = Vector2.one;
                        lockRect.offsetMin = Vector2.zero;
                        lockRect.offsetMax = Vector2.zero;
                        var lockImg = lockOverlay.AddComponent<Image>();
                        lockImg.color = new Color(0.05f, 0.04f, 0.08f, 0.65f);
                        lockImg.raycastTarget = false;

                        // Lock icon
                        AddInfoPanelText(btnGO.transform, "LockIcon", "\uD83D\uDD12", 14, FontStyle.Bold,
                            new Color(0.60f, 0.55f, 0.50f),
                            new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.75f), TextAnchor.MiddleCenter);

                        // Required level text
                        AddInfoPanelText(btnGO.transform, "ReqLevel", $"SH Lv.{requiredSH}", 6, FontStyle.Bold,
                            new Color(0.85f, 0.55f, 0.25f),
                            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.25f), TextAnchor.MiddleCenter);
                    }
                }

                yTop = rowBot - gap;
            }

            // Fade in
            var cg = _buildSelectorPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        /// <summary>P&C: Get stronghold level required to unlock a building type.</summary>
        private static int GetBuildingUnlockLevel(string buildingId)
        {
            // Try to load from BuildingData ScriptableObject
            if (ServiceLocator.TryGet<BuildingManager>(out var bm))
            {
                foreach (var pb in bm.PlacedBuildings.Values)
                {
                    if (pb.Data != null && pb.Data.buildingId == buildingId)
                        return pb.Data.strongholdLevelRequired;
                }
            }
            // Fallback unlock levels for buildings not yet placed
            return buildingId switch
            {
                "grain_farm" or "iron_mine" or "stone_quarry" or "barracks" => 1,
                "wall" or "watch_tower" or "marketplace" => 2,
                "academy" or "training_ground" or "forge" => 3,
                "arcane_tower" or "guild_hall" or "armory" => 4,
                "laboratory" or "embassy" or "enchanting_tower" => 5,
                "library" or "hero_shrine" or "observatory" => 6,
                "archive" => 7,
                _ => 1
            };
        }

        /// <summary>P&C: Short production/function hint for build selector buttons.</summary>
        private static string GetBuildingSelectorHint(string buildingId)
        {
            return buildingId switch
            {
                "grain_farm" => "+250/hr",
                "iron_mine" => "+250/hr",
                "stone_quarry" => "+250/hr",
                "arcane_tower" => "+250/hr",
                "barracks" => "500 troops",
                "training_ground" => "300 troops",
                "armory" => "+ATK",
                "wall" => "+DEF",
                "watch_tower" => "Scout",
                "academy" => "Research",
                "laboratory" => "Tech",
                "marketplace" => "Trade",
                "guild_hall" => "Rally",
                "embassy" => "Diplomacy",
                _ => null
            };
        }

        private void DismissBuildSelector()
        {
            if (_buildSelectorPanel != null)
            {
                Destroy(_buildSelectorPanel);
                _buildSelectorPanel = null;
            }
        }

        /// <summary>P&C: Get formatted upgrade cost string for next tier from BuildingData.</summary>
        private static string GetUpgradeCostString(string instanceId, int currentTier)
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return null;
            if (!bm.PlacedBuildings.TryGetValue(instanceId, out var placed)) return null;
            if (placed.Data == null) return null;

            var nextTier = placed.Data.GetTier(currentTier); // tier array is 0-based, currentTier is display tier
            if (nextTier == null) return "MAX LEVEL";

            var parts = new List<string>();
            if (nextTier.stoneCost > 0) parts.Add($"\u25C8{FormatCost(nextTier.stoneCost)}");
            if (nextTier.ironCost > 0) parts.Add($"\u2666{FormatCost(nextTier.ironCost)}");
            if (nextTier.grainCost > 0) parts.Add($"\u2740{FormatCost(nextTier.grainCost)}");
            if (nextTier.arcaneEssenceCost > 0) parts.Add($"\u2726{FormatCost(nextTier.arcaneEssenceCost)}");

            return parts.Count > 0 ? string.Join("  ", parts) : null;
        }

        private static string FormatCost(int amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:F1}M";
            if (amount >= 1_000) return $"{amount / 1_000f:F1}K";
            return amount.ToString();
        }

        /// <summary>P&C: Check why an upgrade is blocked. Returns null if upgrade is allowed.</summary>
        private static string GetUpgradeBlockReason(string instanceId, int currentTier)
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return "Building system unavailable";
            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) return "Resource system unavailable";
            if (!bm.PlacedBuildings.TryGetValue(instanceId, out var placed) || placed.Data == null) return null;

            var nextTier = placed.Data.GetTier(currentTier);
            if (nextTier == null) return "Already at max level";

            // P&C: Check stronghold level requirement
            if (placed.Data.buildingId != "stronghold")
            {
                int requiredStrongholdTier = currentTier + 1; // Buildings need stronghold >= their target tier
                int strongholdTier = 0;
                foreach (var pb in bm.PlacedBuildings.Values)
                {
                    if (pb.Data != null && pb.Data.buildingId == "stronghold")
                    {
                        strongholdTier = pb.CurrentTier;
                        break;
                    }
                }
                if (strongholdTier < requiredStrongholdTier)
                    return $"Requires Stronghold Lv.{requiredStrongholdTier + 1}";
            }

            // Check builder queue capacity
            if (bm.BuildQueue.Count >= 2)
                return "Build queue full (2/2)";

            // Check resource affordability — report each missing resource
            var missing = new List<string>();
            if (nextTier.stoneCost > 0 && rm.Stone < nextTier.stoneCost)
                missing.Add($"Stone ({FormatCost(nextTier.stoneCost)})");
            if (nextTier.ironCost > 0 && rm.Iron < nextTier.ironCost)
                missing.Add($"Iron ({FormatCost(nextTier.ironCost)})");
            if (nextTier.grainCost > 0 && rm.Grain < nextTier.grainCost)
                missing.Add($"Grain ({FormatCost(nextTier.grainCost)})");
            if (nextTier.arcaneEssenceCost > 0 && rm.ArcaneEssence < nextTier.arcaneEssenceCost)
                missing.Add($"Arcane ({FormatCost(nextTier.arcaneEssenceCost)})");

            if (missing.Count > 0)
                return $"Not enough: {string.Join(", ", missing)}";

            return null; // Can upgrade
        }

        /// <summary>P&C: Red toast at bottom of screen showing why upgrade is blocked.</summary>
        private void ShowUpgradeBlockedToast(string message)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var toast = new GameObject("UpgradeBlockedToast");
            toast.transform.SetParent(canvas.transform, false);
            var rect = toast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.20f);
            rect.anchorMax = new Vector2(0.90f, 0.26f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = toast.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.10f, 0.10f, 0.92f);
            bg.raycastTarget = false;
            var outline = toast.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.3f, 0.3f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(toast.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 0);
            textRect.offsetMax = new Vector2(-6, 0);
            var text = textGO.AddComponent<Text>();
            text.text = message;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            StartCoroutine(FadeOutAndDestroyToast(toast));
        }

        private IEnumerator FadeOutAndDestroyToast(GameObject toast)
        {
            yield return new WaitForSeconds(2f);
            var cg = toast.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < 0.5f && toast != null)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / 0.5f);
                yield return null;
            }
            if (toast != null) Destroy(toast);
        }

        // ====================================================================
        // P&C: Upgrade-available arrow indicators
        // ====================================================================

        /// <summary>P&C: Orange pulsing arrows above buildings that can be upgraded (have enough resources).</summary>
        private void RefreshUpgradeArrows()
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;
            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) return;

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var existingArrow = p.VisualGO.transform.Find("UpgradeArrow");

                // Check if building is currently upgrading (don't show arrow)
                bool isUpgrading = false;
                foreach (var entry in bm.BuildQueue)
                {
                    if (entry.PlacedId == p.InstanceId) { isUpgrading = true; break; }
                }

                if (isUpgrading)
                {
                    if (existingArrow != null) Destroy(existingArrow.gameObject);
                    // P&C: Show progress bar + scaffolding on upgrading buildings
                    RefreshUpgradeProgressBar(p, bm);
                    continue;
                }
                else
                {
                    // Remove progress bar if upgrade finished
                    var existingBar = p.VisualGO.transform.Find("UpgradeProgressBar");
                    if (existingBar != null) Destroy(existingBar.gameObject);
                    var existingScaffold = p.VisualGO.transform.Find("Scaffolding");
                    if (existingScaffold != null) Destroy(existingScaffold.gameObject);
                    var existingQueueLabel = p.VisualGO.transform.Find("QueuePosLabel");
                    if (existingQueueLabel != null) Destroy(existingQueueLabel.gameObject);
                }

                // Check if can afford next tier
                bool canUpgrade = false;
                if (bm.PlacedBuildings.TryGetValue(p.InstanceId, out var placed) && placed.Data != null)
                {
                    var nextTier = placed.Data.GetTier(p.Tier); // 0-based array, Tier is display tier
                    if (nextTier != null)
                    {
                        canUpgrade = rm.CanAfford(nextTier.stoneCost, nextTier.ironCost,
                            nextTier.grainCost, nextTier.arcaneEssenceCost);
                    }
                }

                if (canUpgrade && existingArrow == null)
                {
                    CreateUpgradeArrow(p.VisualGO, p.InstanceId, p.BuildingId, p.Tier);
                }
                else if (!canUpgrade && existingArrow != null)
                {
                    Destroy(existingArrow.gameObject);
                }
            }
        }

        private void CreateUpgradeArrow(GameObject building, string instanceId, string buildingId, int tier)
        {
            var arrow = new GameObject("UpgradeArrow");
            arrow.transform.SetParent(building.transform, false);

            var rect = arrow.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 1.0f);
            rect.anchorMax = new Vector2(0.75f, 1.0f);
            rect.sizeDelta = new Vector2(28, 26);
            rect.anchoredPosition = new Vector2(0, 8);

            // Tappable background for the arrow
            var bgImg = arrow.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.08f, 0.02f, 0.65f);
            bgImg.raycastTarget = true;

            // P&C: Tapping the arrow triggers upgrade directly
            var btn = arrow.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            string capId = instanceId;
            string capBid = buildingId;
            int capTier = tier;
            btn.onClick.AddListener(() => {
                EventBus.Publish(new BuildingDoubleTappedEvent(capId, capBid, capTier));
            });

            // Arrow text child (so it renders above bg)
            var textGO = new GameObject("ArrowText");
            textGO.transform.SetParent(arrow.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = "\u25B2"; // ▲ upward triangle
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.65f, 0.10f); // Orange arrow
            text.raycastTarget = false;

            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.25f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Animate: bob up/down + pulse
            StartCoroutine(AnimateUpgradeArrow(arrow, rect));
        }

        private IEnumerator AnimateUpgradeArrow(GameObject arrow, RectTransform rect)
        {
            float phase = 0f;
            Vector2 basePos = rect.anchoredPosition;
            var arrowTextT = arrow.transform.Find("ArrowText");
            var text = arrowTextT != null ? arrowTextT.GetComponent<Text>() : null;
            while (arrow != null)
            {
                phase += Time.deltaTime * 3f;
                rect.anchoredPosition = basePos + Vector2.up * (4f * Mathf.Sin(phase));
                if (text != null)
                {
                    float alpha = 0.7f + 0.3f * Mathf.Sin(phase * 1.5f);
                    text.color = new Color(1f, 0.65f, 0.10f, alpha);
                }
                yield return null;
            }
        }

        // ====================================================================
        // P&C: Upgrade Progress Bar + Scaffolding
        // ====================================================================

        /// <summary>P&C: Green progress bar overlay + scaffolding on buildings being upgraded.</summary>
        private void RefreshUpgradeProgressBar(CityBuildingPlacement placement, BuildingManager bm)
        {
            if (placement.VisualGO == null) return;

            // Find the queue entry
            BuildQueueEntry entry = null;
            foreach (var qe in bm.BuildQueue)
            {
                if (qe.PlacedId == placement.InstanceId) { entry = qe; break; }
            }
            if (entry == null) return;

            // Calculate progress (0 to 1)
            float totalTime = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds + entry.RemainingSeconds;
            float progress = totalTime > 0 ? 1f - (entry.RemainingSeconds / totalTime) : 0f;
            progress = Mathf.Clamp01(progress);

            // Compute queue position
            int queuePos = 0;
            for (int i = 0; i < bm.BuildQueue.Count; i++)
            {
                if (bm.BuildQueue[i].PlacedId == placement.InstanceId) { queuePos = i + 1; break; }
            }

            // Create or update progress bar
            var existingBar = placement.VisualGO.transform.Find("UpgradeProgressBar");
            if (existingBar == null)
            {
                CreateUpgradeProgressBar(placement.VisualGO, progress, placement.InstanceId, entry.RemainingSeconds);
                AddScaffoldingOverlay(placement.VisualGO);
                AddConstructionDustEffect(placement.VisualGO);
                if (bm.BuildQueue.Count > 1)
                    CreateQueuePositionLabel(placement.VisualGO, queuePos, bm.BuildQueue.Count);
            }
            else
            {
                // Update fill amount
                var fill = existingBar.Find("Fill");
                if (fill != null)
                {
                    var fillRect = fill.GetComponent<RectTransform>();
                    if (fillRect != null)
                        fillRect.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, progress), 0.80f);
                }
                var pctText = existingBar.Find("PctText");
                if (pctText != null)
                {
                    var text = pctText.GetComponent<Text>();
                    if (text != null)
                        text.text = $"{Mathf.RoundToInt(progress * 100)}%";
                }
                // Update queue position label
                var queueLabel = placement.VisualGO.transform.Find("QueuePosLabel");
                if (bm.BuildQueue.Count > 1)
                {
                    if (queueLabel == null)
                        CreateQueuePositionLabel(placement.VisualGO, queuePos, bm.BuildQueue.Count);
                    else
                    {
                        var qlText = queueLabel.GetComponentInChildren<Text>();
                        if (qlText != null) qlText.text = $"#{queuePos}/{bm.BuildQueue.Count}";
                    }
                }
                else if (queueLabel != null)
                    Destroy(queueLabel.gameObject);

                // Update speed-up button time label
                var speedBtn = existingBar.Find("SpeedUpBtn");
                if (speedBtn != null)
                {
                    var btnLabel = speedBtn.GetComponentInChildren<Text>();
                    if (btnLabel != null)
                    {
                        int secs = Mathf.RoundToInt(entry.RemainingSeconds);
                        btnLabel.text = secs <= FreeSpeedUpThresholdSeconds
                            ? $"\u26A1 FREE"
                            : $"\u26A1 {FormatTimeRemaining(secs)}";
                    }
                }
            }
        }

        private void CreateUpgradeProgressBar(GameObject building, float progress, string instanceId, float remainingSeconds)
        {
            var bar = new GameObject("UpgradeProgressBar");
            bar.transform.SetParent(building.transform, false);
            var barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.05f, 0.42f);
            barRect.anchorMax = new Vector2(0.95f, 0.52f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;

            // Dark track background
            var trackBg = bar.AddComponent<Image>();
            trackBg.color = new Color(0.05f, 0.05f, 0.05f, 0.80f);
            trackBg.raycastTarget = false;

            var trackOutline = bar.AddComponent<Outline>();
            trackOutline.effectColor = new Color(0.4f, 0.35f, 0.2f, 0.6f);
            trackOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Green fill
            var fill = new GameObject("Fill");
            fill.transform.SetParent(bar.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.04f, 0.20f);
            fillRect.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, progress), 0.80f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.85f, 0.35f, 0.90f);
            fillImg.raycastTarget = false;

            // Percentage text
            var pctGO = new GameObject("PctText");
            pctGO.transform.SetParent(bar.transform, false);
            var pctRect = pctGO.AddComponent<RectTransform>();
            pctRect.anchorMin = Vector2.zero;
            pctRect.anchorMax = Vector2.one;
            pctRect.offsetMin = Vector2.zero;
            pctRect.offsetMax = Vector2.zero;
            var pctText = pctGO.AddComponent<Text>();
            pctText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            pctText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pctText.fontSize = 7;
            pctText.fontStyle = FontStyle.Bold;
            pctText.alignment = TextAnchor.MiddleCenter;
            pctText.color = Color.white;
            pctText.raycastTarget = false;
            var pctShadow = pctGO.AddComponent<Shadow>();
            pctShadow.effectColor = new Color(0, 0, 0, 0.9f);
            pctShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // P&C: Speed-up button below the progress bar
            var speedBtn = new GameObject("SpeedUpBtn");
            speedBtn.transform.SetParent(bar.transform, false);
            var speedRect = speedBtn.AddComponent<RectTransform>();
            speedRect.anchorMin = new Vector2(0.15f, -2.8f);
            speedRect.anchorMax = new Vector2(0.85f, -0.4f);
            speedRect.offsetMin = Vector2.zero;
            speedRect.offsetMax = Vector2.zero;

            var speedBg = speedBtn.AddComponent<Image>();
            int secs = Mathf.RoundToInt(remainingSeconds);
            bool isFree = secs <= FreeSpeedUpThresholdSeconds;
            speedBg.color = isFree
                ? new Color(0.15f, 0.65f, 0.30f, 0.92f)
                : new Color(0.55f, 0.30f, 0.70f, 0.90f);
            speedBg.raycastTarget = true;

            var speedOutline = speedBtn.AddComponent<Outline>();
            speedOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.6f);
            speedOutline.effectDistance = new Vector2(0.6f, -0.6f);

            var btn = speedBtn.AddComponent<Button>();
            btn.targetGraphic = speedBg;
            string capturedId = instanceId;
            float capturedSecs = remainingSeconds;
            btn.onClick.AddListener(() =>
            {
                if (capturedSecs <= FreeSpeedUpThresholdSeconds)
                {
                    EventBus.Publish(new SpeedupRequestedEvent(capturedId, 0, capturedSecs));
                    return;
                }
                int gemCost = Mathf.Max(1, Mathf.CeilToInt(capturedSecs / 60f));
                ShowSpeedUpDialog(capturedId, gemCost, Mathf.RoundToInt(capturedSecs));
            });

            var speedTextGO = new GameObject("Label");
            speedTextGO.transform.SetParent(speedBtn.transform, false);
            var speedTextRect = speedTextGO.AddComponent<RectTransform>();
            speedTextRect.anchorMin = Vector2.zero;
            speedTextRect.anchorMax = Vector2.one;
            speedTextRect.offsetMin = Vector2.zero;
            speedTextRect.offsetMax = Vector2.zero;
            var speedText = speedTextGO.AddComponent<Text>();
            speedText.text = isFree ? "\u26A1 FREE" : $"\u26A1 {FormatTimeRemaining(secs)}";
            speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            speedText.fontSize = 7;
            speedText.fontStyle = FontStyle.Bold;
            speedText.alignment = TextAnchor.MiddleCenter;
            speedText.color = Color.white;
            speedText.raycastTarget = false;
            var speedShadow = speedTextGO.AddComponent<Shadow>();
            speedShadow.effectColor = new Color(0, 0, 0, 0.8f);
            speedShadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        /// <summary>P&C: Queue position label above building showing "#1/2".</summary>
        private void CreateQueuePositionLabel(GameObject building, int position, int total)
        {
            var label = new GameObject("QueuePosLabel");
            label.transform.SetParent(building.transform, false);
            var rect = label.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.30f, 0.92f);
            rect.anchorMax = new Vector2(0.70f, 1.02f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = label.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.20f, 0.85f);
            bg.raycastTarget = false;

            var outline = label.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.6f, 0.5f);
            outline.effectDistance = new Vector2(0.4f, -0.4f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(label.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = $"#{position}/{total}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.75f, 0.80f, 0.95f);
            text.raycastTarget = false;
        }

        // ====================================================================
        // P&C: Resource Near-Cap Warning
        // ====================================================================

        private static readonly Dictionary<string, ResourceType> ResourceBuildingToType = new()
        {
            { "grain_farm", ResourceType.Grain },
            { "iron_mine", ResourceType.Iron },
            { "stone_quarry", ResourceType.Stone },
            { "arcane_tower", ResourceType.ArcaneEssence },
        };

        /// <summary>P&C: Show/hide warning badge on resource buildings when storage is near cap (>90%).</summary>
        private void RefreshResourceCapWarnings()
        {
            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) return;

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                if (!ResourceBuildingToType.TryGetValue(p.BuildingId, out var resType)) continue;

                bool nearCap = IsNearResourceCap(rm, resType);
                var existing = p.VisualGO.transform.Find("CapWarning");

                if (nearCap && existing == null)
                    CreateCapWarningBadge(p.VisualGO);
                else if (!nearCap && existing != null)
                    Destroy(existing.gameObject);
            }
        }

        private static bool IsNearResourceCap(ResourceManager rm, ResourceType type)
        {
            float ratio = type switch
            {
                ResourceType.Stone => rm.MaxStone > 0 ? (float)rm.Stone / rm.MaxStone : 0f,
                ResourceType.Iron => rm.MaxIron > 0 ? (float)rm.Iron / rm.MaxIron : 0f,
                ResourceType.Grain => rm.MaxGrain > 0 ? (float)rm.Grain / rm.MaxGrain : 0f,
                ResourceType.ArcaneEssence => rm.MaxArcaneEssence > 0 ? (float)rm.ArcaneEssence / rm.MaxArcaneEssence : 0f,
                _ => 0f
            };
            return ratio >= 0.90f;
        }

        private void CreateCapWarningBadge(GameObject building)
        {
            var badge = new GameObject("CapWarning");
            badge.transform.SetParent(building.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.70f, 0.82f);
            rect.anchorMax = new Vector2(0.98f, 1.0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.85f, 0.25f, 0.15f, 0.90f);
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.5f, 0.3f, 0.6f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = "FULL";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        // ====================================================================
        // P&C: Stronghold recommended upgrade banner
        // ====================================================================

        /// <summary>
        /// P&C: Shows a prominent "RECOMMENDED UPGRADE" banner on the stronghold
        /// when no upgrades are active and builders are free — nudges progression.
        /// </summary>
        private void RefreshStrongholdUpgradeBanner()
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;

            // Find stronghold placement
            CityBuildingPlacement stronghold = null;
            foreach (var p in _placements)
            {
                if (p.BuildingId == "stronghold") { stronghold = p; break; }
            }
            if (stronghold == null || stronghold.VisualGO == null)
            {
                DestroyStrongholdUpgradeBanner();
                return;
            }

            // Show banner only when: no active upgrades & stronghold not at max tier
            bool hasActiveUpgrade = bm.BuildQueue.Count > 0;
            bool isMaxTier = stronghold.Tier >= 3; // tier 0,1,2 = t1,t2,t3
            bool shouldShow = !hasActiveUpgrade && !isMaxTier;

            if (!shouldShow)
            {
                DestroyStrongholdUpgradeBanner();
                return;
            }

            if (_strongholdUpgradeBanner != null) return; // already showing

            // Create banner above stronghold
            var banner = new GameObject("StrongholdUpgradeBanner");
            banner.transform.SetParent(stronghold.VisualGO.transform, false);
            var rect = banner.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, 0.88f);
            rect.anchorMax = new Vector2(1.15f, 1.08f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark bg with gold border
            var bg = banner.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.06f, 0.18f, 0.92f);
            bg.raycastTarget = true;

            var outline = banner.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            // Tap to upgrade
            var btn = banner.AddComponent<Button>();
            btn.targetGraphic = bg;
            string capturedInstanceId = stronghold.InstanceId;
            btn.onClick.AddListener(() =>
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var mgr))
                    mgr.StartUpgrade(capturedInstanceId);
            });

            // Arrow icon + text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(banner.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);
            var text = textGO.AddComponent<Text>();
            text.text = $"\u2B06 UPGRADE STRONGHOLD";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.35f);
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.6f, -0.6f);

            _strongholdUpgradeBanner = banner;
            StartCoroutine(PulseStrongholdBanner());
        }

        private IEnumerator PulseStrongholdBanner()
        {
            while (_strongholdUpgradeBanner != null)
            {
                var img = _strongholdUpgradeBanner.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                    img.color = new Color(0.10f, 0.06f * pulse, 0.18f * pulse, 0.92f);
                }
                yield return null;
            }
        }

        private void DestroyStrongholdUpgradeBanner()
        {
            if (_strongholdUpgradeBanner != null)
            {
                Destroy(_strongholdUpgradeBanner);
                _strongholdUpgradeBanner = null;
            }
        }

        // ====================================================================
        // P&C: Builder count HUD
        // ====================================================================

        /// <summary>P&C: Create "Builder 1/2" HUD element near top of screen.</summary>
        private void CreateBuilderCountHUD()
        {
            if (_builderCountHUD != null) return;

            // Find root canvas
            Transform canvasRoot = transform;
            while (canvasRoot.parent != null && canvasRoot.parent.GetComponent<Canvas>() != null)
                canvasRoot = canvasRoot.parent;

            var hud = new GameObject("BuilderCountHUD");
            hud.transform.SetParent(canvasRoot, false);
            hud.transform.SetAsLastSibling();

            var hudRect = hud.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.02f, 0.88f);
            hudRect.anchorMax = new Vector2(0.18f, 0.93f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            var hudBg = hud.AddComponent<Image>();
            hudBg.color = new Color(0.06f, 0.04f, 0.10f, 0.85f);
            hudBg.raycastTarget = true;

            // P&C: Tap to expand queue detail panel
            var hudBtn = hud.AddComponent<Button>();
            hudBtn.targetGraphic = hudBg;
            hudBtn.onClick.AddListener(ToggleBuildQueuePanel);

            var hudOutline = hud.AddComponent<Outline>();
            hudOutline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.5f);
            hudOutline.effectDistance = new Vector2(0.8f, -0.8f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(hud.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);

            _builderCountText = textGO.AddComponent<Text>();
            _builderCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _builderCountText.fontSize = 10;
            _builderCountText.fontStyle = FontStyle.Bold;
            _builderCountText.alignment = TextAnchor.MiddleCenter;
            _builderCountText.color = new Color(0.95f, 0.85f, 0.45f);
            _builderCountText.raycastTarget = false;
            _builderCountText.text = "\u2692 Builder 0/2";

            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.8f, -0.8f);

            _builderCountHUD = hud;
            UpdateBuilderCountHUD();

            // P&C: Power rating HUD below builder count
            CreatePowerRatingHUD(canvasRoot);
        }

        private GameObject _powerRatingHUD;
        private Text _powerRatingText;

        private void CreatePowerRatingHUD(Transform canvasRoot)
        {
            if (_powerRatingHUD != null) return;

            var hud = new GameObject("PowerRatingHUD");
            hud.transform.SetParent(canvasRoot, false);
            hud.transform.SetAsLastSibling();

            var hudRect = hud.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.02f, 0.83f);
            hudRect.anchorMax = new Vector2(0.18f, 0.87f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            var hudBg = hud.AddComponent<Image>();
            hudBg.color = new Color(0.06f, 0.04f, 0.10f, 0.80f);
            hudBg.raycastTarget = false;
            var hudOutline = hud.AddComponent<Outline>();
            hudOutline.effectColor = new Color(0.70f, 0.45f, 0.15f, 0.4f);
            hudOutline.effectDistance = new Vector2(0.6f, -0.6f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(hud.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-4, 0);

            _powerRatingText = textGO.AddComponent<Text>();
            _powerRatingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _powerRatingText.fontSize = 9;
            _powerRatingText.fontStyle = FontStyle.Bold;
            _powerRatingText.alignment = TextAnchor.MiddleCenter;
            _powerRatingText.color = new Color(0.95f, 0.70f, 0.35f);
            _powerRatingText.raycastTarget = false;
            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.6f, -0.6f);

            _powerRatingHUD = hud;
            UpdatePowerRatingHUD();
        }

        private void UpdatePowerRatingHUD()
        {
            if (_powerRatingText == null) return;

            int totalPower = 0;
            foreach (var p in _placements)
                totalPower += GetBuildingPowerContribution(p.BuildingId, p.Tier);

            string powerStr = totalPower >= 1_000_000 ? $"{totalPower / 1_000_000f:F1}M"
                : totalPower >= 1_000 ? $"{totalPower / 1_000f:F1}K"
                : $"{totalPower}";
            _powerRatingText.text = $"\u2694 {powerStr}";
        }

        private void UpdateBuilderCountHUD()
        {
            if (_builderCountText == null) return;
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;

            int used = bm.BuildQueue.Count;
            int total = 2; // Free queue slots (from EmpireConfig)
            _builderCountText.text = $"\u2692 Builder {used}/{total}";
            _builderCountText.color = used >= total
                ? new Color(1f, 0.45f, 0.35f) // Red when full
                : new Color(0.95f, 0.85f, 0.45f); // Gold when available
        }

        // ====================================================================
        // P&C: Build Queue Expandable Panel
        // ====================================================================

        private GameObject _buildQueuePanel;

        private void ToggleBuildQueuePanel()
        {
            if (_buildQueuePanel != null)
            {
                Destroy(_buildQueuePanel);
                _buildQueuePanel = null;
                return;
            }
            ShowBuildQueuePanel();
        }

        private void ShowBuildQueuePanel()
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;

            Transform canvasRoot = transform;
            while (canvasRoot.parent != null && canvasRoot.parent.GetComponent<Canvas>() != null)
                canvasRoot = canvasRoot.parent;

            _buildQueuePanel = new GameObject("BuildQueuePanel");
            _buildQueuePanel.transform.SetParent(canvasRoot, false);
            _buildQueuePanel.transform.SetAsLastSibling();

            var panelRect = _buildQueuePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.02f, 0.58f);
            panelRect.anchorMax = new Vector2(0.48f, 0.87f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelBg = _buildQueuePanel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.04f, 0.12f, 0.94f);
            panelBg.raycastTarget = true;
            var panelOutline = _buildQueuePanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title bar
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(_buildQueuePanel.transform, false);
            var tbRect = titleBar.AddComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 0.88f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;
            var tbBg = titleBar.AddComponent<Image>();
            tbBg.color = new Color(0.12f, 0.08f, 0.20f, 0.95f);
            tbBg.raycastTarget = false;

            AddInfoPanelText(titleBar.transform, "QTitle", "\u2692 BUILD QUEUE", 11, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0f), new Vector2(0.70f, 1f), TextAnchor.MiddleLeft);

            // Slot count
            int totalSlots = 2;
            AddInfoPanelText(titleBar.transform, "SlotCount", $"{bm.BuildQueue.Count}/{totalSlots}", 10, FontStyle.Bold,
                new Color(0.70f, 0.90f, 0.70f),
                new Vector2(0.75f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleRight);

            var queue = bm.BuildQueue;
            if (queue.Count == 0)
            {
                // Empty state with slot indicators
                for (int s = 0; s < totalSlots; s++)
                {
                    float yTop = 0.82f - s * 0.42f;
                    float yBot = yTop - 0.38f;
                    CreateEmptyQueueSlot(_buildQueuePanel.transform, s + 1, yBot, yTop);
                }
            }
            else
            {
                for (int i = 0; i < totalSlots; i++)
                {
                    float yTop = 0.82f - i * 0.42f;
                    float yBot = yTop - 0.38f;

                    if (i < queue.Count)
                    {
                        var entry = queue[i];
                        CreateFilledQueueSlot(_buildQueuePanel.transform, entry, i, yBot, yTop);
                    }
                    else
                    {
                        CreateEmptyQueueSlot(_buildQueuePanel.transform, i + 1, yBot, yTop);
                    }
                }
            }

            // Auto-dismiss after 8 seconds (longer for richer panel)
            StartCoroutine(AutoDismissQueuePanel());
        }

        private void CreateEmptyQueueSlot(Transform parent, int slotNum, float yBot, float yTop)
        {
            var slot = new GameObject($"EmptySlot{slotNum}");
            slot.transform.SetParent(parent, false);
            var slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.03f, yBot);
            slotRect.anchorMax = new Vector2(0.97f, yTop);
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;

            var slotBg = slot.AddComponent<Image>();
            slotBg.color = new Color(0.10f, 0.08f, 0.16f, 0.6f);
            slotBg.raycastTarget = false;

            // Dashed border effect
            var border = slot.AddComponent<Outline>();
            border.effectColor = new Color(0.4f, 0.35f, 0.5f, 0.4f);
            border.effectDistance = new Vector2(1f, -1f);

            AddInfoPanelText(slot.transform, "Empty", $"Slot {slotNum} — Empty", 10, FontStyle.Normal,
                new Color(0.5f, 0.5f, 0.55f),
                new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleCenter);
        }

        private void CreateFilledQueueSlot(Transform parent, BuildQueueEntry entry, int slotIndex, float yBot, float yTop)
        {
            var slot = new GameObject($"Slot{slotIndex}");
            slot.transform.SetParent(parent, false);
            var slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.03f, yBot);
            slotRect.anchorMax = new Vector2(0.97f, yTop);
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;

            // Slot background — active slot glows slightly
            var slotBg = slot.AddComponent<Image>();
            slotBg.color = slotIndex == 0
                ? new Color(0.12f, 0.10f, 0.22f, 0.85f)
                : new Color(0.10f, 0.08f, 0.18f, 0.75f);
            slotBg.raycastTarget = false;
            var slotBorder = slot.AddComponent<Outline>();
            slotBorder.effectColor = slotIndex == 0
                ? new Color(0.85f, 0.65f, 0.20f, 0.6f)
                : new Color(0.5f, 0.4f, 0.6f, 0.4f);
            slotBorder.effectDistance = new Vector2(1f, -1f);

            // Find placement info
            string buildingId = "";
            string displayName = entry.PlacedId;
            int currentTier = entry.TargetTier - 1;
            foreach (var p in _placements)
            {
                if (p.InstanceId == entry.PlacedId)
                {
                    buildingId = p.BuildingId;
                    displayName = BuildingDisplayNames.TryGetValue(p.BuildingId, out var dn) ? dn : p.BuildingId;
                    currentTier = p.Tier;
                    break;
                }
            }

            // Building sprite thumbnail (left side)
            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(slot.transform, false);
            var thumbRect = thumbGO.AddComponent<RectTransform>();
            thumbRect.anchorMin = new Vector2(0.02f, 0.10f);
            thumbRect.anchorMax = new Vector2(0.22f, 0.90f);
            thumbRect.offsetMin = Vector2.zero;
            thumbRect.offsetMax = Vector2.zero;
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.color = Color.white;
            thumbImg.raycastTarget = false;
            thumbImg.preserveAspect = true;
            var sprite = LoadBuildingSprite(buildingId, entry.TargetTier);
            if (sprite == null) sprite = LoadBuildingSprite(buildingId, currentTier);
            if (sprite != null) thumbImg.sprite = sprite;
            else thumbImg.color = new Color(0.3f, 0.25f, 0.4f, 0.5f);

            // Building name + tier
            AddInfoPanelText(slot.transform, "Name", $"{displayName}", 10, FontStyle.Bold,
                Color.white,
                new Vector2(0.25f, 0.60f), new Vector2(0.75f, 0.95f), TextAnchor.MiddleLeft);

            AddInfoPanelText(slot.transform, "Tier", $"Lv {currentTier} \u2192 {entry.TargetTier}", 9, FontStyle.Normal,
                new Color(0.80f, 0.75f, 0.55f),
                new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.62f), TextAnchor.MiddleLeft);

            // Progress bar
            float totalTime = (float)(System.DateTime.Now - entry.StartTime).TotalSeconds + entry.RemainingSeconds;
            float elapsed = totalTime - entry.RemainingSeconds;
            float progress = totalTime > 0 ? Mathf.Clamp01(elapsed / totalTime) : 0f;

            var barBg = new GameObject("BarBg");
            barBg.transform.SetParent(slot.transform, false);
            var barBgRect = barBg.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.25f, 0.12f);
            barBgRect.anchorMax = new Vector2(0.75f, 0.28f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.12f, 0.20f);
            barBgImg.raycastTarget = false;

            var barFill = new GameObject("BarFill");
            barFill.transform.SetParent(barBg.transform, false);
            var fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(progress, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = barFill.AddComponent<Image>();
            fillImg.color = new Color(0.20f, 0.75f, 0.35f);
            fillImg.raycastTarget = false;

            // Time remaining text
            string timeStr = FormatTimeRemaining(Mathf.RoundToInt(entry.RemainingSeconds));
            AddInfoPanelText(slot.transform, "Time", timeStr, 9, FontStyle.Bold,
                new Color(0.90f, 0.85f, 0.65f),
                new Vector2(0.25f, 0.00f), new Vector2(0.75f, 0.14f), TextAnchor.MiddleLeft);

            // Right side buttons
            bool canFreeSpeedup = entry.RemainingSeconds <= FreeSpeedUpThresholdSeconds;

            // Speed Up / Free button
            var btnGO = new GameObject("SpeedUpBtn");
            btnGO.transform.SetParent(slot.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.76f, 0.45f);
            btnRect.anchorMax = new Vector2(0.98f, 0.95f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            var btnBg = btnGO.AddComponent<Image>();
            btnBg.color = canFreeSpeedup
                ? new Color(0.15f, 0.70f, 0.30f, 0.9f)
                : new Color(0.55f, 0.25f, 0.70f, 0.9f);
            btnBg.raycastTarget = true;
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            string capPlacedId = entry.PlacedId;
            btn.onClick.AddListener(() =>
            {
                if (canFreeSpeedup)
                    ShowUpgradeBlockedToast("Free speed-up applied!");
                else
                    ShowUpgradeBlockedToast("Gems required for speed-up");
            });

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGO.transform, false);
            var blRect = btnLabel.AddComponent<RectTransform>();
            blRect.anchorMin = Vector2.zero;
            blRect.anchorMax = Vector2.one;
            blRect.offsetMin = Vector2.zero;
            blRect.offsetMax = Vector2.zero;
            var blText = btnLabel.AddComponent<Text>();
            blText.text = canFreeSpeedup ? "FREE" : "\u26a1";
            blText.fontSize = canFreeSpeedup ? 9 : 12;
            blText.fontStyle = FontStyle.Bold;
            blText.alignment = TextAnchor.MiddleCenter;
            blText.color = Color.white;
            blText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            blText.raycastTarget = false;

            // Help button (alliance help)
            var helpGO = new GameObject("HelpBtn");
            helpGO.transform.SetParent(slot.transform, false);
            var helpRect = helpGO.AddComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0.76f, 0.05f);
            helpRect.anchorMax = new Vector2(0.98f, 0.42f);
            helpRect.offsetMin = Vector2.zero;
            helpRect.offsetMax = Vector2.zero;
            var helpBg = helpGO.AddComponent<Image>();
            helpBg.color = new Color(0.20f, 0.45f, 0.75f, 0.9f);
            helpBg.raycastTarget = true;
            var helpBtn = helpGO.AddComponent<Button>();
            helpBtn.targetGraphic = helpBg;
            helpBtn.onClick.AddListener(() =>
            {
                EventBus.Publish(new AllianceHelpRequestedEvent(capPlacedId));
                ShowUpgradeBlockedToast("Help requested from alliance!");
            });

            var helpLabel = new GameObject("Label");
            helpLabel.transform.SetParent(helpGO.transform, false);
            var hlRect = helpLabel.AddComponent<RectTransform>();
            hlRect.anchorMin = Vector2.zero;
            hlRect.anchorMax = Vector2.one;
            hlRect.offsetMin = Vector2.zero;
            hlRect.offsetMax = Vector2.zero;
            var hlText = helpLabel.AddComponent<Text>();
            hlText.text = "Help";
            hlText.fontSize = 9;
            hlText.fontStyle = FontStyle.Bold;
            hlText.alignment = TextAnchor.MiddleCenter;
            hlText.color = Color.white;
            hlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hlText.raycastTarget = false;
        }

        private IEnumerator AutoDismissQueuePanel()
        {
            yield return new WaitForSeconds(8f);
            if (_buildQueuePanel != null)
            {
                Destroy(_buildQueuePanel);
                _buildQueuePanel = null;
            }
        }

        // ====================================================================
        // P&C: Construction scaffolding overlay
        // ====================================================================

        /// <summary>P&C: Add scaffolding/construction lines overlay on a building during upgrade.</summary>
        private void AddScaffoldingOverlay(GameObject building)
        {
            if (building == null) return;
            RemoveScaffoldingOverlay(building);

            var scaffold = new GameObject("Scaffolding");
            scaffold.transform.SetParent(building.transform, false);

            var rect = scaffold.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.05f);
            rect.anchorMax = new Vector2(0.95f, 0.90f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Semi-transparent construction overlay
            var img = scaffold.AddComponent<Image>();
            img.color = new Color(0.40f, 0.30f, 0.15f, 0.25f);
            img.raycastTarget = false;

            // Diagonal construction lines (3 lines)
            for (int i = 0; i < 3; i++)
            {
                var lineGO = new GameObject($"Line_{i}");
                lineGO.transform.SetParent(scaffold.transform, false);
                var lineRect = lineGO.AddComponent<RectTransform>();
                float xOffset = 0.15f + i * 0.25f;
                lineRect.anchorMin = new Vector2(xOffset, 0f);
                lineRect.anchorMax = new Vector2(xOffset + 0.04f, 1f);
                lineRect.offsetMin = Vector2.zero;
                lineRect.offsetMax = Vector2.zero;
                lineRect.localRotation = Quaternion.Euler(0, 0, 15f);

                var lineImg = lineGO.AddComponent<Image>();
                lineImg.color = new Color(0.65f, 0.50f, 0.20f, 0.35f);
                lineImg.raycastTarget = false;
            }

            // Animated shimmer coroutine
            StartCoroutine(AnimateScaffolding(scaffold));
        }

        private IEnumerator AnimateScaffolding(GameObject scaffold)
        {
            if (scaffold == null) yield break;
            var cg = scaffold.AddComponent<CanvasGroup>();
            float phase = 0f;
            while (scaffold != null)
            {
                phase += Time.deltaTime * 1.5f;
                cg.alpha = 0.6f + 0.2f * Mathf.Sin(phase);
                yield return null;
            }
        }

        private void RemoveScaffoldingOverlay(GameObject building)
        {
            if (building == null) return;
            var existing = building.transform.Find("Scaffolding");
            if (existing != null) Destroy(existing.gameObject);
        }

        // ====================================================================
        // P&C: Construction dust particle effect on upgrading buildings
        // ====================================================================

        /// <summary>P&C: Spawn rising dust/spark motes around a building under construction.</summary>
        private void AddConstructionDustEffect(GameObject building)
        {
            if (building == null) return;
            RemoveConstructionDustEffect(building);

            var dustRoot = new GameObject("ConstructionDust");
            dustRoot.transform.SetParent(building.transform, false);

            var rootRect = dustRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0.85f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Spawn 6 dust motes that loop
            for (int i = 0; i < 6; i++)
            {
                var mote = new GameObject($"Mote_{i}");
                mote.transform.SetParent(dustRoot.transform, false);

                var moteRect = mote.AddComponent<RectTransform>();
                moteRect.sizeDelta = new Vector2(4f, 4f);
                // Random start position along base
                float xAnchor = 0.1f + (i * 0.15f);
                moteRect.anchorMin = new Vector2(xAnchor, 0f);
                moteRect.anchorMax = new Vector2(xAnchor, 0f);
                moteRect.anchoredPosition = Vector2.zero;

                var img = mote.AddComponent<Image>();
                // Alternate between dust (tan) and spark (orange) colors
                bool isSpark = i % 3 == 0;
                img.color = isSpark
                    ? new Color(1f, 0.7f, 0.2f, 0.7f)   // orange spark
                    : new Color(0.75f, 0.65f, 0.45f, 0.5f); // tan dust
                img.raycastTarget = false;

                // Try to use radial gradient for softer look
                var gradientSprite = Resources.Load<Sprite>("UI/Production/radial_gradient");
                if (gradientSprite != null) img.sprite = gradientSprite;
            }

            StartCoroutine(AnimateConstructionDust(dustRoot));
        }

        private IEnumerator AnimateConstructionDust(GameObject dustRoot)
        {
            if (dustRoot == null) yield break;

            var motes = new List<RectTransform>();
            var images = new List<Image>();
            var phases = new List<float>();
            var speeds = new List<float>();
            var driftX = new List<float>();

            for (int i = 0; i < dustRoot.transform.childCount; i++)
            {
                var child = dustRoot.transform.GetChild(i);
                var rt = child.GetComponent<RectTransform>();
                var img = child.GetComponent<Image>();
                if (rt != null && img != null)
                {
                    motes.Add(rt);
                    images.Add(img);
                    phases.Add(i * 1.1f); // stagger start
                    speeds.Add(18f + i * 4f); // different rise speeds
                    driftX.Add((i % 2 == 0 ? 1f : -1f) * (3f + i * 1.5f)); // lateral drift
                }
            }

            while (dustRoot != null)
            {
                for (int i = 0; i < motes.Count; i++)
                {
                    if (motes[i] == null) continue;

                    phases[i] += Time.deltaTime;
                    float cycle = phases[i] % 3f; // 3 second cycle
                    float t = cycle / 3f; // 0..1

                    // Rise from bottom
                    float y = t * 50f;
                    float x = Mathf.Sin(phases[i] * 2f) * driftX[i];
                    motes[i].anchoredPosition = new Vector2(x, y);

                    // Fade: appear at 0, full at 0.2, fade out at 0.7-1.0
                    float alpha;
                    if (t < 0.15f) alpha = t / 0.15f;
                    else if (t < 0.6f) alpha = 1f;
                    else alpha = 1f - (t - 0.6f) / 0.4f;

                    var c = images[i].color;
                    float baseAlpha = (i % 3 == 0) ? 0.7f : 0.5f;
                    images[i].color = new Color(c.r, c.g, c.b, alpha * baseAlpha);

                    // Scale: small→big→small
                    float scale = 0.5f + 0.8f * Mathf.Sin(t * Mathf.PI);
                    motes[i].localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }

        private void RemoveConstructionDustEffect(GameObject building)
        {
            if (building == null) return;
            var existing = building.transform.Find("ConstructionDust");
            if (existing != null) Destroy(existing.gameObject);
        }

        /// <summary>P&C: Remove a building visual from the grid when demolished.</summary>
        private void OnBuildingDemolished(BuildingDemolishedEvent evt)
        {
            ClearBuildingFootprint();
            for (int i = _placements.Count - 1; i >= 0; i--)
            {
                if (_placements[i].InstanceId == evt.PlacedId)
                {
                    var p = _placements[i];
                    ClearCells(p.GridOrigin, p.Size);
                    if (p.VisualGO != null) Destroy(p.VisualGO);
                    _placements.RemoveAt(i);
                    Debug.Log($"[CityGridView] Demolished visual for {evt.BuildingId} ({evt.PlacedId}).");
                    break;
                }
            }
        }

        private static Text FindLevelBadgeText(Transform parent)
        {
            var badge = parent.Find("LevelBadge");
            if (badge == null) return null;
            var lvlText = badge.Find("LvlText");
            return lvlText != null ? lvlText.GetComponent<Text>() : null;
        }

        private void UpdateProductionLabel(GameObject building, string buildingId, int tier)
        {
            if (!ResourceBuildingTypes.TryGetValue(buildingId, out var resInfo)) return;

            var existing = building.transform.Find("ProductionRate");
            if (existing != null)
            {
                var rateText = existing.GetComponentInChildren<Text>();
                if (rateText != null)
                {
                    int rate = (tier + 1) * 250;
                    rateText.text = rate >= 1000 ? $"+{rate / 1000f:F1}K/hr" : $"+{rate}/hr";
                    rateText.color = resInfo.Tint;
                }
            }
        }

        // ====================================================================
        // Long press / move mode
        // ====================================================================

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_moveMode) return;
            // Don't start long-press if second finger is already down (pinch)
            if (Input.touchCount >= 2) return;
            _holdStarted = true;
            _holdTimer = 0f;
            _holdStartPos = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasTap = _holdStarted && !_moveMode && !_isPinching
                          && _holdTimer < LongPressTime
                          && Vector2.Distance(eventData.position, _holdStartPos) < DragThreshold;

            _holdStarted = false;
            _holdTimer = 0f;

            if (_moveMode && _movingBuilding != null)
            {
                ExitMoveMode(eventData);
                return;
            }

            if (wasTap)
                HandleBuildingTap(eventData);
        }

        private void HandleBuildingTap(PointerEventData eventData)
        {
            // P&C: If in placement mode, tap confirms or repositions
            if (_placementMode && buildingContainer != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    buildingContainer, eventData.position, eventData.pressEventCamera, out var localPt);
                var gridPos = SnapToGrid(localPt, _placementSize);
                if (CanPlaceAt(gridPos, _placementSize, null))
                {
                    _placementSnapOrigin = gridPos;
                    ExitPlacementMode(true); // Confirm placement
                }
                else
                {
                    UpdatePlacementPosition(gridPos); // Show invalid position
                }
                return;
            }

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            CityBuildingPlacement tapped = null;
            foreach (var result in results)
            {
                foreach (var placement in _placements)
                {
                    if (placement.VisualGO == result.gameObject ||
                        (placement.VisualGO != null && result.gameObject.transform.IsChildOf(placement.VisualGO.transform)))
                    {
                        tapped = placement;
                        break;
                    }
                }
                if (tapped != null) break;
            }

            if (tapped == null)
            {
                // Clear footprint when tapping empty ground
                ClearBuildingFootprint();

                // P&C: Double-tap empty ground → toggle zoom between close and overview
                if ((Time.time - _lastEmptyTapTime) < EmptyDoubleTapWindow)
                {
                    _lastEmptyTapTime = 0f;
                    float target = _currentZoom > 1.0f ? ZoomOverview : DoubleTapZoomTarget;
                    if (_smoothZoomCoroutine != null) StopCoroutine(_smoothZoomCoroutine);
                    _targetZoom = target;
                    _smoothZoomCoroutine = StartCoroutine(SmoothZoomToggle(target, eventData.position));
                    return;
                }
                _lastEmptyTapTime = Time.time;

                // P&C: Tap on empty ground → publish event for building placement
                if (buildingContainer != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        buildingContainer, eventData.position, eventData.pressEventCamera, out var localPt);
                    var gridPos = SnapToGrid(localPt, new Vector2Int(2, 2));
                    if (IsInsidePlayableArea(gridPos))
                        EventBus.Publish(new EmptyCellTappedEvent(gridPos));
                }
                return;
            }

            // P&C: Show footprint highlight on tapped building
            ShowBuildingFootprint(tapped);

            // P&C: Audio + haptic feedback on building tap
            PlaySfx(_sfxTap);
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            // P&C: Category-specific bounce animation + tap sparkles
            if (tapped.VisualGO != null)
            {
                StartCoroutine(BounceBuilding(tapped.VisualGO.transform, GetCategoryBounceScale(tapped.BuildingId)));
                SpawnTapSparkles(tapped.VisualGO);
                SpawnTapRipple(tapped.VisualGO);
            }

            // P&C: Double-tap quick-upgrade detection
            if (tapped.InstanceId == _lastTappedInstanceId && (Time.time - _lastTapTime) < DoubleTapWindow)
            {
                _lastTappedInstanceId = null;
                EventBus.Publish(new BuildingDoubleTappedEvent(tapped.InstanceId, tapped.BuildingId, tapped.Tier));
                return;
            }
            _lastTappedInstanceId = tapped.InstanceId;
            _lastTapTime = Time.time;

            // P&C: Soft-center viewport on tapped building
            if (tapped.VisualGO != null)
                SoftCenterOnBuilding(tapped);

            // Publish tap event for UI systems
            EventBus.Publish(new BuildingTappedEvent(tapped));
        }

        private IEnumerator BounceBuilding(Transform building, float bounceScale = 0.08f)
        {
            var original = building.localScale;
            float duration = 0.2f;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration * 0.4f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.4f);
                building.localScale = original * (1f + bounceScale * t);
                yield return null;
            }

            // Bounce back with overshoot
            elapsed = 0f;
            while (elapsed < duration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.6f);
                float bounce = 1f + bounceScale * (1f - t) * Mathf.Cos(t * Mathf.PI);
                building.localScale = original * bounce;
                yield return null;
            }

            building.localScale = original;
        }

        /// <summary>P&C: Small sparkle particles burst on building tap for tactile feedback.</summary>
        private void SpawnTapSparkles(GameObject building)
        {
            var buildingRect = building.GetComponent<RectTransform>();
            if (buildingRect == null) return;
            var parent = buildingRect.parent as RectTransform;
            if (parent == null) return;

            Vector2 center = buildingRect.anchoredPosition + new Vector2(0, buildingRect.sizeDelta.y * 0.3f);
            int count = 5;
            for (int i = 0; i < count; i++)
            {
                var sparkle = new GameObject($"TapSparkle_{i}");
                sparkle.transform.SetParent(parent, false);
                var sRect = sparkle.AddComponent<RectTransform>();
                sRect.sizeDelta = new Vector2(4f, 4f);
                sRect.anchoredPosition = center;

                var sImg = sparkle.AddComponent<Image>();
                // Warm white/gold sparkle colors
                float hue = Random.Range(0.08f, 0.15f);
                sImg.color = Color.HSVToRGB(hue, Random.Range(0.2f, 0.6f), 1f);
                sImg.raycastTarget = false;

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(30f, 60f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StartCoroutine(AnimateTapSparkle(sparkle, sRect, sImg, dir, speed));
            }
        }

        private IEnumerator AnimateTapSparkle(GameObject go, RectTransform rect, Image img, Vector2 dir, float speed)
        {
            float duration = Random.Range(0.25f, 0.45f);
            float elapsed = 0f;
            Vector2 start = rect.anchoredPosition;

            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = start + dir * speed * t;
                rect.sizeDelta = Vector2.Lerp(new Vector2(4f, 4f), new Vector2(1f, 1f), t);
                img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - t * t);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        /// <summary>P&C: Drop/slam animation when building is placed — slight overshoot + squash + dust ring.</summary>
        private IEnumerator DropSlamAnimation(Transform building)
        {
            var original = building.localScale;
            float elapsed = 0f;
            float dropDur = 0.12f;

            // Phase 1: quick scale up (building "drops" from above)
            while (elapsed < dropDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dropDur;
                building.localScale = original * (1f + 0.12f * (1f - t * t)); // overshoot easing
                yield return null;
            }

            // Phase 2: squash on impact + bounce
            elapsed = 0f;
            float bounceDur = 0.18f;
            while (elapsed < bounceDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bounceDur;
                float sx = 1f + 0.06f * (1f - t) * Mathf.Cos(t * Mathf.PI * 2f);
                float sy = 1f - 0.04f * (1f - t) * Mathf.Cos(t * Mathf.PI * 2f);
                building.localScale = new Vector3(original.x * sx, original.y * sy, original.z);
                yield return null;
            }
            building.localScale = original;

            // Phase 3: dust ring expand + fade
            SpawnDustRing(building);
        }

        private void SpawnDustRing(Transform building)
        {
            if (buildingContainer == null) return;
            var dust = new GameObject("DustRing");
            dust.transform.SetParent(buildingContainer, false);
            var rect = dust.AddComponent<RectTransform>();
            var bRect = building.GetComponent<RectTransform>();
            if (bRect != null) rect.anchoredPosition = bRect.anchoredPosition;
            rect.sizeDelta = new Vector2(20, 10); // starts small
            rect.pivot = new Vector2(0.5f, 0.5f);
            var img = dust.AddComponent<Image>();
            img.color = new Color(0.75f, 0.65f, 0.50f, 0.5f);
            img.raycastTarget = false;
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) img.sprite = spr;
            StartCoroutine(ExpandDustRing(dust, rect, img));
        }

        private IEnumerator ExpandDustRing(GameObject dust, RectTransform rect, Image img)
        {
            float elapsed = 0f;
            float duration = 0.35f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.sizeDelta = new Vector2(20 + 80 * t, 10 + 40 * t);
                var c = img.color;
                c.a = 0.5f * (1f - t);
                img.color = c;
                yield return null;
            }
            Destroy(dust);
        }

        // Side length for iso diamond cell: square of this size rotated 45° = diamond CellSize wide
        private static readonly float IsoDiamondSide = CellSize / Mathf.Sqrt(2f); // ~45.25

        /// <summary>P&C: Show gold footprint highlight under a tapped building.</summary>
        public void ShowBuildingFootprint(CityBuildingPlacement placement)
        {
            if (placement == null) return;
            // Clear previous
            if (_footprintInstanceId == placement.InstanceId) return; // already shown
            ClearBuildingFootprint();

            _footprintInstanceId = placement.InstanceId;
            var container = buildingContainer != null ? buildingContainer : contentContainer;
            if (container == null) return;

            // Create an isometric diamond cell for each grid position in the building's footprint
            for (int gx = 0; gx < placement.Size.x; gx++)
            {
                for (int gy = 0; gy < placement.Size.y; gy++)
                {
                    var cellGO = CreateIsoDiamondCell(container,
                        placement.GridOrigin.x + gx, placement.GridOrigin.y + gy,
                        FootprintColor, FootprintBorder);
                    _footprintCells.Add(cellGO);
                }
            }

            // P&C: Glowing selection ring centered under building
            CreateSelectionRing(placement);

            // P&C: Golden outline on selected building sprite
            if (placement.VisualGO != null)
            {
                var existingOutline = placement.VisualGO.GetComponent<Outline>();
                if (existingOutline == null)
                {
                    var outline = placement.VisualGO.AddComponent<Outline>();
                    outline.effectColor = new Color(0.92f, 0.78f, 0.28f, 0.85f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
            }
        }

        /// <summary>
        /// Create a single isometric diamond cell at the given grid position.
        /// A square rotated 45° becomes a diamond. Scale Y by 0.5 for 2:1 iso ratio.
        /// Diamond width = CellSize (64), diamond height = CellSize/2 (32).
        /// </summary>
        private static GameObject CreateIsoDiamondCell(Transform parent, int gridX, int gridY,
            Color fillColor, Color borderColor)
        {
            // Cell center in local space (GridToLocalCenter with size 1x1 = center of that cell)
            var cellCenter = GridToLocalCenter(new Vector2Int(gridX, gridY), Vector2Int.one);

            var cellGO = new GameObject($"IsoCell_{gridX}_{gridY}");
            cellGO.transform.SetParent(parent, false);
            cellGO.transform.SetAsFirstSibling(); // behind buildings

            var rect = cellGO.AddComponent<RectTransform>();
            rect.anchoredPosition = cellCenter;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(IsoDiamondSide, IsoDiamondSide);
            rect.localRotation = Quaternion.Euler(0, 0, 45f);
            rect.localScale = new Vector3(1f, 0.5f, 1f);

            var img = cellGO.AddComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;

            var outline = cellGO.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            return cellGO;
        }

        private void CreateSelectionRing(CityBuildingPlacement placement)
        {
            if (_selectionRing != null) Destroy(_selectionRing);
            if (placement.VisualGO == null) return;

            var container = buildingContainer != null ? buildingContainer : contentContainer;
            if (container == null) return;

            _selectionRing = new GameObject("SelectionRing");
            _selectionRing.transform.SetParent(container, false);
            _selectionRing.transform.SetAsFirstSibling(); // Behind buildings

            var rect = _selectionRing.AddComponent<RectTransform>();
            // Center on building's anchor position
            var buildingRect = placement.VisualGO.GetComponent<RectTransform>();
            if (buildingRect != null)
                rect.anchoredPosition = buildingRect.anchoredPosition - new Vector2(0, buildingRect.sizeDelta.y * 0.15f);

            // Oval glow matching building footprint (P&C: generous size for visibility)
            float ringW = placement.Size.x * CellSize * 1.4f;
            float ringH = placement.Size.y * CellSize * 0.7f;
            rect.sizeDelta = new Vector2(ringW, ringH);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var img = _selectionRing.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0.90f, 0.75f, 0.25f, 0.45f); // Gold glow

            // Use radial gradient for soft edge
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) img.sprite = spr;
        }

        /// <summary>P&C: Clear the building footprint highlight.</summary>
        public void ClearBuildingFootprint()
        {
            foreach (var go in _footprintCells)
            {
                if (go != null) Destroy(go);
            }
            _footprintCells.Clear();
            // P&C: Remove golden outline from previously selected building
            foreach (var p in _placements)
            {
                if (p.VisualGO != null)
                {
                    var outline = p.VisualGO.GetComponent<Outline>();
                    if (outline != null) Destroy(outline);
                }
            }
            _footprintInstanceId = null;
            if (_selectionRing != null) { Destroy(_selectionRing); _selectionRing = null; }
            DismissInfoPopup();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_holdStarted && !_moveMode && !_placementMode)
            {
                if (Vector2.Distance(eventData.position, _holdStartPos) > DragThreshold)
                {
                    _holdStarted = false;
                    _holdTimer = 0f;
                }
            }

            if (_moveMode && _dragGhost != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    buildingContainer, eventData.position, eventData.pressEventCamera, out var localPoint);
                _dragGhost.GetComponent<RectTransform>().anchoredPosition = localPoint;

                // P&C: Shadow follows ghost with slight offset (below and behind)
                if (_dragShadow != null)
                    _dragShadow.GetComponent<RectTransform>().anchoredPosition = localPoint + new Vector2(4f, -6f);

                // Update placement highlight to snapped position
                UpdateHighlight(localPoint);

                // P&C: Show grid coordinate tooltip near ghost
                UpdateMoveCoordLabel(localPoint);
            }

            // P&C: Drag ghost in placement mode
            if (_placementMode && _placementGhost != null && buildingContainer != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    buildingContainer, eventData.position, eventData.pressEventCamera, out var localPoint);
                var snapOrigin = SnapToGrid(localPoint, _placementSize);
                UpdatePlacementPosition(snapOrigin);
            }
        }

        private void TryEnterMoveMode()
        {
            _holdStarted = false;

            var pointerData = new PointerEventData(EventSystem.current) { position = _holdStartPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            CityBuildingPlacement found = null;
            foreach (var result in results)
            {
                foreach (var placement in _placements)
                {
                    if (placement.VisualGO == result.gameObject ||
                        (placement.VisualGO != null && result.gameObject.transform.IsChildOf(placement.VisualGO.transform)))
                    {
                        found = placement;
                        break;
                    }
                }
                if (found != null) break;
            }

            if (found == null) return;

            _moveMode = true;
            _movingBuilding = found;
            _moveOriginalOrigin = found.GridOrigin;
            DestroyHoldIndicator();

            // P&C: Audio + haptic on move mode enter
            PlaySfx(_sfxTap);
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            if (scrollRect != null) scrollRect.enabled = false;
            SetGridOverlayVisible(true);
            ShowMoveConfirmBar();

            // P&C: Dim all non-moving buildings for visual focus
            foreach (var p in _placements)
            {
                if (p.VisualGO == null || p.InstanceId == found.InstanceId) continue;
                var pImg = p.VisualGO.GetComponent<Image>();
                if (pImg != null) pImg.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }

            // P&C: Fade original building in-place
            if (found.VisualGO != null)
                found.VisualGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);

            // P&C: Create drop shadow under ghost
            _dragShadow = new GameObject("DragShadow");
            _dragShadow.transform.SetParent(buildingContainer, false);
            var shadowRect = _dragShadow.AddComponent<RectTransform>();
            var shadowSize = FootprintScreenSize(found.Size);
            shadowRect.sizeDelta = shadowSize * 1.1f;
            var shadowImg = _dragShadow.AddComponent<Image>();
            shadowImg.color = new Color(0, 0, 0, 0.35f);
            shadowImg.raycastTarget = false;

            // Create drag ghost — P&C "lifted" style
            _dragGhost = new GameObject("DragGhost");
            _dragGhost.transform.SetParent(buildingContainer, false);
            var ghostRect = _dragGhost.AddComponent<RectTransform>();
            ghostRect.sizeDelta = FootprintScreenSize(found.Size);

            var ghostImg = _dragGhost.AddComponent<Image>();
            var srcImg = found.VisualGO?.GetComponent<Image>();
            if (srcImg != null)
            {
                ghostImg.sprite = srcImg.sprite;
                ghostImg.preserveAspect = true;
            }
            ghostImg.color = new Color(1, 1, 1, 0.85f);
            ghostImg.raycastTarget = false;

            // P&C: Scale up ghost slightly to show "lift"
            ghostRect.localScale = Vector3.one * 1.12f;

            // Haptic feedback for entering move mode
            EventBus.Publish(new BuildingMoveStartedEvent(found.InstanceId));

            // Create placement highlight
            CreateHighlight();
        }

        /// <summary>P&C: Enter move mode for a specific building instance (called from info popup Move button).</summary>
        public void EnterMoveModeForBuilding(string instanceId)
        {
            if (_moveMode || _placementMode) return;
            CityBuildingPlacement found = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == instanceId) { found = p; break; }
            }
            if (found == null) return;

            _moveMode = true;
            _movingBuilding = found;
            _moveOriginalOrigin = found.GridOrigin;

            if (scrollRect != null) scrollRect.enabled = false;
            SetGridOverlayVisible(true);
            ClearBuildingFootprint();
            ShowMoveConfirmBar();

            // P&C: Dim non-moving buildings
            foreach (var p in _placements)
            {
                if (p.VisualGO == null || p.InstanceId == found.InstanceId) continue;
                var pImg = p.VisualGO.GetComponent<Image>();
                if (pImg != null) pImg.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }

            if (found.VisualGO != null)
                found.VisualGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);

            // Create ghost + shadow at current position
            _dragShadow = new GameObject("DragShadow");
            _dragShadow.transform.SetParent(buildingContainer, false);
            var shadowRect = _dragShadow.AddComponent<RectTransform>();
            var shadowSize = FootprintScreenSize(found.Size);
            shadowRect.sizeDelta = shadowSize;
            var shadowImg = _dragShadow.AddComponent<Image>();
            shadowImg.color = new Color(0, 0, 0, 0.25f);
            shadowImg.raycastTarget = false;
            shadowRect.anchoredPosition = GridToLocalCenter(found.GridOrigin, found.Size) + new Vector2(4, -4);

            _dragGhost = new GameObject("DragGhost");
            _dragGhost.transform.SetParent(buildingContainer, false);
            var ghostRect = _dragGhost.AddComponent<RectTransform>();
            var ghostSize = FootprintScreenSize(found.Size);
            ghostSize.y = ghostSize.x * 1.5f;
            ghostRect.sizeDelta = ghostSize;
            var ghostImg = _dragGhost.AddComponent<Image>();
            var srcImg = found.VisualGO?.GetComponent<Image>();
            if (srcImg != null) ghostImg.sprite = srcImg.sprite;
            ghostImg.preserveAspect = true;
            ghostImg.raycastTarget = false;
            ghostRect.anchoredPosition = GridToLocalCenter(found.GridOrigin, found.Size);
            ghostRect.localScale = Vector3.one * 1.12f;

            EventBus.Publish(new BuildingMoveStartedEvent(found.InstanceId));
            CreateHighlight();
        }

        /// <summary>
        /// Safely clear only cells that belong to a specific building instance.
        /// Handles partial occupancy from overlapping layout gracefully.
        /// </summary>
        private void ClearCellsForInstance(Vector2Int origin, Vector2Int size, string instanceId)
        {
            for (int x = origin.x; x < origin.x + size.x; x++)
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (_occupancy.TryGetValue(pos, out var occupant) && occupant == instanceId)
                        _occupancy.Remove(pos);
                }
        }

        private void ExitMoveMode(PointerEventData eventData)
        {
            if (_dragGhost != null && _movingBuilding != null)
            {
                var ghostPos = _dragGhost.GetComponent<RectTransform>().anchoredPosition;
                var snapOrigin = SnapToGrid(ghostPos, _movingBuilding.Size);

                if (CanPlaceAt(snapOrigin, _movingBuilding.Size, _movingBuilding))
                {
                    // Normal move — clear only cells owned by this building, then mark new position
                    ClearCellsForInstance(_movingBuilding.GridOrigin, _movingBuilding.Size, _movingBuilding.InstanceId);
                    _movingBuilding.GridOrigin = snapOrigin;
                    MarkCells(snapOrigin, _movingBuilding.Size, _movingBuilding.InstanceId);

                    if (_movingBuilding.VisualGO != null)
                    {
                        PositionBuildingRect(
                            _movingBuilding.VisualGO.GetComponent<RectTransform>(),
                            _movingBuilding);
                        // P&C: Drop/slam animation on successful placement
                        StartCoroutine(DropSlamAnimation(_movingBuilding.VisualGO.transform));
                    }
                }
                else
                {
                    // P&C: Try swap — if target is occupied by another building of same size
                    if (!TrySwapBuildings(snapOrigin))
                    {
                        // P&C: Shake building back to original position on invalid drop
                        if (_movingBuilding.VisualGO != null)
                            StartCoroutine(ShakeBuilding(_movingBuilding.VisualGO.transform));
                    }
                }

                if (_movingBuilding.VisualGO != null)
                    _movingBuilding.VisualGO.GetComponent<Image>().color = Color.white;
            }

            // P&C: Restore all buildings to full brightness on exit move mode
            foreach (var p in _placements)
            {
                if (p.VisualGO != null)
                    p.VisualGO.GetComponent<Image>().color = Color.white;
            }

            if (_dragGhost != null) Destroy(_dragGhost);
            if (_dragShadow != null) Destroy(_dragShadow);
            DestroyMoveCoordLabel();
            _dragGhost = null;
            _dragShadow = null;
            _movingBuilding = null;
            _moveMode = false;

            // P&C: SFX on move complete + re-sort depth
            PlaySfx(_sfxBuildComplete);
            SortBuildingsByDepth();

            HideMoveGridCells();
            DestroyHighlight();
            DestroyMoveConfirmBar();
            SetGridOverlayVisible(false);
            if (scrollRect != null) scrollRect.enabled = true;
        }

        /// <summary>
        /// P&C: Swap two buildings' positions when dropped on top of another building.
        /// Only works if both buildings occupy the same footprint size.
        /// </summary>
        private bool TrySwapBuildings(Vector2Int targetOrigin)
        {
            if (_movingBuilding == null) return false;

            // Find the building occupying the target cells
            CityBuildingPlacement targetBuilding = null;
            for (int dx = 0; dx < _movingBuilding.Size.x && targetBuilding == null; dx++)
            {
                for (int dy = 0; dy < _movingBuilding.Size.y && targetBuilding == null; dy++)
                {
                    var cell = targetOrigin + new Vector2Int(dx, dy);
                    if (_occupancy.TryGetValue(cell, out string occupantId) && occupantId != _movingBuilding.InstanceId)
                    {
                        foreach (var p in _placements)
                        {
                            if (p.InstanceId == occupantId)
                            {
                                targetBuilding = p;
                                break;
                            }
                        }
                    }
                }
            }

            if (targetBuilding == null || targetBuilding.Size != _movingBuilding.Size) return false;

            // Perform swap
            var originA = _movingBuilding.GridOrigin;
            var originB = targetBuilding.GridOrigin;

            ClearCellsForInstance(originA, _movingBuilding.Size, _movingBuilding.InstanceId);
            ClearCellsForInstance(originB, targetBuilding.Size, targetBuilding.InstanceId);

            _movingBuilding.GridOrigin = originB;
            targetBuilding.GridOrigin = originA;

            MarkCells(originB, _movingBuilding.Size, _movingBuilding.InstanceId);
            MarkCells(originA, targetBuilding.Size, targetBuilding.InstanceId);

            if (_movingBuilding.VisualGO != null)
                PositionBuildingRect(_movingBuilding.VisualGO.GetComponent<RectTransform>(), _movingBuilding);
            if (targetBuilding.VisualGO != null)
                PositionBuildingRect(targetBuilding.VisualGO.GetComponent<RectTransform>(), targetBuilding);

            // P&C: Swap celebration — bounce both buildings
            if (_movingBuilding.VisualGO != null)
                StartCoroutine(BounceBuilding(_movingBuilding.VisualGO.transform));
            if (targetBuilding.VisualGO != null)
                StartCoroutine(BounceBuilding(targetBuilding.VisualGO.transform));
            PlaySfx(_sfxLevelUp);

            Debug.Log($"[CityGrid] Swapped {_movingBuilding.InstanceId} and {targetBuilding.InstanceId}.");
            return true;
        }

        /// <summary>P&C: Quick horizontal shake on invalid placement.</summary>
        private IEnumerator ShakeBuilding(Transform building)
        {
            var original = building.localPosition;
            float elapsed = 0f;
            float duration = 0.3f;
            float amplitude = 8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float offset = amplitude * (1f - t) * Mathf.Sin(t * Mathf.PI * 6f);
                building.localPosition = original + new Vector3(offset, 0, 0);
                yield return null;
            }
            building.localPosition = original;
        }

        // ====================================================================
        // P&C: Long-press hold indicator — radial fill ring
        // ====================================================================

        private void UpdateHoldIndicator()
        {
            if (_holdIndicator == null && _holdTimer > 0.1f) // Show after 100ms to avoid flash on quick taps
                CreateHoldIndicator();
            if (_holdFillImage != null)
                _holdFillImage.fillAmount = Mathf.Clamp01(_holdTimer / LongPressTime);
        }

        private void CreateHoldIndicator()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _holdIndicator = new GameObject("HoldIndicator");
            _holdIndicator.transform.SetParent(canvas.transform, false);
            _holdIndicator.transform.SetAsLastSibling();

            var rect = _holdIndicator.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(48, 48);

            // Convert hold start screen position to canvas position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, _holdStartPos, null, out var localPos);
            rect.anchoredPosition = localPos + new Vector2(0, 36f); // Above finger

            // Background ring (dark)
            var bgImg = _holdIndicator.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.08f, 0.15f, 0.6f);
            bgImg.raycastTarget = false;
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) bgImg.sprite = spr;

            // Fill ring (gold, radial fill)
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_holdIndicator.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4, 4);
            fillRect.offsetMax = new Vector2(-4, -4);
            _holdFillImage = fillGO.AddComponent<Image>();
            _holdFillImage.color = new Color(0.90f, 0.75f, 0.25f, 0.85f);
            _holdFillImage.type = Image.Type.Filled;
            _holdFillImage.fillMethod = Image.FillMethod.Radial360;
            _holdFillImage.fillOrigin = (int)Image.Origin360.Top;
            _holdFillImage.fillAmount = 0f;
            _holdFillImage.raycastTarget = false;
            if (spr != null) _holdFillImage.sprite = spr;
        }

        private void DestroyHoldIndicator()
        {
            if (_holdIndicator != null) Destroy(_holdIndicator);
            _holdIndicator = null;
            _holdFillImage = null;
        }

        // ====================================================================
        // Placement highlight — shows where building will snap during drag
        // ====================================================================

        private readonly List<GameObject> _highlightCells = new();
        private readonly List<GameObject> _moveGridCells = new();
        private Vector2Int _lastMoveGridCenter = new(-999, -999);
        private const int MoveGridRadius = 8;

        private void CreateHighlight()
        {
            if (_movingBuilding == null || buildingContainer == null) return;
            DestroyHighlight();
            // Create iso diamond cells for the highlight at the current position
            UpdateHighlight(GridToLocalCenter(_movingBuilding.GridOrigin, _movingBuilding.Size));
        }

        private void UpdateHighlight(Vector2 dragLocalPos)
        {
            if (_movingBuilding == null || buildingContainer == null) return;

            var snapOrigin = SnapToGrid(dragLocalPos, _movingBuilding.Size);
            bool valid = CanPlaceAt(snapOrigin, _movingBuilding.Size, _movingBuilding);

            // P&C: Show nearby grid cells with occupancy coloring
            ShowMoveGridCells(snapOrigin);

            // Rebuild iso cells at new snap position
            foreach (var go in _highlightCells) { if (go != null) Destroy(go); }
            _highlightCells.Clear();

            var fill = valid ? HighlightValid : HighlightInvalid;
            var border = valid ? BorderValid : BorderInvalid;

            for (int gx = 0; gx < _movingBuilding.Size.x; gx++)
            {
                for (int gy = 0; gy < _movingBuilding.Size.y; gy++)
                {
                    var cellGO = CreateIsoDiamondCell(buildingContainer,
                        snapOrigin.x + gx, snapOrigin.y + gy, fill, border);
                    _highlightCells.Add(cellGO);
                }
            }
        }

        private void DestroyHighlight()
        {
            foreach (var go in _highlightCells) { if (go != null) Destroy(go); }
            _highlightCells.Clear();
        }

        // ====================================================================
        // Move-mode grid cells (P&C: diamond grid with occupancy coloring)
        // ====================================================================

        private void ShowMoveGridCells(Vector2Int snapOrigin)
        {
            // Only rebuild if snap position changed
            if (snapOrigin == _lastMoveGridCenter) return;
            _lastMoveGridCenter = snapOrigin;

            HideMoveGridCells();

            string movingId = _movingBuilding?.InstanceId;
            var movingSize = _movingBuilding?.Size ?? Vector2Int.one;

            for (int gx = snapOrigin.x - MoveGridRadius; gx <= snapOrigin.x + movingSize.x + MoveGridRadius; gx++)
            {
                for (int gy = snapOrigin.y - MoveGridRadius; gy <= snapOrigin.y + movingSize.y + MoveGridRadius; gy++)
                {
                    if (gx < PlayableMinX || gx >= PlayableMaxX || gy < PlayableMinY || gy >= PlayableMaxY)
                        continue;

                    var cell = new Vector2Int(gx, gy);
                    bool occupied = _occupancy.TryGetValue(cell, out var occupant);
                    bool ownCell = occupied && occupant == movingId;

                    // Skip the moving building's own cells — highlight handles those
                    if (ownCell) continue;

                    Color fill, border;
                    if (occupied)
                    {
                        fill = new Color(0.8f, 0.2f, 0.15f, 0.07f);
                        border = new Color(0.7f, 0.25f, 0.2f, 0.18f);
                    }
                    else
                    {
                        fill = new Color(0.2f, 0.65f, 0.3f, 0.05f);
                        border = new Color(0.3f, 0.75f, 0.4f, 0.15f);
                    }

                    var cellGO = CreateIsoDiamondCell(buildingContainer, gx, gy, fill, border);
                    cellGO.transform.SetAsFirstSibling();
                    _moveGridCells.Add(cellGO);
                }
            }
        }

        private void HideMoveGridCells()
        {
            foreach (var go in _moveGridCells) { if (go != null) Destroy(go); }
            _moveGridCells.Clear();
            _lastMoveGridCenter = new Vector2Int(-999, -999);
        }

        /// <summary>
        /// Snap a local position to the nearest grid origin for a building of given size.
        /// </summary>
        private Vector2Int SnapToGrid(Vector2 localPos, Vector2Int buildingSize)
        {
            // Convert screen position to approximate grid center
            float adjustedY = localPos.y + IsoCenterY;
            float gcx = (localPos.x / HalfW + adjustedY / HalfH) * 0.5f;
            float gcy = (adjustedY / HalfH - localPos.x / HalfW) * 0.5f;
            // Subtract half the building size to get the origin
            int gx = Mathf.RoundToInt(gcx - buildingSize.x * 0.5f);
            int gy = Mathf.RoundToInt(gcy - buildingSize.y * 0.5f);
            return new Vector2Int(gx, gy);
        }

        private static bool IsInsidePlayableArea(Vector2Int pos)
        {
            return pos.x >= PlayableMinX && pos.x < PlayableMaxX
                && pos.y >= PlayableMinY && pos.y < PlayableMaxY;
        }

        // ====================================================================
        // P&C: Building placement mode — select from catalog, tap to place
        // ====================================================================

        private bool _placementMode;
        private string _placementBuildingId;
        private Vector2Int _placementSize;
        private GameObject _placementGhost;
        private readonly List<GameObject> _placementHighlightCells = new();
        private Vector2Int _placementSnapOrigin;
        private EventSubscription _placementRequestSub;
        private GameObject _placementButtons; // P&C: Confirm/Cancel floating buttons

        /// <summary>
        /// Enter placement mode: shows a ghost building that snaps to grid.
        /// Called when player selects a building type from the catalog.
        /// </summary>
        public void EnterPlacementMode(string buildingId, Vector2Int preferredOrigin)
        {
            if (_placementMode) ExitPlacementMode(false);
            if (_moveMode) return; // Don't allow placement while moving

            if (!BuildingSizes.TryGetValue(buildingId, out var size))
                size = new Vector2Int(2, 2);

            _placementMode = true;
            _placementBuildingId = buildingId;
            _placementSize = size;
            _placementSnapOrigin = preferredOrigin;

            SetGridOverlayVisible(true);
            if (scrollRect != null) scrollRect.enabled = false;

            // Create placement ghost
            if (buildingContainer != null)
            {
                _placementGhost = new GameObject("PlacementGhost");
                _placementGhost.transform.SetParent(buildingContainer, false);
                var ghostRect = _placementGhost.AddComponent<RectTransform>();
                ghostRect.sizeDelta = FootprintScreenSize(size);
                ghostRect.localScale = Vector3.one * 1.05f;

                var ghostImg = _placementGhost.AddComponent<Image>();
                // Try to load tier 1 sprite
                var sprite = Resources.Load<Sprite>($"Buildings/{buildingId}_t1");
                #if UNITY_EDITOR
                if (sprite == null)
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                        $"Assets/Art/Buildings/{buildingId}_t1.png");
                #endif
                if (sprite != null)
                {
                    ghostImg.sprite = sprite;
                    ghostImg.preserveAspect = true;
                }
                ghostImg.color = new Color(1, 1, 1, 0.6f);
                ghostImg.raycastTarget = false;

                // Iso diamond highlight cells will be created by UpdatePlacementPosition

                // Position at preferred origin
                UpdatePlacementPosition(preferredOrigin);
            }

            EventBus.Publish(new PlacementModeEnteredEvent(buildingId));

            // P&C: Create floating confirm/cancel buttons
            CreatePlacementButtons();
        }

        private void CreatePlacementButtons()
        {
            // Find canvas root for overlay buttons
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _placementButtons = new GameObject("PlacementButtons");
            _placementButtons.transform.SetParent(canvas.transform, false);
            _placementButtons.transform.SetAsLastSibling();

            // Semi-transparent banner at bottom
            var bannerGO = new GameObject("Banner");
            bannerGO.transform.SetParent(_placementButtons.transform, false);
            var bannerRect = bannerGO.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0f, 0f);
            bannerRect.anchorMax = new Vector2(1f, 0.08f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            var bannerImg = bannerGO.AddComponent<Image>();
            bannerImg.color = new Color(0.06f, 0.04f, 0.10f, 0.85f);
            bannerImg.raycastTarget = false;

            // Instruction text
            var instrGO = new GameObject("Instructions");
            instrGO.transform.SetParent(_placementButtons.transform, false);
            var instrRect = instrGO.AddComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0.05f, 0.085f);
            instrRect.anchorMax = new Vector2(0.95f, 0.13f);
            instrRect.offsetMin = Vector2.zero;
            instrRect.offsetMax = Vector2.zero;
            var instrText = instrGO.AddComponent<Text>();
            instrText.text = "Drag to position building. Tap CONFIRM to place.";
            instrText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instrText.fontSize = 11;
            instrText.alignment = TextAnchor.MiddleCenter;
            instrText.color = new Color(0.80f, 0.75f, 0.65f, 0.9f);
            instrText.raycastTarget = false;

            // Confirm button (green checkmark)
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(_placementButtons.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.52f, 0.01f);
            confirmRect.anchorMax = new Vector2(0.95f, 0.075f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmImg = confirmGO.AddComponent<Image>();
            confirmImg.color = new Color(0.20f, 0.72f, 0.35f, 1f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;
            confirmBtn.onClick.AddListener(() => ExitPlacementMode(true));
            AddPlacementButtonLabel(confirmGO, "\u2714 CONFIRM");

            // Cancel button (red X)
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(_placementButtons.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.05f, 0.01f);
            cancelRect.anchorMax = new Vector2(0.48f, 0.075f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.65f, 0.20f, 0.20f, 1f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(() => ExitPlacementMode(false));
            AddPlacementButtonLabel(cancelGO, "\u2716 CANCEL");
        }

        private static void AddPlacementButtonLabel(GameObject parent, string label)
        {
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(parent.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        /// <summary>
        /// Exit placement mode. If confirmed, publishes PlacementConfirmedEvent.
        /// </summary>
        public void ExitPlacementMode(bool confirm)
        {
            if (!_placementMode) return;

            if (confirm && CanPlaceAt(_placementSnapOrigin, _placementSize, null))
            {
                EventBus.Publish(new PlacementConfirmedEvent(
                    _placementBuildingId, _placementSnapOrigin, _placementSize));
            }

            if (_placementGhost != null) Destroy(_placementGhost);
            if (_placementButtons != null) Destroy(_placementButtons);
            foreach (var go in _placementHighlightCells) { if (go != null) Destroy(go); }
            _placementHighlightCells.Clear();
            _placementGhost = null;
            _placementButtons = null;
            _placementMode = false;
            _placementBuildingId = null;

            SetGridOverlayVisible(false);
            if (scrollRect != null) scrollRect.enabled = true;
        }

        private void UpdatePlacementPosition(Vector2Int origin)
        {
            _placementSnapOrigin = origin;
            var center = GridToLocalCenter(origin, _placementSize);

            if (_placementGhost != null)
                _placementGhost.GetComponent<RectTransform>().anchoredPosition = center;

            // Rebuild iso diamond highlight cells at new position
            foreach (var go in _placementHighlightCells) { if (go != null) Destroy(go); }
            _placementHighlightCells.Clear();

            if (buildingContainer != null)
            {
                bool valid = CanPlaceAt(origin, _placementSize, null);
                var fill = valid ? HighlightValid : HighlightInvalid;
                var border = valid ? BorderValid : BorderInvalid;

                for (int gx = 0; gx < _placementSize.x; gx++)
                {
                    for (int gy = 0; gy < _placementSize.y; gy++)
                    {
                        var cellGO = CreateIsoDiamondCell(buildingContainer,
                            origin.x + gx, origin.y + gy, fill, border);
                        _placementHighlightCells.Add(cellGO);
                    }
                }

                // P&C: Tint ghost green/red based on placement validity
                if (_placementGhost != null)
                {
                    var ghostImg = _placementGhost.GetComponent<Image>();
                    if (ghostImg != null)
                    {
                        ghostImg.color = valid
                            ? new Color(0.7f, 1f, 0.7f, 0.6f)   // green tint = valid
                            : new Color(1f, 0.5f, 0.5f, 0.6f);  // red tint = blocked
                    }

                    // P&C: Glow ring around ghost
                    var glowRing = _placementGhost.transform.Find("GlowRing");
                    if (glowRing == null)
                    {
                        var glowGO = new GameObject("GlowRing");
                        glowGO.transform.SetParent(_placementGhost.transform, false);
                        var glowRect = glowGO.AddComponent<RectTransform>();
                        glowRect.anchorMin = new Vector2(-0.10f, -0.06f);
                        glowRect.anchorMax = new Vector2(1.10f, 1.06f);
                        glowRect.offsetMin = Vector2.zero;
                        glowRect.offsetMax = Vector2.zero;
                        glowGO.transform.SetAsFirstSibling();
                        var glowImg = glowGO.AddComponent<Image>();
                        glowImg.color = valid
                            ? new Color(0.3f, 0.9f, 0.3f, 0.25f)
                            : new Color(0.9f, 0.3f, 0.3f, 0.25f);
                        glowImg.raycastTarget = false;
                        glowRing = glowGO.transform;
                    }
                    else
                    {
                        var glowImg = glowRing.GetComponent<Image>();
                        if (glowImg != null)
                            glowImg.color = valid
                                ? new Color(0.3f, 0.9f, 0.3f, 0.25f)
                                : new Color(0.9f, 0.3f, 0.3f, 0.25f);
                    }
                }
            }
        }

        /// <summary>Whether we are currently in placement mode.</summary>
        public bool IsInPlacementMode => _placementMode;

        // ====================================================================
        // P&C: Time-of-day ambient tint overlay
        // ====================================================================

        private GameObject _ambientTintOverlay;

        private void CreateAmbientTintOverlay()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _ambientTintOverlay = new GameObject("AmbientTint");
            _ambientTintOverlay.transform.SetParent(canvas.transform, false);
            // Behind UI but above city content
            _ambientTintOverlay.transform.SetSiblingIndex(1);

            var rect = _ambientTintOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = _ambientTintOverlay.AddComponent<Image>();
            img.color = GetAmbientTintColor();
            img.raycastTarget = false;

            StartCoroutine(AnimateAmbientTint());
        }

        private static Color GetAmbientTintColor()
        {
            int hour = System.DateTime.Now.Hour;
            // Dawn 5-7: warm orange glow
            if (hour >= 5 && hour < 7)
                return new Color(1f, 0.75f, 0.40f, 0.06f);
            // Morning 7-11: clear/neutral
            if (hour >= 7 && hour < 11)
                return new Color(1f, 0.95f, 0.85f, 0.02f);
            // Midday 11-15: slight warm
            if (hour >= 11 && hour < 15)
                return new Color(1f, 0.98f, 0.90f, 0.03f);
            // Afternoon 15-18: golden hour
            if (hour >= 15 && hour < 18)
                return new Color(1f, 0.80f, 0.45f, 0.07f);
            // Dusk 18-20: purple-orange
            if (hour >= 18 && hour < 20)
                return new Color(0.80f, 0.45f, 0.60f, 0.08f);
            // Night 20-5: deep blue
            return new Color(0.20f, 0.25f, 0.55f, 0.10f);
        }

        private IEnumerator AnimateAmbientTint()
        {
            while (_ambientTintOverlay != null)
            {
                var img = _ambientTintOverlay.GetComponent<Image>();
                if (img != null)
                {
                    var target = GetAmbientTintColor();
                    img.color = Color.Lerp(img.color, target, Time.deltaTime * 0.1f);
                }
                // Only update every 2 seconds for perf
                yield return new WaitForSeconds(2f);
            }
        }

        // ====================================================================
        // P&C: Mini-map overview indicator
        // ====================================================================

        private GameObject _miniMapPanel;
        private readonly List<Image> _miniMapDots = new();

        private void CreateMiniMap()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _miniMapPanel = new GameObject("MiniMap");
            _miniMapPanel.transform.SetParent(canvas.transform, false);
            _miniMapPanel.transform.SetAsLastSibling();

            var rect = _miniMapPanel.AddComponent<RectTransform>();
            // Bottom-left corner, above nav bar
            rect.anchorMin = new Vector2(0.02f, 0.11f);
            rect.anchorMax = new Vector2(0.16f, 0.23f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _miniMapPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.08f, 0.75f);
            bg.raycastTarget = false;

            var border = _miniMapPanel.AddComponent<Outline>();
            border.effectColor = new Color(0.60f, 0.48f, 0.20f, 0.5f);
            border.effectDistance = new Vector2(1f, -1f);

            // Place dots for each building
            foreach (var p in _placements)
            {
                var dot = new GameObject($"Dot_{p.InstanceId}");
                dot.transform.SetParent(_miniMapPanel.transform, false);

                var dotRect = dot.AddComponent<RectTransform>();
                dotRect.sizeDelta = new Vector2(3f, 3f);

                // Map grid position to minimap position (0-1 range)
                float nx = (float)p.GridOrigin.x / GridColumns;
                float ny = (float)p.GridOrigin.y / GridRows;
                // Isometric transform for minimap
                float mx = (nx - ny) * 0.5f + 0.5f;
                float my = (nx + ny) * 0.5f;
                dotRect.anchorMin = new Vector2(mx, my);
                dotRect.anchorMax = new Vector2(mx, my);
                dotRect.anchoredPosition = Vector2.zero;

                var dotImg = dot.AddComponent<Image>();
                dotImg.color = GetMiniMapDotColor(p.BuildingId);
                dotImg.raycastTarget = false;
                _miniMapDots.Add(dotImg);
            }

            // Viewport indicator (shows current scroll position)
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(_miniMapPanel.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0.3f, 0.3f);
            vpRect.anchorMax = new Vector2(0.7f, 0.7f);
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.15f);
            vpImg.raycastTarget = false;
            var vpBorder = viewport.AddComponent<Outline>();
            vpBorder.effectColor = new Color(1f, 1f, 1f, 0.4f);
            vpBorder.effectDistance = new Vector2(0.5f, -0.5f);

            StartCoroutine(UpdateMiniMapViewport(vpRect));
        }

        private IEnumerator UpdateMiniMapViewport(RectTransform vpRect)
        {
            while (_miniMapPanel != null && vpRect != null)
            {
                if (scrollRect != null && contentContainer != null)
                {
                    // Approximate viewport position from scroll rect normalized position
                    float nx = scrollRect.horizontalNormalizedPosition;
                    float ny = scrollRect.verticalNormalizedPosition;

                    // Viewport size inversely proportional to zoom
                    float viewSize = Mathf.Clamp(0.15f / _currentZoom, 0.08f, 0.5f);

                    vpRect.anchorMin = new Vector2(
                        Mathf.Clamp(nx - viewSize, 0f, 1f - viewSize * 2f),
                        Mathf.Clamp(ny - viewSize, 0f, 1f - viewSize * 2f));
                    vpRect.anchorMax = new Vector2(
                        Mathf.Clamp(nx + viewSize, viewSize * 2f, 1f),
                        Mathf.Clamp(ny + viewSize, viewSize * 2f, 1f));
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        private static Color GetMiniMapDotColor(string buildingId)
        {
            if (buildingId == "stronghold") return new Color(1f, 0.85f, 0.25f); // Gold
            if (buildingId.Contains("farm") || buildingId.Contains("mine") || buildingId.Contains("quarry"))
                return new Color(0.40f, 0.80f, 0.40f); // Green — resource
            if (buildingId.Contains("arcane") || buildingId.Contains("tower"))
                return new Color(0.60f, 0.40f, 0.90f); // Purple — magic
            if (buildingId.Contains("barracks") || buildingId.Contains("training") || buildingId.Contains("armory"))
                return new Color(0.90f, 0.35f, 0.35f); // Red — military
            if (buildingId.Contains("wall") || buildingId.Contains("watch"))
                return new Color(0.70f, 0.70f, 0.75f); // Silver — defense
            return new Color(0.65f, 0.55f, 0.40f); // Brown — other
        }

        // ====================================================================
        // P&C: VIP boost indicator on boosted buildings
        // ====================================================================

        private void AddVIPBoostIndicator(GameObject building, string buildingId)
        {
            if (building == null) return;
            // Remove existing
            var existing = building.transform.Find("VIPBoost");
            if (existing != null) Destroy(existing.gameObject);

            // Only show on resource producers and military buildings
            bool isProducer = buildingId.Contains("farm") || buildingId.Contains("mine")
                || buildingId.Contains("quarry") || buildingId.Contains("arcane_tower");
            bool isMilitary = buildingId.Contains("barracks") || buildingId.Contains("training");
            if (!isProducer && !isMilitary) return;

            var badge = new GameObject("VIPBoost");
            badge.transform.SetParent(building.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            // Top-right corner of building
            rect.anchorMin = new Vector2(0.70f, 0.80f);
            rect.anchorMax = new Vector2(0.95f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.85f, 0.55f, 0.10f, 0.85f);
            bg.raycastTarget = false;
            var border = badge.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.85f, 0.30f, 0.9f);
            border.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            string boostText = isProducer ? "\u2191+10%" : "\u2191SPD";
            text.text = boostText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
        }
    }

    /// <summary>
    /// Tracks a building or decoration placed on the city grid.
    /// </summary>
    public class CityBuildingPlacement
    {
        public string InstanceId;     // unique per placement, e.g. "grain_farm_0"
        public string BuildingId;     // building type, e.g. "grain_farm"
        public int Tier;
        public Vector2Int GridOrigin; // bottom-left cell
        public Vector2Int Size;       // width x height in cells
        public GameObject VisualGO;   // runtime reference
    }

    public readonly struct BuildingTappedEvent
    {
        public readonly string BuildingId;
        public readonly string InstanceId;
        public readonly int Tier;
        public readonly Vector2Int GridPosition;
        public readonly GameObject VisualGO;

        public BuildingTappedEvent(CityBuildingPlacement p)
        {
            BuildingId = p.BuildingId;
            InstanceId = p.InstanceId;
            Tier = p.Tier;
            GridPosition = p.GridOrigin;
            VisualGO = p.VisualGO;
        }

        public BuildingTappedEvent(string buildingId, string instanceId, int tier, Vector2Int gridPos)
        {
            BuildingId = buildingId;
            InstanceId = instanceId;
            Tier = tier;
            GridPosition = gridPos;
            VisualGO = null;
        }
    }

    public readonly struct BuildingMoveStartedEvent
    {
        public readonly string InstanceId;
        public BuildingMoveStartedEvent(string id) { InstanceId = id; }
    }

    public readonly struct BuildingDoubleTappedEvent
    {
        public readonly string InstanceId;
        public readonly string BuildingId;
        public readonly int Tier;
        public BuildingDoubleTappedEvent(string id, string bid, int t) { InstanceId = id; BuildingId = bid; Tier = t; }
    }

    public readonly struct EmptyCellTappedEvent
    {
        public readonly Vector2Int GridPosition;
        public EmptyCellTappedEvent(Vector2Int pos) { GridPosition = pos; }
    }

    public readonly struct PlacementModeEnteredEvent
    {
        public readonly string BuildingId;
        public PlacementModeEnteredEvent(string id) { BuildingId = id; }
    }

    public readonly struct PlacementConfirmedEvent
    {
        public readonly string BuildingId;
        public readonly Vector2Int GridOrigin;
        public readonly Vector2Int Size;
        public PlacementConfirmedEvent(string id, Vector2Int origin, Vector2Int size) { BuildingId = id; GridOrigin = origin; Size = size; }
    }
}
