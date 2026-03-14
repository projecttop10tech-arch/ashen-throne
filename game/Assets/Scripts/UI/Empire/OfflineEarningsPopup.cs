using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality welcome-back popup with ornate triple-bordered gold frame,
    /// glass highlight, warm inner glow, elastic pop-in, animated count-up,
    /// shimmer on resource rows, and ornate collect button.
    /// </summary>
    public class OfflineEarningsPopup : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _popup;
        private GameObject _panel;
        private EventSubscription _sub;

        private long _targetStone, _targetIron, _targetGrain, _targetArcane;
        private float _countUpElapsed;
        private const float CountUpDuration = 1.2f;
        private Text _stoneText, _ironText, _grainText, _arcaneText;
        private bool _counting;

        private static readonly Color PanelBg = new(0.05f, 0.03f, 0.09f, 0.96f);
        private static readonly Color GoldBorder = new(0.83f, 0.66f, 0.26f, 1f);
        private static readonly Color GoldGlow = new(0.90f, 0.72f, 0.28f, 0.30f);
        private static readonly Color StoneColor = new(0.88f, 0.85f, 0.78f);
        private static readonly Color IronColor = new(0.80f, 0.82f, 0.92f);
        private static readonly Color GrainColor = new(1f, 0.92f, 0.45f);
        private static readonly Color ArcaneColor = new(0.82f, 0.55f, 1f);

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
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_popup.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.24f);
            panelRect.anchorMax = new Vector2(0.92f, 0.76f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _panel.transform.localScale = Vector3.zero; // For pop-in

            var panelBg = _panel.AddComponent<Image>();
            panelBg.color = PanelBg;
            panelBg.raycastTarget = false;

            // Triple border: outer glow → gold → inner
            var glowBorder = new GameObject("GlowBorder");
            glowBorder.transform.SetParent(_panel.transform, false);
            var gbRect = glowBorder.AddComponent<RectTransform>();
            gbRect.anchorMin = Vector2.zero; gbRect.anchorMax = Vector2.one;
            gbRect.offsetMin = Vector2.zero; gbRect.offsetMax = Vector2.zero;
            var gbImg = glowBorder.AddComponent<Image>();
            gbImg.color = new Color(0, 0, 0, 0); gbImg.raycastTarget = false;
            var gbOut = glowBorder.AddComponent<Outline>();
            gbOut.effectColor = GoldGlow;
            gbOut.effectDistance = new Vector2(3f, -3f);

            var goldBorder = new GameObject("GoldBorder");
            goldBorder.transform.SetParent(_panel.transform, false);
            var goldr = goldBorder.AddComponent<RectTransform>();
            goldr.anchorMin = Vector2.zero; goldr.anchorMax = Vector2.one;
            goldr.offsetMin = Vector2.zero; goldr.offsetMax = Vector2.zero;
            var goldi = goldBorder.AddComponent<Image>();
            goldi.color = new Color(0, 0, 0, 0); goldi.raycastTarget = false;
            var goldo = goldBorder.AddComponent<Outline>();
            goldo.effectColor = GoldBorder;
            goldo.effectDistance = new Vector2(1.5f, -1.5f);

            var innerBorder = new GameObject("InnerBorder");
            innerBorder.transform.SetParent(_panel.transform, false);
            var ibRect = innerBorder.AddComponent<RectTransform>();
            ibRect.anchorMin = Vector2.zero; ibRect.anchorMax = Vector2.one;
            ibRect.offsetMin = Vector2.zero; ibRect.offsetMax = Vector2.zero;
            var ibImg = innerBorder.AddComponent<Image>();
            ibImg.color = new Color(0, 0, 0, 0); ibImg.raycastTarget = false;
            var ibShadow = innerBorder.AddComponent<Shadow>();
            ibShadow.effectColor = new Color(0.60f, 0.48f, 0.18f, 0.35f);
            ibShadow.effectDistance = new Vector2(0.5f, -0.5f);

            // Glass highlight
            var glass = new GameObject("Glass");
            glass.transform.SetParent(_panel.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.55f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero;
            glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.04f);
            glImg.raycastTarget = false;

            // Warm inner glow (bottom edge)
            var warmGlow = new GameObject("WarmGlow");
            warmGlow.transform.SetParent(_panel.transform, false);
            var wgRect = warmGlow.AddComponent<RectTransform>();
            wgRect.anchorMin = Vector2.zero;
            wgRect.anchorMax = new Vector2(1f, 0.15f);
            wgRect.offsetMin = Vector2.zero;
            wgRect.offsetMax = Vector2.zero;
            var wgImg = warmGlow.AddComponent<Image>();
            wgImg.color = new Color(0.90f, 0.72f, 0.28f, 0.04f);
            wgImg.raycastTarget = false;

            // Decorative corner diamonds
            float[] cx = { 0f, 1f, 0f, 1f };
            float[] cy = { 0f, 0f, 1f, 1f };
            for (int i = 0; i < 4; i++)
            {
                var corner = new GameObject($"Corner_{i}");
                corner.transform.SetParent(_panel.transform, false);
                var cr = corner.AddComponent<RectTransform>();
                cr.anchorMin = new Vector2(cx[i] - 0.02f, cy[i] - 0.02f);
                cr.anchorMax = new Vector2(cx[i] + 0.02f, cy[i] + 0.02f);
                cr.offsetMin = Vector2.zero;
                cr.offsetMax = Vector2.zero;
                cr.localRotation = Quaternion.Euler(0, 0, 45);
                var ci = corner.AddComponent<Image>();
                ci.color = new Color(0.83f, 0.66f, 0.26f, 0.35f);
                ci.raycastTarget = false;
            }

            // Title: "WELCOME BACK" with ornate styling
            var titleGO = CreateText(_panel.transform, "Title",
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.95f),
                "\u2726 WELCOME BACK \u2726", 18, FontStyle.Bold, GoldBorder);

            // Gold separator under title
            var sep = new GameObject("Separator");
            sep.transform.SetParent(_panel.transform, false);
            var sepRect = sep.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.15f, 0.79f);
            sepRect.anchorMax = new Vector2(0.85f, 0.795f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.83f, 0.66f, 0.26f, 0.40f);
            sepImg.raycastTarget = false;

            string timeStr = FormatDuration(offlineSeconds);
            CreateText(_panel.transform, "Subtitle",
                new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.80f),
                $"Your empire produced while you were away ({timeStr}):", 11, FontStyle.Normal, new Color(0.72f, 0.70f, 0.68f));

            var panel = _panel; // local reference for resource rows

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

            // Ornate collect button
            var btnGO = new GameObject("CollectBtn");
            btnGO.transform.SetParent(panel.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.18f, 0.03f);
            btnRect.anchorMax = new Vector2(0.82f, 0.17f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnBg = btnGO.AddComponent<Image>();
            btnBg.color = new Color(0.15f, 0.68f, 0.32f, 1f);
            btnBg.raycastTarget = true;

            // Triple border on button
            var btnGlow = btnGO.AddComponent<Outline>();
            btnGlow.effectColor = new Color(0.25f, 0.85f, 0.40f, 0.35f);
            btnGlow.effectDistance = new Vector2(2f, -2f);
            var btnGoldEdge = new GameObject("GoldEdge");
            btnGoldEdge.transform.SetParent(btnGO.transform, false);
            var bgeRect = btnGoldEdge.AddComponent<RectTransform>();
            bgeRect.anchorMin = Vector2.zero; bgeRect.anchorMax = Vector2.one;
            bgeRect.offsetMin = Vector2.zero; bgeRect.offsetMax = Vector2.zero;
            var bgeImg = btnGoldEdge.AddComponent<Image>();
            bgeImg.color = new Color(0, 0, 0, 0); bgeImg.raycastTarget = false;
            var bgeOut = btnGoldEdge.AddComponent<Outline>();
            bgeOut.effectColor = new Color(0.78f, 0.62f, 0.22f, 0.55f);
            bgeOut.effectDistance = new Vector2(0.8f, -0.8f);

            // Glass on button
            var btnGlass = new GameObject("Glass");
            btnGlass.transform.SetParent(btnGO.transform, false);
            var bgRect = btnGlass.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.45f);
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImgGlass = btnGlass.AddComponent<Image>();
            bgImgGlass.color = new Color(1f, 1f, 1f, 0.10f);
            bgImgGlass.raycastTarget = false;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            btn.onClick.AddListener(OnCollectPressed);

            CreateText(btnGO.transform, "BtnLabel",
                Vector2.zero, Vector2.one,
                "COLLECT", 16, FontStyle.Bold, Color.white, true);

            // Start count-up + pop-in animation
            _countUpElapsed = 0f;
            _counting = true;
            StartCoroutine(PopInPanel());
        }

        private IEnumerator PopInPanel()
        {
            if (_panel == null) yield break;
            float elapsed = 0f;
            const float duration = 0.35f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (t < 0.6f)
                    scale = Mathf.Lerp(0f, 1.1f, t / 0.6f);
                else
                    scale = Mathf.Lerp(1.1f, 1f, (t - 0.6f) / 0.4f);
                _panel.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _panel.transform.localScale = Vector3.one;
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

            // Ornate row background with tinted border
            var bg = rowGO.AddComponent<Image>();
            bg.color = new Color(color.r * 0.08f, color.g * 0.08f, color.b * 0.08f, 0.40f);
            bg.raycastTarget = false;
            var rowOutline = rowGO.AddComponent<Outline>();
            rowOutline.effectColor = new Color(color.r, color.g, color.b, 0.20f);
            rowOutline.effectDistance = new Vector2(0.6f, -0.6f);

            // Left accent strip (resource color)
            var accent = new GameObject("Accent");
            accent.transform.SetParent(rowGO.transform, false);
            var accentRect = accent.AddComponent<RectTransform>();
            accentRect.anchorMin = Vector2.zero;
            accentRect.anchorMax = new Vector2(0.01f, 1f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = Vector2.zero;
            var accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(color.r, color.g, color.b, 0.50f);
            accentImg.raycastTarget = false;

            // Resource label (left)
            CreateText(rowGO.transform, "Label",
                new Vector2(0.03f, 0f), new Vector2(0.55f, 1f),
                label, 13, FontStyle.Bold, color, false, TextAnchor.MiddleLeft);

            // Amount (right) — animated count-up
            var amountText = CreateText(rowGO.transform, "Amount",
                new Vector2(0.55f, 0f), new Vector2(0.97f, 1f),
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
