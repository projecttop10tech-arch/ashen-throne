#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class PhoneScreenshot
    {
        [MenuItem("AshenThrone/Capture Phone Screenshot")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[PhoneScreenshot] Must be in Play mode.");
                return;
            }

            // Focus and repaint the Game view so it renders
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                var gv = EditorWindow.GetWindow(gameViewType);
                if (gv != null)
                {
                    gv.Focus();
                    gv.Repaint();
                }
            }

            // Force canvas update
            Canvas.ForceUpdateCanvases();

            // Delete old file first to detect if new one is written
            if (System.IO.File.Exists("/tmp/empire_capture.png"))
                System.IO.File.Delete("/tmp/empire_capture.png");

            // Capture at 2x game view resolution for quality
            ScreenCapture.CaptureScreenshot("/tmp/empire_capture.png", 2);
            Debug.Log($"[PhoneScreenshot] Capture queued. Screen: {Screen.width}x{Screen.height}");

            // Also schedule a delayed check
            EditorApplication.delayCall += () =>
            {
                // Force another repaint to ensure frame renders
                if (gameViewType != null)
                {
                    var gv2 = EditorWindow.GetWindow(gameViewType);
                    gv2?.Repaint();
                }
                Debug.Log("[PhoneScreenshot] DelayCall repaint triggered.");
            };
        }

        [MenuItem("AshenThrone/Toggle Play Mode")]
        public static void TogglePlayMode()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = !EditorApplication.isPlaying;
            Debug.Log($"[PhoneScreenshot] Play mode: {(EditorApplication.isPlaying ? "ON" : "OFF")}");
        }

        [MenuItem("AshenThrone/Force Edit Mode")]
        public static void ForceEditMode()
        {
            Debug.Log($"[PhoneScreenshot] Current isPlaying: {EditorApplication.isPlaying}");
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.Log("[PhoneScreenshot] Set isPlaying = false directly.");
            }
            else
            {
                Debug.Log("[PhoneScreenshot] Already in edit mode.");
            }
        }
    }
}
#endif
