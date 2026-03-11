using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-style welcome-back popup showing accumulated offline resource production.
    /// Appears once when OfflineEarningsAppliedEvent fires, with animated count-up and collect button.
    /// </summary>
    public class OfflineEarningsPopup : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _popup;
        private EventSubscription _sub;

        // Count-up animation state
        private long _targetStone, _targetIron, _targetGrain, _targetArcane;
        private float _countUpElapsed;
        private const float CountUpDuration = 1.2f;
        private Text _stoneText, _ironText, _grainText, _arcaneText;
        private bool _counting;

        private static readonly Color PanelBg = new(0.06f, 0.04f, 0.10f, 0.95f);
        private static readonly Color GoldBorder = new(0.78f, 0.62f, 0.22f, 1f);
        private static readonly Color StoneColor = new(0.85f, 0.82f, 0.76f);
        private static readonly Color IronColor = new(0.78f, 0.80f, 0.90f);
        private static readonly Color GrainColor = new(1f, 0.92f, 0.45f);
        private static readonly Color ArcaneColor = new(0.80f, 0.55f, 1f);

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _sub = EventBus.Subscribe<OfflineEarningsAppliedEvent>(OnOfflineEarnings);
        }

        private void OnDisable()
        {
            _sub?.Dispose();
        }

        private void OnOfflineEarnings(OfflineEarningsAppliedEvent evt)
        {
            // Only show if meaningful earnings
            if (evt.Stone + evt.Iron + evt.Grain + evt.ArcaneEssence <= 0) return;

            _targetStone = evt.Stone;
            _targetIron = evt.Iron;
            _targetGrain = evt.Grain;
            _targetArcane = evt.ArcaneEssence;

            ShowPopup(evt.OfflineSeconds);
        }

        private void ShowPopup(float offlineSeconds)
        {
            if (_popup != null) Destroy(_popup);
            if (_canvas == null) return;

            _popup = new GameObject("OfflineEarningsPopup");
            _popup.transform.SetParent(_canvas.transform, false);
            _popup.transform.SetAsLastSibling();

            // Full-screen dim overlay
            var dimRect = _popup.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            var dimImg = _popup.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.7f);
            dimImg.raycastTarget = true;

            // Center panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_popup.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.25f);
            panelRect.anchorMax = new Vector2(0.90f, 0.75f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelBg = panel.AddComponent<Image>();
            panelBg.color = PanelBg;
            panelBg.raycastTarget = false;

            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = GoldBorder;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Inner gold line
            var panelOutline2 = panel.AddComponent<Outline>();
            panelOutline2.effectColor = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.4f);
            panelOutline2.effectDistance = new Vector2(-1f, 1f);

            // Title: "WELCOME BACK"
            var titleGO = CreateText(panel.transform, "Title",
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.95f),
                "WELCOME BACK", 18, FontStyle.Bold, GoldBorder);

            // Subtitle: time away
            string timeStr = FormatDuration(offlineSeconds);
            CreateText(panel.transform, "Subtitle",
                new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.80f),
                $"Your empire produced while you were away ({timeStr}):", 11, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));

            // Resource rows
            float rowTop = 0.62f;
            float rowHeight = 0.11f;

            _stoneText = CreateResourceRow(panel.transform, "Stone", "\u25C8 Stone", StoneColor,
                new Vector2(0.08f, rowTop - rowHeight), new Vector2(0.92f, rowTop));
            rowTop -= rowHeight + 0.02f;

            _ironText = CreateResourceRow(panel.transform, "Iron", "\u2666 Iron", IronColor,
                new Vector2(0.08f, rowTop - rowHeight), new Vector2(0.92f, rowTop));
            rowTop -= rowHeight + 0.02f;

            _grainText = CreateResourceRow(panel.transform, "Grain", "\u2740 Grain", GrainColor,
                new Vector2(0.08f, rowTop - rowHeight), new Vector2(0.92f, rowTop));
            rowTop -= rowHeight + 0.02f;

            _arcaneText = CreateResourceRow(panel.transform, "Arcane", "\u2726 Arcane Essence", ArcaneColor,
                new Vector2(0.08f, rowTop - rowHeight), new Vector2(0.92f, rowTop));

            // Collect button
            var btnGO = new GameObject("CollectBtn");
            btnGO.transform.SetParent(panel.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.20f, 0.04f);
            btnRect.anchorMax = new Vector2(0.80f, 0.16f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnBg = btnGO.AddComponent<Image>();
            btnBg.color = new Color(0.15f, 0.65f, 0.30f, 1f);
            btnBg.raycastTarget = true;

            var btnOutline = btnGO.AddComponent<Outline>();
            btnOutline.effectColor = GoldBorder;
            btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(OnCollectPressed);

            CreateText(btnGO.transform, "BtnLabel",
                Vector2.zero, Vector2.one,
                "COLLECT", 16, FontStyle.Bold, Color.white, true);

            // Start count-up animation
            _countUpElapsed = 0f;
            _counting = true;
        }

        private void Update()
        {
            if (!_counting) return;

            _countUpElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_countUpElapsed / CountUpDuration);
            float ease = t * t * (3f - 2f * t); // Smoothstep

            if (_stoneText != null) _stoneText.text = $"+{FormatAmount((long)(_targetStone * ease))}";
            if (_ironText != null) _ironText.text = $"+{FormatAmount((long)(_targetIron * ease))}";
            if (_grainText != null) _grainText.text = $"+{FormatAmount((long)(_targetGrain * ease))}";
            if (_arcaneText != null) _arcaneText.text = $"+{FormatAmount((long)(_targetArcane * ease))}";

            if (t >= 1f) _counting = false;
        }

        private void OnCollectPressed()
        {
            if (_popup != null)
            {
                Destroy(_popup);
                _popup = null;
            }
            _counting = false;
        }

        private Text CreateResourceRow(Transform parent, string name, string label, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rowGO = new GameObject($"Row_{name}");
            rowGO.transform.SetParent(parent, false);
            var rowRect = rowGO.AddComponent<RectTransform>();
            rowRect.anchorMin = anchorMin;
            rowRect.anchorMax = anchorMax;
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            // Row background
            var bg = rowGO.AddComponent<Image>();
            bg.color = new Color(color.r, color.g, color.b, 0.08f);
            bg.raycastTarget = false;

            // Resource label (left)
            CreateText(rowGO.transform, "Label",
                new Vector2(0.02f, 0f), new Vector2(0.55f, 1f),
                label, 13, FontStyle.Bold, color, false, TextAnchor.MiddleLeft);

            // Amount (right) — animated count-up
            var amountText = CreateText(rowGO.transform, "Amount",
                new Vector2(0.55f, 0f), new Vector2(0.98f, 1f),
                "+0", 15, FontStyle.Bold, color, false, TextAnchor.MiddleRight);

            return amountText.GetComponent<Text>();
        }

        private static GameObject CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, FontStyle style, Color color,
            bool fill = false, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (fill)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(0.8f, -0.8f);

            return go;
        }

        private static string FormatAmount(long amount)
        {
            if (amount >= 1_000_000) return $"{amount / 1_000_000f:F1}M";
            if (amount >= 1_000) return $"{amount / 1_000f:F1}K";
            return amount.ToString();
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds >= 3600f)
            {
                int hours = (int)(seconds / 3600f);
                int mins = (int)((seconds % 3600f) / 60f);
                return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
            }
            if (seconds >= 60f)
            {
                int mins = (int)(seconds / 60f);
                return $"{mins}m";
            }
            return $"{(int)seconds}s";
        }
    }
}
