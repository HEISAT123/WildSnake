using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Погода: время от времени набегают тучи и идёт дождь. Дождь плавно усиливается и стихает (Rain01),
    /// сгущает тучи (DayNightCycle.Overcast01), а частицы дождя летят вокруг камеры.
    /// На игру дождь влияет через GameManager: змея зябнет, часть добычи прячется, лягушек больше.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса (как часто и как долго идёт дождь)")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Смена дня и ночи: ей передаётся пасмурность")]
        [SerializeField] private DayNightCycle _dayNight;

        [Tooltip("Частицы дождя")]
        [SerializeField] private ParticleSystem _rain;

        [Tooltip("Камера: дождь идёт вокруг неё")]
        [SerializeField] private Transform _camera;

        [Header("Вид")]
        [Tooltip("За сколько секунд дождь набирает силу и стихает")]
        [SerializeField, Min(0.1f)] private float _fadeTime = 8f;

        [Tooltip("Сколько капель в секунду в полную силу")]
        [SerializeField, Min(0f)] private float _maxDrops = 2200f;

        [Tooltip("На какой высоте над камерой рождаются капли")]
        [SerializeField] private float _rainHeight = 9f;

        /// <summary>Пошёл дождь.</summary>
        public event Action RainStarted;

        /// <summary>Дождь кончился.</summary>
        public event Action RainStopped;

        /// <summary>Погода меняется (идёт партия).</summary>
        public bool Running { get; set; }

        /// <summary>Сила дождя 0..1.</summary>
        public float Rain01 { get; private set; }

        public bool IsRaining => _raining;

        private bool _raining;
        private float _timer;

        /// <summary>Ясно; первый дождь не раньше чем через delay секунд.</summary>
        public void ResetWeather(float delay)
        {
            _raining = false;
            Rain01 = 0f;
            _timer = delay;
            Apply();
        }

        /// <summary>Сразу начать дождь (проверка и скриншоты).</summary>
        public void StartRain(bool instant)
        {
            _raining = true;
            _timer = UnityEngine.Random.Range(_config.RainDuration.x, _config.RainDuration.y);
            if (instant) Rain01 = 1f;
            RainStarted?.Invoke();
            Apply();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Running)
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    if (_raining)
                    {
                        _raining = false;
                        _timer = UnityEngine.Random.Range(_config.RainInterval.x, _config.RainInterval.y);
                        RainStopped?.Invoke();
                    }
                    else
                    {
                        StartRain(false);
                    }
                }
            }

            float target = _raining ? 1f : 0f;
            if (!Mathf.Approximately(Rain01, target))
            {
                Rain01 = Mathf.MoveTowards(Rain01, target, dt / _fadeTime);
                Apply();
            }
            if (Rain01 > 0f) _rain.transform.position = _camera.position + Vector3.up * _rainHeight;
        }

        private void Apply()
        {
            _dayNight.Overcast01 = Mathf.Clamp01(Rain01 * 1.2f);
            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = _maxDrops * Rain01;
            bool on = Rain01 > 0.001f;
            if (on && !_rain.isPlaying) _rain.Play();
            else if (!on && _rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
