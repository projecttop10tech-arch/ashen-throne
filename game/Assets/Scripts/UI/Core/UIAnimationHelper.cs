using System;
using System.Collections;
using UnityEngine;

namespace AshenThrone.UI
{
    /// <summary>
    /// Lightweight UI animation utilities. Attach to any UI GameObject.
    /// Respects AccessibilityManager.ReduceMotion — when true, animations skip to end state.
    /// </summary>
    public class UIAnimationHelper : MonoBehaviour
    {
        /// <summary>Slide a RectTransform from off-screen to its anchored position.</summary>
        public Coroutine SlideIn(RectTransform target, Vector2 fromOffset, float duration, Action onComplete = null)
        {
            if (target == null) return null;
            if (ShouldSkipAnimation(ref duration))
            {
                onComplete?.Invoke();
                return null;
            }
            return StartCoroutine(SlideRoutine(target, target.anchoredPosition + fromOffset, target.anchoredPosition, duration, onComplete));
        }

        /// <summary>Slide a RectTransform from its current position to an offset.</summary>
        public Coroutine SlideOut(RectTransform target, Vector2 toOffset, float duration, Action onComplete = null)
        {
            if (target == null) return null;
            var start = target.anchoredPosition;
            if (ShouldSkipAnimation(ref duration))
            {
                target.anchoredPosition = start + toOffset;
                onComplete?.Invoke();
                return null;
            }
            return StartCoroutine(SlideRoutine(target, start, start + toOffset, duration, onComplete));
        }

        /// <summary>Fade a CanvasGroup alpha from current to target.</summary>
        public Coroutine FadeAlpha(CanvasGroup group, float targetAlpha, float duration, Action onComplete = null)
        {
            if (group == null) return null;
            if (ShouldSkipAnimation(ref duration))
            {
                group.alpha = targetAlpha;
                onComplete?.Invoke();
                return null;
            }
            return StartCoroutine(FadeRoutine(group, group.alpha, targetAlpha, duration, onComplete));
        }

        /// <summary>Scale punch effect (e.g., card play, level up).</summary>
        public Coroutine ScalePunch(Transform target, float punchScale, float duration, Action onComplete = null)
        {
            if (target == null) return null;
            if (ShouldSkipAnimation(ref duration))
            {
                onComplete?.Invoke();
                return null;
            }
            return StartCoroutine(ScalePunchRoutine(target, punchScale, duration, onComplete));
        }

        /// <summary>Lerp a fill amount (HP bars, energy bars).</summary>
        public Coroutine LerpFill(UnityEngine.UI.Image fillImage, float targetFill, float duration, Action onComplete = null)
        {
            if (fillImage == null) return null;
            if (ShouldSkipAnimation(ref duration))
            {
                fillImage.fillAmount = targetFill;
                onComplete?.Invoke();
                return null;
            }
            return StartCoroutine(FillRoutine(fillImage, fillImage.fillAmount, targetFill, duration, onComplete));
        }

        private bool ShouldSkipAnimation(ref float duration)
        {
            if (Core.ServiceLocator.TryGet<Accessibility.AccessibilityManager>(out var mgr) && mgr.ReduceMotion)
            {
                duration = 0f;
                return true;
            }
            return duration <= 0f;
        }

        private static IEnumerator SlideRoutine(RectTransform target, Vector2 from, Vector2 to, float duration, Action onComplete)
        {
            target.anchoredPosition = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            target.anchoredPosition = to;
            onComplete?.Invoke();
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
            onComplete?.Invoke();
        }

        private static IEnumerator ScalePunchRoutine(Transform target, float punchScale, float duration, Action onComplete)
        {
            Vector3 original = target.localScale;
            Vector3 punched = original * punchScale;
            float half = duration * 0.5f;

            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(original, punched, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                target.localScale = Vector3.Lerp(punched, original, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            target.localScale = original;
            onComplete?.Invoke();
        }

        private static IEnumerator FillRoutine(UnityEngine.UI.Image img, float from, float to, float duration, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                img.fillAmount = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            img.fillAmount = to;
            onComplete?.Invoke();
        }
    }
}
