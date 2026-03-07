using System;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.UI.Accessibility
{
    // ---------------------------------------------------------------------------
    // Colorblind mode enum
    // ---------------------------------------------------------------------------

    /// <summary>Colorblind simulation/correction modes (check #31 WCAG compliance).</summary>
    public enum ColorblindMode
    {
        None,
        Protanopia,   // Red-blind
        Deuteranopia, // Green-blind
        Tritanopia    // Blue-blind
    }

    // ---------------------------------------------------------------------------
    // Accessibility save data
    // ---------------------------------------------------------------------------

    /// <summary>Serializable accessibility preferences for persistence.</summary>
    [Serializable]
    public class AccessibilitySaveData
    {
        public int ColorblindModeIndex;
        public float TextSizeScale = 1.0f;
        public bool HapticsEnabled = true;
        public bool ScreenReaderEnabled;
        public bool ReduceMotion;
        public float UiBrightness = 1.0f;
    }

    // ---------------------------------------------------------------------------
    // AccessibilityConfig — default values as ScriptableObject (check #8)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Default accessibility settings. Designers can override defaults per-region.
    /// </summary>
    [CreateAssetMenu(fileName = "AccessibilityConfig", menuName = "AshenThrone/Config/AccessibilityConfig")]
    public class AccessibilityConfig : ScriptableObject
    {
        /// <summary>Minimum text size multiplier (0.8 = 80% of base).</summary>
        [field: SerializeField] public float MinTextScale { get; private set; } = 0.8f;

        /// <summary>Maximum text size multiplier (1.5 = 150% of base).</summary>
        [field: SerializeField] public float MaxTextScale { get; private set; } = 1.5f;

        /// <summary>Default text size multiplier.</summary>
        [field: SerializeField] public float DefaultTextScale { get; private set; } = 1.0f;

        /// <summary>Whether haptics are enabled by default.</summary>
        [field: SerializeField] public bool DefaultHapticsEnabled { get; private set; } = true;

        /// <summary>Minimum UI brightness multiplier.</summary>
        [field: SerializeField] public float MinBrightness { get; private set; } = 0.7f;

        /// <summary>Maximum UI brightness multiplier.</summary>
        [field: SerializeField] public float MaxBrightness { get; private set; } = 1.3f;
    }

    // ---------------------------------------------------------------------------
    // AccessibilityManager
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Manages runtime accessibility settings: colorblind mode, text scaling,
    /// haptic feedback, motion reduction, and screen reader support.
    ///
    /// Architecture:
    /// - Config loaded from AccessibilityConfig ScriptableObject (check #8).
    /// - Settings persisted via AccessibilitySaveData to PlayFab.
    /// - Publishes AccessibilitySettingsChangedEvent on any change so UI can react.
    /// - Registered in ServiceLocator; other systems query via ServiceLocator.Get.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        [SerializeField] private AccessibilityConfig _config;

        private ColorblindMode _colorblindMode = ColorblindMode.None;
        private float _textSizeScale = 1.0f;
        private bool _hapticsEnabled = true;
        private bool _screenReaderEnabled;
        private bool _reduceMotion;
        private float _uiBrightness = 1.0f;

        /// <summary>Current colorblind correction mode.</summary>
        public ColorblindMode ColorblindMode => _colorblindMode;

        /// <summary>Current text size multiplier (0.8–1.5).</summary>
        public float TextSizeScale => _textSizeScale;

        /// <summary>Whether haptic feedback is enabled.</summary>
        public bool HapticsEnabled => _hapticsEnabled;

        /// <summary>Whether screen reader hints are enabled.</summary>
        public bool ScreenReaderEnabled => _screenReaderEnabled;

        /// <summary>Whether motion-heavy animations should be reduced.</summary>
        public bool ReduceMotion => _reduceMotion;

        /// <summary>UI brightness multiplier (0.7–1.3).</summary>
        public float UiBrightness => _uiBrightness;

        private void Awake()
        {
            ServiceLocator.Register<AccessibilityManager>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AccessibilityManager>();
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------

        /// <summary>
        /// Load accessibility settings from save data. Pass null for defaults.
        /// </summary>
        public void Initialize(AccessibilitySaveData save)
        {
            float minScale = _config != null ? _config.MinTextScale : 0.8f;
            float maxScale = _config != null ? _config.MaxTextScale : 1.5f;
            float defaultScale = _config != null ? _config.DefaultTextScale : 1.0f;
            bool defaultHaptics = _config == null || _config.DefaultHapticsEnabled;
            float minBright = _config != null ? _config.MinBrightness : 0.7f;
            float maxBright = _config != null ? _config.MaxBrightness : 1.3f;

            if (save == null)
            {
                _colorblindMode = ColorblindMode.None;
                _textSizeScale = defaultScale;
                _hapticsEnabled = defaultHaptics;
                _screenReaderEnabled = false;
                _reduceMotion = false;
                _uiBrightness = 1.0f;
                return;
            }

            _colorblindMode = (ColorblindMode)Mathf.Clamp(save.ColorblindModeIndex, 0, 3);
            _textSizeScale = Mathf.Clamp(save.TextSizeScale, minScale, maxScale);
            _hapticsEnabled = save.HapticsEnabled;
            _screenReaderEnabled = save.ScreenReaderEnabled;
            _reduceMotion = save.ReduceMotion;
            _uiBrightness = Mathf.Clamp(save.UiBrightness, minBright, maxBright);
        }

        // -------------------------------------------------------------------
        // Setters — each fires an event so UI can react
        // -------------------------------------------------------------------

        /// <summary>Set the colorblind correction mode.</summary>
        public void SetColorblindMode(ColorblindMode mode)
        {
            if (_colorblindMode == mode) return;
            _colorblindMode = mode;
            PublishChanged();
        }

        /// <summary>Set the text size multiplier. Clamped to config min/max.</summary>
        public void SetTextSizeScale(float scale)
        {
            float min = _config != null ? _config.MinTextScale : 0.8f;
            float max = _config != null ? _config.MaxTextScale : 1.5f;
            float clamped = Mathf.Clamp(scale, min, max);
            if (Mathf.Approximately(_textSizeScale, clamped)) return;
            _textSizeScale = clamped;
            PublishChanged();
        }

        /// <summary>Enable or disable haptic feedback.</summary>
        public void SetHapticsEnabled(bool enabled)
        {
            if (_hapticsEnabled == enabled) return;
            _hapticsEnabled = enabled;
            PublishChanged();
        }

        /// <summary>Enable or disable screen reader hints.</summary>
        public void SetScreenReaderEnabled(bool enabled)
        {
            if (_screenReaderEnabled == enabled) return;
            _screenReaderEnabled = enabled;
            PublishChanged();
        }

        /// <summary>Enable or disable motion reduction.</summary>
        public void SetReduceMotion(bool enabled)
        {
            if (_reduceMotion == enabled) return;
            _reduceMotion = enabled;
            PublishChanged();
        }

        /// <summary>Set UI brightness multiplier. Clamped to config min/max.</summary>
        public void SetUiBrightness(float brightness)
        {
            float min = _config != null ? _config.MinBrightness : 0.7f;
            float max = _config != null ? _config.MaxBrightness : 1.3f;
            float clamped = Mathf.Clamp(brightness, min, max);
            if (Mathf.Approximately(_uiBrightness, clamped)) return;
            _uiBrightness = clamped;
            PublishChanged();
        }

        // -------------------------------------------------------------------
        // Save / Load
        // -------------------------------------------------------------------

        /// <summary>Build save data for persistence.</summary>
        public AccessibilitySaveData BuildSaveData()
        {
            return new AccessibilitySaveData
            {
                ColorblindModeIndex = (int)_colorblindMode,
                TextSizeScale = _textSizeScale,
                HapticsEnabled = _hapticsEnabled,
                ScreenReaderEnabled = _screenReaderEnabled,
                ReduceMotion = _reduceMotion,
                UiBrightness = _uiBrightness
            };
        }

        // -------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------

        private void PublishChanged()
        {
            EventBus.Publish(new AccessibilitySettingsChangedEvent(
                _colorblindMode, _textSizeScale, _hapticsEnabled,
                _screenReaderEnabled, _reduceMotion, _uiBrightness));
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Fired whenever any accessibility setting changes.</summary>
    public readonly struct AccessibilitySettingsChangedEvent
    {
        public readonly ColorblindMode ColorblindMode;
        public readonly float TextSizeScale;
        public readonly bool HapticsEnabled;
        public readonly bool ScreenReaderEnabled;
        public readonly bool ReduceMotion;
        public readonly float UiBrightness;

        public AccessibilitySettingsChangedEvent(
            ColorblindMode cb, float text, bool haptic,
            bool reader, bool motion, float bright)
        {
            ColorblindMode = cb;
            TextSizeScale = text;
            HapticsEnabled = haptic;
            ScreenReaderEnabled = reader;
            ReduceMotion = motion;
            UiBrightness = bright;
        }
    }
}
