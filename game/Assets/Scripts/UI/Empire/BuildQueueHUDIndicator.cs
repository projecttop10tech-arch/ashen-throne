using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality build queue HUD at bottom-left of Empire screen.
    /// Ornate gold-framed slots with glow effects, shimmer progress bars,
    /// elastic spawn animations, and timer color warnings.
    /// </summary>
    public class BuildQueueHUDIndicator : MonoBehaviour
    {
        private BuildingManager _buildingManager;
        private RectTransform _container;
        private readonly List<QueueSlotUI> _slots = new();
        private EventSubscription _startedSub;
        private EventSubscription _completedSub;
        private EventSubscription _cancelledSub;

        private const int TotalSlots = 2;

        // P&C ornate palette
        private static readonly Color SlotBg = new(0.05f, 0.03f, 0.09f, 0.92f);
        private static readonly Color SlotBorderGold = new(0.78f, 0.62f, 0.22f, 0.85f);
        private static readonly Color SlotBorderGlow = new(0.90f, 0.72f, 0.28f, 0.30f);
        private static readonly Color EmptySlotBg = new(0.04f, 0.03f, 0.07f, 0.60f);
        private static readonly Color EmptySlotBorder = new(0.40f, 0.32f, 0.14f, 0.35f);
        private static readonly Color FillColor = new(0.20f, 0.82f, 0.38f, 1f);
        private static readonly Color FillGlow = new(0.30f, 0.95f, 0.50f, 0.40f);
        private static readonly Color TimerColor = new(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color HeaderColor = new(0.90f, 0.72f, 0.28f, 1f);
        private static readonly Color GlassHighlight = new(1f, 1f, 1f, 0.06f);

        private void Awake()
        {
            ServiceLocator.TryGet(out _buildingManager);
        }

        private void OnEnable()
        {
            _startedSub = EventBus.Subscribe<BuildingUpgradeStartedEvent>(_ => RebuildSlots());
            _completedSub = EventBus.Subscribe<BuildingUpgradeCompletedEvent>(_ => RebuildSlots());
            _cancelledSub = EventBus.Subscribe<BuildingUpgradeCancelledEvent>(_ => RebuildSlots());
        }

        private void OnDisable()
        {
            _startedSub?.Dispose();
            _completedSub?.Dispose();
            _cancelledSub?.Dispose();
        }

        private void Start()
        {
            FindContainer();
            RebuildSlots();
        }

        private void Update()
        {
            if (_buildingManager == null) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                // Spawn pop-in animation
                if (slot.Spawning)
                {
                    slot.SpawnTimer += Time.deltaTime;
                    float st = Mathf.Clamp01(slot.SpawnTimer / 0.35f);
                    float scale = st < 0.6f
                        ? Mathf.Lerp(0f, 1.2f, st / 0.6f)
                        : Mathf.Lerp(1.2f, 1f, (st - 0.6f) / 0.4f);
                    if (slot.Root != null)
                        slot.Root.transform.localScale = Vector3.one * scale;
                    if (st >= 1f) slot.Spawning = false;
                }

                // Active slot animation
                if (slot.TimerText == null || slot.FillRect == null) continue;
                if (i >= _buildingManager.BuildQueue.Count) continue;

                var entry = _buildingManager.BuildQueue[i];
                float total = slot.TotalSeconds;
                float remaining = entry.RemainingSeconds;
                float progress = total > 0 ? 1f - (remaining / total) : 1f;

                slot.TimerText.text = FormatTime(Mathf.CeilToInt(remaining));
                slot.FillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

                // Shimmer sweep across progress bar
                if (slot.ShimmerImage != null)
                {
                    float shimmerT = Mathf.Repeat(Time.time * 0.4f, 1.5f) - 0.25f;
                    var shimmerRect = slot.ShimmerImage.GetComponent<RectTransform>();
                    shimmerRect.anchorMin = new Vector2(Mathf.Clamp01(shimmerT - 0.12f), 0f);
                    shimmerRect.anchorMax = new Vector2(Mathf.Clamp01(shimmerT + 0.12f), 1f);
                    float shimmerAlpha = 0.25f + 0.15f * Mathf.Sin(Time.time * 3f);
                    slot.ShimmerImage.color = new Color(1f, 1f, 1f, shimmerAlpha);
                }

                // Fill bar glow pulse
                if (slot.FillGlowImage != null)
                {
                    float glowAlpha = 0.25f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                    slot.FillGlowImage.color = new Color(FillGlow.r, FillGlow.g, FillGlow.b, glowAlpha);
                }

                // Timer color warning: normal → amber → urgent red pulse
                float ratio = total > 0 ? remaining / total : 1f;
                if (ratio < 0.05f)
                {
                    float pulse = Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f;
                    slot.TimerText.color = Color.Lerp(
                        new Color(1f, 0.35f, 0.25f, 1f),
                        new Color(1f, 0.55f, 0.40f, 1f), pulse);
                }
                else if (ratio < 0.15f)
                {
                    slot.TimerText.color = new Color(1f, 0.70f, 0.30f, 1f);
                }
                else
                {
                    slot.TimerText.color = TimerColor;
                }
            }

            // Locked slot pulse
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsLocked && slot.Root != null)
                {
                    float pulse = Mathf.Sin(Time.time * 2f) * 0.5f + 0.5f;
                    float s = Mathf.Lerp(0.96f, 1.04f, pulse);
                    slot.Root.transform.localScale = Vector3.one * s;

                    // Plus label glow
                    var plusText = slot.Root.transform.Find("PlusLabel")?.GetComponent<Text>();
                    if (plusText != null)
                        plusText.color = new Color(0.85f, 0.68f, 0.25f, Mathf.Lerp(0.4f, 0.7f, pulse));
                }
            }
        }

        private void FindContainer()
        {
            // Find or create the container
            var t = FindDeepChild(transform.root, "BuildQueueHUD");
            if (t != null)
            {
                _container = t.GetComponent<RectTransform>();
            }
            else
            {
                // Create it dynamically near bottom-left, above nav bar
                var go = new GameObject("BuildQueueHUD");
                go.transform.SetParent(transform, false);
                _container = go.AddComponent<RectTransform>();
                _container.anchorMin = new Vector2(0.01f, 0.11f);
                _container.anchorMax = new Vector2(0.40f, 0.25f);
                _container.offsetMin = Vector2.zero;
                _container.offsetMax = Vector2.zero;
            }
        }

        private GameObject _headerLabel;

        private void RebuildSlots()
        {
            // Clear existing
            foreach (var slot in _slots)
            {
                if (slot.Root != null)
                    Destroy(slot.Root);
            }
            _slots.Clear();
            if (_headerLabel != null) { Destroy(_headerLabel); _headerLabel = null; }

            if (_buildingManager == null || _container == null) return;

            // P&C: always show the HUD (even with 0 active builds)
            _container.gameObject.SetActive(true);

            int activeCount = _buildingManager.BuildQueue.Count;

            // Header: "Builder X/2" with ornate styling
            _headerLabel = new GameObject("BuilderHeader");
            _headerLabel.transform.SetParent(_container, false);
            var headerRect = _headerLabel.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 0.78f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            // Header bg pill
            var headerBg = _headerLabel.AddComponent<Image>();
            headerBg.color = new Color(0.06f, 0.04f, 0.10f, 0.80f);
            headerBg.raycastTarget = false;
            var headerBorder = _headerLabel.AddComponent<Outline>();
            headerBorder.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.50f);
            headerBorder.effectDistance = new Vector2(0.8f, -0.8f);

            // Glass highlight on header
            var headerGlass = new GameObject("Glass");
            headerGlass.transform.SetParent(_headerLabel.transform, false);
            var hgRect = headerGlass.AddComponent<RectTransform>();
            hgRect.anchorMin = new Vector2(0f, 0.5f);
            hgRect.anchorMax = Vector2.one;
            hgRect.offsetMin = Vector2.zero;
            hgRect.offsetMax = Vector2.zero;
            var hgImg = headerGlass.AddComponent<Image>();
            hgImg.color = GlassHighlight;
            hgImg.raycastTarget = false;

            var headerTextGO = new GameObject("Text");
            headerTextGO.transform.SetParent(_headerLabel.transform, false);
            var htRect = headerTextGO.AddComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.offsetMin = Vector2.zero;
            htRect.offsetMax = Vector2.zero;
            var headerText = headerTextGO.AddComponent<Text>();
            headerText.text = $"\u2692 Builder {activeCount}/{TotalSlots}";
            headerText.fontSize = 9;
            headerText.fontStyle = FontStyle.Bold;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = HeaderColor;
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.raycastTarget = false;
            var headerOutline = headerTextGO.AddComponent<Outline>();
            headerOutline.effectColor = new Color(0, 0, 0, 0.9f);
            headerOutline.effectDistance = new Vector2(0.8f, -0.8f);
            var headerShadow = headerTextGO.AddComponent<Shadow>();
            headerShadow.effectColor = new Color(0, 0, 0, 0.7f);
            headerShadow.effectDistance = new Vector2(0.5f, -1f);

            // Always create TotalSlots (2) + 1 locked premium slot
            int displaySlots = TotalSlots + 1; // 2 free + 1 locked
            float slotWidth = 1f / displaySlots;
            for (int i = 0; i < displaySlots; i++)
            {
                float xMin = i * slotWidth;
                float xMax = (i + 1) * slotWidth - 0.01f;

                if (i < activeCount)
                {
                    var entry = _buildingManager.BuildQueue[i];
                    var slot = CreateSlot(entry, xMin, xMax);
                    _slots.Add(slot);
                }
                else if (i < TotalSlots)
                {
                    var slot = CreateEmptySlot(i, xMin, xMax);
                    _slots.Add(slot);
                }
                else
                {
                    // P&C: Locked premium 3rd builder slot
                    var slot = CreateLockedSlot(xMin, xMax);
                    _slots.Add(slot);
                }
            }
        }

        private QueueSlotUI CreateSlot(BuildQueueEntry entry, float xMin, float xMax)
        {
            var root = new GameObject($"QueueSlot_{entry.PlacedId}");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.localScale = Vector3.zero; // Start hidden for pop-in

            var bgImg = root.AddComponent<Image>();
            bgImg.color = SlotBg;
            bgImg.raycastTarget = true;

            var slotBtn = root.AddComponent<Button>();
            slotBtn.targetGraphic = bgImg;
            string zoomTargetId = entry.PlacedId;
            slotBtn.onClick.AddListener(() => OnSlotTapped(zoomTargetId));

            // Triple border: outer glow → gold → inner edge
            AddTripleBorder(root.transform, SlotBorderGlow, SlotBorderGold, new Color(0.50f, 0.40f, 0.15f, 0.50f));

            // Glass highlight (top half)
            AddGlassHighlight(root.transform, 0.45f);

            // Warm inner edge glow
            var warmEdge = new GameObject("WarmEdge");
            warmEdge.transform.SetParent(root.transform, false);
            var weRect = warmEdge.AddComponent<RectTransform>();
            weRect.anchorMin = new Vector2(0f, 0.7f);
            weRect.anchorMax = Vector2.one;
            weRect.offsetMin = Vector2.zero;
            weRect.offsetMax = Vector2.zero;
            var weImg = warmEdge.AddComponent<Image>();
            weImg.color = new Color(0.90f, 0.72f, 0.28f, 0.04f);
            weImg.raycastTarget = false;

            // Progress fill bar at bottom (ornate)
            var fillBg = new GameObject("FillBg");
            fillBg.transform.SetParent(root.transform, false);
            var fillBgRect = fillBg.AddComponent<RectTransform>();
            fillBgRect.anchorMin = new Vector2(0.02f, 0.02f);
            fillBgRect.anchorMax = new Vector2(0.98f, 0.28f);
            fillBgRect.offsetMin = Vector2.zero;
            fillBgRect.offsetMax = Vector2.zero;
            var fillBgImg = fillBg.AddComponent<Image>();
            fillBgImg.color = new Color(0.02f, 0.01f, 0.04f, 0.70f);
            fillBgImg.raycastTarget = false;
            var fillBgOutline = fillBg.AddComponent<Outline>();
            fillBgOutline.effectColor = new Color(0.60f, 0.48f, 0.18f, 0.40f);
            fillBgOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // Fill bar
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillBg.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.01f, 1f);
            fillRect.offsetMin = new Vector2(1, 1);
            fillRect.offsetMax = new Vector2(-1, -1);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = FillColor;
            fillImg.raycastTarget = false;

            // Fill glow (pulsing underneath)
            var fillGlowGO = new GameObject("FillGlow");
            fillGlowGO.transform.SetParent(fillBg.transform, false);
            var fgRect = fillGlowGO.AddComponent<RectTransform>();
            fgRect.anchorMin = new Vector2(0f, -0.3f);
            fgRect.anchorMax = new Vector2(1f, 1.3f);
            fgRect.offsetMin = Vector2.zero;
            fgRect.offsetMax = Vector2.zero;
            var fillGlowImg = fillGlowGO.AddComponent<Image>();
            fillGlowImg.color = FillGlow;
            fillGlowImg.raycastTarget = false;

            // Shimmer sweep (bright bar that slides across fill)
            var shimmerGO = new GameObject("Shimmer");
            shimmerGO.transform.SetParent(fillBg.transform, false);
            var shimmerRect = shimmerGO.AddComponent<RectTransform>();
            shimmerRect.anchorMin = new Vector2(0f, 0f);
            shimmerRect.anchorMax = new Vector2(0.12f, 1f);
            shimmerRect.offsetMin = new Vector2(1, 1);
            shimmerRect.offsetMax = new Vector2(-1, -1);
            var shimmerImg = shimmerGO.AddComponent<Image>();
            shimmerImg.color = new Color(1f, 1f, 1f, 0.25f);
            shimmerImg.raycastTarget = false;

            // Building name label with outline
            string displayName = GetBuildingName(entry.PlacedId);
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(root.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.06f, 0.48f);
            nameRect.anchorMax = new Vector2(0.75f, 0.95f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameText = nameGO.AddComponent<Text>();
            nameText.text = displayName;
            nameText.fontSize = 10;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = HeaderColor;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontStyle = FontStyle.Bold;
            nameText.raycastTarget = false;
            var nameOutline = nameGO.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0, 0, 0, 0.9f);
            nameOutline.effectDistance = new Vector2(0.7f, -0.7f);
            var nameShadow = nameGO.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0, 0, 0, 0.6f);
            nameShadow.effectDistance = new Vector2(0.4f, -0.8f);

            // Timer text with outline
            var timerGO = new GameObject("Timer");
            timerGO.transform.SetParent(root.transform, false);
            var timerRect = timerGO.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.06f, 0.22f);
            timerRect.anchorMax = new Vector2(0.75f, 0.52f);
            timerRect.offsetMin = Vector2.zero;
            timerRect.offsetMax = Vector2.zero;
            var timerText = timerGO.AddComponent<Text>();
            timerText.text = FormatTime(Mathf.CeilToInt(entry.RemainingSeconds));
            timerText.fontSize = 11;
            timerText.alignment = TextAnchor.MiddleLeft;
            timerText.color = TimerColor;
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontStyle = FontStyle.Bold;
            timerText.raycastTarget = false;
            var timerOutline = timerGO.AddComponent<Outline>();
            timerOutline.effectColor = new Color(0, 0, 0, 0.9f);
            timerOutline.effectDistance = new Vector2(0.6f, -0.6f);

            float elapsed = (float)(System.DateTime.UtcNow - entry.StartTime).TotalSeconds;
            float total = elapsed + entry.RemainingSeconds;

            // Ornate cancel button
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(root.transform, false);
            var cancelRect = cancelGO.AddComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.78f, 0.55f);
            cancelRect.anchorMax = new Vector2(0.97f, 0.93f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.55f, 0.15f, 0.15f, 0.90f);
            var cancelBorder = cancelGO.AddComponent<Outline>();
            cancelBorder.effectColor = new Color(0.85f, 0.30f, 0.25f, 0.60f);
            cancelBorder.effectDistance = new Vector2(0.6f, -0.6f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            string cancelId = entry.PlacedId;
            cancelBtn.onClick.AddListener(() => OnCancelSlotPressed(cancelId));

            var cancelLabel = new GameObject("X");
            cancelLabel.transform.SetParent(cancelGO.transform, false);
            var clRect = cancelLabel.AddComponent<RectTransform>();
            clRect.anchorMin = Vector2.zero;
            clRect.anchorMax = Vector2.one;
            clRect.offsetMin = Vector2.zero;
            clRect.offsetMax = Vector2.zero;
            var clText = cancelLabel.AddComponent<Text>();
            clText.text = "\u2716";
            clText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clText.fontSize = 9;
            clText.fontStyle = FontStyle.Bold;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.color = new Color(1f, 0.85f, 0.85f, 1f);
            clText.raycastTarget = false;

            return new QueueSlotUI
            {
                Root = root,
                FillRect = fillRect,
                TimerText = timerText,
                TotalSeconds = total,
                PlacedId = entry.PlacedId,
                ShimmerImage = shimmerImg,
                FillGlowImage = fillGlowImg,
                Spawning = true,
                SpawnTimer = 0f
            };
        }

        private QueueSlotUI CreateEmptySlot(int index, float xMin, float xMax)
        {
            var root = new GameObject($"QueueSlot_Empty_{index}");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.localScale = Vector3.zero;

            var bgImg = root.AddComponent<Image>();
            bgImg.color = EmptySlotBg;

            // Subtle border
            AddTripleBorder(root.transform, new Color(0.30f, 0.24f, 0.10f, 0.15f),
                EmptySlotBorder, new Color(0.25f, 0.20f, 0.10f, 0.20f));

            // Dashed inner pattern (faint diamond)
            var diamond = new GameObject("Diamond");
            diamond.transform.SetParent(root.transform, false);
            var dRect = diamond.AddComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.35f, 0.25f);
            dRect.anchorMax = new Vector2(0.65f, 0.75f);
            dRect.offsetMin = Vector2.zero;
            dRect.offsetMax = Vector2.zero;
            dRect.localRotation = Quaternion.Euler(0, 0, 45);
            var dImg = diamond.AddComponent<Image>();
            dImg.color = new Color(0.40f, 0.32f, 0.14f, 0.12f);
            dImg.raycastTarget = false;
            var dOutline = diamond.AddComponent<Outline>();
            dOutline.effectColor = new Color(0.40f, 0.32f, 0.14f, 0.20f);
            dOutline.effectDistance = new Vector2(0.5f, -0.5f);

            // "IDLE" label with subtle styling
            var labelGO = new GameObject("IdleLabel");
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = "IDLE";
            labelText.fontSize = 9;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.50f, 0.42f, 0.30f, 0.55f);
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontStyle = FontStyle.Italic;
            labelText.raycastTarget = false;
            var labelShadow = labelGO.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0, 0, 0, 0.5f);
            labelShadow.effectDistance = new Vector2(0.3f, -0.3f);

            return new QueueSlotUI
            {
                Root = root,
                FillRect = null,
                TimerText = null,
                TotalSeconds = 0f,
                Spawning = true,
                SpawnTimer = 0f
            };
        }

        /// <summary>P&C: Locked premium builder slot with animated "+" icon and gold shimmer.</summary>
        private QueueSlotUI CreateLockedSlot(float xMin, float xMax)
        {
            var root = new GameObject("QueueSlot_Locked");
            root.transform.SetParent(_container, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(xMin, 0f);
            rootRect.anchorMax = new Vector2(xMax, 0.76f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.03f, 0.07f, 0.60f);

            // Gold border with premium glow
            AddTripleBorder(root.transform,
                new Color(0.85f, 0.68f, 0.25f, 0.18f),
                new Color(0.65f, 0.50f, 0.18f, 0.45f),
                new Color(0.45f, 0.35f, 0.12f, 0.25f));

            // Inner radial glow (premium hint)
            var innerGlow = new GameObject("PremiumGlow");
            innerGlow.transform.SetParent(root.transform, false);
            var igRect = innerGlow.AddComponent<RectTransform>();
            igRect.anchorMin = new Vector2(0.2f, 0.1f);
            igRect.anchorMax = new Vector2(0.8f, 0.9f);
            igRect.offsetMin = Vector2.zero;
            igRect.offsetMax = Vector2.zero;
            var igImg = innerGlow.AddComponent<Image>();
            igImg.color = new Color(0.85f, 0.68f, 0.25f, 0.06f);
            igImg.raycastTarget = false;
            var radial = Resources.Load<Sprite>("UI/Production/radial_gradient");
            if (radial != null) { igImg.sprite = radial; igImg.type = Image.Type.Simple; }

            // "+" label (animated via Update)
            var labelGO = new GameObject("PlusLabel");
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<Text>();
            labelText.text = "+";
            labelText.fontSize = 18;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.85f, 0.68f, 0.25f, 0.5f);
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;
            var labelOutline = labelGO.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0, 0, 0, 0.7f);
            labelOutline.effectDistance = new Vector2(0.5f, -0.5f);

            var btn = root.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(OnLockedSlotTapped);

            return new QueueSlotUI
            {
                Root = root,
                FillRect = null,
                TimerText = null,
                TotalSeconds = 0f,
                IsLocked = true
            };
        }

        private void OnLockedSlotTapped()
        {
            Debug.Log("[BuildQueueHUD] Premium 3rd builder slot tapped — unlock via Battle Pass Season progression.");
        }

        private string GetBuildingName(string placedId)
        {
            if (_buildingManager != null && _buildingManager.PlacedBuildings.TryGetValue(placedId, out var placed))
            {
                if (placed.Data != null && !string.IsNullOrEmpty(placed.Data.displayName))
                    return placed.Data.displayName;
            }
            // Fallback: extract from instance ID
            string id = placedId.Contains("_") ? placedId.Substring(0, placedId.LastIndexOf('_')) : placedId;
            var parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
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

        /// <summary>P&C: Tap a queue slot to zoom the camera to that building.</summary>
        private void OnSlotTapped(string placedId)
        {
            var gridView = FindFirstObjectByType<CityGridView>();
            if (gridView != null)
                gridView.ZoomToBuildingSmooth(placedId);
        }

        private void OnCancelSlotPressed(string placedId)
        {
            if (_buildingManager == null) return;
            bool cancelled = _buildingManager.CancelUpgrade(placedId);
            if (cancelled)
                Debug.Log($"[BuildQueueHUD] Cancelled upgrade for {placedId}.");
        }

        // === Ornate UI helpers ===

        private static void AddTripleBorder(Transform parent, Color glowColor, Color goldColor, Color innerColor)
        {
            // Outer glow
            var glow = new GameObject("BorderGlow");
            glow.transform.SetParent(parent, false);
            var gr = glow.AddComponent<RectTransform>();
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = Vector2.zero; gr.offsetMax = Vector2.zero;
            var gi = glow.AddComponent<Image>();
            gi.color = new Color(0, 0, 0, 0);
            gi.raycastTarget = false;
            var go = glow.AddComponent<Outline>();
            go.effectColor = glowColor;
            go.effectDistance = new Vector2(2f, -2f);

            // Gold border
            var gold = new GameObject("BorderGold");
            gold.transform.SetParent(parent, false);
            var goldr = gold.AddComponent<RectTransform>();
            goldr.anchorMin = Vector2.zero; goldr.anchorMax = Vector2.one;
            goldr.offsetMin = Vector2.zero; goldr.offsetMax = Vector2.zero;
            var goldi = gold.AddComponent<Image>();
            goldi.color = new Color(0, 0, 0, 0);
            goldi.raycastTarget = false;
            var goldo = gold.AddComponent<Outline>();
            goldo.effectColor = goldColor;
            goldo.effectDistance = new Vector2(1f, -1f);

            // Inner edge
            var inner = new GameObject("BorderInner");
            inner.transform.SetParent(parent, false);
            var ir = inner.AddComponent<RectTransform>();
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
            var ii = inner.AddComponent<Image>();
            ii.color = new Color(0, 0, 0, 0);
            ii.raycastTarget = false;
            var io = inner.AddComponent<Shadow>();
            io.effectColor = innerColor;
            io.effectDistance = new Vector2(0.5f, -0.5f);
        }

        private static void AddGlassHighlight(Transform parent, float bottomAnchor)
        {
            var glass = new GameObject("Glass");
            glass.transform.SetParent(parent, false);
            var glr = glass.AddComponent<RectTransform>();
            glr.anchorMin = new Vector2(0f, bottomAnchor);
            glr.anchorMax = Vector2.one;
            glr.offsetMin = Vector2.zero;
            glr.offsetMax = Vector2.zero;
            var gli = glass.AddComponent<Image>();
            gli.color = GlassHighlight;
            gli.raycastTarget = false;
        }

        private class QueueSlotUI
        {
            public GameObject Root;
            public RectTransform FillRect;
            public Text TimerText;
            public float TotalSeconds;
            public string PlacedId;
            public Image ShimmerImage;
            public Image FillGlowImage;
            public bool Spawning;
            public float SpawnTimer;
            public bool IsLocked;
        }
    }
}
