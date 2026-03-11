using UnityEngine;

namespace AshenThrone.UI
{
    /// <summary>
    /// Adjusts RectTransform to fit within Screen.safeArea.
    /// Handles iPhone notch/Dynamic Island, Android display cutouts, etc.
    /// Attach to a panel that is a direct child of the Canvas.
    /// All UI content should be parented under this panel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafeArea;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            ApplySafeArea(Screen.safeArea);
            _lastSafeArea = Screen.safeArea;
        }

        void Update()
        {
            if (_lastSafeArea != Screen.safeArea)
            {
                ApplySafeArea(Screen.safeArea);
                _lastSafeArea = Screen.safeArea;
            }
        }

        void ApplySafeArea(Rect safeArea)
        {
            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
