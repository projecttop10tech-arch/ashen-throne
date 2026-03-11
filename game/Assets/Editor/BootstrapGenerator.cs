#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshenThrone.Editor
{
    public static class BootstrapGenerator
    {
        [MenuItem("AshenThrone/Setup Bootstrap")]
        public static void SetupAll()
        {
            SetupBuildSettings();
            SetupBootScene();
            Debug.Log("[BootstrapGenerator] Bootstrap complete.");
        }

        [MenuItem("AshenThrone/Setup Bootstrap/Build Settings")]
        public static void SetupBuildSettings()
        {
            var sceneNames = new[] { "Boot", "Lobby", "Combat", "Empire", "WorldMap", "Alliance" };
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (var name in sceneNames)
            {
                string path = $"Assets/Scenes/{name}/{name}.unity";
                if (System.IO.File.Exists(path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                else
                    Debug.LogWarning($"[BootstrapGenerator] Scene not found: {path}");
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[BootstrapGenerator] Added {scenes.Count} scenes to Build Settings (Boot=0).");
        }

        [MenuItem("AshenThrone/Setup Bootstrap/Boot Scene")]
        public static void SetupBootScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boot/Boot.unity", OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "GameManager" || root.name == "Services")
                    Object.DestroyImmediate(root);
            }

            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<Core.GameManager>();
            gmGo.AddComponent<Utils.MainThreadDispatcher>();

            var servicesGo = new GameObject("Services");
            servicesGo.transform.SetParent(gmGo.transform);

            AddService<Network.PlayFabService>(servicesGo);
            AddService<Network.ATTManager>(servicesGo);
            AddService<Network.CrashReporter>(servicesGo);
            AddService<Network.AnalyticsService>(servicesGo);
            AddService<Network.PhotonManager>(servicesGo);
            AddService<Network.DeepLinkHandler>(servicesGo);

            AddService<Empire.ResourceManager>(servicesGo);
            AddService<Empire.BuildingManager>(servicesGo);
            AddService<Empire.ResearchManager>(servicesGo);

            AddService<Heroes.HeroRoster>(servicesGo);

            AddService<Economy.BattlePassManager>(servicesGo);
            AddService<Economy.IAPManager>(servicesGo);
            AddService<Economy.GachaSystem>(servicesGo);
            AddService<Economy.HeroShardSystem>(servicesGo);
            AddService<Economy.QuestEngine>(servicesGo);

            AddService<Alliance.AllianceManager>(servicesGo);
            AddService<Alliance.WarEngine>(servicesGo);
            AddService<Alliance.TerritoryManager>(servicesGo);
            AddService<Alliance.AsyncPvpManager>(servicesGo);
            AddService<Alliance.AllianceChatManager>(servicesGo);
            AddService<Alliance.LeaderboardManager>(servicesGo);

            AddService<Events.EventEngine>(servicesGo);
            AddService<Events.WorldBossManager>(servicesGo);
            AddService<Events.VoidRiftManager>(servicesGo);
            AddService<Events.NotificationScheduler>(servicesGo);

            AddService<UI.Accessibility.AccessibilityManager>(servicesGo);
            AddService<UI.Tutorial.TutorialManager>(servicesGo);
            AddService<UI.Localization.LocalizationBootstrap>(servicesGo);
            AddService<Core.SettingsManager>(servicesGo);
            AddService<Core.HapticFeedbackManager>(servicesGo);
            AddService<UI.PrivacyConsentManager>(servicesGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BootstrapGenerator] Boot scene populated with GameManager + 30 services.");
        }

        private static T AddService<T>(GameObject parent) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(parent.transform);
            return go.AddComponent<T>();
        }
    }
}
#endif
