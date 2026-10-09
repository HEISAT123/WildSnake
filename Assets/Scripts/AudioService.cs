using System.Threading.Tasks;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Звуки и музыка. Эффекты синтезируются при старте (ProceduralAudio), музыка — в фоновом
    /// потоке (MusicSynth), чтобы не подвешивать загрузку; когда готова, плавно включается.
    /// </summary>
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

        [Tooltip("Длительность плавного появления музыки, сек")]
        [SerializeField, Min(0.1f)] private float _fadeInTime = 2f;

        private const int SOUND_COUNT = 12;

        private readonly AudioClip[] _clips = new AudioClip[SOUND_COUNT];
        private Task<float[]> _musicTask;
        private int _musicFrames;
        private bool _paused;
        private float _targetPitch = 1f;
        private float _fade;

        private void Awake()
        {
            for (int i = 0; i < SOUND_COUNT; i++) _clips[i] = ProceduralAudio.CreateSound((SoundId)i);

            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _sfxSource.playOnAwake = false;

            // Синтез ~40 секунд стерео занимает заметное время — уводим его с главного потока.
            _musicTask = Task.Run(() => MusicSynth.RenderLoop(out _musicFrames));
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
        }

        private void Update()
        {
            if (_musicTask != null && _musicTask.IsCompleted) StartMusic();

            float dt = Time.unscaledDeltaTime;
            _musicSource.pitch = Mathf.MoveTowards(_musicSource.pitch, _targetPitch, _pitchLerpSpeed * dt);
            if (_fade < 1f && _musicSource.isPlaying)
            {
                _fade = Mathf.Min(1f, _fade + dt / _fadeInTime);
                ApplyVolumes();
            }
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

        /// <summary>AudioClip можно создать только в главном потоке, поэтому здесь, а не в задаче.</summary>
        private void StartMusic()
        {
            Task<float[]> task = _musicTask;
            _musicTask = null;
            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogWarning("AudioService: не удалось синтезировать музыку: " + task.Exception);
                return;
            }

            AudioClip clip = AudioClip.Create("MusicLoop", _musicFrames, 2, MusicSynth.SAMPLE_RATE, false);
            clip.SetData(task.Result, 0);
            _musicSource.clip = clip;
            _fade = 0f;
            ApplyVolumes();
            _musicSource.Play();
        }

        private void ApplyVolumes()
        {
            _sfxSource.volume = _settings.SfxVolume;
            _musicSource.volume = _settings.MusicVolume * (_paused ? _pauseDuck : 1f) * _fade;
        }
    }
}
