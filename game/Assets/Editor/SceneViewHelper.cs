#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class SceneViewHelper
    {
        [MenuItem("AshenThrone/Toggle 2D Mode")]
        public static void Toggle2DMode()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.in2DMode = !sv.in2DMode;
                sv.Repaint();
                Debug.Log($"[SceneViewHelper] 2D mode: {sv.in2DMode}");
            }
            else
            {
                Debug.LogWarning("[SceneViewHelper] No active SceneView found.");
            }
        }
    }
}
#endif
