using UnityEngine;
using AshenThrone.UI.Accessibility;

namespace AshenThrone.Core
{
    /// <summary>
    /// Centralized haptic feedback for mobile. Checks AccessibilityManager.HapticsEnabled
    /// before triggering. Uses Unity's Handheld.Vibrate as a stub — production should
    /// use platform-specific APIs for nuanced haptic patterns.
    /// </summary>
    public class HapticFeedbackManager : MonoBehaviour
    {
        public enum HapticIntensity { Light, Medium, Heavy }

        private EventSubscription _cardPlayedSub;
        private EventSubscription _heroDiedSub;
        private EventSubscription _buildCompleteSub;
        private EventSubscription _buildTappedSub;
        private EventSubscription _speedupSub;
        private EventSubscription _moveStartedSub;
        private EventSubscription _levelUpSub;
        private EventSubscription _gachaPullSub;
        private EventSubscription _collectSub;
        private EventSubscription _placeSub;

        private void Awake()
        {
            ServiceLocator.Register<HapticFeedbackManager>(this);
        }

        private void OnEnable()
        {
            _cardPlayedSub = EventBus.Subscribe<Combat.CardPlayedEvent>(_ => TriggerHaptic(HapticIntensity.Light));
            _heroDiedSub = EventBus.Subscribe<Combat.HeroDiedEvent>(_ => TriggerHaptic(HapticIntensity.Heavy));
            _buildCompleteSub = EventBus.Subscribe<Empire.BuildingUpgradeCompletedEvent>(_ => TriggerHaptic(HapticIntensity.Medium));
            _buildTappedSub = EventBus.Subscribe<Empire.BuildingTappedEvent>(_ => TriggerHaptic(HapticIntensity.Light));
            _speedupSub = EventBus.Subscribe<Empire.SpeedupAppliedEvent>(_ => TriggerHaptic(HapticIntensity.Light));
            _moveStartedSub = EventBus.Subscribe<Empire.BuildingMoveStartedEvent>(_ => TriggerHaptic(HapticIntensity.Medium));
            _levelUpSub = EventBus.Subscribe<Heroes.HeroLeveledUpEvent>(_ => TriggerHaptic(HapticIntensity.Medium));
            _gachaPullSub = EventBus.Subscribe<Economy.GachaPullConfirmedEvent>(_ => TriggerHaptic(HapticIntensity.Heavy));
            _collectSub = EventBus.Subscribe<Empire.ResourceCollectedEvent>(_ => TriggerHaptic(HapticIntensity.Light));
            _placeSub = EventBus.Subscribe<Empire.BuildingPlacedEvent>(_ => TriggerHaptic(HapticIntensity.Medium));
        }

        private void OnDisable()
        {
            _cardPlayedSub?.Dispose();
            _heroDiedSub?.Dispose();
            _buildCompleteSub?.Dispose();
            _buildTappedSub?.Dispose();
            _speedupSub?.Dispose();
            _moveStartedSub?.Dispose();
            _levelUpSub?.Dispose();
            _gachaPullSub?.Dispose();
            _collectSub?.Dispose();
            _placeSub?.Dispose();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<HapticFeedbackManager>();
        }

        /// <summary>
        /// Trigger haptic feedback at the given intensity.
        /// No-ops if haptics are disabled in accessibility settings or on unsupported platforms.
        /// </summary>
        public void TriggerHaptic(HapticIntensity intensity)
        {
            if (!IsHapticsEnabled()) return;

#if UNITY_IOS || UNITY_ANDROID
            // Stub: Unity's basic vibration. Production should use CoreHaptics (iOS)
            // or VibrationEffect (Android) for intensity-based patterns.
            switch (intensity)
            {
                case HapticIntensity.Light:
                    Handheld.Vibrate();
                    break;
                case HapticIntensity.Medium:
                    Handheld.Vibrate();
                    break;
                case HapticIntensity.Heavy:
                    Handheld.Vibrate();
                    break;
            }
#endif
            Debug.Log($"[Haptic] {intensity}");
        }

        private bool IsHapticsEnabled()
        {
            if (ServiceLocator.TryGet<AccessibilityManager>(out var mgr))
                return mgr.HapticsEnabled;
            return true; // Default enabled if no AccessibilityManager
        }
    }
}
