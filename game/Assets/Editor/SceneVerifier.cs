#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Verifies all scenes have correct structure, UI elements, and navigation wiring.
    /// Runs in editor mode (no play mode needed).
    /// Menu: AshenThrone/Test/Verify All Scenes
    /// </summary>
    public static class SceneVerifier
    {
        private static List<string> _passed = new();
        private static List<string> _errors = new();

        [MenuItem("AshenThrone/Test/Verify All Scenes")]
        public static void VerifyAll()
        {
            _passed.Clear();
            _errors.Clear();

            VerifyBootScene();
            VerifyLobbyScene();
            VerifyCombatScene();
            VerifyEmpireScene();
            VerifyWorldMapScene();
            VerifyAllianceScene();
            VerifyBuildSettings();

            PrintResults();
        }

        static void VerifyBootScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot/Boot.unity", OpenSceneMode.Single);
            Check("Boot: scene loaded", scene.IsValid());

            bool hasGM = false;
            bool hasServices = false;
            bool hasCanvas = false;
            int serviceCount = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Core.GameManager>() != null)
                {
                    hasGM = true;
                    Check("Boot: GameManager has MainThreadDispatcher",
                        root.GetComponent<Utils.MainThreadDispatcher>() != null);

                    var servicesTf = root.transform.Find("Services");
                    if (servicesTf != null)
                    {
                        hasServices = true;
                        serviceCount = servicesTf.childCount;
                    }
                }
                if (root.GetComponent<Canvas>() != null)
                {
                    hasCanvas = true;
                    CheckChild(root.transform, "Boot", "Background");
                    CheckChild(root.transform, "Boot", "LoadingFrame");
                    CheckChild(root.transform, "Boot", "Title");
                    CheckChild(root.transform, "Boot", "TipText");
                }
            }

            Check("Boot: GameManager exists", hasGM);
            Check("Boot: Services is child of GameManager (DontDestroyOnLoad)", hasServices);
            Check($"Boot: Services has 30+ children ({serviceCount})", serviceCount >= 30);
            Check("Boot: Canvas with UI exists", hasCanvas);
        }

        static void VerifyLobbyScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby/Lobby.unity", OpenSceneMode.Single);
            Check("Lobby: scene loaded", scene.IsValid());

            foreach (var root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas != null)
                {
                    // Verify NEW UI (not old)
                    CheckChild(root.transform, "Lobby", "HeaderBar");
                    CheckChild(root.transform, "Lobby", "BottomNavBar");
                    CheckChild(root.transform, "Lobby", "PlayButton");
                    CheckChild(root.transform, "Lobby", "ResourceBar");
                    CheckChild(root.transform, "Lobby", "QuickActions");

                    // Verify OLD UI is gone
                    var oldNav = FindDeep(root.transform, "NavigationPanel");
                    Check("Lobby: old NavigationPanel removed", oldNav == null);
                    var oldHeader = FindDeep(root.transform, "HeaderPanel");
                    Check("Lobby: old HeaderPanel removed", oldHeader == null);

                    // Verify navigation buttons have SceneNavigator
                    CheckNavButton(root.transform, "Lobby", "PlayButton");
                    CheckNavButton(root.transform, "Lobby", "NavEmpire");
                    CheckNavButton(root.transform, "Lobby", "NavAlliance");
                    CheckNavButton(root.transform, "Lobby", "NavBattle");

                    // Verify CanvasScaler
                    var scaler = root.GetComponent<CanvasScaler>();
                    if (scaler != null)
                    {
                        Check("Lobby: CanvasScaler ScaleWithScreenSize",
                            scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize);
                        Check("Lobby: Reference resolution 1080x1920",
                            scaler.referenceResolution == new Vector2(1080, 1920));
                    }
                    else
                    {
                        _errors.Add("Lobby: CanvasScaler MISSING");
                    }
                }
            }
        }

        static void VerifyCombatScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Combat/Combat.unity", OpenSceneMode.Single);
            Check("Combat: scene loaded", scene.IsValid());

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() != null)
                {
                    CheckChild(root.transform, "Combat", "TopBar");
                    CheckChild(root.transform, "Combat", "TurnOrderPanel");
                    CheckChild(root.transform, "Combat", "CardHand");
                    CheckChild(root.transform, "Combat", "EndTurnBtn");
                    CheckChild(root.transform, "Combat", "VictoryOverlay");
                    CheckChild(root.transform, "Combat", "DefeatOverlay");
                    CheckNavButton(root.transform, "Combat", "ContinueBtn");
                    CheckNavButton(root.transform, "Combat", "QuitBtn");
                    CheckNavButton(root.transform, "Combat", "RetryBtn");
                }
            }
        }

        static void VerifyEmpireScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Empire/Empire.unity", OpenSceneMode.Single);
            Check("Empire: scene loaded", scene.IsValid());

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() != null)
                {
                    CheckChild(root.transform, "Empire", "ResourceHUD");
                    CheckChild(root.transform, "Empire", "StrongholdInfo");
                    CheckChild(root.transform, "Empire", "BuildQueuePanel");
                    CheckChild(root.transform, "Empire", "Toolbar");
                    CheckNavButton(root.transform, "Empire", "BattleBtn");
                    CheckNavButton(root.transform, "Empire", "HeroesBtn");
                }
            }
        }

        static void VerifyWorldMapScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WorldMap/WorldMap.unity", OpenSceneMode.Single);
            Check("WorldMap: scene loaded", scene.IsValid());

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() != null)
                {
                    CheckChild(root.transform, "WorldMap", "TerritoryGrid");
                    CheckChild(root.transform, "WorldMap", "TopBar");
                    CheckNavButton(root.transform, "WorldMap", "BackBtn");
                    CheckNavButton(root.transform, "WorldMap", "AttackBtn");
                }
            }
        }

        static void VerifyAllianceScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Alliance/Alliance.unity", OpenSceneMode.Single);
            Check("Alliance: scene loaded", scene.IsValid());

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Canvas>() != null)
                {
                    CheckChild(root.transform, "Alliance", "TopBar");
                    CheckChild(root.transform, "Alliance", "TabBar");
                    CheckChild(root.transform, "Alliance", "ChatPanel");
                    CheckNavButton(root.transform, "Alliance", "BackBtn");
                }
            }
        }

        static void VerifyBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            Check("BuildSettings: has scenes", scenes.Length >= 6);
            if (scenes.Length >= 6)
            {
                Check("BuildSettings: Boot is index 0", scenes[0].path.Contains("Boot"));
                Check("BuildSettings: Lobby is index 1", scenes[1].path.Contains("Lobby"));
            }
        }

        // --- Helpers ---

        static void Check(string name, bool condition)
        {
            if (condition)
                _passed.Add(name);
            else
                _errors.Add(name);
        }

        static void CheckChild(Transform root, string scene, string childName)
        {
            var found = FindDeep(root, childName);
            Check($"{scene}: {childName} exists", found != null);
        }

        static void CheckNavButton(Transform root, string scene, string buttonName)
        {
            var found = FindDeep(root, buttonName);
            if (found != null)
            {
                var nav = found.GetComponent<UI.SceneNavigator>();
                Check($"{scene}: {buttonName} has SceneNavigator", nav != null);
                var btn = found.GetComponent<Button>();
                Check($"{scene}: {buttonName} has Button component", btn != null);
            }
            else
            {
                _errors.Add($"{scene}: {buttonName} NOT FOUND (can't check nav)");
            }
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        static void PrintResults()
        {
            Debug.Log("========================================");
            Debug.Log("  SCENE VERIFICATION RESULTS");
            Debug.Log("========================================");
            Debug.Log($"  PASSED: {_passed.Count}  |  FAILED: {_errors.Count}");
            Debug.Log("----------------------------------------");
            foreach (var p in _passed)
                Debug.Log($"  [PASS] {p}");
            if (_errors.Count > 0)
            {
                Debug.Log("----------------------------------------");
                foreach (var e in _errors)
                    Debug.LogError($"  [FAIL] {e}");
            }
            Debug.Log("========================================");
            if (_errors.Count == 0)
                Debug.Log("[SceneVerifier] ALL CHECKS PASSED!");
            else
                Debug.LogError($"[SceneVerifier] {_errors.Count} check(s) FAILED.");
        }
    }
}
#endif
