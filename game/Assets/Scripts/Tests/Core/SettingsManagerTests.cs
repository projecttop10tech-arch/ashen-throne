using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class SettingsManagerTests
    {
        private GameObject _go;
        private SettingsManager _settings;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("SettingsTest");
            _settings = _go.AddComponent<SettingsManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void DefaultMusicVolume_Is08()
        {
            Assert.AreEqual(0.8f, _settings.MusicVolume, 0.01f);
        }

        [Test]
        public void SetMusicVolume_ClampsTo01()
        {
            _settings.SetMusicVolume(1.5f);
            Assert.AreEqual(1.0f, _settings.MusicVolume, 0.01f);
        }

        [Test]
        public void SetMusicVolume_ClampsNegative()
        {
            _settings.SetMusicVolume(-0.5f);
            Assert.AreEqual(0.0f, _settings.MusicVolume, 0.01f);
        }

        [Test]
        public void SetSfxVolume_ClampsTo01()
        {
            _settings.SetSfxVolume(2.0f);
            Assert.AreEqual(1.0f, _settings.SfxVolume, 0.01f);
        }

        [Test]
        public void SetVoiceVolume_ClampsTo01()
        {
            _settings.SetVoiceVolume(1.5f);
            Assert.AreEqual(1.0f, _settings.VoiceVolume, 0.01f);
        }

        [Test]
        public void SetGraphicsQuality_SetsValue()
        {
            _settings.SetGraphicsQuality(SettingsManager.GraphicsQuality.High);
            Assert.AreEqual(SettingsManager.GraphicsQuality.High, _settings.Quality);
        }

        [Test]
        public void SetFrameRateTarget_ClampsMin30()
        {
            _settings.SetFrameRateTarget(10);
            Assert.AreEqual(30, _settings.FrameRateTarget);
        }

        [Test]
        public void SetFrameRateTarget_ClampsMax120()
        {
            _settings.SetFrameRateTarget(240);
            Assert.AreEqual(120, _settings.FrameRateTarget);
        }

        [Test]
        public void SetLanguage_IgnoresNull()
        {
            _settings.SetLanguage("fr");
            _settings.SetLanguage(null);
            Assert.AreEqual("fr", _settings.Language);
        }

        [Test]
        public void SetLanguage_IgnoresEmpty()
        {
            _settings.SetLanguage("de");
            _settings.SetLanguage("");
            Assert.AreEqual("de", _settings.Language);
        }

        [Test]
        public void SetNotificationsEnabled_Toggles()
        {
            _settings.SetNotificationsEnabled(false);
            Assert.IsFalse(_settings.NotificationsEnabled);
            _settings.SetNotificationsEnabled(true);
            Assert.IsTrue(_settings.NotificationsEnabled);
        }

        [Test]
        public void SettingsChanged_EventFires()
        {
            bool fired = false;
            var sub = EventBus.Subscribe<SettingsChangedEvent>(_ => fired = true);
            _settings.SetMusicVolume(0.5f);
            Assert.IsTrue(fired);
            sub.Dispose();
        }

        [Test]
        public void Register_InServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<SettingsManager>(out var svc));
            Assert.AreSame(_settings, svc);
        }
    }
}
