using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AshenThrone.Core;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Floating collectible resource bubble above resource-producing buildings.
    /// Bobs gently, pulses when ready, and plays a collect animation on tap.
    /// </summary>
    public class ResourceCollectBubble : MonoBehaviour, IPointerClickHandler
    {
        public ResourceType ResourceType { get; set; }
        public long Amount { get; set; }
        public string BuildingInstanceId { get; set; }

        private RectTransform _rect;
        private Text _amountText;
        private CanvasGroup _canvasGroup;

        private float _bobPhase;
        private float _bobSpeed = 1.8f;
        private float _bobAmplitude = 6f;
        private Vector2 _basePosition;
        private bool _collecting;
        private float _collectTimer;
        private const float CollectDuration = 0.5f;

        // Fly-to-bar animation
        private Vector2 _flyStartPos;
        private Vector2 _flyTargetPos;
        private Vector2 _flyControlPoint; // Bezier control for arc

        // Pulse glow
        private float _pulsePhase;
        private float _pulseSpeed = 2.5f;

        // Resource type colors
        private static readonly Color StoneColor = new(0.60f, 0.58f, 0.54f, 1f);
        private static readonly Color IronColor = new(0.55f, 0.55f, 0.62f, 1f);
        private static readonly Color GrainColor = new(0.80f, 0.72f, 0.30f, 1f);
        private static readonly Color ArcaneColor = new(0.55f, 0.30f, 0.85f, 1f);

        public void Initialize(ResourceType type, long amount, string buildingId)
        {
            ResourceType = type;
            Amount = amount;
            BuildingInstanceId = buildingId;

            _rect = GetComponent<RectTransform>();
            if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();

            _rect.sizeDelta = new Vector2(64, 64); // P&C: Large tap target

            // Canvas group for fade
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // Fade in

            // P&C: Colored circular background bubble
            Color bubbleCol = GetBubbleColor(type);
            var radialSpr = Resources.Load<Sprite>("UI/Production/radial_gradient");

            // Glow ring behind bubble
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(transform, false);
            var glowRect = glowGO.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(-0.25f, -0.25f);
            glowRect.anchorMax = new Vector2(1.25f, 1.25f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.color = new Color(bubbleCol.r, bubbleCol.g, bubbleCol.b, 0.30f);
            glowImg.raycastTarget = false;
            if (radialSpr != null) glowImg.sprite = radialSpr;

            // Main circle bg
            var bgImg = gameObject.AddComponent<Image>();
            bgImg.color = new Color(bubbleCol.r * 0.35f, bubbleCol.g * 0.35f, bubbleCol.b * 0.35f, 0.88f);
            bgImg.raycastTarget = true;
            if (radialSpr != null) { bgImg.sprite = radialSpr; bgImg.type = Image.Type.Simple; }
            var bgOutline = gameObject.AddComponent<Outline>();
            bgOutline.effectColor = new Color(bubbleCol.r, bubbleCol.g, bubbleCol.b, 0.70f);
            bgOutline.effectDistance = new Vector2(1f, -1f);

            // Resource icon (top portion)
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.10f, 0.35f);
            iconRect.anchorMax = new Vector2(0.90f, 0.90f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconText = iconGO.AddComponent<Text>();
            iconText.text = GetResourceSymbol(type);
            iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconText.fontSize = 16;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = GetBubbleTextColor(type);
            iconText.raycastTarget = false;
            var iconShadow = iconGO.AddComponent<Shadow>();
            iconShadow.effectColor = new Color(0, 0, 0, 0.9f);
            iconShadow.effectDistance = new Vector2(0.8f, -0.8f);

            // Amount label (bottom portion)
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.05f);
            labelRect.anchorMax = new Vector2(1, 0.40f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _amountText = labelGO.AddComponent<Text>();
            _amountText.text = $"+{FormatAmount(amount)}";
            _amountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _amountText.fontSize = 9;
            _amountText.fontStyle = FontStyle.Bold;
            _amountText.alignment = TextAnchor.MiddleCenter;
            _amountText.color = Color.white;
            _amountText.raycastTarget = false;

            // Strong outline for readability
            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Random bob phase offset so bubbles aren't synchronized
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
            _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_collecting)
            {
                _collectTimer += Time.deltaTime;
                float t = _collectTimer / CollectDuration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }

                // P&C: Ease-in-out timing for smooth arc
                float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                // Quadratic bezier arc: start → control → target
                Vector2 a = Vector2.Lerp(_flyStartPos, _flyControlPoint, eased);
                Vector2 b = Vector2.Lerp(_flyControlPoint, _flyTargetPos, eased);
                _rect.anchoredPosition = Vector2.Lerp(a, b, eased);

                // Scale: punch up then shrink as it reaches the bar
                float scale = t < 0.15f ? 1f + t * 3f : Mathf.Lerp(1.45f, 0.4f, (t - 0.15f) / 0.85f);
                transform.localScale = Vector3.one * scale;

                // Fade: stay visible during flight, fade at end
                _canvasGroup.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                return;
            }

            // Fade in
            if (_canvasGroup.alpha < 1f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, Time.deltaTime * 3f);

            // P&C: Static icon — stays at base position, no bob/pulse
            _rect.anchoredPosition = _basePosition;
            transform.localScale = Vector3.one;
        }

        public void SetBasePosition(Vector2 pos)
        {
            _basePosition = pos;
            if (_rect != null)
                _rect.anchoredPosition = pos;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_collecting) return;

            // P&C: Tapping one bubble collects ALL bubbles of the same resource type
            // Only trigger batch collection on direct user tap (eventData != null)
            if (eventData != null)
            {
                EventBus.Publish(new ResourceBubbleBatchCollectEvent(ResourceType));
            }
            else
            {
                // Called programmatically (from batch collect) — just collect this one
                Collect();
            }
        }

        private void Collect()
        {
            _collecting = true;
            _collectTimer = 0f;

            // P&C-style: quick punch scale on collect
            transform.localScale = Vector3.one * 1.4f;

            // Set up fly-to-bar bezier arc
            _flyStartPos = _rect.anchoredPosition;

            // Target: resource bar at top of screen (approximate anchored position)
            // Resource bar icons are at ~95% Y, spread across top: Stone(10%), Iron(30%), Grain(50%), Arcane(70%)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>();
                float canvasH = canvasRect != null ? canvasRect.rect.height : 1920f;
                float canvasW = canvasRect != null ? canvasRect.rect.width : 1080f;

                float targetX = ResourceType switch
                {
                    ResourceType.Stone => canvasW * 0.08f,
                    ResourceType.Iron => canvasW * 0.28f,
                    ResourceType.Grain => canvasW * 0.48f,
                    ResourceType.ArcaneEssence => canvasW * 0.68f,
                    _ => canvasW * 0.5f,
                };
                float targetY = canvasH * 0.45f; // resource bar Y in anchored space

                // Convert to local space if needed
                _flyTargetPos = new Vector2(
                    targetX - _flyStartPos.x + (_flyStartPos.x - canvasW * 0.5f) * 0.3f,
                    targetY
                );
            }
            else
            {
                _flyTargetPos = _flyStartPos + new Vector2(0f, 400f);
            }

            // Bezier control point: arc upward and slightly toward center
            float midX = (_flyStartPos.x + _flyTargetPos.x) * 0.5f;
            float peakY = Mathf.Max(_flyStartPos.y, _flyTargetPos.y) + 120f;
            _flyControlPoint = new Vector2(midX, peakY);

            // Actually add the resources
            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                rm.AddResource(ResourceType, Amount);

            // Publish event for UI feedback
            EventBus.Publish(new ResourceCollectedEvent(ResourceType, Amount, BuildingInstanceId));
        }

        private static Color GetBubbleTextColor(ResourceType type) => type switch
        {
            ResourceType.Stone => new Color(0.85f, 0.82f, 0.76f),
            ResourceType.Iron => new Color(0.78f, 0.80f, 0.90f),
            ResourceType.Grain => new Color(1f, 0.92f, 0.45f),
            ResourceType.ArcaneEssence => new Color(0.80f, 0.55f, 1f),
            _ => Color.white,
        };

        private static Color GetBubbleColor(ResourceType type) => type switch
        {
            ResourceType.Stone => StoneColor,
            ResourceType.Iron => IronColor,
            ResourceType.Grain => GrainColor,
            ResourceType.ArcaneEssence => ArcaneColor,
            _ => Color.white,
        };

        private static string GetResourceSymbol(ResourceType type) => type switch
        {
            ResourceType.Stone => "\u25C8",  // ◈
            ResourceType.Iron => "\u2666",   // ♦
            ResourceType.Grain => "\u2740",  // ❀
            ResourceType.ArcaneEssence => "\u2726", // ✦
            _ => "?",
        };

        private static string FormatAmount(long amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:F1}M";
            if (amount >= 1_000) return $"{amount / 1_000f:F1}K";
            return amount.ToString();
        }
    }

    public readonly struct ResourceCollectedEvent
    {
        public readonly ResourceType Type;
        public readonly long Amount;
        public readonly string BuildingInstanceId;
        public ResourceCollectedEvent(ResourceType t, long a, string b) { Type = t; Amount = a; BuildingInstanceId = b; }
    }

    /// <summary>P&C: Batch-collect all bubbles of the same resource type (fired when tapping any single bubble).</summary>
    public readonly struct ResourceBubbleBatchCollectEvent
    {
        public readonly ResourceType Type;
        public ResourceBubbleBatchCollectEvent(ResourceType t) { Type = t; }
    }
}
