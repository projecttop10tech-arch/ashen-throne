using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.UI.Localization
{
    // ---------------------------------------------------------------------------
    // Supported languages
    // ---------------------------------------------------------------------------

    /// <summary>Languages supported at launch (8 languages — Phase 6 target).</summary>
    public enum GameLanguage
    {
        English,
        Spanish,
        French,
        German,
        Portuguese,
        Japanese,
        Korean,
        ChineseSimplified
    }

    // ---------------------------------------------------------------------------
    // Localization save data
    // ---------------------------------------------------------------------------

    /// <summary>Serializable language preference for persistence.</summary>
    [Serializable]
    public class LocalizationSaveData
    {
        /// <summary>Index into GameLanguage enum. -1 means auto-detect.</summary>
        public int LanguageIndex = -1;
    }

    // ---------------------------------------------------------------------------
    // LocalizationBootstrap
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Initializes the localization system and manages language selection.
    ///
    /// Architecture:
    /// - Wraps Unity Localization package (com.unity.localization) setup.
    /// - Auto-detects device language on first launch; player can override in settings.
    /// - Stores preference in LocalizationSaveData (persisted to PlayFab).
    /// - Fires LanguageChangedEvent so all UI can rebuild strings.
    /// - Registered in ServiceLocator for cross-system access.
    ///
    /// Supported languages at launch: EN, ES, FR, DE, PT, JA, KO, ZH-CN.
    /// </summary>
    public class LocalizationBootstrap : MonoBehaviour
    {
        /// <summary>Number of supported languages at launch.</summary>
        public const int SupportedLanguageCount = 8;

        private GameLanguage _currentLanguage = GameLanguage.English;
        private bool _isInitialized;

        /// <summary>The currently active language.</summary>
        public GameLanguage CurrentLanguage => _currentLanguage;

        /// <summary>Whether the localization system has been initialized.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>ISO 639-1 code for the current language.</summary>
        public string CurrentLanguageCode => GetLanguageCode(_currentLanguage);

        private void Awake()
        {
            ServiceLocator.Register<LocalizationBootstrap>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<LocalizationBootstrap>();
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------

        /// <summary>
        /// Initialize with saved preference. Pass null for auto-detection.
        /// </summary>
        public void Initialize(LocalizationSaveData save)
        {
            if (save != null && save.LanguageIndex >= 0 && save.LanguageIndex < SupportedLanguageCount)
            {
                _currentLanguage = (GameLanguage)save.LanguageIndex;
            }
            else
            {
                _currentLanguage = DetectDeviceLanguage();
            }

            _isInitialized = true;
            ApplyLanguage(_currentLanguage);
        }

        /// <summary>
        /// Change the active language. Fires LanguageChangedEvent.
        /// </summary>
        public void SetLanguage(GameLanguage language)
        {
            if (_currentLanguage == language && _isInitialized) return;
            _currentLanguage = language;
            ApplyLanguage(language);
        }

        /// <summary>
        /// Returns all supported languages as an array.
        /// </summary>
        public static GameLanguage[] GetSupportedLanguages()
        {
            return (GameLanguage[])Enum.GetValues(typeof(GameLanguage));
        }

        /// <summary>
        /// Returns the display name for a language (in that language — for the settings UI).
        /// </summary>
        public static string GetDisplayName(GameLanguage language)
        {
            return language switch
            {
                GameLanguage.English            => "English",
                GameLanguage.Spanish            => "Espa\u00f1ol",
                GameLanguage.French             => "Fran\u00e7ais",
                GameLanguage.German             => "Deutsch",
                GameLanguage.Portuguese         => "Portugu\u00eas",
                GameLanguage.Japanese           => "\u65e5\u672c\u8a9e",
                GameLanguage.Korean             => "\ud55c\uad6d\uc5b4",
                GameLanguage.ChineseSimplified  => "\u7b80\u4f53\u4e2d\u6587",
                _ => "English"
            };
        }

        /// <summary>
        /// Returns the ISO 639-1 language code.
        /// </summary>
        public static string GetLanguageCode(GameLanguage language)
        {
            return language switch
            {
                GameLanguage.English            => "en",
                GameLanguage.Spanish            => "es",
                GameLanguage.French             => "fr",
                GameLanguage.German             => "de",
                GameLanguage.Portuguese         => "pt",
                GameLanguage.Japanese           => "ja",
                GameLanguage.Korean             => "ko",
                GameLanguage.ChineseSimplified  => "zh",
                _ => "en"
            };
        }

        /// <summary>Build save data for persistence.</summary>
        public LocalizationSaveData BuildSaveData()
        {
            return new LocalizationSaveData
            {
                LanguageIndex = (int)_currentLanguage
            };
        }

        // -------------------------------------------------------------------
        // Private
        // -------------------------------------------------------------------

        /// <summary>
        /// Detect the device language and map to the nearest supported GameLanguage.
        /// Falls back to English if the device language is not supported.
        /// </summary>
        private static GameLanguage DetectDeviceLanguage()
        {
            SystemLanguage sysLang = Application.systemLanguage;
            return sysLang switch
            {
                SystemLanguage.English    => GameLanguage.English,
                SystemLanguage.Spanish    => GameLanguage.Spanish,
                SystemLanguage.French     => GameLanguage.French,
                SystemLanguage.German     => GameLanguage.German,
                SystemLanguage.Portuguese => GameLanguage.Portuguese,
                SystemLanguage.Japanese   => GameLanguage.Japanese,
                SystemLanguage.Korean     => GameLanguage.Korean,
                SystemLanguage.Chinese    => GameLanguage.ChineseSimplified,
                SystemLanguage.ChineseSimplified  => GameLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional => GameLanguage.ChineseSimplified,
                _ => GameLanguage.English
            };
        }

        private void ApplyLanguage(GameLanguage language)
        {
            // In production, this sets Unity Localization's selected locale:
            //   var locale = LocalizationSettings.AvailableLocales.Locales
            //       .Find(l => l.Identifier.Code == GetLanguageCode(language));
            //   LocalizationSettings.SelectedLocale = locale;

            EventBus.Publish(new LanguageChangedEvent(language, GetLanguageCode(language)));
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>Fired when the active language changes.</summary>
    public readonly struct LanguageChangedEvent
    {
        public readonly GameLanguage Language;
        public readonly string LanguageCode;
        public LanguageChangedEvent(GameLanguage lang, string code)
        { Language = lang; LanguageCode = code; }
    }
}
