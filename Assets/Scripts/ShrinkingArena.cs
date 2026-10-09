using System;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Режим «Выживание»: каждые N секунд арена сужается на клетку с каждой стороны.
    /// Красные полупрозрачные стены плавно съезжаются, граница для столкновений двигается вместе с ними.
    /// </summary>
    public class ShrinkingArena : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Параметры баланса")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Сетка поля (в неё пишется текущее сужение)")]
        [SerializeField] private GridManager _grid;

        [Tooltip("Стены опасной зоны: север, юг, восток, запад")]
        [SerializeField] private Transform[] _walls = new Transform[4];

        [Header("Вид")]
        [Tooltip("Высота стен опасной зоны")]
        [SerializeField, Min(0.1f)] private float _wallHeight = 1.4f;

        [Tooltip("Толщина стен опасной зоны (доля клетки)")]
        [SerializeField, Min(0.05f)] private float _wallThickness = 0.3f;

        [Tooltip("Скорость сдвига стен, клеток в секунду")]
        [SerializeField, Min(0.1f)] private float _moveSpeed = 0.5f;

        /// <summary>Арена начала сужаться (для звука и баннера).</summary>
        public event Action Shrunk;

        public bool IsRunning { get; set; }

        private bool _enabledForMode;
        private float _timer;
        private float _targetInset;
        private float _currentInset;

        public void ResetArena(bool enabledForMode)
        {
            _enabledForMode = enabledForMode;
            _timer = _config.ShrinkInterval;
            _targetInset = 0f;
            _currentInset = 0f;
            _grid.ArenaInset = 0f;
            for (int i = 0; i < _walls.Length; i++) _walls[i].gameObject.SetActive(enabledForMode);
            if (enabledForMode) PlaceWalls();
        }

        private void Update()
        {
            if (!_enabledForMode || !IsRunning) return;
            float dt = Time.deltaTime;

            _timer -= dt;
            if (_timer <= 0f)
            {
                _timer = _config.ShrinkInterval;
                float maxInset = (Mathf.Min(_grid.Width, _grid.Height) - _config.MinArenaSize) * 0.5f;
                if (_targetInset + 1f <= maxInset)
                {
                    _targetInset += 1f;
                    Shrunk?.Invoke();
                }
            }

            if (_currentInset < _targetInset)
            {
                _currentInset = Mathf.MoveTowards(_currentInset, _targetInset, _moveSpeed * dt);
                _grid.ArenaInset = _currentInset;
                PlaceWalls();
            }
        }

        private void PlaceWalls()
        {
            float cs = _grid.CellSize;
            Vector2 half = _grid.ArenaHalfExtents;
            float t = _wallThickness * cs;
            float y = _wallHeight * 0.5f;
            Vector3 c = _grid.Center;

            SetWall(0, c + new Vector3(0f, y, half.y + t * 0.5f), new Vector3(half.x * 2f + t * 2f, _wallHeight, t));
            SetWall(1, c + new Vector3(0f, y, -half.y - t * 0.5f), new Vector3(half.x * 2f + t * 2f, _wallHeight, t));
            SetWall(2, c + new Vector3(half.x + t * 0.5f, y, 0f), new Vector3(t, _wallHeight, half.y * 2f));
            SetWall(3, c + new Vector3(-half.x - t * 0.5f, y, 0f), new Vector3(t, _wallHeight, half.y * 2f));
        }

        private void SetWall(int index, Vector3 position, Vector3 scale)
        {
            if (index >= _walls.Length || _walls[index] == null) return;
            _walls[index].position = position;
            _walls[index].localScale = scale;
        }
    }
}
