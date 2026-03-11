#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class GameViewFixer
    {
        [MenuItem("AshenThrone/Fix Game View")]
        public static void FixGameView()
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) { Debug.LogError("[GameViewFixer] Cannot find GameView type"); return; }

            var gameView = EditorWindow.GetWindow(gameViewType, false, "Game", true);
            if (gameView == null) { Debug.LogError("[GameViewFixer] Cannot get GameView window"); return; }

            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var instance = sizesType.GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var sizesObj = instance.GetValue(null);

                var currentGroupProp = sizesType.GetProperty("currentGroup",
                    BindingFlags.Public | BindingFlags.Instance);
                var currentGroup = currentGroupProp.GetValue(sizesObj);

                var totalCountMethod = currentGroup.GetType().GetMethod("GetTotalCount");
                var getDisplayTextsMethod = currentGroup.GetType().GetMethod("GetDisplayTexts");
                var getGameViewSizeMethod = currentGroup.GetType().GetMethod("GetGameViewSize");

                var gameViewSizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                var fixedResVal = Enum.Parse(sizeTypeEnum, "FixedResolution");

                // Look for an existing FIXED RESOLUTION 1080x1920 entry
                string[] displayTexts = (string[])getDisplayTextsMethod.Invoke(currentGroup, null);
                int bestIndex = -1;

                var widthProp = gameViewSizeType.GetProperty("width",
                    BindingFlags.Public | BindingFlags.Instance);
                var heightProp = gameViewSizeType.GetProperty("height",
                    BindingFlags.Public | BindingFlags.Instance);
                var sizeTypeProp = gameViewSizeType.GetProperty("sizeType",
                    BindingFlags.Public | BindingFlags.Instance);

                if (widthProp != null && heightProp != null && sizeTypeProp != null)
                {
                    int total = (int)totalCountMethod.Invoke(currentGroup, null);
                    for (int i = 0; i < total; i++)
                    {
                        var gvs = getGameViewSizeMethod.Invoke(currentGroup, new object[] { i });
                        int w = (int)widthProp.GetValue(gvs);
                        int h = (int)heightProp.GetValue(gvs);
                        var st = sizeTypeProp.GetValue(gvs);
                        if (w == 1080 && h == 1920 && st.ToString() == "FixedResolution")
                        {
                            bestIndex = i;
                            Debug.Log($"[GameViewFixer] Found FixedResolution 1080x1920 at index {i}: '{displayTexts[i]}'");
                            break;
                        }
                    }
                }

                // Add if not found
                if (bestIndex < 0)
                {
                    var ctor = gameViewSizeType.GetConstructor(new Type[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new object[] { fixedResVal, 1080, 1920, "Phone 9:16 (1080x1920)" });
                    var addMethod = currentGroup.GetType().GetMethod("AddCustomSize");
                    addMethod.Invoke(currentGroup, new object[] { newSize });
                    bestIndex = (int)totalCountMethod.Invoke(currentGroup, null) - 1;
                    Debug.Log($"[GameViewFixer] Added FixedResolution Phone 9:16 (1080x1920) at index {bestIndex}");

                    var changedMethod = sizesType.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.Instance);
                    changedMethod?.Invoke(sizesObj, null);
                }

                // Set the selected size index
                var selectedProp = gameViewType.GetProperty("selectedSizeIndex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selectedProp != null && selectedProp.CanWrite)
                {
                    selectedProp.SetValue(gameView, bestIndex);
                    Debug.Log($"[GameViewFixer] Set selectedSizeIndex = {bestIndex}");
                }
                else
                {
                    var callbackMethod = gameViewType.GetMethod("SizeSelectionCallback",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    callbackMethod?.Invoke(gameView, new object[] { bestIndex, null });
                    Debug.Log($"[GameViewFixer] Called SizeSelectionCallback({bestIndex})");
                }

                // Disable low resolution aspect ratios (Retina fix)
                var lowResProp = gameViewType.GetProperty("lowResolutionForAspectRatios",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (lowResProp != null && lowResProp.CanWrite)
                {
                    lowResProp.SetValue(gameView, false);
                    Debug.Log("[GameViewFixer] Disabled lowResolutionForAspectRatios");
                }

                // Resize window to portrait shape
                gameView.position = new Rect(80, 34, 432, 845);
                gameView.Repaint();
                Debug.Log("[GameViewFixer] Game View set to FixedResolution 1080x1920");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameViewFixer] Reflection error: {e.Message}\n{e.StackTrace}");
                gameView.position = new Rect(80, 34, 432, 845);
                gameView.Repaint();
                Debug.Log("[GameViewFixer] Fallback: resized window to 432x845");
            }
        }
    }
}
#endif
