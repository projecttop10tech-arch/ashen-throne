using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AshenThrone.Core;

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
        private EventSubscription _upgradeStartedSfxSub;

        private void OnEnable()
        {
            _upgradeCompletedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(OnBuildingUpgradeCompleted);
            _demolishedSub = EventBus.Subscribe<BuildingDemolishedEvent>(OnBuildingDemolished);
            _doubleTapZoomSub = EventBus.Subscribe<BuildingDoubleTappedEvent>(OnDoubleTapZoom);
            _collectSub = EventBus.Subscribe<ResourceCollectedEvent>(_ => PlaySfx(_sfxCollect));
            _buildCompleteSfxSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(_ => PlaySfx(_sfxBuildComplete));
            _upgradeStartedSfxSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(_ => PlaySfx(_sfxLevelUp));
        }

        private void OnDisable()
        {
            _upgradeCompletedSub?.Dispose();
            _demolishedSub?.Dispose();
            _doubleTapZoomSub?.Dispose();
            _collectSub?.Dispose();
            _buildCompleteSfxSub?.Dispose();
            _upgradeStartedSfxSub?.Dispose();
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
            // Center on stronghold after layout rebuild
            StartCoroutine(DelayedCenterOnStronghold());
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
            bool showDetails = _currentZoom >= 1.0f;
            bool showCategoryIcons = _currentZoom >= 0.6f && _currentZoom < 1.3f;
            foreach (var p in _placements)
            {
                if (p.VisualGO == null) continue;
                var badge = p.VisualGO.transform.Find("LevelBadge");
                if (badge != null) badge.gameObject.SetActive(showDetails);
                var nameLabel = p.VisualGO.transform.Find("NameLabel");
                if (nameLabel != null) nameLabel.gameObject.SetActive(showDetails);
                // P&C: Category mini-icons visible at medium zoom
                var catIcon = p.VisualGO.transform.Find("CategoryIcon");
                if (catIcon != null) catIcon.gameObject.SetActive(showCategoryIcons);
            }
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

            // P&C-style production rate label on resource buildings
            CreateProductionLabel(go, placement.BuildingId, placement.Tier);

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
                        img.sprite = sprite;
                        img.preserveAspect = true;
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
            }
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

            // Bounce animation
            if (tapped.VisualGO != null)
                StartCoroutine(BounceBuilding(tapped.VisualGO.transform));

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

        private IEnumerator BounceBuilding(Transform building)
        {
            var original = building.localScale;
            float duration = 0.2f;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration * 0.4f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.4f);
                building.localScale = original * (1f + 0.08f * t);
                yield return null;
            }

            // Bounce back with overshoot
            elapsed = 0f;
            while (elapsed < duration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.6f);
                float bounce = 1f + 0.08f * (1f - t) * Mathf.Cos(t * Mathf.PI);
                building.localScale = original * bounce;
                yield return null;
            }

            building.localScale = original;
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
            _footprintInstanceId = null;
            if (_selectionRing != null) { Destroy(_selectionRing); _selectionRing = null; }
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
            DestroyHoldIndicator();

            // P&C: Audio + haptic on move mode enter
            PlaySfx(_sfxTap);
            #if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
            #endif

            if (scrollRect != null) scrollRect.enabled = false;
            SetGridOverlayVisible(true);

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

            if (scrollRect != null) scrollRect.enabled = false;
            SetGridOverlayVisible(true);
            ClearBuildingFootprint();

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

            if (_dragGhost != null) Destroy(_dragGhost);
            if (_dragShadow != null) Destroy(_dragShadow);
            _dragGhost = null;
            _dragShadow = null;
            _movingBuilding = null;
            _moveMode = false;

            // P&C: SFX on move complete + re-sort depth
            PlaySfx(_sfxBuildComplete);
            SortBuildingsByDepth();

            HideMoveGridCells();
            DestroyHighlight();
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
            }
        }

        /// <summary>Whether we are currently in placement mode.</summary>
        public bool IsInPlacementMode => _placementMode;
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
