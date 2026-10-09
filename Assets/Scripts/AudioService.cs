using System.Threading.Tasks;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Звуки и музыка. Всё синтезируется кодом в фоновом потоке (SfxSynth, MusicSynth), чтобы не
    /// подвешивать загрузку; клипы создаются в главном потоке, когда данные готовы.
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

        private readonly AudioClip[] _clips = new AudioClip[SfxSynth.SOUND_COUNT];
        private Task<float[][]> _sfxTask;
        private Task<float[]> _musicTask;
        private int _musicFrames;
        private bool _paused;
        private float _targetPitch = 1f;
        private float _fade;

        private void Awake()
        {
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _sfxSource.playOnAwake = false;

            // Синтез занимает заметное время — уводим его с главного потока. Звуки готовы быстрее музыки.
            _sfxTask = Task.Run(() => SfxSynth.RenderAll());
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
            if (_sfxTask != null && _sfxTask.IsCompleted) CreateSoundClips();
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
            // Пока звуки синтезируются (доли секунды после запуска), просто молчим.
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
        private void CreateSoundClips()
        {
            Task<float[][]> task = _sfxTask;
            _sfxTask = null;
            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogWarning("AudioService: не удалось синтезировать звуки: " + task.Exception);
                return;
            }

            float[][] data = task.Result;
            for (int i = 0; i < data.Length && i < _clips.Length; i++)
            {
                AudioClip clip = AudioClip.Create(((SoundId)i).ToString(), data[i].Length / 2, 2, SynthDsp.SAMPLE_RATE, false);
                clip.SetData(data[i], 0);
                _clips[i] = clip;
            }
        }

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
