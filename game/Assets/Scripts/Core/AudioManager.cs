using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Centralized audio system. Manages music crossfades, SFX pooling, and per-category
    /// volume control. Reads volumes from SettingsManager. Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSourceA;
        [SerializeField] private AudioSource _musicSourceB;

        [Header("Settings")]
        [SerializeField] private float _crossfadeDuration = 1.5f;
        [SerializeField] private int _sfxPoolSize = 16;

        private readonly List<AudioSource> _sfxPool = new();
        private AudioSource _activeMusicSource;
        private Coroutine _crossfadeCoroutine;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1.0f;
        private float _voiceVolume = 1.0f;

        private EventSubscription _settingsChangedSub;
        private EventSubscription _sceneTransitionSub;

        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;
        public float VoiceVolume => _voiceVolume;
        public bool IsMusicPlaying => _activeMusicSource != null && _activeMusicSource.isPlaying;

        private void Awake()
        {
            ServiceLocator.Register<AudioManager>(this);
            DontDestroyOnLoad(gameObject);
            InitializeSfxPool();
            SyncVolumesFromSettings();

            if (_musicSourceA == null)
            {
                _musicSourceA = gameObject.AddComponent<AudioSource>();
                _musicSourceA.loop = true;
                _musicSourceA.playOnAwake = false;
            }
            if (_musicSourceB == null)
            {
                _musicSourceB = gameObject.AddComponent<AudioSource>();
                _musicSourceB.loop = true;
                _musicSourceB.playOnAwake = false;
            }

            _activeMusicSource = _musicSourceA;
        }

        private void OnEnable()
        {
            _settingsChangedSub = EventBus.Subscribe<SettingsChangedEvent>(_ => SyncVolumesFromSettings());
            _sceneTransitionSub = EventBus.Subscribe<SceneTransitionCompletedEvent>(OnSceneLoaded);
        }

        private void OnDisable()
        {
            _settingsChangedSub?.Dispose();
            _sceneTransitionSub?.Dispose();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<AudioManager>();
        }

        // -------------------------------------------------------------------
        // Music
        // -------------------------------------------------------------------

        /// <summary>
        /// Crossfade to a new music clip. If null, fades out current music.
        /// </summary>
        public void PlayMusic(AudioClip clip, float fadeOverride = -1f)
        {
            if (clip == null)
            {
                FadeOutMusic(fadeOverride > 0 ? fadeOverride : _crossfadeDuration);
                return;
            }

            if (_activeMusicSource.clip == clip && _activeMusicSource.isPlaying)
                return;

            var incoming = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;
            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();

            float duration = fadeOverride > 0 ? fadeOverride : _crossfadeDuration;

            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);

            _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(_activeMusicSource, incoming, duration));
            _activeMusicSource = incoming;
        }

        /// <summary>Stop music immediately.</summary>
        public void StopMusic()
        {
            if (_crossfadeCoroutine != null)
            {
                StopCoroutine(_crossfadeCoroutine);
                _crossfadeCoroutine = null;
            }
            _musicSourceA.Stop();
            _musicSourceB.Stop();
        }

        private void FadeOutMusic(float duration)
        {
            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(FadeOutRoutine(_activeMusicSource, duration));
        }

        private IEnumerator CrossfadeRoutine(AudioSource outgoing, AudioSource incoming, float duration)
        {
            float elapsed = 0f;
            float outStart = outgoing.volume;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                outgoing.volume = Mathf.Lerp(outStart, 0f, t);
                incoming.volume = Mathf.Lerp(0f, _musicVolume, t);
                yield return null;
            }
            outgoing.Stop();
            outgoing.volume = 0f;
            incoming.volume = _musicVolume;
            _crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutRoutine(AudioSource source, float duration)
        {
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            source.Stop();
            source.volume = 0f;
            _crossfadeCoroutine = null;
        }

        // -------------------------------------------------------------------
        // SFX
        // -------------------------------------------------------------------

        /// <summary>Play a one-shot SFX clip from the pool.</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            var source = GetAvailableSfxSource();
            if (source == null) return;
            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.Play();
        }

        /// <summary>Play a voice line.</summary>
        public void PlayVoice(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            var source = GetAvailableSfxSource();
            if (source == null) return;
            source.clip = clip;
            source.volume = _voiceVolume * volumeScale;
            source.Play();
        }

        /// <summary>Stop all SFX.</summary>
        public void StopAllSfx()
        {
            foreach (var source in _sfxPool)
            {
                if (source.isPlaying)
                    source.Stop();
            }
        }

        private AudioSource GetAvailableSfxSource()
        {
            foreach (var source in _sfxPool)
            {
                if (!source.isPlaying)
                    return source;
            }
            // All busy — steal the oldest
            return _sfxPool.Count > 0 ? _sfxPool[0] : null;
        }

        private void InitializeSfxPool()
        {
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var go = new GameObject($"SFX_{i}");
                go.transform.SetParent(transform);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                _sfxPool.Add(source);
            }
        }

        // -------------------------------------------------------------------
        // Volume
        // -------------------------------------------------------------------

        /// <summary>Set music volume (0-1). Updates active music source immediately.</summary>
        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (_activeMusicSource != null && _activeMusicSource.isPlaying)
                _activeMusicSource.volume = _musicVolume;
        }

        /// <summary>Set SFX volume (0-1).</summary>
        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>Set voice volume (0-1).</summary>
        public void SetVoiceVolume(float volume)
        {
            _voiceVolume = Mathf.Clamp01(volume);
        }

        private void SyncVolumesFromSettings()
        {
            if (ServiceLocator.TryGet<SettingsManager>(out var settings))
            {
                SetMusicVolume(settings.MusicVolume);
                SetSfxVolume(settings.SfxVolume);
                SetVoiceVolume(settings.VoiceVolume);
            }
        }

        // -------------------------------------------------------------------
        // Scene music mapping
        // -------------------------------------------------------------------

        private void OnSceneLoaded(SceneTransitionCompletedEvent evt)
        {
            // Load scene-appropriate music from Resources
            string musicName = evt.Scene switch
            {
                SceneName.Boot => null,
                SceneName.Lobby => "Audio/music_menu",
                SceneName.Empire => "Audio/music_empire",
                SceneName.Combat => "Audio/music_combat",
                SceneName.WorldMap => "Audio/music_empire",
                SceneName.Alliance => "Audio/music_menu",
                _ => null
            };

            if (musicName != null)
            {
                var clip = Resources.Load<AudioClip>(musicName);
                if (clip != null)
                    PlayMusic(clip);
            }
        }
    }
}
