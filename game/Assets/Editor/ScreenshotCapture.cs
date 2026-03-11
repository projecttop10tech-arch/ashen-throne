#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Captures the Game View at 1080×1920 resolution.
    /// Works in Play mode only (Canvas renders through normal pipeline).
    /// In Edit mode, just saves a dark background placeholder.
    /// Output: Screenshots/capture.png
    /// </summary>
    public static class ScreenshotCapture
    {
        const int WIDTH = 1080;
        const int HEIGHT = 1920;
        static string OutputDir => Path.Combine(Application.dataPath, "..", "Screenshots");
        static string OutputPath => Path.Combine(OutputDir, "capture.png");

        [MenuItem("AshenThrone/Capture Screenshot")]
        public static void Capture()
        {
            if (!Directory.Exists(OutputDir))
                Directory.CreateDirectory(OutputDir);

            if (EditorApplication.isPlaying)
            {
                // In Play mode: use ScreenCapture which captures the Game View including Canvas UI
                ScreenCapture.CaptureScreenshot(OutputPath, 1);
                Debug.Log($"[Screenshot] Play mode capture saved: {OutputPath}");
            }
            else
            {
                // In Edit mode: render what we can (scene camera + attempt UI)
                Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                {
                    Debug.LogWarning("[Screenshot] No camera in scene. Creating temp camera for bg only.");
                    var tmpGo = new GameObject("__TmpCam");
                    cam = tmpGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.04f, 0.02f, 0.08f, 1f);
                    cam.orthographic = true;

                    var rt = new RenderTexture(WIDTH, HEIGHT, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;

                    RenderTexture.active = rt;
                    var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(tmpGo);
                    Debug.Log($"[Screenshot] Edit mode bg-only capture saved: {OutputPath}");
                    Debug.Log("[Screenshot] Enter Play mode for full UI capture.");
                }
                else
                {
                    var rt = new RenderTexture(WIDTH, HEIGHT, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;

                    RenderTexture.active = rt;
                    var tex = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    Debug.Log($"[Screenshot] Edit mode scene capture saved: {OutputPath}");
                    Debug.Log("[Screenshot] Note: Canvas UI only renders in Play mode.");
                }
            }
        }
    }
}
#endif
