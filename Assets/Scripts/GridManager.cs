using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// Дискретная логика поля: размеры, границы, занятые клетки и перевод клетки в мировые координаты.
    /// Визуал поля строит BoardView.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Размер поля")]
        [Tooltip("Количество клеток по оси X")]
        [SerializeField, Min(5)] private int _width = 20;

        [Tooltip("Количество клеток по оси Z")]
        [SerializeField, Min(5)] private int _height = 20;

        [Tooltip("Размер одной клетки в мировых единицах")]
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;

        private bool[] _occupied;
        private int _occupiedCount;

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public int CellCount => _width * _height;
        public Vector3 Center => transform.position;

        private void Awake()
        {
            _occupied = new bool[CellCount];
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
        }

        /// <summary>Клетка должна быть внутри поля (проверяй IsInside заранее).</summary>
        public bool IsOccupied(Vector2Int cell)
        {
            return _occupied[ToIndex(cell)];
        }

        public void SetOccupied(Vector2Int cell, bool value)
        {
            int i = ToIndex(cell);
            if (_occupied[i] == value) return;
            _occupied[i] = value;
            _occupiedCount += value ? 1 : -1;
        }

        public void ClearAll()
        {
            System.Array.Clear(_occupied, 0, _occupied.Length);
            _occupiedCount = 0;
        }

        /// <summary>Случайная свободная клетка без аллокаций: выбираем k-ю свободную по порядку.</summary>
        public bool TryGetRandomFreeCell(out Vector2Int cell)
        {
            int free = _occupied.Length - _occupiedCount;
            if (free <= 0)
            {
                cell = default;
                return false;
            }

            int k = Random.Range(0, free);
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (_occupied[i]) continue;
                if (k == 0)
                {
                    cell = new Vector2Int(i % _width, i / _width);
                    return true;
                }
                k--;
            }

            cell = default;
            return false;
        }

        /// <summary>Центр клетки на уровне поверхности поля (y = 0 относительно сетки).</summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            float x = (cell.x - (_width - 1) * 0.5f) * _cellSize;
            float z = (cell.y - (_height - 1) * 0.5f) * _cellSize;
            return transform.position + new Vector3(x, 0f, z);
        }

        private int ToIndex(Vector2Int cell)
        {
            return cell.y * _width + cell.x;
        }
    }
}
