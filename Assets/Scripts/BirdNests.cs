using System;
using UnityEngine;

namespace WildSnake
{
    /// <summary>
    /// Птичьи гнёзда с яйцами — набег за лёгкой едой. Яйца не убегают: подползи и съешь касанием.
    /// Но на гнезде сидит птица-родитель: заметив змею, она с криком наскакивает и клюёт, пока змея рядом.
    /// Бросок в птицу отгоняет её надолго; днём она и сама иногда улетает кормиться — гнездо без охраны.
    /// Ночью птица спит на гнезде и замечает хуже. Съеденные яйца через время появляются снова.
    /// Логика в данных, Transform только показывает результат; события забирает GameManager.
    /// </summary>
    public class BirdNests : MonoBehaviour
    {
        private const int EGGS_PER_NEST = 3;

        private enum ParentState
        {
            Sitting,
            Away,
            Alarm,
            Fled
        }

        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля: где гнёзда")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Поле: яйца в гнёздах")]
        [SerializeField] private BoardView _board;

        [Tooltip("Змея игрока")]
        [SerializeField] private SnakeController _snake;

        [Header("Птицы-родители")]
        [Tooltip("Корни моделей птиц, по одной на гнездо")]
        [SerializeField] private Transform[] _parents = new Transform[0];

        [Tooltip("Тела птиц (наклон, подскок)")]
        [SerializeField] private Transform[] _bodies = new Transform[0];

        [Tooltip("Левые крылья")]
        [SerializeField] private Transform[] _leftWings = new Transform[0];

        [Tooltip("Правые крылья")]
        [SerializeField] private Transform[] _rightWings = new Transform[0];

        [Header("Поведение")]
        [Tooltip("С какого расстояния птица замечает змею у гнезда, клеток")]
        [SerializeField, Min(0.5f)] private float _alarmRadius = 3.6f;

        [Tooltip("Дальше этого от гнезда птица змею не преследует, клеток")]
        [SerializeField, Min(0.5f)] private float _leash = 3.2f;

        [Tooltip("Дистанция клевка, клеток")]
        [SerializeField, Min(0.2f)] private float _peckRange = 0.9f;

        [Tooltip("Пауза между клевками, сек")]
        [SerializeField, Min(0.2f)] private float _peckCooldown = 1.3f;

        [Tooltip("Скорость наскоков, клеток в секунду")]
        [SerializeField, Min(0.1f)] private float _hopSpeed = 2.8f;

        [Tooltip("Насколько близко к птице должен пройти бросок, чтобы её отогнать, клеток")]
        [SerializeField, Min(0.2f)] private float _scareRadius = 1.3f;

        [Tooltip("Сколько отогнанная птица не возвращается, сек")]
        [SerializeField, Min(1f)] private float _fledTime = 30f;

        [Tooltip("Сколько птица сидит на гнезде днём, прежде чем улететь кормиться, сек (мин, макс)")]
        [SerializeField] private Vector2 _sitTime = new Vector2(30f, 60f);

        [Tooltip("Сколько птица кормится вдали от гнезда, сек (мин, макс)")]
        [SerializeField] private Vector2 _awayTime = new Vector2(12f, 25f);

        [Tooltip("С какого расстояния змея достаёт яйцо, клеток")]
        [SerializeField, Min(0.2f)] private float _eatRadius = 0.8f;

        [Tooltip("Пауза между яйцами, сек")]
        [SerializeField, Min(0.1f)] private float _eatCooldown = 0.7f;

        /// <summary>Змея съела яйцо (где).</summary>
        public event Action<Vector3> EggEaten;

        /// <summary>Птица клюнула змею (откуда — позиция птицы).</summary>
        public event Action<Vector3> Pecked;

        /// <summary>Бросок отогнал птицу от гнезда.</summary>
        public event Action ParentScared;

        /// <summary>Гнёзда живут (идёт партия).</summary>
        public bool IsRunning { get; set; }

        /// <summary>Ночью птицы спят на гнёздах и замечают змею хуже.</summary>
        public bool IsNight { get; set; }

        private int _count;
        private int[] _eggs;
        private ParentState[] _state;
        private float[] _timer;
        private float[] _peckTimer;
        private float[] _refill;
        private float[] _anim;
        private float[] _wingOpen;
        private float[] _look;
        private Vector3[] _pos;
        private Vector3[] _nest;
        private float[] _yaw;
        private float[] _height;
        private float _eatTimer;
        private bool _initialized;

        private static readonly Quaternion WingFoldL = Quaternion.Euler(0f, -78f, -12f);
        private static readonly Quaternion WingFoldR = Quaternion.Euler(0f, 78f, 12f);

        private void OnEnable()
        {
            _snake.StrikeStarted += OnStrikeStarted;
        }

        private void OnDisable()
        {
            _snake.StrikeStarted -= OnStrikeStarted;
        }

        private void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Бросок в сторону птицы отгоняет её сразу, в момент начала: иначе встречный клевок сбил бы бросок раньше,
        /// чем голова долетит.
        /// </summary>
        private void OnStrikeStarted(float charge)
        {
            if (!_initialized || !IsRunning) return;
            float cs = _grid.CellSize;
            Vector3 head = _snake.HeadPosition;
            Vector3 forward = _snake.Forward;
            float reach = (_scareRadius + 1f + charge) * cs;
            for (int i = 0; i < _count; i++)
            {
                if (_state[i] != ParentState.Sitting && _state[i] != ParentState.Alarm) continue;
                Vector3 d = _pos[i] - head;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > reach) continue;
                if (dist > _scareRadius * cs && Vector3.Angle(forward, d) > 60f) continue;
                Scare(i, head);
            }
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _count = Mathf.Min(_grid.BirdNestCount, _parents.Length);
            _eggs = new int[_count];
            _state = new ParentState[_count];
            _timer = new float[_count];
            _peckTimer = new float[_count];
            _refill = new float[_count];
            _anim = new float[_count];
            _wingOpen = new float[_count];
            _look = new float[_count];
            _pos = new Vector3[_count];
            _nest = new Vector3[_count];
            _yaw = new float[_count];
            _height = new float[_count];
            for (int i = 0; i < _count; i++) _nest[i] = _grid.GetBirdNest(i);
            ResetAll();
        }

        /// <summary>Новая партия: полные гнёзда, птицы на месте.</summary>
        public void ResetAll()
        {
            Initialize();
            _eatTimer = 0f;
            for (int i = 0; i < _count; i++)
            {
                _eggs[i] = EGGS_PER_NEST;
                ShowEggs(i);
                _refill[i] = 0f;
                _peckTimer[i] = 0f;
                _anim[i] = UnityEngine.Random.value * 10f;
                Sit(i, UnityEngine.Random.Range(_sitTime.x, _sitTime.y));
                ApplyVisual(i);
            }
        }

        /// <summary>Ближайшее гнездо с яйцами в радиусе (для подсказок). guarded — птица на месте или рядом.</summary>
        public bool TryGetNearest(Vector3 from, float maxDistance, out Vector3 nest, out bool guarded)
        {
            nest = default;
            guarded = false;
            if (!_initialized) return false;
            float best = maxDistance * maxDistance;
            bool found = false;
            for (int i = 0; i < _count; i++)
            {
                if (_eggs[i] <= 0) continue;
                Vector3 d = _nest[i] - from;
                d.y = 0f;
                if (d.sqrMagnitude > best) continue;
                best = d.sqrMagnitude;
                nest = _nest[i];
                guarded = _state[i] == ParentState.Sitting || _state[i] == ParentState.Alarm;
                found = true;
            }
            return found;
        }

        /// <summary>Есть ли рядом с точкой птица, которая сейчас наскакивает на змею.</summary>
        public bool IsAttacking(Vector3 from, float maxDistance)
        {
            if (!_initialized) return false;
            for (int i = 0; i < _count; i++)
            {
                if (_state[i] != ParentState.Alarm) continue;
                Vector3 d = _pos[i] - from;
                d.y = 0f;
                if (d.sqrMagnitude <= maxDistance * maxDistance) return true;
            }
            return false;
        }

        private void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (IsRunning)
            {
                if (_eatTimer > 0f) _eatTimer -= dt;
                for (int i = 0; i < _count; i++) TickNest(i, dt);
            }
            for (int i = 0; i < _count; i++)
            {
                _anim[i] += dt;
                ApplyVisual(i);
            }
        }

        private void TickNest(int i, float dt)
        {
            float cs = _grid.CellSize;
            bool present = SnakePresent();
            Vector3 head = _snake.HeadPosition;
            float toNest = present ? Flat(head - _nest[i]) : float.MaxValue;
            if (_peckTimer[i] > 0f) _peckTimer[i] -= dt;

            switch (_state[i])
            {
                case ParentState.Sitting:
                    _timer[i] -= dt;
                    if (present && toNest <= AlarmRadius() * cs)
                    {
                        _state[i] = ParentState.Alarm;
                        break;
                    }
                    // Днём птица иногда улетает кормиться — гнездо остаётся без охраны.
                    if (!IsNight && _timer[i] <= 0f)
                    {
                        _state[i] = ParentState.Away;
                        _timer[i] = UnityEngine.Random.Range(_awayTime.x, _awayTime.y);
                    }
                    break;

                case ParentState.Away:
                    _timer[i] -= dt;
                    if (_timer[i] <= 0f) Sit(i, UnityEngine.Random.Range(_sitTime.x, _sitTime.y));
                    break;

                case ParentState.Alarm:
                    TickAlarm(i, dt, present, head, toNest);
                    break;

                case ParentState.Fled:
                    _timer[i] -= dt;
                    // Сначала улетает прочь и вверх, потом её не видно, пока не вернётся.
                    _pos[i] += Quaternion.Euler(0f, _yaw[i], 0f) * Vector3.forward * (5f * cs * dt);
                    _height[i] += 3f * cs * dt;
                    if (_timer[i] <= 0f) Sit(i, UnityEngine.Random.Range(_sitTime.x, _sitTime.y));
                    break;
            }

            // Яйца: подползти вплотную — и съесть, по одному.
            if (present && _eggs[i] > 0 && _eatTimer <= 0f && toNest <= _eatRadius * cs && _snake.IsGrounded && !_snake.IsStriking)
            {
                _eggs[i]--;
                ShowEggs(i);
                _eatTimer = _eatCooldown;
                _refill[i] = 0f;
                if (_state[i] == ParentState.Sitting) _state[i] = ParentState.Alarm;
                EggEaten?.Invoke(_nest[i]);
            }

            // Пустое гнездо со временем снова полно — если змеи нет поблизости.
            if (_eggs[i] < EGGS_PER_NEST)
            {
                _refill[i] += dt;
                if (_refill[i] >= _config.NestRefillSeconds && toNest > 10f * cs)
                {
                    _eggs[i] = EGGS_PER_NEST;
                    _refill[i] = 0f;
                    ShowEggs(i);
                }
            }
        }

        /// <summary>Наскоки: птица прыгает к змее, не отходя от гнезда дальше поводка, и клюёт; бросок её отгоняет.</summary>
        private void TickAlarm(int i, float dt, bool present, Vector3 head, float toNest)
        {
            float cs = _grid.CellSize;
            if (!present || toNest > AlarmRadius() * 1.6f * cs)
            {
                // Змея ушла — назад на гнездо.
                if (MoveTo(i, _nest[i], dt)) Sit(i, UnityEngine.Random.Range(_sitTime.x, _sitTime.y));
                return;
            }

            // Цель — змея, но не дальше поводка от гнезда.
            Vector3 target = head;
            Vector3 fromNest = target - _nest[i];
            fromNest.y = 0f;
            float leash = _leash * cs;
            if (fromNest.magnitude > leash) target = _nest[i] + fromNest.normalized * leash;
            MoveTo(i, target, dt);
            FaceTo(i, head);

            if (_peckTimer[i] <= 0f && Flat(head - _pos[i]) <= _peckRange * cs && _snake.HeightAboveGround < 0.6f * cs)
            {
                _peckTimer[i] = _peckCooldown;
                Pecked?.Invoke(_pos[i]);
            }
        }

        private void Scare(int i, Vector3 from)
        {
            _state[i] = ParentState.Fled;
            _timer[i] = _fledTime;
            Vector3 away = _pos[i] - from;
            _yaw[i] = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
            ParentScared?.Invoke();
        }

        private float AlarmRadius()
        {
            float r = _alarmRadius;
            if (IsNight) r *= 0.5f;
            if (_snake.IsHidden) r *= 0.7f;
            if (_snake.IsMoving && _snake.Pace == SnakePace.Sneak) r *= 0.75f;
            return r;
        }

        private bool SnakePresent()
        {
            return _snake != null && _snake.IsAlive && _snake.IsRunning && !_snake.InBurrow;
        }

        /// <summary>Шаг к точке подскоками; true — дошла.</summary>
        private bool MoveTo(int i, Vector3 target, float dt)
        {
            Vector3 d = target - _pos[i];
            d.y = 0f;
            float dist = d.magnitude;
            float step = _hopSpeed * _grid.CellSize * dt;
            if (dist <= step)
            {
                _pos[i] = new Vector3(target.x, _pos[i].y, target.z);
                return true;
            }
            _pos[i] += d / dist * step;
            _yaw[i] = Mathf.MoveTowardsAngle(_yaw[i], Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 540f * dt);
            return false;
        }

        private void FaceTo(int i, Vector3 point)
        {
            Vector3 d = point - _pos[i];
            if (d.x * d.x + d.z * d.z < 1e-4f) return;
            _yaw[i] = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private void Sit(int i, float duration)
        {
            _state[i] = ParentState.Sitting;
            _timer[i] = duration;
            _pos[i] = _nest[i];
            _height[i] = 0f;
        }

        private void ShowEggs(int i)
        {
            Renderer[] eggs = _board.BirdEggs;
            for (int k = 0; k < EGGS_PER_NEST; k++)
            {
                int index = i * EGGS_PER_NEST + k;
                if (index >= eggs.Length) break;
                bool visible = k < _eggs[i];
                if (eggs[index].gameObject.activeSelf != visible) eggs[index].gameObject.SetActive(visible);
            }
        }

        private void ApplyVisual(int i)
        {
            Transform root = _parents[i];
            bool visible = _state[i] != ParentState.Away && !(_state[i] == ParentState.Fled && _height[i] > 8f * _grid.CellSize);
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            float cs = _grid.CellSize;
            Vector3 p = _pos[i];
            p.y = _grid.GroundHeight(p) + _height[i] + (_state[i] == ParentState.Sitting ? 0.12f * cs : 0f);
            float t = _anim[i];
            float hop = _state[i] == ParentState.Alarm ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.18f * cs : 0f;
            root.SetPositionAndRotation(p + Vector3.up * hop, Quaternion.Euler(0f, _yaw[i], 0f));

            float dt = Time.deltaTime;
            float pitch = 0f;
            float flap = 0f;
            float look = 0f;
            float breathe = 0f;
            bool open = false;
            switch (_state[i])
            {
                case ParentState.Sitting:
                    // Сидит, нахохлившись: дышит и дёргано оглядывается (птица крутит головой рывками).
                    look = Mathf.Round((Mathf.PerlinNoise(t * 0.9f, i * 3.1f) - 0.5f) * 5f) * 22f;
                    breathe = Mathf.Sin(t * 3f) * 0.03f;
                    break;
                case ParentState.Alarm:
                {
                    // Расправляет крылья, хлопает ими и тычет клювом с выпадом.
                    float peck = Mathf.Clamp01((_peckTimer[i] - (_peckCooldown - 0.3f)) / 0.3f);
                    pitch = -12f + Mathf.Sin(peck * Mathf.PI) * 50f;
                    float ph = t * 22f;
                    flap = 35f + Mathf.Sin(ph + 0.5f * Mathf.Cos(ph)) * 30f;
                    open = true;
                    break;
                }
                case ParentState.Fled:
                {
                    float ph = t * 26f;
                    pitch = -20f + Mathf.Sin(ph) * 4f;
                    flap = 12f + Mathf.Sin(ph + 0.5f * Mathf.Cos(ph)) * 55f;
                    open = true;
                    break;
                }
            }
            float ease = 1f - Mathf.Exp(-12f * dt);
            _look[i] = Mathf.Lerp(_look[i], look, ease);
            if (_state[i] == ParentState.Sitting) root.rotation = Quaternion.Euler(0f, _yaw[i] + _look[i], 0f);
            if (i < _bodies.Length && _bodies[i] != null)
            {
                _bodies[i].localRotation = Quaternion.Euler(pitch, 0f, 0f);
                _bodies[i].localScale = new Vector3(1f + breathe * 0.6f, 1f + breathe, 1f);
            }
            if (i < _leftWings.Length && _leftWings[i] != null && i < _rightWings.Length && _rightWings[i] != null)
            {
                // Крылья раскрываются и складываются плавно, а не щелчком.
                _wingOpen[i] = Mathf.MoveTowards(_wingOpen[i], open ? 1f : 0f, dt * (open ? 8f : 3f));
                _leftWings[i].localRotation = Quaternion.Slerp(WingFoldL, Quaternion.Euler(0f, 0f, flap), _wingOpen[i]);
                _rightWings[i].localRotation = Quaternion.Slerp(WingFoldR, Quaternion.Euler(0f, 0f, -flap), _wingOpen[i]);
            }
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
