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
        private const float CollectDuration = 0.3f;

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

            _rect.sizeDelta = new Vector2(56, 56); // P&C: Generous tap target

            // Canvas group for fade
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // Fade in

            // Combined icon + amount as single floating text (no bg rectangle)
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = new Vector2(40, 0); // Extra width for text
            _amountText = labelGO.AddComponent<Text>();
            _amountText.text = $"{GetResourceSymbol(type)}+{FormatAmount(amount)}";
            _amountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _amountText.fontSize = 11;
            _amountText.fontStyle = FontStyle.Bold;
            _amountText.alignment = TextAnchor.MiddleCenter;
            _amountText.color = GetBubbleTextColor(type);
            _amountText.raycastTarget = false;

            // Strong outline for readability against dark bg
            var outline = labelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            // Second outline pass for extra contrast
            var outline2 = labelGO.AddComponent<Outline>();
            outline2.effectColor = new Color(0, 0, 0, 0.6f);
            outline2.effectDistance = new Vector2(-1f, 1f);

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

                // Scale up and fade out
                float scale = 1f + t * 0.5f;
                transform.localScale = Vector3.one * scale;
                _canvasGroup.alpha = 1f - t;

                // Float upward
                _rect.anchoredPosition = _basePosition + Vector2.up * (t * 40f);
                return;
            }

            // Fade in
            if (_canvasGroup.alpha < 1f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, Time.deltaTime * 3f);

            // Bob animation
            _bobPhase += Time.deltaTime * _bobSpeed;
            float bobOffset = Mathf.Sin(_bobPhase) * _bobAmplitude;
            _rect.anchoredPosition = _basePosition + Vector2.up * bobOffset;

            // Gentle scale pulse
            _pulsePhase += Time.deltaTime * _pulseSpeed;
            float pulse = 1f + 0.05f * Mathf.Sin(_pulsePhase);
            transform.localScale = Vector3.one * pulse;
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
            Collect();
        }

        private void Collect()
        {
            _collecting = true;
            _collectTimer = 0f;

            // P&C-style: quick punch scale on collect
            transform.localScale = Vector3.one * 1.4f;

            // Actually add the resources
            if (ServiceLocator.TryGet<ResourceManager>(out var rm))
                rm.AddResource(ResourceType, Amount);

            // Publish event for UI feedback (triggers burst fly animation)
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
}
