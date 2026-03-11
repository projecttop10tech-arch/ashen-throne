#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Wires SceneNavigator components to UI buttons so they transition between scenes.
    /// Run after SceneUIGenerator to attach navigation behavior.
    /// Menu: AshenThrone → Wire Navigation
    /// </summary>
    public static class NavigationWirer
    {
        [MenuItem("AshenThrone/Wire Navigation")]
        public static void WireAll()
        {
            WireLobbyNavigation();
            WireEmpireNavigation();
            WireCombatNavigation();
            WireWorldMapNavigation();
            WireAllianceNavigation();
            Debug.Log("[NavigationWirer] All scene navigation buttons wired.");
        }

        [MenuItem("AshenThrone/Wire Navigation/Lobby")]
        static void WireLobbyNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby/Lobby.unity", OpenSceneMode.Single);
            WireButton(scene, "PlayButton", SceneName.Combat);
            WireButton(scene, "NavEmpire", SceneName.Empire);
            WireButton(scene, "NavHeroes", SceneName.Lobby); // stays on lobby (heroes panel)
            WireButton(scene, "NavBattle", SceneName.Combat);
            WireButton(scene, "NavAlliance", SceneName.Alliance);
            WireButton(scene, "PvPBtn", SceneName.Combat);
            WireButton(scene, "VoidRiftBtn", SceneName.Combat);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavigationWirer] Lobby navigation wired.");
        }

        [MenuItem("AshenThrone/Wire Navigation/Empire")]
        static void WireEmpireNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Empire/Empire.unity", OpenSceneMode.Single);
            WireButton(scene, "BattleBtn", SceneName.Combat);
            WireButton(scene, "HeroesBtn", SceneName.Lobby);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavigationWirer] Empire navigation wired.");
        }

        [MenuItem("AshenThrone/Wire Navigation/Combat")]
        static void WireCombatNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Combat/Combat.unity", OpenSceneMode.Single);
            // Victory/Defeat continue buttons go back to lobby
            WireButton(scene, "ContinueBtn", SceneName.Lobby);
            WireButton(scene, "QuitBtn", SceneName.Lobby);
            WireButton(scene, "RetryBtn", SceneName.Combat);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavigationWirer] Combat navigation wired.");
        }

        [MenuItem("AshenThrone/Wire Navigation/WorldMap")]
        static void WireWorldMapNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/WorldMap/WorldMap.unity", OpenSceneMode.Single);
            WireButton(scene, "BackBtn", SceneName.Lobby);
            WireButton(scene, "AttackBtn", SceneName.Combat);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavigationWirer] WorldMap navigation wired.");
        }

        [MenuItem("AshenThrone/Wire Navigation/Alliance")]
        static void WireAllianceNavigation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Alliance/Alliance.unity", OpenSceneMode.Single);
            WireButton(scene, "BackBtn", SceneName.Lobby);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NavigationWirer] Alliance navigation wired.");
        }

        static void WireButton(UnityEngine.SceneManagement.Scene scene, string buttonName, SceneName target)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDeep(root.transform, buttonName);
                if (found != null)
                {
                    // Remove existing SceneNavigator if any
                    var existing = found.GetComponent<UI.SceneNavigator>();
                    if (existing != null)
                        Object.DestroyImmediate(existing);

                    var nav = found.gameObject.AddComponent<UI.SceneNavigator>();
                    // Set the target scene via SerializedObject
                    var so = new SerializedObject(nav);
                    so.FindProperty("targetScene").enumValueIndex = (int)target;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            // Button not found is OK — some scenes may not have all buttons
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
    }
}
#endif
