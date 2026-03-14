using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;
using AshenThrone.Empire;

namespace AshenThrone.UI.Empire
{
    /// <summary>
    /// P&C-quality demolish confirmation popup with ornate triple border,
    /// glass highlight, corner diamond accents, warm glow, animated pop-in,
    /// and ornate buttons. Triggered by DemolishRequestedEvent.
    /// </summary>
    public class DemolishConfirmDialog : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _popup;
        private GameObject _panel;
        private EventSubscription _sub;

        private string _pendingInstanceId;
        private string _pendingBuildingId;

        private static readonly Color WarningRed = new(0.85f, 0.20f, 0.18f, 1f);
        private static readonly Color WarningRedGlow = new(0.95f, 0.25f, 0.20f, 0.35f);
        private static readonly Color PanelBg = new(0.06f, 0.04f, 0.10f, 0.96f);
        private static readonly Color GoldBorder = new(0.78f, 0.62f, 0.22f, 0.75f);
        private static readonly Color GoldBorderGlow = new(0.90f, 0.72f, 0.28f, 0.25f);
        private static readonly Color HeaderBg = new(0.14f, 0.06f, 0.08f, 0.90f);
        private static readonly Color GoldText = new(0.92f, 0.87f, 0.72f, 1f);
        private static readonly Color CancelBg = new(0.30f, 0.28f, 0.25f, 0.90f);

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            _sub = EventBus.Subscribe<DemolishRequestedEvent>(OnDemolishRequested);
        }

        private void OnDisable()
        {
            _sub?.Dispose();
        }

        private void OnDemolishRequested(DemolishRequestedEvent evt)
        {
            _pendingInstanceId = evt.InstanceId;
            _pendingBuildingId = evt.BuildingId;
            ShowDialog(evt.BuildingId, evt.Tier);
        }

        private void ShowDialog(string buildingId, int tier)
        {
            if (_popup != null) Destroy(_popup);
            if (_canvas == null) return;

            _popup = new GameObject("DemolishConfirmDialog");
            _popup.transform.SetParent(_canvas.transform, false);
            _popup.transform.SetAsLastSibling();

            // Dim overlay
            var dimRect = _popup.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImg = _popup.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.75f);
            dimImg.raycastTarget = true;

            // Panel
            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_popup.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.10f, 0.32f);
            panelRect.anchorMax = new Vector2(0.90f, 0.68f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelBg = _panel.AddComponent<Image>();
            panelBg.color = PanelBg;
            panelBg.raycastTarget = true;

            // Triple border: outer glow → gold → inner warning red
            var outerGlow = _panel.AddComponent<Outline>();
            outerGlow.effectColor = GoldBorderGlow;
            outerGlow.effectDistance = new Vector2(3f, -3f);

            var goldBorderGO = new GameObject("GoldBorder");
            goldBorderGO.transform.SetParent(_panel.transform, false);
            FillParent(goldBorderGO);
            var goldBorderImg = goldBorderGO.AddComponent<Image>();
            goldBorderImg.color = new Color(0, 0, 0, 0); goldBorderImg.raycastTarget = false;
            var goldOut = goldBorderGO.AddComponent<Outline>();
            goldOut.effectColor = GoldBorder;
            goldOut.effectDistance = new Vector2(1.5f, -1.5f);

            var innerBorderGO = new GameObject("InnerBorder");
            innerBorderGO.transform.SetParent(_panel.transform, false);
            FillParent(innerBorderGO);
            var innerBorderImg = innerBorderGO.AddComponent<Image>();
            innerBorderImg.color = new Color(0, 0, 0, 0); innerBorderImg.raycastTarget = false;
            var innerOut = innerBorderGO.AddComponent<Outline>();
            innerOut.effectColor = new Color(WarningRed.r, WarningRed.g, WarningRed.b, 0.40f);
            innerOut.effectDistance = new Vector2(0.7f, -0.7f);

            // Glass highlight (top half)
            var glass = new GameObject("Glass");
            glass.transform.SetParent(_panel.transform, false);
            var glRect = glass.AddComponent<RectTransform>();
            glRect.anchorMin = new Vector2(0f, 0.48f);
            glRect.anchorMax = Vector2.one;
            glRect.offsetMin = Vector2.zero;
            glRect.offsetMax = Vector2.zero;
            var glImg = glass.AddComponent<Image>();
            glImg.color = new Color(1f, 1f, 1f, 0.04f);
            glImg.raycastTarget = false;

            // Warm bottom glow (danger feel)
            var warmBottom = new GameObject("WarnGlow");
            warmBottom.transform.SetParent(_panel.transform, false);
            var wbRect = warmBottom.AddComponent<RectTransform>();
            wbRect.anchorMin = Vector2.zero;
            wbRect.anchorMax = new Vector2(1f, 0.15f);
            wbRect.offsetMin = Vector2.zero;
            wbRect.offsetMax = Vector2.zero;
            var wbImg = warmBottom.AddComponent<Image>();
            wbImg.color = new Color(0.85f, 0.20f, 0.15f, 0.06f);
            wbImg.raycastTarget = false;

            // Corner diamond accents
            float cs = 0.04f;
            float[][] corners = { new[]{0.01f, 0.96f}, new[]{0.96f, 0.96f}, new[]{0.01f, 0.01f}, new[]{0.96f, 0.01f} };
            for (int ci = 0; ci < corners.Length; ci++)
            {
                CreateCornerDiamond(_panel.transform,
                    new Vector2(corners[ci][0], corners[ci][1]),
                    new Vector2(corners[ci][0] + cs, corners[ci][1] + cs));
            }

            // Header bg strip
            var headerBg = new GameObject("HeaderBg");
            headerBg.transform.SetParent(_panel.transform, false);
            var hRect = headerBg.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.02f, 0.72f);
            hRect.anchorMax = new Vector2(0.98f, 0.96f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = Vector2.zero;
            var hImg = headerBg.AddComponent<Image>();
            hImg.color = HeaderBg;
            hImg.raycastTarget = false;
            var hOut = headerBg.AddComponent<Outline>();
            hOut.effectColor = new Color(WarningRed.r, WarningRed.g, WarningRed.b, 0.30f);
            hOut.effectDistance = new Vector2(0.6f, -0.6f);

            string displayName = buildingId.Replace('_', ' ');

            // Warning icon + title
            CreateOrnateLabel(_panel.transform, "Title",
                new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.95f),
                $"\u26A0 DEMOLISH {displayName.ToUpper()}?", 15, FontStyle.Bold, WarningRed);

            // Gold separator line under header
            var sep = new GameObject("Separator");
            sep.transform.SetParent(_panel.transform, false);
            var sepRect = sep.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0.06f, 0.71f);
            sepRect.anchorMax = new Vector2(0.94f, 0.715f);
            sepRect.offsetMin = Vector2.zero;
            sepRect.offsetMax = Vector2.zero;
            var sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.40f);
            sepImg.raycastTarget = false;

            // Tier info with warning styling
            CreateOrnateLabel(_panel.transform, "Info",
                new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.70f),
                $"Level <color=#FFD966>{tier}</color> — This action cannot be undone.\nResources spent will <color=#FF6655>NOT</color> be refunded.",
                11, FontStyle.Normal, GoldText, true);

            // Decorative ✦ warning accent
            CreateOrnateLabel(_panel.transform, "WarningAccent",
                new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.46f),
                "\u2666 \u2666 \u2666", 8, FontStyle.Normal, new Color(WarningRed.r, WarningRed.g, WarningRed.b, 0.35f));

            // DEMOLISH button (red, ornate)
            var confirmGO = CreateOrnateButton(_panel.transform, "ConfirmBtn",
                new Vector2(0.06f, 0.06f), new Vector2(0.48f, 0.36f),
                "DEMOLISH", WarningRed, WarningRedGlow);
            confirmGO.GetComponent<Button>().onClick.AddListener(OnConfirm);

            // CANCEL button (muted, ornate)
            var cancelGO = CreateOrnateButton(_panel.transform, "CancelBtn",
                new Vector2(0.52f, 0.06f), new Vector2(0.94f, 0.36f),
                "CANCEL", CancelBg, new Color(0.50f, 0.45f, 0.40f, 0.25f));
            cancelGO.GetComponent<Button>().onClick.AddListener(OnCancel);

            // Elastic pop-in animation
            StartCoroutine(PopInPanel());
        }

        private IEnumerator PopInPanel()
        {
            if (_panel == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale;
                if (t < 0.55f)
                    scale = Mathf.Lerp(0f, 1.12f, t / 0.55f);
                else
                    scale = Mathf.Lerp(1.12f, 1f, (t - 0.55f) / 0.45f);
                if (_panel != null) _panel.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            if (_panel != null) _panel.transform.localScale = Vector3.one;
        }

        private void OnConfirm()
        {
            if (_pendingInstanceId != null)
            {
                if (ServiceLocator.TryGet<BuildingManager>(out var bm))
                    bm.DemolishBuilding(_pendingInstanceId);
            }
            Close();
        }

        private void OnCancel()
        {
            Close();
        }

        private void Close()
        {
            _pendingInstanceId = null;
            _pendingBuildingId = null;
            if (_popup != null) { Destroy(_popup); _popup = null; }
            _panel = null;
        }

        private static void FillParent(GameObject go)
        {
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private static void CreateCornerDiamond(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("CornerAccent");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0, 0, 45f);
            var img = go.AddComponent<Image>();
            img.color = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.55f);
            img.raycastTarget = false;
        }

        private static void CreateOrnateLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string text, int fontSize, FontStyle style, Color color, bool richText = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = richText;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(0.7f, -0.7f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(0.4f, -0.7f);
        }

        private static GameObject CreateOrnateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string label, Color bgColor, Color glowColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;

            // Triple border on button
            var btnGlow = go.AddComponent<Outline>();
            btnGlow.effectColor = glowColor;
            btnGlow.effectDistance = new Vector2(2f, -2f);

            var goldEdge = new GameObject("GoldEdge");
            goldEdge.transform.SetParent(go.transform, false);
            FillParent(goldEdge);
            goldEdge.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            goldEdge.GetComponent<RectTransform>().anchorMax = Vector2.one;
            var geImg = goldEdge.AddComponent<Image>();
            geImg.color = new Color(0, 0, 0, 0); geImg.raycastTarget = false;
            var geOut = goldEdge.AddComponent<Outline>();
            geOut.effectColor = new Color(GoldBorder.r, GoldBorder.g, GoldBorder.b, 0.50f);
            geOut.effectDistance = new Vector2(0.8f, -0.8f);

            // Glass highlight on button
            var btnGlass = new GameObject("Glass");
            btnGlass.transform.SetParent(go.transform, false);
            var bgRect = btnGlass.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.45f);
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = btnGlass.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.08f);
            bgImg.raycastTarget = false;

            go.AddComponent<Button>().targetGraphic = bg;

            // Label with outline
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lRect = labelGO.AddComponent<RectTransform>();
            lRect.anchorMin = Vector2.zero;
            lRect.anchorMax = Vector2.one;
            lRect.offsetMin = Vector2.zero;
            lRect.offsetMax = Vector2.zero;
            var t = labelGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            var lblOutline = labelGO.AddComponent<Outline>();
            lblOutline.effectColor = new Color(0, 0, 0, 0.80f);
            lblOutline.effectDistance = new Vector2(0.6f, -0.6f);

            return go;
        }
    }

    public readonly struct DemolishRequestedEvent
    {
        public readonly string InstanceId;
        public readonly string BuildingId;
        public readonly int Tier;
        public DemolishRequestedEvent(string id, string bid, int t) { InstanceId = id; BuildingId = bid; Tier = t; }
    }
}
