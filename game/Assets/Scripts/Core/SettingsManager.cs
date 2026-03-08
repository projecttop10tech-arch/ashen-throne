using System;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Persists all player settings via PlayerPrefs. Categories: Audio, Graphics, Accessibility, Language.
    /// Publishes SettingsChangedEvent on any change so UI panels and systems can react.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        private const string KeyMusicVolume = "settings_music_volume";
        private const string KeySfxVolume = "settings_sfx_volume";
        private const string KeyVoiceVolume = "settings_voice_volume";
        private const string KeyGraphicsQuality = "settings_graphics_quality";
        private const string KeyLanguage = "settings_language";
        private const string KeyFrameRateTarget = "settings_fps_target";
        private const string KeyNotificationsEnabled = "settings_notifications";

        public enum GraphicsQuality { Low, Medium, High }

        // Audio
        public float MusicVolume { get; private set; } = 0.8f;
        public float SfxVolume { get; private set; } = 1.0f;
        public float VoiceVolume { get; private set; } = 1.0f;

        // Graphics
        public GraphicsQuality Quality { get; private set; } = GraphicsQuality.Medium;
        public int FrameRateTarget { get; private set; } = 60;

        // General
        public string Language { get; private set; } = "en";
        public bool NotificationsEnabled { get; private set; } = true;

        private void Awake()
        {
            ServiceLocator.Register<SettingsManager>(this);
            LoadFromPrefs();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<SettingsManager>();
        }

        public void SetMusicVolume(float volume)
        {
            MusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
            PublishChanged();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PublishChanged();
        }

        public void SetVoiceVolume(float volume)
        {
            VoiceVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KeyVoiceVolume, VoiceVolume);
            PublishChanged();
        }

        public void SetGraphicsQuality(GraphicsQuality quality)
        {
            Quality = quality;
            PlayerPrefs.SetInt(KeyGraphicsQuality, (int)quality);
            QualitySettings.SetQualityLevel((int)quality);
            PublishChanged();
        }

        public void SetFrameRateTarget(int fps)
        {
            FrameRateTarget = Mathf.Clamp(fps, 30, 120);
            Application.targetFrameRate = FrameRateTarget;
            PlayerPrefs.SetInt(KeyFrameRateTarget, FrameRateTarget);
            PublishChanged();
        }

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return;
            Language = languageCode;
            PlayerPrefs.SetString(KeyLanguage, Language);
            PublishChanged();
        }

        public void SetNotificationsEnabled(bool enabled)
        {
            NotificationsEnabled = enabled;
            PlayerPrefs.SetInt(KeyNotificationsEnabled, enabled ? 1 : 0);
            PublishChanged();
        }

        public void SaveAll()
        {
            PlayerPrefs.Save();
        }

        private void LoadFromPrefs()
        {
            MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, 1.0f);
            VoiceVolume = PlayerPrefs.GetFloat(KeyVoiceVolume, 1.0f);
            Quality = (GraphicsQuality)PlayerPrefs.GetInt(KeyGraphicsQuality, (int)GraphicsQuality.Medium);
            FrameRateTarget = PlayerPrefs.GetInt(KeyFrameRateTarget, 60);
            Language = PlayerPrefs.GetString(KeyLanguage, "en");
            NotificationsEnabled = PlayerPrefs.GetInt(KeyNotificationsEnabled, 1) == 1;

            Application.targetFrameRate = FrameRateTarget;
            QualitySettings.SetQualityLevel((int)Quality);
        }

        private void PublishChanged()
        {
            EventBus.Publish(new SettingsChangedEvent());
        }
    }

    /// <summary>Fired when any setting changes. Listeners should re-read SettingsManager properties.</summary>
    public readonly struct SettingsChangedEvent { }
}
