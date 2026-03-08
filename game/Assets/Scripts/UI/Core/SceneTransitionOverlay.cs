using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AshenThrone.Core;

namespace AshenThrone.UI
{
    /// <summary>
    /// Fullscreen fade overlay for scene transitions. Listens to SceneTransition events.
    /// Place on a Canvas with high sort order in the Boot scene (DontDestroyOnLoad).
    /// </summary>
    public class SceneTransitionOverlay : MonoBehaviour
    {
        [SerializeField] private Image _fadeImage;
        [SerializeField] private float _fadeDuration = 0.4f;
        [SerializeField] private GameObject _loadingSpinner;

        private EventSubscription _startSub;
        private EventSubscription _completeSub;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (_fadeImage != null)
            {
                var c = _fadeImage.color;
                c.a = 0f;
                _fadeImage.color = c;
                _fadeImage.raycastTarget = false;
            }
            if (_loadingSpinner != null)
                _loadingSpinner.SetActive(false);
        }

        private void OnEnable()
        {
            _startSub = EventBus.Subscribe<SceneTransitionStartedEvent>(OnTransitionStarted);
            _completeSub = EventBus.Subscribe<SceneTransitionCompletedEvent>(OnTransitionCompleted);
        }

        private void OnDisable()
        {
            _startSub?.Dispose();
            _completeSub?.Dispose();
        }

        private void OnTransitionStarted(SceneTransitionStartedEvent e)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeToBlack());
        }

        private void OnTransitionCompleted(SceneTransitionCompletedEvent e)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeFromBlack());
        }

        private IEnumerator FadeToBlack()
        {
            if (_fadeImage == null) yield break;
            _fadeImage.raycastTarget = true;
            if (_loadingSpinner != null) _loadingSpinner.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var c = _fadeImage.color;
                c.a = Mathf.Clamp01(elapsed / _fadeDuration);
                _fadeImage.color = c;
                yield return null;
            }

            var final = _fadeImage.color;
            final.a = 1f;
            _fadeImage.color = final;
        }

        private IEnumerator FadeFromBlack()
        {
            if (_fadeImage == null) yield break;

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var c = _fadeImage.color;
                c.a = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
                _fadeImage.color = c;
                yield return null;
            }

            var final = _fadeImage.color;
            final.a = 0f;
            _fadeImage.color = final;
            _fadeImage.raycastTarget = false;
            if (_loadingSpinner != null) _loadingSpinner.SetActive(false);
        }
    }
}
