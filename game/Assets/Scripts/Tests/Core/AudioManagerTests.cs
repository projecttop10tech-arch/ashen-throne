using NUnit.Framework;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Tests.Core
{
    [TestFixture]
    public class AudioManagerTests
    {
        private GameObject _go;
        private AudioManager _audio;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Initialize();
            EventBus.Initialize();
            _go = new GameObject("AudioTest");
            _audio = _go.AddComponent<AudioManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            EventBus.Shutdown();
            ServiceLocator.Shutdown();
        }

        [Test]
        public void Register_InServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.TryGet<AudioManager>(out var svc));
            Assert.AreSame(_audio, svc);
        }

        [Test]
        public void DefaultVolumes_AreReasonable()
        {
            Assert.GreaterOrEqual(_audio.MusicVolume, 0f);
            Assert.LessOrEqual(_audio.MusicVolume, 1f);
            Assert.GreaterOrEqual(_audio.SfxVolume, 0f);
            Assert.LessOrEqual(_audio.SfxVolume, 1f);
        }

        [Test]
        public void SetMusicVolume_ClampsTo01()
        {
            _audio.SetMusicVolume(1.5f);
            Assert.AreEqual(1.0f, _audio.MusicVolume, 0.01f);
            _audio.SetMusicVolume(-0.5f);
            Assert.AreEqual(0.0f, _audio.MusicVolume, 0.01f);
        }

        [Test]
        public void SetSfxVolume_ClampsTo01()
        {
            _audio.SetSfxVolume(2.0f);
            Assert.AreEqual(1.0f, _audio.SfxVolume, 0.01f);
        }

        [Test]
        public void SetVoiceVolume_ClampsTo01()
        {
            _audio.SetVoiceVolume(1.5f);
            Assert.AreEqual(1.0f, _audio.VoiceVolume, 0.01f);
        }

        [Test]
        public void PlaySfx_NullClip_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.PlaySfx(null));
        }

        [Test]
        public void PlayVoice_NullClip_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.PlayVoice(null));
        }

        [Test]
        public void PlayMusic_NullClip_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.PlayMusic(null));
        }

        [Test]
        public void StopMusic_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.StopMusic());
        }

        [Test]
        public void StopAllSfx_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.StopAllSfx());
        }

        [Test]
        public void SyncsWithSettingsManager()
        {
            var settingsGo = new GameObject("Settings");
            var settings = settingsGo.AddComponent<SettingsManager>();
            settings.SetMusicVolume(0.3f);
            settings.SetSfxVolume(0.5f);
            settings.SetVoiceVolume(0.7f);

            // Trigger settings sync via event
            EventBus.Publish(new SettingsChangedEvent());

            Assert.AreEqual(0.3f, _audio.MusicVolume, 0.01f);
            Assert.AreEqual(0.5f, _audio.SfxVolume, 0.01f);
            Assert.AreEqual(0.7f, _audio.VoiceVolume, 0.01f);

            Object.DestroyImmediate(settingsGo);
        }
    }
}
