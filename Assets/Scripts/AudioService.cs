using UnityEngine;

namespace Snake3D
{
    /// <summary>Звуки и музыка. Клипы синтезируются при старте (ProceduralAudio), громкость — из настроек.</summary>
    public class AudioService : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Настройки (громкость)")]
        [SerializeField] private SettingsService _settings;

        [Tooltip("Источник звуковых эффектов")]
        [SerializeField] private AudioSource _sfxSource;

        [Tooltip("Источник музыки")]
        [SerializeField] private AudioSource _musicSource;

        [Header("Музыка")]
        [Tooltip("Насколько тише музыка на паузе")]
        [SerializeField, Range(0f, 1f)] private float _pauseDuck = 0.35f;

        [Tooltip("Скорость плавного изменения темпа музыки")]
        [SerializeField, Min(0.1f)] private float _pitchLerpSpeed = 2f;

        private const int SOUND_COUNT = 12;

        private readonly AudioClip[] _clips = new AudioClip[SOUND_COUNT];
        private bool _paused;
        private float _targetPitch = 1f;

        private void Awake()
        {
            for (int i = 0; i < SOUND_COUNT; i++) _clips[i] = ProceduralAudio.CreateSound((SoundId)i);

            _musicSource.clip = ProceduralAudio.CreateMusicLoop();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _sfxSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            _settings.Changed += ApplyVolumes;
        }

        private void OnDisable()
        {
            _settings.Changed -= ApplyVolumes;
        }

        private void Start()
        {
            ApplyVolumes();
            _musicSource.Play();
        }

        private void Update()
        {
            _musicSource.pitch = Mathf.MoveTowards(_musicSource.pitch, _targetPitch, _pitchLerpSpeed * Time.unscaledDeltaTime);
        }

        public void Play(SoundId id)
        {
            AudioClip clip = _clips[(int)id];
            if (clip != null) _sfxSource.PlayOneShot(clip);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            ApplyVolumes();
        }

        /// <summary>Темп музыки: растёт с уровнем, падает при замедлении времени и после смерти.</summary>
        public void SetMusicPitch(float pitch)
        {
            _targetPitch = pitch;
        }

        private void ApplyVolumes()
        {
            _sfxSource.volume = _settings.SfxVolume;
            _musicSource.volume = _settings.MusicVolume * (_paused ? _pauseDuck : 1f);
        }
    }
}
