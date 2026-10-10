using System.Threading.Tasks;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Звуки окружения: птицы днём, сверчки ночью, шум дождя и шорох травы, когда змея ползёт в зарослях.
    /// Четыре зацикленных источника, громкость каждого плавно следует за временем суток, погодой и змеёй.
    /// Петли синтезируются AmbientSynth в фоновом потоке; клипы создаются в главном, когда данные готовы.
    /// </summary>
    public class AmbienceService : MonoBehaviour
    {
        private const int DAY = 0;
        private const int NIGHT = 1;
        private const int RAIN = 2;
        private const int RUSTLE = 3;
        private const int COUNT = 4;

        [Header("Ссылки")]
        [SerializeField] private GameManager _game;
        [SerializeField] private SettingsService _settings;
        [SerializeField] private DayNightCycle _dayNight;
        [SerializeField] private WeatherSystem _weather;
        [SerializeField] private SnakeController _snake;

        [Tooltip("Источники: птицы, сверчки, дождь, шорох")]
        [SerializeField] private AudioSource[] _sources = new AudioSource[COUNT];

        [Header("Громкость")]
        [Tooltip("Наибольшая громкость: птицы, сверчки, дождь, шорох")]
        [SerializeField] private Vector4 _maxVolume = new Vector4(0.55f, 0.5f, 0.7f, 0.6f);

        [Tooltip("Скорость, с которой громкость догоняет нужную, в секунду")]
        [SerializeField, Min(0.1f)] private float _response = 1.5f;

        [Tooltip("Насколько тише всё на паузе и в меню")]
        [SerializeField, Range(0f, 1f)] private float _menuDuck = 0.5f;

        private Task<float[][]> _task;
        private readonly int[] _frames = new int[COUNT];
        private readonly float[] _volume = new float[COUNT];
        private bool _ready;

        private void Awake()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i].loop = true;
                _sources[i].playOnAwake = false;
                _sources[i].volume = 0f;
            }
            int[] frames = _frames;
            _task = Task.Run(() => new[]
            {
                AmbientSynth.Birds(out frames[DAY]),
                AmbientSynth.Crickets(out frames[NIGHT]),
                AmbientSynth.Rain(out frames[RAIN]),
                AmbientSynth.Rustle(out frames[RUSTLE])
            });
        }

        private void Update()
        {
            if (!_ready && _task != null && _task.IsCompleted) CreateClips();
            if (!_ready) return;

            float dt = Time.unscaledDeltaTime;
            GameState state = _game.State;
            bool playing = state == GameState.Playing;
            float duck = playing ? 1f : _menuDuck;
            float day = _dayNight.Daylight01;
            float rain = _weather.Rain01;

            float rustle = 0f;
            if (playing && _snake.IsAlive && _snake.IsHidden && !_snake.InBurrow)
                rustle = Mathf.Clamp01(_snake.Speed / Mathf.Max(0.1f, _snake.WalkSpeed));

            SetTarget(DAY, day * (1f - 0.75f * rain) * duck, dt);
            SetTarget(NIGHT, (1f - day) * (1f - 0.5f * rain) * duck, dt);
            SetTarget(RAIN, rain * duck, dt);
            // Шорох откликается быстро — иначе он «тянется» за остановившейся змеёй.
            SetTarget(RUSTLE, rustle, dt * 4f);
        }

        private void SetTarget(int index, float target01, float dt)
        {
            _volume[index] = Mathf.MoveTowards(_volume[index], target01, _response * dt);
            _sources[index].volume = _volume[index] * _maxVolume[index] * _settings.SfxVolume;
        }

        private void CreateClips()
        {
            Task<float[][]> task = _task;
            _task = null;
            if (task.IsFaulted || task.Result == null)
            {
                Debug.LogWarning("AmbienceService: не удалось синтезировать звуки природы: " + task.Exception);
                return;
            }
            string[] names = { "AmbienceBirds", "AmbienceCrickets", "AmbienceRain", "AmbienceRustle" };
            for (int i = 0; i < COUNT; i++)
            {
                AudioClip clip = AudioClip.Create(names[i], _frames[i], 2, AmbientSynth.SAMPLE_RATE, false);
                clip.SetData(task.Result[i], 0);
                _sources[i].clip = clip;
                _sources[i].Play();
            }
            _ready = true;
        }
    }
}
