using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Змейка: дискретные шаги по сетке, буфер ввода, рост, столкновения.
    /// Визуал (кубы) плавно интерполируется между клетками предыдущего и текущего шага.
    /// </summary>
    public class SnakeController : MonoBehaviour
    {
        private const int INPUT_BUFFER_SIZE = 2;

        [Header("Ссылки")]
        [Tooltip("Сетка поля")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Спавнер еды (чтобы знать, где еда)")]
        [SerializeField] private FoodSpawner _food;

        [Tooltip("Куб головы")]
        [SerializeField] private Transform _head;

        [Tooltip("Неактивный шаблон сегмента тела для пула")]
        [SerializeField] private Transform _segmentTemplate;

        [Tooltip("Родитель для сегментов тела")]
        [SerializeField] private Transform _segmentRoot;

        [Header("Движение")]
        [Tooltip("Интервал шага в начале партии, сек")]
        [SerializeField, Min(0.02f)] private float _startStepInterval = 0.18f;

        [Tooltip("Минимальный интервал шага (предел ускорения), сек")]
        [SerializeField, Min(0.02f)] private float _minStepInterval = 0.07f;

        [Tooltip("Множитель интервала после каждой еды (меньше 1 — змейка ускоряется)")]
        [SerializeField, Range(0.8f, 1f)] private float _speedUpFactor = 0.97f;

        [Tooltip("Начальная длина змейки, включая голову")]
        [SerializeField, Min(2)] private int _startLength = 3;

        [Header("Визуал")]
        [Tooltip("Высота центра кубов над полем (доля клетки)")]
        [SerializeField] private float _bodyHeight = 0.5f;

        [Tooltip("Скорость поворота головы, град/с")]
        [SerializeField, Min(1f)] private float _turnSpeed = 900f;

        [Tooltip("Длительность сквош-стретча при поедании, сек")]
        [SerializeField, Min(0.01f)] private float _squashDuration = 0.25f;

        [Tooltip("Сила сквош-стретча")]
        [SerializeField, Range(0f, 0.6f)] private float _squashAmount = 0.3f;

        /// <summary>Позиция головы в мире в момент поедания.</summary>
        public event Action<Vector3> FoodEaten;

        /// <summary>Позиция головы в мире в момент смерти.</summary>
        public event Action<Vector3> Died;

        public bool IsRunning { get; set; }

        /// <summary>Поменять местами «влево» и «вправо»: нужно для вида спереди, где экран зеркален курсу змейки.</summary>
        public bool MirrorTurns { get; set; }
        public bool IsAlive { get; private set; }
        public int Length => _length;
        public Transform Head => _head;

        // _cells[0] — голова. _from — клетки на предыдущем шаге, для интерполяции.
        private Vector2Int[] _cells;
        private Vector2Int[] _from;
        private int _length;
        private Vector2Int _dir;

        private readonly Vector2Int[] _inputBuffer = new Vector2Int[INPUT_BUFFER_SIZE];
        private int _bufferCount;

        private float _timer;
        private float _interval;
        private float _squashTimer;
        private Vector3 _headBaseScale;

        // Кэш геометрии сетки: интерполяция считает позиции без обращений к Transform сетки.
        private Vector3 _visualOrigin;
        private float _cellSize;

        private readonly List<Transform> _segments = new List<Transform>();

        private void Awake()
        {
            _headBaseScale = _head.localScale;
            _segmentTemplate.gameObject.SetActive(false);

            // Буферы на максимум клеток поля: змейка длиннее поля не станет, аллокаций в игре не будет.
            // Читаем только сериализованный размер сетки, её Awake для этого не нужен.
            _cells = new Vector2Int[_grid.CellCount];
            _from = new Vector2Int[_grid.CellCount];

            // Пул сразу на всё поле, чтобы рост змейки никогда не вызывал Instantiate.
            for (int i = 1; i < _grid.CellCount; i++) CreateSegment();
        }

        /// <summary>Вернуть змейку в начальное состояние в центре поля.</summary>
        public void ResetSnake()
        {
            _grid.ClearAll();
            _cellSize = _grid.CellSize;
            _visualOrigin = _grid.CellToWorld(Vector2Int.zero) + Vector3.up * (_bodyHeight * _cellSize);

            _length = Mathf.Clamp(_startLength, 2, _grid.Width / 2);
            _dir = Vector2Int.right;
            var start = new Vector2Int(_grid.Width / 2, _grid.Height / 2);
            for (int i = 0; i < _length; i++)
            {
                _cells[i] = start - _dir * i;
                _from[i] = _cells[i];
                _grid.SetOccupied(_cells[i], true);
            }

            _bufferCount = 0;
            _timer = 0f;
            _interval = _startStepInterval;
            _squashTimer = _squashDuration;
            IsAlive = true;
            IsRunning = false;

            _head.localScale = _headBaseScale;
            _head.rotation = Quaternion.LookRotation(DirToWorld(_dir));
            SetActiveSegments(_length - 1);
            UpdateVisuals(1f);
        }

        private void Update()
        {
            if (!IsRunning || !IsAlive) return;

            ReadInput();

            _timer += Time.deltaTime;
            while (_timer >= _interval && IsAlive && IsRunning)
            {
                _timer -= _interval;
                Step();
            }

            UpdateVisuals(IsAlive ? _timer / _interval : 1f);
        }

        private void ReadInput()
        {
            bool left = GameInput.Left;
            bool right = GameInput.Right;
            if (MirrorTurns)
            {
                bool tmp = left;
                left = right;
                right = tmp;
            }

            if (left) EnqueueTurn(false);
            if (right) EnqueueTurn(true);
        }

        /// <summary>
        /// Поворот на 90° относительно последней команды в очереди (а не текущего курса),
        /// чтобы два быстрых нажатия «вправо, вправо» давали разворот за два шага.
        /// Относительный поворот всегда перпендикулярен, поэтому разворот на 180° невозможен.
        /// </summary>
        private void EnqueueTurn(bool clockwise)
        {
            if (_bufferCount >= INPUT_BUFFER_SIZE) return;
            Vector2Int last = _bufferCount > 0 ? _inputBuffer[_bufferCount - 1] : _dir;
            // Сетка: x — вправо, y — вперёд (+Z мира). По часовой стрелке (x, y) -> (y, -x).
            _inputBuffer[_bufferCount++] = clockwise ? new Vector2Int(last.y, -last.x) : new Vector2Int(-last.y, last.x);
        }

        private void Step()
        {
            if (_bufferCount > 0)
            {
                _dir = _inputBuffer[0];
                for (int i = 1; i < _bufferCount; i++) _inputBuffer[i - 1] = _inputBuffer[i];
                _bufferCount--;
            }

            Vector2Int newHead = _cells[0] + _dir;
            Vector2Int tail = _cells[_length - 1];
            bool grow = _food != null && _food.HasFood && newHead == _food.Cell;

            // Хвост на этом шаге уходит, поэтому въезд в клетку хвоста разрешён (если не растём).
            bool hitsSelf = _grid.IsInside(newHead) && _grid.IsOccupied(newHead) && !(newHead == tail && !grow);
            if (!_grid.IsInside(newHead) || hitsSelf)
            {
                Die();
                return;
            }

            if (!grow) _grid.SetOccupied(tail, false);

            for (int i = 0; i < _length; i++) _from[i] = _cells[i];
            for (int i = _length - 1; i > 0; i--) _cells[i] = _cells[i - 1];
            _cells[0] = newHead;
            _grid.SetOccupied(newHead, true);

            if (grow)
            {
                // Новый сегмент появляется на месте старого хвоста и стоит на месте этот шаг.
                _cells[_length] = tail;
                _from[_length] = tail;
                _length++;
                SetActiveSegments(_length - 1);

                // Масштабируем остаток таймера, иначе интерполяция дёрнется при смене интервала.
                float next = Mathf.Max(_minStepInterval, _interval * _speedUpFactor);
                _timer *= next / _interval;
                _interval = next;
                _squashTimer = 0f;
                FoodEaten?.Invoke(CellToVisual(newHead));
            }
        }

        private void Die()
        {
            IsAlive = false;
            for (int i = 0; i < _length; i++) _from[i] = _cells[i];
            UpdateVisuals(1f);
            Died?.Invoke(_head.position);
        }

        private void UpdateVisuals(float t)
        {
            t = Mathf.Clamp01(t);
            _head.position = Vector3.LerpUnclamped(CellToVisual(_from[0]), CellToVisual(_cells[0]), t);
            for (int i = 1; i < _length; i++)
                _segments[i - 1].position = Vector3.LerpUnclamped(CellToVisual(_from[i]), CellToVisual(_cells[i]), t);

            Quaternion targetRot = Quaternion.LookRotation(DirToWorld(_dir));
            _head.rotation = Quaternion.RotateTowards(_head.rotation, targetRot, _turnSpeed * Time.deltaTime);

            if (_squashTimer < _squashDuration)
            {
                _squashTimer += Time.deltaTime;
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
            while (_segments.Count < count) CreateSegment();
            for (int i = 0; i < _segments.Count; i++)
            {
                bool active = i < count;
                if (_segments[i].gameObject.activeSelf != active) _segments[i].gameObject.SetActive(active);
            }
        }

        /// <summary>Пул растёт только при поедании, если заготовленных сегментов не хватило.</summary>
        private void CreateSegment()
        {
            Transform seg = Instantiate(_segmentTemplate, _segmentRoot);
            seg.gameObject.SetActive(false);
            _segments.Add(seg);
        }

        private Vector3 CellToVisual(Vector2Int cell)
        {
            return _visualOrigin + new Vector3(cell.x * _cellSize, 0f, cell.y * _cellSize);
        }

        private static Vector3 DirToWorld(Vector2Int dir)
        {
            return new Vector3(dir.x, 0f, dir.y);
        }
    }
}
