using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Змейка с непрерывным движением: плавное руление, прыжок, рост.
    /// Голова пишет свою траекторию в кольцевой буфер точек с равным шагом,
    /// сегменты тела ставятся на этой траектории на фиксированном расстоянии друг от друга,
    /// поэтому тело повторяет и повороты, и прыжки головы.
    /// </summary>
    public class SnakeController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Сетка поля (границы и размер клетки)")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Спавнер еды (где сейчас еда)")]
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Куб головы")]
        [SerializeField] private Transform _head;

        [Tooltip("Неактивный шаблон сегмента тела для пула")]
        [SerializeField] private Transform _segmentTemplate;

        [Tooltip("Родитель для сегментов тела")]
        [SerializeField] private Transform _segmentRoot;

        [Header("Скорость")]
        [Tooltip("Начальная скорость, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _startSpeed = 4.5f;

        [Tooltip("Максимальная скорость, клеток в секунду")]
        [SerializeField, Min(0.5f)] private float _maxSpeed = 9f;

        [Tooltip("Прибавка скорости за каждую еду, клеток в секунду")]
        [SerializeField, Min(0f)] private float _speedPerFood = 0.12f;

        [Header("Поворот")]
        [Tooltip("Скорость поворота при руле до упора, град/с")]
        [SerializeField, Min(10f)] private float _turnSpeed = 190f;

        [Tooltip("Как быстро руль доходит до упора и возвращается, 1/с (больше — резче)")]
        [SerializeField, Min(0.5f)] private float _steerResponse = 6f;

        [Header("Прыжок")]
        [Tooltip("Высота прыжка, клеток")]
        [SerializeField, Min(0.1f)] private float _jumpHeight = 1.4f;

        [Tooltip("Гравитация, клеток в секунду²")]
        [SerializeField, Min(1f)] private float _gravity = 30f;

        [Tooltip("На сколько голова должна быть выше/ниже сегмента, чтобы не столкнуться с ним (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _jumpClearance = 0.6f;

        [Header("Тело")]
        [Tooltip("Начальная длина, включая голову")]
        [SerializeField, Min(2)] private int _startLength = 4;

        [Tooltip("Максимальная длина (размер пула сегментов)")]
        [SerializeField, Min(2)] private int _maxLength = 250;

        [Tooltip("Расстояние между сегментами вдоль траектории (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _segmentSpacing = 0.8f;

        [Tooltip("Шаг записи траектории головы (доля клетки). Меньше — точнее, но больше памяти")]
        [SerializeField, Range(0.02f, 0.5f)] private float _pathSampleDistance = 0.1f;

        [Header("Столкновения")]
        [Tooltip("Радиус столкновения головы с телом (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _bodyCollisionRadius = 0.55f;

        [Tooltip("Сколько ближайших к голове сегментов не проверять на столкновение")]
        [SerializeField, Min(1)] private int _selfCollisionSkip = 3;

        [Tooltip("Радиус поедания еды (доля клетки)")]
        [SerializeField, Min(0.1f)] private float _eatRadius = 0.75f;

        [Tooltip("Отступ головы от стены, при котором засчитывается удар (доля клетки)")]
        [SerializeField, Min(0f)] private float _wallMargin = 0.45f;

        [Header("Визуал")]
        [Tooltip("Высота центра кубов над полем (доля клетки)")]
        [SerializeField] private float _bodyHeight = 0.5f;

        [Tooltip("Крен головы в повороте, град")]
        [SerializeField, Range(0f, 45f)] private float _bankAngle = 18f;

        [Tooltip("Наклон головы вверх/вниз в прыжке на единицу вертикальной скорости, град")]
        [SerializeField, Min(0f)] private float _pitchPerSpeed = 2.5f;

        [Tooltip("Длительность сквош-стретча (еда, приземление), сек")]
        [SerializeField, Min(0.01f)] private float _squashDuration = 0.25f;

        [Tooltip("Сила сквош-стретча")]
        [SerializeField, Range(0f, 0.6f)] private float _squashAmount = 0.3f;

        /// <summary>Позиция головы в мире в момент поедания.</summary>
        public event Action<Vector3> FoodEaten;

        /// <summary>Позиция головы в мире в момент смерти.</summary>
        public event Action<Vector3> Died;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                // Запоминаем кадр старта: Space, которым начали игру, не должен сразу стать прыжком.
                if (value && !_isRunning) _runStartFrame = Time.frameCount;
                _isRunning = value;
            }
        }

        public bool IsAlive { get; private set; }
        public int Length => _length;
        public Transform Head => _head;

        private bool _isRunning;
        private int _runStartFrame;

        // Траектория головы: кольцевой буфер, _pathHead — индекс самой свежей точки.
        private Vector3[] _path;
        private int _pathHead;
        private int _pathCount;

        // Позиция и направление каждого сегмента (индекс 0 — голова, не используется).
        private Vector3[] _segPositions;
        private Vector3[] _segForward;

        private int _length;
        private Vector3 _headPos;
        private float _yaw;
        private float _steer;
        private float _speed;

        private float _height;
        private float _verticalSpeed;
        private bool _grounded;

        private float _squashTimer;
        private Vector3 _headBaseScale;

        // Кэш геометрии, пересчитывается в ResetSnake.
        private float _cellSize;
        private float _groundY;
        private float _sampleDist;

        private readonly List<Transform> _segments = new List<Transform>();

        private void Awake()
        {
            _headBaseScale = _head.localScale;
            _segmentTemplate.gameObject.SetActive(false);

            // Всё выделяем заранее под максимальную длину: в игре аллокаций и Instantiate нет.
            int capacity = Mathf.CeilToInt((_maxLength + 1) * _segmentSpacing / _pathSampleDistance) + 8;
            _path = new Vector3[capacity];
            _segPositions = new Vector3[_maxLength];
            _segForward = new Vector3[_maxLength];
            for (int i = 1; i < _maxLength; i++) CreateSegment();
        }

        /// <summary>Вернуть змейку в начальное состояние в центре поля.</summary>
        public void ResetSnake()
        {
            _cellSize = _grid.CellSize;
            _groundY = _grid.Center.y + _bodyHeight * _cellSize;
            _sampleDist = _pathSampleDistance * _cellSize;

            _length = Mathf.Clamp(_startLength, 2, _maxLength);
            _yaw = 90f; // смотрим вдоль +X
            _steer = 0f;
            _speed = _startSpeed;
            _height = 0f;
            _verticalSpeed = 0f;
            _grounded = true;
            _squashTimer = _squashDuration;
            IsAlive = true;
            IsRunning = false;

            _headPos = new Vector3(_grid.Center.x, _groundY, _grid.Center.z);

            // Заполняем траекторию прямой линией позади головы.
            Vector3 forward = Forward();
            int needed = Mathf.Min(_path.Length, Mathf.CeilToInt(_length * _segmentSpacing / _pathSampleDistance) + 2);
            _pathHead = _path.Length - 1;
            _pathCount = 0;
            for (int j = needed - 1; j >= 0; j--) PushPathPoint(_headPos - forward * (j * _sampleDist));

            _head.localScale = _headBaseScale;
            SetActiveSegments(_length - 1);
            ComputeBody();
            ApplyVisuals(0f);
        }

        /// <summary>Отметить в сетке клетки под змейкой и прямо перед ней, чтобы еда там не появлялась.</summary>
        public void MarkOccupiedCells()
        {
            _grid.ClearAll();
            MarkCell(_headPos);
            Vector3 forward = Forward();
            for (int j = 1; j <= 3; j++) MarkCell(_headPos + forward * (j * _cellSize));
            for (int i = 1; i < _length; i++) MarkCell(_segPositions[i]);
        }

        private void Update()
        {
            if (!IsRunning || !IsAlive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Steer(dt);
            UpdateJump(dt);

            _headPos += Forward() * (_speed * _cellSize * dt);
            _headPos.y = _groundY + _height;
            RecordPath();
            ComputeBody();

            if (HitsWall() || HitsSelf())
            {
                Die();
                return;
            }

            TryEat();
            ApplyVisuals(dt);
        }

        private void Steer(float dt)
        {
            float target = 0f;
            if (GameInput.LeftHeld) target -= 1f;
            if (GameInput.RightHeld) target += 1f;

            // Руль не прыгает сразу в упор, а доворачивается — так поворот получается дугой.
            _steer = Mathf.MoveTowards(_steer, target, _steerResponse * dt);
            _yaw += _steer * _turnSpeed * dt;
        }

        private void UpdateJump(float dt)
        {
            if (_grounded && GameInput.Jump && Time.frameCount != _runStartFrame)
            {
                _verticalSpeed = Mathf.Sqrt(2f * _gravity * _jumpHeight) * _cellSize;
                _grounded = false;
            }

            if (_grounded) return;

            _verticalSpeed -= _gravity * _cellSize * dt;
            _height += _verticalSpeed * dt;
            if (_height <= 0f)
            {
                _height = 0f;
                _verticalSpeed = 0f;
                _grounded = true;
                _squashTimer = 0f; // сквош при приземлении
            }
        }

        /// <summary>Добавляем точки строго через _sampleDist, чтобы сегмент находился по индексу за O(1).</summary>
        private void RecordPath()
        {
            Vector3 newest = _path[_pathHead];
            Vector3 delta = _headPos - newest;
            float dist = delta.magnitude;
            while (dist >= _sampleDist)
            {
                newest += delta * (_sampleDist / dist);
                PushPathPoint(newest);
                delta = _headPos - newest;
                dist = delta.magnitude;
            }
        }

        private void PushPathPoint(Vector3 point)
        {
            _pathHead = (_pathHead + 1) % _path.Length;
            _path[_pathHead] = point;
            if (_pathCount < _path.Length) _pathCount++;
        }

        /// <summary>k = 0 — самая свежая точка траектории.</summary>
        private Vector3 PathPoint(int k)
        {
            return _path[(_pathHead - k + _path.Length) % _path.Length];
        }

        private void ComputeBody()
        {
            float spacing = _segmentSpacing * _cellSize;
            for (int i = 1; i < _length; i++)
                SamplePath(i * spacing, out _segPositions[i], out _segForward[i]);
        }

        /// <summary>Точка траектории на расстоянии distance позади головы и направление «к голове» в ней.</summary>
        private void SamplePath(float distance, out Vector3 position, out Vector3 forward)
        {
            Vector3 newest = _path[_pathHead];
            Vector3 toHead = _headPos - newest;
            float r = toHead.magnitude;
            if (distance <= r)
            {
                position = r > 1e-5f ? Vector3.Lerp(_headPos, newest, distance / r) : _headPos;
                forward = toHead;
                return;
            }

            float k = (distance - r) / _sampleDist;
            int i = (int)k;
            int last = _pathCount - 1;
            if (i >= last)
            {
                position = PathPoint(last);
                forward = last > 0 ? PathPoint(last - 1) - position : Forward();
                return;
            }

            Vector3 a = PathPoint(i);
            Vector3 b = PathPoint(i + 1);
            position = Vector3.Lerp(a, b, k - i);
            forward = a - b;
        }

        private bool HitsWall()
        {
            Vector3 local = _headPos - _grid.Center;
            float halfX = _grid.Width * _cellSize * 0.5f - _wallMargin * _cellSize;
            float halfZ = _grid.Height * _cellSize * 0.5f - _wallMargin * _cellSize;
            return Mathf.Abs(local.x) > halfX || Mathf.Abs(local.z) > halfZ;
        }

        /// <summary>Сегмент мешает, только если он близко по горизонтали и примерно на той же высоте — так прыжок спасает.</summary>
        private bool HitsSelf()
        {
            float radius = _bodyCollisionRadius * _cellSize;
            float radiusSqr = radius * radius;
            float clearance = _jumpClearance * _cellSize;
            for (int i = _selfCollisionSkip; i < _length; i++)
            {
                Vector3 d = _segPositions[i] - _headPos;
                if (Mathf.Abs(d.y) >= clearance) continue;
                d.y = 0f;
                if (d.sqrMagnitude < radiusSqr) return true;
            }
            return false;
        }

        private void TryEat()
        {
            if (_food == null || !_food.HasFood) return;
            float r = _eatRadius * _cellSize;
            if ((_food.Position - _headPos).sqrMagnitude > r * r) return;

            if (_length < _maxLength)
            {
                _length++;
                int i = _length - 1;
                SamplePath(i * _segmentSpacing * _cellSize, out _segPositions[i], out _segForward[i]);
                SetActiveSegments(_length - 1);
            }

            _speed = Mathf.Min(_maxSpeed, _speed + _speedPerFood);
            _squashTimer = 0f;
            FoodEaten?.Invoke(_headPos);
        }

        private void Die()
        {
            IsAlive = false;
            ApplyVisuals(0f);
            Died?.Invoke(_headPos);
        }

        private void ApplyVisuals(float dt)
        {
            _head.position = _headPos;
            float pitch = Mathf.Clamp(-_verticalSpeed / _cellSize * _pitchPerSpeed, -35f, 35f);
            float roll = -_steer * _bankAngle;
            _head.rotation = Quaternion.Euler(pitch, _yaw, roll);

            for (int i = 1; i < _length; i++)
            {
                Transform seg = _segments[i - 1];
                seg.position = _segPositions[i];
                Vector3 f = _segForward[i];
                if (f.sqrMagnitude > 1e-8f) seg.rotation = Quaternion.LookRotation(f);
            }

            if (_squashTimer < _squashDuration)
            {
                _squashTimer += dt;
                float k = Mathf.Clamp01(_squashTimer / _squashDuration);
                float s = Mathf.Sin(k * Mathf.PI) * _squashAmount;
                _head.localScale = Vector3.Scale(_headBaseScale, new Vector3(1f + s, 1f - s, 1f + s));
            }
            else
            {
                _head.localScale = _headBaseScale;
            }
        }

        private void SetActiveSegments(int count)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                bool active = i < count;
                if (_segments[i].gameObject.activeSelf != active) _segments[i].gameObject.SetActive(active);
            }
        }

        private void CreateSegment()
        {
            Transform seg = Instantiate(_segmentTemplate, _segmentRoot);
            seg.gameObject.SetActive(false);
            _segments.Add(seg);
        }

        private void MarkCell(Vector3 world)
        {
            Vector2Int cell = _grid.WorldToCell(world);
            if (_grid.IsInside(cell)) _grid.SetOccupied(cell, true);
        }

        private Vector3 Forward()
        {
            return Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        }
    }
}
