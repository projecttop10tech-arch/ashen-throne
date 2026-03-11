#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Automated play mode test runner. Enters play mode with Boot scene,
    /// monitors boot sequence, verifies scene transitions, and reports results.
    /// Menu: AshenThrone → Test → Run Play Mode Test
    /// </summary>
    public static class PlayModeTestRunner
    {
        private static bool _isRunning;
        private static float _startTime;
        private static string _currentPhase;
        private static List<string> _errors = new();
        private static List<string> _warnings = new();
        private static List<string> _passed = new();

        [MenuItem("AshenThrone/Test/Run Play Mode Test")]
        public static void RunPlayModeTest()
        {
            if (_isRunning) return;

            // Ensure Boot scene is loaded
            var bootPath = "Assets/Scenes/Boot/Boot.unity";
            if (!System.IO.File.Exists(bootPath))
            {
                Debug.LogError("[PlayModeTest] Boot scene not found!");
                return;
            }

            EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);

            _isRunning = true;
            _errors.Clear();
            _warnings.Clear();
            _passed.Clear();
            _currentPhase = "entering_play_mode";

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Application.logMessageReceived += OnLogMessage;

            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _startTime = Time.realtimeSinceStartup;
                _currentPhase = "boot_sequence";
                EditorApplication.update += MonitorPlayMode;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= MonitorPlayMode;
                Application.logMessageReceived -= OnLogMessage;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                PrintResults();
                _isRunning = false;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                // Ignore known benign warnings
                if (condition.Contains("MCP Unity") || condition.Contains("stub mode"))
                    return;
                _errors.Add($"[{type}] {condition}");
            }
        }

        private static void MonitorPlayMode()
        {
            if (!EditorApplication.isPlaying) return;

            float elapsed = Time.realtimeSinceStartup - _startTime;

            switch (_currentPhase)
            {
                case "boot_sequence":
                    // Check if GameManager exists and is booting
                    var gm = Core.GameManager.Instance;
                    if (gm != null)
                    {
                        _passed.Add("GameManager.Instance is set");

                        // Check services survived
                        if (Core.ServiceLocator.IsRegistered<Network.PlayFabService>())
                            _passed.Add("PlayFabService registered");
                        else if (elapsed > 2f)
                            _errors.Add("PlayFabService NOT registered after 2s");

                        // Check if we transitioned past Boot
                        if (gm.CurrentState == Core.AppState.Lobby)
                        {
                            _passed.Add($"Boot → Lobby transition completed ({elapsed:F1}s)");
                            _currentPhase = "lobby_check";
                        }
                        else if (elapsed > 10f)
                        {
                            _errors.Add($"Boot sequence stuck in state {gm.CurrentState} after {elapsed:F1}s");
                            _currentPhase = "timeout";
                        }
                    }
                    else if (elapsed > 3f)
                    {
                        _errors.Add("GameManager.Instance is null after 3s");
                        _currentPhase = "timeout";
                    }
                    break;

                case "lobby_check":
                    // Verify Lobby scene is loaded and has expected objects
                    var activeScene = SceneManager.GetActiveScene();
                    if (activeScene.name == "Lobby")
                    {
                        _passed.Add("Active scene is Lobby");

                        // Check for Canvas with UI
                        var roots = activeScene.GetRootGameObjects();
                        bool hasCanvas = false;
                        bool hasNewUI = false;
                        foreach (var root in roots)
                        {
                            var canvas = root.GetComponent<Canvas>();
                            if (canvas != null)
                            {
                                hasCanvas = true;
                                // Check for new UI elements
                                var headerBar = FindDeep(root.transform, "HeaderBar");
                                var bottomNav = FindDeep(root.transform, "BottomNavBar");
                                var playBtn = FindDeep(root.transform, "PlayButton");
                                if (headerBar != null) { _passed.Add("Lobby: HeaderBar found"); hasNewUI = true; }
                                else _errors.Add("Lobby: HeaderBar MISSING");
                                if (bottomNav != null) _passed.Add("Lobby: BottomNavBar found");
                                else _errors.Add("Lobby: BottomNavBar MISSING");
                                if (playBtn != null) _passed.Add("Lobby: PlayButton found");
                                else _errors.Add("Lobby: PlayButton MISSING");
                            }
                        }
                        if (!hasCanvas) _errors.Add("Lobby: No Canvas found");
                        if (!hasNewUI) _errors.Add("Lobby: NEW UI not present (old UI may still be showing)");

                        // Check services still alive after transition
                        if (Core.ServiceLocator.IsRegistered<Network.PlayFabService>())
                            _passed.Add("Services survived Boot→Lobby transition");
                        else
                            _errors.Add("Services DESTROYED during Boot→Lobby transition (DontDestroyOnLoad missing?)");

                        // Check GameManager persisted
                        if (Core.GameManager.Instance != null)
                            _passed.Add("GameManager survived scene transition");
                        else
                            _errors.Add("GameManager DESTROYED during scene transition");

                        _currentPhase = "done";
                    }
                    else if (elapsed > 12f)
                    {
                        _errors.Add($"Lobby scene not loaded after {elapsed:F1}s, active: {activeScene.name}");
                        _currentPhase = "timeout";
                    }
                    break;

                case "done":
                case "timeout":
                    EditorApplication.isPlaying = false;
                    break;
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private static void PrintResults()
        {
            Debug.Log("=== PLAY MODE TEST RESULTS ===");
            Debug.Log($"PASSED: {_passed.Count}  |  ERRORS: {_errors.Count}");
            foreach (var p in _passed)
                Debug.Log($"  [PASS] {p}");
            foreach (var e in _errors)
                Debug.LogError($"  [FAIL] {e}");
            if (_errors.Count == 0)
                Debug.Log("[PlayModeTest] ALL CHECKS PASSED!");
            else
                Debug.LogError($"[PlayModeTest] {_errors.Count} FAILURES detected.");
            Debug.Log("=== END TEST RESULTS ===");
        }
    }
}
#endif
