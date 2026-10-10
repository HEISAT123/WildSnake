using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Смена дня и ночи: солнце идёт по небу и садится, ночью светит луна и проступают звёзды,
    /// небо, туман и рассеянный свет плавно меняют цвет, пыльца над поляной превращается в светлячков.
    /// Время суток Phase: 0 — полночь, 0.25 — рассвет, 0.5 — полдень, 0.75 — закат.
    /// День и ночь идут с разной скоростью (длительности из GameConfig). Счёт дней — по рассветам.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        private const float SUNRISE = 0.25f;
        private const float SUNSET = 0.75f;

        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Ссылки")]
        [Tooltip("Длительности дня и ночи")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Солнце (оно же RenderSettings.sun для неба)")]
        [SerializeField] private Light _sun;

        [Tooltip("Луна — второй направленный свет, только ночью")]
        [SerializeField] private Light _moon;

        [Tooltip("Звёздное небо (необязательно)")]
        [SerializeField] private Renderer _stars;

        [Tooltip("Пыльца днём — светлячки ночью (необязательно)")]
        [SerializeField] private ParticleSystem _fireflies;

        [Tooltip("Слой облаков: их подсветка гаснет ночью (необязательно)")]
        [SerializeField] private Transform _clouds;

        [Header("Солнце и луна")]
        [Tooltip("Наибольшая высота солнца, град")]
        [SerializeField, Range(10f, 90f)] private float _sunMaxElevation = 62f;

        [Tooltip("Азимут солнца на рассвете и на закате, град")]
        [SerializeField] private Vector2 _sunAzimuth = new Vector2(-120f, 50f);

        [Tooltip("Яркость солнца днём")]
        [SerializeField, Min(0f)] private float _sunIntensity = 1.25f;

        [SerializeField] private Color _sunNoonColor = new Color(1f, 0.93f, 0.8f);
        [SerializeField] private Color _sunLowColor = new Color(1f, 0.52f, 0.28f);

        [Tooltip("Яркость луны")]
        [SerializeField, Min(0f)] private float _moonIntensity = 0.42f;

        [SerializeField] private Color _moonColor = new Color(0.55f, 0.66f, 1f);

        [Header("Рассеянный свет (небо / горизонт / земля)")]
        [SerializeField] private Color _daySky = new Color(0.55f, 0.62f, 0.76f);
        [SerializeField] private Color _dayEquator = new Color(0.45f, 0.48f, 0.44f);
        [SerializeField] private Color _dayGround = new Color(0.3f, 0.28f, 0.22f);
        [SerializeField] private Color _nightSky = new Color(0.12f, 0.15f, 0.26f);
        [SerializeField] private Color _nightEquator = new Color(0.07f, 0.08f, 0.13f);
        [SerializeField] private Color _nightGround = new Color(0.03f, 0.03f, 0.05f);
        [SerializeField] private Color _duskTint = new Color(0.95f, 0.55f, 0.4f);

        [Header("Небо и туман")]
        [SerializeField] private Color _dayFog = new Color(0.72f, 0.8f, 0.88f);
        [SerializeField] private Color _nightFog = new Color(0.04f, 0.055f, 0.1f);
        [SerializeField] private Color _daySkyTint = new Color(0.52f, 0.62f, 0.8f);
        [SerializeField] private Color _nightSkyTint = new Color(0.18f, 0.22f, 0.42f);

        [Tooltip("Яркость неба днём и ночью")]
        [SerializeField] private Vector2 _skyExposure = new Vector2(1.25f, 0.18f);

        [Tooltip("Дальность тумана днём и ночью")]
        [SerializeField] private Vector2 _fogEnd = new Vector2(260f, 120f);

        [Header("Светлячки")]
        [SerializeField] private Color _pollenColorA = new Color(1f, 0.95f, 0.7f, 0.8f);
        [SerializeField] private Color _pollenColorB = new Color(0.85f, 1f, 0.6f, 0.6f);
        [SerializeField] private Color _fireflyColorA = new Color(0.85f, 1f, 0.35f, 1f);
        [SerializeField] private Color _fireflyColorB = new Color(1f, 0.85f, 0.3f, 0.9f);

        public event Action<int> DayStarted;
        public event Action NightStarted;

        /// <summary>Время идёт (в меню время суток застыло на золотом часе).</summary>
        public bool Running { get; set; }

        public float Phase { get; private set; }
        public int Day { get; private set; } = 1;
        public bool IsNight => Phase < SUNRISE || Phase >= SUNSET;

        /// <summary>Насколько светло: 1 — день, 0 — ночь, между — сумерки.</summary>
        public float Daylight01 { get; private set; } = 1f;

        /// <summary>Доля прошедшего светлого времени (днём) или тёмного (ночью), 0..1.</summary>
        public float PeriodProgress01
        {
            get
            {
                if (!IsNight) return (Phase - SUNRISE) / (SUNSET - SUNRISE);
                float t = Phase >= SUNSET ? Phase - SUNSET : Phase + 1f - SUNSET;
                return t / (1f - (SUNSET - SUNRISE));
            }
        }

        /// <summary>Часы и минуты суток (для HUD).</summary>
        public int Hours => Mathf.FloorToInt(Phase * 24f) % 24;
        public int Minutes => Mathf.FloorToInt(Phase * 1440f) % 60;

        private Material _sky;
        private Material _starsMaterial;
        private Material _cloudMaterial;
        private Color _cloudEmission;
        private bool _firefliesOn;

        private void Awake()
        {
            // Копия неба: правка общего ассета в редакторе сохранилась бы после выхода из Play Mode.
            if (RenderSettings.skybox != null)
            {
                _sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = _sky;
            }
            if (_stars != null) _starsMaterial = _stars.material;
            if (_clouds != null) ShareCloudMaterial();
            RenderSettings.sun = _sun;
            Phase = _config.StartPhase;
            Apply();
        }

        private void OnDestroy()
        {
            if (_sky != null) Destroy(_sky);
            if (_starsMaterial != null) Destroy(_starsMaterial);
            if (_cloudMaterial != null) Destroy(_cloudMaterial);
        }

        /// <summary>Новая партия: первый день, заданное время суток.</summary>
        public void ResetCycle(float phase)
        {
            Day = 1;
            SetPhase(phase);
        }

        /// <summary>Продолжение сохранённой партии: номер суток и время без событий.</summary>
        public void Restore(int day, float phase)
        {
            Day = Mathf.Max(1, day);
            SetPhase(phase);
        }

        /// <summary>Поставить время суток без событий (меню, начало партии).</summary>
        public void SetPhase(float phase)
        {
            Phase = Mathf.Repeat(phase, 1f);
            Apply();
        }

        private void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool wasNight = IsNight;
            float before = Phase;
            float half = SUNSET - SUNRISE;
            float speed = IsNight ? (1f - half) / _config.NightSeconds : half / _config.DaySeconds;
            Phase = Mathf.Repeat(Phase + speed * dt, 1f);

            if (wasNight && !IsNight && before < SUNRISE + 0.5f)
            {
                Day++;
                DayStarted?.Invoke(Day);
            }
            else if (!wasNight && IsNight)
            {
                NightStarted?.Invoke();
            }
            Apply();
        }

        private void Apply()
        {
            // Солнце: днём по дуге с востока на запад, ночью под горизонтом.
            float elevation;
            float azimuth;
            if (!IsNight)
            {
                float t = (Phase - SUNRISE) / (SUNSET - SUNRISE);
                elevation = Mathf.Sin(t * Mathf.PI) * _sunMaxElevation;
                azimuth = Mathf.Lerp(_sunAzimuth.x, _sunAzimuth.y, t);
            }
            else
            {
                float t = PeriodProgress01;
                elevation = -Mathf.Sin(t * Mathf.PI) * 30f;
                azimuth = Mathf.Lerp(_sunAzimuth.y, _sunAzimuth.x + 360f, t);
            }
            // Сумерки длятся, пока солнце в пределах −6°..+12° от горизонта.
            float daylight = Smooth(Mathf.InverseLerp(-6f, 12f, elevation));
            float dusk = 1f - Mathf.Abs(daylight * 2f - 1f);
            Daylight01 = daylight;

            _sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
            float low = 1f - Mathf.Clamp01(elevation / 30f);
            _sun.color = Color.Lerp(_sunNoonColor, _sunLowColor, low * low);
            // Свет солнца не выключаем совсем: по нему процедурное небо рисует закат и тёмную ночь.
            _sun.intensity = _sunIntensity * Smooth(Mathf.InverseLerp(-2f, 10f, elevation));
            SetShadows(_sun, _sun.intensity > 0.05f);

            if (_moon != null)
            {
                float moonElevation = IsNight ? 20f + Mathf.Sin(PeriodProgress01 * Mathf.PI) * 35f : 30f;
                _moon.transform.rotation = Quaternion.Euler(moonElevation, azimuth + 180f, 0f);
                _moon.color = _moonColor;
                _moon.intensity = _moonIntensity * (1f - daylight);
                bool moonOn = _moon.intensity > 0.02f;
                if (_moon.enabled != moonOn) _moon.enabled = moonOn;
                SetShadows(_moon, moonOn && !(_sun.intensity > 0.05f));
            }

            RenderSettings.ambientSkyColor = Mix(_nightSky, _daySky, daylight, dusk * 0.35f);
            RenderSettings.ambientEquatorColor = Mix(_nightEquator, _dayEquator, daylight, dusk * 0.5f);
            RenderSettings.ambientGroundColor = Color.Lerp(_nightGround, _dayGround, daylight);
            RenderSettings.fogColor = Mix(_nightFog, _dayFog, daylight, dusk * 0.55f);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd.y, _fogEnd.x, daylight);

            if (_sky != null)
            {
                _sky.SetColor(SkyTintId, Color.Lerp(_nightSkyTint, _daySkyTint, daylight));
                _sky.SetFloat(ExposureId, Mathf.Lerp(_skyExposure.y, _skyExposure.x, daylight));
            }

            // Ночью облака не должны светиться в тёмном небе — гасим их подсветку вместе с днём.
            if (_cloudMaterial != null) _cloudMaterial.SetColor(EmissionId, _cloudEmission * Mathf.Lerp(0.06f, 1f, daylight));

            if (_starsMaterial != null)
            {
                float a = (1f - daylight) * (1f - daylight);
                _starsMaterial.SetColor(ColorId, new Color(1f, 1f, 1f, a));
                bool show = a > 0.01f;
                if (_stars.enabled != show) _stars.enabled = show;
            }

            if (_fireflies != null)
            {
                bool on = daylight < 0.35f;
                if (on != _firefliesOn)
                {
                    _firefliesOn = on;
                    ParticleSystem.MainModule main = _fireflies.main;
                    main.startColor = on
                        ? new ParticleSystem.MinMaxGradient(_fireflyColorA, _fireflyColorB)
                        : new ParticleSystem.MinMaxGradient(_pollenColorA, _pollenColorB);
                }
            }
        }

        /// <summary>Одна копия материала облаков на все клубы: меняем подсветку разом и не трогаем ассет.</summary>
        private void ShareCloudMaterial()
        {
            Renderer[] puffs = _clouds.GetComponentsInChildren<Renderer>();
            if (puffs.Length == 0) return;
            _cloudMaterial = new Material(puffs[0].sharedMaterial);
            _cloudEmission = _cloudMaterial.GetColor(EmissionId);
            for (int i = 0; i < puffs.Length; i++) puffs[i].sharedMaterial = _cloudMaterial;
        }

        private static void SetShadows(Light light, bool on)
        {
            LightShadows wanted = on ? LightShadows.Soft : LightShadows.None;
            if (light.shadows != wanted) light.shadows = wanted;
        }

        /// <summary>Ночь→день по daylight и тёплый оттенок сумерек поверх.</summary>
        private Color Mix(Color night, Color day, float daylight, float duskAmount)
        {
            Color c = Color.Lerp(night, day, daylight);
            return Color.Lerp(c, c * _duskTint * 1.35f, duskAmount);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
