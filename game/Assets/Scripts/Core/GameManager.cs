using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenThrone.Core
{
    /// <summary>
    /// Central singleton managing application lifecycle, scene transitions, and boot sequence.
    /// Persists across all scenes. All systems register through ServiceLocator, not GameManager directly.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public AppState CurrentState { get; private set; } = AppState.Boot;

        public event Action<AppState, AppState> OnStateChanged;

        [SerializeField] private float sceneTransitionFadeSeconds = 0.4f;

        private bool _isTransitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeCoreServices();
        }

        private void InitializeCoreServices()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
        }

        private void Start()
        {
            StartCoroutine(BootSequence());
        }

        private IEnumerator BootSequence()
        {
            TransitionTo(AppState.Boot);
            yield return StartCoroutine(ServiceLocator.Get<Network.PlayFabService>().AuthenticateAsync());
            TransitionTo(AppState.Lobby);
            yield return LoadSceneAsync(SceneName.Lobby);
        }

        /// <summary>
        /// Transitions the app to a new state and fires OnStateChanged event.
        /// </summary>
        public void TransitionTo(AppState newState)
        {
            if (CurrentState == newState) return;
            AppState previous = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(previous, newState);
            EventBus.Publish(new AppStateChangedEvent(previous, newState));
        }

        /// <summary>
        /// Asynchronously loads a scene with fade transition. Prevents double-loading.
        /// </summary>
        public IEnumerator LoadSceneAsync(SceneName sceneName)
        {
            if (_isTransitioning) yield break;
            _isTransitioning = true;

            EventBus.Publish(new SceneTransitionStartedEvent(sceneName));
            yield return new WaitForSeconds(sceneTransitionFadeSeconds);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName.ToString());
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
                yield return null;

            op.allowSceneActivation = true;
            yield return new WaitUntil(() => op.isDone);

            EventBus.Publish(new SceneTransitionCompletedEvent(sceneName));
            _isTransitioning = false;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            EventBus.Publish(new AppPauseEvent(pauseStatus));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            EventBus.Publish(new AppFocusEvent(hasFocus));
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                EventBus.Shutdown();
                ServiceLocator.Shutdown();
            }
        }
    }

    public enum AppState
    {
        Boot,
        Lobby,
        Empire,
        Combat,
        WorldMap,
        Alliance,
        Loading
    }

    public enum SceneName
    {
        Boot,
        Lobby,
        Empire,
        Combat,
        WorldMap,
        Alliance
    }

    // --- Events ---
    public readonly struct AppStateChangedEvent { public readonly AppState Previous; public readonly AppState Next; public AppStateChangedEvent(AppState prev, AppState next) { Previous = prev; Next = next; } }
    public readonly struct SceneTransitionStartedEvent { public readonly SceneName Scene; public SceneTransitionStartedEvent(SceneName s) { Scene = s; } }
    public readonly struct SceneTransitionCompletedEvent { public readonly SceneName Scene; public SceneTransitionCompletedEvent(SceneName s) { Scene = s; } }
    public readonly struct AppPauseEvent { public readonly bool IsPaused; public AppPauseEvent(bool p) { IsPaused = p; } }
    public readonly struct AppFocusEvent { public readonly bool HasFocus; public AppFocusEvent(bool f) { HasFocus = f; } }
}
