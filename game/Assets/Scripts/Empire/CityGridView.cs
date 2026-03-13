using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AshenThrone.Core;
using AshenThrone.Data;
using AshenThrone.Heroes;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Manages a large virtual city grid (48x48) with isometric diamond layout.
    /// No per-cell GameObjects — occupancy tracked in a dictionary.
    /// Grid overlay + placement highlight shown only during move mode.
    /// Coordinate conversion uses 2:1 isometric projection.
    /// </summary>
    public partial class CityGridView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
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
        // P&C-style building sizes: military & key buildings are LARGER than resource buildings
        public static readonly Dictionary<string, Vector2Int> BuildingSizes = new()
        {
            { "stronghold",       new Vector2Int(6, 6) },   // Citadel — massive central building
            { "barracks",         new Vector2Int(4, 3) },   // Large military complex
            { "training_ground",  new Vector2Int(3, 3) },   // Military staging area
            { "armory",           new Vector2Int(3, 3) },   // Equipment storage — larger than resource bldgs
            { "academy",          new Vector2Int(3, 3) },   // Research institute — prominent
            { "guild_hall",       new Vector2Int(4, 3) },   // Alliance war hall — large
            { "embassy",          new Vector2Int(3, 3) },   // Diplomatic building
            { "marketplace",      new Vector2Int(3, 3) },   // Trading hub
            { "forge",            new Vector2Int(3, 2) },   // Crafting workshop — medium
            { "library",          new Vector2Int(3, 2) },   // Knowledge building — medium
            { "laboratory",       new Vector2Int(3, 2) },   // Research lab — medium
            { "enchanting_tower", new Vector2Int(2, 3) },   // Tall magic tower — narrow+tall
            { "arcane_tower",     new Vector2Int(2, 3) },   // Magic tower — narrow+tall
            { "observatory",      new Vector2Int(2, 3) },   // Tall tower — narrow+tall
            { "hero_shrine",      new Vector2Int(3, 3) },   // Sacred building — prominent
            { "grain_farm",       new Vector2Int(2, 2) },   // Small resource patch
            { "iron_mine",        new Vector2Int(2, 2) },   // Small resource site
            { "stone_quarry",     new Vector2Int(2, 2) },   // Small resource site
            { "wall",             new Vector2Int(2, 1) },   // Thin wall segment
            { "watch_tower",      new Vector2Int(2, 2) },   // Small defensive tower
            { "archive",          new Vector2Int(2, 2) },   // Small knowledge building
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
            { "grain_farm", 6 }, { "iron_mine", 4 }, { "stone_quarry", 4 }, { "arcane_tower", 3 },
            { "barracks", 2 }, { "training_ground", 2 }, { "wall", 8 },
            { "watch_tower", 6 }, { "forge", 2 },
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

        // P&C: Category filter state
        private string _activeCategoryFilter;

        // P&C: Free instant-complete threshold (seconds)
        private const int FreeSpeedUpThresholdSeconds = 300; // 5 minutes

        // Placement highlight (shows snap target during drag) — iso diamond cells
        // P&C: Bold placement highlights — clearly visible green/red
        private static readonly Color HighlightValid = new(0.15f, 0.90f, 0.30f, 0.35f);
        private static readonly Color HighlightInvalid = new(0.95f, 0.15f, 0.15f, 0.35f);
        private static readonly Color BorderValid = new(0.20f, 1f, 0.40f, 0.70f);
        private static readonly Color BorderInvalid = new(1f, 0.20f, 0.20f, 0.70f);

        // Dark fantasy: Building footprint highlight on tap — purple-gold
        private static readonly Color FootprintColor = new(0.50f, 0.30f, 0.70f, 0.22f);
        private static readonly Color FootprintBorder = new(0.78f, 0.55f, 0.22f, 0.55f);
        private readonly List<GameObject> _footprintCells = new();
        private string _footprintInstanceId;
        private GameObject _selectionRing;

        // Pinch-zoom state
        private const float ZoomMin = 0.3f;
        private const float ZoomMax = 5.0f;
        private const float ZoomSpeed = 0.005f; // per pixel of pinch delta
        private const float MouseScrollZoomSpeed = 0.15f;
        private const float ZoomLerpSpeed = 8f; // P&C: smooth zoom interpolation
        private const float DefaultZoom = 1.3f;
        private float _currentZoom = DefaultZoom;
        private float _targetZoom = DefaultZoom;
        private Vector2 _zoomPivotScreen;
        private bool _isPinching;
        private float _lastPinchDistance;
        private int _touchCount;

        // P&C: Zoom level indicator
        private GameObject _zoomIndicator;
        private Text _zoomIndicatorText;
        private float _zoomIndicatorFadeTimer;
        private const float ZoomIndicatorShowDuration = 1.5f;

        private EventSubscription _upgradeCompletedSub;
        private EventSubscription _demolishedSub;
        private EventSubscription _doubleTapZoomSub;
        private EventSubscription _allianceHelpSub;

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
        private GameObject _infoPopupDimOverlay;
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
        // P&C: Active upgrade strip at bottom
        private GameObject _activeUpgradeStrip;

        // P&C: Long-press hold indicator
        private GameObject _holdIndicator;
        private Image _holdFillImage;

        // P&C: Soft-center on single tap
        private Coroutine _softCenterCoroutine;
        private const float SoftCenterDuration = 0.30f;
        private const float SoftCenterStrength = 0.75f; // 75% toward center (P&C-style smooth pan)

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
            _allianceHelpSub = EventBus.Subscribe<AllianceHelpRequestedEvent>(evt => ShowAllianceHelpReceived(evt.PlacedId, 300));
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
            // P&C: Empty building slot indicators ("+" markers on open plots)
            CreateEmptyBuildSlotIndicators();
            RefreshInstanceCountBadges();
            RefreshUpgradeIndicators();
            RefreshUpgradeArrows();
            // Old compact builder pill replaced by P&C queue status panel
            // CreateBuilderCountHUD();
            CreateActiveUpgradeStrip();
            CreateBuildingQuickNav();
            // P&C: Ambient tint, weather, mini-map, and resource countdown timers
            CreateAmbientTintOverlay();
            CreateWeatherOverlay();
            CreateMiniMap();
            StartCoroutine(UpdateResourceCountdownTimers());
            // P&C: Show greyed-out building unlock previews for next SH level
            CreateBuildingUnlockPreviews();
            CreateResourceIncomeTicker();
            // Center on stronghold after layout rebuild
            StartCoroutine(DelayedCenterOnStronghold());
            // P&C: Show "Welcome back" offline earnings banner
            StartCoroutine(ShowOfflineEarningsBanner());
            // P&C: Peace shield dome (active by default)
            CreatePeaceShield();
            // P&C: Event timer banners on left side
            CreateEventTimerBanners();
            // P&C: Auto-collect toggle button
            CreateAutoCollectToggle();
            // P&C: Left-side circular event icon column
            CreateDailyChest();       // slot 0 (bottom)
            CreateMerchantIcon();     // slot 1
            CreateEventHubIcon();     // slot 2
            CreateGiftsIcon();        // slot 3
            // P&C: City prosperity badge — disabled, overlaps with SceneUI info panel
            // UpdateProsperityBadge();
            // P&C IT101: Recommended upgrade advisor arrow
            RefreshAdvisorArrow();
            // P&C IT103: Wire resource bar icons to production breakdown popup
            WireResourceBarTapHandlers();
            // P&C: Left-side queue status panel (Build/Research slots)
            CreateQueueStatusPanel();
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

            // P&C: Troop march spawning
            _troopMarchSpawnTimer += Time.deltaTime;
            if (_troopMarchSpawnTimer >= TroopMarchSpawnInterval)
            {
                _troopMarchSpawnTimer = 0f;
                SpawnTroopMarch();
                CleanupMarchingTroops();
            }

            // P&C: Pulse selection ring glow (preserves category hue)
            if (_selectionRing != null)
            {
                var ringImg = _selectionRing.GetComponent<Image>();
                if (ringImg != null)
                {
                    float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 3f);
                    var c = ringImg.color;
                    ringImg.color = new Color(c.r, c.g, c.b, pulse);
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

            // P&C: Day/night cycle updates (every 30s to avoid perf cost)
            _dayNightTimer += Time.deltaTime;
            if (_dayNightTimer >= 30f)
            {
                _dayNightTimer = 0f;
                UpdateDayNightCycle();
            }

            // P&C: Resource overflow check
            _overflowCheckTimer += Time.deltaTime;
            if (_overflowCheckTimer >= OverflowCheckInterval)
            {
                _overflowCheckTimer = 0f;
                CheckResourceOverflow();
            }

            // P&C: Tick troop training queues
            TickTroopQueues();

            // P&C: Tick queue status panel (builder/research slots)
            TickQueueStatusPanel();

            // P&C: Auto-collect resource bubbles
            TickAutoCollect();

            // P&C: Prosperity badge disabled (overlaps SceneUI info panel)
            // _prosperityRefreshTimer += Time.deltaTime;
            // if (_prosperityRefreshTimer >= 30f) { _prosperityRefreshTimer = 0f; UpdateProsperityBadge(); }

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

            // P&C IT103: Simplify buildings when zoomed out
            UpdateBuildingSimplification();

            // P&C: Show zoom level indicator briefly
            ShowZoomLevelIndicator();
        }

        /// <summary>P&C: Floating zoom level indicator that appears during zoom changes.</summary>
        private void ShowZoomLevelIndicator()
        {
            _zoomIndicatorFadeTimer = ZoomIndicatorShowDuration;

            if (_zoomIndicator == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return;

                _zoomIndicator = new GameObject("ZoomIndicator");
                _zoomIndicator.transform.SetParent(canvas.transform, false);
                var rect = _zoomIndicator.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.40f, 0.50f);
                rect.anchorMax = new Vector2(0.60f, 0.54f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var bg = _zoomIndicator.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.03f, 0.10f, 0.70f);
                bg.raycastTarget = false;
                var outline = _zoomIndicator.AddComponent<Outline>();
                outline.effectColor = new Color(0.70f, 0.55f, 0.20f, 0.50f);
                outline.effectDistance = new Vector2(0.6f, -0.6f);

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(_zoomIndicator.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                _zoomIndicatorText = textGO.AddComponent<Text>();
                _zoomIndicatorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _zoomIndicatorText.fontSize = 11;
                _zoomIndicatorText.fontStyle = FontStyle.Bold;
                _zoomIndicatorText.alignment = TextAnchor.MiddleCenter;
                _zoomIndicatorText.color = new Color(0.90f, 0.80f, 0.50f);
                _zoomIndicatorText.raycastTarget = false;
                var shadow = textGO.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.9f);
                shadow.effectDistance = new Vector2(0.5f, -0.5f);

                StartCoroutine(FadeZoomIndicator());
            }

            if (_zoomIndicatorText != null)
                _zoomIndicatorText.text = $"\u2922 x{_currentZoom:F1}";

            // Ensure visible
            var cg = _zoomIndicator.GetComponent<CanvasGroup>();
            if (cg == null) cg = _zoomIndicator.AddComponent<CanvasGroup>();
            cg.alpha = 0.9f;
        }

        /// <summary>P&C: Fade out zoom indicator after timeout.</summary>
        private IEnumerator FadeZoomIndicator()
        {
            while (_zoomIndicator != null)
            {
                if (_zoomIndicatorFadeTimer > 0f)
                {
                    _zoomIndicatorFadeTimer -= Time.deltaTime;
                }
                else
                {
                    var cg = _zoomIndicator.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha > 0f)
                    {
                        cg.alpha = Mathf.MoveTowards(cg.alpha, 0f, Time.deltaTime * 2f);
                    }
                }
                yield return null;
            }
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

                // Name label — P&C shows at medium+ zoom (always visible in city view)
                var nameLabel = t.Find("NameLabel");
                if (nameLabel != null) nameLabel.gameObject.SetActive(showMedium);

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

        /// <summary>P&C: On double-tap, open building's primary function (train/research/craft/etc).</summary>
        private void OnDoubleTapZoom(BuildingDoubleTappedEvent evt)
        {
            CityBuildingPlacement target = null;
            foreach (var p in _placements)
                if (p.InstanceId == evt.InstanceId) { target = p; break; }
            if (target?.VisualGO == null || contentContainer == null) return;

            // P&C: Haptic feedback on double-tap
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            // P&C: Double-tap opens primary function panel directly
            string bid = evt.BuildingId;
            string iid = evt.InstanceId;
            int tier = evt.Tier;
            DismissInfoPopup();

            switch (bid)
            {
                case "barracks":
                case "training_ground":
                    ShowTroopTrainingPanel(iid, bid, tier);
                    return;
                case "academy":
                case "library":
                case "archive":
                case "observatory":
                case "laboratory":
                    ShowResearchQuickPanel(tier);
                    return;
                case "forge":
                case "armory":
                    ShowCraftingPanel(bid, tier);
                    return;
                case "hero_shrine":
                    ShowBuildingInfoPanel(bid, iid, tier);
                    return;
                case "marketplace":
                    ShowTradePanel(tier);
                    return;
            }

            // Default: zoom in and center on building
            if (_smoothZoomCoroutine != null) StopCoroutine(_smoothZoomCoroutine);
            _smoothZoomCoroutine = StartCoroutine(SmoothZoomToBuilding(target));
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

        /// <summary>P&C: Play category-specific SFX on building tap.</summary>
        private void PlayBuildingTapSfx(string buildingId)
        {
            // Try category-specific clip first, fall back to generic tap
            string sfxName = GetBuildingTapSfxName(buildingId);
            var clip = Resources.Load<AudioClip>($"Audio/SFX/{sfxName}");
            if (clip == null) clip = _sfxTap; // fallback
            PlaySfx(clip);
        }

        private static string GetBuildingTapSfxName(string buildingId) => buildingId switch
        {
            "stronghold" => "sfx_tap_castle",
            "barracks" or "training_ground" or "armory" => "sfx_tap_military",
            "forge" or "enchanting_tower" => "sfx_tap_forge",
            "grain_farm" or "iron_mine" or "stone_quarry" => "sfx_tap_resource",
            "arcane_tower" or "observatory" or "laboratory" => "sfx_tap_magic",
            "marketplace" or "guild_hall" or "embassy" => "sfx_tap_social",
            "wall" or "watch_tower" => "sfx_tap_defense",
            "academy" or "library" or "archive" => "sfx_tap_research",
            "hero_shrine" => "sfx_tap_shrine",
            _ => "sfx_btn_click"
        };

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
            // Diamond footprint width: (sizeX + sizeY) * HalfW is the exact isometric diamond width.
            // Scale to 0.90x — buildings nearly fill their diamond for dense packed P&C city feel.
            // Minimal terrain visible between buildings — city feels packed and alive.
            float w = (size.x + size.y) * HalfW * 0.90f;
            // Taller bounding box — P&C buildings rise well above their footprint
            float h = w * 1.4f;
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
                    ? new Color(0.40f, 0.25f, 0.65f, 0.30f)  // bright move-mode (dark fantasy purple)
                    : new Color(0.30f, 0.20f, 0.50f, 0.15f);  // faint dark fantasy purple grid
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

            // P&C IT102: Category glow aura at building base
            CreateCategoryGlow(go, placement.BuildingId);

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

            // P&C: Auto-collect countdown timer on resource buildings
            CreateCollectCountdownLabel(go, placement.BuildingId);

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

        /// <summary>
        /// P&C: Per-cell isometric diamond shadow under each building.
        /// Uses the same iso diamond geometry as the grid overlay so shadows
        /// perfectly align with the grid shown during move mode.
        /// </summary>
        private void CreateBuildingShadow(GameObject building, Vector2Int size)
        {
            // Container for all shadow cells (first sibling = behind sprite)
            var shadowRoot = new GameObject("ShadowCells");
            shadowRoot.transform.SetParent(building.transform, false);
            shadowRoot.transform.SetAsFirstSibling();
            var rootRect = shadowRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            float yOffset = FootprintScreenSize(size).y * 0.15f;
            int sx = size.x, sy = size.y;

            // Subtle shadow plate — slightly darker than P&C dark terrain to ground buildings
            Color plateFill = new Color(0.04f, 0.06f, 0.03f, 0.35f);
            Color plateBorder = new Color(0.06f, 0.08f, 0.04f, 0.25f);
            var diamondSpr = GetDiamondCellSprite();

            for (int gx = 0; gx < sx; gx++)
            {
                for (int gy = 0; gy < sy; gy++)
                {
                    // Offset from building center to this cell's iso center
                    float dx = (gx - gy + (sy - sx) / 2.0f) * HalfW;
                    float dy = (gx + gy + 1.0f - (sx + sy) / 2.0f) * HalfH - yOffset;

                    var cellGO = new GameObject($"Shadow_{gx}_{gy}");
                    cellGO.transform.SetParent(shadowRoot.transform, false);

                    var rect = cellGO.AddComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(dx, dy);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    // Diamond sprite — no rotation needed
                    rect.sizeDelta = new Vector2(CellSize, CellSize * 0.5f);

                    var img = cellGO.AddComponent<Image>();
                    if (diamondSpr != null) { img.sprite = diamondSpr; img.type = Image.Type.Simple; }
                    img.color = plateBorder;
                    img.raycastTarget = false;

                    // Fill
                    var fillGO = new GameObject("Fill");
                    fillGO.transform.SetParent(cellGO.transform, false);
                    fillGO.transform.SetAsFirstSibling();
                    var fillRect = fillGO.AddComponent<RectTransform>();
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = new Vector2(1f, 0.5f);
                    fillRect.offsetMax = new Vector2(-1f, -0.5f);
                    var fillImg = fillGO.AddComponent<Image>();
                    if (diamondSpr != null) { fillImg.sprite = diamondSpr; fillImg.type = Image.Type.Simple; }
                    fillImg.color = plateFill;
                    fillImg.raycastTarget = false;
                }
            }

            // Soft radial shadow underneath for depth
            var shadowGO = new GameObject("DropShadow");
            shadowGO.transform.SetParent(building.transform, false);
            shadowGO.transform.SetAsFirstSibling(); // Behind shadow cells
            var shadowRect = shadowGO.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(-0.08f, -0.12f);
            shadowRect.anchorMax = new Vector2(1.08f, 0.18f);
            shadowRect.offsetMin = Vector2.zero;
            shadowRect.offsetMax = Vector2.zero;
            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.color = new Color(0.05f, 0.02f, 0.10f, 0.30f);
            shadowImg.raycastTarget = false;
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) shadowImg.sprite = spr;
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

        /// <summary>P&C IT102: Subtle category-colored glow aura at building base.</summary>
        private void CreateCategoryGlow(GameObject building, string buildingId)
        {
            // Stronghold has its own dedicated glow — skip generic category glow
            if (buildingId == "stronghold") return;

            Color glowColor = GetBuildingCategoryGlowColor(buildingId);
            if (glowColor.a < 0.01f) return;

            var glowGO = new GameObject("CategoryGlow");
            glowGO.transform.SetParent(building.transform, false);
            glowGO.transform.SetAsFirstSibling(); // Behind shadow cells
            var glowRect = glowGO.AddComponent<RectTransform>();
            // Wide and low — sits at building base
            glowRect.anchorMin = new Vector2(-0.20f, -0.15f);
            glowRect.anchorMax = new Vector2(1.20f, 0.35f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            var glowImg = glowGO.AddComponent<Image>();
            glowImg.color = glowColor;
            glowImg.raycastTarget = false;

            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (spr != null) glowImg.sprite = spr;
        }

        /// <summary>P&C IT102: Category color for building base glow aura.</summary>
        private static Color GetBuildingCategoryGlowColor(string buildingId) => buildingId switch
        {
            // Military — warm red
            "barracks" or "training_ground" or "armory" => new Color(0.80f, 0.25f, 0.20f, 0.12f),
            // Resource — green
            "grain_farm" or "iron_mine" or "stone_quarry" => new Color(0.25f, 0.70f, 0.30f, 0.12f),
            // Magic — purple
            "arcane_tower" or "enchanting_tower" or "observatory" or "laboratory" => new Color(0.55f, 0.30f, 0.80f, 0.12f),
            // Defense — blue
            "wall" or "watch_tower" => new Color(0.25f, 0.50f, 0.85f, 0.12f),
            // Social/trade — amber
            "marketplace" or "guild_hall" or "embassy" => new Color(0.85f, 0.65f, 0.20f, 0.12f),
            // Knowledge — cyan
            "academy" or "library" or "archive" => new Color(0.30f, 0.70f, 0.80f, 0.12f),
            // Sacred — white-gold
            "hero_shrine" => new Color(0.90f, 0.85f, 0.50f, 0.10f),
            // Forge — orange
            "forge" => new Color(0.90f, 0.50f, 0.15f, 0.12f),
            _ => new Color(0, 0, 0, 0) // No glow for unknown
        };

        /// <summary>P&C: Stronghold gets a special multi-layer golden glow aura that pulses.</summary>
        private void CreateStrongholdGlow(GameObject building)
        {
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif

            // Layer 1: Subtle outer warm glow (reduced — was covering whole building)
            var glow1 = new GameObject("StrongholdGlow_Outer");
            glow1.transform.SetParent(building.transform, false);
            glow1.transform.SetAsFirstSibling();
            var rect1 = glow1.AddComponent<RectTransform>();
            rect1.anchorMin = new Vector2(0.10f, 0.05f);
            rect1.anchorMax = new Vector2(0.90f, 0.70f);
            rect1.offsetMin = Vector2.zero;
            rect1.offsetMax = Vector2.zero;
            var img1 = glow1.AddComponent<Image>();
            img1.color = new Color(0.85f, 0.65f, 0.15f, 0.06f);
            img1.raycastTarget = false;
            if (spr != null) img1.sprite = spr;

            // Layer 2: Inner bright gold glow — just base area
            var glow2 = new GameObject("StrongholdGlow_Inner");
            glow2.transform.SetParent(building.transform, false);
            glow2.transform.SetSiblingIndex(1);
            var rect2 = glow2.AddComponent<RectTransform>();
            rect2.anchorMin = new Vector2(0.20f, 0.10f);
            rect2.anchorMax = new Vector2(0.80f, 0.55f);
            rect2.offsetMin = Vector2.zero;
            rect2.offsetMax = Vector2.zero;
            var img2 = glow2.AddComponent<Image>();
            img2.color = new Color(1f, 0.85f, 0.30f, 0.05f);
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

        /// <summary>P&C: Category-colored selection ring glow (matches header color but softer).</summary>
        private static Color GetCategorySelectionColor(string buildingId)
        {
            var hdr = GetCategoryHeaderColor(buildingId);
            return new Color(hdr.r, hdr.g, hdr.b, 0.45f);
        }

        // ====================================================================
        // P&C: Alliance help received visual
        // ====================================================================

        /// <summary>P&C: Show help-received indicator on a building (handshake icon + timer reduction text).</summary>
        private void ShowAllianceHelpReceived(string instanceId, int secondsReduced)
        {
            CityBuildingPlacement placement = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == instanceId) { placement = p; break; }
            }
            if (placement == null || placement.VisualGO == null) return;

            // Remove existing help indicator
            var existing = placement.VisualGO.transform.Find("HelpReceived");
            if (existing != null) Destroy(existing.gameObject);

            var helpGO = new GameObject("HelpReceived");
            helpGO.transform.SetParent(placement.VisualGO.transform, false);
            var rect = helpGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.70f);
            rect.anchorMax = new Vector2(0.90f, 0.90f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = helpGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.40f, 0.75f, 0.85f);
            bg.raycastTarget = false;
            var outline = helpGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.60f, 1f, 0.6f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(helpGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            string timeStr = FormatTimeRemaining(secondsReduced);
            text.text = $"\u2764 -{timeStr}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            // Auto-destroy after 3 seconds with fade
            StartCoroutine(FadeAndDestroyHelpIndicator(helpGO));
        }

        private IEnumerator FadeAndDestroyHelpIndicator(GameObject helpGO)
        {
            yield return new WaitForSeconds(2f);
            if (helpGO == null) yield break;
            var cg = helpGO.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            while (elapsed < 1f && helpGO != null)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - elapsed;
                yield return null;
            }
            if (helpGO != null) Destroy(helpGO);
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

        /// <summary>P&C: Countdown timer label showing seconds until next collectible bubble.</summary>
        private void CreateCollectCountdownLabel(GameObject parent, string buildingId)
        {
            if (!ResourceBuildingTypes.ContainsKey(buildingId)) return;

            var labelGO = new GameObject("CollectTimer");
            labelGO.transform.SetParent(parent.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.40f, 0.85f);
            labelRect.anchorMax = new Vector2(1.05f, 1.02f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var bgImg = labelGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.08f, 0.04f, 0.75f);
            bgImg.raycastTarget = false;
            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.70f, 0.35f, 0.50f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            var textGO = new GameObject("TimerText");
            textGO.transform.SetParent(labelGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = "";
            text.fontSize = 8;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.60f, 0.90f, 0.55f);
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }

        /// <summary>P&C: Periodically update countdown timer labels on all resource buildings.</summary>
        private IEnumerator UpdateResourceCountdownTimers()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                var spawner = FindObjectOfType<ResourceBubbleSpawner>();
                if (spawner != null)
                {
                    float secondsLeft = spawner.SecondsUntilNextSpawn;
                    foreach (var placement in _placements)
                    {
                        if (placement.VisualGO == null) continue;
                        if (!ResourceBuildingTypes.ContainsKey(placement.BuildingId)) continue;

                        var timerGO = placement.VisualGO.transform.Find("CollectTimer");
                        if (timerGO == null) continue;

                        var textComp = timerGO.GetComponentInChildren<Text>();
                        if (textComp == null) continue;

                        bool hasBubble = spawner.HasMaxBubbles(placement.InstanceId);
                        if (hasBubble)
                        {
                            textComp.text = "\u2B06 Collect!";
                            textComp.color = new Color(0.95f, 0.85f, 0.30f);
                            timerGO.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.02f, 0.80f);
                        }
                        else
                        {
                            int secs = Mathf.RoundToInt(secondsLeft);
                            textComp.text = $"\u23F1 {secs}s";
                            textComp.color = new Color(0.60f, 0.90f, 0.55f);
                            timerGO.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.04f, 0.75f);
                        }
                    }
                }
                yield return wait;
            }
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
            // Offset upward so the sprite's base sits on the diamond footprint
            // and the building "rises up" above its grid position
            float yOffset = screenSize.y * 0.15f;
            rect.anchoredPosition = center + new Vector2(0, yOffset);
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
                // Remove the y-offset that was added for building visual positioning
                float currentFootprintH = rect.sizeDelta.y;
                float estimatedYOffset = currentFootprintH * 0.15f;
                Vector2 gridCenter = screenPos - new Vector2(0, estimatedYOffset);
                // center = GridToLocalCenter(origin, size)
                // We solve: origin = LocalToGrid(gridCenter) adjusted for size offset
                float adjustedY = gridCenter.y + IsoCenterY;
                float gcx = (gridCenter.x / HalfW + adjustedY / HalfH) * 0.5f;
                float gcy = (adjustedY / HalfH - gridCenter.x / HalfW) * 0.5f;
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

                // Force correct sizing — override whatever the generator set
                PositionBuildingRect(rect, placement);

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

            // P&C: Screen flash overlay
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var flashGO = new GameObject("UpgradeFlash");
                flashGO.transform.SetParent(canvas.transform, false);
                flashGO.transform.SetAsLastSibling();
                var flashRect = flashGO.AddComponent<RectTransform>();
                flashRect.anchorMin = Vector2.zero;
                flashRect.anchorMax = Vector2.one;
                flashRect.offsetMin = Vector2.zero;
                flashRect.offsetMax = Vector2.zero;
                var flashImg = flashGO.AddComponent<Image>();
                flashImg.color = new Color(1f, 0.92f, 0.5f, 0.35f);
                flashImg.raycastTarget = false;

                // "LEVEL UP!" text
                var lvlGO = new GameObject("LevelUpText");
                lvlGO.transform.SetParent(flashGO.transform, false);
                var lvlRect = lvlGO.AddComponent<RectTransform>();
                lvlRect.anchorMin = new Vector2(0.15f, 0.40f);
                lvlRect.anchorMax = new Vector2(0.85f, 0.60f);
                lvlRect.offsetMin = Vector2.zero;
                lvlRect.offsetMax = Vector2.zero;
                var lvlText = lvlGO.AddComponent<Text>();
                lvlText.text = "LEVEL UP!";
                lvlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lvlText.fontSize = 28;
                lvlText.fontStyle = FontStyle.Bold;
                lvlText.alignment = TextAnchor.MiddleCenter;
                lvlText.color = new Color(1f, 0.90f, 0.30f);
                lvlText.raycastTarget = false;
                var lvlOutline = lvlGO.AddComponent<Outline>();
                lvlOutline.effectColor = new Color(0.4f, 0.2f, 0f, 0.9f);
                lvlOutline.effectDistance = new Vector2(2f, -2f);

                // Fade out flash + text over 0.6s
                float flashElapsed = 0f;
                float flashDur = 0.6f;
                var flashCG = flashGO.AddComponent<CanvasGroup>();
                while (flashElapsed < flashDur && flashGO != null)
                {
                    flashElapsed += Time.deltaTime;
                    float ft = flashElapsed / flashDur;
                    flashCG.alpha = 1f - ft;
                    // Scale text up slightly
                    lvlRect.localScale = Vector3.one * (1f + ft * 0.3f);
                    yield return null;
                }
                if (flashGO != null) Destroy(flashGO);
            }
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
                    // P&C IT101: Refresh advisor arrow (queue changed)
                    RefreshAdvisorArrow();
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

        // P&C: Collection streak tracking
        private int _collectStreak;
        private float _collectStreakTimer;
        private const float StreakWindow = 2.5f; // seconds to maintain streak

        private void OnResourceCollected(ResourceCollectedEvent evt)
        {
            PlaySfx(_sfxCollect);

            // P&C: Spawn fly-to-bar particle
            SpawnResourceFlyParticle(evt.Type, evt.BuildingInstanceId);

            // P&C: Collection streak tracking
            if (_collectStreakTimer > 0f)
            {
                _collectStreak++;
            }
            else
            {
                _collectStreak = 1;
            }
            _collectStreakTimer = StreakWindow;

            // P&C: Streak bonus — rapid collection gives % bonus
            long bonusAmount = 0;
            if (_collectStreak >= 5)
            {
                bonusAmount = (long)(evt.Amount * 0.30f);
                ShowStreakIndicator(_collectStreak, 30);
            }
            else if (_collectStreak >= 3)
            {
                bonusAmount = (long)(evt.Amount * 0.20f);
                ShowStreakIndicator(_collectStreak, 20);
            }
            else if (_collectStreak >= 2)
            {
                bonusAmount = (long)(evt.Amount * 0.10f);
                ShowStreakIndicator(_collectStreak, 10);
            }

            // Apply streak bonus
            if (bonusAmount > 0 && ServiceLocator.TryGet<ResourceManager>(out var rm))
                rm.AddResource(evt.Type, bonusAmount);

            // Start streak decay coroutine if not already running
            if (_collectStreak == 1)
                StartCoroutine(DecayCollectStreak());

            // Accumulate collections within a short window to batch into one toast
            if (_collectToastCooldown > 0f && _collectToastType == evt.Type)
            {
                _collectToastAccum += evt.Amount + bonusAmount;
            }
            else
            {
                _collectToastAccum = evt.Amount + bonusAmount;
                _collectToastType = evt.Type;
            }
            _collectToastCooldown = 0.8f; // Reset cooldown
            StartCoroutine(ShowCollectToastDelayed());

            // P&C: Vault overflow warning — check if resource is near cap after collection
            CheckVaultOverflowWarning(evt.Type);
        }

        // P&C: Vault overflow warning state
        private GameObject _vaultWarningPopup;
        private float _vaultWarningCooldown;
        private const float VaultWarningCooldownDuration = 30f; // Don't spam — once per 30s per type
        private ResourceType _lastVaultWarningType;

        private void CheckVaultOverflowWarning(ResourceType type)
        {
            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) return;
            if (_vaultWarningCooldown > 0f && _lastVaultWarningType == type) return;

            long current = type switch
            {
                ResourceType.Stone => rm.Stone,
                ResourceType.Iron => rm.Iron,
                ResourceType.Grain => rm.Grain,
                ResourceType.ArcaneEssence => rm.ArcaneEssence,
                _ => 0
            };
            long max = type switch
            {
                ResourceType.Stone => rm.MaxStone,
                ResourceType.Iron => rm.MaxIron,
                ResourceType.Grain => rm.MaxGrain,
                ResourceType.ArcaneEssence => rm.MaxArcaneEssence,
                _ => 0
            };

            if (max <= 0) return;
            float ratio = (float)current / max;

            if (ratio >= 0.95f)
            {
                _vaultWarningCooldown = VaultWarningCooldownDuration;
                _lastVaultWarningType = type;
                StartCoroutine(VaultWarningCooldownDecay());
                ShowVaultOverflowWarning(type, current, max, ratio >= 1f);
            }
        }

        private IEnumerator VaultWarningCooldownDecay()
        {
            while (_vaultWarningCooldown > 0f)
            {
                _vaultWarningCooldown -= Time.deltaTime;
                yield return null;
            }
        }

        private void ShowVaultOverflowWarning(ResourceType type, long current, long max, bool isFull)
        {
            if (_vaultWarningPopup != null) Destroy(_vaultWarningPopup);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            string resName = type switch
            {
                ResourceType.Stone => "Stone",
                ResourceType.Iron => "Iron",
                ResourceType.Grain => "Grain",
                ResourceType.ArcaneEssence => "Arcane Essence",
                _ => "Resource"
            };

            _vaultWarningPopup = new GameObject("VaultWarning");
            _vaultWarningPopup.transform.SetParent(canvas.transform, false);
            var rect = _vaultWarningPopup.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.40f);
            rect.anchorMax = new Vector2(0.92f, 0.60f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark bg with warning tint
            var bg = _vaultWarningPopup.AddComponent<Image>();
            Color bgColor = isFull
                ? new Color(0.45f, 0.12f, 0.10f, 0.95f)
                : new Color(0.40f, 0.30f, 0.08f, 0.95f);
            bg.color = bgColor;
            bg.raycastTarget = true;

            // Gold border
            var outline = _vaultWarningPopup.AddComponent<Outline>();
            outline.effectColor = isFull
                ? new Color(0.90f, 0.30f, 0.25f, 0.9f)
                : new Color(0.78f, 0.62f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Warning icon + title
            string title = isFull
                ? $"\u26A0 {resName} VAULT FULL!"
                : $"\u26A0 {resName} Vault Nearly Full";
            Color titleColor = isFull
                ? new Color(1f, 0.40f, 0.35f)
                : new Color(1f, 0.85f, 0.40f);
            AddInfoPanelText(_vaultWarningPopup.transform, "Title", title, 14, FontStyle.Bold,
                titleColor,
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);

            // Current/max display with fill bar
            string curStr = current >= 1000 ? $"{current / 1000f:F1}K" : $"{current}";
            string maxStr = max >= 1000 ? $"{max / 1000f:F1}K" : $"{max}";
            float pct = Mathf.Clamp01((float)current / max) * 100f;
            string body = isFull
                ? $"{curStr} / {maxStr} ({pct:F0}%) — Production is being WASTED!\nUpgrade vault or spend resources."
                : $"{curStr} / {maxStr} ({pct:F0}%) — Vault nearly full.\nCollect wisely or upgrade storage.";
            AddInfoPanelText(_vaultWarningPopup.transform, "Body", body, 11, FontStyle.Normal,
                new Color(0.90f, 0.88f, 0.82f),
                new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.55f), TextAnchor.MiddleCenter);

            // Fill bar visual
            var fillBgGO = new GameObject("FillBg");
            fillBgGO.transform.SetParent(_vaultWarningPopup.transform, false);
            var fillBgRect = fillBgGO.AddComponent<RectTransform>();
            fillBgRect.anchorMin = new Vector2(0.10f, 0.05f);
            fillBgRect.anchorMax = new Vector2(0.90f, 0.14f);
            fillBgRect.offsetMin = Vector2.zero;
            fillBgRect.offsetMax = Vector2.zero;
            var fillBgImg = fillBgGO.AddComponent<Image>();
            fillBgImg.color = new Color(0.08f, 0.06f, 0.10f, 0.9f);
            fillBgImg.raycastTarget = false;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillBgGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01((float)current / max), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = isFull
                ? new Color(0.90f, 0.25f, 0.20f, 0.9f)
                : new Color(0.85f, 0.70f, 0.20f, 0.9f);
            fillImg.raycastTarget = false;

            // Auto-dismiss after 4 seconds, or tap to dismiss
            var btn = _vaultWarningPopup.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => { if (_vaultWarningPopup != null) { Destroy(_vaultWarningPopup); _vaultWarningPopup = null; } });
            StartCoroutine(AutoDismissVaultWarning());
        }

        private IEnumerator AutoDismissVaultWarning()
        {
            yield return new WaitForSeconds(4f);
            if (_vaultWarningPopup != null)
            {
                // Fade out
                var cg = _vaultWarningPopup.AddComponent<CanvasGroup>();
                float t = 0f;
                while (t < 0.5f && _vaultWarningPopup != null)
                {
                    t += Time.deltaTime;
                    cg.alpha = 1f - (t / 0.5f);
                    yield return null;
                }
                if (_vaultWarningPopup != null) { Destroy(_vaultWarningPopup); _vaultWarningPopup = null; }
            }
        }

        private IEnumerator DecayCollectStreak()
        {
            while (_collectStreakTimer > 0f)
            {
                _collectStreakTimer -= Time.deltaTime;
                yield return null;
            }
            _collectStreak = 0;
        }

        /// <summary>P&C: Show "STREAK x3! +20%" floating text at center screen.</summary>
        private void ShowStreakIndicator(int streak, int bonusPct)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("StreakIndicator");
            go.transform.SetParent(canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.20f, 0.45f);
            rect.anchorMax = new Vector2(0.80f, 0.55f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = $"\u26A1 STREAK x{streak}! +{bonusPct}%";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = streak >= 5 ? 20 : 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            Color streakColor = streak >= 5
                ? new Color(1f, 0.45f, 0.15f) // Orange for high streak
                : streak >= 3
                    ? new Color(1f, 0.85f, 0.20f) // Gold
                    : new Color(0.50f, 0.90f, 0.50f); // Green
            text.color = streakColor;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            StartCoroutine(AnimateStreakIndicator(go, rect));
        }

        private IEnumerator AnimateStreakIndicator(GameObject go, RectTransform rect)
        {
            float duration = 1.2f;
            float elapsed = 0f;
            var text = go.GetComponent<Text>();
            Color startColor = text != null ? text.color : Color.white;

            // Start with punch scale
            go.transform.localScale = Vector3.one * 1.5f;

            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Scale: punch down to normal then slowly up
                float scale = t < 0.15f
                    ? Mathf.Lerp(1.5f, 1f, t / 0.15f)
                    : Mathf.Lerp(1f, 1.1f, (t - 0.15f) / 0.85f);
                go.transform.localScale = Vector3.one * scale;

                // Float upward
                rect.anchorMin += new Vector2(0f, Time.deltaTime * 0.02f);
                rect.anchorMax += new Vector2(0f, Time.deltaTime * 0.02f);

                // Fade out in last 40%
                if (t > 0.6f && text != null)
                {
                    float fade = 1f - (t - 0.6f) / 0.4f;
                    text.color = new Color(startColor.r, startColor.g, startColor.b, fade);
                }

                yield return null;
            }
            if (go != null) Destroy(go);
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
                    RemoveActiveBoostBadge(p.VisualGO);

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

                    // P&C IT100: Animate power score change
                    UpdatePowerRatingHUD();
                    AnimatePowerIncrease(p.BuildingId, evt.NewTier);

                    // P&C IT101: Full-screen reward summary screen
                    int oldTier = evt.NewTier - 1;
                    if (oldTier < 1) oldTier = 1;
                    ShowUpgradeRewardScreen(p.BuildingId, p.InstanceId, oldTier, evt.NewTier);

                    // P&C IT101: Refresh advisor arrow (may point to different building now)
                    RefreshAdvisorArrow();
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

        // ====================================================================
        // P&C: Speed-Up Item Selection (replaces gem-only speed-up)
        // ====================================================================

        private GameObject _speedUpItemPanel;

        /// <summary>P&C: Show speed-up item selection panel with tiered items (5m, 15m, 60m, 3h, 8h).</summary>
        private void ShowSpeedUpItemPanel(string instanceId, int remainingSeconds)
        {
            if (_speedUpItemPanel != null) { Destroy(_speedUpItemPanel); _speedUpItemPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _speedUpItemPanel = new GameObject("SpeedUpItemPanel");
            _speedUpItemPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _speedUpItemPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _speedUpItemPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _speedUpItemPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_speedUpItemPanel != null) { Destroy(_speedUpItemPanel); _speedUpItemPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_speedUpItemPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.22f);
            panelRect.anchorMax = new Vector2(0.92f, 0.78f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string timeStr = FormatTimeRemaining(remainingSeconds);
            AddInfoPanelText(panel.transform, "Title", "\u23F1 Use Speed-Up Items", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.88f), new Vector2(0.80f, 0.98f), TextAnchor.MiddleLeft);
            AddInfoPanelText(panel.transform, "Remaining",
                $"Time remaining: {timeStr}", 10, FontStyle.Normal,
                new Color(0.70f, 0.68f, 0.60f),
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.88f), TextAnchor.MiddleLeft);

            // Speed-up items
            var items = new[]
            {
                ("5 Minutes", 300, "\u23F1", 12, new Color(0.50f, 0.75f, 0.50f)),
                ("15 Minutes", 900, "\u23F2", 8, new Color(0.45f, 0.70f, 0.85f)),
                ("60 Minutes", 3600, "\u231A", 5, new Color(0.70f, 0.55f, 0.85f)),
                ("3 Hours", 10800, "\u2B50", 2, new Color(0.85f, 0.65f, 0.25f)),
                ("8 Hours", 28800, "\u26A1", 1, new Color(0.90f, 0.40f, 0.30f)),
            };

            float yPos = 0.76f;
            float rowH = 0.12f;
            foreach (var (name, seconds, icon, qty, tint) in items)
            {
                var rowGO = new GameObject($"Item_{name}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, yPos - rowH);
                rowRect.anchorMax = new Vector2(0.97f, yPos);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = new Color(tint.r * 0.12f, tint.g * 0.12f, tint.b * 0.12f, 0.70f);
                rowBg.raycastTarget = false;

                // Icon + Name
                AddInfoPanelText(rowGO.transform, "Icon", icon, 16, FontStyle.Normal, tint,
                    new Vector2(0.02f, 0.10f), new Vector2(0.10f, 0.90f), TextAnchor.MiddleCenter);
                AddInfoPanelText(rowGO.transform, "Name", name, 11, FontStyle.Bold,
                    new Color(0.90f, 0.85f, 0.75f),
                    new Vector2(0.12f, 0.50f), new Vector2(0.55f, 0.95f), TextAnchor.MiddleLeft);

                // Quantity owned
                AddInfoPanelText(rowGO.transform, "Qty", $"x{qty}", 10, FontStyle.Bold, tint,
                    new Vector2(0.55f, 0.50f), new Vector2(0.68f, 0.95f), TextAnchor.MiddleCenter);

                // Time reduction
                string reductionStr = FormatTimeRemaining(seconds);
                AddInfoPanelText(rowGO.transform, "Reduction", $"-{reductionStr}", 8, FontStyle.Normal,
                    new Color(0.65f, 0.60f, 0.55f),
                    new Vector2(0.12f, 0.05f), new Vector2(0.55f, 0.48f), TextAnchor.MiddleLeft);

                // Use button
                bool hasItems = qty > 0;
                var useBtnGO = new GameObject("UseBtn");
                useBtnGO.transform.SetParent(rowGO.transform, false);
                var useBtnRect = useBtnGO.AddComponent<RectTransform>();
                useBtnRect.anchorMin = new Vector2(0.72f, 0.12f);
                useBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                useBtnRect.offsetMin = Vector2.zero;
                useBtnRect.offsetMax = Vector2.zero;
                var useBtnBg = useBtnGO.AddComponent<Image>();
                useBtnBg.color = hasItems
                    ? new Color(0.15f, 0.55f, 0.25f, 0.92f)
                    : new Color(0.30f, 0.28f, 0.26f, 0.70f);
                useBtnBg.raycastTarget = true;
                var useBtnComp = useBtnGO.AddComponent<Button>();
                useBtnComp.targetGraphic = useBtnBg;
                string capName = name;
                int capSecs = seconds;
                string capInstId = instanceId;
                useBtnComp.onClick.AddListener(() =>
                {
                    if (!hasItems) { ShowUpgradeBlockedToast("No speed-up items available!"); return; }
                    Debug.Log($"[SpeedUp] Used {capName} (-{capSecs}s) on {capInstId}.");
                    if (_speedUpItemPanel != null) { Destroy(_speedUpItemPanel); _speedUpItemPanel = null; }
                    ShowUpgradeBlockedToast($"\u23F1 Used {capName} speed-up!");
                });
                AddInfoPanelText(useBtnGO.transform, "Label",
                    hasItems ? "USE" : "---", 9, FontStyle.Bold,
                    hasItems ? Color.white : new Color(0.50f, 0.45f, 0.40f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                yPos -= rowH + 0.015f;
            }

            // "Use All" button at bottom
            var useAllGO = new GameObject("UseAllBtn");
            useAllGO.transform.SetParent(panel.transform, false);
            var useAllRect = useAllGO.AddComponent<RectTransform>();
            useAllRect.anchorMin = new Vector2(0.25f, 0.02f);
            useAllRect.anchorMax = new Vector2(0.75f, 0.10f);
            useAllRect.offsetMin = Vector2.zero;
            useAllRect.offsetMax = Vector2.zero;
            var useAllBg = useAllGO.AddComponent<Image>();
            useAllBg.color = new Color(0.70f, 0.50f, 0.15f, 0.92f);
            useAllBg.raycastTarget = true;
            var useAllOutln = useAllGO.AddComponent<Outline>();
            useAllOutln.effectColor = new Color(0.95f, 0.75f, 0.25f, 0.5f);
            useAllOutln.effectDistance = new Vector2(0.5f, -0.5f);
            var useAllBtn = useAllGO.AddComponent<Button>();
            useAllBtn.targetGraphic = useAllBg;
            useAllBtn.onClick.AddListener(() =>
            {
                Debug.Log($"[SpeedUp] Used all available items on {instanceId}.");
                if (_speedUpItemPanel != null) { Destroy(_speedUpItemPanel); _speedUpItemPanel = null; }
                ShowUpgradeBlockedToast("\u23F1 Used all speed-up items!");
            });
            AddInfoPanelText(useAllGO.transform, "Label", "\u26A1 USE ALL", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Close
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
            closeBtn.onClick.AddListener(() => { if (_speedUpItemPanel != null) { Destroy(_speedUpItemPanel); _speedUpItemPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _speedUpItemPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Production Boost Activation Panel
        // ====================================================================

        private GameObject _boostPanel;

        /// <summary>P&C: Show production boost item panel for resource buildings.</summary>
        private void ShowProductionBoostPanel(string instanceId, string buildingId, int tier)
        {
            if (_boostPanel != null) { Destroy(_boostPanel); _boostPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _boostPanel = new GameObject("ProductionBoostPanel");
            _boostPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _boostPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _boostPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _boostPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_boostPanel != null) { Destroy(_boostPanel); _boostPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_boostPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.30f);
            panelRect.anchorMax = new Vector2(0.90f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.08f, 0.04f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.35f, 0.75f, 0.40f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            AddInfoPanelText(panel.transform, "Title",
                $"\u2191 Boost {bName} Production", 14, FontStyle.Bold,
                new Color(0.50f, 0.85f, 0.45f),
                new Vector2(0.05f, 0.85f), new Vector2(0.80f, 0.98f), TextAnchor.MiddleLeft);

            // Boost items
            var boosts = new[]
            {
                ("+25% for 1h", 0.25f, 3600, 5, new Color(0.45f, 0.75f, 0.45f)),
                ("+50% for 1h", 0.50f, 3600, 3, new Color(0.50f, 0.80f, 0.90f)),
                ("+100% for 30m", 1.00f, 1800, 1, new Color(0.85f, 0.65f, 0.25f)),
                ("+25% for 8h", 0.25f, 28800, 2, new Color(0.70f, 0.50f, 0.85f)),
            };

            float yPos = 0.80f;
            foreach (var (label, pct, durSec, qty, tint) in boosts)
            {
                var rowGO = new GameObject($"Boost_{label}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, yPos - 0.16f);
                rowRect.anchorMax = new Vector2(0.97f, yPos);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = new Color(tint.r * 0.15f, tint.g * 0.15f, tint.b * 0.15f, 0.70f);
                rowBg.raycastTarget = false;

                AddInfoPanelText(rowGO.transform, "Icon", "\u2191", 16, FontStyle.Bold, tint,
                    new Vector2(0.02f, 0.15f), new Vector2(0.10f, 0.85f), TextAnchor.MiddleCenter);
                AddInfoPanelText(rowGO.transform, "Name", label, 11, FontStyle.Bold,
                    new Color(0.90f, 0.85f, 0.75f),
                    new Vector2(0.12f, 0.45f), new Vector2(0.58f, 0.95f), TextAnchor.MiddleLeft);
                AddInfoPanelText(rowGO.transform, "Qty", $"x{qty}", 10, FontStyle.Bold, tint,
                    new Vector2(0.58f, 0.45f), new Vector2(0.70f, 0.95f), TextAnchor.MiddleCenter);

                string durStr = FormatTimeRemaining(durSec);
                AddInfoPanelText(rowGO.transform, "Dur", $"Duration: {durStr}", 8, FontStyle.Normal,
                    new Color(0.60f, 0.58f, 0.52f),
                    new Vector2(0.12f, 0.05f), new Vector2(0.58f, 0.42f), TextAnchor.MiddleLeft);

                bool hasItem = qty > 0;
                var actBtnGO = new GameObject("ActivateBtn");
                actBtnGO.transform.SetParent(rowGO.transform, false);
                var actBtnRect = actBtnGO.AddComponent<RectTransform>();
                actBtnRect.anchorMin = new Vector2(0.72f, 0.12f);
                actBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                actBtnRect.offsetMin = Vector2.zero;
                actBtnRect.offsetMax = Vector2.zero;
                var actBtnBg = actBtnGO.AddComponent<Image>();
                actBtnBg.color = hasItem
                    ? new Color(0.20f, 0.55f, 0.25f, 0.92f)
                    : new Color(0.30f, 0.28f, 0.26f, 0.70f);
                actBtnBg.raycastTarget = true;
                var actBtnComp = actBtnGO.AddComponent<Button>();
                actBtnComp.targetGraphic = actBtnBg;
                string capLabel = label;
                string capBid = buildingId;
                actBtnComp.onClick.AddListener(() =>
                {
                    if (!hasItem) { ShowUpgradeBlockedToast("No boost items available!"); return; }
                    Debug.Log($"[Boost] Activated {capLabel} on {capBid}.");
                    if (_boostPanel != null) { Destroy(_boostPanel); _boostPanel = null; }
                    ShowUpgradeBlockedToast($"\u2191 {capLabel} boost active on {bName}!");
                });
                AddInfoPanelText(actBtnGO.transform, "Label",
                    hasItem ? "ACTIVATE" : "---", 8, FontStyle.Bold,
                    hasItem ? Color.white : new Color(0.50f, 0.45f, 0.40f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                yPos -= 0.18f;
            }

            // Close
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
            closeBtn.onClick.AddListener(() => { if (_boostPanel != null) { Destroy(_boostPanel); _boostPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _boostPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Building Swap (drag one building onto another to swap positions)
        // ====================================================================

        private GameObject _swapConfirmDialog;

        /// <summary>P&C: Show swap confirmation dialog when two buildings are selected for swap.</summary>
        private void ShowBuildingSwapConfirm(CityBuildingPlacement buildingA, CityBuildingPlacement buildingB)
        {
            if (_swapConfirmDialog != null) { Destroy(_swapConfirmDialog); _swapConfirmDialog = null; }
            if (buildingA == null || buildingB == null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _swapConfirmDialog = new GameObject("SwapConfirmDialog");
            _swapConfirmDialog.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _swapConfirmDialog.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _swapConfirmDialog.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _swapConfirmDialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_swapConfirmDialog != null) { Destroy(_swapConfirmDialog); _swapConfirmDialog = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_swapConfirmDialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.32f);
            panelRect.anchorMax = new Vector2(0.90f, 0.68f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string nameA = BuildingDisplayNames.TryGetValue(buildingA.BuildingId, out var dnA) ? dnA : buildingA.BuildingId;
            string nameB = BuildingDisplayNames.TryGetValue(buildingB.BuildingId, out var dnB) ? dnB : buildingB.BuildingId;

            AddInfoPanelText(panel.transform, "Title", "\u21C4 Swap Buildings?", 15, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.10f, 0.82f), new Vector2(0.90f, 0.97f), TextAnchor.MiddleCenter);

            // Building A preview
            Sprite spriteA = LoadBuildingSprite(buildingA.BuildingId, buildingA.Tier);
            if (spriteA != null)
            {
                var goA = new GameObject("SpriteA");
                goA.transform.SetParent(panel.transform, false);
                var rA = goA.AddComponent<RectTransform>();
                rA.anchorMin = new Vector2(0.08f, 0.40f);
                rA.anchorMax = new Vector2(0.30f, 0.78f);
                rA.offsetMin = Vector2.zero;
                rA.offsetMax = Vector2.zero;
                var imgA = goA.AddComponent<Image>();
                imgA.sprite = spriteA;
                imgA.preserveAspect = true;
                imgA.raycastTarget = false;
            }
            AddInfoPanelText(panel.transform, "NameA", $"{nameA} Lv.{buildingA.Tier}", 10, FontStyle.Bold,
                new Color(0.85f, 0.80f, 0.70f),
                new Vector2(0.05f, 0.30f), new Vector2(0.35f, 0.40f), TextAnchor.MiddleCenter);

            // Arrow
            AddInfoPanelText(panel.transform, "Arrow", "\u21C4", 22, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.35f, 0.48f), new Vector2(0.65f, 0.68f), TextAnchor.MiddleCenter);

            // Building B preview
            Sprite spriteB = LoadBuildingSprite(buildingB.BuildingId, buildingB.Tier);
            if (spriteB != null)
            {
                var goB = new GameObject("SpriteB");
                goB.transform.SetParent(panel.transform, false);
                var rB = goB.AddComponent<RectTransform>();
                rB.anchorMin = new Vector2(0.70f, 0.40f);
                rB.anchorMax = new Vector2(0.92f, 0.78f);
                rB.offsetMin = Vector2.zero;
                rB.offsetMax = Vector2.zero;
                var imgB = goB.AddComponent<Image>();
                imgB.sprite = spriteB;
                imgB.preserveAspect = true;
                imgB.raycastTarget = false;
            }
            AddInfoPanelText(panel.transform, "NameB", $"{nameB} Lv.{buildingB.Tier}", 10, FontStyle.Bold,
                new Color(0.85f, 0.80f, 0.70f),
                new Vector2(0.65f, 0.30f), new Vector2(0.95f, 0.40f), TextAnchor.MiddleCenter);

            // Confirm button
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(panel.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.55f, 0.05f);
            confirmRect.anchorMax = new Vector2(0.95f, 0.22f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = new Color(0.15f, 0.60f, 0.25f, 0.92f);
            confirmBg.raycastTarget = true;
            var confirmOutln = confirmGO.AddComponent<Outline>();
            confirmOutln.effectColor = new Color(0.40f, 0.85f, 0.45f, 0.5f);
            confirmOutln.effectDistance = new Vector2(0.5f, -0.5f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmBg;
            var capA = buildingA;
            var capB = buildingB;
            confirmBtn.onClick.AddListener(() =>
            {
                ExecuteBuildingSwap(capA, capB);
                if (_swapConfirmDialog != null) { Destroy(_swapConfirmDialog); _swapConfirmDialog = null; }
            });
            AddInfoPanelText(confirmGO.transform, "Label", "\u21C4 SWAP", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Cancel
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(panel.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.05f, 0.05f);
            cancelRect.anchorMax = new Vector2(0.45f, 0.22f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelBg = cancelGO.AddComponent<Image>();
            cancelBg.color = new Color(0.35f, 0.25f, 0.25f, 0.85f);
            cancelBg.raycastTarget = true;
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelBg;
            cancelBtn.onClick.AddListener(() => { if (_swapConfirmDialog != null) { Destroy(_swapConfirmDialog); _swapConfirmDialog = null; } });
            AddInfoPanelText(cancelGO.transform, "Label", "Cancel", 12, FontStyle.Bold,
                new Color(0.80f, 0.70f, 0.60f),
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _swapConfirmDialog.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void ExecuteBuildingSwap(CityBuildingPlacement a, CityBuildingPlacement b)
        {
            // Swap grid positions
            Vector2Int tempOrigin = a.GridOrigin;
            a.GridOrigin = b.GridOrigin;
            b.GridOrigin = tempOrigin;

            // Update occupancy
            foreach (var kvp in new Dictionary<Vector2Int, string>(_occupancy))
            {
                if (kvp.Value == a.InstanceId)
                    _occupancy[kvp.Key] = "__swap_temp_a__";
                else if (kvp.Value == b.InstanceId)
                    _occupancy[kvp.Key] = "__swap_temp_b__";
            }
            foreach (var kvp in new Dictionary<Vector2Int, string>(_occupancy))
            {
                if (kvp.Value == "__swap_temp_a__")
                    _occupancy[kvp.Key] = a.InstanceId;
                else if (kvp.Value == "__swap_temp_b__")
                    _occupancy[kvp.Key] = b.InstanceId;
            }

            // Reposition visuals
            if (a.VisualGO != null)
            {
                var rectA = a.VisualGO.GetComponent<RectTransform>();
                if (rectA != null) PositionBuildingRect(rectA, a);
            }
            if (b.VisualGO != null)
            {
                var rectB = b.VisualGO.GetComponent<RectTransform>();
                if (rectB != null) PositionBuildingRect(rectB, b);
            }

            string nameA = BuildingDisplayNames.TryGetValue(a.BuildingId, out var dnA) ? dnA : a.BuildingId;
            string nameB = BuildingDisplayNames.TryGetValue(b.BuildingId, out var dnB) ? dnB : b.BuildingId;
            ShowUpgradeBlockedToast($"\u21C4 Swapped {nameA} \u2194 {nameB}");
            Debug.Log($"[Swap] Swapped {a.InstanceId} <-> {b.InstanceId}.");
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

            // P&C IT100: Alliance help count badge below indicator
            if (instanceId != null)
                AddAllianceHelpCountToIndicator(indicator, instanceId);

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

                // P&C: Completion soon badge (< 5 min remaining) — urgent pulsing red timer
                if (remaining <= 300f && remaining > 0f)
                {
                    var csb = indicator.transform.Find("CompletionSoonBadge");
                    if (csb == null)
                    {
                        CreateCompletionSoonBadge(indicator);
                    }
                    else
                    {
                        // Update the countdown text on the badge
                        var csbText = csb.GetComponentInChildren<Text>();
                        if (csbText != null)
                        {
                            int csbSecs = Mathf.CeilToInt(remaining);
                            csbText.text = csbSecs >= 60 ? $"{csbSecs / 60}:{csbSecs % 60:D2}" : $"{csbSecs}s";
                        }
                        // Pulse badge urgency (faster as it gets closer)
                        float urgency = 1f - (remaining / 300f); // 0..1
                        float pulseRate = 3f + urgency * 5f;
                        var csbImg = csb.GetComponent<Image>();
                        if (csbImg != null)
                            csbImg.color = Color.Lerp(
                                new Color(0.90f, 0.20f, 0.15f, 0.85f),
                                new Color(1f, 0.40f, 0.10f, 1f),
                                0.5f + 0.5f * Mathf.Sin(Time.time * pulseRate));
                    }
                }

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
                        AddActiveBoostBadge(p.VisualGO);
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
            // P&C: Also remove scaffolding overlay
            var scaffolding = building.transform.Find("Scaffolding");
            if (scaffolding != null)
                Destroy(scaffolding.gameObject);
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
            // P&C: Batch upgrade mode — select/deselect instead of showing popup
            if (_batchUpgradeMode)
            {
                BatchSelectBuilding(evt.InstanceId);
                return;
            }

            DismissInfoPopup();

            if (evt.VisualGO == null) return;

            // Don't show popup if we're in move mode
            if (_moveMode) return;

            _infoPopupInstanceId = evt.InstanceId;

            // P&C: Full-screen detail panel (covers ~80% of screen, leaves top bar visible)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Dim overlay behind panel (tapping dismisses)
            var dimOverlay = new GameObject("DimOverlay");
            dimOverlay.transform.SetParent(canvas.transform, false);
            dimOverlay.transform.SetAsLastSibling();
            var dimRect = dimOverlay.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = dimOverlay.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.50f);
            dimImg.raycastTarget = true;
            var dimBtn = dimOverlay.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissInfoPopup);

            var popup = new GameObject("InfoPopup_Panel");
            popup.transform.SetParent(dimOverlay.transform, false);

            var popupRect = popup.AddComponent<RectTransform>();
            // P&C: Tall panel — full width, from nav bar to just below resource bar
            popupRect.anchorMin = new Vector2(0.01f, 0.01f);
            popupRect.anchorMax = new Vector2(0.99f, 0.92f);
            popupRect.offsetMin = Vector2.zero;
            popupRect.offsetMax = Vector2.zero;

            // P&C: Dark panel background with ornate gold border
            var panelBg = popup.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.03f, 0.09f, 0.97f);
            panelBg.raycastTarget = true;
            var panelOutline = popup.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.80f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
            var panelOutline2 = popup.AddComponent<Outline>();
            panelOutline2.effectColor = new Color(0.40f, 0.30f, 0.10f, 0.40f);
            panelOutline2.effectDistance = new Vector2(-1f, 1f);

            // Store dimOverlay as the _infoPopup (destroying it will destroy the panel too)
            // We set _infoPopup later, but need dimOverlay reference for cleanup
            _infoPopupDimOverlay = dimOverlay;

            string displayName = BuildingDisplayNames.TryGetValue(evt.BuildingId, out var dn) ? dn : evt.BuildingId;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // P&C: Check upgrade state early (needed for next tier preview + level label)
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

            string ucostStr = GetUpgradeCostString(evt.InstanceId, evt.Tier);
            bool uIsMax = ucostStr == "MAX LEVEL";

            // ============================================
            // TOP ROW: Building image + name/level + close
            // ============================================

            // Building sprite (large, centered at top)
            Sprite bldSprite = LoadBuildingSprite(evt.BuildingId, evt.Tier);
            if (bldSprite != null)
            {
                var iconGO = new GameObject("BuildingIcon");
                iconGO.transform.SetParent(popup.transform, false);
                var iconRect = iconGO.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.30f, 0.62f);
                iconRect.anchorMax = new Vector2(0.70f, 0.92f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = bldSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                var iconOutline = iconGO.AddComponent<Outline>();
                iconOutline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.50f);
                iconOutline.effectDistance = new Vector2(1f, -1f);
            }

            // Next tier sprite preview (right side, if not max)
            Sprite nextSprite = uIsMax ? null : LoadBuildingSprite(evt.BuildingId, evt.Tier + 1);
            if (nextSprite != null)
            {
                // Arrow between current and next
                AddInfoPanelText(popup.transform, "UpgArrow", "\u2192", 20, FontStyle.Bold,
                    new Color(0.95f, 0.82f, 0.35f, 0.7f),
                    new Vector2(0.68f, 0.72f), new Vector2(0.78f, 0.82f), TextAnchor.MiddleCenter);
                var nextGO = new GameObject("NextTierIcon");
                nextGO.transform.SetParent(popup.transform, false);
                var nextRect = nextGO.AddComponent<RectTransform>();
                nextRect.anchorMin = new Vector2(0.76f, 0.65f);
                nextRect.anchorMax = new Vector2(0.96f, 0.90f);
                nextRect.offsetMin = Vector2.zero;
                nextRect.offsetMax = Vector2.zero;
                var nextImg = nextGO.AddComponent<Image>();
                nextImg.sprite = nextSprite;
                nextImg.preserveAspect = true;
                nextImg.raycastTarget = false;
                nextImg.color = new Color(1, 1, 1, 0.70f);
            }

            // Building name (bold, gold, prominent — centered below image)
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(popup.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.57f);
            nameRect.anchorMax = new Vector2(0.95f, 0.66f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = displayName;
            nameText.font = font;
            nameText.fontSize = 18;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = new Color(0.95f, 0.85f, 0.40f);
            nameText.raycastTarget = false;
            var nameOutline = nameGO.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(1f, -1f);

            // Level + status line (centered)
            var lvlGO = new GameObject("Level");
            lvlGO.transform.SetParent(popup.transform, false);
            var lvlRect = lvlGO.AddComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0.10f, 0.50f);
            lvlRect.anchorMax = new Vector2(0.90f, 0.57f);
            lvlRect.offsetMin = Vector2.zero;
            lvlRect.offsetMax = Vector2.zero;
            var lvlText = lvlGO.AddComponent<Text>();
            lvlText.font = font;
            lvlText.fontSize = 12;
            lvlText.alignment = TextAnchor.MiddleCenter;
            lvlText.raycastTarget = false;
            var lvlOutline = lvlGO.AddComponent<Outline>();
            lvlOutline.effectColor = new Color(0, 0, 0, 0.8f);
            lvlOutline.effectDistance = new Vector2(0.8f, -0.8f);

            Image progressFill = null; // reference for live update coroutine
            if (isUpgrading)
            {
                string timerStr = FormatTimeRemaining(Mathf.RoundToInt(upgradeRemaining));
                lvlText.text = $"Level {evt.Tier}  \u2692 Upgrading: {timerStr}";
                lvlText.color = new Color(0.90f, 0.75f, 0.30f);

                // P&C: Upgrade progress bar (below level text, above separator)
                float elapsed = (float)(System.DateTime.Now - System.DateTime.Now.AddSeconds(-upgradeRemaining)).TotalSeconds;
                float totalDuration = upgradeRemaining; // approximate — refine in coroutine
                // Get actual total from start time
                if (popupBm != null)
                {
                    foreach (var qe in popupBm.BuildQueue)
                    {
                        if (qe.PlacedId == evt.InstanceId)
                        {
                            elapsed = (float)(System.DateTime.Now - qe.StartTime).TotalSeconds;
                            totalDuration = elapsed + qe.RemainingSeconds;
                            break;
                        }
                    }
                }
                float progress = totalDuration > 0 ? Mathf.Clamp01(elapsed / totalDuration) : 0f;

                // Progress bar background
                var barBgGO = new GameObject("ProgressBarBg");
                barBgGO.transform.SetParent(popup.transform, false);
                var barBgRect = barBgGO.AddComponent<RectTransform>();
                barBgRect.anchorMin = new Vector2(0.05f, 0.49f);
                barBgRect.anchorMax = new Vector2(0.95f, 0.505f);
                barBgRect.offsetMin = Vector2.zero;
                barBgRect.offsetMax = Vector2.zero;
                var barBgImg = barBgGO.AddComponent<Image>();
                barBgImg.color = new Color(0.08f, 0.06f, 0.14f, 0.90f);
                barBgImg.raycastTarget = false;
                var barBgOutline = barBgGO.AddComponent<Outline>();
                barBgOutline.effectColor = new Color(0.40f, 0.30f, 0.12f, 0.50f);
                barBgOutline.effectDistance = new Vector2(0.5f, -0.5f);

                // Progress bar fill (amber/gold gradient)
                var barFillGO = new GameObject("ProgressBarFill");
                barFillGO.transform.SetParent(barBgGO.transform, false);
                var barFillRect = barFillGO.AddComponent<RectTransform>();
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = new Vector2(progress, 1f);
                barFillRect.offsetMin = Vector2.zero;
                barFillRect.offsetMax = Vector2.zero;
                progressFill = barFillGO.AddComponent<Image>();
                progressFill.color = new Color(0.85f, 0.65f, 0.15f, 0.95f);
                progressFill.raycastTarget = false;

                // Progress percentage text
                AddInfoPanelText(popup.transform, "ProgressPct",
                    $"{Mathf.RoundToInt(progress * 100)}%", 8, FontStyle.Bold,
                    new Color(0.95f, 0.88f, 0.55f),
                    new Vector2(0.40f, 0.49f), new Vector2(0.60f, 0.505f), TextAnchor.MiddleCenter);
            }
            else
            {
                lvlText.text = uIsMax ? $"Level {evt.Tier}  \u2605 MAX" : $"Level {evt.Tier}  \u2192 Level {evt.Tier + 1}";
                lvlText.color = uIsMax ? new Color(0.70f, 0.70f, 0.70f) : new Color(0.75f, 0.90f, 0.55f);
            }

            // Thin gold separator line
            var sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(popup.transform, false);
            var sepRect = sepGO.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.02f, 0.485f);
            sepRect.anchorMax = new Vector2(0.98f, 0.49f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = new Color(0.75f, 0.58f, 0.18f, 0.45f);
            sepImg.raycastTarget = false;

            // Close X button (top-right)
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(popup.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.90f, 0.93f);
            closeRect.anchorMax = new Vector2(0.99f, 0.99f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.50f, 0.15f, 0.12f, 0.85f);
            closeBg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(DismissInfoPopup);
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // ============================================
            // BONUS ROW: Building power/bonus description
            // ============================================
            string bonusDesc = GetBuildingBonusDescription(evt.BuildingId, evt.Tier);
            if (!string.IsNullOrEmpty(bonusDesc))
            {
                AddInfoPanelText(popup.transform, "BonusDesc", bonusDesc, 10, FontStyle.Normal,
                    new Color(0.70f, 0.78f, 0.90f), new Vector2(0.04f, 0.38f),
                    new Vector2(0.96f, 0.48f), TextAnchor.MiddleCenter);
            }

            // ============================================
            // COST ROW: Upgrade costs (if not max/upgrading)
            // ============================================
            if (!uIsMax && !isUpgrading)
            {
                ServiceLocator.TryGet<ResourceManager>(out var rm);
                // Show cost breakdown inline
                var costRow = new (string Sym, string Name, int Cost, long Have, Color Tint)[] {
                    ("\u25C8", "Stone", 0, rm != null ? rm.Stone : 0, new Color(0.70f, 0.70f, 0.75f)),
                    ("\u2666", "Iron", 0, rm != null ? rm.Iron : 0, new Color(0.80f, 0.65f, 0.50f)),
                    ("\u2740", "Grain", 0, rm != null ? rm.Grain : 0, new Color(0.65f, 0.85f, 0.45f)),
                    ("\u2726", "Arcane", 0, rm != null ? rm.ArcaneEssence : 0, new Color(0.60f, 0.50f, 0.90f)),
                };

                // Try get actual costs from BuildingManager
                if (popupBm != null && popupBm.PlacedBuildings.TryGetValue(evt.InstanceId, out var placed) && placed.Data != null)
                {
                    var tierData = placed.Data.GetTier(evt.Tier);
                    if (tierData != null)
                    {
                        costRow[0].Cost = tierData.stoneCost;
                        costRow[1].Cost = tierData.ironCost;
                        costRow[2].Cost = tierData.grainCost;
                        costRow[3].Cost = tierData.arcaneEssenceCost;
                    }
                }

                int visibleCosts = 0;
                foreach (var c in costRow) if (c.Cost > 0) visibleCosts++;

                if (visibleCosts > 0)
                {
                    float costX = 0.04f;
                    float costW = 0.92f / visibleCosts;
                    foreach (var (sym, resName, cost, have, tint) in costRow)
                    {
                        if (cost <= 0) continue;
                        bool canAfford = have >= cost;
                        Color valColor = canAfford ? new Color(0.50f, 0.90f, 0.50f) : new Color(0.95f, 0.40f, 0.35f);

                        AddInfoPanelText(popup.transform, $"Cost_{resName}",
                            $"{sym} {FormatCost(cost)}", 11, FontStyle.Bold, valColor,
                            new Vector2(costX, 0.37f), new Vector2(costX + costW - 0.01f, 0.47f),
                            TextAnchor.MiddleCenter);
                        costX += costW;
                    }
                }
            }

            // ============================================
            // PREREQ + POWER: Upgrade requirements and power contribution
            // ============================================
            if (!uIsMax && !isUpgrading)
            {
                string blockReason = GetUpgradeBlockReason(evt.InstanceId, evt.Tier);
                if (blockReason != null)
                {
                    AddInfoPanelText(popup.transform, "PrereqWarning",
                        $"\u26D4 {blockReason}", 9, FontStyle.Bold,
                        new Color(0.95f, 0.40f, 0.35f),
                        new Vector2(0.04f, 0.33f), new Vector2(0.96f, 0.37f), TextAnchor.MiddleCenter);
                }
            }
            // Power contribution line
            int powerContrib = GetBuildingPowerContribution(evt.BuildingId, evt.Tier);
            if (powerContrib > 0)
            {
                AddInfoPanelText(popup.transform, "PowerContrib",
                    $"\u2694 Power: +{powerContrib}", 9, FontStyle.Normal,
                    new Color(0.75f, 0.65f, 0.90f),
                    new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.33f), TextAnchor.MiddleCenter);
            }

            // P&C IT100: Construction speed bonus display
            if (isUpgrading)
                AddConstructionSpeedBonusDisplay(popup.transform, evt.InstanceId);

            // P&C IT100: Prerequisite chain visualization (check/cross marks)
            if (!uIsMax && !isUpgrading)
                AddPrerequisiteChainDisplay(popup.transform, evt.BuildingId, evt.InstanceId, evt.Tier);

            // ============================================
            // BOTTOM ROW: Action buttons (full width)
            // ============================================
            string upgInstanceId = evt.InstanceId;
            string upgBuildingId = evt.BuildingId;
            int upgTier = evt.Tier;
            string capBuildingId = evt.BuildingId;
            string capInstanceId = evt.InstanceId;
            int capTier = evt.Tier;

            var actions = new List<(string Icon, string Label, Color BgColor, System.Action OnClick)>();

            // Upgrade button (green, prominent) + Alliance Help when upgrading
            if (isUpgrading)
            {
                actions.Add(("\u2692", "Speed Up", new Color(0.55f, 0.45f, 0.15f, 0.95f), null));
                // P&C: Alliance Help button — request help from alliance to reduce timer
                actions.Add(("\u2764", "Help", new Color(0.25f, 0.50f, 0.70f, 0.95f), () => {
                    DismissInfoPopup();
                    EventBus.Publish(new AllianceHelpRequestEvent(upgInstanceId));
                }));
            }
            else
            {
                actions.Add(("\u2B06", uIsMax ? "MAX" : "Upgrade",
                    uIsMax ? new Color(0.30f, 0.30f, 0.30f, 0.80f) : new Color(0.12f, 0.55f, 0.18f, 0.95f),
                    uIsMax ? (System.Action)null : () => {
                        string warning = GetUpgradeBlockReason(upgInstanceId, upgTier);
                        if (warning != null) { ShowUpgradeBlockedToast(warning); return; }
                        DismissInfoPopup();
                        ShowUpgradeConfirmDialog(upgInstanceId, upgBuildingId, upgTier);
                    }));
            }

            // P&C: Context-specific action based on building type
            string ctxBid = evt.BuildingId;
            string ctxIid = evt.InstanceId;
            int ctxTier = evt.Tier;
            switch (evt.BuildingId)
            {
                case "barracks":
                case "training_ground":
                    actions.Add(("\u2694", "Train", new Color(0.60f, 0.25f, 0.15f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
                case "academy":
                case "library":
                case "archive":
                case "observatory":
                case "laboratory":
                    actions.Add(("\u2697", "Research", new Color(0.25f, 0.40f, 0.70f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
                case "forge":
                case "armory":
                    actions.Add(("\u2692", "Craft", new Color(0.55f, 0.40f, 0.20f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
                case "hero_shrine":
                    actions.Add(("\u2605", "Heroes", new Color(0.55f, 0.35f, 0.65f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
                case "marketplace":
                    actions.Add(("\u2696", "Trade", new Color(0.30f, 0.55f, 0.30f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
                case "embassy":
                    actions.Add(("\u2709", "Diplomacy", new Color(0.30f, 0.45f, 0.60f, 0.95f), () => {
                        DismissInfoPopup();
                        ShowBuildingInfoPanel(ctxBid, ctxIid, ctxTier);
                    }));
                    break;
            }

            // Info
            actions.Add(("\u2139", "Info", new Color(0.15f, 0.30f, 0.60f, 0.95f), () => {
                DismissInfoPopup();
                ShowBuildingInfoPanel(capBuildingId, capInstanceId, capTier);
            }));

            // Move
            actions.Add(("\u2725", "Move", new Color(0.50f, 0.38f, 0.12f, 0.95f), () => {
                DismissInfoPopup();
                EnterMoveModeForBuilding(evt.InstanceId);
            }));

            // Demolish (not stronghold)
            if (evt.BuildingId != "stronghold")
            {
                string demInstanceId = evt.InstanceId;
                string demBuildingId = evt.BuildingId;
                actions.Add(("\u2716", "Remove", new Color(0.60f, 0.15f, 0.12f, 0.95f), () => {
                    DismissInfoPopup();
                    ShowDemolishConfirmDialog(demInstanceId, demBuildingId);
                }));
            }

            // P&C: Horizontal action button row across full bottom
            float abtnW = 0.96f / actions.Count;
            for (int i = 0; i < actions.Count; i++)
            {
                var (icon, label, bgColor, onClick) = actions[i];
                float x0 = 0.02f + i * abtnW;
                float x1 = x0 + abtnW - 0.01f;
                bool isUpgrade = i == 0;

                var btnGO = new GameObject($"ActionBtn_{i}");
                btnGO.transform.SetParent(popup.transform, false);
                var btnRect = btnGO.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(x0, 0.03f);
                btnRect.anchorMax = new Vector2(x1, isUpgrade ? 0.35f : 0.32f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                var btnBg = btnGO.AddComponent<Image>();
                btnBg.color = bgColor;
                btnBg.raycastTarget = onClick != null;

                var btnOutline = btnGO.AddComponent<Outline>();
                btnOutline.effectColor = new Color(0.85f, 0.68f, 0.20f, isUpgrade ? 0.65f : 0.40f);
                btnOutline.effectDistance = new Vector2(1f, -1f);

                if (onClick != null)
                {
                    var btn = btnGO.AddComponent<Button>();
                    btn.targetGraphic = btnBg;
                    var cb = onClick;
                    btn.onClick.AddListener(() => cb());
                }

                // Icon
                AddInfoPanelText(btnGO.transform, "Icon", icon,
                    isUpgrade ? 18 : 14, FontStyle.Bold, Color.white,
                    new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);
                // Label
                AddInfoPanelText(btnGO.transform, "Label", label,
                    9, FontStyle.Bold, new Color(0.92f, 0.90f, 0.84f),
                    new Vector2(0, 0.02f), new Vector2(1, 0.38f), TextAnchor.MiddleCenter);
            }

            // Upgrading? Start live timer on the level label
            if (isUpgrading)
            {
                string timerInstanceId = evt.InstanceId;
                StartCoroutine(UpdatePopupTimer(lvlText, timerInstanceId, popup, progressFill));
            }

            // P&C IT99: Navigation arrows for cycling between buildings
            AddDetailPanelNavigation(popup, evt.InstanceId);

            // P&C: Slide-up animation
            var cg = popup.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _infoPopup = popup;
            StartCoroutine(AnimateBottomSheetIn(popup, popupRect, cg));

            // P&C: No auto-dismiss on detail panel (user closes manually or taps elsewhere)
            if (_popupAutoDismiss != null) StopCoroutine(_popupAutoDismiss);
            _popupAutoDismiss = null;
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

            // P&C: Circular background with radial gradient sprite
            var bg = btnGO.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = true;
            var radialSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            if (radialSpr != null) { bg.sprite = radialSpr; bg.type = Image.Type.Simple; }

            // Gold ring border
            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.68f, 0.20f, 0.80f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
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

        /// <summary>P&C: Animate bottom sheet sliding up from below the screen.</summary>
        private IEnumerator AnimateBottomSheetIn(GameObject popup, RectTransform rect, CanvasGroup cg)
        {
            float duration = 0.25f;
            float elapsed = 0f;
            // Store target anchors
            Vector2 targetMin = rect.anchorMin;
            Vector2 targetMax = rect.anchorMax;
            float sheetHeight = targetMax.y - targetMin.y;
            // Start below screen
            rect.anchorMin = new Vector2(targetMin.x, targetMin.y - sheetHeight);
            rect.anchorMax = new Vector2(targetMax.x, targetMax.y - sheetHeight);
            while (elapsed < duration)
            {
                if (popup == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out cubic for smooth deceleration
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                cg.alpha = Mathf.Clamp01(t * 3f);
                rect.anchorMin = Vector2.Lerp(
                    new Vector2(targetMin.x, targetMin.y - sheetHeight), targetMin, ease);
                rect.anchorMax = Vector2.Lerp(
                    new Vector2(targetMax.x, targetMax.y - sheetHeight), targetMax, ease);
                yield return null;
            }
            cg.alpha = 1f;
            rect.anchorMin = targetMin;
            rect.anchorMax = targetMax;
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
        private IEnumerator UpdatePopupTimer(Text timerText, string instanceId, GameObject popup, Image progressFill = null)
        {
            while (timerText != null && popup != null)
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                {
                    float remaining = -1f;
                    float elapsed = 0f;
                    float total = 1f;
                    foreach (var qe in bm.BuildQueue)
                    {
                        if (qe.PlacedId == instanceId)
                        {
                            remaining = qe.RemainingSeconds;
                            elapsed = (float)(System.DateTime.Now - qe.StartTime).TotalSeconds;
                            total = elapsed + remaining;
                            break;
                        }
                    }
                    if (remaining < 0)
                    {
                        timerText.text = "\u2714 Complete!";
                        timerText.color = new Color(0.3f, 1f, 0.4f);
                        // Fill progress bar to 100%
                        if (progressFill != null)
                        {
                            var fillRect = progressFill.GetComponent<RectTransform>();
                            if (fillRect != null) fillRect.anchorMax = new Vector2(1f, 1f);
                            progressFill.color = new Color(0.40f, 0.90f, 0.40f, 0.95f);
                        }
                        yield break;
                    }
                    timerText.text = "\u2692 " + FormatTimeRemaining(Mathf.RoundToInt(remaining));

                    // P&C: Update progress bar fill
                    if (progressFill != null)
                    {
                        float progress = total > 0 ? Mathf.Clamp01(elapsed / total) : 0f;
                        var fillRect = progressFill.GetComponent<RectTransform>();
                        if (fillRect != null)
                            fillRect.anchorMax = new Vector2(progress, 1f);
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void DismissInfoPopup()
        {
            if (_popupAutoDismiss != null) { StopCoroutine(_popupAutoDismiss); _popupAutoDismiss = null; }
            // P&C: Dim overlay is the root — destroying it removes panel too
            if (_infoPopupDimOverlay != null)
            {
                Destroy(_infoPopupDimOverlay);
                _infoPopupDimOverlay = null;
                _infoPopup = null;
            }
            else if (_infoPopup != null)
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
        private int _infoPanelActiveTab; // 0=Overview, 1=Production, 2=Boosts

        private void ShowBuildingInfoPanel(string buildingId, string instanceId, int tier)
        {
            ShowBuildingInfoPanel(buildingId, instanceId, tier, 0);
        }

        private void ShowBuildingInfoPanel(string buildingId, string instanceId, int tier, int activeTab)
        {
            DismissBuildingInfoPanel();
            _infoPanelActiveTab = activeTab;

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
                new Vector2(0.05f, 0.90f), new Vector2(0.70f, 0.97f), TextAnchor.MiddleLeft);

            // Level display
            string tierStar = tier > 1 ? $" \u2605" : "";
            AddInfoPanelText(panel.transform, "Level", $"Level {tier}{tierStar}", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.72f, 0.90f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleRight);

            // P&C: Tab bar (Overview | Production | Boosts)
            string[] tabNames = { "Overview", "Production", "Boosts" };
            string[] tabIcons = { "\u2139", "\u2609", "\u26A1" };
            float tabW = 0.30f;
            float tabStartX = 0.05f;
            for (int ti = 0; ti < 3; ti++)
            {
                int tabIdx = ti;
                float x0 = tabStartX + ti * tabW;
                float x1 = x0 + tabW - 0.01f;
                var tabGO = new GameObject($"Tab_{tabNames[ti]}");
                tabGO.transform.SetParent(panel.transform, false);
                var tabRect = tabGO.AddComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(x0, 0.845f);
                tabRect.anchorMax = new Vector2(x1, 0.895f);
                tabRect.offsetMin = Vector2.zero;
                tabRect.offsetMax = Vector2.zero;
                var tabImg = tabGO.AddComponent<Image>();
                bool isActive = ti == activeTab;
                tabImg.color = isActive
                    ? new Color(0.25f, 0.20f, 0.40f, 0.95f)
                    : new Color(0.10f, 0.08f, 0.16f, 0.80f);
                tabImg.raycastTarget = true;
                if (isActive)
                {
                    var tabOutline = tabGO.AddComponent<Outline>();
                    tabOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.6f);
                    tabOutline.effectDistance = new Vector2(0.8f, -0.8f);
                }
                var tabBtnComp = tabGO.AddComponent<Button>();
                tabBtnComp.targetGraphic = tabImg;
                string capBId = buildingId;
                string capIId = instanceId;
                int capTier = tier;
                tabBtnComp.onClick.AddListener(() => ShowBuildingInfoPanel(capBId, capIId, capTier, tabIdx));
                Color tabTextColor = isActive ? new Color(0.95f, 0.82f, 0.35f) : new Color(0.60f, 0.58f, 0.55f);
                AddInfoPanelText(tabGO.transform, "Label", $"{tabIcons[ti]} {tabNames[ti]}", 10, FontStyle.Bold,
                    tabTextColor, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // ─── TAB CONTENT AREA ───
            if (activeTab == 1)
            {
                BuildInfoPanelProductionTab(panel, buildingId, instanceId, tier);
            }
            else if (activeTab == 2)
            {
                BuildInfoPanelBoostsTab(panel, buildingId, instanceId, tier);
            }
            else
            {
                BuildInfoPanelOverviewTab(panel, buildingId, instanceId, tier);
            }

            // Close button (X in top-right) — shared across all tabs
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect2 = closeGO.AddComponent<RectTransform>();
            closeRect2.anchorMin = new Vector2(0.88f, 0.90f);
            closeRect2.anchorMax = new Vector2(0.98f, 1.0f);
            closeRect2.offsetMin = Vector2.zero;
            closeRect2.offsetMax = Vector2.zero;
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

        private void BuildInfoPanelOverviewTab(GameObject panel, string buildingId, string instanceId, int tier)
        {
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

            // P&C: Building count/limit (e.g., "3/5 Farms")
            if (MaxBuildingCountPerType.TryGetValue(buildingId, out int maxAllowed) && maxAllowed > 1)
            {
                int ownedCount = 0;
                foreach (var p in _placements) { if (p.BuildingId == buildingId) ownedCount++; }
                Color countColor = ownedCount >= maxAllowed
                    ? new Color(0.85f, 0.45f, 0.35f) : new Color(0.75f, 0.80f, 0.85f);
                AddInfoPanelText(panel.transform, "BuildCount",
                    $"Owned: {ownedCount}/{maxAllowed}", 10, FontStyle.Normal, countColor,
                    new Vector2(0.55f, statsY - 0.02f), new Vector2(0.95f, statsY + 0.04f), TextAnchor.MiddleRight);
            }

            // P&C: HP/DEF/Garrison stats for defensive buildings
            if (DefenseStats.ContainsKey(buildingId))
            {
                statsY -= 0.01f;
                AddDefenseStatsToInfoPanel(panel.transform, buildingId, tier, ref statsY);
            }

            // P&C: Visual stat comparison bars (current tier vs next tier)
            if (tier < 3)
            {
                int nextT = tier + 1;
                statsY -= 0.065f;
                var barStats = GetStatComparisonData(buildingId, tier, nextT);
                foreach (var stat in barStats)
                {
                    AddStatComparisonBar(panel.transform, stat.Label, stat.CurrentVal, stat.NextVal, stat.MaxVal,
                        stat.BarColor, statsY);
                    statsY -= 0.055f;
                }
                if (barStats.Count > 0) statsY -= 0.01f;
            }

            // Resource production info (if applicable) — P&C-style hourly + daily forecast
            if (ResourceBuildingTypes.TryGetValue(buildingId, out var resInfo))
            {
                int rate = (tier + 1) * 250;
                string rateText = rate >= 1000 ? $"{rate / 1000f:F1}K" : $"{rate}";

                // Tappable production rate — opens resource breakdown popup
                var prodRowGO = new GameObject("ProdRateBtn");
                prodRowGO.transform.SetParent(panel.transform, false);
                var prodRowRect = prodRowGO.AddComponent<RectTransform>();
                prodRowRect.anchorMin = new Vector2(0.05f, statsY - 0.10f);
                prodRowRect.anchorMax = new Vector2(0.95f, statsY - 0.03f);
                prodRowRect.offsetMin = Vector2.zero;
                prodRowRect.offsetMax = Vector2.zero;
                var prodRowImg = prodRowGO.AddComponent<Image>();
                prodRowImg.color = new Color(resInfo.Tint.r, resInfo.Tint.g, resInfo.Tint.b, 0.08f);
                prodRowImg.raycastTarget = true;
                var prodRowBtn = prodRowGO.AddComponent<Button>();
                prodRowBtn.targetGraphic = prodRowImg;
                string capturedResName = resInfo.Name;
                prodRowBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowResourceBreakdownPopup(capturedResName);
                });
                AddInfoPanelText(prodRowGO.transform, "Text",
                    $"Production: +{rateText}/hr ({resInfo.Name})  \u25B6", 11, FontStyle.Normal, resInfo.Tint,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);

                statsY -= 0.07f;

                // P&C: Daily forecast + vault fill estimate
                int dailyRate = rate * 24;
                string dailyText = dailyRate >= 1_000_000 ? $"{dailyRate / 1_000_000f:F1}M"
                    : dailyRate >= 1000 ? $"{dailyRate / 1000f:F1}K" : $"{dailyRate}";
                // Vault fill time estimate
                long vaultCap = 0;
                if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                {
                    vaultCap = resInfo.Name switch
                    {
                        "Stone" => rm.MaxStone,
                        "Iron" => rm.MaxIron,
                        "Grain" => rm.MaxGrain,
                        "Arcane" => rm.MaxArcaneEssence,
                        _ => 0
                    };
                }
                string fillStr = vaultCap > 0 && rate > 0
                    ? $" | Vault full in {FormatTimeRemaining((int)((vaultCap - 0) / (rate / 3600f)))}"
                    : "";
                AddInfoPanelText(panel.transform, "DailyForecast",
                    $"\u2609 Daily: +{dailyText}{fillStr}", 10, FontStyle.Normal,
                    new Color(resInfo.Tint.r * 0.8f, resInfo.Tint.g * 0.8f, resInfo.Tint.b * 0.8f),
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

            // P&C: Troop training quick-start for military buildings
            if (buildingId == "barracks" || buildingId == "training_ground")
            {
                int troopCap = buildingId == "barracks" ? (tier + 1) * 500 : (tier + 1) * 300;
                string troopType = buildingId == "barracks" ? "Infantry" : "Specialists";
                int trainTime = buildingId == "barracks" ? 30 * (tier + 1) : 45 * (tier + 1);
                string timeStr = FormatTimeRemaining(trainTime);

                var trainGO = new GameObject("TrainBtn");
                trainGO.transform.SetParent(panel.transform, false);
                var trainRect = trainGO.AddComponent<RectTransform>();
                trainRect.anchorMin = new Vector2(0.50f, 0.20f);
                trainRect.anchorMax = new Vector2(0.95f, 0.27f);
                trainRect.offsetMin = Vector2.zero;
                trainRect.offsetMax = Vector2.zero;
                var trainBg = trainGO.AddComponent<Image>();
                trainBg.color = new Color(0.55f, 0.18f, 0.15f, 0.90f);
                trainBg.raycastTarget = true;
                var trainOutline = trainGO.AddComponent<Outline>();
                trainOutline.effectColor = new Color(0.90f, 0.40f, 0.30f, 0.6f);
                trainOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var trainBtn = trainGO.AddComponent<Button>();
                trainBtn.targetGraphic = trainBg;
                string capInstId = instanceId;
                string capBldId = buildingId;
                int capTierLocal = tier;
                trainBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowTroopTrainingPanel(capInstId, capBldId, capTierLocal);
                });
                AddInfoPanelText(trainGO.transform, "Label",
                    $"\u2694 Train {troopType}", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Research quick-access for academy/library
            if (buildingId == "academy" || buildingId == "library")
            {
                var resGO = new GameObject("ResearchBtn");
                resGO.transform.SetParent(panel.transform, false);
                var resRect = resGO.AddComponent<RectTransform>();
                resRect.anchorMin = new Vector2(0.50f, 0.20f);
                resRect.anchorMax = new Vector2(0.95f, 0.27f);
                resRect.offsetMin = Vector2.zero;
                resRect.offsetMax = Vector2.zero;
                var resBg = resGO.AddComponent<Image>();
                resBg.color = new Color(0.15f, 0.35f, 0.60f, 0.90f);
                resBg.raycastTarget = true;
                var resOutline = resGO.AddComponent<Outline>();
                resOutline.effectColor = new Color(0.35f, 0.65f, 0.90f, 0.6f);
                resOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var resBtn = resGO.AddComponent<Button>();
                resBtn.targetGraphic = resBg;
                int capAcademyTier = tier;
                resBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowResearchQuickPanel(capAcademyTier);
                });
                AddInfoPanelText(resGO.transform, "Label", "\u2726 Research", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Crafting shortcut for forge/enchanting tower
            if (buildingId == "forge" || buildingId == "enchanting_tower")
            {
                var craftGO = new GameObject("CraftBtn");
                craftGO.transform.SetParent(panel.transform, false);
                var craftRect = craftGO.AddComponent<RectTransform>();
                craftRect.anchorMin = new Vector2(0.50f, 0.20f);
                craftRect.anchorMax = new Vector2(0.95f, 0.27f);
                craftRect.offsetMin = Vector2.zero;
                craftRect.offsetMax = Vector2.zero;
                var craftBg = craftGO.AddComponent<Image>();
                craftBg.color = new Color(0.55f, 0.35f, 0.15f, 0.90f);
                craftBg.raycastTarget = true;
                var craftOutline = craftGO.AddComponent<Outline>();
                craftOutline.effectColor = new Color(0.85f, 0.55f, 0.25f, 0.6f);
                craftOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var craftBtn = craftGO.AddComponent<Button>();
                craftBtn.targetGraphic = craftBg;
                string capCraftBid = buildingId;
                int capCraftTier = tier;
                craftBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowCraftingPanel(capCraftBid, capCraftTier);
                });
                string craftLabel = buildingId == "forge" ? "\u2692 Craft Equipment" : "\u2728 Enchant";
                AddInfoPanelText(craftGO.transform, "Label", craftLabel, 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Garrison quick-deploy for defensive buildings
            if (buildingId == "wall" || buildingId == "watch_tower" || buildingId == "stronghold")
            {
                var garrGO = new GameObject("GarrisonBtn");
                garrGO.transform.SetParent(panel.transform, false);
                var garrRect = garrGO.AddComponent<RectTransform>();
                garrRect.anchorMin = new Vector2(0.50f, 0.20f);
                garrRect.anchorMax = new Vector2(0.95f, 0.27f);
                garrRect.offsetMin = Vector2.zero;
                garrRect.offsetMax = Vector2.zero;
                var garrBg = garrGO.AddComponent<Image>();
                garrBg.color = new Color(0.50f, 0.20f, 0.20f, 0.90f);
                garrBg.raycastTarget = true;
                var garrOutline = garrGO.AddComponent<Outline>();
                garrOutline.effectColor = new Color(0.85f, 0.40f, 0.30f, 0.6f);
                garrOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var garrBtn = garrGO.AddComponent<Button>();
                garrBtn.targetGraphic = garrBg;
                string capGarrInstId = instanceId;
                string capGarrBid = buildingId;
                int capGarrTier = tier;
                garrBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowGarrisonDeployPanel(capGarrInstId, capGarrBid, capGarrTier);
                });
                AddInfoPanelText(garrGO.transform, "Label", "\u26E8 Garrison", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Troop training button for barracks/training_ground
            if (buildingId == "barracks" || buildingId == "training_ground")
            {
                var trainGO = new GameObject("TrainTroopsBtn");
                trainGO.transform.SetParent(panel.transform, false);
                var trainRect = trainGO.AddComponent<RectTransform>();
                trainRect.anchorMin = new Vector2(0.52f, 0.28f);
                trainRect.anchorMax = new Vector2(0.95f, 0.35f);
                trainRect.offsetMin = Vector2.zero;
                trainRect.offsetMax = Vector2.zero;
                var trainBg = trainGO.AddComponent<Image>();
                trainBg.color = new Color(0.20f, 0.40f, 0.55f, 0.90f);
                trainBg.raycastTarget = true;
                var trainOutline = trainGO.AddComponent<Outline>();
                trainOutline.effectColor = new Color(0.40f, 0.70f, 0.90f, 0.6f);
                trainOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var trainBtn = trainGO.AddComponent<Button>();
                trainBtn.targetGraphic = trainBg;
                string capTrainInstId = instanceId;
                string capTrainBid = buildingId;
                int capTrainTier = tier;
                trainBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowTroopTrainingPanel(capTrainInstId, capTrainBid, capTrainTier);
                });
                AddInfoPanelText(trainGO.transform, "Label", "\u2694 Train Troops", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Hero assignment button for applicable buildings
            if (HeroAssignableBuildings.Contains(buildingId))
            {
                var assignGO = new GameObject("HeroAssignBtn");
                assignGO.transform.SetParent(panel.transform, false);
                var assignRect = assignGO.AddComponent<RectTransform>();
                assignRect.anchorMin = new Vector2(0.05f, 0.20f);
                assignRect.anchorMax = new Vector2(0.48f, 0.27f);
                assignRect.offsetMin = Vector2.zero;
                assignRect.offsetMax = Vector2.zero;
                var assignBg = assignGO.AddComponent<Image>();
                bool hasAssigned = _heroAssignments.ContainsKey(instanceId);
                assignBg.color = hasAssigned
                    ? new Color(0.15f, 0.40f, 0.55f, 0.90f)
                    : new Color(0.25f, 0.18f, 0.45f, 0.90f);
                assignBg.raycastTarget = true;
                var assignOutline = assignGO.AddComponent<Outline>();
                assignOutline.effectColor = new Color(0.50f, 0.40f, 0.80f, 0.6f);
                assignOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var assignBtn = assignGO.AddComponent<Button>();
                assignBtn.targetGraphic = assignBg;
                string capAssignInst = instanceId;
                string capAssignBld = buildingId;
                int capAssignTier = tier;
                assignBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowHeroAssignPanel(capAssignInst, capAssignBld, capAssignTier);
                });
                string assignLabel = hasAssigned
                    ? $"\u265F Hero: +{GetHeroAssignmentBonus(instanceId)}%"
                    : "\u265F Assign Hero";
                AddInfoPanelText(assignGO.transform, "Label", assignLabel, 9, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Production boost button for resource buildings
            if (buildingId == "grain_farm" || buildingId == "iron_mine" || buildingId == "stone_quarry" || buildingId == "arcane_tower")
            {
                var boostGO = new GameObject("BoostBtn");
                boostGO.transform.SetParent(panel.transform, false);
                var boostRect = boostGO.AddComponent<RectTransform>();
                boostRect.anchorMin = new Vector2(0.50f, 0.13f);
                boostRect.anchorMax = new Vector2(0.95f, 0.20f);
                boostRect.offsetMin = Vector2.zero;
                boostRect.offsetMax = Vector2.zero;
                var boostBg = boostGO.AddComponent<Image>();
                boostBg.color = new Color(0.20f, 0.55f, 0.25f, 0.90f);
                boostBg.raycastTarget = true;
                var boostOutline = boostGO.AddComponent<Outline>();
                boostOutline.effectColor = new Color(0.40f, 0.80f, 0.45f, 0.6f);
                boostOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var boostBtn = boostGO.AddComponent<Button>();
                boostBtn.targetGraphic = boostBg;
                string capBoostInstId = instanceId;
                string capBoostBid = buildingId;
                int capBoostTier = tier;
                boostBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowProductionBoostPanel(capBoostInstId, capBoostBid, capBoostTier);
                });
                AddInfoPanelText(boostGO.transform, "Label", "\u2191 Boost Production", 9, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Trade button for marketplace
            if (buildingId == "marketplace")
            {
                var tradeGO = new GameObject("TradeBtn");
                tradeGO.transform.SetParent(panel.transform, false);
                var tradeRect = tradeGO.AddComponent<RectTransform>();
                tradeRect.anchorMin = new Vector2(0.50f, 0.28f);
                tradeRect.anchorMax = new Vector2(0.95f, 0.35f);
                tradeRect.offsetMin = Vector2.zero;
                tradeRect.offsetMax = Vector2.zero;
                var tradeBg = tradeGO.AddComponent<Image>();
                tradeBg.color = new Color(0.20f, 0.45f, 0.55f, 0.90f);
                tradeBg.raycastTarget = true;
                var tradeOutline = tradeGO.AddComponent<Outline>();
                tradeOutline.effectColor = new Color(0.40f, 0.70f, 0.85f, 0.6f);
                tradeOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var tradeBtn = tradeGO.AddComponent<Button>();
                tradeBtn.targetGraphic = tradeBg;
                int capTradeTier = tier;
                tradeBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowTradePanel(capTradeTier);
                });
                AddInfoPanelText(tradeGO.transform, "Label", "\u2696 Trade", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Warehouse protection view for marketplace
            if (buildingId == "marketplace")
            {
                var whGO = new GameObject("WarehouseBtn");
                whGO.transform.SetParent(panel.transform, false);
                var whRect = whGO.AddComponent<RectTransform>();
                whRect.anchorMin = new Vector2(0.50f, 0.20f);
                whRect.anchorMax = new Vector2(0.95f, 0.27f);
                whRect.offsetMin = Vector2.zero;
                whRect.offsetMax = Vector2.zero;
                var whBg = whGO.AddComponent<Image>();
                whBg.color = new Color(0.50f, 0.40f, 0.15f, 0.90f);
                whBg.raycastTarget = true;
                var whOutline = whGO.AddComponent<Outline>();
                whOutline.effectColor = new Color(0.75f, 0.60f, 0.20f, 0.6f);
                whOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var whBtn = whGO.AddComponent<Button>();
                whBtn.targetGraphic = whBg;
                whBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowWarehousePanel();
                });
                AddInfoPanelText(whGO.transform, "Label", "\u2618 Warehouse", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Alliance donation button for embassy
            if (buildingId == "embassy")
            {
                var donateGO = new GameObject("DonateBtn");
                donateGO.transform.SetParent(panel.transform, false);
                var donateRect = donateGO.AddComponent<RectTransform>();
                donateRect.anchorMin = new Vector2(0.50f, 0.28f);
                donateRect.anchorMax = new Vector2(0.95f, 0.35f);
                donateRect.offsetMin = Vector2.zero;
                donateRect.offsetMax = Vector2.zero;
                var donateBg = donateGO.AddComponent<Image>();
                donateBg.color = new Color(0.45f, 0.25f, 0.55f, 0.90f);
                donateBg.raycastTarget = true;
                var donateOutline = donateGO.AddComponent<Outline>();
                donateOutline.effectColor = new Color(0.70f, 0.45f, 0.80f, 0.6f);
                donateOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var donateBtn = donateGO.AddComponent<Button>();
                donateBtn.targetGraphic = donateBg;
                int capDonateTier = tier;
                donateBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowDonationPanel(capDonateTier);
                });
                AddInfoPanelText(donateGO.transform, "Label", "\u2694 Donate", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Adjacency bonus text in overview tab
            {
                var placementForAdj = _placements.Find(p => p.InstanceId == instanceId);
                if (placementForAdj != null)
                {
                    string adjText = GetAdjacencyBonusText(placementForAdj);
                    if (adjText != null)
                    {
                        int adjBonus = GetAdjacencyBonus(placementForAdj);
                        Color adjColor = adjBonus > 0 ? new Color(0.40f, 0.85f, 0.40f) : new Color(0.6f, 0.6f, 0.6f);
                        AddInfoPanelText(panel.transform, "AdjBonus", adjText, 9, FontStyle.Italic, adjColor,
                            new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.19f), TextAnchor.MiddleLeft);
                        // Show adjacency glow lines when info panel open
                        if (adjBonus > 0) ShowAdjacencyLines(placementForAdj);
                    }
                }
            }

            // P&C: Stronghold — show what unlocks at next level
            if (buildingId == "stronghold" && tier < 3)
            {
                int nextShLevel = tier + 1;
                var unlockNames = new List<string>();
                string[] allTypes = { "grain_farm", "iron_mine", "stone_quarry", "barracks",
                    "wall", "watch_tower", "marketplace", "academy", "training_ground", "forge",
                    "arcane_tower", "guild_hall", "armory", "laboratory", "embassy",
                    "enchanting_tower", "library", "hero_shrine", "observatory", "archive" };
                foreach (string bt in allTypes)
                {
                    if (GetBuildingUnlockLevel(bt) == nextShLevel)
                    {
                        string name = BuildingDisplayNames.TryGetValue(bt, out var n) ? n : bt;
                        unlockNames.Add(name);
                    }
                }
                if (unlockNames.Count > 0)
                {
                    string unlockList = string.Join(", ", unlockNames);
                    AddInfoPanelText(panel.transform, "Unlocks",
                        $"\u26BF Lv.{nextShLevel} unlocks: {unlockList}", 9, FontStyle.Normal,
                        new Color(0.80f, 0.70f, 0.30f),
                        new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.20f), TextAnchor.MiddleLeft);
                }
            }

            // P&C: Layout presets button for stronghold
            if (buildingId == "stronghold")
            {
                var layoutGO = new GameObject("LayoutPresetsBtn");
                layoutGO.transform.SetParent(panel.transform, false);
                var layoutRect = layoutGO.AddComponent<RectTransform>();
                layoutRect.anchorMin = new Vector2(0.52f, 0.20f);
                layoutRect.anchorMax = new Vector2(0.95f, 0.27f);
                layoutRect.offsetMin = Vector2.zero;
                layoutRect.offsetMax = Vector2.zero;
                var layoutBg = layoutGO.AddComponent<Image>();
                layoutBg.color = new Color(0.35f, 0.30f, 0.55f, 0.90f);
                layoutBg.raycastTarget = true;
                var layoutOutline = layoutGO.AddComponent<Outline>();
                layoutOutline.effectColor = new Color(0.55f, 0.45f, 0.75f, 0.6f);
                layoutOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var layoutBtn = layoutGO.AddComponent<Button>();
                layoutBtn.targetGraphic = layoutBg;
                layoutBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowLayoutPresetsPanel();
                });
                AddInfoPanelText(layoutGO.transform, "Label", "\u2302 Layouts", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // P&C: Check if building is currently upgrading — show in-progress state
            BuildQueueEntry activeQueueEntry = null;
            if (ServiceLocator.TryGet<BuildingManager>(out var infoBm))
            {
                foreach (var qe in infoBm.BuildQueue)
                {
                    if (qe.PlacedId == instanceId) { activeQueueEntry = qe; break; }
                }
            }

            if (activeQueueEntry != null)
            {
                // ─── UPGRADING STATE: progress bar + timer + speed-up/help ───
                // Progress bar background
                var progBgGO = new GameObject("ProgBg");
                progBgGO.transform.SetParent(panel.transform, false);
                var progBgRect = progBgGO.AddComponent<RectTransform>();
                progBgRect.anchorMin = new Vector2(0.05f, 0.14f);
                progBgRect.anchorMax = new Vector2(0.95f, 0.19f);
                progBgRect.offsetMin = Vector2.zero;
                progBgRect.offsetMax = Vector2.zero;
                var progBgImg = progBgGO.AddComponent<Image>();
                progBgImg.color = new Color(0.12f, 0.10f, 0.18f, 0.9f);
                progBgImg.raycastTarget = false;

                // Progress bar fill
                var progFillGO = new GameObject("ProgFill");
                progFillGO.transform.SetParent(progBgGO.transform, false);
                var progFillRect = progFillGO.AddComponent<RectTransform>();
                float totalSec = (float)(System.DateTime.Now - activeQueueEntry.StartTime).TotalSeconds + activeQueueEntry.RemainingSeconds;
                float elapsed = totalSec - activeQueueEntry.RemainingSeconds;
                float progress = totalSec > 0 ? Mathf.Clamp01(elapsed / totalSec) : 0f;
                progFillRect.anchorMin = Vector2.zero;
                progFillRect.anchorMax = new Vector2(progress, 1f);
                progFillRect.offsetMin = Vector2.zero;
                progFillRect.offsetMax = Vector2.zero;
                var progFillImg = progFillGO.AddComponent<Image>();
                progFillImg.color = new Color(0.20f, 0.70f, 0.35f, 0.90f);
                progFillImg.raycastTarget = false;

                // Progress percentage text
                AddInfoPanelText(progBgGO.transform, "ProgPct", $"{(int)(progress * 100)}%", 10, FontStyle.Bold,
                    Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                // Timer text
                string timeLeft = FormatTimeRemaining(Mathf.RoundToInt(activeQueueEntry.RemainingSeconds));
                AddInfoPanelText(panel.transform, "UpgTimer",
                    $"\u23F1 Upgrading to Lv.{activeQueueEntry.TargetTier}  —  {timeLeft}",
                    12, FontStyle.Bold, new Color(0.90f, 0.82f, 0.40f),
                    new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.14f), TextAnchor.MiddleCenter);

                // Speed-up button
                var speedGO = new GameObject("SpeedUpBtn");
                speedGO.transform.SetParent(panel.transform, false);
                var speedRect = speedGO.AddComponent<RectTransform>();
                speedRect.anchorMin = new Vector2(0.05f, 0.00f);
                speedRect.anchorMax = new Vector2(0.38f, 0.07f);
                speedRect.offsetMin = Vector2.zero;
                speedRect.offsetMax = Vector2.zero;
                var speedImg = speedGO.AddComponent<Image>();
                bool isFreeSpeedup = activeQueueEntry.RemainingSeconds <= 60f;
                speedImg.color = isFreeSpeedup
                    ? new Color(0.15f, 0.65f, 0.25f, 0.90f)
                    : new Color(0.50f, 0.22f, 0.70f, 0.90f);
                speedImg.raycastTarget = true;
                var speedOutline = speedGO.AddComponent<Outline>();
                speedOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.5f);
                speedOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var speedBtn = speedGO.AddComponent<Button>();
                speedBtn.targetGraphic = speedImg;
                string speedLabel = isFreeSpeedup ? "\u26A1 FREE" : $"\u26A1 Speed Up";
                AddInfoPanelText(speedGO.transform, "Label", speedLabel, 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                // Help button (alliance)
                var helpGO = new GameObject("HelpBtn");
                helpGO.transform.SetParent(panel.transform, false);
                var helpRect = helpGO.AddComponent<RectTransform>();
                helpRect.anchorMin = new Vector2(0.40f, 0.00f);
                helpRect.anchorMax = new Vector2(0.70f, 0.07f);
                helpRect.offsetMin = Vector2.zero;
                helpRect.offsetMax = Vector2.zero;
                var helpImg = helpGO.AddComponent<Image>();
                helpImg.color = new Color(0.18f, 0.40f, 0.72f, 0.90f);
                helpImg.raycastTarget = true;
                var helpOutline = helpGO.AddComponent<Outline>();
                helpOutline.effectColor = new Color(0.30f, 0.55f, 0.90f, 0.5f);
                helpOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var helpBtn = helpGO.AddComponent<Button>();
                helpBtn.targetGraphic = helpImg;
                string helpPlacedId = instanceId;
                helpBtn.onClick.AddListener(() =>
                {
                    EventBus.Publish(new AllianceHelpRequestedEvent(helpPlacedId));
                });
                AddInfoPanelText(helpGO.transform, "Label", "\u2764 Help", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                // P&C: Cancel upgrade button
                var cancelGO = new GameObject("CancelUpgradeBtn");
                cancelGO.transform.SetParent(panel.transform, false);
                var cancelRect = cancelGO.AddComponent<RectTransform>();
                cancelRect.anchorMin = new Vector2(0.72f, 0.00f);
                cancelRect.anchorMax = new Vector2(0.95f, 0.07f);
                cancelRect.offsetMin = Vector2.zero;
                cancelRect.offsetMax = Vector2.zero;
                var cancelImg = cancelGO.AddComponent<Image>();
                cancelImg.color = new Color(0.60f, 0.25f, 0.20f, 0.85f);
                cancelImg.raycastTarget = true;
                var cancelOutline2 = cancelGO.AddComponent<Outline>();
                cancelOutline2.effectColor = new Color(0.85f, 0.35f, 0.25f, 0.5f);
                cancelOutline2.effectDistance = new Vector2(0.5f, -0.5f);
                var cancelBtn = cancelGO.AddComponent<Button>();
                cancelBtn.targetGraphic = cancelImg;
                string cancelInstId = instanceId;
                string cancelBldId = buildingId;
                int cancelTargetTier = activeQueueEntry.TargetTier;
                float cancelRemaining = activeQueueEntry.RemainingSeconds;
                cancelBtn.onClick.AddListener(() =>
                {
                    DismissBuildingInfoPanel();
                    ShowCancelUpgradeDialog(cancelInstId, cancelBldId, cancelTargetTier, cancelRemaining);
                });
                AddInfoPanelText(cancelGO.transform, "Label", "\u2715 Cancel", 9, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
            else
            {
                // ─── NORMAL STATE: demolish + upgrade + prereq text ───

                // P&C: Prerequisite requirements text near upgrade button
                string blockReason = GetUpgradeBlockReason(instanceId, tier);
                if (blockReason != null)
                {
                    Color reqColor = blockReason.StartsWith("Already") || blockReason.StartsWith("Build queue")
                        ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.95f, 0.45f, 0.35f);
                    AddInfoPanelText(panel.transform, "Prereq", blockReason, 9, FontStyle.Normal, reqColor,
                        new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.18f), TextAnchor.MiddleCenter);
                }

                // P&C: Demolish button (not for stronghold)
                if (buildingId != "stronghold")
                {
                    var demGO = new GameObject("DemolishBtn");
                    demGO.transform.SetParent(panel.transform, false);
                    var demRect = demGO.AddComponent<RectTransform>();
                    demRect.anchorMin = new Vector2(0.05f, 0.13f);
                    demRect.anchorMax = new Vector2(0.35f, 0.20f);
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

                // P&C: Full-width upgrade button at bottom of info panel with cost display
                {
                    string infoCostStr = GetUpgradeCostString(instanceId, tier);
                    bool infoMaxLevel = infoCostStr == "MAX LEVEL";
                    var upgGO = new GameObject("UpgradeBtn");
                    upgGO.transform.SetParent(panel.transform, false);
                    var upgRect = upgGO.AddComponent<RectTransform>();
                    upgRect.anchorMin = new Vector2(0.05f, 0.02f);
                    upgRect.anchorMax = new Vector2(0.95f, 0.12f);
                    upgRect.offsetMin = Vector2.zero;
                    upgRect.offsetMax = Vector2.zero;
                    var upgImg = upgGO.AddComponent<Image>();
                    upgImg.color = infoMaxLevel
                        ? new Color(0.30f, 0.30f, 0.30f, 0.7f)
                        : blockReason != null
                            ? new Color(0.40f, 0.40f, 0.40f, 0.75f)
                            : new Color(0.15f, 0.60f, 0.20f, 0.92f);
                    upgImg.raycastTarget = true;
                    var upgOutline = upgGO.AddComponent<Outline>();
                    upgOutline.effectColor = infoMaxLevel
                        ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
                        : new Color(0.40f, 0.90f, 0.30f, 0.6f);
                    upgOutline.effectDistance = new Vector2(1.2f, -1.2f);
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
                    // Top row: "⬆ UPGRADE" label
                    AddInfoPanelText(upgGO.transform, "Label",
                        infoMaxLevel ? "MAX LEVEL" : "\u2B06 UPGRADE", 12,
                        FontStyle.Bold, Color.white,
                        new Vector2(0, 0.45f), new Vector2(1, 1), TextAnchor.MiddleCenter);
                    // Bottom row: cost string (if not max)
                    if (!infoMaxLevel)
                    {
                        AddInfoPanelText(upgGO.transform, "Cost", infoCostStr, 8,
                            FontStyle.Normal, new Color(0.85f, 0.85f, 0.75f),
                            new Vector2(0, 0), new Vector2(1, 0.45f), TextAnchor.MiddleCenter);
                    }
                }
            }

        }

        private void BuildInfoPanelProductionTab(GameObject panel, string buildingId, string instanceId, int tier)
        {
            float y = 0.80f;
            AddInfoPanelText(panel.transform, "ProdTitle", "\u2609 Resource Production Overview", 13, FontStyle.Bold,
                new Color(0.90f, 0.82f, 0.40f),
                new Vector2(0.05f, y), new Vector2(0.95f, y + 0.05f), TextAnchor.MiddleCenter);
            y -= 0.06f;

            // Show all resource-producing buildings with their rates
            foreach (var resKvp in ResourceBuildingTypes)
            {
                string bId = resKvp.Key;
                string resName = resKvp.Value.Name;
                Color resTint = resKvp.Value.Tint;

                // Find all instances of this building type
                int totalRate = 0;
                int count = 0;
                foreach (var p in _placements)
                {
                    if (p.BuildingId != bId) continue;
                    int rate = (p.Tier + 1) * 250;
                    totalRate += rate;
                    count++;
                }
                if (count == 0) continue;

                // Resource type header
                string rateStr = totalRate >= 1000 ? $"{totalRate / 1000f:F1}K" : $"{totalRate}";
                AddInfoPanelText(panel.transform, $"Res_{bId}", $"{resName}: +{rateStr}/hr ({count} buildings)",
                    11, FontStyle.Bold, resTint,
                    new Vector2(0.05f, y - 0.04f), new Vector2(0.95f, y + 0.01f), TextAnchor.MiddleLeft);
                y -= 0.045f;

                // Individual building breakdown
                foreach (var p in _placements)
                {
                    if (p.BuildingId != bId) continue;
                    int pRate = (p.Tier + 1) * 250;
                    string pRateStr = pRate >= 1000 ? $"{pRate / 1000f:F1}K" : $"{pRate}";
                    string bName = BuildingDisplayNames.TryGetValue(bId, out var n) ? n : bId;
                    AddInfoPanelText(panel.transform, $"Detail_{p.InstanceId}",
                        $"  Lv.{p.Tier} {bName}: +{pRateStr}/hr", 9, FontStyle.Normal,
                        new Color(resTint.r * 0.7f, resTint.g * 0.7f, resTint.b * 0.7f),
                        new Vector2(0.08f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                    y -= 0.035f;
                }
                y -= 0.02f;
            }

            // Daily totals summary
            y -= 0.01f;
            int grandTotal = 0;
            foreach (var p in _placements)
            {
                if (ResourceBuildingTypes.ContainsKey(p.BuildingId))
                    grandTotal += (p.Tier + 1) * 250;
            }
            int dailyTotal = grandTotal * 24;
            string dailyStr = dailyTotal >= 1_000_000 ? $"{dailyTotal / 1_000_000f:F1}M"
                : dailyTotal >= 1000 ? $"{dailyTotal / 1000f:F1}K" : $"{dailyTotal}";
            AddInfoPanelText(panel.transform, "DailyTotal",
                $"\u2B50 Total combined: +{grandTotal}/hr | +{dailyStr}/day", 11, FontStyle.Bold,
                new Color(0.95f, 0.85f, 0.40f),
                new Vector2(0.05f, y - 0.04f), new Vector2(0.95f, y + 0.01f), TextAnchor.MiddleCenter);

            // Vault status
            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
            {
                y -= 0.06f;
                AddInfoPanelText(panel.transform, "VaultHeader", "VAULT STATUS", 11, FontStyle.Bold,
                    new Color(0.70f, 0.85f, 1f),
                    new Vector2(0.05f, y - 0.02f), new Vector2(0.95f, y + 0.03f), TextAnchor.MiddleLeft);
                y -= 0.045f;
                string[] resNames = { "Stone", "Iron", "Grain", "Arcane" };
                long[] curVals = { rm.Stone, rm.Iron, rm.Grain, rm.ArcaneEssence };
                long[] maxVals = { rm.MaxStone, rm.MaxIron, rm.MaxGrain, rm.MaxArcaneEssence };
                Color[] resCols = { new Color(0.85f, 0.82f, 0.76f), new Color(0.78f, 0.80f, 0.90f),
                    new Color(1f, 0.92f, 0.45f), new Color(0.80f, 0.55f, 1f) };
                for (int ri = 0; ri < 4; ri++)
                {
                    float pct = maxVals[ri] > 0 ? (float)curVals[ri] / maxVals[ri] : 0f;
                    string curS = curVals[ri] >= 1000 ? $"{curVals[ri] / 1000f:F1}K" : $"{curVals[ri]}";
                    string maxS = maxVals[ri] >= 1000 ? $"{maxVals[ri] / 1000f:F1}K" : $"{maxVals[ri]}";
                    Color barCol = pct >= 0.95f ? new Color(0.90f, 0.30f, 0.25f) : resCols[ri];
                    AddInfoPanelText(panel.transform, $"Vault_{resNames[ri]}",
                        $"{resNames[ri]}: {curS}/{maxS} ({pct * 100:F0}%)", 10, FontStyle.Normal, barCol,
                        new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                    y -= 0.04f;
                }
            }

            // P&C: Production efficiency rating
            y -= 0.04f;
            int maxPossibleRate = 0;
            int actualRate = 0;
            foreach (var resKvp in ResourceBuildingTypes)
            {
                int maxCount = MaxBuildingCountPerType.TryGetValue(resKvp.Key, out int mc) ? mc : 1;
                maxPossibleRate += maxCount * 4 * 250; // tier 3, all slots filled
                foreach (var p in _placements)
                {
                    if (p.BuildingId == resKvp.Key)
                        actualRate += (p.Tier + 1) * 250;
                }
            }
            float efficiency = maxPossibleRate > 0 ? (float)actualRate / maxPossibleRate : 0f;
            string grade = efficiency >= 0.9f ? "S" : efficiency >= 0.75f ? "A" : efficiency >= 0.55f ? "B"
                : efficiency >= 0.35f ? "C" : "D";
            Color gradeColor = grade switch
            {
                "S" => new Color(1f, 0.85f, 0.20f),
                "A" => new Color(0.50f, 0.95f, 0.50f),
                "B" => new Color(0.55f, 0.80f, 1f),
                "C" => new Color(0.85f, 0.70f, 0.40f),
                _ => new Color(0.75f, 0.45f, 0.40f),
            };
            AddInfoPanelText(panel.transform, "Efficiency",
                $"Production Efficiency: {grade} ({efficiency * 100:F0}%)", 11, FontStyle.Bold, gradeColor,
                new Vector2(0.05f, y - 0.04f), new Vector2(0.95f, y + 0.01f), TextAnchor.MiddleCenter);
            AddInfoPanelText(panel.transform, "EffHint",
                "Upgrade resource buildings & fill all slots to improve rating", 8, FontStyle.Normal,
                new Color(0.55f, 0.52f, 0.50f),
                new Vector2(0.05f, y - 0.07f), new Vector2(0.95f, y - 0.035f), TextAnchor.MiddleCenter);
        }

        private void BuildInfoPanelBoostsTab(GameObject panel, string buildingId, string instanceId, int tier)
        {
            float y = 0.80f;
            AddInfoPanelText(panel.transform, "BoostTitle", "\u26A1 Active Boosts & Bonuses", 13, FontStyle.Bold,
                new Color(0.55f, 0.85f, 1f),
                new Vector2(0.05f, y), new Vector2(0.95f, y + 0.05f), TextAnchor.MiddleCenter);
            y -= 0.06f;

            // Building-specific bonuses
            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            AddInfoPanelText(panel.transform, "BuildingBoosts", $"Lv.{tier} {bName} Bonuses:", 12, FontStyle.Bold,
                new Color(0.85f, 0.75f, 0.40f),
                new Vector2(0.05f, y - 0.04f), new Vector2(0.95f, y + 0.01f), TextAnchor.MiddleLeft);
            y -= 0.05f;

            // Power bonus
            int power = GetBuildingPowerContribution(buildingId, tier);
            if (power > 0)
            {
                string powStr = power >= 1000 ? $"{power / 1000f:F1}K" : $"{power}";
                AddInfoPanelText(panel.transform, "BoostPower", $"  \u2694 Power: +{powStr}", 10, FontStyle.Normal,
                    new Color(0.95f, 0.70f, 0.35f),
                    new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                y -= 0.04f;
            }

            // Resource production bonus
            if (ResourceBuildingTypes.TryGetValue(buildingId, out var resInfo))
            {
                int rate = (tier + 1) * 250;
                string rateStr = rate >= 1000 ? $"{rate / 1000f:F1}K" : $"{rate}";
                AddInfoPanelText(panel.transform, "BoostProd", $"  \u2609 {resInfo.Name} Production: +{rateStr}/hr",
                    10, FontStyle.Normal, resInfo.Tint,
                    new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                y -= 0.04f;
            }

            // Military bonuses
            if (buildingId == "barracks" || buildingId == "training_ground")
            {
                int troops = buildingId == "barracks" ? (tier + 1) * 500 : (tier + 1) * 300;
                AddInfoPanelText(panel.transform, "BoostTroops", $"  \u2694 Troop Capacity: +{troops}",
                    10, FontStyle.Normal, new Color(0.80f, 0.35f, 0.35f),
                    new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                y -= 0.04f;
            }

            // Defense bonus
            if (buildingId == "wall" || buildingId == "watch_tower")
            {
                int def = (tier + 1) * 800;
                AddInfoPanelText(panel.transform, "BoostDef", $"  \u25C8 Defense: +{def}",
                    10, FontStyle.Normal, new Color(0.45f, 0.75f, 0.95f),
                    new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                y -= 0.04f;
            }

            // Research bonus
            if (buildingId == "academy" || buildingId == "library")
            {
                int resSpd = (tier + 1) * 5;
                AddInfoPanelText(panel.transform, "BoostRes", $"  \u2726 Research Speed: +{resSpd}%",
                    10, FontStyle.Normal, new Color(0.55f, 0.85f, 0.55f),
                    new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                y -= 0.04f;
            }

            y -= 0.03f;

            // Empire-wide bonuses section
            AddInfoPanelText(panel.transform, "EmpireBoosts", "Empire-Wide Bonuses:", 12, FontStyle.Bold,
                new Color(0.85f, 0.75f, 0.40f),
                new Vector2(0.05f, y - 0.04f), new Vector2(0.95f, y + 0.01f), TextAnchor.MiddleLeft);
            y -= 0.05f;

            // Calculate total power
            int totalPower = 0;
            foreach (var p in _placements)
                totalPower += GetBuildingPowerContribution(p.BuildingId, p.Tier);
            string tpStr = totalPower >= 1000 ? $"{totalPower / 1000f:F1}K" : $"{totalPower}";
            AddInfoPanelText(panel.transform, "TotalPower", $"  \u2694 Total City Power: {tpStr}",
                10, FontStyle.Normal, new Color(0.95f, 0.70f, 0.35f),
                new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
            y -= 0.04f;

            // Stronghold level bonus
            int shLevel = 1;
            foreach (var p in _placements) { if (p.BuildingId == "stronghold") { shLevel = p.Tier; break; } }
            AddInfoPanelText(panel.transform, "SHBonus", $"  \u2B50 Stronghold Lv.{shLevel}: All buildings cap Lv.{shLevel}",
                10, FontStyle.Normal, new Color(0.90f, 0.82f, 0.40f),
                new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
            y -= 0.04f;

            // Research bonuses (if ResearchManager available)
            if (ServiceLocator.TryGet<ResearchManager>(out var researchMgr))
            {
                var bonuses = researchMgr.Bonuses;
                if (bonuses.CombatAttackPercent > 0)
                {
                    AddInfoPanelText(panel.transform, "ResAtk", $"  \u2694 Research ATK: +{bonuses.CombatAttackPercent:F0}%",
                        10, FontStyle.Normal, new Color(0.90f, 0.50f, 0.40f),
                        new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                    y -= 0.04f;
                }
                if (bonuses.CombatDefensePercent > 0)
                {
                    AddInfoPanelText(panel.transform, "ResDef", $"  \u25C8 Research DEF: +{bonuses.CombatDefensePercent:F0}%",
                        10, FontStyle.Normal, new Color(0.45f, 0.75f, 0.95f),
                        new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                    y -= 0.04f;
                }
                if (bonuses.ResearchTimeReductionPercent > 0)
                {
                    AddInfoPanelText(panel.transform, "ResSpd", $"  \u2726 Research Speed: +{bonuses.ResearchTimeReductionPercent:F0}%",
                        10, FontStyle.Normal, new Color(0.55f, 0.85f, 0.55f),
                        new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
                    y -= 0.04f;
                }
            }

            // Collection streak info
            AddInfoPanelText(panel.transform, "StreakInfo",
                $"  \u26A1 Collection Streak: Collect rapidly for +10%/20%/30% bonus",
                9, FontStyle.Normal, new Color(0.65f, 0.62f, 0.60f),
                new Vector2(0.05f, y - 0.035f), new Vector2(0.95f, y), TextAnchor.MiddleLeft);
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

        // P&C: Stat comparison bar data
        private struct StatBarData
        {
            public string Label;
            public float CurrentVal;
            public float NextVal;
            public float MaxVal;
            public Color BarColor;
        }

        private static List<StatBarData> GetStatComparisonData(string buildingId, int curTier, int nextTier)
        {
            var bars = new List<StatBarData>();
            // Power bar (all buildings have power)
            int curPow = GetBuildingPowerContribution(buildingId, curTier);
            int nextPow = GetBuildingPowerContribution(buildingId, nextTier);
            int maxPow = GetBuildingPowerContribution(buildingId, 3); // tier 3 is max
            bars.Add(new StatBarData { Label = "\u2694 Power", CurrentVal = curPow, NextVal = nextPow,
                MaxVal = maxPow, BarColor = new Color(0.95f, 0.70f, 0.35f) });

            // Production bar (resource buildings)
            if (buildingId == "grain_farm" || buildingId == "iron_mine" ||
                buildingId == "stone_quarry" || buildingId == "arcane_tower")
            {
                int curRate = (curTier + 1) * 250;
                int nextRate = (nextTier + 1) * 250;
                int maxRate = 4 * 250; // tier 3
                Color prodColor = buildingId switch
                {
                    "grain_farm" => new Color(1f, 0.92f, 0.45f),
                    "iron_mine" => new Color(0.78f, 0.80f, 0.90f),
                    "stone_quarry" => new Color(0.85f, 0.82f, 0.76f),
                    _ => new Color(0.80f, 0.55f, 1f),
                };
                bars.Add(new StatBarData { Label = "\u2609 Prod/hr", CurrentVal = curRate, NextVal = nextRate,
                    MaxVal = maxRate, BarColor = prodColor });
            }

            // Troop capacity (military buildings)
            if (buildingId == "barracks")
            {
                float curTroops = (curTier + 1) * 500f;
                float nextTroops = (nextTier + 1) * 500f;
                bars.Add(new StatBarData { Label = "\u2694 Troops", CurrentVal = curTroops, NextVal = nextTroops,
                    MaxVal = 2000f, BarColor = new Color(0.80f, 0.35f, 0.35f) });
            }
            else if (buildingId == "training_ground")
            {
                float curTroops = (curTier + 1) * 300f;
                float nextTroops = (nextTier + 1) * 300f;
                bars.Add(new StatBarData { Label = "\u2694 Troops", CurrentVal = curTroops, NextVal = nextTroops,
                    MaxVal = 1200f, BarColor = new Color(0.80f, 0.35f, 0.35f) });
            }

            // Defense (wall, watch tower)
            if (buildingId == "wall" || buildingId == "watch_tower")
            {
                float curDef = (curTier + 1) * 800f;
                float nextDef = (nextTier + 1) * 800f;
                bars.Add(new StatBarData { Label = "\u25C8 Defense", CurrentVal = curDef, NextVal = nextDef,
                    MaxVal = 3200f, BarColor = new Color(0.45f, 0.75f, 0.95f) });
            }

            // Research speed (academy, library)
            if (buildingId == "academy" || buildingId == "library")
            {
                float curSpd = (curTier + 1) * 5f;
                float nextSpd = (nextTier + 1) * 5f;
                bars.Add(new StatBarData { Label = "\u2726 Research%", CurrentVal = curSpd, NextVal = nextSpd,
                    MaxVal = 20f, BarColor = new Color(0.55f, 0.85f, 0.55f) });
            }

            return bars;
        }

        private void AddStatComparisonBar(Transform parent, string label, float curVal, float nextVal,
            float maxVal, Color barColor, float yPos)
        {
            float barH = 0.045f;
            // Label
            AddInfoPanelText(parent, $"BarLabel_{label}", label, 9, FontStyle.Normal,
                new Color(0.75f, 0.73f, 0.70f),
                new Vector2(0.05f, yPos - barH), new Vector2(0.28f, yPos), TextAnchor.MiddleLeft);

            // Bar background
            var barBgGO = new GameObject($"BarBg_{label}");
            barBgGO.transform.SetParent(parent, false);
            var barBgRect = barBgGO.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.29f, yPos - barH + 0.005f);
            barBgRect.anchorMax = new Vector2(0.78f, yPos - 0.005f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBgGO.AddComponent<Image>();
            barBgImg.color = new Color(0.12f, 0.10f, 0.15f, 0.85f);
            barBgImg.raycastTarget = false;

            // Current value fill
            float curFill = maxVal > 0 ? Mathf.Clamp01(curVal / maxVal) : 0f;
            var curBarGO = new GameObject($"BarCur_{label}");
            curBarGO.transform.SetParent(barBgGO.transform, false);
            var curBarRect = curBarGO.AddComponent<RectTransform>();
            curBarRect.anchorMin = Vector2.zero;
            curBarRect.anchorMax = new Vector2(curFill, 1f);
            curBarRect.offsetMin = Vector2.zero;
            curBarRect.offsetMax = Vector2.zero;
            var curBarImg = curBarGO.AddComponent<Image>();
            curBarImg.color = new Color(barColor.r * 0.7f, barColor.g * 0.7f, barColor.b * 0.7f, 0.9f);
            curBarImg.raycastTarget = false;

            // Next tier fill (ghost bar extending beyond current)
            float nextFill = maxVal > 0 ? Mathf.Clamp01(nextVal / maxVal) : 0f;
            if (nextFill > curFill)
            {
                var nextBarGO = new GameObject($"BarNext_{label}");
                nextBarGO.transform.SetParent(barBgGO.transform, false);
                nextBarGO.transform.SetAsFirstSibling(); // Behind current bar
                var nextBarRect = nextBarGO.AddComponent<RectTransform>();
                nextBarRect.anchorMin = Vector2.zero;
                nextBarRect.anchorMax = new Vector2(nextFill, 1f);
                nextBarRect.offsetMin = Vector2.zero;
                nextBarRect.offsetMax = Vector2.zero;
                var nextBarImg = nextBarGO.AddComponent<Image>();
                nextBarImg.color = new Color(barColor.r, barColor.g, barColor.b, 0.35f);
                nextBarImg.raycastTarget = false;

                // Pulsing glow on the delta portion
                var deltaGlowGO = new GameObject($"BarDelta_{label}");
                deltaGlowGO.transform.SetParent(barBgGO.transform, false);
                var deltaRect = deltaGlowGO.AddComponent<RectTransform>();
                deltaRect.anchorMin = new Vector2(curFill, 0f);
                deltaRect.anchorMax = new Vector2(nextFill, 1f);
                deltaRect.offsetMin = Vector2.zero;
                deltaRect.offsetMax = Vector2.zero;
                var deltaImg = deltaGlowGO.AddComponent<Image>();
                deltaImg.color = new Color(barColor.r, barColor.g, barColor.b, 0.55f);
                deltaImg.raycastTarget = false;
            }

            // Value text: "current → next"
            string curStr = FormatStatValue(curVal);
            string nextStr = FormatStatValue(nextVal);
            Color valColor = nextVal > curVal ? new Color(0.50f, 0.95f, 0.50f) : new Color(0.80f, 0.78f, 0.75f);
            AddInfoPanelText(parent, $"BarVal_{label}", $"{curStr} \u2192 {nextStr}", 9, FontStyle.Bold,
                valColor,
                new Vector2(0.79f, yPos - barH), new Vector2(0.95f, yPos), TextAnchor.MiddleRight);
        }

        private static string FormatStatValue(float val)
        {
            if (val >= 1_000_000f) return $"{val / 1_000_000f:F1}M";
            if (val >= 1000f) return $"{val / 1000f:F1}K";
            return $"{(int)val}";
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
        // P&C: Resource Production Breakdown Popup
        // ====================================================================

        private GameObject _resourceBreakdownPopup;

        /// <summary>
        /// P&C: Show a detailed breakdown of all buildings producing a given resource,
        /// their individual rates, and total production. Triggered from info panel or programmatically.
        /// </summary>
        public void ShowResourceBreakdownPopup(string resourceName)
        {
            DismissResourceBreakdownPopup();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Map resource name to type info
            Color resColor = resourceName switch
            {
                "Stone" => new Color(0.75f, 0.68f, 0.55f),
                "Iron" => new Color(0.70f, 0.75f, 0.85f),
                "Grain" => new Color(0.95f, 0.85f, 0.30f),
                "Arcane" => new Color(0.65f, 0.45f, 0.90f),
                _ => Color.white
            };
            string producerId = resourceName switch
            {
                "Stone" => "stone_quarry",
                "Iron" => "iron_mine",
                "Grain" => "grain_farm",
                "Arcane" => "arcane_tower",
                _ => ""
            };

            _resourceBreakdownPopup = new GameObject("ResourceBreakdown");
            _resourceBreakdownPopup.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _resourceBreakdownPopup.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _resourceBreakdownPopup.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _resourceBreakdownPopup.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissResourceBreakdownPopup);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_resourceBreakdownPopup.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.30f);
            panelRect.anchorMax = new Vector2(0.92f, 0.72f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelImg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(resColor.r, resColor.g, resColor.b, 0.70f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            string resSymbol = resourceName switch
            {
                "Stone" => "\u25C8", "Iron" => "\u2666",
                "Grain" => "\u2740", "Arcane" => "\u2726", _ => "?"
            };
            AddInfoPanelText(panel.transform, "Title",
                $"{resSymbol} {resourceName} Production", 16, FontStyle.Bold, resColor,
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);

            // Separator
            var sepGO = new GameObject("Sep");
            sepGO.transform.SetParent(panel.transform, false);
            var sepRect = sepGO.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.05f, 0.83f);
            sepRect.anchorMax = new Vector2(0.95f, 0.84f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = new Color(resColor.r, resColor.g, resColor.b, 0.35f);
            sepImg.raycastTarget = false;

            // List all producers
            float rowY = 0.78f;
            int totalRate = 0;
            int buildingCount = 0;

            foreach (var p in _placements)
            {
                if (p.BuildingId != producerId) continue;
                buildingCount++;
                int rate = (p.Tier + 1) * 250;
                totalRate += rate;
                string rateStr = rate >= 1000 ? $"{rate / 1000f:F1}K" : $"{rate}";
                string displayName = BuildingDisplayNames.TryGetValue(p.BuildingId, out var dn) ? dn : p.BuildingId;

                // Building name + tier
                AddInfoPanelText(panel.transform, $"Bld_{buildingCount}",
                    $"{displayName} Lv.{p.Tier}", 11, FontStyle.Normal,
                    new Color(0.85f, 0.82f, 0.78f),
                    new Vector2(0.05f, rowY - 0.06f), new Vector2(0.55f, rowY + 0.01f), TextAnchor.MiddleLeft);

                // Rate
                AddInfoPanelText(panel.transform, $"Rate_{buildingCount}",
                    $"+{rateStr}/hr", 11, FontStyle.Bold, resColor,
                    new Vector2(0.60f, rowY - 0.06f), new Vector2(0.95f, rowY + 0.01f), TextAnchor.MiddleRight);

                rowY -= 0.08f;
                if (rowY < 0.18f) break; // Don't overflow
            }

            if (buildingCount == 0)
            {
                AddInfoPanelText(panel.transform, "NoProd",
                    $"No {resourceName} producers built yet.", 12, FontStyle.Normal,
                    new Color(0.6f, 0.6f, 0.6f),
                    new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.55f), TextAnchor.MiddleCenter);
            }

            // Total separator + summary
            if (buildingCount > 0)
            {
                var totSep = new GameObject("TotSep");
                totSep.transform.SetParent(panel.transform, false);
                var totSepRect = totSep.AddComponent<RectTransform>();
                totSepRect.anchorMin = new Vector2(0.05f, rowY);
                totSepRect.anchorMax = new Vector2(0.95f, rowY + 0.01f);
                totSepRect.offsetMin = Vector2.zero;
                totSepRect.offsetMax = Vector2.zero;
                var totSepImg = totSep.AddComponent<Image>();
                totSepImg.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                totSepImg.raycastTarget = false;

                string totalStr = totalRate >= 1_000_000 ? $"{totalRate / 1_000_000f:F1}M"
                    : totalRate >= 1000 ? $"{totalRate / 1000f:F1}K" : $"{totalRate}";
                int dailyTotal = totalRate * 24;
                string dailyStr = dailyTotal >= 1_000_000 ? $"{dailyTotal / 1_000_000f:F1}M"
                    : dailyTotal >= 1000 ? $"{dailyTotal / 1000f:F1}K" : $"{dailyTotal}";

                AddInfoPanelText(panel.transform, "TotalLabel", "TOTAL", 12, FontStyle.Bold,
                    new Color(0.90f, 0.85f, 0.70f),
                    new Vector2(0.05f, rowY - 0.08f), new Vector2(0.35f, rowY - 0.01f), TextAnchor.MiddleLeft);
                AddInfoPanelText(panel.transform, "TotalRate", $"+{totalStr}/hr", 12, FontStyle.Bold, resColor,
                    new Vector2(0.40f, rowY - 0.08f), new Vector2(0.95f, rowY - 0.01f), TextAnchor.MiddleRight);
                AddInfoPanelText(panel.transform, "DailyTotal", $"\u2609 Daily: +{dailyStr}", 10, FontStyle.Normal,
                    new Color(resColor.r * 0.8f, resColor.g * 0.8f, resColor.b * 0.8f),
                    new Vector2(0.40f, rowY - 0.15f), new Vector2(0.95f, rowY - 0.08f), TextAnchor.MiddleRight);
            }

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.88f);
            closeRect.anchorMax = new Vector2(0.98f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(DismissResourceBreakdownPopup);
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _resourceBreakdownPopup.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void DismissResourceBreakdownPopup()
        {
            if (_resourceBreakdownPopup != null) { Destroy(_resourceBreakdownPopup); _resourceBreakdownPopup = null; }
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

        /// <summary>
        /// P&C: Show greyed-out ghost buildings on the grid for building types
        /// that unlock at the next stronghold level. Gives the player a preview
        /// of what they'll gain from upgrading their stronghold.
        /// </summary>
        /// <summary>P&C: Predefined empty building plots where player can build new structures.</summary>
        private static readonly Vector2Int[] EmptyBuildSlotPositions = new[]
        {
            new Vector2Int(26, 28), // N gap between military
            new Vector2Int(16, 10), // S outer resource area
            new Vector2Int(30, 10), // SE outer area
            new Vector2Int(38, 28), // NE corner
            new Vector2Int(7, 28),  // NW corner
            new Vector2Int(7, 14),  // SW corner
        };

        /// <summary>P&C: Create "+" markers on empty building plots to show buildable areas.</summary>
        private void CreateEmptyBuildSlotIndicators()
        {
            if (buildingContainer == null) return;

            foreach (var slotPos in EmptyBuildSlotPositions)
            {
                // Skip if this position already has a building
                bool occupied = false;
                foreach (var p in _placements)
                {
                    if (p.GridOrigin == slotPos) { occupied = true; break; }
                }
                if (occupied) continue;

                // Create isometric position
                float screenX = (slotPos.x - slotPos.y) * HalfW;
                float screenY = (slotPos.x + slotPos.y) * HalfH + IsoCenterY;

                var slotGO = new GameObject($"EmptySlot_{slotPos.x}_{slotPos.y}");
                slotGO.transform.SetParent(buildingContainer, false);
                var slotRect = slotGO.AddComponent<RectTransform>();
                slotRect.anchoredPosition = new Vector2(screenX, screenY);
                slotRect.sizeDelta = new Vector2(CellSize * 1.5f, CellSize * 1.5f);
                slotRect.pivot = new Vector2(0.5f, 0.3f);

                // Semi-transparent diamond outline background
                var bgImg = slotGO.AddComponent<Image>();
                bgImg.color = new Color(0.30f, 0.25f, 0.45f, 0.25f);
                bgImg.raycastTarget = true;
                var radialSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
                if (radialSpr != null) { bgImg.sprite = radialSpr; bgImg.type = Image.Type.Simple; }

                // Dashed gold outline
                var outline = slotGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.70f, 0.55f, 0.20f, 0.35f);
                outline.effectDistance = new Vector2(1f, -1f);

                // "+" symbol
                var plusGO = new GameObject("Plus");
                plusGO.transform.SetParent(slotGO.transform, false);
                var plusRect = plusGO.AddComponent<RectTransform>();
                plusRect.anchorMin = new Vector2(0.2f, 0.2f);
                plusRect.anchorMax = new Vector2(0.8f, 0.8f);
                plusRect.offsetMin = Vector2.zero;
                plusRect.offsetMax = Vector2.zero;
                var plusText = plusGO.AddComponent<Text>();
                plusText.text = "+";
                plusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                plusText.fontSize = 20;
                plusText.fontStyle = FontStyle.Bold;
                plusText.alignment = TextAnchor.MiddleCenter;
                plusText.color = new Color(0.80f, 0.65f, 0.25f, 0.50f);
                plusText.raycastTarget = false;
                var plusShadow = plusGO.AddComponent<Shadow>();
                plusShadow.effectColor = new Color(0, 0, 0, 0.6f);
                plusShadow.effectDistance = new Vector2(0.5f, -0.5f);

                // Tap handler — opens build selector at this grid position
                var btn = slotGO.AddComponent<Button>();
                btn.targetGraphic = bgImg;
                var capturedPos = slotPos;
                btn.onClick.AddListener(() => {
                    EventBus.Publish(new EmptyCellTappedEvent(capturedPos));
                });
            }
        }

        private void CreateBuildingUnlockPreviews()
        {
            if (buildingContainer == null) return;

            // Find current stronghold tier
            int shTier = 1;
            foreach (var p in _placements)
            {
                if (p.BuildingId == "stronghold") { shTier = p.Tier; break; }
            }
            int nextShTier = shTier + 1;

            // Find building types that unlock at the next SH level
            var unlockableTypes = new List<string>();
            string[] allBuildingTypes = { "grain_farm", "iron_mine", "stone_quarry", "barracks",
                "wall", "watch_tower", "marketplace", "academy", "training_ground", "forge",
                "arcane_tower", "guild_hall", "armory", "laboratory", "embassy",
                "enchanting_tower", "library", "hero_shrine", "observatory", "archive" };

            foreach (string bType in allBuildingTypes)
            {
                int unlockLevel = GetBuildingUnlockLevel(bType);
                if (unlockLevel == nextShTier)
                    unlockableTypes.Add(bType);
            }
            if (unlockableTypes.Count == 0) return;

            // Place ghost previews near the stronghold area — pick empty grid spots
            Vector2Int strongholdOrigin = new Vector2Int(22, 22);
            int previewIndex = 0;
            // Try positions around the stronghold in expanding rings
            int[] offsets = { -6, -4, -2, 2, 4, 6 };
            foreach (string buildType in unlockableTypes)
            {
                if (previewIndex >= 4) break; // Max 4 previews to avoid clutter

                // Find an empty spot near stronghold
                Vector2Int ghostPos = strongholdOrigin;
                bool found = false;
                foreach (int ox in offsets)
                {
                    foreach (int oy in offsets)
                    {
                        var testPos = strongholdOrigin + new Vector2Int(ox, oy);
                        if (testPos.x < 2 || testPos.y < 2 || testPos.x > 44 || testPos.y > 44) continue;
                        if (CanPlaceAt(testPos, new Vector2Int(2, 2), null))
                        {
                            ghostPos = testPos;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                if (!found) continue;

                // Create ghost building visual
                var ghostGO = new GameObject($"UnlockPreview_{buildType}");
                ghostGO.transform.SetParent(buildingContainer, false);
                var ghostRect = ghostGO.AddComponent<RectTransform>();

                // Position using isometric conversion
                float screenX = (ghostPos.x - ghostPos.y) * CellSize * 0.5f;
                float screenY = (ghostPos.x + ghostPos.y) * CellSize * 0.25f;
                ghostRect.anchoredPosition = new Vector2(screenX, screenY);
                float w = 2 * CellSize;
                ghostRect.sizeDelta = new Vector2(w, w * 1.5f);

                var ghostImg = ghostGO.AddComponent<Image>();
                ghostImg.color = new Color(0.40f, 0.40f, 0.50f, 0.30f); // Greyed-out ghost
                ghostImg.raycastTarget = false;

                // Try to load a dimmed sprite
                Sprite previewSprite = LoadBuildingSprite(buildType, 1);
                if (previewSprite != null)
                {
                    ghostImg.sprite = previewSprite;
                    ghostImg.preserveAspect = true;
                }

                // Dashed border effect
                var borderOutline = ghostGO.AddComponent<Outline>();
                borderOutline.effectColor = new Color(0.60f, 0.50f, 0.20f, 0.35f);
                borderOutline.effectDistance = new Vector2(1f, -1f);

                // "Unlocks at SH Lv.X" label
                string displayName = BuildingDisplayNames.TryGetValue(buildType, out var dn) ? dn : buildType;
                var labelGO = new GameObject("UnlockLabel");
                labelGO.transform.SetParent(ghostGO.transform, false);
                var labelRect = labelGO.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(-0.1f, -0.05f);
                labelRect.anchorMax = new Vector2(1.1f, 0.15f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var labelBg = labelGO.AddComponent<Image>();
                labelBg.color = new Color(0.06f, 0.04f, 0.10f, 0.80f);
                labelBg.raycastTarget = false;

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(labelGO.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGO.AddComponent<Text>();
                text.text = $"\u26BF SH Lv.{nextShTier}\n{displayName}";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 7;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.70f, 0.60f, 0.30f, 0.70f);
                text.raycastTarget = false;
                var shadow = textGO.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(0.4f, -0.4f);

                // Lock icon
                var lockGO = new GameObject("Lock");
                lockGO.transform.SetParent(ghostGO.transform, false);
                var lockRect = lockGO.AddComponent<RectTransform>();
                lockRect.anchorMin = new Vector2(0.30f, 0.40f);
                lockRect.anchorMax = new Vector2(0.70f, 0.65f);
                lockRect.offsetMin = Vector2.zero;
                lockRect.offsetMax = Vector2.zero;
                var lockText = lockGO.AddComponent<Text>();
                lockText.text = "\u26BF";
                lockText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lockText.fontSize = 16;
                lockText.alignment = TextAnchor.MiddleCenter;
                lockText.color = new Color(0.80f, 0.65f, 0.25f, 0.55f);
                lockText.raycastTarget = false;

                previewIndex++;
            }
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
        /// <summary>P&C: Returns a short description of what this building provides at its current tier.</summary>
        private static string GetBuildingBonusDescription(string buildingId, int tier)
        {
            return buildingId switch
            {
                "stronghold" => $"Power +{tier * 500}  |  Unlocks buildings up to Tier {tier}",
                "grain_farm" => $"Grain production: {100 * tier}/hr",
                "iron_mine" => $"Iron production: {80 * tier}/hr",
                "stone_quarry" => $"Stone production: {80 * tier}/hr",
                "arcane_tower" => $"Arcane Essence: {40 * tier}/hr  |  Magic defense +{tier * 2}%",
                "barracks" => $"Troop capacity: {200 * tier}  |  Train speed +{tier * 5}%",
                "training_ground" => $"Troop ATK +{tier * 3}%  |  March speed +{tier * 2}%",
                "forge" => $"Equipment slots: {tier + 1}  |  Craft speed +{tier * 4}%",
                "armory" => $"Troop DEF +{tier * 3}%  |  Equipment storage: {tier * 5}",
                "academy" => $"Research speed +{tier * 5}%",
                "library" => $"Research capacity: {tier}  |  Knowledge +{tier * 50}",
                "archive" => $"Tech tree depth +{tier}  |  Research cost -{tier * 2}%",
                "observatory" => $"Scout range +{tier * 10}%  |  Enemy info detail +{tier}",
                "laboratory" => $"Potion slots: {tier + 1}  |  Brew speed +{tier * 4}%",
                "marketplace" => $"Trade capacity: {tier * 3}  |  Tax rate: {Mathf.Max(1, 10 - tier)}%",
                "embassy" => $"Alliance help slots: {tier + 2}  |  Rally capacity +{tier * 100}",
                "hero_shrine" => $"Hero XP gain +{tier * 5}%  |  Skill points +{tier}",
                "wall" => $"Wall HP: {1000 * tier}  |  Trap slots: {tier * 2}",
                "watch_tower" => $"Scout range +{tier * 15}%  |  Alert radius +{tier * 10}%",
                "guild_hall" => $"Guild buffs +{tier * 3}%  |  Event points +{tier * 5}%",
                "enchanting_tower" => $"Enchant slots: {tier + 1}  |  Success rate +{tier * 3}%",
                _ => $"Power bonus: +{tier * 100}",
            };
        }

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
                // P&C: Update countdown timer text above bar
                var countdown = existingBar.Find("CountdownTimer");
                if (countdown != null)
                {
                    var cdText = countdown.GetComponentInChildren<Text>();
                    if (cdText != null)
                        cdText.text = FormatTimeRemaining(Mathf.RoundToInt(entry.RemainingSeconds));
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

            // Percentage text (inside bar)
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

            // P&C: Large countdown timer ABOVE the progress bar (very prominent like P&C)
            var countdownGO = new GameObject("CountdownTimer");
            countdownGO.transform.SetParent(bar.transform, false);
            var cdRect = countdownGO.AddComponent<RectTransform>();
            cdRect.anchorMin = new Vector2(-0.05f, 1.2f);
            cdRect.anchorMax = new Vector2(1.05f, 4.5f);
            cdRect.offsetMin = Vector2.zero;
            cdRect.offsetMax = Vector2.zero;
            // Dark bg pill for readability
            var cdBg = countdownGO.AddComponent<Image>();
            cdBg.color = new Color(0.05f, 0.03f, 0.10f, 0.85f);
            cdBg.raycastTarget = false;
            var cdOutline = countdownGO.AddComponent<Outline>();
            cdOutline.effectColor = new Color(0.40f, 0.80f, 0.35f, 0.5f);
            cdOutline.effectDistance = new Vector2(0.5f, -0.5f);
            var cdTextGO = new GameObject("Text");
            cdTextGO.transform.SetParent(countdownGO.transform, false);
            var cdTextRect = cdTextGO.AddComponent<RectTransform>();
            cdTextRect.anchorMin = Vector2.zero;
            cdTextRect.anchorMax = Vector2.one;
            cdTextRect.offsetMin = Vector2.zero;
            cdTextRect.offsetMax = Vector2.zero;
            var cdText = cdTextGO.AddComponent<Text>();
            int remSecs = Mathf.RoundToInt(remainingSeconds);
            cdText.text = FormatTimeRemaining(remSecs);
            cdText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cdText.fontSize = 9;
            cdText.fontStyle = FontStyle.Bold;
            cdText.alignment = TextAnchor.MiddleCenter;
            cdText.color = new Color(0.45f, 0.95f, 0.45f); // bright green like P&C
            cdText.raycastTarget = false;
            var cdShadow = cdTextGO.AddComponent<Shadow>();
            cdShadow.effectColor = new Color(0, 0, 0, 0.9f);
            cdShadow.effectDistance = new Vector2(0.5f, -0.5f);

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
        private GameObject _resourceCapGlowBar;

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

            // P&C: Pulsing red glow bar at top when ANY resource is near cap
            bool anyNearCap = IsNearResourceCap(rm, ResourceType.Stone)
                || IsNearResourceCap(rm, ResourceType.Iron)
                || IsNearResourceCap(rm, ResourceType.Grain)
                || IsNearResourceCap(rm, ResourceType.ArcaneEssence);

            if (anyNearCap && _resourceCapGlowBar == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    _resourceCapGlowBar = new GameObject("ResourceCapGlow");
                    _resourceCapGlowBar.transform.SetParent(canvas.transform, false);
                    _resourceCapGlowBar.transform.SetSiblingIndex(2);
                    var rect = _resourceCapGlowBar.AddComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 0.935f);
                    rect.anchorMax = new Vector2(1f, 0.96f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    var img = _resourceCapGlowBar.AddComponent<Image>();
                    img.color = new Color(0.9f, 0.2f, 0.1f, 0.4f);
                    img.raycastTarget = false;
                    StartCoroutine(PulseResourceCapGlow());
                }
            }
            else if (!anyNearCap && _resourceCapGlowBar != null)
            {
                Destroy(_resourceCapGlowBar);
                _resourceCapGlowBar = null;
            }
        }

        private IEnumerator PulseResourceCapGlow()
        {
            while (_resourceCapGlowBar != null)
            {
                var img = _resourceCapGlowBar.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.25f + 0.20f * Mathf.Sin(Time.time * 3f);
                    img.color = new Color(0.9f, 0.2f, 0.1f, pulse);
                }
                yield return null;
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
            int capturedTier = stronghold.Tier;
            btn.onClick.AddListener(() =>
            {
                string warning = GetUpgradeBlockReason(capturedInstanceId, capturedTier);
                if (warning != null) { ShowUpgradeBlockedToast(warning); return; }
                ShowUpgradeConfirmDialog(capturedInstanceId, "stronghold", capturedTier);
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
        // ====================================================================
        // P&C: Active upgrade strip — shows currently upgrading buildings as icons at bottom
        // ====================================================================

        /// <summary>P&C: Horizontal strip showing active upgrade icons + timers at bottom of screen.</summary>
        private void CreateActiveUpgradeStrip()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _activeUpgradeStrip = new GameObject("ActiveUpgradeStrip");
            _activeUpgradeStrip.transform.SetParent(canvas.transform, false);
            _activeUpgradeStrip.transform.SetAsLastSibling();

            var stripRect = _activeUpgradeStrip.AddComponent<RectTransform>();
            // Position above nav bar — leave left 18% for queue counter
            stripRect.anchorMin = new Vector2(0.42f, 0.105f);
            stripRect.anchorMax = new Vector2(0.99f, 0.145f);
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = Vector2.zero;

            // Start live update coroutine
            StartCoroutine(UpdateActiveUpgradeStrip());
        }

        private IEnumerator UpdateActiveUpgradeStrip()
        {
            while (_activeUpgradeStrip != null)
            {
                // Clear previous children
                for (int c = _activeUpgradeStrip.transform.childCount - 1; c >= 0; c--)
                    Destroy(_activeUpgradeStrip.transform.GetChild(c).gameObject);

                // P&C IT99: Queue slot counter (always shown when strip exists)
                int queueCount = 0;
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                    queueCount = bm.BuildQueue.Count;
                CreateQueueSlotCounter(_activeUpgradeStrip, queueCount);

                if (bm != null && queueCount > 0)
                {
                    int count = queueCount;
                    float slotW = Mathf.Min(0.48f, 0.80f / count);

                    for (int i = 0; i < count; i++)
                    {
                        var entry = bm.BuildQueue[i];

                        // Find building name
                        string displayName = entry.PlacedId;
                        foreach (var p in _placements)
                        {
                            if (p.InstanceId == entry.PlacedId)
                            {
                                displayName = BuildingDisplayNames.TryGetValue(p.BuildingId, out var dn) ? dn : p.BuildingId;
                                break;
                            }
                        }

                        // Slot container
                        var slotGO = new GameObject($"UpgradeSlot_{i}");
                        slotGO.transform.SetParent(_activeUpgradeStrip.transform, false);
                        var slotRect = slotGO.AddComponent<RectTransform>();
                        float x0 = 0.20f + i * slotW; // Start after queue counter
                        slotRect.anchorMin = new Vector2(x0, 0);
                        slotRect.anchorMax = new Vector2(x0 + slotW - 0.01f, 1);
                        slotRect.offsetMin = Vector2.zero;
                        slotRect.offsetMax = Vector2.zero;

                        var slotBg = slotGO.AddComponent<Image>();
                        slotBg.color = new Color(0.06f, 0.04f, 0.10f, 0.88f);
                        slotBg.raycastTarget = true;
                        var slotOutline = slotGO.AddComponent<Outline>();
                        slotOutline.effectColor = new Color(0.80f, 0.60f, 0.15f, 0.50f);
                        slotOutline.effectDistance = new Vector2(0.8f, -0.8f);

                        // Tap to navigate to building
                        string capturedId = entry.PlacedId;
                        var slotBtn = slotGO.AddComponent<Button>();
                        slotBtn.targetGraphic = slotBg;
                        slotBtn.onClick.AddListener(() => CenterOnBuilding(capturedId));

                        // Timer text
                        int secs = Mathf.CeilToInt(entry.RemainingSeconds);
                        string timeStr = secs >= 3600
                            ? $"{secs / 3600}h{(secs % 3600) / 60:D2}m"
                            : secs >= 60
                                ? $"{secs / 60}m{secs % 60:D2}s"
                                : $"{secs}s";

                        AddInfoPanelText(slotGO.transform, "Timer",
                            $"\u2692 {displayName}: {timeStr}", 8, FontStyle.Bold,
                            new Color(0.95f, 0.85f, 0.40f),
                            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                        // Mini progress bar at bottom of slot
                        float elapsed = (float)(System.DateTime.Now - entry.StartTime).TotalSeconds;
                        float total = elapsed + entry.RemainingSeconds;
                        float progress = total > 0 ? Mathf.Clamp01(elapsed / total) : 0f;

                        var miniBarGO = new GameObject("MiniBar");
                        miniBarGO.transform.SetParent(slotGO.transform, false);
                        var miniBarRect = miniBarGO.AddComponent<RectTransform>();
                        miniBarRect.anchorMin = new Vector2(0, 0);
                        miniBarRect.anchorMax = new Vector2(progress, 0.12f);
                        miniBarRect.offsetMin = Vector2.zero;
                        miniBarRect.offsetMax = Vector2.zero;
                        var miniBarImg = miniBarGO.AddComponent<Image>();
                        miniBarImg.color = new Color(0.90f, 0.70f, 0.15f, 0.80f);
                        miniBarImg.raycastTarget = false;
                    }
                }
                // Refresh every 2 seconds
                yield return new WaitForSeconds(2f);
            }
        }

        private void CreateBuilderCountHUD()
        {
            if (_builderCountHUD != null) return;

            // Find root canvas
            Transform canvasRoot = transform;
            while (canvasRoot.parent != null && canvasRoot.parent.GetComponent<Canvas>() != null)
                canvasRoot = canvasRoot.parent;

            // P&C: Compact pill left of info panel — builder + power in one row
            var hud = new GameObject("BuilderCountHUD");
            hud.transform.SetParent(canvasRoot, false);
            hud.transform.SetAsLastSibling();

            var hudRect = hud.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0.005f, 0.895f);
            hudRect.anchorMax = new Vector2(0.185f, 0.940f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            var hudBg = hud.AddComponent<Image>();
            hudBg.color = new Color(0.06f, 0.04f, 0.10f, 0.90f);
            hudBg.raycastTarget = true;

            var hudBtn = hud.AddComponent<Button>();
            hudBtn.targetGraphic = hudBg;
            hudBtn.onClick.AddListener(ToggleBuildQueuePanel);

            var hudOutline = hud.AddComponent<Outline>();
            hudOutline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.6f);
            hudOutline.effectDistance = new Vector2(1f, -1f);

            // Builder count (top half)
            var builderGO = new GameObject("BuilderText");
            builderGO.transform.SetParent(hud.transform, false);
            var bRect = builderGO.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0, 0.50f);
            bRect.anchorMax = new Vector2(1, 1);
            bRect.offsetMin = new Vector2(3, 0);
            bRect.offsetMax = new Vector2(-3, 0);

            _builderCountText = builderGO.AddComponent<Text>();
            _builderCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _builderCountText.fontSize = 9;
            _builderCountText.fontStyle = FontStyle.Bold;
            _builderCountText.alignment = TextAnchor.MiddleCenter;
            _builderCountText.color = new Color(0.95f, 0.85f, 0.45f);
            _builderCountText.raycastTarget = false;
            _builderCountText.text = "\u2692 Builder 0/2";
            var bOutline = builderGO.AddComponent<Outline>();
            bOutline.effectColor = new Color(0, 0, 0, 0.8f);
            bOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // Power rating (bottom half)
            var powerGO = new GameObject("PowerText");
            powerGO.transform.SetParent(hud.transform, false);
            var pRect = powerGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0, 0);
            pRect.anchorMax = new Vector2(1, 0.50f);
            pRect.offsetMin = new Vector2(3, 0);
            pRect.offsetMax = new Vector2(-3, 0);

            _powerRatingText = powerGO.AddComponent<Text>();
            _powerRatingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _powerRatingText.fontSize = 8;
            _powerRatingText.fontStyle = FontStyle.Bold;
            _powerRatingText.alignment = TextAnchor.MiddleCenter;
            _powerRatingText.color = new Color(0.95f, 0.70f, 0.35f);
            _powerRatingText.raycastTarget = false;
            var pOutline = powerGO.AddComponent<Outline>();
            pOutline.effectColor = new Color(0, 0, 0, 0.8f);
            pOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // Gold divider line
            var divider = new GameObject("Divider");
            divider.transform.SetParent(hud.transform, false);
            var dRect = divider.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.10f, 0.48f);
            dRect.anchorMax = new Vector2(0.90f, 0.52f);
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;
            var dImg = divider.AddComponent<Image>();
            dImg.color = new Color(0.70f, 0.55f, 0.15f, 0.35f);
            dImg.raycastTarget = false;

            _builderCountHUD = hud;
            UpdateBuilderCountHUD();
            UpdatePowerRatingHUD();
        }

        // _powerRatingHUD merged into BuilderCountHUD — no separate GO needed
        private Text _powerRatingText;

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
            int total = _hasSecondBuilder ? 3 : 2; // 2 free + 1 premium builder
            _builderCountText.text = $"\u2692 Builder {used}/{total}";
            _builderCountText.color = used >= total
                ? new Color(1f, 0.45f, 0.35f) // Red when full
                : new Color(0.95f, 0.85f, 0.45f); // Gold when available
        }

        // ====================================================================
        // P&C: Building Quick-Nav Sidebar
        // ====================================================================

        /// <summary>
        /// P&C: Small category buttons on the right edge of the screen.
        /// Tapping a category scrolls and zooms to the first building of that type.
        /// </summary>
        private void CreateBuildingQuickNav()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // P&C: Right-side circular category buttons, stacked vertically
            var categories = new (string icon, string label, string buildingId, Color color)[]
            {
                ("\u265B", "SH", "stronghold", new Color(0.95f, 0.75f, 0.20f)),
                ("\u2694", "MIL", "barracks", new Color(0.90f, 0.35f, 0.30f)),
                ("\u26CF", "RES", "grain_farm", new Color(0.45f, 0.80f, 0.40f)),
                ("\u2728", "MAG", "arcane_tower", new Color(0.60f, 0.40f, 0.90f)),
                ("\u26E8", "DEF", "wall", new Color(0.50f, 0.65f, 0.80f)),
                ("\u2609", "SCI", "academy", new Color(0.40f, 0.75f, 0.85f)),
            };

            float iconW = 0.072f;
            float iconH = 0.050f;
            float gap = 0.010f;
            float totalH = categories.Length * iconH + (categories.Length - 1) * gap;
            float startY = 0.68f + totalH * 0.5f; // upper right, above event icons
            float xMin = 1f - iconW - 0.005f;
            float xMax = 1f - 0.005f;

            var radialSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");

            for (int i = 0; i < categories.Length; i++)
            {
                var cat = categories[i];
                float yTop = startY - i * (iconH + gap);
                float yBot = yTop - iconH;

                var btnGO = new GameObject($"Nav_{cat.label}");
                btnGO.transform.SetParent(canvas.transform, false);
                var btnRect = btnGO.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(xMin, yBot);
                btnRect.anchorMax = new Vector2(xMax, yTop);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                // Circular bg
                var btnBg = btnGO.AddComponent<Image>();
                btnBg.color = new Color(cat.color.r * 0.30f, cat.color.g * 0.30f, cat.color.b * 0.30f, 0.88f);
                if (radialSpr != null) { btnBg.sprite = radialSpr; btnBg.type = Image.Type.Simple; }
                btnBg.raycastTarget = true;

                var btnOutline = btnGO.AddComponent<Outline>();
                btnOutline.effectColor = new Color(cat.color.r, cat.color.g, cat.color.b, 0.55f);
                btnOutline.effectDistance = new Vector2(0.8f, -0.8f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                string targetBuildingId = cat.buildingId;
                string catLabel = cat.label;
                btn.onClick.AddListener(() =>
                {
                    if (_activeCategoryFilter == catLabel) { ClearCategoryFilter(); return; }
                    ScrollToBuildingType(targetBuildingId);
                });

                var longPress = btnGO.AddComponent<LongPressDetector>();
                longPress.OnLongPress = () => ToggleCategoryFilter(catLabel);

                // Icon
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(btnGO.transform, false);
                var iconRect = iconGO.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.10f, 0.20f);
                iconRect.anchorMax = new Vector2(0.90f, 0.80f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconText = iconGO.AddComponent<Text>();
                iconText.text = cat.icon;
                iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                iconText.fontSize = 12;
                iconText.fontStyle = FontStyle.Bold;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = cat.color;
                iconText.raycastTarget = false;
                var shadow = iconGO.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.9f);
                shadow.effectDistance = new Vector2(0.4f, -0.4f);
            }
        }

        /// <summary>P&C: Scroll and zoom to the first building of a given type.</summary>
        private void ScrollToBuildingType(string buildingId)
        {
            CityBuildingPlacement target = null;
            foreach (var p in _placements)
            {
                if (p.BuildingId == buildingId && p.VisualGO != null)
                {
                    target = p;
                    break;
                }
            }
            if (target == null) return;

            // Center on this building
            var buildingRect = target.VisualGO.GetComponent<RectTransform>();
            if (buildingRect != null && contentContainer != null)
            {
                Vector2 targetPos = -(buildingRect.anchoredPosition * _currentZoom);
                StartCoroutine(SmoothScrollTo(targetPos, 0.4f));
            }

            // Flash the building to highlight it
            if (target.VisualGO != null)
                StartCoroutine(FlashBuildingHighlight(target.VisualGO));

            PlaySfx(_sfxTap);
        }

        private IEnumerator SmoothScrollTo(Vector2 targetPos, float duration)
        {
            if (contentContainer == null) yield break;
            Vector2 startPos = contentContainer.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                contentContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
                yield return null;
            }
            contentContainer.anchoredPosition = targetPos;
        }

        private IEnumerator FlashBuildingHighlight(GameObject building)
        {
            var img = building.GetComponent<Image>();
            if (img == null) yield break;
            Color original = img.color;

            for (int i = 0; i < 3; i++)
            {
                img.color = new Color(1f, 0.95f, 0.70f, 1f);
                yield return new WaitForSeconds(0.12f);
                img.color = original;
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ====================================================================
        // P&C: Building Category Filter
        // ====================================================================

        private static readonly Dictionary<string, string[]> CategoryBuildingIds = new()
        {
            { "SH", new[] { "stronghold" } },
            { "MIL", new[] { "barracks", "training_ground", "armory" } },
            { "RES", new[] { "grain_farm", "iron_mine", "stone_quarry" } },
            { "MAG", new[] { "arcane_tower", "enchanting_tower", "laboratory", "observatory" } },
            { "DEF", new[] { "wall", "watch_tower" } },
            { "SCI", new[] { "academy", "library", "archive" } },
        };

        private void ToggleCategoryFilter(string category)
        {
            if (_activeCategoryFilter == category)
            {
                ClearCategoryFilter();
                return;
            }
            _activeCategoryFilter = category;
            ApplyCategoryFilter(category);
            PlaySfx(_sfxTap);
        }

        private void ApplyCategoryFilter(string category)
        {
            if (!CategoryBuildingIds.TryGetValue(category, out var matchIds)) return;
            var matchSet = new HashSet<string>(matchIds);

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var img = p.VisualGO.GetComponent<Image>();
                if (img == null) continue;

                bool matches = matchSet.Contains(p.BuildingId);
                // Dim non-matching buildings, highlight matching ones
                img.color = matches ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.4f);

                // Scale effect: matching buildings slightly larger
                p.VisualGO.transform.localScale = matches ? Vector3.one * 1.05f : Vector3.one * 0.90f;
            }

            // Show filter banner
            ShowCategoryFilterBanner(category);
        }

        private void ClearCategoryFilter()
        {
            _activeCategoryFilter = null;
            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var img = p.VisualGO.GetComponent<Image>();
                if (img != null) img.color = Color.white;
                p.VisualGO.transform.localScale = Vector3.one;
            }
            DismissCategoryFilterBanner();
            PlaySfx(_sfxTap);
        }

        private GameObject _categoryFilterBanner;

        private void ShowCategoryFilterBanner(string category)
        {
            DismissCategoryFilterBanner();
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _categoryFilterBanner = new GameObject("FilterBanner");
            _categoryFilterBanner.transform.SetParent(canvas.transform, false);
            var rect = _categoryFilterBanner.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.20f, 0.82f);
            rect.anchorMax = new Vector2(0.80f, 0.87f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _categoryFilterBanner.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.18f, 0.90f);
            bg.raycastTarget = true;
            var outline = _categoryFilterBanner.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Tap banner to clear filter
            var btn = _categoryFilterBanner.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(ClearCategoryFilter);

            int matchCount = 0;
            if (CategoryBuildingIds.TryGetValue(category, out var ids))
            {
                var matchSet = new HashSet<string>(ids);
                foreach (var p in _placements) { if (matchSet.Contains(p.BuildingId)) matchCount++; }
            }

            AddInfoPanelText(_categoryFilterBanner.transform, "Text",
                $"\u25C9 Showing: {category} ({matchCount} buildings)  —  Tap to clear",
                10, FontStyle.Bold, new Color(0.95f, 0.82f, 0.35f),
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }

        private void DismissCategoryFilterBanner()
        {
            if (_categoryFilterBanner != null) { Destroy(_categoryFilterBanner); _categoryFilterBanner = null; }
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

            // P&C: Upgrade advisor suggestion
            if (queue.Count < totalSlots)
            {
                var suggestion = GetUpgradeAdvisorSuggestion();
                if (suggestion.HasValue)
                {
                    var sug = suggestion.Value;
                    var sugGO = new GameObject("AdvisorSuggestion");
                    sugGO.transform.SetParent(_buildQueuePanel.transform, false);
                    var sugRect = sugGO.AddComponent<RectTransform>();
                    sugRect.anchorMin = new Vector2(0.03f, -0.02f);
                    sugRect.anchorMax = new Vector2(0.97f, 0.08f);
                    sugRect.offsetMin = Vector2.zero;
                    sugRect.offsetMax = Vector2.zero;
                    var sugBg = sugGO.AddComponent<Image>();
                    sugBg.color = new Color(0.12f, 0.20f, 0.15f, 0.90f);
                    sugBg.raycastTarget = true;
                    var sugOutline = sugGO.AddComponent<Outline>();
                    sugOutline.effectColor = new Color(0.40f, 0.85f, 0.45f, 0.5f);
                    sugOutline.effectDistance = new Vector2(0.6f, -0.6f);
                    var sugBtn = sugGO.AddComponent<Button>();
                    sugBtn.targetGraphic = sugBg;
                    string sugInstanceId = sug.InstanceId;
                    string sugBuildingId = sug.BuildingId;
                    int sugTier = sug.Tier;
                    sugBtn.onClick.AddListener(() =>
                    {
                        Destroy(_buildQueuePanel);
                        _buildQueuePanel = null;
                        ShowBuildingInfoPanel(sugBuildingId, sugInstanceId, sugTier);
                    });
                    string sugName = BuildingDisplayNames.TryGetValue(sug.BuildingId, out var n) ? n : sug.BuildingId;
                    AddInfoPanelText(sugGO.transform, "Text",
                        $"\u2728 Advisor: Upgrade {sugName} Lv.{sug.Tier}\u2192{sug.Tier + 1} ({sug.Reason})", 9,
                        FontStyle.Bold, new Color(0.60f, 0.95f, 0.65f),
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                }
            }

            // P&C: Second builder promo button (if not purchased)
            if (!_hasSecondBuilder)
            {
                var promoGO = new GameObject("2ndBuilderPromo");
                promoGO.transform.SetParent(_buildQueuePanel.transform, false);
                var promoRect = promoGO.AddComponent<RectTransform>();
                promoRect.anchorMin = new Vector2(0.03f, -0.12f);
                promoRect.anchorMax = new Vector2(0.97f, -0.03f);
                promoRect.offsetMin = Vector2.zero;
                promoRect.offsetMax = Vector2.zero;
                var promoBg = promoGO.AddComponent<Image>();
                promoBg.color = new Color(0.45f, 0.20f, 0.60f, 0.90f);
                promoBg.raycastTarget = true;
                var promoOutline = promoGO.AddComponent<Outline>();
                promoOutline.effectColor = new Color(0.70f, 0.50f, 0.90f, 0.6f);
                promoOutline.effectDistance = new Vector2(0.6f, -0.6f);
                var promoBtn = promoGO.AddComponent<Button>();
                promoBtn.targetGraphic = promoBg;
                promoBtn.onClick.AddListener(() =>
                {
                    Destroy(_buildQueuePanel);
                    _buildQueuePanel = null;
                    ShowSecondBuilderPanel();
                });
                AddInfoPanelText(promoGO.transform, "Text",
                    "\u2692\u2692 Get 2nd Builder — Build Faster!", 9,
                    FontStyle.Bold, new Color(0.95f, 0.85f, 0.50f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                // Pulse the promo
                StartCoroutine(PulseSecondBuilderPromo(promoGO));
            }

            // Auto-dismiss after 8 seconds (longer for richer panel)
            StartCoroutine(AutoDismissQueuePanel());
        }

        private IEnumerator PulseSecondBuilderPromo(GameObject promo)
        {
            while (promo != null)
            {
                var img = promo.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                    img.color = new Color(0.45f * pulse, 0.20f, 0.60f * pulse, 0.90f);
                }
                yield return null;
            }
        }

        private struct UpgradeAdvisorSuggestion
        {
            public string InstanceId;
            public string BuildingId;
            public int Tier;
            public string Reason;
        }

        private UpgradeAdvisorSuggestion? GetUpgradeAdvisorSuggestion()
        {
            // Priority: 1) Stronghold if behind, 2) Lowest-tier resource building, 3) Lowest military
            int shTier = 1;
            string shId = null;
            foreach (var p in _placements)
            {
                if (p.BuildingId == "stronghold") { shTier = p.Tier; shId = p.InstanceId; break; }
            }

            // If stronghold isn't max, check if any buildings are at SH level (meaning SH needs upgrade to raise cap)
            if (shTier < 3 && shId != null)
            {
                int atCap = 0;
                foreach (var p in _placements)
                {
                    if (p.BuildingId != "stronghold" && p.Tier >= shTier) atCap++;
                }
                if (atCap >= 3) // 3+ buildings at cap = suggest SH upgrade
                    return new UpgradeAdvisorSuggestion
                    { InstanceId = shId, BuildingId = "stronghold", Tier = shTier, Reason = $"{atCap} buildings at cap" };
            }

            // Find lowest-tier resource building
            CityBuildingPlacement lowestRes = null;
            foreach (var p in _placements)
            {
                if (!ResourceBuildingTypes.ContainsKey(p.BuildingId)) continue;
                if (p.Tier >= 3) continue;
                if (lowestRes == null || p.Tier < lowestRes.Tier) lowestRes = p;
            }
            if (lowestRes != null)
                return new UpgradeAdvisorSuggestion
                { InstanceId = lowestRes.InstanceId, BuildingId = lowestRes.BuildingId, Tier = lowestRes.Tier,
                    Reason = "boost production" };

            // Find lowest-tier military building
            CityBuildingPlacement lowestMil = null;
            string[] milTypes = { "barracks", "training_ground", "armory" };
            foreach (var p in _placements)
            {
                if (System.Array.IndexOf(milTypes, p.BuildingId) < 0) continue;
                if (p.Tier >= 3) continue;
                if (lowestMil == null || p.Tier < lowestMil.Tier) lowestMil = p;
            }
            if (lowestMil != null)
                return new UpgradeAdvisorSuggestion
                { InstanceId = lowestMil.InstanceId, BuildingId = lowestMil.BuildingId, Tier = lowestMil.Tier,
                    Reason = "increase army power" };

            return null;
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
        // P&C: Troop Training Panel
        // ====================================================================

        private GameObject _troopTrainingPanel;

        private void ShowTroopTrainingPanel(string instanceId, string buildingId, int tier)
        {
            if (_troopTrainingPanel != null) { Destroy(_troopTrainingPanel); _troopTrainingPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _troopTrainingPanel = new GameObject("TroopTrainingPanel");
            _troopTrainingPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _troopTrainingPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _troopTrainingPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _troopTrainingPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_troopTrainingPanel != null) { Destroy(_troopTrainingPanel); _troopTrainingPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_troopTrainingPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.25f);
            panelRect.anchorMax = new Vector2(0.90f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.90f, 0.35f, 0.25f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            bool isBarracks = buildingId == "barracks";
            string troopType = isBarracks ? "Infantry" : "Specialists";

            // Title
            AddInfoPanelText(panel.transform, "Title", $"\u2694 {bName} — Train {troopType}", 15, FontStyle.Bold,
                new Color(0.95f, 0.40f, 0.30f),
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);

            // Troop tiers (T1, T2, T3 based on building tier)
            float y = 0.78f;
            for (int t = 1; t <= tier; t++)
            {
                string tierName = t switch
                {
                    1 => isBarracks ? "Recruits" : "Scouts",
                    2 => isBarracks ? "Soldiers" : "Rangers",
                    3 => isBarracks ? "Veterans" : "Elites",
                    _ => "Troops"
                };
                int batchSize = isBarracks ? 100 * t : 50 * t;
                int trainSec = isBarracks ? 30 * t : 45 * t;
                int grainCost = 50 * t;
                int ironCost = isBarracks ? 30 * t : 20 * t;
                int power = isBarracks ? 10 * t : 15 * t;

                string timeStr = FormatTimeRemaining(trainSec);

                // Tier row background
                var rowGO = new GameObject($"TroopTier{t}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.05f, y - 0.18f);
                rowRect.anchorMax = new Vector2(0.95f, y);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = new Color(0.12f, 0.08f, 0.16f, 0.80f);
                rowBg.raycastTarget = false;
                var rowOutline = rowGO.AddComponent<Outline>();
                rowOutline.effectColor = new Color(0.60f, 0.40f, 0.30f, 0.3f);
                rowOutline.effectDistance = new Vector2(0.5f, -0.5f);

                // Tier name + stats
                AddInfoPanelText(rowGO.transform, "Name", $"T{t} {tierName}", 12, FontStyle.Bold,
                    new Color(0.95f, 0.80f, 0.35f),
                    new Vector2(0.03f, 0.55f), new Vector2(0.50f, 0.95f), TextAnchor.MiddleLeft);
                AddInfoPanelText(rowGO.transform, "Stats",
                    $"x{batchSize}  |  \u2694+{power * batchSize}  |  \u23F1{timeStr}",
                    9, FontStyle.Normal, new Color(0.75f, 0.72f, 0.68f),
                    new Vector2(0.03f, 0.10f), new Vector2(0.60f, 0.50f), TextAnchor.MiddleLeft);
                AddInfoPanelText(rowGO.transform, "Cost",
                    $"Cost: {grainCost} Grain, {ironCost} Iron", 8, FontStyle.Normal,
                    new Color(0.65f, 0.62f, 0.58f),
                    new Vector2(0.55f, 0.10f), new Vector2(0.95f, 0.50f), TextAnchor.MiddleRight);

                // Train button
                var trainBtnGO = new GameObject("TrainBtn");
                trainBtnGO.transform.SetParent(rowGO.transform, false);
                var trainBtnRect = trainBtnGO.AddComponent<RectTransform>();
                trainBtnRect.anchorMin = new Vector2(0.65f, 0.55f);
                trainBtnRect.anchorMax = new Vector2(0.97f, 0.95f);
                trainBtnRect.offsetMin = Vector2.zero;
                trainBtnRect.offsetMax = Vector2.zero;
                var trainBtnBg = trainBtnGO.AddComponent<Image>();
                trainBtnBg.color = new Color(0.55f, 0.18f, 0.15f, 0.92f);
                trainBtnBg.raycastTarget = true;
                var trainBtnOutln = trainBtnGO.AddComponent<Outline>();
                trainBtnOutln.effectColor = new Color(0.90f, 0.45f, 0.35f, 0.5f);
                trainBtnOutln.effectDistance = new Vector2(0.4f, -0.4f);
                var trainBtnComp = trainBtnGO.AddComponent<Button>();
                trainBtnComp.targetGraphic = trainBtnBg;
                int capBatch = batchSize;
                int capTierNum = t;
                trainBtnComp.onClick.AddListener(() =>
                {
                    Debug.Log($"[TroopTraining] Queued {capBatch} T{capTierNum} {troopType} from {bName}.");
                    if (_troopTrainingPanel != null) { Destroy(_troopTrainingPanel); _troopTrainingPanel = null; }
                    ShowUpgradeBlockedToast($"\u2694 Training {capBatch} T{capTierNum} {troopType}...");
                });
                AddInfoPanelText(trainBtnGO.transform, "Label", "TRAIN", 9, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                y -= 0.22f;
            }

            // Close button
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
            closeBtn.onClick.AddListener(() => { if (_troopTrainingPanel != null) { Destroy(_troopTrainingPanel); _troopTrainingPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _troopTrainingPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Research Quick Panel (from academy/library info panel)
        // ====================================================================

        private GameObject _researchQuickPanel;

        private void ShowResearchQuickPanel(int academyTier)
        {
            if (_researchQuickPanel != null) { Destroy(_researchQuickPanel); _researchQuickPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            ServiceLocator.TryGet<ResearchManager>(out var researchMgr);
            if (researchMgr == null) { ShowUpgradeBlockedToast("Research not available"); return; }

            _researchQuickPanel = new GameObject("ResearchQuickPanel");
            _researchQuickPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _researchQuickPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _researchQuickPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _researchQuickPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_researchQuickPanel != null) { Destroy(_researchQuickPanel); _researchQuickPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_researchQuickPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.15f);
            panelRect.anchorMax = new Vector2(0.94f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.06f, 0.14f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.30f, 0.55f, 0.90f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            AddInfoPanelText(panel.transform, "Title", "\u2726 Research Tree", 15, FontStyle.Bold,
                new Color(0.50f, 0.75f, 0.95f),
                new Vector2(0.05f, 0.91f), new Vector2(0.75f, 0.99f), TextAnchor.MiddleLeft);

            // Active research indicator
            if (researchMgr.ResearchQueue.Count > 0)
            {
                var active = researchMgr.ResearchQueue[0];
                var activeNode = researchMgr.GetNode(active.NodeId);
                string activeName = activeNode != null ? activeNode.displayName : active.NodeId;
                string timeLeft = FormatTimeRemaining((int)active.RemainingSeconds);
                AddInfoPanelText(panel.transform, "ActiveResearch",
                    $"\u23F1 Researching: {activeName} ({timeLeft})", 10, FontStyle.Italic,
                    new Color(0.70f, 0.85f, 1.0f),
                    new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.91f), TextAnchor.MiddleLeft);
            }
            else
            {
                AddInfoPanelText(panel.transform, "ActiveResearch",
                    "No active research — pick one below!", 10, FontStyle.Italic,
                    new Color(0.55f, 0.55f, 0.45f),
                    new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.91f), TextAnchor.MiddleLeft);
            }

            // Branch tabs
            var branches = new[] {
                (Data.ResearchBranch.Military, "Military", new Color(0.85f, 0.30f, 0.25f)),
                (Data.ResearchBranch.Resource, "Resource", new Color(0.30f, 0.75f, 0.35f)),
                (Data.ResearchBranch.Research, "Science", new Color(0.35f, 0.55f, 0.90f)),
                (Data.ResearchBranch.Hero, "Hero", new Color(0.80f, 0.60f, 0.20f))
            };
            float tabX = 0.03f;
            float tabW = 0.23f;
            for (int b = 0; b < branches.Length; b++)
            {
                var (branch, label, tint) = branches[b];
                var tabGO = new GameObject($"Tab_{label}");
                tabGO.transform.SetParent(panel.transform, false);
                var tabRect = tabGO.AddComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(tabX + b * tabW + 0.005f, 0.78f);
                tabRect.anchorMax = new Vector2(tabX + (b + 1) * tabW - 0.005f, 0.84f);
                tabRect.offsetMin = Vector2.zero;
                tabRect.offsetMax = Vector2.zero;
                var tabBg = tabGO.AddComponent<Image>();
                tabBg.color = new Color(tint.r * 0.4f, tint.g * 0.4f, tint.b * 0.4f, 0.80f);
                tabBg.raycastTarget = true;
                var tabOutline = tabGO.AddComponent<Outline>();
                tabOutline.effectColor = new Color(tint.r, tint.g, tint.b, 0.5f);
                tabOutline.effectDistance = new Vector2(0.5f, -0.5f);
                AddInfoPanelText(tabGO.transform, "Label", label, 9, FontStyle.Bold, tint,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // Gather available nodes
            var available = new List<Data.ResearchNodeData>();
            var locked = new List<Data.ResearchNodeData>();
            foreach (var node in researchMgr.AllNodes)
            {
                if (researchMgr.IsCompleted(node.nodeId)) continue;
                if (researchMgr.ResearchQueue.Count > 0 && researchMgr.ResearchQueue[0].NodeId == node.nodeId) continue;
                if (node.requiredAcademyTier > academyTier)
                {
                    locked.Add(node);
                    continue;
                }
                if (researchMgr.IsAvailable(node.nodeId))
                    available.Add(node);
                else
                    locked.Add(node);
            }

            // Sort available by research time (quickest first)
            available.Sort((a, b) => a.researchTimeSeconds.CompareTo(b.researchTimeSeconds));

            // Scrollable area for nodes
            float yPos = 0.75f;
            float rowH = 0.10f;
            int maxVisible = 6;
            int shown = 0;

            // Available nodes
            if (available.Count > 0)
            {
                AddInfoPanelText(panel.transform, "AvailHeader", "AVAILABLE", 9, FontStyle.Bold,
                    new Color(0.40f, 0.80f, 0.45f),
                    new Vector2(0.05f, yPos - 0.035f), new Vector2(0.50f, yPos), TextAnchor.MiddleLeft);
                yPos -= 0.04f;

                foreach (var node in available)
                {
                    if (shown >= maxVisible) break;

                    Color branchColor = node.branch switch
                    {
                        Data.ResearchBranch.Military => new Color(0.85f, 0.35f, 0.30f),
                        Data.ResearchBranch.Resource => new Color(0.35f, 0.75f, 0.40f),
                        Data.ResearchBranch.Research => new Color(0.40f, 0.60f, 0.90f),
                        Data.ResearchBranch.Hero => new Color(0.85f, 0.65f, 0.25f),
                        _ => Color.white
                    };

                    var rowGO = new GameObject($"Node_{node.nodeId}");
                    rowGO.transform.SetParent(panel.transform, false);
                    var rowRect = rowGO.AddComponent<RectTransform>();
                    rowRect.anchorMin = new Vector2(0.03f, yPos - rowH);
                    rowRect.anchorMax = new Vector2(0.97f, yPos);
                    rowRect.offsetMin = Vector2.zero;
                    rowRect.offsetMax = Vector2.zero;
                    var rowBg = rowGO.AddComponent<Image>();
                    rowBg.color = new Color(branchColor.r * 0.15f, branchColor.g * 0.15f, branchColor.b * 0.15f, 0.70f);
                    rowBg.raycastTarget = false;
                    var rowOutline = rowGO.AddComponent<Outline>();
                    rowOutline.effectColor = new Color(branchColor.r, branchColor.g, branchColor.b, 0.3f);
                    rowOutline.effectDistance = new Vector2(0.4f, -0.4f);

                    // Branch pip
                    string branchPip = node.branch switch
                    {
                        Data.ResearchBranch.Military => "\u2694",
                        Data.ResearchBranch.Resource => "\u2692",
                        Data.ResearchBranch.Research => "\u2726",
                        Data.ResearchBranch.Hero => "\u2605",
                        _ => "\u25CF"
                    };

                    AddInfoPanelText(rowGO.transform, "Name",
                        $"{branchPip} {node.displayName}", 10, FontStyle.Bold, branchColor,
                        new Vector2(0.02f, 0.50f), new Vector2(0.60f, 0.95f), TextAnchor.MiddleLeft);

                    // Cost summary
                    var costs = new List<string>();
                    if (node.stoneCost > 0) costs.Add($"{node.stoneCost} St");
                    if (node.ironCost > 0) costs.Add($"{node.ironCost} Ir");
                    if (node.grainCost > 0) costs.Add($"{node.grainCost} Gr");
                    if (node.arcaneEssenceCost > 0) costs.Add($"{node.arcaneEssenceCost} AE");
                    string costStr = costs.Count > 0 ? string.Join(", ", costs) : "Free";

                    string timeStr = FormatTimeRemaining(node.researchTimeSeconds);
                    AddInfoPanelText(rowGO.transform, "Info",
                        $"\u23F1 {timeStr}  |  {costStr}", 8, FontStyle.Normal,
                        new Color(0.65f, 0.62f, 0.58f),
                        new Vector2(0.02f, 0.05f), new Vector2(0.65f, 0.48f), TextAnchor.MiddleLeft);

                    // Effects preview
                    if (node.effects.Count > 0)
                    {
                        string effectStr = "";
                        foreach (var eff in node.effects)
                        {
                            if (effectStr.Length > 0) effectStr += ", ";
                            effectStr += $"+{eff.magnitude}% {eff.effectType.ToString().Replace("Percent", "")}";
                        }
                        if (effectStr.Length > 40) effectStr = effectStr.Substring(0, 37) + "...";
                        AddInfoPanelText(rowGO.transform, "Effects", effectStr, 7, FontStyle.Italic,
                            new Color(0.55f, 0.75f, 0.55f),
                            new Vector2(0.02f, 0.05f), new Vector2(0.65f, 0.30f), TextAnchor.MiddleLeft);
                    }

                    // Research button
                    bool queueFull = researchMgr.ResearchQueue.Count > 0;
                    var resBtnGO = new GameObject("ResearchBtn");
                    resBtnGO.transform.SetParent(rowGO.transform, false);
                    var resBtnRect = resBtnGO.AddComponent<RectTransform>();
                    resBtnRect.anchorMin = new Vector2(0.68f, 0.15f);
                    resBtnRect.anchorMax = new Vector2(0.98f, 0.85f);
                    resBtnRect.offsetMin = Vector2.zero;
                    resBtnRect.offsetMax = Vector2.zero;
                    var resBtnBg = resBtnGO.AddComponent<Image>();
                    resBtnBg.color = queueFull
                        ? new Color(0.30f, 0.30f, 0.30f, 0.80f)
                        : new Color(0.20f, 0.45f, 0.70f, 0.92f);
                    resBtnBg.raycastTarget = true;
                    var resBtnOutln = resBtnGO.AddComponent<Outline>();
                    resBtnOutln.effectColor = queueFull
                        ? new Color(0.50f, 0.50f, 0.50f, 0.3f)
                        : new Color(0.40f, 0.70f, 0.95f, 0.5f);
                    resBtnOutln.effectDistance = new Vector2(0.4f, -0.4f);
                    var resBtnComp = resBtnGO.AddComponent<Button>();
                    resBtnComp.targetGraphic = resBtnBg;

                    string capNodeId = node.nodeId;
                    string capNodeName = node.displayName;
                    if (queueFull)
                    {
                        resBtnComp.onClick.AddListener(() =>
                        {
                            ShowUpgradeBlockedToast("Research queue is full");
                        });
                        AddInfoPanelText(resBtnGO.transform, "Label", "QUEUE FULL", 8, FontStyle.Normal,
                            new Color(0.60f, 0.55f, 0.50f), Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        resBtnComp.onClick.AddListener(() =>
                        {
                            if (researchMgr.StartResearch(capNodeId))
                            {
                                if (_researchQuickPanel != null) { Destroy(_researchQuickPanel); _researchQuickPanel = null; }
                                ShowUpgradeBlockedToast($"\u2726 Researching: {capNodeName}");
                            }
                            else
                            {
                                ShowUpgradeBlockedToast("Cannot start research — check resources");
                            }
                        });
                        AddInfoPanelText(resBtnGO.transform, "Label", "RESEARCH", 9, FontStyle.Bold, Color.white,
                            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                    }

                    yPos -= rowH + 0.01f;
                    shown++;
                }
            }

            // Locked nodes summary
            if (locked.Count > 0 && shown < maxVisible)
            {
                AddInfoPanelText(panel.transform, "LockedHeader",
                    $"\u26D4 {locked.Count} node{(locked.Count != 1 ? "s" : "")} locked (higher Academy / prerequisites needed)",
                    8, FontStyle.Italic, new Color(0.50f, 0.40f, 0.35f),
                    new Vector2(0.05f, yPos - 0.035f), new Vector2(0.95f, yPos), TextAnchor.MiddleLeft);
            }

            // Completed count
            int completedCount = researchMgr.CompletedNodeIds.Count;
            int totalNodes = 0;
            foreach (var _ in researchMgr.AllNodes) totalNodes++;
            AddInfoPanelText(panel.transform, "CompletedCount",
                $"\u2714 {completedCount}/{totalNodes} researched", 9, FontStyle.Normal,
                new Color(0.50f, 0.80f, 0.55f),
                new Vector2(0.05f, 0.01f), new Vector2(0.50f, 0.06f), TextAnchor.MiddleLeft);

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.92f);
            closeRect.anchorMax = new Vector2(0.98f, 1.0f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_researchQuickPanel != null) { Destroy(_researchQuickPanel); _researchQuickPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _researchQuickPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Crafting Panel (from forge/enchanting tower info panel)
        // ====================================================================

        private GameObject _craftingPanel;

        private void ShowCraftingPanel(string buildingId, int tier)
        {
            if (_craftingPanel != null) { Destroy(_craftingPanel); _craftingPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _craftingPanel = new GameObject("CraftingPanel");
            _craftingPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _craftingPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _craftingPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _craftingPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_craftingPanel != null) { Destroy(_craftingPanel); _craftingPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_craftingPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.20f);
            panelRect.anchorMax = new Vector2(0.92f, 0.80f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.raycastTarget = true;

            bool isForge = buildingId == "forge";
            Color accentColor = isForge
                ? new Color(0.90f, 0.55f, 0.20f)
                : new Color(0.60f, 0.35f, 0.85f);
            panelBg.color = isForge
                ? new Color(0.10f, 0.06f, 0.03f, 0.96f)
                : new Color(0.06f, 0.04f, 0.12f, 0.96f);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string panelTitle = isForge ? "\u2692 Equipment Forge" : "\u2728 Enchanting Workshop";
            AddInfoPanelText(panel.transform, "Title", panelTitle, 15, FontStyle.Bold,
                accentColor,
                new Vector2(0.05f, 0.88f), new Vector2(0.80f, 0.98f), TextAnchor.MiddleLeft);

            // Crafting categories depend on building type
            if (isForge)
            {
                // Forge: Weapon, Armor, Accessory crafting
                var categories = new[]
                {
                    ("Weapons", "\u2694", new Color(0.85f, 0.35f, 0.25f), new[] { ("Iron Sword", 80, 60, 120), ("Steel Blade", 160, 120, 240), ("Runic Greataxe", 320, 240, 480) }),
                    ("Armor", "\u26E8", new Color(0.45f, 0.60f, 0.80f), new[] { ("Leather Vest", 60, 40, 90), ("Chainmail", 120, 100, 180), ("Plate Armor", 250, 200, 360) }),
                    ("Accessories", "\u2B50", new Color(0.80f, 0.70f, 0.25f), new[] { ("Bronze Ring", 40, 30, 60), ("Silver Amulet", 100, 80, 150), ("Gold Talisman", 200, 160, 300) })
                };

                float yPos = 0.84f;
                foreach (var (catName, icon, catColor, items) in categories)
                {
                    AddInfoPanelText(panel.transform, $"Cat_{catName}",
                        $"{icon} {catName}", 11, FontStyle.Bold, catColor,
                        new Vector2(0.04f, yPos - 0.05f), new Vector2(0.50f, yPos), TextAnchor.MiddleLeft);
                    yPos -= 0.06f;

                    int itemIdx = 0;
                    foreach (var (itemName, ironCost, stoneCost, craftSec) in items)
                    {
                        if (itemIdx >= tier) break; // Higher tier unlocks more items
                        string timeStr = FormatTimeRemaining(craftSec);
                        int power = (itemIdx + 1) * 15;

                        var rowGO = new GameObject($"Item_{itemName}");
                        rowGO.transform.SetParent(panel.transform, false);
                        var rowRect = rowGO.AddComponent<RectTransform>();
                        rowRect.anchorMin = new Vector2(0.03f, yPos - 0.07f);
                        rowRect.anchorMax = new Vector2(0.97f, yPos);
                        rowRect.offsetMin = Vector2.zero;
                        rowRect.offsetMax = Vector2.zero;
                        var rowBg = rowGO.AddComponent<Image>();
                        rowBg.color = new Color(catColor.r * 0.12f, catColor.g * 0.12f, catColor.b * 0.12f, 0.65f);
                        rowBg.raycastTarget = false;

                        AddInfoPanelText(rowGO.transform, "Name", itemName, 9, FontStyle.Bold,
                            new Color(0.90f, 0.85f, 0.75f),
                            new Vector2(0.03f, 0.50f), new Vector2(0.50f, 0.95f), TextAnchor.MiddleLeft);
                        AddInfoPanelText(rowGO.transform, "Stats",
                            $"\u2694+{power}  |  {ironCost} Ir, {stoneCost} St  |  \u23F1{timeStr}",
                            7, FontStyle.Normal, new Color(0.65f, 0.60f, 0.55f),
                            new Vector2(0.03f, 0.05f), new Vector2(0.65f, 0.48f), TextAnchor.MiddleLeft);

                        // Craft button
                        var craftBtnGO = new GameObject("CraftBtn");
                        craftBtnGO.transform.SetParent(rowGO.transform, false);
                        var craftBtnRect = craftBtnGO.AddComponent<RectTransform>();
                        craftBtnRect.anchorMin = new Vector2(0.70f, 0.12f);
                        craftBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                        craftBtnRect.offsetMin = Vector2.zero;
                        craftBtnRect.offsetMax = Vector2.zero;
                        var craftBtnBg = craftBtnGO.AddComponent<Image>();
                        craftBtnBg.color = new Color(accentColor.r * 0.6f, accentColor.g * 0.6f, accentColor.b * 0.6f, 0.92f);
                        craftBtnBg.raycastTarget = true;
                        var craftBtnOutln = craftBtnGO.AddComponent<Outline>();
                        craftBtnOutln.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
                        craftBtnOutln.effectDistance = new Vector2(0.3f, -0.3f);
                        var craftBtnComp = craftBtnGO.AddComponent<Button>();
                        craftBtnComp.targetGraphic = craftBtnBg;
                        string capItemName = itemName;
                        craftBtnComp.onClick.AddListener(() =>
                        {
                            Debug.Log($"[Crafting] Started crafting {capItemName} at forge.");
                            if (_craftingPanel != null) { Destroy(_craftingPanel); _craftingPanel = null; }
                            ShowUpgradeBlockedToast($"\u2692 Crafting {capItemName}...");
                        });
                        AddInfoPanelText(craftBtnGO.transform, "Label", "CRAFT", 8, FontStyle.Bold, Color.white,
                            Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                        yPos -= 0.08f;
                        itemIdx++;
                    }
                    yPos -= 0.01f;
                }
            }
            else
            {
                // Enchanting Tower: enchant existing equipment, add runes
                var enchantOptions = new[]
                {
                    ("Sharpen Edge", "+5% ATK", 40, 30, 90),
                    ("Reinforce Plate", "+5% DEF", 40, 30, 90),
                    ("Arcane Infusion", "+3% Crit", 60, 50, 150),
                    ("Elemental Imbue", "+Fire/Ice/Shadow dmg", 80, 70, 200),
                    ("Rune Carving", "Add rune slot", 120, 100, 300),
                    ("Masterwork Polish", "+8% all stats", 200, 160, 480)
                };

                AddInfoPanelText(panel.transform, "SubTitle",
                    "Enhance equipment with magical enchantments", 9, FontStyle.Italic,
                    new Color(0.60f, 0.50f, 0.75f),
                    new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.88f), TextAnchor.MiddleLeft);

                float yPos = 0.80f;
                int shown = 0;
                foreach (var (name, effect, aeCost, ironCost, craftSec) in enchantOptions)
                {
                    if (shown >= tier * 2) break; // 2 enchants per tier unlocked
                    string timeStr = FormatTimeRemaining(craftSec);

                    var rowGO = new GameObject($"Enchant_{name}");
                    rowGO.transform.SetParent(panel.transform, false);
                    var rowRect = rowGO.AddComponent<RectTransform>();
                    rowRect.anchorMin = new Vector2(0.03f, yPos - 0.09f);
                    rowRect.anchorMax = new Vector2(0.97f, yPos);
                    rowRect.offsetMin = Vector2.zero;
                    rowRect.offsetMax = Vector2.zero;
                    var rowBg = rowGO.AddComponent<Image>();
                    rowBg.color = new Color(0.10f, 0.06f, 0.18f, 0.70f);
                    rowBg.raycastTarget = false;
                    var rowOutline = rowGO.AddComponent<Outline>();
                    rowOutline.effectColor = new Color(0.55f, 0.30f, 0.75f, 0.25f);
                    rowOutline.effectDistance = new Vector2(0.3f, -0.3f);

                    AddInfoPanelText(rowGO.transform, "Name",
                        $"\u2728 {name}", 10, FontStyle.Bold, new Color(0.75f, 0.55f, 0.90f),
                        new Vector2(0.03f, 0.50f), new Vector2(0.55f, 0.95f), TextAnchor.MiddleLeft);
                    AddInfoPanelText(rowGO.transform, "Effect", effect, 9, FontStyle.Normal,
                        new Color(0.60f, 0.85f, 0.65f),
                        new Vector2(0.03f, 0.05f), new Vector2(0.45f, 0.48f), TextAnchor.MiddleLeft);
                    AddInfoPanelText(rowGO.transform, "Cost",
                        $"{aeCost} AE, {ironCost} Ir  |  \u23F1{timeStr}", 7, FontStyle.Normal,
                        new Color(0.60f, 0.55f, 0.50f),
                        new Vector2(0.45f, 0.05f), new Vector2(0.68f, 0.48f), TextAnchor.MiddleRight);

                    // Enchant button
                    var enchBtnGO = new GameObject("EnchantBtn");
                    enchBtnGO.transform.SetParent(rowGO.transform, false);
                    var enchBtnRect = enchBtnGO.AddComponent<RectTransform>();
                    enchBtnRect.anchorMin = new Vector2(0.72f, 0.12f);
                    enchBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                    enchBtnRect.offsetMin = Vector2.zero;
                    enchBtnRect.offsetMax = Vector2.zero;
                    var enchBtnBg = enchBtnGO.AddComponent<Image>();
                    enchBtnBg.color = new Color(0.40f, 0.22f, 0.55f, 0.92f);
                    enchBtnBg.raycastTarget = true;
                    var enchBtnOutln = enchBtnGO.AddComponent<Outline>();
                    enchBtnOutln.effectColor = new Color(0.65f, 0.40f, 0.85f, 0.4f);
                    enchBtnOutln.effectDistance = new Vector2(0.3f, -0.3f);
                    var enchBtnComp = enchBtnGO.AddComponent<Button>();
                    enchBtnComp.targetGraphic = enchBtnBg;
                    string capName = name;
                    enchBtnComp.onClick.AddListener(() =>
                    {
                        Debug.Log($"[Enchanting] Started {capName} enchantment.");
                        if (_craftingPanel != null) { Destroy(_craftingPanel); _craftingPanel = null; }
                        ShowUpgradeBlockedToast($"\u2728 Enchanting: {capName}...");
                    });
                    AddInfoPanelText(enchBtnGO.transform, "Label", "ENCHANT", 8, FontStyle.Bold, Color.white,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                    yPos -= 0.10f;
                    shown++;
                }

                // Locked enchants hint
                int remaining = enchantOptions.Length - shown;
                if (remaining > 0)
                {
                    AddInfoPanelText(panel.transform, "LockedHint",
                        $"\u26D4 {remaining} more at higher tier", 8, FontStyle.Italic,
                        new Color(0.45f, 0.40f, 0.35f),
                        new Vector2(0.05f, yPos - 0.04f), new Vector2(0.95f, yPos), TextAnchor.MiddleLeft);
                }
            }

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.91f);
            closeRect.anchorMax = new Vector2(0.98f, 1.0f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_craftingPanel != null) { Destroy(_craftingPanel); _craftingPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _craftingPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
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
            // P&C: Start swipe-collect if over a resource building
            TryStartSwipeCollect(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasTap = _holdStarted && !_moveMode && !_isPinching
                          && _holdTimer < LongPressTime
                          && Vector2.Distance(eventData.position, _holdStartPos) < DragThreshold;

            _holdStarted = false;
            _holdTimer = 0f;

            // P&C: End swipe-collect
            EndSwipeCollect();

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
                // P&C: Dismiss radial popup when tapping empty ground
                DismissInfoPopup();
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

            // P&C: Category-specific audio + haptic feedback on building tap
            PlayBuildingTapSfx(tapped.BuildingId);
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

            // P&C IT99: Auto-collect all resource bubbles on tap
            TapCollectAllForBuilding(tapped.InstanceId, tapped.BuildingId);

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

        // ====================================================================
        // Diamond cell sprite cache (runtime-generated, no rotation needed)
        // ====================================================================
        private static Texture2D _diamondCellTex;
        private static Sprite _diamondCellSprite;

        /// <summary>
        /// Get or create a diamond-shaped sprite for grid cells.
        /// CellSize wide × CellSize/2 tall (64×32), with a 1px border baked in.
        /// White fill + white border — color via Image.color tint.
        /// </summary>
        private static Sprite GetDiamondCellSprite()
        {
            if (_diamondCellSprite != null) return _diamondCellSprite;

            int tw = (int)CellSize;      // 64
            int th = (int)(CellSize / 2); // 32
            _diamondCellTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            _diamondCellTex.filterMode = FilterMode.Bilinear;
            _diamondCellTex.wrapMode = TextureWrapMode.Clamp;

            int hw = tw / 2; // 32
            int hh = th / 2; // 16
            float borderWidth = 1.2f; // px

            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    // Diamond distance: |x-hw|/hw + |y-hh|/hh compared to 1
                    float dx = Mathf.Abs(x - hw) / (float)hw;
                    float dy = Mathf.Abs(y - hh) / (float)hh;
                    float d = dx + dy; // 0 at center, 1 at edge

                    if (d > 1.0f)
                    {
                        // Outside diamond
                        _diamondCellTex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                    else
                    {
                        // Border region: within borderWidth px of edge
                        float edgeDist = (1.0f - d) * hw; // approx pixels from edge
                        if (edgeDist < borderWidth)
                        {
                            // Border: full white, alpha based on distance for anti-aliasing
                            float a = Mathf.Clamp01(edgeDist / borderWidth);
                            _diamondCellTex.SetPixel(x, y, new Color(1, 1, 1, 1f - 0.3f * a));
                        }
                        else
                        {
                            // Interior fill: semi-transparent white
                            _diamondCellTex.SetPixel(x, y, new Color(1, 1, 1, 0.5f));
                        }
                    }
                }
            }

            _diamondCellTex.Apply();
            _diamondCellSprite = Sprite.Create(
                _diamondCellTex,
                new Rect(0, 0, tw, th),
                new Vector2(0.5f, 0.5f),
                100f);
            return _diamondCellSprite;
        }

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
        /// Uses a pre-built diamond sprite (CellSize × CellSize/2) — no rotation or
        /// Y-scaling needed, so the border renders as clean flat diamond edges.
        /// The sprite has white fill+border; fillColor tints the whole image,
        /// and borderColor is blended into the border region via a child overlay.
        /// </summary>
        private static GameObject CreateIsoDiamondCell(Transform parent, int gridX, int gridY,
            Color fillColor, Color borderColor)
        {
            var cellCenter = GridToLocalCenter(new Vector2Int(gridX, gridY), Vector2Int.one);

            var cellGO = new GameObject($"IsoCell_{gridX}_{gridY}");
            cellGO.transform.SetParent(parent, false);
            cellGO.transform.SetAsFirstSibling(); // behind buildings

            var rect = cellGO.AddComponent<RectTransform>();
            rect.anchoredPosition = cellCenter;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Diamond sprite is already the correct iso shape — no rotation needed
            rect.sizeDelta = new Vector2(CellSize, CellSize * 0.5f);

            var img = cellGO.AddComponent<Image>();
            var diamondSpr = GetDiamondCellSprite();
            if (diamondSpr != null)
            {
                img.sprite = diamondSpr;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }
            // Blend fill and border: the sprite has white interior (0.5 alpha)
            // and white border (1.0 alpha). Tinting with borderColor makes border
            // prominent while fill stays subtle.
            img.color = borderColor;
            img.raycastTarget = false;

            // Fill overlay: full diamond tinted with fillColor, drawn behind border
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(cellGO.transform, false);
            fillGO.transform.SetAsFirstSibling();
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 0.5f); // Inset slightly to not cover border
            fillRect.offsetMax = new Vector2(-1f, -0.5f);
            var fillImg = fillGO.AddComponent<Image>();
            if (diamondSpr != null)
            {
                fillImg.sprite = diamondSpr;
                fillImg.type = Image.Type.Simple;
                fillImg.preserveAspect = false;
            }
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

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
            // P&C: Category-colored selection glow
            img.color = GetCategorySelectionColor(placement.BuildingId);

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

            // P&C: Swipe-collect mode — drag across resource buildings to collect
            if (_swipeCollectMode)
                UpdateSwipeCollect(eventData.position);
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

            // P&C IT102: Show quick action radial instead of immediately entering move mode
            ShowLongPressRadial(found);
        }

        private GameObject _longPressRadial;

        /// <summary>P&C IT102: Quick action radial on long-press — Move, Info, Boost options.</summary>
        private void ShowLongPressRadial(CityBuildingPlacement placement)
        {
            DismissLongPressRadial();
            if (placement.VisualGO == null) return;

            // P&C: Audio + haptic on long-press
            PlaySfx(_sfxTap);
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            // P&C: Bounce building to confirm long-press registered
            StartCoroutine(BounceBuilding(placement.VisualGO.transform, 0.08f));

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Dim overlay (tap to dismiss)
            _longPressRadial = new GameObject("LongPressRadial");
            _longPressRadial.transform.SetParent(canvas.transform, false);
            _longPressRadial.transform.SetAsLastSibling();
            var dimRect = _longPressRadial.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _longPressRadial.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.30f);
            dimImg.raycastTarget = true;
            var dimBtn = _longPressRadial.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissLongPressRadial);

            // Position radial at building's screen position
            var buildingRect = placement.VisualGO.GetComponent<RectTransform>();
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, placement.VisualGO.transform.position);

            // Container for radial buttons
            var container = new GameObject("RadialContainer");
            container.transform.SetParent(_longPressRadial.transform, false);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.position = placement.VisualGO.transform.position;
            containerRect.sizeDelta = new Vector2(200, 200);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string capturedInstanceId = placement.InstanceId;
            string capturedBuildingId = placement.BuildingId;
            int capturedTier = placement.Tier;

            // Check if building is upgrading (for Boost button)
            bool isUpgrading = false;
            float upgRemaining = 0f;
            if (ServiceLocator.TryGet<BuildingManager>(out var bm))
            {
                foreach (var qe in bm.BuildQueue)
                {
                    if (qe.PlacedId == capturedInstanceId)
                    {
                        isUpgrading = true;
                        upgRemaining = qe.RemainingSeconds;
                        break;
                    }
                }
            }

            // --- MOVE button (left) ---
            CreateRadialActionButton(container.transform, font, "\u2725", "Move",
                new Color(0.20f, 0.45f, 0.65f, 0.92f), new Color(0.40f, 0.70f, 0.95f, 0.6f),
                new Vector2(-70, 40), () =>
                {
                    DismissLongPressRadial();
                    EnterMoveModeForPlacement(placement);
                });

            // --- INFO button (top) ---
            CreateRadialActionButton(container.transform, font, "\u2139", "Info",
                new Color(0.50f, 0.40f, 0.65f, 0.92f), new Color(0.70f, 0.55f, 0.90f, 0.6f),
                new Vector2(0, 80), () =>
                {
                    DismissLongPressRadial();
                    EventBus.Publish(new BuildingTappedEvent(placement));
                });

            // --- BOOST button (right, only if upgrading) ---
            if (isUpgrading)
            {
                float capSecs = upgRemaining;
                CreateRadialActionButton(container.transform, font, "\u26A1", "Boost",
                    new Color(0.60f, 0.45f, 0.15f, 0.92f), new Color(0.90f, 0.70f, 0.25f, 0.6f),
                    new Vector2(70, 40), () =>
                    {
                        DismissLongPressRadial();
                        if (capSecs <= FreeSpeedUpThresholdSeconds)
                        {
                            EventBus.Publish(new SpeedupRequestedEvent(capturedInstanceId, 0, capSecs));
                        }
                        else
                        {
                            int gemCost = Mathf.Max(1, Mathf.CeilToInt(capSecs / 60f));
                            ShowSpeedUpDialog(capturedInstanceId, gemCost, Mathf.RoundToInt(capSecs));
                        }
                    });
            }

            // Auto-dismiss after 3 seconds
            StartCoroutine(AutoDismissRadial(3f));
        }

        private void CreateRadialActionButton(Transform parent, Font font, string icon, string label,
            Color bgColor, Color outlineColor, Vector2 offset, System.Action onClick)
        {
            var btnGO = new GameObject($"Radial_{label}");
            btnGO.transform.SetParent(parent, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchoredPosition = offset;
            btnRect.sizeDelta = new Vector2(56, 56);

            var bg = btnGO.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = true;
            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => onClick());

            // Icon
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(btnGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.35f);
            iconRect.anchorMax = new Vector2(0.9f, 0.95f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = icon;
            iconText.font = font;
            iconText.fontSize = 18;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.raycastTarget = false;

            // Label
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(btnGO.transform, false);
            var lblRect = lblGO.AddComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0f, 0f);
            lblRect.anchorMax = new Vector2(1f, 0.38f);
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;
            var lblText = lblGO.AddComponent<Text>();
            lblText.text = label;
            lblText.font = font;
            lblText.fontSize = 8;
            lblText.fontStyle = FontStyle.Bold;
            lblText.alignment = TextAnchor.MiddleCenter;
            lblText.color = new Color(0.9f, 0.9f, 0.9f);
            lblText.raycastTarget = false;

            // Scale-in animation
            btnGO.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleInRadialButton(btnGO));
        }

        private IEnumerator ScaleInRadialButton(GameObject btn)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration && btn != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = 1f - (1f - t) * (1f - t);
                btn.transform.localScale = Vector3.one * ease;
                yield return null;
            }
            if (btn != null) btn.transform.localScale = Vector3.one;
        }

        private IEnumerator AutoDismissRadial(float delay)
        {
            yield return new WaitForSeconds(delay);
            DismissLongPressRadial();
        }

        private void DismissLongPressRadial()
        {
            if (_longPressRadial != null)
            {
                Destroy(_longPressRadial);
                _longPressRadial = null;
            }
        }

        /// <summary>P&C IT102: Enter move mode for a specific placement (from radial menu).</summary>
        private void EnterMoveModeForPlacement(CityBuildingPlacement found)
        {
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
                        // Dark fantasy: occupied cells glow red-purple
                        fill = new Color(0.70f, 0.12f, 0.20f, 0.12f);
                        border = new Color(0.80f, 0.15f, 0.25f, 0.30f);
                    }
                    else
                    {
                        // Dark fantasy: empty cells show faint purple grid
                        fill = new Color(0.25f, 0.15f, 0.45f, 0.08f);
                        border = new Color(0.40f, 0.25f, 0.65f, 0.22f);
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
        // P&C: Weather particle overlay
        // ====================================================================

        private GameObject _weatherOverlay;

        /// <summary>
        /// P&C: Ambient weather particle overlay. Spawns falling particles (rain/snow/dust)
        /// based on time of day. Night = snow sparkles, dusk = embers/dust, rain at random.
        /// </summary>
        private void CreateWeatherOverlay()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _weatherOverlay = new GameObject("WeatherOverlay");
            _weatherOverlay.transform.SetParent(canvas.transform, false);
            _weatherOverlay.transform.SetSiblingIndex(2); // Above ambient tint, below UI

            var rect = _weatherOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var cg = _weatherOverlay.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            StartCoroutine(AnimateWeatherParticles());
        }

        private IEnumerator AnimateWeatherParticles()
        {
            var wait = new WaitForSeconds(0.12f);
            int hour = System.DateTime.Now.Hour;
            // Weather type based on time — deterministic per session
            // Night: snow sparkles, dusk/dawn: floating embers, day: occasional rain
            bool isNight = hour < 5 || hour >= 20;
            bool isDusk = hour >= 18 && hour < 20;
            bool isDawn = hour >= 5 && hour < 7;

            Color particleColor;
            float particleAlpha;
            float fallSpeed;
            float drift;

            if (isNight)
            {
                particleColor = new Color(0.70f, 0.75f, 1f);
                particleAlpha = 0.40f;
                fallSpeed = 30f;
                drift = 15f;
            }
            else if (isDusk || isDawn)
            {
                particleColor = new Color(1f, 0.65f, 0.25f);
                particleAlpha = 0.35f;
                fallSpeed = 20f;
                drift = 25f;
            }
            else
            {
                // Day: subtle rain
                particleColor = new Color(0.60f, 0.70f, 0.85f);
                particleAlpha = 0.20f;
                fallSpeed = 120f;
                drift = 5f;
            }

            while (_weatherOverlay != null)
            {
                // Spawn a particle
                if (_weatherOverlay.transform.childCount < 25) // Max particles
                {
                    var p = new GameObject("WP");
                    p.transform.SetParent(_weatherOverlay.transform, false);
                    var pRect = p.AddComponent<RectTransform>();
                    float startX = Random.Range(0f, 1f);
                    pRect.anchorMin = new Vector2(startX, 1.02f);
                    pRect.anchorMax = new Vector2(startX + 0.005f, 1.04f);
                    pRect.offsetMin = Vector2.zero;
                    pRect.offsetMax = Vector2.zero;

                    var pImg = p.AddComponent<Image>();
                    pImg.color = new Color(particleColor.r, particleColor.g, particleColor.b, particleAlpha);
                    pImg.raycastTarget = false;

                    // Load radial gradient for soft look
                    var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
                    #if UNITY_EDITOR
                    if (spr == null)
                        spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
                    #endif
                    if (spr != null && isNight) pImg.sprite = spr; // Snow uses soft dot

                    StartCoroutine(AnimateWeatherParticle(p, pRect, fallSpeed, drift, particleAlpha));
                }

                yield return wait;
            }
        }

        private IEnumerator AnimateWeatherParticle(GameObject p, RectTransform rect, float speed, float driftAmt, float alpha)
        {
            float life = 0f;
            float maxLife = Random.Range(3f, 6f);
            float driftPhase = Random.Range(0f, Mathf.PI * 2f);
            float driftFreq = Random.Range(0.5f, 1.5f);

            while (p != null && life < maxLife)
            {
                life += Time.deltaTime;

                // Move down
                float yDelta = speed * Time.deltaTime / 1920f; // Normalized to screen height
                float xDrift = Mathf.Sin(life * driftFreq + driftPhase) * driftAmt * Time.deltaTime / 1080f;

                rect.anchorMin += new Vector2(xDrift, -yDelta);
                rect.anchorMax += new Vector2(xDrift, -yDelta);

                // Fade at end of life
                float fadeStart = maxLife * 0.7f;
                if (life > fadeStart)
                {
                    var img = p.GetComponent<Image>();
                    if (img != null)
                    {
                        float fadeT = (life - fadeStart) / (maxLife - fadeStart);
                        img.color = new Color(img.color.r, img.color.g, img.color.b, alpha * (1f - fadeT));
                    }
                }

                // Off screen — destroy early
                if (rect.anchorMin.y < -0.05f) break;

                yield return null;
            }

            if (p != null) Destroy(p);
        }

        // ====================================================================
        // P&C: Mini-map overview indicator
        // ====================================================================

        // ====================================================================
        // P&C: Resource Income Ticker (scrolling bar below resource HUD)
        // ====================================================================

        private GameObject _resourceTicker;

        private void CreateResourceIncomeTicker()
        {
            // P&C: No scrolling ticker bar — resource rates shown in BuilderCountHUD and BuildQueuePanel.
            // Removed in iteration 84 to reduce visual clutter between resource bar and info panel.
            return;
#pragma warning disable CS0162 // Unreachable code
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _resourceTicker = new GameObject("ResourceTicker");
            _resourceTicker.transform.SetParent(canvas.transform, false);
            var rect = _resourceTicker.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.905f);
            rect.anchorMax = new Vector2(1f, 0.93f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Subtle dark bg
            var bg = _resourceTicker.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.03f, 0.08f, 0.70f);
            bg.raycastTarget = false;

            // Mask to clip scrolling text
            var mask = _resourceTicker.AddComponent<RectMask2D>();

            // Build ticker text content
            var tickerParts = new List<string>();
            int totalHr = 0;
            foreach (var resKvp in ResourceBuildingTypes)
            {
                int typeRate = 0;
                foreach (var p in _placements)
                {
                    if (p.BuildingId == resKvp.Key)
                        typeRate += (p.Tier + 1) * 250;
                }
                if (typeRate > 0)
                {
                    string rateStr = typeRate >= 1000 ? $"{typeRate / 1000f:F1}K" : $"{typeRate}";
                    tickerParts.Add($"+{rateStr} {resKvp.Value.Name}/hr");
                    totalHr += typeRate;
                }
            }
            int totalPower = 0;
            foreach (var p in _placements)
                totalPower += GetBuildingPowerContribution(p.BuildingId, p.Tier);
            string powerStr = totalPower >= 1000 ? $"{totalPower / 1000f:F1}K" : $"{totalPower}";
            tickerParts.Add($"\u2694 {powerStr} Power");
            tickerParts.Add($"\u2B50 {_placements.Count} Buildings");

            string tickerContent = "    " + string.Join("   \u2022   ", tickerParts) + "   \u2022   ";
            // Duplicate for seamless loop
            string fullTicker = tickerContent + tickerContent;

            var textGO = new GameObject("TickerText");
            textGO.transform.SetParent(_resourceTicker.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = fullTicker;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.70f, 0.68f, 0.62f, 0.85f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            StartCoroutine(ScrollTicker(textRect));
#pragma warning restore CS0162
        }

        private IEnumerator ScrollTicker(RectTransform textRect)
        {
            float scrollSpeed = 30f; // pixels per second
            float offset = 0f;
            while (textRect != null)
            {
                offset -= scrollSpeed * Time.deltaTime;
                // Reset when we've scrolled half the content (seamless loop)
                if (offset < -500f) offset += 500f;
                textRect.anchoredPosition = new Vector2(offset, 0f);
                yield return null;
            }
        }

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
            // P&C: Bottom-right corner, above nav bar (left side reserved for circular event icons)
            rect.anchorMin = new Vector2(0.82f, 0.115f);
            rect.anchorMax = new Vector2(0.98f, 0.215f);
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

        /// <summary>
        /// P&C: Active boost badge on upgrading buildings — shows VIP speed boost
        /// with a pulse glow effect to indicate the boost is actively reducing build time.
        /// </summary>
        private void AddActiveBoostBadge(GameObject building)
        {
            if (building == null) return;
            // Don't duplicate
            if (building.transform.Find("ActiveBoost") != null) return;

            var badge = new GameObject("ActiveBoost");
            badge.transform.SetParent(building.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.65f, 0.68f);
            rect.anchorMax = new Vector2(1.05f, 0.82f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.50f, 0.75f, 0.88f);
            bg.raycastTarget = false;
            var border = badge.AddComponent<Outline>();
            border.effectColor = new Color(0.40f, 0.75f, 1f, 0.70f);
            border.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = "\u26A1 VIP +10%";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.4f, -0.4f);

            StartCoroutine(PulseActiveBoostBadge(badge));
        }

        private IEnumerator PulseActiveBoostBadge(GameObject badge)
        {
            while (badge != null)
            {
                var bg = badge.GetComponent<Image>();
                if (bg != null)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
                    bg.color = new Color(0.15f, 0.50f * pulse, 0.75f * pulse, 0.88f);
                }
                yield return null;
            }
        }

        private void RemoveActiveBoostBadge(GameObject building)
        {
            if (building == null) return;
            var existing = building.transform.Find("ActiveBoost");
            if (existing != null) Destroy(existing.gameObject);
        }

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

        // ====================================================================
        // P&C: Upgrade Requirements Checklist (why upgrade is blocked)
        // ====================================================================

        private GameObject _requirementsPanel;

        /// <summary>P&C: Show detailed requirements checklist when upgrade is blocked.</summary>
        private void ShowUpgradeRequirements(string instanceId, string buildingId, int currentTier)
        {
            if (_requirementsPanel != null) { Destroy(_requirementsPanel); _requirementsPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            ServiceLocator.TryGet<BuildingManager>(out var bm);
            ServiceLocator.TryGet<ResourceManager>(out var rm);

            _requirementsPanel = new GameObject("RequirementsPanel");
            _requirementsPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _requirementsPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _requirementsPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _requirementsPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_requirementsPanel != null) { Destroy(_requirementsPanel); _requirementsPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_requirementsPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.22f);
            panelRect.anchorMax = new Vector2(0.92f, 0.78f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.04f, 0.12f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.90f, 0.40f, 0.30f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            int nextTier = currentTier + 1;

            AddInfoPanelText(panel.transform, "Title",
                $"\u26A0 Requirements for {bName} Lv.{nextTier}", 13, FontStyle.Bold,
                new Color(0.95f, 0.50f, 0.30f),
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            float yPos = 0.84f;
            float rowH = 0.08f;
            Color passColor = new Color(0.40f, 0.85f, 0.45f);
            Color failColor = new Color(0.90f, 0.35f, 0.30f);

            // 1. Stronghold level requirement
            int shTier = 1;
            foreach (var p in _placements)
            {
                if (p.BuildingId == "stronghold") { shTier = p.Tier; break; }
            }
            int reqShLevel = buildingId == "stronghold" ? currentTier : nextTier; // Buildings need SH >= target tier
            bool shMet = shTier >= reqShLevel || buildingId == "stronghold";
            AddRequirementRow(panel.transform, "Stronghold",
                shMet ? $"\u2713 Stronghold Lv.{shTier} (needs Lv.{reqShLevel})" : $"\u2717 Stronghold Lv.{shTier} — need Lv.{reqShLevel}",
                shMet, ref yPos, rowH);

            // 2. Resource requirements
            if (bm != null && bm.PlacedBuildings.TryGetValue(instanceId, out var placed) && placed.Data != null)
            {
                var tierData = placed.Data.GetTier(currentTier);
                if (tierData != null)
                {
                    long curStone = rm != null ? rm.Stone : 0;
                    long curIron = rm != null ? rm.Iron : 0;
                    long curGrain = rm != null ? rm.Grain : 0;
                    long curArcane = rm != null ? rm.ArcaneEssence : 0;

                    if (tierData.stoneCost > 0)
                    {
                        bool met = curStone >= tierData.stoneCost;
                        AddRequirementRow(panel.transform, "Stone",
                            $"{(met ? "\u2713" : "\u2717")} Stone: {curStone}/{tierData.stoneCost}",
                            met, ref yPos, rowH);
                    }
                    if (tierData.ironCost > 0)
                    {
                        bool met = curIron >= tierData.ironCost;
                        AddRequirementRow(panel.transform, "Iron",
                            $"{(met ? "\u2713" : "\u2717")} Iron: {curIron}/{tierData.ironCost}",
                            met, ref yPos, rowH);
                    }
                    if (tierData.grainCost > 0)
                    {
                        bool met = curGrain >= tierData.grainCost;
                        AddRequirementRow(panel.transform, "Grain",
                            $"{(met ? "\u2713" : "\u2717")} Grain: {curGrain}/{tierData.grainCost}",
                            met, ref yPos, rowH);
                    }
                    if (tierData.arcaneEssenceCost > 0)
                    {
                        bool met = curArcane >= tierData.arcaneEssenceCost;
                        AddRequirementRow(panel.transform, "Arcane",
                            $"{(met ? "\u2713" : "\u2717")} Arcane: {curArcane}/{tierData.arcaneEssenceCost}",
                            met, ref yPos, rowH);
                    }

                    // Build time
                    string timeStr = FormatTimeRemaining(Mathf.RoundToInt(tierData.buildTimeSeconds));
                    AddRequirementRow(panel.transform, "Time",
                        $"\u23F1 Build Time: {timeStr}", true, ref yPos, rowH);
                }
            }

            // 3. Build queue availability
            bool queueFree = bm != null && bm.BuildQueue.Count < 2;
            AddRequirementRow(panel.transform, "Queue",
                queueFree ? "\u2713 Build queue slot available" : "\u2717 Build queue full (2/2)",
                queueFree, ref yPos, rowH);

            // 4. Not already upgrading
            bool notUpgrading = true;
            if (bm != null)
            {
                foreach (var qe in bm.BuildQueue)
                {
                    if (qe.PlacedId == instanceId) { notUpgrading = false; break; }
                }
            }
            AddRequirementRow(panel.transform, "NotUpgrading",
                notUpgrading ? "\u2713 Building not in queue" : "\u2717 Already upgrading",
                notUpgrading, ref yPos, rowH);

            // Summary
            AddInfoPanelText(panel.transform, "Hint",
                "Meet all requirements to upgrade this building", 8, FontStyle.Italic,
                new Color(0.55f, 0.50f, 0.45f),
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.10f), TextAnchor.MiddleCenter);

            // Close
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
            closeBtn.onClick.AddListener(() => { if (_requirementsPanel != null) { Destroy(_requirementsPanel); _requirementsPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _requirementsPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void AddRequirementRow(Transform parent, string id, string text, bool met, ref float yPos, float rowH)
        {
            Color color = met ? new Color(0.40f, 0.85f, 0.45f) : new Color(0.90f, 0.35f, 0.30f);
            Color bgColor = met ? new Color(0.10f, 0.18f, 0.10f, 0.60f) : new Color(0.20f, 0.08f, 0.08f, 0.60f);

            var rowGO = new GameObject($"Req_{id}");
            rowGO.transform.SetParent(parent, false);
            var rowRect = rowGO.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.04f, yPos - rowH);
            rowRect.anchorMax = new Vector2(0.96f, yPos);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            var rowBg = rowGO.AddComponent<Image>();
            rowBg.color = bgColor;
            rowBg.raycastTarget = false;

            AddInfoPanelText(rowGO.transform, "Text", text, 10, FontStyle.Normal, color,
                new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), TextAnchor.MiddleLeft);

            yPos -= rowH + 0.01f;
        }

        // ====================================================================
        // P&C: Build Queue Reorder (swap queue slot positions)
        // ====================================================================

        /// <summary>P&C: Move a build queue entry up or down. Refreshes the panel after swap.</summary>
        private void SwapQueueEntries(int indexA, int indexB)
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;
            var queue = bm.BuildQueue;
            if (indexA < 0 || indexB < 0 || indexA >= queue.Count || indexB >= queue.Count) return;
            if (indexA == indexB) return;

            // BuildQueue is IReadOnlyList, so we publish an event for BuildingManager to handle
            // For now simulate the swap in the UI and log
            Debug.Log($"[BuildQueue] Reorder: slot {indexA} <-> slot {indexB}");
            ShowUpgradeBlockedToast($"\u21C5 Queue reordered: slot {indexA + 1} \u2194 {indexB + 1}");

            // Refresh the queue panel
            if (_buildQueuePanel != null)
            {
                Destroy(_buildQueuePanel);
                _buildQueuePanel = null;
                ShowBuildQueuePanel();
            }
        }

        // ====================================================================
        // P&C: Warehouse / Resource Protection Display
        // ====================================================================

        private GameObject _warehousePanel;

        /// <summary>P&C: Show warehouse resource protection breakdown.</summary>
        private void ShowWarehousePanel()
        {
            if (_warehousePanel != null) { Destroy(_warehousePanel); _warehousePanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            ServiceLocator.TryGet<ResourceManager>(out var rm);

            _warehousePanel = new GameObject("WarehousePanel");
            _warehousePanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _warehousePanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _warehousePanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _warehousePanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_warehousePanel != null) { Destroy(_warehousePanel); _warehousePanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_warehousePanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.25f);
            panelRect.anchorMax = new Vector2(0.92f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.06f, 0.10f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.60f, 0.50f, 0.20f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            AddInfoPanelText(panel.transform, "Title",
                "\u2618 Warehouse — Resource Protection", 13, FontStyle.Bold,
                new Color(0.85f, 0.75f, 0.30f),
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            AddInfoPanelText(panel.transform, "SubTitle",
                "Protected resources cannot be plundered by enemy raids", 8, FontStyle.Italic,
                new Color(0.55f, 0.50f, 0.42f),
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.87f), TextAnchor.MiddleCenter);

            // Find marketplace tier for protection cap
            int marketplaceTier = 0;
            foreach (var p in _placements)
            {
                if (p.BuildingId == "marketplace") { marketplaceTier = Mathf.Max(marketplaceTier, p.Tier); }
            }
            int protectionCap = 5000 + marketplaceTier * 5000; // Base 5K + 5K per tier

            // Resource rows
            var resources = new[]
            {
                ("Stone", "\u25C8", rm != null ? rm.Stone : 0L, new Color(0.70f, 0.70f, 0.75f)),
                ("Iron", "\u2666", rm != null ? rm.Iron : 0L, new Color(0.80f, 0.65f, 0.50f)),
                ("Grain", "\u2740", rm != null ? rm.Grain : 0L, new Color(0.65f, 0.85f, 0.45f)),
                ("Arcane", "\u2726", rm != null ? rm.ArcaneEssence : 0L, new Color(0.60f, 0.50f, 0.90f)),
            };

            float yPos = 0.76f;
            foreach (var (name, icon, current, tint) in resources)
            {
                long protectedAmt = current < protectionCap ? current : protectionCap;
                long unprotected = current - protectedAmt;
                float fillRatio = protectionCap > 0 ? (float)protectedAmt / protectionCap : 0f;

                var rowGO = new GameObject($"Res_{name}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.04f, yPos - 0.15f);
                rowRect.anchorMax = new Vector2(0.96f, yPos);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = new Color(tint.r * 0.10f, tint.g * 0.10f, tint.b * 0.10f, 0.60f);
                rowBg.raycastTarget = false;

                // Icon + name
                AddInfoPanelText(rowGO.transform, "Name", $"{icon} {name}", 11, FontStyle.Bold, tint,
                    new Vector2(0.02f, 0.55f), new Vector2(0.25f, 0.95f), TextAnchor.MiddleLeft);

                // Protection bar
                var barBgGO = new GameObject("BarBg");
                barBgGO.transform.SetParent(rowGO.transform, false);
                var barBgRect = barBgGO.AddComponent<RectTransform>();
                barBgRect.anchorMin = new Vector2(0.27f, 0.55f);
                barBgRect.anchorMax = new Vector2(0.75f, 0.90f);
                barBgRect.offsetMin = Vector2.zero;
                barBgRect.offsetMax = Vector2.zero;
                var barBgImg = barBgGO.AddComponent<Image>();
                barBgImg.color = new Color(0.15f, 0.12f, 0.10f, 0.80f);
                barBgImg.raycastTarget = false;

                var barFillGO = new GameObject("BarFill");
                barFillGO.transform.SetParent(barBgGO.transform, false);
                var barFillRect = barFillGO.AddComponent<RectTransform>();
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = new Vector2(fillRatio, 1f);
                barFillRect.offsetMin = Vector2.zero;
                barFillRect.offsetMax = Vector2.zero;
                var barFillImg = barFillGO.AddComponent<Image>();
                barFillImg.color = new Color(0.25f, 0.65f, 0.35f, 0.90f);
                barFillImg.raycastTarget = false;

                // Protected amount
                string protStr = protectedAmt >= 1000 ? $"{protectedAmt / 1000f:F1}K" : $"{protectedAmt}";
                string capStr = protectionCap >= 1000 ? $"{protectionCap / 1000f:F1}K" : $"{protectionCap}";
                AddInfoPanelText(rowGO.transform, "Protected",
                    $"\u26E8 {protStr} / {capStr}", 8, FontStyle.Normal,
                    new Color(0.45f, 0.80f, 0.50f),
                    new Vector2(0.27f, 0.08f), new Vector2(0.75f, 0.50f), TextAnchor.MiddleLeft);

                // Unprotected (at risk)
                if (unprotected > 0)
                {
                    string riskStr = unprotected >= 1000 ? $"{unprotected / 1000f:F1}K" : $"{unprotected}";
                    AddInfoPanelText(rowGO.transform, "AtRisk",
                        $"\u26A0 {riskStr} at risk", 9, FontStyle.Bold,
                        new Color(0.90f, 0.40f, 0.30f),
                        new Vector2(0.77f, 0.30f), new Vector2(0.98f, 0.70f), TextAnchor.MiddleCenter);
                }
                else
                {
                    AddInfoPanelText(rowGO.transform, "Safe",
                        "\u2713 Safe", 9, FontStyle.Bold,
                        new Color(0.40f, 0.80f, 0.45f),
                        new Vector2(0.77f, 0.30f), new Vector2(0.98f, 0.70f), TextAnchor.MiddleCenter);
                }

                yPos -= 0.17f;
            }

            // Upgrade hint
            AddInfoPanelText(panel.transform, "UpgradeHint",
                $"Marketplace Lv.{marketplaceTier} — Protection cap: {protectionCap / 1000f:F0}K per resource",
                8, FontStyle.Normal, new Color(0.65f, 0.60f, 0.50f),
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.12f), TextAnchor.MiddleCenter);

            // Close
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
            closeBtn.onClick.AddListener(() => { if (_warehousePanel != null) { Destroy(_warehousePanel); _warehousePanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _warehousePanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Alliance Help Request (from upgrade confirm / building under construction)
        // ====================================================================

        private GameObject _allianceHelpPanel;
        private readonly Dictionary<string, int> _allianceHelpsReceived = new();
        private const int MaxAllianceHelps = 5;
        private const int HelpReductionSeconds = 60;

        /// <summary>P&C: Show "Request Help" floating button on buildings under construction.</summary>
        private void CreateAllianceHelpButton(GameObject building, string instanceId, string buildingId)
        {
            if (building == null) return;
            var existing = building.transform.Find("AllianceHelpBtn");
            if (existing != null) Destroy(existing.gameObject);

            var helpGO = new GameObject("AllianceHelpBtn");
            helpGO.transform.SetParent(building.transform, false);
            var rect = helpGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.15f, 0.55f);
            rect.anchorMax = new Vector2(0.25f, 0.72f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = helpGO.AddComponent<Image>();
            bg.color = new Color(0.20f, 0.45f, 0.75f, 0.90f);
            bg.raycastTarget = true;
            var outline = helpGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.40f, 0.70f, 1f, 0.7f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            var btn = helpGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            string capInstId = instanceId;
            string capBldId = buildingId;
            btn.onClick.AddListener(() => RequestAllianceHelp(capInstId, capBldId));

            // Help count text
            int received = _allianceHelpsReceived.TryGetValue(instanceId, out var c) ? c : 0;
            string helpText = received >= MaxAllianceHelps ? "\u2764 Max" : $"\u2764 Help ({received}/{MaxAllianceHelps})";

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(helpGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = helpText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            var shadow = textGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.4f, -0.4f);

            // Pulse if help available
            if (received < MaxAllianceHelps)
                StartCoroutine(PulseAllianceHelpButton(helpGO));
        }

        private void RequestAllianceHelp(string instanceId, string buildingId)
        {
            int received = _allianceHelpsReceived.TryGetValue(instanceId, out var c) ? c : 0;
            if (received >= MaxAllianceHelps)
            {
                ShowUpgradeBlockedToast("Maximum alliance helps reached for this building");
                return;
            }

            _allianceHelpsReceived[instanceId] = received + 1;
            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            ShowUpgradeBlockedToast($"\u2764 Requested help for {bName} ({received + 1}/{MaxAllianceHelps})");

            // Simulate alliance response after short delay
            StartCoroutine(SimulateAllianceHelpResponse(instanceId, received + 1));

            // Refresh the help button text
            foreach (var p in _placements)
            {
                if (p.InstanceId == instanceId && p.VisualGO != null)
                {
                    CreateAllianceHelpButton(p.VisualGO, instanceId, buildingId);
                    break;
                }
            }
        }

        private IEnumerator SimulateAllianceHelpResponse(string instanceId, int helpCount)
        {
            yield return new WaitForSeconds(1.5f + Random.Range(0f, 2f));
            ShowAllianceHelpReceived(instanceId, HelpReductionSeconds);
        }

        private IEnumerator PulseAllianceHelpButton(GameObject helpGO)
        {
            while (helpGO != null)
            {
                var bg = helpGO.GetComponent<Image>();
                if (bg != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                    bg.color = new Color(0.20f, 0.45f * pulse, 0.75f * pulse, 0.90f);
                }
                yield return null;
            }
        }

        // ====================================================================
        // P&C: Decoration Placement System
        // ====================================================================

        private GameObject _decorationSelector;

        private static readonly Dictionary<string, (string Icon, string Name, Color Tint)> DecorationTypes = new()
        {
            { "road", ("\u2550", "Road", new Color(0.65f, 0.55f, 0.40f)) },
            { "fountain", ("\u26F2", "Fountain", new Color(0.40f, 0.65f, 0.90f)) },
            { "tree", ("\u2742", "Tree", new Color(0.35f, 0.70f, 0.30f)) },
            { "statue", ("\u2655", "Statue", new Color(0.80f, 0.70f, 0.20f)) },
            { "garden", ("\u2741", "Garden", new Color(0.55f, 0.80f, 0.40f)) },
            { "torch", ("\u2739", "Torch", new Color(0.90f, 0.55f, 0.20f)) },
        };

        /// <summary>P&C: Show decoration selector on empty cell tap (if not already in build mode).</summary>
        private void ShowDecorationSelector(Vector2Int gridPos)
        {
            DismissDecorationSelector();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _decorationSelector = new GameObject("DecorationSelector");
            _decorationSelector.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _decorationSelector.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _decorationSelector.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.45f);
            dimImg.raycastTarget = true;
            var dimBtn = _decorationSelector.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(DismissDecorationSelector);

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_decorationSelector.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.30f);
            panelRect.anchorMax = new Vector2(0.90f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.08f, 0.04f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.45f, 0.70f, 0.30f, 0.80f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            AddInfoPanelText(panel.transform, "Title", "\u2742 Place Decoration", 14, FontStyle.Bold,
                new Color(0.55f, 0.85f, 0.40f),
                new Vector2(0.05f, 0.82f), new Vector2(0.75f, 0.97f), TextAnchor.MiddleLeft);

            AddInfoPanelText(panel.transform, "SubTitle",
                "Decorations are cosmetic — no gameplay effect", 8, FontStyle.Italic,
                new Color(0.50f, 0.50f, 0.40f),
                new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.82f), TextAnchor.MiddleLeft);

            // Grid of decoration options (2 rows x 3 cols)
            int idx = 0;
            foreach (var kvp in DecorationTypes)
            {
                int row = idx / 3;
                int col = idx % 3;
                float x0 = 0.03f + col * 0.32f;
                float x1 = x0 + 0.30f;
                float y1 = 0.70f - row * 0.35f;
                float y0 = y1 - 0.32f;

                var itemGO = new GameObject($"Deco_{kvp.Key}");
                itemGO.transform.SetParent(panel.transform, false);
                var itemRect = itemGO.AddComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(x0, y0);
                itemRect.anchorMax = new Vector2(x1, y1);
                itemRect.offsetMin = Vector2.zero;
                itemRect.offsetMax = Vector2.zero;
                var itemBg = itemGO.AddComponent<Image>();
                itemBg.color = new Color(kvp.Value.Tint.r * 0.2f, kvp.Value.Tint.g * 0.2f, kvp.Value.Tint.b * 0.2f, 0.80f);
                itemBg.raycastTarget = true;
                var itemOutline = itemGO.AddComponent<Outline>();
                itemOutline.effectColor = new Color(kvp.Value.Tint.r, kvp.Value.Tint.g, kvp.Value.Tint.b, 0.4f);
                itemOutline.effectDistance = new Vector2(0.5f, -0.5f);

                // Icon
                AddInfoPanelText(itemGO.transform, "Icon", kvp.Value.Icon, 18, FontStyle.Normal,
                    kvp.Value.Tint,
                    new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.90f), TextAnchor.MiddleCenter);

                // Name
                AddInfoPanelText(itemGO.transform, "Name", kvp.Value.Name, 8, FontStyle.Bold,
                    new Color(0.85f, 0.82f, 0.75f),
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.32f), TextAnchor.MiddleCenter);

                // Button
                var itemBtn = itemGO.AddComponent<Button>();
                itemBtn.targetGraphic = itemBg;
                string decoId = kvp.Key;
                Vector2Int capPos = gridPos;
                itemBtn.onClick.AddListener(() => PlaceDecoration(decoId, capPos));

                idx++;
            }

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.88f);
            closeRect.anchorMax = new Vector2(0.98f, 1.0f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(DismissDecorationSelector);
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _decorationSelector.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void PlaceDecoration(string decoId, Vector2Int gridPos)
        {
            DismissDecorationSelector();

            if (!DecorationTypes.TryGetValue(decoId, out var decoData)) return;

            // Create decoration as a placement
            string instanceId = $"{decoId}_{gridPos.x}_{gridPos.y}";
            var placement = new CityBuildingPlacement
            {
                InstanceId = instanceId,
                BuildingId = decoId,
                Tier = 1,
                GridOrigin = gridPos,
                Size = new Vector2Int(1, 1)
            };
            _placements.Add(placement);
            _occupancy[gridPos] = instanceId;

            // Create visual
            CreateDecorationVisual(placement, decoData);

            string displayName = decoData.Name;
            ShowUpgradeBlockedToast($"\u2742 Placed {displayName} at ({gridPos.x}, {gridPos.y})");
            Debug.Log($"[Decoration] Placed {decoId} at {gridPos}.");
        }

        private void CreateDecorationVisual(CityBuildingPlacement placement, (string Icon, string Name, Color Tint) data)
        {
            var container = buildingContainer != null ? buildingContainer : contentContainer;
            if (container == null) return;

            Vector2 screenPos = GridToLocalCenter(placement.GridOrigin, placement.Size);
            float decoSize = CellSize * 0.8f;

            var decoGO = new GameObject($"Deco_{placement.InstanceId}");
            decoGO.transform.SetParent(container, false);
            var rect = decoGO.AddComponent<RectTransform>();
            rect.anchoredPosition = screenPos;
            rect.sizeDelta = new Vector2(decoSize, decoSize);

            // Background circle
            var bg = decoGO.AddComponent<Image>();
            bg.color = new Color(data.Tint.r * 0.3f, data.Tint.g * 0.3f, data.Tint.b * 0.3f, 0.70f);
            bg.raycastTarget = false;

            // Icon
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(decoGO.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.10f, 0.10f);
            iconRect.anchorMax = new Vector2(0.90f, 0.90f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = data.Icon;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = Mathf.RoundToInt(decoSize * 0.5f);
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = data.Tint;
            iconText.raycastTarget = false;

            placement.VisualGO = decoGO;
        }

        private void DismissDecorationSelector()
        {
            if (_decorationSelector != null) { Destroy(_decorationSelector); _decorationSelector = null; }
        }

        // ====================================================================
        // P&C: Garrison Quick-Deploy on Defensive Buildings
        // ====================================================================

        private GameObject _garrisonPanel;

        private static readonly Dictionary<string, (string TroopName, int BaseCapacity)> DefensiveGarrisonMap = new()
        {
            { "wall", ("Sentinels", 200) },
            { "watch_tower", ("Archers", 150) },
            { "stronghold", ("Royal Guard", 500) },
        };

        /// <summary>P&C: Show garrison deploy panel for defensive buildings.</summary>
        private void ShowGarrisonDeployPanel(string instanceId, string buildingId, int tier)
        {
            if (_garrisonPanel != null) { Destroy(_garrisonPanel); _garrisonPanel = null; }

            if (!DefensiveGarrisonMap.TryGetValue(buildingId, out var garrisonInfo)) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _garrisonPanel = new GameObject("GarrisonPanel");
            _garrisonPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _garrisonPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _garrisonPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);
            dimImg.raycastTarget = true;
            var dimBtn = _garrisonPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_garrisonPanel != null) { Destroy(_garrisonPanel); _garrisonPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_garrisonPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.28f);
            panelRect.anchorMax = new Vector2(0.90f, 0.72f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.04f, 0.04f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.35f, 0.25f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            string bName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            int maxCapacity = garrisonInfo.BaseCapacity * tier;
            int currentGarrison = maxCapacity / 2; // Simulated current garrison

            // Title
            AddInfoPanelText(panel.transform, "Title",
                $"\u26E8 {bName} — Garrison", 14, FontStyle.Bold,
                new Color(0.90f, 0.40f, 0.30f),
                new Vector2(0.05f, 0.87f), new Vector2(0.80f, 0.98f), TextAnchor.MiddleLeft);

            // Current garrison info
            AddInfoPanelText(panel.transform, "TroopType",
                $"Stationed: {garrisonInfo.TroopName}", 10, FontStyle.Normal,
                new Color(0.75f, 0.70f, 0.65f),
                new Vector2(0.05f, 0.76f), new Vector2(0.60f, 0.85f), TextAnchor.MiddleLeft);

            // Garrison fill bar
            float fillRatio = (float)currentGarrison / maxCapacity;
            var barBgGO = new GameObject("BarBg");
            barBgGO.transform.SetParent(panel.transform, false);
            var barBgRect = barBgGO.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.05f, 0.64f);
            barBgRect.anchorMax = new Vector2(0.95f, 0.74f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBgGO.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.10f, 0.10f, 0.90f);
            barBgImg.raycastTarget = false;

            var barFillGO = new GameObject("BarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            var barFillRect = barFillGO.AddComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = new Vector2(fillRatio, 1f);
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
            var barFillImg = barFillGO.AddComponent<Image>();
            Color barColor = fillRatio > 0.7f ? new Color(0.30f, 0.75f, 0.35f) :
                             fillRatio > 0.3f ? new Color(0.85f, 0.70f, 0.25f) :
                             new Color(0.85f, 0.30f, 0.25f);
            barFillImg.color = barColor;
            barFillImg.raycastTarget = false;

            AddInfoPanelText(barBgGO.transform, "Label",
                $"{currentGarrison} / {maxCapacity}", 9, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Defense power from garrison
            int defPower = currentGarrison * 3 * tier;
            AddInfoPanelText(panel.transform, "DefPower",
                $"\u26E8 Defense Power: {FormatStatValue(defPower)}", 10, FontStyle.Bold,
                new Color(0.85f, 0.55f, 0.30f),
                new Vector2(0.05f, 0.54f), new Vector2(0.60f, 0.63f), TextAnchor.MiddleLeft);

            // Deploy buttons: +25%, +50%, Max
            var deployOptions = new[] { ("+ 25%", 0.25f), ("+ 50%", 0.50f), ("MAX", 1.0f) };
            float btnX = 0.05f;
            foreach (var (label, fraction) in deployOptions)
            {
                int troopsToAdd = Mathf.Min(
                    Mathf.RoundToInt((maxCapacity - currentGarrison) * fraction),
                    maxCapacity - currentGarrison);
                if (troopsToAdd < 0) troopsToAdd = 0;

                var deployGO = new GameObject($"Deploy_{label}");
                deployGO.transform.SetParent(panel.transform, false);
                var deployRect = deployGO.AddComponent<RectTransform>();
                deployRect.anchorMin = new Vector2(btnX, 0.38f);
                deployRect.anchorMax = new Vector2(btnX + 0.28f, 0.52f);
                deployRect.offsetMin = Vector2.zero;
                deployRect.offsetMax = Vector2.zero;
                var deployBg = deployGO.AddComponent<Image>();
                bool canDeploy = troopsToAdd > 0;
                deployBg.color = canDeploy
                    ? new Color(0.55f, 0.20f, 0.18f, 0.90f)
                    : new Color(0.30f, 0.28f, 0.26f, 0.70f);
                deployBg.raycastTarget = true;
                var deployOutln = deployGO.AddComponent<Outline>();
                deployOutln.effectColor = new Color(0.85f, 0.40f, 0.30f, 0.4f);
                deployOutln.effectDistance = new Vector2(0.4f, -0.4f);
                var deployBtn = deployGO.AddComponent<Button>();
                deployBtn.targetGraphic = deployBg;

                int capTroops = troopsToAdd;
                string capLabel = label;
                string capBName = bName;
                deployBtn.onClick.AddListener(() =>
                {
                    if (capTroops <= 0) { ShowUpgradeBlockedToast("Garrison is full!"); return; }
                    Debug.Log($"[Garrison] Deployed {capTroops} troops to {capBName}.");
                    if (_garrisonPanel != null) { Destroy(_garrisonPanel); _garrisonPanel = null; }
                    ShowUpgradeBlockedToast($"\u26E8 Deployed {capTroops} {garrisonInfo.TroopName} to {capBName}");
                });

                string btnLabel = canDeploy ? $"{label}\n+{capTroops}" : "FULL";
                AddInfoPanelText(deployGO.transform, "Label", btnLabel, 9, FontStyle.Bold,
                    canDeploy ? Color.white : new Color(0.55f, 0.50f, 0.45f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                btnX += 0.31f;
            }

            // Withdraw button
            var withdrawGO = new GameObject("WithdrawBtn");
            withdrawGO.transform.SetParent(panel.transform, false);
            var withdrawRect = withdrawGO.AddComponent<RectTransform>();
            withdrawRect.anchorMin = new Vector2(0.05f, 0.22f);
            withdrawRect.anchorMax = new Vector2(0.50f, 0.34f);
            withdrawRect.offsetMin = Vector2.zero;
            withdrawRect.offsetMax = Vector2.zero;
            var withdrawBg = withdrawGO.AddComponent<Image>();
            withdrawBg.color = new Color(0.35f, 0.30f, 0.25f, 0.85f);
            withdrawBg.raycastTarget = true;
            var withdrawBtn = withdrawGO.AddComponent<Button>();
            withdrawBtn.targetGraphic = withdrawBg;
            withdrawBtn.onClick.AddListener(() =>
            {
                Debug.Log($"[Garrison] Withdrew all troops from {bName}.");
                if (_garrisonPanel != null) { Destroy(_garrisonPanel); _garrisonPanel = null; }
                ShowUpgradeBlockedToast($"\u26E8 Withdrew troops from {bName}");
            });
            AddInfoPanelText(withdrawGO.transform, "Label", "Withdraw All", 10, FontStyle.Bold,
                new Color(0.80f, 0.70f, 0.55f),
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Auto-garrison toggle
            var autoGO = new GameObject("AutoGarrison");
            autoGO.transform.SetParent(panel.transform, false);
            var autoRect = autoGO.AddComponent<RectTransform>();
            autoRect.anchorMin = new Vector2(0.55f, 0.22f);
            autoRect.anchorMax = new Vector2(0.95f, 0.34f);
            autoRect.offsetMin = Vector2.zero;
            autoRect.offsetMax = Vector2.zero;
            var autoBg = autoGO.AddComponent<Image>();
            autoBg.color = new Color(0.20f, 0.40f, 0.25f, 0.85f);
            autoBg.raycastTarget = true;
            var autoBtn = autoGO.AddComponent<Button>();
            autoBtn.targetGraphic = autoBg;
            autoBtn.onClick.AddListener(() =>
            {
                ShowUpgradeBlockedToast("\u2705 Auto-garrison enabled");
            });
            AddInfoPanelText(autoGO.transform, "Label", "\u2705 Auto-Garrison", 9, FontStyle.Bold,
                new Color(0.50f, 0.85f, 0.55f),
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Garrison tip
            AddInfoPanelText(panel.transform, "Tip",
                "Tip: Garrisoned troops defend against enemy raids automatically", 7, FontStyle.Italic,
                new Color(0.50f, 0.45f, 0.40f),
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.18f), TextAnchor.MiddleCenter);

            // Close button
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
            closeBtn.onClick.AddListener(() => { if (_garrisonPanel != null) { Destroy(_garrisonPanel); _garrisonPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Fade in
            var cg = _garrisonPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: Second Builder / VIP Builder System
        // ====================================================================

        private bool _hasSecondBuilder;
        private GameObject _secondBuilderPanel;

        /// <summary>P&C: Show the second builder purchase panel when builder HUD is tapped and queue is full.</summary>
        private void ShowSecondBuilderPanel()
        {
            if (_secondBuilderPanel != null) { Destroy(_secondBuilderPanel); _secondBuilderPanel = null; }
            if (_hasSecondBuilder) return; // Already purchased

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _secondBuilderPanel = new GameObject("SecondBuilderPanel");
            _secondBuilderPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _secondBuilderPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _secondBuilderPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _secondBuilderPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_secondBuilderPanel != null) { Destroy(_secondBuilderPanel); _secondBuilderPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_secondBuilderPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.30f);
            panelRect.anchorMax = new Vector2(0.90f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            AddInfoPanelText(panel.transform, "Title", "\u2692 Unlock Second Builder", 16, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);

            // Description
            AddInfoPanelText(panel.transform, "Desc",
                "Build two buildings at the same time!\nA second builder lets you progress twice as fast.\nUpgrade your VIP level or use gems to unlock.",
                11, FontStyle.Normal, new Color(0.78f, 0.75f, 0.72f),
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.80f), TextAnchor.UpperCenter);

            // Builder icon (large hammer)
            AddInfoPanelText(panel.transform, "Icon", "\u2692\u2692", 32, FontStyle.Bold,
                new Color(0.95f, 0.75f, 0.25f),
                new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.55f), TextAnchor.MiddleCenter);

            // Benefit list
            AddInfoPanelText(panel.transform, "Ben1", "\u2713 Build 2 buildings simultaneously", 10, FontStyle.Normal,
                new Color(0.50f, 0.90f, 0.50f),
                new Vector2(0.10f, 0.26f), new Vector2(0.90f, 0.33f), TextAnchor.MiddleLeft);
            AddInfoPanelText(panel.transform, "Ben2", "\u2713 Permanent unlock — never expires", 10, FontStyle.Normal,
                new Color(0.50f, 0.90f, 0.50f),
                new Vector2(0.10f, 0.19f), new Vector2(0.90f, 0.26f), TextAnchor.MiddleLeft);
            AddInfoPanelText(panel.transform, "Ben3", "\u2713 Also available at VIP Level 5", 10, FontStyle.Normal,
                new Color(0.50f, 0.90f, 0.50f),
                new Vector2(0.10f, 0.12f), new Vector2(0.90f, 0.19f), TextAnchor.MiddleLeft);

            // Gem cost button
            var gemGO = new GameObject("GemBtn");
            gemGO.transform.SetParent(panel.transform, false);
            var gemRect = gemGO.AddComponent<RectTransform>();
            gemRect.anchorMin = new Vector2(0.15f, 0.02f);
            gemRect.anchorMax = new Vector2(0.55f, 0.12f);
            gemRect.offsetMin = Vector2.zero;
            gemRect.offsetMax = Vector2.zero;
            var gemBg = gemGO.AddComponent<Image>();
            gemBg.color = new Color(0.55f, 0.25f, 0.70f, 0.92f);
            gemBg.raycastTarget = true;
            var gemOutline = gemGO.AddComponent<Outline>();
            gemOutline.effectColor = new Color(0.75f, 0.50f, 0.90f, 0.6f);
            gemOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var gemBtn = gemGO.AddComponent<Button>();
            gemBtn.targetGraphic = gemBg;
            gemBtn.onClick.AddListener(() =>
            {
                _hasSecondBuilder = true;
                UpdateBuilderCountHUD();
                if (_secondBuilderPanel != null) { Destroy(_secondBuilderPanel); _secondBuilderPanel = null; }
                ShowUpgradeBlockedToast("\u2692 Second Builder unlocked!");
            });
            AddInfoPanelText(gemGO.transform, "Label", "\u2B25 500 Gems", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.60f, 0.02f);
            closeRect.anchorMax = new Vector2(0.85f, 0.12f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.40f, 0.35f, 0.35f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_secondBuilderPanel != null) { Destroy(_secondBuilderPanel); _secondBuilderPanel = null; } });
            AddInfoPanelText(closeGO.transform, "Label", "Not Now", 10, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var fadeCg = _secondBuilderPanel.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            StartCoroutine(FadeInDialog(fadeCg));
        }

        // ====================================================================
        // P&C: Troop March Animation — tiny figures marching from barracks to gate
        // ====================================================================

        private readonly List<GameObject> _marchingTroops = new();
        private float _troopMarchSpawnTimer;
        private float _dayNightTimer;
        private float _prosperityRefreshTimer;
        private const float TroopMarchSpawnInterval = 8f; // seconds between march waves

        /// <summary>P&C: Spawn a tiny troop figure that marches from a barracks to the nearest wall gate.</summary>
        private void SpawnTroopMarch()
        {
            if (buildingContainer == null) return;

            // Find a barracks or training ground
            CityBuildingPlacement source = null;
            CityBuildingPlacement wallGate = null;
            foreach (var p in _placements)
            {
                if ((p.BuildingId == "barracks" || p.BuildingId == "training_ground") && p.VisualGO != null)
                {
                    if (source == null || Random.value > 0.5f) source = p;
                }
                if (p.BuildingId == "wall" && p.VisualGO != null)
                {
                    if (wallGate == null || Random.value > 0.5f) wallGate = p;
                }
            }

            if (source == null) return;

            // Default destination: stronghold if no wall
            CityBuildingPlacement dest = wallGate;
            if (dest == null)
            {
                foreach (var p in _placements)
                {
                    if (p.BuildingId == "stronghold" && p.VisualGO != null) { dest = p; break; }
                }
            }
            if (dest == null) return;

            var container = buildingContainer ?? contentContainer;
            if (container == null) return;

            Vector2 startPos = GridToLocalCenter(source.GridOrigin, source.Size);
            Vector2 endPos = GridToLocalCenter(dest.GridOrigin, dest.Size);

            // Spawn 2-4 tiny figures in a line
            int count = Random.Range(2, 5);
            for (int i = 0; i < count; i++)
            {
                var troopGO = new GameObject($"MarchingTroop_{i}");
                troopGO.transform.SetParent(container, false);
                var rect = troopGO.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(6, 8);
                rect.anchoredPosition = startPos + new Vector2(i * 4f, 0);

                var img = troopGO.AddComponent<Image>();
                bool isArcher = source.BuildingId == "training_ground";
                img.color = isArcher
                    ? new Color(0.30f, 0.60f, 0.90f, 0.85f)  // Blue for specialists
                    : new Color(0.80f, 0.55f, 0.20f, 0.85f);  // Bronze for infantry
                img.raycastTarget = false;

                // Tiny shield/weapon icon
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(troopGO.transform, false);
                var iconRect = iconGO.AddComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconText = iconGO.AddComponent<Text>();
                iconText.text = isArcher ? "\u2694" : "\u26E8";
                iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                iconText.fontSize = 5;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = Color.white;
                iconText.raycastTarget = false;

                _marchingTroops.Add(troopGO);
                float delay = i * 0.3f;
                float duration = 4f + Random.Range(0f, 1.5f);
                StartCoroutine(AnimateTroopMarch(troopGO, startPos + new Vector2(i * 4f, 0), endPos, delay, duration));
            }
        }

        private IEnumerator AnimateTroopMarch(GameObject troop, Vector2 start, Vector2 end, float delay, float duration)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            var rect = troop != null ? troop.GetComponent<RectTransform>() : null;
            if (rect == null) yield break;

            float elapsed = 0f;
            // Add slight arc to path (troops don't walk perfectly straight)
            Vector2 mid = (start + end) * 0.5f + new Vector2(Random.Range(-15f, 15f), Random.Range(5f, 20f));

            while (elapsed < duration && troop != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Quadratic bezier for curved path
                Vector2 pos = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * mid + t * t * end;
                rect.anchoredPosition = pos;

                // Tiny bobbing animation (walking)
                float bob = Mathf.Sin(elapsed * 12f) * 1.5f;
                rect.anchoredPosition += new Vector2(0, bob);

                yield return null;
            }

            // Fade out at destination
            if (troop != null)
            {
                var img = troop.GetComponent<Image>();
                if (img != null)
                {
                    float fade = 0.3f;
                    float fadeElapsed = 0f;
                    while (fadeElapsed < fade && troop != null)
                    {
                        fadeElapsed += Time.deltaTime;
                        float a = Mathf.Lerp(0.85f, 0f, fadeElapsed / fade);
                        img.color = new Color(img.color.r, img.color.g, img.color.b, a);
                        yield return null;
                    }
                }
                _marchingTroops.Remove(troop);
                Destroy(troop);
            }
        }

        /// <summary>Cleanup destroyed marching troop references.</summary>
        private void CleanupMarchingTroops()
        {
            _marchingTroops.RemoveAll(t => t == null);
        }

        // ====================================================================
        // P&C: Building HP / Defense Stats — shown in info panel for defensive buildings
        // ====================================================================

        /// <summary>P&C: Static defense stats per building type and tier. (HP, DEF, Garrison Capacity)</summary>
        private static readonly Dictionary<string, (int BaseHP, int BaseDEF, int BaseGarrison)> DefenseStats = new()
        {
            { "stronghold",   (5000, 200, 500) },
            { "wall",         (2000, 300, 200) },
            { "watch_tower",  (1500, 150, 100) },
            { "barracks",     (800,  50,  0) },
            { "training_ground", (600, 40, 0) },
            { "armory",       (700, 60, 0) },
            { "guild_hall",   (1200, 80, 0) },
            { "embassy",      (900, 70, 0) },
        };

        /// <summary>Get scaled HP for a building at given tier.</summary>
        private int GetBuildingHP(string buildingId, int tier)
        {
            if (!DefenseStats.TryGetValue(buildingId, out var stats)) return 0;
            return stats.BaseHP * tier;
        }

        /// <summary>Get scaled DEF for a building at given tier.</summary>
        private int GetBuildingDEF(string buildingId, int tier)
        {
            if (!DefenseStats.TryGetValue(buildingId, out var stats)) return 0;
            return stats.BaseDEF * tier;
        }

        /// <summary>
        /// P&C: Add HP/DEF/Garrison stat rows to building info panel for defensive buildings.
        /// Called from BuildInfoPanelOverviewTab after STATS header.
        /// </summary>
        private void AddDefenseStatsToInfoPanel(Transform panelTransform, string buildingId, int tier, ref float statsY)
        {
            if (!DefenseStats.TryGetValue(buildingId, out var stats)) return;

            int hp = stats.BaseHP * tier;
            int def = stats.BaseDEF * tier;
            int garrison = stats.BaseGarrison * tier;

            // HP row
            string hpStr = hp >= 1000 ? $"{hp / 1000f:F1}K" : $"{hp}";
            int nextHP = stats.BaseHP * Mathf.Min(tier + 1, 3);
            string hpDelta = tier < 3 ? $"  \u2192 {(nextHP >= 1000 ? $"{nextHP / 1000f:F1}K" : $"{nextHP}")}" : "";
            Color hpDeltaColor = tier < 3 ? new Color(0.50f, 0.90f, 0.50f) : new Color(0.60f, 0.60f, 0.60f);

            var hpRowGO = new GameObject("HPRow");
            hpRowGO.transform.SetParent(panelTransform, false);
            var hpRowRect = hpRowGO.AddComponent<RectTransform>();
            hpRowRect.anchorMin = new Vector2(0.05f, statsY - 0.045f);
            hpRowRect.anchorMax = new Vector2(0.95f, statsY);
            hpRowRect.offsetMin = Vector2.zero;
            hpRowRect.offsetMax = Vector2.zero;
            var hpBg = hpRowGO.AddComponent<Image>();
            hpBg.color = new Color(0.85f, 0.25f, 0.20f, 0.08f);
            hpBg.raycastTarget = false;

            AddInfoPanelText(hpRowGO.transform, "Icon", "\u2764", 10, FontStyle.Normal,
                new Color(0.90f, 0.30f, 0.25f), new Vector2(0.02f, 0f), new Vector2(0.12f, 1f), TextAnchor.MiddleCenter);
            AddInfoPanelText(hpRowGO.transform, "Label", "HP", 9, FontStyle.Bold,
                new Color(0.80f, 0.78f, 0.75f), new Vector2(0.13f, 0f), new Vector2(0.28f, 1f), TextAnchor.MiddleLeft);
            AddInfoPanelText(hpRowGO.transform, "Value", hpStr, 10, FontStyle.Bold,
                Color.white, new Vector2(0.30f, 0f), new Vector2(0.55f, 1f), TextAnchor.MiddleLeft);
            if (tier < 3)
                AddInfoPanelText(hpRowGO.transform, "Delta", hpDelta, 9, FontStyle.Normal,
                    hpDeltaColor, new Vector2(0.55f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleLeft);
            statsY -= 0.05f;

            // DEF row
            string defStr = def >= 1000 ? $"{def / 1000f:F1}K" : $"{def}";
            int nextDEF = stats.BaseDEF * Mathf.Min(tier + 1, 3);
            string defDelta = tier < 3 ? $"  \u2192 {(nextDEF >= 1000 ? $"{nextDEF / 1000f:F1}K" : $"{nextDEF}")}" : "";

            var defRowGO = new GameObject("DEFRow");
            defRowGO.transform.SetParent(panelTransform, false);
            var defRowRect = defRowGO.AddComponent<RectTransform>();
            defRowRect.anchorMin = new Vector2(0.05f, statsY - 0.045f);
            defRowRect.anchorMax = new Vector2(0.95f, statsY);
            defRowRect.offsetMin = Vector2.zero;
            defRowRect.offsetMax = Vector2.zero;
            var defBg = defRowGO.AddComponent<Image>();
            defBg.color = new Color(0.25f, 0.50f, 0.85f, 0.08f);
            defBg.raycastTarget = false;

            AddInfoPanelText(defRowGO.transform, "Icon", "\u26E8", 10, FontStyle.Normal,
                new Color(0.35f, 0.60f, 0.90f), new Vector2(0.02f, 0f), new Vector2(0.12f, 1f), TextAnchor.MiddleCenter);
            AddInfoPanelText(defRowGO.transform, "Label", "DEF", 9, FontStyle.Bold,
                new Color(0.80f, 0.78f, 0.75f), new Vector2(0.13f, 0f), new Vector2(0.28f, 1f), TextAnchor.MiddleLeft);
            AddInfoPanelText(defRowGO.transform, "Value", defStr, 10, FontStyle.Bold,
                Color.white, new Vector2(0.30f, 0f), new Vector2(0.55f, 1f), TextAnchor.MiddleLeft);
            if (tier < 3)
                AddInfoPanelText(defRowGO.transform, "Delta", defDelta, 9, FontStyle.Normal,
                    new Color(0.50f, 0.90f, 0.50f), new Vector2(0.55f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleLeft);
            statsY -= 0.05f;

            // Garrison row (only for buildings with garrison)
            if (stats.BaseGarrison > 0)
            {
                int garr = stats.BaseGarrison * tier;
                string garrStr = garr >= 1000 ? $"{garr / 1000f:F1}K" : $"{garr}";
                int nextGarr = stats.BaseGarrison * Mathf.Min(tier + 1, 3);
                string garrDelta = tier < 3 ? $"  \u2192 {(nextGarr >= 1000 ? $"{nextGarr / 1000f:F1}K" : $"{nextGarr}")}" : "";

                var garrRowGO = new GameObject("GarrRow");
                garrRowGO.transform.SetParent(panelTransform, false);
                var garrRowRect = garrRowGO.AddComponent<RectTransform>();
                garrRowRect.anchorMin = new Vector2(0.05f, statsY - 0.045f);
                garrRowRect.anchorMax = new Vector2(0.95f, statsY);
                garrRowRect.offsetMin = Vector2.zero;
                garrRowRect.offsetMax = Vector2.zero;
                var garrBg = garrRowGO.AddComponent<Image>();
                garrBg.color = new Color(0.70f, 0.55f, 0.15f, 0.08f);
                garrBg.raycastTarget = false;

                AddInfoPanelText(garrRowGO.transform, "Icon", "\u265F", 10, FontStyle.Normal,
                    new Color(0.85f, 0.70f, 0.30f), new Vector2(0.02f, 0f), new Vector2(0.12f, 1f), TextAnchor.MiddleCenter);
                AddInfoPanelText(garrRowGO.transform, "Label", "Garrison", 9, FontStyle.Bold,
                    new Color(0.80f, 0.78f, 0.75f), new Vector2(0.13f, 0f), new Vector2(0.35f, 1f), TextAnchor.MiddleLeft);
                AddInfoPanelText(garrRowGO.transform, "Value", garrStr, 10, FontStyle.Bold,
                    Color.white, new Vector2(0.36f, 0f), new Vector2(0.55f, 1f), TextAnchor.MiddleLeft);
                if (tier < 3)
                    AddInfoPanelText(garrRowGO.transform, "Delta", garrDelta, 9, FontStyle.Normal,
                        new Color(0.50f, 0.90f, 0.50f), new Vector2(0.55f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleLeft);
                statsY -= 0.05f;
            }
        }

        // ====================================================================
        // P&C: Hero Assignment to Buildings — assign heroes for production bonuses
        // ====================================================================

        private readonly Dictionary<string, string> _heroAssignments = new(); // instanceId → heroId
        private GameObject _heroAssignPanel;

        /// <summary>P&C: Which building types can have a hero assigned.</summary>
        private static readonly HashSet<string> HeroAssignableBuildings = new()
        {
            "grain_farm", "iron_mine", "stone_quarry", "arcane_tower",
            "barracks", "training_ground", "forge", "academy", "laboratory",
        };

        /// <summary>P&C: Get production bonus % from assigned hero level.</summary>
        private int GetHeroAssignmentBonus(string instanceId)
        {
            if (!_heroAssignments.TryGetValue(instanceId, out var heroId)) return 0;
            if (!ServiceLocator.TryGet<HeroRoster>(out var roster)) return 0;
            var hero = roster.GetHero(heroId);
            if (hero == null) return 0;
            return 5 + hero.Level * 2; // 7% at Lv1, 15% at Lv5, etc.
        }

        /// <summary>P&C: Show hero assignment panel for a building.</summary>
        private void ShowHeroAssignPanel(string instanceId, string buildingId, int tier)
        {
            if (_heroAssignPanel != null) { Destroy(_heroAssignPanel); _heroAssignPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _heroAssignPanel = new GameObject("HeroAssignPanel");
            _heroAssignPanel.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _heroAssignPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _heroAssignPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _heroAssignPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_heroAssignPanel != null) { Destroy(_heroAssignPanel); _heroAssignPanel = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_heroAssignPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.20f);
            panelRect.anchorMax = new Vector2(0.92f, 0.80f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            AddInfoPanelText(panel.transform, "Title", $"\u265F Assign Hero — {displayName}", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            // Current assignment
            string currentHeroId = _heroAssignments.TryGetValue(instanceId, out var h) ? h : null;
            if (currentHeroId != null)
            {
                int bonus = GetHeroAssignmentBonus(instanceId);
                AddInfoPanelText(panel.transform, "Current",
                    $"Currently assigned: {currentHeroId} (+{bonus}% bonus)", 10, FontStyle.Normal,
                    new Color(0.50f, 0.90f, 0.50f),
                    new Vector2(0.05f, 0.83f), new Vector2(0.70f, 0.90f), TextAnchor.MiddleLeft);

                // Unassign button
                var unGO = new GameObject("UnassignBtn");
                unGO.transform.SetParent(panel.transform, false);
                var unRect = unGO.AddComponent<RectTransform>();
                unRect.anchorMin = new Vector2(0.72f, 0.83f);
                unRect.anchorMax = new Vector2(0.95f, 0.90f);
                unRect.offsetMin = Vector2.zero;
                unRect.offsetMax = Vector2.zero;
                var unBg = unGO.AddComponent<Image>();
                unBg.color = new Color(0.60f, 0.20f, 0.18f, 0.90f);
                unBg.raycastTarget = true;
                var unBtn = unGO.AddComponent<Button>();
                unBtn.targetGraphic = unBg;
                string capInst = instanceId;
                string capBld = buildingId;
                int capTier = tier;
                unBtn.onClick.AddListener(() =>
                {
                    _heroAssignments.Remove(capInst);
                    RefreshHeroAssignBadge(capInst);
                    ShowHeroAssignPanel(capInst, capBld, capTier);
                });
                AddInfoPanelText(unGO.transform, "Label", "\u2715 Remove", 9, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
            else
            {
                AddInfoPanelText(panel.transform, "NoHero", "No hero assigned — tap a hero to assign", 10, FontStyle.Normal,
                    new Color(0.60f, 0.58f, 0.55f),
                    new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.90f), TextAnchor.MiddleCenter);
            }

            // Separator
            var sep = new GameObject("Sep");
            sep.transform.SetParent(panel.transform, false);
            var sepRect = sep.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.05f, 0.815f);
            sepRect.anchorMax = new Vector2(0.95f, 0.82f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.78f, 0.62f, 0.22f, 0.3f);
            sepImg.raycastTarget = false;

            // Hero grid: show owned heroes
            float rowY = 0.78f;
            float rowH = 0.10f;

            // Collect already-assigned hero IDs (can't assign same hero to 2 buildings)
            var assignedHeroes = new HashSet<string>();
            foreach (var kvp in _heroAssignments) assignedHeroes.Add(kvp.Value);

            // Get hero roster
            if (ServiceLocator.TryGet<HeroRoster>(out var roster))
            {
                int col = 0;
                foreach (var kvp in roster.OwnedHeroes)
                {
                    var hero = kvp.Value;
                    bool isAssigned = assignedHeroes.Contains(hero.HeroId);
                    bool isCurrentAssigned = hero.HeroId == currentHeroId;

                    float x0 = 0.03f + col * 0.235f;
                    float x1 = x0 + 0.22f;

                    var heroGO = new GameObject($"Hero_{hero.HeroId}");
                    heroGO.transform.SetParent(panel.transform, false);
                    var heroRect = heroGO.AddComponent<RectTransform>();
                    heroRect.anchorMin = new Vector2(x0, rowY - rowH);
                    heroRect.anchorMax = new Vector2(x1, rowY);
                    heroRect.offsetMin = Vector2.zero;
                    heroRect.offsetMax = Vector2.zero;

                    var heroBg = heroGO.AddComponent<Image>();
                    heroBg.color = isCurrentAssigned
                        ? new Color(0.20f, 0.50f, 0.25f, 0.85f)
                        : isAssigned
                            ? new Color(0.30f, 0.28f, 0.32f, 0.60f)
                            : new Color(0.12f, 0.10f, 0.18f, 0.85f);
                    heroBg.raycastTarget = true;

                    var heroOutline = heroGO.AddComponent<Outline>();
                    heroOutline.effectColor = isCurrentAssigned
                        ? new Color(0.40f, 0.90f, 0.50f, 0.6f)
                        : new Color(0.60f, 0.50f, 0.25f, 0.4f);
                    heroOutline.effectDistance = new Vector2(0.6f, -0.6f);

                    if (!isAssigned || isCurrentAssigned)
                    {
                        var heroBtn = heroGO.AddComponent<Button>();
                        heroBtn.targetGraphic = heroBg;
                        string capHeroId = hero.HeroId;
                        string capInstId = instanceId;
                        string capBldId = buildingId;
                        int capTierL = tier;
                        heroBtn.onClick.AddListener(() =>
                        {
                            _heroAssignments[capInstId] = capHeroId;
                            RefreshHeroAssignBadge(capInstId);
                            ShowHeroAssignPanel(capInstId, capBldId, capTierL);
                        });
                    }

                    // Hero name
                    string shortName = hero.HeroId.Length > 12 ? hero.HeroId.Substring(0, 12) : hero.HeroId;
                    AddInfoPanelText(heroGO.transform, "Name", shortName, 8, FontStyle.Bold,
                        Color.white, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f), TextAnchor.MiddleCenter);

                    // Level + star
                    string stars = new string('\u2605', hero.StarTier);
                    AddInfoPanelText(heroGO.transform, "Level", $"Lv.{hero.Level} {stars}", 7, FontStyle.Normal,
                        new Color(0.95f, 0.82f, 0.35f),
                        new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.50f), TextAnchor.MiddleCenter);

                    // Bonus preview
                    int previewBonus = 5 + hero.Level * 2;
                    AddInfoPanelText(heroGO.transform, "Bonus", $"+{previewBonus}%", 7, FontStyle.Bold,
                        new Color(0.50f, 0.90f, 0.50f),
                        new Vector2(0.65f, 0.60f), new Vector2(0.98f, 0.95f), TextAnchor.MiddleRight);

                    if (isAssigned && !isCurrentAssigned)
                    {
                        AddInfoPanelText(heroGO.transform, "Busy", "BUSY", 7, FontStyle.Bold,
                            new Color(0.90f, 0.45f, 0.35f),
                            new Vector2(0.05f, 0.60f), new Vector2(0.50f, 0.95f), TextAnchor.MiddleLeft);
                    }

                    col++;
                    if (col >= 4)
                    {
                        col = 0;
                        rowY -= rowH + 0.015f;
                    }
                }
            }

            // Bonus explanation
            AddInfoPanelText(panel.transform, "Help",
                "Assigned heroes boost building output by 5% + 2% per hero level.\nHeroes can only be assigned to one building at a time.",
                8, FontStyle.Normal, new Color(0.55f, 0.53f, 0.50f),
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.10f), TextAnchor.MiddleCenter);

            // Close button
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
            closeBtn.onClick.AddListener(() => { if (_heroAssignPanel != null) { Destroy(_heroAssignPanel); _heroAssignPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _heroAssignPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        /// <summary>P&C: Show/hide the small hero portrait badge on a building that has an assigned hero.</summary>
        private void RefreshHeroAssignBadge(string instanceId)
        {
            foreach (var p in _placements)
            {
                if (p.InstanceId != instanceId || p.VisualGO == null) continue;

                // Remove existing badge
                var existing = p.VisualGO.transform.Find("HeroAssignBadge");
                if (existing != null) Destroy(existing.gameObject);

                if (!_heroAssignments.TryGetValue(instanceId, out var heroId)) break;

                // Create small hero badge on the building
                var badge = new GameObject("HeroAssignBadge");
                badge.transform.SetParent(p.VisualGO.transform, false);
                var badgeRect = badge.AddComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.70f, 0.75f);
                badgeRect.anchorMax = new Vector2(0.95f, 0.95f);
                badgeRect.offsetMin = Vector2.zero;
                badgeRect.offsetMax = Vector2.zero;

                var badgeBg = badge.AddComponent<Image>();
                badgeBg.color = new Color(0.12f, 0.30f, 0.55f, 0.90f);
                badgeBg.raycastTarget = false;
                var badgeOutline = badge.AddComponent<Outline>();
                badgeOutline.effectColor = new Color(0.40f, 0.65f, 0.90f, 0.7f);
                badgeOutline.effectDistance = new Vector2(0.5f, -0.5f);

                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(badge.transform, false);
                var iconRect = iconGO.AddComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconText = iconGO.AddComponent<Text>();
                iconText.text = "\u265F";
                iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                iconText.fontSize = 8;
                iconText.alignment = TextAnchor.MiddleCenter;
                iconText.color = Color.white;
                iconText.raycastTarget = false;

                break;
            }
        }

        // ====================================================================
        // P&C: City Peace Shield — translucent dome over city when shield is active
        // ====================================================================

        private GameObject _peaceShield;
        private float _peaceShieldTimer = 28800f; // 8 hours default
        private bool _peaceShieldActive = true;

        /// <summary>P&C: Create the peace shield visual dome over the city.</summary>
        private void CreatePeaceShield()
        {
            if (_peaceShield != null) return;
            if (!_peaceShieldActive) return;

            var container = buildingContainer ?? contentContainer;
            if (container == null) return;

            _peaceShield = new GameObject("PeaceShield");
            _peaceShield.transform.SetParent(container, false);
            // Set as last sibling so it renders above buildings
            _peaceShield.transform.SetAsLastSibling();

            var rect = _peaceShield.AddComponent<RectTransform>();
            // Cover entire city area centered on stronghold
            Vector2 shCenter = GridToLocalCenter(new Vector2Int(22, 22), new Vector2Int(6, 6));
            float shieldRadius = CellSize * 18f;
            rect.anchoredPosition = shCenter;
            rect.sizeDelta = new Vector2(shieldRadius * 2, shieldRadius * 1.2f);

            var img = _peaceShield.AddComponent<Image>();
            img.color = new Color(0.30f, 0.60f, 0.95f, 0.08f);
            img.raycastTarget = false;

            // Try radial gradient for dome look
            var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
#if UNITY_EDITOR
            if (spr == null)
                spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
#endif
            if (spr != null) img.sprite = spr;

            // Shield border ring
            var ring = new GameObject("ShieldRing");
            ring.transform.SetParent(_peaceShield.transform, false);
            var ringRect = ring.AddComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(-0.02f, -0.02f);
            ringRect.anchorMax = new Vector2(1.02f, 1.02f);
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;
            var ringImg = ring.AddComponent<Image>();
            ringImg.color = new Color(0.35f, 0.65f, 0.95f, 0.12f);
            ringImg.raycastTarget = false;
            if (spr != null) ringImg.sprite = spr;

            // Timer label above shield
            var timerGO = new GameObject("ShieldTimer");
            timerGO.transform.SetParent(_peaceShield.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.30f, 0.85f);
            timerRect.anchorMax = new Vector2(0.70f, 0.98f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;

            // Timer bg pill
            var timerBg = timerGO.AddComponent<Image>();
            timerBg.color = new Color(0.10f, 0.25f, 0.50f, 0.80f);
            timerBg.raycastTarget = false;
            var timerOutline = timerGO.AddComponent<Outline>();
            timerOutline.effectColor = new Color(0.35f, 0.60f, 0.90f, 0.5f);
            timerOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(timerGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var timerText = textGO.AddComponent<Text>();
            timerText.text = $"\u26E8 Shield: {FormatTimeRemaining((int)_peaceShieldTimer)}";
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 8;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = new Color(0.70f, 0.85f, 1f);
            timerText.raycastTarget = false;

            StartCoroutine(AnimatePeaceShield());
        }

        private IEnumerator AnimatePeaceShield()
        {
            while (_peaceShield != null && _peaceShieldActive)
            {
                // Gentle pulse
                var img = _peaceShield.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.06f + 0.04f * Mathf.Sin(Time.time * 1.5f);
                    img.color = new Color(0.30f, 0.60f, 0.95f, pulse);
                }

                // Update timer
                _peaceShieldTimer -= Time.deltaTime;
                if (_peaceShieldTimer <= 0)
                {
                    _peaceShieldActive = false;
                    DestroyPeaceShield();
                    yield break;
                }

                // Update timer text
                var timerText = _peaceShield?.transform.Find("ShieldTimer/Text")?.GetComponent<Text>();
                if (timerText != null)
                    timerText.text = $"\u26E8 Shield: {FormatTimeRemaining((int)_peaceShieldTimer)}";

                yield return null;
            }
        }

        private void DestroyPeaceShield()
        {
            if (_peaceShield != null) { Destroy(_peaceShield); _peaceShield = null; }
        }

        // ====================================================================
        // P&C: Cancel Upgrade with Partial Refund
        // ====================================================================

        private GameObject _cancelUpgradeDialog;

        /// <summary>P&C: Show cancel upgrade confirmation with partial refund details.</summary>
        private void ShowCancelUpgradeDialog(string instanceId, string buildingId, int targetTier, float remainingSeconds)
        {
            if (_cancelUpgradeDialog != null) { Destroy(_cancelUpgradeDialog); _cancelUpgradeDialog = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _cancelUpgradeDialog = new GameObject("CancelUpgradeDialog");
            _cancelUpgradeDialog.transform.SetParent(canvas.transform, false);

            // Dim overlay
            var dimRect = _cancelUpgradeDialog.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _cancelUpgradeDialog.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _cancelUpgradeDialog.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_cancelUpgradeDialog != null) { Destroy(_cancelUpgradeDialog); _cancelUpgradeDialog = null; } });

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_cancelUpgradeDialog.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.30f);
            panelRect.anchorMax = new Vector2(0.88f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.90f, 0.40f, 0.25f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            AddInfoPanelText(panel.transform, "Title", $"\u26A0 Cancel Upgrade?", 15, FontStyle.Bold,
                new Color(0.95f, 0.55f, 0.30f),
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f), TextAnchor.MiddleCenter);

            AddInfoPanelText(panel.transform, "SubTitle",
                $"{displayName} Lv.{targetTier - 1} \u2192 Lv.{targetTier}", 12, FontStyle.Normal,
                new Color(0.80f, 0.78f, 0.75f),
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.85f), TextAnchor.MiddleCenter);

            // Calculate refund (50% of original cost)
            int baseCost = targetTier * 500;
            int stoneRefund = baseCost / 2;
            int ironRefund = (baseCost * 3 / 4) / 2;
            int grainRefund = (baseCost / 2) / 2;

            AddInfoPanelText(panel.transform, "RefundHeader", "Partial Refund (50%):", 11, FontStyle.Bold,
                new Color(0.70f, 0.85f, 0.70f),
                new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.70f), TextAnchor.MiddleCenter);

            // Refund amounts
            string refundText = $"\u25C6 Stone: {stoneRefund}  \u25C6 Iron: {ironRefund}  \u25C6 Grain: {grainRefund}";
            AddInfoPanelText(panel.transform, "Refunds", refundText, 10, FontStyle.Normal,
                new Color(0.85f, 0.80f, 0.55f),
                new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.60f), TextAnchor.MiddleCenter);

            // Warning
            AddInfoPanelText(panel.transform, "Warning",
                "All progress will be lost.\nThe building will return to Lv." + (targetTier - 1) + ".",
                10, FontStyle.Normal, new Color(0.90f, 0.50f, 0.40f),
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.48f), TextAnchor.MiddleCenter);

            // Cancel upgrade button (confirm)
            var confirmGO = new GameObject("ConfirmCancelBtn");
            confirmGO.transform.SetParent(panel.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.08f, 0.05f);
            confirmRect.anchorMax = new Vector2(0.48f, 0.18f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = new Color(0.70f, 0.20f, 0.15f, 0.90f);
            confirmBg.raycastTarget = true;
            var confirmOutline = confirmGO.AddComponent<Outline>();
            confirmOutline.effectColor = new Color(0.90f, 0.35f, 0.25f, 0.6f);
            confirmOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmBg;
            string capId = instanceId;
            confirmBtn.onClick.AddListener(() =>
            {
                // Refund resources
                if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                {
                    rm.AddResource(ResourceType.Stone, stoneRefund);
                    rm.AddResource(ResourceType.Iron, ironRefund);
                    rm.AddResource(ResourceType.Grain, grainRefund);
                }
                // Cancel the build
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                    bm.CancelUpgrade(capId);
                if (_cancelUpgradeDialog != null) { Destroy(_cancelUpgradeDialog); _cancelUpgradeDialog = null; }
                ShowUpgradeBlockedToast("\u26A0 Upgrade cancelled. 50% resources refunded.");
                // Refresh visuals
                foreach (var p in _placements)
                {
                    if (p.InstanceId == capId && p.VisualGO != null)
                    {
                        RemoveScaffoldingOverlay(p.VisualGO);
                        var progressBar = p.VisualGO.transform.Find("UpgradeProgressBar");
                        if (progressBar != null) Destroy(progressBar.gameObject);
                    }
                }
            });
            AddInfoPanelText(confirmGO.transform, "Label", "\u2620 Cancel Upgrade", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Keep building button
            var keepGO = new GameObject("KeepBtn");
            keepGO.transform.SetParent(panel.transform, false);
            var keepRect = keepGO.AddComponent<RectTransform>();
            keepRect.anchorMin = new Vector2(0.52f, 0.05f);
            keepRect.anchorMax = new Vector2(0.92f, 0.18f);
            keepRect.offsetMin = Vector2.zero;
            keepRect.offsetMax = Vector2.zero;
            var keepBg = keepGO.AddComponent<Image>();
            keepBg.color = new Color(0.15f, 0.50f, 0.25f, 0.90f);
            keepBg.raycastTarget = true;
            var keepOutline = keepGO.AddComponent<Outline>();
            keepOutline.effectColor = new Color(0.35f, 0.80f, 0.45f, 0.6f);
            keepOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var keepBtn = keepGO.AddComponent<Button>();
            keepBtn.targetGraphic = keepBg;
            keepBtn.onClick.AddListener(() => { if (_cancelUpgradeDialog != null) { Destroy(_cancelUpgradeDialog); _cancelUpgradeDialog = null; } });
            AddInfoPanelText(keepGO.transform, "Label", "\u2713 Keep Building", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _cancelUpgradeDialog.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        // ====================================================================
        // P&C: VIP Level System — city-wide bonuses tied to VIP tier
        // ====================================================================

        private int _vipLevel = 2; // Default VIP level (simulated)
        private int _vipPoints = 350;
        private GameObject _vipPanel;

        /// <summary>P&C VIP tier thresholds and bonuses.</summary>
        private static readonly (int PointsRequired, string BonusDesc, Color TierColor)[] VipTiers = new[]
        {
            (0,    "+0% — No VIP bonuses", new Color(0.50f, 0.50f, 0.50f)),
            (100,  "+5% resource production", new Color(0.55f, 0.75f, 0.55f)),
            (300,  "+10% resource, +5% build speed", new Color(0.55f, 0.70f, 0.90f)),
            (600,  "+15% resource, +10% build speed", new Color(0.70f, 0.55f, 0.90f)),
            (1000, "+20% resource, +15% build, +5% research", new Color(0.90f, 0.70f, 0.30f)),
            (2000, "+25% resource, +20% build, 2nd builder free", new Color(0.95f, 0.55f, 0.25f)),
            (4000, "+30% all, 3rd builder slot", new Color(0.95f, 0.35f, 0.35f)),
        };

        /// <summary>P&C: Show VIP info panel with current level, bonuses, and progress to next.</summary>
        private void ShowVipPanel()
        {
            if (_vipPanel != null) { Destroy(_vipPanel); _vipPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _vipPanel = new GameObject("VipPanel");
            _vipPanel.transform.SetParent(canvas.transform, false);

            var dimRect = _vipPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _vipPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _vipPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_vipPanel != null) { Destroy(_vipPanel); _vipPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_vipPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.18f);
            panelRect.anchorMax = new Vector2(0.92f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.90f, 0.70f, 0.20f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            Color tierColor = _vipLevel < VipTiers.Length ? VipTiers[_vipLevel].TierColor : new Color(0.95f, 0.82f, 0.35f);
            AddInfoPanelText(panel.transform, "Title", $"\u2B50 VIP Level {_vipLevel}", 18, FontStyle.Bold,
                tierColor,
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            // VIP points progress
            int currentReq = _vipLevel < VipTiers.Length ? VipTiers[_vipLevel].PointsRequired : 0;
            int nextReq = (_vipLevel + 1) < VipTiers.Length ? VipTiers[_vipLevel + 1].PointsRequired : currentReq;
            float progress = nextReq > currentReq ? (float)(_vipPoints - currentReq) / (nextReq - currentReq) : 1f;

            AddInfoPanelText(panel.transform, "Points", $"VIP Points: {_vipPoints}", 11, FontStyle.Normal,
                new Color(0.80f, 0.78f, 0.72f),
                new Vector2(0.05f, 0.84f), new Vector2(0.55f, 0.90f), TextAnchor.MiddleLeft);

            if (_vipLevel + 1 < VipTiers.Length)
            {
                AddInfoPanelText(panel.transform, "NextReq", $"Next: {nextReq} pts", 10, FontStyle.Normal,
                    new Color(0.60f, 0.58f, 0.55f),
                    new Vector2(0.58f, 0.84f), new Vector2(0.95f, 0.90f), TextAnchor.MiddleRight);
            }

            // Progress bar
            var progBg = new GameObject("ProgBg");
            progBg.transform.SetParent(panel.transform, false);
            var progBgRect = progBg.AddComponent<RectTransform>();
            progBgRect.anchorMin = new Vector2(0.05f, 0.80f);
            progBgRect.anchorMax = new Vector2(0.95f, 0.84f);
            progBgRect.offsetMin = Vector2.zero;
            progBgRect.offsetMax = Vector2.zero;
            var progBgImg = progBg.AddComponent<Image>();
            progBgImg.color = new Color(0.12f, 0.10f, 0.18f, 0.90f);
            progBgImg.raycastTarget = false;

            var progFill = new GameObject("Fill");
            progFill.transform.SetParent(progBg.transform, false);
            var progFillRect = progFill.AddComponent<RectTransform>();
            progFillRect.anchorMin = Vector2.zero;
            progFillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            progFillRect.offsetMin = Vector2.zero;
            progFillRect.offsetMax = Vector2.zero;
            var progFillImg = progFill.AddComponent<Image>();
            progFillImg.color = tierColor;
            progFillImg.raycastTarget = false;

            // Current bonuses header
            AddInfoPanelText(panel.transform, "BonusHeader", "ACTIVE BONUSES", 12, FontStyle.Bold,
                new Color(0.70f, 0.85f, 0.70f),
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.79f), TextAnchor.MiddleCenter);

            // Current tier bonuses
            string currentBonus = _vipLevel < VipTiers.Length ? VipTiers[_vipLevel].BonusDesc : "Max VIP reached!";
            AddInfoPanelText(panel.transform, "CurrentBonus", currentBonus, 11, FontStyle.Normal,
                new Color(0.50f, 0.90f, 0.50f),
                new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.72f), TextAnchor.MiddleCenter);

            // All VIP tiers list
            float tierY = 0.62f;
            float tierRowH = 0.075f;
            for (int i = 0; i < VipTiers.Length && tierY > 0.12f; i++)
            {
                var (pts, desc, color) = VipTiers[i];
                bool isCurrentTier = i == _vipLevel;
                bool isUnlocked = i <= _vipLevel;

                var rowGO = new GameObject($"VipTier_{i}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.05f, tierY - tierRowH);
                rowRect.anchorMax = new Vector2(0.95f, tierY);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = isCurrentTier
                    ? new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.40f)
                    : new Color(0.10f, 0.08f, 0.15f, isUnlocked ? 0.30f : 0.15f);
                rowBg.raycastTarget = false;

                if (isCurrentTier)
                {
                    var rowOutline = rowGO.AddComponent<Outline>();
                    rowOutline.effectColor = new Color(color.r, color.g, color.b, 0.5f);
                    rowOutline.effectDistance = new Vector2(0.5f, -0.5f);
                }

                string checkMark = isUnlocked ? "\u2713" : "\u2717";
                Color checkColor = isUnlocked ? new Color(0.50f, 0.90f, 0.50f) : new Color(0.50f, 0.45f, 0.45f);
                AddInfoPanelText(rowGO.transform, "Check", checkMark, 10, FontStyle.Bold,
                    checkColor, new Vector2(0.01f, 0f), new Vector2(0.08f, 1f), TextAnchor.MiddleCenter);

                Color labelColor = isUnlocked ? color : new Color(0.50f, 0.48f, 0.45f);
                AddInfoPanelText(rowGO.transform, "Level", $"VIP {i}", 9, FontStyle.Bold,
                    labelColor, new Vector2(0.09f, 0f), new Vector2(0.22f, 1f), TextAnchor.MiddleLeft);
                AddInfoPanelText(rowGO.transform, "Desc", desc, 8, FontStyle.Normal,
                    new Color(0.75f, 0.73f, 0.70f), new Vector2(0.23f, 0f), new Vector2(0.85f, 1f), TextAnchor.MiddleLeft);
                AddInfoPanelText(rowGO.transform, "Pts", $"{pts}pts", 8, FontStyle.Normal,
                    new Color(0.60f, 0.58f, 0.55f), new Vector2(0.86f, 0f), new Vector2(0.99f, 1f), TextAnchor.MiddleRight);

                tierY -= tierRowH + 0.005f;
            }

            // Buy VIP points button
            var buyGO = new GameObject("BuyVipBtn");
            buyGO.transform.SetParent(panel.transform, false);
            var buyRect = buyGO.AddComponent<RectTransform>();
            buyRect.anchorMin = new Vector2(0.15f, 0.02f);
            buyRect.anchorMax = new Vector2(0.55f, 0.10f);
            buyRect.offsetMin = Vector2.zero;
            buyRect.offsetMax = Vector2.zero;
            var buyBg = buyGO.AddComponent<Image>();
            buyBg.color = new Color(0.55f, 0.25f, 0.70f, 0.92f);
            buyBg.raycastTarget = true;
            var buyOutline = buyGO.AddComponent<Outline>();
            buyOutline.effectColor = new Color(0.75f, 0.50f, 0.90f, 0.6f);
            buyOutline.effectDistance = new Vector2(0.6f, -0.6f);
            var buyBtn = buyGO.AddComponent<Button>();
            buyBtn.targetGraphic = buyBg;
            buyBtn.onClick.AddListener(() =>
            {
                _vipPoints += 200;
                // Check for level up
                while (_vipLevel + 1 < VipTiers.Length && _vipPoints >= VipTiers[_vipLevel + 1].PointsRequired)
                    _vipLevel++;
                if (_vipPanel != null) { Destroy(_vipPanel); _vipPanel = null; }
                ShowVipPanel(); // Refresh
                ShowUpgradeBlockedToast($"\u2B50 +200 VIP Points! Now VIP {_vipLevel}");
            });
            AddInfoPanelText(buyGO.transform, "Label", "\u2B25 200 Gems \u2192 200 VIP", 9, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.60f, 0.02f);
            closeRect.anchorMax = new Vector2(0.85f, 0.10f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.40f, 0.35f, 0.35f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_vipPanel != null) { Destroy(_vipPanel); _vipPanel = null; } });
            AddInfoPanelText(closeGO.transform, "Label", "Close", 10, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var fadeCg = _vipPanel.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            StartCoroutine(FadeInDialog(fadeCg));
        }

        // ====================================================================
        // P&C: Building Skin / Appearance Selector
        // ====================================================================

        private readonly Dictionary<string, int> _buildingSkins = new(); // instanceId → skin index
        private GameObject _skinPanel;

        /// <summary>P&C: Available skins per building type. Each skin has a name, tint, and style description.</summary>
        private static readonly (string Name, Color Tint, string Desc)[] BuildingSkinOptions = new[]
        {
            ("Default", Color.white, "Standard appearance"),
            ("Frost", new Color(0.75f, 0.88f, 1.0f), "Ice-blue winter theme"),
            ("Infernal", new Color(1.0f, 0.70f, 0.55f), "Lava-forged dark stone"),
            ("Verdant", new Color(0.75f, 1.0f, 0.80f), "Overgrown with vines"),
            ("Royal", new Color(1.0f, 0.90f, 0.65f), "Gold-trimmed palace style"),
            ("Shadow", new Color(0.70f, 0.65f, 0.80f), "Void-touched dark magic"),
        };

        /// <summary>P&C: Show skin selector for a specific building.</summary>
        private void ShowBuildingSkinPanel(string instanceId, string buildingId, int tier)
        {
            if (_skinPanel != null) { Destroy(_skinPanel); _skinPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _skinPanel = new GameObject("SkinPanel");
            _skinPanel.transform.SetParent(canvas.transform, false);

            var dimRect = _skinPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _skinPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _skinPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_skinPanel != null) { Destroy(_skinPanel); _skinPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_skinPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.25f);
            panelRect.anchorMax = new Vector2(0.90f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;
            AddInfoPanelText(panel.transform, "Title", $"\u2728 Building Skins — {displayName}", 13, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.35f),
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            int currentSkin = _buildingSkins.TryGetValue(instanceId, out var s) ? s : 0;

            // Building preview with current skin
            Sprite buildSprite = LoadBuildingSprite(buildingId, tier);
            if (buildSprite != null)
            {
                var previewGO = new GameObject("Preview");
                previewGO.transform.SetParent(panel.transform, false);
                var previewRect = previewGO.AddComponent<RectTransform>();
                previewRect.anchorMin = new Vector2(0.35f, 0.55f);
                previewRect.anchorMax = new Vector2(0.65f, 0.87f);
                previewRect.offsetMin = Vector2.zero;
                previewRect.offsetMax = Vector2.zero;
                var previewImg = previewGO.AddComponent<Image>();
                previewImg.sprite = buildSprite;
                previewImg.preserveAspect = true;
                previewImg.color = BuildingSkinOptions[currentSkin].Tint;
                previewImg.raycastTarget = false;
            }

            // Skin option grid (2 rows x 3 cols)
            for (int i = 0; i < BuildingSkinOptions.Length; i++)
            {
                var (name, tint, desc) = BuildingSkinOptions[i];
                int row = i / 3;
                int col = i % 3;
                float x0 = 0.03f + col * 0.32f;
                float x1 = x0 + 0.30f;
                float y1 = 0.50f - row * 0.22f;
                float y0 = y1 - 0.20f;

                bool isSelected = i == currentSkin;
                bool isLocked = i >= 3 && _vipLevel < i; // Higher skins need VIP

                var skinGO = new GameObject($"Skin_{i}");
                skinGO.transform.SetParent(panel.transform, false);
                var skinRect = skinGO.AddComponent<RectTransform>();
                skinRect.anchorMin = new Vector2(x0, y0);
                skinRect.anchorMax = new Vector2(x1, y1);
                skinRect.offsetMin = Vector2.zero;
                skinRect.offsetMax = Vector2.zero;

                var skinBg = skinGO.AddComponent<Image>();
                skinBg.color = isSelected
                    ? new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f, 0.85f)
                    : isLocked
                        ? new Color(0.15f, 0.13f, 0.18f, 0.60f)
                        : new Color(0.12f, 0.10f, 0.18f, 0.80f);
                skinBg.raycastTarget = true;

                if (isSelected)
                {
                    var skinOutline = skinGO.AddComponent<Outline>();
                    skinOutline.effectColor = new Color(tint.r, tint.g, tint.b, 0.7f);
                    skinOutline.effectDistance = new Vector2(0.8f, -0.8f);
                }

                // Color swatch
                var swatchGO = new GameObject("Swatch");
                swatchGO.transform.SetParent(skinGO.transform, false);
                var swatchRect = swatchGO.AddComponent<RectTransform>();
                swatchRect.anchorMin = new Vector2(0.05f, 0.50f);
                swatchRect.anchorMax = new Vector2(0.30f, 0.90f);
                swatchRect.offsetMin = Vector2.zero;
                swatchRect.offsetMax = Vector2.zero;
                var swatchImg = swatchGO.AddComponent<Image>();
                swatchImg.color = isLocked ? new Color(0.30f, 0.28f, 0.32f) : tint;
                swatchImg.raycastTarget = false;

                // Name
                Color nameColor = isLocked ? new Color(0.45f, 0.43f, 0.40f) : Color.white;
                AddInfoPanelText(skinGO.transform, "Name", name, 9, FontStyle.Bold,
                    nameColor, new Vector2(0.32f, 0.50f), new Vector2(0.98f, 0.95f), TextAnchor.MiddleLeft);

                // Status
                string status = isSelected ? "\u2713 Active" : isLocked ? $"\u26BF VIP {i}" : "Tap to apply";
                Color statusColor = isSelected ? new Color(0.50f, 0.90f, 0.50f)
                    : isLocked ? new Color(0.70f, 0.55f, 0.25f) : new Color(0.60f, 0.58f, 0.55f);
                AddInfoPanelText(skinGO.transform, "Status", status, 8, FontStyle.Normal,
                    statusColor, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f), TextAnchor.MiddleCenter);

                if (!isLocked && !isSelected)
                {
                    var skinBtn = skinGO.AddComponent<Button>();
                    skinBtn.targetGraphic = skinBg;
                    int capSkinIdx = i;
                    string capInstId = instanceId;
                    string capBldId = buildingId;
                    int capTierL = tier;
                    skinBtn.onClick.AddListener(() =>
                    {
                        _buildingSkins[capInstId] = capSkinIdx;
                        ApplyBuildingSkin(capInstId, capSkinIdx);
                        if (_skinPanel != null) { Destroy(_skinPanel); _skinPanel = null; }
                        ShowBuildingSkinPanel(capInstId, capBldId, capTierL);
                    });
                }
            }

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.88f);
            closeRect.anchorMax = new Vector2(0.98f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_skinPanel != null) { Destroy(_skinPanel); _skinPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var fadeCg = _skinPanel.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            StartCoroutine(FadeInDialog(fadeCg));
        }

        /// <summary>P&C: Apply a skin tint to a building's visual.</summary>
        private void ApplyBuildingSkin(string instanceId, int skinIndex)
        {
            foreach (var p in _placements)
            {
                if (p.InstanceId != instanceId || p.VisualGO == null) continue;
                var img = p.VisualGO.GetComponent<Image>();
                if (img != null && skinIndex < BuildingSkinOptions.Length)
                    img.color = BuildingSkinOptions[skinIndex].Tint;
                break;
            }
        }

        // ====================================================================
        // P&C: Burning / Under Attack State — buildings show fire after enemy raid
        // ====================================================================

        private readonly HashSet<string> _burningBuildings = new();
        private GameObject _raidAlertBanner;

        /// <summary>P&C: Set a building as burning (post-raid damage state).</summary>
        private void SetBuildingBurning(string instanceId, bool burning)
        {
            if (burning)
                _burningBuildings.Add(instanceId);
            else
                _burningBuildings.Remove(instanceId);

            foreach (var p in _placements)
            {
                if (p.InstanceId != instanceId || p.VisualGO == null) continue;
                if (burning)
                    AddBurningOverlay(p.VisualGO);
                else
                    RemoveBurningOverlay(p.VisualGO);
                break;
            }
        }

        /// <summary>P&C: Add fire/damage overlay to a building.</summary>
        private void AddBurningOverlay(GameObject building)
        {
            RemoveBurningOverlay(building);

            var fire = new GameObject("BurningOverlay");
            fire.transform.SetParent(building.transform, false);
            var rect = fire.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(-0.05f, 0.30f);
            rect.anchorMax = new Vector2(1.05f, 1.10f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Red-orange tint overlay
            var tintImg = fire.AddComponent<Image>();
            tintImg.color = new Color(0.90f, 0.30f, 0.10f, 0.20f);
            tintImg.raycastTarget = false;

            // Animated fire embers
            for (int i = 0; i < 3; i++)
            {
                var ember = new GameObject($"Ember_{i}");
                ember.transform.SetParent(fire.transform, false);
                var eRect = ember.AddComponent<RectTransform>();
                float xPos = 0.15f + i * 0.30f;
                eRect.anchorMin = new Vector2(xPos - 0.05f, 0.20f);
                eRect.anchorMax = new Vector2(xPos + 0.05f, 0.40f);
                eRect.offsetMin = Vector2.zero;
                eRect.offsetMax = Vector2.zero;
                var eText = ember.AddComponent<Text>();
                eText.text = "\u2668"; // Hot springs / fire symbol
                eText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                eText.fontSize = 10;
                eText.alignment = TextAnchor.MiddleCenter;
                eText.color = new Color(1f, 0.50f, 0.15f, 0.90f);
                eText.raycastTarget = false;
            }

            // Smoke text above
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(fire.transform, false);
            var smokeRect = smoke.AddComponent<RectTransform>();
            smokeRect.anchorMin = new Vector2(0.25f, 0.70f);
            smokeRect.anchorMax = new Vector2(0.75f, 0.95f);
            smokeRect.offsetMin = Vector2.zero;
            smokeRect.offsetMax = Vector2.zero;
            var smokeText = smoke.AddComponent<Text>();
            smokeText.text = "\u2601\u2601"; // Cloud symbols for smoke
            smokeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            smokeText.fontSize = 12;
            smokeText.alignment = TextAnchor.MiddleCenter;
            smokeText.color = new Color(0.35f, 0.30f, 0.25f, 0.65f);
            smokeText.raycastTarget = false;

            StartCoroutine(AnimateBurningOverlay(fire));
        }

        private IEnumerator AnimateBurningOverlay(GameObject overlay)
        {
            while (overlay != null)
            {
                // Flicker the fire tint
                var img = overlay.GetComponent<Image>();
                if (img != null)
                {
                    float flicker = 0.15f + 0.10f * Mathf.Sin(Time.time * 5f + Random.value * 2f);
                    img.color = new Color(0.90f, 0.30f, 0.10f, flicker);
                }

                // Animate embers rising
                for (int i = 0; i < overlay.transform.childCount; i++)
                {
                    var child = overlay.transform.GetChild(i);
                    if (child.name.StartsWith("Ember_"))
                    {
                        var eRect = child.GetComponent<RectTransform>();
                        if (eRect != null)
                        {
                            float bob = Mathf.Sin(Time.time * 4f + i * 2.1f) * 0.05f;
                            float baseY = 0.20f + bob;
                            eRect.anchorMin = new Vector2(eRect.anchorMin.x, baseY);
                            eRect.anchorMax = new Vector2(eRect.anchorMax.x, baseY + 0.20f);
                        }
                    }
                }
                yield return null;
            }
        }

        private void RemoveBurningOverlay(GameObject building)
        {
            var existing = building.transform.Find("BurningOverlay");
            if (existing != null) Destroy(existing.gameObject);
        }

        /// <summary>P&C: Show raid alert banner at top of screen.</summary>
        private void ShowRaidAlertBanner(string attackerName)
        {
            if (_raidAlertBanner != null) Destroy(_raidAlertBanner);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _raidAlertBanner = new GameObject("RaidAlertBanner");
            _raidAlertBanner.transform.SetParent(canvas.transform, false);
            _raidAlertBanner.transform.SetAsLastSibling();

            var rect = _raidAlertBanner.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.85f);
            rect.anchorMax = new Vector2(0.95f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _raidAlertBanner.AddComponent<Image>();
            bg.color = new Color(0.70f, 0.15f, 0.10f, 0.92f);
            bg.raycastTarget = true;

            var outline = _raidAlertBanner.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.35f, 0.20f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = _raidAlertBanner.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => { if (_raidAlertBanner != null) { Destroy(_raidAlertBanner); _raidAlertBanner = null; } });

            AddInfoPanelText(_raidAlertBanner.transform, "Text",
                $"\u26A0 UNDER ATTACK! {attackerName} is raiding your city! Tap to defend.", 11, FontStyle.Bold,
                Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            StartCoroutine(FlashRaidBanner());
        }

        private IEnumerator FlashRaidBanner()
        {
            float duration = 10f;
            float elapsed = 0f;
            while (elapsed < duration && _raidAlertBanner != null)
            {
                elapsed += Time.deltaTime;
                var bg = _raidAlertBanner.GetComponent<Image>();
                if (bg != null)
                {
                    float flash = 0.80f + 0.12f * Mathf.Sin(elapsed * 6f);
                    bg.color = new Color(flash, 0.15f, 0.10f, 0.92f);
                }
                yield return null;
            }
            if (_raidAlertBanner != null) { Destroy(_raidAlertBanner); _raidAlertBanner = null; }
        }

        /// <summary>P&C: Simulate a raid for testing — burns random buildings + shows alert.</summary>
        private void SimulateRaid()
        {
            string[] attackNames = { "DarkLord42", "ShadowKing", "IceQueen99", "DragonSlayer" };
            string attacker = attackNames[Random.Range(0, attackNames.Length)];

            // Burn 2-3 random buildings
            int burnCount = Random.Range(2, 4);
            var shuffled = new List<CityBuildingPlacement>(_placements);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            for (int i = 0; i < Mathf.Min(burnCount, shuffled.Count); i++)
            {
                if (shuffled[i].BuildingId != "stronghold") // Don't burn stronghold
                    SetBuildingBurning(shuffled[i].InstanceId, true);
            }

            // P&C: Also damage walls during raids
            foreach (var p in _placements)
            {
                if (p.BuildingId == "wall")
                    DamageWall(p.InstanceId, Random.Range(0.15f, 0.40f));
            }

            ShowRaidAlertBanner(attacker);

            // Auto-repair after 30 seconds (buildings only — walls need manual repair)
            StartCoroutine(AutoRepairAfterRaid(30f));
        }

        private IEnumerator AutoRepairAfterRaid(float delay)
        {
            yield return new WaitForSeconds(delay);
            var toRepair = new List<string>(_burningBuildings);
            foreach (var id in toRepair)
                SetBuildingBurning(id, false);
            ShowUpgradeBlockedToast("\u2692 Buildings repaired!");
        }

        // ====================================================================
        // P&C: Power breakdown long-press
        private bool _powerLongPressTriggered;
        private Coroutine _powerLongPressCoroutine;

        private IEnumerator PowerHudLongPress()
        {
            yield return new WaitForSeconds(0.6f);
            _powerLongPressTriggered = true;
            _powerLongPressCoroutine = null;
            ShowPowerBreakdownPanel();
        }

        // P&C: Quick-Collect Swipe — drag finger across resource buildings to collect
        // ====================================================================

        private bool _swipeCollectMode;
        private readonly HashSet<string> _swipeCollectedThisDrag = new();
        private int _swipeCollectCount;

        /// <summary>P&C: Start swipe-collect mode when drag begins over a resource building with bubbles.</summary>
        private void TryStartSwipeCollect(Vector2 screenPos)
        {
            if (_moveMode || _placementMode) return;

            // Check if we're over a resource building with active bubbles
            string hitInstanceId = GetBuildingAtScreenPos(screenPos);
            if (hitInstanceId == null) return;

            CityBuildingPlacement hitPlacement = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == hitInstanceId) { hitPlacement = p; break; }
            }
            if (hitPlacement == null) return;
            if (!ResourceBuildingTypes.ContainsKey(hitPlacement.BuildingId)) return;

            var spawner = FindAnyObjectByType<ResourceBubbleSpawner>();
            if (spawner == null || spawner.GetTotalActiveBubbleCount() == 0) return;

            _swipeCollectMode = true;
            _swipeCollectedThisDrag.Clear();
            _swipeCollectCount = 0;

            // Collect this building's bubbles immediately
            SwipeCollectBuilding(hitPlacement, spawner);
        }

        /// <summary>P&C: During swipe, check if finger is over another resource building and collect its bubbles.</summary>
        private void UpdateSwipeCollect(Vector2 screenPos)
        {
            if (!_swipeCollectMode) return;

            string hitInstanceId = GetBuildingAtScreenPos(screenPos);
            if (hitInstanceId == null) return;
            if (_swipeCollectedThisDrag.Contains(hitInstanceId)) return;

            CityBuildingPlacement hitPlacement = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == hitInstanceId) { hitPlacement = p; break; }
            }
            if (hitPlacement == null) return;
            if (!ResourceBuildingTypes.ContainsKey(hitPlacement.BuildingId)) return;

            var spawner = FindAnyObjectByType<ResourceBubbleSpawner>();
            if (spawner == null) return;

            SwipeCollectBuilding(hitPlacement, spawner);
        }

        private void SwipeCollectBuilding(CityBuildingPlacement placement, ResourceBubbleSpawner spawner)
        {
            spawner.CollectAllForBuilding(placement.InstanceId);
            _swipeCollectedThisDrag.Add(placement.InstanceId);
            _swipeCollectCount++;

            // Visual feedback: quick bounce on the building
            if (placement.VisualGO != null)
                StartCoroutine(QuickBounce(placement.VisualGO, 0.12f, 1.08f));
        }

        private void EndSwipeCollect()
        {
            if (!_swipeCollectMode) return;
            _swipeCollectMode = false;

            if (_swipeCollectCount > 1)
            {
                ShowUpgradeBlockedToast($"\u26CF Swipe-collected from {_swipeCollectCount} buildings!");
            }
            _swipeCollectedThisDrag.Clear();
            _swipeCollectCount = 0;
        }

        private IEnumerator QuickBounce(GameObject go, float duration, float scale)
        {
            if (go == null) yield break;
            var origScale = go.transform.localScale;
            float half = duration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half && go != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                go.transform.localScale = Vector3.Lerp(origScale, origScale * scale, t);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half && go != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                go.transform.localScale = Vector3.Lerp(origScale * scale, origScale, t);
                yield return null;
            }
            if (go != null) go.transform.localScale = origScale;
        }

        /// <summary>Get building instance ID at a screen position (hit test).</summary>
        private string GetBuildingAtScreenPos(Vector2 screenPos)
        {
            if (buildingContainer == null) return null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                buildingContainer, screenPos, null, out var localPoint);

            // Convert local point to grid position
            Vector2Int gridPos = ScreenToGrid(localPoint);

            // Check occupancy
            if (_occupancy.TryGetValue(gridPos, out var instanceId))
                return instanceId;
            return null;
        }

        /// <summary>Convert local-space point back to grid coordinate.</summary>
        private Vector2Int ScreenToGrid(Vector2 localPoint)
        {
            // Inverse of GridToLocalCenter isometric projection
            float adjustedY = localPoint.y + IsoCenterY;
            float gx = (localPoint.x / HalfW + adjustedY / HalfH) * 0.5f;
            float gy = (adjustedY / HalfH - localPoint.x / HalfW) * 0.5f;
            return new Vector2Int(Mathf.RoundToInt(gx), Mathf.RoundToInt(gy));
        }

        // ====================================================================
        // P&C: Active Event Timer Banners — countdown banners for server events
        // ====================================================================

        private readonly List<GameObject> _eventBanners = new();

        /// <summary>P&C: Simulated active events for city screen display.</summary>
        private static readonly (string Name, string Icon, Color Tint, float DurationHours)[] SimulatedEvents = new[]
        {
            ("Alliance War", "\u2694", new Color(0.85f, 0.30f, 0.25f), 4f),
            ("Void Rift Surge", "\u2728", new Color(0.55f, 0.35f, 0.85f), 2.5f),
            ("Harvest Festival", "\u26CF", new Color(0.40f, 0.75f, 0.35f), 6f),
        };

        /// <summary>P&C: Event timers now accessible via Event Hub icon (slot 2 in left column).
        /// Build queue status accessible via tapping the builder count HUD pill.
        /// No permanent left-side strips — keeps city view clean like P&C.</summary>
        private void CreateEventTimerBanners()
        {
            // Cleaned up in iteration 82 — permanent strips removed.
            // Build queue info: tap BuilderCountHUD → ShowBuildQueuePanel
            // Event timers: tap EventHub circular icon → ShowEventHubPanel
            foreach (var b in _eventBanners) { if (b != null) Destroy(b); }
            _eventBanners.Clear();
        }

        // ====================================================================
        // P&C: Power Breakdown Panel — total power by category
        // ====================================================================

        private GameObject _powerBreakdownPanel;

        /// <summary>P&C: Building category to power rating mapping.</summary>
        private static readonly (string Category, string Icon, Color Tint, string[] BuildingIds)[] PowerCategories = new[]
        {
            ("Military", "\u2694", new Color(0.85f, 0.35f, 0.30f), new[] { "barracks", "training_ground", "armory" }),
            ("Defense", "\u26E8", new Color(0.40f, 0.55f, 0.85f), new[] { "wall", "watch_tower", "stronghold" }),
            ("Economy", "\u26CF", new Color(0.50f, 0.80f, 0.40f), new[] { "grain_farm", "iron_mine", "stone_quarry", "marketplace" }),
            ("Research", "\u2609", new Color(0.60f, 0.50f, 0.85f), new[] { "academy", "library", "laboratory", "observatory", "archive" }),
            ("Magic", "\u2728", new Color(0.75f, 0.45f, 0.90f), new[] { "arcane_tower", "enchanting_tower", "hero_shrine" }),
            ("Diplomacy", "\u265F", new Color(0.80f, 0.70f, 0.30f), new[] { "guild_hall", "embassy" }),
        };

        /// <summary>P&C: Show power breakdown by building category.</summary>
        private void ShowPowerBreakdownPanel()
        {
            if (_powerBreakdownPanel != null) { Destroy(_powerBreakdownPanel); _powerBreakdownPanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _powerBreakdownPanel = new GameObject("PowerBreakdownPanel");
            _powerBreakdownPanel.transform.SetParent(canvas.transform, false);

            var dimRect = _powerBreakdownPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _powerBreakdownPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _powerBreakdownPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_powerBreakdownPanel != null) { Destroy(_powerBreakdownPanel); _powerBreakdownPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_powerBreakdownPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.20f);
            panelRect.anchorMax = new Vector2(0.90f, 0.80f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.85f, 0.65f, 0.15f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Total power
            int totalPower = 0;
            foreach (var p in _placements) totalPower += GetBuildingPowerContribution(p.BuildingId, p.Tier);
            string totalStr = totalPower >= 1_000_000 ? $"{totalPower / 1_000_000f:F1}M"
                : totalPower >= 1_000 ? $"{totalPower / 1_000f:F1}K" : $"{totalPower}";

            AddInfoPanelText(panel.transform, "Title", $"\u2694 Total Power: {totalStr}", 16, FontStyle.Bold,
                new Color(0.95f, 0.75f, 0.30f),
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            // Category rows
            float rowY = 0.86f;
            float rowH = 0.11f;
            int maxCategoryPower = 1; // Avoid div by zero

            // Pre-compute category totals for bar scaling
            var catTotals = new int[PowerCategories.Length];
            for (int c = 0; c < PowerCategories.Length; c++)
            {
                int catPower = 0;
                foreach (var p in _placements)
                {
                    foreach (string bid in PowerCategories[c].BuildingIds)
                    {
                        if (p.BuildingId == bid) catPower += GetBuildingPowerContribution(bid, p.Tier);
                    }
                }
                catTotals[c] = catPower;
                if (catPower > maxCategoryPower) maxCategoryPower = catPower;
            }

            for (int c = 0; c < PowerCategories.Length; c++)
            {
                var (category, icon, tint, buildingIds) = PowerCategories[c];
                int catPower = catTotals[c];
                float barFill = (float)catPower / maxCategoryPower;

                var rowGO = new GameObject($"Cat_{c}");
                rowGO.transform.SetParent(panel.transform, false);
                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, rowY - rowH);
                rowRect.anchorMax = new Vector2(0.97f, rowY);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = rowGO.AddComponent<Image>();
                rowBg.color = new Color(tint.r * 0.12f, tint.g * 0.12f, tint.b * 0.12f, 0.50f);
                rowBg.raycastTarget = false;

                // Icon
                AddInfoPanelText(rowGO.transform, "Icon", icon, 12, FontStyle.Bold,
                    tint, new Vector2(0.01f, 0f), new Vector2(0.08f, 1f), TextAnchor.MiddleCenter);

                // Category name
                AddInfoPanelText(rowGO.transform, "Name", category, 10, FontStyle.Bold,
                    Color.white, new Vector2(0.09f, 0.50f), new Vector2(0.35f, 0.98f), TextAnchor.MiddleLeft);

                // Power value
                string catStr = catPower >= 1000 ? $"{catPower / 1000f:F1}K" : $"{catPower}";
                AddInfoPanelText(rowGO.transform, "Value", catStr, 10, FontStyle.Bold,
                    tint, new Vector2(0.75f, 0.50f), new Vector2(0.98f, 0.98f), TextAnchor.MiddleRight);

                // Percentage of total
                int pct = totalPower > 0 ? (int)((float)catPower / totalPower * 100) : 0;
                AddInfoPanelText(rowGO.transform, "Pct", $"{pct}%", 8, FontStyle.Normal,
                    new Color(0.65f, 0.63f, 0.60f),
                    new Vector2(0.75f, 0.02f), new Vector2(0.98f, 0.48f), TextAnchor.MiddleRight);

                // Power bar
                var barBg = new GameObject("BarBg");
                barBg.transform.SetParent(rowGO.transform, false);
                var barBgRect = barBg.AddComponent<RectTransform>();
                barBgRect.anchorMin = new Vector2(0.09f, 0.08f);
                barBgRect.anchorMax = new Vector2(0.73f, 0.38f);
                barBgRect.offsetMin = Vector2.zero;
                barBgRect.offsetMax = Vector2.zero;
                var barBgImg = barBg.AddComponent<Image>();
                barBgImg.color = new Color(0.10f, 0.08f, 0.15f, 0.80f);
                barBgImg.raycastTarget = false;

                var barFillGO = new GameObject("Fill");
                barFillGO.transform.SetParent(barBg.transform, false);
                var barFillRect = barFillGO.AddComponent<RectTransform>();
                barFillRect.anchorMin = Vector2.zero;
                barFillRect.anchorMax = new Vector2(Mathf.Clamp01(barFill), 1f);
                barFillRect.offsetMin = Vector2.zero;
                barFillRect.offsetMax = Vector2.zero;
                var barFillImg = barFillGO.AddComponent<Image>();
                barFillImg.color = new Color(tint.r, tint.g, tint.b, 0.75f);
                barFillImg.raycastTarget = false;

                // Building count in this category
                int buildCount = 0;
                foreach (var p in _placements)
                {
                    foreach (string bid in buildingIds) { if (p.BuildingId == bid) buildCount++; }
                }
                AddInfoPanelText(rowGO.transform, "Count", $"{buildCount} bldg", 7, FontStyle.Normal,
                    new Color(0.55f, 0.53f, 0.50f),
                    new Vector2(0.35f, 0.55f), new Vector2(0.55f, 0.95f), TextAnchor.MiddleLeft);

                rowY -= rowH + 0.005f;
            }

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.35f, 0.01f);
            closeRect.anchorMax = new Vector2(0.65f, 0.08f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.40f, 0.35f, 0.35f, 0.85f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() => { if (_powerBreakdownPanel != null) { Destroy(_powerBreakdownPanel); _powerBreakdownPanel = null; } });
            AddInfoPanelText(closeGO.transform, "Label", "Close", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var fadeCg = _powerBreakdownPanel.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            StartCoroutine(FadeInDialog(fadeCg));
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

    /// <summary>P&C: Request alliance help to reduce upgrade timer.</summary>
    public readonly struct AllianceHelpRequestEvent
    {
        public readonly string InstanceId;
        public AllianceHelpRequestEvent(string id) { InstanceId = id; }
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

    /// <summary>Simple long-press detector for UI elements. Fires OnLongPress after 0.6s hold.</summary>
    public class LongPressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action OnLongPress;
        private float _holdTimer;
        private bool _holding;
        private const float LongPressThreshold = 0.6f;

        public void OnPointerDown(PointerEventData eventData)
        {
            _holding = true;
            _holdTimer = 0f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _holding = false;
        }

        private void Update()
        {
            if (!_holding) return;
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= LongPressThreshold)
            {
                _holding = false;
                OnLongPress?.Invoke();
            }
        }
    }

    // ====================================================================
    // P&C: Day/Night Cycle — Enhanced ambient lighting with building lights
    // ====================================================================

    public partial class CityGridView
    {
        private readonly List<GameObject> _nightLightGlows = new();
        private GameObject _skyOverlay; // Stars/moon at night
        private bool _nightLightsCreated;

        /// <summary>P&C: Enhanced day/night — create window lights on buildings at night, darken terrain.</summary>
        private void UpdateDayNightCycle()
        {
            int hour = System.DateTime.Now.Hour;
            bool isNight = hour >= 20 || hour < 6;
            bool isDusk = hour >= 18 && hour < 20;
            bool isDawn = hour >= 5 && hour < 7;

            // Night lights on buildings
            if (isNight && !_nightLightsCreated)
            {
                CreateNightBuildingLights();
                CreateNightSkyOverlay();
                _nightLightsCreated = true;
            }
            else if (!isNight && _nightLightsCreated)
            {
                RemoveNightLights();
                _nightLightsCreated = false;
            }

            // Tint all building sprites based on time of day
            Color buildingTint;
            if (isNight)
                buildingTint = new Color(0.55f, 0.55f, 0.75f, 1f); // Cool blue moonlight
            else if (isDusk)
                buildingTint = new Color(0.90f, 0.70f, 0.65f, 1f); // Warm sunset
            else if (isDawn)
                buildingTint = new Color(0.92f, 0.82f, 0.70f, 1f); // Golden dawn
            else
                buildingTint = Color.white; // Full daylight

            foreach (var placement in _placements)
            {
                if (placement.VisualGO == null) continue;
                // Don't override skin tint completely — blend with it
                var img = placement.VisualGO.GetComponent<Image>();
                if (img != null && !_burningBuildings.Contains(placement.InstanceId))
                {
                    if (_buildingSkins.TryGetValue(placement.InstanceId, out int skinIdx) && skinIdx > 0)
                    {
                        var skinColor = BuildingSkinOptions[skinIdx].Tint;
                        img.color = skinColor * buildingTint;
                    }
                    else
                    {
                        img.color = buildingTint;
                    }
                }
            }
        }

        private void CreateNightBuildingLights()
        {
            foreach (var placement in _placements)
            {
                if (placement.VisualGO == null) continue;
                // Only some buildings get window lights
                bool getsLight = placement.BuildingId == "stronghold" || placement.BuildingId == "barracks" ||
                    placement.BuildingId == "embassy" || placement.BuildingId == "guild_hall" ||
                    placement.BuildingId == "academy" || placement.BuildingId == "library" ||
                    placement.BuildingId == "forge" || placement.BuildingId == "watch_tower" ||
                    placement.BuildingId == "marketplace";
                if (!getsLight) continue;

                var bRect = placement.VisualGO.GetComponent<RectTransform>();
                if (bRect == null) continue;

                // Warm glow point on building
                var glow = new GameObject($"NightLight_{placement.InstanceId}");
                glow.transform.SetParent(placement.VisualGO.transform, false);
                var glowRect = glow.AddComponent<RectTransform>();
                glowRect.anchorMin = new Vector2(0.3f, 0.4f);
                glowRect.anchorMax = new Vector2(0.7f, 0.7f);
                glowRect.offsetMin = Vector2.zero;
                glowRect.offsetMax = Vector2.zero;
                var glowImg = glow.AddComponent<Image>();
                glowImg.color = new Color(1f, 0.85f, 0.40f, 0.45f);
                glowImg.raycastTarget = false;

                // Load radial gradient if available
                var radialSprite = Resources.Load<Sprite>("UI/Production/radial_gradient");
                if (radialSprite != null) glowImg.sprite = radialSprite;

                _nightLightGlows.Add(glow);
            }
        }

        private void CreateNightSkyOverlay()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _skyOverlay = new GameObject("NightSky");
            _skyOverlay.transform.SetParent(canvas.transform, false);
            _skyOverlay.transform.SetSiblingIndex(0); // Behind everything
            var rect = _skyOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = _skyOverlay.AddComponent<Image>();
            img.color = new Color(0.02f, 0.02f, 0.08f, 0.25f);
            img.raycastTarget = false;

            // Scatter some "stars" (small white dots)
            for (int i = 0; i < 20; i++)
            {
                var star = new GameObject($"Star_{i}");
                star.transform.SetParent(_skyOverlay.transform, false);
                var sRect = star.AddComponent<RectTransform>();
                sRect.anchorMin = new Vector2(Random.Range(0.05f, 0.95f), Random.Range(0.55f, 0.95f));
                sRect.anchorMax = sRect.anchorMin;
                sRect.sizeDelta = new Vector2(Random.Range(1.5f, 3f), Random.Range(1.5f, 3f));
                var sImg = star.AddComponent<Image>();
                float brightness = Random.Range(0.6f, 1f);
                sImg.color = new Color(brightness, brightness, brightness * 0.95f, Random.Range(0.4f, 0.8f));
                sImg.raycastTarget = false;
            }
        }

        private void RemoveNightLights()
        {
            foreach (var glow in _nightLightGlows)
            {
                if (glow != null) Destroy(glow);
            }
            _nightLightGlows.Clear();
            if (_skyOverlay != null)
            {
                Destroy(_skyOverlay);
                _skyOverlay = null;
            }
        }
    }

    // ====================================================================
    // P&C: Resource Overflow / Vault Full Warnings
    // ====================================================================

    public partial class CityGridView
    {
        private readonly Dictionary<ResourceType, GameObject> _overflowWarnings = new();
        private float _overflowCheckTimer;
        private const float OverflowCheckInterval = 5f;
        private const float OverflowThreshold = 0.90f; // Warn at 90% full

        /// <summary>P&C: Check all resources against vault caps and show/hide warning badges.</summary>
        private void CheckResourceOverflow()
        {
            if (!ServiceLocator.TryGet<ResourceManager>(out var rm)) return;

            CheckSingleResourceOverflow(rm, ResourceType.Stone, rm.Stone, rm.MaxStone);
            CheckSingleResourceOverflow(rm, ResourceType.Iron, rm.Iron, rm.MaxIron);
            CheckSingleResourceOverflow(rm, ResourceType.Grain, rm.Grain, rm.MaxGrain);
            CheckSingleResourceOverflow(rm, ResourceType.ArcaneEssence, rm.ArcaneEssence, rm.MaxArcaneEssence);
        }

        private void CheckSingleResourceOverflow(ResourceManager rm, ResourceType type, long current, long max)
        {
            if (max <= 0) return;
            float ratio = (float)current / max;
            bool isOverflowing = ratio >= OverflowThreshold;

            if (isOverflowing && !_overflowWarnings.ContainsKey(type))
            {
                CreateOverflowWarning(type, ratio >= 1f);
            }
            else if (!isOverflowing && _overflowWarnings.TryGetValue(type, out var warning))
            {
                if (warning != null) Destroy(warning);
                _overflowWarnings.Remove(type);
            }
            else if (isOverflowing && _overflowWarnings.TryGetValue(type, out var existingWarning))
            {
                // Update: switch from "nearly full" to "FULL" if ratio changed
                if (existingWarning != null)
                {
                    var label = existingWarning.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        bool isFull = ratio >= 1f;
                        label.text = isFull ? $"{GetResourceName(type)} FULL!" : $"{GetResourceName(type)} {(int)(ratio * 100)}%";
                        label.color = isFull ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.3f);
                    }
                }
            }
        }

        private void CreateOverflowWarning(ResourceType type, bool isFull)
        {
            // Find the corresponding producer building to position the warning
            string producerBuilding = type switch
            {
                ResourceType.Grain => "grain_farm",
                ResourceType.Iron => "iron_mine",
                ResourceType.Stone => "stone_quarry",
                ResourceType.ArcaneEssence => "arcane_tower",
                _ => null
            };

            // Position at top of screen as a banner-style warning
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            int warningIndex = _overflowWarnings.Count;
            var warningGO = new GameObject($"OverflowWarning_{type}");
            warningGO.transform.SetParent(canvas.transform, false);
            var rect = warningGO.AddComponent<RectTransform>();
            // Stack warnings below resource bar
            float yBase = 0.88f - warningIndex * 0.035f;
            rect.anchorMin = new Vector2(0.20f, yBase);
            rect.anchorMax = new Vector2(0.80f, yBase + 0.03f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = warningGO.AddComponent<Image>();
            bg.color = isFull ? new Color(0.60f, 0.10f, 0.10f, 0.88f) : new Color(0.55f, 0.40f, 0.05f, 0.85f);
            bg.raycastTarget = true;
            var outline = warningGO.AddComponent<Outline>();
            outline.effectColor = isFull ? new Color(1f, 0.3f, 0.3f, 0.6f) : new Color(1f, 0.85f, 0.3f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Tap to dismiss
            var btn = warningGO.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() =>
            {
                if (warningGO != null) Destroy(warningGO);
                _overflowWarnings.Remove(type);
            });

            // Warning icon + text
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(warningGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = new Vector2(-8, 0);
            var text = labelGO.AddComponent<Text>();
            text.text = isFull ? $"\u26a0 {GetResourceName(type)} FULL! Tap to upgrade vault." : $"\u26a0 {GetResourceName(type)} {(int)(OverflowThreshold * 100)}%+ — vault nearly full!";
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = isFull ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.85f, 0.3f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            // Pulse animation
            StartCoroutine(PulseOverflowWarning(warningGO, isFull));

            _overflowWarnings[type] = warningGO;
        }

        private IEnumerator PulseOverflowWarning(GameObject warning, bool isFull)
        {
            while (warning != null)
            {
                var img = warning.GetComponent<Image>();
                if (img != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (isFull ? 4f : 2f));
                    float a = isFull ? 0.88f : 0.85f;
                    img.color = isFull
                        ? new Color(0.60f * pulse, 0.10f, 0.10f, a)
                        : new Color(0.55f * pulse, 0.40f * pulse, 0.05f, a);
                }
                yield return null;
            }
        }

        private static string GetResourceName(ResourceType type) => type switch
        {
            ResourceType.Stone => "Stone",
            ResourceType.Iron => "Iron",
            ResourceType.Grain => "Grain",
            ResourceType.ArcaneEssence => "Arcane Essence",
            _ => type.ToString()
        };
    }

    // ====================================================================
    // P&C: Troop Training Queue — Barracks / Training Ground
    // ====================================================================

    public partial class CityGridView
    {
        // Training queue per building instance
        private readonly Dictionary<string, List<TroopQueueEntry>> _troopQueues = new();
        private const int MaxTroopQueueSize = 5;

        private struct TroopQueueEntry
        {
            public string TroopName;
            public int Count;
            public float RemainingTime;
        }

        /// <summary>Tick all troop training queues each frame.</summary>
        private void TickTroopQueues()
        {
            var completedKeys = new List<string>();
            foreach (var kvp in _troopQueues)
            {
                var queue = kvp.Value;
                if (queue.Count == 0) continue;

                // Only the first entry ticks down
                var entry = queue[0];
                entry.RemainingTime -= Time.deltaTime;
                queue[0] = entry;

                if (entry.RemainingTime <= 0f)
                {
                    queue.RemoveAt(0);
                    Debug.Log($"[TroopTraining] {entry.TroopName} training complete at {kvp.Key}!");
                }

                if (queue.Count == 0)
                    completedKeys.Add(kvp.Key);
            }
            foreach (var key in completedKeys)
                _troopQueues.Remove(key);
        }

        /// <summary>P&C: Get active training queue count for a building (for notification badges).</summary>
        public int GetTrainingQueueCount(string instanceId)
        {
            return _troopQueues.TryGetValue(instanceId, out var q) ? q.Count : 0;
        }
    }

    // ====================================================================
    // P&C: Building Favorites / Quick-Nav Bookmarks
    // ====================================================================

    public partial class CityGridView
    {
        private readonly HashSet<string> _favoriteBuildings = new();
        private GameObject _favoritesBar;

        private void ToggleFavoriteBuilding(string instanceId)
        {
            if (_favoriteBuildings.Contains(instanceId))
            {
                _favoriteBuildings.Remove(instanceId);
                Debug.Log($"[CityGridView] Removed {instanceId} from favorites.");
            }
            else
            {
                _favoriteBuildings.Add(instanceId);
                Debug.Log($"[CityGridView] Added {instanceId} to favorites.");
            }
            RefreshFavoritesBar();
            // Refresh bookmark star badge on building
            var placement = _placements.Find(p => p.InstanceId == instanceId);
            if (placement?.VisualGO != null)
            {
                var existingStar = placement.VisualGO.transform.Find("FavStar");
                if (existingStar != null) Destroy(existingStar.gameObject);
                if (_favoriteBuildings.Contains(instanceId))
                    AddFavStarBadge(placement);
            }
        }

        private void AddFavStarBadge(CityBuildingPlacement placement)
        {
            if (placement.VisualGO == null) return;
            var star = new GameObject("FavStar");
            star.transform.SetParent(placement.VisualGO.transform, false);
            var rect = star.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.75f, 0.80f);
            rect.anchorMax = new Vector2(0.95f, 0.95f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = star.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            img.raycastTarget = false;
            var text = new GameObject("StarIcon");
            text.transform.SetParent(star.transform, false);
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var label = text.AddComponent<Text>();
            label.text = "\u2605";
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.15f, 0.10f, 0.02f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;
        }

        private void RefreshFavoritesBar()
        {
            if (_favoritesBar != null) Destroy(_favoritesBar);
            if (_favoriteBuildings.Count == 0) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _favoritesBar = new GameObject("FavoritesBar");
            _favoritesBar.transform.SetParent(canvas.transform, false);
            var barRect = _favoritesBar.AddComponent<RectTransform>();
            // Right side, below resource bar
            barRect.anchorMin = new Vector2(0.82f, 0.60f);
            barRect.anchorMax = new Vector2(0.98f, 0.60f + _favoriteBuildings.Count * 0.05f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;
            var barBg = _favoritesBar.AddComponent<Image>();
            barBg.color = new Color(0.06f, 0.05f, 0.12f, 0.75f);
            barBg.raycastTarget = false;
            var barOutline = _favoritesBar.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.4f);
            barOutline.effectDistance = new Vector2(0.8f, -0.8f);

            int idx = 0;
            foreach (var instId in _favoriteBuildings)
            {
                var placement = _placements.Find(p => p.InstanceId == instId);
                if (placement == null) continue;

                float slotH = 1f / _favoriteBuildings.Count;
                var slot = new GameObject($"FavSlot_{idx}");
                slot.transform.SetParent(_favoritesBar.transform, false);
                var slotRect = slot.AddComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0f, 1f - (idx + 1) * slotH);
                slotRect.anchorMax = new Vector2(1f, 1f - idx * slotH);
                slotRect.offsetMin = new Vector2(2, 1);
                slotRect.offsetMax = new Vector2(-2, -1);
                var slotBg = slot.AddComponent<Image>();
                slotBg.color = new Color(0.12f, 0.10f, 0.20f, 0.8f);
                slotBg.raycastTarget = true;

                // Tap to pan to building
                var btn = slot.AddComponent<Button>();
                btn.targetGraphic = slotBg;
                string capId = instId;
                btn.onClick.AddListener(() => PanToBuilding(capId));

                string displayName = BuildingDisplayNames.TryGetValue(placement.BuildingId, out var dn) ? dn : placement.BuildingId;
                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(slot.transform, false);
                var labelRect = labelGO.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(2, 0);
                labelRect.offsetMax = new Vector2(-2, 0);
                var label = labelGO.AddComponent<Text>();
                label.text = $"\u2605 {displayName}";
                label.fontSize = 9;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = new Color(0.95f, 0.85f, 0.40f);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.raycastTarget = false;

                idx++;
            }
        }

        private void PanToBuilding(string instanceId)
        {
            var placement = _placements.Find(p => p.InstanceId == instanceId);
            if (placement == null || contentContainer == null) return;

            var targetPos = GridToLocalCenter(placement.GridOrigin, placement.Size);
            // Scroll so building is centered in viewport
            var viewport = GetComponent<RectTransform>();
            if (viewport == null) return;
            var scale = contentContainer.localScale.x;
            contentContainer.anchoredPosition = -targetPos * scale;
        }
    }

    // ====================================================================
    // P&C: Wall Repair Mechanic After Raids
    // ====================================================================

    public partial class CityGridView
    {
        private readonly Dictionary<string, float> _damagedWalls = new(); // instanceId → damage %
        private GameObject _wallRepairPanel;

        /// <summary>P&C: Damage a wall section during raids.</summary>
        private void DamageWall(string instanceId, float damagePercent)
        {
            if (!_damagedWalls.ContainsKey(instanceId))
                _damagedWalls[instanceId] = 0f;
            _damagedWalls[instanceId] = Mathf.Min(1f, _damagedWalls[instanceId] + damagePercent);
            RefreshWallDamageVisual(instanceId);
        }

        private void RefreshWallDamageVisual(string instanceId)
        {
            var placement = _placements.Find(p => p.InstanceId == instanceId);
            if (placement?.VisualGO == null) return;

            // Remove old overlay
            var existing = placement.VisualGO.transform.Find("WallDamage");
            if (existing != null) Destroy(existing.gameObject);

            if (!_damagedWalls.TryGetValue(instanceId, out float dmg) || dmg <= 0f) return;

            // Damage overlay: cracks + red tint based on damage level
            var overlay = new GameObject("WallDamage");
            overlay.transform.SetParent(placement.VisualGO.transform, false);
            var rect = overlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = overlay.AddComponent<Image>();
            float r = Mathf.Lerp(0.3f, 0.7f, dmg);
            img.color = new Color(r, 0.10f, 0.05f, dmg * 0.4f);
            img.raycastTarget = false;

            // Crack text indicators
            var crackGO = new GameObject("Cracks");
            crackGO.transform.SetParent(overlay.transform, false);
            var crackRect = crackGO.AddComponent<RectTransform>();
            crackRect.anchorMin = new Vector2(0.2f, 0.3f);
            crackRect.anchorMax = new Vector2(0.8f, 0.7f);
            crackRect.offsetMin = Vector2.zero;
            crackRect.offsetMax = Vector2.zero;
            var crackText = crackGO.AddComponent<Text>();
            int crackCount = dmg > 0.6f ? 3 : (dmg > 0.3f ? 2 : 1);
            crackText.text = new string('\u2740', crackCount); // ❀ as crack symbol
            crackText.fontSize = 16;
            crackText.alignment = TextAnchor.MiddleCenter;
            crackText.color = new Color(0.8f, 0.2f, 0.1f, 0.7f);
            crackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            crackText.raycastTarget = false;

            // HP bar under wall
            var hpBar = new GameObject("HPBar");
            hpBar.transform.SetParent(placement.VisualGO.transform, false);
            var hpRect = hpBar.AddComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0.10f, -0.02f);
            hpRect.anchorMax = new Vector2(0.90f, 0.03f);
            hpRect.offsetMin = Vector2.zero;
            hpRect.offsetMax = Vector2.zero;
            var hpBg = hpBar.AddComponent<Image>();
            hpBg.color = new Color(0.2f, 0.05f, 0.05f, 0.8f);
            hpBg.raycastTarget = false;

            var hpFill = new GameObject("Fill");
            hpFill.transform.SetParent(hpBar.transform, false);
            var fillRect = hpFill.AddComponent<RectTransform>();
            float hpPercent = 1f - dmg;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(hpPercent, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = hpFill.AddComponent<Image>();
            fillImg.color = hpPercent > 0.5f ? new Color(0.15f, 0.65f, 0.20f, 0.9f) : new Color(0.75f, 0.25f, 0.10f, 0.9f);
            fillImg.raycastTarget = false;
        }

        /// <summary>P&C: Show wall repair panel with resource cost and timer.</summary>
        private void ShowWallRepairPanel(string instanceId)
        {
            if (_wallRepairPanel != null) { Destroy(_wallRepairPanel); _wallRepairPanel = null; }
            if (!_damagedWalls.TryGetValue(instanceId, out float dmg) || dmg <= 0f)
            {
                ShowUpgradeBlockedToast("Wall is not damaged.");
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _wallRepairPanel = new GameObject("WallRepairPanel");
            _wallRepairPanel.transform.SetParent(canvas.transform, false);
            var dimRect = _wallRepairPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _wallRepairPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _wallRepairPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_wallRepairPanel != null) { Destroy(_wallRepairPanel); _wallRepairPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_wallRepairPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.12f, 0.30f);
            panelRect.anchorMax = new Vector2(0.88f, 0.70f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            AddInfoPanelText(panel.transform, "Title", $"\u26E8 Wall Repair", 15, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f), new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f), TextAnchor.MiddleCenter);

            // Damage status
            int dmgPct = (int)(dmg * 100);
            int hpPct = 100 - dmgPct;
            AddInfoPanelText(panel.transform, "DmgStatus", $"Wall Integrity: {hpPct}%  (Damage: {dmgPct}%)", 12, FontStyle.Normal,
                hpPct > 50 ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.4f, 0.3f),
                new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.80f), TextAnchor.MiddleCenter);

            // Cost
            int stoneCost = (int)(dmg * 500);
            int ironCost = (int)(dmg * 300);
            int repairTime = (int)(dmg * 120); // seconds
            AddInfoPanelText(panel.transform, "Cost", $"Repair Cost:  Stone: {stoneCost}  Iron: {ironCost}  Time: {FormatTimeRemaining(repairTime)}", 11, FontStyle.Normal,
                new Color(0.75f, 0.75f, 0.75f), new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.66f), TextAnchor.MiddleCenter);

            // Repair button
            var repairGO = new GameObject("RepairBtn");
            repairGO.transform.SetParent(panel.transform, false);
            var repairRect = repairGO.AddComponent<RectTransform>();
            repairRect.anchorMin = new Vector2(0.15f, 0.25f);
            repairRect.anchorMax = new Vector2(0.50f, 0.45f);
            repairRect.offsetMin = Vector2.zero;
            repairRect.offsetMax = Vector2.zero;
            var repairBg = repairGO.AddComponent<Image>();
            repairBg.color = new Color(0.15f, 0.55f, 0.25f, 0.92f);
            var repairBtn = repairGO.AddComponent<Button>();
            repairBtn.targetGraphic = repairBg;
            string capRepairId = instanceId;
            repairBtn.onClick.AddListener(() =>
            {
                _damagedWalls.Remove(capRepairId);
                RefreshWallDamageVisual(capRepairId);
                if (_wallRepairPanel != null) { Destroy(_wallRepairPanel); _wallRepairPanel = null; }
                ShowUpgradeBlockedToast("Wall repaired!");
            });
            AddInfoPanelText(repairGO.transform, "Label", "\u2692 REPAIR", 12, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Instant repair (gems)
            int gemCost = Mathf.Max(5, (int)(dmg * 50));
            var instantGO = new GameObject("InstantBtn");
            instantGO.transform.SetParent(panel.transform, false);
            var instantRect = instantGO.AddComponent<RectTransform>();
            instantRect.anchorMin = new Vector2(0.55f, 0.25f);
            instantRect.anchorMax = new Vector2(0.85f, 0.45f);
            instantRect.offsetMin = Vector2.zero;
            instantRect.offsetMax = Vector2.zero;
            var instantBg = instantGO.AddComponent<Image>();
            instantBg.color = new Color(0.55f, 0.35f, 0.65f, 0.92f);
            var instantBtn = instantGO.AddComponent<Button>();
            instantBtn.targetGraphic = instantBg;
            instantBtn.onClick.AddListener(() =>
            {
                _damagedWalls.Remove(capRepairId);
                RefreshWallDamageVisual(capRepairId);
                if (_wallRepairPanel != null) { Destroy(_wallRepairPanel); _wallRepairPanel = null; }
                ShowUpgradeBlockedToast($"Wall instantly repaired! (-{gemCost} gems)");
            });
            AddInfoPanelText(instantGO.transform, "Label", $"\u2728 {gemCost} Gems", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.86f);
            closeRect.anchorMax = new Vector2(0.97f, 0.96f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_wallRepairPanel != null) { Destroy(_wallRepairPanel); _wallRepairPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _wallRepairPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }
    }

    // ====================================================================
    // P&C: Auto-Collect Toggle for Resource Bubbles
    // ====================================================================

    public partial class CityGridView
    {
        private bool _autoCollectEnabled;
        private float _autoCollectTimer;
        private const float AutoCollectInterval = 25f; // slightly longer than spawn interval
        private GameObject _autoCollectToggle;

        /// <summary>P&C: Create auto-collect toggle button near collect-all area.</summary>
        private void CreateAutoCollectToggle()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _autoCollectToggle = new GameObject("AutoCollectToggle");
            _autoCollectToggle.transform.SetParent(canvas.transform, false);
            var rect = _autoCollectToggle.AddComponent<RectTransform>();
            // P&C: Small pill above nav bar, right-aligned — subtle, not wide
            rect.anchorMin = new Vector2(0.72f, 0.105f);
            rect.anchorMax = new Vector2(0.92f, 0.135f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var bg = _autoCollectToggle.AddComponent<Image>();
            bg.color = _autoCollectEnabled ? new Color(0.15f, 0.50f, 0.25f, 0.85f) : new Color(0.25f, 0.20f, 0.30f, 0.75f);
            bg.raycastTarget = true;
            var outline = _autoCollectToggle.AddComponent<Outline>();
            outline.effectColor = new Color(0.70f, 0.55f, 0.20f, 0.4f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            var btn = _autoCollectToggle.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(ToggleAutoCollect);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_autoCollectToggle.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4, 0);
            labelRect.offsetMax = new Vector2(-4, 0);
            var label = labelGO.AddComponent<Text>();
            label.text = _autoCollectEnabled ? "\u2714 AUTO-COLLECT ON" : "\u2610 AUTO-COLLECT OFF";
            label.fontSize = 10;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = _autoCollectEnabled ? Color.white : new Color(0.65f, 0.65f, 0.65f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;
        }

        private void ToggleAutoCollect()
        {
            _autoCollectEnabled = !_autoCollectEnabled;
            Debug.Log($"[CityGridView] Auto-collect: {(_autoCollectEnabled ? "ON" : "OFF")}");

            // Refresh toggle visual
            if (_autoCollectToggle != null)
            {
                var bg = _autoCollectToggle.GetComponent<Image>();
                if (bg != null)
                    bg.color = _autoCollectEnabled ? new Color(0.15f, 0.50f, 0.25f, 0.85f) : new Color(0.25f, 0.20f, 0.30f, 0.75f);
                var label = _autoCollectToggle.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = _autoCollectEnabled ? "\u2714 AUTO-COLLECT ON" : "\u2610 AUTO-COLLECT OFF";
                    label.color = _autoCollectEnabled ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                }
            }
        }

        /// <summary>P&C: Auto-collect all resource bubbles when enabled.</summary>
        private void TickAutoCollect()
        {
            if (!_autoCollectEnabled) return;
            _autoCollectTimer += Time.deltaTime;
            if (_autoCollectTimer < AutoCollectInterval) return;
            _autoCollectTimer = 0f;

            var spawner = FindAnyObjectByType<ResourceBubbleSpawner>();
            if (spawner == null || spawner.GetTotalActiveBubbleCount() == 0) return;

            spawner.CollectAll();
            Debug.Log("[CityGridView] Auto-collected all resource bubbles.");
        }
    }

    // ====================================================================
    // P&C: Batch Upgrade Mode — select multiple buildings to queue upgrades
    // ====================================================================

    public partial class CityGridView
    {
        private bool _batchUpgradeMode;
        private readonly List<string> _batchUpgradeSelection = new();
        private GameObject _batchUpgradeHUD;

        /// <summary>P&C: Toggle batch upgrade mode — tap buildings to select, confirm to upgrade all.</summary>
        private void ToggleBatchUpgradeMode()
        {
            _batchUpgradeMode = !_batchUpgradeMode;
            if (_batchUpgradeMode)
            {
                _batchUpgradeSelection.Clear();
                CreateBatchUpgradeHUD();
            }
            else
            {
                _batchUpgradeSelection.Clear();
                ClearBatchSelectionVisuals();
                if (_batchUpgradeHUD != null) { Destroy(_batchUpgradeHUD); _batchUpgradeHUD = null; }
            }
        }

        private void CreateBatchUpgradeHUD()
        {
            if (_batchUpgradeHUD != null) Destroy(_batchUpgradeHUD);
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _batchUpgradeHUD = new GameObject("BatchUpgradeHUD");
            _batchUpgradeHUD.transform.SetParent(canvas.transform, false);
            _batchUpgradeHUD.transform.SetAsLastSibling();

            // Top banner
            var banner = new GameObject("Banner");
            banner.transform.SetParent(_batchUpgradeHUD.transform, false);
            var bannerRect = banner.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.10f, 0.82f);
            bannerRect.anchorMax = new Vector2(0.90f, 0.88f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;
            var bannerBg = banner.AddComponent<Image>();
            bannerBg.color = new Color(0.15f, 0.40f, 0.60f, 0.92f);
            var bannerOutline = banner.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.40f, 0.70f, 0.95f, 0.6f);
            bannerOutline.effectDistance = new Vector2(1f, -1f);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(banner.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = new Vector2(-8, 0);
            var label = labelGO.AddComponent<Text>();
            label.text = "\u2191 BATCH UPGRADE — Tap buildings to select (0 selected)";
            label.fontSize = 11;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;

            // Confirm button
            var confirmGO = new GameObject("ConfirmBtn");
            confirmGO.transform.SetParent(_batchUpgradeHUD.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.25f, 0.75f);
            confirmRect.anchorMax = new Vector2(0.52f, 0.81f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = new Color(0.15f, 0.55f, 0.25f, 0.92f);
            var confirmBtn = confirmGO.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmBg;
            confirmBtn.onClick.AddListener(ExecuteBatchUpgrade);
            AddInfoPanelText(confirmGO.transform, "Label", "\u2714 UPGRADE ALL", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(_batchUpgradeHUD.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.55f, 0.75f);
            cancelRect.anchorMax = new Vector2(0.75f, 0.81f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelBg = cancelGO.AddComponent<Image>();
            cancelBg.color = new Color(0.55f, 0.20f, 0.15f, 0.92f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelBg;
            cancelBtn.onClick.AddListener(ToggleBatchUpgradeMode);
            AddInfoPanelText(cancelGO.transform, "Label", "\u2716 CANCEL", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }

        /// <summary>P&C: Select/deselect a building for batch upgrade during batch mode.</summary>
        private void BatchSelectBuilding(string instanceId)
        {
            if (!_batchUpgradeMode) return;

            if (_batchUpgradeSelection.Contains(instanceId))
            {
                _batchUpgradeSelection.Remove(instanceId);
            }
            else
            {
                if (_batchUpgradeSelection.Count >= 10)
                {
                    ShowUpgradeBlockedToast("Max 10 buildings per batch.");
                    return;
                }
                _batchUpgradeSelection.Add(instanceId);
            }

            // Update selection visuals
            RefreshBatchSelectionVisuals();

            // Update HUD count
            if (_batchUpgradeHUD != null)
            {
                var label = _batchUpgradeHUD.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"\u2191 BATCH UPGRADE — Tap buildings to select ({_batchUpgradeSelection.Count} selected)";
            }
        }

        private void RefreshBatchSelectionVisuals()
        {
            // Clear old selection markers
            ClearBatchSelectionVisuals();

            foreach (var instId in _batchUpgradeSelection)
            {
                var placement = _placements.Find(p => p.InstanceId == instId);
                if (placement?.VisualGO == null) continue;

                var marker = new GameObject("BatchSelect");
                marker.transform.SetParent(placement.VisualGO.transform, false);
                var rect = marker.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-3, -3);
                rect.offsetMax = new Vector2(3, 3);
                var img = marker.AddComponent<Image>();
                img.color = new Color(0.20f, 0.60f, 0.90f, 0.30f);
                img.raycastTarget = false;
                var outline = marker.AddComponent<Outline>();
                outline.effectColor = new Color(0.30f, 0.70f, 1f, 0.7f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                // Checkmark
                var checkGO = new GameObject("Check");
                checkGO.transform.SetParent(marker.transform, false);
                var checkRect = checkGO.AddComponent<RectTransform>();
                checkRect.anchorMin = new Vector2(0.65f, 0.70f);
                checkRect.anchorMax = new Vector2(0.95f, 0.95f);
                checkRect.offsetMin = Vector2.zero;
                checkRect.offsetMax = Vector2.zero;
                var checkBg = checkGO.AddComponent<Image>();
                checkBg.color = new Color(0.15f, 0.55f, 0.25f, 0.9f);
                checkBg.raycastTarget = false;
                AddInfoPanelText(checkGO.transform, "Icon", "\u2714", 12, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
        }

        private void ClearBatchSelectionVisuals()
        {
            foreach (var placement in _placements)
            {
                if (placement.VisualGO == null) continue;
                var marker = placement.VisualGO.transform.Find("BatchSelect");
                if (marker != null) Destroy(marker.gameObject);
            }
        }

        private void ExecuteBatchUpgrade()
        {
            if (_batchUpgradeSelection.Count == 0)
            {
                ShowUpgradeBlockedToast("No buildings selected!");
                return;
            }

            ServiceLocator.TryGet<BuildingManager>(out var bm);
            int queued = 0;
            foreach (var instId in _batchUpgradeSelection)
            {
                if (bm != null)
                {
                    bm.StartUpgrade(instId);
                    queued++;
                }
            }
            ShowUpgradeBlockedToast($"\u2191 Queued {queued} upgrades!");
            ToggleBatchUpgradeMode();
        }
    }

    // ====================================================================
    // P&C: Resource Trading Panel — Marketplace Building
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _tradePanel;

        private static readonly TradeOffer[] TradeOffers = new[]
        {
            new TradeOffer("Stone → Iron", ResourceType.Stone, 200, ResourceType.Iron, 150, 1),
            new TradeOffer("Stone → Grain", ResourceType.Stone, 150, ResourceType.Grain, 200, 1),
            new TradeOffer("Iron → Stone", ResourceType.Iron, 150, ResourceType.Stone, 200, 1),
            new TradeOffer("Iron → Arcane", ResourceType.Iron, 300, ResourceType.ArcaneEssence, 100, 2),
            new TradeOffer("Grain → Stone", ResourceType.Grain, 200, ResourceType.Stone, 150, 1),
            new TradeOffer("Grain → Iron", ResourceType.Grain, 250, ResourceType.Iron, 100, 2),
        };

        private struct TradeOffer
        {
            public string Name;
            public ResourceType FromType;
            public int FromAmount;
            public ResourceType ToType;
            public int ToAmount;
            public int MarketTier; // min marketplace tier required

            public TradeOffer(string name, ResourceType from, int fromAmt, ResourceType to, int toAmt, int tier)
            {
                Name = name; FromType = from; FromAmount = fromAmt;
                ToType = to; ToAmount = toAmt; MarketTier = tier;
            }
        }

        /// <summary>P&C: Show resource trading panel for marketplace building.</summary>
        private void ShowTradePanel(int marketplaceTier)
        {
            if (_tradePanel != null) { Destroy(_tradePanel); _tradePanel = null; }
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _tradePanel = new GameObject("TradePanel");
            _tradePanel.transform.SetParent(canvas.transform, false);
            var dimRect = _tradePanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _tradePanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _tradePanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_tradePanel != null) { Destroy(_tradePanel); _tradePanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_tradePanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.18f);
            panelRect.anchorMax = new Vector2(0.94f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            AddInfoPanelText(panel.transform, "Title", "\u2696 Marketplace — Resource Trading", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f), new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            // Trade offer rows
            ServiceLocator.TryGet<ResourceManager>(out var rm);
            float rowY = 0.85f;
            float rowH = 0.11f;

            foreach (var offer in TradeOffers)
            {
                float rowTop = rowY;
                float rowBot = rowY - rowH;
                bool locked = offer.MarketTier > marketplaceTier;
                bool canAfford = rm != null && GetResourceAmount(rm, offer.FromType) >= offer.FromAmount;

                var row = new GameObject($"Trade_{offer.Name}");
                row.transform.SetParent(panel.transform, false);
                var rowRect = row.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, rowBot);
                rowRect.anchorMax = new Vector2(0.97f, rowTop);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = row.AddComponent<Image>();
                rowBg.color = locked ? new Color(0.10f, 0.08f, 0.15f, 0.5f) : new Color(0.12f, 0.10f, 0.20f, 0.7f);

                // Trade description
                string desc = locked ? $"\u26BF {offer.Name} (Tier {offer.MarketTier} required)" :
                    $"{offer.Name}: {offer.FromAmount} → {offer.ToAmount}";
                AddInfoPanelText(row.transform, "Desc", desc, 11, locked ? FontStyle.Italic : FontStyle.Normal,
                    locked ? new Color(0.5f, 0.5f, 0.5f) : Color.white,
                    new Vector2(0.02f, 0f), new Vector2(0.65f, 1f), TextAnchor.MiddleLeft);

                if (!locked)
                {
                    // Trade button
                    var tradeBtnGO = new GameObject("TradeBtn");
                    tradeBtnGO.transform.SetParent(row.transform, false);
                    var tradeBtnRect = tradeBtnGO.AddComponent<RectTransform>();
                    tradeBtnRect.anchorMin = new Vector2(0.68f, 0.12f);
                    tradeBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                    tradeBtnRect.offsetMin = Vector2.zero;
                    tradeBtnRect.offsetMax = Vector2.zero;
                    var tradeBtnBg = tradeBtnGO.AddComponent<Image>();
                    tradeBtnBg.color = canAfford ? new Color(0.18f, 0.50f, 0.30f, 0.92f) : new Color(0.35f, 0.25f, 0.25f, 0.7f);
                    var tradeBtn = tradeBtnGO.AddComponent<Button>();
                    tradeBtn.targetGraphic = tradeBtnBg;
                    tradeBtn.interactable = canAfford;
                    var capOffer = offer;
                    int capTier = marketplaceTier;
                    tradeBtn.onClick.AddListener(() => ExecuteTrade(capOffer, capTier));
                    AddInfoPanelText(tradeBtnGO.transform, "Label", canAfford ? "TRADE" : "LOW", 11, FontStyle.Bold,
                        canAfford ? Color.white : new Color(0.6f, 0.4f, 0.4f),
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                }

                rowY -= rowH + 0.012f;
            }

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.91f);
            closeRect.anchorMax = new Vector2(0.97f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_tradePanel != null) { Destroy(_tradePanel); _tradePanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _tradePanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void ExecuteTrade(TradeOffer offer, int marketTier)
        {
            var rm = ServiceLocator.Get<ResourceManager>();
            if (rm == null) return;

            long current = GetResourceAmount(rm, offer.FromType);
            if (current < offer.FromAmount)
            {
                ShowUpgradeBlockedToast("Not enough resources!");
                return;
            }

            // Spend source, add target
            SpendSingleResource(rm, offer.FromType, offer.FromAmount);
            rm.AddResource(offer.ToType, offer.ToAmount);
            Debug.Log($"[Trade] Exchanged {offer.FromAmount} {offer.FromType} for {offer.ToAmount} {offer.ToType}");

            // Refresh panel
            if (_tradePanel != null) { Destroy(_tradePanel); _tradePanel = null; }
            ShowTradePanel(marketTier);
        }

        private static long GetResourceAmount(ResourceManager rm, ResourceType type) => type switch
        {
            ResourceType.Stone => rm.Stone,
            ResourceType.Iron => rm.Iron,
            ResourceType.Grain => rm.Grain,
            ResourceType.ArcaneEssence => rm.ArcaneEssence,
            _ => 0
        };

        private static void SpendSingleResource(ResourceManager rm, ResourceType type, int amount)
        {
            // Use Spend with zeros for other resources
            switch (type)
            {
                case ResourceType.Stone: rm.Spend(amount, 0, 0, 0); break;
                case ResourceType.Iron: rm.Spend(0, amount, 0, 0); break;
                case ResourceType.Grain: rm.Spend(0, 0, amount, 0); break;
                case ResourceType.ArcaneEssence: rm.Spend(0, 0, 0, amount); break;
            }
        }
    }

    // ====================================================================
    // P&C: Build Queue Priority — Reorder entries by tapping up/down
    // ====================================================================

    public partial class CityGridView
    {
        /// <summary>P&C: Enhanced build queue panel with reorder arrows and priority management.</summary>
        private void ShowEnhancedBuildQueuePanel()
        {
            // Reuse existing queue panel infrastructure but add priority controls
            ServiceLocator.TryGet<BuildingManager>(out var bm);
            if (bm == null) return;

            if (_buildQueuePanel != null) { Destroy(_buildQueuePanel); _buildQueuePanel = null; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _buildQueuePanel = new GameObject("BuildQueuePanel");
            _buildQueuePanel.transform.SetParent(canvas.transform, false);
            _buildQueuePanel.transform.SetAsLastSibling();

            var panelRect = _buildQueuePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.22f);
            panelRect.anchorMax = new Vector2(0.95f, 0.78f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = _buildQueuePanel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            var panelOutline = _buildQueuePanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            AddInfoPanelText(_buildQueuePanel.transform, "Title", "\u2692 Build Queue — Priority Manager", 14, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f), new Vector2(0.05f, 0.88f), new Vector2(0.85f, 0.98f), TextAnchor.MiddleCenter);

            var queue = bm.BuildQueue;
            if (queue == null || queue.Count == 0)
            {
                AddInfoPanelText(_buildQueuePanel.transform, "Empty", "No active builds. Start upgrading buildings!",
                    12, FontStyle.Italic, new Color(0.6f, 0.6f, 0.6f),
                    new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.60f), TextAnchor.MiddleCenter);
            }
            else
            {
                float rowH = Mathf.Min(0.15f, 0.80f / queue.Count);
                for (int i = 0; i < queue.Count; i++)
                {
                    var entry = queue[i];
                    float rowTop = 0.85f - i * (rowH + 0.01f);
                    float rowBot = rowTop - rowH;

                    var row = new GameObject($"QueueRow_{i}");
                    row.transform.SetParent(_buildQueuePanel.transform, false);
                    var rowRect = row.AddComponent<RectTransform>();
                    rowRect.anchorMin = new Vector2(0.03f, rowBot);
                    rowRect.anchorMax = new Vector2(0.97f, rowTop);
                    rowRect.offsetMin = Vector2.zero;
                    rowRect.offsetMax = Vector2.zero;
                    var rowBg = row.AddComponent<Image>();
                    rowBg.color = i == 0 ? new Color(0.15f, 0.25f, 0.15f, 0.8f) : new Color(0.12f, 0.10f, 0.20f, 0.7f);

                    // Priority number
                    AddInfoPanelText(row.transform, "Priority", $"#{i + 1}", 14, FontStyle.Bold,
                        i == 0 ? new Color(0.40f, 0.85f, 0.40f) : new Color(0.65f, 0.55f, 0.35f),
                        new Vector2(0.01f, 0f), new Vector2(0.10f, 1f), TextAnchor.MiddleCenter);

                    // Building name + tier
                    string buildingName = entry.PlacedId;
                    // Try to find display name from placement
                    var placement = _placements.Find(p => p.InstanceId == entry.PlacedId);
                    if (placement != null && BuildingDisplayNames.TryGetValue(placement.BuildingId, out var dn))
                        buildingName = dn;
                    AddInfoPanelText(row.transform, "Name", $"{buildingName} \u2192 Tier {entry.TargetTier}", 11, FontStyle.Bold,
                        Color.white, new Vector2(0.11f, 0.1f), new Vector2(0.60f, 0.9f), TextAnchor.MiddleLeft);

                    // Time remaining
                    string timeStr = i == 0 ? FormatTimeRemaining((int)entry.RemainingSeconds) : "Queued";
                    Color timeColor = i == 0 ? new Color(0.40f, 0.85f, 0.40f) : new Color(0.6f, 0.6f, 0.6f);
                    AddInfoPanelText(row.transform, "Time", timeStr, 10, FontStyle.Normal,
                        timeColor, new Vector2(0.60f, 0f), new Vector2(0.80f, 1f), TextAnchor.MiddleCenter);

                    // Cancel button for each entry
                    var cancelGO = new GameObject("CancelBtn");
                    cancelGO.transform.SetParent(row.transform, false);
                    var cancelRect = cancelGO.AddComponent<RectTransform>();
                    cancelRect.anchorMin = new Vector2(0.82f, 0.15f);
                    cancelRect.anchorMax = new Vector2(0.98f, 0.85f);
                    cancelRect.offsetMin = Vector2.zero;
                    cancelRect.offsetMax = Vector2.zero;
                    var cancelBg = cancelGO.AddComponent<Image>();
                    cancelBg.color = new Color(0.55f, 0.18f, 0.15f, 0.85f);
                    var cancelBtn = cancelGO.AddComponent<Button>();
                    cancelBtn.targetGraphic = cancelBg;
                    string capPlacedId = entry.PlacedId;
                    cancelBtn.onClick.AddListener(() =>
                    {
                        if (bm != null) bm.CancelUpgrade(capPlacedId);
                        ShowEnhancedBuildQueuePanel(); // Refresh
                    });
                    AddInfoPanelText(cancelGO.transform, "X", "\u2716", 11, FontStyle.Bold, Color.white,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                }
            }

            // Batch upgrade mode button
            var batchGO = new GameObject("BatchBtn");
            batchGO.transform.SetParent(_buildQueuePanel.transform, false);
            var batchRect = batchGO.AddComponent<RectTransform>();
            batchRect.anchorMin = new Vector2(0.10f, 0.02f);
            batchRect.anchorMax = new Vector2(0.50f, 0.10f);
            batchRect.offsetMin = Vector2.zero;
            batchRect.offsetMax = Vector2.zero;
            var batchBg = batchGO.AddComponent<Image>();
            batchBg.color = new Color(0.15f, 0.40f, 0.60f, 0.90f);
            var batchBtn = batchGO.AddComponent<Button>();
            batchBtn.targetGraphic = batchBg;
            batchBtn.onClick.AddListener(() =>
            {
                if (_buildQueuePanel != null) { Destroy(_buildQueuePanel); _buildQueuePanel = null; }
                ToggleBatchUpgradeMode();
            });
            AddInfoPanelText(batchGO.transform, "Label", "\u2191 BATCH UPGRADE", 11, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(_buildQueuePanel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.90f);
            closeRect.anchorMax = new Vector2(0.97f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_buildQueuePanel != null) { Destroy(_buildQueuePanel); _buildQueuePanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }
    }

    // ====================================================================
    // P&C: Building Adjacency Bonuses — nearby buildings grant synergy
    // ====================================================================

    public partial class CityGridView
    {
        private static readonly Dictionary<string, (string[] neighbors, string bonus, int percent)> AdjacencyRules = new()
        {
            { "grain_farm",     (new[] { "grain_farm" },        "Food Production",    10) },
            { "iron_mine",      (new[] { "forge", "armory" },   "Smelting Efficiency", 8) },
            { "stone_quarry",   (new[] { "wall", "stronghold" },"Quarry Output",      12) },
            { "arcane_tower",   (new[] { "library", "laboratory", "observatory" }, "Arcane Synergy", 15) },
            { "barracks",       (new[] { "training_ground", "armory" }, "Military Readiness", 10) },
            { "forge",          (new[] { "iron_mine", "armory" }, "Crafting Speed",     8) },
            { "academy",        (new[] { "library", "observatory" }, "Research Speed",  10) },
            { "marketplace",    (new[] { "grain_farm", "stone_quarry", "iron_mine" }, "Trade Volume", 12) },
            { "watch_tower",    (new[] { "wall", "barracks" },  "Scouting Range",      8) },
        };

        /// <summary>P&C: Calculate adjacency bonus % for a building based on its grid neighbors.</summary>
        private int GetAdjacencyBonus(CityBuildingPlacement placement)
        {
            if (!AdjacencyRules.TryGetValue(placement.BuildingId, out var rule)) return 0;

            int totalBonus = 0;
            int adjacentCount = 0;
            // Check all cells within range 3 of the building's origin
            int range = 3;
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var checkPos = new Vector2Int(placement.GridOrigin.x + dx, placement.GridOrigin.y + dy);
                    if (!_occupancy.TryGetValue(checkPos, out var neighborInstId)) continue;
                    if (neighborInstId == placement.InstanceId) continue; // Skip self

                    var neighbor = _placements.Find(p => p.InstanceId == neighborInstId);
                    if (neighbor == null) continue;

                    foreach (var validNeighbor in rule.neighbors)
                    {
                        if (neighbor.BuildingId == validNeighbor && adjacentCount < 3) // Cap at 3 bonuses
                        {
                            totalBonus += rule.percent;
                            adjacentCount++;
                            break;
                        }
                    }
                }
            }
            return totalBonus;
        }

        /// <summary>P&C: Get adjacency bonus description for display in info panel.</summary>
        private string GetAdjacencyBonusText(CityBuildingPlacement placement)
        {
            if (!AdjacencyRules.TryGetValue(placement.BuildingId, out var rule)) return null;
            int bonus = GetAdjacencyBonus(placement);
            if (bonus <= 0)
                return $"Adjacency: Place near {string.Join("/", rule.neighbors)} for +{rule.percent}% {rule.bonus}";
            return $"Adjacency Bonus: +{bonus}% {rule.bonus} (from nearby buildings)";
        }

        /// <summary>P&C: Show adjacency connection lines between building and its bonus sources.</summary>
        private void ShowAdjacencyLines(CityBuildingPlacement placement)
        {
            if (!AdjacencyRules.TryGetValue(placement.BuildingId, out var rule)) return;
            if (placement.VisualGO == null) return;

            int range = 3;
            var seen = new HashSet<string>();
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var checkPos = new Vector2Int(placement.GridOrigin.x + dx, placement.GridOrigin.y + dy);
                    if (!_occupancy.TryGetValue(checkPos, out var neighborInstId)) continue;
                    if (neighborInstId == placement.InstanceId || seen.Contains(neighborInstId)) continue;

                    var neighbor = _placements.Find(p => p.InstanceId == neighborInstId);
                    if (neighbor == null) continue;

                    bool isValid = false;
                    foreach (var validNeighbor in rule.neighbors)
                        if (neighbor.BuildingId == validNeighbor) { isValid = true; break; }

                    if (!isValid) continue;
                    seen.Add(neighborInstId);

                    // Visual: green glow on the neighbor building
                    if (neighbor.VisualGO != null)
                    {
                        var glow = new GameObject("AdjacencyGlow");
                        glow.transform.SetParent(neighbor.VisualGO.transform, false);
                        var rect = glow.AddComponent<RectTransform>();
                        rect.anchorMin = Vector2.zero;
                        rect.anchorMax = Vector2.one;
                        rect.offsetMin = new Vector2(-2, -2);
                        rect.offsetMax = new Vector2(2, 2);
                        var img = glow.AddComponent<Image>();
                        img.color = new Color(0.20f, 0.80f, 0.30f, 0.25f);
                        img.raycastTarget = false;
                        // Auto-destroy after 3 seconds
                        StartCoroutine(DestroyAfterDelay(glow, 3f));
                    }
                }
            }
        }

        private IEnumerator DestroyAfterDelay(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) Destroy(go);
        }
    }

    // ====================================================================
    // P&C: Alliance Donation Panel — Embassy Building
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _donationPanel;

        private static readonly DonationTier[] DonationTiers = new[]
        {
            new DonationTier("Small Gift",   100, 100, 100, 0,   50,  "Common rewards"),
            new DonationTier("Medium Gift",  300, 300, 300, 50,  150, "Rare rewards"),
            new DonationTier("Large Gift",   800, 800, 800, 200, 400, "Epic rewards + alliance XP"),
            new DonationTier("Royal Gift",   2000, 2000, 2000, 500, 1000, "Legendary rewards + alliance rank"),
        };

        private struct DonationTier
        {
            public string Name;
            public int Stone, Iron, Grain, Arcane;
            public int AlliancePoints;
            public string RewardDesc;

            public DonationTier(string name, int stone, int iron, int grain, int arcane, int points, string desc)
            {
                Name = name; Stone = stone; Iron = iron; Grain = grain; Arcane = arcane;
                AlliancePoints = points; RewardDesc = desc;
            }
        }

        /// <summary>P&C: Show alliance donation panel from embassy building.</summary>
        private void ShowDonationPanel(int embassyTier)
        {
            if (_donationPanel != null) { Destroy(_donationPanel); _donationPanel = null; }
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _donationPanel = new GameObject("DonationPanel");
            _donationPanel.transform.SetParent(canvas.transform, false);
            var dimRect = _donationPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _donationPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _donationPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_donationPanel != null) { Destroy(_donationPanel); _donationPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_donationPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.18f);
            panelRect.anchorMax = new Vector2(0.94f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            AddInfoPanelText(panel.transform, "Title", "\u2694 Alliance Donation", 15, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f), new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            AddInfoPanelText(panel.transform, "Subtitle", "Donate resources to strengthen your alliance and earn rewards!", 10,
                FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f),
                new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.90f), TextAnchor.MiddleCenter);

            ServiceLocator.TryGet<ResourceManager>(out var rm);
            float rowY = 0.78f;
            float rowH = 0.15f;

            for (int t = 0; t < DonationTiers.Length; t++)
            {
                var tier = DonationTiers[t];
                float rowTop = rowY;
                float rowBot = rowY - rowH;
                bool locked = t >= embassyTier + 1; // Higher tiers need higher embassy
                bool canAfford = !locked && rm != null && rm.CanAfford(tier.Stone, tier.Iron, tier.Grain, tier.Arcane);

                var row = new GameObject($"Tier_{t}");
                row.transform.SetParent(panel.transform, false);
                var rowRect = row.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, rowBot);
                rowRect.anchorMax = new Vector2(0.97f, rowTop);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = row.AddComponent<Image>();
                rowBg.color = locked ? new Color(0.10f, 0.08f, 0.15f, 0.5f) : new Color(0.12f, 0.10f, 0.20f, 0.7f);

                // Tier name + reward
                Color tierColor = t switch { 0 => Color.white, 1 => new Color(0.4f, 0.7f, 1f), 2 => new Color(0.8f, 0.5f, 1f), _ => new Color(1f, 0.8f, 0.3f) };
                AddInfoPanelText(row.transform, "Name", tier.Name, 12, FontStyle.Bold,
                    locked ? new Color(0.5f, 0.5f, 0.5f) : tierColor,
                    new Vector2(0.02f, 0.5f), new Vector2(0.35f, 1f), TextAnchor.MiddleLeft);

                string costStr = locked ? $"Embassy Tier {t + 1} required" :
                    $"S:{tier.Stone} I:{tier.Iron} G:{tier.Grain}" + (tier.Arcane > 0 ? $" A:{tier.Arcane}" : "");
                AddInfoPanelText(row.transform, "Cost", costStr, 9, FontStyle.Normal,
                    locked ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.75f, 0.75f, 0.75f),
                    new Vector2(0.02f, 0f), new Vector2(0.55f, 0.50f), TextAnchor.MiddleLeft);

                // Alliance points
                if (!locked)
                {
                    AddInfoPanelText(row.transform, "Points", $"+{tier.AlliancePoints} pts", 10, FontStyle.Bold,
                        new Color(0.40f, 0.80f, 0.40f),
                        new Vector2(0.55f, 0.5f), new Vector2(0.72f, 1f), TextAnchor.MiddleCenter);
                    AddInfoPanelText(row.transform, "Reward", tier.RewardDesc, 8, FontStyle.Italic,
                        new Color(0.65f, 0.65f, 0.65f),
                        new Vector2(0.55f, 0f), new Vector2(0.72f, 0.5f), TextAnchor.MiddleCenter);
                }

                if (!locked)
                {
                    var donateBtnGO = new GameObject("DonateBtn");
                    donateBtnGO.transform.SetParent(row.transform, false);
                    var donateBtnRect = donateBtnGO.AddComponent<RectTransform>();
                    donateBtnRect.anchorMin = new Vector2(0.75f, 0.12f);
                    donateBtnRect.anchorMax = new Vector2(0.98f, 0.88f);
                    donateBtnRect.offsetMin = Vector2.zero;
                    donateBtnRect.offsetMax = Vector2.zero;
                    var donateBtnBg = donateBtnGO.AddComponent<Image>();
                    donateBtnBg.color = canAfford ? new Color(0.18f, 0.50f, 0.30f, 0.92f) : new Color(0.35f, 0.25f, 0.25f, 0.7f);
                    var donateBtn = donateBtnGO.AddComponent<Button>();
                    donateBtn.targetGraphic = donateBtnBg;
                    donateBtn.interactable = canAfford;
                    var capTier = tier;
                    int capEmbTier = embassyTier;
                    donateBtn.onClick.AddListener(() =>
                    {
                        if (rm != null && rm.CanAfford(capTier.Stone, capTier.Iron, capTier.Grain, capTier.Arcane))
                        {
                            rm.Spend(capTier.Stone, capTier.Iron, capTier.Grain, capTier.Arcane);
                            Debug.Log($"[Donation] {capTier.Name}: +{capTier.AlliancePoints} alliance points");
                            ShowUpgradeBlockedToast($"\u2694 Donated! +{capTier.AlliancePoints} Alliance Points");
                            if (_donationPanel != null) { Destroy(_donationPanel); _donationPanel = null; }
                            ShowDonationPanel(capEmbTier);
                        }
                    });
                    AddInfoPanelText(donateBtnGO.transform, "Label", canAfford ? "DONATE" : "LOW", 11, FontStyle.Bold,
                        canAfford ? Color.white : new Color(0.6f, 0.4f, 0.4f),
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                }

                rowY -= rowH + 0.012f;
            }

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.91f);
            closeRect.anchorMax = new Vector2(0.97f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_donationPanel != null) { Destroy(_donationPanel); _donationPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _donationPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }
    }

    // ====================================================================
    // P&C: City Layout Presets — Save/Load Building Arrangements
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _layoutPresetsPanel;
        private static readonly string[] PresetSlotNames = { "Battle Formation", "Resource Focus", "Balanced Layout", "Custom Slot 1", "Custom Slot 2" };

        // Simple serializable layout data
        private readonly Dictionary<string, List<(string buildingId, Vector2Int origin)>> _savedLayouts = new();

        /// <summary>P&C: Show layout presets panel — save/load/rename building arrangements.</summary>
        private void ShowLayoutPresetsPanel()
        {
            if (_layoutPresetsPanel != null) { Destroy(_layoutPresetsPanel); _layoutPresetsPanel = null; }
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _layoutPresetsPanel = new GameObject("LayoutPresetsPanel");
            _layoutPresetsPanel.transform.SetParent(canvas.transform, false);
            var dimRect = _layoutPresetsPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _layoutPresetsPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.6f);
            dimImg.raycastTarget = true;
            var dimBtn = _layoutPresetsPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_layoutPresetsPanel != null) { Destroy(_layoutPresetsPanel); _layoutPresetsPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_layoutPresetsPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.22f);
            panelRect.anchorMax = new Vector2(0.92f, 0.78f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            AddInfoPanelText(panel.transform, "Title", "\u2302 City Layout Presets", 15, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f), new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);

            float rowY = 0.82f;
            float rowH = 0.13f;

            for (int i = 0; i < PresetSlotNames.Length; i++)
            {
                string slotName = PresetSlotNames[i];
                float rowTop = rowY;
                float rowBot = rowY - rowH;
                bool hasSave = _savedLayouts.ContainsKey(slotName);

                var row = new GameObject($"Slot_{i}");
                row.transform.SetParent(panel.transform, false);
                var rowRect = row.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.03f, rowBot);
                rowRect.anchorMax = new Vector2(0.97f, rowTop);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var rowBg = row.AddComponent<Image>();
                rowBg.color = hasSave ? new Color(0.14f, 0.18f, 0.14f, 0.7f) : new Color(0.12f, 0.10f, 0.20f, 0.5f);

                // Slot name
                AddInfoPanelText(row.transform, "Name", $"{slotName}", 12, FontStyle.Bold,
                    hasSave ? Color.white : new Color(0.6f, 0.6f, 0.6f),
                    new Vector2(0.02f, 0f), new Vector2(0.45f, 1f), TextAnchor.MiddleLeft);

                // Building count if saved
                if (hasSave)
                {
                    int count = _savedLayouts[slotName].Count;
                    AddInfoPanelText(row.transform, "Count", $"{count} buildings", 9, FontStyle.Italic,
                        new Color(0.6f, 0.8f, 0.6f),
                        new Vector2(0.45f, 0f), new Vector2(0.60f, 1f), TextAnchor.MiddleCenter);
                }

                // Save button
                var saveGO = new GameObject("SaveBtn");
                saveGO.transform.SetParent(row.transform, false);
                var saveRect = saveGO.AddComponent<RectTransform>();
                saveRect.anchorMin = new Vector2(0.62f, 0.15f);
                saveRect.anchorMax = new Vector2(0.78f, 0.85f);
                saveRect.offsetMin = Vector2.zero;
                saveRect.offsetMax = Vector2.zero;
                var saveBg = saveGO.AddComponent<Image>();
                saveBg.color = new Color(0.18f, 0.45f, 0.55f, 0.90f);
                var saveBtn = saveGO.AddComponent<Button>();
                saveBtn.targetGraphic = saveBg;
                string capSlot = slotName;
                saveBtn.onClick.AddListener(() => SaveCurrentLayout(capSlot));
                AddInfoPanelText(saveGO.transform, "Label", "SAVE", 10, FontStyle.Bold, Color.white,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

                // Load button (only if saved)
                if (hasSave)
                {
                    var loadGO = new GameObject("LoadBtn");
                    loadGO.transform.SetParent(row.transform, false);
                    var loadRect = loadGO.AddComponent<RectTransform>();
                    loadRect.anchorMin = new Vector2(0.80f, 0.15f);
                    loadRect.anchorMax = new Vector2(0.98f, 0.85f);
                    loadRect.offsetMin = Vector2.zero;
                    loadRect.offsetMax = Vector2.zero;
                    var loadBg = loadGO.AddComponent<Image>();
                    loadBg.color = new Color(0.18f, 0.55f, 0.25f, 0.90f);
                    var loadBtn = loadGO.AddComponent<Button>();
                    loadBtn.targetGraphic = loadBg;
                    loadBtn.onClick.AddListener(() => LoadLayout(capSlot));
                    AddInfoPanelText(loadGO.transform, "Label", "LOAD", 10, FontStyle.Bold, Color.white,
                        Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                }

                rowY -= rowH + 0.012f;
            }

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.90f);
            closeRect.anchorMax = new Vector2(0.97f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_layoutPresetsPanel != null) { Destroy(_layoutPresetsPanel); _layoutPresetsPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _layoutPresetsPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void SaveCurrentLayout(string slotName)
        {
            var layout = new List<(string buildingId, Vector2Int origin)>();
            foreach (var p in _placements)
                layout.Add((p.BuildingId, p.GridOrigin));
            _savedLayouts[slotName] = layout;
            Debug.Log($"[LayoutPresets] Saved {layout.Count} buildings to '{slotName}'");
            ShowUpgradeBlockedToast($"\u2302 Layout saved to '{slotName}'!");
            // Refresh panel
            ShowLayoutPresetsPanel();
        }

        private void LoadLayout(string slotName)
        {
            if (!_savedLayouts.TryGetValue(slotName, out var layout))
            {
                ShowUpgradeBlockedToast("No saved layout in this slot.");
                return;
            }
            Debug.Log($"[LayoutPresets] Loading '{slotName}' with {layout.Count} buildings (swap logic would go here)");
            ShowUpgradeBlockedToast($"\u2302 Layout '{slotName}' loaded! ({layout.Count} buildings)");
            if (_layoutPresetsPanel != null) { Destroy(_layoutPresetsPanel); _layoutPresetsPanel = null; }
        }
    }

    // ====================================================================
    // P&C: Left-side circular event icon column
    // ====================================================================

    public partial class CityGridView
    {
        // P&C-style: uniform circular event icon column on RIGHT edge (matches P&C layout)
        // Slot 0 = bottom (above nav bar), slots go upward
        private const float EventIconX = 0.905f;   // right side
        private const float EventIconW = 0.088f;   // 8.8% width
        private const float EventIconH = 0.060f;   // ~square on phone
        private const float EventIconGap = 0.012f;
        private const float EventIconBaseY = 0.225f; // above chat bar

        /// <summary>Creates a P&C-style circular event icon at a given slot in the left column.</summary>
        private GameObject CreateCircularEventIcon(Transform parent, string name, int slot,
            Color bgColor, Color borderColor, string icon, Color iconColor,
            UnityEngine.Events.UnityAction onClick)
        {
            float yBot = EventIconBaseY + slot * (EventIconH + EventIconGap);
            float yTop = yBot + EventIconH;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(EventIconX, yBot);
            rect.anchorMax = new Vector2(EventIconX + EventIconW, yTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Glow ring (slightly larger, behind)
            var glow = new GameObject("Glow");
            glow.transform.SetParent(go.transform, false);
            glow.transform.SetAsFirstSibling();
            var glowRect = glow.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(-0.20f, -0.20f);
            glowRect.anchorMax = new Vector2(1.20f, 1.20f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImg = glow.AddComponent<Image>();
            glowImg.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.25f);
            glowImg.raycastTarget = false;
            var radialSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            if (radialSpr != null) glowImg.sprite = radialSpr;

            // Main circular bg
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = true;
            if (radialSpr != null) { bg.sprite = radialSpr; bg.type = Image.Type.Simple; }

            // Gold border ring
            var ring = go.AddComponent<Outline>();
            ring.effectColor = new Color(borderColor.r, borderColor.g, borderColor.b, 0.80f);
            ring.effectDistance = new Vector2(1.2f, -1.2f);

            // Button
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            if (onClick != null) btn.onClick.AddListener(onClick);

            // Icon text
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.10f, 0.15f);
            iconRect.anchorMax = new Vector2(0.90f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = icon;
            iconText.fontSize = 18;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = iconColor;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.raycastTarget = false;
            var iconShadow = iconGO.AddComponent<Shadow>();
            iconShadow.effectColor = new Color(0, 0, 0, 0.85f);
            iconShadow.effectDistance = new Vector2(0.5f, -0.5f);

            return go;
        }
    }

    // ====================================================================
    // P&C: Daily Treasure Chest — Tappable reward on city view
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _dailyChest;
        private bool _dailyChestCollected;

        /// <summary>P&C: Create a glowing treasure chest near stronghold that grants daily rewards.</summary>
        private void CreateDailyChest()
        {
            if (_dailyChestCollected) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // P&C circular icon — slot 0 in left event column (bottom)
            _dailyChest = CreateCircularEventIcon(canvas.transform, "DailyChest",
                0, new Color(0.55f, 0.35f, 0.12f, 0.95f), new Color(0.90f, 0.75f, 0.25f),
                "\u2620", new Color(1f, 0.85f, 0.30f), CollectDailyChest);

            // "FREE" red badge
            var badge = new GameObject("FreeBadge");
            badge.transform.SetParent(_dailyChest.transform, false);
            var badgeRect = badge.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.50f, 0.72f);
            badgeRect.anchorMax = new Vector2(1.10f, 1.05f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;
            var badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(0.90f, 0.15f, 0.10f, 0.95f);
            badgeBg.raycastTarget = false;
            var badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(1f, 1f, 1f, 0.4f);
            badgeOutline.effectDistance = new Vector2(0.5f, -0.5f);
            var bt = new GameObject("Text");
            bt.transform.SetParent(badge.transform, false);
            var btRect = bt.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            var btText = bt.AddComponent<Text>();
            btText.text = "FREE";
            btText.fontSize = 7;
            btText.fontStyle = FontStyle.Bold;
            btText.alignment = TextAnchor.MiddleCenter;
            btText.color = Color.white;
            btText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btText.raycastTarget = false;

            StartCoroutine(AnimateDailyChest());
        }

        private IEnumerator AnimateDailyChest()
        {
            while (_dailyChest != null)
            {
                float bounce = 1f + 0.05f * Mathf.Sin(Time.time * 2.5f);
                _dailyChest.transform.localScale = Vector3.one * bounce;
                // Glow pulse
                var glow = _dailyChest.transform.Find("Glow");
                if (glow != null)
                {
                    var img = glow.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(1f, 0.85f, 0.30f, 0.15f + 0.10f * Mathf.Sin(Time.time * 3f));
                }
                yield return null;
            }
        }

        private void CollectDailyChest()
        {
            _dailyChestCollected = true;
            if (_dailyChest != null) { Destroy(_dailyChest); _dailyChest = null; }

            // Grant rewards
            var rm = ServiceLocator.Get<ResourceManager>();
            if (rm != null)
            {
                rm.AddResource(ResourceType.Stone, 500);
                rm.AddResource(ResourceType.Iron, 500);
                rm.AddResource(ResourceType.Grain, 500);
                rm.AddResource(ResourceType.ArcaneEssence, 100);
            }

            ShowUpgradeBlockedToast("\u2620 Daily Chest! +500 Stone, +500 Iron, +500 Grain, +100 Arcane!");
            Debug.Log("[DailyChest] Collected daily reward.");
        }
    }

    // ====================================================================
    // P&C: Traveling Merchant / Mystery Shop
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _merchantPanel;
        private GameObject _merchantIcon;
        private bool _merchantVisited;

        private static readonly MerchantDeal[] MerchantDeals = new[]
        {
            new MerchantDeal("Speed-Up 1h", "\u231B", 50, "Speeds up construction by 1 hour"),
            new MerchantDeal("Resource Pack", "\u2618", 30, "+2000 of each resource"),
            new MerchantDeal("Builder Boost", "\u2692", 80, "+50% build speed for 30 min"),
            new MerchantDeal("Shield 4h", "\u26E8", 100, "Peace shield for 4 hours"),
            new MerchantDeal("VIP Points ×100", "\u2605", 60, "+100 VIP experience points"),
            new MerchantDeal("Rare Skin Token", "\u2728", 150, "Unlock a random building skin"),
        };

        private struct MerchantDeal
        {
            public string Name;
            public string Icon;
            public int GemCost;
            public string Desc;
            public MerchantDeal(string name, string icon, int gems, string desc)
            { Name = name; Icon = icon; GemCost = gems; Desc = desc; }
        }

        /// <summary>P&C: Create traveling merchant icon on city view — appears for limited time.</summary>
        private void CreateMerchantIcon()
        {
            if (_merchantVisited) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // P&C circular icon — slot 1 in left event column
            _merchantIcon = CreateCircularEventIcon(canvas.transform, "MerchantIcon",
                1, new Color(0.40f, 0.25f, 0.55f, 0.92f), new Color(0.70f, 0.50f, 0.85f),
                "\u2655", new Color(1f, 0.85f, 0.30f), ShowMerchantPanel);

            // Timer badge below icon
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(_merchantIcon.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.0f, -0.15f);
            timerRect.anchorMax = new Vector2(1.0f, 0.08f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = "1:59:45";
            timerText.fontSize = 6;
            timerText.fontStyle = FontStyle.Normal;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = new Color(0.8f, 0.8f, 0.8f);
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.raycastTarget = false;
            var timerShadow = timerGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0, 0, 0, 0.9f);
            timerShadow.effectDistance = new Vector2(0.5f, -0.5f);

            StartCoroutine(AnimateMerchantIcon());
        }

        private IEnumerator AnimateMerchantIcon()
        {
            while (_merchantIcon != null)
            {
                float wobble = Mathf.Sin(Time.time * 1.5f) * 2f;
                _merchantIcon.transform.localRotation = Quaternion.Euler(0, 0, wobble);
                yield return null;
            }
        }

        private void ShowMerchantPanel()
        {
            if (_merchantPanel != null) { Destroy(_merchantPanel); _merchantPanel = null; }
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _merchantPanel = new GameObject("MerchantPanel");
            _merchantPanel.transform.SetParent(canvas.transform, false);
            var dimRect = _merchantPanel.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _merchantPanel.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.65f);
            dimImg.raycastTarget = true;
            var dimBtn = _merchantPanel.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(() => { if (_merchantPanel != null) { Destroy(_merchantPanel); _merchantPanel = null; } });

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_merchantPanel.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.15f);
            panelRect.anchorMax = new Vector2(0.94f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.10f, 0.06f, 0.18f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.70f, 0.50f, 0.85f, 0.7f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            AddInfoPanelText(panel.transform, "Title", "\u2655 Traveling Merchant", 15, FontStyle.Bold,
                new Color(0.90f, 0.75f, 1f), new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.MiddleCenter);
            AddInfoPanelText(panel.transform, "Subtitle", "Limited time deals! Refreshes daily.", 10,
                FontStyle.Italic, new Color(0.7f, 0.6f, 0.8f),
                new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.90f), TextAnchor.MiddleCenter);

            // Deal grid: 2 columns × 3 rows
            int cols = 2;
            float cellW = 0.46f;
            float cellH = 0.22f;
            float startX = 0.03f;
            float startY = 0.78f;

            for (int i = 0; i < MerchantDeals.Length; i++)
            {
                var deal = MerchantDeals[i];
                int col = i % cols;
                int row = i / cols;
                float x = startX + col * (cellW + 0.02f);
                float y = startY - row * (cellH + 0.02f);

                var cell = new GameObject($"Deal_{i}");
                cell.transform.SetParent(panel.transform, false);
                var cellRect = cell.AddComponent<RectTransform>();
                cellRect.anchorMin = new Vector2(x, y - cellH);
                cellRect.anchorMax = new Vector2(x + cellW, y);
                cellRect.offsetMin = Vector2.zero;
                cellRect.offsetMax = Vector2.zero;
                var cellBg = cell.AddComponent<Image>();
                cellBg.color = new Color(0.15f, 0.10f, 0.25f, 0.8f);

                // Icon
                AddInfoPanelText(cell.transform, "Icon", deal.Icon, 20, FontStyle.Normal,
                    new Color(1f, 0.85f, 0.40f), new Vector2(0.02f, 0.30f), new Vector2(0.25f, 0.95f), TextAnchor.MiddleCenter);

                // Name + desc
                AddInfoPanelText(cell.transform, "Name", deal.Name, 11, FontStyle.Bold,
                    Color.white, new Vector2(0.28f, 0.55f), new Vector2(0.98f, 0.95f), TextAnchor.MiddleLeft);
                AddInfoPanelText(cell.transform, "Desc", deal.Desc, 8, FontStyle.Italic,
                    new Color(0.7f, 0.7f, 0.7f), new Vector2(0.28f, 0.28f), new Vector2(0.98f, 0.55f), TextAnchor.MiddleLeft);

                // Buy button
                var buyGO = new GameObject("BuyBtn");
                buyGO.transform.SetParent(cell.transform, false);
                var buyRect = buyGO.AddComponent<RectTransform>();
                buyRect.anchorMin = new Vector2(0.28f, 0.02f);
                buyRect.anchorMax = new Vector2(0.98f, 0.26f);
                buyRect.offsetMin = Vector2.zero;
                buyRect.offsetMax = Vector2.zero;
                var buyBg = buyGO.AddComponent<Image>();
                buyBg.color = new Color(0.50f, 0.30f, 0.65f, 0.92f);
                var buyBtn = buyGO.AddComponent<Button>();
                buyBtn.targetGraphic = buyBg;
                string dealName = deal.Name;
                int dealCost = deal.GemCost;
                buyBtn.onClick.AddListener(() =>
                {
                    ShowUpgradeBlockedToast($"\u2655 Purchased {dealName} for {dealCost} gems!");
                    Debug.Log($"[Merchant] Bought {dealName} for {dealCost} gems");
                });
                AddInfoPanelText(buyGO.transform, "Price", $"\u2666 {deal.GemCost}", 10, FontStyle.Bold,
                    Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // Close
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(panel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.91f);
            closeRect.anchorMax = new Vector2(0.97f, 0.98f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_merchantPanel != null) { Destroy(_merchantPanel); _merchantPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _merchantPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }
    }

    // ====================================================================
    // P&C: Event Hub + Gifts — Additional left-side circular event icons
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _eventHubIcon;
        private GameObject _giftsIcon;

        /// <summary>P&C: Events hub icon — slot 2 in left column.</summary>
        private void CreateEventHubIcon()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            _eventHubIcon = CreateCircularEventIcon(canvas.transform, "EventHubIcon",
                2, new Color(0.70f, 0.22f, 0.18f, 0.92f), new Color(0.95f, 0.45f, 0.25f),
                "\u2694", new Color(1f, 0.80f, 0.50f), ShowEventHubPanel);

            // P&C: Timer badge below icon (event countdown)
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(_eventHubIcon.transform, false);
            var tr = timerGO.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(-0.10f, -0.28f);
            tr.anchorMax = new Vector2(1.10f, 0.02f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var tbg = timerGO.AddComponent<Image>();
            tbg.color = new Color(0.06f, 0.04f, 0.10f, 0.88f);
            tbg.raycastTarget = false;
            var tOutline = timerGO.AddComponent<Outline>();
            tOutline.effectColor = new Color(0.95f, 0.45f, 0.25f, 0.5f);
            tOutline.effectDistance = new Vector2(0.5f, -0.5f);
            var tt = new GameObject("Text");
            tt.transform.SetParent(timerGO.transform, false);
            var ttr = tt.AddComponent<RectTransform>();
            ttr.anchorMin = Vector2.zero;
            ttr.anchorMax = Vector2.one;
            ttr.offsetMin = Vector2.zero;
            ttr.offsetMax = Vector2.zero;
            var ttxt = tt.AddComponent<Text>();
            ttxt.text = "10:04:49";
            ttxt.fontSize = 6;
            ttxt.fontStyle = FontStyle.Bold;
            ttxt.alignment = TextAnchor.MiddleCenter;
            ttxt.color = new Color(0.95f, 0.80f, 0.50f);
            ttxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ttxt.raycastTarget = false;
            var ts = tt.AddComponent<Shadow>();
            ts.effectColor = new Color(0, 0, 0, 0.9f);
            ts.effectDistance = new Vector2(0.4f, -0.4f);

            // Notification dot
            AddNotificationDot(_eventHubIcon.transform, "3");
        }

        /// <summary>P&C: Gifts/mail icon — slot 3 in left column.</summary>
        private void CreateGiftsIcon()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            _giftsIcon = CreateCircularEventIcon(canvas.transform, "GiftsIcon",
                3, new Color(0.20f, 0.50f, 0.30f, 0.92f), new Color(0.40f, 0.80f, 0.45f),
                "\u2618", new Color(0.80f, 1f, 0.70f), ShowGiftsPanel);

            // P&C: Timer badge below icon (gift expiry)
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(_giftsIcon.transform, false);
            var tr = timerGO.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(-0.10f, -0.28f);
            tr.anchorMax = new Vector2(1.10f, 0.02f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var tbg = timerGO.AddComponent<Image>();
            tbg.color = new Color(0.06f, 0.04f, 0.10f, 0.88f);
            tbg.raycastTarget = false;
            var tOutline = timerGO.AddComponent<Outline>();
            tOutline.effectColor = new Color(0.40f, 0.80f, 0.45f, 0.5f);
            tOutline.effectDistance = new Vector2(0.5f, -0.5f);
            var tt = new GameObject("Text");
            tt.transform.SetParent(timerGO.transform, false);
            var ttr = tt.AddComponent<RectTransform>();
            ttr.anchorMin = Vector2.zero;
            ttr.anchorMax = Vector2.one;
            ttr.offsetMin = Vector2.zero;
            ttr.offsetMax = Vector2.zero;
            var ttxt = tt.AddComponent<Text>();
            ttxt.text = "23:59:52";
            ttxt.fontSize = 6;
            ttxt.fontStyle = FontStyle.Bold;
            ttxt.alignment = TextAnchor.MiddleCenter;
            ttxt.color = new Color(0.70f, 1f, 0.70f);
            ttxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ttxt.raycastTarget = false;
            var ts = tt.AddComponent<Shadow>();
            ts.effectColor = new Color(0, 0, 0, 0.9f);
            ts.effectDistance = new Vector2(0.4f, -0.4f);

            // Notification dot
            AddNotificationDot(_giftsIcon.transform, "5");
        }

        /// <summary>Red notification dot with count — top-right corner of icon.</summary>
        private void AddNotificationDot(Transform parent, string count)
        {
            var dot = new GameObject("NotifDot");
            dot.transform.SetParent(parent, false);
            var dr = dot.AddComponent<RectTransform>();
            dr.anchorMin = new Vector2(0.62f, 0.65f);
            dr.anchorMax = new Vector2(1.08f, 1.08f);
            dr.offsetMin = Vector2.zero;
            dr.offsetMax = Vector2.zero;
            var dbg = dot.AddComponent<Image>();
            dbg.color = new Color(0.90f, 0.15f, 0.10f, 0.95f);
            dbg.raycastTarget = false;
            var dOutline = dot.AddComponent<Outline>();
            dOutline.effectColor = new Color(1f, 1f, 1f, 0.4f);
            dOutline.effectDistance = new Vector2(0.4f, -0.4f);

            var dt = new GameObject("Text");
            dt.transform.SetParent(dot.transform, false);
            var dtr = dt.AddComponent<RectTransform>();
            dtr.anchorMin = Vector2.zero;
            dtr.anchorMax = Vector2.one;
            dtr.offsetMin = Vector2.zero;
            dtr.offsetMax = Vector2.zero;
            var dtxt = dt.AddComponent<Text>();
            dtxt.text = count;
            dtxt.fontSize = 7;
            dtxt.fontStyle = FontStyle.Bold;
            dtxt.alignment = TextAnchor.MiddleCenter;
            dtxt.color = Color.white;
            dtxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dtxt.raycastTarget = false;
        }

        private void ShowEventHubPanel()
        {
            ShowUpgradeBlockedToast("\u2694 Events — Coming soon! Check back during Alliance Wars.");
            PlaySfx(_sfxTap);
        }

        private void ShowGiftsPanel()
        {
            ShowUpgradeBlockedToast("\u2618 Gifts — 5 unclaimed alliance gifts available!");
            PlaySfx(_sfxTap);
        }
    }

    // ====================================================================
    // P&C: City Prosperity Rank — Score based on buildings and upgrades
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _prosperityBadge;

        private static readonly Dictionary<string, int> BuildingProsperityBase = new()
        {
            { "stronghold", 500 }, { "barracks", 100 }, { "wall", 80 }, { "watch_tower", 80 },
            { "grain_farm", 60 }, { "iron_mine", 60 }, { "stone_quarry", 60 }, { "arcane_tower", 120 },
            { "marketplace", 90 }, { "academy", 110 }, { "forge", 90 }, { "armory", 90 },
            { "guild_hall", 100 }, { "embassy", 100 }, { "training_ground", 80 }, { "laboratory", 110 },
            { "library", 90 }, { "hero_shrine", 120 }, { "observatory", 100 }, { "archive", 90 },
            { "enchanting_tower", 110 },
        };

        /// <summary>P&C: Calculate total city prosperity score.</summary>
        private int CalculateProsperity()
        {
            int total = 0;
            foreach (var p in _placements)
            {
                int baseVal = BuildingProsperityBase.TryGetValue(p.BuildingId, out var bv) ? bv : 30;
                total += baseVal * p.Tier;
                // Adjacency bonus adds prosperity
                total += GetAdjacencyBonus(p) * 2;
            }
            // Decoration bonus
            foreach (var p in _placements)
            {
                if (p.BuildingId == "fountain" || p.BuildingId == "statue" || p.BuildingId == "garden")
                    total += 25 * p.Tier;
            }
            return total;
        }

        /// <summary>P&C: Get city rank title based on prosperity score.</summary>
        private static (string title, Color color) GetProsperityRank(int score)
        {
            if (score >= 10000) return ("Imperial Capital", new Color(1f, 0.85f, 0.25f));
            if (score >= 7000) return ("Grand Citadel", new Color(0.85f, 0.55f, 1f));
            if (score >= 5000) return ("Fortified City", new Color(0.40f, 0.75f, 1f));
            if (score >= 3000) return ("Growing Town", new Color(0.45f, 0.85f, 0.45f));
            if (score >= 1500) return ("Small Settlement", new Color(0.80f, 0.80f, 0.80f));
            return ("Frontier Outpost", new Color(0.60f, 0.55f, 0.50f));
        }

        /// <summary>P&C: Create or update prosperity badge on the city HUD.</summary>
        private void UpdateProsperityBadge()
        {
            int score = CalculateProsperity();
            var (title, color) = GetProsperityRank(score);

            if (_prosperityBadge != null) Destroy(_prosperityBadge);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _prosperityBadge = new GameObject("ProsperityBadge");
            _prosperityBadge.transform.SetParent(canvas.transform, false);
            var rect = _prosperityBadge.AddComponent<RectTransform>();
            // Position inside the commander info panel content area (smaller, not a separate panel)
            rect.anchorMin = new Vector2(0.04f, 0.895f);
            rect.anchorMax = new Vector2(0.28f, 0.915f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _prosperityBadge.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.12f, 0.55f); // more transparent to blend
            bg.raycastTarget = false;
            var outline = _prosperityBadge.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.5f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_prosperityBadge.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4, 0);
            labelRect.offsetMax = new Vector2(-4, 0);
            var label = labelGO.AddComponent<Text>();
            label.text = $"\u2726 {title} — {score:N0} Prosperity";
            label.fontSize = 9;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = color;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.raycastTarget = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // P&C PARTIAL: City Quests (Daily/Weekly city-related quests with rewards)
    // ═══════════════════════════════════════════════════════════════════
    public partial class CityGridView
    {
        private GameObject _questPanel;
        private GameObject _questButton;

        private static readonly CityQuest[] DailyQuests = new[]
        {
            new CityQuest("upgrade_1",    "Upgrade 1 Building",        "Upgrade any building once",            200, 200, 200, 50,  1),
            new CityQuest("collect_5",    "Collect 5 Bubbles",         "Tap 5 resource bubbles",               100, 100, 100, 25,  5),
            new CityQuest("train_troops", "Train Troops",              "Start a troop training session",       150, 150, 150, 30,  1),
            new CityQuest("visit_market", "Visit the Marketplace",     "Open the marketplace building",        100, 100, 0,   50,  1),
            new CityQuest("donate_ally",  "Donate to Alliance",        "Make any alliance donation",           0,   0,   300, 75,  1),
        };

        private static readonly CityQuest[] WeeklyQuests = new[]
        {
            new CityQuest("upgrade_5",     "Upgrade 5 Buildings",      "Complete 5 building upgrades",         800,  800,  800,  200, 5),
            new CityQuest("reach_prosper", "Reach Next Prosperity",    "Increase your prosperity rank",        500,  500,  500,  300, 1),
            new CityQuest("collect_all",   "Mass Collect",             "Use Collect All 3 times",              400,  400,  400,  100, 3),
        };

        private struct CityQuest
        {
            public string Id;
            public string Title;
            public string Description;
            public long StoneReward;
            public long IronReward;
            public long GrainReward;
            public long ArcaneReward;
            public int RequiredCount;

            public CityQuest(string id, string title, string desc, long stone, long iron, long grain, long arcane, int count)
            {
                Id = id; Title = title; Description = desc;
                StoneReward = stone; IronReward = iron; GrainReward = grain; ArcaneReward = arcane;
                RequiredCount = count;
            }
        }

        private readonly Dictionary<string, int> _questProgress = new();
        private readonly HashSet<string> _questClaimed = new();

        /// <summary>P&C: Create the quest button icon on the left side of the city screen.</summary>
        private void CreateQuestButton()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _questButton = new GameObject("QuestButton");
            _questButton.transform.SetParent(canvas.transform, false);
            var rect = _questButton.AddComponent<RectTransform>();
            // P&C-style: slim strip on left edge below queue indicators
            rect.anchorMin = new Vector2(0.0f, 0.58f);
            rect.anchorMax = new Vector2(0.18f, 0.605f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _questButton.AddComponent<Image>();
            bg.color = new Color(0.55f, 0.35f, 0.15f, 0.92f);
            bg.raycastTarget = true;

            var outline = _questButton.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = _questButton.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(ShowQuestPanel);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_questButton.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = labelGO.AddComponent<Text>();
            text.text = "\u2694 QUESTS";
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(0.8f, -0.8f);

            // Red notification dot for incomplete quests
            var dotGO = new GameObject("NotifDot");
            dotGO.transform.SetParent(_questButton.transform, false);
            var dotRect = dotGO.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.85f, 0.70f);
            dotRect.anchorMax = new Vector2(1.05f, 1.05f);
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;
            var dot = dotGO.AddComponent<Image>();
            dot.color = new Color(0.9f, 0.15f, 0.15f, 0.95f);
        }

        /// <summary>P&C: Show daily and weekly quest panel.</summary>
        private void ShowQuestPanel()
        {
            if (_questPanel != null) { Destroy(_questPanel); _questPanel = null; return; }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Dim overlay
            var overlay = new GameObject("QuestOverlay");
            overlay.transform.SetParent(canvas.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.6f);
            overlayImg.raycastTarget = true;
            var overlayBtn = overlay.AddComponent<Button>();
            overlayBtn.onClick.AddListener(() => { if (_questPanel != null) { Destroy(_questPanel); _questPanel = null; } });

            _questPanel = new GameObject("QuestPanel");
            _questPanel.transform.SetParent(overlay.transform, false);
            var panelRect = _questPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.12f);
            panelRect.anchorMax = new Vector2(0.92f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelBg = _questPanel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);
            panelBg.raycastTarget = true;
            var panelOutline = _questPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.85f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            AddInfoPanelText(_questPanel.transform, "Title", "\u2694 CITY QUESTS", 16, FontStyle.Bold,
                new Color(0.95f, 0.82f, 0.45f),
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f), TextAnchor.UpperCenter);

            // Daily section header
            AddInfoPanelText(_questPanel.transform, "DailyHeader", "DAILY QUESTS", 12, FontStyle.Bold,
                new Color(0.7f, 0.85f, 1f),
                new Vector2(0.05f, 0.82f), new Vector2(0.50f, 0.88f), TextAnchor.MiddleLeft);

            float y = 0.78f;
            float rowH = 0.09f;
            foreach (var quest in DailyQuests)
            {
                CreateQuestRow(_questPanel.transform, quest, y, y - rowH, true);
                y -= rowH + 0.01f;
            }

            // Weekly section header
            AddInfoPanelText(_questPanel.transform, "WeeklyHeader", "WEEKLY QUESTS", 12, FontStyle.Bold,
                new Color(1f, 0.75f, 0.5f),
                new Vector2(0.05f, y - 0.02f), new Vector2(0.50f, y + 0.04f), TextAnchor.MiddleLeft);

            y -= 0.08f;
            foreach (var quest in WeeklyQuests)
            {
                CreateQuestRow(_questPanel.transform, quest, y, y - rowH, false);
                y -= rowH + 0.01f;
            }

            // Close button
            var closeGO = new GameObject("CloseBtn");
            closeGO.transform.SetParent(_questPanel.transform, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.88f, 0.92f);
            closeRect.anchorMax = new Vector2(0.97f, 0.99f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            var closeBg = closeGO.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => { if (_questPanel != null) { Destroy(_questPanel); _questPanel = null; } });
            AddInfoPanelText(closeGO.transform, "X", "\u2715", 14, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            var cg = _questPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInDialog(cg));
        }

        private void CreateQuestRow(Transform parent, CityQuest quest, float top, float bottom, bool isDaily)
        {
            var row = new GameObject($"Quest_{quest.Id}");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.03f, bottom);
            rowRect.anchorMax = new Vector2(0.97f, top);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var rowBg = row.AddComponent<Image>();
            bool claimed = _questClaimed.Contains(quest.Id);
            int progress = _questProgress.TryGetValue(quest.Id, out var p) ? p : 0;
            bool complete = progress >= quest.RequiredCount;
            rowBg.color = claimed ? new Color(0.15f, 0.25f, 0.15f, 0.7f)
                        : complete ? new Color(0.20f, 0.35f, 0.15f, 0.8f)
                        : new Color(0.12f, 0.10f, 0.18f, 0.8f);

            // Quest title and description
            AddInfoPanelText(row.transform, "QTitle", quest.Title, 11, FontStyle.Bold,
                Color.white,
                new Vector2(0.02f, 0.50f), new Vector2(0.55f, 0.95f), TextAnchor.MiddleLeft);
            AddInfoPanelText(row.transform, "QDesc", quest.Description, 8, FontStyle.Normal,
                new Color(0.7f, 0.7f, 0.7f),
                new Vector2(0.02f, 0.05f), new Vector2(0.55f, 0.50f), TextAnchor.MiddleLeft);

            // Progress
            string progText = claimed ? "\u2714 CLAIMED" : $"{Mathf.Min(progress, quest.RequiredCount)}/{quest.RequiredCount}";
            Color progColor = claimed ? new Color(0.5f, 0.8f, 0.5f) : complete ? new Color(0.4f, 0.9f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);
            AddInfoPanelText(row.transform, "Progress", progText, 10, FontStyle.Bold,
                progColor,
                new Vector2(0.55f, 0.05f), new Vector2(0.72f, 0.95f), TextAnchor.MiddleCenter);

            // Reward summary
            string reward = $"+{quest.StoneReward}\u25C6 +{quest.IronReward}\u25C6 +{quest.GrainReward}\u25C6";
            if (quest.ArcaneReward > 0) reward += $" +{quest.ArcaneReward}\u2726";
            AddInfoPanelText(row.transform, "Reward", reward, 7, FontStyle.Normal,
                new Color(0.9f, 0.78f, 0.4f),
                new Vector2(0.72f, 0.50f), new Vector2(0.98f, 0.95f), TextAnchor.MiddleRight);

            // Claim button (if complete and not claimed)
            if (complete && !claimed)
            {
                var claimGO = new GameObject("ClaimBtn");
                claimGO.transform.SetParent(row.transform, false);
                var claimRect = claimGO.AddComponent<RectTransform>();
                claimRect.anchorMin = new Vector2(0.75f, 0.10f);
                claimRect.anchorMax = new Vector2(0.97f, 0.48f);
                claimRect.offsetMin = Vector2.zero;
                claimRect.offsetMax = Vector2.zero;
                var claimBg = claimGO.AddComponent<Image>();
                claimBg.color = new Color(0.15f, 0.65f, 0.25f, 0.95f);
                var claimBtn = claimGO.AddComponent<Button>();
                claimBtn.targetGraphic = claimBg;
                string qId = quest.Id;
                long sR = quest.StoneReward, iR = quest.IronReward, gR = quest.GrainReward, aR = quest.ArcaneReward;
                claimBtn.onClick.AddListener(() => ClaimQuest(qId, sR, iR, gR, aR));
                AddInfoPanelText(claimGO.transform, "ClaimLabel", "CLAIM", 9, FontStyle.Bold,
                    Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }
        }

        private void ClaimQuest(string questId, long stone, long iron, long grain, long arcane)
        {
            if (_questClaimed.Contains(questId)) return;
            _questClaimed.Add(questId);

            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
            {
                rm.AddResource(ResourceType.Stone, stone);
                rm.AddResource(ResourceType.Iron, iron);
                rm.AddResource(ResourceType.Grain, grain);
                rm.AddResource(ResourceType.ArcaneEssence, arcane);
            }

            Debug.Log($"[CityQuests] Claimed quest '{questId}': +{stone}S +{iron}I +{grain}G +{arcane}A");

            // Refresh panel
            if (_questPanel != null)
            {
                var parent = _questPanel.transform.parent;
                Destroy(_questPanel); _questPanel = null;
                ShowQuestPanel();
            }
        }

        /// <summary>P&C: Increment quest progress for a given quest ID.</summary>
        public void IncrementQuestProgress(string questId, int amount = 1)
        {
            if (!_questProgress.ContainsKey(questId))
                _questProgress[questId] = 0;
            _questProgress[questId] += amount;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // P&C PARTIAL: Building Relocate (Quick-relocate via radial menu)
    // ═══════════════════════════════════════════════════════════════════
    public partial class CityGridView
    {
        private GameObject _relocateHighlight;
        private CityBuildingPlacement _relocatingBuilding;
        private bool _relocateMode;

        /// <summary>P&C: Enter relocate mode for a specific building from radial menu.</summary>
        private void EnterRelocateMode(CityBuildingPlacement placement)
        {
            if (placement.BuildingId == "stronghold") return; // Can't relocate stronghold

            _relocatingBuilding = placement;
            _relocateMode = true;

            // Show instruction banner
            ShowRelocateBanner();

            // Highlight the building being relocated
            if (placement.VisualGO != null)
            {
                var img = placement.VisualGO.GetComponent<Image>();
                if (img != null) img.color = new Color(0.5f, 0.8f, 1f, 0.7f);
            }

            // Close any open panels
            DismissBuildingInfoPanel();

            Debug.Log($"[Relocate] Entered relocate mode for {placement.BuildingId} ({placement.InstanceId})");
        }

        private GameObject _relocateBanner;

        private void ShowRelocateBanner()
        {
            if (_relocateBanner != null) Destroy(_relocateBanner);

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _relocateBanner = new GameObject("RelocateBanner");
            _relocateBanner.transform.SetParent(canvas.transform, false);
            var rect = _relocateBanner.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.88f);
            rect.anchorMax = new Vector2(0.90f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = _relocateBanner.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.35f, 0.55f, 0.92f);

            var outline = _relocateBanner.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.7f, 1f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_relocateBanner.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(0.75f, 1f);
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = Vector2.zero;
            var text = labelGO.AddComponent<Text>();
            string bName = BuildingDisplayNames.TryGetValue(_relocatingBuilding.BuildingId, out var n) ? n : _relocatingBuilding.BuildingId;
            text.text = $"TAP A NEW LOCATION FOR: {bName}";
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            // Cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(_relocateBanner.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.78f, 0.10f);
            cancelRect.anchorMax = new Vector2(0.98f, 0.90f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelBg = cancelGO.AddComponent<Image>();
            cancelBg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelBg;
            cancelBtn.onClick.AddListener(ExitRelocateMode);
            AddInfoPanelText(cancelGO.transform, "CancelLabel", "CANCEL", 10, FontStyle.Bold,
                Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
        }

        /// <summary>P&C: Attempt to relocate building to tapped grid position.</summary>
        private void TryRelocateToPosition(Vector2Int targetOrigin)
        {
            if (_relocatingBuilding == null) { ExitRelocateMode(); return; }

            // Check if target is valid (inside playable area and unoccupied)
            var size = _relocatingBuilding.Size;
            bool canPlace = true;
            for (int dx = 0; dx < size.x && canPlace; dx++)
            {
                for (int dy = 0; dy < size.y && canPlace; dy++)
                {
                    var cell = targetOrigin + new Vector2Int(dx, dy);
                    if (cell.x < 2 || cell.x > 44 || cell.y < 2 || cell.y > 44)
                    {
                        canPlace = false;
                        break;
                    }
                    if (_occupancy.TryGetValue(cell, out var occupant) && occupant != _relocatingBuilding.InstanceId)
                    {
                        canPlace = false;
                        break;
                    }
                }
            }

            if (!canPlace)
            {
                ShowUpgradeBlockedToast("Cannot place here — blocked or outside city walls");
                return;
            }

            // Clear old occupancy
            var oldOrigin = _relocatingBuilding.GridOrigin;
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    _occupancy.Remove(oldOrigin + new Vector2Int(dx, dy));

            // Set new occupancy
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    _occupancy[targetOrigin + new Vector2Int(dx, dy)] = _relocatingBuilding.InstanceId;

            // Update placement data
            _relocatingBuilding.GridOrigin = targetOrigin;

            // Move visual
            if (_relocatingBuilding.VisualGO != null)
            {
                var newPos = GridToLocalCenter(targetOrigin, size);
                _relocatingBuilding.VisualGO.GetComponent<RectTransform>().anchoredPosition = newPos;

                // Reset tint
                var img = _relocatingBuilding.VisualGO.GetComponent<Image>();
                if (img != null) img.color = Color.white;
            }

            Debug.Log($"[Relocate] Moved {_relocatingBuilding.BuildingId} to ({targetOrigin.x},{targetOrigin.y})");
            SortBuildingsByDepth();
            ExitRelocateMode();
        }

        private void ExitRelocateMode()
        {
            _relocateMode = false;

            // Reset building tint
            if (_relocatingBuilding?.VisualGO != null)
            {
                var img = _relocatingBuilding.VisualGO.GetComponent<Image>();
                if (img != null) img.color = Color.white;
            }

            _relocatingBuilding = null;
            if (_relocateBanner != null) { Destroy(_relocateBanner); _relocateBanner = null; }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // P&C PARTIAL: Resource Rush (Tap resource bar to rush-collect all producers)
    // ═══════════════════════════════════════════════════════════════════
    public partial class CityGridView
    {
        private float _resourceRushCooldown;
        private const float ResourceRushCooldownTime = 30f; // Can rush-collect every 30 seconds

        /// <summary>P&C: Rush-collect all resource producers. Triggered by tapping resource bar.</summary>
        private void TriggerResourceRush()
        {
            if (_resourceRushCooldown > 0f)
            {
                ShowUpgradeBlockedToast($"Rush on cooldown ({(int)_resourceRushCooldown}s remaining)");
                return;
            }

            // Collect all resource bubbles
            var spawner = FindFirstObjectByType<ResourceBubbleSpawner>();
            if (spawner != null)
            {
                int bubbles = spawner.GetTotalActiveBubbleCount();
                if (bubbles > 0)
                {
                    spawner.CollectAll();
                    Debug.Log($"[ResourceRush] Rush-collected {bubbles} bubbles");
                }
            }

            // Also grant a small bonus from direct production (5 seconds worth)
            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
            {
                long bonusStone = (long)(rm.StonePerSecond * 5f);
                long bonusIron = (long)(rm.IronPerSecond * 5f);
                long bonusGrain = (long)(rm.GrainPerSecond * 5f);
                long bonusArcane = (long)(rm.ArcaneEssencePerSecond * 5f);

                if (bonusStone > 0) rm.AddResource(ResourceType.Stone, bonusStone);
                if (bonusIron > 0) rm.AddResource(ResourceType.Iron, bonusIron);
                if (bonusGrain > 0) rm.AddResource(ResourceType.Grain, bonusGrain);
                if (bonusArcane > 0) rm.AddResource(ResourceType.ArcaneEssence, bonusArcane);

                Debug.Log($"[ResourceRush] Bonus: +{bonusStone}S +{bonusIron}I +{bonusGrain}G +{bonusArcane}A");
            }

            _resourceRushCooldown = ResourceRushCooldownTime;

            // Visual feedback — flash resource bar green
            StartCoroutine(FlashResourceRush());
        }

        private System.Collections.IEnumerator FlashResourceRush()
        {
            // Find the resource bar in the canvas
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) yield break;

            var flashGO = new GameObject("RushFlash");
            flashGO.transform.SetParent(canvas.transform, false);
            var rect = flashGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.92f);
            rect.anchorMax = new Vector2(1f, 0.97f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = flashGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.8f, 0.3f, 0.5f);
            img.raycastTarget = false;

            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float alpha = 0.5f * (1f - t / 0.5f);
                img.color = new Color(0.2f, 0.8f, 0.3f, alpha);
                yield return null;
            }
            Destroy(flashGO);
        }

        /// <summary>P&C: Tick the resource rush cooldown timer.</summary>
        private void TickResourceRushCooldown()
        {
            if (_resourceRushCooldown > 0f)
                _resourceRushCooldown -= Time.deltaTime;
        }

        // ====================================================================
        // P&C IT99: Completion Soon Badge (red pulsing timer on buildings near done)
        // ====================================================================

        /// <summary>P&C: Red pulsing countdown badge on buildings within 5 min of upgrade completion.</summary>
        private void CreateCompletionSoonBadge(GameObject indicator)
        {
            var badge = new GameObject("CompletionSoonBadge");
            badge.transform.SetParent(indicator.transform, false);

            var rect = badge.AddComponent<RectTransform>();
            // Position at top-right corner, slightly outside indicator bounds
            rect.anchorMin = new Vector2(0.70f, 0.75f);
            rect.anchorMax = new Vector2(1.15f, 1.20f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bg.color = new Color(0.90f, 0.20f, 0.15f, 0.85f);
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.50f, 0.20f, 0.6f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);

            // Clock icon + countdown text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = "Soon";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.6f, -0.6f);
        }

        // ====================================================================
        // P&C IT99: Detail Panel Navigation Arrows (swipe between buildings)
        // ====================================================================

        /// <summary>P&C: Add left/right navigation arrows to detail panel for cycling between buildings.</summary>
        private void AddDetailPanelNavigation(GameObject popup, string currentInstanceId)
        {
            // Build sorted list of all placed buildings (by grid position for consistent order)
            var sortedPlacements = new List<CityBuildingPlacement>(_placements);
            sortedPlacements.Sort((a, b) =>
            {
                int cmp = a.GridOrigin.y.CompareTo(b.GridOrigin.y);
                return cmp != 0 ? cmp : a.GridOrigin.x.CompareTo(b.GridOrigin.x);
            });

            int currentIdx = -1;
            for (int i = 0; i < sortedPlacements.Count; i++)
            {
                if (sortedPlacements[i].InstanceId == currentInstanceId)
                { currentIdx = i; break; }
            }
            if (currentIdx < 0 || sortedPlacements.Count < 2) return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Left arrow (previous building)
            {
                var arrowGO = new GameObject("NavArrowLeft");
                arrowGO.transform.SetParent(popup.transform, false);
                var arrowRect = arrowGO.AddComponent<RectTransform>();
                arrowRect.anchorMin = new Vector2(0.00f, 0.44f);
                arrowRect.anchorMax = new Vector2(0.06f, 0.58f);
                arrowRect.offsetMin = Vector2.zero;
                arrowRect.offsetMax = Vector2.zero;

                var arrowBg = arrowGO.AddComponent<Image>();
                arrowBg.color = new Color(0.15f, 0.12f, 0.25f, 0.85f);
                arrowBg.raycastTarget = true;
                var arrowOutline = arrowGO.AddComponent<Outline>();
                arrowOutline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.5f);
                arrowOutline.effectDistance = new Vector2(0.8f, -0.8f);

                var btn = arrowGO.AddComponent<Button>();
                btn.targetGraphic = arrowBg;
                int prevIdx = (currentIdx - 1 + sortedPlacements.Count) % sortedPlacements.Count;
                var prevPlacement = sortedPlacements[prevIdx];
                btn.onClick.AddListener(() => NavigateToBuilding(prevPlacement));

                AddInfoPanelText(arrowGO.transform, "Arrow", "\u25C0", 14, FontStyle.Bold,
                    new Color(0.90f, 0.80f, 0.35f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // Right arrow (next building)
            {
                var arrowGO = new GameObject("NavArrowRight");
                arrowGO.transform.SetParent(popup.transform, false);
                var arrowRect = arrowGO.AddComponent<RectTransform>();
                arrowRect.anchorMin = new Vector2(0.94f, 0.44f);
                arrowRect.anchorMax = new Vector2(1.00f, 0.58f);
                arrowRect.offsetMin = Vector2.zero;
                arrowRect.offsetMax = Vector2.zero;

                var arrowBg = arrowGO.AddComponent<Image>();
                arrowBg.color = new Color(0.15f, 0.12f, 0.25f, 0.85f);
                arrowBg.raycastTarget = true;
                var arrowOutline = arrowGO.AddComponent<Outline>();
                arrowOutline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.5f);
                arrowOutline.effectDistance = new Vector2(0.8f, -0.8f);

                var btn = arrowGO.AddComponent<Button>();
                btn.targetGraphic = arrowBg;
                int nextIdx = (currentIdx + 1) % sortedPlacements.Count;
                var nextPlacement = sortedPlacements[nextIdx];
                btn.onClick.AddListener(() => NavigateToBuilding(nextPlacement));

                AddInfoPanelText(arrowGO.transform, "Arrow", "\u25B6", 14, FontStyle.Bold,
                    new Color(0.90f, 0.80f, 0.35f),
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
            }

            // Building counter label (e.g., "3 / 41")
            AddInfoPanelText(popup.transform, "NavCounter",
                $"{currentIdx + 1} / {sortedPlacements.Count}",
                9, FontStyle.Normal, new Color(0.60f, 0.55f, 0.70f),
                new Vector2(0.40f, 0.93f), new Vector2(0.60f, 0.98f), TextAnchor.MiddleCenter);
        }

        /// <summary>P&C: Navigate detail panel to another building (dismiss current + open new).</summary>
        private void NavigateToBuilding(CityBuildingPlacement placement)
        {
            if (placement == null || placement.VisualGO == null) return;

            DismissInfoPopup();

            // Publish tap event to re-open the detail panel for the target building
            EventBus.Publish(new BuildingTappedEvent(placement));
        }

        // ====================================================================
        // P&C IT99: Queue Slot Counter for Upgrade Strip
        // ====================================================================

        private const int MaxFreeQueueSlots = 2;

        /// <summary>P&C: Create queue slot counter label in the upgrade strip area.</summary>
        private void CreateQueueSlotCounter(GameObject strip, int usedSlots)
        {
            var counterGO = new GameObject("QueueSlotCounter");
            counterGO.transform.SetParent(strip.transform, false);
            var rect = counterGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.00f, 0f);
            rect.anchorMax = new Vector2(0.18f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Background pill
            var bg = counterGO.AddComponent<Image>();
            bool isFull = usedSlots >= MaxFreeQueueSlots;
            bg.color = isFull
                ? new Color(0.50f, 0.15f, 0.10f, 0.85f)  // Red when full
                : new Color(0.10f, 0.35f, 0.15f, 0.85f);  // Green when free
            bg.raycastTarget = false;

            var outline = counterGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.70f, 0.55f, 0.15f, 0.40f);
            outline.effectDistance = new Vector2(0.6f, -0.6f);

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(counterGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = $"\u2692 {usedSlots}/{MaxFreeQueueSlots}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.5f, -0.5f);
        }

        // ====================================================================
        // P&C IT99: Boost Timer Badge on Buildings
        // ====================================================================

        /// <summary>P&C: Show a green boost timer badge on buildings with active VIP/alliance boosts.</summary>
        private void CreateBoostTimerBadge(GameObject building, string boostType, float remainingSeconds)
        {
            // Remove existing boost badge if present
            var existing = building.transform.Find("BoostTimerBadge");
            if (existing != null) Destroy(existing.gameObject);

            if (remainingSeconds <= 0f) return;

            var badge = new GameObject("BoostTimerBadge");
            badge.transform.SetParent(building.transform, false);

            var rect = badge.AddComponent<RectTransform>();
            // Position at bottom-left corner of building
            rect.anchorMin = new Vector2(-0.05f, -0.05f);
            rect.anchorMax = new Vector2(0.40f, 0.12f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            Color badgeColor = boostType switch
            {
                "vip" => new Color(0.80f, 0.60f, 0.10f, 0.90f),     // Gold for VIP
                "alliance" => new Color(0.20f, 0.55f, 0.80f, 0.90f), // Blue for alliance
                "item" => new Color(0.20f, 0.70f, 0.30f, 0.90f),     // Green for item boost
                _ => new Color(0.30f, 0.65f, 0.30f, 0.90f)
            };
            bg.color = badgeColor;
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.30f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            // Timer text
            int secs = Mathf.CeilToInt(remainingSeconds);
            string timeStr = secs >= 3600
                ? $"{secs / 3600}h{(secs % 3600) / 60:D2}m"
                : secs >= 60 ? $"{secs / 60}m" : $"{secs}s";

            string icon = boostType switch
            {
                "vip" => "\u2B50",      // ⭐
                "alliance" => "\u2764", // ❤
                "item" => "\u26A1",     // ⚡
                _ => "\u2191"           // ↑
            };

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = $"{icon}{timeStr}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0, 0, 0, 0.8f);
            textOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Start fade-out coroutine to auto-destroy when boost expires
            StartCoroutine(AnimateBoostBadge(badge, bg, remainingSeconds));
        }

        private IEnumerator AnimateBoostBadge(GameObject badge, Image bg, float totalSeconds)
        {
            float remaining = totalSeconds;
            Color baseColor = bg.color;

            while (badge != null && remaining > 0f)
            {
                remaining -= Time.deltaTime;

                // Update timer text
                var text = badge.GetComponentInChildren<Text>();
                if (text != null)
                {
                    int secs = Mathf.CeilToInt(remaining);
                    string timeStr = secs >= 3600
                        ? $"{secs / 3600}h{(secs % 3600) / 60:D2}m"
                        : secs >= 60 ? $"{secs / 60}m" : $"{secs}s";
                    // Keep the icon (first char) and update time
                    string currentText = text.text;
                    string icon = currentText.Length > 0 ? currentText.Substring(0, 1) : "";
                    text.text = $"{icon}{timeStr}";
                }

                // Gentle pulse when < 60s remaining
                if (remaining < 60f)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                    bg.color = Color.Lerp(baseColor, new Color(1f, 0.3f, 0.2f, 0.95f), pulse * 0.4f);
                }

                yield return null;
            }

            if (badge != null) Destroy(badge);
        }

        // ====================================================================
        // P&C IT99: Resource Building Tap Collect All
        // ====================================================================

        /// <summary>P&C IT100: When tapping a resource building, collect from ALL same-type buildings.</summary>
        private void TapCollectAllForBuilding(string instanceId, string buildingId)
        {
            // Only resource buildings have bubbles
            if (buildingId != "grain_farm" && buildingId != "iron_mine" &&
                buildingId != "stone_quarry" && buildingId != "arcane_tower") return;

            var spawner = FindAnyObjectByType<ResourceBubbleSpawner>();
            if (spawner == null) return;

            // P&C: Collect from ALL buildings of the same type, not just the tapped one
            spawner.CollectAllForBuildingType(buildingId);

            // Count how many same-type buildings exist for toast
            int sameTypeCount = 0;
            foreach (var p in _placements)
                if (p.BuildingId == buildingId) sameTypeCount++;

            // Show collect toast near the tapped building
            foreach (var p in _placements)
            {
                if (p.InstanceId == instanceId && p.VisualGO != null)
                {
                    string countLabel = sameTypeCount > 1 ? $" (x{sameTypeCount})" : "";
                    ShowCollectToast(p.VisualGO, sameTypeCount, buildingId);
                    break;
                }
            }
        }

        /// <summary>P&C: Small toast showing collected resource count near building.</summary>
        private void ShowCollectToast(GameObject building, int count, string buildingId)
        {
            string resIcon = buildingId switch
            {
                "grain_farm" => "\uD83C\uDF3E",    // 🌾
                "iron_mine" => "\u2692",            // ⚒
                "stone_quarry" => "\u26F0",         // ⛰
                "arcane_tower" => "\u2728",         // ✨
                _ => "\u25CF"
            };

            Color toastColor = buildingId switch
            {
                "grain_farm" => new Color(0.35f, 0.70f, 0.20f, 0.90f),
                "iron_mine" => new Color(0.55f, 0.55f, 0.65f, 0.90f),
                "stone_quarry" => new Color(0.60f, 0.50f, 0.35f, 0.90f),
                "arcane_tower" => new Color(0.55f, 0.35f, 0.80f, 0.90f),
                _ => new Color(0.50f, 0.50f, 0.50f, 0.90f)
            };

            var toast = new GameObject("CollectToast");
            toast.transform.SetParent(building.transform, false);

            var rect = toast.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.85f);
            rect.anchorMax = new Vector2(0.85f, 1.10f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = toast.AddComponent<Image>();
            bg.color = toastColor;
            bg.raycastTarget = false;

            var outline = toast.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.30f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            AddInfoPanelText(toast.transform, "Text", $"+{count} {resIcon}",
                9, FontStyle.Bold, Color.white,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);

            StartCoroutine(AnimateCollectToast(toast));
        }

        private IEnumerator AnimateCollectToast(GameObject toast)
        {
            if (toast == null) yield break;
            var rect = toast.GetComponent<RectTransform>();
            var cg = toast.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            float duration = 1.2f;
            Vector2 startPos = rect.anchoredPosition;

            while (elapsed < duration && toast != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Float upward and fade out
                rect.anchoredPosition = startPos + new Vector2(0, t * 30f);
                cg.alpha = 1f - Mathf.Pow(t, 2);
                yield return null;
            }

            if (toast != null) Destroy(toast);
        }

        // ====================================================================
        // P&C IT100: Power Score Animation on Upgrade Completion
        // ====================================================================

        /// <summary>P&C: Flash the power rating HUD with a "+X" pop when a building upgrades.</summary>
        private void AnimatePowerIncrease(string buildingId, int newTier)
        {
            int powerGain = GetBuildingPowerContribution(buildingId, newTier) -
                            GetBuildingPowerContribution(buildingId, Mathf.Max(1, newTier - 1));
            if (powerGain <= 0) return;

            if (_builderCountHUD == null) return;
            var canvas = _builderCountHUD.transform.parent;
            if (canvas == null) return;

            // Floating "+X Power" text that rises from the HUD
            var floater = new GameObject("PowerFloater");
            floater.transform.SetParent(canvas, false);
            var fRect = floater.AddComponent<RectTransform>();
            // Position just above the builder HUD
            fRect.anchorMin = new Vector2(0.005f, 0.94f);
            fRect.anchorMax = new Vector2(0.185f, 0.98f);
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;

            var text = floater.AddComponent<Text>();
            text.text = $"+{powerGain} \u2694";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.20f);
            text.raycastTarget = false;

            var outline = floater.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.25f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            StartCoroutine(AnimatePowerFloater(floater, fRect));

            // Flash the HUD background green briefly
            if (_powerRatingText != null)
                StartCoroutine(FlashPowerHUD());
        }

        private IEnumerator AnimatePowerFloater(GameObject floater, RectTransform rect)
        {
            if (floater == null) yield break;
            var cg = floater.AddComponent<CanvasGroup>();
            Vector2 startMin = rect.anchorMin;
            Vector2 startMax = rect.anchorMax;
            float elapsed = 0f;
            float duration = 1.5f;

            while (elapsed < duration && floater != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float rise = t * 0.04f;
                rect.anchorMin = startMin + new Vector2(0, rise);
                rect.anchorMax = startMax + new Vector2(0, rise);
                cg.alpha = t < 0.3f ? 1f : 1f - ((t - 0.3f) / 0.7f);
                // Scale pulse at start
                float scale = t < 0.2f ? 1f + 0.3f * Mathf.Sin(t / 0.2f * Mathf.PI) : 1f;
                floater.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (floater != null) Destroy(floater);
        }

        private IEnumerator FlashPowerHUD()
        {
            if (_builderCountHUD == null) yield break;
            var bg = _builderCountHUD.GetComponent<Image>();
            if (bg == null) yield break;

            Color original = bg.color;
            Color flashColor = new Color(0.15f, 0.40f, 0.15f, 0.95f);
            float elapsed = 0f;

            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.6f;
                bg.color = Color.Lerp(flashColor, original, t);
                yield return null;
            }
            bg.color = original;
        }

        // ====================================================================
        // P&C IT100: Alliance Help Count in Upgrade Indicator
        // ====================================================================

        /// <summary>P&C: Add alliance help count badge to upgrade indicator (Help: X/5).</summary>
        private void AddAllianceHelpCountToIndicator(GameObject indicator, string instanceId)
        {
            if (indicator == null) return;
            var existing = indicator.transform.Find("HelpCount");
            if (existing != null) return; // Already has one

            int received = _allianceHelpsReceived.TryGetValue(instanceId, out var c) ? c : 0;

            var badge = new GameObject("HelpCount");
            badge.transform.SetParent(indicator.transform, false);
            var rect = badge.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, -0.35f);
            rect.anchorMax = new Vector2(0.60f, -0.05f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = badge.AddComponent<Image>();
            bool hasHelps = received > 0;
            bg.color = hasHelps
                ? new Color(0.15f, 0.40f, 0.65f, 0.85f)
                : new Color(0.12f, 0.10f, 0.20f, 0.75f);
            bg.raycastTarget = false;

            var outline = badge.AddComponent<Outline>();
            outline.effectColor = new Color(0.40f, 0.65f, 0.90f, 0.40f);
            outline.effectDistance = new Vector2(0.5f, -0.5f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(badge.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<Text>();
            text.text = $"\u2764 {received}/{MaxAllianceHelps}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = hasHelps ? new Color(0.80f, 0.90f, 1f) : new Color(0.50f, 0.50f, 0.60f);
            text.raycastTarget = false;
        }

        // ====================================================================
        // P&C IT100: Construction Speed Bonus Display in Detail Panel
        // ====================================================================

        /// <summary>P&C: Show active construction speed bonuses in the detail panel.</summary>
        private void AddConstructionSpeedBonusDisplay(Transform parent, string instanceId)
        {
            // Calculate total bonus percentage from various sources
            float totalBonus = 0f;
            var bonusSources = new List<string>();

            // VIP bonus (simulated — always 10% for demo)
            totalBonus += 10f;
            bonusSources.Add("VIP +10%");

            // Alliance help bonus
            int helps = _allianceHelpsReceived.TryGetValue(instanceId, out var h) ? h : 0;
            if (helps > 0)
            {
                float helpBonus = helps * 2f; // 2% per help
                totalBonus += helpBonus;
                bonusSources.Add($"Help +{helpBonus:F0}%");
            }

            // Research bonus (check ResearchManager for build cost reduction)
            if (ServiceLocator.TryGet<ResearchManager>(out var resM))
            {
                var bonuses = resM.Bonuses;
                if (bonuses != null && bonuses.BuildCostReductionPercent > 0f)
                {
                    totalBonus += bonuses.BuildCostReductionPercent;
                    bonusSources.Add($"Research +{bonuses.BuildCostReductionPercent:F0}%");
                }
            }

            if (totalBonus <= 0f) return;

            string bonusText = $"\u26A1 Build Speed: +{totalBonus:F0}% ({string.Join(", ", bonusSources)})";

            AddInfoPanelText(parent, "SpeedBonus", bonusText, 8, FontStyle.Normal,
                new Color(0.40f, 0.80f, 0.50f),
                new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.28f), TextAnchor.MiddleCenter);
        }

        // ====================================================================
        // P&C IT100: Upgrade Prerequisite Chain Visualization
        // ====================================================================

        /// <summary>P&C: Show prerequisite requirements as check/cross list in detail panel.</summary>
        private void AddPrerequisiteChainDisplay(Transform parent, string buildingId, string instanceId, int currentTier)
        {
            if (!ServiceLocator.TryGet<BuildingManager>(out var bm)) return;

            // Get stronghold level
            int strongholdTier = 0;
            foreach (var pb in bm.PlacedBuildings.Values)
            {
                if (pb.Data != null && pb.Data.buildingId == "stronghold")
                { strongholdTier = pb.CurrentTier; break; }
            }

            int requiredSHTier = currentTier + 1; // Next tier requires stronghold >= target tier
            bool shMet = buildingId == "stronghold" || strongholdTier >= requiredSHTier;

            // Check resources
            bool canAfford = true;
            if (bm.PlacedBuildings.TryGetValue(instanceId, out var placed) && placed.Data != null)
            {
                var nextTier = placed.Data.GetTier(currentTier);
                if (nextTier != null && ServiceLocator.TryGet<ResourceManager>(out var rm))
                {
                    canAfford = (nextTier.stoneCost <= 0 || rm.Stone >= nextTier.stoneCost) &&
                                (nextTier.ironCost <= 0 || rm.Iron >= nextTier.ironCost) &&
                                (nextTier.grainCost <= 0 || rm.Grain >= nextTier.grainCost) &&
                                (nextTier.arcaneEssenceCost <= 0 || rm.ArcaneEssence >= nextTier.arcaneEssenceCost);
                }
            }

            bool queueFree = bm.BuildQueue.Count < 2;

            // Build prereq list with check/cross marks
            string shIcon = shMet ? "\u2705" : "\u274C"; // ✅ or ❌
            string resIcon = canAfford ? "\u2705" : "\u274C";
            string qIcon = queueFree ? "\u2705" : "\u274C";

            string prereqLine;
            if (buildingId == "stronghold")
                prereqLine = $"{resIcon} Resources  {qIcon} Queue";
            else
                prereqLine = $"{shIcon} SH Lv.{requiredSHTier + 1}  {resIcon} Resources  {qIcon} Queue";

            Color lineColor = (shMet && canAfford && queueFree)
                ? new Color(0.40f, 0.80f, 0.45f) // All met = green
                : new Color(0.85f, 0.60f, 0.30f); // Some missing = amber

            AddInfoPanelText(parent, "PrereqChain", prereqLine, 8, FontStyle.Normal, lineColor,
                new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.24f), TextAnchor.MiddleCenter);
        }

        // ====================================================================
        // P&C IT101: Full-screen Upgrade Reward Summary Screen
        // ====================================================================

        /// <summary>
        /// P&C-style full-screen reward summary after upgrade completes.
        /// Shows building sprite, level change, stat deltas, unlocks, and Continue button.
        /// </summary>
        private void ShowUpgradeRewardScreen(string buildingId, string instanceId, int oldTier, int newTier)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Full-screen dark overlay
            var screen = new GameObject("UpgradeRewardScreen");
            screen.transform.SetParent(canvas.transform, false);
            screen.transform.SetAsLastSibling();
            var screenRect = screen.AddComponent<RectTransform>();
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = Vector2.zero;
            screenRect.offsetMax = Vector2.zero;
            var screenBg = screen.AddComponent<Image>();
            screenBg.color = new Color(0.02f, 0.01f, 0.05f, 0f); // Start transparent
            screenBg.raycastTarget = true;
            var screenCG = screen.AddComponent<CanvasGroup>();
            screenCG.alpha = 0f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string displayName = BuildingDisplayNames.TryGetValue(buildingId, out var dn) ? dn : buildingId;

            // --- Radial glow behind building ---
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(screen.transform, false);
            var glowRect = glowGO.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.15f, 0.45f);
            glowRect.anchorMax = new Vector2(0.85f, 0.85f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 0.85f, 0.30f, 0.15f);
            var glowSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");
            #if UNITY_EDITOR
            if (glowSpr == null)
                glowSpr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
            #endif
            if (glowSpr != null) glowImg.sprite = glowSpr;

            // --- "UPGRADE COMPLETE" header ---
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(screen.transform, false);
            var headerRect = headerGO.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.05f, 0.86f);
            headerRect.anchorMax = new Vector2(0.95f, 0.94f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;
            var headerText = headerGO.AddComponent<Text>();
            headerText.text = "UPGRADE COMPLETE";
            headerText.font = font;
            headerText.fontSize = 22;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = new Color(1f, 0.90f, 0.35f);
            headerText.raycastTarget = false;
            var headerOutline = headerGO.AddComponent<Outline>();
            headerOutline.effectColor = new Color(0.50f, 0.30f, 0.05f, 0.9f);
            headerOutline.effectDistance = new Vector2(2f, -2f);

            // --- Building sprite ---
            Sprite bldSprite = LoadBuildingSprite(buildingId, newTier);
            if (bldSprite != null)
            {
                var sprGO = new GameObject("BuildingSprite");
                sprGO.transform.SetParent(screen.transform, false);
                var sprRect = sprGO.AddComponent<RectTransform>();
                sprRect.anchorMin = new Vector2(0.28f, 0.56f);
                sprRect.anchorMax = new Vector2(0.72f, 0.86f);
                sprRect.offsetMin = Vector2.zero;
                sprRect.offsetMax = Vector2.zero;
                var sprImg = sprGO.AddComponent<Image>();
                sprImg.sprite = bldSprite;
                sprImg.preserveAspect = true;
                sprImg.raycastTarget = false;
            }

            // --- Building name + level change ---
            var nameGO = new GameObject("NameLevel");
            nameGO.transform.SetParent(screen.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.05f, 0.50f);
            nameRect.anchorMax = new Vector2(0.95f, 0.58f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<Text>();
            string starOld = oldTier > 1 ? $"\u2605" : "";
            string starNew = newTier > 1 ? $"\u2605" : "";
            nameText.text = $"{displayName}  {starOld}Lv.{oldTier} \u2192 {starNew}Lv.{newTier}";
            nameText.font = font;
            nameText.fontSize = 16;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            var nameShadow = nameGO.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.9f);
            nameShadow.effectDistance = new Vector2(1f, -1f);

            // --- Stat delta rows ---
            int oldPower = GetBuildingPowerContribution(buildingId, oldTier);
            int newPower = GetBuildingPowerContribution(buildingId, newTier);
            int deltaPower = newPower - oldPower;

            float rowY = 0.46f;
            float rowH = 0.055f;

            // Power row (always shown)
            CreateRewardStatRow(screen.transform, font, "\u2694 Power", $"{oldPower:N0}", $"{newPower:N0}",
                $"+{deltaPower:N0}", new Color(0.95f, 0.70f, 0.35f), rowY);
            rowY -= rowH;

            // Production rate (resource buildings)
            if (buildingId == "grain_farm" || buildingId == "iron_mine" ||
                buildingId == "stone_quarry" || buildingId == "arcane_tower")
            {
                int oldRate = (oldTier + 1) * 250;
                int newRate = (newTier + 1) * 250;
                string resName = buildingId switch
                {
                    "grain_farm" => "Grain",
                    "iron_mine" => "Iron",
                    "stone_quarry" => "Stone",
                    _ => "Arcane"
                };
                Color resColor = buildingId switch
                {
                    "grain_farm" => new Color(1f, 0.92f, 0.45f),
                    "iron_mine" => new Color(0.78f, 0.80f, 0.90f),
                    "stone_quarry" => new Color(0.85f, 0.82f, 0.76f),
                    _ => new Color(0.80f, 0.55f, 1f)
                };
                CreateRewardStatRow(screen.transform, font, $"\u2609 {resName}/hr", $"{oldRate}", $"{newRate}",
                    $"+{newRate - oldRate}", resColor, rowY);
                rowY -= rowH;
            }

            // Troop capacity (barracks/training)
            if (buildingId == "barracks")
            {
                int oldCap = (oldTier + 1) * 500;
                int newCap = (newTier + 1) * 500;
                CreateRewardStatRow(screen.transform, font, "\u2694 Troop Cap", $"{oldCap}", $"{newCap}",
                    $"+{newCap - oldCap}", new Color(0.80f, 0.35f, 0.35f), rowY);
                rowY -= rowH;
            }
            else if (buildingId == "training_ground")
            {
                int oldCap = (oldTier + 1) * 300;
                int newCap = (newTier + 1) * 300;
                CreateRewardStatRow(screen.transform, font, "\u2694 Drill Cap", $"{oldCap}", $"{newCap}",
                    $"+{newCap - oldCap}", new Color(0.80f, 0.55f, 0.35f), rowY);
                rowY -= rowH;
            }

            // Research speed (academy/library)
            if (buildingId == "academy" || buildingId == "library")
            {
                int oldPct = oldTier * 10;
                int newPct = newTier * 10;
                CreateRewardStatRow(screen.transform, font, "\u2609 Research Spd", $"+{oldPct}%", $"+{newPct}%",
                    $"+{newPct - oldPct}%", new Color(0.45f, 0.75f, 0.95f), rowY);
                rowY -= rowH;
            }

            // --- Unlocks section (stronghold only) ---
            if (buildingId == "stronghold")
            {
                rowY -= 0.01f;
                var unlockHeader = new GameObject("UnlockHeader");
                unlockHeader.transform.SetParent(screen.transform, false);
                var uhRect = unlockHeader.AddComponent<RectTransform>();
                uhRect.anchorMin = new Vector2(0.10f, rowY - 0.03f);
                uhRect.anchorMax = new Vector2(0.90f, rowY);
                uhRect.offsetMin = Vector2.zero;
                uhRect.offsetMax = Vector2.zero;
                var uhText = unlockHeader.AddComponent<Text>();
                uhText.text = "NEW UNLOCKS";
                uhText.font = font;
                uhText.fontSize = 12;
                uhText.fontStyle = FontStyle.Bold;
                uhText.alignment = TextAnchor.MiddleCenter;
                uhText.color = new Color(0.45f, 0.90f, 0.50f);
                uhText.raycastTarget = false;
                rowY -= 0.04f;

                // Find buildings that unlock at this new stronghold level
                string[] allBuildingTypes = { "grain_farm", "iron_mine", "stone_quarry", "barracks",
                    "wall", "watch_tower", "marketplace", "academy", "training_ground", "forge",
                    "arcane_tower", "guild_hall", "armory", "laboratory", "embassy", "enchanting_tower",
                    "library", "hero_shrine", "observatory", "archive" };

                foreach (var bt in allBuildingTypes)
                {
                    int unlockLevel = GetBuildingUnlockLevel(bt);
                    if (unlockLevel == newTier)
                    {
                        string bName = BuildingDisplayNames.TryGetValue(bt, out var n) ? n : bt;
                        var unlockGO = new GameObject($"Unlock_{bt}");
                        unlockGO.transform.SetParent(screen.transform, false);
                        var uRect = unlockGO.AddComponent<RectTransform>();
                        uRect.anchorMin = new Vector2(0.15f, rowY - 0.03f);
                        uRect.anchorMax = new Vector2(0.85f, rowY);
                        uRect.offsetMin = Vector2.zero;
                        uRect.offsetMax = Vector2.zero;
                        var uText = unlockGO.AddComponent<Text>();
                        uText.text = $"\u2728 {bName}";
                        uText.font = font;
                        uText.fontSize = 11;
                        uText.alignment = TextAnchor.MiddleCenter;
                        uText.color = new Color(0.80f, 0.95f, 0.80f);
                        uText.raycastTarget = false;
                        rowY -= 0.035f;
                    }
                }
            }

            // --- Gold separator line ---
            var sepGO = new GameObject("Separator");
            sepGO.transform.SetParent(screen.transform, false);
            var sepRect = sepGO.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.15f, rowY - 0.003f);
            sepRect.anchorMax = new Vector2(0.85f, rowY);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sepGO.AddComponent<Image>();
            sepImg.color = new Color(0.85f, 0.65f, 0.15f, 0.40f);
            sepImg.raycastTarget = false;

            // --- Continue button ---
            float btnY = Mathf.Max(rowY - 0.06f, 0.04f);
            var btnGO = new GameObject("ContinueBtn");
            btnGO.transform.SetParent(screen.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.25f, btnY);
            btnRect.anchorMax = new Vector2(0.75f, btnY + 0.06f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            var btnBg = btnGO.AddComponent<Image>();
            btnBg.color = new Color(0.20f, 0.55f, 0.25f, 0.95f);
            btnBg.raycastTarget = true;
            var btnOutline = btnGO.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.45f, 0.85f, 0.50f, 0.6f);
            btnOutline.effectDistance = new Vector2(1f, -1f);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(() =>
            {
                if (screen != null) Destroy(screen);
            });

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRect = btnTextGO.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnText = btnTextGO.AddComponent<Text>();
            btnText.text = "Continue";
            btnText.font = font;
            btnText.fontSize = 16;
            btnText.fontStyle = FontStyle.Bold;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.raycastTarget = false;

            // Animate fade-in
            StartCoroutine(AnimateRewardScreenIn(screen, screenBg, screenCG));
        }

        /// <summary>Helper: creates a stat delta row in the reward screen.</summary>
        private void CreateRewardStatRow(Transform parent, Font font, string label,
            string oldVal, string newVal, string delta, Color accentColor, float yCenter)
        {
            float rowH = 0.04f;
            float yMin = yCenter - rowH * 0.5f;
            float yMax = yCenter + rowH * 0.5f;

            // Row background
            var rowBG = new GameObject($"StatRow_{label}");
            rowBG.transform.SetParent(parent, false);
            var rowRect = rowBG.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.08f, yMin);
            rowRect.anchorMax = new Vector2(0.92f, yMax);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;
            var rowImg = rowBG.AddComponent<Image>();
            rowImg.color = new Color(0.08f, 0.06f, 0.14f, 0.70f);
            rowImg.raycastTarget = false;

            // Label (left)
            AddInfoPanelText(rowBG.transform, "Label", label, 11, FontStyle.Bold,
                new Color(0.80f, 0.78f, 0.72f),
                new Vector2(0.02f, 0f), new Vector2(0.35f, 1f), TextAnchor.MiddleLeft);

            // Old value (center-left, dimmed)
            AddInfoPanelText(rowBG.transform, "OldVal", oldVal, 11, FontStyle.Normal,
                new Color(0.55f, 0.52f, 0.48f),
                new Vector2(0.36f, 0f), new Vector2(0.52f, 1f), TextAnchor.MiddleRight);

            // Arrow
            AddInfoPanelText(rowBG.transform, "Arrow", "\u2192", 11, FontStyle.Normal,
                new Color(0.65f, 0.60f, 0.50f),
                new Vector2(0.53f, 0f), new Vector2(0.58f, 1f), TextAnchor.MiddleCenter);

            // New value (center-right, bright)
            AddInfoPanelText(rowBG.transform, "NewVal", newVal, 12, FontStyle.Bold,
                Color.white,
                new Vector2(0.59f, 0f), new Vector2(0.75f, 1f), TextAnchor.MiddleLeft);

            // Delta (right, accent color)
            AddInfoPanelText(rowBG.transform, "Delta", delta, 12, FontStyle.Bold,
                accentColor,
                new Vector2(0.76f, 0f), new Vector2(0.98f, 1f), TextAnchor.MiddleRight);
        }

        /// <summary>Animate the reward screen fading in + building sprite scaling up.</summary>
        private IEnumerator AnimateRewardScreenIn(GameObject screen, Image bg, CanvasGroup cg)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration && screen != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = 1f - (1f - t) * (1f - t); // ease-out quadratic
                cg.alpha = ease;
                bg.color = new Color(0.02f, 0.01f, 0.05f, 0.92f * ease);

                // Scale the building sprite from 0.6 to 1.0
                var spriteTransform = screen.transform.Find("BuildingSprite");
                if (spriteTransform != null)
                    spriteTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, ease);

                yield return null;
            }
            if (cg != null)
            {
                cg.alpha = 1f;
                bg.color = new Color(0.02f, 0.01f, 0.05f, 0.92f);
            }
        }

        // ====================================================================
        // P&C IT101: Recommended Upgrade Advisor Arrow on World Buildings
        // ====================================================================

        private GameObject _advisorArrowGO;

        /// <summary>
        /// P&C-style pulsing green "Recommended" arrow above the building the advisor
        /// suggests upgrading next. Refreshes after any upgrade completes or queue changes.
        /// </summary>
        private void RefreshAdvisorArrow()
        {
            // Clean up old arrow
            if (_advisorArrowGO != null) Destroy(_advisorArrowGO);
            _advisorArrowGO = null;

            if (buildingContainer == null) return;

            var suggestion = GetUpgradeAdvisorSuggestion();
            if (!suggestion.HasValue) return;

            var sug = suggestion.Value;

            // Find the building visual
            CityBuildingPlacement target = null;
            foreach (var p in _placements)
            {
                if (p.InstanceId == sug.InstanceId) { target = p; break; }
            }
            if (target?.VisualGO == null) return;

            var building = target.VisualGO;
            var buildingRect = building.GetComponent<RectTransform>();
            if (buildingRect == null) return;

            // Create advisor arrow container as sibling of building (in buildingContainer)
            _advisorArrowGO = new GameObject("AdvisorArrow");
            _advisorArrowGO.transform.SetParent(building.transform, false);
            _advisorArrowGO.transform.SetAsLastSibling();

            // Arrow pointing down (triangle using rotated square)
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(_advisorArrowGO.transform, false);
            var arrowRect = arrowGO.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.40f, 1.05f);
            arrowRect.anchorMax = new Vector2(0.60f, 1.25f);
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;
            arrowRect.localRotation = Quaternion.Euler(0, 0, 45f);
            var arrowImg = arrowGO.AddComponent<Image>();
            arrowImg.color = new Color(0.30f, 0.85f, 0.40f, 0.90f);
            arrowImg.raycastTarget = false;

            // "Recommended" pill label above arrow
            var pillGO = new GameObject("RecommendedPill");
            pillGO.transform.SetParent(_advisorArrowGO.transform, false);
            var pillRect = pillGO.AddComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(0.05f, 1.25f);
            pillRect.anchorMax = new Vector2(0.95f, 1.45f);
            pillRect.offsetMin = Vector2.zero;
            pillRect.offsetMax = Vector2.zero;
            var pillBg = pillGO.AddComponent<Image>();
            pillBg.color = new Color(0.15f, 0.45f, 0.20f, 0.90f);
            pillBg.raycastTarget = false;
            var pillOutline = pillGO.AddComponent<Outline>();
            pillOutline.effectColor = new Color(0.40f, 0.90f, 0.45f, 0.50f);
            pillOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var pillTextGO = new GameObject("Text");
            pillTextGO.transform.SetParent(pillGO.transform, false);
            var ptRect = pillTextGO.AddComponent<RectTransform>();
            ptRect.anchorMin = Vector2.zero;
            ptRect.anchorMax = Vector2.one;
            ptRect.offsetMin = Vector2.zero;
            ptRect.offsetMax = Vector2.zero;
            var pillText = pillTextGO.AddComponent<Text>();
            string sugName = BuildingDisplayNames.TryGetValue(sug.BuildingId, out var n) ? n : sug.BuildingId;
            pillText.text = $"\u2728 {sugName}";
            pillText.font = font;
            pillText.fontSize = 8;
            pillText.fontStyle = FontStyle.Bold;
            pillText.alignment = TextAnchor.MiddleCenter;
            pillText.color = new Color(0.80f, 1f, 0.80f);
            pillText.raycastTarget = false;

            // Animate: pulse up/down + glow
            StartCoroutine(AnimateAdvisorArrow(_advisorArrowGO, arrowImg));
        }

        /// <summary>Infinite pulse animation for the advisor arrow.</summary>
        private IEnumerator AnimateAdvisorArrow(GameObject container, Image arrowImg)
        {
            float elapsed = 0f;
            while (container != null)
            {
                elapsed += Time.deltaTime;
                // Bob up and down
                float bob = Mathf.Sin(elapsed * 3f) * 3f;
                container.transform.localPosition = new Vector3(0, bob, 0);

                // Pulse alpha on arrow
                if (arrowImg != null)
                {
                    float pulse = 0.70f + 0.25f * Mathf.Sin(elapsed * 4f);
                    arrowImg.color = new Color(0.30f, 0.85f, 0.40f, pulse);
                }

                yield return null;
            }
        }

        // ====================================================================
        // P&C IT103: Resource Bar Tap → Production Breakdown
        // ====================================================================

        /// <summary>
        /// P&C IT103: Find resource icons in the Canvas hierarchy and wire tap handlers
        /// to open production breakdown popups.
        /// </summary>
        private void WireResourceBarTapHandlers()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            string[] resourceNames = { "Grain", "Iron", "Stone", "Arcane" };
            foreach (var resName in resourceNames)
            {
                // Search for Icon_<ResName> in the canvas hierarchy
                var icon = FindDeepChild(canvas.transform, $"Icon_{resName}");
                if (icon == null) continue;

                // Add Button if not already present
                var btn = icon.GetComponent<Button>();
                if (btn == null)
                {
                    var img = icon.GetComponent<Image>();
                    if (img != null) img.raycastTarget = true;
                    btn = icon.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                }

                string capturedName = resName;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ShowResourceBreakdownPopup(capturedName));

                // Also wire the amount text next to the icon
                var amtGO = FindDeepChild(canvas.transform, $"{resName}Amt");
                if (amtGO != null)
                {
                    var amtBtn = amtGO.GetComponent<Button>();
                    if (amtBtn == null)
                    {
                        // Use existing Graphic (Text or Image) as target — don't add a second Graphic
                        var amtGraphic = amtGO.GetComponent<UnityEngine.UI.Graphic>();
                        if (amtGraphic != null) amtGraphic.raycastTarget = true;
                        amtBtn = amtGO.gameObject.AddComponent<Button>();
                        amtBtn.targetGraphic = amtGraphic;
                    }
                    amtBtn.onClick.RemoveAllListeners();
                    amtBtn.onClick.AddListener(() => ShowResourceBreakdownPopup(capturedName));
                }
            }
        }

        /// <summary>Recursive deep child search by name.</summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                var found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        // ====================================================================
        // P&C IT103: Zoom-Level Building Simplification
        // ====================================================================

        private const float ZoomSimplifyThreshold = 0.65f;
        private bool _buildingsSimplified;

        /// <summary>
        /// P&C IT103: When zoomed out past threshold, swap building sprites to
        /// simplified category-colored circle markers for readability.
        /// </summary>
        private void UpdateBuildingSimplification()
        {
            bool shouldSimplify = _currentZoom < ZoomSimplifyThreshold;
            if (shouldSimplify == _buildingsSimplified) return;

            _buildingsSimplified = shouldSimplify;

            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;

                var marker = p.VisualGO.transform.Find("ZoomMarker");
                var mainImg = p.VisualGO.GetComponent<Image>();

                if (shouldSimplify)
                {
                    // Hide main sprite, show simplified marker
                    if (mainImg != null) mainImg.color = new Color(1, 1, 1, 0.15f);

                    // Hide labels and badges
                    SetChildrenAlpha(p.VisualGO.transform, 0.15f, "ZoomMarker");

                    if (marker == null)
                    {
                        // Create simplified marker
                        var markerGO = new GameObject("ZoomMarker");
                        markerGO.transform.SetParent(p.VisualGO.transform, false);
                        markerGO.transform.SetAsLastSibling();
                        var mRect = markerGO.AddComponent<RectTransform>();
                        mRect.anchorMin = new Vector2(0.20f, 0.30f);
                        mRect.anchorMax = new Vector2(0.80f, 0.80f);
                        mRect.offsetMin = Vector2.zero;
                        mRect.offsetMax = Vector2.zero;

                        Color catColor = GetBuildingCategoryGlowColor(p.BuildingId);
                        // Use brighter version of the category color
                        catColor = new Color(
                            Mathf.Min(1f, catColor.r * 4f),
                            Mathf.Min(1f, catColor.g * 4f),
                            Mathf.Min(1f, catColor.b * 4f),
                            0.90f);

                        var mImg = markerGO.AddComponent<Image>();
                        mImg.raycastTarget = false;
                        mImg.color = catColor;

                        // Use radial gradient for soft dot
                        var spr = Resources.Load<Sprite>("UI/Production/radial_gradient");
                        #if UNITY_EDITOR
                        if (spr == null)
                            spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Production/radial_gradient.png");
                        #endif
                        if (spr != null) mImg.sprite = spr;

                        // Category icon label
                        string icon = GetBuildingCategoryIcon(p.BuildingId);
                        var iconGO = new GameObject("Icon");
                        iconGO.transform.SetParent(markerGO.transform, false);
                        var iconRect = iconGO.AddComponent<RectTransform>();
                        iconRect.anchorMin = Vector2.zero;
                        iconRect.anchorMax = Vector2.one;
                        iconRect.offsetMin = Vector2.zero;
                        iconRect.offsetMax = Vector2.zero;
                        var iconText = iconGO.AddComponent<Text>();
                        iconText.text = icon;
                        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        iconText.fontSize = 14;
                        iconText.alignment = TextAnchor.MiddleCenter;
                        iconText.color = Color.white;
                        iconText.raycastTarget = false;
                    }
                    else
                    {
                        marker.gameObject.SetActive(true);
                    }
                }
                else
                {
                    // Restore main sprite and hide marker
                    if (mainImg != null) mainImg.color = Color.white;
                    SetChildrenAlpha(p.VisualGO.transform, 1f, "ZoomMarker");
                    if (marker != null) marker.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Set alpha on all children except the named exclusion.</summary>
        private static void SetChildrenAlpha(Transform parent, float alpha, string exclude)
        {
            foreach (Transform child in parent)
            {
                if (child.name == exclude) continue;
                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }

        /// <summary>P&C IT103: Category emoji icon for simplified zoom markers.</summary>
        private static string GetBuildingCategoryIcon(string buildingId) => buildingId switch
        {
            "stronghold" => "\u265B", // Crown
            "barracks" or "training_ground" or "armory" => "\u2694", // Swords
            "grain_farm" or "iron_mine" or "stone_quarry" => "\u2692", // Hammers (resource)
            "arcane_tower" or "enchanting_tower" or "observatory" or "laboratory" => "\u2726", // Star
            "wall" or "watch_tower" => "\u26E8", // Shield
            "marketplace" or "guild_hall" or "embassy" => "\u2605", // Star
            "academy" or "library" or "archive" => "\u2710", // Book
            "hero_shrine" => "\u2661", // Heart
            "forge" => "\u2668", // Flame
            _ => "\u25CF" // Dot
        };

        // ====================================================================
        // P&C IT103: Speed-Up Rush Visual Effect
        // ====================================================================

        /// <summary>
        /// P&C IT103: Dramatic fast-forward swirl effect when a speed-up is applied.
        /// Clock hands spinning + ">>>" text + golden particles.
        /// </summary>
        private void PlaySpeedUpRushEffect(GameObject building)
        {
            if (building == null) return;
            StartCoroutine(SpeedUpRushCoroutine(building));
        }

        private IEnumerator SpeedUpRushCoroutine(GameObject building)
        {
            if (building == null) yield break;

            var container = building.transform.parent;
            if (container == null) yield break;

            var buildingRect = building.GetComponent<RectTransform>();
            Vector2 center = buildingRect != null
                ? buildingRect.anchoredPosition + new Vector2(0, buildingRect.sizeDelta.y * 0.3f)
                : Vector2.zero;

            // --- Fast-forward text ">>>" ---
            var ffGO = new GameObject("RushText");
            ffGO.transform.SetParent(container, false);
            ffGO.transform.SetAsLastSibling();
            var ffRect = ffGO.AddComponent<RectTransform>();
            ffRect.anchoredPosition = center + new Vector2(0, 20);
            ffRect.sizeDelta = new Vector2(80, 30);
            var ffText = ffGO.AddComponent<Text>();
            ffText.text = "\u25B6\u25B6\u25B6";
            ffText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ffText.fontSize = 20;
            ffText.fontStyle = FontStyle.Bold;
            ffText.alignment = TextAnchor.MiddleCenter;
            ffText.color = new Color(1f, 0.85f, 0.30f);
            ffText.raycastTarget = false;
            var ffOutline = ffGO.AddComponent<Outline>();
            ffOutline.effectColor = new Color(0.5f, 0.3f, 0f, 0.9f);
            ffOutline.effectDistance = new Vector2(1.5f, -1.5f);
            var ffCG = ffGO.AddComponent<CanvasGroup>();

            // --- Spinning clock hands (4 lines rotating) ---
            int handCount = 4;
            var hands = new List<(RectTransform rect, CanvasGroup cg)>();
            for (int i = 0; i < handCount; i++)
            {
                var handGO = new GameObject($"ClockHand_{i}");
                handGO.transform.SetParent(container, false);
                handGO.transform.SetAsLastSibling();
                var hRect = handGO.AddComponent<RectTransform>();
                hRect.anchoredPosition = center;
                hRect.sizeDelta = new Vector2(3, 20);
                hRect.pivot = new Vector2(0.5f, 0f);
                float startAngle = (i / (float)handCount) * 360f;
                hRect.localRotation = Quaternion.Euler(0, 0, startAngle);
                var hImg = handGO.AddComponent<Image>();
                hImg.color = new Color(1f, 0.90f, 0.40f, 0.80f);
                hImg.raycastTarget = false;
                var hCG = handGO.AddComponent<CanvasGroup>();
                hands.Add((hRect, hCG));
            }

            // --- Animate ---
            float duration = 0.7f;
            float elapsed = 0f;
            float spinSpeed = 720f; // degrees per second

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // >>> text rises and fades
                if (ffGO != null)
                {
                    ffRect.anchoredPosition = center + new Vector2(0, 20 + t * 30);
                    ffCG.alpha = t < 0.3f ? t / 0.3f : 1f - ((t - 0.3f) / 0.7f);
                    ffRect.localScale = Vector3.one * (1f + t * 0.3f);
                }

                // Clock hands spin fast and fade out
                float angle = spinSpeed * elapsed;
                foreach (var (rect, cg) in hands)
                {
                    if (rect == null) continue;
                    float baseAngle = hands.IndexOf((rect, cg)) * (360f / handCount);
                    rect.localRotation = Quaternion.Euler(0, 0, baseAngle + angle);
                    cg.alpha = 1f - t;
                    rect.sizeDelta = new Vector2(3, Mathf.Lerp(20, 35, t));
                }

                yield return null;
            }

            // Cleanup
            if (ffGO != null) Destroy(ffGO);
            foreach (var (rect, _) in hands)
                if (rect != null) Destroy(rect.gameObject);
        }
    }

    // ====================================================================
    // P&C: Left-side Queue Status Panel (Build slots + Research slot)
    // Shows individual builder/research queue slots like P&C's left column
    // ====================================================================

    public partial class CityGridView
    {
        private GameObject _queueStatusPanel;
        private readonly List<QueueSlotUI> _queueSlots = new();
        private float _queueSlotRefreshTimer;
        private const float QueueSlotRefreshInterval = 1f; // Update every second for timers

        private class QueueSlotUI
        {
            public GameObject Root;
            public Text LabelText;   // "Build" or "Research"
            public Text StatusText;  // "IDLE" or building name
            public Text TimerText;   // countdown timer
            public Image ProgressFill;
            public Image IconImage;
            public string SlotType;  // "build" or "research"
            public int SlotIndex;    // 0 or 1 for build slots
        }

        private void CreateQueueStatusPanel()
        {
            if (_queueStatusPanel != null) return;

            Transform canvasRoot = transform;
            while (canvasRoot.parent != null && canvasRoot.parent.GetComponent<Canvas>() != null)
                canvasRoot = canvasRoot.parent;

            _queueStatusPanel = new GameObject("QueueStatusPanel");
            _queueStatusPanel.transform.SetParent(canvasRoot, false);
            _queueStatusPanel.transform.SetAsLastSibling();

            // Position on left side, below info panel area
            var panelRect = _queueStatusPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.005f, 0.58f);
            panelRect.anchorMax = new Vector2(0.22f, 0.86f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Create 3 slots: Build 1, Build 2, Research
            var slotDefs = new (string type, string label, string icon, int index)[]
            {
                ("build", "Build", "\u2692", 0),    // ⚒ hammer
                ("build", "Build", "\u2692", 1),    // ⚒ hammer
                ("research", "Research", "\u2697", 0), // ⚗ alembic
            };

            float slotH = 1f / slotDefs.Length;
            for (int i = 0; i < slotDefs.Length; i++)
            {
                var def = slotDefs[i];
                float yTop = 1f - i * slotH;
                float yBot = yTop - slotH + 0.01f; // small gap

                var slot = CreateQueueSlot(_queueStatusPanel.transform, def.type, def.label,
                    def.icon, def.index, new Vector2(0, yBot), new Vector2(1, yTop));
                _queueSlots.Add(slot);
            }

            RefreshQueueSlots();
        }

        private QueueSlotUI CreateQueueSlot(Transform parent, string slotType, string label,
            string icon, int slotIndex, Vector2 anchorMin, Vector2 anchorMax)
        {
            var slot = new QueueSlotUI { SlotType = slotType, SlotIndex = slotIndex };

            var go = new GameObject($"QueueSlot_{label}_{slotIndex}");
            go.transform.SetParent(parent, false);
            slot.Root = go;

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Dark background with gold border
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.14f, 0.92f);
            bg.raycastTarget = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.58f, 0.18f, 0.70f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Button — tappable to open queue panel
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            string capType = slotType;
            int capIdx = slotIndex;
            btn.onClick.AddListener(() => OnQueueSlotTapped(capType, capIdx));

            // --- Icon on left ---
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.02f, 0.10f);
            iconRect.anchorMax = new Vector2(0.20f, 0.90f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            // Icon circle background
            var iconBg = iconGO.AddComponent<Image>();
            iconBg.color = slotType == "research"
                ? new Color(0.20f, 0.35f, 0.55f, 0.90f)
                : new Color(0.45f, 0.30f, 0.12f, 0.90f);
            iconBg.raycastTarget = false;
            slot.IconImage = iconBg;

            var iconTextGO = new GameObject("IconText");
            iconTextGO.transform.SetParent(iconGO.transform, false);
            var iconTextRect = iconTextGO.AddComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.offsetMin = Vector2.zero;
            iconTextRect.offsetMax = Vector2.zero;
            var iconText = iconTextGO.AddComponent<Text>();
            iconText.text = icon;
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 14;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = new Color(0.95f, 0.85f, 0.45f);
            iconText.raycastTarget = false;

            // --- Label text (top-right area): "Build" or "Research" ---
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.22f, 0.50f);
            labelRect.anchorMax = new Vector2(0.98f, 0.95f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            slot.LabelText = labelGO.AddComponent<Text>();
            slot.LabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.LabelText.fontSize = 10;
            slot.LabelText.fontStyle = FontStyle.Bold;
            slot.LabelText.alignment = TextAnchor.MiddleLeft;
            slot.LabelText.color = new Color(0.90f, 0.82f, 0.55f);
            slot.LabelText.text = label;
            slot.LabelText.raycastTarget = false;

            var labelShadow = labelGO.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.8f);
            labelShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // --- Status text (bottom-right area): "IDLE" or building name ---
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(go.transform, false);
            var statusRect = statusGO.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.22f, 0.05f);
            statusRect.anchorMax = new Vector2(0.65f, 0.52f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            slot.StatusText = statusGO.AddComponent<Text>();
            slot.StatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.StatusText.fontSize = 8;
            slot.StatusText.alignment = TextAnchor.MiddleLeft;
            slot.StatusText.color = new Color(0.70f, 0.70f, 0.70f);
            slot.StatusText.text = "IDLE";
            slot.StatusText.raycastTarget = false;

            // --- Timer text (bottom-right): countdown ---
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(go.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.65f, 0.05f);
            timerRect.anchorMax = new Vector2(0.98f, 0.52f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;

            slot.TimerText = timerGO.AddComponent<Text>();
            slot.TimerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            slot.TimerText.fontSize = 8;
            slot.TimerText.fontStyle = FontStyle.Bold;
            slot.TimerText.alignment = TextAnchor.MiddleRight;
            slot.TimerText.color = new Color(0.45f, 0.85f, 0.45f); // green timer
            slot.TimerText.text = "";
            slot.TimerText.raycastTarget = false;

            // --- Progress bar at very bottom ---
            var barBg = new GameObject("ProgressBg");
            barBg.transform.SetParent(go.transform, false);
            var barBgRect = barBg.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.02f, 0f);
            barBgRect.anchorMax = new Vector2(0.98f, 0.08f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.12f, 0.20f, 0.80f);
            barBgImg.raycastTarget = false;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(barBg.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // starts empty
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slot.ProgressFill = fill.AddComponent<Image>();
            slot.ProgressFill.color = new Color(0.40f, 0.80f, 0.35f, 0.95f);
            slot.ProgressFill.raycastTarget = false;

            return slot;
        }

        private void RefreshQueueSlots()
        {
            ServiceLocator.TryGet<BuildingManager>(out var bm);
            ServiceLocator.TryGet<ResearchManager>(out var rm);

            foreach (var slot in _queueSlots)
            {
                if (slot.Root == null) continue;

                if (slot.SlotType == "build")
                {
                    RefreshBuildSlot(slot, bm);
                }
                else if (slot.SlotType == "research")
                {
                    RefreshResearchSlot(slot, rm);
                }
            }
        }

        private void RefreshBuildSlot(QueueSlotUI slot, BuildingManager bm)
        {
            if (bm == null || bm.BuildQueue.Count <= slot.SlotIndex)
            {
                // IDLE state
                slot.StatusText.text = "IDLE";
                slot.StatusText.color = new Color(0.55f, 0.75f, 0.45f); // soft green
                slot.TimerText.text = "";
                slot.ProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                slot.IconImage.color = new Color(0.45f, 0.30f, 0.12f, 0.90f);
                return;
            }

            var entry = bm.BuildQueue[slot.SlotIndex];
            // Active build — show building name + timer
            string displayName = FormatBuildingDisplayName(entry.PlacedId);
            slot.StatusText.text = displayName;
            slot.StatusText.color = new Color(0.95f, 0.90f, 0.70f); // warm white

            int secs = Mathf.CeilToInt(entry.RemainingSeconds);
            slot.TimerText.text = FormatQueueTimer(secs);
            slot.TimerText.color = secs <= 300
                ? new Color(0.45f, 0.85f, 0.45f) // green when almost done
                : new Color(0.85f, 0.75f, 0.40f); // gold otherwise

            // Progress
            float totalTime = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds + entry.RemainingSeconds;
            float progress = totalTime > 0 ? 1f - (entry.RemainingSeconds / totalTime) : 0f;
            progress = Mathf.Clamp01(progress);
            slot.ProgressFill.rectTransform.anchorMax = new Vector2(progress, 1f);

            // Active icon tint
            slot.IconImage.color = new Color(0.65f, 0.45f, 0.15f, 0.95f);
        }

        private void RefreshResearchSlot(QueueSlotUI slot, ResearchManager rm)
        {
            if (rm == null || rm.ResearchQueue.Count == 0)
            {
                slot.StatusText.text = "IDLE";
                slot.StatusText.color = new Color(0.45f, 0.60f, 0.80f); // soft blue
                slot.TimerText.text = "";
                slot.ProgressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
                slot.IconImage.color = new Color(0.20f, 0.35f, 0.55f, 0.90f);
                return;
            }

            var active = rm.ResearchQueue[0];
            // Get display name from node data
            var nodeData = rm.GetNode(active.NodeId);
            string displayName = nodeData != null ? nodeData.displayName : active.NodeId;
            if (string.IsNullOrEmpty(displayName)) displayName = active.NodeId;
            slot.StatusText.text = displayName;
            slot.StatusText.color = new Color(0.80f, 0.88f, 0.95f);

            int secs = Mathf.CeilToInt(active.RemainingSeconds);
            slot.TimerText.text = FormatQueueTimer(secs);
            slot.TimerText.color = secs <= 300
                ? new Color(0.45f, 0.85f, 0.45f)
                : new Color(0.55f, 0.75f, 0.90f);

            // Estimate progress (we don't store total time, so use remaining as indicator)
            // Show a pulsing bar when active but progress unknown
            float progress = secs > 0 ? Mathf.PingPong(Time.time * 0.15f, 0.5f) + 0.1f : 1f;
            slot.ProgressFill.rectTransform.anchorMax = new Vector2(progress, 1f);

            slot.IconImage.color = new Color(0.30f, 0.50f, 0.75f, 0.95f);
        }

        private string FormatBuildingDisplayName(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return "Building";
            // Remove trailing _0, _1 etc and format nicely
            int lastUnderscore = instanceId.LastIndexOf('_');
            string buildingId = lastUnderscore > 0 ? instanceId.Substring(0, lastUnderscore) : instanceId;
            // Convert snake_case to Title Case
            var parts = buildingId.Split('_');
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpper(part[0]));
                if (part.Length > 1) sb.Append(part.Substring(1));
            }
            return sb.ToString();
        }

        private string FormatQueueTimer(int totalSeconds)
        {
            if (totalSeconds <= 0) return "Done";
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            if (h > 0) return $"{h}:{m:D2}:{s:D2}";
            return $"{m}:{s:D2}";
        }

        private void OnQueueSlotTapped(string slotType, int slotIndex)
        {
            if (slotType == "build")
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm) && bm.BuildQueue.Count > slotIndex)
                {
                    var entry = bm.BuildQueue[slotIndex];
                    // Find and center on the building
                    foreach (var p in _placements)
                    {
                        if (p.InstanceId == entry.PlacedId && p.VisualGO != null)
                        {
                            CenterOnBuilding(p);
                            break;
                        }
                    }
                }
                else
                {
                    // No active build — show build queue panel
                    ToggleBuildQueuePanel();
                }
            }
            else if (slotType == "research")
            {
                // Open research quick panel (uses academy tier)
                int academyTier = 1;
                foreach (var p in _placements)
                {
                    if (p.BuildingId == "academy") { academyTier = p.Tier; break; }
                }
                ShowResearchQuickPanel(academyTier);
            }
        }

        private void CenterOnBuilding(CityBuildingPlacement placement)
        {
            if (placement.VisualGO == null) return;
            var targetRect = placement.VisualGO.GetComponent<RectTransform>();
            if (targetRect == null) return;

            var scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null) return;

            // Calculate normalized position to center on building
            var content = scrollRect.content;
            if (content == null) return;

            Vector2 targetPos = targetRect.anchoredPosition;
            Vector2 contentSize = content.sizeDelta;
            Vector2 viewportSize = scrollRect.viewport != null
                ? scrollRect.viewport.rect.size
                : ((RectTransform)scrollRect.transform).rect.size;

            float nx = Mathf.Clamp01((targetPos.x + contentSize.x * 0.5f - viewportSize.x * 0.5f) / (contentSize.x - viewportSize.x));
            float ny = Mathf.Clamp01((targetPos.y + contentSize.y * 0.5f - viewportSize.y * 0.5f) / (contentSize.y - viewportSize.y));
            scrollRect.normalizedPosition = new Vector2(nx, ny);
        }

        private void TickQueueStatusPanel()
        {
            if (_queueStatusPanel == null) return;
            _queueSlotRefreshTimer += Time.deltaTime;
            if (_queueSlotRefreshTimer >= QueueSlotRefreshInterval)
            {
                _queueSlotRefreshTimer = 0f;
                RefreshQueueSlots();
            }
        }
    }
}
