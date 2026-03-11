using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using AshenThrone.Core;

namespace AshenThrone.UI
{
    /// <summary>
    /// Attach to any GameObject with a Button component.
    /// On click, transitions to the target scene via GameManager (or direct SceneManager fallback).
    /// </summary>
    public class SceneNavigator : MonoBehaviour
    {
        [SerializeField] private SceneName targetScene;

        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(Navigate);
        }

        public void Navigate()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartCoroutine(GameManager.Instance.LoadSceneAsync(targetScene));
            }
            else
            {
                // Fallback: direct scene load when GameManager not present (e.g. testing scenes directly)
                SceneManager.LoadScene(targetScene.ToString());
            }
        }
    }
}
